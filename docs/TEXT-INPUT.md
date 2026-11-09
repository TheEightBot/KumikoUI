# Text input: the adapter contract

**Status:** the Core contract shipped with #39. The host adapters that implement it are #40 (iOS and Mac Catalyst), #41 (Android), #42 (Windows on MAUI) and #43 (Uno). Until each one lands, that host keeps feeding text through its hidden native text box, one character at a time, as `KumikoKeyEventArgs` with `Character` set.

## Why

KumikoUI draws its editors on the canvas, so there is no native text control for the platform's keyboard or input method to talk to. The previous design put a hidden native text box behind the grid, compared its contents after every change and reset it. That design had five problems:
- It destroyed input-method composition (Japanese, Chinese, Korean, dead keys).
- It split surrogate pairs.
- It left no room for undo, word selection or a correct caret.
- Each host did it differently.

Under the new contract, Core owns the text, and each host supplies a thin adapter. The adapter translates the platform's text-input protocol into calls on one Core interface.

## Roles

| Core: `DrawnTextBox` (implements `ITextInputSession`) | Host adapter |
|---|---|
| Holds the text, the selection, the composition and the undo history | Holds the platform's text-input object (`UITextInput`, an `InputConnection`, a `CoreTextEditContext`, Uno composition events) |
| Keeps the caret and selection on grapheme-cluster boundaries | Reports what the keyboard or input method did, in UTF-16 indices |
| Applies the input rules (single line, `NumericOnly`, `InputMask`, `InputValidator`, `MaxLength`) when text is committed | Shows and hides the soft keyboard, sets its type (text or number) |
| Draws the text, selection, caret and composition underline | Places the input method's candidate window, using the editor's bounds |
| Handles editing keys (arrows, Home/End, Backspace/Delete, Ctrl/Cmd+A/C/V/X/Z/Y) | Forwards keys that are not text to `KumikoInputController.HandleKey` |
| Raises clipboard requests through `EditSession` | Reads and writes the system clipboard |

`EditSession.ActiveTextInput` is the session of the open editor, or null when no edit is open or the editor takes no text. Every call happens on the UI thread.

## The interface

```csharp
public interface ITextInputSession
{
    string Text { get; }                    // includes any composition
    KumikoTextRange Selection { get; }      // UTF-16 Start/Length; empty at the caret
    int CursorPosition { get; }
    KumikoTextRange? Composition { get; }   // marked text, or null
    bool IsReadOnly { get; }
    event EventHandler? TextInputStateChanged;

    void InsertText(string text);
    void SetComposition(string text, int cursorInComposition);
    void CommitComposition();
    void CancelComposition();
    void DeleteBackward();
    void DeleteForward();
    void SetSelection(int anchor, int active);
}
```

### Indices

All indices count UTF-16 code units in `Text`, which is what UIKit, Android and WinUI use. An index the adapter sets inside a grapheme cluster moves to that cluster's start. Grapheme clusters include emoji sequences with skin tones or zero-width joiners, flags, and letters with combining marks.

### Committed text

`InsertText` inserts text that is final: a typed character, a word from the suggestion bar, or a dictation result. It replaces the composition if there is one, otherwise the selection. Line breaks become spaces and other control characters are dropped. If the editor's rules reject the result, nothing changes: for example letters in a numeric editor, text that doesn't fit the mask, or text the validator refuses. The adapter should then read `Text` and `Selection` again.

Consecutive typing is one undo step per word; a paste or a committed composition is always its own step.

### Composition

A typical sequence for typing "かな" with a Japanese input method:

| Call | `Text` | `Composition` |
|---|---|---|
| (start) | `ab` | null |
| `SetComposition("k", 1)` | `abk` | (2, 1) |
| `SetComposition("か", 1)` | `abか` | (2, 1) |
| `SetComposition("かな", 2)` | `abかな` | (2, 2) |
| `CommitComposition()` | `abかな` | null |

- The marked text is part of `Text` and is underlined. The input rules apply only when it is committed, so a numeric editor can show marked letters but commits only the digits.
- `SetComposition("", …)` and `CancelComposition()` restore the text and selection from before the composition began.
- `DeleteBackward` during a composition removes one grapheme from the marked text.
- A selection set outside the marked text commits it first.
- When an edit is committed while a composition is still active, `EditSession.CommitEdit` commits the composition first, so its text is part of the value.
- While a composition is active, Enter commits it and Escape cancels it. Neither commits or cancels the edit; `KumikoInputController` sends both keys to the editor first.

Platform mapping:

| Platform | Marked text | Commit | Cancel |
|---|---|---|---|
| iOS / Mac Catalyst (`UITextInput`) | `setMarkedText:selectedRange:` | `unmarkText`, `insertText:` | `setMarkedText:` with an empty string |
| Android (`InputConnection`) | `setComposingText` | `finishComposingText`, `commitText` | `setComposingText("")` |
| Windows (`CoreTextEditContext`) | `TextUpdating` within a `CompositionStarted`/`CompositionCompleted` pair | `CompositionCompleted` | `NotifyFocusLeave` |
| Uno | composition events of the hidden text box | the composition-completed event | an empty composition |

### State changes

`TextInputStateChanged` is raised when the text, selection or composition change by any route except the adapter's own calls on the interface. Examples are a keyboard shortcut handled in Core, a pointer gesture (click, double click, drag), undo or redo, setting `DrawnTextBox.Text`, or `CommitEdit` committing a composition.

The adapter uses it to tell the platform its view is stale:
- iOS: `inputDelegate.textWillChange`/`textDidChange` and `selectionWillChange`/`selectionDidChange`.
- Android: `InputMethodManager.updateSelection`, or `restartInput` when the text changed.
- Windows: `NotifyTextChanged`/`NotifySelectionChanged`.

Changes the adapter makes itself raise nothing, so there is no feedback loop.

### Keys

Keys that are not text go to `KumikoInputController.HandleKey` as before: navigation, Tab, Enter, Escape, F2 and shortcuts. The editor handles the editing keys among them.

Text that arrives as a key event can set `KumikoKeyEventArgs.Text`, a string, instead of `Character`. A surrogate pair or a dead-key sequence then arrives whole.

Shortcut detection accepts Control or Meta: `KumikoKeyEventArgs.HasCommand`, where Meta is Command on Apple keyboards. Ctrl/Cmd+Z undoes; Ctrl/Cmd+Shift+Z and Ctrl/Cmd+Y redo.

### Clipboard

- Ctrl/Cmd+C and Ctrl/Cmd+X raise `EditSession.ClipboardCopyRequested` with the selected text. The host writes it to the system clipboard.
- Ctrl/Cmd+V raises `EditSession.ClipboardPasteRequested`. The host sets `ClipboardRequestEventArgs.Text` synchronously; an empty value pastes nothing.
- Pasted text is single-line, filtered by `NumericOnly`, and checked by the mask, validator and maximum length like any other input.

Copying grid selections outside an editor is #44.

### Pointer

Pointer input reaches the editor through `KumikoInputController` as before:
- **Press** places the caret; with Shift, it extends the selection.
- **Drag** selects, and scrolls when the pointer is past either edge.
- **Double click** selects a word (or a run of spaces or punctuation). The editor counts clicks itself from `KumikoPointerEventArgs.TimestampMs` and position, and also honors `ClickCount` from the host.
- **Triple click** selects all.

On touch, an adapter shows the platform's own selection handles and edit menu as part of its host issue; they call `SetSelection`.

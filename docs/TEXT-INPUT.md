# Text input: the adapter contract

**Status:** the Core contract shipped with #39, and every host drives it: #40 (iOS and Mac Catalyst: `KeyInputResponder` and `AppleTextInputBridge` in KumikoUI.Maui), #41 (Android: see [Android](#android)), #42 (Windows on MAUI) and #43 (Uno: `KumikoUI.Uno.Input.KumikoTextInputAdapter`).

**Windows on MAUI** (#42) has no hidden text box. The grid's platform view is a tab stop; its `PreviewKeyDown` sends keys to `KumikoInputController.HandleKey` and `CharacterReceived` sends characters (a surrogate pair joined into `Text`). While a text editor is open, a `CoreTextEditContext` is attached to its session: `TextUpdating` within `CompositionStarted`/`CompositionCompleted` is marked text, other `TextUpdating` is committed text, `SelectionUpdating` sets the selection, `LayoutRequested` gets the edited cell's screen rectangle, and `TextInputStateChanged` becomes `NotifyTextChanged`/`NotifySelectionChanged`. The bookkeeping (what the text services last saw, and the change that brings them back in step) is `TextEditContextState`, which has no Windows dependency and is unit-tested. Windows documents `CoreTextServicesManager` for desktop apps as available only in Insider builds; where it can't be created, or delivers no text, the adapter falls back to `CharacterReceived`, so typing and text committed by an input method's own window still work, without in-place composition.

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
| Uno | `TextCompositionStarted`/`TextCompositionChanged` of the hidden text box, which mirrors the editor's text | `TextCompositionEnded` | an empty composition, then `TextCompositionEnded` |

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

## Android

The MAUI host on Android has no hidden `Entry`. Its adapter has three parts:

- `KumikoTextInputView`: a native view added to the grid's platform view. It draws nothing and takes no touches, and it holds the keyboard focus.
- `KumikoInputConnection`: the `BaseInputConnection` its `onCreateInputConnection` returns.
- `AndroidTextInputMapper`: the platform-independent mapping between them and `ITextInputSession`, unit-tested in `KumikoUI.Maui.Tests`.

### Focus and the soft keyboard

- Pressing the grid focuses the input view, so a hardware keyboard drives the grid without an editor open. The soft keyboard doesn't appear, because the view is a text editor only while a cell editor or the filter popup's search box is open.
- When an edit begins, the view takes focus, the input method restarts with the editor's keyboard type, and `InputMethodManager.showSoftInput` shows the keyboard.
- When the edit ends, the keyboard is hidden and the connection is dropped.
- An edit that ends and immediately begins again (Tab, or Enter with `DismissKeyboardOnEnter` off) keeps the keyboard up.
- A tap during an edit brings back a keyboard the user dismissed with Back.

### Input connection

| `InputConnection` call | Core |
|---|---|
| `setComposingText(text, pos)` | `SetComposition`. `pos` is relative: `> 0` counts from the end of the text (1 = just after it), `<= 0` from its start. Core keeps the caret inside the composition, so the position is clamped there. |
| `commitText(text, pos)` | `InsertText`, then the caret as `pos` asks. `"\n"` is Enter and `"\t"` is Tab, as keys. Committing `""` over a selection deletes it. |
| `finishComposingText` | `CommitComposition` |
| `setComposingRegion(a, b)` | Commits any composition, then marks the existing text as the composition (selection + `SetComposition` with the same text). |
| `deleteSurroundingText(before, after)` | Commits any composition, then deletes the ranges before and after the selection and keeps the selection. Each range is UTF-16 code units, widened to whole grapheme clusters, so `(1, 0)` before 👍🏽 deletes the whole emoji. |
| `deleteSurroundingTextInCodePoints` | The same, with the lengths counted in code points. |
| `setSelection(a, b)` | `SetSelection`; out-of-range indices are ignored. |
| `getTextBeforeCursor`, `getTextAfterCursor`, `getSelectedText`, `getSurroundingText`, `getExtractedText` | Read from `Text` and `Selection`. |
| `sendKeyEvent` | The same path as a hardware key. |
| `performEditorAction` | Enter. |
| `performContextMenuAction` (select all, cut, copy, paste) | Ctrl+A, X, C, V. |
| `beginBatchEdit` / `endBatchEdit` | The state is reported once, at the end of the batch. |

After the input method's calls, the adapter reports the new selection and composition with `InputMethodManager.updateSelection`; a monitored extract gets `updateExtractedText`. When Core changes the text itself (undo, paste, a shortcut, a pointer gesture), the adapter calls `restartInput`. When only the selection moves, it calls `updateSelection`.

With no text surface (the open editor is a combo box, a date picker or a picker), committed text becomes `KumikoKeyEventArgs` with `Text` set, deletions become Backspace and Delete, and a composition is typed when it finishes.

### Keyboard type (`EditorInfo`)

| Editor | `inputType` |
|---|---|
| Text editor | `TYPE_CLASS_TEXT`: suggestions on, no autocorrect |
| Numeric editor (`NumericOnly`) | `TYPE_CLASS_NUMBER \| SIGNED`, plus `DECIMAL` unless the cell holds an integer |
| `InputMask` of `#` placeholders only | `TYPE_CLASS_NUMBER` |
| Any other `InputMask` | `TYPE_CLASS_TEXT \| NO_SUGGESTIONS` |
| Filter popup search box | `TYPE_CLASS_TEXT \| VARIATION_FILTER \| NO_SUGGESTIONS` |
| No text surface | `TYPE_CLASS_TEXT \| NO_SUGGESTIONS` |

`imeOptions` is `IME_ACTION_NEXT` when Enter goes on to edit the next cell (`DismissKeyboardOnEnter` off), and `IME_ACTION_DONE` otherwise. It always includes `IME_FLAG_NO_EXTRACT_UI | IME_FLAG_NO_FULLSCREEN`, so landscape phones don't hide the grid behind a full-screen editor. The initial selection and, on Android 11 and later, the surrounding text are set too.

### Hardware keys

The input view's `dispatchKeyEvent` maps key codes and `MetaState` (Shift, Alt, Ctrl, Meta) to Core keys:
- the arrow keys, Home, End, Page Up and Page Down;
- Tab and Shift+Tab, left to Android when the grid has no further tab stop, so focus moves on;
- Enter (and numpad Enter), Escape, F2, Space, Backspace and Delete;
- Ctrl or Meta with A, C, V, X, Y or Z.

Other keys type their character. Alt (AltGr) chooses the character and isn't a modifier, and dead keys compose with the next letter (´ then e types é). In an open text editor, typed text and plain Backspace and Delete edit it directly, as the input method's own calls do. Escape is taken in `onKeyPreIme`, before the input method sees it, unless a composition is active: then it belongs to the input method.

### Keeping the edited cell visible

The input view is placed over the edited cell, so the window's `adjustPan` and an enclosing scroll view bring that cell into view when the keyboard appears. When the window doesn't pan, the grid reads the keyboard's top from the root window insets (`WindowInsetsCompat.Type.ime()`) and scrolls the edited cell above it. A cell in a frozen row, or in the last rows, can only move as far as the grid scrolls.

For an app whose grid fills the page, set the activity's soft-input mode. `adjustResize` shrinks the window, and the grid scrolls the cell into the smaller viewport. `adjustPan` pans the window to the cell instead.

```csharp
// Platforms/Android/MainActivity.cs
[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true,
    WindowSoftInputMode = SoftInput.AdjustResize)]
public class MainActivity : MauiAppCompatActivity { }
```

In shared code, set it with `Application.Current.On<Microsoft.Maui.Controls.PlatformConfiguration.Android>().UseWindowSoftInputModeAdjust(WindowSoftInputModeAdjust.Resize)`. On Android 15 and later, apps are drawn edge to edge and `adjustResize` no longer shrinks the window. The grid's inset-based scrolling still keeps the cell above the keyboard.

## Keeping the edited cell above the on-screen keyboard (iOS and Uno)

On iOS the grid does what Android's inset check does (#258). While a cell editor is open, it follows `UIKeyboardWillChangeFrame` and `UIKeyboardWillHide`, works out how much of the grid's bottom the keyboard covers (the keyboard frame converted into the grid's own coordinates), scrolls the edited row above the keyboard's top edge, and sets `ScrollState.BottomInset` to the covered height. The inset extends the scroll range, so the last rows can scroll clear of the keyboard. It is set back to 0 when the keyboard hides or the edit ends. The Uno heads (iOS, Android, WebAssembly) do the same from `InputPane.Showing` and `Hiding`, reading `OccludedRect` and the grid's place in the window. Where the platform already shrinks the window for the keyboard, the grid's bottom is the keyboard's top, nothing is covered, and nothing changes. A keyboard that does not reach the grid's bottom edge (a floating iPad keyboard) is ignored.

A newly edited cell is checked once per keyboard change, so a user who scrolls the cell away is not pulled back. A cell in a frozen row does not scroll, and a grid whose visible part above the keyboard is shorter than the header, summary and one row cannot show the cell; give the grid more room (for example by hiding the page's toolbar while editing). The filter popup is anchored under the header and does not scroll, so its search box is not moved. On Mac Catalyst no on-screen keyboard appears and nothing happens.

The geometry (covered height, scroll distance, scroll with an inset) is `KumikoKeyboardAvoidance` in `KumikoUI.Core.Layout`, shared by every host and unit-tested; the Android code above uses it too, without an inset.

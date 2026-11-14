using System.Globalization;
using System.Text;
using KumikoUI.Core.Components;
using KumikoUI.Core.Input;

namespace KumikoUI.Maui;

/// <summary>
/// The platform-independent half of the Android text-input adapter (#41). It maps what an Android input method
/// does through an <c>InputConnection</c> (<c>setComposingText</c>, <c>commitText</c>, <c>finishComposingText</c>,
/// <c>deleteSurroundingText</c>, <c>setSelection</c>, <c>setComposingRegion</c>, ...) onto Core's
/// <see cref="ITextInputSession"/>, maps hardware key codes and meta state to <see cref="KumikoKey"/> and
/// <see cref="InputModifiers"/>, and chooses the soft keyboard's input type. It has no Android dependency, so it
/// is unit-tested on a plain .NET runtime; <c>KumikoInputConnection</c> and <c>KumikoTextInputView</c> are the thin
/// Android shells around it.
/// </summary>
/// <remarks>
/// <para>
/// Android counts text in UTF-16 code units, like Core. Core keeps its caret and selection on grapheme-cluster
/// boundaries, so a deletion the input method asks for in code units is widened to whole clusters: deleting one
/// code unit before an emoji with a skin tone removes the whole emoji, never half a surrogate pair.
/// </para>
/// <para>
/// With no <see cref="Session"/> (the open editor takes no text, such as a combo box or a date picker), committed
/// text and deletions are turned into key events for <see cref="KeySink"/>, the way the hidden <c>Entry</c> used to
/// deliver them.
/// </para>
/// </remarks>
internal sealed class AndroidTextInputMapper
{
    // ── android.view.KeyEvent key codes ──
    internal const int KeycodeDpadUp = 19;
    internal const int KeycodeDpadDown = 20;
    internal const int KeycodeDpadLeft = 21;
    internal const int KeycodeDpadRight = 22;
    internal const int KeycodeA = 29;
    internal const int KeycodeC = 31;
    internal const int KeycodeV = 50;
    internal const int KeycodeX = 52;
    internal const int KeycodeY = 53;
    internal const int KeycodeZ = 54;
    internal const int KeycodeTab = 61;
    internal const int KeycodeSpace = 62;
    internal const int KeycodeEnter = 66;
    internal const int KeycodeDel = 67;
    internal const int KeycodePageUp = 92;
    internal const int KeycodePageDown = 93;
    internal const int KeycodeEscape = 111;
    internal const int KeycodeForwardDel = 112;
    internal const int KeycodeMoveHome = 122;
    internal const int KeycodeMoveEnd = 123;
    internal const int KeycodeF2 = 132;
    internal const int KeycodeNumpadEnter = 160;

    // ── android.view.KeyEvent meta state ──
    internal const int MetaShiftOn = 0x1;
    internal const int MetaAltOn = 0x2;
    internal const int MetaCtrlOn = 0x1000;
    internal const int MetaMetaOn = 0x10000;

    // ── android.view.KeyCharacterMap: a dead key's getUnicodeChar ──
    internal const int CombiningAccent = unchecked((int)0x80000000);
    internal const int CombiningAccentMask = 0x7FFFFFFF;

    // ── android.text.InputType ──
    internal const int TypeClassText = 0x1;
    internal const int TypeClassNumber = 0x2;
    internal const int TypeTextVariationFilter = 0xB0;
    internal const int TypeTextFlagNoSuggestions = 0x80000;
    internal const int TypeNumberFlagSigned = 0x1000;
    internal const int TypeNumberFlagDecimal = 0x2000;

    // ── android.view.inputmethod.EditorInfo ime options ──
    internal const int ImeActionNext = 0x5;
    internal const int ImeActionDone = 0x6;
    internal const int ImeFlagNoFullscreen = 0x2000000;
    internal const int ImeFlagNoExtractUi = 0x10000000;

    // ── android.R.id context menu actions (InputConnection.performContextMenuAction) ──
    internal const int IdSelectAll = 0x0102001F;
    internal const int IdCut = 0x01020020;
    internal const int IdCopy = 0x01020021;
    internal const int IdPaste = 0x01020022;
    internal const int IdPasteAsPlainText = 0x01020031;

    private ITextInputSession? _session;
    private string? _pendingComposition; // composition text with no session, delivered on finish
    private int _pendingAccent;           // a dead key's combining accent, waiting for its letter
    private TextInputSnapshot _reported = TextInputSnapshot.Empty;

    /// <summary>The text surface the input method edits, or null when the open editor takes no text.</summary>
    public ITextInputSession? Session => _session;

    /// <summary>
    /// Receives key events the mapper produces: Enter and Tab committed as text, and, with no
    /// <see cref="Session"/>, committed text and deletions. Returns whether the key was handled.
    /// </summary>
    public Func<KumikoKeyEventArgs, bool>? KeySink { get; set; }

    /// <summary>
    /// Points the mapper at another text surface (or none). Clears a pending dead key and a sessionless
    /// composition; the next report starts from the new surface's state.
    /// </summary>
    public void Bind(ITextInputSession? session)
    {
        _session = session;
        _pendingComposition = null;
        _pendingAccent = 0;
        _reported = Current;
    }

    // ── InputConnection operations ───────────────────────────────

    /// <summary>
    /// <c>setComposingText</c>: replaces the composition (or the selection) with marked text. Android's
    /// <paramref name="newCursorPosition"/> is relative: above zero it counts from the end of the text (1 = just
    /// after it), otherwise from its start. Core keeps the caret inside the composition, so it is clamped there.
    /// </summary>
    public void SetComposingText(string text, int newCursorPosition)
    {
        text ??= string.Empty;
        if (_session is not { } session)
        {
            _pendingComposition = text.Length == 0 ? null : text;
            return;
        }
        if (session.IsReadOnly) return;

        int cursor = newCursorPosition > 0 ? text.Length + newCursorPosition - 1 : newCursorPosition;
        session.SetComposition(text, Math.Clamp(cursor, 0, text.Length));
    }

    /// <summary>
    /// <c>commitText</c>: inserts final text in place of the composition (or the selection), then places the caret
    /// as <paramref name="newCursorPosition"/> asks (relative, as in <see cref="SetComposingText"/>). A line break
    /// is Enter and a tab is Tab, as keys.
    /// </summary>
    public void CommitText(string text, int newCursorPosition)
    {
        text ??= string.Empty;
        if (text is "\n" or "\r" or "\r\n")
        {
            FinishComposingText();
            SendKey(KumikoKey.Enter);
            return;
        }
        if (text == "\t")
        {
            FinishComposingText();
            SendKey(KumikoKey.Tab);
            return;
        }

        if (_session is not { } session)
        {
            _pendingComposition = null;
            if (text.Length > 0) SendText(text);
            return;
        }
        if (session.IsReadOnly) return;

        int start = session.Composition?.Start ?? session.Selection.Start;
        if (text.Length == 0 && session.Composition == null)
        {
            // Committing nothing over a selection deletes it (Core ignores empty input)
            if (!session.Selection.IsEmpty) session.DeleteBackward();
        }
        else
        {
            session.InsertText(text);
        }

        if (newCursorPosition == 1) return;
        int end = session.CursorPosition;
        int position = newCursorPosition > 0 ? end + newCursorPosition - 1 : start + newCursorPosition;
        position = Math.Clamp(position, 0, session.Text.Length);
        session.SetSelection(position, position);
    }

    /// <summary><c>finishComposingText</c>: the composition becomes ordinary text.</summary>
    public void FinishComposingText()
    {
        if (_session is { } session)
        {
            session.CommitComposition();
            return;
        }
        if (_pendingComposition is { } pending)
        {
            _pendingComposition = null;
            SendText(pending);
        }
    }

    /// <summary>
    /// <c>deleteSurroundingText</c>: deletes <paramref name="before"/> code units before the selection and
    /// <paramref name="after"/> code units after it, keeping the selection. Each range is widened to whole grapheme
    /// clusters. An active composition is committed first. Returns false for a negative length, as Android does.
    /// </summary>
    public bool DeleteSurroundingText(int before, int after)
    {
        if (before < 0 || after < 0) return false;
        if (_session is not { } session)
        {
            if (before > 0) SendKey(KumikoKey.Backspace);
            if (after > 0) SendKey(KumikoKey.Delete);
            return true;
        }
        if (session.IsReadOnly || (before == 0 && after == 0)) return true;

        session.CommitComposition();
        string text = session.Text;
        var selection = session.Selection;
        bool caretAtStart = session.CursorPosition == selection.Start && !selection.IsEmpty;

        int afterEnd = ClusterCeiling(text, (int)Math.Min((long)selection.End + after, text.Length));
        int beforeStart = ClusterFloor(text, Math.Max(0, selection.Start - before));

        // After first, so the indices before the selection still hold
        if (afterEnd > selection.End)
        {
            session.SetSelection(selection.End, afterEnd);
            session.DeleteBackward();
        }
        if (beforeStart < selection.Start)
        {
            session.SetSelection(beforeStart, selection.Start);
            session.DeleteBackward();
        }

        int removed = selection.Start - beforeStart;
        int start = selection.Start - removed, end = selection.End - removed;
        if (caretAtStart) session.SetSelection(end, start);
        else session.SetSelection(start, end);
        return true;
    }

    /// <summary>
    /// <c>deleteSurroundingTextInCodePoints</c>: as <see cref="DeleteSurroundingText"/>, with the lengths in code
    /// points (a surrogate pair counts once).
    /// </summary>
    public bool DeleteSurroundingTextInCodePoints(int before, int after)
    {
        if (before < 0 || after < 0) return false;
        if (_session is not { } session) return DeleteSurroundingText(before, after);

        string text = session.Text;
        var selection = session.Selection;
        int start = selection.Start;
        for (int i = 0; i < before && start > 0; i++)
            start -= start >= 2 && char.IsLowSurrogate(text[start - 1]) && char.IsHighSurrogate(text[start - 2]) ? 2 : 1;
        int end = selection.End;
        for (int i = 0; i < after && end < text.Length; i++)
            end += end + 1 < text.Length && char.IsHighSurrogate(text[end]) && char.IsLowSurrogate(text[end + 1]) ? 2 : 1;
        return DeleteSurroundingText(selection.Start - start, end - selection.End);
    }

    /// <summary>
    /// <c>setSelection</c>: selects from <paramref name="start"/> to <paramref name="end"/> (the caret). Out of range
    /// indices are ignored, as in Android's editors.
    /// </summary>
    public void SetSelection(int start, int end)
    {
        if (_session is not { } session) return;
        int length = session.Text.Length;
        if (start < 0 || end < 0 || start > length || end > length) return;
        session.SetSelection(start, end);
    }

    /// <summary>
    /// <c>setComposingRegion</c>: marks existing text as the composition, which an input method does to correct a
    /// word the caret moved into. The region is widened to whole clusters. Core keeps the caret inside a
    /// composition, so a caret outside the region moves to its nearer edge. An empty region finishes the
    /// composition.
    /// </summary>
    public void SetComposingRegion(int start, int end)
    {
        if (_session is not { } session || session.IsReadOnly) return;
        session.CommitComposition();

        string text = session.Text;
        int a = Math.Clamp(Math.Min(start, end), 0, text.Length);
        int b = Math.Clamp(Math.Max(start, end), 0, text.Length);
        if (a == b) return;
        a = ClusterFloor(text, a);
        b = ClusterCeiling(text, b);

        int caret = session.CursorPosition;
        string region = text.Substring(a, b - a);
        session.SetSelection(a, b);
        session.SetComposition(region, Math.Clamp(caret - a, 0, region.Length));
    }

    /// <summary>
    /// <c>replaceText</c> (Android 14): replaces the range from <paramref name="start"/> to <paramref name="end"/>
    /// with committed text and places the caret as <see cref="CommitText"/> does.
    /// </summary>
    public void ReplaceText(int start, int end, string text, int newCursorPosition)
    {
        if (_session is not { } session)
        {
            CommitText(text, newCursorPosition);
            return;
        }
        if (session.IsReadOnly) return;
        session.CommitComposition();
        int length = session.Text.Length;
        int a = Math.Clamp(Math.Min(start, end), 0, length);
        int b = Math.Clamp(Math.Max(start, end), 0, length);
        session.SetSelection(a, b);
        CommitText(text, newCursorPosition);
    }

    /// <summary><c>getTextBeforeCursor</c>: up to <paramref name="length"/> code units before the selection.</summary>
    public string GetTextBeforeCursor(int length)
    {
        if (_session is not { } session || length <= 0) return string.Empty;
        int end = session.Selection.Start;
        int start = Math.Max(0, end - length);
        return session.Text.Substring(start, end - start);
    }

    /// <summary><c>getTextAfterCursor</c>: up to <paramref name="length"/> code units after the selection.</summary>
    public string GetTextAfterCursor(int length)
    {
        if (_session is not { } session || length <= 0) return string.Empty;
        string text = session.Text;
        int start = session.Selection.End;
        return text.Substring(start, Math.Min(length, text.Length - start));
    }

    /// <summary><c>getSelectedText</c>: the selected text, or null when nothing is selected.</summary>
    public string? GetSelectedText()
    {
        if (_session is not { } session || session.Selection.IsEmpty) return null;
        var selection = session.Selection;
        return session.Text.Substring(selection.Start, selection.Length);
    }

    /// <summary>
    /// The key a context-menu action (<c>performContextMenuAction</c>: select all, cut, copy, paste) stands for,
    /// pressed with Control; null for others.
    /// </summary>
    public static KumikoKey? MapContextMenuAction(int id) => id switch
    {
        IdSelectAll => KumikoKey.A,
        IdCut => KumikoKey.X,
        IdCopy => KumikoKey.C,
        IdPaste or IdPasteAsPlainText => KumikoKey.V,
        _ => null,
    };

    // ── State reported to the input method ───────────────────────

    /// <summary>The text, selection and composition the input method should see now.</summary>
    public TextInputSnapshot Current => _session is { } session
        ? new TextInputSnapshot(session.Text, session.Selection.Start, session.Selection.End,
            session.Composition?.Start ?? -1, session.Composition?.End ?? -1)
        : TextInputSnapshot.Empty;

    /// <summary>Records the current state as known to the input method (it was just given it, by a restart).</summary>
    public void MarkReported() => _reported = Current;

    /// <summary>
    /// After the input method's own calls: whether the selection or composition differs from what it was last told
    /// (<c>InputMethodManager.updateSelection</c>). The input method expects the text it changed, so text alone is
    /// not a reason to report.
    /// </summary>
    public bool TryTakeSelectionReport(out TextInputSnapshot state)
    {
        state = Current;
        bool changed = !state.SameSelection(_reported);
        _reported = state;
        return changed;
    }

    /// <summary>
    /// After a change Core made (<see cref="ITextInputSession.TextInputStateChanged"/>: a shortcut, undo, a pointer
    /// gesture): <see cref="TextInputReport.Restart"/> when the text differs from what the input method knows, so
    /// it reads it again; <see cref="TextInputReport.Selection"/> when only the selection or composition moved.
    /// </summary>
    public TextInputReport TakeExternalChange(out TextInputSnapshot state)
    {
        state = Current;
        var report = !string.Equals(state.Text, _reported.Text, StringComparison.Ordinal) ? TextInputReport.Restart
            : !state.SameSelection(_reported) ? TextInputReport.Selection
            : TextInputReport.None;
        _reported = state;
        return report;
    }

    // ── Keys ──────────────────────────────────────────────────────

    /// <summary>
    /// Maps a key press. Returns false for a key the grid does not use. Returns true with
    /// <paramref name="key"/> <see cref="KumikoKey.None"/> for a dead key, which waits for the next key;
    /// <see cref="KumikoKey.Character"/> with <paramref name="text"/> for typed text; otherwise a grid key.
    /// Letters are shortcut keys (Ctrl+A/C/V/X/Y/Z) only with Control or Meta; plain, they are text. Space is
    /// <see cref="KumikoKey.Space"/> with the text " ".
    /// </summary>
    /// <param name="keyCode">The Android key code.</param>
    /// <param name="metaState">The event's meta state.</param>
    /// <param name="unicodeChar">The event's <c>getUnicodeChar(metaState)</c>: 0 for none, a dead key with
    /// <see cref="CombiningAccent"/> set.</param>
    /// <param name="key">The grid key.</param>
    /// <param name="text">The text the key types, if any.</param>
    /// <param name="modifiers">The modifiers held.</param>
    public bool MapKeyDown(int keyCode, int metaState, int unicodeChar, out KumikoKey key, out string? text, out InputModifiers modifiers)
    {
        modifiers = MapMetaState(metaState);
        bool command = (modifiers & (InputModifiers.Control | InputModifiers.Meta)) != 0;
        text = null;

        if (MapKeyCode(keyCode, command) is { } mapped)
        {
            key = mapped;
            if (mapped == KumikoKey.Space && !command)
                text = _pendingAccent != 0 ? TakePendingAccent() : " ";
            else
                _pendingAccent = 0;
            return true;
        }

        key = KumikoKey.None;
        if (unicodeChar == 0 || command) return false;

        text = ApplyDeadKey(unicodeChar);
        if (text != null) key = KumikoKey.Character;
        // Alt (AltGr on many layouts) picks the character; it is not a shortcut, so the text is plain typing
        modifiers &= ~InputModifiers.Alt;
        return true;
    }

    /// <summary>The grid key for an Android key code; letters only with <paramref name="command"/>.</summary>
    public static KumikoKey? MapKeyCode(int keyCode, bool command) => keyCode switch
    {
        KeycodeDpadUp => KumikoKey.Up,
        KeycodeDpadDown => KumikoKey.Down,
        KeycodeDpadLeft => KumikoKey.Left,
        KeycodeDpadRight => KumikoKey.Right,
        KeycodeMoveHome => KumikoKey.Home,
        KeycodeMoveEnd => KumikoKey.End,
        KeycodePageUp => KumikoKey.PageUp,
        KeycodePageDown => KumikoKey.PageDown,
        KeycodeTab => KumikoKey.Tab,
        KeycodeEnter or KeycodeNumpadEnter => KumikoKey.Enter,
        KeycodeEscape => KumikoKey.Escape,
        KeycodeSpace => KumikoKey.Space,
        KeycodeForwardDel => KumikoKey.Delete,
        KeycodeDel => KumikoKey.Backspace,
        KeycodeF2 => KumikoKey.F2,
        KeycodeA when command => KumikoKey.A,
        KeycodeC when command => KumikoKey.C,
        KeycodeV when command => KumikoKey.V,
        KeycodeX when command => KumikoKey.X,
        KeycodeY when command => KumikoKey.Y,
        KeycodeZ when command => KumikoKey.Z,
        _ => null,
    };

    /// <summary>Android meta state as Core modifiers: Shift, Alt, Control and Meta.</summary>
    public static InputModifiers MapMetaState(int metaState)
    {
        var modifiers = InputModifiers.None;
        if ((metaState & MetaShiftOn) != 0) modifiers |= InputModifiers.Shift;
        if ((metaState & MetaAltOn) != 0) modifiers |= InputModifiers.Alt;
        if ((metaState & MetaCtrlOn) != 0) modifiers |= InputModifiers.Control;
        if ((metaState & MetaMetaOn) != 0) modifiers |= InputModifiers.Meta;
        return modifiers;
    }

    /// <summary>
    /// Applies dead keys to a typed character: a dead key (its accent flagged with <see cref="CombiningAccent"/>)
    /// is held and returns null; the next letter comes back composed with it (é from ´ then e). A second dead key or
    /// a space types the held accent on its own.
    /// </summary>
    public string? ApplyDeadKey(int unicodeChar)
    {
        if ((unicodeChar & CombiningAccent) != 0)
        {
            int accent = unicodeChar & CombiningAccentMask;
            string? previous = _pendingAccent != 0 ? TakePendingAccent() : null;
            _pendingAccent = accent;
            return previous;
        }

        if (unicodeChar is <= 0 or > 0x10FFFF || (unicodeChar is >= 0xD800 and <= 0xDFFF)) return null;
        string text = char.ConvertFromUtf32(unicodeChar);
        if (_pendingAccent == 0) return text;

        int pending = _pendingAccent;
        _pendingAccent = 0;
        if (unicodeChar == ' ') return SpacingAccent(pending);
        return (text + char.ConvertFromUtf32(pending)).Normalize(NormalizationForm.FormC);
    }

    private string TakePendingAccent()
    {
        string accent = SpacingAccent(_pendingAccent);
        _pendingAccent = 0;
        return accent;
    }

    /// <summary>The character a combining accent types on its own.</summary>
    private static string SpacingAccent(int combining) => combining switch
    {
        0x0300 => "`",
        0x0301 => "´",
        0x0302 => "^",
        0x0303 => "~",
        0x0308 => "¨",
        0x0327 => "¸",
        _ => char.ConvertFromUtf32(combining),
    };

    // ── Keyboard type ─────────────────────────────────────────────

    /// <summary>
    /// The <c>EditorInfo.inputType</c> for a text surface:
    /// <list type="bullet">
    /// <item>a numeric editor (<see cref="DrawnTextBox.NumericOnly"/>): the signed number keyboard, with a decimal
    /// separator unless the cell holds an integer;</item>
    /// <item>an editor with an <see cref="DrawnTextBox.InputMask"/> of digit placeholders only: the number keyboard;
    /// any other mask: text without suggestions;</item>
    /// <item>the filter popup's search box: filter text without suggestions;</item>
    /// <item>other text editors: plain text (suggestions on, no autocorrect);</item>
    /// <item>no text surface (keys typed into a combo box or date picker): text without suggestions, so the input
    /// method commits each character.</item>
    /// </list>
    /// </summary>
    /// <param name="session">The text surface, or null.</param>
    /// <param name="cellValue">The edited cell's value, which tells integers from decimals.</param>
    /// <param name="isFilterSearch">Whether the surface is the filter popup's search box.</param>
    public static int GetInputType(ITextInputSession? session, object? cellValue, bool isFilterSearch)
    {
        if (session == null) return TypeClassText | TypeTextFlagNoSuggestions;
        if (isFilterSearch) return TypeClassText | TypeTextVariationFilter | TypeTextFlagNoSuggestions;

        if (session is DrawnTextBox box)
        {
            if (box.NumericOnly)
                return TypeClassNumber | TypeNumberFlagSigned | (IsIntegral(cellValue) ? 0 : TypeNumberFlagDecimal);
            if (!string.IsNullOrEmpty(box.InputMask))
                return IsDigitMask(box.InputMask) ? TypeClassNumber : TypeClassText | TypeTextFlagNoSuggestions;
        }
        return TypeClassText;
    }

    /// <summary>
    /// The <c>EditorInfo.imeOptions</c>: the action key is Next when Enter goes on to edit the cell below, Done
    /// otherwise; no full-screen extract mode in landscape, which would hide the grid.
    /// </summary>
    public static int GetImeOptions(bool enterContinuesEditing) =>
        (enterContinuesEditing ? ImeActionNext : ImeActionDone) | ImeFlagNoExtractUi | ImeFlagNoFullscreen;

    private static bool IsIntegral(object? value) =>
        value is int or long or short or byte or sbyte or uint or ulong or ushort or nint or nuint
            or System.Numerics.BigInteger;

    private static bool IsDigitMask(string mask)
    {
        foreach (char c in mask)
        {
            if (c is 'A' or '*' || char.IsLetter(c)) return false;
        }
        return true;
    }

    // ── Helpers ───────────────────────────────────────────────────

    private void SendKey(KumikoKey key) => KeySink?.Invoke(new KumikoKeyEventArgs { Key = key });

    private void SendText(string text) => KeySink?.Invoke(new KumikoKeyEventArgs
    {
        Key = KumikoKey.Character,
        Text = text,
        Character = text.Length == 1 ? text[0] : null,
    });

    /// <summary>The grapheme-cluster boundary at or before <paramref name="index"/>.</summary>
    internal static int ClusterFloor(string text, int index)
    {
        if (index <= 0) return 0;
        if (index >= text.Length) return text.Length;
        int boundary = 0;
        while (boundary < text.Length)
        {
            int next = boundary + StringInfo.GetNextTextElementLength(text, boundary);
            if (next > index) return boundary;
            boundary = next;
        }
        return boundary;
    }

    /// <summary>The grapheme-cluster boundary at or after <paramref name="index"/>.</summary>
    internal static int ClusterCeiling(string text, int index)
    {
        int floor = ClusterFloor(text, index);
        return floor == index || floor >= text.Length ? floor : floor + StringInfo.GetNextTextElementLength(text, floor);
    }
}

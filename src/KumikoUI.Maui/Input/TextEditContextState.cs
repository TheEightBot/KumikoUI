using KumikoUI.Core.Input;

namespace KumikoUI.Maui;

/// <summary>
/// A change to report to the Windows text services (<c>CoreTextEditContext.NotifyTextChanged</c> and
/// <c>NotifySelectionChanged</c>) so their view of the text matches the editor's again.
/// </summary>
/// <param name="Start">Start of the replaced range, in the text the text services last saw.</param>
/// <param name="OldEnd">End of the replaced range, in the text the text services last saw.</param>
/// <param name="NewLength">Length of the text that replaced the range.</param>
/// <param name="Selection">The selection now.</param>
internal readonly record struct TextEditChange(int Start, int OldEnd, int NewLength, KumikoTextRange Selection)
{
    /// <summary>Whether the text changed; false when only the selection moved.</summary>
    public bool TextChanged => OldEnd > Start || NewLength > 0;
}

/// <summary>
/// The platform-independent half of the Windows text-input adapter (<c>CoreTextEditContext</c>): it remembers the
/// text and selection the text services last saw, turns their requests and updates into calls on the editor's
/// <see cref="ITextInputSession"/>, and works out the notification that brings them back in step when the editor
/// changed the text itself (a key, a pointer gesture, undo) or did not apply an update exactly as asked (input
/// rules, a maximum length, grapheme snapping). Indices are UTF-16 code units, as in both APIs.
/// </summary>
/// <remarks>
/// The text services only see the editor through this class: <see cref="GetText"/> and <see cref="GetSelection"/>
/// answer their requests, <see cref="ApplyTextUpdate"/> and <see cref="ApplySelectionUpdate"/> apply their edits,
/// and <see cref="TakeChange"/> returns what to notify. Kept free of WinRT types so it is unit-tested on any OS.
/// </remarks>
internal sealed class TextEditContextState
{
    private string _knownText = string.Empty;
    private KumikoTextRange _knownSelection;

    /// <summary>The editor the text services are attached to, or null.</summary>
    public ITextInputSession? Session { get; private set; }

    /// <summary>
    /// True between the text services' <c>CompositionStarted</c> and <c>CompositionCompleted</c>: text updates
    /// in between are marked text (an input method's composition), not committed text.
    /// </summary>
    public bool IsComposing { get; private set; }

    /// <summary>The text the text services last saw.</summary>
    public string KnownText => _knownText;

    /// <summary>The selection the text services last saw.</summary>
    public KumikoTextRange KnownSelection => _knownSelection;

    /// <summary>Attaches the text services to <paramref name="session"/> (or detaches them, for null) and takes its state as known.</summary>
    public void Attach(ITextInputSession? session)
    {
        Session = session;
        IsComposing = false;
        _knownText = session?.Text ?? string.Empty;
        _knownSelection = session != null ? ClampRange(session.Selection, _knownText.Length) : default;
    }

    /// <summary>The editor's text from <paramref name="start"/> to <paramref name="end"/>, clamped to the text (a <c>TextRequested</c>).</summary>
    public string GetText(int start, int end)
    {
        string text = Session?.Text ?? string.Empty;
        var range = Clamp(start, end, text.Length);
        return range.Start == 0 && range.Length == text.Length ? text : text.Substring(range.Start, range.Length);
    }

    /// <summary>The editor's selection (a <c>SelectionRequested</c>).</summary>
    public KumikoTextRange GetSelection()
    {
        var session = Session;
        return session == null ? default : ClampRange(session.Selection, session.Text.Length);
    }

    /// <summary>Marks the start of a composition (<c>CompositionStarted</c>).</summary>
    public void BeginComposition() => IsComposing = true;

    /// <summary>
    /// Marks the end of a composition (<c>CompositionCompleted</c>): the marked text becomes ordinary text, with
    /// the editor's input rules applied. A difference that leaves is reported by <see cref="TakeChange"/>.
    /// </summary>
    public void CompleteComposition()
    {
        IsComposing = false;
        if (Session is { Composition: not null } session)
            session.CommitComposition();
    }

    /// <summary>
    /// Applies a <c>TextUpdating</c>: the range from <paramref name="start"/> to <paramref name="end"/> of the text
    /// the text services last saw is replaced by <paramref name="text"/>, and the selection becomes
    /// <paramref name="newSelectionStart"/>–<paramref name="newSelectionEnd"/>. During a composition the text is
    /// marked text. Returns false (the update failed) when nothing was applied: no editor, a read-only one, a line
    /// break or tab (those are the Enter and Tab keys, which reach the grid as keys), or input the editor's rules
    /// refused. When the editor applied it differently, it returns true and <see cref="TakeChange"/> reports the
    /// difference.
    /// </summary>
    public bool ApplyTextUpdate(int start, int end, string? text, int newSelectionStart, int newSelectionEnd)
    {
        var session = Session;
        if (session == null || session.IsReadOnly) return false;
        text ??= string.Empty;
        if (!IsComposing && IsOnlyLineControl(text)) return false;

        string before = session.Text;
        var range = Clamp(start, end, before.Length);
        string expected = before.Remove(range.Start, range.Length).Insert(range.Start, text);

        if (IsComposing)
            ApplyComposition(session, before, range, text, newSelectionEnd);
        else
            ApplyCommitted(session, range, text, expected, newSelectionStart, newSelectionEnd);

        // Refused: the text services discard the update, so what they saw stays as it was.
        if (expected != before && session.Text == before)
            return false;

        _knownText = expected;
        _knownSelection = Clamp(newSelectionStart, newSelectionEnd, expected.Length);
        return true;
    }

    /// <summary>
    /// Applies a <c>SelectionUpdating</c>. The editor may move the ends to grapheme boundaries (or commit a
    /// composition the selection leaves); <see cref="TakeChange"/> reports that. False when there is no editor.
    /// </summary>
    public bool ApplySelectionUpdate(int start, int end)
    {
        var session = Session;
        if (session == null) return false;
        var range = Clamp(start, end, session.Text.Length);
        if (session.Selection != range)
            session.SetSelection(range.Start, range.End);
        _knownSelection = Clamp(start, end, _knownText.Length);
        return true;
    }

    /// <summary>
    /// The notification that brings the text services in step with the editor, or null when they already are.
    /// What it reports becomes the known state.
    /// </summary>
    public TextEditChange? TakeChange()
    {
        var session = Session;
        if (session == null) return null;
        string text = session.Text;
        var selection = ClampRange(session.Selection, text.Length);
        bool sameText = ReferenceEquals(text, _knownText) || text == _knownText;
        if (sameText && selection == _knownSelection) return null;

        var change = sameText
            ? new TextEditChange(0, 0, 0, selection)
            : Diff(_knownText, text, selection);
        _knownText = text;
        _knownSelection = selection;
        return change;
    }

    /// <summary>
    /// The smallest replacement that turns <paramref name="oldText"/> into <paramref name="newText"/> (common
    /// prefix and suffix removed), never splitting a surrogate pair.
    /// </summary>
    internal static TextEditChange Diff(string oldText, string newText, KumikoTextRange selection)
    {
        int max = Math.Min(oldText.Length, newText.Length);
        int prefix = 0;
        while (prefix < max && oldText[prefix] == newText[prefix]) prefix++;
        // A high surrogate at the end of the prefix belongs with the low surrogate that changed
        if (prefix > 0 && char.IsHighSurrogate(oldText[prefix - 1])) prefix--;

        int maxSuffix = max - prefix;
        int suffix = 0;
        while (suffix < maxSuffix && oldText[oldText.Length - 1 - suffix] == newText[newText.Length - 1 - suffix]) suffix++;
        // A low surrogate at the start of the suffix belongs with the high surrogate that changed
        if (suffix > 0 && suffix < oldText.Length && char.IsLowSurrogate(oldText[oldText.Length - suffix])) suffix--;

        return new TextEditChange(prefix, oldText.Length - suffix, newText.Length - suffix - prefix, selection);
    }

    // Committed text: select the range the update replaces, then insert (an empty text deletes the range).
    private static void ApplyCommitted(ITextInputSession session, KumikoTextRange range, string text, string expected,
        int newSelectionStart, int newSelectionEnd)
    {
        if (session.Composition != null) session.CommitComposition();
        Select(session, range);
        if (text.Length > 0 || !range.IsEmpty)
            session.InsertText(text);

        // The text services usually leave the caret after the inserted text; honor any other selection they ask
        // for, as long as the editor took the text as given (otherwise the indices refer to text that isn't there).
        int caret = range.Start + text.Length;
        if ((newSelectionStart != caret || newSelectionEnd != caret) && session.Text == expected)
        {
            var wanted = Clamp(newSelectionStart, newSelectionEnd, expected.Length);
            if (session.Selection != wanted) session.SetSelection(wanted.Start, wanted.End);
        }
    }

    // Marked text: an update inside the current composition rewrites it; anything else commits the current
    // composition (if any) and starts a new one over the range.
    private static void ApplyComposition(ITextInputSession session, string before, KumikoTextRange range, string text,
        int newSelectionEnd)
    {
        if (session.Composition is { } composition && range.Start >= composition.Start && range.End <= composition.End)
        {
            string marked = before.Substring(composition.Start, composition.Length);
            int offset = range.Start - composition.Start;
            marked = marked.Remove(offset, range.Length).Insert(offset, text);
            // An empty composition cancels it, as the session documents
            session.SetComposition(marked, Math.Clamp(newSelectionEnd - composition.Start, 0, marked.Length));
            return;
        }

        if (session.Composition != null) session.CommitComposition();
        Select(session, range);
        if (text.Length > 0)
            session.SetComposition(text, Math.Clamp(newSelectionEnd - range.Start, 0, text.Length));
        else if (!range.IsEmpty)
            session.InsertText(string.Empty);
    }

    private static void Select(ITextInputSession session, KumikoTextRange range)
    {
        if (session.Selection != range)
            session.SetSelection(range.Start, range.End);
    }

    private static bool IsOnlyLineControl(string text)
    {
        if (text.Length == 0) return false;
        foreach (char c in text)
        {
            if (c is not ('\r' or '\n' or '\t')) return false;
        }
        return true;
    }

    private static KumikoTextRange ClampRange(KumikoTextRange range, int length) => Clamp(range.Start, range.End, length);

    private static KumikoTextRange Clamp(int a, int b, int length) =>
        KumikoTextRange.FromBounds(Math.Clamp(a, 0, length), Math.Clamp(b, 0, length));
}

#if !ANDROID && !WINDOWS
using System.Globalization;
using KumikoUI.Core.Components;
using KumikoUI.Core.Input;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Maui;

/// <summary>
/// The UIKit-independent half of the iOS and Mac Catalyst text-input adapter (#40). <c>KeyInputResponder</c>
/// implements <c>UITextInput</c> and turns each UIKit call into a call here, in UTF-16 indices; this class applies it
/// to the open editor's <see cref="ITextInputSession"/> (see docs/TEXT-INPUT.md), or to the grid as key events when no
/// text editor is open. It is compiled into the MAUI tests, which drive it without UIKit.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Marked text is the session's composition: <c>setMarkedText:selectedRange:</c> sets it, an empty marked text
/// cancels it, <c>unmarkText</c> and <c>insertText:</c> commit it.</item>
/// <item>Typing or composing with no text editor open sends the first character to the grid as a key, so the typing
/// edit trigger opens the editor; the rest of the text then goes to the new session, and a composition replaces the
/// character the editor started with.</item>
/// <item><see cref="Invalidated"/> asks the responder to tell UIKit (<c>UITextInputDelegate</c>) that its view of the
/// text is stale: when the session changes, or when Core changes the text, selection or composition (a key handled in
/// Core, a pointer gesture, undo). Changes made through this class raise nothing.</item>
/// </list>
/// </remarks>
internal sealed class AppleTextInputBridge
{
    /// <summary>A direction of <c>UITextLayoutDirection</c>.</summary>
    internal enum Direction
    {
        /// <summary>Toward the start of the text.</summary>
        Left,

        /// <summary>Toward the end of the text.</summary>
        Right,

        /// <summary>The line above: the start of a single-line text.</summary>
        Up,

        /// <summary>The line below: the end of a single-line text.</summary>
        Down,
    }

    private readonly Func<ITextInputSession?> _sessionProvider;
    private readonly Func<KumikoRect?> _editorBoundsProvider;
    private readonly Action<KumikoKeyEventArgs> _sendKey;
    private ITextInputSession? _attached;
    private int _callDepth; // inside a call from UIKit: changes it makes are not reported back to it

    /// <summary>Creates the bridge.</summary>
    /// <param name="sessionProvider">The text session of the open editor or filter search box, or null.</param>
    /// <param name="editorBoundsProvider">The open editor's bounds, for a session that cannot measure its text.</param>
    /// <param name="sendKey">Sends a key event to the grid's input controller.</param>
    public AppleTextInputBridge(
        Func<ITextInputSession?> sessionProvider,
        Func<KumikoRect?> editorBoundsProvider,
        Action<KumikoKeyEventArgs> sendKey)
    {
        _sessionProvider = sessionProvider;
        _editorBoundsProvider = editorBoundsProvider;
        _sendKey = sendKey;
    }

    /// <summary>
    /// UIKit's view of the text is stale. The argument is true when the session itself changed (an edit began or
    /// ended), which can also change the keyboard type.
    /// </summary>
    public event Action<bool>? Invalidated;

    /// <summary>The text session, or null when no text editor is open.</summary>
    public ITextInputSession? Session => _sessionProvider();

    /// <summary>The text UIKit sees: the session's text with any marked text, or empty.</summary>
    public string Text => Session?.Text ?? string.Empty;

    /// <summary>The selection; the caret at 0 when no text editor is open.</summary>
    public KumikoTextRange SelectedRange => Session?.Selection ?? default;

    /// <summary>The marked text (the composition), or null.</summary>
    public KumikoTextRange? MarkedRange => Session?.Composition;

    /// <summary>Whether an input method is composing.</summary>
    public bool HasMarkedText => MarkedRange != null;

    /// <summary>
    /// <c>hasText</c>: whether Backspace has something to delete. True with no text editor open, so the key still
    /// reaches the grid.
    /// </summary>
    public bool HasText => Session is not { } s || s.Text.Length > 0;

    /// <summary>Whether the open text editor takes only numbers (for the keyboard type).</summary>
    public bool IsNumeric => Session is DrawnTextBox { NumericOnly: true };

    /// <summary>
    /// Whether a hardware key must go to UIKit's text system rather than to the grid: every key while an input method
    /// is composing (Space converts, Return commits, Escape cancels, the arrows move between clauses), and Space while
    /// a text editor is open (it is text).
    /// </summary>
    /// <param name="key">The key.</param>
    public bool ShouldLeaveKeyToTextSystem(KumikoKey key) =>
        HasMarkedText || (key == KumikoKey.Space && Session != null);

    /// <summary>
    /// Follow the current session: subscribe to its changes and raise <see cref="Invalidated"/> when it is not the one
    /// followed before. Call it whenever an edit may have begun or ended.
    /// </summary>
    public void Sync()
    {
        var session = Session;
        if (ReferenceEquals(session, _attached)) return;
        if (_attached != null) _attached.TextInputStateChanged -= OnSessionStateChanged;
        _attached = session;
        if (session != null) session.TextInputStateChanged += OnSessionStateChanged;
        if (_callDepth == 0) Invalidated?.Invoke(true);
    }

    /// <summary>Stop following the session.</summary>
    public void Detach()
    {
        if (_attached != null) _attached.TextInputStateChanged -= OnSessionStateChanged;
        _attached = null;
    }

    private void OnSessionStateChanged(object? sender, EventArgs e)
    {
        if (_callDepth == 0) Invalidated?.Invoke(false);
    }

    // ── Positions ────────────────────────────────────────────────

    /// <summary>An index clamped to the text.</summary>
    public int Clamp(long index) => (int)Math.Clamp(index, 0, Text.Length);

    /// <summary><c>textInRange:</c>: the text between two indices, in either order, clamped.</summary>
    public string TextInRange(long start, long end)
    {
        string text = Text;
        int a = (int)Math.Clamp(Math.Min(start, end), 0, text.Length);
        int b = (int)Math.Clamp(Math.Max(start, end), 0, text.Length);
        return text.Substring(a, b - a);
    }

    /// <summary><c>positionFromPosition:offset:</c>: null when the result is outside the text.</summary>
    public int? PositionFromPosition(long position, long offset)
    {
        long result = position + offset;
        return result < 0 || result > Text.Length ? null : (int)result;
    }

    /// <summary>
    /// <c>positionFromPosition:inDirection:offset:</c>: left and right move by grapheme cluster, so an emoji sequence
    /// or a letter with combining marks is one step; up and down go to the start and end of the single line. Null
    /// when the move would leave the text.
    /// </summary>
    public int? PositionInDirection(long position, Direction direction, long offset)
    {
        string text = Text;
        int index = (int)Math.Clamp(position, 0, text.Length);
        if (offset < 0)
        {
            offset = -offset;
            direction = direction switch
            {
                Direction.Left => Direction.Right,
                Direction.Right => Direction.Left,
                Direction.Up => Direction.Down,
                _ => Direction.Up,
            };
        }
        switch (direction)
        {
            case Direction.Up: return offset == 0 ? index : 0;
            case Direction.Down: return offset == 0 ? index : text.Length;
        }
        for (long i = 0; i < offset; i++)
        {
            if (direction == Direction.Left)
            {
                if (index == 0) return null;
                index = PreviousBoundary(text, index);
            }
            else
            {
                if (index >= text.Length) return null;
                index = NextBoundary(text, index);
            }
        }
        return index;
    }

    /// <summary>
    /// <c>characterRangeByExtendingPosition:inDirection:</c>: the grapheme cluster before (left) or after (right) the
    /// position; up and down extend to the start or end of the line.
    /// </summary>
    public KumikoTextRange CharacterRangeByExtending(long position, Direction direction)
    {
        string text = Text;
        int index = (int)Math.Clamp(position, 0, text.Length);
        return direction switch
        {
            Direction.Left => KumikoTextRange.FromBounds(PreviousBoundary(text, index), index),
            Direction.Right => KumikoTextRange.FromBounds(index, NextBoundary(text, index)),
            Direction.Up => KumikoTextRange.FromBounds(0, index),
            _ => KumikoTextRange.FromBounds(index, text.Length),
        };
    }

    /// <summary>The grapheme-cluster boundary after <paramref name="index"/>.</summary>
    internal static int NextBoundary(string text, int index) =>
        index >= text.Length ? text.Length : index + StringInfo.GetNextTextElementLength(text, index);

    /// <summary>The grapheme-cluster boundary before <paramref name="index"/>.</summary>
    internal static int PreviousBoundary(string text, int index)
    {
        int previous = 0;
        for (int i = 0; i < index && i < text.Length; i += StringInfo.GetNextTextElementLength(text, i))
            previous = i;
        return previous;
    }

    // ── Editing ──────────────────────────────────────────────────

    /// <summary><c>setSelectedTextRange:</c>.</summary>
    public void SetSelectedRange(long start, long end)
    {
        if (Session is not { } session) return;
        var range = KumikoTextRange.FromBounds(Clamp(start), Clamp(end));
        if (range == session.Selection) return;
        _callDepth++;
        try { session.SetSelection(range.Start, range.End); }
        finally { _callDepth--; }
    }

    /// <summary>
    /// <c>insertText:</c>: committed text (a key, a suggestion, dictation). It replaces the marked text or the
    /// selection. A line break is Enter and a tab is Tab, as keys for the grid; with no text editor open the text
    /// is typed into the grid, which may open an editor.
    /// </summary>
    public void InsertText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        _callDepth++;
        try
        {
            if (text is "\n" or "\r" or "\r\n")
                _sendKey(new KumikoKeyEventArgs { Key = KumikoKey.Enter });
            else if (text == "\t")
                _sendKey(new KumikoKeyEventArgs { Key = KumikoKey.Tab });
            else if (Session is { } session)
            {
                if (!session.IsReadOnly) session.InsertText(text);
            }
            else
                TypeIntoGrid(text);
        }
        finally { _callDepth--; }
    }

    /// <summary><c>deleteBackward</c>: deletes in the editor, or is Backspace for the grid.</summary>
    public void DeleteBackward()
    {
        _callDepth++;
        try
        {
            if (Session is { } session)
            {
                if (!session.IsReadOnly) session.DeleteBackward();
            }
            else
                _sendKey(new KumikoKeyEventArgs { Key = KumikoKey.Backspace });
        }
        finally { _callDepth--; }
    }

    /// <summary>
    /// <c>replaceRange:withText:</c> (autocorrection, the keyboard's own edits). A composition is committed first.
    /// </summary>
    public void ReplaceRange(long start, long end, string? text)
    {
        if (Session is not { } session || session.IsReadOnly) return;
        _callDepth++;
        try
        {
            if (session.Composition != null) session.CommitComposition();
            var range = KumikoTextRange.FromBounds(Clamp(start), Clamp(end));
            session.SetSelection(range.Start, range.End);
            if (!string.IsNullOrEmpty(text))
                session.InsertText(text);
            else if (!range.IsEmpty)
                session.DeleteBackward();
        }
        finally { _callDepth--; }
    }

    /// <summary>
    /// <c>setMarkedText:selectedRange:</c>: sets the composition, replacing the current one or the selection; an
    /// empty text cancels it. With no text editor open, the first character opens one as typing would, and the whole
    /// marked text becomes the composition.
    /// </summary>
    /// <param name="text">The marked text.</param>
    /// <param name="selectedLocation">The start of the selection within the marked text.</param>
    /// <param name="selectedLength">The length of that selection; the caret goes to its end.</param>
    public void SetMarkedText(string? text, long selectedLocation, long selectedLength)
    {
        _callDepth++;
        try
        {
            var session = Session;
            if (string.IsNullOrEmpty(text))
            {
                session?.CancelComposition();
                return;
            }
            session ??= BeginEditForComposition(text);
            if (session == null || session.IsReadOnly) return;
            long caret = Math.Clamp(selectedLocation, 0, text.Length) + Math.Clamp(selectedLength, 0, text.Length);
            session.SetComposition(text, (int)Math.Min(caret, text.Length));
        }
        finally { _callDepth--; }
    }

    /// <summary><c>unmarkText</c>: commits the composition.</summary>
    public void UnmarkText()
    {
        if (Session is not { } session) return;
        _callDepth++;
        try { session.CommitComposition(); }
        finally { _callDepth--; }
    }

    // Typing with no text editor open: each text element is a key for the grid until one opens a text editor
    // (the typing edit trigger), which then takes the rest. The editor starts with the key's first UTF-16 unit
    // only, so a surrogate pair or combining sequence replaces it.
    private void TypeIntoGrid(string text)
    {
        int i = 0;
        while (i < text.Length)
        {
            int length = StringInfo.GetNextTextElementLength(text, i);
            string element = text.Substring(i, length);
            i += length;
            SendText(element);
            if (Session is not { } session) continue;

            string rest = text.Substring(i);
            if (element.Length > 1 && session.Text == element.Substring(0, 1))
            {
                session.SetSelection(0, session.Text.Length);
                rest = element + rest;
            }
            if (rest.Length > 0 && !session.IsReadOnly) session.InsertText(rest);
            return;
        }
    }

    // A composition with no text editor open: its first character opens one, as typing would, and is then taken out
    // again so the editor's text is replaced by the composition alone.
    private ITextInputSession? BeginEditForComposition(string text)
    {
        SendText(text.Substring(0, StringInfo.GetNextTextElementLength(text, 0)));
        if (Session is not { } session) return null;
        if (!session.IsReadOnly && session.Text.Length > 0 && session.Text == text.Substring(0, 1))
        {
            session.SetSelection(0, session.Text.Length);
            session.DeleteBackward();
        }
        return session;
    }

    private void SendText(string element) =>
        _sendKey(new KumikoKeyEventArgs { Key = KumikoKey.Character, Character = element[0], Text = element });

    // ── Geometry ─────────────────────────────────────────────────

    /// <summary>
    /// <c>caretRectForPosition:</c>, in the grid's coordinates: the caret's area in the editor, or the editor's
    /// bounds when its session cannot measure its text.
    /// </summary>
    public KumikoRect CaretRect(long position) =>
        Session is DrawnTextBox box
            ? box.GetTextRangeBounds(new KumikoTextRange(Clamp(position), 0))
            : _editorBoundsProvider() ?? default;

    /// <summary>
    /// <c>firstRectForRange:</c>, in the grid's coordinates: the area of the range (the input method places its
    /// candidate window next to it), or the editor's bounds when its session cannot measure its text.
    /// </summary>
    public KumikoRect FirstRect(long start, long end) =>
        Session is DrawnTextBox box
            ? box.GetTextRangeBounds(KumikoTextRange.FromBounds(Clamp(start), Clamp(end)))
            : _editorBoundsProvider() ?? default;

    /// <summary><c>closestPositionToPoint:</c>, from a point in the grid's coordinates.</summary>
    public int ClosestPosition(float x) =>
        Session is DrawnTextBox box ? Clamp(box.GetTextIndexAt(x)) : 0;
}
#endif

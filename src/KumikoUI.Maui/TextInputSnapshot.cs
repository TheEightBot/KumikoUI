namespace KumikoUI.Maui;

/// <summary>
/// What a platform input method is told about the text it edits: the text, the selection and the composition, in
/// UTF-16 code units; the composition is -1/-1 when there is none.
/// </summary>
/// <param name="Text">The text.</param>
/// <param name="SelectionStart">The start of the selection.</param>
/// <param name="SelectionEnd">The end of the selection (equal to the start at a caret).</param>
/// <param name="CompositionStart">The start of the composition, or -1.</param>
/// <param name="CompositionEnd">The end of the composition, or -1.</param>
internal readonly record struct TextInputSnapshot(
    string Text, int SelectionStart, int SelectionEnd, int CompositionStart, int CompositionEnd)
{
    /// <summary>No text, a caret at 0, no composition.</summary>
    public static TextInputSnapshot Empty => new(string.Empty, 0, 0, -1, -1);

    /// <summary>Whether the selection and the composition equal <paramref name="other"/>'s.</summary>
    public bool SameSelection(TextInputSnapshot other) =>
        SelectionStart == other.SelectionStart && SelectionEnd == other.SelectionEnd &&
        CompositionStart == other.CompositionStart && CompositionEnd == other.CompositionEnd;
}

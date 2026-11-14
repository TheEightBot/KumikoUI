namespace KumikoUI.Core.Input;

/// <summary>
/// A range of a text in UTF-16 code units: <see cref="Start"/> and <see cref="Length"/>. Used by
/// <see cref="ITextInputSession"/> for the selection and the composition (marked text).
/// </summary>
/// <param name="Start">The index of the first code unit.</param>
/// <param name="Length">The number of code units; 0 for a caret position.</param>
public readonly record struct KumikoTextRange(int Start, int Length)
{
    /// <summary>The index just past the last code unit.</summary>
    public int End => Start + Length;

    /// <summary>Whether the range is a caret position (no code units).</summary>
    public bool IsEmpty => Length == 0;

    /// <summary>The range between two indices, in either order.</summary>
    /// <param name="start">One end of the range.</param>
    /// <param name="end">The other end of the range; it may come before <paramref name="start"/>.</param>
    /// <returns>The range from the smaller index to the larger one.</returns>
    public static KumikoTextRange FromBounds(int start, int end) =>
        start <= end ? new(start, end - start) : new(end, start - end);
}

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
    public static KumikoTextRange FromBounds(int a, int b) => a <= b ? new(a, b - a) : new(b, a - b);
}

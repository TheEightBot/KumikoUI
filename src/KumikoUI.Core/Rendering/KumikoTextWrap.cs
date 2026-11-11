namespace KumikoUI.Core.Rendering;

/// <summary>
/// How text that is wider than its rectangle is laid out by
/// <see cref="IDrawingContext.DrawTextInRect(string, KumikoRect, KumikoPaint, KumikoTextAlignment, KumikoVerticalAlignment, bool, KumikoTextWrap)"/>
/// and measured by <see cref="IDrawingContext.MeasureText(string, KumikoPaint, float, KumikoTextWrap)"/>.
/// </summary>
public enum KumikoTextWrap
{
    /// <summary>
    /// One line. Line breaks and tabs are shown as spaces, and text wider than the rectangle is cut at a whole
    /// character (never inside a surrogate pair or a combining sequence) and ends with an ellipsis.
    /// </summary>
    None,

    /// <summary>
    /// Several lines, broken at spaces and after hyphens; a word wider than the rectangle is broken between
    /// characters. Line breaks in the text start a new line. When the lines do not all fit the rectangle's height,
    /// the last line that fits ends with an ellipsis.
    /// </summary>
    Word,

    /// <summary>
    /// Several lines, broken between any two characters (never inside a surrogate pair or a combining sequence).
    /// Line breaks in the text start a new line. When the lines do not all fit the rectangle's height, the last
    /// line that fits ends with an ellipsis.
    /// </summary>
    Character,
}

namespace KumikoUI.Core.Rendering;

/// <summary>
/// The lines <see cref="KumikoTextLayout.LayOut"/> produced for a text: what to draw on each line and how wide
/// each line is. Immutable, so a drawing context can cache it and draw it again on later frames.
/// </summary>
public sealed class KumikoTextLines
{
    private readonly string[] _lines;
    private readonly float[] _widths;

    /// <summary>An empty layout: no lines.</summary>
    public static KumikoTextLines Empty { get; } = new([], []);

    internal KumikoTextLines(string[] lines, float[] widths)
    {
        _lines = lines;
        _widths = widths;
        float max = 0f;
        for (int i = 0; i < widths.Length; i++)
            max = Math.Max(max, widths[i]);
        Width = max;
    }

    /// <summary>The number of lines.</summary>
    public int Count => _lines.Length;

    /// <summary>The width of the widest line, in pixels.</summary>
    public float Width { get; }

    /// <summary>The text of one line, as it is drawn (with its ellipsis, if it has one).</summary>
    /// <param name="index">The line, from 0 (the top line) to <see cref="Count"/> - 1.</param>
    /// <returns>The line's text.</returns>
    public string GetLine(int index) => _lines[index];

    /// <summary>The width of one line, in pixels.</summary>
    /// <param name="index">The line, from 0 (the top line) to <see cref="Count"/> - 1.</param>
    /// <returns>The measured width of <see cref="GetLine"/>.</returns>
    public float GetLineWidth(int index) => _widths[index];

    /// <summary>
    /// The height of the lines stacked one per <see cref="KumikoFontMetrics.LineHeight"/>, without the leading
    /// below the last line: <see cref="KumikoFontMetrics.TextHeight"/> for one line, zero for none.
    /// </summary>
    /// <param name="metrics">The metrics of the font the lines were measured in.</param>
    /// <returns>The height of the block of lines, in pixels.</returns>
    public float GetHeight(KumikoFontMetrics metrics) =>
        Count == 0 ? 0f : metrics.TextHeight + (Count - 1) * metrics.LineHeight;
}

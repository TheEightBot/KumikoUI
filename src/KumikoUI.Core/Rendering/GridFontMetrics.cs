namespace KumikoUI.Core.Rendering;

/// <summary>
/// Font metrics for precise text positioning (ascent, descent, leading).
/// Ascent is negative (above baseline), descent is positive (below baseline).
/// </summary>
public readonly struct GridFontMetrics
{
    /// <summary>Distance from baseline to top of text (negative value).</summary>
    public float Ascent { get; }

    /// <summary>Distance from baseline to bottom of text (positive value).</summary>
    public float Descent { get; }

    /// <summary>Inter-line spacing.</summary>
    public float Leading { get; }

    /// <summary>Total line height (Descent - Ascent + Leading).</summary>
    public float LineHeight => Descent - Ascent + Leading;

    /// <summary>Text height without leading (Descent - Ascent).</summary>
    public float TextHeight => Descent - Ascent;

    public GridFontMetrics(float ascent, float descent, float leading = 0)
    {
        Ascent = ascent;
        Descent = descent;
        Leading = leading;
    }
}

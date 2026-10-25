namespace KumikoUI.Core.Rendering;

/// <summary>
/// Platform-independent paint/style definition for drawing operations.
/// </summary>
public class KumikoPaint
{
    /// <summary>Fill or stroke color.</summary>
    public KumikoColor Color { get; set; } = KumikoColor.Black;

    /// <summary>Stroke width in pixels (used when Style is Stroke or FillAndStroke).</summary>
    public float StrokeWidth { get; set; } = 1f;

    /// <summary>Paint style: fill, stroke, or both.</summary>
    public PaintStyle Style { get; set; } = PaintStyle.Fill;

    /// <summary>Whether anti-aliasing is enabled.</summary>
    public bool IsAntiAlias { get; set; } = true;

    /// <summary>Optional font for text rendering.</summary>
    public KumikoFont? Font { get; set; }
}

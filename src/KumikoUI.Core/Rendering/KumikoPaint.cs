namespace KumikoUI.Core.Rendering;

/// <summary>
/// Platform-independent paint definition for drawing operations. Whether a shape is filled or stroked is decided by
/// the <see cref="IDrawingContext"/> method (<c>FillRect</c> fills, <c>DrawRect</c> strokes), not by the paint.
/// </summary>
public sealed class KumikoPaint
{
    /// <summary>Fill or stroke color.</summary>
    public KumikoColor Color { get; set; } = KumikoColor.Black;

    /// <summary>Stroke width in pixels, used by the methods that stroke (<c>DrawRect</c>, <c>DrawLine</c>, ...).</summary>
    public float StrokeWidth { get; set; } = 1f;

    /// <summary>Whether anti-aliasing is enabled.</summary>
    public bool IsAntiAlias { get; set; } = true;

    /// <summary>Optional font for text rendering.</summary>
    public KumikoFont? Font { get; set; }
}

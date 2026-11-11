namespace KumikoUI.Uno.Rendering;

/// <summary>
/// Maps a surface's device-pixel size to the logical units (DIPs) the renderer works in, and tells real
/// rasterization-scale changes from noise. Pure math, unit-tested in <c>KumikoUI.Uno.Tests</c>.
/// </summary>
/// <param name="ScaleX">Device pixels per logical unit, horizontally.</param>
/// <param name="ScaleY">Device pixels per logical unit, vertically.</param>
/// <param name="Width">The logical viewport width.</param>
/// <param name="Height">The logical viewport height.</param>
internal readonly record struct KumikoSurfaceMetrics(float ScaleX, float ScaleY, float Width, float Height)
{
    /// <summary>Scale changes smaller than this are rounding noise, not a new display scale.</summary>
    public const double ScaleTolerance = 0.001;

    /// <summary>True when there is nothing to draw (a zero or negative viewport).</summary>
    public bool IsEmpty => float.IsNaN(Width) || Width <= 0 || float.IsNaN(Height) || Height <= 0;

    /// <summary>
    /// The metrics of a bitmap of <paramref name="pixelWidth"/> × <paramref name="pixelHeight"/> device pixels backing
    /// an element of <paramref name="logicalWidth"/> × <paramref name="logicalHeight"/> DIPs. Each axis gets its own
    /// scale because the bitmap size is truncated to whole pixels, so the drawing fills the bitmap exactly. An axis
    /// without a usable logical size falls back to <paramref name="fallbackScale"/> (the window's rasterization
    /// scale) and derives its logical size from the pixels.
    /// </summary>
    public static KumikoSurfaceMetrics FromPixels(
        int pixelWidth, int pixelHeight, double logicalWidth, double logicalHeight, double fallbackScale)
    {
        float fallback = IsUsable(fallbackScale) ? (float)fallbackScale : 1f;
        Axis(pixelWidth, logicalWidth, fallback, out float scaleX, out float width);
        Axis(pixelHeight, logicalHeight, fallback, out float scaleY, out float height);
        return new KumikoSurfaceMetrics(scaleX, scaleY, width, height);
    }

    /// <summary>
    /// Whether a rasterization scale moved from <paramref name="previous"/> to <paramref name="current"/> by more than
    /// <see cref="ScaleTolerance"/>; an unusable value (zero, negative, NaN, infinite) is never a change.
    /// </summary>
    public static bool ScaleChanged(double previous, double current) =>
        IsUsable(current) && (!IsUsable(previous) || Math.Abs(current - previous) > ScaleTolerance);

    private static void Axis(int pixels, double logical, float fallbackScale, out float scale, out float size)
    {
        if (pixels <= 0)
        {
            scale = fallbackScale;
            size = 0f;
        }
        else if (IsUsable(logical))
        {
            scale = (float)(pixels / logical);
            size = (float)logical;
        }
        else
        {
            scale = fallbackScale;
            size = pixels / fallbackScale;
        }
    }

    private static bool IsUsable(double value) => value > 0 && !double.IsInfinity(value);
}

using KumikoUI.Core.Models;

namespace KumikoUI.Core.Rendering;

/// <summary>
/// Draws the grid's busy indicator (#327): an indeterminate bar or a scrim with a spinner. Self-contained (two reused
/// paints, geometry computed into locals) so it can be shared with other indicators later, and allocation-free. The
/// animation phase is a function of the clock the caller passes, so a frame is a pure function of its inputs.
/// </summary>
internal sealed class BusyIndicatorPainter
{
    /// <summary>The bar's height in pixels.</summary>
    internal const float BarHeight = 3f;

    /// <summary>How long one sweep of the bar takes, in milliseconds.</summary>
    internal const int BarPeriodMs = 1400;

    /// <summary>How long one turn of the spinner takes, in milliseconds.</summary>
    internal const int SpinnerPeriodMs = 900;

    /// <summary>How often the hosts repaint while an animated indicator shows, in milliseconds (about 30 frames a second).</summary>
    internal const int FrameIntervalMs = 33;

    private const int Spokes = 12;
    private const float SweepShare = 0.35f; // the moving segment's share of the bar's width

    private readonly KumikoPaint _fill = new();
    private readonly KumikoPaint _stroke = new() { IsAntiAlias = true };

    /// <summary>
    /// Draws the indicator of <paramref name="style"/> in the data area <paramref name="area"/> (viewport width, from
    /// the top of the rows down to the pinned footer).
    /// </summary>
    /// <param name="ctx">The drawing context.</param>
    /// <param name="style">Supplies the kind and the colors.</param>
    /// <param name="area">The data area.</param>
    /// <param name="animate">False draws a still bar or spinner.</param>
    /// <param name="clockMs">A monotonic clock in milliseconds; the phase is derived from it.</param>
    public void Draw(IDrawingContext ctx, KumikoStyle style, KumikoRect area, bool animate, long clockMs)
    {
        if (area.Width <= 0 || area.Height <= 0) return;
        if (style.BusyIndicatorKind == KumikoBusyIndicatorKind.Overlay)
            DrawOverlay(ctx, style, area, animate, clockMs);
        else
            DrawBar(ctx, style, area, animate, clockMs);
    }

    private void DrawBar(IDrawingContext ctx, KumikoStyle style, KumikoRect area, bool animate, long clockMs)
    {
        var color = style.BusyIndicatorColor;
        float height = Math.Min(BarHeight, area.Height);
        _fill.Color = color.WithAlpha((byte)(color.A / 5)); // the track: a faint band
        ctx.FillRect(new KumikoRect(area.X, area.Y, area.Width, height), _fill);

        float segment = area.Width * SweepShare;
        float left;
        if (animate)
        {
            // The segment enters from the left edge and leaves at the right one, eased in and out.
            float t = clockMs % BarPeriodMs / (float)BarPeriodMs;
            float eased = t * t * (3f - 2f * t);
            left = area.X - segment + (area.Width + segment) * eased;
        }
        else
        {
            left = area.X + (area.Width - segment) / 2;
        }
        float right = Math.Min(left + segment, area.Right);
        left = Math.Max(left, area.X);
        if (right <= left) return;
        _fill.Color = color;
        ctx.FillRect(new KumikoRect(left, area.Y, right - left, height), _fill);
    }

    private void DrawOverlay(IDrawingContext ctx, KumikoStyle style, KumikoRect area, bool animate, long clockMs)
    {
        _fill.Color = style.BusyOverlayColor;
        ctx.FillRect(area, _fill);

        float radius = Math.Clamp(Math.Min(area.Width, area.Height) / 2f - 4f, 0f, 16f);
        if (radius < 3f) return;
        float cx = area.X + area.Width / 2f;
        float cy = area.Y + area.Height / 2f;
        float inner = radius * 0.55f;
        var color = style.BusyIndicatorColor;
        _stroke.StrokeWidth = Math.Max(2f, radius / 5f);

        // The head spoke is opaque and the ones behind it fade, so the ring seems to turn. Still: the head is at the top.
        int head = animate ? (int)(clockMs % SpinnerPeriodMs * Spokes / SpinnerPeriodMs) : 0;
        for (int i = 0; i < Spokes; i++)
        {
            int behind = (head - i + Spokes) % Spokes; // 0 for the head, up to Spokes - 1 for the one in front of it
            float alpha = 1f - behind / (float)Spokes * 0.85f;
            _stroke.Color = color.WithAlpha((byte)(color.A * alpha));
            float angle = i * (MathF.PI * 2f / Spokes);
            float sin = MathF.Sin(angle), cos = MathF.Cos(angle);
            ctx.DrawLine(cx + sin * inner, cy - cos * inner, cx + sin * radius, cy - cos * radius, _stroke);
        }
    }
}

using KumikoUI.Core.Input;
using KumikoUI.Core.Layout;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Rendering;

/// <summary>
/// Draws the grid's scroll bars (#316) over the rows, in viewport coordinates, from <see cref="ScrollBarGeometry"/>. An
/// <see cref="KumikoScrollBarVisibility.Always"/> bar is drawn every frame; an <see cref="KumikoScrollBarVisibility.Auto"/>
/// bar is drawn while the connected controller shows it: noticing here that the offset moved since the last frame
/// starts its hold and fade. At rest a bar is a thin thumb inset from the edge; hovered or held by a mouse or pen it
/// expands over its track. Allocation-free: two reused paints.
/// </summary>
internal sealed class ScrollBarRenderer
{
    /// <summary>The gap between a bar's thumb and the viewport edge (and, expanded, the track's inner edge), in pixels.</summary>
    internal const float Inset = 2f;

    private readonly KumikoPaint _thumbPaint = new() { IsAntiAlias = true };
    private readonly KumikoPaint _trackPaint = new();
    private float _lastOffsetX = float.NaN, _lastOffsetY = float.NaN;

    /// <summary>Draws the bars for this frame (after everything they lie over, before the editor and popups).</summary>
    public void Draw(IDrawingContext ctx, KumikoSource dataSource, ScrollState scroll, KumikoStyle style,
        KumikoInputController? input)
    {
        var layout = ScrollBarGeometry.Compute(scroll, style, dataSource.Columns, dataSource);

        // Scrolling (by any means: a pan, the wheel, a fling, the keyboard, code) shows an Auto bar
        if (input != null)
        {
            if (Moved(_lastOffsetY, scroll.OffsetY) && layout.Vertical.IsPresent
                && style.VerticalScrollBarVisibility == KumikoScrollBarVisibility.Auto)
                input.Animations.ShowScrollBar(vertical: true);
            if (Moved(_lastOffsetX, scroll.OffsetX) && layout.Horizontal.IsPresent
                && style.HorizontalScrollBarVisibility == KumikoScrollBarVisibility.Auto)
                input.Animations.ShowScrollBar(vertical: false);
        }
        _lastOffsetX = scroll.OffsetX;
        _lastOffsetY = scroll.OffsetY;

        DrawBar(ctx, layout.Vertical, style, style.VerticalScrollBarVisibility, input);
        DrawBar(ctx, layout.Horizontal, style, style.HorizontalScrollBarVisibility, input);
    }

    private static bool Moved(float last, float now) => !float.IsNaN(last) && last != now;

    private void DrawBar(IDrawingContext ctx, in ScrollBarAxisLayout bar, KumikoStyle style,
        KumikoScrollBarVisibility visibility, KumikoInputController? input)
    {
        if (!bar.IsPresent) return;
        float opacity = visibility == KumikoScrollBarVisibility.Always ? 1f
            : input?.ScrollBarOpacity(bar.IsVertical, style) ?? 0f;
        if (opacity <= 0f) return;

        bool expanded = input?.IsScrollBarExpanded(bar.IsVertical) == true;
        var track = bar.Track;
        float thickness;
        if (expanded)
        {
            _trackPaint.Color = Fade(style.ScrollBarTrackColor, opacity);
            ctx.FillRect(track, _trackPaint);
            thickness = Math.Max(1f, ScrollBarGeometry.ExpandedThickness(style) - 2 * Inset);
        }
        else
        {
            thickness = ScrollBarGeometry.Thickness(style);
            if (thickness <= 0f) return;
        }

        // The thumb keeps its gap from the outer edge, and grows inward when expanded
        var thumb = bar.IsVertical
            ? new KumikoRect(track.Right - Inset - thickness, bar.ThumbStart, thickness, bar.ThumbLength)
            : new KumikoRect(bar.ThumbStart, track.Bottom - Inset - thickness, bar.ThumbLength, thickness);
        _thumbPaint.Color = Fade(style.ScrollBarThumbColor, opacity);
        ScrollThumb.Fill(ctx, thumb, _thumbPaint);
    }

    private static KumikoColor Fade(KumikoColor color, float opacity) =>
        opacity >= 1f ? color : color.WithAlpha((byte)MathF.Round(color.A * opacity));
}

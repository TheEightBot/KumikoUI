namespace KumikoUI.Core.Rendering;

/// <summary>
/// The built-in icon set: every <see cref="KumikoIcon"/> drawn as filled polygons and joined strokes, scaled to its
/// slot. This is the default <see cref="Models.KumikoStyle.IconProvider"/>.
/// </summary>
/// <remarks>
/// Drawing allocates nothing: the points of each glyph are stack-allocated. To replace a few glyphs and keep the
/// rest, derive from this class, override <see cref="DrawIcon"/> for the icons you draw yourself and call the base
/// method for the others.
/// </remarks>
public class KumikoIconProvider : IKumikoIconProvider
{
    /// <summary>The shared instance used by default by every style and drawn component.</summary>
    public static KumikoIconProvider Default { get; } = new();

    /// <inheritdoc />
    /// <remarks>
    /// Solid glyphs (triangles, the active filter funnel, the plus and minus signs, the dash) fill with the paint's
    /// color; line glyphs (chevrons, the tick, the "×", the funnel outline, the calendar) stroke with its color and
    /// stroke width. Every glyph is centered in <paramref name="bounds"/> and sized from its shorter side.
    /// </remarks>
    public virtual void DrawIcon(IDrawingContext ctx, KumikoIcon icon, KumikoRect bounds, KumikoPaint paint)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(paint);

        float s = Math.Min(bounds.Width, bounds.Height);
        if (s <= 0) return;
        float cx = bounds.X + bounds.Width / 2;
        float cy = bounds.Y + bounds.Height / 2;
        // The glyph's square: centered in the slot, as wide and tall as its shorter side.
        float left = cx - s / 2;
        float top = cy - s / 2;

        switch (icon)
        {
            case KumikoIcon.GroupExpanded:
                GlyphDrawing.FillTriangleDown(ctx, cx, cy, s * 0.35f, s * 0.225f, paint);
                break;
            case KumikoIcon.GroupCollapsed:
                GlyphDrawing.FillTriangle(ctx,
                    cx - s * 0.2f, cy - s * 0.35f,
                    cx + s * 0.25f, cy,
                    cx - s * 0.2f, cy + s * 0.35f, paint);
                break;
            case KumikoIcon.SortAscending:
                GlyphDrawing.FillTriangleUp(ctx, cx, cy, s / 3, s / 4, paint);
                break;
            case KumikoIcon.SortDescending:
                GlyphDrawing.FillTriangleDown(ctx, cx, cy, s / 3, s / 4, paint);
                break;
            case KumikoIcon.Filter:
                GlyphDrawing.Funnel(ctx, left, top, s, paint, filled: false);
                break;
            case KumikoIcon.FilterActive:
                GlyphDrawing.Funnel(ctx, left, top, s, paint, filled: true);
                break;
            case KumikoIcon.Remove:
            {
                float half = s * 0.35f;
                ctx.DrawLine(cx - half, cy - half, cx + half, cy + half, paint);
                ctx.DrawLine(cx - half, cy + half, cx + half, cy - half, paint);
                break;
            }
            case KumikoIcon.GroupSeparator:
                GlyphDrawing.Chevron(ctx, cx - s * 0.25f, cy - s * 0.4f, cx + s * 0.25f, cy, cx - s * 0.25f, cy + s * 0.4f, paint);
                break;
            case KumikoIcon.DragHandle:
                GlyphDrawing.Grip(ctx, cx, cy, s, paint);
                break;
            case KumikoIcon.Check:
                GlyphDrawing.Chevron(ctx,
                    left + s * 0.2f, top + s * 0.5f,
                    left + s * 0.4f, top + s * 0.75f,
                    left + s * 0.8f, top + s * 0.25f, paint);
                break;
            case KumikoIcon.Indeterminate:
            {
                float w = s * 0.5f;
                float h = Math.Max(2f, s * 0.18f);
                GlyphDrawing.FillDash(ctx, cx - w / 2, cy - h / 2, w, h, paint);
                break;
            }
            case KumikoIcon.DropDown:
                GlyphDrawing.FillTriangleDown(ctx, cx, cy, s * 5f / 12f, s / 4, paint);
                break;
            case KumikoIcon.DropDownOpen:
                GlyphDrawing.FillTriangleUp(ctx, cx, cy, s * 5f / 12f, s / 4, paint);
                break;
            case KumikoIcon.Increment:
                GlyphDrawing.FillPlus(ctx, cx, cy, s * 0.44f, Math.Max(1f, paint.StrokeWidth) / 2, paint);
                break;
            case KumikoIcon.Decrement:
            {
                float arm = s * 0.44f;
                float half = Math.Max(1f, paint.StrokeWidth) / 2;
                GlyphDrawing.FillDash(ctx, cx - arm, cy - half, arm * 2, half * 2, paint);
                break;
            }
            case KumikoIcon.Calendar:
                GlyphDrawing.CalendarPage(ctx, left, top, s, paint);
                break;
            case KumikoIcon.CalendarPrevious:
                GlyphDrawing.Chevron(ctx, cx + s * 0.15f, cy - s * 0.4f, cx - s * 0.15f, cy, cx + s * 0.15f, cy + s * 0.4f, paint);
                break;
            case KumikoIcon.CalendarNext:
                GlyphDrawing.Chevron(ctx, cx - s * 0.15f, cy - s * 0.4f, cx + s * 0.15f, cy, cx - s * 0.15f, cy + s * 0.4f, paint);
                break;
            case KumikoIcon.SpinUp:
                GlyphDrawing.Chevron(ctx, cx - s / 2, cy + s / 4, cx, cy - s / 4, cx + s / 2, cy + s / 4, paint);
                break;
            case KumikoIcon.SpinDown:
                GlyphDrawing.Chevron(ctx, cx - s / 2, cy - s / 4, cx, cy + s / 4, cx + s / 2, cy - s / 4, paint);
                break;
        }
    }
}

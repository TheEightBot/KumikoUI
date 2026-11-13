namespace KumikoUI.Core.Rendering;

/// <summary>
/// Shapes for the built-in icon set (<see cref="KumikoIconProvider"/>): triangles, chevrons, dashes and the like,
/// each one filled polygon or one joined polyline. Points go in a stack-allocated span, so drawing a glyph
/// allocates nothing.
/// </summary>
internal static class GlyphDrawing
{
    /// <summary>Fills a triangle. <paramref name="paint"/> should be a fill paint.</summary>
    public static void FillTriangle(
        IDrawingContext ctx, float x1, float y1, float x2, float y2, float x3, float y3, KumikoPaint paint)
    {
        Span<KumikoPoint> pts = stackalloc KumikoPoint[3];
        pts[0] = new KumikoPoint(x1, y1);
        pts[1] = new KumikoPoint(x2, y2);
        pts[2] = new KumikoPoint(x3, y3);
        ctx.FillPolygon(pts, paint);
    }

    /// <summary>Fills a triangle with its apex pointing up, centered on (<paramref name="cx"/>, <paramref name="cy"/>).</summary>
    public static void FillTriangleUp(IDrawingContext ctx, float cx, float cy, float halfWidth, float halfHeight, KumikoPaint paint) =>
        FillTriangle(ctx, cx - halfWidth, cy + halfHeight, cx, cy - halfHeight, cx + halfWidth, cy + halfHeight, paint);

    /// <summary>Fills a triangle with its apex pointing down, centered on (<paramref name="cx"/>, <paramref name="cy"/>).</summary>
    public static void FillTriangleDown(IDrawingContext ctx, float cx, float cy, float halfWidth, float halfHeight, KumikoPaint paint) =>
        FillTriangle(ctx, cx - halfWidth, cy - halfHeight, cx + halfWidth, cy - halfHeight, cx, cy + halfHeight, paint);

    /// <summary>Fills an axis-aligned rectangle as one polygon: the dash of an indeterminate check box.</summary>
    public static void FillDash(IDrawingContext ctx, float x, float y, float width, float height, KumikoPaint paint)
    {
        Span<KumikoPoint> pts = stackalloc KumikoPoint[4];
        pts[0] = new KumikoPoint(x, y);
        pts[1] = new KumikoPoint(x + width, y);
        pts[2] = new KumikoPoint(x + width, y + height);
        pts[3] = new KumikoPoint(x, y + height);
        ctx.FillPolygon(pts, paint);
    }

    /// <summary>Strokes an open chevron (two segments meeting at the middle point) with round joins.</summary>
    public static void Chevron(
        IDrawingContext ctx, float x1, float y1, float x2, float y2, float x3, float y3, KumikoPaint paint)
    {
        Span<KumikoPoint> pts = stackalloc KumikoPoint[3];
        pts[0] = new KumikoPoint(x1, y1);
        pts[1] = new KumikoPoint(x2, y2);
        pts[2] = new KumikoPoint(x3, y3);
        ctx.DrawPolyline(pts, paint);
    }

    /// <summary>
    /// A funnel in the square (<paramref name="x"/>, <paramref name="y"/>, <paramref name="size"/>): wide at the top,
    /// narrowing to a stem. Filled, or one closed outline joined at every corner.
    /// </summary>
    public static void Funnel(IDrawingContext ctx, float x, float y, float size, KumikoPaint paint, bool filled)
    {
        float bottom = y + size;
        float mid = y + size * 0.45f;
        Span<KumikoPoint> pts = stackalloc KumikoPoint[7];
        pts[0] = new KumikoPoint(x, y);
        pts[1] = new KumikoPoint(x + size, y);
        pts[2] = new KumikoPoint(x + size * 0.65f, mid);
        pts[3] = new KumikoPoint(x + size * 0.65f, bottom);
        pts[4] = new KumikoPoint(x + size * 0.35f, bottom);
        pts[5] = new KumikoPoint(x + size * 0.35f, mid);
        if (filled)
        {
            ctx.FillPolygon(pts[..6], paint);
        }
        else
        {
            // The last point repeats the first, so the outline closes with a join instead of two caps.
            pts[6] = pts[0];
            ctx.DrawPolyline(pts, paint);
        }
    }

    /// <summary>Fills a plus sign centered on (<paramref name="cx"/>, <paramref name="cy"/>) as one polygon.</summary>
    public static void FillPlus(IDrawingContext ctx, float cx, float cy, float arm, float halfThickness, KumikoPaint paint)
    {
        float t = halfThickness;
        Span<KumikoPoint> pts = stackalloc KumikoPoint[12];
        pts[0] = new KumikoPoint(cx - t, cy - arm);
        pts[1] = new KumikoPoint(cx + t, cy - arm);
        pts[2] = new KumikoPoint(cx + t, cy - t);
        pts[3] = new KumikoPoint(cx + arm, cy - t);
        pts[4] = new KumikoPoint(cx + arm, cy + t);
        pts[5] = new KumikoPoint(cx + t, cy + t);
        pts[6] = new KumikoPoint(cx + t, cy + arm);
        pts[7] = new KumikoPoint(cx - t, cy + arm);
        pts[8] = new KumikoPoint(cx - t, cy + t);
        pts[9] = new KumikoPoint(cx - arm, cy + t);
        pts[10] = new KumikoPoint(cx - arm, cy - t);
        pts[11] = new KumikoPoint(cx - t, cy - t);
        ctx.FillPolygon(pts, paint);
    }

    /// <summary>A grab handle: two columns of three round dots centered on (<paramref name="cx"/>, <paramref name="cy"/>).</summary>
    public static void Grip(IDrawingContext ctx, float cx, float cy, float size, KumikoPaint paint)
    {
        float radius = size * 0.09375f;
        float spacing = size * 0.3125f;
        for (int row = -1; row <= 1; row++)
        {
            for (int col = 0; col <= 1; col++)
            {
                float dx = cx + (col - 0.5f) * spacing;
                float dy = cy + row * spacing;
                ctx.FillRoundRect(new KumikoRect(dx - radius, dy - radius, radius * 2, radius * 2), radius, paint);
            }
        }
    }

    /// <summary>
    /// A calendar page in the square (<paramref name="x"/>, <paramref name="y"/>, <paramref name="size"/>): an outline
    /// with a header line and two binding tabs.
    /// </summary>
    public static void CalendarPage(IDrawingContext ctx, float x, float y, float size, KumikoPaint paint)
    {
        float pageTop = y + size * 0.1875f;
        ctx.DrawRect(new KumikoRect(x, pageTop, size, size - (pageTop - y)), paint);
        float headerY = y + size * 0.4375f;
        ctx.DrawLine(x, headerY, x + size, headerY, paint);
        float tabBottom = y + size * 0.25f;
        ctx.DrawLine(x + size * 0.25f, y, x + size * 0.25f, tabBottom, paint);
        ctx.DrawLine(x + size * 0.75f, y, x + size * 0.75f, tabBottom, paint);
    }
}

namespace KumikoUI.Core.Rendering;

/// <summary>
/// Draws the small built-in glyphs (disclosure triangles, sort arrows, chevrons) as one filled polygon or one
/// joined polyline each. Points go in a stack-allocated span, so drawing a glyph allocates nothing.
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
}

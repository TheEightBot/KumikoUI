using KumikoUI.Core.Rendering;
using KumikoUI.SkiaSharp;
using SkiaSharp;

namespace KumikoUI.Core.Tests.Rendering;

/// <summary>
/// Glyphs drawn through <see cref="SkiaDrawingContext"/> are solid shapes at 1x, 2x and 3x scale (#118).
/// A pixel check, not a golden image.
/// </summary>
public class SkiaGlyphRasterTests
{
    private static readonly KumikoColor Ink = new(200, 30, 30);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void FilledTriangle_IsSolidThroughItsInterior(int scale)
    {
        // The group-header chevron's collapsed shape, in logical pixels.
        var tri = new[] { new KumikoPoint(4, 3), new KumikoPoint(12, 10), new KumikoPoint(4, 17) };
        using var bitmap = Render(scale, 20, 20, ctx =>
            ctx.FillPolygon(tri, new KumikoPaint { Color = Ink, IsAntiAlias = true }));

        // Every sample at least 1.5 device pixels inside every edge is fully opaque ink: no gaps or notches.
        int sampled = 0;
        for (float u = 0.05f; u < 1; u += 0.05f)
        {
            for (float v = 0.05f; u + v < 0.95f; v += 0.05f)
            {
                float lx = tri[0].X + u * (tri[1].X - tri[0].X) + v * (tri[2].X - tri[0].X);
                float ly = tri[0].Y + u * (tri[1].Y - tri[0].Y) + v * (tri[2].Y - tri[0].Y);
                if (DistanceToEdges(tri, lx, ly) * scale < 1.5f) continue;
                var px = bitmap.GetPixel((int)(lx * scale), (int)(ly * scale));
                Assert.Equal(255, px.Alpha);
                Assert.Equal(Ink.R, px.Red);
                sampled++;
            }
        }
        Assert.True(sampled > 20);

        // Outside the triangle stays transparent.
        Assert.Equal(0, bitmap.GetPixel(1 * scale, 1 * scale).Alpha);
        Assert.Equal(0, bitmap.GetPixel(15 * scale, 10 * scale).Alpha);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Polyline_JoinsItsSegmentsWithoutANotch(int scale)
    {
        var chevron = new[] { new KumikoPoint(4, 3), new KumikoPoint(12, 10), new KumikoPoint(4, 17) };
        using var bitmap = Render(scale, 20, 20, ctx =>
            ctx.DrawPolyline(chevron, new KumikoPaint { Color = Ink, StrokeWidth = 3f, IsAntiAlias = true }));

        // The vertex and the pixel just beyond it on the outer side are covered by the round join.
        Assert.Equal(255, bitmap.GetPixel(12 * scale, 10 * scale).Alpha);
        Assert.Equal(255, bitmap.GetPixel((int)(12.8f * scale), 10 * scale).Alpha);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Checkmark_VertexIsCoveredAtEveryScale(int scale)
    {
        var tick = new[] { new KumikoPoint(4, 10), new KumikoPoint(8, 15), new KumikoPoint(16, 5) };
        using var bitmap = Render(scale, 20, 20, ctx =>
            ctx.DrawPolyline(tick, new KumikoPaint { Color = Ink, StrokeWidth = 2f, IsAntiAlias = true }));

        // The bottom vertex and the pixel just below it (the outer side of the corner) are solid: no notch.
        Assert.InRange((int)bitmap.GetPixel(8 * scale, 15 * scale).Alpha, 150, 255);
        Assert.InRange((int)bitmap.GetPixel(8 * scale, (int)(15.7f * scale)).Alpha, 150, 255); // round join, partly covered
        // Mid-stroke on both arms.
        Assert.InRange((int)bitmap.GetPixel(6 * scale, (int)(12.5f * scale)).Alpha, 90, 255);
        Assert.InRange((int)bitmap.GetPixel(12 * scale, 10 * scale).Alpha, 90, 255);
    }

    [Fact]
    public void RepeatedPolygonCalls_ReuseOnePath()
    {
        var tri = new[] { new KumikoPoint(2, 2), new KumikoPoint(10, 2), new KumikoPoint(6, 9) };
        using var bitmap = Render(1, 12, 12, ctx =>
        {
            for (int i = 0; i < 50; i++)
                ctx.FillPolygon(tri, new KumikoPaint { Color = Ink, IsAntiAlias = true });
        });

        Assert.Equal(255, bitmap.GetPixel(6, 4).Alpha);
    }

    private static SKBitmap Render(int scale, int width, int height, Action<SkiaDrawingContext> draw)
    {
        var bitmap = new SKBitmap(width * scale, height * scale, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        canvas.Scale(scale);
        using var ctx = new SkiaDrawingContext(canvas);
        draw(ctx);
        return bitmap;
    }

    private static float DistanceToEdges(KumikoPoint[] t, float x, float y)
    {
        float best = float.MaxValue;
        for (int i = 0; i < 3; i++)
        {
            var a = t[i];
            var b = t[(i + 1) % 3];
            float dx = b.X - a.X, dy = b.Y - a.Y;
            float d = Math.Abs(dy * (x - a.X) - dx * (y - a.Y)) / MathF.Sqrt(dx * dx + dy * dy);
            best = Math.Min(best, d);
        }
        return best;
    }
}

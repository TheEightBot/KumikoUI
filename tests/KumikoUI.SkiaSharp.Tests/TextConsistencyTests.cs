using KumikoUI.Core.Rendering;
using KumikoUI.SkiaSharp;
using SkiaSharp;

namespace KumikoUI.SkiaSharp.Tests;

/// <summary>Measuring, drawing, font fallback, ellipsis and dispose through the public drawing context.</summary>
[Collection(SkiaRegistrarCollection.Name)]
public class TextConsistencyTests : IDisposable
{
    public TextConsistencyTests() => TestFonts.Reset();

    public void Dispose() => SkiaFontRegistrar.Clear();

    private static KumikoPaint Paint(string family = TestFonts.Latin, float size = 20) =>
        new() { Color = KumikoColor.Black, Font = new KumikoFont(family, size) };

    private static float Measure(string text, KumikoPaint paint)
    {
        using var bitmap = new SKBitmap(1, 1);
        using var canvas = new SKCanvas(bitmap);
        using var ctx = new SkiaDrawingContext(canvas);
        return ctx.MeasureText(text, paint).Width;
    }

    private static SKBitmap Draw(int width, int height, Action<SkiaDrawingContext> draw)
    {
        var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        using (var ctx = new SkiaDrawingContext(canvas))
            draw(ctx);
        return bitmap;
    }

    // The first and last columns that contain ink (any non-white pixel), or (-1, -1).
    private static (int First, int Last) InkColumns(SKBitmap bitmap)
    {
        int first = -1, last = -1;
        for (int x = 0; x < bitmap.Width; x++)
        {
            for (int y = 0; y < bitmap.Height; y++)
            {
                if (bitmap.GetPixel(x, y).Red < 250)
                {
                    if (first < 0) first = x;
                    last = x;
                    break;
                }
            }
        }
        return (first, last);
    }

    [Theory]
    [InlineData("Hello world")]
    [InlineData("Wide MMMMMM text")]
    [InlineData("iiii llll")]
    public void Measured_width_matches_the_drawn_advance(string text)
    {
        var paint = Paint();
        float measured = Measure(text, paint);

        using var bitmap = Draw(400, 40, ctx => ctx.DrawText(text, 10, 28, paint));

        // The ink spans the advance minus the glyphs' side bearings: inside it and covering most of it.
        var (first, last) = InkColumns(bitmap);
        float inkWidth = last - first + 1;
        Assert.True(first >= 10 - 2, $"ink starts at {first}");
        Assert.True(inkWidth <= measured + 2, $"ink {inkWidth} is wider than the measured {measured}");
        Assert.True(inkWidth >= measured - 8, $"ink {inkWidth} is much narrower than the measured {measured}");
    }

    [Fact]
    public void Measured_width_scales_with_the_text_and_the_font_size()
    {
        float shortWidth = Measure("abc", Paint());
        float longWidth = Measure("abcabc", Paint());
        float bigWidth = Measure("abc", Paint(size: 40));

        Assert.Equal(shortWidth * 2, longWidth, 1);
        Assert.Equal(shortWidth * 2, bigWidth, 1);
    }

    [Fact]
    public void Text_drawn_in_a_rect_stays_inside_the_rect()
    {
        const int rectWidth = 80;
        using var bitmap = Draw(200, 40, ctx =>
            ctx.DrawTextInRect("This text is far too long for the rect", new KumikoRect(20, 0, rectWidth, 40), Paint(size: 16)));

        var (first, last) = InkColumns(bitmap);

        Assert.True(first >= 20 - 1, $"ink starts at {first}");
        Assert.True(last <= 20 + rectWidth, $"ink reaches x={last}");
    }

    [Fact]
    public void Unknown_family_falls_back_to_the_default_typeface()
    {
        float unknown = Measure("Hello world", Paint("NoSuchFamily-Kumiko"));
        float defaultFamily = Measure("Hello world", Paint("Default"));

        Assert.True(unknown > 0);
        Assert.Equal(defaultFamily, unknown, 2);
        using var bitmap = Draw(200, 40, ctx => ctx.DrawText("Hello world", 4, 28, Paint("NoSuchFamily-Kumiko")));
        Assert.True(InkColumns(bitmap).First >= 0, "nothing was drawn");
    }

    [Fact]
    public void Mixed_script_text_uses_a_registered_fallback_font_for_CJK()
    {
        var paint = Paint();
        TestFonts.Reset(withCjkFallback: true);
        using var cjkTypeface = SKTypeface.FromStream(TestFonts.Open("NotoSansJP-Regular.ttf"));
        using var cjkFont = new SKFont(cjkTypeface, 20);

        float mixed = Measure("Tokyo 東京 city", paint);
        float expected = Measure("Tokyo ", paint) + cjkFont.MeasureText("東京") + Measure(" city", paint);

        // The registered fallback is tried before the platform's font manager, so the result does not depend on
        // which system fonts are installed: the ideographs take the CJK font's advance.
        Assert.True(cjkFont.MeasureText("東京") > 30);
        Assert.Equal(expected, mixed, 1);
        using var latinOnly = Draw(300, 40, ctx => ctx.DrawText("Tokyo ", 4, 28, paint));
        using var mixedBitmap = Draw(300, 40, ctx => ctx.DrawText("Tokyo 東京", 4, 28, paint));
        Assert.True(InkColumns(mixedBitmap).Last > InkColumns(latinOnly).Last + 30);
    }

    [Fact]
    public void Ellipsis_never_splits_a_surrogate_pair()
    {
        const string grin = "\U0001F600";
        string text = "ab" + grin + grin + grin + grin + "cd";
        var paint = Paint();
        using var bitmap = new SKBitmap(200, 40);
        using var canvas = new SKCanvas(bitmap);
        using var ctx = new SkiaDrawingContext(canvas);

        // Cut at every width: no lone surrogate may reach Skia or the measurement.
        for (int width = 4; width < 120; width += 3)
        {
            ctx.DrawTextInRect(text, new KumikoRect(0, 0, width, 40), paint);
            var measured = ctx.MeasureText(text, paint, width, KumikoTextWrap.None);
            Assert.True(measured.Width >= 0);
        }
    }

    [Fact]
    public void Drawing_context_dispose_is_idempotent()
    {
        using var bitmap = new SKBitmap(40, 40);
        using var canvas = new SKCanvas(bitmap);
        using var ctx = new SkiaDrawingContext(canvas);
        ctx.DrawText("x", 4, 20, Paint());

        ctx.Dispose();
        ctx.Dispose();

        // The shared registrar typeface is still alive for the next frame.
        using var next = new SkiaDrawingContext(canvas);
        Assert.True(next.MeasureText("x", Paint()).Width > 0);
    }

    [Fact]
    public void Resource_cache_dispose_is_idempotent()
    {
        using var cache = new SkiaResourceCache();
        using (var bitmap = new SKBitmap(40, 40))
        using (var canvas = new SKCanvas(bitmap))
        using (var ctx = new SkiaDrawingContext(canvas, cache))
            ctx.DrawText("x", 4, 20, Paint());

        cache.Dispose();
        cache.Dispose();
    }
}

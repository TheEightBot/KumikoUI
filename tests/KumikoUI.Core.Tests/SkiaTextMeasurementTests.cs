using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;
using KumikoUI.SkiaSharp;
using SkiaSharp;
using static KumikoUI.Core.Tests.SkiaTestFonts;

namespace KumikoUI.Core.Tests;

/// <summary>
/// The SkiaSharp text path (#23): measurements and layouts are cached in the <see cref="SkiaResourceCache"/> across
/// frames, ellipsis cuts keep text elements whole, and wrapped text draws on several lines.
/// </summary>
// Registers a font, so it shares the registrar's collection with the other Skia tests.
[Collection("SkiaFontSeam")]
public class SkiaTextMeasurementTests : IDisposable
{
    private const string Family = "KumikoTextTestSans";
    private static readonly string Grin = char.ConvertFromUtf32(0x1F600);
    private static readonly KumikoColor Ink = KumikoColor.Black;

    public SkiaTextMeasurementTests()
    {
        SkiaFontRegistrar.Clear();
        SkiaFontRegistrar.RegisterTypefaceFromStream(Family, OpenSansStream());
    }

    public void Dispose() => SkiaFontRegistrar.Clear();

    private static KumikoPaint TextPaint(float size = 13) =>
        new() { Color = Ink, Font = new KumikoFont(Family, size) };

    // ── Cross-frame cache ─────────────────────────────────────────

    [Fact]
    public void Second_frame_of_a_static_grid_measures_no_text()
    {
        var h = RenderHarness.Create(rows: 40, width: 600, height: 500, configure: g =>
        {
            g.Source.SetItems(g.People.Select((p, i) => new RenderHarness.Person
            {
                Name = i % 3 == 0 ? $"A much longer name than the column can show {i}" : p.Name,
                City = i % 4 == 0 ? $"First line\nsecond line {i}" : p.City,
                Age = p.Age,
                Salary = p.Salary,
            }).ToList());
            g.Column("City").TextWrap = KumikoTextWrap.Word;
            g.Style.RowHeight = 40;
        });
        using var cache = new SkiaResourceCache();
        using var bitmap = new SKBitmap(600, 500);
        using var canvas = new SKCanvas(bitmap);

        void Frame()
        {
            using var ctx = new SkiaDrawingContext(canvas, cache);
            h.Renderer.Render(ctx, h.Source, h.Scroll, h.Selection, h.Style);
        }

        Frame();
        long afterFirst = cache.NativeTextMeasureCount;
        Frame();

        Assert.True(afterFirst > 0);
        Assert.Equal(afterFirst, cache.NativeTextMeasureCount);
        Assert.True(cache.TextMeasurementCount > 0);
    }

    [Fact]
    public void Cache_hits_allocate_nothing()
    {
        // Everything the text path looks up per call once warm. (SKCanvas.DrawText itself allocates a managed
        // text blob wrapper inside SkiaSharp on every call; that is outside the cache and not measured here.)
        using var cache = new SkiaResourceCache();
        using var bitmap = new SKBitmap(200, 100);
        using var canvas = new SKCanvas(bitmap);
        using var ctx = new SkiaDrawingContext(canvas, cache);
        var paint = TextPaint();

        void Draw()
        {
            ctx.MeasureText("Measured", paint);
            ctx.MeasureText("Measured wrapped text", paint, 60, KumikoTextWrap.Word);
            ctx.GetFontMetrics(paint);
            cache.MeasureTextWidth("A text far too long for the cell", paint.Font, out _);
            cache.GetTextLayout("A text far too long for the cell", paint.Font, 60, KumikoTextWrap.None, 1);
            cache.GetTextLayout("Line one\nLine two", paint.Font, 60, KumikoTextWrap.None, 1);
            cache.GetTextLayout("Wrapped onto a few lines", paint.Font, 60, KumikoTextWrap.Word, 3);
        }

        for (int i = 0; i < 20; i++) Draw();
        long least = long.MaxValue;
        for (int attempt = 0; attempt < 3 && least > 0; attempt++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++) Draw();
            least = Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        Assert.Equal(0, least);
    }

    [Fact]
    public void Measure_text_matches_Skia_and_is_measured_once()
    {
        using var cache = new SkiaResourceCache();
        using var canvas = new SKCanvas(new SKBitmap(10, 10));
        var paint = TextPaint();
        using var font = new SKFont(cache.GetTypeface(Family, false, false), 13);

        float first, second;
        using (var ctx = new SkiaDrawingContext(canvas, cache)) first = ctx.MeasureText("Hello world", paint).Width;
        long measured = cache.NativeTextMeasureCount;
        using (var ctx = new SkiaDrawingContext(canvas, cache)) second = ctx.MeasureText("Hello world", paint).Width;

        Assert.Equal(font.MeasureText("Hello world"), first, 3);
        Assert.Equal(first, second);
        Assert.Equal(measured, cache.NativeTextMeasureCount);
    }

    [Fact]
    public void A_different_font_size_is_measured_again()
    {
        using var cache = new SkiaResourceCache();
        using var canvas = new SKCanvas(new SKBitmap(10, 10));
        using var ctx = new SkiaDrawingContext(canvas, cache);

        float small = ctx.MeasureText("Hello", TextPaint(10)).Width;
        float large = ctx.MeasureText("Hello", TextPaint(20)).Width;

        Assert.True(large > small * 1.5f);
    }

    [Fact]
    public void A_registrar_change_drops_every_text_measurement()
    {
        using var cache = new SkiaResourceCache();
        using var canvas = new SKCanvas(new SKBitmap(10, 10));
        using (var ctx = new SkiaDrawingContext(canvas, cache))
            ctx.DrawTextInRect("Too long for this", new KumikoRect(0, 0, 30, 20), TextPaint());
        Assert.True(cache.TextMeasurementCount > 0);

        SkiaFontRegistrar.RegisterTypefaceFromStream("SomeOtherFamily", OpenSansStream());

        Assert.Equal(0, cache.TextMeasurementCount);
        long before = cache.NativeTextMeasureCount;
        using (var ctx = new SkiaDrawingContext(canvas, cache))
            ctx.DrawTextInRect("Too long for this", new KumikoRect(0, 0, 30, 20), TextPaint());
        Assert.True(cache.NativeTextMeasureCount > before);
    }

    [Fact]
    public void Clear_drops_every_text_measurement()
    {
        using var cache = new SkiaResourceCache();
        using var canvas = new SKCanvas(new SKBitmap(10, 10));
        using (var ctx = new SkiaDrawingContext(canvas, cache))
            ctx.MeasureText("Hello", TextPaint());

        cache.Clear();

        Assert.Equal(0, cache.TextMeasurementCount);
    }

    [Fact]
    public void Text_measurements_are_bounded()
    {
        using var cache = new SkiaResourceCache(16, 8, 4, maxTextMeasurements: 10);
        using var canvas = new SKCanvas(new SKBitmap(10, 10));
        using var ctx = new SkiaDrawingContext(canvas, cache);
        var paint = TextPaint();

        for (int i = 0; i < 100; i++)
            ctx.DrawTextInRect($"Row {i} with a long value", new KumikoRect(0, 0, 40, 20), paint);

        Assert.Equal(10, cache.MaxTextMeasurements);
        Assert.Equal(20, cache.TextMeasurementCount);   // 10 widths and 10 layouts
    }

    [Fact]
    public void Default_capacity_is_used_by_the_existing_constructors()
    {
        using var a = new SkiaResourceCache();
        using var b = new SkiaResourceCache(16, 8, 4);

        Assert.Equal(SkiaResourceCache.DefaultMaxTextMeasurements, a.MaxTextMeasurements);
        Assert.Equal(SkiaResourceCache.DefaultMaxTextMeasurements, b.MaxTextMeasurements);
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkiaResourceCache(16, 8, 4, 0));
    }

    // ── Ellipsis ──────────────────────────────────────────────────

    [Fact]
    public void Emoji_at_the_ellipsis_boundary_is_never_split()
    {
        using var cache = new SkiaResourceCache();
        string text = "Status " + Grin + Grin + " done";
        var font = new KumikoFont(Family, 13);
        using var canvas = new SKCanvas(new SKBitmap(10, 10));
        using var ctx = new SkiaDrawingContext(canvas, cache);
        float full = ctx.MeasureText(text, TextPaint()).Width;

        for (float width = 1; width < full; width += 0.5f)
        {
            var lines = cache.GetTextLayout(text, font, width, KumikoTextWrap.None, 1);
            string shown = Assert.Single(Enumerable.Range(0, lines.Count).Select(lines.GetLine));
            Assert.EndsWith(KumikoTextLayout.Ellipsis, shown);
            string kept = shown[..^KumikoTextLayout.Ellipsis.Length];
            Assert.StartsWith(kept, text, StringComparison.Ordinal);
            Assert.False(kept.Length > 0 && char.IsHighSurrogate(kept[^1]), $"split emoji at width {width}: '{shown}'");
            Assert.True(lines.GetLineWidth(0) <= width || shown == KumikoTextLayout.Ellipsis, $"'{shown}' overflows {width}");
        }
    }

    [Fact]
    public void Right_aligned_cut_text_stays_inside_its_rectangle()
    {
        using var bitmap = Draw(200, 30, ctx => ctx.DrawTextInRect("A value much too long for the cell",
            new KumikoRect(50, 0, 80, 30), TextPaint(), KumikoTextAlignment.Right, KumikoVerticalAlignment.Center,
            clip: false));

        var (left, right) = InkColumns(bitmap);
        Assert.InRange(left, 50, 130);
        Assert.InRange(right, 50, 130);
    }

    // ── Line breaks and wrapping ──────────────────────────────────

    [Fact]
    public void A_line_break_on_one_line_draws_as_a_space_not_a_missing_glyph_box()
    {
        var rect = new KumikoRect(0, 0, 200, 30);
        using var withBreak = Draw(200, 30, ctx => ctx.DrawTextInRect("one\ntwo", rect, TextPaint()));
        using var withSpace = Draw(200, 30, ctx => ctx.DrawTextInRect("one two", rect, TextPaint()));

        Assert.Equal(withSpace.Pixels, withBreak.Pixels);
    }

    [Fact]
    public void Wrapped_text_draws_on_several_lines_inside_the_rectangle()
    {
        var rect = new KumikoRect(10, 10, 90, 80);
        using var oneLine = Draw(120, 100, ctx => ctx.DrawTextInRect("Several words that wrap", rect, TextPaint()));
        using var wrapped = Draw(120, 100, ctx => ctx.DrawTextInRect("Several words that wrap", rect, TextPaint(),
            KumikoTextAlignment.Left, KumikoVerticalAlignment.Center, clip: true, KumikoTextWrap.Word));

        Assert.Equal(1, InkBands(oneLine));
        Assert.Equal(2, InkBands(wrapped));
        var (left, right) = InkColumns(wrapped);
        Assert.InRange(left, 10, 100);
        Assert.InRange(right, 10, 100);
    }

    [Fact]
    public void Wrapped_measure_reports_the_lines_height()
    {
        using var cache = new SkiaResourceCache();
        using var canvas = new SKCanvas(new SKBitmap(10, 10));
        using var ctx = new SkiaDrawingContext(canvas, cache);
        var paint = TextPaint();
        var metrics = ctx.GetFontMetrics(paint);

        var oneLine = ctx.MeasureText("Several words that wrap", paint, float.PositiveInfinity, KumikoTextWrap.None);
        var wrapped = ctx.MeasureText("Several words that wrap", paint, 90, KumikoTextWrap.Word);

        Assert.Equal(ctx.MeasureText("Several words that wrap", paint).Width, oneLine.Width, 3);
        Assert.Equal(metrics.TextHeight, oneLine.Height, 3);
        Assert.True(wrapped.Width <= 90);
        Assert.Equal(metrics.TextHeight + metrics.LineHeight, wrapped.Height, 3);
    }

    // ── Helpers ───────────────────────────────────────────────────

    private static SKBitmap Draw(int width, int height, Action<SkiaDrawingContext> draw)
    {
        var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        using var cache = new SkiaResourceCache();
        using (var ctx = new SkiaDrawingContext(canvas, cache))
            draw(ctx);
        return bitmap;
    }

    /// <summary>The leftmost and rightmost columns with any ink.</summary>
    private static (int Left, int Right) InkColumns(SKBitmap bitmap)
    {
        int left = int.MaxValue, right = int.MinValue;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
                if (bitmap.GetPixel(x, y).Alpha > 0)
                {
                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                }
        return (left, right);
    }

    /// <summary>The number of horizontal bands of rows with ink, separated by blank rows.</summary>
    private static int InkBands(SKBitmap bitmap)
    {
        int bands = 0;
        bool inBand = false;
        for (int y = 0; y < bitmap.Height; y++)
        {
            bool ink = false;
            for (int x = 0; x < bitmap.Width && !ink; x++)
                ink = bitmap.GetPixel(x, y).Alpha > 0;
            if (ink && !inBand) bands++;
            inBand = ink;
        }
        return bands;
    }
}

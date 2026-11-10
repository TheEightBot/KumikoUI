using KumikoUI.Core.Rendering;
using KumikoUI.SkiaSharp;
using SkiaSharp;
using static KumikoUI.Core.Tests.SkiaTestFonts;

namespace KumikoUI.Core.Tests;

// The registrar, the family matcher and the typeface pool are process-wide, so the Skia tests share a
// collection and never run in parallel with each other.
[Collection("SkiaFontSeam")]
public class SkiaResourceCacheTests : IDisposable
{
    private const string Family = "KumikoCacheTestSans";
    private readonly Func<string, SKFontStyle, SKTypeface?> _originalMatcher = SkiaDrawingContext.FamilyMatcher;
    private readonly SKBitmap _bitmap = new(100, 40);
    private readonly SKCanvas _canvas;

    public SkiaResourceCacheTests()
    {
        _canvas = new SKCanvas(_bitmap);
        SkiaFontRegistrar.Clear();
    }

    public void Dispose()
    {
        SkiaDrawingContext.FamilyMatcher = _originalMatcher;
        SkiaFontRegistrar.Clear();
        _canvas.Dispose();
        _bitmap.Dispose();
    }

    [Fact]
    public void Second_frame_reuses_the_same_native_objects()
    {
        using var cache = new SkiaResourceCache();
        var paint = Paint(bold: true);

        DrawFrame(_canvas, cache, paint);
        var font = cache.GetFont(paint.Font);
        var skPaint = cache.GetPaint(paint, SKPaintStyle.Fill);
        var typeface = cache.GetTypeface("Default", isBold: true, isItalic: false);
        var counts = (cache.PaintCount, cache.FontCount, cache.TypefaceCount);

        DrawFrame(_canvas, cache, paint);

        Assert.Same(font, cache.GetFont(paint.Font));
        Assert.Same(skPaint, cache.GetPaint(paint, SKPaintStyle.Fill));
        Assert.Same(typeface, cache.GetTypeface("Default", isBold: true, isItalic: false));
        Assert.Equal(counts, (cache.PaintCount, cache.FontCount, cache.TypefaceCount));
    }

    [Fact]
    public void Ending_a_frame_does_not_dispose_cached_objects()
    {
        using var cache = new SkiaResourceCache();
        var released = new List<object>();
        cache.ResourceReleased = released.Add;
        var paint = Paint();

        DrawFrame(_canvas, cache, paint);

        Assert.Empty(released);
        Assert.False(IsDisposed(cache.GetFont(paint.Font)));
        Assert.False(IsDisposed(cache.GetPaint(paint, SKPaintStyle.Fill)));
        Assert.True(cache.FontCount > 0);
        Assert.True(cache.PaintCount > 0);
    }

    [Fact]
    public void Legacy_constructor_disposes_its_private_cache_with_the_frame()
    {
        var ctx = new SkiaDrawingContext(_canvas);
        var paint = Paint(bold: true);
        ctx.DrawText("Hello", 4, 20, paint);
        var cache = ctx.ResourceCache;
        var font = cache.GetFont(paint.Font);
        var skPaint = cache.GetPaint(paint, SKPaintStyle.Fill);

        ctx.Dispose();
        ctx.Dispose();

        Assert.True(cache.IsDisposed);
        Assert.True(IsDisposed(font));
        Assert.True(IsDisposed(skPaint));
        Assert.True(SKTypeface.Default.GlyphCount > 0);
    }

    [Fact]
    public void Disposing_the_cache_releases_each_object_once_and_never_shared_or_registered_typefaces()
    {
        SkiaFontRegistrar.RegisterTypefaceFromStream(Family, OpenSansStream());
        Assert.True(SkiaFontRegistrar.TryGetTypeface(Family, out var registered));

        var cache = new SkiaResourceCache();
        var released = new List<object>();
        cache.ResourceReleased = released.Add;

        var paints = new[] { Paint(), Paint(bold: true), Paint(italic: true), Paint(Family), Paint(Family, 20) };
        foreach (var paint in paints)
            DrawFrame(_canvas, cache, paint);

        var fonts = paints.Select(p => cache.GetFont(p.Font)).ToList();
        var leased = cache.GetTypeface("Default", isBold: true, isItalic: false);
        Assert.Equal(5, fonts.Distinct().Count());
        // Where the platform has no bold face at all the cache falls back to the shared default typeface.
        bool isLeased = !ReferenceEquals(leased, SKTypeface.Default);
        if (isLeased)
            Assert.True(SystemTypefacePool.LeaseCount(leased) >= 1);

        cache.Dispose();
        cache.Dispose();

        var disposables = released.Where(r => r is not SKTypeface).ToList();
        Assert.Equal(disposables.Count, disposables.Distinct().Count());
        Assert.All(fonts, f => Assert.Contains(f, disposables));
        Assert.All(fonts, f => Assert.True(IsDisposed(f)));
        Assert.All(released.OfType<SKPaint>(), p => Assert.True(IsDisposed(p)));
        Assert.DoesNotContain(released, r => ReferenceEquals(r, SKTypeface.Default));
        Assert.DoesNotContain(released, r => ReferenceEquals(r, registered));
        Assert.False(IsDisposed(registered!));
        Assert.True(SKTypeface.Default.GlyphCount > 0);
        Assert.Equal(0, SystemTypefacePool.LeaseCount(leased));
        Assert.Throws<ObjectDisposedException>(() => new SkiaDrawingContext(_canvas, cache));
    }

    [Fact]
    public void Paints_are_bounded_and_evict_the_least_recently_used()
    {
        using var cache = new SkiaResourceCache(maxPaints: 4, maxFonts: 4, maxTypefaces: 4);
        var released = new List<object>();
        cache.ResourceReleased = released.Add;

        KumikoPaint Color(byte i) => Paint(color: KumikoColor.FromRgb(i, 0, 0));

        var first = cache.GetPaint(Color(0), SKPaintStyle.Fill);
        for (byte i = 1; i < 4; i++)
            cache.GetPaint(Color(i), SKPaintStyle.Fill);

        Assert.Same(first, cache.GetPaint(Color(0), SKPaintStyle.Fill)); // now most recently used
        var second = cache.GetPaint(Color(1), SKPaintStyle.Fill);         // ... and this one
        cache.GetPaint(Color(4), SKPaintStyle.Fill);                       // evicts color 2
        cache.GetPaint(Color(5), SKPaintStyle.Fill);                       // evicts color 3

        Assert.Equal(4, cache.PaintCount);
        Assert.Equal(2, released.Count);
        Assert.All(released, r => Assert.True(IsDisposed((SKObject)r)));
        Assert.Same(first, cache.GetPaint(Color(0), SKPaintStyle.Fill));
        Assert.Same(second, cache.GetPaint(Color(1), SKPaintStyle.Fill));

        for (byte i = 10; i < 200; i++)
            cache.GetPaint(Color(i), SKPaintStyle.Fill);
        Assert.Equal(4, cache.PaintCount);
    }

    [Fact]
    public void Objects_evicted_during_a_frame_stay_alive_until_the_frame_ends()
    {
        using var cache = new SkiaResourceCache(maxPaints: 2, maxFonts: 2, maxTypefaces: 2);
        var ctx = new SkiaDrawingContext(_canvas, cache);

        var paint = Paint(size: 10);
        ctx.DrawText("Hello", 4, 20, paint);
        var font = cache.GetFont(paint.Font);
        for (int size = 11; size < 30; size++)
            ctx.DrawText("Hello", 4, 20, Paint(size: size)); // font churn evicts the first font

        Assert.Equal(2, cache.FontCount);
        Assert.False(IsDisposed(font));
        Assert.True(font.MeasureText("Hello") > 0);

        ctx.Dispose();
        Assert.True(IsDisposed(font));
        Assert.Equal(2, cache.FontCount);
    }

    [Fact]
    public void Typefaces_are_bounded_and_take_their_fonts_with_them()
    {
        using var cache = new SkiaResourceCache(maxPaints: 8, maxFonts: 8, maxTypefaces: 2);

        foreach (var (bold, italic) in new[] { (false, false), (true, false), (false, true), (true, true) })
            DrawFrame(_canvas, cache, Paint(bold: bold, italic: italic));

        Assert.Equal(2, cache.TypefaceCount);
        Assert.Equal(2, cache.FontCount);
        Assert.True(DrawFrame(_canvas, cache, Paint()) > 0);
    }

    [Fact]
    public void A_font_manager_typeface_shared_by_two_caches_is_disposed_by_the_last_one()
    {
        // A typeface that really disposes (font-manager typefaces ignore Dispose on some platforms).
        var shared = CreateOpenSans();
        SkiaDrawingContext.FamilyMatcher = (_, _) => shared;

        var a = new SkiaResourceCache();
        var b = new SkiaResourceCache();
        DrawFrame(_canvas, a, Paint("Anything", bold: true));
        DrawFrame(_canvas, b, Paint("Anything", bold: true));
        Assert.Same(shared, a.GetTypeface("Anything", isBold: true, isItalic: false));
        Assert.Equal(2, SystemTypefacePool.LeaseCount(shared));

        a.Dispose();
        Assert.False(IsDisposed(shared));
        Assert.True(DrawFrame(_canvas, b, Paint("Anything", bold: true)) > 0);

        b.Dispose();
        Assert.True(IsDisposed(shared));
    }

    [Fact]
    public void Clear_empties_the_cache_and_the_next_frame_rebuilds_it()
    {
        using var cache = new SkiaResourceCache();
        var paint = Paint(bold: true);
        DrawFrame(_canvas, cache, paint);
        var font = cache.GetFont(paint.Font);

        cache.Clear();

        Assert.Equal(0, cache.FontCount);
        Assert.Equal(0, cache.PaintCount);
        Assert.Equal(0, cache.TypefaceCount);
        Assert.True(IsDisposed(font));
        Assert.True(DrawFrame(_canvas, cache, paint) > 0);
        Assert.NotSame(font, cache.GetFont(paint.Font));
    }

    [Fact]
    public void A_disposed_cache_is_no_longer_tracked_by_the_registrar()
    {
        int before = SkiaFontRegistrar.AttachedCacheCount;
        var cache = new SkiaResourceCache();
        Assert.Equal(before + 1, SkiaFontRegistrar.AttachedCacheCount);
        cache.Dispose();
        Assert.Equal(before, SkiaFontRegistrar.AttachedCacheCount);
    }

    [Fact]
    public void Constructor_rejects_bad_arguments()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkiaResourceCache(0, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkiaResourceCache(1, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkiaResourceCache(1, 1, 0));
        Assert.Throws<ArgumentNullException>(() => new SkiaDrawingContext(_canvas, null!));
        using var cache = new SkiaResourceCache();
        Assert.Throws<ArgumentNullException>(() => new SkiaDrawingContext(null!, cache));
    }
}

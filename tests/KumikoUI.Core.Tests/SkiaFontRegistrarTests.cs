using KumikoUI.SkiaSharp;
using SkiaSharp;
using static KumikoUI.Core.Tests.SkiaTestFonts;

namespace KumikoUI.Core.Tests;

[Collection("SkiaFontSeam")]
public class SkiaFontRegistrarTests : IDisposable
{
    private const string Family = "KumikoRegistrarTestSans";
    private readonly Func<string, SKFontStyle, SKTypeface?> _originalMatcher = SkiaDrawingContext.FamilyMatcher;
    private readonly SKBitmap _bitmap = new(100, 40);
    private readonly SKCanvas _canvas;

    public SkiaFontRegistrarTests()
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

    private static SKTypeface Registered(string family)
    {
        Assert.True(SkiaFontRegistrar.TryGetTypeface(family, out var typeface));
        return typeface!;
    }

    [Fact]
    public void IsRegistered_tracks_every_family_a_typeface_is_registered_under()
    {
        using var typeface = CreateOpenSans();
        using var other = CreateOpenSans();

        for (int i = 0; i < 1000; i++)
            SkiaFontRegistrar.RegisterTypeface($"Alias{i}", other);
        SkiaFontRegistrar.RegisterTypeface("A", typeface);
        SkiaFontRegistrar.RegisterTypeface("B", typeface);

        Assert.True(SkiaFontRegistrar.IsRegistered(typeface));
        Assert.True(SkiaFontRegistrar.IsRegistered(other));
        Assert.False(SkiaFontRegistrar.IsRegistered(SKTypeface.Default));

        Assert.True(SkiaFontRegistrar.Unregister("a")); // family names are case-insensitive
        Assert.True(SkiaFontRegistrar.IsRegistered(typeface));
        Assert.True(SkiaFontRegistrar.Unregister("B"));
        Assert.False(SkiaFontRegistrar.IsRegistered(typeface));
        Assert.False(SkiaFontRegistrar.Unregister("B"));

        // Caller-owned typefaces are never disposed by the registrar.
        SkiaFontRegistrar.Clear();
        Assert.False(SkiaFontRegistrar.IsRegistered(other));
        Assert.False(IsDisposed(typeface));
        Assert.False(IsDisposed(other));
    }

    [Fact]
    public void Every_change_increments_the_version()
    {
        long v0 = SkiaFontRegistrar.Version;
        SkiaFontRegistrar.RegisterTypefaceFromStream(Family, OpenSansStream());
        long v1 = SkiaFontRegistrar.Version;
        SkiaFontRegistrar.Unregister(Family);
        long v2 = SkiaFontRegistrar.Version;
        SkiaFontRegistrar.Clear();
        long v3 = SkiaFontRegistrar.Version;

        Assert.True(v0 < v1 && v1 < v2 && v2 < v3);
        Assert.False(SkiaFontRegistrar.Unregister(Family));
        Assert.Equal(v3, SkiaFontRegistrar.Version);
    }

    [Fact]
    public void Unregister_drops_the_typeface_from_caches_before_disposing_it()
    {
        SkiaFontRegistrar.RegisterTypefaceFromStream(Family, OpenSansStream());
        var typeface = Registered(Family);
        using var cache = new SkiaResourceCache();
        var paint = Paint(Family);

        DrawFrame(_canvas, cache, paint);
        var font = cache.GetFont(paint.Font);
        Assert.Same(typeface, cache.GetTypeface(Family, false, false));
        bool? typefaceAliveWhenCacheDropped = null;
        cache.ResourceReleased = _ => typefaceAliveWhenCacheDropped ??= !IsDisposed(typeface);

        Assert.True(SkiaFontRegistrar.Unregister(Family));

        Assert.True(typefaceAliveWhenCacheDropped);  // caches let go first ...
        Assert.True(IsDisposed(typeface));          // ... then the registrar disposed what it owned
        Assert.True(IsDisposed(font));              // the cache dropped the font built on it
        Assert.Equal(0, cache.FontCount);
        Assert.True(DrawFrame(_canvas, cache, paint) > 0); // falls back to a system typeface
        var fallback = cache.GetTypeface(Family, false, false);
        Assert.NotSame(typeface, fallback);
        Assert.False(IsDisposed(fallback));
    }

    [Fact]
    public void Unregister_during_a_frame_keeps_the_frame_working()
    {
        SkiaFontRegistrar.RegisterTypefaceFromStream(Family, OpenSansStream());
        using var cache = new SkiaResourceCache();
        var paint = Paint(Family);

        using var ctx = new SkiaDrawingContext(_canvas, cache);
        ctx.DrawText("Hello", 4, 20, paint);
        var font = cache.GetFont(paint.Font);

        SkiaFontRegistrar.Unregister(Family);

        // The font handed out earlier in the frame stays usable until the frame ends.
        Assert.False(IsDisposed(font));
        Assert.True(font.MeasureText("Hello") > 0);
        ctx.DrawText("Hello", 4, 20, paint);
        Assert.True(ctx.MeasureText("Hello", paint).Width > 0);
        Assert.NotSame(font, cache.GetFont(paint.Font));

        ctx.Dispose();
        Assert.True(IsDisposed(font));
    }

    [Fact]
    public void Clear_while_grids_are_alive_is_safe()
    {
        SkiaFontRegistrar.RegisterTypefaceFromStream(Family, OpenSansStream());
        var owned = Registered(Family);
        using var callerOwned = CreateOpenSans();
        SkiaFontRegistrar.RegisterTypeface("CallerOwned", callerOwned);

        using var a = new SkiaResourceCache();
        using var b = new SkiaResourceCache();
        foreach (var cache in new[] { a, b })
        {
            DrawFrame(_canvas, cache, Paint(Family));
            DrawFrame(_canvas, cache, Paint("CallerOwned", 18));
        }

        SkiaFontRegistrar.Clear();

        Assert.True(IsDisposed(owned));
        Assert.False(IsDisposed(callerOwned));
        foreach (var cache in new[] { a, b })
        {
            Assert.Equal(0, cache.FontCount);
            Assert.True(DrawFrame(_canvas, cache, Paint(Family)) > 0);
            Assert.True(DrawFrame(_canvas, cache, Paint("CallerOwned", 18)) > 0);
            Assert.NotSame(owned, cache.GetTypeface(Family, false, false));
            Assert.NotSame(callerOwned, cache.GetTypeface("CallerOwned", false, false));
        }
    }

    [Fact]
    public void Replacing_a_family_disposes_the_old_owned_typeface_and_caches_pick_up_the_new_one()
    {
        SkiaFontRegistrar.RegisterTypefaceFromStream(Family, OpenSansStream());
        var first = Registered(Family);
        using var cache = new SkiaResourceCache();
        DrawFrame(_canvas, cache, Paint(Family));

        SkiaFontRegistrar.RegisterTypefaceFromStream(Family, OpenSansStream());
        var second = Registered(Family);

        Assert.NotSame(first, second);
        Assert.True(IsDisposed(first));
        Assert.True(DrawFrame(_canvas, cache, Paint(Family)) > 0);
        Assert.Same(second, cache.GetTypeface(Family, false, false));

        // Replacing a stream-owned family with a caller-owned typeface also disposes the owned one.
        using var callerOwned = CreateOpenSans();
        SkiaFontRegistrar.RegisterTypeface(Family, callerOwned);
        Assert.True(IsDisposed(second));
        Assert.Same(callerOwned, cache.GetTypeface(Family, false, false));
    }

    [Fact]
    public void Registering_a_family_replaces_a_fallback_the_cache_resolved_earlier()
    {
        SkiaDrawingContext.FamilyMatcher = static (_, _) => null; // the platform does not know the family
        using var cache = new SkiaResourceCache();
        DrawFrame(_canvas, cache, Paint(Family));
        Assert.Same(SKTypeface.Default, cache.GetTypeface(Family, false, false));

        SkiaFontRegistrar.RegisterTypefaceFromStream(Family, OpenSansStream());

        Assert.Same(Registered(Family), cache.GetTypeface(Family, false, false));
        Assert.True(DrawFrame(_canvas, cache, Paint(Family)) > 0);
    }

    [Fact]
    public void Concurrent_registration_while_rendering_is_safe()
    {
        const int Threads = 8;
        const int Iterations = 25;
        using var cache = new SkiaResourceCache();
        using var start = new Barrier(Threads + 1);

        // Worker exceptions fault their task; WaitAll below rethrows them, failing the test with the real error.
        var workers = Enumerable.Range(0, Threads).Select(t => Task.Factory.StartNew(() =>
        {
            start.SignalAndWait();
            for (int i = 0; i < Iterations; i++)
            {
                SkiaFontRegistrar.RegisterTypefaceFromStream($"Own{t}", OpenSansStream());
                SkiaFontRegistrar.RegisterTypefaceFromStream("Shared", OpenSansStream());
                Assert.True(SkiaFontRegistrar.TryGetTypeface($"Own{t}", out var mine));
                Assert.True(SkiaFontRegistrar.IsRegistered(mine!));
                if (i % 5 == 4)
                    SkiaFontRegistrar.Unregister($"Own{t}");
            }
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();

        start.SignalAndWait();

        // Render on this thread while the workers churn the registry.
        while (workers.Any(w => !w.IsCompleted))
        {
            for (int t = 0; t < Threads; t++)
                DrawFrame(_canvas, cache, Paint($"Own{t}"));
            DrawFrame(_canvas, cache, Paint("Shared", 16));
        }
        Task.WaitAll(workers);

        Assert.True(SkiaFontRegistrar.TryGetTypeface("Shared", out var shared));
        Assert.False(IsDisposed(shared!));
        Assert.True(DrawFrame(_canvas, cache, Paint("Shared", 16)) > 0);
        Assert.Same(shared, cache.GetTypeface("Shared", false, false));
    }

    // ── Styles (#74) ──────────────────────────────────────────────

    [Fact]
    public void A_registered_bold_variant_is_used_for_a_bold_header()
    {
        SkiaFontRegistrar.RegisterTypefaceFromStream(Family, OpenSansStream());
        SkiaFontRegistrar.RegisterTypefaceFromStream(Family, OpenSansSemiboldStream(), SKFontStyle.Bold);
        Assert.True(SkiaFontRegistrar.TryGetTypeface(Family, out var regular));
        Assert.True(SkiaFontRegistrar.TryGetTypeface(Family, SKFontStyle.Bold, out var bold));
        Assert.NotSame(regular, bold);
        Assert.Equal(600, bold!.FontWeight);

        var h = TestHelpers.RenderHarness.Create(width: 400, height: 200);
        h.Style.HeaderFont = new KumikoUI.Core.Rendering.KumikoFont(Family, 14, bold: true);
        h.Style.CellFont = new KumikoUI.Core.Rendering.KumikoFont(Family, 13);
        using var cache = new SkiaResourceCache();
        using var bitmap = new SKBitmap(400, 200);
        using var canvas = new SKCanvas(bitmap);
        using (var ctx = new SkiaDrawingContext(canvas, cache))
            h.Renderer.Render(ctx, h.Source, h.Scroll, h.Selection, h.Style);

        var headerFont = cache.GetFont(h.Style.HeaderFont);
        Assert.Same(bold, headerFont.Typeface);
        Assert.False(headerFont.Embolden);      // a real bold face: no synthetic bold on top
        Assert.Same(regular, cache.GetFont(h.Style.CellFont).Typeface);
    }

    [Fact]
    public void Bold_and_italic_text_in_a_family_registered_only_regular_is_synthesized()
    {
        SkiaFontRegistrar.RegisterTypefaceFromStream(Family, OpenSansStream());
        var regular = Registered(Family);
        using var cache = new SkiaResourceCache();

        var boldFont = cache.GetFont(new KumikoUI.Core.Rendering.KumikoFont(Family, 20, bold: true));
        var italicFont = cache.GetFont(new KumikoUI.Core.Rendering.KumikoFont(Family, 20, italic: true));
        var regularFont = cache.GetFont(new KumikoUI.Core.Rendering.KumikoFont(Family, 20));

        Assert.Same(regular, boldFont.Typeface);
        Assert.True(boldFont.Embolden);
        Assert.Equal(0f, boldFont.SkewX);
        Assert.Same(regular, italicFont.Typeface);
        Assert.False(italicFont.Embolden);
        Assert.Equal(-0.25f, italicFont.SkewX);
        Assert.False(regularFont.Embolden);
        Assert.Equal(0f, regularFont.SkewX);

        // Synthetic bold draws heavier strokes (how much heavier depends on the platform's glyph rasterizer).
        var rect = new KumikoUI.Core.Rendering.KumikoRect(0, 0, 400, 80);
        using var plain = Render(400, 80, ctx => ctx.DrawTextInRect("Header", rect, Paint(Family, 48)));
        using var heavy = Render(400, 80, ctx => ctx.DrawTextInRect("Header", rect, Paint(Family, 48, bold: true)));
        Assert.True(Ink(heavy) > Ink(plain) * 1.05, $"bold ink {Ink(heavy)} vs regular {Ink(plain)}");
    }

    [Fact]
    public void A_registered_italic_variant_is_preferred_to_a_bold_one_for_bold_italic_text()
    {
        using var regular = CreateOpenSans();
        using var bold = CreateOpenSans();
        using var italic = CreateOpenSans();
        SkiaFontRegistrar.RegisterTypeface(Family, regular, SKFontStyle.Normal);
        SkiaFontRegistrar.RegisterTypeface(Family, bold, SKFontStyle.Bold);
        SkiaFontRegistrar.RegisterTypeface(Family, italic, SKFontStyle.Italic);
        using var cache = new SkiaResourceCache();

        // Slant is matched before weight, so bold italic is the italic face emboldened.
        var boldItalic = cache.GetFont(new KumikoUI.Core.Rendering.KumikoFont(Family, 14, bold: true, italic: true));
        Assert.Same(italic, boldItalic.Typeface);
        Assert.True(boldItalic.Embolden);
        Assert.Equal(0f, boldItalic.SkewX);

        using var semibold = new SKFontStyle(SKFontStyleWeight.SemiBold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
        using var light = new SKFontStyle(SKFontStyleWeight.Light, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
        Assert.True(SkiaFontRegistrar.TryGetTypeface(Family, semibold, out var heavier));
        Assert.Same(bold, heavier);             // above 500, heavier faces first
        Assert.True(SkiaFontRegistrar.TryGetTypeface(Family, light, out var lighter));
        Assert.Same(regular, lighter);          // the closest one
        Assert.True(SkiaFontRegistrar.TryGetTypeface(Family, out var plain));
        Assert.Same(regular, plain);
        Assert.False(SkiaFontRegistrar.TryGetTypeface("KumikoNotRegistered", SKFontStyle.Bold, out var none));
        Assert.Null(none);
    }

    [Fact]
    public void Registering_without_a_style_replaces_every_variant_and_disposes_the_owned_ones()
    {
        SkiaFontRegistrar.RegisterTypefaceFromStream(Family, OpenSansStream());
        SkiaFontRegistrar.RegisterTypefaceFromStream(Family, OpenSansSemiboldStream(), SKFontStyle.Bold);
        var regular = Registered(Family);
        Assert.True(SkiaFontRegistrar.TryGetTypeface(Family, SKFontStyle.Bold, out var bold));
        using var replacement = CreateOpenSans();
        long version = SkiaFontRegistrar.Version;

        SkiaFontRegistrar.RegisterTypeface(Family, replacement);

        Assert.True(SkiaFontRegistrar.Version > version);
        Assert.True(IsDisposed(regular));
        Assert.True(IsDisposed(bold!));
        Assert.True(SkiaFontRegistrar.TryGetTypeface(Family, SKFontStyle.Bold, out var now));
        Assert.Same(replacement, now);
        Assert.False(SkiaFontRegistrar.IsRegistered(regular));
    }

    [Fact]
    public void Replacing_a_variant_keeps_the_others_and_Unregister_removes_them_all()
    {
        SkiaFontRegistrar.RegisterTypefaceFromStream(Family, OpenSansStream());
        var regular = Registered(Family);
        SkiaFontRegistrar.RegisterTypefaceFromStream(Family, OpenSansSemiboldStream(), SKFontStyle.Bold);
        Assert.True(SkiaFontRegistrar.TryGetTypeface(Family, SKFontStyle.Bold, out var firstBold));
        using var cache = new SkiaResourceCache();
        Assert.Same(firstBold, cache.GetTypeface(Family, true, false));

        SkiaFontRegistrar.RegisterTypefaceFromStream(Family, OpenSansSemiboldStream(), SKFontStyle.Bold);

        Assert.True(IsDisposed(firstBold!));
        Assert.False(IsDisposed(regular));
        Assert.True(SkiaFontRegistrar.TryGetTypeface(Family, SKFontStyle.Bold, out var secondBold));
        Assert.Same(secondBold, cache.GetTypeface(Family, true, false));
        Assert.Same(regular, cache.GetTypeface(Family, false, false));

        Assert.True(SkiaFontRegistrar.Unregister(Family));
        Assert.True(IsDisposed(regular));
        Assert.True(IsDisposed(secondBold!));
        Assert.False(SkiaFontRegistrar.TryGetTypeface(Family, out _));
    }

    [Fact]
    public void Registering_the_same_typeface_again_for_another_style_never_disposes_it()
    {
        SkiaFontRegistrar.RegisterTypefaceFromStream(Family, OpenSansStream());
        var typeface = Registered(Family);
        SkiaFontRegistrar.RegisterTypeface(Family, typeface, SKFontStyle.Bold);

        SkiaFontRegistrar.RegisterTypeface(Family, typeface);   // replaces both registrations with itself

        Assert.False(IsDisposed(typeface));
        Assert.True(SkiaFontRegistrar.IsRegistered(typeface));
        Assert.True(SkiaFontRegistrar.Unregister(Family));
        Assert.True(IsDisposed(typeface));                      // still owned by the registrar
    }

    [Fact]
    public void Fallback_families_are_validated_versioned_and_emptied_by_Clear()
    {
        long version = SkiaFontRegistrar.Version;

        SkiaFontRegistrar.SetFallbackFamilies("NotoSansJP", "Noto Color Emoji");

        Assert.Equal(["NotoSansJP", "Noto Color Emoji"], SkiaFontRegistrar.FallbackFamilies);
        Assert.True(SkiaFontRegistrar.Version > version);
        Assert.Throws<ArgumentNullException>(() => SkiaFontRegistrar.SetFallbackFamilies(null!));
        Assert.Throws<ArgumentException>(() => SkiaFontRegistrar.SetFallbackFamilies("A", " "));
        Assert.Equal(2, SkiaFontRegistrar.FallbackFamilies.Count);

        SkiaFontRegistrar.Clear();

        Assert.Empty(SkiaFontRegistrar.FallbackFamilies);
    }
}

using System.Globalization;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;
using KumikoUI.SkiaSharp;
using SkiaSharp;
using static KumikoUI.Core.Tests.SkiaTestFonts;

namespace KumikoUI.Core.Tests;

/// <summary>
/// Per-glyph font fallback (#74): text is split into runs of whole text elements, each drawn with a typeface that
/// has its glyphs (the requested one, then the registrar's fallback families, then the font manager's choice), and
/// the runs are cached with the text's measurement.
/// </summary>
/// <remarks>
/// The primary font is Open Sans, which has Latin glyphs but no emoji or CJK. Tests that need the platform's emoji
/// or CJK fonts skip, with the reason, where none is installed.
/// </remarks>
[Collection("SkiaFontSeam")]
public class SkiaFontFallbackTests : IDisposable
{
    private const string Family = "KumikoFallbackTestSans";
    private const string Mixed = "José 👍 東京";
    private const int ThumbsUp = 0x1F44D;
    private const int Tokyo = 0x6771;   // 東

    private readonly Func<string?, SKFontStyle, string[]?, int, SKTypeface?> _originalCharacterMatcher =
        SkiaDrawingContext.CharacterMatcher;
    private readonly Func<string, SKFontStyle, SKTypeface?> _originalFamilyMatcher = SkiaDrawingContext.FamilyMatcher;

    public SkiaFontFallbackTests()
    {
        SkiaFontRegistrar.Clear();
        SkiaFontRegistrar.RegisterTypefaceFromStream(Family, OpenSansStream());
    }

    public void Dispose()
    {
        SkiaDrawingContext.CharacterMatcher = _originalCharacterMatcher;
        SkiaDrawingContext.FamilyMatcher = _originalFamilyMatcher;
        SkiaFontRegistrar.Clear();
    }

    private static KumikoFont Font(float size = 16, bool bold = false) => new(Family, size, bold);

    private static KumikoPaint TextPaint(float size = 16) => new() { Color = KumikoColor.Black, Font = Font(size) };

    // ── Acceptance: every glyph renders ───────────────────────────

    [SystemFontFact(ThumbsUp, Tokyo)]
    public void Mixed_Latin_emoji_and_CJK_text_has_a_glyph_for_every_character()
    {
        using var cache = new SkiaResourceCache();
        var font = Font();

        float width = cache.MeasureTextWidth(Mixed, font, out _, out var runs);

        Assert.False(runs.IsPrimaryOnly);
        AssertEveryGlyphDrawn(cache, Mixed, runs, font);
        var openSans = cache.GetTypeface(Family, false, false);
        Assert.NotSame(openSans, TypefaceAt(cache, Mixed, runs, Mixed.IndexOf("👍", StringComparison.Ordinal)));
        Assert.NotSame(openSans, TypefaceAt(cache, Mixed, runs, Mixed.IndexOf('東')));
        Assert.Same(openSans, TypefaceAt(cache, Mixed, runs, Mixed.IndexOf('J')));
        Assert.Equal(SumOfRunWidths(runs), width, 3);
    }

    [SystemFontFact(ThumbsUp, Tokyo)]
    public void Mixed_text_draws_differently_from_its_missing_glyph_rendering()
    {
        var rect = new KumikoRect(0, 0, 220, 40);
        using var withFallback = Render(220, 40, ctx => ctx.DrawTextInRect(Mixed, rect, TextPaint()));
        using var tofu = new SKBitmap(220, 40, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(tofu))
        using (var cache = new SkiaResourceCache())
        {
            // What #74 fixes: the whole string drawn with the one typeface, its missing glyphs as boxes.
            canvas.Clear(SKColors.Transparent);
            var skFont = cache.GetFont(Font(), out var metrics);
            using var ink = new SKPaint { Color = SKColors.Black, IsAntialias = true };
            canvas.DrawText(Mixed, 0, (40 - (metrics.Descent - metrics.Ascent)) / 2 - metrics.Ascent,
                SKTextAlign.Left, skFont, ink);
        }

        Assert.True(Ink(withFallback) > 0);
        Assert.NotEqual(tofu.Pixels, withFallback.Pixels);
        // The color emoji font draws in color; missing-glyph boxes are drawn in the black text color.
        Assert.Contains(withFallback.Pixels, p => p.Alpha > 200 && p.Red > 150 && p.Red - p.Blue > 80);
    }

    [SystemFontFact(ThumbsUp, Tokyo)]
    public void Ellipsis_and_wrapping_measure_across_runs()
    {
        using var cache = new SkiaResourceCache();
        var font = Font();
        float full = cache.MeasureTextWidth(Mixed, font, out _);

        for (float width = 4; width < full; width += 3)
        {
            var lines = cache.GetTextLayout(Mixed, font, width, KumikoTextWrap.None, 1, out var lineRuns);
            string shown = lines.GetLine(0);
            Assert.EndsWith(KumikoTextLayout.Ellipsis, shown);
            Assert.True(lines.GetLineWidth(0) <= width || shown == KumikoTextLayout.Ellipsis, $"'{shown}' overflows {width}");
            AssertEveryGlyphDrawn(cache, shown, lineRuns[0], font);
            if (!lineRuns[0].IsPrimaryOnly)
                Assert.Equal(SumOfRunWidths(lineRuns[0]), lines.GetLineWidth(0), 3);
        }

        var wrapped = cache.GetTextLayout(Mixed, font, full * 0.6f, KumikoTextWrap.Word, 5, out var wrappedRuns);
        Assert.True(wrapped.Count >= 2);
        for (int i = 0; i < wrapped.Count; i++)
        {
            Assert.True(wrapped.GetLineWidth(i) <= full * 0.6f);
            AssertEveryGlyphDrawn(cache, wrapped.GetLine(i), wrappedRuns[i], font);
        }
    }

    // ── Segmentation ──────────────────────────────────────────────

    [Fact]
    public void Text_the_requested_typeface_covers_is_one_run_and_never_asks_the_font_manager()
    {
        int asked = 0;
        SkiaDrawingContext.CharacterMatcher = (_, _, _, _) => { asked++; return null; };
        using var cache = new SkiaResourceCache();
        var font = Font();

        cache.MeasureTextWidth("Hello world, José … 123", font, out _, out var runs);
        cache.GetTextLayout("A long value that is cut with an ellipsis", font, 60, KumikoTextWrap.None, 1, out var lineRuns);

        Assert.Same(FontRuns.Primary, runs);
        Assert.Same(FontRuns.Primary, lineRuns[0]);
        Assert.Equal(0, asked);
        Assert.Equal(0, cache.FallbackFaceCount);
    }

    [Fact]
    public void Without_any_fallback_font_missing_glyphs_stay_in_the_requested_typeface()
    {
        SkiaDrawingContext.CharacterMatcher = (_, _, _, _) => null;
        using var cache = new SkiaResourceCache();
        var font = Font();

        float width = cache.MeasureTextWidth("A東B", font, out _, out var runs);

        Assert.Same(FontRuns.Primary, runs);
        using var openSans = new SKFont(cache.GetTypeface(Family, false, false), 16);
        Assert.Equal(openSans.MeasureText("A東B"), width, 3);
    }

    [Fact]
    public void Runs_never_split_a_text_element()
    {
        // A ZWJ family, a flag, a skin tone, a combining accent, a keycap and CJK between Latin letters.
        string text = "a👨‍👩‍👧b🇯🇵c👍🏽déf1️⃣g東h";
        using var cache = new SkiaResourceCache();

        cache.MeasureTextWidth(text, Font(), out _, out var runs);

        var boundaries = new HashSet<int>(StringInfo.ParseCombiningCharacters(text)) { text.Length };
        for (int i = 0; i < runs.Count; i++)
            Assert.Contains(runs.GetEnd(i), boundaries);
        if (runs.Count > 0)
            Assert.Equal(text.Length, runs.GetEnd(runs.Count - 1));
    }

    [SystemFontFact(Tokyo)]
    public void Registrar_fallback_families_are_tried_in_order_before_the_font_manager()
    {
        var cjk = SKFontManager.Default.MatchCharacter(Tokyo)!;
        SkiaFontRegistrar.RegisterTypeface("KumikoTestCjk", cjk);
        SkiaDrawingContext.FamilyMatcher = (family, style) =>
            family == "KumikoNoSuchFamily" ? null : _originalFamilyMatcher(family, style);
        int asked = 0;
        SkiaDrawingContext.CharacterMatcher = (_, _, _, _) => { asked++; return null; };
        SkiaFontRegistrar.SetFallbackFamilies("KumikoNoSuchFamily", Family, "KumikoTestCjk");
        using var cache = new SkiaResourceCache();
        var font = Font();

        cache.MeasureTextWidth("Tokyo 東京", font, out _, out var runs);

        Assert.Equal(2, runs.Count);
        Assert.Equal(FontRuns.PrimaryFace, runs.GetFace(0));
        Assert.Same(cjk, cache.GetFallbackTypeface(runs.GetFace(1)));
        Assert.Equal(0, asked);
    }

    [SystemFontFact(ThumbsUp)]
    public void Emoji_sequences_stay_in_one_run()
    {
        using var cache = new SkiaResourceCache();
        string text = "ok 👍🏽 🇯🇵";

        cache.MeasureTextWidth(text, Font(), out _, out var runs);

        // "ok " in Open Sans, then one emoji run: the skin tone and both regional indicators stay with their emoji.
        int thumbs = text.IndexOf("👍", StringComparison.Ordinal);
        int afterFlag = text.Length;
        Assert.Equal(FontRuns.PrimaryFace, runs.GetFace(0));
        Assert.Equal(thumbs, runs.GetEnd(0));
        Assert.DoesNotContain(Enumerable.Range(0, runs.Count).Select(runs.GetEnd),
            end => end > thumbs && end < thumbs + "👍🏽".Length);
        Assert.Equal(afterFlag, runs.GetEnd(runs.Count - 1));
        AssertEveryGlyphDrawn(cache, text, runs, Font());
    }

    // ── Caching ───────────────────────────────────────────────────

    [Fact]
    public void Fallback_text_is_measured_once_and_cache_hits_allocate_nothing()
    {
        // Works with or without installed fallback fonts: either way the second pass is all cache hits.
        using var cache = new SkiaResourceCache();
        using var bitmap = new SKBitmap(200, 60);
        using var canvas = new SKCanvas(bitmap);
        using var ctx = new SkiaDrawingContext(canvas, cache);
        var paint = TextPaint();

        void Measure()
        {
            ctx.MeasureText(Mixed, paint);
            ctx.MeasureText(Mixed, paint, 60, KumikoTextWrap.Word);
            cache.MeasureTextWidth(Mixed, paint.Font, out _, out var runs);
            cache.GetTextLayout(Mixed, paint.Font, 70, KumikoTextWrap.None, 1, out var lineRuns);
            for (int i = 0; i < runs.Count; i++)
                cache.GetFallbackFont(runs.GetFace(i), runs.Generation, paint.Font);
            for (int i = 0; i < lineRuns[0].Count; i++)
                cache.GetFallbackFont(lineRuns[0].GetFace(i), lineRuns[0].Generation, paint.Font);
        }

        for (int i = 0; i < 20; i++) Measure();
        long measured = cache.NativeTextMeasureCount;
        long least = long.MaxValue;
        for (int attempt = 0; attempt < 3 && least > 0; attempt++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++) Measure();
            least = Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        Assert.Equal(0, least);
        Assert.Equal(measured, cache.NativeTextMeasureCount);
    }

    [SystemFontFact(ThumbsUp, Tokyo)]
    public void Second_frame_of_fallback_text_measures_nothing()
    {
        using var cache = new SkiaResourceCache();
        using var bitmap = new SKBitmap(240, 120);
        using var canvas = new SKCanvas(bitmap);
        var paint = TextPaint();

        void Frame()
        {
            using var ctx = new SkiaDrawingContext(canvas, cache);
            ctx.DrawText(Mixed, 0, 20, paint);
            ctx.DrawTextInRect(Mixed, new KumikoRect(0, 30, 70, 30), paint);
            ctx.DrawTextInRect(Mixed, new KumikoRect(0, 60, 90, 60), paint, KumikoTextAlignment.Left,
                KumikoVerticalAlignment.Top, clip: true, KumikoTextWrap.Word);
        }

        Frame();
        long afterFirst = cache.NativeTextMeasureCount;
        int faces = cache.FallbackFaceCount;
        Frame();

        Assert.True(faces > 0);
        Assert.Equal(afterFirst, cache.NativeTextMeasureCount);
        Assert.Equal(faces, cache.FallbackFaceCount);
    }

    [SystemFontFact(ThumbsUp)]
    public void A_fallback_list_change_drops_fallback_choices_and_runs_from_earlier_choices_draw_with_the_primary_font()
    {
        using var cache = new SkiaResourceCache();
        var font = Font();
        cache.MeasureTextWidth("👍", font, out _, out var runs);
        Assert.Equal(1, cache.FallbackFaceCount);
        var emoji = cache.GetFallbackTypeface(runs.GetFace(0));
        int leases = SystemTypefacePool.LeaseCount(emoji);   // other live caches may hold it too
        long version = SkiaFontRegistrar.Version;

        SkiaFontRegistrar.SetFallbackFamilies("KumikoNoSuchFamily");

        Assert.True(SkiaFontRegistrar.Version > version);
        Assert.Equal(0, cache.FallbackFaceCount);
        Assert.Equal(0, cache.TextMeasurementCount);
        Assert.Same(cache.GetFont(font), cache.GetFallbackFont(runs.GetFace(0), runs.Generation, font));
        Assert.Equal(Math.Max(0, leases - 1), SystemTypefacePool.LeaseCount(emoji));
    }

    [SystemFontFact(ThumbsUp)]
    public void Disposing_the_cache_gives_back_fallback_typefaces()
    {
        using var cache = new SkiaResourceCache();
        var released = new List<object>();
        cache.ResourceReleased = released.Add;
        cache.MeasureTextWidth("👍", Font(), out _, out var runs);
        var emoji = cache.GetFallbackTypeface(runs.GetFace(0));
        var fallbackFont = cache.GetFallbackFont(runs.GetFace(0), runs.Generation, Font());
        int leases = SystemTypefacePool.LeaseCount(emoji);

        cache.Dispose();

        Assert.Contains(fallbackFont, released);
        Assert.True(IsDisposed(fallbackFont));
        Assert.Equal(Math.Max(0, leases - 1), SystemTypefacePool.LeaseCount(emoji));
    }

    // ── Helpers ───────────────────────────────────────────────────

    private static SKFont RunFont(SkiaResourceCache cache, FontRuns runs, int run, KumikoFont font) =>
        runs.IsPrimaryOnly || runs.GetFace(run) == FontRuns.PrimaryFace
            ? cache.GetFont(font)
            : cache.GetFallbackFont(runs.GetFace(run), runs.Generation, font);

    private static SKTypeface TypefaceAt(SkiaResourceCache cache, string text, FontRuns runs, int index)
    {
        if (runs.IsPrimaryOnly) return cache.GetFont(Font()).Typeface!;
        int run = 0;
        while (runs.GetEnd(run) <= index) run++;
        return RunFont(cache, runs, run, Font()).Typeface!;
    }

    private static float SumOfRunWidths(FontRuns runs)
    {
        float sum = 0;
        for (int i = 0; i < runs.Count; i++) sum += runs.GetWidth(i);
        return sum;
    }

    /// <summary>Every character of <paramref name="text"/> maps to a real glyph (not .notdef) in its run's font.</summary>
    private static void AssertEveryGlyphDrawn(SkiaResourceCache cache, string text, FontRuns runs, KumikoFont font)
    {
        int count = runs.IsPrimaryOnly ? 1 : runs.Count;
        int start = 0;
        for (int run = 0; run < count; run++)
        {
            int end = runs.IsPrimaryOnly ? text.Length : runs.GetEnd(run);
            var skFont = RunFont(cache, runs, run, font);
            foreach (var rune in text.AsSpan(start, end - start).EnumerateRunes())
            {
                // Joiners and variation selectors select glyphs rather than being drawn.
                if (rune.Value is 0x200D or 0xFE0F) continue;
                Assert.True(skFont.GetGlyph(rune.Value) != 0,
                    $"U+{rune.Value:X4} in run {run} has no glyph in {skFont.Typeface?.FamilyName}");
            }
            start = end;
        }
    }
}

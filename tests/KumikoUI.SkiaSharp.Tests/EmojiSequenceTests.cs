using KumikoUI.Core;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.SkiaSharp;
using SkiaSharp;

namespace KumikoUI.SkiaSharp.Tests;

/// <summary>
/// Emoji sequences (a skin-tone modifier, a ZWJ family, a flag, a variation selector) are drawn as one glyph (#259).
/// Skia maps every code point to a glyph of its own, so a run is shaped with HarfBuzz when it holds such a sequence.
/// </summary>
/// <remarks>
/// The tests use a generated font (<c>Fonts/generate-test-emoji-font.py</c>) with a ligature glyph for each sequence,
/// so they need no emoji font installed and give the same result on every platform. The last test uses the system's
/// own emoji font and does nothing (it says so in its output) on a machine that has none, such as a bare Linux container.
/// </remarks>
[Collection(SkiaRegistrarCollection.Name)]
public class EmojiSequenceTests : IDisposable
{
    public const string Thumb = "\U0001F44D";
    public const string ThumbMediumTone = "\U0001F44D\U0001F3FD";
    public const string Family = "\U0001F468‍\U0001F469‍\U0001F467";
    public const string RainbowFlag = "\U0001F3F3️‍\U0001F308";
    public const string FlagDe = "\U0001F1E9\U0001F1EA";

    public static TheoryData<string> Sequences => new() { ThumbMediumTone, Family, RainbowFlag, FlagDe };

    private const float Size = 20f;
    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public EmojiSequenceTests(Xunit.Abstractions.ITestOutputHelper output)
    {
        _output = output;
        TestFonts.Reset(withCjkFallback: true, withEmojiFallback: true);
    }

    public void Dispose()
    {
        GlyphShaper.SetAvailable(true);
        SkiaFontRegistrar.Clear();
    }

    private static KumikoFont Latin => new(TestFonts.Latin, Size);

    [Theory]
    [MemberData(nameof(Sequences))]
    public void A_sequence_is_one_run_in_one_typeface_measured_as_one_glyph(string sequence)
    {
        using var cache = new SkiaResourceCache();

        float width = cache.MeasureTextWidth(sequence, Latin, out _, out var runs);

        Assert.Equal(1, runs.Count);
        Assert.Equal(sequence.Length, runs.GetEnd(0));
        Assert.True(runs.IsShaped(0), "the run holds an emoji sequence");
        Assert.NotEqual(FontRuns.PrimaryFace, runs.GetFace(0));
        Assert.Equal(TestFonts.Emoji, cache.GetFallbackTypeface(runs.GetFace(0)).FamilyName);
        // Every ligature glyph of the test font is one em wide; the unshaped sequence is 1.5 to 3 em wide.
        Assert.Equal(Size, width, 0.01);
        Assert.Equal(width, runs.GetWidth(0), 0.01);
    }

    [Fact]
    public void A_single_emoji_and_plain_text_are_not_shaped()
    {
        using var cache = new SkiaResourceCache();

        cache.MeasureTextWidth(Thumb, Latin, out _, out var thumb);
        cache.MeasureTextWidth("Plain text", Latin, out _, out var plain);
        cache.MeasureTextWidth("Café é", Latin, out _, out var combining);

        Assert.False(thumb.IsShaped(0));
        Assert.True(plain.IsPrimaryOnly);
        Assert.True(combining.IsPrimaryOnly || !combining.IsShaped(0));
    }

    [Fact]
    public void A_sequence_between_other_text_keeps_the_text_around_it_in_the_primary_typeface()
    {
        using var cache = new SkiaResourceCache();
        string text = "a" + ThumbMediumTone + "b" + Family + "東";

        float width = cache.MeasureTextWidth(text, Latin, out _, out var runs);

        // a | thumb-tone | b | family | CJK
        Assert.Equal(5, runs.Count);
        Assert.Equal(new[] { false, true, false, true, false }, Enumerable.Range(0, 5).Select(runs.IsShaped).ToArray());
        float sum = cache.MeasureTextWidth("a", Latin, out _) + Size + cache.MeasureTextWidth("b", Latin, out _) + Size
                    + cache.MeasureTextWidth("東", Latin, out _);
        Assert.Equal(sum, width, 0.01);
        Assert.Equal(width, Enumerable.Range(0, 5).Sum(runs.GetWidth), 0.01);
    }

    [Theory]
    [MemberData(nameof(Sequences))]
    public void A_sequence_is_drawn_as_one_glyph(string sequence)
    {
        var (left, right) = InkExtent(sequence);

        // The ligature glyph spans 0.1 to 0.9 em; drawn glyph by glyph the sequence is wider than one em.
        _output.WriteLine($"ink from {left} to {right}");
        Assert.InRange(left, Origin, Origin + 0.2f * Size);
        Assert.InRange(right, Origin + 0.7f * Size, Origin + Size);
    }

    [Theory]
    [MemberData(nameof(Sequences))]
    public void Without_HarfBuzz_a_sequence_is_drawn_glyph_by_glyph_as_before(string sequence)
    {
        GlyphShaper.SetAvailable(false);

        var (_, right) = InkExtent(sequence);

        _output.WriteLine($"ink to {right}");
        Assert.True(right >= Origin + 1.3f * Size, "each code point has a glyph of its own");
    }

    [Fact]
    public void A_cut_text_never_splits_a_sequence_and_ends_with_the_ellipsis()
    {
        using var cache = new SkiaResourceCache();
        using var surface = SKSurface.Create(new SKImageInfo(200, 40));
        using var ctx = cache.CreateContext(surface.Canvas);
        var paint = new KumikoPaint { Color = new KumikoColor(0, 0, 0), Font = Latin };
        string text = "ab" + Family + RainbowFlag;

        // Room for "ab", one sequence and the ellipsis, not for the second sequence.
        float room = cache.MeasureTextWidth("ab", Latin, out _) + Size + cache.MeasureTextWidth("…", Latin, out _) + 2;
        var lines = cache.GetTextLayout(text, Latin, room, KumikoTextWrap.None, 1, out var lineRuns);

        Assert.Equal("ab" + Family + "…", lines.GetLine(0));
        Assert.True(lineRuns[0].IsShaped(1));
        ctx.DrawTextInRect(text, new KumikoRect(0, 0, room, 30), paint);
    }

    [Fact]
    public void Drawing_a_sequence_again_allocates_nothing()
    {
        using var cache = new SkiaResourceCache();
        using var surface = SKSurface.Create(new SKImageInfo(300, 60));
        var paint = new KumikoPaint { Color = new KumikoColor(0, 0, 0), Font = Latin };
        string text = "a" + ThumbMediumTone + Family + RainbowFlag + FlagDe + "東";

        void Frame()
        {
            using var ctx = cache.CreateContext(surface.Canvas);
            ctx.DrawText(text, 4, 40, paint);
            ctx.DrawTextInRect(text, new KumikoRect(0, 0, 300, 30), paint);
        }

        for (int i = 0; i < 5; i++) Frame();
        long fewest = long.MaxValue;
        for (int run = 0; run < 5 && fewest > 0; run++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 20; i++) Frame();
            fewest = Math.Min(fewest, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        Assert.Equal(0, fewest);
    }

    [Fact]
    public void The_system_emoji_font_draws_each_sequence_as_one_glyph()
    {
        TestFonts.Reset();   // no registered fallback: the font manager supplies the emoji font
        using var probe = SKFontManager.Default.MatchCharacter(null, SKFontStyle.Normal, null, 0x1F44D);
        if (probe is null || !probe.FamilyName.Contains("Emoji", StringComparison.OrdinalIgnoreCase))
        {
            _output.WriteLine($"Skipped: no system emoji font (the font manager returned '{probe?.FamilyName}').");
            return;
        }

        using var cache = new SkiaResourceCache();
        foreach (var sequence in new[] { ThumbMediumTone, Family, RainbowFlag, FlagDe })
        {
            float width = cache.MeasureTextWidth(sequence, Latin, out _, out var runs);
            _output.WriteLine($"{probe.FamilyName}: {sequence.Length} chars, one run: {runs.Count == 1}, width {width}");

            Assert.Equal(1, runs.Count);
            Assert.True(runs.IsShaped(0));
            // One color glyph is about one em wide; unshaped, a ZWJ family would be three of them.
            Assert.InRange(width, 0.5f * Size, 1.6f * Size);
        }
    }

    private const float Origin = 10f;

    /// <summary>The leftmost and rightmost columns with ink when <paramref name="text"/> is drawn at <see cref="Origin"/>.</summary>
    private static (int Left, int Right) InkExtent(string text)
    {
        using var cache = new SkiaResourceCache();
        using var bitmap = new SKBitmap(120, 50, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        using (var ctx = cache.CreateContext(canvas))
            ctx.DrawText(text, Origin, 35, new KumikoPaint { Color = new KumikoColor(0, 0, 0), Font = Latin });

        int left = int.MaxValue, right = -1;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).Red > 200) continue;
                left = Math.Min(left, x);
                right = Math.Max(right, x);
            }
        }
        Assert.True(right >= 0, "the text drew something");
        return (left, right);
    }
}

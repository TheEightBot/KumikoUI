using KumikoUI.Core;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.SkiaSharp;
using SkiaSharp;

namespace KumikoUI.SkiaSharp.Tests;

/// <summary>
/// A grid whose cells hold emoji sequences, rendered with the SkiaSharp drawing context and compared with a committed
/// image (see <see cref="GoldenImage"/>): every sequence is one glyph of the generated emoji test font (a block, a
/// pentagon, three bars, a striped flag), where drawn glyph by glyph it would be several shapes side by side.
/// </summary>
[Collection(SkiaRegistrarCollection.Name)]
public class EmojiGoldenTests : IDisposable
{
    private const int Width = 420;
    private const int Height = 200;

    public sealed class Row
    {
        public string Name { get; set; } = "";
        public string Reaction { get; set; } = "";
        public string Note { get; set; } = "";
    }

    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public EmojiGoldenTests(Xunit.Abstractions.ITestOutputHelper output)
    {
        _output = output;
        TestFonts.Reset(withCjkFallback: true, withEmojiFallback: true);
    }

    public void Dispose()
    {
        GlyphShaper.SetAvailable(true);
        SkiaFontRegistrar.Clear();
    }

    private static SKBitmap RenderGrid()
    {
        var source = new KumikoSource();
        source.SetColumns(new[]
        {
            new KumikoColumn { Header = "Name", PropertyName = nameof(Row.Name), Width = 100 },
            new KumikoColumn { Header = "Reaction", PropertyName = nameof(Row.Reaction), Width = 100 },
            new KumikoColumn { Header = "Note", PropertyName = nameof(Row.Note), Width = 200 },
        });
        source.SetItems(new List<Row>
        {
            new() { Name = "Ada", Reaction = EmojiSequenceTests.ThumbMediumTone, Note = "Nice " + EmojiSequenceTests.Thumb + EmojiSequenceTests.ThumbMediumTone },
            new() { Name = "Grace", Reaction = EmojiSequenceTests.Family, Note = "Family " + EmojiSequenceTests.Family + " Tokyo 東京" },
            new() { Name = "Linus", Reaction = EmojiSequenceTests.RainbowFlag, Note = "Pride " + EmojiSequenceTests.RainbowFlag },
            new() { Name = "Hedy", Reaction = EmojiSequenceTests.FlagDe, Note = "Flags " + EmojiSequenceTests.FlagDe + EmojiSequenceTests.RainbowFlag },
        });

        var style = new KumikoStyle
        {
            HeaderFont = new KumikoFont(TestFonts.Latin, 14, bold: true),
            CellFont = new KumikoFont(TestFonts.Latin, 16),
        };
        var scroll = new ScrollState { ViewportWidth = Width, ViewportHeight = Height };
        var selection = new KumikoSelectionModel(source);
        using var renderer = new KumikoRenderer();

        var bitmap = new SKBitmap(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        using var cache = new SkiaResourceCache();
        using (var ctx = cache.CreateContext(canvas))
            renderer.Render(ctx, source, scroll, selection, style);
        return bitmap;
    }

    [Fact]
    public void A_grid_of_emoji_sequences_matches_the_golden_image()
    {
        using var bitmap = RenderGrid();

        double differing = GoldenImage.AssertMatches("grid-emoji", bitmap);

        _output.WriteLine($"{differing:P3} of the blocks differ by more than {GoldenImage.ChannelTolerance} levels " +
                          $"(allowed {GoldenImage.MaxDifferingFraction:P1}).");
    }

    [Fact]
    public void The_golden_comparison_tells_shaped_sequences_from_glyph_by_glyph_ones()
    {
        using var shaped = RenderGrid();
        GlyphShaper.SetAvailable(false);
        using var unshaped = RenderGrid();

        int different = GoldenImage.CountDifferingBlocks(shaped, unshaped, out var diff);
        double fraction = (double)different / ((double)diff.Width * diff.Height);
        diff.Dispose();

        _output.WriteLine($"{fraction:P3} of the blocks differ");
        Assert.True(fraction > GoldenImage.MaxDifferingFraction, $"unshaped sequences were not detected ({fraction:P3})");
    }
}

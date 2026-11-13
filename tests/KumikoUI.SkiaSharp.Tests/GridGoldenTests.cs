using KumikoUI.Core;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.SkiaSharp;
using SkiaSharp;

namespace KumikoUI.SkiaSharp.Tests;

/// <summary>
/// Renders a small grid with the Core renderer and the SkiaSharp drawing context to a raster bitmap and compares it
/// with a committed image (see <see cref="GoldenImage"/> for the tolerance and how to regenerate it).
/// </summary>
[Collection(SkiaRegistrarCollection.Name)]
public class GridGoldenTests : IDisposable
{
    private const int Width = 420;
    private const int Height = 200;

    public sealed class Person
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
        public string City { get; set; } = "";
        public decimal Salary { get; set; }
    }

    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public GridGoldenTests(Xunit.Abstractions.ITestOutputHelper output)
    {
        _output = output;
        TestFonts.Reset(withCjkFallback: true);
    }

    public void Dispose() => SkiaFontRegistrar.Clear();

    private static SKBitmap RenderGrid(string family)
    {
        var source = new KumikoSource();
        source.SetColumns(new[]
        {
            new KumikoColumn { Header = "Name", PropertyName = nameof(Person.Name), Width = 120 },
            new KumikoColumn { Header = "Age", PropertyName = nameof(Person.Age), Width = 60, ColumnType = KumikoColumnType.Numeric },
            new KumikoColumn { Header = "City", PropertyName = nameof(Person.City), Width = 120 },
            new KumikoColumn { Header = "Salary", PropertyName = nameof(Person.Salary), Width = 120, ColumnType = KumikoColumnType.Numeric },
        });
        source.SetItems(new List<Person>
        {
            new() { Name = "Ada Lovelace", Age = 36, City = "London", Salary = 5200 },
            new() { Name = "José Müller", Age = 41, City = "東京 Tokyo", Salary = 4875.5m },
            new() { Name = "A very long name that is cut", Age = 29, City = "Oslo", Salary = 3100 },
            new() { Name = "Grace Hopper", Age = 85, City = "Arlington", Salary = 9900 },
        });

        var style = new KumikoStyle
        {
            HeaderFont = new KumikoFont(family, 14, bold: true),
            CellFont = new KumikoFont(family, 13),
        };
        var scroll = new ScrollState { ViewportWidth = Width, ViewportHeight = Height };
        var selection = new KumikoSelectionModel(source);
        using var renderer = new KumikoRenderer();

        var bitmap = new SKBitmap(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        using var cache = new SkiaResourceCache();
        using (var ctx = new SkiaDrawingContext(canvas, cache))
            renderer.Render(ctx, source, scroll, selection, style);
        return bitmap;
    }

    [Fact]
    public void Grid_with_a_registered_font_matches_the_golden_image()
    {
        using var bitmap = RenderGrid(TestFonts.Latin);

        double differing = GoldenImage.AssertMatches("grid-latin-cjk", bitmap);

        _output.WriteLine($"{differing:P3} of the blocks differ by more than {GoldenImage.ChannelTolerance} levels " +
                          $"(allowed {GoldenImage.MaxDifferingFraction:P1}).");
    }

    [Fact]
    public void Rendering_the_same_grid_twice_is_pixel_identical()
    {
        using var first = RenderGrid(TestFonts.Latin);
        using var second = RenderGrid(TestFonts.Latin);

        Assert.Equal(first.Pixels, second.Pixels);
    }

    [Fact]
    public void Golden_comparison_detects_blanked_text_and_a_recolored_row()
    {
        using var original = RenderGrid(TestFonts.Latin);

        foreach (var (rect, color) in new[]
        {
            (new SKRect(8, 52, 118, 104), SKColors.White),      // the text of two cells removed
            (new SKRect(0, 90, Width, 126), SKColors.LightBlue), // a row painted with another color
        })
        {
            using var changed = RenderGrid(TestFonts.Latin);
            using (var canvas = new SKCanvas(changed))
            using (var paint = new SKPaint { Color = color })
                canvas.DrawRect(rect, paint);

            int different = GoldenImage.CountDifferingBlocks(original, changed, out var diff);
            double fraction = (double)different / (diff.Width * diff.Height);
            diff.Dispose();

            _output.WriteLine($"{rect}: {fraction:P3} of the blocks differ");
            Assert.True(fraction > GoldenImage.MaxDifferingFraction, $"{rect} was not detected ({fraction:P3})");
        }
    }
}

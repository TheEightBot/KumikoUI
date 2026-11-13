using SkiaSharp;

namespace KumikoUI.SkiaSharp.Tests;

/// <summary>
/// Compares a rendered bitmap with a committed PNG ("golden") and tells real differences apart from the platform's
/// anti-aliasing noise.
/// </summary>
/// <remarks>
/// <para>
/// Skia rasterizes glyphs with a different scaler on each operating system (CoreText on macOS, DirectWrite on
/// Windows, FreeType on Linux), so the edge pixels of text differ by tens of levels between platforms even with the
/// same font file, and a byte-exact comparison cannot pass on all three. The comparison therefore works on
/// <see cref="BlockSize"/> x <see cref="BlockSize"/> pixel blocks: each block is averaged (which cancels most
/// anti-aliasing and hinting noise inside a glyph), and a block is different when any channel of its average differs
/// by more than <see cref="ChannelTolerance"/> levels (of 255). The image fails when more than
/// <see cref="MaxDifferingFraction"/> of its blocks are different. A missing, moved or recolored element changes whole
/// blocks; the test class checks that blanked text and a recolored row are caught. Measured: the macOS golden compared with
/// a Linux render differs in 0.3% of the blocks (the grid test writes the figure to its output), against the 1%
/// limit; one blanked cell is about 1.2% to 2% and two cells more. Windows renders with DirectWrite and has not been
/// measured; if it needs more room, raise <see cref="MaxDifferingFraction"/> rather than the per-block tolerance.
/// </para>
/// <para>
/// Set the environment variable <c>KUMIKO_UPDATE_GOLDENS=1</c> to write the rendered images over the committed ones
/// (into the source tree when the tests run from the repository). On a mismatch the actual image and a diff image
/// (one pixel per block, differing blocks in red over a faded copy of the golden, scaled up) are written to
/// <c>GoldenFailures/</c> in the test output folder.
/// </para>
/// </remarks>
internal static class GoldenImage
{
    /// <summary>The environment variable that turns a golden comparison into a golden update.</summary>
    public const string UpdateVariable = "KUMIKO_UPDATE_GOLDENS";

    /// <summary>Edge length in pixels of the blocks that are averaged before they are compared.</summary>
    public const int BlockSize = 12;

    /// <summary>Largest per-channel difference (0 to 255) of two block averages still treated as equal.</summary>
    public const int ChannelTolerance = 16;

    /// <summary>Largest share of blocks allowed to differ by more than <see cref="ChannelTolerance"/>.</summary>
    public const double MaxDifferingFraction = 0.01;

    /// <summary>Compares <paramref name="actual"/> with the golden <paramref name="name"/>.png and returns the share of differing blocks.</summary>
    public static double AssertMatches(string name, SKBitmap actual)
    {
        string outputPath = Path.Combine(AppContext.BaseDirectory, "Goldens", name + ".png");
        if (Environment.GetEnvironmentVariable(UpdateVariable) is { Length: > 0 } flag && flag != "0")
        {
            string target = SourcePath(name) ?? outputPath;
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            Save(actual, target);
            return 0;
        }

        Assert.True(File.Exists(outputPath),
            $"The golden image {name}.png is missing. Run the tests with {UpdateVariable}=1 to create it.");
        using var golden = SKBitmap.Decode(outputPath);
        Assert.NotNull(golden);
        Assert.Equal((golden.Width, golden.Height), (actual.Width, actual.Height));

        int different = CountDifferingBlocks(golden, actual, out SKBitmap diff);
        using (diff)
        {
            double fraction = (double)different / (diff.Width * diff.Height);
            if (fraction <= MaxDifferingFraction) return fraction;

            string failures = Path.Combine(AppContext.BaseDirectory, "GoldenFailures");
            Directory.CreateDirectory(failures);
            Save(actual, Path.Combine(failures, name + ".actual.png"));
            using (var scaled = diff.Resize(new SKImageInfo(diff.Width * BlockSize, diff.Height * BlockSize),
                       new SKSamplingOptions(SKFilterMode.Nearest)))
                Save(scaled, Path.Combine(failures, name + ".diff.png"));
            Assert.Fail($"{name}: {different} blocks ({fraction:P2}) differ by more than {ChannelTolerance} levels, " +
                        $"above the allowed {MaxDifferingFraction:P2}. See {failures}.");
            return fraction;
        }
    }

    /// <summary>
    /// Counts the <see cref="BlockSize"/> x <see cref="BlockSize"/> pixel blocks whose average color differs by more
    /// than <see cref="ChannelTolerance"/> in any channel, and builds the diff image (one pixel per block).
    /// </summary>
    public static int CountDifferingBlocks(SKBitmap golden, SKBitmap actual, out SKBitmap diff)
    {
        int blocksX = (actual.Width + BlockSize - 1) / BlockSize;
        int blocksY = (actual.Height + BlockSize - 1) / BlockSize;
        diff = new SKBitmap(blocksX, blocksY, SKColorType.Rgba8888, SKAlphaType.Premul);
        int different = 0;
        for (int by = 0; by < blocksY; by++)
        {
            for (int bx = 0; bx < blocksX; bx++)
            {
                var g = Average(golden, bx, by);
                var a = Average(actual, bx, by);
                int delta = 0;
                for (int c = 0; c < 4; c++)
                    delta = Math.Max(delta, Math.Abs(g[c] - a[c]));
                if (delta > ChannelTolerance)
                {
                    different++;
                    diff.SetPixel(bx, by, SKColors.Red);
                }
                else
                {
                    diff.SetPixel(bx, by, new SKColor((byte)(128 + g[0] / 2), (byte)(128 + g[1] / 2), (byte)(128 + g[2] / 2)));
                }
            }
        }
        return different;
    }

    // The average RGBA of one block.
    private static int[] Average(SKBitmap bitmap, int bx, int by)
    {
        int[] sum = new int[4];
        int count = 0;
        for (int y = by * BlockSize; y < Math.Min((by + 1) * BlockSize, bitmap.Height); y++)
        {
            for (int x = bx * BlockSize; x < Math.Min((bx + 1) * BlockSize, bitmap.Width); x++)
            {
                SKColor p = bitmap.GetPixel(x, y);
                sum[0] += p.Red; sum[1] += p.Green; sum[2] += p.Blue; sum[3] += p.Alpha;
                count++;
            }
        }
        for (int c = 0; c < 4; c++) sum[c] /= count;
        return sum;
    }

    private static void Save(SKBitmap bitmap, string path)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var file = File.Create(path);
        data.SaveTo(file);
    }

    // The Goldens folder of the test project, found by walking up from the output folder.
    private static string? SourcePath(string name)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "KumikoUI.SkiaSharp.Tests.csproj")))
                return Path.Combine(dir.FullName, "Goldens", name + ".png");
        }
        return null;
    }
}

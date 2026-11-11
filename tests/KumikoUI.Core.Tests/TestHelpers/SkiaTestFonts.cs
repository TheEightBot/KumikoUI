using KumikoUI.Core.Rendering;
using KumikoUI.SkiaSharp;
using SkiaSharp;

namespace KumikoUI.Core.Tests;

/// <summary>A real font (Open Sans, embedded from the MAUI sample) and drawing helpers for the Skia tests.</summary>
internal static class SkiaTestFonts
{
    private static readonly Lazy<byte[]> _openSans = new(() => ReadResource("KumikoUI.Core.Tests.OpenSans-Regular.ttf"));
    private static readonly Lazy<byte[]> _openSansSemibold = new(() => ReadResource("KumikoUI.Core.Tests.OpenSans-Semibold.ttf"));

    private static byte[] ReadResource(string name)
    {
        using var resource = typeof(SkiaTestFonts).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"The embedded test font {name} is missing.");
        using var copy = new MemoryStream();
        resource.CopyTo(copy);
        return copy.ToArray();
    }

    /// <summary>A fresh stream over the font file.</summary>
    public static Stream OpenSansStream() => new MemoryStream(_openSans.Value, writable: false);

    /// <summary>A fresh stream over the semibold (weight 600) face of the same family.</summary>
    public static Stream OpenSansSemiboldStream() => new MemoryStream(_openSansSemibold.Value, writable: false);

    /// <summary>The total alpha of every pixel: how much ink was drawn, a measure of how heavy text is.</summary>
    public static long Ink(SKBitmap bitmap)
    {
        long total = 0;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
                total += bitmap.GetPixel(x, y).Alpha;
        return total;
    }

    /// <summary>Draws with a fresh cache into a new transparent bitmap the caller disposes.</summary>
    public static SKBitmap Render(int width, int height, Action<SkiaDrawingContext> draw)
    {
        var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        using var cache = new SkiaResourceCache();
        using (var ctx = new SkiaDrawingContext(canvas, cache))
            draw(ctx);
        return bitmap;
    }

    /// <summary>A new typeface the caller owns.</summary>
    public static SKTypeface CreateOpenSans()
    {
        using var stream = OpenSansStream();
        return SKTypeface.FromStream(stream) ?? throw new InvalidOperationException("Could not load the test font.");
    }

    public static bool IsDisposed(SKObject obj) => obj.Handle == IntPtr.Zero;

    public static KumikoPaint Paint(string family = "Default", float size = 13, bool bold = false, bool italic = false,
        KumikoColor? color = null) =>
        new() { Color = color ?? KumikoColor.Black, Font = new KumikoFont(family, size, bold, italic) };

    /// <summary>Draws and measures one frame with <paramref name="paint"/> and returns the measured width.</summary>
    public static float DrawFrame(SKCanvas canvas, SkiaResourceCache cache, KumikoPaint paint)
    {
        using var ctx = new SkiaDrawingContext(canvas, cache);
        ctx.FillRect(new KumikoRect(0, 0, 10, 10), paint);
        ctx.DrawText("Hello", 4, 20, paint);
        ctx.DrawTextInRect("Hello world", new KumikoRect(0, 0, 60, 30), paint);
        return ctx.MeasureText("Hello", paint).Width;
    }
}

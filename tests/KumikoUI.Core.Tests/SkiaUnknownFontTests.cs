using KumikoUI.Core.Rendering;
using KumikoUI.SkiaSharp;
using SkiaSharp;

namespace KumikoUI.Core.Tests;

// The family matcher and the warning set are process-wide, so these tests must not run in parallel.
[Collection("SkiaFontSeam")]
public class SkiaUnknownFontTests : IDisposable
{
    private const string Unknown = "NoSuchFont-Kumiko";
    private readonly Func<string, SKFontStyle, SKTypeface?> _originalMatcher = SkiaDrawingContext.FamilyMatcher;

    public SkiaUnknownFontTests() => SkiaDrawingContext.ResetUnknownFamilyReports();

    public void Dispose() => SkiaDrawingContext.FamilyMatcher = _originalMatcher;

    private static KumikoPaint Paint(string family, bool bold = false) =>
        new() { Font = new KumikoFont(family, 13, bold) };

    private static void DrawFrame(SKCanvas canvas, string family, bool bold = false)
    {
        using var ctx = new SkiaDrawingContext(canvas);
        var paint = Paint(family, bold);
        ctx.DrawText("Hello", 4, 20, paint);
        Assert.True(ctx.MeasureText("Hello", paint).Width > 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Unknown_family_on_this_platform_draws_and_measures(bool bold)
    {
        using var bitmap = new SKBitmap(100, 40);
        using var canvas = new SKCanvas(bitmap);
        DrawFrame(canvas, Unknown, bold);
        DrawFrame(canvas, Unknown, bold); // second frame
    }

    [Fact]
    public void Matcher_returning_null_falls_back_to_default_and_survives_frames_and_double_dispose()
    {
        SkiaDrawingContext.FamilyMatcher = static (_, _) => null; // Android / Windows behavior
        using var bitmap = new SKBitmap(100, 40);
        using var canvas = new SKCanvas(bitmap);

        var ctx = new SkiaDrawingContext(canvas);
        try
        {
            var paint = Paint(Unknown);
            ctx.DrawText("Hello", 4, 20, paint);
            Assert.True(ctx.MeasureText("Hello", paint).Width > 0);
        }
        finally
        {
            ctx.Dispose();
            ctx.Dispose(); // disposing twice must be safe
        }

        // The shared default typeface must still be alive for later frames.
        Assert.True(SKTypeface.Default.GlyphCount > 0);
        DrawFrame(canvas, Unknown);
        DrawFrame(canvas, Unknown, bold: true);
    }

    [Fact]
    public void Unknown_family_is_reported_once()
    {
        SkiaDrawingContext.FamilyMatcher = static (_, _) => null;
        var listener = new CapturingListener();
        System.Diagnostics.Trace.Listeners.Add(listener);
        try
        {
            using var bitmap = new SKBitmap(100, 40);
            using var canvas = new SKCanvas(bitmap);
            DrawFrame(canvas, Unknown);
            DrawFrame(canvas, Unknown);
            DrawFrame(canvas, Unknown, bold: true);
        }
        finally
        {
            System.Diagnostics.Trace.Listeners.Remove(listener);
        }
        Assert.Single(listener.Messages, m => m.Contains(Unknown));
    }

    private sealed class CapturingListener : System.Diagnostics.TraceListener
    {
        public List<string> Messages { get; } = new();
        public override void Write(string? message) { if (message != null) Messages.Add(message); }
        public override void WriteLine(string? message) { if (message != null) Messages.Add(message); }
    }
}

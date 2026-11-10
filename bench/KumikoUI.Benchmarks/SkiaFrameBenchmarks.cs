using BenchmarkDotNet.Attributes;
using KumikoUI.Core.Rendering;
using KumikoUI.SkiaSharp;
using SkiaSharp;

namespace KumikoUI.Benchmarks;

/// <summary>
/// Draws one grid-like frame (a bold header row, 30 rows x 8 columns of alternating backgrounds, cell
/// text, grid lines and a selection highlight) through <see cref="SkiaDrawingContext"/>, the way the
/// hosts do on every PaintSurface. "PerFrameObjects" uses the original constructor, which creates and
/// disposes every SKPaint, SKFont and SKTypeface each frame (the behavior before #62); "SharedCache"
/// passes a long-lived <see cref="SkiaResourceCache"/>, as the MAUI and Uno hosts now do.
/// <para><see cref="WithText"/> = <c>false</c> draws the same frame without the text calls (fonts are
/// still resolved, through <see cref="SkiaDrawingContext.GetFontMetrics"/>), which isolates the cache:
/// SkiaSharp's <c>SKCanvas.DrawText</c> allocates a text blob per call whichever constructor is used.</para>
/// </summary>
[MemoryDiagnoser]
public class SkiaFrameBenchmarks
{
    private const int Rows = 30;
    private const int Columns = 8;
    private const float RowHeight = 24;
    private const float ColumnWidth = 96;

    private SKBitmap _bitmap = null!;
    private SKCanvas _canvas = null!;
    private SkiaResourceCache _cache = null!;
    private string[] _texts = null!;

    /// <summary>Whether cell and header text is drawn.</summary>
    [Params(true, false)]
    public bool WithText { get; set; }

    private readonly KumikoPaint _headerBackground = new() { Color = KumikoColor.FromRgb(240, 240, 240) };
    private readonly KumikoPaint _headerText = new() { Color = KumikoColor.Black, Font = new KumikoFont("Default", 14, bold: true) };
    private readonly KumikoPaint _rowBackground = new() { Color = KumikoColor.FromRgb(255, 255, 255) };
    private readonly KumikoPaint _alternateBackground = new() { Color = KumikoColor.FromRgb(248, 248, 252) };
    private readonly KumikoPaint _selectionBackground = new() { Color = KumikoColor.FromRgb(204, 228, 255) };
    private readonly KumikoPaint _cellText = new() { Color = KumikoColor.FromRgb(33, 33, 33), Font = new KumikoFont("Default", 13) };
    private readonly KumikoPaint _gridLine = new() { Color = KumikoColor.FromRgb(224, 224, 224), StrokeWidth = 1 };

    [GlobalSetup]
    public void Setup()
    {
        _bitmap = new SKBitmap((int)(Columns * ColumnWidth), (int)((Rows + 1) * RowHeight));
        _canvas = new SKCanvas(_bitmap);
        _cache = new SkiaResourceCache();
        _texts = Enumerable.Range(0, Rows * Columns).Select(i => $"Cell {i}").ToArray();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _cache.Dispose();
        _canvas.Dispose();
        _bitmap.Dispose();
    }

    [Benchmark(Baseline = true)]
    public void PerFrameObjects()
    {
        using var ctx = new SkiaDrawingContext(_canvas);
        DrawFrame(ctx);
    }

    [Benchmark]
    public void SharedCache()
    {
        using var ctx = new SkiaDrawingContext(_canvas, _cache);
        DrawFrame(ctx);
    }

    private void DrawFrame(SkiaDrawingContext ctx)
    {
        if (!WithText)
        {
            ctx.GetFontMetrics(_headerText);
            ctx.GetFontMetrics(_cellText);
        }

        for (int c = 0; c < Columns; c++)
        {
            var header = new KumikoRect(c * ColumnWidth, 0, ColumnWidth, RowHeight);
            ctx.FillRect(header, _headerBackground);
            if (WithText)
                ctx.DrawTextInRect("Header", header, _headerText);
        }

        for (int r = 0; r < Rows; r++)
        {
            var background = r == 3 ? _selectionBackground : (r % 2 == 0 ? _rowBackground : _alternateBackground);
            float y = (r + 1) * RowHeight;
            for (int c = 0; c < Columns; c++)
            {
                var cell = new KumikoRect(c * ColumnWidth, y, ColumnWidth, RowHeight);
                ctx.FillRect(cell, background);
                if (WithText)
                    ctx.DrawTextInRect(_texts[r * Columns + c], cell, _cellText);
            }
            ctx.DrawLine(0, y, Columns * ColumnWidth, y, _gridLine);
        }

        for (int c = 1; c < Columns; c++)
            ctx.DrawLine(c * ColumnWidth, 0, c * ColumnWidth, (Rows + 1) * RowHeight, _gridLine);
    }
}

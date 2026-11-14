using System.Diagnostics;
using KumikoUI.Core.Layout;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.Rendering;

// Runs alone, so a timing is not inflated by other tests competing for the CPU.
[CollectionDefinition(nameof(AutoFitTimingCollection), DisableParallelization = true)]
public sealed class AutoFitTimingCollection;

/// <summary>
/// Auto-fit measures a bounded sample of rows, not every row (#24), so fitting a column of 100 000 rows takes well
/// under the 50 ms budget. Before #24 it formatted and measured all 100 000 cells (a multi-second stall with Skia).
/// </summary>
/// <remarks>
/// The bound is checked two ways: deterministically, by counting the cells measured, and by time. The time is the
/// best of several runs after a warm-up, so JIT, tiering or a busy CI runner cannot make it flaky; the sampled pass
/// takes well under a millisecond, far below the bound.
/// </remarks>
[Collection(nameof(AutoFitTimingCollection))]
public class AutoFitTimingTests(Xunit.Abstractions.ITestOutputHelper output)
{
    private const int Rows = 100_000;
    private const double BudgetMs = 50;

    public sealed class Row
    {
        public string Name { get; set; } = "";
    }

    private static (KumikoSource Source, KumikoColumn Column) CreateSource()
    {
        var column = new KumikoColumn { Header = "Name", PropertyName = nameof(Row.Name), MaxWidth = 1000 };
        var source = new KumikoSource();
        source.SetColumns(new[] { column });
        var rows = new List<Row>(Rows);
        for (int i = 0; i < Rows; i++)
            rows.Add(new Row { Name = $"Row {(i * 7919L) % 1_000_003:D7}" });
        source.SetItems(rows);
        return (source, column);
    }

    [Fact]
    public void AutoFit_On100KRows_MeasuresABoundedSample()
    {
        var (source, column) = CreateSource();
        var engine = new KumikoLayoutEngine();
        var ctx = new CountingDrawingContext();

        engine.CalculateAutoFitWidth(column, source, ctx, new KumikoStyle(), 50_000, 50_030);

        // The header, the first and last 100 rows and the 31 rows on screen.
        Assert.Equal(1 + 2 * engine.AutoFitSampleRows + 31, ctx.Measured);
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void AutoFit_On100KRows_CompletesWithin50Milliseconds()
    {
        var (source, column) = CreateSource();
        var engine = new KumikoLayoutEngine();
        var ctx = new RecordingDrawingContext();
        var style = new KumikoStyle();
        var cold = Stopwatch.StartNew();
        engine.CalculateAutoFitWidth(column, source, ctx, style, 50_000, 50_030); // also the warm-up
        cold.Stop();

        double best = double.MaxValue;
        for (int run = 0; run < 5; run++)
        {
            var watch = Stopwatch.StartNew();
            engine.CalculateAutoFitWidth(column, source, ctx, style, 50_000, 50_030);
            watch.Stop();
            best = Math.Min(best, watch.Elapsed.TotalMilliseconds);
        }

        output.WriteLine($"auto-fit on {Rows} rows: first run {cold.Elapsed.TotalMilliseconds:F3} ms, best of 5 {best:F3} ms");
        Assert.True(best < BudgetMs, $"auto-fit on {Rows} rows took {best:F3} ms (budget {BudgetMs} ms)");
    }

    /// <summary>Measures like <see cref="RecordingDrawingContext"/> and counts the measurements; draws nothing.</summary>
    private sealed class CountingDrawingContext : IDrawingContext
    {
        private readonly NoOpDrawingContext _inner = new();

        public int Measured { get; private set; }

        public KumikoSize MeasureText(string text, KumikoPaint paint)
        {
            Measured++;
            return _inner.MeasureText(text, paint);
        }

        public void DrawRect(KumikoRect rect, KumikoPaint paint) { }
        public void FillRect(KumikoRect rect, KumikoPaint paint) { }
        public void DrawRoundRect(KumikoRect rect, float cornerRadius, KumikoPaint paint) { }
        public void FillRoundRect(KumikoRect rect, float cornerRadius, KumikoPaint paint) { }
        public void DrawLine(float x1, float y1, float x2, float y2, KumikoPaint paint) { }
        public void DrawText(string text, float x, float y, KumikoPaint paint) { }
        public void DrawTextInRect(string text, KumikoRect rect, KumikoPaint paint,
            KumikoTextAlignment hAlign = KumikoTextAlignment.Left,
            KumikoVerticalAlignment vAlign = KumikoVerticalAlignment.Center,
            bool clip = true)
        { }
        public KumikoFontMetrics GetFontMetrics(KumikoPaint paint) => _inner.GetFontMetrics(paint);
        public void ClipRect(KumikoRect rect) { }
        public void Save() { }
        public void Restore() { }
        public void Translate(float dx, float dy) { }
        public void DrawImage(object image, KumikoRect destRect) { }
    }
}

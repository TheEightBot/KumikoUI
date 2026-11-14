using BenchmarkDotNet.Attributes;
using KumikoUI.Core;
using KumikoUI.Core.Data;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Benchmarks;

[KumikoGridItem]
public class RenderRow
{
    public string Name { get; set; } = "";
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public double Ratio { get; set; }
    public DateTime Date { get; set; }
    public bool Active { get; set; }
    public string City { get; set; } = "";
    public long Code { get; set; }
    public float Score { get; set; }
    public string Notes { get; set; } = "";
}

/// <summary>
/// An <see cref="IDrawingContext"/> that draws nothing, so a benchmark measures only the managed render loop.
/// </summary>
public sealed class NoOpDrawingContext : IDrawingContext
{
    public void DrawRect(KumikoRect rect, KumikoPaint paint) { }
    public void FillRect(KumikoRect rect, KumikoPaint paint) { }
    public void DrawRoundRect(KumikoRect rect, float cornerRadius, KumikoPaint paint) { }
    public void FillRoundRect(KumikoRect rect, float cornerRadius, KumikoPaint paint) { }
    public void DrawLine(float x1, float y1, float x2, float y2, KumikoPaint paint) { }
    public void DrawText(string text, float x, float y, KumikoPaint paint) { }
    public void DrawTextInRect(string text, KumikoRect rect, KumikoPaint paint,
        KumikoTextAlignment hAlign = KumikoTextAlignment.Left,
        KumikoVerticalAlignment vAlign = KumikoVerticalAlignment.Center, bool clip = true)
    { }
    public KumikoSize MeasureText(string text, KumikoPaint paint) => new(text.Length * 7f, 14f);
    public KumikoFontMetrics GetFontMetrics(KumikoPaint paint) => new(-11, 3, 2);
    public void ClipRect(KumikoRect rect) { }
    public void Save() { }
    public void Restore() { }
    public void Translate(float dx, float dy) { }
    public void DrawImage(object image, KumikoRect destRect) { }
}

/// <summary>
/// One frame of <see cref="KumikoRenderer.Render"/> for a 30-row × 10-column visible grid into a
/// <see cref="NoOpDrawingContext"/> (#68). <c>Plain</c> is a bare grid; <c>Featured</c> adds a multi-column
/// sort, an active filter, grouping with group and caption summaries, top and bottom table summaries, a
/// frozen column and the row drag handle. Each benchmark renders the same frame repeatedly, so after the
/// warm-up frames it measures the steady state, which must allocate nothing.
/// </summary>
[MemoryDiagnoser]
public class RenderBenchmarks
{
    private const int Rows = 10_000;
    private const int VisibleRows = 30;
    private const int VisibleColumns = 10;
    private const float ColumnWidth = 110f;

    private readonly NoOpDrawingContext _ctx = new();
    private readonly KumikoRenderer _renderer = new();
    private Grid _plain = null!;
    private Grid _featured = null!;

    private sealed record Grid(KumikoSource Source, ScrollState Scroll, KumikoSelectionModel Selection, KumikoStyle Style);

    [GlobalSetup]
    public void Setup()
    {
        _plain = CreateGrid(featured: false);
        _featured = CreateGrid(featured: true);

        // Warm-up frames: fill the caches before the measured iterations.
        for (int i = 0; i < 5; i++)
        {
            Render(_plain);
            Render(_featured);
        }
    }

    [Benchmark(Baseline = true)]
    public void Render_30x10_Plain() => Render(_plain);

    [Benchmark]
    public void Render_30x10_Featured() => Render(_featured);

    /// <summary>
    /// A scrolling frame: the plain grid drawn at one of 200 offsets (a row and 7 px further each time, so every
    /// frame reveals one new row and only the display text cache's misses allocate), then wrapping around.
    /// </summary>
    [Benchmark]
    public void Render_30x10_Scrolling()
    {
        _scrollStep = (_scrollStep + 1) % 200;
        _plain.Scroll.OffsetY = _scrollStep * (_plain.Style.RowHeight + 7);
        Render(_plain);
    }

    private int _scrollStep;

    private readonly KumikoRenderer _statsRenderer = new() { ShowFrameStats = true };

    /// <summary>The plain frame with the frame-time overlay on; the overlay's own cost is the difference to <c>Render_30x10_Plain</c>.</summary>
    [Benchmark]
    public void Render_30x10_Plain_FrameStatsOn() =>
        _statsRenderer.Render(_ctx, _plain.Source, _plain.Scroll, _plain.Selection, _plain.Style);

    private void Render(Grid grid) =>
        _renderer.Render(_ctx, grid.Source, grid.Scroll, grid.Selection, grid.Style);

    private static Grid CreateGrid(bool featured)
    {
        var items = Enumerable.Range(0, Rows).Select(i => new RenderRow
        {
            Name = $"Row {i}",
            Quantity = i % 97,
            Price = 10m + i % 50,
            Ratio = i / 7.0,
            Date = new DateTime(2026, 1, 1).AddDays(i % 365),
            Active = i % 3 == 0,
            City = (i % 4) switch { 0 => "Oslo", 1 => "Rome", 2 => "Lima", _ => "Kyiv" },
            Code = 1_000_000L + i,
            Score = i % 100 / 3f,
            Notes = $"Note {i % 10}",
        }).ToList();

        var columns = new[]
        {
            new KumikoColumn { Header = "Name", PropertyName = nameof(RenderRow.Name), Width = ColumnWidth },
            new KumikoColumn { Header = "Quantity", PropertyName = nameof(RenderRow.Quantity), Width = ColumnWidth, ColumnType = KumikoColumnType.Numeric },
            new KumikoColumn { Header = "Price", PropertyName = nameof(RenderRow.Price), Width = ColumnWidth, ColumnType = KumikoColumnType.Numeric, Format = "C2" },
            new KumikoColumn { Header = "Ratio", PropertyName = nameof(RenderRow.Ratio), Width = ColumnWidth, ColumnType = KumikoColumnType.Numeric, Format = "N3" },
            new KumikoColumn { Header = "Date", PropertyName = nameof(RenderRow.Date), Width = ColumnWidth, ColumnType = KumikoColumnType.Date, Format = "yyyy-MM-dd" },
            new KumikoColumn { Header = "Active", PropertyName = nameof(RenderRow.Active), Width = ColumnWidth, ColumnType = KumikoColumnType.Boolean },
            new KumikoColumn { Header = "City", PropertyName = nameof(RenderRow.City), Width = ColumnWidth },
            new KumikoColumn { Header = "Code", PropertyName = nameof(RenderRow.Code), Width = ColumnWidth, ColumnType = KumikoColumnType.Numeric },
            new KumikoColumn { Header = "Score", PropertyName = nameof(RenderRow.Score), Width = ColumnWidth, ColumnType = KumikoColumnType.Numeric, Format = "N1" },
            new KumikoColumn { Header = "Notes", PropertyName = nameof(RenderRow.Notes), Width = ColumnWidth },
        };

        var source = new KumikoSource();
        source.SetColumns(columns);
        source.SetItems(items);
        var style = new KumikoStyle();

        if (featured)
        {
            columns[0].IsFrozen = true;
            style.ShowRowDragHandle = true;
            source.BeginUpdate();
            source.ToggleSort(columns[1]);
            source.ToggleMultiSort(columns[2]);
            source.SetColumnFilter(columns[6], new FilterDescription(columns[6])
            {
                Condition1 = new FilterCondition { TextOperator = TextFilterOperator.NotEquals, Value = "Never" },
            });
            source.AddGroupDescription(new GroupDescription(nameof(RenderRow.City)));
            source.AddTableSummaryRow(new TableSummaryRow
            {
                Name = "Top", Title = "Totals", Position = SummaryPosition.Top,
                Columns = { new SummaryColumnDescription(nameof(RenderRow.Quantity), SummaryType.Sum) },
            });
            source.AddTableSummaryRow(new TableSummaryRow
            {
                Name = "Bottom", Title = "Averages", Position = SummaryPosition.Bottom,
                Columns =
                {
                    new SummaryColumnDescription(nameof(RenderRow.Price), SummaryType.Average),
                    new SummaryColumnDescription(nameof(RenderRow.Ratio), SummaryType.Max),
                },
            });
            source.AddGroupSummaryRow(new GroupSummaryRow
            {
                Name = "Group", Title = "Group total",
                Columns = { new SummaryColumnDescription(nameof(RenderRow.Quantity), SummaryType.Sum) },
            });
            source.CaptionSummary = new CaptionSummaryDescription
            {
                Columns = { new SummaryColumnDescription(nameof(RenderRow.Price), SummaryType.Sum) },
            };
            source.EndUpdate();
        }

        float handle = featured ? style.RowDragHandleWidth : 0;
        var scroll = new ScrollState
        {
            ViewportWidth = VisibleColumns * ColumnWidth + handle,
            ViewportHeight = style.HeaderHeight + VisibleRows * style.RowHeight
                + (featured ? style.GroupPanelHeight + 2 * style.SummaryRowHeight : 0),
        };
        var selection = new KumikoSelectionModel(source);
        selection.HandleRowSelection(3, shift: false, ctrl: false);
        selection.SetCurrentCell(3, 1);
        return new Grid(source, scroll, selection, style);
    }
}

using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.Rendering;

/// <summary>
/// Allocation budgets (#70, see bench/README.md "Performance budgets"): the data pipeline on 100 000 rows allocates
/// no more than a generous ceiling per operation, and the frame-time overlay adds nothing to a frame. Time budgets
/// are deliberately not asserted (too noisy on shared CI runners); they are documented and measured with the
/// BenchmarkDotNet project. Allocations are counted with <see cref="GC.GetAllocatedBytesForCurrentThread"/>, which
/// is deterministic for a single-threaded rebuild.
/// </summary>
public class PerformanceBudgetTests
{
    private const int Rows = 100_000;

    // Ceilings sit well above what the operations allocate today; they catch a regression (a per-row allocation
    // or a boxed comparison on the hot path), not a few percent of drift.
    private const long MiB = 1024 * 1024;
    private const long SortBudget = 160 * MiB;          // measured ~90 MB (int, date: ~900 B per row)
    private const long FilterBudget = 12 * MiB;         // measured ~5.6 MB numeric, under 1 KB text
    private const long GroupBudget = 40 * MiB;          // measured ~11-19 MB
    private const long IncrementalBudget = 4 * 1024;    // measured 432 B

    public sealed class Row
    {
        public string Name { get; set; } = "";
        public int Quantity { get; set; }
        public DateTime Date { get; set; }
        public string City { get; set; } = "";
        public string Notes { get; set; } = "";
    }

    private static (KumikoSource Source, KumikoColumn[] Columns) CreateSource()
    {
        var columns = new[]
        {
            new KumikoColumn { Header = "Name", PropertyName = nameof(Row.Name) },
            new KumikoColumn { Header = "Quantity", PropertyName = nameof(Row.Quantity), ColumnType = KumikoColumnType.Numeric },
            new KumikoColumn { Header = "Date", PropertyName = nameof(Row.Date), ColumnType = KumikoColumnType.Date },
            new KumikoColumn { Header = "City", PropertyName = nameof(Row.City) },
        };
        var source = new KumikoSource();
        source.SetColumns(columns);
        var cities = new[] { "Oslo", "Rome", "Lima", "Kyiv", "Cairo", "Perth", "Quito", "Hanoi" };
        var rows = new List<Row>(Rows);
        for (int i = 0; i < Rows; i++)
        {
            rows.Add(new Row
            {
                Name = $"Row {(i * 7919L) % 1_000_003:D7}",
                Quantity = (int)((i * 2654435761L) % 100_000),
                Date = new DateTime(2020, 1, 1).AddDays((i * 31L) % 2000),
                City = cities[i % cities.Length],
                Notes = $"Note {i % 10}",
            });
        }
        source.SetItems(rows);
        return (source, columns);
    }

    /// <summary>Bytes allocated by <paramref name="action"/>, after one untimed run that absorbs JIT and first-use costs.</summary>
    private static long Allocated(Action action)
    {
        action();
        long before = GC.GetAllocatedBytesForCurrentThread();
        action();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    [Fact]
    public void Sort_100KRows_StaysWithinTheAllocationBudget()
    {
        var (source, columns) = CreateSource();

        foreach (var column in new[] { columns[1], columns[0], columns[2] })
        {
            long bytes = Allocated(() =>
            {
                source.BeginUpdate();   // one rebuild; clearing first makes the repeat a real sort, not a no-op
                source.ClearSort();
                source.SetSort(column, KumikoSortDirection.Ascending);
                source.EndUpdate();
            });

            Assert.True(bytes <= SortBudget, $"sorting {Rows:N0} rows by {column.Header} allocated {bytes:N0} B (budget {SortBudget:N0})");
        }
    }

    [Fact]
    public void Filter_100KRows_StaysWithinTheAllocationBudget()
    {
        var (source, columns) = CreateSource();

        long text = Allocated(() =>
        {
            source.BeginUpdate();
            source.ClearAllFilters();
            source.SetColumnFilter(columns[0], new FilterDescription(columns[0])
            {
                Condition1 = new FilterCondition { TextOperator = TextFilterOperator.Contains, Value = "77" },
            });
            source.EndUpdate();
        });
        long numeric = Allocated(() =>
        {
            source.BeginUpdate();
            source.ClearAllFilters();
            source.SetColumnFilter(columns[1], new FilterDescription(columns[1])
            {
                Condition1 = new FilterCondition { NumericOperator = NumericFilterOperator.GreaterThan, Value = 50_000d },
            });
            source.EndUpdate();
        });

        Assert.True(text <= FilterBudget, $"a text filter over {Rows:N0} rows allocated {text:N0} B (budget {FilterBudget:N0})");
        Assert.True(numeric <= FilterBudget, $"a numeric filter over {Rows:N0} rows allocated {numeric:N0} B (budget {FilterBudget:N0})");
    }

    [Fact]
    public void Group_100KRows_WithSummaries_StaysWithinTheAllocationBudget()
    {
        var (source, _) = CreateSource();
        source.AddGroupSummaryRow(new GroupSummaryRow
        {
            Name = "Group", Title = "Total",
            Columns = { new SummaryColumnDescription(nameof(Row.Quantity), SummaryType.Sum) },
        });

        long oneLevel = Allocated(() =>
        {
            source.BeginUpdate();
            source.ClearGroupDescriptions();
            source.AddGroupDescription(new GroupDescription(nameof(Row.City)));
            source.EndUpdate();
        });
        long twoLevels = Allocated(() =>
        {
            source.BeginUpdate();
            source.ClearGroupDescriptions();
            source.AddGroupDescription(new GroupDescription(nameof(Row.City)));
            source.AddGroupDescription(new GroupDescription(nameof(Row.Notes)));
            source.EndUpdate();
        });

        Assert.True(oneLevel <= GroupBudget, $"grouping {Rows:N0} rows by one level allocated {oneLevel:N0} B (budget {GroupBudget:N0})");
        Assert.True(twoLevels <= GroupBudget, $"grouping {Rows:N0} rows by two levels allocated {twoLevels:N0} B (budget {GroupBudget:N0})");
    }

    [Fact]
    public void IncrementalAddAndRemove_100KRows_StaysWithinTheAllocationBudget()
    {
        var (source, _) = CreateSource();
        var items = new System.Collections.ObjectModel.ObservableCollection<Row>(
            Enumerable.Range(0, Rows).Select(i => new Row { Name = $"Row {i}", Quantity = i, City = "Oslo" }));
        source.SetItems(items);
        var extra = new Row { Name = "Extra", City = "Oslo" };

        long bytes = Allocated(() =>
        {
            items.Add(extra);
            items.RemoveAt(items.Count - 1);
        });

        // An O(1) change, not a rebuild of the 100 000-row view: a few event args and a list append.
        Assert.True(bytes <= IncrementalBudget, $"adding and removing one item allocated {bytes:N0} B (budget {IncrementalBudget:N0})");
    }

    [Fact]
    public void FrameStatsOverlay_AddsNothingToAFrame_WhetherOffOrOn()
    {
        var (source, _) = CreateSource();
        var scroll = new ScrollState { ViewportWidth = 440, ViewportHeight = 40 + 30 * 36 };
        var selection = new KumikoSelectionModel(source);
        var style = new KumikoStyle();
        var ctx = new NoOpDrawingContext();
        var renderer = new KumikoRenderer();
        renderer.FrameStats.UpdateInterval = TimeSpan.FromHours(1);   // no window ends, so no string is built

        for (int i = 0; i < 20; i++)
            renderer.Render(ctx, source, scroll, selection, style);
        long off = Allocated(() => renderer.Render(ctx, source, scroll, selection, style));

        renderer.ShowFrameStats = true;
        for (int i = 0; i < 20; i++)
            renderer.Render(ctx, source, scroll, selection, style);
        long on = Allocated(() => renderer.Render(ctx, source, scroll, selection, style));

        Assert.Equal(0, off);
        Assert.Equal(0, on);
    }

    [Fact]
    public void FrameStatsOverlay_WhenOn_DrawsTheReadoutAndReportsTheFrame()
    {
        var (source, _) = CreateSource();
        var scroll = new ScrollState { ViewportWidth = 440, ViewportHeight = 40 + 30 * 36 };
        var selection = new KumikoSelectionModel(source);
        var style = new KumikoStyle();
        var ctx = new RecordingDrawingContext();
        var renderer = new KumikoRenderer { ShowFrameStats = true };
        renderer.FrameStats.UpdateInterval = TimeSpan.Zero;   // every frame ends a window

        renderer.Render(ctx, source, scroll, selection, style);
        string text = renderer.FrameStats.Text;
        ctx.Ops.Clear();
        renderer.Render(ctx, source, scroll, selection, style);

        Assert.Matches(@"^\d+\.\d\d ms \| \d+ fps \| (\d+ B|[\d.]+ KB)/frame$", text);
        Assert.True(renderer.FrameStats.FrameTimeMs > 0);
        Assert.Contains(ctx.Ops, op => op.Kind == DrawOpKind.TextInRect && op.Text != null && op.Text.EndsWith("/frame"));
    }
}

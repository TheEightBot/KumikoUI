using System.Collections.ObjectModel;
using BenchmarkDotNet.Attributes;
using KumikoUI.Core;
using KumikoUI.Core.Layout;
using KumikoUI.Core.Models;

namespace KumikoUI.Benchmarks;

/// <summary>Deterministic row and column generators shared by the data benchmarks (#70).</summary>
public static class BenchData
{
    public static readonly string[] Cities = { "Oslo", "Rome", "Lima", "Kyiv", "Cairo", "Perth", "Quito", "Hanoi" };

    /// <summary>Row <c>i</c> is a pure function of <c>i</c>, so every run sorts, filters and groups the same data.</summary>
    public static RenderRow CreateRow(int i) => new()
    {
        Name = $"Row {(i * 7919L) % 1_000_003:D7}",
        Quantity = (int)((i * 2654435761L) % 100_000),
        Price = 10m + i % 500,
        Ratio = i / 7.0,
        Date = new DateTime(2020, 1, 1).AddDays((i * 31L) % 2000),
        Active = i % 3 == 0,
        City = Cities[i % Cities.Length],
        Code = 1_000_000L + i,
        Score = i % 100 / 3f,
        Notes = $"Note {i % 10}",
    };

    public static List<RenderRow> CreateRows(int count)
    {
        var rows = new List<RenderRow>(count);
        for (int i = 0; i < count; i++)
            rows.Add(CreateRow(i));
        return rows;
    }

    public static KumikoColumn[] CreateColumns(float width = 110f) => new[]
    {
        new KumikoColumn { Header = "Name", PropertyName = nameof(RenderRow.Name), Width = width },
        new KumikoColumn { Header = "Quantity", PropertyName = nameof(RenderRow.Quantity), Width = width, ColumnType = KumikoColumnType.Numeric },
        new KumikoColumn { Header = "Price", PropertyName = nameof(RenderRow.Price), Width = width, ColumnType = KumikoColumnType.Numeric, Format = "C2" },
        new KumikoColumn { Header = "Ratio", PropertyName = nameof(RenderRow.Ratio), Width = width, ColumnType = KumikoColumnType.Numeric, Format = "N3" },
        new KumikoColumn { Header = "Date", PropertyName = nameof(RenderRow.Date), Width = width, ColumnType = KumikoColumnType.Date, Format = "yyyy-MM-dd" },
        new KumikoColumn { Header = "Active", PropertyName = nameof(RenderRow.Active), Width = width, ColumnType = KumikoColumnType.Boolean },
        new KumikoColumn { Header = "City", PropertyName = nameof(RenderRow.City), Width = width },
        new KumikoColumn { Header = "Code", PropertyName = nameof(RenderRow.Code), Width = width, ColumnType = KumikoColumnType.Numeric },
        new KumikoColumn { Header = "Score", PropertyName = nameof(RenderRow.Score), Width = width, ColumnType = KumikoColumnType.Numeric, Format = "N1" },
        new KumikoColumn { Header = "Notes", PropertyName = nameof(RenderRow.Notes), Width = width },
    };
}

/// <summary>
/// The data pipeline on 100 000 rows (#70): sorting by an int, a string and a date column, a text filter and a
/// numeric filter, grouping by one and two levels with group and table summaries, and <c>ComputeColumnWidths</c>.
/// Each benchmark is one view rebuild: it replaces whatever sort, filter and grouping the previous benchmark
/// left with the one it measures, inside <c>BeginUpdate</c>/<c>EndUpdate</c> so there is a single rebuild.
/// </summary>
[MemoryDiagnoser]
public class DataBenchmarks
{
    private const int Rows = 100_000;

    private KumikoSource _source = null!;
    private KumikoColumn[] _columns = null!;
    private KumikoLayoutEngine _layout = null!;

    [GlobalSetup]
    public void Setup()
    {
        _columns = BenchData.CreateColumns();
        _source = new KumikoSource();
        _source.SetColumns(_columns);
        _source.SetItems(BenchData.CreateRows(Rows));
        _layout = new KumikoLayoutEngine();
    }

    private void Rebuild(Action configure)
    {
        _source.BeginUpdate();
        _source.ClearSort();
        _source.ClearAllFilters();
        _source.ClearGroupDescriptions();
        _source.ClearGroupSummaryRows();
        _source.ClearTableSummaryRows();
        _source.CaptionSummary = null;
        configure();
        _source.EndUpdate();
    }

    [Benchmark] public void Sort_Int() => Rebuild(() => _source.SetSort(_columns[1], KumikoSortDirection.Ascending));
    [Benchmark] public void Sort_String() => Rebuild(() => _source.SetSort(_columns[0], KumikoSortDirection.Ascending));
    [Benchmark] public void Sort_Date() => Rebuild(() => _source.SetSort(_columns[4], KumikoSortDirection.Descending));

    [Benchmark]
    public void Sort_TwoColumns() => Rebuild(() =>
    {
        _source.SetSort(_columns[6], KumikoSortDirection.Ascending, 0);
        _source.SetSort(_columns[1], KumikoSortDirection.Descending, 1);
    });

    [Benchmark]
    public void Filter_TextContains() => Rebuild(() => _source.SetColumnFilter(_columns[0],
        new FilterDescription(_columns[0]) { Condition1 = new FilterCondition { TextOperator = TextFilterOperator.Contains, Value = "77" } }));

    [Benchmark]
    public void Filter_NumericGreaterThan() => Rebuild(() => _source.SetColumnFilter(_columns[1],
        new FilterDescription(_columns[1]) { Condition1 = new FilterCondition { NumericOperator = NumericFilterOperator.GreaterThan, Value = 50_000d } }));

    [Benchmark]
    public void Group_OneLevel_WithSummaries() => Rebuild(() =>
    {
        _source.AddGroupDescription(new GroupDescription(nameof(RenderRow.City)));
        AddSummaries();
    });

    [Benchmark]
    public void Group_TwoLevels_WithSummaries() => Rebuild(() =>
    {
        _source.AddGroupDescription(new GroupDescription(nameof(RenderRow.City)));
        _source.AddGroupDescription(new GroupDescription(nameof(RenderRow.Notes)));
        AddSummaries();
    });

    [Benchmark]
    public void Sort_Filter_Group_Together() => Rebuild(() =>
    {
        _source.SetSort(_columns[1], KumikoSortDirection.Ascending);
        _source.SetColumnFilter(_columns[1],
            new FilterDescription(_columns[1]) { Condition1 = new FilterCondition { NumericOperator = NumericFilterOperator.GreaterThan, Value = 10_000d } });
        _source.AddGroupDescription(new GroupDescription(nameof(RenderRow.City)));
        AddSummaries();
    });

    [Benchmark]
    public void ComputeColumnWidths_10Columns() => _layout.ComputeColumnWidths(_columns, 1100f);

    private void AddSummaries()
    {
        _source.AddGroupSummaryRow(new GroupSummaryRow
        {
            Name = "Group", Title = "Group total",
            Columns = { new SummaryColumnDescription(nameof(RenderRow.Quantity), SummaryType.Sum) },
        });
        _source.AddTableSummaryRow(new TableSummaryRow
        {
            Name = "Bottom", Title = "Totals", Position = SummaryPosition.Bottom,
            Columns =
            {
                new SummaryColumnDescription(nameof(RenderRow.Price), SummaryType.Average),
                new SummaryColumnDescription(nameof(RenderRow.Ratio), SummaryType.Max),
            },
        });
    }
}

/// <summary>
/// Incremental collection changes on an <see cref="ObservableCollection{T}"/> of 100 000 items with no sort,
/// filter, grouping or summary active (#70): adding and removing one item at the end and in the middle is
/// handled without rebuilding the view.
/// </summary>
[MemoryDiagnoser]
public class IncrementalBenchmarks
{
    private const int Rows = 100_000;

    private ObservableCollection<RenderRow> _items = null!;
    private KumikoSource _source = null!;
    private RenderRow _extra = null!;

    [GlobalSetup]
    public void Setup()
    {
        _items = new ObservableCollection<RenderRow>(BenchData.CreateRows(Rows));
        _source = new KumikoSource();
        _source.SetColumns(BenchData.CreateColumns());
        _source.SetItems(_items);
        _extra = BenchData.CreateRow(Rows);
    }

    [Benchmark]
    public void AddThenRemove_AtEnd()
    {
        _items.Add(_extra);
        _items.RemoveAt(_items.Count - 1);
    }

    [Benchmark]
    public void InsertThenRemove_InMiddle()
    {
        _items.Insert(Rows / 2, _extra);
        _items.RemoveAt(Rows / 2);
    }
}

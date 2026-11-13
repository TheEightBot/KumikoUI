using System.ComponentModel;
using BenchmarkDotNet.Attributes;
using KumikoUI.Core.Models;

namespace KumikoUI.Benchmarks;

/// <summary>A row that raises <see cref="INotifyPropertyChanged.PropertyChanged"/> for <see cref="Quantity"/>.</summary>
public sealed class LiveRow : INotifyPropertyChanged
{
    private int _quantity;

    public string Name { get; set; } = "";

    public int Quantity
    {
        get => _quantity;
        set
        {
            _quantity = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Quantity)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// Live updates on 100 000 rows (#69): 100 items change a summarized property, the way a ticker or a SignalR feed
/// updates a grid. With a Sum (or Average) summary the total follows each change in constant time; with a Max
/// summary the table summaries are recomputed over the visible rows once per batch, but nothing is filtered,
/// sorted or grouped again. Before #69 every change rebuilt the whole view.
/// </summary>
[MemoryDiagnoser]
public class LiveUpdateBenchmarks
{
    private const int Rows = 100_000;

    private LiveRow[] _sumTargets = null!;
    private LiveRow[] _maxTargets = null!;
    private KumikoSource _sumSource = null!; // kept so the rows report to a live source
    private KumikoSource _maxSource = null!;
    private int _delta;

    [GlobalSetup]
    public void Setup()
    {
        // Each source observes its own rows, so a change reaches one source only.
        (_sumSource, _sumTargets) = CreateSource(SummaryType.Sum);
        (_maxSource, _maxTargets) = CreateSource(SummaryType.Max);
    }

    private static (KumikoSource Source, LiveRow[] Targets) CreateSource(SummaryType type)
    {
        var rows = new LiveRow[Rows];
        for (int i = 0; i < Rows; i++)
            rows[i] = new LiveRow { Name = $"Row {i}", Quantity = (int)((i * 2654435761L) % 100_000) };

        var source = new KumikoSource();
        source.SetColumns(new[]
        {
            new KumikoColumn { Header = "Name", PropertyName = nameof(LiveRow.Name) },
            new KumikoColumn { Header = "Quantity", PropertyName = nameof(LiveRow.Quantity), ColumnType = KumikoColumnType.Numeric },
        });
        source.SetItems(rows);
        source.AddTableSummaryRow(new TableSummaryRow("Totals")
        {
            Columns = { new SummaryColumnDescription(nameof(LiveRow.Quantity), type) },
        });
        source.ViewChanged += static (_, _) => { }; // a grid listens

        var random = new Random(69);
        var targets = new LiveRow[100];
        for (int i = 0; i < targets.Length; i++)
            targets[i] = rows[random.Next(Rows)];
        return (source, targets);
    }

    /// <summary>100 property changes with a Sum summary, one notification each.</summary>
    [Benchmark]
    public void HundredChanges_Sum()
    {
        _delta = -_delta + 1;
        foreach (var row in _sumTargets)
            row.Quantity += _delta;
    }

    /// <summary>100 property changes with a Max summary inside one batch: one recompute at EndUpdate.</summary>
    [Benchmark]
    public void HundredChanges_Max_Batched()
    {
        _delta = -_delta + 1;
        using (_maxSource.BeginUpdate())
        {
            foreach (var row in _maxTargets)
                row.Quantity += _delta;
        }
    }
}

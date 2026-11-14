using System.Collections.Concurrent;
using BenchmarkDotNet.Attributes;
using KumikoUI.Core.Models;

namespace KumikoUI.Benchmarks;

/// <summary>
/// Sorting 1 000 000 rows (#254). <c>Synchronous</c> is the whole rebuild on the calling (UI) thread, as with
/// <see cref="KumikoSource.BackgroundRebuildThreshold"/> off. <c>Background_EndToEnd</c> is the time from the sort
/// request until the sorted view is published, with the sort on a worker; the UI thread's share of it (the call,
/// capturing the items, swapping the view in) is measured by
/// <c>BackgroundRebuildTests.ToggleSort_On1MRows_NeverBlocksTheUiThreadLongerThanAFrame</c>. Each sort alternates
/// between the two directions of the same column, so every one is a real sort.
/// </summary>
[MemoryDiagnoser]
public class BackgroundRebuildBenchmarks
{
    private const int Rows = 1_000_000;

    private UiThread _ui = null!;
    private KumikoSource _background = null!;
    private KumikoSource _synchronous = null!;
    private KumikoColumn _backgroundColumn = null!;
    private KumikoColumn _synchronousColumn = null!;

    public sealed class Row
    {
        public int Id { get; set; }
        public int Quantity { get; set; }
    }

    [GlobalSetup]
    public void Setup()
    {
        var rows = new List<Row>(Rows);
        for (int i = 0; i < Rows; i++)
            rows.Add(new Row { Id = i, Quantity = (int)((i * 2654435761L) % 1_000_000) });

        _ui = new UiThread();
        (_background, _backgroundColumn) = _ui.Invoke(() => Create(rows, KumikoSource.DefaultBackgroundRebuildThreshold));
        _ui.Invoke(_background.WhenViewUpdatedAsync).GetAwaiter().GetResult();
        (_synchronous, _synchronousColumn) = Create(rows, int.MaxValue);
    }

    private static (KumikoSource, KumikoColumn) Create(List<Row> rows, int threshold)
    {
        var column = new KumikoColumn { Header = "Quantity", PropertyName = nameof(Row.Quantity) };
        var source = new KumikoSource { BackgroundRebuildThreshold = threshold };
        source.SetColumns(new[] { new KumikoColumn { Header = "Id", PropertyName = nameof(Row.Id) }, column });
        source.SetItems(rows);
        _ = new KumikoSelectionModel(source); // a grid listens to the view, so a rebuild also maps row keys
        return (source, column);
    }

    [GlobalCleanup]
    public void Cleanup() => _ui.Dispose();

    [Benchmark]
    public void Synchronous() => _synchronous.SetSort(_synchronousColumn, Flip(_synchronousColumn));

    [Benchmark]
    public void Background_EndToEnd()
    {
        _ui.Invoke(() => _background.SetSort(_backgroundColumn, Flip(_backgroundColumn)));
        _ui.Invoke(_background.WhenViewUpdatedAsync).GetAwaiter().GetResult();
    }

    private static KumikoSortDirection Flip(KumikoColumn column) =>
        column.SortDirection == KumikoSortDirection.Ascending ? KumikoSortDirection.Descending : KumikoSortDirection.Ascending;

    /// <summary>A thread with a <see cref="SynchronizationContext"/> that queues onto it, standing in for a UI thread.</summary>
    private sealed class UiThread : IDisposable
    {
        private readonly BlockingCollection<(SendOrPostCallback, object?)> _queue = new();
        private readonly Thread _thread;

        public UiThread()
        {
            _thread = new Thread(() =>
            {
                SynchronizationContext.SetSynchronizationContext(new Context(this));
                foreach (var (callback, state) in _queue.GetConsumingEnumerable())
                    callback(state);
            })
            { IsBackground = true };
            _thread.Start();
        }

        public T Invoke<T>(Func<T> func)
        {
            var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            _queue.Add((_ => done.SetResult(func()), null));
            return done.Task.GetAwaiter().GetResult();
        }

        public void Invoke(Action action) => Invoke(() => { action(); return 0; });

        public void Dispose()
        {
            _queue.CompleteAdding();
            _thread.Join();
        }

        private sealed class Context(UiThread owner) : SynchronizationContext
        {
            public override void Post(SendOrPostCallback d, object? state) => owner._queue.Add((d, state));
        }
    }
}

using System.Globalization;
using System.Text;
using KumikoUI.Core.Export;
using KumikoUI.Core.Models;
using static KumikoUI.Core.Tests.Export.ExportTests;

namespace KumikoUI.Core.Tests.Export;

public class ExportAsyncTests
{
    private static List<Person> Many(int count) => Enumerable.Range(0, count).Select(i => new Person
    {
        Name = "Person " + i, Dept = "D" + i % 7, Salary = i * 1.5m, Hired = new DateTime(2020, 1, 1).AddDays(i % 365), Active = i % 2 == 0,
    }).ToList();

    private sealed class RecordingProgress : IProgress<KumikoExportProgress>
    {
        public List<KumikoExportProgress> Reports { get; } = new();
        public void Report(KumikoExportProgress value) { lock (Reports) Reports.Add(value); }
    }

    [Fact]
    public async Task AlreadyCanceledToken_ThrowsBeforeWritingAnything()
    {
        var stream = new CountingStream();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Source().ExportToCsvAsync(stream, cancellationToken: cts.Token));
        Assert.Equal(0, stream.BytesWritten);
    }

    [Fact]
    public async Task CancelMidExport_ThrowsWithinTheCheckInterval()
    {
        var source = Source(Many(20_000));
        using var cts = new CancellationTokenSource();
        int reads = 0;
        source.SetColumns(new[]
        {
            new KumikoColumn { Header = "N", ValueAccessor = i => { if (Interlocked.Increment(ref reads) == 1000) cts.Cancel(); return ((Person)i).Name; } },
        });
        var stream = new CountingStream();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => source.ExportToCsvAsync(stream, cancellationToken: cts.Token));
        Assert.InRange(reads, 1000, 1000 + 256 + 1);
    }

    [Fact]
    public async Task Progress_IsMonotonic_AndEndsAtTheTotal()
    {
        var source = Source(Many(5000));
        var progress = new RecordingProgress();
        await source.ExportToCsvAsync(new CountingStream(), progress: progress);
        Assert.NotEmpty(progress.Reports);
        Assert.Equal(new KumikoExportProgress(5000, 5000), progress.Reports[^1]);
        Assert.All(progress.Reports, r => Assert.Equal(5000, r.TotalRows));
        for (int i = 1; i < progress.Reports.Count; i++)
            Assert.True(progress.Reports[i].RowsWritten >= progress.Reports[i - 1].RowsWritten);
        Assert.True(progress.Reports.Count <= 101, "at most one report per 1%, plus the last");
    }

    [Fact]
    public async Task Progress_ForAnEmptySource_ReportsOnce()
    {
        var source = Source(Array.Empty<Person>());
        var progress = new RecordingProgress();
        await source.ExportToCsvAsync(new MemoryStream(), progress: progress);
        Assert.Equal(new[] { new KumikoExportProgress(0, 0) }, progress.Reports);
    }

    [Fact]
    public async Task ASourceThatChangesAfterTheExportStarts_DoesNotChangeTheOutput()
    {
        var source = Source(Many(3000));
        var stream = new MemoryStream();
        var task = source.ExportToCsvAsync(stream, csv: new KumikoCsvOptions { EmitByteOrderMark = false });
        // The snapshot is taken before the task is returned: what happens to the source now must not matter.
        source.SetSort(source.Columns[0], KumikoSortDirection.Descending);
        source.SetFilter(p => false);
        source.SetItems(new List<Person>());
        await task;
        var lines = Encoding.UTF8.GetString(stream.ToArray()).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3001, lines.Length);
        Assert.StartsWith("Person 0,", lines[1]);
        Assert.StartsWith("Person 2999,", lines[^1]);
    }

    [Fact]
    public void ExceptionInAValueAccessor_SurfacesUnchanged()
    {
        var source = Source();
        source.SetColumns(new[] { new KumikoColumn { Header = "X", ValueAccessor = _ => throw new InvalidOperationException("boom") } });
        var ex = Assert.Throws<InvalidOperationException>(() => source.ExportToCsvString());
        Assert.Equal("boom", ex.Message);
    }

    [Fact]
    public async Task ExceptionInAValueAccessor_SurfacesUnchangedFromTheAsyncExport()
    {
        var source = Source();
        source.SetColumns(new[] { new KumikoColumn { Header = "X", ValueAccessor = _ => throw new InvalidOperationException("boom") } });
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => source.ExportToCsvAsync(new MemoryStream()));
        Assert.Equal("boom", ex.Message);
    }

    private static KumikoSource Reading(HashSet<int> threads)
    {
        var source = Source(Many(50));
        source.SetColumns(new[] { new KumikoColumn { Header = "N", ValueAccessor = i => { lock (threads) threads.Add(Environment.CurrentManagedThreadId); return ((Person)i).Name; } } });
        return source;
    }

    [Fact]
    public async Task ThreadPoolReads_DoNotRunOnTheCallingThread()
    {
        var threads = new HashSet<int>();
        KumikoSource source = null!;
        int caller = -1;
        var done = new TaskCompletionSource();
        // A dedicated (non-pool) thread plays the UI thread.
        var ui = new Thread(() =>
        {
            caller = Environment.CurrentManagedThreadId;
            source = Reading(threads); // the source belongs to the thread that creates it
            source.ExportToCsvAsync(new MemoryStream()).ContinueWith(t =>
            {
                if (t.IsFaulted) done.SetException(t.Exception!.InnerExceptions); else done.SetResult();
            });
        });
        ui.Start();
        await done.Task;
        Assert.NotEmpty(threads);
        Assert.DoesNotContain(caller, threads);
    }

    [Fact]
    public async Task CallerThreadReads_RunOnTheCallingThread()
    {
        var threads = new HashSet<int>();
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new PumpContext());
        try
        {
            int caller = Environment.CurrentManagedThreadId;
            await Reading(threads).ExportToCsvAsync(new MemoryStream(), new KumikoExportOptions { ReadOnCallerThread = true });
            Assert.Equal(new[] { caller }, threads.ToArray());
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
    }

    private sealed class PumpContext : SynchronizationContext
    {
        public int Posts;
        public override void Post(SendOrPostCallback d, object? state) { Interlocked.Increment(ref Posts); d(state); }
    }

    [Fact]
    public async Task CallerThreadMode_YieldsBetweenChunks()
    {
        var source = Source(Many(5000));
        var context = new PumpContext();
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            var stream = new MemoryStream();
            await source.ExportToCsvAsync(stream, new KumikoExportOptions { ReadOnCallerThread = true }, new KumikoCsvOptions { EmitByteOrderMark = false });
            Assert.True(context.Posts >= 2, $"expected yields between chunks, saw {context.Posts}");
            Assert.Equal(source.ExportToCsvString(), Encoding.UTF8.GetString(stream.ToArray()));
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
    }

    [Fact]
    public async Task HundredThousandRowsByTenColumns_StreamWithABoundedAllocationDelta()
    {
        const int rows = 100_000;
        var items = Enumerable.Range(0, rows).Select(i => new Person { Name = "Person " + i, Dept = "Dept " + i % 50, Salary = i, Hired = new DateTime(2020, 1, 1).AddDays(i % 1000), Active = i % 2 == 0 }).ToList();
        var source = new KumikoSource { Culture = CultureInfo.InvariantCulture };
        var columns = new List<KumikoColumn>();
        for (int c = 0; c < 10; c++)
        {
            int k = c;
            columns.Add(new KumikoColumn { Header = "C" + c, ValueAccessor = i => k % 2 == 0 ? ((Person)i).Name : ((Person)i).Dept, });
        }
        source.SetColumns(columns);
        source.SetItems(items);

        var stream = new CountingStream();
        long peak = 0;
        // Collect at each report (at most every 100 ms) so the peak is live memory, not garbage the runner's GC has not reached yet.
        var progress = new SyncProgress(_ => peak = Math.Max(peak, GC.GetTotalMemory(true)));

        // The source belongs to this thread: take what needs it before the first await.
        var snapshot = KumikoExportSnapshot.Create(source);
        var sink = new CsvStreamSink(new CountingStream(), new KumikoCsvOptions(), canBlock: true, CancellationToken.None);
        var formatter = new CsvRowFormatter(new KumikoCsvOptions(), ',');
        long before = GC.GetAllocatedBytesForCurrentThread();
        foreach (var row in snapshot.Rows)
        {
            formatter.WriteRow(row, sink.Buffer);
            sink.AfterRow();
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < 10_000_000, $"allocated {allocated} bytes streaming 1M cells");

        GC.Collect();
        long baseline = GC.GetTotalMemory(true);
        await source.ExportToCsvAsync(stream, progress: progress);

        Assert.True(stream.BytesWritten > 10_000_000, $"output was {stream.BytesWritten} bytes");
        GC.Collect();
        Assert.True(GC.GetTotalMemory(true) - baseline < 8_000_000, "memory retained after the export");
        Assert.True(peak - baseline < 60_000_000, $"peak {peak - baseline} above the baseline");
    }

    private sealed class SyncProgress(Action<KumikoExportProgress> action) : IProgress<KumikoExportProgress>
    {
        public void Report(KumikoExportProgress value) => action(value);
    }
}

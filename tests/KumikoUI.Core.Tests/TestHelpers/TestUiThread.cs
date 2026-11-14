using System.Collections.Concurrent;
using System.Diagnostics;

namespace KumikoUI.Core.Tests.TestHelpers;

/// <summary>
/// A dedicated thread that runs queued work in order, standing in for an app's UI thread in threading tests. With
/// <c>installContext</c> it also installs a <see cref="SynchronizationContext"/> whose <c>Post</c> queues onto it,
/// as a UI framework does.
/// </summary>
internal sealed class TestUiThread : IDisposable
{
    private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = new();
    private readonly Thread _thread;
    private readonly bool _installContext;

    public TestUiThread(bool installContext = true)
    {
        _installContext = installContext;
        _thread = new Thread(Run) { IsBackground = true, Name = "Test UI thread" };
        _thread.Start();
    }

    public int ManagedThreadId => _thread.ManagedThreadId;

    private long _longestWorkTicks;

    /// <summary>
    /// The longest a single queued work item (an <see cref="Invoke{T}"/> or a posted callback) has run since the last
    /// <see cref="ResetTiming"/>, not counting garbage collection pauses (which stop every thread, not just this one).
    /// </summary>
    public TimeSpan LongestWorkItem => TimeSpan.FromTicks(Interlocked.Read(ref _longestWorkTicks));

    /// <summary>Start measuring <see cref="LongestWorkItem"/> afresh.</summary>
    public void ResetTiming() => Interlocked.Exchange(ref _longestWorkTicks, 0);

    private void Run()
    {
        if (_installContext)
            SynchronizationContext.SetSynchronizationContext(new QueueContext(this));
        foreach (var (callback, state) in _queue.GetConsumingEnumerable())
        {
            var pausedBefore = GC.GetTotalPauseDuration();
            long started = Stopwatch.GetTimestamp();
            callback(state);
            var worked = Stopwatch.GetElapsedTime(started) - (GC.GetTotalPauseDuration() - pausedBefore);
            if (worked.Ticks > Interlocked.Read(ref _longestWorkTicks))
                Interlocked.Exchange(ref _longestWorkTicks, worked.Ticks);
        }
    }

    /// <summary>Run <paramref name="func"/> on the thread, after everything queued before it, and return its result.</summary>
    public T Invoke<T>(Func<T> func)
    {
        var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Add((_ =>
        {
            try { done.SetResult(func()); }
            catch (Exception ex) { done.SetException(ex); }
        }, null));
        return done.Task.GetAwaiter().GetResult();
    }

    /// <summary>Run <paramref name="action"/> on the thread, after everything queued before it.</summary>
    public void Invoke(Action action) => Invoke(() => { action(); return 0; });

    public void Dispose()
    {
        _queue.CompleteAdding();
        _thread.Join();
    }

    private sealed class QueueContext(TestUiThread owner) : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) => owner._queue.Add((d, state));

        public override void Send(SendOrPostCallback d, object? state) => throw new NotSupportedException();
    }
}

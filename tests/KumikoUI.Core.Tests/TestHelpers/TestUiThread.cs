using System.Collections.Concurrent;

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

    private void Run()
    {
        if (_installContext)
            SynchronizationContext.SetSynchronizationContext(new QueueContext(this));
        foreach (var (callback, state) in _queue.GetConsumingEnumerable())
            callback(state);
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

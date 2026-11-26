namespace KumikoUI.Core.Data;

/// <summary>Data for <see cref="KumikoIncrementalLoader.LoadMoreRequested"/>: a batch is about to be requested.</summary>
public sealed class KumikoLoadMoreRequestedEventArgs : EventArgs
{
    internal KumikoLoadMoreRequestedEventArgs(int count, KumikoLoadMoreTrigger trigger, CancellationToken cancellationToken)
    {
        Count = count;
        Trigger = trigger;
        CancellationToken = cancellationToken;
    }

    /// <summary>How many items the grid asks for (<see cref="KumikoIncrementalLoader.BatchSize"/>, or the automatic size).</summary>
    public int Count { get; }

    /// <summary>What started the batch.</summary>
    public KumikoLoadMoreTrigger Trigger { get; }

    /// <summary>Canceled when the grid no longer wants the batch: its items were replaced or reset, or it was detached.</summary>
    public CancellationToken CancellationToken { get; }
}

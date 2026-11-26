namespace KumikoUI.Core.Data;

/// <summary>The state of a grid's incremental loading (<see cref="KumikoIncrementalLoader.State"/>).</summary>
public enum KumikoLoadMoreState
{
    /// <summary>Nothing is loading; more items may be available.</summary>
    Idle,

    /// <summary>One batch is loading.</summary>
    Loading,

    /// <summary>The last batch failed; the grid shows the error with a retry and never retries on its own.</summary>
    Failed,

    /// <summary>The source has no more items (<see cref="IKumikoIncrementalSource.HasMoreItems"/> is false).</summary>
    Completed,
}

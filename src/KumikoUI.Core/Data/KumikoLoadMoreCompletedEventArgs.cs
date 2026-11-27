namespace KumikoUI.Core.Data;

/// <summary>Data for <see cref="KumikoIncrementalLoader.LoadMoreCompleted"/>: a batch finished or failed.</summary>
public sealed class KumikoLoadMoreCompletedEventArgs : EventArgs
{
    internal KumikoLoadMoreCompletedEventArgs(int itemsLoaded, int rowsAdded, KumikoLoadMoreState state, Exception? error)
    {
        ItemsLoaded = itemsLoaded;
        RowsAdded = rowsAdded;
        State = state;
        Error = error;
    }

    /// <summary>What <see cref="IKumikoIncrementalSource.LoadMoreItemsAsync"/> returned; 0 on failure.</summary>
    public int ItemsLoaded { get; }

    /// <summary>
    /// How many rows the grid gained: the row count after the batch was applied minus the row count before it, never
    /// below 0. It differs from <see cref="ItemsLoaded"/> when a filter hides new items or grouping adds group rows.
    /// </summary>
    public int RowsAdded { get; }

    /// <summary>
    /// The state the grid is now in: <see cref="KumikoLoadMoreState.Idle"/>, <see cref="KumikoLoadMoreState.Failed"/>
    /// or <see cref="KumikoLoadMoreState.Completed"/>, never <see cref="KumikoLoadMoreState.Loading"/>.
    /// </summary>
    public KumikoLoadMoreState State { get; }

    /// <summary>The exception that failed the batch, or null.</summary>
    public Exception? Error { get; }

    /// <summary>
    /// The message the error row shows for a failed batch. A handler sets it to turn <see cref="Error"/> into a
    /// user-facing message; null shows <see cref="Models.KumikoStrings.LoadMoreFailed"/>. It starts as the message of a
    /// <see cref="KumikoLoadMoreException"/>, and is ignored when the batch succeeded.
    /// </summary>
    public string? ErrorText { get; set; }
}

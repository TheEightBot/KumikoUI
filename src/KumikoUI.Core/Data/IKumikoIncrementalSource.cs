namespace KumikoUI.Core.Data;

/// <summary>
/// A collection (or a companion object) that can append more items to the grid's items on request: the contract of
/// incremental "load more" loading (#318). The grid's <see cref="KumikoIncrementalLoader"/> asks for the next batch
/// when the user nears the end of the loaded rows, draws a loading row while it waits, and offers a retry when the
/// batch fails.
/// </summary>
/// <remarks>
/// <para>
/// Items bound to the grid that implement this interface are used automatically; a separate object can be set through
/// <see cref="Models.KumikoSource.IncrementalSource"/> (or the hosts' <c>IncrementalSource</c> property).
/// <see cref="KumikoIncrementalCollection{T}"/> is a ready-made implementation.
/// </para>
/// <para>
/// <see cref="LoadMoreItemsAsync"/> is called on the UI thread, and the items must be appended to the bound collection
/// on the UI thread, raising <see cref="System.Collections.Specialized.INotifyCollectionChanged.CollectionChanged"/>
/// (the same contract as WinUI's <c>ISupportIncrementalLoading</c>). Await I/O without <c>ConfigureAwait(false)</c>,
/// then append. Appending a batch with one multi-item <c>Add</c> notification costs the grid one view change.
/// </para>
/// <para>
/// An implementation that also implements <see cref="System.ComponentModel.INotifyPropertyChanged"/> and raises it for
/// <see cref="HasMoreItems"/> lets a grid that reached the end (<see cref="KumikoLoadMoreState.Completed"/>) resume
/// loading when more items become available.
/// </para>
/// </remarks>
public interface IKumikoIncrementalSource
{
    /// <summary>
    /// Whether more items can be loaded. Read on the UI thread after every batch and whenever the grid evaluates
    /// whether to load (once per drawn frame), so it must be cheap.
    /// </summary>
    bool HasMoreItems { get; }

    /// <summary>
    /// Loads up to <paramref name="count"/> more items and appends them to the bound collection on the UI thread.
    /// </summary>
    /// <param name="count">How many items the grid asks for; a hint, not a limit.</param>
    /// <param name="cancellationToken">
    /// Canceled when the grid no longer wants the batch (its items were replaced or reset, it was detached or
    /// disposed). Honor it by throwing <see cref="OperationCanceledException"/> or by returning early.
    /// </param>
    /// <returns>The number of items appended.</returns>
    Task<int> LoadMoreItemsAsync(int count, CancellationToken cancellationToken);
}

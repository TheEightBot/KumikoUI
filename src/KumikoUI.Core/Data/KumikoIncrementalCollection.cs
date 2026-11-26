using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace KumikoUI.Core.Data;

/// <summary>
/// An <see cref="ObservableCollection{T}"/> that loads its items in batches for a grid's incremental loading (#318):
/// bind it as the grid's items, and the grid calls <see cref="LoadMoreItemsAsync"/> as the user nears the end. Each
/// batch comes from the fetch function given to the constructor and is appended with one notification
/// (<see cref="AddRange"/>), which costs the grid one view change.
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
/// <remarks>
/// <see cref="HasMoreItems"/> turns false when a fetch returns fewer items than were asked for. The fetch runs on the UI
/// thread and its result is appended there, so await I/O in it without <c>ConfigureAwait(false)</c> on the outermost
/// await (or simply return the task of an HTTP call). A multi-item <c>Add</c> notification is not supported by every
/// collection consumer (WPF's <c>CollectionView</c> rejects it); the Kumiko grids handle it.
/// </remarks>
public class KumikoIncrementalCollection<T> : ObservableCollection<T>, IKumikoIncrementalSource
{
    private static readonly PropertyChangedEventArgs s_hasMoreItemsChanged = new(nameof(HasMoreItems));
    private static readonly PropertyChangedEventArgs s_countChanged = new(nameof(Count));
    private static readonly PropertyChangedEventArgs s_indexerChanged = new("Item[]");

    private readonly Func<int, int, CancellationToken, Task<IReadOnlyList<T>>> _fetch;
    private bool _hasMoreItems;

    /// <summary>Creates an empty collection that loads its items with <paramref name="fetch"/>.</summary>
    /// <param name="fetch">
    /// Loads a batch: the index of the first item to load (the current <see cref="Collection{T}.Count"/>), how many to
    /// load, and a cancellation token; returns the items, fewer than asked for at the end of the data.
    /// </param>
    /// <param name="hasMoreItems">The initial <see cref="HasMoreItems"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="fetch"/> is <c>null</c>.</exception>
    public KumikoIncrementalCollection(Func<int, int, CancellationToken, Task<IReadOnlyList<T>>> fetch, bool hasMoreItems = true)
    {
        ArgumentNullException.ThrowIfNull(fetch);
        _fetch = fetch;
        _hasMoreItems = hasMoreItems;
    }

    /// <summary>
    /// Whether more items can be loaded. Turns false when a fetch returns fewer items than were asked for; set it to
    /// true again (the grid resumes loading) or false (the grid stops) yourself when you know better. Raises
    /// <see cref="ObservableCollection{T}.PropertyChanged"/>.
    /// </summary>
    public bool HasMoreItems
    {
        get => _hasMoreItems;
        set
        {
            if (_hasMoreItems == value) return;
            _hasMoreItems = value;
            OnPropertyChanged(s_hasMoreItemsChanged);
        }
    }

    /// <summary>
    /// Fetches up to <paramref name="count"/> items after the current ones and appends them with <see cref="AddRange"/>.
    /// </summary>
    /// <param name="count">How many items to fetch.</param>
    /// <param name="cancellationToken">Cancels the fetch; a canceled batch appends nothing.</param>
    /// <returns>The number of items appended.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public async Task<int> LoadMoreItemsAsync(int count, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var items = await _fetch(Count, count, cancellationToken) ?? Array.Empty<T>();
        cancellationToken.ThrowIfCancellationRequested();
        AddRange(items);
        HasMoreItems = items.Count >= count && count > 0;
        return items.Count;
    }

    /// <summary>
    /// Appends <paramref name="items"/> with one <see cref="NotifyCollectionChangedAction.Add"/> notification whose
    /// <see cref="NotifyCollectionChangedEventArgs.NewItems"/> holds all of them. Does nothing for an empty list.
    /// </summary>
    /// <param name="items">The items to append.</param>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is <c>null</c>.</exception>
    public void AddRange(IReadOnlyList<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0) return;
        CheckReentrancy();
        int start = Count;
        var added = new List<T>(items.Count);
        for (int i = 0; i < items.Count; i++)
        {
            Items.Add(items[i]);
            added.Add(items[i]);
        }
        OnPropertyChanged(s_countChanged);
        OnPropertyChanged(s_indexerChanged);
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, (IList)added, start));
    }

    /// <summary>
    /// Removes every item (one <see cref="NotifyCollectionChangedAction.Reset"/> notification, which also cancels a
    /// batch the grid has in flight) and sets <see cref="HasMoreItems"/> to true, so a grid bound to the collection loads
    /// it again from the start.
    /// </summary>
    /// <param name="cancellationToken">Not used; reserved for collections that refresh asynchronously.</param>
    /// <returns>A completed task.</returns>
    public Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        Clear();
        HasMoreItems = true;
        return Task.CompletedTask;
    }
}

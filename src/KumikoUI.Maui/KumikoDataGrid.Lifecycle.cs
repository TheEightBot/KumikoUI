using System.Collections.Specialized;

namespace KumikoUI.Maui;

/// <summary>
/// Teardown and re-attach of the collections the app can hand the grid (#59). A collection that outlives the
/// page, such as a view model's, holds a delegate to the grid for as long as the grid is subscribed to it, so
/// <c>Cleanup</c> removes every such subscription and <c>Reconnect</c> restores them.
/// </summary>
public partial class KumikoDataGrid
{
    /// <summary>
    /// True while the grid is subscribed to the collections it observes: from construction until the handler is
    /// disconnected, and again after it reconnects. While false, property changes that supply a new collection
    /// do not subscribe to it; <c>Reconnect</c> does.
    /// </summary>
    private bool IsObservingCollections => !_wasCleanedUp;

    /// <summary>
    /// Removes the <see cref="ItemsSource"/>, <see cref="Columns"/>, <see cref="TableSummaryRows"/>,
    /// <see cref="GroupDescriptions"/> and <see cref="GroupSummaryRows"/> subscriptions.
    /// </summary>
    private void UnsubscribeObservedCollections()
    {
        if (_boundCollectionChangedSource != null)
        {
            _boundCollectionChangedSource.CollectionChanged -= OnBoundItemsCollectionChanged;
            _boundCollectionChangedSource = null;
        }

        Columns.CollectionChanged -= OnColumnsCollectionChanged;
        TableSummaryRows.CollectionChanged -= OnTableSummaryRowsCollectionChanged;
        GroupDescriptions.CollectionChanged -= OnGroupDescriptionsCollectionChanged;
        GroupSummaryRows.CollectionChanged -= OnGroupSummaryRowsCollectionChanged;
    }

    /// <summary>
    /// Subscribes again to the collections <see cref="UnsubscribeObservedCollections"/> released, and copies a
    /// collection into the data source when it changed while the grid was disconnected. The bound items are
    /// re-synced by <c>KumikoSource.Attach</c>.
    /// </summary>
    private void ResubscribeObservedCollections()
    {
        // Subtract first so a repeated call never double-subscribes
        UnsubscribeObservedCollections();

        if (ItemsSource is INotifyCollectionChanged incc)
        {
            _boundCollectionChangedSource = incc;
            incc.CollectionChanged += OnBoundItemsCollectionChanged;
        }

        Columns.CollectionChanged += OnColumnsCollectionChanged;
        TableSummaryRows.CollectionChanged += OnTableSummaryRowsCollectionChanged;
        GroupDescriptions.CollectionChanged += OnGroupDescriptionsCollectionChanged;
        GroupSummaryRows.CollectionChanged += OnGroupSummaryRowsCollectionChanged;

        // Only what differs is copied, so returning to the page keeps group expansion and the like
        if (!Columns.SequenceEqual(_dataSource.Columns))
            _dataSource.SetColumns(Columns);
        if (!TableSummaryRows.SequenceEqual(_dataSource.TableSummaryRows))
            SyncTableSummaryRows(TableSummaryRows);
        if (!GroupDescriptions.SequenceEqual(_dataSource.GroupDescriptions))
            _dataSource.SetGroupDescriptions(GroupDescriptions);
        if (!GroupSummaryRows.SequenceEqual(_dataSource.GroupSummaryRows))
            _dataSource.SetGroupSummaryRows(GroupSummaryRows);
    }
}

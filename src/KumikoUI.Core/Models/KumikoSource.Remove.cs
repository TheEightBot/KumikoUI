using System.ComponentModel;

namespace KumikoUI.Core.Models;

// Removing items from the bound collection (#328).
public sealed partial class KumikoSource
{
    // True while RemoveItems takes items out of the collection: the per-item change notifications are skipped
    // (they only track and untrack items) and RemoveItems rebuilds the view once afterwards, as ReorderRow does.
    private bool _isRemoving;

    /// <summary>
    /// Removes <paramref name="items"/> from the bound collection (the grid's <c>ItemsSource</c>), whatever the
    /// current sort, filter or grouping shows, and rebuilds the view once: many items cause one
    /// <see cref="ViewChanged"/>, not one per item. Items are matched by reference; a <c>null</c> entry in
    /// <paramref name="items"/>, and an item that is not in the collection, are ignored, and <c>null</c> elements of
    /// the collection are left alone.
    /// <para>
    /// Nothing changes, and the result says why, when no item matched, or the items source is not a writable,
    /// resizable <see cref="System.Collections.IList"/> (a copy the grid made of an <see cref="System.Collections.IEnumerable"/>,
    /// a read-only list, an array).
    /// </para>
    /// <para>
    /// The selection and the current cell follow the view change: removed items leave the selection. To also move the
    /// current cell to a neighboring row and ask <see cref="Input.KumikoInputController.RowsDeleting"/> first, use
    /// <see cref="Input.KumikoInputController.DeleteSelectedRows"/>.
    /// </para>
    /// </summary>
    /// <param name="items">The data items to remove.</param>
    /// <returns><see cref="KumikoRemoveResult.Removed"/> when at least one item left the collection, otherwise the reason.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is <c>null</c>.</exception>
    public KumikoRemoveResult RemoveItems(IReadOnlyList<object> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        VerifyAccess();

        var wanted = new HashSet<object>(ReferenceEqualityComparer.Instance);
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] is { } item) wanted.Add(item);
        }
        if (wanted.Count == 0) return KumikoRemoveResult.NothingToRemove;
        if (_sourceItemsAreCopy || _sourceItems.IsReadOnly || _sourceItems.IsFixedSize)
            return KumikoRemoveResult.NotSupportedSource;

        // One pass finds where the items are; they are removed from the end so the earlier indices stay valid.
        var indices = new List<int>(wanted.Count);
        for (int i = 0; i < _sourceItems.Count; i++)
        {
            if (_sourceItems[i] is { } candidate && wanted.Contains(candidate)) indices.Add(i);
        }
        if (indices.Count == 0) return KumikoRemoveResult.NothingToRemove;

        var removed = new List<object>(indices.Count);
        _isRemoving = true;
        try
        {
            for (int i = indices.Count - 1; i >= 0; i--)
            {
                var item = _sourceItems[indices[i]]!;
                _sourceItems.RemoveAt(indices[i]);
                removed.Add(item);
            }
        }
        finally
        {
            _isRemoving = false;
            if (removed.Count > 0) AfterItemsRemoved(removed);
        }
        return removed.Count > 0 ? KumikoRemoveResult.Removed : KumikoRemoveResult.NothingToRemove;
    }

    /// <summary>
    /// Bookkeeping after <see cref="RemoveItems"/> took <paramref name="removed"/> out of the collection, then the one
    /// rebuild. A collection that is not observable raised nothing, so the item tracking and the cached indices are
    /// brought up to date here as well (all of it is idempotent for an observable one).
    /// </summary>
    private void AfterItemsRemoved(List<object> removed)
    {
        if (_observableCollection == null)
        {
            ContentVersion++;
            _collectionVersion++;
        }
        if (!_view.ItemsAreCopy) _view.SourceIndexByItem = null;
        foreach (var item in removed)
        {
            if (item is INotifyPropertyChanged inpc) UntrackItem(inpc);
            _rowLayout?.ForgetItem(item);
        }
        RequestRebuild();
    }
}

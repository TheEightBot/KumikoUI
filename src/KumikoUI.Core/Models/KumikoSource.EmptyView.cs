namespace KumikoUI.Core.Models;

/// <summary>What the empty view (#326) needs to know about the source: how many items it holds and whether a filter hides rows.</summary>
public partial class KumikoSource
{
    private int _itemCountCache;
    private int _itemCountCollectionVersion = -1;
    private int _itemCountViewVersion = -1;
    private int _itemCountSourceCount = -1;

    /// <summary>
    /// The number of non-null items in the bound collection, whatever the filters hide (null entries never become
    /// rows). It is counted again only when the collection, its size or the view changed, so reading it every frame
    /// is cheap. A non-observable list that is edited in place without a notification is noticed at the next rebuild.
    /// </summary>
    public int ItemCount
    {
        get
        {
            var items = _sourceItems;
            if (_itemCountCollectionVersion == _collectionVersion && _itemCountViewVersion == ViewVersion &&
                _itemCountSourceCount == items.Count)
                return _itemCountCache;

            int count = 0;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null) count++;
            }
            _itemCountCache = count;
            _itemCountCollectionVersion = _collectionVersion;
            _itemCountViewVersion = ViewVersion;
            _itemCountSourceCount = items.Count;
            return count;
        }
    }

    /// <summary>
    /// Whether anything can hide rows right now: a column filter that is active, a filter predicate
    /// (<see cref="SetFilter"/>), or a search that filters rows (<see cref="SearchFiltersRows"/> with a
    /// <see cref="SearchText"/>). Reading it allocates nothing.
    /// </summary>
    public bool IsFiltered
    {
        get
        {
            if (_filterPredicate != null || (_searchFiltersRows && _searchText.Length > 0)) return true;
            for (int i = 0; i < _columns.Count; i++)
            {
                if (_columns[i].ActiveFilter is { IsActive: true }) return true;
            }
            return false;
        }
    }

    /// <summary>Why the view has no rows: <see cref="KumikoEmptyViewKind.NoMatches"/> when items exist and a filter is active, else <see cref="KumikoEmptyViewKind.NoItems"/>.</summary>
    internal KumikoEmptyViewKind EmptyViewKind =>
        IsFiltered && ItemCount > 0 ? KumikoEmptyViewKind.NoMatches : KumikoEmptyViewKind.NoItems;
}

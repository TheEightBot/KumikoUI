using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using KumikoUI.Core.Data;

namespace KumikoUI.Core.Models;

/// <summary>
/// Manages the data that backs the grid: sorting, filtering, row access,
/// and change notification. Works with any IList or IEnumerable.
/// Automatically observes INotifyCollectionChanged and INotifyPropertyChanged
/// for dynamic data updates. Items may be of mixed runtime types; <c>null</c> items are skipped
/// (see <see cref="SetItems"/>).
/// </summary>
public class KumikoSource
{
    private IList _sourceItems = Array.Empty<object>();
    private List<int> _viewIndices = new();          // filtered + sorted mapping
    private List<FlatViewRow>? _flatView;            // null when no grouping; otherwise group headers + data rows
    private List<KumikoColumn> _columns = new();
    private Func<object, bool>? _filterPredicate;

    // ── Observable collection tracking ───────────────────────────
    private INotifyCollectionChanged? _observableCollection;
    private bool _isReordering;

    // ── Item property change tracking ────────────────────────────
    private readonly List<WeakReference<INotifyPropertyChanged>> _trackedItems = new();

    // ── Grouping state ───────────────────────────────────────────
    private readonly List<GroupDescription> _groupDescriptions = new();
    private readonly Dictionary<string, bool> _groupExpandedState = new();
    private bool _defaultGroupExpanded = true;

    // ── Summary state ────────────────────────────────────────────
    private readonly List<TableSummaryRow> _tableSummaryRows = new();
    private readonly List<GroupSummaryRow> _groupSummaryRows = new();
    private CaptionSummaryDescription? _captionSummary;

    // Computed caches (rebuilt with the view)
    private List<ComputedSummaryRow> _computedTopSummaries = new();
    private List<ComputedSummaryRow> _computedBottomSummaries = new();
    // Group summaries: keyed by group path
    private readonly Dictionary<string, List<ComputedSummaryRow>> _computedGroupSummaries = new();
    // Caption summaries: keyed by group path
    private readonly Dictionary<string, string> _computedCaptionSummaries = new();

    // ── View change notification ─────────────────────────────────
    private int _updateDepth;
    private bool _pendingRebuild;     // a state change inside BeginUpdate/EndUpdate deferred its rebuild
    private bool _pendingStructural;  // the view changed structurally inside BeginUpdate/EndUpdate
    private bool _pendingRedraw;
    private object?[]? _batchOldRowKeys;
    private object?[]? _rowKeys;      // the rows as last notified; maintained only while ViewChanged has listeners
    private EventHandler<KumikoViewChangedEventArgs>? _viewChanged;
    private bool _isMultiSortActive;

    // ── Comparison ───────────────────────────────────────────────
    private CultureInfo? _culture;
    private KumikoStrings _strings = KumikoStrings.Default;
    private CompareOptions _stringCompareOptions = CompareOptions.IgnoreCase;
    private KumikoComparer _comparer = KumikoComparer.Default;

    /// <summary>
    /// Raised exactly once for every change to the data or the view: <see cref="KumikoViewChangeKind.Redraw"/>
    /// when rows only need repainting, <see cref="KumikoViewChangeKind.Insert"/> /
    /// <see cref="KumikoViewChangeKind.Remove"/> for incremental collection changes, and
    /// <see cref="KumikoViewChangeKind.Rebuild"/> when the visible rows were rebuilt. Changes made between
    /// <see cref="BeginUpdate"/> and <see cref="EndUpdate"/> raise one notification when the batch ends.
    /// </summary>
    public event EventHandler<KumikoViewChangedEventArgs>? ViewChanged
    {
        add
        {
            // The first listener starts the row-key record that maps old rows to new ones.
            if (_viewChanged == null) _rowKeys = GetRowKeys();
            _viewChanged += value;
        }
        remove
        {
            _viewChanged -= value;
            if (_viewChanged == null) _rowKeys = null;
        }
    }

    /// <summary>Raised before sort columns change. Set Cancel=true to prevent.</summary>
    public event EventHandler<SortColumnsChangingEventArgs>? SortColumnsChanging;

    /// <summary>Raised after sort columns have changed.</summary>
    public event EventHandler<SortColumnsChangedEventArgs>? SortColumnsChanged;

    /// <summary>Raised before a filter changes. Set Cancel=true to prevent.</summary>
    public event EventHandler<FilterChangingEventArgs>? FilterChanging;

    /// <summary>Raised after a filter has changed.</summary>
    public event EventHandler<FilterChangedEventArgs>? FilterChanged;

    /// <summary>Raised before a group is expanded or collapsed. Set Cancel=true to prevent.</summary>
    public event EventHandler<GroupExpandingEventArgs>? GroupExpanding;

    /// <summary>Raised after a group header is expanded or collapsed by <see cref="ToggleGroupExpansion"/>.</summary>
    public event EventHandler<GroupExpandedChangedEventArgs>? GroupExpandedChanged;

    // ── Public API ───────────────────────────────────────────────

    /// <summary>
    /// Total number of visible rows after filtering/sorting/grouping.
    /// Includes group header rows when grouping is active.
    /// </summary>
    public int RowCount => _flatView?.Count ?? _viewIndices.Count;

    /// <summary>Whether grouping is currently active.</summary>
    public bool IsGroupingActive => _groupDescriptions.Count > 0;

    /// <summary>
    /// The culture for sorting, grouping, filtering and formatting cell values, or null (the default) for the
    /// current culture at the time of use. Setting it rebuilds the view.
    /// </summary>
    public CultureInfo? Culture
    {
        get => _culture;
        set
        {
            _culture = value;
            _comparer = new KumikoComparer(_culture, _stringCompareOptions);
            RequestRebuild();
        }
    }

    /// <summary>
    /// The text the grid shows on its own: the group panel label, the blank group header, default summary
    /// labels, the filter popup and edit validation messages. Defaults to <see cref="KumikoStrings.Default"/>
    /// (English); assigning null restores it. Setting it rebuilds the view, so text computed while grouping and
    /// summarizing follows.
    /// </summary>
    public KumikoStrings Strings
    {
        get => _strings;
        set
        {
            _strings = value ?? KumikoStrings.Default;
            RequestRebuild();
        }
    }

    /// <summary>
    /// How text compares when sorting, grouping and filtering (including the filter popup's value list and
    /// search). <see cref="CompareOptions.IgnoreCase"/> by default; use <see cref="CompareOptions.Ordinal"/>
    /// for culture-independent ordering. Setting it rebuilds the view.
    /// </summary>
    public CompareOptions StringCompareOptions
    {
        get => _stringCompareOptions;
        set
        {
            _stringCompareOptions = value;
            _comparer = new KumikoComparer(_culture, _stringCompareOptions);
            RequestRebuild();
        }
    }

    /// <summary>
    /// The comparer for cell values under <see cref="Culture"/> and <see cref="StringCompareOptions"/>: what
    /// sorting, group ordering, filters and Min/Max summaries use (unless a column sets
    /// <see cref="KumikoColumn.CustomComparer"/>).
    /// </summary>
    public KumikoComparer Comparer => _comparer;

    // ── Frozen rows ────────────────────────────────────────────
    private int _frozenRowCount;

    /// <summary>
    /// Number of top data rows that remain visible (frozen) when scrolling vertically.
    /// These rows are always displayed at the top of the data area, below the header and top summaries.
    /// When grouping is active, frozen rows are not applied (groups have their own expand/collapse behavior).
    /// </summary>
    public int FrozenRowCount
    {
        get => IsGroupingActive ? 0 : _frozenRowCount;
        set
        {
            int count = Math.Max(0, value);
            if (count == _frozenRowCount) return;
            _frozenRowCount = count;
            NotifyRedraw();
        }
    }

    /// <summary>
    /// The effective frozen row count, clamped to the actual number of rows.
    /// </summary>
    public int EffectiveFrozenRowCount => Math.Min(FrozenRowCount, RowCount);

    // ── Summary public API ───────────────────────────────────────

    /// <summary>The table-level summary row definitions.</summary>
    public IReadOnlyList<TableSummaryRow> TableSummaryRows => _tableSummaryRows;

    /// <summary>The group-level summary row definitions.</summary>
    public IReadOnlyList<GroupSummaryRow> GroupSummaryRows => _groupSummaryRows;

    /// <summary>Caption summary description (shown inline in group headers).</summary>
    public CaptionSummaryDescription? CaptionSummary
    {
        get => _captionSummary;
        set { _captionSummary = value; RequestRebuild(); }
    }

    /// <summary>Computed top-positioned table summary rows.</summary>
    public IReadOnlyList<ComputedSummaryRow> ComputedTopSummaries => _computedTopSummaries;

    /// <summary>Computed bottom-positioned table summary rows.</summary>
    public IReadOnlyList<ComputedSummaryRow> ComputedBottomSummaries => _computedBottomSummaries;

    /// <summary>Number of summary rows above the data area.</summary>
    public int TopSummaryCount => _computedTopSummaries.Count;

    /// <summary>Number of summary rows below the data area.</summary>
    public int BottomSummaryCount => _computedBottomSummaries.Count;

    /// <summary>Whether any group summary rows are defined.</summary>
    public bool HasGroupSummaries => _groupSummaryRows.Count > 0;

    /// <summary>Add a table-level summary row.</summary>
    public void AddTableSummaryRow(TableSummaryRow row)
    {
        _tableSummaryRows.Add(row);
        RequestRebuild();
    }

    /// <summary>Remove a table-level summary row by name.</summary>
    public void RemoveTableSummaryRow(string name)
    {
        _tableSummaryRows.RemoveAll(r => r.Name == name);
        RequestRebuild();
    }

    /// <summary>Clear all table-level summary rows.</summary>
    public void ClearTableSummaryRows()
    {
        _tableSummaryRows.Clear();
        RequestRebuild();
    }

    /// <summary>Add a group-level summary row.</summary>
    public void AddGroupSummaryRow(GroupSummaryRow row)
    {
        _groupSummaryRows.Add(row);
        RequestRebuild();
    }

    /// <summary>Remove a group-level summary row by name.</summary>
    public void RemoveGroupSummaryRow(string name)
    {
        _groupSummaryRows.RemoveAll(r => r.Name == name);
        RequestRebuild();
    }

    /// <summary>Clear all group-level summary rows.</summary>
    public void ClearGroupSummaryRows()
    {
        _groupSummaryRows.Clear();
        RequestRebuild();
    }

    /// <summary>
    /// Get computed group summaries for a specific group path.
    /// Returns null if no group summaries are defined or the path is unknown.
    /// </summary>
    public IReadOnlyList<ComputedSummaryRow>? GetGroupSummaries(string groupPath)
    {
        return _computedGroupSummaries.TryGetValue(groupPath, out var rows) ? rows : null;
    }

    /// <summary>
    /// Get computed caption summary text for a specific group path.
    /// Returns null if no caption summary is defined.
    /// </summary>
    public string? GetCaptionSummaryText(string groupPath)
    {
        return _computedCaptionSummaries.TryGetValue(groupPath, out var text) ? text : null;
    }

    /// <summary>The active group descriptions, in order.</summary>
    public IReadOnlyList<GroupDescription> GroupDescriptions => _groupDescriptions;

    /// <summary>Whether new groups default to expanded.</summary>
    public bool DefaultGroupExpanded
    {
        get => _defaultGroupExpanded;
        set
        {
            if (_defaultGroupExpanded == value) return;
            _defaultGroupExpanded = value;
            // Only groups without their own expanded state follow the default, and only groups show it.
            if (_groupDescriptions.Count > 0)
                RequestRebuild();
        }
    }

    /// <summary>
    /// The columns currently defined.
    /// </summary>
    public IReadOnlyList<KumikoColumn> Columns => _columns;

    /// <summary>
    /// Get all columns that are actively sorted, ordered by SortOrder.
    /// </summary>
    public IReadOnlyList<KumikoColumn> ActiveSortColumns =>
        _columns.Where(c => c.SortDirection != KumikoSortDirection.None && c.SortOrder > 0)
                .OrderBy(c => c.SortOrder)
                .ToList();

    /// <summary>
    /// Get all columns that have an active filter.
    /// </summary>
    public IReadOnlyList<FilterDescription> ActiveFilters =>
        _columns.Where(c => c.ActiveFilter != null && c.ActiveFilter.IsActive)
                .Select(c => c.ActiveFilter!)
                .ToList();

    /// <summary>
    /// Returns true if more than one column is currently sorted.
    /// </summary>
    public bool IsMultiSortActive => _isMultiSortActive;

    /// <summary>
    /// Set the backing data collection. Accepts IList, arrays, or List&lt;T&gt;.
    /// If the collection implements INotifyCollectionChanged, the grid will automatically
    /// update when items are added, removed, or replaced.
    /// If items implement INotifyPropertyChanged, individual cell updates are tracked.
    /// <para>
    /// Items may be of different runtime types (for example a <c>List&lt;Animal&gt;</c> of <c>Dog</c> and
    /// <c>Cat</c>): each column's property is resolved per item type, and an item that lacks the property
    /// reads <c>null</c> for that column.
    /// </para>
    /// <para>
    /// <c>null</c> entries are kept in the collection but never become rows. They are skipped exactly as if a
    /// filter rejected them: they are not sorted, grouped, counted or aggregated, are not offered by
    /// <see cref="GetUniqueColumnValues"/>, and are never tracked for property changes. <see cref="RowCount"/>,
    /// <see cref="GetItem"/> and <see cref="GetCellValue"/> therefore never see a null item.
    /// </para>
    /// </summary>
    public void SetItems(IEnumerable items)
    {
        // Unsubscribe from previous observable collection
        UnsubscribeFromCollection();
        UnsubscribeFromItemPropertyChanged();

        if (items is IList list)
            _sourceItems = list;
        else
            _sourceItems = items.Cast<object>().ToList();

        // Subscribe to INotifyCollectionChanged if supported
        if (items is INotifyCollectionChanged incc)
        {
            _observableCollection = incc;
            _observableCollection.CollectionChanged += OnSourceCollectionChanged;
        }

        // Subscribe to INotifyPropertyChanged on each item
        SubscribeToItemPropertyChanged();

        RequestRebuild();
    }

    // ── Observable collection support ────────────────────────────

    private void UnsubscribeFromCollection()
    {
        if (_observableCollection != null)
        {
            _observableCollection.CollectionChanged -= OnSourceCollectionChanged;
            _observableCollection = null;
        }
    }

    private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // During ReorderRow we do RemoveAt+Insert on the source list;
        // skip the intermediate rebuilds; ReorderRow rebuilds once afterwards.
        if (_isReordering)
        {
            // Still track/untrack item property changes
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Remove:
                    if (e.OldItems != null)
                        foreach (var item in e.OldItems)
                            if (item is INotifyPropertyChanged inpc)
                                UntrackItem(inpc);
                    break;
                case NotifyCollectionChangedAction.Add:
                    if (e.NewItems != null)
                        foreach (var item in e.NewItems)
                            if (item is INotifyPropertyChanged inpc)
                                TrackItem(inpc);
                    break;
            }
            return;
        }

        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (e.NewItems != null)
                {
                    foreach (var item in e.NewItems)
                        if (item is INotifyPropertyChanged inpc)
                            TrackItem(inpc);
                }
                break;

            case NotifyCollectionChangedAction.Remove:
                if (e.OldItems != null)
                {
                    foreach (var item in e.OldItems)
                        if (item is INotifyPropertyChanged inpc)
                            UntrackItem(inpc);
                }
                break;

            case NotifyCollectionChangedAction.Replace:
                if (e.OldItems != null)
                    foreach (var item in e.OldItems)
                        if (item is INotifyPropertyChanged inpc)
                            UntrackItem(inpc);
                if (e.NewItems != null)
                    foreach (var item in e.NewItems)
                        if (item is INotifyPropertyChanged inpc)
                            TrackItem(inpc);
                break;

            case NotifyCollectionChangedAction.Reset:
                UnsubscribeFromItemPropertyChanged();
                SubscribeToItemPropertyChanged();
                break;
        }

        // For simple Add/Remove with no active sort, filter, or grouping we can
        // do an incremental update instead of a full O(n) rebuild. Inside BeginUpdate/EndUpdate the view
        // still follows the collection at once (rows are read while rendering); only the event waits.
        // The collection has already changed, so the old rows come from the record kept since the last
        // notification, not from the (now stale) view.
        var oldRowKeys = _rowKeys;
        bool incremental = TryIncrementalUpdate(e, out int viewIndex, out bool rowsChanged);
        if (!incremental)
            RebuildCore();

        if (incremental && !rowsChanged)
            NotifyRedraw(); // the change is to an item no row shows (filtered out): the rows keep their keys
        else if (_updateDepth > 0)
            _pendingStructural = true;
        else if (_viewChanged != null)
        {
            var newRowKeys = GetRowKeys();
            RaiseStructural(newRowKeys, !incremental
                ? KumikoViewChangedEventArgs.Rebuild(oldRowKeys!, newRowKeys)
                : e.Action == NotifyCollectionChangedAction.Add
                    ? KumikoViewChangedEventArgs.Insert(viewIndex, 1, newRowKeys)
                    : KumikoViewChangedEventArgs.Remove(viewIndex, 1, newRowKeys));
        }
    }

    // ── Item property change support ─────────────────────────────

    private void SubscribeToItemPropertyChanged()
    {
        for (int i = 0; i < _sourceItems.Count; i++)
        {
            if (_sourceItems[i] is INotifyPropertyChanged inpc)
                TrackItem(inpc);
        }
    }

    private void UnsubscribeFromItemPropertyChanged()
    {
        foreach (var weakRef in _trackedItems)
        {
            if (weakRef.TryGetTarget(out var inpc))
                inpc.PropertyChanged -= OnItemPropertyChanged;
        }
        _trackedItems.Clear();
    }

    private void TrackItem(INotifyPropertyChanged inpc)
    {
        inpc.PropertyChanged += OnItemPropertyChanged;
        _trackedItems.Add(new WeakReference<INotifyPropertyChanged>(inpc));
    }

    private void UntrackItem(INotifyPropertyChanged inpc)
    {
        inpc.PropertyChanged -= OnItemPropertyChanged;
        _trackedItems.RemoveAll(wr => !wr.TryGetTarget(out var target) || ReferenceEquals(target, inpc));
    }

    /// <summary>
    /// Handles an item's <c>PropertyChanged</c>: a redraw, or a rebuild when the property drives sorting,
    /// grouping, filtering or a summary. A <c>null</c> or empty property name means every property changed.
    /// Names match a column, group or summary property ignoring case, as columns bind to properties that way;
    /// a change of <c>Address</c> also counts for the path <c>Address.City</c>. With a filter predicate set, any
    /// change may change its result, so any change rebuilds.
    /// Only the item itself is observed: a nested object must raise <c>PropertyChanged</c> through the root item
    /// (for example <c>Address</c>) for a change to <c>Address.City</c> to be seen.
    /// </summary>
    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (ItemPropertyAffectsView(e.PropertyName))
            RequestRebuild();
        else
            NotifyRedraw(); // just a display property change
    }

    /// <summary>Whether a change of <paramref name="propName"/> (null or empty: all) can change the visible rows or summaries.</summary>
    private bool ItemPropertyAffectsView(string? propName)
    {
        // All properties changed, or an opaque filter predicate that may read any of them
        if (string.IsNullOrEmpty(propName) || _filterPredicate != null)
            return true;

        // A sorted or filtered column (several columns can share a property)
        foreach (var col in _columns)
        {
            if (!PropertyPathAffected(col.PropertyName, propName)) continue;
            if (col.SortDirection != KumikoSortDirection.None || col.ActiveFilter is { IsActive: true })
                return true;
        }

        foreach (var group in _groupDescriptions)
            if (PropertyPathAffected(group.PropertyName, propName)) return true;

        // Also rebuild if the property is used in any summary aggregate
        foreach (var tsr in _tableSummaryRows)
            if (tsr.Columns.Any(c => PropertyPathAffected(c.PropertyName, propName))) return true;
        foreach (var gsr in _groupSummaryRows)
            if (gsr.Columns.Any(c => PropertyPathAffected(c.PropertyName, propName))) return true;
        return _captionSummary != null &&
               _captionSummary.Columns.Any(c => PropertyPathAffected(c.PropertyName, propName));
    }

    /// <summary>Whether a change of <paramref name="changed"/> affects the value at <paramref name="path"/>: the same property, or a parent of a dotted path.</summary>
    private static bool PropertyPathAffected(string path, string changed) =>
        path.Length >= changed.Length &&
        path.StartsWith(changed, StringComparison.OrdinalIgnoreCase) &&
        (path.Length == changed.Length || path[changed.Length] == '.');

    /// <summary>
    /// Replace the column definitions.
    /// </summary>
    public void SetColumns(IEnumerable<KumikoColumn> columns)
    {
        _columns = columns.ToList();
        UpdateSortCache();
        RequestRebuild(); // the columns carry sort and filter state and feed summaries
    }

    /// <summary>
    /// Reorder a column from one index to another.
    /// </summary>
    public void ReorderColumn(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= _columns.Count) return;
        if (toIndex < 0 || toIndex >= _columns.Count) return;
        if (fromIndex == toIndex) return;

        var col = _columns[fromIndex];
        _columns.RemoveAt(fromIndex);
        _columns.Insert(toIndex, col);
        NotifyRedraw();
    }

    /// <summary>
    /// Reorder a row from one visible index to another.
    /// Moves the item in both the view index mapping and the underlying source collection.
    /// Only supported when grouping is not active.
    /// </summary>
    public void ReorderRow(int fromVisibleRow, int toVisibleRow)
    {
        // Row drag is not supported when grouping is active (complex flat view)
        if (_flatView != null) return;
        if (fromVisibleRow < 0 || fromVisibleRow >= _viewIndices.Count) return;
        if (toVisibleRow < 0 || toVisibleRow >= _viewIndices.Count) return;
        if (fromVisibleRow == toVisibleRow) return;

        // Move in the source collection if it supports it
        int sourceFrom = _viewIndices[fromVisibleRow];
        int sourceTo = _viewIndices[toVisibleRow];

        if (_sourceItems is System.Collections.IList list)
        {
            _isReordering = true;
            try
            {
                var item = list[sourceFrom];
                list.RemoveAt(sourceFrom);
                list.Insert(sourceTo, item);
            }
            finally
            {
                _isReordering = false;
            }
        }

        // Rebuild the view to reflect the new order
        RequestRebuild();
    }

    /// <summary>
    /// Get a raw data item by visible-row index.
    /// Throws if the row is a group header.
    /// </summary>
    public object GetItem(int visibleRow)
    {
        if (_flatView != null)
        {
            if (visibleRow < 0 || visibleRow >= _flatView.Count)
                throw new ArgumentOutOfRangeException(nameof(visibleRow));
            var entry = _flatView[visibleRow];
            if (entry.Kind == FlatRowKind.GroupHeader)
                throw new InvalidOperationException("Cannot get a data item for a group header row.");
            return _sourceItems[entry.SourceIndex] ?? throw new InvalidOperationException("The view references a null item.");
        }

        if (visibleRow < 0 || visibleRow >= _viewIndices.Count)
            throw new ArgumentOutOfRangeException(nameof(visibleRow));
        return _sourceItems[_viewIndices[visibleRow]] ?? throw new InvalidOperationException("The view references a null item.");
    }

    /// <summary>
    /// Check whether a visible row is a group header.
    /// </summary>
    public bool IsGroupHeaderRow(int visibleRow)
    {
        if (_flatView == null) return false;
        if (visibleRow < 0 || visibleRow >= _flatView.Count) return false;
        return _flatView[visibleRow].Kind == FlatRowKind.GroupHeader;
    }

    /// <summary>
    /// Check whether a visible row is a group summary row.
    /// </summary>
    public bool IsGroupSummaryRow(int visibleRow)
    {
        if (_flatView == null) return false;
        if (visibleRow < 0 || visibleRow >= _flatView.Count) return false;
        return _flatView[visibleRow].Kind == FlatRowKind.GroupSummary;
    }

    /// <summary>
    /// Check whether a visible row is any non-data row (group header or group summary).
    /// </summary>
    public bool IsNonDataRow(int visibleRow)
    {
        return IsGroupHeaderRow(visibleRow) || IsGroupSummaryRow(visibleRow);
    }

    /// <summary>
    /// Get the computed group summary for a group summary row.
    /// Returns (groupPath, summaryRowIndex) or null.
    /// </summary>
    public (string GroupPath, int SummaryIndex)? GetGroupSummaryRowInfo(int visibleRow)
    {
        if (_flatView == null) return null;
        if (visibleRow < 0 || visibleRow >= _flatView.Count) return null;
        var entry = _flatView[visibleRow];
        if (entry.Kind != FlatRowKind.GroupSummary) return null;
        return (entry.GroupPath!, entry.GroupSummaryIndex);
    }

    /// <summary>
    /// Get group header info for a group header row.
    /// Returns null if the row is not a group header.
    /// </summary>
    public GroupHeaderInfo? GetGroupHeaderInfo(int visibleRow)
    {
        if (_flatView == null) return null;
        if (visibleRow < 0 || visibleRow >= _flatView.Count) return null;
        var entry = _flatView[visibleRow];
        return entry.Kind == FlatRowKind.GroupHeader ? entry.GroupInfo : null;
    }

    /// <summary>
    /// Toggle the expand/collapse state of a group header row.
    /// </summary>
    public void ToggleGroupExpansion(int visibleRow)
    {
        var info = GetGroupHeaderInfo(visibleRow);
        if (info == null) return;

        bool isExpanding = !info.IsExpanded;

        // Raise expanding event (cancelable)
        var expandingArgs = new GroupExpandingEventArgs(info, isExpanding);
        GroupExpanding?.Invoke(this, expandingArgs);
        if (expandingArgs.Cancel) return;

        // Toggle state
        _groupExpandedState[info.GroupPath] = isExpanding;
        RequestRebuild();

        var rebuilt = _flatView?.FirstOrDefault(r => r.Kind == FlatRowKind.GroupHeader && r.GroupInfo!.GroupPath == info.GroupPath)?.GroupInfo;
        GroupExpandedChanged?.Invoke(this, new GroupExpandedChangedEventArgs(rebuilt ?? info, isExpanding));
    }

    /// <summary>
    /// Expand all groups.
    /// </summary>
    public void ExpandAllGroups()
    {
        _defaultGroupExpanded = true;
        _groupExpandedState.Clear();
        RequestRebuild();
    }

    /// <summary>
    /// Collapse all groups.
    /// </summary>
    public void CollapseAllGroups()
    {
        _defaultGroupExpanded = false;
        _groupExpandedState.Clear();
        RequestRebuild();
    }

    /// <summary>
    /// Add a group description. Groups are applied in order (multi-level).
    /// </summary>
    public void AddGroupDescription(GroupDescription group)
    {
        _groupDescriptions.Add(group);
        RequestRebuild();
    }

    /// <summary>
    /// Insert a group description at a specific position.
    /// </summary>
    public void InsertGroupDescription(int index, GroupDescription group)
    {
        _groupDescriptions.Insert(index, group);
        RequestRebuild();
    }

    /// <summary>
    /// Remove a group description by property name.
    /// </summary>
    public void RemoveGroupDescription(string propertyName)
    {
        _groupDescriptions.RemoveAll(g => g.PropertyName == propertyName);
        _groupExpandedState.Clear();
        RequestRebuild();
    }

    /// <summary>
    /// Clear all group descriptions.
    /// </summary>
    public void ClearGroupDescriptions()
    {
        _groupDescriptions.Clear();
        _groupExpandedState.Clear();
        _flatView = null;
        RequestRebuild();
    }

    /// <summary>
    /// Get a cell value for a visible row and column.
    /// Returns null for group header rows.
    /// </summary>
    public object? GetCellValue(int visibleRow, KumikoColumn column)
    {
        if (IsGroupHeaderRow(visibleRow)) return null;
        return ReadColumnValue(GetItem(visibleRow), column);
    }

    /// <summary>
    /// Format a cell value as a display string.
    /// Returns empty for group header rows.
    /// </summary>
    public string GetCellDisplayText(int visibleRow, KumikoColumn column)
    {
        if (IsGroupHeaderRow(visibleRow)) return string.Empty;
        return FormatCellValue(GetCellValue(visibleRow, column), column);
    }

    /// <summary>
    /// Apply a filter predicate. Pass null to clear.
    /// </summary>
    public void SetFilter(Func<object, bool>? predicate)
    {
        if (_filterPredicate == null && predicate == null) return;
        _filterPredicate = predicate;
        RequestRebuild();
    }

    /// <summary>
    /// Set or clear a per-column filter. This is the primary filtering API.
    /// Pass null to clear the filter for the column.
    /// </summary>
    public void SetColumnFilter(KumikoColumn column, FilterDescription? filter)
    {
        if (!column.AllowFiltering) return;
        if (filter == null && column.ActiveFilter == null) return; // nothing to clear

        // Raise changing event (cancelable)
        var changingArgs = new FilterChangingEventArgs(column, filter);
        FilterChanging?.Invoke(this, changingArgs);
        if (changingArgs.Cancel) return;

        column.ActiveFilter = filter;
        RequestRebuild();

        // Raise changed event
        FilterChanged?.Invoke(this, new FilterChangedEventArgs(column, ActiveFilters));
    }

    /// <summary>
    /// Clear all column filters.
    /// </summary>
    public void ClearAllFilters()
    {
        if (_filterPredicate == null && _columns.All(c => c.ActiveFilter == null)) return;
        foreach (var col in _columns)
            col.ActiveFilter = null;
        _filterPredicate = null;
        RequestRebuild();
    }

    /// <summary>
    /// Get unique display values for a column (for Excel-style checkbox filter).
    /// Returns sorted distinct values as strings.
    /// </summary>
    public List<string> GetUniqueColumnValues(KumikoColumn column)
    {
        // The same text rules as filtering: values that compare equal are one entry.
        var values = new HashSet<string>(_comparer.TextComparer);
        var filters = PrepareFilters(column);
        for (int i = 0; i < _sourceItems.Count; i++)
        {
            if (_sourceItems[i] is not { } item)
                continue; // null items are skipped, like any item a filter rejects

            // Apply other column filters (exclude this column) so the value list
            // only shows values that are reachable with other filters applied
            if (!PassesFilters(item, filters))
                continue;

            var cellValue = GetCellValueDirect(item, column);
            string displayText = FormatCellValue(cellValue, column);
            values.Add(displayText);
        }

        var sorted = values.ToList();
        sorted.Sort(_comparer.CompareText);
        return sorted;
    }

    /// <summary>
    /// Sort by a single column (clears all other sort state). Steps through the column's first direction, the opposite
    /// direction and no sort (see <see cref="KumikoColumn.FirstSortDirection"/>).
    /// Does nothing when the column does not allow sorting.
    /// </summary>
    public void ToggleSort(KumikoColumn column)
    {
        if (!column.AllowSorting) return;
        ApplySort(column, NextDirection(column), 0, isMultiSort: false);
    }

    /// <summary>
    /// Toggle sort on a column in multi-sort mode.
    /// Adds the column to the existing sort chain or removes it if toggled to None.
    /// Does nothing when the column does not allow sorting.
    /// </summary>
    public void ToggleMultiSort(KumikoColumn column)
    {
        if (!column.AllowSorting) return;

        var direction = NextDirection(column);
        int order = column.SortDirection == KumikoSortDirection.None
            ? _columns.Count(c => c.SortDirection != KumikoSortDirection.None) + 1 // join at the end
            : column.SortOrder;                                                    // keep its place
        ApplySort(column, direction, order, isMultiSort: true);
    }

    /// <summary>
    /// Set a column's sort, the only way to change sort state. With <paramref name="order"/> 0 the column
    /// becomes the only sorted column; with an order of 1 or more it takes that place in the multi-column sort
    /// chain (1 = primary) and the other sorted columns keep their relative order. A direction of
    /// <see cref="KumikoSortDirection.None"/> removes the column from the sort. Raises
    /// <see cref="SortColumnsChanging"/> (cancelable) and <see cref="SortColumnsChanged"/>; unlike
    /// <see cref="ToggleSort"/> it ignores <see cref="KumikoColumn.AllowSorting"/>, which governs the user.
    /// </summary>
    /// <exception cref="ArgumentException">The column is not one of <see cref="Columns"/>.</exception>
    public void SetSort(KumikoColumn column, KumikoSortDirection direction, int order = 0)
    {
        ArgumentNullException.ThrowIfNull(column);
        if (!_columns.Contains(column))
            throw new ArgumentException("The column is not one of this source's columns.", nameof(column));
        // Removing a column (None) keeps the rest of the chain, whatever the order.
        ApplySort(column, direction, Math.Max(0, order), isMultiSort: order > 0 || direction == KumikoSortDirection.None);
    }

    /// <summary>
    /// Clear all sort state on all columns. Does not raise <see cref="SortColumnsChanging"/> or
    /// <see cref="SortColumnsChanged"/>.
    /// </summary>
    public void ClearSort()
    {
        if (_columns.All(c => c.SortDirection == KumikoSortDirection.None && c.SortOrder == 0)) return;
        foreach (var c in _columns)
        {
            c.SortDirection = KumikoSortDirection.None;
            c.SortOrder = 0;
        }
        UpdateSortCache();
        RequestRebuild();
    }

    /// <summary>
    /// The next step of a header tap: none, then the column's first direction, then the opposite one, then none
    /// again (see <see cref="KumikoColumn.FirstSortDirection"/>).
    /// </summary>
    private static KumikoSortDirection NextDirection(KumikoColumn column)
    {
        var first = column.FirstSortDirection;
        var current = column.SortDirection;
        if (current == KumikoSortDirection.None) return first;
        return current == first ? KumikoColumn.Opposite(first) : KumikoSortDirection.None;
    }

    private void ApplySort(KumikoColumn column, KumikoSortDirection direction, int order, bool isMultiSort)
    {
        // The chain without this column, in priority order.
        var chain = _columns
            .Where(c => c != column && c.SortDirection != KumikoSortDirection.None)
            .OrderBy(c => c.SortOrder)
            .ToList();
        if (!isMultiSort)
            chain.Clear();
        if (direction != KumikoSortDirection.None)
            chain.Insert(Math.Clamp(order == 0 ? 0 : order - 1, 0, chain.Count), column);

        bool unchanged = _columns.All(c =>
        {
            int index = chain.IndexOf(c);
            var expectedDirection = index < 0 ? KumikoSortDirection.None : c == column ? direction : c.SortDirection;
            return c.SortDirection == expectedDirection && c.SortOrder == index + 1;
        });
        if (unchanged) return;

        var changingArgs = new SortColumnsChangingEventArgs(column, isMultiSort, direction);
        SortColumnsChanging?.Invoke(this, changingArgs);
        if (changingArgs.Cancel) return;

        foreach (var c in _columns)
        {
            int index = chain.IndexOf(c);
            if (index < 0)
            {
                c.SortDirection = KumikoSortDirection.None;
                c.SortOrder = 0;
            }
            else
            {
                if (c == column) c.SortDirection = direction;
                c.SortOrder = index + 1;
            }
        }
        UpdateSortCache();
        RequestRebuild();

        SortColumnsChanged?.Invoke(this, new SortColumnsChangedEventArgs(column, ActiveSortColumns));
    }

    private void UpdateSortCache() =>
        _isMultiSortActive = _columns.Count(c => c.SortDirection != KumikoSortDirection.None) > 1;

    // ── Batching and view change notification ───────────────────

    /// <summary>
    /// Start a batch: changes until the matching <see cref="EndUpdate"/> raise a single
    /// <see cref="ViewChanged"/>, and sort, filter, grouping and summary changes rebuild the view once.
    /// Batches nest. Collection changes still update the view immediately, so rows stay readable.
    /// </summary>
    public void BeginUpdate()
    {
        if (_updateDepth++ > 0) return;
        _pendingRebuild = _pendingStructural = _pendingRedraw = false;
        _batchOldRowKeys = _rowKeys;
    }

    /// <summary>
    /// End a batch started with <see cref="BeginUpdate"/>. The outermost call applies a deferred rebuild and
    /// raises one <see cref="ViewChanged"/>: <see cref="KumikoViewChangeKind.Rebuild"/> if the rows changed,
    /// otherwise <see cref="KumikoViewChangeKind.Redraw"/> if anything needs repainting.
    /// </summary>
    /// <exception cref="InvalidOperationException">No batch is open.</exception>
    public void EndUpdate()
    {
        if (_updateDepth == 0)
            throw new InvalidOperationException("EndUpdate was called without a matching BeginUpdate.");
        if (--_updateDepth > 0) return;

        var oldRowKeys = _batchOldRowKeys;
        _batchOldRowKeys = null;
        if (_pendingRebuild)
            RebuildCore();

        bool structural = _pendingRebuild || _pendingStructural;
        bool redraw = _pendingRedraw;
        _pendingRebuild = _pendingStructural = _pendingRedraw = false;

        if (_viewChanged == null) return;
        if (structural)
        {
            var newRowKeys = GetRowKeys();
            RaiseStructural(newRowKeys, KumikoViewChangedEventArgs.Rebuild(oldRowKeys ?? _rowKeys!, newRowKeys));
        }
        else if (redraw)
        {
            NotifyRedraw();
        }
    }

    /// <summary>Rebuild the view now and raise <see cref="KumikoViewChangeKind.Rebuild"/>, or defer both in a batch.</summary>
    private void RequestRebuild()
    {
        if (_updateDepth > 0)
        {
            _pendingRebuild = true;
            return;
        }

        RebuildCore();
        if (_viewChanged != null)
        {
            var newRowKeys = GetRowKeys();
            RaiseStructural(newRowKeys, KumikoViewChangedEventArgs.Rebuild(_rowKeys!, newRowKeys));
        }
    }

    /// <summary>Raise <see cref="KumikoViewChangeKind.Redraw"/>, or note it for the end of a batch.</summary>
    private void NotifyRedraw()
    {
        if (_updateDepth > 0)
            _pendingRedraw = true;
        else if (_viewChanged != null && _rowKeys != null)
            _viewChanged.Invoke(this, KumikoViewChangedEventArgs.Redraw(_rowKeys));
    }

    /// <summary>Record the rows as now notified, then raise a structural change.</summary>
    private void RaiseStructural(object?[] newRowKeys, KumikoViewChangedEventArgs args)
    {
        _rowKeys = newRowKeys;
        _viewChanged?.Invoke(this, args);
    }

    /// <summary>
    /// The key of one visible row (see <see cref="GetRowKeys"/>), or null when the row is out of range.
    /// </summary>
    internal object? GetRowKey(int visibleRow)
    {
        if (_flatView == null)
            return visibleRow >= 0 && visibleRow < _viewIndices.Count ? _sourceItems[_viewIndices[visibleRow]] : null;
        if (visibleRow < 0 || visibleRow >= _flatView.Count) return null;
        var row = _flatView[visibleRow];
        return row.Kind switch
        {
            FlatRowKind.Data => _sourceItems[row.SourceIndex],
            FlatRowKind.GroupHeader => new GroupRowKey(row.GroupInfo!.GroupPath, -1),
            _ => new GroupRowKey(row.GroupPath!, row.GroupSummaryIndex),
        };
    }

    /// <summary>The visible row whose key matches <paramref name="key"/>, or -1. Scans the view.</summary>
    internal int IndexOfRowKey(object key)
    {
        int count = RowCount;
        for (int i = 0; i < count; i++)
        {
            if (GetRowKey(i) is { } candidate && RowKeyComparer.Instance.Equals(candidate, key))
                return i;
        }
        return -1;
    }

    /// <summary>
    /// The key of every visible row: the item for data rows, a <see cref="GroupRowKey"/> for group header and
    /// group summary rows.
    /// </summary>
    private object?[] GetRowKeys()
    {
        if (_flatView == null)
        {
            var keys = new object?[_viewIndices.Count];
            for (int i = 0; i < keys.Length; i++)
                keys[i] = _sourceItems[_viewIndices[i]];
            return keys;
        }

        var flatKeys = new object?[_flatView.Count];
        for (int i = 0; i < flatKeys.Length; i++)
        {
            var row = _flatView[i];
            flatKeys[i] = row.Kind switch
            {
                FlatRowKind.Data => _sourceItems[row.SourceIndex],
                FlatRowKind.GroupHeader => new GroupRowKey(row.GroupInfo!.GroupPath, -1),
                _ => new GroupRowKey(row.GroupPath!, row.GroupSummaryIndex),
            };
        }
        return flatKeys;
    }

    /// <summary>
    /// Force a full rebuild of the sorted/filtered view.
    /// </summary>
    public void Refresh()
    {
        RequestRebuild();
    }

    /// <summary>
    /// Set a cell value for a visible row and column, through <see cref="KumikoColumn.ValueSetter"/> when
    /// it is set and through the column's property path otherwise (converting the value to the property
    /// type in <see cref="Culture"/>). Returns whether the value was written: a value that cannot be
    /// written (read-only or unknown property, null intermediate value, failed conversion, a setter that
    /// throws) leaves the item unchanged and raises no change, and so does a row that is out of range or not
    /// a data row.
    /// </summary>
    public bool SetCellValue(int visibleRow, KumikoColumn column, object? value)
    {
        if (visibleRow < 0 || visibleRow >= RowCount || IsNonDataRow(visibleRow)) return false;
        return TrySetItemValue(GetItem(visibleRow), column, value, out _);
    }

    /// <summary>
    /// Write a column's value to <paramref name="item"/>, wherever (or whether) its row is shown, converting
    /// it to the property type in <see cref="Culture"/>. Returns false, with the reason in
    /// <paramref name="error"/> when there is one, if the value does not convert, the setter throws, or the
    /// property cannot be written; the item is then unchanged and no change is reported.
    /// </summary>
    internal bool TrySetItemValue(object item, KumikoColumn column, object? value, out string? error)
    {
        error = null;
        // One notification for the edit, even when the item's PropertyChanged triggers its own.
        BeginUpdate();
        try
        {
            bool written;
            if (column.ValueSetter != null)
            {
                try
                {
                    column.ValueSetter(item, value);
                    written = true;
                }
                catch (Exception ex) when (ex is not (OutOfMemoryException or InsufficientExecutionStackException))
                {
                    error = ex.Message;
                    written = false;
                }
            }
            else
            {
                written = column.ResolvePath(item.GetType()).TrySetValue(item, value, _comparer.Culture, _strings, out error);
            }

            if (written)
                NotifyRedraw();
            return written;
        }
        finally
        {
            EndUpdate();
        }
    }

    // ── Internal ─────────────────────────────────────────────────

    /// <summary>A column filter ready to evaluate: its value list as a set under the current text rules.</summary>
    private readonly record struct PreparedFilter(KumikoColumn Column, FilterDescription Filter, ISet<string>? Values);

    /// <summary>The active column filters (except <paramref name="exclude"/>), prepared for one pass over the items.</summary>
    private List<PreparedFilter> PrepareFilters(KumikoColumn? exclude = null)
    {
        var prepared = new List<PreparedFilter>();
        foreach (var col in _columns)
        {
            if (col == exclude || col.ActiveFilter is not { IsActive: true } filter) continue;
            ISet<string>? values = filter.SelectedValues is { } selected
                ? new HashSet<string>(selected, _comparer.TextComparer)
                : null;
            prepared.Add(new PreparedFilter(col, filter, values));
        }
        return prepared;
    }

    /// <summary>
    /// Check if an item passes the global filter predicate and the prepared per-column filters.
    /// </summary>
    private bool PassesFilters(object item, List<PreparedFilter> filters)
    {
        if (_filterPredicate != null && !_filterPredicate(item))
            return false;

        foreach (var (col, filter, values) in filters)
        {
            var cellValue = GetCellValueDirect(item, col);
            string displayText = FormatCellValue(cellValue, col);
            if (!filter.Evaluate(cellValue, displayText, values, _comparer))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Get a cell value directly from an item (bypasses view indices).
    /// </summary>
    private static object? GetCellValueDirect(object item, KumikoColumn column) => ReadColumnValue(item, column);

    /// <summary>
    /// Read a column's value from an item: <see cref="KumikoColumn.ValueAccessor"/> when it is set,
    /// otherwise the column's property path (an empty path is the item itself).
    /// </summary>
    private static object? ReadColumnValue(object item, KumikoColumn column) =>
        column.ValueAccessor != null
            ? column.ValueAccessor(item)
            : column.ResolvePath(item.GetType()).GetValue(item);

    /// <summary>A reader for one column, for tight loops (sorting, aggregates).</summary>
    private static Func<object, object?> CreateColumnReader(KumikoColumn column) =>
        column.ValueAccessor ?? (item => column.ResolvePath(item.GetType()).GetValue(item));

    /// <summary>
    /// Format a cell value as display text: the column's <see cref="KumikoColumn.Format"/> in
    /// <see cref="Culture"/> for formattable values, otherwise <see cref="object.ToString"/>; empty for null.
    /// </summary>
    public string FormatCellValue(object? value, KumikoColumn column) => _comparer.Format(value, column.Format);

    /// <summary>
    /// Attempts an incremental update of <c>_viewIndices</c> for a single-item Add or Remove event when no
    /// sort, grouping or summary is active. Column filters and the filter predicate do not prevent it: an added
    /// item is tested against them and gets a row only if it passes, and a removed item that has no row only
    /// shifts the source indices after it. Returns false if a full rebuild is needed.
    /// <paramref name="viewIndex"/> is the visible row that was inserted or removed; <paramref name="rowsChanged"/>
    /// is false when the view's rows are the same (the item was filtered out), though source indices moved.
    /// </summary>
    private bool TryIncrementalUpdate(NotifyCollectionChangedEventArgs e, out int viewIndex, out bool rowsChanged)
    {
        viewIndex = -1;
        rowsChanged = true;
        // Only handle single-item Add or Remove
        if (e.Action != NotifyCollectionChangedAction.Add &&
            e.Action != NotifyCollectionChangedAction.Remove)
            return false;

        // A null item is never a row, but it still shifts the source indices of the rows after it:
        // take the full rebuild, which keeps the view correct.
        if ((e.Action == NotifyCollectionChangedAction.Add ? e.NewItems : e.OldItems) is { } changed &&
            changed.Cast<object?>().Any(x => x is null))
            return false;

        // Cannot do incremental when sort, group, or summaries are active
        bool hasSort = _columns.Any(c => c.SortDirection != KumikoSortDirection.None);
        bool hasGroup = _groupDescriptions.Count > 0;
        bool hasSummaries = _tableSummaryRows.Count > 0 || _groupSummaryRows.Count > 0 ||
                            _captionSummary?.Columns.Count > 0;

        if (hasSort || hasGroup || hasSummaries)
            return false;

        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is { Count: 1 })
        {
            int sourceIndex = e.NewStartingIndex;
            if (sourceIndex < 0) return false; // Index not provided — fall back

            // The new item gets a row only if it passes the filter predicate and the column filters.
            bool visible = e.NewItems[0] is { } added && PassesFilters(added, PrepareFilters());

            // Shift existing view indices that are >= the inserted position
            for (int i = 0; i < _viewIndices.Count; i++)
            {
                if (_viewIndices[i] >= sourceIndex)
                    _viewIndices[i]++;
            }

            if (!visible)
            {
                rowsChanged = false;
                return true;
            }

            // No sort, so the new row goes at the insertion position: find where to insert in
            // _viewIndices to maintain source-order
            int insertAt = _viewIndices.Count;
            for (int i = 0; i < _viewIndices.Count; i++)
            {
                if (_viewIndices[i] > sourceIndex)
                {
                    insertAt = i;
                    break;
                }
            }
            _viewIndices.Insert(insertAt, sourceIndex);
            viewIndex = insertAt;

            return true;
        }

        if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems is { Count: 1 })
        {
            int sourceIndex = e.OldStartingIndex;
            if (sourceIndex < 0) return false; // Index not provided — fall back

            // Remove the entry (if the item had a row) and shift indices above the removed position
            viewIndex = _viewIndices.IndexOf(sourceIndex);
            if (viewIndex >= 0)
                _viewIndices.RemoveAt(viewIndex);
            else
                rowsChanged = false; // the item was filtered out
            for (int i = 0; i < _viewIndices.Count; i++)
            {
                if (_viewIndices[i] > sourceIndex)
                    _viewIndices[i]--;
            }

            return true;
        }

        return false;
    }

    /// <summary>A sort value for a source index; the view holds no null items, so null here is only defensive.</summary>
    private object? ReadSortValue(Func<object, object?> accessor, int sourceIndex) =>
        _sourceItems[sourceIndex] is { } item ? accessor(item) : null;

    /// <summary>Rebuild the visible rows from the source items and the current state. Raises nothing.</summary>
    private void RebuildCore()
    {
        _viewIndices.Clear();
        var filters = PrepareFilters();

        for (int i = 0; i < _sourceItems.Count; i++)
        {
            if (_sourceItems[i] is not { } item)
                continue; // null items never become rows
            if (!PassesFilters(item, filters))
                continue;
            _viewIndices.Add(i);
        }

        // Collect sorted columns in priority order
        var sortedColumns = _columns
            .Where(c => c.SortDirection != KumikoSortDirection.None && c.SortOrder > 0)
            .OrderBy(c => c.SortOrder)
            .ToList();

        // Fallback: if a column has a sort direction but no SortOrder, treat as single-sort
        if (sortedColumns.Count == 0)
        {
            var fallback = _columns.FirstOrDefault(c => c.SortDirection != KumikoSortDirection.None);
            if (fallback != null)
            {
                fallback.SortOrder = 1;
                sortedColumns.Add(fallback);
            }
        }

        if (sortedColumns.Count > 0 && _sourceItems.Count > 0)
        {
            // Pre-build readers and comparers for each sort column
            var sortSpecs = sortedColumns.Select(col => new
            {
                Accessor = CreateColumnReader(col),
                Comparer = col.CustomComparer ?? (IComparer)_comparer,
                Direction = col.SortDirection == KumikoSortDirection.Ascending ? 1 : -1
            }).ToList();

            _viewIndices.Sort((a, b) =>
            {
                foreach (var spec in sortSpecs)
                {
                    var va = ReadSortValue(spec.Accessor, a);
                    var vb = ReadSortValue(spec.Accessor, b);

                    int result;
                    if (va == null && vb == null) result = 0;
                    else if (va == null) result = -1 * spec.Direction;
                    else if (vb == null) result = 1 * spec.Direction;
                    else result = spec.Comparer.Compare(va, vb) * spec.Direction;

                    if (result != 0) return result;
                }
                return a.CompareTo(b); // stable: equal values keep their source order
            });
        }

        // Build grouped flat view if grouping is active
        if (_groupDescriptions.Count > 0)
        {
            _flatView = new List<FlatViewRow>();
            _computedGroupSummaries.Clear();
            _computedCaptionSummaries.Clear();
            BuildGroupedFlatView(_viewIndices, 0, "");
        }
        else
        {
            _flatView = null;
            _computedGroupSummaries.Clear();
            _computedCaptionSummaries.Clear();
        }

        // Compute table-level summaries from all visible data rows
        ComputeTableSummaries();
    }

    /// <summary>
    /// Recursively build the flat view by grouping source indices at the given level.
    /// </summary>
    private void BuildGroupedFlatView(List<int> sourceIndices, int level, string parentPath)
    {
        if (level >= _groupDescriptions.Count)
        {
            // No more group levels — add as data rows
            foreach (var idx in sourceIndices)
                _flatView!.Add(new FlatViewRow { Kind = FlatRowKind.Data, SourceIndex = idx });
            return;
        }

        var groupDesc = _groupDescriptions[level];
        var column = _columns.FirstOrDefault(c =>
            string.Equals(c.PropertyName, groupDesc.PropertyName, StringComparison.OrdinalIgnoreCase));

        string headerText = groupDesc.Header ?? column?.Header ?? groupDesc.PropertyName;

        // Group the indices by key
        var groups = new List<(object? Key, string DisplayText, List<int> Indices)>();
        // displayKey → index in groups list; display texts that compare equal under the text rules are one group
        var groupMap = new Dictionary<string, int>(_comparer.TextComparer);

        foreach (var sourceIdx in sourceIndices)
        {
            if (_sourceItems[sourceIdx] is not { } item)
                continue; // the view holds no null items; stay safe regardless
            object? rawValue;
            if (column != null)
            {
                rawValue = ReadColumnValue(item, column);
            }
            else
            {
                rawValue = null;
            }

            object? groupKey = groupDesc.KeySelector != null ? groupDesc.KeySelector(rawValue) : rawValue;
            string displayText = groupDesc.DisplayTextSelector != null
                ? groupDesc.DisplayTextSelector(groupKey)
                : groupKey is null
                    ? _strings.BlankGroupText
                    : _comparer.Format(groupKey, groupDesc.KeySelector == null ? column?.Format : null);

            // Use display text as dictionary key for grouping
            if (!groupMap.TryGetValue(displayText, out int groupIdx))
            {
                groupIdx = groups.Count;
                groupMap[displayText] = groupIdx;
                groups.Add((groupKey, displayText, new List<int>()));
            }
            groups[groupIdx].Indices.Add(sourceIdx);
        }

        // Order groups by their key (numbers numerically, dates chronologically), through the column's
        // comparer when it has one; equal keys fall back to display text, then first appearance.
        if (groupDesc.GroupSortDirection != KumikoSortDirection.None)
        {
            IComparer keyComparer = (groupDesc.KeySelector == null ? column?.CustomComparer : null) ?? _comparer;
            int direction = groupDesc.GroupSortDirection == KumikoSortDirection.Descending ? -1 : 1;
            var order = groups.Select((g, i) => (Group: g, Index: i)).ToList();
            order.Sort((a, b) =>
            {
                int result = keyComparer.Compare(a.Group.Key, b.Group.Key);
                if (result == 0) result = _comparer.CompareText(a.Group.DisplayText, b.Group.DisplayText);
                return result != 0 ? result * direction : a.Index.CompareTo(b.Index);
            });
            groups = order.Select(o => o.Group).ToList();
        }

        // Emit group headers + children
        foreach (var (key, displayText, indices) in groups)
        {
            string groupPath = string.IsNullOrEmpty(parentPath)
                ? $"{groupDesc.PropertyName}={displayText}"
                : $"{parentPath}|{groupDesc.PropertyName}={displayText}";

            bool isExpanded = _groupExpandedState.TryGetValue(groupPath, out bool state)
                ? state
                : _defaultGroupExpanded;

            var groupInfo = new GroupHeaderInfo
            {
                Level = level,
                Key = key,
                DisplayText = displayText,
                PropertyName = groupDesc.PropertyName,
                HeaderText = headerText,
                ItemCount = indices.Count,
                IsExpanded = isExpanded,
                GroupPath = groupPath
            };

            // Compute caption summary for this group header
            if (_captionSummary != null && _captionSummary.Columns.Count > 0)
            {
                string captionText = ComputeCaptionSummaryText(indices);
                _computedCaptionSummaries[groupPath] = captionText;
            }

            _flatView!.Add(new FlatViewRow
            {
                Kind = FlatRowKind.GroupHeader,
                GroupInfo = groupInfo
            });

            if (isExpanded)
            {
                BuildGroupedFlatView(indices, level + 1, groupPath);

                // Emit group summary rows after the group's data: at every level for rows set to All,
                // at the innermost level for rows set to Innermost
                var computedRows = _groupSummaryRows.Count > 0
                    ? ComputeGroupSummaries(indices, level == _groupDescriptions.Count - 1)
                    : null;
                if (computedRows is { Count: > 0 })
                {
                    _computedGroupSummaries[groupPath] = computedRows;

                    for (int si = 0; si < computedRows.Count; si++)
                    {
                        _flatView.Add(new FlatViewRow
                        {
                            Kind = FlatRowKind.GroupSummary,
                            GroupPath = groupPath,
                            GroupSummaryIndex = si
                        });
                    }
                }
            }
        }
    }

    // ── Summary computation ──────────────────────────────────────

    /// <summary>
    /// Compute table-level summaries (top/bottom) from all visible data rows.
    /// </summary>
    private void ComputeTableSummaries()
    {
        _computedTopSummaries.Clear();
        _computedBottomSummaries.Clear();

        if (_tableSummaryRows.Count == 0) return;

        foreach (var sumRow in _tableSummaryRows)
        {
            var computed = ComputeSummaryRow(sumRow.Name, sumRow.Title, sumRow.Columns, _viewIndices);
            if (sumRow.Position == SummaryPosition.Top)
                _computedTopSummaries.Add(computed);
            else
                _computedBottomSummaries.Add(computed);
        }
    }

    /// <summary>
    /// Compute group-level summaries for a set of source indices belonging to a group.
    /// </summary>
    private List<ComputedSummaryRow> ComputeGroupSummaries(List<int> sourceIndices, bool isInnermost)
    {
        var result = new List<ComputedSummaryRow>();
        foreach (var sumRow in _groupSummaryRows)
        {
            if (sumRow.Levels == GroupSummaryLevels.Innermost && !isInnermost) continue;
            result.Add(ComputeSummaryRow(sumRow.Name, sumRow.Title, sumRow.Columns, sourceIndices));
        }
        return result;
    }

    /// <summary>
    /// Compute caption summary text for a group's items.
    /// </summary>
    private string ComputeCaptionSummaryText(List<int> sourceIndices)
    {
        if (_captionSummary == null) return "";

        var summaryValues = new List<string>();
        var rawValues = new List<object?>();

        foreach (var colDesc in _captionSummary.Columns)
        {
            var values = CollectColumnValues(colDesc.PropertyName, sourceIndices);
            object? rawResult = ComputeAggregate(colDesc.SummaryType, values, colDesc.CustomAggregate);
            string formatted = FormatSummaryValue(rawResult, colDesc, _comparer.Culture);

            string label = colDesc.Label ?? _strings.SummaryLabel(colDesc.SummaryType);
            summaryValues.Add($"{label}{formatted}");
            rawValues.Add(rawResult);
        }

        if (_captionSummary.FormatTemplate != null)
        {
            try
            {
                // Format template uses {0}, {1}, etc. referencing formatted values
                return string.Format(_captionSummary.FormatTemplate, summaryValues.Cast<object>().ToArray());
            }
            catch
            {
                return string.Join(" | ", summaryValues);
            }
        }

        return string.Join(" | ", summaryValues);
    }

    /// <summary>
    /// Compute a single summary row from column descriptions and source indices. Every description is kept:
    /// several aggregates of one property show side by side in that property's cell.
    /// </summary>
    private ComputedSummaryRow ComputeSummaryRow(
        string name, string? title,
        List<SummaryColumnDescription> columnDescs,
        List<int> sourceIndices)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var rawValues = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var aggregates = new List<ComputedSummaryValue>(columnDescs.Count);
        var valuesByProperty = new Dictionary<string, List<object?>>(StringComparer.OrdinalIgnoreCase);

        foreach (var colDesc in columnDescs)
        {
            // Several aggregates of one property read its values once
            if (!valuesByProperty.TryGetValue(colDesc.PropertyName, out var colValues))
                valuesByProperty[colDesc.PropertyName] = colValues = CollectColumnValues(colDesc.PropertyName, sourceIndices);

            object? rawResult = ComputeAggregate(colDesc.SummaryType, colValues, colDesc.CustomAggregate);

            string label = colDesc.Label ?? _strings.SummaryLabel(colDesc.SummaryType);
            string formatted = FormatSummaryValue(rawResult, colDesc, _comparer.Culture);
            string text = $"{label}{formatted}";

            aggregates.Add(new ComputedSummaryValue
            {
                PropertyName = colDesc.PropertyName,
                SummaryType = colDesc.SummaryType,
                Label = label,
                Value = rawResult,
                Text = text
            });

            if (values.TryGetValue(colDesc.PropertyName, out var existing))
                values[colDesc.PropertyName] = existing + SummarySeparator + text;
            else
            {
                values[colDesc.PropertyName] = text;
                rawValues[colDesc.PropertyName] = rawResult;
            }
        }

        return new ComputedSummaryRow
        {
            Name = name,
            Title = title,
            Values = values,
            RawValues = rawValues,
            Aggregates = aggregates
        };
    }

    /// <summary>Separates the aggregates of one property in a summary cell.</summary>
    private const string SummarySeparator = "  |  ";

    /// <summary>
    /// Collect raw cell values for a property from a set of source indices. The property need not have a
    /// column; without one it is read from the items directly.
    /// </summary>
    private List<object?> CollectColumnValues(string propertyName, List<int> sourceIndices)
    {
        var column = _columns.FirstOrDefault(c =>
            string.Equals(c.PropertyName, propertyName, StringComparison.OrdinalIgnoreCase))
            ?? new KumikoColumn { PropertyName = propertyName };

        var result = new List<object?>(sourceIndices.Count);

        if (_sourceItems.Count == 0) return result;

        var reader = CreateColumnReader(column);
        foreach (var idx in sourceIndices)
        {
            if (_sourceItems[idx] is { } item)
                result.Add(reader(item));
        }
        return result;
    }

    /// <summary>
    /// Compute an aggregate value from a list of raw values.
    /// </summary>
    private object? ComputeAggregate(
        SummaryType summaryType,
        List<object?> values,
        Func<IReadOnlyList<object?>, object?>? customAggregate)
    {
        switch (summaryType)
        {
            case SummaryType.Count:
                return values.Count;

            case SummaryType.Sum:
            {
                SumNumbers(values, out bool exact, out var dec, out var dbl, out _);
                return exact ? dec : dbl;
            }

            case SummaryType.Average:
            {
                SumNumbers(values, out bool exact, out var dec, out var dbl, out int count);
                if (count == 0) return null;
                return exact ? dec / count : dbl / count;
            }

            case SummaryType.Min:
            {
                object? min = null;
                foreach (var v in values)
                {
                    if (v != null && (min == null || _comparer.Compare(v, min) < 0))
                        min = v;
                }
                return min;
            }

            case SummaryType.Max:
            {
                object? max = null;
                foreach (var v in values)
                {
                    if (v != null && (max == null || _comparer.Compare(v, max) > 0))
                        max = v;
                }
                return max;
            }

            case SummaryType.Custom:
                return customAggregate?.Invoke(values);

            default:
                return null;
        }
    }

    /// <summary>
    /// Totals the numeric values. When a <c>decimal</c> is present and no floating-point value is, the total is
    /// exact (<paramref name="exact"/>, in <paramref name="decimalSum"/>); otherwise it is in
    /// <paramref name="doubleSum"/>. Values that are not numbers (text, dates, booleans, other objects) are
    /// skipped by a type-code switch, so no exception is raised per value.
    /// </summary>
    private static void SumNumbers(List<object?> values, out bool exact, out decimal decimalSum,
        out double doubleSum, out int count)
    {
        decimal dec = 0;
        double dbl = 0;
        int n = 0;
        bool hasDecimal = false, hasFloat = false, decimalOverflow = false;

        foreach (var v in values)
        {
            if (v == null) continue;
            decimal m = 0;
            double d;
            switch (Type.GetTypeCode(v.GetType()))
            {
                case TypeCode.Decimal: m = (decimal)v; d = (double)m; hasDecimal = true; break;
                case TypeCode.Int32: m = (int)v; d = (int)v; break;
                case TypeCode.Int64: m = (long)v; d = (long)v; break;
                case TypeCode.Int16: m = (short)v; d = (short)v; break;
                case TypeCode.SByte: m = (sbyte)v; d = (sbyte)v; break;
                case TypeCode.Byte: m = (byte)v; d = (byte)v; break;
                case TypeCode.UInt16: m = (ushort)v; d = (ushort)v; break;
                case TypeCode.UInt32: m = (uint)v; d = (uint)v; break;
                case TypeCode.UInt64: m = (ulong)v; d = (ulong)v; break;
                case TypeCode.Double: d = (double)v; hasFloat = true; break;
                case TypeCode.Single: d = (float)v; hasFloat = true; break;
                default: continue; // not a number
            }

            n++;
            dbl += d;
            if (!decimalOverflow && !hasFloat)
            {
                try { dec = checked(dec + m); }
                catch (OverflowException) { decimalOverflow = true; }
            }
        }

        count = n;
        decimalSum = dec;
        doubleSum = dbl;
        exact = hasDecimal && !hasFloat && !decimalOverflow;
    }

    /// <summary>
    /// Format an aggregate result using the column description's format.
    /// </summary>
    private static string FormatSummaryValue(object? value, SummaryColumnDescription colDesc, CultureInfo culture)
    {
        if (value == null) return "";

        if (!string.IsNullOrEmpty(colDesc.Format))
        {
            // A composite format ("Max {0:N1}") goes through string.Format; anything else is a format string
            if (colDesc.Format.Contains("{0"))
            {
                try { return string.Format(culture, colDesc.Format, value); }
                catch { /* fall through */ }
            }
            // Otherwise use IFormattable
            if (value is IFormattable fmt)
                return fmt.ToString(colDesc.Format, culture);
        }

        return value is IFormattable plain ? plain.ToString(null, culture) : value.ToString() ?? "";
    }
}

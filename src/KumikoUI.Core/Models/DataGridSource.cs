using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace KumikoUI.Core.Models;

/// <summary>
/// Event args for the SortColumnsChanging event. Set Cancel=true to prevent the sort.
/// </summary>
public class SortColumnsChangingEventArgs : EventArgs
{
    /// <summary>The column whose sort state is being toggled.</summary>
    public DataGridColumn Column { get; }

    /// <summary>Whether this is a multi-sort operation (modifier key held).</summary>
    public bool IsMultiSort { get; }

    /// <summary>The new sort direction that will be applied.</summary>
    public SortDirection NewDirection { get; }

    /// <summary>Set to true to cancel the sort operation.</summary>
    public bool Cancel { get; set; }

    public SortColumnsChangingEventArgs(DataGridColumn column, bool isMultiSort, SortDirection newDirection)
    {
        Column = column;
        IsMultiSort = isMultiSort;
        NewDirection = newDirection;
    }
}

/// <summary>
/// Event args for the SortColumnsChanged event.
/// </summary>
public class SortColumnsChangedEventArgs : EventArgs
{
    /// <summary>The column whose sort state was toggled.</summary>
    public DataGridColumn Column { get; }

    /// <summary>All columns that currently have an active sort, ordered by SortOrder.</summary>
    public IReadOnlyList<DataGridColumn> ActiveSortColumns { get; }

    public SortColumnsChangedEventArgs(DataGridColumn column, IReadOnlyList<DataGridColumn> activeSortColumns)
    {
        Column = column;
        ActiveSortColumns = activeSortColumns;
    }
}

/// <summary>
/// Manages the data that backs the grid: sorting, filtering, row access,
/// and change notification. Works with any IList or IEnumerable.
/// Automatically observes INotifyCollectionChanged and INotifyPropertyChanged
/// for dynamic data updates.
/// </summary>
public class DataGridSource
{
    private IList _sourceItems = Array.Empty<object>();
    private List<int> _viewIndices = new();          // filtered + sorted mapping
    private List<FlatViewRow>? _flatView;            // null when no grouping; otherwise group headers + data rows
    private List<DataGridColumn> _columns = new();
    private readonly Dictionary<string, Func<object, object?>> _accessors = new();
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

    // Computed caches (rebuilt on RebuildView)
    private List<ComputedSummaryRow> _computedTopSummaries = new();
    private List<ComputedSummaryRow> _computedBottomSummaries = new();
    // Group summaries: keyed by group path
    private readonly Dictionary<string, List<ComputedSummaryRow>> _computedGroupSummaries = new();
    // Caption summaries: keyed by group path
    private readonly Dictionary<string, string> _computedCaptionSummaries = new();

    public event Action? DataChanged;

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

    /// <summary>Raised after a group is expanded or collapsed.</summary>
    public event EventHandler<GroupCollapsedEventArgs>? GroupCollapsed;

    // ── Public API ───────────────────────────────────────────────

    /// <summary>
    /// Total number of visible rows after filtering/sorting/grouping.
    /// Includes group header rows when grouping is active.
    /// </summary>
    public int RowCount => _flatView?.Count ?? _viewIndices.Count;

    /// <summary>Whether grouping is currently active.</summary>
    public bool IsGroupingActive => _groupDescriptions.Count > 0;

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
        set { _frozenRowCount = Math.Max(0, value); DataChanged?.Invoke(); }
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
        set { _captionSummary = value; RebuildView(); }
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
        RebuildView();
    }

    /// <summary>Remove a table-level summary row by name.</summary>
    public void RemoveTableSummaryRow(string name)
    {
        _tableSummaryRows.RemoveAll(r => r.Name == name);
        RebuildView();
    }

    /// <summary>Clear all table-level summary rows.</summary>
    public void ClearTableSummaryRows()
    {
        _tableSummaryRows.Clear();
        RebuildView();
    }

    /// <summary>Add a group-level summary row.</summary>
    public void AddGroupSummaryRow(GroupSummaryRow row)
    {
        _groupSummaryRows.Add(row);
        RebuildView();
    }

    /// <summary>Remove a group-level summary row by name.</summary>
    public void RemoveGroupSummaryRow(string name)
    {
        _groupSummaryRows.RemoveAll(r => r.Name == name);
        RebuildView();
    }

    /// <summary>Clear all group-level summary rows.</summary>
    public void ClearGroupSummaryRows()
    {
        _groupSummaryRows.Clear();
        RebuildView();
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
        set => _defaultGroupExpanded = value;
    }

    /// <summary>
    /// The columns currently defined.
    /// </summary>
    public IReadOnlyList<DataGridColumn> Columns => _columns;

    /// <summary>
    /// Get all columns that are actively sorted, ordered by SortOrder.
    /// </summary>
    public IReadOnlyList<DataGridColumn> ActiveSortColumns =>
        _columns.Where(c => c.SortDirection != SortDirection.None && c.SortOrder > 0)
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
    public bool IsMultiSortActive =>
        _columns.Count(c => c.SortDirection != SortDirection.None) > 1;

    /// <summary>
    /// Set the backing data collection. Accepts IList, arrays, or List&lt;T&gt;.
    /// If the collection implements INotifyCollectionChanged, the grid will automatically
    /// update when items are added, removed, or replaced.
    /// If items implement INotifyPropertyChanged, individual cell updates are tracked.
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

        _accessors.Clear();

        // Subscribe to INotifyCollectionChanged if supported
        if (items is INotifyCollectionChanged incc)
        {
            _observableCollection = incc;
            _observableCollection.CollectionChanged += OnSourceCollectionChanged;
        }

        // Subscribe to INotifyPropertyChanged on each item
        SubscribeToItemPropertyChanged();

        RebuildView();
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
        // suppress the intermediate RebuildView calls for efficiency.
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

        // Note: we do NOT clear _accessors here. The compiled expression-tree
        // delegates are keyed by "{type.FullName}.{propertyPath}" and remain
        // valid across add/remove/replace operations (the item type doesn't change).
        // Clearing them here would force expensive recompilation on every INCC event.

        // For simple Add/Remove with no active sort, filter, or grouping we can
        // do an incremental update instead of a full O(n) rebuild.
        if (TryIncrementalUpdate(e))
        {
            DataChanged?.Invoke();
        }
        else
        {
            RebuildView();
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
    /// Raised when an item property changes. Fires DataChanged so the grid redraws.
    /// For non-structural changes we just notify without a full rebuild unless the
    /// changed property is used for sorting, grouping, or filtering.
    /// </summary>
    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        string? propName = e.PropertyName;

        // Check if the changed property is a sort, group, filter, or summary key — if so, full rebuild
        bool needsRebuild = false;
        if (propName != null)
        {
            foreach (var col in _columns)
            {
                if (col.PropertyName == propName)
                {
                    if (col.SortDirection != SortDirection.None ||
                        col.ActiveFilter != null ||
                        _groupDescriptions.Any(g => g.PropertyName == propName))
                    {
                        needsRebuild = true;
                    }
                    break;
                }
            }

            // Also rebuild if the property is used in any summary aggregate
            if (!needsRebuild)
            {
                foreach (var tsr in _tableSummaryRows)
                    if (tsr.Columns.Any(c => c.PropertyName == propName))
                    { needsRebuild = true; break; }
            }
            if (!needsRebuild)
            {
                foreach (var gsr in _groupSummaryRows)
                    if (gsr.Columns.Any(c => c.PropertyName == propName))
                    { needsRebuild = true; break; }
            }
            if (!needsRebuild && _captionSummary != null)
            {
                if (_captionSummary.Columns.Any(c => c.PropertyName == propName))
                    needsRebuild = true;
            }
        }
        else
        {
            // PropertyName is null — treat as full change
            needsRebuild = true;
        }

        if (needsRebuild)
        {
            // Note: we do NOT clear _accessors here. Property value changes
            // don't alter the item type, so compiled accessors remain valid.
            RebuildView();
        }
        else
        {
            // Just a display property change — redraw without rebuilding
            DataChanged?.Invoke();
        }
    }

    /// <summary>
    /// Replace the column definitions.
    /// </summary>
    public void SetColumns(IEnumerable<DataGridColumn> columns)
    {
        _columns = columns.ToList();
        _accessors.Clear();
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
        DataChanged?.Invoke();
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
        RebuildView();
        DataChanged?.Invoke();
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
            return _sourceItems[entry.SourceIndex]!;
        }

        if (visibleRow < 0 || visibleRow >= _viewIndices.Count)
            throw new ArgumentOutOfRangeException(nameof(visibleRow));
        return _sourceItems[_viewIndices[visibleRow]]!;
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
        RebuildView();

        // Raise collapsed event
        GroupCollapsed?.Invoke(this, new GroupCollapsedEventArgs(info, isExpanding));
    }

    /// <summary>
    /// Expand all groups.
    /// </summary>
    public void ExpandAllGroups()
    {
        _defaultGroupExpanded = true;
        _groupExpandedState.Clear();
        RebuildView();
    }

    /// <summary>
    /// Collapse all groups.
    /// </summary>
    public void CollapseAllGroups()
    {
        _defaultGroupExpanded = false;
        _groupExpandedState.Clear();
        RebuildView();
    }

    /// <summary>
    /// Add a group description. Groups are applied in order (multi-level).
    /// </summary>
    public void AddGroupDescription(GroupDescription group)
    {
        _groupDescriptions.Add(group);
        RebuildView();
    }

    /// <summary>
    /// Insert a group description at a specific position.
    /// </summary>
    public void InsertGroupDescription(int index, GroupDescription group)
    {
        _groupDescriptions.Insert(index, group);
        RebuildView();
    }

    /// <summary>
    /// Remove a group description by property name.
    /// </summary>
    public void RemoveGroupDescription(string propertyName)
    {
        _groupDescriptions.RemoveAll(g => g.PropertyName == propertyName);
        _groupExpandedState.Clear();
        RebuildView();
    }

    /// <summary>
    /// Clear all group descriptions.
    /// </summary>
    public void ClearGroupDescriptions()
    {
        _groupDescriptions.Clear();
        _groupExpandedState.Clear();
        _flatView = null;
        RebuildView();
    }

    /// <summary>
    /// Get a cell value for a visible row and column.
    /// Returns null for group header rows.
    /// </summary>
    [RequiresUnreferencedCode("Accesses properties on data item types by name. Ensure the public properties of your data types are preserved when trimming.")]
    public object? GetCellValue(int visibleRow, DataGridColumn column)
    {
        if (IsGroupHeaderRow(visibleRow)) return null;
        var item = GetItem(visibleRow);
        var accessor = GetOrCreateAccessor(item.GetType(), column.PropertyName);
        return accessor(item);
    }

    /// <summary>
    /// Format a cell value as a display string.
    /// Returns empty for group header rows.
    /// </summary>
    [RequiresUnreferencedCode("Accesses properties on data item types by name. Ensure the public properties of your data types are preserved when trimming.")]
    public string GetCellDisplayText(int visibleRow, DataGridColumn column)
    {
        if (IsGroupHeaderRow(visibleRow)) return string.Empty;
        var value = GetCellValue(visibleRow, column);
        if (value is null) return string.Empty;
        if (!string.IsNullOrEmpty(column.Format) && value is IFormattable fmt)
            return fmt.ToString(column.Format, null);
        return value.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Apply a filter predicate. Pass null to clear.
    /// </summary>
    public void SetFilter(Func<object, bool>? predicate)
    {
        _filterPredicate = predicate;
        RebuildView();
    }

    /// <summary>
    /// Set or clear a per-column filter. This is the primary filtering API.
    /// Pass null to clear the filter for the column.
    /// </summary>
    public void SetColumnFilter(DataGridColumn column, FilterDescription? filter)
    {
        if (!column.AllowFiltering) return;

        // Raise changing event (cancelable)
        var changingArgs = new FilterChangingEventArgs(column, filter);
        FilterChanging?.Invoke(this, changingArgs);
        if (changingArgs.Cancel) return;

        column.ActiveFilter = filter;
        RebuildView();

        // Raise changed event
        FilterChanged?.Invoke(this, new FilterChangedEventArgs(column, ActiveFilters));
    }

    /// <summary>
    /// Clear all column filters.
    /// </summary>
    public void ClearAllFilters()
    {
        foreach (var col in _columns)
            col.ActiveFilter = null;
        _filterPredicate = null;
        RebuildView();
    }

    /// <summary>
    /// Get unique display values for a column (for Excel-style checkbox filter).
    /// Returns sorted distinct values as strings.
    /// </summary>
    public List<string> GetUniqueColumnValues(DataGridColumn column)
    {
        var values = new HashSet<string>();
        for (int i = 0; i < _sourceItems.Count; i++)
        {
            var item = _sourceItems[i]!;

            // Apply other column filters (exclude this column) so the value list
            // only shows values that are reachable with other filters applied
            if (!PassesOtherFilters(item, column))
                continue;

            var cellValue = GetCellValueDirect(item, column);
            string displayText = FormatCellValue(cellValue, column);
            values.Add(displayText);
        }

        var sorted = values.ToList();
        sorted.Sort(StringComparer.OrdinalIgnoreCase);
        return sorted;
    }

    /// <summary>
    /// Sort by a single column (clears all other sort state). Toggle direction automatically.
    /// </summary>
    public void ToggleSort(DataGridColumn column)
    {
        if (!column.AllowSorting) return;

        var newDirection = column.SortDirection switch
        {
            SortDirection.None => SortDirection.Ascending,
            SortDirection.Ascending => SortDirection.Descending,
            SortDirection.Descending => SortDirection.None,
            _ => SortDirection.None
        };

        // Raise changing event (cancelable)
        var changingArgs = new SortColumnsChangingEventArgs(column, false, newDirection);
        SortColumnsChanging?.Invoke(this, changingArgs);
        if (changingArgs.Cancel) return;

        // Reset all columns
        foreach (var c in _columns)
        {
            c.SortDirection = SortDirection.None;
            c.SortOrder = 0;
        }

        column.SortDirection = newDirection;
        column.SortOrder = newDirection != SortDirection.None ? 1 : 0;

        RebuildView();

        // Raise changed event
        SortColumnsChanged?.Invoke(this, new SortColumnsChangedEventArgs(column, ActiveSortColumns));
    }

    /// <summary>
    /// Toggle sort on a column in multi-sort mode.
    /// Adds the column to the existing sort chain or removes it if toggled to None.
    /// </summary>
    public void ToggleMultiSort(DataGridColumn column)
    {
        if (!column.AllowSorting) return;

        var newDirection = column.SortDirection switch
        {
            SortDirection.None => SortDirection.Ascending,
            SortDirection.Ascending => SortDirection.Descending,
            SortDirection.Descending => SortDirection.None,
            _ => SortDirection.None
        };

        // Raise changing event (cancelable)
        var changingArgs = new SortColumnsChangingEventArgs(column, true, newDirection);
        SortColumnsChanging?.Invoke(this, changingArgs);
        if (changingArgs.Cancel) return;

        if (newDirection == SortDirection.None)
        {
            // Remove from multi-sort and recompact orders
            int removedOrder = column.SortOrder;
            column.SortDirection = SortDirection.None;
            column.SortOrder = 0;

            // Recompact: shift down orders above the removed one
            foreach (var c in _columns)
            {
                if (c.SortOrder > removedOrder)
                    c.SortOrder--;
            }
        }
        else if (column.SortDirection == SortDirection.None)
        {
            // Adding new column to multi-sort: assign next order
            int maxOrder = 0;
            foreach (var c in _columns)
                if (c.SortOrder > maxOrder) maxOrder = c.SortOrder;

            column.SortDirection = newDirection;
            column.SortOrder = maxOrder + 1;
        }
        else
        {
            // Changing direction of an already-sorted column: keep its order
            column.SortDirection = newDirection;
        }

        RebuildView();

        // Raise changed event
        SortColumnsChanged?.Invoke(this, new SortColumnsChangedEventArgs(column, ActiveSortColumns));
    }

    /// <summary>
    /// Clear all sort state on all columns.
    /// </summary>
    public void ClearSort()
    {
        foreach (var c in _columns)
        {
            c.SortDirection = SortDirection.None;
            c.SortOrder = 0;
        }
        RebuildView();
    }

    /// <summary>
    /// Force a full rebuild of the sorted/filtered view.
    /// </summary>
    public void Refresh()
    {
        RebuildView();
    }

    /// <summary>
    /// Set a cell value for a visible row and column via reflection.
    /// </summary>
    [RequiresUnreferencedCode("Accesses properties on data item types by name. Ensure the public properties of your data types are preserved when trimming.")]
    public void SetCellValue(int visibleRow, DataGridColumn column, object? value)
    {
        var item = GetItem(visibleRow);
        SetPropertyValue(item, column.PropertyName, value);
        DataChanged?.Invoke();
    }

    /// <summary>
    /// Set a property value on an object using reflection. Supports dotted paths.
    /// </summary>
    [RequiresUnreferencedCode("Accesses properties on data item types by name. Ensure the public properties of your data types are preserved when trimming.")]
    private static void SetPropertyValue(object target, string propertyPath, object? value)
    {
        var parts = propertyPath.Split('.');
        object current = target;

        // Navigate to the parent object for nested paths
        for (int i = 0; i < parts.Length - 1; i++)
        {
            var prop = current.GetType().GetProperty(parts[i],
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop == null) return;
            var next = prop.GetValue(current);
            if (next == null) return;
            current = next;
        }

        // Set the final property
        var finalProp = current.GetType().GetProperty(parts[^1],
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (finalProp == null || !finalProp.CanWrite) return;

        try
        {
            // Convert to the target type if needed
            if (value != null && !finalProp.PropertyType.IsAssignableFrom(value.GetType()))
            {
                var targetType = Nullable.GetUnderlyingType(finalProp.PropertyType) ?? finalProp.PropertyType;
                value = Convert.ChangeType(value, targetType);
            }
            finalProp.SetValue(current, value);
        }
        catch
        {
            // Graceful fallback — skip if conversion/set fails
        }
    }

    // ── Internal ─────────────────────────────────────────────────

    /// <summary>
    /// Check if an item passes the global filter predicate AND all per-column filters.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "DataGridSource uses reflection by design. Callers must configure trimming to preserve data item types.")]
    private bool PassesAllFilters(object item)
    {
        // Global predicate filter
        if (_filterPredicate != null && !_filterPredicate(item))
            return false;

        // Per-column filters
        foreach (var col in _columns)
        {
            if (col.ActiveFilter == null || !col.ActiveFilter.IsActive)
                continue;

            var cellValue = GetCellValueDirect(item, col);
            string displayText = FormatCellValue(cellValue, col);

            if (!col.ActiveFilter.Evaluate(cellValue, displayText))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Check if an item passes all filters EXCEPT the specified column's filter.
    /// Used for computing unique value lists.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "DataGridSource uses reflection by design. Callers must configure trimming to preserve data item types.")]
    private bool PassesOtherFilters(object item, DataGridColumn excludeColumn)
    {
        if (_filterPredicate != null && !_filterPredicate(item))
            return false;

        foreach (var col in _columns)
        {
            if (col == excludeColumn) continue;
            if (col.ActiveFilter == null || !col.ActiveFilter.IsActive)
                continue;

            var cellValue = GetCellValueDirect(item, col);
            string displayText = FormatCellValue(cellValue, col);

            if (!col.ActiveFilter.Evaluate(cellValue, displayText))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Get a cell value directly from an item (bypasses view indices).
    /// </summary>
    [RequiresUnreferencedCode("Accesses properties on data item types by name. Ensure the public properties of your data types are preserved when trimming.")]
    private object? GetCellValueDirect(object item, DataGridColumn column)
    {
        var accessor = GetOrCreateAccessor(item.GetType(), column.PropertyName);
        return accessor(item);
    }

    /// <summary>
    /// Format a cell value as display text.
    /// </summary>
    public static string FormatCellValue(object? value, DataGridColumn column)
    {
        if (value is null) return string.Empty;
        if (!string.IsNullOrEmpty(column.Format) && value is IFormattable fmt)
            return fmt.ToString(column.Format, null);
        return value.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Attempts an O(1) or O(log n) incremental update of <c>_viewIndices</c>
    /// for simple Add/Remove INCC events when no sort, filter, grouping, or
    /// summary is active. Returns false if a full <see cref="RebuildView"/> is needed.
    /// </summary>
    private bool TryIncrementalUpdate(NotifyCollectionChangedEventArgs e)
    {
        // Only handle single-item Add or Remove
        if (e.Action != NotifyCollectionChangedAction.Add &&
            e.Action != NotifyCollectionChangedAction.Remove)
            return false;

        // Cannot do incremental when sort, filter, group, or summaries are active
        bool hasSort = _columns.Any(c => c.SortDirection != SortDirection.None);
        bool hasFilter = _columns.Any(c => c.ActiveFilter is { IsActive: true });
        bool hasGroup = _groupDescriptions.Count > 0;
        bool hasSummaries = _tableSummaryRows.Count > 0 || _groupSummaryRows.Count > 0 ||
                            _captionSummary?.Columns.Count > 0;

        if (hasSort || hasFilter || hasGroup || hasSummaries)
            return false;

        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is { Count: 1 })
        {
            int sourceIndex = e.NewStartingIndex;
            if (sourceIndex < 0) return false; // Index not provided — fall back

            // Shift existing view indices that are >= the inserted position
            for (int i = 0; i < _viewIndices.Count; i++)
            {
                if (_viewIndices[i] >= sourceIndex)
                    _viewIndices[i]++;
            }

            // Append the new item (no sort/filter, so it goes at the insertion position)
            // Find where to insert in _viewIndices to maintain source-order
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

            return true;
        }

        if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems is { Count: 1 })
        {
            int sourceIndex = e.OldStartingIndex;
            if (sourceIndex < 0) return false; // Index not provided — fall back

            // Remove the entry and shift indices above the removed position
            _viewIndices.Remove(sourceIndex);
            for (int i = 0; i < _viewIndices.Count; i++)
            {
                if (_viewIndices[i] > sourceIndex)
                    _viewIndices[i]--;
            }

            return true;
        }

        return false;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "DataGridSource uses reflection by design. Callers must configure trimming to preserve data item types.")]
    internal void RebuildView()
    {
        _viewIndices.Clear();

        for (int i = 0; i < _sourceItems.Count; i++)
        {
            var item = _sourceItems[i]!;
            if (!PassesAllFilters(item))
                continue;
            _viewIndices.Add(i);
        }

        // Collect sorted columns in priority order
        var sortedColumns = _columns
            .Where(c => c.SortDirection != SortDirection.None && c.SortOrder > 0)
            .OrderBy(c => c.SortOrder)
            .ToList();

        // Fallback: if a column has a sort direction but no SortOrder, treat as single-sort
        if (sortedColumns.Count == 0)
        {
            var fallback = _columns.FirstOrDefault(c => c.SortDirection != SortDirection.None);
            if (fallback != null)
            {
                fallback.SortOrder = 1;
                sortedColumns.Add(fallback);
            }
        }

        if (sortedColumns.Count > 0 && _sourceItems.Count > 0)
        {
            var itemType = _sourceItems[0]!.GetType();

            // Pre-build accessors and comparers for each sort column
            var sortSpecs = sortedColumns.Select(col => new
            {
                Accessor = GetOrCreateAccessor(itemType, col.PropertyName),
                Comparer = col.CustomComparer ?? Comparer<object>.Default,
                Direction = col.SortDirection == SortDirection.Ascending ? 1 : -1
            }).ToList();

            _viewIndices.Sort((a, b) =>
            {
                foreach (var spec in sortSpecs)
                {
                    var va = spec.Accessor(_sourceItems[a]!);
                    var vb = spec.Accessor(_sourceItems[b]!);

                    int result;
                    if (va == null && vb == null) result = 0;
                    else if (va == null) result = -1 * spec.Direction;
                    else if (vb == null) result = 1 * spec.Direction;
                    else result = spec.Comparer.Compare(va, vb) * spec.Direction;

                    if (result != 0) return result;
                }
                return 0;
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

        DataChanged?.Invoke();
    }

    /// <summary>
    /// Recursively build the flat view by grouping source indices at the given level.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "DataGridSource uses reflection by design. Callers must configure trimming to preserve data item types.")]
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
        var groupMap = new Dictionary<string, int>(); // displayKey → index in groups list

        foreach (var sourceIdx in sourceIndices)
        {
            var item = _sourceItems[sourceIdx]!;
            object? rawValue;
            if (column != null)
            {
                var accessor = GetOrCreateAccessor(item.GetType(), groupDesc.PropertyName);
                rawValue = accessor(item);
            }
            else
            {
                rawValue = null;
            }

            object? groupKey = groupDesc.KeySelector != null ? groupDesc.KeySelector(rawValue) : rawValue;
            string displayText = groupDesc.DisplayTextSelector != null
                ? groupDesc.DisplayTextSelector(groupKey)
                : (groupKey?.ToString() ?? "(Blank)");

            // Use display text as dictionary key for grouping
            if (!groupMap.TryGetValue(displayText, out int groupIdx))
            {
                groupIdx = groups.Count;
                groupMap[displayText] = groupIdx;
                groups.Add((groupKey, displayText, new List<int>()));
            }
            groups[groupIdx].Indices.Add(sourceIdx);
        }

        // Sort groups
        if (groupDesc.GroupSortDirection == SortDirection.Ascending)
            groups.Sort((a, b) => string.Compare(a.DisplayText, b.DisplayText, StringComparison.OrdinalIgnoreCase));
        else if (groupDesc.GroupSortDirection == SortDirection.Descending)
            groups.Sort((a, b) => string.Compare(b.DisplayText, a.DisplayText, StringComparison.OrdinalIgnoreCase));

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

                // Emit group summary rows after the group's data (at leaf level only)
                if (_groupSummaryRows.Count > 0 && level == _groupDescriptions.Count - 1)
                {
                    var computedRows = ComputeGroupSummaries(indices);
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
    private List<ComputedSummaryRow> ComputeGroupSummaries(List<int> sourceIndices)
    {
        var result = new List<ComputedSummaryRow>();
        foreach (var sumRow in _groupSummaryRows)
        {
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
            string formatted = FormatSummaryValue(rawResult, colDesc);

            string label = colDesc.Label ?? GetDefaultLabel(colDesc.SummaryType);
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
    /// Compute a single summary row from column descriptions and source indices.
    /// </summary>
    private ComputedSummaryRow ComputeSummaryRow(
        string name, string? title,
        List<SummaryColumnDescription> columnDescs,
        List<int> sourceIndices)
    {
        var values = new Dictionary<string, string>();
        var rawValues = new Dictionary<string, object?>();

        foreach (var colDesc in columnDescs)
        {
            var colValues = CollectColumnValues(colDesc.PropertyName, sourceIndices);
            object? rawResult = ComputeAggregate(colDesc.SummaryType, colValues, colDesc.CustomAggregate);

            string label = colDesc.Label ?? GetDefaultLabel(colDesc.SummaryType);
            string formatted = FormatSummaryValue(rawResult, colDesc);

            values[colDesc.PropertyName] = $"{label}{formatted}";
            rawValues[colDesc.PropertyName] = rawResult;
        }

        return new ComputedSummaryRow
        {
            Name = name,
            Title = title,
            Values = values,
            RawValues = rawValues
        };
    }

    /// <summary>
    /// Collect raw cell values for a column from a set of source indices.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "DataGridSource uses reflection by design. Callers must configure trimming to preserve data item types.")]
    private List<object?> CollectColumnValues(string propertyName, List<int> sourceIndices)
    {
        var column = _columns.FirstOrDefault(c =>
            string.Equals(c.PropertyName, propertyName, StringComparison.OrdinalIgnoreCase));

        var result = new List<object?>(sourceIndices.Count);

        if (column == null || _sourceItems.Count == 0) return result;

        var accessor = GetOrCreateAccessor(_sourceItems[0]!.GetType(), propertyName);
        foreach (var idx in sourceIndices)
        {
            result.Add(accessor(_sourceItems[idx]!));
        }
        return result;
    }

    /// <summary>
    /// Compute an aggregate value from a list of raw values.
    /// </summary>
    private static object? ComputeAggregate(
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
                double sum = 0;
                foreach (var v in values)
                {
                    if (v != null && TryToDouble(v, out double d))
                        sum += d;
                }
                return sum;
            }

            case SummaryType.Average:
            {
                if (values.Count == 0) return null;
                double sum = 0;
                int count = 0;
                foreach (var v in values)
                {
                    if (v != null && TryToDouble(v, out double d))
                    {
                        sum += d;
                        count++;
                    }
                }
                return count > 0 ? sum / count : (object?)null;
            }

            case SummaryType.Min:
            {
                IComparable? min = null;
                foreach (var v in values)
                {
                    if (v is IComparable c)
                    {
                        if (min == null || c.CompareTo(min) < 0)
                            min = c;
                    }
                }
                return min;
            }

            case SummaryType.Max:
            {
                IComparable? max = null;
                foreach (var v in values)
                {
                    if (v is IComparable c)
                    {
                        if (max == null || c.CompareTo(max) > 0)
                            max = c;
                    }
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
    /// Try to convert a value to double for numeric aggregates.
    /// </summary>
    private static bool TryToDouble(object value, out double result)
    {
        result = 0;
        try
        {
            result = Convert.ToDouble(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Format an aggregate result using the column description's format.
    /// </summary>
    private static string FormatSummaryValue(object? value, SummaryColumnDescription colDesc)
    {
        if (value == null) return "";

        if (!string.IsNullOrEmpty(colDesc.Format))
        {
            // If format contains {0}, use String.Format
            if (colDesc.Format.Contains("{0}"))
            {
                try { return string.Format(colDesc.Format, value); }
                catch { /* fall through */ }
            }
            // Otherwise use IFormattable
            if (value is IFormattable fmt)
                return fmt.ToString(colDesc.Format, null);
        }

        return value.ToString() ?? "";
    }

    /// <summary>
    /// Get a default label for a summary type.
    /// </summary>
    private static string GetDefaultLabel(SummaryType type)
    {
        return type switch
        {
            SummaryType.Sum => "Sum: ",
            SummaryType.Average => "Avg: ",
            SummaryType.Count => "Count: ",
            SummaryType.Min => "Min: ",
            SummaryType.Max => "Max: ",
            SummaryType.Custom => "",
            _ => ""
        };
    }

    [RequiresUnreferencedCode("Accesses properties on data item types by name. Ensure the public properties of your data types are preserved when trimming.")]
    private Func<object, object?> GetOrCreateAccessor(Type type, string propertyPath)
    {
        var key = $"{type.FullName}.{propertyPath}";
        if (_accessors.TryGetValue(key, out var existing))
            return existing;

        var accessor = BuildAccessor(type, propertyPath);
        _accessors[key] = accessor;
        return accessor;
    }

    /// <summary>
    /// Build a cached property accessor for fast repeated access.
    /// Supports dotted paths: "Address.City".
    /// Resolves the PropertyInfo chain once and captures it in a closure.
    /// </summary>
    [RequiresUnreferencedCode("Accesses properties on data item types by name. Ensure the public properties of your data types are preserved when trimming.")]
    private static Func<object, object?> BuildAccessor(Type type, string propertyPath)
    {
        var parts = propertyPath.Split('.');
        var props = new PropertyInfo[parts.Length];
        Type currentType = type;

        for (int i = 0; i < parts.Length; i++)
        {
            var prop = currentType.GetProperty(parts[i],
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop == null)
                return _ => null; // Graceful fallback for unknown property
            props[i] = prop;
            currentType = prop.PropertyType;
        }

        // Capture the resolved PropertyInfo chain in a closure to avoid
        // repeated GetProperty calls on every access.
        return item =>
        {
            object? current = item;
            foreach (var prop in props)
            {
                if (current == null) return null;
                current = prop.GetValue(current);
            }
            return current;
        };
    }
}

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
/// <para>
/// <b>Threading.</b> A source belongs to the thread that last called <see cref="SetItems"/> or <see cref="Attach"/>:
/// the UI thread, for a grid. Change the bound collection, raise the items' <c>PropertyChanged</c> and call the
/// source's methods on that thread (or on its <see cref="SynchronizationContext"/>). A change raised on another
/// thread, such as <c>items.Add(...)</c> in a <c>Task.Run</c> continuation, throws an
/// <see cref="InvalidOperationException"/> to the code that made it, before the view is touched; the source then
/// re-reads the collection on its own thread (posted to that context, or on the next <see cref="Refresh"/>).
/// Marshal background changes with the platform dispatcher.
/// </para>
/// <para>
/// <b>Background rebuilds.</b> Sorting, filtering and grouping a large source rebuild the view on a worker when
/// <see cref="BackgroundRebuildThreshold"/> allows it (the MAUI and Uno grids turn this on for sources of
/// <see cref="DefaultBackgroundRebuildThreshold"/> rows or more). Until the new view is published every read answers
/// from the current one; see <see cref="BackgroundRebuildThreshold"/> for the contract.
/// </para>
/// </summary>
public partial class KumikoSource : IDisposable
{
    private IList _sourceItems = Array.Empty<object>();
    private bool _sourceItemsAreCopy;                 // the items were not an IList: _sourceItems is a private copy

    // The published view (#254): the visible rows, the group structure and the computed summaries. Every read answers
    // from it. A rebuild builds a new one (ViewBuilder) and swaps it in on the source's thread, so a background build
    // never shows a half-built view. A synchronous rebuild refills it in place.
    private ViewSnapshot _view = new();
    private int _collectionVersion;                  // moves on with every collection change notification
    private List<KumikoColumn> _columns = new();
    private Func<object, bool>? _filterPredicate;

    // ── Observable collection tracking ───────────────────────────
    private INotifyCollectionChanged? _observableCollection;
    private bool _isReordering;

    // ── Item property change tracking ────────────────────────────
    // Reference equality (items may override Equals); a set so track/untrack are O(1) and a duplicate is subscribed once.
    private readonly HashSet<INotifyPropertyChanged> _trackedItems = new(ReferenceEqualityComparer.Instance);

    // ── Lifecycle ────────────────────────────────────────────────
    private bool _disposed;
    private bool _detached;

    // ── Thread affinity (#69) ────────────────────────────────────
    // The source belongs to the thread that last called SetItems or Attach (the UI thread, in a grid). Changes
    // raised on another thread are rejected with WrongThreadMessage; see VerifyAccess.
    private int _ownerThreadId;                      // 0: no owner yet (SetItems never called)
    private SynchronizationContext? _ownerContext;   // SynchronizationContext.Current on the owner thread, if any
    private bool _needsResync;                       // a change was rejected: re-read the collection on the owner thread

    /// <summary>The message of the <see cref="InvalidOperationException"/> raised for a change made on another thread.</summary>
    internal const string WrongThreadMessage =
        "KumikoSource was changed from a thread other than the one it is attached to (the UI thread). Make collection " +
        "changes, item PropertyChanged notifications and KumikoSource calls on the UI thread: marshal them with the " +
        "dispatcher (MainThread.BeginInvokeOnMainThread, DispatcherQueue.TryEnqueue or SynchronizationContext.Post).";

    // ── Grouping state ───────────────────────────────────────────
    private readonly List<GroupDescription> _groupDescriptions = new();
    private readonly Dictionary<GroupKeyPath, bool> _groupExpandedState = new();
    private bool _defaultGroupExpanded = true;

    // ── Summary state ────────────────────────────────────────────
    private readonly List<TableSummaryRow> _tableSummaryRows = new();
    private readonly List<GroupSummaryRow> _groupSummaryRows = new();
    private CaptionSummaryDescription? _captionSummary;

    // ── View change notification ─────────────────────────────────
    private int _updateDepth;
    private bool _pendingRebuild;     // a state change inside BeginUpdate/EndUpdate deferred its rebuild
    private bool _pendingStructural;  // the view changed structurally inside BeginUpdate/EndUpdate
    private bool _pendingRedraw;
    private bool _pendingSummaries;   // table summaries must be recomputed at EndUpdate (the view itself is current)
    private bool _viewStale;          // a collection change inside a batch deferred its rebuild: rebuild before a read
    private object?[]? _batchOldRowKeys;
    private object?[]? _rowKeys;      // the rows as last notified; maintained only while ViewChanged has listeners
    private EventHandler<KumikoViewChangedEventArgs>? _viewChanged;
    private bool _isMultiSortActive;

    // ── Display column order and row-key index ───────────────────
    private KumikoColumn[] _displayColumns = Array.Empty<KumikoColumn>();
    private int[] _displayColumnIndices = Array.Empty<int>();
    private IReadOnlyList<KumikoColumn> _displayColumnsView = Array.Empty<KumikoColumn>();
    private int _columnLayoutVersion;

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
    public int RowCount
    {
        get
        {
            EnsureView();
            return _view.FlatView?.Count ?? _view.ViewIndices.Count;
        }
    }

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
            VerifyAccess();
            _culture = value;
            _comparer = new KumikoComparer(_culture, _stringCompareOptions);
            InvalidateCellText();
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
            VerifyAccess();
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
            VerifyAccess();
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
            VerifyAccess();
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
        set { VerifyAccess(); _captionSummary = value; RequestRebuild(); }
    }

    /// <summary>Computed top-positioned table summary rows.</summary>
    public IReadOnlyList<ComputedSummaryRow> ComputedTopSummaries
    {
        get
        {
            EnsureView();
            return _view.TopSummaries;
        }
    }

    /// <summary>Computed bottom-positioned table summary rows.</summary>
    public IReadOnlyList<ComputedSummaryRow> ComputedBottomSummaries
    {
        get
        {
            EnsureView();
            return _view.BottomSummaries;
        }
    }

    /// <summary>Number of summary rows above the data area.</summary>
    public int TopSummaryCount => ComputedTopSummaries.Count;

    /// <summary>Number of summary rows below the data area.</summary>
    public int BottomSummaryCount => ComputedBottomSummaries.Count;

    /// <summary>Whether any group summary rows are defined.</summary>
    public bool HasGroupSummaries => _groupSummaryRows.Count > 0;

    /// <summary>Add a table-level summary row.</summary>
    public void AddTableSummaryRow(TableSummaryRow row)
    {
        VerifyAccess();
        _tableSummaryRows.Add(row);
        RequestRebuild();
    }

    /// <summary>Remove a table-level summary row by name.</summary>
    public void RemoveTableSummaryRow(string name)
    {
        VerifyAccess();
        _tableSummaryRows.RemoveAll(r => r.Name == name);
        RequestRebuild();
    }

    /// <summary>Clear all table-level summary rows.</summary>
    public void ClearTableSummaryRows()
    {
        VerifyAccess();
        _tableSummaryRows.Clear();
        RequestRebuild();
    }

    /// <summary>Add a group-level summary row.</summary>
    public void AddGroupSummaryRow(GroupSummaryRow row)
    {
        VerifyAccess();
        _groupSummaryRows.Add(row);
        RequestRebuild();
    }

    /// <summary>Remove a group-level summary row by name.</summary>
    public void RemoveGroupSummaryRow(string name)
    {
        VerifyAccess();
        _groupSummaryRows.RemoveAll(r => r.Name == name);
        RequestRebuild();
    }

    /// <summary>Clear all group-level summary rows.</summary>
    public void ClearGroupSummaryRows()
    {
        VerifyAccess();
        _groupSummaryRows.Clear();
        RequestRebuild();
    }

    /// <summary>
    /// Get computed group summaries for a specific group path.
    /// Returns null if no group summaries are defined or the path is unknown.
    /// </summary>
    public IReadOnlyList<ComputedSummaryRow>? GetGroupSummaries(string groupPath)
    {
        EnsureView();
        return FindGroup(groupPath)?.Summaries;
    }

    /// <summary>
    /// Get computed caption summary text for a specific group path.
    /// Returns null if no caption summary is defined.
    /// </summary>
    public string? GetCaptionSummaryText(string groupPath)
    {
        EnsureView();
        return FindGroup(groupPath)?.CaptionText;
    }

    /// <summary>
    /// The group of the published view at <paramref name="groupPath"/>, or null. The first lookup after a rebuild
    /// composes the path of every group; the renderer reads a group's summaries from its header instead.
    /// </summary>
    private GroupHeaderInfo? FindGroup(string groupPath)
    {
        var view = _view;
        if (view.Groups.Count == 0) return null;
        var byPath = view.GroupsByPath;
        if (byPath == null)
        {
            byPath = new Dictionary<string, GroupHeaderInfo>(view.Groups.Count, StringComparer.Ordinal);
            foreach (var group in view.Groups)
                byPath.TryAdd(group.GroupPath, group);
            view.GroupsByPath = byPath;
        }
        return byPath.TryGetValue(groupPath, out var info) ? info : null;
    }

    /// <summary>The caption summary text of <paramref name="group"/> (a header of the published view), or null.</summary>
    internal static string? GetCaptionSummaryText(GroupHeaderInfo group) => group.CaptionText;

    /// <summary>The computed group summary row a group summary row shows, or null when the row is not one.</summary>
    internal ComputedSummaryRow? GetGroupSummaryRow(int visibleRow)
    {
        EnsureView();
        var flat = _view.FlatView;
        if (flat == null || visibleRow < 0 || visibleRow >= flat.Count) return null;
        var entry = flat[visibleRow];
        if (entry.Kind != FlatRowKind.GroupSummary || entry.GroupInfo!.Summaries is not { } rows) return null;
        return entry.GroupSummaryIndex < rows.Count ? rows[entry.GroupSummaryIndex] : null;
    }

    /// <summary>The active group descriptions, in order.</summary>
    public IReadOnlyList<GroupDescription> GroupDescriptions => _groupDescriptions;

    /// <summary>Whether new groups default to expanded.</summary>
    public bool DefaultGroupExpanded
    {
        get => _defaultGroupExpanded;
        set
        {
            VerifyAccess();
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
    /// The visible columns in on-screen order: left-frozen columns, then scrolling columns, then right-frozen
    /// columns, each group in <see cref="Columns"/> order. Hidden columns are left out. Keyboard navigation
    /// moves through this order. The list is cached and rebuilt only when the order changes; it is a snapshot,
    /// so read it again after changing the columns.
    /// </summary>
    public IReadOnlyList<KumikoColumn> DisplayColumns
    {
        get
        {
            EnsureDisplayColumns();
            return _displayColumnsView;
        }
    }

    /// <summary>
    /// Incremented whenever <see cref="DisplayColumns"/> changes: a column was hidden, shown, frozen,
    /// unfrozen or reordered, or the columns were replaced. Compare it to a stored value to find out whether
    /// anything derived from the on-screen order is stale.
    /// </summary>
    public int ColumnLayoutVersion
    {
        get
        {
            EnsureDisplayColumns();
            return _columnLayoutVersion;
        }
    }

    /// <summary>
    /// The position of <paramref name="column"/> in <see cref="DisplayColumns"/>, or -1 when it is hidden or
    /// not one of this source's columns.
    /// </summary>
    /// <param name="column">The column to find.</param>
    public int GetDisplayIndex(KumikoColumn column)
    {
        EnsureDisplayColumns();
        for (int i = 0; i < _displayColumns.Length; i++)
        {
            if (ReferenceEquals(_displayColumns[i], column)) return i;
        }
        return -1;
    }

    /// <summary>
    /// The position in <see cref="DisplayColumns"/> of <c>Columns[columnIndex]</c>, or -1 when the index is
    /// out of range or the column is hidden.
    /// </summary>
    /// <param name="columnIndex">An index into <see cref="Columns"/>.</param>
    public int ToDisplayIndex(int columnIndex)
    {
        EnsureDisplayColumns();
        for (int i = 0; i < _displayColumnIndices.Length; i++)
        {
            if (_displayColumnIndices[i] == columnIndex) return i;
        }
        return -1;
    }

    /// <summary>
    /// The index into <see cref="Columns"/> of the column at <paramref name="displayIndex"/> in
    /// <see cref="DisplayColumns"/>, or -1 when the position is out of range.
    /// </summary>
    /// <param name="displayIndex">A position in <see cref="DisplayColumns"/>.</param>
    public int ToColumnIndex(int displayIndex)
    {
        EnsureDisplayColumns();
        return displayIndex >= 0 && displayIndex < _displayColumnIndices.Length
            ? _displayColumnIndices[displayIndex]
            : -1;
    }

    /// <summary>
    /// Keep the cached display order current. A pass over the columns compares them with the cache and
    /// allocates only when the order differs, so calling this on every read is cheap in steady state.
    /// </summary>
    private void EnsureDisplayColumns()
    {
        if (DisplayOrderMatchesCache()) return;

        var columns = new List<KumikoColumn>(_columns.Count);
        var indices = new List<int>(_columns.Count);
        for (int pass = 0; pass < 3; pass++)
        {
            for (int i = 0; i < _columns.Count; i++)
            {
                if (_columns[i].IsVisible && PaneOf(_columns[i]) == pass)
                {
                    columns.Add(_columns[i]);
                    indices.Add(i);
                }
            }
        }

        _displayColumns = columns.ToArray();
        _displayColumnIndices = indices.ToArray();
        _displayColumnsView = Array.AsReadOnly(_displayColumns);
        _columnLayoutVersion++;
    }

    /// <summary>Whether the visible columns, in pane order, are exactly the cached display columns.</summary>
    private bool DisplayOrderMatchesCache()
    {
        int n = 0;
        for (int pass = 0; pass < 3; pass++)
        {
            for (int i = 0; i < _columns.Count; i++)
            {
                var column = _columns[i];
                if (!column.IsVisible || PaneOf(column) != pass) continue;
                if (n >= _displayColumns.Length || !ReferenceEquals(_displayColumns[n], column) ||
                    _displayColumnIndices[n] != i)
                    return false;
                n++;
            }
        }
        return n == _displayColumns.Length;
    }

    /// <summary>The pane a column draws in: 0 left-frozen, 1 scrolling, 2 right-frozen.</summary>
    private static int PaneOf(KumikoColumn column) => column.FreezeMode switch
    {
        ColumnFreezeMode.Left => 0,
        ColumnFreezeMode.Right => 2,
        _ => 1,
    };

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
    /// <para>
    /// The calling thread becomes the source's thread: later collection changes, item property changes and calls
    /// must come from it (see the threading remarks on <see cref="KumikoSource"/>).
    /// </para>
    /// </summary>
    public void SetItems(IEnumerable items)
    {
        if (_disposed) return;
        _detached = false;
        CaptureOwner();
        _needsResync = false;
        _collectionVersion++;

        // Unsubscribe from previous observable collection
        UnsubscribeFromCollection();
        UnsubscribeFromItemPropertyChanged();

        _sourceItemsAreCopy = items is not IList;
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

        // Drop the display text of the previous items (and the references to them)
        InvalidateCellText();
        // Cold path (items replaced), so LINQ is fine here.
        foreach (var col in _columns.Where(c => ReferenceEquals(c.TextCache?.Owner, this)))
            col.TextCache = null;

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

    /// <summary>
    /// Stops observing the bound collection and every item (so a collection that outlives the grid does not keep
    /// the source and its view alive) while keeping the items, view and settings. Call <see cref="Attach"/> to
    /// resume. Does nothing when already detached or disposed.
    /// </summary>
    public void Detach()
    {
        if (_disposed || _detached) return;
        _detached = true;
        UnsubscribeFromColumns();
        UnsubscribeFromCollection();
        UnsubscribeFromItemPropertyChanged();
    }

    /// <summary>
    /// Resumes observing the collection set by <see cref="SetItems"/> after <see cref="Detach"/>. The collection
    /// may have changed meanwhile, so the view is rebuilt. Does nothing unless the source is detached. The calling
    /// thread becomes the source's thread, as for <see cref="SetItems"/>.
    /// </summary>
    public void Attach()
    {
        if (_disposed || !_detached) return;
        _detached = false;
        CaptureOwner();
        _needsResync = false;
        _collectionVersion++; // the collection may have changed while detached
        SubscribeToColumns();
        if (_sourceItems is INotifyCollectionChanged incc)
        {
            _observableCollection = incc;
            incc.CollectionChanged += OnSourceCollectionChanged;
        }
        SubscribeToItemPropertyChanged();
        InvalidateCellText();
        RequestRebuild();
    }

    /// <summary>
    /// Unsubscribes from the bound collection and from every tracked item, releases the items, caches and
    /// <see cref="ViewChanged"/> handlers, and leaves an empty source. Later changes to the old collection raise
    /// nothing, and later calls are safe no-ops. Calling it more than once is harmless.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        UnsubscribeFromColumns();
        UnsubscribeFromCollection();
        UnsubscribeFromItemPropertyChanged();
        _viewChanged = null;
        _sourceItems = Array.Empty<object>();
        InvalidateCellText();
        foreach (var col in _columns.Where(c => ReferenceEquals(c.TextCache?.Owner, this)))
            col.TextCache = null;
        _rowKeys = null;
        _updateDepth = 0;
        _pendingRebuild = false;
        _pendingStructural = false;
        _pendingSummaries = false;
        _needsResync = false;
        _ownerContext = null;
        CancelBackgroundRebuild();
        _spareItems = null;
        RebuildCore();
        _view.TableAggregates.Clear(); // drop the row values they hold
        CompleteViewUpdate();
    }

    // ── Thread affinity ──────────────────────────────────────────

    /// <summary>Make the calling thread (and its synchronization context) the source's thread.</summary>
    private void CaptureOwner()
    {
        _ownerThreadId = Environment.CurrentManagedThreadId;
        _ownerContext = SynchronizationContext.Current;
    }

    /// <summary>
    /// Whether the calling code may change the source: it runs on the source's thread, or under the synchronization
    /// context captured there, or the source has no thread yet.
    /// </summary>
    private bool CheckAccess()
    {
        if (_ownerThreadId == 0 || _ownerThreadId == Environment.CurrentManagedThreadId) return true;
        var context = SynchronizationContext.Current;
        return context != null && ReferenceEquals(context, _ownerContext);
    }

    /// <summary>Throw <see cref="WrongThreadMessage"/> unless the calling code may change the source.</summary>
    private void VerifyAccess()
    {
        if (!CheckAccess())
            throw new InvalidOperationException(WrongThreadMessage);
    }

    /// <summary>
    /// A change notification arrived on another thread: the collection or an item has changed already, so the view
    /// no longer matches it. Re-read everything on the source's thread (posted to its context when there is one,
    /// otherwise on the next <see cref="Refresh"/>), then throw to the code that made the change.
    /// </summary>
    private void RejectForeignThreadChange()
    {
        if (!_needsResync)
        {
            _needsResync = true;
            _ownerContext?.Post(static state => ((KumikoSource)state!).Resync(), this);
        }
        throw new InvalidOperationException(WrongThreadMessage);
    }

    /// <summary>Re-subscribe to every item and rebuild the view after a rejected change (on the source's thread).</summary>
    private void Resync()
    {
        if (!_needsResync || _disposed) return;
        _needsResync = false;
        if (_detached) return; // Attach re-reads everything anyway
        UnsubscribeFromItemPropertyChanged();
        SubscribeToItemPropertyChanged();
        _collectionVersion++;
        if (!_view.ItemsAreCopy) _view.SourceIndexByItem = null;
        InvalidateCellText();
        RequestRebuild();
    }

    private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_disposed || _detached) return;
        if (!CheckAccess()) RejectForeignThreadChange();
        ContentVersion++;
        _collectionVersion++;
        // A view over the bound collection must follow it now; a view over a copy of the items (one built while
        // background rebuilds are enabled, #254) stays readable whatever the collection does, until the next rebuild.
        bool copyView = _view.ItemsAreCopy;
        if (!copyView) _view.SourceIndexByItem = null; // source indices may have moved
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
                InvalidateCellText(); // the same items may now hold anything
                break;
        }

        // For simple Add/Remove with no active sort, filter, or grouping we can
        // do an incremental update instead of a full O(n) rebuild. Inside BeginUpdate/EndUpdate a change that
        // needs a full rebuild only marks the view stale: the batch rebuilds once, at EndUpdate or at the first
        // read of a row before it, so a loop of Adds under a sort does not re-sort per item.
        // The collection has already changed, so the old rows come from the record kept since the last
        // notification, not from the (now stale) view.
        var oldRowKeys = _rowKeys;
        int viewIndex = -1;
        bool rowsChanged = true;
        // A view over a copy follows a single add or remove only while no rebuild is pending, which would not
        // include the change.
        bool incremental = !_viewStale && !(copyView && (IsViewUpdatePending || _pendingRebuild)) &&
                           TryIncrementalUpdate(e, out viewIndex, out rowsChanged);
        if (incremental)
        {
            _view.RowKeyIndex = null; // the rows move; the index is rebuilt on the next lookup
        }
        else if (copyView)
        {
            // The published view still matches its copy of the items, so it stays readable until the rebuild.
            if (_updateDepth > 0)
            {
                _pendingRebuild = true;
                if (!UseBackgroundRebuild()) _viewStale = true; // a synchronous view rebuilds before the next read
                return;
            }
            RequestRebuild();
            return;
        }
        else
        {
            _view.RowKeyIndex = null;
            if (_updateDepth > 0)
            {
                _viewStale = true;
                _pendingRebuild = true;
                return;
            }
            RebuildCore();
        }

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
        foreach (var inpc in _trackedItems)
            inpc.PropertyChanged -= OnItemPropertyChanged;
        _trackedItems.Clear();
    }

    private void TrackItem(INotifyPropertyChanged inpc)
    {
        if (_trackedItems.Add(inpc))
            inpc.PropertyChanged += OnItemPropertyChanged;
    }

    private void UntrackItem(INotifyPropertyChanged inpc)
    {
        if (_trackedItems.Remove(inpc))
            inpc.PropertyChanged -= OnItemPropertyChanged;
    }

    /// <summary>
    /// Handles an item's <c>PropertyChanged</c>: a redraw, or a rebuild when the property drives sorting,
    /// grouping, filtering or (while grouping) a group or caption summary. A <c>null</c> or empty property name
    /// means every property changed.
    /// Names match a column, group or summary property ignoring case, as columns bind to properties that way;
    /// a change of <c>Address</c> also counts for the path <c>Address.City</c>. With a filter predicate set, any
    /// change may change its result, so any change rebuilds.
    /// Only the item itself is observed: a nested object must raise <c>PropertyChanged</c> through the root item
    /// (for example <c>Address</c>) for a change to <c>Address.City</c> to be seen.
    /// <para>
    /// A change of a property that only summaries use rebuilds nothing. Sum, Average and Count are updated from the
    /// item's old and new value in constant time: in the table summaries, and (#254) in the group summaries and
    /// caption summaries of the groups that contain the item. Table Min, Max and Custom aggregates are recomputed over
    /// the visible rows without filtering, sorting or grouping them again (once per batch inside
    /// <see cref="BeginUpdate"/>); a group or caption Min, Max or Custom aggregate rebuilds the view. A change that
    /// rebuilds nothing is a <see cref="KumikoViewChangeKind.Redraw"/>.
    /// </para>
    /// <para>
    /// While background rebuilds apply (<see cref="BackgroundRebuildThreshold"/>), changes that rebuild the view are
    /// coalesced: however many arrive in one turn of the UI thread, one rebuild runs.
    /// </para>
    /// </summary>
    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_disposed || _detached) return;
        if (!CheckAccess()) RejectForeignThreadChange();
        if (sender != null)
            InvalidateCellText(sender, e.PropertyName);
        var impact = ItemPropertyImpact(e.PropertyName);
        if (impact == PropertyImpact.Rows ||
            impact == PropertyImpact.GroupSummaries && !UpdateGroupSummaries(sender, e.PropertyName!))
        {
            RequestRebuild();
            return;
        }
        if (_view.TableAggregates.Count > 0)
            UpdateTableSummaries(sender, e.PropertyName!);
        if (sender != null)
            RecordSummaryChange(sender, e.PropertyName!);
        NotifyRedraw(); // a display property, or a summary that was updated in place
    }

    /// <summary>What a change of an item property can affect.</summary>
    private enum PropertyImpact
    {
        /// <summary>Only what cells and table summaries show.</summary>
        None,

        /// <summary>A group summary or caption summary (but not the rows).</summary>
        GroupSummaries,

        /// <summary>The visible rows or the group structure: the view must be rebuilt.</summary>
        Rows,
    }

    /// <summary>
    /// What a change of <paramref name="propName"/> (null or empty: all) can change: the visible rows or the group
    /// structure (a rebuild), a group or caption summary (updated in place when it can be), or neither (table
    /// summaries, which <see cref="UpdateTableSummaries"/> keeps current without a rebuild).
    /// </summary>
    private PropertyImpact ItemPropertyImpact(string? propName)
    {
        // All properties changed, or an opaque filter predicate that may read any of them
        if (string.IsNullOrEmpty(propName) || _filterPredicate != null)
            return PropertyImpact.Rows;

        // A sorted or filtered column (several columns can share a property)
        foreach (var col in _columns)
        {
            if (!PropertyPathAffected(col.PropertyName, propName)) continue;
            if (col.SortDirection != KumikoSortDirection.None || col.ActiveFilter is { IsActive: true })
                return PropertyImpact.Rows;
        }

        // Group keys, group summaries and caption summaries exist only while grouping
        if (_groupDescriptions.Count == 0)
            return PropertyImpact.None;

        foreach (var group in _groupDescriptions)
            if (PropertyPathAffected(group.PropertyName, propName)) return PropertyImpact.Rows;
        foreach (var gsr in _groupSummaryRows)
            if (UsesProperty(gsr.Columns, propName)) return PropertyImpact.GroupSummaries;
        return _captionSummary != null && UsesProperty(_captionSummary.Columns, propName)
            ? PropertyImpact.GroupSummaries
            : PropertyImpact.None;
    }

    /// <summary>Whether any of <paramref name="descriptions"/> aggregates a property a change of <paramref name="changed"/> affects.</summary>
    private static bool UsesProperty(IReadOnlyList<SummaryColumnDescription> descriptions, string changed)
    {
        foreach (var description in descriptions)
            if (PropertyPathAffected(description.PropertyName, changed)) return true;
        return false;
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
        VerifyAccess();
        UnsubscribeFromColumns();
        _columns = columns.ToList();
        if (!_disposed && !_detached) SubscribeToColumns();
        UpdateSortCache();
        RequestRebuild(); // the columns carry sort and filter state and feed summaries
    }

    private void SubscribeToColumns()
    {
        foreach (var column in _columns)
            column.PropertyChanged += OnColumnPropertyChanged;
    }

    private void UnsubscribeFromColumns()
    {
        foreach (var column in _columns)
            column.PropertyChanged -= OnColumnPropertyChanged;
    }

    /// <summary>
    /// Handles a configuration change of a column (<see cref="KumikoColumn.PropertyChanged"/>): the view is rebuilt
    /// when the change alters what an active sort or filter reads, and otherwise redrawn. Layout and display order
    /// are recomputed from the columns on every frame, so a redraw is enough for a width, visibility or freeze change.
    /// </summary>
    private void OnColumnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_disposed || _detached) return;
        VerifyAccess(); // column settings follow the source's UI-thread contract
        if (sender is KumikoColumn column &&
            e.PropertyName is nameof(KumikoColumn.PropertyName) or nameof(KumikoColumn.ValueAccessor)
                or nameof(KumikoColumn.CustomComparer) or nameof(KumikoColumn.ColumnType) &&
            (column.SortDirection != KumikoSortDirection.None || column.ActiveFilter != null ||
             _groupDescriptions.Count > 0 || _tableSummaryRows.Count > 0))
        {
            RequestRebuild();
            return;
        }
        NotifyRedraw();
    }

    /// <summary>
    /// Move the column at <paramref name="fromIndex"/> in <see cref="Columns"/> so that it ends up at
    /// <paramref name="toIndex"/> (remove, then insert; the columns in between shift by one), then raise
    /// <see cref="ColumnOrderChanged"/> and repaint. Out-of-range indices and a move to the same index do nothing.
    /// The on-screen order (<see cref="DisplayColumns"/>) follows, within each pane.
    /// </summary>
    /// <param name="fromIndex">The column's index in <see cref="Columns"/>.</param>
    /// <param name="toIndex">The column's index in <see cref="Columns"/> after the move.</param>
    public void ReorderColumn(int fromIndex, int toIndex)
    {
        VerifyAccess();
        if (fromIndex < 0 || fromIndex >= _columns.Count) return;
        if (toIndex < 0 || toIndex >= _columns.Count) return;
        if (fromIndex == toIndex) return;

        var col = _columns[fromIndex];
        _columns.RemoveAt(fromIndex);
        _columns.Insert(toIndex, col);
        ColumnOrderChanged?.Invoke(this, EventArgs.Empty);
        NotifyRedraw();
    }

    /// <summary>
    /// Raised after <see cref="ReorderColumn"/> (or <see cref="SetDisplayIndex"/>, or applying a
    /// <see cref="KumikoLayoutState"/>) changed the order of <see cref="Columns"/>. Not raised by
    /// <see cref="SetColumns"/>. The MAUI and Uno grids use it to keep their <c>Columns</c> collection in the same
    /// order.
    /// </summary>
    public event EventHandler? ColumnOrderChanged;

    /// <summary>
    /// Move a visible column to <paramref name="displayIndex"/> in <see cref="DisplayColumns"/>, the on-screen
    /// order read by <see cref="GetDisplayIndex"/>. A column stays in its pane (left-frozen, scrolling or
    /// right-frozen; change <see cref="KumikoColumn.FreezeMode"/> to move it to another one), so the index is
    /// clamped to the pane's range. The column moves in <see cref="Columns"/> next to the column it displaces
    /// (<see cref="ReorderColumn"/>), and hidden columns keep their place. Does nothing for a hidden column.
    /// </summary>
    /// <param name="column">One of <see cref="Columns"/>.</param>
    /// <param name="displayIndex">The position in <see cref="DisplayColumns"/> the column should have.</param>
    /// <returns>The column's display index afterwards, or -1 when it is hidden.</returns>
    /// <exception cref="ArgumentException">The column is not one of <see cref="Columns"/>.</exception>
    public int SetDisplayIndex(KumikoColumn column, int displayIndex)
    {
        ArgumentNullException.ThrowIfNull(column);
        int from = _columns.IndexOf(column);
        if (from < 0)
            throw new ArgumentException("The column is not one of this source's columns.", nameof(column));

        int current = GetDisplayIndex(column);
        if (current < 0) return -1;

        // The pane's range in the display order: a contiguous run of columns with the same freeze mode.
        int paneStart = current, paneEnd = current;
        while (paneStart > 0 && _displayColumns[paneStart - 1].FreezeMode == column.FreezeMode) paneStart--;
        while (paneEnd < _displayColumns.Length - 1 && _displayColumns[paneEnd + 1].FreezeMode == column.FreezeMode) paneEnd++;
        int target = Math.Clamp(displayIndex, paneStart, paneEnd);
        if (target == current) return current;

        // Insert next to the displaced column: after it when moving right, before it when moving left.
        int other = _displayColumnIndices[target];
        int otherAfterRemove = other > from ? other - 1 : other;
        ReorderColumn(from, target > current ? otherAfterRemove + 1 : otherAfterRemove);
        return GetDisplayIndex(column);
    }

    /// <summary>
    /// Move the data row at visible index <paramref name="fromVisibleRow"/> so that it ends up at visible index
    /// <paramref name="toVisibleRow"/> (the rows in between shift by one), by moving its item in the underlying
    /// collection, then rebuild the view once (one <see cref="ViewChanged"/>). Under a filter the item moves to the
    /// target row's position in the collection.
    /// <para>
    /// With grouping active a row moves only within its own (innermost) group: the item is moved next to the target
    /// item in the collection, which keeps the order of the group's other items. A target in another group is
    /// rejected with <see cref="KumikoReorderResult.NotSupportedAcrossGroups"/>, because moving a row between groups
    /// means changing its grouped value, which is the app's decision.
    /// </para>
    /// <para>
    /// Nothing changes, and the result says why, when an index is out of range or not a data row, the row is already
    /// there, a column sort is active (the sort would put the row back), or the items source is not a writable,
    /// resizable <see cref="IList"/>.
    /// </para>
    /// </summary>
    /// <param name="fromVisibleRow">The visible index of the row to move.</param>
    /// <param name="toVisibleRow">The visible index the row should end up at.</param>
    /// <returns><see cref="KumikoReorderResult.Moved"/> when the item moved, otherwise the reason it did not.</returns>
    public KumikoReorderResult ReorderRow(int fromVisibleRow, int toVisibleRow)
    {
        VerifyAccess();
        int rowCount = RowCount;
        if (fromVisibleRow < 0 || fromVisibleRow >= rowCount || toVisibleRow < 0 || toVisibleRow >= rowCount ||
            IsNonDataRow(fromVisibleRow) || IsNonDataRow(toVisibleRow))
            return KumikoReorderResult.OutOfRange;
        if (fromVisibleRow == toVisibleRow) return KumikoReorderResult.NoOp;
        if (_sourceItemsAreCopy || _sourceItems.IsReadOnly || _sourceItems.IsFixedSize)
            return KumikoReorderResult.NotSupportedSource;
        if (IsColumnSortActive()) return KumikoReorderResult.NotSupportedSorted;
        if (!AreInSameGroup(fromVisibleRow, toVisibleRow)) return KumikoReorderResult.NotSupportedAcrossGroups;

        int sourceFrom = GetLiveSourceIndex(fromVisibleRow);
        int sourceTo = GetLiveSourceIndex(toVisibleRow);
        if (sourceFrom < 0 || sourceTo < 0) return KumikoReorderResult.OutOfRange; // no longer in the collection

        _isReordering = true;
        try
        {
            var item = _sourceItems[sourceFrom];
            _sourceItems.RemoveAt(sourceFrom);
            _sourceItems.Insert(sourceTo, item);
        }
        finally
        {
            _isReordering = false;
        }

        // Rebuild the view to reflect the new order
        RequestRebuild();
        return KumikoReorderResult.Moved;
    }

    /// <summary>The index in the published view's items of a data row (no range or kind check).</summary>
    private int GetSourceIndex(int visibleRow) =>
        _view.FlatView is { } flat ? flat[visibleRow].SourceIndex : _view.ViewIndices[visibleRow];

    /// <summary>
    /// The index in the bound collection of a data row's item: the view's own index while the view's items match the
    /// collection, otherwise (a rebuild is pending after a collection change) the item's current position, or -1.
    /// </summary>
    private int GetLiveSourceIndex(int visibleRow)
    {
        int index = GetSourceIndex(visibleRow);
        if (!_view.ItemsAreCopy || _view.CollectionVersion == _collectionVersion) return index;
        var item = _view.Items[index];
        for (int i = 0; i < _sourceItems.Count; i++)
            if (ReferenceEquals(_sourceItems[i], item)) return i;
        return -1;
    }

    /// <summary>Whether a column sort decides the order of the rows.</summary>
    private bool IsColumnSortActive()
    {
        for (int i = 0; i < _columns.Count; i++)
            if (_columns[i].SortDirection != KumikoSortDirection.None) return true;
        return false;
    }

    /// <summary>
    /// Whether two visible rows are in the same innermost group (always true without grouping): the rows a row drag
    /// can be dropped between. O(log groups), allocation-free.
    /// </summary>
    internal bool AreInSameGroup(int visibleRowA, int visibleRowB)
    {
        EnsureView();
        if (_view.FlatView == null) return true;
        int level = _view.GroupLevels - 1;
        return TryGetEnclosingGroup(visibleRowA, level, out int headerA, out _) &&
               TryGetEnclosingGroup(visibleRowB, level, out int headerB, out _) &&
               headerA == headerB;
    }

    /// <summary>
    /// Get a raw data item by visible-row index.
    /// Throws if the row is a group header.
    /// </summary>
    public object GetItem(int visibleRow)
    {
        EnsureView();
        var view = _view;
        if (view.FlatView is { } flat)
        {
            if (visibleRow < 0 || visibleRow >= flat.Count)
                throw new ArgumentOutOfRangeException(nameof(visibleRow));
            var entry = flat[visibleRow];
            if (entry.Kind == FlatRowKind.GroupHeader)
                throw new InvalidOperationException("Cannot get a data item for a group header row.");
            return view.Items[entry.SourceIndex] ?? throw new InvalidOperationException("The view references a null item.");
        }

        if (visibleRow < 0 || visibleRow >= view.ViewIndices.Count)
            throw new ArgumentOutOfRangeException(nameof(visibleRow));
        return view.Items[view.ViewIndices[visibleRow]] ?? throw new InvalidOperationException("The view references a null item.");
    }

    /// <summary>
    /// Check whether a visible row is a group header.
    /// </summary>
    public bool IsGroupHeaderRow(int visibleRow)
    {
        EnsureView();
        if (_view.FlatView is not { } flat) return false;
        if (visibleRow < 0 || visibleRow >= flat.Count) return false;
        return flat[visibleRow].Kind == FlatRowKind.GroupHeader;
    }

    /// <summary>
    /// Check whether a visible row is a group summary row.
    /// </summary>
    public bool IsGroupSummaryRow(int visibleRow)
    {
        EnsureView();
        if (_view.FlatView is not { } flat) return false;
        if (visibleRow < 0 || visibleRow >= flat.Count) return false;
        return flat[visibleRow].Kind == FlatRowKind.GroupSummary;
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
        EnsureView();
        if (_view.FlatView is not { } flat) return null;
        if (visibleRow < 0 || visibleRow >= flat.Count) return null;
        var entry = flat[visibleRow];
        if (entry.Kind != FlatRowKind.GroupSummary) return null;
        return (entry.GroupInfo!.GroupPath, entry.GroupSummaryIndex);
    }

    /// <summary>
    /// Get group header info for a group header row.
    /// Returns null if the row is not a group header.
    /// </summary>
    public GroupHeaderInfo? GetGroupHeaderInfo(int visibleRow)
    {
        EnsureView();
        if (_view.FlatView is not { } flat) return null;
        if (visibleRow < 0 || visibleRow >= flat.Count) return null;
        var entry = flat[visibleRow];
        return entry.Kind == FlatRowKind.GroupHeader ? entry.GroupInfo : null;
    }

    /// <summary>
    /// The group at <paramref name="level"/> that contains <paramref name="visibleRow"/> (the row itself when it is
    /// that group's header): the group's header row and the row just after the group (the next header at the same or
    /// an outer level, or <see cref="RowCount"/>). False when grouping is off, the row is out of range, or the row is
    /// not inside a group at that level (a header of an outer level). A binary search over the group headers, so it
    /// costs O(log groups) and allocates nothing.
    /// </summary>
    internal bool TryGetEnclosingGroup(int visibleRow, int level, out int headerRow, out int endRow)
    {
        EnsureView();
        headerRow = endRow = -1;
        var view = _view;
        if (view.FlatView is not { } flat || visibleRow < 0 || visibleRow >= flat.Count || view.GroupHeaderRows.Count == 0)
            return false;

        // The last header at or before the row is the row's innermost group; walk out to the requested level.
        int index = view.GroupHeaderRows.BinarySearch(visibleRow);
        if (index < 0) index = ~index - 1;
        while (index >= 0 && view.GroupHeaderLevels[index] > level)
            index = view.GroupHeaderParents[index];
        if (index < 0 || view.GroupHeaderLevels[index] != level || visibleRow >= view.GroupHeaderEnds[index])
            return false;

        headerRow = view.GroupHeaderRows[index];
        endRow = view.GroupHeaderEnds[index];
        return true;
    }

    /// <summary>
    /// Toggle the expand/collapse state of a group header row.
    /// </summary>
    public void ToggleGroupExpansion(int visibleRow)
    {
        VerifyAccess();
        var info = GetGroupHeaderInfo(visibleRow);
        if (info == null) return;

        bool isExpanding = !info.IsExpanded;

        // Raise expanding event (cancelable)
        var expandingArgs = new GroupExpandingEventArgs(info, isExpanding);
        GroupExpanding?.Invoke(this, expandingArgs);
        if (expandingArgs.Cancel) return;

        // Toggle state
        _groupExpandedState[info.KeyPath!] = isExpanding;
        RequestRebuild();

        // The rebuilt header, when the rebuild was synchronous; a background rebuild publishes it later.
        GroupHeaderInfo? rebuilt = null;
        if (_view.FlatView is { } flat)
        {
            foreach (var row in flat)
            {
                if (row.Kind == FlatRowKind.GroupHeader && row.GroupInfo!.GroupPath == info.GroupPath)
                {
                    rebuilt = row.GroupInfo;
                    break;
                }
            }
        }
        GroupExpandedChanged?.Invoke(this, new GroupExpandedChangedEventArgs(rebuilt ?? info, isExpanding));
    }


    /// <summary>
    /// The visible row that shows <paramref name="item"/> in the current sorted, filtered and grouped view, or -1
    /// when it has none: it is not in the source, a filter hides it, or it sits in a collapsed group (see
    /// <see cref="ExpandToItem"/>). Items are matched like selection does (by reference, or by value for value
    /// types and records). Scans the view.
    /// </summary>
    /// <param name="item">The item to find.</param>
    /// <returns>The row, or -1.</returns>
    public int IndexOfItem(object item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return IndexOfRowKey(item);
    }

    /// <summary>
    /// The visible row of <paramref name="item"/>, first expanding the collapsed groups that contain it, outermost
    /// first, so a row exists for it. Expanding goes through <see cref="ToggleGroupExpansion"/>, so
    /// <see cref="GroupExpanding"/> and <see cref="GroupExpandedChanged"/> are raised as for a tap and a handler can
    /// cancel it. Returns -1 when the item has no row even so: it is not in the source, a filter hides it, or a
    /// handler canceled an expansion. Nothing is expanded for an item a filter hides.
    /// <para>
    /// The answer needs the current view, so when a background rebuild is pending (<see cref="IsViewUpdatePending"/>)
    /// or the expansion rebuilds the view, this method rebuilds it synchronously on the calling thread.
    /// </para>
    /// </summary>
    /// <param name="item">The item to reveal.</param>
    /// <returns>The row, or -1.</returns>
    public int ExpandToItem(object item)
    {
        ArgumentNullException.ThrowIfNull(item);
        VerifyAccess();
        bool forced = _forceSyncRebuild;
        _forceSyncRebuild = true;
        try
        {
            if (IsViewUpdatePending) RequestRebuild(); // publish the pending view now
            return ExpandToItemCore(item);
        }
        finally
        {
            _forceSyncRebuild = forced;
        }
    }

    private int ExpandToItemCore(object item)
    {
        int row = IndexOfItem(item);
        if (row >= 0 || _view.FlatView == null) return row;

        // Hidden by a filter: no group holds it, so there is nothing to expand.
        bool inView = false;
        var view = _view;
        for (int i = 0; i < view.ViewIndices.Count && !inView; i++)
            inView = RowKeyComparer.Instance.Equals(view.Items[view.ViewIndices[i]], item);
        if (!inView) return -1;

        GroupKeyPath? parentPath = null;
        var keyComparer = new GroupKeyComparer(_comparer.TextComparer);
        for (int level = 0; level < _groupDescriptions.Count; level++)
        {
            var desc = _groupDescriptions[level];
            var column = _columns.FirstOrDefault(c =>
                string.Equals(c.PropertyName, desc.PropertyName, StringComparison.OrdinalIgnoreCase));
            object? key = ComputeGroupKey(desc, column, item).Key;

            int header = FindGroupHeaderRow(level, parentPath, key, keyComparer);
            if (header < 0) return -1;
            if (!_view.FlatView![header].GroupInfo!.IsExpanded)
            {
                ToggleGroupExpansion(header);
                header = FindGroupHeaderRow(level, parentPath, key, keyComparer);
                if (header < 0 || _view.FlatView is not { } rebuilt || !rebuilt[header].GroupInfo!.IsExpanded) return -1;
            }
            parentPath = _view.FlatView![header].GroupInfo!.KeyPath;
        }
        return IndexOfItem(item);
    }

    private int FindGroupHeaderRow(int level, GroupKeyPath? parentPath, object? key, GroupKeyComparer keyComparer)
    {
        if (_view.FlatView is not { } flat) return -1;
        for (int i = 0; i < flat.Count; i++)
        {
            var info = flat[i].GroupInfo;
            if (flat[i].Kind != FlatRowKind.GroupHeader || info == null || info.Level != level) continue;
            if (!Equals(info.KeyPath?.Parent, parentPath)) continue;
            if (key is null ? info.Key is null : info.Key is not null && keyComparer.Equals(info.Key, key)) return i;
        }
        return -1;
    }

    /// <summary>
    /// Expand all groups.
    /// </summary>
    public void ExpandAllGroups()
    {
        VerifyAccess();
        _defaultGroupExpanded = true;
        _groupExpandedState.Clear();
        RequestRebuild();
    }

    /// <summary>
    /// Collapse all groups.
    /// </summary>
    public void CollapseAllGroups()
    {
        VerifyAccess();
        _defaultGroupExpanded = false;
        _groupExpandedState.Clear();
        RequestRebuild();
    }

    /// <summary>
    /// Drop the expand/collapse state of groups the current descriptions can no longer contain (a path whose
    /// property differs from the description at its level, or that is deeper than the description list), and
    /// keep the rest.
    /// </summary>
    private void PruneGroupExpandedState()
    {
        if (_groupExpandedState.Count == 0) return;
        // Runs only when the group descriptions change, so the LINQ allocation is fine here.
        var stale = _groupExpandedState.Keys.Where(path => !path.IsValidFor(_groupDescriptions)).ToList();
        foreach (var path in stale) _groupExpandedState.Remove(path);
    }

    /// <summary>
    /// Add a group description. Groups are applied in order (multi-level).
    /// </summary>
    public void AddGroupDescription(GroupDescription group)
    {
        VerifyAccess();
        _groupDescriptions.Add(group);
        PruneGroupExpandedState();
        RequestRebuild();
    }

    /// <summary>
    /// Insert a group description at a specific position.
    /// </summary>
    public void InsertGroupDescription(int index, GroupDescription group)
    {
        VerifyAccess();
        _groupDescriptions.Insert(index, group);
        PruneGroupExpandedState();
        RequestRebuild();
    }

    /// <summary>
    /// Remove a group description by property name.
    /// </summary>
    public void RemoveGroupDescription(string propertyName)
    {
        VerifyAccess();
        _groupDescriptions.RemoveAll(g => g.PropertyName == propertyName);
        PruneGroupExpandedState();
        RequestRebuild();
    }

    /// <summary>
    /// Clear all group descriptions.
    /// </summary>
    public void ClearGroupDescriptions()
    {
        VerifyAccess();
        _groupDescriptions.Clear();
        PruneGroupExpandedState();
        if (!UseBackgroundRebuild())
        {
            // Rows read before the rebuild (inside a batch) are ungrouped already. A background rebuild keeps
            // answering from the published view until the new one is swapped in.
            _view.FlatView = null;
            _view.RowKeyIndex = null;
        }
        RequestRebuild();
    }

    /// <summary>
    /// Replace all group descriptions with <paramref name="groups"/> (in order) as one change: a single
    /// rebuild and one <see cref="ViewChanged"/> notification. Hosts use it to mirror a bindable collection.
    /// </summary>
    public void SetGroupDescriptions(IEnumerable<GroupDescription>? groups)
    {
        VerifyAccess();
        BeginUpdate();
        try
        {
            ClearGroupDescriptions();
            if (groups != null)
                foreach (var group in groups)
                    AddGroupDescription(group);
        }
        finally { EndUpdate(); }
    }

    /// <summary>
    /// Replace all group summary rows with <paramref name="rows"/> as one change: a single rebuild and one
    /// <see cref="ViewChanged"/> notification. Hosts use it to mirror a bindable collection.
    /// </summary>
    public void SetGroupSummaryRows(IEnumerable<GroupSummaryRow>? rows)
    {
        VerifyAccess();
        BeginUpdate();
        try
        {
            ClearGroupSummaryRows();
            if (rows != null)
                foreach (var row in rows)
                    AddGroupSummaryRow(row);
        }
        finally { EndUpdate(); }
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
        VerifyAccess();
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
        VerifyAccess();
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
        VerifyAccess();
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

            var cellValue = ReadColumnValue(item, column);
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
        VerifyAccess();
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
        VerifyAccess();
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
        VerifyAccess();
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
        VerifyAccess();
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
    /// Batches nest. A collection change that the view can follow incrementally is applied at once; one that needs
    /// a rebuild (an active sort, grouping or summary, a reset, a replace) marks the view stale instead, so many
    /// changes cost one rebuild. Rows stay readable: reading one from a stale view rebuilds it first. (When the
    /// rebuild runs in the background, see <see cref="BackgroundRebuildThreshold"/>, rows are read from the published
    /// view instead, and the rebuild starts at <see cref="EndUpdate"/>.)
    /// <para>
    /// Dispose the returned scope to end the batch, so it ends even if the changes throw:
    /// <c>using (source.BeginUpdate()) { ... }</c>. Dispose it once; it calls <see cref="EndUpdate"/>.
    /// </para>
    /// </summary>
    /// <returns>A scope whose <see cref="KumikoUpdateScope.Dispose"/> calls <see cref="EndUpdate"/>.</returns>
    /// <exception cref="InvalidOperationException">Called from a thread other than the source's.</exception>
    public KumikoUpdateScope BeginUpdate()
    {
        VerifyAccess();
        if (_updateDepth++ == 0)
        {
            _pendingRebuild = _pendingStructural = _pendingRedraw = _pendingSummaries = false;
            _batchOldRowKeys = _rowKeys;
        }
        return new KumikoUpdateScope(this);
    }

    /// <summary>
    /// End a batch started with <see cref="BeginUpdate"/>. The outermost call applies a deferred rebuild and
    /// raises one <see cref="ViewChanged"/>: <see cref="KumikoViewChangeKind.Rebuild"/> if the rows changed,
    /// otherwise <see cref="KumikoViewChangeKind.Redraw"/> if anything needs repainting. A rebuild that runs in the
    /// background (<see cref="BackgroundRebuildThreshold"/>) starts here and raises its notification when it is
    /// published.
    /// </summary>
    /// <exception cref="InvalidOperationException">No batch is open.</exception>
    public void EndUpdate()
    {
        VerifyAccess();
        if (_updateDepth == 0)
            throw new InvalidOperationException("EndUpdate was called without a matching BeginUpdate.");
        if (--_updateDepth > 0) return;

        var oldRowKeys = _batchOldRowKeys;
        _batchOldRowKeys = null;
        bool deferred = false;
        if (_pendingRebuild || _pendingSummaries)
        {
            if (UseBackgroundRebuild())
            {
                // The rows change when the background build is published, which raises the notification then.
                ScheduleBackgroundRebuild();
                deferred = true;
            }
            else if (_pendingRebuild)
                RebuildCore();
            else
                ComputeTableSummaries();
        }
        _pendingSummaries = false;

        bool structural = _pendingRebuild && !deferred || _pendingStructural;
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

    /// <summary>
    /// Rebuild the view now and raise <see cref="KumikoViewChangeKind.Rebuild"/>, or defer both in a batch, or (for a
    /// large source, <see cref="BackgroundRebuildThreshold"/>) schedule a background rebuild that raises both when it
    /// is published.
    /// </summary>
    private void RequestRebuild()
    {
        VerifyAccess();
        if (_updateDepth > 0)
        {
            _pendingRebuild = true;
            return;
        }
        if (UseBackgroundRebuild())
        {
            ScheduleBackgroundRebuild();
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
        VerifyAccess();
        ContentVersion++;
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
        EnsureView();
        return ViewBuilder.RowKeyOf(_view, visibleRow);
    }

    /// <summary>
    /// The visible row whose key matches <paramref name="key"/> (an item by reference, a group row by value),
    /// or -1. Constant time: it reads a dictionary that is built on first use after the view changes.
    /// </summary>
    internal int IndexOfRowKey(object key)
    {
        EnsureView();
        var view = _view;
        var index = view.RowKeyIndex;
        if (index == null)
        {
            int count = RowCount;
            index = new Dictionary<object, int>(count, RowKeyComparer.Instance);
            for (int i = 0; i < count; i++)
            {
                if (ViewBuilder.RowKeyOf(view, i) is { } candidate) index.TryAdd(candidate, i);
            }
            view.RowKeyIndex = index;
        }
        return index.TryGetValue(key, out int row) ? row : -1;
    }

    /// <summary>
    /// The key of every visible row: the item for data rows, a <see cref="GroupRowKey"/> for group header and
    /// group summary rows.
    /// </summary>
    private object?[] GetRowKeys()
    {
        EnsureView();
        var view = _view;
        var keys = new object?[view.FlatView?.Count ?? view.ViewIndices.Count];
        for (int i = 0; i < keys.Length; i++)
            keys[i] = ViewBuilder.RowKeyOf(view, i);
        return keys;
    }

    /// <summary>
    /// Force a full rebuild of the sorted/filtered view, and read every drawn cell again. The grid caches each
    /// drawn cell's display text until the item raises <see cref="INotifyPropertyChanged.PropertyChanged"/> or the
    /// view changes, so call this after changing items that do not raise it. After a change from another thread
    /// was rejected (see <see cref="KumikoSource"/>), it also re-reads the collection and its items.
    /// </summary>
    public void Refresh()
    {
        VerifyAccess();
        if (_needsResync)
        {
            Resync();
            return;
        }
        _collectionVersion++; // items may have moved without a notification
        if (!_view.ItemsAreCopy) _view.SourceIndexByItem = null;
        InvalidateCellText();
        RequestRebuild();
    }

    /// <summary>
    /// Rebuild a view that a collection change inside a batch left stale (see <see cref="BeginUpdate"/>) before
    /// anything reads a row. One field check when the view is current.
    /// </summary>
    private void EnsureView()
    {
        if (_viewStale) RebuildCore(); // the batch still raises its one notification at EndUpdate
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
        VerifyAccess();
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
            {
                InvalidateCellText(item, null);
                NotifyRedraw();
            }
            return written;
        }
        finally
        {
            EndUpdate();
        }
    }

    // ── Internal ─────────────────────────────────────────────────

    /// <summary>
    /// The active column filters (except <paramref name="exclude"/>), prepared for one pass over the items: each value
    /// list as a set under <paramref name="comparer"/>'s text rules (the source's by default). For a build on a worker
    /// (<paramref name="forWorker"/>) the filters are copies.
    /// </summary>
    private FilterSpec[] PrepareFilters(KumikoColumn? exclude = null, KumikoComparer? comparer = null, bool forWorker = false)
    {
        comparer ??= _comparer;
        List<FilterSpec>? prepared = null;
        foreach (var col in _columns)
        {
            if (col == exclude || col.ActiveFilter is not { IsActive: true } filter) continue;
            ISet<string>? values = filter.SelectedValues is { } selected
                ? new HashSet<string>(selected, comparer.TextComparer)
                : null;
            (prepared ??= new()).Add(new FilterSpec(col, new ItemReader(col),
                forWorker ? filter.CloneForBuild() : filter, values, col.Format));
        }
        return prepared?.ToArray() ?? Array.Empty<FilterSpec>();
    }

    /// <summary>
    /// Check if an item passes the global filter predicate and the prepared per-column filters.
    /// </summary>
    private bool PassesFilters(object item, FilterSpec[] filters) =>
        ViewBuilder.Passes(item, _filterPredicate, filters, _comparer);

    /// <summary>
    /// Read a column's value from an item: <see cref="KumikoColumn.ValueAccessor"/> when it is set,
    /// otherwise the column's property path (an empty path is the item itself).
    /// </summary>
    private static object? ReadColumnValue(object item, KumikoColumn column) =>
        column.ValueAccessor != null
            ? column.ValueAccessor(item)
            : column.ResolvePath(item.GetType()).GetValue(item);

    /// <summary>
    /// Format a cell value as display text: the column's <see cref="KumikoColumn.Format"/> in
    /// <see cref="Culture"/> for formattable values, otherwise <see cref="object.ToString"/>; empty for null.
    /// </summary>
    public string FormatCellValue(object? value, KumikoColumn column) => _comparer.Format(value, column.Format);

    // ── Display text cache (#68) ─────────────────────────────────
    //
    // The renderer reads cells through GetCachedCellText, which keeps each drawn cell's value and display text
    // (CellTextCache, one per column) so a frame that shows the same data reads, boxes and formats nothing.
    // An entry is valid while its row still shows the same item and its stamp is current. The stamp moves on
    // whatever can change the text of an unchanged item: a rebuild, Refresh, a collection reset, a culture
    // change. An item's PropertyChanged and SetCellValue invalidate just that item's entries. Like the view
    // rebuilds, the invalidation runs on the thread that raises the change, which should be the UI thread.

    private int _textStamp = 1;           // 0 marks an entry stale
    private int _textFrozenRows;          // slots 0..n-1: the frozen rows
    private int _textRingSize = 64;       // then a ring for the scrolling rows
    private int _textLayoutVersion;       // bumped when the slot layout changes; caches built for another are replaced
    private CultureInfo? _textCulture;    // the culture the entries were formatted in

    /// <summary>
    /// Prepare the display text cache for a frame that draws <paramref name="frozenRows"/> frozen rows and up to
    /// <paramref name="scrollingRows"/> scrolling rows. Call once per frame before <see cref="GetCachedCellText"/>.
    /// </summary>
    internal void PrepareCellTextCache(int frozenRows, int scrollingRows)
    {
        SyncCellTextCulture();

        int ring = _textRingSize;
        while (ring < scrollingRows * 2) ring *= 2;
        if (frozenRows != _textFrozenRows || ring != _textRingSize)
        {
            _textFrozenRows = frozenRows;
            _textRingSize = ring;
            _textLayoutVersion++;
        }
    }

    /// <summary>
    /// Marks the cached display text stale when the formatting culture changed since the last frame. The renderer
    /// calls it before laying out columns, so Auto columns measure text formatted in the culture the frame draws.
    /// </summary>
    internal void SyncCellTextCulture()
    {
        // Culture null means the current culture at the time of use, which may change between frames.
        var culture = _comparer.Culture;
        if (!ReferenceEquals(culture, _textCulture))
        {
            _textCulture = culture;
            InvalidateCellText();
        }
    }

    /// <summary>
    /// The display text of a data cell, and its value in <paramref name="value"/>, from the display text cache:
    /// read and formatted only when the row shows another item than when it was cached, or the entry is stale.
    /// <paramref name="item"/> is the row's item (<see cref="GetItem"/>).
    /// </summary>
    internal string GetCachedCellText(int visibleRow, object item, KumikoColumn column, out object? value)
    {
        var cache = column.TextCache;
        if (cache == null || !cache.IsCurrentFor(this, column, _textLayoutVersion))
            column.TextCache = cache = new CellTextCache(this, column, _textLayoutVersion, _textFrozenRows + _textRingSize);

        int slot = visibleRow < _textFrozenRows
            ? visibleRow
            : _textFrozenRows + (visibleRow - _textFrozenRows) % _textRingSize;
        ref var entry = ref cache.Entries[slot];
        if (entry.Stamp == _textStamp && ReferenceEquals(entry.Item, item))
        {
            value = entry.Value;
            return entry.Text!;
        }

        value = ReadColumnValue(item, column);
        string text = FormatCellValue(value, column);
        entry.Item = item;
        entry.Value = value;
        entry.Text = text;
        entry.Stamp = _textStamp;
        return text;
    }

    /// <summary>
    /// Moves on whenever the rows or what a cell may show can have changed: a rebuild, a collection change, a cell or
    /// item property change, a redraw request. Layout compares it with the version an Auto column was measured at
    /// (#24), so a frame of unchanged data measures nothing. It is not raised as an event and may wrap around.
    /// </summary>
    internal int ContentVersion { get; private set; }

    /// <summary>Mark every cached display text stale.</summary>
    private void InvalidateCellText()
    {
        ContentVersion++;
        if (++_textStamp == 0) _textStamp = 1;
    }

    /// <summary>
    /// Mark the cached display text of <paramref name="item"/> stale in the columns a change of
    /// <paramref name="propertyName"/> (null or empty: every property) can affect.
    /// </summary>
    private void InvalidateCellText(object item, string? propertyName)
    {
        ContentVersion++;
        foreach (var col in _columns)
        {
            var cache = col.TextCache;
            if (cache == null || !ReferenceEquals(cache.Owner, this)) continue;
            // A value accessor or the item itself (empty path) may depend on any property.
            if (string.IsNullOrEmpty(propertyName) || col.ValueAccessor != null
                || string.IsNullOrEmpty(col.PropertyName) || PropertyPathAffected(col.PropertyName, propertyName))
                cache.Invalidate(item);
        }
    }

    /// <summary>
    /// Attempts an incremental update of the view indices for a single-item Add or Remove event when no
    /// sort, grouping or summary is active. Column filters and the filter predicate do not prevent it: an added
    /// item is tested against them and gets a row only if it passes, and a removed item that has no row only
    /// shifts the source indices after it. Returns false if a full rebuild is needed.
    /// <paramref name="viewIndex"/> is the visible row that was inserted or removed; <paramref name="rowsChanged"/>
    /// is false when the view's rows are the same (the item was filtered out), though source indices moved.
    /// A view over a copy of the items applies the change to its copy too, so the copy keeps matching the collection.
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
        bool hasGroup = _groupDescriptions.Count > 0 || _view.FlatView != null;
        bool hasSummaries = _tableSummaryRows.Count > 0 || _groupSummaryRows.Count > 0 ||
                            _captionSummary?.Columns.Count > 0;

        if (hasSort || hasGroup || hasSummaries)
            return false;

        // A copy of the items must have matched the collection before this change.
        var copy = _view.Items as ItemBuffer;
        if (_view.ItemsAreCopy && (copy == null || _view.CollectionVersion != _collectionVersion - 1))
            return false;

        var indices = _view.ViewIndices;
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is { Count: 1 })
        {
            int sourceIndex = e.NewStartingIndex;
            if (sourceIndex < 0) return false; // Index not provided — fall back
            if (copy != null)
            {
                if (sourceIndex > copy.Count) return false;
                copy.InsertItem(sourceIndex, e.NewItems[0]);
                _view.CollectionVersion = _collectionVersion;
                _view.SourceIndexByItem = null;
            }

            // The new item gets a row only if it passes the filter predicate and the column filters.
            bool visible = e.NewItems[0] is { } added && PassesFilters(added, PrepareFilters(comparer: _comparer));

            // Shift existing view indices that are >= the inserted position
            for (int i = 0; i < indices.Count; i++)
            {
                if (indices[i] >= sourceIndex)
                    indices[i]++;
            }

            if (!visible)
            {
                rowsChanged = false;
                return true;
            }

            // No sort, so the new row goes at the insertion position: find where to insert in
            // the view indices to maintain source-order
            int insertAt = indices.Count;
            for (int i = 0; i < indices.Count; i++)
            {
                if (indices[i] > sourceIndex)
                {
                    insertAt = i;
                    break;
                }
            }
            indices.Insert(insertAt, sourceIndex);
            viewIndex = insertAt;

            return true;
        }

        if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems is { Count: 1 })
        {
            int sourceIndex = e.OldStartingIndex;
            if (sourceIndex < 0) return false; // Index not provided — fall back
            if (copy != null)
            {
                if (sourceIndex >= copy.Count || !ReferenceEquals(copy[sourceIndex], e.OldItems[0])) return false;
                copy.RemoveItemAt(sourceIndex);
                _view.CollectionVersion = _collectionVersion;
                _view.SourceIndexByItem = null;
            }

            // Remove the entry (if the item had a row) and shift indices above the removed position
            viewIndex = indices.IndexOf(sourceIndex);
            if (viewIndex >= 0)
                indices.RemoveAt(viewIndex);
            else
                rowsChanged = false; // the item was filtered out
            for (int i = 0; i < indices.Count; i++)
            {
                if (indices[i] > sourceIndex)
                    indices[i]--;
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Rebuild the visible rows from the source items and the current state, synchronously, and publish them. Raises
    /// nothing. Supersedes a pending background rebuild. While background rebuilds are enabled the view reads a copy
    /// of the items, so later collection changes leave it readable until the next rebuild.
    /// </summary>
    private void RebuildCore()
    {
        _viewStale = false;
        CancelBackgroundRebuild();
        bool copy = BackgroundRebuildsEnabled;
        var oldItems = _view.Items as ItemBuffer;
        var settings = CaptureBuildSettings(copy ? CopyItems() : _sourceItems, copy, forWorker: false);
        PublishView(ViewBuilder.Build(settings, _view, CancellationToken.None));
        if (!ReferenceEquals(oldItems, _view.Items)) ReturnItemBuffer(oldItems);
        CompleteViewUpdate();
    }

    /// <summary>Make <paramref name="view"/> the view every read answers from.</summary>
    private void PublishView(ViewSnapshot view)
    {
        _view = view;
        _viewStale = false;
        InvalidateCellText();
    }

    /// <summary>
    /// Capture what a view build reads, on the source's thread: the items (<paramref name="items"/>) and the sort,
    /// filter, grouping and summary settings. For a build on a worker (<paramref name="forWorker"/>) the settings are
    /// copies and the comparer's culture is fixed, so the build never reads the source, its columns or the current
    /// culture of another thread.
    /// </summary>
    private ViewBuildSettings CaptureBuildSettings(IList items, bool itemsAreCopy, bool forWorker)
    {
        var comparer = forWorker ? new KumikoComparer(_comparer.Culture, _comparer.Options) : _comparer;

        // Sorted columns in priority order; a column with a direction but no order sorts alone (as order 1).
        var sortedColumns = _columns
            .Where(c => c.SortDirection != KumikoSortDirection.None && c.SortOrder > 0)
            .OrderBy(c => c.SortOrder)
            .ToList();
        if (sortedColumns.Count == 0)
        {
            var fallback = _columns.FirstOrDefault(c => c.SortDirection != KumikoSortDirection.None);
            if (fallback != null)
            {
                fallback.SortOrder = 1;
                sortedColumns.Add(fallback);
            }
        }
        var sorts = new SortSpec[sortedColumns.Count];
        for (int i = 0; i < sorts.Length; i++)
        {
            var col = sortedColumns[i];
            sorts[i] = new SortSpec(new ItemReader(col), col.CustomComparer ?? (IComparer)comparer,
                col.SortDirection == KumikoSortDirection.Ascending ? 1 : -1);
        }

        var groups = new GroupSpec[_groupDescriptions.Count];
        for (int i = 0; i < groups.Length; i++)
        {
            var desc = _groupDescriptions[i];
            var column = _columns.FirstOrDefault(c =>
                string.Equals(c.PropertyName, desc.PropertyName, StringComparison.OrdinalIgnoreCase));
            groups[i] = new GroupSpec(
                desc.PropertyName,
                desc.Header ?? column?.Header ?? desc.PropertyName,
                column != null ? new ItemReader(column) : null,
                column?.Format,
                (desc.KeySelector == null ? column?.CustomComparer : null) ?? comparer,
                desc.KeySelector,
                desc.DisplayTextSelector,
                desc.GroupSortDirection);
        }

        var readers = new Dictionary<string, ItemReader>(StringComparer.OrdinalIgnoreCase);
        SummaryRowSpec[] Rows(IReadOnlyList<SummaryRowSpec> rows)
        {
            foreach (var row in rows)
                foreach (var column in row.Columns)
                    if (!readers.ContainsKey(column.PropertyName))
                        readers[column.PropertyName] = new ItemReader(FindSummaryColumn(column.PropertyName));
            return rows as SummaryRowSpec[] ?? rows.ToArray();
        }
        var tableSummaries = Rows(_tableSummaryRows.Select(r => new SummaryRowSpec(r.Name, r.Title, r.Position,
            GroupSummaryLevels.All, Describe(r.Columns, forWorker))).ToArray());
        var groupSummaries = Rows(_groupSummaryRows.Select(r => new SummaryRowSpec(r.Name, r.Title,
            SummaryPosition.Bottom, r.Levels, Describe(r.Columns, forWorker))).ToArray());
        CaptionSpec? caption = null;
        if (_captionSummary != null)
        {
            caption = new CaptionSpec(Describe(_captionSummary.Columns, forWorker), _captionSummary.FormatTemplate);
            Rows(new[] { new SummaryRowSpec("", null, SummaryPosition.Bottom, GroupSummaryLevels.All, caption.Columns) });
        }

        return new ViewBuildSettings
        {
            Items = items,
            ItemsAreCopy = itemsAreCopy,
            CollectionVersion = _collectionVersion,
            Comparer = comparer,
            Strings = _strings,
            Predicate = _filterPredicate,
            Filters = PrepareFilters(comparer: comparer, forWorker: forWorker),
            // A first background build allocates the buffer the next copy reuses (see CopyItems).
            AllocateSpareItems = forWorker && _spareItems == null && _view.Items is not ItemBuffer,
            Sorts = sorts,
            Groups = groups,
            ExpandedState = forWorker ? new Dictionary<GroupKeyPath, bool>(_groupExpandedState) : _groupExpandedState,
            DefaultExpanded = _defaultGroupExpanded,
            TableSummaries = tableSummaries,
            GroupSummaries = groupSummaries,
            Caption = caption,
            SummaryReaders = readers,
            BuildLookups = forWorker && _viewChanged != null,
        };
    }

    /// <summary>The summary descriptions of a row: the list itself, or copies of them for a build on a worker.</summary>
    private static IReadOnlyList<SummaryColumnDescription> Describe(List<SummaryColumnDescription> columns, bool copy) =>
        !copy
            ? columns
            : columns.Select(c => new SummaryColumnDescription
            {
                PropertyName = c.PropertyName,
                SummaryType = c.SummaryType,
                Format = c.Format,
                Label = c.Label,
                CustomAggregate = c.CustomAggregate,
            }).ToArray();

    /// <summary>The group key and display text <paramref name="item"/> falls under for one group description.</summary>
    private (object? Key, string DisplayText) ComputeGroupKey(GroupDescription groupDesc, KumikoColumn? column, object item)
    {
        object? rawValue = column != null ? ReadColumnValue(item, column) : null;
        object? groupKey = groupDesc.KeySelector != null ? groupDesc.KeySelector(rawValue) : rawValue;
        string displayText = groupDesc.DisplayTextSelector != null
            ? groupDesc.DisplayTextSelector(groupKey)
            : groupKey is null
                ? _strings.BlankGroupText
                : _comparer.Format(groupKey, groupDesc.KeySelector == null ? column?.Format : null);
        return (groupKey, displayText);
    }

    // ── Summary updates ──────────────────────────────────────────
    //
    // The published view computes every summary when it is built (ViewBuilder). Between rebuilds, an item's
    // PropertyChanged of a summarized property updates the summaries in place where it can (#69, #254).

    /// <summary>
    /// Compute the table-level summaries (top/bottom) of the published view again from its visible data rows, without
    /// filtering, sorting or grouping them again.
    /// </summary>
    private void ComputeTableSummaries()
    {
        var view = _view;
        ViewBuilder.ComputeTableSummaries(view, CaptureBuildSettings(view.Items, view.ItemsAreCopy, forWorker: false),
            CancellationToken.None);
    }

    /// <summary>
    /// Keep the table summaries current after <paramref name="item"/> raised <c>PropertyChanged</c> for
    /// <paramref name="changed"/>, a property that does not change the visible rows (see
    /// <see cref="ItemPropertyImpact"/>). Sum and Average subtract the item's old value and add its new one,
    /// O(1); Count does not change. A property with a Min, Max or Custom aggregate, or an item whose old value is
    /// unknown, recomputes the table summaries over the visible rows (no filtering, sorting or grouping), once per
    /// batch; under background rebuilds, that recompute is a background rebuild.
    /// </summary>
    private void UpdateTableSummaries(object? item, string changed)
    {
        var view = _view;
        bool affected = false;
        bool inPlace = item != null;
        foreach (var aggregate in view.TableAggregates.Values)
        {
            if (!PropertyPathAffected(aggregate.PropertyName, changed) || !aggregate.DependsOnValues) continue;
            affected = true;
            if (aggregate.Values == null) inPlace = false;
        }
        if (!affected) return;                           // not summarized, or Count only
        if (_pendingRebuild || _pendingSummaries) return; // the batch recomputes them at EndUpdate anyway

        int index = -1;
        if (inPlace && (view.Items.Count != view.TableValuesCount || !TryGetSourceIndex(item!, out index)))
            inPlace = false; // a duplicate item, or the collection changed without a notification

        if (inPlace)
        {
            bool updated = false;
            foreach (var aggregate in view.TableAggregates.Values)
            {
                var values = aggregate.Values;
                if (values == null || !PropertyPathAffected(aggregate.PropertyName, changed)) continue;
                var oldValue = values[index];
                if (ReferenceEquals(oldValue, PropertyAggregate.NoRow)) continue; // the item has no visible row
                var newValue = aggregate.Reader(item!);
                values[index] = newValue;
                updated = true;
                if (!aggregate.Replace(oldValue, newValue))
                {
                    inPlace = false; // the exact decimal total no longer fits: recompute it in order
                    break;
                }
            }

            if (inPlace)
            {
                if (updated)
                    RebuildTableSummaryRows(changed);
                return;
            }
        }

        if (_updateDepth > 0)
            _pendingSummaries = true;
        else if (UseBackgroundRebuild())
            ScheduleBackgroundRebuild();
        else
            ComputeTableSummaries();
    }

    /// <summary>Rebuild the computed rows of the table summaries that aggregate a property a change of <paramref name="changed"/> affects.</summary>
    private void RebuildTableSummaryRows(string changed)
    {
        var aggregates = _view.TableAggregates;
        foreach (var slot in _view.TableSummarySlots)
        {
            if (UsesProperty(slot.Columns, changed))
                slot.List[slot.Index] = SummaryText.BuildRow(slot.Name, slot.Title, slot.Columns,
                    p => aggregates.TryGetValue(p, out var a) ? a : null, _comparer.Culture, _strings);
        }
    }

    /// <summary>
    /// Keep the group and caption summaries of the groups that contain <paramref name="item"/> current after it raised
    /// <c>PropertyChanged</c> for <paramref name="changed"/> (#254): Sum, Average and Count follow the item's old and
    /// new value, and only the texts of those groups are composed again. False when the change cannot be applied in
    /// place (a Min, Max or Custom aggregate of the property, an unknown item, an overflowing total): the view must
    /// be rebuilt.
    /// </summary>
    private bool UpdateGroupSummaries(object? item, string changed)
    {
        var view = _view;
        var settings = view.GroupSummaries;
        if (settings == null || view.FlatView == null) return true; // the published view has none; a rebuild reads the new value
        if (_pendingRebuild) return true;                           // the batch rebuilds at EndUpdate anyway

        bool affected = false;
        for (int k = 0; k < settings.Properties.Length; k++)
        {
            if (!PropertyPathAffected(settings.Properties[k], changed)) continue;
            if (!settings.InPlace[k]) return false;
            affected = true;
        }
        if (!affected) return true;

        var innermost = view.InnermostGroup;
        if (item == null || innermost == null || view.Items.Count != innermost.Length ||
            !TryGetSourceIndex(item, out int index))
            return false;
        var group = innermost[index];
        if (group == null) return true; // the item has no row (a filter hides it)

        for (int k = 0; k < settings.Properties.Length; k++)
        {
            if (!PropertyPathAffected(settings.Properties[k], changed)) continue;
            var values = settings.Values[k]!;
            var oldValue = values[index];
            var newValue = settings.Readers[k].Read(item);
            values[index] = newValue;
            for (var g = group; g != null; g = g.Parent)
            {
                if (g.SummaryAggregates?[k] is { } aggregate && !aggregate.Replace(oldValue, newValue))
                    return false; // the exact decimal total no longer fits: rebuild
            }
        }

        for (var g = group; g != null; g = g.Parent)
        {
            if (g.SummaryAggregates is not { } aggregates) continue;
            if (settings.Caption is { } caption && UsesProperty(caption.Columns, changed))
                g.CaptionText = ViewBuilder.BuildCaption(settings, aggregates, _comparer, _strings);
            if (g.Summaries is { } rows && g.SummaryRowSpecs is { } specs)
            {
                for (int i = 0; i < rows.Count && i < specs.Length; i++)
                {
                    if (UsesProperty(specs[i].Columns, changed))
                        rows[i] = ViewBuilder.BuildGroupRow(settings, aggregates, specs[i], _comparer, _strings);
                }
            }
        }
        return true;
    }

    /// <summary>
    /// The index of <paramref name="item"/> in the published view's items, from a map built on first use (or by a
    /// background build). False for an item that is not there or appears more than once (one notification, several
    /// rows).
    /// </summary>
    private bool TryGetSourceIndex(object item, out int index)
    {
        var view = _view;
        var map = view.SourceIndexByItem ??= ViewBuilder.SourceIndexMap(view.Items);
        return map.TryGetValue(item, out index) && index >= 0;
    }

    /// <summary>The first column bound to <paramref name="propertyName"/> (ignoring case), or a column made for it.</summary>
    private KumikoColumn FindSummaryColumn(string propertyName)
    {
        foreach (var column in _columns)
        {
            if (string.Equals(column.PropertyName, propertyName, StringComparison.OrdinalIgnoreCase))
                return column;
        }
        return new KumikoColumn { PropertyName = propertyName };
    }
}

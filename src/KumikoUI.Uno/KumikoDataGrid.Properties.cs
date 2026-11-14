using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Models;
using Microsoft.UI.Xaml;

namespace KumikoUI.Uno;

/// <summary>
/// <see cref="DependencyProperty"/> surface for <see cref="KumikoDataGrid"/>,
/// mapped 1:1 from the <c>KumikoUI.Maui.KumikoDataGrid</c> <c>BindableProperty</c>
/// set (same names, same defaults). WinUI callbacks are <c>static</c> and receive
/// the instance via <c>d</c>; the body logic is otherwise identical to the MAUI
/// <c>propertyChanged</c> handlers.
/// </summary>
public partial class KumikoDataGrid
{
    // ── ItemsSource ───────────────────────────────────────────────

    /// <summary>Identifies the <see cref="ItemsSource"/> dependency property.</summary>
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource), typeof(IEnumerable), typeof(KumikoDataGrid),
            new PropertyMetadata(null, OnItemsSourceChanged));

    /// <summary>The data items. Hooks <see cref="INotifyCollectionChanged"/> and pushes to the data source.</summary>
    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    private INotifyCollectionChanged? _boundCollectionChangedSource;

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (KumikoDataGrid)d;

        // Unsubscribe from the old collection's CollectionChanged.
        if (view._boundCollectionChangedSource != null)
        {
            view._boundCollectionChangedSource.CollectionChanged -= view.OnBoundItemsCollectionChanged;
            view._boundCollectionChangedSource = null;
        }

        if (e.NewValue is IEnumerable newValue)
        {
            view._dataSource.SetItems(newValue);

            if (!view.IsObservingCollections)
            {
                // Set while unloaded: SetItems subscribed, so release it; OnLoaded re-attaches
                view._dataSource.Detach();
            }
            else if (newValue is INotifyCollectionChanged incc)
            {
                view._boundCollectionChangedSource = incc;
                view._boundCollectionChangedSource.CollectionChanged += view.OnBoundItemsCollectionChanged;
            }
        }
        else
        {
            view._dataSource.SetItems(Array.Empty<object>());
        }

        view.Invalidate();
    }

    private void OnBoundItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // KumikoSource already rebuilds the view; we just ensure the canvas
        // repaints. A change raised on a background thread never gets here:
        // KumikoSource subscribed first and throws (#69), then resyncs on the
        // UI thread and raises ViewChanged. The dispatch is a safety net.
        if (DispatcherQueue is { } queue && !queue.HasThreadAccess)
            queue.TryEnqueue(Invalidate);
        else
            Invalidate();
    }

    // ── Columns ───────────────────────────────────────────────────

    /// <summary>Identifies the <see cref="Columns"/> dependency property.</summary>
    public static readonly DependencyProperty ColumnsProperty =
        DependencyProperty.Register(
            nameof(Columns), typeof(ObservableCollection<KumikoColumn>), typeof(KumikoDataGrid),
            new PropertyMetadata(null, OnColumnsChanged));

    /// <summary>Bindable collection of column definitions. Supports runtime add/remove. Never null:
    /// assigning null sets an empty collection.</summary>
    public ObservableCollection<KumikoColumn> Columns
    {
        get => (ObservableCollection<KumikoColumn>)GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    private static void OnColumnsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (KumikoDataGrid)d;

        // Never null: null becomes an empty collection (which re-enters this callback)
        if (e.NewValue is null)
        {
            if (e.OldValue is ObservableCollection<KumikoColumn> previous)
                previous.CollectionChanged -= view.OnColumnsCollectionChanged;
            view.Columns = new ObservableCollection<KumikoColumn>();
            return;
        }

        if (e.OldValue is ObservableCollection<KumikoColumn> oldCollection)
            oldCollection.CollectionChanged -= view.OnColumnsCollectionChanged;

        if (e.NewValue is ObservableCollection<KumikoColumn> newCollection)
        {
            if (view.IsObservingCollections)
                newCollection.CollectionChanged += view.OnColumnsCollectionChanged;
            view._dataSource.SetColumns(newCollection);
            view.SyncActionButtonDefinitions();
        }
        else
        {
            view._dataSource.SetColumns(Array.Empty<KumikoColumn>());
        }

        view.Invalidate();
    }

    private void OnColumnsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_syncingFromSource) return; // the grid is copying the source's column order in (KumikoDataGrid.Layout.cs)
        _dataSource.SetColumns(Columns);
        SyncActionButtonDefinitions();
        Invalidate();
    }

    // ── TableSummaryRows ──────────────────────────────────────────

    /// <summary>Identifies the <see cref="TableSummaryRows"/> dependency property.</summary>
    public static readonly DependencyProperty TableSummaryRowsProperty =
        DependencyProperty.Register(
            nameof(TableSummaryRows), typeof(ObservableCollection<TableSummaryRow>), typeof(KumikoDataGrid),
            new PropertyMetadata(null, OnTableSummaryRowsChanged));

    /// <summary>Bindable collection of table summary row definitions. Never null: assigning null sets an
    /// empty collection.</summary>
    public ObservableCollection<TableSummaryRow> TableSummaryRows
    {
        get => (ObservableCollection<TableSummaryRow>)GetValue(TableSummaryRowsProperty);
        set => SetValue(TableSummaryRowsProperty, value);
    }

    private static void OnTableSummaryRowsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (KumikoDataGrid)d;

        // Never null: null becomes an empty collection (which re-enters this callback)
        if (e.NewValue is null)
        {
            if (e.OldValue is ObservableCollection<TableSummaryRow> previous)
                previous.CollectionChanged -= view.OnTableSummaryRowsCollectionChanged;
            view.TableSummaryRows = new ObservableCollection<TableSummaryRow>();
            return;
        }

        if (e.OldValue is ObservableCollection<TableSummaryRow> oldCollection)
            oldCollection.CollectionChanged -= view.OnTableSummaryRowsCollectionChanged;

        if (e.NewValue is ObservableCollection<TableSummaryRow> newCollection)
        {
            if (view.IsObservingCollections)
                newCollection.CollectionChanged += view.OnTableSummaryRowsCollectionChanged;
            view.SyncTableSummaryRows(newCollection);
        }
        else
        {
            view._dataSource.ClearTableSummaryRows();
        }
    }

    private void OnTableSummaryRowsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SyncTableSummaryRows(TableSummaryRows);
    }

    private void SyncTableSummaryRows(IEnumerable<TableSummaryRow> rows)
    {
        _dataSource.ClearTableSummaryRows();
        foreach (var row in rows)
            _dataSource.AddTableSummaryRow(row);
        Invalidate();
    }

    // ── GridSelectionMode ─────────────────────────────────────────

    /// <summary>Identifies the <see cref="GridSelectionMode"/> dependency property.</summary>
    public static readonly DependencyProperty GridSelectionModeProperty =
        DependencyProperty.Register(
            nameof(GridSelectionMode), typeof(KumikoSelectionMode), typeof(KumikoDataGrid),
            new PropertyMetadata(KumikoSelectionMode.Extended, OnGridSelectionModeChanged));

    /// <summary>Selection mode: None, Single, Multiple, or Extended.</summary>
    public KumikoSelectionMode GridSelectionMode
    {
        get => (KumikoSelectionMode)GetValue(GridSelectionModeProperty);
        set => SetValue(GridSelectionModeProperty, value);
    }

    private static void OnGridSelectionModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (KumikoDataGrid)d;
        view._selection.Mode = (KumikoSelectionMode)e.NewValue;
        view.Invalidate();
    }

    // ── ClearSelectionOnEmptyAreaTap ──────────────────────────────

    /// <summary>Identifies the <see cref="ClearSelectionOnEmptyAreaTap"/> dependency property.</summary>
    public static readonly DependencyProperty ClearSelectionOnEmptyAreaTapProperty =
        DependencyProperty.Register(
            nameof(ClearSelectionOnEmptyAreaTap), typeof(bool), typeof(KumikoDataGrid),
            new PropertyMetadata(true, OnClearSelectionOnEmptyAreaTapChanged));

    /// <summary>
    /// When true (default), a tap on the empty part of the rows area — below the last row or right of the
    /// last column — clears the selection and the current cell.
    /// </summary>
    public bool ClearSelectionOnEmptyAreaTap
    {
        get => (bool)GetValue(ClearSelectionOnEmptyAreaTapProperty);
        set => SetValue(ClearSelectionOnEmptyAreaTapProperty, value);
    }

    private static void OnClearSelectionOnEmptyAreaTapChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((KumikoDataGrid)d)._selection.ClearSelectionOnEmptyAreaTap = (bool)e.NewValue;

    // ── RowHeight ─────────────────────────────────────────────────

    /// <summary>Identifies the <see cref="RowHeight"/> dependency property.</summary>
    public static readonly DependencyProperty RowHeightProperty =
        DependencyProperty.Register(
            nameof(RowHeight), typeof(double), typeof(KumikoDataGrid),
            new PropertyMetadata(36d, OnRowHeightChanged));

    /// <summary>Height of each data row in (logical) pixels.</summary>
    public double RowHeight
    {
        get => (double)GetValue(RowHeightProperty);
        set => SetValue(RowHeightProperty, value);
    }

    private static void OnRowHeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (KumikoDataGrid)d;
        view._styleOptions.RowHeight = (float)(double)e.NewValue;
        view.ApplyStyleOptions();
        view.Invalidate();
    }

    // ── HeaderHeight ──────────────────────────────────────────────

    /// <summary>Identifies the <see cref="HeaderHeight"/> dependency property.</summary>
    public static readonly DependencyProperty HeaderHeightProperty =
        DependencyProperty.Register(
            nameof(HeaderHeight), typeof(double), typeof(KumikoDataGrid),
            new PropertyMetadata(40d, OnHeaderHeightChanged));

    /// <summary>Height of the column header row in (logical) pixels.</summary>
    public double HeaderHeight
    {
        get => (double)GetValue(HeaderHeightProperty);
        set => SetValue(HeaderHeightProperty, value);
    }

    private static void OnHeaderHeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (KumikoDataGrid)d;
        view._styleOptions.HeaderHeight = (float)(double)e.NewValue;
        view.ApplyStyleOptions();
        view.Invalidate();
    }

    // ── BackgroundRebuildThreshold ───────────────────────────────

    /// <summary>Identifies the <see cref="BackgroundRebuildThreshold"/> dependency property.</summary>
    public static readonly DependencyProperty BackgroundRebuildThresholdProperty =
        DependencyProperty.Register(
            nameof(BackgroundRebuildThreshold), typeof(int), typeof(KumikoDataGrid),
            new PropertyMetadata(KumikoSource.DefaultBackgroundRebuildThreshold, OnBackgroundRebuildThresholdChanged));

    /// <summary>
    /// The number of items from which sorting, filtering, grouping and summaries are rebuilt on a worker thread
    /// instead of the UI thread (forwarded to <see cref="KumikoSource.BackgroundRebuildThreshold"/> of
    /// <see cref="DataSource"/>). Default <see cref="KumikoSource.DefaultBackgroundRebuildThreshold"/> (10,000).
    /// Below it rebuilds are synchronous; <see cref="int.MaxValue"/> keeps every rebuild synchronous. During a
    /// background rebuild the grid keeps drawing the current rows, then draws the new view when it is swapped in;
    /// selection, the current cell and an open edit follow their items. Item properties, comparers, the filter
    /// predicate and group selectors must be safe to read from a worker while a rebuild runs.
    /// Setting a negative value throws <see cref="ArgumentOutOfRangeException"/>.
    /// In the browser (single-threaded WebAssembly, no worker to build on) the grid sets it to
    /// <see cref="int.MaxValue"/>, so rebuilds stay synchronous there.
    /// </summary>
    public int BackgroundRebuildThreshold
    {
        get => (int)GetValue(BackgroundRebuildThresholdProperty);
        set => SetValue(BackgroundRebuildThresholdProperty, value);
    }

    private static void OnBackgroundRebuildThresholdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((KumikoDataGrid)d)._dataSource.BackgroundRebuildThreshold = (int)e.NewValue; // throws for a negative value

    // ── FrozenRowCount ────────────────────────────────────────────

    /// <summary>Identifies the <see cref="FrozenRowCount"/> dependency property.</summary>
    public static readonly DependencyProperty FrozenRowCountProperty =
        DependencyProperty.Register(
            nameof(FrozenRowCount), typeof(int), typeof(KumikoDataGrid),
            new PropertyMetadata(0, OnFrozenRowCountChanged));

    /// <summary>Number of top data rows to freeze when scrolling vertically.</summary>
    public int FrozenRowCount
    {
        get => (int)GetValue(FrozenRowCountProperty);
        set => SetValue(FrozenRowCountProperty, value);
    }

    private static void OnFrozenRowCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (KumikoDataGrid)d;
        view._dataSource.FrozenRowCount = (int)e.NewValue;
        view.Invalidate();
    }

    // ── EditTriggers ──────────────────────────────────────────────

    /// <summary>Identifies the <see cref="EditTriggers"/> dependency property.</summary>
    public static readonly DependencyProperty EditTriggersProperty =
        DependencyProperty.Register(
            nameof(EditTriggers), typeof(EditTrigger), typeof(KumikoDataGrid),
            new PropertyMetadata(EditTrigger.Default, OnEditTriggersChanged));

    /// <summary>Which user actions trigger cell editing. Default: DoubleTap | F2Key | Typing.</summary>
    public EditTrigger EditTriggers
    {
        get => (EditTrigger)GetValue(EditTriggersProperty);
        set => SetValue(EditTriggersProperty, value);
    }

    private static void OnEditTriggersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (KumikoDataGrid)d;
        view._editSession.EditTriggers = (EditTrigger)e.NewValue;
    }

    // ── EditTextSelectionMode ─────────────────────────────────────

    /// <summary>Identifies the <see cref="EditTextSelectionMode"/> dependency property.</summary>
    public static readonly DependencyProperty EditTextSelectionModeProperty =
        DependencyProperty.Register(
            nameof(EditTextSelectionMode), typeof(EditTextSelectionMode), typeof(KumikoDataGrid),
            new PropertyMetadata(EditTextSelectionMode.SelectAll, OnEditTextSelectionModeChanged));

    /// <summary>How text is selected when a cell enters edit mode. Default: SelectAll.</summary>
    public EditTextSelectionMode EditTextSelectionMode
    {
        get => (EditTextSelectionMode)GetValue(EditTextSelectionModeProperty);
        set => SetValue(EditTextSelectionModeProperty, value);
    }

    private static void OnEditTextSelectionModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (KumikoDataGrid)d;
        view._editSession.TextSelectionMode = (EditTextSelectionMode)e.NewValue;
    }

    // ── ShowFrameStats ────────────────────────────────────────────

    /// <summary>Identifies the <see cref="ShowFrameStats"/> dependency property.</summary>
    public static readonly DependencyProperty ShowFrameStatsProperty =
        DependencyProperty.Register(
            nameof(ShowFrameStats), typeof(bool), typeof(KumikoDataGrid),
            new PropertyMetadata(false, OnShowFrameStatsChanged));

    /// <summary>
    /// Draws a frame-time overlay (render time, frames per second, bytes allocated per frame) in the grid's
    /// top-right corner. A debugging aid, off by default; when off it costs nothing per frame.
    /// </summary>
    public bool ShowFrameStats
    {
        get => (bool)GetValue(ShowFrameStatsProperty);
        set => SetValue(ShowFrameStatsProperty, value);
    }

    private static void OnShowFrameStatsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (KumikoDataGrid)d;
        view._renderer.ShowFrameStats = (bool)e.NewValue;
        view.Invalidate();
    }

    // ── IsReadOnly ────────────────────────────────────────────────

    /// <summary>Identifies the <see cref="IsReadOnly"/> dependency property.</summary>
    public static readonly DependencyProperty IsReadOnlyProperty =
        DependencyProperty.Register(
            nameof(IsReadOnly), typeof(bool), typeof(KumikoDataGrid),
            new PropertyMetadata(false, OnIsReadOnlyChanged));

    /// <summary>
    /// When true, no cell can be edited, Boolean cells do not toggle and Tab skips every cell; combines with
    /// the per-column <c>IsReadOnly</c> (either one makes a cell read-only).
    /// </summary>
    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    private static void OnIsReadOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (KumikoDataGrid)d;
        view._editSession.IsReadOnly = (bool)e.NewValue;
        view.Invalidate();
    }

    // ── AllowSorting ──────────────────────────────────────────────

    /// <summary>Identifies the <see cref="AllowSorting"/> dependency property.</summary>
    public static readonly DependencyProperty AllowSortingProperty =
        DependencyProperty.Register(
            nameof(AllowSorting), typeof(bool), typeof(KumikoDataGrid),
            new PropertyMetadata(true, OnAllowSortingChanged));

    /// <summary>When false, column header taps do not sort (a column's own <c>AllowSorting</c> also applies).</summary>
    public bool AllowSorting
    {
        get => (bool)GetValue(AllowSortingProperty);
        set => SetValue(AllowSortingProperty, value);
    }

    private static void OnAllowSortingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (KumikoDataGrid)d;
        view._inputController.AllowSorting = (bool)e.NewValue;
        view.Invalidate();
    }

    // ── AllowFiltering ────────────────────────────────────────────

    /// <summary>Identifies the <see cref="AllowFiltering"/> dependency property.</summary>
    public static readonly DependencyProperty AllowFilteringProperty =
        DependencyProperty.Register(
            nameof(AllowFiltering), typeof(bool), typeof(KumikoDataGrid),
            new PropertyMetadata(true, OnAllowFilteringChanged));

    /// <summary>
    /// When false, filter icons are not shown in column headers and cannot be tapped (a column's own
    /// <c>AllowFiltering</c> also applies).
    /// </summary>
    public bool AllowFiltering
    {
        get => (bool)GetValue(AllowFilteringProperty);
        set => SetValue(AllowFilteringProperty, value);
    }

    private static void OnAllowFilteringChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (KumikoDataGrid)d;
        view._inputController.AllowFiltering = (bool)e.NewValue;
        view._renderer.AllowFiltering = (bool)e.NewValue;
        view.Invalidate();
    }

    // ── DismissKeyboardOnEnter ────────────────────────────────────

    /// <summary>Identifies the <see cref="DismissKeyboardOnEnter"/> dependency property.</summary>
    public static readonly DependencyProperty DismissKeyboardOnEnterProperty =
        DependencyProperty.Register(
            nameof(DismissKeyboardOnEnter), typeof(bool), typeof(KumikoDataGrid),
            new PropertyMetadata(true, OnDismissKeyboardOnEnterChanged));

    /// <summary>
    /// When true (default), pressing Enter while editing commits and dismisses
    /// the keyboard. When false, Enter commits and begins editing the cell below.
    /// </summary>
    public bool DismissKeyboardOnEnter
    {
        get => (bool)GetValue(DismissKeyboardOnEnterProperty);
        set => SetValue(DismissKeyboardOnEnterProperty, value);
    }

    private static void OnDismissKeyboardOnEnterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (KumikoDataGrid)d;
        view._editSession.DismissKeyboardOnEnter = (bool)e.NewValue;
    }

    // ── GridDescription ───────────────────────────────────────────

    /// <summary>Identifies the <see cref="GridDescription"/> dependency property.</summary>
    public static readonly DependencyProperty GridDescriptionProperty =
        DependencyProperty.Register(
            nameof(GridDescription), typeof(string), typeof(KumikoDataGrid),
            new PropertyMetadata(null, OnGridDescriptionChanged));

    /// <summary>
    /// Accessible name for screen readers. Null (the default) uses <see cref="KumikoStrings.AccessibleGridName"/>
    /// of <see cref="KumikoSource.Strings"/> on <see cref="DataSource"/>, so the name follows the grid's localization.
    /// </summary>
    public string? GridDescription
    {
        get => (string?)GetValue(GridDescriptionProperty);
        set => SetValue(GridDescriptionProperty, value);
    }

    private static void OnGridDescriptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((KumikoDataGrid)d).UpdateAccessibleName();
}

using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Models;
using Microsoft.UI.Xaml;

// Disambiguate Core's SelectionMode from Microsoft.UI.Xaml.Controls.SelectionMode.
using SelectionMode = KumikoUI.Core.Models.SelectionMode;

namespace KumikoUI.Uno;

/// <summary>
/// <see cref="DependencyProperty"/> surface for <see cref="DataGridView"/>,
/// mapped 1:1 from the <c>KumikoUI.Maui.DataGridView</c> <c>BindableProperty</c>
/// set (same names, same defaults). WinUI callbacks are <c>static</c> and receive
/// the instance via <c>d</c>; the body logic is otherwise identical to the MAUI
/// <c>propertyChanged</c> handlers.
/// </summary>
public partial class DataGridView
{
    // ── ItemsSource ───────────────────────────────────────────────

    /// <summary>Identifies the <see cref="ItemsSource"/> dependency property.</summary>
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource), typeof(IEnumerable), typeof(DataGridView),
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
        var view = (DataGridView)d;

        // Unsubscribe from the old collection's CollectionChanged.
        if (view._boundCollectionChangedSource != null)
        {
            view._boundCollectionChangedSource.CollectionChanged -= view.OnBoundItemsCollectionChanged;
            view._boundCollectionChangedSource = null;
        }

        if (e.NewValue is IEnumerable newValue)
        {
            view._dataSource.SetItems(newValue);

            if (newValue is INotifyCollectionChanged incc)
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
        // DataGridSource already rebuilds the view; we just ensure the canvas
        // repaints. DispatcherQueue marshals to the UI thread if a background
        // thread raised the change.
        if (DispatcherQueue is { } queue && !queue.HasThreadAccess)
            queue.TryEnqueue(Invalidate);
        else
            Invalidate();
    }

    // ── Columns ───────────────────────────────────────────────────

    /// <summary>Identifies the <see cref="Columns"/> dependency property.</summary>
    public static readonly DependencyProperty ColumnsProperty =
        DependencyProperty.Register(
            nameof(Columns), typeof(ObservableCollection<DataGridColumn>), typeof(DataGridView),
            new PropertyMetadata(null, OnColumnsChanged));

    /// <summary>Bindable collection of column definitions. Supports runtime add/remove.</summary>
    public ObservableCollection<DataGridColumn>? Columns
    {
        get => (ObservableCollection<DataGridColumn>?)GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    private static void OnColumnsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (DataGridView)d;

        if (e.OldValue is ObservableCollection<DataGridColumn> oldCollection)
            oldCollection.CollectionChanged -= view.OnColumnsCollectionChanged;

        if (e.NewValue is ObservableCollection<DataGridColumn> newCollection)
        {
            newCollection.CollectionChanged += view.OnColumnsCollectionChanged;
            view._dataSource.SetColumns(newCollection);
        }
        else
        {
            view._dataSource.SetColumns(Array.Empty<DataGridColumn>());
        }

        view.Invalidate();
    }

    private void OnColumnsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (Columns != null)
            _dataSource.SetColumns(Columns);
        Invalidate();
    }

    // ── TableSummaryRows ──────────────────────────────────────────

    /// <summary>Identifies the <see cref="TableSummaryRows"/> dependency property.</summary>
    public static readonly DependencyProperty TableSummaryRowsProperty =
        DependencyProperty.Register(
            nameof(TableSummaryRows), typeof(ObservableCollection<TableSummaryRow>), typeof(DataGridView),
            new PropertyMetadata(null, OnTableSummaryRowsChanged));

    /// <summary>Bindable collection of table summary row definitions.</summary>
    public ObservableCollection<TableSummaryRow>? TableSummaryRows
    {
        get => (ObservableCollection<TableSummaryRow>?)GetValue(TableSummaryRowsProperty);
        set => SetValue(TableSummaryRowsProperty, value);
    }

    private static void OnTableSummaryRowsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (DataGridView)d;

        if (e.OldValue is ObservableCollection<TableSummaryRow> oldCollection)
            oldCollection.CollectionChanged -= view.OnTableSummaryRowsCollectionChanged;

        if (e.NewValue is ObservableCollection<TableSummaryRow> newCollection)
        {
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
        if (TableSummaryRows != null)
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
            nameof(GridSelectionMode), typeof(SelectionMode), typeof(DataGridView),
            new PropertyMetadata(SelectionMode.Extended, OnGridSelectionModeChanged));

    /// <summary>Selection mode: None, Single, Multiple, or Extended.</summary>
    public SelectionMode GridSelectionMode
    {
        get => (SelectionMode)GetValue(GridSelectionModeProperty);
        set => SetValue(GridSelectionModeProperty, value);
    }

    private static void OnGridSelectionModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (DataGridView)d;
        view._selection.Mode = (SelectionMode)e.NewValue;
        view.Invalidate();
    }

    // ── RowHeight ─────────────────────────────────────────────────

    /// <summary>Identifies the <see cref="RowHeight"/> dependency property.</summary>
    public static readonly DependencyProperty RowHeightProperty =
        DependencyProperty.Register(
            nameof(RowHeight), typeof(double), typeof(DataGridView),
            new PropertyMetadata(36d, OnRowHeightChanged));

    /// <summary>Height of each data row in (logical) pixels.</summary>
    public double RowHeight
    {
        get => (double)GetValue(RowHeightProperty);
        set => SetValue(RowHeightProperty, value);
    }

    private static void OnRowHeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (DataGridView)d;
        view._style.RowHeight = (float)(double)e.NewValue;
        view.Invalidate();
    }

    // ── HeaderHeight ──────────────────────────────────────────────

    /// <summary>Identifies the <see cref="HeaderHeight"/> dependency property.</summary>
    public static readonly DependencyProperty HeaderHeightProperty =
        DependencyProperty.Register(
            nameof(HeaderHeight), typeof(double), typeof(DataGridView),
            new PropertyMetadata(40d, OnHeaderHeightChanged));

    /// <summary>Height of the column header row in (logical) pixels.</summary>
    public double HeaderHeight
    {
        get => (double)GetValue(HeaderHeightProperty);
        set => SetValue(HeaderHeightProperty, value);
    }

    private static void OnHeaderHeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (DataGridView)d;
        view._style.HeaderHeight = (float)(double)e.NewValue;
        view.Invalidate();
    }

    // ── FrozenRowCount ────────────────────────────────────────────

    /// <summary>Identifies the <see cref="FrozenRowCount"/> dependency property.</summary>
    public static readonly DependencyProperty FrozenRowCountProperty =
        DependencyProperty.Register(
            nameof(FrozenRowCount), typeof(int), typeof(DataGridView),
            new PropertyMetadata(0, OnFrozenRowCountChanged));

    /// <summary>Number of top data rows to freeze when scrolling vertically.</summary>
    public int FrozenRowCount
    {
        get => (int)GetValue(FrozenRowCountProperty);
        set => SetValue(FrozenRowCountProperty, value);
    }

    private static void OnFrozenRowCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (DataGridView)d;
        view._dataSource.FrozenRowCount = (int)e.NewValue;
        view.Invalidate();
    }

    // ── EditTriggers ──────────────────────────────────────────────

    /// <summary>Identifies the <see cref="EditTriggers"/> dependency property.</summary>
    public static readonly DependencyProperty EditTriggersProperty =
        DependencyProperty.Register(
            nameof(EditTriggers), typeof(EditTrigger), typeof(DataGridView),
            new PropertyMetadata(EditTrigger.Default, OnEditTriggersChanged));

    /// <summary>Which user actions trigger cell editing. Default: DoubleTap | F2Key | Typing.</summary>
    public EditTrigger EditTriggers
    {
        get => (EditTrigger)GetValue(EditTriggersProperty);
        set => SetValue(EditTriggersProperty, value);
    }

    private static void OnEditTriggersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (DataGridView)d;
        view._editSession.EditTriggers = (EditTrigger)e.NewValue;
    }

    // ── EditTextSelectionMode ─────────────────────────────────────

    /// <summary>Identifies the <see cref="EditTextSelectionMode"/> dependency property.</summary>
    public static readonly DependencyProperty EditTextSelectionModeProperty =
        DependencyProperty.Register(
            nameof(EditTextSelectionMode), typeof(EditTextSelectionMode), typeof(DataGridView),
            new PropertyMetadata(EditTextSelectionMode.SelectAll, OnEditTextSelectionModeChanged));

    /// <summary>How text is selected when a cell enters edit mode. Default: SelectAll.</summary>
    public EditTextSelectionMode EditTextSelectionMode
    {
        get => (EditTextSelectionMode)GetValue(EditTextSelectionModeProperty);
        set => SetValue(EditTextSelectionModeProperty, value);
    }

    private static void OnEditTextSelectionModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (DataGridView)d;
        view._editSession.TextSelectionMode = (EditTextSelectionMode)e.NewValue;
    }

    // ── IsReadOnly ────────────────────────────────────────────────

    /// <summary>Identifies the <see cref="IsReadOnly"/> dependency property.</summary>
    public static readonly DependencyProperty IsReadOnlyProperty =
        DependencyProperty.Register(
            nameof(IsReadOnly), typeof(bool), typeof(DataGridView),
            new PropertyMetadata(false, OnIsReadOnlyChanged));

    /// <summary>When true, cells cannot be edited.</summary>
    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    private static void OnIsReadOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // Honored at edit time by the input controller / edit session (the gesture path is
        // wired in DataGridView.Input.cs). MAUI exposes IsReadOnly as a plain flag too;
        // a repaint keeps any read-only affordance in sync.
        ((DataGridView)d).Invalidate();
    }

    // ── AllowSorting ──────────────────────────────────────────────

    /// <summary>Identifies the <see cref="AllowSorting"/> dependency property.</summary>
    public static readonly DependencyProperty AllowSortingProperty =
        DependencyProperty.Register(
            nameof(AllowSorting), typeof(bool), typeof(DataGridView),
            new PropertyMetadata(true, OnAllowSortingChanged));

    /// <summary>When false, column header taps do not sort.</summary>
    public bool AllowSorting
    {
        get => (bool)GetValue(AllowSortingProperty);
        set => SetValue(AllowSortingProperty, value);
    }

    private static void OnAllowSortingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((DataGridView)d).Invalidate();

    // ── AllowFiltering ────────────────────────────────────────────

    /// <summary>Identifies the <see cref="AllowFiltering"/> dependency property.</summary>
    public static readonly DependencyProperty AllowFilteringProperty =
        DependencyProperty.Register(
            nameof(AllowFiltering), typeof(bool), typeof(DataGridView),
            new PropertyMetadata(true, OnAllowFilteringChanged));

    /// <summary>When false, filter icons are not shown in column headers.</summary>
    public bool AllowFiltering
    {
        get => (bool)GetValue(AllowFilteringProperty);
        set => SetValue(AllowFilteringProperty, value);
    }

    private static void OnAllowFilteringChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((DataGridView)d).Invalidate();

    // ── DismissKeyboardOnEnter ────────────────────────────────────

    /// <summary>Identifies the <see cref="DismissKeyboardOnEnter"/> dependency property.</summary>
    public static readonly DependencyProperty DismissKeyboardOnEnterProperty =
        DependencyProperty.Register(
            nameof(DismissKeyboardOnEnter), typeof(bool), typeof(DataGridView),
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
        var view = (DataGridView)d;
        view._editSession.DismissKeyboardOnEnter = (bool)e.NewValue;
    }

    // ── GridDescription ───────────────────────────────────────────

    /// <summary>Identifies the <see cref="GridDescription"/> dependency property.</summary>
    public static readonly DependencyProperty GridDescriptionProperty =
        DependencyProperty.Register(
            nameof(GridDescription), typeof(string), typeof(DataGridView),
            new PropertyMetadata("Data grid", OnGridDescriptionChanged));

    /// <summary>Accessible description for screen readers.</summary>
    public string GridDescription
    {
        get => (string)GetValue(GridDescriptionProperty);
        set => SetValue(GridDescriptionProperty, value);
    }

    private static void OnGridDescriptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (DataGridView)d;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(view, (string)e.NewValue);
    }

    /// <summary>
    /// Shared entry point for property callbacks to request a single repaint.
    /// Mirrors the MAUI control's <c>InvalidateSurface()</c> usage from each
    /// <c>propertyChanged</c> handler.
    /// </summary>
    private void Invalidate() => _canvasView.Invalidate();
}

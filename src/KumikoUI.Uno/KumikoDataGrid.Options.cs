using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Uno.Input;
using Microsoft.UI.Xaml;
using Windows.UI;

namespace KumikoUI.Uno;

/// <summary>
/// The configuration surface of <see cref="KumikoDataGrid"/> beyond the data: grouping, grid lines, row drag,
/// the validator, the commands and the events re-surfaced from the data source, input controller and edit
/// session, and <see cref="Invalidate"/>. Same names and defaults as the MAUI control.
/// </summary>
/// <remarks>
/// <para>
/// <b>Precedence.</b> <see cref="ShowHorizontalGridLines"/>, <see cref="ShowVerticalGridLines"/>,
/// <see cref="AllowRowDragDrop"/>, <see cref="ShowRowDragHandle"/> and <see cref="AlternateRowBackground"/> are
/// style settings. They are nullable: null (the default) leaves the value to <see cref="GridStyle"/>; a value
/// overrides it. Setting one writes it into the current style straight away, and assigning
/// <see cref="GridStyle"/> afterwards re-applies every explicit value to the new style, so an explicit property
/// always wins over the style, whatever the order. The values are written into the style instance you assign
/// (the same object), and setting a property back to null stops overriding but does not restore the earlier
/// value until the next style assignment.
/// </para>
/// </remarks>
public partial class KumikoDataGrid
{
    private readonly KumikoStyleOptions _styleOptions = new();

    private void InitializeOptions()
    {
        GroupDescriptions = new ObservableCollection<GroupDescription>();
        GroupSummaryRows = new ObservableCollection<GroupSummaryRow>();

        // These target objects the control owns, so they are wired once (they do not root the page).
        _inputController.RowTapped += OnRowTappedForCommand;
        _inputController.CellTapped += OnCellTappedForCommand;
        _inputController.HeaderTapped += OnHeaderTappedForCommand;
        _inputController.ContextRequested += OnContextMenuRequestedForCommand;
        _editSession.CellValueChanged += OnCellValueChangedForCommand;
        _dataSource.SortColumnsChanged += OnSortChangedForCommand;
    }

    /// <summary>Re-apply the explicit style settings after the style was replaced.</summary>
    private void ApplyStyleOptions()
    {
        if (_styleOptions.HasOverrides)
            _styleOptions.Apply(_style);
    }

    // ── Invalidate ──

    /// <summary>
    /// Repaints the grid from its current state without rebuilding the view. Use it after changing something
    /// the grid cannot observe (for example a column's formatting or a style object you hold). Safe to call
    /// from any thread.
    /// </summary>
    public void Invalidate()
    {
        if (DispatcherQueue is { } queue && !queue.HasThreadAccess)
            queue.TryEnqueue(RequestRedraw);
        else
            RequestRedraw();
    }

    // ── GroupDescriptions ──

    /// <summary>Identifies the <see cref="GroupDescriptions"/> dependency property.</summary>
    public static readonly DependencyProperty GroupDescriptionsProperty =
        DependencyProperty.Register(
            nameof(GroupDescriptions), typeof(ObservableCollection<GroupDescription>), typeof(KumikoDataGrid),
            new PropertyMetadata(null, OnGroupDescriptionsChanged));

    /// <summary>
    /// The columns the rows are grouped by, outermost first. Add them in XAML or change the collection at
    /// runtime; the data source follows. Never null: assigning null sets an empty collection. Replaces any
    /// grouping you set on <see cref="DataSource"/> directly.
    /// </summary>
    public ObservableCollection<GroupDescription> GroupDescriptions
    {
        get => (ObservableCollection<GroupDescription>)GetValue(GroupDescriptionsProperty);
        set => SetValue(GroupDescriptionsProperty, value);
    }

    private static void OnGroupDescriptionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (KumikoDataGrid)d;

        // Never null: null becomes an empty collection (which re-enters this callback)
        if (e.NewValue is null)
        {
            if (e.OldValue is ObservableCollection<GroupDescription> previous)
                previous.CollectionChanged -= view.OnGroupDescriptionsCollectionChanged;
            view.GroupDescriptions = new ObservableCollection<GroupDescription>();
            return;
        }

        if (e.OldValue is ObservableCollection<GroupDescription> oldCollection)
            oldCollection.CollectionChanged -= view.OnGroupDescriptionsCollectionChanged;
        var newCollection = (ObservableCollection<GroupDescription>)e.NewValue;
        if (view.IsObservingCollections)
            newCollection.CollectionChanged += view.OnGroupDescriptionsCollectionChanged;

        view._dataSource.SetGroupDescriptions(newCollection);
        view.Invalidate();
    }

    private void OnGroupDescriptionsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _dataSource.SetGroupDescriptions(GroupDescriptions);
        Invalidate();
    }

    // ── GroupSummaryRows ──

    /// <summary>Identifies the <see cref="GroupSummaryRows"/> dependency property.</summary>
    public static readonly DependencyProperty GroupSummaryRowsProperty =
        DependencyProperty.Register(
            nameof(GroupSummaryRows), typeof(ObservableCollection<GroupSummaryRow>), typeof(KumikoDataGrid),
            new PropertyMetadata(null, OnGroupSummaryRowsChanged));

    /// <summary>
    /// The summary rows shown after each group's rows when grouping is active. Add them in XAML or change the
    /// collection at runtime. Never null: assigning null sets an empty collection.
    /// </summary>
    public ObservableCollection<GroupSummaryRow> GroupSummaryRows
    {
        get => (ObservableCollection<GroupSummaryRow>)GetValue(GroupSummaryRowsProperty);
        set => SetValue(GroupSummaryRowsProperty, value);
    }

    private static void OnGroupSummaryRowsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (KumikoDataGrid)d;

        // Never null: null becomes an empty collection (which re-enters this callback)
        if (e.NewValue is null)
        {
            if (e.OldValue is ObservableCollection<GroupSummaryRow> previous)
                previous.CollectionChanged -= view.OnGroupSummaryRowsCollectionChanged;
            view.GroupSummaryRows = new ObservableCollection<GroupSummaryRow>();
            return;
        }

        if (e.OldValue is ObservableCollection<GroupSummaryRow> oldCollection)
            oldCollection.CollectionChanged -= view.OnGroupSummaryRowsCollectionChanged;
        var newCollection = (ObservableCollection<GroupSummaryRow>)e.NewValue;
        if (view.IsObservingCollections)
            newCollection.CollectionChanged += view.OnGroupSummaryRowsCollectionChanged;

        view._dataSource.SetGroupSummaryRows(newCollection);
        view.Invalidate();
    }

    private void OnGroupSummaryRowsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _dataSource.SetGroupSummaryRows(GroupSummaryRows);
        Invalidate();
    }

    // ── Style-backed options (null = leave it to the style) ──

    /// <summary>Identifies the <see cref="AllowRowDragDrop"/> dependency property.</summary>
    public static readonly DependencyProperty AllowRowDragDropProperty =
        DependencyProperty.Register(
            nameof(AllowRowDragDrop), typeof(bool?), typeof(KumikoDataGrid),
            new PropertyMetadata(null, (d, e) =>
            {
                var view = (KumikoDataGrid)d;
                view._styleOptions.AllowRowDragDrop = (bool?)e.NewValue;
                view.ApplyStyleOptions();
                view.Invalidate();
            }));

    /// <summary>
    /// Whether a row can be dragged from any cell to reorder it. Null (default) keeps the style's value
    /// (off). See the precedence rules on <see cref="KumikoDataGrid"/>.
    /// </summary>
    public bool? AllowRowDragDrop
    {
        get => (bool?)GetValue(AllowRowDragDropProperty);
        set => SetValue(AllowRowDragDropProperty, value);
    }

    /// <summary>Identifies the <see cref="ShowRowDragHandle"/> dependency property.</summary>
    public static readonly DependencyProperty ShowRowDragHandleProperty =
        DependencyProperty.Register(
            nameof(ShowRowDragHandle), typeof(bool?), typeof(KumikoDataGrid),
            new PropertyMetadata(null, (d, e) =>
            {
                var view = (KumikoDataGrid)d;
                view._styleOptions.ShowRowDragHandle = (bool?)e.NewValue;
                view.ApplyStyleOptions();
                view.Invalidate();
            }));

    /// <summary>
    /// Whether each row shows a drag handle; rows then drag only from the handle. Null (default) keeps the
    /// style's value (off).
    /// </summary>
    public bool? ShowRowDragHandle
    {
        get => (bool?)GetValue(ShowRowDragHandleProperty);
        set => SetValue(ShowRowDragHandleProperty, value);
    }

    /// <summary>Identifies the <see cref="ShowHorizontalGridLines"/> dependency property.</summary>
    public static readonly DependencyProperty ShowHorizontalGridLinesProperty =
        DependencyProperty.Register(
            nameof(ShowHorizontalGridLines), typeof(bool?), typeof(KumikoDataGrid),
            new PropertyMetadata(null, (d, e) =>
            {
                var view = (KumikoDataGrid)d;
                view._styleOptions.ShowHorizontalGridLines = (bool?)e.NewValue;
                view.ApplyStyleOptions();
                view.Invalidate();
            }));

    /// <summary>Whether lines are drawn between rows. Null (default) keeps the style's value (on).</summary>
    public bool? ShowHorizontalGridLines
    {
        get => (bool?)GetValue(ShowHorizontalGridLinesProperty);
        set => SetValue(ShowHorizontalGridLinesProperty, value);
    }

    /// <summary>Identifies the <see cref="ShowVerticalGridLines"/> dependency property.</summary>
    public static readonly DependencyProperty ShowVerticalGridLinesProperty =
        DependencyProperty.Register(
            nameof(ShowVerticalGridLines), typeof(bool?), typeof(KumikoDataGrid),
            new PropertyMetadata(null, (d, e) =>
            {
                var view = (KumikoDataGrid)d;
                view._styleOptions.ShowVerticalGridLines = (bool?)e.NewValue;
                view.ApplyStyleOptions();
                view.Invalidate();
            }));

    /// <summary>Whether lines are drawn between columns. Null (default) keeps the style's value (on).</summary>
    public bool? ShowVerticalGridLines
    {
        get => (bool?)GetValue(ShowVerticalGridLinesProperty);
        set => SetValue(ShowVerticalGridLinesProperty, value);
    }

    /// <summary>Identifies the <see cref="AlternateRowBackground"/> dependency property.</summary>
    public static readonly DependencyProperty AlternateRowBackgroundProperty =
        DependencyProperty.Register(
            nameof(AlternateRowBackground), typeof(Color?), typeof(KumikoDataGrid),
            new PropertyMetadata(null, (d, e) =>
            {
                var view = (KumikoDataGrid)d;
                view._styleOptions.AlternateRowBackground = ToKumikoColor(e.NewValue as Color?);
                view.ApplyStyleOptions();
                view.Invalidate();
            }));

    /// <summary>
    /// Background color of alternate rows; setting it also turns alternate row shading on. Null (default)
    /// keeps the style's colors.
    /// </summary>
    public Color? AlternateRowBackground
    {
        get => (Color?)GetValue(AlternateRowBackgroundProperty);
        set => SetValue(AlternateRowBackgroundProperty, value);
    }

    private static KumikoColor? ToKumikoColor(Color? color) => PointerGestures.ToKumikoColor(color);

    // ── CellValidator ──

    /// <summary>Identifies the <see cref="CellValidator"/> dependency property.</summary>
    public static readonly DependencyProperty CellValidatorProperty =
        DependencyProperty.Register(
            nameof(CellValidator), typeof(Func<int, int, object?, CellValidationResult>), typeof(KumikoDataGrid),
            new PropertyMetadata(null, (d, e) =>
                ((KumikoDataGrid)d)._editSession.CellValidator = e.NewValue as Func<int, int, object?, CellValidationResult>));

    /// <summary>
    /// Validates a converted value before an edit is committed: it receives the visible row, the column index
    /// and the new value. Forwarded to <see cref="EditSession.CellValidator"/>.
    /// </summary>
    public Func<int, int, object?, CellValidationResult>? CellValidator
    {
        get => (Func<int, int, object?, CellValidationResult>?)GetValue(CellValidatorProperty);
        set => SetValue(CellValidatorProperty, value);
    }

    // ── Commands ──

    /// <summary>Identifies the <see cref="RowTappedCommand"/> dependency property.</summary>
    public static readonly DependencyProperty RowTappedCommandProperty =
        DependencyProperty.Register(
            nameof(RowTappedCommand), typeof(ICommand), typeof(KumikoDataGrid), new PropertyMetadata(null));

    /// <summary>Executed when a row is tapped, with the <see cref="RowTappedEventArgs"/> as its parameter.</summary>
    public ICommand? RowTappedCommand
    {
        get => (ICommand?)GetValue(RowTappedCommandProperty);
        set => SetValue(RowTappedCommandProperty, value);
    }

    /// <summary>Identifies the <see cref="CellValueChangedCommand"/> dependency property.</summary>
    public static readonly DependencyProperty CellValueChangedCommandProperty =
        DependencyProperty.Register(
            nameof(CellValueChangedCommand), typeof(ICommand), typeof(KumikoDataGrid), new PropertyMetadata(null));

    /// <summary>Executed after an edit changed a cell value, with the <see cref="CellValueChangedEventArgs"/> as its parameter.</summary>
    public ICommand? CellValueChangedCommand
    {
        get => (ICommand?)GetValue(CellValueChangedCommandProperty);
        set => SetValue(CellValueChangedCommandProperty, value);
    }

    /// <summary>Identifies the <see cref="SortChangedCommand"/> dependency property.</summary>
    public static readonly DependencyProperty SortChangedCommandProperty =
        DependencyProperty.Register(
            nameof(SortChangedCommand), typeof(ICommand), typeof(KumikoDataGrid), new PropertyMetadata(null));

    /// <summary>Executed after the sort changed, with the <see cref="SortColumnsChangedEventArgs"/> as its parameter.</summary>
    public ICommand? SortChangedCommand
    {
        get => (ICommand?)GetValue(SortChangedCommandProperty);
        set => SetValue(SortChangedCommandProperty, value);
    }

    /// <summary>Identifies the <see cref="CellTappedCommand"/> dependency property.</summary>
    public static readonly DependencyProperty CellTappedCommandProperty =
        DependencyProperty.Register(
            nameof(CellTappedCommand), typeof(ICommand), typeof(KumikoDataGrid), new PropertyMetadata(null));

    /// <summary>Executed when a data cell is tapped, with the <see cref="KumikoCellTappedEventArgs"/> as its parameter.</summary>
    public ICommand? CellTappedCommand
    {
        get => (ICommand?)GetValue(CellTappedCommandProperty);
        set => SetValue(CellTappedCommandProperty, value);
    }

    /// <summary>Identifies the <see cref="HeaderTappedCommand"/> dependency property.</summary>
    public static readonly DependencyProperty HeaderTappedCommandProperty =
        DependencyProperty.Register(
            nameof(HeaderTappedCommand), typeof(ICommand), typeof(KumikoDataGrid), new PropertyMetadata(null));

    /// <summary>Executed when a column header is tapped, with the <see cref="KumikoHeaderTappedEventArgs"/> as its parameter.</summary>
    public ICommand? HeaderTappedCommand
    {
        get => (ICommand?)GetValue(HeaderTappedCommandProperty);
        set => SetValue(HeaderTappedCommandProperty, value);
    }

    /// <summary>Identifies the <see cref="ContextMenuRequestedCommand"/> dependency property.</summary>
    public static readonly DependencyProperty ContextMenuRequestedCommandProperty =
        DependencyProperty.Register(
            nameof(ContextMenuRequestedCommand), typeof(ICommand), typeof(KumikoDataGrid), new PropertyMetadata(null));

    /// <summary>
    /// Executed when the user asks for a context menu (a right click or a touch long press), with the
    /// <see cref="KumikoContextRequestedEventArgs"/> as its parameter.
    /// </summary>
    public ICommand? ContextMenuRequestedCommand
    {
        get => (ICommand?)GetValue(ContextMenuRequestedCommandProperty);
        set => SetValue(ContextMenuRequestedCommandProperty, value);
    }

    private void OnRowTappedForCommand(object? sender, RowTappedEventArgs e)
        => KumikoCommands.TryExecute(RowTappedCommand, e);

    private void OnCellTappedForCommand(object? sender, KumikoCellTappedEventArgs e)
        => KumikoCommands.TryExecute(CellTappedCommand, e);

    private void OnHeaderTappedForCommand(object? sender, KumikoHeaderTappedEventArgs e)
        => KumikoCommands.TryExecute(HeaderTappedCommand, e);

    private void OnContextMenuRequestedForCommand(object? sender, KumikoContextRequestedEventArgs e)
        => KumikoCommands.TryExecute(ContextMenuRequestedCommand, e);

    private void OnCellValueChangedForCommand(object? sender, CellValueChangedEventArgs e)
        => KumikoCommands.TryExecute(CellValueChangedCommand, e);

    private void OnSortChangedForCommand(object? sender, SortColumnsChangedEventArgs e)
        => KumikoCommands.TryExecute(SortChangedCommand, e);

    // ── Re-surfaced events (RowTapped, RowDoubleTapped, CellBeginEdit, CellEndEdit and CellValueChanged are in KumikoDataGrid.cs) ──

    /// <summary>Raised before the sort changes. Set <c>Cancel</c> to prevent it.</summary>
    public event EventHandler<SortColumnsChangingEventArgs>? SortChanging
    {
        add => _dataSource.SortColumnsChanging += value;
        remove => _dataSource.SortColumnsChanging -= value;
    }

    /// <summary>Raised after the sort changed.</summary>
    public event EventHandler<SortColumnsChangedEventArgs>? SortChanged
    {
        add => _dataSource.SortColumnsChanged += value;
        remove => _dataSource.SortColumnsChanged -= value;
    }

    /// <summary>Raised before a filter changes. Set <c>Cancel</c> to prevent it.</summary>
    public event EventHandler<FilterChangingEventArgs>? FilterChanging
    {
        add => _dataSource.FilterChanging += value;
        remove => _dataSource.FilterChanging -= value;
    }

    /// <summary>Raised after a filter changed.</summary>
    public event EventHandler<FilterChangedEventArgs>? FilterChanged
    {
        add => _dataSource.FilterChanged += value;
        remove => _dataSource.FilterChanged -= value;
    }

    /// <summary>Raised once for every change to the data or the visible rows (see <see cref="KumikoSource.ViewChanged"/>).</summary>
    public event EventHandler<KumikoViewChangedEventArgs>? ViewChanged
    {
        add => _dataSource.ViewChanged += value;
        remove => _dataSource.ViewChanged -= value;
    }

    /// <summary>Raised before a group is expanded or collapsed. Set <c>Cancel</c> to prevent it.</summary>
    public event EventHandler<GroupExpandingEventArgs>? GroupExpanding
    {
        add => _dataSource.GroupExpanding += value;
        remove => _dataSource.GroupExpanding -= value;
    }

    /// <summary>Raised after a group was expanded or collapsed.</summary>
    public event EventHandler<GroupExpandedChangedEventArgs>? GroupExpandedChanged
    {
        add => _dataSource.GroupExpandedChanged += value;
        remove => _dataSource.GroupExpandedChanged -= value;
    }

    /// <summary>
    /// Raised once after the user dragged a row to a new position and the grid moved its item in
    /// <c>ItemsSource</c> (<see cref="KumikoSource.ReorderRow"/>).
    /// </summary>
    public event EventHandler<RowReorderedEventArgs>? RowReordered
    {
        add => _inputController.RowReordered += value;
        remove => _inputController.RowReordered -= value;
    }

    /// <summary>
    /// Raised once when the user dropped a dragged row at a new position but the grid could not move it:
    /// <see cref="RowReorderedEventArgs.Result"/> says why (a column sort is active, <c>ItemsSource</c> is not a
    /// writable <see cref="System.Collections.IList"/>, or the target is in another group). Nothing changed.
    /// </summary>
    public event EventHandler<RowReorderedEventArgs>? RowReorderRejected
    {
        add => _inputController.RowReorderRejected += value;
        remove => _inputController.RowReorderRejected -= value;
    }

    /// <summary>Raised after the user dragged a column header to a new position.</summary>
    public event EventHandler<KumikoColumnReorderedEventArgs>? ColumnReordered
    {
        add => _inputController.ColumnReordered += value;
        remove => _inputController.ColumnReordered -= value;
    }

    /// <summary>Raised after the user resized a column.</summary>
    public event EventHandler<ColumnResizedEventArgs>? ColumnResized
    {
        add => _inputController.ColumnResized += value;
        remove => _inputController.ColumnResized -= value;
    }

    /// <summary>Raised when an edit is rejected or flagged by validation (a failed conversion or the <see cref="CellValidator"/>).</summary>
    public event EventHandler<CellValidationEventArgs>? ValidationFailed
    {
        add => _editSession.ValidationFailed += value;
        remove => _editSession.ValidationFailed -= value;
    }
}

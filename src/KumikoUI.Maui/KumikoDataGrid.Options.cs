using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Maui;

/// <summary>
/// The configuration surface of <see cref="KumikoDataGrid"/> beyond the data: grouping, grid lines, row drag,
/// the validator, the commands and the events re-surfaced from the data source, input controller and edit
/// session, and <see cref="Invalidate"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Precedence.</b> <see cref="ShowHorizontalGridLines"/>, <see cref="ShowVerticalGridLines"/>,
/// <see cref="AllowRowDragDrop"/>, <see cref="ShowRowDragHandle"/> and <see cref="AlternateRowBackground"/> are
/// style settings. They are nullable: null (the default) leaves the value to <see cref="GridStyle"/> and
/// <see cref="Theme"/>; a value overrides it. Setting one writes it into the current style straight away, and
/// assigning <see cref="GridStyle"/> or <see cref="Theme"/> afterwards re-applies every explicit value to the new
/// style, so an explicit property always wins over a theme, whatever the order. The values are written into the
/// style instance you assign (the same object), and setting a property back to null stops overriding but does not
/// restore the earlier value until the next style or theme assignment.
/// </para>
/// </remarks>
public partial class KumikoDataGrid
{
    private readonly KumikoStyleOptions _styleOptions = new();

    private void InitializeOptions()
    {
        GroupDescriptions = new ObservableCollection<GroupDescription>();
        GroupSummaryRows = new ObservableCollection<GroupSummaryRow>();

        // These target objects the control owns, so they are wired once and need no Cleanup/Reconnect.
        _inputController.RowTapped += OnRowTappedForCommand;
        _editSession.CellValueChanged += OnCellValueChangedForCommand;
        _dataSource.SortColumnsChanged += OnSortChangedForCommand;
    }

    /// <summary>Re-apply the explicit style settings after the style or theme was replaced.</summary>
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
        if (Dispatcher.IsDispatchRequired)
            Dispatcher.Dispatch(InvalidateSurface);
        else
            InvalidateSurface();
    }

    // ── GroupDescriptions ──

    /// <summary>Identifies the <see cref="GroupDescriptions"/> bindable property.</summary>
    public static readonly BindableProperty GroupDescriptionsProperty = BindableProperty.Create(
        nameof(GroupDescriptions),
        typeof(ObservableCollection<GroupDescription>),
        typeof(KumikoDataGrid),
        defaultValue: null,
        coerceValue: (_, v) => v ?? new ObservableCollection<GroupDescription>(),
        propertyChanged: (b, o, n) => ((KumikoDataGrid)b).OnGroupDescriptionsChanged(
            o as ObservableCollection<GroupDescription>,
            n as ObservableCollection<GroupDescription>));

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

    private void OnGroupDescriptionsChanged(
        ObservableCollection<GroupDescription>? oldCollection, ObservableCollection<GroupDescription>? newCollection)
    {
        if (oldCollection != null)
            oldCollection.CollectionChanged -= OnGroupDescriptionsCollectionChanged;
        if (newCollection != null && IsObservingCollections)
            newCollection.CollectionChanged += OnGroupDescriptionsCollectionChanged;

        _dataSource.SetGroupDescriptions(newCollection);
        InvalidateSurface();
    }

    private void OnGroupDescriptionsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _dataSource.SetGroupDescriptions(GroupDescriptions);
        InvalidateSurface();
    }

    // ── GroupSummaryRows ──

    /// <summary>Identifies the <see cref="GroupSummaryRows"/> bindable property.</summary>
    public static readonly BindableProperty GroupSummaryRowsProperty = BindableProperty.Create(
        nameof(GroupSummaryRows),
        typeof(ObservableCollection<GroupSummaryRow>),
        typeof(KumikoDataGrid),
        defaultValue: null,
        coerceValue: (_, v) => v ?? new ObservableCollection<GroupSummaryRow>(),
        propertyChanged: (b, o, n) => ((KumikoDataGrid)b).OnGroupSummaryRowsChanged(
            o as ObservableCollection<GroupSummaryRow>,
            n as ObservableCollection<GroupSummaryRow>));

    /// <summary>
    /// The summary rows shown after each group's rows when grouping is active. Add them in XAML or change the
    /// collection at runtime. Never null: assigning null sets an empty collection.
    /// </summary>
    public ObservableCollection<GroupSummaryRow> GroupSummaryRows
    {
        get => (ObservableCollection<GroupSummaryRow>)GetValue(GroupSummaryRowsProperty);
        set => SetValue(GroupSummaryRowsProperty, value);
    }

    private void OnGroupSummaryRowsChanged(
        ObservableCollection<GroupSummaryRow>? oldCollection, ObservableCollection<GroupSummaryRow>? newCollection)
    {
        if (oldCollection != null)
            oldCollection.CollectionChanged -= OnGroupSummaryRowsCollectionChanged;
        if (newCollection != null && IsObservingCollections)
            newCollection.CollectionChanged += OnGroupSummaryRowsCollectionChanged;

        _dataSource.SetGroupSummaryRows(newCollection);
        InvalidateSurface();
    }

    private void OnGroupSummaryRowsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _dataSource.SetGroupSummaryRows(GroupSummaryRows);
        InvalidateSurface();
    }

    // ── Style-backed options (null = leave it to the style or theme) ──

    /// <summary>Identifies the <see cref="AllowRowDragDrop"/> bindable property.</summary>
    public static readonly BindableProperty AllowRowDragDropProperty = BindableProperty.Create(
        nameof(AllowRowDragDrop), typeof(bool?), typeof(KumikoDataGrid), null,
        propertyChanged: (b, _, n) =>
        {
            var view = (KumikoDataGrid)b;
            view._styleOptions.AllowRowDragDrop = (bool?)n;
            view.ApplyStyleOptions();
            view.InvalidateSurface();
        });

    /// <summary>
    /// Whether a row can be dragged from any cell to reorder it. Null (default) keeps the style's value
    /// (off). See the precedence rules on <see cref="KumikoDataGrid"/>.
    /// </summary>
    public bool? AllowRowDragDrop
    {
        get => (bool?)GetValue(AllowRowDragDropProperty);
        set => SetValue(AllowRowDragDropProperty, value);
    }

    /// <summary>Identifies the <see cref="ShowRowDragHandle"/> bindable property.</summary>
    public static readonly BindableProperty ShowRowDragHandleProperty = BindableProperty.Create(
        nameof(ShowRowDragHandle), typeof(bool?), typeof(KumikoDataGrid), null,
        propertyChanged: (b, _, n) =>
        {
            var view = (KumikoDataGrid)b;
            view._styleOptions.ShowRowDragHandle = (bool?)n;
            view.ApplyStyleOptions();
            view.InvalidateSurface();
        });

    /// <summary>
    /// Whether each row shows a drag handle; rows then drag only from the handle. Null (default) keeps the
    /// style's value (off).
    /// </summary>
    public bool? ShowRowDragHandle
    {
        get => (bool?)GetValue(ShowRowDragHandleProperty);
        set => SetValue(ShowRowDragHandleProperty, value);
    }

    /// <summary>Identifies the <see cref="ShowHorizontalGridLines"/> bindable property.</summary>
    public static readonly BindableProperty ShowHorizontalGridLinesProperty = BindableProperty.Create(
        nameof(ShowHorizontalGridLines), typeof(bool?), typeof(KumikoDataGrid), null,
        propertyChanged: (b, _, n) =>
        {
            var view = (KumikoDataGrid)b;
            view._styleOptions.ShowHorizontalGridLines = (bool?)n;
            view.ApplyStyleOptions();
            view.InvalidateSurface();
        });

    /// <summary>Whether lines are drawn between rows. Null (default) keeps the style's value (on).</summary>
    public bool? ShowHorizontalGridLines
    {
        get => (bool?)GetValue(ShowHorizontalGridLinesProperty);
        set => SetValue(ShowHorizontalGridLinesProperty, value);
    }

    /// <summary>Identifies the <see cref="ShowVerticalGridLines"/> bindable property.</summary>
    public static readonly BindableProperty ShowVerticalGridLinesProperty = BindableProperty.Create(
        nameof(ShowVerticalGridLines), typeof(bool?), typeof(KumikoDataGrid), null,
        propertyChanged: (b, _, n) =>
        {
            var view = (KumikoDataGrid)b;
            view._styleOptions.ShowVerticalGridLines = (bool?)n;
            view.ApplyStyleOptions();
            view.InvalidateSurface();
        });

    /// <summary>Whether lines are drawn between columns. Null (default) keeps the style's value (on).</summary>
    public bool? ShowVerticalGridLines
    {
        get => (bool?)GetValue(ShowVerticalGridLinesProperty);
        set => SetValue(ShowVerticalGridLinesProperty, value);
    }

    /// <summary>Identifies the <see cref="AlternateRowBackground"/> bindable property.</summary>
    public static readonly BindableProperty AlternateRowBackgroundProperty = BindableProperty.Create(
        nameof(AlternateRowBackground), typeof(Color), typeof(KumikoDataGrid), null,
        propertyChanged: (b, _, n) =>
        {
            var view = (KumikoDataGrid)b;
            view._styleOptions.AlternateRowBackground = ToKumikoColor(n as Color);
            view.ApplyStyleOptions();
            view.InvalidateSurface();
        });

    /// <summary>
    /// Background color of alternate rows; setting it also turns alternate row shading on. Null (default)
    /// keeps the style's colors.
    /// </summary>
    public Color? AlternateRowBackground
    {
        get => (Color?)GetValue(AlternateRowBackgroundProperty);
        set => SetValue(AlternateRowBackgroundProperty, value);
    }

    private static KumikoColor? ToKumikoColor(Color? color)
        => color == null
            ? null
            : new KumikoColor(
                (byte)Math.Round(color.Red * 255f), (byte)Math.Round(color.Green * 255f),
                (byte)Math.Round(color.Blue * 255f), (byte)Math.Round(color.Alpha * 255f));

    // ── CellValidator ──

    /// <summary>Identifies the <see cref="CellValidator"/> bindable property.</summary>
    public static readonly BindableProperty CellValidatorProperty = BindableProperty.Create(
        nameof(CellValidator), typeof(Func<int, int, object?, CellValidationResult>), typeof(KumikoDataGrid), null,
        propertyChanged: (b, _, n) =>
            ((KumikoDataGrid)b)._editSession.CellValidator = n as Func<int, int, object?, CellValidationResult>);

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

    /// <summary>Identifies the <see cref="RowTappedCommand"/> bindable property.</summary>
    public static readonly BindableProperty RowTappedCommandProperty = BindableProperty.Create(
        nameof(RowTappedCommand), typeof(ICommand), typeof(KumikoDataGrid));

    /// <summary>Executed when a row is tapped, with the <see cref="RowTappedEventArgs"/> as its parameter.</summary>
    public ICommand? RowTappedCommand
    {
        get => (ICommand?)GetValue(RowTappedCommandProperty);
        set => SetValue(RowTappedCommandProperty, value);
    }

    /// <summary>Identifies the <see cref="CellValueChangedCommand"/> bindable property.</summary>
    public static readonly BindableProperty CellValueChangedCommandProperty = BindableProperty.Create(
        nameof(CellValueChangedCommand), typeof(ICommand), typeof(KumikoDataGrid));

    /// <summary>Executed after an edit changed a cell value, with the <see cref="CellValueChangedEventArgs"/> as its parameter.</summary>
    public ICommand? CellValueChangedCommand
    {
        get => (ICommand?)GetValue(CellValueChangedCommandProperty);
        set => SetValue(CellValueChangedCommandProperty, value);
    }

    /// <summary>Identifies the <see cref="SortChangedCommand"/> bindable property.</summary>
    public static readonly BindableProperty SortChangedCommandProperty = BindableProperty.Create(
        nameof(SortChangedCommand), typeof(ICommand), typeof(KumikoDataGrid));

    /// <summary>Executed after the sort changed, with the <see cref="SortColumnsChangedEventArgs"/> as its parameter.</summary>
    public ICommand? SortChangedCommand
    {
        get => (ICommand?)GetValue(SortChangedCommandProperty);
        set => SetValue(SortChangedCommandProperty, value);
    }

    private void OnRowTappedForCommand(object? sender, RowTappedEventArgs e)
        => KumikoCommands.TryExecute(RowTappedCommand, e);

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

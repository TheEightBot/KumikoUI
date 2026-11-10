using System.Collections;
using System.Windows.Input;
using KumikoUI.Core.Models;

namespace KumikoUI.Maui;

/// <summary>
/// The MVVM selection surface of <see cref="KumikoDataGrid"/>: <see cref="SelectedItem"/>,
/// <see cref="SelectedItems"/>, <see cref="SelectedIndex"/> and <see cref="CurrentCell"/> (all two-way),
/// <see cref="SelectionChanged"/> and <see cref="SelectionChangedCommand"/>. The state lives in a Core
/// <see cref="KumikoSelectionBinder"/>; the properties only mirror it, and a flag keeps the two directions
/// from feeding back into each other.
/// </summary>
public partial class KumikoDataGrid
{
    private KumikoSelectionBinder _selectionBinder = null!;
    private bool _pushingSelectionState;

    private void InitializeSelectionBinding()
    {
        _selectionBinder = new KumikoSelectionBinder(_selection);
        _selectionBinder.Changed += (_, _) => PushSelectionState();
        _selectionBinder.SelectionChanged += OnBinderSelectionChanged;
        SetValue(SelectedItemsProperty, _selectionBinder.SelectedItems);
    }

    // ── SelectedItem ──

    /// <summary>Identifies the <see cref="SelectedItem"/> bindable property.</summary>
    public static readonly BindableProperty SelectedItemProperty = BindableProperty.Create(
        nameof(SelectedItem), typeof(object), typeof(KumikoDataGrid), null, BindingMode.TwoWay,
        propertyChanged: (b, _, n) =>
        {
            var view = (KumikoDataGrid)b;
            if (view._pushingSelectionState) return;
            view._selectionBinder.SetSelectedItem(n);
            view.PushSelectionState();
        });

    /// <summary>
    /// The primary selected item: the current cell's item while it is selected, otherwise the item selected
    /// last (null when nothing is selected). Setting it selects only that item (an item that is not visible,
    /// or any item in <c>None</c> mode, leaves the selection empty and the property resets to null).
    /// </summary>
    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    // ── SelectedIndex ──

    /// <summary>Identifies the <see cref="SelectedIndex"/> bindable property.</summary>
    public static readonly BindableProperty SelectedIndexProperty = BindableProperty.Create(
        nameof(SelectedIndex), typeof(int), typeof(KumikoDataGrid), -1, BindingMode.TwoWay,
        propertyChanged: (b, _, n) =>
        {
            var view = (KumikoDataGrid)b;
            if (view._pushingSelectionState) return;
            view._selectionBinder.SetSelectedIndex((int)n);
            view.PushSelectionState();
        });

    /// <summary>
    /// The visible row of <see cref="SelectedItem"/>, or -1 when nothing is selected. It follows the item
    /// through sort and filter. Setting it selects the item at that visible row.
    /// </summary>
    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    // ── SelectedItems ──

    /// <summary>Identifies the <see cref="SelectedItems"/> bindable property.</summary>
    public static readonly BindableProperty SelectedItemsProperty = BindableProperty.Create(
        nameof(SelectedItems), typeof(IList), typeof(KumikoDataGrid), null, BindingMode.TwoWay,
        propertyChanged: (b, _, n) =>
        {
            var view = (KumikoDataGrid)b;
            if (view._pushingSelectionState) return;
            view._selectionBinder.AttachSelectedItems((IList?)n);
            view.PushSelectionState();
        });

    /// <summary>
    /// The selected items. By default an <see cref="System.Collections.ObjectModel.ObservableCollection{T}"/>
    /// the grid owns and keeps in sync; bind it one-way-to-source to read it. Assign (or two-way bind) your own
    /// list to have the grid select its items and keep it updated: a list that implements
    /// <see cref="System.Collections.Specialized.INotifyCollectionChanged"/> also selects and deselects when
    /// you change it. Setting null goes back to the owned collection.
    /// </summary>
    public IList? SelectedItems
    {
        get => (IList?)GetValue(SelectedItemsProperty);
        set => SetValue(SelectedItemsProperty, value);
    }

    // ── CurrentCell ──

    /// <summary>Identifies the <see cref="CurrentCell"/> bindable property.</summary>
    public static readonly BindableProperty CurrentCellProperty = BindableProperty.Create(
        nameof(CurrentCell), typeof(CellPosition), typeof(KumikoDataGrid), CellPosition.Invalid, BindingMode.TwoWay,
        propertyChanged: (b, _, n) =>
        {
            var view = (KumikoDataGrid)b;
            if (view._pushingSelectionState) return;
            view._selectionBinder.CurrentCell = (CellPosition)n;
            view.InvalidateSurface();
        });

    /// <summary>The current (keyboard focus) cell as visible row and column; <see cref="CellPosition.Invalid"/> when none.</summary>
    public CellPosition CurrentCell
    {
        get => (CellPosition)GetValue(CurrentCellProperty);
        set => SetValue(CurrentCellProperty, value);
    }

    // ── SelectionChanged ──

    /// <summary>
    /// Raised after the selection changed, from the user or from code, with the items added to and removed
    /// from it. <see cref="SelectedItem"/>, <see cref="SelectedItems"/> and <see cref="SelectedIndex"/> are
    /// already up to date.
    /// </summary>
    public event EventHandler<KumikoSelectionChangedEventArgs>? SelectionChanged;

    /// <summary>Identifies the <see cref="SelectionChangedCommand"/> bindable property.</summary>
    public static readonly BindableProperty SelectionChangedCommandProperty = BindableProperty.Create(
        nameof(SelectionChangedCommand), typeof(ICommand), typeof(KumikoDataGrid));

    /// <summary>
    /// Executed when the selection changes. Its parameter is <see cref="SelectionChangedCommandParameter"/>,
    /// or the <see cref="KumikoSelectionChangedEventArgs"/> when that is null.
    /// </summary>
    public ICommand? SelectionChangedCommand
    {
        get => (ICommand?)GetValue(SelectionChangedCommandProperty);
        set => SetValue(SelectionChangedCommandProperty, value);
    }

    /// <summary>Identifies the <see cref="SelectionChangedCommandParameter"/> bindable property.</summary>
    public static readonly BindableProperty SelectionChangedCommandParameterProperty = BindableProperty.Create(
        nameof(SelectionChangedCommandParameter), typeof(object), typeof(KumikoDataGrid));

    /// <summary>The parameter passed to <see cref="SelectionChangedCommand"/>; null passes the event arguments.</summary>
    public object? SelectionChangedCommandParameter
    {
        get => GetValue(SelectionChangedCommandParameterProperty);
        set => SetValue(SelectionChangedCommandParameterProperty, value);
    }

    // ── Plumbing ──

    private void OnBinderSelectionChanged(object? sender, KumikoSelectionChangedEventArgs e)
    {
        PushSelectionState();
        InvalidateSurface();

        SelectionChanged?.Invoke(this, e);
        var command = SelectionChangedCommand;
        var parameter = SelectionChangedCommandParameter ?? e;
        if (command != null && command.CanExecute(parameter))
            command.Execute(parameter);
    }

    /// <summary>Copy the binder's state to the bindable properties without writing it back.</summary>
    private void PushSelectionState()
    {
        if (_pushingSelectionState) return;
        _pushingSelectionState = true;
        try
        {
            SetValue(SelectedItemProperty, _selectionBinder.SelectedItem);
            SetValue(SelectedIndexProperty, _selectionBinder.SelectedIndex);
            SetValue(CurrentCellProperty, _selectionBinder.CurrentCell);
            SetValue(SelectedItemsProperty, _selectionBinder.SelectedItems); // the owned list after a null
        }
        finally { _pushingSelectionState = false; }
    }
}

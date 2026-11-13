using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace KumikoUI.Core.Models;

/// <summary>
/// The host-agnostic MVVM surface of a <see cref="KumikoSelectionModel"/>: a primary selected item, its view
/// index, a selected-items list kept in sync in both directions, and the current cell. The MAUI and Uno grids
/// expose it as <c>SelectedItem</c>, <c>SelectedIndex</c>, <c>SelectedItems</c> and <c>CurrentCell</c>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SelectedItem"/> is the current cell's item while it is selected, otherwise the item added by the
/// latest change; when it leaves the selection, the selected item on the topmost visible row takes over.
/// <see cref="SelectedIndex"/> is that item's visible row, so it follows the item through sort and filter.
/// </para>
/// <para>
/// <see cref="SelectedItems"/> is an <see cref="ObservableCollection{T}"/> owned by the binder until a host
/// attaches a list from the view model (<see cref="AttachSelectedItems"/>). Either list is updated when the
/// grid's selection changes, and if it implements <see cref="INotifyCollectionChanged"/>, changes made to it
/// select and deselect in the grid. The selection never loops back: while one side is applying a change to
/// the other, notifications from the second side are ignored.
/// </para>
/// </remarks>
public sealed class KumikoSelectionBinder
{
    private readonly KumikoSelectionModel _model;
    private readonly ObservableCollection<object> _owned = new();
    private IList _list;
    private INotifyCollectionChanged? _listNotify;
    private object? _primary;
    private bool _syncing;
    private bool _detached;

    // While SetSelectedItem runs, notifications wait here until the selection and the current cell are both
    // up to date.
    private bool _applyingItem;
    private readonly List<KumikoSelectionChangedEventArgs> _deferredSelectionChanges = new();

    // What the last Changed notification reported.
    private object? _publishedItem;
    private int _publishedIndex = -1;
    private CellPosition _publishedCell = CellPosition.Invalid;

    /// <summary>Creates the binder over <paramref name="model"/> and follows its changes.</summary>
    public KumikoSelectionBinder(KumikoSelectionModel model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _list = _owned;
        _owned.CollectionChanged += OnListChanged;
        _listNotify = _owned;
        _model.SelectionChanged += OnModelSelectionChanged;
        _model.CurrentCellChanged += (_, _) => Publish();
        _model.Source.ViewChanged += OnViewChanged;
    }

    /// <summary>Raised after <see cref="SelectedItem"/>, <see cref="SelectedIndex"/> or <see cref="CurrentCell"/> changed.</summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Raised for every selection change, whichever side caused it (the user, or a setter or list change from
    /// the view model), after the binder's state and <see cref="SelectedItems"/> are up to date.
    /// </summary>
    public event EventHandler<KumikoSelectionChangedEventArgs>? SelectionChanged;

    /// <summary>The primary selected item, or null when nothing is selected.</summary>
    public object? SelectedItem => _primary;

    /// <summary>The visible row of <see cref="SelectedItem"/>, or -1 when nothing is selected.</summary>
    public int SelectedIndex => _primary == null ? -1 : _model.Source.IndexOfRowKey(_primary);

    /// <summary>The list of selected items (the owned collection, or the attached one).</summary>
    public IList SelectedItems => _list;

    /// <summary>The current cell, as visible row and column.</summary>
    public CellPosition CurrentCell
    {
        get => _model.CurrentCell;
        set => _model.CurrentCell = value;
    }

    /// <summary>
    /// Select only <paramref name="item"/> and move the current cell to it (keeping its column). A null item
    /// clears the selection. An item that is not a visible data row, or any item in
    /// <see cref="KumikoSelectionMode.None"/> mode, leaves nothing selected. <see cref="SelectionChanged"/> and
    /// <see cref="Changed"/> are raised after both the selection and the current cell are updated, so they never
    /// report the previous item while the new one is being applied.
    /// </summary>
    public void SetSelectedItem(object? item)
    {
        // The whole update is reported once, at the end (#267). Moving the current cell first used to publish
        // the previous item while a host was still applying the new one from a two-way binding, and the binding
        // wrote that stale item back to its source, which set the new one again, forever. So the item is
        // selected before the cell moves, and Changed / SelectionChanged wait until both are done.
        _applyingItem = true;
        try
        {
            if (item == null)
            {
                _model.SetSelectedItems(Array.Empty<object>());
            }
            else
            {
                _model.SetSelectedItems(new[] { item });
                if (_model.IsSelected(item)) _primary = item;

                int row = _model.Source.IndexOfRowKey(item);
                if (row >= 0 && !_model.Source.IsNonDataRow(row))
                    _model.SetCurrentCell(row, _model.CurrentCell.IsValid ? _model.CurrentCell.Column : 0);
            }
        }
        finally { _applyingItem = false; }

        RaiseDeferredSelectionChanges();
        Publish();
    }

    /// <summary>Select only the item at visible row <paramref name="index"/>; a negative or out-of-range index clears.</summary>
    public void SetSelectedIndex(int index)
    {
        if (index < 0 || index >= _model.Source.RowCount) SetSelectedItem(null);
        else SetSelectedItem(_model.Source.IsNonDataRow(index) ? null : _model.Source.GetItem(index));
    }

    /// <summary>
    /// Use <paramref name="list"/> (from the view model) as <see cref="SelectedItems"/>, or the owned
    /// collection when null. The selection becomes the list's items, and the list is then kept in sync.
    /// </summary>
    public void AttachSelectedItems(IList? list)
    {
        var target = list ?? _owned;
        if (ReferenceEquals(target, _list)) return;

        if (_listNotify != null) _listNotify.CollectionChanged -= OnListChanged;
        _list = target;
        _listNotify = _detached ? null : target as INotifyCollectionChanged;
        if (_listNotify != null) _listNotify.CollectionChanged += OnListChanged;

        if (list == null)
            WriteList(_model.SelectedItems);   // back to the owned collection: mirror the grid
        else
            ApplyListToModel();
    }

    /// <summary>
    /// Stops listening to the view model's list, so a list that outlives the grid does not keep it alive. The
    /// grid still writes its selection into the list. <see cref="Attach"/> resumes. Does nothing when the grid's
    /// own collection is in use, or when already detached.
    /// </summary>
    public void Detach()
    {
        if (_listNotify == null || ReferenceEquals(_listNotify, _owned)) return;
        _listNotify.CollectionChanged -= OnListChanged;
        _listNotify = null;
        _detached = true;
    }

    /// <summary>
    /// Resumes listening to the list after <see cref="Detach"/>. The list may have changed meanwhile, so the
    /// selection is set from it again. Does nothing unless detached.
    /// </summary>
    public void Attach()
    {
        if (!_detached) return;
        _detached = false;
        _listNotify = _list as INotifyCollectionChanged;
        if (_listNotify != null) _listNotify.CollectionChanged += OnListChanged;
        ApplyListToModel();
    }

    // ── Model → list ──

    private void OnModelSelectionChanged(object? sender, KumikoSelectionChangedEventArgs e)
    {
        UpdatePrimary(e);

        if (!_syncing)
        {
            _syncing = true;
            try
            {
                if (!_list.IsReadOnly && !_list.IsFixedSize)
                {
                    foreach (var item in e.RemovedItems) _list.Remove(item);
                    foreach (var item in e.AddedItems.Where(added => !_list.Contains(added)))
                        _list.Add(item);
                }
            }
            finally { _syncing = false; }
        }

        if (_applyingItem)
        {
            _deferredSelectionChanges.Add(e);
            return;
        }

        SelectionChanged?.Invoke(this, e);
        Publish();
    }

    private void RaiseDeferredSelectionChanges()
    {
        if (_deferredSelectionChanges.Count == 0) return;
        var pending = _deferredSelectionChanges.ToArray();
        _deferredSelectionChanges.Clear();
        foreach (var e in pending) SelectionChanged?.Invoke(this, e);
    }

    private void UpdatePrimary(KumikoSelectionChangedEventArgs e)
    {
        var current = _model.CurrentItem;
        if (e.AddedItems.Count > 0)
        {
            _primary = current != null && _model.IsSelected(current) ? current : e.AddedItems[^1];
        }
        else if (_primary != null && !_model.IsSelected(_primary))
        {
            _primary = TopmostSelectedItem();
        }
    }

    private object? TopmostSelectedItem()
    {
        if (_model.SelectedRows.Count == 0) return null;
        return _model.Source.GetItem(_model.SelectedRows.Min());
    }

    private void OnViewChanged(object? sender, KumikoViewChangedEventArgs e)
    {
        if (e.IsStructural) Publish();
    }

    private void Publish()
    {
        if (_applyingItem) return; // SetSelectedItem publishes once it is done
        int index = SelectedIndex;
        var cell = _model.CurrentCell;
        if (ReferenceEquals(_publishedItem, _primary) && _publishedIndex == index && _publishedCell.Equals(cell))
            return;
        _publishedItem = _primary;
        _publishedIndex = index;
        _publishedCell = cell;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // ── List → model ──

    private void OnListChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_syncing) return;
        ApplyListToModel();
    }

    private void ApplyListToModel()
    {
        _syncing = true;
        try
        {
            _model.SetSelectedItems(_list.Cast<object>().Where(i => i != null));
        }
        finally { _syncing = false; }

        // A collection must not be changed from inside its own CollectionChanged, so the list is not trimmed
        // here: items the grid could not select (hidden, or refused by the selection mode) stay in it until
        // the next grid-side change.
    }

    private void WriteList(IEnumerable<object> items)
    {
        if (_list.IsReadOnly || _list.IsFixedSize) return;
        _syncing = true;
        try
        {
            _list.Clear();
            foreach (var item in items) _list.Add(item);
        }
        finally { _syncing = false; }
    }
}

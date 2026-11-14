using System.Collections.ObjectModel;

namespace KumikoUI.Core.Models;

/// <summary>
/// The selected items and the current cell of a grid, tracked by item rather than by visible row: when the
/// view is sorted, filtered, regrouped or the collection changes, the same items stay selected and the current
/// cell stays on its item and column. Items that are no longer visible leave the selection. Only data rows can
/// be selected; group header and group summary rows can hold the current cell but are never selected.
/// </summary>
public sealed class KumikoSelectionModel
{
    private readonly HashSet<object> _items = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<object, int> _rowOfItem = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<int> _rows = new();

    // The current cell and the shift-range anchor are tracked by row key (an item, or a group row) and column.
    private object? _currentKey;
    private KumikoColumn? _currentColumn;
    private CellPosition _currentCell = CellPosition.Invalid;
    private object? _anchorKey;
    private int _anchorRow = -1;

    /// <summary>Creates the selection of <paramref name="source"/>'s rows and follows its view changes.</summary>
    public KumikoSelectionModel(KumikoSource source)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        SelectedItems = new ReadOnlySet<object>(_items);
        SelectedRows = new ReadOnlySet<int>(_rows);
        source.ViewChanged += OnViewChanged;
    }

    /// <summary>The data source whose rows are selected.</summary>
    public KumikoSource Source { get; }

    /// <summary>Active selection mode controlling click behavior.</summary>
    public KumikoSelectionMode Mode { get; set; } = KumikoSelectionMode.Extended;

    /// <summary>
    /// Whether a tap on the empty part of the rows area — below the last row or right of the last column —
    /// clears the selection and the current cell (committing an open edit first), as list views do. Default
    /// <c>true</c>; set <c>false</c> to keep the selection.
    /// </summary>
    public bool ClearSelectionOnEmptyAreaTap { get; set; } = true;

    /// <summary>The selected data items.</summary>
    public IReadOnlySet<object> SelectedItems { get; }

    /// <summary>The visible rows of the selected items, in the current view.</summary>
    public IReadOnlySet<int> SelectedRows { get; }

    /// <summary>
    /// The current (keyboard focus) cell, as visible row and column index. It follows its item and column
    /// through view changes, and becomes invalid when its row is no longer visible.
    /// </summary>
    public CellPosition CurrentCell
    {
        get => _currentCell;
        set => SetCurrentCell(value.Row, value.Column);
    }

    /// <summary>The data item of the current cell, or null when there is none or it is on a group row.</summary>
    public object? CurrentItem => _currentKey is GroupRowKey ? null : _currentKey;

    /// <summary>
    /// Whether a cell editor is open. The input controller and the host controls keep it in step with the
    /// <see cref="Editing.EditSession"/>; it is read-only for application code.
    /// </summary>
    public bool IsEditing { get; internal set; }

    // ── Events ──

    /// <summary>
    /// Raised when the current cell moves, or follows its item through a view change. It is raised without a
    /// selection change when only the current cell differs.
    /// </summary>
    public event EventHandler? CurrentCellChanged;

    /// <summary>Raised when items are added to or removed from the selection.</summary>
    public event EventHandler<KumikoSelectionChangedEventArgs>? SelectionChanged;

    // ── Current cell ──

    /// <summary>
    /// Move the current cell to a visible row and column. An out-of-range row clears it. Does not change the
    /// selection.
    /// </summary>
    public void SetCurrentCell(int row, int column)
    {
        var previous = _currentCell;
        _currentKey = row >= 0 ? Source.GetRowKey(row) : null;
        if (_currentKey == null)
        {
            _currentColumn = null;
            _currentCell = CellPosition.Invalid;
        }
        else
        {
            _currentColumn = column >= 0 && column < Source.Columns.Count ? Source.Columns[column] : null;
            _currentCell = new CellPosition(row, column);
        }

        if (!previous.Equals(_currentCell)) CurrentCellChanged?.Invoke(this, EventArgs.Empty);
    }

    // ── Selection ──

    /// <summary>Whether the specified visible row is selected.</summary>
    public bool IsRowSelected(int row) => _rows.Contains(row);

    /// <summary>Whether <paramref name="item"/> is selected.</summary>
    public bool IsSelected(object item) => item != null && _items.Contains(item);

    /// <summary>
    /// Select a visible item (in <see cref="KumikoSelectionMode.Single"/> mode, instead of the current
    /// selection). Items that are not visible, and any item in <see cref="KumikoSelectionMode.None"/> mode, are
    /// ignored.
    /// </summary>
    public void Select(object item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (Mode == KumikoSelectionMode.None) return;
        int row = Source.IndexOfRowKey(item);
        if (!IsDataRow(row)) return;

        Change(() =>
        {
            if (Mode == KumikoSelectionMode.Single) ClearItems();
            AddRow(row);
        });
    }

    /// <summary>
    /// Replace the selection with <paramref name="items"/> in one change, raising one
    /// <see cref="SelectionChanged"/> with the delta. Items that are not visible data rows are ignored; in
    /// <see cref="KumikoSelectionMode.Single"/> mode only the last visible item is kept, and in
    /// <see cref="KumikoSelectionMode.None"/> mode the selection is emptied. The current cell does not move.
    /// </summary>
    public void SetSelectedItems(IEnumerable<object> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var rows = new List<int>();
        if (Mode != KumikoSelectionMode.None)
        {
            rows.AddRange(items
                .Select(item => item == null ? -1 : Source.IndexOfRowKey(item))
                .Where(IsDataRow));
            if (Mode == KumikoSelectionMode.Single && rows.Count > 1) rows.RemoveRange(0, rows.Count - 1);
        }

        Change(() =>
        {
            ClearItems();
            foreach (int row in rows) AddRow(row);
        });
    }

    /// <summary>Remove <paramref name="item"/> from the selection.</summary>
    public void Deselect(object item)
    {
        ArgumentNullException.ThrowIfNull(item);
        Change(() => RemoveItem(item));
    }

    /// <summary>
    /// Add the data rows from <paramref name="fromRow"/> to <paramref name="toRow"/> (inclusive, either order)
    /// to the selection; group rows in between are skipped. In <see cref="KumikoSelectionMode.Single"/> mode
    /// only <paramref name="toRow"/> is selected.
    /// </summary>
    public void SelectRange(int fromRow, int toRow)
    {
        if (Mode == KumikoSelectionMode.None) return;
        if (Mode == KumikoSelectionMode.Single)
        {
            if (IsDataRow(toRow)) Change(() => { ClearItems(); AddRow(toRow); });
            return;
        }

        Change(() => AddRange(fromRow, toRow));
    }

    /// <summary>
    /// Select every visible data row (group rows are skipped). Has no effect in
    /// <see cref="KumikoSelectionMode.None"/> or <see cref="KumikoSelectionMode.Single"/> mode.
    /// </summary>
    public void SelectAll()
    {
        if (Mode is KumikoSelectionMode.None or KumikoSelectionMode.Single) return;
        Change(() => AddRange(0, Source.RowCount - 1));
    }

    /// <summary>Clear the selection, the current cell and the range anchor, and stop editing.</summary>
    public void Clear()
    {
        Change(ClearItems);
        SetCurrentCell(-1, -1);
        _anchorKey = null;
        _anchorRow = -1;
        IsEditing = false;
    }

    /// <summary>
    /// Processes a row click, applying the selection logic for the current <see cref="Mode"/>: single-select,
    /// toggle (Multiple), or Shift/Ctrl range and toggle selection (Extended). The current cell moves to the row.
    /// Group rows take the current cell but never join the selection.
    /// </summary>
    /// <param name="row">Zero-based visible row index that was clicked.</param>
    /// <param name="shift">Whether the Shift modifier is held (range select in Extended mode).</param>
    /// <param name="control">Whether the Ctrl (or Command) modifier is held (toggle in Extended mode).</param>
    public void HandleRowSelection(int row, bool shift, bool control)
    {
        if (row < 0 || row >= Source.RowCount) return;

        // The current cell moves first, so SelectionChanged reports where it now is.
        SetCurrentCell(row, _currentCell.IsValid ? _currentCell.Column : 0);

        if (Mode != KumikoSelectionMode.None)
        {
            bool isData = IsDataRow(row);
            bool extendRange = Mode == KumikoSelectionMode.Extended && shift && _anchorRow >= 0;
            Change(() =>
            {
                if (extendRange)
                {
                    ClearItems();
                    AddRange(_anchorRow, row);
                }
                else if (Mode == KumikoSelectionMode.Multiple || (Mode == KumikoSelectionMode.Extended && control))
                {
                    if (isData) ToggleRow(row);
                }
                else if (isData)
                {
                    ClearItems();
                    AddRow(row);
                }
            });

            if (!extendRange)
            {
                _anchorKey = Source.GetRowKey(row);
                _anchorRow = row;
            }
        }
    }

    // ── Navigation ──

    /// <summary>Moves the current cell up one row and updates selection if applicable.</summary>
    public void NavigateUp(int totalRows, bool shift)
    {
        if (!_currentCell.IsValid) { SetCurrentCell(0, Math.Max(0, Source.ToColumnIndex(0))); return; }
        MoveTo(Math.Max(0, _currentCell.Row - 1), _currentCell.Column, shift, totalRows);
    }

    /// <summary>Moves the current cell down one row and updates selection if applicable.</summary>
    public void NavigateDown(int totalRows, bool shift)
    {
        if (!_currentCell.IsValid) { SetCurrentCell(0, Math.Max(0, Source.ToColumnIndex(0))); return; }
        MoveTo(Math.Min(totalRows - 1, _currentCell.Row + 1), _currentCell.Column, shift, totalRows);
    }

    /// <summary>
    /// Moves the current cell one column to the left in on-screen order (<see cref="KumikoSource.DisplayColumns"/>:
    /// left-frozen, scrolling, right-frozen), skipping hidden columns. It stays put at the first column.
    /// </summary>
    /// <param name="shift">Whether Shift is held.</param>
    public void NavigateLeft(bool shift)
    {
        if (!_currentCell.IsValid) return;
        int display = Source.ToDisplayIndex(_currentCell.Column);
        int target = display < 0 ? Source.DisplayColumns.Count - 1 : display - 1;
        MoveToDisplayColumn(target, shift);
    }

    /// <summary>
    /// Moves the current cell one column to the right in on-screen order, skipping hidden columns. It stays
    /// put at the last column.
    /// </summary>
    /// <param name="shift">Whether Shift is held.</param>
    public void NavigateRight(bool shift)
    {
        if (!_currentCell.IsValid) return;
        int display = Source.ToDisplayIndex(_currentCell.Column);
        MoveToDisplayColumn(display < 0 ? 0 : display + 1, shift);
    }

    /// <summary>
    /// Moves the current cell to the first visible column in on-screen order, or to row 0 as well if Ctrl is
    /// held.
    /// </summary>
    /// <param name="totalRows">The number of visible rows.</param>
    /// <param name="shift">Whether Shift is held.</param>
    /// <param name="control">Whether Ctrl (or Command) is held.</param>
    public void NavigateHome(int totalRows, bool shift, bool control)
    {
        if (!_currentCell.IsValid) return;
        int column = Source.ToColumnIndex(0);
        MoveTo(control ? 0 : _currentCell.Row, column >= 0 ? column : _currentCell.Column, shift, totalRows);
    }

    /// <summary>
    /// Moves the current cell to the last visible column in on-screen order, or to the last row as well if
    /// Ctrl is held.
    /// </summary>
    /// <param name="totalRows">The number of visible rows.</param>
    /// <param name="shift">Whether Shift is held.</param>
    /// <param name="control">Whether Ctrl (or Command) is held.</param>
    public void NavigateEnd(int totalRows, bool shift, bool control)
    {
        if (!_currentCell.IsValid) return;
        int column = Source.ToColumnIndex(Source.DisplayColumns.Count - 1);
        MoveTo(control ? totalRows - 1 : _currentCell.Row, column >= 0 ? column : _currentCell.Column, shift, totalRows);
    }

    /// <summary>Moves within the current row to a display column; a position outside the visible columns does nothing.</summary>
    private void MoveToDisplayColumn(int displayIndex, bool shift)
    {
        int column = Source.ToColumnIndex(displayIndex);
        if (column < 0) return;
        MoveTo(_currentCell.Row, column, shift, 0);
    }

    /// <summary>Moves the current cell up by one page of rows.</summary>
    /// <param name="pageSize">The number of rows in a page.</param>
    /// <param name="totalRows">The number of visible rows.</param>
    /// <param name="shift">Whether Shift is held.</param>
    public void NavigatePageUp(int pageSize, int totalRows, bool shift)
    {
        if (!_currentCell.IsValid) return;
        MoveTo(Math.Max(0, _currentCell.Row - pageSize), _currentCell.Column, shift, totalRows);
    }

    /// <summary>Moves the current cell down by one page of rows.</summary>
    public void NavigatePageDown(int pageSize, int totalRows, bool shift)
    {
        if (!_currentCell.IsValid) return;
        MoveTo(Math.Min(totalRows - 1, _currentCell.Row + pageSize), _currentCell.Column, shift, totalRows);
    }

    private void MoveTo(int row, int col, bool shift, int totalRows)
    {
        // In Multiple mode the keyboard moves focus only: Space toggles the row (Windows list convention).
        if (Mode == KumikoSelectionMode.Multiple)
        {
            SetCurrentCell(row, col);
            return;
        }

        SetCurrentCell(row, col);
        if (totalRows > 0)
            HandleRowSelection(row, shift, false);
    }

    // ── View changes ──

    /// <summary>
    /// Follow a view change: selected items keep their selection at their new rows and drop out when no
    /// longer visible; the current cell and range anchor follow their rows and columns.
    /// </summary>
    private void OnViewChanged(object? sender, KumikoViewChangedEventArgs e)
    {
        if (e.IsStructural)
        {
            Change(() =>
            {
                _rows.Clear();
                foreach (var item in _items.ToArray())
                {
                    int row = e.GetViewRow(item);
                    if (row >= 0)
                    {
                        _rowOfItem[item] = row;
                        _rows.Add(row);
                    }
                    else
                    {
                        _items.Remove(item);
                        _rowOfItem.Remove(item);
                    }
                }
            });

            _anchorRow = _anchorKey != null ? e.GetViewRow(_anchorKey) : -1;
            if (_anchorRow < 0) _anchorKey = null;
        }

        if (_currentKey == null) return;
        var previousCell = _currentCell;
        int currentRow = e.IsStructural ? e.GetViewRow(_currentKey) : _currentCell.Row;
        if (currentRow < 0)
        {
            _currentKey = null;
            _currentColumn = null;
            _currentCell = CellPosition.Invalid;
            CurrentCellChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        // The column moves with column reorders; if it was removed, keep the index it had (clamped).
        int column = _currentColumn != null ? IndexOfColumn(_currentColumn) : -1;
        if (column < 0)
        {
            column = Math.Clamp(_currentCell.Column, 0, Math.Max(0, Source.Columns.Count - 1));
            _currentColumn = column < Source.Columns.Count ? Source.Columns[column] : null;
        }
        _currentCell = new CellPosition(currentRow, column);
        if (!previousCell.Equals(_currentCell)) CurrentCellChanged?.Invoke(this, EventArgs.Empty);
    }

    // ── Internals ──

    private bool IsDataRow(int row) => row >= 0 && row < Source.RowCount && !Source.IsNonDataRow(row);

    private int IndexOfColumn(KumikoColumn column)
    {
        var columns = Source.Columns;
        for (int i = 0; i < columns.Count; i++)
        {
            if (ReferenceEquals(columns[i], column)) return i;
        }
        return -1;
    }

    private void AddRow(int row)
    {
        if (!IsDataRow(row)) return;
        var item = Source.GetItem(row);
        if (_items.Add(item))
        {
            _rowOfItem[item] = row;
            _rows.Add(row);
        }
    }

    private void AddRange(int fromRow, int toRow)
    {
        int start = Math.Max(0, Math.Min(fromRow, toRow));
        int end = Math.Min(Source.RowCount - 1, Math.Max(fromRow, toRow));
        for (int row = start; row <= end; row++)
            AddRow(row);
    }

    private void ToggleRow(int row)
    {
        var item = Source.GetItem(row);
        if (_items.Contains(item)) RemoveItem(item);
        else AddRow(row);
    }

    private void RemoveItem(object item)
    {
        if (!_items.Remove(item)) return;
        if (_rowOfItem.Remove(item, out int row))
            _rows.Remove(row);
    }

    private void ClearItems()
    {
        _items.Clear();
        _rowOfItem.Clear();
        _rows.Clear();
    }

    /// <summary>Apply a change to the selected items and raise <see cref="SelectionChanged"/> with its delta.</summary>
    private void Change(Action change)
    {
        var before = _items.ToArray();
        change();

        var removed = before.Where(item => !_items.Contains(item)).ToArray();
        var beforeSet = new HashSet<object>(before, ReferenceEqualityComparer.Instance);
        var added = _items.Where(item => !beforeSet.Contains(item)).ToArray();
        if (added.Length == 0 && removed.Length == 0) return;

        SelectionChanged?.Invoke(this, new KumikoSelectionChangedEventArgs(
            added, removed, _rows.Order().ToArray(), _currentCell));
    }
}

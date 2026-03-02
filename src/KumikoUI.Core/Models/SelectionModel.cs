using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Models;

/// <summary>
/// Selection mode for the grid.
/// </summary>
public enum SelectionMode
{
    /// <summary>No selection allowed.</summary>
    None,
    /// <summary>Single row or cell selection.</summary>
    Single,
    /// <summary>Multiple selections via Ctrl+Click.</summary>
    Multiple,
    /// <summary>Extended selection: single click = single, Shift = range, Ctrl = toggle.</summary>
    Extended
}

/// <summary>
/// What unit does selection operate on?
/// </summary>
public enum SelectionUnit
{
    /// <summary>Selection operates on entire rows.</summary>
    Row,
    /// <summary>Selection operates on individual cells.</summary>
    Cell
}

/// <summary>
/// Identifies a cell position.
/// </summary>
public readonly struct CellPosition : IEquatable<CellPosition>
{
    /// <summary>Row index (zero-based visible row).</summary>
    public int Row { get; }
    /// <summary>Column index (zero-based).</summary>
    public int Column { get; }

    public CellPosition(int row, int column)
    {
        Row = row;
        Column = column;
    }

    /// <summary>Whether the cell position refers to a valid (non-negative) row and column.</summary>
    public bool IsValid => Row >= 0 && Column >= 0;
    /// <summary>A sentinel value representing no cell (-1, -1).</summary>
    public static CellPosition Invalid => new(-1, -1);

    public bool Equals(CellPosition other) => Row == other.Row && Column == other.Column;
    public override bool Equals(object? obj) => obj is CellPosition other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Row, Column);
    public static bool operator ==(CellPosition a, CellPosition b) => a.Equals(b);
    public static bool operator !=(CellPosition a, CellPosition b) => !a.Equals(b);
    public override string ToString() => $"({Row}, {Column})";
}

/// <summary>
/// Manages selection state with support for single/multiple/extended modes.
/// </summary>
public class SelectionModel
{
    private int _anchorRow = -1; // For shift-range selection

    /// <summary>Active selection mode controlling click behavior.</summary>
    public SelectionMode Mode { get; set; } = SelectionMode.Extended;
    /// <summary>Whether selection targets rows or individual cells.</summary>
    public SelectionUnit Unit { get; set; } = SelectionUnit.Row;

    /// <summary>Selected visible-row indices.</summary>
    public HashSet<int> SelectedRows { get; } = new();

    /// <summary>Selected cell positions (when Unit == Cell).</summary>
    public HashSet<CellPosition> SelectedCells { get; } = new();

    /// <summary>Current focused cell (keyboard cursor position).</summary>
    public CellPosition CurrentCell { get; set; } = CellPosition.Invalid;

    /// <summary>Is cell editing active?</summary>
    public bool IsEditing { get; set; }

    // ── Events ──

    /// <summary>Raised when the set of selected rows or cells changes.</summary>
    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;

    // ── Row-level selection ──

    /// <summary>
    /// Processes a row click, applying the selection logic for the current <see cref="Mode"/>.
    /// Handles single-select, toggle (Multiple), or Shift/Ctrl range selection (Extended).
    /// </summary>
    /// <param name="row">Zero-based visible row index that was clicked.</param>
    /// <param name="shift">Whether the Shift modifier is held (range select in Extended mode).</param>
    /// <param name="ctrl">Whether the Ctrl modifier is held (toggle in Extended mode).</param>
    /// <param name="totalRows">Total number of visible rows (used for clamping).</param>
    public void HandleRowSelection(int row, bool shift, bool ctrl, int totalRows)
    {
        if (Mode == SelectionMode.None) return;

        var previousRows = new HashSet<int>(SelectedRows);

        switch (Mode)
        {
            case SelectionMode.Single:
                SelectedRows.Clear();
                SelectedRows.Add(row);
                _anchorRow = row;
                break;

            case SelectionMode.Multiple:
                // Toggle
                if (!SelectedRows.Add(row))
                    SelectedRows.Remove(row);
                _anchorRow = row;
                break;

            case SelectionMode.Extended:
                if (shift && _anchorRow >= 0)
                {
                    // Range select from anchor to current
                    SelectedRows.Clear();
                    int start = Math.Min(_anchorRow, row);
                    int end = Math.Max(_anchorRow, row);
                    for (int i = start; i <= end; i++)
                        SelectedRows.Add(i);
                }
                else if (ctrl)
                {
                    // Toggle individual row
                    if (!SelectedRows.Add(row))
                        SelectedRows.Remove(row);
                    _anchorRow = row;
                }
                else
                {
                    // Single select (clears previous)
                    SelectedRows.Clear();
                    SelectedRows.Add(row);
                    _anchorRow = row;
                }
                break;
        }

        // Update current cell row
        int col = CurrentCell.IsValid ? CurrentCell.Column : 0;
        CurrentCell = new CellPosition(row, col);

        if (!SelectedRows.SetEquals(previousRows))
            RaiseSelectionChanged();
    }

    /// <summary>Selects all rows. Has no effect in <see cref="SelectionMode.None"/> or <see cref="SelectionMode.Single"/> mode.</summary>
    /// <param name="totalRows">Total number of visible rows to select.</param>
    public void SelectAll(int totalRows)
    {
        if (Mode == SelectionMode.None || Mode == SelectionMode.Single) return;
        SelectedRows.Clear();
        for (int i = 0; i < totalRows; i++)
            SelectedRows.Add(i);
        RaiseSelectionChanged();
    }

    /// <summary>Clears all row and cell selections, resets the current cell, and stops editing.</summary>
    public void ClearSelection()
    {
        bool hadSelection = SelectedRows.Count > 0 || SelectedCells.Count > 0;
        SelectedRows.Clear();
        SelectedCells.Clear();
        CurrentCell = CellPosition.Invalid;
        _anchorRow = -1;
        IsEditing = false;
        if (hadSelection) RaiseSelectionChanged();
    }

    /// <summary>Returns whether the specified row is currently selected.</summary>
    public bool IsRowSelected(int row) => SelectedRows.Contains(row);
    /// <summary>Returns whether the specified cell is currently selected.</summary>
    public bool IsCellSelected(int row, int col) => SelectedCells.Contains(new CellPosition(row, col));

    // ── Navigation ──

    /// <summary>Moves the current cell up one row and updates selection if applicable.</summary>
    public void NavigateUp(int totalRows, bool shift)
    {
        if (!CurrentCell.IsValid) { CurrentCell = new CellPosition(0, 0); return; }
        int newRow = Math.Max(0, CurrentCell.Row - 1);
        MoveTo(newRow, CurrentCell.Column, shift, totalRows);
    }

    /// <summary>Moves the current cell down one row and updates selection if applicable.</summary>
    public void NavigateDown(int totalRows, bool shift)
    {
        if (!CurrentCell.IsValid) { CurrentCell = new CellPosition(0, 0); return; }
        int newRow = Math.Min(totalRows - 1, CurrentCell.Row + 1);
        MoveTo(newRow, CurrentCell.Column, shift, totalRows);
    }

    /// <summary>Moves the current cell one column to the left.</summary>
    public void NavigateLeft(int totalColumns, bool shift)
    {
        if (!CurrentCell.IsValid) return;
        int newCol = Math.Max(0, CurrentCell.Column - 1);
        MoveTo(CurrentCell.Row, newCol, shift, 0);
    }

    /// <summary>Moves the current cell one column to the right.</summary>
    public void NavigateRight(int totalColumns, bool shift)
    {
        if (!CurrentCell.IsValid) return;
        int newCol = Math.Min(totalColumns - 1, CurrentCell.Column + 1);
        MoveTo(CurrentCell.Row, newCol, shift, 0);
    }

    /// <summary>Moves the current cell to column 0, or to row 0 if Ctrl is held.</summary>
    public void NavigateHome(bool ctrlHeld, int totalRows, bool shift)
    {
        if (!CurrentCell.IsValid) return;
        int newRow = ctrlHeld ? 0 : CurrentCell.Row;
        int newCol = 0;
        MoveTo(newRow, newCol, shift, totalRows);
    }

    /// <summary>Moves the current cell to the last column, or to the last row if Ctrl is held.</summary>
    public void NavigateEnd(bool ctrlHeld, int totalRows, int totalColumns, bool shift)
    {
        if (!CurrentCell.IsValid) return;
        int newRow = ctrlHeld ? totalRows - 1 : CurrentCell.Row;
        int newCol = totalColumns - 1;
        MoveTo(newRow, newCol, shift, totalRows);
    }

    /// <summary>Moves the current cell up by one page of rows.</summary>
    public void NavigatePageUp(int pageSize, bool shift, int totalRows)
    {
        if (!CurrentCell.IsValid) return;
        int newRow = Math.Max(0, CurrentCell.Row - pageSize);
        MoveTo(newRow, CurrentCell.Column, shift, totalRows);
    }

    /// <summary>Moves the current cell down by one page of rows.</summary>
    public void NavigatePageDown(int pageSize, int totalRows, bool shift)
    {
        if (!CurrentCell.IsValid) return;
        int newRow = Math.Min(totalRows - 1, CurrentCell.Row + pageSize);
        MoveTo(newRow, CurrentCell.Column, shift, totalRows);
    }

    private void MoveTo(int row, int col, bool shift, int totalRows)
    {
        CurrentCell = new CellPosition(row, col);

        if (Unit == SelectionUnit.Row && totalRows > 0)
            HandleRowSelection(row, shift, false, totalRows);
    }

    private void RaiseSelectionChanged()
    {
        SelectionChanged?.Invoke(this, new SelectionChangedEventArgs(
            new List<int>(SelectedRows), CurrentCell));
    }
}

/// <summary>
/// Event arguments raised when the grid selection changes.
/// </summary>
public class SelectionChangedEventArgs : EventArgs
{
    /// <summary>List of currently selected visible row indices.</summary>
    public IReadOnlyList<int> SelectedRows { get; }
    /// <summary>The currently focused cell position.</summary>
    public CellPosition CurrentCell { get; }

    public SelectionChangedEventArgs(IReadOnlyList<int> selectedRows, CellPosition currentCell)
    {
        SelectedRows = selectedRows;
        CurrentCell = currentCell;
    }
}

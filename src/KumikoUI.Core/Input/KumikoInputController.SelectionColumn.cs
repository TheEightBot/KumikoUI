using KumikoUI.Core.Models;

namespace KumikoUI.Core.Input;

/// <summary>The selection column (#321): a tap on a row's box, a tap on the check-all box in the header, and Space.</summary>
public sealed partial class KumikoInputController
{
    // The selection mode as of the last pointer event, so hover feedback can tell a usable box from a dimmed one
    private KumikoSelectionMode _selectionMode = KumikoSelectionMode.Extended;

    /// <summary>
    /// Raised after a tap on the check-all box of a selection column's header changed the selection. The argument is
    /// <c>true</c> when it selected every row and <c>false</c> when it cleared the selection. The MAUI and Uno grids
    /// use it to announce the change to a screen reader.
    /// </summary>
    public event EventHandler<bool>? CheckAllToggled;

    /// <summary>
    /// A release on a selection column's header that was pressed too toggles check-all: it selects every row when not
    /// all are selected and clears the selection when all are. It never sorts, filters or opens a menu. Returns
    /// whether the release was on a selection column's header (and so is handled).
    /// </summary>
    private bool TryHandleSelectionHeaderRelease(
        KumikoPointerEventArgs e, KumikoHitTestResult hit, KumikoSelectionModel selection, KumikoSource dataSource)
    {
        if (hit.Region != HitRegion.HeaderCell || hit.Column?.ColumnType != KumikoColumnType.Selection) return false;

        if (_pressRegion == HitRegion.HeaderCell && _pressColumnIndex == hit.ColumnIndex && selection.HasCheckAll &&
            CommitEditBeforeViewChange(dataSource, selection) && selection.ToggleCheckAll())
        {
            CheckAllToggled?.Invoke(this, selection.CheckAllState == true);
            Redraw();
        }
        e.Handled = true;
        return true;
    }

    /// <summary>
    /// A release on a selection column's cell whose box was pressed too: commits an open edit (a refused commit cancels the
    /// toggle), moves the current cell to the row and toggles the row's check box. The whole cell is the target. It
    /// never starts an edit and never raises the row and cell tap events. Returns whether the release was on a
    /// selection cell (and so is handled).
    /// </summary>
    private bool TryHandleSelectionCellRelease(
        KumikoPointerEventArgs e, KumikoHitTestResult hit, KumikoSelectionModel selection, KumikoSource dataSource)
    {
        if (hit.Region != HitRegion.Cell || hit.RowIndex < 0 || hit.Column?.ColumnType != KumikoColumnType.Selection)
            return false;

        e.Handled = true;
        _lastTapToggledCell = CellPosition.Invalid;
        // Only a press on this very box, released on it, toggles it
        if (_pressRegion != HitRegion.Cell || _pressColumnIndex != hit.ColumnIndex || _pressRowIndex != hit.RowIndex ||
            dataSource.IsNonDataRow(hit.RowIndex) || selection.Mode == KumikoSelectionMode.None)
            return true;
        if (!CommitEditBeforeViewChange(dataSource, selection)) return true;

        selection.SetCurrentCell(hit.RowIndex, CurrentColumnForBoxClick(selection, dataSource, hit.ColumnIndex));
        selection.HandleCheckBoxToggle(hit.RowIndex, (e.Modifiers & InputModifiers.Shift) != 0);
        Redraw();
        return true;
    }

    /// <summary>
    /// The column the current cell takes when a box is clicked: the one it is in, unless there is none yet or it is
    /// the selection column, then the first data column on screen (the selection column when it is the only one).
    /// </summary>
    private static int CurrentColumnForBoxClick(KumikoSelectionModel selection, KumikoSource dataSource, int boxColumn)
    {
        var current = selection.CurrentCell;
        if (current.IsValid && ColumnAt(dataSource, current.Column) is { } existing &&
            existing.ColumnType != KumikoColumnType.Selection)
            return current.Column;

        var display = dataSource.DisplayColumns;
        for (int d = 0; d < display.Count; d++)
        {
            if (display[d].ColumnType != KumikoColumnType.Selection)
                return dataSource.ToColumnIndex(d);
        }
        return boxColumn;
    }

    /// <summary>
    /// Space on a current cell in a selection column toggles its row's box (Shift+Space range-toggles from the
    /// anchor, as a Shift-click does). Returns whether the current cell is in a selection column.
    /// </summary>
    private bool TryToggleCurrentSelectionCell(KumikoSelectionModel selection, KumikoSource dataSource, bool shift)
    {
        var current = selection.CurrentCell;
        if (!current.IsValid || ColumnAt(dataSource, current.Column)?.ColumnType != KumikoColumnType.Selection)
            return false;
        selection.HandleCheckBoxToggle(current.Row, shift);
        Redraw();
        return true;
    }
}

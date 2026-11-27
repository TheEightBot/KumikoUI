namespace KumikoUI.Core.Models;

// The check box path of the selection: what a selection column's boxes and its check-all header read and do (#321).
public sealed partial class KumikoSelectionModel
{
    /// <summary>
    /// Whether every, some or none of the view's data rows are selected: <c>true</c> when all of
    /// <see cref="KumikoSource.DataRowCount"/> are, <c>false</c> when none is, <c>null</c> (indeterminate) otherwise.
    /// It is what a selection column's header box shows. Cheap: two integer reads.
    /// </summary>
    /// <remarks>
    /// The selection only holds items of the current view, so comparing the counts is exact. The view is the rows
    /// left after filters and, with grouping, the rows of expanded groups.
    /// </remarks>
    public bool? CheckAllState
    {
        get
        {
            int selected = _items.Count;
            if (selected == 0) return false;
            return selected >= Source.DataRowCount ? true : null;
        }
    }

    /// <summary>
    /// Whether a tap on a row selects it. Default <c>true</c>. When <c>false</c>, a tap on a row, outside a selection
    /// column's check box, only moves the current cell (<see cref="HandleRowSelection"/> does not change the
    /// selection), so check boxes are the only way to select with the pointer. The keyboard is not affected.
    /// </summary>
    public bool SelectOnRowClick { get; set; } = true;

    /// <summary>
    /// Selects or deselects the data rows between two visible rows, inclusive and in either order, in one change that
    /// raises one <see cref="SelectionChanged"/>. Group rows are skipped, rows out of range are ignored. In
    /// <see cref="KumikoSelectionMode.Single"/> mode, selecting leaves only <paramref name="toRow"/> selected;
    /// deselecting removes the rows in the range. Nothing happens in <see cref="KumikoSelectionMode.None"/> mode.
    /// </summary>
    /// <param name="fromRow">One end of the range, a visible row index.</param>
    /// <param name="toRow">The other end of the range, a visible row index.</param>
    /// <param name="selected"><c>true</c> to select the rows, <c>false</c> to deselect them.</param>
    public void SetRangeSelected(int fromRow, int toRow, bool selected)
    {
        if (Mode == KumikoSelectionMode.None) return;
        if (selected && Mode == KumikoSelectionMode.Single)
        {
            if (IsDataRow(toRow)) Change(() => { ClearItems(); AddRow(toRow); });
            return;
        }

        Change(() => SetRange(fromRow, toRow, selected));
    }

    /// <summary>
    /// The click path of a selection column's check box: toggles the selection of the data row
    /// <paramref name="row"/>, without the "a plain click replaces the selection" rule of
    /// <see cref="HandleRowSelection"/>. The current cell and the range anchor move to the row, and one
    /// <see cref="SelectionChanged"/> is raised.
    /// </summary>
    /// <remarks>
    /// In <see cref="KumikoSelectionMode.Single"/> mode checking a row replaces the selection and unchecking it clears
    /// it. In <see cref="KumikoSelectionMode.Multiple"/> and <see cref="KumikoSelectionMode.Extended"/> mode the row
    /// is toggled and the other rows are left alone; with <paramref name="shift"/> and an anchor, the row's new state
    /// is applied to every data row from the anchor to the row (<see cref="SetRangeSelected"/>). Nothing happens in
    /// <see cref="KumikoSelectionMode.None"/> mode or on a group row.
    /// </remarks>
    /// <param name="row">The visible row whose box was clicked.</param>
    /// <param name="shift">Whether Shift is held (range toggle).</param>
    public void HandleCheckBoxToggle(int row, bool shift)
    {
        if (Mode == KumikoSelectionMode.None || !IsDataRow(row)) return;

        // The current cell moves first, so SelectionChanged reports where it now is; its column is kept.
        SetCurrentCell(row, _currentCell.IsValid ? _currentCell.Column : Math.Max(0, Source.ToColumnIndex(0)));

        bool selected = !IsRowSelected(row);
        if (Mode == KumikoSelectionMode.Single)
        {
            Change(() =>
            {
                ClearItems();
                if (selected) AddRow(row);
            });
        }
        else if (shift && _anchorRow >= 0)
        {
            SetRangeSelected(_anchorRow, row, selected);
        }
        else
        {
            Change(() =>
            {
                if (selected) AddRow(row);
                else RemoveItem(Source.GetItem(row));
            });
        }

        _anchorKey = Source.GetRowKey(row);
        _anchorRow = row;
    }

    /// <summary>
    /// The click path of a selection column's header box: selects every data row of the view when not all are
    /// selected, clears the selection when all are. The current cell stays. Only <see cref="KumikoSelectionMode.Multiple"/>
    /// and <see cref="KumikoSelectionMode.Extended"/> have a check-all.
    /// </summary>
    /// <returns>Whether the mode has a check-all; when it has not, nothing changed.</returns>
    internal bool ToggleCheckAll()
    {
        if (Mode is KumikoSelectionMode.None or KumikoSelectionMode.Single) return false;
        if (CheckAllState == true) SetSelectedItems(Array.Empty<object>());
        else SelectAll();
        return true;
    }

    /// <summary>Whether the header of a selection column shows a check-all box in the current mode.</summary>
    internal bool HasCheckAll => Mode is KumikoSelectionMode.Multiple or KumikoSelectionMode.Extended;

    private void SetRange(int fromRow, int toRow, bool selected)
    {
        if (selected)
        {
            AddRange(fromRow, toRow);
            return;
        }

        int start = Math.Max(0, Math.Min(fromRow, toRow));
        int end = Math.Min(Source.RowCount - 1, Math.Max(fromRow, toRow));
        for (int row = start; row <= end; row++)
        {
            if (IsDataRow(row)) RemoveItem(Source.GetItem(row));
        }
    }
}

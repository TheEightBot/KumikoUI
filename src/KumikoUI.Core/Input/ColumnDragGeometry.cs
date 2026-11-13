using KumikoUI.Core.Models;

namespace KumikoUI.Core.Input;

/// <summary>
/// The geometry of a column header drag, in viewport x (from the left edge of the grid). The columns are laid out as
/// the renderer draws them: the left drag handle (if any), the left-frozen pane, the scrolling pane (moved by the
/// horizontal scroll), then the right-frozen pane and the right drag handle (if any). Allocation-free.
/// </summary>
/// <remarks>
/// A column moves only within its own pane (left-frozen, scrolling or right-frozen); change
/// <see cref="KumikoColumn.FreezeMode"/> to move it to another pane. The drop slot is found from the on-screen
/// boundaries of the pane's other visible columns, left where they are drawn: the column goes before the first of
/// them whose middle is right of the pointer, or after the last one. The pointer is kept within the pane first, so a
/// drop over another pane or a drag handle lands at the near end of the column's own pane, and a scrolling column
/// hidden under a frozen pane is never the target.
/// </remarks>
internal static class ColumnDragGeometry
{
    /// <summary>The viewport x range of a column's pane.</summary>
    private static void PaneBounds(
        ColumnFreezeMode pane, IReadOnlyList<KumikoColumn> columns, ScrollState scroll, KumikoStyle style,
        out float paneLeft, out float paneRight, out float firstColumnLeft)
    {
        float handleOffset = 0, rightHandleWidth = 0;
        if (style.ShowRowDragHandle)
        {
            if (style.RowDragHandlePosition == DragHandlePosition.Left)
                handleOffset = style.RowDragHandleWidth;
            else
                rightHandleWidth = style.RowDragHandleWidth;
        }

        float frozenWidth = 0, rightFrozenWidth = 0;
        for (int i = 0; i < columns.Count; i++) // not foreach: the interface enumerator would be boxed
        {
            var column = columns[i];
            if (!column.IsVisible) continue;
            if (column.FreezeMode == ColumnFreezeMode.Left) frozenWidth += column.Width;
            else if (column.FreezeMode == ColumnFreezeMode.Right) rightFrozenWidth += column.Width;
        }

        float rightFrozenLeft = scroll.ViewportWidth - rightHandleWidth - rightFrozenWidth;
        switch (pane)
        {
            case ColumnFreezeMode.Left:
                paneLeft = firstColumnLeft = handleOffset;
                paneRight = handleOffset + frozenWidth;
                break;
            case ColumnFreezeMode.Right:
                paneLeft = firstColumnLeft = rightFrozenLeft;
                paneRight = rightFrozenLeft + rightFrozenWidth;
                break;
            default:
                paneLeft = handleOffset + frozenWidth;
                paneRight = Math.Max(paneLeft, rightFrozenLeft);
                firstColumnLeft = paneLeft - scroll.OffsetX;
                break;
        }
    }

    /// <summary>
    /// The viewport x of the left edge of <c>columns[columnIndex]</c> as drawn, or NaN when the index is out of
    /// range or the column is hidden.
    /// </summary>
    public static float ScreenLeft(
        IReadOnlyList<KumikoColumn> columns, int columnIndex, ScrollState scroll, KumikoStyle style)
    {
        if (columnIndex < 0 || columnIndex >= columns.Count || !columns[columnIndex].IsVisible) return float.NaN;

        var pane = columns[columnIndex].FreezeMode;
        PaneBounds(pane, columns, scroll, style, out _, out _, out float x);
        for (int i = 0; i < columnIndex; i++)
        {
            var column = columns[i];
            if (column.IsVisible && column.FreezeMode == pane) x += column.Width;
        }
        return x;
    }

    /// <summary>
    /// Where a drop at viewport <paramref name="pointerX"/> puts <c>columns[draggedIndex]</c>: its index in
    /// <paramref name="columns"/> after the move (remove it, then insert it at that index; see
    /// <see cref="KumikoColumnReorderedEventArgs.NewIndex"/>). Equal to <paramref name="draggedIndex"/> when the
    /// column stays where it is, and -1 when the index is out of range or the column is hidden.
    /// </summary>
    public static int DropIndex(
        IReadOnlyList<KumikoColumn> columns, int draggedIndex, float pointerX, ScrollState scroll, KumikoStyle style)
    {
        if (draggedIndex < 0 || draggedIndex >= columns.Count || !columns[draggedIndex].IsVisible) return -1;

        var pane = columns[draggedIndex].FreezeMode;
        PaneBounds(pane, columns, scroll, style, out float paneLeft, out float paneRight, out float x);
        float pointer = Math.Clamp(pointerX, paneLeft, Math.Max(paneLeft, paneRight));

        int before = -1, last = -1;
        for (int i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            if (!column.IsVisible || column.FreezeMode != pane) continue;
            float left = x;
            x += column.Width;
            if (i == draggedIndex) continue; // the boundaries are the other columns', where they are drawn

            if (pointer < left + column.Width / 2)
            {
                before = i;
                break;
            }
            last = i;
        }

        // Indices in the list without the dragged column, which is where the insert happens.
        if (before >= 0) return before > draggedIndex ? before - 1 : before;
        if (last >= 0) return last > draggedIndex ? last : last + 1;
        return draggedIndex; // the only visible column of its pane
    }
}

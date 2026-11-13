using KumikoUI.Core.Models;

namespace KumikoUI.Core.Layout;

/// <summary>
/// Which pane each column is drawn in (#268): left-frozen, scrolling or right-frozen. The renderer, the layout
/// engine, <see cref="KumikoScroller"/>, the hit tester and the input controller all read panes through here, so
/// they agree on where every column is.
/// </summary>
/// <remarks>
/// <para>
/// A column is pinned where its <see cref="KumikoColumn.FreezeMode"/> says, unless the frozen panes do not fit:
/// when the visible columns are wider than the column area (the viewport less the row drag handle) and the
/// left-frozen and right-frozen columns together are wider than
/// <see cref="KumikoStyle.MaxFrozenColumnsWidthRatio"/> of it, <see cref="Resolve"/> unpins columns until they fit.
/// Right-frozen columns go first, from the inner edge of their pane (the leftmost one) outward, then left-frozen
/// columns from the inner edge (the rightmost one). So the two panes never overlap and some of the width is always
/// left for scrolling columns, which stay reachable.
/// </para>
/// <para>
/// An unpinned column joins the scrolling pane at its place in the on-screen order
/// (<see cref="KumikoSource.DisplayColumns"/>, which does not change): a left-frozen one at the start of the
/// scrolling columns, a right-frozen one at their end. Within the scrolling pane the columns are therefore ordered
/// by <see cref="ScrollPass"/> first and by index second. The decision is made again on every layout pass, so a
/// column is pinned again as soon as the viewport is wide enough.
/// </para>
/// <para>Allocation-free: it runs every frame and on every hit test.</para>
/// </remarks>
internal static class FrozenPanes
{
    /// <summary>
    /// Decides which frozen columns stay pinned in a viewport <paramref name="viewportWidth"/> wide, from the
    /// columns' current widths, and records it on the columns (<see cref="KumikoColumn.Pane"/>). Idempotent: the
    /// same columns, style and width always give the same panes. A viewport that has not been measured yet (zero
    /// or less wide) unpins nothing.
    /// </summary>
    /// <param name="columns">The columns, in order.</param>
    /// <param name="style">Supplies the row drag handle and <see cref="KumikoStyle.MaxFrozenColumnsWidthRatio"/>.</param>
    /// <param name="viewportWidth">The width of the whole grid, drag handle included.</param>
    public static void Resolve(IReadOnlyList<KumikoColumn> columns, KumikoStyle style, float viewportWidth)
    {
        float columnArea = viewportWidth - (style.ShowRowDragHandle ? style.RowDragHandleWidth : 0);

        float total = 0, frozen = 0;
        for (int i = 0; i < columns.Count; i++)
        {
            var c = columns[i];
            if (!c.IsVisible) continue;
            total += c.Width;
            if (c.FreezeMode != ColumnFreezeMode.None) frozen += c.Width;
        }

        // Nothing to unpin when nothing scrolls (every column fits) or the panes fit their share.
        float ratio = style.MaxFrozenColumnsWidthRatio;
        float cap = columnArea * (float.IsNaN(ratio) ? 0 : Math.Clamp(ratio, 0f, 1f));
        float excess = viewportWidth > 0 && total > columnArea ? frozen - cap : 0;

        // Right-frozen columns first, in Columns order: the first one is drawn at the inner edge of its pane.
        for (int i = 0; i < columns.Count; i++)
        {
            var c = columns[i];
            if (c.FreezeMode == ColumnFreezeMode.Right) excess = Suspend(c, excess);
            else if (c.FreezeMode == ColumnFreezeMode.None) c.SuspendedFreezeMode = ColumnFreezeMode.None;
        }

        // Then left-frozen columns from the last one, at the inner edge of the left pane.
        for (int i = columns.Count - 1; i >= 0; i--)
        {
            var c = columns[i];
            if (c.FreezeMode == ColumnFreezeMode.Left) excess = Suspend(c, excess);
        }
    }

    /// <summary>Unpins <paramref name="column"/> while there is excess left, and returns what is left.</summary>
    private static float Suspend(KumikoColumn column, float excess)
    {
        if (column.IsVisible && excess > 0)
        {
            column.SuspendedFreezeMode = column.FreezeMode;
            return excess - column.Width;
        }
        column.SuspendedFreezeMode = ColumnFreezeMode.None;
        return excess;
    }

    /// <summary>
    /// The order of a scrolling column within the scrolling pane, before its index: 0 for an unpinned left-frozen
    /// column, 1 for a column that is not frozen, 2 for an unpinned right-frozen column; -1 for a pinned column.
    /// Iterate the scrolling pane in on-screen order with three passes over the columns.
    /// </summary>
    /// <param name="column">The column.</param>
    public static int ScrollPass(KumikoColumn column)
    {
        if (column.Pane != ColumnFreezeMode.None) return -1;
        return column.FreezeMode switch
        {
            ColumnFreezeMode.Left => 0,
            ColumnFreezeMode.Right => 2,
            _ => 1,
        };
    }

    /// <summary>The total width of the visible pinned left-frozen and right-frozen columns.</summary>
    /// <param name="columns">The columns, in order.</param>
    public static (float Left, float Right) GetWidths(IReadOnlyList<KumikoColumn> columns)
    {
        // Indexed loop: no enumerator or LINQ on the per-frame and keyboard-navigation paths.
        float left = 0, right = 0;
        for (int i = 0; i < columns.Count; i++)
        {
            var c = columns[i];
            if (!c.IsVisible) continue;
            var pane = c.Pane;
            if (pane == ColumnFreezeMode.Left) left += c.Width;
            else if (pane == ColumnFreezeMode.Right) right += c.Width;
        }
        return (left, right);
    }

    /// <summary>
    /// The x of the column at <paramref name="columnIndex"/> within its pane: the width of the visible columns
    /// drawn before it in the same pane. For a scrolling column that is its offset from the first scrolling column.
    /// </summary>
    /// <param name="columns">The columns, in order.</param>
    /// <param name="columnIndex">The index into <paramref name="columns"/>.</param>
    public static float GetOffsetInPane(IReadOnlyList<KumikoColumn> columns, int columnIndex)
    {
        var column = columns[columnIndex];
        var pane = column.Pane;
        float x = 0;
        if (pane != ColumnFreezeMode.None)
        {
            for (int i = 0; i < columnIndex; i++)
                if (columns[i].IsVisible && columns[i].Pane == pane) x += columns[i].Width;
            return x;
        }

        int pass = ScrollPass(column);
        for (int i = 0; i < columns.Count; i++)
        {
            var c = columns[i];
            if (!c.IsVisible) continue;
            int p = ScrollPass(c);
            if (p >= 0 && (p < pass || (p == pass && i < columnIndex))) x += c.Width;
        }
        return x;
    }
}

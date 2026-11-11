using KumikoUI.Core.Models;

namespace KumikoUI.Core.Layout;

/// <summary>
/// Programmatic scrolling (#56): where a row or column is, and the scroll offsets that bring it into view. The hosts'
/// <c>ScrollToRow</c>, <c>ScrollToItem</c> and <c>ScrollToColumn</c>, and the keyboard's keep-the-current-cell-visible
/// logic, all go through here, so they agree on the fixed bands over the rows and the frozen panes.
/// </summary>
/// <remarks>
/// <para>
/// Vertically, <see cref="ScrollState.OffsetY"/> is measured over the <em>scrolling</em> rows only: at offset 0 the
/// first row after the frozen rows sits right below the group panel, header, top summaries and frozen rows. Every row
/// has the same height, group header and group summary rows included, so a row's place is its index times
/// <see cref="KumikoStyle.RowHeight"/>. The visible part of the scrolling rows ends above the pinned bottom summary
/// footer, and a sticky group header (<see cref="KumikoStyle.StickyGroupHeaders"/>) covers the top of that part, so
/// a row scrolled to the start is placed below the headers that would be pinned over it.
/// </para>
/// <para>
/// Horizontally, <see cref="ScrollState.OffsetX"/> is measured over the scrolling columns only, between the
/// left-frozen and the right-frozen columns, net of the row drag handle.
/// </para>
/// <para>
/// Targets are clamped to the scrollable range. The content size is refreshed from the data first, so scrolling
/// before the first paint still lands (the first paint clamps it again with the final column widths).
/// </para>
/// </remarks>
public static class KumikoScroller
{
    /// <summary>
    /// The top edge of <paramref name="row"/> in viewport coordinates at the current scroll offset, counting the
    /// group panel, the header, the top summary rows and the frozen rows above it. A frozen row has a fixed Y; a
    /// scrolling row's Y moves with <see cref="ScrollState.OffsetY"/> and can be off screen (negative or beyond the
    /// viewport).
    /// </summary>
    /// <param name="source">The rows.</param>
    /// <param name="style">Row, header and summary heights.</param>
    /// <param name="scroll">The scroll state supplying the vertical offset.</param>
    /// <param name="row">The row in the flat view (group header and summary rows count).</param>
    /// <returns>The row's top edge in viewport pixels.</returns>
    public static float GetRowY(KumikoSource source, KumikoStyle style, ScrollState scroll, int row)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(scroll);
        float top = GetRowsTop(source, style);
        int frozen = source.EffectiveFrozenRowCount;
        return row < frozen
            ? top + row * style.RowHeight
            : top + frozen * style.RowHeight + (row - frozen) * style.RowHeight - scroll.OffsetY;
    }

    /// <summary>
    /// The left edge of the column at <paramref name="columnIndex"/> in viewport coordinates at the current scroll
    /// offset: frozen columns at a fixed X, right-frozen columns against the right edge, scrolling columns moving
    /// with <see cref="ScrollState.OffsetX"/>. Returns <see cref="float.NaN"/> for a hidden or out-of-range column.
    /// </summary>
    /// <param name="columns">The columns, in order.</param>
    /// <param name="style">Supplies the row drag handle width.</param>
    /// <param name="scroll">The scroll state supplying the horizontal offset and viewport width.</param>
    /// <param name="columnIndex">The index into <paramref name="columns"/>.</param>
    /// <returns>The column's left edge in viewport pixels.</returns>
    public static float GetColumnX(IReadOnlyList<KumikoColumn> columns, KumikoStyle style, ScrollState scroll, int columnIndex)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(scroll);
        if (columnIndex < 0 || columnIndex >= columns.Count || !columns[columnIndex].IsVisible) return float.NaN;

        var column = columns[columnIndex];
        var (leftHandle, rightHandle) = GetHandleWidths(style);
        var (frozenWidth, rightFrozenWidth) = GetFrozenWidths(columns);

        float x = 0;
        if (column.IsFrozen)
        {
            for (int i = 0; i < columnIndex; i++)
                if (columns[i].IsVisible && columns[i].IsFrozen) x += columns[i].Width;
            return leftHandle + x;
        }
        if (column.IsFrozenRight)
        {
            for (int i = 0; i < columnIndex; i++)
                if (columns[i].IsVisible && columns[i].IsFrozenRight) x += columns[i].Width;
            return scroll.ViewportWidth - rightHandle - rightFrozenWidth + x;
        }

        x = frozenWidth;
        for (int i = 0; i < columnIndex; i++)
            if (columns[i].IsVisible && !columns[i].IsFrozen && !columns[i].IsFrozenRight) x += columns[i].Width;
        return leftHandle + x - scroll.OffsetX;
    }

    /// <summary>
    /// The vertical scroll offset that puts <paramref name="row"/> at <paramref name="position"/> in the scrolling
    /// rows, before clamping to the scrollable range. A frozen row never scrolls, so the current offset comes back.
    /// </summary>
    /// <param name="source">The rows.</param>
    /// <param name="style">Row, header and summary heights.</param>
    /// <param name="scroll">The scroll state supplying the viewport and current offset.</param>
    /// <param name="row">The row in the flat view.</param>
    /// <param name="position">Where the row should land.</param>
    /// <returns>The vertical offset.</returns>
    public static float GetRowOffsetY(KumikoSource source, KumikoStyle style, ScrollState scroll, int row,
        KumikoScrollPosition position = KumikoScrollPosition.Nearest)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(scroll);
        int frozen = source.EffectiveFrozenRowCount;
        if (row < frozen) return scroll.OffsetY;

        float rowHeight = style.RowHeight;
        float rowTop = (row - frozen) * rowHeight;
        float visibleHeight = Math.Max(rowHeight, GetScrollableRowsViewportHeight(scroll, style, source));

        // Sticky group headers pinned over the top of the rows once this row is at the top
        float band = Math.Min(PinnedGroupHeaders.BandHeightAbove(source, style, row), visibleHeight - rowHeight);
        float offset = scroll.OffsetY;

        switch (position)
        {
            case KumikoScrollPosition.Start:
                return rowTop - band;
            case KumikoScrollPosition.End:
                return rowTop + rowHeight - visibleHeight;
            case KumikoScrollPosition.Center:
                return rowTop - band - (visibleHeight - band - rowHeight) / 2f;
            default:
                if (rowTop - band < offset) return rowTop - band;
                if (rowTop + rowHeight > offset + visibleHeight) return rowTop + rowHeight - visibleHeight;
                return offset;
        }
    }

    /// <summary>
    /// The horizontal scroll offset that puts the column at <paramref name="columnIndex"/> at
    /// <paramref name="position"/> between the frozen columns, before clamping to the scrollable range. A frozen
    /// or hidden column never scrolls, so the current offset comes back.
    /// </summary>
    /// <param name="columns">The columns, in order.</param>
    /// <param name="style">Supplies the row drag handle width.</param>
    /// <param name="scroll">The scroll state supplying the viewport and current offset.</param>
    /// <param name="columnIndex">The index into <paramref name="columns"/>.</param>
    /// <param name="position">Where the column should land.</param>
    /// <returns>The horizontal offset.</returns>
    public static float GetColumnOffsetX(IReadOnlyList<KumikoColumn> columns, KumikoStyle style, ScrollState scroll,
        int columnIndex, KumikoScrollPosition position = KumikoScrollPosition.Nearest)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(scroll);
        if (columnIndex < 0 || columnIndex >= columns.Count) return scroll.OffsetX;
        var column = columns[columnIndex];
        if (!column.IsVisible || column.IsFrozen || column.IsFrozenRight) return scroll.OffsetX;

        var (frozenWidth, rightFrozenWidth) = GetFrozenWidths(columns);

        float colLeft = frozenWidth;
        for (int i = 0; i < columnIndex; i++)
            if (columns[i].IsVisible && !columns[i].IsFrozen && !columns[i].IsFrozenRight) colLeft += columns[i].Width;
        float colRight = colLeft + column.Width;

        // The row drag handle takes viewport width from the columns as well; the scrolling area is
        // [frozenWidth, scrollableWidth) in content coordinates.
        var (leftHandle, rightHandle) = GetHandleWidths(style);
        float scrollableWidth = scroll.ViewportWidth - Math.Max(leftHandle, rightHandle) - rightFrozenWidth;
        float areaWidth = Math.Max(column.Width, scrollableWidth - frozenWidth);
        float offset = scroll.OffsetX;

        switch (position)
        {
            case KumikoScrollPosition.Start:
                return colLeft - frozenWidth;
            case KumikoScrollPosition.End:
                return colRight - scrollableWidth;
            case KumikoScrollPosition.Center:
                return colLeft - frozenWidth - (areaWidth - column.Width) / 2f;
            default:
                if (colLeft < offset + frozenWidth) return colLeft - frozenWidth;
                if (colRight > offset + scrollableWidth) return colRight - scrollableWidth;
                return offset;
        }
    }

    /// <summary>
    /// Scrolls so <paramref name="row"/> lands at <paramref name="position"/>, leaving the horizontal offset alone.
    /// Raises <see cref="ScrollState.ScrollChanged"/> once when the offset moves. Returns false, scrolling nothing,
    /// when <paramref name="row"/> is out of range.
    /// </summary>
    /// <param name="scroll">The scroll state to move.</param>
    /// <param name="source">The rows.</param>
    /// <param name="style">Row, header and summary heights.</param>
    /// <param name="row">The row in the flat view.</param>
    /// <param name="position">Where the row should land.</param>
    /// <returns>Whether the row exists.</returns>
    public static bool ScrollToRow(ScrollState scroll, KumikoSource source, KumikoStyle style, int row,
        KumikoScrollPosition position = KumikoScrollPosition.Nearest)
    {
        ArgumentNullException.ThrowIfNull(scroll);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(style);
        if (row < 0 || row >= source.RowCount) return false;

        RefreshContentExtents(scroll, source, style);
        scroll.ScrollTo(scroll.OffsetX, GetRowOffsetY(source, style, scroll, row, position));
        return true;
    }

    /// <summary>
    /// Scrolls so the column at <paramref name="columnIndex"/> lands at <paramref name="position"/>, leaving the
    /// vertical offset alone. Raises <see cref="ScrollState.ScrollChanged"/> once when the offset moves. Returns
    /// false, scrolling nothing, when the column is out of range or hidden.
    /// </summary>
    /// <param name="scroll">The scroll state to move.</param>
    /// <param name="source">The rows and columns.</param>
    /// <param name="style">Supplies the row drag handle width.</param>
    /// <param name="columnIndex">The index into <see cref="KumikoSource.Columns"/>.</param>
    /// <param name="position">Where the column should land.</param>
    /// <returns>Whether the column exists and is visible.</returns>
    public static bool ScrollToColumn(ScrollState scroll, KumikoSource source, KumikoStyle style, int columnIndex,
        KumikoScrollPosition position = KumikoScrollPosition.Nearest)
    {
        ArgumentNullException.ThrowIfNull(scroll);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(style);
        var columns = source.Columns;
        if (columnIndex < 0 || columnIndex >= columns.Count || !columns[columnIndex].IsVisible) return false;

        RefreshContentExtents(scroll, source, style);
        scroll.ScrollTo(GetColumnOffsetX(columns, style, scroll, columnIndex, position), scroll.OffsetY);
        return true;
    }

    /// <summary>
    /// Scrolls so <paramref name="item"/>'s row lands at <paramref name="position"/>, finding the row in the current
    /// sorted, filtered and grouped view. When the item is inside a collapsed group the group (and any collapsed
    /// group around it) is expanded first, as a tap on its header would (<see cref="KumikoSource.ExpandToItem"/>), so
    /// the item can be shown; pass <paramref name="expandGroups"/> false to leave collapsed groups alone, which makes
    /// such an item "not found". Returns false, scrolling nothing, when the item has no row: it is not in the
    /// source, a filter hides it, or an expansion was canceled.
    /// </summary>
    /// <param name="scroll">The scroll state to move.</param>
    /// <param name="source">The rows.</param>
    /// <param name="style">Row, header and summary heights.</param>
    /// <param name="item">The item to bring into view.</param>
    /// <param name="position">Where the item's row should land.</param>
    /// <param name="expandGroups">Whether to expand collapsed groups that hold the item.</param>
    /// <returns>Whether the item was found and scrolled to.</returns>
    public static bool ScrollToItem(ScrollState scroll, KumikoSource source, KumikoStyle style, object item,
        KumikoScrollPosition position = KumikoScrollPosition.Nearest, bool expandGroups = true)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(source);
        int row = expandGroups ? source.ExpandToItem(item) : source.IndexOfItem(item);
        return row >= 0 && ScrollToRow(scroll, source, style, row, position);
    }

    /// <summary>
    /// Brings <paramref name="scroll"/>'s content size up to date with the data and columns (the same sizes the
    /// renderer computes each frame), so a scroll before the first paint, or right after the data changed, clamps
    /// against the real extent.
    /// </summary>
    /// <param name="scroll">The scroll state to update.</param>
    /// <param name="source">The rows and columns.</param>
    /// <param name="style">Row, header, summary and drag handle sizes.</param>
    public static void RefreshContentExtents(ScrollState scroll, KumikoSource source, KumikoStyle style)
    {
        float groupPanel = source.IsGroupingActive ? style.GroupPanelHeight : 0;
        float summaries = (source.TopSummaryCount + source.BottomSummaryCount) * style.SummaryRowHeight;
        scroll.ContentHeight = style.HeaderHeight + source.RowCount * style.RowHeight + groupPanel + summaries;

        var (leftHandle, rightHandle) = GetHandleWidths(style);
        float width = 0;
        var columns = source.Columns;
        for (int i = 0; i < columns.Count; i++)
            if (columns[i].IsVisible) width += columns[i].Width;
        scroll.ContentWidth = width + leftHandle + rightHandle;
    }

    /// <summary>
    /// Height available to scrolling rows: the viewport minus the fixed bands drawn over or above them - grouping
    /// panel, header, top summaries, frozen rows and the pinned bottom summary footer.
    /// </summary>
    internal static float GetScrollableRowsViewportHeight(ScrollState scroll, KumikoStyle style, KumikoSource? source)
    {
        float groupPanelHeight = source != null && source.IsGroupingActive ? style.GroupPanelHeight : 0;
        float topSummaryHeight = source != null ? source.TopSummaryCount * style.SummaryRowHeight : 0;
        float frozenRowHeight = (source?.EffectiveFrozenRowCount ?? 0) * style.RowHeight;
        float pinnedFooterHeight = style.GetPinnedBottomSummaryHeight(source?.BottomSummaryCount ?? 0);
        return scroll.ViewportHeight - groupPanelHeight - style.HeaderHeight - topSummaryHeight
            - frozenRowHeight - pinnedFooterHeight;
    }

    /// <summary>The top of the scrolling rows at offset 0: below the group panel, header and top summaries, plus the frozen rows.</summary>
    private static float GetRowsTop(KumikoSource source, KumikoStyle style)
    {
        float groupPanel = source.IsGroupingActive ? style.GroupPanelHeight : 0;
        return groupPanel + style.HeaderHeight + source.TopSummaryCount * style.SummaryRowHeight;
    }

    /// <summary>The total width of the visible left-frozen and right-frozen columns.</summary>
    private static (float Left, float Right) GetFrozenWidths(IReadOnlyList<KumikoColumn> columns)
    {
        // Indexed loop over the visible columns: no enumerator or LINQ on the keyboard-navigation path.
        float left = 0, right = 0;
        for (int i = 0; i < columns.Count; i++)
        {
            var c = columns[i];
            if (c.IsVisible && c.IsFrozen) left += c.Width;
            if (c.IsVisible && c.IsFrozenRight) right += c.Width;
        }
        return (left, right);
    }

    private static (float Left, float Right) GetHandleWidths(KumikoStyle style)
    {
        if (!style.ShowRowDragHandle) return (0, 0);
        return style.RowDragHandlePosition == DragHandlePosition.Left
            ? (style.RowDragHandleWidth, 0)
            : (0, style.RowDragHandleWidth);
    }
}

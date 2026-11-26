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
/// first row after the frozen rows sits right below the group panel, header, top summaries and frozen rows. A row's
/// place comes from the source's row layout: its index times <see cref="KumikoStyle.RowHeight"/> in
/// <see cref="KumikoRowHeightMode.Fixed"/> mode, and the sum of the heights above it when row heights vary
/// (<see cref="KumikoStyle.RowHeightMode"/>, <see cref="KumikoStyle.RowHeightResolver"/>). In
/// <see cref="KumikoRowHeightMode.Auto"/> mode a row that was never drawn is placed with an estimated height, so a
/// scroll to it is refined on the next frame, once the rows around it are measured (one more
/// <see cref="ScrollState.ScrollChanged"/>). The visible part of the scrolling rows ends above the pinned bottom summary
/// footer, and a sticky group header (<see cref="KumikoStyle.StickyGroupHeaders"/>) covers the top of that part, so
/// a row scrolled to the start is placed below the headers that would be pinned over it.
/// </para>
/// <para>
/// Horizontally, <see cref="ScrollState.OffsetX"/> is measured over the scrolling columns only, between the
/// left-frozen and the right-frozen columns, net of the row drag handle. A frozen column the viewport is too narrow
/// to pin (<see cref="KumikoStyle.MaxFrozenColumnsWidthRatio"/>) counts as a scrolling column.
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
    /// <exception cref="ArgumentNullException">
    /// <paramref name="source"/>, <paramref name="style"/> or <paramref name="scroll"/> is <c>null</c>.
    /// </exception>
    public static float GetRowY(KumikoSource source, KumikoStyle style, ScrollState scroll, int row)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(scroll);
        float top = GetRowsTop(source, style);
        return source.RowLayout.GetRowY(row, top, scroll.OffsetY, style);
    }

    /// <summary>
    /// The height of <paramref name="row"/> as laid out now: <see cref="KumikoStyle.RowHeight"/> in
    /// <see cref="KumikoRowHeightMode.Fixed"/> mode and for group header and group summary rows; otherwise the row's
    /// measured or resolved height, or an estimate for a row that has not been drawn yet.
    /// </summary>
    /// <param name="source">The rows.</param>
    /// <param name="style">Row heights and the row height mode.</param>
    /// <param name="row">The row in the flat view.</param>
    /// <returns>The row's height in pixels.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="style"/> is <c>null</c>.</exception>
    public static float GetRowHeight(KumikoSource source, KumikoStyle style, int row)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(style);
        return source.RowLayout.GetRowHeight(row, style);
    }

    /// <summary>
    /// The left edge of the column at <paramref name="columnIndex"/> in viewport coordinates at the current scroll
    /// offset: frozen columns at a fixed X, right-frozen columns against the right edge, scrolling columns moving
    /// with <see cref="ScrollState.OffsetX"/>. A frozen column the viewport is too narrow to pin
    /// (<see cref="KumikoStyle.MaxFrozenColumnsWidthRatio"/>) moves with the scrolling columns. Returns
    /// <see cref="float.NaN"/> for a hidden or out-of-range column.
    /// </summary>
    /// <param name="columns">The columns, in order.</param>
    /// <param name="style">Supplies the row drag handle width.</param>
    /// <param name="scroll">The scroll state supplying the horizontal offset and viewport width.</param>
    /// <param name="columnIndex">The index into <paramref name="columns"/>.</param>
    /// <returns>The column's left edge in viewport pixels.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="columns"/>, <paramref name="style"/> or <paramref name="scroll"/> is <c>null</c>.
    /// </exception>
    public static float GetColumnX(IReadOnlyList<KumikoColumn> columns, KumikoStyle style, ScrollState scroll, int columnIndex)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(scroll);
        if (columnIndex < 0 || columnIndex >= columns.Count || !columns[columnIndex].IsVisible) return float.NaN;

        FrozenPanes.Resolve(columns, style, scroll.ViewportWidth);
        var (leftHandle, rightHandle) = GetHandleWidths(style);
        var (frozenWidth, rightFrozenWidth) = FrozenPanes.GetWidths(columns);
        float x = FrozenPanes.GetOffsetInPane(columns, columnIndex);

        return columns[columnIndex].Pane switch
        {
            ColumnFreezeMode.Left => leftHandle + x,
            ColumnFreezeMode.Right => scroll.ViewportWidth - rightHandle - rightFrozenWidth + x,
            _ => leftHandle + frozenWidth + x - scroll.OffsetX,
        };
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
    /// <exception cref="ArgumentNullException">
    /// <paramref name="source"/>, <paramref name="style"/> or <paramref name="scroll"/> is <c>null</c>.
    /// </exception>
    public static float GetRowOffsetY(KumikoSource source, KumikoStyle style, ScrollState scroll, int row,
        KumikoScrollPosition position = KumikoScrollPosition.Nearest)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(scroll);
        int frozen = source.EffectiveFrozenRowCount;
        if (row < frozen) return scroll.OffsetY;

        var rows = source.RowLayout;
        float rowHeight = rows.GetRowHeight(row, style);
        float rowTop = rows.GetScrollTop(row, style);
        float viewportHeight = GetScrollableRowsViewportHeight(scroll, style, source);
        float visibleHeight = Math.Max(rowHeight, viewportHeight);

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
                // A row taller than the rows' viewport whose top is on screen stays put; otherwise its top is shown.
                if (rowHeight > viewportHeight && !rows.IsUniform(style))
                    return rowTop < offset + viewportHeight ? offset : rowTop - band;
                if (rowTop + rowHeight > offset + visibleHeight) return rowTop + rowHeight - visibleHeight;
                return offset;
        }
    }

    /// <summary>
    /// After a programmatic scroll to <paramref name="row"/> with rows of varying height, whose neighbors may still
    /// have estimated heights: the next frame measures the rows around it and scrolls to it again with their real
    /// heights, unless the offset moved in between (a user scroll). Nothing with uniform rows.
    /// </summary>
    internal static void SetPendingRowTarget(ScrollState scroll, KumikoSource source, KumikoStyle style, int row,
        KumikoScrollPosition position = KumikoScrollPosition.Nearest)
    {
        var rows = source.RowLayout;
        rows.PendingScrollTarget = rows.IsUniform(style) || row < source.EffectiveFrozenRowCount
            ? null
            : new PendingRowScroll(row, position, scroll.OffsetY);
    }

    /// <summary>
    /// How many rows PageDown (<paramref name="down"/>) or PageUp moves the current row from
    /// <paramref name="fromRow"/>: one page of the rows' viewport. With uniform rows it is the number of rows that fit;
    /// with rows of varying height it is the distance to the row that starts the next page (PageDown, with
    /// <paramref name="fromRow"/> at the top) or that starts the page ending at <paramref name="fromRow"/> (PageUp).
    /// Always at least 1.
    /// </summary>
    internal static int GetPageRowCount(KumikoSource source, KumikoStyle style, ScrollState scroll, int fromRow, bool down)
    {
        float pageHeight = GetScrollableRowsViewportHeight(scroll, style, source);
        var rows = source.RowLayout;
        int rowCount = source.RowCount;
        if (rows.IsUniform(style) || fromRow < 0 || fromRow >= rowCount)
            return Math.Max(1, (int)(pageHeight / style.RowHeight));

        double top = rows.GetRowTop(fromRow, style);
        int target;
        if (down)
        {
            target = rows.GetRowAt(top + Math.Max(0, pageHeight), style);
        }
        else
        {
            double pageTop = top - Math.Max(0, pageHeight);
            target = rows.GetRowAt(pageTop, style);
            if (target < 0) target = 0;
            else if (rows.GetRowTop(target, style) < pageTop) target++;
        }
        return Math.Max(1, Math.Abs(target - fromRow));
    }

    /// <summary>
    /// The horizontal scroll offset that puts the column at <paramref name="columnIndex"/> at
    /// <paramref name="position"/> between the frozen columns, before clamping to the scrollable range. A pinned
    /// frozen column or a hidden column never scrolls, so the current offset comes back; a frozen column the viewport
    /// is too narrow to pin (<see cref="KumikoStyle.MaxFrozenColumnsWidthRatio"/>) scrolls like the others.
    /// </summary>
    /// <param name="columns">The columns, in order.</param>
    /// <param name="style">Supplies the row drag handle width.</param>
    /// <param name="scroll">The scroll state supplying the viewport and current offset.</param>
    /// <param name="columnIndex">The index into <paramref name="columns"/>.</param>
    /// <param name="position">Where the column should land.</param>
    /// <returns>The horizontal offset.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="columns"/>, <paramref name="style"/> or <paramref name="scroll"/> is <c>null</c>.
    /// </exception>
    public static float GetColumnOffsetX(IReadOnlyList<KumikoColumn> columns, KumikoStyle style, ScrollState scroll,
        int columnIndex, KumikoScrollPosition position = KumikoScrollPosition.Nearest)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(scroll);
        if (columnIndex < 0 || columnIndex >= columns.Count) return scroll.OffsetX;
        var column = columns[columnIndex];
        if (!column.IsVisible) return scroll.OffsetX;
        FrozenPanes.Resolve(columns, style, scroll.ViewportWidth);
        if (column.Pane != ColumnFreezeMode.None) return scroll.OffsetX;

        var (frozenWidth, rightFrozenWidth) = FrozenPanes.GetWidths(columns);

        float colLeft = frozenWidth + FrozenPanes.GetOffsetInPane(columns, columnIndex);
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
    /// <exception cref="ArgumentNullException">
    /// <paramref name="scroll"/>, <paramref name="source"/> or <paramref name="style"/> is <c>null</c>.
    /// </exception>
    public static bool ScrollToRow(ScrollState scroll, KumikoSource source, KumikoStyle style, int row,
        KumikoScrollPosition position = KumikoScrollPosition.Nearest)
    {
        ArgumentNullException.ThrowIfNull(scroll);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(style);
        if (row < 0 || row >= source.RowCount) return false;

        RefreshContentExtents(scroll, source, style);
        scroll.ScrollTo(scroll.OffsetX, GetRowOffsetY(source, style, scroll, row, position));
        SetPendingRowTarget(scroll, source, style, row, position);
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
    /// <exception cref="ArgumentNullException">
    /// <paramref name="scroll"/>, <paramref name="source"/> or <paramref name="style"/> is <c>null</c>.
    /// </exception>
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
    /// <exception cref="ArgumentNullException">
    /// <paramref name="item"/> or <paramref name="source"/> is <c>null</c>.
    /// </exception>
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
        var rows = source.RowLayout;
        float rowsHeight = rows.IsUniform(style)
            ? source.RowCount * style.RowHeight
            : (float)rows.GetTotalHeight(style);
        scroll.ContentHeight = style.HeaderHeight + rowsHeight + groupPanel + summaries;

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
        float frozenRowHeight = source?.RowLayout.GetFrozenHeight(style) ?? 0;
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

    private static (float Left, float Right) GetHandleWidths(KumikoStyle style)
    {
        if (!style.ShowRowDragHandle) return (0, 0);
        return style.RowDragHandlePosition == DragHandlePosition.Left
            ? (style.RowDragHandleWidth, 0)
            : (0, style.RowDragHandleWidth);
    }
}

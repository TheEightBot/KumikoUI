using KumikoUI.Core.Models;

namespace KumikoUI.Core.Input;

/// <summary>
/// The geometry of a row drag, shared by the input controller (drag start, drop, edge autoscroll) and the renderer
/// (ghost and drop indicator), so the drop indicator always shows where the drop lands. Every y is in viewport
/// coordinates: from the top of the grid, grouping panel included. Allocation-free.
/// </summary>
/// <remarks>
/// The drop target is the row under the middle of the ghost (move semantics: the dragged row ends up at that index
/// and the rows in between shift by one). The ghost's middle is kept within the rows area, between the top of the
/// data area (below the grouping panel, header and top summaries) and the pinned summary footer, so the ghost never
/// covers the header and the target is always a row on screen.
/// </remarks>
internal static class RowDragGeometry
{
    /// <summary>The top of the rows: below the grouping panel, the header and the top summary rows.</summary>
    public static float DataAreaTop(KumikoStyle style, KumikoSource dataSource) =>
        (dataSource.IsGroupingActive ? style.GroupPanelHeight : 0) + style.HeaderHeight +
        dataSource.TopSummaryCount * style.SummaryRowHeight;

    /// <summary>The top of the scrolling rows: below the frozen rows.</summary>
    public static float ScrollableTop(KumikoStyle style, KumikoSource dataSource) =>
        DataAreaTop(style, dataSource) + dataSource.EffectiveFrozenRowCount * style.RowHeight;

    /// <summary>The bottom of the rows area: the viewport bottom, or the top of the pinned summary footer.</summary>
    public static float RowsBottom(ScrollState scroll, KumikoStyle style, KumikoSource dataSource) =>
        scroll.ViewportHeight - style.GetPinnedBottomSummaryHeight(dataSource.BottomSummaryCount);

    /// <summary>The top of visible row <paramref name="row"/>: fixed for a frozen row, scrolled otherwise.</summary>
    public static float RowTop(int row, ScrollState scroll, KumikoStyle style, KumikoSource dataSource)
    {
        float top = DataAreaTop(style, dataSource) + row * style.RowHeight;
        return row < dataSource.EffectiveFrozenRowCount ? top : top - scroll.OffsetY;
    }

    /// <summary>
    /// Keeps a ghost top so that the ghost's middle stays within the rows area (see the remarks).
    /// </summary>
    public static float ClampGhostTop(float ghostTop, ScrollState scroll, KumikoStyle style, KumikoSource dataSource)
    {
        float half = style.RowHeight / 2;
        float min = DataAreaTop(style, dataSource) - half;
        float max = Math.Max(min, RowsBottom(scroll, style, dataSource) - half - 1);
        return Math.Clamp(ghostTop, min, max);
    }

    /// <summary>
    /// The visible row at viewport <paramref name="y"/>, clamped to the rows (-1 when there are none). Over the
    /// frozen rows it is a frozen row; below them the scroll offset counts.
    /// </summary>
    public static int RowAt(float y, ScrollState scroll, KumikoStyle style, KumikoSource dataSource)
    {
        int rowCount = dataSource.RowCount;
        if (rowCount == 0 || style.RowHeight <= 0) return -1;

        float top = DataAreaTop(style, dataSource);
        int frozen = dataSource.EffectiveFrozenRowCount;
        float frozenHeight = frozen * style.RowHeight;
        int row = frozen > 0 && y < top + frozenHeight
            ? Math.Clamp((int)MathF.Floor((y - top) / style.RowHeight), 0, frozen - 1)
            : frozen + (int)MathF.Floor((y - top - frozenHeight + scroll.OffsetY) / style.RowHeight);
        return Math.Clamp(row, 0, rowCount - 1);
    }

    /// <summary>The drop target for a ghost whose top is at <paramref name="ghostTop"/>: the row under its middle.</summary>
    public static int DropTarget(float ghostTop, ScrollState scroll, KumikoStyle style, KumikoSource dataSource) =>
        RowAt(ClampGhostTop(ghostTop, scroll, style, dataSource) + style.RowHeight / 2, scroll, style, dataSource);

    /// <summary>
    /// Whether a drop of <paramref name="dragRow"/> on <paramref name="target"/> can move it: a different data row
    /// in the same group. (A sort or an unsupported source is reported by the drop itself.)
    /// </summary>
    public static bool IsDroppable(int dragRow, int target, KumikoSource dataSource) =>
        target >= 0 && target != dragRow && target < dataSource.RowCount && !dataSource.IsNonDataRow(target) &&
        dataSource.AreInSameGroup(dragRow, target);

    /// <summary>
    /// The viewport y of the drop indicator for a drop of <paramref name="dragRow"/> on <paramref name="target"/>:
    /// below the target when the row moves down, above it when it moves up, kept within the area of the target's
    /// band (frozen or scrolling) so it never draws over the other.
    /// </summary>
    public static float IndicatorY(int dragRow, int target, ScrollState scroll, KumikoStyle style, KumikoSource dataSource)
    {
        float y = RowTop(target, scroll, style, dataSource) + (target > dragRow ? style.RowHeight : 0);
        float top = target < dataSource.EffectiveFrozenRowCount
            ? DataAreaTop(style, dataSource)
            : ScrollableTop(style, dataSource);
        return Math.Clamp(y, top, Math.Max(top, RowsBottom(scroll, style, dataSource)));
    }
}

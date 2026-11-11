using KumikoUI.Core.Models;

namespace KumikoUI.Core.Layout;

/// <summary>One group header pinned at the top of the data area (#174): its row, its level, and where it is drawn.</summary>
/// <param name="Row">The group header's row in the flat view.</param>
/// <param name="Level">The group's level (0 is the outermost).</param>
/// <param name="Y">The pinned header's top edge, in the same coordinates as the scrolling rows.</param>
internal readonly record struct PinnedGroupHeader(int Row, int Level, float Y);

/// <summary>
/// The layout of sticky group headers (<see cref="KumikoStyle.StickyGroupHeaders"/>, #174), shared by the renderer,
/// the hit tester and keyboard ensure-visible so all three agree on where the pinned headers are.
/// </summary>
/// <remarks>
/// <para>
/// Level <c>L</c> has a slot at <c>scrollTop + L * RowHeight</c>, so the levels stack with level 0 on top. The group
/// pinned at level <c>L</c> is the level-<c>L</c> group that contains the row under its slot, and it is pinned only
/// when its own header has scrolled above the slot (otherwise the header is on screen in its natural place, and no
/// deeper level is pinned either). Level <c>L</c> is considered only while every outer level is pinned.
/// </para>
/// <para>
/// Push-up: a pinned header never overlaps the row that ends its group (the next header at the same or an outer
/// level), so it is drawn at <c>min(slot, nextHeaderTop - RowHeight)</c> and slides up as that header arrives. An
/// inner header pushed up slides under the outer ones, which are drawn after it.
/// </para>
/// </remarks>
internal static class PinnedGroupHeaders
{
    /// <summary>
    /// Fill <paramref name="pinned"/> (cleared first) with the headers pinned for this scroll offset, outermost
    /// first, and return the bottom edge of the pinned band (<paramref name="scrollTop"/> when nothing is pinned).
    /// Allocates nothing once <paramref name="pinned"/> has room for every group level.
    /// </summary>
    /// <param name="source">The rows.</param>
    /// <param name="style">Row height and whether sticky headers are on.</param>
    /// <param name="scrollTop">Top edge of the scrolling rows: below the header, top summaries and frozen rows.</param>
    /// <param name="offsetY">The vertical scroll offset.</param>
    /// <param name="frozenRowCount">Rows that do not scroll; their headers are never pinned.</param>
    /// <param name="pinned">Receives the pinned headers.</param>
    public static float Compute(KumikoSource source, KumikoStyle style, float scrollTop, float offsetY,
        int frozenRowCount, List<PinnedGroupHeader> pinned)
    {
        pinned.Clear();
        float rowHeight = style.RowHeight;
        if (!style.StickyGroupHeaders || !source.IsGroupingActive || rowHeight <= 0)
            return scrollTop;

        int rowCount = source.RowCount;
        int levels = source.GroupDescriptions.Count;
        float bandBottom = scrollTop;

        for (int level = 0; level < levels; level++)
        {
            float slot = scrollTop + level * rowHeight;

            // The row under the slot's top edge (scrolling rows start at scrollTop, after the frozen rows).
            int row = frozenRowCount + (int)MathF.Floor((level * rowHeight + offsetY) / rowHeight);
            if (row < frozenRowCount || row >= rowCount) break;
            if (!source.TryGetEnclosingGroup(row, level, out int headerRow, out int endRow)) break;
            if (headerRow < frozenRowCount) break;

            float headerTop = RowTop(headerRow, scrollTop, offsetY, frozenRowCount, rowHeight);
            if (headerTop >= slot) break; // in its natural place, on screen

            float y = slot;
            if (endRow < rowCount)
                y = Math.Min(y, RowTop(endRow, scrollTop, offsetY, frozenRowCount, rowHeight) - rowHeight);

            pinned.Add(new PinnedGroupHeader(headerRow, level, y));
            bandBottom = Math.Max(bandBottom, y + rowHeight);
        }

        return bandBottom;
    }

    /// <summary>
    /// The height of the pinned band above <paramref name="row"/> once the row is scrolled to the top of the data
    /// area: the headers of every group that contains it (one per level for a data or group summary row, the outer
    /// levels for a group header). Keyboard ensure-visible keeps the current row below this band. Zero when sticky
    /// headers are off or nothing is grouped.
    /// </summary>
    public static float BandHeightAbove(KumikoSource? source, KumikoStyle style, int row)
    {
        if (source == null || !style.StickyGroupHeaders || !source.IsGroupingActive) return 0;
        int levels = source.GetGroupHeaderInfo(row) is { } header ? header.Level : source.GroupDescriptions.Count;
        return levels * style.RowHeight;
    }

    /// <summary>
    /// The scroll offset that puts a group header in its pinned slot: where a collapsed group's header stays after
    /// its pinned header is tapped, so the view does not jump to whatever followed the group.
    /// </summary>
    public static float SlotOffset(int headerRow, int level, int frozenRowCount, float rowHeight) =>
        (headerRow - frozenRowCount - level) * rowHeight;

    private static float RowTop(int row, float scrollTop, float offsetY, int frozenRowCount, float rowHeight) =>
        scrollTop + (row - frozenRowCount) * rowHeight - offsetY;
}

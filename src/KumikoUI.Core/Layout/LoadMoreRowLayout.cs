using KumikoUI.Core.Data;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Layout;

/// <summary>
/// The geometry of the load-more row (#318): one full-width band after the last row of the view and before the
/// unpinned bottom summary rows. The renderer (content height, drawing, the unpinned summaries below it), the
/// scroller's content extent, the hit tester and the loader's near-end check all read it from here, so they cannot
/// disagree. The band is not a row of the view: it is not counted in <see cref="KumikoSource.RowCount"/>.
/// </summary>
/// <remarks>
/// The height of the rows above the band is computed in <see cref="GetRowsHeight"/> alone; a layout with rows of
/// different heights changes that one method.
/// </remarks>
internal static class LoadMoreRowLayout
{
    /// <summary>The band's height this frame: 0 while it is hidden, else <see cref="KumikoStyle.LoadMoreRowHeight"/> (or the row height).</summary>
    public static float GetHeight(KumikoSource source, KumikoStyle style) =>
        source.IncrementalLoaderOrNull is { } loader && loader.RowKind != LoadMoreRowKind.None
            ? style.EffectiveLoadMoreRowHeight
            : 0f;

    /// <summary>The height of every row of the view (frozen and scrolling) stacked: where the band starts, in content.</summary>
    public static float GetRowsHeight(KumikoSource source, KumikoStyle style) => source.RowCount * style.RowHeight;

    /// <summary>
    /// The band's top edge in viewport coordinates below the group panel, for a data area (the top of the first row)
    /// at <paramref name="dataAreaTop"/> and a vertical offset of <paramref name="offsetY"/>.
    /// </summary>
    public static float GetTop(KumikoSource source, KumikoStyle style, float dataAreaTop, float offsetY) =>
        dataAreaTop + GetRowsHeight(source, style) - offsetY;

    /// <summary>
    /// How many rows fit the scrolling area (the viewport less the group panel, header, top summaries, frozen rows and
    /// the pinned footer): the default near-end threshold and the basis of the automatic batch size.
    /// </summary>
    public static int GetRowsPerViewport(ScrollState scroll, KumikoStyle style, KumikoSource source)
    {
        if (style.RowHeight <= 0) return 0;
        float height = KumikoScroller.GetScrollableRowsViewportHeight(scroll, style, source);
        return height <= 0 ? 0 : (int)(height / style.RowHeight);
    }
}

using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Layout;

/// <summary>
/// Where one of the grid's scroll bars is this frame (#316), in viewport coordinates (from the top-left of the grid,
/// grouping panel included). The renderer draws it, the hit tester resolves presses against it and the input controller
/// drags and pages with it, all from <see cref="ScrollBarGeometry.Compute"/>, so they cannot disagree.
/// </summary>
internal readonly struct ScrollBarAxisLayout
{
    /// <summary>Whether the bar exists: its visibility is not Never and the content overflows in its direction.</summary>
    public bool IsPresent { get; init; }

    /// <summary>True for the vertical bar, false for the horizontal one.</summary>
    public bool IsVertical { get; init; }

    /// <summary>
    /// The band along the viewport edge that a mouse or pen can press, <see cref="KumikoStyle.ScrollBarExpandedThickness"/>
    /// thick (the drawn bar is never thicker); the thumb moves along it.
    /// </summary>
    public KumikoRect Track { get; init; }

    /// <summary>Where the thumb starts along the axis (y for the vertical bar, x for the horizontal one).</summary>
    public float ThumbStart { get; init; }

    /// <summary>The thumb's length along the axis.</summary>
    public float ThumbLength { get; init; }

    /// <summary>
    /// The length of the band the bar scrolls (the scrolling rows' height, or the scrolling columns' width): one page
    /// of track paging.
    /// </summary>
    public float Page { get; init; }

    /// <summary>The scroll range in the bar's direction (<see cref="ScrollState.MaxOffsetY"/> or <see cref="ScrollState.MaxOffsetX"/>).</summary>
    public float MaxOffset { get; init; }

    /// <summary>The track's start along the axis.</summary>
    public float TrackStart => IsVertical ? Track.Y : Track.X;

    /// <summary>The track's length along the axis.</summary>
    public float TrackLength => IsVertical ? Track.Height : Track.Width;

    /// <summary>The thumb's rectangle, the full thickness of <see cref="Track"/>.</summary>
    public KumikoRect Thumb => IsVertical
        ? new KumikoRect(Track.X, ThumbStart, Track.Width, ThumbLength)
        : new KumikoRect(ThumbStart, Track.Y, ThumbLength, Track.Height);

    /// <summary>The coordinate of a point along the bar's axis.</summary>
    public float Along(float x, float y) => IsVertical ? y : x;

    /// <summary>The scroll offset that puts the thumb's start at <paramref name="thumbStart"/> (unclamped; NaN when it cannot move).</summary>
    public float OffsetForThumbStart(float thumbStart) =>
        ScrollThumb.OffsetAt(thumbStart, TrackStart, TrackLength, ThumbLength, MaxOffset);
}

/// <summary>Both scroll bars of one frame.</summary>
internal readonly struct ScrollBarLayout
{
    /// <summary>The vertical bar, at the right edge of the scrolling rows.</summary>
    public ScrollBarAxisLayout Vertical { get; init; }

    /// <summary>The horizontal bar, at the bottom of the rows, across the scrolling columns.</summary>
    public ScrollBarAxisLayout Horizontal { get; init; }

    /// <summary>The bar under viewport point (<paramref name="x"/>, <paramref name="y"/>), or <see cref="HitRegion.None"/>.</summary>
    public HitRegion HitTest(float x, float y)
    {
        if (Vertical.IsPresent && Vertical.Track.Contains(x, y))
            return y >= Vertical.ThumbStart && y < Vertical.ThumbStart + Vertical.ThumbLength
                ? HitRegion.VerticalScrollBarThumb
                : HitRegion.VerticalScrollBarTrack;
        if (Horizontal.IsPresent && Horizontal.Track.Contains(x, y))
            return x >= Horizontal.ThumbStart && x < Horizontal.ThumbStart + Horizontal.ThumbLength
                ? HitRegion.HorizontalScrollBarThumb
                : HitRegion.HorizontalScrollBarTrack;
        return HitRegion.None;
    }
}

/// <summary>
/// Lays out the grid's scroll bars (#316) from the scroll state and the fixed bands around the scrolling area. Content
/// extents come only from <see cref="ScrollState"/> (<see cref="ScrollState.MaxOffsetY"/>,
/// <see cref="ScrollState.MaxOffsetX"/>) and the rows band from
/// <see cref="KumikoScroller.GetScrollableRowsViewportHeight"/>, never from row counts, so the bars follow however the
/// layout measures rows. Allocation-free.
/// </summary>
internal static class ScrollBarGeometry
{
    /// <summary>
    /// The bars for this scroll state. The vertical bar spans the scrolling rows (below the grouping panel, the header,
    /// the top summaries and the frozen rows; above a pinned summary footer) at the viewport's right edge; the horizontal
    /// bar spans the scrolling columns (between the left drag handle and left-frozen pane and the right-frozen pane and
    /// right drag handle) at the bottom of the rows. Where both would meet, each stops short of the other. The thumb is
    /// as long as the share of the content the band shows (at least <see cref="KumikoStyle.ScrollBarMinThumbLength"/>).
    /// </summary>
    /// <param name="scroll">The scroll state, with its content extents up to date.</param>
    /// <param name="style">The grid style.</param>
    /// <param name="columns">The columns, their panes resolved for this viewport (<see cref="FrozenPanes.Resolve"/>).</param>
    /// <param name="source">The source, for grouping, summaries and frozen rows; null for none.</param>
    public static ScrollBarLayout Compute(ScrollState scroll, KumikoStyle style, IReadOnlyList<KumikoColumn> columns,
        KumikoSource? source)
    {
        float viewportWidth = scroll.ViewportWidth, viewportHeight = scroll.ViewportHeight;
        float thickness = ExpandedThickness(style);
        if (!(thickness > 0) || !(viewportWidth > 0) || !(viewportHeight > 0)) return default;

        // The scrolling rows band
        float rowsBand = KumikoScroller.GetScrollableRowsViewportHeight(scroll, style, source);
        float rowsBottom = viewportHeight - style.GetPinnedBottomSummaryHeight(source?.BottomSummaryCount ?? 0);
        float rowsTop = rowsBottom - rowsBand;

        // The scrolling columns band
        float leftHandle = 0, rightHandle = 0;
        if (style.ShowRowDragHandle)
        {
            if (style.RowDragHandlePosition == DragHandlePosition.Left) leftHandle = style.RowDragHandleWidth;
            else rightHandle = style.RowDragHandleWidth;
        }
        var (frozenWidth, rightFrozenWidth) = FrozenPanes.GetWidths(columns);
        float paneLeft = leftHandle + frozenWidth;
        float paneRight = viewportWidth - rightHandle - rightFrozenWidth;
        float paneWidth = paneRight - paneLeft;

        float maxY = scroll.MaxOffsetY, maxX = scroll.MaxOffsetX;
        bool vertical = style.VerticalScrollBarVisibility != KumikoScrollBarVisibility.Never && maxY >= MinOverflow
            && rowsBand > 0;
        bool horizontal = style.HorizontalScrollBarVisibility != KumikoScrollBarVisibility.Never && maxX >= MinOverflow
            && paneWidth > 0 && rowsBand >= thickness;

        // Where the bars would cross, each stops short of the other
        float verticalLeft = viewportWidth - thickness;
        float verticalBottom = horizontal ? rowsBottom - thickness : rowsBottom;
        float horizontalRight = vertical ? Math.Min(paneRight, verticalLeft) : paneRight;
        float minThumb = style.ScrollBarMinThumbLength;

        var v = default(ScrollBarAxisLayout);
        float vTrack = verticalBottom - rowsTop;
        if (vertical && vTrack > 0)
        {
            float length = ScrollThumb.Length(vTrack, rowsBand, rowsBand + maxY, minThumb);
            v = new ScrollBarAxisLayout
            {
                IsPresent = true,
                IsVertical = true,
                Track = new KumikoRect(verticalLeft, rowsTop, thickness, vTrack),
                ThumbLength = length,
                ThumbStart = ScrollThumb.Start(rowsTop, vTrack, length, scroll.OffsetY, maxY),
                Page = rowsBand,
                MaxOffset = maxY,
            };
        }

        var h = default(ScrollBarAxisLayout);
        float hTrack = horizontalRight - paneLeft;
        if (horizontal && hTrack > 0)
        {
            float length = ScrollThumb.Length(hTrack, paneWidth, paneWidth + maxX, minThumb);
            h = new ScrollBarAxisLayout
            {
                IsPresent = true,
                IsVertical = false,
                Track = new KumikoRect(paneLeft, rowsBottom - thickness, hTrack, thickness),
                ThumbLength = length,
                ThumbStart = ScrollThumb.Start(paneLeft, hTrack, length, scroll.OffsetX, maxX),
                Page = paneWidth,
                MaxOffset = maxX,
            };
        }

        return new ScrollBarLayout { Vertical = v, Horizontal = h };
    }

    /// <summary>The thinnest overflow, in pixels, that shows a bar (less is rounding, not content to scroll to).</summary>
    private const float MinOverflow = 0.5f;

    /// <summary>The bar's thickness at rest: <see cref="KumikoStyle.ScrollBarThickness"/>, never negative.</summary>
    public static float Thickness(KumikoStyle style) =>
        float.IsFinite(style.ScrollBarThickness) ? Math.Max(0, style.ScrollBarThickness) : 0;

    /// <summary>
    /// The bar's thickness while a mouse or pen hovers or drags it, and the width of its pointer band:
    /// <see cref="KumikoStyle.ScrollBarExpandedThickness"/>, never thinner than <see cref="Thickness"/>.
    /// </summary>
    public static float ExpandedThickness(KumikoStyle style) =>
        Math.Max(Thickness(style), float.IsFinite(style.ScrollBarExpandedThickness) ? style.ScrollBarExpandedThickness : 0);
}

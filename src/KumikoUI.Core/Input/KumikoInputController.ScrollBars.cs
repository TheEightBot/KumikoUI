using KumikoUI.Core.Layout;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Input;

/// <summary>
/// The grid's scroll bars with a mouse or a pen (#316): hovering a bar's edge band expands it (and shows an
/// <see cref="KumikoScrollBarVisibility.Auto"/> bar), a press on the thumb drags it, a press on the track pages toward
/// the pointer and repeats while held. A finger never lands on a bar: it pans through it. A press on a bar never pans,
/// drags a row or a column, or selects. Geometry comes from <see cref="ScrollBarGeometry"/>, as the renderer and the hit
/// tester read it.
/// </summary>
public sealed partial class KumikoInputController
{
    /// <summary>The wait after the first page of a press held on a track before it repeats, in milliseconds.</summary>
    internal const float ScrollBarPageDelayMs = 400f;

    /// <summary>The interval between the repeated pages of a press held on a track, in milliseconds.</summary>
    internal const float ScrollBarPageRepeatMs = 50f;

    private enum ScrollBarAxis { None, Vertical, Horizontal }

    // The bar the mouse or pen rests over (expanded and, in Auto, shown)
    private ScrollBarAxis _scrollBarHover;

    // The bar a press in progress holds: its thumb is dragged (_scrollBarPaging false) or its track pages
    private ScrollBarAxis _scrollBarPress;
    private bool _scrollBarPaging;

    // Thumb drag: the pointer's distance from the thumb's start, along the axis
    private float _scrollBarGrab;

    // Track paging: the direction (+1 toward the end, -1 toward the start), the pointer along the axis and whether it
    // is still over the track, the track (start, length, cross-axis band) and page at the press, and the time to the
    // next page
    private int _pageDirection;
    private float _pagePointer;
    private bool _pagePointerOnTrack;
    private float _pageTrackStart, _pageTrackLength, _pageCrossStart, _pageCrossEnd, _pageSize, _pageMinThumb;
    private float _pageWaitMs;

    /// <summary>Whether a press held on a scroll bar's track is paging toward the pointer (stepped by <see cref="UpdateAutoScroll"/>).</summary>
    private bool IsPagingScrollBar =>
        _scrollBarPaging && _scrollBarPress != ScrollBarAxis.None && _pagePointerOnTrack && _pageDirection != 0;

    /// <summary>Whether the vertical (or horizontal) bar is drawn thick: a mouse or pen hovers it, or a press holds it.</summary>
    internal bool IsScrollBarExpanded(bool vertical)
    {
        var axis = vertical ? ScrollBarAxis.Vertical : ScrollBarAxis.Horizontal;
        return _scrollBarHover == axis || _scrollBarPress == axis;
    }

    /// <summary>
    /// How visible an <see cref="KumikoScrollBarVisibility.Auto"/> bar is, from 0 to 1: fully while it is hovered or
    /// held, else as its clock says (fading only when <paramref name="style"/> allows animations).
    /// </summary>
    internal float ScrollBarOpacity(bool vertical, KumikoStyle style) =>
        IsScrollBarExpanded(vertical) ? 1f : _animations.ScrollBarOpacity(vertical, fade: CanAnimate(style));

    /// <summary>
    /// A press landed on <paramref name="region"/>: when it is a bar, start dragging its thumb or paging its track, and
    /// return true (the press is the bar's; nothing else acts on it).
    /// </summary>
    private bool TryBeginScrollBarPress(HitRegion region, KumikoPointerEventArgs e, ScrollState scroll, KumikoStyle style,
        KumikoSource dataSource)
    {
        _scrollBarPress = ScrollBarAxis.None; // a press whose release was lost ends here
        _scrollBarPaging = false;
        var axis = AxisOf(region);
        if (axis == ScrollBarAxis.None) return false;

        var bar = BarFor(axis, scroll, style, dataSource);
        e.Handled = true;
        if (!bar.IsPresent) return true;

        _scrollBarPress = axis;
        float along = bar.Along(e.X, e.Y);
        if (region is HitRegion.VerticalScrollBarThumb or HitRegion.HorizontalScrollBarThumb)
        {
            _scrollBarPaging = false;
            _scrollBarGrab = along - bar.ThumbStart;
        }
        else
        {
            _scrollBarPaging = true;
            _pageDirection = along < bar.ThumbStart ? -1 : 1;
            _pagePointer = along;
            _pagePointerOnTrack = true;
            _pageTrackStart = bar.TrackStart;
            _pageTrackLength = bar.TrackLength;
            _pageCrossStart = bar.IsVertical ? bar.Track.X : bar.Track.Y;
            _pageCrossEnd = bar.IsVertical ? bar.Track.Right : bar.Track.Bottom;
            _pageSize = bar.Page;
            _pageMinThumb = style.ScrollBarMinThumbLength;
            PageScrollBar(scroll); // the first page at once, the repeats after a pause
            _pageWaitMs = ScrollBarPageDelayMs;
        }
        Redraw(); // the bar expands
        return true;
    }

    /// <summary>A move during a press on a bar: the thumb follows the pointer, or paging follows it along the track.</summary>
    private void HandleScrollBarMove(KumikoPointerEventArgs e, ScrollState scroll, KumikoStyle style, KumikoSource dataSource)
    {
        e.Handled = true;
        if (_scrollBarPaging)
        {
            bool vertical = _scrollBarPress == ScrollBarAxis.Vertical;
            _pagePointer = vertical ? e.Y : e.X;
            float cross = vertical ? e.X : e.Y;
            _pagePointerOnTrack = cross >= _pageCrossStart && cross < _pageCrossEnd
                && _pagePointer >= _pageTrackStart && _pagePointer < _pageTrackStart + _pageTrackLength;
            return;
        }

        var bar = BarFor(_scrollBarPress, scroll, style, dataSource);
        if (!bar.IsPresent) return;
        float offset = bar.OffsetForThumbStart(bar.Along(e.X, e.Y) - _scrollBarGrab);
        if (float.IsNaN(offset)) return;

        float oldX = scroll.OffsetX, oldY = scroll.OffsetY;
        if (bar.IsVertical) scroll.ScrollTo(oldX, offset);
        else scroll.ScrollTo(offset, oldY);
        if (scroll.OffsetX != oldX || scroll.OffsetY != oldY) Redraw();
    }

    /// <summary>
    /// Ends a press on a bar (its release or cancel), and returns whether there was one. The bar stays expanded while
    /// the pointer is still over it; else an Auto bar holds and fades from now.
    /// </summary>
    private bool EndScrollBarPress(KumikoPointerEventArgs e)
    {
        if (_scrollBarPress == ScrollBarAxis.None) return false;
        var released = _scrollBarPress;
        _scrollBarPress = ScrollBarAxis.None;
        _scrollBarPaging = false;
        _pageDirection = 0;
        if (_scrollBarHover != released) _animations.ShowScrollBar(released == ScrollBarAxis.Vertical);
        e.Handled = true;
        Redraw();
        return true;
    }

    /// <summary>
    /// The mouse or pen rests over <paramref name="region"/>: over a bar it expands (and an Auto bar shows); leaving it,
    /// an Auto bar holds and fades from now. A finger hovers nothing.
    /// </summary>
    private void UpdateScrollBarHover(HitRegion region, KumikoPointerDeviceType deviceType)
    {
        var axis = deviceType == KumikoPointerDeviceType.Touch ? ScrollBarAxis.None : AxisOf(region);
        if (axis == _scrollBarHover) return;
        var left = _scrollBarHover;
        _scrollBarHover = axis;
        if (left != ScrollBarAxis.None && left != _scrollBarPress) _animations.ShowScrollBar(left == ScrollBarAxis.Vertical);
        Redraw();
    }

    /// <summary>Forgets the hovered and pressed bars (the grid was disabled).</summary>
    private void ResetScrollBars()
    {
        _scrollBarHover = ScrollBarAxis.None;
        _scrollBarPress = ScrollBarAxis.None;
        _scrollBarPaging = false;
        _pageDirection = 0;
    }

    /// <summary>
    /// One frame of a press held on a track (from <see cref="UpdateAutoScroll"/>): after the pause, a page every
    /// <see cref="ScrollBarPageRepeatMs"/> until the thumb reaches the pointer. Returns whether paging goes on.
    /// </summary>
    private bool StepScrollBarPaging(ScrollState scroll, float frameIntervalMs)
    {
        if (float.IsFinite(frameIntervalMs) && frameIntervalMs > 0) _pageWaitMs -= frameIntervalMs;
        while (_pageWaitMs <= 0 && IsPagingScrollBar)
        {
            PageScrollBar(scroll);
            _pageWaitMs += ScrollBarPageRepeatMs;
        }
        return IsPagingScrollBar;
    }

    /// <summary>
    /// Scrolls one page toward the pointer, unless the thumb already reached it; paging then stops until the pointer
    /// moves on along the track. The thumb is measured afresh, so paging follows content that grows meanwhile.
    /// </summary>
    private void PageScrollBar(ScrollState scroll)
    {
        bool vertical = _scrollBarPress == ScrollBarAxis.Vertical;
        float max = vertical ? scroll.MaxOffsetY : scroll.MaxOffsetX;
        float offset = vertical ? scroll.OffsetY : scroll.OffsetX;
        float thumbLength = Rendering.ScrollThumb.Length(_pageTrackLength, _pageSize, _pageSize + max, _pageMinThumb);
        float thumbStart = Rendering.ScrollThumb.Start(_pageTrackStart, _pageTrackLength, thumbLength, offset, max);
        bool reached = _pageDirection > 0 ? thumbStart + thumbLength >= _pagePointer : thumbStart <= _pagePointer;
        if (reached)
        {
            _pagePointerOnTrack = false; // until the pointer moves past the thumb again
            return;
        }

        float oldX = scroll.OffsetX, oldY = scroll.OffsetY;
        float delta = _pageDirection * _pageSize;
        scroll.ScrollBy(vertical ? 0 : delta, vertical ? delta : 0);
        if (scroll.OffsetX != oldX || scroll.OffsetY != oldY) Redraw();
        else _pagePointerOnTrack = false; // at the content edge
    }

    private static ScrollBarAxisLayout BarFor(ScrollBarAxis axis, ScrollState scroll, KumikoStyle style, KumikoSource dataSource)
    {
        var columns = dataSource.Columns;
        FrozenPanes.Resolve(columns, style, scroll.ViewportWidth);
        var layout = ScrollBarGeometry.Compute(scroll, style, columns, dataSource);
        return axis == ScrollBarAxis.Vertical ? layout.Vertical : layout.Horizontal;
    }

    private static ScrollBarAxis AxisOf(HitRegion region) => region switch
    {
        HitRegion.VerticalScrollBarThumb or HitRegion.VerticalScrollBarTrack => ScrollBarAxis.Vertical,
        HitRegion.HorizontalScrollBarThumb or HitRegion.HorizontalScrollBarTrack => ScrollBarAxis.Horizontal,
        _ => ScrollBarAxis.None,
    };
}

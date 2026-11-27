using System.Collections.ObjectModel;
using KumikoUI.Core.Layout;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Input;

/// <summary>Where a swiped row is in its life (#329).</summary>
internal enum SwipePhase
{
    /// <summary>No row is swiped.</summary>
    None,

    /// <summary>A finger is dragging the row sideways; the row follows it.</summary>
    Tracking,

    /// <summary>The row settled open (or is settling), its items shown beside it.</summary>
    Open,

    /// <summary>The row slides shut; it is closed already (<see cref="KumikoInputController.SwipeEnded"/> was raised).</summary>
    Closing,
}

/// <summary>
/// The row a finger swipes sideways to reveal drawn items (#329): its item and offset, the two lists of items, and the
/// geometry the renderer draws and the hit tester tests with. One row at a time. The controller drives it
/// (<c>KumikoInputController.Swipe.cs</c>); the snap after the finger lifts is timed by
/// <see cref="InteractionAnimations"/>. Nothing here allocates once constructed.
/// </summary>
internal sealed class RowSwipe
{
    /// <summary>How much of the pull past the items' width the row follows when there is no full swipe (rubber band).</summary>
    public const float RubberBandFactor = 0.3f;

    /// <summary>The share of the row's width a swipe must reach for a full swipe.</summary>
    public const float FullSwipeFraction = 0.6f;

    /// <summary>The speed past which a release flings the row open or shut whatever its offset, in pixels per millisecond.</summary>
    public const float FlingVelocity = 0.5f;

    /// <summary>How many times the vertical movement the horizontal movement must be for a pan to become a swipe.</summary>
    public const float DirectionRatio = 2f;

    /// <summary>The items revealed at the start edge, outermost (nearest the edge) first.</summary>
    public ObservableCollection<IActionButtonDefinition> Leading { get; } = new();

    /// <summary>The items revealed at the end edge, outermost (nearest the edge) first.</summary>
    public ObservableCollection<IActionButtonDefinition> Trailing { get; } = new();

    public SwipePhase Phase { get; set; }

    /// <summary>The swiped row's item: the row is found by it, and lost when the view changes.</summary>
    public object? Item { get; set; }

    /// <summary>The swiped row's index in the view of <see cref="ViewVersion"/>.</summary>
    public int Row { get; set; } = -1;

    /// <summary>The <see cref="KumikoSource.ViewVersion"/> the row index belongs to.</summary>
    public int ViewVersion { get; set; }

    public KumikoSwipeDirection Direction { get; set; }

    /// <summary>
    /// The row's horizontal offset while tracking, or the offset it settles at: positive moves it toward the end edge
    /// (leading items shown), negative toward the start edge (trailing items shown).
    /// </summary>
    public float Offset { get; set; }

    /// <summary>The offset a snap leaves from.</summary>
    public float SnapFrom { get; set; }

    /// <summary>The total width of this swipe's items (capped at <see cref="BandWidth"/>): the width it opens to.</summary>
    public float RevealWidth { get; set; }

    /// <summary>The width of the row band the row slides in (the viewport less any drag handle column).</summary>
    public float BandWidth { get; set; }

    /// <summary>Whether a full swipe can run this swipe's outermost item (<see cref="KumikoInputController.FullSwipeMode"/>).</summary>
    public bool FullSwipeEnabled { get; set; }

    /// <summary>Whether the row is past the full-swipe point: lifting now runs the outermost item.</summary>
    public bool FullSwipeArmed { get; set; }

    /// <summary>The item a press holds (drawn pressed), or -1.</summary>
    public int PressedItem { get; set; } = -1;

    /// <summary>Whether the snaps of this swipe animate (the controller's and the style's switches at its start).</summary>
    public bool Animate { get; set; }

    /// <summary>The items of this swipe's side.</summary>
    public IList<IActionButtonDefinition> Items => Direction == KumikoSwipeDirection.Leading ? Leading : Trailing;

    /// <summary>Whether either side has an item.</summary>
    public bool HasItems => Leading.Count > 0 || Trailing.Count > 0;

    /// <summary>Whether a row is swiped and not closed: a finger drags it, or it is open.</summary>
    public bool IsActive => Phase is SwipePhase.Tracking or SwipePhase.Open;

    /// <summary>Whether a swiped row is drawn: it is active, or it still slides shut.</summary>
    public bool IsShown(InteractionAnimations animations) =>
        IsActive || (Phase == SwipePhase.Closing && animations.IsSwipeSnapping);

    /// <summary>The offset the row is drawn at now: the tracked offset, or a snap's progress from where it left.</summary>
    public float CurrentOffset(InteractionAnimations animations)
    {
        float target = IsActive ? Offset : 0f;
        if (Phase == SwipePhase.Tracking || !animations.IsSwipeSnapping) return target;
        return SnapFrom + (target - SnapFrom) * animations.SwipeSnapProgress;
    }

    /// <summary>Forgets the swiped row (the snap's timing is the animations' to stop).</summary>
    public void Clear()
    {
        Phase = SwipePhase.None;
        Item = null;
        Row = -1;
        Offset = SnapFrom = 0;
        FullSwipeArmed = false;
        PressedItem = -1;
    }

    /// <summary>
    /// Whether the swiped row still shows <see cref="Item"/> in <paramref name="source"/>'s view: the view has not
    /// changed since the swipe began (a sort, a filter, a group toggled or rows added or removed close it).
    /// </summary>
    public bool IsCurrent(KumikoSource source) =>
        source.ViewVersion == ViewVersion && Row >= 0 && Row < source.RowCount && !source.IsNonDataRow(Row) &&
        Item != null && RowKeyComparer.Instance.Equals(source.GetItem(Row), Item);

    // ── Geometry (rows coordinates: below the grouping panel, as the renderer draws them) ──

    /// <summary>The left edge of the band a row slides in: after a left drag handle column.</summary>
    public static float BandLeft(KumikoStyle style) =>
        style.ShowRowDragHandle && style.RowDragHandlePosition == DragHandlePosition.Left ? style.RowDragHandleWidth : 0f;

    /// <summary>The right edge of the band a row slides in: before a right drag handle column.</summary>
    public static float BandRight(ScrollState scroll, KumikoStyle style) =>
        scroll.ViewportWidth -
        (style.ShowRowDragHandle && style.RowDragHandlePosition == DragHandlePosition.Right ? style.RowDragHandleWidth : 0f);

    /// <summary>
    /// The swiped row's rectangle (<paramref name="row"/>, from the band's left to its right edge) and the part of it on
    /// screen: below the header, the top summaries and (for a scrolling row) the frozen rows, above a pinned footer.
    /// False when none of it is on screen.
    /// </summary>
    public static bool TryGetBand(int row, ScrollState scroll, KumikoStyle style, KumikoSource source,
        out KumikoRect rowRect, out KumikoRect visible)
    {
        var rows = source.RowLayout;
        float topSummaries = source.TopSummaryCount * style.SummaryRowHeight;
        float dataTop = style.HeaderHeight + topSummaries;
        float groupPanel = source.IsGroupingActive ? style.GroupPanelHeight : 0f;
        float bottom = scroll.ViewportHeight - groupPanel -
                       (style.PinBottomSummaryRows ? source.BottomSummaryCount * style.SummaryRowHeight : 0f);
        float top = row < source.EffectiveFrozenRowCount ? dataTop : dataTop + rows.GetFrozenHeight(style);

        float left = BandLeft(style), right = BandRight(scroll, style);
        float y = rows.GetRowY(row, dataTop, scroll.OffsetY, style);
        rowRect = new KumikoRect(left, y, Math.Max(0, right - left), rows.GetRowHeight(row, style));
        float visibleTop = Math.Max(y, top), visibleBottom = Math.Min(rowRect.Bottom, bottom);
        visible = new KumikoRect(left, visibleTop, rowRect.Width, Math.Max(0, visibleBottom - visibleTop));
        return visible.Height > 0 && visible.Width > 0;
    }

    /// <summary>
    /// The rectangle of item <paramref name="index"/> of <paramref name="count"/> in the space the row at
    /// <paramref name="offset"/> uncovers in <paramref name="rowRect"/>: the items share it equally, the outermost
    /// against the edge; past the full-swipe point the outermost takes all of it. Empty for an item with no room.
    /// </summary>
    public KumikoRect ItemRect(int index, int count, KumikoRect rowRect, float offset)
    {
        float revealed = MathF.Abs(offset);
        if (count <= 0 || index < 0 || index >= count || revealed <= 0) return default;
        float width;
        float start; // distance of the item's outer side from the edge
        if (FullSwipeArmed)
        {
            if (index != 0) return default;
            width = revealed;
            start = 0;
        }
        else
        {
            width = revealed / count;
            start = index * width;
        }

        float x = offset > 0 ? rowRect.X + start : rowRect.Right - start - width;
        return new KumikoRect(x, rowRect.Y, width, rowRect.Height);
    }

    /// <summary>
    /// The item of the open row under rows-space point (<paramref name="x"/>, <paramref name="y"/>), or -1. Only a row
    /// settled <see cref="SwipePhase.Open"/> (and still current) has items to hit.
    /// </summary>
    public int ItemAt(float x, float y, ScrollState scroll, KumikoStyle style, KumikoSource source)
    {
        if (Phase != SwipePhase.Open || !IsCurrent(source)) return -1;
        if (!TryGetBand(Row, scroll, style, source, out var rowRect, out var visible) || !visible.Contains(x, y)) return -1;
        int count = Items.Count;
        for (int i = 0; i < count; i++)
        {
            if (ItemRect(i, count, rowRect, Offset).Contains(x, y)) return i;
        }
        return -1;
    }
}

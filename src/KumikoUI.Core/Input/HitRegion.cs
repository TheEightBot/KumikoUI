using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Input;

/// <summary>
/// Identifies what region of the grid a point falls in.
/// </summary>
public enum HitRegion
{
    /// <summary>No part of the grid (outside all content).</summary>
    None,
    /// <summary>A data cell.</summary>
    Cell,
    /// <summary>A column header cell.</summary>
    HeaderCell,
    /// <summary>The draggable resize grip on a column header edge.</summary>
    HeaderResizeGrip,
    /// <summary>The filter icon in a column header.</summary>
    HeaderFilterIcon,
    /// <summary>The drag handle used to reorder a row.</summary>
    RowDragHandle,
    /// <summary>Empty space below the last row or right of the last column.</summary>
    EmptyArea,
    /// <summary>A group header row.</summary>
    GroupHeaderRow,
    /// <summary>The expand/collapse chevron of a group header row.</summary>
    GroupChevron,
    /// <summary>A column chip in the group panel.</summary>
    GroupPanelChip,
    /// <summary>The remove button on a group panel chip.</summary>
    GroupPanelChipRemove,
    /// <summary>A group summary row.</summary>
    GroupSummaryRow,
    /// <summary>A table summary row.</summary>
    TableSummaryRow,
    /// <summary>The thumb of the vertical scroll bar (a mouse or pen only; a finger pans through the bar).</summary>
    VerticalScrollBarThumb,
    /// <summary>The track of the vertical scroll bar, above or below its thumb (a mouse or pen only).</summary>
    VerticalScrollBarTrack,
    /// <summary>The thumb of the horizontal scroll bar (a mouse or pen only; a finger pans through the bar).</summary>
    HorizontalScrollBarThumb,
    /// <summary>The track of the horizontal scroll bar, left or right of its thumb (a mouse or pen only).</summary>
    HorizontalScrollBarTrack,
    /// <summary>The column menu button in a column header (see <see cref="KumikoInputController.ShowColumnMenuButton"/>).</summary>
    HeaderMenuButton,
    /// <summary>
    /// The load-more row after the last row (#318): loading, "Load more" or a failed load's retry. It is not a row of
    /// the view, so the hit's row index is -1.
    /// </summary>
    LoadMoreRow,
    /// <summary>
    /// An item revealed beside a row swiped open (#329, see <see cref="KumikoInputController.AllowSwiping"/>); the hit's
    /// row index is the swiped row.
    /// </summary>
    SwipeItem
}

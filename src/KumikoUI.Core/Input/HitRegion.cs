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
    TableSummaryRow
}

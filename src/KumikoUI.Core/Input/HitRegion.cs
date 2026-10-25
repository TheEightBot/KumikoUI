using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Input;

/// <summary>
/// Identifies what region of the grid a point falls in.
/// </summary>
public enum HitRegion
{
    None,
    Cell,
    HeaderCell,
    HeaderResizeGrip,
    HeaderFilterIcon,
    RowHeader,
    RowDragHandle,
    FrozenColumn,
    EmptyArea,
    GroupHeaderRow,
    GroupChevron,
    GroupPanelChip,
    GroupPanelChipRemove,
    GroupSummaryRow,
    TableSummaryRow
}

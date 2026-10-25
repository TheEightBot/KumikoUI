using KumikoUI.Core.Models;

namespace KumikoUI.Core.Input;

/// <summary>
/// Result of a hit test — identifies the grid region, row/column, and exact position.
/// </summary>
public class KumikoHitTestResult
{
    public HitRegion Region { get; init; }
    public int RowIndex { get; init; } = -1;
    public int ColumnIndex { get; init; } = -1;
    public KumikoColumn? Column { get; init; }
    public float ContentX { get; init; }
    public float ContentY { get; init; }

    /// <summary>Index into GroupDescriptions for GroupPanelChip/GroupPanelChipRemove hits.</summary>
    public int GroupDescriptionIndex { get; init; } = -1;

    public static KumikoHitTestResult Empty => new() { Region = HitRegion.None };
}

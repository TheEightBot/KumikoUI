using KumikoUI.Core.Models;

namespace KumikoUI.Core.Input;

/// <summary>
/// Result of a hit test — identifies the grid region, row/column, and exact position.
/// </summary>
/// <remarks>
/// A value, so a hit test allocates nothing. Use <see cref="Empty"/> (or <c>new KumikoHitTestResult()</c>) for
/// "nothing hit": <c>default</c> has row and column index 0 rather than -1.
/// </remarks>
public readonly record struct KumikoHitTestResult
{
    /// <summary>Creates a result that hit nothing: <see cref="HitRegion.None"/> with every index -1.</summary>
    public KumikoHitTestResult() { }

    /// <summary>Region of the grid that was hit.</summary>
    public HitRegion Region { get; init; }
    /// <summary>Zero-based visible row index, or -1 when the hit is not on a row.</summary>
    public int RowIndex { get; init; } = -1;
    /// <summary>Zero-based column index, or -1 when the hit is not on a column.</summary>
    public int ColumnIndex { get; init; } = -1;
    /// <summary>Column under the hit point, or null when none.</summary>
    public KumikoColumn? Column { get; init; }
    /// <summary>X coordinate within the scrollable content, accounting for horizontal scroll.</summary>
    public float ContentX { get; init; }
    /// <summary>Y coordinate within the scrollable content, accounting for vertical scroll.</summary>
    public float ContentY { get; init; }

    /// <summary>Index into GroupDescriptions for GroupPanelChip/GroupPanelChipRemove hits.</summary>
    public int GroupDescriptionIndex { get; init; } = -1;

    /// <summary>A result that hit nothing (<see cref="HitRegion.None"/>).</summary>
    public static KumikoHitTestResult Empty => new() { Region = HitRegion.None };
}

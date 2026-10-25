namespace KumikoUI.Core.Models;

/// <summary>
/// A single row in the flat (visible) view of the grid.
/// When grouping is active, the flat view alternates between group headers and data rows.
/// </summary>
internal class FlatViewRow
{
    public FlatRowKind Kind { get; init; }

    /// <summary>Index into source items (valid only for Data rows).</summary>
    public int SourceIndex { get; init; } = -1;

    /// <summary>Group header info (valid only for GroupHeader rows).</summary>
    public GroupHeaderInfo? GroupInfo { get; init; }

    /// <summary>Group path this summary belongs to (valid only for GroupSummary rows).</summary>
    public string? GroupPath { get; init; }

    /// <summary>Index of the summary row within the group's summary list (valid only for GroupSummary rows).</summary>
    public int GroupSummaryIndex { get; init; }
}

namespace KumikoUI.Core.Models;

/// <summary>
/// A single row in the flat (visible) view of the grid.
/// When grouping is active, the flat view alternates between group headers and data rows.
/// A struct, so a grouped view of n data rows does not allocate n objects on every rebuild.
/// </summary>
internal readonly struct FlatViewRow
{
    private FlatViewRow(FlatRowKind kind, int sourceIndex, GroupHeaderInfo? groupInfo, string? groupPath, int groupSummaryIndex)
    {
        Kind = kind;
        SourceIndex = sourceIndex;
        GroupInfo = groupInfo;
        GroupPath = groupPath;
        GroupSummaryIndex = groupSummaryIndex;
    }

    /// <summary>A data row showing the item at <paramref name="sourceIndex"/> in the items source.</summary>
    public static FlatViewRow Data(int sourceIndex) => new(FlatRowKind.Data, sourceIndex, null, null, 0);

    /// <summary>A group header row.</summary>
    public static FlatViewRow Header(GroupHeaderInfo info) => new(FlatRowKind.GroupHeader, -1, info, null, 0);

    /// <summary>The <paramref name="summaryIndex"/>-th group summary row of the group at <paramref name="groupPath"/>.</summary>
    public static FlatViewRow Summary(string groupPath, int summaryIndex) =>
        new(FlatRowKind.GroupSummary, -1, null, groupPath, summaryIndex);

    public FlatRowKind Kind { get; }

    /// <summary>Index into source items (valid only for Data rows).</summary>
    public int SourceIndex { get; }

    /// <summary>Group header info (valid only for GroupHeader rows).</summary>
    public GroupHeaderInfo? GroupInfo { get; }

    /// <summary>Group path this summary belongs to (valid only for GroupSummary rows).</summary>
    public string? GroupPath { get; }

    /// <summary>Index of the summary row within the group's summary list (valid only for GroupSummary rows).</summary>
    public int GroupSummaryIndex { get; }
}

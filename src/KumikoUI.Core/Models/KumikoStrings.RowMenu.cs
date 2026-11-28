namespace KumikoUI.Core.Models;

/// <summary>The texts of the row menu (#331).</summary>
public sealed partial record KumikoStrings
{
    /// <summary>The row menu's copy item. Default: "Copy".</summary>
    public string RowMenuCopy { get; init; } = "Copy";

    /// <summary>The row menu's copy item that adds a row of column headers. Default: "Copy with headers".</summary>
    public string RowMenuCopyWithHeaders { get; init; } = "Copy with headers";

    /// <summary>The row menu's item that edits the cell. Default: "Edit".</summary>
    public string RowMenuEdit { get; init; } = "Edit";

    /// <summary>The row menu's delete item for one row. Default: "Delete".</summary>
    public string RowMenuDelete { get; init; } = "Delete";

    /// <summary>The row menu's delete item for several rows. <c>{0}</c> is the number of rows. Default: "Delete {0} rows".</summary>
    public string RowMenuDeleteRows { get; init; } = "Delete {0} rows";

    /// <summary>The group header row menu's item that expands every group. Default: "Expand all groups".</summary>
    public string RowMenuExpandAllGroups { get; init; } = "Expand all groups";

    /// <summary>The group header row menu's item that collapses every group. Default: "Collapse all groups".</summary>
    public string RowMenuCollapseAllGroups { get; init; } = "Collapse all groups";

    /// <summary>The row menu's accessible name, announced when it opens. Default: "Row menu".</summary>
    public string RowMenuName { get; init; } = "Row menu";

    /// <summary>The announcement after several rows were copied from the row menu. <c>{0}</c> is the number of rows. Default: "{0} rows copied".</summary>
    public string AnnounceRowsCopied { get; init; } = "{0} rows copied";

    /// <summary>The announcement after one row was copied from the row menu. Default: "Row copied".</summary>
    public string AnnounceRowCopied { get; init; } = "Row copied";
}

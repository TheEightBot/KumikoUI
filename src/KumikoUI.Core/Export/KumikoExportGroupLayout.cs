namespace KumikoUI.Core.Export;

/// <summary>How an export lays out grouped rows.</summary>
public enum KumikoExportGroupLayout
{
    /// <summary>Mirrors the grid: a group header row, the group's rows, then its summary rows (the default).</summary>
    Rows,

    /// <summary>Data-friendly: no group rows; each data row starts with one column per grouping level holding the group's key text.</summary>
    Columns,

    /// <summary>Ignores grouping: rows follow the grid's sort order.</summary>
    None,
}

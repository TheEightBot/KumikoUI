namespace KumikoUI.Core.Export;

/// <summary>The kind of a row an export writes.</summary>
public enum KumikoExportRowKind
{
    /// <summary>The row of column headers.</summary>
    Header,

    /// <summary>A data row: one item of the source.</summary>
    Data,

    /// <summary>A group header row.</summary>
    GroupHeader,

    /// <summary>A group summary row.</summary>
    GroupSummary,

    /// <summary>A table summary row, at the top or the bottom of the table.</summary>
    TableSummary,
}

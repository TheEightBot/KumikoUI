namespace KumikoUI.Core.Export;

/// <summary>Which rows of a grid an export writes.</summary>
public enum KumikoExportRows
{
    /// <summary>Every row that passes the current filters, sorted and grouped as in the grid, including the rows of collapsed groups and their summaries (the default).</summary>
    Filtered,

    /// <summary>Exactly the rows in the grid's current flat view: a collapsed group contributes only its header.</summary>
    Visible,

    /// <summary>Only the rows named by <see cref="KumikoExportOptions.Selection"/>, in view order.</summary>
    Selected,

    /// <summary>Every item of the source, ignoring filters, sorting and grouping (source order, no summaries).</summary>
    All,
}

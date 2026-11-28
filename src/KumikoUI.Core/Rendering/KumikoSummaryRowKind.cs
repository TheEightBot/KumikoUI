namespace KumikoUI.Core.Rendering;

/// <summary>The kind of summary row a <see cref="SummaryCellRenderContext"/> cell belongs to.</summary>
public enum KumikoSummaryRowKind
{
    /// <summary>A table-level summary row, at the top or the bottom of the grid.</summary>
    Table = 0,

    /// <summary>A group summary row, shown after the data rows of a group.</summary>
    Group = 1,
}

namespace KumikoUI.Core.Models;

/// <summary>One grouping level of a <see cref="KumikoLayoutState"/> (a <see cref="GroupDescription"/>).</summary>
public sealed class KumikoGroupLayoutState
{
    /// <summary>The <see cref="GroupDescription.PropertyName"/> the rows are grouped by.</summary>
    public string PropertyName { get; set; } = string.Empty;

    /// <summary>The <see cref="GroupDescription.Header"/>, or null for the column header.</summary>
    public string? Header { get; set; }

    /// <summary>The <see cref="GroupDescription.GroupSortDirection"/>. Default: ascending.</summary>
    public KumikoSortDirection SortDirection { get; set; } = KumikoSortDirection.Ascending;
}

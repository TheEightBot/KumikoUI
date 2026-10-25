namespace KumikoUI.Core.Models;

/// <summary>
/// Describes a caption summary that appears inline within a group header row.
/// Useful for showing aggregates (e.g., "Sum: $1,234") right in the group header.
/// </summary>
public class CaptionSummaryDescription
{
    /// <summary>
    /// Column-level aggregates to display in the group header.
    /// </summary>
    public List<SummaryColumnDescription> Columns { get; set; } = new();

    /// <summary>
    /// Optional format template for the entire caption summary text.
    /// Use {0}, {1}, etc. to reference computed summary values by index.
    /// If null, individual column summaries are concatenated with " | ".
    /// </summary>
    public string? FormatTemplate { get; set; }
}

namespace KumikoUI.Core.Models;

/// <summary>
/// Holds the computed summary values for a single summary row.
/// </summary>
public class ComputedSummaryRow
{
    /// <summary>The summary row definition this was computed from.</summary>
    public string Name { get; init; } = "";

    /// <summary>Optional title for the row.</summary>
    public string? Title { get; init; }

    /// <summary>
    /// Computed values keyed by column property name.
    /// Value is the formatted display string for each column.
    /// </summary>
    public Dictionary<string, string> Values { get; init; } = new();

    /// <summary>
    /// Raw computed values keyed by column property name (before formatting).
    /// </summary>
    public Dictionary<string, object?> RawValues { get; init; } = new();
}

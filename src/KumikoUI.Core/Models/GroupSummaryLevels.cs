namespace KumikoUI.Core.Models;

/// <summary>
/// The grouping levels at which a <see cref="GroupSummaryRow"/> appears.
/// </summary>
public enum GroupSummaryLevels
{
    /// <summary>Only after the innermost groups, the ones that hold the data rows.</summary>
    Innermost,

    /// <summary>After the groups of every grouping level, each summarizing all the rows beneath it.</summary>
    All
}

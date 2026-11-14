namespace KumikoUI.Core.Models;

/// <summary>
/// Describes a group-level summary row displayed after each group's data.
/// </summary>
public sealed class GroupSummaryRow
{
    /// <summary>Unique name for identification.</summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Optional title displayed in the first column area (e.g., "Group Total").
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Column-level aggregates contained in this summary row.
    /// </summary>
    public List<SummaryColumnDescription> Columns { get; set; } = new();

    /// <summary>
    /// The grouping levels this row appears at. <see cref="GroupSummaryLevels.Innermost"/> (the default)
    /// shows it after the innermost groups only; <see cref="GroupSummaryLevels.All"/> shows it after
    /// the groups of every level.
    /// </summary>
    public GroupSummaryLevels Levels { get; set; } = GroupSummaryLevels.Innermost;

    /// <summary>Creates an empty group summary row.</summary>
    public GroupSummaryRow() { }

    /// <summary>Creates a group summary row with a name.</summary>
    /// <param name="name">Unique name for identification.</param>
    public GroupSummaryRow(string name)
    {
        Name = name;
    }
}

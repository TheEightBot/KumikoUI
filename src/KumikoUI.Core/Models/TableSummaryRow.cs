namespace KumikoUI.Core.Models;

/// <summary>
/// Describes a table-level summary row that spans the full grid width.
/// A single summary row can contain multiple column aggregates.
/// </summary>
public class TableSummaryRow
{
    /// <summary>Unique name for identification.</summary>
    public string Name { get; set; } = "";

    /// <summary>Where the summary row appears (top or bottom of grid).</summary>
    public SummaryPosition Position { get; set; } = SummaryPosition.Bottom;

    /// <summary>
    /// Optional title displayed in the first column area (e.g., "Totals").
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Column-level aggregates contained in this summary row.
    /// </summary>
    public List<SummaryColumnDescription> Columns { get; set; } = new();

    /// <summary>Creates an empty summary row at the bottom of the grid.</summary>
    public TableSummaryRow() { }

    /// <summary>Creates a summary row with a name and position.</summary>
    /// <param name="name">Unique name for identification.</param>
    /// <param name="position">Whether the row appears at the top or bottom of the grid.</param>
    public TableSummaryRow(string name, SummaryPosition position = SummaryPosition.Bottom)
    {
        Name = name;
        Position = position;
    }
}

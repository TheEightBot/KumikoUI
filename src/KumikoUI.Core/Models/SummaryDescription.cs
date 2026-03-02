namespace KumikoUI.Core.Models;

/// <summary>
/// Built-in aggregate function types for summary calculations.
/// </summary>
public enum SummaryType
{
    /// <summary>Sum of numeric values.</summary>
    Sum,
    /// <summary>Average of numeric values.</summary>
    Average,
    /// <summary>Count of items.</summary>
    Count,
    /// <summary>Minimum value.</summary>
    Min,
    /// <summary>Maximum value.</summary>
    Max,
    /// <summary>Custom aggregate using a user-supplied function.</summary>
    Custom
}

/// <summary>
/// Defines where a summary row appears.
/// </summary>
public enum SummaryPosition
{
    /// <summary>Summary row at the top of the grid (above data rows, below header).</summary>
    Top,
    /// <summary>Summary row at the bottom of the grid (below all data rows).</summary>
    Bottom
}

/// <summary>
/// Describes a single summary (aggregate) for a specific column.
/// </summary>
public class SummaryColumnDescription
{
    /// <summary>The column property name to aggregate.</summary>
    public string PropertyName { get; set; } = "";

    /// <summary>Built-in aggregate type.</summary>
    public SummaryType SummaryType { get; set; } = SummaryType.Sum;

    /// <summary>
    /// Optional display format string (e.g., "C2", "N0", "{0:N2} total").
    /// If the format contains {0}, it's used with String.Format; otherwise as IFormattable.
    /// </summary>
    public string? Format { get; set; }

    /// <summary>
    /// Optional label prefix displayed before the value (e.g., "Sum: ", "Avg: ").
    /// If null, a default label based on SummaryType is generated.
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// Custom aggregate function. Receives the list of raw cell values for the column
    /// and returns the aggregate result. Only used when SummaryType == Custom.
    /// </summary>
    public Func<IReadOnlyList<object?>, object?>? CustomAggregate { get; set; }

    public SummaryColumnDescription() { }

    public SummaryColumnDescription(string propertyName, SummaryType summaryType, string? format = null)
    {
        PropertyName = propertyName;
        SummaryType = summaryType;
        Format = format;
    }
}

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

    public TableSummaryRow() { }

    public TableSummaryRow(string name, SummaryPosition position = SummaryPosition.Bottom)
    {
        Name = name;
        Position = position;
    }
}

/// <summary>
/// Describes a group-level summary row displayed after each group's data.
/// </summary>
public class GroupSummaryRow
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

    public GroupSummaryRow() { }

    public GroupSummaryRow(string name)
    {
        Name = name;
    }
}

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

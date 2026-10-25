namespace KumikoUI.Core.Models;

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

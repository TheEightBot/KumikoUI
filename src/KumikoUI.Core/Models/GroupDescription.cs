namespace KumikoUI.Core.Models;

/// <summary>
/// Describes how to group rows by a column value.
/// Multiple GroupDescriptions create multi-level (nested) grouping.
/// </summary>
public class GroupDescription
{
    /// <summary>The column property name to group by.</summary>
    public string PropertyName { get; set; } = "";

    /// <summary>Optional display header override (defaults to column header).</summary>
    public string? Header { get; set; }

    /// <summary>
    /// Optional custom key selector. Given a raw cell value, returns the grouping key.
    /// Useful for bucketing (e.g., date ranges, numeric bands).
    /// If null, the raw cell value is used as the key.
    /// </summary>
    public Func<object?, object?>? KeySelector { get; set; }

    /// <summary>
    /// Optional custom display text formatter. Given the group key, returns display text.
    /// If null, ToString() is used.
    /// </summary>
    public Func<object?, string>? DisplayTextSelector { get; set; }

    /// <summary>
    /// Sort direction for groups. Default is ascending.
    /// </summary>
    public KumikoSortDirection GroupSortDirection { get; set; } = KumikoSortDirection.Ascending;

    public GroupDescription() { }

    public GroupDescription(string propertyName)
    {
        PropertyName = propertyName;
    }

    public GroupDescription(string propertyName, string header)
    {
        PropertyName = propertyName;
        Header = header;
    }
}

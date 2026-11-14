namespace KumikoUI.Core.Models;

/// <summary>
/// Describes how to group rows by a column value.
/// Multiple GroupDescriptions create multi-level (nested) grouping.
/// </summary>
public sealed class GroupDescription
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

    /// <summary>Creates an empty description; set <see cref="PropertyName"/> before use.</summary>
    public GroupDescription() { }

    /// <summary>Creates a description that groups by a column property.</summary>
    /// <param name="propertyName">The column property name to group by.</param>
    public GroupDescription(string propertyName)
    {
        PropertyName = propertyName;
    }

    /// <summary>Creates a description that groups by a column property with a custom header.</summary>
    /// <param name="propertyName">The column property name to group by.</param>
    /// <param name="header">Display header override for the group.</param>
    public GroupDescription(string propertyName, string header)
    {
        PropertyName = propertyName;
        Header = header;
    }
}

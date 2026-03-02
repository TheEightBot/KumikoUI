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
    public SortDirection GroupSortDirection { get; set; } = SortDirection.Ascending;

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

/// <summary>
/// Represents a group header entry in the flat view.
/// Exposed by DataGridSource for rendering and interaction.
/// </summary>
public class GroupHeaderInfo
{
    /// <summary>Nesting depth (0-based). Level 0 is the outermost group.</summary>
    public int Level { get; init; }

    /// <summary>The raw group key value.</summary>
    public object? Key { get; init; }

    /// <summary>Display text for the group (e.g., "Engineering").</summary>
    public string DisplayText { get; init; } = "";

    /// <summary>The column property name this group is based on.</summary>
    public string PropertyName { get; init; } = "";

    /// <summary>Column header or override header text.</summary>
    public string HeaderText { get; init; } = "";

    /// <summary>Number of data items in this group (at all nested levels).</summary>
    public int ItemCount { get; init; }

    /// <summary>Whether the group is currently expanded.</summary>
    public bool IsExpanded { get; set; } = true;

    /// <summary>
    /// Unique path identifying this group in the hierarchy.
    /// Used for persisting expand/collapse state across rebuilds.
    /// Example: "Department=Engineering" or "Department=Engineering|City=Seattle"
    /// </summary>
    public string GroupPath { get; init; } = "";
}

/// <summary>
/// Internal flat view entry that can be either a data row or a group header.
/// </summary>
internal enum FlatRowKind
{
    Data,
    GroupHeader,
    GroupSummary
}

/// <summary>
/// A single row in the flat (visible) view of the grid.
/// When grouping is active, the flat view alternates between group headers and data rows.
/// </summary>
internal class FlatViewRow
{
    public FlatRowKind Kind { get; init; }

    /// <summary>Index into source items (valid only for Data rows).</summary>
    public int SourceIndex { get; init; } = -1;

    /// <summary>Group header info (valid only for GroupHeader rows).</summary>
    public GroupHeaderInfo? GroupInfo { get; init; }

    /// <summary>Group path this summary belongs to (valid only for GroupSummary rows).</summary>
    public string? GroupPath { get; init; }

    /// <summary>Index of the summary row within the group's summary list (valid only for GroupSummary rows).</summary>
    public int GroupSummaryIndex { get; init; }
}

// ── Events ───────────────────────────────────────────────────

/// <summary>
/// Event args raised before a group is expanded or collapsed.
/// Set Cancel = true to prevent the action.
/// </summary>
public class GroupExpandingEventArgs : EventArgs
{
    public GroupHeaderInfo Group { get; }
    public bool IsExpanding { get; }
    public bool Cancel { get; set; }

    public GroupExpandingEventArgs(GroupHeaderInfo group, bool isExpanding)
    {
        Group = group;
        IsExpanding = isExpanding;
    }
}

/// <summary>
/// Event args raised after a group is expanded or collapsed.
/// </summary>
public class GroupCollapsedEventArgs : EventArgs
{
    public GroupHeaderInfo Group { get; }
    public bool WasExpanded { get; }

    public GroupCollapsedEventArgs(GroupHeaderInfo group, bool wasExpanded)
    {
        Group = group;
        WasExpanded = wasExpanded;
    }
}

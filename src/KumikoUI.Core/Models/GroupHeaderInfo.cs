namespace KumikoUI.Core.Models;

/// <summary>
/// Represents a group header entry in the flat view.
/// Exposed by KumikoSource for rendering and interaction.
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

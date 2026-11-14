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
    /// Identifies the group in summaries and row keys. Expand/collapse state is kept by raw key path instead,
    /// so groups with different keys but the same display text get distinct paths (a numeric suffix is appended).
    /// Example: "Department=Engineering" or "Department=Engineering|City=Seattle"
    /// For the groups a <see cref="KumikoSource"/> builds, the path is composed on first read, so a rebuild does not
    /// allocate a string per group.
    /// </summary>
    public string GroupPath
    {
        get => _groupPath ??= ComposePath();
        init => _groupPath = value;
    }
    private string? _groupPath;

    /// <summary>The group's identity by raw keys; expand/collapse state is stored under it.</summary>
    internal GroupKeyPath? KeyPath { get; init; }

    /// <summary>The enclosing group (null at level 0), for the path and for in-place summary updates.</summary>
    internal GroupHeaderInfo? Parent { get; init; }

    /// <summary>0, or n (2 or more) when the group is the n-th sibling with this display text: the path gets "#n".</summary>
    internal int PathOrdinal { get; init; }

    /// <summary>The caption summary text of the group, or null when there is no caption summary.</summary>
    internal string? CaptionText { get; set; }

    /// <summary>The computed group summary rows (expanded groups that have any), or null.</summary>
    internal List<ComputedSummaryRow>? Summaries { get; set; }

    /// <summary>The definitions of <see cref="Summaries"/>, in the same order.</summary>
    internal SummaryRowSpec[]? SummaryRowSpecs { get; set; }

    /// <summary>The group's aggregate of each summarized property (by property id of <see cref="SummaryOwner"/>).</summary>
    internal PropertyAggregate?[]? SummaryAggregates { get; set; }

    /// <summary>The summary settings <see cref="SummaryAggregates"/> are indexed by.</summary>
    internal GroupSummarySettings? SummaryOwner { get; set; }

    private string ComposePath()
    {
        if (KeyPath == null) return ""; // not built by a source, and no path given
        string own = PathOrdinal > 0 ? $"{PropertyName}={DisplayText}#{PathOrdinal}" : $"{PropertyName}={DisplayText}";
        return Parent == null ? own : $"{Parent.GroupPath}|{own}";
    }

    // The header row's text runs, composed once and reused every frame while their inputs are the same.
    private string? _title;
    private string? _count;
    private string? _caption;
    private string? _captionSource;

    /// <summary>The first text run of the header row: "Header: Display". Composed on first use, then reused.</summary>
    internal string GetTitle() => _title ??= $"{HeaderText}: {DisplayText}";

    /// <summary>The item count run of the header row: " (count)". Composed on first use, then reused.</summary>
    internal string GetCountText() => _count ??= $" ({ItemCount})";

    /// <summary>
    /// The caption summary run of the header row ("  —  caption"), or null when there is no caption. Composed on
    /// the first call and when <paramref name="captionText"/> changes, then reused.
    /// </summary>
    internal string? GetCaptionLabel(string? captionText)
    {
        if (captionText == null) return null;
        if (_caption == null || !ReferenceEquals(captionText, _captionSource))
        {
            _caption = $"  —  {captionText}";
            _captionSource = captionText;
        }
        return _caption;
    }
}

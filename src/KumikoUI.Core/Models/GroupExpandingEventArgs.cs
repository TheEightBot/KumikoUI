namespace KumikoUI.Core.Models;

// ── Events ───────────────────────────────────────────────────

/// <summary>
/// Event args raised before a group is expanded or collapsed.
/// Set Cancel = true to prevent the action.
/// </summary>
public sealed class GroupExpandingEventArgs : EventArgs
{
    /// <summary>The group being expanded or collapsed.</summary>
    public GroupHeaderInfo Group { get; }
    /// <summary>True when the group is about to expand; false when it is about to collapse.</summary>
    public bool IsExpanding { get; }
    /// <summary>Set to true to leave the group in its current state.</summary>
    public bool Cancel { get; set; }

    /// <summary>Initializes the event data.</summary>
    /// <param name="group">The group being expanded or collapsed.</param>
    /// <param name="isExpanding">True to expand; false to collapse.</param>
    public GroupExpandingEventArgs(GroupHeaderInfo group, bool isExpanding)
    {
        Group = group;
        IsExpanding = isExpanding;
    }
}

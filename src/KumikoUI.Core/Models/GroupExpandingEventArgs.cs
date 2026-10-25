namespace KumikoUI.Core.Models;

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

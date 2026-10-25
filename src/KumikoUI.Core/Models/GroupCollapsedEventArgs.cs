namespace KumikoUI.Core.Models;

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

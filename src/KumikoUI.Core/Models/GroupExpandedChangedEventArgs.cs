namespace KumikoUI.Core.Models;

/// <summary>Event args for <see cref="KumikoSource.GroupExpandedChanged"/>.</summary>
public sealed class GroupExpandedChangedEventArgs : EventArgs
{
    /// <summary>Creates the args.</summary>
    public GroupExpandedChangedEventArgs(GroupHeaderInfo group, bool isExpanded)
    {
        Group = group;
        IsExpanded = isExpanded;
    }

    /// <summary>The group, as it appears in the rebuilt view.</summary>
    public GroupHeaderInfo Group { get; }

    /// <summary>Whether the group is now expanded (false: it was collapsed).</summary>
    public bool IsExpanded { get; }
}

namespace KumikoUI.Core.Components;

/// <summary>
/// The <see cref="DrawnMenuItem.Key"/> values of the items the grid puts in a row menu, so a <c>RowMenuOpening</c>
/// handler can find, remove or move them whatever language their text is in.
/// </summary>
/// <remarks>
/// On a data row the default menu holds, in this order and as far as the grid allows: copy and copy with headers; edit
/// and delete; the swipe items (<see cref="SwipeItemKeyPrefix"/>); the app's own items. A group header row holds expand
/// all groups and collapse all groups, then the app's own items. Separators sit between the groups and have an empty
/// key.
/// </remarks>
public static class KumikoRowMenuItems
{
    /// <summary>Copies the selected rows to the clipboard, as Ctrl/Cmd+C does when <c>CopyIncludesHeaders</c> is off.</summary>
    public const string Copy = "Copy";

    /// <summary>Copies the selected rows to the clipboard with a first row of the column headers.</summary>
    public const string CopyWithHeaders = "CopyWithHeaders";

    /// <summary>Opens the editor of the cell the menu was asked on.</summary>
    public const string Edit = "Edit";

    /// <summary>Deletes the selected rows, through the same flow as the Delete key (<c>RowsDeleting</c> can cancel it).</summary>
    public const string Delete = "Delete";

    /// <summary>Expands every group (a group header row's menu).</summary>
    public const string ExpandAllGroups = "ExpandAllGroups";

    /// <summary>Collapses every group (a group header row's menu).</summary>
    public const string CollapseAllGroups = "CollapseAllGroups";

    /// <summary>
    /// The start of the key of an item that stands for a swipe item: <c>Swipe:Leading:0</c> for the first of the leading
    /// swipe items, <c>Swipe:Trailing:1</c> for the second of the trailing ones.
    /// </summary>
    public const string SwipeItemKeyPrefix = "Swipe:";
}

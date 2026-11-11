namespace KumikoUI.Core.Input;

/// <summary>
/// The mouse cursor the grid asks its host to show (<see cref="KumikoInputController.Cursor"/>).
/// Hosts map it to the platform cursor; touch-only platforms ignore it.
/// </summary>
public enum KumikoCursor
{
    /// <summary>The platform's default arrow.</summary>
    Default,

    /// <summary>A left-right resize cursor: over a column resize grip, and for the whole of a resize drag.</summary>
    ResizeHorizontal,

    /// <summary>
    /// A hand: over something a click acts on directly, such as a filter icon, a group chevron, a grouping
    /// chip, the row drag handle, an editable check box or an enabled action button.
    /// </summary>
    Hand,
}

namespace KumikoUI.Core.Input;

/// <summary>
/// What asked for a context menu (<see cref="KumikoContextRequestedEventArgs.Trigger"/>).
/// </summary>
public enum KumikoContextTrigger
{
    /// <summary>
    /// A click with the secondary (usually right) mouse button, raised when the button is released. It never
    /// sorts, scrolls, drags or edits.
    /// </summary>
    SecondaryButton,

    /// <summary>
    /// A long press with a finger or a pen (<see cref="InputAction.LongPress"/>), unless the long press starts a
    /// row drag (<see cref="Models.KumikoStyle.AllowRowDragDrop"/> or the drag handle) or edits the cell
    /// (<see cref="Editing.EditTrigger.LongPress"/>). The rest of the press is then ignored: lifting or moving the
    /// finger neither taps nor scrolls.
    /// </summary>
    LongPress,
}

namespace KumikoUI.Core.Input;

/// <summary>
/// Platform-independent input event types.
/// Platforms translate native events into these types.
/// </summary>
public enum InputAction
{
    /// <summary>A pointer or touch was pressed down.</summary>
    Pressed,
    /// <summary>A pointer or touch was released.</summary>
    Released,
    /// <summary>
    /// A pointer moved while down. A move that arrives with no press in progress is handled as
    /// <see cref="Hover"/>.
    /// </summary>
    Moved,
    /// <summary>The input gesture was cancelled.</summary>
    Cancelled,
    /// <summary>A scroll/wheel event.</summary>
    Scroll,
    /// <summary>A double-tap or double-click.</summary>
    DoubleTap,
    /// <summary>A long press / right-click equivalent.</summary>
    LongPress,
    /// <summary>
    /// A mouse or pen moved over the grid with no button pressed. It never scrolls, drags or selects: it
    /// updates <see cref="KumikoInputController.Cursor"/> and the hover feedback of popups and editors.
    /// </summary>
    Hover,
    /// <summary>
    /// The pointer left the grid. The cursor returns to <see cref="KumikoCursor.Default"/> unless a column
    /// resize drag is in progress; a drag in progress is not canceled.
    /// </summary>
    Exited
}

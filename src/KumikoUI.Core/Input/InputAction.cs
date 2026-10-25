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
    /// <summary>A pointer moved while down.</summary>
    Moved,
    /// <summary>The input gesture was cancelled.</summary>
    Cancelled,
    /// <summary>A scroll/wheel event.</summary>
    Scroll,
    /// <summary>A double-tap or double-click.</summary>
    DoubleTap,
    /// <summary>A long press / right-click equivalent.</summary>
    LongPress
}

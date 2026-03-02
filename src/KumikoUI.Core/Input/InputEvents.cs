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

/// <summary>
/// Modifier keys held during an input event.
/// </summary>
[Flags]
public enum InputModifiers
{
    /// <summary>No modifier keys held.</summary>
    None = 0,
    /// <summary>Shift key held.</summary>
    Shift = 1,
    /// <summary>Ctrl key (Windows/Linux) or Cmd key (Mac).</summary>
    Control = 2,
    /// <summary>Alt/Option key held.</summary>
    Alt = 4,
    /// <summary>Windows key or Cmd key.</summary>
    Meta = 8
}

/// <summary>
/// Identifies the mouse button or pointer type.
/// </summary>
public enum PointerButton
{
    /// <summary>No button.</summary>
    None,
    /// <summary>Left click or touch.</summary>
    Primary,
    /// <summary>Right click.</summary>
    Secondary,
    /// <summary>Middle mouse button.</summary>
    Middle
}

/// <summary>
/// Platform-independent pointer/touch event.
/// </summary>
public class GridPointerEventArgs
{
    /// <summary>Position in grid viewport coordinates.</summary>
    public float X { get; init; }

    /// <summary>Vertical position in grid viewport coordinates.</summary>
    public float Y { get; init; }

    /// <summary>Action type.</summary>
    public InputAction Action { get; init; }

    /// <summary>Which button.</summary>
    public PointerButton Button { get; init; } = PointerButton.Primary;

    /// <summary>Active modifier keys.</summary>
    public InputModifiers Modifiers { get; init; }

    /// <summary>Scroll delta (for wheel events).</summary>
    public float ScrollDeltaX { get; init; }

    /// <summary>Vertical scroll delta (for wheel events).</summary>
    public float ScrollDeltaY { get; init; }

    /// <summary>Number of clicks (1 = single, 2 = double).</summary>
    public int ClickCount { get; init; } = 1;

    /// <summary>Timestamp of the event for velocity tracking.</summary>
    public long TimestampMs { get; init; }

    /// <summary>Set to true to indicate the event was handled.</summary>
    public bool Handled { get; set; }
}

/// <summary>
/// Platform-independent keyboard event.
/// </summary>
public class GridKeyEventArgs
{
    /// <summary>Key identifier.</summary>
    public GridKey Key { get; init; }

    /// <summary>Character input (for text entry).</summary>
    public char? Character { get; init; }

    /// <summary>Active modifier keys.</summary>
    public InputModifiers Modifiers { get; init; }

    /// <summary>Is this a key-down (true) or key-up (false) event?</summary>
    public bool IsKeyDown { get; init; } = true;

    /// <summary>Set to true to indicate the event was handled.</summary>
    public bool Handled { get; set; }

    /// <summary>Whether the Shift modifier is active.</summary>
    public bool HasShift => (Modifiers & InputModifiers.Shift) != 0;

    /// <summary>Whether the Control/Cmd modifier is active.</summary>
    public bool HasControl => (Modifiers & InputModifiers.Control) != 0;

    /// <summary>Whether the Alt/Option modifier is active.</summary>
    public bool HasAlt => (Modifiers & InputModifiers.Alt) != 0;
}

/// <summary>
/// Common keyboard keys for grid navigation and editing.
/// </summary>
public enum GridKey
{
    /// <summary>No key.</summary>
    None,
    // Navigation
    /// <summary>Up arrow key.</summary>
    Up,
    /// <summary>Down arrow key.</summary>
    Down,
    /// <summary>Left arrow key.</summary>
    Left,
    /// <summary>Right arrow key.</summary>
    Right,
    /// <summary>Home key.</summary>
    Home,
    /// <summary>End key.</summary>
    End,
    /// <summary>Page Up key.</summary>
    PageUp,
    /// <summary>Page Down key.</summary>
    PageDown,
    /// <summary>Tab key.</summary>
    Tab,
    // Actions
    /// <summary>Enter/Return key.</summary>
    Enter,
    /// <summary>Escape key.</summary>
    Escape,
    /// <summary>Space bar.</summary>
    Space,
    /// <summary>Delete key.</summary>
    Delete,
    /// <summary>Backspace key.</summary>
    Backspace,
    /// <summary>F2 function key (commonly starts editing).</summary>
    F2,
    // Letters/digits (for type-to-search, text entry)
    /// <summary>A key (select-all shortcut).</summary>
    A,
    /// <summary>C key (copy shortcut).</summary>
    C,
    /// <summary>V key (paste shortcut).</summary>
    V,
    /// <summary>X key (cut shortcut).</summary>
    X,
    /// <summary>Z key (undo shortcut).</summary>
    Z,
    // Generic character input
    /// <summary>Generic character input for text entry.</summary>
    Character
}

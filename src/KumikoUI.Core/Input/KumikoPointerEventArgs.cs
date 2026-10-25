namespace KumikoUI.Core.Input;

/// <summary>
/// Platform-independent pointer/touch event.
/// </summary>
public class KumikoPointerEventArgs
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

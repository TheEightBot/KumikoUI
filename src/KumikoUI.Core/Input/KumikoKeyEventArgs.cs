namespace KumikoUI.Core.Input;

/// <summary>
/// Platform-independent keyboard event.
/// </summary>
public class KumikoKeyEventArgs
{
    /// <summary>Key identifier.</summary>
    public KumikoKey Key { get; init; }

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

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

    /// <summary>
    /// Text input as a string, for input that is more than one UTF-16 code unit (a surrogate pair such as
    /// an emoji, a grapheme cluster, a dead-key sequence) or that the host receives as a string. Takes
    /// precedence over <see cref="Character"/> for text entry.
    /// </summary>
    public string? Text { get; init; }

    /// <summary>The text this event enters: <see cref="Text"/>, else <see cref="Character"/>; null for none.</summary>
    internal string? EnteredText =>
        !string.IsNullOrEmpty(Text) ? Text
        : Character is { } c && c != '\0' ? c.ToString()
        : null;

    /// <summary>Whether the platform's shortcut modifier is active (Control, or Command on Apple keyboards).</summary>
    public bool HasCommand => (Modifiers & (InputModifiers.Control | InputModifiers.Meta)) != 0;

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

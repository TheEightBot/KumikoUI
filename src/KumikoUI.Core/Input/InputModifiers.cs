namespace KumikoUI.Core.Input;

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

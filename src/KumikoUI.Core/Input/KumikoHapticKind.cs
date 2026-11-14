namespace KumikoUI.Core.Input;

/// <summary>The moment a finger or pen gesture asks for haptic feedback (<see cref="KumikoHapticEventArgs"/>).</summary>
public enum KumikoHapticKind
{
    /// <summary>A row or a column header was picked up and starts to follow the finger.</summary>
    DragStart,

    /// <summary>A dragged row or column header was dropped.</summary>
    Drop,

    /// <summary>A long press was recognized (it asks for a context menu, or edits a cell with the LongPress trigger).</summary>
    LongPress,
}

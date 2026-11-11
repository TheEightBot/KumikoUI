namespace KumikoUI.Core.Input;

/// <summary>
/// The kind of device behind a pointer event (<see cref="KumikoPointerEventArgs.DeviceType"/>). Some gestures depend
/// on it: a touch drag on a row scrolls the grid and a long press starts a row drag, while a mouse or pen drag on a
/// row starts the row drag directly.
/// </summary>
public enum KumikoPointerDeviceType
{
    /// <summary>A mouse or trackpad pointer. The default, for hosts that cannot tell.</summary>
    Mouse,

    /// <summary>A finger on a touch screen.</summary>
    Touch,

    /// <summary>A pen or stylus. Drags like a mouse.</summary>
    Pen,
}

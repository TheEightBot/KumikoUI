namespace KumikoUI.Core.Input;

/// <summary>
/// The unit of a wheel event's <see cref="KumikoPointerEventArgs.ScrollDeltaX"/> and
/// <see cref="KumikoPointerEventArgs.ScrollDeltaY"/> (<see cref="KumikoPointerEventArgs.ScrollUnit"/>).
/// </summary>
public enum KumikoScrollUnit
{
    /// <summary>
    /// Wheel notches (detents). One notch scrolls the grid <see cref="ScrollSettings.WheelScrollMultiplier"/>
    /// rows; a fraction of a notch (a high-resolution wheel, a Windows precision touchpad) scrolls that fraction.
    /// The default.
    /// </summary>
    Notches,

    /// <summary>
    /// Device-independent pixels, as precise devices report them (a trackpad on macOS, any wheel in the
    /// browser). The grid scrolls by exactly that distance, so the content follows the fingers.
    /// </summary>
    Pixels,
}

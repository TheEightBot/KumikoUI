namespace KumikoUI.Core.Input;

/// <summary>
/// Platform-independent pointer/touch event.
/// </summary>
public sealed class KumikoPointerEventArgs
{
    /// <summary>Position in grid viewport coordinates.</summary>
    public float X { get; init; }

    /// <summary>Vertical position in grid viewport coordinates.</summary>
    public float Y { get; init; }

    /// <summary>Action type.</summary>
    public InputAction Action { get; init; }

    /// <summary>
    /// Which button. The controller reads it from the <see cref="InputAction.Pressed"/> event, and the moves, long
    /// press and release of that press follow it, so a host that reports no button on a release is fine. Only the
    /// primary button (and a finger) selects, sorts, scrolls, drags and edits; a secondary-button click raises
    /// <see cref="KumikoInputController.ContextRequested"/> on its release, and a middle-button press does nothing.
    /// </summary>
    public PointerButton Button { get; init; } = PointerButton.Primary;

    /// <summary>Active modifier keys.</summary>
    public InputModifiers Modifiers { get; init; }

    /// <summary>
    /// Identifies the pointer (a finger, the mouse, a pen) that raised the event. While a press is in progress
    /// the controller follows only the pointer that pressed: the presses, moves and releases of a second finger
    /// are ignored, so it cannot restart or end a drag. Hosts that see one pointer at a time can leave it 0.
    /// </summary>
    public long PointerId { get; init; }

    /// <summary>
    /// The device that raised the event. With <see cref="Models.KumikoStyle.AllowRowDragDrop"/>, a touch drag on a
    /// row scrolls the grid and a touch long press starts the row drag, while a mouse or pen drag on a row starts it
    /// once it passes the drag threshold. Hosts that cannot tell leave the default, <see cref="KumikoPointerDeviceType.Mouse"/>.
    /// </summary>
    public KumikoPointerDeviceType DeviceType { get; init; }

    /// <summary>
    /// Horizontal wheel delta (for <see cref="InputAction.Scroll"/>), in <see cref="ScrollUnit"/>. Positive scrolls
    /// toward the first column (left), negative toward the last.
    /// </summary>
    public float ScrollDeltaX { get; init; }

    /// <summary>
    /// Vertical wheel delta (for <see cref="InputAction.Scroll"/>), in <see cref="ScrollUnit"/>. Positive scrolls
    /// toward the first row (up: a wheel rolled away from the user), negative toward the last. With Shift held and
    /// no <see cref="ScrollDeltaX"/>, the grid scrolls horizontally instead.
    /// </summary>
    public float ScrollDeltaY { get; init; }

    /// <summary>
    /// The unit of <see cref="ScrollDeltaX"/> and <see cref="ScrollDeltaY"/>: wheel notches (the default) or
    /// device-independent pixels from a precise device such as a trackpad.
    /// </summary>
    public KumikoScrollUnit ScrollUnit { get; init; }

    /// <summary>Number of clicks (1 = single, 2 = double).</summary>
    public int ClickCount { get; init; } = 1;

    /// <summary>Timestamp of the event for velocity tracking.</summary>
    public long TimestampMs { get; init; }

    /// <summary>Set to true to indicate the event was handled.</summary>
    public bool Handled { get; set; }

    /// <summary>
    /// <see cref="ScrollDeltaX"/> in device-independent pixels: as is for <see cref="KumikoScrollUnit.Pixels"/>,
    /// else multiplied by <paramref name="pixelsPerNotch"/>.
    /// </summary>
    /// <param name="pixelsPerNotch">How far one wheel notch scrolls the receiver, in pixels.</param>
    /// <returns>The horizontal distance to scroll; positive is toward the start.</returns>
    public float GetScrollPixelsX(float pixelsPerNotch) =>
        ScrollUnit == KumikoScrollUnit.Pixels ? ScrollDeltaX : ScrollDeltaX * pixelsPerNotch;

    /// <summary>
    /// <see cref="ScrollDeltaY"/> in device-independent pixels: as is for <see cref="KumikoScrollUnit.Pixels"/>,
    /// else multiplied by <paramref name="pixelsPerNotch"/>.
    /// </summary>
    /// <param name="pixelsPerNotch">How far one wheel notch scrolls the receiver, in pixels.</param>
    /// <returns>The vertical distance to scroll; positive is toward the start.</returns>
    public float GetScrollPixelsY(float pixelsPerNotch) =>
        ScrollUnit == KumikoScrollUnit.Pixels ? ScrollDeltaY : ScrollDeltaY * pixelsPerNotch;
}

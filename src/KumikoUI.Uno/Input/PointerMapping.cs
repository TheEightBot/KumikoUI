using KumikoUI.Core.Input;
using Microsoft.UI.Input;

namespace KumikoUI.Uno.Input;

/// <summary>
/// Pure translation of WinUI pointer data (wheel deltas, buttons) into KumikoUI.Core's pointer contract
/// (<see cref="KumikoPointerEventArgs"/>), kept free of framework elements so it is unit-tested in
/// <c>KumikoUI.Uno.Tests</c>.
/// </summary>
internal static class PointerMapping
{
    /// <summary>One wheel notch in WinUI's <c>MouseWheelDelta</c> units.</summary>
    public const float WheelNotch = 120f;

    /// <summary>
    /// Whether this head's <c>MouseWheelDelta</c> is in device-independent pixels rather than 120ths of a notch.
    /// Uno's macOS host reports points (a trackpad's precise deltas as they are, a mouse wheel's lines times 10,
    /// AppKit's line height) and the browser host CSS pixels (line-mode deltas times a line height). Win32, X11
    /// and the Windows App SDK report 120ths of a notch, precision touchpads and smooth scrolling included.
    /// </summary>
    public static bool WheelDeltaIsPixels { get; } = OperatingSystem.IsMacOS() || OperatingSystem.IsBrowser();

    /// <summary>
    /// Whether a positive horizontal <c>MouseWheelDelta</c> scrolls toward the left on this head. WinUI documents
    /// positive as "tilted right", which the Windows App SDK, Win32 and browser hosts follow; Uno's macOS host
    /// (AppKit's <c>scrollingDeltaX</c>) and X11 host report the opposite sign.
    /// </summary>
    public static bool HorizontalWheelPositiveIsLeft { get; } = OperatingSystem.IsMacOS() || OperatingSystem.IsLinux();

    /// <summary>
    /// Converts a WinUI wheel delta to Core's scroll deltas, where positive scrolls toward the start (up, left):
    /// pixels or notches per <paramref name="deltaIsPixels"/>, on the axis <paramref name="isHorizontal"/> names
    /// (never both), with the horizontal sign normalized per <paramref name="horizontalPositiveIsLeft"/>.
    /// </summary>
    /// <param name="mouseWheelDelta"><c>PointerPointProperties.MouseWheelDelta</c>.</param>
    /// <param name="isHorizontal"><c>PointerPointProperties.IsHorizontalMouseWheel</c>.</param>
    /// <param name="deltaIsPixels">See <see cref="WheelDeltaIsPixels"/>.</param>
    /// <param name="horizontalPositiveIsLeft">See <see cref="HorizontalWheelPositiveIsLeft"/>.</param>
    /// <returns>The deltas for <see cref="KumikoPointerEventArgs.ScrollDeltaX"/>/<see cref="KumikoPointerEventArgs.ScrollDeltaY"/> and their unit.</returns>
    public static (float DeltaX, float DeltaY, KumikoScrollUnit Unit) ToScrollDelta(
        int mouseWheelDelta, bool isHorizontal, bool deltaIsPixels, bool horizontalPositiveIsLeft)
    {
        var unit = deltaIsPixels ? KumikoScrollUnit.Pixels : KumikoScrollUnit.Notches;
        float amount = deltaIsPixels ? mouseWheelDelta : mouseWheelDelta / WheelNotch;
        if (!isHorizontal) return (0f, amount, unit);
        return (horizontalPositiveIsLeft ? amount : -amount, 0f, unit);
    }

    /// <summary>
    /// Core's device type for a WinUI pointer: a finger is <see cref="KumikoPointerDeviceType.Touch"/>, a pen
    /// <see cref="KumikoPointerDeviceType.Pen"/>, anything else (a mouse, a trackpad) <see cref="KumikoPointerDeviceType.Mouse"/>.
    /// </summary>
    /// <param name="deviceType"><c>Pointer.PointerDeviceType</c>.</param>
    /// <returns>The value for <see cref="KumikoPointerEventArgs.DeviceType"/>.</returns>
    public static KumikoPointerDeviceType ToDeviceType(PointerDeviceType deviceType) => deviceType switch
    {
        PointerDeviceType.Touch => KumikoPointerDeviceType.Touch,
        PointerDeviceType.Pen => KumikoPointerDeviceType.Pen,
        _ => KumikoPointerDeviceType.Mouse,
    };

    /// <summary>
    /// The button an event is about. The update kind names the button that changed, which is the only way to
    /// know it on a release (no button is pressed any more); otherwise the pressed buttons decide. Hover and
    /// exit carry no button; a contact with no named button (touch, pen) is <see cref="PointerButton.Primary"/>.
    /// </summary>
    /// <param name="action">The Core action the event maps to.</param>
    /// <param name="kind"><c>PointerPointProperties.PointerUpdateKind</c>.</param>
    /// <param name="isRightPressed"><c>PointerPointProperties.IsRightButtonPressed</c>.</param>
    /// <param name="isMiddlePressed"><c>PointerPointProperties.IsMiddleButtonPressed</c>.</param>
    /// <returns>The button for <see cref="KumikoPointerEventArgs.Button"/>.</returns>
    public static PointerButton ToPointerButton(
        InputAction action, PointerUpdateKind kind, bool isRightPressed, bool isMiddlePressed)
    {
        if (action is InputAction.Hover or InputAction.Exited) return PointerButton.None;
        return kind switch
        {
            PointerUpdateKind.LeftButtonPressed or PointerUpdateKind.LeftButtonReleased => PointerButton.Primary,
            PointerUpdateKind.RightButtonPressed or PointerUpdateKind.RightButtonReleased => PointerButton.Secondary,
            PointerUpdateKind.MiddleButtonPressed or PointerUpdateKind.MiddleButtonReleased => PointerButton.Middle,
            _ => isRightPressed ? PointerButton.Secondary
               : isMiddlePressed ? PointerButton.Middle
               : PointerButton.Primary,
        };
    }
}

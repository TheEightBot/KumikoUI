using KumikoUI.Core.Input;
using KumikoUI.Uno.Input;
using Microsoft.UI.Input;

namespace KumikoUI.Uno.Tests;

/// <summary>
/// The Uno host's pure pointer mapping (#60): WinUI wheel deltas become Core notches or pixels on one axis with
/// Core's sign convention (positive scrolls toward the start), and a release reports the button that went up.
/// </summary>
public class PointerMappingTests
{
    // ── ToScrollDelta ──

    [Theory]
    [InlineData(120, 1f)]    // one notch away from the user: scroll up
    [InlineData(-120, -1f)]  // toward the user: scroll down
    [InlineData(-360, -3f)]
    [InlineData(30, 0.25f)]  // a precision touchpad / smooth-scrolling fraction of a notch
    public void ToScrollDelta_NotchHeads_DivideBy120(int delta, float expected)
    {
        var (x, y, unit) = PointerMapping.ToScrollDelta(delta, isHorizontal: false, deltaIsPixels: false, horizontalPositiveIsLeft: false);

        Assert.Equal(0f, x);
        Assert.Equal(expected, y);
        Assert.Equal(KumikoScrollUnit.Notches, unit);
    }

    [Theory]
    [InlineData(10)]   // a macOS mouse-wheel line (10 points)
    [InlineData(-3)]   // a slow trackpad movement
    [InlineData(-100)] // a browser wheel notch
    public void ToScrollDelta_PixelHeads_PassThePixelsThrough(int delta)
    {
        var (x, y, unit) = PointerMapping.ToScrollDelta(delta, isHorizontal: false, deltaIsPixels: true, horizontalPositiveIsLeft: true);

        Assert.Equal(0f, x);
        Assert.Equal(delta, y);
        Assert.Equal(KumikoScrollUnit.Pixels, unit);
    }

    [Fact]
    public void ToScrollDelta_Horizontal_WinUISign_IsNegated()
    {
        // WinUI: positive is "tilted right", i.e. toward the last column; Core's positive is toward the first.
        var (x, y, _) = PointerMapping.ToScrollDelta(120, isHorizontal: true, deltaIsPixels: false, horizontalPositiveIsLeft: false);

        Assert.Equal(-1f, x);
        Assert.Equal(0f, y);
    }

    [Fact]
    public void ToScrollDelta_Horizontal_LeftPositiveHeads_KeepTheSign()
    {
        // macOS (scrollingDeltaX) and X11 report positive for a scroll toward the left.
        var (x, y, unit) = PointerMapping.ToScrollDelta(14, isHorizontal: true, deltaIsPixels: true, horizontalPositiveIsLeft: true);

        Assert.Equal(14f, x);
        Assert.Equal(0f, y);
        Assert.Equal(KumikoScrollUnit.Pixels, unit);
    }

    [Fact]
    public void HeadFlags_MatchThisPlatform()
    {
        Assert.Equal(OperatingSystem.IsMacOS() || OperatingSystem.IsBrowser(), PointerMapping.WheelDeltaIsPixels);
        Assert.Equal(OperatingSystem.IsMacOS() || OperatingSystem.IsLinux(), PointerMapping.HorizontalWheelPositiveIsLeft);
    }

    // ── ToPointerButton ──

    [Theory]
    [InlineData(PointerUpdateKind.LeftButtonReleased, PointerButton.Primary)]
    [InlineData(PointerUpdateKind.RightButtonReleased, PointerButton.Secondary)]
    [InlineData(PointerUpdateKind.MiddleButtonReleased, PointerButton.Middle)]
    public void ToPointerButton_Release_ComesFromTheUpdateKind(PointerUpdateKind kind, PointerButton expected)
    {
        // Nothing is pressed any more on a release
        Assert.Equal(expected, PointerMapping.ToPointerButton(InputAction.Released, kind, isRightPressed: false, isMiddlePressed: false));
    }

    [Theory]
    [InlineData(PointerUpdateKind.LeftButtonPressed, false, false, PointerButton.Primary)]
    [InlineData(PointerUpdateKind.RightButtonPressed, true, false, PointerButton.Secondary)]
    [InlineData(PointerUpdateKind.MiddleButtonPressed, false, true, PointerButton.Middle)]
    [InlineData(PointerUpdateKind.Other, true, false, PointerButton.Secondary)]
    [InlineData(PointerUpdateKind.Other, false, true, PointerButton.Middle)]
    public void ToPointerButton_Press_UsesTheUpdateKindThenThePressedButtons(
        PointerUpdateKind kind, bool right, bool middle, PointerButton expected)
    {
        Assert.Equal(expected, PointerMapping.ToPointerButton(InputAction.Pressed, kind, right, middle));
    }

    [Theory]
    [InlineData(InputAction.Released)] // a finger or pen lifting reports no named button
    [InlineData(InputAction.Moved)]
    public void ToPointerButton_ContactWithoutANamedButton_IsPrimary(InputAction action)
    {
        Assert.Equal(PointerButton.Primary, PointerMapping.ToPointerButton(action, PointerUpdateKind.Other, false, false));
    }

    [Theory]
    [InlineData(InputAction.Hover)]
    [InlineData(InputAction.Exited)]
    public void ToPointerButton_HoverAndExit_HaveNoButton(InputAction action)
    {
        Assert.Equal(PointerButton.None, PointerMapping.ToPointerButton(action, PointerUpdateKind.RightButtonPressed, true, false));
    }

    // ── IsUnnamedMouseButtonEvent (#286: an X11 wheel notch is a press with no named button) ──

    [Theory]
    [InlineData(InputAction.Pressed, KumikoPointerDeviceType.Mouse, PointerUpdateKind.Other, false, false, false, true)]
    [InlineData(InputAction.Released, KumikoPointerDeviceType.Mouse, PointerUpdateKind.Other, false, false, false, true)]
    [InlineData(InputAction.Pressed, KumikoPointerDeviceType.Mouse, PointerUpdateKind.LeftButtonPressed, true, false, false, false)]
    [InlineData(InputAction.Pressed, KumikoPointerDeviceType.Mouse, PointerUpdateKind.RightButtonPressed, false, true, false, false)]
    [InlineData(InputAction.Pressed, KumikoPointerDeviceType.Mouse, PointerUpdateKind.MiddleButtonPressed, false, false, true, false)]
    [InlineData(InputAction.Released, KumikoPointerDeviceType.Mouse, PointerUpdateKind.LeftButtonReleased, false, false, false, false)]
    [InlineData(InputAction.Released, KumikoPointerDeviceType.Mouse, PointerUpdateKind.RightButtonReleased, false, false, false, false)]
    [InlineData(InputAction.Released, KumikoPointerDeviceType.Mouse, PointerUpdateKind.MiddleButtonReleased, false, false, false, false)]
    [InlineData(InputAction.Pressed, KumikoPointerDeviceType.Mouse, PointerUpdateKind.Other, true, false, false, false)] // a button is down
    [InlineData(InputAction.Pressed, KumikoPointerDeviceType.Mouse, PointerUpdateKind.Other, false, true, false, false)]
    [InlineData(InputAction.Pressed, KumikoPointerDeviceType.Touch, PointerUpdateKind.Other, false, false, false, false)]
    [InlineData(InputAction.Released, KumikoPointerDeviceType.Touch, PointerUpdateKind.Other, false, false, false, false)]
    [InlineData(InputAction.Pressed, KumikoPointerDeviceType.Pen, PointerUpdateKind.Other, false, false, false, false)]
    [InlineData(InputAction.Released, KumikoPointerDeviceType.Pen, PointerUpdateKind.Other, false, false, false, false)]
    [InlineData(InputAction.Moved, KumikoPointerDeviceType.Mouse, PointerUpdateKind.Other, false, false, false, false)]
    [InlineData(InputAction.Hover, KumikoPointerDeviceType.Mouse, PointerUpdateKind.Other, false, false, false, false)]
    [InlineData(InputAction.Scroll, KumikoPointerDeviceType.Mouse, PointerUpdateKind.Other, false, false, false, false)]
    public void IsUnnamedMouseButtonEvent_OnlyAMousePressOrReleaseWithNoButton(
        InputAction action, KumikoPointerDeviceType device, PointerUpdateKind kind,
        bool left, bool right, bool middle, bool expected)
    {
        Assert.Equal(expected, PointerMapping.IsUnnamedMouseButtonEvent(action, device, kind, left, right, middle));
    }

    // ── ToDeviceType (#99: a touch drag scrolls, a touch long press drags a row) ──

    [Theory]
    [InlineData(PointerDeviceType.Touch, KumikoPointerDeviceType.Touch)]
    [InlineData(PointerDeviceType.Pen, KumikoPointerDeviceType.Pen)]
    [InlineData(PointerDeviceType.Mouse, KumikoPointerDeviceType.Mouse)]
    [InlineData(PointerDeviceType.Touchpad, KumikoPointerDeviceType.Mouse)]
    public void ToDeviceType_MapsEachDevice(PointerDeviceType device, KumikoPointerDeviceType expected)
    {
        Assert.Equal(expected, PointerMapping.ToDeviceType(device));
    }
}

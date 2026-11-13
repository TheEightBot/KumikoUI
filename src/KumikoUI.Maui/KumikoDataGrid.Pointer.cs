using KumikoUI.Core.Input;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
#if MACCATALYST || IOS
using CoreGraphics;
using UIKit;
#endif

namespace KumikoUI.Maui;

/// <summary>
/// Pointer plumbing that <c>SKCanvasView.Touch</c> does not provide (#60, #100): the touch-to-DIP scale before the
/// first paint, modifier keys and mouse buttons on pointer events, the secondary click, and wheel and trackpad
/// scrolling routed through <see cref="KumikoInputController.HandlePointer"/>.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>Mac Catalyst and iPad</b>: modifiers come from SkiaSharp's touch gesture recognizer, which UIKit
/// gives the flags of the event it is handling. Trackpad and mouse-wheel scrolling arrive through a
/// <c>UIPanGestureRecognizer</c> for indirect input and reach Core as pixel deltas. A secondary (right) click
/// reaches no touch handler; a <c>UITapGestureRecognizer</c> requiring the secondary button reports it to Core as
/// a press and release with <see cref="PointerButton.Secondary"/>.</item>
/// <item><b>Android</b>: modifiers and mouse buttons are the <c>MotionEvent.MetaState</c> and <c>ButtonState</c>
/// of the canvas's touches, read by a second touch listener that is moved ahead of SkiaSharp's, so each event
/// reports its own state. The wheel arrives as <c>ACTION_SCROLL</c> generic motion in notches.</item>
/// <item><b>Windows</b>: modifiers are the live keyboard state; SkiaSharp reports the right and middle buttons.
/// The wheel is read from WinUI's <c>PointerWheelChanged</c> on the grid's platform view (SkiaSharp's event has
/// no horizontal flag), as 120ths of a notch, and is marked handled only when the grid scrolled, so a page around
/// it scrolls on.</item>
/// </list>
/// </remarks>
public partial class KumikoDataGrid
{
    // Device pixels per DIP from the last paint: SKTouch locations are in pixels, and CanvasSize is empty until
    // the first paint.
    private float _touchScale;

    /// <summary>
    /// The factor that turns a touch location (device pixels) into grid coordinates (DIPs). False when it is not
    /// known yet (a touch before the first layout and paint): the event has no position the grid can use.
    /// </summary>
    private bool TryGetTouchScale(out float scale)
    {
        double width = _canvasView.Width;
        float pixels = CanvasSize.Width;
        scale = width > 0 && pixels > 0 ? (float)(pixels / width) : _touchScale;
        return scale > 0 && float.IsFinite(scale);
    }

    /// <summary>Remembers the scale a frame was painted at, for touches that arrive before the canvas reports a size.</summary>
    private void RememberTouchScale(float scale)
    {
        if (scale > 0 && float.IsFinite(scale)) _touchScale = scale;
    }

    /// <summary>Mirrors <see cref="VisualElement.IsEnabled"/> into Core: a disabled grid takes no input.</summary>
    private void SyncIsEnabled()
    {
        _inputController.IsEnabled = IsEnabled;
        if (!IsEnabled)
        {
            _pressedPointerId = null;
            CancelLongPressTimer();
            StopInertialScrollTimer();
        }
        _canvasView.InvalidateSurface();
    }

    /// <summary>Dispatches a wheel or trackpad scroll to Core and repaints; returns whether the grid scrolled.</summary>
    private bool DispatchScroll(float x, float y, float deltaX, float deltaY, KumikoScrollUnit unit, InputModifiers modifiers)
    {
        var e = new KumikoPointerEventArgs
        {
            X = x,
            Y = y,
            Action = InputAction.Scroll,
            Button = PointerButton.None,
            ScrollDeltaX = deltaX,
            ScrollDeltaY = deltaY,
            ScrollUnit = unit,
            Modifiers = modifiers,
            TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };
        _inputController.HandlePointer(e, _scroll, _selection, _style, _dataSource);
        return e.Handled;
    }

    /// <summary>
    /// The Core button of a SkiaSharp touch event: SkiaSharp's own mapping (right and middle on Windows, a stylus
    /// button on Android), plus a mouse's right and middle buttons on Android. Core takes the button from the press,
    /// so a release that names none is fine.
    /// </summary>
    private PointerButton GetPointerButton(SKTouchEventArgs e)
    {
#if ANDROID
        return GetPlatformPointerButton(e);
#else
        return MapMouseButton(e.MouseButton);
#endif
    }

    private static PointerButton MapMouseButton(SKMouseButton button) => button switch
    {
        SKMouseButton.Right => PointerButton.Secondary,
        SKMouseButton.Middle => PointerButton.Middle,
        _ => PointerButton.Primary,
    };

#if MACCATALYST || IOS
    /// <summary>Modifier keys held during the touch SkiaSharp is reporting.</summary>
    private InputModifiers GetPointerModifiers()
    {
        if (_canvasView.Handler?.PlatformView is not UIView view || view.GestureRecognizers is not { Length: > 0 } recognizers)
            return InputModifiers.None;
        // UIKit sets modifierFlags on the recognizer handling the event: SkiaSharp's touch handler.
        foreach (var recognizer in recognizers)
        {
            if (recognizer.GetType().Name == "SKTouchHandler")
                return ToInputModifiers(recognizer.ModifierFlags);
        }
        return ToInputModifiers(recognizers[0].ModifierFlags);
    }

    /// <summary>UIKit modifier flags as Core modifiers. Command is the shortcut modifier, as on the keyboard path.</summary>
    private static InputModifiers ToInputModifiers(UIKeyModifierFlags flags)
    {
        var modifiers = InputModifiers.None;
        if (flags.HasFlag(UIKeyModifierFlags.Shift)) modifiers |= InputModifiers.Shift;
        if (flags.HasFlag(UIKeyModifierFlags.Command)) modifiers |= InputModifiers.Control | InputModifiers.Meta;
        if (flags.HasFlag(UIKeyModifierFlags.Alternate)) modifiers |= InputModifiers.Alt;
        return modifiers;
    }

    /// <summary>
    /// Trackpad and mouse-wheel scrolling (indirect input) through Core, so it closes nothing it should not,
    /// stops a fling, honors Shift and reaches open popups: the translation since the last call is a pixel delta
    /// whose sign already matches Core's (content follows the fingers).
    /// </summary>
    private void HandleNativeScrollGesture(UIPanGestureRecognizer recognizer)
    {
        if (recognizer.State is not (UIGestureRecognizerState.Began or UIGestureRecognizerState.Changed))
            return;

        var translation = recognizer.TranslationInView(recognizer.View);
        recognizer.SetTranslation(CGPoint.Empty, recognizer.View); // deltas, not cumulative values
        if (translation.X == 0 && translation.Y == 0) return;

        var location = recognizer.LocationInView(recognizer.View);
        DispatchScroll((float)location.X, (float)location.Y, (float)translation.X, (float)translation.Y,
            KumikoScrollUnit.Pixels, ToInputModifiers(recognizer.ModifierFlags));
    }

    /// <summary>
    /// A secondary click (a right click, or a two-finger trackpad click) on Mac Catalyst or an iPad with a pointer.
    /// UIKit delivers it to no touch handler, so it arrives here once the button is up and reaches Core as a press
    /// and release with <see cref="PointerButton.Secondary"/>, which raises <see cref="ContextMenuRequested"/>.
    /// </summary>
    private void HandleNativeSecondaryClick(UITapGestureRecognizer recognizer)
    {
        if (recognizer.State != UIGestureRecognizerState.Ended || !_inputController.IsEnabled) return;

        var location = recognizer.LocationInView(recognizer.View);
        var modifiers = ToInputModifiers(recognizer.ModifierFlags);
        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        DispatchSecondaryClick((float)location.X, (float)location.Y, InputAction.Pressed, modifiers, nowMs);
        DispatchSecondaryClick((float)location.X, (float)location.Y, InputAction.Released, modifiers, nowMs);
    }

    // The pointer id of a secondary click: UIKit gives it no touch, and touch ids (handles) are never negative.
    private const long SecondaryClickPointerId = -2;

    private void DispatchSecondaryClick(float x, float y, InputAction action, InputModifiers modifiers, long nowMs)
    {
        var e = new KumikoPointerEventArgs
        {
            X = x,
            Y = y,
            Action = action,
            Button = PointerButton.Secondary,
            Modifiers = modifiers,
            PointerId = SecondaryClickPointerId,
            DeviceType = KumikoPointerDeviceType.Mouse,
            TimestampMs = nowMs,
        };
        _inputController.HandlePointer(e, _scroll, _selection, _style, _dataSource);
    }
#elif ANDROID
    private Android.Views.View? _nativeCanvasView;
    private Android.Views.MetaKeyStates _lastMetaState;
    private Android.Views.MotionEventButtonState _lastButtonState;

    private void OnCanvasHandlerChanged_Pointer(object? sender, EventArgs e)
    {
        if (_canvasView.Handler == null) DetachNativePointer();
        else AttachNativePointer();
    }

    private void AttachNativePointer()
    {
        if (_canvasView.Handler?.PlatformView is not Android.Views.View view || ReferenceEquals(view, _nativeCanvasView))
            return;
        DetachNativePointer();
        _nativeCanvasView = view;
        view.Touch += OnNativeCanvasTouch;
        view.GenericMotion += OnNativeCanvasGenericMotion;
        // SkiaSharp subscribed its touch listener when the canvas handler connected; mapping EnableTouchEvents again
        // re-subscribes it after this one, so each event's button and modifier state is read before SkiaSharp
        // reports it.
        _canvasView.Handler?.UpdateValue(nameof(SKCanvasView.EnableTouchEvents));
    }

    private void DetachNativePointer()
    {
        if (_nativeCanvasView == null) return;
        _nativeCanvasView.Touch -= OnNativeCanvasTouch;
        _nativeCanvasView.GenericMotion -= OnNativeCanvasGenericMotion;
        _nativeCanvasView = null;
    }

    // Runs before SkiaSharp's own listener for the same event (see AttachNativePointer); it only records the modifier
    // and button state and leaves Handled to SkiaSharp.
    private void OnNativeCanvasTouch(object? sender, Android.Views.View.TouchEventArgs e)
    {
        if (e.Event == null) return;
        _lastMetaState = e.Event.MetaState;
        _lastButtonState = e.Event.ButtonState;
    }

    /// <summary>
    /// A mouse's right and middle buttons, from the <c>MotionEvent</c> being reported: SkiaSharp names the right
    /// button only for a stylus.
    /// </summary>
    private PointerButton GetPlatformPointerButton(SKTouchEventArgs e)
    {
        if (e.DeviceType == SKTouchDeviceType.Mouse)
        {
            if ((_lastButtonState & Android.Views.MotionEventButtonState.Secondary) != 0) return PointerButton.Secondary;
            if ((_lastButtonState & Android.Views.MotionEventButtonState.Tertiary) != 0) return PointerButton.Middle;
        }
        return MapMouseButton(e.MouseButton);
    }

    private InputModifiers GetPointerModifiers() => ToInputModifiers(_lastMetaState);

    private static InputModifiers ToInputModifiers(Android.Views.MetaKeyStates meta)
    {
        var modifiers = InputModifiers.None;
        if ((meta & Android.Views.MetaKeyStates.ShiftOn) != 0) modifiers |= InputModifiers.Shift;
        if ((meta & Android.Views.MetaKeyStates.CtrlOn) != 0) modifiers |= InputModifiers.Control;
        if ((meta & Android.Views.MetaKeyStates.AltOn) != 0) modifiers |= InputModifiers.Alt;
        if ((meta & Android.Views.MetaKeyStates.MetaOn) != 0) modifiers |= InputModifiers.Meta;
        return modifiers;
    }

    /// <summary>
    /// A mouse wheel or touchpad scroll (<c>ACTION_SCROLL</c>) in notches. Android's vertical axis is positive
    /// upward like Core's; its horizontal axis is positive to the right, the opposite of Core's.
    /// </summary>
    private void OnNativeCanvasGenericMotion(object? sender, Android.Views.View.GenericMotionEventArgs e)
    {
        var motion = e.Event;
        if (motion == null || motion.ActionMasked != Android.Views.MotionEventActions.Scroll || !TryGetTouchScale(out float scale))
        {
            e.Handled = false;
            return;
        }

        float vertical = motion.GetAxisValue(Android.Views.Axis.Vscroll);
        float horizontal = motion.GetAxisValue(Android.Views.Axis.Hscroll);
        e.Handled = DispatchScroll(motion.GetX() / scale, motion.GetY() / scale, -horizontal, vertical,
            KumikoScrollUnit.Notches, ToInputModifiers(motion.MetaState));
    }
#elif WINDOWS
    private Microsoft.UI.Xaml.UIElement? _nativeWheelView;
    private Microsoft.UI.Xaml.Input.PointerEventHandler? _nativeWheelHandler;

    /// <summary>
    /// Listens for the wheel on the grid's platform view, the canvas's parent: the event bubbles there after
    /// SkiaSharp's handler on the canvas, so the handled flag set here is the one the page sees.
    /// </summary>
    private void AttachNativeWheel()
    {
        if (Handler?.PlatformView is not Microsoft.UI.Xaml.UIElement view || ReferenceEquals(view, _nativeWheelView))
            return;
        DetachNativeWheel();
        _nativeWheelHandler ??= new Microsoft.UI.Xaml.Input.PointerEventHandler(OnNativeWheelChanged);
        view.AddHandler(Microsoft.UI.Xaml.UIElement.PointerWheelChangedEvent, _nativeWheelHandler, true);
        _nativeWheelView = view;
    }

    private void DetachNativeWheel()
    {
        if (_nativeWheelView == null || _nativeWheelHandler == null) return;
        _nativeWheelView.RemoveHandler(Microsoft.UI.Xaml.UIElement.PointerWheelChangedEvent, _nativeWheelHandler);
        _nativeWheelView = null;
    }

    /// <summary>
    /// The wheel in 120ths of a notch, one axis per event. WinUI's horizontal delta is positive to the right,
    /// the opposite of Core's.
    /// </summary>
    private void OnNativeWheelChanged(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (!_inputController.IsEnabled) return;
        var origin = _canvasView.Handler?.PlatformView as Microsoft.UI.Xaml.UIElement ?? _nativeWheelView;
        var point = e.GetCurrentPoint(origin);
        var props = point.Properties;
        float notches = props.MouseWheelDelta / 120f;
        bool handled = DispatchScroll((float)point.Position.X, (float)point.Position.Y,
            props.IsHorizontalMouseWheel ? -notches : 0f, props.IsHorizontalMouseWheel ? 0f : notches,
            KumikoScrollUnit.Notches, ToInputModifiers(e.KeyModifiers));
        if (handled) e.Handled = true;
    }

    private static InputModifiers ToInputModifiers(Windows.System.VirtualKeyModifiers keys)
    {
        var modifiers = InputModifiers.None;
        if ((keys & Windows.System.VirtualKeyModifiers.Shift) != 0) modifiers |= InputModifiers.Shift;
        if ((keys & Windows.System.VirtualKeyModifiers.Control) != 0) modifiers |= InputModifiers.Control;
        if ((keys & Windows.System.VirtualKeyModifiers.Menu) != 0) modifiers |= InputModifiers.Alt;
        if ((keys & Windows.System.VirtualKeyModifiers.Windows) != 0) modifiers |= InputModifiers.Meta;
        return modifiers;
    }

    /// <summary>Modifier keys held now (SkiaSharp's touch event carries none).</summary>
    private static InputModifiers GetPointerModifiers()
    {
        var keys = Windows.System.VirtualKeyModifiers.None;
        if (IsKeyDown(Windows.System.VirtualKey.Shift)) keys |= Windows.System.VirtualKeyModifiers.Shift;
        if (IsKeyDown(Windows.System.VirtualKey.Control)) keys |= Windows.System.VirtualKeyModifiers.Control;
        if (IsKeyDown(Windows.System.VirtualKey.Menu)) keys |= Windows.System.VirtualKeyModifiers.Menu;
        if (IsKeyDown(Windows.System.VirtualKey.LeftWindows) || IsKeyDown(Windows.System.VirtualKey.RightWindows))
            keys |= Windows.System.VirtualKeyModifiers.Windows;
        return ToInputModifiers(keys);
    }

    private static bool IsKeyDown(Windows.System.VirtualKey key) =>
        (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(key) & Windows.UI.Core.CoreVirtualKeyStates.Down)
            == Windows.UI.Core.CoreVirtualKeyStates.Down;
#else
    private static InputModifiers GetPointerModifiers() => InputModifiers.None;
#endif
}

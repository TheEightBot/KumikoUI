using System.Diagnostics;
using KumikoUI.Core.Input;
using KumikoUI.Uno.Input;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace KumikoUI.Uno;

/// <summary>
/// Phase 04 input wiring for <see cref="KumikoDataGrid"/>: pointer, keyboard and focus. A fling hands over to
/// the frame loop in <c>KumikoDataGrid.Rendering.cs</c>, which steps the inertial scroller. WinUI delivers all of these as
/// routed events on the control itself; this partial adapts each to the platform-independent
/// <see cref="KumikoInputController"/> API (<c>HandlePointer</c> / <c>HandleKey</c> /
/// <c>UpdateInertialScroll</c>) that already owns the behavior.
/// </summary>
/// <remarks>
/// <para>
/// Coordinates: <see cref="PointerPoint.Position"/> from <c>GetCurrentPoint(PointerOrigin)</c> is in logical
/// DIPs relative to the drawing surface — the same space and origin the renderer draws in — so hit-testing
/// aligns at every display scale with no extra conversion (unlike MAUI, which divides <c>SKTouch.Location</c>
/// by the canvas scale), and stays aligned if the surface is ever inset from the control (UNO-18).
/// </para>
/// <para>
/// Handlers are attached in <see cref="AttachInputHandlers"/> (from <c>OnLoaded</c>) and fully
/// removed in <see cref="DetachInputHandlers"/> (from <c>OnUnloaded</c>); the frame loop and the caret
/// timer are stopped on unload (<c>DetachSurface</c>) so nothing roots the page across navigation.
/// </para>
/// </remarks>
public partial class KumikoDataGrid
{
    // ── Double-tap detection (WinUI pointer args carry no click count) ──
    // WinUI's DoubleTapped routed event fires *after* the second press/release pair, which is too
    // late to stamp ClickCount on that release (and would mis-count a later third click). So we
    // detect it on Pressed via press timing + distance and carry it to the matching Released so
    // both report ClickCount=2 — exactly how the MAUI control does it in OnCanvasTouch.
    private PointerGestures.TapTracker _tapTracker;
    private int _pendingClickCount = 1;

    // ── Touch-vs-mouse tracking ──
    // Tracks whether the last pointer press was a touch event. A mouse/pen press focuses the proxy;
    // a touch press does not (it would summon the soft keyboard on every tap). When a touch edit ends
    // the proxy keeps the focus and the soft keyboard is hidden explicitly (OnEditSessionCellEndEdit).
    private bool _lastPointerWasTouch;

    // ── Pointer identity (UNO-19) ──
    // The pointer whose press is in progress, and when it was last heard from. Core ignores a second finger by
    // PointerId; the host also keeps it out of its own press bookkeeping (click count, keyboard focus). A press
    // after the pressed pointer has been silent for a second takes over, in case its release was lost (as Core).
    private uint? _pressedPointerId;
    private long _pressedPointerLastMs;
    private const long StalePointerMs = 1000;

    /// <summary>
    /// Attaches pointer + keyboard handlers. Called from <c>OnLoaded</c>. Idempotent — every
    /// handler is detached first so a re-load (Uno raises Loaded again on navigation back) never
    /// double-subscribes. (The control is made a tab stop once, in the constructor.)
    /// </summary>
    private void AttachInputHandlers()
    {
        // ── Pointer ──
        PointerPressed -= OnPointerPressed; PointerPressed += OnPointerPressed;
        PointerMoved -= OnPointerMoved; PointerMoved += OnPointerMoved;
        PointerReleased -= OnPointerReleased; PointerReleased += OnPointerReleased;
        PointerCanceled -= OnPointerCanceled; PointerCanceled += OnPointerCanceled;
        PointerCaptureLost -= OnPointerCaptureLost; PointerCaptureLost += OnPointerCaptureLost;
        PointerWheelChanged -= OnPointerWheelChanged; PointerWheelChanged += OnPointerWheelChanged;
        PointerExited -= OnPointerExited; PointerExited += OnPointerExited;

        // ── Gestures: holding (touch long-press). Click count is derived from press timing in
        //    OnPointerPressed, not the DoubleTapped routed event (which fires too late to stamp). ──
        Holding -= OnHolding; Holding += OnHolding;
        IsHoldingEnabled = true;

        // ── Keyboard ──
        // KeyDown is kept on the grid as a harmless fallback for tab-focus navigation. Printable
        // character input flows ONLY through the proxy's text changes — no CharacterReceived here,
        // which would double-fire on Windows (where CharacterReceived bubbles up from the focused
        // proxy to the grid, which would deliver every character twice).
        KeyDown -= OnKeyDown; KeyDown += OnKeyDown;

        // ── Text-input proxy and adapter (see KumikoDataGrid.Editing.cs) ──
        AttachInputProxy();
    }

    /// <summary>
    /// Detaches every pointer + keyboard handler.
    /// Called from <c>OnUnloaded</c> so no delegate roots this control across navigation.
    /// </summary>
    private void DetachInputHandlers()
    {
        PointerPressed -= OnPointerPressed;
        PointerMoved -= OnPointerMoved;
        PointerReleased -= OnPointerReleased;
        PointerCanceled -= OnPointerCanceled;
        PointerCaptureLost -= OnPointerCaptureLost;
        PointerWheelChanged -= OnPointerWheelChanged;
        PointerExited -= OnPointerExited;

        Holding -= OnHolding;

        KeyDown -= OnKeyDown;

        // Detach the hidden TextBox proxy's events (see KumikoDataGrid.Editing.cs).
        TeardownInputProxy();
    }

    /// <summary>
    /// Mirrors <c>IsEnabled</c> into Core: a disabled grid does not select, sort, scroll, drag or
    /// edit, leaves pointer, wheel and key events unhandled for the page, and ends what was in progress.
    /// </summary>
    private void OnIsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        _inputController.IsEnabled = IsEnabled;
        if (!IsEnabled) _pressedPointerId = null;
        InvalidateSurface();
    }

    // ── Pointer event handlers ───────────────────────────────────

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        // A disabled grid takes no input: no capture, no focus, nothing for Core.
        if (!_inputController.IsEnabled) return;

        var point = e.GetCurrentPoint(PointerOrigin);

        // A second finger while one is down: Core ignores it (and marks it handled); it must not touch the click
        // count or the focus of the press in progress.
        long nowMs = ToMillis(point.Timestamp);
        if (_pressedPointerId is { } pressed && pressed != e.Pointer.PointerId &&
            nowMs - _pressedPointerLastMs <= StalePointerMs)
        {
            DispatchPointer(e, point, InputAction.Pressed);
            return;
        }
        _pressedPointerId = e.Pointer.PointerId;
        _pressedPointerLastMs = nowMs;

        // Capture so drags (resize / reorder / pan / selection) keep tracking outside our bounds.
        CapturePointer(e.Pointer);
        _lastPointerWasTouch = e.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Touch;

        // Double-tap heuristic: detect on Pressed (time + distance from the previous tap) and carry
        // the count to the matching Released so both report ClickCount=2 (mirrors MAUI's OnCanvasTouch).
        int clickCount = _tapTracker.RegisterPress(nowMs, (float)point.Position.X, (float)point.Position.Y);
        _pendingClickCount = clickCount;

        DispatchPointer(e, point, InputAction.Pressed, clickCount);
        if (_editSession.IsAnimating)
            StartInertialScroll(); // a spin button pressed and held repeats from the frame loop

        // Route the keyboard to the hidden TextBox proxy for non-touch input (mouse/pen). The proxy
        // is a real focusable element on every head, unlike this control, whose KeyDown/
        // CharacterReceived don't fire on WASM/Skia. Done AFTER DispatchPointer so it wins over any
        // focus change made while Core handled the press (e.g. CellEndEdit). For touch we deliberately
        // do NOT focus here — it would summon the soft keyboard on every tap; editing focuses the
        // proxy on CellBeginEdit instead.
        if (!_lastPointerWasTouch)
            FocusKeyboardInput();
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        // WinUI/Uno raises PointerMoved on every hover, even with no button pressed (desktop and WASM
        // heads). Plain hover goes to Core as InputAction.Hover, which only updates the cursor and the
        // hover feedback of popups and editors; it never pans. A move in contact is a drag — and, for
        // a mouse, only the LEFT button drags/scrolls (a right/middle drag must not pan). Touch/pen
        // contact reports IsInContact without a named mouse button, so the right/middle guard leaves
        // mobile scrolling unaffected.
        var point = e.GetCurrentPoint(PointerOrigin);
        if (!point.IsInContact)
        {
            DispatchPointer(e, point, InputAction.Hover);
            return;
        }
        var props = point.Properties;
        if (props.IsRightButtonPressed || props.IsMiddleButtonPressed) return;
        if (_pressedPointerId == e.Pointer.PointerId) _pressedPointerLastMs = ToMillis(point.Timestamp);
        DispatchPointer(e, point, InputAction.Moved);
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(PointerOrigin);
        if (_pressedPointerId != e.Pointer.PointerId)
        {
            // Another finger lifting: Core ignores it by id. Not the end of the press in progress.
            DispatchPointer(e, point, InputAction.Released);
            ReleasePointerCapture(e.Pointer);
            return;
        }
        _pressedPointerId = null;

        // Carry the click count from the preceding Pressed so a double-click commits on the up.
        int clickCount = _pendingClickCount;
        _pendingClickCount = 1;

        DispatchPointer(e, point, InputAction.Released, clickCount);
        // Releasing the capture raises PointerCaptureLost, which reaches Core as a Cancelled with no press in
        // progress: Core ignores it.
        ReleasePointerCapture(e.Pointer);

        // Touch fling: Core started the inertial scroller (or a scroll picker editor started coasting and
        // snapping) on release; the frame loop steps it once per frame.
        if (_inputController.IsInertialScrolling || _editSession.IsAnimating)
            StartInertialScroll();
    }

    private void OnPointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        if (_pressedPointerId == e.Pointer.PointerId) _pressedPointerId = null;
        DispatchPointer(e, e.GetCurrentPoint(PointerOrigin), InputAction.Cancelled);
        if (_inputController.IsInertialScrolling)
            StartInertialScroll();
    }

    private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        // Losing capture mid-drag is equivalent to a cancel — preserve scroll momentum (Core's
        // HandlePointerCancelled starts a fling if we were panning).
        if (_pressedPointerId == e.Pointer.PointerId) _pressedPointerId = null;
        DispatchPointer(e, e.GetCurrentPoint(PointerOrigin), InputAction.Cancelled);
        if (_inputController.IsInertialScrolling)
            StartInertialScroll();
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        // Core resets the cursor to the default unless a column resize drag is in progress; it does not
        // cancel an active drag (capture keeps drags alive past the bounds, and PointerCaptureLost
        // handles real loss).
        DispatchPointer(e, e.GetCurrentPoint(PointerOrigin), InputAction.Exited);
    }

    // ── Cursor ──
    // InputSystemCursor instances are cached: Core raises CursorChanged only on a change, but a cursor
    // object per change would still be churn. Created lazily on the UI thread, where cursors are set.
    private InputCursor? _resizeCursor, _handCursor, _arrowCursor;

    /// <summary>
    /// Shows the cursor Core asks for (a resize cursor over column grips and during a resize, a hand over
    /// clickable glyphs). <see cref="UIElement.ProtectedCursor"/> is protected, which this control can set
    /// on itself. Touch-only heads ignore it.
    /// </summary>
    private void OnInputControllerCursorChanged(KumikoCursor cursor)
    {
        ProtectedCursor = cursor switch
        {
            KumikoCursor.ResizeHorizontal => _resizeCursor ??= InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast),
            KumikoCursor.Hand => _handCursor ??= InputSystemCursor.Create(InputSystemCursorShape.Hand),
            _ => _arrowCursor ??= InputSystemCursor.Create(InputSystemCursorShape.Arrow),
        };
    }

    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        // Core handles the wheel only when the grid moved: at the end of its content (or while disabled) the event
        // stays unhandled and bubbles on, so a page around the grid scrolls (UNO-14).
        var point = e.GetCurrentPoint(PointerOrigin);
        DispatchPointer(e, point, InputAction.Scroll);
    }

    /// <summary>
    /// Builds a <see cref="KumikoPointerEventArgs"/> from a WinUI pointer point and forwards it to
    /// the Core input controller. Mirrors MAUI's <c>OnCanvasTouch</c>, differing only in the source
    /// type and the (already-logical) coordinate space.
    /// </summary>
    private void DispatchPointer(PointerRoutedEventArgs e, PointerPoint point, InputAction action, int clickCount = 1)
    {
        var props = point.Properties;

        // Wheel: 120ths of a notch on Windows/X11, pixels on macOS and in the browser, one axis per event, the
        // horizontal sign normalized per head (PointerMapping). Shift+wheel is turned horizontal by Core.
        var (scrollX, scrollY, scrollUnit) = action == InputAction.Scroll
            ? PointerMapping.ToScrollDelta(props.MouseWheelDelta, props.IsHorizontalMouseWheel,
                PointerMapping.WheelDeltaIsPixels, PointerMapping.HorizontalWheelPositiveIsLeft)
            : (0f, 0f, KumikoScrollUnit.Notches);

        var evt = new KumikoPointerEventArgs
        {
            X = (float)point.Position.X,
            Y = (float)point.Position.Y,
            Action = action,
            // On a release no button is pressed any more: the update kind names the one that went up (UNO-19).
            Button = PointerMapping.ToPointerButton(action, props.PointerUpdateKind,
                props.IsRightButtonPressed, props.IsMiddleButtonPressed),
            PointerId = e.Pointer.PointerId,
            DeviceType = PointerMapping.ToDeviceType(e.Pointer.PointerDeviceType),
            ScrollDeltaX = scrollX,
            ScrollDeltaY = scrollY,
            ScrollUnit = scrollUnit,
            Modifiers = InputMapping.ToInputModifiers(e.KeyModifiers),
            TimestampMs = ToMillis(point.Timestamp), // WinUI timestamp is microseconds
            ClickCount = clickCount,
        };

        _inputController.HandlePointer(evt, _scroll, _selection, _style, _dataSource);
        e.Handled = evt.Handled;

        // A row drag held at an edge scrolls the grid: the frame loop steps it (UpdateAutoScroll) once per frame.
        if (_inputController.IsAutoScrolling)
            StartInertialScroll();
    }

    /// <summary>
    /// Touch long-press → <see cref="InputAction.LongPress"/> (starts a row drag with <c>AllowRowDragDrop</c> or on a
    /// drag handle; the LongPress edit trigger). Only fires for touch and only on <see cref="HoldingState.Started"/>; mouse/pen use
    /// right-click, which Core reads from <see cref="PointerButton.Secondary"/>.
    /// </summary>
    private void OnHolding(object sender, HoldingRoutedEventArgs e)
    {
        if (e.PointerDeviceType != PointerDeviceType.Touch) return;
        if (e.HoldingState != HoldingState.Started) return;
        if (_pressedPointerId is not { } pointerId) return; // the press already ended (or was disabled)

        var pos = e.GetPosition(PointerOrigin);
        var evt = new KumikoPointerEventArgs
        {
            X = (float)pos.X,
            Y = (float)pos.Y,
            Action = InputAction.LongPress,
            Button = PointerButton.Primary,
            PointerId = pointerId, // Holding carries no pointer: it belongs to the press in progress
            DeviceType = KumikoPointerDeviceType.Touch, // Holding is handled for touch only (above)
            TimestampMs = ToMillis(GetCurrentTimestampMicros()),
            ClickCount = 1,
        };
        _inputController.HandlePointer(evt, _scroll, _selection, _style, _dataSource);
        e.Handled = evt.Handled;
    }

    // ── Keyboard event handlers ──────────────────────────────────

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        // A key the proxy TextBox left unhandled bubbles up here after OnProxyKeyDown already sent it to Core:
        // don't send it twice.
        if (ReferenceEquals(e.OriginalSource, _inputProxy)) return;

        var key = InputMapping.ToGridKey(e.Key);
        if (key == KumikoKey.None) return; // printable text comes through the proxy's text changes

        // KeyRoutedEventArgs.KeyboardModifiers is [NotImplemented] on the Uno Skia/WASM heads (the
        // source generator strips it from the public surface there), so read the live modifier
        // state instead — InputKeyboardSource.GetKeyStateForCurrentThread IS implemented on Skia.
        var evt = new KumikoKeyEventArgs
        {
            Key = key,
            Modifiers = InputMapping.GetLiveModifiers(),
            IsKeyDown = true,
        };
        _inputController.HandleKey(evt, _scroll, _selection, _style, _dataSource);
        if (_editSession.IsAnimating)
            StartInertialScroll(); // a held arrow key repeats a spin button from the frame loop
        if (evt.Handled)
        {
            e.Handled = true;
        }
        else if (key == KumikoKey.Tab)
        {
            // Core leaves Tab unhandled past the last/first tab stop: focus leaves the grid. Default focus
            // navigation from the grid would land on its own hidden proxy, so step past it explicitly.
            MoveFocusOutOfGrid(backwards: evt.HasShift);
            e.Handled = true;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────

    /// <summary>
    /// The element pointer positions are measured against: the drawing surface, whose top-left corner is the
    /// renderer's origin.
    /// </summary>
    private UIElement PointerOrigin => _surface.Element;

    /// <summary>WinUI pointer timestamps are in microseconds; Core wants milliseconds.</summary>
    private static long ToMillis(ulong timestampMicroseconds) => PointerGestures.ToMillis(timestampMicroseconds);

    /// <summary>
    /// Current high-resolution timestamp in microseconds, matching <see cref="PointerPoint.Timestamp"/>'s
    /// unit, for the <c>Holding</c> gesture event which does not expose a pointer timestamp.
    /// </summary>
    private static ulong GetCurrentTimestampMicros() =>
        (ulong)(Stopwatch.GetTimestamp() * 1_000_000L / Stopwatch.Frequency);
}

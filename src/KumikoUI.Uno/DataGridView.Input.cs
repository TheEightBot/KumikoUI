using System.Diagnostics;
using KumikoUI.Core.Input;
using KumikoUI.Uno.Input;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace KumikoUI.Uno;

/// <summary>
/// Phase 04 input wiring for <see cref="DataGridView"/>: pointer, keyboard, focus, and the
/// input-driven repaint timers (inertial scroll + cursor blink). WinUI delivers all of these as
/// routed events on the control itself; this partial adapts each to the platform-independent
/// <see cref="GridInputController"/> API (<c>HandlePointer</c> / <c>HandleKey</c> /
/// <c>UpdateInertialScroll</c>) that already owns the behavior.
/// </summary>
/// <remarks>
/// <para>
/// Coordinates: <see cref="PointerPoint.Position"/> from <c>GetCurrentPoint(this)</c> is in logical
/// DIPs — the same space the renderer was fed in Phase 03 — so hit-testing aligns at every display
/// scale with no extra conversion (unlike MAUI, which divides <c>SKTouch.Location</c> by the canvas
/// scale).
/// </para>
/// <para>
/// Handlers are attached in <see cref="AttachInputHandlers"/> (from <c>OnLoaded</c>) and fully
/// removed in <see cref="DetachInputHandlers"/> (from <c>OnUnloaded</c>); all timers are stopped on
/// unload so nothing roots the page across navigation.
/// </para>
/// </remarks>
public partial class DataGridView
{
    // ── Inertial scroll timer (mirrors MAUI's _scrollTimer) ──
    private DispatcherTimer? _scrollTimer;

    // ── Cursor-blink timer — drives regular repaints while editing or a filter popup is open
    //    (mirrors MAUI's _cursorBlinkTimer) ──
    private DispatcherTimer? _cursorBlinkTimer;
    private bool _filterPopupActive;

    // ── Double-tap detection (WinUI pointer args carry no click count) ──
    // WinUI's DoubleTapped routed event fires *after* the second press/release pair, which is too
    // late to stamp ClickCount on that release (and would mis-count a later third click). So we
    // detect it on Pressed via press timing + distance and carry it to the matching Released so
    // both report ClickCount=2 — exactly how the MAUI control does it in OnCanvasTouch.
    private long _lastTapTimeMs;
    private float _lastTapX, _lastTapY;
    private int _pendingClickCount = 1;
    private const long DoubleTapThresholdMs = 400;
    private const float DoubleTapDistanceThreshold = 20f;

    // ── Wheel scaling ──
    // WinUI MouseWheelDelta is in 120-unit notches; Core's HandleScroll multiplies ScrollDeltaY
    // by (RowHeight * WheelScrollMultiplier). Normalizing one notch (120) to 1.0 makes a single
    // wheel notch scroll WheelScrollMultiplier rows — matching the MAUI feel, where SKTouch.WheelDelta
    // is already a small per-notch value fed straight in.
    private const float WheelNotch = 120f;

    /// <summary>
    /// Attaches pointer + keyboard handlers and enables focusability. Called from
    /// <c>OnLoaded</c>. Idempotent — every handler is detached first so a re-load
    /// (Uno raises Loaded again on navigation back) never double-subscribes.
    /// </summary>
    private void AttachInputHandlers()
    {
        // Focusable so KeyDown / CharacterReceived are delivered. IsTabStop / TabIndex / Focus
        // live on UIElement in WinUI (not Control), so they are available on this Grid-derived
        // control. Set here (not just the ctor) so a re-attach after Unloaded restores it.
        IsTabStop = true;
        if (TabIndex == int.MaxValue) // WinUI default — only set once, respect any XAML override.
            TabIndex = 0;

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
        KeyDown -= OnKeyDown; KeyDown += OnKeyDown;
        CharacterReceived -= OnCharacterReceived; CharacterReceived += OnCharacterReceived;

        // ── Soft-keyboard proxy (see DataGridView.Editing.cs) ──
        // Idempotent re-attach: detach first so navigation-back never double-subscribes.
        _inputProxy.TextChanged -= OnProxyTextChanged; _inputProxy.TextChanged += OnProxyTextChanged;
        _inputProxy.KeyDown -= OnProxyKeyDown; _inputProxy.KeyDown += OnProxyKeyDown;
    }

    /// <summary>
    /// Detaches every pointer + keyboard handler and stops all input-driven timers.
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
        CharacterReceived -= OnCharacterReceived;

        // Detach the hidden TextBox proxy's events (see DataGridView.Editing.cs).
        TeardownInputProxy();

        StopInertialScrollTimer();
        StopCursorBlinkTimer();
    }

    // ── Pointer event handlers ───────────────────────────────────

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        // Capture so drags (resize / reorder / pan / selection) keep tracking outside our bounds.
        CapturePointer(e.Pointer);
        // Focus on press so keyboard navigation works after a click (see OnKeyDown).
        // During an active edit the proxy holds focus (and the soft keyboard is open on mobile);
        // stealing focus here would prematurely dismiss the keyboard. The edit lifecycle
        // (CellBeginEdit / CellEndEdit) owns proxy focus while editing is in progress.
        if (!_editSession.IsEditing)
            Focus(FocusState.Programmatic);

        var point = e.GetCurrentPoint(this);

        // Double-tap heuristic: detect on Pressed (time + distance from the previous tap) and carry
        // the count to the matching Released so both report ClickCount=2 (mirrors MAUI's OnCanvasTouch).
        int clickCount = 1;
        long nowMs = ToMillis(point.Timestamp);
        float x = (float)point.Position.X;
        float y = (float)point.Position.Y;
        float dx = x - _lastTapX;
        float dy = y - _lastTapY;
        if (nowMs - _lastTapTimeMs < DoubleTapThresholdMs &&
            MathF.Sqrt(dx * dx + dy * dy) < DoubleTapDistanceThreshold)
        {
            clickCount = 2;
            _lastTapTimeMs = 0; // reset so a third tap doesn't also count as double
        }
        else
        {
            _lastTapTimeMs = nowMs;
        }
        _lastTapX = x;
        _lastTapY = y;
        _pendingClickCount = clickCount;

        DispatchPointer(e, point, InputAction.Pressed, clickCount);
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        // WinUI/Uno raises PointerMoved on every hover, even with no button pressed. Core's
        // GridInputController.HandlePointerMove expects Moved only while in contact (matching the
        // MAUI SKCanvasView contract), so we filter out plain hover here. Only forward when the
        // pointer is actually down — and, for a mouse, only the LEFT button drags/scrolls (a
        // right/middle drag must not pan). Touch/pen contact reports IsInContact without a named
        // mouse button, so the right/middle guard leaves mobile scrolling unaffected.
        var point = e.GetCurrentPoint(this);
        var props = point.Properties;
        if (!point.IsInContact || props.IsRightButtonPressed || props.IsMiddleButtonPressed) return;
        DispatchPointer(e, point, InputAction.Moved);
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        // Carry the click count from the preceding Pressed so a double-click commits on the up.
        int clickCount = _pendingClickCount;
        _pendingClickCount = 1;

        DispatchPointer(e, point, InputAction.Released, clickCount);
        ReleasePointerCapture(e.Pointer);

        // Touch fling: Core started the inertial scroller on release; pump it from a frame timer.
        if (_inputController.IsInertialScrolling)
            StartInertialScrollTimer();
    }

    private void OnPointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        DispatchPointer(e, e.GetCurrentPoint(this), InputAction.Cancelled);
        if (_inputController.IsInertialScrolling)
            StartInertialScrollTimer();
    }

    private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        // Losing capture mid-drag is equivalent to a cancel — preserve scroll momentum (Core's
        // HandlePointerCancelled starts a fling if we were panning).
        DispatchPointer(e, e.GetCurrentPoint(this), InputAction.Cancelled);
        if (_inputController.IsInertialScrolling)
            StartInertialScrollTimer();
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        // Reset the hover/resize cursor when the pointer leaves; do not cancel an active drag
        // (capture keeps drags alive past the bounds, and PointerCaptureLost handles real loss).
        ProtectedCursor = null;
    }

    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        DispatchPointer(e, point, InputAction.Scroll);
    }

    /// <summary>
    /// Builds a <see cref="GridPointerEventArgs"/> from a WinUI pointer point and forwards it to
    /// the Core input controller. Mirrors MAUI's <c>OnCanvasTouch</c>, differing only in the source
    /// type and the (already-logical) coordinate space.
    /// </summary>
    private void DispatchPointer(PointerRoutedEventArgs e, PointerPoint point, InputAction action, int clickCount = 1)
    {
        var props = point.Properties;

        var evt = new GridPointerEventArgs
        {
            X = (float)point.Position.X,
            Y = (float)point.Position.Y,
            Action = action,
            Button = props.IsRightButtonPressed  ? PointerButton.Secondary
                   : props.IsMiddleButtonPressed ? PointerButton.Middle
                   : props.IsLeftButtonPressed   ? PointerButton.Primary
                   : PointerButton.None,
            // 120-unit notches → notch count; Core scales by RowHeight * WheelScrollMultiplier.
            ScrollDeltaY = action == InputAction.Scroll ? props.MouseWheelDelta / WheelNotch : 0f,
            ScrollDeltaX = action == InputAction.Scroll && props.IsHorizontalMouseWheel
                ? props.MouseWheelDelta / WheelNotch : 0f,
            Modifiers = InputMapping.ToInputModifiers(e.KeyModifiers),
            TimestampMs = ToMillis(point.Timestamp), // WinUI timestamp is microseconds
            ClickCount = clickCount,
        };

        _inputController.HandlePointer(evt, _scroll, _selection, _style, _dataSource);
        e.Handled = evt.Handled;
    }

    /// <summary>
    /// Touch long-press → <see cref="InputAction.LongPress"/> (row drag handle / context actions on
    /// mobile). Only fires for touch and only on <see cref="HoldingState.Started"/>; mouse/pen use
    /// right-click, which Core reads from <see cref="PointerButton.Secondary"/>.
    /// </summary>
    private void OnHolding(object sender, HoldingRoutedEventArgs e)
    {
        if (e.PointerDeviceType != PointerDeviceType.Touch) return;
        if (e.HoldingState != HoldingState.Started) return;

        var pos = e.GetPosition(this);
        var evt = new GridPointerEventArgs
        {
            X = (float)pos.X,
            Y = (float)pos.Y,
            Action = InputAction.LongPress,
            Button = PointerButton.Primary,
            TimestampMs = ToMillis(GetCurrentTimestampMicros()),
            ClickCount = 1,
        };
        _inputController.HandlePointer(evt, _scroll, _selection, _style, _dataSource);
        e.Handled = evt.Handled;
    }

    // ── Keyboard event handlers ──────────────────────────────────

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var key = InputMapping.ToGridKey(e.Key);
        if (key == GridKey.None) return; // printable text comes via CharacterReceived

        // KeyRoutedEventArgs.KeyboardModifiers is [NotImplemented] on the Uno Skia/WASM heads (the
        // source generator strips it from the public surface there), so read the live modifier
        // state instead — InputKeyboardSource.GetKeyStateForCurrentThread IS implemented on Skia.
        var evt = new GridKeyEventArgs
        {
            Key = key,
            Modifiers = InputMapping.GetLiveModifiers(),
            IsKeyDown = true,
        };
        _inputController.HandleKey(evt, _scroll, _selection, _style, _dataSource);
        if (evt.Handled) e.Handled = true;
    }

    private void OnCharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs e)
    {
        // Drives cell editing with the resolved character (honours IME / dead keys). Cleaner than
        // MAUI's hidden-Entry proxy: Core decides whether the char starts/continues an edit.
        var evt = new GridKeyEventArgs
        {
            Key = GridKey.None,
            Character = e.Character,
            Modifiers = InputMapping.GetLiveModifiers(),
            IsKeyDown = true,
        };
        _inputController.HandleKey(evt, _scroll, _selection, _style, _dataSource);
        if (evt.Handled) e.Handled = true;
    }

    // ── Inertial scroll timer (mirrors MAUI's _scrollTimer) ──────

    private void StartInertialScrollTimer()
    {
        if (_scrollTimer != null) return;

        _scrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) }; // ~60fps
        _scrollTimer.Tick += OnScrollTimerTick;
        _scrollTimer.Start();
    }

    private void OnScrollTimerTick(object? sender, object e)
    {
        if (!_inputController.UpdateInertialScroll(_scroll, 16f))
            StopInertialScrollTimer();
        // UpdateInertialScroll raises NeedsRedraw on each step that moves; an explicit invalidate
        // here keeps the surface in lockstep with the timer even on the final (stopping) tick.
        _canvasView.Invalidate();
    }

    private void StopInertialScrollTimer()
    {
        if (_scrollTimer != null)
        {
            _scrollTimer.Stop();
            _scrollTimer.Tick -= OnScrollTimerTick;
            _scrollTimer = null;
        }
    }

    // ── Cursor-blink timer (mirrors MAUI's _cursorBlinkTimer) ────

    private void StartCursorBlinkTimer()
    {
        if (_cursorBlinkTimer != null) return;

        _cursorBlinkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(530) };
        _cursorBlinkTimer.Tick += OnCursorBlinkTimerTick;
        _cursorBlinkTimer.Start();
    }

    private void OnCursorBlinkTimerTick(object? sender, object e)
    {
        if (_editSession.IsEditing || _filterPopupActive)
            _canvasView.Invalidate();
        else
            StopCursorBlinkTimer();
    }

    private void StopCursorBlinkTimer()
    {
        if (_cursorBlinkTimer != null)
        {
            _cursorBlinkTimer.Stop();
            _cursorBlinkTimer.Tick -= OnCursorBlinkTimerTick;
            _cursorBlinkTimer = null;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────

    /// <summary>WinUI pointer timestamps are in microseconds; Core wants milliseconds.</summary>
    private static long ToMillis(ulong timestampMicroseconds) => (long)(timestampMicroseconds / 1000);

    /// <summary>
    /// Current high-resolution timestamp in microseconds, matching <see cref="PointerPoint.Timestamp"/>'s
    /// unit, for the <c>Holding</c> gesture event which does not expose a pointer timestamp.
    /// </summary>
    private static ulong GetCurrentTimestampMicros() =>
        (ulong)(Stopwatch.GetTimestamp() * 1_000_000L / Stopwatch.Frequency);
}

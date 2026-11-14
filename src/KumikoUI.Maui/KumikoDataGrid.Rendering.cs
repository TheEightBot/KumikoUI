using System.Diagnostics;
using KumikoUI.Core.Rendering;
using KumikoUI.Maui.Rendering;
using KumikoUI.SkiaSharp;
using SkiaSharp;

namespace KumikoUI.Maui;

/// <summary>
/// Rendering for <see cref="KumikoDataGrid"/> (#71): the drawing surface (<see cref="RenderMode"/>), the frame loop
/// that drives redraws and inertial scrolling, and the caret-blink redraws. Structured like the Uno host's
/// (<c>KumikoUI.Uno</c>, #65).
/// </summary>
/// <remarks>
/// <para>
/// <b>Surface.</b> The grid draws on an internal <see cref="KumikoSurface"/>: SkiaSharp's <c>SKGLView</c>
/// (<see cref="KumikoRenderMode.Gpu"/>) or <c>SKCanvasView</c> (<see cref="KumikoRenderMode.Software"/>). Switching
/// <see cref="RenderMode"/> at run time removes the old view (disconnecting its handler, which releases its GPU
/// context), inserts the new one below the native text-input views, and redraws.
/// </para>
/// <para>
/// <b>Frame loop.</b> Every Core "redraw" signal goes through <see cref="RequestRedraw"/>. Where the platform folds
/// invalidations into its next frame (iOS, Mac Catalyst, Android software) the surface is invalidated directly; where
/// an invalidation paints synchronously (Windows) or records a frame (the Android GPU surface), requests wait for the
/// next platform frame callback (<see cref="KumikoFrameClock"/>: <c>CADisplayLink</c>, <c>Choreographer</c>,
/// <c>CompositionTarget.Rendering</c>) and paint once. A fling, a row drag's edge autoscroll and an editor's
/// animation step from the same callback by the measured time between frames, so a fling covers the same distance
/// at 60 and 120 Hz. The callback runs only while there is such work and stops on the first idle frame
/// (<see cref="KumikoFrameScheduler"/>).
/// </para>
/// <para>
/// <b>Caret.</b> While an editor or a filter popup's search box has focus, a one-shot timer redraws only at the
/// caret's blink transitions (<see cref="KumikoCaretBlink"/>), twice a blink cycle. Neither SkiaSharp view can
/// redraw a sub-rectangle, so on the software surface a blink copies the last full frame back
/// (<see cref="KumikoCaretFrame"/>) and redraws the grid clipped to the editor and popup area, which rasterizes only
/// that area. The GPU surfaces redraw the whole frame on a blink (the GPU rasterizes it).
/// </para>
/// </remarks>
public partial class KumikoDataGrid
{
    private readonly KumikoFrameScheduler _frames = new();
    private readonly KumikoCaretFrame _caretFrame = new();
    private KumikoSurface _surface = null!; // created by InitializeSurface from the constructor
    private KumikoSurfacePainter? _surfacePainter;
    private KumikoFrameRenderer? _frameRecorder;
    private KumikoFrameClock? _frameClock;
    private bool _surfaceConnected;

    // The next paint may redraw only the caret area: set by a blink, cleared by any other redraw request.
    private bool _caretOnlyRedraw;
    private bool _fullRedrawPending;

    private IDispatcherTimer? _caretBlinkTimer;
    private long _caretBlinkStart;

    // ── RenderMode ────────────────────────────────────────────────

    /// <summary>Identifies the <see cref="RenderMode"/> bindable property.</summary>
    public static readonly BindableProperty RenderModeProperty = BindableProperty.Create(
        nameof(RenderMode), typeof(KumikoRenderMode), typeof(KumikoDataGrid), KumikoRenderMode.Auto,
        propertyChanged: (b, _, n) => ((KumikoDataGrid)b).ApplyRenderMode((KumikoRenderMode)n));

    /// <summary>
    /// Gets or sets the surface the grid draws on. <see cref="KumikoRenderMode.Auto"/> (the default) rasterizes on
    /// the GPU (SkiaSharp's <c>SKGLView</c>) on Android and Mac Catalyst and on the CPU (<c>SKCanvasView</c>) on iOS
    /// and Windows; <see cref="KumikoRenderMode.Gpu"/> and <see cref="KumikoRenderMode.Software"/> ask for one.
    /// Asking for the GPU where SkiaSharp has no GPU view falls back to software; the surface in use is
    /// <see cref="ActiveRenderMode"/>. Changing it at run time replaces the surface and redraws. If a device's GPU
    /// driver draws the grid wrongly, set it to <see cref="KumikoRenderMode.Software"/>.
    /// </summary>
    public KumikoRenderMode RenderMode
    {
        get => (KumikoRenderMode)GetValue(RenderModeProperty);
        set => SetValue(RenderModeProperty, value);
    }

    /// <summary>
    /// Gets the surface the grid draws on now: <see cref="KumikoRenderMode.Gpu"/> or
    /// <see cref="KumikoRenderMode.Software"/>, never <see cref="KumikoRenderMode.Auto"/> (see <see cref="RenderMode"/>).
    /// Raises <see cref="BindableObject.PropertyChanged"/> when it changes.
    /// </summary>
    public KumikoRenderMode ActiveRenderMode => _surface.Mode;

    // ── Surface lifecycle ─────────────────────────────────────────

    /// <summary>The surface's size in device pixels as of its last paint (empty before the first one).</summary>
    private SKSize CanvasSize => _surface.CanvasSize;

    /// <summary>Creates the surface for the default <see cref="RenderMode"/>. Called once from the constructor.</summary>
    private void InitializeSurface()
    {
        _surfacePainter = PaintSurface;
        _frameRecorder = RecordFrame;
        ApplyRenderMode(RenderMode);
    }

    /// <summary>Creates (or swaps to) the surface for <paramref name="requested"/> and redraws.</summary>
    private void ApplyRenderMode(KumikoRenderMode requested)
    {
        var mode = KumikoSurfaceFactory.Resolve(requested);
        if (_surface is not null && _surface.Mode == mode) return;

        if (_surface is not null)
            RemoveSurface();

        _surface = KumikoSurfaceFactory.Create(mode);
        _canvasView = _surface.View;
        // Index 0: below anything else in the grid's root layout.
        _root.Children.Insert(0, _canvasView);
        if (!_wasCleanedUp)
            ConnectSurface();
        _frames.DeferInvalidation = _surface.PaintsOnInvalidate;

        OnPropertyChanged(nameof(ActiveRenderMode));
        RequestRedraw();
    }

    /// <summary>Takes the current surface out of the grid and releases its platform view.</summary>
    private void RemoveSurface()
    {
        var old = _surface;
        DisconnectSurface();
        _caretFrame.Clear();
        _root.Children.Remove(old.View);
        // Removing a view does not disconnect its handler; doing so releases the native view (and its GPU context).
        old.View.Handler?.DisconnectHandler();
        old.Dispose();
    }

    /// <summary>Subscribes to the surface's paint, touch and size events. Idempotent.</summary>
    private void ConnectSurface()
    {
        if (_surfaceConnected) return;
        _surfaceConnected = true;
        _surface.Attach(_surfacePainter!, _frameRecorder!);
        _surface.Touch += OnCanvasTouch;
        // A resize lays the grid out again; the Android GPU surface needs a new recording for it.
        _canvasView.SizeChanged += OnCanvasSizeChanged;
#if ANDROID
        _canvasView.HandlerChanged += OnCanvasHandlerChanged_Pointer;
        AttachNativePointer();
#endif
    }

    /// <summary>Removes what <see cref="ConnectSurface"/> subscribed. Idempotent.</summary>
    private void DisconnectSurface()
    {
        if (!_surfaceConnected) return;
        _surfaceConnected = false;
        _surface.Detach();
        _surface.Touch -= OnCanvasTouch;
        _canvasView.SizeChanged -= OnCanvasSizeChanged;
#if ANDROID
        _canvasView.HandlerChanged -= OnCanvasHandlerChanged_Pointer;
        DetachNativePointer();
#endif
    }

    /// <summary>Called when the grid is connected to a handler: draws, and resumes pending frame work.</summary>
    private void AttachRendering()
    {
        SyncReducedMotion(); // KumikoDataGrid.Motion.cs
        ConnectSurface();
        if (_frames.Attach())
            StartFrameClock();
        RequestRedraw();
    }

    /// <summary>
    /// Called from <c>Cleanup</c>: stops the frame loop and the caret timer and releases the surface callbacks, so
    /// nothing platform-side (the display link, the choreographer, a timer) roots the grid.
    /// </summary>
    private void DetachRendering()
    {
        _frames.Detach();
        _frameClock?.Dispose();
        _frameClock = null;
        ReleaseCursorBlinkTimer();
        DisconnectSurface();
        _caretFrame.Clear();
    }

    private void OnCanvasSizeChanged(object? sender, EventArgs e) => RequestRedraw();

    // ── Drawing ───────────────────────────────────────────────────

    /// <summary>
    /// Paints a frame on the UI thread (every surface except the Android GPU one). The surface is in device pixels;
    /// the grid draws in logical units scaled to them. A caret-blink frame on the software surface copies the last
    /// full frame back and redraws only the caret area (see the type remarks).
    /// </summary>
    private void PaintSurface(SKSurface surface, SKImageInfo info)
    {
        var canvas = surface.Canvas;
        double viewWidth = _canvasView.Width;
        float scale = viewWidth > 0 ? (float)(info.Width / viewWidth) : 1f;
        if (viewWidth > 0) RememberTouchScale(scale);
        float width = info.Width / scale;
        float height = info.Height / scale;

        bool caretOnly = _caretOnlyRedraw;
        _caretOnlyRedraw = false;
        _fullRedrawPending = false;

        if (caretOnly && !_renderer.ShowFrameStats && TryGetCaretArea(out var area))
        {
            var clip = KumikoCaretFrame.ToDeviceClip(area, scale, info.Width, info.Height);
            if (!clip.IsEmpty && _caretFrame.TryRestore(canvas, info))
            {
                canvas.Save();
                canvas.ClipRect(clip);
                DrawFrame(canvas, scale, width, height);
                canvas.Restore();
                return;
            }

            // The first blink after a change draws everything and keeps a copy for the next blinks.
            DrawFrame(canvas, scale, width, height);
            _caretFrame.Capture(surface, info);
            return;
        }

        _caretFrame.Clear();
        DrawFrame(canvas, scale, width, height);
    }

    /// <summary>Clears to the background (within the current clip) and draws the grid scaled to device pixels.</summary>
    private void DrawFrame(SKCanvas canvas, float scale, float width, float height)
    {
        var bg = _style.BackgroundColor;
        canvas.Clear(new SKColor(bg.R, bg.G, bg.B, bg.A));
        canvas.Save();
        canvas.Scale(scale);
        RenderFrame(canvas, width, height);
        canvas.Restore();
    }

    /// <summary>Records a frame for the Android GPU surface, on the UI thread, in logical units.</summary>
    private void RecordFrame(SKCanvas canvas, float width, float height)
    {
        _caretOnlyRedraw = false;
        _fullRedrawPending = false;
        var bg = _style.BackgroundColor;
        canvas.Clear(new SKColor(bg.R, bg.G, bg.B, bg.A));
        RenderFrame(canvas, width, height);
    }

    /// <summary>Draws one frame on a canvas in logical units (DIPs) of the given logical size.</summary>
    private void RenderFrame(SKCanvas canvas, float width, float height)
    {
        _scroll.ViewportWidth = width;
        _scroll.ViewportHeight = height;

        using var drawingContext = ResourceCache.CreateContext(canvas);

        _renderer.RenderFrame(drawingContext, _dataSource, _scroll, _selection, _style,
            _inputController.DragColumnIndex, _inputController.DragColumnScreenX,
            _inputController.DragRowIndex, _inputController.DragRowScreenY,
            _editSession, _inputController.PopupManager);
        // The frame may have started a transition (the current row's highlight fading in), or one runs on: the frame
        // loop steps it
        if (_inputController.IsAnimating)
            StartFrameAnimation();
        OnFrameRendered();
    }

    /// <summary>
    /// Called on the UI thread after each frame is drawn or recorded, whatever the surface; the editor is then
    /// placed over its cell. Android's text input checks the soft keyboard against the edited cell from here.
    /// </summary>
    partial void OnFrameRendered();

    /// <summary>
    /// The area a caret can blink in, in logical units: the open editor and every visible popup (a filter popup's
    /// search box, an editor's drop-down), as placed by the last frame. False when there is none on screen.
    /// </summary>
    private bool TryGetCaretArea(out KumikoRect area)
    {
        area = KumikoRect.Empty;
        if (_editSession is { IsEditing: true, ActiveEditor: { IsVisible: true } editor })
            area = editor.Bounds;
        if (_inputController.PopupManager.TryGetVisibleBounds(out var popups))
            area = KumikoCaretFrame.Union(area, popups);
        return area.Width > 0 && area.Height > 0;
    }

    /// <summary>
    /// Requests a redraw of the whole grid; requests coalesce into one paint per frame. UI thread only — see
    /// <see cref="Invalidate"/> for any thread.
    /// </summary>
    private void RequestRedraw()
    {
        _caretOnlyRedraw = false;
        _fullRedrawPending = true;
        ScheduleRedraw();
    }

    /// <summary>Requests the redraw of a caret blink: only the caret area, unless something else changed too.</summary>
    private void RequestCaretRedraw()
    {
        if (!_fullRedrawPending)
            _caretOnlyRedraw = true;
        ScheduleRedraw();
    }

    private void ScheduleRedraw()
    {
        switch (_frames.RequestRedraw())
        {
            case KumikoRedrawAction.InvalidateNow:
                _surface.Invalidate();
                break;
            case KumikoRedrawAction.StartFrameLoop:
                StartFrameClock();
                break;
        }
    }

    /// <summary>Invalidate the canvas surface to trigger a repaint.</summary>
    private void InvalidateSurface() => RequestRedraw();

    // ── Frame loop ────────────────────────────────────────────────

    /// <summary>
    /// Starts stepping the inertial scroller, edge autoscroll, the editor's animation and Core's transitions once per
    /// frame (a fling or a scroll picker coasting after a release, a row drag held at an edge, a spin button held down,
    /// a group sliding open).
    /// </summary>
    private void StartFrameAnimation()
    {
        if (_frames.StartAnimation())
            StartFrameClock();
    }

    /// <summary>Ends the frame animation; the loop stops on its next idle frame.</summary>
    private void StopFrameAnimation() => _frames.StopAnimation();

    private void StartFrameClock() => (_frameClock ??= new KumikoFrameClock(Dispatcher, OnFrame)).Start();

    /// <summary>
    /// One frame: advance the animations by the time since the previous frame, paint a deferred redraw once, and
    /// stop the loop when nothing is left to do. Allocation-free.
    /// </summary>
    private void OnFrame(TimeSpan frameTime)
    {
        if (!_frames.IsRunning) return;

        float frameMs = _frames.BeginFrame(frameTime);

        // UpdateInertialScroll and UpdateAutoScroll scroll and raise NeedsRedraw (→ RequestRedraw) on every step that
        // moves. The open editor's animation (a scroll picker settling, which may commit the edit, or a held spin
        // button repeating) runs on the same loop, which stops once none of them moves.
        if (_frames.IsAnimating)
        {
            bool scrolling = _inputController.UpdateInertialScroll(_scroll, frameMs);
            bool autoScrolling = _inputController.UpdateAutoScroll(_scroll, frameMs);
            bool animating = _editSession.UpdateAnimation(frameMs);
            bool transitioning = _inputController.UpdateAnimations(frameMs); // KumikoDataGrid.Motion.cs
            if (!scrolling && !autoScrolling && !animating && !transitioning)
                _frames.StopAnimation();
        }

        if (_frames.TakePendingRedraw())
            _surface.Invalidate();

        if (_frames.EndFrame())
            _frameClock?.Stop();
    }

    // ── Caret blink ───────────────────────────────────────────────

    /// <summary>
    /// Starts redrawing at the caret's blink transitions while an editor or a filter popup's search box has focus.
    /// The blink restarts when the editor takes focus and when text is typed, so the schedule restarts too.
    /// </summary>
    private void StartCursorBlinkTimer()
    {
        if (_caretBlinkTimer is null)
        {
            _caretBlinkTimer = Dispatcher.CreateTimer();
            _caretBlinkTimer.IsRepeating = false;
            _caretBlinkTimer.Tick += OnCaretBlinkTick;
        }

        _caretBlinkTimer.Stop();
        _caretBlinkStart = Stopwatch.GetTimestamp();
        _caretBlinkTimer.Interval = TimeSpan.FromMilliseconds(KumikoCaretBlink.DelayToNextTransition(0));
        _caretBlinkTimer.Start();
    }

    /// <summary>Restarts the blink schedule after a key, which restarts the caret's blink, while it is running.</summary>
    private void RestartCursorBlinkTimer()
    {
        if (_caretBlinkTimer is { IsRunning: true } && (_editSession.IsEditing || _filterPopupActive))
            StartCursorBlinkTimer();
    }

    private void StopCursorBlinkTimer() => _caretBlinkTimer?.Stop();

    /// <summary>Stops the blink timer and drops it, so its tick handler no longer roots the grid.</summary>
    private void ReleaseCursorBlinkTimer()
    {
        if (_caretBlinkTimer is null) return;
        _caretBlinkTimer.Stop();
        _caretBlinkTimer.Tick -= OnCaretBlinkTick;
        _caretBlinkTimer = null;
    }

    private void OnCaretBlinkTick(object? sender, EventArgs e)
    {
        if (!_editSession.IsEditing && !_filterPopupActive)
        {
            StopCursorBlinkTimer();
            return;
        }

        RequestCaretRedraw();

        // Re-arm for just after the next transition, measured from the blink's start so drift does not build up.
        var timer = _caretBlinkTimer!;
        long elapsedMs = (long)Stopwatch.GetElapsedTime(_caretBlinkStart).TotalMilliseconds;
        timer.Interval = TimeSpan.FromMilliseconds(KumikoCaretBlink.DelayToNextTransition(elapsedMs));
        timer.Start();
    }
}

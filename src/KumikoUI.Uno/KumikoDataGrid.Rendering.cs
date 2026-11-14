using System.Diagnostics;
using KumikoUI.SkiaSharp;
using KumikoUI.Uno.Rendering;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;

namespace KumikoUI.Uno;

/// <summary>
/// Rendering for <see cref="KumikoDataGrid"/> (#65): the drawing surface (<see cref="RenderMode"/>), the frame loop
/// that drives redraws and inertial scrolling, the caret-blink redraws, and display-scale (DPI) changes.
/// </summary>
/// <remarks>
/// <para>
/// <b>Surface.</b> The grid draws on an internal <see cref="IKumikoSurface"/>: Uno's <c>SKCanvasElement</c>
/// (<see cref="KumikoRenderMode.Hardware"/>, drawn straight into the window's canvas, in logical units) where the
/// platform supports it, otherwise an <c>SKXamlCanvas</c> (<see cref="KumikoRenderMode.Software"/>, a device-pixel
/// bitmap scaled to logical units and copied to the screen; the only surface on WinAppSDK).
/// </para>
/// <para>
/// <b>Frame loop.</b> Every Core "redraw" signal goes through <see cref="RequestRedraw"/>. The hardware surface
/// coalesces invalidations into the next frame by itself; the software surface paints synchronously on every
/// invalidation, so its requests wait for the next <see cref="CompositionTarget.Rendering"/> callback and paint once.
/// A fling steps the inertial scroller from the same callback, by the measured time between frames. The callback is
/// subscribed only while there is such work and removed on the first idle frame (<see cref="KumikoFrameScheduler"/>),
/// because a <c>Rendering</c> subscription keeps the compositor producing frames.
/// </para>
/// <para>
/// <b>Caret.</b> Neither surface can redraw a sub-rectangle, so while an editor or a filter popup's search box has
/// focus a one-shot timer redraws only at the caret's blink transitions (<see cref="KumikoCaretBlink"/>), twice a
/// blink cycle, instead of every frame.
/// </para>
/// <para>
/// <b>DPI.</b> <c>XamlRoot.Changed</c> is observed while loaded; a rasterization-scale change redraws the grid. The
/// hardware surface follows the scale through the window transform; the software surface maps its bitmap's pixels to
/// its own logical size (not the grid's) each paint.
/// </para>
/// </remarks>
public partial class KumikoDataGrid
{
    private static readonly long s_frameClockStart = Stopwatch.GetTimestamp();

    private readonly KumikoFrameScheduler _frames = new();
    private IKumikoSurface _surface = null!; // created by InitializeSurface from the constructor
    private KumikoSurfaceRenderer? _surfaceRenderer;
    private EventHandler<object>? _renderingHandler;
    private bool _frameLoopSubscribed;

    private XamlRoot? _observedXamlRoot;
    private double _rasterizationScale;

    private DispatcherTimer? _caretBlinkTimer;
    private long _caretBlinkStart;
    private bool _filterPopupActive;

    // ── RenderMode ────────────────────────────────────────────────

    /// <summary>Identifies the <see cref="RenderMode"/> dependency property.</summary>
    public static readonly DependencyProperty RenderModeProperty =
        DependencyProperty.Register(
            nameof(RenderMode), typeof(KumikoRenderMode), typeof(KumikoDataGrid),
            new PropertyMetadata(KumikoRenderMode.Auto, OnRenderModeChanged));

    /// <summary>
    /// Gets or sets the surface the grid draws on. <see cref="KumikoRenderMode.Auto"/> (the default) uses
    /// <see cref="KumikoRenderMode.Hardware"/> (Uno's <c>SKCanvasElement</c>: drawn straight into the window's
    /// canvas, GPU-accelerated where the window is) on the Skia-rendered heads and <see cref="KumikoRenderMode.Software"/>
    /// (<c>SKXamlCanvas</c>: a bitmap copied to the screen) elsewhere, including WinAppSDK. Asking for
    /// <see cref="KumikoRenderMode.Hardware"/> where it is unavailable falls back to software; the surface in use is
    /// <see cref="ActiveRenderMode"/>. Changing it at run time swaps the surface and redraws.
    /// </summary>
    public KumikoRenderMode RenderMode
    {
        get => (KumikoRenderMode)GetValue(RenderModeProperty);
        set => SetValue(RenderModeProperty, value);
    }

    /// <summary>
    /// Gets the surface the grid draws on now: <see cref="KumikoRenderMode.Hardware"/> or
    /// <see cref="KumikoRenderMode.Software"/>, never <see cref="KumikoRenderMode.Auto"/> (see <see cref="RenderMode"/>).
    /// </summary>
    public KumikoRenderMode ActiveRenderMode => _surface.Mode;

    private static void OnRenderModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((KumikoDataGrid)d).ApplyRenderMode((KumikoRenderMode)e.NewValue);

    // ── Surface lifecycle ─────────────────────────────────────────

    /// <summary>Creates the surface for the default <see cref="RenderMode"/>. Called once from the constructor.</summary>
    private void InitializeSurface()
    {
        _surfaceRenderer = RenderFrame;
        _renderingHandler = OnCompositionTargetRendering;
        ApplyRenderMode(RenderMode);
    }

    /// <summary>Creates (or swaps to) the surface for <paramref name="requested"/> and redraws.</summary>
    private void ApplyRenderMode(KumikoRenderMode requested)
    {
        var mode = KumikoSurfaceFactory.Resolve(requested, KumikoSurfaceFactory.IsHardwareSupported);
        if (_surface is not null && _surface.Mode == mode) return;

        if (_surface is not null)
        {
            _surface.Detach();
            _root.Children.Remove(_surface.Element);
        }

        _surface = KumikoSurfaceFactory.Create(mode);
        // Index 0: below the keyboard proxy, which must stay on top in the z-order.
        _root.Children.Insert(0, _surface.Element);
        _surface.Attach(_surfaceRenderer!);
        _frames.DeferInvalidation = _surface.PaintsOnInvalidate;
        RequestRedraw();
    }

    /// <summary>Called from <c>OnLoaded</c>: resumes drawing, DPI tracking and any pending frame work.</summary>
    private void AttachSurface()
    {
        _surface.Attach(_surfaceRenderer!);
        AttachXamlRoot();
        if (_frames.Attach())
            SubscribeFrameLoop();
        // Re-render with the current DPI now that we are in the visual tree.
        RequestRedraw();
    }

    /// <summary>
    /// Called from <c>OnUnloaded</c>: stops the frame loop, the caret timer and DPI tracking and releases the
    /// surface's renderer callback, so nothing static (the frame callback) roots this control.
    /// </summary>
    private void DetachSurface()
    {
        if (_frames.Detach())
            UnsubscribeFrameLoop();
        StopCursorBlinkTimer();
        DetachXamlRoot();
        _surface.Detach();
    }

    // ── Drawing ───────────────────────────────────────────────────

    /// <summary>
    /// Draws one frame. Both surfaces hand over a canvas in logical units (DIPs) and their logical size, so the
    /// renderer is fed logical units as on MAUI.
    /// </summary>
    private void RenderFrame(SKCanvas canvas, float width, float height)
    {
        _scroll.ViewportWidth = width;
        _scroll.ViewportHeight = height;

        // Paint the background over what is below (never Clear: on the hardware surface the canvas is the window's).
        var bg = _style.BackgroundColor;
        if (bg.A > 0)
            canvas.DrawColor(new SKColor(bg.R, bg.G, bg.B, bg.A), SKBlendMode.SrcOver);

        using var drawingContext = ResourceCache.CreateContext(canvas);

        _renderer.Render(drawingContext, _dataSource, _scroll, _selection, _style,
            _inputController.DragColumnIndex, _inputController.DragColumnScreenX,
            _inputController.DragRowIndex, _inputController.DragRowScreenY,
            _editSession, _inputController.PopupManager);
    }

    /// <summary>
    /// Requests a redraw of the whole grid; requests coalesce into one paint per frame. UI thread only — see
    /// <see cref="Invalidate"/> for any thread.
    /// </summary>
    private void RequestRedraw()
    {
        switch (_frames.RequestRedraw())
        {
            case KumikoRedrawAction.InvalidateNow:
                _surface.Invalidate();
                break;
            case KumikoRedrawAction.StartFrameLoop:
                SubscribeFrameLoop();
                break;
        }
    }

    /// <summary>Invalidate the surface to trigger a repaint (MAUI: <c>InvalidateSurface()</c>).</summary>
    private void InvalidateSurface() => RequestRedraw();

    // ── Frame loop (CompositionTarget.Rendering) ──────────────────

    /// <summary>
    /// Starts stepping the inertial scroller and edge autoscroll once per frame (a fling began on release or cancel,
    /// or a row drag holds the pointer at an edge).
    /// </summary>
    private void StartInertialScroll()
    {
        if (_frames.StartAnimation())
            SubscribeFrameLoop();
    }

    private void SubscribeFrameLoop()
    {
        if (_frameLoopSubscribed) return;
        _frameLoopSubscribed = true;
        CompositionTarget.Rendering += _renderingHandler;
    }

    private void UnsubscribeFrameLoop()
    {
        if (!_frameLoopSubscribed) return;
        _frameLoopSubscribed = false;
        CompositionTarget.Rendering -= _renderingHandler;
    }

    /// <summary>
    /// One frame: advance the fling by the time since the previous frame, paint a deferred redraw once, and stop the
    /// loop when nothing is left to do. Allocation-free.
    /// </summary>
    private void OnCompositionTargetRendering(object? sender, object e)
    {
        if (!_frameLoopSubscribed) return;

        TimeSpan now = e is RenderingEventArgs args
            ? args.RenderingTime
            : Stopwatch.GetElapsedTime(s_frameClockStart);
        float frameMs = _frames.BeginFrame(now);

        // UpdateInertialScroll and UpdateAutoScroll scroll and raise NeedsRedraw (→ RequestRedraw) on every step that
        // moves. The open editor's animation (a scroll picker settling, a held spin button repeating) runs on the same
        // loop, which stops once none of them moves.
        if (_frames.IsAnimating)
        {
            bool scrolling = _inputController.UpdateInertialScroll(_scroll, frameMs);
            bool autoScrolling = _inputController.UpdateAutoScroll(_scroll, frameMs);
            bool animating = _editSession.UpdateAnimation(frameMs);
            if (!scrolling && !autoScrolling && !animating)
                _frames.StopAnimation();
        }

        if (_frames.TakePendingRedraw())
            _surface.Invalidate();

        if (_frames.EndFrame())
            UnsubscribeFrameLoop();
    }

    // ── Caret blink ───────────────────────────────────────────────

    /// <summary>
    /// Starts redrawing at the caret's blink transitions while an editor or a filter popup's search box has focus.
    /// The blink restarts with the editor's focus, so the schedule restarts too.
    /// </summary>
    private void StartCursorBlinkTimer()
    {
        if (_caretBlinkTimer is null)
        {
            _caretBlinkTimer = new DispatcherTimer();
            _caretBlinkTimer.Tick += OnCaretBlinkTick;
        }

        _caretBlinkTimer.Stop();
        _caretBlinkStart = Stopwatch.GetTimestamp();
        _caretBlinkTimer.Interval = TimeSpan.FromMilliseconds(KumikoCaretBlink.DelayToNextTransition(0));
        _caretBlinkTimer.Start();
    }

    private void StopCursorBlinkTimer() => _caretBlinkTimer?.Stop();

    private void OnCaretBlinkTick(object? sender, object e)
    {
        if (!_editSession.IsEditing && !_filterPopupActive)
        {
            StopCursorBlinkTimer();
            return;
        }

        RequestRedraw();

        // Re-arm for just after the next transition, measured from the blink's start so drift does not build
        // up. Stop/Start so the new interval applies to this very wait on every platform.
        var timer = _caretBlinkTimer!;
        long elapsedMs = (long)Stopwatch.GetElapsedTime(_caretBlinkStart).TotalMilliseconds;
        timer.Stop();
        timer.Interval = TimeSpan.FromMilliseconds(KumikoCaretBlink.DelayToNextTransition(elapsedMs));
        timer.Start();
    }

    // ── DPI ───────────────────────────────────────────────────────

    private void AttachXamlRoot()
    {
        var root = XamlRoot;
        if (ReferenceEquals(root, _observedXamlRoot)) return;
        DetachXamlRoot();
        if (root is null) return;

        _observedXamlRoot = root;
        _rasterizationScale = root.RasterizationScale;
        root.Changed += OnXamlRootChanged;
    }

    private void DetachXamlRoot()
    {
        if (_observedXamlRoot is null) return;
        _observedXamlRoot.Changed -= OnXamlRootChanged;
        _observedXamlRoot = null;
    }

    /// <summary>
    /// The window moved to a display with another scale (or the scale setting changed): redraw at the new
    /// resolution. Size and visibility changes also raise <c>Changed</c>; the surfaces redraw on their own resize.
    /// </summary>
    private void OnXamlRootChanged(XamlRoot sender, XamlRootChangedEventArgs args)
    {
        double scale = sender.RasterizationScale;
        if (!KumikoSurfaceMetrics.ScaleChanged(_rasterizationScale, scale)) return;
        _rasterizationScale = scale;
        RequestRedraw();
    }

    // ── Core redraw signal handlers ───────────────────────────────
    // Named handlers (not lambdas) so they unsubscribe cleanly.

    private void OnDataSourceViewChanged(object? sender, KumikoUI.Core.Models.KumikoViewChangedEventArgs e) => RequestRedraw();

    private void OnInputControllerNeedsRedraw() => RequestRedraw();

    private void OnEditSessionNeedsRedraw() => RequestRedraw();

    // An action button's command raised CanExecuteChanged: that can happen on any thread.
    private void OnRendererRedrawRequested() => Invalidate();
}

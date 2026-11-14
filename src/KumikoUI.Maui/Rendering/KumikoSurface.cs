using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace KumikoUI.Maui.Rendering;

/// <summary>
/// Paints one whole frame into <paramref name="surface"/>, on the UI thread. <paramref name="info"/> is the surface's
/// size in device pixels.
/// </summary>
internal delegate void KumikoSurfacePainter(SKSurface surface, SKImageInfo info);

/// <summary>
/// Draws one frame on <paramref name="canvas"/>, which is in logical units (DIPs) with its origin at the surface's
/// top-left corner; <paramref name="width"/> and <paramref name="height"/> are the surface's logical size. Called on
/// the UI thread.
/// </summary>
internal delegate void KumikoFrameRenderer(SKCanvas canvas, float width, float height);

/// <summary>
/// The view a <see cref="KumikoDataGrid"/> draws on (#71): <see cref="KumikoCanvasViewSurface"/> (SkiaSharp's
/// <c>SKCanvasView</c>, <see cref="KumikoRenderMode.Software"/>) or <see cref="KumikoGpuViewSurface"/>
/// (<c>SKGLView</c>, <see cref="KumikoRenderMode.Hardware"/>). <see cref="KumikoSurfaceFactory"/> picks one.
/// </summary>
/// <remarks>
/// Neither view can redraw a sub-rectangle: every paint covers the whole surface. The grid's caret blink therefore
/// redraws only the editor's area over a cached frame on the software surface (see <see cref="KumikoCaretFrame"/>).
/// </remarks>
internal abstract class KumikoSurface : IDisposable
{
    /// <summary>The MAUI view to add to the grid's layout; it fills the grid and raises the touch events.</summary>
    public abstract View View { get; }

    /// <summary>The resolved mode: <see cref="KumikoRenderMode.Software"/> or <see cref="KumikoRenderMode.Hardware"/>.</summary>
    public abstract KumikoRenderMode Mode { get; }

    /// <summary>
    /// True when <see cref="Invalidate"/> paints (or records) synchronously instead of scheduling a frame, so the
    /// host coalesces redraw requests into one per frame itself. False when the platform already coalesces them.
    /// </summary>
    public abstract bool PaintsOnInvalidate { get; }

    /// <summary>The surface's size in device pixels as of its last paint; empty before the first one.</summary>
    public abstract SKSize CanvasSize { get; }

    /// <summary>Touches, mouse and pen input on the surface, in device pixels.</summary>
    public abstract event EventHandler<SKTouchEventArgs>? Touch;

    /// <summary>Starts drawing frames through the host's callbacks. Idempotent.</summary>
    /// <param name="painter">Paints a frame on the UI thread (the surfaces that draw there).</param>
    /// <param name="renderer">Draws a frame in logical units, for a surface that records frames for another thread.</param>
    public abstract void Attach(KumikoSurfacePainter painter, KumikoFrameRenderer renderer);

    /// <summary>Stops drawing frames and releases the host's callbacks. Idempotent.</summary>
    public abstract void Detach();

    /// <summary>Redraws the whole surface.</summary>
    public abstract void Invalidate();

    /// <summary>Releases what the surface holds beyond its view (a recorded frame). The view itself is not disposed.</summary>
    public virtual void Dispose()
    {
    }

    /// <summary>Applies the settings every surface view shares.</summary>
    protected static void Configure(View view)
    {
        view.HorizontalOptions = LayoutOptions.Fill;
        view.VerticalOptions = LayoutOptions.Fill;
        // The grid draws left-to-right whatever its ancestors' direction (RTL is unsupported in 1.0, #80, #228)
        view.FlowDirection = FlowDirection.LeftToRight;
    }
}

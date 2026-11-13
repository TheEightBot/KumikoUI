using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace KumikoUI.Maui.Rendering;

/// <summary>
/// The <see cref="KumikoRenderMode.Gpu"/> surface: SkiaSharp's <see cref="SKGLView"/>, rasterized on the GPU. Its
/// platform view is an OpenGL ES <c>TextureView</c> on Android, a GLKit view on iOS, a Metal view on Mac Catalyst and
/// an ANGLE swap chain panel on Windows.
/// </summary>
/// <remarks>
/// <para>
/// On iOS, Mac Catalyst and Windows the view paints on the UI thread, like the software surface. On Windows each
/// <c>InvalidateSurface</c> repaints synchronously, so there the host coalesces requests itself
/// (<see cref="PaintsOnInvalidate"/>).
/// </para>
/// <para>
/// On Android the view paints on its own GL thread, while the grid's state may only be read on the UI thread. So
/// <see cref="Invalidate"/> records the frame into an <see cref="SKPicture"/> on the UI thread (the layout walk and
/// the draw calls) and the GL thread only plays the latest picture back (the rasterization). Recording paints
/// synchronously, so the host coalesces requests into one recording per frame (<see cref="PaintsOnInvalidate"/>).
/// A picture is disposed by whichever thread is the last to use it.
/// </para>
/// </remarks>
internal sealed class KumikoGpuViewSurface : KumikoSurface
{
    private readonly SKGLView _view = new() { EnableTouchEvents = true, IgnorePixelScaling = false };
    private bool _attached;

#if !ANDROID
    private KumikoSurfacePainter? _painter;
#else
    private readonly object _gate = new();
    private SKPictureRecorder? _recorder;
    private KumikoFrameRenderer? _renderer;
    private SKPicture? _latest;   // the newest recorded frame (UI thread writes, GL thread reads)
    private SKPicture? _drawing;  // the picture the GL thread is playing back now
    private SKPicture? _retired;  // replaced while it was being played back; the GL thread disposes it
#endif

    /// <summary>Creates the view, stretched to fill the grid.</summary>
    public KumikoGpuViewSurface() => Configure(_view);

    /// <inheritdoc />
    public override View View => _view;

    /// <inheritdoc />
    public override KumikoRenderMode Mode => KumikoRenderMode.Gpu;

    /// <inheritdoc />
    public override bool PaintsOnInvalidate =>
#if WINDOWS || ANDROID
        true;
#else
        false;
#endif

    /// <inheritdoc />
    public override SKSize CanvasSize => _view.CanvasSize;

    /// <inheritdoc />
    public override event EventHandler<SKTouchEventArgs>? Touch
    {
        add => _view.Touch += value;
        remove => _view.Touch -= value;
    }

    /// <inheritdoc />
    public override void Attach(KumikoSurfacePainter painter, KumikoFrameRenderer renderer)
    {
#if ANDROID
        _renderer = renderer;
#else
        _painter = painter;
#endif
        if (_attached) return;
        _attached = true;
        _view.PaintSurface += OnPaintSurface;
    }

    /// <inheritdoc />
    public override void Detach()
    {
#if ANDROID
        _renderer = null;
#else
        _painter = null;
#endif
        if (!_attached) return;
        _attached = false;
        _view.PaintSurface -= OnPaintSurface;
    }

    /// <inheritdoc />
    public override void Invalidate()
    {
#if ANDROID
        RecordFrame();
#endif
        _view.InvalidateSurface();
    }

#if ANDROID
    /// <inheritdoc />
    public override void Dispose()
    {
        lock (_gate)
        {
            Retire(_latest);
            _latest = null;
        }
        _recorder?.Dispose();
        _recorder = null;
    }

    /// <summary>Records the grid's current frame on the UI thread and publishes it to the GL thread.</summary>
    private void RecordFrame()
    {
        if (_renderer is not { } renderer) return;
        float width = (float)_view.Width, height = (float)_view.Height;
        if (!(width > 0 && height > 0)) return; // not laid out yet; the first layout requests a redraw

        _recorder ??= new SKPictureRecorder();
        var canvas = _recorder.BeginRecording(SKRect.Create(width, height));
        renderer(canvas, width, height);
        var picture = _recorder.EndRecording();

        lock (_gate)
        {
            Retire(_latest);
            _latest = picture;
        }
    }

    /// <summary>Disposes a replaced picture now, or after the GL thread finishes playing it back. Under the gate.</summary>
    private void Retire(SKPicture? picture)
    {
        if (picture is null) return;
        if (ReferenceEquals(picture, _drawing))
            _retired = picture;
        else
            picture.Dispose();
    }

    // GL thread: play back the latest recorded frame, scaled from logical units to the surface's pixels.
    private void OnPaintSurface(object? sender, SKPaintGLSurfaceEventArgs e)
    {
        SKPicture? picture;
        lock (_gate)
        {
            picture = _latest;
            _drawing = picture;
        }

        try
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.Transparent);
            if (picture is not null)
            {
                var cull = picture.CullRect;
                if (cull.Width > 0)
                {
                    float scale = e.Info.Width / cull.Width;
                    canvas.Save();
                    canvas.Scale(scale);
                    canvas.DrawPicture(picture);
                    canvas.Restore();
                }
            }
        }
        finally
        {
            lock (_gate)
            {
                _drawing = null;
                _retired?.Dispose();
                _retired = null;
            }
        }
    }
#else
    private void OnPaintSurface(object? sender, SKPaintGLSurfaceEventArgs e) => _painter?.Invoke(e.Surface, e.Info);
#endif
}

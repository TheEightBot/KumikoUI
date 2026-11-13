using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace KumikoUI.Maui.Rendering;

/// <summary>
/// The <see cref="KumikoRenderMode.Software"/> surface: SkiaSharp's <see cref="SKCanvasView"/>, which rasterizes on
/// the CPU into a bitmap and hands it to the platform. Paints on the UI thread.
/// </summary>
/// <remarks>
/// <c>InvalidateSurface</c> marks the native view dirty on iOS, Mac Catalyst and Android, where the platform folds any
/// number of requests into one paint. On Windows it queues a synchronous repaint per call, so there the host
/// coalesces requests itself (<see cref="PaintsOnInvalidate"/>).
/// </remarks>
internal sealed class KumikoCanvasViewSurface : KumikoSurface
{
    private readonly SKCanvasView _view = new() { EnableTouchEvents = true, IgnorePixelScaling = false };
    private KumikoSurfacePainter? _painter;
    private bool _attached;

    /// <summary>Creates the view, stretched to fill the grid.</summary>
    public KumikoCanvasViewSurface() => Configure(_view);

    /// <inheritdoc />
    public override View View => _view;

    /// <inheritdoc />
    public override KumikoRenderMode Mode => KumikoRenderMode.Software;

    /// <inheritdoc />
    public override bool PaintsOnInvalidate =>
#if WINDOWS
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
        _painter = painter;
        if (_attached) return;
        _attached = true;
        _view.PaintSurface += OnPaintSurface;
    }

    /// <inheritdoc />
    public override void Detach()
    {
        _painter = null;
        if (!_attached) return;
        _attached = false;
        _view.PaintSurface -= OnPaintSurface;
    }

    /// <inheritdoc />
    public override void Invalidate() => _view.InvalidateSurface();

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e) => _painter?.Invoke(e.Surface, e.Info);
}

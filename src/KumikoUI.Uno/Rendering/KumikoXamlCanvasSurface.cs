using Microsoft.UI.Xaml;
using SkiaSharp;
using SkiaSharp.Views.Windows; // SKXamlCanvas + SKPaintSurfaceEventArgs

namespace KumikoUI.Uno.Rendering;

/// <summary>
/// The <see cref="KumikoRenderMode.Software"/> surface: an <see cref="SKXamlCanvas"/>, which paints into a bitmap
/// sized in device pixels and copies it to the screen. Works on every head and is the only surface on WinAppSDK.
/// </summary>
/// <remarks>
/// <see cref="SKXamlCanvas.Invalidate"/> paints synchronously on the UI thread (bitmap, Skia surface and copy each
/// time), so <see cref="PaintsOnInvalidate"/> is true and the host routes every redraw request through its frame
/// loop, where they coalesce into one paint per frame. The canvas is in device pixels: one
/// <see cref="SKCanvas.Scale(float, float)"/> by <see cref="KumikoSurfaceMetrics"/> maps it to the logical units
/// the renderer works in, measured against this element's own size (not the grid's, UNO-18).
/// </remarks>
internal sealed class KumikoXamlCanvasSurface : IKumikoSurface
{
    private readonly SKXamlCanvas _canvas = new()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch,
        // The grid draws left-to-right whatever its ancestors' direction (RTL is unsupported in 1.0, #80, #228).
        FlowDirection = FlowDirection.LeftToRight,
    };

    private KumikoSurfaceRenderer? _renderer;

    /// <inheritdoc />
    public FrameworkElement Element => _canvas;

    /// <inheritdoc />
    public KumikoRenderMode Mode => KumikoRenderMode.Software;

    /// <inheritdoc />
    public bool PaintsOnInvalidate => true;

    /// <inheritdoc />
    public void Attach(KumikoSurfaceRenderer renderer)
    {
        _renderer = renderer;
        _canvas.PaintSurface -= OnPaintSurface;
        _canvas.PaintSurface += OnPaintSurface;
    }

    /// <inheritdoc />
    public void Detach()
    {
        _canvas.PaintSurface -= OnPaintSurface;
        _renderer = null;
    }

    /// <inheritdoc />
    public void Invalidate() => _canvas.Invalidate();

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        // The bitmap is reused between paints: start from transparent so a translucent background composes the
        // same way every frame.
        canvas.Clear(SKColors.Transparent);

        var renderer = _renderer;
        if (renderer is null) return;

        double scale = _canvas.XamlRoot?.RasterizationScale ?? 1.0;
        var metrics = KumikoSurfaceMetrics.FromPixels(
            e.Info.Width, e.Info.Height, _canvas.ActualWidth, _canvas.ActualHeight, scale);
        if (metrics.IsEmpty) return;

        canvas.Save();
        canvas.Scale(metrics.ScaleX, metrics.ScaleY);
        renderer(canvas, metrics.Width, metrics.Height);
        canvas.Restore();
    }
}

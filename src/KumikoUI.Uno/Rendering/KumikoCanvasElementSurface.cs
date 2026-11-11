#if HAS_UNO
using Microsoft.UI.Xaml;
using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;

namespace KumikoUI.Uno.Rendering;

/// <summary>
/// The <see cref="KumikoRenderMode.Hardware"/> surface: an Uno <see cref="SKCanvasElement"/> that draws straight into
/// the window's own Skia canvas while the compositor records the frame, so there is no intermediate bitmap and no
/// copy, and the frame is GPU-rasterized wherever Uno renders the window on the GPU. Only constructed where
/// <see cref="SKCanvasElement.IsSupportedOnCurrentPlatform"/> is true (the Skia-rendered heads).
/// </summary>
/// <remarks>
/// <para>
/// The canvas handed to <see cref="RenderOverride"/> already carries the window's transform, including its
/// rasterization scale: it is in logical units with the origin at this element's top-left corner, clipped to
/// <c>area</c>. So there is no scale math here, and a display-scale change re-renders at the new resolution by itself.
/// </para>
/// <para>
/// <see cref="SKCanvasElement.Invalidate"/> only marks the element's visual dirty and asks for a frame; any number
/// of calls before the next frame produce one render, so <see cref="PaintsOnInvalidate"/> is false and the host
/// invalidates directly. The canvas is the window's: nothing here may clear it.
/// </para>
/// </remarks>
internal sealed class KumikoCanvasElementSurface : SKCanvasElement, IKumikoSurface
{
    private KumikoSurfaceRenderer? _renderer;

    /// <summary>Creates the element, stretched to fill the grid.</summary>
    public KumikoCanvasElementSurface()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        // The grid draws left-to-right whatever its ancestors' direction (RTL is unsupported in 1.0, #80, #228).
        FlowDirection = FlowDirection.LeftToRight;
    }

    /// <inheritdoc />
    public FrameworkElement Element => this;

    /// <inheritdoc />
    public KumikoRenderMode Mode => KumikoRenderMode.Hardware;

    /// <inheritdoc />
    public bool PaintsOnInvalidate => false;

    /// <inheritdoc />
    public void Attach(KumikoSurfaceRenderer renderer)
    {
        _renderer = renderer;
        // A resize must redraw at the new size; the visual's size change alone is not guaranteed to.
        SizeChanged -= OnSizeChanged;
        SizeChanged += OnSizeChanged;
    }

    /// <inheritdoc />
    public void Detach()
    {
        SizeChanged -= OnSizeChanged;
        _renderer = null;
    }

    // IKumikoSurface.Invalidate is satisfied by SKCanvasElement.Invalidate.

    /// <inheritdoc />
    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        if (_renderer is { } renderer && area.Width > 0 && area.Height > 0)
            renderer(canvas, (float)area.Width, (float)area.Height);
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => Invalidate();
}
#endif

using Microsoft.UI.Xaml;
using SkiaSharp;

namespace KumikoUI.Uno.Rendering;

/// <summary>
/// Draws one frame of the grid. <paramref name="canvas"/> is already in logical units (DIPs) with its origin at the
/// surface's top-left corner; <paramref name="width"/> and <paramref name="height"/> are the surface's logical size.
/// </summary>
internal delegate void KumikoSurfaceRenderer(SKCanvas canvas, float width, float height);

/// <summary>
/// The element a <see cref="KumikoDataGrid"/> draws on (#65). Two implementations:
/// <c>KumikoCanvasElementSurface</c> (Uno's <c>SKCanvasElement</c>, drawn into the window's own canvas) and
/// <see cref="KumikoXamlCanvasSurface"/> (<c>SKXamlCanvas</c>, a bitmap copied to the screen).
/// <see cref="KumikoSurfaceFactory"/> picks one from <see cref="KumikoRenderMode"/>.
/// </summary>
internal interface IKumikoSurface
{
    /// <summary>The element to add to the visual tree; it fills the grid.</summary>
    FrameworkElement Element { get; }

    /// <summary>The resolved mode: <see cref="KumikoRenderMode.Hardware"/> or <see cref="KumikoRenderMode.Software"/>.</summary>
    KumikoRenderMode Mode { get; }

    /// <summary>
    /// True when <see cref="Invalidate"/> paints synchronously (and pays for a full bitmap copy) instead of
    /// scheduling a frame. The host then coalesces redraw requests into one paint per frame itself.
    /// </summary>
    bool PaintsOnInvalidate { get; }

    /// <summary>Starts drawing frames through <paramref name="renderer"/>. Idempotent.</summary>
    void Attach(KumikoSurfaceRenderer renderer);

    /// <summary>Stops drawing frames and releases the renderer callback. Idempotent.</summary>
    void Detach();

    /// <summary>Redraws the whole surface (neither surface can redraw a sub-rectangle).</summary>
    void Invalidate();
}

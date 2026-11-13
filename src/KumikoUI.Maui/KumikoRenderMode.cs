namespace KumikoUI.Maui;

/// <summary>
/// Selects the SkiaSharp surface a <see cref="KumikoDataGrid"/> draws on. Set it through
/// <see cref="KumikoDataGrid.RenderMode"/>; <see cref="KumikoDataGrid.ActiveRenderMode"/> reports the surface in use.
/// </summary>
public enum KumikoRenderMode
{
    /// <summary>
    /// The default: <see cref="Gpu"/> on Android and Mac Catalyst, <see cref="Software"/> on iOS and Windows.
    /// SkiaSharp's GPU view is OpenGL ES on iOS, which Apple deprecated, and has not been verified on Windows yet, so
    /// those platforms use it only when <see cref="Gpu"/> is asked for.
    /// </summary>
    Auto,

    /// <summary>
    /// Rasterize on the CPU into a bitmap that is then copied to the screen (SkiaSharp's <c>SKCanvasView</c>).
    /// Works everywhere; the surface to fall back to if a device's GPU driver misbehaves.
    /// </summary>
    Software,

    /// <summary>
    /// Rasterize on the GPU (SkiaSharp's <c>SKGLView</c>): OpenGL ES on Android (a <c>TextureView</c>) and iOS,
    /// Metal on Mac Catalyst, Direct3D through ANGLE on Windows. On a platform without a GPU view the grid uses
    /// <see cref="Software"/> instead.
    /// </summary>
    Gpu,
}

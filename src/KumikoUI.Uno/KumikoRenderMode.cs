namespace KumikoUI.Uno;

/// <summary>
/// Selects the Skia surface a <see cref="KumikoDataGrid"/> draws on. Set it through
/// <see cref="KumikoDataGrid.RenderMode"/>; <see cref="KumikoDataGrid.ActiveRenderMode"/> reports the surface in use.
/// </summary>
public enum KumikoRenderMode
{
    /// <summary>
    /// Use <see cref="Hardware"/> where the platform supports it and <see cref="Software"/> elsewhere. The default.
    /// </summary>
    Auto,

    /// <summary>
    /// Draw into a bitmap that is then copied to the screen (<c>SKXamlCanvas</c>). Works on every head; the only
    /// surface on WinAppSDK.
    /// </summary>
    Software,

    /// <summary>
    /// Draw straight into the window's own Skia canvas (Uno's <c>SKCanvasElement</c>): no intermediate bitmap and
    /// no copy, and GPU-accelerated wherever Uno renders the window on the GPU. Available on the Skia-rendered
    /// heads (desktop, WebAssembly, and iOS/Android with the Skia renderer); where it is not (WinAppSDK, native
    /// renderers) the grid uses <see cref="Software"/> instead.
    /// </summary>
    Hardware,
}

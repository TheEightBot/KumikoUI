namespace KumikoUI.Uno.Rendering;

/// <summary>Resolves a <see cref="KumikoRenderMode"/> and creates the matching <see cref="IKumikoSurface"/>.</summary>
internal static class KumikoSurfaceFactory
{
    /// <summary>
    /// Whether the <see cref="KumikoRenderMode.Hardware"/> surface (Uno's <c>SKCanvasElement</c>) can run here:
    /// true on the Skia-rendered Uno heads, false on WinAppSDK and the native renderers.
    /// </summary>
    public static bool IsHardwareSupported =>
#if HAS_UNO
        global::Uno.WinUI.Graphics2DSK.SKCanvasElement.IsSupportedOnCurrentPlatform();
#else
        false;
#endif

    /// <summary>
    /// The surface to use for <paramref name="requested"/>: <see cref="KumikoRenderMode.Software"/> when asked for or
    /// when the hardware surface is unavailable, <see cref="KumikoRenderMode.Hardware"/> otherwise. Never
    /// <see cref="KumikoRenderMode.Auto"/>.
    /// </summary>
    public static KumikoRenderMode Resolve(KumikoRenderMode requested, bool hardwareSupported) =>
        requested == KumikoRenderMode.Software || !hardwareSupported
            ? KumikoRenderMode.Software
            : KumikoRenderMode.Hardware;

    /// <summary>Creates the surface for a resolved mode (see <see cref="Resolve"/>).</summary>
    public static IKumikoSurface Create(KumikoRenderMode resolved)
    {
#if HAS_UNO
        if (resolved == KumikoRenderMode.Hardware)
            return new KumikoCanvasElementSurface();
#endif
        return new KumikoXamlCanvasSurface();
    }
}

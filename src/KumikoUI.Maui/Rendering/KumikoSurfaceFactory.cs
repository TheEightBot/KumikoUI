namespace KumikoUI.Maui.Rendering;

/// <summary>Resolves a <see cref="KumikoRenderMode"/> and creates the matching <see cref="KumikoSurface"/>.</summary>
internal static class KumikoSurfaceFactory
{
    /// <summary>
    /// Whether SkiaSharp has a GPU view (<c>SKGLView</c>) here: on Android, iOS, Mac Catalyst and Windows; not on a
    /// plain .NET target (the unit tests).
    /// </summary>
    public static bool IsGpuSupported =>
#if ANDROID || IOS || MACCATALYST || WINDOWS
        true;
#else
        false;
#endif

    /// <summary>
    /// Whether <see cref="KumikoRenderMode.Auto"/> picks the GPU view: on Android (OpenGL ES) and Mac Catalyst
    /// (Metal). iOS's GPU view is OpenGL ES, which Apple deprecated, and the Windows one is not verified yet; both
    /// remain available through <see cref="KumikoRenderMode.Hardware"/>.
    /// </summary>
    public static bool IsGpuDefault =>
#if ANDROID || MACCATALYST
        true;
#else
        false;
#endif

    /// <summary>
    /// The surface to use for <paramref name="requested"/>: <see cref="KumikoRenderMode.Hardware"/> when it is asked for,
    /// or for <see cref="KumikoRenderMode.Auto"/> where it is the default, and the GPU view exists; otherwise
    /// <see cref="KumikoRenderMode.Software"/>. Never <see cref="KumikoRenderMode.Auto"/>.
    /// </summary>
    public static KumikoRenderMode Resolve(KumikoRenderMode requested, bool gpuSupported, bool gpuDefault)
    {
        bool gpu = requested switch
        {
            KumikoRenderMode.Hardware => true,
            KumikoRenderMode.Software => false,
            _ => gpuDefault,
        };
        return gpu && gpuSupported ? KumikoRenderMode.Hardware : KumikoRenderMode.Software;
    }

    /// <summary>Resolves <paramref name="requested"/> for this platform.</summary>
    public static KumikoRenderMode Resolve(KumikoRenderMode requested) => Resolve(requested, IsGpuSupported, IsGpuDefault);

    /// <summary>Creates the surface for a resolved mode (see <see cref="Resolve(KumikoRenderMode)"/>).</summary>
    public static KumikoSurface Create(KumikoRenderMode resolved) =>
        resolved == KumikoRenderMode.Hardware ? new KumikoGpuViewSurface() : new KumikoCanvasViewSurface();
}

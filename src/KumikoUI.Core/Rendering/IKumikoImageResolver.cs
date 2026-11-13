namespace KumikoUI.Core.Rendering;

/// <summary>
/// Turns a cell value (a file name, a URL, bytes, a host image source and so on) into an image that
/// <see cref="IDrawingContext.DrawImage"/> can draw. The grid passes it to cell renderers as
/// <see cref="CellRenderContext.ImageResolver"/>, from <see cref="KumikoRenderer.ImageResolver"/>, which both hosts
/// set; <see cref="ImageCellRenderer"/> calls it for every visible image cell on every frame.
/// </summary>
/// <remarks>
/// <para><b>Render loop:</b> <see cref="Resolve"/> runs on the render thread inside a frame. It must never block:
/// a value that is not loaded yet returns <see cref="KumikoImageStatus.Pending"/> while the image is loaded and
/// decoded elsewhere, and the resolver then asks the host to redraw. Once an image is cached, resolving it again
/// must not allocate.</para>
/// <para><b>Ownership:</b> the resolver owns the images it returns. Draw them during the frame they were returned
/// in and do not keep them: the resolver may release them afterwards.</para>
/// <para>The SkiaSharp backend's implementation is <c>KumikoUI.SkiaSharp.KumikoImageCache</c>.</para>
/// </remarks>
public interface IKumikoImageResolver
{
    /// <summary>Resolves <paramref name="source"/> to a drawable image.</summary>
    /// <param name="source">The cell value. Never null.</param>
    /// <param name="image">The image to pass to <see cref="IDrawingContext.DrawImage"/> when the result is
    /// <see cref="KumikoImageStatus.Ready"/>; otherwise null.</param>
    /// <returns>Whether the image is ready, still loading, failed to load or of a kind the resolver cannot load.</returns>
    KumikoImageStatus Resolve(object source, out object? image);
}

using KumikoUI.Core.Rendering;
using KumikoUI.SkiaSharp;

namespace KumikoUI.Maui;

/// <summary>
/// Image cells for <see cref="KumikoDataGrid"/>: the grid's <see cref="KumikoImageCache"/>, which loads the values of
/// <c>Image</c> columns off the UI thread. Same name and behavior as the Uno control.
/// </summary>
public partial class KumikoDataGrid
{
    private readonly KumikoImageCache _imageCache = CreateImageCache();

    /// <summary>
    /// The cache that loads, decodes and keeps the images of <c>Image</c> columns (and of renderers that resolve
    /// images through <see cref="CellRenderContext.ImageResolver"/>). Cell values can be an
    /// <see cref="ImageSource"/> (<see cref="FileImageSource"/>, <see cref="UriImageSource"/>,
    /// <see cref="StreamImageSource"/>), a <see cref="Uri"/>, a URL, a file name of an app package asset
    /// (<c>MauiAsset</c>) or image resource (<c>MauiImage</c>), a file path, a <c>byte[]</c>, a <see cref="Stream"/>,
    /// a stream factory, or a ready <c>SKImage</c>/<c>SKBitmap</c>. Set its capacities (<see cref="KumikoImageCache.MaxCount"/>,
    /// <see cref="KumikoImageCache.MaxBytes"/>, <see cref="KumikoImageCache.MaxDecodedSize"/>) or add a loader
    /// (<see cref="KumikoImageCache.Loaders"/>) here. The grid redraws when images finish loading and clears the cache,
    /// disposing its images, when it is disconnected from its handler.
    /// </summary>
    public KumikoImageCache ImageCache => _imageCache;

    private static KumikoImageCache CreateImageCache()
    {
        var cache = new KumikoImageCache();
        cache.Loaders.Insert(0, new MauiImageLoader());
        return cache;
    }

    /// <summary>Called from the constructor: resolves image cells through the cache and redraws when images load.</summary>
    private void InitializeImages()
    {
        _renderer.ImageResolver = _imageCache;
        ConnectImages();
    }

    /// <summary>Called from <c>Reconnect</c>. Idempotent.</summary>
    private void ConnectImages()
    {
        _imageCache.ImagesLoaded -= OnImagesLoaded;
        _imageCache.ImagesLoaded += OnImagesLoaded;
    }

    /// <summary>Called from <c>Cleanup</c>: cancels the loads in progress and disposes the cached images.</summary>
    private void DisconnectImages()
    {
        _imageCache.ImagesLoaded -= OnImagesLoaded;
        _imageCache.Clear();
    }

    // Raised on a worker thread; Invalidate marshals to the UI thread.
    private void OnImagesLoaded(object? sender, EventArgs e) => Invalidate();
}

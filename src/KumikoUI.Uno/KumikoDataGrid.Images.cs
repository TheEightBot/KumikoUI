using KumikoUI.Core.Rendering;
using KumikoUI.SkiaSharp;

namespace KumikoUI.Uno;

/// <summary>
/// Image cells for <see cref="KumikoDataGrid"/>: the grid's <see cref="KumikoImageCache"/>, which loads the values of
/// <c>Image</c> columns off the UI thread. Same name and behavior as the MAUI control.
/// </summary>
public partial class KumikoDataGrid
{
    private readonly KumikoImageCache _imageCache = CreateImageCache();

    /// <summary>
    /// The cache that loads, decodes and keeps the images of <c>Image</c> columns (and of renderers that resolve
    /// images through <see cref="CellRenderContext.ImageResolver"/>). Cell values can be a <see cref="Uri"/>
    /// (<c>ms-appx:///</c>, <c>ms-appdata:///</c>, <c>http</c>, <c>https</c>, <c>file</c>, <c>data</c>), a URL or
    /// app package path string (<c>Assets/Photos/a.png</c>), a
    /// <see cref="Microsoft.UI.Xaml.Media.Imaging.BitmapImage"/> with a <c>UriSource</c>, a file path, a
    /// <c>byte[]</c>, a <see cref="Stream"/>, a stream factory, or a ready <c>SKImage</c>/<c>SKBitmap</c>. Set its
    /// capacities (<see cref="KumikoImageCache.MaxCount"/>, <see cref="KumikoImageCache.MaxBytes"/>,
    /// <see cref="KumikoImageCache.MaxDecodedSize"/>) or add a loader (<see cref="KumikoImageCache.Loaders"/>) here.
    /// The grid redraws when images finish loading and clears the cache, disposing its images, when it is unloaded.
    /// </summary>
    public KumikoImageCache ImageCache => _imageCache;

    private static KumikoImageCache CreateImageCache()
    {
        var cache = new KumikoImageCache();
        cache.Loaders.Insert(0, new UnoImageLoader());
        return cache;
    }

    /// <summary>Called from the constructor: resolves image cells through the cache and redraws when images load.</summary>
    private void InitializeImages()
    {
        _renderer.ImageResolver = _imageCache;
        ConnectImages();
    }

    /// <summary>Called from <c>OnLoaded</c>. Idempotent: Uno raises Loaded again on navigation back.</summary>
    private void ConnectImages()
    {
        _imageCache.ImagesLoaded -= OnImagesLoaded;
        _imageCache.ImagesLoaded += OnImagesLoaded;
    }

    /// <summary>Called from <c>OnUnloaded</c>: cancels the loads in progress and disposes the cached images.</summary>
    private void DisconnectImages()
    {
        _imageCache.ImagesLoaded -= OnImagesLoaded;
        _imageCache.Clear();
    }

    // Raised on a worker thread; Invalidate marshals to the UI thread.
    private void OnImagesLoaded(object? sender, EventArgs e) => Invalidate();
}

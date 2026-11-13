using System.Diagnostics.CodeAnalysis;

namespace KumikoUI.SkiaSharp;

/// <summary>
/// Loads the encoded bytes (PNG, JPEG, WebP, GIF, BMP, ICO) of one kind of image source for a
/// <see cref="KumikoImageCache"/>. The cache asks its <see cref="KumikoImageCache.Loaders"/> in order which one
/// takes a cell value, keys its cache by what that loader returns, and decodes what the loader opens.
/// </summary>
/// <remarks>
/// Both hosts add a loader for their own image types (MAUI <c>ImageSource</c>, Uno <c>BitmapImage</c> and
/// <c>ms-appx:///</c> URIs, app package files) in front of <see cref="KumikoImageLoader.Default"/>. Insert your
/// own at index 0 of <see cref="KumikoImageCache.Loaders"/> to load another kind of value.
/// </remarks>
public interface IKumikoImageLoader
{
    /// <summary>
    /// Returns whether this loader loads <paramref name="source"/> and, if so, the key the cache stores its image
    /// under. Runs on the render thread for every visible image cell on every frame, so it must be cheap and must
    /// not allocate: return the source itself or an object it already holds (a file name, a <see cref="Uri"/>).
    /// </summary>
    /// <param name="source">The cell value. Never null.</param>
    /// <param name="key">The cache key: equal keys share one cached image. A <see cref="Uri"/> key equals another
    /// <see cref="Uri"/> or a string with the same original text.</param>
    /// <returns><c>true</c> if this loader loads <paramref name="source"/>.</returns>
    bool TryGetCacheKey(object source, [NotNullWhen(true)] out object? key);

    /// <summary>
    /// Opens the encoded image for a key this loader returned from <see cref="TryGetCacheKey"/>. Called on a
    /// thread-pool thread; the cache reads and disposes the stream.
    /// </summary>
    /// <param name="key">The key returned by <see cref="TryGetCacheKey"/>.</param>
    /// <param name="cancellationToken">Canceled when the image is evicted or the cache is cleared or disposed
    /// before it has loaded.</param>
    /// <returns>A stream of the encoded image, or null when there is none.</returns>
    Task<Stream?> OpenAsync(object key, CancellationToken cancellationToken);
}

using System.Diagnostics.CodeAnalysis;
using KumikoUI.SkiaSharp;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;

namespace KumikoUI.Uno;

/// <summary>
/// Loads the Uno image sources of image cells for the grid's <see cref="KumikoImageCache"/>, in front of
/// <see cref="KumikoImageLoader.Default"/>:
/// <list type="bullet">
///   <item><c>ms-appx:///</c> and <c>ms-appdata:///</c> URIs and strings: files of the app package or app data;</item>
///   <item>relative paths and relative <see cref="Uri"/>s: app package files (<c>Assets/Photos/a.png</c> is
///   <c>ms-appx:///Assets/Photos/a.png</c>);</item>
///   <item><see cref="BitmapImage"/> with a <see cref="BitmapImage.UriSource"/>: loaded like that URI, so it shares
///   the cached image of the same URL.</item>
/// </list>
/// Other image sources (for example a <c>WriteableBitmap</c> or an <c>SvgImageSource</c>) are not loaded and are
/// reported once.
/// </summary>
internal sealed class UnoImageLoader : IKumikoImageLoader
{
    public bool TryGetCacheKey(object source, [NotNullWhen(true)] out object? key)
    {
        switch (source)
        {
            case Uri { IsAbsoluteUri: false } relative when relative.OriginalString.Length > 0:
                key = relative.OriginalString;
                return true;
            case Uri uri when uri.IsAbsoluteUri && IsAppScheme(uri.Scheme):
                key = uri;
                return true;
            case BitmapImage { UriSource: { } bitmapUri }:
                key = bitmapUri.IsAbsoluteUri ? bitmapUri : bitmapUri.OriginalString;
                return true;
            // A web URL or a rooted path is the default loader's; any other text is an app package path.
            case string text when text.Length > 0 && !KumikoImageLoader.Default.TryGetCacheKey(text, out _):
                key = text;
                return true;
            default:
                key = null;
                return false;
        }
    }

    public async Task<Stream?> OpenAsync(object key, CancellationToken cancellationToken)
    {
        switch (key)
        {
            case Uri uri when uri.IsAbsoluteUri && IsAppScheme(uri.Scheme):
                return await OpenAppFileAsync(uri, cancellationToken).ConfigureAwait(false);
            case Uri uri:
                return await KumikoImageLoader.OpenUriAsync(uri, cancellationToken).ConfigureAwait(false);
            case string text when KumikoImageLoader.Default.TryGetCacheKey(text, out var defaultKey):
                return await KumikoImageLoader.Default.OpenAsync(defaultKey, cancellationToken).ConfigureAwait(false);
            case string path:
                return await OpenAppFileAsync(ToAppUri(path), cancellationToken).ConfigureAwait(false);
            default:
                return null;
        }
    }

    /// <summary>The URI of an app package path: <c>ms-appx:///</c> plus the path, unless it has an app scheme.</summary>
    internal static Uri ToAppUri(string path) =>
        path.StartsWith("ms-appx:", StringComparison.OrdinalIgnoreCase) || path.StartsWith("ms-appdata:", StringComparison.OrdinalIgnoreCase)
            ? new Uri(path, UriKind.Absolute)
            : new Uri("ms-appx:///" + path.Replace('\\', '/').TrimStart('/'), UriKind.Absolute);

    private static bool IsAppScheme(string scheme) =>
        string.Equals(scheme, "ms-appx", StringComparison.OrdinalIgnoreCase)
        || string.Equals(scheme, "ms-appdata", StringComparison.OrdinalIgnoreCase);

    private static async Task<Stream> OpenAppFileAsync(Uri uri, CancellationToken cancellationToken)
    {
        var file = await StorageFile.GetFileFromApplicationUriAsync(uri);
        cancellationToken.ThrowIfCancellationRequested();

        // AsStreamForRead rather than OpenStreamForReadAsync, which is missing on Uno's non-WinAppSDK heads (as in KumikoFonts).
        using var randomAccessStream = await file.OpenReadAsync();
        using var stream = randomAccessStream.AsStreamForRead();
        var copy = new MemoryStream();
        await stream.CopyToAsync(copy, cancellationToken).ConfigureAwait(false);
        copy.Position = 0;
        return copy;
    }
}

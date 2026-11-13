using System.Diagnostics.CodeAnalysis;
using KumikoUI.SkiaSharp;
#if IOS || MACCATALYST
using UIKit;
#endif

namespace KumikoUI.Maui;

/// <summary>
/// Loads the MAUI image sources of image cells for the grid's <see cref="KumikoImageCache"/>, in front of
/// <see cref="KumikoImageLoader.Default"/>:
/// <list type="bullet">
///   <item><see cref="FileImageSource"/> and file-name strings: a file path, an app package asset
///   (<c>MauiAsset</c>, for example under <c>Resources/Raw</c>) or an image resource (<c>MauiImage</c>);</item>
///   <item><see cref="UriImageSource"/>: downloaded like a URL value;</item>
///   <item><see cref="StreamImageSource"/>: its stream factory, cached by the source instance.</item>
/// </list>
/// Other image sources (for example <see cref="FontImageSource"/>) are not loaded and are reported once.
/// </summary>
internal sealed class MauiImageLoader : IKumikoImageLoader
{
    public bool TryGetCacheKey(object source, [NotNullWhen(true)] out object? key)
    {
        switch (source)
        {
            case FileImageSource file when !string.IsNullOrEmpty(file.File):
                key = file.File;
                return true;
            case UriImageSource { Uri: { } uri }:
                key = uri;
                return true;
            case StreamImageSource { Stream: not null } stream:
                key = stream;
                return true;
            case Uri { IsAbsoluteUri: false } relative when relative.OriginalString.Length > 0:
                key = relative.OriginalString;
                return true;
            // A URL or a rooted path is the default loader's; any other text is a file name in the app package.
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
            case StreamImageSource { Stream: { } factory }:
                return await factory(cancellationToken).ConfigureAwait(false);
            case Uri uri:
                return await KumikoImageLoader.OpenUriAsync(uri, cancellationToken).ConfigureAwait(false);
            case string text when KumikoImageLoader.Default.TryGetCacheKey(text, out var defaultKey):
                return await KumikoImageLoader.Default.OpenAsync(defaultKey, cancellationToken).ConfigureAwait(false);
            case string fileName:
                return await OpenAppFileAsync(fileName).ConfigureAwait(false)
                    ?? throw new FileNotFoundException(
                        $"'{fileName}' is not an app package asset (MauiAsset) or an image resource (MauiImage).", fileName);
            default:
                return null;
        }
    }

    /// <summary>Opens a file of the app package: a <c>MauiAsset</c>, or a <c>MauiImage</c> where the platform keeps it.</summary>
    private static async Task<Stream?> OpenAppFileAsync(string fileName)
    {
        try
        {
            // MauiAsset items on every platform; MauiImage files too where they are copied into the package as files.
            return await FileSystem.OpenAppPackageFileAsync(fileName).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Not an asset (or no platform implementation, as in unit tests): try the image resources below.
        }

        string name = Path.GetFileNameWithoutExtension(fileName);
        if (name.Length == 0) return null;

#if ANDROID
        // MauiImage items become drawables named in lower case without the extension.
        var context = Android.App.Application.Context;
        var resources = context.Resources;
        if (resources is not null)
        {
#pragma warning disable CA1422 // GetIdentifier is discouraged, not removed; MAUI resolves image file names the same way
            int id = resources.GetIdentifier(name.ToLowerInvariant(), "drawable", context.PackageName);
#pragma warning restore CA1422
            if (id != 0) return resources.OpenRawResource(id);
        }
#elif IOS || MACCATALYST
        // MauiImage items in the bundle or its asset catalog.
        using var image = UIImage.FromBundle(name);
        using var png = image?.AsPNG();
        if (png is not null)
        {
            var bytes = new byte[(int)png.Length];
            System.Runtime.InteropServices.Marshal.Copy(png.Bytes, bytes, 0, bytes.Length);
            return new MemoryStream(bytes, 0, bytes.Length, writable: false, publiclyVisible: true);
        }
#endif
        return null;
    }
}

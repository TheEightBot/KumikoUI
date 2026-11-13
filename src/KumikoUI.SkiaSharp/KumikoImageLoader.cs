using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace KumikoUI.SkiaSharp;

/// <summary>
/// The platform-neutral <see cref="IKumikoImageLoader"/> every <see cref="KumikoImageCache"/> starts with. It loads:
/// <list type="bullet">
///   <item><c>byte[]</c>: the encoded image;</item>
///   <item><see cref="Stream"/>: read once, from the start when it can seek (the stream is not disposed);</item>
///   <item><c>Func&lt;Stream&gt;</c> and <c>Func&lt;CancellationToken, Task&lt;Stream&gt;&gt;</c>: called on a
///   thread-pool thread for a new stream, which is disposed after reading;</item>
///   <item>absolute <see cref="Uri"/>s and URL strings with the <c>http</c>, <c>https</c>, <c>file</c> or
///   <c>data</c> scheme: web images are downloaded through one shared <see cref="HttpClient"/>, and the download is
///   canceled when the image is evicted or the cache is cleared before it finishes;</item>
///   <item>rooted file paths.</item>
/// </list>
/// </summary>
/// <remarks>
/// Byte arrays, streams and factories are cached by reference: keep the same instance on the item to reuse the
/// cached image. URLs and paths are cached by their text. Relative paths and host image types are loaded by the
/// host's loader, which comes before this one in <see cref="KumikoImageCache.Loaders"/>.
/// </remarks>
public class KumikoImageLoader : IKumikoImageLoader
{
    private static readonly Lazy<HttpClient> s_httpClient = new(
        static () => new HttpClient { Timeout = TimeSpan.FromSeconds(30) }, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>Creates a loader. Use <see cref="Default"/> unless you derive from it.</summary>
    protected KumikoImageLoader()
    {
    }

    /// <summary>The shared instance <see cref="KumikoImageCache.Loaders"/> starts with.</summary>
    public static KumikoImageLoader Default { get; } = new();

    /// <inheritdoc />
    public virtual bool TryGetCacheKey(object source, [NotNullWhen(true)] out object? key)
    {
        switch (source)
        {
            case byte[]:
            case Stream:
            case Func<Stream>:
            case Func<CancellationToken, Task<Stream>>:
                key = source;
                return true;
            case Uri uri when uri.IsAbsoluteUri && IsSupportedScheme(uri.Scheme):
                key = uri;
                return true;
            case string text when IsUrlOrRootedPath(text):
                key = text;
                return true;
            default:
                key = null;
                return false;
        }
    }

    /// <inheritdoc />
    public virtual async Task<Stream?> OpenAsync(object key, CancellationToken cancellationToken)
    {
        switch (key)
        {
            case byte[] bytes:
                return new MemoryStream(bytes, 0, bytes.Length, writable: false, publiclyVisible: true);
            case Stream stream:
                return await CopyAsync(stream, cancellationToken).ConfigureAwait(false);
            case Func<Stream> factory:
                return factory();
            case Func<CancellationToken, Task<Stream>> asyncFactory:
                return await asyncFactory(cancellationToken).ConfigureAwait(false);
            case Uri uri:
                return await OpenUriAsync(uri, cancellationToken).ConfigureAwait(false);
            case string text when IsUrl(text):
                return await OpenUriAsync(new Uri(text, UriKind.Absolute), cancellationToken).ConfigureAwait(false);
            case string path:
                return File.OpenRead(path);
            default:
                return null;
        }
    }

    /// <summary>
    /// Opens an absolute <c>http</c>, <c>https</c>, <c>file</c> or <c>data</c> URI. Host loaders use it for web
    /// images in their own image types, so every grid downloads through the same <see cref="HttpClient"/>.
    /// </summary>
    /// <param name="uri">The absolute URI.</param>
    /// <param name="cancellationToken">Cancels the download.</param>
    /// <returns>The encoded image, or null for another scheme.</returns>
    /// <exception cref="HttpRequestException">The server did not return a success status.</exception>
    public static async Task<Stream?> OpenUriAsync(Uri uri, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri) return null;

        if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        {
            using var response = await s_httpClient.Value
                .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var buffer = new MemoryStream();
            var content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (content.ConfigureAwait(false))
                await content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            buffer.Position = 0;
            return buffer;
        }

        if (uri.Scheme == Uri.UriSchemeFile)
            return File.OpenRead(uri.LocalPath);

        if (string.Equals(uri.Scheme, "data", StringComparison.OrdinalIgnoreCase))
            return DecodeDataUri(uri.OriginalString);

        return null;
    }

    private static bool IsSupportedScheme(string scheme) =>
        scheme == Uri.UriSchemeHttp || scheme == Uri.UriSchemeHttps || scheme == Uri.UriSchemeFile
        || string.Equals(scheme, "data", StringComparison.OrdinalIgnoreCase);

    // Prefix checks rather than Uri.TryCreate: this runs per cell per frame and must not allocate.
    private static bool IsUrl(string text) =>
        text.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        || text.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
        || text.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
        || text.StartsWith("data:", StringComparison.OrdinalIgnoreCase);

    private static bool IsUrlOrRootedPath(string text) => IsUrl(text) || Path.IsPathRooted(text);

    private static async Task<Stream> CopyAsync(Stream source, CancellationToken cancellationToken)
    {
        if (source.CanSeek) source.Position = 0;
        var copy = new MemoryStream();
        await source.CopyToAsync(copy, cancellationToken).ConfigureAwait(false);
        copy.Position = 0;
        return copy;
    }

    // data:[<media type>][;base64],<data>
    private static MemoryStream? DecodeDataUri(string text)
    {
        int comma = text.IndexOf(',');
        if (comma < 0) return null;
        string header = text.Substring(0, comma);
        string payload = text.Substring(comma + 1);
        if (payload.Contains('%')) payload = Uri.UnescapeDataString(payload);
        byte[] bytes = header.EndsWith(";base64", StringComparison.OrdinalIgnoreCase)
            ? Convert.FromBase64String(payload)
            : Encoding.UTF8.GetBytes(payload);
        return new MemoryStream(bytes, 0, bytes.Length, writable: false, publiclyVisible: true);
    }
}

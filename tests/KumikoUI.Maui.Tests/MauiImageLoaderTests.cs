using KumikoUI.Core.Rendering;
using KumikoUI.SkiaSharp;
using SkiaSharp;

namespace KumikoUI.Maui.Tests;

/// <summary>
/// The MAUI grid's image cache (#63): MAUI image sources map to cache keys, and a <see cref="StreamImageSource"/>
/// loads through the grid's <see cref="KumikoDataGrid.ImageCache"/>.
/// </summary>
public class MauiImageLoaderTests
{
    private static byte[] Png(int size = 6)
    {
        using var bitmap = new SKBitmap(size, size);
        bitmap.Erase(SKColors.Teal);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    [Fact]
    public void TheGrid_ResolvesImagesThroughItsCache_WithTheMauiLoaderFirst()
    {
        var grid = new KumikoDataGrid();

        Assert.IsType<MauiImageLoader>(grid.ImageCache.Loaders[0]);
        Assert.Same(KumikoImageLoader.Default, grid.ImageCache.Loaders[1]);
    }

    [Fact]
    public void ImageSources_MapToKeysSharedWithEqualValues()
    {
        var loader = new MauiImageLoader();
        var uri = new Uri("https://example.com/a.png");
        var stream = new StreamImageSource { Stream = _ => Task.FromResult<Stream>(new MemoryStream()) };

        Assert.True(loader.TryGetCacheKey(new FileImageSource { File = "avatar.png" }, out var fileKey));
        Assert.Equal("avatar.png", fileKey);
        Assert.True(loader.TryGetCacheKey(new UriImageSource { Uri = uri }, out var uriKey));
        Assert.Same(uri, uriKey);
        Assert.True(loader.TryGetCacheKey(stream, out var streamKey));
        Assert.Same(stream, streamKey);
        Assert.True(loader.TryGetCacheKey("Avatars/a.png", out var nameKey));
        Assert.Equal("Avatars/a.png", nameKey);
        Assert.True(loader.TryGetCacheKey(new Uri("a.png", UriKind.Relative), out var relativeKey));
        Assert.Equal("a.png", relativeKey);
    }

    [Fact]
    public void UrlsAndUnloadableSources_AreLeftToOtherLoaders()
    {
        var loader = new MauiImageLoader();

        Assert.False(loader.TryGetCacheKey("https://example.com/a.png", out _));
        Assert.False(loader.TryGetCacheKey(new Uri("https://example.com/a.png"), out _));
        Assert.False(loader.TryGetCacheKey(new FontImageSource { Glyph = "A" }, out _));
        Assert.False(loader.TryGetCacheKey(new FileImageSource(), out _));
        Assert.False(loader.TryGetCacheKey("", out _));
    }

    [Fact]
    public async Task AStreamImageSource_LoadsThroughTheGridsCache()
    {
        var grid = new KumikoDataGrid();
        var png = Png();
        var source = new StreamImageSource { Stream = _ => Task.FromResult<Stream>(new MemoryStream(png)) };
        var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        grid.ImageCache.ImagesLoaded += (_, _) => loaded.TrySetResult();

        Assert.Equal(KumikoImageStatus.Pending, grid.ImageCache.Resolve(source, out _));
        await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(KumikoImageStatus.Ready, grid.ImageCache.Resolve(source, out var image));
        Assert.Equal(6, Assert.IsType<SKImage>(image).Width);
    }

    [Fact]
    public async Task AMissingAppFile_Fails()
    {
        var loader = new MauiImageLoader();

        await Assert.ThrowsAsync<FileNotFoundException>(() => loader.OpenAsync("no-such-image.png", CancellationToken.None));
    }
}

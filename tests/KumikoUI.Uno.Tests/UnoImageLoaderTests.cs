using KumikoUI.SkiaSharp;
using KumikoUI.Uno;

namespace KumikoUI.Uno.Tests;

/// <summary>
/// The Uno grid's image loader (#63): app package URIs and paths map to cache keys, and web URLs are left to
/// <see cref="KumikoImageLoader.Default"/>.
/// </summary>
public class UnoImageLoaderTests
{
    [Fact]
    public void AppPackageUrisAndPaths_AreTheUnoLoaders()
    {
        var loader = new UnoImageLoader();
        var appx = new Uri("ms-appx:///Assets/Avatars/a.png");

        Assert.True(loader.TryGetCacheKey(appx, out var appxKey));
        Assert.Same(appx, appxKey);
        Assert.True(loader.TryGetCacheKey(new Uri("ms-appdata:///local/a.png"), out _));
        Assert.True(loader.TryGetCacheKey("ms-appx:///Assets/a.png", out var appxText));
        Assert.Equal("ms-appx:///Assets/a.png", appxText);
        Assert.True(loader.TryGetCacheKey("Assets/a.png", out var path));
        Assert.Equal("Assets/a.png", path);
        Assert.True(loader.TryGetCacheKey(new Uri("Assets/a.png", UriKind.Relative), out var relative));
        Assert.Equal("Assets/a.png", relative);
    }

    [Fact]
    public void WebUrlsAndOtherValues_AreLeftToOtherLoaders()
    {
        var loader = new UnoImageLoader();

        Assert.False(loader.TryGetCacheKey("https://example.com/a.png", out _));
        Assert.False(loader.TryGetCacheKey(new Uri("https://example.com/a.png"), out _));
        Assert.False(loader.TryGetCacheKey(42, out _));
        Assert.False(loader.TryGetCacheKey("", out _));
    }

    [Theory]
    [InlineData("Assets/a.png", "ms-appx:///Assets/a.png")]
    [InlineData("/Assets/a.png", "ms-appx:///Assets/a.png")]
    [InlineData(@"Assets\a.png", "ms-appx:///Assets/a.png")]
    [InlineData("ms-appx:///Assets/a.png", "ms-appx:///Assets/a.png")]
    [InlineData("ms-appdata:///local/a.png", "ms-appdata:///local/a.png")]
    public void PathsBecomeAppPackageUris(string path, string expected)
    {
        Assert.Equal(new Uri(expected), UnoImageLoader.ToAppUri(path));
    }
}

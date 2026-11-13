using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;
using KumikoUI.SkiaSharp;
using SkiaSharp;

namespace KumikoUI.Core.Tests;

/// <summary>
/// <see cref="KumikoImageCache"/> (#63): loads image sources off the render thread, caches the decoded images in a
/// bounded LRU, disposes what it evicts, asks for one redraw per batch of finished loads and reports what it cannot
/// load once.
/// </summary>
public class KumikoImageCacheTests
{
    private static (KumikoImageCache Cache, TestLoader Loader, List<string> Log) Create(int maxCount = 16, long maxBytes = long.MaxValue)
    {
        var cache = new KumikoImageCache(maxCount, maxBytes);
        var loader = new TestLoader();
        cache.Loaders.Insert(0, loader);
        var log = new List<string>();
        cache.Log = message => { lock (log) log.Add(message); };
        return (cache, loader, log);
    }

    /// <summary>Resolves until the image has loaded: the first call starts the load, the second returns the result.</summary>
    private static async Task<(KumikoImageStatus Status, object? Image)> LoadAsync(KumikoImageCache cache, object source)
    {
        cache.Resolve(source, out _);
        await cache.WhenIdleAsync();
        var status = cache.Resolve(source, out var image);
        return (status, image);
    }

    [Fact]
    public async Task AMiss_IsPendingUntilDecoded_ThenEveryHitReturnsTheSameImage()
    {
        var (cache, loader, _) = Create();
        var source = new TestSource(1);

        Assert.Equal(KumikoImageStatus.Pending, cache.Resolve(source, out var pending));
        Assert.Null(pending);
        await cache.WhenIdleAsync();

        Assert.Equal(KumikoImageStatus.Ready, cache.Resolve(source, out var first));
        Assert.Equal(KumikoImageStatus.Ready, cache.Resolve(new TestSource(1), out var second));
        var image = Assert.IsType<SKImage>(first);
        Assert.Same(first, second);
        Assert.Equal(8, image.Width);
        Assert.Equal(1, loader.Opens);
        Assert.Equal(1, cache.Count);
        Assert.Equal(8 * 8 * 4, cache.Bytes);
    }

    [Fact]
    public void SkImageAndSkBitmapValues_AreReturnedAsTheyAre_AndNotCached()
    {
        var (cache, loader, _) = Create();
        using var bitmap = new SKBitmap(4, 4);
        using var image = SKImage.FromBitmap(bitmap);

        Assert.Equal(KumikoImageStatus.Ready, cache.Resolve(image, out var resolvedImage));
        Assert.Equal(KumikoImageStatus.Ready, cache.Resolve(bitmap, out var resolvedBitmap));
        Assert.Same(image, resolvedImage);
        Assert.Same(bitmap, resolvedBitmap);
        Assert.Equal(0, cache.Count);
        Assert.Equal(0, loader.Opens);
    }

    [Fact]
    public async Task EvictionByCount_DisposesTheLeastRecentlyUsedImage()
    {
        var (cache, _, _) = Create(maxCount: 2);
        var (_, a) = await LoadAsync(cache, new TestSource(1));
        var (_, b) = await LoadAsync(cache, new TestSource(2));
        cache.Resolve(new TestSource(1), out _); // 1 is now more recent than 2

        var (_, c) = await LoadAsync(cache, new TestSource(3));

        Assert.Equal(IntPtr.Zero, ((SKImage)b!).Handle);
        Assert.NotEqual(IntPtr.Zero, ((SKImage)a!).Handle);
        Assert.NotEqual(IntPtr.Zero, ((SKImage)c!).Handle);
        Assert.Equal(2, cache.Count);
        Assert.Equal(2, cache.LiveImageCount);
    }

    [Fact]
    public async Task EvictionByBytes_DisposesOldImagesUntilUnderTheBudget()
    {
        // 8 x 8 x 4 = 256 bytes per image: two fit.
        var (cache, _, _) = Create(maxBytes: 512);
        var (_, a) = await LoadAsync(cache, new TestSource(1));
        var (_, b) = await LoadAsync(cache, new TestSource(2));
        var (_, c) = await LoadAsync(cache, new TestSource(3));

        Assert.Equal(IntPtr.Zero, ((SKImage)a!).Handle);
        Assert.NotEqual(IntPtr.Zero, ((SKImage)b!).Handle);
        Assert.NotEqual(IntPtr.Zero, ((SKImage)c!).Handle);
        Assert.Equal(512, cache.Bytes);
        Assert.Equal(2, cache.LiveImageCount);
    }

    [Fact]
    public async Task LoweringMaxCount_EvictsOnTheNextResolve()
    {
        var (cache, _, _) = Create(maxCount: 8);
        for (int i = 0; i < 6; i++) await LoadAsync(cache, new TestSource(i));

        cache.MaxCount = 2;
        cache.Resolve(new TestSource(5), out _);

        Assert.Equal(2, cache.Count);
        Assert.Equal(2, cache.LiveImageCount);
    }

    [Fact]
    public async Task EvictingAnImageStillLoading_CancelsItsLoad_AndItIsNeverKept()
    {
        var (cache, loader, _) = Create(maxCount: 1);
        loader.Hold();
        cache.Resolve(new TestSource(1), out _);
        while (loader.Opens < 1) await Task.Yield();

        cache.Resolve(new TestSource(2), out _); // evicts 1 while it loads
        Assert.Contains(loader.Tokens, t => t.IsCancellationRequested);

        loader.Release();
        await cache.WhenIdleAsync();
        Assert.Equal(KumikoImageStatus.Ready, cache.Resolve(new TestSource(2), out _));
        Assert.Equal(1, cache.Count);
        Assert.Equal(1, cache.LiveImageCount);
    }

    [Fact]
    public async Task AFinishedLoad_AsksForOneRedraw_HoweverOftenThePendingImageWasDrawn()
    {
        var (cache, loader, _) = Create();
        int redraws = 0;
        cache.ImagesLoaded += (_, _) => Interlocked.Increment(ref redraws);
        loader.Hold();

        for (int frame = 0; frame < 5; frame++)
            Assert.Equal(KumikoImageStatus.Pending, cache.Resolve(new TestSource(1), out _));
        loader.Release();
        await cache.WhenIdleAsync();

        Assert.Equal(1, loader.Opens);
        Assert.Equal(1, redraws);
        for (int frame = 0; frame < 5; frame++)
            Assert.Equal(KumikoImageStatus.Ready, cache.Resolve(new TestSource(1), out _));
        Assert.Equal(1, redraws);
    }

    [Fact]
    public async Task LoadsFinishingBeforeTheNextFrame_AskForOneRedraw()
    {
        var (cache, loader, _) = Create();
        int redraws = 0;
        cache.ImagesLoaded += (_, _) => Interlocked.Increment(ref redraws);
        loader.Hold();
        for (int i = 0; i < 10; i++) cache.Resolve(new TestSource(i), out _);

        loader.Release();
        await cache.WhenIdleAsync();
        Assert.Equal(1, redraws);

        // The next frame draws them; a load finishing after it asks again.
        for (int i = 0; i < 10; i++) Assert.Equal(KumikoImageStatus.Ready, cache.Resolve(new TestSource(i), out _));
        await LoadAsync(cache, new TestSource(99));
        Assert.Equal(2, redraws);
    }

    [Fact]
    public void AnUnsupportedType_IsReportedOncePerType()
    {
        var (cache, _, log) = Create();

        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(KumikoImageStatus.Unsupported, cache.Resolve(new object(), out var image));
            Assert.Null(image);
        }
        Assert.Equal(KumikoImageStatus.Unsupported, cache.Resolve(42, out _));
        Assert.Equal(KumikoImageStatus.Unsupported, cache.Resolve(43, out _));

        Assert.Equal(2, log.Count);
        Assert.Contains("System.Object", log[0]);
        Assert.Contains("System.Int32", log[1]);
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public async Task AFailedLoad_IsFailedUntilEvicted_AndReportedOncePerKindOfKey()
    {
        var (cache, loader, log) = Create();
        loader.Fail = _ => new IOException("disk on fire");
        int redraws = 0;
        cache.ImagesLoaded += (_, _) => redraws++;

        var (first, image) = await LoadAsync(cache, new TestSource(1));
        var (second, _) = await LoadAsync(cache, new TestSource(2));

        Assert.Equal(KumikoImageStatus.Failed, first);
        Assert.Equal(KumikoImageStatus.Failed, second);
        Assert.Null(image);
        Assert.Equal(2, loader.Opens); // a failed entry is not retried every frame
        var message = Assert.Single(log);
        Assert.Contains("disk on fire", message);
        Assert.Equal(0, redraws);
    }

    [Fact]
    public async Task UndecodableData_Fails()
    {
        var cache = new KumikoImageCache();
        var log = new List<string>();
        cache.Log = log.Add;

        var (status, _) = await LoadAsync(cache, new byte[] { 1, 2, 3, 4 });

        Assert.Equal(KumikoImageStatus.Failed, status);
        Assert.Single(log);
    }

    [Fact]
    public async Task Clear_DisposesEveryImage_AndTheNextFrameLoadsThemAgain()
    {
        var (cache, loader, _) = Create();
        var (_, image) = await LoadAsync(cache, new TestSource(1));

        cache.Clear();

        Assert.Equal(IntPtr.Zero, ((SKImage)image!).Handle);
        Assert.Equal(0, cache.Count);
        Assert.Equal(0, cache.Bytes);
        Assert.Equal(0, cache.LiveImageCount);
        var (status, _) = await LoadAsync(cache, new TestSource(1));
        Assert.Equal(KumikoImageStatus.Ready, status);
        Assert.Equal(2, loader.Opens);
    }

    [Fact]
    public async Task Dispose_CancelsLoads_AndDisposesImagesThatFinishLater()
    {
        var (cache, loader, _) = Create();
        await LoadAsync(cache, new TestSource(1));
        loader.Hold();
        cache.Resolve(new TestSource(2), out _);
        while (loader.Opens < 2) await Task.Yield();

        cache.Dispose();
        cache.Dispose();
        loader.Release();
        await cache.WhenIdleAsync();

        Assert.True(cache.IsDisposed);
        Assert.Contains(loader.Tokens, t => t.IsCancellationRequested);
        Assert.Equal(0, cache.LiveImageCount);
        Assert.Equal(KumikoImageStatus.Failed, cache.Resolve(new TestSource(1), out _));
    }

    [Fact]
    public async Task LargeImages_AreDecodedAtMostMaxDecodedSize_KeepingTheirAspectRatio()
    {
        var (cache, loader, _) = Create();
        cache.MaxDecodedSize = 100;
        loader.Width = 400;
        loader.Height = 200;

        var (_, image) = await LoadAsync(cache, new TestSource(1));

        var decoded = Assert.IsType<SKImage>(image);
        Assert.Equal(100, decoded.Width);
        Assert.Equal(50, decoded.Height);
        Assert.Equal(100 * 50 * 4, cache.Bytes);
    }

    // ── The default loader ──────────────────────────────────────

    [Fact]
    public async Task TheDefaultLoader_LoadsBytesStreamsFactoriesDataUrisAndFiles()
    {
        var cache = new KumikoImageCache();
        var png = TestImages.Png(6, 6);
        string path = Path.Join(Path.GetTempPath(), $"kumiko-{Guid.NewGuid():N}.png");
        await File.WriteAllBytesAsync(path, png);
        try
        {
            using var stream = new MemoryStream(png);
            stream.Position = 3; // read from the start anyway
            object[] sources =
            {
                png,
                stream,
                new Func<Stream>(() => new MemoryStream(png)),
                new Func<CancellationToken, Task<Stream>>(_ => Task.FromResult<Stream>(new MemoryStream(png))),
                TestImages.DataUri(6, 6),
                new Uri(TestImages.DataUri(6, 6, shade: 10)),
                path,
                new Uri(path).AbsoluteUri,
            };

            foreach (var source in sources)
            {
                var (status, image) = await LoadAsync(cache, source);
                Assert.True(status == KumikoImageStatus.Ready, $"{source.GetType().Name}: {status}");
                Assert.Equal(6, ((SKImage)image!).Width);
            }
            Assert.True(stream.CanRead, "a stream value is not disposed");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task AUriAndAStringWithTheSameText_ShareOneCachedImage()
    {
        var cache = new KumikoImageCache();
        string text = TestImages.DataUri(5, 5);

        var (_, fromString) = await LoadAsync(cache, text);
        Assert.Equal(KumikoImageStatus.Ready, cache.Resolve(new Uri(text), out var fromUri));

        Assert.Same(fromString, fromUri);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void RelativePathsAndUnknownSchemes_AreLeftToTheHostLoaders()
    {
        Assert.False(KumikoImageLoader.Default.TryGetCacheKey("avatar.png", out _));
        Assert.False(KumikoImageLoader.Default.TryGetCacheKey(new Uri("ms-appx:///Assets/a.png"), out _));
        Assert.False(KumikoImageLoader.Default.TryGetCacheKey(new Uri("a.png", UriKind.Relative), out _));
        Assert.True(KumikoImageLoader.Default.TryGetCacheKey("HTTPS://example.com/a.png", out var key));
        Assert.Equal("HTTPS://example.com/a.png", key);
    }

    // ── Through the grid ────────────────────────────────────────

    public sealed class PhotoRow
    {
        public string Name { get; set; } = "";
        public object? Photo { get; set; }
    }

    private static (KumikoRenderer Renderer, KumikoSource Source, ScrollState Scroll, KumikoSelectionModel Selection, KumikoStyle Style)
        CreateImageGrid(IEnumerable<PhotoRow> rows, IKumikoImageResolver? resolver)
    {
        var source = new KumikoSource();
        source.SetColumns(new[]
        {
            new KumikoColumn { Header = "Name", PropertyName = nameof(PhotoRow.Name), Width = 120 },
            new KumikoColumn
            {
                Header = "Photo", PropertyName = nameof(PhotoRow.Photo), Width = 60, ColumnType = KumikoColumnType.Image,
                ValueAccessor = item => ((PhotoRow)item).Photo,
            },
        });
        source.SetItems(rows.ToList());
        var style = new KumikoStyle();
        var scroll = new ScrollState { ViewportWidth = 180, ViewportHeight = style.HeaderHeight + 30 * style.RowHeight };
        var renderer = new KumikoRenderer { ImageResolver = resolver };
        return (renderer, source, scroll, new KumikoSelectionModel(source), style);
    }

    [Fact]
    public async Task ImageColumns_DrawAfterTheImageLoads_AndKeepDrawingSkImagesUnchanged()
    {
        var cache = new KumikoImageCache();
        using var bitmap = new SKBitmap(4, 4);
        var rows = new[]
        {
            new PhotoRow { Name = "url", Photo = TestImages.DataUri() },
            new PhotoRow { Name = "bitmap", Photo = bitmap },
            new PhotoRow { Name = "none", Photo = null },
        };
        var (renderer, source, scroll, selection, style) = CreateImageGrid(rows, cache);

        var first = new RecordingDrawingContext();
        renderer.Render(first, source, scroll, selection, style);
        Assert.Single(first.Ops, o => o.Kind == DrawOpKind.Image); // the bitmap; the URL is loading

        await cache.WhenIdleAsync();
        var second = new RecordingDrawingContext();
        renderer.Render(second, source, scroll, selection, style);
        Assert.Equal(2, second.Ops.Count(o => o.Kind == DrawOpKind.Image));
    }

    [Fact]
    public void WithoutAResolver_TheValueIsPassedToDrawImageUnchanged()
    {
        var value = new object();
        var images = new List<object>();
        var ctx = new NoOpDrawingContext { ImageDrawn = images.Add };
        var column = new KumikoColumn { ColumnType = KumikoColumnType.Image };

        new ImageCellRenderer().Render(ctx, new KumikoRect(0, 0, 40, 40), value, "", column, new KumikoStyle(), false);
        new ImageCellRenderer { ImageResolver = _ => "mapped" }
            .Render(ctx, new KumikoRect(0, 0, 40, 40), value, "", column, new KumikoStyle(), false);

        Assert.Equal(new[] { value, "mapped" }, images);
    }

    [Fact]
    public async Task ScrollingTenThousandRowsOfDistinctImages_NeverKeepsMoreImagesThanTheCapacity()
    {
        const int Rows = 10_000;
        const int Capacity = 64;
        var cache = new KumikoImageCache(Capacity, long.MaxValue);
        var loader = new TestLoader { Width = 4, Height = 4 };
        cache.Loaders.Insert(0, loader);
        var rows = Enumerable.Range(0, Rows).Select(i => new PhotoRow { Name = $"Row {i}", Photo = new TestSource(i) });
        var (renderer, source, scroll, selection, style) = CreateImageGrid(rows, cache);
        var ctx = new NoOpDrawingContext();
        int peak = 0;

        for (float offset = 0; offset <= Rows * style.RowHeight; offset += 25 * style.RowHeight)
        {
            scroll.OffsetY = offset;
            renderer.Render(ctx, source, scroll, selection, style);   // starts the loads of the rows on screen
            await cache.WhenIdleAsync();
            renderer.Render(ctx, source, scroll, selection, style);   // draws them
            peak = Math.Max(peak, cache.LiveImageCount);
            Assert.InRange(cache.Count, 1, Capacity);
        }

        Assert.True(loader.Opens >= Rows, $"every row's image was loaded ({loader.Opens})");
        Assert.InRange(peak, 30, Capacity);
        cache.Clear();
        Assert.Equal(0, cache.LiveImageCount);
    }
}

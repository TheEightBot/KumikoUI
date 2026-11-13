using System.Diagnostics;
using KumikoUI.Core;
using KumikoUI.Core.Rendering;
using SkiaSharp;

namespace KumikoUI.SkiaSharp;

/// <summary>
/// The SkiaSharp <see cref="IKumikoImageResolver"/>: loads and decodes the images of image cells off the UI thread
/// and keeps the decoded <see cref="SKImage"/>s in a bounded least-recently-used cache. Both hosts create one per
/// grid (<c>KumikoDataGrid.ImageCache</c>), set it as the renderer's <see cref="KumikoRenderer.ImageResolver"/>,
/// redraw when <see cref="ImagesLoaded"/> is raised and <see cref="Clear"/> it when the grid is disconnected.
/// </summary>
/// <remarks>
/// <para><b>Sources:</b> a cell value goes to the first of the <see cref="Loaders"/> that takes it; the cache keys the
/// image by what that loader returns (a URL, a file name, the byte array) and decodes what it opens.
/// <see cref="SKImage"/> and <see cref="SKBitmap"/> values are drawn as they are, without caching: their owner
/// keeps them alive and disposes them. A value no loader takes draws nothing and is reported once per type through
/// <see cref="Trace"/>; so is the first failed load of each kind of key.</para>
/// <para><b>Render loop:</b> <see cref="Resolve"/> never blocks. The first request for an image starts loading and
/// decoding it on a thread-pool thread and returns <see cref="KumikoImageStatus.Pending"/>; when it is decoded,
/// <see cref="ImagesLoaded"/> asks for a redraw, and the next frame draws it. A cached image is returned without
/// allocating.</para>
/// <para><b>Bounded:</b> the cache keeps at most <see cref="MaxCount"/> entries (loaded, loading or failed) and at
/// most <see cref="MaxBytes"/> of decoded pixels, evicting the least recently drawn first and never the image just
/// asked for. Evicting an image disposes it; evicting one that is still loading cancels its download. Images are
/// decoded at most <see cref="MaxDecodedSize"/> pixels on their longer side. Keep <see cref="MaxCount"/> above the
/// number of image cells on screen, or the visible images evict each other and reload.</para>
/// <para><b>Threading:</b> call <see cref="Resolve"/>, <see cref="Clear"/> and <see cref="Dispose"/> on the thread
/// that draws the grid: evicted images are disposed on that thread, between draws.</para>
/// </remarks>
public sealed class KumikoImageCache : IKumikoImageResolver, IDisposable
{
    /// <summary>Default value of <see cref="MaxCount"/>.</summary>
    public const int DefaultMaxCount = 256;

    /// <summary>Default value of <see cref="MaxBytes"/>: 64 MiB.</summary>
    public const long DefaultMaxBytes = 64L * 1024 * 1024;

    /// <summary>Default value of <see cref="MaxDecodedSize"/>.</summary>
    public const int DefaultMaxDecodedSize = 256;

    private readonly object _gate = new();
    private readonly Dictionary<object, Entry> _entries = new(ImageKeyComparer.Instance);
    // First = most recently used, Last = least recently used.
    private readonly LinkedList<Entry> _order = new();
    private readonly List<IKumikoImageLoader> _loaders = new() { KumikoImageLoader.Default };
    private readonly List<CancellationTokenSource> _cancels = new();
    private readonly HashSet<Type> _reportedUnsupported = new();
    private readonly HashSet<Type> _reportedFailures = new();

    private int _maxCount;
    private long _maxBytes;
    private int _maxDecodedSize = DefaultMaxDecodedSize;
    private long _bytes;
    private bool _redrawRequested;
    private bool _disposed;
    private int _liveImages;
    private int _inFlight;
    private TaskCompletionSource? _idle;

    /// <summary>Creates a cache with the default capacities.</summary>
    public KumikoImageCache()
        : this(DefaultMaxCount, DefaultMaxBytes)
    {
    }

    /// <summary>Creates a cache with the given capacities.</summary>
    /// <param name="maxCount">Most images kept (loaded, loading or failed); at least 1.</param>
    /// <param name="maxBytes">Most bytes of decoded pixels kept; at least 1.</param>
    /// <exception cref="ArgumentOutOfRangeException">A capacity is less than 1.</exception>
    public KumikoImageCache(int maxCount, long maxBytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxBytes, 1);
        _maxCount = maxCount;
        _maxBytes = maxBytes;
    }

    /// <summary>
    /// Raised on a thread-pool thread when an image has been decoded and the grid should redraw to show it. Raised
    /// once for any number of images that finish before the next <see cref="Resolve"/> call, so a burst of
    /// downloads asks for one redraw. Both hosts handle it by invalidating the grid.
    /// </summary>
    public event EventHandler? ImagesLoaded;

    /// <summary>
    /// The loaders asked, in order, which one loads a cell value. Starts with <see cref="KumikoImageLoader.Default"/>;
    /// each host inserts its own loader in front of it. Insert yours at index 0 to load another kind of value.
    /// Change it on the thread that draws the grid.
    /// </summary>
    public IList<IKumikoImageLoader> Loaders => _loaders;

    /// <summary>
    /// Most images kept (loaded, loading or failed); at least 1. Lowering it evicts the excess on the next
    /// <see cref="Resolve"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
    public int MaxCount
    {
        get { lock (_gate) return _maxCount; }
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            lock (_gate) _maxCount = value;
        }
    }

    /// <summary>
    /// Most bytes of decoded pixels kept (4 bytes per pixel); at least 1. Lowering it evicts the excess on the next
    /// <see cref="Resolve"/>. The image just asked for is kept even when it alone is larger.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
    public long MaxBytes
    {
        get { lock (_gate) return _maxBytes; }
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            lock (_gate) _maxBytes = value;
        }
    }

    /// <summary>
    /// Longest side, in pixels, an image is decoded at: larger images are scaled down while decoding, keeping their
    /// aspect ratio, so a photo shown in a small cell does not hold its full resolution in memory. 0 decodes at full
    /// size. Applies to images loaded afterwards.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public int MaxDecodedSize
    {
        get => Volatile.Read(ref _maxDecodedSize);
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            Volatile.Write(ref _maxDecodedSize, value);
        }
    }

    /// <summary>Number of cached entries: loaded, loading and failed images.</summary>
    public int Count { get { lock (_gate) return _entries.Count; } }

    /// <summary>Bytes of decoded pixels the cached images hold.</summary>
    public long Bytes { get { lock (_gate) return _bytes; } }

    /// <summary><c>true</c> once <see cref="Dispose"/> has been called.</summary>
    public bool IsDisposed { get { lock (_gate) return _disposed; } }

    /// <summary>Images this cache decoded that are not disposed yet (tests only).</summary>
    internal int LiveImageCount => Volatile.Read(ref _liveImages);

    /// <summary>Where diagnostics go (tests only); <see cref="Trace.TraceWarning(string)"/> by default.</summary>
    internal Action<string> Log { get; set; } = static message => Trace.TraceWarning(message);

    /// <inheritdoc />
    /// <remarks>
    /// Returns <see cref="KumikoImageStatus.Ready"/> with the cached image, or for an <see cref="SKImage"/> or
    /// <see cref="SKBitmap"/> value with the value itself; <see cref="KumikoImageStatus.Pending"/> while the image
    /// loads; <see cref="KumikoImageStatus.Failed"/> after a failed load (until <see cref="Clear"/> or eviction) and
    /// after <see cref="Dispose"/>; <see cref="KumikoImageStatus.Unsupported"/> when no loader takes the value.
    /// </remarks>
    public KumikoImageStatus Resolve(object source, out object? image)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source is SKImage or SKBitmap)
        {
            image = source;
            return KumikoImageStatus.Ready;
        }

        image = null;
        IKumikoImageLoader? loader = null;
        object? key = null;
        var loaders = _loaders;
        for (int i = 0; i < loaders.Count; i++)
        {
            if (loaders[i].TryGetCacheKey(source, out key))
            {
                loader = loaders[i];
                break;
            }
        }

        KumikoImageStatus status;
        bool cancel;
        Type? unsupported = null;
        lock (_gate)
        {
            if (_disposed) return KumikoImageStatus.Failed;

            // A frame is being drawn: the next decoded image asks for a redraw again.
            _redrawRequested = false;

            if (loader is null || key is null)
            {
                if (_reportedUnsupported.Add(source.GetType())) unsupported = source.GetType();
                status = KumikoImageStatus.Unsupported;
            }
            else if (_entries.TryGetValue(key, out var entry))
            {
                if (!ReferenceEquals(entry.Node, _order.First))
                {
                    _order.Remove(entry.Node);
                    _order.AddFirst(entry.Node);
                }

                // Images decoded since the last frame, or a lowered capacity, can leave the cache over its limits.
                if (_bytes > _maxBytes || _entries.Count > _maxCount) TrimLocked();

                status = entry.State;
                if (status == KumikoImageStatus.Ready) image = entry.Image;
            }
            else
            {
                entry = new Entry(key, loader);
                entry.Node = _order.AddFirst(entry);
                _entries.Add(key, entry);
                TrimLocked();
                StartLoadLocked(entry);
                status = KumikoImageStatus.Pending;
            }

            cancel = _cancels.Count > 0;
        }

        if (cancel) CancelEvicted();
        if (unsupported is not null)
        {
            Log($"KumikoUI: image cells cannot show values of type '{unsupported.FullName}'; nothing is drawn. " +
                "Bind a URL, a file name, bytes, a stream or an image source, or add an IKumikoImageLoader to the grid's ImageCache.Loaders.");
        }
        return status;
    }

    /// <summary>
    /// Disposes every cached image, cancels the loads in progress and empties the cache; the next frame loads what
    /// it shows again. Both hosts call it when the grid is disconnected. Call it on the thread that draws the grid.
    /// </summary>
    public void Clear()
    {
        lock (_gate)
        {
            if (_disposed) return;
            ClearLocked();
        }
        CancelEvicted();
    }

    /// <summary>
    /// Disposes every cached image and cancels the loads in progress; images that finish loading afterwards are
    /// disposed at once. Calling it again does nothing.
    /// </summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            ClearLocked();
        }
        CancelEvicted();
    }

    /// <summary>Completes when no load is in progress (tests only).</summary>
    internal async Task WhenIdleAsync()
    {
        while (true)
        {
            Task idle;
            lock (_gate)
            {
                if (_inFlight == 0 || _idle is null) return;
                idle = _idle.Task;
            }
            await idle.ConfigureAwait(false);
        }
    }

    // ── Eviction ─────────────────────────────────────────────────

    private void TrimLocked()
    {
        // Count: the least recently used entries of any state, never the most recent one.
        while (_entries.Count > _maxCount && _order.Last is { } last && !ReferenceEquals(last, _order.First))
            EvictLocked(last.Value);

        // Bytes: the least recently used decoded images (loading and failed entries hold none).
        var node = _order.Last;
        while (_bytes > _maxBytes && node is not null && !ReferenceEquals(node, _order.First))
        {
            var previous = node.Previous;
            if (node.Value.State == KumikoImageStatus.Ready) EvictLocked(node.Value);
            node = previous;
        }
    }

    private void EvictLocked(Entry entry)
    {
        _entries.Remove(entry.Key);
        _order.Remove(entry.Node);
        ReleaseLocked(entry);
    }

    private void ClearLocked()
    {
        foreach (var entry in _order) ReleaseLocked(entry);
        _order.Clear();
        _entries.Clear();
        _bytes = 0;
        _redrawRequested = false;
    }

    private void ReleaseLocked(Entry entry)
    {
        entry.Removed = true;
        if (entry.Image is { } image)
        {
            _bytes -= entry.Bytes;
            entry.Image = null;
            ReleaseImage(image);
        }
        if (entry.State == KumikoImageStatus.Pending && entry.Cancellation is { } cts)
            _cancels.Add(cts);
        entry.Cancellation = null;
    }

    // Outside the lock: canceling runs the download's callbacks, which may complete the load on this thread.
    private void CancelEvicted()
    {
        while (true)
        {
            CancellationTokenSource cts;
            lock (_gate)
            {
                if (_cancels.Count == 0) return;
                cts = _cancels[^1];
                _cancels.RemoveAt(_cancels.Count - 1);
            }
            cts.Cancel();
        }
    }

    private void ReleaseImage(SKImage image)
    {
        image.Dispose();
        Interlocked.Decrement(ref _liveImages);
    }

    // ── Loading ──────────────────────────────────────────────────

    private void StartLoadLocked(Entry entry)
    {
        var cts = new CancellationTokenSource();
        entry.Cancellation = cts;
        if (_inFlight++ == 0) _idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = Task.Run(() => LoadAsync(entry, cts.Token));
    }

    private async Task LoadAsync(Entry entry, CancellationToken cancellationToken)
    {
        SKImage? image = null;
        string? failure = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stream = await entry.Loader.OpenAsync(entry.Key, cancellationToken).ConfigureAwait(false);
            if (stream is null)
            {
                failure = "the loader found nothing to open";
            }
            else
            {
                ArraySegment<byte> bytes;
                using (stream)
                    bytes = await ReadAllAsync(stream, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                image = Decode(bytes, MaxDecodedSize);
                if (image is null) failure = "the data is not an image format SkiaSharp can decode";
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Evicted, cleared or disposed: nothing to report.
        }
        catch (Exception ex)
        {
            failure = ex.Message;
        }

        Complete(entry, image, failure);
    }

    private void Complete(Entry entry, SKImage? image, string? failure)
    {
        bool raise = false;
        bool report = false;
        lock (_gate)
        {
            if (entry.Removed || _disposed)
            {
                if (image is not null) ReleaseImage(image);
            }
            else if (image is not null)
            {
                entry.Image = image;
                entry.Bytes = (long)image.Width * image.Height * image.Info.BytesPerPixel;
                entry.State = KumikoImageStatus.Ready;
                _bytes += entry.Bytes;
                raise = !_redrawRequested;
                _redrawRequested = true;
            }
            else
            {
                entry.State = KumikoImageStatus.Failed;
                report = failure is not null && _reportedFailures.Add(entry.Key.GetType());
            }
            entry.Cancellation = null;
        }

        try
        {
            if (report)
                Log($"KumikoUI: could not load the image '{Describe(entry.Key)}': {failure} " +
                    "(further failures of this kind are not reported).");
            if (raise) ImagesLoaded?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            lock (_gate)
            {
                if (--_inFlight == 0) _idle?.TrySetResult();
            }
        }
    }

    private static string Describe(object key)
    {
        string text = key switch
        {
            Uri uri => uri.OriginalString,
            string s => s,
            _ => key.GetType().FullName ?? key.GetType().Name,
        };
        return text.Length > 120 ? string.Concat(text.AsSpan(0, 120), "…") : text;
    }

    private static async Task<ArraySegment<byte>> ReadAllAsync(Stream stream, CancellationToken cancellationToken)
    {
        // The loaders' own memory streams are read in place.
        if (stream is MemoryStream memory && memory.Position == 0 && memory.TryGetBuffer(out var segment))
            return segment;

        var copy = new MemoryStream();
        await stream.CopyToAsync(copy, cancellationToken).ConfigureAwait(false);
        return new ArraySegment<byte>(copy.GetBuffer(), 0, (int)copy.Length);
    }

    /// <summary>Decodes an encoded image into a raster image no larger than <paramref name="maxSize"/> on its longer side.</summary>
    private SKImage? Decode(ReadOnlySpan<byte> encoded, int maxSize)
    {
        using var data = SKData.CreateCopy(encoded);
        using var codec = SKCodec.Create(data);
        if (codec is null) return null;

        int width = codec.Info.Width, height = codec.Info.Height;
        if (width <= 0 || height <= 0) return null;

        float scale = maxSize > 0 && Math.Max(width, height) > maxSize ? (float)maxSize / Math.Max(width, height) : 1f;
        int targetWidth = Math.Max(1, (int)MathF.Round(width * scale));
        int targetHeight = Math.Max(1, (int)MathF.Round(height * scale));

        // Let the codec scale while decoding where it can (JPEG decodes at 1/2, 1/4, 1/8), then resample the rest.
        var decodeSize = scale < 1f ? codec.GetScaledDimensions(scale) : new SKSizeI(width, height);
        var decodeInfo = new SKImageInfo(decodeSize.Width, decodeSize.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        using var decoded = new SKBitmap(decodeInfo);
        var result = codec.GetPixels(decodeInfo, decoded.GetPixels());
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput)) return null;

        SKImage? image;
        if (decodeSize.Width != targetWidth || decodeSize.Height != targetHeight)
        {
            using var resized = decoded.Resize(decodeInfo.WithSize(targetWidth, targetHeight),
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
            image = resized is null ? null : SKImage.FromPixelCopy(resized.PeekPixels());
        }
        else
        {
            image = SKImage.FromPixelCopy(decoded.PeekPixels());
        }

        if (image is not null) Interlocked.Increment(ref _liveImages);
        return image;
    }

    private sealed class Entry
    {
        public Entry(object key, IKumikoImageLoader loader)
        {
            Key = key;
            Loader = loader;
        }

        public object Key { get; }
        public IKumikoImageLoader Loader { get; }
        public LinkedListNode<Entry> Node { get; set; } = null!;
        public KumikoImageStatus State { get; set; } = KumikoImageStatus.Pending;
        public SKImage? Image { get; set; }
        public long Bytes { get; set; }
        public CancellationTokenSource? Cancellation { get; set; }
        public bool Removed { get; set; }
    }
}

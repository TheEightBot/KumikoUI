using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using KumikoUI.SkiaSharp;
using SkiaSharp;

namespace KumikoUI.Core.Tests.TestHelpers;

/// <summary>Encoded test images and a controllable <see cref="IKumikoImageLoader"/>.</summary>
internal static class TestImages
{
    /// <summary>A PNG of the given size filled with one color.</summary>
    public static byte[] Png(int width = 8, int height = 8, byte shade = 128)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(new SKColor(shade, (byte)(255 - shade), 64));
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>A <c>data:</c> URI of <see cref="Png"/>.</summary>
    public static string DataUri(int width = 8, int height = 8, byte shade = 128) =>
        "data:image/png;base64," + Convert.ToBase64String(Png(width, height, shade));
}

/// <summary>A cell value only <see cref="TestLoader"/> loads.</summary>
internal sealed record TestSource(int Id);

/// <summary>
/// Loads <see cref="TestSource"/> values as small PNGs. Loads can be held back with <see cref="Hold"/> until
/// <see cref="Release"/>, or made to fail.
/// </summary>
internal sealed class TestLoader : IKumikoImageLoader
{
    private readonly ConcurrentDictionary<object, TaskCompletionSource> _gates = new();
    private volatile bool _hold;
    private int _opens;

    public int Opens => Volatile.Read(ref _opens);
    public ConcurrentBag<CancellationToken> Tokens { get; } = new();
    public Func<TestSource, Exception?> Fail { get; set; } = _ => null;
    public int Width { get; set; } = 8;
    public int Height { get; set; } = 8;

    /// <summary>Holds every load started from now on until <see cref="Release"/>.</summary>
    public void Hold() => _hold = true;

    /// <summary>Lets every held load and every later one finish.</summary>
    public void Release()
    {
        _hold = false;
        foreach (var gate in _gates.Values) gate.TrySetResult();
    }

    public bool TryGetCacheKey(object source, [NotNullWhen(true)] out object? key)
    {
        key = source as TestSource;
        return key is not null;
    }

    public async Task<Stream?> OpenAsync(object key, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _opens);
        Tokens.Add(cancellationToken);
        var source = (TestSource)key;
        if (_hold)
        {
            var gate = _gates.GetOrAdd(key, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
            if (!_hold) gate.TrySetResult();
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        if (Fail(source) is { } error) throw error;
        return new MemoryStream(TestImages.Png(Width, Height, (byte)(source.Id % 256)));
    }
}

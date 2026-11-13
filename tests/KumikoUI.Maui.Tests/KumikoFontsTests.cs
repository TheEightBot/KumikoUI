using KumikoUI.SkiaSharp;

namespace KumikoUI.Maui.Tests;

/// <summary>Tests for <see cref="KumikoFonts"/>: app-package registration with an injected file opener.</summary>
[Collection("SkiaFontRegistrar")]
public class KumikoFontsTests : IDisposable
{
    private const string Family = "KumikoFontsTestFamily";

    private static byte[] FontBytes()
    {
        using var resource = typeof(KumikoFontsTests).Assembly.GetManifestResourceStream("KumikoUI.Maui.Tests.OpenSans-Regular.ttf")
            ?? throw new InvalidOperationException("The embedded test font is missing.");
        using var copy = new MemoryStream();
        resource.CopyTo(copy);
        return copy.ToArray();
    }

    private static bool IsRegistered(string family) => SkiaFontRegistrar.TryGetTypeface(family, out _);

    public KumikoFontsTests() => SkiaFontRegistrar.Unregister(Family);

    public void Dispose() => SkiaFontRegistrar.Unregister(Family);

    [Fact]
    public async Task RegisterFromAppPackageAsync_RegistersTheFontUnderTheFamily()
    {
        string? opened = null;

        await KumikoFonts.RegisterFromAppPackageAsync(Family, "OpenSans-Regular.ttf", name =>
        {
            opened = name;
            return Task.FromResult<Stream>(new MemoryStream(FontBytes()));
        });

        Assert.Equal("OpenSans-Regular.ttf", opened);
        Assert.True(SkiaFontRegistrar.TryGetTypeface(Family, out var typeface));
        Assert.NotNull(typeface);
    }

    [Fact]
    public async Task RegisterFromAppPackageAsync_DoesNotBlockOnAnAsynchronousOpen()
    {
        var gate = new TaskCompletionSource<Stream>();

        var task = KumikoFonts.RegisterFromAppPackageAsync(Family, "OpenSans-Regular.ttf", _ => gate.Task);

        Assert.False(task.IsCompleted);
        Assert.False(IsRegistered(Family));

        gate.SetResult(new MemoryStream(FontBytes()));
        await task;

        Assert.True(IsRegistered(Family));
    }

    [Fact]
    public async Task RegisterFromAppPackageAsync_MissingFile_IsSwallowed()
    {
        await KumikoFonts.RegisterFromAppPackageAsync(Family, "Missing.ttf",
            _ => throw new FileNotFoundException("not in the package"));

        Assert.False(IsRegistered(Family));
    }

    [Fact]
    public async Task RegisterFromAppPackageAsync_AsyncFailure_IsSwallowed()
    {
        await KumikoFonts.RegisterFromAppPackageAsync(Family, "Missing.ttf",
            _ => Task.FromException<Stream>(new FileNotFoundException("not in the package")));

        Assert.False(IsRegistered(Family));
    }

    [Fact]
    public async Task RegisterFromAppPackageAsync_CorruptFont_IsSwallowed()
    {
        await KumikoFonts.RegisterFromAppPackageAsync(Family, "Corrupt.ttf",
            _ => Task.FromResult<Stream>(new MemoryStream([1, 2, 3, 4])));

        Assert.False(IsRegistered(Family));
    }

    [Fact]
    public async Task RegisterFromAppPackageAsync_Sequence_ContinuesPastAFailedEntry()
    {
        await KumikoFonts.RegisterFromAppPackageAsync(
            [("Broken" + Family, "Missing.ttf"), (Family, "OpenSans-Regular.ttf")],
            name => name == "Missing.ttf"
                ? throw new FileNotFoundException(name)
                : Task.FromResult<Stream>(new MemoryStream(FontBytes())));

        Assert.True(IsRegistered(Family));
        Assert.False(IsRegistered("Broken" + Family));
    }

    [Fact]
    public async Task RegisterFromAppPackageAsync_BlankArguments_Fault()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => KumikoFonts.RegisterFromAppPackageAsync("", "a.ttf"));
        await Assert.ThrowsAsync<ArgumentNullException>(() => KumikoFonts.RegisterFromAppPackageAsync(Family, null!));
    }

    [Fact]
    public async Task RegisterFromAppPackageAsync_NullSequence_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => KumikoFonts.RegisterFromAppPackageAsync((IEnumerable<(string, string)>)null!));
    }

    [Fact]
    public void RegisterFromStream_RegistersTheFont()
    {
        using var stream = new MemoryStream(FontBytes());

        KumikoFonts.RegisterFromStream(Family, stream);

        Assert.True(IsRegistered(Family));
    }

    [Fact]
    public void RegisterFromStream_CorruptFont_Throws()
    {
        using var stream = new MemoryStream([1, 2, 3, 4]);

        Assert.Throws<InvalidOperationException>(() => KumikoFonts.RegisterFromStream(Family, stream));
    }
}

/// <summary>Serializes the tests that touch the process-wide <see cref="SkiaFontRegistrar"/>.</summary>
[CollectionDefinition("SkiaFontRegistrar", DisableParallelization = true)]
public sealed class SkiaFontRegistrarCollection;

using System.Reflection;
using KumikoUI.SkiaSharp;

namespace KumikoUI.Maui;

/// <summary>
/// Decorates MAUI's <see cref="IFontRegistrar"/>: every font registered through
/// <c>ConfigureFonts</c> is forwarded to MAUI unchanged and also loaded into
/// <see cref="SkiaFontRegistrar"/> under its alias and file name, so the grid renderer resolves it.
/// </summary>
internal sealed class KumikoFontRegistrar : IFontRegistrar
{
    private readonly IFontRegistrar _inner;

    public KumikoFontRegistrar(IFontRegistrar inner) => _inner = inner;

    public void Register(string filename, string? alias, Assembly assembly)
    {
        _inner.Register(filename, alias, assembly);
        Bridge(filename, alias, () => assembly.GetManifestResourceStream(filename));
    }

    public void Register(string filename, string? alias)
    {
        _inner.Register(filename, alias);
        // Fonts added with AddFont ship in the app package. The async open runs on the thread pool so blocking
        // on it cannot deadlock a UI synchronization context (Windows completes it asynchronously).
        Bridge(filename, alias, () => Task.Run(() => FileSystem.OpenAppPackageFileAsync(filename)).GetAwaiter().GetResult());
    }

    public string? GetFont(string font) => _inner.GetFont(font);

    private static void Bridge(string filename, string? alias, Func<Stream?> open)
    {
        try
        {
            using var stream = open();
            if (stream is null)
            {
                Skip(filename, "the font file was not found");
                return;
            }

            SkiaFontRegistrar.RegisterTypefaceFamiliesFromStream(stream, FamilyNames(filename, alias));
        }
        catch (Exception ex)
        {
            // Non-fatal: the grid falls back to the system font manager for this family.
            Skip(filename, ex.Message);
        }
    }

    /// <summary>The names a font can be requested by: its alias, its file name and the name without extension.</summary>
    internal static string[] FamilyNames(string filename, string? alias) =>
        [.. new[] { alias, filename, Path.GetFileNameWithoutExtension(filename) }
            .Where(n => !string.IsNullOrWhiteSpace(n))!];

    private static void Skip(string filename, string reason) =>
        System.Diagnostics.Debug.WriteLine($"[KumikoUI] Font '{filename}' was not bridged to the grid renderer: {reason}");
}

using KumikoUI.SkiaSharp;

namespace KumikoUI.Maui;

/// <summary>
/// Host-layer helper for registering custom typefaces with KumikoUI's SkiaSharp renderer on MAUI.
/// Wraps MAUI's app-package access (<see cref="FileSystem.OpenAppPackageFileAsync(string)"/>) into
/// <see cref="SkiaFontRegistrar"/>, so a consuming app can make a TTF/OTF available to the grid by family
/// name (usable from <c>KumikoStyle</c> / <c>KumikoFont.Family</c>) with a single call. The API mirrors
/// <c>KumikoUI.Uno.KumikoFonts</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The library ships no fonts.</b> The consuming app supplies the font files as raw assets
/// (<c>&lt;MauiAsset Include="Resources\Raw\**" LogicalName="%(RecursiveDir)%(Filename)%(Extension)" /&gt;</c>,
/// the default of the MAUI project template) and registers them through this helper, for example from
/// <c>App.CreateWindow</c> before the first grid renders. Fonts added with <c>ConfigureFonts</c> are bridged
/// automatically by <c>UseKumikoUI()</c> and do not need this helper.
/// </para>
/// <para>
/// <b>Non-blocking:</b> the app-package file is opened with the asynchronous MAUI API; nothing blocks the
/// calling thread.
/// </para>
/// <para>
/// <b>Non-fatal by design:</b> a missing or unreadable font is logged through
/// <see cref="System.Diagnostics.Debug"/> and the grid falls back to the system font; the app-package
/// overloads never fault for a font problem.
/// </para>
/// </remarks>
public static class KumikoFonts
{
    /// <summary>
    /// Loads a font from the application package by file name and registers it with
    /// <see cref="SkiaFontRegistrar"/> under <paramref name="family"/>.
    /// </summary>
    /// <param name="family">
    /// Family name to register under. Must match the value used in <c>KumikoStyle</c> /
    /// <c>KumikoFont.Family</c> (for example <c>"NotoSansJP"</c>).
    /// </param>
    /// <param name="fileName">
    /// The packaged asset's file name, as <see cref="FileSystem.OpenAppPackageFileAsync(string)"/> expects
    /// it, for example <c>"NotoSansJP-Regular.ttf"</c>.
    /// </param>
    /// <returns>
    /// A task that completes when registration has been attempted. It does not fault for a missing or
    /// unreadable font; failures are logged and the grid falls back to the system font. It faults with an
    /// <see cref="ArgumentException"/> when <paramref name="family"/> or <paramref name="fileName"/> is
    /// <c>null</c>, empty or white space.
    /// </returns>
    public static Task RegisterFromAppPackageAsync(string family, string fileName) =>
        RegisterFromAppPackageAsync(family, fileName, FileSystem.OpenAppPackageFileAsync);

    /// <summary>
    /// Convenience overload that registers several fonts in sequence. Each entry is attempted
    /// independently; a failure for one font does not prevent the others from registering.
    /// </summary>
    /// <param name="fonts">
    /// A sequence of <c>(family, fileName)</c> pairs, for example <c>("NotoSansJP", "NotoSansJP-Regular.ttf")</c>.
    /// </param>
    /// <returns>A task that completes when every entry has been attempted.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fonts"/> is <c>null</c>.</exception>
    public static Task RegisterFromAppPackageAsync(IEnumerable<(string Family, string FileName)> fonts) =>
        RegisterFromAppPackageAsync(fonts, FileSystem.OpenAppPackageFileAsync);

    /// <summary>
    /// Registers a font already opened as a <see cref="Stream"/> with <see cref="SkiaFontRegistrar"/>.
    /// Useful when the font comes from somewhere other than the app package (an embedded resource or the
    /// file system).
    /// </summary>
    /// <param name="family">
    /// Family name to register under. Must match the value used in <c>KumikoStyle</c> /
    /// <c>KumikoFont.Family</c>.
    /// </param>
    /// <param name="stream">
    /// A readable stream containing a valid TrueType (.ttf) or OpenType (.otf) font. The caller owns the
    /// stream and may dispose it after this method returns.
    /// </param>
    /// <remarks>
    /// Unlike the app-package overloads, this method does not swallow exceptions: an unreadable stream
    /// surfaces the underlying <see cref="InvalidOperationException"/> from
    /// <see cref="SkiaFontRegistrar.RegisterTypefaceFromStream(string, Stream)"/>.
    /// </remarks>
    public static void RegisterFromStream(string family, Stream stream) =>
        SkiaFontRegistrar.RegisterTypefaceFromStream(family, stream);

    /// <summary>Registers one font, opening the file through <paramref name="open"/>.</summary>
    internal static async Task RegisterFromAppPackageAsync(
        string family, string fileName, Func<string, Task<Stream>> open)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(family);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        try
        {
            using var stream = await open(fileName).ConfigureAwait(false);
            SkiaFontRegistrar.RegisterTypefaceFromStream(family, stream);
        }
        catch (Exception ex)
        {
            // Non-fatal: the grid falls back to the system default font.
            System.Diagnostics.Debug.WriteLine(
                $"[KumikoUI] Could not register font '{family}' from '{fileName}': {ex.Message}");
        }
    }

    /// <summary>Registers several fonts in sequence, opening each file through <paramref name="open"/>.</summary>
    internal static async Task RegisterFromAppPackageAsync(
        IEnumerable<(string Family, string FileName)> fonts, Func<string, Task<Stream>> open)
    {
        ArgumentNullException.ThrowIfNull(fonts);

        foreach (var (family, fileName) in fonts)
            await RegisterFromAppPackageAsync(family, fileName, open).ConfigureAwait(false);
    }
}

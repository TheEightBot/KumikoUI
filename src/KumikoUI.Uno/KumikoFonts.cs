using KumikoUI.SkiaSharp;
using Windows.Storage;

namespace KumikoUI.Uno;

/// <summary>
/// Host-layer helper for registering custom typefaces with KumikoUI's SkiaSharp renderer on Uno.
/// Wraps Uno/WinUI asset loading (<see cref="StorageFile"/> + the <c>ms-appx:///</c> scheme) into
/// <see cref="SkiaFontRegistrar"/>, so a consuming app can make a TTF/OTF available to the grid by
/// family name (usable from <c>DataGridStyle</c> / <c>GridFont.Family</c>) with a single call.
/// </summary>
/// <remarks>
/// <para>
/// <b>The library ships no fonts.</b> Exactly like <c>KumikoUI.Maui</c>, <c>KumikoUI.Uno</c> stays
/// font-agnostic — the consuming app supplies the TTFs (typically under its own
/// <c>Assets/Fonts/</c> marked as <c>Content</c>) and registers them via this helper, usually during
/// <c>App.OnLaunched</c> before the first grid renders. See <c>docs/uno/05-fonts-and-assets.md</c>.
/// </para>
/// <para>
/// This is a deliberate improvement over the MAUI sample, which carried an app-private
/// <c>TryRegisterFont</c> helper in <c>MauiProgram</c>: here the registration helper lives in the
/// library itself so every consumer reuses one well-documented entry point instead of copy-pasting it.
/// </para>
/// <para>
/// Why this matters: SkiaSharp resolves text with whatever typeface it can find. On some platforms
/// (notably Android) the system font manager does not expose CJK or icon-font glyphs, so those render
/// as tofu/garbled boxes unless an explicit typeface is registered — which is precisely what
/// <see cref="SkiaFontRegistrar"/> solves.
/// </para>
/// <para>
/// <b>Non-fatal by design:</b> every registration is wrapped in try/catch. A missing or unreadable
/// font is logged via <see cref="System.Diagnostics.Debug"/> and the grid simply falls back to the
/// system default font — registration never throws to the caller.
/// </para>
/// </remarks>
public static class KumikoFonts
{
    /// <summary>
    /// Loads a font from the application package by its <c>ms-appx:///</c> URI and registers it with
    /// <see cref="SkiaFontRegistrar"/> under <paramref name="family"/>.
    /// </summary>
    /// <param name="family">
    /// Family name to register under. Must match the value used in
    /// <c>DataGridStyle</c> / <c>GridFont.Family</c> (e.g. <c>"NotoSansJP"</c>, <c>"MaterialIcons"</c>).
    /// </param>
    /// <param name="appxUri">
    /// The packaged-asset URI of the font file, e.g.
    /// <c>ms-appx:///Assets/Fonts/NotoSansJP-Regular.ttf</c>.
    /// </param>
    /// <returns>
    /// A task that completes when registration has been attempted. The task never faults for a
    /// missing/unreadable font — failures are logged and swallowed so the grid falls back to the
    /// system font.
    /// </returns>
    public static async Task RegisterFromAppPackageAsync(string family, string appxUri)
    {
        try
        {
            var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri(appxUri));

            // OpenReadAsync().AsStreamForRead() is used over the System.IO `OpenStreamForReadAsync()`
            // extension because the latter (System.Runtime.WindowsRuntime interop shim) is not present
            // on Uno's non-WinAppSDK heads. AsStreamForRead() is part of Uno's stream surface and
            // compiles uniformly across desktop/WASM/Android/iOS as well as the Windows head.
            using var randomAccessStream = await file.OpenReadAsync();
            using var stream = randomAccessStream.AsStreamForRead();

            SkiaFontRegistrar.RegisterTypefaceFromStream(family, stream);
        }
        catch (Exception ex)
        {
            // Non-fatal — the grid falls back to the system default font.
            System.Diagnostics.Debug.WriteLine(
                $"[KumikoUI] Could not register font '{family}' from '{appxUri}': {ex.Message}");
        }
    }

    /// <summary>
    /// Convenience overload that registers several fonts in sequence. Each entry is attempted
    /// independently; a failure for one font does not prevent the others from registering.
    /// </summary>
    /// <param name="fonts">
    /// A sequence of <c>(family, appxUri)</c> pairs, e.g.
    /// <c>("NotoSansJP", "ms-appx:///Assets/Fonts/NotoSansJP-Regular.ttf")</c>.
    /// </param>
    /// <returns>A task that completes when every entry has been attempted.</returns>
    public static async Task RegisterFromAppPackageAsync(IEnumerable<(string Family, string AppxUri)> fonts)
    {
        ArgumentNullException.ThrowIfNull(fonts);

        foreach (var (family, appxUri) in fonts)
            await RegisterFromAppPackageAsync(family, appxUri);
    }

    /// <summary>
    /// Pass-through that registers a font already opened as a <see cref="Stream"/> with
    /// <see cref="SkiaFontRegistrar"/>. Useful when the consuming app sources the font from somewhere
    /// other than the application package (e.g. an embedded resource or the file system).
    /// </summary>
    /// <param name="family">
    /// Family name to register under. Must match the value used in
    /// <c>DataGridStyle</c> / <c>GridFont.Family</c>.
    /// </param>
    /// <param name="stream">
    /// A readable stream containing a valid TrueType (.ttf) or OpenType (.otf) font. The caller owns
    /// the stream and may dispose it after this method returns.
    /// </param>
    /// <remarks>
    /// Unlike the app-package overloads, this method does not swallow exceptions: an unreadable stream
    /// surfaces the underlying <see cref="InvalidOperationException"/> from
    /// <see cref="SkiaFontRegistrar.RegisterTypefaceFromStream"/> so the caller can decide how to react.
    /// </remarks>
    public static void RegisterFromStream(string family, Stream stream) =>
        SkiaFontRegistrar.RegisterTypefaceFromStream(family, stream);
}

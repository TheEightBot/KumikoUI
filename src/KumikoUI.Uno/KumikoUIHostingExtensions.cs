using Microsoft.Extensions.Hosting;

namespace KumikoUI.Uno;

/// <summary>
/// Extension methods for registering KumikoUI with an Uno app.
/// Uno apps configure services on the <see cref="IHostBuilder"/> obtained from
/// <c>IApplicationBuilder</c> in <c>App.xaml.cs</c>.
/// </summary>
/// <remarks>
/// Unlike MAUI, SkiaSharp needs no <c>UseSkiaSharp()</c> initializer on Uno —
/// <c>SKXamlCanvas</c> self-hosts. This extension keeps app startup symmetric with MAUI's
/// <c>UseSkiaKumikoUI()</c>. Custom-font registration is handled separately by the awaitable
/// <see cref="KumikoFonts"/> helper (called from <c>App.OnLaunched</c>), not on this synchronous
/// host-builder path, to avoid blocking startup on font I/O.
/// </remarks>
public static class KumikoUIHostingExtensions
{
    /// <summary>
    /// Registers KumikoUI services with the Uno app host. Currently a no-op pass-through.
    /// For custom typefaces, call <see cref="KumikoFonts.RegisterFromAppPackageAsync(string, string)"/>
    /// during app startup.
    /// </summary>
    /// <param name="host">The host builder from <c>IApplicationBuilder.Configure(...)</c>.</param>
    /// <returns>The same <paramref name="host"/> for chaining.</returns>
    public static IHostBuilder UseKumikoUI(this IHostBuilder host) => host;
}

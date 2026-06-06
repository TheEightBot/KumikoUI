using Microsoft.Extensions.Hosting;

namespace KumikoUI.Uno;

/// <summary>
/// Extension methods for registering KumikoUI with an Uno app.
/// Uno apps configure services on the <see cref="IHostBuilder"/> obtained from
/// <c>IApplicationBuilder</c> in <c>App.xaml.cs</c>.
/// </summary>
/// <remarks>
/// Unlike MAUI, SkiaSharp needs no <c>UseSkiaSharp()</c> initializer on Uno —
/// <c>SKXamlCanvas</c> self-hosts. This extension still earns its place as the
/// documented spot for font registration (Phase 05) and keeps app startup
/// symmetric with MAUI's <c>UseSkiaKumikoUI()</c>.
/// </remarks>
public static class KumikoUIHostingExtensions
{
    /// <summary>
    /// Registers KumikoUI services with the Uno app host. Currently a no-op pass-through;
    /// Phase 05 adds custom-font registration here.
    /// </summary>
    /// <param name="host">The host builder from <c>IApplicationBuilder.Configure(...)</c>.</param>
    /// <returns>The same <paramref name="host"/> for chaining.</returns>
    public static IHostBuilder UseKumikoUI(this IHostBuilder host) => host;
}

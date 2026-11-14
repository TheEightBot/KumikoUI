using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SkiaSharp.Views.Maui.Controls;
using SkiaSharp.Views.Maui.Handlers;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace KumikoUI.Maui;

/// <summary>
/// Extension methods for registering KumikoUI with a MAUI app.
/// </summary>
public static class KumikoUIHostingExtensions
{
    /// <summary>
    /// Registers KumikoUI with the MAUI app builder. Call it once in
    /// <c>MauiProgram.CreateMauiApp()</c>: <c>builder.UseKumikoUI();</c>.
    /// </summary>
    /// <remarks>
    /// <para>This initializes SkiaSharp for MAUI, registers the handlers of the views the grid draws on
    /// (<c>SKCanvasView</c>, and <c>SKGLView</c> through SkiaSharp's own registration), and
    /// bridges the fonts registered with <c>ConfigureFonts</c> into the grid renderer, so a font alias
    /// such as <c>fonts.AddFont("MyFont.ttf", "MyAlias")</c> can be used as
    /// <c>KumikoFont.Family = "MyAlias"</c>. Fonts added with <c>ConfigureFonts</c> before or after this
    /// call are both bridged. Without the call, a <see cref="KumikoDataGrid"/> throws an
    /// <see cref="InvalidOperationException"/> when it is attached to a handler.</para>
    /// <para>Fonts that cannot be read from the app package are skipped; the grid then falls back to the
    /// system font manager for that family. Fonts can also be registered by hand with
    /// <c>SkiaFontRegistrar</c>.</para>
    /// </remarks>
    /// <param name="builder">The MAUI app builder.</param>
    /// <returns>The same <paramref name="builder"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <c>null</c>.</exception>
    public static MauiAppBuilder UseKumikoUI(this MauiAppBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseSkiaSharp();
        builder.ConfigureMauiHandlers(handlers => handlers.AddHandler<SKCanvasView, SKCanvasViewHandler>());

        // Make sure the font services exist (a later ConfigureFonts call leaves them alone), then
        // decorate the registrar so every font registered through ConfigureFonts also reaches Skia.
        builder.ConfigureFonts();
        builder.Services.Replace(ServiceDescriptor.Singleton<IFontRegistrar>(
            sp => new KumikoFontRegistrar(new FontRegistrar(sp.GetRequiredService<IEmbeddedFontLoader>(), sp))));

        builder.Services.TryAddSingleton<KumikoUIHostingMarker>();
        return builder;
    }

    /// <summary>
    /// Throws when <see cref="UseKumikoUI"/> did not run for the app that owns <paramref name="services"/>.
    /// </summary>
    /// <param name="services">The app's service provider, or <c>null</c> when it is not known yet.</param>
    internal static void EnsureConfigured(IServiceProvider? services)
    {
        if (services?.GetService<KumikoUIHostingMarker>() is null)
        {
            throw new InvalidOperationException(
                "KumikoUI is not configured. Call builder.UseKumikoUI() in MauiProgram.CreateMauiApp().");
        }
    }
}

/// <summary>Service that records that <see cref="KumikoUIHostingExtensions.UseKumikoUI"/> ran.</summary>
internal sealed class KumikoUIHostingMarker
{
}

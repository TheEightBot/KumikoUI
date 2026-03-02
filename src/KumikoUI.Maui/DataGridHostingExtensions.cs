using SkiaSharp.Views.Maui.Controls.Hosting;

namespace KumikoUI.Maui;

/// <summary>
/// Extension methods for registering the KumikoUI with a MAUI app.
/// </summary>
public static class KumikoUIHostingExtensions
{
    /// <summary>
    /// Register the SkiaSharp-powered KumikoUI with the MAUI app builder.
    /// Call this in MauiProgram.cs: builder.UseSkiaKumikoUI();
    /// </summary>
    public static MauiAppBuilder UseSkiaKumikoUI(this MauiAppBuilder builder)
    {
        builder.UseSkiaSharp();
        return builder;
    }
}

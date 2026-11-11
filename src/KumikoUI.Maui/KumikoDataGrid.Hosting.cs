namespace KumikoUI.Maui;

public partial class KumikoDataGrid
{
    /// <summary>
    /// Fails fast with a KumikoUI-named message when <c>UseKumikoUI()</c> was not called, instead of
    /// letting the canvas fail later with an unrelated handler error.
    /// </summary>
    private void EnsureHostingConfigured()
    {
        if (Handler is not null)
            KumikoUIHostingExtensions.EnsureConfigured(Handler.MauiContext?.Services);
    }
}

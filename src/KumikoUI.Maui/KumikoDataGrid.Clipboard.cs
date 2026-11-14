using KumikoUI.Core.Input;
using MauiClipboard = Microsoft.Maui.ApplicationModel.DataTransfer.Clipboard;

namespace KumikoUI.Maui;

/// <summary>
/// Clipboard support (#44): Ctrl/Cmd+C copies the selected rows as tab-separated text, and copy, cut, paste and
/// select-all work inside the cell editors and the filter popup's search box.
/// </summary>
/// <remarks>
/// Core does the work (see <see cref="IKumikoClipboard"/>); this host supplies the clipboard and gets the keys to
/// Core.
/// <list type="bullet">
/// <item><b>iOS and Mac Catalyst</b>: Command+A/C/X/V/Z (and Shift+Command+Z) are <c>UIKeyCommand</c>s on the
/// keyboard responder, and the responder answers the standard edit actions, so the Edit menu on Mac Catalyst
/// works too.</item>
/// <item><b>Windows</b>: Ctrl+A/C/X/V/Z/Y reach Core from the grid's platform view with the other keys
/// (<c>KumikoDataGrid.TextInput.Windows.cs</c>); Ctrl+Alt is AltGr and types characters instead.</item>
/// <item><b>Android</b>: the grid's input view sends Ctrl+A/C/X/V/Z/Y (and Shift+Ctrl+Z) from a hardware keyboard
/// to Core, and the input method's select all, cut, copy and paste actions arrive as the same shortcuts. Text a
/// keyboard pastes from its clipboard strip is committed text, which enters the editor as one paste. Cut and copy
/// of editor text from a touch context menu are not available.</item>
/// </list>
/// </remarks>
public partial class KumikoDataGrid
{
    /// <summary>
    /// The clipboard used for copy, cut and paste. Defaults to the platform clipboard
    /// (<c>Clipboard.Default</c>); assign another to redirect them, or <c>null</c> to turn them off.
    /// </summary>
    public IKumikoClipboard? Clipboard
    {
        get => _inputController.Clipboard;
        set => _inputController.Clipboard = value;
    }

    /// <summary>
    /// Whether Ctrl/Cmd+C on selected rows starts the copied text with a row of column headers. Default
    /// <c>false</c>. Plain property (not bindable).
    /// </summary>
    public bool ClipboardCopyIncludesHeaders
    {
        get => _inputController.CopyIncludesHeaders;
        set => _inputController.CopyIncludesHeaders = value;
    }

    private void InitializeClipboard()
    {
        _inputController.Clipboard = new MauiKumikoClipboard();
    }

    /// <summary>The MAUI clipboard behind <see cref="IKumikoClipboard"/>.</summary>
    private sealed class MauiKumikoClipboard : IKumikoClipboard
    {
        public bool TrySetText(string text)
        {
            try
            {
                // The platform implementations place the text before they return the task
                var task = MauiClipboard.Default.SetTextAsync(text);
                return task.IsCompletedSuccessfully;
            }
            catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or UnauthorizedAccessException or InvalidOperationException or Microsoft.Maui.ApplicationModel.FeatureNotSupportedException or NotSupportedException or TaskCanceledException)
            {
                return false;
            }
        }

        public async Task<string?> GetTextAsync()
        {
            try
            {
                return MauiClipboard.Default.HasText ? await MauiClipboard.Default.GetTextAsync() : null;
            }
            catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or UnauthorizedAccessException or InvalidOperationException or Microsoft.Maui.ApplicationModel.FeatureNotSupportedException or NotSupportedException or TaskCanceledException)
            {
                return null;
            }
        }
    }
}

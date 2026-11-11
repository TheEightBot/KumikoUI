using KumikoUI.Core.Input;
using Windows.ApplicationModel.DataTransfer;

namespace KumikoUI.Uno;

/// <summary>
/// Clipboard support (#44): Ctrl/Cmd+C copies the selected rows as tab-separated text, and copy, cut, paste and
/// select-all work inside the cell editors and the filter popup's search box.
/// </summary>
/// <remarks>
/// Core does the work (see <see cref="IKumikoClipboard"/>). The hidden proxy <c>TextBox</c>'s <c>KeyDown</c>
/// already forwards Ctrl (Command on Apple platforms and in the browser) with A, C, V, X, Z and Y to Core and
/// marks them handled, so the same keys work on every Uno head; this file supplies the clipboard.
/// </remarks>
public partial class KumikoDataGrid
{
    /// <summary>
    /// The clipboard used for copy, cut and paste. Defaults to the platform clipboard
    /// (<c>Windows.ApplicationModel.DataTransfer.Clipboard</c>); assign another to redirect them, or <c>null</c>
    /// to turn them off.
    /// </summary>
    public IKumikoClipboard? Clipboard
    {
        get => _inputController.Clipboard;
        set => _inputController.Clipboard = value;
    }

    /// <summary>
    /// Whether Ctrl/Cmd+C on selected rows starts the copied text with a row of column headers. Default
    /// <c>false</c>. Plain property (not a dependency property).
    /// </summary>
    public bool ClipboardCopyIncludesHeaders
    {
        get => _inputController.CopyIncludesHeaders;
        set => _inputController.CopyIncludesHeaders = value;
    }

    private void InitializeClipboard() => _inputController.Clipboard = new UnoKumikoClipboard();

    /// <summary>The Uno clipboard behind <see cref="IKumikoClipboard"/>.</summary>
    private sealed class UnoKumikoClipboard : IKumikoClipboard
    {
        public bool TrySetText(string text)
        {
            try
            {
                var package = new DataPackage();
                package.SetText(text);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
                return true;
            }
            catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException or TaskCanceledException)
            {
                return false;
            }
        }

        public async Task<string?> GetTextAsync()
        {
            try
            {
                var view = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
                return view.Contains(StandardDataFormats.Text) ? await view.GetTextAsync() : null;
            }
            catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException or TaskCanceledException)
            {
                return null;
            }
        }
    }
}

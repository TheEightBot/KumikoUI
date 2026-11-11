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
/// <item><b>Windows</b>: the hidden <c>Entry</c> is a WinUI <c>TextBox</c>; its <c>PreviewKeyDown</c> sends
/// Ctrl+A/C/X/V/Z/Y to Core and handles them, so the box never acts on its sentinel text.</item>
/// <item><b>Android</b>: with a hardware keyboard the hidden <c>EditText</c>'s key listener does the same.
/// The on-screen keyboard has no Ctrl key, so there is no copy of the selection from it; text a keyboard
/// pastes (a clipboard suggestion, long-press paste) arrives as typed characters and enters the editor as
/// typing. Cut and copy of editor text from the touch context menu are not available.</item>
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
#if ANDROID || WINDOWS
        _keyboardProxy.HandlerChanged += OnKeyboardProxyHandlerChanged_Clipboard;
#endif
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
            catch
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
            catch
            {
                return null;
            }
        }
    }

#if ANDROID
    private Android.Widget.EditText? _clipboardEditText;

    private void OnKeyboardProxyHandlerChanged_Clipboard(object? sender, EventArgs e)
    {
        if (_clipboardEditText != null) _clipboardEditText.KeyPress -= OnAndroidProxyKeyPress;
        _clipboardEditText = _keyboardProxy.Handler?.PlatformView as Android.Widget.EditText;
        if (_clipboardEditText != null) _clipboardEditText.KeyPress += OnAndroidProxyKeyPress;
    }

    // Hardware keyboard: Ctrl+A/C/X/V/Z/Y go to Core, not to the EditText (which would act on its sentinel).
    private void OnAndroidProxyKeyPress(object? sender, Android.Views.View.KeyEventArgs e)
    {
        e.Handled = false;
        if (e.Event is not { Action: Android.Views.KeyEventActions.Down } press || !press.IsCtrlPressed) return;

        KumikoKey? key = e.KeyCode switch
        {
            Android.Views.Keycode.A => KumikoKey.A,
            Android.Views.Keycode.C => KumikoKey.C,
            Android.Views.Keycode.X => KumikoKey.X,
            Android.Views.Keycode.V => KumikoKey.V,
            Android.Views.Keycode.Z => KumikoKey.Z,
            Android.Views.Keycode.Y => KumikoKey.Y,
            _ => null
        };
        if (key == null) return;

        var modifiers = InputModifiers.Control;
        if (press.IsShiftPressed) modifiers |= InputModifiers.Shift;
        SendKeyEvent(key.Value, null, modifiers);
        e.Handled = true;
    }
#elif WINDOWS
    private Microsoft.UI.Xaml.Controls.TextBox? _clipboardTextBox;

    private void OnKeyboardProxyHandlerChanged_Clipboard(object? sender, EventArgs e)
    {
        if (_clipboardTextBox != null) _clipboardTextBox.PreviewKeyDown -= OnWindowsProxyPreviewKeyDown;
        _clipboardTextBox = _keyboardProxy.Handler?.PlatformView as Microsoft.UI.Xaml.Controls.TextBox;
        if (_clipboardTextBox != null) _clipboardTextBox.PreviewKeyDown += OnWindowsProxyPreviewKeyDown;
    }

    // Ctrl+A/C/X/V/Z/Y go to Core, not to the TextBox (which would act on its sentinel, or paste text as typing).
    private void OnWindowsProxyPreviewKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        KumikoKey? key = e.Key switch
        {
            Windows.System.VirtualKey.A => KumikoKey.A,
            Windows.System.VirtualKey.C => KumikoKey.C,
            Windows.System.VirtualKey.X => KumikoKey.X,
            Windows.System.VirtualKey.V => KumikoKey.V,
            Windows.System.VirtualKey.Z => KumikoKey.Z,
            Windows.System.VirtualKey.Y => KumikoKey.Y,
            _ => null
        };
        if (key == null) return;

        static bool IsDown(Windows.System.VirtualKey k) =>
            (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(k) & Windows.UI.Core.CoreVirtualKeyStates.Down)
                == Windows.UI.Core.CoreVirtualKeyStates.Down;

        if (!IsDown(Windows.System.VirtualKey.Control)) return;

        var modifiers = InputModifiers.Control;
        if (IsDown(Windows.System.VirtualKey.Shift)) modifiers |= InputModifiers.Shift;
        SendKeyEvent(key.Value, null, modifiers);
        e.Handled = true;
    }
#endif
}

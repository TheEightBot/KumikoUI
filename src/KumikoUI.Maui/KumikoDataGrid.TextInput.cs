#if MACCATALYST || IOS
using CoreGraphics;
using KumikoUI.Core.Input;
using UIKit;
#endif

namespace KumikoUI.Maui;

/// <summary>
/// Text input on iOS and Mac Catalyst (#40): <c>KeyInputResponder</c> implements <c>UITextInput</c> and
/// <c>AppleTextInputBridge</c> applies it to the open editor's text session (see docs/TEXT-INPUT.md). Android
/// has its own input view (KumikoDataGrid.TextInput.Android.cs, #41) and Windows the grid's platform view
/// (KumikoDataGrid.TextInput.Windows.cs, #42).
/// </summary>
public partial class KumikoDataGrid
{
    // Tells the native text input that an editor may have opened or closed, or that Core changed its text. Called
    // after a key, on a redraw request and when the keyboard focus changes; it does nothing on other platforms.
    partial void SyncNativeTextInput();

#if MACCATALYST || IOS
    private AppleTextInputBridge? _textInputBridge;

    private KeyInputResponder CreateNativeTextInput()
    {
        _textInputBridge = new AppleTextInputBridge(
            () => _inputController.ActiveTextInput,
            () => _editSession.ActiveEditor?.Bounds,
            SendKeyEvent);
        var responder = new KeyInputResponder(
            _textInputBridge,
            (key, modifiers) => SendKeyEvent(key, null, modifiers),
            () => _canvasView.Handler?.PlatformView as UIView)
        {
            Frame = new CGRect(0, 0, 0, 0),
            Alpha = 0f,
            // Tab past the last (first) tab stop is left to UIKit so focus can leave the grid.
            CanHandleTab = backwards => _inputController.WouldHandleTab(_selection, _dataSource, backwards),
            // Command+F opens the search panel only when the grid allows it (#317)
            CanHandleFind = () => _inputController.AllowSearchPanel || _inputController.IsSearchPanelOpen,
            // Command+Backspace deletes the selected rows only when the grid allows it and no editor is open (#328)
            CanHandleDelete = () => _inputController.AllowDeleting && _selection.SelectedRows.Count > 0 && !_editSession.IsEditing,
        };
        return responder;
    }

    partial void SyncNativeTextInput()
    {
        if (_nativeKeyInput != null) _textInputBridge?.Sync();
        ReleaseKeyboardInsetAfterEdit(); // KumikoDataGrid.Keyboard.Apple.cs
    }
#endif
}

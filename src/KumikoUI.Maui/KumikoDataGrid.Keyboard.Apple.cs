#if MACCATALYST || IOS
using CoreGraphics;
using Foundation;
using KumikoUI.Core.Layout;
using KumikoUI.Core.Models;
using UIKit;

namespace KumikoUI.Maui;

/// <summary>
/// Keeping the edited cell above the on-screen keyboard on iOS and Mac Catalyst (#258). UIKit does not move a
/// custom view for the keyboard, so the grid watches <c>UIKeyboardWillChangeFrame</c> and <c>UIKeyboardWillHide</c>,
/// works out how much of itself the keyboard covers, scrolls the edited cell above it and gives the scroll range a
/// bottom inset (<see cref="ScrollState.BottomInset"/>) for as long as the keyboard is up, so the last rows can scroll
/// clear of it. The geometry is <see cref="KumikoKeyboardAvoidance"/>, shared with Android and Uno.
/// </summary>
/// <remarks>
/// A keyboard that does not cover the grid's bottom edge (a floating iPad keyboard, or none, as on Mac Catalyst where
/// no on-screen keyboard appears) changes nothing. The inset ends with the edit or the keyboard, whichever is first.
/// </remarks>
public partial class KumikoDataGrid
{
    private NSObject? _keyboardFrameObserver;
    private NSObject? _keyboardHideObserver;
    private CGRect _keyboardFrame = CGRect.Empty; // screen coordinates; empty while the keyboard is hidden
    private CellPosition _keyboardCheckedCell = new(-1, -1);
    private Action? _keyboardCheck;
    private Action? _keyboardRelease;
    private bool _keyboardReleasePending;

    /// <summary>Starts watching the keyboard's frame (once; the grid's platform view must be set up).</summary>
    private void ObserveKeyboardFrame()
    {
        if (_keyboardFrameObserver != null) return;
        _keyboardFrameObserver = UIKeyboard.Notifications.ObserveWillChangeFrame(OnKeyboardWillChangeFrame);
        _keyboardHideObserver = UIKeyboard.Notifications.ObserveWillHide(OnKeyboardWillHide);
    }

    /// <summary>Stops watching the keyboard and gives the scroll range its inset back.</summary>
    private void StopObservingKeyboardFrame()
    {
        _keyboardFrameObserver?.Dispose();
        _keyboardHideObserver?.Dispose();
        _keyboardFrameObserver = null;
        _keyboardHideObserver = null;
        ResetKeyboardInset();
    }

    private void OnKeyboardWillChangeFrame(object? sender, UIKeyboardEventArgs e)
    {
        _keyboardFrame = e.FrameEnd;
        _keyboardCheckedCell = new CellPosition(-1, -1); // check the edited cell against the new frame
        InvalidateSurface();                              // after the next frame, once the editor is placed
    }

    private void OnKeyboardWillHide(object? sender, UIKeyboardEventArgs e) => ResetKeyboardInset();

    private void ResetKeyboardInset()
    {
        _keyboardFrame = CGRect.Empty;
        _keyboardCheckedCell = new CellPosition(-1, -1);
        if (_scroll.BottomInset == 0) return;
        _scroll.BottomInset = 0;
        _scroll.ClampOffset();
        InvalidateSurface();
    }

    /// <summary>The height of the grid's bottom the keyboard covers now, from its frame and the grid's place on screen.</summary>
    private float GetKeyboardObscuredHeight()
    {
        if (_keyboardFrame.IsEmpty || Handler?.PlatformView is not UIView { Window: { } window } view) return 0;
        var keyboard = view.ConvertRectFromCoordinateSpace(_keyboardFrame, window.Screen.CoordinateSpace);
        var bounds = view.Bounds;
        // A little slack: the keyboard's bottom is the screen's, and the grid may end exactly there
        return KumikoKeyboardAvoidance.GetObscuredHeight(
            (float)bounds.Top, (float)bounds.Bottom, (float)keyboard.Top, (float)keyboard.Bottom + 1f);
    }

    // After a frame (the renderer has placed the editor over its cell): a newly edited cell is checked once against
    // the keyboard, so a user who scrolls the cell away afterwards is not pulled back.
    partial void OnFrameRendered()
    {
        if (_keyboardFrame.IsEmpty || !_editSession.IsEditing) return;
        var cell = _editSession.EditCell;
        if (cell.Equals(_keyboardCheckedCell)) return;
        _keyboardCheckedCell = cell;
        Dispatcher.Dispatch(_keyboardCheck ??= ScrollEditedCellAboveKeyboard);
    }

    // The edit ended while the keyboard stays up (another control took focus): the inset has no cell to serve. Checked
    // after the current input event, because Tab ends one edit and begins the next inside it.
    private void ReleaseKeyboardInsetAfterEdit()
    {
        if (_scroll.BottomInset == 0 || _editSession.IsEditing || _keyboardReleasePending) return;
        _keyboardReleasePending = true;
        Dispatcher.Dispatch(_keyboardRelease ??= ReleaseKeyboardInsetIfNotEditing);
    }

    private void ReleaseKeyboardInsetIfNotEditing()
    {
        _keyboardReleasePending = false;
        if (_wasCleanedUp || _editSession.IsEditing || _scroll.BottomInset == 0) return;
        _keyboardCheckedCell = new CellPosition(-1, -1);
        _scroll.BottomInset = 0;
        _scroll.ClampOffset();
        InvalidateSurface();
    }

    private void ScrollEditedCellAboveKeyboard()
    {
        if (_wasCleanedUp) return;
        float obscured = _editSession.IsEditing ? GetKeyboardObscuredHeight() : 0;
        float before = _scroll.BottomInset;
        float moved = KumikoKeyboardAvoidance.ScrollEditorIntoView(
            _scroll, _dataSource, _style, _editSession, obscured, applyInset: true);
        if (moved > 0 || before != _scroll.BottomInset)
            InvalidateSurface();
    }
}
#endif

using KumikoUI.Core.Layout;
using KumikoUI.Core.Models;
using Microsoft.UI.Xaml;

namespace KumikoUI.Uno;

/// <summary>
/// Keeping the edited cell above the on-screen keyboard on the Uno heads (#258). The grid follows
/// <c>InputPane.Showing</c> and <c>Hiding</c> (iOS, Android and, where the browser reports a keyboard, WebAssembly),
/// works out how much of itself the pane's <c>OccludedRect</c> covers, scrolls the edited cell above it and gives the
/// scroll range a bottom inset (<see cref="ScrollState.BottomInset"/>) while the keyboard is up, so the last rows can
/// scroll clear of it. The geometry is <see cref="KumikoKeyboardAvoidance"/>, shared with the MAUI hosts.
/// </summary>
/// <remarks>
/// When the platform already resizes the window for the keyboard, the grid's bottom is the keyboard's top and the
/// covered height is 0: nothing changes. On heads without an input pane (desktop, Windows) the events never fire.
/// </remarks>
public partial class KumikoDataGrid
{
    private Windows.Foundation.Rect _occludedRect = Windows.Foundation.Rect.Empty; // the keyboard's rectangle in window coordinates; empty while hidden
    private CellPosition _keyboardCheckedCell = new(-1, -1);
    private bool _keyboardCheckQueued;
    private bool _keyboardObserved;
    private readonly Microsoft.UI.Dispatching.DispatcherQueueHandler _scrollAboveKeyboard;

#if HAS_UNO
    private Windows.UI.ViewManagement.InputPane? _inputPane;
#endif

    /// <summary>Starts following the input pane (idempotent); called from <c>OnLoaded</c>.</summary>
    private void ObserveInputPane()
    {
#if HAS_UNO
        if (_keyboardObserved) return;
        try
        {
#pragma warning disable Uno0001 // Flagged on the native-renderer reference assemblies; the Skia runtime every Uno head of this library uses raises them.
            var pane = Windows.UI.ViewManagement.InputPane.GetForCurrentView();
            pane.Showing += OnInputPaneShowing;
            pane.Hiding += OnInputPaneHiding;
#pragma warning restore Uno0001
            _inputPane = pane;
            _keyboardObserved = true;
        }
        catch (Exception ex) when (ex is NotImplementedException or InvalidOperationException or PlatformNotSupportedException)
        {
            // No input pane on this head.
        }
#endif
    }

    /// <summary>Stops following the input pane and gives the scroll range its inset back; called from <c>OnUnloaded</c>.</summary>
    private void StopObservingInputPane()
    {
#if HAS_UNO
        if (_inputPane is { } pane)
        {
            pane.Showing -= OnInputPaneShowing;
            pane.Hiding -= OnInputPaneHiding;
        }
        _inputPane = null;
#endif
        _keyboardObserved = false;
        ResetKeyboardInset();
    }

#if HAS_UNO
    private void OnInputPaneShowing(Windows.UI.ViewManagement.InputPane sender, Windows.UI.ViewManagement.InputPaneVisibilityEventArgs e)
    {
        _occludedRect = e.OccludedRect;
        _keyboardCheckedCell = new CellPosition(-1, -1); // check the edited cell against the new rectangle
        InvalidateSurface();                              // after the next frame, once the editor is placed
    }

    private void OnInputPaneHiding(Windows.UI.ViewManagement.InputPane sender, Windows.UI.ViewManagement.InputPaneVisibilityEventArgs e) =>
        ResetKeyboardInset();
#endif

    private void ResetKeyboardInset()
    {
        _occludedRect = Windows.Foundation.Rect.Empty;
        _keyboardCheckedCell = new CellPosition(-1, -1);
        if (_scroll.BottomInset == 0) return;
        _scroll.BottomInset = 0;
        _scroll.ClampOffset();
        InvalidateSurface();
    }

    /// <summary>The height of the grid's bottom the keyboard covers now, from the pane's rectangle and the grid's place in the window.</summary>
    private float GetKeyboardObscuredHeight()
    {
        if (_occludedRect.IsEmpty || _occludedRect.Height <= 0 || XamlRoot?.Content is not UIElement root) return 0;
        try
        {
            double top = TransformToVisual(root).TransformPoint(new Windows.Foundation.Point(0, 0)).Y;
            // A little slack: the keyboard's bottom is the window's, and the grid may end exactly there
            return KumikoKeyboardAvoidance.GetObscuredHeight(
                (float)top, (float)(top + ActualHeight), (float)_occludedRect.Top, (float)_occludedRect.Bottom + 1f);
        }
        catch (ArgumentException)
        {
            return 0; // not in the same visual tree (being unloaded)
        }
    }

    // After a frame (the renderer has placed the editor over its cell): a newly edited cell is checked once against
    // the keyboard, so a user who scrolls the cell away afterwards is not pulled back.
    private void OnKeyboardFrameRendered()
    {
        if (_occludedRect.IsEmpty || _keyboardCheckQueued || !_editSession.IsEditing) return;
        var cell = _editSession.EditCell;
        if (cell.Equals(_keyboardCheckedCell)) return;
        _keyboardCheckedCell = cell;
        if (DispatcherQueue is { } queue)
            _keyboardCheckQueued = queue.TryEnqueue(_scrollAboveKeyboard);
    }

    // The edit ended while the keyboard stays up: the inset has no cell to serve. Checked after the current input
    // event, because Tab ends one edit and begins the next inside it.
    private void QueueKeyboardInsetRelease()
    {
        if (_scroll.BottomInset == 0 || _keyboardCheckQueued || DispatcherQueue is not { } queue) return;
        _keyboardCheckQueued = queue.TryEnqueue(_scrollAboveKeyboard);
    }

    private void ScrollEditedCellAboveKeyboard()
    {
        _keyboardCheckQueued = false;
        if (_isUnloaded) return;
        float obscured = _editSession.IsEditing ? GetKeyboardObscuredHeight() : 0;
        float before = _scroll.BottomInset;
        float moved = KumikoKeyboardAvoidance.ScrollEditorIntoView(
            _scroll, _dataSource, _style, _editSession, obscured, applyInset: true);
        if (moved > 0 || before != _scroll.BottomInset)
            InvalidateSurface();
    }
}

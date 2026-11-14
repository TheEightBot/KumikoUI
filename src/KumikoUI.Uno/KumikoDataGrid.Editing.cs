using KumikoUI.Core.Input;
using KumikoUI.Uno.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace KumikoUI.Uno;

/// <summary>
/// Keyboard and text input for <see cref="KumikoDataGrid"/>: a hidden <c>TextBox</c> proxy and the text-input
/// adapter (<see cref="KumikoTextInputAdapter"/>, <c>docs/TEXT-INPUT.md</c>) between it and Core.
///
/// <para>
/// <strong>Why a TextBox:</strong> on iOS, Android and in the browser, focusing a non-text element such as this
/// control does not summon the on-screen keyboard, and on WASM/Skia its <c>KeyDown</c> and
/// <c>CharacterReceived</c> don't fire. A real text box is the one element every head routes the keyboard and the
/// input method to.
/// </para>
///
/// <para><strong>Design:</strong></para>
/// <list type="bullet">
///   <item>The proxy mirrors the open editor's text and selection (it is empty with none open). Its synchronous
///     <c>TextChanging</c> event reports each change the keyboard or input method makes; the adapter replays it on
///     Core's <see cref="ITextInputSession"/> (or, with no editor open, sends typed characters to Core, which can
///     start an edit), then sets the proxy back to Core's text and selection when they differ.</item>
///   <item><c>TextCompositionStarted</c>/<c>Changed</c>/<c>Ended</c> carry input-method composition (Japanese,
///     Chinese, Korean, dead keys) to <see cref="ITextInputSession.SetComposition"/> and
///     <see cref="ITextInputSession.CommitComposition"/>; the proxy is never reset under a composition.</item>
///   <item><c>KeyDown</c> sends command keys to Core and marks a key handled only when Core handled it, so the
///     grid is no keyboard trap: Tab past the last (first) tab stop moves focus out of the grid, and Escape with
///     nothing to cancel reaches the page.</item>
///   <item>The proxy is invisible to the user, to pointer hit-testing and to assistive technology, and its focus
///     never scrolls the page (see <see cref="SetupInputProxy"/>).</item>
/// </list>
/// </summary>
public partial class KumikoDataGrid
{
    private readonly TextBox _inputProxy = new TextBox
    {
        // Opacity=0 hides the proxy from the eye only: an element with Opacity=0 still takes pointer input and is
        // still exposed to UI Automation, so it is also not hit-testable (focus is programmatic) and is marked Raw
        // in SetupInputProxy. It stays Visible, since a collapsed element cannot take focus.
        Visibility = Visibility.Visible,
        Opacity = 0,
        Width = 1,
        Height = 1,
        // The minimums beat the default style's 64x32, which would otherwise leave an invisible box in the grid.
        MinWidth = 0,
        MinHeight = 0,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        IsHitTestVisible = false,
        BorderThickness = new Thickness(0),
        Padding = new Thickness(0),
        // iOS Safari zooms the page when an input with a font smaller than 16px gains focus (WASM).
        FontSize = 16,
        // A grid cell is data: no autocorrect or suggestions rewriting what the user types.
        IsSpellCheckEnabled = false,
        IsTextPredictionEnabled = false,
        AcceptsReturn = false,
        // Focus() does nothing on an element that is not a tab stop, and Uno's input method only serves text boxes
        // that are tab stops. Set once here; its TabIndex is left at the default.
        IsTabStop = true,
    };

    private readonly KumikoTextInputAdapter _textInput;

    // Syncs the proxy once the current input event is done: a text box can still move its own caret after
    // TextChanging returns, which would put the next keystroke in the wrong place.
    private readonly Microsoft.UI.Dispatching.DispatcherQueueHandler _syncTextInput;
    private bool _textInputSyncQueued;

    // True while the proxy raises TextChanging for a change of its own (see OnProxyTextChanging).
    private bool _proxyChangingText;

    // ── Proxy setup / teardown ───────────────────────────────────

    /// <summary>
    /// Adds the proxy TextBox to the grid's private content panel and creates the text-input adapter. Called once
    /// from the constructor, after the drawing surface is added. Event handlers are subscribed idempotently in
    /// <see cref="AttachInputProxy"/> (from <c>AttachInputHandlers</c>) and removed in
    /// <see cref="TeardownInputProxy"/>.
    /// </summary>
    private KumikoTextInputAdapter SetupInputProxy()
    {
        // Raw keeps Narrator/VoiceOver/TalkBack from announcing an empty editable text box; the grid itself is
        // exposed through KumikoDataGridAutomationPeer.
        AutomationProperties.SetAccessibilityView(_inputProxy, AccessibilityView.Raw);
        _root.Children.Add(_inputProxy);
        return new KumikoTextInputAdapter(new TextBoxTextInputProxy(this), new GridTextInputTarget(this));
    }

    /// <summary>Subscribes the proxy's events. Idempotent: Uno raises <c>Loaded</c> again on navigation back.</summary>
    private void AttachInputProxy()
    {
        TeardownInputProxy();
        _inputProxy.TextChanging += OnProxyTextChanging;
        _inputProxy.KeyDown += OnProxyKeyDown;
        _inputProxy.Paste += OnProxyPaste;
        _inputProxy.BringIntoViewRequested += OnProxyBringIntoViewRequested;
#pragma warning disable Uno0001 // Flagged on the native-renderer reference assemblies; the Skia runtime every Uno head of this library uses raises them.
        _inputProxy.TextCompositionStarted += OnProxyTextCompositionStarted;
        _inputProxy.TextCompositionChanged += OnProxyTextCompositionChanged;
        _inputProxy.TextCompositionEnded += OnProxyTextCompositionEnded;
#pragma warning restore Uno0001
    }

    /// <summary>
    /// Detaches the proxy's events. Called from <c>DetachInputHandlers</c> (<c>OnUnloaded</c>) so no delegate
    /// roots the control across navigation.
    /// </summary>
    private void TeardownInputProxy()
    {
        _inputProxy.TextChanging -= OnProxyTextChanging;
        _inputProxy.KeyDown -= OnProxyKeyDown;
        _inputProxy.Paste -= OnProxyPaste;
        _inputProxy.BringIntoViewRequested -= OnProxyBringIntoViewRequested;
#pragma warning disable Uno0001 // See AttachInputProxy.
        _inputProxy.TextCompositionStarted -= OnProxyTextCompositionStarted;
        _inputProxy.TextCompositionChanged -= OnProxyTextCompositionChanged;
        _inputProxy.TextCompositionEnded -= OnProxyTextCompositionEnded;
#pragma warning restore Uno0001
        _textInput.Detach();
    }

    // ── Focus and the soft keyboard ──────────────────────────────

    /// <summary>
    /// Mirrors the open editor into the proxy and focuses it, so the soft keyboard appears on iOS/Android and key
    /// events reach the proxy on every head. Focusing an element that already has focus does not show the soft
    /// keyboard again, so after a touch edit (which hid it but kept the focus) it is shown explicitly.
    /// </summary>
    private void FocusKeyboardInput()
    {
        _textInput.Sync();
        bool hadFocus = _inputProxy.FocusState != FocusState.Unfocused;
        _inputProxy.Focus(FocusState.Programmatic);
        // Taking focus can move the text box's caret (the Android and iOS heads put it at the start), and the
        // input method types where the text box says the selection is: typing over a selected cell value would
        // insert before it. Put the editor's selection back now and again once the focus change has settled.
        _textInput.Sync();
        QueueTextInputSync();
        if (_editSession.IsEditing && _inputController.ActiveTextInput == null)
        {
            // An editor that takes no typed text (a scroll picker, a date picker, a combo box) needs the proxy's focus
            // for hardware keys only: focusing a text box raises the soft keyboard on Android and iOS, so it is hidden
            // again, now and once the focus change has settled.
            HideSoftKeyboard();
            DispatcherQueue?.TryEnqueue(HideSoftKeyboardForNonTextEditor);
        }
        else if (hadFocus && _lastPointerWasTouch)
        {
            ShowSoftKeyboard();
        }
    }

    private void HideSoftKeyboardForNonTextEditor()
    {
        if (_editSession.IsEditing && _inputController.ActiveTextInput == null)
            HideSoftKeyboard();
    }

    private static void ShowSoftKeyboard()
    {
#if HAS_UNO
        try
        {
#pragma warning disable Uno0001 // See AttachInputProxy.
            Windows.UI.ViewManagement.InputPane.GetForCurrentView().TryShow();
#pragma warning restore Uno0001
        }
        catch (Exception ex) when (ex is NotImplementedException or InvalidOperationException)
        {
            // No input pane on this head.
        }
#endif
    }

    private static void HideSoftKeyboard()
    {
#if HAS_UNO
        try
        {
            Windows.UI.ViewManagement.InputPane.GetForCurrentView().TryHide();
        }
        catch (Exception ex) when (ex is NotImplementedException or InvalidOperationException)
        {
            // No input pane on this head.
        }
#endif
    }

    // ── Proxy events ─────────────────────────────────────────────

    private void OnProxyTextChanging(TextBox sender, TextBoxTextChangingEventArgs e)
    {
        // While the text box raises TextChanging its displayed text is still the old one: selecting past that
        // text's end (typing at the end of a cell) throws inside Uno, which crashes the Android input connection.
        // The adapter leaves the proxy alone meanwhile; the queued sync puts the editor's text and selection back.
        bool outer = _proxyChangingText;
        _proxyChangingText = true;
        try
        {
            _textInput.OnProxyTextChanged();
        }
        finally
        {
            _proxyChangingText = outer;
        }
        QueueTextInputSync();
    }

    private void OnProxyTextCompositionStarted(TextBox sender, TextCompositionStartedEventArgs e) =>
        _textInput.OnCompositionStarted(e.StartIndex, e.Length);

    private void OnProxyTextCompositionChanged(TextBox sender, TextCompositionChangedEventArgs e) =>
        _textInput.OnCompositionChanged(e.StartIndex, e.Length);

    private void OnProxyTextCompositionEnded(TextBox sender, TextCompositionEndedEventArgs e)
    {
        _textInput.OnCompositionEnded();
        QueueTextInputSync();
    }

    /// <summary>
    /// Core owns the clipboard (Ctrl/Cmd+V reaches the editor through <c>EditSession.ClipboardPasteRequested</c>):
    /// a paste Core left alone must not reach the proxy, where it would arrive as typing.
    /// </summary>
    private void OnProxyPaste(object sender, TextControlPasteEventArgs e) => e.Handled = true;

    /// <summary>Focusing the proxy must not scroll a page that hosts the grid in a <c>ScrollViewer</c>.</summary>
    private void OnProxyBringIntoViewRequested(UIElement sender, BringIntoViewRequestedEventArgs e) => e.Handled = true;

    private void QueueTextInputSync()
    {
        if (_textInputSyncQueued || DispatcherQueue is not { } queue) return;
        _textInputSyncQueued = queue.TryEnqueue(_syncTextInput);
    }

    private void SyncTextInputQueued()
    {
        _textInputSyncQueued = false;
        _textInput.Sync();
    }

    private void OnProxyKeyDown(object sender, KeyRoutedEventArgs e)
    {
        // Text keys (printable keys, plain letters, Space in an editor, every key during a composition) are left to
        // the text box; the adapter picks the text up from TextChanging.
        if (_textInput.OnKeyDown(InputMapping.ToGridKey(e.Key)) is not { } result) return;

        // A key is marked handled only when Core handled it, so the grid is no keyboard trap: an unhandled Tab
        // (no current cell, or past the last/first tab stop) moves focus out of the grid, and any other unhandled
        // key (Escape with nothing to cancel, Enter on a read-only cell) reaches the page.
        switch (result.Outcome)
        {
            case InputMapping.ProxyKeyOutcome.Handled:
                e.Handled = true;
                break;
            case InputMapping.ProxyKeyOutcome.MoveFocusOut:
                MoveFocusOutOfGrid(result.Backwards);
                e.Handled = true;
                break;
        }
    }

    /// <summary>
    /// Moves keyboard focus to the next (previous when <paramref name="backwards"/>) tab stop outside this grid.
    /// The grid and its hidden proxy <c>TextBox</c> are both tab stops, so focus is stepped until it leaves the
    /// grid; the loop is bounded so a page whose only tab stops are the grid's can't spin.
    /// </summary>
    private void MoveFocusOutOfGrid(bool backwards)
    {
        var root = XamlRoot;
        if (root?.Content is null) return;

        var direction = backwards ? FocusNavigationDirection.Previous : FocusNavigationDirection.Next;
        var options = new FindNextElementOptions { SearchRoot = root.Content };
        for (int step = 0; step < 3; step++)
        {
            if (!FocusManager.TryMoveFocus(direction, options)) return;
            if (FocusManager.GetFocusedElement(root) is not DependencyObject focused || !IsWithinGrid(focused))
                return;
        }
    }

    /// <summary>Whether <paramref name="element"/> is this grid or one of its descendants.</summary>
    private bool IsWithinGrid(DependencyObject element)
    {
        for (DependencyObject? current = element; current != null; current = VisualTreeHelper.GetParent(current))
        {
            if (ReferenceEquals(current, this)) return true;
        }
        return false;
    }

    // ── Adapter plumbing ─────────────────────────────────────────

    /// <summary>The proxy <c>TextBox</c> as the adapter sees it.</summary>
    private sealed class TextBoxTextInputProxy(KumikoDataGrid grid) : IKumikoTextInputProxy
    {
        public string Text => grid._inputProxy.Text ?? string.Empty;

        public int SelectionStart => grid._inputProxy.SelectionStart;

        public int SelectionLength => grid._inputProxy.SelectionLength;

        public bool IsChangingText => grid._proxyChangingText;

        public void SetTextAndSelection(string text, int selectionStart, int selectionLength)
        {
            var box = grid._inputProxy;
            if (!string.Equals(box.Text, text, StringComparison.Ordinal))
                box.Text = text;
            box.Select(selectionStart, selectionLength);
        }
    }

    /// <summary>The grid's input controller and edit session as the adapter sees them.</summary>
    private sealed class GridTextInputTarget(KumikoDataGrid grid) : IKumikoTextInputTarget
    {
        public ITextInputSession? ActiveTextInput => grid._inputController.ActiveTextInput;

        public bool IsEditing => grid._editSession.IsEditing;

        // KeyRoutedEventArgs.KeyboardModifiers is [NotImplemented] on the Skia/WASM heads: read the live state.
        public InputModifiers Modifiers => InputMapping.GetLiveModifiers();

        public bool HandleKey(KumikoKeyEventArgs e)
        {
            grid._inputController.HandleKey(e, grid._scroll, grid._selection, grid._style, grid._dataSource);
            if (grid._editSession.IsAnimating)
                grid.StartInertialScroll(); // a held arrow key repeats a spin button from the frame loop
            return e.Handled;
        }
    }
}

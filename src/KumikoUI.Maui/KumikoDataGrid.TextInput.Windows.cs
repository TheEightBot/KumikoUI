using KumikoUI.Core.Input;
#if WINDOWS
using KumikoUI.Core.Components;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.UI.Text.Core;
#endif

namespace KumikoUI.Maui;

/// <summary>
/// Keyboard and text input on Windows (#42): the grid's own platform view takes the keyboard focus, so there is
/// no hidden <c>Entry</c>.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>Keys</b>: the platform view is a tab stop. Its <c>PreviewKeyDown</c> sends navigation, editing and
/// shortcut keys to Core with the live modifier state (<see cref="WindowsKeyInput"/>), ahead of the page's
/// keyboard accelerators. A key Core used is marked handled, and so are navigation and deletion keys, which never
/// move focus or scroll the page. Tab that Core leaves unhandled (no current cell, or past the last tab stop) is
/// the grid's Tab exit: WinUI moves focus on. Enter, Escape, F2 and shortcuts Core leaves unhandled reach the
/// page.</item>
/// <item><b>Text and composition</b>: while a text editor is open, a <c>CoreTextEditContext</c> (the Text
/// Services Framework) is attached to its <see cref="ITextInputSession"/>, so input methods compose in place and
/// the candidate window sits at the edited cell. <see cref="TextEditContextState"/> keeps the text and selection
/// the text services saw and reports Core's own changes back to them.</item>
/// <item><b>Fallback</b>: Windows documents <c>CoreTextServicesManager</c> for desktop apps as available only in
/// Insider builds. Where it cannot be created, or while it delivers no text, characters come from WinUI's
/// <c>CharacterReceived</c> (<see cref="WindowsCharacterInput"/>), so typing, dead keys and text committed by an
/// input method's own window still work, without in-place composition.</item>
/// </list>
/// Pointer modifiers are the live keyboard state on every pointer event (<c>KumikoDataGrid.Pointer.cs</c>).
/// </remarks>
public partial class KumikoDataGrid
{
#if WINDOWS
    private UIElement? _keyboardElement;
    private bool _keyboardElementFocused;

    private CoreTextEditContext? _textEditContext;
    private bool _textServicesUnavailable;
    private bool _textServicesFocused;
    private bool _textChangeFlushQueued;
    private DispatcherQueueHandler? _flushTextChangeHandler;
    private readonly TextEditContextState _textEditState = new();
    private readonly WindowsCharacterInput _characterInput = new();

    // ── Platform view ────────────────────────────────────────────

    /// <summary>Makes the grid's platform view the keyboard sink: a tab stop with key and character handlers.</summary>
    private void AttachNativeKeyboard()
    {
        if (Handler?.PlatformView is not UIElement view || ReferenceEquals(view, _keyboardElement))
            return;
        DetachNativeKeyboard();
        _keyboardElement = view;
        view.IsTabStop = true;
        view.PreviewKeyDown += OnNativePreviewKeyDown;
        view.PreviewKeyUp += OnNativePreviewKeyUp;
        view.CharacterReceived += OnNativeCharacterReceived;
        view.GotFocus += OnNativeGotFocus;
        view.LostFocus += OnNativeLostFocus;
        _keyboardElementFocused = view.FocusState != FocusState.Unfocused;
    }

    private void DetachNativeKeyboard()
    {
        _keyboardElementFocused = false;
        SyncTextServices(); // leaves the text services

        if (_textEditContext != null)
        {
            _textEditContext.TextRequested -= OnTextRequested;
            _textEditContext.SelectionRequested -= OnSelectionRequested;
            _textEditContext.TextUpdating -= OnTextUpdating;
            _textEditContext.SelectionUpdating -= OnSelectionUpdating;
            _textEditContext.FormatUpdating -= OnFormatUpdating;
            _textEditContext.LayoutRequested -= OnLayoutRequested;
            _textEditContext.CompositionStarted -= OnCompositionStarted;
            _textEditContext.CompositionCompleted -= OnCompositionCompleted;
            _textEditContext.FocusRemoved -= OnTextServicesFocusRemoved;
            _textEditContext = null;
        }

        if (_keyboardElement == null) return;
        _keyboardElement.PreviewKeyDown -= OnNativePreviewKeyDown;
        _keyboardElement.PreviewKeyUp -= OnNativePreviewKeyUp;
        _keyboardElement.CharacterReceived -= OnNativeCharacterReceived;
        _keyboardElement.GotFocus -= OnNativeGotFocus;
        _keyboardElement.LostFocus -= OnNativeLostFocus;
        _keyboardElement = null;
    }

    /// <summary>Gives the grid's platform view the keyboard focus (a press on the grid, an edit beginning).</summary>
    private void FocusKeyboardInput()
    {
        if (_keyboardElement is { } element && !_keyboardElementFocused)
            element.Focus(FocusState.Programmatic);
        SyncTextServices();
    }

    /// <summary>
    /// An edit ended. The grid keeps the keyboard focus (navigation goes on); only the text services leave the
    /// closed editor.
    /// </summary>
    private void UnfocusKeyboardInput() => SyncTextServices();

    private void OnNativeGotFocus(object sender, RoutedEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, _keyboardElement)) return;
        _keyboardElementFocused = true;
        SyncTextServices();
    }

    private void OnNativeLostFocus(object sender, RoutedEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, _keyboardElement)) return;
        _keyboardElementFocused = false;
        SyncTextServices();
    }

    // ── Keys ─────────────────────────────────────────────────────

    private void OnNativePreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, _keyboardElement) || !_inputController.IsEnabled) return;

        var modifiers = GetPointerModifiers(); // the live keyboard state
        var key = WindowsKeyInput.ToKumikoKey((int)e.Key, modifiers);
        // Text keys, and a space typed into an editor, arrive as text (text services or CharacterReceived)
        if (key == KumikoKey.None || (key == KumikoKey.Space && _editSession.IsEditing))
        {
            _characterInput.KeyDown(KumikoKey.None, handledByCore: false);
            return;
        }

        var keyEvent = new KumikoKeyEventArgs { Key = key, Modifiers = modifiers };
        _inputController.HandleKey(keyEvent, _scroll, _selection, _style, _dataSource);
        if (_editSession.IsAnimating)
            StartFrameAnimation(); // a held arrow key repeats a spin button from the frame loop
        RestartCursorBlinkTimer(); // a key restarts the caret's blink

        _characterInput.KeyDown(key, keyEvent.Handled);
        if (WindowsKeyInput.ShouldMarkHandled(key, modifiers, keyEvent.Handled))
            e.Handled = true;
        SyncTextServices(); // the key may have opened or closed an editor
    }

    // A key release only matters to an open editor (a held arrow key repeating a spin button)
    private void OnNativePreviewKeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, _keyboardElement) || !_editSession.IsEditing) return;
        var modifiers = GetPointerModifiers();
        var key = WindowsKeyInput.ToKumikoKey((int)e.Key, modifiers);
        if (key == KumikoKey.None) return;
        _inputController.HandleKey(new KumikoKeyEventArgs { Key = key, Modifiers = modifiers, IsKeyDown = false },
            _scroll, _selection, _style, _dataSource);
    }

    private void OnNativeCharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, _keyboardElement) || !_inputController.IsEnabled) return;

        var text = _characterInput.Character(e.Character);
        if (text == null) return;

        // Text carries no modifiers: AltGr (Ctrl+Alt) characters must still open an editor and type
        var keyEvent = new KumikoKeyEventArgs
        {
            Key = KumikoKey.Character,
            Character = text.Length == 1 ? text[0] : null,
            Text = text,
        };
        _inputController.HandleKey(keyEvent, _scroll, _selection, _style, _dataSource);
        e.Handled = true;
        RestartCursorBlinkTimer(); // typing restarts the caret's blink
        SyncTextServices(); // the character may have opened an editor (Typing trigger)
    }

    // ── Text services (CoreTextEditContext) ──────────────────────

    /// <summary>
    /// Attaches the text services to the open text editor while the grid has the keyboard focus, and detaches
    /// them otherwise. Called whenever either may have changed; does nothing when nothing did.
    /// </summary>
    private void SyncTextServices()
    {
        var wanted = _keyboardElementFocused && _inputController.IsEnabled ? _editSession.ActiveTextInput : null;
        if (ReferenceEquals(wanted, _textEditState.Session)) return;

        LeaveTextServices();
        _textEditState.Attach(wanted);
        if (wanted == null) return;

        var context = EnsureTextEditContext();
        if (context == null) return;
        try
        {
            context.InputScope = wanted is DrawnTextBox { NumericOnly: true } ? CoreTextInputScope.Number : CoreTextInputScope.Text;
            context.IsReadOnly = wanted.IsReadOnly;
            context.NotifyFocusEnter();
        }
        catch (Exception)
        {
            // Text services that refuse the focus are unavailable; characters come from CharacterReceived.
            _textServicesUnavailable = true;
            return;
        }
        wanted.TextInputStateChanged += OnTextInputStateChanged;
        _textServicesFocused = true;
    }

    /// <summary>Detaches the text services from the editor they were attached to (this also ends a composition).</summary>
    private void LeaveTextServices()
    {
        if (_textEditState.Session is { } session)
            session.TextInputStateChanged -= OnTextInputStateChanged;
        if (_textServicesFocused)
        {
            _textServicesFocused = false;
            try { _textEditContext?.NotifyFocusLeave(); }
            catch (Exception) { _textServicesUnavailable = true; }
        }
        _textEditState.Attach(null);
        _characterInput.TextServicesFocusChanged();
    }

    /// <summary>The edit context, created on first use; null where the text services are unavailable.</summary>
    private CoreTextEditContext? EnsureTextEditContext()
    {
        if (_textServicesUnavailable) return null;
        if (_textEditContext != null) return _textEditContext;
        try
        {
            // Throws in a desktop app on Windows releases without desktop support (there is no CoreWindow)
            var context = CoreTextServicesManager.GetForCurrentView()?.CreateEditContext();
            if (context == null)
            {
                _textServicesUnavailable = true;
                return null;
            }
            context.Name = "KumikoDataGrid";
            context.InputPaneDisplayPolicy = CoreTextInputPaneDisplayPolicy.Automatic;
            context.TextRequested += OnTextRequested;
            context.SelectionRequested += OnSelectionRequested;
            context.TextUpdating += OnTextUpdating;
            context.SelectionUpdating += OnSelectionUpdating;
            context.FormatUpdating += OnFormatUpdating;
            context.LayoutRequested += OnLayoutRequested;
            context.CompositionStarted += OnCompositionStarted;
            context.CompositionCompleted += OnCompositionCompleted;
            context.FocusRemoved += OnTextServicesFocusRemoved;
            _textEditContext = context;
        }
        catch (Exception)
        {
            _textServicesUnavailable = true;
        }
        return _textEditContext;
    }

    // Core changed the text or selection itself (a key, a pointer gesture, undo, a paste): tell the text services.
    private void OnTextInputStateChanged(object? sender, EventArgs e)
    {
        if (!_textServicesFocused || _textEditContext is not { } context || !ReferenceEquals(sender, _textEditState.Session))
            return;

        if (_textEditState.IsComposing)
        {
            // The input method's composition no longer matches the text: leave and enter again, so the text
            // services drop the composition and read the new text
            LeaveTextServices();
            SyncTextServices();
            return;
        }
        FlushTextChange(context);
    }

    private void FlushTextChange(CoreTextEditContext context)
    {
        if (_textEditState.TakeChange() is not { } change) return;
        var selection = ToCoreTextRange(change.Selection);
        try
        {
            if (change.TextChanged)
                context.NotifyTextChanged(new CoreTextRange { StartCaretPosition = change.Start, EndCaretPosition = change.OldEnd },
                    change.NewLength, selection);
            else
                context.NotifySelectionChanged(selection);
        }
        catch (Exception)
        {
            // A context that no longer accepts notifications re-reads the text on its next focus.
        }
    }

    // The text services must not be notified from inside their own events: report what an update left different
    // (input rules, grapheme snapping, a committed composition) once the event has returned.
    private void QueueTextChangeFlush()
    {
        if (_textChangeFlushQueued || _keyboardElement?.DispatcherQueue is not { } queue) return;
        _flushTextChangeHandler ??= OnFlushTextChangeQueued;
        _textChangeFlushQueued = queue.TryEnqueue(_flushTextChangeHandler);
    }

    private void OnFlushTextChangeQueued()
    {
        _textChangeFlushQueued = false;
        if (_textServicesFocused && _textEditContext is { } context)
            FlushTextChange(context);
    }

    private void OnTextRequested(CoreTextEditContext sender, CoreTextTextRequestedEventArgs args)
    {
        var request = args.Request;
        request.Text = _textEditState.GetText(request.Range.StartCaretPosition, request.Range.EndCaretPosition);
    }

    private void OnSelectionRequested(CoreTextEditContext sender, CoreTextSelectionRequestedEventArgs args) =>
        args.Request.Selection = ToCoreTextRange(_textEditState.GetSelection());

    private void OnTextUpdating(CoreTextEditContext sender, CoreTextTextUpdatingEventArgs args)
    {
        _characterInput.TextServicesUpdated();
        bool applied = _textEditState.ApplyTextUpdate(args.Range.StartCaretPosition, args.Range.EndCaretPosition, args.Text,
            args.NewSelection.StartCaretPosition, args.NewSelection.EndCaretPosition);
        args.Result = applied ? CoreTextTextUpdatingResult.Succeeded : CoreTextTextUpdatingResult.Failed;
        QueueTextChangeFlush();
    }

    private void OnSelectionUpdating(CoreTextEditContext sender, CoreTextSelectionUpdatingEventArgs args)
    {
        bool applied = _textEditState.ApplySelectionUpdate(args.Selection.StartCaretPosition, args.Selection.EndCaretPosition);
        args.Result = applied ? CoreTextSelectionUpdatingResult.Succeeded : CoreTextSelectionUpdatingResult.Failed;
        QueueTextChangeFlush();
    }

    // The editor draws the composition with its own underline; the input method's formatting is accepted and not
    // applied.
    private void OnFormatUpdating(CoreTextEditContext sender, CoreTextFormatUpdatingEventArgs args) =>
        args.Result = CoreTextFormatUpdatingResult.Succeeded;

    /// <summary>
    /// The edited cell's bounds in screen pixels, where the input method places its candidate window: the editor's
    /// bounds are canvas coordinates (DIPs), mapped to the XAML root and then to the screen.
    /// </summary>
    private void OnLayoutRequested(CoreTextEditContext sender, CoreTextLayoutRequestedEventArgs args)
    {
        if (_editSession.ActiveEditor is not { } editor ||
            _canvasView.Handler?.PlatformView is not UIElement canvas || canvas.XamlRoot is not { } root)
            return;

        var bounds = editor.Bounds;
        var local = canvas.TransformToVisual(null).TransformBounds(
            new Windows.Foundation.Rect(bounds.X, bounds.Y, Math.Max(0, bounds.Width), Math.Max(0, bounds.Height)));
        var screen = root.CoordinateConverter.ConvertLocalToScreen(local);
        var rect = new Windows.Foundation.Rect(screen.X, screen.Y, screen.Width, screen.Height);
        args.Request.LayoutBounds.ControlBounds = rect;
        args.Request.LayoutBounds.TextBounds = rect;
    }

    private void OnCompositionStarted(CoreTextEditContext sender, CoreTextCompositionStartedEventArgs args) =>
        _textEditState.BeginComposition();

    private void OnCompositionCompleted(CoreTextEditContext sender, CoreTextCompositionCompletedEventArgs args)
    {
        _textEditState.CompleteComposition();
        QueueTextChangeFlush();
    }

    // The system took the text focus away (for example to another input target); it comes back on the next edit
    // or focus change.
    private void OnTextServicesFocusRemoved(CoreTextEditContext sender, object args)
    {
        if (_textEditState.Session is { } session)
            session.TextInputStateChanged -= OnTextInputStateChanged;
        _textServicesFocused = false;
        _characterInput.TextServicesFocusChanged();
    }

    private static CoreTextRange ToCoreTextRange(KumikoTextRange range) =>
        new() { StartCaretPosition = range.Start, EndCaretPosition = range.End };
#endif
}

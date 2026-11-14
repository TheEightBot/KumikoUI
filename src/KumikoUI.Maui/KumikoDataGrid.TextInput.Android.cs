#if ANDROID
using Android.Content;
using Android.Views;
using Android.Views.InputMethods;
using AndroidX.Core.View;
using KumikoUI.Core.Input;
using KumikoUI.Core.Rendering;
using AView = Android.Views.View;

namespace KumikoUI.Maui;

/// <summary>
/// Android text input (#41): the grid holds keyboard focus with its own <see cref="KumikoTextInputView"/>, which
/// gives the input method a <see cref="KumikoInputConnection"/> onto Core's <see cref="ITextInputSession"/>
/// (composition, surrounding-text deletion, selection) and turns hardware keys into Core keys. There is no hidden
/// <c>Entry</c> on Android.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>The input view takes focus when the grid is pressed, so a hardware keyboard navigates the grid; it
/// becomes a text editor and shows the soft keyboard only while an edit or the filter popup's search box is open.
/// </item>
/// <item>A new edited cell (or the search box) restarts the input method, with the keyboard type of its editor;
/// a change Core makes to the text (undo, paste, a shortcut) restarts it too, and a selection or composition change
/// is reported with <c>updateSelection</c>.</item>
/// <item>The input view sits over the edited cell, so <c>adjustPan</c> and an enclosing scroll view bring the
/// cell into view. Otherwise the grid reads the soft keyboard's window insets and scrolls the cell above it.</item>
/// </list>
/// </remarks>
public partial class KumikoDataGrid
{
    private readonly AndroidTextInputMapper _androidTextInput = new();
    private KumikoTextInputView? _androidInputView;
    private ViewGroup? _androidInputHost;
    private KumikoInputConnection? _androidConnection;
    private ITextInputSession? _androidObservedSession;
    private ViewTreeObserver? _androidLayoutObserver;
    private bool _androidImeRequested;
    private bool _androidImeUpdatePending;
    private int _androidHandledKeyDown = -1;
    private int _androidLastImeBottom = -1;
    private bool _androidImeCheckQueued;
    private bool _androidSessionChanged; // bound to another surface since the input method last connected

    // Explicit: the user asked to edit (InputMethodManager.SHOW_IMPLICIT would let a later implicit hide win)
    private const ShowFlags ShowSoftInputExplicit = 0;

    /// <summary>Whether the input view is a text editor now: an edit or the filter search box is open.</summary>
    internal bool IsAndroidTextInputActive => _androidImeRequested;

    /// <summary>
    /// Whether <paramref name="connection"/> is the input method's live connection and its text surface is still
    /// the open one (an edit that just ended leaves a stale one until the keyboard catches up).
    /// </summary>
    internal bool IsCurrentAndroidConnection(KumikoInputConnection connection) =>
        ReferenceEquals(connection, _androidConnection) &&
        ReferenceEquals(_androidTextInput.Session, _inputController.ActiveTextInput);

    /// <summary>The open text surface for a key, rebinding first when an edit began or ended since the last update.</summary>
    private ITextInputSession? SyncAndroidSessionForKeys()
    {
        var session = _inputController.ActiveTextInput;
        if (!ReferenceEquals(session, _androidTextInput.Session))
        {
            BindAndroidSession(session);
            if (_androidImeRequested) ScheduleAndroidImeUpdate(); // the keyboard restarts on the new surface
        }
        return session;
    }

    // ── Lifecycle ─────────────────────────────────────────────────

    private void OnHandlerChanged_SetupAndroidTextInput(object? sender, EventArgs e) => AttachAndroidTextInput();

    /// <summary>Adds the input view to the grid's platform view (once; again if the content was re-mapped).</summary>
    private void AttachAndroidTextInput()
    {
        _androidTextInput.KeySink ??= SendAndroidKey;
        if (Handler?.PlatformView is not ViewGroup host || host.Context is not { } context) return;
        if (_androidInputView != null && ReferenceEquals(_androidInputHost, host) && ReferenceEquals(_androidInputView.Parent, host))
            return;

        DetachAndroidTextInput();
        var view = new KumikoTextInputView(context, this);
        host.AddView(view, new ViewGroup.LayoutParams(1, 1));
        view.Layout(0, 0, 1, 1);
        _androidInputView = view;
        _androidInputHost = host;
    }

    private void DetachAndroidTextInput()
    {
        BindAndroidSession(null);
        StopObservingAndroidIme();
        _androidImeCheckQueued = false;
        _androidImeRequested = false;
        _androidConnection = null;
        if (_androidInputView is { } view)
        {
            if (view.IsFocused && GetInputMethodManager(view) is { } imm)
                imm.HideSoftInputFromWindow(view.WindowToken, HideSoftInputFlags.None);
            view.Detach();
            (view.Parent as ViewGroup)?.RemoveView(view);
        }
        _androidInputView = null;
        _androidInputHost = null;
    }

    private static InputMethodManager? GetInputMethodManager(AView view) =>
        view.Context?.GetSystemService(Context.InputMethodService) as InputMethodManager;

    // ── Focus and the soft keyboard ───────────────────────────────

    /// <summary>
    /// A press on the grid: the input view takes focus (for a hardware keyboard) without showing the soft keyboard.
    /// It moves to the press first, so a scroll view around the grid has nothing to scroll to.
    /// </summary>
    private void FocusAndroidTextInputForKeys(float x, float y)
    {
        AttachAndroidTextInput();
        if (_androidInputView is not { } view || view.IsFocused) return;
        if (!_androidImeRequested && TryGetTouchScale(out float scale))
        {
            int px = Math.Max(0, (int)(x * scale)), py = Math.Max(0, (int)(y * scale));
            view.Layout(px, py, px + 1, py + 1);
        }
        view.RequestFocus();
    }

    /// <summary>A release while editing brings back a soft keyboard the user dismissed (Back) during the edit.</summary>
    private void ReshowAndroidSoftInput()
    {
        if (_androidImeRequested && _androidInputView is { IsFocused: true } view)
            GetInputMethodManager(view)?.ShowSoftInput(view, ShowSoftInputExplicit);
    }

    /// <summary>
    /// Brings the soft keyboard in line with the grid after an edit begins or ends or the filter popup asks for
    /// the keyboard. Runs after the current input event, so the new editor exists and an edit that ends and begins
    /// again (Tab, Enter that keeps editing) leaves the keyboard up instead of hiding and showing it.
    /// </summary>
    private void ScheduleAndroidImeUpdate()
    {
        if (_androidImeUpdatePending) return;
        _androidImeUpdatePending = true;
        Dispatcher.Dispatch(UpdateAndroidIme);
    }

    private void UpdateAndroidIme()
    {
        _androidImeUpdatePending = false;
        if (_wasCleanedUp) return;
        AttachAndroidTextInput();
        if (_androidInputView is not { } view) return;

        var session = _inputController.ActiveTextInput;
        if (!ReferenceEquals(session, _androidTextInput.Session)) BindAndroidSession(session);

        var imm = GetInputMethodManager(view);
        bool wanted = _editSession.IsEditing || session != null;
        if (wanted)
        {
            bool wasRequested = _androidImeRequested;
            _androidImeRequested = true;
            PlaceAndroidInputView();
            if (!view.IsFocused) view.RequestFocus(); // focus starts the input method on the view
            else if (!wasRequested || _androidSessionChanged) imm?.RestartInput(view);
            StartObservingAndroidIme();
            view.Post(ShowAndroidSoftInput);
        }
        else if (_androidImeRequested)
        {
            _androidImeRequested = false;
            StopObservingAndroidIme();
            imm?.HideSoftInputFromWindow(view.WindowToken, HideSoftInputFlags.None);
            // The view keeps focus for a hardware keyboard but is no text editor now: drop the connection
            if (view.IsFocused) imm?.RestartInput(view);
        }
    }

    private void ShowAndroidSoftInput()
    {
        if (_androidImeRequested && _androidInputView is { } view)
        {
            if (!view.IsFocused) view.RequestFocus();
            GetInputMethodManager(view)?.ShowSoftInput(view, ShowSoftInputExplicit);
            QueueAndroidImeCheck();
        }
    }

    internal void OnAndroidTextInputFocusChanged(bool gainFocus)
    {
        // Focus taken elsewhere (another control on the page): an open composition is finished, not left dangling
        if (!gainFocus && _androidTextInput.Session?.Composition != null)
        {
            _androidTextInput.FinishComposingText();
            InvalidateSurface();
        }
    }

    // ── The input connection ──────────────────────────────────────

    /// <summary>
    /// <c>onCreateInputConnection</c>: none unless a text editor is open; otherwise the keyboard type of the open
    /// editor, its text and selection, and a connection onto it.
    /// </summary>
    internal IInputConnection? CreateAndroidInputConnection(AView view, EditorInfo outAttrs)
    {
        if (!_androidImeRequested)
        {
            _androidConnection = null;
            return null;
        }

        var session = _inputController.ActiveTextInput;
        if (!ReferenceEquals(session, _androidTextInput.Session)) BindAndroidSession(session);

        bool isFilterSearch = session != null && !ReferenceEquals(session, _editSession.ActiveTextInput);
        object? cellValue = null;
        if (!isFilterSearch && _editSession is { IsEditing: true, EditColumn: { } column } &&
            _editSession.EditCell.Row >= 0 && _editSession.EditCell.Row < _dataSource.RowCount)
            cellValue = _dataSource.GetCellValue(_editSession.EditCell.Row, column);

        outAttrs.InputType = (Android.Text.InputTypes)AndroidTextInputMapper.GetInputType(session, cellValue, isFilterSearch);
        outAttrs.ImeOptions = (ImeFlags)AndroidTextInputMapper.GetImeOptions(
            !isFilterSearch && !_editSession.DismissKeyboardOnEnter);

        var state = _androidTextInput.Current;
        outAttrs.InitialSelStart = state.SelectionStart;
        outAttrs.InitialSelEnd = state.SelectionEnd;
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
            outAttrs.SetInitialSurroundingText(new Java.Lang.String(state.Text));
        _androidTextInput.MarkReported();
        _androidSessionChanged = false;

        _androidConnection = new KumikoInputConnection(view, this, _androidTextInput);
        return _androidConnection;
    }

    /// <summary>Observes the new surface's Core-side changes and points the mapper at it.</summary>
    private void BindAndroidSession(ITextInputSession? session)
    {
        if (_androidObservedSession != null)
            _androidObservedSession.TextInputStateChanged -= OnAndroidTextInputStateChanged;
        _androidObservedSession = session;
        if (session != null)
            session.TextInputStateChanged += OnAndroidTextInputStateChanged;
        _androidTextInput.Bind(session);
        _androidSessionChanged = true;
    }

    /// <summary>After the input method's own calls: tells it the selection and composition they produced.</summary>
    internal void ReportAndroidTextInputState()
    {
        if (_androidInputView is not { } view || GetInputMethodManager(view) is not { } imm) return;
        if (_androidTextInput.TryTakeSelectionReport(out var state))
            imm.UpdateSelection(view, state.SelectionStart, state.SelectionEnd, state.CompositionStart, state.CompositionEnd);
        UpdateAndroidExtractedText(imm, view, state);
    }

    // A change Core made (a shortcut, undo, a pointer gesture): the input method's copy of the text is stale.
    private void OnAndroidTextInputStateChanged(object? sender, EventArgs e)
    {
        if (_androidInputView is not { } view || !_androidImeRequested || GetInputMethodManager(view) is not { } imm) return;
        switch (_androidTextInput.TakeExternalChange(out var state))
        {
            case TextInputReport.Restart:
                imm.RestartInput(view);
                break;
            case TextInputReport.Selection:
                imm.UpdateSelection(view, state.SelectionStart, state.SelectionEnd, state.CompositionStart, state.CompositionEnd);
                UpdateAndroidExtractedText(imm, view, state);
                break;
        }
    }

    private void UpdateAndroidExtractedText(InputMethodManager imm, AView view, TextInputSnapshot state)
    {
        if (_androidConnection is { ExtractedTextToken: >= 0 } connection)
        {
            var extracted = KumikoInputConnection.CreateExtractedText(state);
            imm.UpdateExtractedText(view, connection.ExtractedTextToken, extracted);
        }
    }

    // ── Keys ──────────────────────────────────────────────────────

    /// <summary>
    /// A key event for the grid (a hardware key the input method passed on, or a key the input method sent).
    /// Typed text and plain Backspace/Delete in an open text surface edit it directly; every other key goes to
    /// <see cref="KumikoInputController.HandleKey"/>. Returns whether the grid used it; a Tab the grid would not
    /// use is left to Android, which moves focus on.
    /// </summary>
    internal bool HandleAndroidKeyEvent(KeyEvent e)
    {
        if (!_inputController.IsEnabled) return false;

        if (e.Action == KeyEventActions.Up)
        {
            if (e.KeyCode != Keycode.Unknown && (int)e.KeyCode == _androidHandledKeyDown)
            {
                _androidHandledKeyDown = -1;
                if (AndroidTextInputMapper.MapKeyCode((int)e.KeyCode, command: true) is { } upKey && _editSession.IsEditing)
                    _inputController.HandleKey(new KumikoKeyEventArgs { Key = upKey, IsKeyDown = false }, _scroll, _selection, _style, _dataSource);
                return true;
            }
            return false;
        }

        // A string of characters in one event (ACTION_MULTIPLE with KEYCODE_UNKNOWN), sent before Android 10
        if (!OperatingSystem.IsAndroidVersionAtLeast(29) && e.Action == KeyEventActions.Multiple &&
            e.KeyCode == Keycode.Unknown && e.Characters is { Length: > 0 } characters)
            return TypeAndroidText(characters, InputModifiers.None);

        if (e.Action != KeyEventActions.Down) return false;

        int meta = (int)e.MetaState;
        if (!_androidTextInput.MapKeyDown((int)e.KeyCode, meta, e.GetUnicodeChar(e.MetaState), out var key, out var text, out var modifiers))
            return false;

        bool handled = key switch
        {
            KumikoKey.None => true, // a dead key, waiting for its letter
            KumikoKey.Character => TypeAndroidText(text!, modifiers),
            KumikoKey.Space when text != null && SyncAndroidSessionForKeys() != null => TypeAndroidText(text, modifiers),
            KumikoKey.Backspace or KumikoKey.Delete when DeleteInAndroidSession(key, modifiers) => true,
            KumikoKey.Tab when !_inputController.WouldHandleTab(_selection, _dataSource, (modifiers & InputModifiers.Shift) != 0) => false,
            _ => SendAndroidKey(new KumikoKeyEventArgs
            {
                Key = key,
                Character = key == KumikoKey.Space ? ' ' : null,
                Modifiers = modifiers,
            }),
        };
        if (handled) _androidHandledKeyDown = (int)e.KeyCode;
        return handled;
    }

    /// <summary>
    /// Before the input method sees a key: Escape cancels the edit (or closes the popup) unless the input method is
    /// composing, where it is the input method's to cancel the composition.
    /// </summary>
    internal bool HandleAndroidKeyEventPreIme(KeyEvent e)
    {
        if (e.KeyCode != Keycode.Escape || !_androidImeRequested) return false;
        if (_androidTextInput.Session?.Composition != null) return false;
        return HandleAndroidKeyEvent(e);
    }

    /// <summary>The input method's action key (Done or Next): Enter.</summary>
    internal void HandleAndroidEditorAction() => SendAndroidKey(new KumikoKeyEventArgs { Key = KumikoKey.Enter });

    /// <summary>A context-menu action from the input method (select all, cut, copy, paste) as Ctrl+key.</summary>
    internal void HandleAndroidShortcut(KumikoKey key) =>
        SendAndroidKey(new KumikoKeyEventArgs { Key = key, Modifiers = InputModifiers.Control });

    private bool TypeAndroidText(string text, InputModifiers modifiers)
    {
        if (SyncAndroidSessionForKeys() is { } session)
        {
            session.InsertText(text);
            ReportAndroidTextInputState();
            return true;
        }
        return SendAndroidKey(new KumikoKeyEventArgs
        {
            Key = KumikoKey.Character,
            Text = text,
            Character = text.Length == 1 ? text[0] : null,
            Modifiers = modifiers,
        });
    }

    // Plain Backspace/Delete edit an open text surface as the input method's own deletions do (no restart).
    private bool DeleteInAndroidSession(KumikoKey key, InputModifiers modifiers)
    {
        if (SyncAndroidSessionForKeys() is not { } session ||
            (modifiers & (InputModifiers.Control | InputModifiers.Meta | InputModifiers.Alt)) != 0)
            return false;
        if (key == KumikoKey.Backspace) session.DeleteBackward();
        else session.DeleteForward();
        ReportAndroidTextInputState();
        return true;
    }

    private bool SendAndroidKey(KumikoKeyEventArgs keyEvent)
    {
        _inputController.HandleKey(keyEvent, _scroll, _selection, _style, _dataSource);
        if (_editSession.IsAnimating)
            StartFrameAnimation(); // a held arrow key repeats a spin button from the frame loop
        RestartCursorBlinkTimer(); // a key restarts the caret's blink
        return keyEvent.Handled;
    }

    // ── Keeping the edited cell above the soft keyboard ───────────

    /// <summary>Places the input view over the edited cell (in the grid's pixels), where the window pans to.</summary>
    private void PlaceAndroidInputView()
    {
        if (_androidInputView is not { } view || _androidInputHost is not { } host || !TryGetTouchScale(out float scale))
            return;
        if (_editSession is not { IsEditing: true, ActiveEditor: { IsVisible: true } editor })
            return;

        var b = editor.Bounds;
        int left = Math.Clamp((int)(b.X * scale), 0, Math.Max(0, host.Width - 1));
        int top = Math.Clamp((int)(b.Y * scale), 0, Math.Max(0, host.Height - 1));
        int right = Math.Clamp((int)Math.Ceiling(b.Right * scale), left + 1, Math.Max(left + 1, host.Width));
        int bottom = Math.Clamp((int)Math.Ceiling(b.Bottom * scale), top + 1, Math.Max(top + 1, host.Height));
        view.Layout(left, top, right, bottom);
    }

    private void StartObservingAndroidIme()
    {
        if (_androidLayoutObserver != null || _androidInputHost is not { IsAttachedToWindow: true } host) return;
        _androidLayoutObserver = host.ViewTreeObserver;
        if (_androidLayoutObserver != null)
            _androidLayoutObserver.GlobalLayout += OnAndroidGlobalLayout;
        _androidLastImeBottom = -1;
    }

    private void StopObservingAndroidIme()
    {
        if (_androidLayoutObserver is { IsAlive: true } observer)
            observer.GlobalLayout -= OnAndroidGlobalLayout;
        _androidLayoutObserver = null;
    }

    /// <summary>
    /// Checks the edited cell against the soft keyboard after the next frame, when the renderer has placed the
    /// editor over its cell (a new edit may have scrolled the grid to reach it).
    /// </summary>
    private void QueueAndroidImeCheck()
    {
        _androidImeCheckQueued = true;
        InvalidateSurface();
    }

    // After the next frame (on the UI thread, whatever the surface); the check itself is posted, out of the draw pass.
    partial void OnFrameRendered()
    {
        if (!_androidImeCheckQueued) return;
        _androidImeCheckQueued = false;
        _androidInputHost?.Post(CheckAndroidImeOverlapNow);
    }

    private void CheckAndroidImeOverlapNow() => CheckAndroidImeOverlap(force: true);

    // The soft keyboard showing, hiding or resizing the window relayouts it
    private void OnAndroidGlobalLayout(object? sender, EventArgs e) => CheckAndroidImeOverlap(force: false);

    /// <summary>
    /// Scrolls the grid so the edited cell sits above the soft keyboard when the keyboard covers it: read from the
    /// window insets (the keyboard's top) and the grid's place in the window. With <c>adjustPan</c> the window pans
    /// to the input view over the cell instead, so the grid leaves it alone.
    /// </summary>
    private void CheckAndroidImeOverlap(bool force)
    {
        if (_androidInputHost is not { IsAttachedToWindow: true } host || !TryGetTouchScale(out float scale)) return;
        if (_editSession is not { IsEditing: true, ActiveEditor: { IsVisible: true } editor }) return;

        var insets = ViewCompat.GetRootWindowInsets(host);
        if (insets == null) return;
        int imeType = WindowInsetsCompat.Type.Ime();
        int imeBottom = insets.IsVisible(imeType) ? insets.GetInsets(imeType)?.Bottom ?? 0 : 0;
        if (!force && imeBottom == _androidLastImeBottom) return;
        _androidLastImeBottom = imeBottom;

        PlaceAndroidInputView();
        if (imeBottom <= 0 || IsAdjustPan()) return;
        // A frozen row does not scroll
        if (_editSession.EditCell.Row < _dataSource.FrozenRowCount) return;

        var location = new int[2];
        host.GetLocationInWindow(location);
        int windowHeight = host.RootView?.Height ?? 0;
        if (windowHeight <= 0) return;
        float visibleBottom = (Math.Min(location[1] + host.Height, windowHeight - imeBottom) - location[1]) / scale;

        var bounds = editor.Bounds;
        if (visibleBottom <= 0 || bounds.Bottom <= visibleBottom) return;

        float before = _scroll.OffsetY;
        _scroll.ScrollTo(_scroll.OffsetX, before + (bounds.Bottom - visibleBottom));
        float moved = _scroll.OffsetY - before;
        if (moved <= 0) return;
        editor.Bounds = new KumikoRect(bounds.X, bounds.Y - moved, bounds.Width, bounds.Height);
        PlaceAndroidInputView();
        InvalidateSurface();
    }

    private static bool IsAdjustPan()
    {
        var window = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity?.Window;
        var mode = window?.Attributes?.SoftInputMode ?? SoftInput.AdjustUnspecified;
        return (mode & SoftInput.MaskAdjust) == SoftInput.AdjustPan;
    }
}
#endif

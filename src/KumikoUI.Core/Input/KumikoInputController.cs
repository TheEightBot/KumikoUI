using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Components;
using KumikoUI.Core.Layout;

namespace KumikoUI.Core.Input;

/// <summary>
/// Central input controller for the grid. Processes platform-independent
/// pointer and keyboard events, dispatching to selection, sorting,
/// column resize/reorder, and scrolling subsystems.
/// </summary>
public sealed partial class KumikoInputController
{
    private readonly KumikoHitTester _hitTester = new();

    /// <summary>The hit tester this controller resolves pointer positions with (a renderer publishes regions to it).</summary>
    internal KumikoHitTester HitTester => _hitTester;

    // Hover and pressed feedback for the renderer (#101)
    private readonly InteractionFeedback _feedback = new();

    /// <summary>The hover and pressed state a connected renderer draws.</summary>
    internal InteractionFeedback Feedback => _feedback;

    // Transitions after an interaction (#101), advanced by the host's frame loop
    private readonly InteractionAnimations _animations = new();
    private bool _animationsEnabled;

    /// <summary>The transitions a connected renderer draws.</summary>
    internal InteractionAnimations Animations => _animations;

    /// <summary>
    /// Whether this controller plays transitions after an interaction: a group's rows sliding open or shut, a dragged
    /// column header lifting and settling into its slot, the current row's highlight fading in (with a connected
    /// <see cref="KumikoRenderer"/>, see <see cref="KumikoRenderer.InputController"/>). False by default, because a
    /// transition moves only when the host calls <see cref="UpdateAnimations"/> from its frame loop: a host that does
    /// sets it. The MAUI and Uno grids set it, except when the platform asks for reduced motion. Turning it off ends
    /// the transitions in progress. <see cref="KumikoStyle.EnableAnimations"/> turns them off per style.
    /// </summary>
    public bool AnimationsEnabled
    {
        get => _animationsEnabled;
        set
        {
            if (_animationsEnabled == value) return;
            _animationsEnabled = value;
            if (!value && _animations.IsActive)
            {
                _animations.StopAll();
                Redraw();
            }
        }
    }

    /// <summary>
    /// Whether a transition is running (see <see cref="AnimationsEnabled"/>). Hosts check it after each pointer event
    /// and after each frame they draw, and while it is true call <see cref="UpdateAnimations"/> once per frame.
    /// </summary>
    public bool IsAnimating => _animations.IsActive;

    /// <summary>
    /// Advance the running transitions by <paramref name="frameIntervalMs"/> and redraw. Call it once per frame from
    /// the frame loop or timer that runs <see cref="UpdateInertialScroll"/>; it allocates nothing.
    /// </summary>
    /// <param name="frameIntervalMs">The time since the previous frame, in milliseconds.</param>
    /// <returns>Whether a transition is still running.</returns>
    public bool UpdateAnimations(float frameIntervalMs = 16f)
    {
        if (!_animations.IsActive) return false;
        bool running = _animations.Update(frameIntervalMs);
        Redraw();
        return running;
    }

    /// <summary>Whether transitions play for <paramref name="style"/>: the host drives them and the style allows them.</summary>
    internal bool CanAnimate(KumikoStyle style) => _animationsEnabled && style.EnableAnimations;
    private readonly InertialScroller _inertialScroller = new();
    private readonly PopupManager _popupManager = new();
    private DrawnFilterPopup? _activeFilterPopup;
    private bool _popupRedrawWired;

    // Drag state
    private bool _isPanning;
    private bool _isResizingColumn;
    private bool _isDraggingColumn;
    private bool _isDraggingRow;
    private float _lastPanX, _lastPanY;
    private int _resizeColumnIndex = -1;
    private float _resizeStartX;
    private float _resizeStartWidth;
    private int _dragColumnIndex = -1;
    private float _dragColumnOffset;
    private int _dragRowIndex = -1;
    private float _dragRowOffset;

    // A press on a row drag handle: the drag starts as soon as it moves, with no drag threshold (-1 when none).
    private int _armedDragRow = -1;

    // The device of the press in progress: a touch drag on a row pans, a mouse or pen drag reorders it.
    private KumikoPointerDeviceType _pressDeviceType;

    // Edge autoscroll during a row drag, in pixels per second (positive scrolls down); 0 when not autoscrolling.
    private float _autoScrollVelocityY;

    private const float PanThreshold = 10f;
    private const float DragThreshold = 8f;

    // The movement that starts a drag from a drag handle: enough to ignore the jitter of a tap.
    private const float HandleDragSlop = 2f;

    // Tracks the original press position for more accurate first-move
    private float _pressOriginX, _pressOriginY;

    // What the press in progress landed on. A header sort, a filter icon, a group chip or a group header acts on the
    // release, and only when the release is on the same target (#101, INPUT-15): a press that slides onto another
    // target, or turns into a pan, does nothing there.
    private HitRegion _pressRegion;
    private int _pressColumnIndex = -1;
    private int _pressRowIndex = -1;
    private int _pressGroupIndex = -1;

    // A press is in progress (Pressed seen, Released/Cancelled not yet): a Moved without one is a hover.
    private bool _isPointerDown;

    // The pointer that pressed last (KumikoPointerEventArgs.PointerId) and when it was last heard from: while it
    // is down, other pointers are ignored, unless it has gone silent long enough that its release was lost.
    private long _activePointerId;
    private long _activePointerLastMs;
    private const long StalePointerMs = 1000;

    private bool _isEnabled = true;

    // The Boolean cell that the first tap of a possible double-tap toggled, so the second tap doesn't
    // toggle it back.
    private CellPosition _lastTapToggledCell = CellPosition.Invalid;

    // When the editor consumes a pointer-down (e.g. selecting a dropdown item which
    // immediately ends the edit session), the subsequent pointer-up must also be
    // suppressed so it doesn't fall through to grid-level hit-testing.
    private bool _editorConsumedPointerDown;

    // ── Edit session ──
    private EditSession? _editSession;

    /// <summary>Configurable scroll/inertia settings.</summary>
    public ScrollSettings ScrollSettings
    {
        get => _inertialScroller.Settings;
        set => _inertialScroller.Settings = value ?? new();
    }

    /// <summary>Gets or sets the edit session for inline cell editing.</summary>
    public EditSession? EditSession
    {
        get => _editSession;
        set
        {
            if (ReferenceEquals(_editSession, value)) return;
            UnwireEditorClipboard(_editSession);
            _editSession = value;
            WireEditorClipboard(value);
        }
    }

    /// <summary>
    /// Grid-level switch for header-tap sorting: when false a header tap (with or without Ctrl/Shift) does not
    /// sort, whatever a column's own <see cref="KumikoColumn.AllowSorting"/>. Default true. Sorting set in code
    /// (<see cref="KumikoSource.ToggleSort"/>) is not affected.
    /// </summary>
    public bool AllowSorting { get; set; } = true;

    /// <summary>
    /// Grid-level switch for column filtering from the header: when false the filter icon is not hit-testable
    /// and no filter popup opens, whatever a column's own <see cref="KumikoColumn.AllowFiltering"/>. Default
    /// true. Filters set in code are not affected.
    /// </summary>
    public bool AllowFiltering
    {
        get => _hitTester.AllowFiltering;
        set => _hitTester.AllowFiltering = value;
    }

    // Grid-level read-only: the edit session's flag, so editors, Boolean toggles and Tab stops agree
    private bool GridReadOnly => _editSession is { IsReadOnly: true };

    // ── Events ──
    /// <summary>Raised when the grid surface needs to be redrawn.</summary>
    public event EventHandler? NeedsRedraw;
    /// <summary>
    /// The mouse cursor the grid wants over the pointer's position: <see cref="KumikoCursor.ResizeHorizontal"/>
    /// over a column resize grip and for the whole of a resize drag, <see cref="KumikoCursor.Hand"/> over
    /// something a click acts on directly, else <see cref="KumikoCursor.Default"/>. Hover moves
    /// (<see cref="InputAction.Hover"/>) update it, a release re-evaluates it, and leaving the grid
    /// (<see cref="InputAction.Exited"/>) resets it.
    /// </summary>
    public KumikoCursor Cursor { get; private set; }

    /// <summary>Raised when <see cref="Cursor"/> changes (only when it changes), with the new cursor.</summary>
    public event EventHandler<KumikoCursor>? CursorChanged;
    /// <summary>
    /// Raised when the user finishes resizing a column by dragging its header edge. The drag makes the width
    /// explicit and switches a Star or Auto column to Fixed, so the width persists (see
    /// <see cref="KumikoColumn.SizeMode"/>).
    /// </summary>
    public event EventHandler<ColumnResizedEventArgs>? ColumnResized;
    /// <summary>
    /// Raised when the user drops a dragged column header at a new position, before anything moved: the host
    /// applies the move with <see cref="KumikoSource.ReorderColumn"/> (the MAUI and Uno grids do, and keep their
    /// <c>Columns</c> collection in step). A column moves only within its own pane (left-frozen, scrolling or
    /// right-frozen), and only when <see cref="KumikoColumn.AllowReorder"/> is true. See
    /// <see cref="KumikoColumnReorderedEventArgs"/> for what the indices mean.
    /// </summary>
    public event EventHandler<KumikoColumnReorderedEventArgs>? ColumnReordered;
    /// <summary>
    /// Raised once when the user drops a dragged row at a new position and the row moved: the controller has already
    /// called <see cref="KumikoSource.ReorderRow"/>, which moved the item in the items source.
    /// <see cref="RowReorderedEventArgs.Result"/> is <see cref="KumikoReorderResult.Moved"/>.
    /// </summary>
    public event EventHandler<RowReorderedEventArgs>? RowReordered;
    /// <summary>
    /// Raised once when the user drops a dragged row at a new position but <see cref="KumikoSource.ReorderRow"/>
    /// refused to move it; <see cref="RowReorderedEventArgs.Result"/> says why (a column sort is active, the items
    /// source cannot be reordered, or the target is in another group). Nothing changed. A drop where the row already
    /// is, or a canceled drag, raises neither event.
    /// </summary>
    public event EventHandler<RowReorderedEventArgs>? RowReorderRejected;
    /// <summary>
    /// Raised when a finger or pen gesture reaches a moment phones confirm with a short vibration (#101): a row or a
    /// column header picked up (<see cref="KumikoHapticKind.DragStart"/>) or dropped (<see cref="KumikoHapticKind.Drop"/>),
    /// and a long press that asks for a context menu or edits a cell (<see cref="KumikoHapticKind.LongPress"/>). Never
    /// raised for a mouse. The MAUI and Uno grids answer it when their <c>EnableHaptics</c> is on (off by default).
    /// </summary>
    public event EventHandler<KumikoHapticEventArgs>? HapticFeedbackRequested;

    /// <summary>Raises <see cref="HapticFeedbackRequested"/> for a finger or pen gesture.</summary>
    private void RequestHaptic(KumikoHapticKind kind, KumikoPointerDeviceType device)
    {
        if (device == KumikoPointerDeviceType.Mouse || HapticFeedbackRequested is not { } handler) return;
        handler(this, new KumikoHapticEventArgs(kind, device));
    }

    /// <summary>Raised when a data cell is tapped (after the selection is updated).</summary>
    public event EventHandler<RowTappedEventArgs>? RowTapped;
    /// <summary>Raised on the second tap of a double-tap on a data cell.</summary>
    public event EventHandler<RowTappedEventArgs>? RowDoubleTapped;

    /// <summary>The popup manager the hosts draw overlays from.</summary>
    internal PopupManager PopupManager => _popupManager;

    /// <summary>Raised when a popup or component needs keyboard focus from the platform layer (e.g., to show the software keyboard).</summary>
    public event EventHandler? KeyboardFocusRequested;

    /// <summary>Raised when a filter popup is opened (used to start cursor blink timer).</summary>
    public event EventHandler? FilterPopupOpened;

    /// <summary>
    /// Raised when a filter popup closes, however it closes: OK, Cancel, Clear, a sort, Escape, or a press
    /// outside it (used to stop the cursor blink timer).
    /// </summary>
    public event EventHandler? FilterPopupClosed;

    /// <summary>
    /// The text-input surface that has the keyboard, for a host's native text-input adapter: the open
    /// filter popup's search box, else the active editor's, else null.
    /// </summary>
    public ITextInputSession? ActiveTextInput =>
        _activeFilterPopup?.SearchTextInput ?? _editSession?.ActiveTextInput;

    /// <summary>
    /// Raised when a column edge is double-clicked, requesting auto-fit width. The built-in hosts answer it with
    /// <see cref="KumikoRenderer.AutoFitColumn"/>.
    /// </summary>
    public event EventHandler<AutoFitColumnEventArgs>? AutoFitColumnRequested;

    /// <summary>Is the inertial scroller still animating?</summary>
    public bool IsInertialScrolling => _inertialScroller.IsActive;

    /// <summary>
    /// Whether a drag holds the pointer in an edge autoscroll band (see <see cref="ScrollSettings.AutoScrollEdgeSize"/>)
    /// and the grid can still scroll that way. Hosts check it after each pointer event and, while it is true, call
    /// <see cref="UpdateAutoScroll"/> once per frame (from the same frame loop or timer as
    /// <see cref="UpdateInertialScroll"/>).
    /// </summary>
    public bool IsAutoScrolling => _isDraggingRow && _autoScrollVelocityY != 0;

    /// <summary>
    /// Whether the grid responds to the user. While false, pointer and key events are ignored (the grid does
    /// not select, sort, scroll, drag or edit, and leaves every event unhandled) and the cursor stays the
    /// default. Turning it off ends whatever is in progress: a drag or pan stops, a fling stops, popups close
    /// and an open edit is canceled. Hosts mirror their own <c>IsEnabled</c> here. Default: true.
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value) return;
            _isEnabled = value;
            if (!value) StopInteraction();
        }
    }

    /// <summary>Column being dragged for reorder (-1 if none).</summary>
    public int DragColumnIndex => _dragColumnIndex;

    /// <summary>Current drag X offset for visual feedback.</summary>
    public float DragColumnScreenX { get; private set; }

    /// <summary>Row being dragged for reorder (-1 if none).</summary>
    public int DragRowIndex => _dragRowIndex;

    /// <summary>
    /// The top of the dragged row's ghost, in viewport coordinates (from the top of the grid, grouping panel
    /// included): the pointer minus where the row was grabbed, kept so that the ghost's middle stays within the rows
    /// area. The drop target is the row under the ghost's middle.
    /// </summary>
    public float DragRowScreenY { get; private set; }

    /// <summary>
    /// Handle a pointer event.
    /// </summary>
    public void HandlePointer(
        KumikoPointerEventArgs e,
        ScrollState scroll, KumikoSelectionModel selection, KumikoStyle style,
        KumikoSource dataSource)
    {
        EnsurePopupRedrawWired();

        // Leaving the grid only affects the cursor; it is not a position popups or the grid act on.
        if (e.Action == InputAction.Exited)
        {
            SetCursor(_isResizingColumn ? KumikoCursor.ResizeHorizontal : KumikoCursor.Default);
            if (_feedback.ClearHover()) Redraw();
            return;
        }

        // A disabled grid does nothing, and leaves the event to the page.
        if (!_isEnabled) return;

        // A position the host could not compute (a touch before the first layout) is nowhere.
        if (!float.IsFinite(e.X) || !float.IsFinite(e.Y)) return;

        if (!AcceptPointer(e)) return;

        // A move with no press in progress is a hover, whatever the host called it.
        bool wasPointerDown = _isPointerDown;
        if (e.Action == InputAction.Pressed) _isPointerDown = true;
        else if (e.Action is InputAction.Released or InputAction.Cancelled) _isPointerDown = false;
        var action = e.Action == InputAction.Moved && !wasPointerDown ? InputAction.Hover : e.Action;

        // Only the primary button selects, sorts, scrolls, drags and edits: the press records its button, and the
        // rest of the press follows it (hosts may report no button, or another one, on the release).
        if (action == InputAction.Pressed)
        {
            _pressButton = e.Button;
            _pressConsumedByContext = false;
        }
        bool partOfPress = action == InputAction.Pressed ||
            (wasPointerDown && action is InputAction.Moved or InputAction.Released or InputAction.LongPress);
        if (partOfPress && !IsPrimaryButton(_pressButton))
        {
            HandleNonPrimaryPointer(e, action, scroll, selection, style, dataSource);
            return;
        }

        // A long press that raised ContextRequested ends the gesture: lifting or moving the finger does nothing.
        if (partOfPress && _pressConsumedByContext) // always false on a new press, which resets it above
        {
            e.Handled = true;
            return;
        }

        // ── Popup priority: popups always get first crack at input ──
        if (_popupManager.HandlePointer(e))
        {
            if (action == InputAction.Hover) SetCursor(KumikoCursor.Default);
            if (_feedback.ClearHover() | _feedback.EndPress()) Redraw(); // a popup over the grid takes the pointer
            return;
        }

        var columns = dataSource.Columns;
        int totalRows = dataSource.RowCount;

        switch (action)
        {
            case InputAction.Pressed:
                HandlePointerDown(e, scroll, selection, style, columns, totalRows, dataSource);
                break;
            case InputAction.Moved:
                HandlePointerMove(e, scroll, selection, style, columns, totalRows, dataSource);
                TrackPressFeedback(e, scroll, style, dataSource);
                break;
            case InputAction.Hover:
                HandleHover(e, scroll, style, dataSource);
                break;
            case InputAction.Released:
                if (_feedback.EndPress()) Redraw();
                HandlePointerUp(e, scroll, selection, style, columns, totalRows, dataSource);
                // The release may end a resize or change what is under the pointer (a column resized, a
                // group collapsed, a row removed): the cursor and the hover follow.
                UpdatePointerFeedback(e.X, e.Y, e.DeviceType, scroll, style, dataSource);
                break;
            case InputAction.Scroll:
                // The wheel over an open editor (a dropdown list, a picker wheel) scrolls the editor
                if (_editSession is { IsEditing: true } && _editSession.HandlePointerEvent(e))
                {
                    e.Handled = true;
                    Redraw();
                    break;
                }
                HandleScroll(e, scroll, style);
                // The rows moved under a still mouse: what it hovers follows
                if (e.Handled && e.DeviceType != KumikoPointerDeviceType.Touch && !_isPointerDown)
                    UpdatePointerFeedback(e.X, e.Y, e.DeviceType, scroll, style, dataSource);
                break;
            case InputAction.Cancelled:
                if (_feedback.EndPress()) Redraw();
                HandlePointerCancelled(e, scroll, style, dataSource);
                break;
            case InputAction.LongPress:
                if (_feedback.EndPress()) Redraw(); // a long press is not a press on the control
                HandleLongPress(e, scroll, selection, style, columns, totalRows, dataSource);
                break;
        }
    }

    /// <summary>
    /// Whether the event comes from the pointer the controller follows. A press is followed by its own moves,
    /// long press and release; another pointer pressing, moving or long-pressing meanwhile (a second finger) is
    /// ignored, and so is a release or cancel from a pointer other than the last one to press. Those are marked
    /// handled, so the page does not act on half of a two-finger gesture either. A press from another pointer
    /// is accepted when the followed one has been silent for <see cref="StalePointerMs"/>, in case its release
    /// was lost. A cancel with no press in progress (WinUI's capture loss after a release) is ignored and left
    /// unhandled. Wheel, hover and double-tap events are not tied to a press and always pass.
    /// </summary>
    private bool AcceptPointer(KumikoPointerEventArgs e)
    {
        bool fromActive = e.PointerId == _activePointerId;
        bool accept = e.Action switch
        {
            InputAction.Pressed => !_isPointerDown || fromActive || IsActivePointerStale(e.TimestampMs),
            InputAction.Moved or InputAction.LongPress => !_isPointerDown || fromActive,
            InputAction.Released => fromActive,
            InputAction.Cancelled => fromActive || !_isPointerDown,
            _ => true,
        };
        if (!accept)
        {
            e.Handled = true;
            return false;
        }

        if (e.Action == InputAction.Cancelled && !_isPointerDown) return false; // nothing to cancel

        if (e.Action == InputAction.Pressed) _activePointerId = e.PointerId;
        if (e.PointerId == _activePointerId) _activePointerLastMs = e.TimestampMs;
        return true;
    }

    private bool IsActivePointerStale(long nowMs) =>
        nowMs > 0 && _activePointerLastMs > 0 && nowMs - _activePointerLastMs > StalePointerMs;

    /// <summary>Ends every gesture in progress, for <see cref="IsEnabled"/> turning off.</summary>
    private void StopInteraction()
    {
        _isPointerDown = false;
        _isPanning = false;
        _isResizingColumn = false;
        _isDraggingColumn = false;
        _isDraggingRow = false;
        _dragColumnIndex = -1;
        _dragRowIndex = -1;
        _armedDragRow = -1;
        _autoScrollVelocityY = 0;
        _editorConsumedPointerDown = false;
        _inertialScroller.Reset();
        _feedback.ClearHover();
        _feedback.EndPress();
        _animations.StopAll();
        if (_popupManager.HasActivePopups) _popupManager.CloseAll();
        if (_editSession is { IsEditing: true }) _editSession.CancelEdit();
        SetCursor(KumikoCursor.Default);
        Redraw();
    }

    /// <summary>
    /// Handle a keyboard event.
    /// </summary>
    public void HandleKey(
        KumikoKeyEventArgs e,
        ScrollState scroll, KumikoSelectionModel selection, KumikoStyle style,
        KumikoSource dataSource)
    {
        if (!_isEnabled) return;
        if (!e.IsKeyDown)
        {
            // A key release only matters to an open editor (a held arrow key repeating a spin button)
            if (_editSession is { IsEditing: true })
                _editSession.HandleKeyEvent(e);
            return;
        }

        // ── Popup priority: popups always get first crack at keyboard ──
        if (_popupManager.HandleKey(e))
            return;

        int totalRows = dataSource.RowCount;
        int totalCols = dataSource.Columns.Count;
        // One page = the number of scrollable rows that fit between the fixed bands (header, grouping
        // panel, top summaries, frozen rows, pinned footer), so PageUp/PageDown never skip unseen rows.
        int pageSize = Math.Max(1, (int)(KumikoScroller.GetScrollableRowsViewportHeight(scroll, style, dataSource) / style.RowHeight));

        // ── Editing-mode key handling ──
        if (_editSession != null && _editSession.IsEditing)
        {
            // The editor has the first chance at every key, Enter, Escape and Tab included: an open dropdown
            // or calendar uses them (pick the highlighted item, close the list), as does an input method
            // composing text. Picking an item may complete, and so commit, the edit.
            if (_editSession.HandleKeyEvent(e))
            {
                selection.IsEditing = _editSession.IsEditing;
                e.Handled = true;
                Redraw();
                return;
            }

            switch (e.Key)
            {
                case KumikoKey.Escape:
                    _editSession.CancelEdit();
                    selection.IsEditing = false;
                    Redraw();
                    e.Handled = true;
                    return;

                case KumikoKey.Enter:
                    if (_editSession.CommitEdit(dataSource))
                    {
                        selection.IsEditing = false;
                        // Move down after commit
                        selection.NavigateDown(totalRows, false);
                        EnsureCurrentCellVisible(scroll, selection, style, dataSource.Columns, dataSource);

                        if (!_editSession.DismissKeyboardOnEnter)
                        {
                            // Keep editing: automatically begin editing the new cell
                            TryBeginEdit(selection, dataSource, scroll, style, null);
                        }

                        Redraw();
                    }
                    e.Handled = true;
                    return;

                case KumikoKey.Tab:
                    // A refused commit keeps the editor open and the focus in the grid.
                    if (!_editSession.CommitEdit(dataSource))
                    {
                        e.Handled = true;
                        return;
                    }

                    selection.IsEditing = false;
                    // Tab: next tab stop, Shift+Tab: previous one. Past the last (or first) the edit is
                    // committed and the key is left unhandled, so the host moves focus out of the grid.
                    if (MoveToNextTabStop(selection, dataSource, scroll, style, e.HasShift ? -1 : 1))
                    {
                        BeginEditAfterTab(selection, dataSource, scroll, style);
                        e.Handled = true;
                    }
                    Redraw();
                    return;

                case KumikoKey.Up or KumikoKey.Down or KumikoKey.Left or KumikoKey.Right or
                    KumikoKey.Home or KumikoKey.End or KumikoKey.PageUp or KumikoKey.PageDown:
                    // A navigation key the editor leaves alone moves the selection, which ends the edit: a
                    // refused commit keeps the editor open and the selection where it is.
                    if (!CommitEditBeforeViewChange(dataSource, selection))
                    {
                        e.Handled = true;
                        return;
                    }
                    break;

                default:
                    // Any other key the editor leaves alone stops here: it must not reach the grid's shortcuts
                    // (typing would restart the edit, Space toggle the row's selection, F2 reopen the editor,
                    // Ctrl+A select every row). A key with a shortcut modifier stays unhandled, so the page's
                    // own accelerators still work.
                    if (!e.HasCommand && !e.HasAlt)
                        e.Handled = true;
                    return;
            }
        }

        // ── Normal-mode key handling ──

        // Group header row keyboard navigation
        if (selection.CurrentCell.IsValid && dataSource.IsGroupHeaderRow(selection.CurrentCell.Row))
        {
            int groupRow = selection.CurrentCell.Row;
            var groupInfo = dataSource.GetGroupHeaderInfo(groupRow);
            if (groupInfo != null)
            {
                switch (e.Key)
                {
                    case KumikoKey.Right:
                        // Expand group
                        if (!groupInfo.IsExpanded && CommitEditBeforeViewChange(dataSource, selection))
                        {
                            dataSource.ToggleGroupExpansion(groupRow);
                            Redraw();
                        }
                        e.Handled = true;
                        return;
                    case KumikoKey.Left:
                        // Collapse group
                        if (groupInfo.IsExpanded && CommitEditBeforeViewChange(dataSource, selection))
                        {
                            dataSource.ToggleGroupExpansion(groupRow);
                            Redraw();
                        }
                        e.Handled = true;
                        return;
                    case KumikoKey.Space:
                    case KumikoKey.Enter:
                        // Toggle expand/collapse
                        if (CommitEditBeforeViewChange(dataSource, selection))
                            dataSource.ToggleGroupExpansion(groupRow);
                        Redraw();
                        e.Handled = true;
                        return;
                    case KumikoKey.Up:
                        selection.NavigateUp(totalRows, e.HasShift);
                        EnsureCurrentCellVisible(scroll, selection, style, dataSource.Columns, dataSource);
                        Redraw();
                        e.Handled = true;
                        return;
                    case KumikoKey.Down:
                        selection.NavigateDown(totalRows, e.HasShift);
                        EnsureCurrentCellVisible(scroll, selection, style, dataSource.Columns, dataSource);
                        Redraw();
                        e.Handled = true;
                        return;
                }
            }
        }

        switch (e.Key)
        {
            case KumikoKey.Enter:
                // Enter on an interactive cell (action buttons) runs its default control, without an editor.
                if (selection.CurrentCell.IsValid &&
                    InteractiveRendererAt(dataSource, selection.CurrentCell) is { } interactive)
                {
                    var activated = dataSource.GetCellValue(selection.CurrentCell.Row, dataSource.Columns[selection.CurrentCell.Column]);
                    e.Handled = interactive.OnCellActivate(activated);
                    Redraw();
                    break;
                }

                // Enter on an editable Boolean cell toggles it: a check box has no editor to open (#179).
                if (TryToggleCurrentBooleanCell(selection, dataSource))
                {
                    e.Handled = true;
                    break;
                }

                // Enter opens the editor of the current cell, like F2 (column- or grid-level F2Key trigger).
                // Otherwise it is left unhandled, so the page's default button still works.
                if (_editSession != null && selection.CurrentCell.IsValid &&
                    (GetEffectiveEditTriggers(ColumnAt(dataSource, selection.CurrentCell.Column)) & EditTrigger.F2Key) != 0)
                {
                    e.Handled = TryBeginEdit(selection, dataSource, scroll, style, null);
                }
                break;
            case KumikoKey.F2:
                // F2 on an editable Boolean cell toggles it, like Enter: there is no editor to open (#179).
                if (TryToggleCurrentBooleanCell(selection, dataSource))
                {
                    e.Handled = true;
                    break;
                }

                // F2 → begin editing the current cell (honors column- and grid-level F2Key flag)
                if (_editSession != null && selection.CurrentCell.IsValid)
                {
                    int f2Col = selection.CurrentCell.Column;
                    var f2Column = f2Col >= 0 && f2Col < dataSource.Columns.Count
                        ? dataSource.Columns[f2Col] : null;
                    if ((GetEffectiveEditTriggers(f2Column) & EditTrigger.F2Key) != 0)
                    {
                        TryBeginEdit(selection, dataSource, scroll, style, null);
                        e.Handled = true;
                    }
                }
                break;
            case KumikoKey.Up:
                selection.NavigateUp(totalRows, e.HasShift);
                EnsureCurrentCellVisible(scroll, selection, style, dataSource.Columns, dataSource);
                Redraw();
                e.Handled = true;
                break;
            case KumikoKey.Down:
                selection.NavigateDown(totalRows, e.HasShift);
                EnsureCurrentCellVisible(scroll, selection, style, dataSource.Columns, dataSource);
                Redraw();
                e.Handled = true;
                break;
            case KumikoKey.Left:
                selection.NavigateLeft(totalCols, e.HasShift);
                EnsureCurrentCellVisible(scroll, selection, style, dataSource.Columns, dataSource);
                Redraw();
                e.Handled = true;
                break;
            case KumikoKey.Right:
                selection.NavigateRight(totalCols, e.HasShift);
                EnsureCurrentCellVisible(scroll, selection, style, dataSource.Columns, dataSource);
                Redraw();
                e.Handled = true;
                break;
            case KumikoKey.Home:
                selection.NavigateHome(e.HasControl, totalRows, e.HasShift);
                EnsureCurrentCellVisible(scroll, selection, style, dataSource.Columns, dataSource);
                Redraw();
                e.Handled = true;
                break;
            case KumikoKey.End:
                selection.NavigateEnd(e.HasControl, totalRows, totalCols, e.HasShift);
                EnsureCurrentCellVisible(scroll, selection, style, dataSource.Columns, dataSource);
                Redraw();
                e.Handled = true;
                break;
            case KumikoKey.PageUp:
                selection.NavigatePageUp(pageSize, e.HasShift, totalRows);
                EnsureCurrentCellVisible(scroll, selection, style, dataSource.Columns, dataSource);
                Redraw();
                e.Handled = true;
                break;
            case KumikoKey.PageDown:
                selection.NavigatePageDown(pageSize, totalRows, e.HasShift);
                EnsureCurrentCellVisible(scroll, selection, style, dataSource.Columns, dataSource);
                Redraw();
                e.Handled = true;
                break;
            case KumikoKey.A:
                if (e.HasControl)
                {
                    selection.SelectAll();
                    Redraw();
                    e.Handled = true;
                }
                break;
            case KumikoKey.C when e.HasCommand:
                // Ctrl/Cmd+C copies the selected rows; with nothing selected the key is left to the host.
                if (CopySelection(selection, dataSource))
                    e.Handled = true;
                break;
            case KumikoKey.Escape:
                // Clears the selection and the current cell; with nothing to clear the key is left to the host.
                if (selection.CurrentCell.IsValid || selection.SelectedItems.Count > 0)
                {
                    selection.Clear();
                    Redraw();
                    e.Handled = true;
                }
                break;
            case KumikoKey.Tab:
                // Tab moves to the next tab stop (Shift+Tab the previous one) and edits it when the column's
                // edit triggers allow. With no current cell, or past the last (first) tab stop, the key is
                // left unhandled so the host moves focus out of the grid: Tab is never a keyboard trap.
                if (selection.CurrentCell.IsValid &&
                    MoveToNextTabStop(selection, dataSource, scroll, style, e.HasShift ? -1 : 1))
                {
                    BeginEditAfterTab(selection, dataSource, scroll, style);
                    Redraw();
                    e.Handled = true;
                }
                break;
            case KumikoKey.Space:
                // Space toggles the current Boolean cell; on any other cell it toggles the row's selection.
                if (!e.HasControl && !e.HasShift && CanToggleBooleanCell(selection.CurrentCell, dataSource))
                {
                    var current = selection.CurrentCell;
                    _editSession!.ToggleBooleanCell(current.Row, current.Column, dataSource.Columns[current.Column], dataSource);
                    Redraw();
                    e.Handled = true;
                }
                else if (selection.CurrentCell.IsValid)
                {
                    selection.HandleRowSelection(selection.CurrentCell.Row, false, true);
                    Redraw();
                    e.Handled = true;
                }
                break;

            default:
                // A typed space on an editable Boolean cell is Space: hosts that capture the keyboard through a
                // hidden text box (Uno's proxy TextBox, MAUI's Entry) can deliver it as text, not as Space (#179).
                if (e.Character == ' ' && !e.HasControl && !e.HasAlt && !e.HasShift &&
                    TryToggleCurrentBooleanCell(selection, dataSource))
                {
                    e.Handled = true;
                    break;
                }

                // Typing trigger: printable character starts editing
                if (_editSession != null && e.Character.HasValue && selection.CurrentCell.IsValid &&
                    !e.HasControl && !e.HasAlt)
                {
                    int typingCol = selection.CurrentCell.Column;
                    var typingColumn = typingCol >= 0 && typingCol < dataSource.Columns.Count
                        ? dataSource.Columns[typingCol] : null;
                    if ((GetEffectiveEditTriggers(typingColumn) & EditTrigger.Typing) != 0)
                    {
                        char c = e.Character.Value;
                        if (!char.IsControl(c))
                        {
                            TryBeginEdit(selection, dataSource, scroll, style, c);
                            e.Handled = true;
                        }
                    }
                }
                break;
        }
    }

    /// <summary>
    /// Tick the inertial scroller. Returns true if the scroll is still animating.
    /// Call this from a frame timer, passing the real time since the previous call. The fling ends as soon as a
    /// step cannot move the offset (content edge reached).
    /// </summary>
    /// <param name="scroll">Scroll state to advance.</param>
    /// <param name="frameIntervalMs">Measured milliseconds since the previous tick.</param>
    public bool UpdateInertialScroll(ScrollState scroll, float frameIntervalMs = 16f)
    {
        if (!_inertialScroller.IsActive) return false;

        var (dx, dy) = _inertialScroller.Update(frameIntervalMs);
        if (InertialScroller.IsZero(dx) && InertialScroller.IsZero(dy)) return _inertialScroller.IsActive;

        float beforeX = scroll.OffsetX, beforeY = scroll.OffsetY;
        scroll.ScrollBy(dx, dy);
        float movedX = scroll.OffsetX - beforeX, movedY = scroll.OffsetY - beforeY;
        _inertialScroller.ReportApplied(dx, dy, movedX, movedY);
        if (!InertialScroller.IsZero(movedX) || !InertialScroller.IsZero(movedY)) Redraw();
        return _inertialScroller.IsActive;
    }

    /// <summary>
    /// Advance edge autoscroll by one frame: while a row drag holds the pointer in an edge band
    /// (<see cref="IsAutoScrolling"/>), scroll toward that edge by the band's speed times
    /// <paramref name="frameIntervalMs"/>. The ghost stays under the pointer, so the drop target follows the rows
    /// scrolling by. Returns true while it keeps scrolling; false when there is nothing to do or the content edge was
    /// reached (autoscroll then waits for the pointer to move again). Call it from the frame loop or timer that runs
    /// <see cref="UpdateInertialScroll"/>; it allocates nothing.
    /// </summary>
    /// <param name="scroll">The grid's scroll state.</param>
    /// <param name="frameIntervalMs">The time since the previous frame, in milliseconds.</param>
    /// <returns>Whether the grid scrolled and autoscroll is still active.</returns>
    public bool UpdateAutoScroll(ScrollState scroll, float frameIntervalMs = 16f)
    {
        if (!IsAutoScrolling) return false;

        float oldY = scroll.OffsetY;
        scroll.ScrollBy(0, _autoScrollVelocityY * Math.Max(0, frameIntervalMs) / 1000f);
        if (scroll.OffsetY == oldY)
        {
            _autoScrollVelocityY = 0; // at the content edge
            return false;
        }

        Redraw();
        return true;
    }

    // ── Pointer handlers ──

    private void HandlePointerDown(
        KumikoPointerEventArgs e, ScrollState scroll,
        KumikoSelectionModel selection, KumikoStyle style,
        IReadOnlyList<KumikoColumn> columns, int totalRows,
        KumikoSource dataSource)
    {
        _inertialScroller.Reset();
        _isPanning = false;
        _isResizingColumn = false;
        _isDraggingColumn = false;
        _isDraggingRow = false;
        _dragRowIndex = -1;
        _armedDragRow = -1;
        _autoScrollVelocityY = 0;
        _pressDeviceType = e.DeviceType;
        _editorConsumedPointerDown = false;
        _lastPanX = e.X;
        _lastPanY = e.Y;
        _pressOriginX = e.X;
        _pressOriginY = e.Y;
        _pressRegion = HitRegion.None;
        _pressColumnIndex = _pressRowIndex = _pressGroupIndex = -1;

        // Active editor gets priority — its overlay (calendar, dropdown) may
        // extend outside the cell bounds, so we must check before grid hit-test.
        if (_editSession != null && _editSession.IsEditing)
        {
            if (_editSession.HandlePointerEvent(e))
            {
                // The editor handled the press. If it ended the edit session
                // (e.g. ComboBox dropdown item selected → commit), we still
                // need to suppress the upcoming pointer-up so it doesn't fall
                // through to grid-level cell selection.
                _editorConsumedPointerDown = true;
                e.Handled = true;
                Redraw();
                return;
            }
        }

        var hit = _hitTester.HitTest(e.X, e.Y, scroll, style, columns, totalRows, dataSource, e.DeviceType);
        _pressRegion = hit.Region;
        _pressColumnIndex = hit.ColumnIndex;
        _pressRowIndex = hit.RowIndex;
        _pressGroupIndex = hit.GroupDescriptionIndex;
        if (_feedback.BeginPress(FeedbackTargetFor(hit.Region, hit.RowIndex, hit.ColumnIndex, hit.Column,
                hit.GroupDescriptionIndex, e.X, e.Y, scroll, style, dataSource), e.X, e.Y, scroll))
            Redraw();

        // Group chips and group headers act on the release (HandlePointerUp), on the same target: a press that
        // becomes a pan (scrolling a touch screen over a group header) changes nothing.
        if (hit.Region is HitRegion.GroupPanelChipRemove or HitRegion.GroupPanelChip or HitRegion.GroupChevron
            or HitRegion.GroupHeaderRow)
        {
            e.Handled = true;
            return;
        }

        if (hit.Region == HitRegion.HeaderResizeGrip && hit.Column != null)
        {
            // Double-click on resize grip = auto-fit column width
            if (e.ClickCount == 2)
            {
                AutoFitColumnRequested?.Invoke(this, new AutoFitColumnEventArgs(
                    hit.ColumnIndex, hit.Column));
                e.Handled = true;
                return;
            }

            _isResizingColumn = true;
            _resizeColumnIndex = hit.ColumnIndex;
            _resizeStartX = e.X;
            _resizeStartWidth = hit.Column.Width;
            SetCursor(KumikoCursor.ResizeHorizontal);
            e.Handled = true;
            return;
        }

        // A press on a row's drag handle: the drag starts as soon as it moves, whatever the device or direction.
        if (hit.Region == HitRegion.RowDragHandle && style.ShowRowDragHandle && IsDataRow(dataSource, hit.RowIndex))
            _armedDragRow = hit.RowIndex;

        e.Handled = true;
    }

    private void HandlePointerMove(
        KumikoPointerEventArgs e, ScrollState scroll,
        KumikoSelectionModel selection, KumikoStyle style,
        IReadOnlyList<KumikoColumn> columns, int totalRows,
        KumikoSource dataSource)
    {
        // Forward move events to the active editor first — this enables hover
        // highlighting on dropdown items and prevents the grid from starting
        // a pan while the mouse is inside an editor overlay.
        if (_editSession != null && _editSession.IsEditing)
        {
            if (_editSession.HandlePointerEvent(e))
            {
                e.Handled = true;
                Redraw();
                return;
            }
        }

        if (_isResizingColumn)
        {
            HandleColumnResize(e, columns);
            e.Handled = true;
            return;
        }

        if (_isDraggingColumn)
        {
            DragColumnScreenX = e.X - _dragColumnOffset;
            Redraw();
            e.Handled = true;
            return;
        }

        if (_isDraggingRow)
        {
            UpdateRowDrag(e.Y, scroll, style, dataSource);
            e.Handled = true;
            return;
        }

        // A press on a drag handle starts the row drag as soon as it moves (past a tap's jitter), in any direction.
        if (_armedDragRow >= 0 && !_isPanning)
        {
            if (MathF.Abs(e.X - _pressOriginX) <= HandleDragSlop && MathF.Abs(e.Y - _pressOriginY) <= HandleDragSlop)
            {
                e.Handled = true;
                return;
            }

            int row = _armedDragRow;
            _armedDragRow = -1;
            if (row < totalRows && CommitEditBeforeViewChange(dataSource, selection))
            {
                BeginRowDrag(row, _pressOriginY, scroll, style, dataSource);
                UpdateRowDrag(e.Y, scroll, style, dataSource);
            }
            e.Handled = true;
            return;
        }

        float dx = _lastPanX - e.X;
        float dy = _lastPanY - e.Y;

        if (!_isPanning && !_isDraggingColumn && !_isDraggingRow)
        {
            float distance = MathF.Sqrt(dx * dx + dy * dy);

            if (distance > DragThreshold)
            {
                // Check if this started on a header (potential column reorder)
                var startHit = _hitTester.HitTest(_lastPanX, _lastPanY, scroll, style, columns, totalRows, dataSource,
                    _pressDeviceType);
                if (startHit.Region == HitRegion.HeaderCell && startHit.Column?.AllowReorder == true &&
                    Math.Abs(e.X - _lastPanX) > Math.Abs(e.Y - _lastPanY) &&
                    CommitEditBeforeViewChange(dataSource, selection))
                {
                    _isDraggingColumn = true;
                    _dragColumnIndex = startHit.ColumnIndex;
                    // The ghost keeps the point where the header was grabbed, whatever pane it is in.
                    float columnLeft = ColumnDragGeometry.ScreenLeft(columns, startHit.ColumnIndex, scroll, style);
                    _dragColumnOffset = float.IsNaN(columnLeft) ? 0 : _lastPanX - columnLeft;
                    DragColumnScreenX = e.X - _dragColumnOffset;
                    if (CanAnimate(style)) _animations.StartLift();
                    RequestHaptic(KumikoHapticKind.DragStart, _pressDeviceType);
                    e.Handled = true;
                    return;
                }

                // A mouse or pen drag that started on a data cell reorders the row (with AllowRowDragDrop); a touch
                // drag pans instead, and a touch long press starts the row drag (HandleLongPress).
                if (startHit.Region == HitRegion.Cell && style.AllowRowDragDrop &&
                    _pressDeviceType != KumikoPointerDeviceType.Touch &&
                    Math.Abs(e.Y - _lastPanY) > Math.Abs(e.X - _lastPanX) &&
                    IsDataRow(dataSource, startHit.RowIndex) &&
                    CommitEditBeforeViewChange(dataSource, selection))
                {
                    BeginRowDrag(startHit.RowIndex, _lastPanY, scroll, style, dataSource);
                    UpdateRowDrag(e.Y, scroll, style, dataSource);
                    e.Handled = true;
                    return;
                }
            }

            if (distance > PanThreshold)
            {
                _isPanning = true;
                // Reset last-pan to current position to avoid a big jump
                // on the first frame of panning
                _lastPanX = e.X;
                _lastPanY = e.Y;
            }
        }

        if (_isPanning)
        {
            float panDx = _lastPanX - e.X;
            float panDy = _lastPanY - e.Y;
            _inertialScroller.TrackVelocity(panDx, panDy, e.TimestampMs);
            scroll.ScrollBy(panDx, panDy);
            _lastPanX = e.X;
            _lastPanY = e.Y;
            Redraw();
            e.Handled = true;
        }
    }

    /// <summary>
    /// A pointer moving with no button pressed: hover feedback for an open editor (its dropdown items), then
    /// the cursor for what is under the pointer. Never scrolls, drags or selects.
    /// </summary>
    private void HandleHover(KumikoPointerEventArgs e, ScrollState scroll, KumikoStyle style, KumikoSource dataSource)
    {
        if (_editSession != null && _editSession.IsEditing && _editSession.HandlePointerEvent(e))
        {
            Redraw();
            SetCursor(_isResizingColumn ? KumikoCursor.ResizeHorizontal : KumikoCursor.Default);
            _feedback.ClearHover(); // the editor's own feedback; the redraw above shows it
            return;
        }

        UpdatePointerFeedback(e.X, e.Y, e.DeviceType, scroll, style, dataSource);
    }

    /// <summary>
    /// The cursor and the hover feedback for a pointer resting at viewport point (<paramref name="x"/>,
    /// <paramref name="y"/>), from one hit test. A finger hovers nothing. Redraws only when the hovered target changes.
    /// </summary>
    private void UpdatePointerFeedback(float x, float y, KumikoPointerDeviceType deviceType,
        ScrollState scroll, KumikoStyle style, KumikoSource dataSource)
    {
        var hit = _hitTester.HitTestCore(x, y, scroll, style, dataSource.Columns, dataSource.RowCount, dataSource, deviceType);
        var target = FeedbackTargetFor(hit.Region, hit.RowIndex, hit.ColumnIndex, hit.Column, hit.GroupDescriptionIndex,
            x, y, scroll, style, dataSource);
        SetCursor(_isResizingColumn ? KumikoCursor.ResizeHorizontal : CursorFor(hit.Region, target));
        if (_feedback.SetHover(deviceType == KumikoPointerDeviceType.Touch || _isResizingColumn ? FeedbackTarget.None : target,
                x, y, scroll))
            Redraw();
    }

    /// <summary>
    /// The cursor over a region: resize over a resize grip, a hand over something a click acts on directly (a filter
    /// icon, a chevron, a chip, a drag handle, a cell's control: <paramref name="target"/>), else the default.
    /// </summary>
    private static KumikoCursor CursorFor(HitRegion region, FeedbackTarget target) => region switch
    {
        HitRegion.HeaderResizeGrip => KumikoCursor.ResizeHorizontal,
        HitRegion.HeaderFilterIcon or HitRegion.GroupChevron or HitRegion.GroupPanelChip or HitRegion.GroupPanelChipRemove
            or HitRegion.RowDragHandle => KumikoCursor.Hand,
        HitRegion.Cell when !target.IsNone => KumikoCursor.Hand,
        _ => KumikoCursor.Default,
    };

    /// <summary>
    /// What gets hover and pressed feedback at a hit: a sortable header, a filter icon, a chip or its remove button, a
    /// group header's chevron (the whole header toggles), or the control a click on a cell acts on (an enabled action
    /// button, a check box that can be toggled); <see cref="FeedbackTarget.None"/> elsewhere. Allocation-free beyond
    /// reading an interactive cell's value. It reads the columns and row count afresh: a release may just have removed
    /// rows.
    /// </summary>
    private FeedbackTarget FeedbackTargetFor(HitRegion region, int row, int columnIndex, KumikoColumn? column, int group,
        float x, float y, ScrollState scroll, KumikoStyle style, KumikoSource dataSource)
    {
        switch (region)
        {
            case HitRegion.HeaderCell when column != null && AllowSorting && column.AllowSorting:
                return new FeedbackTarget(HitRegion.HeaderCell, -1, columnIndex, -1, -1);
            case HitRegion.HeaderFilterIcon:
                return new FeedbackTarget(HitRegion.HeaderFilterIcon, -1, columnIndex, -1, -1);
            case HitRegion.GroupPanelChip or HitRegion.GroupPanelChipRemove:
                return new FeedbackTarget(region, -1, -1, group, -1);
            case HitRegion.GroupChevron or HitRegion.GroupHeaderRow:
                return new FeedbackTarget(HitRegion.GroupHeaderRow, row, -1, -1, -1);
            case HitRegion.Cell when column != null && row >= 0 && row < dataSource.RowCount && !dataSource.IsNonDataRow(row):
                if (GetInteractiveRenderer(column) is { } interactive)
                {
                    var bounds = GetInteractiveCellBounds(row, columnIndex, scroll, style, dataSource);
                    int control = interactive.GetControlIndexAt(bounds, x, y, dataSource.GetCellValue(row, column));
                    return control >= 0 ? new FeedbackTarget(HitRegion.Cell, row, columnIndex, -1, control) : FeedbackTarget.None;
                }

                var cell = new CellPosition(row, columnIndex);
                return column.ColumnType == KumikoColumnType.Boolean && CanToggleBooleanCell(cell, dataSource) &&
                       IsOnCheckBox(x, y, cell, scroll, style, dataSource)
                    ? new FeedbackTarget(HitRegion.Cell, row, columnIndex, -1, 0)
                    : FeedbackTarget.None;
            default:
                return FeedbackTarget.None;
        }
    }

    /// <summary>
    /// A press moved: its target is drawn pressed only while the pointer is still on it, and not at all once the press
    /// became a pan, a drag or a resize. Redraws only when that changes.
    /// </summary>
    private void TrackPressFeedback(KumikoPointerEventArgs e, ScrollState scroll, KumikoStyle style, KumikoSource dataSource)
    {
        if (_feedback.Pressed.IsNone) return;
        if (_isPanning || _isDraggingColumn || _isDraggingRow || _isResizingColumn)
        {
            if (_feedback.EndPress()) Redraw();
            return;
        }

        var hit = _hitTester.HitTestCore(e.X, e.Y, scroll, style, dataSource.Columns, dataSource.RowCount, dataSource,
            _pressDeviceType);
        var under = FeedbackTargetFor(hit.Region, hit.RowIndex, hit.ColumnIndex, hit.Column, hit.GroupDescriptionIndex,
            e.X, e.Y, scroll, style, dataSource);
        if (_feedback.TrackPress(under, e.X, e.Y)) Redraw();
    }

    /// <summary>Sets <see cref="Cursor"/>, raising <see cref="CursorChanged"/> only when it changes.</summary>
    private void SetCursor(KumikoCursor cursor)
    {
        if (Cursor == cursor) return;
        Cursor = cursor;
        CursorChanged?.Invoke(this, cursor);
    }

    private void HandlePointerUp(
        KumikoPointerEventArgs e, ScrollState scroll,
        KumikoSelectionModel selection, KumikoStyle style,
        IReadOnlyList<KumikoColumn> columns, int totalRows,
        KumikoSource dataSource)
    {
        if (_isResizingColumn)
        {
            _isResizingColumn = false;

            // Only a drag that changed the width resized the column: a click on the grip (the first click of a
            // double-click that auto-fits, say) raises nothing.
            if (_resizeColumnIndex >= 0 && _resizeColumnIndex < columns.Count &&
                MathF.Abs(columns[_resizeColumnIndex].Width - _resizeStartWidth) > 0.01f)
            {
                ColumnResized?.Invoke(this, new ColumnResizedEventArgs(
                    _resizeColumnIndex, columns[_resizeColumnIndex]));
            }
            e.Handled = true;
            return;
        }

        if (_isDraggingColumn)
        {
            var dragged = _dragColumnIndex >= 0 && _dragColumnIndex < columns.Count ? columns[_dragColumnIndex] : null;
            float releasedAt = DragColumnScreenX;
            FinishColumnReorder(e.X, scroll, columns, style);
            _isDraggingColumn = false;
            _dragColumnIndex = -1;
            SettleDroppedColumn(dragged, releasedAt, scroll, style, dataSource);
            RequestHaptic(KumikoHapticKind.Drop, _pressDeviceType);
            Redraw();
            e.Handled = true;
            return;
        }

        _armedDragRow = -1;
        if (_isDraggingRow)
        {
            UpdateRowDrag(e.Y, scroll, style, dataSource);
            int from = _dragRowIndex;
            int to = RowDragGeometry.DropTarget(DragRowScreenY, scroll, style, dataSource);
            EndRowDrag();
            RequestHaptic(KumikoHapticKind.Drop, _pressDeviceType);
            FinishRowReorder(from, to, dataSource);
            Redraw();
            e.Handled = true;
            return;
        }

        if (_isPanning)
        {
            _isPanning = false;
            _inertialScroller.StartFling(e.TimestampMs);
            e.Handled = true;
            return;
        }

        // ── Active editor gets priority on pointer-up ──
        // The editor's overlay (dropdown, spinner buttons) may extend outside
        // the cell bounds, so we must forward pointer-up before grid hit-test.
        if (_editSession != null && _editSession.IsEditing)
        {
            if (_editSession.HandlePointerEvent(e))
            {
                _editorConsumedPointerDown = false;
                e.Handled = true;
                Redraw();
                return;
            }
        }

        // If the editor consumed the pointer-down (e.g. a ComboBox dropdown item
        // was selected, which committed the edit and ended the session), suppress
        // the pointer-up too so it doesn't fall through to grid cell selection.
        if (_editorConsumedPointerDown)
        {
            _editorConsumedPointerDown = false;
            e.Handled = true;
            Redraw();
            return;
        }

        // This was a tap
        var hit = _hitTester.HitTest(e.X, e.Y, scroll, style, columns, totalRows, dataSource, _pressDeviceType);

        // Group chips and group headers act here, on the release, when the press landed on the same target.
        if (hit.Region is HitRegion.GroupPanelChipRemove or HitRegion.GroupPanelChip)
        {
            if (_pressRegion == hit.Region && _pressGroupIndex == hit.GroupDescriptionIndex)
                ApplyChipTap(hit.Region, hit.GroupDescriptionIndex, selection, dataSource);
            e.Handled = true;
            return;
        }

        if (hit.Region is HitRegion.GroupChevron or HitRegion.GroupHeaderRow)
        {
            if (_pressRegion is HitRegion.GroupChevron or HitRegion.GroupHeaderRow && _pressRowIndex == hit.RowIndex &&
                CommitEditBeforeViewChange(dataSource, selection))
            {
                int rowsBefore = dataSource.RowCount;
                ToggleTappedGroup(dataSource, scroll, style, hit.RowIndex);
                if (CanAnimate(style)) _animations.StartGroupToggle(hit.RowIndex, dataSource.RowCount - rowsBefore);
                Redraw();
            }
            e.Handled = true;
            return;
        }

        if (hit.Region == HitRegion.GroupSummaryRow || hit.Region == HitRegion.TableSummaryRow ||
            hit.Region == HitRegion.RowDragHandle)
        {
            e.Handled = true;
            return;
        }

        if (hit.Region == HitRegion.HeaderFilterIcon && hit.Column != null)
        {
            if (_pressRegion == HitRegion.HeaderFilterIcon && _pressColumnIndex == hit.ColumnIndex &&
                AllowFiltering && hit.Column.AllowFiltering && CommitEditBeforeViewChange(dataSource, selection))
            {
                ShowFilterPopup(hit.Column, hit.ColumnIndex, scroll, style, dataSource);
            }
            e.Handled = true;
            return;
        }

        if (hit.Region == HitRegion.HeaderCell && hit.Column != null)
        {
            // A header sorts only when it was pressed too (a press on a cell that slides up onto it does not)
            if (_pressRegion != HitRegion.HeaderCell || _pressColumnIndex != hit.ColumnIndex)
            {
                e.Handled = true;
                return;
            }
            if (!CommitEditBeforeViewChange(dataSource, selection)) { e.Handled = true; return; }
            bool ctrl = (e.Modifiers & InputModifiers.Control) != 0;
            bool shift = (e.Modifiers & InputModifiers.Shift) != 0;

            if (AllowSorting)
            {
                if (ctrl || shift)
                    dataSource.ToggleMultiSort(hit.Column);
                else
                    dataSource.ToggleSort(hit.Column);
            }

            HeaderTapped?.Invoke(this, new KumikoHeaderTappedEventArgs(hit.ColumnIndex, hit.Column, e.X, e.Y, e.Modifiers));

            Redraw();
            e.Handled = true;
            return;
        }

        if (hit.Region == HitRegion.EmptyArea && selection.ClearSelectionOnEmptyAreaTap &&
            IsInRowsArea(e.Y, scroll, style, dataSource))
        {
            // A tap on the empty part of the rows area (below the last row, right of the last column)
            // commits an open edit and clears the selection; a refused commit keeps both.
            _lastTapToggledCell = CellPosition.Invalid;
            if (CommitEditBeforeViewChange(dataSource, selection) &&
                (selection.CurrentCell.IsValid || selection.SelectedItems.Count > 0))
            {
                selection.Clear();
                Redraw();
            }
            e.Handled = true;
            return;
        }

        // A press that started on something else (a header, a group row, a chip) and slid onto a cell is not a tap on it
        if (hit.Region == HitRegion.Cell && _pressRegion != HitRegion.Cell)
        {
            e.Handled = true;
            return;
        }

        if (hit.Region == HitRegion.Cell && hit.RowIndex >= 0)
        {
            bool shift = (e.Modifiers & InputModifiers.Shift) != 0;
            bool ctrl = (e.Modifiers & InputModifiers.Control) != 0;

            // If editing, click outside editor → commit and continue to select. A refused commit keeps the
            // editor open with its message, and the selection where it is.
            if (!CommitEditBeforeViewChange(dataSource, selection))
            {
                e.Handled = true;
                return;
            }

            selection.HandleRowSelection(hit.RowIndex, shift, ctrl);
            selection.CurrentCell = new CellPosition(hit.RowIndex, hit.ColumnIndex);

            var tappedItem = dataSource.GetItem(hit.RowIndex);
            RowTapped?.Invoke(this, new RowTappedEventArgs(hit.RowIndex, hit.ColumnIndex, tappedItem));
            if (CellTapped != null && hit.Column != null)
            {
                CellTapped(this, new KumikoCellTappedEventArgs(
                    hit.RowIndex, hit.ColumnIndex, hit.Column, tappedItem, e.X, e.Y, e.ClickCount));
            }

            var tappedCell = new CellPosition(hit.RowIndex, hit.ColumnIndex);
            bool isBooleanCell = hit.Column?.ColumnType == KumikoColumnType.Boolean;

            if (e.ClickCount == 2)
            {
                RowDoubleTapped?.Invoke(this, new RowTappedEventArgs(
                    hit.RowIndex, hit.ColumnIndex, dataSource.GetItem(hit.RowIndex)));

                // The second tap of a double-tap toggles a Boolean cell (DoubleTap trigger) unless the first
                // tap already did: a double-tap never toggles twice.
                bool toggledByFirstTap = _lastTapToggledCell == tappedCell;
                _lastTapToggledCell = CellPosition.Invalid;

                // Double-tap edit trigger
                if (_editSession != null && hit.Column != null && GetInteractiveRenderer(hit.Column) == null &&
                    (GetEffectiveEditTriggers(hit.Column) & EditTrigger.DoubleTap) != 0)
                {
                    if (isBooleanCell)
                    {
                        if (!toggledByFirstTap)
                            _editSession.ToggleBooleanCell(hit.RowIndex, hit.ColumnIndex, hit.Column, dataSource);
                    }
                    else
                    {
                        TryBeginEdit(selection, dataSource, scroll, style, null);
                    }
                }
            }
            else if (e.ClickCount == 1 && hit.Column != null)
            {
                _lastTapToggledCell = CellPosition.Invalid;

                if (GetInteractiveRenderer(hit.Column) is { } interactive)
                {
                    // An interactive cell (action buttons) acts on the tap itself: no editor, no edit events.
                    // The control may remove the row (even the last one), so nothing about the row is read
                    // after it runs: the cell value and bounds are taken first, and only a redraw follows.
                    var value = dataSource.GetCellValue(hit.RowIndex, hit.Column);
                    var bounds = GetInteractiveCellBounds(hit.RowIndex, hit.ColumnIndex, scroll, style, dataSource);
                    interactive.OnCellTap(bounds, e.X, e.Y, value);
                }
                else if (isBooleanCell)
                {
                    // A single tap toggles a Boolean cell when it lands on the check box, or anywhere in the
                    // cell with the SingleTap trigger; elsewhere it only selects. EditTriggers=None never toggles.
                    if (CanToggleBooleanCell(tappedCell, dataSource) &&
                        ((GetEffectiveEditTriggers(hit.Column) & EditTrigger.SingleTap) != 0 ||
                         IsOnCheckBox(e.X, e.Y, tappedCell, scroll, style, dataSource)))
                    {
                        _editSession!.ToggleBooleanCell(hit.RowIndex, hit.ColumnIndex, hit.Column, dataSource);
                        _lastTapToggledCell = tappedCell;
                    }
                }
                // Single-tap edit trigger for non-boolean cells
                else if (_editSession != null &&
                    (GetEffectiveEditTriggers(hit.Column) & EditTrigger.SingleTap) != 0)
                {
                    TryBeginEdit(selection, dataSource, scroll, style, null, e);
                }
            }

            Redraw();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Handle touch/pointer cancelled (e.g. finger left the screen bounds).
    /// Preserve scroll momentum instead of freezing.
    /// </summary>
    private void HandlePointerCancelled(KumikoPointerEventArgs e, ScrollState scroll, KumikoStyle style, KumikoSource dataSource)
    {
        _editorConsumedPointerDown = false;

        if (_isResizingColumn)
        {
            _isResizingColumn = false;
            SetCursor(KumikoCursor.Default);
            e.Handled = true;
            return;
        }

        if (_isDraggingColumn)
        {
            // Cancel column drag — the header settles back where it was
            var dragged = _dragColumnIndex >= 0 && _dragColumnIndex < dataSource.Columns.Count ? dataSource.Columns[_dragColumnIndex] : null;
            _isDraggingColumn = false;
            _dragColumnIndex = -1;
            SettleDroppedColumn(dragged, DragColumnScreenX, scroll, style, dataSource);
            Redraw();
            e.Handled = true;
            return;
        }

        _armedDragRow = -1;
        if (_isDraggingRow)
        {
            // Cancel row drag — snap back
            EndRowDrag();
            Redraw();
            e.Handled = true;
            return;
        }

        if (_isPanning)
        {
            // Touch left bounds — start inertial scroll with current velocity
            _isPanning = false;
            _inertialScroller.StartFling(e.TimestampMs);
            e.Handled = true;
            return;
        }
    }

    /// <summary>
    /// The wheel or a trackpad scrolls the grid: notches by <see cref="ScrollSettings.WheelScrollMultiplier"/>
    /// rows, pixels as they are, and with Shift held a vertical-only wheel scrolls horizontally. The event is
    /// handled only when the grid actually moved, so at the end of its content the page around it scrolls on.
    /// </summary>
    private void HandleScroll(KumikoPointerEventArgs e, ScrollState scroll, KumikoStyle style)
    {
        _inertialScroller.Reset();
        float pixelsPerNotch = style.RowHeight * _inertialScroller.Settings.WheelScrollMultiplier;
        float dx = e.GetScrollPixelsX(pixelsPerNotch);
        float dy = e.GetScrollPixelsY(pixelsPerNotch);
        if ((e.Modifiers & InputModifiers.Shift) != 0 && Math.Abs(dx) < 0.01f)
        {
            dx = dy;
            dy = 0;
        }

        float oldX = scroll.OffsetX, oldY = scroll.OffsetY;
        scroll.ScrollBy(-dx, -dy);
        if (Math.Abs(scroll.OffsetX - oldX) <= 0.01f && Math.Abs(scroll.OffsetY - oldY) <= 0.01f) return;

        Redraw();
        e.Handled = true;
    }

    /// <summary>
    /// Handle a long press gesture: on touch it may start a row drag, and a cell with the LongPress edit trigger
    /// edits. Otherwise a finger or a pen raises <see cref="ContextRequested"/> (and the rest of the press is
    /// ignored), and a mouse selects the cell.
    /// </summary>
    private void HandleLongPress(
        KumikoPointerEventArgs e,
        ScrollState scroll, KumikoSelectionModel selection, KumikoStyle style,
        IReadOnlyList<KumikoColumn> columns, int totalRows,
        KumikoSource dataSource)
    {
        // A long press that arrives after the press became a pan or a drag (the host's timer allows a little
        // movement) belongs to that gesture: it neither selects nor edits.
        if (_isPanning || _isDraggingRow || _isDraggingColumn || _isResizingColumn)
        {
            e.Handled = true;
            return;
        }

        var hit = _hitTester.HitTest(e.X, e.Y, scroll, style, columns, totalRows, dataSource, e.DeviceType);

        // A touch long press on a row (with AllowRowDragDrop) or on its drag handle starts the row drag, unless the
        // press already became a pan or a drag, or the cell edits on a long press (LongPress edit trigger).
        if (TryBeginRowDragOnLongPress(e, hit, scroll, selection, style, dataSource))
        {
            e.Handled = true;
            return;
        }

        // A finger or a pen asks for a context menu, unless the cell edits on a long press. A mouse held down is a
        // slow click, not a request for a menu (the secondary button is).
        bool editsOnLongPress = hit.Region == HitRegion.Cell && hit.RowIndex >= 0 && _editSession != null &&
            hit.Column != null && (GetEffectiveEditTriggers(hit.Column) & EditTrigger.LongPress) != 0;
        if (e.DeviceType != KumikoPointerDeviceType.Mouse && !editsOnLongPress)
        {
            // Inside the open editor the press stays the editor's: no menu over it, and nothing is committed
            if (_editSession is { IsEditing: true, ActiveEditor: { } editor } && editor.HitTest(e.X, e.Y))
            {
                e.Handled = true;
                return;
            }

            if (RequestContext(KumikoContextTrigger.LongPress, e, hit, selection, dataSource))
            {
                RequestHaptic(KumikoHapticKind.LongPress, e.DeviceType);
                _pressConsumedByContext = true;
                Redraw();
                e.Handled = true;
            }
            return;
        }

        if (hit.Region != HitRegion.Cell || hit.RowIndex < 0) return;

        // An open edit is committed first; a refused commit keeps the editor and the selection
        if (!CommitEditBeforeViewChange(dataSource, selection))
        {
            e.Handled = true;
            return;
        }

        // Select the cell under the long press
        selection.CurrentCell = new CellPosition(hit.RowIndex, hit.ColumnIndex);

        if (_editSession != null && hit.Column != null &&
            (GetEffectiveEditTriggers(hit.Column) & EditTrigger.LongPress) != 0)
        {
            RequestHaptic(KumikoHapticKind.LongPress, e.DeviceType);
            if (hit.Column.ColumnType == KumikoColumnType.Boolean)
            {
                _editSession.ToggleBooleanCell(
                    hit.RowIndex, hit.ColumnIndex, hit.Column, dataSource);
            }
            else
            {
                TryBeginEdit(selection, dataSource, scroll, style, null);
            }
        }

        Redraw();
        e.Handled = true;
    }

    // ── Column resize ──

    private void HandleColumnResize(KumikoPointerEventArgs e, IReadOnlyList<KumikoColumn> columns)
    {
        if (_resizeColumnIndex < 0 || _resizeColumnIndex >= columns.Count) return;

        var col = columns[_resizeColumnIndex];
        float delta = e.X - _resizeStartX;
        // Not Math.Clamp: a MinWidth above MaxWidth wins instead of throwing. A Star or Auto column switches to
        // Fixed, so the next layout pass keeps the width instead of sharing it out or measuring it again (#24).
        float newWidth = Math.Max(col.MinWidth, Math.Min(_resizeStartWidth + delta, col.MaxWidth));
        col.ApplyUserWidth(newWidth);
        Redraw();
    }

    // ── Column reorder ──

    private void FinishColumnReorder(
        float dropX, ScrollState scroll,
        IReadOnlyList<KumikoColumn> columns, KumikoStyle style)
    {
        if (_dragColumnIndex < 0 || _dragColumnIndex >= columns.Count) return;

        // Pane-aware: the slot among the other columns of the dragged column's own pane (ColumnDragGeometry).
        int dropIndex = ColumnDragGeometry.DropIndex(columns, _dragColumnIndex, dropX, scroll, style);
        if (dropIndex >= 0 && dropIndex != _dragColumnIndex)
        {
            // The host applies the move (KumikoSource.ReorderColumn), so it can keep its own column list in step.
            ColumnReordered?.Invoke(this, new KumikoColumnReorderedEventArgs(
                _dragColumnIndex, dropIndex, columns[_dragColumnIndex]));
        }
    }

    /// <summary>
    /// A column header was dropped (or the drag canceled) with its ghost at viewport <paramref name="releasedAt"/>: the
    /// ghost settles into the slot the column now has, after the host applied the move (#101).
    /// </summary>
    private void SettleDroppedColumn(KumikoColumn? column, float releasedAt, ScrollState scroll, KumikoStyle style,
        KumikoSource dataSource)
    {
        if (column == null || !CanAnimate(style)) return;
        var columns = dataSource.Columns;
        for (int i = 0; i < columns.Count; i++)
        {
            if (!ReferenceEquals(columns[i], column)) continue;
            _animations.StartDrop(column, releasedAt, ColumnDragGeometry.ScreenLeft(columns, i, scroll, style));
            return;
        }
    }

    // ── Row reorder ──

    private static bool IsDataRow(KumikoSource dataSource, int row) =>
        row >= 0 && row < dataSource.RowCount && !dataSource.IsNonDataRow(row);

    /// <summary>
    /// A touch long press starts a row drag on a data row: anywhere on the row with
    /// <see cref="KumikoStyle.AllowRowDragDrop"/> (unless the cell's edit triggers include
    /// <see cref="EditTrigger.LongPress"/>, which then edits), or on its drag handle. Not once the press has become a
    /// pan or another drag. True when the drag started, or the press was consumed by a refused commit.
    /// </summary>
    private bool TryBeginRowDragOnLongPress(
        KumikoPointerEventArgs e, KumikoHitTestResult hit, ScrollState scroll,
        KumikoSelectionModel selection, KumikoStyle style, KumikoSource dataSource)
    {
        if (e.DeviceType != KumikoPointerDeviceType.Touch || !IsDataRow(dataSource, hit.RowIndex) ||
            _isPanning || _isDraggingRow || _isDraggingColumn || _isResizingColumn)
            return false;

        bool onHandle = hit.Region == HitRegion.RowDragHandle && style.ShowRowDragHandle;
        bool onCell = hit.Region == HitRegion.Cell && style.AllowRowDragDrop &&
                      !(_editSession != null && hit.Column != null &&
                        (GetEffectiveEditTriggers(hit.Column) & EditTrigger.LongPress) != 0);
        if (!onHandle && !onCell) return false;

        _armedDragRow = -1;
        if (CommitEditBeforeViewChange(dataSource, selection))
        {
            BeginRowDrag(hit.RowIndex, e.Y, scroll, style, dataSource);
            UpdateRowDrag(e.Y, scroll, style, dataSource);
            _autoScrollVelocityY = 0; // a row held in an edge band scrolls once the finger moves
        }
        return true;
    }

    /// <summary>Starts dragging <paramref name="row"/>, grabbed at viewport y <paramref name="grabY"/>.</summary>
    private void BeginRowDrag(int row, float grabY, ScrollState scroll, KumikoStyle style, KumikoSource dataSource)
    {
        _inertialScroller.Reset();
        _isPanning = false;
        _isDraggingRow = true;
        _dragRowIndex = row;
        RequestHaptic(KumikoHapticKind.DragStart, _pressDeviceType);
        _dragRowOffset = grabY - RowDragGeometry.RowTop(row, scroll, style, dataSource);
        _autoScrollVelocityY = 0;
    }

    /// <summary>
    /// Moves the ghost with the pointer at viewport y <paramref name="pointerY"/> and sets the edge autoscroll speed
    /// for that position.
    /// </summary>
    private void UpdateRowDrag(float pointerY, ScrollState scroll, KumikoStyle style, KumikoSource dataSource)
    {
        DragRowScreenY = RowDragGeometry.ClampGhostTop(pointerY - _dragRowOffset, scroll, style, dataSource);
        _autoScrollVelocityY = ComputeAutoScrollVelocity(
            pointerY, RowDragGeometry.ScrollableTop(style, dataSource),
            RowDragGeometry.RowsBottom(scroll, style, dataSource), scroll);
        Redraw();
    }

    private void EndRowDrag()
    {
        _isDraggingRow = false;
        _dragRowIndex = -1;
        _autoScrollVelocityY = 0;
    }

    /// <summary>
    /// The edge autoscroll speed (pixels per second, positive scrolls down) for a pointer at viewport y
    /// <paramref name="pointerY"/> over a scrolling area from <paramref name="top"/> to <paramref name="bottom"/>: in
    /// a band of <see cref="ScrollSettings.AutoScrollEdgeSize"/> at either edge (at most a third of the area), the
    /// speed grows linearly from 0 at the band's inner edge to <see cref="ScrollSettings.AutoScrollMaxSpeed"/> at its
    /// outer edge and stays there beyond it. 0 elsewhere, and when the grid cannot scroll further that way. It knows
    /// nothing of rows, so another drag (a drag-select) can share it.
    /// </summary>
    private float ComputeAutoScrollVelocity(float pointerY, float top, float bottom, ScrollState scroll)
    {
        var settings = _inertialScroller.Settings;
        float edge = Math.Min(settings.AutoScrollEdgeSize, (bottom - top) / 3);
        if (edge <= 0 || settings.AutoScrollMaxSpeed <= 0) return 0;

        if (pointerY < top + edge && scroll.OffsetY > 0)
            return -settings.AutoScrollMaxSpeed * Math.Min(1f, (top + edge - pointerY) / edge);
        if (pointerY > bottom - edge && scroll.OffsetY < scroll.MaxOffsetY)
            return settings.AutoScrollMaxSpeed * Math.Min(1f, (pointerY - (bottom - edge)) / edge);
        return 0;
    }

    /// <summary>
    /// Drops dragged row <paramref name="from"/> on row <paramref name="to"/>: the source moves it
    /// (<see cref="KumikoSource.ReorderRow"/>), then exactly one of <see cref="RowReordered"/> (it moved) or
    /// <see cref="RowReorderRejected"/> (it did not, with the reason) is raised. A drop on itself raises nothing.
    /// </summary>
    private void FinishRowReorder(int from, int to, KumikoSource dataSource)
    {
        if (from < 0 || to < 0 || from == to || !IsDataRow(dataSource, from)) return;

        object dataItem = dataSource.GetItem(from);
        var result = dataSource.ReorderRow(from, to);
        if (result == KumikoReorderResult.NoOp) return;

        var args = new RowReorderedEventArgs(from, to, dataItem, result);
        if (result == KumikoReorderResult.Moved)
            RowReordered?.Invoke(this, args);
        else
            RowReorderRejected?.Invoke(this, args);
    }

    // ── Scroll helpers ──

    /// <summary>
    /// Toggle the group whose header was tapped. When that collapses a group whose header was pinned at the top of the
    /// data area (#174) — its own row scrolled above its pinned slot — the view scrolls so the collapsed header stays
    /// where it was pinned, instead of jumping to whatever followed the group's rows.
    /// </summary>
    /// <summary>
    /// A tap on a grouping panel chip: its remove button removes the grouping, the rest of it reverses the group's
    /// sort direction. An open edit is committed first; a refused commit does nothing.
    /// </summary>
    private void ApplyChipTap(HitRegion region, int groupIndex, KumikoSelectionModel selection, KumikoSource dataSource)
    {
        var groups = dataSource.GroupDescriptions;
        if (groupIndex < 0 || groupIndex >= groups.Count || !CommitEditBeforeViewChange(dataSource, selection)) return;

        var group = groups[groupIndex];
        if (region == HitRegion.GroupPanelChipRemove)
        {
            dataSource.RemoveGroupDescription(group.PropertyName);
        }
        else
        {
            group.GroupSortDirection = group.GroupSortDirection == KumikoSortDirection.Ascending
                ? KumikoSortDirection.Descending
                : KumikoSortDirection.Ascending;
            dataSource.Refresh();
        }
        Redraw();
    }

    private static void ToggleTappedGroup(KumikoSource dataSource, ScrollState scroll, KumikoStyle style, int headerRow)
    {
        bool wasExpanded = dataSource.GetGroupHeaderInfo(headerRow)?.IsExpanded == true;
        dataSource.ToggleGroupExpansion(headerRow);

        // The header keeps its row: only rows after it change.
        if (!wasExpanded || !style.StickyGroupHeaders || dataSource.GetGroupHeaderInfo(headerRow) is not { IsExpanded: false } collapsed)
            return;

        int frozenRowCount = dataSource.EffectiveFrozenRowCount;
        if (headerRow < frozenRowCount) return;

        float slotOffset = PinnedGroupHeaders.SlotOffset(headerRow, collapsed.Level, frozenRowCount, style.RowHeight);
        if (scroll.OffsetY > slotOffset)
        {
            scroll.OffsetY = Math.Max(0, slotOffset);
            scroll.ClampOffset();
        }
    }

    /// <summary>
    /// Scroll so the current cell is fully visible. Vertically a scrollable row is kept between the fixed bands
    /// (grouping panel, header, top summaries, frozen rows) and the pinned bottom summary footer; a frozen row
    /// needs no scroll. Horizontally a scrollable column is kept between the left-frozen columns and the
    /// right-frozen columns, net of the row drag handle; a frozen column needs no scroll.
    /// </summary>
    private void EnsureCurrentCellVisible(
        ScrollState scroll, KumikoSelectionModel selection,
        KumikoStyle style, IReadOnlyList<KumikoColumn> columns,
        KumikoSource? dataSource = null)
    {
        if (!selection.CurrentCell.IsValid) return;

        int row = selection.CurrentCell.Row;
        int col = selection.CurrentCell.Column;

        float offsetY = dataSource != null
            ? KumikoScroller.GetRowOffsetY(dataSource, style, scroll, row)
            : scroll.OffsetY;
        float offsetX = KumikoScroller.GetColumnOffsetX(columns, style, scroll, col);
        scroll.ScrollTo(offsetX, offsetY);
    }

    private void Redraw() => NeedsRedraw?.Invoke(this, EventArgs.Empty);

    private void EnsurePopupRedrawWired()
    {
        if (_popupRedrawWired) return;
        _popupManager.NeedsRedraw += Redraw;
        _popupManager.PopupClosed += OnPopupClosed;
        _popupRedrawWired = true;
    }

    private void OnPopupClosed(DrawnComponent popup)
    {
        if (popup != _activeFilterPopup) return;
        _activeFilterPopup = null;
        popup.IsFocused = false;
        FilterPopupClosed?.Invoke(this, EventArgs.Empty);
    }

    // ── Filter popup ──────────────────────────────────────────────

    private void ShowFilterPopup(
        KumikoColumn column, int columnIndex,
        ScrollState scroll, KumikoStyle style,
        KumikoSource dataSource)
    {
        // Close any existing popup
        EnsurePopupRedrawWired();
        CloseFilterPopup();

        var popup = new DrawnFilterPopup(column, dataSource);
        popup.ApplyTheme(style);

        // Below the column header, following it as the grid scrolls, and kept inside the viewport
        KumikoRect Anchor()
        {
            _popupManager.Viewport = new KumikoRect(0, 0, scroll.ViewportWidth, scroll.ViewportHeight);
            float x = HeaderScreenX(column, scroll, dataSource, style);
            float popupWidth = popup.Bounds.Width;
            if (x + popupWidth > scroll.ViewportWidth)
                x = scroll.ViewportWidth - popupWidth - 4;
            if (x < 0) x = 4;
            return new KumikoRect(x, style.HeaderHeight, column.Width, 0);
        }

        // Wire events
        popup.FilterApplied += (_, _) => { CloseFilterPopup(); Redraw(); };
        popup.FilterCancelled += (_, _) => { CloseFilterPopup(); Redraw(); };
        popup.FilterCleared += (_, _) => { CloseFilterPopup(); Redraw(); };
        popup.SortRequested += (_, dir) =>
        {
            // This column becomes the only sorted one
            dataSource.SetSort(column, dir);
            CloseFilterPopup();
            Redraw();
        };

        popup.RedrawRequested += (_, _) => Redraw();
        popup.KeyboardFocusRequested += (_, _) => KeyboardFocusRequested?.Invoke(this, EventArgs.Empty);
        WireSearchClipboard(popup);

        _popupManager.Show(popup, Anchor, PopupPlacement.Below);
        _activeFilterPopup = popup;

        // Set focus so the search box shows its focused visual (blue border)
        popup.IsFocused = true;

        // Request keyboard focus from the platform layer so that the search box
        // can receive text input (critical on mobile where the software keyboard
        // must be explicitly shown).
        KeyboardFocusRequested?.Invoke(this, EventArgs.Empty);

        // Notify platform layer to start cursor blink timer
        FilterPopupOpened?.Invoke(this, EventArgs.Empty);

        Redraw();
    }

    private void CloseFilterPopup()
    {
        if (_activeFilterPopup != null)
            _popupManager.Close(_activeFilterPopup); // OnPopupClosed clears it and raises FilterPopupClosed
    }

    /// <summary>
    /// The viewport x of a column's header, by its freeze pane. The row drag handle takes width at the start
    /// (<see cref="Models.DragHandlePosition.Left"/>) or the end of the viewport, and the columns sit beside it.
    /// </summary>
    /// <remarks>Allocation-free: it runs per anchor update while a filter popup is open.</remarks>
    internal static float HeaderScreenX(KumikoColumn column, ScrollState scroll, KumikoSource dataSource, KumikoStyle style)
    {
        var columns = dataSource.Columns;
        for (int i = 0; i < columns.Count; i++)
        {
            if (ReferenceEquals(columns[i], column))
            {
                float x = KumikoScroller.GetColumnX(columns, style, scroll, i);
                return float.IsNaN(x) ? 0 : x;
            }
        }
        return 0;
    }

    // ── Edit helpers ──────────────────────────────────────────────

    /// <summary>
    /// Commit an open edit before a gesture that changes the view (sort, filter, grouping, reorder), as
    /// desktop grids do. Returns false when the value is invalid: the editor stays open showing the error and
    /// the caller skips the gesture, so the input is not lost.
    /// </summary>
    private bool CommitEditBeforeViewChange(KumikoSource dataSource, KumikoSelectionModel selection)
    {
        if (_editSession == null || !_editSession.IsEditing) return true;

        bool committed = _editSession.CommitEdit(dataSource);
        selection.IsEditing = _editSession.IsEditing;
        Redraw();
        return committed;
    }

    /// <summary>
    /// The column's renderer when its cells are interactive without being edited
    /// (<see cref="IInteractiveCellRenderer"/>), unless the column has an editor of its own, which wins.
    /// </summary>
    private static IInteractiveCellRenderer? GetInteractiveRenderer(KumikoColumn? column)
        => column is { EditorFactory: null, CustomEditorFactory: null, EditorDescriptor: null } &&
           column.CustomCellRenderer is IInteractiveCellRenderer interactive
            ? interactive
            : null;

    private IInteractiveCellRenderer? InteractiveRendererAt(KumikoSource dataSource, CellPosition cell)
    {
        if (cell.Column < 0 || cell.Column >= dataSource.Columns.Count) return null;
        if (cell.Row < 0 || cell.Row >= dataSource.RowCount || dataSource.IsNonDataRow(cell.Row)) return null;
        return GetInteractiveRenderer(dataSource.Columns[cell.Column]);
    }

    /// <summary>The cell rectangle as the renderer drew it: the screen bounds inset by the cell padding.</summary>
    private KumikoRect GetInteractiveCellBounds(
        int row, int col, ScrollState scroll, KumikoStyle style, KumikoSource dataSource)
    {
        float groupPanelOffset = dataSource.IsGroupingActive ? style.GroupPanelHeight : 0;
        float topSummaryHeight = dataSource.TopSummaryCount * style.SummaryRowHeight;
        var cell = GetCellScreenBounds(row, col, scroll, style, dataSource.Columns, groupPanelOffset, topSummaryHeight, dataSource);
        float padding = dataSource.Columns[col].CellStyle?.Padding ?? style.CellPadding;
        return new KumikoRect(cell.X + padding, cell.Y, cell.Width - padding * 2, cell.Height);
    }

    /// <summary>
    /// Resolves the effective <see cref="EditTrigger"/> for <paramref name="column"/>.
    /// When the column has its own <see cref="KumikoColumn.EditTriggers"/> override it is
    /// returned directly; otherwise the grid-level <see cref="EditSession.EditTriggers"/> is
    /// used, falling back to <see cref="EditTrigger.Default"/> if the session is null.
    /// </summary>
    private EditTrigger GetEffectiveEditTriggers(KumikoColumn? column)
        => column?.EditTriggers ?? _editSession?.EditTriggers ?? EditTrigger.Default;

    /// <summary>
    /// Try to begin editing the current cell, after scrolling it into view. Returns whether an edit started.
    /// </summary>
    private bool TryBeginEdit(
        KumikoSelectionModel selection, KumikoSource dataSource,
        ScrollState scroll, KumikoStyle style, char? initialCharacter,
        KumikoPointerEventArgs? triggeringTap = null)
    {
        if (_editSession == null || !selection.CurrentCell.IsValid) return false;

        int row = selection.CurrentCell.Row;
        int col = selection.CurrentCell.Column;
        if (col < 0 || col >= dataSource.Columns.Count) return false;
        if (row < 0 || row >= dataSource.RowCount) return false;

        // Cannot edit group header or summary rows
        if (dataSource.IsNonDataRow(row)) return false;

        var column = dataSource.Columns[col];
        if (column.IsReadOnly || _editSession.IsReadOnly) return false;

        // An interactive cell (action buttons) is acted on, never edited
        if (GetInteractiveRenderer(column) != null) return false;

        // Bring the cell fully into view first, so the editor never opens partly hidden under the
        // header/frozen rows or straddling the pinned bottom summary footer (which is drawn below the
        // editor and would otherwise share its screen area).
        float offsetXBefore = scroll.OffsetX, offsetYBefore = scroll.OffsetY;
        EnsureCurrentCellVisible(scroll, selection, style, dataSource.Columns, dataSource);
        float scrolledX = scroll.OffsetX - offsetXBefore, scrolledY = scroll.OffsetY - offsetYBefore;
        if (triggeringTap != null && (scrolledX != 0 || scrolledY != 0))
        {
            // The cell moved under the finger: keep the forwarded tap on the same spot of the cell.
            triggeringTap = new KumikoPointerEventArgs
            {
                X = triggeringTap.X - scrolledX,
                Y = triggeringTap.Y - scrolledY,
                Action = triggeringTap.Action,
                Button = triggeringTap.Button,
                Modifiers = triggeringTap.Modifiers,
                ClickCount = triggeringTap.ClickCount,
                TimestampMs = triggeringTap.TimestampMs,
            };
        }

        // Calculate cell bounds on screen (accounting for group panel offset and top summaries)
        float groupPanelOffset = dataSource.IsGroupingActive ? style.GroupPanelHeight : 0;
        float topSummaryHeight = dataSource.TopSummaryCount * style.SummaryRowHeight;
        var cellBounds = GetCellScreenBounds(row, col, scroll, style, dataSource.Columns, groupPanelOffset, topSummaryHeight, dataSource);
        if (cellBounds.Width <= 0 || cellBounds.Height <= 0) return false;

        // Popups of the editor (dropdown, calendar) stay inside the grid's visible area
        _editSession.Viewport = new KumikoRect(0, 0, scroll.ViewportWidth, scroll.ViewportHeight);
        if (!_editSession.BeginEdit(row, col, column, dataSource, cellBounds, initialCharacter))
            return false;

        selection.IsEditing = true;

        // For editors that activate immediately (e.g. DrawnActionButtons), forward
        // the triggering tap so the button fires on the very first touch.
        if (triggeringTap != null)
        {
            _editSession.TryForwardInitialTap(triggeringTap);
            // Sync selection state in case the editor committed itself (e.g. button fired)
            selection.IsEditing = _editSession.IsEditing;
        }

        Redraw();
        return true;
    }

    /// <summary>
    /// Calculate the screen bounds for a cell (viewport coordinates).
    /// Handles left-frozen, right-frozen, and frozen rows.
    /// </summary>
    internal KumikoRect GetCellScreenBounds(
        int row, int col,
        ScrollState scroll, KumikoStyle style,
        IReadOnlyList<KumikoColumn> columns,
        float groupPanelOffset = 0,
        float topSummaryHeight = 0,
        KumikoSource? dataSource = null)
    {
        // Calculate Y position accounting for frozen rows
        int frozenRowCount = dataSource?.EffectiveFrozenRowCount ?? 0;
        float frozenRowHeight = frozenRowCount * style.RowHeight;
        float rowY;

        if (row < frozenRowCount)
        {
            // Frozen row: fixed position
            rowY = style.HeaderHeight + topSummaryHeight + row * style.RowHeight + groupPanelOffset;
        }
        else
        {
            // Scrollable row
            rowY = style.HeaderHeight + topSummaryHeight + frozenRowHeight
                + (row - frozenRowCount) * style.RowHeight - scroll.OffsetY + groupPanelOffset;
        }

        // X position by the column's pane, as drawn (#268: frozen panes that do not fit are unpinned)
        if (col >= 0 && col < columns.Count)
        {
            var targetCol = columns[col];
            float screenX = KumikoScroller.GetColumnX(columns, style, scroll, col);
            if (float.IsNaN(screenX)) screenX = 0; // hidden: not on screen

            return new KumikoRect(screenX, rowY, targetCol.Width, style.RowHeight);
        }

        return new KumikoRect(0, rowY, 0, style.RowHeight);
    }


    // ── Tab stops ──────────────────────────────────────────────────

    /// <summary>
    /// Whether <see cref="HandleKey"/> would handle Tab (Shift+Tab when <paramref name="backwards"/>) in the
    /// current state. False means the key would be left unhandled — there is no current cell, or no further
    /// tab stop in that direction — so a host that must claim Tab before it is processed (for example a
    /// UIKit key command) can let focus leave the grid instead. While an edit is open Tab is always processed:
    /// it commits first.
    /// </summary>
    public bool WouldHandleTab(KumikoSelectionModel selection, KumikoSource dataSource, bool backwards)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(dataSource);
        if (_editSession != null && _editSession.IsEditing) return true;
        return FindNextTabStop(selection.CurrentCell, dataSource, backwards ? -1 : 1).IsValid;
    }

    /// <summary>A Tab stop: a visible, editable column that allows tab stops, of any column type.</summary>
    private static bool IsTabStop(KumikoColumn column, bool gridReadOnly) =>
        !gridReadOnly && column.IsVisible && column.AllowTabStop && !column.IsReadOnly;

    /// <summary>
    /// The next tab stop after <paramref name="from"/> in reading order (direction +1) or before it (-1),
    /// skipping group header and summary rows; <see cref="CellPosition.Invalid"/> when there is none. Tab stop
    /// columns are the same in every row, so they are found once and the search walks rows, never
    /// rows × columns cells.
    /// </summary>
    private CellPosition FindNextTabStop(CellPosition from, KumikoSource dataSource, int direction)
    {
        if (!from.IsValid) return CellPosition.Invalid;

        // Tab stops are found in on-screen order (left-frozen, scrolling, right-frozen), as Left and Right move.
        var columns = dataSource.DisplayColumns;
        int fromDisplay = dataSource.ToDisplayIndex(from.Column);
        bool gridReadOnly = GridReadOnly;
        int firstStop = -1, lastStop = -1, nextInRow = -1;
        for (int d = 0; d < columns.Count; d++)
        {
            if (!IsTabStop(columns[d], gridReadOnly)) continue;
            int c = dataSource.ToColumnIndex(d);
            if (firstStop < 0) firstStop = c;
            lastStop = c;
            // A current cell on a hidden column has no display position: its Columns index stands in for it.
            bool after = fromDisplay >= 0 ? d > fromDisplay : c > from.Column;
            bool before = fromDisplay >= 0 ? d < fromDisplay : c < from.Column;
            if (direction > 0 ? after && nextInRow < 0 : before)
                nextInRow = c;
        }
        if (firstStop < 0) return CellPosition.Invalid;

        int totalRows = dataSource.RowCount;
        int row = from.Row;
        if (row >= 0 && row < totalRows && !dataSource.IsNonDataRow(row) && nextInRow >= 0)
            return new CellPosition(row, nextInRow);

        // Wrap to the first (last) tab stop of the next (previous) data row
        row += direction;
        while (row >= 0 && row < totalRows && dataSource.IsNonDataRow(row))
            row += direction;
        if (row < 0 || row >= totalRows) return CellPosition.Invalid;

        return new CellPosition(row, direction > 0 ? firstStop : lastStop);
    }

    /// <summary>
    /// Move the current cell to the next tab stop in <paramref name="direction"/> (+1 forward, -1 back), select
    /// its row and scroll it into view. Returns false, changing nothing, when there is no further tab stop.
    /// </summary>
    private bool MoveToNextTabStop(
        KumikoSelectionModel selection, KumikoSource dataSource,
        ScrollState scroll, KumikoStyle style, int direction)
    {
        var target = FindNextTabStop(selection.CurrentCell, dataSource, direction);
        if (!target.IsValid) return false;

        selection.CurrentCell = target;
        selection.HandleRowSelection(target.Row, false, false);
        EnsureCurrentCellVisible(scroll, selection, style, dataSource.Columns, dataSource);
        return true;
    }

    /// <summary>
    /// After Tab moves to a cell, open its editor unless the column's edit triggers are
    /// <see cref="EditTrigger.None"/> (editing disabled): Tab then only navigates.
    /// </summary>
    private void BeginEditAfterTab(
        KumikoSelectionModel selection, KumikoSource dataSource,
        ScrollState scroll, KumikoStyle style)
    {
        if (GetEffectiveEditTriggers(ColumnAt(dataSource, selection.CurrentCell.Column)) == EditTrigger.None)
            return;
        TryBeginEdit(selection, dataSource, scroll, style, null);
    }

    // ── Boolean cells ─────────────────────────────────────────────

    /// <summary>
    /// Whether <paramref name="cell"/> is an editable Boolean data cell that a tap or Space may toggle: there is
    /// an edit session, the column is a Boolean column that is not read-only and whose edit triggers are not
    /// <see cref="EditTrigger.None"/>.
    /// </summary>
    private bool CanToggleBooleanCell(CellPosition cell, KumikoSource dataSource)
    {
        if (_editSession == null || _editSession.IsReadOnly || !cell.IsValid) return false;
        if (cell.Row >= dataSource.RowCount || dataSource.IsNonDataRow(cell.Row)) return false;
        var column = ColumnAt(dataSource, cell.Column);
        return column is { ColumnType: KumikoColumnType.Boolean, IsReadOnly: false } &&
               GetEffectiveEditTriggers(column) != EditTrigger.None;
    }

    /// <summary>
    /// Toggles the current cell when it is an editable Boolean cell (see <see cref="CanToggleBooleanCell"/>) and
    /// redraws. Returns whether it did; the toggle itself may still be refused by validation.
    /// </summary>
    private bool TryToggleCurrentBooleanCell(KumikoSelectionModel selection, KumikoSource dataSource)
    {
        var current = selection.CurrentCell;
        if (!CanToggleBooleanCell(current, dataSource)) return false;
        _editSession!.ToggleBooleanCell(current.Row, current.Column, dataSource.Columns[current.Column], dataSource);
        Redraw();
        return true;
    }

    /// <summary>Whether viewport point (<paramref name="x"/>, <paramref name="y"/>) is on the check box of a Boolean cell.</summary>
    private bool IsOnCheckBox(float x, float y, CellPosition cell, ScrollState scroll, KumikoStyle style, KumikoSource dataSource)
    {
        float groupPanelOffset = dataSource.IsGroupingActive ? style.GroupPanelHeight : 0;
        float topSummaryHeight = dataSource.TopSummaryCount * style.SummaryRowHeight;
        var cellBounds = GetCellScreenBounds(cell.Row, cell.Column, scroll, style, dataSource.Columns,
            groupPanelOffset, topSummaryHeight, dataSource);
        return BooleanCellRenderer.GetCheckBoxHitBounds(cellBounds).Contains(x, y);
    }

    // ── Misc helpers ──────────────────────────────────────────────

    private static KumikoColumn? ColumnAt(KumikoSource dataSource, int column) =>
        column >= 0 && column < dataSource.Columns.Count ? dataSource.Columns[column] : null;

    /// <summary>
    /// Whether viewport y <paramref name="y"/> is in the rows area: below the grouping panel, header and top
    /// summaries, and above the pinned bottom summary footer.
    /// </summary>
    private static bool IsInRowsArea(float y, ScrollState scroll, KumikoStyle style, KumikoSource dataSource)
    {
        float top = (dataSource.IsGroupingActive ? style.GroupPanelHeight : 0) + style.HeaderHeight
            + dataSource.TopSummaryCount * style.SummaryRowHeight;
        float bottom = scroll.ViewportHeight - style.GetPinnedBottomSummaryHeight(dataSource.BottomSummaryCount);
        return y >= top && y < bottom;
    }
}

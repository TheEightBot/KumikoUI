using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Components;

namespace KumikoUI.Core.Input;

/// <summary>
/// Central input controller for the grid. Processes platform-independent
/// pointer and keyboard events, dispatching to selection, sorting,
/// column resize/reorder, and scrolling subsystems.
/// </summary>
public class KumikoInputController
{
    private readonly KumikoHitTester _hitTester = new();
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

    private const float PanThreshold = 10f;
    private const float DragThreshold = 8f;

    // Tracks the original press position for more accurate first-move
    private float _pressOriginX, _pressOriginY;

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
        set => _editSession = value;
    }

    // ── Events ──
    /// <summary>Raised when the grid surface needs to be redrawn.</summary>
    public event Action? NeedsRedraw;
    /// <summary>Raised when the mouse cursor style should change. <c>true</c> indicates a resize cursor; <c>false</c> restores the default cursor.</summary>
    public event Action<bool>? CursorChanged;  // true = resize cursor, false = default
    /// <summary>Raised when the user finishes resizing a column by dragging its header edge.</summary>
    public event EventHandler<ColumnResizedEventArgs>? ColumnResized;
    /// <summary>Raised when the user drops a dragged column header at a new position.</summary>
    public event EventHandler<KumikoColumnReorderedEventArgs>? ColumnReordered;
    /// <summary>Raised when the user drops a dragged row at a new position.</summary>
    public event EventHandler<RowReorderedEventArgs>? RowReordered;
    /// <summary>Raised when a data cell is tapped (after the selection is updated).</summary>
    public event EventHandler<RowTappedEventArgs>? RowTapped;
    /// <summary>Raised on the second tap of a double-tap on a data cell.</summary>
    public event EventHandler<RowTappedEventArgs>? RowDoubleTapped;

    /// <summary>Gets the popup manager for drawing overlays.</summary>
    public PopupManager PopupManager => _popupManager;

    /// <summary>Raised when a popup or component needs keyboard focus from the platform layer (e.g., to show the software keyboard).</summary>
    public event Action? KeyboardFocusRequested;

    /// <summary>Raised when a filter popup is opened (used to start cursor blink timer).</summary>
    public event Action? FilterPopupOpened;

    /// <summary>
    /// Raised when a filter popup closes, however it closes: OK, Cancel, Clear, a sort, Escape, or a press
    /// outside it (used to stop the cursor blink timer).
    /// </summary>
    public event Action? FilterPopupClosed;

    /// <summary>
    /// The text-input surface that has the keyboard, for a host's native text-input adapter: the open
    /// filter popup's search box, else the active editor's, else null.
    /// </summary>
    public ITextInputSession? ActiveTextInput =>
        _activeFilterPopup?.SearchTextInput ?? _editSession?.ActiveTextInput;

    /// <summary>Raised when a column edge is double-clicked, requesting auto-fit width.</summary>
    public event EventHandler<AutoFitColumnEventArgs>? AutoFitColumnRequested;

    /// <summary>Is the inertial scroller still animating?</summary>
    public bool IsInertialScrolling => _inertialScroller.IsActive;

    /// <summary>Column being dragged for reorder (-1 if none).</summary>
    public int DragColumnIndex => _dragColumnIndex;

    /// <summary>Current drag X offset for visual feedback.</summary>
    public float DragColumnScreenX { get; private set; }

    /// <summary>Row being dragged for reorder (-1 if none).</summary>
    public int DragRowIndex => _dragRowIndex;

    /// <summary>Current drag Y offset for visual feedback.</summary>
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

        // ── Popup priority: popups always get first crack at input ──
        if (_popupManager.HandlePointer(e))
            return;

        var columns = dataSource.Columns;
        int totalRows = dataSource.RowCount;

        switch (e.Action)
        {
            case InputAction.Pressed:
                HandlePointerDown(e, scroll, selection, style, columns, totalRows, dataSource);
                break;
            case InputAction.Moved:
                HandlePointerMove(e, scroll, selection, style, columns, totalRows, dataSource);
                break;
            case InputAction.Released:
                HandlePointerUp(e, scroll, selection, style, columns, totalRows, dataSource);
                break;
            case InputAction.Scroll:
                HandleScroll(e, scroll, style);
                break;
            case InputAction.Cancelled:
                HandlePointerCancelled(e, scroll);
                break;
            case InputAction.LongPress:
                HandleLongPress(e, scroll, selection, style, columns, totalRows, dataSource);
                break;
        }
    }

    /// <summary>
    /// Handle a keyboard event.
    /// </summary>
    public void HandleKey(
        KumikoKeyEventArgs e,
        ScrollState scroll, KumikoSelectionModel selection, KumikoStyle style,
        KumikoSource dataSource)
    {
        if (!e.IsKeyDown) return;

        // ── Popup priority: popups always get first crack at keyboard ──
        if (_popupManager.HandleKey(e))
            return;

        int totalRows = dataSource.RowCount;
        int totalCols = dataSource.Columns.Count;
        // One page = the number of scrollable rows that fit between the fixed bands (header, grouping
        // panel, top summaries, frozen rows, pinned footer), so PageUp/PageDown never skip unseen rows.
        int pageSize = Math.Max(1, (int)(GetScrollableRowsViewportHeight(scroll, style, dataSource) / style.RowHeight));

        // ── Editing-mode key handling ──
        if (_editSession != null && _editSession.IsEditing)
        {
            // While an input method is composing, Enter and Escape finish or abandon the composition
            // (in the editor) rather than commit or cancel the edit.
            if (e.Key is KumikoKey.Enter or KumikoKey.Escape &&
                _editSession.ActiveTextInput?.Composition != null &&
                _editSession.HandleKeyEvent(e))
            {
                e.Handled = true;
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

                default:
                    // Forward to editor
                    if (_editSession.HandleKeyEvent(e))
                    {
                        e.Handled = true;
                        return;
                    }

                    // A navigation key the editor leaves alone moves the selection, which ends the edit: a
                    // refused commit keeps the editor open and the selection where it is.
                    if (e.Key is KumikoKey.Up or KumikoKey.Down or KumikoKey.Left or KumikoKey.Right or
                        KumikoKey.Home or KumikoKey.End or KumikoKey.PageUp or KumikoKey.PageDown &&
                        !CommitEditBeforeViewChange(dataSource, selection))
                    {
                        e.Handled = true;
                        return;
                    }
                    break;
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
    /// Call this from a frame timer.
    /// </summary>
    public bool UpdateInertialScroll(ScrollState scroll, float frameIntervalMs = 16f)
    {
        if (!_inertialScroller.IsActive) return false;

        var (dx, dy) = _inertialScroller.Update(frameIntervalMs);
        if (dx == 0 && dy == 0) return false;

        scroll.ScrollBy(dx, dy);
        Redraw();
        return _inertialScroller.IsActive;
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
        _editorConsumedPointerDown = false;
        _lastPanX = e.X;
        _lastPanY = e.Y;
        _pressOriginX = e.X;
        _pressOriginY = e.Y;

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

        var hit = _hitTester.HitTest(e.X, e.Y, scroll, style, columns, totalRows, dataSource);

        // Group panel chip remove → remove grouping
        if (hit.Region == HitRegion.GroupPanelChipRemove && hit.GroupDescriptionIndex >= 0)
        {
        if (!CommitEditBeforeViewChange(dataSource, selection)) { e.Handled = true; return; }
            var groups = dataSource.GroupDescriptions;
            if (hit.GroupDescriptionIndex < groups.Count)
            {
                dataSource.RemoveGroupDescription(groups[hit.GroupDescriptionIndex].PropertyName);
                Redraw();
            }
            e.Handled = true;
            return;
        }

        // Group panel chip click — toggle sort direction for this group
        if (hit.Region == HitRegion.GroupPanelChip && hit.GroupDescriptionIndex >= 0)
        {
        if (!CommitEditBeforeViewChange(dataSource, selection)) { e.Handled = true; return; }
            var groups = dataSource.GroupDescriptions;
            if (hit.GroupDescriptionIndex < groups.Count)
            {
                var group = groups[hit.GroupDescriptionIndex];
                group.GroupSortDirection = group.GroupSortDirection == KumikoSortDirection.Ascending
                    ? KumikoSortDirection.Descending
                    : KumikoSortDirection.Ascending;
                dataSource.Refresh();
                Redraw();
            }
            e.Handled = true;
            return;
        }

        // Group chevron → toggle expand/collapse
        if (hit.Region == HitRegion.GroupChevron)
        {
        if (!CommitEditBeforeViewChange(dataSource, selection)) { e.Handled = true; return; }
            dataSource.ToggleGroupExpansion(hit.RowIndex);
            Redraw();
            e.Handled = true;
            return;
        }

        // Group header row click → toggle expand/collapse (whole row acts as toggle)
        if (hit.Region == HitRegion.GroupHeaderRow)
        {
        if (!CommitEditBeforeViewChange(dataSource, selection)) { e.Handled = true; return; }
            dataSource.ToggleGroupExpansion(hit.RowIndex);
            Redraw();
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
                CursorChanged?.Invoke(false);
                e.Handled = true;
                return;
            }

            _isResizingColumn = true;
            _resizeColumnIndex = hit.ColumnIndex;
            _resizeStartX = e.X;
            _resizeStartWidth = hit.Column.Width;
            CursorChanged?.Invoke(true);
            e.Handled = true;
            return;
        }

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
            DragRowScreenY = e.Y - _dragRowOffset;
            Redraw();
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
                var startHit = _hitTester.HitTest(_lastPanX, _lastPanY, scroll, style, columns, totalRows, dataSource);
                if (startHit.Region == HitRegion.HeaderCell && startHit.Column?.AllowReorder == true &&
                    Math.Abs(e.X - _lastPanX) > Math.Abs(e.Y - _lastPanY) &&
                    CommitEditBeforeViewChange(dataSource, selection))
                {
                    _isDraggingColumn = true;
                    _dragColumnIndex = startHit.ColumnIndex;
                    _dragColumnOffset = _lastPanX - (startHit.ContentX - scroll.OffsetX);
                    DragColumnScreenX = e.X - _dragColumnOffset;
                    e.Handled = true;
                    return;
                }

                // Check if this started on a data cell (potential row reorder) or a drag handle
                bool isDragHandle = startHit.Region == HitRegion.RowDragHandle && style.ShowRowDragHandle;
                bool isCellDrag = startHit.Region == HitRegion.Cell && style.AllowRowDragDrop;
                if ((isDragHandle || isCellDrag) &&
                    Math.Abs(e.Y - _lastPanY) > Math.Abs(e.X - _lastPanX) &&
                    startHit.RowIndex >= 0 &&
                    CommitEditBeforeViewChange(dataSource, selection))
                {
                    _isDraggingRow = true;
                    _dragRowIndex = startHit.RowIndex;
                    // Compute the Y position of the row top in viewport coords
                    float topSummaryHeight = dataSource.TopSummaryCount * style.SummaryRowHeight;
                    float dataAreaTop = style.HeaderHeight + topSummaryHeight;
                    int frozenRowCount = dataSource.EffectiveFrozenRowCount;
                    float rowScreenY;
                    if (startHit.RowIndex < frozenRowCount)
                    {
                        rowScreenY = dataAreaTop + startHit.RowIndex * style.RowHeight;
                    }
                    else
                    {
                        rowScreenY = dataAreaTop + (startHit.RowIndex * style.RowHeight) - scroll.OffsetY;
                    }
                    _dragRowOffset = _lastPanY - rowScreenY;
                    DragRowScreenY = e.Y - _dragRowOffset;
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
        else
        {
            // Hover: update cursor for resize grips
            var hit = _hitTester.HitTest(e.X, e.Y, scroll, style, columns, totalRows);
            CursorChanged?.Invoke(hit.Region == HitRegion.HeaderResizeGrip);
        }
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
            CursorChanged?.Invoke(false);

            if (_resizeColumnIndex >= 0 && _resizeColumnIndex < columns.Count)
            {
                ColumnResized?.Invoke(this, new ColumnResizedEventArgs(
                    _resizeColumnIndex, columns[_resizeColumnIndex]));
            }
            e.Handled = true;
            return;
        }

        if (_isDraggingColumn)
        {
            FinishColumnReorder(e.X, scroll, columns, style);
            _isDraggingColumn = false;
            _dragColumnIndex = -1;
            Redraw();
            e.Handled = true;
            return;
        }

        if (_isDraggingRow)
        {
            FinishRowReorder(e.Y, scroll, style, dataSource);
            _isDraggingRow = false;
            _dragRowIndex = -1;
            Redraw();
            e.Handled = true;
            return;
        }

        if (_isPanning)
        {
            _isPanning = false;
            _inertialScroller.StartFling();
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
        var hit = _hitTester.HitTest(e.X, e.Y, scroll, style, columns, totalRows, dataSource);

        // Group interactions on tap-up (already handled in HandlePointerDown too,
        // but we check here in case the implementation evolves)
        if (hit.Region == HitRegion.GroupChevron || hit.Region == HitRegion.GroupHeaderRow ||
            hit.Region == HitRegion.GroupPanelChip || hit.Region == HitRegion.GroupPanelChipRemove ||
            hit.Region == HitRegion.GroupSummaryRow || hit.Region == HitRegion.TableSummaryRow ||
            hit.Region == HitRegion.RowDragHandle)
        {
            e.Handled = true;
            return;
        }

        if (hit.Region == HitRegion.HeaderFilterIcon && hit.Column != null)
        {
            if (hit.Column.AllowFiltering && CommitEditBeforeViewChange(dataSource, selection))
            {
                ShowFilterPopup(hit.Column, hit.ColumnIndex, scroll, style, dataSource);
            }
            e.Handled = true;
            return;
        }

        if (hit.Region == HitRegion.HeaderCell && hit.Column != null)
        {
        if (!CommitEditBeforeViewChange(dataSource, selection)) { e.Handled = true; return; }
            bool ctrl = (e.Modifiers & InputModifiers.Control) != 0;
            bool shift = (e.Modifiers & InputModifiers.Shift) != 0;

            if (ctrl || shift)
                dataSource.ToggleMultiSort(hit.Column);
            else
                dataSource.ToggleSort(hit.Column);

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

            RowTapped?.Invoke(this, new RowTappedEventArgs(
                hit.RowIndex, hit.ColumnIndex, dataSource.GetItem(hit.RowIndex)));

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
    private void HandlePointerCancelled(KumikoPointerEventArgs e, ScrollState scroll)
    {
        _editorConsumedPointerDown = false;

        if (_isResizingColumn)
        {
            _isResizingColumn = false;
            CursorChanged?.Invoke(false);
            e.Handled = true;
            return;
        }

        if (_isDraggingColumn)
        {
            // Cancel column drag — snap back
            _isDraggingColumn = false;
            _dragColumnIndex = -1;
            Redraw();
            e.Handled = true;
            return;
        }

        if (_isDraggingRow)
        {
            // Cancel row drag — snap back
            _isDraggingRow = false;
            _dragRowIndex = -1;
            Redraw();
            e.Handled = true;
            return;
        }

        if (_isPanning)
        {
            // Touch left bounds — start inertial scroll with current velocity
            _isPanning = false;
            _inertialScroller.StartFling();
            e.Handled = true;
            return;
        }
    }

    private void HandleScroll(KumikoPointerEventArgs e, ScrollState scroll, KumikoStyle style)
    {
        _inertialScroller.Reset();
        float scrollAmount = style.RowHeight * _inertialScroller.Settings.WheelScrollMultiplier;
        scroll.ScrollBy(-e.ScrollDeltaX * scrollAmount, -e.ScrollDeltaY * scrollAmount);
        Redraw();
        e.Handled = true;
    }

    /// <summary>
    /// Handle a long press gesture. Triggers editing if the LongPress edit trigger is configured.
    /// </summary>
    private void HandleLongPress(
        KumikoPointerEventArgs e,
        ScrollState scroll, KumikoSelectionModel selection, KumikoStyle style,
        IReadOnlyList<KumikoColumn> columns, int totalRows,
        KumikoSource dataSource)
    {
        var hit = _hitTester.HitTest(e.X, e.Y, scroll, style, columns, totalRows, dataSource);
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
        float newWidth = Math.Clamp(_resizeStartWidth + delta, col.MinWidth, col.MaxWidth);
        col.Width = newWidth;
        Redraw();
    }

    // ── Column reorder ──

    private void FinishColumnReorder(
        float dropX, ScrollState scroll,
        IReadOnlyList<KumikoColumn> columns, KumikoStyle style)
    {
        if (_dragColumnIndex < 0) return;

        // Find drop target column
        float contentDropX = dropX + scroll.OffsetX;
        float x = 0;
        int dropIndex = columns.Count - 1;
        for (int i = 0; i < columns.Count; i++)
        {
            if (!columns[i].IsVisible) continue;
            float midX = x + columns[i].Width / 2;
            if (contentDropX < midX)
            {
                dropIndex = i;
                break;
            }
            x += columns[i].Width;
        }

        if (dropIndex != _dragColumnIndex)
        {
            // The columns list is from KumikoSource which uses List<KumikoColumn>
            // We need to reorder via the source
            ColumnReordered?.Invoke(this, new KumikoColumnReorderedEventArgs(
                _dragColumnIndex, dropIndex, columns[_dragColumnIndex]));
        }
    }

    // ── Row reorder ──

    private void FinishRowReorder(
        float dropY, ScrollState scroll,
        KumikoStyle style, KumikoSource dataSource)
    {
        if (_dragRowIndex < 0) return;

        int totalRows = dataSource.RowCount;
        float topSummaryHeight = dataSource.TopSummaryCount * style.SummaryRowHeight;
        int frozenRowCount = dataSource.EffectiveFrozenRowCount;
        float frozenRowHeight = frozenRowCount * style.RowHeight;

        // Account for grouping panel offset
        float groupPanelOffset = dataSource.IsGroupingActive ? style.GroupPanelHeight : 0;

        // Convert drop Y to row index
        float dataAreaTop = style.HeaderHeight + topSummaryHeight + groupPanelOffset;
        int dropIndex;

        if (frozenRowCount > 0 && dropY >= dataAreaTop && dropY < dataAreaTop + frozenRowHeight)
        {
            // Dropping in the frozen row area
            dropIndex = (int)((dropY - dataAreaTop) / style.RowHeight);
            dropIndex = Math.Clamp(dropIndex, 0, frozenRowCount - 1);
        }
        else
        {
            // Dropping in the scrollable row area
            float scrollableDataTop = dataAreaTop + frozenRowHeight;
            dropIndex = (int)((dropY - scrollableDataTop + scroll.OffsetY) / style.RowHeight) + frozenRowCount;
        }

        dropIndex = Math.Clamp(dropIndex, 0, totalRows - 1);

        // Don't reorder onto group header or group summary rows
        if (dataSource.IsGroupHeaderRow(dropIndex) || dataSource.IsGroupSummaryRow(dropIndex))
            return;

        // Don't reorder the dragged row onto itself
        if (dropIndex != _dragRowIndex)
        {
            object? dataItem = null;
            try { dataItem = dataSource.GetItem(_dragRowIndex); }
            catch { /* group header — shouldn't happen but guard */ }

            RowReordered?.Invoke(this, new RowReorderedEventArgs(
                _dragRowIndex, dropIndex, dataItem));
        }
    }

    // ── Scroll helpers ──

    /// <summary>
    /// Height available to scrollable rows: the viewport minus the fixed bands drawn over or above
    /// them — grouping panel, header, top summaries, frozen rows and the pinned bottom summary footer.
    /// </summary>
    private static float GetScrollableRowsViewportHeight(ScrollState scroll, KumikoStyle style, KumikoSource? dataSource)
    {
        float groupPanelHeight = dataSource != null && dataSource.IsGroupingActive ? style.GroupPanelHeight : 0;
        float topSummaryHeight = dataSource != null ? dataSource.TopSummaryCount * style.SummaryRowHeight : 0;
        float frozenRowHeight = (dataSource?.EffectiveFrozenRowCount ?? 0) * style.RowHeight;
        float pinnedFooterHeight = style.GetPinnedBottomSummaryHeight(dataSource?.BottomSummaryCount ?? 0);
        return scroll.ViewportHeight - groupPanelHeight - style.HeaderHeight - topSummaryHeight
            - frozenRowHeight - pinnedFooterHeight;
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

        int frozenRowCount = dataSource?.EffectiveFrozenRowCount ?? 0;

        // If the row is a frozen row, no vertical scroll needed for it
        if (row >= frozenRowCount)
        {
            // Scrollable row: ensure visible between the frozen rows and the pinned footer
            float scrollableRowTop = (row - frozenRowCount) * style.RowHeight;
            float scrollableRowBottom = scrollableRowTop + style.RowHeight;

            float visibleHeight = Math.Max(style.RowHeight, GetScrollableRowsViewportHeight(scroll, style, dataSource));

            if (scrollableRowTop < scroll.OffsetY)
                scroll.OffsetY = scrollableRowTop;
            else if (scrollableRowBottom > scroll.OffsetY + visibleHeight)
                scroll.OffsetY = scrollableRowBottom - visibleHeight;
        }

        // If the column is frozen (left or right), no horizontal scroll needed
        if (col >= 0 && col < columns.Count && (columns[col].IsFrozen || columns[col].IsFrozenRight))
        {
            scroll.ClampOffset();
            return;
        }

        // Calculate frozen widths
        float frozenWidth = 0;
        float rightFrozenWidth = 0;
        foreach (var c in columns)
        {
            if (!c.IsVisible) continue;
            if (c.IsFrozen) frozenWidth += c.Width;
            if (c.IsFrozenRight) rightFrozenWidth += c.Width;
        }

        // Ensure scrollable column is visible (accounting for frozen areas on both sides)
        float colLeft = frozenWidth;
        for (int i = 0; i < columns.Count; i++)
        {
            if (!columns[i].IsVisible || columns[i].IsFrozen || columns[i].IsFrozenRight) continue;
            if (i == col) break;
            colLeft += columns[i].Width;
        }

        float colRight = colLeft;
        if (col >= 0 && col < columns.Count)
            colRight += columns[col].Width;

        // The row drag handle (left or right) takes viewport width from the columns as well
        float handleWidth = style.ShowRowDragHandle ? style.RowDragHandleWidth : 0;

        // Scrollable area: starts at frozenWidth, ends at viewportWidth - drag handle - rightFrozenWidth
        float scrollableWidth = scroll.ViewportWidth - handleWidth - rightFrozenWidth;
        float scrollableLeft = scroll.OffsetX + frozenWidth;
        float scrollableRight = scroll.OffsetX + scrollableWidth;

        if (colLeft < scrollableLeft)
            scroll.OffsetX = colLeft - frozenWidth;
        else if (colRight > scrollableRight)
            scroll.OffsetX = colRight - scrollableWidth;

        scroll.ClampOffset();
    }

    private void Redraw() => NeedsRedraw?.Invoke();

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
        FilterPopupClosed?.Invoke();
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
            float x = HeaderScreenX(column, scroll, dataSource);
            float popupWidth = popup.Bounds.Width;
            if (x + popupWidth > scroll.ViewportWidth)
                x = scroll.ViewportWidth - popupWidth - 4;
            if (x < 0) x = 4;
            return new KumikoRect(x, style.HeaderHeight, column.Width, 0);
        }

        // Wire events
        popup.FilterApplied += () => { CloseFilterPopup(); Redraw(); };
        popup.FilterCancelled += () => { CloseFilterPopup(); Redraw(); };
        popup.FilterCleared += () => { CloseFilterPopup(); Redraw(); };
        popup.SortRequested += dir =>
        {
            // This column becomes the only sorted one
            dataSource.SetSort(column, dir);
            CloseFilterPopup();
            Redraw();
        };

        popup.RedrawRequested += Redraw;
        popup.KeyboardFocusRequested += () => KeyboardFocusRequested?.Invoke();

        _popupManager.Show(popup, Anchor, PopupPlacement.Below);
        _activeFilterPopup = popup;

        // Set focus so the search box shows its focused visual (blue border)
        popup.IsFocused = true;

        // Request keyboard focus from the platform layer so that the search box
        // can receive text input (critical on mobile where the software keyboard
        // must be explicitly shown).
        KeyboardFocusRequested?.Invoke();

        // Notify platform layer to start cursor blink timer
        FilterPopupOpened?.Invoke();

        Redraw();
    }

    private void CloseFilterPopup()
    {
        if (_activeFilterPopup != null)
            _popupManager.Close(_activeFilterPopup); // OnPopupClosed clears it and raises FilterPopupClosed
    }

    /// <summary>The viewport x of a column's header, by its freeze pane.</summary>
    private static float HeaderScreenX(KumikoColumn column, ScrollState scroll, KumikoSource dataSource)
    {
        if (column.IsFrozen)
        {
            float x = 0;
            foreach (var c in dataSource.Columns)
            {
                if (c == column) break;
                if (c.IsVisible && c.IsFrozen) x += c.Width;
            }
            return x;
        }

        if (column.IsFrozenRight)
        {
            var rightFrozen = dataSource.Columns.Where(c => c.IsVisible && c.IsFrozenRight).ToList();
            float x = scroll.ViewportWidth - rightFrozen.Sum(c => c.Width);
            return x + rightFrozen.TakeWhile(c => c != column).Sum(c => c.Width);
        }

        float frozenWidth = dataSource.Columns.Where(c => c.IsVisible && c.IsFrozen).Sum(c => c.Width);
        float scrollableBefore = dataSource.Columns
            .Where(c => c.IsVisible && !c.IsFrozen && !c.IsFrozenRight)
            .TakeWhile(c => c != column)
            .Sum(c => c.Width);
        return frozenWidth + scrollableBefore - scroll.OffsetX;
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
        if (column.IsReadOnly) return false;

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
    public KumikoRect GetCellScreenBounds(
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

        // Compute drag handle offsets
        float handleOffset = 0, rightHandleOffset = 0;
        if (style.ShowRowDragHandle)
        {
            if (style.RowDragHandlePosition == Models.DragHandlePosition.Left)
                handleOffset = style.RowDragHandleWidth;
            else
                rightHandleOffset = style.RowDragHandleWidth;
        }

        // Calculate X position based on column freeze mode
        if (col >= 0 && col < columns.Count)
        {
            var targetCol = columns[col];
            float screenX;

            if (targetCol.IsFrozen)
            {
                // Left-frozen: sum widths of preceding left-frozen columns
                float x = handleOffset;
                for (int i = 0; i < col; i++)
                {
                    if (columns[i].IsVisible && columns[i].IsFrozen)
                        x += columns[i].Width;
                }
                screenX = x;
            }
            else if (targetCol.IsFrozenRight)
            {
                // Right-frozen: compute position from right side
                float rightFrozenWidth = 0;
                foreach (var c in columns)
                    if (c.IsVisible && c.IsFrozenRight) rightFrozenWidth += c.Width;

                float rightFrozenLeft = scroll.ViewportWidth - rightFrozenWidth - rightHandleOffset;
                float x = rightFrozenLeft;
                for (int i = 0; i < columns.Count; i++)
                {
                    if (!columns[i].IsVisible || !columns[i].IsFrozenRight) continue;
                    if (i == col) break;
                    x += columns[i].Width;
                }
                screenX = x;
            }
            else
            {
                // Scrollable: content X minus scroll offset
                float frozenWidth = 0;
                foreach (var c in columns)
                    if (c.IsVisible && c.IsFrozen) frozenWidth += c.Width;

                float colX = frozenWidth + handleOffset;
                for (int i = 0; i < columns.Count; i++)
                {
                    if (!columns[i].IsVisible || columns[i].IsFrozen || columns[i].IsFrozenRight) continue;
                    if (i == col) break;
                    colX += columns[i].Width;
                }
                screenX = colX - scroll.OffsetX;
            }

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
    private static bool IsTabStop(KumikoColumn column) =>
        column.IsVisible && column.AllowTabStop && !column.IsReadOnly;

    /// <summary>
    /// The next tab stop after <paramref name="from"/> in reading order (direction +1) or before it (-1),
    /// skipping group header and summary rows; <see cref="CellPosition.Invalid"/> when there is none. Tab stop
    /// columns are the same in every row, so they are found once and the search walks rows, never
    /// rows × columns cells.
    /// </summary>
    private static CellPosition FindNextTabStop(CellPosition from, KumikoSource dataSource, int direction)
    {
        if (!from.IsValid) return CellPosition.Invalid;

        var columns = dataSource.Columns;
        int firstStop = -1, lastStop = -1, nextInRow = -1;
        for (int c = 0; c < columns.Count; c++)
        {
            if (!IsTabStop(columns[c])) continue;
            if (firstStop < 0) firstStop = c;
            lastStop = c;
            if (direction > 0 ? c > from.Column && nextInRow < 0 : c < from.Column)
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
        if (_editSession == null || !cell.IsValid) return false;
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

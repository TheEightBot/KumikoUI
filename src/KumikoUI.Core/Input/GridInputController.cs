using KumikoUI.Core.Models;
using KumikoUI.Core.Layout;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Components;

namespace KumikoUI.Core.Input;

/// <summary>
/// Central input controller for the grid. Processes platform-independent
/// pointer and keyboard events, dispatching to selection, sorting,
/// column resize/reorder, and scrolling subsystems.
/// </summary>
public class GridInputController
{
    private readonly GridHitTester _hitTester = new();
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
    public event EventHandler<ColumnResizedEventArgs>? ColumnResized;
    public event EventHandler<ColumnReorderedEventArgs>? ColumnReordered;
    public event EventHandler<RowReorderedEventArgs>? RowReordered;
    public event EventHandler<RowTappedEventArgs2>? RowTapped;
    public event EventHandler<RowTappedEventArgs2>? RowDoubleTapped;

    /// <summary>Gets the popup manager for drawing overlays.</summary>
    public PopupManager PopupManager => _popupManager;

    /// <summary>Raised when a popup or component needs keyboard focus from the platform layer (e.g., to show the software keyboard).</summary>
    public event Action? KeyboardFocusRequested;

    /// <summary>Raised when a filter popup is opened (used to start cursor blink timer).</summary>
    public event Action? FilterPopupOpened;

    /// <summary>Raised when a filter popup is closed (used to stop cursor blink timer).</summary>
    public event Action? FilterPopupClosed;

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
        GridPointerEventArgs e,
        ScrollState scroll, SelectionModel selection, DataGridStyle style,
        DataGridSource dataSource)
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
        GridKeyEventArgs e,
        ScrollState scroll, SelectionModel selection, DataGridStyle style,
        DataGridSource dataSource)
    {
        if (!e.IsKeyDown) return;

        // ── Popup priority: popups always get first crack at keyboard ──
        if (_popupManager.HandleKey(e))
            return;

        int totalRows = dataSource.RowCount;
        int totalCols = dataSource.Columns.Count;
        int pageSize = Math.Max(1, (int)(scroll.ViewportHeight / style.RowHeight) - 1);

        // ── Editing-mode key handling ──
        if (_editSession != null && _editSession.IsEditing)
        {
            switch (e.Key)
            {
                case GridKey.Escape:
                    _editSession.CancelEdit();
                    selection.IsEditing = false;
                    Redraw();
                    e.Handled = true;
                    return;

                case GridKey.Enter:
                    if (_editSession.CommitEdit(dataSource))
                    {
                        selection.IsEditing = false;
                        // Move down after commit
                        selection.NavigateDown(totalRows, false);
                        EnsureCurrentCellVisible(scroll, selection, style, dataSource.Columns);

                        if (!_editSession.DismissKeyboardOnEnter)
                        {
                            // Keep editing: automatically begin editing the new cell
                            TryBeginEdit(selection, dataSource, scroll, style, null);
                        }

                        Redraw();
                    }
                    e.Handled = true;
                    return;

                case GridKey.Tab:
                    if (_editSession.CommitEdit(dataSource))
                    {
                        selection.IsEditing = false;
                        // Tab: move right, Shift+Tab: move left
                        if (e.HasShift)
                            NavigateToNextEditableCell(selection, dataSource, scroll, style, -1);
                        else
                            NavigateToNextEditableCell(selection, dataSource, scroll, style, 1);

                        // Automatically begin editing the next cell for seamless tabbing
                        TryBeginEdit(selection, dataSource, scroll, style, null);

                        Redraw();
                    }
                    e.Handled = true;
                    return;

                default:
                    // Forward to editor
                    if (_editSession.HandleKeyEvent(e))
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
                    case GridKey.Right:
                        // Expand group
                        if (!groupInfo.IsExpanded)
                        {
                            dataSource.ToggleGroupExpansion(groupRow);
                            Redraw();
                        }
                        e.Handled = true;
                        return;
                    case GridKey.Left:
                        // Collapse group
                        if (groupInfo.IsExpanded)
                        {
                            dataSource.ToggleGroupExpansion(groupRow);
                            Redraw();
                        }
                        e.Handled = true;
                        return;
                    case GridKey.Space:
                    case GridKey.Enter:
                        // Toggle expand/collapse
                        dataSource.ToggleGroupExpansion(groupRow);
                        Redraw();
                        e.Handled = true;
                        return;
                    case GridKey.Up:
                        selection.NavigateUp(totalRows, e.HasShift);
                        EnsureCurrentCellVisible(scroll, selection, style, dataSource.Columns);
                        Redraw();
                        e.Handled = true;
                        return;
                    case GridKey.Down:
                        selection.NavigateDown(totalRows, e.HasShift);
                        EnsureCurrentCellVisible(scroll, selection, style, dataSource.Columns);
                        Redraw();
                        e.Handled = true;
                        return;
                }
            }
        }

        switch (e.Key)
        {
            case GridKey.F2:
                // F2 → begin editing the current cell (honours column- and grid-level F2Key flag)
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
            case GridKey.Up:
                selection.NavigateUp(totalRows, e.HasShift);
                EnsureCurrentCellVisible(scroll, selection, style, dataSource.Columns);
                Redraw();
                e.Handled = true;
                break;
            case GridKey.Down:
                selection.NavigateDown(totalRows, e.HasShift);
                EnsureCurrentCellVisible(scroll, selection, style, dataSource.Columns);
                Redraw();
                e.Handled = true;
                break;
            case GridKey.Left:
                selection.NavigateLeft(totalCols, e.HasShift);
                EnsureCurrentCellVisible(scroll, selection, style, dataSource.Columns);
                Redraw();
                e.Handled = true;
                break;
            case GridKey.Right:
                selection.NavigateRight(totalCols, e.HasShift);
                EnsureCurrentCellVisible(scroll, selection, style, dataSource.Columns);
                Redraw();
                e.Handled = true;
                break;
            case GridKey.Home:
                selection.NavigateHome(e.HasControl, totalRows, e.HasShift);
                EnsureCurrentCellVisible(scroll, selection, style, dataSource.Columns);
                Redraw();
                e.Handled = true;
                break;
            case GridKey.End:
                selection.NavigateEnd(e.HasControl, totalRows, totalCols, e.HasShift);
                EnsureCurrentCellVisible(scroll, selection, style, dataSource.Columns);
                Redraw();
                e.Handled = true;
                break;
            case GridKey.PageUp:
                selection.NavigatePageUp(pageSize, e.HasShift, totalRows);
                EnsureCurrentCellVisible(scroll, selection, style, dataSource.Columns);
                Redraw();
                e.Handled = true;
                break;
            case GridKey.PageDown:
                selection.NavigatePageDown(pageSize, totalRows, e.HasShift);
                EnsureCurrentCellVisible(scroll, selection, style, dataSource.Columns);
                Redraw();
                e.Handled = true;
                break;
            case GridKey.A:
                if (e.HasControl)
                {
                    selection.SelectAll(totalRows);
                    Redraw();
                    e.Handled = true;
                }
                break;
            case GridKey.Escape:
                selection.ClearSelection();
                Redraw();
                e.Handled = true;
                break;
            case GridKey.Tab:
                // Tab in normal mode: navigate to next editable cell and begin editing
                if (selection.CurrentCell.IsValid)
                {
                    if (e.HasShift)
                        NavigateToNextEditableCell(selection, dataSource, scroll, style, -1);
                    else
                        NavigateToNextEditableCell(selection, dataSource, scroll, style, 1);

                    TryBeginEdit(selection, dataSource, scroll, style, null);
                    Redraw();
                }
                e.Handled = true;
                break;
            case GridKey.Space:
                // Toggle selection on current row
                if (selection.CurrentCell.IsValid)
                {
                    selection.HandleRowSelection(selection.CurrentCell.Row, false, true, totalRows);
                    Redraw();
                    e.Handled = true;
                }
                break;

            default:
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
        GridPointerEventArgs e, ScrollState scroll,
        SelectionModel selection, DataGridStyle style,
        IReadOnlyList<DataGridColumn> columns, int totalRows,
        DataGridSource dataSource)
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
            var groups = dataSource.GroupDescriptions;
            if (hit.GroupDescriptionIndex < groups.Count)
            {
                var group = groups[hit.GroupDescriptionIndex];
                group.GroupSortDirection = group.GroupSortDirection == SortDirection.Ascending
                    ? SortDirection.Descending
                    : SortDirection.Ascending;
                dataSource.RebuildView();
                Redraw();
            }
            e.Handled = true;
            return;
        }

        // Group chevron → toggle expand/collapse
        if (hit.Region == HitRegion.GroupChevron)
        {
            dataSource.ToggleGroupExpansion(hit.RowIndex);
            Redraw();
            e.Handled = true;
            return;
        }

        // Group header row click → toggle expand/collapse (whole row acts as toggle)
        if (hit.Region == HitRegion.GroupHeaderRow)
        {
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
        GridPointerEventArgs e, ScrollState scroll,
        SelectionModel selection, DataGridStyle style,
        IReadOnlyList<DataGridColumn> columns, int totalRows,
        DataGridSource dataSource)
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
                    Math.Abs(e.X - _lastPanX) > Math.Abs(e.Y - _lastPanY))
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
                    startHit.RowIndex >= 0)
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
        GridPointerEventArgs e, ScrollState scroll,
        SelectionModel selection, DataGridStyle style,
        IReadOnlyList<DataGridColumn> columns, int totalRows,
        DataGridSource dataSource)
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
            if (hit.Column.AllowFiltering)
            {
                ShowFilterPopup(hit.Column, hit.ColumnIndex, scroll, style, dataSource);
            }
            e.Handled = true;
            return;
        }

        if (hit.Region == HitRegion.HeaderCell && hit.Column != null)
        {
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

        if (hit.Region == HitRegion.Cell && hit.RowIndex >= 0)
        {
            bool shift = (e.Modifiers & InputModifiers.Shift) != 0;
            bool ctrl = (e.Modifiers & InputModifiers.Control) != 0;

            // If editing, click outside editor → commit and continue to select
            if (_editSession != null && _editSession.IsEditing)
            {
                _editSession.CommitEdit(dataSource);
                selection.IsEditing = false;
            }

            selection.HandleRowSelection(hit.RowIndex, shift, ctrl, totalRows);
            selection.CurrentCell = new CellPosition(hit.RowIndex, hit.ColumnIndex);

            RowTapped?.Invoke(this, new RowTappedEventArgs2(
                hit.RowIndex, hit.ColumnIndex, dataSource.GetItem(hit.RowIndex)));

            if (e.ClickCount == 2)
            {
                RowDoubleTapped?.Invoke(this, new RowTappedEventArgs2(
                    hit.RowIndex, hit.ColumnIndex, dataSource.GetItem(hit.RowIndex)));

                // Double-tap edit trigger
                if (_editSession != null && hit.Column != null &&
                    (GetEffectiveEditTriggers(hit.Column) & EditTrigger.DoubleTap) != 0)
                {
                    // Boolean toggle on double-tap
                    if (hit.Column.ColumnType == DataGridColumnType.Boolean)
                    {
                        _editSession.ToggleBooleanCell(
                            hit.RowIndex, hit.ColumnIndex, hit.Column, dataSource);
                    }
                    else
                    {
                        TryBeginEdit(selection, dataSource, scroll, style, null);
                    }
                }
            }
            else if (e.ClickCount == 1 && hit.Column != null)
            {
                // Single-tap toggle for boolean cells (more natural than double-tap)
                if (hit.Column.ColumnType == DataGridColumnType.Boolean && _editSession != null)
                {
                    _editSession.ToggleBooleanCell(
                        hit.RowIndex, hit.ColumnIndex, hit.Column, dataSource);
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
    private void HandlePointerCancelled(GridPointerEventArgs e, ScrollState scroll)
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

    private void HandleScroll(GridPointerEventArgs e, ScrollState scroll, DataGridStyle style)
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
        GridPointerEventArgs e,
        ScrollState scroll, SelectionModel selection, DataGridStyle style,
        IReadOnlyList<DataGridColumn> columns, int totalRows,
        DataGridSource dataSource)
    {
        var hit = _hitTester.HitTest(e.X, e.Y, scroll, style, columns, totalRows, dataSource);
        if (hit.Region != HitRegion.Cell || hit.RowIndex < 0) return;

        // Select the cell under the long press
        selection.CurrentCell = new CellPosition(hit.RowIndex, hit.ColumnIndex);

        if (_editSession != null && hit.Column != null &&
            (GetEffectiveEditTriggers(hit.Column) & EditTrigger.LongPress) != 0)
        {
            if (hit.Column.ColumnType == DataGridColumnType.Boolean)
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

    private void HandleColumnResize(GridPointerEventArgs e, IReadOnlyList<DataGridColumn> columns)
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
        IReadOnlyList<DataGridColumn> columns, DataGridStyle style)
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
            // The columns list is from DataGridSource which uses List<DataGridColumn>
            // We need to reorder via the source
            ColumnReordered?.Invoke(this, new ColumnReorderedEventArgs(
                _dragColumnIndex, dropIndex, columns[_dragColumnIndex]));
        }
    }

    // ── Row reorder ──

    private void FinishRowReorder(
        float dropY, ScrollState scroll,
        DataGridStyle style, DataGridSource dataSource)
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

    private void EnsureCurrentCellVisible(
        ScrollState scroll, SelectionModel selection,
        DataGridStyle style, IReadOnlyList<DataGridColumn> columns,
        DataGridSource? dataSource = null)
    {
        if (!selection.CurrentCell.IsValid) return;

        int row = selection.CurrentCell.Row;
        int col = selection.CurrentCell.Column;

        // Account for top summary area between header and data
        float topSummaryHeight = dataSource != null ? dataSource.TopSummaryCount * style.SummaryRowHeight : 0;

        // Account for frozen rows
        int frozenRowCount = dataSource?.EffectiveFrozenRowCount ?? 0;
        float frozenRowHeight = frozenRowCount * style.RowHeight;

        // If the row is a frozen row, no vertical scroll needed for it
        if (row >= frozenRowCount)
        {
            // Scrollable row: ensure visible below frozen rows
            float scrollableRowTop = (row - frozenRowCount) * style.RowHeight;
            float scrollableRowBottom = scrollableRowTop + style.RowHeight;

            float headerAndSummaryAndFrozen = style.HeaderHeight + topSummaryHeight + frozenRowHeight;
            if (scrollableRowTop < scroll.OffsetY)
                scroll.OffsetY = scrollableRowTop;
            else if (scrollableRowBottom > scroll.OffsetY + scroll.ViewportHeight - headerAndSummaryAndFrozen + scroll.OffsetY)
            {
                // Ensure the row bottom doesn't extend past the viewport
                float visibleBottom = scroll.ViewportHeight - headerAndSummaryAndFrozen;
                if (scrollableRowBottom > scroll.OffsetY + visibleBottom)
                    scroll.OffsetY = scrollableRowBottom - visibleBottom;
            }
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

        // Scrollable area: starts at frozenWidth, ends at viewportWidth - rightFrozenWidth
        float scrollableLeft = scroll.OffsetX + frozenWidth;
        float scrollableRight = scroll.OffsetX + scroll.ViewportWidth - rightFrozenWidth;

        if (colLeft < scrollableLeft)
            scroll.OffsetX = colLeft - frozenWidth;
        else if (colRight > scrollableRight)
            scroll.OffsetX = colRight - (scroll.ViewportWidth - rightFrozenWidth);

        scroll.ClampOffset();
    }

    private void Redraw() => NeedsRedraw?.Invoke();

    private void EnsurePopupRedrawWired()
    {
        if (_popupRedrawWired) return;
        _popupManager.NeedsRedraw += Redraw;
        _popupRedrawWired = true;
    }

    // ── Filter popup ──────────────────────────────────────────────

    private void ShowFilterPopup(
        DataGridColumn column, int columnIndex,
        ScrollState scroll, DataGridStyle style,
        DataGridSource dataSource)
    {
        // Close any existing popup
        CloseFilterPopup();

        var popup = new DrawnFilterPopup(column, dataSource);
        popup.ApplyTheme(style);

        // Position below the column header based on freeze mode
        float screenX;
        if (column.IsFrozen)
        {
            float x = 0;
            foreach (var c in dataSource.Columns)
            {
                if (c == column) break;
                if (c.IsVisible && c.IsFrozen) x += c.Width;
            }
            screenX = x;
        }
        else if (column.IsFrozenRight)
        {
            float rightFrozenWidth = 0;
            foreach (var c in dataSource.Columns)
                if (c.IsVisible && c.IsFrozenRight) rightFrozenWidth += c.Width;

            float rightFrozenLeft = scroll.ViewportWidth - rightFrozenWidth;
            float x = rightFrozenLeft;
            foreach (var c in dataSource.Columns)
            {
                if (!c.IsVisible || !c.IsFrozenRight) continue;
                if (c == column) break;
                x += c.Width;
            }
            screenX = x;
        }
        else
        {
            float frozenWidth = 0;
            foreach (var c in dataSource.Columns)
                if (c.IsVisible && c.IsFrozen) frozenWidth += c.Width;

            float colX = frozenWidth;
            foreach (var c in dataSource.Columns)
            {
                if (!c.IsVisible || c.IsFrozen || c.IsFrozenRight) continue;
                if (c == column) break;
                colX += c.Width;
            }
            screenX = colX - scroll.OffsetX;
        }
        float anchorY = style.HeaderHeight;

        // Ensure popup stays within viewport
        float popupWidth = 220;
        if (screenX + popupWidth > scroll.ViewportWidth)
            screenX = scroll.ViewportWidth - popupWidth - 4;
        if (screenX < 0) screenX = 4;

        var anchor = new GridRect(screenX, anchorY, column.Width, 0);
        popup.Bounds = new GridRect(screenX, anchorY, popupWidth, 300);

        // Wire events
        popup.FilterApplied += () => { CloseFilterPopup(); Redraw(); };
        popup.FilterCancelled += () => { CloseFilterPopup(); Redraw(); };
        popup.FilterCleared += () => { CloseFilterPopup(); Redraw(); };
        popup.SortRequested += dir =>
        {
            // Clear existing sorts and set this column
            foreach (var c in dataSource.Columns)
            {
                c.SortDirection = SortDirection.None;
                c.SortOrder = 0;
            }
            column.SortDirection = dir;
            column.SortOrder = 1;
            dataSource.RebuildView();
            CloseFilterPopup();
            Redraw();
        };

        popup.RedrawRequested += Redraw;
        popup.KeyboardFocusRequested += () => KeyboardFocusRequested?.Invoke();

        _popupManager.Show(popup, anchor, PopupPlacement.Below);
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
        {
            _popupManager.Close(_activeFilterPopup);
            _activeFilterPopup = null;
            FilterPopupClosed?.Invoke();
        }
    }

    // ── Edit helpers ──────────────────────────────────────────────

    /// <summary>
    /// Resolves the effective <see cref="EditTrigger"/> for <paramref name="column"/>.
    /// When the column has its own <see cref="DataGridColumn.EditTriggers"/> override it is
    /// returned directly; otherwise the grid-level <see cref="EditSession.EditTriggers"/> is
    /// used, falling back to <see cref="EditTrigger.Default"/> if the session is null.
    /// </summary>
    private EditTrigger GetEffectiveEditTriggers(DataGridColumn? column)
        => column?.EditTriggers ?? _editSession?.EditTriggers ?? EditTrigger.Default;

    /// <summary>
    /// Try to begin editing the current cell.
    /// </summary>
    private void TryBeginEdit(
        SelectionModel selection, DataGridSource dataSource,
        ScrollState scroll, DataGridStyle style, char? initialCharacter,
        GridPointerEventArgs? triggeringTap = null)
    {
        if (_editSession == null || !selection.CurrentCell.IsValid) return;

        int row = selection.CurrentCell.Row;
        int col = selection.CurrentCell.Column;
        if (col < 0 || col >= dataSource.Columns.Count) return;

        // Cannot edit group header or summary rows
        if (dataSource.IsNonDataRow(row)) return;

        var column = dataSource.Columns[col];
        if (column.IsReadOnly) return;

        // Calculate cell bounds on screen (accounting for group panel offset and top summaries)
        float groupPanelOffset = dataSource.IsGroupingActive ? style.GroupPanelHeight : 0;
        float topSummaryHeight = dataSource.TopSummaryCount * style.SummaryRowHeight;
        var cellBounds = GetCellScreenBounds(row, col, scroll, style, dataSource.Columns, groupPanelOffset, topSummaryHeight, dataSource);
        if (cellBounds.Width <= 0 || cellBounds.Height <= 0) return;

        if (_editSession.BeginEdit(row, col, column, dataSource, cellBounds, initialCharacter))
        {
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
        }
    }

    /// <summary>
    /// Calculate the screen bounds for a cell (viewport coordinates).
    /// Handles left-frozen, right-frozen, and frozen rows.
    /// </summary>
    public GridRect GetCellScreenBounds(
        int row, int col,
        ScrollState scroll, DataGridStyle style,
        IReadOnlyList<DataGridColumn> columns,
        float groupPanelOffset = 0,
        float topSummaryHeight = 0,
        DataGridSource? dataSource = null)
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

            return new GridRect(screenX, rowY, targetCol.Width, style.RowHeight);
        }

        return new GridRect(0, rowY, 0, style.RowHeight);
    }

    /// <summary>
    /// Navigate to the next editable cell in the given direction.
    /// direction: +1 = right, -1 = left.
    /// </summary>
    private void NavigateToNextEditableCell(
        SelectionModel selection, DataGridSource dataSource,
        ScrollState scroll, DataGridStyle style, int direction)
    {
        if (!selection.CurrentCell.IsValid) return;

        int totalCols = dataSource.Columns.Count;
        int totalRows = dataSource.RowCount;
        int row = selection.CurrentCell.Row;
        int col = selection.CurrentCell.Column;
        int startRow = row;
        int startCol = col;

        // Search for the next cell that allows tab stop and is not read-only
        int maxIterations = totalCols * totalRows; // prevent infinite loop
        int iterations = 0;

        while (iterations++ < maxIterations)
        {
            // Move to next/previous cell
            col += direction;

            // Wrap to next/previous row if needed
            if (col >= totalCols)
            {
                col = 0;
                row++;
            }
            else if (col < 0)
            {
                col = totalCols - 1;
                row--;
            }

            // Skip group header and summary rows
            while (row >= 0 && row < totalRows && dataSource.IsNonDataRow(row))
                row += direction > 0 ? 1 : -1;

            // Bounds check
            if (row < 0 || row >= totalRows) return;

            // Check if we wrapped all the way around
            if (row == startRow && col == startCol) return;

            var column = dataSource.Columns[col];

            // Skip columns that don't allow tab stop or are not visible
            if (!column.AllowTabStop || !column.IsVisible)
                continue;

            // Skip read-only columns (can't edit them)
            if (column.IsReadOnly)
                continue;

            // Found a valid target cell
            selection.CurrentCell = new CellPosition(row, col);
            selection.HandleRowSelection(row, false, false, totalRows);
            EnsureCurrentCellVisible(scroll, selection, style, dataSource.Columns);
            return;
        }
    }
}

/// <summary>Provides data for the <see cref="GridInputController.ColumnResized"/> event.</summary>
public class ColumnResizedEventArgs : EventArgs
{
    /// <summary>Zero-based index of the resized column.</summary>
    public int ColumnIndex { get; }
    /// <summary>The column that was resized.</summary>
    public DataGridColumn Column { get; }
    /// <summary>Initializes a new instance of the <see cref="ColumnResizedEventArgs"/> class.</summary>
    /// <param name="index">The zero-based column index.</param>
    /// <param name="column">The resized column.</param>
    public ColumnResizedEventArgs(int index, DataGridColumn column) { ColumnIndex = index; Column = column; }
}

/// <summary>Provides data for the <see cref="GridInputController.ColumnReordered"/> event.</summary>
public class ColumnReorderedEventArgs : EventArgs
{
    /// <summary>Original zero-based index of the column before it was moved.</summary>
    public int OldIndex { get; }
    /// <summary>New zero-based index of the column after it was moved.</summary>
    public int NewIndex { get; }
    /// <summary>The column that was reordered.</summary>
    public DataGridColumn Column { get; }
    /// <summary>Initializes a new instance of the <see cref="ColumnReorderedEventArgs"/> class.</summary>
    /// <param name="oldIndex">The original column index.</param>
    /// <param name="newIndex">The new column index.</param>
    /// <param name="column">The reordered column.</param>
    public ColumnReorderedEventArgs(int oldIndex, int newIndex, DataGridColumn column)
    {
        OldIndex = oldIndex; NewIndex = newIndex; Column = column;
    }
}

/// <summary>Provides data for the <see cref="GridInputController.RowReordered"/> event.</summary>
public class RowReorderedEventArgs : EventArgs
{
    /// <summary>Original zero-based index of the row before it was moved.</summary>
    public int OldIndex { get; }
    /// <summary>New zero-based index of the row after it was moved.</summary>
    public int NewIndex { get; }
    /// <summary>The data item associated with the reordered row.</summary>
    public object? DataItem { get; }
    /// <summary>Initializes a new instance of the <see cref="RowReorderedEventArgs"/> class.</summary>
    /// <param name="oldIndex">The original row index.</param>
    /// <param name="newIndex">The new row index.</param>
    /// <param name="dataItem">The data item of the reordered row.</param>
    public RowReorderedEventArgs(int oldIndex, int newIndex, object? dataItem)
    {
        OldIndex = oldIndex; NewIndex = newIndex; DataItem = dataItem;
    }
}

/// <summary>Provides data for the <see cref="GridInputController.RowTapped"/> and <see cref="GridInputController.RowDoubleTapped"/> events.</summary>
public class RowTappedEventArgs2 : EventArgs
{
    /// <summary>Zero-based index of the tapped row.</summary>
    public int RowIndex { get; }
    /// <summary>Zero-based index of the tapped column.</summary>
    public int ColumnIndex { get; }
    /// <summary>The data item associated with the tapped row.</summary>
    public object DataItem { get; }
    /// <summary>Initializes a new instance of the <see cref="RowTappedEventArgs2"/> class.</summary>
    /// <param name="row">The tapped row index.</param>
    /// <param name="col">The tapped column index.</param>
    /// <param name="item">The row's data item.</param>
    public RowTappedEventArgs2(int row, int col, object item)
    {
        RowIndex = row; ColumnIndex = col; DataItem = item;
    }
}

/// <summary>Provides data when a column auto-fit is requested (e.g., double-click on a column resize handle).</summary>
public class AutoFitColumnEventArgs : EventArgs
{
    /// <summary>Zero-based index of the column to auto-fit.</summary>
    public int ColumnIndex { get; }
    /// <summary>The column to auto-fit.</summary>
    public DataGridColumn Column { get; }
    /// <summary>Initializes a new instance of the <see cref="AutoFitColumnEventArgs"/> class.</summary>
    /// <param name="index">The zero-based column index.</param>
    /// <param name="column">The column to auto-fit.</param>
    public AutoFitColumnEventArgs(int index, DataGridColumn column)
    {
        ColumnIndex = index;
        Column = column;
    }
}

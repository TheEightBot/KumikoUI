using KumikoUI.Core.Components;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Editing;

/// <summary>
/// Manages inline cell editing. Tracks the active edit session,
/// creates/positions editors, and handles commit/cancel.
/// </summary>
public class EditSession
{
    private readonly DrawnFocusManager _focusManager = new();

    // Edit state. The edit belongs to an item and a column; _editCell is where they are shown now, kept
    // current by ApplyViewChange (rows move with sort, filter, grouping; columns with reordering).
    private bool _isEditing;
    private CellPosition _editCell = CellPosition.Invalid;
    private object? _editItem;
    private bool _isCommitting;
    private object? _originalValue;
    private DrawnComponent? _activeEditor;
    private KumikoColumn? _editColumn;
    private KumikoSource? _editDataSource;

    /// <summary>Whether a cell is currently being edited.</summary>
    public bool IsEditing => _isEditing;

    /// <summary>The visible cell currently being edited (it moves with its item and column).</summary>
    public CellPosition EditCell => _editCell;

    /// <summary>The data item being edited, or null when not editing.</summary>
    public object? EditItem => _editItem;

    /// <summary>The column being edited, or null when not editing.</summary>
    public KumikoColumn? EditColumn => _editColumn;

    /// <summary>The active editor component (null if not editing).</summary>
    public DrawnComponent? ActiveEditor => _activeEditor;

    /// <summary>
    /// The active editor's text-input surface for a host's native text-input adapter, or null when not
    /// editing or the editor takes no text (a check box, a picker).
    /// </summary>
    public ITextInputSession? ActiveTextInput => _activeEditor as ITextInputSession;

    /// <summary>
    /// Edit trigger configuration. Default: DoubleTap + F2 + Typing.
    /// </summary>
    public EditTrigger EditTriggers { get; set; } = EditTrigger.Default;

    /// <summary>
    /// Controls how text is selected when a cell enters edit mode.
    /// Default: SelectAll.
    /// </summary>
    public EditTextSelectionMode TextSelectionMode { get; set; } = EditTextSelectionMode.SelectAll;

    /// <summary>
    /// When true, pressing Enter while editing will commit the edit, dismiss the
    /// keyboard, and end editing. When false, pressing Enter commits the current
    /// cell and automatically begins editing the cell below.
    /// Default: true.
    /// </summary>
    public bool DismissKeyboardOnEnter { get; set; } = true;

    /// <summary>Optional custom cell validator.</summary>
    public Func<int, int, object?, CellValidationResult>? CellValidator { get; set; }

    /// <summary>Current validation result for the active edit.</summary>
    public CellValidationResult? CurrentValidation { get; private set; }

    /// <summary>
    /// The current grid style. When set, newly created editors will have
    /// their theme colors applied automatically.
    /// </summary>
    public KumikoStyle? Style { get; set; }

    // ── Events ──────────────────────────────────────────────────

    /// <summary>Fires before a cell enters edit mode. Set Cancel to prevent.</summary>
    public event EventHandler<CellBeginEditEventArgs>? CellBeginEdit;

    /// <summary>Fires after a cell exits edit mode.</summary>
    public event EventHandler<CellEndEditEventArgs>? CellEndEdit;

    /// <summary>Fires when a cell value is changed via editing.</summary>
    public event EventHandler<CellValueChangedEventArgs>? CellValueChanged;

    /// <summary>Raised when the editor component needs to be redrawn.</summary>
    public event Action? NeedsRedraw;

    /// <summary>
    /// Raised when the active editor copies or cuts text (Ctrl/Cmd+C, Ctrl/Cmd+X): the host puts
    /// <see cref="ClipboardTextEventArgs.Text"/> on the system clipboard.
    /// </summary>
    public event EventHandler<ClipboardTextEventArgs>? ClipboardCopyRequested;

    /// <summary>
    /// Raised when the active editor pastes (Ctrl/Cmd+V): the host sets
    /// <see cref="ClipboardRequestEventArgs.Text"/> to the system clipboard's text, synchronously.
    /// </summary>
    public event EventHandler<ClipboardRequestEventArgs>? ClipboardPasteRequested;

    // ── Edit lifecycle ──────────────────────────────────────────

    /// <summary>
    /// Try to begin editing a cell.
    /// </summary>
    public bool BeginEdit(
        int rowIndex, int columnIndex,
        KumikoColumn column, KumikoSource dataSource,
        KumikoRect cellBounds, char? initialCharacter = null)
    {
        if (_isEditing)
            CommitEdit(dataSource);

        if (column.IsReadOnly) return false;
        if (rowIndex < 0 || rowIndex >= dataSource.RowCount || dataSource.IsNonDataRow(rowIndex)) return false;

        var item = dataSource.GetItem(rowIndex);
        var cellValue = dataSource.GetCellValue(rowIndex, column);

        // Raise begin-edit event (cancelable)
        var args = new CellBeginEditEventArgs(rowIndex, columnIndex, column, cellValue);
        CellBeginEdit?.Invoke(this, args);
        if (args.Cancel) return false;

        _isEditing = true;
        _editCell = new CellPosition(rowIndex, columnIndex);
        _editItem = item;
        _editColumn = column;
        _originalValue = cellValue;
        CurrentValidation = null;

        // Create the editor
        _activeEditor = CellEditorFactory.CreateEditor(column, cellValue, cellBounds, initialCharacter, TextSelectionMode);
        if (_activeEditor == null)
        {
            _isEditing = false;
            _editCell = CellPosition.Invalid;
            _editItem = null;
            _editColumn = null;
            return false;
        }

        // Apply theme colors to the new editor
        if (Style != null)
            CellEditorFactory.ApplyThemeToEditor(_activeEditor, Style);

        _activeEditor.RedrawRequested += OnEditorRedrawRequested;
        _activeEditor.EditCompleted += OnEditorEditCompleted;
        if (_activeEditor is DrawnTextBox textBox)
        {
            textBox.CopyRequested += OnEditorCopyRequested;
            textBox.PasteRequested += OnEditorPasteRequested;
        }
        _editDataSource = dataSource;
        dataSource.ViewChanged += OnViewChanged;
        _focusManager.Register(_activeEditor);
        _focusManager.SetFocus(_activeEditor);

        NeedsRedraw?.Invoke();
        return true;
    }

    /// <summary>
    /// Commit the current edit, writing the value to the edited item (wherever its row is now).
    /// Returns true if the commit succeeded; false when nothing is being edited or the value is invalid
    /// (the editor stays open and shows the error).
    /// </summary>
    public bool CommitEdit(KumikoSource dataSource)
    {
        if (!_isEditing || _activeEditor == null || _editColumn == null || _editItem == null) return false;

        // Text still being composed by an input method is part of the value
        (_activeEditor as DrawnTextBox)?.CommitComposition();
        var newValue = CellEditorFactory.GetEditorValue(_activeEditor, _editColumn);

        // Validate
        CurrentValidation = ValidateValue(_editCell.Row, _editCell.Column, _editItem, newValue);
        if (CurrentValidation != null && !CurrentValidation.IsValid)
        {
            NeedsRedraw?.Invoke();
            return false; // Keep editing, show error
        }

        // Write value back. The write raises ViewChanged, which may move the row (ApplyViewChange); the events
        // below report the row the edit was made on.
        var editedCell = _editCell;
        _isCommitting = true;
        try
        {
            dataSource.SetItemValue(_editItem, _editColumn, newValue);
        }
        finally
        {
            _isCommitting = false;
        }

        var endArgs = new CellEndEditEventArgs(
            editedCell.Row, editedCell.Column, _editColumn,
            _originalValue, newValue, committed: true);

        // Raise value changed if different
        if (!Equals(_originalValue, newValue))
        {
            CellValueChanged?.Invoke(this, new CellValueChangedEventArgs(
                editedCell.Row, editedCell.Column, _editColumn,
                _originalValue, newValue));
        }

        EndEdit();
        CellEndEdit?.Invoke(this, endArgs);
        return true;
    }

    /// <summary>
    /// Cancel the current edit, discarding changes.
    /// </summary>
    public void CancelEdit()
    {
        if (!_isEditing) return;

        var endArgs = new CellEndEditEventArgs(
            _editCell.Row, _editCell.Column, _editColumn!,
            _originalValue, _originalValue, committed: false);

        EndEdit();
        CellEndEdit?.Invoke(this, endArgs);
    }

    /// <summary>
    /// Toggle a boolean cell directly (no editor needed).
    /// </summary>
    public void ToggleBooleanCell(int row, int col, KumikoColumn column, KumikoSource dataSource)
    {
        if (column.IsReadOnly) return;
        if (row < 0 || row >= dataSource.RowCount || dataSource.IsNonDataRow(row)) return;

        var currentValue = dataSource.GetCellValue(row, column);
        bool current = currentValue is true;
        var newValue = (object)!current;

        // Raise begin-edit (cancelable)
        var beginArgs = new CellBeginEditEventArgs(row, col, column, currentValue);
        CellBeginEdit?.Invoke(this, beginArgs);
        if (beginArgs.Cancel) return;

        dataSource.SetCellValue(row, column, newValue);

        CellValueChanged?.Invoke(this, new CellValueChangedEventArgs(
            row, col, column, currentValue, newValue));
        CellEndEdit?.Invoke(this, new CellEndEditEventArgs(
            row, col, column, currentValue, newValue, committed: true));

        NeedsRedraw?.Invoke();
    }

    /// <summary>
    /// Keep an active edit on its item after the grid's view changes: the edited cell follows the item's row
    /// (sort, insert, removal above it) and the column's position (column reorder), and the edit is canceled
    /// when the item is no longer visible (filtered out, collapsed, removed) or its column was removed. A
    /// later commit writes to the item wherever it is. The session subscribes to the edited source's
    /// <see cref="KumikoSource.ViewChanged"/> while an edit is open, so callers need not call this.
    /// </summary>
    public void ApplyViewChange(KumikoViewChangedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (!_isEditing || _editItem == null || _editColumn == null || _editDataSource == null) return;

        int row = e.IsStructural ? e.GetViewRow(_editItem) : _editCell.Row;
        int column = IndexOfColumn(_editDataSource, _editColumn);
        if (row >= 0 && column >= 0)
            _editCell = new CellPosition(row, column);
        else if (!_isCommitting)
            CancelEdit();
    }

    private static int IndexOfColumn(KumikoSource dataSource, KumikoColumn column)
    {
        var columns = dataSource.Columns;
        for (int i = 0; i < columns.Count; i++)
        {
            if (ReferenceEquals(columns[i], column)) return i;
        }
        return -1;
    }

    /// <summary>
    /// Update the editor bounds (e.g., when scrolling while editing).
    /// </summary>
    public void UpdateEditorBounds(KumikoRect cellBounds)
    {
        if (_activeEditor != null)
            _activeEditor.Bounds = cellBounds;
    }

    /// <summary>
    /// Place the editor over its cell for this frame, or hide it while the cell is scrolled out of view
    /// (<paramref name="cellBounds"/> null). A hidden editor keeps its value and focus and reappears when the
    /// cell scrolls back. Called by the renderer every frame.
    /// </summary>
    internal void PlaceEditor(KumikoRect? cellBounds)
    {
        if (_activeEditor == null) return;
        if (cellBounds is { } bounds)
            _activeEditor.Bounds = bounds;
        _activeEditor.IsVisible = cellBounds.HasValue;
    }

    /// <summary>
    /// Check if the given cell is currently being edited.
    /// </summary>
    public bool IsCellBeingEdited(int row, int col) =>
        _isEditing && _editCell.Row == row && _editCell.Column == col;

    // ── Rendering ───────────────────────────────────────────────

    /// <summary>
    /// Draw the active editor (call after main grid rendering).
    /// </summary>
    public void DrawEditor(IDrawingContext ctx)
    {
        if (!_isEditing || _activeEditor == null || !_activeEditor.IsVisible) return;

        var bounds = _activeEditor.Bounds;

        // Resolve frame colors from theme or fall back to defaults
        var bgColor = Style?.BackgroundColor ?? KumikoColor.White;
        var shadowColor = Style != null
            ? new KumikoColor(Style.CellTextColor.R, Style.CellTextColor.G, Style.CellTextColor.B, 30)
            : new KumikoColor(0, 0, 0, 30);

        // Draw subtle drop shadow for depth effect
        var shadowBounds = bounds.Offset(1, 1).Inflate(1, 1);
        ctx.FillRect(shadowBounds, new KumikoPaint
        {
            Color = shadowColor,
            Style = PaintStyle.Fill
        });

        // Background to cover cell content underneath
        ctx.FillRect(bounds, new KumikoPaint { Color = bgColor });
        _activeEditor.OnDraw(ctx);

        // Draw validation error indicator (overrides normal border)
        if (CurrentValidation != null && !CurrentValidation.IsValid)
        {
            var errorColor = CurrentValidation.Severity == ValidationSeverity.Warning
                ? new KumikoColor(255, 165, 0)   // orange
                : new KumikoColor(220, 50, 50);   // red

            ctx.DrawRect(bounds, new KumikoPaint
            {
                Color = errorColor,
                Style = PaintStyle.Stroke,
                StrokeWidth = 2f,
                IsAntiAlias = true
            });
        }
    }

    // ── Input forwarding ────────────────────────────────────────

    /// <summary>
    /// Forward a key event to the active editor.
    /// Returns true if the editor handled it.
    /// </summary>
    public bool HandleKeyEvent(KumikoKeyEventArgs e)
    {
        if (!_isEditing) return false;
        return _focusManager.DispatchKey(e);
    }

    /// <summary>
    /// Forward a pointer event to the active editor.
    /// Returns true if the editor handled it.
    /// </summary>
    public bool HandlePointerEvent(KumikoPointerEventArgs e)
    {
        if (!_isEditing || _activeEditor == null) return false;

        // Check if click is inside editor
        if (_activeEditor.HitTest(e.X, e.Y))
            return _focusManager.DispatchPointer(e);

        return false;
    }

    /// <summary>
    /// Immediately forwards the activating tap to an editor that has
    /// <see cref="DrawnComponent.ActivatesImmediately"/> set to <c>true</c>
    /// (e.g. <c>DrawnActionButtons</c>).
    /// Synthesizes a <c>Pressed</c> event at the same coordinates and then
    /// dispatches the original <c>Released</c> event so the editor sees a
    /// complete press–release cycle without requiring a second tap.
    /// Does nothing if the session is not currently editing or the active
    /// editor does not have <c>ActivatesImmediately == true</c>.
    /// </summary>
    /// <param name="releasedEvent">
    /// The original <c>Released</c> pointer event that triggered <c>BeginEdit</c>.
    /// </param>
    public void TryForwardInitialTap(KumikoPointerEventArgs releasedEvent)
    {
        if (!_isEditing || _activeEditor == null) return;
        if (!_activeEditor.ActivatesImmediately) return;
        if (!_activeEditor.HitTest(releasedEvent.X, releasedEvent.Y)) return;

        var syntheticDown = new KumikoPointerEventArgs
        {
            X = releasedEvent.X,
            Y = releasedEvent.Y,
            Action = InputAction.Pressed,
            Button = releasedEvent.Button,
            Modifiers = releasedEvent.Modifiers
        };

        _focusManager.DispatchPointer(syntheticDown);
        _focusManager.DispatchPointer(releasedEvent);
    }

    // ── Validation ──────────────────────────────────────────────

    private CellValidationResult? ValidateValue(int row, int col, object item, object? value)
    {
        // Check custom validator first
        if (CellValidator != null)
            return CellValidator(row, col, value);

        // Check IDataErrorInfo on the data item
        try
        {
            if (item is System.ComponentModel.IDataErrorInfo errorInfo)
            {
                var colDef = _editColumn;
                if (colDef != null)
                {
                    var error = errorInfo[colDef.PropertyName];
                    if (!string.IsNullOrEmpty(error))
                        return CellValidationResult.Error(error);
                }
            }

            // Check INotifyDataErrorInfo
            if (item is System.ComponentModel.INotifyDataErrorInfo notifyErrorInfo)
            {
                var colDef = _editColumn;
                if (colDef != null && notifyErrorInfo.HasErrors)
                {
                    var errors = notifyErrorInfo.GetErrors(colDef.PropertyName);
                    if (errors != null)
                    {
                        foreach (var err in errors)
                        {
                            if (err != null)
                                return CellValidationResult.Error(err.ToString() ?? "Validation error");
                        }
                    }
                }
            }
        }
        catch
        {
            // Graceful fallback if data item access fails
        }

        return CellValidationResult.Valid;
    }

    private void EndEdit()
    {
        if (_editDataSource != null)
            _editDataSource.ViewChanged -= OnViewChanged;
        if (_activeEditor != null)
        {
            _activeEditor.RedrawRequested -= OnEditorRedrawRequested;
            _activeEditor.EditCompleted -= OnEditorEditCompleted;
            if (_activeEditor is DrawnTextBox textBox)
            {
                textBox.CopyRequested -= OnEditorCopyRequested;
                textBox.PasteRequested -= OnEditorPasteRequested;
            }
            _focusManager.Unregister(_activeEditor);
            _focusManager.ClearFocus();
            _activeEditor = null;
        }
        _isEditing = false;
        _editCell = CellPosition.Invalid;
        _editItem = null;
        _editColumn = null;
        _editDataSource = null;
        _originalValue = null;
        CurrentValidation = null;
        NeedsRedraw?.Invoke();
    }

    /// <summary>
    /// Called when an editor signals that its value is finalized
    /// (e.g. date picked, combo item selected). Auto-commits the edit.
    /// </summary>
    private void OnEditorRedrawRequested() => NeedsRedraw?.Invoke();

    private void OnViewChanged(object? sender, KumikoViewChangedEventArgs e) => ApplyViewChange(e);

    private void OnEditorCopyRequested(object? sender, ClipboardTextEventArgs e) => ClipboardCopyRequested?.Invoke(this, e);

    private void OnEditorPasteRequested(object? sender, ClipboardRequestEventArgs e) => ClipboardPasteRequested?.Invoke(this, e);

    private void OnEditorEditCompleted()
    {
        if (!_isEditing || _editDataSource == null) return;
        CommitEdit(_editDataSource);
    }
}

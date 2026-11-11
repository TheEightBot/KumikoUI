using KumikoUI.Core.Components;
using KumikoUI.Core.Data;
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
    private bool _warningShown;     // a warning was shown for _warnedValue; committing it again accepts it
    private object? _warnedValue;
    private bool _pointerCaptured;  // the editor took the press: moves and the release go to it

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

    /// <summary>The focus manager holding the active editor: exactly one editor while editing, none otherwise.</summary>
    internal DrawnFocusManager FocusManager => _focusManager;

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
    /// Grid-level read-only switch. While true no cell can be edited: <see cref="BeginEdit"/> returns false
    /// and Boolean cells do not toggle. It combines with the per-column <see cref="KumikoColumn.IsReadOnly"/>:
    /// a cell is editable only when neither is set. Setting it to true cancels an edit that is open.
    /// </summary>
    public bool IsReadOnly
    {
        get => _isReadOnly;
        set
        {
            if (_isReadOnly == value) return;
            _isReadOnly = value;
            if (value && _isEditing) CancelEdit();
        }
    }
    private bool _isReadOnly;

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

    /// <summary>
    /// Optional validator, called with the visible row, the column index and the value converted to the
    /// property type, before the value is written. An error keeps the editor open; the item's own
    /// validation (<see cref="System.ComponentModel.IDataErrorInfo"/>,
    /// <see cref="System.ComponentModel.INotifyDataErrorInfo"/>) still runs after this one passes.
    /// </summary>
    public Func<int, int, object?, CellValidationResult>? CellValidator { get; set; }

    /// <summary>
    /// The validation result the open editor shows (an error, or a warning awaiting confirmation), or
    /// null.
    /// </summary>
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
    /// Raised when a commit produces an error, a warning or an informational message, with the message
    /// for the user. See <see cref="CommitEdit"/> for the validation order.
    /// </summary>
    public event EventHandler<CellValidationEventArgs>? ValidationFailed;

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
    /// <remarks>
    /// An edit that is already open is committed first (<see cref="CommitEdit"/>). When that commit is
    /// refused, nothing else happens: the open editor stays, with its message, and this returns false. When
    /// it succeeds, the old editor is ended (and unregistered from focus) before the new one is created, so
    /// at most one editor exists at a time.
    /// </remarks>
    /// <returns>True if the cell is now being edited.</returns>
    public bool BeginEdit(
        int rowIndex, int columnIndex,
        KumikoColumn column, KumikoSource dataSource,
        KumikoRect cellBounds, char? initialCharacter = null)
    {
        if (_isReadOnly) return false;

        if (_isEditing && !CommitEdit(_editDataSource ?? dataSource))
            return false;

        // A CellEndEdit handler may have started another edit while the commit finished: it ends here
        if (_isEditing)
            CancelEdit();

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
        _activeEditor = CellEditorFactory.CreateEditor(column, cellValue, cellBounds, initialCharacter, TextSelectionMode,
            dataSource.Comparer.Culture);
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
        _activeEditor.ValueChanged += OnEditorValueChanged;
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
    /// </summary>
    /// <remarks>
    /// <para>In order:</para>
    /// <list type="number">
    /// <item>nothing is written when the editor has no value (<see cref="IValueEditor.HasValue"/>), the user
    /// didn't change it (<see cref="IValueEditor.IsDirty"/>), or it converts to the original value;</item>
    /// <item>the editor's value is converted to the property type in the source's culture
    /// (<see cref="KumikoValueConverter"/>); text that doesn't convert is an error;</item>
    /// <item><see cref="CellValidator"/> checks the converted value;</item>
    /// <item>the value is written; a setter that throws is an error, with its message;</item>
    /// <item>the item validates itself with the new value in place
    /// (<see cref="System.ComponentModel.IDataErrorInfo"/>, <see cref="System.ComponentModel.INotifyDataErrorInfo"/>);
    /// on an error the original value is written back.</item>
    /// </list>
    /// <para>
    /// An error keeps the editor open with the message (<see cref="CurrentValidation"/>,
    /// <see cref="ValidationFailed"/>) and raises no <see cref="CellValueChanged"/>. A warning is shown
    /// once the same way; committing the same value again accepts it. Informational messages never block.
    /// </para>
    /// </remarks>
    /// <returns>True if the value was committed; false when nothing is being edited or the value was refused.</returns>
    public bool CommitEdit(KumikoSource dataSource)
    {
        if (!_isEditing || _activeEditor == null || _editColumn == null || _editItem == null) return false;

        // Text still being composed by an input method is part of the value
        (_activeEditor as DrawnTextBox)?.CommitComposition();
        var item = _editItem;
        var column = _editColumn;
        var editedCell = _editCell;

        // Nothing to write: an editor without a value (action buttons, a custom editor that isn't an
        // IValueEditor), nothing chosen (a combo box whose value isn't in its list, an empty date), no change
        // by the user, or a column with nothing to write to. The cell keeps its value and the edit just ends.
        if (_activeEditor is not IValueEditor { HasValue: true, IsDirty: true } ||
            column.ValueSetter == null && string.IsNullOrEmpty(column.PropertyName))
            return EndUnchanged(editedCell, column);

        var editorValue = CellEditorFactory.GetEditorValue(_activeEditor, column);

        // 1. Convert to the property type
        object? newValue = editorValue;
        if (TargetType(item, column, _originalValue) is { } targetType &&
            !KumikoValueConverter.TryConvert(editorValue, targetType, dataSource.Comparer.Culture, dataSource.Strings, out newValue, out var conversionError))
        {
            return Refuse(CellValidationResult.Error(conversionError ?? dataSource.Strings.EnterValidValue), editorValue);
        }

        // The user changed the value back: nothing to write
        if (Equals(newValue, _originalValue))
            return EndUnchanged(editedCell, column);

        // 2. The grid's validator
        var result = CellValidator?.Invoke(editedCell.Row, editedCell.Column, newValue);
        if (result is { IsValid: false })
            return Refuse(result, newValue);

        // 3. Write, then 4. let the item validate itself with the new value in place. The write raises
        // ViewChanged, which may move the row (ApplyViewChange); the events report the row the edit was made on.
        _isCommitting = true;
        try
        {
            if (!dataSource.TrySetItemValue(item, column, newValue, out var writeError))
                return Refuse(CellValidationResult.Error(writeError ?? dataSource.Strings.ValueCouldNotBeSaved), newValue);

            var modelResult = ValidateItem(item, column);
            if (modelResult is { IsValid: false })
            {
                dataSource.TrySetItemValue(item, column, _originalValue, out _);
                return Refuse(modelResult, newValue);
            }
            result = MoreSevere(result, modelResult);

            // A warning is shown once; committing the same value again accepts it
            if (result is { Severity: ValidationSeverity.Warning } && !(_warningShown && Equals(_warnedValue, newValue)))
            {
                dataSource.TrySetItemValue(item, column, _originalValue, out _);
                _warningShown = true;
                _warnedValue = newValue;
                return Refuse(result, newValue);
            }
        }
        finally
        {
            _isCommitting = false;
        }

        var oldValue = _originalValue;

        // The edit ends first, so handlers see a session that is no longer editing (and may start the next
        // edit): CellEndEdit, then CellValueChanged, then a message that goes with the value.
        EndEdit();
        CellEndEdit?.Invoke(this, new CellEndEditEventArgs(
            editedCell.Row, editedCell.Column, column, oldValue, newValue, committed: true));

        if (!Equals(oldValue, newValue))
        {
            CellValueChanged?.Invoke(this, new CellValueChangedEventArgs(
                editedCell.Row, editedCell.Column, column, oldValue, newValue));
        }

        // A warning (accepted) or an informational message goes with the committed value
        if (result is { ErrorMessage: { Length: > 0 } })
            ValidationFailed?.Invoke(this, new CellValidationEventArgs(
                editedCell.Row, editedCell.Column, column, item, newValue, result, committed: true));

        return true;
    }

    /// <summary>End the edit without writing: committed, with the original value as old and new.</summary>
    private bool EndUnchanged(CellPosition editedCell, KumikoColumn column)
    {
        var unchanged = _originalValue;
        EndEdit();
        CellEndEdit?.Invoke(this, new CellEndEditEventArgs(
            editedCell.Row, editedCell.Column, column, unchanged, unchanged, committed: true));
        return true;
    }

    /// <summary>Keep the editor open showing <paramref name="result"/>, and report it.</summary>
    private bool Refuse(CellValidationResult result, object? value)
    {
        CurrentValidation = result;
        ValidationFailed?.Invoke(this, new CellValidationEventArgs(
            _editCell.Row, _editCell.Column, _editColumn!, _editItem!, value, result, committed: false));
        NeedsRedraw?.Invoke();
        return false;
    }

    /// <summary>
    /// The type an edited value converts to: the property's declared type, else the type of the value
    /// being replaced, else one implied by the column type; null to write the editor's value as it is.
    /// </summary>
    private static Type? TargetType(object item, KumikoColumn column, object? originalValue)
    {
        if (column.ValueSetter == null && !string.IsNullOrEmpty(column.PropertyName) &&
            column.ResolvePath(item.GetType()).ValueType is { } declared && declared != typeof(object))
            return declared;
        if (originalValue != null)
            return originalValue.GetType();
        return column.ColumnType switch
        {
            KumikoColumnType.Numeric => typeof(decimal?),
            KumikoColumnType.Date => typeof(DateTime?),
            KumikoColumnType.Boolean => typeof(bool?),
            _ => null,
        };
    }

    private static CellValidationResult? MoreSevere(CellValidationResult? a, CellValidationResult? b)
    {
        static int Rank(CellValidationResult? r) => r == null || string.IsNullOrEmpty(r.ErrorMessage) && r.IsValid ? 0
            : !r.IsValid ? 3
            : r.Severity == ValidationSeverity.Warning ? 2
            : 1;
        return Rank(b) > Rank(a) ? b : a;
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
    /// Toggle a boolean cell directly (no editor needed). The new value goes through the same pipeline as
    /// <see cref="CommitEdit"/>: <see cref="CellValidator"/>, the write, then the item's own validation,
    /// rolling the value back on an error and reporting it through <see cref="ValidationFailed"/>. Events
    /// follow a successful write in the order <see cref="CellEndEdit"/>, <see cref="CellValueChanged"/>; a
    /// refused toggle raises <see cref="CellEndEdit"/> with <c>Committed</c> false and no
    /// <see cref="CellValueChanged"/>. A warning does not block (there is no editor to keep open): it is
    /// reported and the value kept. An edit that is open is committed first, and the toggle is skipped when
    /// that commit is refused.
    /// </summary>
    public void ToggleBooleanCell(int row, int col, KumikoColumn column, KumikoSource dataSource)
    {
        if (column.IsReadOnly) return;
        if (row < 0 || row >= dataSource.RowCount || dataSource.IsNonDataRow(row)) return;

        if (_isEditing && !CommitEdit(_editDataSource ?? dataSource)) return;
        // The commit may have moved rows (sort, grouping): the row is checked again
        if (row >= dataSource.RowCount || dataSource.IsNonDataRow(row)) return;

        var item = dataSource.GetItem(row);
        var currentValue = dataSource.GetCellValue(row, column);
        bool current = currentValue is true;
        var newValue = (object)!current;

        // Raise begin-edit (cancelable)
        var beginArgs = new CellBeginEditEventArgs(row, col, column, currentValue);
        CellBeginEdit?.Invoke(this, beginArgs);
        if (beginArgs.Cancel) return;

        CellValidationResult? result = CellValidator?.Invoke(row, col, newValue);
        if (result is { IsValid: false })
        {
            RefuseToggle(row, col, column, item, currentValue, newValue, result);
            return;
        }

        if (!dataSource.TrySetItemValue(item, column, newValue, out var writeError))
        {
            RefuseToggle(row, col, column, item, currentValue, newValue,
                CellValidationResult.Error(writeError ?? "The value could not be saved."));
            return;
        }

        var modelResult = ValidateItem(item, column);
        if (modelResult is { IsValid: false })
        {
            dataSource.TrySetItemValue(item, column, currentValue, out _);
            RefuseToggle(row, col, column, item, currentValue, newValue, modelResult);
            return;
        }
        result = MoreSevere(result, modelResult);

        NeedsRedraw?.Invoke();
        CellEndEdit?.Invoke(this, new CellEndEditEventArgs(
            row, col, column, currentValue, newValue, committed: true));
        CellValueChanged?.Invoke(this, new CellValueChangedEventArgs(
            row, col, column, currentValue, newValue));
        if (result is { ErrorMessage: { Length: > 0 } })
            ValidationFailed?.Invoke(this, new CellValidationEventArgs(
                row, col, column, item, newValue, result, committed: true));
    }

    private void RefuseToggle(int row, int col, KumikoColumn column, object item, object? oldValue, object? attempted,
        CellValidationResult result)
    {
        ValidationFailed?.Invoke(this, new CellValidationEventArgs(
            row, col, column, item, attempted, result, committed: false));
        CellEndEdit?.Invoke(this, new CellEndEditEventArgs(
            row, col, column, oldValue, oldValue, committed: false));
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

        // Validation state: an outline, an icon, and the message under the editor
        if (CurrentValidation is { } validation && (!validation.IsValid || !string.IsNullOrEmpty(validation.ErrorMessage)))
            DrawValidation(ctx, bounds, validation);
    }

    /// <summary>The red (error) or orange (warning) outline, the "!" icon, and the message below the editor.</summary>
    private void DrawValidation(IDrawingContext ctx, KumikoRect bounds, CellValidationResult validation)
    {
        var color = !validation.IsValid && validation.Severity != ValidationSeverity.Warning
            ? new KumikoColor(220, 50, 50)    // red
            : new KumikoColor(255, 165, 0);   // orange

        ctx.DrawRect(bounds, new KumikoPaint
        {
            Color = color,
            Style = PaintStyle.Stroke,
            StrokeWidth = 2f,
            IsAntiAlias = true
        });

        const float iconRadius = 6f;
        var icon = new KumikoRect(bounds.Right - iconRadius * 2 - 4, bounds.Y + bounds.Height / 2 - iconRadius,
            iconRadius * 2, iconRadius * 2);
        ctx.FillRoundRect(icon, iconRadius, new KumikoPaint { Color = color, Style = PaintStyle.Fill, IsAntiAlias = true });
        var onColor = new KumikoPaint { Color = KumikoColor.White, Font = new KumikoFont("Default", 10, bold: true), IsAntiAlias = true };
        ctx.DrawTextInRect("!", icon, onColor, KumikoTextAlignment.Center, KumikoVerticalAlignment.Center);

        if (string.IsNullOrEmpty(validation.ErrorMessage)) return;
        var messagePaint = new KumikoPaint { Color = KumikoColor.White, Font = new KumikoFont("Default", 12), IsAntiAlias = true };
        float width = Math.Max(bounds.Width, ctx.MeasureText(validation.ErrorMessage, messagePaint).Width + 12);
        var message = new KumikoRect(bounds.X, bounds.Bottom + 2, width, 22);
        ctx.FillRoundRect(message, 3, new KumikoPaint { Color = color, Style = PaintStyle.Fill, IsAntiAlias = true });
        ctx.DrawTextInRect(validation.ErrorMessage, new KumikoRect(message.X + 6, message.Y, message.Width - 12, message.Height),
            messagePaint, KumikoTextAlignment.Left, KumikoVerticalAlignment.Center);
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
    /// Forward a pointer event to the active editor: a press, move, release or wheel scroll over it (its
    /// dropdown or calendar included). A press the editor handles captures the pointer: the moves and the
    /// release that follow go to the editor wherever they are, so a drag that starts in a dropdown list keeps
    /// scrolling it past its edge instead of panning the grid.
    /// Returns true if the editor handled it.
    /// </summary>
    public bool HandlePointerEvent(KumikoPointerEventArgs e)
    {
        if (!_isEditing || _activeEditor == null) return false;

        if (e.Action == InputAction.Pressed)
            _pointerCaptured = false;
        else if (_pointerCaptured && e.Action is InputAction.Moved or InputAction.Released or InputAction.Cancelled)
        {
            var editor = _activeEditor;
            if (e.Action == InputAction.Moved)
                editor.OnPointerMove(e);
            else
            {
                _pointerCaptured = false;
                if (e.Action == InputAction.Released)
                    editor.OnPointerUp(e);
            }
            return true;
        }

        // Check if click is inside editor
        if (_activeEditor.HitTest(e.X, e.Y))
        {
            bool handled = _focusManager.DispatchPointer(e);
            if (handled && e.Action == InputAction.Pressed && _isEditing)
                _pointerCaptured = true;
            return handled;
        }

        return false;
    }

    /// <summary>
    /// Whether the active editor is animating (a scroll picker coasting or snapping to an item). While it
    /// is, the host calls <see cref="UpdateAnimation"/> from its frame timer.
    /// </summary>
    public bool IsAnimating => _isEditing && _activeEditor is { IsAnimating: true };

    /// <summary>
    /// Advance the active editor's animation by one frame (<see cref="DrawnComponent.UpdateAnimation"/>).
    /// An editor may complete the edit when its animation settles (a scroll picker confirms the item it
    /// stops on), which commits it like any other completion.
    /// </summary>
    /// <param name="frameIntervalMs">Time since the previous frame, in milliseconds.</param>
    /// <returns>True while the editor is still animating.</returns>
    public bool UpdateAnimation(float frameIntervalMs = 16f)
    {
        if (!_isEditing || _activeEditor is not { } editor) return false;
        return editor.UpdateAnimation(frameIntervalMs) && _isEditing;
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

    /// <summary>The item's own validation of the edited property (the new value is already in place).</summary>
    private static CellValidationResult? ValidateItem(object item, KumikoColumn column)
    {
        string property = column.PropertyName;
        try
        {
            if (item is System.ComponentModel.IDataErrorInfo errorInfo &&
                errorInfo[property] is { Length: > 0 } error)
                return CellValidationResult.Error(error);

            if (item is System.ComponentModel.INotifyDataErrorInfo notifyErrorInfo && notifyErrorInfo.HasErrors)
            {
                foreach (var err in notifyErrorInfo.GetErrors(property) ?? Array.Empty<object>())
                {
                    if (err?.ToString() is { Length: > 0 } message)
                        return CellValidationResult.Error(message);
                }
            }
        }
        catch (Exception ex) when (ex is not (OutOfMemoryException or InsufficientExecutionStackException))
        {
            // An item whose validation throws is not blocked by it
        }

        return null;
    }

    private void EndEdit()
    {
        if (_editDataSource != null)
            _editDataSource.ViewChanged -= OnViewChanged;
        if (_activeEditor != null)
        {
            _activeEditor.RedrawRequested -= OnEditorRedrawRequested;
            _activeEditor.EditCompleted -= OnEditorEditCompleted;
            _activeEditor.ValueChanged -= OnEditorValueChanged;
            if (_activeEditor is DrawnTextBox textBox)
            {
                textBox.CopyRequested -= OnEditorCopyRequested;
                textBox.PasteRequested -= OnEditorPasteRequested;
            }
            // Blur first, then unregister: the editor loses focus properly and the manager keeps none
            _focusManager.ClearFocus();
            _focusManager.Unregister(_activeEditor);
            _activeEditor = null;
        }
        _isEditing = false;
        _editCell = CellPosition.Invalid;
        _editItem = null;
        _editColumn = null;
        _editDataSource = null;
        _originalValue = null;
        _warningShown = false;
        _warnedValue = null;
        _pointerCaptured = false;
        CurrentValidation = null;
        NeedsRedraw?.Invoke();
    }

    /// <summary>
    /// Called when an editor signals that its value is finalized
    /// (e.g. date picked, combo item selected). Auto-commits the edit.
    /// </summary>
    private void OnEditorRedrawRequested() => NeedsRedraw?.Invoke();

    private void OnViewChanged(object? sender, KumikoViewChangedEventArgs e) => ApplyViewChange(e);

    /// <summary>A changed value is a new attempt: the message shown for the previous one goes away.</summary>
    private void OnEditorValueChanged(object? sender, ComponentValueChangedEventArgs e)
    {
        if (CurrentValidation == null) return;
        CurrentValidation = null;
        NeedsRedraw?.Invoke();
    }

    private void OnEditorCopyRequested(object? sender, ClipboardTextEventArgs e) => ClipboardCopyRequested?.Invoke(this, e);

    private void OnEditorPasteRequested(object? sender, ClipboardRequestEventArgs e) => ClipboardPasteRequested?.Invoke(this, e);

    private void OnEditorEditCompleted()
    {
        if (!_isEditing || _editDataSource == null) return;
        CommitEdit(_editDataSource);
    }
}

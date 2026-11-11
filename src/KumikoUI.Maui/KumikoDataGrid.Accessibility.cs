using KumikoUI.Core.Editing;
using KumikoUI.Core.Models;

namespace KumikoUI.Maui;

/// <summary>
/// Accessibility quick wins (#77). The canvas is one opaque element to a screen reader, so the grid
/// exposes grid-level semantics (<see cref="GridDescription"/>, <see cref="GridHint"/>) and announces
/// meaningful state changes through <see cref="SemanticScreenReader.Announce"/>: the current cell, a sort
/// change, the start and end of an edit and a validation failure. The hidden keyboard-proxy entry is kept out
/// of the accessibility tree. A per-cell virtual accessibility tree is a separate piece of work.
/// </summary>
/// <remarks>
/// Announcements are built by Core's <see cref="KumikoAnnouncements"/> from <see cref="KumikoStrings"/> (set
/// <see cref="KumikoSource.Strings"/> on <see cref="DataSource"/> to localize them). They run on state change
/// events only, never in the render loop, and current-cell announcements are coalesced so that holding an
/// arrow key announces where it lands instead of every cell on the way.
/// </remarks>
public partial class KumikoDataGrid
{
    private const int CurrentCellAnnounceDelayMs = 250;

    private IDispatcherTimer? _announceTimer;

    // ── GridHint ──

    /// <summary>Identifies the <see cref="GridHint"/> bindable property.</summary>
    public static readonly BindableProperty GridHintProperty = BindableProperty.Create(
        nameof(GridHint), typeof(string), typeof(KumikoDataGrid), null,
        propertyChanged: (b, _, _) => ((KumikoDataGrid)b).UpdateAccessibilityHint());

    /// <summary>
    /// Accessible hint for screen readers. Null (the default) lets the grid describe itself: the row and
    /// column counts and the current cell, kept current as the data and the current cell change.
    /// </summary>
    public string? GridHint
    {
        get => (string?)GetValue(GridHintProperty);
        set => SetValue(GridHintProperty, value);
    }

    // ── AnnounceChanges ──

    /// <summary>Identifies the <see cref="AnnounceChanges"/> bindable property.</summary>
    public static readonly BindableProperty AnnounceChangesProperty = BindableProperty.Create(
        nameof(AnnounceChanges), typeof(bool), typeof(KumikoDataGrid), true);

    /// <summary>
    /// Whether the grid announces the current cell, sort changes, edits and validation errors to the
    /// platform screen reader. Default true; the announcements do nothing when no screen reader is running.
    /// </summary>
    public bool AnnounceChanges
    {
        get => (bool)GetValue(AnnounceChangesProperty);
        set => SetValue(AnnounceChangesProperty, value);
    }

    private void InitializeAccessibility()
    {
        // The keyboard proxy is plumbing for the software keyboard; a screen reader must not land on it.
        AutomationProperties.SetIsInAccessibleTree(_keyboardProxy, false);
        SemanticProperties.SetDescription(_keyboardProxy, string.Empty);

        AutomationProperties.SetIsInAccessibleTree(this, true);
        UpdateAccessibilityHint();
        SubscribeAccessibility();
    }

    private void SubscribeAccessibility()
    {
        _selection.CurrentCellChanged += OnAccessibilityCurrentCellChanged;
        _dataSource.SortColumnsChanged += OnAccessibilitySortChanged;
        _editSession.CellBeginEdit += OnAccessibilityCellBeginEdit;
        _editSession.CellEndEdit += OnAccessibilityCellEndEdit;
        _editSession.ValidationFailed += OnAccessibilityValidationFailed;
    }

    private void UnsubscribeAccessibility()
    {
        _selection.CurrentCellChanged -= OnAccessibilityCurrentCellChanged;
        _dataSource.SortColumnsChanged -= OnAccessibilitySortChanged;
        _editSession.CellBeginEdit -= OnAccessibilityCellBeginEdit;
        _editSession.CellEndEdit -= OnAccessibilityCellEndEdit;
        _editSession.ValidationFailed -= OnAccessibilityValidationFailed;
        if (_announceTimer != null)
        {
            _announceTimer.Stop();
            _announceTimer.Tick -= OnAnnounceTimerTick;
            _announceTimer = null;
        }
    }

    /// <summary>Applies the accessible description and hint from the properties or the localized defaults.</summary>
    private void UpdateAccessibilityHint()
    {
        var strings = _dataSource.Strings;
        SemanticProperties.SetDescription(this, GridDescription ?? strings.AccessibleGridName);
        SemanticProperties.SetHint(this, GridHint ?? KumikoAnnouncements.GridHint(
            strings, _dataSource.RowCount, _dataSource.DisplayColumns.Count, ToDisplayCell(_selection.CurrentCell)));
    }

    private void Announce(string message)
    {
        if (!AnnounceChanges || string.IsNullOrEmpty(message)) return;
        if (Dispatcher.IsDispatchRequired)
            Dispatcher.Dispatch(() => SemanticScreenReader.Announce(message));
        else
            SemanticScreenReader.Announce(message);
    }

    private void OnAccessibilityCurrentCellChanged(object? sender, EventArgs e)
    {
        UpdateAccessibilityHint();
        if (!AnnounceChanges || _editSession.IsEditing) return;

        // Coalesce: restart the timer on every move so only the cell the user stops on is read.
        _announceTimer ??= CreateAnnounceTimer();
        _announceTimer.Stop();
        _announceTimer.Start();
    }

    private IDispatcherTimer CreateAnnounceTimer()
    {
        var timer = Dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(CurrentCellAnnounceDelayMs);
        timer.IsRepeating = false;
        timer.Tick += OnAnnounceTimerTick;
        return timer;
    }

    private void OnAnnounceTimerTick(object? sender, EventArgs e)
    {
        if (_editSession.IsEditing) return;
        var cell = _selection.CurrentCell;
        if (!cell.IsValid || cell.Column >= _dataSource.Columns.Count || cell.Row >= _dataSource.RowCount) return;
        if (_dataSource.IsGroupHeaderRow(cell.Row)) return;

        var column = _dataSource.Columns[cell.Column];
        Announce(KumikoAnnouncements.CurrentCell(_dataSource.Strings, column, DisplayColumnIndex(cell.Column), cell.Row,
            _dataSource.GetCellDisplayText(cell.Row, column)));
    }

    private void OnAccessibilitySortChanged(object? sender, SortColumnsChangedEventArgs e)
    {
        int index = _dataSource.GetDisplayIndex(e.Column);
        Announce(KumikoAnnouncements.SortChanged(_dataSource.Strings, e.Column, index < 0 ? 0 : index));
    }

    private void OnAccessibilityCellBeginEdit(object? sender, CellBeginEditEventArgs e)
    {
        // Handlers added later may cancel the edit, so look once they have run.
        string text = _dataSource.FormatCellValue(e.CellValue, e.Column);
        int columnIndex = e.ColumnIndex;
        var column = e.Column;
        Dispatcher.Dispatch(() =>
        {
            if (_editSession.IsEditing)
                Announce(KumikoAnnouncements.EditStarted(_dataSource.Strings, column, DisplayColumnIndex(columnIndex), text));
        });
    }

    private void OnAccessibilityCellEndEdit(object? sender, CellEndEditEventArgs e)
    {
        string? text = e.Committed ? _dataSource.FormatCellValue(e.NewValue, e.Column) : null;
        Announce(KumikoAnnouncements.EditEnded(_dataSource.Strings, e.Column, DisplayColumnIndex(e.ColumnIndex), e.Committed, text));
    }

    private void OnAccessibilityValidationFailed(object? sender, CellValidationEventArgs e)
        => Announce(KumikoAnnouncements.ValidationFailed(
            _dataSource.Strings, e.Column, DisplayColumnIndex(e.ColumnIndex), e.Result.ErrorMessage));

    // Screen readers number columns as the user sees them: on-screen order, hidden columns not counted (#79).
    private int DisplayColumnIndex(int columnIndex)
    {
        int display = _dataSource.ToDisplayIndex(columnIndex);
        return display < 0 ? columnIndex : display;
    }

    private CellPosition ToDisplayCell(CellPosition cell)
    {
        if (!cell.IsValid) return cell;
        int display = _dataSource.ToDisplayIndex(cell.Column);
        return display < 0 ? CellPosition.Invalid : new CellPosition(cell.Row, display);
    }
}

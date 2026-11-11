using KumikoUI.Core.Editing;
using KumikoUI.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;

namespace KumikoUI.Uno;

/// <summary>
/// UI Automation support for <see cref="KumikoDataGrid"/> (#78). The grid is drawn on a canvas, so without a
/// peer Narrator and VoiceOver would see nothing; this exposes the grid as one DataGrid element with a
/// name (<see cref="GridDescription"/>) and a help text (<see cref="GridHint"/>), and announces meaningful state
/// changes through the peer's notification events: the current cell, a sort change, the start and end of an
/// edit and a validation failure. The hidden keyboard-proxy text box is marked Raw. A per-cell virtual
/// accessibility tree is a separate piece of work.
/// </summary>
/// <remarks>
/// Announcements are built by Core's <see cref="KumikoAnnouncements"/> from <see cref="KumikoStrings"/> (set
/// <see cref="KumikoSource.Strings"/> on <see cref="DataSource"/> to localize them). They run on state change
/// events only, never in the render loop, only once assistive technology has asked for the peer, and
/// current-cell announcements are coalesced so that holding an arrow key announces where it lands.
/// </remarks>
public partial class KumikoDataGrid
{
    private const int CurrentCellAnnounceDelayMs = 250;

    private DispatcherTimer? _announceTimer;
    private string _lastAccessibilityHint = string.Empty;

    // ── GridHint ──

    /// <summary>Identifies the <see cref="GridHint"/> dependency property.</summary>
    public static readonly DependencyProperty GridHintProperty =
        DependencyProperty.Register(
            nameof(GridHint), typeof(string), typeof(KumikoDataGrid),
            new PropertyMetadata(null, (d, _) => ((KumikoDataGrid)d).RaiseAccessibilityHintChanged()));

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

    /// <summary>Identifies the <see cref="AnnounceChanges"/> dependency property.</summary>
    public static readonly DependencyProperty AnnounceChangesProperty =
        DependencyProperty.Register(
            nameof(AnnounceChanges), typeof(bool), typeof(KumikoDataGrid), new PropertyMetadata(true));

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
        _dataSource.ViewChanged += OnAccessibilityViewChanged;
        _selection.CurrentCellChanged += OnAccessibilityCurrentCellChanged;
        _dataSource.SortColumnsChanged += OnAccessibilitySortChanged;
        _editSession.CellBeginEdit += OnAccessibilityCellBeginEdit;
        _editSession.CellEndEdit += OnAccessibilityCellEndEdit;
        _editSession.ValidationFailed += OnAccessibilityValidationFailed;
        Unloaded += (_, _) => StopAnnounceTimer();
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new KumikoDataGridAutomationPeer(this);

    /// <summary>The accessible name: <see cref="GridDescription"/> or the localized default.</summary>
    internal string GetAccessibleName() => GridDescription ?? _dataSource.Strings.AccessibleGridName;

    /// <summary>
    /// The text a screen reader announces after the name: <see cref="GridHint"/>, or the size of the grid and
    /// its current cell.
    /// </summary>
    internal string GetAccessibilityHint() => GridHint ?? KumikoAnnouncements.GridHint(
        _dataSource.Strings, _dataSource.RowCount, _dataSource.Columns.Count, _selection.CurrentCell);

    /// <summary>Mirrors an explicit <see cref="GridDescription"/> into AutomationProperties.Name; null clears it.</summary>
    private void UpdateAccessibleName()
    {
        if (GridDescription is { } name)
            AutomationProperties.SetName(this, name);
        else
            ClearValue(AutomationProperties.NameProperty);
    }

    private AutomationPeer? ExistingPeer => FrameworkElementAutomationPeer.FromElement(this);

    private void RaiseAccessibilityHintChanged()
    {
        string hint = GetAccessibilityHint();
        if (hint == _lastAccessibilityHint) return;
        string previous = _lastAccessibilityHint;
        _lastAccessibilityHint = hint;
        // Only when assistive technology has asked for the peer; creating it here would cost every grid.
        ExistingPeer?.RaisePropertyChangedEvent(AutomationElementIdentifiers.HelpTextProperty, previous, hint);
    }

    /// <summary>
    /// Raises a notification through the grid's peer, only when it exists and <see cref="AnnounceChanges"/> is on.
    /// </summary>
    private void Announce(string message, bool important)
    {
        if (!AnnounceChanges || string.IsNullOrEmpty(message)) return;
        ExistingPeer?.RaiseNotificationEvent(
            important ? AutomationNotificationKind.ActionCompleted : AutomationNotificationKind.Other,
            important ? AutomationNotificationProcessing.ImportantMostRecent : AutomationNotificationProcessing.MostRecent,
            message,
            "KumikoDataGrid");
    }

    private void OnAccessibilityViewChanged(object? sender, KumikoViewChangedEventArgs e) => RaiseAccessibilityHintChanged();

    private void OnAccessibilityCurrentCellChanged(object? sender, EventArgs e)
    {
        RaiseAccessibilityHintChanged();
        if (!AnnounceChanges || _editSession.IsEditing) return;

        // Coalesce: restart the timer on every move so only the cell the user stops on is read.
        if (_announceTimer == null)
        {
            _announceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(CurrentCellAnnounceDelayMs) };
            _announceTimer.Tick += OnAnnounceTimerTick;
        }
        _announceTimer.Stop();
        _announceTimer.Start();
    }

    private void StopAnnounceTimer()
    {
        if (_announceTimer == null) return;
        _announceTimer.Stop();
        _announceTimer.Tick -= OnAnnounceTimerTick;
        _announceTimer = null;
    }

    private void OnAnnounceTimerTick(object? sender, object e)
    {
        _announceTimer?.Stop();
        if (_editSession.IsEditing) return;
        var cell = _selection.CurrentCell;
        if (!cell.IsValid || cell.Column >= _dataSource.Columns.Count || cell.Row >= _dataSource.RowCount) return;
        if (_dataSource.IsGroupHeaderRow(cell.Row)) return;

        var column = _dataSource.Columns[cell.Column];
        Announce(KumikoAnnouncements.CurrentCell(_dataSource.Strings, column, cell.Column, cell.Row,
            _dataSource.GetCellDisplayText(cell.Row, column)), important: false);
    }

    private void OnAccessibilitySortChanged(object? sender, SortColumnsChangedEventArgs e)
    {
        int index = _dataSource.Columns.ToList().IndexOf(e.Column);
        Announce(KumikoAnnouncements.SortChanged(_dataSource.Strings, e.Column, index < 0 ? 0 : index), important: true);
    }

    private void OnAccessibilityCellBeginEdit(object? sender, CellBeginEditEventArgs e)
    {
        // Handlers added later may cancel the edit, so look once they have run.
        string text = _dataSource.FormatCellValue(e.CellValue, e.Column);
        int columnIndex = e.ColumnIndex;
        var column = e.Column;
        void AnnounceIfEditing()
        {
            if (_editSession.IsEditing)
                Announce(KumikoAnnouncements.EditStarted(_dataSource.Strings, column, columnIndex, text), important: true);
        }
        if (DispatcherQueue is { } queue) queue.TryEnqueue(AnnounceIfEditing);
        else AnnounceIfEditing();
    }

    private void OnAccessibilityCellEndEdit(object? sender, CellEndEditEventArgs e)
    {
        string? text = e.Committed ? _dataSource.FormatCellValue(e.NewValue, e.Column) : null;
        Announce(KumikoAnnouncements.EditEnded(_dataSource.Strings, e.Column, e.ColumnIndex, e.Committed, text), important: true);
    }

    private void OnAccessibilityValidationFailed(object? sender, CellValidationEventArgs e)
        => Announce(KumikoAnnouncements.ValidationFailed(
            _dataSource.Strings, e.Column, e.ColumnIndex, e.Result.ErrorMessage), important: true);
}

/// <summary>The automation peer of <see cref="KumikoDataGrid"/>: a DataGrid element named by <c>GridDescription</c>.</summary>
internal sealed class KumikoDataGridAutomationPeer : FrameworkElementAutomationPeer
{
    private readonly KumikoDataGrid _grid;

    public KumikoDataGridAutomationPeer(KumikoDataGrid owner) : base(owner) => _grid = owner;

    protected override string GetClassNameCore() => nameof(KumikoDataGrid);

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.DataGrid;

    protected override string GetNameCore()
    {
        // An app-supplied AutomationProperties.Name wins (it also holds an explicit GridDescription).
        string name = base.GetNameCore();
        return string.IsNullOrEmpty(name) ? _grid.GetAccessibleName() : name;
    }

    protected override string GetHelpTextCore()
    {
        // An app-supplied AutomationProperties.HelpText wins over the computed hint.
        string help = base.GetHelpTextCore();
        return string.IsNullOrEmpty(help) ? _grid.GetAccessibilityHint() : help;
    }
}

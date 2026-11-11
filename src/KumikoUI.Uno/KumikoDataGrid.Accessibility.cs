using KumikoUI.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;

namespace KumikoUI.Uno;

/// <summary>
/// UI Automation support for <see cref="KumikoDataGrid"/>. The grid is drawn on a canvas, so without a
/// peer Narrator and VoiceOver would see nothing; this exposes the grid as one DataGrid element with a
/// name and a help text. The cells are not individually exposed (a virtual accessibility tree is a
/// separate piece of work).
/// </summary>
public partial class KumikoDataGrid
{
    private string _lastAccessibilityHint = string.Empty;

    /// <summary>
    /// Sets the default accessible name (the <see cref="GridDescription"/> default is otherwise only applied
    /// when the property changes) and keeps the help text current as the data or the current cell changes.
    /// </summary>
    private void InitializeAccessibility()
    {
        AutomationProperties.SetName(this, GridDescription);
        _dataSource.ViewChanged += OnAccessibilityViewChanged;
        _selection.CurrentCellChanged += OnAccessibilityCurrentCellChanged;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new KumikoDataGridAutomationPeer(this);

    /// <summary>
    /// Gets the text a screen reader announces after the name: the size of the grid and its current cell,
    /// from <see cref="KumikoSource.Strings"/>.
    /// </summary>
    internal string GetAccessibilityHint()
    {
        var cell = _selection.CurrentCell;
        return _dataSource.Strings.FormatGridHint(
            _dataSource.RowCount, _dataSource.Columns.Count,
            cell.IsValid ? cell.Row : -1, cell.IsValid ? cell.Column : -1);
    }

    private void OnAccessibilityViewChanged(object? sender, KumikoViewChangedEventArgs e) => RaiseAccessibilityHintChanged();

    private void OnAccessibilityCurrentCellChanged(object? sender, EventArgs e) => RaiseAccessibilityHintChanged();

    private void RaiseAccessibilityHintChanged()
    {
        string hint = GetAccessibilityHint();
        if (hint == _lastAccessibilityHint) return;
        string previous = _lastAccessibilityHint;
        _lastAccessibilityHint = hint;
        // Only when assistive technology has asked for the peer; creating it here would cost every grid.
        if (FrameworkElementAutomationPeer.FromElement(this) is { } peer)
            peer.RaisePropertyChangedEvent(AutomationElementIdentifiers.HelpTextProperty, previous, hint);
    }
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
        // AutomationProperties.Name wins (it holds GridDescription unless the app overrode it).
        string name = base.GetNameCore();
        return string.IsNullOrEmpty(name) ? _grid.GridDescription : name;
    }

    protected override string GetHelpTextCore()
    {
        // An app-supplied AutomationProperties.HelpText wins over the computed hint.
        string help = base.GetHelpTextCore();
        return string.IsNullOrEmpty(help) ? _grid.GetAccessibilityHint() : help;
    }
}

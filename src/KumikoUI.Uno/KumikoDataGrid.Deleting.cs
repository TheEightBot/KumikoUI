using System.Windows.Input;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using Microsoft.UI.Xaml;

namespace KumikoUI.Uno;

/// <summary>
/// Deleting rows (#328): <see cref="AllowDeleting"/> lets the Delete key (Command+Backspace on Apple keyboards) remove
/// the selected rows from <c>ItemsSource</c>, <see cref="DeleteSelectedRows"/> does the same from code, and
/// <see cref="RowsDeleting"/> (or <see cref="RowsDeletingCommand"/>) can cancel it. The flow lives in Core
/// (<see cref="KumikoInputController.DeleteSelectedRows"/>); this file is the dependency-property surface.
/// </summary>
public partial class KumikoDataGrid
{
    /// <summary>Runs <see cref="RowsDeletingCommand"/> as the first listener of the controller's event.</summary>
    private void InitializeDeleting() => _inputController.RowsDeleting += OnRowsDeletingForCommand;

    // ── AllowDeleting ──

    /// <summary>Identifies the <see cref="AllowDeleting"/> dependency property.</summary>
    public static readonly DependencyProperty AllowDeletingProperty =
        DependencyProperty.Register(
            nameof(AllowDeleting), typeof(bool), typeof(KumikoDataGrid),
            new PropertyMetadata(false, (d, e) => ((KumikoDataGrid)d)._inputController.AllowDeleting = (bool)e.NewValue));

    /// <summary>
    /// Whether the Delete key (on Apple keyboards also Command+Backspace) removes the selected rows from
    /// <c>ItemsSource</c> while no cell editor is open. It does nothing in a read-only grid, and without a selected
    /// row the key stays with the page. <see cref="RowsDeleting"/> is raised first and can cancel the delete.
    /// <see cref="DeleteSelectedRows"/> works whatever this is. Default: false.
    /// </summary>
    public bool AllowDeleting
    {
        get => (bool)GetValue(AllowDeletingProperty);
        set => SetValue(AllowDeletingProperty, value);
    }

    // ── Events and command ──

    /// <summary>
    /// Raised before the selected rows are removed, by the Delete key or <see cref="DeleteSelectedRows"/> (an open edit
    /// is committed first). Set <see cref="KumikoRowsDeletingEventArgs.Cancel"/> to keep the rows. The event is
    /// synchronous: to confirm with a dialog, cancel here, show the dialog, then call
    /// <see cref="KumikoSource.RemoveItems"/> on <see cref="DataSource"/> with <see cref="KumikoRowsDeletingEventArgs.Items"/>
    /// yourself. It re-surfaces <see cref="KumikoInputController.RowsDeleting"/>.
    /// </summary>
    public event EventHandler<KumikoRowsDeletingEventArgs>? RowsDeleting
    {
        add => _inputController.RowsDeleting += value;
        remove => _inputController.RowsDeleting -= value;
    }

    /// <summary>
    /// Raised once after the grid removed the rows (or could not: <see cref="KumikoRowsDeletedEventArgs.Result"/>), when
    /// the current cell and the selection have already moved to the row that took the first deleted row's place. It
    /// re-surfaces <see cref="KumikoInputController.RowsDeleted"/>.
    /// </summary>
    public event EventHandler<KumikoRowsDeletedEventArgs>? RowsDeleted
    {
        add => _inputController.RowsDeleted += value;
        remove => _inputController.RowsDeleted -= value;
    }

    /// <summary>Identifies the <see cref="RowsDeletingCommand"/> dependency property.</summary>
    public static readonly DependencyProperty RowsDeletingCommandProperty =
        DependencyProperty.Register(
            nameof(RowsDeletingCommand), typeof(ICommand), typeof(KumikoDataGrid), new PropertyMetadata(null));

    /// <summary>
    /// Executed, with the <see cref="KumikoRowsDeletingEventArgs"/> as its parameter, when rows are about to be
    /// deleted, before the <see cref="RowsDeleting"/> handlers you add run: the command can set
    /// <see cref="KumikoRowsDeletingEventArgs.Cancel"/> to keep the rows (the command is synchronous, so a view model
    /// that asks the user first cancels and deletes later through <see cref="KumikoSource.RemoveItems"/>). Skipped when its
    /// <c>CanExecute</c> says no.
    /// </summary>
    public ICommand? RowsDeletingCommand
    {
        get => (ICommand?)GetValue(RowsDeletingCommandProperty);
        set => SetValue(RowsDeletingCommandProperty, value);
    }

    private void OnRowsDeletingForCommand(object? sender, KumikoRowsDeletingEventArgs e)
    {
        var command = RowsDeletingCommand;
        if (command != null && command.CanExecute(e)) command.Execute(e);
    }

    // ── DeleteSelectedRows ──

    /// <summary>
    /// Removes the selected rows' items from <c>ItemsSource</c>, whatever <see cref="AllowDeleting"/> says: an open
    /// edit is committed first (a refused commit stops the delete), then <see cref="RowsDeleting"/> is raised and, unless
    /// canceled, the items are removed with <see cref="KumikoSource.RemoveItems"/> and <see cref="RowsDeleted"/> is
    /// raised. The current cell (same column) and the selection move to the row that took the first deleted row's
    /// place, and the deletion is announced to the screen reader. Group header and summary rows are never deleted.
    /// </summary>
    /// <returns>Whether items were removed: false with no selected row, a refused commit, a canceled event or an
    /// <c>ItemsSource</c> that cannot be changed (an array, a read-only list, a plain <c>IEnumerable</c>).</returns>
    public bool DeleteSelectedRows()
    {
        bool deleted = _inputController.DeleteSelectedRows(_scroll, _selection, _style, _dataSource);
        Invalidate();
        return deleted;
    }
}

namespace KumikoUI.Core.Models;

/// <summary>
/// Event arguments raised when the grid selection changes.
/// </summary>
public class DataGridSelectionChangedEventArgs : EventArgs
{
    /// <summary>List of currently selected visible row indices.</summary>
    public IReadOnlyList<int> SelectedRows { get; }
    /// <summary>The currently focused cell position.</summary>
    public CellPosition CurrentCell { get; }

    public DataGridSelectionChangedEventArgs(IReadOnlyList<int> selectedRows, CellPosition currentCell)
    {
        SelectedRows = selectedRows;
        CurrentCell = currentCell;
    }
}

namespace KumikoUI.Core.Models;

/// <summary>
/// Event arguments raised when items are added to or removed from the grid selection.
/// </summary>
public sealed class KumikoSelectionChangedEventArgs : EventArgs
{
    /// <summary>Creates the args.</summary>
    public KumikoSelectionChangedEventArgs(IReadOnlyList<object> addedItems, IReadOnlyList<object> removedItems,
        IReadOnlyList<int> selectedRows, CellPosition currentCell)
    {
        AddedItems = addedItems;
        RemovedItems = removedItems;
        SelectedRows = selectedRows;
        CurrentCell = currentCell;
    }

    /// <summary>The items that joined the selection.</summary>
    public IReadOnlyList<object> AddedItems { get; }

    /// <summary>The items that left the selection (deselected, or no longer visible).</summary>
    public IReadOnlyList<object> RemovedItems { get; }

    /// <summary>The visible rows of all selected items after the change, in ascending order.</summary>
    public IReadOnlyList<int> SelectedRows { get; }

    /// <summary>The current cell after the change.</summary>
    public CellPosition CurrentCell { get; }
}

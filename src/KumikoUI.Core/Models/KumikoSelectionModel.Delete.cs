namespace KumikoUI.Core.Models;

// Moving the selection after rows were deleted (#328).
public sealed partial class KumikoSelectionModel
{
    /// <summary>
    /// Makes <paramref name="item"/> the only selected item and the current item, in <paramref name="column"/>, and
    /// the anchor of the next Shift range. The item may sit in a view that is about to be rebuilt: the current cell
    /// follows it through that view change. Without a selection mode that allows a selection the item only becomes
    /// current.
    /// </summary>
    internal void MoveToItem(object item, int column)
    {
        int row = Source.IndexOfRowKey(item);
        SetSelectedItems(new[] { item });
        SetCurrentCell(row, column);
        _anchorKey = row >= 0 ? item : null;
        _anchorRow = row;
    }
}

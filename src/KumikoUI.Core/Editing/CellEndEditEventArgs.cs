using KumikoUI.Core.Input;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Editing;

/// <summary>
/// Event args for CellEndEdit.
/// </summary>
public class CellEndEditEventArgs : EventArgs
{
    /// <summary>Zero-based row index of the cell.</summary>
    public int RowIndex { get; }

    /// <summary>Zero-based column index of the cell.</summary>
    public int ColumnIndex { get; }

    /// <summary>Column definition for the cell.</summary>
    public DataGridColumn Column { get; }

    /// <summary>Cell value before editing.</summary>
    public object? OldValue { get; }

    /// <summary>Cell value after editing.</summary>
    public object? NewValue { get; }

    /// <summary>True if the edit was committed; false if cancelled.</summary>
    public bool Committed { get; }

    public CellEndEditEventArgs(int row, int col, DataGridColumn column,
        object? oldValue, object? newValue, bool committed)
    {
        RowIndex = row;
        ColumnIndex = col;
        Column = column;
        OldValue = oldValue;
        NewValue = newValue;
        Committed = committed;
    }
}

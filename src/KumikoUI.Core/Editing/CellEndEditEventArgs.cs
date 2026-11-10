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
    public KumikoColumn Column { get; }

    /// <summary>Cell value before editing.</summary>
    public object? OldValue { get; }

    /// <summary>Cell value after editing.</summary>
    public object? NewValue { get; }

    /// <summary>True if the edit was committed; false if cancelled.</summary>
    public bool Committed { get; }

    /// <summary>Initializes the event data.</summary>
    /// <param name="row">Zero-based row index of the cell.</param>
    /// <param name="col">Zero-based column index of the cell.</param>
    /// <param name="column">Column definition for the cell.</param>
    /// <param name="oldValue">Cell value before editing.</param>
    /// <param name="newValue">Cell value after editing.</param>
    /// <param name="committed">True if the edit was committed; false if canceled.</param>
    public CellEndEditEventArgs(int row, int col, KumikoColumn column,
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

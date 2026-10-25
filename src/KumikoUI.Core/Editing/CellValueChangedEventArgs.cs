using KumikoUI.Core.Input;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Editing;

/// <summary>
/// Event args for CellValueChanged.
/// </summary>
public class CellValueChangedEventArgs : EventArgs
{
    /// <summary>Zero-based row index of the changed cell.</summary>
    public int RowIndex { get; }

    /// <summary>Zero-based column index of the changed cell.</summary>
    public int ColumnIndex { get; }

    /// <summary>Column definition for the changed cell.</summary>
    public KumikoColumn Column { get; }

    /// <summary>Previous cell value.</summary>
    public object? OldValue { get; }

    /// <summary>New cell value.</summary>
    public object? NewValue { get; }

    public CellValueChangedEventArgs(int row, int col, KumikoColumn column,
        object? oldValue, object? newValue)
    {
        RowIndex = row;
        ColumnIndex = col;
        Column = column;
        OldValue = oldValue;
        NewValue = newValue;
    }
}

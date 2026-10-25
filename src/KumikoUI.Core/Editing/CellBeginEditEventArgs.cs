using KumikoUI.Core.Input;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Editing;

/// <summary>
/// Event args for CellBeginEdit. Set Cancel=true to prevent editing.
/// </summary>
public class CellBeginEditEventArgs : EventArgs
{
    /// <summary>Zero-based row index of the cell.</summary>
    public int RowIndex { get; }

    /// <summary>Zero-based column index of the cell.</summary>
    public int ColumnIndex { get; }

    /// <summary>Column definition for the cell.</summary>
    public KumikoColumn Column { get; }

    /// <summary>Current value of the cell before editing.</summary>
    public object? CellValue { get; }

    /// <summary>Set to true to cancel the edit operation.</summary>
    public bool Cancel { get; set; }

    public CellBeginEditEventArgs(int row, int col, KumikoColumn column, object? value)
    {
        RowIndex = row;
        ColumnIndex = col;
        Column = column;
        CellValue = value;
    }
}

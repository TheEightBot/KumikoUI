using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Components;

namespace KumikoUI.Core.Input;

/// <summary>Provides data when a column auto-fit is requested (e.g., double-click on a column resize handle).</summary>
public sealed class AutoFitColumnEventArgs : EventArgs
{
    /// <summary>Zero-based index of the column to auto-fit.</summary>
    public int ColumnIndex { get; }
    /// <summary>The column to auto-fit.</summary>
    public KumikoColumn Column { get; }
    /// <summary>Initializes a new instance of the <see cref="AutoFitColumnEventArgs"/> class.</summary>
    /// <param name="columnIndex">The zero-based column index.</param>
    /// <param name="column">The column to auto-fit.</param>
    public AutoFitColumnEventArgs(int columnIndex, KumikoColumn column)
    {
        ColumnIndex = columnIndex;
        Column = column;
    }
}

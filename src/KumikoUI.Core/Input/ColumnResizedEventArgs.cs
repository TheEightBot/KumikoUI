using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Components;

namespace KumikoUI.Core.Input;

/// <summary>Provides data for the <see cref="GridInputController.ColumnResized"/> event.</summary>
public class ColumnResizedEventArgs : EventArgs
{
    /// <summary>Zero-based index of the resized column.</summary>
    public int ColumnIndex { get; }
    /// <summary>The column that was resized.</summary>
    public DataGridColumn Column { get; }
    /// <summary>Initializes a new instance of the <see cref="ColumnResizedEventArgs"/> class.</summary>
    /// <param name="index">The zero-based column index.</param>
    /// <param name="column">The resized column.</param>
    public ColumnResizedEventArgs(int index, DataGridColumn column) { ColumnIndex = index; Column = column; }
}

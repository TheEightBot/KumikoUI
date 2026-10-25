using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Components;

namespace KumikoUI.Core.Input;

/// <summary>Provides data for the <see cref="GridInputController.ColumnReordered"/> event.</summary>
public class GridColumnReorderedEventArgs : EventArgs
{
    /// <summary>Original zero-based index of the column before it was moved.</summary>
    public int OldIndex { get; }
    /// <summary>New zero-based index of the column after it was moved.</summary>
    public int NewIndex { get; }
    /// <summary>The column that was reordered.</summary>
    public DataGridColumn Column { get; }
    /// <summary>Initializes a new instance of the <see cref="GridColumnReorderedEventArgs"/> class.</summary>
    /// <param name="oldIndex">The original column index.</param>
    /// <param name="newIndex">The new column index.</param>
    /// <param name="column">The reordered column.</param>
    public GridColumnReorderedEventArgs(int oldIndex, int newIndex, DataGridColumn column)
    {
        OldIndex = oldIndex; NewIndex = newIndex; Column = column;
    }
}

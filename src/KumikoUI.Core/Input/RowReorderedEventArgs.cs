using KumikoUI.Core.Layout;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Components;

namespace KumikoUI.Core.Input;

/// <summary>Provides data for the <see cref="GridInputController.RowReordered"/> event.</summary>
public class RowReorderedEventArgs : EventArgs
{
    /// <summary>Original zero-based index of the row before it was moved.</summary>
    public int OldIndex { get; }
    /// <summary>New zero-based index of the row after it was moved.</summary>
    public int NewIndex { get; }
    /// <summary>The data item associated with the reordered row.</summary>
    public object? DataItem { get; }
    /// <summary>Initializes a new instance of the <see cref="RowReorderedEventArgs"/> class.</summary>
    /// <param name="oldIndex">The original row index.</param>
    /// <param name="newIndex">The new row index.</param>
    /// <param name="dataItem">The data item of the reordered row.</param>
    public RowReorderedEventArgs(int oldIndex, int newIndex, object? dataItem)
    {
        OldIndex = oldIndex; NewIndex = newIndex; DataItem = dataItem;
    }
}

using KumikoUI.Core.Layout;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Components;

namespace KumikoUI.Core.Input;

/// <summary>Provides data for the <see cref="GridInputController.RowTapped"/> and <see cref="GridInputController.RowDoubleTapped"/> events.</summary>
public class RowTappedEventArgs : EventArgs
{
    /// <summary>Zero-based index of the tapped row.</summary>
    public int RowIndex { get; }
    /// <summary>Zero-based index of the tapped column.</summary>
    public int ColumnIndex { get; }
    /// <summary>The data item associated with the tapped row.</summary>
    public object DataItem { get; }
    /// <summary>Initializes a new instance of the <see cref="RowTappedEventArgs"/> class.</summary>
    /// <param name="row">The tapped row index.</param>
    /// <param name="col">The tapped column index.</param>
    /// <param name="item">The row's data item.</param>
    public RowTappedEventArgs(int row, int col, object item)
    {
        RowIndex = row; ColumnIndex = col; DataItem = item;
    }
}

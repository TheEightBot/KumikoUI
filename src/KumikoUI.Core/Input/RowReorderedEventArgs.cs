using KumikoUI.Core.Models;

namespace KumikoUI.Core.Input;

/// <summary>
/// Provides data for the <see cref="KumikoInputController.RowReordered"/> and
/// <see cref="KumikoInputController.RowReorderRejected"/> events.
/// </summary>
public sealed class RowReorderedEventArgs : EventArgs
{
    /// <summary>Original zero-based visible index of the row before it was moved.</summary>
    public int OldIndex { get; }
    /// <summary>
    /// The zero-based visible index the row was dropped at: where it is after the move (the rows between shift by
    /// one), or where it would have gone when the move was rejected.
    /// </summary>
    public int NewIndex { get; }
    /// <summary>The data item associated with the reordered row.</summary>
    public object? DataItem { get; }
    /// <summary>
    /// What <see cref="KumikoSource.ReorderRow"/> did with the drop: <see cref="KumikoReorderResult.Moved"/> for
    /// <see cref="KumikoInputController.RowReordered"/>, the reason the rows did not move for
    /// <see cref="KumikoInputController.RowReorderRejected"/>.
    /// </summary>
    public KumikoReorderResult Result { get; }
    /// <summary>Initializes a new instance of the <see cref="RowReorderedEventArgs"/> class for a row that moved.</summary>
    /// <param name="oldIndex">The original row index.</param>
    /// <param name="newIndex">The new row index.</param>
    /// <param name="dataItem">The data item of the reordered row.</param>
    public RowReorderedEventArgs(int oldIndex, int newIndex, object? dataItem)
        : this(oldIndex, newIndex, dataItem, KumikoReorderResult.Moved)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RowReorderedEventArgs"/> class.</summary>
    /// <param name="oldIndex">The original row index.</param>
    /// <param name="newIndex">The row index the row was dropped at.</param>
    /// <param name="dataItem">The data item of the dragged row.</param>
    /// <param name="result">What the reorder did.</param>
    public RowReorderedEventArgs(int oldIndex, int newIndex, object? dataItem, KumikoReorderResult result)
    {
        OldIndex = oldIndex; NewIndex = newIndex; DataItem = dataItem; Result = result;
    }
}

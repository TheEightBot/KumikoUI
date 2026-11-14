using KumikoUI.Core.Models;

namespace KumikoUI.Core.Input;

/// <summary>
/// Provides data for the <see cref="KumikoInputController.ColumnReordered"/> event and the hosts'
/// <c>ColumnReordered</c> event.
/// </summary>
/// <remarks>
/// Both indices are positions in <see cref="KumikoSource.Columns"/> (the order the columns were defined in, hidden
/// columns included), not on-screen positions. The move is "remove at <see cref="OldIndex"/>, then insert at
/// <see cref="NewIndex"/>", which is what <see cref="KumikoSource.ReorderColumn"/> does, so <see cref="NewIndex"/> is
/// the column's index after the move. For the on-screen position use <see cref="KumikoSource.GetDisplayIndex"/>.
/// The drop slot is computed within the column's own pane (left-frozen, scrolling or right-frozen) from the on-screen
/// boundaries of the pane's other columns: the column lands before the first of them whose middle is right of the
/// pointer.
/// </remarks>
public sealed class KumikoColumnReorderedEventArgs : EventArgs
{
    /// <summary>The column's index in <see cref="KumikoSource.Columns"/> before the move.</summary>
    public int OldIndex { get; }
    /// <summary>The column's index in <see cref="KumikoSource.Columns"/> after the move (see the remarks).</summary>
    public int NewIndex { get; }
    /// <summary>The column that was reordered.</summary>
    public KumikoColumn Column { get; }
    /// <summary>Initializes a new instance of the <see cref="KumikoColumnReorderedEventArgs"/> class.</summary>
    /// <param name="oldIndex">The column's index in <see cref="KumikoSource.Columns"/> before the move.</param>
    /// <param name="newIndex">The column's index in <see cref="KumikoSource.Columns"/> after the move.</param>
    /// <param name="column">The reordered column.</param>
    public KumikoColumnReorderedEventArgs(int oldIndex, int newIndex, KumikoColumn column)
    {
        OldIndex = oldIndex; NewIndex = newIndex; Column = column;
    }
}

namespace KumikoUI.Core.Models;

/// <summary>
/// Provides data for <see cref="KumikoSource.ColumnsChanged"/>: which column was shown, hidden, frozen or moved, or
/// that several changed at once.
/// </summary>
public sealed class KumikoColumnsChangedEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="KumikoColumnsChangedEventArgs"/> class.</summary>
    /// <param name="kind">What changed.</param>
    /// <param name="column">The column that changed; null for <see cref="KumikoColumnsChangeKind.Reset"/>.</param>
    /// <param name="oldIndex">For <see cref="KumikoColumnsChangeKind.Order"/>, the column's index in
    /// <see cref="KumikoSource.Columns"/> before the move; otherwise -1.</param>
    /// <param name="newIndex">For <see cref="KumikoColumnsChangeKind.Order"/>, the column's index after the move;
    /// otherwise -1.</param>
    public KumikoColumnsChangedEventArgs(KumikoColumnsChangeKind kind, KumikoColumn? column, int oldIndex = -1, int newIndex = -1)
    {
        Kind = kind;
        Column = column;
        OldIndex = oldIndex;
        NewIndex = newIndex;
    }

    /// <summary>What changed.</summary>
    public KumikoColumnsChangeKind Kind { get; }

    /// <summary>The column that was shown, hidden, frozen or moved; null for <see cref="KumikoColumnsChangeKind.Reset"/>.</summary>
    public KumikoColumn? Column { get; }

    /// <summary>For <see cref="KumikoColumnsChangeKind.Order"/>, the column's index in <see cref="KumikoSource.Columns"/> before the move; otherwise -1.</summary>
    public int OldIndex { get; }

    /// <summary>For <see cref="KumikoColumnsChangeKind.Order"/>, the column's index in <see cref="KumikoSource.Columns"/> after the move; otherwise -1.</summary>
    public int NewIndex { get; }
}

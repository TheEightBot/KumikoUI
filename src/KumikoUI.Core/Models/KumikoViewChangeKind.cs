namespace KumikoUI.Core.Models;

/// <summary>What a <see cref="KumikoSource.ViewChanged"/> notification changed.</summary>
public enum KumikoViewChangeKind
{
    /// <summary>
    /// Only how rows look changed (a cell value, a column reorder, the frozen row count). Every visible row
    /// still shows the same item at the same index.
    /// </summary>
    Redraw,

    /// <summary>
    /// The visible rows were rebuilt (sort, filter, grouping, summaries, new items or columns, or several
    /// changes batched with <see cref="KumikoSource.BeginUpdate"/>). Use
    /// <see cref="KumikoViewChangedEventArgs.MapRow"/> to find where a row went.
    /// </summary>
    Rebuild,

    /// <summary>
    /// <see cref="KumikoViewChangedEventArgs.Count"/> rows were inserted at
    /// <see cref="KumikoViewChangedEventArgs.ViewIndex"/>; rows at or after it moved down.
    /// </summary>
    Insert,

    /// <summary>
    /// <see cref="KumikoViewChangedEventArgs.Count"/> rows were removed at
    /// <see cref="KumikoViewChangedEventArgs.ViewIndex"/>; rows after them moved up.
    /// </summary>
    Remove,
}

namespace KumikoUI.Core.Models;

/// <summary>
/// The outcome of <see cref="KumikoSource.ReorderRow"/>, also reported for a dropped row drag by
/// <see cref="Input.RowReorderedEventArgs.Result"/>.
/// </summary>
public enum KumikoReorderResult
{
    /// <summary>The item moved in the underlying collection and the view was rebuilt once.</summary>
    Moved,

    /// <summary>The row is already at the target position; nothing changed.</summary>
    NoOp,

    /// <summary>
    /// An index is outside the rows, or names a group header or group summary row rather than a data row; nothing
    /// changed.
    /// </summary>
    OutOfRange,

    /// <summary>
    /// A column sort is active. The sort decides the visible order, so a moved item would snap back; nothing changed.
    /// Clear the sort to reorder rows.
    /// </summary>
    NotSupportedSorted,

    /// <summary>
    /// The items source cannot be reordered: it is not an <see cref="System.Collections.IList"/> (the grid then
    /// reads a private copy the app never sees), or the list is read-only or fixed-size (an array). Nothing changed.
    /// </summary>
    NotSupportedSource,

    /// <summary>
    /// Grouping is active and the target row belongs to another group. A row moves only within its own group,
    /// because moving it to another group would mean changing the item's grouped value; nothing changed.
    /// </summary>
    NotSupportedAcrossGroups,
}

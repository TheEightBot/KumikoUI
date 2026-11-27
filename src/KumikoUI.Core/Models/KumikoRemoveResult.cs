namespace KumikoUI.Core.Models;

/// <summary>
/// The outcome of <see cref="KumikoSource.RemoveItems"/>, also reported by
/// <see cref="Input.KumikoRowsDeletedEventArgs.Result"/> after a delete.
/// </summary>
public enum KumikoRemoveResult
{
    /// <summary>At least one item was removed from the items source and the view was rebuilt once.</summary>
    Removed,

    /// <summary>No item was removed: the list was empty, held only <c>null</c>, or none of its items is in the items source.</summary>
    NothingToRemove,

    /// <summary>
    /// The items source cannot be changed by the grid: it is not an <see cref="System.Collections.IList"/> (the grid
    /// then reads a private copy the app never sees), or the list is read-only or fixed-size (an array). Nothing changed.
    /// </summary>
    NotSupportedSource,
}

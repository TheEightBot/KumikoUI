using KumikoUI.Core.Input;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Components;

/// <summary>
/// Provides data for the <c>RowMenuOpening</c> event: the row whose menu is about to open and the items it will show,
/// which a handler may change, reorder or add to.
/// </summary>
public sealed class KumikoRowMenuOpeningEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="KumikoRowMenuOpeningEventArgs"/> class.</summary>
    /// <param name="dataItem">The data item of the row, or null on a group header row.</param>
    /// <param name="rowIndex">The visible row the menu is for.</param>
    /// <param name="column">The column under the point or of the current cell, or null.</param>
    /// <param name="region">Where the menu was asked for.</param>
    /// <param name="items">The items the menu will show.</param>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is null.</exception>
    public KumikoRowMenuOpeningEventArgs(
        object? dataItem, int rowIndex, KumikoColumn? column, HitRegion region, IList<DrawnMenuItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        DataItem = dataItem;
        RowIndex = rowIndex;
        Column = column;
        Region = region;
        Items = items;
    }

    /// <summary>The data item of the row the menu is for, or null on a group header row.</summary>
    public object? DataItem { get; }

    /// <summary>The zero-based visible row the menu is for.</summary>
    public int RowIndex { get; }

    /// <summary>The column of the cell under the point (or of the current cell, for the keyboard), or null.</summary>
    public KumikoColumn? Column { get; }

    /// <summary>
    /// Where the menu was asked for: <see cref="HitRegion.Cell"/>, <see cref="HitRegion.RowDragHandle"/> or
    /// <see cref="HitRegion.GroupHeaderRow"/>.
    /// </summary>
    public HitRegion Region { get; }

    /// <summary>
    /// The items the menu will show: the grid's default items (see <see cref="KumikoRowMenuItems"/> for their keys), the
    /// swipe items and the app's <c>RowMenuItems</c>, in order. Remove, reorder, change or add items; a menu left with
    /// no item to choose does not open.
    /// </summary>
    public IList<DrawnMenuItem> Items { get; }

    /// <summary>Set it to true to keep the menu from opening.</summary>
    public bool Cancel { get; set; }
}

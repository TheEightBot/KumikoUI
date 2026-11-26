using KumikoUI.Core.Models;

namespace KumikoUI.Core.Components;

/// <summary>
/// Provides data for the <c>ColumnMenuOpening</c> event: the column whose menu is about to open and the items it
/// will show, which a handler may change, reorder or add to.
/// </summary>
public sealed class KumikoColumnMenuOpeningEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="KumikoColumnMenuOpeningEventArgs"/> class.</summary>
    /// <param name="column">The column the menu is for.</param>
    /// <param name="columnIndex">The index of the column in the data source's columns.</param>
    /// <param name="items">The items the menu will show.</param>
    /// <exception cref="ArgumentNullException"><paramref name="column"/> or <paramref name="items"/> is null.</exception>
    public KumikoColumnMenuOpeningEventArgs(KumikoColumn column, int columnIndex, IList<DrawnMenuItem> items)
    {
        ArgumentNullException.ThrowIfNull(column);
        ArgumentNullException.ThrowIfNull(items);
        Column = column;
        ColumnIndex = columnIndex;
        Items = items;
    }

    /// <summary>The column the menu is for.</summary>
    public KumikoColumn Column { get; }

    /// <summary>The index of the column in the data source's columns.</summary>
    public int ColumnIndex { get; }

    /// <summary>
    /// The items the menu will show: the grid's default items (see <see cref="KumikoColumnMenuItems"/> for their
    /// keys), in order. Remove, reorder, change or add items; a menu left with no item to choose does not open.
    /// </summary>
    public IList<DrawnMenuItem> Items { get; }

    /// <summary>Set it to true to keep the menu from opening.</summary>
    public bool Cancel { get; set; }
}

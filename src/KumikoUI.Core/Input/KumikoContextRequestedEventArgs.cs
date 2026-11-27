using KumikoUI.Core.Models;

namespace KumikoUI.Core.Input;

/// <summary>
/// Provides data for the <see cref="KumikoInputController.ContextRequested"/> event: where the user asked for a
/// context menu and what is there.
/// </summary>
/// <remarks>
/// On a data row the grid updates the selection before raising the event, as file managers and spreadsheets do:
/// a row that is not selected becomes the selection (as a tap without modifiers would select it), a row that is
/// already selected keeps the whole selection, and the current cell moves to the cell under the point. An open
/// edit on another cell is committed first; if the commit is refused, the editor stays open and no event is raised.
/// Elsewhere (a header, a group row, the empty area) the selection is left as it is.
/// </remarks>
public sealed class KumikoContextRequestedEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="KumikoContextRequestedEventArgs"/> class.</summary>
    /// <param name="trigger">What asked for the menu.</param>
    /// <param name="region">The region of the grid under the point.</param>
    /// <param name="rowIndex">The visible row under the point, or -1.</param>
    /// <param name="columnIndex">The column under the point, or -1.</param>
    /// <param name="column">The column under the point, or null.</param>
    /// <param name="dataItem">The data item of the row under the point, or null.</param>
    /// <param name="x">Horizontal position in grid viewport coordinates.</param>
    /// <param name="y">Vertical position in grid viewport coordinates.</param>
    /// <param name="deviceType">The device that asked for the menu.</param>
    public KumikoContextRequestedEventArgs(
        KumikoContextTrigger trigger, HitRegion region, int rowIndex, int columnIndex, KumikoColumn? column,
        object? dataItem, float x, float y, KumikoPointerDeviceType deviceType)
    {
        Trigger = trigger;
        Region = region;
        RowIndex = rowIndex;
        ColumnIndex = columnIndex;
        Column = column;
        DataItem = dataItem;
        X = x;
        Y = y;
        DeviceType = deviceType;
    }

    /// <summary>What asked for the menu: the secondary mouse button or a long press.</summary>
    public KumikoContextTrigger Trigger { get; }

    /// <summary>
    /// The region of the grid under the point: <see cref="HitRegion.Cell"/> for a data cell,
    /// <see cref="HitRegion.HeaderCell"/> (or its filter icon or resize grip) for a column header,
    /// <see cref="HitRegion.EmptyArea"/> for the empty part of the grid, and so on.
    /// </summary>
    public HitRegion Region { get; }

    /// <summary>
    /// Zero-based visible row under the point (a data row, a group header or a group summary), or -1 when the
    /// point is not on a row (a header, the empty area, a table summary row).
    /// </summary>
    public int RowIndex { get; }

    /// <summary>Zero-based index of the column under the point, or -1 when the point is not on a column.</summary>
    public int ColumnIndex { get; }

    /// <summary>The column under the point (a cell or a header), or null.</summary>
    public KumikoColumn? Column { get; }

    /// <summary>The data item of the row under the point, or null when the point is not on a data row.</summary>
    public object? DataItem { get; }

    /// <summary>
    /// Horizontal position in grid viewport coordinates: device-independent pixels from the left edge of the grid's
    /// drawing surface, which fills the control. Use it to place a menu next to the pointer.
    /// </summary>
    public float X { get; }

    /// <summary>Vertical position in grid viewport coordinates, from the top edge of the grid's drawing surface.</summary>
    public float Y { get; }

    /// <summary>The device that asked for the menu: a mouse for the secondary button, a finger or a pen for a long press.</summary>
    public KumikoPointerDeviceType DeviceType { get; }

    /// <summary>
    /// Whether the point is on a column header: <see cref="HitRegion.HeaderCell"/>,
    /// <see cref="HitRegion.HeaderFilterIcon"/>, <see cref="HitRegion.HeaderResizeGrip"/> or
    /// <see cref="HitRegion.HeaderMenuButton"/>.
    /// </summary>
    public bool IsHeader =>
        Region is HitRegion.HeaderCell or HitRegion.HeaderFilterIcon or HitRegion.HeaderResizeGrip
            or HitRegion.HeaderMenuButton;

    /// <summary>
    /// Set it to true when a handler shows its own menu, so the grid shows none. With
    /// <see cref="KumikoInputController.IsColumnMenuEnabled"/> on, a request on a column header that no handler marks
    /// handled opens the built-in column menu; an app that already shows its own header menu from this event sets
    /// it before turning the built-in menu on. Default false.
    /// </summary>
    public bool Handled { get; set; }
}

using KumikoUI.Core.Models;

namespace KumikoUI.Core.Rendering;

/// <summary>
/// Everything an <see cref="IHeaderCellRenderer"/> needs to draw one header cell: where to draw, which column it
/// is, its sort and filter state, how it is styled and the grid's shared paints. The grid creates one per header on
/// the stack and passes it by reference, so drawing a header allocates nothing.
/// </summary>
/// <remarks>
/// The reference-type members (<see cref="Column"/>, <see cref="Style"/>, <see cref="HeaderCellStyle"/>,
/// <see cref="Paints"/>, <see cref="TextPaint"/>) may be shared with other headers or reused for the next one: read
/// them during <see cref="IHeaderCellRenderer.Render"/>, and do not keep or modify them.
/// </remarks>
public readonly struct HeaderCellRenderContext
{
    /// <summary>Create the context for one header cell.</summary>
    /// <param name="bounds">The padded rectangle to draw the content within, without the room of the glyphs.</param>
    /// <param name="cellBounds">The whole header cell.</param>
    /// <param name="column">The column definition.</param>
    /// <param name="columnIndex">The column's index in the source's column list.</param>
    /// <param name="sortDirection">The column's sort direction.</param>
    /// <param name="sortOrder">The column's position in a multi-column sort, or 0.</param>
    /// <param name="isFiltered">Whether the column has an active filter.</param>
    /// <param name="style">The grid style.</param>
    /// <param name="headerCellStyle">The column's header style overrides, or null.</param>
    /// <param name="paints">The grid's shared paints for <paramref name="style"/>.</param>
    /// <param name="textPaint">The paint for the header text (the style's header paint with the column's overrides).</param>
    /// <param name="textAlignment">The header's horizontal text alignment.</param>
    public HeaderCellRenderContext(
        KumikoRect bounds,
        KumikoRect cellBounds,
        KumikoColumn column,
        int columnIndex,
        KumikoSortDirection sortDirection,
        int sortOrder,
        bool isFiltered,
        KumikoStyle style,
        KumikoCellStyle? headerCellStyle,
        PaintCache paints,
        KumikoPaint textPaint,
        KumikoTextAlignment textAlignment)
    {
        Bounds = bounds;
        CellBounds = cellBounds;
        Column = column;
        ColumnIndex = columnIndex;
        SortDirection = sortDirection;
        SortOrder = sortOrder;
        IsFiltered = isFiltered;
        Style = style;
        HeaderCellStyle = headerCellStyle;
        Paints = paints;
        TextPaint = textPaint;
        TextAlignment = textAlignment;
    }

    /// <summary>The padded rectangle to draw the content within: the header cell less its padding and the room of the sort, filter and menu glyphs.</summary>
    public KumikoRect Bounds { get; }

    /// <summary>The whole header cell, including the room of the glyphs and the padding.</summary>
    public KumikoRect CellBounds { get; }

    /// <summary>The column definition.</summary>
    public KumikoColumn Column { get; }

    /// <summary>The column's index in the source's column list.</summary>
    public int ColumnIndex { get; }

    /// <summary>The column's sort direction.</summary>
    public KumikoSortDirection SortDirection { get; }

    /// <summary>The column's position in a multi-column sort, or 0 when it is not sorted.</summary>
    public int SortOrder { get; }

    /// <summary>Whether the column has an active filter.</summary>
    public bool IsFiltered { get; }

    /// <summary>The grid style.</summary>
    public KumikoStyle Style { get; }

    /// <summary>The column's header style overrides (<see cref="KumikoColumn.HeaderCellStyle"/>), or null.</summary>
    public KumikoCellStyle? HeaderCellStyle { get; }

    /// <summary>The grid's shared paints for <see cref="Style"/>.</summary>
    public PaintCache Paints { get; }

    /// <summary>The paint for the header text: the style's header paint with the column's color and font overrides.</summary>
    public KumikoPaint TextPaint { get; }

    /// <summary>The header's horizontal text alignment: <see cref="KumikoCellStyle.TextAlignment"/> of <see cref="HeaderCellStyle"/>, else left.</summary>
    public KumikoTextAlignment TextAlignment { get; }
}

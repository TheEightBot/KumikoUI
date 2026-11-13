using KumikoUI.Core.Models;

namespace KumikoUI.Core.Rendering;

/// <summary>
/// Everything an <see cref="ICellRenderer"/> needs to draw one cell: where to draw, what to draw, how the cell
/// is styled, and the grid's shared paints. The grid creates one per cell on the stack and passes it by
/// reference, so drawing a cell allocates nothing.
/// </summary>
/// <remarks>
/// The reference-type members (<see cref="CellStyle"/>, <see cref="TextPaint"/>, <see cref="Paints"/>) may be
/// shared with other cells or reused for the next one: read them during <see cref="ICellRenderer.Render"/>, and
/// do not keep or modify them. A renderer that needs a paint the cache does not have should keep its own
/// instance and reuse it across calls rather than allocate one per cell.
/// </remarks>
public readonly struct CellRenderContext
{
    /// <summary>Create the context for one cell.</summary>
    /// <param name="bounds">The padded rectangle to draw the cell content within.</param>
    /// <param name="row">The visible row index of the cell.</param>
    /// <param name="value">The raw cell value.</param>
    /// <param name="displayText">The cell value formatted with the column's format in the source's culture.</param>
    /// <param name="column">The column definition.</param>
    /// <param name="style">The grid style.</param>
    /// <param name="cellStyle">The cell's effective style overrides, or null.</param>
    /// <param name="isSelected">Whether the cell's row is selected.</param>
    /// <param name="paints">The grid's shared paints for <paramref name="style"/>.</param>
    /// <param name="textPaint">The paint for the cell's text (the style's cell text paint with the cell's overrides).</param>
    /// <param name="textAlignment">The cell's effective horizontal text alignment.</param>
    public CellRenderContext(
        KumikoRect bounds,
        int row,
        object? value,
        string displayText,
        KumikoColumn column,
        KumikoStyle style,
        KumikoCellStyle? cellStyle,
        bool isSelected,
        PaintCache paints,
        KumikoPaint textPaint,
        KumikoTextAlignment textAlignment)
        : this(bounds, row, value, displayText, column, style, cellStyle, isSelected, paints, textPaint, textAlignment,
            imageResolver: null)
    {
    }

    /// <summary>Create the context for one cell, with the grid's image resolver.</summary>
    /// <param name="bounds">The padded rectangle to draw the cell content within.</param>
    /// <param name="row">The visible row index of the cell.</param>
    /// <param name="value">The raw cell value.</param>
    /// <param name="displayText">The cell value formatted with the column's format in the source's culture.</param>
    /// <param name="column">The column definition.</param>
    /// <param name="style">The grid style.</param>
    /// <param name="cellStyle">The cell's effective style overrides, or null.</param>
    /// <param name="isSelected">Whether the cell's row is selected.</param>
    /// <param name="paints">The grid's shared paints for <paramref name="style"/>.</param>
    /// <param name="textPaint">The paint for the cell's text (the style's cell text paint with the cell's overrides).</param>
    /// <param name="textAlignment">The cell's effective horizontal text alignment.</param>
    /// <param name="imageResolver">The grid's image resolver, or null when the host has none.</param>
    public CellRenderContext(
        KumikoRect bounds,
        int row,
        object? value,
        string displayText,
        KumikoColumn column,
        KumikoStyle style,
        KumikoCellStyle? cellStyle,
        bool isSelected,
        PaintCache paints,
        KumikoPaint textPaint,
        KumikoTextAlignment textAlignment,
        IKumikoImageResolver? imageResolver)
    {
        Bounds = bounds;
        Row = row;
        Value = value;
        DisplayText = displayText;
        Column = column;
        Style = style;
        CellStyle = cellStyle;
        IsSelected = isSelected;
        Paints = paints;
        TextPaint = textPaint;
        TextAlignment = textAlignment;
        ImageResolver = imageResolver;
    }

    /// <summary>The padded rectangle to draw the cell content within (the cell minus its horizontal padding).</summary>
    public KumikoRect Bounds { get; }

    /// <summary>The visible row index of the cell.</summary>
    public int Row { get; }

    /// <summary>The raw cell value.</summary>
    public object? Value { get; }

    /// <summary>
    /// The cell value formatted with <see cref="KumikoColumn.Format"/> in the source's culture; empty for null.
    /// The grid caches it per row and column, so it is the same instance from frame to frame while the value is
    /// unchanged.
    /// </summary>
    public string DisplayText { get; }

    /// <summary>The column definition.</summary>
    public KumikoColumn Column { get; }

    /// <summary>The grid style (colors, fonts, sizes).</summary>
    public KumikoStyle Style { get; }

    /// <summary>
    /// The cell's style overrides (the column's <see cref="KumikoColumn.CellStyle"/> merged with the
    /// resolvers'), or null. The instance may be reused for the next cell.
    /// </summary>
    public KumikoCellStyle? CellStyle { get; }

    /// <summary>Whether the cell's row is selected.</summary>
    public bool IsSelected { get; }

    /// <summary>The grid's shared paints, built from <see cref="Style"/>. Draw with them; do not modify them.</summary>
    public PaintCache Paints { get; }

    /// <summary>
    /// The paint for the cell's text: <see cref="PaintCache.CellText"/>, or a reused paint carrying the text color
    /// and font of <see cref="CellStyle"/> when it overrides them. Draw with it; do not keep or modify it.
    /// </summary>
    public KumikoPaint TextPaint { get; }

    /// <summary>The cell's horizontal text alignment: the cell style's, else the column's.</summary>
    public KumikoTextAlignment TextAlignment { get; }

    /// <summary>
    /// The grid's image resolver (<see cref="KumikoRenderer.ImageResolver"/>), or null when the host has none. A
    /// renderer that draws images from cell values resolves them through it, as <see cref="ImageCellRenderer"/> does.
    /// </summary>
    public IKumikoImageResolver? ImageResolver { get; }
}

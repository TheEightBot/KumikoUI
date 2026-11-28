using KumikoUI.Core.Models;

namespace KumikoUI.Core.Rendering;

/// <summary>
/// Everything an <see cref="ISummaryCellRenderer"/> needs to draw one summary value cell: where to draw, the
/// computed values, and the grid's shared paints. The grid creates one per cell on the stack and passes it by
/// reference, so drawing a cell allocates nothing.
/// </summary>
/// <remarks>
/// The reference-type members may be shared with other cells or reused for the next one: read them during
/// <see cref="ISummaryCellRenderer.Render"/>, and do not keep or modify them.
/// </remarks>
public readonly struct SummaryCellRenderContext
{
    /// <summary>Create the context for one summary cell.</summary>
    /// <param name="bounds">The padded rectangle to draw within, clipped to the pane and right of the row title.</param>
    /// <param name="cellBounds">The whole column slot in the summary row.</param>
    /// <param name="kind">Whether the row is a table or a group summary row.</param>
    /// <param name="summary">The computed summary row.</param>
    /// <param name="group">The group, for a group summary row; otherwise null.</param>
    /// <param name="column">The column.</param>
    /// <param name="displayText">The cell's text.</param>
    /// <param name="rawValue">The first aggregate's raw value.</param>
    /// <param name="style">The grid style.</param>
    /// <param name="paints">The grid's shared paints for <paramref name="style"/>.</param>
    /// <param name="textPaint">The paint of the summary row's text.</param>
    /// <param name="textAlignment">The column's horizontal text alignment.</param>
    public SummaryCellRenderContext(
        KumikoRect bounds,
        KumikoRect cellBounds,
        KumikoSummaryRowKind kind,
        ComputedSummaryRow summary,
        GroupHeaderInfo? group,
        KumikoColumn column,
        string displayText,
        object? rawValue,
        KumikoStyle style,
        PaintCache paints,
        KumikoPaint textPaint,
        KumikoTextAlignment textAlignment)
    {
        Bounds = bounds;
        CellBounds = cellBounds;
        Kind = kind;
        Summary = summary;
        Group = group;
        Column = column;
        DisplayText = displayText;
        RawValue = rawValue;
        Style = style;
        Paints = paints;
        TextPaint = textPaint;
        TextAlignment = textAlignment;
    }

    /// <summary>The padded rectangle to draw within, clipped to the visible part of the column and never under the row title.</summary>
    public KumikoRect Bounds { get; }

    /// <summary>The whole column slot in the summary row, before padding and clipping.</summary>
    public KumikoRect CellBounds { get; }

    /// <summary>Whether the row is a table or a group summary row.</summary>
    public KumikoSummaryRowKind Kind { get; }

    /// <summary>
    /// The computed summary row: <see cref="ComputedSummaryRow.Values"/>, <see cref="ComputedSummaryRow.RawValues"/>
    /// and <see cref="ComputedSummaryRow.Aggregates"/>. Several aggregates can share the column's property: filter
    /// <see cref="ComputedSummaryRow.Aggregates"/> by <see cref="ComputedSummaryValue.PropertyName"/>.
    /// </summary>
    public ComputedSummaryRow Summary { get; }

    /// <summary>The group a group summary row belongs to; null for a table summary row.</summary>
    public GroupHeaderInfo? Group { get; }

    /// <summary>The column the cell sits in.</summary>
    public KumikoColumn Column { get; }

    /// <summary>The cell's text: the column's entry in <see cref="ComputedSummaryRow.Values"/>.</summary>
    public string DisplayText { get; }

    /// <summary>The raw result of the column's first aggregate, before formatting.</summary>
    public object? RawValue { get; }

    /// <summary>The grid style.</summary>
    public KumikoStyle Style { get; }

    /// <summary>The grid's shared paints; match the theme with <see cref="PaintCache.SummaryLabel"/> and the other summary paints.</summary>
    public PaintCache Paints { get; }

    /// <summary>The paint for the row's text: <see cref="PaintCache.SummaryText"/> or <see cref="PaintCache.GroupSummaryText"/>.</summary>
    public KumikoPaint TextPaint { get; }

    /// <summary>The column's horizontal text alignment.</summary>
    public KumikoTextAlignment TextAlignment { get; }
}

namespace KumikoUI.Core.Rendering;

/// <summary>
/// Renders text content (Text, Numeric, Date column types): one line cut with an ellipsis, or wrapped lines when
/// the column's <see cref="Models.KumikoColumn.TextWrap"/> asks for them. In
/// <see cref="Models.KumikoRowHeightMode.Auto"/> mode a wrapping cell's row grows to show all of its lines.
/// </summary>
public class TextCellRenderer : ICellRenderer, IMeasurableCellRenderer
{
    /// <summary>
    /// The height of the cell's wrapped lines at the cell's width, or <c>null</c> for a column that does not wrap
    /// (<see cref="KumikoTextWrap.None"/>: one line never needs more than the minimum row height) or an empty cell.
    /// </summary>
    /// <param name="ctx">Measures the text.</param>
    /// <param name="cell">The cell, as it will be drawn.</param>
    /// <returns>The height of the text block in pixels, or <c>null</c>.</returns>
    public virtual float? MeasureHeight(IDrawingContext ctx, in CellRenderContext cell)
    {
        var wrap = cell.Column.TextWrap;
        if (wrap == KumikoTextWrap.None || string.IsNullOrEmpty(cell.DisplayText)) return null;
        return ctx.MeasureText(cell.DisplayText, cell.TextPaint, cell.Bounds.Width, wrap).Height;
    }

    /// <inheritdoc />
    public void Render(IDrawingContext ctx, in CellRenderContext cell)
    {
        var wrap = cell.Column.TextWrap;
        if (wrap == KumikoTextWrap.None)
        {
            ctx.DrawTextInRect(cell.DisplayText, cell.Bounds, cell.TextPaint,
                cell.TextAlignment, KumikoVerticalAlignment.Center);
        }
        else
        {
            ctx.DrawTextInRect(cell.DisplayText, cell.Bounds, cell.TextPaint,
                cell.TextAlignment, KumikoVerticalAlignment.Center, clip: true, wrap);
        }
    }
}

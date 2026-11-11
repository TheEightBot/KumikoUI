namespace KumikoUI.Core.Rendering;

/// <summary>
/// Renders text content (Text, Numeric, Date column types): one line cut with an ellipsis, or wrapped lines when
/// the column's <see cref="Models.KumikoColumn.TextWrap"/> asks for them.
/// </summary>
public class TextCellRenderer : ICellRenderer
{
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

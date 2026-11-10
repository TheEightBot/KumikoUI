namespace KumikoUI.Core.Rendering;

/// <summary>
/// Renders text content (Text, Numeric, Date column types).
/// </summary>
public class TextCellRenderer : ICellRenderer
{
    /// <inheritdoc />
    public void Render(IDrawingContext ctx, in CellRenderContext cell)
    {
        ctx.DrawTextInRect(cell.DisplayText, cell.Bounds, cell.TextPaint,
            cell.TextAlignment, KumikoVerticalAlignment.Center);
    }
}

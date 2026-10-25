using KumikoUI.Core.Models;

namespace KumikoUI.Core.Rendering;

/// <summary>
/// Renders text content (Text, Numeric, Date column types).
/// </summary>
public class TextCellRenderer : ICellRenderer
{
    public void Render(
        IDrawingContext ctx, GridRect cellRect, object? value,
        string displayText, GridColumn column,
        DataGridStyle style, bool isSelected,
        DataGridCellStyle? cellStyle = null)
    {
        var paint = new GridPaint
        {
            Color = cellStyle?.TextColor ?? style.CellTextColor,
            Font = cellStyle?.Font ?? style.CellFont,
            IsAntiAlias = true
        };

        var alignment = cellStyle?.TextAlignment ?? column.TextAlignment;
        ctx.DrawTextInRect(displayText, cellRect, paint,
            alignment, GridVerticalAlignment.Center);
    }
}

using KumikoUI.Core.Models;

namespace KumikoUI.Core.Rendering;

/// <summary>
/// Renders text content (Text, Numeric, Date column types).
/// </summary>
public class TextCellRenderer : ICellRenderer
{
    public void Render(
        IDrawingContext ctx, KumikoRect cellRect, object? value,
        string displayText, KumikoColumn column,
        KumikoStyle style, bool isSelected,
        KumikoCellStyle? cellStyle = null)
    {
        var paint = new KumikoPaint
        {
            Color = cellStyle?.TextColor ?? style.CellTextColor,
            Font = cellStyle?.Font ?? style.CellFont,
            IsAntiAlias = true
        };

        var alignment = cellStyle?.TextAlignment ?? column.TextAlignment;
        ctx.DrawTextInRect(displayText, cellRect, paint,
            alignment, KumikoVerticalAlignment.Center);
    }
}

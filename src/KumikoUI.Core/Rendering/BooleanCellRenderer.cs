using KumikoUI.Core.Models;

namespace KumikoUI.Core.Rendering;

/// <summary>
/// Renders boolean values as custom-drawn checkboxes.
/// </summary>
public class BooleanCellRenderer : ICellRenderer
{
    public void Render(
        IDrawingContext ctx, GridRect cellRect, object? value,
        string displayText, GridColumn column,
        DataGridStyle style, bool isSelected,
        DataGridCellStyle? cellStyle = null)
    {
        bool isChecked = value is true
            || string.Equals(displayText, "True", StringComparison.OrdinalIgnoreCase);

        float boxSize = Math.Min(16f, cellRect.Height - 8);
        float centerY = cellRect.Y + (cellRect.Height - boxSize) / 2;
        float centerX = cellRect.X + (cellRect.Width - boxSize) / 2;
        var boxRect = new GridRect(centerX, centerY, boxSize, boxSize);

        // Checkbox border
        ctx.DrawRect(boxRect, new GridPaint
        {
            Color = style.GridLineColor,
            Style = PaintStyle.Stroke,
            StrokeWidth = 1.5f,
            IsAntiAlias = true
        });

        // Checkmark
        if (isChecked)
        {
            var checkPaint = new GridPaint
            {
                Color = style.AccentColor,
                Style = PaintStyle.Stroke,
                StrokeWidth = 2f,
                IsAntiAlias = true
            };
            float pad = boxSize * 0.2f;
            ctx.DrawLine(
                boxRect.X + pad, boxRect.Y + boxSize * 0.5f,
                boxRect.X + boxSize * 0.4f, boxRect.Bottom - pad,
                checkPaint);
            ctx.DrawLine(
                boxRect.X + boxSize * 0.4f, boxRect.Bottom - pad,
                boxRect.Right - pad, boxRect.Y + pad,
                checkPaint);
        }
    }
}

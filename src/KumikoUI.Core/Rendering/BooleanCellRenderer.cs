using KumikoUI.Core.Models;

namespace KumikoUI.Core.Rendering;

/// <summary>
/// Renders boolean values as custom-drawn checkboxes.
/// </summary>
public class BooleanCellRenderer : ICellRenderer
{
    // Extra margin around the drawn box that still counts as a tap on it, so the target is easy to hit.
    private const float CheckBoxHitSlop = 8f;

    /// <summary>The check box drawn in <paramref name="cellRect"/>: at most 16 px square, centered.</summary>
    internal static KumikoRect GetCheckBoxBounds(KumikoRect cellRect)
    {
        float boxSize = Math.Min(16f, cellRect.Height - 8);
        float centerY = cellRect.Y + (cellRect.Height - boxSize) / 2;
        float centerX = cellRect.X + (cellRect.Width - boxSize) / 2;
        return new KumikoRect(centerX, centerY, boxSize, boxSize);
    }

    /// <summary>
    /// The area of <paramref name="cellRect"/> where a tap toggles the value: the check box plus a margin,
    /// kept inside the cell.
    /// </summary>
    internal static KumikoRect GetCheckBoxHitBounds(KumikoRect cellRect)
    {
        var box = GetCheckBoxBounds(cellRect);
        float left = Math.Max(cellRect.X, box.X - CheckBoxHitSlop);
        float top = Math.Max(cellRect.Y, box.Y - CheckBoxHitSlop);
        float right = Math.Min(cellRect.Right, box.Right + CheckBoxHitSlop);
        float bottom = Math.Min(cellRect.Bottom, box.Bottom + CheckBoxHitSlop);
        return new KumikoRect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    /// <summary>Draws the check box for <paramref name="value"/> centered in <paramref name="cellRect"/>.</summary>
    public void Render(
        IDrawingContext ctx, KumikoRect cellRect, object? value,
        string displayText, KumikoColumn column,
        KumikoStyle style, bool isSelected,
        KumikoCellStyle? cellStyle = null)
    {
        bool isChecked = value is true
            || string.Equals(displayText, "True", StringComparison.OrdinalIgnoreCase);

        var boxRect = GetCheckBoxBounds(cellRect);
        float boxSize = boxRect.Width;

        // Checkbox border
        ctx.DrawRect(boxRect, new KumikoPaint
        {
            Color = style.GridLineColor,
            Style = PaintStyle.Stroke,
            StrokeWidth = 1.5f,
            IsAntiAlias = true
        });

        // Checkmark
        if (isChecked)
        {
            var checkPaint = new KumikoPaint
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

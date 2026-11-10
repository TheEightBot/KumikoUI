namespace KumikoUI.Core.Rendering;

/// <summary>
/// Renders boolean values as custom-drawn checkboxes.
/// </summary>
public class BooleanCellRenderer : ICellRenderer
{
    /// <inheritdoc />
    public void Render(IDrawingContext ctx, in CellRenderContext cell)
    {
        bool isChecked = cell.Value is true
            || string.Equals(cell.DisplayText, "True", StringComparison.OrdinalIgnoreCase);

        var cellRect = cell.Bounds;
        float boxSize = Math.Min(16f, cellRect.Height - 8);
        float centerY = cellRect.Y + (cellRect.Height - boxSize) / 2;
        float centerX = cellRect.X + (cellRect.Width - boxSize) / 2;
        var boxRect = new KumikoRect(centerX, centerY, boxSize, boxSize);

        // Checkbox border
        ctx.DrawRect(boxRect, cell.Paints.CheckBoxBorder);

        // Checkmark
        if (isChecked)
        {
            var checkPaint = cell.Paints.CheckMark;
            float pad = boxSize * 0.2f;
            GlyphDrawing.Chevron(ctx,
                boxRect.X + pad, boxRect.Y + boxSize * 0.5f,
                boxRect.X + boxSize * 0.4f, boxRect.Bottom - pad,
                boxRect.Right - pad, boxRect.Y + pad,
                checkPaint);
        }
    }
}

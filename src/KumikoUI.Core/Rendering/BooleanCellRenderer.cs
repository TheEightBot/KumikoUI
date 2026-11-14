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

    /// <inheritdoc />
    public void Render(IDrawingContext ctx, in CellRenderContext cell)
    {
        bool isChecked = cell.Value is true
            || string.Equals(cell.DisplayText, "True", StringComparison.OrdinalIgnoreCase);

        var boxRect = GetCheckBoxBounds(cell.Bounds);

        // Hover and pressed feedback: a soft square around the box while the pointer is on its target
        if (cell.PointerPosition is { } pointer && GetCheckBoxHitBounds(cell.Bounds).Contains(pointer.X, pointer.Y))
            ctx.FillRoundRect(boxRect.Inflate(4, 4), 4, cell.IsPointerPressed ? cell.Paints.PressedOverlay : cell.Paints.HoverOverlay);

        // Checkbox border
        ctx.DrawRect(boxRect, cell.Paints.CheckBoxBorder);

        // Indeterminate: a three-state column's null is the indeterminate mark, the other columns' null is unchecked
        if (cell.Value is null && cell.Column.IsThreeState)
            cell.Style.IconProvider.DrawIcon(ctx, KumikoIcon.Indeterminate, boxRect, cell.Paints.CheckIndeterminate);
        else if (isChecked)
            cell.Style.IconProvider.DrawIcon(ctx, KumikoIcon.Check, boxRect, cell.Paints.CheckMark);
    }
}

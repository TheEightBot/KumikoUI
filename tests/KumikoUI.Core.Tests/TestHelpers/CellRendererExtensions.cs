using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Tests.TestHelpers;

/// <summary>
/// Calls an <see cref="ICellRenderer"/> directly with the <see cref="CellRenderContext"/> the grid would build for
/// the cell: the paints of <paramref name="style"/>, the text paint with the cell style's overrides, and the
/// cell style's alignment over the column's.
/// </summary>
internal static class CellRendererExtensions
{
    public static void Render(this ICellRenderer renderer, IDrawingContext ctx, KumikoRect cellRect, object? value,
        string displayText, KumikoColumn column, KumikoStyle style, bool isSelected,
        KumikoCellStyle? cellStyle = null, int row = 0)
    {
        var paints = new PaintCache();
        paints.Update(style);
        var cell = new CellRenderContext(cellRect, row, value, displayText, column, style, cellStyle, isSelected,
            paints, paints.CellTextPaint(cellStyle, style), cellStyle?.TextAlignment ?? column.TextAlignment);
        renderer.Render(ctx, in cell);
    }
}

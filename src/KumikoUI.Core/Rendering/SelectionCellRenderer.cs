using KumikoUI.Core.Components;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Rendering;

/// <summary>
/// Draws the cells of a <see cref="KumikoSelectionColumn"/> (<see cref="KumikoColumnType.Selection"/>): a check box
/// that shows whether the row is selected. It draws no text. In <see cref="KumikoSelectionMode.None"/> mode the box is
/// dimmed. The grid sets <see cref="Mode"/> before it draws the rows. Allocation-free.
/// </summary>
internal sealed class SelectionCellRenderer : ICellRenderer
{
    // Alpha of the border and mark of a box that cannot be used
    private const byte DimmedAlpha = 90;

    // Reused each frame: the dimmed copies of the grid's check box paints
    private readonly KumikoPaint _dimmedBorder = new();
    private readonly KumikoPaint _dimmedMark = new();

    /// <summary>
    /// The grid's selection mode: <see cref="KumikoSelectionMode.None"/> draws the boxes dimmed. The grid keeps it
    /// current; set it only on a renderer you draw with yourself.
    /// </summary>
    internal KumikoSelectionMode Mode { get; set; } = KumikoSelectionMode.Extended;

    /// <summary>
    /// The area of a cell where a tap toggles its row: the whole cell, not only the drawn box, so the touch target is
    /// the column's width.
    /// </summary>
    internal static KumikoRect GetHitBounds(KumikoRect cellRect) => cellRect;

    /// <inheritdoc />
    public void Render(IDrawingContext ctx, in CellRenderContext cell)
    {
        var box = CheckBoxGlyph.BoxBounds(cell.Bounds);
        var state = cell.IsSelected ? DrawnCheckState.Checked : DrawnCheckState.Unchecked;

        if (Mode == KumikoSelectionMode.None)
        {
            _dimmedBorder.Color = cell.Paints.CheckBoxBorder.Color.WithAlpha(DimmedAlpha);
            _dimmedBorder.StrokeWidth = cell.Paints.CheckBoxBorder.StrokeWidth;
            _dimmedMark.Color = cell.Paints.CheckMark.Color.WithAlpha(DimmedAlpha);
            _dimmedMark.StrokeWidth = cell.Paints.CheckMark.StrokeWidth;
            CheckBoxGlyph.Draw(ctx, box, state, cell.Style, cell.Paints, _dimmedBorder, _dimmedMark);
            return;
        }

        // Hover and pressed feedback: a soft square around the box while the pointer is on the cell
        if (cell.PointerPosition is { } pointer && GetHitBounds(cell.Bounds).Contains(pointer.X, pointer.Y))
            ctx.FillRoundRect(box.Inflate(4, 4), 4, cell.IsPointerPressed ? cell.Paints.PressedOverlay : cell.Paints.HoverOverlay);

        CheckBoxGlyph.Draw(ctx, box, state, cell.Style, cell.Paints);
    }
}

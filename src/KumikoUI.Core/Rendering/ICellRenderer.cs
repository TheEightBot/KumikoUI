using KumikoUI.Core.Models;

namespace KumikoUI.Core.Rendering;

/// <summary>
/// Interface for rendering individual cell content.
/// Implementations are dispatched by <see cref="KumikoColumnType"/>, or set per column with
/// <see cref="KumikoColumn.CustomCellRenderer"/>.
/// </summary>
/// <remarks>
/// <see cref="Render"/> runs for every visible cell on every frame. To keep frames free of allocations, draw with
/// the shared paints in <see cref="CellRenderContext.Paints"/> or <see cref="CellRenderContext.TextPaint"/>, or
/// with paints the renderer creates once and reuses; do not create a <see cref="KumikoPaint"/>,
/// <see cref="KumikoFont"/> or string per call.
/// </remarks>
public interface ICellRenderer
{
    /// <summary>
    /// Render the cell content within <see cref="CellRenderContext.Bounds"/>.
    /// </summary>
    /// <param name="ctx">Drawing context.</param>
    /// <param name="cell">The cell to draw: its bounds, value, display text, column, styles and the grid's
    /// shared paints. Its reference-type members may be reused for the next cell: read them during the call, do
    /// not keep or modify them.</param>
    void Render(IDrawingContext ctx, in CellRenderContext cell);
}

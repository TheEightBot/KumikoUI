using KumikoUI.Core.Models;

namespace KumikoUI.Core.Rendering;

/// <summary>
/// Interface for rendering individual cell content.
/// Implementations are dispatched by <see cref="KumikoColumnType"/>.
/// </summary>
public interface ICellRenderer
{
    /// <summary>
    /// Render the cell content within the given rectangle.
    /// </summary>
    /// <param name="ctx">Drawing context.</param>
    /// <param name="cellRect">The padded cell rectangle to draw within.</param>
    /// <param name="value">The raw cell value.</param>
    /// <param name="displayText">The formatted display text.</param>
    /// <param name="column">The column definition.</param>
    /// <param name="style">Grid style for colors/fonts.</param>
    /// <param name="isSelected">Whether the row is selected.</param>
    /// <param name="cellStyle">Optional per-column style overrides.</param>
    void Render(
        IDrawingContext ctx,
        KumikoRect cellRect,
        object? value,
        string displayText,
        KumikoColumn column,
        KumikoStyle style,
        bool isSelected,
        KumikoCellStyle? cellStyle = null);
}

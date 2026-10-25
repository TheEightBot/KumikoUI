using KumikoUI.Core.Components;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Rendering;

/// <summary>
/// Renders an arbitrary set of action buttons inside a cell (display-only).
/// Pair with <see cref="KumikoUI.Core.Components.DrawnActionButtons"/> as the
/// interactive editor (activated on tap) to handle click events.
/// </summary>
/// <remarks>
/// Both this renderer and <c>DrawnActionButtons</c> share the same
/// <see cref="ActionButtonDefinition"/> list and layout logic, so the display
/// and interactive states are visually identical.
///
/// <code>
/// actionsColumn.CustomCellRenderer = new ActionButtonsCellRenderer
/// {
///     Buttons =
///     [
///         new() { Label = "Edit",   BackgroundColor = new GridColor(13, 110, 253) },
///         new() { Label = "Delete", BackgroundColor = new GridColor(220, 53, 69)  }
///     ]
/// };
/// </code>
/// </remarks>
public class ActionButtonsCellRenderer : ICellRenderer, ICellEditorProvider
{
    /// <summary>
    /// The ordered list of buttons to render.
    /// Buttons are distributed with equal widths across the cell.
    /// Actions and commands on these definitions are ignored in display mode.
    /// </summary>
    public IList<IActionButtonDefinition> Buttons { get; set; } = new List<IActionButtonDefinition>();

    /// <summary>Corner radius for all button pills.</summary>
    public float CornerRadius { get; set; } = 5f;

    /// <summary>Horizontal gap between adjacent buttons (pixels).</summary>
    public float ButtonSpacing { get; set; } = 6f;

    /// <summary>Inset from all four cell edges (pixels).</summary>
    public float CellPadding { get; set; } = 4f;

    /// <inheritdoc />
    public void Render(
        IDrawingContext ctx, GridRect cellRect, object? value,
        string displayText, DataGridColumn column,
        DataGridStyle style, bool isSelected,
        CellStyle? cellStyle = null)
    {
        if (Buttons.Count == 0) return;
        var rects = ActionButtonLayout.ComputeButtonRects(cellRect, Buttons.Count, CellPadding, ButtonSpacing);

        for (int i = 0; i < Buttons.Count; i++)
        {
            var btn = Buttons[i];
            ctx.FillRoundRect(rects[i], CornerRadius, new GridPaint { Color = btn.BackgroundColor, IsAntiAlias = true });
            ctx.DrawTextInRect(btn.Label, rects[i], new GridPaint
            {
                Color = btn.TextColor,
                Font = new GridFont("Default", 11, bold: true),
                IsAntiAlias = true
            }, GridTextAlignment.Center, GridVerticalAlignment.Center);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Creates a <see cref="KumikoUI.Core.Components.DrawnActionButtons"/> editor that
    /// shares this renderer's <see cref="Buttons"/> list. Because the list is shared,
    /// any <c>ICommand</c> bindings already resolved on the definitions (e.g. via
    /// <c>MauiActionButtonDefinition</c> XAML bindings) are immediately available to
    /// the interactive editor without additional wiring.
    /// </remarks>
    public DrawnComponent? CreateEditor(object? value, GridRect bounds)
        => new KumikoUI.Core.Components.DrawnActionButtons
        {
            Bounds  = bounds,
            RowItem = value,
            Buttons = Buttons,
            CornerRadius  = CornerRadius,
            ButtonSpacing = ButtonSpacing,
            CellPadding   = CellPadding
        };
}

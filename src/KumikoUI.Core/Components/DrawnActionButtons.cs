using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>
/// A canvas-drawn component that renders an arbitrary set of action buttons
/// inside a grid cell. Add any number of <see cref="ActionButtonDefinition"/>
/// entries to <see cref="Buttons"/> — they are distributed with equal widths
/// across the cell.
/// </summary>
/// <remarks>
/// Pair with <see cref="ActionButtonsCellRenderer"/> for the always-visible
/// display pass. The editor activates on the first tap (set column
/// <c>EditTrigger = SingleTap</c>) and closes automatically after any
/// button action fires.
///
/// <b>Code-behind usage</b> — capture the row item and wire <c>Command</c> per button:
/// <code>
/// actionsColumn.CustomEditorFactory = (value, bounds) => new DrawnActionButtons
/// {
///     Bounds  = bounds,
///     RowItem = value,
///     Buttons =
///     [
///         new() { Label = "Edit",   BackgroundColor = new GridColor(13, 110, 253), Command = vm.EditCommand   },
///         new() { Label = "Delete", BackgroundColor = new GridColor(220, 53, 69),  Command = vm.DeleteCommand }
///     ]
/// };
/// </code>
/// When <see cref="ActionButtonDefinition.CommandParameter"/> is not set the
/// row item (<see cref="RowItem"/>) is passed to <c>ICommand.Execute</c>.
/// </remarks>
public class DrawnActionButtons : DrawnComponent
{
    private int _pressedIndex = -1;

    /// <inheritdoc />
    /// <remarks>
    /// <c>true</c> — the grid forwards the activating tap to this editor immediately
    /// after creation so the button fires on the first touch.
    /// </remarks>
    public override bool ActivatesImmediately => true;

    /// <summary>
    /// The ordered list of buttons to render.
    /// Buttons are distributed with equal widths across the cell.
    /// </summary>
    public IList<IActionButtonDefinition> Buttons { get; set; } = new List<IActionButtonDefinition>();

    /// <summary>
    /// The row item (the cell value for a <c>PropertyName=""</c> column).
    /// Used as the default <c>CommandParameter</c> for any button whose
    /// <see cref="ActionButtonDefinition.CommandParameter"/> is <see langword="null"/>.
    /// </summary>
    public object? RowItem { get; set; }

    /// <summary>Corner radius for all button pills.</summary>
    public float CornerRadius { get; set; } = 5f;

    /// <summary>Horizontal gap between adjacent buttons (pixels).</summary>
    public float ButtonSpacing { get; set; } = 6f;

    /// <summary>Inset from all four cell edges (pixels).</summary>
    public float CellPadding { get; set; } = 4f;

    /// <inheritdoc />
    public override void OnDraw(IDrawingContext ctx)
    {
        if (Buttons.Count == 0) return;
        var rects = ActionButtonLayout.ComputeButtonRects(Bounds, Buttons.Count, CellPadding, ButtonSpacing);
        for (int i = 0; i < Buttons.Count; i++)
            DrawButton(ctx, rects[i], Buttons[i], pressed: i == _pressedIndex);
    }

    /// <inheritdoc />
    public override bool OnPointerDown(GridPointerEventArgs e)
    {
        var rects = ActionButtonLayout.ComputeButtonRects(Bounds, Buttons.Count, CellPadding, ButtonSpacing);
        for (int i = 0; i < rects.Count; i++)
        {
            if (rects[i].Contains(e.X, e.Y))
            {
                _pressedIndex = i;
                InvalidateVisual();
                return true;
            }
        }
        return false;
    }

    /// <inheritdoc />
    public override bool OnPointerUp(GridPointerEventArgs e)
    {
        int wasPressed = _pressedIndex;
        _pressedIndex = -1;
        InvalidateVisual();

        if (wasPressed < 0 || wasPressed >= Buttons.Count) return false;

        var rects = ActionButtonLayout.ComputeButtonRects(Bounds, Buttons.Count, CellPadding, ButtonSpacing);
        if (rects[wasPressed].Contains(e.X, e.Y))
        {
            var btn = Buttons[wasPressed];
            var param = btn.CommandParameter ?? RowItem;
            btn.Command?.Execute(param);
            btn.Action?.Invoke();
            RaiseEditCompleted();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Called by <c>CellEditorFactory.ApplyThemeToEditor</c> to adapt button
    /// colors to the active grid theme. Override to customize.
    /// </summary>
    public virtual void ApplyTheme(DataGridStyle style) { }

    // ── Drawing ──────────────────────────────────────────────────

    private void DrawButton(IDrawingContext ctx, GridRect rect, IActionButtonDefinition btn, bool pressed)
    {
        var bg = pressed
            ? new GridColor(
                (byte)Math.Max(0, btn.BackgroundColor.R - 40),
                (byte)Math.Max(0, btn.BackgroundColor.G - 40),
                (byte)Math.Max(0, btn.BackgroundColor.B - 40),
                btn.BackgroundColor.A)
            : btn.BackgroundColor;

        ctx.FillRoundRect(rect, CornerRadius, new GridPaint { Color = bg, IsAntiAlias = true });
        ctx.DrawTextInRect(btn.Label, rect, new GridPaint
        {
            Color = btn.TextColor,
            Font = new GridFont("Default", 11, bold: true),
            IsAntiAlias = true
        }, GridTextAlignment.Center, GridVerticalAlignment.Center);
    }
}


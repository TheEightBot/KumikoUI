using KumikoUI.Core.Input;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>
/// Fully custom-drawn checkbox with checked, unchecked, and indeterminate states.
/// </summary>
public class DrawnCheckBox : DrawnComponent
{
    private CheckState _state = CheckState.Unchecked;

    // ── Style ───────────────────────────────────────────────────

    /// <summary>Size (width and height) of the checkbox box in device-independent pixels.</summary>
    public float BoxSize { get; set; } = 18f;
    /// <summary>Stroke width of the box border outline.</summary>
    public float StrokeWidth { get; set; } = 1.5f;
    /// <summary>Stroke width used to draw the check mark inside the box.</summary>
    public float CheckStrokeWidth { get; set; } = 2f;
    /// <summary>Border color of the unchecked checkbox box.</summary>
    public GridColor BoxBorderColor { get; set; } = new(120, 120, 120);
    /// <summary>Fill color of the checkbox box when unchecked.</summary>
    public GridColor BoxFillColor { get; set; } = GridColor.White;
    /// <summary>Fill color of the checkbox box when checked.</summary>
    public GridColor CheckedBoxFillColor { get; set; } = new(0, 120, 215);
    /// <summary>Color of the check mark glyph drawn inside the box.</summary>
    public GridColor CheckMarkColor { get; set; } = GridColor.White;
    /// <summary>Color of the indeterminate-state dash drawn inside the box.</summary>
    public GridColor IndeterminateColor { get; set; } = new(0, 120, 215);
    /// <summary>Corner radius for the rounded checkbox box.</summary>
    public float CornerRadius { get; set; } = 3f;

    // ── Properties ──────────────────────────────────────────────

    /// <summary>Current check state.</summary>
    public CheckState State
    {
        get => _state;
        set
        {
            if (_state == value) return;
            var old = _state;
            _state = value;
            RaiseValueChanged(old, value);
            InvalidateVisual();
        }
    }

    /// <summary>Whether the checkbox is checked.</summary>
    public bool IsChecked
    {
        get => _state == CheckState.Checked;
        set => State = value ? CheckState.Checked : CheckState.Unchecked;
    }

    /// <summary>Whether to support tri-state (checked/unchecked/indeterminate).</summary>
    public bool IsThreeState { get; set; }

    // ── Events ──────────────────────────────────────────────────

    /// <summary>Raised when the check state changes via user interaction.</summary>
    public event EventHandler<CheckStateChangedEventArgs>? CheckStateChanged;

    // ── Drawing ─────────────────────────────────────────────────

    /// <inheritdoc />
    public override void OnDraw(IDrawingContext ctx)
    {
        var b = Bounds;
        // Center the checkbox box within bounds
        float boxX = b.X + (b.Width - BoxSize) / 2;
        float boxY = b.Y + (b.Height - BoxSize) / 2;
        var boxRect = new GridRect(boxX, boxY, BoxSize, BoxSize);

        // Draw box background
        var fillColor = _state == CheckState.Checked ? CheckedBoxFillColor : BoxFillColor;
        ctx.FillRoundRect(boxRect, CornerRadius, new GridPaint { Color = fillColor, Style = PaintStyle.Fill });

        // Draw box border
        ctx.DrawRoundRect(boxRect, CornerRadius, new GridPaint
        {
            Color = IsFocused ? new GridColor(0, 120, 215) : BoxBorderColor,
            Style = PaintStyle.Stroke,
            StrokeWidth = StrokeWidth
        });

        // Draw check mark or indeterminate dash
        switch (_state)
        {
            case CheckState.Checked:
                DrawCheckMark(ctx, boxX, boxY);
                break;
            case CheckState.Indeterminate:
                DrawIndeterminate(ctx, boxX, boxY);
                break;
        }
    }

    private void DrawCheckMark(IDrawingContext ctx, float boxX, float boxY)
    {
        var paint = new GridPaint
        {
            Color = CheckMarkColor,
            Style = PaintStyle.Stroke,
            StrokeWidth = CheckStrokeWidth
        };

        // Draw a checkmark (√) shape
        float x1 = boxX + BoxSize * 0.2f;
        float y1 = boxY + BoxSize * 0.5f;
        float x2 = boxX + BoxSize * 0.4f;
        float y2 = boxY + BoxSize * 0.75f;
        float x3 = boxX + BoxSize * 0.8f;
        float y3 = boxY + BoxSize * 0.25f;

        ctx.DrawLine(x1, y1, x2, y2, paint);
        ctx.DrawLine(x2, y2, x3, y3, paint);
    }

    private void DrawIndeterminate(IDrawingContext ctx, float boxX, float boxY)
    {
        var paint = new GridPaint
        {
            Color = IndeterminateColor,
            Style = PaintStyle.Fill
        };

        float dashW = BoxSize * 0.5f;
        float dashH = 3f;
        float dashX = boxX + (BoxSize - dashW) / 2;
        float dashY = boxY + (BoxSize - dashH) / 2;
        ctx.FillRect(new GridRect(dashX, dashY, dashW, dashH), paint);
    }

    // ── Input ───────────────────────────────────────────────────

    /// <inheritdoc />
    public override bool OnPointerUp(GridPointerEventArgs e)
    {
        if (!IsEnabled) return false;
        Toggle();
        return true;
    }

    /// <inheritdoc />
    public override bool OnKeyDown(GridKeyEventArgs e)
    {
        if (e.Key == GridKey.Space || e.Key == GridKey.Enter)
        {
            Toggle();
            return true;
        }
        return false;
    }

    /// <summary>Toggle to the next state.</summary>
    public void Toggle()
    {
        var old = _state;
        _state = IsThreeState
            ? _state switch
            {
                CheckState.Unchecked => CheckState.Checked,
                CheckState.Checked => CheckState.Indeterminate,
                CheckState.Indeterminate => CheckState.Unchecked,
                _ => CheckState.Unchecked
            }
            : _state == CheckState.Checked ? CheckState.Unchecked : CheckState.Checked;

        CheckStateChanged?.Invoke(this, new CheckStateChangedEventArgs(old, _state));
        RaiseValueChanged(old, _state);
        InvalidateVisual();
    }
}

/// <summary>Check state for a checkbox.</summary>
public enum CheckState
{
    /// <summary>The checkbox is unchecked.</summary>
    Unchecked,
    /// <summary>The checkbox is checked.</summary>
    Checked,
    /// <summary>The checkbox is in an indeterminate (tri-state) state.</summary>
    Indeterminate
}

/// <summary>Check state changed event args.</summary>
public class CheckStateChangedEventArgs : EventArgs
{
    /// <summary>The previous check state before the change.</summary>
    public CheckState OldState { get; }
    /// <summary>The new check state after the change.</summary>
    public CheckState NewState { get; }
    /// <summary>Initializes a new instance of the <see cref="CheckStateChangedEventArgs"/> class.</summary>
    /// <param name="oldState">The previous check state.</param>
    /// <param name="newState">The new check state.</param>
    public CheckStateChangedEventArgs(CheckState oldState, CheckState newState)
    {
        OldState = oldState;
        NewState = newState;
    }
}

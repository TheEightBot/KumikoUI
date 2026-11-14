using KumikoUI.Core.Input;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>
/// Fully custom-drawn checkbox with checked, unchecked, and indeterminate states.
/// </summary>
internal class DrawnCheckBox : DrawnComponent
{
    private DrawnCheckState _state = DrawnCheckState.Unchecked;

    // ── Style ───────────────────────────────────────────────────

    /// <summary>Size (width and height) of the checkbox box in device-independent pixels.</summary>
    public float BoxSize { get; set; } = 18f;
    /// <summary>Stroke width of the box border outline.</summary>
    public float StrokeWidth { get; set; } = 1.5f;
    /// <summary>Stroke width used to draw the check mark inside the box.</summary>
    public float CheckStrokeWidth { get; set; } = 2f;
    /// <summary>Border color of the unchecked checkbox box.</summary>
    public KumikoColor BoxBorderColor { get; set; } = new(120, 120, 120);
    /// <summary>Fill color of the checkbox box when unchecked.</summary>
    public KumikoColor BoxFillColor { get; set; } = KumikoColor.White;
    /// <summary>Fill color of the checkbox box when checked.</summary>
    public KumikoColor CheckedBoxFillColor { get; set; } = new(0, 120, 215);
    /// <summary>Color of the check mark glyph drawn inside the box.</summary>
    public KumikoColor CheckMarkColor { get; set; } = KumikoColor.White;
    /// <summary>Color of the indeterminate-state dash drawn inside the box.</summary>
    public KumikoColor IndeterminateColor { get; set; } = new(0, 120, 215);
    /// <summary>Corner radius for the rounded checkbox box.</summary>
    public float CornerRadius { get; set; } = 3f;

    // ── Properties ──────────────────────────────────────────────

    /// <summary>Current check state.</summary>
    public DrawnCheckState State
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
        get => _state == DrawnCheckState.Checked;
        set => State = value ? DrawnCheckState.Checked : DrawnCheckState.Unchecked;
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
        var boxRect = new KumikoRect(boxX, boxY, BoxSize, BoxSize);

        // Draw box background
        var fillColor = _state == DrawnCheckState.Checked ? CheckedBoxFillColor : BoxFillColor;
        ctx.FillRoundRect(boxRect, CornerRadius, Paints.Get(color: fillColor));

        // Draw box border
        ctx.DrawRoundRect(boxRect, CornerRadius, Paints.Get(color: IsFocused ? new KumikoColor(0, 120, 215) : BoxBorderColor, strokeWidth: StrokeWidth));

        // Check mark or indeterminate mark
        switch (_state)
        {
            case DrawnCheckState.Checked:
                IconProvider.DrawIcon(ctx, KumikoIcon.Check, boxRect, Paints.Get(color: CheckMarkColor, strokeWidth: CheckStrokeWidth, isAntiAlias: true));
                break;
            case DrawnCheckState.Indeterminate:
                IconProvider.DrawIcon(ctx, KumikoIcon.Indeterminate, boxRect, Paints.Get(color: IndeterminateColor, isAntiAlias: true));
                break;
        }
    }

    // ── Input ───────────────────────────────────────────────────

    /// <inheritdoc />
    public override bool OnPointerUp(KumikoPointerEventArgs e)
    {
        if (!IsEnabled) return false;
        Toggle();
        return true;
    }

    /// <inheritdoc />
    public override bool OnKeyDown(KumikoKeyEventArgs e)
    {
        if (e.Key == KumikoKey.Space || e.Key == KumikoKey.Enter)
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
                DrawnCheckState.Unchecked => DrawnCheckState.Checked,
                DrawnCheckState.Checked => DrawnCheckState.Indeterminate,
                DrawnCheckState.Indeterminate => DrawnCheckState.Unchecked,
                _ => DrawnCheckState.Unchecked
            }
            : _state == DrawnCheckState.Checked ? DrawnCheckState.Unchecked : DrawnCheckState.Checked;

        CheckStateChanged?.Invoke(this, new CheckStateChangedEventArgs(old, _state));
        RaiseValueChanged(old, _state);
        InvalidateVisual();
    }
}

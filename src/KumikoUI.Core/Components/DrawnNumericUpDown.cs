using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>
/// Fully custom-drawn numeric up/down control with side-by-side [−] value [+] layout.
/// Sized for touch (44pt minimum recommended button size on mobile).
/// </summary>
public class DrawnNumericUpDown : DrawnComponent
{
    private double _value;
    private long _repeatTimerStart;
    private long _lastRepeatTick;
    private bool _isIncrementing;
    private bool _isDecrementing;

    // ── Style ───────────────────────────────────────────────────

    /// <summary>Font used to render the numeric value text.</summary>
    public GridFont Font { get; set; } = new("Default", 14);
    /// <summary>Color of the displayed numeric value text.</summary>
    public GridColor TextColor { get; set; } = new(30, 30, 30);
    /// <summary>Background fill color of the control.</summary>
    public GridColor BackgroundColor { get; set; } = GridColor.White;
    /// <summary>Border color of the control when not focused.</summary>
    public GridColor BorderColor { get; set; } = new(180, 180, 180);
    /// <summary>Border color of the control when focused.</summary>
    public GridColor FocusedBorderColor { get; set; } = new(0, 120, 215);
    /// <summary>Background color of the increment and decrement buttons.</summary>
    public GridColor ButtonBackgroundColor { get; set; } = new(240, 240, 240);
    /// <summary>Color of the plus and minus symbols on the buttons.</summary>
    public GridColor ButtonSymbolColor { get; set; } = new(60, 60, 60);
    /// <summary>Background color of a button while it is being pressed.</summary>
    public GridColor ButtonPressedColor { get; set; } = new(200, 200, 200);
    /// <summary>Inner padding between the control border and its content.</summary>
    public float Padding { get; set; } = 4f;

    /// <summary>
    /// Width of each [−] / [+] button. Auto-sized to fill available height
    /// when set to 0 (ensures square touch targets).
    /// </summary>
    public float ButtonWidth { get; set; }

    /// <summary>Corner radius for the outer shape and buttons.</summary>
    public float CornerRadius { get; set; } = 4f;

    // ── Theming ─────────────────────────────────────────────────

    /// <summary>
    /// Applies theme colors from a <see cref="DataGridStyle"/> so the numeric
    /// up/down visually matches the current grid theme.
    /// </summary>
    public void ApplyTheme(DataGridStyle style)
    {
        var bg = style.BackgroundColor;
        var txt = style.CellTextColor;

        TextColor = txt;
        BackgroundColor = bg;
        BorderColor = style.GridLineColor;
        FocusedBorderColor = style.AccentColor;

        // Button background: slight shift from main background toward text
        ButtonBackgroundColor = new GridColor(
            (byte)(bg.R + (txt.R - bg.R) * 0.08),
            (byte)(bg.G + (txt.G - bg.G) * 0.08),
            (byte)(bg.B + (txt.B - bg.B) * 0.08));

        ButtonSymbolColor = txt;

        // Button pressed: stronger shift toward text
        ButtonPressedColor = new GridColor(
            (byte)(bg.R + (txt.R - bg.R) * 0.2),
            (byte)(bg.G + (txt.G - bg.G) * 0.2),
            (byte)(bg.B + (txt.B - bg.B) * 0.2));
    }

    // ── Properties ──────────────────────────────────────────────

    /// <summary>Current value.</summary>
    public double Value
    {
        get => _value;
        set
        {
            double clamped = Math.Clamp(value, Minimum, Maximum);
            if (Math.Abs(_value - clamped) < double.Epsilon) return;
            var old = _value;
            _value = clamped;
            RaiseValueChanged(old, _value);
            InvalidateVisual();
        }
    }

    /// <summary>Minimum value.</summary>
    public double Minimum { get; set; } = double.MinValue;

    /// <summary>Maximum value.</summary>
    public double Maximum { get; set; } = double.MaxValue;

    /// <summary>Increment/decrement step.</summary>
    public double Step { get; set; } = 1;

    /// <summary>Display format string.</summary>
    public string Format { get; set; } = "G";

    /// <summary>Number of decimal places (-1 = auto).</summary>
    public int DecimalPlaces { get; set; } = -1;

    /// <summary>Is the control read-only?</summary>
    public bool IsReadOnly { get; set; }

    /// <summary>Auto-repeat interval in ms when holding button.</summary>
    public int RepeatIntervalMs { get; set; } = 100;

    /// <summary>Delay before auto-repeat starts in ms.</summary>
    public int RepeatDelayMs { get; set; } = 400;

    // ── Layout helpers ──────────────────────────────────────────

    private float GetButtonWidth()
    {
        if (ButtonWidth > 0) return ButtonWidth;
        // Auto: use height as width to make square touch targets
        return Math.Max(Bounds.Height, 36f);
    }

    private GridRect GetMinusButtonRect()
    {
        var b = Bounds;
        float bw = GetButtonWidth();
        return new GridRect(b.X, b.Y, bw, b.Height);
    }

    private GridRect GetPlusButtonRect()
    {
        var b = Bounds;
        float bw = GetButtonWidth();
        return new GridRect(b.Right - bw, b.Y, bw, b.Height);
    }

    private GridRect GetValueRect()
    {
        var b = Bounds;
        float bw = GetButtonWidth();
        return new GridRect(b.X + bw, b.Y, b.Width - bw * 2, b.Height);
    }

    // ── Drawing ─────────────────────────────────────────────────

    /// <inheritdoc />
    public override void OnDraw(IDrawingContext ctx)
    {
        var b = Bounds;

        // Outer background + border
        ctx.FillRoundRect(b, CornerRadius, new GridPaint { Color = BackgroundColor });
        ctx.DrawRoundRect(b, CornerRadius, new GridPaint
        {
            Color = IsFocused ? FocusedBorderColor : BorderColor,
            Style = PaintStyle.Stroke,
            StrokeWidth = 1
        });

        var minusRect = GetMinusButtonRect();
        var plusRect = GetPlusButtonRect();
        var valueRect = GetValueRect();

        // [−] button
        ctx.FillRect(minusRect, new GridPaint
        {
            Color = _isDecrementing ? ButtonPressedColor : ButtonBackgroundColor
        });
        ctx.DrawRect(minusRect, new GridPaint
        {
            Color = BorderColor, Style = PaintStyle.Stroke, StrokeWidth = 0.5f
        });
        DrawMinus(ctx, minusRect);

        // [+] button
        ctx.FillRect(plusRect, new GridPaint
        {
            Color = _isIncrementing ? ButtonPressedColor : ButtonBackgroundColor
        });
        ctx.DrawRect(plusRect, new GridPaint
        {
            Color = BorderColor, Style = PaintStyle.Stroke, StrokeWidth = 0.5f
        });
        DrawPlus(ctx, plusRect);

        // Value text (centered between buttons)
        string text = FormatValue();
        var textPaint = new GridPaint
        {
            Color = TextColor, Style = PaintStyle.Fill, Font = Font, IsAntiAlias = true
        };
        ctx.DrawTextInRect(text, valueRect, textPaint,
            GridTextAlignment.Center, GridVerticalAlignment.Center);
    }

    private void DrawMinus(IDrawingContext ctx, GridRect r)
    {
        var paint = new GridPaint
        {
            Color = ButtonSymbolColor, Style = PaintStyle.Stroke,
            StrokeWidth = 2f, IsAntiAlias = true
        };
        float cx = r.X + r.Width / 2;
        float cy = r.Y + r.Height / 2;
        float armLen = Math.Min(r.Width, r.Height) * 0.22f;
        ctx.DrawLine(cx - armLen, cy, cx + armLen, cy, paint);
    }

    private void DrawPlus(IDrawingContext ctx, GridRect r)
    {
        var paint = new GridPaint
        {
            Color = ButtonSymbolColor, Style = PaintStyle.Stroke,
            StrokeWidth = 2f, IsAntiAlias = true
        };
        float cx = r.X + r.Width / 2;
        float cy = r.Y + r.Height / 2;
        float armLen = Math.Min(r.Width, r.Height) * 0.22f;
        ctx.DrawLine(cx - armLen, cy, cx + armLen, cy, paint);
        ctx.DrawLine(cx, cy - armLen, cx, cy + armLen, paint);
    }

    private string FormatValue()
    {
        if (DecimalPlaces >= 0)
            return _value.ToString($"F{DecimalPlaces}");
        return _value.ToString(Format);
    }

    // ── Input ───────────────────────────────────────────────────

    /// <inheritdoc />
    public override bool OnPointerDown(GridPointerEventArgs e)
    {
        if (!IsEnabled || IsReadOnly) return false;

        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if (GetMinusButtonRect().Contains(e.X, e.Y))
        {
            Decrement();
            _isDecrementing = true;
            _repeatTimerStart = now;
            _lastRepeatTick = now;
            return true;
        }

        if (GetPlusButtonRect().Contains(e.X, e.Y))
        {
            Increment();
            _isIncrementing = true;
            _repeatTimerStart = now;
            _lastRepeatTick = now;
            return true;
        }

        // Tap on value area — claim it but don't do anything special
        return true;
    }

    /// <inheritdoc />
    public override bool OnPointerUp(GridPointerEventArgs e)
    {
        _isIncrementing = false;
        _isDecrementing = false;
        InvalidateVisual();
        return true;
    }

    /// <summary>
    /// Tick the auto-repeat timer. Call this from a frame timer (~60fps) while
    /// a spin button is held down. Handles initial delay then repeated firing.
    /// Returns true if still repeating (button still held).
    /// </summary>
    public bool UpdateAutoRepeat()
    {
        if (!_isIncrementing && !_isDecrementing) return false;

        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long elapsed = now - _repeatTimerStart;

        if (elapsed < RepeatDelayMs) return true; // Still in initial delay

        long repeatElapsed = elapsed - RepeatDelayMs;
        long expectedTicks = repeatElapsed / RepeatIntervalMs;
        long currentTick = (_lastRepeatTick - _repeatTimerStart - RepeatDelayMs);
        if (currentTick < 0) currentTick = -RepeatIntervalMs;
        long currentTickCount = currentTick / RepeatIntervalMs;

        if (expectedTicks > currentTickCount)
        {
            _lastRepeatTick = now;
            if (_isIncrementing) Increment();
            else if (_isDecrementing) Decrement();
        }

        return true;
    }

    /// <inheritdoc />
    public override bool OnKeyDown(GridKeyEventArgs e)
    {
        if (IsReadOnly) return false;

        switch (e.Key)
        {
            case GridKey.Up:
                Increment();
                return true;
            case GridKey.Down:
                Decrement();
                return true;
            case GridKey.PageUp:
                Value += Step * 10;
                return true;
            case GridKey.PageDown:
                Value -= Step * 10;
                return true;
            case GridKey.Home:
                if (Maximum < double.MaxValue) Value = Maximum;
                return true;
            case GridKey.End:
                if (Minimum > double.MinValue) Value = Minimum;
                return true;
        }
        return false;
    }

    /// <summary>Increment the value by one step.</summary>
    public void Increment() => Value += Step;

    /// <summary>Decrement the value by one step.</summary>
    public void Decrement() => Value -= Step;
}

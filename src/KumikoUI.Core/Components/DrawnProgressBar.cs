using KumikoUI.Core.Input;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>
/// Fully custom-drawn progress bar / slider.
/// Can be used as a read-only progress indicator or interactive slider.
/// </summary>
public class DrawnProgressBar : DrawnComponent
{
    private double _value;
    private bool _isDragging;

    // ── Style ───────────────────────────────────────────────────

    /// <summary>Background color of the unfilled portion of the track.</summary>
    public GridColor TrackColor { get; set; } = new(220, 220, 220);
    /// <summary>Color of the filled portion of the progress track.</summary>
    public GridColor FillColor { get; set; } = new(0, 120, 215);
    /// <summary>Color of the draggable thumb handle.</summary>
    public GridColor ThumbColor { get; set; } = new(0, 100, 200);
    /// <summary>Border color of the progress bar track.</summary>
    public GridColor BorderColor { get; set; } = new(180, 180, 180);
    /// <summary>Height of the progress track in device-independent pixels.</summary>
    public float TrackHeight { get; set; } = 6f;
    /// <summary>Radius of the circular thumb handle.</summary>
    public float ThumbRadius { get; set; } = 8f;
    /// <summary>Corner radius for the rounded track ends.</summary>
    public float CornerRadius { get; set; } = 3f;

    // ── Properties ──────────────────────────────────────────────

    /// <summary>Current value (between Minimum and Maximum).</summary>
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
    public double Minimum { get; set; }

    /// <summary>Maximum value.</summary>
    public double Maximum { get; set; } = 100;

    /// <summary>Whether the user can drag to change value (slider mode).</summary>
    public bool IsInteractive { get; set; }

    /// <summary>Whether to show a thumb handle.</summary>
    public bool ShowThumb { get; set; }

    // ── Drawing ─────────────────────────────────────────────────

    /// <inheritdoc />
    public override void OnDraw(IDrawingContext ctx)
    {
        var b = Bounds;
        float trackTop = b.Y + (b.Height - TrackHeight) / 2;
        var trackRect = new GridRect(b.X, trackTop, b.Width, TrackHeight);

        // Track background
        ctx.FillRoundRect(trackRect, CornerRadius, new GridPaint { Color = TrackColor, Style = PaintStyle.Fill });

        // Fill
        double range = Maximum - Minimum;
        float fraction = range > 0 ? (float)((_value - Minimum) / range) : 0;
        float fillWidth = b.Width * fraction;
        if (fillWidth > 0)
        {
            var fillRect = new GridRect(b.X, trackTop, fillWidth, TrackHeight);
            ctx.FillRoundRect(fillRect, CornerRadius, new GridPaint { Color = FillColor, Style = PaintStyle.Fill });
        }

        // Thumb
        if (ShowThumb || IsInteractive)
        {
            float thumbX = b.X + fillWidth;
            float thumbY = b.Y + b.Height / 2;
            // Draw thumb as a filled circle approximated with a round rect
            var thumbRect = new GridRect(thumbX - ThumbRadius, thumbY - ThumbRadius, ThumbRadius * 2, ThumbRadius * 2);
            ctx.FillRoundRect(thumbRect, ThumbRadius, new GridPaint { Color = ThumbColor, Style = PaintStyle.Fill });

            if (IsFocused)
            {
                ctx.DrawRoundRect(thumbRect, ThumbRadius, new GridPaint
                {
                    Color = new GridColor(0, 120, 215),
                    Style = PaintStyle.Stroke,
                    StrokeWidth = 2
                });
            }
        }
    }

    // ── Input ───────────────────────────────────────────────────

    /// <inheritdoc />
    public override bool OnPointerDown(GridPointerEventArgs e)
    {
        if (!IsInteractive || !IsEnabled) return false;
        _isDragging = true;
        UpdateValueFromPointer(e.X);
        return true;
    }

    /// <inheritdoc />
    public override bool OnPointerMove(GridPointerEventArgs e)
    {
        if (!_isDragging) return false;
        UpdateValueFromPointer(e.X);
        return true;
    }

    /// <inheritdoc />
    public override bool OnPointerUp(GridPointerEventArgs e)
    {
        _isDragging = false;
        return true;
    }

    /// <inheritdoc />
    public override bool OnKeyDown(GridKeyEventArgs e)
    {
        if (!IsInteractive) return false;

        double step = (Maximum - Minimum) / 20;
        switch (e.Key)
        {
            case GridKey.Left:
            case GridKey.Down:
                Value -= step;
                return true;
            case GridKey.Right:
            case GridKey.Up:
                Value += step;
                return true;
        }
        return false;
    }

    private void UpdateValueFromPointer(float x)
    {
        float fraction = Math.Clamp((x - Bounds.X) / Bounds.Width, 0, 1);
        Value = Minimum + fraction * (Maximum - Minimum);
    }
}

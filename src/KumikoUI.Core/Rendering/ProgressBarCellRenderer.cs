using KumikoUI.Core.Models;

namespace KumikoUI.Core.Rendering;

/// <summary>
/// Renders a progress bar / gauge inside a cell.
/// Interprets the cell value as a numeric percentage between <see cref="Minimum"/> and <see cref="Maximum"/>.
/// </summary>
public class ProgressBarCellRenderer : ICellRenderer
{
    /// <summary>Minimum value (left edge).</summary>
    public double Minimum { get; set; }

    /// <summary>Maximum value (right edge).</summary>
    public double Maximum { get; set; } = 100;

    /// <summary>Height of the track in pixels. If 0, auto-sizes to cell height minus padding.</summary>
    public float TrackHeight { get; set; } = 10f;

    /// <summary>Corner radius of the track.</summary>
    public float CornerRadius { get; set; } = 4f;

    /// <summary>Track background color.</summary>
    public GridColor TrackColor { get; set; } = new(220, 220, 220);

    /// <summary>Fill color. When null, uses a color gradient based on value percentage.</summary>
    public GridColor? FillColor { get; set; }

    /// <summary>Whether to show the numeric value text overlaid on the bar.</summary>
    public bool ShowText { get; set; } = true;

    /// <summary>Format string for the text label (e.g., "N1", "P0").</summary>
    public string TextFormat { get; set; } = "N0";

    /// <summary>
    /// Optional delegate to pick fill color based on value. Receives (value, min, max).
    /// When null, uses <see cref="FillColor"/> or a red → amber → green gradient.
    /// </summary>
    public Func<double, double, double, GridColor>? ColorSelector { get; set; }

    public void Render(
        IDrawingContext ctx, GridRect cellRect, object? value,
        string displayText, KumikoUI.Core.Models.DataGridColumn column,
        DataGridStyle style, bool isSelected,
        CellStyle? cellStyle = null)
    {
        double numValue = 0;
        if (value is IConvertible conv)
        {
            try { numValue = conv.ToDouble(null); }
            catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException) { /* leave at 0 */ }
        }

        double range = Maximum - Minimum;
        double pct = range > 0 ? Math.Clamp((numValue - Minimum) / range, 0, 1) : 0;

        // Layout
        float pad = 4f;
        float trackH = TrackHeight > 0 ? TrackHeight : cellRect.Height - pad * 2;
        float trackY = cellRect.Y + (cellRect.Height - trackH) / 2;
        var trackRect = new GridRect(cellRect.X + pad, trackY, cellRect.Width - pad * 2, trackH);

        // Track background
        ctx.FillRoundRect(trackRect, CornerRadius, new GridPaint { Color = TrackColor });

        // Fill
        float fillWidth = trackRect.Width * (float)pct;
        if (fillWidth > 0.5f)
        {
            var fillColor = ColorSelector != null
                ? ColorSelector(numValue, Minimum, Maximum)
                : FillColor ?? DefaultGradientColor(pct);
            // Clip to track bounds then draw a rounded rect that extends beyond right edge
            // so only the left portion with fill width is visible
            ctx.Save();
            ctx.ClipRect(new GridRect(trackRect.X, trackRect.Y, fillWidth, trackRect.Height));
            ctx.FillRoundRect(trackRect, CornerRadius, new GridPaint { Color = fillColor });
            ctx.Restore();
        }

        // Text overlay
        if (ShowText)
        {
            string text = numValue.ToString(TextFormat);
            var textPaint = new GridPaint
            {
                Color = pct > 0.45 ? GridColor.White : style.CellTextColor,
                Font = new GridFont(style.CellFont.Family, Math.Max(9, style.CellFont.Size - 1)),
                IsAntiAlias = true
            };
            ctx.DrawTextInRect(text, trackRect, textPaint,
                GridTextAlignment.Center, GridVerticalAlignment.Center);
        }
    }

    private static GridColor DefaultGradientColor(double pct)
    {
        return pct switch
        {
            >= 0.8 => new GridColor(40, 167, 70),
            >= 0.5 => new GridColor(255, 193, 7),
            _ => new GridColor(220, 53, 69)
        };
    }
}

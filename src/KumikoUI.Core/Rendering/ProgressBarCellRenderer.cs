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
    public KumikoColor TrackColor { get; set; } = new(220, 220, 220);

    /// <summary>Fill color. When null, uses a color gradient based on value percentage.</summary>
    public KumikoColor? FillColor { get; set; }

    /// <summary>Whether to show the numeric value text overlaid on the bar.</summary>
    public bool ShowText { get; set; } = true;

    /// <summary>Format string for the text label (e.g., "N1", "P0").</summary>
    public string TextFormat { get; set; } = "N0";

    /// <summary>
    /// Optional delegate to pick fill color based on value. Receives (value, min, max).
    /// When null, uses <see cref="FillColor"/> or a red → amber → green gradient.
    /// </summary>
    public Func<double, double, double, KumikoColor>? ColorSelector { get; set; }

    // Reused for every cell (only the color varies), so drawing a bar allocates nothing.
    private readonly KumikoPaint _trackPaint = new();
    private readonly KumikoPaint _fillPaint = new();
    private readonly KumikoPaint _textPaint = new() { IsAntiAlias = true };

    // The label font, derived from the style's cell font (one point smaller, at least 9) when that changes.
    private KumikoFont? _textFont;
    private string? _textFontFamily;
    private float _textFontSourceSize = float.NaN;

    // Formatted labels by value, so a value shown again is not formatted again each frame. Built for one format
    // and culture; emptied when either changes, or when it holds more values than a screen shows.
    private const int MaxLabels = 512;
    private readonly Dictionary<double, string> _labels = new();
    private string? _labelsFormat;
    private System.Globalization.CultureInfo? _labelsCulture;

    /// <inheritdoc />
    public void Render(IDrawingContext ctx, in CellRenderContext cell)
    {
        var cellRect = cell.Bounds;
        var style = cell.Style;
        double numValue = ToDouble(cell.Value);

        double range = Maximum - Minimum;
        double pct = range > 0 ? Math.Clamp((numValue - Minimum) / range, 0, 1) : 0;

        // Layout
        float pad = 4f;
        float trackH = TrackHeight > 0 ? TrackHeight : cellRect.Height - pad * 2;
        float trackY = cellRect.Y + (cellRect.Height - trackH) / 2;
        var trackRect = new KumikoRect(cellRect.X + pad, trackY, cellRect.Width - pad * 2, trackH);

        // Track background
        _trackPaint.Color = TrackColor;
        ctx.FillRoundRect(trackRect, CornerRadius, _trackPaint);

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
            ctx.ClipRect(new KumikoRect(trackRect.X, trackRect.Y, fillWidth, trackRect.Height));
            _fillPaint.Color = fillColor;
            ctx.FillRoundRect(trackRect, CornerRadius, _fillPaint);
            ctx.Restore();
        }

        // Text overlay
        if (ShowText)
        {
            string text = Label(numValue);
            _textPaint.Color = pct > 0.45 ? KumikoColor.White : style.CellTextColor;
            _textPaint.Font = TextFont(style.CellFont);
            ctx.DrawTextInRect(text, trackRect, _textPaint,
                KumikoTextAlignment.Center, KumikoVerticalAlignment.Center);
        }
    }

    /// <summary>The value as a number (0 when it is not one), without throwing for text.</summary>
    private static double ToDouble(object? value)
    {
        // Text parses the way IConvertible.ToDouble would, but without an exception per frame when it is not a number.
        if (value is string s)
            return double.TryParse(s, System.Globalization.NumberStyles.Float | System.Globalization.NumberStyles.AllowThousands,
                null, out double parsed) ? parsed : 0;

        if (value is IConvertible conv)
        {
            try { return conv.ToDouble(null); }
            catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException) { /* leave at 0 */ }
        }
        return 0;
    }

    /// <summary>The value formatted with <see cref="TextFormat"/> in the current culture, reusing a recent result.</summary>
    private string Label(double value)
    {
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        if (!ReferenceEquals(_labelsFormat, TextFormat) || !ReferenceEquals(_labelsCulture, culture))
        {
            _labels.Clear();
            _labelsFormat = TextFormat;
            _labelsCulture = culture;
        }

        if (_labels.TryGetValue(value, out var text))
            return text;

        if (_labels.Count >= MaxLabels)
            _labels.Clear();
        text = value.ToString(TextFormat, culture);
        _labels[value] = text;
        return text;
    }

    /// <summary>The label font: one point smaller than <paramref name="cellFont"/>, but not below 9.</summary>
    private KumikoFont TextFont(KumikoFont cellFont)
    {
        if (_textFont == null || !ReferenceEquals(cellFont.Family, _textFontFamily) || !cellFont.Size.Equals(_textFontSourceSize)) // exact: cache change detection
        {
            _textFont = new KumikoFont(cellFont.Family, Math.Max(9, cellFont.Size - 1));
            _textFontFamily = cellFont.Family;
            _textFontSourceSize = cellFont.Size;
        }
        return _textFont;
    }

    private static KumikoColor DefaultGradientColor(double pct)
    {
        return pct switch
        {
            >= 0.8 => new KumikoColor(40, 167, 70),
            >= 0.5 => new KumikoColor(255, 193, 7),
            _ => new KumikoColor(220, 53, 69)
        };
    }
}

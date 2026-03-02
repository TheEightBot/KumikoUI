namespace KumikoUI.Core.Rendering;

using KumikoUI.Core.Models;

/// <summary>
/// Interface for rendering individual cell content.
/// Implementations are dispatched by <see cref="DataGridColumnType"/>.
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
        GridRect cellRect,
        object? value,
        string displayText,
        DataGridColumn column,
        DataGridStyle style,
        bool isSelected,
        CellStyle? cellStyle = null);
}

/// <summary>
/// Renders text content (Text, Numeric, Date column types).
/// </summary>
public class TextCellRenderer : ICellRenderer
{
    public void Render(
        IDrawingContext ctx, GridRect cellRect, object? value,
        string displayText, DataGridColumn column,
        DataGridStyle style, bool isSelected,
        CellStyle? cellStyle = null)
    {
        var paint = new GridPaint
        {
            Color = cellStyle?.TextColor ?? style.CellTextColor,
            Font = cellStyle?.Font ?? style.CellFont,
            IsAntiAlias = true
        };

        var alignment = cellStyle?.TextAlignment ?? column.TextAlignment;
        ctx.DrawTextInRect(displayText, cellRect, paint,
            alignment, GridVerticalAlignment.Center);
    }
}

/// <summary>
/// Renders boolean values as custom-drawn checkboxes.
/// </summary>
public class BooleanCellRenderer : ICellRenderer
{
    public void Render(
        IDrawingContext ctx, GridRect cellRect, object? value,
        string displayText, DataGridColumn column,
        DataGridStyle style, bool isSelected,
        CellStyle? cellStyle = null)
    {
        bool isChecked = value is true
            || string.Equals(displayText, "True", StringComparison.OrdinalIgnoreCase);

        float boxSize = Math.Min(16f, cellRect.Height - 8);
        float centerY = cellRect.Y + (cellRect.Height - boxSize) / 2;
        float centerX = cellRect.X + (cellRect.Width - boxSize) / 2;
        var boxRect = new GridRect(centerX, centerY, boxSize, boxSize);

        // Checkbox border
        ctx.DrawRect(boxRect, new GridPaint
        {
            Color = style.GridLineColor,
            Style = PaintStyle.Stroke,
            StrokeWidth = 1.5f,
            IsAntiAlias = true
        });

        // Checkmark
        if (isChecked)
        {
            var checkPaint = new GridPaint
            {
                Color = style.AccentColor,
                Style = PaintStyle.Stroke,
                StrokeWidth = 2f,
                IsAntiAlias = true
            };
            float pad = boxSize * 0.2f;
            ctx.DrawLine(
                boxRect.X + pad, boxRect.Y + boxSize * 0.5f,
                boxRect.X + boxSize * 0.4f, boxRect.Bottom - pad,
                checkPaint);
            ctx.DrawLine(
                boxRect.X + boxSize * 0.4f, boxRect.Bottom - pad,
                boxRect.Right - pad, boxRect.Y + pad,
                checkPaint);
        }
    }
}

/// <summary>
/// Renders images in cells using <see cref="IDrawingContext.DrawImage"/>.
/// The cell value should be a platform-specific image object
/// (e.g., SKBitmap for SkiaSharp, or a URI string that has been loaded).
/// </summary>
public class ImageCellRenderer : ICellRenderer
{
    /// <summary>
    /// Optional delegate to resolve a cell value to a platform-specific image object.
    /// If not set, the raw cell value is passed to DrawImage directly.
    /// </summary>
    public Func<object?, object?>? ImageResolver { get; set; }

    public void Render(
        IDrawingContext ctx, GridRect cellRect, object? value,
        string displayText, DataGridColumn column,
        DataGridStyle style, bool isSelected,
        CellStyle? cellStyle = null)
    {
        var image = ImageResolver != null ? ImageResolver(value) : value;
        if (image == null) return;

        // Fit the image within the cell, maintaining aspect ratio,
        // centered in the cell rect
        float maxSize = Math.Min(cellRect.Width, cellRect.Height) - 4;
        if (maxSize <= 0) return;

        float imgX = cellRect.X + (cellRect.Width - maxSize) / 2;
        float imgY = cellRect.Y + (cellRect.Height - maxSize) / 2;

        ctx.DrawImage(image, new GridRect(imgX, imgY, maxSize, maxSize));
    }
}

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
            catch { /* leave at 0 */ }
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

using KumikoUI.Core.Rendering;
using SkiaSharp;

namespace KumikoUI.SkiaSharp;

/// <summary>
/// SkiaSharp implementation of <see cref="IDrawingContext"/>.
/// Maps all platform-independent drawing calls to SKCanvas operations.
/// Uses the modern SKFont-based text APIs (SkiaSharp 3.x).
///
/// <para><b>Performance:</b> Caches all native SkiaSharp objects (SKPaint, SKFont, SKTypeface)
/// per frame to avoid creating and disposing native resources on every draw call.
/// Call <see cref="Dispose"/> at the end of each frame to release cached resources.</para>
/// </summary>
public class SkiaDrawingContext : IDrawingContext, IDisposable
{
    private readonly SKCanvas _canvas;

    // ── Native object caches (keyed by configuration) ────────────
    // These avoid creating + disposing SKPaint/SKFont/SKTypeface per draw call.
    // Instead, identical configurations reuse the same native object within a frame.

    private readonly Dictionary<PaintKey, SKPaint> _paintCache = new();
    private readonly Dictionary<FontKey, SKFont> _fontCache = new();
    private readonly Dictionary<TypefaceKey, SKTypeface> _typefaceCache = new();

    /// <summary>Cache key for SKPaint objects.</summary>
    private readonly record struct PaintKey(uint Color, float StrokeWidth, SKPaintStyle Style, bool IsAntialias);

    /// <summary>Cache key for SKFont objects.</summary>
    private readonly record struct FontKey(string Family, float Size, bool IsBold, bool IsItalic);

    /// <summary>Cache key for SKTypeface objects.</summary>
    private readonly record struct TypefaceKey(string Family, bool IsBold, bool IsItalic);

    /// <summary>
    /// Initializes a new <see cref="SkiaDrawingContext"/> that renders onto the specified <see cref="SKCanvas"/>.
    /// </summary>
    /// <param name="canvas">The SkiaSharp canvas to draw on. Must not be <c>null</c>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="canvas"/> is <c>null</c>.</exception>
    public SkiaDrawingContext(SKCanvas canvas)
    {
        _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
    }

    // ── Rectangles ───────────────────────────────────────────────

    /// <inheritdoc />
    public void DrawRect(GridRect rect, GridPaint paint)
    {
        var skPaint = GetOrCreatePaint(paint, SKPaintStyle.Stroke);
        _canvas.DrawRect(ToSKRect(rect), skPaint);
    }

    /// <inheritdoc />
    public void FillRect(GridRect rect, GridPaint paint)
    {
        var skPaint = GetOrCreatePaint(paint, SKPaintStyle.Fill);
        _canvas.DrawRect(ToSKRect(rect), skPaint);
    }

    /// <inheritdoc />
    public void DrawRoundRect(GridRect rect, float cornerRadius, GridPaint paint)
    {
        var skPaint = GetOrCreatePaint(paint, SKPaintStyle.Stroke);
        _canvas.DrawRoundRect(ToSKRect(rect), cornerRadius, cornerRadius, skPaint);
    }

    /// <inheritdoc />
    public void FillRoundRect(GridRect rect, float cornerRadius, GridPaint paint)
    {
        var skPaint = GetOrCreatePaint(paint, SKPaintStyle.Fill);
        _canvas.DrawRoundRect(ToSKRect(rect), cornerRadius, cornerRadius, skPaint);
    }

    // ── Lines ────────────────────────────────────────────────────

    /// <inheritdoc />
    public void DrawLine(float x1, float y1, float x2, float y2, GridPaint paint)
    {
        var skPaint = GetOrCreatePaint(paint, SKPaintStyle.Stroke);
        _canvas.DrawLine(x1, y1, x2, y2, skPaint);
    }

    // ── Text ─────────────────────────────────────────────────────

    /// <inheritdoc />
    public void DrawText(string text, float x, float y, GridPaint paint)
    {
        if (string.IsNullOrEmpty(text)) return;
        var skPaint = GetOrCreatePaint(paint, SKPaintStyle.Fill);
        var skFont = GetOrCreateFont(paint);
        _canvas.DrawText(text, x, y, SKTextAlign.Left, skFont, skPaint);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Text that exceeds the rectangle width is truncated with an ellipsis ("…") using a binary search
    /// to find the optimal truncation point.
    /// </remarks>
    public void DrawTextInRect(string text, GridRect rect, GridPaint paint,
        GridTextAlignment hAlign = GridTextAlignment.Left,
        GridVerticalAlignment vAlign = GridVerticalAlignment.Center,
        bool clip = true)
    {
        if (string.IsNullOrEmpty(text)) return;

        var skPaint = GetOrCreatePaint(paint, SKPaintStyle.Fill);
        var skFont = GetOrCreateFont(paint);

        if (clip)
        {
            _canvas.Save();
            _canvas.ClipRect(ToSKRect(rect));
        }

        // Measure text using SKFont
        float textWidth = skFont.MeasureText(text, out var bounds, skPaint);
        var metrics = skFont.Metrics;
        float textHeight = metrics.Descent - metrics.Ascent;

        // Horizontal alignment
        float x = hAlign switch
        {
            GridTextAlignment.Center => rect.X + (rect.Width - textWidth) / 2,
            GridTextAlignment.Right => rect.Right - textWidth,
            _ => rect.X
        };

        // Vertical alignment (baseline)
        float y = vAlign switch
        {
            GridVerticalAlignment.Top => rect.Y - metrics.Ascent,
            GridVerticalAlignment.Bottom => rect.Bottom - metrics.Descent,
            _ => rect.Y + (rect.Height - textHeight) / 2 - metrics.Ascent
        };

        // Truncate with ellipsis if too wide
        if (textWidth > rect.Width && rect.Width > 20)
        {
            var truncated = TruncateTextWithEllipsis(text, skFont, skPaint, rect.Width);
            _canvas.DrawText(truncated, x, y, SKTextAlign.Left, skFont, skPaint);
        }
        else
        {
            _canvas.DrawText(text, x, y, SKTextAlign.Left, skFont, skPaint);
        }

        if (clip)
            _canvas.Restore();
    }

    // ── Text measuring ───────────────────────────────────────────

    /// <inheritdoc />
    public GridSize MeasureText(string text, GridPaint paint)
    {
        if (string.IsNullOrEmpty(text)) return GridSize.Empty;

        var skPaint = GetOrCreatePaint(paint, SKPaintStyle.Fill);
        var skFont = GetOrCreateFont(paint);

        float textWidth = skFont.MeasureText(text, out _, skPaint);
        var metrics = skFont.Metrics;
        float height = metrics.Descent - metrics.Ascent;

        return new GridSize(textWidth, height);
    }

    /// <inheritdoc />
    public GridFontMetrics GetFontMetrics(GridPaint paint)
    {
        var skFont = GetOrCreateFont(paint);
        var metrics = skFont.Metrics;
        return new GridFontMetrics(metrics.Ascent, metrics.Descent, metrics.Leading);
    }

    // ── Clipping & transforms ────────────────────────────────────

    /// <inheritdoc />
    public void ClipRect(GridRect rect) => _canvas.ClipRect(ToSKRect(rect));

    /// <inheritdoc />
    public void Save() => _canvas.Save();

    /// <inheritdoc />
    public void Restore() => _canvas.Restore();

    /// <inheritdoc />
    public void Translate(float dx, float dy) => _canvas.Translate(dx, dy);

    // ── Images ───────────────────────────────────────────────────

    /// <inheritdoc />
    /// <remarks>
    /// Supports <see cref="SKImage"/> and <see cref="SKBitmap"/> as the image object.
    /// Other types are silently ignored.
    /// </remarks>
    public void DrawImage(object image, GridRect destRect)
    {
        if (image is SKImage skImage)
        {
            _canvas.DrawImage(skImage, ToSKRect(destRect));
        }
        else if (image is SKBitmap skBitmap)
        {
            _canvas.DrawBitmap(skBitmap, ToSKRect(destRect));
        }
    }

    // ── Conversion helpers ───────────────────────────────────────

    private static SKRect ToSKRect(GridRect r) => new(r.Left, r.Top, r.Right, r.Bottom);
    private static SKColor ToSKColor(GridColor c) => new(c.R, c.G, c.B, c.A);

    /// <summary>
    /// Get or create a cached SKPaint for the given configuration.
    /// Cached paints are reused within the same frame to avoid native object churn.
    /// </summary>
    private SKPaint GetOrCreatePaint(GridPaint paint, SKPaintStyle styleOverride)
    {
        var key = new PaintKey(paint.Color.ToUint32(), paint.StrokeWidth, styleOverride, paint.IsAntiAlias);
        if (!_paintCache.TryGetValue(key, out var skPaint))
        {
            skPaint = new SKPaint
            {
                Color = ToSKColor(paint.Color),
                StrokeWidth = paint.StrokeWidth,
                IsAntialias = paint.IsAntiAlias,
                Style = styleOverride
            };
            _paintCache[key] = skPaint;
        }
        return skPaint;
    }

    /// <summary>
    /// Get or create a cached SKFont for the given paint's font settings.
    /// Cached fonts are reused within the same frame to avoid native object churn.
    /// </summary>
    private SKFont GetOrCreateFont(GridPaint paint)
    {
        var gridFont = paint.Font;
        var family = gridFont?.Family ?? "Default";
        var size = gridFont?.Size ?? 14f;
        var isBold = gridFont?.IsBold ?? false;
        var isItalic = gridFont?.IsItalic ?? false;

        var key = new FontKey(family, size, isBold, isItalic);
        if (!_fontCache.TryGetValue(key, out var skFont))
        {
            var typeface = GetOrCreateTypeface(family, isBold, isItalic);
            skFont = new SKFont(typeface, size)
            {
                Edging = SKFontEdging.SubpixelAntialias,
                Subpixel = true
            };
            _fontCache[key] = skFont;
        }
        return skFont;
    }

    /// <summary>
    /// Get or create a cached SKTypeface for the given family and style.
    /// Typefaces are expensive to resolve from family names; caching avoids repeated lookups.
    /// </summary>
    private SKTypeface GetOrCreateTypeface(string family, bool isBold, bool isItalic)
    {
        var key = new TypefaceKey(family, isBold, isItalic);
        if (!_typefaceCache.TryGetValue(key, out var typeface))
        {
            if (isBold || isItalic || family != "Default")
            {
                var weight = isBold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
                var slant = isItalic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
                typeface = SKTypeface.FromFamilyName(
                    family == "Default" ? null : family,
                    weight, SKFontStyleWidth.Normal, slant);
            }
            else
            {
                typeface = SKTypeface.Default;
            }
            _typefaceCache[key] = typeface;
        }
        return typeface;
    }

    private static string TruncateTextWithEllipsis(string text, SKFont font, SKPaint paint, float maxWidth)
    {
        const string ellipsis = "…";
        float ellipsisWidth = font.MeasureText(ellipsis, out _, paint);
        float targetWidth = maxWidth - ellipsisWidth;

        if (targetWidth <= 0) return ellipsis;

        // Binary search for the right truncation point
        int lo = 0, hi = text.Length;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (font.MeasureText(text[..mid], out _, paint) <= targetWidth)
                lo = mid;
            else
                hi = mid - 1;
        }

        return lo > 0 ? text[..lo] + ellipsis : ellipsis;
    }

    // ── IDisposable ──────────────────────────────────────────────

    /// <summary>
    /// Dispose all cached native objects. Call at the end of each frame.
    /// </summary>
    public void Dispose()
    {
        foreach (var paint in _paintCache.Values)
            paint.Dispose();
        _paintCache.Clear();

        foreach (var font in _fontCache.Values)
            font.Dispose();
        _fontCache.Clear();

        // Don't dispose SKTypeface.Default — only dispose custom typefaces
        foreach (var (key, typeface) in _typefaceCache)
        {
            if (typeface != SKTypeface.Default)
                typeface.Dispose();
        }
        _typefaceCache.Clear();
    }
}

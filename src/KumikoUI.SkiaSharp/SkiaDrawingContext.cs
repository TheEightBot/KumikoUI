using KumikoUI.Core.Rendering;
using SkiaSharp;

namespace KumikoUI.SkiaSharp;

/// <summary>
/// SkiaSharp implementation of <see cref="IDrawingContext"/>.
/// Maps all platform-independent drawing calls to SKCanvas operations.
/// Uses the modern SKFont-based text APIs (SkiaSharp 3.x).
///
/// <para><b>Performance:</b> the native SkiaSharp objects a frame draws with (SKPaint, SKFont,
/// SKTypeface) come from a <see cref="SkiaResourceCache"/>. A host creates one cache per control and
/// passes it to <see cref="SkiaDrawingContext(SKCanvas, SkiaResourceCache)"/> every frame, so the same
/// native objects are reused frame after frame; the context itself is a thin per-frame wrapper over the
/// canvas. Dispose the context at the end of each frame: that ends the frame on the cache but leaves the
/// cached objects alive.</para>
/// </summary>
public class SkiaDrawingContext : IDrawingContext, IDisposable
{
    private readonly SKCanvas _canvas;
    private readonly SkiaResourceCache _cache;
    private readonly bool _ownsCache;
    private bool _disposed;

    /// <summary>
    /// Initializes a new <see cref="SkiaDrawingContext"/> that renders onto the specified <see cref="SKCanvas"/>
    /// with a private <see cref="SkiaResourceCache"/> that is disposed with the context. Every frame drawn
    /// this way creates its native objects again; hosts that draw repeatedly should keep a
    /// <see cref="SkiaResourceCache"/> and use <see cref="SkiaDrawingContext(SKCanvas, SkiaResourceCache)"/>.
    /// </summary>
    /// <param name="canvas">The SkiaSharp canvas to draw on. Must not be <c>null</c>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="canvas"/> is <c>null</c>.</exception>
    public SkiaDrawingContext(SKCanvas canvas)
        : this(canvas ?? throw new ArgumentNullException(nameof(canvas)), new SkiaResourceCache(), ownsCache: true)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="SkiaDrawingContext"/> for one frame on <paramref name="canvas"/>, drawing
    /// with the native objects in <paramref name="resourceCache"/>. The cache is not disposed with the
    /// context; its owner (normally the host control) disposes it.
    /// </summary>
    /// <param name="canvas">The SkiaSharp canvas to draw on. Must not be <c>null</c>.</param>
    /// <param name="resourceCache">The long-lived cache to draw with. Must not be <c>null</c> or disposed.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <c>null</c>.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when <paramref name="resourceCache"/> is disposed.</exception>
    public SkiaDrawingContext(SKCanvas canvas, SkiaResourceCache resourceCache)
        : this(canvas ?? throw new ArgumentNullException(nameof(canvas)),
               resourceCache ?? throw new ArgumentNullException(nameof(resourceCache)), ownsCache: false)
    {
    }

    private SkiaDrawingContext(SKCanvas canvas, SkiaResourceCache resourceCache, bool ownsCache)
    {
        _canvas = canvas;
        _cache = resourceCache;
        _ownsCache = ownsCache;
        _cache.BeginFrame();
    }

    /// <summary>The cache this context draws with.</summary>
    public SkiaResourceCache ResourceCache => _cache;

    // ── Rectangles ───────────────────────────────────────────────

    /// <inheritdoc />
    public void DrawRect(KumikoRect rect, KumikoPaint paint)
    {
        var skPaint = GetOrCreatePaint(paint, SKPaintStyle.Stroke);
        _canvas.DrawRect(ToSKRect(rect), skPaint);
    }

    /// <inheritdoc />
    public void FillRect(KumikoRect rect, KumikoPaint paint)
    {
        var skPaint = GetOrCreatePaint(paint, SKPaintStyle.Fill);
        _canvas.DrawRect(ToSKRect(rect), skPaint);
    }

    /// <inheritdoc />
    public void DrawRoundRect(KumikoRect rect, float cornerRadius, KumikoPaint paint)
    {
        var skPaint = GetOrCreatePaint(paint, SKPaintStyle.Stroke);
        _canvas.DrawRoundRect(ToSKRect(rect), cornerRadius, cornerRadius, skPaint);
    }

    /// <inheritdoc />
    public void FillRoundRect(KumikoRect rect, float cornerRadius, KumikoPaint paint)
    {
        var skPaint = GetOrCreatePaint(paint, SKPaintStyle.Fill);
        _canvas.DrawRoundRect(ToSKRect(rect), cornerRadius, cornerRadius, skPaint);
    }

    // ── Lines ────────────────────────────────────────────────────

    /// <inheritdoc />
    public void DrawLine(float x1, float y1, float x2, float y2, KumikoPaint paint)
    {
        var skPaint = GetOrCreatePaint(paint, SKPaintStyle.Stroke);
        _canvas.DrawLine(x1, y1, x2, y2, skPaint);
    }

    // ── Text ─────────────────────────────────────────────────────

    /// <inheritdoc />
    public void DrawText(string text, float x, float y, KumikoPaint paint)
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
    public void DrawTextInRect(string text, KumikoRect rect, KumikoPaint paint,
        KumikoTextAlignment hAlign = KumikoTextAlignment.Left,
        KumikoVerticalAlignment vAlign = KumikoVerticalAlignment.Center,
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
            KumikoTextAlignment.Center => rect.X + (rect.Width - textWidth) / 2,
            KumikoTextAlignment.Right => rect.Right - textWidth,
            _ => rect.X
        };

        // Vertical alignment (baseline)
        float y = vAlign switch
        {
            KumikoVerticalAlignment.Top => rect.Y - metrics.Ascent,
            KumikoVerticalAlignment.Bottom => rect.Bottom - metrics.Descent,
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
    public KumikoSize MeasureText(string text, KumikoPaint paint)
    {
        if (string.IsNullOrEmpty(text)) return KumikoSize.Empty;

        var skPaint = GetOrCreatePaint(paint, SKPaintStyle.Fill);
        var skFont = GetOrCreateFont(paint);

        float textWidth = skFont.MeasureText(text, out _, skPaint);
        var metrics = skFont.Metrics;
        float height = metrics.Descent - metrics.Ascent;

        return new KumikoSize(textWidth, height);
    }

    /// <inheritdoc />
    public KumikoFontMetrics GetFontMetrics(KumikoPaint paint)
    {
        var skFont = GetOrCreateFont(paint);
        var metrics = skFont.Metrics;
        return new KumikoFontMetrics(metrics.Ascent, metrics.Descent, metrics.Leading);
    }

    // ── Clipping & transforms ────────────────────────────────────

    /// <inheritdoc />
    public void ClipRect(KumikoRect rect) => _canvas.ClipRect(ToSKRect(rect));

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
    public void DrawImage(object image, KumikoRect destRect)
    {
        // SkiaSharp 4 requires explicit sampling. SKSamplingOptions.Default (nearest neighbour)
        // matches what the v3 paint-less overloads used, so image cells render unchanged.
        if (image is SKImage skImage)
        {
            _canvas.DrawImage(skImage, ToSKRect(destRect), SKSamplingOptions.Default, null);
        }
        else if (image is SKBitmap skBitmap)
        {
            _canvas.DrawBitmap(skBitmap, ToSKRect(destRect), SKSamplingOptions.Default, null);
        }
    }

    // ── Conversion helpers ───────────────────────────────────────

    private static SKRect ToSKRect(KumikoRect r) => new(r.Left, r.Top, r.Right, r.Bottom);

    private SKPaint GetOrCreatePaint(KumikoPaint paint, SKPaintStyle styleOverride) => _cache.GetPaint(paint, styleOverride);

    private SKFont GetOrCreateFont(KumikoPaint paint) => _cache.GetFont(paint.Font);

    /// <summary>
    /// Resolves a family name and style to a typeface, or <c>null</c> when the platform does not know
    /// the family. A seam so tests can simulate the platforms where SkiaSharp returns <c>null</c>.
    /// </summary>
    internal static Func<string, SKFontStyle, SKTypeface?> FamilyMatcher { get; set; } =
        static (family, style) => SKFontManager.Default.MatchFamily(family, style);

    private static readonly HashSet<string> _reportedUnknownFamilies = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Logs each unknown family once per process, so a grid redrawn at 60 fps does not flood the log.</summary>
    internal static void ReportUnknownFamily(string family)
    {
        lock (_reportedUnknownFamilies)
        {
            if (!_reportedUnknownFamilies.Add(family)) return;
        }
        System.Diagnostics.Trace.TraceWarning(
            $"KumikoUI: font family '{family}' was not found; using the default typeface instead. " +
            "Register it with SkiaFontRegistrar if it is a custom font.");
    }

    /// <summary>Forgets which unknown families were already reported (tests only).</summary>
    internal static void ResetUnknownFamilyReports()
    {
        lock (_reportedUnknownFamilies) _reportedUnknownFamilies.Clear();
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
    /// Ends the frame. Call at the end of each frame. Objects in a shared <see cref="SkiaResourceCache"/>
    /// stay cached for the next frame; a private cache (created by <see cref="SkiaDrawingContext(SKCanvas)"/>)
    /// is disposed. Calling it again does nothing.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cache.EndFrame();
        if (_ownsCache)
            _cache.Dispose();
    }
}

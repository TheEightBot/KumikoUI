using KumikoUI.Core.Rendering;
using SkiaSharp;

namespace KumikoUI.SkiaSharp;

/// <summary>
/// SkiaSharp implementation of <see cref="IDrawingContext"/>.
/// Maps all platform-independent drawing calls to SKCanvas operations.
/// Uses the modern SKFont-based text APIs (SkiaSharp 3.x).
///
/// <para><b>Performance:</b> the native SkiaSharp objects a frame draws with (SKPaint, SKFont,
/// SKTypeface, the text blobs, the polygon path) come from a <see cref="SkiaResourceCache"/>. A host creates
/// one cache per control and starts every frame with <see cref="SkiaResourceCache.CreateContext"/>, which also
/// reuses the context object itself, so a steady-state frame allocates no managed memory. The constructor
/// <see cref="SkiaDrawingContext(SKCanvas, SkiaResourceCache)"/> does the same with a new context object per frame
/// (a few dozen bytes). Dispose the context at the end of each frame: that ends the frame on the cache but
/// leaves the cached objects alive.</para>
/// </summary>
public class SkiaDrawingContext : IDrawingContext, IDisposable
{
    private SKCanvas _canvas;
    private readonly SkiaResourceCache _cache;
    private readonly bool _ownsCache;
    private readonly bool _pooled;
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

    /// <summary>The context <see cref="SkiaResourceCache.CreateContext"/> reuses for every frame; bound per frame by <see cref="Bind"/>.</summary>
    internal SkiaDrawingContext(SkiaResourceCache resourceCache)
    {
        _canvas = null!;
        _cache = resourceCache;
        _pooled = true;
        _disposed = true;
    }

    /// <summary>Starts a frame of the reused context on <paramref name="canvas"/>.</summary>
    internal void Bind(SKCanvas canvas)
    {
        _cache.BeginFrame();
        _canvas = canvas;
        _disposed = false;
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

    // ── Polygons & polylines ─────────────────────────────────────

    /// <inheritdoc />
    public void FillPolygon(ReadOnlySpan<KumikoPoint> points, KumikoPaint paint)
    {
        if (points.Length < 3) return;
        var skPaint = GetOrCreatePaint(paint, SKPaintStyle.Fill);
        DrawPolygonPath(points, close: true, skPaint);
    }

    /// <inheritdoc />
    public void DrawPolyline(ReadOnlySpan<KumikoPoint> points, KumikoPaint paint)
    {
        if (points.Length < 2) return;
        // Joins and caps are not part of the shared paint cache key, so strokes use their own paint.
        var skPaint = _cache.GetPolylinePaint();
        skPaint.Color = new SKColor(paint.Color.R, paint.Color.G, paint.Color.B, paint.Color.A);
        skPaint.StrokeWidth = paint.StrokeWidth;
        skPaint.IsAntialias = paint.IsAntiAlias;
        DrawPolygonPath(points, close: false, skPaint);
    }

    /// <summary>
    /// Draws a polygon or polyline from the path the cache keeps for its shape (translated to where it is drawn), so
    /// the glyphs drawn every frame (arrows, chevrons, check marks) neither build a path nor allocate.
    /// </summary>
    private void DrawPolygonPath(ReadOnlySpan<KumikoPoint> points, bool close, SKPaint skPaint)
    {
        var cached = _cache.GetPolygonPath(points, close);
        if (cached is null)
        {
            _canvas.DrawPath(BuildPath(points, close), skPaint);
            return;
        }

        _canvas.Save();
        _canvas.Translate(points[0].X, points[0].Y);
        _canvas.DrawPath(cached, skPaint);
        _canvas.Restore();
    }

    // SkiaSharp 4 marks SKPath's mutators obsolete in favor of SKPathBuilder, but SKPathBuilder.Detach/Snapshot
    // create a new SKPath per call; one reused, rewound SKPath keeps glyph drawing allocation-free.
#pragma warning disable CS0618
    private SKPath BuildPath(ReadOnlySpan<KumikoPoint> points, bool close)
    {
        var path = _cache.GetScratchPath();
        path.Rewind();
        path.MoveTo(points[0].X, points[0].Y);
        for (int i = 1; i < points.Length; i++)
            path.LineTo(points[i].X, points[i].Y);
        if (close) path.Close();
        return path;
    }
#pragma warning restore CS0618

    // ── Text ─────────────────────────────────────────────────────

    /// <inheritdoc />
    /// <remarks>
    /// Characters the font's typeface has no glyph for are drawn with a fallback typeface (see
    /// <see cref="SkiaFontRegistrar.FallbackFamilies"/>); the runs come from the <see cref="ResourceCache"/>.
    /// </remarks>
    public void DrawText(string text, float x, float y, KumikoPaint paint)
    {
        if (string.IsNullOrEmpty(text)) return;
        var skPaint = GetOrCreatePaint(paint, SKPaintStyle.Fill);
        var skFont = GetOrCreateFont(paint);
        _cache.MeasureTextWidth(text, paint.Font, out _, out var runs);
        DrawRuns(text, runs, x, y, paint.Font, skFont, skPaint);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Line breaks and tabs are drawn as spaces, and text wider than the rectangle is cut between two whole text
    /// elements (an emoji or a letter with combining marks is never split) and ends with an ellipsis ("…"); just
    /// the ellipsis is drawn when no character fits beside it. The measured width and the cut text come from the
    /// <see cref="ResourceCache"/>, so unchanged text is measured only the first time it is drawn.
    /// </remarks>
    public void DrawTextInRect(string text, KumikoRect rect, KumikoPaint paint,
        KumikoTextAlignment hAlign = KumikoTextAlignment.Left,
        KumikoVerticalAlignment vAlign = KumikoVerticalAlignment.Center,
        bool clip = true)
    {
        if (string.IsNullOrEmpty(text)) return;

        var skPaint = GetOrCreatePaint(paint, SKPaintStyle.Fill);
        var skFont = _cache.GetFont(paint.Font, out var metrics);

        string line = text;
        float width = _cache.MeasureTextWidth(text, paint.Font, out bool hasControlCharacters, out var runs);
        if (hasControlCharacters || width > rect.Width)
        {
            var lines = _cache.GetTextLayout(text, paint.Font, rect.Width, KumikoTextWrap.None, 1, out var lineRuns);
            if (lines.Count == 0) return;
            line = lines.GetLine(0);
            width = lines.GetLineWidth(0);
            runs = lineRuns[0];
        }

        float textHeight = metrics.Descent - metrics.Ascent;
        float top = vAlign switch
        {
            KumikoVerticalAlignment.Top => rect.Y,
            KumikoVerticalAlignment.Bottom => rect.Bottom - textHeight,
            _ => rect.Y + (rect.Height - textHeight) / 2
        };

        if (clip)
        {
            _canvas.Save();
            _canvas.ClipRect(ToSKRect(rect));
        }

        DrawRuns(line, runs, AlignX(rect, width, hAlign), top - metrics.Ascent, paint.Font, skFont, skPaint);

        if (clip)
            _canvas.Restore();
    }

    /// <inheritdoc />
    /// <remarks>
    /// The lines come from the <see cref="ResourceCache"/>, keyed by the text, the font, the rectangle's width,
    /// the wrap mode and the number of lines that fit its height, so unchanged text is laid out only the first
    /// time it is drawn and later frames allocate nothing.
    /// </remarks>
    public void DrawTextInRect(string text, KumikoRect rect, KumikoPaint paint,
        KumikoTextAlignment hAlign, KumikoVerticalAlignment vAlign, bool clip, KumikoTextWrap wrap)
    {
        if (wrap == KumikoTextWrap.None)
        {
            DrawTextInRect(text, rect, paint, hAlign, vAlign, clip);
            return;
        }
        if (string.IsNullOrEmpty(text)) return;

        var skPaint = GetOrCreatePaint(paint, SKPaintStyle.Fill);
        var skFont = _cache.GetFont(paint.Font, out var skMetrics);
        var metrics = new KumikoFontMetrics(skMetrics.Ascent, skMetrics.Descent, skMetrics.Leading);

        var lines = _cache.GetTextLayout(text, paint.Font, rect.Width, wrap,
            KumikoTextLayout.GetMaxLines(rect.Height, metrics), out var lineRuns);
        if (lines.Count == 0) return;

        float height = lines.GetHeight(metrics);
        float top = vAlign switch
        {
            KumikoVerticalAlignment.Top => rect.Y,
            KumikoVerticalAlignment.Bottom => rect.Bottom - height,
            _ => rect.Y + (rect.Height - height) / 2
        };

        if (clip)
        {
            _canvas.Save();
            _canvas.ClipRect(ToSKRect(rect));
        }

        float baseline = top - metrics.Ascent;
        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines.GetLine(i);
            if (line.Length > 0)
            {
                float x = AlignX(rect, lines.GetLineWidth(i), hAlign);
                DrawRuns(line, lineRuns[i], x, baseline + i * metrics.LineHeight, paint.Font, skFont, skPaint);
            }
        }

        if (clip)
            _canvas.Restore();
    }

    /// <summary>
    /// Draws one line of text run by run: each run in its typeface (the requested font, or a fallback for glyphs
    /// it lacks), advancing by the run widths measured with the runs. A run is drawn from a text blob built the
    /// first time it is drawn and kept with the runs, so drawing it again allocates nothing.
    /// </summary>
    private void DrawRuns(string line, FontRuns runs, float x, float baseline, KumikoFont? font, SKFont skFont,
        SKPaint skPaint)
    {
        if (runs.IsPrimaryOnly)
        {
            DrawRun(line, 0, line.Length, runs, 0, x, baseline, skFont, skPaint);
            return;
        }

        int start = 0;
        for (int i = 0; i < runs.Count; i++)
        {
            int end = runs.GetEnd(i);
            int face = runs.GetFace(i);
            var runFont = face == FontRuns.PrimaryFace ? skFont : _cache.GetFallbackFont(face, runs.Generation, font);
            DrawRun(line, start, end, runs, i, x, baseline, runFont, skPaint);
            x += runs.GetWidth(i);
            start = end;
        }
    }

    private void DrawRun(string line, int start, int end, FontRuns runs, int index, float x, float baseline,
        SKFont runFont, SKPaint skPaint)
    {
        var blob = runs.GetBlob(index, out bool blank);
        if (blob is null)
        {
            if (blank) return;
            blob = runs.SetBlob(index, SKTextBlob.Create(line.AsSpan(start, end - start), runFont, default));
            if (blob is null) return;
        }
        _canvas.DrawText(blob, x, baseline, skPaint);
    }

    private static float AlignX(KumikoRect rect, float width, KumikoTextAlignment hAlign) => hAlign switch
    {
        KumikoTextAlignment.Center => rect.X + (rect.Width - width) / 2,
        KumikoTextAlignment.Right => rect.Right - width,
        _ => rect.X
    };

    // ── Text measuring ───────────────────────────────────────────

    /// <inheritdoc />
    /// <remarks>
    /// The width is cached in the <see cref="ResourceCache"/> by text and font, so measuring the same text again
    /// (on this or a later frame) does not measure it with Skia again.
    /// </remarks>
    public KumikoSize MeasureText(string text, KumikoPaint paint)
    {
        if (string.IsNullOrEmpty(text)) return KumikoSize.Empty;

        _cache.GetFont(paint.Font, out var metrics);
        float textWidth = _cache.MeasureTextWidth(text, paint.Font, out _);
        return new KumikoSize(textWidth, metrics.Descent - metrics.Ascent);
    }

    /// <inheritdoc />
    /// <remarks>The layout is cached in the <see cref="ResourceCache"/>, as it is for drawing.</remarks>
    public KumikoSize MeasureText(string text, KumikoPaint paint, float maxWidth, KumikoTextWrap wrap)
    {
        if (string.IsNullOrEmpty(text)) return KumikoSize.Empty;

        _cache.GetFont(paint.Font, out var skMetrics);
        var metrics = new KumikoFontMetrics(skMetrics.Ascent, skMetrics.Descent, skMetrics.Leading);
        var lines = _cache.GetTextLayout(text, paint.Font, maxWidth, wrap, int.MaxValue);
        return new KumikoSize(lines.Width, lines.GetHeight(metrics));
    }

    /// <inheritdoc />
    public KumikoFontMetrics GetFontMetrics(KumikoPaint paint)
    {
        _cache.GetFont(paint.Font, out var metrics);
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
    /// Supports <see cref="SKImage"/> and <see cref="SKBitmap"/> as the image object. Other types draw nothing and
    /// are reported once per type through <see cref="System.Diagnostics.Trace"/>; resolve other values (URLs, files,
    /// bytes, host image sources) to an <see cref="SKImage"/> first with a <see cref="KumikoImageCache"/>, as both
    /// hosts do for image cells.
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
        else if (image is not null)
        {
            ReportUnsupportedImage(image.GetType());
        }
    }

    private static readonly HashSet<Type> _reportedImageTypes = new();

    /// <summary>Logs each image type <see cref="DrawImage"/> cannot draw once per process.</summary>
    private static void ReportUnsupportedImage(Type type)
    {
        lock (_reportedImageTypes)
        {
            if (!_reportedImageTypes.Add(type)) return;
        }
        System.Diagnostics.Trace.TraceWarning(
            $"KumikoUI: DrawImage cannot draw a '{type.FullName}'; only SKImage and SKBitmap are drawn. " +
            "Resolve other image sources with a KumikoImageCache.");
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

    /// <summary>
    /// Finds a typeface that has a glyph for a code point (family hint, style, BCP 47 language tags, code point), or
    /// <c>null</c> when no installed font has one. A seam so tests can simulate platforms without fallback fonts.
    /// </summary>
    internal static Func<string?, SKFontStyle, string[]?, int, SKTypeface?> CharacterMatcher { get; set; } =
        static (family, style, bcp47, codepoint) => SKFontManager.Default.MatchCharacter(family, style, bcp47, codepoint);

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
        if (_pooled)
            _cache.ReleasePooledContext();
        if (_ownsCache)
            _cache.Dispose();
    }
}

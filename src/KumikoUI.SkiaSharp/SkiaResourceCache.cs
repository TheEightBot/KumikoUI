using System.Globalization;
using System.Linq;
using KumikoUI.Core.Rendering;
using SkiaSharp;

namespace KumikoUI.SkiaSharp;

/// <summary>
/// Long-lived cache of the native SkiaSharp objects (<see cref="SKPaint"/>, <see cref="SKFont"/>,
/// <see cref="SKTypeface"/>) that <see cref="SkiaDrawingContext"/> draws with. A host control owns one
/// cache, passes it to the <see cref="SkiaDrawingContext"/> it creates for every frame, and disposes it
/// when the control is torn down, so steady-state frames reuse the same native objects instead of
/// creating and disposing them every frame.
/// </summary>
/// <remarks>
/// <para><b>Bounded:</b> each kind of object is kept in a least-recently-used map with a fixed capacity
/// (<see cref="MaxPaints"/>, <see cref="MaxFonts"/>, <see cref="MaxTypefaces"/>), so a renderer that
/// keeps asking for new colors or font sizes cannot grow the cache without limit. An object evicted
/// while a frame is being drawn is disposed when that frame ends, never in the middle of it.</para>
/// <para><b>Ownership:</b> the cache disposes only what it created. It never disposes
/// <see cref="SKTypeface.Default"/> or a typeface registered with <see cref="SkiaFontRegistrar"/>, and a
/// typeface resolved through the font manager is shared between caches and disposed when the last
/// cache that uses it lets it go.</para>
/// <para><b>Custom fonts:</b> registering, replacing or unregistering a family with
/// <see cref="SkiaFontRegistrar"/> drops that family's typefaces and fonts from every live cache before
/// the registrar disposes anything, so a cache never holds a disposed typeface and the next frame uses
/// the new registration.</para>
/// <para><b>Text:</b> the width of each text measured in a font, and the lines each text was laid out into
/// for a width (cut with an ellipsis or wrapped), are cached as well (<see cref="MaxTextMeasurements"/>), so
/// redrawing unchanged text measures nothing. Entries are keyed by the text and the font's family, size and style,
/// so a style change that picks another font simply stops using them; any <see cref="SkiaFontRegistrar"/> change
/// and <see cref="Clear"/> drop them all.</para>
/// <para><b>Font fallback:</b> characters the requested typeface has no glyph for are drawn with a fallback typeface
/// (the <see cref="SkiaFontRegistrar.FallbackFamilies"/> in order, then the font manager's choice for the character).
/// The choice is cached per family, style and code point, and the runs a text splits into are cached with its
/// measurement, so text that needs fallback is segmented once; text the requested typeface covers costs one glyph
/// check when it is first measured. Bold and italic fonts whose typeface is lighter or upright are emboldened or
/// slanted synthetically.</para>
/// <para><b>Threading:</b> a cache is meant to be used by one render thread at a time. Its members take a
/// lock, so the registrar can invalidate it from another thread safely.</para>
/// </remarks>
public sealed class SkiaResourceCache : IDisposable
{
    /// <summary>Default value of <see cref="MaxPaints"/>.</summary>
    public const int DefaultMaxPaints = 512;

    /// <summary>Default value of <see cref="MaxFonts"/>.</summary>
    public const int DefaultMaxFonts = 128;

    /// <summary>Default value of <see cref="MaxTypefaces"/>.</summary>
    public const int DefaultMaxTypefaces = 32;

    /// <summary>Default value of <see cref="MaxTextMeasurements"/>.</summary>
    public const int DefaultMaxTextMeasurements = 4096;

    private readonly object _gate = new();
    private readonly LruMap<PaintKey, SKPaint> _paints;
    private readonly LruMap<FontKey, FontEntry> _fonts;
    private readonly LruMap<TypefaceKey, TypefaceEntry> _typefaces;
    private readonly LruMap<TextKey, TextMeasurement> _textWidths;
    private readonly LruMap<TextLayoutKey, TextLayoutEntry> _textLayouts;

    // Font fallback. Faces are never evicted on their own: runs cached with text measurements refer to them by
    // index, so the table is emptied only together with the text entries (ClearTextLocked).
    internal const int MaxFallbackFaces = 64;
    internal const int MaxCharacterFaces = 4096;
    private const float SyntheticItalicSkew = -0.25f;
    private readonly List<FallbackFace> _fallbackFaces = new();
    private readonly Dictionary<SKTypeface, int> _fallbackFaceIndex = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<FallbackFamilyKey, int> _fallbackFamilyFaces = new();
    private readonly LruMap<CharFaceKey, int> _charFaces = new(MaxCharacterFaces);
    private readonly LruMap<FallbackFontKey, SKFont> _fallbackFonts;

    // HarfBuzz shapers of the typefaces that draw emoji sequences, created on first use.
    private readonly Dictionary<SKTypeface, GlyphShaper> _shapers = new(ReferenceEqualityComparer.Instance);

    // Incremented whenever the text entries are dropped, so a measurement taken across a drop is not stored.
    private long _textGeneration;
    private long _nativeTextMeasureCount;

    // What SkiaDrawingContext draws polygons and polylines with, and the context a host reuses every frame:
    // all created on first use, so a steady-state frame allocates none of them.
    private SKPath? _scratchPath;
    private readonly PolygonEntry[] _polygons = new PolygonEntry[MaxPolygons];
    private int _polygonCount;
    private int _nextPolygon;
    private SKPaint? _polylinePaint;
    private SkiaDrawingContext? _pooledContext;
    private bool _pooledContextInUse;

    // Objects retired while a frame is being drawn; released when the outermost frame ends.
    private readonly List<object> _retired = new();
    private int _frameDepth;
    private bool _disposed;

    /// <summary>Creates a cache with the default capacities.</summary>
    public SkiaResourceCache()
        : this(DefaultMaxPaints, DefaultMaxFonts, DefaultMaxTypefaces)
    {
    }

    /// <summary>Creates a cache with the given capacities.</summary>
    /// <param name="maxPaints">Most <see cref="SKPaint"/> objects kept; at least 1.</param>
    /// <param name="maxFonts">Most <see cref="SKFont"/> objects kept; at least 1.</param>
    /// <param name="maxTypefaces">Most typefaces kept, one per family, weight and slant; at least 1.</param>
    /// <exception cref="ArgumentOutOfRangeException">A capacity is less than 1.</exception>
    public SkiaResourceCache(int maxPaints, int maxFonts, int maxTypefaces)
        : this(maxPaints, maxFonts, maxTypefaces, DefaultMaxTextMeasurements)
    {
    }

    /// <summary>Creates a cache with the given capacities, including the text measurement capacity.</summary>
    /// <param name="maxPaints">Most <see cref="SKPaint"/> objects kept; at least 1.</param>
    /// <param name="maxFonts">Most <see cref="SKFont"/> objects kept; at least 1.</param>
    /// <param name="maxTypefaces">Most typefaces kept, one per family, weight and slant; at least 1.</param>
    /// <param name="maxTextMeasurements">Most text widths kept, and separately most text layouts kept; at least 1.</param>
    /// <exception cref="ArgumentOutOfRangeException">A capacity is less than 1.</exception>
    public SkiaResourceCache(int maxPaints, int maxFonts, int maxTypefaces, int maxTextMeasurements)
    {
        _paints = new LruMap<PaintKey, SKPaint>(maxPaints);
        _fonts = new LruMap<FontKey, FontEntry>(maxFonts);
        _typefaces = new LruMap<TypefaceKey, TypefaceEntry>(maxTypefaces);
        _textWidths = new LruMap<TextKey, TextMeasurement>(maxTextMeasurements);
        _textLayouts = new LruMap<TextLayoutKey, TextLayoutEntry>(maxTextMeasurements);
        _fallbackFonts = new LruMap<FallbackFontKey, SKFont>(maxFonts);
        SkiaFontRegistrar.Attach(this);
    }

    /// <summary>Most <see cref="SKPaint"/> objects the cache keeps.</summary>
    public int MaxPaints => _paints.Capacity;

    /// <summary>Most <see cref="SKFont"/> objects the cache keeps.</summary>
    public int MaxFonts => _fonts.Capacity;

    /// <summary>Most typefaces the cache keeps (one per family, weight and slant).</summary>
    public int MaxTypefaces => _typefaces.Capacity;

    /// <summary>
    /// Most text measurements the cache keeps: up to this many measured text widths and, separately, up to this
    /// many text layouts (text cut with an ellipsis or wrapped for a width).
    /// </summary>
    public int MaxTextMeasurements => _textWidths.Capacity;

    /// <summary>Number of cached text measurements: measured widths plus laid-out texts.</summary>
    public int TextMeasurementCount { get { lock (_gate) return _textWidths.Count + _textLayouts.Count; } }

    /// <summary>Number of cached <see cref="SKPaint"/> objects.</summary>
    public int PaintCount { get { lock (_gate) return _paints.Count; } }

    /// <summary>Number of cached <see cref="SKFont"/> objects.</summary>
    public int FontCount { get { lock (_gate) return _fonts.Count; } }

    /// <summary>Number of cached typefaces.</summary>
    public int TypefaceCount { get { lock (_gate) return _typefaces.Count; } }

    /// <summary><c>true</c> once <see cref="Dispose"/> has been called.</summary>
    public bool IsDisposed { get { lock (_gate) return _disposed; } }

    /// <summary>
    /// Drops every cached object (for example after a theme or font change that makes the current
    /// contents useless). Objects in use by a frame being drawn are disposed when that frame ends.
    /// </summary>
    public void Clear()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            RetireAll();
        }
    }

    /// <summary>
    /// Disposes every object the cache created. Calling it again does nothing. If a frame is being
    /// drawn with the cache, its objects are disposed when that frame ends.
    /// </summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            RetireAll();
        }
        SkiaFontRegistrar.Detach(this);
    }

    // ── Frames ───────────────────────────────────────────────────

    /// <summary>
    /// Starts a frame on <paramref name="canvas"/> and returns the context to draw it with; dispose it at the end of
    /// the frame. Unlike <c>new</c> <see cref="SkiaDrawingContext(SKCanvas, SkiaResourceCache)"/>, which allocates a
    /// context object every time, this reuses one context for every frame, so a host that draws a frame after frame
    /// (both hosts do) allocates no managed memory at all in the steady state.
    /// </summary>
    /// <remarks>
    /// Only one frame at a time can use the shared context: a call made while the previous context is not disposed
    /// yet (a nested or a concurrent frame) returns a new one, as the constructor does. Do not use a context after
    /// disposing it: the next frame rebinds it to its own canvas.
    /// </remarks>
    /// <param name="canvas">The canvas to draw on. Must not be <c>null</c>.</param>
    /// <returns>A context for one frame.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="canvas"/> is <c>null</c>.</exception>
    /// <exception cref="ObjectDisposedException">The cache is disposed.</exception>
    public SkiaDrawingContext CreateContext(SKCanvas canvas)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        SkiaDrawingContext? shared = null;
        lock (_gate)
        {
            ThrowIfDisposed();
            if (!_pooledContextInUse)
            {
                _pooledContextInUse = true;
                shared = _pooledContext ??= new SkiaDrawingContext(this);
            }
        }

        if (shared is null)
            return new SkiaDrawingContext(canvas, this);

        try
        {
            shared.Bind(canvas);
        }
        catch
        {
            ReleasePooledContext();
            throw;
        }
        return shared;
    }

    /// <summary>Called when the shared context is disposed: it can start the next frame.</summary>
    internal void ReleasePooledContext()
    {
        lock (_gate)
            _pooledContextInUse = false;
    }

    /// <summary>The path <see cref="SkiaDrawingContext"/> rewinds and redraws for every polygon and polyline.</summary>
    internal SKPath GetScratchPath()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            return _scratchPath ??= new SKPath();
        }
    }

    /// <summary>Most polygon shapes kept (see <see cref="GetPolygonPath"/>).</summary>
    internal const int MaxPolygons = 64;

    // Polygon coordinates are matched to 1/64 of a unit, so the same shape at another position (or with the
    // rounding noise of a subtraction) finds its cached path.
    private const float PolygonResolution = 64f;

    /// <summary>
    /// The path of a polygon or polyline (<paramref name="close"/>: the last point connects to the first), with
    /// its first point at the origin; the caller translates the canvas to that point. Built once per shape: an
    /// <see cref="SKPath"/> that is changed and drawn again makes SkiaSharp allocate a new native snapshot for
    /// every draw, so a shape that is drawn every frame (an arrow, a chevron, a check mark) must not be rebuilt.
    /// <c>null</c> when a coordinate is not a finite number (the caller builds the path itself).
    /// </summary>
    internal SKPath? GetPolygonPath(ReadOnlySpan<KumikoPoint> points, bool close)
    {
        var origin = points[0];
        var hash = new HashCode();
        hash.Add(points.Length);
        hash.Add(close);
        for (int i = 1; i < points.Length; i++)
        {
            float dx = (points[i].X - origin.X) * PolygonResolution;
            float dy = (points[i].Y - origin.Y) * PolygonResolution;
            if (!float.IsFinite(dx) || !float.IsFinite(dy) || Math.Abs(dx) > 1e9f || Math.Abs(dy) > 1e9f)
                return null;
            hash.Add((long)MathF.Round(dx) << 32 ^ (long)MathF.Round(dy));
        }
        int code = hash.ToHashCode();

        lock (_gate)
        {
            ThrowIfDisposed();
            for (int e = 0; e < _polygonCount; e++)
            {
                ref var entry = ref _polygons[e];
                if (entry.Hash == code && entry.Close == close && entry.Matches(points))
                    return entry.Path;
            }

            var path = new SKPath();
#pragma warning disable CS0618 // SKPath's mutators: the path is built once and never changed again
            path.MoveTo(0f, 0f);
            for (int i = 1; i < points.Length; i++)
                path.LineTo(points[i].X - origin.X, points[i].Y - origin.Y);
            if (close) path.Close();
#pragma warning restore CS0618

            var shape = new float[(points.Length - 1) * 2];
            for (int i = 1; i < points.Length; i++)
            {
                shape[(i - 1) * 2] = MathF.Round((points[i].X - origin.X) * PolygonResolution);
                shape[(i - 1) * 2 + 1] = MathF.Round((points[i].Y - origin.Y) * PolygonResolution);
            }

            int slot = _polygonCount < MaxPolygons ? _polygonCount++ : _nextPolygon++ % MaxPolygons;
            if (_polygons[slot].Path is { } evicted)
                Retire(evicted);
            _polygons[slot] = new PolygonEntry(code, close, shape, path);
            return path;
        }
    }

    /// <summary>The stroke paint of polylines: joins and caps are not part of the shared paint key.</summary>
    internal SKPaint GetPolylinePaint()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            return _polylinePaint ??= new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeJoin = SKStrokeJoin.Round,
                StrokeCap = SKStrokeCap.Round
            };
        }
    }

    /// <summary>Called by <see cref="SkiaDrawingContext"/> when it starts drawing with this cache.</summary>
    internal void BeginFrame()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            _frameDepth++;
        }
    }

    /// <summary>Called by <see cref="SkiaDrawingContext"/> when it is disposed; releases what was retired during the frame.</summary>
    internal void EndFrame()
    {
        lock (_gate)
        {
            if (_frameDepth == 0) return;
            if (--_frameDepth == 0)
                ReleaseRetired();
        }
    }

    // ── Lookups ──────────────────────────────────────────────────

    /// <summary>Returns the cached <see cref="SKPaint"/> for <paramref name="paint"/> drawn with <paramref name="style"/>.</summary>
    internal SKPaint GetPaint(KumikoPaint paint, SKPaintStyle style)
    {
        var key = new PaintKey(paint.Color.ToUint32(), paint.StrokeWidth, style, paint.IsAntiAlias);
        lock (_gate)
        {
            if (_paints.TryGet(key, out var skPaint))
                return skPaint;

            ThrowIfDisposed();
            var color = paint.Color;
            skPaint = new SKPaint
            {
                Color = new SKColor(color.R, color.G, color.B, color.A),
                StrokeWidth = paint.StrokeWidth,
                IsAntialias = paint.IsAntiAlias,
                Style = style
            };
            if (_paints.Add(key, skPaint, out var evicted))
                Retire(evicted.Value);
            return skPaint;
        }
    }

    /// <summary>Returns the cached <see cref="SKFont"/> for <paramref name="font"/> (<c>null</c> = default 14 pt).</summary>
    internal SKFont GetFont(KumikoFont? font) => GetFont(font, out _);

    /// <summary>
    /// Returns the cached <see cref="SKFont"/> for <paramref name="font"/> (<c>null</c> = default 14 pt) and its
    /// metrics, read once when the font was created.
    /// </summary>
    internal SKFont GetFont(KumikoFont? font, out SKFontMetrics metrics)
    {
        var key = ToFontKey(font);
        lock (_gate)
        {
            if (_fonts.TryGet(key, out var entry))
            {
                metrics = entry.Metrics;
                return entry.Font;
            }

            ThrowIfDisposed();
            var typefaceKey = new TypefaceKey(key.Family, key.IsBold, key.IsItalic);
            var typeface = GetTypefaceEntryLocked(typefaceKey);
            var skFont = CreateFont(typeface.Typeface, key.Size, key.IsBold, key.IsItalic, typeface.Weight, typeface.Slant);
            metrics = skFont.Metrics;
            if (_fonts.Add(key, new FontEntry(skFont, typefaceKey, metrics), out var evicted))
                Retire(evicted.Value.Font);
            return skFont;
        }
    }

    private static FontKey ToFontKey(KumikoFont? font) => new(
        font?.Family ?? "Default", font?.Size ?? 14f, font?.IsBold ?? false, font?.IsItalic ?? false);

    /// <summary>
    /// A font on <paramref name="typeface"/>, emboldened when a bold font gets a face lighter than semibold and
    /// slanted when an italic font gets an upright face (<paramref name="weight"/> and <paramref name="slant"/> are
    /// the face's style).
    /// </summary>
    private static SKFont CreateFont(SKTypeface typeface, float size, bool isBold, bool isItalic, int weight,
        SKFontStyleSlant slant) => new(typeface, size)
    {
        Edging = SKFontEdging.SubpixelAntialias,
        Subpixel = true,
        Embolden = isBold && weight < (int)SKFontStyleWeight.SemiBold,
        SkewX = isItalic && slant == SKFontStyleSlant.Upright ? SyntheticItalicSkew : 0f,
    };

    // ── Text ─────────────────────────────────────────────────────

    /// <summary>
    /// The advance width of <paramref name="text"/> (not empty) in <paramref name="font"/>, measured once and then
    /// read from the cache; <paramref name="hasControlCharacters"/> tells whether the text holds line breaks, tabs
    /// or other control characters that cannot be drawn as they are.
    /// </summary>
    internal float MeasureTextWidth(string text, KumikoFont? font, out bool hasControlCharacters) =>
        MeasureTextWidth(text, font, out hasControlCharacters, out _);

    /// <summary>
    /// <see cref="MeasureTextWidth(string, KumikoFont?, out bool)"/>, and the typeface runs
    /// <paramref name="text"/> is drawn in (<see cref="FontRuns.IsPrimaryOnly"/> when the requested typeface has every
    /// glyph), cached with the width.
    /// </summary>
    internal float MeasureTextWidth(string text, KumikoFont? font, out bool hasControlCharacters, out FontRuns runs)
    {
        var key = new TextKey(text, ToFontKey(font));
        long generation;
        lock (_gate)
        {
            if (_textWidths.TryGet(key, out var cached))
            {
                hasControlCharacters = cached.HasControlCharacters;
                runs = cached.Runs;
                return cached.Width;
            }
            ThrowIfDisposed();
            generation = _textGeneration;
        }

        runs = BuildRuns(text, font, generation, measure: true, out float width);
        hasControlCharacters = KumikoTextLayout.ContainsControlCharacters(text);

        lock (_gate)
        {
            if (generation == _textGeneration && !_textWidths.TryGet(key, out _))
            {
                if (_textWidths.Add(key, new TextMeasurement(width, hasControlCharacters, runs), out var evicted))
                    Retire(evicted.Value.Runs);
            }
        }
        return width;
    }

    /// <summary>
    /// The lines <paramref name="text"/> (not empty) is drawn as in <paramref name="font"/> within
    /// <paramref name="maxWidth"/> (see <see cref="KumikoTextLayout.LayOut"/>), laid out once and then read from
    /// the cache.
    /// </summary>
    internal KumikoTextLines GetTextLayout(string text, KumikoFont? font, float maxWidth, KumikoTextWrap wrap,
        int maxLines) => GetTextLayout(text, font, maxWidth, wrap, maxLines, out _);

    /// <summary>
    /// <see cref="GetTextLayout(string, KumikoFont?, float, KumikoTextWrap, int)"/>, and the typeface runs each line
    /// is drawn in, cached with the lines.
    /// </summary>
    internal KumikoTextLines GetTextLayout(string text, KumikoFont? font, float maxWidth, KumikoTextWrap wrap,
        int maxLines, out FontRuns[] lineRuns)
    {
        if (float.IsNaN(maxWidth)) maxWidth = float.PositiveInfinity;
        if (maxWidth < 0f) maxWidth = 0f;
        if (wrap == KumikoTextWrap.None) maxLines = 1;

        var key = new TextLayoutKey(text, ToFontKey(font), maxWidth, wrap, maxLines);
        long generation;
        lock (_gate)
        {
            if (_textLayouts.TryGet(key, out var cached))
            {
                lineRuns = cached.Runs;
                return cached.Lines;
            }
            ThrowIfDisposed();
            generation = _textGeneration;
        }

        var lines = LayOutText(text, font, GetFont(font), generation, maxWidth, wrap, maxLines);
        lineRuns = new FontRuns[lines.Count];
        for (int i = 0; i < lineRuns.Length; i++)
            lineRuns[i] = BuildRuns(lines.GetLine(i), font, generation, measure: false, out _);

        lock (_gate)
        {
            if (generation == _textGeneration && !_textLayouts.TryGet(key, out _))
            {
                if (_textLayouts.Add(key, new TextLayoutEntry(lines, lineRuns), out var evicted))
                    RetireRuns(evicted.Value.Runs);
            }
        }
        return lines;
    }

    // A separate method so the measuring closure is allocated only on a miss, not on every lookup.
    private KumikoTextLines LayOutText(string text, KumikoFont? font, SKFont primary, long generation, float maxWidth,
        KumikoTextWrap wrap, int maxLines) =>
        KumikoTextLayout.LayOut(text, maxWidth, wrap, maxLines, span => MeasureRuns(span, font, primary, generation));

    /// <summary>The number of times the cache has measured text with Skia (tests and diagnostics).</summary>
    internal long NativeTextMeasureCount => Interlocked.Read(ref _nativeTextMeasureCount);

    /// <summary>The width of one run: shaped when it holds an emoji sequence and HarfBuzz is available, else measured by Skia.</summary>
    private float MeasureRun(SKFont font, ReadOnlySpan<char> text, bool shaped)
    {
        if (shaped)
        {
            lock (_gate)
            {
                if (GetShaperLocked(font.Typeface) is { } shaper)
                    return shaper.Measure(text, font);
            }
        }
        return MeasureNative(font, text);
    }

    /// <summary>
    /// The text blob of a shaped run (see <see cref="FontRuns.IsShaped"/>), or <c>null</c> when HarfBuzz is not
    /// available and the caller builds the blob glyph by glyph.
    /// </summary>
    internal SKTextBlob? CreateShapedBlob(ReadOnlySpan<char> text, SKFont font)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            return GetShaperLocked(font.Typeface) is { } shaper ? shaper.CreateBlob(text, font) : null;
        }
    }

    private GlyphShaper? GetShaperLocked(SKTypeface typeface)
    {
        if (_shapers.TryGetValue(typeface, out var shaper))
            return shaper;

        shaper = GlyphShaper.TryCreate(typeface);
        if (shaper is null)
            return null;
        if (_shapers.Count >= MaxFallbackFaces + DefaultMaxTypefaces)
            RetireShapersLocked();
        _shapers.Add(typeface, shaper);
        return shaper;
    }

    private void RetireShapersLocked()
    {
        foreach (var shaper in _shapers.Values)
            Retire(shaper);
        _shapers.Clear();
    }

    private float MeasureNative(SKFont font, ReadOnlySpan<char> text)
    {
        Interlocked.Increment(ref _nativeTextMeasureCount);
        return text.IsEmpty ? 0f : font.MeasureText(text);
    }

    private void ClearTextLocked()
    {
        _textGeneration++;
        var measurements = new List<TextMeasurement>();
        _textWidths.Clear(measurements);
        foreach (var measurement in measurements)
            Retire(measurement.Runs);
        var layouts = new List<TextLayoutEntry>();
        _textLayouts.Clear(layouts);
        foreach (var layout in layouts)
            RetireRuns(layout.Runs);
        RetireShapersLocked();
        ClearFallbackLocked();
    }

    // ── Font fallback ────────────────────────────────────────────

    /// <summary>
    /// The runs <paramref name="text"/> is drawn in and, when <paramref name="measure"/>, its width. Text the
    /// requested typeface covers (the common case) costs one glyph check and one measurement.
    /// </summary>
    private FontRuns BuildRuns(ReadOnlySpan<char> text, KumikoFont? font, long generation, bool measure,
        out float width)
    {
        var primary = GetFont(font);
        width = 0f;
        if (text.IsEmpty || primary.ContainsGlyphs(text))
        {
            if (measure) width = MeasureNative(primary, text);
            return FontRuns.CreatePrimary(generation);
        }

        var ends = new List<int>();
        var faces = new List<int>();
        var shaped = new List<bool>();
        Segment(text, ToFontKey(font), primary, ends, faces, shaped);
        if (faces.Count == 1 && faces[0] == FontRuns.PrimaryFace)
        {
            if (measure) width = MeasureNative(primary, text);
            return FontRuns.CreatePrimary(generation);
        }

        var widths = new float[faces.Count];
        int start = 0;
        for (int i = 0; i < widths.Length; i++)
        {
            var runFont = faces[i] == FontRuns.PrimaryFace ? primary : GetFallbackFont(faces[i], generation, font);
            widths[i] = MeasureRun(runFont, text[start..ends[i]], shaped[i]);
            width += widths[i];
            start = ends[i];
        }
        return new FontRuns([.. ends], [.. faces], widths, [.. shaped], generation);
    }

    /// <summary>The width of <paramref name="text"/> drawn run by run, for <see cref="KumikoTextLayout"/>.</summary>
    private float MeasureRuns(ReadOnlySpan<char> text, KumikoFont? font, SKFont primary, long generation)
    {
        if (text.IsEmpty || primary.ContainsGlyphs(text))
            return MeasureNative(primary, text);

        var ends = new List<int>();
        var faces = new List<int>();
        var shaped = new List<bool>();
        Segment(text, ToFontKey(font), primary, ends, faces, shaped);
        float width = 0f;
        int start = 0;
        for (int i = 0; i < faces.Count; i++)
        {
            var runFont = faces[i] == FontRuns.PrimaryFace ? primary : GetFallbackFont(faces[i], generation, font);
            width += MeasureRun(runFont, text[start..ends[i]], shaped[i]);
            start = ends[i];
        }
        return width;
    }

    /// <summary>
    /// Splits <paramref name="text"/> into runs of whole text elements drawn with the same face: adds the end of
    /// each run to <paramref name="ends"/>, its face to <paramref name="faces"/> and to <paramref name="shaped"/>
    /// whether it holds an emoji sequence (a modifier, a joiner, a flag) that must be shaped to draw as one glyph.
    /// </summary>
    private void Segment(ReadOnlySpan<char> text, FontKey font, SKFont primary, List<int> ends, List<int> faces,
        List<bool> shaped)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            int current = int.MinValue;
            int pos = 0;
            while (pos < text.Length)
            {
                int length = Math.Max(1, StringInfo.GetNextTextElementLength(text[pos..]));
                int face = ElementFaceLocked(text.Slice(pos, length), font, primary, current);
                if (face != current)
                {
                    if (current != int.MinValue) ends.Add(pos);
                    faces.Add(face);
                    shaped.Add(false);
                    current = face;
                }
                if (GlyphShaper.IsAvailable && GlyphShaper.NeedsShaping(text.Slice(pos, length)))
                    shaped[^1] = true;
                pos += length;
            }
            ends.Add(text.Length);
        }
    }

    /// <summary>
    /// The face a text element is drawn with: the face of its first visible code point, unless that face lacks
    /// another of the element's code points and the face of one of those has them all. Elements with no visible
    /// code point (controls, joiners, variation selectors) stay in the current run.
    /// </summary>
    private int ElementFaceLocked(ReadOnlySpan<char> element, FontKey font, SKFont primary, int current)
    {
        int first = -1;
        bool more = false;
        foreach (var rune in element.EnumerateRunes())
        {
            if (IsNeutral(rune.Value)) continue;
            if (first < 0)
            {
                first = rune.Value;
            }
            else
            {
                more = true;
                break;
            }
        }
        if (first < 0)
            return current == int.MinValue ? FontRuns.PrimaryFace : current;

        int face = CharFaceLocked(font, primary, first);
        if (!more || FaceCovers(face, primary, element))
            return face;

        foreach (var rune in element.EnumerateRunes())
        {
            if (IsNeutral(rune.Value) || rune.Value == first) continue;
            int other = CharFaceLocked(font, primary, rune.Value);
            if (other != face && FaceCovers(other, primary, element))
                return other;
        }
        return face;
    }

    private bool FaceCovers(int face, SKFont primary, ReadOnlySpan<char> element)
    {
        var font = face == FontRuns.PrimaryFace ? primary : _fallbackFaces[face].Probe;
        foreach (var rune in element.EnumerateRunes())
        {
            if (!IsNeutral(rune.Value) && !font.ContainsGlyph(rune.Value))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Code points that never pick a typeface: controls and default-ignorable characters (joiners, variation
    /// selectors, bidi marks, tags). They are drawn with whatever face the text element around them uses.
    /// </summary>
    private static bool IsNeutral(int c) =>
        c < 0x20 || c is >= 0x7F and <= 0x9F
        || c is 0x00AD or 0x034F or 0xFEFF
        || c is >= 0x180B and <= 0x180F
        || c is >= 0x200B and <= 0x200F
        || c is >= 0x202A and <= 0x202E
        || c is >= 0x2060 and <= 0x206F
        || c is >= 0xFE00 and <= 0xFE0F
        || c is >= 0xE0000 and <= 0xE0FFF;

    /// <summary>The face that draws <paramref name="codepoint"/> in <paramref name="font"/>'s family and style; cached.</summary>
    private int CharFaceLocked(FontKey font, SKFont primary, int codepoint)
    {
        var key = new CharFaceKey(font.Family, font.IsBold, font.IsItalic, codepoint);
        if (_charFaces.TryGet(key, out int face))
            return face;

        face = primary.ContainsGlyph(codepoint) ? FontRuns.PrimaryFace : FindFallbackLocked(font, codepoint);
        _charFaces.Add(key, face, out _);
        return face;
    }

    /// <summary>
    /// The first of the registrar's fallback families that has <paramref name="codepoint"/>, else the font
    /// manager's choice for it; <see cref="FontRuns.PrimaryFace"/> when no typeface has it (it is drawn missing).
    /// </summary>
    private int FindFallbackLocked(FontKey font, int codepoint)
    {
        foreach (var family in SkiaFontRegistrar.FallbackFamilySnapshot)
        {
            int face = FallbackFamilyFaceLocked(family, font.IsBold, font.IsItalic);
            if (face >= 0 && _fallbackFaces[face].Probe.ContainsGlyph(codepoint))
                return face;
        }

        using var style = StyleOf(font.IsBold, font.IsItalic);
        // The family is a hint; the UI language picks between Han glyph variants (Japanese, Chinese, Korean).
        string? hint = font.Family == "Default" ? null : font.Family;
        string language = CultureInfo.CurrentUICulture.Name;
        var typeface = SystemTypefacePool.MatchCharacter(hint, style,
            string.IsNullOrEmpty(language) ? null : [language], codepoint, out bool leased);
        if (typeface is null)
            return FontRuns.PrimaryFace;

        int index = AddFallbackFaceLocked(typeface, SourceOf(typeface, leased), typeface.FontWeight, typeface.FontSlant);
        return index >= 0 && _fallbackFaces[index].Probe.ContainsGlyph(codepoint) ? index : FontRuns.PrimaryFace;
    }

    /// <summary>The face of a fallback family in a style, or -1 when the family is neither registered nor installed.</summary>
    private int FallbackFamilyFaceLocked(string family, bool isBold, bool isItalic)
    {
        var key = new FallbackFamilyKey(family, isBold, isItalic);
        if (_fallbackFamilyFaces.TryGetValue(key, out int face))
            return face;

        if (SkiaFontRegistrar.TryResolve(family, WeightOf(isBold), SlantOf(isItalic), out var registered,
                out int weight, out var slant))
        {
            face = AddFallbackFaceLocked(registered, TypefaceSource.Registrar, weight, slant);
        }
        else
        {
            using var style = StyleOf(isBold, isItalic);
            var typeface = SystemTypefacePool.Match(family, style, out bool leased);
            face = typeface is null
                ? -1
                : AddFallbackFaceLocked(typeface, SourceOf(typeface, leased), typeface.FontWeight, typeface.FontSlant);
        }
        _fallbackFamilyFaces[key] = face;
        return face;
    }

    /// <summary>
    /// Adds a fallback face, or finds it and gives back the extra lease; -1 when the table is full (the lease is
    /// given back too, and the character is drawn with the primary typeface).
    /// </summary>
    private int AddFallbackFaceLocked(SKTypeface typeface, TypefaceSource source, int weight, SKFontStyleSlant slant)
    {
        bool known = _fallbackFaceIndex.TryGetValue(typeface, out int index);
        if (known || _fallbackFaces.Count >= MaxFallbackFaces)
        {
            // Never the last lease when the face is already held.
            if (source == TypefaceSource.FontManager)
                SystemTypefacePool.Release(typeface);
            return known ? index : -1;
        }

        index = _fallbackFaces.Count;

        // The probe font is owned by the fallback table (disposed with it when the table is emptied); it is
        // disposed here only when the entry could not be stored.
        var probe = new SKFont(typeface, 12f);
        try
        {
            _fallbackFaces.Add(new FallbackFace(typeface, source, probe, weight, slant));
            _fallbackFaceIndex.Add(typeface, index);
        }
        catch
        {
            _fallbackFaceIndex.Remove(typeface);
            if (_fallbackFaces.Count > index)
                _fallbackFaces.RemoveAt(index);
            probe.Dispose();
            throw;
        }
        return index;
    }

    /// <summary>
    /// The font that draws runs of fallback face <paramref name="face"/> in <paramref name="font"/>'s size and
    /// style, cached; the primary font when the face belongs to an earlier generation (the fallback table was
    /// emptied after the runs were made).
    /// </summary>
    internal SKFont GetFallbackFont(int face, long generation, KumikoFont? font)
    {
        var fontKey = ToFontKey(font);
        lock (_gate)
        {
            if (face < 0 || generation != _textGeneration || face >= _fallbackFaces.Count)
                return GetFont(font);

            var key = new FallbackFontKey(face, fontKey.Size, fontKey.IsBold, fontKey.IsItalic);
            if (_fallbackFonts.TryGet(key, out var skFont))
                return skFont;

            ThrowIfDisposed();
            var entry = _fallbackFaces[face];
            skFont = CreateFont(entry.Typeface, fontKey.Size, fontKey.IsBold, fontKey.IsItalic, entry.Weight, entry.Slant);
            if (_fallbackFonts.Add(key, skFont, out var evicted))
                Retire(evicted.Value);
            return skFont;
        }
    }

    /// <summary>The typeface of fallback face <paramref name="face"/> (tests and diagnostics).</summary>
    internal SKTypeface GetFallbackTypeface(int face)
    {
        lock (_gate)
            return _fallbackFaces[face].Typeface;
    }

    /// <summary>Number of fallback faces in use (tests and diagnostics).</summary>
    internal int FallbackFaceCount { get { lock (_gate) return _fallbackFaces.Count; } }

    /// <summary>Called by <see cref="SkiaFontRegistrar.SetFallbackFamilies"/>: drops the text entries and fallback choices.</summary>
    internal void OnFallbackFamiliesChanged()
    {
        lock (_gate)
        {
            if (_disposed) return;
            ClearTextLocked();
        }
    }

    /// <summary>Empties the fallback tables; only together with the text entries, whose runs refer to the faces by index.</summary>
    private void ClearFallbackLocked()
    {
        var fonts = new List<SKFont>();
        _fallbackFonts.Clear(fonts);
        foreach (var font in fonts)
            Retire(font);
        foreach (var face in _fallbackFaces)
        {
            Retire(face.Probe);
            if (face.Source == TypefaceSource.FontManager)
                Retire(face.Typeface);
        }
        _fallbackFaces.Clear();
        _fallbackFaceIndex.Clear();
        _fallbackFamilyFaces.Clear();
        _charFaces.Clear();
    }

    private static int WeightOf(bool isBold) => (int)(isBold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal);

    private static SKFontStyleSlant SlantOf(bool isItalic) => isItalic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;

    private static SKFontStyle StyleOf(bool isBold, bool isItalic) =>
        new((SKFontStyleWeight)WeightOf(isBold), SKFontStyleWidth.Normal, SlantOf(isItalic));

    private static TypefaceSource SourceOf(SKTypeface typeface, bool leased) =>
        leased ? TypefaceSource.FontManager
        : ReferenceEquals(typeface, SKTypeface.Default) ? TypefaceSource.Shared
        : TypefaceSource.Registrar;

    /// <summary>Returns the cached typeface for a family and style (tests and diagnostics).</summary>
    internal SKTypeface GetTypeface(string family, bool isBold, bool isItalic)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            return GetTypefaceEntryLocked(new TypefaceKey(family, isBold, isItalic)).Typeface;
        }
    }

    private TypefaceEntry GetTypefaceEntryLocked(TypefaceKey key)
    {
        if (_typefaces.TryGet(key, out var entry))
            return entry;

        entry = Resolve(key);
        if (_typefaces.Add(key, entry, out var evicted))
            RetireTypeface(evicted.Key, evicted.Value);
        return entry;
    }

    /// <summary>
    /// Resolves a typeface: a family registered with <see cref="SkiaFontRegistrar"/> wins (its face closest to the
    /// requested style), then the font manager in the requested weight and slant, then the default family in that
    /// style, then <see cref="SKTypeface.Default"/>. The entry carries the face's own weight and slant, so fonts on
    /// it synthesize what it lacks. Runs under the cache lock, so a registrar change waits for it and
    /// then sees (and drops) the typeface this returns.
    /// </summary>
    private static TypefaceEntry Resolve(TypefaceKey key)
    {
        // Custom fonts first: host apps supply fonts the system font manager cannot resolve
        // (e.g. CJK fonts on Android, icon fonts). The registered face closest to the requested style wins;
        // a weight or slant it lacks is synthesized (CreateFont).
        if (SkiaFontRegistrar.TryResolve(key.Family, WeightOf(key.IsBold), SlantOf(key.IsItalic), out var registered,
                out int registeredWeight, out var registeredSlant))
            return new TypefaceEntry(registered, TypefaceSource.Registrar, registeredWeight, registeredSlant);

        if (!key.IsBold && !key.IsItalic && key.Family == "Default")
            return new TypefaceEntry(SKTypeface.Default, TypefaceSource.Shared,
                (int)SKFontStyleWeight.Normal, SKFontStyleSlant.Upright);

        using var style = new SKFontStyle(
            key.IsBold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            key.IsItalic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);

        // Resolve through the font manager with an explicit family so the requested weight and slant are
        // honored. SkiaSharp 4's SKTypeface.FromFamilyName(null, …) falls back to the plain default
        // typeface and drops the style (bold headers rendered regular). An unknown family falls back to
        // the default family in the same style rather than to null.
        var defaultFamily = SKTypeface.Default.FamilyName;
        var requested = key.Family == "Default" ? defaultFamily : key.Family;
        var typeface = SystemTypefacePool.Match(requested, style, out var leased);
        if (typeface is null)
        {
            // SkiaSharp returns null for a family the platform does not know (e.g. a MAUI font alias on
            // Android or Windows). Never cache that null: fall back to the default family in the same
            // style, then to the shared default typeface.
            SkiaDrawingContext.ReportUnknownFamily(requested);
            typeface = SystemTypefacePool.Match(defaultFamily, style, out leased);
        }

        // The style of the face found, so a bold or italic request the platform has no face for is synthesized.
        typeface ??= SKTypeface.Default;
        return new TypefaceEntry(typeface, SourceOf(typeface, leased), typeface.FontWeight, typeface.FontSlant);
    }

    // ── Registrar coordination ───────────────────────────────────

    /// <summary>
    /// Called by <see cref="SkiaFontRegistrar"/> after a change and before it disposes anything: drops the
    /// typefaces of <paramref name="family"/> (or, when <c>null</c>, every typeface that came from the
    /// registrar) and the fonts built on them.
    /// </summary>
    internal void OnRegistrarChanged(string? family)
    {
        lock (_gate)
        {
            if (_disposed) return;

            // A family's text may have been measured in a fallback typeface before it was registered, or in a
            // typeface that is now replaced; registrar changes are rare, so every text entry goes.
            ClearTextLocked();

            var stale = new List<TypefaceKey>();
            if (family is null)
                _typefaces.CollectKeys(static (_, entry) => entry.Source == TypefaceSource.Registrar, stale);
            else
                _typefaces.CollectKeys((key, _) => string.Equals(key.Family, family, StringComparison.OrdinalIgnoreCase), stale);

            foreach (var key in stale)
            {
                if (_typefaces.Remove(key, out var entry))
                    RetireTypeface(key, entry);
            }
        }
    }

    // ── Release ──────────────────────────────────────────────────

    /// <summary>Test seam: invoked with every <see cref="SKPaint"/>, <see cref="SKFont"/> or leased <see cref="SKTypeface"/> the cache releases.</summary>
    internal Action<object>? ResourceReleased { get; set; }

    /// <summary>Removes a typeface entry: the fonts built on it go with it, and a leased typeface is given back.</summary>
    private void RetireTypeface(TypefaceKey key, TypefaceEntry entry)
    {
        var dependents = new List<FontKey>();
        _fonts.CollectKeys((_, font) => font.TypefaceKey == key, dependents);
        foreach (var fontKey in dependents)
        {
            if (_fonts.Remove(fontKey, out var font))
                Retire(font.Font);
        }

        // The registrar and SKTypeface.Default own theirs; only font-manager leases are given back.
        if (entry.Source == TypefaceSource.FontManager)
            Retire(entry.Typeface);
    }

    private void RetireAll()
    {
        ClearTextLocked();

        for (int i = 0; i < _polygonCount; i++)
        {
            Retire(_polygons[i].Path!);
            _polygons[i] = default;
        }
        _polygonCount = 0;
        _nextPolygon = 0;
        if (_scratchPath is not null) Retire(_scratchPath);
        if (_polylinePaint is not null) Retire(_polylinePaint);
        _scratchPath = null;
        _polylinePaint = null;

        var fonts = new List<FontEntry>();
        _fonts.Clear(fonts);
        foreach (var font in fonts)
            Retire(font.Font);

        var paints = new List<SKPaint>();
        _paints.Clear(paints);
        foreach (var paint in paints)
            Retire(paint);

        var typefaces = new List<TypefaceEntry>();
        _typefaces.Clear(typefaces);
        // Cold path (dispose / registrar clear), so LINQ is fine here; the lookup and eviction paths stay allocation-free.
        foreach (var typeface in typefaces.Where(static t => t.Source == TypefaceSource.FontManager))
            Retire(typeface.Typeface);
    }

    /// <summary>Releases <paramref name="resource"/> now, or when the current frame ends if one is being drawn.</summary>
    private void Retire(object resource)
    {
        if (_frameDepth > 0)
            _retired.Add(resource);
        else
            Release(resource);
    }

    private void ReleaseRetired()
    {
        if (_retired.Count == 0) return;
        foreach (var resource in _retired)
            Release(resource);
        _retired.Clear();
    }

    private void RetireRuns(FontRuns[] runs)
    {
        foreach (var line in runs)
            Retire(line);
    }

    private void Release(object resource)
    {
        // Text blobs of cached runs: not one of the native objects the seam reports.
        if (resource is FontRuns runs)
        {
            runs.Dispose();
            return;
        }

        ResourceReleased?.Invoke(resource);
        if (resource is SKTypeface typeface)
            SystemTypefacePool.Release(typeface);
        else
            ((IDisposable)resource).Dispose();
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    // ── Keys and entries ─────────────────────────────────────────

    /// <summary>Cache key for SKPaint objects.</summary>
    private readonly record struct PaintKey(uint Color, float StrokeWidth, SKPaintStyle Style, bool IsAntialias);

    /// <summary>Cache key for SKFont objects.</summary>
    private readonly record struct FontKey(string Family, float Size, bool IsBold, bool IsItalic);

    /// <summary>Cache key for typefaces.</summary>
    private readonly record struct TypefaceKey(string Family, bool IsBold, bool IsItalic);

    private readonly record struct FontEntry(SKFont Font, TypefaceKey TypefaceKey, SKFontMetrics Metrics);

    /// <summary>Cache key for text widths: the text and the font it is measured in.</summary>
    private readonly record struct TextKey(string Text, FontKey Font);

    /// <summary>Cache key for text layouts.</summary>
    private readonly record struct TextLayoutKey(string Text, FontKey Font, float MaxWidth, KumikoTextWrap Wrap, int MaxLines);

    /// <summary>A measured text width, whether the text holds control characters, and the runs it is drawn in.</summary>
    private readonly record struct TextMeasurement(float Width, bool HasControlCharacters, FontRuns Runs);

    /// <summary>A text's lines and the runs each line is drawn in.</summary>
    private readonly record struct TextLayoutEntry(KumikoTextLines Lines, FontRuns[] Runs);

    /// <summary>
    /// A resolved typeface, where it came from, and the weight and slant it has (as registered, for a registered
    /// face), which decide whether a bold or italic font on it is synthesized.
    /// </summary>
    private readonly record struct TypefaceEntry(SKTypeface Typeface, TypefaceSource Source, int Weight, SKFontStyleSlant Slant);

    /// <summary>Cache key for the face chosen for a code point in a family and style.</summary>
    private readonly record struct CharFaceKey(string Family, bool IsBold, bool IsItalic, int Codepoint);

    /// <summary>Cache key for the face of a registrar fallback family in a style.</summary>
    private readonly record struct FallbackFamilyKey(string Family, bool IsBold, bool IsItalic);

    /// <summary>Cache key for the font that draws a fallback face in a size and requested style.</summary>
    private readonly record struct FallbackFontKey(int Face, float Size, bool IsBold, bool IsItalic);

    /// <summary>A fallback typeface, where it came from, a font for glyph checks, and its weight and slant.</summary>
    private readonly record struct FallbackFace(SKTypeface Typeface, TypefaceSource Source, SKFont Probe, int Weight,
        SKFontStyleSlant Slant);

    /// <summary>A cached polygon: the shape's coordinates (relative to its first point, in 1/64 units) and its path.</summary>
    private struct PolygonEntry(int hash, bool close, float[] shape, SKPath path)
    {
        public readonly int Hash = hash;
        public readonly bool Close = close;
        public readonly SKPath? Path = path;
        private readonly float[]? _shape = shape;

        public readonly bool Matches(ReadOnlySpan<KumikoPoint> points)
        {
            if (_shape is null || _shape.Length != (points.Length - 1) * 2) return false;
            for (int i = 1; i < points.Length; i++)
            {
                if (_shape[(i - 1) * 2] != MathF.Round((points[i].X - points[0].X) * PolygonResolution)
                    || _shape[(i - 1) * 2 + 1] != MathF.Round((points[i].Y - points[0].Y) * PolygonResolution))
                    return false;
            }
            return true;
        }
    }

    private enum TypefaceSource
    {
        /// <summary><see cref="SKTypeface.Default"/>; never disposed.</summary>
        Shared,

        /// <summary>Registered with <see cref="SkiaFontRegistrar"/>; the registrar or the app owns it.</summary>
        Registrar,

        /// <summary>Leased from <see cref="SystemTypefacePool"/>; given back on release.</summary>
        FontManager,
    }
}

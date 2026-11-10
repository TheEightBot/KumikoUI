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

    private readonly object _gate = new();
    private readonly LruMap<PaintKey, SKPaint> _paints;
    private readonly LruMap<FontKey, FontEntry> _fonts;
    private readonly LruMap<TypefaceKey, TypefaceEntry> _typefaces;

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
    {
        _paints = new LruMap<PaintKey, SKPaint>(maxPaints);
        _fonts = new LruMap<FontKey, FontEntry>(maxFonts);
        _typefaces = new LruMap<TypefaceKey, TypefaceEntry>(maxTypefaces);
        SkiaFontRegistrar.Attach(this);
    }

    /// <summary>Most <see cref="SKPaint"/> objects the cache keeps.</summary>
    public int MaxPaints => _paints.Capacity;

    /// <summary>Most <see cref="SKFont"/> objects the cache keeps.</summary>
    public int MaxFonts => _fonts.Capacity;

    /// <summary>Most typefaces the cache keeps (one per family, weight and slant).</summary>
    public int MaxTypefaces => _typefaces.Capacity;

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
    internal SKFont GetFont(KumikoFont? font)
    {
        var family = font?.Family ?? "Default";
        var size = font?.Size ?? 14f;
        var isBold = font?.IsBold ?? false;
        var isItalic = font?.IsItalic ?? false;

        var key = new FontKey(family, size, isBold, isItalic);
        lock (_gate)
        {
            if (_fonts.TryGet(key, out var entry))
                return entry.Font;

            ThrowIfDisposed();
            var typefaceKey = new TypefaceKey(family, isBold, isItalic);
            var typeface = GetTypefaceLocked(typefaceKey);
            var skFont = new SKFont(typeface, size)
            {
                Edging = SKFontEdging.SubpixelAntialias,
                Subpixel = true
            };
            if (_fonts.Add(key, new FontEntry(skFont, typefaceKey), out var evicted))
                Retire(evicted.Value.Font);
            return skFont;
        }
    }

    /// <summary>Returns the cached typeface for a family and style (tests and diagnostics).</summary>
    internal SKTypeface GetTypeface(string family, bool isBold, bool isItalic)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            return GetTypefaceLocked(new TypefaceKey(family, isBold, isItalic));
        }
    }

    private SKTypeface GetTypefaceLocked(TypefaceKey key)
    {
        if (_typefaces.TryGet(key, out var entry))
            return entry.Typeface;

        entry = Resolve(key);
        if (_typefaces.Add(key, entry, out var evicted))
            RetireTypeface(evicted.Key, evicted.Value);
        return entry.Typeface;
    }

    /// <summary>
    /// Resolves a typeface: a family registered with <see cref="SkiaFontRegistrar"/> wins, then the font
    /// manager in the requested weight and slant, then the default family in that style, then
    /// <see cref="SKTypeface.Default"/>. Runs under the cache lock, so a registrar change waits for it and
    /// then sees (and drops) the typeface this returns.
    /// </summary>
    private static TypefaceEntry Resolve(TypefaceKey key)
    {
        // Custom fonts first: host apps supply fonts the system font manager cannot resolve
        // (e.g. CJK fonts on Android, icon fonts).
        if (SkiaFontRegistrar.TryGetTypeface(key.Family, out var registered) && registered is not null)
            return new TypefaceEntry(registered, TypefaceSource.Registrar);

        if (!key.IsBold && !key.IsItalic && key.Family == "Default")
            return new TypefaceEntry(SKTypeface.Default, TypefaceSource.Shared);

        var style = new SKFontStyle(
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

        if (typeface is null)
            return new TypefaceEntry(SKTypeface.Default, TypefaceSource.Shared);
        if (leased)
            return new TypefaceEntry(typeface, TypefaceSource.FontManager);
        return new TypefaceEntry(typeface,
            ReferenceEquals(typeface, SKTypeface.Default) ? TypefaceSource.Shared : TypefaceSource.Registrar);
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
        foreach (var typeface in typefaces)
        {
            if (typeface.Source == TypefaceSource.FontManager)
                Retire(typeface.Typeface);
        }
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

    private void Release(object resource)
    {
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

    private readonly record struct FontEntry(SKFont Font, TypefaceKey TypefaceKey);

    private readonly record struct TypefaceEntry(SKTypeface Typeface, TypefaceSource Source);

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

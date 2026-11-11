using SkiaSharp;

namespace KumikoUI.SkiaSharp;

/// <summary>
/// A static registry for custom <see cref="SKTypeface"/> instances used by <see cref="SkiaDrawingContext"/>.
/// Register fonts loaded from streams (e.g. embedded resources, app-bundle raw assets) so they are
/// available to the grid renderer by family name via <see cref="KumikoUI.Core.Rendering.KumikoFont.Family"/>.
/// </summary>
/// <remarks>
/// <para>
/// On some platforms (notably Android) the system font manager does not expose fonts that cover
/// CJK (Chinese / Japanese / Korean) characters or icon-font glyph sets. When <see cref="SkiaDrawingContext"/>
/// cannot find a matching system typeface it falls back to the default typeface, which causes those
/// characters to render as empty boxes or garbled glyphs.
/// </para>
/// <para>
/// Registering a custom typeface here gives <see cref="SkiaDrawingContext"/> an exact match for the
/// requested family name, bypassing the system font manager entirely. Family names are compared
/// case-insensitively.
/// </para>
/// <para><b>Styles:</b> a family can hold several faces, one per weight, width and slant: register the bold or
/// italic file under the same family with <see cref="RegisterTypeface(string, SKTypeface, SKFontStyle)"/> or
/// <see cref="RegisterTypefaceFromStream(string, Stream, SKFontStyle)"/>. A bold or italic
/// <see cref="KumikoUI.Core.Rendering.KumikoFont"/> uses the closest registered face (width first, then slant, then
/// weight, as CSS font matching does); when that face is lighter than semibold for a bold font, or upright for an
/// italic font, the text is emboldened or slanted synthetically.
/// </para>
/// <para><b>Fallback:</b> characters the requested typeface has no glyph for (emoji, CJK, symbols) are drawn with
/// another typeface. The families in <see cref="FallbackFamilies"/> are tried first, in order (registered or
/// installed), then the platform font manager's own choice for the character. Text is split into runs between
/// whole text elements, so an emoji sequence or a letter with combining marks is never drawn in two fonts.
/// </para>
/// <para><b>Lifetime:</b> Registered typefaces are <em>not</em> disposed by
/// <see cref="SkiaDrawingContext"/> or <see cref="SkiaResourceCache"/> — the registrar or the caller owns
/// the lifetime. Typefaces created via <see cref="RegisterTypefaceFromStream(string, Stream)"/> are owned by the
/// registrar and are disposed when they are replaced, unregistered (<see cref="Unregister"/>) or cleared
/// (<see cref="Clear"/>). Typefaces passed directly to <see cref="RegisterTypeface(string, SKTypeface)"/> are
/// <em>not</em> disposed by the registrar; the caller retains ownership and may dispose one once it has been
/// replaced or unregistered.
/// </para>
/// <para><b>Changes while grids are alive:</b> every change increments <see cref="Version"/> and, before the
/// registrar disposes anything, makes every live <see cref="SkiaResourceCache"/> drop the typefaces and
/// fonts of the affected family. The next frame resolves the family again, so replacing, unregistering or
/// clearing fonts at run time is safe and takes effect on the next redraw (invalidate the grid to see it
/// immediately).
/// </para>
/// <para><b>Threading:</b> all members are thread-safe. Registering fonts from a background thread at
/// startup is fine. Prefer to replace, unregister or clear fonts on the UI thread: it is safe from any
/// thread, but a change made while a grid is drawing on another thread waits for that grid's current
/// cache lookup to finish.
/// </para>
/// </remarks>
public static class SkiaFontRegistrar
{
    private static readonly object _lock = new();

    /// <summary>The faces registered under each family, one per weight, width and slant.</summary>
    private static readonly Dictionary<string, List<RegisteredFace>> _registry =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Every registered typeface and the number of registrations (family and style) it has, so
    /// <see cref="IsRegistered"/> is a single hash lookup.
    /// </summary>
    private static readonly Dictionary<SKTypeface, int> _registrations = new(ReferenceEqualityComparer.Instance);

    /// <summary>Typefaces whose lifetime is owned by this registrar (created from streams).</summary>
    private static readonly HashSet<SKTypeface> _ownedTypefaces = new(ReferenceEqualityComparer.Instance);

    /// <summary>Live caches to invalidate on a change. Weak, so a cache that was never disposed can still be collected.</summary>
    private static readonly List<WeakReference<SkiaResourceCache>> _caches = new();

    // Replaced, never mutated, so readers can use the array they read without a lock.
    private static string[] _fallbackFamilies = [];
    private static IReadOnlyList<string> _fallbackFamiliesView = Array.AsReadOnly(_fallbackFamilies);

    private static long _version;

    /// <summary>
    /// Incremented by every registration, replacement, <see cref="Unregister"/>, <see cref="Clear"/> and
    /// <see cref="SetFallbackFamilies"/>.
    /// </summary>
    public static long Version => Interlocked.Read(ref _version);

    /// <summary>
    /// The families tried, in order, for a character the requested typeface has no glyph for, before the
    /// platform font manager's own choice. Each is a registered family or an installed one; families that are
    /// neither are skipped. Empty by default. Set with <see cref="SetFallbackFamilies"/>.
    /// </summary>
    public static IReadOnlyList<string> FallbackFamilies => Volatile.Read(ref _fallbackFamiliesView);

    /// <summary>
    /// Registers a <see cref="SKTypeface"/> under the given <paramref name="family"/> name, replacing every
    /// typeface registered under it (a replaced typeface the registrar owns is disposed). The typeface is used
    /// for every weight and slant of the family, as the face of its own style (bold and italic text use synthetic
    /// styles when it is regular). The caller retains ownership of <paramref name="typeface"/> and must not
    /// dispose it while the registrar holds it.
    /// </summary>
    /// <param name="family">
    /// Family name to register. Must match the value used in
    /// <see cref="KumikoUI.Core.Rendering.KumikoFont.Family"/>.
    /// </param>
    /// <param name="typeface">The <see cref="SKTypeface"/> to register.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="family"/> or <paramref name="typeface"/> is <c>null</c>.</exception>
    public static void RegisterTypeface(string family, SKTypeface typeface)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(typeface);
        Register(family, typeface, owned: false, style: null);
    }

    /// <summary>
    /// Registers a <see cref="SKTypeface"/> as the face of <paramref name="family"/> for one
    /// <paramref name="style"/> (for example the bold file of a family), replacing only a typeface registered
    /// under the same family, weight, width and slant; the family's other faces stay registered. The caller
    /// retains ownership of <paramref name="typeface"/> and must not dispose it while the registrar holds it.
    /// </summary>
    /// <param name="family">
    /// Family name to register. Must match the value used in
    /// <see cref="KumikoUI.Core.Rendering.KumikoFont.Family"/>.
    /// </param>
    /// <param name="typeface">The <see cref="SKTypeface"/> to register.</param>
    /// <param name="style">
    /// The weight, width and slant <paramref name="typeface"/> is used for, for example
    /// <see cref="SKFontStyle.Bold"/> or <see cref="SKFontStyle.Italic"/>. Only its values are kept.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <c>null</c>.</exception>
    public static void RegisterTypeface(string family, SKTypeface typeface, SKFontStyle style)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(typeface);
        ArgumentNullException.ThrowIfNull(style);
        Register(family, typeface, owned: false, StyleOf(style));
    }

    /// <summary>
    /// Creates a <see cref="SKTypeface"/> from the given <paramref name="stream"/> and registers it
    /// under <paramref name="family"/>, replacing every typeface registered under it. The
    /// <see cref="SkiaFontRegistrar"/> takes ownership of the created typeface and disposes it when it is
    /// replaced, unregistered or cleared. The <paramref name="stream"/> may be disposed after this call returns.
    /// </summary>
    /// <param name="family">
    /// Family name to register. Must match the value used in
    /// <see cref="KumikoUI.Core.Rendering.KumikoFont.Family"/>.
    /// </param>
    /// <param name="stream">A readable stream containing a valid TrueType (.ttf) or OpenType (.otf) font.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="family"/> or <paramref name="stream"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the stream cannot be decoded as a valid font.</exception>
    public static void RegisterTypefaceFromStream(string family, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(stream);
        Register(family, CreateTypeface(family, stream), owned: true, style: null);
    }

    /// <summary>
    /// Creates a <see cref="SKTypeface"/> from the given <paramref name="stream"/> and registers it as the face
    /// of <paramref name="family"/> for one <paramref name="style"/>, replacing only a typeface registered under
    /// the same family, weight, width and slant. The <see cref="SkiaFontRegistrar"/> takes ownership of the
    /// created typeface and disposes it when it is replaced, unregistered or cleared. The
    /// <paramref name="stream"/> may be disposed after this call returns.
    /// </summary>
    /// <param name="family">
    /// Family name to register. Must match the value used in
    /// <see cref="KumikoUI.Core.Rendering.KumikoFont.Family"/>.
    /// </param>
    /// <param name="stream">A readable stream containing a valid TrueType (.ttf) or OpenType (.otf) font.</param>
    /// <param name="style">
    /// The weight, width and slant the font is used for, for example <see cref="SKFontStyle.Bold"/>. Only its
    /// values are kept.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the stream cannot be decoded as a valid font.</exception>
    public static void RegisterTypefaceFromStream(string family, Stream stream, SKFontStyle style)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(style);
        var key = StyleOf(style);
        Register(family, CreateTypeface(family, stream), owned: true, key);
    }

    /// <summary>
    /// Sets <see cref="FallbackFamilies"/>: the families tried, in order, for characters the requested typeface
    /// has no glyph for, before the platform font manager's own choice. For example, register NotoSansJP and
    /// a color emoji font and list them here so Japanese text and emoji render the same on every platform.
    /// Every <see cref="SkiaResourceCache"/> drops its text measurements and fallback choices, so the next frame
    /// uses the new list.
    /// </summary>
    /// <param name="families">The family names, most preferred first; none to use only the font manager.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="families"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when a family name is <c>null</c>, empty or white space.</exception>
    public static void SetFallbackFamilies(params string[] families)
    {
        ArgumentNullException.ThrowIfNull(families);
        foreach (var family in families)
        {
            if (string.IsNullOrWhiteSpace(family))
                throw new ArgumentException("Fallback family names must not be null, empty or white space.", nameof(families));
        }

        string[] copy = [.. families];
        SkiaResourceCache[] caches;
        lock (_lock)
        {
            _fallbackFamilies = copy;
            Volatile.Write(ref _fallbackFamiliesView, Array.AsReadOnly(copy));
            Interlocked.Increment(ref _version);
            caches = LiveCaches();
        }

        foreach (var cache in caches)
            cache.OnFallbackFamiliesChanged();
    }

    /// <summary>
    /// Creates one <see cref="SKTypeface"/> from <paramref name="stream"/> and registers it under every
    /// name in <paramref name="families"/> (for example a MAUI font alias and its file name), replacing
    /// whatever is registered under those names. The registrar owns the typeface and disposes it once
    /// it is no longer registered under any family. Null, empty and duplicate names are ignored.
    /// </summary>
    /// <param name="stream">A readable stream containing a valid TrueType (.ttf) or OpenType (.otf) font.</param>
    /// <param name="families">The family names to register the font under.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> or <paramref name="families"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="families"/> has no usable name.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the stream cannot be decoded as a valid font.</exception>
    public static void RegisterTypefaceFamiliesFromStream(Stream stream, IEnumerable<string?> families)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(families);

        // Distinct keeps the first spelling of each name, in order.
        var names = families
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (names.Count == 0)
            throw new ArgumentException("At least one family name is required.", nameof(families));

        var typeface = SKTypeface.FromStream(stream)
            ?? throw new InvalidOperationException(
                $"Could not create a typeface from the provided stream for family '{names[0]}'.");

        foreach (var name in names)
            Register(name, typeface, owned: true, style: null);
    }

    /// <summary>
    /// Removes every typeface registered under <paramref name="family"/> (all of its styles). Every
    /// <see cref="SkiaResourceCache"/> drops them first; then those the registrar owns (they came from
    /// <see cref="RegisterTypefaceFromStream(string, Stream)"/>) that are not registered elsewhere are disposed.
    /// Later frames resolve the family through the system font manager again. <see cref="FallbackFamilies"/> is
    /// left unchanged.
    /// </summary>
    /// <param name="family">The family name to remove.</param>
    /// <returns><c>true</c> if a typeface was registered under <paramref name="family"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="family"/> is <c>null</c>.</exception>
    public static bool Unregister(string family)
    {
        ArgumentNullException.ThrowIfNull(family);

        using var retired = new RetiredTypefaces();
        SkiaResourceCache[] caches;
        lock (_lock)
        {
            if (!_registry.Remove(family, out var previous))
                return false;
            foreach (var face in previous)
                RemoveRegistration(face.Typeface, retired);
            Interlocked.Increment(ref _version);
            caches = LiveCaches();
        }

        // The retired typefaces are disposed when the method exits, after the caches dropped them,
        // even if an invalidation throws.
        Invalidate(caches, family);
        return true;
    }

    /// <summary>
    /// Returns <c>true</c> if a typeface is registered under the given <paramref name="family"/> name.
    /// </summary>
    /// <param name="family">The family name to look up.</param>
    /// <param name="typeface">
    /// When this method returns <c>true</c>, contains the registered typeface closest to the regular (normal
    /// weight, upright) style; otherwise <c>null</c>.
    /// </param>
    public static bool TryGetTypeface(string family, out SKTypeface? typeface)
    {
        lock (_lock)
        {
            if (_registry.TryGetValue(family, out var faces))
            {
                typeface = faces[FindClosest(faces, (int)SKFontStyleWeight.Normal, (int)SKFontStyleWidth.Normal,
                    SKFontStyleSlant.Upright)].Typeface;
                return true;
            }
            typeface = null;
            return false;
        }
    }

    /// <summary>
    /// Returns <c>true</c> if a typeface is registered under the given <paramref name="family"/> name, and the
    /// registered face closest to <paramref name="style"/> (width first, then slant, then weight).
    /// </summary>
    /// <param name="family">The family name to look up.</param>
    /// <param name="style">The weight, width and slant wanted.</param>
    /// <param name="typeface">
    /// When this method returns <c>true</c>, contains the closest registered typeface; otherwise <c>null</c>.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="family"/> or <paramref name="style"/> is <c>null</c>.</exception>
    public static bool TryGetTypeface(string family, SKFontStyle style, out SKTypeface? typeface)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(style);
        var wanted = StyleOf(style);
        lock (_lock)
        {
            if (_registry.TryGetValue(family, out var faces))
            {
                typeface = faces[FindClosest(faces, wanted.Weight, wanted.Width, wanted.Slant)].Typeface;
                return true;
            }
            typeface = null;
            return false;
        }
    }

    /// <summary>
    /// Returns <c>true</c> if <paramref name="typeface"/> is currently registered (under any family).
    /// A single hash lookup. Used by <see cref="SkiaResourceCache"/> to avoid disposing typefaces it does not own.
    /// </summary>
    /// <param name="typeface">The typeface instance to check.</param>
    public static bool IsRegistered(SKTypeface typeface)
    {
        if (typeface is null) return false;
        lock (_lock)
        {
            return _registrations.ContainsKey(typeface);
        }
    }

    /// <summary>
    /// Removes all registered typefaces and empties <see cref="FallbackFamilies"/>. Every
    /// <see cref="SkiaResourceCache"/> drops the registered typefaces it holds first; then typefaces created via
    /// <see cref="RegisterTypefaceFromStream(string, Stream)"/> are disposed. Those registered via
    /// <see cref="RegisterTypeface(string, SKTypeface)"/> are <em>not</em> disposed (caller owns them).
    /// Safe to call while grids are alive.
    /// </summary>
    public static void Clear()
    {
        SKTypeface[] retired;
        SkiaResourceCache[] caches;
        lock (_lock)
        {
            retired = [.. _ownedTypefaces];
            _ownedTypefaces.Clear();
            _registrations.Clear();
            _registry.Clear();
            _fallbackFamilies = [];
            Volatile.Write(ref _fallbackFamiliesView, Array.AsReadOnly(_fallbackFamilies));
            Interlocked.Increment(ref _version);
            caches = LiveCaches();
        }

        try
        {
            Invalidate(caches, family: null);
        }
        finally
        {
            foreach (var typeface in retired)
                typeface.Dispose();
        }
    }

    // ── Resolution (used by SkiaResourceCache) ───────────────────

    /// <summary>The fallback family list, for the caches. The array is never mutated.</summary>
    internal static string[] FallbackFamilySnapshot => Volatile.Read(ref _fallbackFamilies);

    /// <summary>
    /// The registered face of <paramref name="family"/> closest to <paramref name="weight"/> and
    /// <paramref name="slant"/> (normal width), and the style it was registered for, so the caller can tell
    /// whether a synthetic bold or slant is needed.
    /// </summary>
    internal static bool TryResolve(string family, int weight, SKFontStyleSlant slant, out SKTypeface typeface,
        out int registeredWeight, out SKFontStyleSlant registeredSlant)
    {
        lock (_lock)
        {
            if (_registry.TryGetValue(family, out var faces))
            {
                var face = faces[FindClosest(faces, weight, (int)SKFontStyleWidth.Normal, slant)];
                typeface = face.Typeface;
                registeredWeight = face.Style.Weight;
                registeredSlant = face.Style.Slant;
                return true;
            }
        }
        typeface = null!;
        registeredWeight = 0;
        registeredSlant = SKFontStyleSlant.Upright;
        return false;
    }

    /// <summary>
    /// The index of the face in <paramref name="faces"/> (not empty) that best matches the requested style:
    /// the closest width, then the closest slant (italic and oblique stand in for each other before upright),
    /// then the closest weight, preferring lighter faces for a regular request and heavier ones for a bold one.
    /// </summary>
    internal static int FindClosest(IReadOnlyList<RegisteredFace> faces, int weight, int width, SKFontStyleSlant slant)
    {
        int best = 0;
        long bestScore = long.MaxValue;
        for (int i = 0; i < faces.Count; i++)
        {
            var style = faces[i].Style;
            long score = Math.Abs(style.Width - width) * 100_000L
                + SlantDistance(slant, style.Slant) * 10_000L
                + WeightDistance(weight, style.Weight);
            if (score < bestScore)
            {
                bestScore = score;
                best = i;
            }
        }
        return best;
    }

    private static int SlantDistance(SKFontStyleSlant wanted, SKFontStyleSlant actual)
    {
        if (wanted == actual) return 0;
        bool wantedSloped = wanted != SKFontStyleSlant.Upright;
        bool actualSloped = actual != SKFontStyleSlant.Upright;
        return wantedSloped == actualSloped ? 1 : 2;
    }

    private static int WeightDistance(int wanted, int actual)
    {
        if (wanted == actual) return 0;
        int distance = Math.Abs(wanted - actual);
        // CSS: up to 500 look lighter first, above 500 look heavier first.
        bool preferred = wanted <= 500 ? actual < wanted : actual > wanted;
        return preferred ? distance : distance + 1_000;
    }

    // ── Cache tracking ───────────────────────────────────────────

    internal static void Attach(SkiaResourceCache cache)
    {
        lock (_lock)
            _caches.Add(new WeakReference<SkiaResourceCache>(cache));
    }

    internal static void Detach(SkiaResourceCache cache)
    {
        lock (_lock)
        {
            for (int i = _caches.Count - 1; i >= 0; i--)
            {
                if (!_caches[i].TryGetTarget(out var target) || ReferenceEquals(target, cache))
                    _caches.RemoveAt(i);
            }
        }
    }

    /// <summary>Number of tracked caches (tests only).</summary>
    internal static int AttachedCacheCount
    {
        get { lock (_lock) return _caches.Count; }
    }

    // ── Internals ────────────────────────────────────────────────

    /// <summary>A registered face: the typeface and the style it is registered for.</summary>
    internal readonly record struct RegisteredFace(SKTypeface Typeface, FaceStyle Style);

    /// <summary>The values of an <see cref="SKFontStyle"/> (which is a native object).</summary>
    internal readonly record struct FaceStyle(int Weight, int Width, SKFontStyleSlant Slant);

    private static FaceStyle StyleOf(SKFontStyle style) => new(style.Weight, style.Width, style.Slant);

    private static SKTypeface CreateTypeface(string family, Stream stream) =>
        SKTypeface.FromStream(stream)
        ?? throw new InvalidOperationException(
            $"Could not create a typeface from the provided stream for family '{family}'.");

    /// <summary>
    /// Registers <paramref name="typeface"/> under <paramref name="family"/>: as the family's only face when
    /// <paramref name="style"/> is <c>null</c> (in the typeface's own style), else as the face for that style.
    /// </summary>
    private static void Register(string family, SKTypeface typeface, bool owned, FaceStyle? style)
    {
        using var retired = new RetiredTypefaces();
        SkiaResourceCache[] caches;
        lock (_lock)
        {
            _registry.TryGetValue(family, out var faces);
            bool unchanged = style is { } requested
                ? faces?.Exists(f => f.Style == requested && ReferenceEquals(f.Typeface, typeface)) == true
                : faces is { Count: 1 } && ReferenceEquals(faces[0].Typeface, typeface);
            if (owned) _ownedTypefaces.Add(typeface);
            if (unchanged)
                return;   // Same instance again: only the ownership can change.

            // Counted before the replaced faces are dropped, so a typeface that replaces itself is never retired.
            _registrations[typeface] = _registrations.TryGetValue(typeface, out var count) ? count + 1 : 1;
            if (style is not { } wanted)
            {
                if (faces is not null)
                {
                    foreach (var face in faces)
                        RemoveRegistration(face.Typeface, retired);
                }
                faces = [new RegisteredFace(typeface,
                    new FaceStyle(typeface.FontWeight, typeface.FontWidth, typeface.FontSlant))];
                _registry[family] = faces;
            }
            else
            {
                faces ??= _registry[family] = [];
                int existing = faces.FindIndex(f => f.Style == wanted);
                if (existing >= 0)
                {
                    RemoveRegistration(faces[existing].Typeface, retired);
                    faces[existing] = new RegisteredFace(typeface, wanted);
                }
                else
                {
                    faces.Add(new RegisteredFace(typeface, wanted));
                }
            }

            Interlocked.Increment(ref _version);
            caches = LiveCaches();
        }

        // Caches drop what they resolved for this family (the replaced typeface, or a system fallback
        // cached before the family was registered) before the replaced typeface is disposed, which happens
        // when the method exits, even if an invalidation throws.
        Invalidate(caches, family);
    }

    /// <summary>Typefaces retired by a registry change; disposes them when the scope that owns it ends.</summary>
    private sealed class RetiredTypefaces : List<SKTypeface>, IDisposable
    {
        public void Dispose()
        {
            foreach (var typeface in this)
                typeface.Dispose();
        }
    }

    /// <summary>
    /// Drops one registration of <paramref name="typeface"/>; adds it to <paramref name="retired"/> when that was
    /// its last registration and the registrar owns it, so the caller disposes it after invalidating caches.
    /// </summary>
    private static void RemoveRegistration(SKTypeface typeface, List<SKTypeface> retired)
    {
        if (_registrations.TryGetValue(typeface, out var count) && count > 1)
        {
            _registrations[typeface] = count - 1;
            return;
        }
        _registrations.Remove(typeface);
        if (_ownedTypefaces.Remove(typeface))
            retired.Add(typeface);
    }

    /// <summary>Snapshot of the live caches; prunes collected ones. Call under <see cref="_lock"/>.</summary>
    private static SkiaResourceCache[] LiveCaches()
    {
        var live = new List<SkiaResourceCache>(_caches.Count);
        for (int i = _caches.Count - 1; i >= 0; i--)
        {
            if (_caches[i].TryGetTarget(out var cache))
                live.Add(cache);
            else
                _caches.RemoveAt(i);
        }
        return [.. live];
    }

    /// <summary>
    /// Makes every cache drop <paramref name="family"/> (or every registered typeface when <c>null</c>).
    /// Runs outside the registrar lock: a cache takes its own lock, which waits for a lookup in flight
    /// on a render thread, and that lookup may itself read the registry.
    /// </summary>
    private static void Invalidate(SkiaResourceCache[] caches, string? family)
    {
        foreach (var cache in caches)
            cache.OnRegistrarChanged(family);
    }
}

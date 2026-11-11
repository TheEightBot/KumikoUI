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
/// <para><b>Lifetime:</b> Registered typefaces are <em>not</em> disposed by
/// <see cref="SkiaDrawingContext"/> or <see cref="SkiaResourceCache"/> — the registrar or the caller owns
/// the lifetime. Typefaces created via <see cref="RegisterTypefaceFromStream"/> are owned by the registrar
/// and are disposed when they are replaced, unregistered (<see cref="Unregister"/>) or cleared
/// (<see cref="Clear"/>). Typefaces passed directly to <see cref="RegisterTypeface"/> are <em>not</em>
/// disposed by the registrar; the caller retains ownership and may dispose one once it has been
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

    private static readonly Dictionary<string, SKTypeface> _registry =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Every registered typeface and the number of families it is registered under, so
    /// <see cref="IsRegistered"/> is a single hash lookup.
    /// </summary>
    private static readonly Dictionary<SKTypeface, int> _registrations = new(ReferenceEqualityComparer.Instance);

    /// <summary>Typefaces whose lifetime is owned by this registrar (created from streams).</summary>
    private static readonly HashSet<SKTypeface> _ownedTypefaces = new(ReferenceEqualityComparer.Instance);

    /// <summary>Live caches to invalidate on a change. Weak, so a cache that was never disposed can still be collected.</summary>
    private static readonly List<WeakReference<SkiaResourceCache>> _caches = new();

    private static long _version;

    /// <summary>
    /// Incremented by every registration, replacement, <see cref="Unregister"/> and <see cref="Clear"/>.
    /// </summary>
    public static long Version => Interlocked.Read(ref _version);

    /// <summary>
    /// Registers a <see cref="SKTypeface"/> under the given <paramref name="family"/> name, replacing any
    /// typeface registered under it (a replaced typeface the registrar owns is disposed).
    /// The caller retains ownership of <paramref name="typeface"/> and must not dispose it while the
    /// registrar holds it.
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
        Register(family, typeface, owned: false);
    }

    /// <summary>
    /// Creates a <see cref="SKTypeface"/> from the given <paramref name="stream"/> and registers it
    /// under <paramref name="family"/>, replacing any typeface registered under it. The
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

        var typeface = SKTypeface.FromStream(stream)
            ?? throw new InvalidOperationException(
                $"Could not create a typeface from the provided stream for family '{family}'.");

        Register(family, typeface, owned: true);
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
            Register(name, typeface, owned: true);
    }

    /// <summary>
    /// Removes the typeface registered under <paramref name="family"/>. Every <see cref="SkiaResourceCache"/>
    /// drops it first; then, if the registrar owns it (it came from <see cref="RegisterTypefaceFromStream"/>)
    /// and it is not registered under another family, it is disposed. Later frames resolve the family
    /// through the system font manager again.
    /// </summary>
    /// <param name="family">The family name to remove.</param>
    /// <returns><c>true</c> if a typeface was registered under <paramref name="family"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="family"/> is <c>null</c>.</exception>
    public static bool Unregister(string family)
    {
        ArgumentNullException.ThrowIfNull(family);

        SKTypeface? retired;
        SkiaResourceCache[] caches;
        lock (_lock)
        {
            if (!_registry.Remove(family, out var previous))
                return false;
            retired = RemoveRegistration(previous);
            Interlocked.Increment(ref _version);
            caches = LiveCaches();
        }

        // Disposed after the caches dropped it, even if an invalidation throws
        using (retired)
        {
            Invalidate(caches, family);
        }
        return true;
    }

    /// <summary>
    /// Returns <c>true</c> if a typeface is registered under the given <paramref name="family"/> name.
    /// </summary>
    /// <param name="family">The family name to look up.</param>
    /// <param name="typeface">
    /// When this method returns <c>true</c>, contains the registered typeface; otherwise <c>null</c>.
    /// </param>
    public static bool TryGetTypeface(string family, out SKTypeface? typeface)
    {
        lock (_lock)
        {
            return _registry.TryGetValue(family, out typeface);
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
    /// Removes all registered typefaces. Every <see cref="SkiaResourceCache"/> drops the registered
    /// typefaces it holds first; then typefaces created via <see cref="RegisterTypefaceFromStream"/> are
    /// disposed. Those registered via <see cref="RegisterTypeface"/> are <em>not</em> disposed (caller owns them).
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

    private static void Register(string family, SKTypeface typeface, bool owned)
    {
        SKTypeface? retired = null;
        SkiaResourceCache[] caches;
        lock (_lock)
        {
            if (_registry.TryGetValue(family, out var previous))
            {
                if (ReferenceEquals(previous, typeface))
                {
                    // Same instance again: only the ownership can change.
                    if (owned) _ownedTypefaces.Add(typeface);
                    return;
                }
                retired = RemoveRegistration(previous);
            }

            _registry[family] = typeface;
            _registrations[typeface] = _registrations.TryGetValue(typeface, out var count) ? count + 1 : 1;
            if (owned) _ownedTypefaces.Add(typeface);
            Interlocked.Increment(ref _version);
            caches = LiveCaches();
        }

        // Caches drop what they resolved for this family (the replaced typeface, or a system fallback
        // cached before the family was registered) before the replaced typeface is disposed.
        // Disposed after the caches dropped it, even if an invalidation throws
        using (retired)
        {
            Invalidate(caches, family);
        }
    }

    /// <summary>
    /// Drops one registration of <paramref name="typeface"/>; returns it when that was its last
    /// registration and the registrar owns it, so the caller disposes it after invalidating caches.
    /// </summary>
    private static SKTypeface? RemoveRegistration(SKTypeface typeface)
    {
        if (_registrations.TryGetValue(typeface, out var count) && count > 1)
        {
            _registrations[typeface] = count - 1;
            return null;
        }
        _registrations.Remove(typeface);
        return _ownedTypefaces.Remove(typeface) ? typeface : null;
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

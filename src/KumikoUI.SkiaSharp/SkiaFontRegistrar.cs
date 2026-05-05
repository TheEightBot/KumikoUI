using SkiaSharp;

namespace KumikoUI.SkiaSharp;

/// <summary>
/// A static registry for custom <see cref="SKTypeface"/> instances used by <see cref="SkiaDrawingContext"/>.
/// Register fonts loaded from streams (e.g. embedded resources, app-bundle raw assets) so they are
/// available to the grid renderer by family name via <see cref="KumikoUI.Core.Rendering.GridFont.Family"/>.
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
/// requested family name, bypassing the system font manager entirely.
/// </para>
/// <para><b>Lifetime:</b> Registered typefaces are <em>not</em> disposed by
/// <see cref="SkiaDrawingContext"/> — the registrar or the caller owns the lifetime.
/// Typefaces created via <see cref="RegisterTypefaceFromStream"/> are owned by the registrar and
/// are disposed when <see cref="Clear"/> is called.
/// Typefaces passed directly to <see cref="RegisterTypeface"/> are <em>not</em> disposed by the
/// registrar; the caller retains ownership.
/// </para>
/// </remarks>
public static class SkiaFontRegistrar
{
    private static readonly Dictionary<string, SKTypeface> _registry =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Set of typefaces whose lifetime is owned by this registrar (created from streams).</summary>
    private static readonly HashSet<SKTypeface> _ownedTypefaces = [];

    private static readonly object _lock = new();

    /// <summary>
    /// Registers a <see cref="SKTypeface"/> under the given <paramref name="family"/> name.
    /// The caller retains ownership and must not dispose the typeface while the registrar holds it.
    /// </summary>
    /// <param name="family">
    /// Family name to register. Must match the value used in
    /// <see cref="KumikoUI.Core.Rendering.GridFont.Family"/>.
    /// </param>
    /// <param name="typeface">The <see cref="SKTypeface"/> to register.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="typeface"/> is <c>null</c>.</exception>
    public static void RegisterTypeface(string family, SKTypeface typeface)
    {
        ArgumentNullException.ThrowIfNull(typeface);

        lock (_lock)
        {
            _registry[family] = typeface;
        }
    }

    /// <summary>
    /// Creates a <see cref="SKTypeface"/> from the given <paramref name="stream"/> and registers it
    /// under <paramref name="family"/>. The <see cref="SkiaFontRegistrar"/> takes ownership of the
    /// created typeface and disposes it on <see cref="Clear"/>.
    /// The <paramref name="stream"/> may be disposed after this call returns.
    /// </summary>
    /// <param name="family">
    /// Family name to register. Must match the value used in
    /// <see cref="KumikoUI.Core.Rendering.GridFont.Family"/>.
    /// </param>
    /// <param name="stream">A readable stream containing a valid TrueType (.ttf) or OpenType (.otf) font.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the stream cannot be decoded as a valid font.</exception>
    public static void RegisterTypefaceFromStream(string family, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var typeface = SKTypeface.FromStream(stream)
            ?? throw new InvalidOperationException(
                $"Could not create a typeface from the provided stream for family '{family}'.");

        lock (_lock)
        {
            // If a previous owned typeface exists under this name, dispose it.
            if (_registry.TryGetValue(family, out var previous) && _ownedTypefaces.Remove(previous))
                previous.Dispose();

            _registry[family] = typeface;
            _ownedTypefaces.Add(typeface);
        }
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
    /// Returns <c>true</c> if <paramref name="typeface"/> was registered via this registrar.
    /// Used internally by <see cref="SkiaDrawingContext"/> to avoid disposing externally owned typefaces.
    /// </summary>
    /// <param name="typeface">The typeface instance to check.</param>
    public static bool IsRegistered(SKTypeface typeface)
    {
        lock (_lock)
        {
            return _registry.ContainsValue(typeface);
        }
    }

    /// <summary>
    /// Removes all registered typefaces. Typefaces created via
    /// <see cref="RegisterTypefaceFromStream"/> are disposed; those registered via
    /// <see cref="RegisterTypeface"/> are <em>not</em> disposed (caller owns them).
    /// </summary>
    public static void Clear()
    {
        lock (_lock)
        {
            foreach (var owned in _ownedTypefaces)
                owned.Dispose();

            _ownedTypefaces.Clear();
            _registry.Clear();
        }
    }
}

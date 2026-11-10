using SkiaSharp;

namespace KumikoUI.SkiaSharp;

/// <summary>
/// Reference counts the typefaces <see cref="SkiaResourceCache"/> instances resolve through the font
/// manager. SkiaSharp hands out one managed <see cref="SKTypeface"/> per native typeface, so two caches
/// (two grids) asking for the same family and style receive the same object; it is disposed only
/// when the last cache releases it.
/// </summary>
/// <remarks>
/// Matching and leasing happen under one lock, as do releasing and disposing, so a typeface is never
/// disposed between another cache's match and its lease.
/// </remarks>
internal static class SystemTypefacePool
{
    private static readonly Dictionary<SKTypeface, int> _leases = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Resolves <paramref name="family"/> in <paramref name="style"/> through
    /// <see cref="SkiaDrawingContext.FamilyMatcher"/>. <paramref name="leased"/> is <c>true</c> when the
    /// result must be given back with <see cref="Release"/>; the shared default typeface and typefaces
    /// registered with <see cref="SkiaFontRegistrar"/> are never leased.
    /// </summary>
    public static SKTypeface? Match(string family, SKFontStyle style, out bool leased)
    {
        lock (_leases)
        {
            var typeface = SkiaDrawingContext.FamilyMatcher(family, style);
            if (typeface is null || ReferenceEquals(typeface, SKTypeface.Default) || SkiaFontRegistrar.IsRegistered(typeface))
            {
                leased = false;
                return typeface;
            }
            _leases[typeface] = _leases.TryGetValue(typeface, out var count) ? count + 1 : 1;
            leased = true;
            return typeface;
        }
    }

    /// <summary>Releases one lease; returns <c>true</c> when it was the last one and the typeface was disposed.</summary>
    public static bool Release(SKTypeface typeface)
    {
        lock (_leases)
        {
            if (!_leases.TryGetValue(typeface, out var count))
                return false;
            if (count > 1)
            {
                _leases[typeface] = count - 1;
                return false;
            }
            _leases.Remove(typeface);

            // An app may have registered this very instance under an alias since it was leased.
            if (SkiaFontRegistrar.IsRegistered(typeface))
                return false;
            typeface.Dispose();
            return true;
        }
    }

    /// <summary>Number of outstanding leases on <paramref name="typeface"/> (tests only).</summary>
    internal static int LeaseCount(SKTypeface typeface)
    {
        lock (_leases)
            return _leases.TryGetValue(typeface, out var count) ? count : 0;
    }
}

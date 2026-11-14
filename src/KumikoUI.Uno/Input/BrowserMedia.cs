#if BROWSERWASM
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
#endif

namespace KumikoUI.Uno.Input;

/// <summary>
/// CSS media queries of the browser the WebAssembly head runs in (#101): whether the user asked for reduced motion
/// (<c>prefers-reduced-motion: reduce</c>) and whether the main pointer is a finger (<c>pointer: coarse</c>). Every
/// other head answers false.
/// </summary>
internal static partial class BrowserMedia
{
    /// <summary>Whether the browser reports <c>prefers-reduced-motion: reduce</c>.</summary>
    public static bool PrefersReducedMotion => Matches("(prefers-reduced-motion: reduce)");

    /// <summary>Whether the browser's main pointer is coarse (a touch screen).</summary>
    public static bool HasCoarsePointer => Matches("(pointer: coarse)");

    private static bool Matches(string query)
    {
#if BROWSERWASM
        if (!OperatingSystem.IsBrowser()) return false;
        try
        {
            using var list = MatchMedia(query);
            return list.GetPropertyAsBoolean("matches");
        }
        catch (Exception)
        {
            return false; // no matchMedia (a worker, an old browser): nothing to honor
        }
#else
        return false;
#endif
    }

#if BROWSERWASM
    [JSImport("globalThis.matchMedia")]
    [SupportedOSPlatform("browser")]
    private static partial JSObject MatchMedia(string query);
#endif
}

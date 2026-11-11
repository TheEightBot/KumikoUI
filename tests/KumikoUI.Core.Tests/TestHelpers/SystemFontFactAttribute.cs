using SkiaSharp;

namespace KumikoUI.Core.Tests.TestHelpers;

/// <summary>
/// A <see cref="FactAttribute"/> that skips its test, with the reason, when no installed font has a glyph for one of
/// the given code points (for example a Linux CI host without emoji or CJK fonts), so font fallback tests check what
/// the platform can render instead of failing where it cannot.
/// </summary>
public sealed class SystemFontFactAttribute : FactAttribute
{
    /// <summary>Skips the test unless the font manager finds a font for every code point.</summary>
    /// <param name="codepoints">The code points the test needs an installed font for.</param>
    public SystemFontFactAttribute(params int[] codepoints)
    {
        foreach (int codepoint in codepoints)
        {
            if (!HasInstalledFont(codepoint))
            {
                Skip = $"No installed font has a glyph for U+{codepoint:X4}, so this platform cannot draw it " +
                       "without a registered fallback font.";
                return;
            }
        }
    }

    /// <summary>Whether the font manager finds a typeface with a glyph for <paramref name="codepoint"/>.</summary>
    public static bool HasInstalledFont(int codepoint)
    {
        // Shared with every other caller of the font manager, so not disposed here.
        var typeface = SKFontManager.Default.MatchCharacter(codepoint);
        if (typeface is null) return false;
        using var font = new SKFont(typeface);
        return font.ContainsGlyph(codepoint);
    }
}

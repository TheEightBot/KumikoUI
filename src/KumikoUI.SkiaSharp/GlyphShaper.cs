using SkiaSharp;
using SkiaSharp.HarfBuzz;

namespace KumikoUI.SkiaSharp;

/// <summary>
/// Shapes the runs that hold emoji sequences with HarfBuzz, so a skin-tone modifier, a ZWJ family, a flag or a
/// variation selector is drawn as the one glyph the emoji font has for the whole sequence. Skia alone maps every
/// code point to a glyph of its own, which draws the base emoji and the modifier (a color swatch) side by side.
/// </summary>
/// <remarks>
/// One shaper per typeface, owned and disposed by <see cref="SkiaResourceCache"/>, which also serializes every call
/// (a HarfBuzz font is mutated while it shapes). Shaping happens only the first time a run is measured or drawn; the
/// width and the <see cref="SKTextBlob"/> are cached with the run, so drawing it again allocates nothing. When
/// HarfBuzz cannot be loaded (a platform without its native library), <see cref="IsAvailable"/> turns <c>false</c>
/// for the process and the runs are drawn glyph by glyph as before.
/// </remarks>
internal sealed class GlyphShaper : IDisposable
{
    private static volatile bool s_unavailable;

    private readonly SKShaper _shaper;

    private GlyphShaper(SKShaper shaper) => _shaper = shaper;

    /// <summary>Whether HarfBuzz can be used; <c>false</c> after it failed to load once (or when a test turned it off).</summary>
    public static bool IsAvailable => !s_unavailable;

    /// <summary>Turns shaping off for the process (tests simulate a platform without HarfBuzz).</summary>
    internal static void SetAvailable(bool available) => s_unavailable = !available;

    /// <summary>Creates the shaper of <paramref name="typeface"/>, or <c>null</c> when HarfBuzz is not available.</summary>
    public static GlyphShaper? TryCreate(SKTypeface typeface)
    {
        if (s_unavailable) return null;
        try
        {
            return new GlyphShaper(new SKShaper(typeface));
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException
                                       or TypeInitializationException or BadImageFormatException
                                       or NotSupportedException or InvalidOperationException)
        {
            s_unavailable = true;
            System.Diagnostics.Trace.TraceWarning(
                $"KumikoUI: HarfBuzz is not available ({ex.GetType().Name}); emoji sequences are drawn glyph by glyph.");
            return null;
        }
    }

    /// <summary>
    /// Whether a text element (one grapheme cluster) is an emoji sequence that needs shaping to be drawn as one glyph:
    /// more than one code point, with a joiner, a variation selector, a skin-tone modifier, a regional indicator
    /// (flag), a tag character or a keycap among them.
    /// </summary>
    public static bool NeedsShaping(ReadOnlySpan<char> element)
    {
        if (element.Length < 2) return false;
        int count = 0;
        bool marker = false;
        foreach (var rune in element.EnumerateRunes())
        {
            count++;
            int v = rune.Value;
            marker |= v is 0x200D or 0xFE0F or 0x20E3
                or >= 0x1F3FB and <= 0x1F3FF
                or >= 0x1F1E6 and <= 0x1F1FF
                or >= 0xE0020 and <= 0xE007F;
        }
        return count > 1 && marker;
    }

    /// <summary>The advance width of <paramref name="text"/> shaped in <paramref name="font"/>.</summary>
    public float Measure(ReadOnlySpan<char> text, SKFont font) => _shaper.Shape(text.ToString(), 0f, 0f, font).Width;

    /// <summary>
    /// A text blob of <paramref name="text"/> shaped in <paramref name="font"/>, positioned from the origin (the
    /// baseline start), or <c>null</c> when the text has no glyphs.
    /// </summary>
    public SKTextBlob? CreateBlob(ReadOnlySpan<char> text, SKFont font)
    {
        var result = _shaper.Shape(text.ToString(), 0f, 0f, font);
        int count = result.Codepoints.Length;
        if (count == 0) return null;

        var glyphs = new ushort[count];
        for (int i = 0; i < count; i++)
            glyphs[i] = (ushort)result.Codepoints[i];

        using var builder = new SKTextBlobBuilder();
        builder.AddPositionedRun(glyphs, font, result.Points);
        return builder.Build();
    }

    /// <inheritdoc />
    public void Dispose() => _shaper.Dispose();
}

using KumikoUI.SkiaSharp;

namespace KumikoUI.SkiaSharp.Tests;

/// <summary>The embedded fonts and a registrar that starts every test from the same state.</summary>
internal static class TestFonts
{
    /// <summary>The family the Latin test font (Open Sans Regular) is registered under.</summary>
    public const string Latin = "KumikoTestLatin";

    /// <summary>The family the CJK test font (Noto Sans JP) is registered under.</summary>
    public const string Cjk = "KumikoTestCjk";

    /// <summary>The family the generated emoji test font (ligature glyphs for four sequences) is registered under.</summary>
    public const string Emoji = "KumikoTestEmoji";

    /// <summary>A fresh stream over an embedded font.</summary>
    public static Stream Open(string fileName) =>
        typeof(TestFonts).Assembly.GetManifestResourceStream("KumikoUI.SkiaSharp.Tests." + fileName)
            ?? throw new InvalidOperationException($"The embedded test font {fileName} is missing.");

    /// <summary>
    /// Clears the process-wide registrar, then registers the Latin font and optionally the CJK and the emoji fonts as
    /// fallbacks (the emoji font first, so it wins for the code points only it has).
    /// </summary>
    public static void Reset(bool withCjkFallback = false, bool withEmojiFallback = false)
    {
        SkiaFontRegistrar.Clear();
        using (var latin = Open("OpenSans-Regular.ttf"))
            SkiaFontRegistrar.RegisterTypefaceFromStream(Latin, latin);
        var fallbacks = new List<string>();
        if (withEmojiFallback)
        {
            using var emoji = Open("KumikoTestEmoji.ttf");
            SkiaFontRegistrar.RegisterTypefaceFromStream(Emoji, emoji);
            fallbacks.Add(Emoji);
        }
        if (withCjkFallback)
        {
            using var cjk = Open("NotoSansJP-Regular.ttf");
            SkiaFontRegistrar.RegisterTypefaceFromStream(Cjk, cjk);
            fallbacks.Add(Cjk);
        }
        if (fallbacks.Count > 0)
            SkiaFontRegistrar.SetFallbackFamilies(fallbacks.ToArray());
    }
}

/// <summary>
/// Every test touches the process-wide <see cref="SkiaFontRegistrar"/>, so they share one collection and run one at
/// a time.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SkiaRegistrarCollection
{
    /// <summary>The collection name.</summary>
    public const string Name = "SkiaRegistrar";
}

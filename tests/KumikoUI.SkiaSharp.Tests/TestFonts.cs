using KumikoUI.SkiaSharp;

namespace KumikoUI.SkiaSharp.Tests;

/// <summary>The embedded fonts and a registrar that starts every test from the same state.</summary>
internal static class TestFonts
{
    /// <summary>The family the Latin test font (Open Sans Regular) is registered under.</summary>
    public const string Latin = "KumikoTestLatin";

    /// <summary>The family the CJK test font (Noto Sans JP) is registered under.</summary>
    public const string Cjk = "KumikoTestCjk";

    /// <summary>A fresh stream over an embedded font.</summary>
    public static Stream Open(string fileName) =>
        typeof(TestFonts).Assembly.GetManifestResourceStream("KumikoUI.SkiaSharp.Tests." + fileName)
            ?? throw new InvalidOperationException($"The embedded test font {fileName} is missing.");

    /// <summary>Clears the process-wide registrar, then registers the Latin font and optionally the CJK fallback.</summary>
    public static void Reset(bool withCjkFallback = false)
    {
        SkiaFontRegistrar.Clear();
        using (var latin = Open("OpenSans-Regular.ttf"))
            SkiaFontRegistrar.RegisterTypefaceFromStream(Latin, latin);
        if (!withCjkFallback) return;
        using (var cjk = Open("NotoSansJP-Regular.ttf"))
            SkiaFontRegistrar.RegisterTypefaceFromStream(Cjk, cjk);
        SkiaFontRegistrar.SetFallbackFamilies(Cjk);
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

using KumikoUI.Core.Rendering;
using KumikoUI.SkiaSharp;
using SkiaSharp;

namespace KumikoUI.SkiaSharp.Tests;

/// <summary>Replacing, removing and clearing registered fonts while drawing contexts keep working.</summary>
[Collection(SkiaRegistrarCollection.Name)]
public class RegistrarLifecycleTests : IDisposable
{
    public RegistrarLifecycleTests() => TestFonts.Reset();

    public void Dispose() => SkiaFontRegistrar.Clear();

    private static float Measure(string family, string text)
    {
        using var bitmap = new SKBitmap(1, 1);
        using var canvas = new SKCanvas(bitmap);
        using var ctx = new SkiaDrawingContext(canvas);
        return ctx.MeasureText(text, new KumikoPaint { Font = new KumikoFont(family, 20) }).Width;
    }

    [Fact]
    public void Registering_a_family_again_replaces_it()
    {
        float before = Measure(TestFonts.Latin, "Hello world");

        using (var cjk = TestFonts.Open("NotoSansJP-Regular.ttf"))
            SkiaFontRegistrar.RegisterTypefaceFromStream(TestFonts.Latin, cjk);

        Assert.True(SkiaFontRegistrar.TryGetTypeface(TestFonts.Latin, out var replaced));
        Assert.NotNull(replaced);
        Assert.NotEqual(before, Measure(TestFonts.Latin, "Hello world"), 1);
    }

    [Fact]
    public void Every_change_increments_the_version()
    {
        long start = SkiaFontRegistrar.Version;
        using (var s = TestFonts.Open("OpenSans-Regular.ttf"))
            SkiaFontRegistrar.RegisterTypefaceFromStream("Other", s);
        long afterRegister = SkiaFontRegistrar.Version;
        SkiaFontRegistrar.Unregister("Other");
        long afterUnregister = SkiaFontRegistrar.Version;
        SkiaFontRegistrar.Clear();

        Assert.True(afterRegister > start);
        Assert.True(afterUnregister > afterRegister);
        Assert.True(SkiaFontRegistrar.Version > afterUnregister);
    }

    [Fact]
    public void Unregister_removes_one_family_and_text_still_draws_with_the_default()
    {
        Assert.True(SkiaFontRegistrar.Unregister(TestFonts.Latin));
        Assert.False(SkiaFontRegistrar.Unregister(TestFonts.Latin));

        Assert.False(SkiaFontRegistrar.TryGetTypeface(TestFonts.Latin, out _));
        Assert.True(Measure(TestFonts.Latin, "Hello") > 0);
    }

    [Fact]
    public void Clear_removes_every_family_and_fallback_and_is_idempotent()
    {
        TestFonts.Reset(withCjkFallback: true);
        Assert.NotEmpty(SkiaFontRegistrar.FallbackFamilies);

        SkiaFontRegistrar.Clear();
        SkiaFontRegistrar.Clear();

        Assert.False(SkiaFontRegistrar.TryGetTypeface(TestFonts.Latin, out _));
        Assert.False(SkiaFontRegistrar.TryGetTypeface(TestFonts.Cjk, out _));
        Assert.Empty(SkiaFontRegistrar.FallbackFamilies);
    }

    [Fact]
    public void A_context_created_before_Clear_keeps_working_after_it()
    {
        using var bitmap = new SKBitmap(100, 40);
        using var canvas = new SKCanvas(bitmap);
        var paint = new KumikoPaint { Font = new KumikoFont(TestFonts.Latin, 16) };
        using var ctx = new SkiaDrawingContext(canvas);
        ctx.DrawText("Hello", 4, 20, paint);

        SkiaFontRegistrar.Clear();

        ctx.DrawText("Hello", 4, 20, paint);
        Assert.True(ctx.MeasureText("Hello", paint).Width > 0);
    }
}

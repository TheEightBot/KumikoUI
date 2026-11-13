using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;
using KumikoUI.SkiaSharp;
using SkiaSharp;

namespace KumikoUI.Core.Tests.Rendering;

/// <summary>
/// <see cref="KumikoFontIconProvider"/> (#119) draws the mapped glyph of the icon font, in the icon's color, sized to
/// and centered in its slot, and hands unmapped icons to its fallback.
/// </summary>
[Collection("SkiaFontSeam")]
public sealed class KumikoFontIconProviderTests : IDisposable
{
    private const string Family = "KumikoTestMaterialIcons";
    private static readonly KumikoColor Ink = new(200, 30, 30);
    private static readonly Lazy<byte[]> s_materialIcons = new(() =>
    {
        using var resource = typeof(KumikoFontIconProviderTests).Assembly
            .GetManifestResourceStream("KumikoUI.Core.Tests.MaterialIcons-Regular.ttf")!;
        using var copy = new MemoryStream();
        resource.CopyTo(copy);
        return copy.ToArray();
    });

    public KumikoFontIconProviderTests() =>
        SkiaFontRegistrar.RegisterTypefaceFromStream(Family, new MemoryStream(s_materialIcons.Value, writable: false));

    public void Dispose() => SkiaFontRegistrar.Unregister(Family);

    private static KumikoPaint Paint() => new() { Color = Ink, StrokeWidth = 1.5f, IsAntiAlias = true };

    // ── Drawing (recorded) ────────────────────────────────────────

    [Fact]
    public void AMappedIcon_IsItsGlyph_InTheIconFont_SizedAndCenteredInTheSlot()
    {
        var provider = new KumikoFontIconProvider("Icons") { Scale = 1.5f }.SetGlyph(KumikoIcon.Check, 0xE5CA);
        var ctx = new RecordingDrawingContext();

        provider.DrawIcon(ctx, KumikoIcon.Check, new KumikoRect(10, 20, 30, 16), Paint());

        var glyph = Assert.Single(ctx.Ops);
        Assert.Equal(DrawOpKind.Text, glyph.Kind);
        Assert.Equal("", glyph.Text);
        Assert.Equal(Ink, glyph.Color);
        Assert.Equal("Icons", glyph.Font!.Family);
        Assert.Equal(16 * 1.5f, glyph.Font.Size); // the shorter side times the scale
        // Centered on its measured width, and on the font's ascent (-11) and descent (3): baseline at 28 + 4
        Assert.Equal(25, glyph.Rect.X + glyph.Rect.Width / 2, 3);
        Assert.Equal(32, glyph.Rect.Bottom, 3);
    }

    [Fact]
    public void AnUnmappedIcon_IsDrawnByTheFallback_OrNotAtAllWithoutOne()
    {
        var provider = new KumikoFontIconProvider("Icons").SetGlyph(KumikoIcon.Check, 0xE5CA);
        var fallback = new RecordingIconProvider();
        provider.Fallback = fallback;

        provider.DrawIcon(new RecordingDrawingContext(), KumikoIcon.SortAscending, new KumikoRect(0, 0, 12, 12), Paint());

        var call = Assert.Single(fallback.Calls);
        Assert.Equal(KumikoIcon.SortAscending, call.Icon);
        Assert.Equal(new KumikoRect(0, 0, 12, 12), call.Bounds);
        Assert.Equal(Ink, call.Color);

        provider.Fallback = null;
        var ctx = new RecordingDrawingContext();
        provider.DrawIcon(ctx, KumikoIcon.SortAscending, new KumikoRect(0, 0, 12, 12), Paint());
        Assert.Empty(ctx.Ops);
    }

    [Fact]
    public void TheDefaultFallback_IsTheBuiltInSet()
    {
        var provider = new KumikoFontIconProvider("Icons");
        var ctx = new RecordingDrawingContext();

        provider.DrawIcon(ctx, KumikoIcon.SortAscending, new KumikoRect(0, 0, 12, 12), Paint());

        Assert.Same(KumikoIconProvider.Default, provider.Fallback);
        Assert.Single(ctx.Polygons);
    }

    [Fact]
    public void Glyphs_CanBeSetReadRemovedAndReplaced_IncludingOutsideTheBasicPlane()
    {
        var provider = new KumikoFontIconProvider("Icons");

        Assert.False(provider.TryGetGlyph(KumikoIcon.Filter, out int none));
        Assert.Equal(0, none);
        Assert.Same(provider, provider.SetGlyph(KumikoIcon.Filter, 0xE152));
        Assert.True(provider.TryGetGlyph(KumikoIcon.Filter, out int filter));
        Assert.Equal(0xE152, filter);

        provider.SetGlyph(KumikoIcon.Filter, 0x1F50D); // a supplementary-plane code point: two UTF-16 units
        Assert.True(provider.TryGetGlyph(KumikoIcon.Filter, out int magnifier));
        Assert.Equal(0x1F50D, magnifier);
        var ctx = new RecordingDrawingContext();
        provider.DrawIcon(ctx, KumikoIcon.Filter, new KumikoRect(0, 0, 12, 12), Paint());
        Assert.Equal(char.ConvertFromUtf32(0x1F50D), Assert.Single(ctx.Texts).Text);

        Assert.True(provider.RemoveGlyph(KumikoIcon.Filter));
        Assert.False(provider.RemoveGlyph(KumikoIcon.Filter));
        Assert.False(provider.TryGetGlyph(KumikoIcon.Filter, out _));
    }

    [Fact]
    public void InvalidArguments_AreRejected()
    {
        var provider = new KumikoFontIconProvider("Icons");

        Assert.Throws<ArgumentException>(() => new KumikoFontIconProvider(" "));
        Assert.ThrowsAny<ArgumentException>(() => new KumikoFontIconProvider(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => provider.SetGlyph(KumikoIcon.Check, 0xD800)); // a surrogate
        Assert.Throws<ArgumentOutOfRangeException>(() => provider.SetGlyph(KumikoIcon.Check, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => provider.SetGlyph((KumikoIcon)999, 0xE5CA));
        Assert.Throws<ArgumentOutOfRangeException>(() => provider.Scale = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => provider.Scale = float.NaN);
        Assert.False(provider.RemoveGlyph((KumikoIcon)999));
        Assert.False(provider.TryGetGlyph((KumikoIcon)999, out _));
    }

    // ── Material Icons ────────────────────────────────────────────

    [Fact]
    public void MaterialIcons_MapsEveryIcon_ToAGlyphTheFontHas()
    {
        var provider = KumikoFontIconProvider.CreateMaterialIcons(Family);
        Assert.True(SkiaFontRegistrar.TryGetTypeface(Family, out var typeface));
        using var font = new SKFont(typeface);

        Assert.Equal(Family, provider.FontFamily);
        foreach (var icon in Enum.GetValues<KumikoIcon>())
        {
            Assert.True(provider.TryGetGlyph(icon, out int codePoint), $"{icon} has no code point");
            Assert.NotEqual((ushort)0, font.GetGlyph(codePoint));
        }
    }

    [Theory]
    [InlineData(KumikoIcon.SortAscending, 0xE5D8)]   // arrow_upward
    [InlineData(KumikoIcon.GroupExpanded, 0xE5CF)]   // expand_more
    [InlineData(KumikoIcon.DragHandle, 0xE945)]      // drag_indicator
    [InlineData(KumikoIcon.Check, 0xE5CA)]           // check
    public void MaterialIcons_UsesTheOfficialCodePoints(KumikoIcon icon, int expected)
    {
        Assert.True(KumikoFontIconProvider.CreateMaterialIcons().TryGetGlyph(icon, out int codePoint));
        Assert.Equal(expected, codePoint);
    }

    // ── Drawing (rasterized) ──────────────────────────────────────

    [Fact]
    public void ThroughSkia_TheGlyphIsInkedInsideItsSlot_InTheIconsColor()
    {
        var provider = KumikoFontIconProvider.CreateMaterialIcons(Family);
        provider.Scale = 1f;
        var slot = new KumikoRect(8, 8, 24, 24);

        using var bitmap = SkiaTestFonts.Render(40, 40, ctx => provider.DrawIcon(ctx, KumikoIcon.Check, slot, Paint()));

        long inside = 0, outside = 0;
        for (int y = 0; y < 40; y++)
        {
            for (int x = 0; x < 40; x++)
            {
                var px = bitmap.GetPixel(x, y);
                bool inSlot = x >= slot.X && x < slot.Right && y >= slot.Y && y < slot.Bottom;
                if (inSlot) inside += px.Alpha; else outside += px.Alpha;
                if (px.Alpha == 255) Assert.Equal(Ink.R, px.Red);
            }
        }
        Assert.True(inside > 255 * 40, $"the check glyph left too little ink: {inside}");
        Assert.Equal(0, outside);
    }

    [Fact]
    public void ThroughSkia_DifferentIcons_DrawDifferentGlyphs()
    {
        var provider = KumikoFontIconProvider.CreateMaterialIcons(Family);
        var slot = new KumikoRect(4, 4, 24, 24);

        using var check = SkiaTestFonts.Render(32, 32, ctx => provider.DrawIcon(ctx, KumikoIcon.Check, slot, Paint()));
        using var close = SkiaTestFonts.Render(32, 32, ctx => provider.DrawIcon(ctx, KumikoIcon.Remove, slot, Paint()));

        int differing = 0;
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
                if (Math.Abs(check.GetPixel(x, y).Alpha - close.GetPixel(x, y).Alpha) > 128) differing++;
        Assert.True(differing > 20, $"check and close look alike ({differing} pixels differ)");
    }
}

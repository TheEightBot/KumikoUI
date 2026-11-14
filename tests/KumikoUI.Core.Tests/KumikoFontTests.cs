using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Tests;

/// <summary>A font is a value: equal by content, safe to share, changed by building a new one.</summary>
public class KumikoFontTests
{
    [Fact]
    public void DefaultConstructor_IsTheDefaultFamilyAt14Pixels()
    {
        var font = new KumikoFont();

        Assert.Equal("Default", font.Family);
        Assert.Equal(14f, font.Size);
        Assert.False(font.IsBold);
        Assert.False(font.IsItalic);
    }

    [Fact]
    public void FontsWithTheSameContent_AreEqualAndHashAlike()
    {
        var a = new KumikoFont("Mono", 12f, bold: true);
        var b = new KumikoFont("Mono", 12f, bold: true);

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, new KumikoFont("Mono", 12f));
        Assert.NotEqual(a, new KumikoFont("Mono", 13f, bold: true));
        Assert.NotEqual(a, new KumikoFont("Mono", 12f, bold: true, italic: true));
        Assert.NotEqual(a, new KumikoFont("Other", 12f, bold: true));
    }

    [Fact]
    public void With_BuildsAChangedCopyAndLeavesTheOriginal()
    {
        var original = new KumikoFont("Mono", 12f);

        var bigger = original with { Size = 20f };

        Assert.Equal(12f, original.Size);
        Assert.Equal(20f, bigger.Size);
        Assert.Equal("Mono", bigger.Family);
    }

    [Fact]
    public void ANullFamily_IsTheDefaultFamily()
    {
        Assert.Equal("Default", new KumikoFont(null!, 10f).Family);
        Assert.Equal("Default", default(KumikoFont).Family);
    }

    [Fact]
    public void AStyleSeesAReplacedFontAsAChange_AndAnEqualOneAsNone()
    {
        var style = new KumikoStyle();
        int version = style.Version;

        style.CellFont = style.CellFont with { };
        Assert.Equal(version, style.Version);

        style.CellFont = style.CellFont with { Size = 30f };
        Assert.Equal(version + 1, style.Version);
    }
}

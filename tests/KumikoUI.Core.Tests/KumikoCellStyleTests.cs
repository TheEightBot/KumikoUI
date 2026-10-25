using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Tests;

public class KumikoCellStyleTests
{
    // ── HasOverrides ──────────────────────────────────────────────

    [Fact]
    public void HasOverrides_EmptyStyle_ReturnsFalse()
    {
        var style = new KumikoCellStyle();
        Assert.False(style.HasOverrides);
    }

    [Fact]
    public void HasOverrides_WithBackgroundColor_ReturnsTrue()
    {
        var style = new KumikoCellStyle { BackgroundColor = KumikoColor.Red };
        Assert.True(style.HasOverrides);
    }

    [Fact]
    public void HasOverrides_WithTextColor_ReturnsTrue()
    {
        var style = new KumikoCellStyle { TextColor = KumikoColor.Blue };
        Assert.True(style.HasOverrides);
    }

    [Fact]
    public void HasOverrides_WithFont_ReturnsTrue()
    {
        var style = new KumikoCellStyle { Font = new KumikoFont("Arial", 12f) };
        Assert.True(style.HasOverrides);
    }

    [Fact]
    public void HasOverrides_WithPadding_ReturnsTrue()
    {
        var style = new KumikoCellStyle { Padding = 10f };
        Assert.True(style.HasOverrides);
    }

    [Fact]
    public void HasOverrides_WithBorderColor_ReturnsTrue()
    {
        var style = new KumikoCellStyle { BorderColor = KumikoColor.Black };
        Assert.True(style.HasOverrides);
    }

    [Fact]
    public void HasOverrides_WithBorderWidth_ReturnsTrue()
    {
        var style = new KumikoCellStyle { BorderWidth = 2f };
        Assert.True(style.HasOverrides);
    }

    [Fact]
    public void HasOverrides_WithTextAlignment_ReturnsTrue()
    {
        var style = new KumikoCellStyle { TextAlignment = KumikoTextAlignment.Center };
        Assert.True(style.HasOverrides);
    }

    // ── Merge ─────────────────────────────────────────────────────

    [Fact]
    public void Merge_BothNull_ReturnsNull()
    {
        Assert.Null(KumikoCellStyle.Merge(null, null));
    }

    [Fact]
    public void Merge_PrimaryNull_ReturnsFallback()
    {
        var fallback = new KumikoCellStyle { TextColor = KumikoColor.Blue };
        var result = KumikoCellStyle.Merge(null, fallback);
        Assert.Same(fallback, result);
    }

    [Fact]
    public void Merge_FallbackNull_ReturnsPrimary()
    {
        var primary = new KumikoCellStyle { TextColor = KumikoColor.Red };
        var result = KumikoCellStyle.Merge(primary, null);
        Assert.Same(primary, result);
    }

    [Fact]
    public void Merge_PrimaryTakesPrecedence()
    {
        var primary = new KumikoCellStyle { TextColor = KumikoColor.Red };
        var fallback = new KumikoCellStyle { TextColor = KumikoColor.Blue };
        var result = KumikoCellStyle.Merge(primary, fallback);
        Assert.Equal(KumikoColor.Red, result!.TextColor);
    }

    [Fact]
    public void Merge_PrimaryNullProperty_FallsBackToFallback()
    {
        var primary = new KumikoCellStyle { TextColor = KumikoColor.Red };
        var fallback = new KumikoCellStyle { BackgroundColor = KumikoColor.Green };
        var result = KumikoCellStyle.Merge(primary, fallback);
        Assert.Equal(KumikoColor.Red, result!.TextColor);
        Assert.Equal(KumikoColor.Green, result.BackgroundColor);
    }

    [Fact]
    public void Merge_AllPropertiesSet_PrimaryWinsOnAll()
    {
        var primary = new KumikoCellStyle
        {
            BackgroundColor = KumikoColor.Red,
            TextColor = KumikoColor.Blue,
            Padding = 5f,
            BorderWidth = 2f,
            TextAlignment = KumikoTextAlignment.Right
        };
        var fallback = new KumikoCellStyle
        {
            BackgroundColor = KumikoColor.Green,
            TextColor = KumikoColor.Black,
            Padding = 8f,
            BorderWidth = 1f,
            TextAlignment = KumikoTextAlignment.Left
        };
        var result = KumikoCellStyle.Merge(primary, fallback)!;
        Assert.Equal(KumikoColor.Red, result.BackgroundColor);
        Assert.Equal(KumikoColor.Blue, result.TextColor);
        Assert.Equal(5f, result.Padding);
        Assert.Equal(2f, result.BorderWidth);
        Assert.Equal(KumikoTextAlignment.Right, result.TextAlignment);
    }

    [Fact]
    public void Merge_Font_PrimaryFontWins()
    {
        var primaryFont = new KumikoFont("Arial", 14f);
        var fallbackFont = new KumikoFont("Helvetica", 12f);
        var primary = new KumikoCellStyle { Font = primaryFont };
        var fallback = new KumikoCellStyle { Font = fallbackFont };
        var result = KumikoCellStyle.Merge(primary, fallback)!;
        Assert.Same(primaryFont, result.Font);
    }

    [Fact]
    public void Merge_FontNullInPrimary_UsesFallbackFont()
    {
        var fallbackFont = new KumikoFont("Helvetica", 12f);
        var primary = new KumikoCellStyle { TextColor = KumikoColor.Red };
        var fallback = new KumikoCellStyle { Font = fallbackFont };
        var result = KumikoCellStyle.Merge(primary, fallback)!;
        Assert.Same(fallbackFont, result.Font);
    }
}

public class KumikoRowStyleTests
{
    [Fact]
    public void HasOverrides_EmptyStyle_ReturnsFalse()
    {
        var style = new KumikoRowStyle();
        Assert.False(style.HasOverrides);
    }

    [Fact]
    public void HasOverrides_WithBackgroundColor_ReturnsTrue()
    {
        var style = new KumikoRowStyle { BackgroundColor = KumikoColor.Red };
        Assert.True(style.HasOverrides);
    }

    [Fact]
    public void HasOverrides_WithHeight_ReturnsTrue()
    {
        var style = new KumikoRowStyle { Height = 48f };
        Assert.True(style.HasOverrides);
    }

    [Fact]
    public void HasOverrides_WithFont_ReturnsTrue()
    {
        var style = new KumikoRowStyle { Font = new KumikoFont("Arial", 14f) };
        Assert.True(style.HasOverrides);
    }

    [Fact]
    public void HasOverrides_WithTextColor_ReturnsTrue()
    {
        var style = new KumikoRowStyle { TextColor = KumikoColor.Green };
        Assert.True(style.HasOverrides);
    }
}


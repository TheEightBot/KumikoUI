using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Tests;

public class CellStyleTests
{
    // ── HasOverrides ──────────────────────────────────────────────

    [Fact]
    public void HasOverrides_EmptyStyle_ReturnsFalse()
    {
        var style = new CellStyle();
        Assert.False(style.HasOverrides);
    }

    [Fact]
    public void HasOverrides_WithBackgroundColor_ReturnsTrue()
    {
        var style = new CellStyle { BackgroundColor = GridColor.Red };
        Assert.True(style.HasOverrides);
    }

    [Fact]
    public void HasOverrides_WithTextColor_ReturnsTrue()
    {
        var style = new CellStyle { TextColor = GridColor.Blue };
        Assert.True(style.HasOverrides);
    }

    [Fact]
    public void HasOverrides_WithFont_ReturnsTrue()
    {
        var style = new CellStyle { Font = new GridFont("Arial", 12f) };
        Assert.True(style.HasOverrides);
    }

    [Fact]
    public void HasOverrides_WithPadding_ReturnsTrue()
    {
        var style = new CellStyle { Padding = 10f };
        Assert.True(style.HasOverrides);
    }

    [Fact]
    public void HasOverrides_WithBorderColor_ReturnsTrue()
    {
        var style = new CellStyle { BorderColor = GridColor.Black };
        Assert.True(style.HasOverrides);
    }

    [Fact]
    public void HasOverrides_WithBorderWidth_ReturnsTrue()
    {
        var style = new CellStyle { BorderWidth = 2f };
        Assert.True(style.HasOverrides);
    }

    [Fact]
    public void HasOverrides_WithTextAlignment_ReturnsTrue()
    {
        var style = new CellStyle { TextAlignment = GridTextAlignment.Center };
        Assert.True(style.HasOverrides);
    }

    // ── Merge ─────────────────────────────────────────────────────

    [Fact]
    public void Merge_BothNull_ReturnsNull()
    {
        Assert.Null(CellStyle.Merge(null, null));
    }

    [Fact]
    public void Merge_PrimaryNull_ReturnsFallback()
    {
        var fallback = new CellStyle { TextColor = GridColor.Blue };
        var result = CellStyle.Merge(null, fallback);
        Assert.Same(fallback, result);
    }

    [Fact]
    public void Merge_FallbackNull_ReturnsPrimary()
    {
        var primary = new CellStyle { TextColor = GridColor.Red };
        var result = CellStyle.Merge(primary, null);
        Assert.Same(primary, result);
    }

    [Fact]
    public void Merge_PrimaryTakesPrecedence()
    {
        var primary = new CellStyle { TextColor = GridColor.Red };
        var fallback = new CellStyle { TextColor = GridColor.Blue };
        var result = CellStyle.Merge(primary, fallback);
        Assert.Equal(GridColor.Red, result!.TextColor);
    }

    [Fact]
    public void Merge_PrimaryNullProperty_FallsBackToFallback()
    {
        var primary = new CellStyle { TextColor = GridColor.Red };
        var fallback = new CellStyle { BackgroundColor = GridColor.Green };
        var result = CellStyle.Merge(primary, fallback);
        Assert.Equal(GridColor.Red, result!.TextColor);
        Assert.Equal(GridColor.Green, result.BackgroundColor);
    }

    [Fact]
    public void Merge_AllPropertiesSet_PrimaryWinsOnAll()
    {
        var primary = new CellStyle
        {
            BackgroundColor = GridColor.Red,
            TextColor = GridColor.Blue,
            Padding = 5f,
            BorderWidth = 2f,
            TextAlignment = GridTextAlignment.Right
        };
        var fallback = new CellStyle
        {
            BackgroundColor = GridColor.Green,
            TextColor = GridColor.Black,
            Padding = 8f,
            BorderWidth = 1f,
            TextAlignment = GridTextAlignment.Left
        };
        var result = CellStyle.Merge(primary, fallback)!;
        Assert.Equal(GridColor.Red, result.BackgroundColor);
        Assert.Equal(GridColor.Blue, result.TextColor);
        Assert.Equal(5f, result.Padding);
        Assert.Equal(2f, result.BorderWidth);
        Assert.Equal(GridTextAlignment.Right, result.TextAlignment);
    }

    [Fact]
    public void Merge_Font_PrimaryFontWins()
    {
        var primaryFont = new GridFont("Arial", 14f);
        var fallbackFont = new GridFont("Helvetica", 12f);
        var primary = new CellStyle { Font = primaryFont };
        var fallback = new CellStyle { Font = fallbackFont };
        var result = CellStyle.Merge(primary, fallback)!;
        Assert.Same(primaryFont, result.Font);
    }

    [Fact]
    public void Merge_FontNullInPrimary_UsesFallbackFont()
    {
        var fallbackFont = new GridFont("Helvetica", 12f);
        var primary = new CellStyle { TextColor = GridColor.Red };
        var fallback = new CellStyle { Font = fallbackFont };
        var result = CellStyle.Merge(primary, fallback)!;
        Assert.Same(fallbackFont, result.Font);
    }
}

public class RowStyleTests
{
    [Fact]
    public void HasOverrides_EmptyStyle_ReturnsFalse()
    {
        var style = new RowStyle();
        Assert.False(style.HasOverrides);
    }

    [Fact]
    public void HasOverrides_WithBackgroundColor_ReturnsTrue()
    {
        var style = new RowStyle { BackgroundColor = GridColor.Red };
        Assert.True(style.HasOverrides);
    }

    [Fact]
    public void HasOverrides_WithHeight_ReturnsTrue()
    {
        var style = new RowStyle { Height = 48f };
        Assert.True(style.HasOverrides);
    }

    [Fact]
    public void HasOverrides_WithFont_ReturnsTrue()
    {
        var style = new RowStyle { Font = new GridFont("Arial", 14f) };
        Assert.True(style.HasOverrides);
    }

    [Fact]
    public void HasOverrides_WithTextColor_ReturnsTrue()
    {
        var style = new RowStyle { TextColor = GridColor.Green };
        Assert.True(style.HasOverrides);
    }
}


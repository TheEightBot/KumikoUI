using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Tests;

public class KumikoThemeTests
{
    // ── Create factory ────────────────────────────────────────────

    [Fact]
    public void Create_Light_ReturnsNonNullStyle()
    {
        var style = KumikoTheme.Create(KumikoThemeMode.Light);
        Assert.NotNull(style);
    }

    [Fact]
    public void Create_Dark_ReturnsNonNullStyle()
    {
        var style = KumikoTheme.Create(KumikoThemeMode.Dark);
        Assert.NotNull(style);
    }

    [Fact]
    public void Create_HighContrast_ReturnsNonNullStyle()
    {
        var style = KumikoTheme.Create(KumikoThemeMode.HighContrast);
        Assert.NotNull(style);
    }

    [Fact]
    public void Create_UnknownMode_ReturnsLightStyle()
    {
        // Out-of-range enum value falls back to light
        var style = KumikoTheme.Create((KumikoThemeMode)999);
        Assert.NotNull(style);
        Assert.Equal(KumikoTheme.CreateLight().BackgroundColor, style.BackgroundColor);
    }

    // ── Light theme ───────────────────────────────────────────────

    [Fact]
    public void LightTheme_BackgroundColor_IsWhite()
    {
        var style = KumikoTheme.CreateLight();
        Assert.Equal(KumikoColor.White, style.BackgroundColor);
    }

    [Fact]
    public void LightTheme_CellTextColor_IsDark()
    {
        var style = KumikoTheme.CreateLight();
        // Should be a dark color (low R, G, B)
        Assert.True(style.CellTextColor.R < 128 && style.CellTextColor.G < 128 && style.CellTextColor.B < 128);
    }

    [Fact]
    public void LightTheme_HeaderHeight_IsPositive()
    {
        var style = KumikoTheme.CreateLight();
        Assert.True(style.HeaderHeight > 0);
    }

    [Fact]
    public void LightTheme_RowHeight_IsPositive()
    {
        var style = KumikoTheme.CreateLight();
        Assert.True(style.RowHeight > 0);
    }

    // ── Dark theme ────────────────────────────────────────────────

    [Fact]
    public void DarkTheme_BackgroundColor_IsDark()
    {
        var style = KumikoTheme.CreateDark();
        // Dark background should have low luminance
        Assert.True(style.BackgroundColor.R < 100 && style.BackgroundColor.G < 100 && style.BackgroundColor.B < 100);
    }

    [Fact]
    public void DarkTheme_CellTextColor_IsLight()
    {
        var style = KumikoTheme.CreateDark();
        // Text should be bright enough to read
        Assert.True(style.CellTextColor.R > 100 || style.CellTextColor.G > 100 || style.CellTextColor.B > 100);
    }

    [Fact]
    public void DarkTheme_DiffersFromLightTheme()
    {
        var light = KumikoTheme.CreateLight();
        var dark = KumikoTheme.CreateDark();
        Assert.NotEqual(light.BackgroundColor, dark.BackgroundColor);
    }

    // ── High contrast theme ───────────────────────────────────────

    [Fact]
    public void HighContrastTheme_BackgroundColor_IsBlack()
    {
        var style = KumikoTheme.CreateHighContrast();
        Assert.Equal(KumikoColor.Black, style.BackgroundColor);
    }

    [Fact]
    public void HighContrastTheme_CellTextColor_IsWhite()
    {
        var style = KumikoTheme.CreateHighContrast();
        Assert.Equal(KumikoColor.White, style.CellTextColor);
    }

    [Fact]
    public void HighContrastTheme_CurrentCellBorderWidth_IsThicker()
    {
        var light = KumikoTheme.CreateLight();
        var hc = KumikoTheme.CreateHighContrast();
        Assert.True(hc.CurrentCellBorderWidth >= light.CurrentCellBorderWidth);
    }

    [Fact]
    public void HighContrastTheme_SortIndicatorColor_IsHighlyVisible()
    {
        var style = KumikoTheme.CreateHighContrast();
        // Should be a bright color (one component maxed)
        bool isBright = style.SortIndicatorColor.R == 255 ||
                        style.SortIndicatorColor.G == 255 ||
                        style.SortIndicatorColor.B == 255;
        Assert.True(isBright);
    }

    // ── KumikoStyle defaults ────────────────────────────────────

    [Fact]
    public void DataGridStyle_Defaults_GridLinesVisible()
    {
        var style = new KumikoStyle();
        Assert.True(style.ShowHorizontalGridLines);
        Assert.True(style.ShowVerticalGridLines);
    }

    [Fact]
    public void DataGridStyle_Defaults_AlternateRowEnabled()
    {
        var style = new KumikoStyle();
        Assert.True(style.AlternateRowBackground);
    }

    [Fact]
    public void DataGridStyle_Defaults_HeaderFontIsBold()
    {
        var style = new KumikoStyle();
        Assert.True(style.HeaderFont.IsBold);
    }

    [Fact]
    public void DataGridStyle_Defaults_RowDragDropDisabled()
    {
        var style = new KumikoStyle();
        Assert.False(style.AllowRowDragDrop);
        Assert.False(style.ShowRowDragHandle);
    }

    [Fact]
    public void DataGridStyle_Defaults_AccentColorIsBlue()
    {
        var style = new KumikoStyle();
        // Accent is some form of blue (B is highest component)
        Assert.True(style.AccentColor.B > style.AccentColor.R);
    }
}


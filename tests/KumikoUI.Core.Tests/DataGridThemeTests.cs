using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Tests;

public class DataGridThemeTests
{
    // ── Create factory ────────────────────────────────────────────

    [Fact]
    public void Create_Light_ReturnsNonNullStyle()
    {
        var style = DataGridTheme.Create(DataGridThemeMode.Light);
        Assert.NotNull(style);
    }

    [Fact]
    public void Create_Dark_ReturnsNonNullStyle()
    {
        var style = DataGridTheme.Create(DataGridThemeMode.Dark);
        Assert.NotNull(style);
    }

    [Fact]
    public void Create_HighContrast_ReturnsNonNullStyle()
    {
        var style = DataGridTheme.Create(DataGridThemeMode.HighContrast);
        Assert.NotNull(style);
    }

    [Fact]
    public void Create_UnknownMode_ReturnsLightStyle()
    {
        // Out-of-range enum value falls back to light
        var style = DataGridTheme.Create((DataGridThemeMode)999);
        Assert.NotNull(style);
        Assert.Equal(DataGridTheme.CreateLight().BackgroundColor, style.BackgroundColor);
    }

    // ── Light theme ───────────────────────────────────────────────

    [Fact]
    public void LightTheme_BackgroundColor_IsWhite()
    {
        var style = DataGridTheme.CreateLight();
        Assert.Equal(GridColor.White, style.BackgroundColor);
    }

    [Fact]
    public void LightTheme_CellTextColor_IsDark()
    {
        var style = DataGridTheme.CreateLight();
        // Should be a dark color (low R, G, B)
        Assert.True(style.CellTextColor.R < 128 && style.CellTextColor.G < 128 && style.CellTextColor.B < 128);
    }

    [Fact]
    public void LightTheme_HeaderHeight_IsPositive()
    {
        var style = DataGridTheme.CreateLight();
        Assert.True(style.HeaderHeight > 0);
    }

    [Fact]
    public void LightTheme_RowHeight_IsPositive()
    {
        var style = DataGridTheme.CreateLight();
        Assert.True(style.RowHeight > 0);
    }

    // ── Dark theme ────────────────────────────────────────────────

    [Fact]
    public void DarkTheme_BackgroundColor_IsDark()
    {
        var style = DataGridTheme.CreateDark();
        // Dark background should have low luminance
        Assert.True(style.BackgroundColor.R < 100 && style.BackgroundColor.G < 100 && style.BackgroundColor.B < 100);
    }

    [Fact]
    public void DarkTheme_CellTextColor_IsLight()
    {
        var style = DataGridTheme.CreateDark();
        // Text should be bright enough to read
        Assert.True(style.CellTextColor.R > 100 || style.CellTextColor.G > 100 || style.CellTextColor.B > 100);
    }

    [Fact]
    public void DarkTheme_DiffersFromLightTheme()
    {
        var light = DataGridTheme.CreateLight();
        var dark = DataGridTheme.CreateDark();
        Assert.NotEqual(light.BackgroundColor, dark.BackgroundColor);
    }

    // ── High contrast theme ───────────────────────────────────────

    [Fact]
    public void HighContrastTheme_BackgroundColor_IsBlack()
    {
        var style = DataGridTheme.CreateHighContrast();
        Assert.Equal(GridColor.Black, style.BackgroundColor);
    }

    [Fact]
    public void HighContrastTheme_CellTextColor_IsWhite()
    {
        var style = DataGridTheme.CreateHighContrast();
        Assert.Equal(GridColor.White, style.CellTextColor);
    }

    [Fact]
    public void HighContrastTheme_CurrentCellBorderWidth_IsThicker()
    {
        var light = DataGridTheme.CreateLight();
        var hc = DataGridTheme.CreateHighContrast();
        Assert.True(hc.CurrentCellBorderWidth >= light.CurrentCellBorderWidth);
    }

    [Fact]
    public void HighContrastTheme_SortIndicatorColor_IsHighlyVisible()
    {
        var style = DataGridTheme.CreateHighContrast();
        // Should be a bright color (one component maxed)
        bool isBright = style.SortIndicatorColor.R == 255 ||
                        style.SortIndicatorColor.G == 255 ||
                        style.SortIndicatorColor.B == 255;
        Assert.True(isBright);
    }

    // ── DataGridStyle defaults ────────────────────────────────────

    [Fact]
    public void DataGridStyle_Defaults_GridLinesVisible()
    {
        var style = new DataGridStyle();
        Assert.True(style.ShowHorizontalGridLines);
        Assert.True(style.ShowVerticalGridLines);
    }

    [Fact]
    public void DataGridStyle_Defaults_AlternateRowEnabled()
    {
        var style = new DataGridStyle();
        Assert.True(style.AlternateRowBackground);
    }

    [Fact]
    public void DataGridStyle_Defaults_HeaderFontIsBold()
    {
        var style = new DataGridStyle();
        Assert.True(style.HeaderFont.IsBold);
    }

    [Fact]
    public void DataGridStyle_Defaults_RowDragDropDisabled()
    {
        var style = new DataGridStyle();
        Assert.False(style.AllowRowDragDrop);
        Assert.False(style.ShowRowDragHandle);
    }

    [Fact]
    public void DataGridStyle_Defaults_AccentColorIsBlue()
    {
        var style = new DataGridStyle();
        // Accent is some form of blue (B is highest component)
        Assert.True(style.AccentColor.B > style.AccentColor.R);
    }
}


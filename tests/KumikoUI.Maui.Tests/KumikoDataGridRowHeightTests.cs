using KumikoUI.Core.Models;

namespace KumikoUI.Maui.Tests;

/// <summary>
/// The MAUI grid's row height properties (#320) reach the style, survive a theme or style change, and the
/// invalidation methods forward to the source.
/// </summary>
public class KumikoDataGridRowHeightTests
{
    private static readonly KumikoRowHeightResolver Pinned = (in KumikoRowHeightContext c) => 56;

    [Fact]
    public void Defaults_MatchTheCoreStyle()
    {
        var grid = new KumikoDataGrid();

        Assert.Equal(KumikoRowHeightMode.Fixed, grid.RowHeightMode);
        Assert.True(double.IsNaN(grid.MinRowHeight));
        Assert.True(double.IsNaN(grid.MaxRowHeight));
        Assert.Equal(grid.GridStyle.CellVerticalPadding, (float)grid.CellVerticalPadding);
        Assert.Null(grid.RowHeightResolver);
        Assert.Equal(KumikoRowHeightMode.Fixed, grid.GridStyle.RowHeightMode);
    }

    [Fact]
    public void Properties_ReachTheStyle()
    {
        var grid = new KumikoDataGrid
        {
            RowHeightMode = KumikoRowHeightMode.Auto,
            MinRowHeight = 30,
            MaxRowHeight = 120,
            CellVerticalPadding = 4,
            RowHeightResolver = Pinned,
        };

        Assert.Equal(KumikoRowHeightMode.Auto, grid.GridStyle.RowHeightMode);
        Assert.Equal(30f, grid.GridStyle.MinRowHeight);
        Assert.Equal(120f, grid.GridStyle.MaxRowHeight);
        Assert.Equal(4f, grid.GridStyle.CellVerticalPadding);
        Assert.Same(Pinned, grid.GridStyle.RowHeightResolver);
    }

    [Fact]
    public void NaNLimitsAndANullResolver_ClearTheStyle()
    {
        var grid = new KumikoDataGrid { MinRowHeight = 30, MaxRowHeight = 90, RowHeightResolver = Pinned };

        grid.MinRowHeight = double.NaN;
        grid.MaxRowHeight = double.NaN;
        grid.RowHeightResolver = null;

        Assert.Null(grid.GridStyle.MinRowHeight);
        Assert.Null(grid.GridStyle.MaxRowHeight);
        Assert.Null(grid.GridStyle.RowHeightResolver);
    }

    [Fact]
    public void Settings_SurviveAThemeChange_AndANewGridStyle()
    {
        var grid = new KumikoDataGrid
        {
            RowHeightMode = KumikoRowHeightMode.Auto,
            MaxRowHeight = 120,
            CellVerticalPadding = 3,
            RowHeightResolver = Pinned,
        };

        grid.Theme = KumikoThemeMode.Dark;
        Assert.Equal(KumikoRowHeightMode.Auto, grid.GridStyle.RowHeightMode);
        Assert.Equal(120f, grid.GridStyle.MaxRowHeight);
        Assert.Equal(3f, grid.GridStyle.CellVerticalPadding);
        Assert.Same(Pinned, grid.GridStyle.RowHeightResolver);

        grid.GridStyle = new KumikoStyle();
        Assert.Equal(KumikoRowHeightMode.Auto, grid.GridStyle.RowHeightMode);
        Assert.Same(Pinned, grid.GridStyle.RowHeightResolver);
    }

    [Fact]
    public void AnUnsetProperty_LeavesTheAssignedStyleAlone()
    {
        var grid = new KumikoDataGrid();
        var style = new KumikoStyle { RowHeightMode = KumikoRowHeightMode.Auto, MinRowHeight = 44 };

        grid.GridStyle = style;

        Assert.Equal(KumikoRowHeightMode.Auto, grid.GridStyle.RowHeightMode);
        Assert.Equal(44f, grid.GridStyle.MinRowHeight);
    }

    [Fact]
    public void InvalidateRowHeight_ForwardsToTheSource()
    {
        var grid = new KumikoDataGrid();

        grid.InvalidateRowHeights();
        Assert.Throws<ArgumentNullException>(() => grid.InvalidateRowHeight(null!));
    }
}

using KumikoUI.Core.Models;

namespace KumikoUI.Maui.Tests;

/// <summary>
/// The scroll bar visibility properties of the MAUI <see cref="KumikoDataGrid"/> (#316) reach the grid's style and
/// survive a replaced style or theme.
/// </summary>
public class KumikoDataGridScrollBarTests
{
    [Fact]
    public void Defaults_AreAuto_AsTheStyle()
    {
        var grid = new KumikoDataGrid();

        Assert.Equal(KumikoScrollBarVisibility.Auto, grid.VerticalScrollBarVisibility);
        Assert.Equal(KumikoScrollBarVisibility.Auto, grid.HorizontalScrollBarVisibility);
        Assert.Equal(KumikoScrollBarVisibility.Auto, grid.GridStyle.VerticalScrollBarVisibility);
        Assert.Equal(KumikoScrollBarVisibility.Auto, grid.GridStyle.HorizontalScrollBarVisibility);
    }

    [Fact]
    public void Set_ReachesTheStyle()
    {
        var grid = new KumikoDataGrid
        {
            VerticalScrollBarVisibility = KumikoScrollBarVisibility.Always,
            HorizontalScrollBarVisibility = KumikoScrollBarVisibility.Never,
        };

        Assert.Equal(KumikoScrollBarVisibility.Always, grid.GridStyle.VerticalScrollBarVisibility);
        Assert.Equal(KumikoScrollBarVisibility.Never, grid.GridStyle.HorizontalScrollBarVisibility);
    }

    [Fact]
    public void Set_SurvivesATheme_AndAReplacedStyle()
    {
        var grid = new KumikoDataGrid { VerticalScrollBarVisibility = KumikoScrollBarVisibility.Never };

        grid.Theme = KumikoThemeMode.Dark;
        Assert.Equal(KumikoScrollBarVisibility.Never, grid.GridStyle.VerticalScrollBarVisibility);

        grid.GridStyle = new KumikoStyle { VerticalScrollBarVisibility = KumikoScrollBarVisibility.Always };
        Assert.Equal(KumikoScrollBarVisibility.Never, grid.GridStyle.VerticalScrollBarVisibility);
        Assert.Equal(KumikoScrollBarVisibility.Auto, grid.GridStyle.HorizontalScrollBarVisibility); // never set: the style's
    }
}

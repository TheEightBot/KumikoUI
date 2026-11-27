using KumikoUI.Core.Models;

namespace KumikoUI.Maui.Tests;

/// <summary>The empty view properties of the MAUI <see cref="KumikoDataGrid"/> (#326): style wiring, theme survival and the grid hint.</summary>
public class KumikoDataGridEmptyViewTests
{
    public sealed class Row
    {
        public string Name { get; set; } = "";
    }

    private static KumikoDataGrid Grid(int rows)
    {
        var grid = new KumikoDataGrid
        {
            AnnounceChanges = false, // the reference assembly cannot announce
            Columns = { new KumikoColumn { Header = "Name", PropertyName = "Name" } },
            ItemsSource = Enumerable.Range(0, rows).Select(i => new Row { Name = $"R{i}" }).ToList(),
        };
        return grid;
    }

    [Fact]
    public void Defaults_ShowTheEmptyView_AsTheStyle()
    {
        var grid = new KumikoDataGrid();

        Assert.Null(grid.ShowEmptyView);
        Assert.Null(grid.EmptyViewText);
        Assert.Null(grid.EmptyViewRenderer);
        Assert.True(grid.GridStyle.ShowEmptyView);
    }

    [Fact]
    public void ShowEmptyView_ReachesTheStyle_AndSurvivesAThemeAndAReplacedStyle()
    {
        var grid = new KumikoDataGrid { ShowEmptyView = false };
        Assert.False(grid.GridStyle.ShowEmptyView);

        grid.Theme = KumikoThemeMode.Dark;
        Assert.False(grid.GridStyle.ShowEmptyView);

        grid.GridStyle = new KumikoStyle { ShowEmptyView = true };
        Assert.False(grid.GridStyle.ShowEmptyView);

        grid.ShowEmptyView = true;
        Assert.True(grid.GridStyle.ShowEmptyView);
    }

    [Fact]
    public void TheEmptyViewTextColor_FollowsTheTheme()
    {
        var grid = new KumikoDataGrid { Theme = KumikoThemeMode.Dark };

        Assert.Equal(KumikoTheme.CreateDark().EmptyViewTextColor, grid.GridStyle.EmptyViewTextColor);
    }

    [Fact]
    public void GridHint_IsTheEmptyText_WhileTheViewHasNoRows()
    {
        var grid = Grid(rows: 0);

        Assert.Equal("No items to show", Microsoft.Maui.Controls.SemanticProperties.GetHint(grid));

        grid.EmptyViewText = "Nothing yet";
        Assert.Equal("Nothing yet", Microsoft.Maui.Controls.SemanticProperties.GetHint(grid));
    }

    [Fact]
    public void GridHint_IsTheNoMatchesText_AfterAFilterHidesEveryRow_AndTheSizeAgainWhenCleared()
    {
        var grid = Grid(rows: 3);
        Assert.Equal("3 rows, 1 columns", Microsoft.Maui.Controls.SemanticProperties.GetHint(grid));

        grid.DataSource.SetFilter(_ => false);
        Assert.Equal("No rows match the current filters", Microsoft.Maui.Controls.SemanticProperties.GetHint(grid));

        grid.DataSource.SetFilter(null);
        Assert.Equal("3 rows, 1 columns", Microsoft.Maui.Controls.SemanticProperties.GetHint(grid));
    }

    [Fact]
    public void GridHint_ExplicitHintWins_AndDisabledEmptyViewKeepsTheSize()
    {
        var grid = Grid(rows: 0);
        grid.GridHint = "My grid";
        Assert.Equal("My grid", Microsoft.Maui.Controls.SemanticProperties.GetHint(grid));

        grid.GridHint = null;
        grid.ShowEmptyView = false;
        Assert.Equal("0 rows, 1 columns", Microsoft.Maui.Controls.SemanticProperties.GetHint(grid));
    }
}

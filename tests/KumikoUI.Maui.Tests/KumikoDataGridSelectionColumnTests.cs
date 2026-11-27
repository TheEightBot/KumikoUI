using System.Collections.ObjectModel;
using KumikoUI.Core.Models;

namespace KumikoUI.Maui.Tests;

/// <summary>
/// The MAUI grid's selection column surface (#321): <c>ShowSelectionColumn</c> adds and removes the column the grid
/// owns, <c>SelectOnRowClick</c> reaches the selection model, and check box toggles keep <c>SelectedItems</c> in step.
/// </summary>
public class KumikoDataGridSelectionColumnTests
{
    private sealed record Row(string Name);

    private static KumikoDataGrid Grid(params KumikoColumn[] columns)
    {
        var grid = new KumikoDataGrid
        {
            ItemsSource = new ObservableCollection<Row>(Enumerable.Range(0, 5).Select(i => new Row($"R{i}"))),
            AnnounceChanges = false,
        };
        foreach (var column in columns) grid.Columns.Add(column);
        return grid;
    }

    private static KumikoColumn Name() => new() { Header = "Name", PropertyName = "Name" };

    [Fact]
    public void ShowSelectionColumn_DefaultsToFalse_AndSelectOnRowClickToTrue()
    {
        var grid = Grid(Name());

        Assert.False(grid.ShowSelectionColumn);
        Assert.True(grid.SelectOnRowClick);
        Assert.DoesNotContain(grid.Columns, c => c is KumikoSelectionColumn);
    }

    [Fact]
    public void ShowSelectionColumn_InsertsTheColumnFirst_AndRemovesIt()
    {
        var grid = Grid(Name());

        grid.ShowSelectionColumn = true;

        Assert.IsType<KumikoSelectionColumn>(grid.Columns[0]);
        Assert.Equal(2, grid.Columns.Count);
        Assert.Same(grid.Columns[0], grid.DataSource.Columns[0]);
        Assert.Equal(2, grid.DataSource.Columns.Count);

        grid.ShowSelectionColumn = false;

        Assert.Single(grid.Columns);
        Assert.Single(grid.DataSource.Columns);
    }

    [Fact]
    public void ShowSelectionColumn_DoesNothing_WhenTheAppDeclaredOne()
    {
        var declared = new KumikoSelectionColumn();
        var grid = Grid(Name(), declared);

        grid.ShowSelectionColumn = true;

        Assert.Single(grid.Columns, c => c.ColumnType == KumikoColumnType.Selection);
        Assert.Same(declared, grid.Columns.Single(c => c.ColumnType == KumikoColumnType.Selection));
        Assert.Equal(2, grid.Columns.Count);
    }

    [Fact]
    public void ShowSelectionColumn_GivesWay_WhenTheAppDeclaresOneLater()
    {
        var grid = Grid(Name());
        grid.ShowSelectionColumn = true;
        var declared = new KumikoSelectionColumn();

        grid.Columns.Add(declared);

        Assert.Single(grid.Columns, c => c.ColumnType == KumikoColumnType.Selection);
        Assert.Contains(declared, grid.Columns);
        Assert.Equal(grid.Columns.Count, grid.DataSource.Columns.Count);
    }

    [Fact]
    public void ShowSelectionColumn_IsInsertedAgain_WhenColumnsIsReplacedOrCleared()
    {
        var grid = Grid(Name());
        grid.ShowSelectionColumn = true;

        grid.Columns = new ObservableCollection<KumikoColumn> { Name() };
        Assert.IsType<KumikoSelectionColumn>(grid.Columns[0]);
        Assert.IsType<KumikoSelectionColumn>(grid.DataSource.Columns[0]);
        Assert.Equal(2, grid.DataSource.Columns.Count);

        grid.Columns.Clear();
        Assert.Single(grid.Columns);
        Assert.IsType<KumikoSelectionColumn>(grid.Columns[0]);

        grid.Columns.Add(Name());
        Assert.IsType<KumikoSelectionColumn>(grid.Columns[0]);
        Assert.Equal(2, grid.DataSource.Columns.Count);
    }

    [Fact]
    public void ShowSelectionColumn_StaysFirst_WhenAColumnIsInsertedAtTheStart()
    {
        var grid = Grid(Name());
        grid.ShowSelectionColumn = true;

        grid.Columns.Insert(0, new KumikoColumn { Header = "Id", PropertyName = "Id" });

        Assert.IsType<KumikoSelectionColumn>(grid.Columns[0]);
        Assert.Equal("Id", grid.Columns[1].Header);
    }

    [Fact]
    public void ShowSelectionColumn_FromTheStart_IsHonoredBeforeColumnsAreDeclared()
    {
        var grid = new KumikoDataGrid { ShowSelectionColumn = true, AnnounceChanges = false };
        grid.Columns.Add(Name());

        Assert.IsType<KumikoSelectionColumn>(grid.Columns[0]);
        Assert.Equal(2, grid.DataSource.Columns.Count);
    }

    [Fact]
    public void SelectOnRowClick_ReachesTheSelectionModel()
    {
        var grid = Grid(Name());

        grid.SelectOnRowClick = false;
        Assert.False(grid.Selection.SelectOnRowClick);

        grid.SelectOnRowClick = true;
        Assert.True(grid.Selection.SelectOnRowClick);
    }

    [Fact]
    public void CheckBoxToggles_KeepSelectedItemsInSync()
    {
        var grid = Grid(Name());
        grid.GridSelectionMode = KumikoSelectionMode.Multiple;
        grid.ShowSelectionColumn = true;

        grid.Selection.HandleCheckBoxToggle(1, false);
        grid.Selection.HandleCheckBoxToggle(3, false);

        Assert.Equal(2, grid.SelectedItems!.Count);
        Assert.Equal(new[] { "R1", "R3" }, grid.SelectedItems.Cast<Row>().Select(r => r.Name).Order());

        grid.Selection.HandleCheckBoxToggle(1, false);
        Assert.Single(grid.SelectedItems);
    }

    [Fact]
    public void SelectedItemsBinding_SelectingFromTheList_ChecksTheRows()
    {
        var grid = Grid(Name());
        grid.GridSelectionMode = KumikoSelectionMode.Extended;
        grid.ShowSelectionColumn = true;
        var items = ((ObservableCollection<Row>)grid.ItemsSource!);

        grid.SelectedItems!.Add(items[2]);

        Assert.True(grid.Selection.IsRowSelected(2));
        Assert.Null(grid.Selection.CheckAllState);
    }
}

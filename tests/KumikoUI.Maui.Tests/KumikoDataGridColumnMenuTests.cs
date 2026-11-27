using System.Collections.ObjectModel;
using KumikoUI.Core.Components;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;

namespace KumikoUI.Maui.Tests;

/// <summary>
/// The MAUI grid's column menu surface (#319): <c>IsColumnMenuEnabled</c> and <c>ShowColumnMenuButton</c> reach the
/// input controller, <c>ColumnMenuOpening</c> re-surfaces the controller's event, and a handler of
/// <c>ContextMenuRequested</c> that marks the request handled keeps the built-in menu closed.
/// </summary>
public class KumikoDataGridColumnMenuTests
{
    private sealed record Person(string Name, int Age);

    private static KumikoDataGrid Grid()
    {
        var grid = new KumikoDataGrid
        {
            ItemsSource = new ObservableCollection<Person>(Enumerable.Range(0, 5).Select(i => new Person($"P{i}", 20 + i))),
            AnnounceChanges = false, // the announcement needs a real dispatcher
        };
        grid.Columns.Add(new KumikoColumn { Header = "Name", PropertyName = "Name", Width = 120 });
        grid.Columns.Add(new KumikoColumn { Header = "Age", PropertyName = "Age", Width = 80 });
        return grid;
    }

    private static void RightClick(KumikoDataGrid grid, float x, float y)
    {
        var scroll = new ScrollState { ViewportWidth = 600, ViewportHeight = 600 };
        foreach (var action in new[] { InputAction.Pressed, InputAction.Released })
        {
            grid.InputController.HandlePointer(
                new KumikoPointerEventArgs { X = x, Y = y, Action = action, Button = PointerButton.Secondary },
                scroll, grid.Selection, grid.GridStyle, grid.DataSource);
        }
    }

    [Fact]
    public void ColumnMenu_IsOffByDefault()
    {
        var grid = Grid();

        Assert.False(grid.IsColumnMenuEnabled);
        Assert.False(grid.ShowColumnMenuButton);
        RightClick(grid, 150, 20);
        Assert.Null(grid.InputController.ActivePopup);
    }

    [Fact]
    public void IsColumnMenuEnabled_OpensTheMenuOnARightClick_AndRaisesColumnMenuOpening()
    {
        var grid = Grid();
        grid.IsColumnMenuEnabled = true;
        KumikoColumn? opened = null;
        grid.ColumnMenuOpening += (_, e) => opened = e.Column;

        RightClick(grid, 150, 20);

        Assert.True(grid.InputController.IsColumnMenuEnabled);
        Assert.IsType<DrawnMenu>(grid.InputController.ActivePopup);
        Assert.Same(grid.Columns[1], opened);
    }

    [Fact]
    public void AContextMenuRequestedHandler_ThatMarksItHandled_KeepsTheMenuClosed()
    {
        var grid = Grid();
        grid.IsColumnMenuEnabled = true;
        grid.ContextMenuRequested += (_, e) => e.Handled = true;

        RightClick(grid, 150, 20);

        Assert.Null(grid.InputController.ActivePopup);
    }

    [Fact]
    public void ShowColumnMenuButton_IsOnlyShownWhileTheMenuIsEnabled()
    {
        var grid = Grid();

        grid.ShowColumnMenuButton = true;
        Assert.False(grid.InputController.ShowColumnMenuButton);

        grid.IsColumnMenuEnabled = true;
        Assert.True(grid.InputController.ShowColumnMenuButton);
    }

    [Fact]
    public void ShowColumnMenu_OpensTheMenuOfTheColumn()
    {
        var grid = Grid();

        Assert.True(grid.ShowColumnMenu(grid.Columns[0]));

        Assert.IsType<DrawnMenu>(grid.InputController.ActivePopup);
    }
}

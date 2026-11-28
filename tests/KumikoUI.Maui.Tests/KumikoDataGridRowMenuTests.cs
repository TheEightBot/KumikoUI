using System.Collections.ObjectModel;
using KumikoUI.Core.Components;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;

namespace KumikoUI.Maui.Tests;

/// <summary>
/// The MAUI grid's row menu surface (#331): <c>IsRowMenuEnabled</c> reaches the input controller, <c>RowMenuItems</c>
/// is the controller's list, <c>RowMenuOpening</c> re-surfaces its event, and a handler of <c>ContextMenuRequested</c>
/// that marks the request handled keeps the built-in menu closed.
/// </summary>
public class KumikoDataGridRowMenuTests
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

    private static void RightClickRow(KumikoDataGrid grid, int row)
    {
        var scroll = new ScrollState { ViewportWidth = 600, ViewportHeight = 600 };
        float y = grid.GridStyle.HeaderHeight + (row + 0.5f) * grid.GridStyle.RowHeight;
        foreach (var action in new[] { InputAction.Pressed, InputAction.Released })
        {
            grid.InputController.HandlePointer(
                new KumikoPointerEventArgs { X = 50, Y = y, Action = action, Button = PointerButton.Secondary },
                scroll, grid.Selection, grid.GridStyle, grid.DataSource);
        }
    }

    [Fact]
    public void RowMenu_IsOffByDefault()
    {
        var grid = Grid();

        Assert.False(grid.IsRowMenuEnabled);
        RightClickRow(grid, 1);
        Assert.Null(grid.InputController.ActivePopup);
    }

    [Fact]
    public void IsRowMenuEnabled_OpensTheMenuOnARightClick_AndRaisesRowMenuOpening()
    {
        var grid = Grid();
        grid.IsRowMenuEnabled = true;
        grid.Clipboard = new TestClipboard();
        grid.RowMenuItems.Add(new DrawnMenuItem("Mine", "Mine"));
        object? opened = null;
        grid.RowMenuOpening += (_, e) => opened = e.DataItem;

        RightClickRow(grid, 1);

        var menu = Assert.IsType<DrawnMenu>(grid.InputController.ActivePopup);
        Assert.Equal(new Person("P1", 21), opened);
        Assert.Contains("Mine", menu.Items.Select(i => i.Key));
        Assert.Contains(KumikoRowMenuItems.Copy, menu.Items.Select(i => i.Key));
    }

    [Fact]
    public void AHandlerOfContextMenuRequested_ThatMarksItHandled_KeepsTheRowMenuClosed()
    {
        var grid = Grid();
        grid.IsRowMenuEnabled = true;
        grid.Clipboard = new TestClipboard();
        grid.ContextMenuRequested += (_, e) => e.Handled = true;

        RightClickRow(grid, 1);

        Assert.Null(grid.InputController.ActivePopup);
    }

    private sealed class TestClipboard : IKumikoClipboard
    {
        public bool TrySetText(string text) => true;

        public Task<string?> GetTextAsync() => Task.FromResult<string?>(null);
    }
}

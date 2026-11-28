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
        grid.InputController.RowMenuItems.Add(new DrawnMenuItem("Mine", "Mine"));
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

    private sealed class MenuViewModel
    {
        public List<object?> Received { get; } = new();

        public bool CanRun { get; set; } = true;

        public System.Windows.Input.ICommand Command { get; }

        public MenuViewModel() => Command = new Microsoft.Maui.Controls.Command<object?>(p => Received.Add(p), _ => CanRun);
    }

    [Fact]
    public void DeclaredItems_GetTheGridsBindingContext_AndTheirCommandGetsTheRowsItem()
    {
        var grid = Grid();
        var viewModel = new MenuViewModel();
        var item = new MauiRowMenuItem { Key = "Mine", Text = "Mine" };
        item.SetBinding(MauiRowMenuItem.CommandProperty, nameof(MenuViewModel.Command));
        var explicitItem = new MauiRowMenuItem { Key = "Fixed", Text = "Fixed", CommandParameter = "x" };
        explicitItem.SetBinding(MauiRowMenuItem.CommandProperty, nameof(MenuViewModel.Command));
        grid.RowMenuItems.Add(item);
        grid.RowMenuItems.Add(explicitItem);
        grid.BindingContext = viewModel; // after the items were added: the context still reaches them
        grid.IsRowMenuEnabled = true;
        grid.Clipboard = new TestClipboard();

        RightClickRow(grid, 2);
        var menu = Assert.IsType<DrawnMenu>(grid.InputController.ActivePopup);
        var mine = menu.Items.Single(i => i.Key == "Mine");
        var fixedItem = menu.Items.Single(i => i.Key == "Fixed");
        Assert.True(mine.IsEnabled);
        Assert.Equal("x", fixedItem.CommandParameter);

        mine.Action?.Invoke();
        mine.Command!.Execute(mine.CommandParameter);
        fixedItem.Command!.Execute(fixedItem.CommandParameter);

        Assert.Equal(new Person("P2", 22), viewModel.Received[0]);
        Assert.Equal("x", viewModel.Received[1]);
    }

    [Fact]
    public void ADeclaredItem_IsDisabled_WhenItsCommandCannotExecute_OrItIsNotEnabled()
    {
        var grid = Grid();
        var viewModel = new MenuViewModel { CanRun = false };
        var blocked = new MauiRowMenuItem { Key = "Blocked", Text = "Blocked" };
        blocked.SetBinding(MauiRowMenuItem.CommandProperty, nameof(MenuViewModel.Command));
        grid.RowMenuItems.Add(blocked);
        grid.RowMenuItems.Add(new MauiRowMenuItem { Key = "Off", Text = "Off", IsEnabled = false });
        grid.BindingContext = viewModel;
        grid.IsRowMenuEnabled = true;
        grid.Clipboard = new TestClipboard();

        RightClickRow(grid, 1);

        var menu = Assert.IsType<DrawnMenu>(grid.InputController.ActivePopup);
        Assert.False(menu.Items.Single(i => i.Key == "Blocked").IsEnabled);
        Assert.False(menu.Items.Single(i => i.Key == "Off").IsEnabled);
    }

    [Fact]
    public void DeclaredItems_ComeBeforeTheHandlersOfRowMenuOpening()
    {
        var grid = Grid();
        grid.RowMenuItems.Add(new MauiRowMenuItem { Key = "Mine", Text = "Mine" });
        grid.IsRowMenuEnabled = true;
        grid.Clipboard = new TestClipboard();
        bool seen = false;
        grid.RowMenuOpening += (_, e) => seen = e.Items.Any(i => i.Key == "Mine");

        RightClickRow(grid, 1);

        Assert.True(seen);
    }

    private sealed class TestClipboard : IKumikoClipboard
    {
        public bool TrySetText(string text) => true;

        public Task<string?> GetTextAsync() => Task.FromResult<string?>(null);
    }
}

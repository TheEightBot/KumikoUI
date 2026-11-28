using System.Collections.ObjectModel;
using KumikoUI.Core.Components;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;

namespace KumikoUI.Maui.Tests;

/// <summary>
/// The MAUI grid's column chooser surface (#335): its members forward to the input controller and the source with the
/// same defaults as Core, <c>ColumnsChanged</c> re-surfaces the source's event with the grid as sender, the command
/// opens the chooser, and the chooser's changes reach the grid's <c>Columns</c> collection.
/// </summary>
public class KumikoDataGridColumnChooserTests
{
    private sealed record Person(string Name, int Age, string City);

    private static KumikoDataGrid Grid()
    {
        var grid = new KumikoDataGrid
        {
            ItemsSource = new ObservableCollection<Person>(Enumerable.Range(0, 5).Select(i => new Person($"P{i}", 20 + i, "Oslo"))),
            AnnounceChanges = false, // the announcement needs a real dispatcher
        };
        grid.Columns.Add(new KumikoColumn { Header = "Name", PropertyName = "Name", Width = 120 });
        grid.Columns.Add(new KumikoColumn { Header = "Age", PropertyName = "Age", Width = 80 });
        grid.Columns.Add(new KumikoColumn { Header = "City", PropertyName = "City", Width = 100 });
        return grid;
    }

    [Fact]
    public void Defaults_MatchCore()
    {
        var grid = Grid();

        Assert.False(grid.IsColumnChooserOpen);
        Assert.False(grid.ColumnChooserOnHeaderContext);
        Assert.Null(grid.ColumnChooserShortcut);
        Assert.False(grid.InputController.ColumnChooserOnHeaderContext);
    }

    [Fact]
    public void ShowAndClose_ForwardToTheController_AndRaiseTheGridsEvents()
    {
        var grid = Grid();
        var events = new List<(string Name, object? Sender)>();
        grid.ColumnChooserOpened += (s, _) => events.Add(("opened", s));
        grid.ColumnChooserClosed += (s, _) => events.Add(("closed", s));

        grid.ShowColumnChooser();
        Assert.True(grid.IsColumnChooserOpen);
        Assert.IsType<DrawnColumnChooser>(grid.InputController.ActivePopup);

        grid.CloseColumnChooser();
        Assert.False(grid.IsColumnChooserOpen);

        Assert.Equal(new[] { ("opened", (object?)grid), ("closed", grid) }, events);
    }

    [Fact]
    public void ShowColumnChooser_AtARect_PlacesItBelowTheRect()
    {
        var grid = Grid();

        grid.ShowColumnChooser(new Rect(10, 50, 80, 20));
        grid.InputController.PopupManager.UpdateAnchors();

        var chooser = Assert.IsType<DrawnColumnChooser>(grid.InputController.ActivePopup);
        Assert.Equal(10f, chooser.Bounds.X);
        Assert.Equal(70f, chooser.Bounds.Y);
    }

    [Fact]
    public void ShowColumnChooserCommand_OpensTheChooser()
    {
        var grid = Grid();

        Assert.True(grid.ShowColumnChooserCommand.CanExecute(null));
        grid.ShowColumnChooserCommand.Execute(null);

        Assert.True(grid.IsColumnChooserOpen);
        Assert.Same(grid.ShowColumnChooserCommand, grid.ShowColumnChooserCommand);
    }

    [Fact]
    public void TheOptions_ReachTheController()
    {
        var grid = Grid();
        var shortcut = new KumikoShortcut(KumikoKey.C, InputModifiers.Control | InputModifiers.Alt);

        grid.ColumnChooserOnHeaderContext = true;
        grid.ColumnChooserShortcut = shortcut;

        Assert.True(grid.InputController.ColumnChooserOnHeaderContext);
        Assert.Equal(shortcut, grid.InputController.ColumnChooserShortcut);
    }

    [Fact]
    public void ColumnsChanged_ReSurfacesTheSourcesEvent_WithTheGridAsSender()
    {
        var grid = Grid();
        var events = new List<(object? Sender, KumikoColumnsChangedEventArgs Args)>();
        grid.ColumnsChanged += (s, e) => events.Add((s, e));

        grid.Columns[1].IsVisible = false;

        var (sender, args) = Assert.Single(events);
        Assert.Same(grid, sender);
        Assert.Equal(KumikoColumnsChangeKind.Visibility, args.Kind);
        Assert.Same(grid.Columns[1], args.Column);
    }

    [Fact]
    public void ResetFromTheChooser_ReordersTheGridsColumnsCollectionToo()
    {
        var grid = Grid();
        var name = grid.Columns[0];
        grid.DataSource.ReorderColumn(0, 2);
        Assert.Same(name, grid.Columns[2]); // the collection follows a move

        grid.DataSource.ResetColumnLayout();

        Assert.Same(name, grid.Columns[0]);
        Assert.Equal(grid.DataSource.Columns, grid.Columns);
    }
}

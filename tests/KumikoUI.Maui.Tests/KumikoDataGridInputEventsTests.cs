using System.Collections.ObjectModel;
using System.Windows.Input;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;

namespace KumikoUI.Maui.Tests;

/// <summary>
/// The tap and context-menu surface of the MAUI <see cref="KumikoDataGrid"/> (#100): <c>CellTapped</c>,
/// <c>HeaderTapped</c> and <c>ContextMenuRequested</c> re-surface the input controller's events, and their commands
/// run with the event arguments.
/// </summary>
public class KumikoDataGridInputEventsTests
{
    private sealed record Person(string Name, int Age);

    private sealed class RecordingCommand : ICommand
    {
        public List<object?> Parameters { get; } = new();
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => Parameters.Add(parameter);
    }

    // Header 0-40 (default height), rows of the default height below it; Name is x 0-120, Age x 120-200.
    private static KumikoDataGrid Grid()
    {
        var grid = new KumikoDataGrid
        {
            ItemsSource = new ObservableCollection<Person>(Enumerable.Range(0, 5).Select(i => new Person($"P{i}", 20 + i))),
            AnnounceChanges = false, // the announcement timer needs a real dispatcher
        };
        grid.Columns.Add(new KumikoColumn { Header = "Name", PropertyName = "Name", Width = 120 });
        grid.Columns.Add(new KumikoColumn { Header = "Age", PropertyName = "Age", Width = 80 });
        return grid;
    }

    private static float RowY(KumikoDataGrid grid, int row) =>
        grid.GridStyle.HeaderHeight + row * grid.GridStyle.RowHeight + grid.GridStyle.RowHeight / 2;

    private static void Click(KumikoDataGrid grid, float x, float y, PointerButton button)
    {
        var scroll = new ScrollState { ViewportWidth = 400, ViewportHeight = 400 };
        foreach (var action in new[] { InputAction.Pressed, InputAction.Released })
        {
            grid.InputController.HandlePointer(new KumikoPointerEventArgs { X = x, Y = y, Action = action, Button = button },
                scroll, grid.Selection, grid.GridStyle, grid.DataSource);
        }
    }

    [Fact]
    public void RightClick_RaisesContextMenuRequested_AndRunsItsCommand_WithoutSorting()
    {
        var grid = Grid();
        var command = new RecordingCommand();
        grid.ContextMenuRequestedCommand = command;
        var raised = new List<KumikoContextRequestedEventArgs>();
        grid.ContextMenuRequested += (_, e) => raised.Add(e);

        Click(grid, 150, 20, PointerButton.Secondary);

        var e = Assert.Single(raised);
        Assert.Same(e, Assert.Single(command.Parameters));
        Assert.Equal(KumikoContextTrigger.SecondaryButton, e.Trigger);
        Assert.Equal("Age", e.Column?.PropertyName);
        Assert.Equal(KumikoSortDirection.None, grid.Columns[1].SortDirection);
    }

    [Fact]
    public void RightClick_OnARow_ReportsItsItem()
    {
        var grid = Grid();
        var command = new RecordingCommand();
        grid.ContextMenuRequestedCommand = command;

        Click(grid, 60, RowY(grid, 2), PointerButton.Secondary);

        var e = Assert.IsType<KumikoContextRequestedEventArgs>(Assert.Single(command.Parameters));
        Assert.Equal(new Person("P2", 22), e.DataItem);
        Assert.Equal(2, e.RowIndex);
    }

    [Fact]
    public void PrimaryTaps_RaiseCellTappedAndHeaderTapped_AndRunTheirCommands()
    {
        var grid = Grid();
        var cellCommand = new RecordingCommand();
        var headerCommand = new RecordingCommand();
        grid.CellTappedCommand = cellCommand;
        grid.HeaderTappedCommand = headerCommand;
        int cellEvents = 0, headerEvents = 0;
        grid.CellTapped += (_, _) => cellEvents++;
        grid.HeaderTapped += (_, _) => headerEvents++;

        Click(grid, 60, RowY(grid, 1), PointerButton.Primary);
        Click(grid, 60, 20, PointerButton.Primary);

        var cell = Assert.IsType<KumikoCellTappedEventArgs>(Assert.Single(cellCommand.Parameters));
        Assert.Equal(1, cell.RowIndex);
        var header = Assert.IsType<KumikoHeaderTappedEventArgs>(Assert.Single(headerCommand.Parameters));
        Assert.Equal("Name", header.Column.PropertyName);
        Assert.Equal((1, 1), (cellEvents, headerEvents));
        Assert.Equal(KumikoSortDirection.Ascending, grid.Columns[0].SortDirection);
    }
}

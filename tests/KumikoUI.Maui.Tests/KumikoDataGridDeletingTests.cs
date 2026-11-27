using System.Collections.ObjectModel;
using System.Windows.Input;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;

namespace KumikoUI.Maui.Tests;

/// <summary>
/// The MAUI grid's delete surface (#328): <c>AllowDeleting</c> reaches the input controller, the Delete key and
/// <c>DeleteSelectedRows</c> remove the selected rows, <c>RowsDeleting</c> and <c>RowsDeleted</c> re-surface the
/// controller's events, and <c>RowsDeletingCommand</c> runs with the event arguments and can cancel.
/// </summary>
public class KumikoDataGridDeletingTests
{
    private sealed record Person(string Name, int Age);

    private sealed class RecordingCommand(Action<object?> execute, bool canExecute = true) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => canExecute;
        public void Execute(object? parameter) => execute(parameter);
    }

    private static (KumikoDataGrid Grid, ObservableCollection<Person> People) Grid()
    {
        var people = new ObservableCollection<Person>(Enumerable.Range(0, 6).Select(i => new Person($"P{i}", 20 + i)));
        var grid = new KumikoDataGrid { ItemsSource = people, AnnounceChanges = false };
        grid.Columns.Add(new KumikoColumn { Header = "Name", PropertyName = "Name", Width = 120 });
        grid.Columns.Add(new KumikoColumn { Header = "Age", PropertyName = "Age", Width = 80 });
        return (grid, people);
    }

    private static void PressDelete(KumikoDataGrid grid)
    {
        var scroll = new ScrollState { ViewportWidth = 600, ViewportHeight = 600 };
        grid.InputController.HandleKey(new KumikoKeyEventArgs { Key = KumikoKey.Delete }, scroll, grid.Selection, grid.GridStyle, grid.DataSource);
    }

    [Fact]
    public void AllowDeleting_IsOffByDefault_AndReachesTheController()
    {
        var (grid, people) = Grid();
        grid.Selection.Select(people[1]);

        Assert.False(grid.AllowDeleting);
        PressDelete(grid);
        Assert.Equal(6, people.Count);

        grid.AllowDeleting = true;
        Assert.True(grid.InputController.AllowDeleting);
        PressDelete(grid);
        Assert.Equal(5, people.Count);
    }

    [Fact]
    public void DeleteSelectedRows_RemovesTheSelection_WhateverAllowDeletingSays()
    {
        var (grid, people) = Grid();
        grid.Selection.SetSelectedItems(new object[] { people[0], people[2] });

        Assert.True(grid.DeleteSelectedRows());

        Assert.Equal(new[] { "P1", "P3", "P4", "P5" }, people.Select(p => p.Name));
    }

    [Fact]
    public void RowsDeleting_ReSurfacesTheEvent_AndCancelKeepsTheRows()
    {
        var (grid, people) = Grid();
        KumikoRowsDeletingEventArgs? seen = null;
        grid.RowsDeleting += (_, e) => { seen = e; e.Cancel = true; };
        grid.Selection.Select(people[3]);

        Assert.False(grid.DeleteSelectedRows());

        Assert.Equal(KumikoRowsDeleteTrigger.Command, seen!.Trigger);
        Assert.Same(people[3], seen.Items.Single());
        Assert.Equal(6, people.Count);
    }

    [Fact]
    public void RowsDeleted_ReportsTheResult()
    {
        var (grid, people) = Grid();
        KumikoRowsDeletedEventArgs? seen = null;
        grid.RowsDeleted += (_, e) => seen = e;
        grid.Selection.Select(people[3]);

        grid.DeleteSelectedRows();

        Assert.Equal(KumikoRemoveResult.Removed, seen!.Result);
    }

    [Fact]
    public void RowsDeletingCommand_RunsWithTheEventArguments_AndCanCancel()
    {
        var (grid, people) = Grid();
        object? parameter = null;
        grid.RowsDeletingCommand = new RecordingCommand(p => { parameter = p; ((KumikoRowsDeletingEventArgs)p!).Cancel = true; });
        grid.Selection.Select(people[0]);

        Assert.False(grid.DeleteSelectedRows());

        var args = Assert.IsType<KumikoRowsDeletingEventArgs>(parameter);
        Assert.Same(people[0], args.Items.Single());
        Assert.Equal(6, people.Count);
    }

    [Fact]
    public void RowsDeletingCommand_IsSkipped_WhenItCannotExecute()
    {
        var (grid, people) = Grid();
        bool ran = false;
        grid.RowsDeletingCommand = new RecordingCommand(_ => ran = true, canExecute: false);
        grid.Selection.Select(people[0]);

        Assert.True(grid.DeleteSelectedRows());

        Assert.False(ran);
        Assert.Equal(5, people.Count);
    }
}

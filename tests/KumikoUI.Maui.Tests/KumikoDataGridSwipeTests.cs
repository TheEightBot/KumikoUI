using System.Collections.ObjectModel;
using System.Windows.Input;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;

namespace KumikoUI.Maui.Tests;

/// <summary>
/// The MAUI grid's swipe row actions surface (#329): the bindable properties reach the input controller, the swipe item
/// lists are the controller's, <see cref="MauiActionButtonDefinition"/> items get the grid's <c>BindingContext</c> and
/// bind their commands, the events re-surface the controller's, and a change of the rows closes an open row.
/// </summary>
public class KumikoDataGridSwipeTests
{
    private sealed record Person(string Name, int Age);

    private sealed class RecordingCommand : ICommand
    {
        public List<object?> Runs { get; } = new();
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => Runs.Add(parameter);
    }

    private sealed class ViewModel
    {
        public RecordingCommand ArchiveCommand { get; } = new();
    }

    private static readonly ScrollState Scroll = new() { ViewportWidth = 300, ViewportHeight = 400 };

    private static (KumikoDataGrid Grid, ObservableCollection<Person> People) Grid()
    {
        var people = new ObservableCollection<Person>(Enumerable.Range(0, 8).Select(i => new Person($"P{i}", 20 + i)));
        var grid = new KumikoDataGrid { ItemsSource = people, AnnounceChanges = false, AllowSwiping = true };
        grid.Columns.Add(new KumikoColumn { Header = "Name", PropertyName = "Name", Width = 150 });
        grid.Columns.Add(new KumikoColumn { Header = "Age", PropertyName = "Age", Width = 150 });
        return (grid, people);
    }

    private static void Touch(KumikoDataGrid grid, InputAction action, float x, float y) =>
        grid.InputController.HandlePointer(
            new KumikoPointerEventArgs { X = x, Y = y, Action = action, DeviceType = KumikoPointerDeviceType.Touch },
            Scroll, grid.Selection, grid.GridStyle, grid.DataSource);

    /// <summary>A finger drags row 1 (y 94) 60 px to the left and lifts: the row opens.</summary>
    private static void SwipeOpen(KumikoDataGrid grid)
    {
        Touch(grid, InputAction.Pressed, 200, 94);
        for (int x = 190; x >= 140; x -= 10) Touch(grid, InputAction.Moved, x, 94);
        Touch(grid, InputAction.Released, 140, 94);
    }

    [Fact]
    public void TheProperties_ReachTheInputController()
    {
        var grid = new KumikoDataGrid { AnnounceChanges = false };
        Assert.False(grid.AllowSwiping);
        Assert.Equal(72, grid.SwipeItemWidth);
        Assert.Equal(KumikoFullSwipeMode.None, grid.FullSwipeMode);

        grid.AllowSwiping = true;
        grid.SwipeItemWidth = 90;
        grid.FullSwipeMode = KumikoFullSwipeMode.Trailing;

        Assert.True(grid.InputController.AllowSwiping);
        Assert.Equal(90f, grid.InputController.SwipeItemWidth);
        Assert.Equal(KumikoFullSwipeMode.Trailing, grid.InputController.FullSwipeMode);
        Assert.Same(grid.InputController.LeadingSwipeItems, grid.LeadingSwipeItems);
        Assert.Same(grid.InputController.TrailingSwipeItems, grid.TrailingSwipeItems);
    }

    [Fact]
    public void SwipeItems_GetTheGridsBindingContext_AndBindTheirCommands()
    {
        var (grid, people) = Grid();
        var archive = new MauiActionButtonDefinition { Label = "Archive" };
        archive.SetBinding(MauiActionButtonDefinition.CommandProperty, nameof(ViewModel.ArchiveCommand));
        grid.TrailingSwipeItems.Add(archive);
        var viewModel = new ViewModel();

        grid.BindingContext = viewModel;

        Assert.Same(viewModel, archive.BindingContext);
        Assert.Same(viewModel.ArchiveCommand, archive.Command);

        // A full swipe runs it with the row's item
        grid.FullSwipeMode = KumikoFullSwipeMode.Trailing;
        Touch(grid, InputAction.Pressed, 290, 94);
        for (int x = 280; x >= 40; x -= 10) Touch(grid, InputAction.Moved, x, 94);
        Touch(grid, InputAction.Released, 40, 94);
        Assert.Equal(new object?[] { people[1] }, viewModel.ArchiveCommand.Runs);
    }

    [Fact]
    public void AnItemAddedLater_GetsTheBindingContextToo_AndARemovedOneIsLetGo()
    {
        var (grid, _) = Grid();
        var viewModel = new ViewModel();
        grid.BindingContext = viewModel;
        var flag = new MauiActionButtonDefinition();
        flag.SetBinding(MauiActionButtonDefinition.CommandProperty, nameof(ViewModel.ArchiveCommand));

        grid.LeadingSwipeItems.Add(flag);
        Assert.Same(viewModel.ArchiveCommand, flag.Command);

        var changed = typeof(MauiActionButtonDefinition).GetField("Changed",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        Assert.NotNull(changed.GetValue(flag)); // the grid redraws on a change
        grid.LeadingSwipeItems.Remove(flag);
        Assert.Null(changed.GetValue(flag));
    }

    [Fact]
    public void TheEvents_ReSurfaceTheControllers()
    {
        var (grid, people) = Grid();
        grid.TrailingSwipeItems.Add(new MauiActionButtonDefinition { Label = "Archive" });
        var started = new List<KumikoSwipeStartingEventArgs>();
        var ended = new List<KumikoSwipeEndedEventArgs>();
        grid.SwipeStarting += (_, e) => started.Add(e);
        grid.SwipeEnded += (_, e) => ended.Add(e);

        SwipeOpen(grid);
        Assert.Same(people[1], Assert.Single(started).DataItem);
        Assert.Empty(ended);

        grid.CloseSwipe();
        Assert.Same(people[1], Assert.Single(ended).DataItem);
    }

    [Fact]
    public void AChangeOfTheRows_ClosesTheOpenRow_AtOnce()
    {
        var (grid, people) = Grid();
        grid.TrailingSwipeItems.Add(new MauiActionButtonDefinition { Label = "Archive" });
        var ended = new List<KumikoSwipeEndedEventArgs>();
        grid.SwipeEnded += (_, e) => ended.Add(e);
        SwipeOpen(grid);

        people.Insert(0, new Person("New", 1));

        Assert.Null(Assert.Single(ended).InvokedItem);
    }
}

using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows.Input;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace KumikoUI.Maui.Tests;

/// <summary>
/// The MAUI grid's pull-to-refresh surface (#330): the properties reach Core's input controller, <c>IsRefreshing</c>
/// is two-way, a pull raises <c>Refreshing</c> and executes <c>RefreshCommand</c>, and the frame loop runs only while
/// the indicator moves.
/// </summary>
public class KumikoDataGridPullToRefreshTests
{
    private sealed record Row(string Name);

    private sealed class RecordingCommand : ICommand
    {
        public bool Can { get; set; } = true;
        public List<object?> Executed { get; } = new();
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => Can;
        public void Execute(object? parameter) => Executed.Add(parameter);
    }

    private static KumikoDataGrid Grid()
    {
        var grid = new KumikoDataGrid
        {
            ItemsSource = new ObservableCollection<Row>(Enumerable.Range(0, 30).Select(i => new Row($"R{i}"))),
            RenderMode = KumikoRenderMode.Software,
            AnnounceChanges = false, // the screen reader has no implementation in the test host
        };
        grid.Columns.Add(new KumikoColumn { Header = "Name", PropertyName = nameof(Row.Name), Width = 200 });
        return grid;
    }

    /// <summary>A finger pulls from a row straight down by <paramref name="fingerTravel"/> past the pan threshold, then lifts.</summary>
    private static void Pull(KumikoDataGrid grid, float fingerTravel)
    {
        var scroll = new ScrollState { ViewportWidth = 300, ViewportHeight = 400 };
        void Send(InputAction action, float y) => grid.InputController.HandlePointer(new KumikoPointerEventArgs
        {
            X = 100, Y = y, Action = action, DeviceType = KumikoPointerDeviceType.Touch, TimestampMs = 1,
        }, scroll, grid.Selection, grid.GridStyle, grid.DataSource);

        Send(InputAction.Pressed, 60);
        Send(InputAction.Moved, 71);
        Send(InputAction.Moved, 71 + fingerTravel);
        Send(InputAction.Released, 71 + fingerTravel);
    }

    // ── Properties ──

    [Fact]
    public void Defaults_AreOff()
    {
        var grid = Grid();

        Assert.False(grid.IsPullToRefreshEnabled);
        Assert.False(grid.IsRefreshing);
        Assert.Null(grid.RefreshCommand);
        Assert.False(grid.InputController.IsPullToRefreshEnabled);
        Assert.Equal(BindingMode.TwoWay, KumikoDataGrid.IsRefreshingProperty.DefaultBindingMode);
    }

    [Fact]
    public void IsPullToRefreshEnabled_ReachesTheController()
    {
        var grid = Grid();

        grid.IsPullToRefreshEnabled = true;
        Assert.True(grid.InputController.IsPullToRefreshEnabled);

        grid.IsPullToRefreshEnabled = false;
        Assert.False(grid.InputController.IsPullToRefreshEnabled);
    }

    [Fact]
    public void IsRefreshing_IsTwoWay()
    {
        var grid = Grid();

        grid.IsRefreshing = true; // the view model starts a refresh
        Assert.True(grid.InputController.IsRefreshing);

        grid.InputController.IsRefreshing = false; // the grid ends it
        Assert.False(grid.IsRefreshing);

        grid.IsPullToRefreshEnabled = true;
        Pull(grid, 200); // a pull starts one
        Assert.True(grid.IsRefreshing);
    }

    [Fact]
    public void IsRefreshing_FollowsABinding_BothWays()
    {
        var vm = new ViewModel();
        var grid = Grid();
        grid.BindingContext = vm;
        grid.SetBinding(KumikoDataGrid.IsRefreshingProperty, nameof(ViewModel.IsLoading));
        grid.IsPullToRefreshEnabled = true;

        Pull(grid, 200);
        Assert.True(vm.IsLoading);

        vm.IsLoading = false;
        Assert.False(grid.IsRefreshing);
        Assert.False(grid.InputController.IsRefreshing);
    }

    // ── Event and command ──

    [Fact]
    public void APull_RaisesRefreshing_ThenExecutesTheCommand_WithItsParameter()
    {
        var grid = Grid();
        var command = new RecordingCommand();
        var order = new List<string>();
        grid.IsPullToRefreshEnabled = true;
        grid.RefreshCommand = command;
        grid.RefreshCommandParameter = "p";
        grid.Refreshing += (sender, _) =>
        {
            Assert.Same(grid, sender);
            Assert.True(grid.IsRefreshing);
            Assert.Empty(command.Executed);
            order.Add("Refreshing");
        };

        Pull(grid, 200);

        Assert.Equal(new[] { "Refreshing" }, order);
        Assert.Equal(new object?[] { "p" }, command.Executed);
    }

    [Fact]
    public void ACommandThatCannotExecute_IsNotExecuted()
    {
        var grid = Grid();
        var command = new RecordingCommand { Can = false };
        grid.IsPullToRefreshEnabled = true;
        grid.RefreshCommand = command;

        Pull(grid, 200);

        Assert.Empty(command.Executed);
        Assert.True(grid.IsRefreshing); // the app still ends it
    }

    [Fact]
    public void IsRefreshing_SetInCode_RaisesNoRefreshing_NorTheCommand()
    {
        var grid = Grid();
        var command = new RecordingCommand();
        int raised = 0;
        grid.RefreshCommand = command;
        grid.Refreshing += (_, _) => raised++;

        grid.IsRefreshing = true;

        Assert.Equal(0, raised);
        Assert.Empty(command.Executed);
    }

    [Fact]
    public void APullShortOfTheThreshold_RaisesNothing()
    {
        var grid = Grid();
        int raised = 0;
        grid.IsPullToRefreshEnabled = true;
        grid.Refreshing += (_, _) => raised++;

        Pull(grid, 60); // 30 px of the 64

        Assert.Equal(0, raised);
        Assert.False(grid.IsRefreshing);
    }

    // ── Frames ──

    private static T Field<T>(KumikoDataGrid grid, string name) =>
        (T)typeof(KumikoDataGrid).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(grid)!;

    private static bool FrameLoopAnimating(KumikoDataGrid grid)
    {
        object frames = Field<object>(grid, "_frames");
        return (bool)frames.GetType().GetProperty("IsAnimating")!.GetValue(frames)!;
    }

    /// <summary>Connects the frame scheduler as the handler would, so redraws and animations can start the loop.</summary>
    private static void AttachFrames(KumikoDataGrid grid)
    {
        object frames = Field<object>(grid, "_frames");
        frames.GetType().GetMethod("Attach")!.Invoke(frames, null);
    }

    private static void Frame(KumikoDataGrid grid, TimeSpan time) =>
        typeof(KumikoDataGrid).GetMethod("OnFrame", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(grid, new object[] { time });

    /// <summary>Paints the grid's software surface as its handler would (no layout).</summary>
    private static void Paint(KumikoDataGrid grid)
    {
        var view = Assert.IsType<SKCanvasView>(((Grid)grid.Content).Children[0]);
        var info = new SKImageInfo(300, 400, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        ((ISKCanvasView)view).OnPaintSurface(new SKPaintSurfaceEventArgs(surface, info));
    }

    [Fact]
    public void TheFrameLoop_RunsWhileTheIndicatorMoves_AndStopsOnceItIsGone()
    {
        var grid = Grid();
        grid.InputController.AnimationsEnabled = true; // the handler sets it from the reduced-motion setting
        AttachFrames(grid);
        Paint(grid);
        Assert.False(FrameLoopAnimating(grid));

        grid.IsRefreshing = true;
        Paint(grid);
        Assert.True(FrameLoopAnimating(grid)); // sliding in, then the spinner

        var time = TimeSpan.Zero;
        for (int i = 0; i < 30; i++) Frame(grid, time += TimeSpan.FromMilliseconds(16));
        Assert.True(FrameLoopAnimating(grid)); // the spinner turns while refreshing

        grid.IsRefreshing = false;
        for (int i = 0; i < 40; i++) Frame(grid, time += TimeSpan.FromMilliseconds(16));
        Assert.False(grid.InputController.PullToRefresh.IsVisible);
        Assert.False(FrameLoopAnimating(grid)); // nothing moves: no more frames
    }

    [Fact]
    public void WithAnimationsOff_ARefresh_RequestsNoFrames()
    {
        var grid = Grid();
        grid.InputController.AnimationsEnabled = true;
        grid.GridStyle.EnableAnimations = false;
        AttachFrames(grid);
        Paint(grid);

        grid.IsRefreshing = true;
        Paint(grid);

        Assert.True(grid.InputController.PullToRefresh.IsVisible); // a static spinner
        Assert.False(FrameLoopAnimating(grid));
    }

    private sealed class ViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isLoading;

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading == value) return;
                _isLoading = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsLoading)));
            }
        }
    }
}

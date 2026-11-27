using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.Rendering;

/// <summary>
/// The pull-to-refresh indicator allocates nothing per frame (#330), like the rest of the render loop: the disc, the
/// arrow of a pull in progress and the spinner turning between frames draw from reused paints and stack memory.
/// </summary>
[Collection(nameof(RenderAllocationCollection))]
public class PullToRefreshAllocationTests
{
    private const int WarmUpFrames = 20;
    private const int MeasuredFrames = 100;

    private sealed class Row
    {
        public string Name { get; set; } = "";
        public int Quantity { get; set; }
    }

    private sealed record Grid(KumikoRenderer Renderer, KumikoInputController Input, KumikoSource Source,
        ScrollState Scroll, KumikoSelectionModel Selection, KumikoStyle Style)
    {
        public void Render(IDrawingContext ctx) => Renderer.Render(ctx, Source, Scroll, Selection, Style);

        public void Pointer(InputAction action, float y) =>
            Input.HandlePointer(new KumikoPointerEventArgs
            {
                Action = action, X = 200, Y = y, DeviceType = KumikoPointerDeviceType.Touch, TimestampMs = 1,
            }, Scroll, Selection, Style, Source);
    }

    private static Grid CreateGrid(bool animations)
    {
        var source = new KumikoSource();
        source.SetColumns(new[]
        {
            new KumikoColumn { Header = "Name", PropertyName = nameof(Row.Name), Width = 150 },
            new KumikoColumn { Header = "Quantity", PropertyName = nameof(Row.Quantity), Width = 110, ColumnType = KumikoColumnType.Numeric },
        });
        source.SetItems(Enumerable.Range(0, 500).Select(i => new Row { Name = $"Row {i}", Quantity = i }).ToList());
        var style = new KumikoStyle { EnableAnimations = animations };
        var input = new KumikoInputController { IsPullToRefreshEnabled = true, AnimationsEnabled = true };
        var renderer = new KumikoRenderer { InputController = input };
        var grid = new Grid(renderer, input, source, new ScrollState { ViewportWidth = 400, ViewportHeight = 600 },
            new KumikoSelectionModel(source), style);
        grid.Render(new NoOpDrawingContext());
        return grid;
    }

    private static long MeasureSteadyState(Action<IDrawingContext> render)
    {
        var ctx = new NoOpDrawingContext();
        for (int i = 0; i < WarmUpFrames; i++)
            render(ctx);

        long least = long.MaxValue;
        for (int attempt = 0; attempt < 3 && least > 0; attempt++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < MeasuredFrames; i++)
                render(ctx);
            least = Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - before);
        }
        return least;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void APullInProgress_AllocatesNothingPerFrame(bool animations)
    {
        var grid = CreateGrid(animations);
        grid.Pointer(InputAction.Pressed, 100);
        grid.Pointer(InputAction.Moved, 111);
        grid.Pointer(InputAction.Moved, 191); // 40 px of pull
        Assert.True(grid.Input.PullToRefresh.IsVisible);

        Assert.Equal(0, MeasureSteadyState(grid.Render));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheSpinner_WhileRefreshing_AllocatesNothingPerFrame(bool animations)
    {
        var grid = CreateGrid(animations);
        grid.Input.IsRefreshing = true;

        Assert.Equal(0, MeasureSteadyState(ctx =>
        {
            grid.Input.UpdateAnimations(16);
            grid.Render(ctx);
        }));
        Assert.True(grid.Input.PullToRefresh.IsVisible);
    }

    [Fact]
    public void TheIndicatorSlidingAway_AllocatesNothingPerFrame()
    {
        var grid = CreateGrid(animations: true);
        grid.Input.IsRefreshing = true;
        for (int i = 0; i < 30; i++) grid.Input.UpdateAnimations(16);
        grid.Input.IsRefreshing = false;

        // The clock is held (0 ms steps), so every measured frame draws the indicator on its way out
        Assert.Equal(0, MeasureSteadyState(ctx =>
        {
            grid.Input.UpdateAnimations(0);
            grid.Render(ctx);
        }));
        Assert.True(grid.Input.PullToRefresh.IsTransitioning);
    }
}

using System.Collections.ObjectModel;
using System.Collections.Concurrent;
using System.Reflection;
using KumikoUI.Core;
using KumikoUI.Core.Models;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace KumikoUI.Maui.Tests;

/// <summary>
/// The MAUI grid's busy indicator (#327): <see cref="KumikoDataGrid.IsBusy"/> reaches the renderer, a background rebuild
/// turns it on by itself after <see cref="KumikoDataGrid.BusyIndicatorDelay"/> (never for a fast one), and frames are
/// requested only while the indicator animates.
/// </summary>
[Collection("BusyGrid")]
public class KumikoDataGridBusyTests
{
    private sealed record Row(int Id, int Quantity);

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    /// <summary>A UI-thread stand-in: posted callbacks wait in a queue until the test pumps them.</summary>
    private sealed class QueueContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _queue = new();

        public override void Post(SendOrPostCallback d, object? state) => _queue.Enqueue((d, state));

        /// <summary>Runs the queued callbacks, including those the builds post later, until <paramref name="done"/>.</summary>
        public void PumpUntil(Func<bool> done)
        {
            var deadline = DateTime.UtcNow + Timeout;
            while (!done())
            {
                if (_queue.TryDequeue(out var item)) item.Callback(item.State);
                else Thread.Sleep(1);
                Assert.True(DateTime.UtcNow < deadline, "timed out waiting for the rebuild");
            }
        }
    }

    private static KumikoRenderer RendererOf(KumikoDataGrid grid) =>
        (KumikoRenderer)typeof(KumikoDataGrid).GetField("_renderer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(grid)!;

    private static KumikoDataGrid NewGrid(int rows)
    {
        var grid = new KumikoDataGrid
        {
            ItemsSource = new ObservableCollection<Row>(Enumerable.Range(0, rows).Select(i => new Row(i, (int)(i * 2654435761L % 1000)))),
            RenderMode = KumikoRenderMode.Software,
            AnnounceChanges = false, // the screen reader has no implementation in the test host
        };
        grid.Columns.Add(new KumikoColumn { Header = "Id", PropertyName = nameof(Row.Id), Width = 100 });
        grid.Columns.Add(new KumikoColumn { Header = "Quantity", PropertyName = nameof(Row.Quantity), Width = 100 });
        return grid;
    }

    private static void Paint(KumikoDataGrid grid)
    {
        using var surface = SKSurface.Create(new SKImageInfo(300, 300, SKColorType.Rgba8888, SKAlphaType.Premul));
        var view = Assert.IsType<SKCanvasView>(((Grid)grid.Content).Children[0]);
        ((ISKCanvasView)view).OnPaintSurface(new SKPaintSurfaceEventArgs(surface,
            new SKImageInfo(300, 300, SKColorType.Rgba8888, SKAlphaType.Premul)));
    }

    /// <summary>Lets transitions play, as attaching the grid to a handler does when the platform has no reduced-motion setting.</summary>
    private static void EnableTransitions(KumikoDataGrid grid) =>
        ((KumikoUI.Core.Input.KumikoInputController)typeof(KumikoDataGrid).GetField("_inputController",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(grid)!).AnimationsEnabled = true;

    private static IDispatcherTimer? IndicatorTimer(KumikoDataGrid grid) =>
        (IDispatcherTimer?)typeof(KumikoDataGrid).GetField("_busyIndicatorTimer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(grid);

    [Fact]
    public void Defaults()
    {
        var grid = NewGrid(5);

        Assert.False(grid.IsBusy);
        Assert.True(grid.ShowBusyDuringViewUpdate);
        Assert.Equal(TimeSpan.FromMilliseconds(300), grid.BusyIndicatorDelay);
        Assert.False(RendererOf(grid).IsBusy);
    }

    [Fact]
    public void IsBusy_ReachesTheRenderer_OnAndOff()
    {
        var grid = NewGrid(5);

        grid.IsBusy = true;
        Assert.True(RendererOf(grid).IsBusy);
        grid.IsBusy = false;
        Assert.False(RendererOf(grid).IsBusy);
    }

    [Fact]
    public void WhileTheIndicatorAnimates_AFrameIsRequestedAtItsPace_AndStopsWhenItClears()
    {
        var grid = NewGrid(5);
        EnableTransitions(grid);
        Paint(grid);
        Assert.Null(IndicatorTimer(grid)); // idle: no timer, no frames

        grid.IsBusy = true;
        Paint(grid);
        var timer = IndicatorTimer(grid)!;
        Assert.True(timer.IsRunning);
        Assert.Equal(KumikoRenderer.BusyFrameIntervalMs, timer.Interval.TotalMilliseconds);

        grid.IsBusy = false;
        Paint(grid);
        Assert.False(IndicatorTimer(grid)!.IsRunning);
    }

    [Fact]
    public void WithAnimationsOff_TheIndicatorIsDrawnStill_AndNoFramesAreRequested()
    {
        var grid = NewGrid(5);
        EnableTransitions(grid);
        grid.GridStyle.EnableAnimations = false;

        grid.IsBusy = true;
        Paint(grid);

        Assert.True(RendererOf(grid).IsBusy);
        Assert.False(RendererOf(grid).IsBusyIndicatorAnimating);
        Assert.Null(IndicatorTimer(grid));
    }

    [Fact]
    public void ABackgroundRebuild_TurnsTheIndicatorOnAfterTheDelayOnly()
    {
        var context = new QueueContext();
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            var grid = NewGrid(300);
            grid.BackgroundRebuildThreshold = 100;
            context.PumpUntil(() => !grid.DataSource.IsViewUpdatePending);
            grid.BusyIndicatorDelay = TimeSpan.FromHours(1); // the delay timer never fires in this test

            grid.DataSource.ToggleSort(grid.DataSource.Columns[1]);
            Assert.True(grid.DataSource.IsViewUpdatePending);
            Assert.False(RendererOf(grid).IsBusy, "a rebuild shorter than the delay never shows the indicator");

            context.PumpUntil(() => !grid.DataSource.IsViewUpdatePending);
            Assert.False(RendererOf(grid).IsBusy);

            grid.BusyIndicatorDelay = TimeSpan.Zero;
            grid.DataSource.ToggleSort(grid.DataSource.Columns[1]);
            Assert.True(RendererOf(grid).IsBusy, "with no delay the indicator shows as soon as the rebuild is pending");

            context.PumpUntil(() => !grid.DataSource.IsViewUpdatePending);
            Assert.False(RendererOf(grid).IsBusy, "and goes when the new view is in");
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    [Fact]
    public void WithShowBusyDuringViewUpdateOff_ARebuildNeverShowsTheIndicator_ButIsBusyStillDoes()
    {
        var context = new QueueContext();
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            var grid = NewGrid(300);
            grid.BackgroundRebuildThreshold = 100;
            grid.BusyIndicatorDelay = TimeSpan.Zero;
            grid.ShowBusyDuringViewUpdate = false;
            context.PumpUntil(() => !grid.DataSource.IsViewUpdatePending);

            grid.DataSource.ToggleSort(grid.DataSource.Columns[1]);
            Assert.True(grid.DataSource.IsViewUpdatePending);
            Assert.False(RendererOf(grid).IsBusy);

            grid.IsBusy = true;
            Assert.True(RendererOf(grid).IsBusy);
            context.PumpUntil(() => !grid.DataSource.IsViewUpdatePending);
            Assert.True(RendererOf(grid).IsBusy, "the app's flag outlives the rebuild");
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }
}

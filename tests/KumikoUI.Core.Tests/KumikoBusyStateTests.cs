using KumikoUI.Core.Models;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests;

/// <summary>
/// The grid's effective busy state (#327): the app's flag, or a pending background rebuild or an initial load that lasts
/// longer than the delay.
/// </summary>
[Collection(nameof(BackgroundRebuildCollection))]
public class KumikoBusyStateTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    /// <summary>Timers a test fires by hand.</summary>
    private sealed class FakeTimers
    {
        public List<(TimeSpan Delay, Action Elapsed, bool Canceled)> Started { get; } = new();

        public Action Start(TimeSpan delay, Action elapsed)
        {
            int index = Started.Count;
            Started.Add((delay, elapsed, false));
            return () => Started[index] = Started[index] with { Canceled = true };
        }

        public void FireLast()
        {
            var (_, elapsed, canceled) = Started[^1];
            if (!canceled) elapsed();
        }
    }

    private sealed record Item(int Id, int Quantity);

    private static KumikoSource NewSource(int count)
    {
        var source = new KumikoSource { BackgroundRebuildThreshold = 100 };
        source.SetColumns(new[]
        {
            new KumikoColumn { Header = "Id", PropertyName = nameof(Item.Id), ColumnType = KumikoColumnType.Numeric },
            new KumikoColumn { Header = "Quantity", PropertyName = nameof(Item.Quantity), ColumnType = KumikoColumnType.Numeric },
        });
        source.SetItems(Enumerable.Range(0, count).Select(i => new Item(i, (int)(i * 2654435761L % 1000))).ToList());
        return source;
    }

    [Fact]
    public void TheAppsFlag_ShowsAtOnce_AndHidesWhenCleared()
    {
        var timers = new FakeTimers();
        using var state = new KumikoBusyState(timers.Start);
        int changes = 0;
        state.Changed += (_, _) => changes++;

        state.Manual = true;
        Assert.True(state.IsBusy);
        state.Manual = true;
        state.Manual = false;

        Assert.False(state.IsBusy);
        Assert.Equal(2, changes);
        Assert.Empty(timers.Started);
    }

    [Fact]
    public async Task APendingRebuild_ShowsOnlyAfterTheDelay()
    {
        using var ui = new TestUiThread();
        var timers = new FakeTimers();
        var source = ui.Invoke(() => NewSource(2000));
        await ui.Invoke(source.WhenViewUpdatedAsync).WaitAsync(Timeout);
        var state = ui.Invoke(() =>
        {
            var s = new KumikoBusyState(timers.Start);
            s.Attach(source);
            return s;
        });

        // All in the request's own turn: a fast build's result is posted behind the next queued call
        var (before, after) = ui.Invoke(() =>
        {
            source.ToggleSort(source.Columns[1]);
            bool beforeDelay = state.IsBusy;
            timers.FireLast();
            return (beforeDelay, state.IsBusy);
        });
        Assert.False(before);
        Assert.Equal(KumikoBusyState.DefaultDelay, Assert.Single(timers.Started).Delay);
        Assert.True(after);

        await ui.Invoke(source.WhenViewUpdatedAsync).WaitAsync(Timeout);
        Assert.False(ui.Invoke(() => state.IsBusy));
    }

    [Fact]
    public async Task AFastRebuild_NeverShows()
    {
        using var ui = new TestUiThread();
        var timers = new FakeTimers();
        var source = ui.Invoke(() => NewSource(2000));
        await ui.Invoke(source.WhenViewUpdatedAsync).WaitAsync(Timeout);
        var changes = 0;
        var state = ui.Invoke(() =>
        {
            var s = new KumikoBusyState(timers.Start);
            s.Changed += (_, _) => changes++;
            s.Attach(source);
            return s;
        });

        ui.Invoke(() => source.ToggleSort(source.Columns[1]));
        await ui.Invoke(source.WhenViewUpdatedAsync).WaitAsync(Timeout);
        ui.Invoke(timers.FireLast); // the delay's timer was canceled when the view published

        Assert.False(ui.Invoke(() => state.IsBusy));
        Assert.Equal(0, changes);
        Assert.True(Assert.Single(timers.Started).Canceled);
    }

    [Fact]
    public async Task ANonPositiveDelay_ShowsAtOnce()
    {
        using var ui = new TestUiThread();
        var timers = new FakeTimers();
        var source = ui.Invoke(() => NewSource(2000));
        await ui.Invoke(source.WhenViewUpdatedAsync).WaitAsync(Timeout);
        var state = ui.Invoke(() =>
        {
            var s = new KumikoBusyState(timers.Start) { Delay = TimeSpan.Zero };
            s.Attach(source);
            return s;
        });

        bool busy = ui.Invoke(() =>
        {
            source.ToggleSort(source.Columns[1]);
            return state.IsBusy;
        });

        Assert.True(busy);
        Assert.Empty(timers.Started);
        await ui.Invoke(source.WhenViewUpdatedAsync).WaitAsync(Timeout);
        Assert.False(ui.Invoke(() => state.IsBusy));
    }

    [Fact]
    public async Task WithTheAutomaticModeOff_OnlyTheAppsFlagShows()
    {
        using var ui = new TestUiThread();
        var timers = new FakeTimers();
        var source = ui.Invoke(() => NewSource(2000));
        await ui.Invoke(source.WhenViewUpdatedAsync).WaitAsync(Timeout);
        var state = ui.Invoke(() =>
        {
            var s = new KumikoBusyState(timers.Start) { ShowDuringViewUpdate = false, Delay = TimeSpan.Zero };
            s.Attach(source);
            return s;
        });

        ui.Invoke(() => source.ToggleSort(source.Columns[1]));
        Assert.False(ui.Invoke(() => state.IsBusy));
        await ui.Invoke(source.WhenViewUpdatedAsync).WaitAsync(Timeout);

        state.Manual = true;
        Assert.True(state.IsBusy);
        Assert.Empty(timers.Started);
    }

    [Fact]
    public void AnInitialLoad_ShowsAfterTheDelay_AndEndsWithTheLoad()
    {
        var timers = new FakeTimers();
        using var state = new KumikoBusyState(timers.Start);

        state.InitialLoad = true;
        Assert.False(state.IsBusy);
        timers.FireLast();
        Assert.True(state.IsBusy);

        state.InitialLoad = false;
        Assert.False(state.IsBusy);
    }

    [Fact]
    public void AnInitialLoad_WhileTheAppsFlagIsSet_StaysBusyAfterItEnds()
    {
        var timers = new FakeTimers();
        using var state = new KumikoBusyState(timers.Start) { Manual = true };

        state.InitialLoad = true;
        state.InitialLoad = false;

        Assert.True(state.IsBusy);
    }

    [Fact]
    public void Dispose_CancelsTheDelay_AndStopsFollowingTheSource()
    {
        var timers = new FakeTimers();
        var state = new KumikoBusyState(timers.Start);
        state.InitialLoad = true;

        state.Dispose();
        timers.FireLast();

        Assert.True(timers.Started[0].Canceled);
        Assert.False(state.IsBusy);
    }
}

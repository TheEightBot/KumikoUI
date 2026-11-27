using KumikoUI.Core.Data;
using KumikoUI.Core.Models;
using KumikoUI.Core.Tests.IncrementalLoading;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.EmptyView;

/// <summary>When the empty view (#326) steps aside: a load-more row in the data area, a background rebuild that empties a view.</summary>
public class EmptyViewInteractionTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    private static bool Shown(RecordingDrawingContext ctx, KumikoStyle style) =>
        ctx.Ops.Any(o => o.Kind == DrawOpKind.TextInRect && o.Color == style.EmptyViewTextColor);

    // ── Incremental loading (#318) ──

    [Fact]
    public void FirstBatchLoading_DrawsNoEmptyView_UntilTheBatchEndsEmpty()
    {
        var grid = new LoadMoreGrid();
        grid.Frame(); // requests the first batch: the loading row shows
        Assert.Equal(KumikoLoadMoreState.Loading, grid.Loader.State);
        Assert.False(Shown(grid.Render(), grid.Style));

        grid.Fake.Complete(0, hasMore: false);
        grid.Frame(); // the loader settles after the next frame
        grid.Frame();

        Assert.Equal(KumikoLoadMoreState.Completed, grid.Loader.State);
        Assert.True(Shown(grid.Render(), grid.Style));
    }

    [Fact]
    public void ALoadMoreButtonOrAFailedBatch_DrawsNoEmptyView()
    {
        var manual = new LoadMoreGrid(configure: g => g.Loader.Mode = KumikoLoadMoreMode.Manual);
        Assert.False(Shown(manual.Frame(), manual.Style));

        var failing = new LoadMoreGrid();
        failing.Frame();
        failing.Fake.Fail(new InvalidOperationException("offline"));
        failing.Pump();
        Assert.False(Shown(failing.Render(), failing.Style));
    }

    // ── A pending background rebuild (#254) ──

    [Fact]
    public async Task APendingRebuild_ThatStartedFromRows_DoesNotFlashTheEmptyView()
    {
        using var ui = new TestUiThread();
        using var gate = new ManualResetEventSlim();
        var h = ui.Invoke(() => RenderHarness.Create(rows: 20, configure: x => x.Source.BackgroundRebuildThreshold = 5));
        Assert.False(ui.Invoke(() => Shown(h.Render(), h.Style))); // rows drawn

        ui.Invoke(() => h.Source.SetFilter(_ => false));
        await ui.Invoke(h.Source.WhenViewUpdatedAsync).WaitAsync(Timeout);
        Assert.Equal(0, ui.Invoke(() => h.Source.RowCount));

        // A newer rebuild is pending while the empty view is the published one: no message in between
        ui.Invoke(() => h.Source.SetFilter(_ => { gate.Wait(Timeout); return false; }));
        Assert.True(ui.Invoke(() => h.Source.IsViewUpdatePending));
        Assert.False(ui.Invoke(() => Shown(h.Render(), h.Style)));

        gate.Set();
        await ui.Invoke(h.Source.WhenViewUpdatedAsync).WaitAsync(Timeout);
        Assert.True(ui.Invoke(() => Shown(h.Render(), h.Style)));
    }

    [Fact]
    public async Task APendingRebuild_OfAnEmptyView_StillShowsTheEmptyView()
    {
        using var ui = new TestUiThread();
        using var gate = new ManualResetEventSlim();
        var h = ui.Invoke(() => RenderHarness.Create(rows: 20, configure: x =>
        {
            x.Source.BackgroundRebuildThreshold = 5;
            x.Source.SetFilter(_ => false);
        }));
        await ui.Invoke(h.Source.WhenViewUpdatedAsync).WaitAsync(Timeout);
        Assert.True(ui.Invoke(() => Shown(h.Render(), h.Style)));

        ui.Invoke(() => h.Source.SetFilter(_ => { gate.Wait(Timeout); return false; }));
        Assert.True(ui.Invoke(() => h.Source.IsViewUpdatePending));
        Assert.True(ui.Invoke(() => Shown(h.Render(), h.Style))); // the previous frame was empty too

        gate.Set();
        await ui.Invoke(h.Source.WhenViewUpdatedAsync).WaitAsync(Timeout);
    }
}

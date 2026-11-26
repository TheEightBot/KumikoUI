using System.Collections.ObjectModel;
using KumikoUI.Core.Data;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests.IncrementalLoading;

/// <summary>
/// The incremental loader's state machine (#318, spec 104 §5.2 to §7): when a batch starts, that only one runs, when it
/// finishes, how it fails, and what cancels it.
/// </summary>
public class IncrementalLoaderTests
{
    // ── Detection and precedence ──

    [Fact]
    public void SetItems_WithItemsThatAreIncremental_UsesThem()
    {
        var items = new KumikoIncrementalCollection<LoadItem>((_, _, _) => Task.FromResult<IReadOnlyList<LoadItem>>(Array.Empty<LoadItem>()));
        var source = new KumikoSource();

        source.SetItems(items);

        Assert.Same(items, source.IncrementalLoading.Source);
    }

    [Fact]
    public void IncrementalSource_TakesPrecedenceOverIncrementalItems()
    {
        var items = new KumikoIncrementalCollection<LoadItem>((_, _, _) => Task.FromResult<IReadOnlyList<LoadItem>>(Array.Empty<LoadItem>()));
        var source = new KumikoSource();
        source.SetItems(items);
        var explicitSource = new FakeIncrementalSource(new ObservableCollection<LoadItem>());

        source.IncrementalSource = explicitSource;
        Assert.Same(explicitSource, source.IncrementalLoading.Source);

        source.IncrementalSource = null;
        Assert.Same(items, source.IncrementalLoading.Source);
    }

    [Fact]
    public void HostSource_IsUsedOnlyWithoutAnExplicitOrItemsSource()
    {
        var grid = new LoadMoreGrid(attachSource: false);
        var host = new FakeIncrementalSource(grid.Items);

        grid.Loader.HostSource = host;
        Assert.Same(host, grid.Loader.Source);

        grid.Source.IncrementalSource = grid.Fake;
        Assert.Same(grid.Fake, grid.Loader.Source);
    }

    [Fact]
    public void WithoutASource_NothingLoadsAndNoRowIsDrawn()
    {
        var grid = new LoadMoreGrid(attachSource: false);

        grid.Frame();

        Assert.Empty(grid.Requests);
        Assert.Equal(KumikoLoadMoreState.Idle, grid.Loader.State);
        Assert.False(grid.Loader.IsPartial);
        Assert.Equal(0, grid.Loader.LoadMoreAsync().Result);
    }

    // ── Automatic loading ──

    [Fact]
    public void FirstFrame_OfAnEmptyGrid_RequestsTheFirstBatch()
    {
        var grid = new LoadMoreGrid();

        grid.Frame();

        var request = Assert.Single(grid.Requests);
        Assert.Equal(KumikoLoadMoreTrigger.NearEnd, request.Trigger);
        Assert.Equal(20, request.Count); // auto: max(20, 2 x 10 rows per viewport)
        Assert.Equal(KumikoLoadMoreState.Loading, grid.Loader.State);
        Assert.Single(grid.Fake.Calls);
        Assert.Equal(new[] { "State:Loading", "Requested:NearEnd" }, grid.Events);
    }

    [Fact]
    public void ObserveFrame_NeverStartsABatchInsideThePaint()
    {
        var grid = new LoadMoreGrid();

        grid.Render();

        Assert.Empty(grid.Fake.Calls);
        Assert.Single(grid.Posted);
    }

    [Fact]
    public void ManyFrames_WhileLoading_StartOneBatch()
    {
        var grid = new LoadMoreGrid();

        for (int i = 0; i < 10; i++) grid.Frame();

        Assert.Single(grid.Fake.Calls);
    }

    [Fact]
    public void BatchSize_IsPassedAsTheCount()
    {
        var grid = new LoadMoreGrid(configure: g => g.Loader.BatchSize = 7);

        grid.Frame();

        Assert.Equal(7, grid.Fake.Last.Count);
    }

    [Fact]
    public void ACompletedBatch_StaysLoadingUntilTheNextFrame_ThenRaisesCompleted()
    {
        var grid = new LoadMoreGrid();
        grid.Frame();

        grid.Fake.Complete(30);

        Assert.Equal(KumikoLoadMoreState.Loading, grid.Loader.State);
        Assert.Empty(grid.Completions);

        grid.Frame();

        Assert.Equal(KumikoLoadMoreState.Idle, grid.Loader.State);
        var completed = Assert.Single(grid.Completions);
        Assert.Equal(30, completed.ItemsLoaded);
        Assert.Equal(30, completed.RowsAdded);
        Assert.Equal(KumikoLoadMoreState.Idle, completed.State);
        Assert.Null(completed.Error);
    }

    [Fact]
    public void ABatchThatDoesNotFillTheViewport_IsFollowedByAnother()
    {
        var grid = new LoadMoreGrid();
        grid.Frame();
        grid.Fake.Complete(3);

        grid.Frame(); // finishes the first batch
        grid.Frame(); // the view is still short: the next batch

        Assert.Equal(2, grid.Fake.Calls.Count);
    }

    [Fact]
    public void FarFromTheEnd_NothingLoads()
    {
        var grid = new LoadMoreGrid(rows: 100);

        grid.Frame();

        Assert.Empty(grid.Fake.Calls);
    }

    [Theory]
    // 100 rows, 10 fit: with row f at the top, the last row on screen is f + 10, so 89 - f rows remain after it
    [InlineData(-1, 78, false)]
    [InlineData(-1, 79, true)]  // one viewport (10 rows) ahead
    [InlineData(0, 88, false)]
    [InlineData(0, 89, true)]   // the last row comes into view
    [InlineData(5, 83, false)]
    [InlineData(5, 84, true)]
    public void Threshold_IsTheRowsRemainingAfterTheLastRowOnScreen(int threshold, int firstRow, bool loads)
    {
        var grid = new LoadMoreGrid(rows: 100, configure: g => g.Loader.Threshold = threshold);
        grid.Render();

        grid.Scroll.OffsetY = firstRow * grid.Style.RowHeight;
        grid.Frame();

        Assert.Equal(loads, grid.Fake.Calls.Count == 1);
    }

    [Fact]
    public void Threshold_BelowMinusOne_Throws()
    {
        var grid = new LoadMoreGrid();
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.Loader.Threshold = -2);
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.Loader.BatchSize = -1);
    }

    [Fact]
    public void FrozenRows_DoNotCountAsRemainingRows()
    {
        var grid = new LoadMoreGrid(rows: 12, configure: g => g.Source.FrozenRowCount = 12);

        grid.Frame();

        Assert.Single(grid.Fake.Calls); // every row is frozen and on screen
    }

    [Fact]
    public void ModeNone_AndManual_NeverLoadOnTheirOwn()
    {
        foreach (var mode in new[] { KumikoLoadMoreMode.None, KumikoLoadMoreMode.Manual })
        {
            var grid = new LoadMoreGrid(configure: g => g.Loader.Mode = mode);
            grid.Frame();
            grid.Frame();
            Assert.Empty(grid.Fake.Calls);
        }
    }

    [Fact]
    public void AnUnproductiveBatch_StopsAutomaticLoading_UntilTheViewChanges()
    {
        // A filter hides every new item: the batch adds no row
        var grid = new LoadMoreGrid(configure: g => g.Source.SetFilter(item => ((LoadItem)item).Value < 0));
        grid.Frame();
        grid.Fake.Complete(20);
        grid.Frame(); // finishes: 0 rows added

        Assert.Equal(0, grid.Completions[0].RowsAdded);
        grid.Frame();
        grid.Frame();
        Assert.Single(grid.Fake.Calls);

        // The user changes the filter: the view changed for another reason, so loading resumes
        grid.Source.SetFilter(item => ((LoadItem)item).Value < 5);
        grid.Frame();
        grid.Frame();
        Assert.Equal(2, grid.Fake.Calls.Count);
    }

    [Fact]
    public void AnUnproductiveBatch_IsReArmedByAScrollDown()
    {
        var grid = new LoadMoreGrid(rows: 40, configure: g => g.Loader.Threshold = 0);
        grid.ScrollToEnd();
        grid.Frame();
        grid.Fake.Complete(0);
        grid.Frame();
        grid.Frame();
        Assert.Single(grid.Fake.Calls);

        grid.Scroll.OffsetY -= 50;
        grid.Frame(); // scrolling up does not re-arm (and the end is out of reach)
        grid.Scroll.OffsetY += 50;
        grid.Frame();
        grid.Frame();

        Assert.Equal(2, grid.Fake.Calls.Count);
    }

    [Fact]
    public void AnUnproductiveBatch_IsReArmedByATallerViewport()
    {
        var grid = new LoadMoreGrid();
        grid.Frame();
        grid.Fake.Complete(0);
        grid.Frame();
        grid.Frame();

        grid.Scroll.ViewportHeight += 100;
        grid.Frame();
        grid.Frame();

        Assert.Equal(2, grid.Fake.Calls.Count);
    }

    // ── Completion ──

    [Fact]
    public void HasMoreItemsFalse_AtBind_CompletesWithoutLoading()
    {
        var grid = new LoadMoreGrid(configure: g => g.Fake.HasMoreItems = false);

        grid.Frame();

        Assert.Empty(grid.Fake.Calls);
        Assert.Equal(KumikoLoadMoreState.Completed, grid.Loader.State);
        Assert.False(grid.Loader.IsPartial);
    }

    [Fact]
    public void TheLastBatch_Completes()
    {
        var grid = new LoadMoreGrid();
        grid.Frame();
        grid.Fake.Complete(5, hasMore: false);

        grid.Frame();

        Assert.Equal(KumikoLoadMoreState.Completed, grid.Loader.State);
        Assert.Equal(KumikoLoadMoreState.Completed, grid.Completions[0].State);
    }

    [Fact]
    public void Completed_IsNotLeftByPolling_OnlyByANotificationOrAReset()
    {
        var grid = new LoadMoreGrid(configure: g => g.Fake.HasMoreItems = false);
        grid.Frame();

        grid.Fake.HasMoreItems = true; // silently
        grid.Frame();
        grid.Frame();
        Assert.Equal(KumikoLoadMoreState.Completed, grid.Loader.State);

        grid.Fake.NotifyHasMoreItems(true);
        Assert.Equal(KumikoLoadMoreState.Idle, grid.Loader.State);
        grid.Frame();
        Assert.Single(grid.Fake.Calls);
    }

    [Fact]
    public void Reset_LeavesCompleted()
    {
        var grid = new LoadMoreGrid(configure: g => g.Fake.HasMoreItems = false);
        grid.Frame();
        grid.Fake.HasMoreItems = true;

        grid.Loader.Reset();
        grid.Frame();

        Assert.Single(grid.Fake.Calls);
    }

    // ── Failure ──

    [Fact]
    public void AFailedBatch_IsFailed_AndNeverRetriedAutomatically()
    {
        var grid = new LoadMoreGrid();
        grid.Frame();
        var error = new InvalidOperationException("offline");

        grid.Fake.Fail(error);

        Assert.Equal(KumikoLoadMoreState.Failed, grid.Loader.State);
        Assert.Same(error, grid.Loader.LastError);
        var completed = Assert.Single(grid.Completions);
        Assert.Same(error, completed.Error);
        Assert.Equal(KumikoLoadMoreState.Failed, completed.State);
        for (int i = 0; i < 5; i++) grid.Frame();
        Assert.Single(grid.Fake.Calls);
    }

    [Fact]
    public void LoadMoreAsync_AfterAFailure_Retries()
    {
        var grid = new LoadMoreGrid();
        grid.Frame();
        grid.Fake.Fail(new InvalidOperationException());

        var retry = grid.Loader.LoadMoreAsync();

        Assert.Equal(2, grid.Fake.Calls.Count);
        Assert.Equal(KumikoLoadMoreState.Loading, grid.Loader.State);
        Assert.Null(grid.Loader.LastError);
        Assert.Equal(KumikoLoadMoreTrigger.Programmatic, grid.Requests[^1].Trigger);
        grid.Fake.Complete(4);
        Assert.Equal(4, retry.Result);
    }

    [Fact]
    public void ACompletedHandler_SetsTheErrorText()
    {
        var grid = new LoadMoreGrid();
        grid.Loader.LoadMoreCompleted += (_, e) => e.ErrorText = "No connection";
        grid.Frame();

        grid.Fake.Fail(new HttpRequestException());

        Assert.Equal("No connection", grid.Loader.ErrorText);
    }

    [Fact]
    public void AKumikoLoadMoreException_GivesTheErrorText()
    {
        var grid = new LoadMoreGrid();
        grid.Frame();

        grid.Fake.Fail(new KumikoLoadMoreException("Server busy"));

        Assert.Equal("Server busy", grid.Loader.ErrorText);
        Assert.Equal("Server busy", grid.Completions[0].ErrorText);
    }

    [Fact]
    public void AnOperationCanceledException_FromTheAppsOwnToken_Fails()
    {
        var grid = new LoadMoreGrid();
        grid.Frame();

        grid.Fake.Fail(new TaskCanceledException("HttpClient timeout"));

        Assert.Equal(KumikoLoadMoreState.Failed, grid.Loader.State);
    }

    [Fact]
    public void ASourceThatThrowsSynchronously_Fails()
    {
        var grid = new LoadMoreGrid();
        grid.Fake.Respond = (_, _) => throw new InvalidOperationException("sync");

        grid.Frame();

        Assert.Equal(KumikoLoadMoreState.Failed, grid.Loader.State);
    }

    [Fact]
    public void ARequestedHandlerThatThrows_FailsTheBatch()
    {
        var grid = new LoadMoreGrid();
        grid.Loader.LoadMoreRequested += (_, _) => throw new InvalidOperationException("handler");

        grid.Frame();

        Assert.Equal(KumikoLoadMoreState.Failed, grid.Loader.State);
        Assert.Empty(grid.Fake.Calls);
    }

    // ── One in flight ──

    [Fact]
    public void LoadMoreAsync_WhileLoading_JoinsTheBatchInFlight()
    {
        var grid = new LoadMoreGrid();
        grid.Frame();

        var joined = grid.Loader.LoadMoreAsync();
        grid.Fake.Complete(8);

        Assert.Single(grid.Fake.Calls);
        Assert.Equal(8, joined.Result);
    }

    [Fact]
    public async Task LoadMoreAsync_ItsTokenCancelsOnlyTheWait()
    {
        var grid = new LoadMoreGrid();
        grid.Frame();
        using var cts = new CancellationTokenSource();

        var wait = grid.Loader.LoadMoreAsync(cts.Token);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
        Assert.False(grid.Fake.Last.Token.IsCancellationRequested);
        Assert.Equal(KumikoLoadMoreState.Loading, grid.Loader.State);
    }

    [Fact]
    public void ASynchronousSource_LoadsOneBatchPerFrame_WithoutRecursion()
    {
        var grid = new LoadMoreGrid();
        grid.Fake.Respond = (count, _) =>
        {
            grid.Fake.Append(2);
            return Task.FromResult(2);
        };

        grid.Frame(); // starts and completes a batch
        Assert.Single(grid.Fake.Calls);
        grid.Frame(); // finishes it
        grid.Frame(); // still short: the next one
        Assert.Equal(2, grid.Fake.Calls.Count);
    }

    [Fact]
    public void ABatchArrivingInsideBeginUpdate_FinishesOnlyAfterEndUpdate()
    {
        var grid = new LoadMoreGrid();
        grid.Frame();

        var scope = grid.Source.BeginUpdate();
        grid.Fake.Complete(30);
        grid.Frame();
        Assert.Equal(KumikoLoadMoreState.Loading, grid.Loader.State);

        scope.Dispose();
        grid.Frame();
        Assert.Equal(KumikoLoadMoreState.Idle, grid.Loader.State);
        Assert.Equal(30, grid.Completions[0].RowsAdded);
    }

    // ── Cancellation ──

    public static TheoryData<string> CancellationSources => new() { "SetItems", "Reset", "IncrementalSource", "Detach", "Dispose", "LoaderReset" };

    [Theory]
    [MemberData(nameof(CancellationSources))]
    public void ACancellation_CancelsTheToken_AndALateCompletionIsIgnored(string what)
    {
        var grid = new LoadMoreGrid();
        grid.Frame();
        var call = grid.Fake.Last;
        grid.Events.Clear();

        switch (what)
        {
            case "SetItems": grid.Source.SetItems(new ObservableCollection<LoadItem>()); break;
            case "Reset": grid.Items.Clear(); break;
            case "IncrementalSource": grid.Source.IncrementalSource = new FakeIncrementalSource(grid.Items); break;
            case "Detach": grid.Source.Detach(); break;
            case "Dispose": grid.Source.Dispose(); break;
            case "LoaderReset": grid.Loader.Reset(); break;
        }

        Assert.True(call.Token.IsCancellationRequested);
        Assert.Equal(KumikoLoadMoreState.Idle, grid.Loader.State);
        grid.Events.Clear();

        call.Completion.SetResult(12); // late
        grid.Frame();
        Assert.Empty(grid.Completions);
        Assert.DoesNotContain(grid.Events, e => e.StartsWith("Completed", StringComparison.Ordinal));
    }

    [Fact]
    public void ALateCancellation_IsIgnored_NotAFailure()
    {
        var grid = new LoadMoreGrid();
        grid.Frame();
        var call = grid.Fake.Last;

        grid.Loader.Reset();
        call.Completion.SetCanceled(call.Token);

        Assert.Equal(KumikoLoadMoreState.Idle, grid.Loader.State);
        Assert.Null(grid.Loader.LastError);
    }

    [Fact]
    public void ModeNone_DoesNotCancelTheBatchInFlight()
    {
        var grid = new LoadMoreGrid();
        grid.Frame();

        grid.Loader.Mode = KumikoLoadMoreMode.None;
        grid.Fake.Complete(30);
        grid.Frame();

        Assert.False(grid.Fake.Last.Token.IsCancellationRequested);
        Assert.Single(grid.Completions);
    }

    [Fact]
    public void AClientSortChange_DoesNotCancelTheBatchInFlight()
    {
        var grid = new LoadMoreGrid();
        grid.Frame();

        grid.Source.SetSort(grid.Value, KumikoSortDirection.Descending);

        Assert.False(grid.Fake.Last.Token.IsCancellationRequested);
        Assert.Equal(KumikoLoadMoreState.Loading, grid.Loader.State);
    }

    [Fact]
    public void Attach_AfterDetach_EvaluatesAgainOnTheNextFrame()
    {
        var grid = new LoadMoreGrid();
        grid.Frame();
        grid.Source.Detach();
        grid.Frame(); // detached: nothing

        Assert.Single(grid.Fake.Calls);

        grid.Source.Attach();
        grid.Frame();
        Assert.Equal(2, grid.Fake.Calls.Count);
    }

    [Fact]
    public void Dispose_LeavesAnInertLoader()
    {
        var grid = new LoadMoreGrid();
        grid.Source.Dispose();

        grid.Frame();

        Assert.Empty(grid.Fake.Calls);
        Assert.Null(grid.Loader.Source);
        Assert.Equal(0, grid.Loader.LoadMoreAsync().Result);
    }

    // ── Handlers may change things ──

    [Fact]
    public void ARequestedHandler_ThatResets_StopsTheBatch()
    {
        var grid = new LoadMoreGrid();
        grid.Loader.LoadMoreRequested += (_, _) => grid.Loader.Reset();

        grid.Frame();

        Assert.Empty(grid.Fake.Calls);
        Assert.Equal(KumikoLoadMoreState.Idle, grid.Loader.State);
    }

    [Fact]
    public void ACompletedHandler_MayStartTheNextBatch()
    {
        var grid = new LoadMoreGrid(rows: 100);
        _ = grid.Loader.LoadMoreAsync();
        grid.Loader.LoadMoreCompleted += (_, e) =>
        {
            if (grid.Fake.Calls.Count == 1) _ = grid.Loader.LoadMoreAsync();
        };

        grid.Fake.Complete(5);
        grid.Frame();

        Assert.Equal(2, grid.Fake.Calls.Count);
        Assert.Equal(KumikoLoadMoreState.Loading, grid.Loader.State);
    }
}

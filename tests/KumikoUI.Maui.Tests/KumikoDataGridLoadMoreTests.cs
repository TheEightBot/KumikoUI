using System.Collections.ObjectModel;
using System.Windows.Input;
using KumikoUI.Core.Data;

namespace KumikoUI.Maui.Tests;

/// <summary>
/// The MAUI grid's load-more surface (#318, spec 104 §10): the bindable properties reach Core's loader, the state is
/// mirrored back without feedback, and <see cref="KumikoDataGrid.LoadMoreCommand"/> is adapted (the
/// <c>IsLoadingMore</c> / <c>LoadMoreErrorText</c> round trip).
/// </summary>
public class KumikoDataGridLoadMoreTests
{
    private sealed record Row(int Id);

    private sealed class PendingSource : IKumikoIncrementalSource
    {
        public TaskCompletionSource<int> Pending { get; private set; } = new();
        public int Calls { get; private set; }
        public bool HasMoreItems { get; set; } = true;

        public Task<int> LoadMoreItemsAsync(int count, CancellationToken cancellationToken)
        {
            Calls++;
            Pending = new TaskCompletionSource<int>();
            return Pending.Task;
        }
    }

    /// <summary>A command whose CanExecute and Execute the test controls.</summary>
    private sealed class TestCommand : ICommand
    {
        public bool Can { get; set; } = true;
        public List<object?> Executed { get; } = new();
        public Action<object?>? OnExecute { get; set; }
        public object? LastCanExecuteParameter { get; private set; }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            LastCanExecuteParameter = parameter;
            return Can;
        }

        public void Execute(object? parameter)
        {
            Executed.Add(parameter);
            OnExecute?.Invoke(parameter);
        }

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

        public int Subscribers => CanExecuteChanged?.GetInvocationList().Length ?? 0;
    }

    private static (KumikoDataGrid Grid, ObservableCollection<Row> Items) Grid()
    {
        var items = new ObservableCollection<Row>();
        var grid = new KumikoDataGrid { ItemsSource = items, AnnounceChanges = false };
        return (grid, items);
    }

    // ── Properties reach the loader ──

    [Fact]
    public void Defaults_MatchTheLoader()
    {
        var (grid, _) = Grid();
        var loader = grid.DataSource.IncrementalLoading;

        Assert.Equal(KumikoLoadMoreMode.Automatic, grid.LoadMoreMode);
        Assert.Equal(-1, grid.LoadMoreThreshold);
        Assert.Equal(0, grid.LoadMoreBatchSize);
        Assert.Equal(loader.Mode, grid.LoadMoreMode);
        Assert.Equal(loader.Threshold, grid.LoadMoreThreshold);
        Assert.Equal(KumikoLoadMoreState.Idle, grid.LoadMoreState);
        Assert.False(grid.IsLoadingMore);
    }

    [Fact]
    public void Properties_ReachTheLoader()
    {
        var (grid, _) = Grid();
        var source = new PendingSource();

        grid.LoadMoreMode = KumikoLoadMoreMode.Manual;
        grid.LoadMoreThreshold = 5;
        grid.LoadMoreBatchSize = 50;
        grid.IncrementalSource = source;

        var loader = grid.DataSource.IncrementalLoading;
        Assert.Equal(KumikoLoadMoreMode.Manual, loader.Mode);
        Assert.Equal(5, loader.Threshold);
        Assert.Equal(50, loader.BatchSize);
        Assert.Same(source, grid.DataSource.IncrementalSource);
        Assert.Same(source, loader.Source);
    }

    [Fact]
    public void AThresholdOrBatchSizeOutOfRange_IsClamped()
    {
        var (grid, _) = Grid();

        grid.LoadMoreThreshold = -5;
        grid.LoadMoreBatchSize = -1;

        Assert.Equal(-1, grid.DataSource.IncrementalLoading.Threshold);
        Assert.Equal(0, grid.DataSource.IncrementalLoading.BatchSize);
    }

    // ── State mirrored back ──

    [Fact]
    public void TheState_IsMirroredIntoTheProperties()
    {
        var (grid, _) = Grid();
        var source = new PendingSource();
        grid.IncrementalSource = source;

        _ = grid.LoadMoreAsync();
        Assert.True(grid.IsLoadingMore);
        Assert.Equal(KumikoLoadMoreState.Loading, grid.LoadMoreState);

        source.Pending.SetException(new KumikoLoadMoreException("Server busy"));
        Assert.False(grid.IsLoadingMore);
        Assert.Equal(KumikoLoadMoreState.Failed, grid.LoadMoreState);
        Assert.Equal("Server busy", grid.LoadMoreErrorText);

        _ = grid.LoadMoreAsync(); // retry clears the error
        Assert.Null(grid.LoadMoreErrorText);
        Assert.True(grid.IsLoadingMore);
    }

    [Fact]
    public void SettingTheErrorText_WhileFailed_ChangesTheRowsMessage()
    {
        var (grid, _) = Grid();
        var source = new PendingSource();
        grid.IncrementalSource = source;
        _ = grid.LoadMoreAsync();
        source.Pending.SetException(new InvalidOperationException());

        grid.LoadMoreErrorText = "Check your connection";

        Assert.Equal("Check your connection", grid.DataSource.IncrementalLoading.ErrorText);
    }

    [Fact]
    public void SettingIsLoadingMore_WithoutACommand_DoesNothing()
    {
        var (grid, _) = Grid();
        grid.IncrementalSource = new PendingSource();

        grid.IsLoadingMore = true;
        grid.IsLoadingMore = false;

        Assert.Equal(KumikoLoadMoreState.Idle, grid.LoadMoreState);
    }

    [Fact]
    public void TheEvents_AreReRaisedWithTheGridAsSender()
    {
        var (grid, _) = Grid();
        var source = new PendingSource();
        grid.IncrementalSource = source;
        object? requestedBy = null, completedBy = null;
        grid.LoadMoreRequested += (s, _) => requestedBy = s;
        grid.LoadMoreCompleted += (s, _) => completedBy = s;

        _ = grid.LoadMoreAsync();
        source.Pending.SetException(new InvalidOperationException());

        Assert.Same(grid, requestedBy);
        Assert.Same(grid, completedBy);
    }

    [Fact]
    public void ResetLoadMore_CancelsTheBatch()
    {
        var (grid, _) = Grid();
        grid.IncrementalSource = new PendingSource();
        _ = grid.LoadMoreAsync();

        grid.ResetLoadMore();

        Assert.Equal(KumikoLoadMoreState.Idle, grid.LoadMoreState);
        Assert.False(grid.IsLoadingMore);
    }

    // ── Command adapter ──

    [Fact]
    public async Task TheCommand_IsExecuted_AndTheViewModelEndsTheBatchWithIsLoadingMore()
    {
        var (grid, items) = Grid();
        var command = new TestCommand();
        grid.LoadMoreCommand = command;
        grid.LoadMoreCommandParameter = "page";

        var load = grid.LoadMoreAsync();
        Assert.True(grid.IsLoadingMore);
        Assert.Equal(new object?[] { "page" }, command.Executed);
        Assert.False(load.IsCompleted);

        // The view model appends, then ends the batch
        items.Add(new Row(1));
        items.Add(new Row(2));
        grid.IsLoadingMore = false;

        Assert.Equal(2, await load);
    }

    [Fact]
    public async Task ASynchronousCommand_ThatEndsTheBatchInExecute_Completes()
    {
        var (grid, items) = Grid();
        var command = new TestCommand();
        command.OnExecute = _ =>
        {
            items.Add(new Row(1));
            grid.IsLoadingMore = false;
        };
        grid.LoadMoreCommand = command;

        Assert.Equal(1, await grid.LoadMoreAsync());
    }

    [Fact]
    public void TheViewModel_FailsTheBatchWithLoadMoreErrorText()
    {
        var (grid, _) = Grid();
        grid.LoadMoreCommand = new TestCommand();
        _ = grid.LoadMoreAsync();

        grid.LoadMoreErrorText = "Offline";

        Assert.Equal(KumikoLoadMoreState.Failed, grid.LoadMoreState);
        Assert.Equal("Offline", grid.DataSource.IncrementalLoading.ErrorText);
        Assert.IsType<KumikoLoadMoreException>(grid.DataSource.IncrementalLoading.LastError);
        Assert.False(grid.IsLoadingMore);
    }

    [Fact]
    public void ACommandThatThrows_FailsTheBatch()
    {
        var (grid, _) = Grid();
        grid.LoadMoreCommand = new TestCommand { OnExecute = _ => throw new InvalidOperationException("boom") };

        _ = grid.LoadMoreAsync();

        Assert.Equal(KumikoLoadMoreState.Failed, grid.LoadMoreState);
        Assert.Equal("boom", grid.DataSource.IncrementalLoading.LastError?.Message);
    }

    [Fact]
    public async Task CanExecute_IsHasMoreItems_AndCanExecuteChanged_ResumesACompletedGrid()
    {
        var (grid, _) = Grid();
        var command = new TestCommand { Can = false };
        grid.LoadMoreCommand = command;
        grid.LoadMoreCommandParameter = 7;

        Assert.Equal(0, await grid.LoadMoreAsync());
        Assert.Equal(KumikoLoadMoreState.Completed, grid.LoadMoreState);
        Assert.Equal(7, command.LastCanExecuteParameter);
        Assert.Empty(command.Executed);

        command.Can = true;
        command.RaiseCanExecuteChanged();

        Assert.Equal(KumikoLoadMoreState.Idle, grid.LoadMoreState);
    }

    [Fact]
    public void AnIncrementalSource_TakesPrecedenceOverTheCommand()
    {
        var (grid, _) = Grid();
        var command = new TestCommand();
        var source = new PendingSource();
        grid.LoadMoreCommand = command;
        grid.IncrementalSource = source;

        _ = grid.LoadMoreAsync();

        Assert.Equal(1, source.Calls);
        Assert.Empty(command.Executed);
    }

    [Fact]
    public void ReplacingTheCommand_StopsListeningToTheOldOne()
    {
        var (grid, _) = Grid();
        var first = new TestCommand();
        grid.LoadMoreCommand = first;
        Assert.Equal(1, first.Subscribers);

        grid.LoadMoreCommand = new TestCommand();

        Assert.Equal(0, first.Subscribers);
    }

    [Fact]
    public void ReplacingTheItems_CancelsTheCommandsBatch()
    {
        var (grid, _) = Grid();
        grid.LoadMoreCommand = new TestCommand();
        var load = grid.LoadMoreAsync();

        grid.ItemsSource = new ObservableCollection<Row>();

        Assert.False(grid.IsLoadingMore);
        Assert.Equal(KumikoLoadMoreState.Idle, grid.LoadMoreState);
        Assert.Equal(0, load.Result);
    }
}

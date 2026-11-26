using System.ComponentModel;
using KumikoUI.Core.Data;
using KumikoUI.Uno;
using Microsoft.UI.Xaml.Data;
using Windows.Foundation;

namespace KumikoUI.Uno.Tests;

/// <summary>
/// The Uno adapter from WinUI's <see cref="ISupportIncrementalLoading"/> to the grid's incremental loading (#318, spec
/// 104 §10.4): the count maps to the page size, the result count comes back, and the loader's cancellation cancels the
/// <see cref="IAsyncOperation{TResult}"/>.
/// </summary>
public class SupportIncrementalLoadingAdapterTests
{
    /// <summary>An operation the test completes by hand.</summary>
    private sealed class ManualOperation : IAsyncOperation<LoadMoreItemsResult>
    {
        private AsyncOperationCompletedHandler<LoadMoreItemsResult>? _completed;
        private LoadMoreItemsResult _result;

        public bool CancelRequested { get; private set; }

        public AsyncOperationCompletedHandler<LoadMoreItemsResult> Completed
        {
            get => _completed!;
            set
            {
                _completed = value;
                if (Status != AsyncStatus.Started) value(this, Status);
            }
        }

        public uint Id => 1;
        public AsyncStatus Status { get; private set; } = AsyncStatus.Started;
        public Exception ErrorCode => null!;

        public LoadMoreItemsResult GetResults() => _result;

        public void Complete(uint count)
        {
            _result = new LoadMoreItemsResult { Count = count };
            Status = AsyncStatus.Completed;
            _completed?.Invoke(this, Status);
        }

        public void Cancel()
        {
            CancelRequested = true;
            Status = AsyncStatus.Canceled;
            _completed?.Invoke(this, Status);
        }

        public void Close() { }
    }

    private sealed class PagedCollection : List<object>, ISupportIncrementalLoading, INotifyPropertyChanged
    {
        public bool HasMoreItems { get; set; } = true;
        public List<uint> Requested { get; } = new();
        public ManualOperation? Operation { get; private set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            Requested.Add(count);
            Operation = new ManualOperation();
            return Operation;
        }

        public void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    [Fact]
    public async Task TheCount_IsThePageSize_AndTheResultCountComesBack()
    {
        var collection = new PagedCollection();
        var adapter = new SupportIncrementalLoadingAdapter(collection);

        var load = adapter.LoadMoreItemsAsync(40, CancellationToken.None);
        collection.Operation!.Complete(25);

        Assert.Equal(25, await load);
        Assert.Equal(new uint[] { 40 }, collection.Requested);
    }

    [Fact]
    public async Task AnOperationFromAsyncInfoRun_IsAwaited()
    {
        // How the Windows Community Toolkit's IncrementalLoadingCollection creates its operations
        var adapter = new SupportIncrementalLoadingAdapter(new RunCollection());

        Assert.Equal(12, await adapter.LoadMoreItemsAsync(12, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10)));
    }

    private sealed class RunCollection : List<object>, ISupportIncrementalLoading
    {
        public bool HasMoreItems => true;

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count) =>
            System.Runtime.InteropServices.WindowsRuntime.AsyncInfo.Run(async _ =>
            {
                await Task.Yield();
                return new LoadMoreItemsResult { Count = count };
            });
    }

    [Fact]
    public async Task CancelingTheToken_CancelsTheOperation()
    {
        var collection = new PagedCollection();
        var adapter = new SupportIncrementalLoadingAdapter(collection);
        using var cts = new CancellationTokenSource();

        var load = adapter.LoadMoreItemsAsync(20, cts.Token);
        cts.Cancel();

        Assert.True(collection.Operation!.CancelRequested);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => load);
    }

    [Fact]
    public void HasMoreItems_IsTheCollections_AndItsChangesAreForwarded()
    {
        var collection = new PagedCollection();
        var adapter = new SupportIncrementalLoadingAdapter(collection);
        var changed = new List<string?>();
        adapter.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        collection.HasMoreItems = false;
        collection.Raise(nameof(ISupportIncrementalLoading.HasMoreItems));
        collection.Raise("IsLoading");

        Assert.False(adapter.HasMoreItems);
        Assert.Equal(new[] { nameof(ISupportIncrementalLoading.HasMoreItems) }, changed);
    }

    [Fact]
    public void Detach_StopsListeningToTheCollection()
    {
        var collection = new PagedCollection();
        var adapter = new SupportIncrementalLoadingAdapter(collection);
        int changed = 0;
        adapter.PropertyChanged += (_, _) => changed++;

        adapter.Detach();
        collection.Raise(nameof(ISupportIncrementalLoading.HasMoreItems));

        Assert.Equal(0, changed);
    }

    [Fact]
    public async Task InAGridSource_ItLoadsThroughTheLoader()
    {
        var collection = new PagedCollection();
        var source = new KumikoUI.Core.Models.KumikoSource();
        source.SetItems(collection);
        source.IncrementalLoading.HostSource = new SupportIncrementalLoadingAdapter(collection);
        source.IncrementalLoading.BatchSize = 30;

        var load = source.IncrementalLoading.LoadMoreAsync();
        collection.Operation!.Complete(30);

        Assert.Equal(30, await load);
        Assert.Equal(new uint[] { 30 }, collection.Requested);
    }
}

using System.ComponentModel;
using KumikoUI.Core.Data;
using Microsoft.UI.Xaml.Data;
using Windows.Foundation;

namespace KumikoUI.Uno;

/// <summary>
/// Lets a grid load from an items source that implements WinUI's <see cref="ISupportIncrementalLoading"/> (#318, spec
/// 104 §10.4), such as the Windows Community Toolkit's <c>IncrementalLoadingCollection</c>: the batch size is passed as
/// the page size, the loader's cancellation cancels the <see cref="IAsyncOperation{TResult}"/>, and the operation is
/// awaited on the UI thread. A collection that raises <see cref="INotifyPropertyChanged.PropertyChanged"/> for
/// <c>HasMoreItems</c> lets a grid that reached the end resume.
/// </summary>
internal sealed class SupportIncrementalLoadingAdapter : IKumikoIncrementalSource, INotifyPropertyChanged
{
    private readonly ISupportIncrementalLoading _collection;
    private bool _attached;

    public SupportIncrementalLoadingAdapter(ISupportIncrementalLoading collection)
    {
        _collection = collection;
        Attach();
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The adapted collection.</summary>
    public ISupportIncrementalLoading Collection => _collection;

    /// <inheritdoc />
    public bool HasMoreItems => _collection.HasMoreItems;

    /// <inheritdoc />
    public async Task<int> LoadMoreItemsAsync(int count, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var operation = _collection.LoadMoreItemsAsync((uint)Math.Max(1, count));
        using var registration = cancellationToken.Register(static state => ((IAsyncInfo)state!).Cancel(), operation);
        var result = await ToTask(operation);
        return (int)Math.Min(int.MaxValue, result.Count);
    }

    /// <summary>
    /// The operation as a task: <c>AsTask</c> for the operations Uno creates (<c>AsyncInfo.Run</c>, which the
    /// Windows Community Toolkit uses), and the <see cref="IAsyncOperation{TResult}.Completed"/> handler for a custom
    /// implementation, which Uno's <c>AsTask</c> rejects.
    /// </summary>
    private static Task<LoadMoreItemsResult> ToTask(IAsyncOperation<LoadMoreItemsResult> operation)
    {
        // Uno's AsTask reports a custom implementation as a task faulted with NotSupportedException, at once
        var task = operation.AsTask();
        return task.IsFaulted && task.Exception?.InnerException is NotSupportedException
            ? ToTaskThroughCompleted(operation)
            : task;
    }

    private static Task<LoadMoreItemsResult> ToTaskThroughCompleted(IAsyncOperation<LoadMoreItemsResult> operation)
    {
        var completion = new TaskCompletionSource<LoadMoreItemsResult>();
        operation.Completed = (op, status) =>
        {
            switch (status)
            {
                case AsyncStatus.Completed:
                    try { completion.TrySetResult(op.GetResults()); }
                    catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException)) { completion.TrySetException(ex); }
                    break;
                case AsyncStatus.Canceled:
                    completion.TrySetCanceled();
                    break;
                case AsyncStatus.Error:
                    completion.TrySetException(op.ErrorCode ?? new InvalidOperationException("The incremental load failed."));
                    break;
            }
        };
        return completion.Task;
    }

    /// <summary>Starts forwarding the collection's <c>HasMoreItems</c> change notifications.</summary>
    public void Attach()
    {
        if (_attached || _collection is not INotifyPropertyChanged notifying) return;
        _attached = true;
        notifying.PropertyChanged += OnCollectionPropertyChanged;
    }

    /// <summary>Stops listening to the collection, so it does not keep the grid alive.</summary>
    public void Detach()
    {
        if (!_attached || _collection is not INotifyPropertyChanged notifying) return;
        _attached = false;
        notifying.PropertyChanged -= OnCollectionPropertyChanged;
    }

    private void OnCollectionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(HasMoreItems))
            PropertyChanged?.Invoke(this, e);
    }
}

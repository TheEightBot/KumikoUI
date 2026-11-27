using System.ComponentModel;
using System.Windows.Input;

namespace KumikoUI.Core.Data;

/// <summary>
/// Turns the hosts' MVVM surface (<c>LoadMoreCommand</c>, <c>IsLoadingMore</c>, <c>LoadMoreErrorText</c>) into an
/// <see cref="IKumikoIncrementalSource"/> (#318, spec 104 §10.3), the <c>RefreshView.IsRefreshing</c> pattern: a batch
/// sets <c>IsLoadingMore</c> to true and executes the command; it ends when the view model sets <c>IsLoadingMore</c>
/// back to false, and fails when the view model sets <c>LoadMoreErrorText</c>. Shared by the MAUI and Uno hosts.
/// </summary>
/// <remarks>
/// <see cref="HasMoreItems"/> is the command's <see cref="ICommand.CanExecute"/>, read only while no batch runs (an async
/// command reports false while it runs). <see cref="ICommand.CanExecuteChanged"/> is forwarded as a
/// <see cref="INotifyPropertyChanged.PropertyChanged"/> of <see cref="HasMoreItems"/>, which lets a grid that reached the
/// end resume. Call <see cref="Detach"/> when the host is unloaded, so the command does not keep it alive.
/// </remarks>
internal sealed class KumikoCommandIncrementalSource : IKumikoIncrementalSource, INotifyPropertyChanged
{
    private static readonly PropertyChangedEventArgs s_hasMoreItemsChanged = new(nameof(HasMoreItems));

    private readonly ICommand _command;
    private readonly Func<object?> _parameter;
    private readonly Func<int> _itemCount;
    private readonly Action<bool> _setIsLoadingMore;
    private readonly Action<string?> _setErrorText;
    private TaskCompletionSource<int>? _pending;
    private CancellationTokenRegistration _cancellation;
    private int _countBefore;
    private bool _attached;

    /// <summary>Creates the adapter.</summary>
    /// <param name="command">The view model's command that loads the next batch.</param>
    /// <param name="parameter">Reads the command parameter.</param>
    /// <param name="itemCount">Reads how many items the bound collection holds, to report what a batch appended.</param>
    /// <param name="setIsLoadingMore">Sets the host's <c>IsLoadingMore</c>.</param>
    /// <param name="setErrorText">Sets the host's <c>LoadMoreErrorText</c>.</param>
    public KumikoCommandIncrementalSource(ICommand command, Func<object?> parameter, Func<int> itemCount,
        Action<bool> setIsLoadingMore, Action<string?> setErrorText)
    {
        _command = command;
        _parameter = parameter;
        _itemCount = itemCount;
        _setIsLoadingMore = setIsLoadingMore;
        _setErrorText = setErrorText;
        Attach();
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The command this adapter executes.</summary>
    public ICommand Command => _command;

    /// <summary>Whether a batch is waiting for the view model.</summary>
    public bool IsPending => _pending != null;

    /// <summary>The command's <see cref="ICommand.CanExecute"/>; true while a batch is pending.</summary>
    public bool HasMoreItems => _pending != null || _command.CanExecute(_parameter());

    /// <inheritdoc />
    public Task<int> LoadMoreItemsAsync(int count, CancellationToken cancellationToken)
    {
        Cancel(); // a batch the loader no longer waits for
        // Continuations run inline, on the UI thread that ends the batch (the view model setting IsLoadingMore)
        var pending = new TaskCompletionSource<int>();
        _pending = pending;
        _countBefore = _itemCount();
        if (cancellationToken.CanBeCanceled)
            _cancellation = cancellationToken.Register(static state => ((KumikoCommandIncrementalSource)state!).Cancel(), this);

        _setErrorText(null);
        _setIsLoadingMore(true);
        if (!ReferenceEquals(_pending, pending)) return pending.Task; // the view model answered while it was set
        try
        {
            _command.Execute(_parameter());
        }
        catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException))
        {
            Complete(error: ex);
        }
        return pending.Task;
    }

    /// <summary>The view model set <c>IsLoadingMore</c> to false: the batch succeeded with what the collection gained.</summary>
    public void OnIsLoadingMoreCleared()
    {
        if (_pending != null) Complete();
    }

    /// <summary>The view model set <c>LoadMoreErrorText</c>: a non-empty text fails the batch with it.</summary>
    public void OnErrorTextSet(string? text)
    {
        if (_pending != null && !string.IsNullOrEmpty(text)) Complete(error: new KumikoLoadMoreException(text));
    }

    /// <summary>Starts forwarding the command's <see cref="ICommand.CanExecuteChanged"/>.</summary>
    public void Attach()
    {
        if (_attached) return;
        _attached = true;
        _command.CanExecuteChanged += OnCanExecuteChanged;
    }

    /// <summary>Stops listening to the command, so it does not keep the host alive. A pending batch is abandoned.</summary>
    public void Detach()
    {
        if (!_attached) return;
        _attached = false;
        _command.CanExecuteChanged -= OnCanExecuteChanged;
        Cancel();
    }

    private void OnCanExecuteChanged(object? sender, EventArgs e) => PropertyChanged?.Invoke(this, s_hasMoreItemsChanged);

    private void Cancel()
    {
        var pending = _pending;
        if (pending == null) return;
        _pending = null;
        _cancellation.Dispose();
        pending.TrySetCanceled();
    }

    private void Complete(Exception? error = null)
    {
        var pending = _pending;
        if (pending == null) return;
        _pending = null;
        _cancellation.Dispose();
        if (error != null) pending.TrySetException(error);
        else pending.TrySetResult(Math.Max(0, _itemCount() - _countBefore));
    }
}

using System.ComponentModel;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Data;

/// <summary>
/// The state machine of a grid's incremental "load more" loading (#318), owned by its <see cref="KumikoSource"/>
/// (<see cref="KumikoSource.IncrementalLoading"/>). When an <see cref="IKumikoIncrementalSource"/> is set it asks for
/// the next batch as the user nears the end of the loaded rows (<see cref="Mode"/>, <see cref="Threshold"/>), allows
/// one batch in flight, draws a loading row while it waits and an error row with a retry when the batch fails, and
/// cancels the batch when the items are replaced or reset or the source is detached or disposed.
/// </summary>
/// <remarks>
/// <para>
/// The source is, in order of precedence: <see cref="KumikoSource.IncrementalSource"/>, then items passed to
/// <see cref="KumikoSource.SetItems"/> that implement <see cref="IKumikoIncrementalSource"/>, then what the host adds
/// (the hosts' <c>LoadMoreCommand</c> and, on Uno, an <c>ISupportIncrementalLoading</c> collection). While there is
/// none the loader is inactive.
/// </para>
/// <para>
/// The near-end check runs once per drawn frame, after the frame: a grid that is not drawn does not load. Starting a
/// batch, its events and its state changes always happen after the paint, on the UI thread. A batch that succeeds
/// stays <see cref="KumikoLoadMoreState.Loading"/> until the first frame drawn from the view that contains its rows, so
/// the loading row never flickers away before the new rows are drawn. A batch that adds no row stops automatic loading
/// until the user scrolls down, the viewport grows, the view changes for another reason, or a load is asked for
/// explicitly, so a filter that matches nothing does not download the whole dataset. A failed batch is never retried
/// automatically.
/// </para>
/// <para>
/// Every member is used on the UI thread. Events are raised on it.
/// </para>
/// </remarks>
public sealed class KumikoIncrementalLoader
{
    private static readonly SendOrPostCallback s_runScheduled = static state => ((KumikoIncrementalLoader)state!).RunScheduled();

    private readonly KumikoSource _owner;

    // The candidates in precedence order, and the effective one
    private IKumikoIncrementalSource? _explicitSource;
    private IKumikoIncrementalSource? _itemsSource;
    private IKumikoIncrementalSource? _hostSource;
    private IKumikoIncrementalSource? _source;
    private INotifyPropertyChanged? _notifyingSource;

    private KumikoLoadMoreMode _mode = KumikoLoadMoreMode.Automatic;
    private KumikoLoadMoreState _state;
    private int _threshold = -1;
    private int _batchSize;
    private string? _errorText;

    // The batch in flight: every cancellation bumps the generation, so a late completion of an older batch is ignored
    private int _generation;
    private CancellationTokenSource? _cts;
    private Task<int>? _inflight;
    private bool _batchDone;          // the source's task completed; the batch finishes on the next settled frame
    private int _itemsLoaded;
    private int _rowCountBefore;

    // Work decided in a frame and run after it, on the UI thread
    private ScheduledWork _scheduled;
    private SynchronizationContext? _context;

    // Automatic loading stops after a batch that added no row, until something re-arms it
    private bool _disarmed;
    private int _disarmViewVersion;
    private float _lastOffsetY = float.NaN;
    private float _lastViewportHeight = float.NaN;
    private int _rowsPerViewport;
    private bool _disposed;

    private enum ScheduledWork { None, StartNearEnd, Finish, Complete }

    internal KumikoIncrementalLoader(KumikoSource owner) => _owner = owner;

    /// <summary>Raised on the UI thread before a batch is requested from the source.</summary>
    public event EventHandler<KumikoLoadMoreRequestedEventArgs>? LoadMoreRequested;

    /// <summary>
    /// Raised on the UI thread when a batch finished (on the first frame drawn with its rows) or failed. A handler of a
    /// failed batch can set <see cref="KumikoLoadMoreCompletedEventArgs.ErrorText"/>. A batch canceled by a reset raises
    /// nothing.
    /// </summary>
    public event EventHandler<KumikoLoadMoreCompletedEventArgs>? LoadMoreCompleted;

    /// <summary>Raised on the UI thread when <see cref="State"/> or <see cref="Source"/> changes.</summary>
    public event EventHandler? StateChanged;

    /// <summary>The source the loader loads from, after the precedence rules (see the remarks); null when there is none.</summary>
    public IKumikoIncrementalSource? Source => _source;

    /// <summary>When the next batch loads. Default: <see cref="KumikoLoadMoreMode.Automatic"/>.</summary>
    public KumikoLoadMoreMode Mode
    {
        get => _mode;
        set
        {
            if (_mode == value) return;
            _mode = value;
            if (value != KumikoLoadMoreMode.Automatic) _disarmed = false;
            _owner.RaiseLoaderRedraw();
        }
    }

    /// <summary>
    /// How close to the end of the loaded rows the next batch is requested in <see cref="KumikoLoadMoreMode.Automatic"/>
    /// mode, in rows remaining after the last row on screen: 0 loads when the last row comes into view, -1 (the default)
    /// one viewport of rows ahead. Frozen rows, sticky group headers and summary rows do not count.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Set below -1.</exception>
    public int Threshold
    {
        get => _threshold;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, -1);
            if (_threshold == value) return;
            _threshold = value;
            _owner.RaiseLoaderRedraw();
        }
    }

    /// <summary>
    /// How many items each batch asks for. 0 (the default) asks for twice the rows that fit the viewport, at least 20.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Set below 0.</exception>
    public int BatchSize
    {
        get => _batchSize;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _batchSize = value;
        }
    }

    /// <summary>The loading state.</summary>
    public KumikoLoadMoreState State => _state;

    /// <summary>
    /// The message the error row shows while <see cref="State"/> is <see cref="KumikoLoadMoreState.Failed"/>; null shows
    /// <see cref="KumikoStrings.LoadMoreFailed"/>. Set by a failure (a <see cref="KumikoLoadMoreException"/>'s message,
    /// or a <see cref="LoadMoreCompleted"/> handler) and cleared when the next batch starts.
    /// </summary>
    public string? ErrorText
    {
        get => _errorText;
        set
        {
            if (_errorText == value) return;
            _errorText = value;
            if (_state == KumikoLoadMoreState.Failed) _owner.RaiseLoaderRedraw();
        }
    }

    /// <summary>The exception that failed the last batch, or null.</summary>
    public Exception? LastError { get; private set; }

    /// <summary>
    /// Whether the rows are a partial load: a source is set and it has not reported the end
    /// (<see cref="State"/> is not <see cref="KumikoLoadMoreState.Completed"/>). Summaries are computed over the loaded rows.
    /// </summary>
    public bool IsPartial => _source != null && _state != KumikoLoadMoreState.Completed;

    /// <summary>The explicit source (<see cref="KumikoSource.IncrementalSource"/>), which takes precedence over the items.</summary>
    internal IKumikoIncrementalSource? ExplicitSource
    {
        get => _explicitSource;
        set
        {
            if (ReferenceEquals(_explicitSource, value)) return;
            _explicitSource = value;
            ResolveSource(cancel: true);
        }
    }

    /// <summary>
    /// The host's source, used when neither an explicit source nor the items are incremental: the command adapter, or
    /// on Uno the <c>ISupportIncrementalLoading</c> adapter.
    /// </summary>
    internal IKumikoIncrementalSource? HostSource
    {
        get => _hostSource;
        set
        {
            if (ReferenceEquals(_hostSource, value)) return;
            _hostSource = value;
            ResolveSource(cancel: true);
        }
    }

    /// <summary>
    /// Replaces how the work decided in a frame (starting a batch, finishing one) is run after the frame. Null (the
    /// default) posts it to the UI thread's <see cref="SynchronizationContext"/>, captured on the first frame, or runs
    /// it at once when there is none. Tests use it to run that work deterministically.
    /// </summary>
    internal Action<SendOrPostCallback, object?>? Scheduler { get; set; }

    /// <summary>What the load-more row shows this frame; <see cref="LoadMoreRowKind.None"/> when it is hidden.</summary>
    internal LoadMoreRowKind RowKind
    {
        get
        {
            if (_source is not { } source) return LoadMoreRowKind.None;
            return _state switch
            {
                KumikoLoadMoreState.Loading => LoadMoreRowKind.Loading,
                KumikoLoadMoreState.Failed => LoadMoreRowKind.Failed,
                KumikoLoadMoreState.Idle when _mode == KumikoLoadMoreMode.Manual && source.HasMoreItems => LoadMoreRowKind.LoadMore,
                _ => LoadMoreRowKind.None,
            };
        }
    }

    /// <summary>The trigger of the batch in flight (or the last one).</summary>
    internal KumikoLoadMoreTrigger LastTrigger { get; private set; }

    /// <summary>The batch size asked for: <see cref="BatchSize"/>, or twice the rows per viewport (at least 20).</summary>
    internal int EffectiveBatchSize => _batchSize > 0 ? _batchSize : Math.Max(20, 2 * _rowsPerViewport);

    /// <summary>
    /// Loads the next batch now (<see cref="KumikoLoadMoreTrigger.Programmatic"/>), in every <see cref="Mode"/>, and
    /// retries after a failure. While a batch is loading it joins that batch instead of starting another; the token
    /// then cancels only this caller's wait, never the shared batch. Returns 0 without loading when there is no source
    /// or it has no more items.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait for the batch.</param>
    /// <returns>The number of items the batch appended, or 0 when it failed or was canceled.</returns>
    public Task<int> LoadMoreAsync(CancellationToken cancellationToken = default)
    {
        var task = _state == KumikoLoadMoreState.Loading
            ? _inflight ?? Task.FromResult(0)
            : Start(KumikoLoadMoreTrigger.Programmatic);
        return cancellationToken.CanBeCanceled ? task.WaitAsync(cancellationToken) : task;
    }

    /// <summary>
    /// Cancels a batch in flight, clears a failure or the end of the data, and evaluates afresh on the next frame
    /// (<see cref="KumikoLoadMoreState.Idle"/>). Call it after changing the query behind the source in place (a
    /// server-side sort or filter) without replacing or resetting the items.
    /// </summary>
    public void Reset()
    {
        CancelLoad();
        _disarmed = false;
        _errorText = null;
        LastError = null;
        SetState(KumikoLoadMoreState.Idle);
        _owner.RaiseLoaderRedraw();
    }

    // ── Lifecycle (called by KumikoSource) ──────────────────────────

    /// <summary>The items were replaced (<see cref="KumikoSource.SetItems"/>): cancel, and detect the new items.</summary>
    internal void OnItemsReplaced(object items)
    {
        _itemsSource = items as IKumikoIncrementalSource;
        CancelLoad();
        _disarmed = false;
        ResolveSource(cancel: false);
        SetState(KumikoLoadMoreState.Idle);
    }

    /// <summary>The bound collection raised a <c>Reset</c>: cancel and start over.</summary>
    internal void OnItemsReset()
    {
        if (_source == null) return;
        CancelLoad();
        _disarmed = false;
        SetState(KumikoLoadMoreState.Idle);
    }

    /// <summary>The source was detached: cancel; nothing evaluates until frames are drawn again.</summary>
    internal void OnDetached()
    {
        _scheduled = ScheduledWork.None;
        if (_state != KumikoLoadMoreState.Loading) return;
        CancelLoad();
        SetState(KumikoLoadMoreState.Idle);
    }

    /// <summary>The source was disposed: cancel and go inert.</summary>
    internal void OnDisposed()
    {
        CancelLoad();
        _disposed = true;
        SubscribeToSource(null);
        _source = _explicitSource = _itemsSource = _hostSource = null;
        _state = KumikoLoadMoreState.Idle;
        LoadMoreRequested = null;
        LoadMoreCompleted = null;
        StateChanged = null;
    }

    // ── Per frame ───────────────────────────────────────────────────

    /// <summary>
    /// The renderer drew a frame. Decides, without allocating, whether a batch should start, a finished batch can be
    /// completed, or the end of the data was reached, and schedules that work to run after the paint.
    /// </summary>
    /// <param name="lastVisibleRow">The last row drawn (-1 when none).</param>
    /// <param name="rowCount">The rows of the view drawn.</param>
    /// <param name="rowsPerViewport">The rows that fit the scrolling area.</param>
    /// <param name="offsetY">The vertical scroll offset.</param>
    /// <param name="viewportHeight">The viewport height.</param>
    internal void ObserveFrame(int lastVisibleRow, int rowCount, int rowsPerViewport, float offsetY, float viewportHeight)
    {
        if (_disposed || _source is not { } source || _owner.IsDetachedOrDisposed) return;
        _context ??= SynchronizationContext.Current;
        _rowsPerViewport = rowsPerViewport;

        // A downward scroll, a taller viewport or a view change re-arm automatic loading after an unproductive batch
        bool scrolledDown = offsetY > _lastOffsetY + 0.5f;
        bool grew = viewportHeight > _lastViewportHeight + 0.5f;
        _lastOffsetY = offsetY;
        _lastViewportHeight = viewportHeight;
        if (_disarmed && (scrolledDown || grew || _owner.ViewVersion != _disarmViewVersion))
            _disarmed = false;

        if (_scheduled != ScheduledWork.None) return;
        switch (_state)
        {
            case KumikoLoadMoreState.Loading:
                // The batch's rows are in the view being drawn: finish it after this frame
                if (_batchDone && _owner.IsViewSettled) Schedule(ScheduledWork.Finish);
                return;

            case KumikoLoadMoreState.Idle:
                if (!source.HasMoreItems)
                {
                    Schedule(ScheduledWork.Complete);
                    return;
                }
                if (_mode != KumikoLoadMoreMode.Automatic || _disarmed) return;
                int threshold = _threshold >= 0 ? _threshold : Math.Max(1, rowsPerViewport);
                if (rowCount - 1 - lastVisibleRow <= threshold) Schedule(ScheduledWork.StartNearEnd);
                return;
        }
    }

    /// <summary>
    /// The "Load more" or error row was activated (a tap, or a key on the last row): load, or retry. Returns false when
    /// the row shows neither.
    /// </summary>
    internal bool Activate(KumikoLoadMoreTrigger trigger)
    {
        var kind = RowKind;
        if (kind is not (LoadMoreRowKind.LoadMore or LoadMoreRowKind.Failed)) return false;
        _ = Start(trigger);
        return true;
    }

    // ── Batches ─────────────────────────────────────────────────────

    private Task<int> Start(KumikoLoadMoreTrigger trigger)
    {
        if (_disposed || _source is not { } source) return Task.FromResult(0);
        if (_state == KumikoLoadMoreState.Loading) return _inflight ?? Task.FromResult(0);
        if (!source.HasMoreItems)
        {
            SetState(KumikoLoadMoreState.Completed);
            return Task.FromResult(0);
        }

        _disarmed = false;
        int count = EffectiveBatchSize;
        int generation = ++_generation;
        _cts ??= new CancellationTokenSource();
        var token = _cts.Token;
        _batchDone = false;
        _itemsLoaded = 0;
        _rowCountBefore = _owner.RowCount;
        _errorText = null;
        LastError = null;
        LastTrigger = trigger;
        SetState(KumikoLoadMoreState.Loading);
        if (generation != _generation) return Task.FromResult(0);

        try
        {
            LoadMoreRequested?.Invoke(this, new KumikoLoadMoreRequestedEventArgs(count, trigger, token));
        }
        catch (Exception ex)
        {
            if (generation == _generation) Fail(ex, generation);
            return Task.FromResult(0);
        }
        if (generation != _generation) return Task.FromResult(0);

        var task = RunAsync(source, count, generation, token);
        if (generation == _generation && _state == KumikoLoadMoreState.Loading) _inflight = task;
        return task;
    }

    private async Task<int> RunAsync(IKumikoIncrementalSource source, int count, int generation, CancellationToken token)
    {
        int loaded;
        try
        {
            // Resume on the UI thread (no ConfigureAwait(false)): the state, the events and the re-arm happen there.
            loaded = await source.LoadMoreItemsAsync(count, token);
        }
        catch (Exception ex)
        {
            // A cancellation by the loader bumped the generation first: that completion is stale and ignored. Anything
            // else, an OperationCanceledException from a token the app owns included, fails the batch.
            if (generation == _generation) Fail(ex, generation);
            return 0;
        }

        if (generation != _generation || _disposed) return 0;
        _itemsLoaded = loaded;
        _batchDone = true;
        _owner.RaiseLoaderRedraw(); // the next frame drawn from a settled view finishes the batch
        return loaded;
    }

    private void Fail(Exception error, int generation)
    {
        _inflight = null;
        _batchDone = false;
        LastError = error;
        _errorText = error is KumikoLoadMoreException ? error.Message : null;
        _state = KumikoLoadMoreState.Failed;
        _owner.RaiseLoaderRedraw();

        var args = new KumikoLoadMoreCompletedEventArgs(0, 0, KumikoLoadMoreState.Failed, error) { ErrorText = _errorText };
        LoadMoreCompleted?.Invoke(this, args);
        if (generation != _generation || _state != KumikoLoadMoreState.Failed) return;
        _errorText = args.ErrorText;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The first frame drawn from the view containing the batch: Idle or Completed, and the completion event.</summary>
    private void Finish()
    {
        if (_state != KumikoLoadMoreState.Loading || !_batchDone || _source is not { } source) return;
        int generation = _generation;
        _batchDone = false;
        _inflight = null;
        if (_cts != null && !_cts.TryReset()) _cts = null;

        int rowsAdded = Math.Max(0, _owner.RowCount - _rowCountBefore);
        if (rowsAdded == 0)
        {
            // Unproductive: stop automatic loading until something re-arms it (ObserveFrame)
            _disarmed = true;
            _disarmViewVersion = _owner.ViewVersion;
        }

        var next = source.HasMoreItems ? KumikoLoadMoreState.Idle : KumikoLoadMoreState.Completed;
        _state = next;
        _owner.RaiseLoaderRedraw();
        LoadMoreCompleted?.Invoke(this, new KumikoLoadMoreCompletedEventArgs(_itemsLoaded, rowsAdded, next, null));
        if (generation != _generation || _state != next) return;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void CancelLoad()
    {
        _generation++;
        var cts = _cts;
        _cts = null;
        // Not disposed: the app may still hold its token. A canceled source is never reused.
        cts?.Cancel();
        _inflight = null;
        _batchDone = false;
        _scheduled = ScheduledWork.None;
    }

    private void SetState(KumikoLoadMoreState state)
    {
        if (_state == state) return;
        _state = state;
        _owner.RaiseLoaderRedraw();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    // ── Scheduling ──────────────────────────────────────────────────

    private void Schedule(ScheduledWork work)
    {
        _scheduled = work;
        if (Scheduler is { } scheduler) scheduler(s_runScheduled, this);
        else if (_context is { } context) context.Post(s_runScheduled, this);
        else RunScheduled();
    }

    private void RunScheduled()
    {
        var work = _scheduled;
        _scheduled = ScheduledWork.None;
        if (_disposed || _owner.IsDetachedOrDisposed) return;
        switch (work)
        {
            case ScheduledWork.StartNearEnd:
                if (_state == KumikoLoadMoreState.Idle && _mode == KumikoLoadMoreMode.Automatic && !_disarmed)
                    _ = Start(KumikoLoadMoreTrigger.NearEnd);
                break;
            case ScheduledWork.Finish:
                Finish();
                break;
            case ScheduledWork.Complete:
                if (_state == KumikoLoadMoreState.Idle && _source is { HasMoreItems: false })
                    SetState(KumikoLoadMoreState.Completed);
                break;
        }
    }

    // ── Source ──────────────────────────────────────────────────────

    private void ResolveSource(bool cancel)
    {
        var effective = _explicitSource ?? _itemsSource ?? _hostSource;
        if (ReferenceEquals(effective, _source)) return;
        if (cancel) CancelLoad();
        _disarmed = false;
        SubscribeToSource(effective);
        _source = effective;
        if (_state != KumikoLoadMoreState.Idle) _state = KumikoLoadMoreState.Idle;
        _owner.RaiseLoaderRedraw();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SubscribeToSource(IKumikoIncrementalSource? source)
    {
        if (_notifyingSource != null) _notifyingSource.PropertyChanged -= OnSourcePropertyChanged;
        _notifyingSource = source as INotifyPropertyChanged;
        if (_notifyingSource != null) _notifyingSource.PropertyChanged += OnSourcePropertyChanged;
    }

    private void OnSourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.PropertyName) && e.PropertyName != nameof(IKumikoIncrementalSource.HasMoreItems)) return;
        if (_disposed || _source is not { } source) return;

        // The only way out of Completed besides a reset: the source says more is available again
        if (_state == KumikoLoadMoreState.Completed && source.HasMoreItems)
        {
            _disarmed = false;
            SetState(KumikoLoadMoreState.Idle);
        }
        else if (_state == KumikoLoadMoreState.Idle)
        {
            _owner.RaiseLoaderRedraw(); // a Manual "Load more" row appears or disappears
        }
    }
}

/// <summary>What the load-more row after the last row shows.</summary>
internal enum LoadMoreRowKind
{
    /// <summary>Hidden.</summary>
    None,

    /// <summary>A busy indicator and <see cref="KumikoStrings.LoadMoreLoading"/>.</summary>
    Loading,

    /// <summary>The Manual-mode button, <see cref="KumikoStrings.LoadMoreButton"/>.</summary>
    LoadMore,

    /// <summary>The error and a retry button.</summary>
    Failed,
}

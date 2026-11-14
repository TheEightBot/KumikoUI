using System.Runtime.ExceptionServices;

namespace KumikoUI.Core.Models;

// Background view rebuilds (#254). A rebuild of a large source (sort, filter, grouping, summaries) runs on a worker
// from a snapshot of the items and the settings, and the finished ViewSnapshot is swapped in on the source's thread.
// Requests made in one turn of the UI thread start one build (the start is posted to the captured
// SynchronizationContext); a newer request cancels a running build, except that after two cancellations in a row the
// running build is allowed to finish and publish first, so constant changes cannot keep the view from ever updating.
public partial class KumikoSource
{
    /// <summary>
    /// The <see cref="BackgroundRebuildThreshold"/> the MAUI and Uno grids give their source: 10 000 rows.
    /// </summary>
    public const int DefaultBackgroundRebuildThreshold = 10_000;

    private const int MaxCancelStreak = 2;               // cancellations in a row before a running build may finish
    private const int MaxRecordedSummaryChanges = 4096;  // summary changes replayed onto a build; more rebuild again

    private int _backgroundRebuildThreshold = int.MaxValue;
    private bool _forceSyncRebuild;                      // ExpandToItem needs the rebuilt view now

    private bool _bgRequested;                           // a rebuild is wanted that no running build covers
    private bool _bgStartPosted;                         // StartBackgroundRebuild is queued on the context
    private bool _bgRunning;                             // a build of generation _bgGeneration is running
    private int _bgGeneration;
    private int _bgCancelStreak;
    private CancellationTokenSource? _bgCts;
    private TaskCompletionSource? _viewUpdated;
    private List<(object Item, string Property)>? _bgSummaryChanges; // summary changes made while a build runs
    private bool _bgSummaryOverflow;
    private ItemBuffer? _spareItems;                     // a copy buffer no view or build reads, for the next copy

    /// <summary>The number of background builds started (tests).</summary>
    internal int BackgroundBuildsStarted { get; private set; }

    /// <summary>
    /// The number of items from which sorting, filtering, grouping and summaries are rebuilt on a worker thread
    /// instead of on the source's (UI) thread. <see cref="int.MaxValue"/> (the default for a source you create)
    /// keeps every rebuild synchronous; the MAUI and Uno grids set <see cref="DefaultBackgroundRebuildThreshold"/>.
    /// Below the threshold, and on a thread without a <see cref="SynchronizationContext"/>, rebuilds stay synchronous
    /// exactly as before, so small grids and tests remain deterministic. Changing it rebuilds nothing.
    /// <para>
    /// A background rebuild works from a copy of the items and of the view settings taken when it starts. Requests
    /// made in one turn of the UI thread (several <c>PropertyChanged</c> notifications of a sorted property, a sort and
    /// a filter) start one rebuild, and a newer request cancels a running one. When the rebuild finishes, the new view
    /// is swapped in on the source's thread (through its <see cref="SynchronizationContext"/>), raising
    /// <see cref="ViewChanged"/> (<see cref="KumikoViewChangeKind.Rebuild"/>) and then <see cref="ViewUpdated"/>.
    /// Until then every read (<see cref="RowCount"/>, <see cref="GetItem"/>, group information, summaries) answers
    /// from the current view, so reads never block and never see a half-built view; <see cref="IsViewUpdatePending"/>
    /// tells whether a rebuild is under way and <see cref="WhenViewUpdatedAsync"/> waits for it. Selection and the
    /// current cell are tracked by item, so they follow their items into the new order; an open edit stays on its item.
    /// </para>
    /// <para>
    /// <b>Contract.</b> While a background rebuild runs, the worker reads the items' property values (through the
    /// column accessors), and calls the comparers, the filter predicate, group key and display text selectors and
    /// custom aggregates you supplied, and the items' <see cref="object.ToString"/> or formatting for value-list filters
    /// and group text. These must be safe to call from another thread while the UI thread may change the items: they
    /// should only read. Values are read on the worker (reading 1 000 000 rows on the UI thread is what a background
    /// rebuild avoids); an item changed during the rebuild raises its own notification, which schedules the next one.
    /// Everything else stays on the source's thread.
    /// </para>
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public int BackgroundRebuildThreshold
    {
        get => _backgroundRebuildThreshold;
        set
        {
            VerifyAccess();
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _backgroundRebuildThreshold = value;
        }
    }

    /// <summary>
    /// Whether a background rebuild has been requested or is running and its view is not published yet (see
    /// <see cref="BackgroundRebuildThreshold"/>). Reads answer from the current view meanwhile. Always false while
    /// rebuilds are synchronous.
    /// </summary>
    public bool IsViewUpdatePending => _bgRequested || _bgRunning;

    /// <summary>
    /// Raised on the source's thread after a background rebuild's view was swapped in, right after the
    /// <see cref="ViewChanged"/> notification for it (see <see cref="BackgroundRebuildThreshold"/>). Not raised by
    /// synchronous rebuilds, whose new view is in place when the call that caused them returns.
    /// </summary>
    public event EventHandler? ViewUpdated;

    /// <summary>
    /// A task that completes when no background rebuild is pending: at once when none is (or rebuilds are
    /// synchronous), otherwise when the last requested view has been published (after <see cref="ViewUpdated"/>), a
    /// synchronous rebuild replaced it, or the source was disposed. Faults when the rebuild threw (for example in a
    /// comparer). Use it where code needs the new order, such as after <see cref="ToggleSort"/> in a test.
    /// </summary>
    /// <returns>The task; its continuations run asynchronously.</returns>
    /// <exception cref="InvalidOperationException">Called from a thread other than the source's.</exception>
    public Task WhenViewUpdatedAsync()
    {
        VerifyAccess();
        if (!IsViewUpdatePending || _disposed) return Task.CompletedTask;
        return (_viewUpdated ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
    }

    /// <summary>
    /// Whether views are built over a copy of the items, so they survive collection changes: background rebuilds are
    /// enabled (a threshold is set) and the source has a context to publish them on.
    /// </summary>
    private bool BackgroundRebuildsEnabled =>
        _backgroundRebuildThreshold != int.MaxValue && _ownerContext != null && !_disposed;

    /// <summary>Whether a rebuild requested now runs in the background.</summary>
    private bool UseBackgroundRebuild() =>
        !_forceSyncRebuild && BackgroundRebuildsEnabled && _sourceItems.Count >= _backgroundRebuildThreshold;

    /// <summary>Request a background rebuild: one start is posted per turn of the source's thread.</summary>
    private void ScheduleBackgroundRebuild()
    {
        _bgRequested = true;
        if (_bgStartPosted) return;
        _bgStartPosted = true;
        _ownerContext!.Post(static state => ((KumikoSource)state!).StartBackgroundRebuild(), this);
    }

    /// <summary>Start the requested build (on the source's thread): capture the items and settings, then build on a worker.</summary>
    private void StartBackgroundRebuild()
    {
        _bgStartPosted = false;
        if (_disposed || !_bgRequested) return;
        if (!UseBackgroundRebuild())
        {
            // The threshold changed or the items shrank since the request: rebuild here.
            _bgRequested = false;
            RebuildAndNotify();
            return;
        }

        if (_bgRunning)
        {
            // Let a build that newer requests canceled twice in a row finish, so a view is published; it restarts
            // with the latest settings then (_bgRequested stays set).
            if (_bgCancelStreak >= MaxCancelStreak) return;
            _bgCts?.Cancel();
            _bgCancelStreak++;
        }

        var settings = CaptureBuildSettings(CopyItems(), itemsAreCopy: true, forWorker: true);
        _bgRequested = false;
        _bgRunning = true;
        BackgroundBuildsStarted++;
        int generation = ++_bgGeneration;
        var cts = _bgCts = new CancellationTokenSource();
        _bgSummaryChanges?.Clear();
        _bgSummaryOverflow = false;
        var context = _ownerContext!;
        Task.Run(() => RunBackgroundBuild(settings, generation, context, cts.Token));
    }

    /// <summary>Build the view on a worker and post the result (or the failure) back to the source's thread.</summary>
    private void RunBackgroundBuild(ViewBuildSettings settings, int generation, SynchronizationContext context,
        CancellationToken ct)
    {
        try
        {
            var view = ViewBuilder.Build(settings, null, ct);
            context.Post(static state =>
            {
                var (source, gen, built) = ((KumikoSource, int, ViewSnapshot))state!;
                source.PublishBackgroundBuild(gen, built);
            }, (this, generation, view));
        }
        catch (Exception ex) when (ct.IsCancellationRequested &&
                                   (ex is OperationCanceledException || ex.InnerException is OperationCanceledException))
        {
            // Superseded by a newer request, whose build publishes. Its copy of the items can serve the next one.
            if (settings.Items is ItemBuffer buffer)
                context.Post(static state =>
                {
                    var (source, items) = ((KumikoSource, ItemBuffer))state!;
                    source.ReturnItemBuffer(items);
                }, (this, buffer));
        }
        catch (Exception ex)
        {
            var failure = ExceptionDispatchInfo.Capture(ex);
            context.Post(static state =>
            {
                var (source, gen, error) = ((KumikoSource, int, ExceptionDispatchInfo))state!;
                source.FailBackgroundBuild(gen, error);
            }, (this, generation, failure));
        }
    }

    /// <summary>Swap a finished build in (on the source's thread) unless a newer request or a synchronous rebuild superseded it.</summary>
    private void PublishBackgroundBuild(int generation, ViewSnapshot view)
    {
        if (_disposed || !_bgRunning || generation != _bgGeneration)
        {
            ReturnItemBuffer(view.Items as ItemBuffer);
            return;
        }
        _bgRunning = false;
        _bgCts = null;
        _bgCancelStreak = 0;

        var oldRowKeys = _rowKeys;
        var oldItems = _view.Items as ItemBuffer;
        PublishView(view);
        // The replaced view's copy of the items (or a buffer the build made for the purpose) is the next build's.
        ReturnItemBuffer(oldItems ?? view.SpareItems);
        view.SpareItems = null;
        ReplaySummaryChanges();

        if (_updateDepth > 0)
            _pendingStructural = true; // the open batch raises its one notification at EndUpdate
        else if (_viewChanged != null && oldRowKeys != null)
        {
            var newRowKeys = view.RowKeys ?? GetRowKeys();
            RaiseStructural(newRowKeys, KumikoViewChangedEventArgs.Rebuild(oldRowKeys, newRowKeys, view.RowKeyIndex));
        }
        view.RowKeys = null; // the notification record keeps them

        ViewUpdated?.Invoke(this, EventArgs.Empty);

        if (_bgRequested)
        {
            // Requests arrived while the build ran: build again from the latest state.
            if (!_bgStartPosted)
            {
                _bgStartPosted = true;
                _ownerContext?.Post(static state => ((KumikoSource)state!).StartBackgroundRebuild(), this);
            }
        }
        else
        {
            CompleteViewUpdate();
        }
    }

    /// <summary>A build threw (on the source's thread): fault the awaiters, or rethrow there when nobody awaits.</summary>
    private void FailBackgroundBuild(int generation, ExceptionDispatchInfo failure)
    {
        if (_disposed || !_bgRunning || generation != _bgGeneration) return;
        _bgRunning = false;
        _bgCts = null;
        _bgCancelStreak = 0;
        _bgRequested = false;
        var awaiters = _viewUpdated;
        _viewUpdated = null;
        if (awaiters != null)
            awaiters.TrySetException(failure.SourceException);
        else
            failure.Throw(); // like a synchronous rebuild, the error surfaces on the source's thread
    }

    /// <summary>
    /// Copy the bound collection for a build, into the spare buffer when it fits. A copy of 1 000 000 items is an
    /// 8 MB array; allocating one on the UI thread waits while a background garbage collection runs (tens of
    /// milliseconds), so the copy a replaced view held is reused instead, and the first background build allocates the
    /// spare on its worker.
    /// </summary>
    private ItemBuffer CopyItems()
    {
        var spare = _spareItems;
        _spareItems = null;
        return ItemBuffer.CopyOf(_sourceItems, spare);
    }

    /// <summary>Keep <paramref name="buffer"/> (no longer read by any view or build) for the next copy.</summary>
    private void ReturnItemBuffer(ItemBuffer? buffer)
    {
        if (buffer == null || _disposed) return;
        if (_spareItems == null || _spareItems.Capacity < buffer.Capacity)
            _spareItems = buffer;
    }

    /// <summary>Drop a requested or running background rebuild (a synchronous rebuild supersedes it).</summary>
    private void CancelBackgroundRebuild()
    {
        _bgRequested = false;
        if (_bgRunning)
        {
            _bgCts?.Cancel();
            _bgCts = null;
            _bgRunning = false;
            _bgGeneration++;
        }
        _bgCancelStreak = 0;
        _bgSummaryChanges?.Clear();
        _bgSummaryOverflow = false;
    }

    /// <summary>Complete <see cref="WhenViewUpdatedAsync"/> once nothing is pending.</summary>
    private void CompleteViewUpdate()
    {
        if (IsViewUpdatePending && !_disposed) return;
        var awaiters = _viewUpdated;
        _viewUpdated = null;
        awaiters?.TrySetResult();
    }

    /// <summary>Rebuild synchronously now and raise <see cref="KumikoViewChangeKind.Rebuild"/>.</summary>
    private void RebuildAndNotify()
    {
        RebuildCore();
        if (_viewChanged != null)
        {
            var newRowKeys = GetRowKeys();
            RaiseStructural(newRowKeys, KumikoViewChangedEventArgs.Rebuild(_rowKeys!, newRowKeys));
        }
    }

    /// <summary>
    /// Note a summarized property's change made while a background build runs: the build may have read the old value,
    /// so the change is applied again to the published view (<see cref="ReplaySummaryChanges"/>).
    /// </summary>
    private void RecordSummaryChange(object item, string property)
    {
        if (!_bgRunning || !IsSummarized(property)) return;
        var changes = _bgSummaryChanges ??= new();
        if (changes.Count >= MaxRecordedSummaryChanges)
            _bgSummaryOverflow = true;
        else
            changes.Add((item, property));
    }

    /// <summary>Whether a table, group or caption summary aggregates a property a change of <paramref name="property"/> affects.</summary>
    private bool IsSummarized(string property)
    {
        foreach (var row in _tableSummaryRows)
            if (UsesProperty(row.Columns, property)) return true;
        foreach (var row in _groupSummaryRows)
            if (UsesProperty(row.Columns, property)) return true;
        return _captionSummary != null && UsesProperty(_captionSummary.Columns, property);
    }

    /// <summary>
    /// Apply the summary changes made while the published build ran to its summaries: each replaces the value the
    /// build read with the item's current one. Too many, or one that cannot apply in place, request another rebuild.
    /// </summary>
    private void ReplaySummaryChanges()
    {
        var changes = _bgSummaryChanges;
        bool overflow = _bgSummaryOverflow;
        _bgSummaryOverflow = false;
        if (overflow)
        {
            changes?.Clear();
            _bgRequested = true;
            return;
        }
        if (changes == null || changes.Count == 0) return;

        foreach (var (item, property) in changes)
        {
            if (!UpdateGroupSummaries(item, property))
            {
                _bgRequested = true;
                break;
            }
            if (_view.TableAggregates.Count > 0)
                UpdateTableSummaries(item, property);
        }
        changes.Clear();
    }
}

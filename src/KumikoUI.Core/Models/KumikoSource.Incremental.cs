using KumikoUI.Core.Data;

namespace KumikoUI.Core.Models;

/// <summary>Incremental "load more" loading (#318): the source's <see cref="KumikoIncrementalLoader"/>.</summary>
public partial class KumikoSource
{
    private KumikoIncrementalLoader? _incremental;

    /// <summary>
    /// The object that loads more items on request, overriding items that implement
    /// <see cref="IKumikoIncrementalSource"/> themselves. Null (the default) uses the items when they implement it.
    /// Replacing it cancels a batch in flight. See <see cref="IncrementalLoading"/>.
    /// </summary>
    public IKumikoIncrementalSource? IncrementalSource
    {
        get => _incremental?.ExplicitSource;
        set
        {
            if (value == null && _incremental == null) return;
            IncrementalLoading.ExplicitSource = value;
        }
    }

    /// <summary>
    /// The incremental loading state machine: its mode, threshold and batch size, its state and events, and
    /// <see cref="KumikoIncrementalLoader.LoadMoreAsync"/>. Never null; inactive while there is no
    /// <see cref="KumikoIncrementalLoader.Source"/>.
    /// </summary>
    public KumikoIncrementalLoader IncrementalLoading => _incremental ??= new KumikoIncrementalLoader(this);

    /// <summary>The loader when one was created (an incremental source was set or the loader was read), else null.</summary>
    internal KumikoIncrementalLoader? IncrementalLoaderOrNull => _incremental;

    /// <summary>
    /// A counter that moves on every structural change of the published view (a rebuild, an insert or a removal of
    /// rows). The incremental loader compares it to tell a view change it did not cause.
    /// </summary>
    internal int ViewVersion { get; private set; }

    /// <summary>How many items the bound collection holds (null entries included).</summary>
    internal int SourceItemCount => _sourceItems.Count;

    /// <summary>Whether the source is detached or disposed: no collection is observed, nothing loads.</summary>
    internal bool IsDetachedOrDisposed => _detached || _disposed;

    /// <summary>
    /// Whether the view being drawn includes every change made so far: no batch is open, no rebuild is deferred and no
    /// background rebuild is pending.
    /// </summary>
    internal bool IsViewSettled => !_viewStale && _updateDepth == 0 && !IsViewUpdatePending;

    /// <summary>
    /// The incremental loader changed what the load-more row shows: raise <see cref="KumikoViewChangeKind.Redraw"/> so a
    /// host repaints. Unlike a data change it leaves cached measurements alone. From another thread it is posted to
    /// the source's thread.
    /// </summary>
    internal void RaiseLoaderRedraw()
    {
        if (_disposed) return;
        if (!CheckAccess())
        {
            _ownerContext?.Post(static state => ((KumikoSource)state!).RaiseLoaderRedraw(), this);
            return;
        }
        if (_updateDepth > 0)
            _pendingRedraw = true;
        else if (_viewChanged != null && _rowKeys != null)
            _viewChanged.Invoke(this, KumikoViewChangedEventArgs.Redraw(_rowKeys));
    }
}

# Spec: incremental / load-more data loading (#104)

| | |
|---|---|
| Issue | [#104](https://github.com/TheEightBot/KumikoUI/issues/104) (plan key X2, Epic X, 1.x fast follow-on) |
| Findings | CORE-33 (no paging/incremental hook), CORE-29 (no scroll event), report 07 §1.3 item 7 (load-more in 3/3 MAUI-market vendors) |
| Builds on | #56 / PR #198 (`Scrolled`, `KumikoScroller`, `ScrollState.ScrollTo`), #28 / PR #194 (`KumikoSource` `Detach`/`Attach`/`Dispose`), #59 / PR #197 (host teardown and re-attach), #77 / #78 (PRs #192 / #190, `KumikoAnnouncements`) |
| Interacts with | #69 (off-UI-thread view pipeline), #107 (paging spec), #102 (variable row height), #79 (virtual accessibility tree) |
| Status | Draft for review |

## 1. Summary

A grid bound to a large remote dataset should load rows on demand: when the user scrolls near the end of the
loaded rows, the grid asks the app for the next batch, draws a busy row while it waits, appends the result
without disturbing what the user is looking at, and offers a retry when the load fails.

The recommended design has three layers:

1. **Core contract.** `IKumikoIncrementalSource` (`HasMoreItems`, `LoadMoreItemsAsync(count, CancellationToken)`),
   detected on the items passed to `KumikoSource.SetItems` or set explicitly.
2. **Core state machine.** `KumikoIncrementalLoader`, owned by `KumikoSource` (so it shares the source's
   lifecycle: `SetItems`, INCC `Reset`, `Detach`, `Attach`, `Dispose`). It evaluates the near-end condition
   once per rendered frame, allows one load in flight, cancels on source reset, and exposes the state the
   renderer draws as a full-width **load-more row** after the last row.
3. **Host adapters.** MAUI and Uno expose the same bindable surface (`LoadMoreCommand`, `LoadMoreThreshold`,
   `IsLoadingMore`, ...), events and methods, plus an Uno-only adapter for WinUI's `ISupportIncrementalLoading`.
   All of them feed the one Core state machine.

A prerequisite Core change makes appending a batch cheap: a multi-item INCC `Add` becomes one view change (an
`Insert` of `count` rows when no transform is active, one rebuild otherwise) instead of one rebuild per item.

## 2. Goals and non-goals

### Goals

- G1. Load the next batch automatically when the user nears the end of the loaded rows, on both hosts and all
  their targets, with no host-specific behavior differences.
- G2. A manual mode in which the user taps a drawn "Load more" row (and keyboard users can trigger it too).
- G3. A drawn loading row while a batch is in flight and a drawn error row with retry when it fails, localized
  through `KumikoStrings`, themed through `KumikoStyle`, hit-testable and announced to screen readers.
- G4. Exactly one load in flight; cancellation on source replacement, collection reset, host unload/`Detach`
  and `Dispose`; stale completions ignored.
- G5. Appending a batch costs one view change, not one per item, and does not move the rows the user sees.
- G6. Three ways in, all mapping to the same Core behavior: a Kumiko interface (portable, testable), an MVVM
  command with a threshold (MAUI `CollectionView` idiom), and WinUI `ISupportIncrementalLoading` on Uno.
- G7. The steady-state render loop stays allocation-free (#68, `RenderAllocationTests`), including frames that
  draw the loading row.

### Non-goals

- Paging UI (page numbers, page size picker, "page 3 of 40"): that is #107.
- Server-side sort/filter/group as a built-in feature. The spec defines the hooks an app uses to re-query
  (section 6.4), not a query model. #107 owns `DataTable`/dictionary sources and query descriptors.
- Data virtualization of an unknown or huge total count (placeholder rows for not-yet-loaded indices, random
  access to row 900,000). Incremental loading only appends at the end.
- Loading at the top ("load older") or bidirectional loading. Section 13 keeps the enum shape open for it.
- Pull-to-refresh. A separate mobile idiom (report 07 §1.3 item 11).
- Unloading (evicting) rows that scrolled far away.

## 3. Current state (main at c77f44b)

| Area | What exists | Gap for load-more |
|---|---|---|
| Scroll | `ScrollState.ScrollChanged` (`KumikoScrollChangedEventArgs`), `MaxOffsetY`. PR #198 adds host `Scrolled` (`KumikoScrolledEventArgs` with `MaxOffsetY`), `ScrollState.ScrollTo`, `KumikoScroller` (`GetRowOffsetY`, `ScrollToRow`, `ScrollToItem`, `RefreshContentExtents`), `KumikoSource.IndexOfItem` / `ExpandToItem`. | Apps can hand-roll a near-end check on `Scrolled`, but there is no gate, no busy row, no cancellation. |
| View changes | `KumikoSource.ViewChanged` with `KumikoViewChangeKind` `Redraw`/`Rebuild`/`Insert`/`Remove`; `KumikoViewChangedEventArgs.MapRow`/`GetViewRow`; `BeginUpdate`/`EndUpdate`. | `TryIncrementalUpdate` handles only `NewItems.Count == 1` and only without sort/group/summaries; a multi-item `Add` or any `Add` under a sort runs `RebuildCore` per event. `ObservableCollection<T>.Add` in a loop under a sort is N full rebuilds. Inside `BeginUpdate` collection changes still rebuild immediately ("rows stay readable"). |
| Layout | `KumikoRenderer.Render` sets `scroll.ContentHeight = GetContentHeight(RowCount) + groupPanel + top + bottom summaries`; PR #198's `KumikoScroller.RefreshContentExtents` duplicates that formula. | No band after the last row other than the unpinned bottom summary rows. |
| Hit testing | `KumikoHitTester.HitTestCore`, `HitRegion` (15 values ending in `TableSummaryRow`). | No region for a footer row. |
| Strings | `KumikoStrings` record (`with` to localize); PRs #192/#190 add `Announce*` strings and `KumikoAnnouncements`. | No load-more strings. |
| Lifecycle | PR #194: `KumikoSource : IDisposable`, `Detach()`/`Attach()` (Attach rebuilds), `Dispose()`. PR #197: hosts call `Detach` in `Cleanup`/`Unloaded` and `Attach` on reconnect/`Loaded`. | Nothing to cancel yet. |
| Threading | `KumikoSource` applies INCC changes on the raising thread; hosts marshal only the invalidation (`OnBoundItemsCollectionChanged` uses `Dispatcher`/`DispatcherQueue`). #69 will make mutations UI-thread-only and move rebuilds off the UI thread. | A loader that appends from a thread-pool continuation races the renderer today. |

## 4. API options compared

### Option A: WinUI `ISupportIncrementalLoading` interop (Uno)

`Microsoft.UI.Xaml.Data.ISupportIncrementalLoading { bool HasMoreItems; IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count); }`,
the contract WinUI `ListView`/`GridView` consume (with `ListViewBase.IncrementalLoadingThreshold` and
`DataFetchSize`, both in pages, and `IncrementalLoadingTrigger`). The Windows Community Toolkit's
`IncrementalLoadingCollection<TSource, IType>` (over `IIncrementalSource<T>.GetPagedItemsAsync(pageIndex, pageSize, ct)`)
implements it.

- Pros: zero learning cost for WinUI/Uno developers; existing collections (Toolkit) work unchanged.
- Cons: WinRT types (`IAsyncOperation<T>`, `uint`) cannot appear in `KumikoUI.Core` (zero dependencies) or on
  MAUI; no `CancellationToken` in the signature (cancellation is `IAsyncInfo.Cancel()`); the Toolkit collection
  adds items one `Add` at a time (N INCC events per batch).
- Verdict: **support as an Uno-only adapter** onto the Core contract, not as the primary API.

### Option B: a Kumiko interface, `IKumikoIncrementalSource`

```csharp
namespace KumikoUI.Core.Data;

/// A collection (or a companion object) that can append more items to the grid's items on request.
public interface IKumikoIncrementalSource
{
    /// Whether more items can be loaded. Read on the UI thread after every batch and on every evaluation.
    bool HasMoreItems { get; }

    /// Loads up to count more items and appends them to the bound collection on the UI thread, raising
    /// INotifyCollectionChanged. Returns the number of items appended. Honors cancellationToken by throwing
    /// OperationCanceledException (or returning early).
    Task<int> LoadMoreItemsAsync(int count, CancellationToken cancellationToken);
}
```

- Pros: portable (Core, both hosts, unit tests without a UI); first-class cancellation; same shape as WinUI's
  contract so the Uno adapter is trivial; the items stay in the app's collection, so the existing
  `SetItems`/INCC/INPC pipeline, selection (`KumikoSelectionModel`, item-based) and `ViewChanged` keep working.
- Cons: one more interface for MAUI developers used to commands; the implementer must append on the UI thread.
- Verdict: **the primary contract.** Every other entry point adapts to it.

### Option C: MVVM `LoadMoreCommand` + `LoadMoreThreshold` (MAUI `CollectionView` idiom)

MAUI's `ItemsView` has `RemainingItemsThreshold` (int, `-1` disables), `RemainingItemsThresholdReached` and
`RemainingItemsThresholdReachedCommand`; `RefreshView` pairs a `Command` with a two-way `IsRefreshing` the view
model clears when it finishes. DevExpress's MAUI grid exposes `IsLoadMoreEnabled` + `LoadMoreCommand`;
Syncfusion and Telerik document "load more" / "load on demand" equivalents (report 07, row "Incremental / load-more").

- Pros: no interface to implement; binds from XAML; familiar to the MAUI market the grid competes in.
- Cons: `ICommand.Execute` is fire-and-forget, so completion and failure must come back through bindings;
  `CanExecute` is overloaded to mean "has more".
- Verdict: **ship it**, as a host-side adapter that turns the command into an `IKumikoIncrementalSource`
  (section 10.3): `HasMoreItems` is `CanExecute(LoadMoreCommandParameter)`, the load starts by setting
  `IsLoadingMore = true` and executing the command, and it ends when the view model sets `IsLoadingMore` back to
  `false` (success) or sets `LoadMoreErrorText` (failure). This is the `RefreshView.IsRefreshing` pattern.

### Option D (rejected): event only, app owns everything

A `NearEnd` event (or `Scrolled` from #56 alone) and nothing else. Every app would re-implement the gate,
cancellation, busy row and anchoring, and get the reentrancy wrong. `LoadMoreRequested` still exists as a
notification (section 10.1), and `Scrolled` remains available for custom schemes.

### Recommendation

B as the Core contract, C on both hosts, A as an Uno adapter, all driving one `KumikoIncrementalLoader`.
Precedence when several are present (first wins; the others are ignored, and a debug trace says so):

1. The host's explicit `IncrementalSource` property (or `KumikoSource.IncrementalSource` in Core).
2. `ItemsSource` implementing `IKumikoIncrementalSource`.
3. (Uno) `ItemsSource` implementing `ISupportIncrementalLoading`.
4. `LoadMoreCommand`.

## 5. Core model

### 5.1 Types

```csharp
namespace KumikoUI.Core.Data;

public enum KumikoLoadMoreMode
{
    None,       // never load (the source may still be set; LoadMoreAsync still works)
    Automatic,  // load when the user nears the end (default)
    Manual,     // draw a "Load more" row; load when it is activated
}

public enum KumikoLoadMoreState
{
    Idle,       // nothing in flight; more may be available
    Loading,    // one batch in flight
    Failed,     // the last batch threw; waiting for a retry
    Completed,  // HasMoreItems is false
}

public enum KumikoLoadMoreTrigger
{
    NearEnd,       // scrolling, a resize, a view change or the initial fill brought the end within the threshold
    Keyboard,      // Down/PageDown/End (Ctrl+End) past the last row
    Tap,           // the "Load more" or "Retry" row was activated by pointer
    Programmatic,  // LoadMoreAsync
}

public sealed class KumikoIncrementalLoader
{
    public IKumikoIncrementalSource? Source { get; }            // the effective source (section 4 precedence)
    public KumikoLoadMoreMode Mode { get; set; }                // default Automatic
    public int Threshold { get; set; }                          // rows; -1 = one viewport of rows (default)
    public int BatchSize { get; set; }                          // items; 0 = auto (default)
    public KumikoLoadMoreState State { get; }
    public string? ErrorText { get; set; }                      // shown on the failed row; null = KumikoStrings.LoadMoreFailed
    public Exception? LastError { get; }
    public bool IsPartial { get; }                              // a source is set and State != Completed

    public Task<int> LoadMoreAsync(CancellationToken cancellationToken = default); // Programmatic trigger
    public void Reset();                                        // cancel, clear Failed, re-arm (section 6.4)

    public event EventHandler<KumikoLoadMoreRequestedEventArgs>? LoadMoreRequested;
    public event EventHandler<KumikoLoadMoreCompletedEventArgs>? LoadMoreCompleted;
    public event EventHandler? StateChanged;
}

public sealed class KumikoLoadMoreRequestedEventArgs : EventArgs
{
    public int Count { get; }
    public KumikoLoadMoreTrigger Trigger { get; }
    public CancellationToken CancellationToken { get; }
}

public sealed class KumikoLoadMoreCompletedEventArgs : EventArgs
{
    public int ItemsLoaded { get; }          // what LoadMoreItemsAsync returned (0 on failure)
    public int RowsAdded { get; }            // RowCount after minus before: what became visible
    public KumikoLoadMoreState State { get; } // Idle, Failed or Completed (never Loading)
    public Exception? Error { get; }
    public string? ErrorText { get; set; }   // a handler sets the user-facing message for the failed row
}

/// Thrown into a load by an adapter that learns of a failure as text (the command path's LoadMoreErrorText).
public sealed class KumikoLoadMoreException : Exception { /* standard constructors */ }
```

`KumikoSource` gains:

```csharp
public IKumikoIncrementalSource? IncrementalSource { get; set; } // explicit; overrides detection on SetItems
public KumikoIncrementalLoader IncrementalLoading { get; }        // never null; inactive while Source is null
```

The loader lives on the source rather than on the hosts because every geometry consumer already takes a
`KumikoSource` (`KumikoRenderer.Render`, `KumikoHitTester.HitTestCore`, `KumikoScroller.*`,
`KumikoInputController`), and because the source owns the lifecycle events that must cancel a load (`SetItems`,
INCC `Reset`, PR #194's `Detach`/`Dispose`). Hosts own no loading logic, only adapters and timers.

All new public members get XML docs (doc warnings are errors since #54/#188).

### 5.2 State machine

| From | Event | To |
|---|---|---|
| `Idle` | trigger (near end, tap, keyboard, `LoadMoreAsync`) with `HasMoreItems` | `Loading` |
| `Idle` | evaluation observes `HasMoreItems == false` | `Completed` |
| `Loading` | task returned n, and the first frame rendered from the view containing the batch (6.3) reads `HasMoreItems` true | `Idle` |
| `Loading` | same, `HasMoreItems` false | `Completed` |
| `Loading` | task throws (anything but the loader's own cancellation) | `Failed` |
| `Failed` | retry (tap, keyboard, `LoadMoreAsync`) | `Loading` |
| any | `Reset`, `SetItems`, INCC `Reset`, `IncrementalSource` replaced, `Detach`, `Dispose` (cancels an in-flight load) | `Idle` |

- `Completed` is also entered without a load: whenever the loader observes `HasMoreItems == false` while
  `Idle` (it reads the property on every evaluation; it is a property read, never an allocation).
- `Completed` leaves through `Reset`, a source change, or a **change notification** saying more is available:
  a source that implements `INotifyPropertyChanged` and raises it for `HasMoreItems` (and the command adapter on
  `CanExecuteChanged`, section 10.3) moves `Completed` back to `Idle` when the value now reads `true`. The loader
  never leaves `Completed` by polling, so a property that flaps without notifying cannot cause a loop.
- A successful batch stays `Loading` until the re-arm frame (6.3): the loading row does not flicker away
  before the new rows are drawn, `HasMoreItems` is read after the source had a chance to update it, and
  `LoadMoreCompleted.RowsAdded` is measured against the published view. A failure leaves `Loading` at once.
- `Failed` is never left automatically: no automatic retry on scroll, so a dead network does not produce a
  request per frame.

## 6. Near-end trigger semantics

### 6.1 What "the end" is

The end is the **last row of the current view**: `KumikoSource.RowCount - 1`, the sorted, filtered and grouped
flat view, including group header and group summary rows and counting a collapsed group as its header row. It
is not the end of the source collection. The rows remaining after the viewport are

```
remaining = RowCount - 1 - lastVisibleRow
```

where `lastVisibleRow` is the last scrolling row the renderer drew (frozen rows are excluded: they never
scroll). The trigger fires when `remaining <= effectiveThreshold`.

- `Threshold >= 0`: `effectiveThreshold = Threshold` rows. `0` means "when the last row comes into view".
- `Threshold == -1` (default): one viewport of rows,
  `floor(KumikoScroller.GetScrollableRowsViewportHeight(...) / style.RowHeight)`, so the next batch is
  requested one screen ahead. This matches WinUI's default prefetch more closely than MAUI's `-1 = disabled`;
  MAUI developers wanting "off" use `LoadMoreMode.None`.
- The threshold is specified in rows (the API unit apps reason in) and evaluated as rows because every row has
  the same height today (`style.RowHeight`, see `KumikoScroller` remarks). When #102 adds variable row heights,
  evaluation moves to pixels (`MaxOffsetY - OffsetY <= threshold * style.RowHeight`) with the same API.
- The pinned bottom summary footer, sticky group headers and the load-more row itself do not count as rows.

### 6.2 When it is evaluated: once per rendered frame

The renderer already computes `(firstRow, lastRow)` every frame
(`_layout.GetVisibleRowRangeWithOffset(...)` in `KumikoRenderer.Render`). At the end of `Render` it calls
`dataSource.IncrementalLoading.ObserveFrame(lastRow, rowsPerViewport, publishedViewVersion)` (internal). That
single hook covers every reason the end can come into reach, because each of them repaints:

| Cause | Repaint source |
|---|---|
| user scroll, inertia, keyboard scroll | `ScrollState.ScrollChanged` → host invalidates |
| programmatic scroll (#56 `ScrollToRow`/`VerticalOffset`) | `FinishScroll` / `OnOffsetPropertyChanged` invalidate |
| resize (viewport grew) | the platform repaints |
| a filter, collapse or removal shrank the view | structural `ViewChanged` → host invalidates |
| initial bind with an empty or short collection | first paint |
| a batch arrived but did not fill the viewport | the batch's `ViewChanged` |

Properties:

- **Debounce for free.** At most one evaluation per frame, never more than the display rate; no timers.
- **Never runs user code inside paint.** `ObserveFrame` only decides; when it decides to load it schedules the
  start on the UI thread (`SynchronizationContext.Post` with a cached static callback, captured when the loader
  first observes a frame; an internal scheduler seam replaces it in tests). The app's `LoadMoreItemsAsync` runs
  after the paint returns.
- **Zero allocation when nothing triggers.** `ObserveFrame` is integer arithmetic plus a `HasMoreItems` read.
  Scheduling and the async state machine allocate once per batch, not per frame.
- **Not visible, no load.** A grid that is not painted (hidden tab, detached) does not load.

### 6.3 Once per batch, and runaway protection

- **One in flight.** While `State == Loading`, evaluations return immediately. `LoadMoreAsync` called while
  loading returns a task for the in-flight load (it does not start a second load, and its own token cancels only
  that caller's wait, never the shared load).
- **Re-arm after the view settles.** A batch completing does not re-enable triggering until the view that
  contains it is the one being drawn. At completion the loader records the source's *requested* view version
  (section 8.4: the version the latest mutation will produce), and the re-arm frame is the first frame rendered
  from a *published* version at or past it. Today rebuilds are synchronous, so both versions are equal at
  completion and the re-arm frame is simply the next frame (also when the batch added nothing). After #69 it is
  the frame that swaps in the `ViewSnapshot` containing the batch. Without this gate a slow background rebuild
  would let the next frame see the short view and request a second batch immediately.
- **Fill the viewport.** If after a batch the end is still within the threshold (a short first batch, a tall
  window), the next frame triggers again: batches chain until the viewport plus threshold is filled or
  `HasMoreItems` is false. Each chained batch is a separate dispatcher turn, so a synchronously completing
  source cannot recurse.
- **Disarm on unproductive batches.** A batch that leaves `RowCount` unchanged (it returned 0 items, or a client
  filter hid every new item) disarms automatic triggering. It re-arms on the next user-originated scroll
  downward (`DeltaY > 0`), a viewport size increase, a structural `ViewChanged` not caused by a load (the user
  changed a filter), `Reset`, or a manual/keyboard/programmatic trigger. This stops a filter that matches
  nothing from downloading the entire dataset, without a magic batch cap. (Open question Q4 asks whether an
  additional cap is wanted.)
- **Direction does not matter.** Scrolling up near the end does not trigger anything new because the state gate
  already covers it; the check does not look at the scroll direction except for re-arming.

### 6.4 Sorting, filtering and grouping

Client-side transforms keep working over the loaded rows; the loader does not change their semantics.

| Transform | Behavior | Consequence to document |
|---|---|---|
| Sort (client) | New items are sorted among the loaded ones and may appear anywhere, including above the viewport. | "Sorted among loaded rows." Rows above the viewport are anchored (section 11) so nothing jumps. The end of the view is the sorted end, not "the newest items". |
| Filter (client, `SetFilter`/`SetColumnFilter`) | New items that fail the filter get no row. | The view can stay short, so batches chain (6.3) until it fills or a batch adds no row, which disarms. |
| Grouping | New items join existing groups or create groups anywhere in the flat view; group header counts and caption summaries grow. | The end is the last flat row (possibly a collapsed group's header or a group summary row). |
| Collapse a group near the end | The view shrinks; the end comes into reach. | Triggers a load (structural `ViewChanged` → repaint → evaluation). Intended. |

**Server-side query changes.** An app whose sort/filter must apply to the whole remote dataset handles
`KumikoSource.SortColumnsChanging` / `FilterChanging` (cancelable) or the `*Changed` events, re-queries, and
either replaces `ItemsSource` (which resets the loader) or clears its collection (INCC `Reset`, which also
resets the loader) or calls `ResetLoadMore()` after changing the query in place. The spec does not add a
"server mode" switch; #107 is the place for a query model. The cookbook (issue 7) shows the pattern.

**Not a cancellation trigger.** A client-side sort, filter or grouping change does **not** cancel an in-flight
load: the batch still appends to the same collection, and the transform applies to it on arrival. Only a source
reset cancels (section 7).

## 7. Cancellation and reentrancy

### 7.1 Cancellation sources

| Event | Effect on an in-flight load | State after |
|---|---|---|
| `KumikoSource.SetItems` (host `ItemsSource` replaced) | cancel the token, bump the generation | `Idle`, loader re-detects the source (section 4) |
| INCC `Reset` on the bound collection | cancel, bump the generation | `Idle` |
| `KumikoIncrementalLoader.Reset()` / host `ResetLoadMore()` | cancel, bump the generation, clear `Failed`/`Completed`, re-arm | `Idle` |
| `KumikoSource.IncrementalSource` set to another object | cancel, bump the generation | `Idle` |
| `KumikoSource.Detach()` (PR #194; hosts call it from `Cleanup`/`Unloaded`, PR #197) | cancel, bump the generation | `Idle`; nothing evaluates while detached |
| `KumikoSource.Attach()` | none | `Idle`; Attach's rebuild repaints, and the first frame re-evaluates |
| `KumikoSource.Dispose()` | cancel, dispose the `CancellationTokenSource` | `Idle`, inert; later calls are no-ops, like the rest of PR #194 |
| `Mode` set to `None` | none (an in-flight batch finishes) | unchanged |
| client-side sort/filter/group change | none | unchanged |

### 7.2 Generation and stale completions

Each load captures `generation`. A completion whose generation is not current is ignored: no state change, no
event, no announcement. The items it may already have appended to the old collection are the app's business
(the grid no longer observes that collection after `SetItems`, or observed the `Reset` that superseded them).

### 7.3 Outcomes

- `OperationCanceledException` after the loader's own token was canceled: every cancellation source in 7.1
  bumps the generation first, so this completion is stale and ignored (7.2). No event, no state change.
- Any other exception, including an `OperationCanceledException` from a token the app owns (an `HttpClient`
  timeout surfaces as `TaskCanceledException`): `Failed`; `LastError` set; `LoadMoreCompleted` raised with
  `Error`, where a handler may set `ErrorText`. Exceptions never escape to the dispatcher (the loader awaits
  inside a `try` in a method that is not `async void`; the scheduled start is a non-throwing wrapper).
- Success: on the re-arm frame, `Idle` or `Completed` per `HasMoreItems`; `LoadMoreCompleted` with
  `ItemsLoaded` and `RowsAdded`.

### 7.4 Reentrancy rules

- `LoadMoreItemsAsync` may complete synchronously; the loader handles completion identically and schedules any
  follow-up evaluation through the next frame, never by recursion.
- Handlers of `LoadMoreRequested`/`LoadMoreCompleted`/`StateChanged` may call `Reset`, `LoadMoreAsync`, or
  replace the items. The loader reads its state after every callback and never assumes it unchanged.
- `KumikoSource.BeginUpdate` open while a batch arrives: the batch's view change waits for `EndUpdate` like any
  other; the re-arm gate (6.3) waits for that structural change.
- A load does not hold `BeginUpdate` open while it awaits the app (that would suppress redraws for the
  duration of a network call).

## 8. Threading, batching and view changes

### 8.1 Threading contract

- `IKumikoIncrementalSource.LoadMoreItemsAsync` is called on the UI thread, and the items must be appended to the
  bound collection on the UI thread (the same contract as WinUI's `ISupportIncrementalLoading`). The typical
  implementation awaits I/O without `ConfigureAwait(false)` and then appends.
- `KumikoIncrementalLoader` awaits the returned task **with** the captured context (deliberately not
  `ConfigureAwait(false)`), so its state changes, events and the re-arm happen on the UI thread. When there is
  no `SynchronizationContext` (plain Core use, tests) the continuation runs where the task completes; hosts
  always have one.
- Today a source that appends off the UI thread races the renderer (section 3). The spec does not fix that
  globally; #69 does ("Background `Add` from `Task.Run` throws the documented exception"). Until #69, the
  docs say "append on the UI thread" and `KumikoIncrementalCollection<T>` (8.2) does it for the app.

### 8.2 `KumikoIncrementalCollection<T>` (Core helper)

A ready-made `ObservableCollection<T>` subclass implementing `IKumikoIncrementalSource`, the Kumiko
counterpart of the Toolkit's `IncrementalLoadingCollection`:

```csharp
public class KumikoIncrementalCollection<T> : ObservableCollection<T>, IKumikoIncrementalSource
{
    public KumikoIncrementalCollection(
        Func<int /*startIndex*/, int /*count*/, CancellationToken, Task<IReadOnlyList<T>>> fetch,
        bool hasMoreItems = true);
    public bool HasMoreItems { get; set; }       // set false by the helper when fetch returns fewer than count
    public Task<int> LoadMoreItemsAsync(int count, CancellationToken cancellationToken);
    public void AddRange(IReadOnlyList<T> items); // one NotifyCollectionChangedAction.Add with NewItems.Count == items.Count
    public Task RefreshAsync(CancellationToken cancellationToken = default); // Clear (Reset) and HasMoreItems = true
}
```

`LoadMoreItemsAsync` calls `fetch(Count, count, ct)`, applies the result with one `AddRange` on the captured
context, and sets `HasMoreItems = result.Count >= count`. This makes the fast path in 8.3 the default.

### 8.3 Batched appends in `KumikoSource` (prerequisite)

`OnSourceCollectionChanged` and `TryIncrementalUpdate` change as follows:

1. **Multi-item `Add`.** `TryIncrementalUpdate` accepts `NewItems.Count >= 1`. With no sort, grouping or summary
   active, the visible new items are inserted into `_viewIndices` in one pass (one shift loop, not one per item)
   and the change is raised as one `KumikoViewChangedEventArgs.Insert(viewIndex, visibleCount, newRowKeys)`
   when the visible new rows are contiguous (always true for an append at the end), otherwise as one `Rebuild`.
2. **Append at the end** (`NewStartingIndex == _sourceItems.Count - NewItems.Count`) needs no shift of existing
   view indices at all: O(k) instead of O(n).
3. **Under a sort, grouping or summary**, a multi-item `Add` runs `RebuildCore` once per event (not per item) and
   raises one `Rebuild`. A later optimization (an O(n + k log k) merge of the sorted new indices into
   `_viewIndices`, and #69's incremental aggregates) is allowed but not required.
4. **Per-item adds inside `BeginUpdate`.** An append at the end does not invalidate any existing view index, so
   inside a batch it may defer its rebuild to `EndUpdate` (`_pendingRebuild = true`) instead of rebuilding
   immediately. This narrows the existing rule "collection changes still update the view immediately" to
   changes that move existing rows, and lets an app that adds one item at a time wrap its loop in
   `BeginUpdate`/`EndUpdate` to get one rebuild.
5. INCC `Reset` additionally calls `IncrementalLoading.OnSourceReset()` (section 7.1).

`ViewChanged` is therefore incremental where it can be (`Insert(oldRowCount, k)` for a plain append, which
`KumikoViewChangedEventArgs.MapRow` and the item-based `KumikoSelectionModel` already handle) and a single
`Rebuild` otherwise.

### 8.4 View version

`KumikoSource` gains two internal counters: `RequestedViewVersion`, incremented by every mutation that changes
the view structurally (whether or not `ViewChanged` has listeners), and `PublishedViewVersion`, the version the
renderer draws. With today's synchronous rebuilds they are always equal. With #69, `Requested` moves when a
mutation is queued and `Published` when its `ViewSnapshot` is swapped in on the UI thread. The renderer passes
`PublishedViewVersion` to `ObserveFrame`; the loader re-arms when it reaches the requested version it recorded
at batch completion.

### 8.5 Interaction with #69 (off-UI-thread view pipeline)

| #69 change | Effect here |
|---|---|
| Mutations UI-thread-only, with a documented exception | Matches 8.1. The loader awaits on the captured context; `KumikoIncrementalCollection<T>` appends there; the Uno adapter awaits `IAsyncOperation` on the UI thread. |
| Rebuilds on a worker from an immutable snapshot, published atomically | `ObserveFrame` evaluates against the `RowCount` of the snapshot being drawn (consistent with what the user sees); the re-arm gate (8.4) waits for the snapshot that contains the batch; the load-more row stays in `Loading` until then, so there is no flash of "nothing" between completion and publish. |
| Coalescing of rebuilds | Makes 8.3 items 3 and 4 automatic for any burst of adds; 8.3 remains useful for small sources rebuilt synchronously (< ~1,000 rows). |
| Incremental Sum/Count/Avg | Appending a batch updates table summaries in O(k) instead of O(n). |

Neither spec blocks the other: this one works on today's synchronous pipeline, and #69's acceptance tests should
include "append 50 items to a 100K-row sorted view with a Sum summary" as a load-more scenario.

## 9. The load-more row

### 9.1 Placement and layout

A single full-width band drawn **after the last row of the view** and **before the unpinned bottom summary rows**
(`style.PinBottomSummaryRows == false`). With pinned bottom summaries (the default) it is the last thing in the
scrolling content, above the pinned footer. It is not a row of the view: it is not counted in `RowCount`, has no
row key, is never selected, never edited, and does not appear in `MapRow`, `IndexOfItem` or summaries. Keeping it
out of the flat view leaves selection, keyboard navigation, `ViewChanged` mapping and #69's snapshot untouched.

Visible when:

| Mode \ State | Idle | Loading | Failed | Completed |
|---|---|---|---|---|
| None | hidden | loading row (a `LoadMoreAsync` call) | error row | hidden |
| Automatic | hidden | loading row | error row | hidden |
| Manual | "Load more" row (if `HasMoreItems`) | loading row | error row | hidden |

Height: `KumikoStyle.LoadMoreRowHeight` (default `float.NaN` = `RowHeight`). One internal helper,
`KumikoLayoutEngine.GetLoadMoreRowHeight(KumikoSource, KumikoStyle)`, returns 0 or the height, and is used by
every place that computes vertical geometry, so they cannot disagree:

- `KumikoRenderer.Render` (`scroll.ContentHeight`, the unpinned `bottomSummaryY`);
- PR #198's `KumikoScroller.RefreshContentExtents` (adds it to `ContentHeight`);
- `KumikoHitTester.HitTestCore` (section 9.4; the unpinned bottom summary top moves down by it);
- `KumikoInputController` row-drag drop-index math (the band is not a drop target; drops clamp to `RowCount`).

When the row disappears (a batch fills its place, or `Completed`), content height shrinks by its height and
`ScrollState.ClampOffset` handles a grid scrolled to the very bottom (a one-row settle at the end of data is
acceptable and expected).

### 9.2 Rendering

Drawn in the full-width pass (with group header and group summary rows, clipped to the data area), positioned
in content Y but **not** in content X: its text and indicator are centered in the viewport width minus the drag
handle, so it stays readable while the user is scrolled horizontally.

| State | Content |
|---|---|
| Loading | Busy indicator (three dots, `FillRoundRect` with radius = size / 2, animated opacity phase) + `KumikoStrings.LoadMoreLoading` |
| Manual idle | `KumikoStrings.LoadMoreButton`, drawn as a link/button (accent color) |
| Failed | `ErrorText ?? KumikoStrings.LoadMoreFailed` + a "Retry" pill (`KumikoStrings.LoadMoreRetry`), `FillRoundRect` + `DrawTextInRect` |

New `KumikoStyle` members (all versioned through `Set`, so `PaintCache` rebuilds only on change):
`LoadMoreRowHeight`, `LoadMoreRowBackgroundColor`, `LoadMoreTextColor`, `LoadMoreFont`,
`LoadMoreIndicatorColor` (default `AccentColor`), `LoadMoreErrorTextColor`. `KumikoTheme` presets set them.
`PaintCache` gains the corresponding `KumikoPaint`s.

**Animation.** The indicator needs repaints while it is visible and loading. The renderer exposes an internal
"needs animation frame" flag after `Render`; hosts drive it with the timer infrastructure they already have
(MAUI `IDispatcherTimer`, like the cursor blink timer; Uno `DispatcherTimer`, or #65's frame-callback loop if it
lands) at a low rate (about 8 fps, a phase change every 125 ms is enough for three dots) and stop the timer as
soon as the row is off screen or not loading. With the platform's reduce-motion setting on, the indicator is
static. The phase is an integer derived from a monotonic clock read by the host, so drawing allocates nothing.

**Allocation.** All strings come from `KumikoStrings` (stable instances); text measurement goes through the
existing cell text/measure caches (#68, #199); geometry is stack-only. `RenderAllocationTests` gains a case
with the loading row on screen and asserts zero bytes per frame.

### 9.3 Interaction

- **Pointer.** Tap (press and release inside the row, same rules as a cell tap) on the Manual row loads
  (`Trigger = Tap`); on the Failed row it retries (the whole row is the target, not only the pill, for touch
  size). On the Loading row nothing happens. No tap on the row changes selection or triggers
  `ClearSelectionOnEmptyAreaTap`.
- **Hover cursor** (#181, `KumikoCursor`): hand over an actionable row (Manual idle, Failed), default otherwise.
- **Keyboard.** The row is not a focus stop (the selection model knows nothing of it). Instead, when the current
  cell is on the last row and the user presses Down, PageDown, or Ctrl+End: in Automatic mode the loader is
  already near its threshold so nothing extra is needed; in Manual mode it requests a load
  (`Trigger = Keyboard`); in the Failed state it retries. After a batch arrives the current cell stays where it
  was, so pressing Down again moves into the new rows. (Open question Q5.)
- **Drag.** Row drag-drop never targets the band; column drag ignores it.

### 9.4 Hit testing

`HitRegion` gains `LoadMoreRow`, appended after `TableSummaryRow` (appending keeps existing values stable).
`KumikoHitTester.HitTestCore` order becomes: pinned bottom summary footer, group panel, header, top summaries,
sticky group headers, unpinned bottom summary rows, **load-more row**, frozen rows, scrolling rows, empty area.
`KumikoHitInfo.RowIndex` is `-1` for it. `KumikoHitTestResult` consumers that switch on `Region` treat it like
`TableSummaryRow` unless they handle it.

### 9.5 Accessibility

Announcements go through PR #192/#190's `KumikoAnnouncements` and honor the hosts' `AnnounceChanges`:

| Moment | String (new `KumikoStrings` member, default) |
|---|---|
| Load started by Tap/Keyboard/Retry (the user asked; announce immediately) | `AnnounceLoadMoreStarted` = "Loading more rows." |
| Load started by NearEnd and still running after 1 s (avoids chatter for fast loads) | same |
| Batch completed with rows added | `AnnounceLoadMoreCompleted` = "{0} more rows loaded." (rows added, culture-formatted) |
| Batch completed, `HasMoreItems` false | `AnnounceAllRowsLoaded` = "All rows loaded." |
| Failed | `AnnounceLoadMoreFailed` = "Could not load more rows. {0}" (the error text) |

The grid-level accessibility hint (`UpdateAccessibilityHint`, row count) updates through the existing structural
`ViewChanged` path. When #79 builds the virtual accessibility tree, the load-more row becomes a button element
in it ("Load more" / "Retry") with the busy state exposed; until then the keyboard route in 9.3 is the
non-pointer path.

### 9.6 Localization

New `KumikoStrings` members (English defaults, `with`-replaceable, format placeholders kept):
`LoadMoreLoading` = "Loading more...", `LoadMoreButton` = "Load more", `LoadMoreFailed` =
"Could not load more rows.", `LoadMoreRetry` = "Retry", plus the four `Announce*` strings above.
`LocalizationTests` gains the defaults and a `with` round trip. Number formatting uses `KumikoSource.Culture`.

## 10. Host APIs (MAUI and Uno, same names and defaults)

### 10.1 Surface

| Member | MAUI | Uno | Notes |
|---|---|---|---|
| `IncrementalSource` (`IKumikoIncrementalSource?`) | `BindableProperty` | `DependencyProperty` | Precedence 1 (section 4). Default null. |
| `LoadMoreCommand` (`ICommand?`) | `BindableProperty` | `DependencyProperty` | Precedence 4. `CanExecute(LoadMoreCommandParameter)` = has more. |
| `LoadMoreCommandParameter` (`object?`) | `BindableProperty` | `DependencyProperty` | |
| `LoadMoreMode` (`KumikoLoadMoreMode`) | `BindableProperty` | `DependencyProperty` | Default `Automatic`. |
| `LoadMoreThreshold` (`int`) | `BindableProperty` | `DependencyProperty` | Rows; default `-1` (one viewport). |
| `LoadMoreBatchSize` (`int`) | `BindableProperty` | `DependencyProperty` | Default `0` = auto: `max(20, 2 × rows per viewport)`. Passed as `count`. |
| `IsLoadingMore` (`bool`) | `BindableProperty`, `TwoWay` | `DependencyProperty` | Mirrors `State == Loading`. On the command path the view model ends a load by setting it false. |
| `LoadMoreErrorText` (`string?`) | `BindableProperty`, `TwoWay` | `DependencyProperty` | Command path: the view model sets it to fail the load. Mirrors `ErrorText` otherwise. Cleared on retry. |
| `LoadMoreState` (`KumikoLoadMoreState`) | read-only `BindableProperty` (`BindablePropertyKey`) | `DependencyProperty`, documented read-only | For status UI outside the grid. |
| `LoadMoreRequested` | event | event | `KumikoLoadMoreRequestedEventArgs`, re-raised from Core. |
| `LoadMoreCompleted` | event | event | `KumikoLoadMoreCompletedEventArgs`; a handler can set `ErrorText`. |
| `Task<int> LoadMoreAsync(CancellationToken = default)` | method | method | `Trigger = Programmatic`; works in every mode; joins an in-flight load. |
| `void ResetLoadMore()` | method | method | Section 6.4 and 7.1. |

Style members (`LoadMoreRowHeight` and colors) are reached through `GridStyle` like the other style members
(and through #57's bindable style options if that pattern is extended). Strings through `DataSource.Strings`.

All events are raised on the UI thread. The hosts' `Scrolled` event (#56) stays as is and remains the hook for
apps that want their own scheme.

### 10.2 Wiring

- Each host forwards its properties to `_dataSource.IncrementalLoading` in the property-changed callbacks
  (MAUI `propertyChanged`, Uno static `PropertyMetadata` callbacks, mirroring the existing pattern in
  `KumikoDataGrid.Properties.cs`), and mirrors `StateChanged` back into `IsLoadingMore`, `LoadMoreState` and
  `LoadMoreErrorText` with a reentrancy flag (the same pattern as #56's `_pushingScrollState`).
- `OnDataSourceViewChanged` and `ScrollState.ScrollChanged` already invalidate; nothing else is needed for
  evaluation (section 6.2). The paint handler starts or stops the indicator timer from the renderer's
  "needs animation frame" flag.
- Lifecycle: PR #197's `Cleanup`/`Unloaded` already call `_dataSource.Detach()`, which cancels (7.1); the
  indicator timer stops with the other timers in `Cleanup` (`StopInertialScrollTimer`, `StopCursorBlinkTimer`).
- Code-behind-only partial file per host: `KumikoDataGrid.LoadMore.cs`.

### 10.3 Command adapter (both hosts, internal)

`CommandIncrementalSource : IKumikoIncrementalSource`, created when `LoadMoreCommand` is the effective source:

- `HasMoreItems` → `LoadMoreCommand.CanExecute(LoadMoreCommandParameter)`, read only outside `Loading` (an
  async command such as CommunityToolkit.Mvvm's `AsyncRelayCommand` reports `CanExecute == false` while it runs,
  which must not read as "no more items"). `CanExecuteChanged` is the change notification of 5.2: it repaints,
  and moves `Completed` back to `Idle` when `CanExecute` now returns true.
- `LoadMoreItemsAsync(count, ct)` → sets `IsLoadingMore = true`, clears `LoadMoreErrorText`, executes the command
  with `LoadMoreCommandParameter` (the batch size is available on `LoadMoreRequested` for apps that need it), and
  returns a task that completes when the view model sets `IsLoadingMore = false` (result: rows the bound
  collection gained meanwhile, counted from INCC `Add` events) or faults when it sets `LoadMoreErrorText`
  (a `KumikoLoadMoreException` carrying the text). Cancellation completes it as canceled; the view model is not
  told except through `LoadMoreRequested`'s token (open question Q6 discusses an async-command path).
- If `Execute` throws, the load fails with that exception.

### 10.4 Uno: `ISupportIncrementalLoading` adapter

`SupportIncrementalLoadingAdapter : IKumikoIncrementalSource` in `KumikoUI.Uno`, created when `ItemsSource`
implements `Microsoft.UI.Xaml.Data.ISupportIncrementalLoading` and nothing with higher precedence is set:

- `HasMoreItems` → the collection's `HasMoreItems`.
- `LoadMoreItemsAsync(count, ct)` → `LoadMoreItemsAsync((uint)count)`; `ct.Register(op.Cancel)`; await the
  operation as a task on the UI thread; return `(int)result.Count`.
- `LoadMoreThreshold` defaults are unchanged; the docs map WinUI's `IncrementalLoadingThreshold` (pages) to
  `LoadMoreThreshold` (rows ≈ pages × rows per viewport) and `DataFetchSize` to `LoadMoreBatchSize`.
- The Toolkit's `IncrementalLoadingCollection` adds items one at a time: the adapter wraps the await in nothing
  (no `BeginUpdate`, per 7.4), so with a client sort active this is N rebuilds until #69's coalescing lands. The
  docs recommend `KumikoIncrementalCollection<T>` for sorted grids.

## 11. Scroll anchoring when items arrive

- **Plain append (no transform):** rows are added after the last row, existing rows keep their indices, the
  offset does not change. Content grows; the load-more row moves down by the batch height. Nothing to do.
- **Rows inserted above the viewport** (client sort or grouping placed new items earlier, or a structural
  change during the batch): the host keeps the first fully visible scrolling row as the anchor. Before the
  structural change it records the anchor's row key and its screen Y (`KumikoScroller.GetRowY`); in the
  `ViewChanged` handler it finds the anchor's new row (`KumikoViewChangedEventArgs.MapRow`) and calls
  `ScrollState.ScrollTo` (PR #198, one `ScrollChanged`) so the anchor keeps its screen Y. If the anchor is gone
  (filtered out) it falls back to the nearest surviving row before it.
- **Scope.** Anchoring applies to structural changes raised while a batch is being applied (from completion
  until the re-arm frame) and to `Insert` events above the anchor at any time. User-initiated sort/filter/group
  changes keep today's behavior (the offset stays numerically the same), because there the user expects the
  view to change.
- **Inertia.** `InertialScroller` applies deltas through `ScrollBy`, so an anchoring adjustment mid-fling
  composes with the fling instead of fighting it.
- **Allocation.** The anchor record is a key reference and a float in host fields; the existing structural
  `ViewChanged` already allocates its row-key array, so anchoring adds nothing per frame.
- Core helper: `KumikoScroller.CaptureAnchor(...)` / `RestoreAnchor(...)` (internal or public, decided in
  implementation), unit-tested in Core rather than per host.

## 12. Summaries over partially loaded data

- Table, group and caption summaries keep computing over the **loaded** rows (no special-casing in
  `KumikoSource`'s summary code). That is correct for "what you see" and is what users of the MAUI vendors'
  grids get.
- `KumikoIncrementalLoader.IsPartial` (true while a source is set and the state is not `Completed`) lets the
  renderer mark summaries as partial: when `KumikoStyle.MarkPartialSummaries` is true, summary values render
  through `KumikoStrings.PartialSummaryFormat` (default "{0}+"). Default **off** (open question Q3). The flag is
  read at draw time, so flipping to `Completed` is a redraw, not a summary recompute.
- Exact totals over the full remote dataset are the app's job: a `SummaryColumnDescription` with
  `SummaryType.Custom` whose `CustomAggregate` returns a server-provided total (and the app calls
  `KumikoSource.Refresh()` when that total changes). The cookbook shows it.
- Group header counts ("(12)") count loaded rows. Documented.

## 13. Programmatic `ScrollToItem` for items not loaded yet

PR #198's `ScrollToItem` returns false when the item has no row. With incremental loading:

- **v1 (no new API):** unchanged. The docs show the app-side loop with `LoadMoreAsync` and
  `KumikoSource.IndexOfItem`:
  ```csharp
  while (grid.DataSource.IndexOfItem(target) < 0 && grid.LoadMoreState != KumikoLoadMoreState.Completed)
      if (await grid.LoadMoreAsync(ct) == 0) break;
  grid.ScrollToItem(target, KumikoScrollPosition.Center);
  ```
- **Later (issue 7, optional):** `Task<bool> ScrollToItemAsync(object item, KumikoScrollPosition position =
  Nearest, int maxBatches = 10, CancellationToken ct = default)` on both hosts, running the loop above with the
  loading row visible and an announcement per batch. Items are matched like `IndexOfItem`; a predicate overload
  (`Func<object, bool>`) covers "scroll to the order with id 42" when the instance does not exist yet. Apps that
  can ask the server for an item's position should page directly instead (#107).
- **Bidirectional / random access** is a non-goal; `KumikoLoadMoreTrigger` and the loader are named so a future
  `Direction` (end/start) can be added without renaming.

## 14. Zero-allocation constraints

| Path | Constraint |
|---|---|
| `ObserveFrame` (every frame) | No allocation, no LINQ, no closures, no interface enumerators; one `HasMoreItems` property read; integer math. |
| Drawing the load-more row | No allocation: cached strings, `PaintCache` paints, measure cache, stack geometry. Covered by `RenderAllocationTests`. |
| Hit testing (every hover move, `HitTestCore`) | Value result as today; the band check is float math. |
| Per batch (not per frame) | Allowed: the async state machine, the scheduled start, `CancellationTokenSource` (reused across batches with `TryReset` when not canceled), event args, the structural `ViewChanged` row-key array, announcement strings. |
| Indicator timer | Runs only while the row is visible and loading; no per-tick allocation (cached tick delegate). |

## 15. Test plan

Core (`tests/KumikoUI.Core.Tests`, new `IncrementalLoading/` folder; fakes with `TaskCompletionSource`, a
deterministic scheduler seam and a single-threaded `SynchronizationContext` helper):

1. State machine: every transition in 5.2, including `HasMoreItems` false at bind (`Completed` with no load),
   `Failed` never auto-retrying, `Completed` leaving only through `Reset`, a source change or a change
   notification (`HasMoreItems` `PropertyChanged`, `CanExecuteChanged`), never by polling.
2. Threshold math (table-driven): `Threshold` -1/0/n; frozen rows; top summaries; pinned and unpinned bottom
   summaries; group panel; sticky group headers; viewport shorter than one row; empty view.
3. Once per batch: many `ObserveFrame` calls while loading start one load; `LoadMoreAsync` while loading joins
   the in-flight task; the re-arm gate waits for `PublishedViewVersion` to reach the recorded requested version
   (a batch that added nothing re-arms on the next frame); a batch completing inside `BeginUpdate` re-arms only
   after `EndUpdate`.
4. Fill-the-viewport chaining; disarm after a batch that adds no visible row; re-arm on downward scroll,
   viewport growth, user filter change, `Reset`.
5. Cancellation: `SetItems`, INCC `Reset`, `IncrementalSource` replaced, `Detach`, `Dispose` cancel the token;
   stale completions (old generation) change nothing and raise nothing; `Attach` re-evaluates.
6. Outcomes: a canceled load completing late is ignored; other exceptions (including an app-token
   `OperationCanceledException`) → `Failed` with `LastError`; `ErrorText` set by a
   `LoadMoreCompleted` handler is drawn; synchronous completion does not recurse.
7. Batched appends (8.3): one multi-item `Add` → one `Insert(oldRowCount, k)`; append under sort → exactly one
   `RebuildCore` and one `Rebuild` (count rebuilds via `ViewChanged`); non-contiguous visible inserts →
   `Rebuild`; filtered-out new items; per-item appends inside `BeginUpdate` → one rebuild at `EndUpdate`;
   selection survives (item-based).
8. Layout parity: `KumikoRenderer` content height equals `KumikoScroller.RefreshContentExtents` for every
   footer state (guards against the duplicated formula drifting).
9. Rendering (`RecordingDrawingContext`): each footer state's command sequence; placement before unpinned and
   above pinned summaries; centered in the viewport when scrolled horizontally; reduced-motion static indicator.
10. `RenderAllocationTests`: zero bytes per frame with the loading row visible and animating.
11. Hit testing (`HitTesterTests`): `LoadMoreRow` in each state and its precedence against the neighbors in 9.4.
12. Input (`PointerGestureTests`, `KeyboardNavigationTests`): tap loads/retries and does not touch selection;
    Down/PageDown/Ctrl+End on the last row in Manual and Failed; nothing in Automatic beyond the threshold
    path; row drag cannot drop on the band; hover cursor.
13. Anchoring: inserts above the viewport keep the anchor's screen Y; anchor removed → fallback; user sort does
    not anchor.
14. Strings and announcements (`LocalizationTests`, `KumikoAnnouncementsTests`): defaults, `with` round trip,
    the 1 s delayed start announcement, culture-formatted counts.
15. `KumikoIncrementalCollection<T>`: `AddRange` raises one event; `HasMoreItems` from short pages; `RefreshAsync`
    resets; cancellation.
16. Performance (`PerformanceBudgetTests`): 100 batches of 50 appended to a 10K-row view sorted on two columns
    with a Sum summary stay within the frame budget per batch.

Hosts (`tests/KumikoUI.Uno.Tests`, and MAUI host tests once #84 / PR #208 lands):

17. Property mapping both ways (`IsLoadingMore`, `LoadMoreErrorText`, `LoadMoreState`) without feedback loops.
18. Command adapter: `CanExecute` gating, completion via `IsLoadingMore = false`, failure via
    `LoadMoreErrorText`, `Execute` throwing.
19. Uno `ISupportIncrementalLoading` adapter: count mapping, `Cancel` on token cancel, precedence rules.
20. Lifecycle (`HostLifecycleLeakTests` from PR #197): unload during a load cancels it and leaks nothing;
    reload re-evaluates.

Manual / samples:

21. A "Load more" page in `samples/SampleApp.Maui` and `samples/SampleApp.Uno`: simulated latency, a failure
    toggle, a mode picker (Automatic/Manual), sort/filter/group toggles, a summary row; checked with
    VoiceOver/TalkBack/Narrator for the announcements.

## 16. Staged implementation issues

Each issue is one branch and one PR, in order; 1 and 2 can run in parallel.

1. **Batched collection appends in `KumikoSource` (multi-item Add, append fast path, view versions)**
   - [ ] A multi-item INCC `Add` with no sort/group/summary raises one `ViewChanged` `Insert` (contiguous) or one
     `Rebuild`; an append at the end shifts no existing view index.
   - [ ] Under sort/group/summaries a multi-item `Add` rebuilds once and raises one `Rebuild`.
   - [ ] Appends at the end inside `BeginUpdate` defer their rebuild to `EndUpdate`; other collection changes keep
     today's immediate behavior; XML docs of `BeginUpdate` updated.
   - [ ] Internal `RequestedViewVersion`/`PublishedViewVersion` (equal until #69) increment once per structural
     change.
   - [ ] Tests from 15.7; existing `IncrementalUpdateTests`/`ViewChangedTests` pass.

2. **Core: `IKumikoIncrementalSource`, `KumikoIncrementalLoader` and lifecycle integration** (blocked by #56, #28)
   - [ ] Types in 5.1 with XML docs; `KumikoSource.IncrementalSource` and `IncrementalLoading`; detection on
     `SetItems`.
   - [ ] State machine (5.2), threshold evaluation via internal `ObserveFrame` (6.1 to 6.3), scheduling seam.
   - [ ] Cancellation table 7.1 including PR #194's `Detach`/`Attach`/`Dispose`; generation-based stale
     completion handling; outcomes 7.3.
   - [ ] `KumikoIncrementalCollection<T>` (8.2).
   - [ ] Tests 15.1 to 15.6 and 15.15.

3. **Core: the load-more row (layout, rendering, hit testing, input, strings, announcements)** (blocked by 2;
   announcements by #77/#78)
   - [ ] `GetLoadMoreRowHeight` used by `KumikoRenderer`, `KumikoScroller.RefreshContentExtents`,
     `KumikoHitTester`, `KumikoInputController`; parity test 15.8.
   - [ ] Drawing per 9.2 with the new `KumikoStyle` members and theme values; `HitRegion.LoadMoreRow` appended;
     tap/keyboard/hover per 9.3.
   - [ ] `KumikoStrings` members (9.6) and `KumikoAnnouncements` entries (9.5).
   - [ ] Renderer "needs animation frame" flag; reduced-motion static indicator.
   - [ ] Tests 15.9 to 15.12 and 15.14; `RenderAllocationTests` still zero.

4. **Hosts: MAUI and Uno load-more API, command adapter and indicator timer** (blocked by 3)
   - [ ] Section 10.1 surface on both hosts with identical names, defaults and XML docs (`KumikoDataGrid.LoadMore.cs`).
   - [ ] Two-way mirroring without feedback; events on the UI thread.
   - [ ] Command adapter (10.3); indicator timer started/stopped from the renderer flag and in `Cleanup`.
   - [ ] Tests 15.17, 15.18, 15.20; sample pages (15.21) on both hosts.

5. **Uno: `ISupportIncrementalLoading` interop** (blocked by 4)
   - [ ] Adapter (10.4) with precedence (section 4); cancellation via `IAsyncInfo.Cancel`.
   - [ ] Works on the Skia desktop, WebAssembly and mobile heads; tests 15.19.
   - [ ] Docs mapping `IncrementalLoadingThreshold`/`DataFetchSize`; sample with the Toolkit collection if the
     dependency is acceptable for the sample only.

6. **Scroll anchoring for batch inserts and partial-summary marking** (blocked by 3)
   - [ ] Anchor capture/restore per section 11 in Core, used by both hosts; tests 15.13.
   - [ ] `KumikoStyle.MarkPartialSummaries` + `KumikoStrings.PartialSummaryFormat`, read at draw time.

7. **Docs and cookbook; optional `ScrollToItemAsync`** (blocked by 4)
   - [ ] README section and cookbook: interface, `KumikoIncrementalCollection<T>`, MVVM command, Uno interop,
     server-side sort/filter re-query (6.4), server totals (12), scroll-to-unloaded loop (13).
   - [ ] If Q8 is answered yes: `ScrollToItemAsync` (and predicate overload) on both hosts with tests.
   - [ ] Performance test 15.16 recorded in the benchmarks used for the README numbers (#113).

## 17. Open questions

- Q1. Default `LoadMoreThreshold`: `-1` (one viewport, prefetch) as proposed, or a fixed row count (MAUI users
  may expect `-1` to mean "off", as `RemainingItemsThreshold` does)?
- Q2. Should Automatic mode fill the viewport on first bind when the collection is empty or short (WinUI
  behavior, proposed), or wait for the first scroll?
- Q3. Mark partial summaries by default (`MarkPartialSummaries = true`, "{0}+") or leave them unmarked (proposed)?
- Q4. Is the "disarm after a batch that adds no visible row" rule enough, or do we also want a hard cap on
  chained automatic batches (for a filter matching one item per batch)?
- Q5. Keyboard access to the Manual/Failed row via Down/PageDown/Ctrl+End on the last row (proposed), or a real
  focus stop on the row (needs the selection model or #79's virtual tree to know about it)?
- Q6. Command path: is `IsLoadingMore`/`LoadMoreErrorText` round-tripping (RefreshView pattern) enough, or should
  the adapter also recognize an async command (any `ICommand` exposing `ExecuteAsync(object?)` returning
  `Task`) by convention, without a CommunityToolkit.Mvvm dependency?
- Q7. Animate the busy indicator (timer cost and battery while visible) or draw it static everywhere?
- Q8. Ship `ScrollToItemAsync` in the grid, or keep it as a documented app-side loop?
- Q9. Uno: confirm `IAsyncOperation<LoadMoreItemsResult>`-to-`Task` conversion and `Cancel` work on all Uno heads
  (Skia, WebAssembly, iOS/Android) without extra package references.
- Q10. Relationship with #107: can paging and incremental loading be active together (proposed: no; the
  paging spec decides which wins and documents it), and should both share the query-change hook?
- Q11. Should client-side sorting be allowed at all while `HasMoreItems` is true, or should the grid warn
  (debug trace) because "sorted among loaded rows" surprises users?

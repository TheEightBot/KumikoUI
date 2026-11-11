# Spec: paging and DataTable / dictionary / dynamic row sources (#107)

| | |
|---|---|
| Issue | [#107](https://github.com/TheEightBot/KumikoUI/issues/107) (plan key X6, Epic X, 1.x fast follow-on) |
| Findings | CORE-33 (only POCO property access; no paging hook), research section 1.3 ([07-external-research.md](../release-readiness/07-external-research.md): paging and DataTable binding in 2 of 7 competitors) |
| Builds on | #17 AOT-safe accessors (merged: `IKumikoAccessorProvider`, `KumikoAccessorRegistry`, `KumikoPropertyAccessor`), #18 per-item accessors, #20 `ViewChanged`, #28 source lifecycle, #30 item selection, #32 typed conversion |
| Designed on top of (unmerged) | #104 incremental loading spec (PR #221), #38 tri-state Boolean (PR #217), #36 popup layout (PR #212), #44 clipboard (PR #211) |
| Interacts with | #106 selection column (header check-all scope), #105 export, #108 cell selection, #69 off-UI-thread view pipeline, #76 AOT publish jobs, #77 / #78 accessibility |
| Status | Draft for review |

## 1. Summary

Two independent tracks that share an issue because both widen "what can the grid bind to".

**Track A, paging.** The grid shows one page of rows at a time, with a pager. Two modes:

- **Client paging**: the whole collection is in memory; the grid sorts, filters and groups all of it, then shows
  one page (`KumikoPager.PageSize > 0`).
- **Server paging**: the app implements `IKumikoPagedSource`; the grid asks it for one page at a time with the
  current sort and filter as a query (`KumikoPageRequest`), and never sees the rest.

A **headless pager model** (`KumikoPager`) lives in Core on `KumikoSource`, like #104's loader. A **drawn pager
bar** (`DrawnPager`) is the default UI; hosts also expose the model as bindable properties and commands, so an app
can build its own pager in XAML. Paging and #104's load-more are **mutually exclusive** per source.

**Track B, row sources.** Rows that are not POCOs: `DataView`/`DataTable`, `IDictionary<string, object?>` rows,
`ExpandoObject` and `DynamicObject`. The accessor seam from #17 (`IKumikoAccessorProvider`) already exists and
is the right shape, so this is mostly providers, plus four small gaps in Core that the providers hit:

1. a `KumikoColumn.ValueType` hint, because these accessors report `object` and edits would not convert;
2. an `IBindingList` adapter in `KumikoSource` (a `DataView` raises `ListChanged`, not `CollectionChanged`);
3. column factories, because there are no properties to discover columns from;
4. a decision about where `System.Data` lives (a new opt-in package, `KumikoUI.Data`, so Core and every trimmed
   app that does not use `DataTable` stay free of it).

Dictionary, `ExpandoObject` and `DynamicObject` support ship **in Core**, on by default, reflection free, with no
new dependency.

## 2. Goals and non-goals

### Goals

- Client paging and a server paging contract, with page size, page index, page count and total as bindable
  state on both hosts.
- A drawn pager with first/previous/numbers/next/last, a page-size picker and a "51-100 of 1,234" label, plus a
  headless model so apps can supply their own.
- A defined meaning for sort, filter, group, summaries, selection, editing and row reorder under paging, in
  both modes.
- A defined relationship to #104: exclusive, with a shared query-change event.
- `DataView` rows (columns from `DataColumn`s, change tracking from `ListChanged`, edits through
  `DataRowView`), dictionary rows, `ExpandoObject` and `DynamicObject` rows, all working under Native AOT.
- No reflection on any new path; the existing `IsReflectionPropertyAccessSupported` switch stays false-safe.
- Trimming annotations that state what is and is not safe, and performance targets for the new accessors.

### Non-goals

- A query language (LINQ expression trees, OData). `KumikoPageRequest` carries the grid's own sort and filter
  descriptors; mapping them to a backend is the app's job.
- Server-side grouping and server-side summaries. Grouping applies to the rows of the current page; totals come from
  the app (section 6.6).
- Infinite scrolling. That is #104. An adapter lets one backend serve both (section 6.8, optional).
- `DataSet` relations (master-detail from `DataRelation`). Row details are a separate feature.
- Binding a `DataTable` directly as `ItemsSource`. The hosts' `ItemsSource` is `IEnumerable`; `DataTable` is not
  one. Apps bind `table.DefaultView` (section 9.3).
- Virtualization of dictionaries that are lazily materialized (an `IAsyncEnumerable`). Out of scope.

## 3. Current state (main at c77f44b)

- `KumikoSource` (`Models/KumikoSource.cs`) holds `_sourceItems` (an `IList`), builds `_viewIndices` (filtered and
  sorted source indices) or a flat grouped view (`_flatView`) in `RebuildCore`, and reads items by index. It
  follows `INotifyCollectionChanged` (`OnSourceCollectionChanged`, with an incremental path for simple adds and
  removes) and `INotifyPropertyChanged` of each item (`TrackItem`; `OnItemPropertyChanged` decides between a
  redraw and a rebuild). `SetItems(IEnumerable)` materializes a non-`IList` into a `List<object>`. Null items are
  skipped. It has no concept of a page, a total, or a remote query.
- Items are read through `KumikoColumn.PropertyName` (a path of dot-separated segments, resolved by
  `KumikoValuePath` in `Data/KumikoValuePath.cs`) or `KumikoColumn.ValueAccessor`/`ValueSetter`. Each segment
  resolves through `KumikoAccessorRegistry.GetSegmentAccessor(type, name)`, cached by `(Type, name)`, in this
  order: providers registered for the item's type or a base type (`Register`, which is also how the
  `[KumikoGridItem]` generator registers), global providers (`AddProvider`), reflection when
  `IsReflectionPropertyAccessSupported`. When none applies the cell reads null and a warning is traced once.
- `IKumikoAccessorProvider.GetAccessor(Type itemType, string propertyName)` returns a `KumikoPropertyAccessor`
  (a name, a `PropertyType`, a getter and a setter delegate) or null. Its documentation already says it "can also
  serve items without CLR properties, for example dictionaries or data rows". **The seam #107 asked for exists**
  (the issue calls it `IPropertyAccessorProvider`; the shipped name is `IKumikoAccessorProvider`).
  Nothing implements it for dictionaries or data rows yet, so those rows show blank cells (CORE-33).
- `KumikoValuePath.TrySetValue` converts an edited value to the accessor's `PropertyType` with
  `KumikoValueConverter` (public, culture and `KumikoStrings` aware) unless the value is already an instance of that
  type. An accessor whose `PropertyType` is `object` therefore accepts the raw edited value, whatever it is.
- `KumikoColumn` has `ColumnType` (`Text`, `Numeric`, `Boolean`, `Date`, ..., and `IsThreeState` after #38),
  `Format`, `IsReadOnly`, `ValueAccessor`, `ValueSetter`. There is no column auto-generation of any kind (not even
  for POCOs, which would need reflection).
- `KumikoSource.SetFilter(Func<object, bool>)` is an opaque predicate. `SortColumnsChanging/Changed`,
  `FilterChanging/Changed` and `GroupExpanding` events exist (cancelable `*Changing`). `ActiveSortColumns` and
  `ActiveFilters` (`FilterDescription`, which holds its `KumikoColumn`, `SelectedValues`, `Condition1/2`,
  `Combination`) describe the query. `GroupDescriptions` describes grouping.
- Table summaries (`TableSummaryRow`) are computed in `ComputeSummaryRow` over `_viewIndices`, i.e. over every
  filtered row; group and caption summaries over each group's rows.
- `ReorderRow` is supported "only when grouping is not active".
- The hosts' `ItemsSource` is `IEnumerable` (`KumikoUI.Maui/KumikoDataGrid.cs`, `KumikoUI.Uno/KumikoDataGrid.Properties.cs`).
- Core's csproj is `IsAotCompatible`; the AOT publish jobs (#76) run a console smoke test. Core references no
  assembly outside the framework's base libraries, and no `System.Data`.
- `KumikoSelectionModel` tracks selected items by reference (`ReferenceEqualityComparer`), drops items that leave
  the view, and row keys are items (`GetRowKey`).

Unmerged work this design assumes: **#104 (PR #221)** `IKumikoIncrementalSource`, `KumikoIncrementalLoader` owned
by `KumikoSource`, a "load-more row" after the last row, generation-based cancellation of stale loads, and the
statement that "#107 owns `DataTable`/dictionary sources and query descriptors". **#38 (PR #217)** `IsThreeState`.
**#106 (this PR)** `KumikoSelectionModel.CheckAllState` over `DataRowCount`.

## 4. Concepts and terminology

- **Page**: a contiguous slice of the sorted, filtered data rows, `PageSize` long (the last may be shorter).
  `PageIndex` is zero-based.
- **Query**: the grid's sort, filter and group state: `ActiveSortColumns`, `ActiveFilters`, `GroupDescriptions`.
- **Client mode / Server mode**: whether the grid owns the full collection or asks a source for each page.
- **Row source**: what `SetItems` receives. **Row type**: the runtime type of its items (`Dictionary<string, object?>`,
  `DataRowView`, `ExpandoObject`, a POCO).
- **Accessor**: a `KumikoPropertyAccessor` (getter/setter delegates) the registry resolves per `(row type, name)`.
- **Schema-bound row type**: a row type whose available names depend on the instance, not the type
  (`DataRowView`, dictionaries). Its accessor cannot be specific to a name or type; section 8.2.

## 5. Paging: models and decisions

### 5.1 Recommended design

Three layers, as #104:

1. **Core contract.** `IKumikoPagedSource` (server mode) and the request/response types.
2. **Core state.** `KumikoPager`, owned by `KumikoSource` (shares its lifecycle: `SetItems`, collection `Reset`,
   `Detach`, `Attach`, `Dispose` from #28), exposes page state and runs page changes (a slice in client mode, an
   async load in server mode).
3. **UI.** The drawn `DrawnPager` bar, and host properties that bind the same `KumikoPager`.

### 5.2 Client-side vs server-side

| | Client paging | Server paging |
|---|---|---|
| Configure | `Paging.PageSize = 50` | `Paging.PagedSource = source` |
| Items in memory | all | one page |
| Sort / filter / group | the grid, over all items | sort and filter go to the source; grouping is applied to the page |
| Total | computed (filtered count) | the page's `TotalCount`, or unknown |
| Best for | up to some 100K rows where a paged UX is wanted | data that does not fit, or lives behind an API |

They are two modes of one `KumikoPager`, selected by what is set (`Mode` is derived, never assigned):
`PagedSource != null` is `Server`, else `PageSize > 0` is `Client`, else `None`.

### 5.3 Pager UI: drawn vs host-provided

Both, with one model:

- **Drawn (default).** `DrawnPager` is a `DrawnComponent` bar docked at the bottom (or top) of the grid:
  first/previous, a window of page numbers with ellipses, next/last, a page-size picker (a `DrawnComboBox`), and a
  label ("51-100 of 1,234"). Same look on both hosts, themed from `KumikoStyle`, touch-sized, accessible (section 5.12).
  It is shown whenever `Paging.Mode != None` and `PagerPosition != None`.
- **Host-provided.** Both hosts expose `PageIndex` (two-way), `PageSize` (two-way), `PageCount`, `TotalItemCount`
  and `IsPageLoading` (read-only), `PagerPosition`, `PageChangedCommand`, so an app can hide the drawn bar
  (`PagerPosition = None`) and draw its own with a `Slider`, `Button`s or the platform's pager. The model is what
  the host properties forward to; neither UI has logic the other lacks.

Rejected: a host-native pager control only (two implementations to keep alike, and it leaves the visual
inconsistency the project avoids); a drawn pager only with no model (apps with their own design systems could not
adapt).

### 5.4 Page size and page index

- `PageSize` (int, default 0 = paging off). `PageSizeOptions` (default `{ 10, 25, 50, 100 }`) feeds the pager's
  picker; the picker is hidden when it has fewer than two options. Changing `PageSize` keeps the first row of the
  current page visible: the new `PageIndex` is `firstRowIndex / newPageSize`.
- `PageIndex` is zero-based, clamped to `[0, PageCount - 1]`. The label and the number buttons are one-based.
  Setting it outside the range clamps; it does not throw. The setter raises `PageChanging` (cancelable), then
  `PageChanged`. In server mode `PageChanged` is raised when the load completes.
- `PageCount` is `ceil(TotalItemCount / PageSize)`, or -1 (unknown) in server mode when the source gave no
  total (section 5.11).
- Binding: `PageIndex` and `PageSize` are two-way bindable on MAUI (`BindableProperty`, `BindingMode.TwoWay`) and
  Uno (`DependencyProperty`); an app's view-model value is applied at attach and after `ItemsSource` changes.

### 5.5 Client mode: where the slice sits in the pipeline

`RebuildCore` today is: filter, sort, group. With client paging it becomes:

```text
all items -> filter -> sort -> _filteredIndices (kept) -> slice [PageIndex*PageSize, +PageSize) -> _viewIndices -> group -> flat view
```

- `_filteredIndices` is the full sorted, filtered index list (4 bytes per row). `TotalItemCount` is its count.
  `_viewIndices` becomes the page's slice, so nothing downstream (`GetItem`, `RowCount`, selection, rendering)
  changes.
- **Grouping applies to the page's rows**, not to all rows. A group can therefore continue across pages: the same
  group header appears on each page it has rows on, with the page's count. Paging over group blocks (a page is N
  top-level groups) was considered and rejected for v1: page sizes would vary wildly and the "N rows per page"
  contract the pager shows would be false. See Q2.
- Incremental updates (`TryIncrementalUpdate`) are skipped while paging; a collection change rebuilds. That is
  O(n) per change, the same as a sorted view's worst case today, and acceptable for the client sizes this mode is
  for. An optimization (only rebuild when the change falls in or before the page) is a later stage.
- After any rebuild the page index is re-clamped (rows removed or filtered can leave the index past the end).

### 5.6 Sort, filter and group interaction

| Action | Client mode | Server mode |
|---|---|---|
| Sort a column (`ToggleSort`, `SetSort`) | all rows re-sorted, `PageIndex` resets to 0 | `SortColumnsChanging/Changed` fire as today; the grid does **not** sort locally; the source gets a new request, `PageIndex` resets to 0 |
| Filter (`SetColumnFilter`) | all rows re-filtered, `PageIndex` resets to 0, `PageCount` recomputed | as sort; the filter descriptors go to the source |
| `SetFilter(Func<object, bool>)` | works (applies before the slice) | throws `InvalidOperationException`: an opaque predicate cannot be sent to a source |
| Group (`AddGroupDescription`, ...) | applies to the page (section 5.5); changing it keeps `PageIndex` | applies to the loaded page only; the descriptors are in the request so a source that can group may ignore the client view |
| Edit a cell that changes the sort key | the row stays where it is until the next rebuild, as today | same; the app reloads |
| `ToggleGroupExpansion`, collapse | unaffected | unaffected |
| `ReorderRow` | not supported while paging is on (returns without effect) | same |

`KumikoPager.ResetPageOnQueryChange` (default true) controls the reset. The reset and the new request happen
in one step, so a server source never sees an out-of-range page for the new query.

**Server mode: the filter popup.** `GetUniqueColumnValues` lists values from the loaded page only. That is
misleading for a value list. In server mode the popup shows the page's values with a footer note
("Showing values on this page", `KumikoStrings.FilterPageValuesOnly`) unless the source also implements the optional
`IKumikoDistinctValuesSource` (section 6.8), whose `GetDistinctValuesAsync(propertyName, query, ct)` the popup
awaits. Condition-based filters (contains, greater than, ...) work in both modes.

### 5.7 Summaries: per page vs total

`TableSummaryRow` gains a scope:

```csharp
public enum KumikoSummaryScope
{
    AllRows, // every filtered row (client mode); in server mode the loaded page, see below
    Page,    // the rows on the current page
}

// TableSummaryRow
public KumikoSummaryScope Scope { get; set; } = KumikoSummaryScope.AllRows;
```

- **Client mode.** `AllRows` (the default, and today's behavior) computes over `_filteredIndices`; `Page` over
  `_viewIndices`. A footer that shows both is two `TableSummaryRow`s ("Page total", "Grand total"). Group and
  caption summaries always cover the group's rows on the page.
- **Server mode.** Only the page is in memory, so only `Page` can be computed; `AllRows` rows compute over the page
  too and are therefore not the grand total. The grand total must come from the app: a `SummaryColumnDescription`
  with `SummaryType.Custom` whose `CustomAggregate` returns the server's number, as the #104 cookbook shows.
  `KumikoPage.TotalCount` feeds `Paging.TotalItemCount` and the pager label, not the summaries. The spec does
  not add server-supplied summary values to `KumikoPage` in v1 (Q5).

### 5.8 Selection, editing, scrolling, frozen rows

- **Selection.** A page change is a structural view change. `KumikoSelectionModel` already drops items that leave
  the view, so selection is per page and `SelectedItems` empties when the page changes. This is the simplest
  contract and matches what #106's header check-all says ("the current page"). Keeping selection across pages
  needs the model to hold non-visible items (the same question as "selection survives a filter", #106 Q5) and is
  left open (Q6).
- **Current cell and edit.** An open edit commits before a page change (the same path as a click on another
  cell). If validation fails the page change is canceled (`PageChanging` args `Cancel = true`, `Reason =
  PendingEdit`). The current cell moves to the first row of the new page, same column.
- **Scrolling.** A page change scrolls to the top. Within a page, scrolling is as today. Page Up/Down keyboard
  navigation moves within the page and does not cross it (Ctrl+PageDown/PageUp goes to the next/previous page,
  Alt+Home/End to the first/last).
- **`ScrollToItem`/navigate to an item** (#56): in client mode an `item` not on the current page changes the page
  first (`KumikoPager.PageOf(item)` returns the index, or -1 when the item is filtered out). In server mode it
  scrolls only within the page; the app pages itself.
- **Frozen rows, pinned summaries.** Unchanged; they apply to the page.
- **Row reorder (`AllowRowDragDrop`).** Disabled under paging; the drag handle is hidden.

### 5.9 Relationship to #104 incremental loading

**Exclusive per source.** Both modes decide what the grid owns and what the end of the rows means, and the load-more
row and the pager both claim the space after the last row. Combining them has no coherent UX ("load more" inside
page 3). Rules:

- `KumikoSource.Paging.Mode != None` implies `IncrementalLoading.Mode == None`: no load-more row, no
  near-end trigger.
- Setting `Paging.PageSize > 0` or `Paging.PagedSource` while an `IKumikoIncrementalSource` is active throws
  `InvalidOperationException("Paging and incremental loading are exclusive; clear IncrementalSource first.")`.
  The reverse (setting `IncrementalSource` while paging) throws the same way. A caller sees the mistake at
  configuration time.
- On the hosts, where both can arrive through independent bindable properties in any order (`PageSize` bound, and
  a `LoadMoreCommand` bound), the host cannot throw from a property callback. Paging wins, load-more is
  disabled, and a one-time `Trace.TraceWarning` says so (the pattern `KumikoAccessorRegistry.ReportMissing` uses).
- **Shared:** the query. #104's section 6.4 and Q10 ask for one hook that says "sort/filter/group changed, re-query".
  This spec defines it and #104 can adopt it:

  ```csharp
  // KumikoSource
  public event EventHandler<KumikoQueryChangedEventArgs>? QueryChanged;
  public KumikoQuery GetQuery();   // a snapshot of the current sort, filter and group state

  public sealed class KumikoQuery
  {
      public IReadOnlyList<KumikoSortDescriptor> Sorts { get; }
      public IReadOnlyList<FilterDescription> Filters { get; }
      public IReadOnlyList<GroupDescription> Groups { get; }
  }
  public readonly record struct KumikoSortDescriptor(string PropertyName, KumikoSortDirection Direction);
  ```

  `QueryChanged` is raised once after a sort, filter or group change (coalesced in `BeginUpdate/EndUpdate`),
  *after* the `*Changed` events, never while a `*Changing` event can still cancel. In server paging it triggers the
  reload; in #104's pattern an app handles it to reset and re-query. `KumikoSortDescriptor` uses the column's
  `PropertyName`, which is what a server query needs; a column with only a `ValueAccessor` has no property name and is
  reported with `PropertyName = Id` (documented).
- **One backend, either UI.** A paged backend can serve incremental loading through an optional adapter,
  `KumikoPagedIncrementalSource` (section 6.8), so an app can offer either with one service class.

### 5.10 Layout

The pager bar reserves height at the top or bottom of the grid's viewport, so the row area (and `ScrollState`'s
viewport height) shrinks by `KumikoStyle.PagerHeight` (default: the row height plus padding, 40 logical pixels). The
renderer draws it after the rows and the pinned footer. Stage 3 confirms whether the layout engine already has an
inset mechanism that pinned bottom summaries use; if not, add `KumikoLayoutEngine` viewport insets as part of that
stage (#102 and #104 also need one for their rows).

### 5.11 Loading state (server mode)

- One request in flight. A newer request (page change, query change, size change) cancels the one in flight
  through its `CancellationToken` and ignores its result if it still arrives (a generation counter, as #104's
  section 7.2). The page being replaced stays on screen until the new one arrives.
- `Paging.IsLoading` is true while a request is in flight; the drawn grid dims the rows to 60% and draws a thin
  indeterminate bar under the header; the pager's buttons stay enabled so the user can change their mind.
- Failure: `Paging.State` is `Failed`, the old page stays, the pager shows an error state with a Retry button
  (`KumikoStrings.PagerRetry`, `PagerLoadFailed`), `PageLoadFailed` is raised with the exception and a settable
  user message. The source being replaced or `ItemsSource` changing resets the pager and cancels the request.
- `SetItems` or an INCC `Reset` in client mode resets `PageIndex` to 0.

### 5.12 Accessibility (#77 / #78)

- The pager is a group named `PagerLabel` ("Pagination"). Each button is a button with a name
  ("First page", "Previous page", "Page 3", "Next page", "Last page"); the current page is exposed as current/selected;
  disabled buttons are reported disabled. The page-size picker is a combo box with its current value.
- A page change announces ("Page 3 of 25, rows 101 to 150") through `KumikoAnnouncements`; a failed load announces
  the failure text.
- Keyboard: the pager is a focus stop after the grid (Tab). Left/Right move between buttons, Enter/Space
  activates; Ctrl+PageDown/PageUp and Alt+Home/End work from the grid itself (section 5.8).
- The screen-reader-facing row count reports the page's rows and, when known, the total ("rows 101 to 150 of 1,234").

## 6. Paging: API

### 6.1 Core types

```csharp
namespace KumikoUI.Core.Data;

public enum KumikoPagingMode { None, Client, Server }

public enum KumikoPagerState { Idle, Loading, Failed }

public sealed class KumikoPager
{
    public KumikoPagingMode Mode { get; }                       // derived
    public int PageSize { get; set; }                           // 0 = paging off
    public IReadOnlyList<int> PageSizeOptions { get; set; }     // default { 10, 25, 50, 100 }
    public int PageIndex { get; set; }                          // zero-based, clamped
    public int PageCount { get; }                               // -1 when unknown (server, no total)
    public int TotalItemCount { get; }                          // filtered count, or the server's total; -1 when unknown
    public int FirstRowNumber { get; }                          // one-based row number of the first row on the page, 0 when empty
    public int LastRowNumber { get; }
    public bool ResetPageOnQueryChange { get; set; }            // default true
    public IKumikoPagedSource? PagedSource { get; set; }
    public KumikoPagerState State { get; }
    public bool IsLoading { get; }
    public Exception? LastError { get; }

    public bool CanGoFirst { get; } public bool CanGoPrevious { get; }
    public bool CanGoNext { get; }  public bool CanGoLast { get; }
    public bool GoToPage(int pageIndex);   // false when canceled, clamped to the same page, or refused
    public bool GoToFirst(); public bool GoToPrevious(); public bool GoToNext(); public bool GoToLast();
    public int PageOf(object item);        // client mode; -1 when the item is not in the filtered set
    public void Reload();                  // server: re-issue the current request; client: rebuild

    public event EventHandler<KumikoPageChangingEventArgs>? PageChanging;   // cancelable
    public event EventHandler<KumikoPageChangedEventArgs>? PageChanged;
    public event EventHandler<KumikoPageLoadFailedEventArgs>? PageLoadFailed;
    public event EventHandler? StateChanged;
}
```

`KumikoSource` gains `public KumikoPager Paging { get; }` (never null; `Mode == None` until configured), plus
`QueryChanged` and `GetQuery()` (section 5.9), and uses `DataRowCount` from #106. `TableSummaryRow.Scope` is
section 5.7.

```csharp
public sealed class KumikoPageChangingEventArgs : EventArgs
{
    public int OldPageIndex { get; }
    public int NewPageIndex { get; }
    public KumikoPageChangeReason Reason { get; }   // User, Programmatic, QueryChanged, PageSizeChanged
    public bool Cancel { get; set; }
}
public sealed class KumikoPageChangedEventArgs : EventArgs
{
    public int PageIndex { get; }
    public int PageCount { get; }
    public int RowsOnPage { get; }
}
public sealed class KumikoPageLoadFailedEventArgs : EventArgs
{
    public Exception Error { get; }
    public string? ErrorText { get; set; }   // user-facing message on the pager; null = KumikoStrings.PagerLoadFailed
}
```

All public members get XML docs (documentation warnings are errors, #54/#188).

### 6.2 Server contract

```csharp
public interface IKumikoPagedSource
{
    /// <summary>Loads one page for <paramref name="request"/>. May be called again before an earlier call
    /// completes; the earlier call's token is canceled and its result is ignored.</summary>
    Task<KumikoPage> LoadPageAsync(KumikoPageRequest request, CancellationToken cancellationToken);
}

public sealed class KumikoPageRequest
{
    public int PageIndex { get; }
    public int PageSize { get; }
    public int Skip => PageIndex * PageSize;
    public KumikoQuery Query { get; }               // Sorts, Filters (FilterDescription), Groups
    public KumikoPageChangeReason Reason { get; }
}

public sealed class KumikoPage
{
    public KumikoPage(IReadOnlyList<object> items, int? totalCount = null);
    public static KumikoPage Create<T>(IReadOnlyList<T> items, int? totalCount = null) where T : notnull;
    public IReadOnlyList<object> Items { get; }
    public int? TotalCount { get; }                 // null: unknown; Next stays enabled while Items.Count == PageSize
}
```

The request uses `FilterDescription` as it exists (public: `Column`, `SelectedValues`, `Condition1/2`,
`Combination`, `IsActive`), so there is no second filter model to keep in step. A source maps
`FilterDescription.Column.PropertyName` and its conditions to its own query. A helper,
`KumikoPageRequest.TryGetEquals(propertyName, out object? value)`, covers the common "simple equality list" case.

A generic convenience, `KumikoPagedSource<T>` (delegate-based: `new KumikoPagedSource<Order>((request, ct) => ...)`),
makes the common case a lambda. It is a thin class over the interface.

### 6.3 Host surface (MAUI and Uno, same names and defaults)

| Member | Type | Default |
|---|---|---|
| `PageSize` | int, two-way | 0 |
| `PageIndex` | int, two-way | 0 |
| `PageCount`, `TotalItemCount` | read-only int | |
| `IsPageLoading` | read-only bool | false |
| `PageSizeOptions` | `IReadOnlyList<int>` | `{ 10, 25, 50, 100 }` |
| `PagedSource` | `IKumikoPagedSource?` | null |
| `PagerPosition` | `KumikoPagerPosition` (`None`, `Top`, `Bottom`) | `Bottom` |
| `PageChanged`, `PageLoadFailed` | events | |
| `PageChangedCommand` (MAUI) | `ICommand` | |
| `GoToPage(int)`, `NextPage()`, `PreviousPage()` | methods | |

The properties forward to `KumikoSource.Paging` (a few lines each in `KumikoDataGrid.Options.cs` on both hosts).

### 6.4 `DrawnPager`

A public `DrawnComponent` (`Components/DrawnPager.cs`), constructed over a `KumikoPager`, with `ApplyTheme`,
`Bounds`, pointer/keyboard handlers, the page-size picker as a nested `DrawnComboBox`. The number window shows the
current page, its two neighbors, the first and last pages, and ellipses (`1 ... 4 5 6 ... 25`); on a narrow
viewport (under 480 logical pixels) it collapses to first/previous/"Page 3 of 25"/next/last. The page-size
picker and label hide when the bar is narrower still. Drawing allocates nothing in steady state.

### 6.5 Strings (`KumikoStrings`)

`PagerLabel`, `PagerFirst`, `PagerPrevious`, `PagerNext`, `PagerLast`, `PagerPage` ("Page {0}"),
`PagerPageOf` ("Page {0} of {1}"), `PagerRows` ("{0}-{1} of {2}"), `PagerRowsUnknownTotal` ("{0}-{1}"), `PagerPageSize`
("Rows per page"), `PagerLoadFailed`, `PagerRetry`, `FilterPageValuesOnly`, `PageChangedAnnouncement`.

### 6.6 Optional interfaces (stage 6)

```csharp
public interface IKumikoDistinctValuesSource
{
    Task<IReadOnlyList<string>> GetDistinctValuesAsync(
        string propertyName, KumikoQuery query, CancellationToken cancellationToken);
}

/// Adapts an IKumikoPagedSource to #104's IKumikoIncrementalSource, so one backend serves both UIs.
public sealed class KumikoPagedIncrementalSource : IKumikoIncrementalSource { /* wraps a paged source */ }
```

## 7. Paging: performance

- Client mode keeps `_filteredIndices` (4 bytes per row, 4 MB at 1M rows) in addition to `_viewIndices`, which
  now holds only a page. Rendering and hit testing see only a page, so they are cheaper than the unpaged view.
- A sort or filter change is O(n log n)/O(n) as today; a page change is O(page) (a slice and a regroup of the
  page), with no re-sort and no re-filter.
- A collection change while paging rebuilds (section 5.5). Target: 100K rows, one add, under 15 ms (same budget as
  the sorted worst case in the incremental-update tests).
- Server mode: the grid holds one page; memory is O(page). The only per-page cost is the slice into
  `_sourceItems` (replaced wholesale) and one structural `ViewChanged`.
- The drawn pager allocates nothing per frame (renderer allocation tests, #68).

## 8. Row sources: design

### 8.1 What already works and what is missing

The accessor seam is the right shape. A provider registered with `KumikoAccessorRegistry` is consulted before
reflection, so it works under Native AOT. Concretely `IKumikoAccessorProvider` plus `KumikoPropertyAccessor` can
express "read entry `name` of this dictionary / data row". The gaps, each small:

| # | Gap | Where it bites | Fix (section) |
|---|---|---|---|
| G1 | Accessors are cached by `(Type, name)`, and a `DataRowView` or dictionary has the same type for every schema | the accessor cannot know which columns exist, or their types | schema-agnostic accessors that look the entry up on the instance (8.2, 8.4) |
| G2 | An accessor whose `PropertyType` is `object` accepts the raw edited value (a text editor's string lands in a numeric column) | editing dictionary and data-row cells | `KumikoColumn.ValueType` (8.3) |
| G3 | Case-insensitive name contract (`IKumikoAccessorProvider` doc) vs ordinal dictionaries | `PropertyName="name"` over key `"Name"` | exact lookup, then an ignore-case scan on a miss (8.2) |
| G4 | `KumikoSource` observes `INotifyCollectionChanged` and item `INotifyPropertyChanged` only | `DataView` (and `BindingList<T>`) raise `ListChanged`; changes never appear | `IBindingList` adapter (8.5) |
| G5 | No way to create columns for rows without properties (and no auto-generation at all) | the app writes every column by hand | column factories (8.6) |
| G6 | Item identity is the reference; a `DataView` re-creates its `DataRowView`s on reset | selection and current cell are lost on a `DataView` reset | documented limitation (8.4), Q4 |
| G7 | A dot splits a path, so a dictionary key that contains one cannot be addressed | keys like `"user.name"` | `[key.with.dots]` bracket segments (8.2), Q3 |

### 8.2 Dictionary rows (`IDictionary<string, object?>`), in Core

A built-in provider, `KumikoDictionaryAccessorProvider` (`Data/`, public so apps can compose it, internal ctor,
`Instance` singleton), handles row types that implement, in this order:

1. `IDictionary<string, object?>` (covers `Dictionary<string, object?>`, `SortedDictionary`, `ConcurrentDictionary`,
   `ExpandoObject`, and anything else that implements it);
2. `IReadOnlyDictionary<string, object?>` (read-only accessor);
3. the non-generic `System.Collections.IDictionary` with string keys (covers `Hashtable`, `Dictionary<string, int>`
   and other value types by boxing).

`GetAccessor(itemType, name)` returns null unless `itemType` is assignable to one of these
(`Type.IsAssignableFrom`, trim and AOT safe, no reflection of members), then an accessor:

```csharp
// pseudo-code of the accessor for one name
getter = item => TryGet(item, name, out var v) ? v : null;    // missing key reads null
setter = (item, value) => item[name] = value;                  // creates the key; a read-only or fixed dictionary throws, the message becomes the edit error
PropertyType = typeof(object);
```

- **Registration.** Built in. `KumikoAccessorRegistry` consults it as the last global provider: after
  `[KumikoGridItem]` and `Register` providers, after providers the app added with `AddProvider` (so an app
  provider for a specific dictionary type wins), and before reflection. It is on by default because the
  alternative (today's silently blank cells) is the bug. An `AppContext` switch
  `KumikoUI.Core.IsDictionaryRowAccessSupported` (default true; the same `RuntimeHostConfigurationOption`
  pattern as the reflection switch) turns it off for the rare app that binds a dictionary's own properties
  (`Count`, `Keys`) as columns (Q1).
- **Missing key.** Reads null (the grid already treats a missing property on a row as null, #18). A write to a
  missing key adds it.
- **Case.** Names match exactly first. On a miss the getter scans the keys with `OrdinalIgnoreCase` (a
  `Dictionary` built with an ignore-case comparer already matched on the first try), so the documented
  case-insensitive contract holds. The miss path is O(keys) for rows that lack the key; the cell text cache
  (#68, `CellTextCache`) means a miss is paid when the cell's text is built or the row changes, not every frame.
  Apps with sparse dictionaries can set `Dictionary` comparers or generate all keys (section 8.6).
- **Typing.** `PropertyType` is `object`; `KumikoColumn.ValueType` (8.3) or the existing value's runtime type
  decides the conversion on write.
- **Nested paths.** `"Address.City"` where `Address` is a dictionary value works: the path resolver already
  re-resolves a segment per call when the previous accessor's type is `object` (`KumikoValuePath`), and the nested
  dictionary reaches this provider.
- **Dotted keys (G7).** A path segment wrapped in brackets is a literal key: `PropertyName="[user.name]"`. `KumikoValuePath`
  splits on dots outside brackets. A new, optional, backwards-compatible syntax (existing names cannot start with `[`
  usefully). Q3 asks whether it is worth doing in v1.
- **Sorting, filtering, grouping, summaries.** Work through `KumikoColumn.ResolvePath`/`CreateColumnReader` like any
  property. Values in a column may have mixed runtime types in dictionaries; `KumikoComparer` (the safe comparer
  from #15) already orders mixed types without throwing. A text filter evaluates the display text.

### 8.3 `KumikoColumn.ValueType` and edit conversion (G2)

```csharp
// KumikoColumn
/// <summary>The type edited values are converted to when the property accessor reports object, as it does for
/// dictionary, dynamic and data-row cells. Null: convert to the type of the cell's current value, if any.</summary>
public Type? ValueType { get; set; }
```

`KumikoValuePath.TrySetValue` gets an overload taking the column's override: when the last accessor's
`PropertyType` is `typeof(object)` and an override is set, convert with `KumikoValueConverter.TryConvert` to the
override; when no override is set and the current value is non-null, convert to the current value's runtime type
(so a numeric cell stays numeric); when both are absent the raw value is stored (today's behavior). Nullable
targets accept the empty string as null. `KumikoSource.TrySetItemValue` passes `column.ValueType`. The column
factories (8.6) set `ValueType` from the schema, so the common cases need no manual setting.

### 8.4 `DataView` / `DataTable` rows, in a new package `KumikoUI.Data`

**Where it lives.** A new project `src/KumikoUI.Data` (assembly and NuGet package `KumikoUI.Data`, `net10.0`,
`IsAotCompatible`, `IsTrimmable`, lockstep versioning per `docs/VERSIONING.md`), referencing `KumikoUI.Core` and the
framework's `System.Data.Common`. Rationale: `System.Data.Common` is a large assembly with a known trimming story
([UNVERIFIED]: parts of `DataSet`/`DataTable` carry `RequiresUnreferencedCode`, basic row/column access does not),
and Core is "zero external dependencies". Any provider class that mentions `DataRowView` forces the linker to
keep that assembly for every Core user if it lives in Core. Core stays free of `System.Data`.

**How apps bind.** The hosts' `ItemsSource` is `IEnumerable`, and `DataTable` is not one. Apps bind the view:

```csharp
KumikoDataTable.Use();                       // once at startup: registers the DataRowView provider
grid.ItemsSource = table.DefaultView;        // or new DataView(table) { RowFilter = "..." }
foreach (var column in KumikoDataTable.ColumnsFor(table))   // or hand-written columns
    grid.Columns.Add(column);
```

`KumikoDataTable.Use()` is explicit (no module initializer: a module initializer in a trimmed package runs only if
the assembly loads, and explicit registration is the same pattern `KumikoAccessorRegistry.Register` and the source
generator use). The item type is `DataRowView`.

**Provider.** `KumikoDataRowViewAccessorProvider : IKumikoAccessorProvider`, registered with
`KumikoAccessorRegistry.Register(typeof(DataRowView), provider)`:

- It is **schema-agnostic** (G1): the same accessor serves every table, so its getter looks the column up on the
  instance: `row.Row.Table.Columns[name]` (a hash lookup; a missing column, or a deleted/detached row, reads null,
  never throws). The value maps `DBNull.Value` to null.
- Setter: `view[name] = value ?? DBNull.Value`, then `view.EndEdit()`. `DataRowView` begins an edit on assignment and
  raises `ListChanged.ItemChanged` only when the edit ends, so without `EndEdit` the grid would not hear about its
  own edit. Exceptions the table throws (`ReadOnlyException`, `ConstraintException`, `ArgumentException` for a
  type mismatch, `InvalidCastException`) are caught by `KumikoValuePath.TrySetValue`, and the message is the cell's
  edit error (the grid already shows a throwing setter's message). If the assignment or `EndEdit` throws, the setter calls
  `view.CancelEdit()` first so the row does not stay in edit mode.
- `PropertyType` is `typeof(object)`; the conversion target is `KumikoColumn.ValueType` (8.3), set by the factory
  from `DataColumn.DataType`. Conversion goes through `KumikoValueConverter`, so culture-aware parsing and localized
  error messages are the same as for POCOs.
- `DataRowView` implements `IDataErrorInfo`; the grid's existing validation display uses `Error` and `this[column]`
  with no change.
- **Identity limitation (G6).** `DataView` creates its `DataRowView`s lazily and keeps them while the view is
  stable. Assigning `Sort`/`RowFilter`/`RowStateFilter`, and `DataTable.Clear`/`Load`/`Merge`, raise `Reset` and
  recreate them, so `KumikoSelectionModel` (reference identity) loses the selection and current cell on those
  operations. Adds, deletes and edits keep them. Documented; Q4 asks whether the model should accept an
  `IEqualityComparer<object>` so `DataRowView` can compare by `.Row`.

**Alternatives considered.** Binding `DataRow` items through a Kumiko-owned wrapper (`IList` of `DataRow` with
`INotifyCollectionChanged`) gives stable identity and precise remove events, but needs a per-row change-notification
seam (`DataRow` has no `INotifyPropertyChanged`), a second list implementation to maintain, and loses the user's
`DataView` sort/filter presets. Rejected for v1; it can come later behind the same provider idea.

### 8.5 `IBindingList` adapter (G4), in Core

`DataView` implements `IBindingList` (`ListChanged`), not `INotifyCollectionChanged`, and so does `BindingList<T>`,
which today is silently not observed when items are added. `IBindingList` and `ListChangedEventArgs` are in
`System.ComponentModel` (already imported by `KumikoSource`), not `System.Data`, so the adapter belongs in Core.

`KumikoSource.SetItems` subscribes to `ListChanged` when the items implement `IBindingList` and not
`INotifyCollectionChanged` (when both are present, the collection event wins, to avoid double handling). The
adapter maps events onto the existing handlers:

| `ListChangedType` | Handled as |
|---|---|
| `ItemAdded(newIndex)` | an `Add` at `newIndex` (the incremental path when it applies), tracks the new item's `INotifyPropertyChanged` |
| `ItemDeleted(newIndex)` | the removed item is not in the args and is already gone from the list, so: a rebuild with a full re-track (O(n)) |
| `ItemChanged(newIndex)` | the item at `newIndex` changed: invalidate its cell text, rebuild if a sort/filter/group/summary property is involved (`ItemPropertyAffectsView` with an unknown property name, i.e. rebuild when a view is active), else redraw |
| `ItemMoved` | rebuild (a `DataView` re-sorted itself) |
| `Reset`, `PropertyDescriptor*` | `Reset`: rebuild with a full re-track, `InvalidateCellText` (as the INCC `Reset` case does) |

`DataRowView` implements `INotifyPropertyChanged` ([UNVERIFIED]: confirm in the target framework), in which case
the per-item tracking path also works and `ItemChanged` is a redundant fast path; the adapter is written so a
double notification costs one extra redraw, not a double rebuild (the existing `BeginUpdate` coalescing).

Threading: `ListChanged` fires on the thread that changed the data. The existing contract is unchanged: the app
raises changes on the UI thread (or marshals), as for `ObservableCollection`.

### 8.6 Column factories (G5)

```csharp
public static class KumikoColumnFactory   // Core
{
    /// Columns for dictionary rows: the union of keys in order of first appearance over the first
    /// <paramref name="sampleSize"/> rows. ColumnType/Format/alignment come from the first non-null value's runtime type.
    public static IReadOnlyList<KumikoColumn> FromDictionaries(
        IEnumerable<object?> rows, int sampleSize = 100);
}

public static class KumikoDataTable        // KumikoUI.Data
{
    public static void Use();               // registers the DataRowView provider
    public static IReadOnlyList<KumikoColumn> ColumnsFor(DataTable table);   // one column per DataColumn
}
```

`KumikoDataTable.ColumnsFor(DataTable)` (in `KumikoUI.Data`) maps each `DataColumn`:

| `DataColumn` | `KumikoColumn` |
|---|---|
| `ColumnName` | `PropertyName`, `Id` (stable key for layouts, #98) |
| `Caption` | `Header` |
| `DataType` | `ValueType`; `ColumnType`: `bool`/`bool?` Boolean, integral and floating types and `decimal` Numeric, `DateTime`/`DateOnly`/`DateTimeOffset` Date, everything else Text |
| `AllowDBNull` and `bool` | `IsThreeState = true` (#38; null shows as the dash) |
| `ReadOnly`, or a non-empty `Expression` | `IsReadOnly = true` |
| `ColumnMapping == MappingType.Hidden` | `IsVisible = false` |
| `DataType == byte[]` | skipped (not displayable) |

Numeric format: `N0` for integral, `N2` for `decimal`/floating; dates `d`; right-aligned numbers.
`FromDictionaries` infers the same way from sample values (`bool`, numeric, date, else Text) with `ValueType`
left null (the value's own type applies). Both are plain, allocation-light, reflection-free helpers; the app can
modify the returned columns and pass them to `SetColumns`.

There is no POCO equivalent: discovering columns needs reflection or the source generator; `[KumikoGridItem]` could emit a
column list in a later feature and is out of scope here.

### 8.7 `ExpandoObject` and dynamic rows

- **`ExpandoObject`** implements `IDictionary<string, object?>`, so it is covered by 8.2 with no dynamic code.
  It also implements `INotifyPropertyChanged`, so the grid's per-item tracking already works for edits through
  the dictionary interface ([UNVERIFIED]: confirm that writes through the indexer raise it; add a test either way).
- **`DynamicObject` subclasses** (the common "property bag" shape) are supported by `KumikoDynamicObjectAccessorProvider`
  in Core. It calls `DynamicObject.TryGetMember`/`TrySetMember` **directly** with a small private
  `GetMemberBinder`/`SetMemberBinder` subclass (their `Fallback*` methods are never invoked), so it uses no
  `CallSite` and no dynamic code, and runs under Native AOT. Column discovery uses `GetDynamicMemberNames()`.
  The provider is a global provider ordered after the dictionary one.
- **Other `IDynamicMetaObjectProvider`s** (COM, IronPython, a custom meta object) need the DLR's call sites, which
  need dynamic code. They are not supported: `GetAccessor` returns null and the existing "no accessor" warning
  is traced once, with a message naming the supported alternatives (set `ValueAccessor`, wrap in a dictionary).
  Where dynamic code is available (not AOT), a reflection-style fallback could be added later; it is not part
  of this spec.

### 8.8 AOT and trimming

- **Core providers** (`KumikoDictionaryAccessorProvider`, `KumikoDynamicObjectAccessorProvider`,
  `KumikoColumnFactory`): only interface casts, `Type.IsAssignableFrom`, `typeof(...)` and delegates. No member
  reflection, no `Activator`, no `MakeGenericType`, no `dynamic`, no expression trees. They carry no
  `RequiresUnreferencedCode`/`RequiresDynamicCode` and add no IL warnings. The generic dictionary interfaces are
  instantiated over `string`/`object?` only, which the AOT compiler sees statically.
- **`KumikoUI.Data`**: `IsAotCompatible` and `IsTrimmable` true with `System.Data.Common` references limited to
  indexers, `Columns`, `DataColumn` properties, `DataRowView.EndEdit/CancelEdit`, `ListChanged`. The AOT console
  smoke test from #76 gets two cases: a `Dictionary<string, object?>` grid model and a `DataView` grid model
  (read, edit, sort, filter), published with Native AOT and `TrimMode=full`. If `System.Data.Common` produces IL
  warnings in that publish, they are surfaced in the package's README rather than suppressed.
- **`KumikoAccessorRegistry`**: unchanged contract. With `IsReflectionPropertyAccessSupported` false the
  dictionary and `DynamicObject` providers still answer (they come before the reflection step); POCOs without a
  provider behave as today (null and one warning).
- The `[KumikoGridItem]` generator is not involved.

### 8.9 Performance targets (benchmarks added to the existing benchmark set)

| Operation | Target |
|---|---|
| Dictionary cell read (key present) | within 4x of a generated POCO accessor, 0 allocations |
| Dictionary cell read (key absent, ignore-case scan over 20 keys) | under 400 ns, 0 allocations |
| `DataRowView` cell read | within 6x of a generated POCO accessor; allocates only the value's box (`DataRow` stores typed arrays and boxes on read), which the text cache keeps off the frame path |
| Sort 100K dictionary rows by a numeric key | within 3x of the POCO sort |
| `ListChanged.ItemAdded` on a 100K-row `DataView` | under 10 ms (the incremental add path) |
| `ListChanged.Reset` on 100K rows | one rebuild, same as an INCC `Reset` |

## 9. Compatibility and migration

- All additions are additive. Existing grids keep paging off (`PageSize = 0`), do not set a paged source, and keep
  every behavior. The dictionary provider changes only what happens for row types that implement the dictionary
  interfaces (previously blank cells, or a column named like a dictionary property: switch in 8.2).
- `KumikoColumn.ValueType` defaults to null (today's behavior).
- `TableSummaryRow.Scope` defaults to `AllRows` (today's behavior).
- New package `KumikoUI.Data`; no change for apps that do not reference it. Neither host references it (a
  `DataTable` app adds the package and calls `KumikoDataTable.Use()` once).
- `SetFilter(Func<object, bool>)` throwing in server mode is new behavior only in a mode that did not exist.

## 10. Test plan

Core (`tests/KumikoUI.Core.Tests`):

- **Client paging**: slice sizes (exact multiple, remainder, empty, `PageSize` larger than the data); `PageIndex`
  clamping and re-clamping after removal or filtering; `PageSize` change keeps the first visible row; sort and
  filter reset the page and recompute `PageCount`; summaries `AllRows` vs `Page`; grouping over a page (a group
  split across pages shows a header on each); selection empties on page change; edit commit-or-cancel before a
  page change; `PageOf`; `ReorderRow` refused; collection add/remove/`Reset` rebuilds; `PageChanging` cancel;
  `QueryChanged` coalescing and ordering after `*Changed`; `SetFilter` allowed in client, throws in server.
- **Server paging** (fake `IKumikoPagedSource`): request contents (page, size, sorts, `FilterDescription`s,
  groups); one in flight and cancellation of the older; stale results ignored; failure then retry; `TotalCount`
  null (next enabled while a full page returns); `SetItems` and `ItemsSource` replacement reset; the filter popup
  note and `IKumikoDistinctValuesSource`; `PagedSource` and `PageSize` interplay for `Mode`.
- **Exclusivity with #104**: throws on conflicting configuration in either order; the host path warns once and
  paging wins; no load-more row while paging.
- **`DrawnPager`**: layout at wide/narrow widths, number window with ellipses, disabled buttons at the ends,
  picker, theme, keyboard (Left/Right/Enter/Space), pointer hit tests, allocation test, fake `IDrawingContext`
  draw-call assertions.
- **Dictionary rows**: read, write (existing and new key), missing key, case-insensitive fallback, nested
  dictionary paths, bracket keys (if built), `IReadOnlyDictionary`, non-generic `IDictionary` and
  `Dictionary<string, int>` boxing, `ExpandoObject` read/write and `PropertyChanged`, precedence (type provider >
  `AddProvider` > dictionary > reflection), the opt-out switch, mixed-type sort without exceptions, `ValueType`
  conversion (`"42"` into a numeric column becomes a number; invalid text gives the converter's localized
  message), `KumikoColumnFactory.FromDictionaries` (key union, order, type inference, heterogeneous rows).
- **`DynamicObject`**: `TryGetMember`/`TrySetMember` called without call sites (a subclass that asserts on the
  binder type), member names, unsupported `IDynamicMetaObjectProvider` warns once.
- **`IBindingList` adapter**: each `ListChangedType` against `BindingList<T>`; no double handling when both
  event kinds exist; unsubscribe on `SetItems`/`Dispose`/`Detach` (#28).
- **Reflection switch off**: dictionary and `DynamicObject` rows work with
  `IsReflectionPropertyAccessSupported == false`.

`KumikoUI.Data` tests (new test project): `DataRowView` read (`DBNull` to null, missing column to null), write
(type conversion, `ReadOnlyException` and `ConstraintException` messages, `EndEdit` raising `ItemChanged`, `CancelEdit`
after a failure), `ListChanged` end to end (add, delete, edit, re-sort of the `DataView` itself, `Reset` and the
selection-loss limitation), `IDataErrorInfo` display, `ColumnsFor` mapping table, 100K-row benchmark smoke.

Hosts (property-mapping tests as #84): the paging members forward to Core; two-way `PageIndex`/`PageSize`
binding; `PagerPosition`; the conflict warning.

AOT/trim: the #76 smoke test extended as in 8.8, run in CI for Core (dictionary, `DynamicObject`) and
`KumikoUI.Data` (`DataView`).

Samples: a client-paged grid with the drawn pager and a summary row of each scope; a server-paged grid over a
fake paged API with latency and a forced failure; a `DataView` grid with edits; a dictionary/`ExpandoObject` grid
built by `FromDictionaries`, on both sample apps.

## 11. Staged implementation issues

Track B (row sources) and track A (paging) are independent; order within each matters.

Track B:

1. **Dictionary, `ExpandoObject` and non-generic `IDictionary` rows in Core**: `KumikoDictionaryAccessorProvider`
   (built in, switch, case fallback, bracket keys if accepted), `KumikoColumn.ValueType` and the conversion
   plumbing in `KumikoValuePath`/`KumikoSource.TrySetItemValue`, AOT smoke case. Tests.
2. **`IBindingList` adapter in `KumikoSource`, `KumikoDynamicObjectAccessorProvider` and `KumikoColumnFactory.FromDictionaries`**.
   Tests.
3. **`KumikoUI.Data` package**: project, packaging and CI wiring, `KumikoDataTable.Use`, `DataRowView` provider,
   `ColumnsFor(DataTable)`, AOT/trim smoke case, tests, README, sample. (Decide Q4 first if row identity is
   in scope.)

Track A:

4. **Paging core**: `KumikoPager` model, client slice in the `RebuildCore` pipeline, `TableSummaryRow.Scope`,
   `QueryChanged`/`GetQuery`, exclusivity with #104, selection/edit/`PageOf` behavior. Tests. (Needs #104's loader
   type to exist for the exclusivity check; stub it behind an interface if PR #221 lands later.)
5. **`DrawnPager` and host surface**: the drawn bar, layout inset (viewport reserve), `KumikoStrings`, host
   bindable members and command, keyboard shortcuts, accessibility, announcements, sample.
6. **Server paging**: `IKumikoPagedSource`, `KumikoPageRequest`/`KumikoPage`, loading/cancel/stale/failure/retry,
   `SetFilter` guard, the filter popup note, optional `IKumikoDistinctValuesSource` and `KumikoPagedIncrementalSource`,
   delegate-based `KumikoPagedSource<T>`, fake-server sample. Tests.
7. **Docs and benchmarks**: a "paging" and a "row sources" page for the cookbook (#110), the benchmark rows in 8.9,
   cookbook recipes for server totals and for `DataTable`.

## 12. Open questions

- **Q1.** Should the dictionary provider be on by default (proposed, with an `AppContext` switch to turn it off) or
  opt-in (`KumikoDictionaryRows.Use()`)? Default-on fixes blank cells with no code but changes what a
  `PropertyName` of `Count` or `Keys` means for dictionary rows.
- **Q2.** Client paging with grouping: group the page's rows (proposed, a group can span pages), or page over
  top-level group blocks (variable page size)?
- **Q3.** Is the `[key.with.dots]` bracket syntax worth the `KumikoValuePath` change in v1, or do apps with such keys
  use `ValueAccessor`?
- **Q4.** Should `KumikoSelectionModel` and `KumikoSource` accept an item identity comparer, so `DataRowView`s compare by
  their `DataRow` and a `DataView` reset keeps selection? It touches `IndexOfRowKey` (linear by reference today) and
  the row-key arrays, so it is a core change with its own perf work. Proposed: document the limitation for 1.x.
- **Q5.** Should `KumikoPage` carry server-computed summary values (`IReadOnlyDictionary<string, object?>`) so table
  summaries in `AllRows` scope show grand totals without a `Custom` aggregate?
- **Q6.** Should selection survive page changes in client mode (an "include off-page items" selection)? It needs the
  same capability as "selection survives a filter" (#106 Q5). Proposed: no for v1.
- **Q7.** Pager placement default: bottom (proposed). Should the pager be inside the pinned footer region (with
  summaries) or below it?
- **Q8.** Name and home of the data package: `KumikoUI.Data` (proposed) vs `KumikoUI.DataTable`. The namespace
  `KumikoUI.Data` does not clash with Core's `KumikoUI.Core.Data`, but the folder names read alike.
- **Q9.** Should the hosts also accept a `DataTable` as `ItemsSource` (by changing the property's type to `object` and
  resolving `IListSource`)? It would drop the `.DefaultView` step but changes a public property type on both hosts.
- **Q10.** Paging UI for incremental sources: should #104 expose a "footer: N of M loaded" label from the same
  `KumikoPager` label machinery, or stay separate?

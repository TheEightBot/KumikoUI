# Spec: item-based pinned rows, stacked headers and cell merging (#109)

| | |
|---|---|
| Issue | [#109](https://github.com/TheEightBot/KumikoUI/issues/109) (plan key X8, milestone 1.x – Fast follow-ons) |
| Status | Draft for review |
| Blocked by | none for the design. Implementation stages depend on [#108](https://github.com/TheEightBot/KumikoUI/issues/108) stage 1 (display column order) and coordinate with [#102](https://github.com/TheEightBot/KumikoUI/issues/102) stage 2 (row geometry routing), see §4 and §8.1 |
| Designed with | [#102](https://github.com/TheEightBot/KumikoUI/issues/102) variable row height (PR #220: `KumikoRowLayout`, Fenwick offsets), [#56](https://github.com/TheEightBot/KumikoUI/issues/56) `KumikoScroller` (PR #198), [#108](https://github.com/TheEightBot/KumikoUI/issues/108) cell selection (PR #222: `KumikoCellRange`, `DisplayColumns`), [#80](https://github.com/TheEightBot/KumikoUI/issues/80) RTL (spec in progress), [#79](https://github.com/TheEightBot/KumikoUI/issues/79) accessibility tree, [#174](https://github.com/TheEightBot/KumikoUI/issues/174) sticky group headers (merged) |
| Findings | CORE question 14, CORE-09, CORE-25 ([01](../release-readiness/01-core-data-layout-render.md)); research §1.3 items 11–12 and the feature matrix rows "Stacked headers", "Cell merging", "Row/column spanning" ([07](../release-readiness/07-external-research.md)) |

## 1. Summary

Three features that change what a row, a header and a cell are on screen. They are specified together because
they touch the same code: the vertical bands of the grid (header, summaries, frozen rows, scrolling rows,
footer), the three column panes, and the hit tester that has to agree with both.

- **Item-based pinned rows (A).** `FrozenRowCount` freezes "the first N rows of the current view" (Excel
  semantics, kept as is per the [roadmap](../ROADMAP.md)). Pinning keeps chosen *items* at the top or bottom of
  the data area regardless of sort and filter, with or without grouping. Pinned items are spliced into the view
  at its start and end, so row indices, selection, navigation and the #102 row layout keep working on one
  contiguous list of rows.
- **Stacked column headers (B).** Columns can belong to a tree of `KumikoColumnBand`s. The header grows by one
  band row per level above today's column header row. Bands are computed from contiguous runs of display
  columns, split at pane boundaries, and constrain reorder so that a band's columns stay together.
- **Cell merging (C).** A column can merge vertically adjacent equal values (`KumikoColumn.CellMergeMode`), and
  a `KumikoCellMergeResolver` on the source can return any rectangular span. Merged ranges are logical
  rectangles in view rows and display columns. They are drawn as *pieces*, one per band and pane they cross, so
  a merged cell is correct while its anchor row is virtualized away, while it crosses the frozen divider, and
  while rows have variable height.

All three are opt-in. With none configured, pixels, hit testing, scroll extents and allocation stay exactly as
on `main`.

## 2. Terminology

The code already uses "pinned" and "group" for other things. This spec uses these words consistently:

| Term | Meaning | In code today |
|---|---|---|
| **Frozen rows** | The first `FrozenRowCount` rows of the body; position-based | `KumikoSource.FrozenRowCount`, `EffectiveFrozenRowCount` |
| **Pinned rows** (new) | Rows of items pinned to the top or bottom band; item-based | n/a |
| **Top band / bottom band** (new) | The fixed rows above and below the scrolling rows: top = pinned top + frozen, bottom = pinned bottom | `frozenRowCount` locals |
| **Sticky group headers** | Group headers held at the top of the scrolling rows (#174) | internal `PinnedGroupHeaders` (renamed `StickyGroupHeaders` in stage A1 to free the word "pinned") |
| **Pinned footer** | Bottom table summaries drawn at the viewport bottom | `KumikoStyle.PinBottomSummaryRows` (unchanged) |
| **Column band** (new) | A labeled header spanning a set of columns (a "column group" in other grids) | n/a |
| **Display column** | A visible column's on-screen position: left-frozen, scrollable, right-frozen | added by #108 stage 1 (`KumikoSource.DisplayColumns`) |
| **Merged range** (new) | A rectangle of cells shown as one cell: view rows × display columns | n/a |
| **Anchor** | The top-left cell of a merged range (top-start under RTL); it supplies the content | n/a |
| **Piece** (new) | The part of a merged range inside one vertical band and one column pane; what is drawn | n/a |

"Column band" is used instead of "column group" because row grouping already owns `GroupDescription`,
`GroupHeaderRow`, `GroupHeaderBackgroundColor`, the group panel and `HitRegion.GroupHeaderRow`. See open question
Q8.

## 3. Current state (main @ c77f44b)

| Area | Where it lives today | What it assumes |
|---|---|---|
| Frozen rows | `KumikoSource.FrozenRowCount` (0 while grouping), `EffectiveFrozenRowCount`; `KumikoLayoutEngine.GetFrozenRowHeight`; `KumikoRenderer.DrawFrozenRows` (3 times, once per pane), `DrawFrozenRowDivider` | Rows `[0, F)` of the view are fixed; nothing is fixed at the bottom except the pinned footer |
| Header | `KumikoStyle.HeaderHeight` read in 35 places (renderer 21, input controller 7, layout 4, hit tester 3); `DrawHeader`, `DrawSortIndicators`, `DrawFilterIcons`, `DrawColumnDragOverlay`; `KumikoHitTester.HitTestHeader`/`CheckHeaderColumn` | One header row, `[0, HeaderHeight)` |
| Header fit | `KumikoLayoutEngine.UpdateHeaderFitWidths`, `MeasureHeaderFitWidth` (#182, cached on the column) | Only leaf headers need width |
| Panes | `PopulateVisibleFrozenColumns`, `PopulateVisibleScrollableColumns`, `PopulateVisibleRightFrozenColumns`; 3-pass rendering with a pane clip and a data clip (`dataClip`) | A cell belongs to exactly one pane |
| Rows | `DrawRows` → `DrawRowCells` per row and visible column; `DrawGridLines` draws full-width horizontal lines and pane-height vertical lines; frozen rows get no horizontal grid lines (finding (d) in report 01) | One cell = one column × one row |
| Hit testing | `KumikoHitTester.HitTestCore`: footer, group panel, header, top summaries, sticky headers, unpinned bottom summary, frozen rows, scrolling rows, then `HitTestColumn` | Regions are disjoint horizontal bands |
| Reorder / resize | `KumikoInputController.HandleColumnResize` (one column), `FinishColumnReorder` (drop index from content X over **all** visible columns, ignoring panes), `ColumnReordered` → host calls `KumikoSource.ReorderColumn(from, to)` | Any column can move anywhere |
| Row reorder | `FinishRowReorder` → `RowReordered` → host calls `KumikoSource.ReorderRow`, which moves the item in the source `IList` (no-op while grouping) | All rows are body rows |
| Ensure visible | `EnsureCurrentCellVisible`, `GetScrollableRowsViewportHeight` (input controller); `KumikoScroller.GetRowOffsetY` (#56) returns the current offset for a frozen row | The scrolling area ends at the footer |
| Display text cache | `KumikoSource.PrepareCellTextCache(frozenRows, scrollingRows)`, `GetCachedCellText`: slots `[0, F)` for frozen rows, then a ring | Only the top band is fixed |
| Sticky headers | `PinnedGroupHeaders.Compute(source, style, scrollTop, offsetY, frozenRowCount, …)`, `BandHeightAbove`, `SlotOffset` | Scrolling rows start after `F` rows |
| Summaries | `ComputeTableSummaries` over the view's data rows; group summaries per group path | Every data row is in the body |

The duplication of band arithmetic (renderer, hit tester, input controller, `KumikoScroller`) is what produced
CORE-09: the pinned footer was drawn, but hit testing and ensure-visible kept the old geometry. Adding a bottom
pinned band and a multi-row header the same way would repeat it, so §4 introduces one geometry snapshot first.

## 4. Shared foundation

### 4.1 `KumikoGridGeometry` (internal)

A readonly struct computed from `(KumikoSource, KumikoStyle, ScrollState)` that every consumer uses instead of
recomputing bands inline:

```csharp
internal readonly struct KumikoGridGeometry
{
    // Vertical bands, in the coordinates below the grouping panel (the renderer's translated space)
    public float HeaderTop { get; }            // 0
    public float LeafHeaderTop { get; }        // BandDepth * BandHeaderHeight (B)
    public float HeaderBottom { get; }         // LeafHeaderTop + HeaderHeight
    public float TopSummaryBottom { get; }     // HeaderBottom + TopSummaryCount * SummaryRowHeight
    public float TopBandBottom { get; }        // TopSummaryBottom + height of rows [0, FixedTopRowCount)  (A)
    public float BottomBandTop { get; }        // see §5.4; == DataClipBottom when there is no bottom band
    public float DataClipBottom { get; }       // above the pinned footer
    public float ScrollingHeight { get; }      // BottomBandTop - TopBandBottom
    public int FixedTopRowCount { get; }       // pinned top + effective frozen (A)
    public int FixedBottomRowCount { get; }    // pinned bottom (A)

    // Horizontal panes
    public float HandleOffset { get; }         // left drag handle
    public float RightHandleWidth { get; }
    public float FrozenWidth { get; }
    public float RightFrozenLeft { get; }
    public float RightFrozenWidth { get; }

    public static KumikoGridGeometry Compute(KumikoSource source, KumikoStyle style, ScrollState scroll);
}
```

Row tops and heights inside the bands come from #102's `KumikoRowLayout` (`GetRowTop`, `GetRowAt`). The geometry
holds only band edges and pane edges, so it is O(1) to compute once the row layout's index is current, and it
allocates nothing (a struct). `KumikoRenderer.Render`, `KumikoHitTester.HitTestCore`,
`KumikoInputController` (`EnsureCurrentCellVisible`, `GetScrollableRowsViewportHeight`, `FinishRowReorder`,
`GetCellScreenBounds`, `ToggleTappedGroup`) and `KumikoScroller` all take it from `Compute`.

**Relation to #102.** #102 stage 2 routes every *row* computation through `KumikoRowLayout`. This struct routes
every *band* computation. They touch the same lines, so they should land as one refactor (stage G0 below),
whichever of the two issues is picked up first. If #102 stage 2 lands first, G0 only adds the band edges and the
header layout on top of it.

### 4.2 Display column order

Bands (B) and horizontal merges (C) are defined in display order: left-frozen, scrollable, right-frozen, each in
`Columns` order, hidden columns skipped. #108 stage 1 adds exactly this (`KumikoSource.DisplayColumns`,
`GetDisplayIndex`, `ToDisplayIndex`, `ToColumnIndex`, `ColumnLayoutVersion`) and fixes `NavigateLeft`/`Right`
to move in display order. This spec depends on it. If #108 stage 1 hasn't merged when B1 or C1 starts, that
stage lands it as #108 specifies (same names, same semantics).

### 4.3 `KumikoCellRange`

Merged ranges reuse #108's `KumikoCellRange` (top/bottom view row, first/last display column, inclusive). A
selection range and a merged range are then the same type, and #108's range expansion (§7.9) needs no conversion.
If C1 starts before #108's stage 2, C1 adds the struct with the shape #108 specifies.

---

## 5. Feature A: item-based pinned rows

### 5.1 Goals and non-goals

**Goals**

1. Pin specific items to the top or bottom of the data area. They stay there whatever the sort, the filter
   (by default) and the grouping, and they are not repeated in the body.
2. Two ways to pin: an explicit, ordered list per band (`PinnedTopItems`, `PinnedBottomItems`), and a
   declarative `PinnedItemSelector` for "every item whose `IsFavorite` is true".
3. `FrozenRowCount` keeps its meaning, applied to the body: the top band is pinned-top rows followed by the
   first `FrozenRowCount` body rows.
4. Pinned rows behave like data rows everywhere: selection (item-based, #30), current cell, keyboard
   navigation, editing, cell styles, row style resolvers, drag handle, sticky group headers below them.
5. The bottom band is a real fixed band: drawn, hit-tested, excluded from ensure-visible and from the drag
   handle grips the same way (no CORE-09 repeat).
6. No cost when nothing is pinned. Steady-state frames with pinned rows allocate 0 B.

**Non-goals**

- Showing a pinned item *also* in its natural place in the body (AG Grid can do this with separate data). See
  Q1.
- Pinned rows that come from a separate collection (rows that aren't in `ItemsSource`).
- A built-in "Pin" button or context-menu command. Apps add it with `ContextRequested` or a template column.
- Dragging rows between bands (pin by drag). v1 clamps a drag to the band it started in. See Q3.
- Pinning group header rows ("sticky groups" exist through #174).
- Independent horizontal scrolling for the bands. They scroll horizontally with the body, like frozen rows
  today.

### 5.2 API

#### Core

```csharp
namespace KumikoUI.Core.Models;

/// Where an item is pinned.
public enum KumikoPinPosition
{
    /// Not pinned: the item is in the body, in sort and filter order.
    None,
    /// Pinned to the top band, above the frozen rows and the scrolling rows.
    Top,
    /// Pinned to the bottom band, below the scrolling rows and above the pinned footer.
    Bottom,
}

public class KumikoSource
{
    /// The items pinned to the top, in band order. Items not in the source are kept but not shown.
    public IReadOnlyList<object> PinnedTopItems { get; }

    /// The items pinned to the bottom, in band order.
    public IReadOnlyList<object> PinnedBottomItems { get; }

    /// Pins item to position (None unpins it). index is its place in the band; -1 appends. An item is in at
    /// most one band: pinning it to the other band moves it. Raises PinnedItemsChanged and a structural
    /// ViewChanged (Rebuild) when the rows change.
    public void Pin(object item, KumikoPinPosition position, int index = -1);

    /// Unpins item (same as Pin(item, None)).
    public void Unpin(object item);

    /// Replaces both lists in one change (one PinnedItemsChanged, one ViewChanged).
    public void SetPinnedItems(IEnumerable<object>? top, IEnumerable<object>? bottom);

    /// Moves a pinned item within its band.
    public void MovePinnedItem(object item, int newIndex);

    /// Pins items by rule: evaluated for every item on a rebuild and for one item when it raises
    /// PropertyChanged. Items in PinnedTopItems / PinnedBottomItems are placed by the lists, not the selector.
    /// Selector-pinned items keep the current sort order inside their band, after the listed items.
    public Func<object, KumikoPinPosition>? PinnedItemSelector { get; set; }

    /// Whether filters (column filters and SetFilter) apply to pinned items. Default false: pinned items are
    /// shown whatever the filter. When true, a pinned item that fails the filter is hidden (and stays pinned).
    public bool FilterPinnedItems { get; set; }

    /// The band item is shown in, or None (also for an item that isn't in the source).
    public KumikoPinPosition GetPinPosition(object item);

    /// The band of a visible row: Top for rows [0, PinnedTopRowCount), Bottom for the last
    /// PinnedBottomRowCount rows, otherwise None.
    public KumikoPinPosition GetRowPinPosition(int visibleRow);

    /// Rows shown in the top band from pins (frozen rows not included).
    public int PinnedTopRowCount { get; }

    /// Rows shown in the bottom band.
    public int PinnedBottomRowCount { get; }

    /// Rows that don't scroll at the top: PinnedTopRowCount + the effective frozen body rows.
    public int FixedTopRowCount { get; }

    /// Raised after the pinned lists or the rows they produce change, with the items added and removed per band.
    public event EventHandler<KumikoPinnedItemsChangedEventArgs>? PinnedItemsChanged;
}

public sealed class KumikoPinnedItemsChangedEventArgs : EventArgs
{
    public IReadOnlyList<object> PinnedTop { get; }   // snapshot after the change
    public IReadOnlyList<object> PinnedBottom { get; }
    public IReadOnlyList<object> Added { get; }
    public IReadOnlyList<object> Removed { get; }
}
```

`FrozenRowCount` keeps its getter and setter. Its XML docs change from "number of top data rows" to "number of
top **body** rows, after the pinned-top rows". `EffectiveFrozenRowCount` keeps its meaning (frozen body rows
only, `Math.Min(FrozenRowCount, bodyRowCount)`). Every internal use of `EffectiveFrozenRowCount` as "rows that
don't scroll" switches to `FixedTopRowCount`; there are eight such uses on `main` (one in the renderer, one in the
hit tester, six in the input controller) plus `KumikoScroller` on #56.

Item identity is reference equality (`RowKeyComparer`, the rule `KumikoSelectionModel` uses since #30). Value
types boxed anew on each read can't be pinned reliably; that's the same documented limitation selection has.

New style members (`KumikoStyle`, each with a `KumikoTheme` value for Light, Dark and HighContrast):

| Member | Default | Notes |
|---|---|---|
| `PinnedRowBackgroundColor` | `(250, 250, 250)` (same as `FrozenRowBackgroundColor`) | background of pinned rows when not selected, focused, alternate or resolver-colored |
| `PinnedRowDividerColor` | `(180, 180, 180)` | line between the pinned-top rows and the frozen rows, and above the bottom band |
| `PinnedRowDividerWidth` | `2` | |

The divider below the whole top band stays `FrozenRowDivider*`, so a grid that only uses `FrozenRowCount` looks
as it does today.

#### Hosts (MAUI and Uno)

| Member | Type | Notes |
|---|---|---|
| `PinnedTopItems`, `PinnedBottomItems` | `IList?` | bindable / dependency properties. When the bound list is an `INotifyCollectionChanged`, the grid follows its changes. Drag reordering inside a band moves the item in the bound list when it is writable (the rule `ReorderRow` follows for `ItemsSource`). |
| `PinnedItemSelector` | `Func<object, KumikoPinPosition>?` | bindable |
| `FilterPinnedItems` | `bool` | |
| `Pin(item, position)`, `Unpin(item)` | methods | forward to the source |
| `PinnedItemsChanged` | event | forwards the source event |
| `PinnedRowBackgroundColor` and the divider members | color / double | survive `Theme` and `GridStyle` changes (the MAUI-06 rule) |

### 5.3 View model: pinned rows are spliced into the view

`RebuildCore` produces the body as today (`_viewIndices`, then `_flatView` when grouping). Pinned items are
taken out of the body and placed at its ends:

```
view row:  0 … Pt-1 | Pt … Pt+F-1 | Pt+F … R-Pb-1 | R-Pb … R-1
           pinned top | frozen body | scrolling body | pinned bottom
           └──── top band ───────┘                  └ bottom band ┘
```

- **Ungrouped:** `_viewIndices = [top source indices][body][bottom source indices]`.
- **Grouped:** `_flatView = [Data rows for the top pins][groups as today][Data rows for the bottom pins]`.
  `IndexGroupHeaders` sets the last groups' end rows to the body's end, not `RowCount`, so `TryGetEnclosingGroup`
  never puts a pinned-bottom row inside a group.

Because the rows are in the same list, `GetItem`, `GetRowKey`, `IsNonDataRow`, `SetCellValue`, the selection
model, `CellPosition.Row` and #102's `KumikoRowLayout` work unchanged. Only the code that asks "is this row
fixed?" changes, and it asks `FixedTopRowCount` and `PinnedBottomRowCount`.

**Membership and order.**

1. Build a `HashSet<object>` (`RowKeyComparer`) of the listed pins once per rebuild, only when a list is
   non-empty. The filter/sort loop skips listed items (and, when `PinnedItemSelector` is set, items it pins).
   Listed items are emitted in list order. Selector-pinned items are emitted after them, in the order the sort
   gives them.
2. A listed item that isn't in the source (or fails the filter with `FilterPinnedItems`) produces no row and
   stays listed. Items removed from the collection (`Remove`, `Replace`, `Reset` that drops them) are removed
   from the lists, with a `PinnedItemsChanged`, so the source doesn't hold removed items forever.
3. `null` items can't be pinned (`ArgumentNullException`), as they can't be rows (`SetItems` skips them).

**Changes.** `Pin`, `Unpin`, `SetPinnedItems`, `MovePinnedItem`, setting `PinnedItemSelector` or
`FilterPinnedItems` call `RequestRebuild()`: one structural `ViewChanged`, inside `BeginUpdate`/`EndUpdate` when
batched. `KumikoViewChangedEventArgs.GetViewRow(item)` maps every item to its new row, so the selection, the
current cell (#30) and an open editor (`EditSession.ApplyViewChange`) follow the item into or out of a band.

**Selector re-evaluation.** `OnItemPropertyChanged` calls the selector for that item (any property, because the
grid can't know what the selector reads). Only a changed result requests a rebuild. Cost: one delegate call
per `PropertyChanged`.

**Incremental updates.** `TryIncrementalUpdate` keeps its fast path when nothing is pinned. With pins it adds
`PinnedTopRowCount` to the body index of an inserted or removed body item, and falls back to a rebuild when the
item is (or becomes, through the selector) pinned.

### 5.4 Layout and rendering

**Bands.** With `T = FixedTopRowCount` and `B = PinnedBottomRowCount`, using #102's row layout:

- top band: rows `[0, T)`, from `TopSummaryBottom` to `TopBandBottom = TopSummaryBottom + top(T)`
- scrolling rows: `[T, R - B)`, offset 0 shows row `T` at `TopBandBottom`
- bottom band: rows `[R - B, R)`, height `bh = top(R) - top(R - B)`. It sits directly after the last scrolling
  row when the body is shorter than the viewport, and at the bottom of the data area otherwise:
  `BottomBandTop = min(DataClipBottom - bh, TopBandBottom + (top(R - B) - top(T)) - OffsetY)`. A short list
  therefore doesn't show its bottom pins far below the data (see Q2).

The scroll extent doesn't change: `ContentHeight` already counts every row, and the bottom band takes the same
height out of the viewport as it adds to the rows, so `MaxOffsetY` scrolls the last body row to exactly the top
of the bottom band. An unpinned bottom summary (`PinBottomSummaryRows = false`) still follows the last row: it
scrolls up from under the bottom band, and the extent keeps room for it.

**Drawing.** The top band is today's frozen-row pass, generalized from `frozenRowCount` to `T`
(`DrawFrozenRows` becomes `DrawFixedRows(first, last, y)`, used for both bands in all three panes). The bottom
band is drawn after the scrolling rows, with the same per-pane clips and the same row background rules, with
`PinnedRowBackground` for pinned rows and `FrozenRowBackground` for frozen ones. Order in each pane pass:

1. scrolling rows (data clip `[TopBandBottom, BottomBandTop)`), current-cell border, grid lines
2. header
3. top band rows, their grid lines (fixing the missing frozen-row grid lines, report 01 (d))
4. bottom band rows, their grid lines

Then, outside the panes: the pinned divider below the pinned-top rows (when `F > 0` and `Pt > 0`), the frozen
divider below the top band, the pinned divider above the bottom band, table summaries, group rows, sticky group
headers (clipped to the scrolling rows, so they never cover a band), the drag-handle column (grips for band rows
at band positions, none for rows hidden behind a band), and the pinned footer.

**Sticky group headers.** `StickyGroupHeaders.Compute` (renamed from `PinnedGroupHeaders`) already takes
`scrollTop` and the fixed row count; it gets `TopBandBottom` and `T`, and clips to `BottomBandTop`. Because
pinned rows are outside every group, `T` rows never contain a group header.

**Display text cache.** `PrepareCellTextCache(fixedTopRows, fixedBottomRows, scrollingRows)`: slots
`[0, T)` for the top band, `[T, T + B)` for the bottom band, then the ring. `GetCachedCellText` maps rows
`[R - B, R)` to the bottom slots. The slot layout only changes when `T` or `B` changes, so steady state is
unchanged.

**#102 variable row height.** Pinned rows are view rows in `KumikoRowLayout`, so they're measured like frozen
rows: both bands are measured every frame they are stale (they're always on screen), and the anchor correction
(#102 §7.1 step 4) uses the scrolling rows only. `GetFrozenHeight(F)` becomes `top(T)`, and the bottom band
height is `top(R) - top(R - B)`; both are two prefix queries.

### 5.5 Interactions

| Feature | Behavior with pinned rows |
|---|---|
| `FrozenRowCount` | Counts body rows after the pinned-top rows. Still 0 while grouping; pins work while grouping. |
| Sort | Doesn't reorder pinned rows. Listed pins keep list order; selector pins keep sort order among themselves. |
| Filter | Ignored for pinned items unless `FilterPinnedItems`. The filter popup's distinct values (`GetUniqueColumnValues`) include pinned items. |
| Grouping | Pinned rows sit outside every group: group `ItemCount`, caption summaries and group summaries exclude them. Sticky group headers pin below the top band. |
| Table summaries | Computed over the items that pass the filter, pinned or not (pinning is presentation). A pinned item shown only because filters don't apply to it is not aggregated. See Q4. |
| Selection | Item-based (#30): unchanged. Shift-range by row covers pinned rows between the anchor and the clicked row in view order. `SelectAll` includes pinned rows. |
| Keyboard | Up/Down move in view order: from the last top-band row into the body, from the last body row into the bottom band. `PageUp`/`PageDown` page over the scrolling height. Ctrl+Home/End go to row 0 and row R-1 (a pinned row when there are any). |
| Ensure visible | A band row needs no vertical scroll (as frozen rows today). A body row is kept between `TopBandBottom` (plus the sticky band) and `BottomBandTop`. `KumikoScroller.GetRowOffsetY` returns the current offset for any band row. |
| Editing | Unchanged for the row's cells. Committing an edit that changes the selector's answer moves the row into or out of a band after the commit (structural `ViewChanged`); the current cell follows the item. Committing an edit to a body row's sort key moves it as today; a pinned row doesn't move. |
| Row drag | Inside a band: reorders that band (`MovePinnedItem`, and the bound host list when writable); `RowReordered` is raised with the view rows. From the body: the drop index is clamped to the body. A drag never crosses a band boundary in v1 (Q3). Works while grouping for pinned rows (it only reorders the pin list). |
| Programmatic scrolling (#56) | `ScrollToRow`/`ScrollToItem` on a band row returns true without scrolling. `GetRowY` returns its band position. |
| Row details (#103) | Band rows can expand details; the band grows. A band taller than the data area is clipped (§9 R6). |
| Cell merging (C) | Merges never cross a band boundary (§7.5). Rows inside a band can merge with each other. |

### 5.6 Hit testing

`HitTestCore` checks, in this order: pinned footer, grouping panel, header (with bands, §6.6), top summaries,
**top band**, **bottom band**, sticky group headers, unpinned bottom summary, scrolling rows. A point in a band
resolves its row with `GetRowAt` over the band's rows and never reaches the scrolling row beneath it. The
result's `RowIndex` is the view row as always; callers that care ask `GetRowPinPosition(row)`.

### 5.7 Allocation and performance (100K rows)

| Operation | Cost | Allocation |
|---|---|---|
| Rebuild with no pins and no selector | unchanged (one `Count` check) | unchanged |
| Rebuild with k listed pins | + one `HashSet` lookup per item: ≤ 2 ms at 100K on the reference machine | the set, reused across rebuilds (cleared, not reallocated) |
| Rebuild with a selector | + 100K delegate calls (≤ 1 ms for a property read) | none from the grid |
| `PropertyChanged` with a selector | one delegate call; a rebuild only when the band changes | none |
| Frame, steady state, with pins in both bands | same as frozen rows: O(visible cells) | 0 B (`RenderAllocationTests`) |

Band membership is never evaluated per frame. The pin lists are `List<object>` and the read-only views are cached
wrappers, so reading `PinnedTopItems` doesn't allocate.

### 5.8 Accessibility

- Row order in the accessibility tree is view order, which is visual order (top band, body, bottom band), so
  `aria-rowindex`, UIA `IGridItemProvider.Row` and Android `CollectionItemInfo` rows need no mapping.
- #79's semantics provider gets `GetRowPinPosition(row)` and exposes it: UIA `ItemStatus` ("Pinned to top" /
  "Pinned to bottom", from `KumikoStrings` so it localizes), `accessibilityValue`/hint on iOS, the state
  description on Android, `aria-description` on WASM.
- Pin and unpin are announced ("Row pinned to top") through #79's notification API when they come from a user
  gesture.
- Fixed bands are always in the realized set of semantic nodes, like frozen rows.

### 5.9 Test plan

- **Source** (`PinnedRowsTests`): list order, selector order, list wins over selector, sort and filter don't
  move pinned rows, `FilterPinnedItems` hides failing pins and keeps them listed, removal from the collection
  unpins, `Reset` keeps pins that are still present, grouping places pins outside groups (`TryGetEnclosingGroup`,
  `ItemCount`, group summaries), table summaries per §5.5, `PropertyChanged` with a selector moves the row,
  incremental insert/remove with pins, `ReorderRow` inside a band, `PinnedItemsChanged` and one `ViewChanged`
  per change, batched inside `BeginUpdate`.
- **Rendering** (`RecordingDrawingContext`, three panes, drag handle left and right): band rows at the band
  positions, the bottom band hugging a short list, dividers, grid lines in both bands, sticky headers clipped
  between the bands, grips only for visible rows, the footer drawn last.
- **Hit testing and input**: a point in each band hits the band row (never the row beneath), keyboard
  navigation across band boundaries, ensure-visible keeps body rows between the bands, PageDown with a bottom
  band, editor bounds for band rows, `ScrollToItem` on a pinned item.
- **Selection and editing**: the current cell and the selection follow an item that is pinned or unpinned; an
  open editor follows it (`ApplyViewChange`).
- **#102** (when merged): mixed heights in both bands; anchor correction ignores band rows.
- **Allocation**: `SteadyState_PinnedTopAndBottomWithFrozenRows_AllocatesNothing`.
- **Performance**: `DataBenchmarks` sort of 100K with 50 listed pins and with a selector.

### 5.10 Implementation issues

**A1. Pinned rows: item-based top band in KumikoSource and the renderer**

- `KumikoPinPosition`, the §5.2 source API (lists, `Pin`/`Unpin`/`SetPinnedItems`/`MovePinnedItem`,
  `PinnedItemSelector`, `FilterPinnedItems`, `GetPinPosition`, `GetRowPinPosition`, row counts,
  `FixedTopRowCount`, `PinnedItemsChanged`), XML docs, PublicAPI entries.
- Splicing in `RebuildCore` (ungrouped and grouped), group end rows, incremental-update offsets, removal
  unpins, selector re-evaluation on `PropertyChanged`.
- Every "rows that don't scroll" use switches to `FixedTopRowCount`; `PinnedGroupHeaders` renamed
  `StickyGroupHeaders` (internal).
- Top band drawn with `PinnedRowBackground` and the pinned divider; frozen and pinned band rows get horizontal
  grid lines.
- The bottom band is not drawn until A2. Until then `Pin(…, Bottom)` and a non-empty `bottom` in
  `SetPinnedItems` throw `NotSupportedException`, and a selector result of `Bottom` is treated as `None`, so A1
  can ship alone.
- §5.9 source tests and top-band rendering, hit-test and keyboard tests pass; 0 B steady state.

**A2. Pinned rows: bottom band (layout, rendering, hit testing, ensure-visible, text cache)**

- `KumikoGridGeometry.BottomBandTop` per §5.4 (hugging rule), used by the renderer, the hit tester, the input
  controller and `KumikoScroller`.
- Bottom band drawn in all panes, divider, grips, sticky header clip; hit testing order of §5.6;
  `EnsureCurrentCellVisible`/`GetRowOffsetY` subtract the band; `PrepareCellTextCache` bottom slots.
- Tests for each consumer, including a CORE-09-style test that a tap on the bottom band never hits the row
  beneath it.

**A3. Pinned rows: drag reorder inside a band and edit-driven moves**

- `FinishRowReorder` and `DrawRowDragOverlay` clamp the drop to the source band; band reorder calls
  `MovePinnedItem`; `ReorderRow` routes band rows to the pin list.
- Edit commits that change the selector's answer move the row and keep the current cell on the item.
- Tests.

**A4. Pinned rows on MAUI and Uno, samples and docs**

- §5.2 host members on both hosts, surviving `Theme`/`GridStyle` changes; bound `INotifyCollectionChanged`
  lists followed; writable bound lists updated by drag reorder. Uno tests.
- A "Favorites" sample page on both hosts (pin from a template column, a selector, 100K rows).
- README frozen-rows section, ARCHITECTURE.md ("HitTest Region System", "Virtual Scrolling"), RENDERING.md
  frame lifecycle and 3-pass sections.

---

## 6. Feature B: stacked column headers

### 6.1 Goals and non-goals

**Goals**

1. Columns can be organized under labeled bands, nested up to 8 levels. The header shows one band row per
   level above the column header row.
2. A column outside any band, or in fewer levels than the deepest, has a header cell that extends up through
   the empty band rows (like other grids' "column not in a band").
3. Bands follow the columns: they're computed from contiguous display columns, so hiding, freezing and
   reordering columns updates them with no extra state.
4. Reorder keeps band members together by default: a column moves within its innermost band, and a band moves
   as a block within its parent.
5. Band edges resize the band's columns; double-click auto-fits them.
6. Frozen panes work: a band crossing the frozen divider is drawn as one piece per pane, each labeled. In the
   scrollable pane, a band's label stays in the visible part of the band while scrolling horizontally.
7. Theming through `KumikoStyle` and per-band `HeaderCellStyle`.
8. 0 B per steady-state frame; band structure is rebuilt only when the columns, their order, visibility or
   freeze mode, or the bands change.

**Non-goals**

- Sorting or filtering by a band. A band header has no built-in tap action.
- Multi-row *leaf* layouts (DevExpress "multi-row layout", where one data row is drawn on two lines).
- Bands in the row-group panel, and dragging a band to the group panel.
- Collapsible bands (show/hide a band's columns from its header). See Q9.
- Variable band row heights or wrapped band text (the band row is one line, ellipsized).

### 6.2 API

```csharp
namespace KumikoUI.Core.Models;

/// A labeled header over a set of columns. Bands nest through ParentKey; columns join a band through
/// KumikoColumn.BandKey (its innermost band).
public sealed class KumikoColumnBand
{
    /// Identifies the band; unique within the source. Required.
    public string Key { get; set; } = string.Empty;

    /// The text shown in the band's header cell.
    public string Header { get; set; } = string.Empty;

    /// The enclosing band's Key, or null for a top-level band.
    public string? ParentKey { get; set; }

    /// Per-band header overrides (background, text color, font, padding), like KumikoColumn.HeaderCellStyle.
    public KumikoCellStyle? HeaderCellStyle { get; set; }

    /// Alignment of the band text. Default Center.
    public KumikoTextAlignment HeaderTextAlignment { get; set; } = KumikoTextAlignment.Center;

    /// Whether the band can be dragged as a block within its parent. Default true.
    public bool AllowReorder { get; set; } = true;

    /// Whether the band's edge resizes its columns. Default true.
    public bool AllowResize { get; set; } = true;
}

public class KumikoColumn
{
    /// The Key of the innermost band this column belongs to, or null. A key with no matching band leaves the
    /// column unbanded.
    public string? BandKey { get; set; }
}

public class KumikoSource
{
    /// The bands, as last set.
    public IReadOnlyList<KumikoColumnBand> ColumnBands { get; }

    /// Replaces the bands. Throws ArgumentException for an empty or duplicate Key, a ParentKey that names no
    /// band, a cycle, or more than 8 levels. Raises a Redraw ViewChanged.
    public void SetColumnBands(IEnumerable<KumikoColumnBand>? bands);

    /// Number of band rows in the header: the deepest band chain among the columns (hidden ones included, so
    /// hiding a column doesn't change the header height). 0 without bands.
    public int BandDepth { get; }

    /// Moves count adjacent columns starting at fromIndex so that the first lands at toIndex (Columns indices).
    /// One Redraw notification. Used for band moves.
    public void MoveColumns(int fromIndex, int count, int toIndex);
}
```

**Why keys, not object references.** `Columns` is an `ObservableCollection<KumikoColumn>` of plain Core objects
on both hosts. MAUI XAML could use `x:Reference`, but WinUI/Uno XAML has no equivalent for plain objects in a
collection. Keys work the same in C#, MAUI XAML and Uno XAML, survive serialization (a future layout-persistence
API, CORE-33), and keep `KumikoColumn` free of object cycles. See Q8.

```xml
<kumiko:KumikoDataGrid.ColumnBands>
    <kumiko:KumikoColumnBand Key="sales" Header="Sales" />
    <kumiko:KumikoColumnBand Key="q1" Header="Q1" ParentKey="sales" />
    <kumiko:KumikoColumnBand Key="q2" Header="Q2" ParentKey="sales" />
</kumiko:KumikoDataGrid.ColumnBands>
<kumiko:KumikoColumn Header="Region" PropertyName="Region" FreezeMode="Left" />
<kumiko:KumikoColumn Header="North" PropertyName="Q1North" BandKey="q1" />
<kumiko:KumikoColumn Header="South" PropertyName="Q1South" BandKey="q1" />
<kumiko:KumikoColumn Header="North" PropertyName="Q2North" BandKey="q2" />
```

```
┌────────┬───────────────────────────────┐
│        │ Sales                         │  band row 0
│ Region ├───────────────┬───────────────┤
│        │ Q1            │ Q2            │  band row 1
│        ├───────┬───────┼───────┬───────┤
│        │ North │ South │ North │ …     │  column header row (HeaderHeight)
└────────┴───────┴───────┴───────┴───────┘
```

Style members (`KumikoStyle`, with theme values):

| Member | Default | Notes |
|---|---|---|
| `BandHeaderHeight` | `32` | height of each band row; `HeaderHeight` stays the column header row |
| `BandHeaderBackgroundColor` | `HeaderBackgroundColor`'s default `(230, 230, 230)` | |
| `BandHeaderTextColor` | `(30, 30, 30)` | |
| `BandHeaderFont` | `("Default", 13, bold: true)` | |
| `BandHeaderBorderColor` | `HeaderBorderColor`'s default `(200, 200, 200)` | lines between band cells and below each band row |

The header's total height is `BandDepth * BandHeaderHeight + HeaderHeight`. Without bands it is `HeaderHeight`,
so existing grids don't change.

Input additions:

| Member | Notes |
|---|---|
| `HitRegion.BandHeader`, `HitRegion.BandResizeGrip` | appended to the enum (existing values keep their numbers) |
| `KumikoHitTestResult.Band` (`KumikoColumnBand?`), `HeaderLevel` (`int`, band row or `BandDepth` for the column header row, -1 elsewhere) | |
| `KumikoColumnReorderedEventArgs.ColumnCount` (default 1), `Band` (`KumikoColumnBand?`) | a band move is one event; hosts call `MoveColumns(OldIndex, ColumnCount, NewIndex)` |
| `AutoFitColumnEventArgs.Band` | set when the double-click was on a band edge |

Hosts get a bindable `ColumnBands` (`ObservableCollection<KumikoColumnBand>`, auto-created like `Columns`), the
style members, and update `OnColumnReordered` to call `MoveColumns`.

### 6.3 Band layout (`KumikoHeaderLayout`, internal)

Owned by the `KumikoSource` (it depends only on the columns and bands), rebuilt lazily when
`ColumnLayoutVersion` (#108) or the band version changes:

1. Resolve each display column's band chain (innermost to outermost) through a `Dictionary<string,
   KumikoColumnBand>` built by `SetColumnBands`. Chain length `k ≤ BandDepth`.
2. For each band row `L` in `[0, BandDepth)`, walk the display columns and emit a **band cell** for every maximal
   run of adjacent display columns, in the same pane, whose chain has the same band at level `L` (levels are
   counted from the outermost: `chain[k - 1 - L]` when `L < k`). Pane boundaries end a run, so a band crossing
   the frozen divider produces one cell per pane.
3. A column whose chain is shorter than `BandDepth` has no band at the deeper levels; its **column header cell
   extends up** to the bottom of its deepest band (to the header top when unbanded).

Stored as reused arrays of `struct BandCell { short Level; short BandIndex; int FirstDisplay; int LastDisplay;
byte Pane; }` and `int[] LeafTopLevel` per display column. X positions are not stored: they come from the
frame's pane column lists, so scrolling and resizing don't rebuild the layout. A band whose columns aren't
adjacent (possible after `SetColumns`, a programmatic `ReorderColumn` or a freeze-mode change) simply produces
several cells; each shows the band text. That is defined behavior, not an error.

Cost: O(columns × depth) per rebuild, which happens only on column changes. At 200 columns × 8 levels that's
1,600 steps, and it allocates only when the arrays must grow.

### 6.4 Rendering

`DrawHeader` keeps its signature and per-pane call; it now draws, inside the pane clip:

1. band rows: for each band cell in this pane, fill `[x, L * BandHeaderHeight, w, BandHeaderHeight]` with the band
   background (or its `HeaderCellStyle.BackgroundColor`), stroke its right and bottom edges with the band
   border, and draw the text. In the scrollable pane, the text rect is the band rect **intersected with the
   visible pane** before padding, so the label of a wide band stays visible while it scrolls (sticky band
   labels). The text is ellipsized to that rect.
2. column header cells: each from `LeafTopLevel * BandHeaderHeight` to the header bottom; text vertically
   centered in that rect (so an unbanded column's text is centered over the whole header).

`DrawSortIndicators` and `DrawFilterIcons` place their glyphs in the bottom `HeaderHeight` of the column header
cell, so they line up across banded and unbanded columns. `DrawColumnDragOverlay` draws the ghost from the
dragged column's (or band's) top to the header bottom. `DrawDragHandleColumn` draws its header cell over the full
header height. All `HeaderHeight` reads in the renderer become `geometry.HeaderBottom` (or `LeafHeaderTop` where
they mean the column row).

Paint: `PaintCache` gets `BandHeaderBackground`, `BandHeaderText`, `BandHeaderBorder`, rebuilt with the others.
Per-band style overrides reuse the renderer's existing override paints (`_headerText`, `_headerBackground`).

### 6.5 Sizing, header fit and auto-fit

- **Header fit (#182).** After `UpdateHeaderFitWidths` measures the column headers, a band pass measures each
  band's text (cached on the band like `KumikoColumn.HeaderMeasure`) and, when a band cell's fit width exceeds
  the sum of its columns' `HeaderFitWidth`, raises the `HeaderFitWidth` of its columns **without an explicit
  width** in proportion to their current fit widths. Columns with an explicit width aren't changed, so a band
  over explicit widths may ellipsize (as an explicit-width column header does today). Steady state measures
  nothing.
- **Star/Fixed/Auto** sizing (#24) is unchanged; it sees the raised `HeaderFitWidth` as a minimum.
- **Band resize.** Dragging a band's right edge distributes the delta over the band's resizable columns
  (`IsResizable`) in proportion to their widths, each clamped to `[MinWidth, MaxWidth]`, with what one column
  can't take passed to the others. The result is explicit widths (as a column resize sets today), and
  `ColumnResized` is raised once per changed column. A band edge is a grip only when `AllowResize` and at least
  one column can change.
- **Auto-fit.** Double-click on a band edge raises `AutoFitColumnRequested` with `Band` set; the hosts auto-fit
  each of the band's visible columns (`CalculateAutoFitWidth`), then apply the band header rule above.

### 6.6 Hit testing

`HitTestHeader` takes the point's `adjustedY` and:

1. finds the display column under X in the right pane (as today);
2. if `adjustedY < LeafTopLevel(column) * BandHeaderHeight`, the point is in band row
   `L = (int)(adjustedY / BandHeaderHeight)`: it finds the band cell at `(L, display column)` (binary search in
   that level's cells) and returns `BandResizeGrip` within `ResizeGripWidth` of the cell's right edge (when
   resizable), else `BandHeader`, with `Band` and `HeaderLevel` set;
3. otherwise it is in the column header cell: resize grip, filter icon and header cell as today, with the filter
   icon's Y range limited to the bottom `HeaderHeight`.

Every other `HeaderHeight` comparison in the hit tester and the input controller uses `geometry.HeaderBottom`.

### 6.7 Reorder constraints

The pan-start test in `HandlePointerMove` starts a column drag from `HeaderCell` (as today) or a band drag from
`BandHeader` when `KumikoColumnBand.AllowReorder`. `FinishColumnReorder` is rewritten to work in display order
(it currently ignores panes, see §3):

- **Column drag:** the drop target is limited to the display columns of the dragged column's innermost band
  (or, when unbanded, to the unbanded runs and band-block boundaries of its pane at the top level). The drop
  indicator snaps to the nearest allowed boundary.
- **Band drag:** the band's columns move as a block; allowed boundaries are the edges of its sibling bands and
  unbanded columns within the parent band (or the pane at the top level).
- A drag never changes a column's pane (`FreezeMode` is not changed by a drop). A drop across the frozen divider
  snaps to the nearest boundary inside the source pane.
- The controller raises one `ColumnReordered` (`ColumnCount` = the block size); hosts call `MoveColumns`.

`KumikoSource.ReorderColumn` and `SetColumns` stay unconstrained (programmatic); they may split a band (§6.3).

### 6.8 Interactions

| Feature | Behavior with bands |
|---|---|
| Frozen columns | Bands split at pane boundaries; each piece labeled. |
| Hidden columns / column chooser (X5) | Hidden columns leave their bands; a band with no visible columns draws nothing. `BandDepth` counts hidden columns so the header height is stable. |
| Sorting, filtering | Leaf headers only; glyphs in the bottom `HeaderHeight`. |
| Row drag handle | Its header cell spans the full header height. |
| Group panel | Unaffected (it is above the header). |
| Cell selection (#108) | A click on a band header could select the band's columns (whole-column range); proposed as a #108 follow-up, not in this spec (Q10). |
| Export (#105) | XLSX: band rows become merged header cells; CSV: band text is omitted or prefixed, decided in #105. |
| RTL (#80) | Bands mirror with their columns; the sticky label clamps to the visible part in mirrored coordinates; reorder boundaries are the same logical boundaries. |

### 6.9 Allocation and performance

- Per frame: `DrawHeader` iterates the band cells of the visible range of each pane (cells are sorted by
  `FirstDisplay` per level, so the visible ones are found with one binary search per level). With 200 columns
  and 3 levels, a frame draws at most a few dozen band cells.
- Steady state: 0 B. Band text measurement is cached; layout arrays are reused.
- 100K rows don't matter here: bands are per column. The header adds `BandDepth * BandHeaderHeight` to the fixed
  vertical bands, which reduces the scrolling height accordingly.

### 6.10 Accessibility

- Column headers: UIA `ITableProvider.GetColumnHeaders` returns the column header cells (leaves). Band cells are
  exposed as `HeaderItem` elements with `IGridItemProvider.ColumnSpan` = their column count and `Row` = their
  level. WASM: band cells are `role="columnheader"` with `aria-colspan`, in additional header rows
  (`aria-rowindex` counts them); column header cells extending up get `aria-rowspan`.
- A cell's accessible name may include its band path ("Sales, Q1, North: 120") through #79's provider
  (`GetColumnHeaderPath(column)`); whether to include it by default is #79's call (Q11).
- Band reorder and resize have keyboard equivalents only through #79's column actions (not in this spec).

### 6.11 Test plan

- **Model** (`ColumnBandTests`): validation errors (empty/duplicate key, unknown parent, cycle, depth > 8),
  unknown `BandKey` = unbanded, `BandDepth` with hidden columns, layout cells for contiguous, split and
  cross-pane bands, `LeafTopLevel`, rebuild only on `ColumnLayoutVersion`/band changes.
- **Header fit and resize** (`HeaderFitWidthTests`, `ResizeAndReorderTests`): band text raises only
  non-explicit widths; band resize distributes with min/max overflow; one `ColumnResized` per changed column;
  auto-fit with `Band`.
- **Rendering** (`RecordingDrawingContext`): band cells per pane, sticky labels while scrolled, column header
  cells extending up, sort/filter glyphs at the bottom row, drag ghost, drag-handle header height, theme paints.
- **Hit testing** (`HitTesterTests`): every band level, grips, column header extending up, frozen panes, the
  footer and group panel still first.
- **Reorder**: column within its band, band within its parent, snapping at pane boundaries, one event per band
  move, `MoveColumns` result.
- **Geometry**: every consumer (rows, summaries, frozen and pinned bands, sticky headers, editor bounds,
  `KumikoScroller`) is shifted by the band rows.
- **Allocation**: `SteadyState_ThreeBandLevelsAcrossPanes_AllocatesNothing`.

### 6.12 Implementation issues

**B1. Stacked headers: KumikoColumnBand model, header layout and header height routing**

- `KumikoColumnBand`, `KumikoColumn.BandKey`, `KumikoSource.ColumnBands`/`SetColumnBands`/`BandDepth`/
  `MoveColumns`, validation, the internal `KumikoHeaderLayout` (§6.3), documented and in PublicAPI.
- `KumikoGridGeometry.HeaderBottom`/`LeafHeaderTop` used by every former `HeaderHeight` read in the renderer,
  hit tester, input controller and `KumikoScroller`; with no bands every existing test passes unchanged.
- Model tests of §6.11.

**B2. Stacked headers: rendering, theming and sticky band labels**

- §6.4 drawing in all three panes, the style members of §6.2 in `KumikoStyle`, `PaintCache` and all
  `KumikoTheme` presets; per-band `HeaderCellStyle`; header fit with bands (§6.5).
- Rendering tests and the allocation test.

**B3. Stacked headers: hit testing, band resize, auto-fit and constrained reorder**

- `HitRegion.BandHeader`/`BandResizeGrip`, `KumikoHitTestResult.Band`/`HeaderLevel`, cursors for band grips
  (`HoverCursorTests`).
- Band resize and auto-fit (§6.5), the new `FinishColumnReorder` in display order with band constraints
  (§6.7), event args additions.
- Tests of §6.11.

**B4. Stacked headers on MAUI and Uno, samples and docs**

- `ColumnBands` on both hosts (XAML as in §6.2), style members surviving theme changes, `OnColumnReordered` calls
  `MoveColumns`. Uno tests.
- A "Quarterly sales" sample page with two levels, a frozen column and 100K rows on both hosts.
- README, ARCHITECTURE.md, RENDERING.md.

---

## 7. Feature C: cell merging

### 7.1 Goals and non-goals

**Goals**

1. **Vertical merge of equal values** declared per column: adjacent rows with equal values in that column show
   one cell. Optionally hierarchical: a column merges only within the runs of the merging column before it
   ("Country | City").
2. **Arbitrary spans** through a resolver on the source: any rectangle of rows and display columns.
3. Correct drawing when the anchor row is outside the rendered window (virtualized away), when a range crosses
   the frozen divider or a band boundary, and with variable row heights (#102).
4. Defined semantics for hit testing, selection (Row unit and #108's Cell unit), the current cell, keyboard and
   Tab navigation, and editing.
5. Merges follow sort, filter, grouping and collapse automatically; equal-value runs update locally on
   `PropertyChanged`.
6. 0 B per steady-state frame with declarative merges; 0 B with an allocation-free resolver.
7. 100K rows: run lookups cost O(run length / 64) word reads (about 1 µs for a 100K-row run), and the index is
   built in O(n) per merging column, only when a merging column is drawn after a view change.

**Non-goals**

- Horizontal merge of equal values across columns (only through the resolver).
- Merges across group header or group summary rows, across bands, or that include non-data rows.
- Distributing a tall merged cell's content height over its rows in `Auto` row height (§7.11).
- Editing the covered cells of a resolver span (they have no value of their own on screen).
- Merging header cells (that is §6) or summary cells.

### 7.2 API

```csharp
namespace KumikoUI.Core.Models;

/// How a column merges vertically adjacent cells.
public enum KumikoCellMergeMode
{
    /// No merging (the default).
    None,
    /// Adjacent data rows with equal values (by MergeKeySelector, else the column value) form one cell.
    EqualValues,
    /// Like EqualValues, but a run also ends wherever the run of the nearest merging column to its left (in
    /// display order) ends: "City" merges only within one "Country".
    EqualValuesWithinPrevious,
}

public class KumikoColumn
{
    public KumikoCellMergeMode CellMergeMode { get; set; }

    /// Maps a cell value to the key compared for merging (for example a DateTime to its date). Null: the value.
    /// Keys are compared with CustomComparer when set, else KumikoSource.Comparer (culture-aware, numeric).
    public Func<object?, object?>? MergeKeySelector { get; set; }
}

/// Returns the merged range that contains a cell, or false when the cell isn't merged.
/// The range must contain (context.Row, context.DisplayColumn), and every cell of the range must return the
/// same range. Called on the UI thread: per visible unmerged cell per frame, and for navigation and hit testing.
public delegate bool KumikoCellMergeResolver(in KumikoCellMergeContext context, out KumikoCellRange range);

public readonly struct KumikoCellMergeContext
{
    public KumikoSource Source { get; }
    public int Row { get; }               // view row
    public int DisplayColumn { get; }
    public KumikoColumn Column { get; }
    public object Item { get; }           // the row's item
}

public class KumikoSource
{
    /// Spans beyond the declarative modes. Consulted before CellMergeMode; false falls back to the column's mode.
    public KumikoCellMergeResolver? CellMergeResolver { get; set; }

    /// The merged range containing the cell, after the clamping rules of §7.5, or false.
    public bool TryGetMergedRange(int row, int displayColumn, out KumikoCellRange range);

    /// Tells the grid that what the resolver depends on changed (the declarative index tracks itself).
    public void InvalidateMerges();
}

public class KumikoStyle
{
    /// Where a merged cell's content is laid out. Default VisibleArea.
    public KumikoMergedContentPlacement MergedCellContentPlacement { get; set; }
}

public enum KumikoMergedContentPlacement
{
    /// Within the part of the merged cell that is on screen, so a long merged run keeps its text visible.
    VisibleArea,
    /// Within the whole merged cell (centered in a 1,000-row run may be off screen).
    Range,
}
```

`CellRenderContext` gains `KumikoCellRange? MergedRange` (through a new constructor overload; the struct's
existing members are unchanged), and `KumikoHitTestResult` gains `MergedRange`. Hosts get `CellMergeResolver`
(bindable), `InvalidateMerges()`, and `CellMergeMode`/`MergeKeySelector` are column properties, so they're
already settable in XAML.

**Declarative vs resolver.** Equal-value merging is the common case (reports, pivots, schedules) and it is
cheap with an index (§7.4), so it is a column property. The resolver covers everything else (a "Total" cell
spanning three columns, a calendar event spanning hours) the way Syncfusion's `QueryCoveredRange` does. Per cell
the resolver wins; when it returns false the column's mode applies. See Q12.

### 7.3 Model: logical ranges and pieces

A merged range is a `KumikoCellRange` in view rows and display columns. It's logical: selection, navigation and
editing reason about it as one cell. For drawing and hit testing it is cut into **pieces**: its intersection
with each vertical band (top band, scrolling rows, bottom band; §5) and each column pane (left-frozen,
scrollable, right-frozen). Each piece is drawn as a merged cell of its own, showing the anchor's content. A range
that crosses the frozen divider therefore shows its text in both panes (each piece is self-contained), and a
vertical run continuing from the frozen rows into the scrolling rows shows its text again in the scrolling part.

### 7.4 Equal-value index (`KumikoMergeIndex`, internal)

Owned by the source, one per merging column:

- **Break bitset** `ulong[]` over view rows: bit `r` set means row `r` starts a new run. Bits are set at row 0,
  at every row whose key differs from the previous row's, at every non-data row and the row after it, at the
  band boundaries `T` and `R - B`, at every row with expanded details (#103), and, for
  `EqualValuesWithinPrevious`, wherever the previous merging column's bitset has a bit (OR of the words).
- **Run lookup** for row `r`: the top is the highest set bit ≤ `r`, the bottom is the lowest set bit > `r`,
  minus 1 (or `R - 1`). Both scan words with `BitOperations.LeadingZeroCount`/`TrailingZeroCount`: O(run / 64).
  A 100K-row run is about 1,600 word reads (≈ 1 µs). Runs of one row aren't merged.
- **Build**: lazily, on the first lookup after the view changed or the column's merge settings, key selector,
  comparer, culture or display order (for `WithinPrevious`) changed. One pass reading each row's key and
  comparing it with the previous one: O(n), ≤ 3 ms per column at 100K rows. Value types are boxed by the
  accessor here (one short-lived box per row), which a sort already does about log n times per row; the build
  keeps only the previous key.
- **Local updates**: `PropertyChanged` on the column's property (the `InvalidateCellText(item, propertyName)`
  test) and `SetCellValue` recompute bits `r` and `r + 1` from the neighbors: O(1). Incremental insert/remove
  shift the bitset (a memmove of n/64 words) and recompute around the change. A collection reset, `Refresh` or a
  culture change marks the index dirty.

Memory: n/8 bytes per merging column (12.5 KB at 100K rows, 125 KB at 1M), grown by doubling, reused across
builds.

### 7.5 Range rules

`TryGetMergedRange(row, displayColumn)`:

1. Not a data row → false.
2. Resolver set and returns true → take its range; check it contains the cell (else ignore it and report a
   debug assertion); clamp it to the contiguous data rows around `row` in the same band (non-data rows, band
   boundaries and rows with expanded details cut it) and to the visible display columns.
3. Else the column's mode → the run from §7.4, one column wide.
4. A result of one cell → false.

Merged ranges must not overlap. The resolver contract says so; the renderer enforces it per frame (first range
wins in top-left order, later overlapping ones are drawn unmerged, and a debug assertion fires), so a buggy
resolver produces a visible but stable result instead of overdraw.

### 7.6 Rendering

In each pane pass and each vertical band, before `DrawRows`:

1. **Coverage pass.** Walk the band's visible rows × the pane's visible columns, skipping cells already marked
   in a reused `bool[]` coverage mask. For each unmarked cell call `TryGetMergedRange`; for a range, compute its
   piece (range ∩ band rows ∩ pane columns), add it to a reused `List<MergedPiece>` and mark its visible cells.
   Cost: one lookup per merged piece plus one per unmerged cell, the same order as `CellStyleResolver` calls
   today.
2. **Rows.** `DrawRows`/`DrawRowCells` draw row backgrounds as today and skip the content and the per-cell
   background of covered cells.
3. **Grid lines** as today.
4. **Pieces.** For each piece, with the band clip already set:
   - rect: X from the pane's column positions; Y from `top(range.TopRow)` to `top(range.BottomRow + 1)` through
     the row layout (#102), so it may extend above or below the clip;
   - fill with the anchor cell's resolved background (`ResolveCellStyle` for the anchor item and column), else
     `Background`. Alternate-row striping and `RowStyleResolver` backgrounds don't show inside a merged cell
     (Q13);
   - selection strips: in Row unit, each selected or focused row inside the piece gets its row strip filled with
     `SelectionBackground`/`FocusedRowBackground` over the piece's width, so a row's highlight stays continuous
     across the grid. In Cell unit (#108), the whole piece is filled when its range is selected;
   - stroke the piece's four edges with the grid line paint (this covers the interior grid lines step 3 drew);
   - content: the anchor's value and display text through the anchor text cache (below), drawn by the column's
     `ICellRenderer` with `Bounds` = the content rect: the piece ∩ the visible data area for `VisibleArea`, the
     whole piece for `Range`, minus padding; `MergedRange` is set and `IsSelected` is true only when the whole
     piece is selected.
5. **Current-cell border.** When the current cell is inside a piece, the border is drawn around the piece's
   visible part.

**Anchor text cache.** A piece's anchor row is often outside the rows the display text cache holds (scrolled
away above the viewport). `GetCachedCellText` gets a small fixed set of anchor slots (16 per column, LRU) next
to the band and ring slots, keyed by (anchor row, item). A steady-state frame reads its anchors from there and
allocates nothing.

**Pane passes.** Pieces are computed per pane, so the left-frozen piece and the scrollable piece of one range are
drawn in their own clips and the frozen divider is drawn over the boundary as today. Group rows, sticky group
headers, the pinned footer and the bands are drawn after the panes and cover pieces exactly as they cover rows.

### 7.7 Hit testing

After `HitTestCore` resolves the row (`RowIndex`, the row under the pointer) and the column, it calls
`TryGetMergedRange`. When merged, the result keeps `RowIndex` = the pointer row, sets `ColumnIndex`/`Column` to
the **anchor column** (covered columns of a span have no content of their own), and sets `MergedRange`. The
selection model then decides what the click means (§7.8). Interactive renderers (checkbox, action buttons) hit
test against the piece's content rect, the same rect the renderer used, which `GetCellScreenBounds` returns for a
merged cell.

### 7.8 Selection, current cell and navigation

Equal-value merges are cosmetic: every row is still its own item. Resolver spans can be structural. The rules
keep row-based grids working and give the cell unit Excel's behavior:

| Situation | Row unit (default) | Cell unit (#108) |
|---|---|---|
| Click a merged cell | selects the pointer row; current cell = (pointer row, anchor column) | current cell = the anchor; the selection is the whole range |
| Up / Down | move one row (the current cell may stay inside the piece); the border marks the piece | from inside a range, move to the row above its top / below its bottom; entering a range lands on its anchor |
| Left / Right | skip the covered columns of a horizontal span | same |
| Tab (`FindNextTabStop`) | skips covered columns | skips covered cells |
| Shift+arrows, drag-select | row ranges as today | ranges expand to include every merged range they touch, repeated until stable (Excel) |
| Copy (#44/#108) | each row's own values | equal-value merges: each cell's own value; resolver spans: anchor value in the anchor cell, empty strings for covered cells |

Entering a range "lands on its anchor" cannot loop: ranges don't overlap, so the range below a range's bottom
starts exactly at bottom + 1 in that column.

### 7.9 Editing

- **Equal-value merges.** The edit is on the current cell's own row (Row unit) or the anchor (Cell unit). While
  editing, the renderer treats the edited cell as a run break for that frame: the piece is drawn split around
  the cell, and the editor (`GetEditorCellBounds`) covers that cell's own rect. On commit, `SetCellValue`
  updates the index locally (§7.4), so the run splits or joins on the next frame.
- **Resolver spans.** `BeginEdit` on any cell of the range edits the anchor (anchor item, anchor column), and the
  editor covers the visible piece. Covered cells aren't editable on their own.
- `EditSession.ApplyViewChange` is unchanged: it follows the edited item.

### 7.10 Sort, filter, grouping and pins

Equal-value runs are a function of the view, so a rebuild marks the index dirty and the next lookup rebuilds it.
Collapse/expand rebuilds the view and with it the runs. Group rows and band boundaries break runs (§7.4). A
resolver gets the current view through `context.Source` and must compute from it (for example comparing
`GetItem(row - 1)`); `InvalidateMerges()` exists for state the grid can't see.

### 7.11 Variable row height (#102)

- A piece's height is the sum of its rows' heights (`top(bottom + 1) - top(top)`), two prefix queries per
  piece.
- `Auto` measurement (#102 §7.1) skips cells covered by a vertical merge (row span > 1): their content doesn't
  set any row's height. A horizontal-only span (one row) is measured at the span's width and counts for its row.
  Content taller than a vertical piece is ellipsized. Distributing a merged cell's height over its rows would
  need an iterative layout, and it is a non-goal.
- Merge breaks don't depend on heights, so measurement never invalidates the merge index.

### 7.12 Allocation and performance (100K rows)

| Operation | Cost | Allocation |
|---|---|---|
| Frame, steady state, 2 equal-value columns, 30×10 visible | ≤ 25 µs (budget; 20 µs today) | 0 B |
| Frame with an allocation-free resolver | + one delegate call per visible unmerged cell and per piece | 0 B |
| Index build after a sort, per merging column | ≤ 3 ms at 100K | bitset reused; value boxes are short-lived |
| `PropertyChanged` in a merging column | O(1) bit update | 0 B |
| Run lookup | O(run / 64) word scans | 0 B |
| Insert/remove one item | O(n / 64) memmove | 0 B once sized |

The coverage mask, the piece list and the anchor slots are grown, never shrunk, and reused. No LINQ on the frame
path.

### 7.13 Accessibility

- UIA: a merged range is one `GridItem` with `IGridItemProvider.RowSpan`/`ColumnSpan` and `Row`/`Column` at the
  anchor; covered cells are not exposed, and `IGridProvider.GetItem` on covered coordinates returns the anchor's
  element.
- WASM: the anchor `gridcell` gets `aria-rowspan`/`aria-colspan`; covered cells are omitted.
- Android: `CollectionItemInfo` with `rowSpan`/`columnSpan`.
- iOS: one accessibility element per merged range, framed to its visible piece.
- In Row unit with equal-value merges, the row-based reading order (VoiceOver/TalkBack row by row) would skip
  the merged value for every row but the anchor. #79's provider should report each row's own value for
  equal-value merges when navigating by row (the data is there; only the drawing is merged). Q11.

### 7.14 Test plan

- **Index** (`CellMergeIndexTests`): runs for equal values, key selector, custom comparer, culture, `WithinPrevious`
  following display order changes, breaks at group rows, band boundaries and details rows; a randomized
  differential test against a naive run computation over 10K operations (set value, insert, remove, sort,
  filter, group, collapse); lookups on a 100K-row single run.
- **Resolver**: precedence over the column mode, clamping to data rows, bands and visible columns, a range that
  doesn't contain its cell is ignored, overlaps resolved first-wins with a debug assertion.
- **Rendering** (`RecordingDrawingContext`): pieces for a range crossing the frozen divider, crossing the top band
  into the scrolling rows, with the anchor 1,000 rows above the viewport (content in the visible area, anchor
  text from the anchor slots), `Range` placement, interior grid lines covered, selection strips in Row unit,
  whole-piece fill in Cell unit, current-cell border around the piece, sticky headers and footer drawn over
  pieces.
- **Hit testing and input**: pointer row and anchor column, interactive renderer bounds, navigation tables of
  §7.8 in both units, Tab skipping, edit placement for both kinds, local index update after commit.
- **#102**: piece heights with mixed row heights; `Auto` measurement skips vertical merges.
- **Allocation**: `SteadyState_EqualValueMergesAcrossPanesAndBands_AllocatesNothing`, and the same with a static
  resolver.
- **Performance**: `RenderBenchmarks` with merges; index build at 100K in `DataBenchmarks`.

### 7.15 Implementation issues

**C1. Cell merging: merge model, equal-value index and resolver (no rendering)**

- `KumikoCellMergeMode`, `KumikoColumn.CellMergeMode`/`MergeKeySelector`, `KumikoCellMergeResolver`,
  `KumikoCellMergeContext`, `KumikoSource.CellMergeResolver`/`TryGetMergedRange`/`InvalidateMerges`, and
  `KumikoCellRange` if #108 hasn't added it; documented, PublicAPI.
- `KumikoMergeIndex` (§7.4) with lazy build, local updates, insert/remove shifts; range rules (§7.5).
- Index and resolver tests; no rendering change (merges are queryable but not drawn yet).

**C2. Cell merging: rendering of merged pieces across bands, panes and virtualized rows**

- Coverage pass, piece list, piece drawing (§7.6) in all panes and bands, anchor text slots,
  `MergedCellContentPlacement`, `CellRenderContext.MergedRange`, selection strips (Row unit), current-cell
  border around pieces.
- Rendering and allocation tests; frame budget in `bench/README.md`.

**C3. Cell merging: hit testing, current cell, navigation and editing**

- `KumikoHitTestResult.MergedRange`, the Row-unit rules of §7.8, Tab skipping, interactive renderer bounds,
  editing rules of §7.9 (split-while-editing for equal values, anchor edit for spans).
- Input tests.

**C4. Cell merging with the cell selection unit (after #108)**

- Cell-unit rules of §7.8: anchor normalization, range expansion to merged ranges, whole-piece selection fill,
  copy semantics.
- Tests in #108's selection suites.

**C5. Cell merging on MAUI and Uno, samples and docs**

- Host `CellMergeResolver` and `InvalidateMerges()`; a "Schedule" sample (equal-value merges with
  `WithinPrevious`, a resolver span, 100K rows) on both hosts; README and RENDERING.md.

**C6. Cell merging with variable row height (after #102 stage 3)**

- Piece heights from the row layout, `Auto` measurement skips vertical merges and measures horizontal spans at
  span width (§7.11). Tests.

---

## 8. Cross-cutting

### 8.1 Stage order and dependencies

```
#108 stage 1 (DisplayColumns) ─┬──────────────▶ B1 ─▶ B2 ─▶ B3 ─▶ B4
                               └──────────────▶ C1 ─▶ C2 ─▶ C3 ─▶ C5
G0 (geometry; with #102 stage 2) ─┬─▶ A1 ─▶ A2 ─▶ A3 ─▶ A4
                                  ├─▶ B1
                                  └─▶ C2
#108 stages 2–4 ─▶ C4            #102 stage 3 ─▶ C6            #79 ─▶ X1
```

**G0. Grid geometry: one snapshot of header, bands and panes for the renderer, hit tester, input controller and
scroller (no behavior change)**

- `KumikoGridGeometry` (§4.1) used by every band and pane computation listed in §3; `grep HeaderHeight` and
  `grep EffectiveFrozenRowCount` in `src/KumikoUI.Core` find only the geometry, the style and the source.
- Lands together with, or directly on top of, #102 stage 2.
- All existing renderer, hit-test, pinned-summary, sticky-header, scroller and keyboard tests pass unchanged;
  `RenderAllocationTests` stays at 0 B; the 30×10 frame stays within 5% of `main`.

**X1. Accessibility for pinned rows, column bands and merged cells (after #79)**

- The provider members of §5.8, §6.10 and §7.13 on every platform #79 covers, with tests per platform where #79
  has a harness.

The three features are independent of each other after G0: A, B and C can be implemented in parallel by
different people, in any order.

### 8.2 Risks

| # | Risk | Mitigation |
|---|---|---|
| R1 | **Geometry drift** between renderer, hit tester, input controller and `KumikoScroller` (CORE-09 again), now with two more vertical bands and a multi-row header | G0 first; every A/B/C stage has a hit-test test for each new band that would catch a mismatch |
| R2 | **G0 and #102 stage 2 rewrite the same lines** and conflict | One issue owns both, or #102 stage 2 merges first and G0 rebases; called out in both issues |
| R3 | **Row index semantics with pins**: apps that treat `SelectedRows`/`GetItem(row)` indices as "sorted position" see pinned rows first | Pinning is opt-in; documented in the README and `FixedTopRowCount` docs; item-based APIs (#30) are the recommended path |
| R4 | **Resolver misuse**: slow, allocating or inconsistent resolvers, or ones that mutate the source during a frame | Contract in XML docs; first-wins overlap handling and debug assertions; allocation test with a static resolver; the view version is checked after the coverage pass (a rebuild during it requests a redraw, as #102 does for its resolver) |
| R5 | **Display order prerequisite** (#108 stage 1) slips | B1/C1 land it themselves with #108's names |
| R6 | **Fixed bands taller than the data area** (many pins, tall #102 rows, row details in a band) leave no scrolling rows | Same as `FrozenRowCount` today: clipped; documented; a debug warning when the top and bottom bands exceed the data area; Q5 asks whether to cap |
| R7 | **Header height grows** with bands and reduces the scrolling area on phones | `BandHeaderHeight` is separate and smaller (32) than `HeaderHeight`; samples show a compact theme |
| R8 | **Theme replacement resets new style members** (MAUI-06) | Host stages (A4, B4) re-apply them like the existing members |
| R9 | **`ColumnReordered` consumers** that call `ReorderColumn` themselves move only the first column of a band | `ColumnCount` added; README documents `MoveColumns`; hosts updated in B4 |
| R10 | **RTL (#80)** mirrors X everywhere; sticky band labels, merged pieces across panes and the anchor ("top-start") must use the same mirrored pane edges | The #80 spec takes `KumikoGridGeometry` pane edges as its single source; this spec's rules are stated in display order so they mirror without change |
| R11 | **Accessibility gap** until #79 ships: merged and banded structure is invisible to assistive tech | X1 tracked; MAUI quick wins (#77) don't regress |
| R12 | **Export and clipboard** (#105, #44, #108) must agree on merged values and band headers | §7.8 and §6.8 state the rules for those specs to adopt |

### 8.3 Open questions

1. **Pins also in place.** Should an option show a pinned item in the body as well (AG Grid style), or is "moved,
   not copied" (the proposal) enough?
2. **Bottom band placement.** The proposal hugs a short list (bottom pins directly after the last body row) and
   docks to the data area's bottom otherwise. The pinned footer always docks. Should both behave the same, and
   which way?
3. **Pin by drag.** Should dragging a body row into a band pin it (and out of a band unpin it) in 1.x, or stay a
   host feature built on `Pin`/`Unpin`?
4. **Summaries and pins.** Table summaries aggregate filtered items, pinned or not, and skip pinned items shown
   only because filters don't apply to them. Is that right, or should pinned rows be excluded (or always
   included)?
5. **Band size cap.** Should the grid cap the top and bottom bands (for example at half the data area each) and
   scroll the rest, or keep today's clipping?
6. **`FrozenRowCount` while grouping.** It stays 0 while grouping. With pins working in grouped views, should
   frozen rows also apply to the first body rows of a grouped view?
7. **Selector inputs.** `PinnedItemSelector` is re-evaluated on every `PropertyChanged` of an item. Should it
   instead declare the properties it depends on (cheaper, but more API)?
8. **Naming and references.** `KumikoColumnBand` + string keys (proposal) or `KumikoColumnGroup` with object
   references (more natural in C#, no XAML story on Uno)? Should `KumikoColumn.Id` (CORE-30) become the stable
   column key instead of adding `BandKey`-style strings elsewhere later?
9. **Collapsible bands.** Is a chevron on band headers that hides all but the first column a 1.x follow-up?
10. **Band click selects columns.** In #108's Cell unit, should a band header click select the band's columns?
11. **Accessible names.** Should cell names include the band path by default, and should equal-value merges be
    read per row (each row's own value) in Row unit? Both are #79 decisions informed by this spec.
12. **Resolver home.** The resolver is on `KumikoSource` (it affects navigation and selection, which run outside
    a frame). `RowHeightResolver` (#102) and the style resolvers are on `KumikoStyle`. Accept the split, or move
    both kinds of structural resolver to the source?
13. **Row colors in merged cells.** Merged cells use the anchor cell's style or `Background`; alternate rows and
    `RowStyleResolver` backgrounds don't show inside them. Should a column opt into striping merged cells by
    their anchor row's background instead?
14. **Equal-value merge key.** Values are compared (culture-aware, through `CustomComparer`). Should the
    displayed text be compared instead (what the user sees, but it formats every row on build)?
15. **Merges and pins across band boundaries.** Merges stop at band boundaries (§7.5). Is a merged run that
    continues from the frozen rows into the scrolling rows (drawn twice, once per band) acceptable, or should
    `FrozenRowCount` be adjusted to the end of the run?

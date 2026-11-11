# Spec: variable row height and text wrapping (#102)

| | |
|---|---|
| Issue | [#102](https://github.com/TheEightBot/KumikoUI/issues/102) (plan key X4, milestone 1.x – Fast follow-ons) |
| Status | Draft for review |
| Blocked by | [#23](https://github.com/TheEightBot/KumikoUI/issues/23) text measurement cache and wrapping (PR #199) |
| Blocks | [#103](https://github.com/TheEightBot/KumikoUI/issues/103) row details / master-detail |
| Builds on | [#56](https://github.com/TheEightBot/KumikoUI/issues/56) programmatic scrolling (PR #198), [#204](https://github.com/TheEightBot/KumikoUI/issues/204) removal of `KumikoRowStyle.Height` (PR #210), [#174](https://github.com/TheEightBot/KumikoUI/issues/174) sticky group headers |
| Findings | CORE-30, SKIA-05 ([01](../release-readiness/01-core-data-layout-render.md), [03](../release-readiness/03-skiasharp-maui.md)); research §1.3 item 9 ([07](../release-readiness/07-external-research.md)) |

## 1. Summary

Every row in KumikoUI is `KumikoStyle.RowHeight` tall, and that constant is multiplied into the geometry in
dozens of places: the renderer, the hit tester, the input controller, the sticky group header layout and (once #56
merges) `KumikoScroller`. #23 adds wrapped text to `IDrawingContext` and `KumikoColumn.TextWrap`, but a wrapped
cell can only show as many lines as the fixed row height holds.

This spec adds:

- **Row height modes.** `Fixed` (today's behavior and the default), `Auto` (a row is as tall as its tallest
  wrapped cell, within `MinRowHeight`/`MaxRowHeight`), and a `RowHeightResolver` delegate that can set or adjust
  the height of any data row in either mode.
- **One geometry service**, `KumikoRowLayout`, owned by the `KumikoSource`. Every row-to-Y and Y-to-row question
  goes through it. In the uniform case it is the current arithmetic. Otherwise it is a Fenwick tree of row
  heights that answers both questions in O(log n). Rows that have not been measured yet use an estimate, and
  changing the estimate costs O(1).
- **Measurement in the frame.** Rows are measured only inside `KumikoRenderer.Render` (which has the
  `IDrawingContext`), only when they are about to be drawn, and only once per item, content version and column
  width. Heights are cached per item, so they survive sort, filter and grouping. A steady-state frame measures
  nothing and allocates nothing.
- **Scroll anchoring.** When measuring a row changes the content above the viewport, the vertical offset is
  corrected so the visible rows don't move. Programmatic scrolls to rows that haven't been measured are finished
  on the next frame.

## 2. Goals and non-goals

### Goals

1. `Fixed` mode (the default) keeps today's geometry exactly: same pixels, same hit testing, same scroll extents.
   It also keeps the same cost: no per-row arrays and 0 B per frame. All existing tests pass unchanged.
2. `Auto` mode sizes each data row to its content when one or more visible columns wrap
   (`KumikoColumn.TextWrap != None`), or have a cell renderer that can measure itself.
3. `RowHeightResolver` lets an app set a row's height from its item (for example "pinned rows are 56 px"), or
   adjust the measured height (for example "at least 48 px when the item has a note").
4. Every geometry consumer agrees: drawing, hit testing, keyboard navigation and paging, ensure-visible,
   programmatic scrolling, sticky group headers, frozen rows, summaries, row drag, the editor overlay and the
   drag handle column.
5. 100K rows scroll smoothly in `Auto` mode. Queries are O(log n), index rebuilds are O(n), and memory is bounded
   per row (§8).
6. The steady-state render loop stays allocation-free (#68), in both modes.
7. Behavior added in 1.x doesn't break the 1.0 public API (see [VERSIONING.md](../VERSIONING.md)).

### Non-goals (this spec)

- Variable heights for **group header rows, group summary rows, table summary rows or the column header**. They
  keep `RowHeight` / `SummaryRowHeight` / `HeaderHeight`. Sticky group header slots therefore stay
  `level * RowHeight` (see §7.4).
- A **multi-line cell editor**. `DrawnTextBox` stays single-line. Editing a wrapped cell shows the single-line
  editor over the full row height (§7.11). Multi-line editing is a follow-up issue.
- **Exact scroll extents before every row is measured.** In `Auto` mode, rows that haven't been measured use an
  estimate, and the scroll range converges as rows are measured. Optional idle measurement (stage 8) speeds this
  up.
- Row spans and cell merging, animated height transitions, rich text, measurement on a background thread.
- Row details (#103). This spec only leaves the hook it needs (§13).

## 3. Current state (main, plus the open PRs)

| Area | Where the uniform height lives today |
|---|---|
| Extent | `KumikoLayoutEngine.GetContentHeight(rowCount, style)` = `HeaderHeight + rowCount * RowHeight`; `KumikoRenderer.Render` adds the group panel and summaries; `KumikoScroller.RefreshContentExtents` (#56) does the same again |
| Visible range | `KumikoLayoutEngine.GetVisibleRowRangeWithOffset`: `first = (int)(OffsetY / RowHeight)` |
| Frozen band | `KumikoLayoutEngine.GetFrozenRowHeight` = `frozenRowCount * RowHeight`; repeated inline in `DrawRows`, `DrawGridLines`, `DrawCurrentCellBorder`, `DrawDragHandleColumn`, `DrawRowDragOverlay`, `KumikoHitTester.HitTestCore`, `KumikoInputController.FinishRowReorder` / `GetScrollableRowsViewportHeight` / `GetCellScreenBounds` |
| Row Y | `HeaderHeight + topSummaryHeight + frozenRowHeight + (row - frozen) * RowHeight - OffsetY` in about 12 copies (renderer, input controller, `KumikoScroller.GetRowY`) |
| Y to row | `(int)((y - scrollableDataTop + OffsetY) / RowHeight) + frozen` in `KumikoHitTester.HitTestCore`, `FinishRowReorder` and `DrawRowDragOverlay` |
| Sticky headers | `PinnedGroupHeaders.Compute`/`SlotOffset`/`RowTop` divide and multiply by `RowHeight` |
| Paging | `pageSize = viewportHeight / RowHeight`, passed to `KumikoSelectionModel.NavigatePageUp/Down` |
| Ensure visible | `EnsureCurrentCellVisible` on main; `KumikoScroller.GetRowOffsetY` on #56, whose remarks say "every row has the same height" |
| Bottom summary | `dataAreaTop + RowCount * RowHeight - OffsetY` in `Render` and `HitTestCore` |
| Wheel | `RowHeight * WheelScrollMultiplier` per notch (`HandleScroll`); it's a line unit, not geometry, and stays as is |
| Dead API | `KumikoRowStyle.Height`: never read (CORE-30); removed by #210 |
| Wrapping | #23: `KumikoTextWrap`, `KumikoColumn.TextWrap`, `KumikoTextLayout`, `IDrawingContext.DrawTextInRect(..., wrap)` and `MeasureText(text, paint, maxWidth, wrap)`; `SkiaResourceCache` caches widths and `KumikoTextLines` by `(text, font, width, wrap, lineCount)`. The `TextWrap` docs say "rows keep their height (#102 adds variable row heights)". |

The duplication is the main risk. Porting the geometry to one service is a refactor of its own (stage 2), and it
lands with no change in behavior before any height becomes variable.

## 4. API

All new public members get XML docs (#54: doc warnings are errors) and entries in the PublicAPI files.

### 4.1 Core

```csharp
namespace KumikoUI.Core.Models;

/// How data row heights are determined.
public enum KumikoRowHeightMode
{
    /// Every data row is KumikoStyle.RowHeight tall (the default; 1.0 behavior).
    Fixed,

    /// A data row is as tall as its tallest measurable cell plus CellVerticalPadding above and below, within
    /// MinRowHeight and MaxRowHeight.
    Auto,
}

public class KumikoStyle
{
    // Existing: the Fixed height, the estimate for unmeasured Auto rows, and the height of group header and
    // group summary rows in every mode.
    public float RowHeight { get; set; } = 36f;

    public KumikoRowHeightMode RowHeightMode { get; set; } = KumikoRowHeightMode.Fixed;

    /// Auto mode: the shortest a data row is laid out. Null means RowHeight, so rows without wrapped content look
    /// exactly as in Fixed mode.
    public float? MinRowHeight { get; set; }

    /// Auto mode: the tallest a data row is laid out; wrapped text beyond it ends with an ellipsis (#23's
    /// DrawTextInRect behavior). Null means no limit.
    public float? MaxRowHeight { get; set; }

    /// Auto mode: space above and below the tallest cell content. Default 6.
    public float CellVerticalPadding { get; set; } = 6f;

    /// Sets or adjusts the height of a data row. Called only while a frame is drawn, on the render thread,
    /// once per item and content version (not every frame). Null result: the mode's height.
    public KumikoRowHeightResolver? RowHeightResolver { get; set; }
}

/// Returns a data row's height in pixels, or null for the height its mode gives it.
public delegate float? KumikoRowHeightResolver(in KumikoRowHeightContext context);

/// What a KumikoRowHeightResolver is asked about. Valid only during the call.
public readonly struct KumikoRowHeightContext
{
    public object Item { get; }                 // the row's item
    public int Row { get; }                     // its row in the flat view (changes with sort/filter/grouping)
    public KumikoRowHeightMode Mode { get; }
    public float DefaultHeight { get; }         // KumikoStyle.RowHeight

    /// The Auto height of this row (measured now, clamped to Min/MaxRowHeight), whatever the mode. Lets a
    /// Fixed-mode grid auto-size selected rows, or an Auto-mode grid adjust the measurement.
    public float MeasureContentHeight();
}
```

The three behaviors the issue asks for (fixed, auto and resolver) come from a two-value enum plus an optional
resolver:

| `RowHeightMode` | `RowHeightResolver` | Data row height |
|---|---|---|
| `Fixed` | null | `RowHeight` (uniform fast path: no per-row state at all) |
| `Auto` | null | measured, clamped to `[MinRowHeight ?? RowHeight, MaxRowHeight ?? ∞]` |
| either | set | the resolver's value when not null, else the mode's height |

A resolver result that is NaN, infinite or below 1 is treated as null (1 px is the floor: zero-height rows would
break the Y-to-row search, §6). The resolver's own value is **not** clamped to Min/Max, because an explicit
answer wins. `MeasureContentHeight()` returns a clamped value. A resolver-only grid (Fixed plus resolver) never
measures text unless the resolver calls `MeasureContentHeight()`.

**Why on `KumikoStyle`.** `RowHeight`, `StickyGroupHeaders`, `RowStyleResolver` and `CellStyleResolver` already
live there, and the renderer, the hit tester and `KumikoScroller` all receive the style. The cost is that a
theme or `GridStyle` replaces the style instance, so the hosts must re-apply these properties to the new style.
`RowHeight` has the same problem today (MAUI-06), and one fix should cover both. See open question Q1.

**Per-column wrapping** is #23's `KumikoColumn.TextWrap` (`None`, `Word`, `Character`). This spec changes only its
docs: in `Auto` mode a wrapping column makes its row tall enough for all of its lines. No new column property is
needed.

**Custom renderers.** A new optional interface lets a cell renderer take part in `Auto` mode, following the
`IInteractiveCellRenderer` pattern:

```csharp
namespace KumikoUI.Core.Rendering;

/// A cell renderer that can report how tall its content wants to be at a given width.
public interface IMeasurableCellRenderer
{
    /// The content height (without CellVerticalPadding) of the cell at cell.Bounds.Width, or null when the
    /// content doesn't constrain the row height. Called only for Auto measurement, never per frame.
    float? MeasureHeight(IDrawingContext ctx, in CellRenderContext cell);
}
```

`TextCellRenderer` implements it. It returns null for `TextWrap.None` (a single line never needs more than the
minimum height), and otherwise `ctx.MeasureText(text, paint, width, wrap).Height`. Image, boolean and progress
renderers don't implement it in v1.

**Invalidation** (public, because the grid can't observe what a resolver or a `CellStyleResolver` depends on):

```csharp
public class KumikoSource
{
    /// Re-measures every row on the next frame (heights that were measured stay in use as estimates until then).
    public void InvalidateRowHeights();

    /// Re-measures the rows showing item on the next frame they are drawn.
    public void InvalidateRowHeight(object item);
}
```

**Programmatic scrolling** (#56) keeps its signatures. One read-only query is added for row details (#103) and
custom overlays:

```csharp
public static class KumikoScroller
{
    /// The height of row in the flat view as laid out now (an estimate for an unmeasured Auto row).
    public static float GetRowHeight(KumikoSource source, KumikoStyle style, int row);
}
```

`KumikoRowLayout` itself (§5) stays `internal` in v1 (see Q11).

### 4.2 Hosts (MAUI and Uno)

| Property | Type | Default | Notes |
|---|---|---|---|
| `RowHeightMode` | `KumikoRowHeightMode` | `Fixed` | bindable / dependency property |
| `MinRowHeight` | `double` | `NaN` (meaning null) | NaN rather than `double?` for XAML friendliness; same convention on both hosts |
| `MaxRowHeight` | `double` | `NaN` | |
| `CellVerticalPadding` | `double` | `6` | |
| `RowHeightResolver` | `KumikoRowHeightResolver?` | null | bindable, so a view model can supply it |
| `InvalidateRowHeights()` / `InvalidateRowHeight(object)` | methods | | forward to the source and redraw |

All of them must survive a `Theme` or `GridStyle` change (MAUI-06) and are documented in the README's sizing
section. `KumikoColumn.TextWrap` is already settable from XAML through the column.

## 5. Architecture: `KumikoRowLayout`

```
KumikoSource ──owns──▶ KumikoRowLayout (internal, Core/Layout)
   │ RebuildCore / TryIncrementalUpdate /           ├─ uniform fast path (Fixed, no resolver): arithmetic only
   │ OnItemPropertyChanged / Refresh ──notify──▶   ├─ per-view-row arrays: height, state stamp
   │                                               ├─ Fenwick tree over view rows (sum of measured, count of unmeasured)
   │                                               ├─ per-item measured-height cache (survives rebuilds)
   ▼                                               └─ stamps: content, width signature, style snapshot
consumers: KumikoRenderer, KumikoHitTester, KumikoInputController, PinnedGroupHeaders, KumikoScroller,
           KumikoLayoutEngine overloads, EditSession placement (through the renderer)
measurer (internal KumikoRowMeasurer, used only by KumikoRenderer.Render with the frame's IDrawingContext)
```

**Ownership.** The layout belongs to the `KumikoSource` (lazily created, `internal KumikoRowLayout RowLayout`).
There are three reasons:

1. Every consumer already receives the source.
2. The source knows synchronously when the view changes (`RebuildCore`, `TryIncrementalUpdate`,
   `ToggleGroupExpansion`), so the index is marked dirty before any `ViewChanged` listener (the selection binder,
   a host's ensure-visible) asks for geometry.
3. Column widths already live on the shared `KumikoColumn` objects, so a source is already one grid's layout.
   Showing one source in two grids with different widths isn't supported today, and won't be.

The layout holds no style. Each query takes the `KumikoStyle`, and the layout notices a different instance or a
changed `Version` (§6.4).

### 5.1 Internal surface

```csharp
internal sealed class KumikoRowLayout
{
    bool IsUniform(KumikoStyle style);                 // Fixed && no resolver
    int RowCount { get; }
    double GetRowTop(int row, KumikoStyle style);      // from the top of row 0 (frozen rows included)
    float GetRowHeight(int row, KumikoStyle style);
    int GetRowAt(double y, KumikoStyle style);         // row containing y; clamped to [0, RowCount - 1]
    double GetTotalHeight(KumikoStyle style);          // all rows
    double GetFrozenHeight(int frozenRowCount, KumikoStyle style) => GetRowTop(frozenRowCount, style);
    bool IsMeasured(int row);

    // Frame protocol, called only by KumikoRenderer.Render (§7.1)
    void BeginFrame(KumikoStyle style, IReadOnlyList<KumikoColumn> columns);
    bool MeasureRows(int first, int last, KumikoRowMeasurer measurer, ref MeasureBudget budget);  // true if any height changed
    (int Row, int Position)? PendingScrollTarget { get; set; }

    // From KumikoSource
    void OnViewRebuilt();                  // sort, filter, grouping, expand/collapse, SetItems: mark index dirty
    void OnRowsInserted(int index, int count);
    void OnRowsRemoved(int index, int count);
    void InvalidateItem(object item);      // PropertyChanged, SetCellValue, InvalidateRowHeight(item)
    void InvalidateAll();                  // Refresh, culture change, collection Reset, InvalidateRowHeights()
}
```

Coordinates: `GetRowTop` is measured from the top of the first row (row 0, a frozen row when there are any). The
frozen band is `GetRowTop(F)`. A scrolling row's offset in `ScrollState.OffsetY` space is
`GetRowTop(row) - GetRowTop(F)`, so the scroll offset keeps its current meaning: 0 shows the first row after the
frozen rows directly below the frozen band. Sums are `double` (§6.3).

## 6. Data structures

### 6.1 Per-view-row arrays (non-uniform only)

| Array | Type | Meaning |
|---|---|---|
| `_heights` | `float[]` | the row's current height: measured, stale-but-measured, or 0 for unmeasured (the estimate applies) |
| `_state` | `int[]` | the content stamp the height was measured at (0 means unmeasured) |
| `_sum` | `double[]` (1-based Fenwick) | sums of `_heights` over measured rows |
| `_unmeasured` | `int[]` (1-based Fenwick) | counts of unmeasured rows |

Group header and group summary rows are written as measured with `RowHeight` when the index is built, so they
never use the estimate. The arrays grow by doubling, are never shrunk below the high-water mark during a view's
lifetime, and are released when the layout returns to the uniform path or `SetItems` drops the data.

### 6.2 Fenwick tree with an O(1) estimate

Row tops combine the two trees:

```
top(r)   = sum(r) + estimate * unmeasured(r)       // prefix over rows [0, r)
total    = top(RowCount)
```

- **Row to Y.** Two prefix queries, about 2 × ⌈log₂ n⌉ steps (17 at 100K rows, 20 at 1M).
- **Y to row.** Binary lifting over both trees at once: for `step` from the highest power of two down to 1, take
  `pos + step` while `sum[pos+step] + estimate * unmeasured[pos+step] ≤ remaining`. That's ⌈log₂ n⌉ steps. It's
  correct because every row is at least 1 px tall (heights are clamped to ≥ 1 and the estimate is ≥ 1).
- **Measuring a row** (unmeasured to measured, or a new height): two point updates, O(log n).
- **Changing the estimate** touches no tree: it's one field. This is why unmeasured rows are counted instead of
  stored at the estimate. In `Auto` mode the estimate is the running mean of the measured data rows (kept as a
  sum and a count, O(1) per measurement), or `max(RowHeight, MinRowHeight)` before anything is measured
  (see Q3).
- **Build** (after a rebuild, an insert or a remove): one linear pass, `tree[i] += v; j = i + (i & -i); if (j ≤ n)
  tree[j] += tree[i]`, which is O(n) with no allocation once the arrays are big enough.

Inserts and removes shift every later row. A Fenwick tree has no O(log n) insert, so `OnRowsInserted` and
`OnRowsRemoved` `Array.Copy` the per-row arrays (a memmove) and mark the tree dirty. The tree is rebuilt once, on
the next query. Several collection changes within a frame therefore cost one rebuild.

Alternatives considered:

- **Blocked prefix sums** (blocks of 256 rows plus a prefix array over the blocks). Inserts are cheaper, but
  queries are O(√n) and a height change rewrites the block prefixes. Rejected: queries and point updates are far
  more frequent than inserts.
- **A balanced tree or rope of blocks** (O(log n) for everything, including insert). Rejected for v1 because of
  its complexity and pointer-chasing reads. Revisit if the 1M-row insert budget (§8) is missed.

### 6.3 Precision

Prefix sums are `double`. A float sum of 100K varying heights accumulates rounding error, and float spacing at
4M px is 0.25–0.5 px. Results are converted to `float` only relative to the scroll offset:
`rowY = (float)(top(row) - top(F) - OffsetY) + scrollingRowsTop`. `ScrollState.OffsetY` is still a `float`
(public API), and that already limits 1M-row grids today (36M px gives a 4 px float spacing). See Q8.

### 6.4 Measured-height cache and stamps

- **Per-item cache.** `Dictionary<object, Entry>` with `ReferenceEqualityComparer` (the same identity rule as
  `CellTextCache`), where `Entry { float Height; int ContentStamp; int WidthStamp; }`. It holds only measured
  items. On `OnViewRebuilt` the per-row arrays are refilled from it with one lookup per data row, so sort, filter,
  regrouping and expand/collapse keep every measured height. `SetItems` clears it. A collection `Remove` deletes
  the item's entry.
- **Content stamps.** A global `_contentStamp` is bumped by `InvalidateAll()`. Per item, `InvalidateItem` sets the
  entry's `ContentStamp` to 0 and bumps `_dirtyGeneration`. A height whose stamp isn't current stays in the tree
  (it's the best estimate there is) and is re-measured the next time its row is in the measure window.
- **Width signature.** `BeginFrame` computes, for each visible column that can affect height (`TextWrap != None`,
  or a renderer that implements `IMeasurableCellRenderer`, or any column when a resolver is set), its content
  width (`Width - 2 * padding`), its wrap mode and its font. The font is compared by instance and properties,
  like `HeaderMeasurement.IsCurrentFor`. These go into a reused `float[]`/object array. Any difference bumps
  `_widthStamp`, which makes every height stale (so it's re-measured when drawn) without touching the tree.
- **Style snapshot.** When the style instance or `KumikoStyle.Version` changes, `BeginFrame` compares the fields
  that affect height: `RowHeight`, `RowHeightMode`, `MinRowHeight`, `MaxRowHeight`, `CellVerticalPadding`,
  `CellPadding`, `CellFont` and the resolver reference. Only a difference there invalidates heights, so a color
  change never re-measures. A change to `RowHeight` alone updates group-row heights and the estimate. A switch
  between the uniform and non-uniform paths builds or drops the arrays.
- **What does *not* invalidate.** `RebuildCore` calls `InvalidateCellText()` on every rebuild, but heights are
  per item, so the height hook is wired only at the true content-change sites:
  - `OnItemPropertyChanged` (only when the property can affect a measured column, the same test as
    `InvalidateCellText(item, propertyName)`; any property when a resolver is set)
  - `SetCellValue` / `TrySetItemValue`
  - `Refresh()`
  - the collection `Reset` path
  - culture changes

  It isn't wired into `RebuildCore` itself.

## 7. Behavior by area

### 7.1 Frame protocol (renderer)

[RENDERING.md](../RENDERING.md) step 3 ("ComputeColumnWidths") is followed by a new measurement step before
`ClampOffset`:

1. `layout.BeginFrame(style, columns)`: style snapshot, width signature, and a lazy index rebuild if the view
   changed. In the uniform case it returns immediately.
2. **Anchor.** Record `anchorRow`, the first scrolling row visible at the current offset whose height is
   measured and current (or simply the first visible row), and `anchorDelta = OffsetY - scrollTop(anchorRow)`.
3. **Measure window.** Measure the frozen rows `[0, F)` and the scrolling rows in
   `[first - overscan, last + overscan]` (overscan: 2 rows each side) whose state isn't current. Measuring a
   row resolves its cell styles the way `DrawRowCells` does (`ResolveCellStyle`, `PaintCache.CellTextPaint`), so
   the measured font and width equal the drawn ones. It reads the display text through
   `KumikoSource.GetCachedCellText` for rows in the ring and through `GetCellDisplayText` for overscan rows
   outside it. Then it asks every measurable column for `MeasureHeight`, and finally applies the resolver.
4. **Correct.** If any row above `anchorRow` changed height, set
   `OffsetY = scrollTop(anchorRow) + anchorDelta`. A pending scroll target (§7.8) takes precedence over the
   anchor.
5. **Repeat** steps 3–4 while the visible range changed, up to 3 times. After that, leftover unmeasured rows draw
   at the estimate and the renderer requests one more frame (`RedrawRequested`).
6. **Budget.** Steps 3–5 stop after 4 ms or 256 measured cells per frame, whichever comes first. Rows past the
   budget draw at their estimate, and a redraw is requested. This bounds the first frame after a jump into an
   unmeasured region.
7. `ContentHeight = HeaderHeight + total + groupPanel + summaries`, then `ClampOffset()` as today.

The anchor correction and the clamp change `OffsetY` inside `Render` and raise `ScrollState.ScrollChanged`. That
already happens today (`Render` calls `ClampOffset()`), so hosts handle it. See Q9 about marking such changes.

The draw loops (`DrawRows`, `DrawFrozenRows`, `DrawGridLines`, `DrawCurrentCellBorder`, the full-width group
pass, `DrawDragHandleColumn`) compute the first row's Y once with `GetRowTop` and then accumulate
`y += _heights[r]`. That's O(1) per drawn row with no tree query per row. Every `new KumikoRect(..., style.RowHeight)`
for a data row becomes the row's height. `DrawGroupHeaderRow` and `DrawGroupSummaryRow` keep `RowHeight`.

Wrapped text uses the same width when measured and when drawn (`cellRect.Width = col.Width - 2 * padding`), and
#23's `DrawTextInRect(..., wrap)` draws `GetMaxLines(rect.Height)` lines, so a row measured at N lines shows all N
lines. When `MaxRowHeight` caps it, the last line ends with an ellipsis. Single-line cells stay vertically
centered in a tall row (see Q6).

### 7.2 Virtual scrolling and extents

- Visible range: `first = GetRowAt(top(F) + OffsetY)` and `last = GetRowAt(top(F) + OffsetY + scrollingHeight)`,
  replacing the divisions in `KumikoLayoutEngine.GetVisibleRowRangeWithOffset`. This also fixes CORE-46's
  overdraw, because `first` no longer depends on the header height.
- `ScrollState.ContentHeight` comes from `GetTotalHeight`, both in `Render` and in
  `KumikoScroller.RefreshContentExtents`, which share one helper instead of two formulas.
- In `Auto` mode the extent is an estimate until rows are measured, so the scroll thumb can move slightly as
  rows are measured. Anchoring keeps the content still.

### 7.3 Frozen rows

Frozen rows are the first F rows of the view, so the frozen band is `top(F)`. They're measured in every frame in
which they're stale, so they're always exact. A tall frozen row grows the band and shrinks the scrolling area.
No extra cap beyond `MaxRowHeight` applies, and rows past the data area are clipped as they are today.
`GetScrollableRowsViewportHeight` (input controller on main, `KumikoScroller` on #56) uses `top(F)`.

### 7.4 Sticky group headers

Group header rows keep `RowHeight`, so the pinned slots (`scrollTop + level * RowHeight`), the push-up
(`RowTop(endRow) - RowHeight`) and `BandHeightAbove` (`levels * RowHeight`) keep their arithmetic.
`PinnedGroupHeaders` changes only where it converts between rows and offsets:

- the row under a slot: `GetRowAt(top(F) + offsetY + level * RowHeight)` instead of
  `frozen + floor((level * RowHeight + offsetY) / RowHeight)`
- `RowTop(row)`: `scrollTop + (top(row) - top(F)) - offsetY`
- `SlotOffset(headerRow, level, ...)`: `(top(headerRow) - top(F)) - level * RowHeight`

`Compute`, `BandHeightAbove` and `SlotOffset` take the source (they already do or can), so the renderer, the hit
tester and `ToggleTappedGroup` stay in agreement.

### 7.5 Summaries

- **Top table summaries:** a fixed band of `TopSummaryCount * SummaryRowHeight`, unchanged.
- **Unpinned bottom summaries:** `dataAreaTop + total - OffsetY` in `Render` and `HitTestCore`.
- **Pinned bottom summaries:** unchanged. `GetPinnedBottomSummaryHeight` still sizes the footer, and the
  scrollable viewport height subtracts it as today.
- **Group summary rows:** rows in the flat view at `RowHeight`. Caption summaries are drawn inside group headers
  and aren't affected.

### 7.6 Hit testing

`KumikoHitTester.HitTestCore`:

- frozen area: `row = GetRowAt(adjustedY - dataAreaTop)`, clamped to `[0, F - 1]`
- scrolling area: `row = GetRowAt(top(F) + adjustedY - scrollableDataTop + OffsetY)`
- pinned headers: hit by the `RowHeight` band, unchanged
- bottom summary: from the total

The legacy public `KumikoLayoutEngine.HitTestRow(contentY, style, totalRows)` assumes uniform rows and gets a
source-aware overload (§10). Round trip property: `GetRowAt(GetRowTop(r)) == r` and
`GetRowAt(GetRowTop(r) + h - ε) == r` for every row.

### 7.7 Keyboard: arrows, paging, ensure-visible

- **Arrows** go through `KumikoScroller.GetRowOffsetY` (#56), which uses `top(row)` and `GetRowHeight(row)`. The
  rows next to the viewport are within the overscan, so they're already measured.
- **PageDown/PageUp.** `pageSize = viewport / RowHeight` is replaced by an internal
  `KumikoScroller.GetPageRowCount(source, style, scroll, fromRow, direction)`. Down:
  `target = GetRowAt(top(fromRow) + pageHeight)`, stepped back one row when the target doesn't fit entirely in the
  page, and at least `fromRow + 1`. Up mirrors it. The result feeds the existing
  `KumikoSelectionModel.NavigatePageUp/Down(pageSize, ...)`, so the selection model doesn't change.
- **Rows taller than the viewport.** `Nearest` aligns the row's top. A row that's already partly visible with its
  top showing doesn't scroll.
- **Ctrl+Home/End** and other long jumps land on rows that may not be measured: see §7.8.

### 7.8 Programmatic scrolling (`KumikoScroller`, #56)

`GetRowY`, `GetRowOffsetY` (Start/Center/End/Nearest), `RefreshContentExtents` and
`GetScrollableRowsViewportHeight` switch to the layout. `GetRowOffsetY` uses the target row's height for
`End`/`Center`/`Nearest`. The remarks lose "every row has the same height".

Scrolls happen outside a frame, where there's no drawing context and nothing can be measured. `ScrollToRow` (and
so `ScrollToItem` and keyboard navigation):

1. computes the offset with the current heights (estimates for unmeasured rows) and applies it, so `Scrolled`
   fires and `VerticalOffset` updates at once;
2. if the target row isn't measured and current, sets `layout.PendingScrollTarget = (row, position)`.

The next `Render` measures around the pending target, recomputes `GetRowOffsetY` with real heights, applies it,
measures any rows that became visible (the §7.1 loop), and clears the target. The target is consumed by that frame
whatever happens. A user pan or wheel scroll before it clears the target too. `ScrollToRow` keeps returning
`true` immediately. The docs say that in `Auto` mode the final offset can be refined on the next frame (one more
`Scrolled`).

`ScrollToItem`'s `ExpandToItem` rebuilds the view, and the layout's lazy rebuild picks that up before the offset
is computed.

### 7.9 Row drag and reorder

- **Start** (`KumikoInputController`, pan start): the dragged row's screen top comes from `KumikoScroller.GetRowY`.
  This also removes today's two inline formulas that disagree about the group panel offset.
- **Ghost** (`DrawRowDragOverlay`): the ghost's height is the dragged row's height. Its text is drawn wrapped
  with the column's `TextWrap` (uncached measurement is fine; it's one row).
- **Drop index** (`DrawRowDragOverlay` and `FinishRowReorder`): `GetRowAt` at the ghost's center, so a drop
  between rows of different heights splits at their real boundary. The indicator is drawn at `top(dropIndex)`.
- **Grip icons** (`DrawDragHandleColumn`): drawn vertically centered in each row's height.

### 7.10 Auto-size columns

`KumikoLayoutEngine.CalculateAutoFitWidth` (a double-click on the resize grip, `AutoFitColumnRequested`):

- **Wrapping column:** the widest paragraph, `MeasureText(text, paint, +∞, wrap).Width` (#23: line breaks still
  start new lines), so auto-fit makes the column wide enough not to wrap, capped by `MaxWidth`.
- **Non-wrapping column:** unchanged.

The new width changes the width signature, so visible rows are re-measured on the next frame. Rows off screen
keep their old height as an estimate until they're drawn. There's no layout iteration: column widths are always
resolved first (`ComputeColumnWidths`) and heights follow. A column that is both `ColumnSizeMode.Auto` (#24) and
wrapping is sized from its sampled natural widths, then wraps within that width.

### 7.11 Editing and the current cell

`GetEditorCellBounds` and `KumikoInputController.GetCellScreenBounds` use the row's Y and height, so the editor
covers the whole (tall) cell. `DrawnTextBox` is single-line and draws vertically centered. Committing
(`SetCellValue`) invalidates the item, and the row re-measures on the next frame. The height doesn't change while
the editor is open. `DrawCurrentCellBorder` uses the row's height.

### 7.12 Mouse wheel, fling, scrollbars

Wheel scrolling stays `RowHeight * WheelScrollMultiplier` per notch: it's a line unit, and that's what desktop
users expect. Fling is pixel-based and unaffected. Any future scroll indicator reads `ContentHeight`.

## 8. Performance and memory

### 8.1 Cost of the index at 100K rows

| Item | 100K rows | 1M rows |
|---|---|---|
| Per-row arrays (`float` height, `int` stamp, `double` + `int` trees) | 20 B/row (4 + 4 + 8 + 4) = 2.0 MB | 20 MB |
| Per-item cache (measured items only; about 28 B per entry plus buckets) | ≤ 2.8 MB if everything is measured | ≤ 28 MB |
| Row to Y | 2 × 17 steps, target ≤ 100 ns | 2 × 20 steps |
| Y to row | 17 steps, target ≤ 150 ns | 20 steps |
| Point update (row measured) | 2 × 17 steps | 2 × 20 |
| Index build after a sort, filter or grouping (dictionary lookup per data row plus a linear build) | target ≤ 5 ms | ≤ 50 ms |
| Insert or remove one row (memmove plus a linear build on next query) | target ≤ 0.5 ms | ≤ 5 ms |

The uniform path uses none of this.

### 8.2 Zero-allocation constraints

- The steady state (all visible rows measured and current) allocates 0 B in both modes. Per frame there's one
  width-signature compare over reused arrays, two tree queries for the visible range, and O(visible) array
  reads. The per-item dictionary is read only when `_dirtyGeneration` changed since the last frame, and only for
  the visible rows.
- Measuring a row may allocate: #23's `KumikoTextLayout.LayOut` allocates the lines, and
  `GetCellDisplayText` may format a value. It happens once per item and content version.
- **Measurement must not evict draw layouts.** `SkiaDrawingContext.MeasureText(..., wrap)` (#23) caches
  `KumikoTextLines` under `lineCount = int.MaxValue`, while drawing caches under `GetMaxLines(rect.Height)`. The
  same text would then be laid out twice, and measuring many rows could push visible rows' layouts out of the
  4096-entry LRU. Stage 4 fixes this in two ways:
  - It normalizes the key: a draw whose `maxLines` is at least the unconstrained line count reuses the
    unconstrained entry.
  - It adds a height-only path, `KumikoTextLayout.CountLines(text, maxWidth, wrap, measure)` with a matching
    cache entry. This path runs the wrap loop over spans without allocating line strings, and is what `Auto`
    measurement calls.
- Arrays grow only on view growth (doubling), never per frame. The tree rebuild after a view change allocates
  nothing once the arrays are big enough.
- A live column-resize drag changes the width signature every frame. Visible wrapped rows are re-measured each
  of those frames, which allocates (new width, new layout key). That's acceptable because it isn't a steady
  state, but the budget below covers it.

### 8.3 Targets (added to bench/README.md "Performance budgets")

| Operation | Time (reference machine) | Allocation | Checked by |
|---|---|---|---|
| 30×10 frame, `Fixed` mode, steady state | ≤ 20 µs (unchanged) | exactly 0 B | `RenderAllocationTests` (CI) |
| 30×10 frame, `Auto` with 2 wrapped columns, steady state | ≤ 25 µs | exactly 0 B | `RenderAllocationTests` (CI) |
| Scrolling frame in `Auto`, one new row with 2 wrapped cells | ≤ 80 µs | ≤ 8 KB | benchmark |
| First frame after a jump into an unmeasured region (30 rows × 2 wrapped cells) | ≤ 4 ms (budget cap) | ≤ 256 KB | benchmark |
| Index rebuild after sorting 100K rows with all heights measured | ≤ 5 ms on top of the sort budget | ≤ 64 B once sized | `PerformanceBudgetTests` (allocation) + benchmark |
| Add or remove one item, 100K rows, `Auto` | ≤ 1 ms (existing budget) | ≤ 4 KB (existing) | `PerformanceBudgetTests` |
| Row to Y / Y to row, 1M rows | ≤ 150 ns | 0 B | benchmark |

## 9. Threading

The threading rule is unchanged: `KumikoSource`, the renderer, the hit tester and the input controller are used
on one thread (the UI thread on MAUI and Uno, which is also the render thread for `SKCanvasView` and Uno's
canvas). `KumikoRowLayout` adds no locks, and the same rule applies to it:

- Measurement and `RowHeightResolver` calls happen only inside `KumikoRenderer.Render`. A resolver must be fast
  and pure with respect to the item, and must not modify the source. The layout records the source's view
  version before measuring. If a rebuild happens during measurement (a resolver that mutates the source), the
  frame stops measuring and requests a redraw instead of writing heights into a rebuilt index. Resolver
  exceptions propagate, as they do for `CellStyleResolver` (Q10).
- Queries outside `Render` (hit testing, keyboard, `KumikoScroller`) never measure. A lazy index rebuild after a
  view change is deterministic: it only reads cached heights. So input always sees the geometry of the last
  frame, adjusted only for view changes.
- If the GPU render mode (ROADMAP: `SKGLView` on MAUI) moves `Render` off the UI thread, the layout inherits
  whatever synchronization that work gives `KumikoSource`'s cell text cache. It has the same exposure, and no
  extra design is needed here.

## 10. Migration and API compatibility

- **Defaults keep 1.0 behavior.** `RowHeightMode = Fixed` with no resolver takes the uniform path. Pixels, hit
  testing, extents and allocation are unchanged, and the existing test suite is the regression gate.
- **`KumikoRowStyle.Height`** is removed by #210 (#204). It isn't brought back. Its replacement is
  `RowHeightResolver`: `style.RowHeightResolver = (in KumikoRowHeightContext c) => IsPinned(c.Item) ? 56 : null;`.
  If #210 hasn't merged when stage 3 starts, stage 3 removes it, with the same CHANGELOG **Breaking:** note (only
  allowed pre-1.0).
- **Uniform-only public methods** stay, with their uniform meaning. Each gets a source-aware overload, and the
  uniform overloads get `[Obsolete("Assumes uniform rows; use the overload taking a KumikoSource.")]` in the 1.x
  release that adds variable heights. They're removed in 2.0, per VERSIONING.md's deprecation rule. CORE-30 lists
  these:
  - `KumikoLayoutEngine.GetContentHeight(int, KumikoStyle)`
  - `GetFrozenRowHeight(int, KumikoStyle)`
  - `GetVisibleRowRange`
  - `GetVisibleRowRangeWithOffset`
  - `HitTestRow`
- **`KumikoInputController.GetCellScreenBounds(..., KumikoSource? dataSource = null)`.** With a null source it
  keeps uniform geometry. With a source it's exact.
- **`KumikoScroller` (#56)** keeps its signatures. Only its remarks and its `Auto`-mode timing (refinement on the
  next frame) are new, documented behavior.
- **`ScrollState.ContentHeight`** docs change from "row count × row height + header" to "total row height +
  header + ...".
- **`IDrawingContext`** gets no new members beyond #23's. `KumikoTextLayout.CountLines` is a new public static.
- **`ICellRenderer`** is unchanged. Opting in is `IMeasurableCellRenderer`.
- **Docs to update:**
  - [ARCHITECTURE.md](../ARCHITECTURE.md): the "Virtual Scrolling" paragraph and a `KumikoRowLayout` row in Key
    Classes
  - [RENDERING.md](../RENDERING.md): frame lifecycle step 3a, "Visible Range Computation", "Scroll Clamping", and
    #23's "rows keep their fixed height" sentence
  - the `KumikoColumn.TextWrap` XML docs
  - the README sizing section
  - CHANGELOG entries, written by the implementation PRs

## 11. Test plan

**Unit, layout index** (`KumikoRowLayoutTests`):

- Randomized differential test against a naive `List<float>` model: 10K random operations, each one of measure,
  invalidate, insert, remove, rebuild with a permutation, estimate change or style change. After each, assert
  `GetRowTop`, `GetRowAt`, `GetTotalHeight` and `IsMeasured` for random rows, within 1e-6 relative.
- Edge cases: 0 rows, all rows frozen, one row taller than the viewport, heights at the 1 px floor,
  `MaxRowHeight` capping, a Y at an exact boundary (belongs to the lower row), Y < 0 and Y ≥ total.
- The uniform path returns exactly `row * RowHeight` and allocates nothing.
- Sort, filter, group and expand/collapse keep measured heights. `SetItems` drops them. `Remove` deletes the
  entry.

**Unit, invalidation:**

- `PropertyChanged` on a wrapped column's property re-measures that row only. An unrelated property doesn't
  (unless a resolver is set).
- `SetCellValue`, `Refresh`, a culture change, a column width or font change, and a `CellPadding` or `RowHeight`
  change each re-measure.
- A color change re-measures nothing.

**Rendering** (`RecordingDrawingContext`; mixed heights, frozen rows, grouping with sticky headers, top, bottom
and pinned summaries, left and right frozen columns, drag handle left and right):

- Row backgrounds, cell rectangles, grid lines, the current cell border, group rows, the bottom summary,
  the editor bounds and the grip icons sit at the layout's Y and height.
- A wrapped cell in `Auto` draws all its lines. With `MaxRowHeight` it ends with an ellipsis.

**Anchoring and pending targets:**

- Scroll up into unmeasured rows: the anchor row's screen Y is unchanged after the frame.
- At offset 0 nothing moves.
- `ScrollToRow(far, Start)` lands with the row's top exactly at the start after one frame, and `Scrolled` is
  raised at most twice.
- Ctrl+End shows the last row fully.

**Input:**

- Hit-test round trip for every row of a mixed-height grid, in the frozen area, the scrolling area, sticky
  headers and the bottom summary.
- PageDown/PageUp move by whole pages for tall and short rows.
- Row drag drop index at the boundaries between rows of different heights.
- Ensure-visible for a row taller than the viewport.

**Allocation** (`RenderAllocationTests`, CI-gated): `Fixed` steady state 0 B (unchanged), `Auto` with two
wrapped columns steady state 0 B, `Auto` plus resolver steady state 0 B, and scrolling within already-measured
rows 0 B.

**Performance** (`bench/KumikoUI.Benchmarks`):

- `RowLayoutBenchmarks`: build, query and update at 100K and 1M rows
- `RenderBenchmarks`: an `Auto` steady frame and a one-new-row frame
- `DataBenchmarks`: sort 100K with measured heights
- New entries in the budgets table (§8.3)

**Hosts** (`KumikoUI.Uno.Tests`, MAUI build): property round trip, survival of a `Theme` change, invalidation
methods redraw. Each sample app gets a "Notes" page: 100K rows, two wrapped columns, `Auto`.

## 12. Staged implementation plan

Each stage is one issue, one branch and one PR (the repo's issue workflow). Stages 1–2 change no behavior and can
land before #23 merges. Stage 3 needs #23, and stage 2 needs #56 merged (or it ports `KumikoScroller` itself).

### Stage 1: `KumikoRowLayout` offset index

**Title:** Row layout: Fenwick-tree row offsets with estimates (internal, no behavior change)

Acceptance criteria:

- `internal sealed class KumikoRowLayout` in `Core/Layout` with the §5.1 query surface, the uniform fast path,
  the two-tree Fenwick with an O(1) estimate, double sums, lazy rebuild, and insert/remove by memmove.
- Owned by `KumikoSource` and notified from `RebuildCore`, `TryIncrementalUpdate`, `ToggleGroupExpansion`/expand
  all/collapse all, and `SetItems`. Nothing reads it yet.
- The randomized differential test and the edge-case tests pass. A benchmark exists with row to Y ≤ 100 ns and Y
  to row ≤ 150 ns at 100K rows.
- No public API change. All existing tests pass.

### Stage 2: route all row geometry through the layout

**Title:** Route every row-geometry calculation through KumikoRowLayout (uniform rows, no behavior change)

Acceptance criteria:

- All row-height arithmetic listed in §3 calls the layout: `KumikoRenderer` (`Render`, `DrawRows`,
  `DrawFrozenRows`, `DrawGridLines`, `DrawCurrentCellBorder`, `DrawDragHandleColumn`, `DrawRowDragOverlay`,
  `GetEditorCellBounds`), `KumikoHitTester.HitTestCore`, `KumikoInputController` (paging, drag start,
  `FinishRowReorder`, `GetCellScreenBounds`, `ToggleTappedGroup`), `PinnedGroupHeaders`, and `KumikoScroller`.
  `grep 'RowHeight' src/KumikoUI.Core` finds only the layout, group-row drawing, the wheel unit, and the style.
- Draw loops accumulate heights per row (no tree query per drawn row).
- Source-aware overloads on `KumikoLayoutEngine` (`GetContentHeight`, `GetVisibleRowRange*`, `HitTestRow`). The
  uniform overloads are unchanged (they're obsoleted in stage 3).
- All existing renderer, hit-test, sticky-header, pinned-summary and scroller tests pass unchanged.
  `RenderAllocationTests` stays at 0 B, and the 30×10 frame time is within 5% of main.

### Stage 3: row height modes, resolver and Auto measurement

**Title:** Variable row height: RowHeightMode, RowHeightResolver, Min/MaxRowHeight and Auto measurement of wrapped cells

Acceptance criteria:

- Public API as in §4.1: `KumikoRowHeightMode`, the style properties, `KumikoRowHeightResolver`,
  `KumikoRowHeightContext`, `IMeasurableCellRenderer` (implemented by `TextCellRenderer`),
  `KumikoSource.InvalidateRowHeights/InvalidateRowHeight` and `KumikoScroller.GetRowHeight`. All documented, all
  in PublicAPI.Unshipped.
- The frame protocol of §7.1: measure window, overscan, anchoring, loop limit, per-frame budget, and a redraw
  request when there's work left.
- Per-item cache and the invalidation of §6.4 (content, width signature, style snapshot), with the hooks only at
  the listed sites.
- Pending scroll targets for `ScrollToRow`/`ScrollToItem`/keyboard (§7.8). Paging by height (§7.7).
- Uniform-only `KumikoLayoutEngine` methods marked `[Obsolete]`. `KumikoRowStyle.Height` removed if #210 hasn't.
- The tests of §11 (unit, rendering, anchoring, input, allocation) pass. The `Fixed` default is unchanged.

### Stage 4: measurement efficiency in SkiaSharp

**Title:** Height-only text measurement and shared measure/draw layout cache entries

Acceptance criteria:

- `KumikoTextLayout.CountLines` (public, documented) computes the line count of a wrapped text without
  allocating line strings. `IDrawingContext.MeasureText(..., wrap)`'s Skia override uses it for the height when
  the caller needs only the size.
- `SkiaResourceCache.GetTextLayout` reuses an unconstrained layout for a draw whose `maxLines` is at least its
  line count, so measuring then drawing the same cell lays it out once. Covered by `SkiaTextMeasurementTests`.
- `Auto` measurement of 1,000 rows doesn't evict the visible rows' layouts: a test asserts that a steady frame
  after a measurement burst allocates 0 B.

### Stage 5: hosts and samples

**Title:** MAUI and Uno: RowHeightMode, Min/MaxRowHeight, CellVerticalPadding, RowHeightResolver and InvalidateRowHeights

Acceptance criteria:

- The bindable and dependency properties and methods of §4.2 on both hosts, surviving `Theme`/`GridStyle`
  changes (MAUI-06 pattern), with Uno tests.
- Both sample apps get a wrapped-text page (100K rows, two wrapped columns, `Auto`, frame-stats overlay on).
- README sizing section, ARCHITECTURE.md and RENDERING.md updated (§10).

### Stage 6: interaction polish for tall rows

**Title:** Tall rows: row drag ghost and drop at real row boundaries, ensure-visible and paging for rows taller than the viewport

Acceptance criteria:

- §7.9 ghost height and wrapped ghost text, drop index by `GetRowAt`, and grip icons centered in the row.
- §7.7 rules for rows taller than the viewport (Nearest aligns the top, and a partly visible row with its top
  showing doesn't scroll).
- Auto-fit width of wrapping columns by widest paragraph (§7.10).
- Tests for each.

### Stage 7: performance budgets

**Title:** Benchmarks and budgets for variable row height

Acceptance criteria:

- `RowLayoutBenchmarks`, plus `Auto` frames in `RenderBenchmarks` and a sort with measured heights in
  `DataBenchmarks`.
- The §8.3 rows in bench/README.md's budgets table. Allocation budgets enforced in `PerformanceBudgetTests` and
  `RenderAllocationTests`.
- Numbers published in the PR, measured on the reference machine.

### Stage 8 (optional, after feedback): idle measurement

**Title:** Converge Auto-mode scroll extents by measuring unmeasured rows in idle frames

Acceptance criteria:

- When the grid is idle (no fling, no input for 250 ms), up to 2 ms per frame measures unmeasured rows outward
  from the viewport. This is opt-in (`KumikoStyle.MeasureRowsWhenIdle`, see Q5).
- It never measures during a fling and never allocates in a frame with nothing to measure.
- A test shows the extent becomes exact for 10K rows.

## 13. Row details (#103)

#103 needs a row to be taller than its data row by a details panel. The layout's height for a data row is
computed by one internal function (`ComputeRowHeight(row)`: mode, then resolver, then clamp). #103 adds an
internal `detailsHeight(item)` term to it and draws the panel in the space below the cells. The layout, the
anchoring, the hit testing (a new `HitRegion.RowDetails` below the cells' band) and the scrolling all carry over.
Expanding details is an item invalidation (`InvalidateItem`), not a view rebuild.

## 14. Open questions

1. **Where the settings live.** Should `RowHeightMode`, `RowHeightResolver` and Min/Max be on `KumikoStyle`
   (consistent with `RowHeight` and the other resolvers, but reset by theme replacement) or on the source/grid as
   behavior? This spec picks the style, plus re-application by the hosts.
2. **Default floor in Auto mode.** Should it be `MinRowHeight ?? RowHeight` (rows look like Fixed until content
   wraps), or the single-line text height plus padding (compact rows)? This spec picks `RowHeight`.
3. **The estimate.** Should it be the running mean of measured rows (the proposal), or should an
   `EstimatedRowHeight` style property be exposed for apps that know their data?
4. **Eager resolver evaluation.** When the resolver doesn't measure (resolver-only grids), should every row be
   evaluated on rebuild, giving exact extents at O(n) resolver calls per rebuild? Lazy evaluation is the proposal;
   eager would need a way to know the resolver never calls `MeasureContentHeight()`.
5. **Idle measurement (stage 8).** Should it be on by default for small data sets (for example ≤ 5,000 rows), or
   always opt-in?
6. **Vertical alignment in tall rows.** Single-line and short wrapped cells are centered. Is a per-column
   `VerticalAlignment` (Top/Center/Bottom) needed in the same release?
7. **Group header height.** Should group headers ever get their own height (`GroupHeaderHeight`)? It would make
   sticky slots variable, which is out of scope here.
8. **Float offsets.** `ScrollState.OffsetY`/`ContentHeight` are `float`, so grids of about 1M rows get 1–4 px
   float spacing. Should they move to `double` in 2.0, or should the coordinates be rebased (scroll relative to
   an anchor row)?
9. **Marking anchor corrections.** Anchor corrections raise `ScrollChanged` like any scroll. Should
   `KumikoScrollChangedEventArgs`/`KumikoScrolledEventArgs` carry a reason (`User`, `Programmatic`,
   `LayoutCorrection`) so bindings can tell them apart?
10. **Resolver exceptions.** Should they propagate (consistent with `CellStyleResolver`, the proposal), or be
    caught and fall back to the mode's height?
11. **Public `KumikoRowLayout`.** Should it be public for custom hosts and overlays, or stay internal with
    `KumikoScroller.GetRowY/GetRowHeight` as the public face (the proposal)?
12. **One source, one grid.** Is it acceptable to document that a `KumikoSource` (and its columns) is laid out
    for one grid? It already is, through `KumikoColumn.Width`.
13. **Multi-line editor.** Does a multi-line `DrawnTextBox` for wrapping columns ship with this feature, or as a
    follow-up issue under TEXT-INPUT?
14. **Value-type items.** Items boxed anew on each access (structs in a non-generic `IList`) don't keep their
    measured height, the same limitation the display text cache has today. Is that acceptable?

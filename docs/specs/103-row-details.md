# Spec: row details / master-detail panel (#103)

| | |
|---|---|
| Issue | [#103](https://github.com/TheEightBot/KumikoUI/issues/103) (plan key X1, Epic X, milestone 1.x – Fast follow-ons) |
| Findings | research §1.3 ([07-external-research.md](../release-readiness/07-external-research.md): row details in 5 of 7 competitors) |
| Status | Draft for review |
| Blocked by | [#102](102-variable-row-height.md) variable row height (the layout that lets one row be taller than its neighbors; this spec only adds a term to it, see its section 13) |
| Builds on | [#102](102-variable-row-height.md) `KumikoRowLayout`, [#106](106-column-chooser-and-select-column.md) (a column type that draws a control, the pattern the toggle column copies), #174 sticky group headers (merged) |
| Interacts with | [#79](79-accessibility-tree.md) accessibility tree (a `RowDetails` role under a row, reserved in its section 5.2), [#109](109-pinning-stacked-headers-merging.md) (pinned bands can expand details), [#80](80-right-to-left.md) RTL, [#105](105-export.md) export, [#108](108-cell-selection.md) cell selection |

All code in this document is a **proposal**. It is not compiled, and every name is open to change in review.

## 1. Summary

A user taps a chevron on a row and a panel opens under it, showing more about that row: the columns that do not
fit, a note, a thumbnail, or a second grid with the row's children (master-detail). Five of the seven competitors
in the research have it, and it is the most requested feature after row reorder and validation.

The design in one paragraph. The details panel is **part of its row**, not an extra row: it is extra height under
the row's cells, so `KumikoSource.RowCount`, view row indices, selection, sorting, filtering, grouping and
`GetItem(row)` do not change when a panel opens. Expansion is stored **by item** (reference identity, like the
selection), so it survives sort, filter, grouping and scrolling. The panel's height is one more term in the
row-height function that #102 introduces, so scroll extents, hit testing, ensure-visible and scroll anchoring come
from #102 and are not re-implemented. Content comes from one of two sources: a **drawn renderer**
(`IKumikoRowDetailsRenderer`, Core, painted on the canvas like a cell renderer, with a built-in field list), or a
**hosted view** (`RowDetailsTemplate`, a MAUI or Uno `DataTemplate` realized as a native child of the grid, the
only way to get a nested grid or arbitrary controls). The drawn path ships first and covers most apps. The hosted
path is a second stage with documented limits.

## 2. Goals and non-goals

### Goals (1.x)

1. Expand and collapse a details panel per row, by tap on a toggle, by tap on the row, by keyboard, and from code
   (`SetExpanded`, `ExpandAll`, `CollapseAll`). Expansion is by item and survives sort, filter, grouping and edits.
2. A **drawn** panel in Core with a fixed or auto (measured) height, drawn by an app-supplied renderer, plus one
   built-in renderer (a label/value field list) that needs no code.
3. A **hosted** panel (native view from a `DataTemplate`) on both MAUI and Uno, for content that cannot be drawn,
   notably a nested `KumikoDataGrid`.
4. Everything that depends on row geometry stays correct with a tall row: drawing, hit testing, keyboard
   navigation, ensure-visible, programmatic scrolling (#56), sticky group headers, scroll anchoring, the editor
   overlay, row drag.
5. Zero cost when unused: a grid with no details renderer or template, or with no row expanded, keeps the uniform
   (#102 `Fixed`) fast path and allocates nothing per frame (`RenderAllocationTests`, `PerformanceBudgetTests`).
6. Accessible: expandable state, expand and collapse actions, announcements, and the panel's text available to a
   screen reader (section 8).
7. Same public names and defaults on MAUI and Uno.

### Non-goals (1.x, possible later)

- **Animated expand and collapse.** The first version is instant. The design leaves room (section 6.4).
- **Details for group header, group summary or table summary rows.** Only data rows have details.
- **Details in the legacy frozen rows** (`KumikoSource.FrozenRowCount`). The frozen region has a fixed height by
  contract; the toggle is hidden for those rows. Item-based pinned bands from #109 can expand (the band grows).
- **Horizontal scrolling of the panel.** The panel is as wide as the grid viewport and does not move when the
  columns scroll (what the competitors do). A nested grid scrolls itself.
- **A multi-level tree** (rows that expand into rows of the same grid). This is a tree grid, a different feature.
  A nested `KumikoDataGrid` inside a hosted panel covers hierarchy one level at a time.
- **Editing in a drawn panel.** A drawn panel can have buttons and links (interactive renderer, section 5.3). Text
  entry in a panel is what the hosted path is for. A drawn form helper (`DrawnComponent`s inside a panel) is a
  possible later stage (section 12, stage 6).
- **Persisting expansion in `KumikoLayoutState`.** The layout state saves columns, sort, filter and grouping, which
  are keyed by column; expanded items are application data (Q8).

## 3. Current state (main at 0ac86c2)

- Every row is `KumikoStyle.RowHeight` tall (default 36). The renderer (`KumikoRenderer.cs`), the hit tester
  (`KumikoHitTester.cs`), `KumikoScroller` and the input controller multiply that constant directly
  (`dataAreaTop + frozenRowHeight + (row - frozenRowCount) * style.RowHeight - scroll.OffsetY`, repeated in the
  draw passes, the editor bounds and the drag handle code). **There is no row layout service yet; #102 adds one
  (`KumikoRowLayout`) and routes all of that through it.** This spec cannot start before #102 stages 1 to 3.
- The flat view of `KumikoSource` is `_viewIndices` (filtered, sorted source indices) or, when grouping, a list of
  `FlatViewRow` (kinds `Data`, `GroupHeader`, `GroupSummary`). A data row maps to an item by index. Group
  expansion is stored by group key, and `ToggleGroupExpansion`, `ExpandToItem` and `IndexOfItem` exist.
- `KumikoSelectionModel` keys rows by **item reference**, drops items that leave the view, and navigates by view
  row. The keyboard for group header rows (Right expands, Left collapses, Space or Enter toggles) lives in
  `KumikoInputController.HandleKeyDown`.
- The hit tester returns a `HitRegion` (`Cell`, `GroupChevron`, `GroupHeaderRow`, `RowDragHandle`, ...). There is no
  region for anything below a row's cells.
- The renderer draws data rows in three pane passes (scrollable columns, left frozen, right frozen), each clipped
  to its pane, then frozen-row, summary and group header passes. Cell content goes through `ICellRenderer`
  (`TextCellRenderer`, `BooleanCellRenderer`, ...), and interactive cells through `IInteractiveCellRenderer`
  (`OnCellTap`, `OnCellActivate`, `HasControlAt`, `RedrawRequested`).
- Style hooks per item: `KumikoStyle.RowStyleResolver` and `CellStyleResolver`.
- `KumikoColumnType` has `Text`, `Numeric`, `Boolean`, `Date`, `Template`, `Image`, `ComboBox`, `Picker`,
  `ProgressBar` (and `Selection` once #106 lands). A `Template` column is drawn by a cell renderer.
- Hosts: both are one control (`KumikoDataGrid`: a `ContentView` on MAUI, a `UserControl` on Uno) whose content
  is a root `Grid` holding the canvas surface and, on demand, a hidden text-input proxy child (MAUI
  `KumikoTextInputView`, Uno `TextBox`). Native children of the root grid are therefore already an accepted
  pattern. Neither host has a `DataTemplate` property for cells or rows.
- Accessibility today (#77, #78): one node with a hint and announcements from `KumikoAnnouncements`. The per-cell
  tree is #79 (spec). It reserves a `RowDetails` role under a row.
- `RowHeight` and the other style properties are on `KumikoStyle`; the hosts must re-apply host-level settings
  after a theme change (the MAUI-06 problem #102 notes).

## 4. Key decisions

### D1. A panel is part of its row, not a flat-view row

The alternative, a new `FlatRowKind.RowDetails` entry after the data row, was rejected.

| | Part of the row (chosen) | Separate flat-view row |
|---|---|---|
| `RowCount`, view indices | Unchanged by expansion | Shifts by one per open panel; every `row` an app holds goes stale |
| Selection, current cell, `GetItem(row)` | Unchanged | Needs a "details row is not selectable/navigable" rule everywhere (arrows, Home/End, PageUp/Down, select all, export, a11y counts) |
| Sort, filter, group | Unaffected | The panel row must travel with its data row in `RebuildCore` and in the incremental add/remove path |
| Geometry | One more term in #102's row height | Needs a row kind with its own height, which #102 does not have for non-data rows |
| Cost | Extra draw pass and hit region | New `FlatRowKind` threaded through ~40 call sites |

The price is that the row's height depends on state outside the view (`IsExpanded(item)`), so the #102 layout must
be told when it changes. That is `InvalidateItem`-class work, not a rebuild (#102 section 13 already plans it).

### D2. Expansion state is by item reference, on `KumikoSource`

Same identity rule as `KumikoSelectionModel` (`ReferenceEqualityComparer`), so a value-type item boxed anew each
time cannot be expanded (same limitation as the cell text cache and #102 Q14). State lives on the **source**
because the source owns the view and the layout, and because both hosts forward to it. Items removed from the
source are dropped from the set; items hidden by a filter keep their state (Q4). A `KumikoSource.SetItems` with
a new collection clears it.

### D3. Two content paths behind one expand model

The expand model, the layout term, the hit region, the keyboard and the semantics are common. Only "who paints
or hosts the panel" differs: an `IKumikoRowDetailsRenderer` (drawn) or a host `DataTemplate` (hosted). Both are
optional and mutually exclusive per grid; the template wins if both are set. This keeps Core free of UI
framework references (CONTRIBUTING) and lets hosted support be delivered separately.

### D4. The panel spans the viewport and does not scroll horizontally

Its left edge is the viewport's left edge (under the frozen columns too), its width is the viewport width minus the
vertical scrollbar. It is drawn in its own pass after the pane passes, clipped to the scrollable data area
(below the header and the top summaries, above the bottom summaries). Sticky group headers draw over it as over
any row.

### D5. The toggle is a column, the trigger is a setting

The chevron lives in a column (`KumikoRowDetailsColumn`, column type `RowDetailsToggle`) so that it follows column
freezing, reordering, RTL and the saved layout like the select column of #106, rather than a gutter outside the
column model like the drag handle. Triggers that need no column (row tap, selection, programmatic) work without it.
Alternative: a grid-level `ShowRowDetailsToggle` that injects the column (Q2).

## 5. Proposed API: Core

All new public members get XML docs (doc warnings are errors) and PublicAPI entries. Names follow CONTRIBUTING
"Public type names": `Kumiko` prefix, nothing named `Grid*`, nothing that collides with MAUI or WinUI types
(`RowDetails*` alone is not a WinUI type, but `DataGridRowDetails*` is, hence the prefix on every type).

### 5.1 Expand model (`KumikoUI.Core.Models`)

```csharp
// PROPOSAL, not compiled.
namespace KumikoUI.Core.Models;

/// <summary>What makes a row's details open or close from user input.</summary>
[Flags]
public enum KumikoRowDetailsTriggers
{
    /// <summary>Only code opens details (<see cref="KumikoRowDetails.SetExpanded"/>).</summary>
    None = 0,
    /// <summary>The toggle column's chevron, and Enter or Space on it.</summary>
    Toggle = 1,
    /// <summary>A tap on the row's cells (a second tap on a selected row when selection is on).</summary>
    RowTap = 2,
    /// <summary>The selected rows show details; deselecting hides them.</summary>
    Selection = 4,
}

/// <summary>How tall a details panel is.</summary>
public enum KumikoRowDetailsHeightMode
{
    /// <summary><see cref="KumikoRowDetails.Height"/> for every panel (the default; cheapest).</summary>
    Fixed,
    /// <summary>The renderer or the hosted view measures the panel at the viewport width.</summary>
    Auto,
}

/// <summary>Event data for <see cref="KumikoRowDetails.Expanding"/>.</summary>
public sealed class KumikoRowDetailsExpandingEventArgs : CancelEventArgs
{
    public object Item { get; }          // the row's item
    public int Row { get; }              // its row in the current view
    public bool IsExpanding { get; }     // true: opening, false: closing
}

/// <summary>Event data for <see cref="KumikoRowDetails.ExpandedChanged"/>.</summary>
public sealed class KumikoRowDetailsChangedEventArgs : EventArgs
{
    public object Item { get; }
    public bool IsExpanded { get; }
}

/// <summary>Expansion state and options for row details. Reached through <see cref="KumikoSource.RowDetails"/>.</summary>
public sealed class KumikoRowDetails
{
    /// <summary>True when a renderer or template is set: the toggle column and the triggers are live.</summary>
    public bool IsEnabled { get; }

    public KumikoRowDetailsTriggers Triggers { get; set; } = KumikoRowDetailsTriggers.Toggle;
    public KumikoRowDetailsHeightMode HeightMode { get; set; } = KumikoRowDetailsHeightMode.Fixed;

    /// <summary>The Fixed height, and the estimate for an Auto panel that is not measured yet. Default 120.</summary>
    public float Height { get; set; } = 120f;

    /// <summary>Auto mode: the panel is never shorter or taller than these. Null: no limit.</summary>
    public float? MinHeight { get; set; }
    public float? MaxHeight { get; set; }

    /// <summary>True (default): any number of panels can be open. False: opening one closes the others.</summary>
    public bool AllowMultipleExpanded { get; set; } = true;

    /// <summary>True (default): opening a panel scrolls so the whole panel is visible, when it fits.</summary>
    public bool ScrollIntoViewOnExpand { get; set; } = true;

    /// <summary>Which items can expand. Null (default): every data row outside the frozen rows.</summary>
    public Func<object, bool>? HasDetails { get; set; }

    public IReadOnlyCollection<object> ExpandedItems { get; }
    public int ExpandedCount { get; }
    public bool IsExpanded(object item);
    public bool CanExpand(object item);

    /// <summary>Opens or closes the details of an item. Returns false when the change was canceled or not allowed.</summary>
    public bool SetExpanded(object item, bool expanded);
    public bool Toggle(int visibleRow);
    public void ExpandAll();            // every item that CanExpand, honoring AllowMultipleExpanded (false: no-op, documented)
    public void CollapseAll();

    /// <summary>Raised before a change, on the UI thread; set Cancel to veto.</summary>
    public event EventHandler<KumikoRowDetailsExpandingEventArgs>? Expanding;
    public event EventHandler<KumikoRowDetailsChangedEventArgs>? ExpandedChanged;

    /// <summary>Re-measures the panel of one item (or all) on the next frame, for content that changed size.</summary>
    public void InvalidateHeight(object item);
    public void InvalidateHeights();
}

public class KumikoSource
{
    /// <summary>Row details state and options.</summary>
    public KumikoRowDetails RowDetails { get; }
}
```

`SetExpanded` runs `Expanding` (cancelable), updates the set, then raises `ExpandedChanged` and asks the source
for an item-level view change. It is **not** a `KumikoViewChangeKind.Rebuild`: no row moves. It needs a way to
tell the layout "row height of this item changed". #102 gives it that as `InvalidateRowHeight(object item)`
(its section 4.1) and the layout's anchoring turns it into a corrected scroll offset. If #102 chooses a new
`KumikoViewChangeKind` for it, this spec uses that one (Q9).

### 5.2 Renderer (`KumikoUI.Core.Rendering`)

```csharp
// PROPOSAL, not compiled.
namespace KumikoUI.Core.Rendering;

/// <summary>Draws the details panel of a row on the grid's canvas.</summary>
public interface IKumikoRowDetailsRenderer
{
    /// <summary>
    /// The panel height for <paramref name="context"/>.Bounds.Width, used when the height mode is Auto. Called
    /// only when a panel is first drawn or invalidated, never on every frame. Must not allocate per call beyond
    /// what the drawing context's text measurement does.
    /// </summary>
    float Measure(IDrawingContext ctx, in KumikoRowDetailsContext context);

    /// <summary>Draws the panel inside <paramref name="context"/>.Bounds. The clip is already set.</summary>
    void Draw(IDrawingContext ctx, in KumikoRowDetailsContext context);

    /// <summary>
    /// Plain text for a screen reader and for copy: what the panel says, or null for a panel with no text.
    /// Called on demand by the accessibility layer, never in the render loop.
    /// </summary>
    string? GetAccessibleText(in KumikoRowDetailsContext context) => null;
}

/// <summary>What a renderer is told about the panel. A struct, valid only during the call.</summary>
public readonly struct KumikoRowDetailsContext
{
    public object Item { get; }
    public int Row { get; }                 // the row's view row
    public KumikoRect Bounds { get; }       // the panel's rectangle in canvas coordinates
    public bool IsSelected { get; }
    public KumikoStyle Style { get; }
    public KumikoSource Source { get; }     // for columns, GetCellValue, formatting
}
```

The renderer is stateless per call so a frame allocates nothing: the context is a struct passed by `in`, and the
renderer instance is shared (it can keep reusable paints, as the existing cell renderers do through `PaintCache`).

**Built-in `KumikoFieldListRowDetails : IKumikoRowDetailsRenderer`** (Core): draws label and value pairs for a
set of columns, in a configurable number of columns, using the grid's fonts and `KumikoStrings`-independent
formatting from `KumikoSource.GetCellDisplayText`. It is the zero-code answer ("show the columns I hid") and it
doubles as the reference implementation and the benchmark.

```csharp
// PROPOSAL, not compiled.
public sealed class KumikoFieldListRowDetails : IKumikoRowDetailsRenderer
{
    /// <summary>Columns to list. Null (default): the columns whose IsVisible is false.</summary>
    public IReadOnlyList<KumikoColumn>? Columns { get; set; }
    public int FieldColumns { get; set; } = 1;          // label/value pairs per line
    public float LabelWidth { get; set; } = 140f;
    public float Padding { get; set; } = 12f;
    public float LineSpacing { get; set; } = 4f;
    // + Measure, Draw, GetAccessibleText
}
```

### 5.3 Interaction from a drawn panel

```csharp
// PROPOSAL, not compiled. Mirrors IInteractiveCellRenderer.
public interface IInteractiveRowDetailsRenderer : IKumikoRowDetailsRenderer
{
    /// <summary>A tap at (x, y) in canvas coordinates. Returns true when handled (the grid does not select the row).</summary>
    bool OnTap(in KumikoRowDetailsContext context, float x, float y);

    /// <summary>True when there is a control at (x, y): the host shows a pointer cursor and does not start a drag.</summary>
    bool HasControlAt(in KumikoRowDetailsContext context, float x, float y) => false;

    /// <summary>Raised when the panel needs a redraw (a hover or pressed state).</summary>
    event Action? RedrawRequested;
}
```

Taps and hover reach it through the new hit region below.

### 5.4 Toggle column and hit region

```csharp
// PROPOSAL, not compiled.
public enum KumikoColumnType { /* existing values, then appended: */ RowDetailsToggle }

/// <summary>A narrow column whose cells show an expand/collapse chevron for the row's details.</summary>
public class KumikoRowDetailsColumn : KumikoColumn
{
    public KumikoRowDetailsColumn();   // ColumnType = RowDetailsToggle, Width 40, not sortable, filterable, editable
                                       // or groupable; header empty; resizable false; Key "kumiko:details-toggle"
}

public enum HitRegion { /* existing values, then appended: */ RowDetailsToggle, RowDetails }
```

`KumikoColumnType.RowDetailsToggle` is **appended** (existing numeric values and the editor factory switches are
unaffected; the repo's own `switch`es over `KumikoColumnType` get a case, as #106 does for `Selection`). A
`RowDetailsToggleCellRenderer` (Core) draws a chevron that points right when collapsed and down when expanded, in
the cell text color, centered; it draws nothing for a row where `CanExpand` is false. Its glyph comes from
`IKumikoIconProvider` like the group chevron.

`KumikoHitTestResult` already carries the row and column; for `RowDetails` it also exposes the panel rectangle
(`KumikoHitTestResult.DetailsBounds`, appended property) so the controller can build a `KumikoRowDetailsContext`.

### 5.5 Style

```csharp
// PROPOSAL, not compiled.
public class KumikoStyle
{
    public KumikoColor RowDetailsBackgroundColor { get; set; }   // default: slightly offset from the row background
    public KumikoColor RowDetailsBorderColor { get; set; }       // a 1 px line under the panel
    public KumikoColor RowDetailsIndicatorColor { get; set; }    // a thin bar at the left edge linking panel to row; default: accent
}
```

`KumikoTheme` sets all three for the built-in light and dark themes. A theme or `GridStyle` change replaces the
style instance; the host re-applies host-level options as #102 does (these three are style values, so they travel
with the theme).

## 6. How it fits the pipeline

### 6.1 Row layout (depends on #102)

#102 computes each data row's height in one internal function (`ComputeRowHeight`: mode, resolver, clamp) and
answers row-to-Y and Y-to-row through `KumikoRowLayout` (uniform arithmetic, else a Fenwick tree). This spec adds:

```
rowHeight(item) = baseRowHeight(item) + (IsExpanded(item) ? detailsHeight(item) : 0)
detailsHeight   = Fixed ? RowDetails.Height : clamp(measured, MinHeight, MaxHeight) ?? RowDetails.Height (not yet measured)
```

- **Uniform fast path preserved.** While `RowDetails.ExpandedCount == 0` and the base mode is `Fixed`, the layout
  stays uniform: no per-row arrays, no Fenwick tree, 0 bytes per frame. Expanding the first row switches the layout
  to the non-uniform representation (O(n) once); collapsing the last one switches back. A grid that only ever
  expands a few rows pays the #102 non-uniform cost (O(log n) queries) and nothing before that. The first
  expansion on a 100K row grid must stay within #102's rebuild budget (section 8.3 of that spec); this is a
  benchmark item (section 11).
- **Auto measurement runs in the frame**, as #102 measures rows, because only `Render` has an `IDrawingContext`.
  A panel is measured once per `(item, width, invalidation stamp)` and cached next to #102's measured-height
  cache. Width change (rotation, window resize) re-measures panels as it re-measures wrapped rows. Hosted panels
  are measured on the UI thread by the host (section 7.2), not in the frame, and the result is stored in the same
  cache.
- **Scroll anchoring** (#102): expanding or collapsing a row above the first visible row corrects `OffsetY` by the
  height delta, so the visible rows do not move. The row being expanded keeps its top edge fixed (the panel
  grows downward). With `ScrollIntoViewOnExpand`, if the panel's bottom is below the viewport the offset moves just
  far enough to show it, but never so far that the row's own top scrolls off.

### 6.2 Rendering

`KumikoRenderer.DrawRows` today draws a row at `rowY` with height `style.RowHeight` in three pane passes. After #102 the
height comes from the layout, and the cells use `layout.GetBaseHeight(row)` (the cell band), not the full row
height, so the cell band keeps `RowHeight` (or its Auto height) while the panel sits under it.

New pass, **`DrawRowDetails`**, after the pane passes and before the frozen-row dividers, sticky group headers and
summaries (so those paint over it):

1. Set the clip to the scrollable data area (the same `dataClip` the cell passes use), full viewport width.
2. For each visible row (the layout answers the first and last row by Y), if `IsExpanded(item)`, compute the panel
   rectangle: `x = 0`, `y = rowY + baseHeight`, `width = viewport width`, `height = detailsHeight`. Skip it when it
   does not intersect the clip.
3. Fill the background, draw the bottom border and the left indicator bar, then call
   `renderer.Draw(ctx, in context)`. A hosted panel draws only the background and border; the native view covers
   the rest (section 7.2).
4. Selected and hovered rows tint the panel's background the same way as the row (one `PaintCache` paint each).

**Zero allocation**: the visible row range is a loop over ints; `IsExpanded` is a hash lookup on a `HashSet<object>`
with `ReferenceEqualityComparer` (no allocation) and is skipped entirely when `ExpandedCount == 0`;
`KumikoRowDetailsContext` is a struct passed by `in`; no closures, no LINQ, no enumerators over the set in the
frame. `RenderAllocationTests` gets a case with 3 open panels (fixed and auto height) that must still read 0 bytes
per frame after warm-up.

Interaction with other passes: group headers and group summary rows have no details; their height is unchanged
(#102 keeps them at `RowHeight`). The row-separator line is drawn under the panel, not between cells and panel.
Banded row colors alternate by data row, not by panel. The frozen-row block ignores details (section 6.6).

### 6.3 Hit testing and input

`KumikoHitTester.HitTestCore` already walks Y down through header, summaries, frozen rows and scrolling rows. With
the layout it learns, per data row, a second band: the cell band `[rowY, rowY + base)` returns `Cell` (or
`RowDetailsToggle` when the column is the toggle column) and the panel band `[rowY + base, rowY + total)` returns
**`HitRegion.RowDetails`**, with `RowIndex` set. A tap in the panel:

- on a drawn panel, goes to `IInteractiveRowDetailsRenderer.OnTap` if implemented; unhandled taps do nothing (they
  do not select the row and do not start a drag or an edit);
- on a hosted panel, is not seen by Core at all, because the native view covers the area and takes the pointer
  (section 7.2); Core only sees taps on the margin the view does not cover.

Mouse wheel and touch scroll over a panel scroll the grid (a drawn panel does not scroll). A hosted nested grid
takes the wheel when it can scroll in that direction and passes it on at its edges (Q6).

Row drag (`ShowRowDragHandle`) drags the row **and its panel** as one: the drag ghost uses the cell band only, the
panel collapses visually during the drag (it is hidden, not collapsed in the model) and returns at the drop. This
needs the `RowDragGeometry` helper to use the layout's total height for drop targets (a #102 stage 6 item that
already covers tall rows).

### 6.4 Expand and collapse flow, and animation room

`SetExpanded` (user or code) does: commit a running edit (same helper as group expansion, `CommitEditBeforeViewChange`)
and cancel if the commit fails, raise `Expanding`, update the set, invalidate the item's height in the layout,
request a frame, raise `ExpandedChanged`, announce. All on the UI thread. It never runs in the render loop.

Animation is not in v1, but the design does not block it: the layout's height for an item is a function, and
animation would add a transient per-item factor `0..1` multiplying `detailsHeight` and driven by the frame
scheduler (`IsAnimating` style, like `DrawnComponent.IsAnimating`). That is a stage 7 item.

### 6.5 Keyboard

| Key | On a data row's current cell | Notes |
|---|---|---|
| **Alt+Down** | expands the current row's details | Proposed; ARIA convention for "expand". Q3 |
| **Alt+Up** | collapses them | |
| **Enter** or **Space** | toggles when the current cell is in the toggle column | otherwise unchanged (Enter edits, Space as today) |
| **Ctrl+Alt+Down / Ctrl+Alt+Up** | expand all / collapse all | optional, Q3 |
| arrows, Home, End, PageUp/Down | unchanged | the panel is never a navigation stop; a PageDown moves by the viewport height in pixels and may land the current row below an open panel |

`Right` and `Left` keep moving between cells. They do not expand (group header rows keep their own Right/Left
rule). Keyboard focus never enters a drawn panel. For hosted panels, Tab from the last cell of the row enters the
panel's first focusable element and Shift+Tab from outside returns (section 7.2); Escape returns focus to the
row's current cell.

### 6.6 Other row features

| Feature | Behavior |
|---|---|
| Virtualization | Only visible rows are drawn and measured. Hosted views exist only for visible expanded rows (a pool, section 7.2). 100K rows with 10 open panels is the benchmark. |
| Frozen rows (`FrozenRowCount`) | Rows inside the frozen block cannot expand: `CanExpand` is false for them (no chevron). Expanding is allowed for the item after it leaves the block (it is by item). #109 pinned-band rows can expand and the band grows (its section 5 table). |
| Frozen columns | The cell band is split across panes as today. The panel ignores panes and spans the viewport (D4). The toggle column can be a frozen column (recommended: first and frozen). |
| Grouping | Details are per data row inside a group. Collapsing a group hides its rows and their panels; the open state is kept (by item). `ExpandToItem` also works. Sticky group headers draw over a panel scrolled underneath. Group summaries are unaffected. |
| Sorting and filtering | By item, so a sort moves a row and its open panel together. A filtered-out item keeps its open state; it reappears open (Q4). |
| Selection | A row is selected by its cells, not by its panel. Selecting does not open details unless `Triggers` has `Selection`. `Selection` trigger drives the set from the selection model's `SelectionChanged` (it is then a function of selection, `SetExpanded` calls are overridden on the next change). |
| Current cell and editing | The current cell is always a data cell. Opening or closing details commits a running edit first. The editor overlay is positioned from the layout, so it follows the row if the layout shifts. |
| Summaries | Table summaries are pinned above and below the data area and unaffected. Their position math moves with #102's extents. |
| Programmatic scrolling (#56) | `ScrollToRow` and `ScrollToItem` position the row's **cell band** top; `KumikoScroller.GetRowHeight` (#102) includes the panel. A new `KumikoScroller.GetRowDetailsHeight(source, style, row)` is not needed (height minus base). Programmatic expansion of an unrealized row only changes the layout. |
| Export (#105) | Details are not exported by default; `IKumikoRowDetailsRenderer.GetAccessibleText` is available to an exporter that wants it (Q10). |
| RTL (#80) | The indicator bar and the field list mirror with the flow direction. The panel still spans the viewport. |
| Column auto-fit | Panel text is ignored. |

## 7. Host work

### 7.1 Public host surface (MAUI and Uno, same names)

| Member | Type | Default |
|---|---|---|
| `RowDetailsRenderer` | `IKumikoRowDetailsRenderer?` | null |
| `RowDetailsTemplate` | `DataTemplate?` | null |
| `RowDetailsTriggers` | `KumikoRowDetailsTriggers` | `Toggle` |
| `RowDetailsHeightMode` | `KumikoRowDetailsHeightMode` | `Fixed` |
| `RowDetailsHeight` | `double` | 120 |
| `RowDetailsMinHeight`, `RowDetailsMaxHeight` | `double` | `NaN` (null), the #102 host convention |
| `AllowMultipleRowDetails` | `bool` | true |
| `HasRowDetailsPredicate` | `Func<object, bool>?` | null |
| `ExpandRowDetails(object)`, `CollapseRowDetails(object)`, `ToggleRowDetails(object)`, `ExpandAllRowDetails()`, `CollapseAllRowDetails()`, `IsRowDetailsExpanded(object)` | methods | |
| `RowDetailsExpanding`, `RowDetailsExpandedChanged` | events (args from 5.1) | |
| `RowDetailsExpandedCommand` (MAUI) | `ICommand` | |

Each forwards to `KumikoSource.RowDetails` and redraws, in `KumikoDataGrid.Options.cs` (MAUI) and
`KumikoDataGrid.Properties.cs` plus `Options.cs` (Uno). All of them survive a `Theme`/`GridStyle` change and a
`ItemsSource` change (the host re-applies them to the new source, as for sort and filter options).

`KumikoRowDetailsColumn` is a Core type, so it is usable from XAML on both hosts like other columns.

### 7.2 Hosted panel (`RowDetailsTemplate`)

Needed for a nested grid or real controls. The model:

- **A realization pool.** The host keeps a small pool of template instances (`DataTemplate.CreateContent()`),
  one per visible expanded row, reused as rows scroll in and out. `BindingContext` (MAUI) or `DataContext` (Uno)
  is the row's item. A view is created the first time its row becomes visible expanded, recycled when it leaves.
  Pool size is bounded by the number of rows that fit plus a few.
- **An overlay layer** in the root `Grid`, above the canvas and below the text-input proxy: an `AbsoluteLayout`
  (MAUI) or `Canvas` (Uno), clipped to the data area. Views are positioned with absolute bounds
  (`AbsoluteLayout.SetLayoutBounds` / `Canvas.SetLeft/Top` plus `Width`/`Height`).
- **Positioning.** After each render the host receives the list of visible panels (item, rectangle) from Core and
  moves the views. That list is a reused `List<KumikoRowDetailsSlot>`, filled by the renderer in the frame without
  allocation (a struct of item, row, rectangle). A view update with an unchanged rectangle is skipped.
- **Measurement.** `HeightMode = Auto`: the host calls the view's native measure at the viewport width
  (`View.Measure` on MAUI, `UIElement.Measure` on Uno) synchronously on the UI thread when the panel is opened,
  resized or invalidated, and stores the result in the layout cache. The frame never measures views. A view whose
  content changes size later must call `InvalidateHeight(item)` (a size-changed hook on the view does it
  automatically in the host).
- **Clip and z-order.** The overlay is clipped to the data area, so panels slide under the column header and the
  top summaries. Pinned group headers and bottom summaries are canvas-drawn, so the host raises the overlay's clip
  to their bottom/top edges each frame (it already knows `dataClip`).
- **Input and focus.** The native view takes pointer, focus and its own keyboard. The grid's text-input proxy and key
  routing must not steal keys while a panel view has focus: the grid's key handler runs only when the proxy or the
  canvas has focus (as when a popup is open). Tab from the grid's last cell enters the panel, Escape leaves it.
- **Scroll lag.** The canvas redraws synchronously in the frame while the native overlay moves on the platform's
  next layout pass, so a panel can lag one frame during a fast fling on some platforms (MAUI iOS and Android
  especially). This is the main risk of the hosted path (section 13, R1). Mitigations: move views in the same
  UI-thread callback as the invalidate, and prefer `TranslationY` of one container over repositioning each view.
  A spike (stage 5) decides whether the hosted path ships on every head or is marked experimental on some.
- **Accessibility.** Native views are real accessibility nodes, so a hosted panel is readable by default. They are
  placed in the accessibility order after the grid's node (section 8).
- **Platforms.** MAUI: all heads. Uno: the Skia desktop heads, WASM and native heads that host Skia; a
  `ContentControl` per slot. Windows-only code cannot be built on the Mac CI; it is kept compiling by review.

The hosted path is where `RowDetailsTemplate` shines for master-detail: a `KumikoDataGrid` inside a panel, bound to
`Orders` of the `Customer`, sized with `RowDetailsHeight` (fixed, because a nested virtualized grid has no intrinsic
height).

### 7.3 Drawn-panel host work

None beyond forwarding properties: the renderer runs in Core on the shared canvas.

## 8. Accessibility

Until #79 lands: opening or closing a panel announces through `KumikoAnnouncements` (new `KumikoStrings`
members `AnnounceRowDetailsExpanded` "Details expanded" and `AnnounceRowDetailsCollapsed` "Details collapsed",
plus the current cell announcement stays the same); the grid hint mentions the shortcut when details are enabled
(`AccessibleGridHint` gets a variant). Nothing is exposed per row.

With #79 (the semantics provider, its section 5.2 reserves a `RowDetails` role):

- The **row node** gets `Expandable` and `Expanded` states and `Expand`/`Collapse` actions, driven from
  `IsExpanded`, and `Toggle` on the toggle cell. Mapped to UIA `ExpandCollapse` pattern (Windows), `UIAccessibilityTraits`
  plus a custom action (iOS/Mac Catalyst), `AccessibilityNodeInfo` expand/collapse actions (Android) and
  `aria-expanded` (WASM).
- A **`RowDetails` node** is a child of the row, realized only while the panel is open and visible (or holds
  focus). Its name is `IKumikoRowDetailsRenderer.GetAccessibleText`, its bounds the panel rectangle. For hosted
  panels, the native view subtree replaces it.
- The toggle cell's name comes from `KumikoStrings` ("Expand details" / "Collapse details", localizable).
- Keyboard-only users have Alt+Down/Up. Screen reader users have the actions above. There is no
  panel-only navigation stop in the grid's arrow navigation, by design (D1).
- All text comes from `KumikoStrings`. None of it is built in the render loop.

## 9. XAML usage examples (proposal)

### 9.1 MAUI, drawn field list (no code)

```xml
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:dg="clr-namespace:KumikoUI.Maui;assembly=KumikoUI.Maui"
             xmlns:core="clr-namespace:KumikoUI.Core.Models;assembly=KumikoUI.Core"
             xmlns:rend="clr-namespace:KumikoUI.Core.Rendering;assembly=KumikoUI.Core">
    <dg:KumikoDataGrid ItemsSource="{Binding Customers}"
                       RowDetailsHeightMode="Auto"
                       AllowMultipleRowDetails="False">
        <dg:KumikoDataGrid.RowDetailsRenderer>
            <rend:KumikoFieldListRowDetails FieldColumns="2" />
        </dg:KumikoDataGrid.RowDetailsRenderer>
        <dg:KumikoDataGrid.Columns>
            <core:KumikoRowDetailsColumn />
            <core:KumikoColumn Header="Name" PropertyName="Name" Width="200" />
            <core:KumikoColumn Header="City" PropertyName="City" Width="140" />
            <core:KumikoColumn Header="Phone" PropertyName="Phone" IsVisible="False" />
            <core:KumikoColumn Header="Notes" PropertyName="Notes" IsVisible="False" />
        </dg:KumikoDataGrid.Columns>
    </dg:KumikoDataGrid>
</ContentPage>
```

### 9.2 MAUI, hosted master-detail (a nested grid)

```xml
<dg:KumikoDataGrid ItemsSource="{Binding Customers}"
                   RowDetailsHeightMode="Fixed" RowDetailsHeight="220"
                   RowDetailsTriggers="Toggle,RowTap">
    <dg:KumikoDataGrid.RowDetailsTemplate>
        <DataTemplate>
            <!-- BindingContext is the Customer -->
            <dg:KumikoDataGrid ItemsSource="{Binding Orders}">
                <dg:KumikoDataGrid.Columns>
                    <core:KumikoColumn Header="Order" PropertyName="Number" />
                    <core:KumikoColumn Header="Total" PropertyName="Total" ColumnType="Numeric" Format="C2" />
                </dg:KumikoDataGrid.Columns>
            </dg:KumikoDataGrid>
        </DataTemplate>
    </dg:KumikoDataGrid.RowDetailsTemplate>
    <dg:KumikoDataGrid.Columns>
        <core:KumikoRowDetailsColumn />
        <core:KumikoColumn Header="Customer" PropertyName="Name" Width="240" />
    </dg:KumikoDataGrid.Columns>
</dg:KumikoDataGrid>
```

### 9.3 Uno

```xml
<Page xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns:kumiko="using:KumikoUI.Uno"
      xmlns:core="using:KumikoUI.Core.Models">
    <kumiko:KumikoDataGrid ItemsSource="{Binding Customers}"
                           RowDetailsHeightMode="Fixed" RowDetailsHeight="220">
        <kumiko:KumikoDataGrid.RowDetailsTemplate>
            <DataTemplate>
                <!-- DataContext is the Customer -->
                <kumiko:KumikoDataGrid ItemsSource="{Binding Orders}">
                    <kumiko:KumikoDataGrid.Columns>
                        <core:KumikoColumn Header="Order" PropertyName="Number" />
                        <core:KumikoColumn Header="Total" PropertyName="Total" ColumnType="Numeric" Format="C2" />
                    </kumiko:KumikoDataGrid.Columns>
                </kumiko:KumikoDataGrid>
            </DataTemplate>
        </kumiko:KumikoDataGrid.RowDetailsTemplate>
        <kumiko:KumikoDataGrid.Columns>
            <core:KumikoRowDetailsColumn />
            <core:KumikoColumn Header="Customer" PropertyName="Name" Width="240" />
        </kumiko:KumikoDataGrid.Columns>
    </kumiko:KumikoDataGrid>
</Page>
```

### 9.4 Code: a custom drawn renderer

```csharp
// PROPOSAL, not compiled.
sealed class NoteDetails : IKumikoRowDetailsRenderer
{
    static readonly KumikoPaint Text = new() { Color = new KumikoColor(60, 60, 60) };

    public float Measure(IDrawingContext ctx, in KumikoRowDetailsContext c) =>
        ctx.MeasureText(((Customer)c.Item).Note, Text, c.Bounds.Width - 24, KumikoTextWrap.Word).Height + 24;

    public void Draw(IDrawingContext ctx, in KumikoRowDetailsContext c) =>
        ctx.DrawTextInRect(((Customer)c.Item).Note, c.Bounds.Inflate(-12, -12), Text, KumikoTextWrap.Word);

    public string? GetAccessibleText(in KumikoRowDetailsContext c) => ((Customer)c.Item).Note;
}

grid.RowDetailsRenderer = new NoteDetails();
grid.RowDetailsHeightMode = KumikoRowDetailsHeightMode.Auto;
grid.ExpandRowDetails(selectedCustomer);
```

(`DrawTextInRect`, `MeasureText` and `Inflate` follow #23's wrapped-text API and are subject to its final
signatures.)

## 10. Test plan

Core (xUnit, `tests/KumikoUI.Core.Tests`):

- **Expand model**: `SetExpanded`/`Toggle`/`ExpandAll`/`CollapseAll`; `Expanding` veto; `AllowMultipleExpanded`
  false closes others; `HasDetails` predicate; items removed from the source leave the set; `SetItems` clears;
  value-type boxed item limitation documented by a test.
- **Layout (needs #102)**: heights with and without panels in Fixed and Auto; the uniform path returns when the last
  panel closes; Y-to-row and row-to-Y round trips with 10 open panels in 100K rows; anchoring when a row above the
  viewport opens (visible rows do not move); `ScrollIntoViewOnExpand` limits.
- **Hit testing**: `Cell`, `RowDetailsToggle`, `RowDetails` bands for a row with a panel; frozen rows have no panel;
  grouped views; a tap in a panel does not select.
- **Rendering** (SkiaSharp tests): golden or pixel-probe tests for the panel background, border and indicator;
  `KumikoFieldListRowDetails` output; clip under the header and sticky group header; RTL mirroring.
- **Zero allocation**: extend `RenderAllocationTests` with 3 open panels, Fixed and Auto, with the field list and a
  custom renderer: 0 B per frame after warm-up. `PerformanceBudgetTests`: frame time with 10 open panels in a
  100K row grid is within the #102 budget; first expansion (uniform to non-uniform) within #102's rebuild budget.
- **Selection, keyboard, editing**: Alt+Down/Up, Enter on the toggle cell, a running edit is committed before
  opening (and a failed commit blocks it); sort, filter and grouping keep open state; `Selection` trigger follows
  selection; row drag with an open panel.
- **Accessibility**: announcements through `KumikoAnnouncements`; with #79, tree-dump snapshots with `Expanded` and a
  `RowDetails` node (#79 section 16 asks every feature for a semantics test).

Hosts (`tests/KumikoUI.Maui.Tests`, `tests/KumikoUI.Uno.Tests`): property forwarding and defaults identical on both
hosts; survive `Theme` change and `ItemsSource` change; hosted pool realizes only visible expanded rows and recycles;
a template's `BindingContext`/`DataContext` is the item; template measured height reaches the layout; teardown
(`Detach`) releases views. Manual or DevFlow checks (`eng/devflow`): a drawn panel and a hosted nested grid on
MAUI Mac Catalyst and Uno desktop, scrolling fast with panels open (lag R1), keyboard path, screen reader pass
on one platform.

## 11. Compatibility

Additive. No default changes: with no renderer or template the toggle column is inert and the layout is the 1.0
uniform path. `KumikoColumnType.RowDetailsToggle` and `HitRegion.RowDetails`/`RowDetailsToggle` are appended enum values (switches in
user code over those enums get a new case, noted in the CHANGELOG as `Added`). `KumikoStyle` gains three
properties. Nothing in the public API is removed. This spec is **blocked by #102**: shipping it in 1.0.x
patch releases is not possible (VERSIONING: new public API is 1.x minor).

## 12. Staged implementation issues and effort

Sizes: S = under 2 days, M = 3 to 5 days, L = 1 to 2 weeks (one engineer, including tests and docs). The
issues are in dependency order. Stages 1 to 4 deliver the drawn path on both hosts; 5 and 6 are independent after 3.

| # | Issue | Scope | Depends on | Size |
|---|---|---|---|---|
| 1 | **Core expand model**: `KumikoRowDetails` on `KumikoSource`, enums, event args, `HasDetails`, item pruning, `KumikoRowDetailsTriggers`; unit tests | section 5.1 | none (no layout yet) | M |
| 2 | **Layout term and scroll anchoring**: details height in #102's row-height function, uniform fast path switching, `InvalidateRowHeight` on toggle, `ScrollIntoViewOnExpand`; layout and scroller tests | 6.1 | #102 stages 1 to 3, issue 1 | M |
| 3 | **Drawn panel**: `IKumikoRowDetailsRenderer`, `KumikoRowDetailsContext`, `DrawRowDetails` pass, style colors, `KumikoFieldListRowDetails`, Auto measurement and cache, allocation and perf tests | 5.2, 5.5, 6.2 | 2 | L |
| 4 | **Toggle column, hit regions, input**: `KumikoRowDetailsColumn`, `RowDetailsToggle` column type and renderer, `HitRegion` values, `IInteractiveRowDetailsRenderer`, keyboard (Alt+Down/Up, Enter), commit-edit-first, row drag with panels, `KumikoStrings` and announcements | 5.3, 5.4, 6.3, 6.5, 8 | 3 | L |
| 5 | **MAUI and Uno host surface + sample**: properties, events, command, forwarding, theme/ItemsSource survival, host tests, sample page with the field list and a note renderer, README and CHANGELOG | 7.1, 7.3, 9 | 4 | M |
| 6 | **Hosted `RowDetailsTemplate` spike, then implementation**: slot list from Core, pool, overlay layer, positioning, measure, clip, focus routing, lag mitigation; nested-grid sample; decision record on platforms | 7.2 | 5 | L (spike M first) |
| 7 | **Accessibility tree integration** (when #79 core exists): `Expandable`/`Expanded`, `RowDetails` node, UIA/UIKit/Android/ARIA mapping | 8 | #79 core, 4 | M |
| 8 | **Animated expand/collapse** (optional, after feedback) | 6.4 | 3 | M |
| 9 | **Drawn form helper** (optional): `DrawnComponent`s inside a drawn panel for light editing | non-goals | 3, #38/#44 as needed | L |

Total for the drawn path (1 to 5): about 3.5 to 4.5 engineer-weeks after #102 stage 3. With hosted panels and
accessibility (6 and 7): about 6 to 7 weeks. The riskiest item is 6 (R1).

## 13. Risks

| | Risk | Mitigation |
|---|---|---|
| R1 | Hosted views lag or jitter against the canvas during fast scroll on some platforms | Spike first; drawn path ships regardless; mark hosted experimental per head if needed |
| R2 | The #102 layout lands late or changes shape | This spec adds one term to its internal function; keep the term behind `ComputeRowHeight` so only that function changes |
| R3 | First expansion on a very large grid is slow (uniform to non-uniform) | Benchmark item; #102's index is O(n) once, and it is the same cost as turning on Auto height |
| R4 | Nested hosted grid steals wheel and gestures | Scroll hand-off rule (Q6); a fixed-height nested grid is the supported shape |
| R5 | State by item breaks for structs in `IList` | Same limitation as selection; documented |
| R6 | A panel taller than the data area | Clipped; `ScrollIntoViewOnExpand` aligns the row's top; a nested scroller is the answer for tall content |

## 14. Open questions (for the maintainer)

1. **Drawn first, hosted second?** Is it acceptable to ship drawn panels (stages 1 to 5) as the feature and treat
   hosted templates (stage 6) as a later, possibly experimental addition, given the scroll lag risk?
2. **Toggle as a column or a grid option?** Proposed: `KumikoRowDetailsColumn` the app adds (consistent with #106). Alternative: a
   `ShowRowDetailsToggle` option that injects a frozen leading column. Which one, or both?
3. **Keyboard.** Alt+Down/Alt+Up to expand and collapse, Enter or Space on the toggle cell. Alt+arrows are taken by
   the back/forward navigation on some platforms (Android hardware keyboards, browsers on WASM). Alternatives:
   Ctrl+Right/Left, or `Right`/`Left` when the current column is the toggle column.
4. **Filtered-out items keep expansion?** Proposed yes (state follows the item until it leaves the source). The selection model drops items that
   leave the view. Which rule?
5. **Default height and mode.** Fixed 120, or Auto by default when a renderer is set?
6. **Nested scrolling.** Does a hosted nested grid take the wheel until it hits its end, then pass it on (proposed), or never pass it on?
7. **Frozen rows.** Is excluding legacy `FrozenRowCount` rows acceptable? Allowing them means the frozen block's height becomes variable, which
   #102 explicitly keeps fixed.
8. **Layout persistence.** Should `KumikoLayoutState` save expanded items (needs a stable key per item), or leave it to the app?
9. **Layout invalidation shape.** Does #102 expose `InvalidateRowHeight(object)` as the item-level height change (assumed), or does it add a `KumikoViewChangeKind` for it?
10. **Export and copy.** Should `GetAccessibleText` also feed export (#105) and clipboard (#44) as an opt-in?
11. **Selection trigger.** Is `Selection` (details follow selected rows) worth shipping in the first stage, or cut to reduce the matrix?
12. **Hosted and drawn together.** Allow a drawn renderer for the first N lines and a hosted view below? Proposed no: one source per grid.

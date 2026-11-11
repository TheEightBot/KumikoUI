# Spec: cell selection unit with ranges and clipboard (#108)

| | |
|---|---|
| Issue | [#108](https://github.com/TheEightBot/KumikoUI/issues/108) (plan key X7, 1.x) |
| Findings | EDIT-06, CORE-26 ([02-core-components-editing-input.md](../release-readiness/02-core-components-editing-input.md), [01-core-data-layout-render.md](../release-readiness/01-core-data-layout-render.md)) |
| Builds on | #30 item-based `KumikoSelectionModel` (merged), #20 `KumikoSource.ViewChanged` (merged) |
| Designed on top of (unmerged) | #44 clipboard (PR #211), #46 editor key routing (PR #205), #204 `SelectionTextColor` (PR #210), #59 `KumikoSelectionBinder.Detach/Attach` (PR #197) |
| Depends on (in parallel) | #79 accessibility tree spec |
| Status | Draft for review |

## 1. Summary

Add a cell selection unit to the grid: the user selects rectangular ranges of cells with the mouse, touch and
keyboard, several ranges at once in Extended mode, and copies them as tab-separated text through the #44
clipboard path. The selection is stored as a short list of rectangles, not a set of cells, so selecting
everything in a 100K x 50 grid costs the same as selecting one cell. Rectangles are positional while the view
holds still. When the view changes they are rebuilt from identity: they follow their items through sort, filter,
grouping and collection changes, and their columns through reorder and hide, the same way the row selection
does since #30.

Row selection stays the default and does not change.

## 2. Goals and non-goals

### Goals

- `KumikoSelectionUnit` with `Row` (today's behavior, default), `Cell` and `RowOrCell`.
- Rectangular ranges, several of them in Extended mode, with an anchor and an active (current) cell.
- Pointer: click, Shift+click, Ctrl/Cmd+click, mouse drag with edge autoscroll, column header click (opt-in),
  the select-all corner, and touch behavior that does not break panning.
- Keyboard: Shift+arrows, Shift+PageUp/PageDown/Home/End, Ctrl+Shift+arrows to the data edge, Ctrl+A,
  Ctrl+Space and Shift+Space.
- Rendering: a range fill plus an outline on the active range. It must be correct across frozen columns, frozen
  rows, sticky group headers and the pinned footer, and allocate nothing in steady state (#68).
- Copy of ranges as TSV through #44's `KumikoTsvFormatter` and `IKumikoClipboard`, with defined multi-range
  rules.
- A defined meaning for every existing row-based API (`SelectedItems`, `SelectedRows`, `IsRowSelected`,
  `SelectionChanged`, `KumikoSelectionBinder`, `RowTapped`) in the cell units.
- O(ranges) memory and O(log ranges) hit queries on 100K rows x 50 columns.

### Non-goals (this spec)

- Paste is designed here (section 10) but is a separate, optional implementation stage. Cut, the Delete key
  clearing cells, and fill (Ctrl+Enter, Ctrl+D) are out of scope.
- A grid-level undo stack. Paste reports its changes so that an app, or a later grid feature, can undo them.
- Non-rectangular shapes beyond a union of rectangles, and Excel's "add to selection" mode (Shift+F8).
- Moving the active cell inside a selection with Tab/Enter (Excel behavior). Tab and Enter keep their current
  meaning and collapse the selection.
- Selecting cells in group header, group summary and table summary rows. These rows are never selected, as in
  Row unit (#30).
- Cell merging, stacked headers and variable row height (X4, X8). The design does not block them.

## 3. Current state (main @ c77f44b)

- `KumikoSelectionModel` (`src/KumikoUI.Core/Models/KumikoSelectionModel.cs`) tracks selected items
  (`_items`, `_rowOfItem`, `_rows`), the current cell (`_currentKey`, `_currentColumn`, `_currentCell`) and the
  Shift anchor (`_anchorKey`, `_anchorRow`) by identity. `OnViewChanged` rebuilds them from
  `KumikoViewChangedEventArgs.GetViewRow`. `SelectedCells` and `SelectionUnit` were removed by #30. Only rows
  are selectable.
- `HandleRowSelection(row, shift, ctrl)` is the only click path. `Navigate*` call
  `MoveTo` → `HandleRowSelection(row, shift, false)`. Two latent problems the cell work has to fix first:
  - `NavigateLeft` and `NavigateRight` step `CellPosition.Column` by ±1 in `KumikoSource.Columns` *index*
    order. That order includes hidden columns, and it differs from the on-screen order when left-frozen,
    scrollable and right-frozen columns are interleaved by index. The renderer draws panes in the order
    left-frozen → scrollable → right-frozen (`KumikoLayoutEngine.PopulateVisible*Columns`).
  - In `Multiple` mode, arrow keys toggle every row they land on: `MoveTo` passes `ctrl: false` and
    `HandleRowSelection` toggles whenever `Mode == Multiple`.
- `KumikoInputController.HandlePointerUp` selects on release (`HitRegion.Cell`). A mouse drag on a cell pans
  (`_isPanning`) or, with `AllowRowDragDrop`, reorders the row. A header click sorts (Ctrl/Shift for multi-sort).
  The pointer handlers test `InputModifiers.Control` only, while `KumikoKeyEventArgs.HasCommand` accepts
  Control or Meta.
- `KumikoPointerEventArgs` has no device type. Uno knows it (`_lastPointerWasTouch` from
  `PointerDeviceType`), and MAUI's `SKTouchEventArgs.DeviceType` has it.
- `KumikoRenderer.DrawRows` and `DrawFrozenRows` fill the whole row with `PaintCache.SelectionBackground`
  when `IsRowSelected(row)`. `DrawCurrentCellBorder` draws a 2px `CurrentCellBorder` in each pane. Group rows,
  then sticky group headers, then the pinned footer are drawn over the panes.
- `KumikoSource.IndexOfRowKey` is a linear scan. `KumikoSelectionBinder.SelectedIndex` calls it on every
  `Publish`.
- `KumikoColumn` raises no change notifications. `ReorderColumn` raises a `Redraw` view change, and a change to
  `IsVisible` or `FreezeMode` raises nothing.

Unmerged work this design assumes:

- **#44 (PR #211)**: `IKumikoClipboard`, `KumikoTsvFormatter.Format(source, rows, includeHeaders)`,
  `KumikoInputController.CopySelection`, `CopyIncludesHeaders`, `ClipboardCopyRequested`, Ctrl/Cmd+C
  (`KumikoKey.C when e.HasCommand`), and the async paste plumbing `ReadClipboardForPaste`. The formatter writes
  visible columns in `Source.Columns` order, which is not the on-screen order when freeze modes are
  interleaved. Section 10 moves it to display order.
- **#46 (PR #205)**: while editing, the editor sees every key first. Navigation keys it leaves alone commit and
  navigate. Any other key stops at the editor unless it carries a shortcut modifier.
- **#204 (PR #210)**: `PaintCache.CellTextPaint(cellStyle, style, isSelected)` and `SelectedCellText`, so
  `CellRenderContext.IsSelected` controls the text color.
- **#59 (PR #197)**: `KumikoSelectionBinder.Detach()` and `Attach()`. `Attach` re-applies the view-model list
  to the model.

## 4. Concepts and terminology

- **Visible row**: index into the current view (`KumikoSource.RowCount`), as in `CellPosition.Row`. It
  includes group header and group summary rows, which are never selected.
- **Column index**: index into `KumikoSource.Columns`, as in `CellPosition.Column`. This does not change.
- **Display column**: position among the *visible* columns in on-screen order: left-frozen, then scrollable,
  then right-frozen, each in `Columns` order. Rectangles only make sense in display order, so ranges use display
  columns. Section 5.1 adds the helpers that convert between the two.
- **Range**: a rectangle of visible rows x display columns, inclusive on both axes. Either axis can be *whole*
  (all rows, or all columns).
- **Anchor**: the fixed corner of the active range, stored as a row key and a `KumikoColumn`.
- **Current cell**: the moving corner of the active range. It is the keyboard focus, the cell an edit opens on,
  and what accessibility reports as focused. This keeps today's rule: `CurrentCell` is where the keyboard is.
  Excel instead leaves the active cell at the anchor. We follow the existing row model and WPF/WinUI so that
  focus, editing and screen readers follow the user's last move.
- **Active range**: the last range. It is the one that Shift extends and that the outline is drawn around.

## 5. API

All new public types carry the `Kumiko` prefix (CONTRIBUTING.md naming rule, enforced by `eng/TypeNameCheck`).

### 5.1 Display column order (prerequisite)

On `KumikoSource`:

```csharp
/// The visible columns in on-screen order: left-frozen, scrollable, right-frozen, each in Columns order.
public IReadOnlyList<KumikoColumn> DisplayColumns { get; }
/// Display position of a column, or -1 when it is hidden or not in Columns.
public int GetDisplayIndex(KumikoColumn column);
/// Display position of Columns[columnIndex], or -1.
public int ToDisplayIndex(int columnIndex);
/// Columns index of the column at a display position, or -1.
public int ToColumnIndex(int displayIndex);
/// Bumped whenever DisplayColumns may have changed.
public int ColumnLayoutVersion { get; }
```

`KumikoColumn` has no notifications, so `DisplayColumns` is rebuilt lazily. Each call compares a cached
`KumikoColumn[]` with the current `Columns`, `IsVisible` and `FreezeMode`. The comparison is O(columns), is
cheap at ≤ 200 columns, and allocates only when the order changed (it reuses the array when the count is the
same). `ColumnLayoutVersion` increments when the comparison finds a difference, and always on `SetColumns` and
`ReorderColumn`. A future `KumikoColumn` property-change event (not required here) would let this skip the
comparison.

`KumikoSelectionModel.NavigateLeft`, `NavigateRight`, `NavigateHome` and `NavigateEnd` move in display order and
skip hidden columns. `KumikoInputController.EnsureCurrentCellVisible`, `FindNextTabStop` and
`KumikoTsvFormatter` use the same order. This fixes the index-order navigation bug in section 3 for every unit.

The same stage adds an O(1) row-key index: `KumikoSource.IndexOfRowKey` is backed by a
`Dictionary<object, int>` (`RowKeyComparer`), built lazily on first use after each view change and dropped
when the view changes. This removes the O(n) scan from `KumikoSelectionBinder.SelectedIndex` and from item
lookups in this spec.

### 5.2 Unit

```csharp
namespace KumikoUI.Core.Models;

/// What a click, a key or a programmatic call selects.
public enum KumikoSelectionUnit
{
    /// Whole rows (data items). The default; the behavior before this spec.
    Row,
    /// Rectangular ranges of cells. Whole-row and whole-column gestures select all the cells of the row or column.
    Cell,
    /// Cells, plus row gestures with row semantics: clicks on the row strip select, extend and toggle whole rows,
    /// Space toggles the current row, and fully selected rows are drawn as row selections.
    RowOrCell,
}
```

On `KumikoSelectionModel`:

```csharp
public KumikoSelectionUnit Unit { get; set; }   // default Row
```

Changing `Unit` clears the selection, keeps the current cell, resets the anchor to the current cell, and raises
`SelectionChanged` if anything was selected. `KumikoSelectionMode` keeps its meaning in every unit:

| Mode | Row (today) | Cell / RowOrCell |
|---|---|---|
| `None` | nothing selected | nothing selected; the current cell still moves |
| `Single` | one row | one cell, always the current cell; Shift, Ctrl and drag act like a plain click/move |
| `Multiple` | click toggles a row | click toggles a cell; drag adds a range; Shift is ignored; arrows move the current cell only |
| `Extended` | click / Shift range / Ctrl toggle | click replaces; Shift+click replaces the active range; Ctrl/Cmd+click toggles a cell or adds a range; drag selects |

### 5.3 Range type

```csharp
namespace KumikoUI.Core.Models;

/// A rectangle of selected cells in the current view: visible rows by display columns, inclusive.
/// A range is valid only for the view it was read from (see KumikoCellSelection.IsCurrent).
public readonly struct KumikoCellRange : IEquatable<KumikoCellRange>
{
    public KumikoCellRange(int topRow, int bottomRow, int firstDisplayColumn, int lastDisplayColumn);
    public static KumikoCellRange AllCells { get; }                      // both axes whole
    public static KumikoCellRange EntireRows(int topRow, int bottomRow);  // columns whole
    public static KumikoCellRange EntireColumns(int firstDisplayColumn, int lastDisplayColumn); // rows whole

    public int TopRow { get; }               // resolved against the current view
    public int BottomRow { get; }
    public int FirstDisplayColumn { get; }
    public int LastDisplayColumn { get; }
    public bool SpansAllRows { get; }        // a whole-column selection: follows the view as rows come and go
    public bool SpansAllColumns { get; }     // a whole-row selection: follows the columns as they are shown or added
    public int RowCount { get; }             // visible rows covered, group rows included
    public int ColumnCount { get; }
    public bool Contains(int row, int displayColumn);
}
```

Display columns, not `CellPosition`-style column indices, are used so that a range is a rectangle on screen.
`KumikoSource.ToColumnIndex` and `ToDisplayIndex` convert between the two. Programmatic entry points that take
a `CellPosition` (column index) convert internally.

### 5.4 Selection snapshot and cell identity

```csharp
/// A cell by identity: survives sort, filter, regroup and column reorder.
public readonly record struct KumikoCellInfo(object Item, KumikoColumn Column);

/// An immutable, cheap snapshot of the cell selection (a copy of the range list plus the view it was taken in).
public sealed class KumikoCellSelection
{
    public static KumikoCellSelection Empty { get; }
    public IReadOnlyList<KumikoCellRange> Ranges { get; }   // in selection order; the last is the active range
    public KumikoCellRange? ActiveRange { get; }
    public long CellCount { get; }                          // data cells, overlaps and group rows excluded
    public bool IsEmpty { get; }
    /// False once the view or the column layout changed after the snapshot was taken; enumeration then throws.
    public bool IsCurrent { get; }
    public bool Contains(int row, int displayColumn);
    public bool Contains(object item, KumikoColumn column);
    /// Every selected data cell once, row-major in view order, then display order. Lazy: no list is built.
    public IEnumerable<KumikoCellInfo> EnumerateCells();
    /// The visible data rows that contain at least one selected cell, ascending. Lazy.
    public IEnumerable<int> EnumerateRows();
}
```

`EnumerateCells` on Ctrl+A at 100K x 50 yields 5M structs without materializing them. A caller that wants a list
asks for it explicitly.

### 5.5 `KumikoSelectionModel` additions

```csharp
// Read
public KumikoCellSelection CellSelection { get; }        // cached until the selection or view changes
public IReadOnlyList<KumikoCellRange> SelectedRanges { get; }   // = CellSelection.Ranges
public bool IsCellSelected(int row, int column);          // column index, like CellPosition
public bool IsCellSelected(object item, KumikoColumn column);
public CellPosition Anchor { get; }                        // fixed corner of the active range (column index)

// Write (Cell / RowOrCell; in Row unit they select the rows the ranges touch)
public void SelectCell(int row, int column);               // replaces; moves the current cell and anchor
public void SelectRange(CellPosition anchor, CellPosition end);  // replaces; current cell = end
public void AddRange(KumikoCellRange range);               // Extended/Multiple; becomes the active range
public void DeselectRange(KumikoCellRange range);          // subtracts (splits rectangles)
public void SetSelectedRanges(IEnumerable<KumikoCellRange> ranges);  // one change, one event
public void SelectColumns(int firstDisplayColumn, int lastDisplayColumn, bool add);   // whole columns
public void SelectRows(int fromRow, int toRow, bool add);  // whole rows

// Input entry points (used by KumikoInputController; public like HandleRowSelection)
public void HandleCellSelection(int row, int column, bool shift, bool command);
public void ExtendActiveRange(int row, int column);        // drag / autoscroll / Shift+nav
```

Existing members keep their names. Their behavior in the cell units:

| Member | Row | Cell / RowOrCell |
|---|---|---|
| `SelectedItems`, `SelectedRows`, `IsSelected(item)`, `IsRowSelected(row)` | selected rows | **fully selected rows**: every display column of the row is selected (a whole-row range, or ranges whose columns together cover the row) |
| `Select(item)`, `SelectRange(int,int)`, `SetSelectedItems(items)` | select rows | select those rows as whole-row ranges (`SpansAllColumns`); `SetSelectedItems` replaces the whole selection, cells included, unless the items equal the current fully selected rows (then it is a no-op) |
| `Deselect(item)` | removes the row | removes every cell of the row (splits ranges) |
| `SelectAll()` | all data rows | `KumikoCellRange.AllCells` |
| `Clear()` | as today | as today |
| `HandleRowSelection` | as today | RowOrCell: row-strip gestures; Cell: selects the row's cells |

The "fully selected rows" rule keeps every row consumer (the binder, a "delete selected rows" command,
`SelectionChanged.AddedItems`) correct with one meaning. In Row unit a selected row is also a row with every
cell selected, so the rule needs no special case. Cell-level consumers read `CellSelection`.

`SelectedItems` and `SelectedRows` stop being backed by `HashSet`s in the cell units. They become read-only set
views over the row intervals (section 6.4). `Contains` is O(log ranges) through the O(1) row-key index,
`Count` is O(ranges), and enumeration is ascending and lazy.

### 5.6 `SelectionChanged`

`KumikoSelectionChangedEventArgs` gains:

```csharp
public KumikoSelectionUnit Unit { get; }
public KumikoCellSelection CellSelection { get; }   // after the change; Empty in Row unit
```

Rules:

- `AddedItems` and `RemovedItems` are the delta of *fully selected rows*. In the cell units they are lazy
  lists computed from the interval difference on first access. A click that selects one cell raises the event
  with both lists empty.
- In the cell units the event is raised for **any** change to the cell selection (`CellSelection` differs),
  including changes caused by reconciliation. The docs change from "raised when items are added or removed" to
  "raised when the selection changes; `AddedItems`/`RemovedItems` report the rows that became or stopped being
  fully selected".
- `SelectedRows` (the args property) becomes a lazy ascending list over intervals instead of a materialized
  array, in every unit. This also removes the 100K-element array that Ctrl+A allocates in Row unit today.
- During a drag, the event is raised on every move that changes the active range (one at most per pointer
  event). Coalescing to the release is an open question (O3).

### 5.7 Hosts (MAUI and Uno)

Both `KumikoDataGrid` hosts mirror the Core surface, as they do for `GridSelectionMode`:

| Property | Kind | Notes |
|---|---|---|
| `GridSelectionUnit` (`KumikoSelectionUnit`) | bindable / DP, two-way | maps to `Selection.Unit` |
| `CellSelection` (`KumikoCellSelection`) | read-only bindable / DP (MAUI `BindablePropertyKey`, Uno DP with a private setter) | republished on every `SelectionChanged`; bind it one-way |
| `HeaderClickAction` (`KumikoHeaderClickAction`) | bindable / DP | section 7.5 |
| `ClipboardCopyIncludesHeaders` | plain property (#44) | also applies to ranges |

`SelectedItem`, `SelectedIndex`, `SelectedItems`, `CurrentCell`, `SelectionChanged` and
`SelectionChangedCommand` keep their meaning through the "fully selected rows" rule. Setting cells from a view
model is done in code (`grid.Selection.SetSelectedRanges(...)`). A two-way bindable cell list is deferred (O4).

## 6. Internal model

### 6.1 Storage

```csharp
// internal, in KumikoSelectionModel
private struct SelectionBlock
{
    public bool AllRows;    public int Top, Bottom;     // visible rows, inclusive; may cover group rows
    public bool AllColumns; public int First, Last;     // display columns, valid for _columnLayout
}
private readonly List<SelectionBlock> _blocks = new();  // selection order; the last block is active
private KumikoColumn[] _columnLayout;                   // DisplayColumns when the blocks were last valid
private object? _anchorKey; private KumikoColumn? _anchorColumn;   // _anchorKey already exists
private int _version;                                   // bumped on every change; caches key on it
```

The meaning is a **union** of blocks. Overlaps are allowed and never double-counted (`CellCount`,
`EnumerateCells` and copy merge per row). Group rows inside a block are covered positionally but never
selected. Keeping blocks positional means a drag across a group header stays one block, and reconciliation can
bridge across group rows that it lands next to.

Row unit keeps its current item store. The block store is used only in the cell units, so Row unit pays
nothing for this feature.

### 6.2 Reconciliation: identity across view changes

Rule: **an axis marked whole is structural, and a bounded axis follows identity.** This matches #30: anything
no longer visible leaves the selection, and items that newly appear are not selected. The exception is a
whole-column (or whole-grid) selection, which keeps covering the column as rows come and go, because the user
selected *the column*. Open question O1 covers that exception.

Rows, in `OnViewChanged(KumikoViewChangedEventArgs e)`:

| `e.Kind` | Bounded row axis | Cost |
|---|---|---|
| `Redraw` | unchanged (also check the column layout, below) | O(1) |
| `Insert` | rows ≥ `ViewIndex` shift by `Count`; a block that straddles `ViewIndex` splits in two (inserted items are not selected) | O(blocks) |
| `Remove` | rows inside the removed span drop; the rest shift up; empty blocks go | O(blocks) |
| `Rebuild` (sort, filter, group, expand/collapse, `BeginUpdate` batch) | for each block, map each old row `Top..Bottom` that was a data row through `e.MapRow`, keep targets that are data rows, sort, then split into runs, joining across new-view gaps that contain only non-data rows | O(S log S), where S is the number of selected rows in bounded blocks |

The anchor and current cell already follow their keys (`_anchorKey`, `_currentKey`). The anchor column follows
`_anchorColumn` by reference. If that column is hidden or removed, the anchor collapses to the current cell.

Columns are checked lazily by comparing `_columnLayout` with `Source.DisplayColumns`, at the start of every
public query, every input entry point, and once per frame from `KumikoRenderer.Render`. This is needed because
column changes raise no event. When the order differs, each block's display columns `First..Last` are mapped
through the old layout to their columns and then to new display indices. Hidden or removed columns drop out.
The rest are sorted and split into runs, which can split a block into several. `AllColumns` blocks are
unaffected. `_columnLayout` is then replaced.

After either step, empty blocks are removed, blocks that are identical or adjacent with the same other axis are
merged, the active block stays last, `_version` is bumped, and `SelectionChanged` is raised when the resolved
selection differs.

**Fragmentation.** After a sort, a 1,000-row block can become up to 1,000 one-row blocks. That is acceptable:
memory is 20 bytes per block, and queries use the index in 6.3. Blocks with the same column extent are merged
into one block with a sorted row-interval list, so a scattered selection over N rows costs O(N) intervals
rather than O(N) blocks.

### 6.3 Queries

- `IsCellSelected(row, column)`: converts the column to a display index, then runs a binary search on an
  interval index. The index is a list of blocks sorted by `Top` with a running maximum of `Bottom`, rebuilt only
  when `_version` changes, and whole-row blocks are checked first. Cost is O(log B + k), where k is the number
  of overlapping hits.
- `CellCount`: the sum over blocks of data rows times columns, minus overlaps. Overlaps come from a sweep over
  block row boundaries, O(B log B). Data rows in an interval come from `KumikoSource.CountDataRows(from, to)`
  (new, internal): with grouping active it reads a prefix-sum array built once per view (an `int[RowCount]`);
  without grouping it is `to - from + 1`.
- Fully selected rows: rows covered by `AllColumns` blocks, plus rows where the union of bounded blocks covers
  every display column. Both come from the same sweep, O(B log B), cached per `_version`.

### 6.4 `SelectedRows` / `SelectedItems` views

`KumikoRowIntervalSet : IReadOnlySet<int>` (internal) holds sorted, disjoint data-row intervals.
`SelectedItems` wraps it and maps items through the O(1) row-key index. Both are rebuilt per `_version`, never
per access.

## 7. Pointer gestures

### 7.1 Device type

`KumikoPointerEventArgs` gains `DeviceType` (`KumikoPointerDeviceType { Mouse, Touch, Pen }`, default
`Mouse`) and `HasCommand` (Control or Meta, the same as `KumikoKeyEventArgs.HasCommand`). MAUI maps
`SKTouchEventArgs.DeviceType`, and Uno maps `PointerPoint.PointerDeviceType` (it already computes
`_lastPointerWasTouch`). Every add/toggle gesture below tests `HasCommand`, so Cmd+click works on macOS. On Mac
Catalyst, Ctrl+click is a secondary click and opens a context menu (`ContextRequested`), not a toggle.

### 7.2 Clicks (Cell and RowOrCell, Extended)

Selection moves from release to press for mouse and pen in the cell units, so a drag can grow from the press.
Touch keeps selecting on release, because a press that becomes a pan must not select.

| Gesture | Effect |
|---|---|
| click | `SelectCell`: one range of one cell; anchor = current = cell |
| Shift+click | replace the active range with anchor → clicked cell; other ranges stay (Excel); current = clicked cell |
| Ctrl/Cmd+click on an unselected cell | add a one-cell range, make it active; anchor = current = cell |
| Ctrl/Cmd+click on a selected cell | subtract that cell (up to 4 rectangles); current = cell; anchor = cell |
| Ctrl/Cmd+Shift+click | add a new range anchor → clicked cell, keeping the others |
| double-click | as today (select, then the DoubleTap edit trigger) |

A click with Shift or Ctrl/Cmd never toggles a Boolean cell, never starts an edit and never activates an
interactive cell (action buttons). It only selects. `RowTapped` and `RowDoubleTapped` are still raised with the
row, column and item, as today. A click on a group header row keeps toggling the group and moves the current
cell there without selecting.

### 7.3 Drag-select (mouse and pen)

- A press on a data cell, followed by a move beyond `DragThreshold` (8px), starts a drag-select. In the cell
  units a mouse or pen drag on a cell no longer pans (the wheel, scroll bars and touch still scroll) and no
  longer starts a cell-drag row reorder (`AllowRowDragDrop` then works only from the drag handle, as with
  `ShowRowDragHandle`). Row unit keeps today's behavior.
- Each move hit-tests the pointer, clamped into the data area, and calls `ExtendActiveRange(row, column)`.
  Ctrl/Cmd at press time makes the drag add a new range rather than replace the selection.
- A drag that started in the open editor stays in the editor (#44's `EditSession` pointer capture). A drag that
  starts on a different cell first commits the edit (`CommitEditBeforeViewChange`), and a refused commit
  cancels the drag.
- `Released` ends the drag. `Cancelled` ends it and keeps the range reached so far.

### 7.4 Edge autoscroll

While a drag-select is active and the pointer is outside the *scrollable* data region, the controller scrolls
toward the pointer. The scrollable data region is the area below the header, top summaries, frozen rows and
sticky band, above the pinned footer, and between the frozen panes and drag handle strips. Each scrolled frame
extends the range to the cell now under the clamped pointer.

- Speed: `clamp(distance / RowHeight, 1, 10)` rows per 16ms frame vertically, and the same in units of
  `DefaultWidth / 4` horizontally. Distance is measured from the region edge, and the speed is normalized by the
  frame interval like `InertialScroller`.
- New API: `KumikoInputController.IsAutoScrolling` and `bool UpdateAutoScroll(ScrollState scroll, float frameIntervalMs = 16f)`.
  Hosts already run a frame timer for `UpdateInertialScroll` (MAUI `KumikoDataGrid.cs`, Uno
  `KumikoDataGrid.Input.cs`). After each pointer event they start it when `IsAutoScrolling` is true, and the
  tick calls both methods.
- Frozen panes: over the left-frozen pane, the drag scrolls left until `OffsetX == 0`, then extends into the
  frozen column under the pointer. The right-frozen pane and frozen rows work the same way. Over sticky group
  headers or the pinned footer, the pointer counts as outside (it scrolls).

### 7.5 Column header, corner and row strip

- `KumikoHeaderClickAction { Sort, SelectColumn }`. The property is `KumikoSelectionModel.HeaderClickAction`,
  mirrored by the hosts, and defaults to `Sort`, so existing apps do not change. With `SelectColumn` in a cell
  unit, a header click selects the whole column (`SelectColumns`, `SpansAllRows`), Shift+click extends from the
  anchor column, and Ctrl/Cmd+click adds or toggles. Sorting is still available through `KumikoSource` and the
  filter popup. A dedicated sort hit target is open question O2. A header drag still reorders the column.
- Corner: new `HitRegion.SelectAllCorner`, the header band above the left row strip (the drag handle column
  when `ShowRowDragHandle` with `DragHandlePosition.Left`). A click selects all, in Extended and Multiple modes
  and in any unit. A grid without a left strip has no corner, so Ctrl+A is the only way.
- Row strip (RowOrCell): a click on `HitRegion.RowDragHandle` that does not become a drag selects the row with
  row semantics (`HandleRowSelection`: Shift range, Ctrl/Cmd toggle). Today such a tap is ignored. In Cell unit a
  strip click selects the row's cells. `HitRegion.RowHeader` (declared, unused) is reserved for a future row
  header and gets the same behavior.

### 7.6 Touch

- A tap selects a cell (release-time, as today). A drag pans. Inertia, long press and the edit triggers are
  unchanged.
- A long press on a cell whose column has no `EditTrigger.LongPress` (the `EditTrigger.Default` set does not
  include it) selects the cell and enters **extend mode**: moves extend the range, with autoscroll, until
  release. With `LongPress` set, the long press edits as today.
- Drawn selection handles (corner grips to resize a range after the fact) are a follow-up issue (stage 8),
  because they need hit regions and drawing of their own.

## 8. Keyboard

The keys work in Cell and RowOrCell (Extended). In Single mode, Shift and the extend keys act like the plain
keys. In Multiple mode the plain keys move the current cell without changing the selection. "Command" means
`KumikoKeyEventArgs.HasCommand` (Ctrl on Windows, Linux and Android; Cmd on Apple platforms).

| Keys | Effect |
|---|---|
| arrows, PageUp/PageDown, Home/End, Command+Home/End | move the current cell (display order, hidden columns skipped) and collapse the selection to it |
| Shift + any of the above | `ExtendActiveRange` from the anchor to the new current cell |
| Command+arrows | move to the data edge: first or last data row (group rows skipped) in the arrow's direction, or first or last display column. Excel's "edge of the non-empty block" would scan cell values and is open question O5 |
| Command+Shift+arrows | extend to the data edge |
| Command+A | `SelectAll` (whole grid) |
| Ctrl+Space | select the whole columns of the active range (Excel binds Ctrl on every OS; macOS may reserve it for input sources, so the header click is the alternative) |
| Shift+Space | select the whole rows of the active range (RowOrCell: row semantics) |
| Space | Boolean cell: toggle (#179, unchanged). Otherwise Cell: toggle the current cell's selection; RowOrCell: toggle the current row |
| Command+C | copy (section 9) |
| Escape | clear the selection and current cell (unchanged) |
| Tab / Shift+Tab, Enter | unchanged (next tab stop / edit / commit-and-move-down); they collapse the selection to the new current cell |

Rules for the key paths:

- `KumikoInputController.HandleKey` passes Command to the navigation calls. Today Ctrl+Up and Ctrl+Down fall
  through to plain `NavigateUp` and `NavigateDown`. The `Navigate*` methods gain a `toEdge` flag. Their
  current signatures stay as overloads.
- Hosts that deliver Space as text (Uno's proxy `TextBox`, MAUI's `Entry`, #179) send `Character == ' '`
  with Shift or Ctrl. In the cell units that must count as `KumikoKey.Space`, not as typing. Without that rule,
  Shift+Space would open the editor with a space.
- In Single and Multiple modes, the arrow keys move the current cell and never toggle. This also fixes the
  Row-unit Multiple bug in section 3: `MoveTo` stops calling the toggle path.
- While editing (#46), the editor sees keys first. Shift+Left/Right select text in a text editor.
  Shift+Up/Down, which a single-line editor leaves alone, commit and then extend, like the plain navigation
  keys. Command+A and Command+C go to the editor.

## 9. Rendering

All rendering changes are in `KumikoRenderer`, are used only when `Unit != Row`, and keep the three-pass pane
scheme (scrollable, left-frozen, right-frozen) plus the frozen-row passes.

### 9.1 Per-frame selection mask

At the start of `Render`, after the visible row range and pane column lists are computed:

1. Call `selection.SyncColumnLayout()` (internal). It is the lazy column check from 6.2 and allocates only when
   the layout changed.
2. Fill a reusable `bool[]` mask indexed by (visible row slot x display column) for the frozen rows and the
   visible scrolling rows. The mask is grown only, so it allocates nothing in steady state. Each block whose
   rows intersect the window (found through the interval index) sets its cells. Cost is O(log B + covered
   visible cells), and nothing is looked up per cell during drawing.

### 9.2 Fill

- `DrawRowCells` passes `isSelected = mask[slot, display]` to `CellRenderContext`, so custom renderers and the
  #204 `SelectedCellText` paint (`CellTextPaint(colCellStyle, style, isSelected)`) follow the cell. After the
  per-column cell background and before the content, a selected cell gets
  `ctx.FillRect(cellRect, _paintCache.SelectionBackground)`. That paint is translucent by default, so per-cell
  backgrounds and conditional row colors still show through.
- Row backgrounds: in Cell unit, rows use their normal background (alternate, conditional, focused) and never
  the row `SelectionBackground`. In RowOrCell, fully selected rows get the row `SelectionBackground` across the
  full width, drag-handle strip included, so they look as they do in Row unit.
- Group header and summary rows are not filled. They are drawn over the panes afterwards, so a fill could not
  leak through them anyway.

### 9.3 Outline

- New style members: `KumikoStyle.SelectionBorderColor` (default: `CurrentCellBorderColor`) and
  `SelectionBorderWidth` (default 1). `PaintCache.SelectionBorder` is built with the other paints.
- Only the **active** range gets an outline, when it covers more than one cell. With one cell, the current-cell
  border (`DrawCurrentCellBorder`, 2px) is the only marker, as today.
- In each pane pass, `DrawActiveRangeOutline` computes the part of the active range that lies in that pane's
  columns, and in each vertical band (frozen rows, scrolling rows). It draws up to four line segments: the
  left edge only if the range's first display column is in this pane, the right edge only if its last column is
  in this pane, the top edge only if `TopRow` is in this band, and the bottom edge the same way. Without this,
  an outline crossing the frozen divider would draw a false vertical line at the pane edge.
- Drawing order inside each pane clip: rows, the outline, the current-cell border, then grid lines (the outline
  sits under the 2px current-cell border). Sticky group headers and the pinned footer are drawn later and
  cover both, so no extra clip is needed.

### 9.4 Allocation and cost

- The mask, interval index and range list are reused, and no LINQ runs on the frame path. `RenderAllocationTests`
  gains `SteadyState_CellSelectionWithRangesAcrossPanes_AllocatesNothing`: a multi-range selection crossing
  the frozen columns and frozen rows, with sticky headers on, and an active outline.
- Frame cost: O(log B + visible cells). With a fragmented 1,000-block selection, a 60 x 20 visible window stays
  under 0.1 ms on the mask.

## 10. Clipboard

### 10.1 Copy (on #44)

`KumikoInputController.CopySelection(selection, dataSource)` branches on `selection.Unit`:

- **Row**: unchanged (`KumikoTsvFormatter.Format(source, SelectedRows, CopyIncludesHeaders)`).
- **Cell / RowOrCell**: a new overload,
  `KumikoTsvFormatter.Format(KumikoSource source, IEnumerable<int> rows, IReadOnlyList<KumikoColumn> columns, bool includeHeaders)`.
  The existing overload delegates to it with `source.DisplayColumns`, which moves #44 from `Columns` order to
  display order. Rows are visible data rows ascending, with group rows skipped. Fields are display text
  (`GetCellDisplayText`), escaped by `EscapeField`, and every row ends with `\r\n`, as #44 does.

Which cells are copied:

| Selection | Copied block |
|---|---|
| one range | its data rows x its display columns |
| several ranges, all with the same display columns | union of their rows (ascending, de-duplicated) x those columns (Excel) |
| several ranges, all with the same rows | those rows x the union of their columns in display order (Excel) |
| several ranges, otherwise | the **active range only** (Google Sheets). Excel refuses instead; a silent refusal is worse here because there is no message surface (O6) |
| whole column / whole grid | the visible rows only, because filtered-out rows are not in the view |

With `CopyIncludesHeaders`, the first row holds the copied columns' `Header`. `ClipboardCopyRequested` and
`IKumikoClipboard.TrySetText` are used unchanged.

Large copies: Ctrl+A then Ctrl+C at 100K x 50 writes 5M fields. The formatter streams into one `StringBuilder`
sized from an estimate (cells x 8 characters) and allocates nothing per row (no `List<int>` copy when the rows
are already ascending). Most of the cost is the `GetCellDisplayText` strings, because off-screen cells are not
in the display-text cache. The target is in section 12. A cell-count cap or async copy is open question O7.

### 10.2 Paste into ranges (design only; optional stage 9)

Trigger: Command+V outside an editor, when `EditSession` is set and the grid is not read-only. The clipboard is
read through `IKumikoClipboard.GetTextAsync` with #44's pending-read pattern: a delayed result is applied only
if the selection `_version` and the view have not changed since the key press.

1. **Parse**: a new `KumikoTsvParser.Parse(string) → string[][]`, the inverse of `EscapeField`. It handles quoted
   fields, embedded tabs and newlines, doubled quotes, `\r\n` or `\n` line ends, and drops one trailing empty
   line.
2. **Shape** (Excel rules): the clipboard is h x w, and the target is the active range or, in Row unit, the
   current cell.
   - 1 x 1 → fills every data cell of every selected range.
   - The target is a single range whose size is a multiple of h x w → the block is tiled.
   - The target is one cell, or smaller than the block → the block is pasted starting at the target's top-left,
     down the visible data rows (group rows skipped) and right along the display columns, and clipped at the
     last row or column. The clipped amount is reported.
   - Several ranges with a clipboard larger than 1 x 1 → refused (`CellsPasting` is raised with `Cancel = true`
     and a reason).
3. **Resolve**: target cells are captured by identity (`KumikoCellInfo`) before any write, because a write can
   re-sort. Read-only columns (`IsReadOnly`) and cells refused by `CellBeginEdit` are skipped and reported.
4. **Validate**: for each target cell, the text is converted with the edit commit's conversion (the
   `KumikoValueConverter` path behind `KumikoSource.TrySetItemValue`) and passed to `EditSession.CellValidator`.
   The results go into one cancelable `CellsPasting` event (`KumikoCellsPastingEventArgs`: values, targets, and
   per-cell converted value or error), which a handler can edit or cancel. `PasteErrorBehavior { AbortAll,
   SkipInvalid }` defaults to `AbortAll`: any `ValidationSeverity.Error` aborts the whole paste and shows the
   first error through the existing validation UI on that cell.
5. **Write**: all writes run inside `Source.BeginUpdate()` / `EndUpdate()`, which produces one `Rebuild`. Each
   written cell raises `CellValueChanged`, which apps rely on to persist edits. `CellBeginEdit` and
   `CellEndEdit` are not raised per cell. Then `CellsPasted` (`KumikoCellsPastedEventArgs`) is raised with an
   `IReadOnlyList<KumikoCellChange>(Item, Column, OldValue, NewValue)`.
6. **Selection**: afterwards, the selection is the pasted area. It is reconciled by identity like any other
   change.
7. **Undo**: `KumikoCellChange` lists are what an app needs to undo a paste in its own stack. A grid-level
   `KumikoUndoStack` (Command+Z outside an editor, covering edits, toggles and pastes) is a separate 1.x issue
   and consumes the same records.

## 11. Interaction with editing, row APIs, MVVM and accessibility

### 11.1 Editing (`EditSession`)

- An edit opens on `CurrentCell`, the moving corner, through F2, Enter, typing or the edit triggers. The
  selection is kept and drawn while editing. `EditSession.IsCellBeingEdited(row, col)` (column index) is
  unchanged, and the edited cell's content is skipped as today.
- Enter commits, moves down and collapses the selection to the new cell (`NavigateDown(shift: false)`). Tab
  commits, moves to the next tab stop and collapses. Escape cancels the edit and keeps the selection.
- A refused commit (`CommitEditBeforeViewChange` returns false) blocks any selection change: click, drag start,
  Shift+navigation and header select.
- Boolean toggles (`EditSession.ToggleBooleanCell`) and interactive cells act on the clicked cell only, never
  on the range. Fill-the-selection (Ctrl+Enter) is out of scope.
- `EditSession.ApplyViewChange` and the selection both reconcile from the same `KumikoViewChangedEventArgs`, so
  the editor and the range stay on the same items.

### 11.2 Row-based APIs and events

- `RowTapped` and `RowDoubleTapped` are raised as today in every unit.
- `CellBeginEdit`, `CellEndEdit`, `CellValueChanged` and `ValidationFailed` are unchanged.
- `ContextRequested` (roadmap) should carry the `CellSelection` snapshot when it lands.
- `KumikoSelectionChangedEventArgs.CurrentCell` stays the moving corner.

### 11.3 MVVM binder (`KumikoSelectionBinder`)

- `SelectedItem` and `SelectedIndex`: unchanged. The primary item comes from `AddedItems` (fully selected rows),
  or from the current item when that item is fully selected. In a cell-only selection, `SelectedItem` is null.
  An alternative is to report the current cell's item (O8).
- `SelectedItems` stays two-way, with the "fully selected rows" meaning. `ApplyListToModel` first compares the
  list with `model.SelectedItems` and does nothing when they are equal. That stops #59's `Attach()` (which
  re-applies the list) from replacing a cell selection with whole rows when the view model did not change its
  list. This change should be made in #59's code as well.
- The binder raises `Changed` when `CellSelection` changes, so the hosts can republish the read-only
  `CellSelection` property.
- Large deltas: Ctrl+A in a cell unit makes every row fully selected, so the owned `ObservableCollection`
  receives one `Add` per item, which is today's Row-unit cost. A Reset-based bulk update above a threshold is
  open question O9 and applies to both units.

### 11.4 Accessibility (#79)

This spec relies on the semantics provider that #79 is defining (the issue calls it `IGridSemanticsProvider`;
its final name follows that spec). It needs:

- Per-cell selected state from `IsCellSelected(row, column)`, O(log B). It is exposed on UIA as
  `ISelectionItemProvider.IsSelected` on the cell, `ISelectionProvider.CanSelectMultiple` (Multiple and
  Extended) on the grid, `UIAccessibilityTraits.Selected` on iOS cell elements,
  `AccessibilityNodeInfo.isSelected` with `CollectionItemInfo` on Android, and `aria-multiselectable` on the
  grid with `aria-selected` on each `gridcell` for WASM.
- `ISelectionProvider.GetSelection()` cannot return 5M elements. Return the realized (visible) selected cells
  plus the active range's corners. This is to be confirmed in #79 (O10).
- Announcements: when a range changes, announce "B2 to D10 selected, 27 cells" through #79's notification API.
  During a drag, announce only on release. During Shift+arrows, announce after each key, throttled.
- Keyboard parity: every pointer gesture in section 7 has a keyboard equivalent in section 8. Column selection
  uses Ctrl+Space, row selection Shift+Space, and select all Command+A.
- `SelectionChanged` invalidates the cell states of the visible semantic nodes only. It does not rebuild the
  tree.

Stage 8 delivers this integration once #79's provider exists. Stages 2 to 7 do not need it.

## 12. Performance on 100K rows x 50 columns

| Operation | Cost | Target |
|---|---|---|
| Ctrl+A (cell unit) | 1 block, O(1) | < 1 ms, < 1 KB (excluding binder list sync) |
| Drag over 100K rows | 1 block updated per move, O(1) + redraw | no allocation per move in steady state |
| `IsCellSelected` | O(log B) | < 100 ns at B = 1,000 |
| Frame mask (60 x 20 visible) | O(log B + visible cells) | < 0.1 ms, 0 B |
| Sort with 100K selected rows in bounded blocks | O(S log S) remap | < 30 ms, < 2 MB |
| Sort with a whole-column or whole-grid selection | O(1) | — |
| Filter or collapse | as for sort (bounded blocks) | — |
| `CellCount`, fully selected rows | O(B log B), cached per version | — |
| Column reorder or hide | O(blocks x columns) | < 1 ms |
| Copy 100K x 50 | O(cells), string-bound | < 1.5 s, allocation ≈ 2 x output size plus the display strings |

Measured by new `bench/KumikoUI.Benchmarks/SelectionBenchmarks.cs` (Ctrl+A, drag extend, reconcile after sort,
`IsCellSelected`, mask build, copy) and by allocation budgets in
`tests/KumikoUI.Core.Tests/Rendering/PerformanceBudgetTests.cs` (reconcile after sort ≤ 4 MiB, Ctrl+A ≤ 4 KiB).

## 13. Test plan

All tests use xUnit and live in `tests/KumikoUI.Core.Tests` unless noted.

| File | Covers |
|---|---|
| `DisplayColumnOrderTests.cs` | `DisplayColumns` with mixed freeze modes and hidden columns; `ToDisplayIndex`/`ToColumnIndex`; `ColumnLayoutVersion`; navigation in display order (regression for the index-order bug); O(1) `IndexOfRowKey` |
| `CellSelectionModelTests.cs` | each unit x mode (section 5.2 table); `SelectCell`, `SelectRange`, `AddRange`, `DeselectRange` splitting, `SetSelectedRanges`, `SelectAll`; `Unit` change clears; "fully selected rows" for `SelectedItems`, `IsRowSelected` and `Select(item)`; `CellCount` with overlaps and group rows; `EnumerateCells` order and de-duplication |
| `CellSelectionReconcileTests.cs` | sort, filter, group, collapse and expand, insert and remove inside, across and around blocks; whole axes stay whole; items that appear are not selected (bounded axes); column reorder, hide, remove and add (split and drop); anchor follows; snapshot `IsCurrent` turns false; `SelectionChanged` raised for reconcile-only changes |
| `KumikoSelectionChangedArgsTests` (in `ItemSelectionTests.cs`) | lazy `AddedItems`, `RemovedItems` and `SelectedRows`; cell-only change raises with empty item deltas; Row-unit behavior unchanged |
| `Input/CellSelectionPointerTests.cs` (with `InputGrid`) | click, Shift, Ctrl/Cmd (Meta) and Ctrl+Shift click; modifier clicks never toggle Booleans or edit; press-time selection for mouse, release-time for touch; drag-select; drag does not pan or reorder rows in cell units; refused commit blocks; header `SelectColumn` versus `Sort`; corner; row strip in RowOrCell; touch long-press extend versus `LongPress` trigger |
| `Input/CellSelectionAutoScrollTests.cs` | `IsAutoScrolling` and `UpdateAutoScroll` in four directions; speed by distance; frozen panes and frozen rows; sticky band and pinned footer count as outside |
| `Input/CellSelectionKeyboardTests.cs` | Shift+arrows, Page, Home and End; Command+(Shift+)arrows to the data edge with groups; Ctrl+A, Ctrl+Space, Shift+Space; Space toggles; Space delivered as text with Shift; Single and Multiple arrow behavior (including the Row-unit Multiple toggle regression); editing routing with #46 |
| `Rendering/CellSelectionRenderTests.cs` (recording `IDrawingContext`) | fill rectangles per pane; no row fill in Cell unit; full-row fill in RowOrCell; outline segments across the frozen divider and frozen rows; outline under the current-cell border; `IsSelected` and `SelectionTextColor` per cell (#204) |
| `Rendering/RenderAllocationTests.cs` | the new steady-state case from 9.4 |
| `Input/ClipboardTests.cs` (#44 file) | range copy; multi-range rules; whole column under a filter; headers; display order with interleaved freeze modes; Row unit unchanged |
| `KumikoSelectionBinderTests.cs` | fully selected rows in `SelectedItems`; `ApplyListToModel` no-op when equal; `Attach` keeps the cell selection (#59); `Changed` on cell change |
| `tests/KumikoUI.Uno.Tests` and MAUI host tests | `GridSelectionUnit`, `HeaderClickAction`, read-only `CellSelection` republished; `DeviceType` and `HasCommand` mapping; autoscroll timer start |
| (stage 9) `Input/PasteTests.cs`, `KumikoTsvParserTests.cs` | parser round trip with `EscapeField`; shape rules; read-only skip; validation abort and skip; one `Rebuild`; `CellsPasted` changes; stale async read ignored |
| `eng/TypeNameCheck` | new public names collide with nothing |

## 14. Staged implementation issues

Each stage is one issue, one branch and one PR, in this order. Stage 1 can merge before the others because it
fixes Row-unit bugs on its own.

1. **Display column order and O(1) row-key lookup** (core)
   - `KumikoSource.DisplayColumns`, `GetDisplayIndex`, `ToDisplayIndex`, `ToColumnIndex` and
     `ColumnLayoutVersion`; `IndexOfRowKey` backed by a lazy dictionary.
   - `NavigateLeft`, `NavigateRight`, `NavigateHome`, `NavigateEnd`, `EnsureCurrentCellVisible` and
     `FindNextTabStop` work in display order and skip hidden columns.
   - Multiple-mode arrows stop toggling rows.
   - Tests from `DisplayColumnOrderTests.cs`. No public API is removed.
2. **Cell selection model: unit, ranges, reconciliation** (core, no input or rendering)
   - `KumikoSelectionUnit`, `KumikoCellRange`, `KumikoCellInfo`, `KumikoCellSelection`; block store; the
     section 5.5 API; the "fully selected rows" semantics; extended `KumikoSelectionChangedEventArgs`.
   - Reconciliation for every `KumikoViewChangeKind` and for column layout changes.
   - `CellSelectionModelTests`, `CellSelectionReconcileTests` and args tests pass. Row-unit tests pass
     unchanged.
   - `SelectionBenchmarks` and the budget tests meet section 12.
3. **Render cell selection: fill, active outline, panes** (core)
   - Frame mask; per-cell fill with `isSelected` (on #204); `SelectionBorderColor`, `SelectionBorderWidth` and
     `PaintCache.SelectionBorder`; outline segments across panes and frozen rows; RowOrCell full-row fill.
   - `CellSelectionRenderTests` and the new zero-allocation test pass, and `RenderAllocationTests` stays green.
4. **Keyboard range selection** (core)
   - Every row of the section 8 table; data-edge navigation; Space-as-text rule; #46 routing.
   - `CellSelectionKeyboardTests` pass.
5. **Pointer range selection, drag and autoscroll** (core and both hosts)
   - `KumikoPointerDeviceType` and `HasCommand` mapped on MAUI and Uno; press-time selection; drag-select;
     `UpdateAutoScroll` wired to both host timers; `HeaderClickAction`; `SelectAllCorner`; row strip; touch
     long-press extend.
   - Pointer and autoscroll tests pass. Manual check on Windows, macOS (Catalyst), iOS, Android and WASM in the
     samples.
6. **Copy cell ranges as TSV** (core, after #44 merges)
   - Formatter overload; display order for the existing overload; multi-range rules.
   - Updated `ClipboardTests` pass. Ctrl+C works in both samples.
7. **Host and MVVM surface for cell selection** (MAUI, Uno, docs)
   - `GridSelectionUnit`, read-only `CellSelection` and `HeaderClickAction` on both hosts.
   - Binder changes, coordinated with #59.
   - README selection section, ARCHITECTURE.md (the `KumikoSelectionModel` row), and a sample page with a
     unit picker.
   - Host tests pass, and every public member is documented (#54).
8. **Cell selection accessibility** (after #79, #77, #78)
   - Per-cell selected state, multi-select, announcements and `GetSelection` policy on every adapter, per
     section 11.4.
   - Screen reader pass on Windows Narrator and VoiceOver.
   - Touch selection handles may be split into their own issue here.
9. **Paste into cell ranges (optional)** (core and hosts)
   - `KumikoTsvParser`, Command+V, shape rules, `CellsPasting`/`CellsPasted`, `PasteErrorBehavior`,
     `KumikoCellChange`.
   - Paste tests pass, and a paste produces one `Rebuild`.
   - Follow-up: "Grid-level undo stack for edits and paste".

## 15. Open questions

- **O1. Whole-column and select-all growth.** Should a whole-column or select-all selection keep covering rows
  that appear later (structural, as proposed), or freeze to the items present (identity, like Row-unit
  `SelectAll`)? The proposal keeps Ctrl+A at O(1) and matches spreadsheets. The cost is that Row and Cell units
  then differ when items are added after Ctrl+A.
- **O2. Sorting with `HeaderClickAction.SelectColumn`.** Add a sort-glyph hit region
  (`HitRegion.HeaderSortIndicator`), use Alt+click, or leave sorting to the filter popup and code?
- **O3. `SelectionChanged` during drag.** Raise it on every move that changes the range (proposed, live
  feedback), or once on release (cheaper for view models)?
- **O4. Two-way cell binding.** Is a settable `CellSelection` (or an `IList<KumikoCellInfo>` with the
  owned/attached pattern) needed in 1.x, or are code-behind `SetSelectedRanges` and the read-only snapshot
  enough?
- **O5. Data edge.** Should Command+arrow follow Excel and stop at the edge of non-empty cells? That costs a
  value scan, bounded by the scan distance. The proposal uses the view edge.
- **O6. Incompatible multi-range copy.** Copy the active range (proposed), refuse like Excel (no text, and
  `CopySelection` returns false), or copy the bounding box with blanks?
- **O7. Very large copies.** Cap the cell count, warn through an event, or format off the UI thread? Display
  text is computed on the UI thread today.
- **O8. `SelectedItem` for a cell-only selection.** Should it be null (proposed: only fully selected rows count)
  or the current cell's item?
- **O9. Bulk binder updates.** Should the owned `SelectedItems` use a Reset notification above a threshold (for
  example 1,000 items)? This affects Row unit too.
- **O10. UIA `GetSelection`.** Return the visible selected cells or every selected cell? To be decided with #79.
- **O11. RowOrCell.** Does it earn its place next to Cell plus `Shift+Space`? It differs in row-strip
  gestures, the Space toggle and full-row drawing. Dropping it before release costs nothing. Adding it later is
  additive.
- **O12. Inactive selection color.** Should the selection fill dim while the grid has no keyboard focus
  (`InactiveSelectionColor`), as most desktop grids do?
- **O13. Header highlight.** Should headers of columns that intersect the selection get a tint (Excel)? Off by
  default if added.

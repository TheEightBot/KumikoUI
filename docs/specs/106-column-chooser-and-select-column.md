# Spec: column chooser UI and multi-select checkbox column (#106)

| | |
|---|---|
| Issue | [#106](https://github.com/TheEightBot/KumikoUI/issues/106) (plan key X5, Epic X, 1.x fast follow-on) |
| Findings | research section 1.3 ([07-external-research.md](../release-readiness/07-external-research.md): column chooser is "API only", no checkbox selection column), CORE-33 (`DisplayIndex` and state persistence) |
| Builds on | #30 item-based `KumikoSelectionModel` (merged), #20 `KumikoSource.ViewChanged` (merged), #49 filter popup (merged) |
| Blocked by (per the issue) | #100 `ContextRequested` (open), #30 (merged) |
| Designed on top of (unmerged) | #36 `PopupLayout` (PR #212), #38 `IsThreeState` and the indeterminate dash (PR #217), #44 clipboard (PR #211), #77 / #78 accessibility (PRs #192 / #190) |
| Interacts with | #98 column reorder and `GridLayoutState`, #104 incremental loading (PR #221), #105 export, #107 paging, #108 cell selection |
| Status | Draft for review |

## 1. Summary

Two small features that share one idea: chrome the user needs to manage columns and rows, drawn by Core so it
looks and behaves the same on MAUI and Uno.

1. **Column chooser.** A drawn popup (a `DrawnComponent` shown through `PopupManager`, like `DrawnFilterPopup`)
   that lists the grid's columns with a visibility checkbox, a drag grip to reorder, a search box and a
   Reset button. It opens from a host API (`ShowColumnChooser()`), from a header right-click or long-press
   (#100), or from a keyboard shortcut. It edits `KumikoColumn.IsVisible` and the order of
   `KumikoSource.Columns` live, and raises one event so the app can persist the layout (#98).
2. **Selection column.** A built-in `KumikoSelectionColumn`: a narrow, frozen-left column whose cells are
   checkboxes bound to `KumikoSelectionModel`, and whose header is a tri-state "check all" over the current
   view. It reuses the #38 tri-state glyph. It needs no property on the item, and it is the way to get
   additive multi-select on touch screens, where Ctrl/Shift are not available.

Neither feature adds a dependency or changes today's defaults: no chooser opens and no selection column
exists unless the app asks for one.

## 2. Goals and non-goals

### Goals

- A column chooser with visibility toggles, drag reorder, search and reset, usable with pointer, touch and
  keyboard, correct with frozen columns, and drawn with the grid's theme.
- Three entry points: host API, header context request (#100), keyboard.
- One change event for column visibility and order that a layout-persistence feature (#98) can subscribe to.
  The chooser itself persists nothing.
- A `KumikoSelectionColumn` with per-row checkboxes, a tri-state header check-all over the current view,
  Shift-click range, and defined behavior for every `KumikoSelectionMode`, for editing, for grouping and for
  frozen panes.
- Accessibility parity with the rest of the grid (#77 / #78): both features are reachable and announced.
- Localizable strings through `KumikoStrings`.

### Non-goals

- A drawn context menu. #100 gives hosts the event; the app (or a later spec) shows the menu. This spec only
  adds the chooser as a target the menu can call.
- Persisting layouts. #98 owns `GridLayoutState`; this spec only makes sure its inputs exist and change events
  fire.
- Column grouping, pinning and sort commands inside the chooser (a "field list"). The chooser is visibility and
  order only. Freeze mode can be added later without changing the API.
- Checkbox selection of group header rows (a group-level check box). Designed as stage 7, optional.
- Retaining selected items that a filter hides. `KumikoSelectionModel` drops them today (#30); see Q5.

## 3. Current state (main at c77f44b)

- `KumikoColumn.IsVisible` (`Models/KumikoColumn.cs`) is a plain auto-property. Setting it raises nothing: no
  `ViewChanged`, no layout invalidation, no selection reconciliation. There is no chooser UI anywhere (report
  07 lists it as API only).
- `KumikoColumn.AllowTabStop`, `AllowReorder`, `AllowResize`, `AllowSorting`, `AllowFiltering`, `IsReadOnly`
  and `FreezeMode` (`ColumnFreezeMode.None/Left/Right`) exist. `KumikoColumn.Id` defaults to a random 8
  character GUID fragment, so it is not a stable key across runs unless the app sets it.
- `KumikoSource.Columns` is an `IReadOnlyList<KumikoColumn>` over a private list. `SetColumns` replaces it and
  rebuilds; `ReorderColumn(from, to)` moves one column and raises a `Redraw` `ViewChanged` (it is not
  structural). Hidden columns stay in the list, so `CellPosition.Column` indexes include them.
- `KumikoSelectionModel` tracks `SelectedItems`, `SelectedRows` and the anchor by identity, and drops items that
  leave the view. `Mode` is a `KumikoSelectionMode` (`None`, `Single`, `Multiple`, `Extended`).
  `HandleRowSelection(row, shift, ctrl)` is the only click path and, in `Extended`, a plain click replaces the
  selection. Public members: `Select`, `Deselect`, `SetSelectedItems`, `SelectRange(from, to)` (add only),
  `SelectAll` (a no-op in `None` and `Single`), `Clear`. There is no way to deselect a range and no
  "toggle without replacing" call. `OnViewChanged` keeps `_currentColumn` by column identity and, when the
  column is gone from `Columns`, falls back to the clamped old index; a column that is merely hidden is still in
  `Columns`, so the current cell can sit on a hidden column.
- `KumikoSelectionBinder` exposes `SelectedItem`, `SelectedIndex`, `SelectedItems` (two-way) to both hosts.
- `KumikoInputController` owns `PopupManager` and one `DrawnFilterPopup` (`ShowFilterPopup`, `CloseFilterPopup`,
  `FilterPopupOpened`/`FilterPopupClosed`). It anchors the popup with a `Func<KumikoRect>` that follows the
  header as the grid scrolls, and requests keyboard focus through `KeyboardFocusRequested`. Both hosts react to
  `FilterPopupOpened/Closed` to run the cursor-blink timer. `DrawnFilterPopup` has a search box
  (`ITextInputSession SearchTextInput`), a scrolling checkbox list, and OK/Cancel buttons.
- `BooleanCellRenderer` draws the check box and the check mark; `Components/DrawnCheckBox` has `IsChecked` and
  `IsThreeState`; `DrawnCheckState` is `Unchecked/Checked/Indeterminate`.
- `CellRenderContext.IsSelected` exists. `KumikoKeyEventArgs.HasCommand` is Control or Meta. Ctrl+A calls
  `SelectAll()`.
- `KumikoStrings` holds every user-visible string (`FilterSelectAll`, `FilterSearchPlaceholder`, ...).

Unmerged work this design assumes:

- **#36 (PR #212)**: `PopupLayout.Place(anchor, width, height, viewport, ...)` flips and clamps popups to the
  viewport. The chooser uses it instead of its own placement.
- **#38 (PR #217)**: `KumikoColumn.IsThreeState`; `BooleanCellRenderer` draws a null value as a filled dash with
  `GlyphDrawing.FillDash` and `PaintCache.CheckIndeterminate`.
- **#44 (PR #211)**: `KumikoTsvFormatter`, Ctrl/Cmd+C. The formatter must skip the selection column (section 9).
- **#100 (open)**: `ContextRequested(row item, column, position, trigger)`. The shape used here is an
  assumption; section 5.6 states what the chooser needs from it.
- **#98 (open)**: `DisplayIndex` and `GridLayoutState`. Section 5.5 states what the chooser needs from it.

## 4. Concepts and terminology

- **Chooser entry**: one row of the chooser, for one `KumikoColumn` whose `ShowInChooser` is true. The label is
  `KumikoColumn.Header`, or `PropertyName` when the header is empty.
- **Pane**: left-frozen, scrollable or right-frozen columns (`ColumnFreezeMode`). The renderer draws them in that
  order, each in `Columns` order.
- **Selection column**: a `KumikoColumn` whose `ColumnType` is `KumikoColumnType.Selection`.
- **Check-all state**: `bool?` over the data rows of the current view: `false` none selected, `true` all
  selected, `null` some selected.
- **Data row**: a view row that is not a group header or group summary row (`KumikoSource.IsNonDataRow`).

## 5. Column chooser

### 5.1 Behavior

- Opens as a popup panel. Default size 260 x 340 logical pixels, clamped to the viewport; the list scrolls.
- Content, top to bottom: a title ("Columns") and a close button; a search box; the scrolling entry list; a footer
  with **Show all** and **Reset**.
- Each entry: a drag grip on the left, a checkbox, the header text. A hidden column is not "disabled", it is
  just unchecked.
- Search filters entries by case-insensitive substring of the label (using `KumikoSource.Comparer.TextComparer`).
  While a search is active, drag reorder is disabled (the drop slot would be ambiguous) and the grips are drawn
  dimmed.
- Toggling a checkbox applies immediately and the grid behind redraws. There is no OK/Cancel: the popup is a
  live panel, and Reset is the undo. A tap outside closes it (the `PopupManager` rule that already closes the
  filter popup); Escape closes it.
- **Show all** sets `IsVisible` true on every entry whose `ShowInChooser` is true.
- **Reset** restores the order and visibility captured as the default layout (section 5.5), not the state at the
  time the chooser opened. It is dimmed when the layout already equals the default.
- The last visible column cannot be hidden: its checkbox is drawn disabled, and its accessibility description
  says why (`ColumnChooserLastColumn`). Programmatic `SetColumnVisible(column, false)` on the last visible
  column returns false.
- Hiding a column that has an active sort, filter or group keeps that state active; the entry shows a small
  sort/filter badge so the user can tell why rows look ordered or missing. The state is cleared from the
  filter popup or the app, not from here (Q3).
- Hiding the column of the current cell moves the current cell to the nearest visible column in display order,
  right first, then left. An open edit on a hidden column is committed first, through the same path as a click
  on another cell.

### 5.2 Reorder

- Drag a grip: the row follows the pointer, a 2 px insertion line shows the drop slot. Touch drags the grip with
  no long-press (the grip is the affordance, so scrolling the list by dragging elsewhere still works).
- Only same-pane moves are allowed: an entry cannot be dropped across the boundary of its freeze pane
  (`FreezeMode` is a column property, not a position). This is the same rule #98 adopts for header drag.
  Entries show a small pane marker (a pin glyph for `Left`/`Right`) so the rule is visible.
- Hidden columns are reordered too. The list shows `Columns` order, not display order, and a hidden column
  keeps its slot. The move calls `KumikoSource.ReorderColumn(from, to)` with indices into `Columns`.
- Keyboard: Alt+Up / Alt+Down moves the focused entry one slot within its pane.
- A column with `AllowReorder` false is not draggable and has no grip; Alt+Up/Down is ignored for it. Other
  entries can still be dropped around it.
- The selection column has `ShowInChooser` false (it is chrome, section 6.1), so it never appears.

### 5.3 API (Core)

On `KumikoColumn` (`src/KumikoUI.Core/Models/KumikoColumn.cs`):

```csharp
/// <summary>Whether the column is listed in the column chooser. Default true.</summary>
public bool ShowInChooser { get; set; } = true;

/// <summary>Whether the user may hide the column from the chooser. Default true.</summary>
public bool AllowHide { get; set; } = true;
```

On `KumikoSource`:

```csharp
/// <summary>Raised when a column's visibility or position changed (not its width or sort state).</summary>
public event EventHandler<KumikoColumnsChangedEventArgs>? ColumnsChanged;

/// <summary>Shows or hides a column. Returns false when the change is refused (last visible column, or
/// AllowHide is false while hiding). Raises ColumnsChanged and a Redraw ViewChanged.</summary>
public bool SetColumnVisible(KumikoColumn column, bool visible);

/// <summary>Number of columns with IsVisible true.</summary>
public int VisibleColumnCount { get; }

/// <summary>Captures the current order and visibility as the layout Reset restores.</summary>
public void MarkDefaultColumnLayout();

/// <summary>Restores the layout captured by MarkDefaultColumnLayout (or by SetColumns). Raises
/// ColumnsChanged once with Kind Reset.</summary>
public void ResetColumnLayout();
```

```csharp
public enum KumikoColumnsChangeKind { Visibility, Order, Reset }

public sealed class KumikoColumnsChangedEventArgs : EventArgs
{
    public KumikoColumnsChangeKind Kind { get; }
    public KumikoColumn? Column { get; }   // null for Reset
    public int OldIndex { get; }           // Order only, else -1
    public int NewIndex { get; }           // Order only, else -1
}
```

Making `IsVisible` itself notify (rather than only adding `SetColumnVisible`) is better for bindings and for
apps that already assign `column.IsVisible`. Section 5.4 does that: the `IsVisible` and `FreezeMode` setters
call back into the owning source. `SetColumnVisible` stays as the validating entry point the chooser uses.

On the input layer (`src/KumikoUI.Core/Input/KumikoInputController.cs`), mirroring the filter popup:

```csharp
public bool IsColumnChooserOpen { get; }
public void ShowColumnChooser(KumikoRect? anchor = null);   // null = the grid's top-right corner
public void CloseColumnChooser();
public event Action? ColumnChooserOpened;
public event Action? ColumnChooserClosed;
```

Opening a chooser closes the filter popup and vice versa (one non-modal popup at a time, as today). While it is
open, `KeyboardFocusRequested` fires so the hosts show the software keyboard for the search box, and the hosts
run their cursor-blink timer through `ColumnChooserOpened/Closed` exactly as they do for
`FilterPopupOpened/Closed`.

New component `src/KumikoUI.Core/Components/DrawnColumnChooser.cs` (a public `DrawnComponent`, like
`DrawnFilterPopup`, so apps can embed or restyle it):

```csharp
public class DrawnColumnChooser : DrawnComponent
{
    public DrawnColumnChooser(KumikoSource source);
    public ITextInputSession SearchTextInput { get; }
    public string SearchText { get; set; }
    public void ApplyTheme(KumikoStyle style);
    public event Action? CloseRequested;
    public event Action? KeyboardFocusRequested;
    // OnDraw, HitTest, OnPointer*, OnScroll, OnKeyDown as DrawnFilterPopup
}
```

Implementation notes:

- It reads `source.Columns` live and subscribes to `source.ColumnsChanged` to refresh the list, so changes from
  the app while it is open show up.
- The search box is the same `DrawnTextBox` plumbing `DrawnFilterPopup` uses (it exposes an
  `ITextInputSession`, #39). Reuse, do not copy: extract the shared "search box + scrolling checkbox list"
  pieces into an internal base (`DrawnCheckListPanel`) as the first commit of stage 2, then derive both popups.
  If that refactor is judged too risky for #49's tests, copy the drawing code and leave the dedupe to a
  follow-up (Q8).
- Placement goes through `PopupLayout.Place` (PR #212). Before it merges, stage 2 uses the same clamp logic
  inline and switches when #212 lands.
- Drawing is allocation free in steady state (renderer rule, #68): fonts and paints come from `KumikoStyle`
  and `PaintCache`; entry text is the column's header string, no per-frame formatting.

### 5.4 Source notifications for column changes

`KumikoColumn` raises nothing today, so a chooser (or any app code) that changes `IsVisible` does not update the
grid until something else repaints it. Fix it at the source, in stage 1:

- `KumikoColumn` gets an internal `Owner` (`KumikoSource?`), set by `SetColumns` and cleared for columns that
  are removed. The setters of `IsVisible` and `FreezeMode` call `Owner?.OnColumnPropertyChanged(this, kind)`.
- `KumikoSource.OnColumnPropertyChanged` invalidates column-derived caches (layout widths, the per-column text
  cache `TextCache`), raises `ColumnsChanged` (`Visibility`), and requests a `Redraw` `ViewChanged`. A
  visibility change is not structural: no row moves.
- `KumikoSource.ReorderColumn` also raises `ColumnsChanged` (`Order`).
- Inside `BeginUpdate()/EndUpdate()` the events coalesce like every other view change.
- `KumikoSelectionModel.OnViewChanged` treats a hidden `_currentColumn` like a removed one: it moves the current
  cell to the nearest visible column (section 5.1) instead of leaving it on a column the user cannot see. This
  also fixes the latent hide case that #108 notes.

This stage is useful on its own and is a prerequisite for #98 and #108, which both need to know when columns
change. It is small enough to land before the UI.

### 5.5 Persistence hook (#98)

The chooser changes two things a layout state must hold: visibility and order. The hook is deliberately thin:

- `KumikoSource.ColumnsChanged` is the one event an app (or the future `GridLayoutState` auto-save) subscribes
  to. Header drag reorder (#98), chooser toggles, chooser drag and `ResetColumnLayout` all raise it.
- `Reset` restores `MarkDefaultColumnLayout()`. `SetColumns` calls `MarkDefaultColumnLayout()` implicitly, so
  the default is "the columns as the app defined them". After an app restores a saved layout it must not
  re-mark the default, or Reset would return to the saved state instead of the shipped one; #98's import
  should apply a saved layout without touching the default. This is the one coupling to call out in the #98
  review.
- Identity: layout entries must be keyed by a stable column key. `KumikoColumn.Id` is a random GUID fragment by
  default, so it is not stable. #98 should key by `Id` and document that apps persisting layouts must set `Id`
  (or that `Id` defaults to `PropertyName` when one is set). Flagged as Q2. The chooser itself holds column
  references and does not depend on the answer.
- Width, sort, filter and group are #98's business; the chooser neither reads nor writes them.

### 5.6 Entry points

1. **Host API.** Both hosts add `ShowColumnChooser()`, `ShowColumnChooser(Rect anchor)` (the host rectangle type,
   mapped to `KumikoRect`), `CloseColumnChooser()`, read-only `IsColumnChooserOpen`, and events
   `ColumnChooserOpened`/`ColumnChooserClosed`. MAUI also exposes `ShowColumnChooserCommand` (`ICommand`, no
   parameter) for MVVM buttons, matching the `SelectionChangedCommand` pattern in
   `KumikoUI.Maui/KumikoDataGrid.Selection.cs`. Both hosts own a Core `KumikoInputController`, so each member is
   a one-line forwarder.
2. **Header context request (#100).** When #100 lands, a right-click, long-press or Menu key on a header raises
   `ContextRequested` with the column and a position. This spec asks two things of it:
   - the args say whether the request is on a header (the issue text has `column` and `position`; a header
     request has no row item) and carry a `Handled` flag;
   - the grid property `ColumnChooserOnHeaderContext` (bool, default **false**) opens the chooser anchored at the
     header when the request is a header request and no handler set `Handled`. It stays false in 1.x so that
     adding the feature changes no app behavior. The sample sets it true. Q1 asks whether to flip the default.
   Until #100 lands, `ShowColumnChooser()` is the only entry point.
3. **Keyboard.** `ColumnChooserShortcut` (a Core `KumikoShortcut?`, default **Ctrl+Alt+C**; `null` disables it)
   handled in `KumikoInputController` before the cell handlers. Ctrl+Shift+C is avoided (browser dev tools on
   Uno WebAssembly) and Ctrl+C is the clipboard (#44). The shortcut works whenever the grid has focus and no
   edit is open. The context-menu key route is #100's.

`KumikoShortcut` does not exist yet. If #100 or #44 introduce a gesture type, use it; otherwise add a small
`readonly record struct KumikoShortcut(KumikoKey Key, InputModifiers Modifiers)` in `Input/`. Either is a Core
type, so both hosts get the same default.

### 5.7 Keyboard and focus inside the chooser

- Opening focuses the search box (the same as the filter popup). Typing filters.
- Down / Up from the search box move into the list; Up from the first entry returns to the search box.
- In the list: Up/Down/Home/End/PageUp/PageDown move the focus ring; Space toggles the focused entry;
  Alt+Up/Alt+Down reorder it.
- Tab cycles search box, list, Show all, Reset, Close, then back (the `DrawnFocusManager` cycle already used for
  popups).
- Escape clears a non-empty search first, then closes. Enter on a footer button activates it; Enter in the list
  toggles like Space.
- On close, focus returns to the grid's current cell.

### 5.8 Rendering

- Panel: `KumikoStyle` background, border and shadow as the filter popup, header-height title row, row height
  equal to the grid's row height (touch-sized targets: an entry's hit rectangle is the whole row, not the
  checkbox).
- Checkbox glyph: the shared check box drawing from `BooleanCellRenderer`, extracted in stage 5 (section 6.5)
  into an internal `CheckBoxGlyph.Draw(ctx, rect, DrawnCheckState, paints)`. The chooser only uses `Unchecked`
  and `Checked`. If stage 2 lands before stage 5, it draws the box with the same paints inline and switches
  later.
- Drag: the dragged entry is drawn at 50% alpha under the pointer; the insertion line uses
  `KumikoStyle.AccentColor`.
- Theme: `ApplyTheme(KumikoStyle)` like `DrawnFilterPopup`; dark and light come from the style.

### 5.9 Accessibility (#77 / #78)

- The panel is a dialog-like group labeled with `ColumnChooserTitle`. Each entry is a checkable item whose name
  is the column label and whose value is checked or unchecked, with its position ("3 of 12") when the platform
  supports it.
- Toggle and reorder announce through `KumikoAnnouncements` (the type from #77/#78): `ColumnShown` ("Salary
  shown"), `ColumnHidden`, `ColumnMoved` ("Salary moved to position 3 of 12"), `ColumnLayoutReset`.
- The chooser is reachable without a pointer (shortcut, command, context key) and fully operable from the
  keyboard (section 5.7). Reordering has a non-drag alternative (Alt+Up/Down), as WCAG 2.5.7 requires.

## 6. Selection column

### 6.1 Concept and defaults

```csharp
/// <summary>A narrow column of row check boxes bound to the grid's KumikoSelectionModel, with a
/// tri-state check-all box in its header.</summary>
public class KumikoSelectionColumn : KumikoColumn
{
    public KumikoSelectionColumn();   // applies the defaults below
}
```

It is a `KumikoColumn` subclass (the class is not sealed, and `ColumnType`-based dispatch already drives
rendering and editing) rather than a flag on every column, so XAML, code and `SetColumns` handle it like any
other column. A new enum member `KumikoColumnType.Selection` is appended to `KumikoColumnType` (appended, so
existing numeric values do not change).

Constructor defaults:

| Property | Value | Why |
|---|---|---|
| `ColumnType` | `Selection` | dispatches to `SelectionCellRenderer` and the header check-all |
| `PropertyName` | empty | the cell reads selection state, not an item property; an empty path already means "the item itself" in `KumikoValuePath` |
| `Header` | empty | the header draws the check-all box; the accessibility name is `KumikoStrings.SelectAllRows` |
| `Width` / `MinWidth` / `MaxWidth` | 44 | touch-sized target; `SizeMode` is `Fixed` |
| `FreezeMode` | `Left` | the box stays put while the rest scrolls ("frozen by default") |
| `IsReadOnly` | true | the edit pipeline never opens an editor on it; Tab skips it (`AllowTabStop` rules) |
| `AllowSorting`, `AllowFiltering`, `AllowResize`, `AllowReorder` | false | |
| `AllowHide`, `ShowInChooser` | false | it is chrome; the app removes it by removing the column, or via host `ShowSelectionColumn` |
| `TextAlignment` | center | |

The column is excluded from copy (#44), export (#105), summaries and the filter popup (section 9).

Two ways to get one:

```csharp
// Code, either host (also valid in XAML where columns are declared)
grid.Columns.Insert(0, new KumikoSelectionColumn());

// Convenience on the host: adds/removes an owned KumikoSelectionColumn at index 0
grid.ShowSelectionColumn = true;
```

`ShowSelectionColumn` (bindable on MAUI, a dependency property on Uno, default false) inserts one
`KumikoSelectionColumn` the host owns at index 0 of the columns and removes it when set false. If the app
already declared a `KumikoSelectionColumn` the property does nothing (no duplicate). It follows `SetColumns`
replacements by re-inserting.

### 6.2 Cell behavior by `KumikoSelectionMode`

`SelectionMode` is the host-exposed `KumikoSelectionModel.Mode`.

| Mode | Cell box | Click on the box | Header check-all |
|---|---|---|---|
| `None` | drawn disabled (dimmed), not hittable | nothing | hidden |
| `Single` | check box, at most one checked | checking a row replaces the selection (like `Select`); unchecking clears | hidden (a single selection has no "all") |
| `Multiple` | check box | toggles that row, others untouched | shown |
| `Extended` | check box | toggles that row, others untouched (the point of the column: additive selection without Ctrl) | shown |

Clicking the box never triggers the `Extended` "plain click replaces the selection" rule, in any mode.

- **Shift-click** in `Multiple`/`Extended`: apply the new state of the clicked row to every data row between the
  range anchor and the clicked row (the Gmail/GitHub list convention): shift-checking checks the range,
  shift-unchecking unchecks it. Group rows in the range are skipped. The anchor then moves to the clicked row.
- **Ctrl-click** on the box behaves exactly like a click (it is already additive).
- **Keyboard**: Space on the current cell when its column is a selection column toggles that row (Shift+Space
  range-toggles from the anchor, as a click does). Left/Right arrows can land on the column; Tab skips it. This
  matches Boolean cells ("Space toggles it").
- **Pointer pipeline**: on pointer-up over a `HitRegion.Cell` whose column is `Selection`, the input controller
  commits any open edit, moves the current cell to that row (keeping its column unless it is the selection
  column), then calls the new `KumikoSelectionModel.HandleCheckBoxToggle(row, shift)`. It never starts an edit
  and never starts a pan or row drag from the box.
- **Touch**: same as pointer; the whole 44 px cell is the hit target, not only the drawn box.
- **Rest of the row**: unchanged by default. In `Extended`, tapping another cell still replaces the selection,
  and the boxes follow. Apps that want checkbox-only selection set `SelectOnRowClick = false` (section 6.4); a
  tap elsewhere then only moves the current cell.

### 6.3 Header check-all

- State: `KumikoSelectionModel.CheckAllState` returns `bool?` computed in O(1): `SelectedItems.Count == 0` is
  `false`; `SelectedItems.Count == Source.DataRowCount` (above 0) is `true`; otherwise `null` (indeterminate).
  The model already removes items that leave the view, so every selected item is in the current view and the
  count comparison is exact.
- Scope: the **current view**, i.e. the rows left after filters. The header never selects rows a filter hides.
  Rows inside collapsed groups follow whatever `SelectAll` does for the view (Q4). Under client paging (#107) the
  scope is the current page (section 9).
- Click: `false` becomes select all (`SelectAll()`); `true` clears the selection (`SetSelectedItems` with an empty
  set; the current cell is kept); `null` (indeterminate) becomes select all, as Gmail, GitHub and most table
  UIs do. The click does not sort and never opens the filter popup.
- Rendering reuses #38: the header draws `DrawnCheckState.Unchecked/Checked/Indeterminate` through the shared
  `CheckBoxGlyph`, where `Indeterminate` is the filled dash (`GlyphDrawing.FillDash` with
  `PaintCache.CheckIndeterminate`, PR #217). This is the one reason stage 5 depends on #217. Per-row cells use
  `Unchecked/Checked` only.
- Cost: the renderer draws the header every frame; computing the state is two integer reads. No allocation.
- `Single` and `None` modes hide the header box (section 6.2). Switching `SelectionMode` repaints.
- Keyboard: Ctrl+A already selects all and works in every mode that allows it. When a header becomes focusable
  (#98/#77), Space toggles check-all.

### 6.4 Core selection model additions

All on `KumikoSelectionModel` (`src/KumikoUI.Core/Models/KumikoSelectionModel.cs`); none changes existing behavior.

```csharp
/// <summary>Whether every, some or none of the view's data rows are selected.</summary>
public bool? CheckAllState { get; }

/// <summary>Whether a tap on a row outside a selection column's check box selects it. Default true.</summary>
public bool SelectOnRowClick { get; set; } = true;

/// <summary>Selects or deselects the data rows between two visible rows, inclusive, in one change.
/// Group rows are skipped. Raises one SelectionChanged.</summary>
public void SetRangeSelected(int fromRow, int toRow, bool selected);

/// <summary>The click path of a selection column's check box (section 6.2).</summary>
public void HandleCheckBoxToggle(int row, bool shift);
```

On `KumikoSource`: `public int DataRowCount { get; }`, maintained by `RebuildCore` (view rows minus group
header and summary rows). It is also what #104's and #107's footers want for "N rows".

`HandleCheckBoxToggle(row, shift)`: ignore non-data rows and `None`; in `Single`, `Select`/`Deselect` as in the
table; otherwise compute `selected = !IsRowSelected(row)`; if `shift` and an anchor exists, call
`SetRangeSelected(anchorRow, row, selected)`, else toggle one row; move the anchor and the current cell to `row`.
It raises one `SelectionChanged`, so `KumikoSelectionBinder` and the host `SelectedItems` stay in sync with no new
wiring. `SelectOnRowClick = false` makes `HandleRowSelection` move only the current cell.

### 6.5 Rendering

- `SelectionCellRenderer : ICellRenderer` (`Rendering/`), registered for `KumikoColumnType.Selection` where
  `BooleanCellRenderer` is registered. It draws only a box from `cell.IsSelected`. No cell text, no text cache.
- Extract `CheckBoxGlyph.Draw(IDrawingContext, KumikoRect box, DrawnCheckState, PaintCache)` from
  `BooleanCellRenderer` so the Boolean column, the selection column, the header and the chooser share one
  drawing path. After #217 the Boolean renderer's dash branch moves into it unchanged.
- Row highlight: selected rows already get the selection background across the row, the selection column
  included, so the box sits on the highlight. Box colors are the existing `CheckBoxBorder`, `CheckMark` and
  `CheckIndeterminate` paints, so themes need no new fields.
- Header: `KumikoRenderer` draws the header cell background and border as usual and, for a `Selection` column,
  draws the `CheckBoxGlyph` centered instead of text, sort glyph and filter icon.
- Frozen panes: it is a left-frozen column like any other; pane drawing, hit testing (`KumikoHitTester`) and
  clipping already handle `FreezeMode.Left` columns. If the app sets another freeze mode, that is honored.
- Group header and summary rows span all columns, so nothing is drawn per group in v1 (stage 7).
- No steady-state allocation (`RenderAllocationTests`): the renderer is stateless and reads two booleans.

### 6.6 Interactions

- **Editing.** The column is read-only and never opens an editor. Clicking its box commits an open edit first
  (so a pending value is not lost; if validation fails the edit stays open and the toggle is not applied, the
  same as clicking another cell).
- **Sorting, filtering, grouping.** `AllowSorting`/`AllowFiltering` are false; the column cannot be a group key
  (it has no property). Selection follows items through sort/filter/group (#30), and check-all state is
  recomputed from the model.
- **Cell selection (#108).** The selection column is outside cell ranges: drag-select and Shift+arrows skip it,
  the select-all corner selects data columns only, and the box always toggles the row regardless of
  `SelectionUnit`. #108's display-column helpers should ignore columns whose `ColumnType` is `Selection`.
- **Row reorder, action buttons.** Unaffected. The drag handle remains its own hit region.
- **Incremental loading (#104) and paging (#107).** Rows that arrive are unselected, so check-all becomes `null`
  or `false`; this is correct for "select all loaded". The header selects what is loaded, never "all on the
  server" (Q6). Under client paging the header covers the current page, and selection does not survive a page
  change by default (see #107).
- **Selection binder and hosts.** `SelectedItem`, `SelectedItems` and `SelectionChangedCommand` need no change:
  `HandleCheckBoxToggle` goes through `SelectionChanged`.
- **Chooser.** The column is not listed (`ShowInChooser` false) and cannot be hidden by the user.

### 6.7 Accessibility

- Each data-row box is a checkable element named by `KumikoStrings.SelectRow` with the row's state; the
  row/cell semantics from #77/#78 keep reporting the row's selected state, so the box and the row agree.
- The header box is named `SelectAllRows`, with value checked, unchecked or mixed. Toggling announces
  `AllRowsSelected` ("All 120 rows selected") or `SelectionCleared` through `KumikoAnnouncements`; individual
  toggles rely on the platform's selected-state change, not an extra announcement.
- The touch target is at least 44 logical pixels; state is conveyed by the mark and the dash (distinct shapes),
  not by color alone.

### 6.8 Strings (`KumikoStrings`)

New `init` properties with English defaults, following the existing pattern: `ColumnChooserTitle` ("Columns"),
`ColumnChooserSearchPlaceholder` ("Search columns..."), `ColumnChooserShowAll` ("Show all"),
`ColumnChooserReset` ("Reset"), `ColumnChooserClose` ("Close"), `ColumnChooserLastColumn` ("At least one column
must stay visible"), `ColumnShown`, `ColumnHidden`, `ColumnMoved`, `ColumnLayoutReset`, `SelectAllRows`
("Select all rows"), `SelectRow` ("Select row"), `AllRowsSelected`, `SelectionCleared`. The announcement
strings take composite-format placeholders consumed with the source's `Culture`.

## 7. Host surface (MAUI and Uno, same names and defaults)

| Member | Type | Default |
|---|---|---|
| `ShowColumnChooser()`, `ShowColumnChooser(Rect)`, `CloseColumnChooser()` | methods | |
| `IsColumnChooserOpen` | read-only bool | false |
| `ColumnChooserOpened`, `ColumnChooserClosed` | events | |
| `ShowColumnChooserCommand` (MAUI) | `ICommand` | |
| `ColumnChooserOnHeaderContext` | bool | false |
| `ColumnChooserShortcut` | `KumikoShortcut?` | Ctrl+Alt+C |
| `ColumnsChanged` | event (forwards `KumikoSource.ColumnsChanged`) | |
| `ShowSelectionColumn` | bool | false |
| `SelectOnRowClick` | bool | true |

Both hosts forward each member to Core in a few lines (`KumikoUI.Maui/KumikoDataGrid.Options.cs`,
`KumikoUI.Uno/KumikoDataGrid.Options.cs`), as they do for `ColumnReordered`. Cursor-blink and software-keyboard
handling for the chooser's search box reuses the filter popup's wiring (`OnFilterPopupOpened` becomes a handler
for both popups' open events).

## 8. Performance

- Chooser: O(columns) draw per frame while open, one popup, no allocation in steady state. A grid with 200
  columns scrolls the list; entries outside the visible range are not drawn.
- Visibility changes invalidate column layout and the per-column text cache for that column only; no rows
  rebuild. Hiding a column with an active sort or filter does not re-sort or re-filter.
- Selection column: header state is O(1); a row box is one lookup in the `int` row set, the same per-cell cost
  the row highlight already pays.
- Shift-click range on 100K rows is O(range) with one `SelectionChanged`.

## 9. Compatibility, AOT, trimming and exclusions

- Additive API; no default changes. Appending `KumikoColumnType.Selection` keeps existing enum values; the repo's
  own switches over `KumikoColumnType` (editor factory, renderer dispatch) are updated in stage 5.
- No reflection, no new dependencies, no `dynamic`. `KumikoSelectionColumn` has an empty `PropertyName`, so
  `KumikoAccessorRegistry` is not consulted.
- **Paging (#107).** The header check-all covers the rows of the current page; changing page drops off-page
  items from the selection, as any view change that removes rows does today.
- **Exclusions.** `KumikoTsvFormatter` (#44), export (#105), summaries and `GetUniqueColumnValues` skip columns
  whose `ColumnType` is `Selection`; the filter popup never opens for it (`AllowFiltering` false). An internal
  `KumikoColumn.IsDataColumn` helper (false only for `Selection`) states the rule once.

## 10. Test plan

Core unit tests (`tests/KumikoUI.Core.Tests`), no UI:

- `KumikoSource` column notifications: `IsVisible` and `FreezeMode` setters raise `ColumnsChanged` and a
  `Redraw` view change, batched by `BeginUpdate`; `ReorderColumn` raises `Order`; `SetColumnVisible` refuses the
  last visible column and `AllowHide == false`; `ResetColumnLayout` restores order and visibility and raises one
  event; `SetColumns` marks the default.
- `KumikoSelectionModel`: the current cell moves off a hidden column to the nearest visible one in display
  order; `CheckAllState` is `false/null/true` across select, deselect, filter, add/remove items, grouping (group
  rows not counted), `None`/`Single`; `SetRangeSelected` skips group rows and raises one change;
  `HandleCheckBoxToggle` for each mode, shift ranges both ways, anchor movement, no replacement in `Extended`;
  `SelectOnRowClick`.
- Chooser logic (`DrawnColumnChooser` driven with synthetic `KumikoPointerEventArgs`/`KumikoKeyEventArgs`, like
  the filter popup tests): toggle by tap and Space, the last-column guard, search filter and its effect on drag,
  drag reorder within a pane, refused across panes, Alt+Up/Down, Reset and Show all, Escape order (search then
  close), keyboard-only operation, theme application.
- Input: `ShowColumnChooser` closes the filter popup and vice versa; outside tap closes; shortcut opens;
  `ColumnChooserOnHeaderContext` opens on an unhandled header context request (after #100) and not on cell
  requests.
- Selection column: header click states (none to all, all to none, indeterminate to all); a click does not sort
  or filter; box click commits an open edit and never starts one; touch tap; Space toggles; hit testing in the
  left-frozen pane with horizontal scroll; excluded from TSV (#44) and export.
- Rendering (fake `IDrawingContext`, as `TriStateAndProgressRenderingTests` does): header dash for
  indeterminate, check for all; allocation test with the column present and the chooser open; no draw calls for
  a hidden column.
- Accessibility: announcements for toggle, reorder, reset, check-all (with the `KumikoAnnouncements` test
  recorder); strings localizable through `KumikoStrings`.
- Host tests (MAUI property mapping, Uno helpers, as #84 sets up): the new members forward to Core;
  `ShowSelectionColumn` inserts once, survives `SetColumns`, and is not duplicated by a declared column.
- Samples: a chooser button and context-menu wiring on both samples; a grid with `ShowSelectionColumn`.

## 11. Staged implementation issues

1. **Column change notifications on `KumikoSource` and `KumikoColumn`**: `Owner`, `IsVisible`/`FreezeMode`
   notification, `ColumnsChanged`, `SetColumnVisible`, `VisibleColumnCount`, `MarkDefaultColumnLayout`,
   `ResetColumnLayout`; the selection model leaves a hidden current column. Tests. (Prerequisite for #98 and
   #108.)
2. **`DrawnColumnChooser` component**: list, toggles, search, reset, show all, drag and keyboard reorder, theme,
   strings, accessibility metadata; `KumikoColumn.ShowInChooser`/`AllowHide`; optional `DrawnCheckListPanel`
   dedupe with the filter popup. Tests.
3. **Chooser entry points and host surface**: `KumikoInputController.ShowColumnChooser/Close`, events,
   `ColumnChooserShortcut`, both hosts' API and MAUI command, `ColumnChooserOnHeaderContext` wiring (after #100),
   cursor-blink and keyboard-focus reuse, samples.
4. **Selection model additions**: `CheckAllState`, `DataRowCount`, `SetRangeSelected`, `HandleCheckBoxToggle`,
   `SelectOnRowClick`. Tests.
5. **`KumikoSelectionColumn`, renderer and header check-all**: `KumikoColumnType.Selection`,
   `SelectionCellRenderer`, `CheckBoxGlyph` extraction (shared with Boolean after #217), header drawing, input
   routing (box click, Space, header click), exclusions from copy/export/summaries/filter. Tests. Blocked by #217
   for the dash glyph.
6. **Host surface for the selection column**: `ShowSelectionColumn`, `SelectOnRowClick`, accessibility roles
   and announcements on both hosts (#77/#78), docs and sample.
7. **Optional: group header check boxes**: a tri-state box in group header rows over the group's data rows;
   needs an items-in-group query on `KumikoSource`. Defer unless requested.

Stages 1 to 3 and 4 to 6 are independent tracks. #100 gates only the context-request part of stage 3.

## 12. Open questions

- **Q1.** Should `ColumnChooserOnHeaderContext` default to true once #100 ships (header right-click sorted
  before, so apps are not relying on it)? Proposed: false in 1.x, flip in 2.0.
- **Q2.** `KumikoColumn.Id` is a random GUID fragment. Should it default to `PropertyName` (makes persisted
  layouts stable) or should #98 require apps to set `Id`?
- **Q3.** Should hiding a column with an active sort/filter/group clear that state, ask, or keep it with a badge
  (proposed: keep with a badge)?
- **Q4.** Should header check-all count rows inside collapsed groups? `SelectAll` follows the rows in the view;
  Excel-like users may expect collapsed rows included.
- **Q5.** Should the selection retain items a filter hides? `KumikoSelectionModel` drops them (#30). Out of scope
  here, but it decides what "check all" means after filter changes.
- **Q6.** Is "select all N on the server" needed for a selection column over an incremental or paged source
  (#104/#107)? Proposed: no; expose `CheckAllState` and let the app show a banner.
- **Q7.** Default keyboard shortcut for the chooser: Ctrl+Alt+C is AltGr on some European layouts. Is a default
  needed at all, or should the shortcut be opt-in?
- **Q8.** Refactor `DrawnFilterPopup` and the chooser onto a shared `DrawnCheckListPanel` now, or copy and dedupe
  later to keep #49's tests untouched?
- **Q9.** Should `KumikoSelectionColumn` be a subclass (proposed) or only a `KumikoColumnType.Selection` value
  with defaults applied by a factory? The enum value is the single source of truth for rendering either way.

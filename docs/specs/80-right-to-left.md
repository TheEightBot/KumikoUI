# Spec: right-to-left layout mirroring (FlowDirection)

**Issue:** #80 (plan key M2, Epic M: Localization) · **Status:** proposed, awaiting review · **Target:** 1.x. For 1.0, only a host fix and a documented limitation (stage 0).

**Findings:**
- CORE-32: layout is left-anchored everywhere and frozen columns use "Left" semantics.
- MAUI-19: `FlowDirection` is ignored.
- `04-uno.md` §(e): an RTL ancestor mirrors the whole Uno canvas, so text is drawn backwards.

**Related work:**

| Work | State | Relationship |
|---|---|---|
| #23 text layout and ellipsis | PR #199, open | Grapheme-safe ellipsis, `KumikoTextWrap`, and a cross-frame measurement cache. The cache needs a direction in its key, and the ellipsis must go at the logical end. |
| #74 per-glyph font fallback | Branch `feat/74-font-fallback` not pushed when this spec was written | Splits text into typeface runs. Bidi level runs must be computed first and then split by typeface. |
| #111 HarfBuzz shaping | Open spec issue | Shapes Arabic and other complex scripts. Related to this spec but separate: bidi ordering is a prerequisite for usable RTL *text*, and shaping is a prerequisite for usable *Arabic*. |
| #36 popup placement | PR #212, open | Adds `PopupLayout.Place`/`Clamp` (flip and clamp). It works unchanged in logical coordinates. |
| #56 `KumikoScroller` geometry | PR #198, open | `GetColumnX`, `ScrollToColumn`, `HorizontalOffset` and `Scrolled` are defined as logical. |
| #60 pointer semantics | In progress, not pushed | Takes Uno pointer and size from `_canvasView`, normalizes wheel deltas, and maps Shift+wheel to horizontal. RTL negation is applied in Core after that normalization. |

---

## 1. Problem

Every horizontal coordinate in KumikoUI is measured from the left edge of the viewport:
- `KumikoLayoutEngine.PopulateVisibleFrozenColumns` starts at `x = 0`. `PopulateVisibleRightFrozenColumns` anchors to `viewportWidth`.
- `KumikoHitTester.HitTestCore` assumes the same.
- `KumikoRenderer.Render` draws its three passes in that frame: scrollable, then left-frozen, then right-frozen.
- Header text is drawn with a hard-coded `KumikoTextAlignment.Left`. Sort arrows and filter icons sit at the right end of the header (`DrawSortIndicators`, `DrawFilterIcons`).
- Group indent grows from the left (`DrawGroupHeaderRow`), and the drag handle defaults to `DragHandlePosition.Left`.

Arabic, Hebrew, Persian and Urdu apps therefore get an LTR grid, and nothing lets them change it.

The host behavior also depends on the platform:

| Host / platform | What an RTL ancestor does today |
|---|---|
| Uno (all heads), MAUI on Windows | WinUI applies a mirror transform to the `SKXamlCanvas`. All drawing is flipped, text included, so text reads backwards. Pointer coordinates are taken in the control's mirrored space. |
| MAUI on iOS, Mac Catalyst, Android | The platform does not mirror custom drawing. The grid stays LTR. |

A separate problem: `SkiaDrawingContext.DrawText` and `DrawTextInRect` call `SKCanvas.DrawText(string, …)`. That call puts glyphs in logical order and applies no bidi reordering and no shaping. As a result, Hebrew cell text is drawn reversed and Arabic is drawn reversed and unjoined **in any grid direction**. Mirroring the layout does not fix that. Section 6 covers it and staged issue S5 tracks it.

## 2. Goals and non-goals

**Goals**

1. Hosts gain a grid-level flow direction that follows the platform property: `VisualElement.FlowDirection` on MAUI and `FrameworkElement.FlowDirection` on Uno. When it is RTL, the grid mirrors as a whole:
   - Column order runs right to left, and the start-frozen pane sits on the right.
   - Headers, glyph positions, resize grips, the drag handle, group indent and the group panel all mirror.
   - Summaries, editors and popups mirror too.
2. Input agrees with the drawing:
   - Hit testing, resize, reorder, pan, wheel and the keyboard all work on the mirrored layout.
   - Arrow keys are visual, as on the platforms. Tab, Home and End are logical.
3. Text is never mirrored:
   - Glyphs are drawn upright.
   - Alignment is flow-relative.
   - Each cell has a bidi paragraph direction, and the ellipsis goes at the logical end.
4. The direction can change at runtime without leaking state, and an open editor survives the change.
5. LTR is unchanged:
   - Every existing test and golden image passes as is.
   - The public API changes are additive only.
6. The steady-state frame still allocates nothing (#68), in RTL as well.

**Non-goals**

- Full bidi editing in `DrawnTextBox`: visual caret movement through mixed-direction runs, split carets, and dragging a selection across direction boundaries. This spec defines unidirectional editing in both directions and leaves mixed text approximate (section 6.4).
- Text shaping (#111) and font fallback (#74). This spec consumes them.
- Localized built-in strings. `KumikoStrings` already exists (M1).
- Per-column flow direction overrides. See open question Q11.
- Vertical writing modes.
- Inferring the direction from `KumikoSource.Culture`. The host property is the only input (Q4).

## 3. Approaches compared

All options share three prerequisites:
- The hosts force their canvas to LTR, so the platform never mirrors the pixels.
- The hosts take pointer coordinates relative to the canvas.
- Core receives a `KumikoFlowDirection`.

The options differ in where the mirroring happens.

### Option A: canvas mirror transform, text un-mirrored

**How it works:** apply `canvas.Translate(W, 0); canvas.Scale(-1, 1)` before `KumikoRenderer.Render`. Core keeps drawing in its LTR frame. Each text and image draw is flipped back locally around its rect, and input X is mirrored once (`x' = W - x`).

**Pros:**
- Layout, hit testing and the renderer need almost no changes.
- Everything mirrors by construction, frozen panes included.

**Cons:**
- `IDrawingContext` has no `Scale`. Adding one is a new interface member, and every backend must un-mirror text and images itself, so a backend that forgets draws text backwards. That is today's Uno bug, moved into the backend.
- Text drawn through a negative-scale matrix loses LCD/subpixel positioning on some backends, and hinting differs from LTR.
- Every `DrawText(text, x, y)` needs a measurement to un-mirror around the text's own extent, inside Skia code that Core cannot test with `RecordingDrawingContext`.
- Recorded draw operations in tests stay in the unmirrored frame, so tests cannot assert physical output.

### Option B: physical coordinates in the layout engine

**How it works:**
- `KumikoLayoutEngine` returns physical X positions per direction. For example, the start-frozen pane is anchored at `W - frozenWidth` and scrollable X runs right to left.
- `KumikoHitTester`, `KumikoRenderer`, `KumikoInputController` (`EnsureCurrentCellVisible`, `HeaderScreenX`, `GetCellScreenBounds`, `FinishColumnReorder`, `HandleColumnResize`) and every drawn component branch on direction at each geometric site.

**Pros:**
- No indirection.
- Exact, explicit control of each element.
- Text is drawn natively.

**Cons:**
- About 60 sites in the renderer, hit tester, controller and components each need their own RTL branch, roughly:
  - the three renderer passes, header text and glyphs, the group panel, summaries and the drag overlays;
  - the hit tester's pane math;
  - every component's internal layout (combo box arrow, numeric buttons, date picker navigation, filter popup rows).
- Drawing and hit testing can disagree, because they are separate code that must stay in sync.
- Each new feature needs RTL work and RTL tests.
- Third-party `ICellRenderer`s must handle RTL themselves.

### Option C (recommended): logical layout with a mirroring drawing context

Core lays out, hit-tests and draws in **logical** coordinates, where x is measured from the start edge. Three seams convert to physical:

1. **Output.** `KumikoRenderer.Render` wraps the frame's `IDrawingContext` in an internal `MirroringDrawingContext` when the direction is RTL. It maps every geometric argument with `x → W − x − width`:
   - rects, lines, polygon and polyline points, clips and images;
   - `Translate(dx, dy)` is forwarded as `Translate(−dx, dy)`, which keeps the mapping consistent under translation.

   Text is drawn upright in the mapped rect, with `Left` and `Right` alignment swapped (`Center` is unchanged). A point-anchored `DrawText(text, x, y)` is measured and drawn so the text's logical extent `[x, x + w]` maps to `[W − x − w, W − x]`. Images are drawn upright in the mapped rect.
2. **Input.** `KumikoInputController.HandlePointer` maps `e.X` to logical once, on entry. `HandleScroll` negates the horizontal delta. `HandleKey` maps the physical arrow keys to logical ones (section 7).
3. **Outbound geometry.** Every API that hands viewport geometry to a platform converts logical to physical:
   - `GetCellScreenBounds`;
   - editor bounds for the IME candidate window;
   - the accessibility semantics provider's rects.

   Section 8 lists them.

This is the idea behind Option A, implemented in Core instead of as a backend matrix.

**Why C:**
- **One seam per direction of travel.** Layout, hit testing and drawing share one coordinate frame, so they cannot disagree. Today's tests for panes, hit testing and keyboard keep testing the same math, and RTL tests only need to prove the three seams.
- **No backend work for geometry.** `SkiaDrawingContext` stays as it is. Any `IDrawingContext` works, test doubles included. `RecordingDrawingContext` records *physical* operations, so tests can assert the mirror directly (section 11).
- **Text is never mirrored.** Text never goes through a negative-scale matrix, and images are upright by default.
- **Third-party `ICellRenderer`s are mostly right for free.** They receive the wrapper, so their geometry mirrors, their text stays upright and their `Left` alignment means start. They can read `CellRenderContext.FlowDirection` to pick directional glyph variants.
- **The diff is small.** Most of it is in the input entry, the wrapper, the editors' text and caret code, and a handful of opt-outs.

**Costs and mitigations:**
- Things that must not mirror need an explicit opt-out: the checkmark glyph, the time-picker column order, and images marked directional (section 5.3). The renderer and components ask `ctx.FlowDirection` (a new default member, section 9) and pre-flip the few glyphs that must stay unmirrored.
- `DrawText(text, x, y)` needs a width. It uses `MeasureText`, which #23 caches across frames, so the steady state measures nothing.
- A new `IDrawingContext` member that a backend overrides for performance must also be forwarded, mapped, by the wrapper. If it isn't, the default implementation still routes through the wrapper's mapped primitives, which is correct but slower. A reflection test checks that the wrapper implements every member explicitly (section 11).

## 4. Model and terminology

- **Flow direction:** a new `KumikoFlowDirection { LeftToRight, RightToLeft }` for the whole grid, set by the host.
- **Logical X:** the distance from the **start** edge (left in LTR, right in RTL). In Core, all layout state is logical:
  - `ScrollState.OffsetX`;
  - the column X values that `KumikoLayoutEngine` produces;
  - `KumikoHitTestResult.ContentX`;
  - `KumikoScroller.GetColumnX` (#56);
  - `KumikoInputController.DragColumnScreenX`;
  - `DrawnComponent.Bounds`, popup anchors and `PopupLayout`.
- **Physical X:** pixels from the left edge of the canvas. Only the hosts, the `MirroringDrawingContext` output and the outbound APIs in section 8 use it.
- **Mapping:**
  - a point: `physical = W − logical`;
  - a rect: `physicalX = W − logicalX − width`.
  - `W` is `ScrollState.ViewportWidth`, the full canvas width, which includes the drag handle.
- **Start and end:** the names this spec uses for what the API calls Left and Right. On KumikoUI enums, `Left` and `Right` are flow-relative, as `TextAlignment` and `HorizontalAlignment` are in WinUI and WPF. Section 9 adds `Start` and `End` aliases that say so.

## 5. What mirrors and what does not

### 5.1 Grid structure (all through the wrapper, no per-site code)

| Element | Code today | RTL result |
|---|---|---|
| Column order | `PopulateVisibleScrollableColumns`, ascending X | First column at the right; columns continue leftward |
| Start-frozen pane (`ColumnFreezeMode.Left`, alias `Start`) | `PopulateVisibleFrozenColumns`, x from 0 | Pinned to the right edge |
| End-frozen pane (`Right`, alias `End`) | `PopulateVisibleRightFrozenColumns` | Pinned to the left edge |
| Frozen dividers and backgrounds | `DrawFrozenColumnDivider`, `DrawRightFrozenColumnDivider` | Divider at the pane's inner edge; colors unchanged (`FrozenColumn*` is the start pane, `RightFrozenColumn*` the end pane) |
| Pane clips and z-order | Passes 1–3 in `Render` | Unchanged order; the clips map |
| Scroll origin | `OffsetX = 0` shows the first columns | Unchanged logically: `OffsetX = 0` shows the start columns at the right; a larger offset reveals columns to the left |
| Horizontal pan | `HandlePointerMove`: `panDx = _lastPanX − e.X` | Correct with logical X: dragging content rightward reveals end columns, as on platform RTL scroll views |
| Inertia | `InertialScroller` on logical deltas | Unchanged |
| Resize grip | At `colRight` (logical end edge) | At the column's physical left edge. Dragging left widens it, because `HandleColumnResize` sees the logical delta |
| Column reorder ghost and drop | `DragColumnScreenX`, `FinishColumnReorder` | Logical; the ghost is drawn mirrored |
| Row drag handle (`DragHandlePosition.Left`, alias `Start`) | `DrawDragHandleColumn`, `handleOffset` | Handle strip on the right; grip dots are symmetric |
| Row drag ghost and drop line | `DrawRowDragOverlay` | Mirrored; the end ticks are symmetric |
| Group indent and chevron | `DrawGroupHeaderRow`: `indent = handleOffset + CellPadding + Level × GroupIndentWidth` | Indent from the right; the collapsed triangle points left (toward the end) |
| Sticky group headers (#174) | `PinnedGroupHeaders` | Vertical only; unchanged |
| Group panel | `DrawGroupingPanel` / `HitTestGroupPanel` | Label at the right, chips running leftward, accent bar on each chip's right, remove button on its left, separator chevron points left |
| Header text | `DrawHeader`, alignment `Left` | Right-aligned (start), with the glyph space reserved on its left |
| Sort arrow and multi-sort badge | `DrawSortIndicators`: `screenX + Width − CellPadding − 8` | At the header's left end. The up/down triangles are symmetric; the badge digits are upright |
| Filter funnel | `DrawFilterIcons` | Between the header text and the sort arrow, so just to the right of the arrow. Symmetric |
| Grid lines, current-cell border | `DrawGridLines`, `DrawCurrentCellBorder` | Mirrored |
| Table and group summaries | `DrawTableSummaryRows`, `DrawGroupSummaryRow`, `DrawSummaryValues`, `DrawSummaryOverflow` | Title at the start (right) edge; values in their column with the column's flow-relative alignment; overflow after the title (leftward) |
| Frame stats overlay | `KumikoFrameStats.Draw` | Drawn with the unwrapped context, so it stays where it is |

### 5.2 Editors and popups

| Element | RTL result |
|---|---|
| `DrawnTextBox` | Border, padding and clip mirror. Text anchors at the start edge, the caret and selection follow the paragraph direction (section 6.4), and `_scrollOffset` scrolls toward the end. |
| `DrawnComboBox` | The arrow slot (`ArrowWidth`) is at the end, physically left. The dropdown aligns to the anchor's start edge, so right edges align. The list text is start-aligned. |
| `DrawnNumericUpDown` | The [−] and [+] order mirrors: [+] on the left, [−] on the right. This matches WinUI `NumberBox` with spin buttons in RTL; Q3 asks reviewers to confirm. |
| `DrawnDatePicker` | The month grid mirrors, so the first day of the week is at the right. The previous and next chevrons swap sides and point outward, as platform RTL calendars do. The time columns do **not** mirror (5.3). |
| `DrawnProgressBar` (display and slider) | Fills from the right, and the thumb drag uses the logical delta. Material and platform sliders mirror progress the same way. |
| `DrawnCheckBox` | Its position mirrors; the check glyph does not (5.3). |
| `DrawnFilterPopup` | Check boxes on the right, text right-aligned, buttons in mirrored order. Anchored to the header's start edge by `HeaderScreenX` and clamped by `PopupLayout.Clamp` (#36), both logical. |
| Action buttons (`ActionButtonLayout`) | The first button is at the start (right). `ComputeButtonRects` and `IndexAt` stay logical. |
| `PopupManager` placement | `PopupPlacement.Below` and `Above` are unchanged. `Left` and `Right` become flow-relative (aliases `Start` and `End`). `PopupLayout.Place` puts a popup at `anchor.X`, its logical start, and that is already the correct RTL alignment. |
| Validation message (`EditSession`, "Left" text) | Start-aligned. |

### 5.3 What must not mirror

| Element | Why | How |
|---|---|---|
| Text glyphs (all strings) | Text is never mirrored; bidi decides the visual order (section 6) | The wrapper draws text upright |
| Numbers, dates and times in cells | Digits are LTR in every RTL script. Formatting follows `KumikoSource.Culture`, never the flow direction | Numeric and date columns default to the LTR reading order (6.2). The flow direction never changes the format culture |
| Images (`ImageCellRenderer`, `DrawImage`) | Photos and logos are not directional | The wrapper draws images upright in the mapped rect. Directional images (arrows) need an app-supplied RTL variant or the opt-in flag in Q12 |
| Checkmark (`DrawnCheckBox`, `BooleanCellRenderer`, filter popup) | Platform checkmarks are not mirrored; WinUI `FontIcon.MirroredWhenRightToLeft` is false by default | `GlyphDrawing.Checkmark(…)`, a new helper that replaces the three inline `Chevron` calls. It pre-flips its points inside its box when `ctx.FlowDirection` is RTL, so the wrapper's flip restores them |
| Sort arrows, filter funnel, grip dots, plus and minus, expanded (down) triangle, "×" | Symmetric | Nothing to do: their positions mirror and their shapes are unaffected |
| Time picker columns (`DrawnScrollPicker` in `DrawnDatePicker`) | "HH:MM" reads LTR in RTL locales | The picker lays its columns out with a local un-mirror (Q3) |
| Vertical geometry | Not affected by direction | Not affected by the wrapper |

## 6. Text

### 6.1 Two directions, kept apart

- **Grid flow direction** sets the layout and the meaning of alignment. A column's `Start` is on the right in an RTL grid, whatever its content.
- **Paragraph (base) direction** is the bidi base level for one string. It decides the order of runs, where neutrals go, and on which side the ellipsis falls. It is per string and may differ from the grid. A Latin product name in a Hebrew grid is an LTR paragraph, right-aligned.

This follows WinUI, where `TextAlignment` follows `FlowDirection` and `TextReadingOrder` decides the base direction, and Excel, where the sheet direction is set apart from a cell's "Context" reading order.

### 6.2 Reading order per column

New `KumikoTextReadingOrder { Default, UseFlowDirection, DetectFromContent, LeftToRight, RightToLeft }` and a column property `KumikoColumn.TextReadingOrder` (default `Default`).

`Default` resolves by column type:
- `Numeric` and `Date` columns use **LeftToRight**. Without it, "-5" in an RTL paragraph displays as "5-": a leading `ES` before `EN` resolves to `ON`.
- Every other column uses **DetectFromContent**: the first strong character, else the grid direction. Without it, "Hello!" in an RTL paragraph displays as "!Hello".

Headers, the group panel, summary titles and built-in strings (`KumikoStrings`) use **UseFlowDirection**, because they belong to the app's UI language.

### 6.3 Alignment

Recommendation: **`Left` and `Right` are flow-relative**, as in WinUI and WPF, and `Start` and `End` are added as aliases with the same values (section 9). The reasons:
- Every built-in call site that passes `Left` today means "start", for example `DrawHeader` and `DrawGroupHeaderRow`.
- The default `KumikoColumn.TextAlignment = Left` becomes "start" with no change in behavior.
- Third-party code that switches over `Left`, `Center` and `Right` keeps handling every value, which distinct new values would break.
- A numeric column set to `Right` lands on the left in RTL, as Excel's General alignment does in an RTL sheet.

There is no physical alignment in v1 (Q1). The wrapper swaps `Left` and `Right` because it maps the rect. An unwrapped `SkiaDrawingContext` draws LTR, where `Start` is `Left`.

### 6.4 Paragraph direction in drawing, ellipsis and the editor

**Drawing API.** New default members take a `KumikoTextDirection { Auto, LeftToRight, RightToLeft }` (section 9). The default implementation ignores the direction, so existing backends keep compiling. `SkiaDrawingContext` implements it in S5:
- Runs a UAX #9 analysis of the string.
- Puts the level runs in visual order.
- Measures and positions each run, with #74's typeface runs inside each level run and #111's shaping on each run.
- Places the line by alignment, using its drawn width (as #23 already does for truncated text).

**Ellipsis.** It always replaces the **logical end** of the text. #23's `KumikoTextLayout.Ellipsize` keeps the leading text elements and appends "…". In a bidi-aware draw, a trailing "…" takes the paragraph direction, so it appears on the visual left of an RTL paragraph and on the right of an LTR one, whatever the alignment. The fitting search still measures logical prefixes, which bidi does not change.

**Caching.** #23's cross-frame width and line caches add the direction to their key. A string with no strong RTL character, in a paragraph that resolves to LTR, takes a fast path with no bidi analysis. That fast path covers all numeric and date columns.

**Until S5 lands**, the grid can mirror, but Hebrew and Arabic strings still render in logical order. RTL is documented as supported only once S5 ships, and Arabic is complete only with #111 (section 12).

**Editor (`DrawnTextBox`):**
- **Base direction:** the column's resolved reading order, with `DetectFromContent` re-evaluated as the user types (as the platforms do). `CellEditorFactory` sets it, next to #36's `ApplyFontToEditor` and `ApplyViewportToEditor`.
- **Caret and selection X:** `XAt(index)` today is a prefix width from the left.
  - Under the wrapper, the editor draws in logical coordinates. For a paragraph that matches the flow direction (RTL text in an RTL grid, or LTR text in an LTR grid), the visual offset of logical index *i* from the start edge equals the prefix width. `XAt` is then correct unchanged.
  - When the paragraph direction is opposite to the flow (LTR text in an RTL grid), the editor flips its caret and selection X inside the inner rect: `innerX + innerW − (textStartOffset + XAt(i))`, with the text block anchored at the start edge. The text itself is drawn by `DrawText(…, direction)` at the same place.
  - Mixed-direction text is approximate in v1: logical prefix widths. A follow-up issue maps carets through S5's visual runs.
- **Keys:** `Left` and `Right` in the editor are relative to the **paragraph** direction. In an RTL paragraph, `Left` moves to the next grapheme (logically forward) and `Right` to the previous one. Ctrl or Cmd with an arrow does the same by word. `Home` and `End` stay logical, to the paragraph's start and end. For unidirectional text this equals visual movement.
- **Composition underline and the IME candidate window:** the underline is mapped like the selection. The window receives physical editor bounds (section 8).

## 7. Keyboard

`KumikoInputController.HandleKey` maps the physical key to a logical key once, before the normal-mode `switch`, when the grid flow is RTL: `Left ↔ Right`. Editing-mode keys go to the editor unmapped, because the editor maps by paragraph direction (6.4). Popups receive their own mapping through `DrawnComponent.FlowDirection`. A key that an editor or popup leaves unhandled, and that falls through to grid navigation, is mapped when it falls through.

| Key | LTR today | RTL |
|---|---|---|
| Right arrow | `NavigateRight`: next column | Previous column, which is to the visual right |
| Left arrow | `NavigateLeft`: previous column | Next column, which is to the visual left |
| Shift + Left/Right | Extends | Extends in the mapped direction |
| Home / End | First / last column (`NavigateHome`, `NavigateEnd`) | Unchanged logically: Home goes to the first column, now at the right |
| Ctrl+Home / Ctrl+End | First / last cell | Unchanged |
| Tab / Shift+Tab | Next / previous tab stop (`MoveToNextTabStop`) | Unchanged logically: Tab moves toward the end, which is visually leftward. Reading order is logical on every platform |
| Group header Right / Left | Expand / collapse | Left expands (the collapsed triangle points left) and Right collapses, as a platform RTL tree view does |
| Up, Down, PageUp, PageDown | Rows | Unchanged |
| Editor Left / Right / Home / End | Caret | By paragraph direction (6.4) |
| `DrawnDatePicker` Left / Right (days) | ±1 day | Mapped by the picker's flow: Left is the next day in RTL |
| Combo box, scroll picker | Up and Down only | Unchanged |

Navigation should follow **display order**: start-frozen columns, then scrollable, then end-frozen, skipping hidden columns. Today `KumikoSelectionModel.NavigateLeft` and `NavigateRight` step through the raw column index. In LTR, that already jumps across panes when an end-frozen column sits in the middle of `Columns`, and it can land on hidden columns. The RTL keyboard issue (S3) fixes this for both directions with a `KumikoLayoutEngine.GetDisplayOrder` helper (allocation-free and reused) and records the LTR change in the changelog.

## 8. Hit testing and input mapping

- **Pointer entry.** `HandlePointer` maps `e.X` to logical in place, once (`W − e.X`). It does so before `PopupManager.HandlePointer`, so popups, editors, the hit tester and the gesture state all see logical X.
  - `KumikoPointerEventArgs.X` is `init`-only. The mapping writes the backing field through an internal method, guarded by an internal `IsLogical` flag so a forwarded event is never mapped twice.
  - Hosts only read `Handled` after the call, so mapping in place keeps hover allocation-free.
  - Alternative: allocate a mapped copy, in RTL only. Pointer events are not on the frame path, but hover moves are frequent, so the in-place form is preferred.
- **Wheel.** A host-reported horizontal delta (tilt wheel, trackpad) is physical, so `HandleScroll` negates `ScrollDeltaX` in RTL.
  - After #60, Shift+wheel is converted to a horizontal delta in Core. That converted delta is **logical**: wheel-down scrolls toward the end. It is not negated (Q6).
  - The MAUI Catalyst trackpad pan goes through `HandlePointer` after #60 and is covered by the pan rule.
- **`KumikoHitTester`.** The math does not change. `HitTest(viewportX, …)` is documented as taking **logical** X. A public overload `HitTest(physicalX, …, KumikoFlowDirection)` maps the point and calls through, for hosts and the accessibility provider. `KumikoHitTestResult.ContentX` is logical.
- **Logical consumers.** `ComputeCursor`, `IsOnCheckBox`, `GetInteractiveCellBounds`, `IInteractiveCellRenderer.HasControlAt`, `ActionButtonLayout.IndexAt`, `PopupManager` and `DrawnComponent` hit tests all receive logical bounds and logical points. They stay consistent and need no change.
- **Outbound geometry is physical:**
  - `KumikoInputController.GetCellScreenBounds` returns physical rects. Its internal callers move to a logical `GetCellLogicalBounds`.
  - The editor's bounds for IME placement (#40–#43 adapters) are physical.
  - The accessibility semantics provider (A11Y spec, #77/#78) reports physical rects in logical reading order.
  - Any future host-level `GetCellBounds` is physical.

  Each conversion is one call to `KumikoFlowMapper.ToPhysical(KumikoRect, float viewportWidth)`.

Two problems found while reading the code (not RTL-specific, fixed in S2):
- `KumikoInputController.HeaderScreenX` ignores the drag handle offset. The filter popup anchor is off by `RowDragHandleWidth` when the handle is at the start.
- `HeaderScreenX` also allocates through LINQ on every anchor evaluation.

## 9. API changes and compatibility

Every change is additive. LTR behavior is identical. The sketches below list only the new members; `partial` marks a type that gains members, not a declaration change.

```csharp
namespace KumikoUI.Core.Rendering;

/// <summary>The grid's layout direction.</summary>
public enum KumikoFlowDirection { LeftToRight, RightToLeft }

/// <summary>Base (paragraph) direction of one drawn string.</summary>
public enum KumikoTextDirection { Auto, LeftToRight, RightToLeft }

public enum KumikoTextAlignment
{
    Left, Center, Right,
    /// <summary>The start edge: left in a left-to-right grid, right in a right-to-left one. Same value as Left.</summary>
    Start = Left,
    /// <summary>The end edge. Same value as Right.</summary>
    End = Right,
}

public interface IDrawingContext
{
    // New default members (no breaking change for implementers):
    KumikoFlowDirection FlowDirection => KumikoFlowDirection.LeftToRight;
    void DrawText(string text, float x, float y, KumikoPaint paint, KumikoTextDirection direction)
        => DrawText(text, x, y, paint);
    void DrawTextInRect(string text, KumikoRect rect, KumikoPaint paint, KumikoTextAlignment hAlign,
        KumikoVerticalAlignment vAlign, bool clip, KumikoTextWrap wrap, KumikoTextDirection direction)
        => DrawTextInRect(text, rect, paint, hAlign, vAlign, clip, wrap);   // #23 overload
}

public readonly struct CellRenderContext { /* + */ public KumikoFlowDirection FlowDirection { get; } }
```

```csharp
namespace KumikoUI.Core.Models;

public enum ColumnFreezeMode   { None, Left, Right, Start = Left, End = Right }
public enum DragHandlePosition { Left, Right, Start = Left, End = Right }
public enum KumikoTextReadingOrder { Default, UseFlowDirection, DetectFromContent, LeftToRight, RightToLeft }

public partial class KumikoColumn { public KumikoTextReadingOrder TextReadingOrder { get; set; } }
```

```csharp
namespace KumikoUI.Core.Components;
public enum PopupPlacement { Below, Above, Left, Right, Start = Left, End = Right }
public abstract partial class DrawnComponent { public KumikoFlowDirection FlowDirection { get; set; } }

// Core, per grid (not on KumikoStyle: GridStyle can be shared between grids, and the direction comes
// from each grid's own visual tree):
public partial class KumikoRenderer        { public KumikoFlowDirection FlowDirection { get; set; } }
public partial class KumikoInputController { public KumikoFlowDirection FlowDirection { get; set; } }
public partial class EditSession           { public KumikoFlowDirection FlowDirection { get; set; } } // to editors and popups
public static class KumikoFlowMapper       { /* ToPhysical / ToLogical for X, rects, points */ }
```

Internal: `MirroringDrawingContext` (a reused instance; `Bind(inner, viewportWidth)` once per frame) and `GlyphDrawing.Checkmark`.

**Compatibility notes:**
- **Enum aliases.** Existing XAML (`FreezeMode="Left"`) and code keep working and now mean "start". `Enum.ToString()` for an aliased value may return either name, because .NET leaves it unspecified. Nothing in Core relies on enum names, and the aliases are declared after the originals. A test pins XAML parsing of both names on both hosts.
- **No `[Obsolete]` on Left or Right in 1.x.** Docs and samples switch to `Start` and `End`. Whether 2.0 obsoletes `Left` and `Right` is left open (Q7).
- **Names that keep "Right".** `IsFrozenRight`, `GetRightFrozenColumnWidth`, `PopulateVisibleRightFrozenColumns`, `RightFrozenColumnDividerColor`, `RightFrozenColumnBackgroundColor` and `RightFrozenColumnDividerWidth` keep their names, and their docs say "end". Adding `IsFrozenEnd` and `EndFrozen*` forwarding aliases is Q7.
- **Docs.** `docs/RENDERING.md` §"Coordinate System" changes from "top-left" to "top-start (logical)", and `docs/ARCHITECTURE.md` gains a "Flow direction" pattern entry.

## 10. Host plumbing

Both hosts do the same five things:

1. **Force the canvas LTR.** Set `_canvasView.FlowDirection = LeftToRight` explicitly, not inherited, so the platform never mirrors pixels. This is stage 0 and fixes the "text drawn backwards" bug on Uno and MAUI Windows even before Core supports RTL.
2. **Take pointer coordinates relative to the canvas.**
   - Uno: `e.GetCurrentPoint(_canvasView)` instead of `GetCurrentPoint(this)`, because in an RTL `UserControl` the control's own coordinate space is mirrored. This overlaps #60.
   - MAUI: `SKTouchEventArgs.Location` is already relative to the canvas.
3. **Read the effective direction**, set `KumikoRenderer.FlowDirection`, `KumikoInputController.FlowDirection` and `EditSession.FlowDirection` through one private `ApplyFlowDirection()`, and invalidate.
   - MAUI: `((IVisualElementController)this).EffectiveFlowDirection.IsRightToLeft()`, re-read in the existing `OnPropertyChanged` override when `FlowDirectionProperty.PropertyName` changes. MAUI raises that name when an ancestor's direction changes the effective value; S6 verifies this on every head.
   - Uno: `this.FlowDirection` is an inherited dependency property. Watch it with `RegisterPropertyChangedCallback(FrameworkElement.FlowDirectionProperty, …)` from the constructor. S6 verifies that the callback fires for inherited changes on Skia, WASM and the native heads.
4. **Runtime change:**
   - Close open popups (`PopupManager`). Their anchors and keyboard mapping change.
   - Keep an open editor. `EditSession.PlaceEditor` re-places it on the next frame, and its caret mapping follows the new flow.
   - Keep `ScrollState.OffsetX`. It is logical, so the same columns stay in view, mirrored.
   - No measurement cache is invalidated. Widths do not depend on direction, and the line cache is keyed by direction.
5. **Hidden text-input proxies** (Uno `ProxyTextInput`, MAUI `_keyboardProxy` and `KeyInputResponder`): set their `FlowDirection` or semantic content attribute to match the grid, so the platform input UI aligns. Text input does not depend on it.

Apps decide the direction, for example `FlowDirection = CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft ? RightToLeft : LeftToRight` on the page. Neither host infers it from `KumikoSource.Culture` (Q4).

## 11. Zero-allocation constraints

The steady-state RTL frame allocates 0 bytes, as LTR does (#68). `RenderAllocationTests` gains RTL cases.
- **Wrapper.** `MirroringDrawingContext` is one instance per renderer, re-bound each frame. It captures no closures and boxes nothing.
- **Polygons and polylines.** Points are mapped into a `stackalloc` span, as `GlyphDrawing` already does. Glyphs have 3 to 7 points, and anything over 32 rents from `ArrayPool<KumikoPoint>.Shared`.
- **Point-anchored text.** `DrawText(text, x, y)` measures once per string and font. With #23 the measurement is cached across frames. Without #23, `SKFont.MeasureText` allocates no managed memory, but it measures every frame.
- **Bidi analysis.** Cached per (text, font, direction) in #23's LRU line cache. LTR-only strings take the fast path (6.4), and level-run buffers are pooled.
- **Input.** Pointer X is mapped in place, and keys are mapped with a `switch`. `HitTestCore` stays a value type.
- **Display-order navigation (S3).** Uses a reused `int[]` on the layout engine.

## 12. Test plan

All Core tests use the existing helpers: `RenderHarness`, `RecordingDrawingContext`, `InputGrid`, `HitTesterTests`, `KeyboardTableTests` and `RenderAllocationTests`.

1. **Mirror equivalence** (`RtlMirrorTests`). The fixture is the same grid rendered at `W = 600` in LTR and in RTL. It has start- and end-frozen columns, the drag handle at the start and at the end, grouping with summaries, a top summary and a pinned footer, frozen rows, an open editor and an open filter popup. For every recorded operation:
   - Non-text operations: `RTL.rect == Mirror(LTR.rect)`. Polylines and polygons are mirrored point by point, except the checkmark, which must equal the *unmirrored* LTR points translated into the mirrored box.
   - Text operations: the same string, a mirrored rect, and swapped `Left`/`Right` alignment.
2. **Pixel goldens** (Skia, like `SkiaGlyphRasterTests`):
   - A text-free fixture: the RTL bitmap equals the horizontally flipped LTR bitmap. `W` is an integer, so hairlines stay on the pixel grid; the tolerance is 1 LSB per channel. The checkmark region is compared with the unflipped LTR region.
   - Text goldens (after S5): Hebrew, Arabic (after #111), "-5", "2026-11-11", "Hello!" and mixed "שלום Hello 123" cells in LTR and RTL grids, with an ellipsis on each paragraph direction. This needs Noto Sans Hebrew and Noto Sans Arabic in `SkiaTestFonts` (Q10).
3. **Hit-test tables** (`HitTesterRtlTests`):
   - A theory table maps physical (x, y) to (region, row, column) for each RTL region: start- and end-frozen cells, resize grips (at the physical left edge of each column), the filter icon (left of the header text), the group chevron (at the right), group panel chips and their remove buttons, the drag handle at the start and at the end, and empty area.
   - A property test: for 1,000 random points, `HitTest_RTL(W − x, y)` equals `HitTest_LTR(x, y)` in region, row and column.
4. **Keyboard tables** (`KeyboardTableRtlTests`): one test per row of the section 7 table, plus display-order navigation in LTR and RTL with an end-frozen column in the middle of `Columns` and a hidden column.
5. **Gesture tests** (`PointerGestureTests`, `ResizeAndReorderTests` RTL variants):
   - pan direction and inertia;
   - wheel: the horizontal delta is negated in RTL and Shift+wheel is not;
   - dragging a resize grip physically left widens the column;
   - column reorder drop index;
   - row drag drop line.
6. **Editor tests**:
   - caret X for RTL text in an RTL grid, LTR text in an RTL grid, and RTL text in an LTR grid;
   - arrow keys by paragraph direction;
   - the position of the composition underline;
   - physical editor bounds for the IME.
7. **Popup tests** (#36's `EditorFontAndPopupTests` RTL variants): the dropdown is right-aligned to the anchor, flips and clamps in RTL, and the date picker grid and its navigation are mirrored.
8. **Wrapper coverage**: a reflection test asserts `MirroringDrawingContext` implements every `IDrawingContext` member explicitly, so a new member cannot bypass the mapping silently.
9. **Hosts**:
   - Uno `InputMappingTests`: pointer origin from the canvas under an RTL parent.
   - XAML parsing of `Start`, `End`, `Left` and `Right` on both hosts.
   - Manual device matrix (MAUI iOS, Android, Catalyst, Windows; Uno Skia desktop, WASM, iOS, Android):
     - an RTL ancestor mirrors the grid with no backwards text;
     - toggling at runtime with an editor open;
     - IME candidate window placement.
10. **Allocation tests**: RTL cases in `RenderAllocationTests` (static grid, grid with an open editor, scrolling) assert 0 bytes per frame.

## 13. Staged implementation issues

| # | Title | Depends on | Acceptance criteria |
|---|---|---|---|
| S0 | **Force the canvas left-to-right on both hosts and document RTL as unsupported (1.0)** | none | `_canvasView.FlowDirection = LeftToRight` on MAUI and Uno. Uno pointer coordinates are taken relative to `_canvasView`. Under an RTL ancestor, the grid renders and hit-tests exactly as in LTR on Uno Skia, WASM and Windows, and on MAUI Windows (no backwards text). README "Requirements" states that RTL layout is not supported in 1.0 (proposed wording below). |
| S1 | **Core: KumikoFlowDirection, Start/End aliases and the mirroring drawing context** | none | The enums, aliases and default members from section 9. `KumikoFlowMapper`. `MirroringDrawingContext`, with the reflection coverage test and mirror-equivalence tests for every primitive. `FlowDirection` on the renderer, the input controller, `EditSession` and `DrawnComponent` (no behavior yet). The public API has XML docs, and doc warnings are errors (#54). |
| S2 | **Core: render and hit-test the grid in RTL** | S1 | The renderer wraps the context, and pointer X is mapped on entry. Grid elements in sections 5.1 and 5.3 mirror and opt out as specified, with `GlyphDrawing.Checkmark`. `GetCellScreenBounds` returns physical rects. Fix `HeaderScreenX` (drag handle offset, no LINQ). Test plan items 1, 2 (text-free), 3 and 10 pass. |
| S3 | **Core: RTL keyboard and gestures (arrows, Home/End, Tab, group headers, wheel, pan, resize, reorder, display-order navigation)** | S2 | The section 7 table is implemented. Navigation follows display order in both directions (LTR change noted in CHANGELOG). The horizontal wheel delta is negated. Test plan items 4 and 5 pass, and README keyboard table gets an RTL note. |
| S4 | **Core: RTL editors and popups** | S2, #36 (PR #212) | The section 5.2 table is implemented. The `DrawnTextBox` caret and selection follow the paragraph direction (6.4). The date picker and numeric up/down are mirrored, the time picker is not. Popups are start-aligned. Editor bounds are physical for IME adapters. Test plan items 6 and 7 pass. |
| S5 | **Skia: paragraph direction and bidi run ordering (UAX #9) in SkiaDrawingContext** | S1, #23 (PR #199); coordinates with #74 and #111 | The `KumikoTextDirection` overloads are implemented. UAX #9 level runs are put in visual order, and #74's typeface runs go inside them. `KumikoColumn.TextReadingOrder` resolves as in 6.2. The ellipsis goes at the logical end. The cache key includes the direction, and the LTR fast path does no analysis. Hebrew goldens pass, plus the "-5", date and "Hello!" cases. Arabic shaping is deferred to #111. RTL allocation tests stay at 0. |
| S6 | **Hosts: FlowDirection plumbing and runtime changes (MAUI, Uno)** | S2 (S3, S4 for complete behavior) | Section 10 steps 3–5. Inherited and explicit `FlowDirection`, and runtime toggles, verified on every head in the test plan matrix (including whether inherited changes raise notifications). |
| S7 | **Docs and samples: RTL** | S2–S6 | An RTL page in `SampleApp.Maui` and `SampleApp.Uno` with Hebrew data. Updates to `docs/RENDERING.md`, `docs/ARCHITECTURE.md` and `docs/TEXT-INPUT.md` (paragraph direction, physical IME bounds). README: RTL listed as supported (Hebrew), with Arabic pending #111. |

S2 and S3 can run in parallel after S1, and S5 can run in parallel with S2 through S4. RTL is announced as supported only when S0 through S6 have shipped. Arabic is announced once #111 has shipped as well.

**Proposed README wording for S0** (README.md is not edited by this spec):

> **Right-to-left layout** is not supported in 1.0: the grid always lays out left to right, also inside a `FlowDirection="RightToLeft"` page, and Hebrew or Arabic cell text is drawn without bidi reordering or shaping. Mirroring is planned for 1.x (#80).

## 14. Open questions

1. **Alignment semantics.** This spec proposes that `Left` and `Right` are flow-relative (WinUI and WPF) and that `Start` and `End` are aliases. The alternative is CSS-style physical `Left`/`Right` with distinct logical `Start`/`End`; it adds an escape hatch for always-left columns, but it breaks third-party `switch`es and makes `Left` mean start in only some places. Is a physical escape hatch needed in v1?
2. **Default reading order for text columns.** This spec proposes `DetectFromContent`. The alternative is `UseFlowDirection`, which is WinUI's default.
3. **Exceptions to mirroring.** Is the numeric up/down order mirrored? Is the time picker column order kept LTR? Is the checkmark kept unmirrored? Each is proposed in 5.2/5.3 and needs designer confirmation.
4. **Culture and direction.** Should Core ever derive the direction from `KumikoSource.Culture` when no host property is set (for example in headless rendering)? This spec proposes no.
5. **Bidi implementation for S5.** The choices are a managed UAX #9 port in `KumikoUI.SkiaSharp`, as Topten.RichTextKit does (MIT); ICU through a dependency; or a combination with #111's HarfBuzz package. The decision affects WASM size and AOT. Decide alongside #111.
6. **Shift+wheel in RTL.** This spec proposes logical: wheel-down scrolls toward the end. Platforms differ, so verify against WinUI `ScrollViewer` and browsers.
7. **Naming aliases.** Should `IsFrozenEnd`, `EndFrozenColumn*` and `PopupPlacement.Start`/`End` be added, or are the enum aliases enough? Should `Left` and `Right` be obsoleted in 2.0?
8. **Timing.** Should S1's enum aliases and `KumikoFlowDirection` land before 1.0, so the 1.0 API already has its final shape? The change is additive, and LTR behavior does not change.
9. **MAUI defaults.** Does MAUI follow the device RTL setting automatically on Android and iOS (`supportsRtl`, `UIView.SemanticContentAttribute`), or only when the app sets `FlowDirection`? Does an ancestor change raise `FlowDirection` on the grid?
10. **Test fonts.** Adding Noto Sans Hebrew and Noto Sans Arabic (OFL) to the test assets adds a few hundred KB to the repo.
11. **Per-column direction override.** For example an LTR code column inside an RTL grid that keeps its column order. Not in v1, but is it in scope for 1.x?
12. **Directional images.** Should `ImageColumn` get a flag like `MirroredWhenRightToLeft`, as on WinUI `FontIcon`, for arrow-like images?

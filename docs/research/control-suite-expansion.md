# From datagrid to control suite: research notes

What it takes to grow KumikoUI from one datagrid into a suite with six more controls: tree grid, charts, scheduler, docking, pivot grid and Gantt. This file is planning input for one milestone per control plus the shared-foundation milestones in front of them. It changes no code.

- **Date:** 2026-11-27, at commit `15cbb15` on `release/1.0.0`.
- **Scope:** the six controls above on seven frameworks: Blazor, WPF, Windows Forms, WinUI, .NET MAUI, Avalonia and the Uno Platform. Editors and a PDF viewer are out of scope and appear only where a target control depends on them.
- **Inputs:** the repository at that commit, and a separate market-demand ranking that is treated as background on demand and ordering and is not repeated here.
- **Sources:** this repository's code (cited as `path:line`), first-party framework documentation, W3C and IETF specifications, and SkiaSharp documentation. No third-party control vendor or control library is named or cited.
- **Labels:** **(inferred)** marks a conclusion drawn rather than read in a source. **(unverified)** marks a claim for which no first-party statement was found; each one is repeated in [Unverified items](#unverified-items). Web pages were read through a summarizing fetcher on 2026-11-27, so quotations are close paraphrases unless short.
- **Sizes:** S, M, L and XL are relative to each other only. As an anchor, the existing grid engine is about 28,000 lines in `src/KumikoUI.Core`, and each existing host is 6,000 to 9,000 lines.

## Decisions

Made by the maintainer on 2026-11-27 after reading these notes. Where a decision narrows the scope above, the analysis below is kept as the reasoning behind it.

| Area | Decision |
|---|---|
| Controls | Tree grid, charts, scheduler, pivot grid and Gantt. **Docking is out of scope:** it shares almost nothing with the engine and depends on host windowing. |
| Frameworks | Six, for every control including the datagrid: .NET MAUI, Uno Platform, WinUI, WPF, Avalonia and Blazor. **Windows Forms is out of scope.** Blazor means WebAssembly only; server rendering is recorded as not supported. |
| Platform coverage | A control may ship on a subset of frameworks. Every gap is recorded in the support matrix with a reason. |
| Rendering | All controls are drawn on the Core / SkiaSharp / host split. Native pieces are used only where drawing cannot do the job. |
| Foundations | A "Suite foundations" milestone precedes every control: control-neutral kernel, host kit, drawing companion interface, hierarchical row source, and a control-neutral semantic tree. It also takes from Broadside (the maintainer's PDF library, which has viewer shells for WPF, WinUI, Avalonia and Blazor WebAssembly): a single-compiled host controller, a kernel-to-host interface with a fake host for tests, a multi-pointer gesture recognizer, a viewport type with anchored zoom, and its semantic tree model. Two spikes belong here: SkiaSharp version compatibility on Avalonia and Uno, and whether shipped types can move behind type forwards. Code ported from Broadside is recorded in `THIRD-PARTY-NOTICES.md`. |
| Hosts | New hosts land in the order WPF, WinUI, Avalonia, Blazor WebAssembly, one milestone each, bringing up the datagrid first and then every control already shipped. |
| Shared engines | The date engine, aggregation engine and scale/axis component are built inside the first control that needs them, but written to be portable: no dependency on that control's types, so later controls can take them unchanged. |
| Tree grid | A separate `KumikoTreeGrid` control over shared engine code, not a mode of the datagrid. |
| MVP depth | Scheduler: a documented RFC 5545 subset with a conformance suite. Pivot grid: in-memory data only. Gantt: display only, with the scheduling engine in its complete tier. |
| Editing | Scheduler and Gantt raise an edit-requested event, plus an optional quick editor built from existing drawn components. A full editors pack is deferred. |
| Accessibility | The semantic tree is required before any control leaves experimental. |
| Versioning | One lockstep version. New controls ship in stable 1.x minors marked experimental until they graduate. |
| Packaging | Each control, including the datagrid, ends up in its own package. New control families get their own package from the start. The split of the existing packages happens in 1.1 or 2.0, decided by the type-forward spike. |
| Plan | Eleven milestones: Foundations, Tree grid, Charts, Scheduler, Pivot grid, Gantt, four host milestones, and Docs and website. See the [milestone plan](../suite/milestone-plan.md). |
| Repository | One repo and one solution. KumikoUI stays the suite name. |
| Milestones | GitHub milestones without due dates, ordered by dependency, each with a tracking issue. The open 1.x specs are pulled into the suite milestone that first needs them; 1.0.x fixes stay on a patch track. |
| Docs | A support matrix in `docs/SUPPORT-MATRIX.md` linked from the README, updated in the PR that changes a status. Docking and Windows Forms are omitted from it; the roadmap records that both were considered and dropped. `docs/ROADMAP.md` becomes the suite roadmap. A unified website (landing page, per-control docs, API reference, matrix, then live demos) is its own milestone, starting alongside the tree grid. |

## Summary of key conclusions

1. **Two of the seven frameworks have a host today.** `KumikoUI.Maui` and `KumikoUI.Uno` ship (`docs/ARCHITECTURE.md:20-29`, `README.md:118-129`). There is no Blazor, WPF, Windows Forms, native WinUI or Avalonia host anywhere in the solution (`KumikoUI.sln`). The demand ranking assumes a grid on all seven, so the host gap is the largest single item in this plan: five new hosts, each multiplied by every control.
2. **The layering is real but the engine above the drawing layer is grid-shaped.** Core has no UI-framework or SkiaSharp reference (`src/KumikoUI.Core/KumikoUI.Core.csproj:4`), and the SkiaSharp package is fully control-agnostic. But the frame (`KumikoRenderer`, 2,018 lines), input (`KumikoInputController`, 2,390 lines), hit testing, layout, selection and the data source are written for a flat, fixed-row-height, column-based grid. There is no control-agnostic base to derive a second control from.
3. **Per-host code is duplicated, not shared.** The two hosts each carry their own copy of the frame scheduler (the files differ only in comments), surfaces, caret blink, pointer mapping and text-input adapters, all `internal` to the grid host. A second control on the same framework cannot reuse them without a refactor. A shared "host kit" per framework is the highest-leverage foundation.
4. **The drawing abstraction is too small for charts.** `IDrawingContext` has rectangles, round rectangles, lines, polygons, polylines, text, clip, translate and images (`src/KumikoUI.Core/Rendering/IDrawingContext.cs:13-243`). It has no paths, arcs, curves, gradients, dashes, rotation, scaling or opacity layers, and `KumikoPaint` is a color, a stroke width, an anti-alias flag and a font (`src/KumikoUI.Core/Rendering/KumikoPaint.cs:7-20`). The interface is frozen public API, so it grows only through default interface members or a second interface.
5. **Tree grid is the cheapest control and unlocks two others.** Grouping already flattens a hierarchy into a row list with levels, expansion state and sticky headers. A hierarchical row source added to that pipeline gives the tree grid, the task table of the Gantt and the row-header hierarchy of the pivot grid.
6. **Docking is a different kind of control.** Its panes hold the app's own UI, which is native framework content, not something the engine draws. Floating windows, tool windows and tear-off are host-windowing features with very uneven support: complete on WPF and Windows Forms, partial on WinUI, desktop-only on Avalonia and Uno, absent on phones, and limited to in-page panels in a browser. It shares almost nothing with the grid engine and should be planned as its own track.
7. **Scheduler and Gantt share a date engine, and it is standards work.** Recurrence (RFC 5545), time-zone handling with gaps and overlaps, and working calendars are the hard parts. .NET's default for an ambiguous local time is the opposite of the RFC's rule, so the engine has to resolve gaps and overlaps explicitly.
8. **Accessibility is one node per control today.** The per-cell semantic tree is a draft spec (#79) with no code (`docs/specs/79-accessibility-tree.md:6-7`). Every new control needs the same Core semantic model and the same host adapters, so that work should be generalized once before six controls each invent one.
9. **The frozen 1.0.0 API does not block any of this, if the work is additive.** New controls are new types; shared pieces can move to a new assembly behind type forwards **(inferred to pass package validation; unverified)**. New controls can ship inside stable minors marked experimental, because `docs/VERSIONING.md:28` already excludes experimental members from the compatibility promise.
10. **Recommended order:** foundations (kernel, host kit, drawing v2), then tree grid, charts, scheduler, pivot grid and Gantt, with new framework hosts and docking as two parallel tracks. Details in [D](#d-sequencing-and-dependencies).

---

## A. Current state

### A.1 Hosts that exist

| Framework | Host in repo | Evidence | State |
|---|---|---|---|
| .NET MAUI | `src/KumikoUI.Maui` (`ContentView` on `SKGLView` or `SKCanvasView`) | `src/KumikoUI.Maui/KumikoDataGrid.cs:39`, `src/KumikoUI.Maui/KumikoUI.Maui.csproj:4-5` | Ships in 1.0.0. iOS, Mac Catalyst and Android pass the device suite; Windows is "CI build only; not yet run on a device" (`README.md:121-124`). |
| Uno Platform | `src/KumikoUI.Uno` (`UserControl` on `SKCanvasElement` or `SKXamlCanvas`) | `src/KumikoUI.Uno/KumikoDataGrid.cs:40`, `src/KumikoUI.Uno/KumikoUI.Uno.csproj` (target frameworks `net10.0`, `-ios`, `-android`, `-browserwasm`, `-desktop`, and `-windows10.0.26100` on Windows) | Ships in 1.0.0. Desktop checked on Linux and partly on Windows; macOS input unchecked (#274); WebAssembly checked in Chromium; iOS and Android on simulator and emulator; Windows App SDK head "CI build only" (`README.md:125-129`). Open items are in the 1.0.1 milestone (`docs/ROADMAP.md:119-123`). |
| WinUI (native, without Uno) | none | No project in `KumikoUI.sln` | Absent. The Uno host's Windows head compiles against WinUI types and `SkiaSharp.Views.WinUI` (`src/KumikoUI.Uno/KumikoUI.Uno.csproj`, the `-windows` item group), so most of a WinUI host exists as source. Whether a plain WinUI 3 app can consume the `KumikoUI.Uno` package's Windows build without the Uno runtime is **(unverified)**. |
| WPF | none | — | Absent. |
| Windows Forms | none | — | Absent. |
| Avalonia | none | — | Absent. |
| Blazor | none | — | Absent. |

The README's custom-host section (`README.md:1780-1803`) shows that a third party can host the engine on any SkiaSharp surface, so new hosts are possible without Core changes. Nothing in the roadmap plans one (`docs/ROADMAP.md` has no entry for the five missing frameworks).

### A.2 What each layer owns

| Layer | Owns | Evidence |
|---|---|---|
| `KumikoUI.Core` (`net10.0`, no dependencies) | Data pipeline, layout, the frame, hit testing, input, selection, editing, drawn editor components, popups and focus, themes and styles, strings, the drawing abstraction and its value types | `docs/ARCHITECTURE.md:49-63`; folder list at `docs/ARCHITECTURE.md:10-19` |
| `KumikoUI.SkiaSharp` (`net10.0`) | `SkiaDrawingContext`, the per-control resource cache (paints, fonts, typefaces, text measurements, run layouts), font registration and fallback, HarfBuzz shaping, image decoding and cache | `src/KumikoUI.SkiaSharp/SkiaDrawingContext.cs:19`, `SkiaResourceCache.cs:42`, `SkiaFontRegistrar.cs:54`, `KumikoImageCache.cs:32`, `GlyphShaper.cs:18` |
| Host (`KumikoUI.Maui`, `KumikoUI.Uno`) | The control type and its bindable properties, the surface and frame loop, pointer and key translation, native text input and input methods, clipboard, haptics, cursor, system theme, on-screen keyboard avoidance, the accessibility peer | `docs/ARCHITECTURE.md:112-120`, `:184-191`, `:202-203` |
| `KumikoUI.SourceGenerators` | Reflection-free property accessors for `[KumikoGridItem]` types, packed inside the Core package | `src/KumikoUI.Core/KumikoUI.Core.csproj:24-37` |

### A.3 What is reusable outside a grid

**Reusable as it stands (public, no grid concepts):**

| Piece | Where | Notes |
|---|---|---|
| Drawing abstraction and value types | `src/KumikoUI.Core/Rendering/IDrawingContext.cs:13`, `KumikoPaint.cs:7`, `KumikoFont.cs:12`, `KumikoColor.cs`, `KumikoRect.cs`, `KumikoPoint.cs`, `KumikoSize.cs` | Sufficient for grids, scheduler blocks and docking chrome. Not sufficient for charts (see A.4). |
| Text layout and wrapping | `src/KumikoUI.Core/Rendering/KumikoTextLayout.cs:19`; wrap overloads at `IDrawingContext.cs:141`, `:206` | Horizontal text only. No rotated text. |
| The whole SkiaSharp layer | `src/KumikoUI.SkiaSharp/*` | One `SkiaResourceCache` per control is the documented model (`README.md:1780-1803`), which already suits several controls on one page. |
| Drawn component base and editors | `src/KumikoUI.Core/Components/DrawnComponent.cs:10` (draw, pointer, key, focus and animation hooks); text box, combo box, date picker, numeric up-down, scroll picker, filter popup (`docs/ARCHITECTURE.md:126-138`) | The date picker already draws a month calendar (`DrawnDatePicker.cs:34-42`), a starting point for the scheduler's navigator. The text box is single-line (`DrawnTextBox.cs:11`). |
| Pointer, key and text-input contracts | `src/KumikoUI.Core/Input/KumikoPointerEventArgs.cs:6`, `KumikoKeyEventArgs.cs:6`, `KumikoKey.cs`, `ITextInputSession.cs:31`, `IKumikoClipboard.cs`, `KumikoCursor.cs`, `KumikoHapticKind.cs` | Host-neutral and control-neutral. |
| Scroll physics and scroll state | `src/KumikoUI.Core/Input/InertialScroller.cs:6`, `ScrollSettings.cs`, `src/KumikoUI.Core/Models/ScrollState.cs:7` | `ScrollState` is generic offsets, extents and viewport. Its doc comments speak of rows and columns but the type does not depend on them. |
| Property accessors and value conversion | `src/KumikoUI.Core/Data/KumikoAccessorRegistry.cs:22`, `KumikoValueConverter.cs`, the source generator | Control-neutral in behavior. The attribute name `[KumikoGridItem]` (`KumikoGridItemAttribute.cs:15`) is grid-flavored but frozen. |
| Comparison and formatting | `src/KumikoUI.Core/Models/KumikoComparer.cs:18` | Culture-aware, never throws; usable by pivot and chart category sorting. |
| Icons | `src/KumikoUI.Core/Rendering/KumikoIconProvider.cs:12`, `IKumikoIconProvider.cs` | |
| Keyboard avoidance math | `src/KumikoUI.Core/Layout/KumikoKeyboardAvoidance.cs:19` | |
| TSV field escaping | `src/KumikoUI.Core/Input/KumikoTsvFormatter.cs:16` | Clipboard for pivot and tree grid. |

**Reusable in substance but locked inside the grid (internal, or bound to grid types):**

| Piece | Where | What blocks reuse |
|---|---|---|
| Popup stack and input priority | `src/KumikoUI.Core/Components/PopupManager.cs:21` (`internal`) | Owned by `KumikoInputController`; reached by hosts through `InternalsVisibleTo` (`KumikoUI.Core.csproj:48-49`). |
| Drawn focus model | `src/KumikoUI.Core/Components/DrawnFocusManager.cs:9` (`internal`) | Same. |
| Paint memoization, hover and press feedback, transitions | `Rendering/PaintMemo.cs`, `Input/InteractionAnimations.cs:11`, `Input/InteractionFeedback.cs` (all `internal`) | Keyed on grid hit regions. |
| View pipeline: filter, sort, group, summarize on a worker, swap a snapshot | `src/KumikoUI.Core/Models/ViewBuilder.cs:112-142`, `ViewSnapshot.cs:11`, `KumikoSource.Background.cs` (all `internal` except the source) | Driven by `KumikoColumn` and the flat row list. |
| Streaming aggregates | `src/KumikoUI.Core/Models/SummaryAggregates.cs:14` (`internal sealed class PropertyAggregate`) | Sum, Average, Count, Min, Max, Custom (`SummaryType.cs:6-20`); values are boxed (`docs/research/SIMD.md`, "The work is boxed"). |
| Flattened hierarchy with levels | `src/KumikoUI.Core/Models/FlatViewRow.cs:8` (`internal readonly struct`), `GroupKeyPath.cs:9`, `GroupHeaderInfo.cs:7`, `Layout/PinnedGroupHeaders.cs:28` | Rows are Data, GroupHeader or GroupSummary; a data row cannot have children. |
| Frame scheduler, surfaces, caret blink | `src/KumikoUI.Maui/Rendering/KumikoFrameScheduler.cs:36` and `src/KumikoUI.Uno/Rendering/KumikoFrameScheduler.cs` (identical apart from comments), `KumikoSurface.cs:28`, `IKumikoSurface.cs:18`, `KumikoCaretBlink.cs` | `internal` to each host assembly and duplicated between them. |
| Host input and text-input adapters | `src/KumikoUI.Maui/AppleTextInputBridge.cs`, `AndroidTextInputMapper.cs`, `Input/TextEditContextState.cs`; `src/KumikoUI.Uno/Input/KumikoTextInputAdapter.cs`, `PointerMapping.cs`, `InputMapping.cs`, `LongPressTracker.cs` | Wired into the `KumikoDataGrid` partial classes. |
| Announcement text | `src/KumikoUI.Core/Models/KumikoAnnouncements.cs:11` | Public, but the strings are grid strings (`KumikoStrings.cs`). |

**Grid-specific (not reusable; new controls need their own):**

| Piece | Where | Why |
|---|---|---|
| The frame | `src/KumikoUI.Core/KumikoRenderer.cs:14`, `RenderFrame` at `:236`, pane passes at `:363-438` | One sealed class that draws headers, three column panes, group rows, summaries, drag overlays and popups. `RowHeight` appears 61 times. |
| Input controller | `src/KumikoUI.Core/Input/KumikoInputController.cs:14`, `HandlePointer` at `:339`, `HandleKey` at `:510` | Sorting, column resize and reorder, row drag, filter popup and cell editing in one class. |
| Hit testing | `src/KumikoUI.Core/Input/KumikoHitTester.cs:10`, `HitRegion.cs:8-36` | 13 grid regions in a closed enum. |
| Layout | `src/KumikoUI.Core/Layout/KumikoLayoutEngine.cs:10`; row range at `:509-531` | Column widths and a visible row range computed as `scrollY / RowHeight`. Every row is the same height (`:491-494`; `docs/specs/102-variable-row-height.md`, summary). |
| Selection | `src/KumikoUI.Core/Models/KumikoSelectionModel.cs:11`, constructor at `:25` | Bound to a `KumikoSource`; rows and a current cell only. |
| Data source | `src/KumikoUI.Core/Models/KumikoSource.cs:31` (2,633 lines) | Flat items with filter, sort, group and summaries. No parent-child relation between items. |
| Style and theme | `src/KumikoUI.Core/Models/KumikoStyle.cs:8`, `KumikoTheme.cs:9`, `Rendering/PaintCache.cs:14` | A flat bag of grid colors, fonts and metrics. There is no shared token layer (surface, accent, text, border) that a chart or scheduler could read. |
| Column model | `src/KumikoUI.Core/Models/KumikoColumn.cs`, `KumikoColumnType.cs:11` | |

### A.4 Gaps that matter for new controls

| Gap | Evidence | Who needs it |
|---|---|---|
| No paths, arcs, curves, gradients, dashes, rotation, scale or opacity layers | `IDrawingContext.cs:13-243`: the only transform is `Translate` (`:235`); `KumikoPaint.cs:7-20` | Charts (pie, area fills, smooth lines, rotated axis labels), Gantt (dashed today-line, baseline bars), scheduler (hatched free/busy) |
| No partial redraw | `src/KumikoUI.Uno/Rendering/IKumikoSurface.cs:38`: "neither surface can redraw a sub-rectangle" | Charts with live data, Gantt drag feedback (cost, not correctness) |
| Fixed row height | `KumikoLayoutEngine.cs:491-531` | Scheduler agenda view, tree grid with wrapped text; spec #102 is a draft |
| No hierarchy in the row source | `FlatViewRow.cs:8-38` | Tree grid, Gantt, pivot |
| One accessibility node per control | `README.md:1920-1926`; no `IKumikoSemanticsProvider` in `src` | All six |
| No right-to-left layout | `README.md:1928`, spec #80 | All six |
| Single-line text editor only | `DrawnTextBox.cs:11` | Scheduler appointment notes (later) |
| No cell-range selection | spec #108 is a draft | Pivot grid |
| No export | `docs/specs/105-export.md` is a draft | Pivot grid, Gantt, charts (image export) |
| No shared theme tokens | `KumikoStyle.cs:8` | All six |

---

## B. Per control

Each section gives scope, architecture fit, platform risk, size and unknowns. Platform risk assumes the framework has a host (see [C.2](#c2-host-kit-and-the-five-missing-hosts)); risks listed are the ones specific to the control.

### B.1 Tree grid

**What the category conventionally includes.** The W3C treegrid pattern describes "a hierarchical data grid consisting of tabular information that is editable or interactive", in which rows and cells are both focusable and the author picks rows-first, cells-first or cells-only focus (https://www.w3.org/WAI/ARIA/apg/patterns/treegrid/). Its keyboard contract: Right Arrow on a collapsed row expands it and on an expanded row moves to the first cell; Left Arrow on an expanded row collapses it; Home, End, Ctrl+Home and Ctrl+End move by row or by cell; Enter on the first cell of a parent row toggles its children when only cells take focus. Parent rows carry `aria-expanded`; rows carry `aria-level`, `aria-setsize` and `aria-posinset`, which WAI-ARIA 1.2 says apply only inside a treegrid (https://www.w3.org/TR/wai-aria-1.2/#row). Virtualized grids state `aria-rowcount` and `aria-rowindex`. UI Automation has no tree-grid control type; a data grid's items "can be structured in a hierarchy", a data item that can expand "must" support the ExpandCollapse pattern, and a tree item with more functionality "should be based on the DataItem control type" (https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-supportdatagridcontroltype, https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-supportdataitemcontroltype, https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-supporttreeitemcontroltype). Level, position in set and size of set are dedicated UIA properties (https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-automation-element-propids).

| Tier | Scope |
|---|---|
| MVP | Hierarchical binding by child-collection accessor and by parent-id (self-referencing) list; expander column with indentation and chevron; expand, collapse, expand-all, collapse-all, expand-to-item; everything the flat grid does on rows (columns, frozen panes, cell rendering, selection, editing with the existing cell editors, keyboard) with the treegrid keys added; sort within siblings; filter that keeps ancestors of matches; live updates for child-collection changes; expansion state in the saved layout. |
| 1.0-complete | Load children on demand with a busy indicator; tri-state check-box column with parent-child propagation; per-level summaries (node aggregates rolling up); drag and drop to reorder and reparent with a non-drag alternative (WCAG 2.5.7, https://www.w3.org/WAI/WCAG22/Understanding/dragging-movements.html); sticky ancestor rows while scrolling; row focus mode as well as cell focus; the semantic tree (ExpandCollapse, level, position, size) once [C.6](#c6-semantic-accessibility-tree) exists. |
| Later | Variable row height (spec #102); cell-range selection (spec #108); export preserving outline levels (spec #105); mixed-type nodes with per-level column sets. |

**Architecture fit.**

- Reuses: nearly the whole grid. The flattened view with levels already exists for grouping (`FlatViewRow.cs:8`, `GroupHeaderInfo.cs:7`), as do per-group expansion state, `ExpandToItem` (`KumikoSource.cs:1389`), chevron hit regions (`HitRegion.cs:26-27`), the expand and collapse slide (`docs/INTERACTION.md:52`) and sticky headers (`PinnedGroupHeaders.cs:28`).
- New shared infrastructure: the hierarchical row source ([C.4](#c4-hierarchical-row-source)).
- Refactor first: the view builder must accept data rows that have children; today only group headers do (`FlatViewRow.cs:18-26`). Sort and filter must run per sibling set (`ViewBuilder.cs:142`). The renderer's first-column drawing needs an indent and expander slot (`KumikoRenderer.cs:1067`, `DrawRowCells`).
- Layout: no new package. Add the feature to `KumikoUI.Core` and to each host. Whether it surfaces as a mode of `KumikoDataGrid` or as a separate `KumikoTreeGrid` control over the same engine is an open question ([F.6](#f-open-questions)).

**Platform risk.** Low and uniform: the control draws on one surface and needs no host feature the grid does not already use. Touch targets for the expander follow the existing chevron rules (`docs/INTERACTION.md:25`). On phones, deep indentation eats column width; an indent cap is the usual mitigation **(inferred)**.

**Size: M.** Reasoning: one new data-shape in an existing pipeline, small renderer and input additions, no new host code beyond a few bindable properties per host. It is L if the maintainer wants it as a separate control type with its own host surface in every framework.

**Unknowns to prototype.** (1) Whether grouping and hierarchy can be active together, and what that means. (2) Incremental update cost when a deep subtree is inserted, against the O(1) add and remove path (`KumikoSource.cs:2213`). (3) Background rebuild with a recursive child accessor that must only read (`docs/ARCHITECTURE.md:176`).

### B.2 Charts

**What the category conventionally includes.** There is no single specification. First-party guidance agrees on a core. Apple's guidance names bar, line and point marks, axes with a fixed or dynamic range, a legend for properties not shown by position, and tells authors not to "require interaction to reveal critical information" and to avoid "relying solely on color" (https://developer.apple.com/design/human-interface-guidelines/charts). Apple's charting framework documents area, line, point, rectangle, rule, bar and sector marks, and scale types category, date, linear, log, square root, symmetric log and power, plus axis scrolling and point and range selection (https://developer.apple.com/documentation/charts, https://developer.apple.com/documentation/charts/scaletype). Microsoft's Office documentation lists column, line, pie and doughnut, bar, area, scatter and bubble, stock, surface, radar, treemap, sunburst, histogram, box and whisker, waterfall, funnel, combination and map charts, with stacked and 100% stacked variants (https://support.microsoft.com/en-us/office/available-chart-types-in-office-a6187218-807e-4103-9e0a-27cdb19afb90), and notes that "a logarithmic scale cannot be used for negative values or zero" (https://support.microsoft.com/en-us/office/change-the-scale-of-the-vertical-value-axis-in-a-chart). For accessibility, WCAG 2.2 requires a text alternative (1.1.1), information not carried by color alone (1.4.1), 3:1 contrast for graphical objects (1.4.11) and keyboard operation (2.1.1) (https://www.w3.org/TR/WCAG22/). The WAI-ARIA Graphics Module defines only `graphics-document`, `graphics-object` and `graphics-symbol` and defers data-rich graphics (https://www.w3.org/TR/graphics-aria-1.0/). The W3C tutorial asks for a short description plus a long one that is "a textual representation of the essential information", which can be a data table (https://www.w3.org/WAI/tutorials/images/complex/). UI Automation has no chart control type; charts "will support the Image control type" with help text for a long description (https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-supportimagecontroltype). Apple has a chart descriptor API from which VoiceOver builds an audio graph (https://developer.apple.com/documentation/accessibility/audio-graphs).

| Tier | Scope |
|---|---|
| MVP | Cartesian chart with line, area, column and bar, scatter, and stacked and 100% stacked variants; pie and doughnut; numeric linear, category and date-time axes with automatic "nice" ticks and label collision handling; multiple series bound through the existing accessors; legend; title; tooltip and crosshair on hover or tap; point selection; themes derived from the suite tokens; a text summary and data-table alternative for assistive technology; image export through the SkiaSharp layer. |
| 1.0-complete | Zoom and pan (wheel, pinch, drag, range selector) with inertial scrolling; logarithmic axis; secondary axes and combination charts; range, step, spline, bubble, candlestick and OHLC series; annotations and reference lines; data labels with overlap avoidance; live data with a ring buffer and decimation for large series; keyboard navigation between points; non-color encodings (markers, dash patterns); animations honoring reduced motion. |
| Later | Histogram, box plot, waterfall, funnel, radar and polar; treemap and sunburst; heatmap; sparklines as a grid cell renderer; trend lines; audio-graph descriptor on Apple platforms. |

**Architecture fit.**

- Reuses: `IDrawingContext` and the SkiaSharp caches; text measurement; accessors and the source generator for series binding; `KumikoComparer` for category order; `InertialScroller` for pan; the popup manager for tooltips; the host kit. It reuses none of the grid's frame, layout, hit testing or selection.
- New shared infrastructure: drawing v2 ([C.3](#c3-drawing-primitives-v2)) is a hard prerequisite. The scale and axis component ([C.5](#c5-scale-axis-and-time-scale)) is built here first and shared with the scheduler and Gantt. A typed, unboxed numeric column path is needed for large series; the grid's boxed values cost 2.32 ms per 100,000-row sum against 0.026 ms typed (`docs/research/SIMD.md`, summary).
- Refactor first: the control kernel ([C.1](#c1-control-kernel)) so the popup manager, focus and animation tick are usable outside the grid.
- Layout: a new platform-neutral project `src/KumikoUI.Charts` (engine, series, axes, legend), tests in `tests/KumikoUI.Charts.Tests`, with the chart control type added to each host package.

**Platform risk.**

| Framework | Risk |
|---|---|
| Blazor | The SkiaSharp Blazor views are documented for "Blazor WebAssembly applications" (https://learn.microsoft.com/en-us/dotnet/api/skiasharp.views.blazor). Interactive Server rendering has no SkiaSharp surface in the browser **(inferred from the package's browser-only platform attribute; unverified)**. Pointer-move tooltips over interop are chatty if rendered server-side. |
| WPF, Windows Forms, WinUI | Low. Software and GPU surfaces exist (https://learn.microsoft.com/en-us/dotnet/api/skiasharp.views.wpf, https://learn.microsoft.com/en-us/dotnet/api/skiasharp.views.desktop, https://learn.microsoft.com/en-us/dotnet/api/skiasharp.views.windows). |
| .NET MAUI | Low for drawing. Pinch zoom needs multi-touch, which the current host maps as a single active pointer (`docs/INTERACTION.md:15`, "Stale pointer": while one pointer is down others are ignored). |
| Avalonia | Drawing goes through a custom draw operation that leases the Skia canvas, and that "bypasses Avalonia's scene graph caching" (https://docs.avaloniaui.net/docs/graphics-animation/custom-rendering). Fine for a chart; note for live data. |
| Uno Platform | Low on Skia targets. On the Windows App SDK target the hardware surface is unavailable because `SKCanvasElement` covers Skia-rendered targets only (https://platform.uno/docs/articles/controls/SKCanvasElement.html), so the software canvas is used there. |

**Size: XL.** Reasoning: no engine reuse above the drawing layer, a prerequisite change to the drawing abstraction, a wide series catalog, and interaction (zoom, pan, selection, tooltips) that must feel right per input device. The MVP alone is L.

**Unknowns to prototype.** (1) Frame time for 100,000 and 1,000,000 points with decimation, on the software surface and in WebAssembly. (2) Whether default interface members on `IDrawingContext` are enough or a path object needs backend-owned native resources. (3) Pinch-zoom through the existing single-pointer host mapping. (4) The accessible representation per platform, since no platform has a chart role.

### B.3 Scheduler

**What the category conventionally includes.** Microsoft's Outlook documentation lists Day, Work Week, Week and Month views, working hours, first day of week and a second time zone (https://support.microsoft.com/en-us/office/change-how-you-view-your-outlook-calendar-a4e0dfd2-89a1-4770-9197-a3e786f4cd8f), free/busy states and reminders (https://support.microsoft.com/en-us/outlook/create-or-schedule-an-appointment), and editing one occurrence or the series (https://support.microsoft.com/en-us/office/update-or-cancel-a-meeting-you-organized-c6d5a9ba-fc38-4c54-9375-a77bdbc7cc30). Apple's event framework models a recurrence rule with frequency, interval, days of week, month and year, set positions and an end by date or count, and a span of "this event" or "future events" (https://developer.apple.com/documentation/eventkit/ekrecurrencerule, https://developer.apple.com/documentation/eventkit/ekspan). Resource and timeline views have no first-party design specification; they are convention **(inferred)**.

The data standard is RFC 5545 (https://www.rfc-editor.org/rfc/rfc5545):

- Section 3.3.5 defines three forms of date-time: floating, UTC, and local with a time-zone identifier. For a local time that occurs twice, the value "refers to the first occurrence"; for a local time that does not exist, it is interpreted with the offset before the gap.
- Section 3.3.10 defines the recurrence rule: `FREQ` (required), `INTERVAL`, `COUNT` or `UNTIL` (never both), `BYDAY`, `BYMONTHDAY`, `BYMONTH`, `BYSETPOS`, `BYWEEKNO`, `BYYEARDAY`, `BYHOUR`, `BYMINUTE`, `BYSECOND` and `WKST`, with a fixed evaluation order. Invalid dates are skipped.
- Sections 3.8.5.1 to 3.8.5.3: the recurrence set is the rule's dates plus `RDATE`, minus `EXDATE`.
- Section 3.8.4.4: `RECURRENCE-ID` identifies one changed instance, and `RANGE=THISANDFUTURE` extends a change to later ones.
- Section 3.6.1: an event with a date-only start is an all-day event, and the end is non-inclusive.
- Verified erratum 4271 changes how nonexistent local times in recurrences are handled; implement the erratum (https://errata.rfc-editor.org/rfc5545).
- RFC 7986 adds `COLOR` and other display properties (https://www.rfc-editor.org/rfc/rfc7986); RFC 7529 adds non-Gregorian recurrence with `RSCALE` (https://www.rfc-editor.org/rfc/rfc7529).

Time zones: the IANA database is the reference (https://www.iana.org/time-zones). .NET accepts IANA identifiers on every operating system that has time-zone data, with exceptions under NLS or invariant globalization on Windows (https://learn.microsoft.com/en-us/dotnet/api/system.timezoneinfo.findsystemtimezonebyid). `TimeZoneInfo.ConvertTimeToUtc` treats an ambiguous time as standard time and throws on an invalid time (https://learn.microsoft.com/en-us/dotnet/api/system.timezoneinfo.converttimetoutc), which is the opposite of the RFC's first-occurrence rule, so the engine must resolve both cases itself **(inferred)**. Blazor WebAssembly can trim time-zone data with `InvariantTimezone` (https://learn.microsoft.com/en-us/aspnet/core/blazor/globalization-localization). First day of week comes from culture data (https://www.unicode.org/reports/tr35/tr35-dates.html#Week_Data).

Accessibility: UI Automation's Calendar control type models a month date grid with Grid and Table patterns (https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-supportcalendarcontroltype); there is no scheduler control type. The W3C date-picker example gives the month-grid keys: arrows by day and week, Home and End by week, Page Up and Down by month (https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/examples/datepicker-dialog/).

| Tier | Scope |
|---|---|
| MVP | Day, week, work-week and month views; all-day lane; appointment model bound through accessors (start, end, subject, all-day, color, id); overlap layout in columns; current-time indicator; working-hours shading; navigation (today, previous, next, date jump using the existing month calendar); tap and keyboard selection; create by tap or drag on empty time; move and resize by drag with snapping and a non-drag alternative; culture-aware first day of week and time format; a read-only recurrence expander for a documented subset of RFC 5545 (daily, weekly, monthly, yearly with interval, count, until, by-day, by-month-day) with exception dates. |
| 1.0-complete | Full recurrence rule evaluation including set positions and week start; edit occurrence, series, or this-and-following; time-zone-aware appointments with explicit gap and overlap rules and a second time-zone ruler; agenda (list) view; timeline view with resource grouping; drag between resources and days; an appointment editing surface (see the editor dependency below); iCalendar import and export of events; reminders raised as events for the app to present; the semantic tree. |
| Later | Non-Gregorian recurrence (RFC 7529); multi-day timeline with virtualized resources in the thousands; free/busy overlays; printing; year view. |

**Editor dependency.** Editing an appointment needs a form (subject, dates, recurrence, notes). Editors are out of scope and their cost is per platform. The control should raise an edit-requested event with the appointment and let the app show its own UI, and offer an optional drawn quick-edit popup built from the existing drawn components (text box, date picker with time, combo box). Multi-line notes are not possible with the current single-line text box (`DrawnTextBox.cs:11`).

**Architecture fit.**

- Reuses: the month calendar drawing in `DrawnDatePicker.cs`; scroll physics; drag thresholds and edge autoscroll (`docs/INTERACTION.md:11-13`, `:42`); popups; selection concepts; accessors; background rebuild pattern for recurrence expansion; the host kit.
- New shared infrastructure: the date and calendar engine ([C.7](#c7-date-recurrence-and-working-calendar-engine)); the time scale ([C.5](#c5-scale-axis-and-time-scale)) for the day ruler and the timeline view; two-dimensional virtualization of variable-size items (appointments are not rows).
- Refactor first: the control kernel. Nothing needs to leave the grid specifically.
- Layout: `src/KumikoUI.Scheduling` (date engine, recurrence, appointment layout, view models), shared later with Gantt; control types in the host packages.

**Platform risk.**

| Framework | Risk |
|---|---|
| Blazor | Time-zone data may be trimmed; the engine must detect that and fail clearly. WebAssembly only, as for charts. |
| .NET MAUI, Uno mobile targets | A week view does not fit a phone; day, three-day and agenda views are the realistic set **(inferred)**. Drag to create conflicts with scrolling and needs the long-press convention the grid already uses (`docs/INTERACTION.md:10`). |
| WPF, Windows Forms, WinUI, Avalonia, Uno desktop | Low. |
| All | Reminders and notifications are an app concern, not a control feature. |

**Size: XL.** Reasoning: the recurrence and time-zone engine is a product in itself and must be exact against a standard with 39 errata; four to seven views; drag interactions; editing surface. The MVP with a recurrence subset is L.

**Unknowns to prototype.** (1) A conformance test corpus for recurrence, including the daylight-saving cases. (2) Overlap layout performance for dense days and a year of expanded recurrences. (3) Time-zone data availability per target, especially WebAssembly, Android and iOS **(unverified in first-party docs)**. (4) Whether the timeline view can share the Gantt's time scale and row virtualization without compromise.

### B.4 Docking

**What the category conventionally includes.** Microsoft's Visual Studio documentation is first-party evidence of the convention: tool windows and document windows; a guide diamond with a drop preview while dragging; floating windows, including on other monitors; auto-hide; horizontal and vertical tab groups; pinned tabs; saved layouts and reset (https://learn.microsoft.com/en-us/visualstudio/ide/customizing-window-layouts-in-visual-studio). UI Automation offers a Dock pattern with positions top, left, bottom, right, fill and none, which "does not expose any properties of the docking container" (https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-implementingdock), the Window pattern for floating panes, required also for "resizable split pane controls" (https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-implementingwindow), the Transform pattern for move and resize, and Tab, TabItem, Pane and Thumb control types (https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-supporttabcontroltype, https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-supportthumbcontroltype). W3C patterns give the keyboard contract: a splitter moves with arrow keys, Enter collapses or restores, F6 cycles panes (https://www.w3.org/WAI/ARIA/apg/patterns/windowsplitter/); tabs move with arrows, Delete optionally closes (https://www.w3.org/WAI/ARIA/apg/patterns/tabs/). Windows guidance adds F6 and Shift+F6 between panes and Ctrl+F4 or Ctrl+W to close a tab (https://learn.microsoft.com/en-us/windows/apps/design/input/keyboard-interactions). WCAG 2.5.7 requires every drag operation to have a single-pointer alternative without dragging (https://www.w3.org/WAI/WCAG22/Understanding/dragging-movements.html), so docking by menu ("dock left", "float", "move to tab group") is required, not optional **(the menu is an inferred way to meet it)**.

| Tier | Scope |
|---|---|
| MVP (in-window) | Dock layout tree of splits and tab groups; document area and tool panes; splitters with keyboard support; tabs with close, reorder and overflow; drag a pane or tab to re-dock inside the same window with drop guides and preview; dock, close and move commands in a menu; pane activation and F6 cycling; layout save and restore to JSON; minimum sizes. |
| 1.0-complete (desktop) | Floating windows as real top-level windows owned by the main window; drag between windows and tear-off; auto-hide with slide-out; pinned tabs; maximize a pane; multiple monitors and per-monitor scaling; layout versioning with unknown-pane handling; UI Automation Window, Transform and Dock patterns. |
| Later | Layout presets; nested dock hosts; document wells with side-by-side groups; in-page "floating" panels on single-window targets; pop-out browser windows. |

**Architecture fit.** Docking differs from the other five in a basic way: a pane's content is the app's own view (a native framework element), not data the engine draws.

- A platform-neutral core can own the layout tree, serialization, constraint solving, drop-target geometry, hit testing of chrome, keyboard commands and the semantic model. That part is pure logic, testable without a host, and fits the repo's Core conventions.
- The content must be arranged by the host framework's own layout system, as native children placed in the rectangles the core computes.
- Chrome (tab strips, captions, splitters) can be drawn on a Skia surface behind the content, or built from native elements per host. Drop guides must appear above native content, which a surface behind the content cannot do; that needs an overlay surface, or, for cross-window drags, a transparent top-level window **(inferred)**. Mixing a GPU Skia surface with native siblings can have z-order limits on some frameworks **(unverified)**.
- Reuses from the grid engine: drag thresholds, pointer and key types, theme tokens, icons, the layout-state JSON approach (`src/KumikoUI.Core/Models/KumikoLayoutState.cs`). Nothing else.
- Layout: `src/KumikoUI.Docking` (neutral layout core), and a docking control per desktop-capable host. It does not need drawing v2, the hierarchy source, aggregation or the time scale.

**Platform-specific risk: windowing.** What each framework's first-party documentation says:

| Framework or target | Multiple top-level windows | Owned or tool windows | Programmatic move and resize | Drag between windows | Verdict for floating panes |
|---|---|---|---|---|---|
| WPF | Yes. Modeless windows by `Show`; one dispatcher thread can manage several (https://learn.microsoft.com/en-us/dotnet/desktop/wpf/windows/, https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/threading-model) | Yes. `Window.Owner`: "An owner window can never cover an owned window" (https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.owner); `WindowStyle.ToolWindow` (https://learn.microsoft.com/en-us/dotnet/api/system.windows.windowstyle) | Yes. `Left`, `Top`, `Width`, `Height` | Drag and drop "within a single application, or between different applications" (https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/drag-and-drop-overview) | Full |
| Windows Forms | Yes. `Form.Show(owner)` (https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.form.show) | Yes. Owned forms "are also never displayed behind their owner form" (https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.form.owner); tool-window border styles (https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.formborderstyle); MDI (https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.form.ismdicontainer) | Yes | Yes (https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.control.dodragdrop) | Full |
| WinUI 3 (Windows App SDK) | Yes, on one thread (https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.window) | Partial. A tool-window presenter exists (https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.windowing.overlappedpresenter.createfortoolwindow), but "to set the owner window in a WinUI app requires Win32 interop" (https://learn.microsoft.com/en-us/windows/apps/develop/ui/manage-app-windows) | Yes, in physical pixels through `AppWindow.Move` and `Resize` (https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.windowing.appwindow) | Drag and drop works app-to-app (https://learn.microsoft.com/en-us/windows/apps/develop/data/drag-and-drop) but `StartDragAsync` is not supported when elevated. The framework's own tab control has tear-out from SDK 1.6 that "doesn't use drag-and-drop APIs" (https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/tab-view); the documented primitive for a custom control is `InputNonClientPointerSource` with its move-size events (https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.input.inputnonclientpointersource) | Feasible with interop |
| .NET MAUI | Yes on Android, iPadOS, Mac Catalyst and Windows; "Multi-window support doesn't work on iOS for iPhone" (https://learn.microsoft.com/en-us/dotnet/maui/user-interface/controls/window) | No API found. On Windows the native window is reachable through the handler | Position and size on Mac Catalyst and Windows only (same page; older versions of the page disagree about Mac Catalyst) | Between apps on some platforms (https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/gestures/drag-and-drop); between two windows of one app **(unverified)**. Pointer positions are window-relative with no capture API (https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/gestures/pointer) | Not through MAUI APIs; native code per platform |
| Avalonia, desktop | Yes. `Show(owner)` "opens the window as a child" (https://docs.avaloniaui.net/docs/reference/controls/window) | Yes. Recommended for "tool or settings windows" (https://docs.avaloniaui.net/docs/app-development/window-management); `Topmost`, `ShowInTaskbar` | Yes. `Position` in screen coordinates; `BeginMoveDrag` (https://docs.avaloniaui.net/api/avalonia/controls/window) | Drag and drop between controls is documented (https://docs.avaloniaui.net/docs/input-interaction/drag-and-drop); across windows **(unverified)** | Full on Windows, macOS and X11. On the opt-in native Wayland backend the compositor owns placement and moves are ignored, per a merged framework change (https://github.com/AvaloniaUI/Avalonia/pull/22292); the docs site does not state it |
| Avalonia, mobile and browser | Single view: these platforms "only allow one view on the device's screen at a time" (https://docs.avaloniaui.net/docs/concepts/application-lifetimes) | No | No | No | Not applicable |
| Uno, Windows App SDK target | As WinUI 3 **(inferred: this target is native WinUI)** | As WinUI 3 | As WinUI 3 | As WinUI 3 | Feasible with interop |
| Uno, Skia desktop (Windows, macOS, Linux X11) | Yes: "All Uno Platform desktop targets support displaying secondary windows" (https://platform.uno/docs/articles/features/windows-ui-xaml-window.html) | **(unverified)** | Partial: `AppWindow` resize and presenters are documented on that page; per-target move **(unverified)** | In-app drag and drop "is supported on all platforms" (https://platform.uno/docs/articles/features/pointers-keyboard-and-other-user-inputs.html); across windows **(unverified)**. Tab tear-out is disabled pending windowing APIs (https://github.com/unoplatform/uno/issues/21762) | Feasible with caveats; needs a prototype |
| Uno, iOS and iPadOS | Opt-in scenes on iPadOS; otherwise "the app stays single-window" (windowing page above) | **(unverified)** | **(unverified)** | **(unverified)** | Not applicable on phones |
| Uno, Android | "Secondary windows are not supported yet" (windowing page above) | No | No | No | Not applicable |
| Uno, WebAssembly | Not mentioned; single window **(inferred)** | No | No | No | Not applicable |
| Blazor in a browser | Pop-ups only, each needing a user gesture (https://developer.mozilla.org/en-US/docs/Web/API/Window/open) | An always-on-top document picture-in-picture window exists but is "limited to one per browser tab", cannot be positioned by the site and has limited availability (https://developer.mozilla.org/en-US/docs/Web/API/Document_Picture-in-Picture_API) | Only for script-opened pop-ups (https://developer.mozilla.org/en-US/docs/Web/API/Window/moveTo) | Drag data "must be serialized to a string (or as a File)" (https://developer.mozilla.org/en-US/docs/Web/API/HTML_Drag_and_Drop_API); live content cannot follow the pointer between windows | In-page panels only |
| Blazor Hybrid | The native host owns the windows; components render to an embedded web view (https://learn.microsoft.com/en-us/aspnet/core/blazor/hybrid/) | Per host | Per host | Per host | Per host **(inferred)** |

Three cross-cutting gaps: no framework documents that pointer capture keeps delivering events once the pointer leaves the window, which tear-off tracking depends on; same-app drag between windows is stated plainly only for Windows Forms; and Wayland placement rests on one change description. All three need prototypes.

**Where docking is not meaningful.** On phones (MAUI iOS and Android phones, Uno iOS and Android, Avalonia mobile) a docking layout has no room to work and no windows to float; this plan marks those cells not applicable. On tablets and in browsers, in-window docking (splits and tabs, no floating) is meaningful. In a browser the pane content is DOM, so a Blazor docking control is a DOM layout component with the neutral core, not a Skia-drawn control **(inferred)**.

**Size: XL** for the full desktop feature with floating windows on WPF, Windows Forms, WinUI, Avalonia and Uno desktop; **L** for in-window docking only. Reasoning: the neutral core is M, but every host is a separate implementation of windowing, native content hosting, overlays and accessibility, and none of it reuses the grid.

**Unknowns to prototype.** (1) Tear-off drag tracking across windows on each desktop host. (2) Drop-guide overlay above native content on each host. (3) WinUI owner windows through interop and `InputNonClientPointerSource` from a custom control. (4) Uno Skia desktop owned windows and window move per operating system. (5) Wayland. (6) Per-monitor scaling when a pane moves between monitors (WPF documents `Window.DpiChanged`, https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.dpichanged; WinUI sizes are physical pixels).

### B.5 Pivot grid

**What the category conventionally includes.** Microsoft's Excel documentation defines the four areas (filters, columns, rows, values) (https://support.microsoft.com/en-us/office/use-the-field-list-to-arrange-fields-in-a-pivottable-43980e05-a585-4fcd-bd91-80160adfebec); summary functions sum, count, average, max, min, product, count numbers, standard deviation and variance, with distinct count only for model-backed data (https://support.microsoft.com/en-us/excel/calculate-values-in-a-pivottable); "show values as" calculations such as percent of grand, row, column and parent totals, difference from, running total and rank (https://support.microsoft.com/en-us/excel/show-different-calculations-in-pivottable-value-fields); subtotals at top or bottom and grand totals for rows, columns or both (https://support.microsoft.com/en-us/office/show-or-hide-subtotals-and-totals-in-a-pivottable-fc4d8406-f230-4762-aa2f-310826f3e5e2); compact, outline and tabular layouts (https://support.microsoft.com/en-us/excel/design-the-layout-and-format-of-a-pivottable); numeric and date grouping (https://support.microsoft.com/en-us/excel/get-started/group-or-ungroup-data-in-a-pivottable); label and value filters and top-N (https://support.microsoft.com/en-us/office/filter-data-in-a-pivottable-cc1ed287-3a97-4e95-b377-ddfafe79fa8f); and expand, collapse and show-details, which places the underlying rows elsewhere (https://support.microsoft.com/en-us/office/expand-collapse-or-show-details-in-a-pivottable-or-pivotchart-d70d7e70-d230-4d45-81db-1f5e39bcb394).

The OLAP concepts come from Microsoft's Analysis Services documentation: "A cube is defined by its measures and dimensions" (https://learn.microsoft.com/en-us/analysis-services/multidimensional-models/olap-logical/logical-architecture-overview-analysis-services-multidimensional-data); hierarchies have levels and may be balanced, unbalanced or ragged (https://learn.microsoft.com/en-us/analysis-services/multidimensional-models-olap-logical-dimension-objects/user-hierarchies); measures are additive (sum, count), semi-additive (min, max, first and last child, average of children and others) or non-additive (distinct count, none) (https://learn.microsoft.com/en-us/analysis-services/multidimensional-models/use-aggregate-functions). That classification matters for the engine: additive totals can be rolled up from subtotals; the others need the underlying rows **(inferred)**. MDX and XMLA are the query and transport standards for server cubes (https://learn.microsoft.com/en-us/analysis-services/xmla/xml-for-analysis-xmla-reference); drill-through "retrieves the underlying table rows that were used to create a specified cell" (https://learn.microsoft.com/en-us/sql/mdx/mdx-data-manipulation-drillthrough).

Accessibility: a grid with row headers and column headers, spans for merged header cells, and expandable headers. UI Automation's Table pattern lets a cell return all its row and column header items and reports row-major, column-major or indeterminate (https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-implementingtableitem, https://learn.microsoft.com/en-us/windows/win32/api/uiautomationcore/ne-uiautomationcore-roworcolumnmajor); spanned cells report their anchor (https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-implementinggriditem).

| Tier | Scope |
|---|---|
| MVP | In-memory pivot over a list: row fields, column fields, value fields and report filters set in code; sum, count, average, min, max; multi-level row and column headers with expand and collapse; subtotals and grand totals; value formatting; sort by label; virtualized in both directions with frozen row headers; background computation with a snapshot swap; drill-through event giving the underlying items; copy to clipboard. |
| 1.0-complete | Field list UI with drag between areas and a non-drag alternative; distinct count, product, standard deviation, variance and custom aggregates; "show values as" calculations; sort by value; label, value and top-N filters; date and numeric bucketing (year, quarter, month; ranges); compact, outline and tabular layouts; multiple value fields on rows or columns; calculated fields; conditional cell styles; saved layout; cell-range selection; export; incremental recalculation on item changes; the semantic tree with header association. |
| Later | Server sources through a provider interface (XMLA or app-supplied); KPIs; pivot chart linked to the chart control; deferred layout update for large sources. |

**Architecture fit.**

- Reuses: two-pane frozen rendering, cell renderers, text cache, scrolling, selection basics, clipboard formatting, the background rebuild and snapshot swap (`docs/ARCHITECTURE.md:175-176`), group keys (`GroupKeyPath.cs:9`), streaming aggregates (`SummaryAggregates.cs:14`), the comparer, and the tree-grid expander drawing for row headers.
- New shared infrastructure: the aggregation engine ([C.8](#c8-aggregation-engine)) with a cross-tabulation step; virtualized columns generated from data rather than declared (the layout engine assumes a declared `KumikoColumn` list, `KumikoLayoutEngine.cs:46`); multi-level column headers with spans (stacked headers are draft spec #109); cell-range selection (draft spec #108).
- Refactor first: aggregates out of `internal`; the renderer's header drawing (`KumikoRenderer.cs:868-963`) must allow header rows of several levels. This is the strongest argument for a control kernel in which the grid frame is composed of reusable band and pane painters rather than one sealed class.
- Layout: engine in `KumikoUI.Core` beside the grid (it shares the row and cell pipeline) or in `src/KumikoUI.Pivot` if Core should not grow; pivot control per host.

**Platform risk.** Low and uniform for drawing. The field list is a second drawn surface or popup with drag between lists; on phones it needs a different arrangement, and demand for pivot on phones is low by the demand ranking. Blazor is WebAssembly only, and large in-memory pivots run single-threaded there unless threading is enabled, so the background rebuild pattern may run on the UI thread **(inferred)**.

**Size: L.** Reasoning: high engine reuse for display, but a new computation engine with correctness-sensitive totals, a generated two-axis header model, and a field-list UI. XL if server sources are in scope.

**Unknowns to prototype.** (1) Memory and time for 1,000,000 rows by two row fields and two column fields with boxed values against typed columns. (2) Column virtualization with thousands of generated columns. (3) Incremental update of non-additive aggregates. (4) Field list interaction on touch.

### B.6 Gantt

**What the category conventionally includes.** Microsoft's Project documentation states that Gantt views "consist of a table and a bar chart" (https://support.microsoft.com/en-us/project/overview-of-project-views). The scheduling model:

- **Dependencies.** Finish-to-start (the default), start-to-start, finish-to-finish and start-to-finish (https://support.microsoft.com/en-us/project/link-tasks-in-a-project), with lead (negative) and lag (https://support.microsoft.com/en-us/office/add-lead-or-lag-time-to-a-task-692fde75-0734-47b6-a259-e87aa1a538f4).
- **Critical path.** "A chain of linked tasks that directly affects the project finish date" (https://support.microsoft.com/en-us/project/show-the-critical-path-of-your-project-in-project). Total slack is the smaller of late finish minus early finish and late start minus early start (https://support.microsoft.com/en-us/office/total-slack-task-field-55dacfda-95bc-469c-9cc9-b1454b8df42c); free slack is the delay possible "without delaying any successor tasks" (https://support.microsoft.com/en-us/office/free-slack-task-field-87145b2b-8d50-45a1-b89b-89aa73015d17). Early dates come from a forward pass and late dates from a backward pass **(inferred; the Microsoft pages do not use those terms)**.
- **Constraints.** As soon as possible, as late as possible, start or finish no earlier or later than, must start on, must finish on (https://support.microsoft.com/en-us/office/set-a-task-start-or-finish-date-constraint-for-a-task-3a7544fa-e992-4647-911b-54cdbe508784).
- **Calendars.** Base, project, resource and task calendars with exceptions and a defined precedence (https://support.microsoft.com/en-us/office/work-with-calendars-in-project-323ff403-a676-428c-be11-6942bfb7210c, https://support.microsoft.com/en-us/project/how-project-schedules-tasks-behind-the-scenes).
- **Structure and tracking.** Summary tasks and subtasks, milestones, baselines (https://support.microsoft.com/en-us/office/create-or-update-a-baseline-or-an-interim-plan-in-project-desktop-7e775482-ac84-4f4a-bbd0-592f9ac91953), percent complete as actual duration over duration (https://support.microsoft.com/en-us/office/percent-complete-fields-84ec5068-4a34-497c-97eb-e12b6dc47cc5), split tasks, manual and automatic scheduling.
- **Resources.** Assignment, overallocation and leveling that "works by delaying tasks or splitting them" (https://support.microsoft.com/en-us/office/distribute-project-work-evenly-level-resource-assignments-59ee715d-4446-42c9-8756-4ea2a5a7e4a0).
- **Time scale.** "Up to three timescale tiers" (https://support.microsoft.com/en-us/office/change-the-timescale-in-a-project-view-in-project-desktop-d46038c4-e8db-4013-a024-db0251dd8a4f).
- **Interchange.** A documented XML schema (https://learn.microsoft.com/en-us/office-project/xml-data-interchange/introduction-to-project-xml-data?view=project-client-2016).

Accessibility: no Gantt pattern or control type exists. The task table is a treegrid ([B.1](#b1-tree-grid)); the bars can be graphics objects or folded into each row's name and status; bar drags need non-drag alternatives; critical and progress styling must not depend on color alone **(inferred from the sources in B.1 and B.2)**.

| Tier | Scope |
|---|---|
| MVP | Tree-grid task table and a time-scaled bar pane with a splitter and synchronized vertical scrolling; task bars, summary bars, milestones and progress fill; two-tier time scale with zoom levels; nonworking-time shading from one calendar; today line; the four dependency types drawn as link lines (display only, dates supplied by the app); tooltips; selection synchronized between panes; scroll-to-task and scroll-to-date. |
| 1.0-complete | Drag to move, resize, set progress and create links, each with a non-drag alternative and validation events; a scheduling engine: forward and backward pass with the four dependency types, lead and lag, constraints, working calendars and exceptions, summary roll-up, cycle detection; critical path and slack; baselines drawn under bars; three-tier time scale; task editing in the table with the existing cell editors; undo and redo for schedule edits; the semantic tree. |
| Later | Resource assignment and a resource-usage view; leveling; split tasks; effort-driven scheduling and task types; multiple critical paths; interchange import and export; printing across pages. |

**Architecture fit.**

- Reuses: the tree grid whole (left pane); the time scale from charts or scheduler ([C.5](#c5-scale-axis-and-time-scale)); working calendars and recurrence from the date engine ([C.7](#c7-date-recurrence-and-working-calendar-engine)); drawing v2 for dashed lines and arrowheads; scroll physics; drag thresholds; the splitter from docking's neutral core if it exists by then.
- New: synchronized two-pane control composition (two engines sharing one vertical scroll state and one row virtualization); link-line routing; the scheduling engine.
- Refactor first: the grid frame must be embeddable as a pane inside a larger control, sharing `ScrollState` with a sibling pane. Today the renderer assumes it owns the whole surface (`KumikoRenderer.cs:221-236`).
- Layout: `src/KumikoUI.Scheduling` for the calendar and scheduling engines (shared with scheduler), `src/KumikoUI.Gantt` for the bar pane and composition, control per host.

**Platform risk.** Low for drawing. Bar dragging on touch conflicts with two-axis scrolling and needs long-press; a phone shows either the table or the bars, not both **(inferred)**. Blazor is WebAssembly only. Demand on mobile is low by the demand ranking.

**Size: L** for the control with display-only dependencies, **XL** with the scheduling engine, on top of the tree grid, time scale and date engine. Reasoning: it is the most dependent control; most of its cost is paid by earlier milestones if they are built with it in mind.

**Unknowns to prototype.** (1) Two panes on one surface against two surfaces with a native splitter. (2) Link routing readability with hundreds of links. (3) Scheduling-engine correctness against a reference set of projects. (4) Row virtualization shared with the tree grid at 100,000 tasks.

### B.7 Size and dependency summary

| Control | MVP size | Complete size | Engine reuse | Hard prerequisites | Main unknown |
|---|---|---|---|---|---|
| Tree grid | M | M to L | High | Hierarchical row source | Grouping and hierarchy together; deep incremental updates |
| Charts | L | XL | Low (drawing layer only) | Drawing v2, control kernel, scale and axis | Large-series frame time; accessible representation |
| Scheduler | L | XL | Low to medium | Control kernel, date engine, time scale | Recurrence and time-zone conformance |
| Docking | L (in-window) | XL (floating, per host) | Very low | Desktop hosts; none of the data foundations | Cross-window drag and overlays per host |
| Pivot grid | L | L to XL | Medium to high | Aggregation engine, hierarchy headers, kernel band painters | Scale of cross-tab computation; generated columns |
| Gantt | L | XL | Medium (through tree grid) | Tree grid, time scale, date engine, drawing v2 | Pane composition; scheduling engine |

---

## C. Shared foundations

Each item says what exists and what is new. Sizes are relative.

### C.1 Control kernel

**Purpose.** A control-agnostic base in Core that a second control can be built on without copying the grid.

**Exists.** The behaviors exist inside the grid: popup priority (`PopupManager.cs:21`), drawn focus (`DrawnFocusManager.cs:9`), redraw requests and animation ticks (`KumikoInputController.cs:69`, `:197`), cursor and haptic events (`:205-242`), hover and press feedback (`InteractionFeedback.cs`), transitions (`InteractionAnimations.cs:11`), paint memoization (`PaintMemo.cs`), frame statistics (`KumikoFrameStats.cs`), scroll state and inertia.

**New.** A small engine contract that every control implements and every host drives: render a frame into an `IDrawingContext`, handle pointer and key, tick animations, report whether it needs frames, expose the active text-input session, cursor, haptics and redraw requests. The popup manager and focus manager become public or move behind that contract. A shared theme-token layer (surface, text, accent, border, selection, focus, error; light, dark and high contrast) from which `KumikoStyle` and each new control's style derive.

**Refactor.** Split the monolithic frame into reusable painters (header band, row band, pane clip, overlay) so tree grid, pivot and Gantt compose them. Keep `KumikoRenderer` and `KumikoInputController` as the public grid entry points with unchanged signatures.

**Size: L.**

### C.2 Host kit and the five missing hosts

**Purpose.** Make a host a thin wrapper, so a new control costs a few hundred lines per framework instead of thousands, and add the frameworks that have no host.

**Exists.** Two complete hosts with the pieces listed in A.3, written twice and private to the grid: `KumikoFrameScheduler` (identical in both), surfaces, caret blink, pointer and key mapping, text-input adapters for iOS, Android and Windows on MAUI and a proxy text box on Uno, clipboard, haptics, cursor, keyboard avoidance, theme follow and the accessibility peer.

**New, step 1 (refactor the two hosts).** Per host package, an internal base control that owns the surface, frame loop, input translation, text input, clipboard and accessibility bridge and drives any engine that implements the kernel contract. Platform-free pieces (the frame scheduler, caret timing, double-tap and long-press recognition) move to Core or a shared source folder. `KumikoDataGrid` keeps its public surface and derives from or wraps the base.

**New, step 2 (five hosts).** First-party SkiaSharp hosting exists for four of them:

| Framework | Skia surface | Text input and input methods | Accessibility bridge | Notes |
|---|---|---|---|---|
| WinUI 3 | `SKXamlCanvas`, `SKSwapChainPanel` (https://learn.microsoft.com/en-us/dotnet/api/skiasharp.views.windows) | The MAUI host's Windows adapter already targets WinUI text services (`src/KumikoUI.Maui/Input/TextEditContextState.cs`, `KumikoDataGrid.TextInput.Windows.cs`) | Automation peers, as in the Uno host | Cheapest new host: the Uno host's Windows head and MAUI's Windows code are both WinUI code **(inferred)**. |
| WPF | `SKElement`, `SKGLElement` (https://learn.microsoft.com/en-us/dotnet/api/skiasharp.views.wpf) | New adapter (text composition events or the text services framework) | New automation peers; UI Automation patterns are the same as WinUI's | |
| Windows Forms | `SKControl`, `SKGLControl` (https://learn.microsoft.com/en-us/dotnet/api/skiasharp.views.desktop) | New adapter (input-method messages) | New accessible objects or UI Automation providers | No XAML; properties and designer support differ from every other host. |
| Avalonia | No views package. Draw by leasing the Skia canvas inside a custom draw operation (https://docs.avaloniaui.net/docs/graphics-animation/custom-rendering) | New adapter over the framework's text-input client | New automation peers | One host covers Windows, macOS, Linux, and the mobile and browser targets where the framework runs. |
| Blazor | `SKCanvasView`, `SKGLView`, documented for WebAssembly (https://learn.microsoft.com/en-us/dotnet/api/skiasharp.views.blazor) | A hidden DOM input, as the Uno WebAssembly head does through its proxy | A hidden DOM mirror with ARIA roles | WebAssembly render mode only. Interactive Server has no Skia surface in the browser **(inferred; unverified)**; a server option would need a different drawing backend. The SkiaSharp maintainers do not expect the Blazor views to work inside Blazor Hybrid (https://github.com/mono/SkiaSharp/discussions/3185); a hybrid app should use the native host's control. |

**Size.** Step 1: M to L. Step 2: M each for WinUI and WPF, M to L each for Windows Forms and Avalonia, L for Blazor; XL in total. Each host then needs the grid's device verification repeated, which is what the 1.0 cycle spent a preview release on (`docs/ROADMAP.md:44`).

### C.3 Drawing primitives v2

**Exists.** The members in `IDrawingContext.cs:13-243` and `KumikoPaint.cs:7-20`. The SkiaSharp backend can already do everything missing; the abstraction does not expose it.

**New.** A path type (move, line, quadratic and cubic curve, arc, close) with fill and stroke; arcs and circles; linear and radial gradients; dash patterns, caps and joins; rotate and scale transforms; opacity layers; rotated text; optional shadow. A recording test context exists for the current members (`tests/KumikoUI.Core.Tests`, per `docs/ARCHITECTURE.md:35`) and must grow with them.

**API constraint.** `IDrawingContext` is in `PublicAPI.Shipped.txt`. The file already uses default interface members so "existing implementers keep compiling" (`IDrawingContext.cs:57-62`, `:79-84`). Either continue that way, or add a second interface that the SkiaSharp context also implements and that new controls require. See [F.5](#f-open-questions).

**Size: M.**

### C.4 Hierarchical row source

**Exists.** Grouping builds a tree of groups and flattens it with levels and expansion state (`ViewBuilder.cs:142`, `FlatViewRow.cs:8`, `GroupKeyPath.cs:9`, `KumikoSource.cs:1328`, `:1389`, `:1456-1467`). Item identity, selection by item and incremental updates exist for flat lists.

**New.** Parent-child relations between data items: a child-collection accessor, a parent-id mapping, and on-demand loading; a data row kind that carries level, has-children and expanded state; per-sibling sort; ancestor-preserving filter; subtree-aware incremental updates; node roll-up aggregates. Expose a read-only tree view of the flattened result so Gantt and pivot can consume it.

**Size: M to L.** Used by tree grid, Gantt and pivot row headers.

### C.5 Scale, axis and time scale

**Exists.** Nothing. The date picker has month arithmetic only.

**New.** Scales (linear, logarithmic, category, date-time) mapping data to pixels and back; tick generation with "nice" intervals; label formatting through culture; multi-tier time headers (hour, day, week, month, quarter, year) with zoom levels; viewport with zoom and pan limits; working-time shading fed by a calendar. Built once in a neutral project and consumed by charts (axes), scheduler (day ruler, timeline view) and Gantt (time header).

**Size: M.**

### C.6 Semantic accessibility tree

**Exists.** A draft spec for the grid (`docs/specs/79-accessibility-tree.md`): a Core semantics provider producing realized nodes with stable ids, bounds, states and actions, and host adapters for WinUI automation peers, iOS accessibility containers, Android explore-by-touch and an ARIA mirror on WebAssembly. No code. Announcements exist (`KumikoAnnouncements.cs:11`).

**New.** Make the Core model control-neutral before implementing it: roles for grid, tree-grid row with level and expansion, calendar and time-slot grid, chart with series and points and a table alternative, tabs, panes and splitters. Then one adapter per host, shared by all controls. The five new hosts each need an adapter.

**Size: L**, already planned as a 1.x item for the grid; generalizing it first avoids six rewrites.

### C.7 Date, recurrence and working-calendar engine

**Exists.** Nothing beyond .NET's own types.

**New.** Appointment and occurrence model; RFC 5545 recurrence expansion with exceptions and overrides; explicit gap and overlap resolution; all-day and floating times; working calendars (work weeks, exceptions, shifts) and working-time arithmetic (add working duration, next working instant); optional iCalendar read and write. Scheduler uses all of it; Gantt uses working calendars and, later, recurring tasks.

**Size: L.**

### C.8 Aggregation engine

**Exists.** `PropertyAggregate` computes sum, average, count, min, max and custom in one streaming pass and can follow a changed value in constant time for the additive ones (`SummaryAggregates.cs:1-80`); `ViewBuilder` computes group and table summaries off the UI thread (`ViewBuilder.cs:603`). All internal, all boxed.

**New.** A public aggregator contract with the additive, semi-additive and non-additive distinction; distinct count, product, standard deviation and variance; typed column readers to avoid boxing; cross-tabulation (group by two key paths at once) producing a sparse cell store with subtotals; "show values as" post-processing. The grid's summaries move onto it without behavior change.

**Size: M.** Used by grid summaries, tree-grid roll-ups, pivot and chart binning.

### C.9 Already specified 1.x items that new controls lean on

| Spec | Needed by |
|---|---|
| #102 variable row height | Tree grid (later tier), scheduler agenda view |
| #108 cell selection with ranges | Pivot grid |
| #109 stacked headers and merging | Pivot grid column headers |
| #105 export | Pivot grid, Gantt, charts |
| #80 right-to-left | All |

---

## D. Sequencing and dependencies

### D.1 Dependency graph

```
C.1 Control kernel ──┬─► C.2 Host kit (refactor MAUI, Uno) ──► C.2 New hosts (WinUI, WPF, WinForms, Avalonia, Blazor)
                     │
                     ├─► Charts ◄── C.3 Drawing v2
                     │      ▲
                     │      └────── C.5 Scale / time scale ──┬─► Scheduler ◄── C.7 Date engine
                     │                                       │                      │
C.4 Hierarchical source ─► Tree grid ────────────────────────┴─► Gantt ◄────────────┘
          │                    │                                   ▲
          │                    └───────────────────────────────────┘ (C.3 for dashed lines and arrows)
          └─► Pivot grid ◄── C.8 Aggregation engine
                   ▲
                   └── specs #108, #109

C.6 Semantic tree ─► every control (complete tier)

Docking ◄── desktop hosts from C.2 only (independent of C.3 to C.8)
```

### D.2 Recommended order

| Step | Milestone | Why here | Can run in parallel with |
|---|---|---|---|
| 0 | 1.0.1 follow-ups and the package validation baseline | Already planned (`docs/ROADMAP.md:119-123`, `Directory.Build.props:60`); the baseline is what makes later refactors safe. | — |
| 1 | C.1 control kernel and C.2 step 1 (host kit on MAUI and Uno) | Every later milestone pays less once these exist. Doing them before any new control avoids building the second control as a copy of the first. | C.3 drawing v2; C.4 hierarchical source |
| 2 | Tree grid (with C.4) | Highest demand rank, highest reuse, smallest size, and a prerequisite for Gantt and pivot row headers. It validates the kernel with a control that is close to the grid. | First new host (WinUI); C.6 semantic model |
| 3 | Charts MVP (with C.3 and C.5) | Second in demand. First control that shares nothing with the grid, so it proves the kernel and host kit are really control-neutral. Builds the scale component the next two need. | WPF host; C.8 aggregation engine |
| 4 | Scheduler MVP (with C.7) | Third in demand. Builds the date engine Gantt needs. | Windows Forms and Avalonia hosts; docking neutral core |
| 5 | Pivot grid (with C.8, specs #108 and #109) | Reuses the grid; demand concentrated on desktop frameworks, which have hosts by now. | Blazor host; docking on WPF and Windows Forms |
| 6 | Gantt | Needs tree grid, time scale, date engine and drawing v2, all done. | Docking on WinUI, Avalonia and Uno desktop |
| Track A | New hosts (C.2 step 2): WinUI, WPF, Windows Forms, Avalonia, Blazor | Independent of control work once the host kit exists. Order by cost and reach: WinUI first (closest to existing code), then WPF and Windows Forms (largest audiences per the demand input), Avalonia, Blazor last (most different). | Steps 2 to 6 |
| Track B | Docking | Shares nothing with the data controls and needs desktop hosts. Start its neutral core any time; ship per host as each desktop host lands. | Steps 3 to 6 |

This departs from the demand ranking in one place: docking moves from fourth to a parallel track, because it depends on hosts that do not exist yet and on nothing else in the plan. Pivot before Gantt follows the dependency graph and the demand ranking alike.

### D.3 The frozen 1.0.0 API

- The four `PublicAPI.Shipped.txt` files are frozen (`docs/ROADMAP.md:114`); Core's has 2,404 entries. `PublicAPI.Unshipped.txt` files are empty. From 1.0 a breaking change needs a major version and removals need an `[Obsolete]` cycle (`docs/VERSIONING.md:11-16`, `:32-34`).
- **New controls are additive.** New types in new namespaces or new assemblies do not touch shipped entries.
- **The kernel extraction must not move or rename shipped types in a breaking way.** Options: build the kernel beside the grid types inside `KumikoUI.Core`; or move shared types to a new lower-level assembly and leave `[TypeForwardedTo]` in Core, which keeps binary compatibility **(inferred; whether package validation accepts forwarded types without suppressions is unverified)**.
- **`IDrawingContext` can only grow** through default interface members or a companion interface (C.3).
- **Internals shared by friendship do not scale.** Hosts reach Core internals through `InternalsVisibleTo` (`KumikoUI.Core.csproj:48-49`). Every new control assembly would need the same grant; making the popup and focus managers a public kernel contract is cleaner.
- **Names.** Core's central types have no "grid" in their names (`KumikoSource`, `KumikoRenderer`, `KumikoStyle`, `KumikoColumn`, `KumikoSelectionModel`, `KumikoInputController`). They cannot be renamed before 2.0. New controls should take explicit names (`KumikoChart*`, `KumikoScheduler*`) and the kernel should avoid colliding with the generic-sounding grid names. Package descriptions say "for KumikoUI DataGrid" (`KumikoUI.Core.csproj:19`, `KumikoUI.SkiaSharp.csproj`), which is metadata and can change.
- **Preview controls inside stable packages.** Versions are in lockstep (`docs/VERSIONING.md:5`), so a new control cannot be `-preview` while the grid is stable in the same package. `docs/VERSIONING.md:28` excludes "members marked internal or experimental"; marking a new control's API experimental until its own 1.0-complete tier is the mechanism that fits the existing policy.

### D.4 Packaging

Today: one platform-neutral engine package, one drawing package, one package per host, all at one version with exact pins (`README.md:110`).

| Option | Shape | For | Against |
|---|---|---|---|
| A. One package per control per host | `KumikoUI.Charts.Maui`, `KumikoUI.Charts.Wpf`, … | Smallest installs; per-control licensing possible | Up to 6 controls × 7 hosts plus engines: more than 40 packages in lockstep; CI and validation cost per package |
| B. Suite per host | Neutral engine packages per control family; each host package contains every control for that framework | Seven host packages; matches today's shape; trimming removes unused controls | A host package depends on every engine; per-control licensing is harder; docking exists only in desktop-capable hosts |
| C. Family per host | Host packages split by family: grid family (grid, tree grid, pivot), visualization (charts), scheduling (scheduler, Gantt), docking | Middle ground; families match shared engines | About 20 packages |

Recommendation: **B** for the grid family now (tree grid and pivot ship inside the existing packages), with neutral engines as separate projects and packages (`KumikoUI.Charts`, `KumikoUI.Scheduling`, `KumikoUI.Gantt`, `KumikoUI.Docking`) referenced by the host packages. Revisit C if install size or licensing requires it. Keep one version for everything.

Suggested project layout, following the repo's conventions (`src/<Package>`, `tests/<Package>.Tests`, `samples/SampleApp.<Host>`, `docs/nuget/README.<Package>.md`, a `PublicAPI.Shipped.txt` per project):

```
src/
  KumikoUI.Core/            grid family engine + kernel + hierarchy + aggregation (existing)
  KumikoUI.SkiaSharp/       drawing backend incl. drawing v2 (existing)
  KumikoUI.Charts/          neutral: scales, axes, series, legend
  KumikoUI.Scheduling/      neutral: date engine, recurrence, calendars, appointment layout
  KumikoUI.Gantt/           neutral: bar pane, links, scheduling engine
  KumikoUI.Docking/         neutral: layout tree, drop targets, serialization
  KumikoUI.Maui/  KumikoUI.Uno/                       existing hosts, all controls
  KumikoUI.WinUI/ KumikoUI.Wpf/ KumikoUI.WinForms/
  KumikoUI.Avalonia/ KumikoUI.Blazor/                 new hosts, all controls
```

---

## E. Support matrix

### E.1 Planned support

Cell values: **Shipped**; **Planned** (no control-specific obstacle); **Caveats** (feasible with the stated limit); **N/A** (not meaningful). **[H]** means the framework has no host today and the cell depends on the host milestone in C.2. Blazor means the WebAssembly render mode throughout.

| Control | Blazor [H] | WPF [H] | Windows Forms [H] | WinUI [H] | .NET MAUI | Avalonia [H] | Uno Platform |
|---|---|---|---|---|---|---|---|
| Datagrid (baseline) | No host | No host | No host | No host (Uno's Windows head is WinUI code) | Shipped; Windows not yet device-verified | No host | Shipped; see E.2 |
| Tree grid | Planned | Planned | Planned | Planned | Planned | Planned | Planned |
| Charts | Caveats: WebAssembly only; no chart role for assistive technology, table alternative instead | Planned | Planned | Planned | Caveats: pinch zoom needs multi-pointer input the host does not map today | Planned | Planned; see E.2 |
| Scheduler | Caveats: time-zone data can be trimmed; WebAssembly only | Planned | Planned | Planned | Caveats: phones get day and agenda views, not week or timeline | Planned; phone caveat on mobile targets | Planned; phone caveat on mobile targets |
| Docking | Caveats: in-page panels only; no OS floating windows; DOM-based implementation | Planned: full, with owned tool windows | Planned: full, with owned forms | Caveats: owner windows need Win32 interop; tear-off through non-client input APIs | Caveats: in-window docking on Windows, Mac Catalyst and tablets; floating needs native code; N/A on phones | Caveats: full on desktop; placement not honored on native Wayland; N/A on mobile and browser targets (in-window only) | See E.2 |
| Pivot grid | Caveats: large pivots compute on the UI thread unless threading is enabled | Planned | Planned | Planned | Caveats: field list needs a phone layout; low mobile demand | Planned | Planned |
| Gantt | Planned (WebAssembly only) | Planned | Planned | Planned | Caveats: phones show table or bars, not both; drag needs long-press | Planned; phone caveat on mobile targets | Planned; phone caveat on mobile targets |

### E.2 Uno Platform by target

| Control | Windows (Windows App SDK) | Desktop (Windows, macOS, Linux; Skia) | WebAssembly | iOS | Android |
|---|---|---|---|---|---|
| Datagrid (baseline) | Shipped; CI build only, not device-verified | Shipped; macOS input unchecked (#274), Linux assistive tree is a plain panel (#287) | Shipped; checked in Chromium | Shipped; simulator | Shipped; emulator |
| Tree grid | Planned | Planned | Planned | Planned | Planned |
| Charts | Caveats: software canvas only; the hardware element covers Skia targets | Planned | Planned | Planned | Planned |
| Scheduler | Planned | Planned | Caveats: time-zone data | Caveats: phone views | Caveats: phone views |
| Docking | Caveats: as WinUI | Caveats: secondary windows are supported; owned windows, window move and cross-window drag are unverified; no Wayland target | Caveats: in-window only | N/A on phones; in-window on iPadOS, floating not planned | N/A: secondary windows not supported |
| Pivot grid | Planned | Planned | Caveats: UI-thread computation | Caveats: field list layout | Caveats: field list layout |
| Gantt | Planned | Planned | Planned | Caveats: one pane at a time | Caveats: one pane at a time |

### E.3 A matrix the repo can keep current

- **Where:** `docs/SUPPORT-MATRIX.md`, linked from the README's "Supported platforms" section, which today carries the same information for the grid alone (`README.md:114-129`). One table per framework family, rows by control, columns by target, as in E.1 and E.2.
- **Status values:**

| Value | Meaning |
|---|---|
| Shipped | In a stable release and device-verified on that target; link the verification document |
| Shipped, unverified | In a stable release, compiled in CI, not yet run on the target (the README's "CI build only") |
| Experimental | In a release behind the experimental marker; API may change |
| In progress | Milestone open; link it |
| Planned | Accepted, no milestone yet |
| Caveats | Any of the above with a stated limit; the limit is written in the cell |
| N/A | Not meaningful on the target; reason in the cell |
| No host | The framework has no KumikoUI host |

- **Rules:** every cell that is not Shipped or N/A links to a milestone or issue; every Caveats cell states the limit in ten words or fewer and links to detail; the pinned release checklist gets a line "support matrix updated"; the verification documents (`docs/maui-verification.md`, `docs/uno/verification.md`) remain the evidence for Shipped.
- **Keeping it honest:** the repo already fails the build when a document names a type that no longer exists (`eng/DocsIdentifiers`, per `docs/ARCHITECTURE.md:3`). A similar small check can assert that every control type found in a host assembly has a row and that every target framework in a host project has a column **(suggestion)**.

---

## F. Open questions

Each needs a maintainer decision before milestones are written.

| # | Question | Options | Recommendation |
|---|---|---|---|
| 1 | **Seven frameworks: is that the commitment, and in what order?** Five have no host. | (a) All seven, hosts in a parallel track. (b) MAUI and Uno only until two or three controls ship. (c) Add desktop hosts only (WinUI, WPF, Windows Forms, Avalonia); defer Blazor. | (a) with the order WinUI, WPF, Windows Forms, Avalonia, Blazor, and the grid as the first control verified on each. If capacity is the limit, (c). |
| 2 | **Blazor strategy.** | (a) SkiaSharp canvas in WebAssembly only. (b) Also a non-Skia drawing backend for Interactive Server. (c) No Blazor. | (a), documented as WebAssembly only; decide (b) from demand after the first Blazor release. |
| 3 | **Hosts first or controls first?** | (a) Host kit refactor, then controls on MAUI and Uno while new hosts follow. (b) All hosts before any new control. (c) Controls first, refactor later. | (a). (c) repeats the duplication that exists between the two hosts today. |
| 4 | **Where does the kernel live?** | (a) Inside `KumikoUI.Core`, additive. (b) New lower assembly with type forwards from Core. (c) Wait for 2.0 and restructure. | (a) now; consider (b) at 2.0. |
| 5 | **How does `IDrawingContext` grow?** | (a) Default interface members on the frozen interface. (b) A second interface implemented by the SkiaSharp context and required by new controls. | (b) for paths, gradients and transforms, since a default implementation of a gradient or a path cannot be meaningful; (a) only for small additions. |
| 6 | **Tree grid: a mode of `KumikoDataGrid` or a separate control?** | (a) A hierarchy property on the existing control. (b) A `KumikoTreeGrid` type per host over the same engine. | (b) for discoverability and a clean API (tree-only members do not clutter the grid), implemented as a thin subclass or sibling over shared engine code. |
| 7 | **Docking scope and form.** | (a) Full floating windows on desktop hosts, in-window elsewhere. (b) In-window only everywhere. (c) Not part of the Skia-drawn suite; neutral layout core plus native chrome per framework. (d) Drop docking. | (a) for behavior and (c) for implementation: a neutral core, native content hosting, and chrome drawn or native per host as prototypes decide. Do not start before a desktop host exists. |
| 8 | **Scheduler standard conformance.** | (a) Full RFC 5545 recurrence. (b) A documented subset. Also: iCalendar import and export in scope or not; rely on the platform's time-zone data or bundle it. | (b) for the MVP, (a) for the complete tier, with a conformance suite from day one; import and export in the complete tier; platform data with a clear failure when it is missing. |
| 9 | **Pivot data sources.** | (a) In-memory only. (b) A provider interface so apps can plug server cubes. (c) Built-in XMLA client. | (a) for MVP and complete; design the engine boundary so (b) is possible; not (c). |
| 10 | **Gantt: display or scheduling engine?** | (a) Display and editing only; the app owns dates. (b) Built-in forward and backward pass with dependencies, constraints and calendars. (c) Also resources and leveling. | (a) for MVP, (b) for complete, (c) later. |
| 11 | **Chart MVP breadth and performance bar.** | Which series; what point count is promised. | The MVP list in B.2; publish measured numbers rather than a promise, as the grid does (`docs/ROADMAP.md:36`). |
| 12 | **Accessibility bar for new controls.** | (a) Same as the grid at 1.0: one node, announcements, keyboard. (b) Semantic tree required before a control leaves experimental. | (b), with C.6 generalized before the tree grid's complete tier. |
| 13 | **Editing dependency for scheduler and Gantt.** Editors are deferred. | (a) Event only; the app shows its own form. (b) Event plus an optional drawn quick editor from existing components. (c) Wait for editors. | (b). |
| 14 | **Packaging.** | Options A, B, C in D.4. | B, revisited if licensing needs per-control packages. |
| 15 | **Experimental policy.** | (a) New controls ship in stable minors marked experimental. (b) Separate prerelease packages outside the lockstep. (c) Hold each control until complete. | (a); it is what `docs/VERSIONING.md:28` already allows. |
| 16 | **Mobile scope for pivot, Gantt and docking.** | (a) Full parity. (b) Reduced, documented in the matrix. (c) Desktop and tablet only. | (b) for pivot and Gantt; docking N/A on phones. |
| 17 | **Order against the existing 1.x backlog.** Specs #79, #80, #102, #105, #108, #109 are prerequisites for parts of this plan. | (a) Finish the 1.x specs first. (b) Pull each in when a control needs it. | (b), except #79 (semantic tree), which should be generalized early. |

---

## Unverified items

| # | Claim | Why it matters | How to settle it |
|---|---|---|---|
| 1 | A plain WinUI 3 app can consume the `KumikoUI.Uno` package's Windows build without the Uno runtime. | Decides whether a WinUI host is new work or packaging. | Build a WinUI 3 sample against the package. |
| 2 | SkiaSharp's Blazor views do not work under Interactive Server rendering. | Blazor scope. The docs say "Blazor WebAssembly applications"; no page states the negative. | Try it; ask upstream. |
| 3 | Pointer or mouse capture keeps delivering events outside the window on each desktop framework. | Tear-off drag. Every doc speaks of element bounds only. | Prototype per host. |
| 4 | Same-app drag and drop between two windows on WPF, WinUI, Avalonia, Uno and MAUI. | Docking between windows. Stated plainly only for Windows Forms. | Prototype per host. |
| 5 | Programmatic window placement is ignored on Avalonia's native Wayland backend. | Floating panes on Linux. Source is one merged change description, not a docs page. | Test on a Wayland session. |
| 6 | Uno Skia desktop: owned windows, `AppWindow.Move` per operating system, always-on-top. | Floating panes on Uno desktop. | Prototype on Windows, macOS, Linux. |
| 7 | Uno WebAssembly and Avalonia mobile and browser targets are single-window. | Matrix cells marked in-window only. Inferred from lifetime docs and silence. | Confirm with the frameworks' docs or issue trackers. |
| 8 | MAUI window position and size on Mac Catalyst. | The current page says supported; an older version of the same page says not. | Test on .NET 10. |
| 9 | A GPU Skia surface and native sibling elements compose without z-order limits on WPF and Windows Forms. | Docking chrome and overlays. | Prototype. |
| 10 | Package validation accepts types moved to another assembly behind type forwards. | Kernel extraction option (b). | Try with the 1.0.0 baseline. |
| 11 | Time-zone data sources on Linux, Android, iOS and in WebAssembly with `InvariantTimezone`. | Scheduler correctness. | Read runtime docs; test per target. |
| 12 | The WAI-ARIA 1.2 `treegrid` role definition text and any 1.3 changes. | Tree-grid semantics. The row and row-header sections were read; the role section was truncated. | Read https://www.w3.org/TR/wai-aria-1.2/#treegrid. |
| 13 | "Forward pass" and "backward pass" as terms. | Wording only; the early and late date fields are documented. | None needed. |
| 14 | Engine reuse estimates and all sizes. | Planning. They are architectural judgment from reading the code; nothing was measured. | Prototypes listed per control. |

## References

Repository files cited (at commit `15cbb15`): `README.md`, `KumikoUI.sln`, `Directory.Build.props`, `docs/ARCHITECTURE.md`, `docs/ROADMAP.md`, `docs/VERSIONING.md`, `docs/INTERACTION.md`, `docs/research/SIMD.md`, `docs/specs/79-accessibility-tree.md`, `docs/specs/102-variable-row-height.md`, `docs/specs/105-export.md`, and the source files named inline under `src/KumikoUI.Core`, `src/KumikoUI.SkiaSharp`, `src/KumikoUI.Maui` and `src/KumikoUI.Uno`.

External sources are cited inline by URL. They fall into these groups:

- W3C: ARIA Authoring Practices (treegrid, grid, tabs, window splitter, date-picker example), WAI-ARIA 1.2, Core Accessibility API Mappings 1.2, WAI-ARIA Graphics Module 1.0, WCAG 2.2 and its Understanding documents, the WAI images tutorial.
- IETF and IANA: RFC 5545 and its errata, RFC 7986, RFC 7529, the IANA time-zone database.
- Unicode: UTS #35 part 4 (dates), week data.
- Microsoft Learn: UI Automation control types and patterns; WPF, Windows Forms, WinUI and Windows App SDK windowing; .NET MAUI windows, gestures and pointer; Blazor render modes, hybrid and globalization; `TimeZoneInfo`; Analysis Services and MDX concepts; the SkiaSharp views API reference.
- Microsoft Support: Excel PivotTable, Outlook calendar, Project scheduling, Office chart types.
- Apple: Human Interface Guidelines (charts), the charting, accessibility (audio graphs) and event frameworks' documentation.
- Avalonia documentation and repository; Uno Platform documentation and repository; the SkiaSharp repository.
- MDN for browser window, drag-and-drop and picture-in-picture APIs, as a secondary reference to the WHATWG HTML and W3C specifications.

# KumikoUI suite: milestone plan

**Status:** approved by the maintainer on 2026-11-27 and created on GitHub the same day: milestones 7 to 17, tracking issues #433 to #443, seed issues for Foundations (#444 to #460) and Tree grid (#461 to #471), and the twelve labels in section 4. The other nine milestones have a tracking issue only; their issues are split when each implementation session starts. The [roadmap](../ROADMAP.md) has the links. On GitHub, kernel issues carry `area:kernel` and host kit issues `area:hosts` in addition to the labels listed here. The research was done at `15cbb15` on `release/1.0.0`; `main` has since merged grid feature work (variable row height, scrollbars, header menu, search, load-more), so file and line references need re-checking when an issue is picked up. Background and sources are in the [research notes](../research/control-suite-expansion.md); the decisions this plan implements are in that file's Decisions section.

KumikoUI grows from one datagrid into a suite: tree grid, charts, scheduler, pivot grid and Gantt, on six frameworks (.NET MAUI, Uno Platform, WPF, WinUI, Avalonia, Blazor WebAssembly). Docking and Windows Forms were considered and are out of scope.

## 1. Milestones

Eleven milestones, no due dates, ordered by dependency. Each gets a tracking issue (`kind:epic`) that lists its issues and exit criteria.

| # | Milestone | Goal | Depends on | Size |
|---|---|---|---|---|
| 1 | Suite – Foundations | A control-neutral kernel and a per-framework host kit, so a second control is not a copy of the grid | 1.0.1 package validation baseline | L |
| 2 | Suite – Tree grid | `KumikoTreeGrid` on MAUI and Uno | 1 | M |
| 3 | Suite – Charts | Chart control with the shared scale and axis component | 1 | L (MVP), XL (complete) |
| 4 | Suite – Scheduler | Scheduler control with the shared date engine | 1, scale component from 3 | L, XL |
| 5 | Suite – Pivot grid | Pivot control with the shared aggregation engine | 1, 2 | L |
| 6 | Suite – Gantt | Gantt control | 2, time scale from 3, date engine from 4 | L, XL |
| 7 | Host – WPF | Datagrid, then every shipped control, on WPF | 1 | M |
| 8 | Host – WinUI | The same on native WinUI | 1 | M |
| 9 | Host – Avalonia | The same on Avalonia | 1, SkiaSharp version spike | M to L |
| 10 | Host – Blazor WebAssembly | The same on Blazor WebAssembly | 1 | L |
| 11 | Suite – Docs and website | One website for the suite: landing page, per-control docs, API reference, support matrix, live demos | Starts with 2 | M |

Sizes are relative judgment from reading the code; nothing was measured.

### Order

```
1 Foundations ─┬─► 2 Tree grid ─► 3 Charts ─► 4 Scheduler ─► 5 Pivot grid ─► 6 Gantt
               ├─► 7 WPF ─► 8 WinUI ─► 9 Avalonia ─► 10 Blazor WebAssembly      (parallel track)
               └─► 11 Docs and website (starts with 2, grows with each control and host)
```

- The control track builds each control on MAUI and Uno first.
- The host track brings up the datagrid on a new framework, then every control that has shipped by then. A control that ships after a host exists includes that host in its own exit criteria.
- WPF goes first on the host track because host skeletons from Broadside (the maintainer's PDF library, which has shells for all four missing frameworks) port most directly and its Skia surface is the simplest, so it validates the host kit fastest.

### Rules that apply to every milestone

- New controls ship in stable minors, marked experimental until their complete tier and the semantic accessibility tree are done.
- One lockstep version across all packages. New control families get their own neutral package from the start (`KumikoUI.Charts`, `KumikoUI.Scheduling`, `KumikoUI.Gantt`); tree grid and pivot start in `KumikoUI.Core` and move out when the datagrid does.
- Shared engines (scale and axis, date engine, aggregation engine) are built inside the first control that needs them, with no dependency on that control's types.
- The frozen 1.0.0 API is not broken: additions only, and `IDrawingContext` grows through a companion interface.
- Every milestone's exit criteria include: XML docs on every public member, a sample page, package README updates, a CHANGELOG entry, and the support matrix updated in the same PR that changes a status.
- No third-party control vendor or control library is named in issues, docs or milestones.
- Code ported from Broadside is recorded in `THIRD-PARTY-NOTICES.md`.

## 2. Milestone detail

Issue lists are seeds: one line each, to be expanded with acceptance criteria when the milestone's implementation session starts. "Complete tier" items are listed on the tracking issue and split later.

### 1. Suite – Foundations

**Exit criteria:** `KumikoDataGrid` on MAUI and Uno runs on the kernel and host kit with no public API change and no benchmark regression; a trivial second drawn control runs on both hosts using only public kernel types; both spikes have a written result.

| Issue | Labels |
|---|---|
| Kernel: define the control engine contract (render frame, pointer and key input, animation tick, needs-another-frame, text-input session, cursor, haptics, redraw request) with a fake host for tests | `area:core` `kind:enhancement` |
| Kernel: make the popup manager and drawn focus manager usable by any control without `InternalsVisibleTo` | `area:core` `area:api` |
| Kernel: split the grid frame into reusable painters (header band, row band, pane clip, overlay) and let a grid pane share scroll state with a sibling pane | `area:rendering` `kind:tech-debt` |
| Kernel: shared theme tokens (surface, text, accent, border, selection, focus, error; light, dark, high contrast) that `KumikoStyle` derives from | `area:core` |
| Kernel: multi-pointer gesture recognizer (pan, pinch, wheel zoom about a point, fling) for controls that opt in | `area:input` |
| Kernel: viewport type with clamp, zoom-at-point and re-anchor after relayout | `area:layout` |
| Host kit: compile the platform-free host controller once (frame scheduler, caret timing, tap, double-tap and long-press recognition, key and pointer normalization) | `area:maui` `area:uno` `kind:tech-debt` |
| Host kit: MAUI base control owning surface, frame loop, input translation, text input, clipboard and accessibility bridge; move `KumikoDataGrid` onto it | `area:maui` |
| Host kit: Uno base control, the same | `area:uno` |
| Drawing: companion interface for paths, arcs, curves, gradients, dashes, caps and joins, rotate and scale, opacity layers and rotated text, with the recording test context and a Skia path cache | `area:rendering` `area:skiasharp` `area:api` |
| Data: hierarchical row source (child accessor, parent-id mapping, level and expansion state, per-sibling sort, ancestor-preserving filter, subtree-aware updates) | `area:core` |
| Accessibility: control-neutral semantic tree design, generalizing spec #79 (nodes, states, change notifications, element source, roles for grid, tree row, calendar, chart, tabs) | `area:a11y` `kind:spec` |
| Accessibility: Core semantic model and MAUI and Uno adapters | `area:a11y` |
| Spike: can shipped types move to a new assembly behind type forwards and pass package validation? Decides whether the datagrid package split lands in 1.1 or 2.0 | `area:packaging` `kind:spec` |
| Spike: SkiaSharp version compatibility on Avalonia and Uno against the pinned version | `area:skiasharp` `kind:spec` |
| Policy: experimental marking for new controls (attribute, diagnostic ids, `VERSIONING.md` wording) | `area:api` `area:docs` |
| Repo: `THIRD-PARTY-NOTICES.md` entry and rule for code ported from Broadside | `area:repo` |

### 2. Suite – Tree grid

**MVP exit criteria:** `KumikoTreeGrid` on MAUI and Uno binds a hierarchy two ways, does everything the flat grid does on rows, and follows the W3C treegrid keyboard contract.

| Issue | Labels |
|---|---|
| `KumikoTreeGrid` control type on MAUI and Uno over the shared engine | `area:treegrid` `area:maui` `area:uno` |
| Binding: child-collection accessor and self-referencing parent-id list, including source-generated accessors | `area:treegrid` `area:core` |
| Expander column: indentation, chevron, hit region, indent cap for narrow screens | `area:treegrid` `area:rendering` |
| API: expand, collapse, expand all, collapse all, expand to item, with events | `area:treegrid` `area:api` |
| Keyboard: treegrid keys (Right and Left expand, collapse and move; Home, End and Ctrl variants) | `area:treegrid` `area:input` |
| Sort within siblings and filter that keeps ancestors of matches | `area:treegrid` `area:core` |
| Live updates for child-collection changes | `area:treegrid` `area:core` |
| Expansion state in the saved layout | `area:treegrid` |
| Spike: grouping and hierarchy active together, and what that means | `area:treegrid` `kind:spec` |
| Benchmark: deep subtree insert and 100,000-node expand | `area:treegrid` `area:perf` |
| Sample pages and docs | `area:treegrid` `area:docs` |

**Complete tier (split later):** load children on demand; tri-state check-box column; per-level roll-up summaries; drag to reorder and reparent with a non-drag alternative; sticky ancestor rows; row focus mode; semantic tree (expand/collapse, level, position, set size).

### 3. Suite – Charts

**MVP exit criteria:** a Cartesian and a pie chart on MAUI and Uno with tooltips, legend and selection, a text and data-table alternative for assistive technology, and published frame times for large series.

| Issue | Labels |
|---|---|
| `src/KumikoUI.Charts` neutral package and chart control types on MAUI and Uno | `area:charts` `area:packaging` |
| Scale and axis component: linear, category and date-time scales, nice ticks, label collision, culture formatting (portable; reused by scheduler and Gantt) | `area:charts` `area:core` |
| Series binding through accessors with a typed, unboxed numeric path | `area:charts` `area:perf` |
| Series: line, area, column, bar, scatter; stacked and 100% stacked variants | `area:charts` `area:rendering` |
| Series: pie and doughnut | `area:charts` `area:rendering` |
| Legend and title | `area:charts` |
| Tooltip and crosshair on hover and tap; point selection | `area:charts` `area:input` |
| Theme derived from the suite tokens; non-color encodings available from the start | `area:charts` `area:a11y` |
| Accessibility: text summary and data-table alternative | `area:charts` `area:a11y` |
| Image export through the SkiaSharp layer | `area:charts` `area:skiasharp` |
| Benchmark: 100,000 and 1,000,000 points on software, GPU and WebAssembly surfaces | `area:charts` `area:perf` |
| Sample pages and docs | `area:charts` `area:docs` |

**Complete tier:** zoom and pan with range selector; logarithmic axis; secondary axes and combination charts; range, step, spline, bubble, candlestick and OHLC series; annotations and reference lines; data labels; live data with decimation; keyboard navigation between points; animations honoring reduced motion. **Later:** statistical, polar and hierarchical chart types; sparklines as a grid cell renderer.

### 4. Suite – Scheduler

**MVP exit criteria:** day, week, work-week and month views on MAUI and Uno; create, move and resize appointments by drag and by a non-drag alternative; a documented RFC 5545 recurrence subset passing a conformance suite.

| Issue | Labels |
|---|---|
| `src/KumikoUI.Scheduling` neutral package and scheduler control types on MAUI and Uno | `area:scheduler` `area:packaging` |
| Date engine: appointment and occurrence model, all-day and floating times, explicit gap and overlap resolution (portable; reused by Gantt) | `area:scheduler` `area:core` |
| Date engine: recurrence expander for the documented subset (daily, weekly, monthly, yearly; interval, count, until, by-day, by-month-day; exception dates) | `area:scheduler` |
| Conformance suite for recurrence, including daylight-saving cases and the verified errata | `area:scheduler` `area:tests` |
| Date engine: working calendar (work week, working hours, exceptions) and working-time arithmetic | `area:scheduler` |
| Spike: time-zone data availability per target (WebAssembly, Android, iOS, Linux) and a clear failure when missing | `area:scheduler` `kind:spec` |
| Views: day, week, work-week with all-day lane, overlap layout, current-time indicator, working-hours shading | `area:scheduler` `area:rendering` |
| View: month | `area:scheduler` `area:rendering` |
| Navigation: today, previous, next, date jump; culture-aware first day of week and time format | `area:scheduler` `area:i18n` |
| Interaction: select, create by tap or drag, move and resize with snapping, plus non-drag alternatives | `area:scheduler` `area:input` |
| Editing: edit-requested event and an optional drawn quick editor | `area:scheduler` `area:editing` |
| Phone layout: day, three-day and agenda-style fallback | `area:scheduler` `area:layout` |
| Sample pages and docs | `area:scheduler` `area:docs` |

**Complete tier:** full recurrence rule evaluation; edit occurrence, series, or this-and-following; time-zone-aware appointments and a second time-zone ruler; agenda view; timeline view with resources; iCalendar import and export; reminders as events; semantic tree.

### 5. Suite – Pivot grid

**MVP exit criteria:** an in-memory pivot on MAUI and Uno with row, column, value and filter fields set in code, multi-level headers with expand and collapse, correct subtotals and grand totals, and published numbers for 1,000,000 source rows.

| Issue | Labels |
|---|---|
| Pivot control types on MAUI and Uno | `area:pivot` `area:maui` `area:uno` |
| Aggregation engine: public aggregator contract (additive, semi-additive, non-additive), typed column readers; grid summaries move onto it unchanged (portable) | `area:pivot` `area:core` |
| Aggregation engine: cross-tabulation into a sparse cell store with subtotals and grand totals | `area:pivot` `area:core` |
| Columns generated from data, virtualized in both directions | `area:pivot` `area:layout` |
| Multi-level column headers with spans (implements the stacked-header part of spec #109) | `area:pivot` `area:rendering` |
| Row headers with expand and collapse, reusing the tree grid expander | `area:pivot` |
| Background computation with snapshot swap | `area:pivot` `area:perf` |
| Value formatting and sort by label | `area:pivot` |
| Drill-through event returning the underlying items | `area:pivot` `area:api` |
| Copy to clipboard | `area:pivot` |
| Benchmark: 1,000,000 rows by two row and two column fields | `area:pivot` `area:perf` |
| Sample pages and docs | `area:pivot` `area:docs` |

**Complete tier:** field list UI with a non-drag alternative; distinct count, product, standard deviation, variance, custom aggregates; "show values as"; sort by value; label, value and top-N filters; date and numeric bucketing; compact, outline and tabular layouts; calculated fields; cell-range selection (spec #108); export (spec #105); incremental recalculation; semantic tree with header association. **Later:** app-supplied server data provider.

### 6. Suite – Gantt

**MVP exit criteria:** a tree-grid task table and a time-scaled bar pane on MAUI and Uno with synchronized scrolling and selection, the four dependency types drawn from app-supplied dates, and a phone layout that shows one pane at a time.

| Issue | Labels |
|---|---|
| `src/KumikoUI.Gantt` neutral package and Gantt control types on MAUI and Uno | `area:gantt` `area:packaging` |
| Spike: two panes on one surface against two surfaces with a native splitter | `area:gantt` `kind:spec` |
| Two-pane composition: tree grid and bar pane sharing vertical scroll and row virtualization, with a splitter | `area:gantt` `area:layout` |
| Time header: two tiers with zoom levels, on the shared scale component | `area:gantt` `area:rendering` |
| Bars: task, summary, milestone, progress fill | `area:gantt` `area:rendering` |
| Nonworking-time shading from one working calendar; today line | `area:gantt` |
| Dependency links: finish-to-start, start-to-start, finish-to-finish, start-to-finish, with routing | `area:gantt` `area:rendering` |
| Tooltips; selection synchronized between panes; scroll to task and to date | `area:gantt` `area:input` |
| Phone layout: one pane at a time | `area:gantt` `area:layout` |
| Benchmark: 100,000 tasks and hundreds of links | `area:gantt` `area:perf` |
| Sample pages and docs | `area:gantt` `area:docs` |

**Complete tier:** drag to move, resize, set progress and link, each with a non-drag alternative; scheduling engine (forward and backward pass, lead and lag, constraints, calendars, summary roll-up, cycle detection); critical path and slack; baselines; three-tier time scale; undo and redo; semantic tree. **Later:** resources and leveling; split tasks; interchange import and export.

### 7 to 10. Host milestones

The same issue set for WPF, WinUI, Avalonia and Blazor WebAssembly. Each starts with a spike.

**Exit criteria:** the datagrid passes the same verification the MAUI and Uno hosts passed for 1.0, every control shipped so far runs on the host, and the package is published.

| Issue | Labels |
|---|---|
| Spike (see below) | `kind:spec` |
| Project, package, CI job and build recipe | `area:packaging` `area:ci` |
| Base control on the host kit: Skia surface (software and GPU) and frame loop | `area:rendering` |
| Input: pointer, wheel, touch and key mapping onto the shared enum contracts, with the shared key-mapping test table | `area:input` |
| Text input and input methods into the drawn text box | `area:input` `area:editing` |
| Clipboard, cursor, theme follow, scaling changes | `area:core` |
| Accessibility adapter for the semantic tree | `area:a11y` |
| `KumikoDataGrid` on the host with bindable properties | `area:api` |
| Each shipped control on the host | per control |
| Sample app | `area:docs` |
| Verification pass and support matrix update | `area:tests` `area:docs` |

| Host | Spike | Specific notes |
|---|---|---|
| WPF | Skia GPU surface next to native siblings; text composition into the drawn text box | Port input mapping, DPI handling and render-loop subscription from Broadside |
| WinUI | Can a plain WinUI app consume the Uno host's Windows build without the Uno runtime? If yes, this is packaging | Otherwise port pointer and key mapping, scale tracking and peers from Broadside |
| Avalonia | Result of the SkiaSharp version spike; custom draw operation leasing the Skia canvas | One host covers Windows, macOS and Linux; mobile and browser targets recorded separately in the matrix |
| Blazor WebAssembly | Synchronous interop for input so default actions can be prevented; time-zone data trimming | WebAssembly only; server rendering not supported. Hidden DOM input for text and an ARIA mirror for accessibility |

### 11. Suite – Docs and website

**Exit criteria (phase 1):** a deployed site with a landing page, datagrid and tree grid docs, generated API reference and the support matrix. **Phase 2:** live in-browser demos of every control.

| Issue | Labels |
|---|---|
| Site skeleton in this repo, built and deployed from CI (builds on spec #110) | `area:website` `area:ci` |
| Landing page for the suite | `area:website` |
| Docs structure: one section per control, shared concepts (theming, data binding, accessibility, hosts) | `area:website` `area:docs` |
| Generated API reference for every package | `area:website` |
| Support matrix page generated from `docs/SUPPORT-MATRIX.md` | `area:website` |
| README rewritten as the suite front page; one NuGet README per package | `area:docs` |
| `docs/ROADMAP.md` rewritten as the suite roadmap | `area:docs` |
| Phase 2: live demos through the WebAssembly host | `area:website` |
| Per-control docs page as part of each control milestone (checklist item, not an issue here) | — |

## 3. Support matrix (draft of `docs/SUPPORT-MATRIX.md`)

Status values: **Shipped**, **Experimental** (released, API may change), **In progress**, **Planned**, **N/A**. A cell that is not Shipped links to its milestone. Caveats are footnotes.

| Control | .NET MAUI | Uno Platform | WPF | WinUI | Avalonia | Blazor WebAssembly |
|---|---|---|---|---|---|---|
| Datagrid | Shipped | Shipped | Planned (7) | Planned (8) | Planned (9) | Planned (10) |
| Tree grid | Planned (2) | Planned (2) | Planned (7) | Planned (8) | Planned (9) | Planned (10) |
| Charts | Planned (3) | Planned (3) [1] | Planned | Planned | Planned | Planned [2] |
| Scheduler | Planned (4) [3] | Planned (4) [3] | Planned | Planned | Planned [3] | Planned [4] |
| Pivot grid | Planned (5) [5] | Planned (5) [5] | Planned | Planned | Planned | Planned [6] |
| Gantt | Planned (6) [7] | Planned (6) [7] | Planned | Planned | Planned [7] | Planned |

1. On Uno's Windows App SDK target the software canvas is used.
2. No chart role exists for assistive technology; a text and table alternative is provided on every framework.
3. Phones get day and agenda-style views, not the full week.
4. Requires time-zone data that the app must not trim.
5. The field list needs a separate phone layout.
6. Large pivots compute on the UI thread unless WebAssembly threading is enabled.
7. Phones show one pane at a time.

Blazor means WebAssembly; server rendering is not supported. Existing caveats for the shipped datagrid (unverified devices) stay in the README until the 1.0.1 milestone closes them.

## 4. New labels

`area:kernel`, `area:hosts`, `area:treegrid`, `area:charts`, `area:scheduler`, `area:pivot`, `area:gantt`, `area:wpf`, `area:winui`, `area:avalonia`, `area:blazor`, `area:website`.

## 5. Risks to watch

| Risk | Where it is handled |
|---|---|
| The kernel refactor and the open grid feature issues touch the same renderer and input code | Decided: Foundations runs before the bulk of the grid feature issues; issues that do not touch the renderer or input controller proceed in parallel |
| SkiaSharp version conflict on Avalonia | Foundations spike, before milestone 9 is designed |
| Type forwards may not pass package validation | Foundations spike; decides the package split release |
| Recurrence and time-zone correctness | Conformance suite from the first scheduler issue |
| Chart performance in WebAssembly | Benchmark issue in the charts MVP; publish numbers, promise nothing |
| Each new host repeats the device verification the 1.0 cycle needed | Verification issue in every host milestone |

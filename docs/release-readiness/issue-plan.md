# KumikoUI 1.0 — Issue plan

Generated from the release-readiness evaluation and the two decision rounds. One issue per work item; each lists the findings it closes, acceptance criteria, dependencies and a suggested implementation model (Opus for architectural or cross-cutting work, Sonnet for well-specified contained work). Created on GitHub on 2026-10-22 as issues #10–#114 (tracking issue #114 is pinned); the index below maps each plan key to its issue.

**105 issues** · by milestone: 0.2.0 – Stabilisation: 81 · 1.0.0-preview: 9 · 1.0.0: 3 · 1.x – Fast follow-ons: 12 · by model: sonnet: 62, opus: 43 · by priority: blocker: 13, high: 48, medium: 34, low: 10

## Index

| Key | Issue | Milestone | Priority | Model | Title |
|---|---|---|---|---|---|
| A1 | [#10](https://github.com/TheEightBot/KumikoUI/issues/10) | 0.2.0 – Stabilisation | blocker | sonnet | Retarget every library, head and test project to net10.0 and drop net9.0 |
| A2 | [#11](https://github.com/TheEightBot/KumikoUI/issues/11) | 0.2.0 – Stabilisation | high | opus | Upgrade to SkiaSharp 4.153.x and migrate text drawing to the SKFont API |
| A3 | [#12](https://github.com/TheEightBot/KumikoUI/issues/12) | 0.2.0 – Stabilisation | high | sonnet | Central package management, global.json for Uno.Sdk 6.7.x, and pinned MAUI/Uno workload sets |
| A4 | [#13](https://github.com/TheEightBot/KumikoUI/issues/13) | 0.2.0 – Stabilisation | blocker | sonnet | Set the platform watermark: Android 24, iOS 17.0, Mac Catalyst 17.0, Windows 10.0.19041 on both hosts |
| A5 | [#14](https://github.com/TheEightBot/KumikoUI/issues/14) | 0.2.0 – Stabilisation | blocker | sonnet | Single source of truth for versions: MinVer from tags, lockstep packages, deterministic metadata |
| B1 | [#15](https://github.com/TheEightBot/KumikoUI/issues/15) | 0.2.0 – Stabilisation | blocker | opus | Safe, culture-aware comparison for sorting, grouping and filtering (never throws) |
| B2 | [#18](https://github.com/TheEightBot/KumikoUI/issues/18) | 0.2.0 – Stabilisation | high | sonnet | Per-item accessor resolution and null-row handling (heterogeneous and nullable sources) |
| B3 | [#19](https://github.com/TheEightBot/KumikoUI/issues/19) | 0.2.0 – Stabilisation | high | sonnet | Summary rows: several aggregates per column, all-level group summaries, decimal-safe arithmetic |
| B4 | [#21](https://github.com/TheEightBot/KumikoUI/issues/21) | 0.2.0 – Stabilisation | high | sonnet | Incremental collection updates honour predicate filters; INPC heuristics and event consistency |
| B5 | [#22](https://github.com/TheEightBot/KumikoUI/issues/22) | 0.2.0 – Stabilisation | high | sonnet | Style resolution must not mutate shared CellStyle instances |
| B6 | [#24](https://github.com/TheEightBot/KumikoUI/issues/24) | 0.2.0 – Stabilisation | high | opus | Column sizing: Auto actually measures (sampled), Star columns can be resized, min/max redistribution |
| B7 | [#25](https://github.com/TheEightBot/KumikoUI/issues/25) | 0.2.0 – Stabilisation | high | sonnet | PR #9: pinned bottom summary footer must be reflected in hit-testing, ensure-visible and draw order (+ Core test) |
| B8 | [#27](https://github.com/TheEightBot/KumikoUI/issues/27) | 0.2.0 – Stabilisation | medium | sonnet | Clip summary values to their pane; horizontal grid lines on frozen rows |
| B9 | [#28](https://github.com/TheEightBot/KumikoUI/issues/28) | 0.2.0 – Stabilisation | medium | sonnet | DataGridSource lifecycle: IDisposable/Detach, unsubscribe INCC and every INPC item |
| B10 | [#29](https://github.com/TheEightBot/KumikoUI/issues/29) | 0.2.0 – Stabilisation | low | sonnet | Group expand/collapse state keyed by raw key and pruned per path |
| C1 | [#31](https://github.com/TheEightBot/KumikoUI/issues/31) | 0.2.0 – Stabilisation | blocker | opus | Edit session tracks the item, not the row index; commits or cancels on view changes |
| C2 | [#33](https://github.com/TheEightBot/KumikoUI/issues/33) | 0.2.0 – Stabilisation | blocker | opus | Editors never commit a value they could not represent (IValueEditor contract) |
| C3 | [#32](https://github.com/TheEightBot/KumikoUI/issues/32) | 0.2.0 – Stabilisation | high | opus | Typed value conversion, validation and error UI following WPF/WinUI DataGrid conventions |
| C4 | [#34](https://github.com/TheEightBot/KumikoUI/issues/34) | 0.2.0 – Stabilisation | high | sonnet | Honour commit results, end the previous editor before starting another, fix event ordering |
| C5 | [#35](https://github.com/TheEightBot/KumikoUI/issues/35) | 0.2.0 – Stabilisation | high | sonnet | Action buttons: CanExecute, disabled state, and a non-editing interactive cell path |
| C6 | [#36](https://github.com/TheEightBot/KumikoUI/issues/36) | 0.2.0 – Stabilisation | medium | sonnet | Editors use the grid's CellFont and popups flip/clamp to the viewport |
| C7 | [#38](https://github.com/TheEightBot/KumikoUI/issues/38) | 0.2.0 – Stabilisation | medium | sonnet | ProgressBar column type and tri-state Boolean editing (or remove the promise) |
| D1 | [#39](https://github.com/TheEightBot/KumikoUI/issues/39) | 0.2.0 – Stabilisation | blocker | opus | Core text-input contract (composition-aware) and a grapheme-correct DrawnTextBox with undo/redo |
| D2 | [#40](https://github.com/TheEightBot/KumikoUI/issues/40) | 0.2.0 – Stabilisation | high | opus | iOS / Mac Catalyst text-input adapter: UITextInput with marked text and UIKeyCommand |
| D3 | [#41](https://github.com/TheEightBot/KumikoUI/issues/41) | 0.2.0 – Stabilisation | high | opus | Android text-input adapter: InputConnection-based editing and hardware-key navigation |
| D4 | [#42](https://github.com/TheEightBot/KumikoUI/issues/42) | 0.2.0 – Stabilisation | high | opus | Windows (MAUI) text-input adapter: CoreTextEditContext, key hooks and pointer modifiers |
| D5 | [#43](https://github.com/TheEightBot/KumikoUI/issues/43) | 0.2.0 – Stabilisation | high | opus | Uno text-input adapter: composition events, no keyboard trap, proxy invisible to hit-testing and assistive tech |
| D6 | [#44](https://github.com/TheEightBot/KumikoUI/issues/44) | 0.2.0 – Stabilisation | high | sonnet | Clipboard: Ctrl/Cmd+C copies the selection as TSV, copy/cut/paste inside editors on every host |
| D7 | [#46](https://github.com/TheEightBot/KumikoUI/issues/46) | 0.2.0 – Stabilisation | high | opus | ComboBox and Picker editors: pointer scrolling, typeahead, keyboard commit, correct key routing while editing |
| D8 | [#48](https://github.com/TheEightBot/KumikoUI/issues/48) | 0.2.0 – Stabilisation | medium | sonnet | DatePicker and NumericUpDown: culture-aware, typed entry, auto-repeat, sane limits |
| D9 | [#49](https://github.com/TheEightBot/KumikoUI/issues/49) | 0.2.0 – Stabilisation | blocker | opus | Filter popup: scrollable, virtualised value list with keyboard support, dismissal notifications and themed search box |
| E1 | [#20](https://github.com/TheEightBot/KumikoUI/issues/20) | 0.2.0 – Stabilisation | high | opus | Structured ViewChanged event (replaces DataChanged), ScrollChanged, consistent state APIs on DataGridSource |
| E2 | [#30](https://github.com/TheEightBot/KumikoUI/issues/30) | 0.2.0 – Stabilisation | high | opus | Item-based selection model with read-only sets, Select/Deselect, deltas, and reconciliation on view changes |
| E3 | [#50](https://github.com/TheEightBot/KumikoUI/issues/50) | 0.2.0 – Stabilisation | medium | sonnet | DataGridColumn: configuration vs runtime state, change notification |
| F1 | [#16](https://github.com/TheEightBot/KumikoUI/issues/16) | 0.2.0 – Stabilisation | high | sonnet | Naming pass: KumikoDataGrid control, collision-free type names, RowTappedEventArgs2, file = type |
| F2 | [#37](https://github.com/TheEightBot/KumikoUI/issues/37) | 0.2.0 – Stabilisation | medium | sonnet | Remove or internalise dead and unsafe public surface before the freeze |
| F3 | [#51](https://github.com/TheEightBot/KumikoUI/issues/51) | 0.2.0 – Stabilisation | medium | sonnet | One hosting entry point: UseKumikoUI() on MAUI (with handler registration, diagnostics, font-alias bridging); delete the Uno no-op |
| F4 | [#52](https://github.com/TheEightBot/KumikoUI/issues/52) | 0.2.0 – Stabilisation | medium | opus | Host control base class and surface clean-up (ContentView/TemplatedView, no SetItemsSource, non-nullable Columns, ContentProperty on Uno) |
| F5 | [#53](https://github.com/TheEightBot/KumikoUI/issues/53) | 0.2.0 – Stabilisation | high | sonnet | Implement grid-level IsReadOnly, AllowSorting and AllowFiltering in Core |
| F6 | [#54](https://github.com/TheEightBot/KumikoUI/issues/54) | 0.2.0 – Stabilisation | high | sonnet | API governance: PublicApiAnalyzers, package validation baseline, CS1591 as error, .editorconfig + dotnet format in CI |
| G1 | [#55](https://github.com/TheEightBot/KumikoUI/issues/55) | 0.2.0 – Stabilisation | high | sonnet | MVVM selection surface: SelectedItem, SelectedItems, SelectedIndex, CurrentCell, SelectionChanged + commands on both hosts |
| G2 | [#56](https://github.com/TheEightBot/KumikoUI/issues/56) | 0.2.0 – Stabilisation | medium | sonnet | Programmatic scrolling API and Scrolled event |
| G3 | [#57](https://github.com/TheEightBot/KumikoUI/issues/57) | 0.2.0 – Stabilisation | medium | sonnet | Bindable Theme, GridStyle, ScrollSettings; FollowSystemTheme; height precedence |
| G4 | [#58](https://github.com/TheEightBot/KumikoUI/issues/58) | 0.2.0 – Stabilisation | medium | sonnet | Expose grouping, sorting, filtering, row-drag and grid-line options as bindables with commands; public Invalidate() |
| G5 | [#59](https://github.com/TheEightBot/KumikoUI/issues/59) | 0.2.0 – Stabilisation | medium | sonnet | Lifecycle and leak fixes on both hosts (cleanup completeness, unsubscribe bound collections, dispose the source) |
| G6 | [#60](https://github.com/TheEightBot/KumikoUI/issues/60) | 0.2.0 – Stabilisation | medium | opus | Pointer and wheel semantics across hosts: hover action, cursor feedback, modifiers, normalised wheel deltas, pointer-id tracking, DPI from the canvas |
| G7 | [#61](https://github.com/TheEightBot/KumikoUI/issues/61) | 0.2.0 – Stabilisation | medium | sonnet | Uno long-press via timer (parity with MAUI), swallow the following release |
| G9 | [#64](https://github.com/TheEightBot/KumikoUI/issues/64) | 0.2.0 – Stabilisation | low | sonnet | UnoActionButtonDefinition: XAML-bindable action buttons on Uno (parity with MauiActionButtonDefinition) |
| H1 | [#65](https://github.com/TheEightBot/KumikoUI/issues/65) | 0.2.0 – Stabilisation | high | opus | Uno rendering surface: SKCanvasElement (GPU) with SKXamlCanvas fallback, frame-callback loop, DPI handling |
| H2 | [#66](https://github.com/TheEightBot/KumikoUI/issues/66) | 0.2.0 – Stabilisation | high | sonnet | Uno package hygiene: strip AndroidX content, drop dev-time dependencies, assert the nuspec in CI, fix metadata |
| I1 | [#68](https://github.com/TheEightBot/KumikoUI/issues/68) | 0.2.0 – Stabilisation | high | opus | Allocation-free steady-state render loop (accessor and display-text caches, paint cache versioning, no LINQ in the render path) |
| I2 | [#23](https://github.com/TheEightBot/KumikoUI/issues/23) | 0.2.0 – Stabilisation | medium | opus | Cross-frame text measurement cache, grapheme-safe ellipsis, and text wrapping support in IDrawingContext |
| I3 | [#62](https://github.com/TheEightBot/KumikoUI/issues/62) | 0.2.0 – Stabilisation | medium | opus | Long-lived Skia resource cache per control; registrar O(1) ownership, Unregister, safe Clear |
| I4 | [#69](https://github.com/TheEightBot/KumikoUI/issues/69) | 0.2.0 – Stabilisation | high | opus | Off-UI-thread view pipeline: immutable ViewSnapshot, background sort/filter/group/summaries with coalescing, incremental aggregates |
| I7 | [#72](https://github.com/TheEightBot/KumikoUI/issues/72) | 0.2.0 – Stabilisation | low | sonnet | InertialScroller edge cases: hold-then-release, boundary stop, timestamp guard, frame-delta driven |
| J1 | [#73](https://github.com/TheEightBot/KumikoUI/issues/73) | 0.2.0 – Stabilisation | high | sonnet | Unknown font family must not crash: fallback to default, log once, null-safe disposal |
| J2 | [#74](https://github.com/TheEightBot/KumikoUI/issues/74) | 0.2.0 – Stabilisation | high | opus | Per-glyph font fallback (run segmentation via SKFontManager.MatchCharacter), registrar fallback list, bold/italic for registered faces |
| J3 | [#75](https://github.com/TheEightBot/KumikoUI/issues/75) | 0.2.0 – Stabilisation | medium | sonnet | KumikoFonts helper on MAUI (parity with Uno), shared sample fonts, licence notices |
| K1 | [#17](https://github.com/TheEightBot/KumikoUI/issues/17) | 0.2.0 – Stabilisation | high | opus | AOT-safe property access: source generator, delegate accessors, annotated reflection fallback; IsAotCompatible everywhere |
| M1 | [#47](https://github.com/TheEightBot/KumikoUI/issues/47) | 0.2.0 – Stabilisation | high | sonnet | GridStrings provider for every built-in string and culture-aware formatting |
| N1 | [#26](https://github.com/TheEightBot/KumikoUI/issues/26) | 0.2.0 – Stabilisation | blocker | opus | Recording IDrawingContext fake and DataGridRenderer command tests (passes, frozen panes, summaries, group rows, overlays) |
| N2 | [#81](https://github.com/TheEightBot/KumikoUI/issues/81) | 0.2.0 – Stabilisation | high | sonnet | DataGridSource, layout and selection behaviour tests (INCC/INPC, nested grouping, summary values, sort stability, culture, null and heterogeneous items) |
| N3 | [#82](https://github.com/TheEightBot/KumikoUI/issues/82) | 0.2.0 – Stabilisation | blocker | opus | Editing, input-controller, hit-testing, popup/focus and drawn-component tests |
| N4 | [#83](https://github.com/TheEightBot/KumikoUI/issues/83) | 0.2.0 – Stabilisation | medium | sonnet | SkiaSharp tests: raster golden images, measure/draw consistency, font fallback, registrar lifecycle |
| N5 | [#84](https://github.com/TheEightBot/KumikoUI/issues/84) | 0.2.0 – Stabilisation | medium | sonnet | Host tests: Uno pure helpers and desktop-head runtime tests; MAUI property → Core mapping |
| N6 | [#85](https://github.com/TheEightBot/KumikoUI/issues/85) | 0.2.0 – Stabilisation | medium | sonnet | Code coverage collection, reporting and ratchet; test dependency refresh |
| N7 | [#87](https://github.com/TheEightBot/KumikoUI/issues/87) | 0.2.0 – Stabilisation | high | sonnet | Compiled documentation snippets: every README/instructions code block builds in CI |
| O1 | [#88](https://github.com/TheEightBot/KumikoUI/issues/88) | 0.2.0 – Stabilisation | blocker | sonnet | Release gate: rulesets on main, required checks, tag protection, environment reviewers, dispatch validation, all test projects in the gate |
| O2 | [#89](https://github.com/TheEightBot/KumikoUI/issues/89) | 0.2.0 – Stabilisation | high | sonnet | CI matrix: macOS job for Apple heads and both samples, Android sample, OS matrix for Core tests, timeouts, test reporter, doc-only no-op job |
| O3 | [#90](https://github.com/TheEightBot/KumikoUI/issues/90) | 0.2.0 – Stabilisation | high | sonnet | Supply-chain automation: Dependabot (nuget + actions), CodeQL, least-privilege permissions, SHA-pinned and updated actions, secret scanning |
| O4 | [#92](https://github.com/TheEightBot/KumikoUI/issues/92) | 0.2.0 – Stabilisation | medium | sonnet | Publishing hardening: NuGet Trusted Publishing (OIDC), required symbol packages, release notes from CHANGELOG, dispatch parity |
| P1 | [#93](https://github.com/TheEightBot/KumikoUI/issues/93) | 0.2.0 – Stabilisation | high | sonnet | Community and governance files: CONTRIBUTING, SECURITY (private vulnerability reporting), PR template, YAML issue forms, CODEOWNERS, labels, milestones, Discussions, branch model |
| P2 | [#91](https://github.com/TheEightBot/KumikoUI/issues/91) | 0.2.0 – Stabilisation | high | sonnet | CHANGELOG (Keep a Changelog) and versioning/support policy |
| P3 | [#94](https://github.com/TheEightBot/KumikoUI/issues/94) | 0.2.0 – Stabilisation | low | sonnet | Repository assets: re-encode the showcase GIF, MP4 release asset, template leftovers, .gitignore additions, legal name consistency |
| P4 | [#86](https://github.com/TheEightBot/KumikoUI/issues/86) | 0.2.0 – Stabilisation | blocker | opus | README rewrite: host-neutral, accurate claims, compiled snippets, Uno getting started, platform matrix, theming reference, FAQ, column reorder and validation |
| P5 | [#95](https://github.com/TheEightBot/KumikoUI/issues/95) | 0.2.0 – Stabilisation | blocker | sonnet | Per-package NuGet READMEs (plain Markdown, absolute images) and corrected package tags |
| P6 | [#96](https://github.com/TheEightBot/KumikoUI/issues/96) | 0.2.0 – Stabilisation | medium | sonnet | Internal docs regenerated from code: ARCHITECTURE, RENDERING, Copilot instructions and prompts, testing instructions, Uno instructions; docs/uno becomes a user guide |
| Q1 | [#98](https://github.com/TheEightBot/KumikoUI/issues/98) | 0.2.0 – Stabilisation | high | opus | Column header reorder: documented, tested, pane-aware drop index, DisplayIndex and layout persistence |
| Q2 | [#99](https://github.com/TheEightBot/KumikoUI/issues/99) | 0.2.0 – Stabilisation | high | opus | Row drag and drop: long-press start on touch, drag on mouse, edge autoscroll, correct ghost offset, ReorderRow semantics |
| Q3 | [#45](https://github.com/TheEightBot/KumikoUI/issues/45) | 0.2.0 – Stabilisation | high | opus | Keyboard navigation completeness: Tab exit, Enter edits, EditTriggers honoured, ensure-visible math with frozen rows/summaries/group panel, Space toggles, page sizes |
| Q4 | [#100](https://github.com/TheEightBot/KumikoUI/issues/100) | 0.2.0 – Stabilisation | medium | sonnet | ContextRequested event: right-click and long-press raise it and never sort, edit or drag; CellTapped/HeaderTapped |
| G8 | [#63](https://github.com/TheEightBot/KumikoUI/issues/63) | 1.0.0-preview | medium | opus | Image columns: IImageResolver with caching, async decode and disposal on both hosts |
| H3 | [#67](https://github.com/TheEightBot/KumikoUI/issues/67) | 1.0.0-preview | high | opus | Uno device verification matrix: every head exercised interactively with a recorded checklist |
| I5 | [#71](https://github.com/TheEightBot/KumikoUI/issues/71) | 1.0.0-preview | medium | opus | MAUI GPU render mode (SKGLView), platform frame callback, partial redraw |
| I6 | [#70](https://github.com/TheEightBot/KumikoUI/issues/70) | 1.0.0-preview | high | sonnet | BenchmarkDotNet project and frame-time overlay; performance budgets and published numbers |
| K2 | [#76](https://github.com/TheEightBot/KumikoUI/issues/76) | 1.0.0-preview | high | sonnet | AOT and trimming CI: PublishAot samples (iOS, Mac Catalyst, Windows), PublishTrimmed WASM, console smoke with warnings as errors |
| L1 | [#77](https://github.com/TheEightBot/KumikoUI/issues/77) | 1.0.0-preview | high | sonnet | MAUI accessibility quick wins: proxy hidden from assistive tech, grid-level semantics and announcements |
| L2 | [#78](https://github.com/TheEightBot/KumikoUI/issues/78) | 1.0.0-preview | high | sonnet | Uno accessibility quick wins: automation peer with name/help text, proxy marked Raw, keyboard exit |
| P7 | [#97](https://github.com/TheEightBot/KumikoUI/issues/97) | 1.0.0-preview | low | sonnet | Sample apps: README, Images & Styling page, Events/MVVM page, DisplayAlertAsync, cached commands, Android soft-input mode, WASM linker config |
| Q5 | [#101](https://github.com/TheEightBot/KumikoUI/issues/101) | 1.0.0-preview | high | opus | Interaction polish for a premium feel: animations, hover and pressed states, cursor feedback, momentum tuning, haptics, exact hit regions |
| R1 | [#114](https://github.com/TheEightBot/KumikoUI/issues/114) | 1.0.0 | high | sonnet | 1.0.0 release checklist (tracking issue) |
| R2 | [#112](https://github.com/TheEightBot/KumikoUI/issues/112) | 1.0.0 | high | opus | API review sign-off: freeze PublicAPI.Shipped.txt, enable package validation baseline, final naming audit |
| R3 | [#113](https://github.com/TheEightBot/KumikoUI/issues/113) | 1.0.0 | medium | sonnet | Final docs pass and launch: README numbers from benchmarks, CHANGELOG 1.0.0 section, nuget.org README previews, announcement draft |
| L3 | [#79](https://github.com/TheEightBot/KumikoUI/issues/79) | 1.x – Fast follow-ons | medium | opus | Spec: virtual accessibility tree with per-cell navigation (WinUI IGridProvider, iOS UIAccessibilityContainer, Android ExploreByTouchHelper, WASM ARIA) |
| M2 | [#80](https://github.com/TheEightBot/KumikoUI/issues/80) | 1.x – Fast follow-ons | low | opus | Spec: right-to-left layout mirroring (FlowDirection) for the grid, headers, frozen panes and editors |
| X1 | [#103](https://github.com/TheEightBot/KumikoUI/issues/103) | 1.x – Fast follow-ons | medium | opus | Spec: row details / master-detail panel |
| X2 | [#104](https://github.com/TheEightBot/KumikoUI/issues/104) | 1.x – Fast follow-ons | medium | opus | Spec: incremental / load-more data loading (ISupportIncrementalLoading, NearEnd hook, loading row) |
| X3 | [#105](https://github.com/TheEightBot/KumikoUI/issues/105) | 1.x – Fast follow-ons | medium | sonnet | Spec: export to CSV and Excel/PDF |
| X4 | [#102](https://github.com/TheEightBot/KumikoUI/issues/102) | 1.x – Fast follow-ons | medium | opus | Spec: variable row height and text wrapping |
| X5 | [#106](https://github.com/TheEightBot/KumikoUI/issues/106) | 1.x – Fast follow-ons | low | sonnet | Spec: column chooser UI and multi-select checkbox column |
| X6 | [#107](https://github.com/TheEightBot/KumikoUI/issues/107) | 1.x – Fast follow-ons | low | sonnet | Spec: paging and DataTable / dictionary / dynamic row sources |
| X7 | [#108](https://github.com/TheEightBot/KumikoUI/issues/108) | 1.x – Fast follow-ons | medium | opus | Spec: cell selection unit (SelectionUnit.Cell) with ranges and clipboard |
| X8 | [#109](https://github.com/TheEightBot/KumikoUI/issues/109) | 1.x – Fast follow-ons | low | opus | Spec: item-based frozen rows (pinning), stacked headers and cell merging |
| X9 | [#110](https://github.com/TheEightBot/KumikoUI/issues/110) | 1.x – Fast follow-ons | medium | sonnet | Spec: API reference site (DocFX) and cookbook for custom columns, renderers and editors |
| X10 | [#111](https://github.com/TheEightBot/KumikoUI/issues/111) | 1.x – Fast follow-ons | low | opus | Spec: HarfBuzz shaping for complex scripts (Arabic, Indic) on top of per-glyph fallback |

## Issues

# Milestone: 0.2.0 – Stabilisation

### A1 — Retarget every library, head and test project to net10.0 and drop net9.0

Labels: `area:packaging`, `area:ci`, `kind:task`, `priority:blocker`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic A — Platform & toolchain** · plan key `A1` · priority **blocker** · suggested model **sonnet**

## Problem
Core, SkiaSharp, every Uno head and both test projects target net9.0, whose support ends 2026-11-10. The MAUI host and both samples are already net10.0. The Core test suite cannot even run on a machine with only the .NET 10 runtime.

## Evidence
Findings: PKG-02, UNO-01, TEST-06, CI-07
Reports: [05-tests-ci-packaging.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/05-tests-ci-packaging.md), [04-uno.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/04-uno.md)

## Proposed change
- `net9.0` → `net10.0` in KumikoUI.Core, KumikoUI.SkiaSharp, KumikoUI.Uno (all heads incl. `net10.0-windows10.0.26100`), KumikoUI.Core.Tests, KumikoUI.Uno.Tests
- CI: install `10.x` only; `build-core` and the publish `test` job on .NET 10
- Record the decision in docs/uno/README.md ADR-4 and the README requirements table

## Acceptance criteria
- [ ] No `net9.0` string remains in any csproj or workflow
- [ ] `dotnet test tests/KumikoUI.Core.Tests` passes without `DOTNET_ROLL_FORWARD`
- [ ] All CI jobs green on .NET 10 SDK only

## Dependencies
Blocked by: none
Blocks: `A2`, `A3`, `A4`, `F1`, `N1`, `N6`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### A2 — Upgrade to SkiaSharp 4.153.x and migrate text drawing to the SKFont API

Labels: `area:skiasharp`, `area:rendering`, `area:packaging`, `kind:task`, `priority:high`, `model:opus` · Milestone: `0.2.0 – Stabilisation`

**Epic A — Platform & toolchain** · plan key `A2` · priority **high** · suggested model **opus**

## Problem
SkiaSharp is pinned at 3.119.2 (Feb 2026); 4.x has been stable since 4.148.0 with compile-breaking SKPaint→SKFont text changes and a native ABI change. Shipping 1.0 on 3.x forces every consumer through that break later.

## Evidence
Findings: PKG-05, research §2.3
Reports: [07-external-research.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/07-external-research.md), [03-skiasharp-maui.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/03-skiasharp-maui.md)

## Proposed change
- Bump SkiaSharp, SkiaSharp.Views.Maui.Controls, SkiaSharp.Views.Uno.WinUI, SkiaSharp.Views.WinUI to 4.153.x (single `SkiaSharpVersion` in Directory.Packages.props)
- Migrate `SkiaDrawingContext` text/measure/typeface code to `SKFont` + `SKCanvas.DrawText(…, SKFont, …)`; handle `SKTypeface.Empty` and non-null `SKPaint.Typeface` semantics
- Verify rendering parity with a before/after raster snapshot of the sample grid

## Acceptance criteria
- [ ] Builds with zero obsolete-API warnings
- [ ] Snapshot of the main sample page is pixel-equivalent (±1 LSB) before/after on macOS
- [ ] Uno `SkiaSharpVersion` override documented in the csproj comment

## Dependencies
Blocked by: `A1`
Blocks: `A3`, `H1`, `I2`, `I3`, `J1`, `N4`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### A3 — Central package management, global.json for Uno.Sdk 6.7.x, and pinned MAUI/Uno workload sets

Labels: `area:packaging`, `area:ci`, `kind:task`, `priority:high`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic A — Platform & toolchain** · plan key `A3` · priority **high** · suggested model **sonnet**

## Problem
SkiaSharp is pinned in seven places, Uno.Sdk 6.5.31 inline in three csproj files (current 6.7.30), `$(MauiVersion)` floats with the build machine, and CI reinstalls unpinned workloads on every run because the cache key never hits.

## Evidence
Findings: PKG-05, UNO-08, MAUI-22, CI-03
Reports: [05-tests-ci-packaging.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/05-tests-ci-packaging.md), [04-uno.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/04-uno.md), [03-skiasharp-maui.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/03-skiasharp-maui.md)

## Proposed change
- Add `Directory.Packages.props` with `ManagePackageVersionsCentrally`; move every `PackageReference` version there
- Add `global.json` with `msbuild-sdks: { Uno.Sdk: 6.7.30 }` (and an SDK band with `rollForward: latestFeature`); remove inline Uno.Sdk pins
- Set an explicit `MauiVersion` floor for the library
- Pin workloads via a rollback file / workload-set version used by CI and documented in CONTRIBUTING; fix the cache paths (`${{ env.USERPROFILE }}` resolves empty) or drop the cache

## Acceptance criteria
- [ ] Every version appears exactly once in the repo
- [ ] A CI run shows either a cache hit or a pinned workload install
- [ ] `dotnet build` of every project succeeds on a clean clone following CONTRIBUTING

## Dependencies
Blocked by: `A1`, `A2`
Blocks: `H1`, `H2`, `O2`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### A4 — Set the platform watermark: Android 24, iOS 17.0, Mac Catalyst 17.0, Windows 10.0.19041 on both hosts

Labels: `area:maui`, `area:uno`, `area:packaging`, `area:docs`, `kind:task`, `priority:blocker`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic A — Platform & toolchain** · plan key `A4` · priority **blocker** · suggested model **sonnet**

## Problem
The Xcode 27 toolchain rejects the current Mac Catalyst floor (15.0 < 17.0): the MAUI sample no longer builds and the README advertises an unachievable minimum. Floors differ between hosts and docs.

## Evidence
Findings: MAUI-01, DOC-14, UNO-25
Reports: [03-skiasharp-maui.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/03-skiasharp-maui.md), [06-docs.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/06-docs.md), [04-uno.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/04-uno.md)

## Proposed change
- `SupportedOSPlatformVersion`/`TargetPlatformMinVersion` on KumikoUI.Maui, KumikoUI.Uno and both samples: android 24, ios 17.0, maccatalyst 17.0, windows 10.0.19041.0
- Remove the Mac Catalyst claim from the Uno package description
- README 'Supported platforms' matrix per package with build-host requirements and verification status

## Acceptance criteria
- [ ] `dotnet build samples/SampleApp.Maui -f net10.0-maccatalyst` succeeds
- [ ] README matrix matches every csproj value
- [ ] Decision recorded: re-baseline floors at each .NET major

## Dependencies
Blocked by: `A1`
Blocks: `K2`, `O2`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### A5 — Single source of truth for versions: MinVer from tags, lockstep packages, deterministic metadata

Labels: `area:packaging`, `area:ci`, `kind:task`, `priority:blocker`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic A — Platform & toolchain** · plan key `A5` · priority **blocker** · suggested model **sonnet**

## Problem
Every csproj hard-codes `<Version>1.0.0</Version>` while tags are v0.1.x; a local `dotnet pack` produces a 1.0.0 package that one push would burn forever. Copyright year is computed at build time, breaking reproducibility.

## Evidence
Findings: PKG-01, PKG-06
Reports: [05-tests-ci-packaging.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/05-tests-ci-packaging.md)

## Proposed change
- Adopt MinVer (tag-driven; local builds get a prerelease height suffix); delete `<Version>` from all csproj
- publish.yml asserts the tag matches the resolved version and the tag commit is on `main`
- Hard-code the copyright year (bumped at release); move `IncludeSymbols`/`SymbolPackageFormat` into Directory.Build.props; drop the redundant SourceLink package reference

## Acceptance criteria
- [ ] `dotnet pack` on a non-tagged commit yields `0.2.0-alpha.0.N`
- [ ] Tagging `v0.2.0` yields exactly `0.2.0` for all four packages
- [ ] Two builds of the same commit produce byte-identical assemblies

## Dependencies
Blocked by: none
Blocks: `O1`, `O4`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### B1 — Safe, culture-aware comparison for sorting, grouping and filtering (never throws)

Labels: `area:core`, `kind:bug`, `priority:blocker`, `model:opus` · Milestone: `0.2.0 – Stabilisation`

**Epic B — Data engine correctness** · plan key `B1` · priority **blocker** · suggested model **opus**

## Problem
`Comparer<object>.Default` throws for Template/action columns (which sort the row object), mixed int/double values and any non-IComparable type; `AllowSorting` defaults to true, so a plain header tap crashes the app (reproduced). Ties are unstable, row sort is culture/case-sensitive while groups and filters are ordinal, and groups are ordered by display text (1, 10, 11, 2…).

## Evidence
Findings: CORE-01, CORE-12, CORE-13, CORE-45
Reports: [01-core-data-layout-render.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/01-core-data-layout-render.md)

## Proposed change
- One `GridComparer`: nulls first, IComparable fast path, numeric/date natural order across types, ordinal fallback for unrelated types, never throws
- `DataGridSource.Culture` + `StringComparison` (default current culture, ignore case) used by sort, group, filter and formatting
- Groups ordered by raw key through the column's `CustomComparer`/`GridComparer`
- Stable sort (tie-break on source index)
- `AllowSorting` defaults false for Template/Image/empty-`PropertyName` columns

## Acceptance criteria
- [ ] Tests: Template column header tap, mixed int/double, custom class, null values — no exception, deterministic order
- [ ] Group by int/DateTime orders numerically/chronologically
- [ ] Same case/culture rules observed in sort, group and filter popup

## Dependencies
Blocked by: none
Blocks: `B3`, `B10`, `N2`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### B2 — Per-item accessor resolution and null-row handling (heterogeneous and nullable sources)

Labels: `area:core`, `kind:bug`, `priority:high`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic B — Data engine correctness** · plan key `B2` · priority **high** · suggested model **sonnet**

## Problem
Sort and summaries compile an accessor against `_sourceItems[0].GetType()` and cast every item to it, so `List<Animal>` with `Dog`/`Cat` throws; a `null` item crashes the first render (reproduced).

## Evidence
Findings: CORE-14, CORE-15
Reports: [01-core-data-layout-render.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/01-core-data-layout-render.md)

## Proposed change
- Resolve accessors per runtime item type everywhere `GetCellValue` already does (through the accessor abstraction from K1)
- Define null-row semantics: skipped by filters, rendered empty, `GetCellValue` returns null; or reject in `SetItems` with a clear message — pick and document

## Acceptance criteria
- [ ] Tests for heterogeneous lists (sort, filter, group, summary) and for null items
- [ ] No `_sourceItems[0]` type assumption remains

## Dependencies
Blocked by: `K1`
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### B3 — Summary rows: several aggregates per column, all-level group summaries, decimal-safe arithmetic

Labels: `area:core`, `area:rendering`, `kind:bug`, `priority:high`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic B — Data engine correctness** · plan key `B3` · priority **high** · suggested model **sonnet**

## Problem
Computed values are keyed by property name, so the README's own Sum + Average example drops the Sum (reproduced); aggregates run through `double` with per-value exception handling; Min/Max throw on mixed types; lookups are case-sensitive; group summaries appear only at the innermost level.

## Evidence
Findings: CORE-02, CORE-40, CORE-41, CORE-42
Reports: [01-core-data-layout-render.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/01-core-data-layout-render.md)

## Proposed change
- Key computed values by (property, summary type/label); render several aggregates stacked in one cell
- `decimal` path when inputs are decimal; type-code switch instead of exceptions; safe Min/Max via `GridComparer`
- `GroupSummaryRow.Levels = All | Innermost`
- Case-insensitive value lookup; allow aggregating a property with no visible column

## Acceptance criteria
- [ ] README summary example renders both Sum and Avg
- [ ] Tests for group summaries at two levels and for decimal totals
- [ ] 100K-row Sum on a string column does not throw per row

## Dependencies
Blocked by: `B1`
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### B4 — Incremental collection updates honour predicate filters; INPC heuristics and event consistency

Labels: `area:core`, `kind:bug`, `priority:high`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic B — Data engine correctness** · plan key `B4` · priority **high** · suggested model **sonnet**

## Problem
`ObservableCollection.Add` bypasses `SetFilter` predicates until the next full rebuild (reproduced); `PropertyChanged("")` and nested paths (`Address.City`) never trigger a rebuild; `ClearSort`/`ClearAllFilters`/`SetFilter`/`DefaultGroupExpanded` raise no change events or do not rebuild.

## Evidence
Findings: CORE-04, CORE-17, CORE-18
Reports: [01-core-data-layout-render.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/01-core-data-layout-render.md)

## Proposed change
- Evaluate `PassesAllFilters` in the incremental Add path
- Treat empty/null property name as 'all', prefix-match dotted paths, compare `OrdinalIgnoreCase`; document that nested objects must raise through the root
- Route every state change through one method that raises the matching Changed event; fire only on actual change; `ReorderRow` fires once

## Acceptance criteria
- [ ] Tests: predicate + Add/Replace/Move/Reset, nested-path INPC, each Changed event for each mutation

## Dependencies
Blocked by: `E1`
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### B5 — Style resolution must not mutate shared CellStyle instances

Labels: `area:core`, `area:rendering`, `kind:bug`, `priority:high`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic B — Data engine correctness** · plan key `B5` · priority **high** · suggested model **sonnet**

## Problem
`CellStyle.Merge(null, x)` returns `x` by reference, then row-level overrides write into it: one `RowStyleResolver` result permanently recolours the column for every row, and resolver-returned cached instances are corrupted.

## Evidence
Findings: CORE-03
Reports: [01-core-data-layout-render.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/01-core-data-layout-render.md)

## Proposed change
- Compute the effective style into a reusable scratch instance (or clone before override); never write into `col.CellStyle` or resolver results

## Acceptance criteria
- [ ] Test: resolver returning a style for row 0 leaves rows 1..n and `col.CellStyle` unchanged across two frames

## Dependencies
Blocked by: none
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### B6 — Column sizing: Auto actually measures (sampled), Star columns can be resized, min/max redistribution

Labels: `area:core`, `area:layout`, `kind:bug`, `priority:high`, `model:opus` · Milestone: `0.2.0 – Stabilisation`

**Epic B — Data engine correctness** · plan key `B6` · priority **high** · suggested model **opus**

## Problem
`SizeMode=Auto` behaves exactly like Fixed; `ComputeColumnWidths` overwrites Star widths every frame so resizing snaps back; min/max clamps do not redistribute slack; auto-fit measures all 100K rows on the UI thread and the MAUI host leaks a drawing context doing it.

## Evidence
Findings: CORE-07, CORE-08, CORE-24, CORE-39, MAUI-10
Reports: [01-core-data-layout-render.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/01-core-data-layout-render.md), [03-skiasharp-maui.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/03-skiasharp-maui.md)

## Proposed change
- Auto: measure header + a bounded sample (visible range plus first/last N rows) on first render and on `ViewChanged`, using a host-supplied measuring context; cache per column
- Resize/auto-fit on a Star column switches it to Fixed (or recomputes `StarWeight`)
- Iterative star distribution after clamping
- `CalculateAutoFitWidth(range)`; host disposes its measuring context

## Acceptance criteria
- [ ] Tests for Auto with sample, Star resize persistence, two-star-with-max redistribution
- [ ] Auto-fit on 100K rows completes < 50 ms

## Dependencies
Blocked by: `I2`
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### B7 — PR #9: pinned bottom summary footer must be reflected in hit-testing, ensure-visible and draw order (+ Core test)

Labels: `area:core`, `area:rendering`, `area:input`, `kind:bug`, `priority:high`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic B — Data engine correctness** · plan key `B7` · priority **high** · suggested model **sonnet**

## Problem
`PinBottomSummaryRows` (default true) draws a fixed footer but hit-testing still maps taps on it to the data row underneath, keyboard navigation hides the current row behind it, and drag-handle glyphs paint over it. It is the only Core change in PR #9 and has no test. Fix inside PR #9 before merging.

## Evidence
Findings: CORE-09
Reports: [01-core-data-layout-render.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/01-core-data-layout-render.md)

## Proposed change
- Add a footer hit region and exclude the band from row hit-testing
- Subtract footer height in `EnsureCurrentCellVisible`
- Draw the footer after the drag-handle column
- Core test for the pinned path; keep default `true`, note in CHANGELOG

## Acceptance criteria
- [ ] Tap on footer selects nothing; ArrowDown keeps the current row visible above the footer
- [ ] Test added and green; PR #9 merged afterwards

## Dependencies
Blocked by: none
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### B8 — Clip summary values to their pane; horizontal grid lines on frozen rows

Labels: `area:rendering`, `kind:bug`, `priority:medium`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic B — Data engine correctness** · plan key `B8` · priority **medium** · suggested model **sonnet**

## Problem
Summary cells of scrolled-off columns are drawn after pane clips are restored, so totals slide over the frozen pane; frozen rows get no horizontal grid lines (needs visual confirmation).

## Evidence
Findings: CORE-25
Reports: [01-core-data-layout-render.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/01-core-data-layout-render.md)

## Proposed change
- Draw summary values with the same 3-pass clipping as rows
- Draw grid lines for frozen rows

## Acceptance criteria
- [ ] Recording-context test: summary draw calls for scrollable columns are inside the scroll clip
- [ ] Visual check on the sample with frozen columns + bottom totals + horizontal scroll

## Dependencies
Blocked by: `N1`
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### B9 — DataGridSource lifecycle: IDisposable/Detach, unsubscribe INCC and every INPC item

Labels: `area:core`, `area:maui`, `area:uno`, `kind:bug`, `priority:medium`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic B — Data engine correctness** · plan key `B9` · priority **medium** · suggested model **sonnet**

## Problem
After a page is torn down the orphaned source stays subscribed to the app collection and every item, keeps rebuilding the view and keeps the view/canvas alive. INPC tracking allocates a `WeakReference` per item and untracks in O(n).

## Evidence
Findings: CORE-31, CORE-19
Reports: [01-core-data-layout-render.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/01-core-data-layout-render.md)

## Proposed change
- `IDisposable` on `DataGridSource` (unsubscribe all, clear caches); hosts call it from handler disconnect/Unloaded
- Replace the weak-reference list with a reference-equality `HashSet` (or walk the source on Reset); skip duplicates

## Acceptance criteria
- [ ] Test: after `Dispose`, a collection change raises nothing and no handler remains
- [ ] 100K items track/untrack in O(1) each

## Dependencies
Blocked by: none
Blocks: `G5`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### B10 — Group expand/collapse state keyed by raw key and pruned per path

Labels: `area:core`, `kind:bug`, `priority:low`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic B — Data engine correctness** · plan key `B10` · priority **low** · suggested model **sonnet**

## Problem
Groups merge by display text (two keys with the same `DisplayTextSelector` output collapse into one group) and all expand state is wiped when any group description changes.

## Evidence
Findings: CORE-49
Reports: [01-core-data-layout-render.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/01-core-data-layout-render.md)

## Proposed change
- Key groups and paths by raw key (via `KeySelector`)
- Prune only affected paths on description changes

## Acceptance criteria
- [ ] Tests for identical display text with distinct keys and for state survival when an inner level is removed

## Dependencies
Blocked by: `B1`
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### C1 — Edit session tracks the item, not the row index; commits or cancels on view changes

Labels: `area:editing`, `area:core`, `kind:bug`, `priority:blocker`, `model:opus` · Milestone: `0.2.0 – Stabilisation`

**Epic C — Editing pipeline** · plan key `C1` · priority **blocker** · suggested model **opus**

## Problem
An open editor survives sort, filter, group and collection changes and commits by stale visible-row index: the value lands on another item or `GetItem` throws out of the input handler. The editor overlay is never repositioned on scroll (`UpdateEditorBounds` has no callers).

## Evidence
Findings: EDIT-01, INPUT-01
Reports: [02-core-components-editing-input.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/02-core-components-editing-input.md)

## Proposed change
- `EditSession` stores the item + column; on `ViewChanged` commit to the item if still present, else cancel (decision Q7/R2)
- `GridInputController` commits/cancels before sort, filter, group toggles and reorders
- Editor bounds recomputed each frame from the item's current row; hide/commit when scrolled out of view
- Guard `SetCellValue`/`TryBeginEdit` with range checks

## Acceptance criteria
- [ ] Tests: edit → sort → Enter writes the original item; edit → filter removes item → cancelled; scroll while editing keeps the overlay on the cell
- [ ] No `ArgumentOutOfRangeException` reachable from `HandleKey`/`HandlePointer`

## Dependencies
Blocked by: `E1`, `E2`
Blocks: `N3`, `Q3`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### C2 — Editors never commit a value they could not represent (IValueEditor contract)

Labels: `area:editing`, `area:components`, `kind:bug`, `priority:blocker`, `model:opus` · Milestone: `0.2.0 – Stabilisation`

**Epic C — Editing pipeline** · plan key `C2` · priority **blocker** · suggested model **opus**

## Problem
ComboBox (value not in list → null), ScrollPicker (→ item 0), DatePicker (null → today, time-of-day stripped) and any custom editor (→ null) silently overwrite the cell on any outside tap, and `CellValueChanged` fires.

## Evidence
Findings: EDIT-02, COMP-12
Reports: [02-core-components-editing-input.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/02-core-components-editing-input.md)

## Proposed change
- `IValueEditor { object? Value; bool HasValue; bool IsDirty }` implemented by every editor; `CommitEdit` skips the write when `!HasValue` or value equals the original
- ComboBox/Picker start with no selection when the value is absent; nullable `DrawnDatePicker.SelectedDate`; merge time-of-day from the original value
- ScrollPicker commits on explicit confirm; display/value pairs instead of strings

## Acceptance criteria
- [ ] Test matrix: each editor × (value in list, value absent, null) → original preserved unless the user chose a value
- [ ] No `CellValueChanged` when nothing changed

## Dependencies
Blocked by: `C3`
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### C3 — Typed value conversion, validation and error UI following WPF/WinUI DataGrid conventions

Labels: `area:editing`, `area:core`, `kind:bug`, `priority:high`, `model:opus` · Milestone: `0.2.0 – Stabilisation`

**Epic C — Editing pipeline** · plan key `C3` · priority **high** · suggested model **opus**

## Problem
Text→typed conversion picks the first parse that works (decimal precision lost), swallows failures while reporting success, cannot write enum/Guid/DateOnly/TimeOnly/DateTimeOffset; `IDataErrorInfo` is evaluated on the old value, the custom validator short-circuits model validation, `Warning` is never shown and no message reaches the user; `SetCellValue` swallows exceptions.

## Evidence
Findings: EDIT-03, EDIT-04, CORE-16, COMP-17, COMP-11 (types)
Reports: [02-core-components-editing-input.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/02-core-components-editing-input.md), [01-core-data-layout-render.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/01-core-data-layout-render.md)

## Proposed change
- Resolve the target property type once per column; parse with culture + `NumberStyles`, `Enum.Parse`, `TypeConverter`; support every numeric primitive, decimal, enum, Guid, DateTime, DateOnly, TimeOnly, DateTimeOffset, TimeSpan, bool, string and nullable forms
- Empty text → null for nullable, validation error for non-nullable; conversion failure → validation error, editor stays open, no `CellValueChanged`
- Set-then-validate with rollback (or transient value) so model validation sees the new value; run model and delegate validation; keep Warning visible
- `ValidationFailed(CellValidationEventArgs)` event with message; visible error state per cell (border + icon + message surface) documented as the validation contract
- `SetCellValue` returns a result and raises `DataChanged` only on success

## Acceptance criteria
- [ ] Conversion test matrix for all listed types × culture (en-US, de-DE)
- [ ] Validation tests: model error blocks commit; warning shown; message delivered via event
- [ ] README 'Validation' section

## Dependencies
Blocked by: `E1`
Blocks: `C2`, `C4`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### C4 — Honour commit results, end the previous editor before starting another, fix event ordering

Labels: `area:editing`, `kind:bug`, `priority:high`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic C — Editing pipeline** · plan key `C4` · priority **high** · suggested model **sonnet**

## Problem
Callers ignore a failed `CommitEdit`, selection moves while `EditSession.IsEditing` stays true, `BeginEdit` overwrites the active editor without unregistering it, `CellValueChanged` fires before `EndEdit`, custom factories drop the typing-trigger character, `ToggleBooleanCell` bypasses validation.

## Evidence
Findings: EDIT-05, EDIT-07
Reports: [02-core-components-editing-input.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/02-core-components-editing-input.md)

## Proposed change
- `BeginEdit`: commit first, abort on failure; `EndEdit` before replacing the editor
- Pointer-up: if commit fails, redraw and keep focus
- Fire `CellValueChanged` after `EndEdit`; pass `initialCharacter` to custom factories; validate in `ToggleBooleanCell`

## Acceptance criteria
- [ ] Tests for each path; `FocusManager` holds exactly one editor at any time

## Dependencies
Blocked by: `C3`
Blocks: `C5`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### C5 — Action buttons: CanExecute, disabled state, and a non-editing interactive cell path

Labels: `area:components`, `area:editing`, `kind:bug`, `priority:high`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic C — Editing pipeline** · plan key `C5` · priority **high** · suggested model **sonnet**

## Problem
`ICommand.Execute` runs without `CanExecute`, every tap commits a bogus `null` through `EditSession` (raising `CellValueChanged` and a rebuild), and a synchronous delete of the last row crashes the tap handler.

## Evidence
Findings: COMP-04
Reports: [02-core-components-editing-input.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/02-core-components-editing-input.md)

## Proposed change
- Check `CanExecute`, listen to `CanExecuteChanged`, render disabled buttons
- Value-less editors complete via cancel, not commit; introduce an 'interactive cell' path that bypasses `EditSession`

## Acceptance criteria
- [ ] Sample Delete on the last row does not throw
- [ ] No `CellValueChanged`/`DataChanged` from a button tap

## Dependencies
Blocked by: `C4`
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### C6 — Editors use the grid's CellFont and popups flip/clamp to the viewport

Labels: `area:components`, `area:editing`, `kind:bug`, `priority:medium`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic C — Editing pipeline** · plan key `C6` · priority **medium** · suggested model **sonnet**

## Problem
Every editor hard-codes `new GridFont("Default", 13)`, so registered CJK/icon fonts garble again in edit mode and the text size jumps; date/combo/picker popups always open below and run off short grids.

## Evidence
Findings: COMP-15, COMP-06
Reports: [02-core-components-editing-input.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/02-core-components-editing-input.md)

## Proposed change
- `ApplyTheme`/factory set `Font = style.CellFont`
- Pass viewport size into `BeginEdit`/`PopupManager.Show`; flip above when below does not fit, else shift and shrink

## Acceptance criteria
- [ ] Edit a CJK cell with a registered font: identical glyphs and size
- [ ] Editing the bottom row of a 400 px grid shows the full calendar

## Dependencies
Blocked by: none
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### C7 — ProgressBar column type and tri-state Boolean editing (or remove the promise)

Labels: `area:core`, `area:components`, `kind:enhancement`, `priority:medium`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic C — Editing pipeline** · plan key `C7` · priority **medium** · suggested model **sonnet**

## Problem
README advertises `ColumnType="ProgressBar"` (does not parse) and a three-state checkbox editor; `DrawnCheckBox`/`DrawnProgressBar` are never created by the pipeline.

## Evidence
Findings: COMP-16, DOC-02 (ProgressBar)
Reports: [02-core-components-editing-input.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/02-core-components-editing-input.md), [06-docs.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/06-docs.md)

## Proposed change
- Add `DataGridColumnType.ProgressBar` wired to `ProgressBarCellRenderer` (read-only)
- `bool?` columns cycle null→true→false via `DrawnCheckBox` (internal)

## Acceptance criteria
- [ ] README XAML for ProgressBar parses and renders
- [ ] Tri-state test

## Dependencies
Blocked by: `F2`
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### D1 — Core text-input contract (composition-aware) and a grapheme-correct DrawnTextBox with undo/redo

Labels: `area:editing`, `area:components`, `area:input`, `kind:enhancement`, `priority:blocker`, `model:opus` · Milestone: `0.2.0 – Stabilisation`

**Epic D — Text input & editor components** · plan key `D1` · priority **blocker** · suggested model **opus**

## Problem
Both hosts feed the grid through a hidden native text box diffed character by character and reset after every change; IME composition is destroyed by design, surrogate pairs split, there is no undo/redo, word/line selection, or clipboard, and caret hit-testing is O(n²). The hidden-proxy design is being replaced (decision R2-Q4).

## Evidence
Findings: COMP-07, COMP-08, COMP-19, COMP-03 (Core part), MAUI-27, UNO-05
Reports: [02-core-components-editing-input.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/02-core-components-editing-input.md), [03-skiasharp-maui.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/03-skiasharp-maui.md), [04-uno.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/04-uno.md)

## Proposed change
- `ITextInputSession` in Core: `InsertText(string)`, `SetComposition(text, selection)`, `CommitComposition`, `DeleteBackward/Forward` by grapheme, selection/caret sync, `GridKey` generalised to key + modifiers + text
- `DrawnTextBox`: `Rune`/`StringInfo` cursor ops, composition rendering (underline), undo/redo stack, double/triple-click, drag-select with autoscroll, prefix-width cache, paste sanitised through the numeric policy, `EnsureCursorVisible` on draw
- `EditSession` exposes `ClipboardCopyRequested`/`ClipboardPasteRequested` and wires every editor

## Acceptance criteria
- [ ] Headless tests: composition start/update/commit sequences, emoji/combining-mark deletion, undo/redo, selection gestures
- [ ] Design note in docs/ describing the adapter contract for hosts

## Dependencies
Blocked by: `F1`
Blocks: `D2`, `D3`, `D4`, `D5`, `D6`, `D7`, `D8`, `D9`, `N3`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### D2 — iOS / Mac Catalyst text-input adapter: UITextInput with marked text and UIKeyCommand

Labels: `area:maui`, `kind:enhancement`, `priority:high`, `model:opus` · Milestone: `0.2.0 – Stabilisation`

**Epic D — Text input & editor components** · plan key `D2` · priority **high** · suggested model **opus**

## Problem
The Apple responder implements only `IUIKeyInput`, so Japanese/Chinese/Korean keyboards cannot show candidates; keyboard navigation works only through `UIKeyCommand` on Apple platforms.

## Evidence
Findings: MAUI-27 (iOS), MAUI-03
Reports: [03-skiasharp-maui.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/03-skiasharp-maui.md)

## Proposed change
- Replace the responder with a `UITextInput` implementation bridging marked text, selection and `UITextInputDelegate` to `ITextInputSession`
- Keep `UIKeyCommand` navigation; add Cmd+C/V/X/Z/A mapping
- Catalyst: trackpad scroll through the input controller (see G6)

## Acceptance criteria
- [ ] Device check: Japanese keyboard with candidate bar commits kanji into a cell; emoji keyboard; hardware arrows/Tab/Escape
- [ ] No hidden `Entry` remains on iOS/Catalyst

## Dependencies
Blocked by: `D1`
Blocks: `D6`, `L1`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### D3 — Android text-input adapter: InputConnection-based editing and hardware-key navigation

Labels: `area:maui`, `kind:enhancement`, `priority:high`, `model:opus` · Milestone: `0.2.0 – Stabilisation`

**Epic D — Text input & editor components** · plan key `D3` · priority **high** · suggested model **opus**

## Problem
Android input goes through a hidden `Entry` reset mid-composition; hardware keyboards deliver only characters/Backspace/Enter.

## Evidence
Findings: MAUI-27 (Android), MAUI-03 (Android)
Reports: [03-skiasharp-maui.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/03-skiasharp-maui.md)

## Proposed change
- Custom `View` with `OnCreateInputConnection` returning a `BaseInputConnection` that maps `setComposingText`/`commitText`/`deleteSurroundingText` to `ITextInputSession`; `TextFlagNoSuggestions` where appropriate
- `DispatchKeyEvent`/`OnKeyPreIme` → `GridKey` with `MetaState` modifiers
- `WindowSoftInputMode` guidance; scroll the edited cell above the IME

## Acceptance criteria
- [ ] Device check: Gboard Japanese/Korean composition, voice/swipe input, Bluetooth keyboard navigation
- [ ] No hidden `Entry` remains on Android

## Dependencies
Blocked by: `D1`
Blocks: `D6`, `L1`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### D4 — Windows (MAUI) text-input adapter: CoreTextEditContext, key hooks and pointer modifiers

Labels: `area:maui`, `kind:enhancement`, `priority:high`, `model:opus` · Milestone: `0.2.0 – Stabilisation`

**Epic D — Text input & editor components** · plan key `D4` · priority **high** · suggested model **opus**

## Problem
On Windows only characters, Backspace and Enter reach the grid; arrows move the caret of an invisible TextBox, Tab leaves the page, F2/Home/End/PageUp/Down/Delete/Ctrl+A/C/V never arrive, and pointer modifiers are never populated so Extended selection behaves like Single.

## Evidence
Findings: MAUI-03 (Windows), MAUI-05
Reports: [03-skiasharp-maui.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/03-skiasharp-maui.md)

## Proposed change
- `CoreTextEditContext` (TSF) adapter for text/composition; `KeyDown`/`PreviewKeyDown`/`CharacterReceived` on the platform element mapped to `GridKey` + modifiers; mark handled to stop focus traversal except at the grid's Tab exit
- Populate `Modifiers` from `InputKeyboardSource` on every pointer event (also on Catalyst/Android via native state)

## Acceptance criteria
- [ ] Windows: every key in the README table works; Shift/Ctrl-click range and toggle selection work
- [ ] No hidden `Entry` remains on Windows

## Dependencies
Blocked by: `D1`
Blocks: `D6`, `L1`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### D5 — Uno text-input adapter: composition events, no keyboard trap, proxy invisible to hit-testing and assistive tech

Labels: `area:uno`, `kind:bug`, `priority:high`, `model:opus` · Milestone: `0.2.0 – Stabilisation`

**Epic D — Text input & editor components** · plan key `D5` · priority **high** · suggested model **opus**

## Problem
The Uno proxy marks every mapped key handled regardless of Core's result (Tab/Shift+Tab/Escape can never leave the grid), resets after every text change (IME destroyed), is hit-testable at the grid centre, is announced by screen readers, triggers bring-into-view and iOS-Safari zoom, forces `IsTabStop`/`TabIndex` on every load, and loses keyboard focus after touch edits.

## Evidence
Findings: UNO-02, UNO-03, UNO-05, UNO-21, UNO-24
Reports: [04-uno.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/04-uno.md)

## Proposed change
- Replace the sentinel/diff proxy with the `ITextInputSession` adapter using `TextBox.TextCompositionStarted/Changed/Ended` and `TextChanging` where available; Android `EditText.InputType` via `#if __ANDROID__`
- `HandleEditKey` returns `evt.Handled`; `e.Handled` set only when true; Tab exits at the last/first editable cell
- Proxy: `MinWidth/MinHeight=0`, top-left, `IsHitTestVisible=false`, `AccessibilityView.Raw`, `BringIntoViewRequested` handled, `FontSize=16`
- Set `IsTabStop` once in the ctor; never touch `TabIndex`; keep focus on touch end-edit and hide the keyboard explicitly

## Acceptance criteria
- [ ] Desktop/WASM: Tab leaves the grid at the last cell; Escape with nothing selected bubbles
- [ ] Narrator/VoiceOver do not announce the proxy
- [ ] Composition test sequences pass through the adapter

## Dependencies
Blocked by: `D1`
Blocks: `D6`, `G7`, `H3`, `L2`, `N5`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### D6 — Clipboard: Ctrl/Cmd+C copies the selection as TSV, copy/cut/paste inside editors on every host

Labels: `area:input`, `area:maui`, `area:uno`, `area:editing`, `kind:enhancement`, `priority:high`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic D — Text input & editor components** · plan key `D6` · priority **high** · suggested model **sonnet**

## Problem
`DrawnTextBox` raises clipboard events nobody subscribes to; Cut deletes without copying; hosts map no Ctrl/Cmd+letter keys; the grid has no copy-selection feature at all.

## Evidence
Findings: COMP-03
Reports: [02-core-components-editing-input.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/02-core-components-editing-input.md)

## Proposed change
- `GridInputController.CopySelection()` producing TSV (with optional headers) raised as `ClipboardCopyRequested`; hosts implement with `Clipboard.Default` (MAUI) / `DataTransfer.Clipboard` (Uno)
- Wire editor copy/cut/paste through `EditSession`; Cut only after a successful copy
- Hosts map Ctrl/Cmd+A/C/V/X/Z

## Acceptance criteria
- [ ] Select rows → Ctrl+C → paste into a spreadsheet gives a table
- [ ] Editor cut/copy/paste round-trip on every host

## Dependencies
Blocked by: `D1`, `D2`, `D3`, `D4`, `D5`
Blocks: `X3`, `X7`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### D7 — ComboBox and Picker editors: pointer scrolling, typeahead, keyboard commit, correct key routing while editing

Labels: `area:components`, `area:input`, `kind:bug`, `priority:high`, `model:opus` · Milestone: `0.2.0 – Stabilisation`

**Epic D — Text input & editor components** · plan key `D7` · priority **high** · suggested model **opus**

## Problem
Keyboard-only users cannot change a ComboBox value (Enter is intercepted before the editor), the dropdown cannot be scrolled by pointer, typing restarts the edit, Space toggles row selection inside a date picker, and the ScrollPicker commits on any tap with no physics.

## Evidence
Findings: INPUT-03, COMP-02, COMP-14, COMP-12
Reports: [02-core-components-editing-input.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/02-core-components-editing-input.md)

## Proposed change
- Focused editor gets first chance at Enter/Escape/Tab; `MoveHighlight` updates the selection; printable keys go to the editor only; unhandled keys are consumed while editing
- Dropdown drag/wheel/scrollbar scrolling; `IsFilterable` from a column flag; first-letter typeahead
- ScrollPicker: commit on confirm or settled snap; drive `UpdateAnimation` from the host frame loop

## Acceptance criteria
- [ ] Tests: F2 → Down×3 → Enter commits the third item; typing 'B' in an open ComboBox jumps, does not restart
- [ ] 50-item dropdown reachable by drag and wheel

## Dependencies
Blocked by: `D1`, `Q3`
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### D8 — DatePicker and NumericUpDown: culture-aware, typed entry, auto-repeat, sane limits

Labels: `area:components`, `kind:bug`, `priority:medium`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic D — Text input & editor components** · plan key `D8` · priority **medium** · suggested model **sonnet**

## Problem
The calendar is English/Sunday-first with a ±50-year window of which ~7 years are reachable, ignores the column `Format` and `MinDate/MaxDate`; the numeric editor has no typed entry, auto-repeat never ticks, displays `0.30000000000000004`, inverts Home/End and is unusable in narrow cells.

## Evidence
Findings: COMP-11, COMP-13
Reports: [02-core-components-editing-input.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/02-core-components-editing-input.md)

## Proposed change
- Use `CultureInfo.CurrentCulture.DateTimeFormat` (first day, day names), column `Format`, scrollable/typed year, wire min/max from column metadata
- NumericUpDown: typed entry reusing `DrawnTextBox`, repeat ticks from the frame loop, rounding to `DecimalPlaces`/step precision, Home=min/End=max, buttons sized from available width

## Acceptance criteria
- [ ] de-DE calendar starts on Monday with localised names
- [ ] Hold '+' for 1 s increments repeatedly; 0.1 steps display cleanly

## Dependencies
Blocked by: `D1`, `M1`
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### D9 — Filter popup: scrollable, virtualised value list with keyboard support, dismissal notifications and themed search box

Labels: `area:components`, `area:input`, `kind:bug`, `priority:blocker`, `model:opus` · Milestone: `0.2.0 – Stabilisation`

**Epic D — Text input & editor components** · plan key `D9` · priority **blocker** · suggested model **opus**

## Problem
The value list cannot be scrolled at all (`_isPanning` is never set, wheel never reaches popups), press toggles items so drag flips checkboxes, only ~10 values are reachable; the popup materialises every distinct value and re-copies on each keystroke; outside taps dismiss silently without telling the controller; the search box ignores the dark theme; anchors never follow scroll.

## Evidence
Findings: COMP-01, COMP-05, COMP-09 (theme), COMP-10, COMP-18
Reports: [02-core-components-editing-input.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/02-core-components-editing-input.md)

## Proposed change
- Press/drag/release semantics with threshold; route `InputAction.Scroll` to the hit popup; draggable scrollbar; Up/Down/Space/Enter
- Virtualised drawing from `_scrollOffset`; debounced search; cap with 'showing first N' and lazy load
- `PopupManager.PopupClosed` → `FilterPopupClosed`; outside tap swallowed (decision R2-Q11)
- Theme colours for the search box; one shadow; monotonic z-order; `UpdateAnchors(scroll)`

## Acceptance criteria
- [ ] Column with 10,000 distinct values: opens < 100 ms, every value reachable by drag, wheel and keyboard
- [ ] Outside tap closes the popup and does nothing else

## Dependencies
Blocked by: `D1`
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### E1 — Structured ViewChanged event (replaces DataChanged), ScrollChanged, consistent state APIs on DataGridSource

Labels: `area:core`, `area:api`, `kind:tech-debt`, `priority:high`, `model:opus` · Milestone: `0.2.0 – Stabilisation`

**Epic E — Identity model & events** · plan key `E1` · priority **high** · suggested model **opus**

## Problem
`DataChanged` is an argument-less `Action`: hosts and the edit/selection code cannot tell a repaint from a structural change; there is no scroll event; `GroupCollapsed` fires on expand with a misleading payload; sort/filter state is written directly on columns.

## Evidence
Findings: CORE-29, CORE-17, CORE-27
Reports: [01-core-data-layout-render.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/01-core-data-layout-render.md)

## Proposed change
- `EventHandler<ViewChangedEventArgs>` with kind (Redraw | Rebuild | Incremental(index, count)) and the source→view index map
- `ScrollChanged` on `ScrollState`; `GroupExpandedChanged { IsExpanded }`
- `SetSort(column, direction, order)`, `ClearSort`, `SetColumnFilter`, `SetFilter` as the only writers of sort/filter state; `IsMultiSortActive` cached
- `BeginUpdate`/`EndUpdate` batching (the API the instructions already promise)

## Acceptance criteria
- [ ] Every mutation raises exactly one `ViewChanged` of the right kind (tests)
- [ ] Both hosts and `EditSession`/`SelectionModel` consume `ViewChanged`

## Dependencies
Blocked by: `F1`
Blocks: `B4`, `C1`, `C3`, `E2`, `E3`, `G2`, `G4`, `I4`, `N2`, `Q2`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### E2 — Item-based selection model with read-only sets, Select/Deselect, deltas, and reconciliation on view changes

Labels: `area:core`, `area:api`, `kind:bug`, `priority:high`, `model:opus` · Milestone: `0.2.0 – Stabilisation`

**Epic E — Identity model & events** · plan key `E2` · priority **high** · suggested model **opus**

## Problem
Selection and current cell are visible-row indices that nothing remaps: after a filter, Enter/F2 throws (reproduced); after a sort the highlighted rows are different items; Ctrl+A selects group headers; `SelectedRows` is a mutable `HashSet` with no event on external mutation; `SelectionUnit.Cell` is unimplemented.

## Evidence
Findings: CORE-06, CORE-26, CORE-28, EDIT-06
Reports: [01-core-data-layout-render.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/01-core-data-layout-render.md), [02-core-components-editing-input.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/02-core-components-editing-input.md)

## Proposed change
- Selection holds item references (+ column for the current cell); `SelectedRows` becomes a read-only view computed from the current `ViewSnapshot`
- `Select/Deselect/SetCurrentCell/SelectRange/Clear`; `SelectionChanged` carries added/removed
- Reconcile on `ViewChanged`: removed items drop out, order changes keep identity
- Non-data rows excluded from range/all selection
- Remove `SelectionUnit.Cell` (tracked as a 1.x feature)

## Acceptance criteria
- [ ] Tests: select → sort/filter/remove → same items selected; Ctrl+A on grouped grid selects only data rows
- [ ] No public mutable collection on `SelectionModel`

## Dependencies
Blocked by: `E1`
Blocks: `C1`, `G1`, `Q3`, `X5`, `X7`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### E3 — DataGridColumn: configuration vs runtime state, change notification

Labels: `area:core`, `area:api`, `kind:tech-debt`, `priority:medium`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic E — Identity model & events** · plan key `E3` · priority **medium** · suggested model **sonnet**

## Problem
`Width`, `SortDirection`, `SortOrder`, `ActiveFilter` are publicly settable with no notification; the README's 'set SortDirection and Refresh' recipe sorts one column only; `Id` is a random unused int.

## Evidence
Findings: CORE-27, CORE-30 (Id)
Reports: [01-core-data-layout-render.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/01-core-data-layout-render.md)

## Proposed change
- Runtime state `internal set`; configuration properties raise `PropertyChanged` so hosts can invalidate
- Remove `Id` (or make it a stable user key)
- `Width` documented as an output for Star columns

## Acceptance criteria
- [ ] Changing `IsVisible`/`Width` from code repaints without `Refresh()`
- [ ] API analyzer file shows no public setter for runtime state

## Dependencies
Blocked by: `E1`
Blocks: `Q1`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### F1 — Naming pass: KumikoDataGrid control, collision-free type names, RowTappedEventArgs2, file = type

Labels: `area:api`, `area:core`, `area:maui`, `area:uno`, `kind:tech-debt`, `priority:high`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic F — API freeze & naming** · plan key `F1` · priority **high** · suggested model **sonnet**

## Problem
Public names collide with platform types in MAUI/Uno apps (`SelectionMode`, `SelectionChangedEventArgs`, `TextChangedEventArgs`, `TimeChangedEventArgs`, `ComboBoxItem`, `FocusManager`, `CheckState`) or with other grids (`DataGridView` is the WinForms control); `RowTappedEventArgs2` is public; several files do not match their types. All of this is breaking after 1.0.

## Evidence
Findings: DOC-13, API-01, MAUI-15, UNO-23
Reports: [06-docs.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/06-docs.md), [02-core-components-editing-input.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/02-core-components-editing-input.md), [03-skiasharp-maui.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/03-skiasharp-maui.md), [04-uno.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/04-uno.md)

## Proposed change
- Rename the host control to `KumikoDataGrid` on both hosts (namespaces stay `KumikoUI.*`)
- `DataGridSelectionMode`, `DataGridSelectionChangedEventArgs`, `DataGridSortDirection`, `GridTextChangedEventArgs`, `GridTimeChangedEventArgs`, `DrawnComboBoxItem`; `RowTappedEventArgs2` → `RowTappedEventArgs`
- Move `DataGridStyle` to `DataGridStyle.cs` etc.; rename `DataGridHostingExtensions.cs` to match its type
- Product name 'KumikoUI DataGrid' in all descriptions

## Acceptance criteria
- [ ] No public type shares a simple name with `Microsoft.Maui.Controls.*`, `Microsoft.UI.Xaml.*` or `System.Windows.Forms.*` (scripted check in CI)
- [ ] Samples and docs updated

## Dependencies
Blocked by: `A1`
Blocks: `D1`, `E1`, `F2`, `F3`, `F4`, `F6`, `K1`, `M1`, `P4`, `P6`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### F2 — Remove or internalise dead and unsafe public surface before the freeze

Labels: `area:api`, `area:core`, `kind:tech-debt`, `priority:medium`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic F — API freeze & naming** · plan key `F2` · priority **medium** · suggested model **sonnet**

## Problem
`RowStyle.Height`, `CellStyle.BorderColor/BorderWidth`, `GridPaint.Style`, legacy layout methods that ignore frozen panes, `FocusManager.FocusNext`, `DrawnTextBox.Validation`, `HitRegion.RowHeader/FrozenColumn`, `TryForwardInitialTap`, `GetCellScreenBounds(8 args)`, mutable `GridFont`, inclusive `GridRect.Contains`, a 7-colour type converter — all public, all unimplemented or internal plumbing.

## Evidence
Findings: CORE-30, CORE-44, CORE-45, CORE-47, API-01, SKIA-07
Reports: [01-core-data-layout-render.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/01-core-data-layout-render.md), [02-core-components-editing-input.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/02-core-components-editing-input.md), [03-skiasharp-maui.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/03-skiasharp-maui.md)

## Proposed change
- Delete or implement each item (list in the report); `FocusManager`, `PopupManager`, `CheckState`, `DrawnCheckBox`, `DrawnProgressBar` internal
- `HitTestResult` → documented `readonly record struct`; `GridFont` → `readonly record struct`; half-open `Contains`
- `GridColorTypeConverter` accepts standard colour names and `#RGB`/`#RRGGBBAA`; typed filter operators
- Seal leaf types; `EventHandler<T>` everywhere instead of `Action`

## Acceptance criteria
- [ ] PublicAPI.Unshipped.txt reviewed line by line; every remaining public member is documented and reachable

## Dependencies
Blocked by: `F1`
Blocks: `C7`, `F6`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### F3 — One hosting entry point: UseKumikoUI() on MAUI (with handler registration, diagnostics, font-alias bridging); delete the Uno no-op

Labels: `area:maui`, `area:uno`, `area:api`, `kind:tech-debt`, `priority:medium`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic F — API freeze & naming** · plan key `F3` · priority **medium** · suggested model **sonnet**

## Problem
MAUI's `UseSkiaKumikoUI` only calls `UseSkiaSharp()` and gives no KumikoUI-specific error when omitted; Uno's `UseKumikoUI` is a no-op that costs every consumer a Hosting.Abstractions dependency and misleads docs.

## Evidence
Findings: MAUI-21, UNO-12, SKIA-01 (ergonomics)
Reports: [03-skiasharp-maui.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/03-skiasharp-maui.md), [04-uno.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/04-uno.md)

## Proposed change
- MAUI: `UseKumikoUI(this MauiAppBuilder)` registers the canvas handler explicitly, bridges `ConfigureFonts` aliases into `SkiaFontRegistrar`, and throws a clear message from `OnHandlerChanged` if missing
- Uno: delete the extension and the package reference; document that no startup call is needed

## Acceptance criteria
- [ ] Omitting the MAUI call yields a KumikoUI-named exception
- [ ] MAUI font alias usable in `GridFont.Family`
- [ ] Uno nuspec has no Hosting.Abstractions dependency

## Dependencies
Blocked by: `F1`
Blocks: `J3`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### F4 — Host control base class and surface clean-up (ContentView/TemplatedView, no SetItemsSource, non-nullable Columns, ContentProperty on Uno)

Labels: `area:maui`, `area:uno`, `area:api`, `kind:tech-debt`, `priority:medium`, `model:opus` · Milestone: `0.2.0 – Stabilisation`

**Epic F — API freeze & naming** · plan key `F4` · priority **medium** · suggested model **opus**

## Problem
`DataGridView : Grid` exposes the whole layout API (consumers can `Children.Add`), hides `Layout.IsReadOnly` with `new`, carries duplicate `SetItemsSource/SetColumns` that collection handlers overwrite, types `Columns` as nullable, and the Uno twin lacks `ContentProperty` and uses `double` where MAUI uses `float`.

## Evidence
Findings: MAUI-24, MAUI-15, UNO-23
Reports: [03-skiasharp-maui.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/03-skiasharp-maui.md), [04-uno.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/04-uno.md)

## Proposed change
- Derive from `TemplatedView`/`ContentView` (MAUI) and a sealed `Control` (Uno) with private canvas/proxy children
- Remove `SetItemsSource/SetColumns`; `Columns` non-nullable; `[ContentProperty(Columns)]` on Uno; consistent `double` for heights
- `IsReadOnly` no longer shadows a base member

## Acceptance criteria
- [ ] Consumers cannot add children; XAML implicit content works on both hosts
- [ ] No CS8602 warnings in samples

## Dependencies
Blocked by: `F1`
Blocks: `F5`, `G1`, `G2`, `G3`, `G4`, `G5`, `G6`, `G9`, `H1`, `L2`, `N5`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### F5 — Implement grid-level IsReadOnly, AllowSorting and AllowFiltering in Core

Labels: `area:core`, `area:maui`, `area:uno`, `kind:bug`, `priority:high`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic F — API freeze & naming** · plan key `F5` · priority **high** · suggested model **sonnet**

## Problem
The three bindable/dependency properties exist on both hosts but nothing consumes them: `IsReadOnly="True"` still edits, `AllowSorting="False"` still sorts, the samples' 'Toggle Filtering' button does nothing.

## Evidence
Findings: MAUI-02, UNO-10
Reports: [03-skiasharp-maui.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/03-skiasharp-maui.md), [04-uno.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/04-uno.md)

## Proposed change
- `EditSession.IsReadOnly` gate in `TryBeginEdit`/`ToggleBooleanCell`; `GridInputController.AllowSorting/AllowFiltering` gating header tap and filter icon hit-test; renderer skips the icons
- Both hosts push the values into Core

## Acceptance criteria
- [ ] Tests for each flag in Core; sample toggles visibly work

## Dependencies
Blocked by: `F4`
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### F6 — API governance: PublicApiAnalyzers, package validation baseline, CS1591 as error, .editorconfig + dotnet format in CI

Labels: `area:api`, `area:ci`, `area:docs`, `kind:task`, `priority:high`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic F — API freeze & naming** · plan key `F6` · priority **high** · suggested model **sonnet**

## Problem
Nothing tracks the public surface; 96 public Core members lack XML docs although docs generation is on; 90 whitespace drifts; no analyzer or style enforcement.

## Evidence
Findings: PKG-04, CI-06, DOC-08, CORE-43
Reports: [05-tests-ci-packaging.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/05-tests-ci-packaging.md), [06-docs.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/06-docs.md), [01-core-data-layout-render.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/01-core-data-layout-render.md)

## Proposed change
- `Microsoft.CodeAnalysis.PublicApiAnalyzers` with `PublicAPI.Shipped.txt`/`Unshipped.txt` per package
- `EnablePackageValidation` + `PackageValidationBaselineVersion` from the first stable release
- Document the 96 members; `WarningsAsErrors` for CS1591; `.editorconfig`; `dotnet format --verify-no-changes` CI step; `AnalysisLevel=latest-recommended`

## Acceptance criteria
- [ ] CI fails on a new undocumented public member or an unrecorded API change

## Dependencies
Blocked by: `F1`, `F2`
Blocks: `X9`, `R2`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### G1 — MVVM selection surface: SelectedItem, SelectedItems, SelectedIndex, CurrentCell, SelectionChanged + commands on both hosts

Labels: `area:maui`, `area:uno`, `area:api`, `kind:enhancement`, `priority:high`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic G — Host surface & MVVM** · plan key `G1` · priority **high** · suggested model **sonnet**

## Problem
View models cannot bind or observe selection; the README tells users to read `HashSet<int>` of view-row indices that change meaning on every sort.

## Evidence
Findings: MAUI-04
Reports: [03-skiasharp-maui.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/03-skiasharp-maui.md)

## Proposed change
- Two-way `SelectedItem`, `SelectedItems` (IList), `SelectedIndex`, `CurrentCell` on `KumikoDataGrid`; `SelectionChanged` event + `SelectionChangedCommand`
- Backed by the item-based `SelectionModel`

## Acceptance criteria
- [ ] Sample 'MVVM' page binds `SelectedItem` and reacts to `SelectionChanged`; works after sort/filter

## Dependencies
Blocked by: `E2`, `F4`
Blocks: `P4`, `P7`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### G2 — Programmatic scrolling API and Scrolled event

Labels: `area:maui`, `area:uno`, `area:core`, `area:api`, `kind:enhancement`, `priority:medium`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic G — Host surface & MVVM** · plan key `G2` · priority **medium** · suggested model **sonnet**

## Problem
No `ScrollTo*`, no offsets, no scroll event: cannot bring a new or selected row into view or implement load-more.

## Evidence
Findings: MAUI-08
Reports: [03-skiasharp-maui.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/03-skiasharp-maui.md)

## Proposed change
- Core helper computing row Y incl. group headers/summaries; `ScrollToRow(int, position)`, `ScrollToItem(object)`, `ScrollToColumn`, bindable two-way `VerticalOffset/HorizontalOffset`, `Scrolled` event on both hosts

## Acceptance criteria
- [ ] `ScrollToItem` after a filter lands the item visible; `Scrolled` fires with offsets

## Dependencies
Blocked by: `E1`, `F4`
Blocks: `X2`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### G3 — Bindable Theme, GridStyle, ScrollSettings; FollowSystemTheme; height precedence

Labels: `area:maui`, `area:uno`, `kind:enhancement`, `priority:medium`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic G — Host surface & MVVM** · plan key `G3` · priority **medium** · suggested model **sonnet**

## Problem
`Theme`/`GridStyle`/`ScrollSettings` are CLR-only (no binding, no `AppThemeBinding`), a dark app shows a white grid, and `RowHeight`/`HeaderHeight` are silently lost when `Theme` is set afterwards (XAML attribute order changes the result). Uno has no `Theme` at all.

## Evidence
Findings: MAUI-06, MAUI-07, UNO-15
Reports: [03-skiasharp-maui.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/03-skiasharp-maui.md), [04-uno.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/04-uno.md)

## Proposed change
- Bindable/dependency properties with converters; `FollowSystemTheme` (default true when `GridStyle` untouched) subscribing to `RequestedThemeChanged`/`ActualThemeChanged`
- Keep height overrides in fields and re-apply whenever the style instance changes

## Acceptance criteria
- [ ] `Theme="{AppThemeBinding Light=Light, Dark=Dark}"` works; runtime OS theme switch follows
- [ ] Attribute order no longer matters

## Dependencies
Blocked by: `F4`
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### G4 — Expose grouping, sorting, filtering, row-drag and grid-line options as bindables with commands; public Invalidate()

Labels: `area:maui`, `area:uno`, `area:api`, `kind:enhancement`, `priority:medium`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic G — Host surface & MVVM** · plan key `G4` · priority **medium** · suggested model **sonnet**

## Problem
Grouping, summaries, sort/filter state, drag options, grid lines, fonts and colours are reachable only through `DataSource`/`GridStyle` from code-behind; there is no way to repaint after a column change except a full `Refresh()`.

## Evidence
Findings: MAUI-16
Reports: [03-skiasharp-maui.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/03-skiasharp-maui.md)

## Proposed change
- `GroupDescriptions`, `GroupSummaryRows`, `AllowRowDragDrop`, `ShowRowDragHandle`, `ShowHorizontalGridLines`, `ShowVerticalGridLines`, `AlternateRowBackground`, `CellValidator`, `RowTappedCommand`, `CellValueChangedCommand`, `SortChangedCommand`; re-surface `DataSource`/`InputController` events on the control; public `Invalidate()`

## Acceptance criteria
- [ ] Every README feature configurable from XAML on both hosts; instructions regenerated from the property list

## Dependencies
Blocked by: `F4`, `E1`
Blocks: `P4`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### G5 — Lifecycle and leak fixes on both hosts (cleanup completeness, unsubscribe bound collections, dispose the source)

Labels: `area:maui`, `area:uno`, `kind:bug`, `priority:medium`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic G — Host surface & MVVM** · plan key `G5` · priority **medium** · suggested model **sonnet**

## Problem
MAUI's `Cleanup` misses the long-press timer, the `Columns`/`TableSummaryRows` subscriptions and two native responder events; Uno never unsubscribes the bound collection on Unloaded; a long-lived VM collection roots every navigated-away page.

## Evidence
Findings: MAUI-13, UNO-11, CORE-31 (host side)
Reports: [03-skiasharp-maui.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/03-skiasharp-maui.md), [04-uno.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/04-uno.md)

## Proposed change
- Complete `Cleanup`/`Reconnect` pairs; `OnUnloaded` unsubscribes and `OnLoaded` re-subscribes; call `DataGridSource.Dispose` on final disconnect; stop timers when hidden

## Acceptance criteria
- [ ] Memory test: navigate to and from a grid page 20 times with a singleton VM → no growth

## Dependencies
Blocked by: `B9`, `F4`
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### G6 — Pointer and wheel semantics across hosts: hover action, cursor feedback, modifiers, normalised wheel deltas, pointer-id tracking, DPI from the canvas

Labels: `area:input`, `area:maui`, `area:uno`, `area:core`, `kind:bug`, `priority:medium`, `model:opus` · Milestone: `0.2.0 – Stabilisation`

**Epic G — Host surface & MVVM** · plan key `G6` · priority **medium** · suggested model **opus**

## Problem
Core has no pointer-down state (a hover move can start a pan), `Entered/Exited` map to `Cancelled`, `CursorChanged` is never consumed, Catalyst trackpad scroll bypasses the controller, wheel deltas assume 120-unit notches (trackpads scroll 3 rows per pixel on Uno), Shift+wheel does nothing, a tap before first paint divides by zero, Uno derives scale from the Grid instead of the canvas, a second finger resets drags, and `IsEnabled=false` still edits.

## Evidence
Findings: INPUT-08, MAUI-05 (Catalyst/Android), MAUI-11, MAUI-17, MAUI-26, UNO-14, UNO-18, UNO-19, UNO-20, INPUT-16, MAUI-19 (IsEnabled)
Reports: [02-core-components-editing-input.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/02-core-components-editing-input.md), [03-skiasharp-maui.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/03-skiasharp-maui.md), [04-uno.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/04-uno.md)

## Proposed change
- `InputAction.Hover` / `IsInContact`; hosts forward non-contact moves only to the hover branch and set the platform cursor from `CursorChanged`
- Wheel: precise/pixel deltas vs notches, Shift → horizontal, `Handled` only when the offset changed
- Catalyst pan through `HandlePointer`; modifiers populated from native state on Catalyst/Android
- Guard scale ≤ 0; Uno uses `_canvasView` for size and pointer origin; track `_activePointerId`; derive release button from `PointerUpdateKind`; gate on `IsEnabled`

## Acceptance criteria
- [ ] Desktop: resize cursor over grips, no pan on hover, trackpad scroll feels native on every host, page scrolls once the grid reaches its end

## Dependencies
Blocked by: `F4`
Blocks: `H3`, `Q2`, `Q4`, `Q5`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### G7 — Uno long-press via timer (parity with MAUI), swallow the following release

Labels: `area:uno`, `area:input`, `kind:bug`, `priority:medium`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic G — Host surface & MVVM** · plan key `G7` · priority **medium** · suggested model **sonnet**

## Problem
Uno relies on WinUI `Holding` (touch-only, Uno0001 on some heads) and does not suppress the `Released` that follows, so a long-press on a Boolean cell toggles twice and fires `RowTapped`.

## Evidence
Findings: UNO-13
Reports: [04-uno.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/04-uno.md)

## Proposed change
- 500 ms / 15 px timer started on press, cancelled on move/release, `_longPressFired` swallows the next release; `Holding` kept as fallback

## Acceptance criteria
- [ ] Host-level test with synthesised pointer sequences; Boolean long-press toggles once

## Dependencies
Blocked by: `D5`
Blocks: `H3`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### G9 — UnoActionButtonDefinition: XAML-bindable action buttons on Uno (parity with MauiActionButtonDefinition)

Labels: `area:uno`, `kind:enhancement`, `priority:low`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic G — Host surface & MVVM** · plan key `G9` · priority **low** · suggested model **sonnet**

## Problem
Uno has no `DependencyObject` action-button definition, so commands cannot be declared or bound in XAML; the sample builds the column in code.

## Evidence
Findings: UNO-23
Reports: [04-uno.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/04-uno.md)

## Proposed change
- `UnoActionButtonDefinition : DependencyObject, IActionButtonDefinition` with `Command`/`CommandParameter`/colours as dependency properties

## Acceptance criteria
- [ ] Uno MVVM sample page declares its buttons in XAML

## Dependencies
Blocked by: `F4`
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### H1 — Uno rendering surface: SKCanvasElement (GPU) with SKXamlCanvas fallback, frame-callback loop, DPI handling

Labels: `area:uno`, `area:rendering`, `area:perf`, `kind:enhancement`, `priority:high`, `model:opus` · Milestone: `0.2.0 – Stabilisation`

**Epic H — Uno host rework** · plan key `H1` · priority **high** · suggested model **opus**

## Problem
Every Skia-rendered head paints through software `SKXamlCanvas` (extra buffer copy, no GPU) driven by a 16 ms `DispatcherTimer` that repaints the whole surface, although `Uno.WinUI.Graphics2DSK` is already a dependency.

## Evidence
Findings: UNO-06, UNO-18
Reports: [04-uno.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/04-uno.md)

## Proposed change
- Internal `IGridSurface` with an `SKCanvasElement` implementation (`RenderOverride`, logical units) chosen when `IsSupportedOnCurrentPlatform()`, `SKXamlCanvas` for WinAppSDK; `SKSwapChainPanel` option on WinAppSDK
- `CompositionTarget.Rendering` frame loop that stops when idle; caret blink redraws only the editor rect; `XamlRoot.Changed` → invalidate on scale change

## Acceptance criteria
- [ ] Fling of 100K rows on WASM and macOS desktop stays smooth (frame time logged by the overlay from I6)
- [ ] `RenderMode` property documented

## Dependencies
Blocked by: `A2`, `A3`, `F4`
Blocks: `H3`, `Q5`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### H2 — Uno package hygiene: strip AndroidX content, drop dev-time dependencies, assert the nuspec in CI, fix metadata

Labels: `area:uno`, `area:packaging`, `area:ci`, `kind:task`, `priority:high`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic H — Uno host rework** · plan key `H2` · priority **high** · suggested model **sonnet**

## Problem
The multi-head pack sweeps ~1,200 AndroidX `content/` entries into the nupkg (documented as 'acceptable'); Lottie, Resizetizer, DevServer and Hosting.Abstractions are direct dependencies on every head; the description claims Mac Catalyst and tags lack wasm/desktop. The package has never been published, so this is the last cheap moment.

## Evidence
Findings: UNO-09, UNO-25, PKG-07
Reports: [04-uno.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/04-uno.md), [05-tests-ci-packaging.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/05-tests-ci-packaging.md)

## Proposed change
- `IncludeContentInPack=false` (or pack-exclude the Android content), drop `GenerateLibraryLayout`, minimal `UnoFeatures`, remove stray references
- CI step: `unzip -l` the nupkg, fail on `abc_` entries or unexpected dependencies, print size
- Fix Description/PackageTags

## Acceptance criteria
- [ ] KumikoUI.Uno nupkg < 2 MB with dependencies limited to Core, SkiaSharp, SkiaSharp.Views.*, Uno.WinUI(+runtime)

## Dependencies
Blocked by: `A3`
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### I1 — Allocation-free steady-state render loop (accessor and display-text caches, paint cache versioning, no LINQ in the render path)

Labels: `area:rendering`, `area:perf`, `area:core`, `kind:perf`, `priority:high`, `model:opus` · Milestone: `0.2.0 – Stabilisation`

**Epic I — Rendering performance & async pipeline** · plan key `I1` · priority **high** · suggested model **opus**

## Problem
Every visible cell allocates an accessor key string, a boxed value, a display string and a `GridPaint` per frame; `PaintCache` is rebuilt every frame; LINQ and per-row paints in group/summary drawing. The 'no per-frame allocations' rule in the instructions is not met.

## Evidence
Findings: CORE-10, CORE-23, CORE-46
Reports: [01-core-data-layout-render.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/01-core-data-layout-render.md)

## Proposed change
- Accessor cached per column via the K1 abstraction; display text cached per (item, column) invalidated by `ViewChanged`/INPC
- `PaintCache` rebuilt only when the style version changes; `CellRenderContext` passed to `ICellRenderer.Render` carrying shared paints (breaking, before 1.0)
- Remove LINQ/allocation from group chips, summary rows, sort/filter glyphs; reserve glyph space only when needed

## Acceptance criteria
- [ ] BenchmarkDotNet: `Render` of a 30×10 visible grid allocates 0 bytes in steady state
- [ ] dotnet-counters shows no Gen0 GCs during a 10 s fling of the 100K sample

## Dependencies
Blocked by: `K1`, `N1`
Blocks: `I5`, `I6`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### I2 — Cross-frame text measurement cache, grapheme-safe ellipsis, and text wrapping support in IDrawingContext

Labels: `area:rendering`, `area:skiasharp`, `kind:perf`, `priority:medium`, `model:opus` · Milestone: `0.2.0 – Stabilisation`

**Epic I — Rendering performance & async pipeline** · plan key `I2` · priority **medium** · suggested model **opus**

## Problem
Every frame re-measures and re-truncates every visible string (binary search over UTF-16 indices that can split emoji); `\n` renders as a tofu box and there is no wrapping even though per-row heights are planned.

## Evidence
Findings: CORE-34, SKIA-05, SKIA-06
Reports: [01-core-data-layout-render.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/01-core-data-layout-render.md), [03-skiasharp-maui.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/03-skiasharp-maui.md)

## Proposed change
- (text, font, width) → (truncated text, width) cache invalidated by style/data version
- Truncate by `StringInfo` text elements / `SKFont.BreakText`; drop the 20 px magic
- `GridTextWrap` (none/word/char) + `MeasureText(text, paint, maxWidth)` on `IDrawingContext`

## Acceptance criteria
- [ ] Benchmark: second frame of a static grid performs zero `MeasureText` calls
- [ ] Emoji at the ellipsis boundary renders intact; multi-line cell renders wrapped when enabled

## Dependencies
Blocked by: `A2`
Blocks: `B6`, `X4`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### I3 — Long-lived Skia resource cache per control; registrar O(1) ownership, Unregister, safe Clear

Labels: `area:skiasharp`, `area:perf`, `kind:perf`, `priority:medium`, `model:opus` · Milestone: `0.2.0 – Stabilisation`

**Epic I — Rendering performance & async pipeline** · plan key `I3` · priority **medium** · suggested model **opus**

## Problem
`SkiaDrawingContext` is created per frame and rebuilds every paint, font and typeface (with a font-manager lookup per family per frame); `IsRegistered` scans a dictionary per typeface per frame; `Clear()` can dispose typefaces a frame still uses.

## Evidence
Findings: SKIA-03, SKIA-08, SKIA-10
Reports: [03-skiasharp-maui.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/03-skiasharp-maui.md)

## Proposed change
- `SkiaResourceCache` owned by the control (typeface/font/paint keyed by value), `SkiaDrawingContext` becomes a thin per-frame wrapper; clear on theme/font change
- Registry version counter; `Unregister(family)`; `HashSet` ownership; UI-thread documentation
- `SKFontEdging.Antialias` + explicit hinting; expose text rendering options

## Acceptance criteria
- [ ] No native Skia allocation in steady-state frames (benchmark)
- [ ] Registrar tests for replace/unregister/clear during rendering

## Dependencies
Blocked by: `A2`
Blocks: `G8`, `I5`, `J2`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### I4 — Off-UI-thread view pipeline: immutable ViewSnapshot, background sort/filter/group/summaries with coalescing, incremental aggregates

Labels: `area:core`, `area:perf`, `kind:enhancement`, `priority:high`, `model:opus` · Milestone: `0.2.0 – Stabilisation`

**Epic I — Rendering performance & async pipeline** · plan key `I4` · priority **high** · suggested model **opus**

## Problem
Collection/property changes rebuild `_viewIndices` on the raising thread while the UI thread renders from them (no contract); every INPC change on a summarised property forces a full O(n log n) rebuild; summaries materialise boxed lists; grouping allocates per row; filtering formats every row per filter.

## Evidence
Findings: CORE-05, CORE-19, CORE-20, CORE-21, CORE-22, MAUI-12, CORE-11 (BeginUpdate)
Reports: [01-core-data-layout-render.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/01-core-data-layout-render.md), [03-skiasharp-maui.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/03-skiasharp-maui.md)

## Proposed change
- Mutations are UI-thread-only (captured context, debug assert, clear exception); Core builds the view on a worker from an immutable snapshot with cancellation and coalescing and publishes a `ViewSnapshot` swapped atomically on the UI thread; sources < ~1,000 rows rebuild synchronously
- Incremental Sum/Count/Avg; single-pass aggregates; struct `FlatViewRow`; reuse index buffers; format only when a value-list filter is active
- `BeginUpdate/EndUpdate` batching

## Acceptance criteria
- [ ] 100 random INPC updates on 100K rows with a Sum summary: UI thread blocked < 16 ms total
- [ ] Background `Add` from `Task.Run` throws the documented exception (test)

## Dependencies
Blocked by: `E1`, `K1`
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### I7 — InertialScroller edge cases: hold-then-release, boundary stop, timestamp guard, frame-delta driven

Labels: `area:input`, `area:core`, `kind:bug`, `priority:low`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic I — Rendering performance & async pipeline** · plan key `I7` · priority **low** · suggested model **sonnet**

## Problem
Releasing after a 1 s hold still flings; a fling into the end of the list keeps the frame loop alive ~2 s; `TimestampMs=0` silently disables inertia; the host hard-codes 16 ms.

## Evidence
Findings: INPUT-14
Reports: [02-core-components-editing-input.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/02-core-components-editing-input.md)

## Proposed change
- Zero velocity when `now - last > ~100 ms`; stop when `ScrollBy` did not move; diagnostic for missing timestamps; accept measured delta

## Acceptance criteria
- [ ] Unit tests for each case (existing vacuous test replaced)

## Dependencies
Blocked by: none
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### J1 — Unknown font family must not crash: fallback to default, log once, null-safe disposal

Labels: `area:skiasharp`, `kind:bug`, `priority:high`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic J — Fonts & glyph rendering** · plan key `J1` · priority **high** · suggested model **sonnet**

## Problem
`SKTypeface.FromFamilyName` returns null on Android/Windows for an unknown family (e.g. a MAUI font alias); the null is cached and dereferenced in `Dispose()` at the end of the first frame → crash.

## Evidence
Findings: SKIA-01
Reports: [03-skiasharp-maui.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/03-skiasharp-maui.md)

## Proposed change
- `?? SKTypeface.Default`, log once per family, null-guard disposal; unit test with an unknown family on Linux CI

## Acceptance criteria
- [ ] `new GridFont("NoSuchFont", 13)` renders with the default face on every platform

## Dependencies
Blocked by: `A2`
Blocks: `N4`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### J2 — Per-glyph font fallback (run segmentation via SKFontManager.MatchCharacter), registrar fallback list, bold/italic for registered faces

Labels: `area:skiasharp`, `area:rendering`, `kind:enhancement`, `priority:high`, `model:opus` · Milestone: `0.2.0 – Stabilisation`

**Epic J — Fonts & glyph rendering** · plan key `J2` · priority **high** · suggested model **opus**

## Problem
Text is drawn with one typeface per run: mixed Latin + CJK or any emoji renders tofu unless a single registered font covers every glyph; registered families ignore bold/italic (sample headers render regular).

## Evidence
Findings: SKIA-02, SKIA-04
Reports: [03-skiasharp-maui.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/03-skiasharp-maui.md)

## Proposed change
- Segment strings into runs by typeface using `ContainsGlyphs`/`MatchCharacter`, cache per (family, style, codepoint), draw/measure run by run (evaluate `SkiaSharp.HarfBuzz` for shaping as a follow-up)
- Registrar accepts fallback families and `(family, SKFontStyle)` variants; synthetic bold/italic when a weight is missing

## Acceptance criteria
- [ ] Cell "José 👍 東京" renders every glyph on Android/Windows/WASM
- [ ] Registered NotoSansJP bold header renders bold

## Dependencies
Blocked by: `I3`
Blocks: `X10`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### J3 — KumikoFonts helper on MAUI (parity with Uno), shared sample fonts, licence notices

Labels: `area:maui`, `area:docs`, `area:repo`, `kind:enhancement`, `priority:medium`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic J — Fonts & glyph rendering** · plan key `J3` · priority **medium** · suggested model **sonnet**

## Problem
MAUI registers Skia fonts through sample-private blocking code while Uno has a library helper; both samples duplicate 9 MB of fonts; Open Sans, Material Icons (Apache-2.0) and Noto Sans JP (OFL-1.1) ship without their licence texts.

## Evidence
Findings: REPO-07, REPO-05, MAUI-25 (font pipelines)
Reports: [05-tests-ci-packaging.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/05-tests-ci-packaging.md), [03-skiasharp-maui.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/03-skiasharp-maui.md)

## Proposed change
- `KumikoFonts.RegisterFromAppPackageAsync` in KumikoUI.Maui; one `samples/Shared/Fonts` folder linked into both samples
- `LICENSE-*.txt`/OFL next to the fonts and a root `THIRD-PARTY-NOTICES.md`

## Acceptance criteria
- [ ] Both samples register fonts through the library helper; licence files present

## Dependencies
Blocked by: `F3`
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### K1 — AOT-safe property access: source generator, delegate accessors, annotated reflection fallback; IsAotCompatible everywhere

Labels: `area:core`, `area:api`, `area:packaging`, `kind:enhancement`, `priority:high`, `model:opus` · Milestone: `0.2.0 – Stabilisation`

**Epic K — Native AOT & trimming** · plan key `K1` · priority **high** · suggested model **opus**

## Problem
Core reads properties through `Expression.Compile()` and `Type.GetProperty`, which Native AOT and trimming cannot support; main shows 4 IL2075 sites under `IsAotCompatible`; PR #1 (stale since April) swaps in slower reflection without tests or numbers. Native AOT support is a 1.0 goal (decision R1-Q10/R2-Q3).

## Evidence
Findings: REPO-02, MAUI-23, CORE-33 (accessor abstraction), PR #1
Reports: [05-tests-ci-packaging.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/05-tests-ci-packaging.md), [03-skiasharp-maui.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/03-skiasharp-maui.md), [01-core-data-layout-render.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/01-core-data-layout-render.md)

## Proposed change
- `IPropertyAccessorProvider` abstraction with three tiers: (1) `[KumikoGridItem]` source generator emitting typed accessors (recommended path), (2) public `ValueAccessor`/`ValueSetter` delegates on `DataGridColumn` and `DisplaySelector`/`ValueSelector` on ComboBox, (3) reflection fallback annotated `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]` on the `PropertyName` setters (compiled expression where `RuntimeFeature.IsDynamicCodeSupported`, `PropertyInfo` otherwise)
- `IsAotCompatible`/`IsTrimmable` + analyzers on Core, SkiaSharp, MAUI, Uno with zero IL warnings; dictionary/`DataRow` providers become possible
- Close PR #1 as superseded with a comment

## Acceptance criteria
- [ ] Build with zero IL2xxx/IL3xxx warnings
- [ ] Tests: generator output, delegate precedence, nested-null, write-only, `SetCellValue` via setter
- [ ] Benchmark: generated and delegate paths ≥ compiled-expression speed

## Dependencies
Blocked by: `F1`
Blocks: `B2`, `I1`, `I4`, `K2`, `P4`, `X6`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### M1 — GridStrings provider for every built-in string and culture-aware formatting

Labels: `area:i18n`, `area:core`, `area:components`, `kind:enhancement`, `priority:high`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic M — Localisation** · plan key `M1` · priority **high** · suggested model **sonnet**

## Problem
'Grouped by:', '(Blank)', 'Sum:', 'Search...', 'Select All', 'OK', 'Cancel', 'Clear', 'Sort A-Z', day names and AM/PM are hard-coded English; sort labels are wrong for numeric/date columns.

## Evidence
Findings: CORE-32, COMP-09, COMP-11 (culture)
Reports: [01-core-data-layout-render.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/01-core-data-layout-render.md), [02-core-components-editing-input.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/02-core-components-editing-input.md)

## Proposed change
- `GridStrings` (resource-backed, overridable) on `DataGridStyle`/`DataGridSource` used by renderer, popup and editors; type-aware sort labels
- Calendar and numbers from `CultureInfo.CurrentCulture`

## Acceptance criteria
- [ ] Setting `GridStrings` to a German table localises every visible string; test asserts no literal English remains in Core

## Dependencies
Blocked by: `F1`
Blocks: `D8`, `P4`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### N1 — Recording IDrawingContext fake and DataGridRenderer command tests (passes, frozen panes, summaries, group rows, overlays)

Labels: `area:tests`, `area:rendering`, `kind:task`, `priority:blocker`, `model:opus` · Milestone: `0.2.0 – Stabilisation`

**Epic N — Tests** · plan key `N1` · priority **blocker** · suggested model **opus**

## Problem
The 1,786-line renderer and its 3-pass clipping have zero automated coverage; the fake context specified in the testing instructions was never created.

## Evidence
Findings: CORE-35, TEST-02, TEST-01
Reports: [01-core-data-layout-render.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/01-core-data-layout-render.md), [05-tests-ci-packaging.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/05-tests-ci-packaging.md)

## Proposed change
- `RecordingDrawingContext` (monospace measure, recorded ops with clip state) in Core tests
- Golden-command tests per pass and per feature (header, rows, frozen rows/columns, top/bottom/pinned summaries, group headers/chips, drag overlays, editor)

## Acceptance criteria
- [ ] Renderer tests cover every `Draw*` method; CORE-03/CORE-25 regressions caught

## Dependencies
Blocked by: `A1`
Blocks: `B8`, `I1`, `N3`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### N2 — DataGridSource, layout and selection behaviour tests (INCC/INPC, nested grouping, summary values, sort stability, culture, null and heterogeneous items)

Labels: `area:tests`, `area:core`, `kind:task`, `priority:high`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic N — Tests** · plan key `N2` · priority **high** · suggested model **sonnet**

## Problem
The existing 353 tests concentrate on models; grouping/summary/collection-change paths and layout edge cases are untested, and several tests are tautological (`EditTriggerResolutionTests` tests a local copy; vacuous `if (IsActive)` assertions).

## Evidence
Findings: CORE-36, CORE-37, TEST-05
Reports: [01-core-data-layout-render.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/01-core-data-layout-render.md), [05-tests-ci-packaging.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/05-tests-ci-packaging.md)

## Proposed change
- Cases listed in CORE-36/37; culture fixtures; replace tautological tests with calls to the real members (`InternalsVisibleTo`)

## Acceptance criteria
- [ ] Every public `DataGridSource` member has a behavioural test; no test asserts only `NotNull`

## Dependencies
Blocked by: `B1`, `E1`
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### N3 — Editing, input-controller, hit-testing, popup/focus and drawn-component tests

Labels: `area:tests`, `area:editing`, `area:input`, `kind:task`, `priority:blocker`, `model:opus` · Milestone: `0.2.0 – Stabilisation`

**Epic N — Tests** · plan key `N3` · priority **blocker** · suggested model **opus**

## Problem
`EditSession`, `CellEditorFactory`, `GridInputController`, `GridHitTester`, `PopupManager`, `FocusManager` and every `Drawn*` component have zero coverage; the last three Uno commits fixed exactly these paths with no safety net.

## Evidence
Findings: TEST-01 (report 02), TEST-01 (report 05)
Reports: [02-core-components-editing-input.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/02-core-components-editing-input.md), [05-tests-ci-packaging.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/05-tests-ci-packaging.md)

## Proposed change
- Conversion matrix; edit lifecycle incl. view changes; key routing while editing; `EnsureCurrentCellVisible`, Tab navigation, reorder math; hit-test matrix (frozen/summary/group/drag handle); each component's pointer/keyboard state machine through synthesised event sequences

## Acceptance criteria
- [ ] Every public type in Core has at least one behavioural test; coverage of `Input/` and `Editing/` ≥ 70%

## Dependencies
Blocked by: `N1`, `D1`, `C1`
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### N4 — SkiaSharp tests: raster golden images, measure/draw consistency, font fallback, registrar lifecycle

Labels: `area:tests`, `area:skiasharp`, `kind:task`, `priority:medium`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic N — Tests** · plan key `N4` · priority **medium** · suggested model **sonnet**

## Problem
KumikoUI.SkiaSharp has no tests although SkiaSharp runs headless on Linux CI.

## Evidence
Findings: SKIA-11, TEST-02
Reports: [03-skiasharp-maui.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/03-skiasharp-maui.md), [05-tests-ci-packaging.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/05-tests-ci-packaging.md)

## Proposed change
- `tests/KumikoUI.SkiaSharp.Tests`: render a small grid to `SKSurface` with an embedded deterministic font and compare to committed PNGs with tolerance; unknown family, mixed-script fallback, ellipsis on surrogate pairs, registrar replace/clear, dispose idempotence

## Acceptance criteria
- [ ] Golden tests run on ubuntu/windows/macos runners

## Dependencies
Blocked by: `A2`, `J1`
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### N5 — Host tests: Uno pure helpers and desktop-head runtime tests; MAUI property → Core mapping

Labels: `area:tests`, `area:uno`, `area:maui`, `kind:task`, `priority:medium`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic N — Tests** · plan key `N5` · priority **medium** · suggested model **sonnet**

## Problem
Uno tests cover only the enum mapper; the composition adapter, tap detector, wheel normaliser and property callbacks are untested; MAUI has no tests at all; Uno tests are not in the publish gate.

## Evidence
Findings: TEST-07, UNO-16
Reports: [05-tests-ci-packaging.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/05-tests-ci-packaging.md), [04-uno.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/04-uno.md)

## Proposed change
- Extract pure helpers into testable classes; `net10.0-desktop` runtime tests for property → renderer state; `KumikoUI.Maui.Tests` for bindable → Core mapping
- Publish workflow runs all test projects

## Acceptance criteria
- [ ] Host test projects green in CI and required by the publish gate

## Dependencies
Blocked by: `D5`, `F4`
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### N6 — Code coverage collection, reporting and ratchet; test dependency refresh

Labels: `area:tests`, `area:ci`, `kind:task`, `priority:medium`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic N — Tests** · plan key `N6` · priority **medium** · suggested model **sonnet**

## Problem
coverlet is referenced but never collected; no badge or threshold; test SDK packages are a year behind.

## Evidence
Findings: TEST-04, TEST-08
Reports: [05-tests-ci-packaging.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/05-tests-ci-packaging.md)

## Proposed change
- `--collect:"XPlat Code Coverage"` + Cobertura artifact + summary comment on PRs; threshold starting at current value and ratcheting; bump xunit/Test SDK/coverlet (evaluate xunit v3 + Microsoft.Testing.Platform)

## Acceptance criteria
- [ ] PRs show a coverage delta; CI fails below the threshold

## Dependencies
Blocked by: `A1`
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### N7 — Compiled documentation snippets: every README/instructions code block builds in CI

Labels: `area:tests`, `area:docs`, `kind:task`, `priority:high`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic N — Tests** · plan key `N7` · priority **high** · suggested model **sonnet**

## Problem
Nine README snippets and ~15 identifiers in the Copilot instructions reference members that do not exist; nothing catches drift.

## Evidence
Findings: DOC-02, CORE-11, DOC-11
Reports: [06-docs.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/06-docs.md), [01-core-data-layout-render.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/01-core-data-layout-render.md)

## Proposed change
- `tests/KumikoUI.Docs.Tests` (or a script) that extracts ```csharp/```xml blocks from README.md and `.github/instructions/*.md` into a compiled sample page per host; a scripted check that every backticked identifier in instruction files exists as a public declaration

## Acceptance criteria
- [ ] CI fails when a documented member disappears

## Dependencies
Blocked by: `P4`
Blocks: `P6`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### O1 — Release gate: rulesets on main, required checks, tag protection, environment reviewers, dispatch validation, all test projects in the gate

Labels: `area:ci`, `area:repo`, `kind:task`, `priority:blocker`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic O — CI/CD & release engineering** · plan key `O1` · priority **blocker** · suggested model **sonnet**

## Problem
`main` is unprotected, the publish environment has no reviewers, `workflow_dispatch` publishes any version string from any ref, and only the Core suite gates a publish; three of ten tag publishes already failed and burned versions.

## Evidence
Findings: CI-01
Reports: [05-tests-ci-packaging.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/05-tests-ci-packaging.md)

## Proposed change
- Ruleset: PRs required, required status checks (Core+Skia, MAUI, Uno, samples), linear history, no force-push, `v*` tags protected
- `nuget-publish`: required reviewer + `main`-only deployment branches
- publish.yml: version regex, tag reachable from `main`, depends on every test job
- Repo settings steps documented for the maintainer (manual)

## Acceptance criteria
- [ ] A tag pushed from a feature branch does not publish; a publish requires approval

## Dependencies
Blocked by: `A5`
Blocks: `O4`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### O2 — CI matrix: macOS job for Apple heads and both samples, Android sample, OS matrix for Core tests, timeouts, test reporter, doc-only no-op job

Labels: `area:ci`, `kind:task`, `priority:high`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic O — CI/CD & release engineering** · plan key `O2` · priority **high** · suggested model **sonnet**

## Problem
Samples and Apple app heads are never built; Core/SkiaSharp are tested on Linux only; no timeouts; TRX only as artifact; `paths-ignore` for `**.md` will block doc-only PRs once checks are required.

## Evidence
Findings: CI-02, CI-07, CI-08, UNO-16 (samples)
Reports: [05-tests-ci-packaging.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/05-tests-ci-packaging.md), [04-uno.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/04-uno.md)

## Proposed change
- `build-samples` on macos-latest (MAUI ios/maccatalyst, Uno ios/desktop/wasm) and ubuntu (android); Core+Skia tests on ubuntu/windows/macos; `timeout-minutes`; `dorny/test-reporter`; `dorny/paths-filter` with a no-op job for docs-only changes

## Acceptance criteria
- [ ] A sample compile break on any head fails CI

## Dependencies
Blocked by: `A4`, `A3`
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### O3 — Supply-chain automation: Dependabot (nuget + actions), CodeQL, least-privilege permissions, SHA-pinned and updated actions, secret scanning

Labels: `area:ci`, `area:repo`, `kind:task`, `priority:high`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic O — CI/CD & release engineering** · plan key `O3` · priority **high** · suggested model **sonnet**

## Problem
No Dependabot or CodeQL; default workflow token permission is write; actions are 2–4 majors behind on floating tags; push protection disabled.

## Evidence
Findings: CI-04, CI-05
Reports: [05-tests-ci-packaging.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/05-tests-ci-packaging.md)

## Proposed change
- `.github/dependabot.yml` (grouped weekly); CodeQL default setup for C#; `permissions: contents: read` at workflow level, `contents: write` only on the publish job; bump and SHA-pin third-party actions; enable push protection; OpenSSF Scorecard baseline

## Acceptance criteria
- [ ] Scorecard ≥ 6; Dependabot PRs flowing

## Dependencies
Blocked by: none
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### O4 — Publishing hardening: NuGet Trusted Publishing (OIDC), required symbol packages, release notes from CHANGELOG, dispatch parity

Labels: `area:ci`, `area:packaging`, `kind:task`, `priority:medium`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic O — CI/CD & release engineering** · plan key `O4` · priority **medium** · suggested model **sonnet**

## Problem
A long-lived API key secret publishes; missing `.snupkg` only warns; GitHub releases are created only on tag push with auto-generated notes.

## Evidence
Findings: CI-08, PKG-01 (notes)
Reports: [05-tests-ci-packaging.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/05-tests-ci-packaging.md)

## Proposed change
- `NuGet/login` OIDC, drop `EIGHTBOT_NUGET_APIKEY`; `if-no-files-found: error` for symbols; `PackageReleaseNotes` and the GitHub release body from the CHANGELOG section; `release-dry-run` on PRs to main

## Acceptance criteria
- [ ] A tagged release publishes four packages + symbols with the changelog as notes, no static secret

## Dependencies
Blocked by: `A5`, `P2`, `O1`
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### P1 — Community and governance files: CONTRIBUTING, SECURITY (private vulnerability reporting), PR template, YAML issue forms, CODEOWNERS, labels, milestones, Discussions, branch model

Labels: `area:repo`, `area:docs`, `kind:task`, `priority:high`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic P — Governance & documentation** · plan key `P1` · priority **high** · suggested model **sonnet**

## Problem
No CONTRIBUTING (PR #9 links a non-existent one under the wrong org), no SECURITY, stock web-app issue templates, default labels, no milestones, Discussions off, `develop` vs `main` unclear, stale branches.

## Evidence
Findings: REPO-01, REPO-03, DOC-15, DOC-16
Reports: [05-tests-ci-packaging.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/05-tests-ci-packaging.md), [06-docs.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/06-docs.md)

## Proposed change
- CONTRIBUTING (branch model `main` + tags, build/test per project, Conventional Commits, PR checklist, release steps); SECURITY.md pointing to GitHub private vulnerability reporting (enabled); PR template; YAML issue forms with package/host/platform/version fields; CODEOWNERS; CoC contact = maintainer handle; enable Discussions; delete merged branches; retire `develop`

## Acceptance criteria
- [ ] Community profile 100%; PR #9's link fixed; `develop` removed from CI triggers

## Dependencies
Blocked by: none
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### P2 — CHANGELOG (Keep a Changelog) and versioning/support policy

Labels: `area:docs`, `area:packaging`, `kind:task`, `priority:high`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic P — Governance & documentation** · plan key `P2` · priority **high** · suggested model **sonnet**

## Problem
No changelog or release notes beyond auto-generated PR titles; no statement that the four packages version in lockstep or how breaking changes are handled.

## Evidence
Findings: PKG-01 (notes), DOC-07
Reports: [05-tests-ci-packaging.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/05-tests-ci-packaging.md), [06-docs.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/06-docs.md)

## Proposed change
- CHANGELOG.md with an Unreleased section back-filled from v0.1.3; `docs/VERSIONING.md` (SemVer 2, lockstep packages, support window per .NET major, deprecation policy)

## Acceptance criteria
- [ ] Every release from 0.2.0 has a dated section; publish job reads it

## Dependencies
Blocked by: none
Blocks: `O4`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### P3 — Repository assets: re-encode the showcase GIF, MP4 release asset, template leftovers, .gitignore additions, legal name consistency

Labels: `area:repo`, `area:docs`, `kind:task`, `priority:low`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic P — Governance & documentation** · plan key `P3` · priority **low** · suggested model **sonnet**

## Problem
`images/large_data.gif` is 15 MB, Uno template leftovers remain, `TestResults/`/coverage files are not ignored, and the copyright holder is spelled three ways.

## Evidence
Findings: REPO-05, REPO-06, REPO-08
Reports: [05-tests-ci-packaging.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/05-tests-ci-packaging.md)

## Proposed change
- Re-encode the GIF to ~2–3 MB (keep it in the README) and publish the full demo as an MP4 release asset; delete `ReadMe.md`/`SharedAssets.md`/`AboutAssets.txt`/`dotnet_bot.png` leftovers; ignore `TestResults/`, `*.trx`, `coverage*`, `BenchmarkDotNet.Artifacts/`, `.claude/`; 'Eight-Bot, Inc.' everywhere

## Acceptance criteria
- [ ] Repo clone < 10 MB of images; README still shows the demo

## Dependencies
Blocked by: none
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### P4 — README rewrite: host-neutral, accurate claims, compiled snippets, Uno getting started, platform matrix, theming reference, FAQ, column reorder and validation

Labels: `area:docs`, `kind:task`, `priority:blocker`, `model:opus` · Milestone: `0.2.0 – Stabilisation`

**Epic P — Governance & documentation** · plan key `P4` · priority **blocker** · suggested model **opus**

## Problem
The README describes a three-project MAUI-only library, contains nine snippets that do not compile, claims 60 fps, canvas scrollbars and zero native controls, omits Uno entirely, shows 13 of ~95 style members (7 wrong) and mis-states filter, drag and multi-sort behaviour.

## Evidence
Findings: DOC-02, DOC-03, DOC-05, DOC-06, DOC-12, DOC-14, DOC-17, UNO-07, CORE-38, MAUI-20, DOCS-01
Reports: [06-docs.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/06-docs.md), [04-uno.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/04-uno.md), [01-core-data-layout-render.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/01-core-data-layout-render.md), [03-skiasharp-maui.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/03-skiasharp-maui.md), [02-core-components-editing-input.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/02-core-components-editing-input.md)

## Proposed change
- Side-by-side MAUI/Uno getting started; packages table with KumikoUI.Uno; supported-platforms matrix; every snippet taken from the compiled docs tests; remove unmeasured claims until I6 publishes numbers; document column reorder, validation, `ScrollSettings`, `PinBottomSummaryRows`, keyboard matrix per platform, accessibility statement, performance guidance; FAQ seeded from issues #2/#5/#6; theming reference generated from `DataGridStyle`

## Acceptance criteria
- [ ] Every claim in the README maps to a test, a benchmark or a sample; zero unverifiable numbers

## Dependencies
Blocked by: `F1`, `G1`, `G4`, `K1`, `M1`
Blocks: `N7`, `P5`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### P5 — Per-package NuGet READMEs (plain Markdown, absolute images) and corrected package tags

Labels: `area:docs`, `area:packaging`, `kind:task`, `priority:blocker`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic P — Governance & documentation** · plan key `P5` · priority **blocker** · suggested model **sonnet**

## Problem
The 34 KB GitHub README with raw HTML and relative images is packed into all four packages and renders as literal tags on nuget.org; Core is tagged `maui`.

## Evidence
Findings: DOC-01, PKG-03, PKG-08
Reports: [06-docs.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/06-docs.md), [05-tests-ci-packaging.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/05-tests-ci-packaging.md)

## Proposed change
- `docs/nuget/README.<Package>.md` per package (≤150 lines, absolute raw.githubusercontent URLs, shields badges, no HTML, no emoji headings) set via `PackageReadmeFile`; remove the global include; fix tags; preview on the nuget.org upload tab before 0.2.0

## Acceptance criteria
- [ ] nuget.org page for each 0.2.0 package renders logo, badges and the quick start

## Dependencies
Blocked by: `P4`
Blocks: `R3`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### P6 — Internal docs regenerated from code: ARCHITECTURE, RENDERING, Copilot instructions and prompts, testing instructions, Uno instructions; docs/uno becomes a user guide

Labels: `area:docs`, `kind:task`, `priority:medium`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic P — Governance & documentation** · plan key `P6` · priority **medium** · suggested model **sonnet**

## Problem
ARCHITECTURE/RENDERING list folders, classes, counts and pipeline steps that do not exist; the instruction files and prompts generate code against non-existent members; `docs/uno` still says no Uno code exists.

## Evidence
Findings: DOC-09, DOC-10, DOC-11, TEST-09, REPO-04, UNO-17, DOC-04, CORE-11
Reports: [06-docs.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/06-docs.md), [05-tests-ci-packaging.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/05-tests-ci-packaging.md), [04-uno.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/04-uno.md), [01-core-data-layout-render.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/01-core-data-layout-render.md)

## Proposed change
- Regenerate trees and tables from the source; real method names and pass order; four-project diagrams; `uno-control.instructions.md`; `docs/uno/README.md` rewritten as the Uno user guide with 01–08 moved to `docs/uno/plan/`

## Acceptance criteria
- [ ] Identifier check from N7 passes for every instruction file

## Dependencies
Blocked by: `N7`, `F1`
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### Q1 — Column header reorder: documented, tested, pane-aware drop index, DisplayIndex and layout persistence

Labels: `area:input`, `area:core`, `area:docs`, `kind:enhancement`, `priority:high`, `model:opus` · Milestone: `0.2.0 – Stabilisation`

**Epic Q — Features & interaction** · plan key `Q1` · priority **high** · suggested model **opus**

## Problem
Header drag-reorder exists but is undocumented (the single most common competitor feature), the drop index ignores frozen panes and the drag handle, and there is no `DisplayIndex`/state persistence.

## Evidence
Findings: INPUT-10, CORE-33 (DisplayIndex), research §1.3
Reports: [02-core-components-editing-input.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/02-core-components-editing-input.md), [01-core-data-layout-render.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/01-core-data-layout-render.md), [07-external-research.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/07-external-research.md)

## Proposed change
- Pane-aware drop slot computed from screen column boundaries excluding the dragged column; restricted to the source pane; `NewIndex` semantics documented
- `DisplayIndex` + `GridLayoutState` export/import (sort, filter, group, column order/width/visibility)
- README section and sample

## Acceptance criteria
- [ ] Tests for drop index with left/right frozen panes; state round-trips through JSON

## Dependencies
Blocked by: `E3`
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### Q2 — Row drag and drop: long-press start on touch, drag on mouse, edge autoscroll, correct ghost offset, ReorderRow semantics

Labels: `area:input`, `area:core`, `kind:bug`, `priority:high`, `model:opus` · Milestone: `0.2.0 – Stabilisation`

**Epic Q — Features & interaction** · plan key `Q2` · priority **high** · suggested model **opus**

## Problem
`AllowRowDragDrop` turns any 8 px vertical touch move into a row drag (users cannot scroll), the ghost ignores the group panel offset, rows cannot be dragged beyond the viewport, and `ReorderRow` silently no-ops on sorted or non-`IList` sources.

## Evidence
Findings: INPUT-04, INPUT-11, CORE-48
Reports: [02-core-components-editing-input.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/02-core-components-editing-input.md), [01-core-data-layout-render.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/01-core-data-layout-render.md)

## Proposed change
- Touch: long-press starts the full-row drag; mouse: drag from row; handle column always; edge-zone autoscroll on the frame loop
- `ReorderRow` returns a result and rejects sorted/non-list sources with a diagnostic; fires once

## Acceptance criteria
- [ ] Touch scroll works with drag enabled; dragging to the viewport edge scrolls; tests for drop math under grouping

## Dependencies
Blocked by: `G6`, `E1`
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### Q3 — Keyboard navigation completeness: Tab exit, Enter edits, EditTriggers honoured, ensure-visible math with frozen rows/summaries/group panel, Space toggles, page sizes

Labels: `area:input`, `area:core`, `kind:bug`, `priority:high`, `model:opus` · Milestone: `0.2.0 – Stabilisation`

**Epic Q — Features & interaction** · plan key `Q3` · priority **high** · suggested model **opus**

## Problem
Auto-scroll adds `OffsetY` twice and ignores frozen rows/summaries/group panel; Tab is a keyboard trap that ignores `EditTriggers.None` and can scan rows × cols cells; Enter does nothing in normal mode; Boolean cells toggle on every tap and twice on double-tap; F2 opens an invisible editor; PageUp/Down overshoot.

## Evidence
Findings: INPUT-02, INPUT-05, INPUT-06, INPUT-09, INPUT-13, INPUT-17, UNO-02 (Core)
Reports: [02-core-components-editing-input.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/02-core-components-editing-input.md), [04-uno.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/04-uno.md)

## Proposed change
- Correct `EnsureCurrentCellVisible` with all bands; Tab leaves the grid at the last/first editable cell (unhandled) and skips `EditTriggers=None`; Enter begins edit; Boolean gating + Space; `TryBeginEdit` scrolls into view; page size from the visible data band; empty-area tap clears selection (configurable)

## Acceptance criteria
- [ ] Every row of the README keyboard table has a controller test; Tab exits on all desktop hosts

## Dependencies
Blocked by: `E2`, `C1`
Blocks: `D7`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### Q4 — ContextRequested event: right-click and long-press raise it and never sort, edit or drag; CellTapped/HeaderTapped

Labels: `area:input`, `area:api`, `area:maui`, `area:uno`, `kind:enhancement`, `priority:medium`, `model:sonnet` · Milestone: `0.2.0 – Stabilisation`

**Epic Q — Features & interaction** · plan key `Q4` · priority **medium** · suggested model **sonnet**

## Problem
Core never reads the pointer button: right-click sorts headers, selects rows, starts drags and edits; hosts cannot offer a context menu; `RowTapped` fires only for cells.

## Evidence
Findings: INPUT-07
Reports: [02-core-components-editing-input.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/02-core-components-editing-input.md)

## Proposed change
- `ContextRequested(row item, column, position, trigger)`; `CellTapped`/`HeaderTapped`; ignore non-primary buttons for sort/edit/drag; hosts surface the event and a command

## Acceptance criteria
- [ ] Right-click on a header does not sort; sample shows a context menu on both hosts

## Dependencies
Blocked by: `G6`
Blocks: `X5`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

# Milestone: 1.0.0-preview

### G8 — Image columns: IImageResolver with caching, async decode and disposal on both hosts

Labels: `area:rendering`, `area:maui`, `area:uno`, `area:skiasharp`, `kind:enhancement`, `priority:medium`, `model:opus` · Milestone: `1.0.0-preview`

**Epic G — Host surface & MVVM** · plan key `G8` · priority **medium** · suggested model **opus**

## Problem
`DrawImage(object)` accepts only `SKImage`/`SKBitmap`; binding an `ImageSource`, URL, `byte[]` or stream yields an empty cell with no diagnostic, and view models must own decoded Skia objects.

## Evidence
Findings: SKIA-09
Reports: [03-skiasharp-maui.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/03-skiasharp-maui.md)

## Proposed change
- Core `IImageResolver` hook; hosts resolve `ImageSource`/`Uri`/`Stream`/`byte[]` → `SKImage` with an LRU cache, async decode and invalidate on completion, disposal on eviction; log ignored types

## Acceptance criteria
- [ ] Sample page with image column bound to URLs and resources on every host; no leak after scrolling 10K rows

## Dependencies
Blocked by: `I3`
Blocks: `P7`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### H3 — Uno device verification matrix: every head exercised interactively with a recorded checklist

Labels: `area:uno`, `area:tests`, `kind:task`, `priority:high`, `model:opus` · Milestone: `1.0.0-preview`

**Epic H — Uno host rework** · plan key `H3` · priority **high** · suggested model **opus**

## Problem
No Uno head has been exercised interactively (the branch's own commit messages say so; the smoke matrix is entirely unchecked). Uno's GA label at 1.0 is gated on this matrix passing (decision Q3 + R2-Q13).

## Evidence
Findings: UNO-16, docs/uno/07 smoke matrix
Reports: [04-uno.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/04-uno.md)

## Proposed change
- Checklist per head (Windows, Desktop macOS/Linux, WASM in Chrome/Safari, Android emulator, iOS simulator): render, scroll/fling, wheel, select, edit (text/IME/combo/date), keyboard nav + Tab exit, filter popup, drag, theme, DPI change, screen reader smoke
- Record results in docs/uno/verification.md; file issues for each failure

## Acceptance criteria
- [ ] Every row of the matrix green or linked to an open issue; decision recorded whether KumikoUI.Uno ships as 1.0.0 or 1.0.0-preview

## Dependencies
Blocked by: `H1`, `D5`, `G6`, `G7`
Blocks: `R3`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### I5 — MAUI GPU render mode (SKGLView), platform frame callback, partial redraw

Labels: `area:maui`, `area:rendering`, `area:perf`, `kind:enhancement`, `priority:medium`, `model:opus` · Milestone: `1.0.0-preview`

**Epic I — Rendering performance & async pipeline** · plan key `I5` · priority **medium** · suggested model **opus**

## Problem
`SKCanvasView` rasterises the full surface on the CPU on every invalidate, caret blink repaints the whole grid every 530 ms, and the 16 ms timer is independent of vsync so fling distance differs between 60/120 Hz devices.

## Evidence
Findings: MAUI-14, MAUI-18, INPUT-14 (host part)
Reports: [03-skiasharp-maui.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/03-skiasharp-maui.md), [02-core-components-editing-input.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/02-core-components-editing-input.md)

## Proposed change
- `RenderMode = Auto | Software | Gpu` (`SKGLView`), Auto = GPU where available with software fallback and a documented switch
- `CADisplayLink`/`Choreographer`/`CompositionTarget.Rendering` frame loop stopping when idle; measured frame delta passed to `UpdateInertialScroll`
- Editor-rect-only redraw for caret blink

## Acceptance criteria
- [ ] Frame-time overlay shows ≥ 55 fps fling on a mid-range Android phone at 3× density (recorded in docs)
- [ ] Fling distance identical at 60 and 120 Hz

## Dependencies
Blocked by: `I1`, `I3`, `I6`
Blocks: `Q5`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### I6 — BenchmarkDotNet project and frame-time overlay; performance budgets and published numbers

Labels: `area:perf`, `area:tests`, `area:docs`, `kind:task`, `priority:high`, `model:sonnet` · Milestone: `1.0.0-preview`

**Epic I — Rendering performance & async pipeline** · plan key `I6` · priority **high** · suggested model **sonnet**

## Problem
The README claims 60 fps and 100K rows with no benchmark or instrumentation anywhere; PR #1 changes the hot path without numbers.

## Evidence
Findings: TEST-03, DOC-05
Reports: [05-tests-ci-packaging.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/05-tests-ci-packaging.md), [06-docs.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/06-docs.md)

## Proposed change
- `benchmarks/KumikoUI.Benchmarks`: accessor paths, sort/filter/group on 100K rows, `Render` with a no-op context, `ComputeColumnWidths`, text cache; nightly/manual workflow publishing results
- Frame-time overlay toggle in both samples' Large Data page
- Budgets: zero steady-state allocations, published fps per reference device; README quotes measured numbers only

## Acceptance criteria
- [ ] Benchmark workflow green with results artifact; README performance section cites a table with device/date

## Dependencies
Blocked by: `I1`
Blocks: `I5`, `R3`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### K2 — AOT and trimming CI: PublishAot samples (iOS, Mac Catalyst, Windows), PublishTrimmed WASM, console smoke with warnings as errors

Labels: `area:ci`, `area:packaging`, `kind:task`, `priority:high`, `model:sonnet` · Milestone: `1.0.0-preview`

**Epic K — Native AOT & trimming** · plan key `K2` · priority **high** · suggested model **sonnet**

## Problem
Without a publish job the AOT claim cannot be defended; the Uno WASM sample preserves only the app assembly.

## Evidence
Findings: K1 follow-up, UNO-22 (LinkerConfig)
Reports: [04-uno.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/04-uno.md), [05-tests-ci-packaging.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/05-tests-ci-packaging.md)

## Proposed change
- Jobs publishing SampleApp.Maui with `PublishAot` (ios/maccatalyst on macOS, windows on Windows), SampleApp.Uno with `PublishTrimmed` wasm; tiny console referencing Core+SkiaSharp with `PublishAot` and `TreatWarningsAsErrors`
- README 'Native AOT' section with the three tiers

## Acceptance criteria
- [ ] All AOT jobs green; README states 'Native AOT supported' with the conditions

## Dependencies
Blocked by: `K1`, `A4`
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### L1 — MAUI accessibility quick wins: proxy hidden from assistive tech, grid-level semantics and announcements

Labels: `area:a11y`, `area:maui`, `kind:enhancement`, `priority:high`, `model:sonnet` · Milestone: `1.0.0-preview`

**Epic L — Accessibility** · plan key `L1` · priority **high** · suggested model **sonnet**

## Problem
VoiceOver/TalkBack/Narrator announce an empty text field inside 'Data grid'; the rows/columns hint updates only on data change; nothing announces the current cell, edit state or validation errors.

## Evidence
Findings: MAUI-09, A11Y-01 (quick wins)
Reports: [03-skiasharp-maui.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/03-skiasharp-maui.md), [02-core-components-editing-input.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/02-core-components-editing-input.md)

## Proposed change
- Exclude the input adapter from the accessibility tree; hint updated on selection/scroll; `SemanticScreenReader.Announce` for current cell value, edit begin/end and validation failures; focus ring contract for drawn controls
- README accessibility statement (what works, what is planned)

## Acceptance criteria
- [ ] Screen reader walkthrough recorded on iOS and Android: no phantom text field, current cell announced

## Dependencies
Blocked by: `D2`, `D3`, `D4`
Blocks: `L3`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### L2 — Uno accessibility quick wins: automation peer with name/help text, proxy marked Raw, keyboard exit

Labels: `area:a11y`, `area:uno`, `kind:enhancement`, `priority:high`, `model:sonnet` · Milestone: `1.0.0-preview`

**Epic L — Accessibility** · plan key `L2` · priority **high** · suggested model **sonnet**

## Problem
The Uno grid derives from a Panel with no automation peer, so it is invisible to Narrator/VoiceOver, while the hidden proxy text box is announced; the default `GridDescription` is never applied.

## Evidence
Findings: UNO-03, UNO-04
Reports: [04-uno.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/04-uno.md)

## Proposed change
- `OnCreateAutomationPeer` → `FrameworkElementAutomationPeer` subclass (`AutomationControlType.DataGrid`, name = `GridDescription`, help text = rows/columns/current cell refreshed on change); default name in ctor

## Acceptance criteria
- [ ] Narrator on Windows and VoiceOver on macOS desktop announce the grid with its description and hint

## Dependencies
Blocked by: `D5`, `F4`
Blocks: `L3`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### P7 — Sample apps: README, Images & Styling page, Events/MVVM page, DisplayAlertAsync, cached commands, Android soft-input mode, WASM linker config

Labels: `area:docs`, `area:maui`, `area:uno`, `kind:task`, `priority:low`, `model:sonnet` · Milestone: `1.0.0-preview`

**Epic P — Governance & documentation** · plan key `P7` · priority **low** · suggested model **sonnet**

## Problem
Samples cover ~60% of README features, use an obsolete `DisplayAlert`, allocate commands per getter, hide the editing cell under the Android keyboard and preserve only the app assembly for WASM trimming.

## Evidence
Findings: MAUI-25, UNO-22
Reports: [03-skiasharp-maui.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/03-skiasharp-maui.md), [04-uno.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/04-uno.md)

## Proposed change
- `samples/README.md` with per-head run commands; new pages for images/styling/events/selection; `AdjustResize`; `LinkerConfig.xml` includes KumikoUI.Core (until K1 lands); navigation guard

## Acceptance criteria
- [ ] Every README feature is demonstrated on both samples

## Dependencies
Blocked by: `G1`, `G8`
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### Q5 — Interaction polish for a premium feel: animations, hover and pressed states, cursor feedback, momentum tuning, haptics, exact hit regions

Labels: `area:input`, `area:rendering`, `area:maui`, `area:uno`, `kind:enhancement`, `priority:high`, `model:opus` · Milestone: `1.0.0-preview`

**Epic Q — Features & interaction** · plan key `Q5` · priority **high** · suggested model **opus**

## Problem
Interaction quality is the product's differentiator and today it has approximated hit regions (group chips), 6 px touch targets for column grips, no hover/pressed feedback, no animation on expand/collapse or drag, momentum untuned per platform, and several double-fires.

## Evidence
Findings: INPUT-12, INPUT-15, decision R2-Q11
Reports: [02-core-components-editing-input.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/02-core-components-editing-input.md)

## Proposed change
- Hit regions published by the renderer (chip rects, grips sized from `DataGridStyle` and pointer type); hover/pressed visual states on headers, chips, buttons, checkboxes; animated group expand/collapse, drag ghost lift and drop, selection transitions; per-platform momentum presets (iOS/Android/desktop); optional haptics on mobile for drag start/drop and long-press; consistent timings table in docs

## Acceptance criteria
- [ ] Interaction review checklist signed off on each platform; no double-fire in the state-machine tests

## Dependencies
Blocked by: `G6`, `I5`, `H1`
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

# Milestone: 1.0.0

### R1 — 1.0.0 release checklist (tracking issue)

Labels: `area:repo`, `kind:epic`, `priority:high`, `model:sonnet` · Milestone: `1.0.0`

**Epic R — 1.0.0 release** · plan key `R1` · priority **high** · suggested model **sonnet**

## Problem
Single place to track the release gate: blockers closed, API frozen, device matrix, docs, package previews, tag.

## Evidence
Findings: release-readiness §3
Reports: [README.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)

## Proposed change
- Checklist referencing every 0.2.0/preview issue, the Uno GA/preview decision, PublicAPI.Shipped sign-off, package validation baseline, nuget.org README preview, CHANGELOG, announcement

## Acceptance criteria
- [ ] All boxes ticked before `v1.0.0`

## Dependencies
Blocked by: none
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### R2 — API review sign-off: freeze PublicAPI.Shipped.txt, enable package validation baseline, final naming audit

Labels: `area:api`, `area:packaging`, `kind:task`, `priority:high`, `model:opus` · Milestone: `1.0.0`

**Epic R — 1.0.0 release** · plan key `R2` · priority **high** · suggested model **opus**

## Problem
The last chance to change anything public.

## Evidence
Findings: PKG-04, DOC-13
Reports: [05-tests-ci-packaging.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/05-tests-ci-packaging.md), [06-docs.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/06-docs.md)

## Proposed change
- Line-by-line review of every public member across four packages against the naming policy; move Unshipped → Shipped; `PackageValidationBaselineVersion=1.0.0` on the first post-1.0 build

## Acceptance criteria
- [ ] Review notes committed; no `Unshipped` entries at tag time

## Dependencies
Blocked by: `F6`
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### R3 — Final docs pass and launch: README numbers from benchmarks, CHANGELOG 1.0.0 section, nuget.org README previews, announcement draft

Labels: `area:docs`, `kind:task`, `priority:medium`, `model:sonnet` · Milestone: `1.0.0`

**Epic R — 1.0.0 release** · plan key `R3` · priority **medium** · suggested model **sonnet**

## Problem
Docs must reflect measured numbers and the final API.

## Evidence
Findings: DOC-05, P4/P5 follow-up
Reports: [06-docs.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/06-docs.md)

## Proposed change
- Update performance section from I6 results, platform matrix from H3, changelog, preview all four package READMEs, draft announcement

## Acceptance criteria
- [ ] Maintainer sign-off

## Dependencies
Blocked by: `I6`, `H3`, `P5`
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

# Milestone: 1.x – Fast follow-ons

### L3 — Spec: virtual accessibility tree with per-cell navigation (WinUI IGridProvider, iOS UIAccessibilityContainer, Android ExploreByTouchHelper, WASM ARIA)

Labels: `area:a11y`, `area:core`, `area:maui`, `area:uno`, `kind:spec`, `priority:medium`, `model:opus` · Milestone: `1.x – Fast follow-ons`

**Epic L — Accessibility** · plan key `L3` · priority **medium** · suggested model **opus**

## Problem
A canvas-drawn grid is one opaque node per platform. Full screen-reader support needs a platform-neutral semantic tree (visible rows × columns, headers, group and summary rows, state, actions) and four native adapters; Uno's Skia accessibility on Android/iOS is still work in progress upstream.

## Evidence
Findings: A11Y-01, research §4
Reports: [02-core-components-editing-input.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/02-core-components-editing-input.md), [07-external-research.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/07-external-research.md)

## Proposed change
- Write the design: Core `IGridSemanticsProvider` built from the layout engine; adapters per host; re-publishing on scroll/sort/filter; keyboard focus parity; staged delivery (Windows first, then iOS, Android, WASM)

## Acceptance criteria
- [ ] Design reviewed and split into implementation issues for the 1.x milestone

## Dependencies
Blocked by: `L1`, `L2`
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### M2 — Spec: right-to-left layout mirroring (FlowDirection) for the grid, headers, frozen panes and editors

Labels: `area:i18n`, `area:rendering`, `area:maui`, `area:uno`, `kind:spec`, `priority:low`, `model:opus` · Milestone: `1.x – Fast follow-ons`

**Epic M — Localisation** · plan key `M2` · priority **low** · suggested model **opus**

## Problem
Layout is left-anchored everywhere; Arabic/Hebrew apps get an LTR grid with no option; an RTL ancestor on Uno mirrors the canvas (text backwards).

## Evidence
Findings: CORE-32 (RTL), MAUI-19
Reports: [01-core-data-layout-render.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/01-core-data-layout-render.md), [03-skiasharp-maui.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/03-skiasharp-maui.md)

## Proposed change
- Design: `FlowDirection` on the control → mirrored X in layout and hit testing, frozen 'Left' semantics swapped, popups mirrored, canvas forced LTR with logical mirroring; documented as unsupported until then

## Acceptance criteria
- [ ] Design reviewed; README states the limitation for 1.0

## Dependencies
Blocked by: none
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### X1 — Spec: row details / master-detail panel

Labels: `area:core`, `area:rendering`, `area:api`, `kind:spec`, `priority:medium`, `model:opus` · Milestone: `1.x – Fast follow-ons`

**Epic X — 1.x fast follow-on specs** · plan key `X1` · priority **medium** · suggested model **opus**

## Problem
No expandable per-row detail area; the most common missing feature after reorder and validation.

## Evidence
Findings: research §1.3 (5/7 competitors)
Reports: [07-external-research.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/07-external-research.md)

## Proposed change
- Design: `RowDetailsTemplate` (canvas renderer or hosted native view), variable row height dependency, expand state by item, keyboard and accessibility semantics

## Acceptance criteria
- [ ] Design reviewed; implementation issues filed

## Dependencies
Blocked by: `X4`
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### X2 — Spec: incremental / load-more data loading (ISupportIncrementalLoading, NearEnd hook, loading row)

Labels: `area:core`, `area:api`, `kind:spec`, `priority:medium`, `model:opus` · Milestone: `1.x – Fast follow-ons`

**Epic X — 1.x fast follow-on specs** · plan key `X2` · priority **medium** · suggested model **opus**

## Problem
Large remote datasets cannot be loaded on demand; there is no near-end hook or loading indicator.

## Evidence
Findings: CORE-33, research §1.3 (all MAUI vendors)
Reports: [01-core-data-layout-render.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/01-core-data-layout-render.md), [07-external-research.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/07-external-research.md)

## Proposed change
- Design on top of `Scrolled` and `ViewChanged`: `LoadMoreCommand`/`ISupportIncrementalLoading`, busy row, cancellation

## Acceptance criteria
- [ ] Design reviewed

## Dependencies
Blocked by: `G2`
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### X3 — Spec: export to CSV and Excel/PDF

Labels: `area:core`, `area:api`, `kind:spec`, `priority:medium`, `model:sonnet` · Milestone: `1.x – Fast follow-ons`

**Epic X — 1.x fast follow-on specs** · plan key `X3` · priority **medium** · suggested model **sonnet**

## Problem
No export; CSV is cheap and dependency-free, Excel/PDF need a third-party library decision.

## Evidence
Findings: research §1.3 (4/7)
Reports: [07-external-research.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/07-external-research.md)

## Proposed change
- Design: `IGridExporter` with CSV in Core; optional packages for Excel/PDF

## Acceptance criteria
- [ ] Design reviewed

## Dependencies
Blocked by: `D6`
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### X4 — Spec: variable row height and text wrapping

Labels: `area:core`, `area:layout`, `area:rendering`, `kind:spec`, `priority:medium`, `model:opus` · Milestone: `1.x – Fast follow-ons`

**Epic X — 1.x fast follow-on specs** · plan key `X4` · priority **medium** · suggested model **opus**

## Problem
Row height is uniform and baked into the virtual-scroll math; wrapping exists only at the drawing-context level after I2.

## Evidence
Findings: CORE-30 (RowStyle.Height), SKIA-05, research §1.3
Reports: [01-core-data-layout-render.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/01-core-data-layout-render.md), [03-skiasharp-maui.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/03-skiasharp-maui.md), [07-external-research.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/07-external-research.md)

## Proposed change
- Design: measured/cached row heights, prefix-sum offsets, auto height mode, `RowHeightResolver`, interaction with frozen rows and summaries

## Acceptance criteria
- [ ] Design reviewed

## Dependencies
Blocked by: `I2`
Blocks: `X1`

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### X5 — Spec: column chooser UI and multi-select checkbox column

Labels: `area:components`, `area:api`, `kind:spec`, `priority:low`, `model:sonnet` · Milestone: `1.x – Fast follow-ons`

**Epic X — 1.x fast follow-on specs** · plan key `X5` · priority **low** · suggested model **sonnet**

## Problem
`IsVisible` exists but there is no chooser; no checkbox selection column.

## Evidence
Findings: research §1.3
Reports: [07-external-research.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/07-external-research.md)

## Proposed change
- Design: drawn chooser popup from the header context menu; `SelectionColumn` bound to the selection model

## Acceptance criteria
- [ ] Design reviewed

## Dependencies
Blocked by: `Q4`, `E2`
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### X6 — Spec: paging and DataTable / dictionary / dynamic row sources

Labels: `area:core`, `area:api`, `kind:spec`, `priority:low`, `model:sonnet` · Milestone: `1.x – Fast follow-ons`

**Epic X — 1.x fast follow-on specs** · plan key `X6` · priority **low** · suggested model **sonnet**

## Problem
Only POCO property access is supported; no paging model.

## Evidence
Findings: CORE-33, research §1.3
Reports: [01-core-data-layout-render.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/01-core-data-layout-render.md), [07-external-research.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/07-external-research.md)

## Proposed change
- Design: `IPropertyAccessorProvider` implementations for `DataRow`, `IDictionary<string,object>`, `ExpandoObject`; a `PagedDataGridSource` or pager control

## Acceptance criteria
- [ ] Design reviewed

## Dependencies
Blocked by: `K1`
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### X7 — Spec: cell selection unit (SelectionUnit.Cell) with ranges and clipboard

Labels: `area:core`, `area:input`, `kind:spec`, `priority:medium`, `model:opus` · Milestone: `1.x – Fast follow-ons`

**Epic X — 1.x fast follow-on specs** · plan key `X7` · priority **medium** · suggested model **opus**

## Problem
Cell-unit selection was advertised but never implemented and is removed from the 1.0 API; it remains a wanted feature.

## Evidence
Findings: EDIT-06, CORE-26
Reports: [02-core-components-editing-input.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/02-core-components-editing-input.md), [01-core-data-layout-render.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/01-core-data-layout-render.md)

## Proposed change
- Design: cell/range selection model, keyboard extension, rendering, TSV copy of ranges

## Acceptance criteria
- [ ] Design reviewed

## Dependencies
Blocked by: `E2`, `D6`
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### X8 — Spec: item-based frozen rows (pinning), stacked headers and cell merging

Labels: `area:core`, `area:rendering`, `kind:spec`, `priority:low`, `model:opus` · Milestone: `1.x – Fast follow-ons`

**Epic X — 1.x fast follow-on specs** · plan key `X8` · priority **low** · suggested model **opus**

## Problem
Frozen rows are 'first N rows of the view'; no pinned items, header groups or merged cells.

## Evidence
Findings: CORE question 14, research §1.3
Reports: [01-core-data-layout-render.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/01-core-data-layout-render.md), [07-external-research.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/07-external-research.md)

## Proposed change
- Design for each with layout and hit-testing implications

## Acceptance criteria
- [ ] Design reviewed

## Dependencies
Blocked by: none
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### X9 — Spec: API reference site (DocFX) and cookbook for custom columns, renderers and editors

Labels: `area:docs`, `kind:spec`, `priority:medium`, `model:sonnet` · Milestone: `1.x – Fast follow-ons`

**Epic X — 1.x fast follow-on specs** · plan key `X9` · priority **medium** · suggested model **sonnet**

## Problem
Generated XML docs are not browsable; the custom column/renderer/editor recipes live only in `.github/prompts`.

## Evidence
Findings: DOC-07 §4
Reports: [06-docs.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/06-docs.md)

## Proposed change
- DocFX site from the XML docs published via GitHub Pages; cookbook pages

## Acceptance criteria
- [ ] Site published

## Dependencies
Blocked by: `F6`
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

### X10 — Spec: HarfBuzz shaping for complex scripts (Arabic, Indic) on top of per-glyph fallback

Labels: `area:skiasharp`, `area:rendering`, `kind:spec`, `priority:low`, `model:opus` · Milestone: `1.x – Fast follow-ons`

**Epic X — 1.x fast follow-on specs** · plan key `X10` · priority **low** · suggested model **opus**

## Problem
Run segmentation gives correct glyphs but not correct shaping for complex scripts.

## Evidence
Findings: SKIA-02 follow-up
Reports: [03-skiasharp-maui.md](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/03-skiasharp-maui.md)

## Proposed change
- Evaluate `SkiaSharp.HarfBuzz` `SKShaper` as an optional package

## Acceptance criteria
- [ ] Decision recorded

## Dependencies
Blocked by: `J2`
Blocks: none

_Source: KumikoUI 1.0 release-readiness evaluation (2026-10-22), [summary](https://github.com/TheEightBot/KumikoUI/blob/main/docs/release-readiness/README.md)._

---

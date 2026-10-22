# KumikoUI 1.0 Release-Readiness Evaluation

**Date:** 2026-10-22 · **Reviewed tree:** `feature/uno-platform-port` @ `4938125` (open PR #9) on top of `main` @ `59825e2` · **Published state:** KumikoUI.Core / .SkiaSharp / .Maui 0.1.10 on nuget.org; KumikoUI.Uno never published.

This folder is the complete evaluation. This page is the summary and the decision list; the numbered files are the detailed per-area reports, each finding cited to `file:line`.

| File | Area |
|---|---|
| [01-core-data-layout-render.md](01-core-data-layout-render.md) | `DataGridSource`, models, `GridLayoutEngine`, `DataGridRenderer`, primitives |
| [02-core-components-editing-input.md](02-core-components-editing-input.md) | Drawn editors/popups, `EditSession`, `CellEditorFactory`, `GridInputController`, hit testing, inertia |
| [03-skiasharp-maui.md](03-skiasharp-maui.md) | `SkiaDrawingContext`, `SkiaFontRegistrar`, MAUI `DataGridView`, MAUI sample |
| [04-uno.md](04-uno.md) | Uno `DataGridView`, input mapping, fonts, Uno sample, port plan |
| [05-tests-ci-packaging.md](05-tests-ci-packaging.md) | Test coverage, CI/CD, versioning, packaging, repository hygiene, licences |
| [06-docs.md](06-docs.md) | README claim-by-claim audit, docs accuracy, nuget.org rendering, XML docs |
| [07-external-research.md](07-external-research.md) | Competitor parity matrix, .NET/MAUI/Uno/SkiaSharp lifecycle, packaging and accessibility guidance (primary sources, cited) |

---

## 1. Verdict

**KumikoUI is not ready for a 1.0 release.** The architecture is sound and the feature breadth is competitive, but the engine assumes happy-path data, the edit pipeline can write values to the wrong item, the public API still contains placeholders that cannot be changed after 1.0, 60% of Core has no tests, the documentation does not match the code, and the core packages target a runtime that leaves support on 2026-11-10.

Findings by severity (214 across the seven reports; UNO-01 and PKG-02 describe the same root cause, so there are 11 distinct blockers):

| Severity | Count | Meaning |
|---|---|---|
| Blocker | 12 | Ships broken for a documented or default scenario, or makes the release itself unsafe |
| High | 50 | Wrong results, crashes, leaks, or a public promise that is not kept |
| Medium | 98 | Correctness, performance or API issues worth fixing before the API freezes |
| Low | 54 | Polish |

The realistic path is a **stabilisation line first** (0.2.x: fix blockers and highs, freeze the API, retarget .NET 10, fix docs), then **1.0.0-preview** builds for device verification, then **1.0.0**. A roadmap built on that path lives in [`../ROADMAP.md`](../ROADMAP.md) once the decisions in §7 are settled.

## 2. What is already good

- **The layering holds.** Core knows nothing about SkiaSharp or a UI framework; the Uno host was added with one additive Core change (`DataGridStyle.PinBottomSummaryRows`). Adding a third host would be the same thin exercise.
- **Virtualisation is real.** Only visible rows and columns are laid out and drawn; the 100K-row sample scrolls.
- **Feature breadth is competitive.** Sorting, Excel-style filtering, grouping with summaries, frozen panes, row drag-and-drop, action buttons, themes, custom fonts, and (undocumented) column header reordering and `IDataErrorInfo` validation already exist. Against seven mainstream grids the only common features missing outright are row details, localisable strings, export, load-more, clipboard copy and variable row height (report 07 §1.3).
- **Release plumbing exists.** Deterministic builds, SourceLink, symbol packages, tag-driven publish, Conventional Commits, 353 passing model/layout tests, CI on Linux and Windows.

## 3. Release blockers

Each must be closed before any `1.0.0` tag. "Reproduced" means I ran it against the library in this session (see §8).

| # | ID | Blocker | Report |
|---|---|---|---|
| 1 | CORE-01 | Tapping the header of a Template/action column, or any column with mixed or non-comparable values, throws `InvalidOperationException`. `AllowSorting` defaults to true. **Reproduced.** | 01 |
| 2 | EDIT-01 | An open editor survives sort, filter, group and collection changes and commits by the stale visible-row index: the value lands on another item or `GetItem` throws out of the input handler. | 02 |
| 3 | EDIT-02 | ComboBox, Picker, DatePicker and custom editors that cannot represent the current value commit a different value (`null`, item 0, today, midnight) on any outside tap. | 02 |
| 4 | COMP-01 | The filter popup's value list cannot be scrolled (`_isPanning` is never set, wheel is never routed to popups); only about ten distinct values are reachable and dragging toggles checkboxes. | 02 |
| 5 | MAUI-01 | `SupportedOSPlatformVersion` 15.0 for Mac Catalyst is rejected by the .NET 10 / Xcode 27 toolchain (floor 17.0). The library still builds; the sample app and any consumer following the README's "Catalyst 15+" fail. **Reproduced.** | 03 |
| 6 | PKG-02, UNO-01 | Core, SkiaSharp, every Uno head and both test projects target `net9.0`, whose support ends 2026-11-10. The MAUI host and both samples are already `net10.0`; Uno.Sdk is pinned at 6.5.31 (current 6.7.30) inline in three csproj files. | 05, 04, 07 |
| 7 | PKG-01 | No version source of truth: every csproj says `1.0.0` while tags are `v0.1.x`; a local `dotnet pack` produces `KumikoUI.Core.1.0.0.nupkg`. No changelog, no release notes. | 05 |
| 8 | CI-01 | No release gate: `main` unprotected, the `nuget-publish` environment has no reviewers or branch policy, `workflow_dispatch` publishes any version from any ref, the publish test job runs only the Core suite. Three of ten tag publishes already failed and burned versions. | 05 |
| 9 | TEST-01 | 72 of 120 public Core types have no tests, including `DataGridRenderer`, `GridInputController`, every `Drawn*` editor, `EditSession`, hit testing and `CellEditorFactory`. No rendering, host or benchmark tests. | 05, 02 |
| 10 | DOC-01 | The README packed into all four packages opens with raw HTML and relative image paths that nuget.org does not render (the live 0.1.10 page shows the tags as text), and it is MAUI-only even inside Core, SkiaSharp and Uno. | 06, 05 |
| 11 | DOC-02 | Nine README snippets do not compile or parse against the current API (`ColumnType="ProgressBar"`, `Width="*"`, `EditTextSelectionMode="End"`, `Min/Max`, `SummaryDescription`, float `GridColor`, six style members, `CellStyle.Bold`, `await` in a sync method). | 06 |

Near-blockers that a maintainer may reasonably promote: UNO-02 (the Uno keyboard proxy marks every key handled, so Tab, Shift+Tab and Escape can never leave the grid: a WCAG keyboard trap), UNO-03/04 (the Uno grid is invisible to screen readers while its hidden proxy text box is announced and hit-testable at the grid centre), UNO-05 (the proxy reset after every text change destroys CJK IME composition by construction; no Uno head has been exercised interactively), SKIA-01 (unknown font family crashes at end of first frame on Android/Windows), CORE-09 (PR #9's pinned footer is not reflected in hit testing or keyboard ensure-visible, default on), CORE-05 / MAUI-12 (no threading contract), A11Y-01 (no accessibility surface at all), COMP-04 (action-button `ICommand` runs without `CanExecute` and a synchronous delete of the last row crashes).

## 4. Scorecard by area

| Area | Status | B / H / M / L | What drives the status | Report |
|---|---|---|---|---|
| Core data, layout, render | Red | 1 / 10 / 27 / 11 | Sorting crashes on real-world data; summaries drop aggregates; row styles mutate shared state; selection never reconciled; `Auto` sizing is a no-op; Star columns snap back; per-cell per-frame allocations; no threading contract | 01 |
| Core editing, components, input | Red | 3 / 13 / 24 / 7 | Stale-row commits; silent overwrites; filter popup unusable past ten values; keyboard-only ComboBox cannot change a value; clipboard dead; Tab is a keyboard trap; drag hijacks touch scroll; no accessibility | 02 |
| SkiaSharp backend + MAUI host | Red | 1 / 7 / 18 / 12 | Catalyst floor; unknown-font NRE; no glyph fallback; `IsReadOnly`/`AllowSorting`/`AllowFiltering` inert; no keyboard nav on Windows/Android; no modifiers; no MVVM selection or scroll API; theme not bindable; every Skia object rebuilt per frame | 03 |
| Uno host | Red | 1 / 6 / 10 / 8 | Structurally sound and ahead of MAUI on modifiers and fonts, but: net9.0 heads; keyboard trap; invisible to assistive tech; IME broken by design; software `SKXamlCanvas` plus 16 ms timer repaints although `SKCanvasElement` is already referenced; ~1,200 AndroidX files in the nupkg; README has zero Uno content; no head tested interactively; `UseKumikoUI` is a no-op that costs a dependency | 04 |
| Tests, CI, packaging, hygiene | Red | 4 / 9 / 12 / 8 | No release gate; net9.0; version drift; workload cache never hits; no Dependabot/CodeQL; no CONTRIBUTING/SECURITY/CHANGELOG; fonts without licence notices; PR #1 stale | 05 |
| Documentation | Red | 2 / 5 / 7 / 3 | nuget.org rendering; nine broken snippets; MAUI-only everywhere; unmeasured "60 fps"; stale ARCHITECTURE/RENDERING/instructions; 96 undocumented public members | 06 |
| Competitive position | Amber | — | Feature set is at parity for a mobile/desktop grid except row details, localisation, export, load-more, clipboard copy, variable row height, accessibility | 07 |

## 5. Cross-cutting themes

These are the root causes behind most findings. Fixing them is cheaper than fixing findings one by one.

1. **Index-based identity.** Selection, the edit session, frozen rows and `RowTapped` all speak in visible-row indices that change meaning on every sort, filter, group or collection change. Nothing remaps them (CORE-06, EDIT-01, CORE-26, MAUI-04). Tracking the *item* (plus a structured `ViewChanged` event from `DataGridSource`) resolves a whole class of bugs.
2. **Happy-path data assumptions.** Non-comparable values, mixed types, heterogeneous item types, null items, enums, `decimal`, `DateOnly`/`TimeOnly`/`DateTimeOffset`, culture separators and nested-object changes all break sorting, summaries, editing or rendering (CORE-01, 13, 14, 15, 16, 40, EDIT-03, COMP-11, 17).
3. **Edit pipeline integrity.** Commit results are ignored, failed conversions are reported as success, validation runs on the old value, and value-less editors (action buttons) go through `CommitEdit` (EDIT-03, 04, 05, COMP-04, EDIT-07).
4. **No threading contract.** Collection and property changes rebuild the view on the raising thread while the UI thread renders; hosts marshal only the repaint (CORE-05, MAUI-12).
5. **The public API is not frozen-ready.** `RowTappedEventArgs2`; publicly mutable runtime state on `DataGridColumn` and `SelectionModel`; argument-less `DataChanged`; dead members (`RowStyle.Height`, `CellStyle.Border*`, `Column.Id`, `SelectionUnit.Cell`, `DrawnCheckBox`, `DrawnProgressBar`, `FocusManager.FocusNext`); inert MAUI bindables; four naming prefixes; `UseSkiaKumikoUI` vs `UseKumikoUI`; `DataGridView : Grid` (CORE-27..30, 44, API-01, MAUI-02, 15, 24, DOC-13). Every one of these is breaking after 1.0.
6. **Host MVVM surface.** No `SelectedItem`/`SelectedItems`/`SelectionChanged`, no `ScrollTo*`, no bindable theme or dark-mode reaction, no commands, and grouping/sorting/filtering reachable only through `DataSource` (MAUI-04, 06, 07, 08, 16).
7. **Keyboard and text input.** Windows and Android get characters, Backspace and Enter only; pointer modifiers are never populated so Extended selection is Single; clipboard shortcuts never reach editors; IME composition is reset mid-composition; editors ignore registered fonts; the Uno proxy swallows every key and resets mid-composition (MAUI-03, 05, 27, COMP-03, 07, 15, UNO-02, 05).
8. **Fonts and glyphs.** Unknown family → null typeface → NRE; no per-glyph fallback so mixed scripts and emoji render tofu; registered fonts ignore bold/italic (SKIA-01, 02, 04).
9. **Performance is claimed, not measured.** No benchmark, no frame-time instrumentation; the render loop allocates three or more objects per visible cell per frame, rebuilds `PaintCache` and every Skia object every frame, re-measures all text every frame, and any INPC change on a summarised property forces an O(n log n) rebuild (CORE-10, 19..24, 34, SKIA-03, TEST-03, DOC-05).
10. **Tests cover the models, not the control.** Renderer, input controller, editors, hit testing and hosts have zero coverage; some existing tests are tautological (TEST-01, 02, 05, CORE-35..37).
11. **Documentation drift.** The README, ARCHITECTURE.md, RENDERING.md, Copilot instructions and prompts describe members and pipelines that do not exist, omit Uno entirely, and overstate claims ("zero native controls", canvas scrollbars, 60 fps) (DOC-02..12, CORE-11, 38).
12. **Toolchain and platform lifecycle.** net9.0 EOL in 19 days; Catalyst floor 17; SkiaSharp 3.119.2 is a major behind (4.153.1, compile-breaking text API); Uno.Sdk 6.5.31 is eight months old; MAUI and Uno workloads are unpinned (PKG-02, 05, MAUI-01, 22, CI-03, UNO-01, 08).
13. **Release engineering and governance.** No gate, no versioning tool, no changelog, no Dependabot/CodeQL, over-broad token permissions, no CONTRIBUTING/SECURITY/PR template, default labels, no milestones, stale PR #1, bundled fonts without Apache-2.0/OFL notices (CI-01..08, PKG-01..08, REPO-01..08).
14. **Accessibility and localisation are absent.** One opaque canvas node per platform; the hidden keyboard proxy *is* exposed to screen readers; the Uno host has no automation peer at all; all built-in strings are hard-coded English; no RTL (A11Y-01, MAUI-09, UNO-03, 04, CORE-32, COMP-09, MAUI-19).

## 6. Competitive position (report 07)

Against Syncfusion (MAUI and WinUI), Telerik, DevExpress, Avalonia, the archived Windows Community Toolkit grid and WinUI.TableView, KumikoUI already covers the core table-stakes. Ranked by how many competitors document a feature KumikoUI lacks or does not document:

1. Column header drag-reorder (6/7) — **already implemented, undocumented** (`DataGridColumn.AllowReorder`, `ColumnReordered`).
2. Validation error UI (6/7) — plumbing exists (`IDataErrorInfo`, `INotifyDataErrorInfo`), no visible contract or docs.
3. Row details / master-detail (5/7).
4. Localisation of built-in strings (5/7).
5. Export (4/7); load-more (3/7, but all three MAUI vendors); clipboard copy (3/7); variable row height (3/7).

Items 1 and 2 are documentation and polish; items 3 to 5 are roadmap features, not 1.0 blockers unless the maintainer decides otherwise (§7, Q13).

Lifecycle facts that bound the schedule: .NET 9 support ends 2026-11-10; .NET 10 LTS runs to 2028-11-14; MAUI 9 ended 2026-05-12; Uno 6.4+ defaults to .NET 10; SkiaSharp 4.148.0 (2026-06-22) is the first stable 4.x with compile-breaking `SKPaint` text changes; Uno still defaults to SkiaSharp 3.x.

## 7. Decisions required before the roadmap is final

Consolidated from 60 maintainer questions across the seven reports. Each is put to the maintainer in the review conversation with a recommended answer; the roadmap is written from the answers.

**Scope and platforms**
- D1. Which packages ship as 1.0.0: all four, or Core/SkiaSharp/MAUI at 1.0 with KumikoUI.Uno as 1.0-preview until every head is device-verified?
- D2. Retarget Core, SkiaSharp, Uno and tests to `net10.0` and drop `net9.0`?
- D3. Move to SkiaSharp 4.x before 1.0 (native ABI and text-API change for every consumer)?
- D4. Platform floors: Mac Catalyst 17.0, iOS floor, Android 21, Windows 17763; pin the MAUI/Uno workload sets; upgrade Uno.Sdk to 6.7.x?
- D5. Keyboard matrix for 1.0: full navigation on Windows and Android, or document a per-platform matrix?
- D6. IME composition (CJK text entry) in 1.0?
- D7. Accessibility: 1.0 requirement, or documented limitation plus quick wins and a 1.x virtual-tree plan?
- D8. Localisable built-in strings and RTL in 1.0?
- D9. Which gap features, if any, join 1.0: clipboard copy, `ScrollTo*`, MVVM selection, documented column reorder and validation (recommended) versus row details, load-more, export, variable row height, column chooser, context menu (recommended 1.x)?

**API freeze**
- D10. Accept the breaking clean-up now: rename `RowTappedEventArgs2`; make column sort/filter/width runtime state settable only through `DataGridSource`; read-only selection sets with `Select`/`Deselect`; structured `ViewChanged` event; remove or implement dead members; implement or delete the inert MAUI bindables; one hosting method name; `DataGridView` base class?
- D11. Naming policy (`DataGrid*` / `Grid*` / `Drawn*` / `Kumiko*`) and product name.
- D12. Add PublicAPI analyzers now and package validation from 1.0.0.
- D13. Trimming/AOT: adopt the delegate accessor API and `IsTrimmable` in 1.0, or close PR #1 and defer?

**Behavioural contracts**
- D14. Threading: UI-thread-only with a debug assertion, or marshal rebuilds in Core?
- D15. Identity: selection follows items across sort/filter; a mid-edit view change commits to the item if still present, otherwise cancels?
- D16. Comparison semantics: ordinal versus culture, case handling, group ordering by raw key.
- D17. Several aggregates per column in one summary row; group summaries at every level?
- D18. `ColumnSizeMode.Auto`: sampled auto-measure on first render and data change?
- D19. `PinBottomSummaryRows` default after the hit-testing fix.
- D20. Full-row drag gesture: long-press on touch, immediate on mouse?
- D21. Popups: an outside tap that dismisses a popup is swallowed?
- D22. Editable property types that must round-trip: enum, decimal, DateOnly, TimeOnly, DateTimeOffset, Guid, nullable value types; empty numeric text semantics.
- D23. Frozen rows: first N rows of the view (current) or pinned items?

**Process**
- D24. Branching: `main` plus tags plus rulesets; retire `develop`?
- D25. Versioning: MinVer from tags, four packages in lockstep, `0.2.0` → `1.0.0-preview.N` → `1.0.0`?
- D26. Publishing: NuGet Trusted Publishing, required reviewer on `nuget-publish`.
- D27. Coverage and performance budgets to commit to.
- D28. Issue granularity, labels, milestones, GitHub Discussions, security contact.
- D29. Repository size: move the 15 MB GIF, share the 9 MB font.
- D30. Docs: rich GitHub README plus per-package NuGet READMEs; soften unmeasured claims; DocFX site later.

## 8. Verification log

What was executed in this session, as opposed to read:

| Check | Result |
|---|---|
| Scratch console app against `KumikoUI.Core` (`feature/uno-platform-port`) | CORE-01: `ToggleSort` on a Template column, on mixed `int`/`double` values, and on a `List<Animal>` of `Dog`/`Cat` all throw `InvalidOperationException`. CORE-02: Sum + Average on `Salary` in one `TableSummaryRow` yields one value (`Avg: 15`). CORE-04: `SetFilter(age > 30)` then `Add(age 20)` → row count 3 until `Refresh()` → 2. CORE-06: `GetItem(2)` after a filter leaving one row throws `ArgumentOutOfRangeException`. CORE-15: a `null` item throws `NullReferenceException` on `GetCellDisplayText`. |
| `dotnet build src/KumikoUI.Maui -f net10.0-maccatalyst -c Release --no-incremental` | Succeeds (library). |
| `dotnet build samples/SampleApp.Maui -f net10.0-maccatalyst` | Fails: `The SupportedOSPlatformVersion value '15.0' in the project file is lower than the minimum value '17.0'` (MacCatalyst SDK 27.0.10742). |
| `dotnet build src/KumikoUI.Core -c Release` | 96 warnings, all CS1591. `src/KumikoUI.SkiaSharp`: 0 warnings. |
| `dotnet test tests/KumikoUI.Core.Tests -c Release` | Aborts without a .NET 9 runtime; with `DOTNET_ROLL_FORWARD=Major`: 353 passed, 0 failed. |
| `dotnet format --verify-no-changes` on Core | 90 whitespace violations. |
| `dotnet list package --outdated` | SkiaSharp 3.119.2 → 4.153.1; test SDK packages behind; no vulnerable packages. |
| Greps confirming report claims | `_isPanning` in `DrawnFilterPopup.cs` is assigned only `false`; `UpdateEditorBounds` has no callers; no subscriber to `CopyRequested`/`PasteRequested`; no `CanExecute` anywhere in Core; `PopupManager` routes only Pressed/Moved/Released. |
| nuget.org | Core/SkiaSharp/Maui 0.1.3–0.1.10 published (0.1.1, 0.1.2, 0.1.5 missing); KumikoUI.Uno absent; MAUI package depends on `Microsoft.Maui.Controls >= 10.0.20`. |
| GitHub | `main` not protected; no rulesets; `nuget-publish` environment without reviewers; no Dependabot, CodeQL, Discussions, topics, milestones; default labels only; last CI run 2026-06-30 green. |

Not verified in this session: anything requiring a device or emulator (IME behaviour, Android/Windows font NRE, hover-move panning, Uno WASM/iOS/Android heads, frame times). Each report ends with its own "needs runtime check" list.

## 9. Method

Seven reviews ran in parallel, each reading its slice of the source in full and cross-checking the README, `docs/`, `.github/instructions` and the sample apps; one researched competitors, lifecycle dates and packaging guidance against primary sources only. Every finding carries a `file:line` reference. Findings that a reviewer could not confirm from code are listed separately as unverified rather than mixed into the tables. The repository was not modified except for adding this folder.

## Appendix A — Findings index

Severity and title for every finding, grouped by report. Details, evidence and suggested fixes are in the linked reports. Note: report 02 uses `TEST-01` for its own test-gap item; report 05's `TEST-01` is the repository-wide one.

### Report 01 — Core data, layout, render ([01-core-data-layout-render.md](01-core-data-layout-render.md))

| ID | Severity | Category | Title |
|---|---|---|---|
| CORE-01 | Blocker | bug | Sorting throws for non-IComparable or mixed-type values; Template columns sort the row object itself |
| CORE-02 | High | bug | Two aggregates on the same property in one summary row overwrite each other (README's own example) |
| CORE-03 | High | bug | `RowStyleResolver` text/font overrides permanently mutate the column's shared `CellStyle` (and resolver-returned instances) |
| CORE-04 | High | bug | Incremental INCC `Add` ignores the global `SetFilter` predicate |
| CORE-05 | High | bug | No thread-affinity handling: collection/property changes on a background thread rebuild the view while the UI thread renders from it |
| CORE-06 | High | bug | Selection/current cell are never reconciled after sort/filter/group/remove; stale index crashes on Enter/F2 |
| CORE-07 | High | missing-feature | `ColumnSizeMode.Auto` never measures content; it behaves exactly like `Fixed` |
| CORE-08 | High | bug | Star-sized columns cannot be resized or auto-fitted: `ComputeColumnWidths` overwrites `Width` every frame |
| CORE-09 | High | bug | PR #9: `PinBottomSummaryRows` (default `true`) moves the bottom summary into a fixed footer, but hit-testing, keyboard ensure-visible and the drag-handle column still use the old geometry |
| CORE-10 | High | perf | Per-cell, per-frame allocations contradict the "no per-frame heap allocations" rule |
| CORE-11 | High | docs | Copilot instructions and prompts document an API that does not exist |
| CORE-12 | Medium | bug | Sort is unstable: ties return 0 into `List<T>.Sort` (introsort) |
| CORE-13 | Medium | bug | Inconsistent comparison semantics; groups are ordered by display text so numeric/date groups sort lexicographically |
| CORE-14 | Medium | bug | Sort and summaries compile the accessor against `_sourceItems[0].GetType()` and cast every item to it |
| CORE-15 | Medium | bug | Null items in the source throw `NullReferenceException` during render |
| CORE-16 | Medium | bug | `SetCellValue` silently drops failed writes and does not support enums/`TypeConverter`s |
| CORE-17 | Medium | bug | Notification events are raised inconsistently |
| CORE-18 | Medium | bug | INPC rebuild heuristic misses empty property names and nested paths; nested objects are not observed |
| CORE-19 | Medium | perf | INPC tracking allocates a `WeakReference` (GC handle) per item and untracks in O(n) |
| CORE-20 | Medium | perf | Every rebuild recomputes all summaries from boxed value lists, and any INPC change on a summarized/sorted/grouped property forces a full O(n log n) rebuild with no batching |
| CORE-21 | Medium | perf | Filtering formats a display string for every row × active filter even when only condition filters are set; unique-value list formats all rows |
| CORE-22 | Medium | perf | Grouping allocates a `FlatViewRow` class per row plus per-group lists/strings on every rebuild |
| CORE-23 | Medium | perf | LINQ and non-cell allocations in the render path |
| CORE-24 | Medium | perf | `CalculateAutoFitWidth` measures every row, not the visible ones |
| CORE-25 | Medium | bug | Summary values of horizontally scrolled-off columns are drawn without clipping and overlap the frozen-column pane |
| CORE-26 | Medium | bug | `SelectAll`/Shift-range select include group header and summary rows; `SelectionUnit.Cell` is not implemented |
| CORE-27 | Medium | api-design | `DataGridColumn` mixes immutable configuration with publicly settable runtime state and has no change notification; README's "multi-sort via code" path is only partially honoured |
| CORE-28 | Medium | api-design | `SelectionModel` exposes mutable `HashSet`s and settable `CurrentCell`/`IsEditing`; external mutation bypasses `SelectionChanged` |
| CORE-29 | Medium | api-design | `DataChanged` is an argument-less `Action`; no `ScrollChanged`/`RowCountChanged`/`ColumnsChanged`; `GroupCollapsed` naming and payload are misleading |
| CORE-30 | Medium | api-design | Dead or unimplemented public surface that will be breaking to remove later |
| CORE-31 | Medium | missing-feature | No `Dispose`/`Detach` on `DataGridSource`: after the view is torn down it stays subscribed to the app's collection and every INPC item |
| CORE-32 | Medium | missing-feature | No localization hook for built-in strings and no RTL support |
| CORE-33 | Medium | missing-feature | Data-source capabilities expected of a 1.0 grid are absent in Core |
| CORE-34 | Medium | perf | Text is measured and ellipsised per cell per frame with no measurement cache (Skia layer, cross-slice but driven by this renderer) |
| CORE-35 | Medium | test-gap | No tests for `DataGridRenderer`, `PaintCache`, built-in cell renderers, `ActionButtonLayout` or `GridColorTypeConverter` |
| CORE-36 | Medium | test-gap | `DataGridSource` behaviours without tests |
| CORE-37 | Medium | test-gap | Layout and selection edge cases without tests |
| CORE-38 | Medium | docs | README statements that are false against the code |
| CORE-39 | Low | bug | Star sizing does not redistribute space after Min/Max clamping |
| CORE-40 | Low | bug | Numeric aggregates go through `double` and exception-based conversion; Min/Max throw on mixed types |
| CORE-41 | Low | bug | Summary values are looked up case-sensitively at render time and require a matching column |
| CORE-42 | Low | bug | Group summaries are emitted only at the innermost level |
| CORE-43 | Low | docs | ~55 public members lack XML docs although `GenerateDocumentationFile=true` (CS1591 in the package) |
| CORE-44 | Low | api-design | General hygiene: nothing sealed, mutable reference types for value-like concepts, inclusive hit rectangles, behaviour callbacks on the theme object |
| CORE-45 | Low | api-design | `FilterCondition` carries three operator properties and an untyped `Value`; numeric/date parsing is current-culture; integer coverage incomplete |
| CORE-46 | Low | perf | Header always reserves 26 px for sort/filter glyphs; visible-row math overdraws rows hidden under the header |
| CORE-47 | Low | api-design | `GridColorTypeConverter` accepts only 7 named colours and rejects `#RGB`/`#RRGGBBAA`; bad hex throws `FormatException` from `Convert.ToByte` with an unhelpful message |
| CORE-48 | Low | api-design | `ReorderRow` semantics are surprising: operates on a private snapshot for non-`IList` sources, is visually a no-op while sorted, double-fires `DataChanged` |
| CORE-49 | Low | bug | Expand/collapse state is keyed by display text and wiped wholesale on group changes |

### Report 02 — Core components, editing, input ([02-core-components-editing-input.md](02-core-components-editing-input.md))

| ID | Severity | Category | Title |
|---|---|---|---|
| EDIT-01 | Blocker | bug | Edit session survives sort/filter/group/collection changes and commits by stale row index |
| EDIT-02 | Blocker | bug | Editors that cannot represent the current value commit a different value on outside-tap (silent data overwrite) |
| COMP-01 | Blocker | bug | Filter popup value list cannot be scrolled; press toggles items so drag attempts flip checkboxes |
| EDIT-03 | High | bug | Text→typed conversion is culture-dependent, lossy and failure is reported as success |
| EDIT-04 | High | bug | `IDataErrorInfo`/`INotifyDataErrorInfo` are evaluated on the *old* value; custom validator short-circuits model validation; Warning severity never shown; message never surfaced |
| EDIT-05 | High | bug | Failed commit is ignored by callers; `BeginEdit` leaks the previous editor |
| INPUT-01 | High | bug | Editor overlay is never repositioned when the grid scrolls (`UpdateEditorBounds` has no callers) |
| INPUT-02 | High | bug | Keyboard auto-scroll math wrong (`OffsetY` counted twice) and frozen rows/summaries/group panel ignored |
| INPUT-03 | High | bug | Keyboard-only ComboBox editing cannot change the value (Enter intercepted before the editor; Up/Down only move the highlight) |
| COMP-02 | High | bug | ComboBox dropdown has no pointer scrolling and no typeahead in the grid |
| INPUT-04 | High | bug | `AllowRowDragDrop` turns any vertical drag on a cell into a row drag (README says long-press); ghost offset ignores the group panel |
| COMP-03 | High | bug | Clipboard inside editors is dead; Cut destroys text without copying; select-all/copy/paste keys never produced by the MAUI host |
| COMP-04 | High | bug | Action buttons execute `ICommand` without `CanExecute`, then commit a `null` value; a command that removes the last row crashes the tap |
| INPUT-05 | High | bug | Tab is a keyboard trap and always starts editing (ignores `EditTriggers.None`); normal-mode Enter does nothing despite README |
| A11Y-01 | High | a11y | No accessibility surface for rows, cells, headers, editors or popups |
| EDIT-06 | Medium | missing-feature | `SelectionUnit.Cell` is unimplemented (`SelectedCells` never populated) |
| COMP-05 | Medium | bug | Outside-press dismisses all popups silently; controller/host are not told |
| COMP-06 | Medium | bug | Popups/overlays never clamp or flip to the viewport |
| INPUT-06 | Medium | bug | Boolean cells toggle on every tap regardless of `EditTriggers` (even `None`); double-tap toggles twice; Space does not toggle |
| INPUT-07 | Medium | missing-feature | No right-click / long-press context surface; secondary button behaves like primary |
| INPUT-08 | Medium | bug | No pointer-down state: a hover `Moved` event >10 px from the last press starts a pan/drag; text selection extends on hover |
| INPUT-09 | Medium | perf | `NavigateToNextEditableCell` can loop `rows × cols` times |
| INPUT-10 | Medium | bug | Column reorder drop index ignores frozen panes / drag handle and includes the dragged column |
| INPUT-11 | Medium | missing-feature | No edge autoscroll during row/column drags |
| INPUT-12 | Medium | bug | Group-panel chip hit testing approximates text width while the renderer measures it |
| COMP-07 | Medium | bug | `DrawnTextBox` is UTF-16 code-unit based: no surrogate/grapheme handling, no IME composition API, paste bypasses `NumericOnly`, newlines accepted, `OnLostFocus` never runs |
| COMP-08 | Medium | bug/perf | Caret off-screen on long text until a key press; O(n²) caret hit-testing |
| COMP-09 | Medium | bug | Filter popup is English-only and ignores the theme for the search box |
| COMP-10 | Medium | perf | Filter popup materializes every distinct value and re-copies on each keystroke |
| COMP-11 | Medium | bug | `DrawnDatePicker` is not culture-aware and has hard limits; `DateTimeOffset`/`DateOnly` round-trips fail |
| COMP-12 | Medium | bug | `DrawnScrollPicker`: any tap commits immediately; physics never run; strings only |
| COMP-13 | Medium | bug | `DrawnNumericUpDown`: no typed entry, auto-repeat never ticks, FP noise in display, inverted Home/End, unusable in narrow cells |
| COMP-14 | Medium | bug | Unhandled keys inside non-text editors fall through to grid shortcuts (typing restarts the edit, Space toggles row selection, Up/Down move selection away from the edited cell) |
| COMP-15 | Medium | bug | Editors ignore `style.CellFont` — hard-coded `"Default"` family/size, so registered (CJK/icon) fonts are not used inside editors |
| COMP-16 | Medium | docs/missing-feature | `DrawnCheckBox`, `DrawnProgressBar` are never created by the pipeline; README advertises a three-state checkbox editor and a `ProgressBar` column type |
| COMP-17 | Medium | bug | Numeric typing-trigger drops culture decimal separators; `NumericOnly` accepts malformed input |
| INPUT-13 | Medium | bug | F2/typing open the editor on a cell that may be scrolled out of view |
| INPUT-14 | Low | bug | `InertialScroller` edge cases: hold-then-release still flings; fling keeps the frame timer alive while clamped at the boundary; `TimestampMs=0` silently disables inertia; host hard-codes 16 ms |
| INPUT-15 | Low | bug | Header grip/hover details |
| INPUT-16 | Low | missing-feature | Wheel: no Shift+wheel → horizontal, no pixel-precise trackpad deltas, sign convention undocumented |
| INPUT-17 | Low | bug | Misc. navigation details: PageUp/Down page size ignores header height; taps on empty area never clear selection; LongPress sets `CurrentCell` without updating row selection; Escape in normal mode clears selection (non-standard) |
| COMP-18 | Low | bug | `PopupManager` internals: z-order ties after `Close`, `HitTest` order ≠ `GetTopmostPopup` order, anchors never used to reposition on scroll, `HandleKey` sends key-up to `OnKeyDown` when called directly |
| COMP-19 | Low | missing-feature | `DrawnTextBox` lacks double-click word select / triple-click select-all, undo/redo (`GridKey.Z` defined, unused), password masking, multi-line, RTL/bidi |
| EDIT-07 | Low | api-design | Event ordering/semantics: `CellValueChanged` fires before `EndEdit` (handlers see `IsEditing==true`), `CellEndEdit` after; `ToggleBooleanCell` bypasses `CellValidator`; template custom factories drop the typing-trigger character; `CellValidationResult.Valid` allocates per access |
| API-01 | Medium | api-design | Public surface not 1.0-clean |
| DOCS-01 | Medium | docs | README / instructions reference non-existent members or behaviour |
| TEST-01 | High | test-gap | No tests for the editing pipeline, input controller, hit testing, popups, focus or any drawn component |

### Report 03 — SkiaSharp backend and MAUI host ([03-skiasharp-maui.md](03-skiasharp-maui.md))

| ID | Severity | Category | Title |
|---|---|---|---|
| MAUI-01 | Blocker | packaging | Mac Catalyst min version 15.0 rejected by current .NET 10 SDK (min 17.0) |
| SKIA-01 | High | bug | Unknown / non‑registered font family → `null` typeface → NRE in `Dispose()` on Android/Windows |
| SKIA-02 | High | missing-feature | No per‑glyph font fallback (CJK/emoji/mixed scripts render tofu) |
| MAUI-02 | High | bug | `IsReadOnly`, `AllowSorting`, `AllowFiltering` BindableProperties are inert |
| MAUI-03 | High | missing-feature / docs | Windows and Android have no keyboard navigation (only characters/Backspace/Enter) |
| MAUI-04 | High | api-design | No MVVM selection surface (`SelectedItem`/`SelectedItems`/`SelectionChanged`/`SelectionUnit`) |
| MAUI-05 | High | bug | Pointer modifiers are never supplied → Shift/Ctrl selection and multi‑sort gestures are unreachable |
| MAUI-27 | High(needsdevicecheck) | bug | Android IME composition and surrogate pairs are broken by the sentinel/diff keyboard proxy |
| SKIA-03 | Medium | perf | All native Skia objects are created and disposed every frame; typeface resolution via font manager every frame |
| SKIA-04 | Medium | bug | Registered typefaces ignore bold/italic |
| SKIA-05 | Medium | missing-feature | No multi‑line / wrapped text; `\n` renders as a .notdef box |
| SKIA-09 | Medium | api-design / missing-feature | `DrawImage(object)` only accepts `SKImage`/`SKBitmap`; MAUI host offers no image resolution, caching, async loading or disposal |
| SKIA-11 | Medium | test-gap | Zero tests for KumikoUI.SkiaSharp |
| MAUI-06 | Medium | bug | `HeaderHeight`/`RowHeight` are lost when `Theme`/`GridStyle` is applied afterwards; XAML attribute order changes the result |
| MAUI-07 | Medium | missing-feature | `Theme`, `GridStyle`, `ScrollSettings` are not bindable; no system dark‑mode handling |
| MAUI-08 | Medium | missing-feature | No programmatic scrolling API or scroll events |
| MAUI-09 | Medium | a11y | Hidden keyboard proxy is exposed to assistive tech; grid content is invisible to screen readers |
| MAUI-10 | Medium | bug / perf | Auto‑fit leaks a `SkiaDrawingContext` and measures every row on the UI thread |
| MAUI-11 | Medium | bug | Mac Catalyst trackpad/wheel scrolling bypasses the input controller; no horizontal wheel on Windows |
| MAUI-12 | Medium | bug | "Thread‑safe invalidation" is only half the story: data‑source mutation happens on the raising thread |
| MAUI-13 | Medium | bug (leak) | Cleanup misses the long‑press timer, column/summary collection subscriptions and two native events |
| MAUI-14 | Medium | perf (needs device check) | CPU rasterisation via `SKCanvasView`, full‑surface repaint on every invalidate, timer‑driven repaint storms |
| MAUI-15 | Medium | api-design | Duplicate/unsafe public surface that will be breaking to fix after 1.0 |
| MAUI-16 | Medium | api-design | Many Core options/events have no MAUI exposure (see table in §c) and no `ICommand` equivalents |
| MAUI-23 | Medium | packaging | No trimming/AOT annotations; PR #1 (open since 2026‑04‑14) unresolved |
| MAUI-24 | Medium | api-design | `DataGridView` derives from `Grid` and repurposes its content property |
| SKIA-06 | Low | bug | Ellipsis truncation splits surrogate pairs/grapheme clusters; narrow cells never get an ellipsis |
| SKIA-07 | Low | api-design | `GridPaint.Style` is ignored by the Skia backend |
| SKIA-08 | Low | perf / quality | Text edging/hinting settings are ineffective or risky; `IsRegistered` is O(n) per typeface per frame |
| SKIA-10 | Low | bug | Registrar ownership edge cases; no `Unregister`; `Clear()` races in‑flight frames |
| MAUI-17 | Low | missing-feature / bug | Hover/cursor plumbing: `Entered`/`Exited` mapped to `Cancelled`; `CursorChanged` never consumed |
| MAUI-18 | Low | bug | Inertial scroll integrates a fixed 16 ms step regardless of real elapsed time |
| MAUI-19 | Low | i18n / bug | No RTL (`FlowDirection`) and no `IsEnabled` handling |
| MAUI-20 | Low | docs | README API drift in the reviewed sections |
| MAUI-21 | Low | docs / api-design | Hosting extension is a pass‑through with no diagnostic when omitted |
| MAUI-22 | Low | packaging | `$(MauiVersion)` unpinned; Windows TFM silently absent when packed off‑Windows; workload cache can mask toolchain drift |
| MAUI-25 | Low | docs / sample | Sample app rough edges |
| MAUI-26 | Low | bug | Touch‑to‑DIP scale can divide by zero before first paint |

### Report 04 — Uno host ([04-uno.md](04-uno.md))

| ID | Severity | Category | Title |
|---|---|---|---|
| UNO-01 | Blocker | packaging | Library heads target `net9.0-*`; .NET 9 support ends 2026-11-10 (19 days). Sample already on `net10.0`, Core/SkiaSharp still `net9.0`. |
| UNO-02 | High | a11y / bug | Keyboard trap: the proxy `KeyDown` path marks every mapped key `Handled` regardless of whether Core handled it; `HandleEditKey` discards `evt.Handled`. Core itself marks Tab handled unconditionally. |
| UNO-03 | High | bug / a11y | Hidden proxy `TextBox` is centred, hit-testable, exposed to assistive tech and focus side-effects. No `MinWidth/MinHeight` override (WinUI default style applies `TextControlThemeMinWidth/MinHeight` = 64×32 — verify), default `Stretch` alignment with explicit `Width/Height` centres it, `Opacity=0` disables neither hit-testing nor UIA, no `IsHitTestVisible=false`, no `AutomationProperties.AccessibilityView=Raw`; programmatic focus on every mouse press. |
| UNO-04 | High | a11y | Grid is invisible to screen readers: derives from `Grid` (a `Panel` — no automation peer is created), `AutomationProperties.SetName` is only applied in the DP *changed* callback so the default `"Data grid"` is never set, and there is no rows/columns hint. MAUI sets the description + `IsInAccessibleTree` in the ctor and updates a hint on every data change. |
| UNO-05 | High | bug | IME/composition and asynchronous `TextChanged`: `ResetProxy()` rewrites the proxy text after *every* `TextChanged`, so each IME composition update (Japanese/Chinese/Korean, dead keys on Skia) is consumed as final characters and the composition buffer is destroyed. On the Windows head `TextChanged` is raised asynchronously, so `_suppressProxyTextChanged` cannot cover the reset (the `newText == ProxySentinel` guard saves it) and two fast keystrokes before the reset lands can be forwarded twice. Android `IsTextPredictionEnabled` is a `Uno0001` no-op, so autocorrect can inject whole words; MAUI configures the native `EditText` directly. |
| UNO-06 | High | perf | Rendering goes through `SKXamlCanvas` (software bitmap + `WriteableBitmap` upload) on every Skia-rendered head (desktop, WASM, iOS/Android with `SkiaRenderer`), and the inertial scroller / cursor blink are `DispatcherTimer`s that invalidate the whole surface every 16 ms / 530 ms. Uno's own docs say `SKXamlCanvas` is not hardware accelerated on Skia targets and `SKCanvasElement` avoids the extra buffer copies; the package is already referenced. |
| UNO-07 | High | docs | Root `README.md` — embedded in every nupkg including `KumikoUI.Uno` — contains no Uno content: platform badge and intro omit WASM/Linux/Desktop, package table and badges omit `KumikoUI.Uno`, Getting Started is MAUI-only (`UseSkiaKumikoUI`, `clr-namespace`), keyboard-nav and font-registration sections describe MAUI mechanisms, `Theme="Dark"` XAML does not exist on Uno, architecture diagram/key-class table/sample/requirements omit Uno. `docs/ARCHITECTURE.md` likewise. |
| UNO-08 | Medium | packaging | `Uno.Sdk` pinned inline at `6.5.31` in three csproj files (library, tests, sample) because the plan believed a `global.json` would "pin the SDK for the whole repo". Current stable is 6.7.30 (7.0 in dev). Uno tooling (`uno-check`, VS/Rider extension, Hot Reload, `dotnet new` upgrades) reads the version from `global.json` `msbuild-sdks`. |
| UNO-09 | Medium | packaging | nupkg bloat and dependency leakage: ~1,200 transitive AndroidX `content/`+`contentFiles/` entries (`abc_*` drawables/layouts) are packed from the Android head and documented as "acceptable". The branch `project.assets.json` shows 58 AndroidX/Google/Kotlin packages on `net9.0-android35.0` (via `Uno.WinUI.Runtime.Skia.Android`) and **zero** NuGet `contentFiles`, so the entries come from the Android resource pipeline being swept into the library layout. `Uno.WinUI.Lottie`, `Uno.Resizetizer`, `Uno.Settings.DevServer`, `Uno.Sdk.Extras` and `Microsoft.Extensions.Hosting.Abstractions` are direct dependencies on every head — nuspec not inspected in CI. |
| UNO-10 | Medium | bug | Grid-level `IsReadOnly`, `AllowSorting`, `AllowFiltering` DPs are no-ops: the callbacks only repaint and nothing pushes them into Core, which consults only the per-column flags. The Grouping sample's "Filtering: ON/OFF" button therefore does nothing. MAUI has the same dead properties. The plan claims they are "honored at edit time / wired in Phase 04". |
| UNO-11 | Medium | bug | Leak across navigation: `OnUnloaded` detaches paint/input but never unsubscribes `_boundCollectionChangedSource.CollectionChanged` (nor `Columns`/`TableSummaryRows` collection handlers), so a long-lived VM collection roots the `DataGridView`, its `SKXamlCanvas` bitmap and the whole page. MAUI's `Cleanup` does unsubscribe it. |
| UNO-12 | Medium | api-design | `UseKumikoUI(this IHostBuilder)` is a no-op that exists only for "symmetry" with MAUI, drags `Microsoft.Extensions.Hosting.Abstractions 9.0.4` into every consumer, is not called by the sample, is named differently from MAUI's `UseSkiaKumikoUI`, and the docs suggest doing font registration "inside `UseKumikoUI()`" although it is synchronous and does nothing. Nothing on Uno needs registration (`SKXamlCanvas` self-hosts). |
| UNO-13 | Medium | bug | Long-press: relies on WinUI `Holding` (touch only, `IsHoldingEnabled` is Uno0001 on some heads, Uno raises it on its own thresholds) and does **not** suppress the `Released` that follows, whereas MAUI runs its own 500 ms timer and swallows the matching Released. |
| UNO-14 | Medium | bug / perf | Wheel handling assumes 120-unit notches and Core marks every wheel event handled. On Uno Skia heads `MouseWheelDelta` magnitudes differ (macOS trackpad precise deltas, X11 buttons 4/5, WASM `deltaMode` pixel/line values), so one event can scroll `WheelScrollMultiplier` rows (3) per *pixel* or almost nothing; horizontal sign convention is unverified; because `HandleScroll` always sets `Handled`, a grid inside a scrollable page never lets the page scroll even at the end of its content. |
| UNO-15 | Medium | missing-feature | No `Theme` DP (MAUI has one) and no `ActualTheme`/`RequestedTheme` integration; the default `DataGridStyle` is light, so the grid is a white rectangle inside a dark-themed Uno app. Both sample pages carry work-arounds. |
| UNO-16 | Medium | test-gap | Tests cover only `InputMapping` (41 enum-table cases). Untested but headlessly testable: proxy sentinel diff (`"​a"`→`a`, `""`→Backspace, IME replacement, `\n`/`\t`), double-tap heuristic, wheel normalisation, `ToMillis`, the Core `PinBottomSummaryRows` branch added by this PR (no Core test), and host→Core event sequences (Pressed/Released with `Button=None`, Holding+Released). CI never builds `SampleApp.Uno` although the csproj and docs claim the other heads are "CI-verified"; the publish gate runs only Core tests. |
| UNO-17 | Medium | docs | `docs/uno/*` is stale against the code it describes. |
| UNO-18 | Low | bug | DPI/scale is derived per paint from the **Grid's** `ActualWidth`, not the canvas's, and pointer coordinates are relative to the Grid: any `Padding`/`BorderThickness` set on the `DataGridView` (it is a `Grid`) skews the scale and misaligns hit-testing. No `XamlRoot.Changed`/`RasterizationScale` handling (plan item left unchecked); relies on `SKXamlCanvas` re-rastering on DPI change (unverified on Uno heads). |
| UNO-19 | Low | bug (latent) | Pointer edge cases: (1) no pointer-id tracking — a second finger raises another `Pressed` that resets Core's pan/drag origin and later a stray `Released`; (2) `ReleasePointerCapture` inside `OnPointerReleased` raises `PointerCaptureLost` → a `Cancelled` is dispatched *after* every `Released` (benign today because Up already cleared the flags, but an ordering hazard); (3) `Released` always carries `Button = None` because `IsLeftButtonPressed` is false at release, and Core copies it into the synthetic Pressed for `ActivatesImmediately` editors (harmless only because Core never branches on `Button`). |
| UNO-20 | Low | missing-feature | Resize-cursor feedback cannot work: hover moves are (correctly) filtered because Core's `HandlePointerMove` would pan on hover, the host never subscribes `GridInputController.CursorChanged`, and `OnPointerExited` only clears `ProtectedCursor`. |
| UNO-21 | Low | api-design | `AttachInputHandlers` forces `IsTabStop = true` on every `Loaded` (overriding a consumer's `IsTabStop="False"`) and sets `TabIndex = 0` whenever it is at the default, putting the grid *first* in the page's tab order regardless of position. |
| UNO-22 | Low | docs / bug | Sample app issues: Android `WindowSoftInputMode = AdjustNothing` means the soft keyboard covers the editing cell with no scroll-into-view; WASM `LinkerConfig.xml` preserves only the app assembly while Core resolves `PropertyName` via compiled `Expression.Property` chains (trimming risk for `KumikoUI.Core` in Release WASM/iOS AOT); `ReadMe.md` is template boilerplate; `MainShell` navigates on `Loaded` so the first page is created twice if `Loaded` re-fires. |
| UNO-23 | Low | api-design | Parity nits: no `[ContentProperty(Name = nameof(Columns))]` (MAUI has it); `RowHeight`/`HeaderHeight` are `double` (MAUI `float`); setting `GridStyle` replaces the style object and silently discards the `RowHeight`/`HeaderHeight` DP values previously applied (same in MAUI); no `DependencyObject`-based `ActionButtonDefinition` (MAUI has `MauiActionButtonDefinition`), so action-button commands cannot be declared/bound in Uno XAML and the sample builds that column in code. |
| UNO-24 | Low | bug | Touch/hardware-keyboard mix: touch taps never focus anything, so keyboard focus stays on a previous control (typing goes elsewhere); after a touch edit ends focus goes to the Grid, whose `KeyDown` is documented as unreliable on WASM/mobile, so iPad/Android users with a Bluetooth keyboard lose arrow navigation after one touch edit. |
| UNO-25 | Low | packaging | Package metadata: Description claims "Mac Catalyst" (no such head), `PackageTags` lacks `wasm`/`webassembly`/`desktop`, tests reference `coverlet.collector` but CI never collects coverage, `Microsoft.NET.Test.Sdk 17.12.0` / `xunit 2.9.2` are a year behind. |

### Report 05 — Tests, CI, packaging, hygiene ([05-tests-ci-packaging.md](05-tests-ci-packaging.md))

| ID | Severity | Category | Title |
|---|---|---|---|
| TEST-01 | Blocker | Tests | ~60% of Core (by LOC) and 72/120 public types have no tests: renderer, input controller, all drawn editors, edit pipeline, hit-testing, popups/focus |
| TEST-02 | High | Tests | No rendering tests at any layer: no drawing-context double, no SkiaSharp raster/golden tests, no Maui/Uno host tests |
| TEST-03 | High | Tests | No benchmark project; README performance claims are unmeasured and PR #1 changes the hot path |
| TEST-04 | Medium | Tests/CI | Code coverage is never collected or reported |
| TEST-05 | Medium | Tests | Several tests are tautological or vacuous |
| TEST-06 | Medium | Tests/Repo | Test suite cannot run on a machine with only the .NET 10 runtime (no roll-forward, no global.json) |
| TEST-07 | Low | Tests | Uno tests cover only the enum mapper; Maui has no tests; Uno tests are not part of the publish gate |
| TEST-08 | Low | Tests | Test dependencies outdated |
| TEST-09 | Low | Tests/Docs | Testing instructions are stale/contradictory |
| CI-01 | Blocker | CI/Release | No release gate: unprotected `main`, unprotected publish environment, `workflow_dispatch` publishes any version from any ref, Uno tests not in gate |
| CI-02 | High | CI | Sample apps and Apple app heads are never built; no macOS runner |
| CI-03 | High | CI | Workload cache never hits — every Windows job reinstalls the MAUI/Uno workloads; workloads unpinned |
| CI-04 | High | CI/Security | No dependency or security automation; actions out of date and not SHA-pinned |
| CI-05 | Medium | CI/Security | Over-broad `GITHUB_TOKEN` permissions |
| CI-06 | Medium | CI/Quality | No analyzer, warning or formatting enforcement; 96 CS1591 + 90 whitespace drifts today |
| CI-07 | Medium | CI | Core/SkiaSharp are built and tested on Linux only; SkiaSharp native assets never exercised; CI SDK pinned to a dying major |
| CI-08 | Low | CI/Release | Release-process rough edges |
| PKG-01 | Blocker | Packaging/Versioning | No single source of truth for the version: csproj says 1.0.0, tags say 0.1.x, local pack yields 1.0.0; no changelog or release notes |
| PKG-02 | Blocker | Packaging/TFM | Core/SkiaSharp/Uno target `net9.0` (STS, end of support 2026-11-10) while Maui and both samples are `net10.0`; shipping 1.0 on a TFM that dies in 19 days |
| PKG-03 | High | Packaging/README | Embedded README renders broken on NuGet.org and is MAUI-only for all four packages |
| PKG-04 | High | Packaging/API | No API-compatibility or public-surface gate for 1.0 |
| PKG-05 | Medium | Packaging/Versioning | Dependency versions duplicated across projects; no central package management; no global.json; Uno.Sdk pinned inline ×3; SkiaSharp a major behind |
| PKG-06 | Medium | Packaging/Reproducibility | Non-deterministic copyright year; redundant conditional SourceLink reference; symbols only via CLI flags |
| PKG-07 | Medium | Packaging | KumikoUI.Uno nupkg bloat (~1,200 transitive AndroidX content entries) accepted "as-is"; package not yet published |
| PKG-08 | Low | Packaging | Metadata nits |
| REPO-01 | High | Repo hygiene | Missing governance/community files; stock issue templates; default labels; no milestones/Discussions; Code of Conduct contact is "GitHub." |
| REPO-02 | High | Repo hygiene/PR #1 | PR #1 (NativeAOT & trimming) is stale, conflicting and unreviewed by a human; the approach needs rework before it can land |
| REPO-03 | Medium | Repo hygiene/Branching | `develop` vs `main` model is unclear and inconsistently used; stale branches |
| REPO-04 | Medium | Repo hygiene/Docs | Contributor-facing instructions are MAUI-only and partly wrong after the Uno port |
| REPO-05 | Low | Repo hygiene/Size | Large binaries in history; duplicated 9 MB font; template leftovers |
| REPO-06 | Low | Repo hygiene/.gitignore | `.gitignore` gaps (no tracked user files found — positive) |
| REPO-07 | Medium | Legal/Licensing | Bundled third-party fonts lack their required license notices |
| REPO-08 | Low | Legal/Metadata | Copyright holder naming inconsistent across LICENSE, package metadata and PR links |

### Report 06 — Documentation ([06-docs.md](06-docs.md))

| ID | Severity | Category | Title |
|---|---|---|---|
| DOC-01 | Blocker | nuget-render | README breaks on nuget.org (raw HTML, relative images, relative links, emoji anchors) and the same 34 KB MAUI-centric README ships in all four packages |
| DOC-02 | Blocker | inaccurate | Nine README snippets fail to compile / parse against the current API |
| DOC-03 | High | stale | All user-facing and contributor docs describe a three-project, MAUI-only library; Uno is absent |
| DOC-04 | High | stale / missing | `docs/uno/` is an implementation checklist with stale status, not consumer documentation; no Uno getting-started exists |
| DOC-05 | High | inaccurate / unverifiable | Headline performance and architecture numbers are unmeasured or wrong |
| DOC-06 | High | inaccurate | "Zero native controls" and "scrollbars … rendered directly on a SkiaSharp canvas" are both overstated |
| DOC-07 | High | missing | 1.0 documentation set is incomplete (ranked list in §4) |
| DOC-08 | Medium | missing | 96 public members in KumikoUI.Core lack XML docs (CS1591) despite `GenerateDocumentationFile=true`; instructions claim 100% |
| DOC-09 | Medium | stale / inaccurate | docs/ARCHITECTURE.md structure and class inventory do not match the tree |
| DOC-10 | Medium | inaccurate | docs/RENDERING.md describes a pipeline, signatures and names that differ from code |
| DOC-11 | Medium | inaccurate / stale | Copilot instructions & prompts generate code against non-existent members |
| DOC-12 | Medium | inaccurate | Behavioural claims that differ from the implementation |
| DOC-13 | Medium | naming | API naming the 1.0 should settle (details §6) |
| DOC-14 | Medium | inaccurate / missing | Platform & requirements statements are incomplete and ambiguous |
| DOC-15 | Low | stale | Contributing instructions do not work on a bare machine and CONTRIBUTING.md is missing |
| DOC-16 | Low | stale | Issue templates are GitHub's generic web-app defaults |
| DOC-17 | Low | missing | New public API in this PR is undocumented in user docs |

### Report 07 — External research ([07-external-research.md](07-external-research.md))

Not a findings report; see its final section, "Implications for KumikoUI 1.0", for the 15 ranked implications with sources.


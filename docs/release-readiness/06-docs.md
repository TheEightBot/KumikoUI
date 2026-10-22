# KumikoUI 1.0 release-readiness review — slice: documentation accuracy & completeness

Reviewer scope: README.md, docs/ARCHITECTURE.md, docs/RENDERING.md, docs/uno/*, .github/copilot-instructions.md, .github/instructions/*.md, .github/prompts/*.md, .github/ISSUE_TEMPLATE/*.md, cross-checked against the code on branch `feature/uno-platform-port` (PR #9, merge-base with `main` = `a11a5aa`). No repo files were modified. Build evidence: `dotnet build src/KumikoUI.SkiaSharp/KumikoUI.SkiaSharp.csproj -c Release --no-incremental` (log: `/private/tmp/claude-501/-Users-mstonis-Developer-KumikoUI/8e19cc50-8ba5-4cd9-a515-34788102ecc3/scratchpad/build-skiasharp-full.log`).

Citation keys: `R:` README.md, `A:` docs/ARCHITECTURE.md, `RD:` docs/RENDERING.md, `U:` docs/uno/README.md, `CI:` .github/copilot-instructions.md, `AI:` .github/instructions/architecture.instructions.md, `MI:` maui-control.instructions.md, `RI:` rendering.instructions.md, `MD:` models-and-data.instructions.md, `CF:` .github/prompts/configure-datagrid-features.prompt.md, `BR:` .github/ISSUE_TEMPLATE/bug_report.md. Code paths are relative to `/Users/mstonis/Developer/KumikoUI/`.

---

## (a) Summary — five bullets

1. **The README will render broken on nuget.org, and it is embedded in all four packages.** `Directory.Build.props:21,40` packs the root README into Core, SkiaSharp, Maui and Uno. Lines R:1-28 (logo/title/nav/badges), R:61-63 (hero image) and R:869-890 (screenshot table) are raw HTML with relative `images/...` paths; nuget.org disables HTML and only renders images from an allow-list of absolute hosts (Microsoft Learn, fetched). The live page for KumikoUI.Maui 0.1.10 already shows `<p align="center"> <img src="images/logo.svg" ...>` as literal text. A Core-only or Uno consumer also gets a MAUI-only "Getting Started".
2. **Nine README code samples do not compile or will not parse against the 1.0 API.** `ColumnType="ProgressBar"` (no such enum value), `Width="*"` (float property, no star parser), `EditTextSelectionMode="End"` (member is `CursorAtEnd`), `NumericUpDownEditorDescriptor { Min, Max }` (members are `Minimum`/`Maximum`), `new SummaryDescription` (type does not exist; method takes `GroupSummaryRow`), `new GridColor(0.95f, …)` (byte-only ctor), `SelectedRowColor`/`FrozenDividerColor`/`CellFontSize`/`HeaderFontSize`/`CellFontBold`/`HeaderFontBold` (none exist on `DataGridStyle`), `CellStyle { Bold = true }` (no `Bold`), and `await` inside non-async `CreateMauiApp()`.
3. **Every user-facing document is MAUI-only and describes a three-project library; PR #9 ships a fourth project and touched none of them.** `git diff a11a5aa..HEAD` changes 0 lines in README.md, docs/ARCHITECTURE.md, docs/RENDERING.md, copilot-instructions, instructions, prompts or issue templates; README mentions "Uno" 0 times; the packages table (R:104-108), badges (R:18-26), Architecture (R:823-831), Requirements (R:894-904) and Sample section (R:856-867) all omit KumikoUI.Uno / WebAssembly / Linux desktop. Meanwhile `docs/uno/README.md:7-8` still says "No `KumikoUI.Uno` code exists yet".
4. **The headline numbers are unmeasured or wrong.** "60 fps" (R:71) has no benchmark or fps instrumentation anywhere in the repo; "~60 GridPaint objects" (R:765,841; A:51,114; RD:21,134) is 29 in `PaintCache.cs`; "14 `HitRegion` types" (R:840; A:50,150) is 15 with different names (`HitTesting.cs:9-26`); "18-step pipeline" (R:837,852; RD:5) matches nothing numbered in `DataGridRenderer.cs`; "canvas-drawn scrollbars" (R:32; RI:116) — the grid draws no scrollbars at all (only the filter popup's list does, `DrawnFilterPopup.cs:518-526`).
5. **The 1.0 documentation set is missing most of what a NuGet library needs**: no CHANGELOG, CONTRIBUTING.md (PR #9's own body links to a non-existent `.github/CONTRIBUTING.md` on the wrong org), SECURITY.md, versioning/support policy, API reference, per-package README, Uno getting-started, platform/min-OS matrix covering Uno heads, accessibility statement, performance guidance, theming reference (README shows 13 of ~95 `DataGridStyle` members, 7 of them wrong), troubleshooting/FAQ (closed issues #2, #5, #6 are exactly FAQ material), samples README, or Uno/WASM screenshots. XML docs: 96 public Core members lack `///` (CS1591), contradicting `AI:66` "All public API members have `/// <summary>`".

---

## (b) Findings table (ranked)

| ID | Severity | Category | Title | Evidence | Impact | Suggested fix |
|---|---|---|---|---|---|---|
| DOC-01 | **Blocker** | nuget-render | README breaks on nuget.org (raw HTML, relative images, relative links, emoji anchors) and the same 34 KB MAUI-centric README ships in all four packages | `Directory.Build.props:21` (`PackageReadmeFile`), `:40` (packs root README into every project); R:1-28 `<p align>`/`<img src="images/logo.svg">`/`<h1 align>`/badge `<img>`s; R:61-63 `<img src="images/main.png">`; R:869-890 `<table>`/`<img>`/`<br/>`/`<sub>`; R:36-57 anchors `#-getting-started` built from emoji headings; R:852,917 `docs/ARCHITECTURE.md` and R:923 `LICENSE` relative links. Microsoft Learn "Package readme on NuGet.org": only CommonMark features are rendered (HTML not listed), images only from an allow-list (img.shields.io, raw.githubusercontent.com, user-images.githubusercontent.com …), "images with relative local paths … will not be rendered and will produce a warning … only visible to the package owners". Live nuget.org page for KumikoUI.Maui 0.1.10 shows the header tags as literal text. | First impression of the 1.0 package page is a wall of escaped tags with no logo, no screenshots, no working badges, broken TOC, 404 links. Core/SkiaSharp/Uno consumers read "dotnet add package KumikoUI.Maui" + `MauiProgram.cs` instructions. | Create `docs/nuget/README.<Package>.md` per package (plain Markdown, ≤150 lines, no HTML, no emoji in headings, absolute `https://raw.githubusercontent.com/TheEightBot/KumikoUI/main/images/…` images, markdown-syntax badges `![](https://img.shields.io/…)`, absolute links to GitHub docs); set `<PackageReadmeFile>` per csproj and drop the global `None Include` in Directory.Build.props. Keep the rich HTML README for GitHub. Preview via nuget.org upload tab before tagging. |
| DOC-02 | **Blocker** | inaccurate | Nine README snippets fail to compile / parse against the current API | (1) R:73,163,182-185 `ColumnType="ProgressBar"` — `DataGridColumnType` has no `ProgressBar` (`src/KumikoUI.Core/Models/DataGridColumn.cs:12-30`); the sample uses a Template column + `ProgressBarCellRenderer` (`samples/SampleApp.Maui/MainPage.xaml:106`). (2) R:304 `Width="*"`/`"2*"` — `Width` is `float` (`DataGridColumn.cs:89`), star sizing is `SizeMode="Star"` + `StarWeight` (`:98-101`; `GridLayoutEngine.cs:28-39`); no `"*"` parser exists in Core/Maui/Uno. (3) R:491 `EditTextSelectionMode="End"` — enum is `SelectAll`, `CursorAtEnd` (`src/KumikoUI.Core/Editing/EditSession.cs:33-39`). (4) R:510 `NumericUpDownEditorDescriptor { Min = 0, Max = 10 }` — properties are `Minimum`/`Maximum` (`EditorDescriptor.cs:33-36`). (5) R:412 `AddGroupSummaryRow(new SummaryDescription {…})` — no `SummaryDescription` type; method takes `GroupSummaryRow` (`DataGridSource.cs:194`, `SummaryDescription.cs:106`). (6) R:609-615 `new GridColor(0.95f, 0.95f, 0.97f)` — only `GridColor(byte,byte,byte,byte=255)` (`Color.cs:24`); float→byte is CS1503. (7) R:613,615,618-621 `SelectedRowColor`, `FrozenDividerColor`, `CellFontSize`, `HeaderFontSize`, `CellFontBold`, `HeaderFontBold` — none exist; real members are `SelectionColor` (`GridState.cs:75`), `FrozenColumnDividerColor` (`:118`), `HeaderFont`/`CellFont` as `GridFont` (`:94-96`). (8) R:639 `new CellStyle { …, Bold = true }` — `CellStyle` has `Font`, no `Bold` (`StyleClasses.cs:9-30`). (9) R:665-679 `await FileSystem.OpenAppPackageFileAsync` inside non-async `public static MauiApp CreateMauiApp()` — CS4033; the sample uses `.GetAwaiter().GetResult()` (`samples/SampleApp.Maui/MauiProgram.cs:51-68`). | Copy-paste from the README fails for the first-run theming, summaries, editing and fonts scenarios — the exact paths a 1.0 evaluator tries first. | Fix each snippet (details in audit table §c). Add a `docs-snippets` compile check: a `tests/KumikoUI.Docs.Tests` project that `#include`s the README C# blocks (or a script that extracts ```csharp/```xml blocks into a compiled sample page) so CI fails on API drift. |
| DOC-03 | High | stale | All user-facing and contributor docs describe a three-project, MAUI-only library; Uno is absent | R:98-108 packages table (no KumikoUI.Uno), R:18-26 badges, R:823-831 "Architecture" (3 boxes), R:894-904 Requirements, R:856-867 samples (no SampleApp.Uno), R:546 keyboard "on all platforms (iOS/macOS … Android … Windows)"; CI:5 "DataGrid for .NET MAUI", CI:24 "three-layer project structure (Core → SkiaSharp → Maui)"; AI:9,15-32 "Three-Layer Architecture"/dependency table without Uno, AI:36-44 paths without `src/KumikoUI.Uno`, `samples/SampleApp.Uno`, `tests/KumikoUI.Uno.Tests`, AI:48-59 namespace map without `KumikoUI.Uno`; A:3 ".NET MAUI SkiaSharp KumikoUI", A:7-37; RD:8-12 "MAUI Trigger → SKCanvasView.InvalidateSurface()". No `.github/instructions/*.md` `applyTo` covers `src/KumikoUI.Uno/**`. PR #9 diff vs `a11a5aa` touches 0 of these files (only `docs/uno/**`, workflows, code). Code: `src/KumikoUI.Uno/KumikoUI.Uno.csproj:14-15` (net9.0;ios;android;browserwasm;desktop;+windows), `KumikoUIHostingExtensions.cs:26` `UseKumikoUI(IHostBuilder)`, `KumikoFonts.cs:55-114`. | Uno/WASM/Linux users cannot discover the package, its namespaces, `UseKumikoUI()`, `KumikoFonts`, or that `MauiActionButtonDefinition` has no Uno twin (Uno sample uses `ActionButtonDefinition` in code-behind, `samples/SampleApp.Uno/MvvmActionsPage.xaml.cs:125`). Copilot/Claude instructions actively steer contributors to a wrong structure. | Make README host-neutral: one "Hosts" section with a two-column MAUI/Uno getting-started (`UseSkiaKumikoUI` vs `UseKumikoUI`, `xmlns:dg="clr-namespace:KumikoUI.Maui;…"` vs `xmlns:kumiko="using:KumikoUI.Uno"`), add KumikoUI.Uno to packages table/badges/architecture/requirements/samples. Update CI:24, AI:15-59, A:7-37, RD:8-12 to four projects; add `uno-control.instructions.md` with `applyTo: "src/KumikoUI.Uno/**,samples/SampleApp.Uno/**,tests/KumikoUI.Uno.Tests/**"`. |
| DOC-04 | High | stale / missing | `docs/uno/` is an implementation checklist with stale status, not consumer documentation; no Uno getting-started exists | U:7-8 "Status: Planning artifact for review. No `KumikoUI.Uno` code exists yet … Implement only after this plan set is reviewed" (code exists: 8 files in `src/KumikoUI.Uno/`); U:73-74 "exact monikers … confirm after scaffolding", U:78 `net9.0-windows10.0.xxxxx.0` (actual `net9.0-windows10.0.26100`, csproj:15); `docs/uno/01-prerequisites.md:10` "Environment status — verified 2026-06-06 (this macOS dev machine)", `:52,61-63` unchecked workload items; `07-tests.md:82-88,112-113` only Desktop head smoke-verified, WASM/mobile "pending CI + device testing"; `04-input-keyboard-focus.md:392,413-416` interactive verification deferred. | A reader landing on docs/uno/README.md concludes the port is unbuilt; the only "Uno docs" are phase checklists with the author's machine state. Release-readiness: the docs themselves record that non-desktop heads have not been exercised by hand. | Rewrite `docs/uno/README.md` as the Uno user guide (install, `UseKumikoUI`, XAML, `KumikoFonts.RegisterFromAppPackageAsync`, per-head notes, known gaps) and move 01-08 to `docs/uno/plan/` (or delete; git history keeps them). Replace the "Status" box with a verified-heads matrix. |
| DOC-05 | High | inaccurate / unverifiable | Headline performance and architecture numbers are unmeasured or wrong | R:71 "60 fps" — no BenchmarkDotNet, no frame-time/fps counter anywhere (`grep -rE "BenchmarkDotNet|fps|FrameTime" src samples tests` → only unrelated hits); `samples/*/LargeDataPage.xaml.cs:31-53` measures generate/assign milliseconds only. R:72 "tested with 100K+ rows" — a manual sample page, no automated test. R:765,841 / A:51,114 / RD:21,134 "~60 `GridPaint` objects" — `PaintCache.cs` declares 29 `public GridPaint` members (+1 factory `:257`). R:840 / A:50,150 "14 `HitRegion` types (Cell, Header, ColumnResizeHandle, FilterIcon, SortIndicator, FrozenDivider, GroupPanel, GroupChip, …)" — `HitTesting.cs:9-26` has 15: `None, Cell, HeaderCell, HeaderResizeGrip, HeaderFilterIcon, RowHeader, RowDragHandle, FrozenColumn, EmptyArea, GroupHeaderRow, GroupChevron, GroupPanelChip, GroupPanelChipRemove, GroupSummaryRow, TableSummaryRow`. R:837,852 / RD:5-43 "18-step" — `DataGridRenderer.Render` (`DataGridRenderer.cs:86-305`) has no numbered steps; per-pane sequence is `DrawRows, DrawCurrentCellBorder, DrawGridLines, DrawHeader, DrawSortIndicators, DrawFilterIcons, DrawFrozenRows` (rows before headers), then dividers, top/bottom `DrawTableSummaryRows`, group overlays, `DrawDragHandleColumn`, `DrawColumnDragOverlay`, `DrawRowDragOverlay`, `editSession.DrawEditor`, `popupManager.DrawPopups`. | Marketing claims a 1.0 cannot back; wrong internals mislead contributors and the AI instructions. | Either add a BenchmarkDotNet project + a frame-time overlay in LargeDataPage and publish numbers (device, row count, fps), or reword to "virtualized rendering; the sample scrolls 100K rows smoothly on …". Replace "~60", "14", "18" with words ("pre-computed paints", "hit regions") or regenerate from code. |
| DOC-06 | High | inaccurate | "Zero native controls" and "scrollbars … rendered directly on a SkiaSharp canvas" are both overstated | R:5,32 "cells, headers, editors, scrollbars, popups — rendered directly on a SkiaSharp canvas … zero native UIKit / AppKit / WinUI controls"; CI:6-7; AI:10; A:89,105; RI:116 "`DrawScrollbars` — canvas-drawn scrollbars". Code: no grid scrollbar drawing exists (`grep -rni scrollbar src` → only `DrawnFilterPopup.cs:518-526`, the popup list's own thumb); keyboard input uses a hidden native `Entry` on MAUI (`src/KumikoUI.Maui/DataGridView.cs:31,37,91-…`) and a hidden native `TextBox` proxy on Uno (`src/KumikoUI.Uno/DataGridView.cs:61-64`, `DataGridView.Input.cs:102-103,124`). | Desktop/WASM users expect scrollbars; evaluators will test the claim. "Zero native controls" also has accessibility implications (DOC-07 #9). | Remove "scrollbars" from R:32 and `DrawScrollbars` from RI:116 until implemented; reword to "no native controls are used for rendering; a hidden native text proxy captures keyboard/IME input". |
| DOC-07 | High | missing | 1.0 documentation set is incomplete (ranked list in §4) | No `CHANGELOG.md`, `CONTRIBUTING.md`, `SECURITY.md`, `docs/VERSIONING.md`, API reference (no DocFX/`docfx.json`), per-package README, Uno guide, platform matrix, accessibility statement, performance guidance, theming reference, cookbook, FAQ, samples README, Uno/WASM screenshots (`ls`, `find docs .github -type f`). PR #9 body links `https://github.com/EightBot/KumikoUI/blob/main/.github/CONTRIBUTING.md` — wrong org (`TheEightBot`, `Directory.Build.props:16`) and file absent; no `PULL_REQUEST_TEMPLATE.md`. Tags are `v0.1.1…v0.1.10` while every csproj says `<Version>1.0.0</Version>` (Core:11, SkiaSharp:19, Maui:15, Uno:49) with no release notes. | Adoption friction, no upgrade story, no security disclosure path, contributors cannot find rules. | See §4 ranked list; minimum for 1.0: CHANGELOG (Keep-a-Changelog), CONTRIBUTING, SECURITY, platform matrix, theming reference, FAQ seeded from issues #2/#5/#6, Uno guide. |
| DOC-08 | Medium | missing | 96 public members in KumikoUI.Core lack XML docs (CS1591) despite `GenerateDocumentationFile=true`; instructions claim 100% | Build log: 96 unique CS1591 in Core, 0 in SkiaSharp (§d). Whole `HitRegion` enum (15 values) + `HitTestResult` (7 members) `HitTesting.cs:11-43`; `CellPosition` ctor/Equals/operators `SelectionModel.cs:41-57`; `GridColor` ctor/Equals/operators `Color.cs:24,84-89`; `GridFont`/`GridRect`/`GridSize`/`GridPoint`/`GridFontMetrics` ctors `Primitives.cs:20-22,120,155,176,204`; `DataGridSource.DataChanged` `:92`; five `GridInputController` events `:69-73` (`ColumnResized`, `ColumnReordered`, `RowReordered`, `RowTapped`, `RowDoubleTapped`); `TextCellRenderer/BooleanCellRenderer/ImageCellRenderer/ProgressBarCellRenderer.Render`; `CellValidationResult.Valid/Error/Warning`; `*EventArgs` ctors/properties for Filter/Group/Sort/Selection. AI:66 "All public API members have `/// <summary>` XML doc comments." Heuristic (no build) for Maui: ≤12 undocumented (`DataGridView.cs:84,259,1355-1357,1399-1452`); Uno: 0. | IntelliSense gaps on the most-used primitives (`GridColor(r,g,b)`, `GridFont(family,size)`, `GridRect`, `CellPosition`, events); the public API surface is not reviewable from docs. | Document the 96 (3–4 h), then add `<WarningsAsErrors>$(WarningsAsErrors);CS1591</WarningsAsErrors>` to `Directory.Build.props` so CI enforces it; consider `<inheritdoc/>` for overrides/interface impls. |
| DOC-09 | Medium | stale / inaccurate | docs/ARCHITECTURE.md structure and class inventory do not match the tree | A:12 `Data/ # Data source management (future)` — no such folder (`find src/KumikoUI.Core -type d`); A:20-21 `Hosting/`, `Platforms/` under KumikoUI.Maui — neither exists (Maui has 3 root files: `DataGridHostingExtensions.cs`, `DataGridView.cs`, `MauiActionButtonDefinition.cs`); A:22-25 samples/tests omit `SampleApp.Uno`, `KumikoUI.Uno.Tests`; A:36 TFMs omit `net10.0-windows10.0.19041.0` (`KumikoUI.Maui.csproj:5`); A:85 `KumikoUIHostingExtensions` is correct but lives in `DataGridHostingExtensions.cs` while AI:59 says the type is `DataGridHostingExtensions` (wrong); A:91-100 Drawn Components table omits `DrawnActionButtons` (`Components/DrawnActionButtons.cs:35`); A:150 HitRegion list wrong (see DOC-05); A:11 "(10 types)" — 12 files/9 concrete `Drawn*` + base + FocusManager + PopupManager. | Contributor on-ramp points at folders/classes that don't exist. | Regenerate the tree from `tree src -I obj`, fix names, add Uno row to every table. |
| DOC-10 | Medium | inaccurate | docs/RENDERING.md describes a pipeline, signatures and names that differ from code | RD:117-123 renderer table uses `TextColumn`, `BooleanColumn`, `ImageColumn`, `ProgressBarColumn`, `TemplateColumn` — not type/enum names (enum is `Text/Boolean/Image/Template`, no ProgressBar); RD:103 `cellRenderer.Draw(context, cellRect, value, paint)` vs `ICellRenderer.Render(ctx, cellRect, value, displayText, column, style, isSelected, cellStyle)` (`ICellRenderer.cs:48-56`); RD:24-33,75-85 per-pane "DrawColumnHeaders → DrawRows → DrawFrozenRowDividers → DrawFrozenRows → DrawTableSummaries → DrawGroupHeaders → DrawGroupSummaries" vs actual `DrawRows → DrawCurrentCellBorder → DrawGridLines → DrawHeader → DrawSortIndicators → DrawFilterIcons → DrawFrozenRows` (`DataGridRenderer.cs:154-164`); RD:200 "Each popup provides a `Draw(context, rect)`" vs `entry.Component.OnDraw(ctx)` (`PopupManager.cs:64-75`); RD:192 "writes back via compiled property setter" vs reflection `GetProperty(...)` in `SetCellValue` (`DataGridSource.cs:933-942`; only getters are compiled, `:1574-1597`); RD:153 typeface cache "disposed at context disposal" — true but omits that registered/default typefaces are skipped (`SkiaDrawingContext.cs:334-339`); no mention of `PinBottomSummaryRows` footer (`GridState.cs:236`, added in this PR) or `DrawColumnDragOverlay`; RD:8-12 MAUI-only entry chain. | Rendering doc is the contributor's map for the hottest code path; wrong order/names cost real time. | Regenerate the step list from `Render()`; use real method names; add the Uno entry chain (`SKXamlCanvas.PaintSurface → Invalidate()`); document the pinned footer. |
| DOC-11 | Medium | inaccurate / stale | Copilot instructions & prompts generate code against non-existent members | RI:104-121 render-loop names `DrawHeaderRow`, `DrawFrozenRows`, `DrawScrollableArea`, `DrawDataRow`, `DrawCell`, `DrawFrozenColumns`, `DrawGroupHeaders`, `DrawSummaryRows`, `DrawGroupSummaryRows`, `DrawActiveEditor`, `DrawScrollbars` — only `DrawRowDragOverlay` and `DrawFrozenRows` exist (`DataGridRenderer.cs` private `Draw*` list: `DrawFrozenBackground, DrawRightFrozenBackground, DrawFrozenColumnDivider, DrawRightFrozenColumnDivider, DrawFrozenRowDivider, DrawHeader, DrawRows, DrawFrozenRows, DrawGridLines, DrawCurrentCellBorder, DrawSortIndicators, DrawFilterIcons, DrawDragHandleColumn, DrawRowDragOverlay` …); CF:107 `dataGridView.Source.GroupDescriptions.Add(...)` — no `Source` property (it is `DataSource`, `Maui/DataGridView.cs:201`) and `GroupDescriptions` is `IReadOnlyList` (`DataGridSource.cs:233`; use `AddGroupDescription`); CF:115-124 `<dg:DataGridView.GroupSummaryRows>` — no such bindable/dependency property on MAUI (`DataGridView.cs` BindableProperties: ItemsSource, FrozenRowCount, GridSelectionMode, IsReadOnly, DismissKeyboardOnEnter, EditTriggers, EditTextSelectionMode, AllowSorting, AllowFiltering, HeaderHeight, RowHeight, GridDescription, Columns, TableSummaryRows) nor Uno (`DataGridView.Properties.cs:25-385`); CF:172 `TableSummaryRows.Add` on a property whose default is `null` (`DataGridView.cs:542-546`); AI:59 wrong type name; CI:24 "three-layer". | AI-assisted contributions (the repo's declared workflow) will reproduce these errors. | Treat instruction files as code: cross-check in CI with a script that greps every backticked identifier against `public` declarations; fix the listed lines. |
| DOC-12 | Medium | inaccurate | Behavioural claims that differ from the implementation | R:357 "Filters are applied immediately on close … Remove a filter by opening the popup and selecting all values" — popup has OK / Cancel / Clear buttons; filter applies on OK (`DrawnFilterPopup.cs:13,77,201,262,539-546`). R:80,363 "Drag-and-drop group panel" — only chip click/remove hit regions found (`GridInputController.cs:453,466`); no header-to-panel drag handler located (marked Unverifiable). R:341-347 "Multi-column sort … via code" then sets only `SortDirection` — works for one column via fallback (`DataGridSource.cs:1121-1127`) but multi-sort also needs `SortOrder` 1..n (`:250-252`, `:1117-1118`); user-driven multi-sort is `ToggleMultiSort` on a modified header tap (`GridInputController.cs:749`) and is undocumented. R:596-598 `Theme="…"` works but is a plain CLR property, not a `BindableProperty` (`Maui/DataGridView.cs:223-235`) — `{Binding}`/styles won't work; Uno has no `Theme` at all (set `GridStyle = DataGridTheme.Create(mode)`). R:524-528 omits `SelectionMode.None` (`SelectionModel.cs:11`); R:424 omits `SummaryType.Custom` + `CustomAggregate` (`SummaryDescription.cs:19,60`). R:858 "four pages" then lists six; project has seven XAML pages. R:866 "performance metrics" = ms timings, not fps. R:389 pinned bottom summaries is true only because of the new `PinBottomSummaryRows` (default true) which README never names. | Users debug "why doesn't my filter apply on close / why doesn't multi-sort work". | Correct wording; document `SortOrder`, `ToggleMultiSort` gesture, OK/Cancel/Clear, `PinBottomSummaryRows`, `Theme` bindability; verify or drop "drag-and-drop group panel". |
| DOC-13 | Medium | naming | API naming the 1.0 should settle (details §6) | `RowTappedEventArgs2` is public (`GridInputController.cs:1477`; used by both hosts `Maui/DataGridView.cs:1009-1016`, `Uno/DataGridView.cs:280-287`); file/type mismatches: `DataGridTheme.cs` holds `DataGridThemeMode`+`DataGridTheme` while `DataGridStyle` lives in `GridState.cs:53` next to `ScrollState`; `StyleClasses.cs`→`CellStyle/RowStyle`; `Color.cs`→`GridColor`; `Primitives.cs`→`GridFont/GridPaint/GridRect/PaintStyle/…`; `HitTesting.cs`→`HitRegion/HitTestResult`; `InputEvents.cs`→`GridKey/…`; `DataGridHostingExtensions.cs`→`KumikoUIHostingExtensions`. Four prefix families: `DataGrid*`, `Grid*`, `Drawn*`, `Kumiko*` (`KumikoFonts`, `KumikoUIHostingExtensions`) plus unprefixed (`PaintStyle`, `SelectionMode`, `SelectionUnit`, `SortDirection`, `SummaryType`, `CheckState`, `ValidationState`, `DragHandlePosition`). Host asymmetry `UseSkiaKumikoUI()` vs `UseKumikoUI()`. `GridSelectionMode` property wraps `SelectionMode` enum. `IsFrozen`/`IsFrozenRight`/`FreezeMode` triple. Product name appears as ".NET KumikoUI" (R:3), "the Kumiko" (RD:3), "KumikoUI control" (csproj descriptions), "KumikoUI DataGrid" (Uno csproj:50). | Post-1.0 renames are breaking; docs must use one vocabulary. | Decide before tagging 1.0: rename `RowTappedEventArgs2`→`RowTappedEventArgs`; move `DataGridStyle` to `DataGridStyle.cs`; pick one prefix policy (e.g. `Grid*` for primitives, `DataGrid*` for model/host, no `Kumiko*` types); unify hosting method name; state the product name once. |
| DOC-14 | Medium | inaccurate / missing | Platform & requirements statements are incomplete and ambiguous | R:20 badge "iOS \| Android \| macOS \| Windows" and R:32 omit WebAssembly and Linux (Uno `net9.0-browserwasm`, `net9.0-desktop`, csproj:14); R:18 ".NET 10.0 / 9.0" vs library net9.0, MAUI host net10.0, Uno host net9.0 heads; R:896-904 table lacks Uno heads/min OS, lacks "Windows TFM only built on Windows" (`Maui.csproj:5`, `Uno.csproj:15`), "macOS Catalyst 15.0+" reads as macOS 15 (Catalyst 15 ≈ macOS 12); nuget.org lists KumikoUI.Maui 0.1.10 as `net10.0-android36.0 / ios26.0 / maccatalyst26.0 / windows10.0.19041` — the SDK-level targets a consumer must match are undocumented; SkiaSharp 3.119.2 pin and why it matters (`Uno.csproj:26-30`) undocumented. | Wrong expectations on supported OS versions; support tickets. | Add a "Supported platforms" matrix per package: host, TFM, min OS, build-host requirement, verification status (CI / manual / none). |
| DOC-15 | Low | stale | Contributing instructions do not work on a bare machine and CONTRIBUTING.md is missing | R:912-915 `dotnet build KumikoUI.sln` requires MAUI workloads (ios/android/maccatalyst), Uno workloads (wasm-tools, android, ios) and Windows for the windows TFMs; `dotnet test` at the root also builds `tests/KumikoUI.Uno.Tests` (Uno.Sdk, net9.0-desktop); `docs/uno/01-prerequisites.md:52-63` shows even the author's machine lacks wasm/android/maccatalyst workloads. `.github/workflows/ci.yml:6-13` ignores `**.md`/`docs/**` so docs are never validated (no link/markdown lint). | New contributors fail at step 2. | Document the per-project commands used by CI (`ci.yml:34-48`) as the contributor path; add CONTRIBUTING.md; add a markdown link-checker job (`lychee`) and a README-snippet compile job not covered by `paths-ignore`. |
| DOC-16 | Low | stale | Issue templates are GitHub's generic web-app defaults | BR:26-35 "Desktop: OS/Browser/Version", "Smartphone: Device [iPhone6], OS [iOS8.1], Browser [stock browser]"; `feature_request.md` generic; no fields for package (Core/SkiaSharp/Maui/Uno) + version, host framework, TFM/head, OS + version, device/emulator, renderer (Skia/Metal/GL), fonts registered, repro project. | Bug reports arrive without the data needed (issue #5 needed platform + font info). | Replace with YAML issue forms (`.github/ISSUE_TEMPLATE/bug.yml`) with dropdowns for package/host/platform and required version fields; add `config.yml` linking to discussions/FAQ. |
| DOC-17 | Low | missing | New public API in this PR is undocumented in user docs | `DataGridStyle.PinBottomSummaryRows` (`GridState.cs:236`, only in `U:31-36`); `KumikoFonts` (`src/KumikoUI.Uno/KumikoFonts.cs:36-114`); `UseKumikoUI` (`KumikoUIHostingExtensions.cs:26`); `ScrollSettings` property on both hosts (`Maui/DataGridView.cs:194`, `Uno/DataGridView.cs:116`) backing the "configurable friction" claim (R:88,761; `InertialScroller.cs:9`) is never named in README. | Features exist but are undiscoverable. | Add to README theming/summaries, scrolling, and the new Uno guide. |

---

## (c) Claim-by-claim audit of README.md

Verdicts: **Accurate** / **Inaccurate** / **Partially** / **Unverifiable** (from code).

### Header block, intro, contents (R:1-63)

| README | Claim | Verdict | Evidence / note |
|---|---|---|---|
| R:1-28 | HTML header, relative logo, shields badges, nav anchors, NuGet badges for Maui/SkiaSharp/Core | Partially | Renders on GitHub; breaks on nuget.org (DOC-01). No KumikoUI.Uno badge although package is packable (`Uno.csproj:47-51`). |
| R:3 | ".NET KumikoUI" | Partially | Name used nowhere else (packages are `KumikoUI.*`). |
| R:5, 32 | "zero native controls … pixel-perfect on every platform … identical output across iOS, Android, macOS Catalyst, and Windows" | Partially | Hidden native `Entry`/`TextBox` proxies exist (DOC-06); default font family `"Default"` resolves per platform (`GridState.cs:94-96`, `SkiaDrawingContext.cs:272-292`), so output is not pixel-identical across OSes unless fonts are registered; Uno heads (WASM/Linux) omitted. |
| R:32 | "cells, headers, editors, scrollbars, popups — rendered directly on a SkiaSharp canvas" | Inaccurate | No grid scrollbars (DOC-06). |
| R:36-57 | Contents anchors `#-features-at-a-glance` … | Partially | GitHub slugs for emoji headings; likely different ids on nuget.org (Markdig auto-identifiers) — unverifiable without publishing; avoid emoji headings in the NuGet README. |
| R:61-63 | hero `images/main.png` | Partially | Exists in repo; relative path → not rendered on nuget.org. |

### Features at a Glance (R:67-90)

| README | Claim | Verdict | Evidence |
|---|---|---|---|
| R:71 | "100% SkiaSharp canvas, zero native controls, 60 fps" | Unverifiable / Partially | No fps measurement anywhere; native keyboard proxies (DOC-05/06). |
| R:72 | "Only visible rows/columns rendered — tested with 100K+ rows" | Partially | Virtualization real (`GridLayoutEngine`, `DataGridRenderer.cs:154`); "tested" = manual sample page (`LargeDataPage`), no automated/benchmark test. |
| R:73 | Column types "Text, Numeric, Boolean, Date, ComboBox, Picker, ProgressBar, Image, Template" | Inaccurate | `DataGridColumnType` = Text, Numeric, Boolean, Date, Template, Image, ComboBox, Picker (`DataGridColumn.cs:12-30`). ProgressBar is `ProgressBarCellRenderer` on a Template column (`ICellRenderer.cs:172`). |
| R:74 | Action buttons with `ICommand`/callback, first-tap activation | Accurate | `ActionButtonDefinition.cs:48-63`, `DrawnActionButtons.cs:44` `ActivatesImmediately`, `ICellEditorProvider` (`ICellRenderer.cs:20-29`). |
| R:75 | Fixed, Star, Auto | Accurate | `ColumnSizeMode` (`DataGridColumn.cs:61-69`). |
| R:76-77 | Frozen columns left/right; frozen top N rows | Accurate | `ColumnFreezeMode`, `FreezeMode` (`:48-56,154`); `FrozenRowCount` (`DataGridSource.cs:131`; `Maui/DataGridView.cs:306`). |
| R:78 | Header tap cycles asc/desc/none; multi-column | Accurate | `ToggleSort` (`DataGridSource.cs:801-832`), `ToggleMultiSort` (`:838`). Gesture for multi-sort undocumented. |
| R:79 | Excel-style popup with search, value checklist, sort controls | Accurate | `DrawnFilterPopup.cs:9,21-35` search; OK/Cancel/Clear `:539-546`. |
| R:80 | "Drag-and-drop group panel, expandable groups, nested groups" | Partially / Unverifiable | Panel + chips + expand/collapse + nested groups exist (`GroupDescription.cs`, `GridInputController.cs:453-466`); no header-drag-to-group handler found. |
| R:81 | Aggregates Sum, Avg, Count, Min, Max | Partially | Also `Custom` with `CustomAggregate` (`SummaryDescription.cs:6-20,60`). |
| R:82 | Triggers tap/double-tap/long-press/F2/typing; per-column override | Accurate | `EditTrigger` flags (`EditSession.cs:11-26`); `DataGridColumn.EditTriggers` (`:245`). |
| R:83 | Single/Multiple/Extended; Row or Cell | Partially | Also `None` (`SelectionModel.cs:8-18`). `SelectionUnit` has no XAML property on either host (code only via `Selection.Unit`). |
| R:84 | Arrows, Tab/Shift+Tab, Enter, Home/End, Page Up/Down | Accurate | `GridKey` (`InputEvents.cs:127-177`); handlers `GridInputController.cs:175-180,194,315-333,352`; `SelectionModel.cs:186-251`. |
| R:85 | Handle column or full-row drag reorder | Accurate | `ShowRowDragHandle`, `AllowRowDragDrop` (`GridState.cs:220,243`); `ReorderRow` (`DataGridSource.cs:518`). |
| R:86 | Custom fonts via `SkiaFontRegistrar`, `GridFont` family | Accurate | `SkiaFontRegistrar.cs:49-134`; `SkiaDrawingContext.cs:276`. |
| R:87 | Light / Dark / HighContrast; customisable style object | Accurate | `DataGridThemeMode`, `DataGridTheme.Create*` (`DataGridTheme.cs:8-193`); `DataGridStyle`. |
| R:88 | Physics inertia, configurable friction | Accurate | `ScrollSettings.Friction` (`InertialScroller.cs:9,124-125`); exposed via `ScrollSettings` on both hosts (never named in README). |
| R:89 | INPC and ObservableCollection O(1) updates | Accurate | `DataGridSource.cs:288-296,393-415` (INPC via weak refs), `TryIncrementalUpdate` (`:1039-1054`, Add/Remove of one item, no transforms). |
| R:90 | Column resizing by dragging border | Accurate | `AllowResize` (`DataGridColumn.cs:119`), `HitRegion.HeaderResizeGrip`. |

### Getting Started (R:94-146)

| README | Claim | Verdict | Evidence |
|---|---|---|---|
| R:98-101 | `dotnet add package KumikoUI.Maui` brings Core + SkiaSharp | Accurate | ProjectReferences → package deps (`Maui.csproj:31-34`; nuget.org 0.1.10 deps). |
| R:104-108 | Packages table | Partially | KumikoUI.Uno missing (`Uno.csproj:47-51`). |
| R:114-121 | `UseSkiaKumikoUI()` | Accurate | `DataGridHostingExtensions.cs:14`. |
| R:125-129 | `xmlns:dg="clr-namespace:KumikoUI.Maui;assembly=KumikoUI.Maui"`, `xmlns:core="clr-namespace:KumikoUI.Core.Models;assembly=KumikoUI.Core"` | Accurate | Namespaces exist. Uno equivalents undocumented. |
| R:134-145 | `DataGridView ItemsSource RowHeight HeaderHeight`, `Columns`, `DataGridColumn Header PropertyName Width ColumnType TextAlignment` | Accurate | BindableProperties `Maui/DataGridView.cs:255,437,456,493`; column members `DataGridColumn.cs:80-89,86,175`. |

### Column Types (R:150-194)

| README | Claim | Verdict | Evidence |
|---|---|---|---|
| R:152 | `ColumnType` controls renderer and editor | Accurate | `CellEditorFactory`, renderer dispatch. |
| R:156-161 | Text/Numeric/Boolean/Date/ComboBox/Picker → editors | Accurate | Components exist (`Components/*.cs`); Boolean three-state `DrawnCheckBox.cs:58` `IsThreeState`. |
| R:162 | Image read-only, aspect preserving | Accurate | `ImageCellRenderer` (`ICellRenderer.cs:139`). |
| R:163 | `ProgressBar` column type with `DrawnProgressBar` editor | Inaccurate | Not a `ColumnType`; use `Template` + `ProgressBarCellRenderer` (sample `MainPage.xaml:106`, `MI:89-97`). |
| R:164 | Template: custom `ICellRenderer`, `EditorDescriptor` | Accurate | `DataGridColumn.cs:217-233`. |
| R:169-180 | ComboBox `EditorItemsString`, Numeric `Format="C0"` | Accurate | `DataGridColumn.cs:193,172`. |
| R:182-185 | `ColumnType="ProgressBar"` XAML | Inaccurate | Enum parse failure (XamlC/XAML runtime). |
| R:191-193 | `IsReadOnly`, `AllowTabStop` | Accurate | `DataGridColumn.cs:107,113`. |

### Action Button Columns (R:198-292)

| README | Claim | Verdict | Evidence |
|---|---|---|---|
| R:200-202 | equal-width pills; `ICommand`+`Action`; renderer implements `ICellEditorProvider`; no `CustomEditorFactory` needed | Accurate | `ActionButtonsCellRenderer : ICellRenderer, ICellEditorProvider` (`ICellRenderer.cs:291`), `ActionButtonLayout.ComputeButtonRects` (`:357`). |
| R:204 | Tip `EditTriggers="SingleTap"` | Accurate | Sample `MvvmActionsPage.xaml:150`. |
| R:208-209 | `PropertyName=""` passes row item; `MauiActionButtonDefinition : BindableObject` | Accurate | `ICellRenderer.cs:25`; `MauiActionButtonDefinition.cs:28`. |
| R:211-244 | XAML snippet | Accurate (compiles) | `render:` namespace, `.Buttons` is `IList<IActionButtonDefinition>` (`ICellRenderer.cs:298`), `BackgroundColor="13,110,253"` via `GridColorTypeConverter` (`Color.cs:9,109`). Note: `xmlns:core` not redeclared in this snippet (declared at R:128). |
| R:246 | BindingContext note | Accurate | Matches `MauiActionButtonDefinition.cs:23-26`. |
| R:253-272 | `CustomEditorFactory = (value, bounds) => new DrawnActionButtons { Bounds, RowItem, Buttons = [ … ] }` | Accurate (compiles, C# 12) | `DataGridColumn.cs:217` signature; `DrawnActionButtons.cs:50-57`; `DrawnComponent.Bounds` (`:19`). |
| R:279-284 | Button property table incl. `TextColor` default white, `Action` executed in addition to `Command` | Accurate | `ActionButtonDefinition.cs:36-63`. |
| R:290-292 | `CornerRadius 5f`, `ButtonSpacing 6f`, `CellPadding 4f` | Accurate | `DrawnActionButtons.cs:60-66`, `ICellRenderer.cs:301-307`. |

### Column Sizing (R:296-306)

| README | Claim | Verdict | Evidence |
|---|---|---|---|
| R:302 | Fixed is default | Accurate | `DataGridColumn.cs:98`. |
| R:303 | Auto measures header + visible content, clamps Min/Max | Accurate | `GridLayoutEngine.cs:317-357` `CalculateAutoFitWidth`, `Math.Clamp(..MinWidth, MaxWidth)`. |
| R:304 | Star via `Width="*"` or `"2*"` | Inaccurate | `Width` is `float`; use `SizeMode="Star" StarWeight="2"` (`DataGridColumn.cs:89,98-101`; no parser in any project). |
| R:306 | single layout pass per frame | Accurate | `ComputeColumnWidths` once in `Render()` (`DataGridRenderer.cs:86`). |

### Frozen Columns & Rows (R:310-333)

| README | Claim | Verdict | Evidence |
|---|---|---|---|
| R:318-322 | `IsFrozen="True"`, `FreezeMode="Right"` | Accurate | `DataGridColumn.cs:135-154`. |
| R:325 | 3-pass clip regions | Accurate | `DataGridRenderer.cs:154-219`. |
| R:332 | `FrozenRowCount="2"` | Accurate | `Maui/DataGridView.cs:306`; Uno `:237`. |

### Sorting & Filtering (R:337-357)

| README | Claim | Verdict | Evidence |
|---|---|---|---|
| R:341 | tap cycles Asc→Desc→None; multi-column supported | Accurate | `DataGridSource.cs:805-810`, `ToggleMultiSort`. |
| R:343-347 | multi-sort "via code": set `SortDirection`, `Refresh()` | Partially | Single column works via fallback (`:1121-1127`); multi needs `SortOrder` (`:250-252,1117`). |
| R:351 | filter icon on every header | Partially | Only when `AllowFiltering` (default true, `DataGridColumn.cs:125`) and grid `AllowFiltering` (`Maui/DataGridView.cs:424`). |
| R:353-355 | search, checklist, A→Z/Z→A | Accurate | `DrawnFilterPopup.cs:9-14`. |
| R:357 | applied on close; remove by selecting all | Partially | OK/Cancel/Clear buttons; apply on OK, clear via Clear (`DrawnFilterPopup.cs:13,539-546`). |

### Grouping (R:361-383)

| README | Claim | Verdict | Evidence |
|---|---|---|---|
| R:363 | interactive drag-and-drop group panel | Partially / Unverifiable | See DOC-12. |
| R:366-381 | `AddGroupDescription(new GroupDescription("Department"))`, `ExpandAllGroups`, `CollapseAllGroups`, `ClearGroupDescriptions` | Accurate | `DataGridSource.cs:650-698`; ctor `GroupDescription.cs:35`. |
| R:370 | expanded by default | Accurate | `DefaultGroupExpanded` (`:236`), `GroupHeaderInfo.IsExpanded = true`. |
| R:383 | `AddGroupSummaryRow()` | Accurate (name) | takes `GroupSummaryRow` (`:194`). |

### Summaries (R:387-424)

| README | Claim | Verdict | Evidence |
|---|---|---|---|
| R:389 | pinned rows top/bottom | Accurate (now) | Bottom pinning depends on `PinBottomSummaryRows` (default true, `GridState.cs:236`) — unnamed in README. |
| R:393-407 | `TableSummaryRows` XAML with `TableSummaryRow Name Position Title`, `SummaryColumnDescription PropertyName SummaryType Format Label` | Accurate | `Maui/DataGridView.cs:542-560`; `SummaryDescription.cs:36-96`. |
| R:411-420 | `AddGroupSummaryRow(new SummaryDescription { Columns = {…} })` | Inaccurate | Type is `GroupSummaryRow` (`SummaryDescription.cs:106-123`); `Columns` is `List<SummaryColumnDescription>` so initializer works once the type is fixed. |
| R:424 | Sum · Average · Count · Min · Max | Partially | `Custom` omitted. |

### Editing (R:428-511)

| README | Claim | Verdict | Evidence |
|---|---|---|---|
| R:435-437 | `EditTriggers="DoubleTap,F2Key,Typing"`, `EditTextSelectionMode="SelectAll"`, `DismissKeyboardOnEnter="True"` | Accurate | `Maui/DataGridView.cs:357-411`; sample `MainPage.xaml:34`. |
| R:440-448 | trigger table; flags combinable; DoubleTap default | Accurate | `EditSession.cs:11-26` (default = DoubleTap\|F2Key\|Typing per `:26`, README says "DoubleTap (default)" — Partially: default is the trio). |
| R:452-482 | per-column override; `null` inherits; `None` suppresses | Accurate | `DataGridColumn.cs:237-245`; samples. |
| R:488 | `EditTextSelectionMode="SelectAll"` | Accurate | `EditSession.cs:36`. |
| R:491 | `EditTextSelectionMode="End"` | Inaccurate | member is `CursorAtEnd` (`:38`). |
| R:496-498 | Enter commits + next row; Tab/Shift+Tab; Escape | Accurate | `GridInputController.cs:175-180` (`CommitEdit` → `NavigateDown`), `:194,352`, Tab wrap `:1384`. |
| R:504-511 | `NumericUpDownEditorDescriptor { Min, Max, Step }` | Inaccurate | `Minimum`, `Maximum`, `Step`, `DecimalPlaces`, `Format`, `IsReadOnly` (`EditorDescriptor.cs:33-48`). |

### Selection (R:516-540)

| README | Claim | Verdict | Evidence |
|---|---|---|---|
| R:521 | `GridSelectionMode="Multiple"` | Accurate | `Maui/DataGridView.cs:324`; Uno `:171`. |
| R:524-533 | mode/unit tables | Partially | `None` omitted; no XAML for `SelectionUnit`. |
| R:538-539 | `Selection.SelectedRows` `HashSet<int>`, `SelectedCells` `HashSet<CellPosition>` | Accurate | `Maui/DataGridView.cs:204`; `SelectionModel.cs:73-76`. |

### Keyboard Navigation (R:544-558)

| README | Claim | Verdict | Evidence |
|---|---|---|---|
| R:546 | iOS/macOS `UIKeyCommand`, Android hidden `Entry` + IME diffing, Windows hidden `Entry` | Accurate (MAUI) | `Maui/DataGridView.cs:1355-1452` (`KeyInputResponder`, `UIKeyCommand`), `:91,1043,1204`. Uno path (hidden `TextBox`, WASM) undocumented. |
| R:549-558 | key table incl. Ctrl+Home/End, PageUp/Down, Tab wraps | Accurate | `SelectionModel.cs:218-251`, `GridInputController.cs:315-333,1384`. |

### Row Drag & Drop (R:562-587)

| README | Claim | Verdict | Evidence |
|---|---|---|---|
| R:571-575 | `GridStyle`, `ShowRowDragHandle`, `RowDragHandlePosition = DragHandlePosition.Left` | Accurate | `Maui/DataGridView.cs:213`; `GridState.cs:243-246,278-284`. |
| R:581-585 | `AllowRowDragDrop`, long-press to drag | Accurate / Unverifiable (gesture) | `GridState.cs:220`; `DrawRowDragOverlay` (`DataGridRenderer.cs:294`). |

### Theming & Styling (R:591-645)

| README | Claim | Verdict | Evidence |
|---|---|---|---|
| R:596-598 | `Theme="Light|Dark|HighContrast"`, Light default | Accurate (MAUI, non-bindable) | CLR property `Maui/DataGridView.cs:223-235`; `DataGridThemeMode`; not on Uno. |
| R:603 | every colour/font/size/line width configurable via `DataGridStyle` | Accurate | ~95 members `GridState.cs:53-272`. |
| R:609-615 | `new GridColor(0.95f, …)` | Inaccurate | byte ctor only (`Color.cs:24`); use `new GridColor(242, 242, 247)`. |
| R:613 | `SelectedRowColor` | Inaccurate | `SelectionColor` (`GridState.cs:75`) / `FocusedRowColor` (`:79`). |
| R:615 | `FrozenDividerColor` | Inaccurate | `FrozenColumnDividerColor` (`:118`), `FrozenRowDividerColor` (`:134`). |
| R:618-621 | `CellFontSize`, `HeaderFontSize`, `CellFontBold`, `HeaderFontBold` | Inaccurate | `CellFont = new GridFont("Default", 14)`, `HeaderFont = new GridFont("Default", 14, bold: true)` (`:94-96`). |
| R:624-625 | `ShowHorizontalGridLines`, `ShowVerticalGridLines` | Accurate | `:108-110`. |
| R:636-641 | `CellStyleResolver = (item, column) => …` | Accurate (signature) | `Func<object, DataGridColumn, CellStyle?>` (`:266`). |
| R:639 | `new CellStyle { TextColor = …, Bold = true }` | Inaccurate | no `Bold`; use `Font = new GridFont("Default", 13, bold: true)` (`StyleClasses.cs:18`). |
| R:645 | cascade resolver → column `CellStyle` → grid | Accurate | `CellStyle.Merge` (`StyleClasses.cs:45`); A:125-129. `RowStyleResolver` (`GridState.cs:272`) undocumented in README. |

### Custom Fonts (R:649-742)

| README | Claim | Verdict | Evidence |
|---|---|---|---|
| R:651-655 | registrar semantics; Android CJK/icon-font problem | Accurate | `SkiaFontRegistrar.cs:5-28`; issue #5. |
| R:659 | register in `MauiProgram.cs`; fonts in `Resources/Raw/` | Accurate | sample `MauiProgram.cs:21-26`. |
| R:661-679 | code sample | Inaccurate | `await` in non-async method (CS4033); sample pattern is `.GetAwaiter().GetResult()` with try/catch (`MauiProgram.cs:51-68`). Uno: `KumikoFonts.RegisterFromAppPackageAsync` (`KumikoFonts.cs:55`). |
| R:682 | ownership note | Accurate | `SkiaFontRegistrar.cs:59-63,119-123`. |
| R:688-693 | `new GridFont("NotoSansJP", 14, bold: true)` | Accurate | `Primitives.cs:22`. |
| R:700-717 | `IconFontCellRenderer : ICellRenderer` with `Render(IDrawingContext, GridRect, object?, string, DataGridColumn, DataGridStyle, bool, CellStyle?)`, `DrawTextInRect(text, rect, paint, GridTextAlignment.Center, GridVerticalAlignment.Center)`, `style.SelectionTextColor/CellTextColor`, `GridPaint { Color, Font, IsAntiAlias }` | Accurate | `ICellRenderer.cs:48-56`; `IDrawingContext.cs:63-66`; `GridState.cs:71,77`; `Primitives.cs:34-49`. |
| R:722-732 | column with `CustomCellRenderer`, `TextAlignment = GridTextAlignment.Center` | Accurate | `DataGridColumn.cs:175,233`. |
| R:737-742 | API table (4 methods) | Accurate | `SkiaFontRegistrar.cs:49,72,98,124`; `IsRegistered` (`:111`) omitted (internal-use, fine). |

### Scrolling & Performance (R:746-773)

| README | Claim | Verdict | Evidence |
|---|---|---|---|
| R:750-757 | visible-range formula; `GridLayoutEngine` | Accurate (approx.) | `GridLayoutEngine.GetVisibleRowRange` (RI:160). |
| R:761 | velocity tracker, `InertialScroller`, frame-normalised friction | Accurate | `InertialScroller.cs:9,124-125`. |
| R:765 | "~60 `GridPaint` objects" | Inaccurate | 29 (`PaintCache.cs`). |
| R:769 | SKTypeface/SKFont/SKPaint cached by value keys, disposed at frame end | Accurate | `SkiaDrawingContext.cs:23-25,324-339` (registered/default typefaces excepted). |
| R:773 | O(1) add/remove when no sort/filter/group/summary | Accurate | `DataGridSource.cs:1039-1054` (single-item Add/Remove only — worth stating). |

### Live Data (R:777-817)

| README | Claim | Verdict | Evidence |
|---|---|---|---|
| R:783-790 | ObservableCollection add/remove updates grid | Accurate | `:288-296,381`. |
| R:794-807 | INPC per-cell updates | Accurate | `:393-415` (weak references). |
| R:811-817 | dotted paths; compiled `Expression.Property` accessors cached | Accurate | `BuildAccessor` `:1574-1597`, `_accessors` `:64` (setter path is reflection, `:933-942`). |

### Architecture (R:821-852)

| README | Claim | Verdict | Evidence |
|---|---|---|---|
| R:823-829 | three-box graph; Core net9.0 zero deps; SkiaSharp 3.119.2; Maui net10.0 iOS/Android/MacCatalyst/Windows | Partially | Accurate for the three, but KumikoUI.Uno (`Uno.csproj:14-15`) missing; Windows TFM only on Windows build hosts. |
| R:831 | Core has no MAUI/SkiaSharp dependency; portable backends | Accurate | `Core.csproj` (no PackageReference); `IDrawingContext.cs`. |
| R:837 | `DataGridRenderer` "18-step" | Unverifiable / Inaccurate | DOC-05. |
| R:838-839 | `DataGridSource`, `GridLayoutEngine` | Accurate | — |
| R:840 | `GridHitTester` 14 `HitRegion` types, 3 panes | Inaccurate (count) | 15 (`HitTesting.cs:9-26`). |
| R:841 | `PaintCache` ~60 | Inaccurate | 29. |
| R:842-849 | `GridInputController`, `SelectionModel`, `EditSession`, `CellEditorFactory`, `InertialScroller`, `PopupManager`, `IDrawingContext`, `SkiaDrawingContext` 3-tier cache | Accurate | files exist; `SkiaDrawingContext.cs:23-25`. |
| R:850 | `DataGridView` MAUI control hosts `SKCanvasView` | Partially | Also Uno `DataGridView : Grid` hosting `SKXamlCanvas` (`Uno/DataGridView.cs:38`). |
| R:852 | links to ARCHITECTURE/RENDERING "full 18-step pipeline" | Partially | relative links break on nuget.org; "18-step" see above. |

### Sample Application (R:856-890)

| README | Claim | Verdict | Evidence |
|---|---|---|---|
| R:858 | "four pages" | Inaccurate | six listed; seven XAML pages (`samples/SampleApp.Maui/*.xaml` incl. `EmployeeDetailsPage`); `SampleApp.Uno` (7 pages) not mentioned. |
| R:862-867 | page descriptions | Accurate (MAUI) | pages exist; "performance metrics" = ms timings. |
| R:869-890 | screenshot table | Partially | files exist; HTML + relative paths break on nuget.org; no Uno/WASM/Linux screenshots. |

### Requirements, Contributing, License (R:894-923)

| README | Claim | Verdict | Evidence |
|---|---|---|---|
| R:898-904 | .NET 9 library / .NET 10 MAUI / SkiaSharp 3.119.2 / iOS 15 / Android API 21 / Catalyst 15 / Windows 17763 | Accurate (MAUI) | `Maui.csproj:4,19-23`; `SkiaSharp.csproj:8`. Uno heads absent; Catalyst wording ambiguous. |
| R:910-915 | fork, `dotnet build KumikoUI.sln`, `dotnet test` | Partially | Requires MAUI + Uno workloads and Windows for windows TFMs (DOC-15). No CONTRIBUTING.md. |
| R:917 | link to ARCHITECTURE.md | Partially | stale content (DOC-09); relative link. |
| R:923 | MIT, `LICENSE` | Accurate | `LICENSE:1-3` ("Copyright (c) 2026 Eight-Bot"); `Directory.Build.props:15`. |

---

## (d) XML documentation coverage (CS1591)

Command: `dotnet build src/KumikoUI.SkiaSharp/KumikoUI.SkiaSharp.csproj -c Release --no-incremental` → `98 Warning(s), 0 Error(s)`; `grep -c CS1591` = **192 raw lines** (each warning is printed twice by MSBuild for the referenced project) = **96 unique** `file(line,col)` locations. All 96 are in **KumikoUI.Core**; **KumikoUI.SkiaSharp: 0** (`SkiaDrawingContext.cs`, `SkiaFontRegistrar.cs` fully documented). Other warnings: 2× CS8603, 2× CS8601 (nullability) — not doc-related. Maui/Uno not built (workloads); heuristic scan (public member not preceded by `///`): **Maui ≤ 12** (`DataGridView.cs:84` ctor, `:259` `ItemsSource`, `:1355-1357` events, `:1399-1452` `KeyInputResponder` members — several may be on a non-public nested type), **Uno 0**.

Top offending files (unique warnings):

| Count | File |
|---|---|
| 22 | src/KumikoUI.Core/Input/HitTesting.cs (entire `HitRegion` enum, `HitTestResult`) |
| 10 | src/KumikoUI.Core/Models/GroupDescription.cs (ctors, `GroupExpandingEventArgs`, `GroupCollapsedEventArgs`) |
| 8 | src/KumikoUI.Core/Models/SelectionModel.cs (`CellPosition` ctor/Equals/operators/ToString, `SelectionChangedEventArgs` ctor) |
| 8 | src/KumikoUI.Core/Models/FilterDescription.cs (`FilterDescription(DataGridColumn)`, `FilterChangingEventArgs`, `FilterChangedEventArgs`) |
| 7 | src/KumikoUI.Core/Rendering/Color.cs (`GridColor(byte,byte,byte,byte)`, Equals/operators/ToString) |
| 6 | src/KumikoUI.Core/Rendering/Primitives.cs (`GridFont()`, `GridFont(string,float,bool,bool)`, `GridRect`, `GridSize`, `GridPoint`, `GridFontMetrics` ctors) |
| 6 | src/KumikoUI.Core/Models/SummaryDescription.cs (`SummaryColumnDescription`, `TableSummaryRow`, `GroupSummaryRow` ctors) |
| 6 | src/KumikoUI.Core/Editing/EditSession.cs (`CellValidationResult.Valid/Error/Warning`, `CellBeginEdit/CellEndEdit/CellValueChangedEventArgs` ctors) |
| 5 | src/KumikoUI.Core/Input/GridInputController.cs (events `ColumnResized`, `ColumnReordered`, `RowReordered`, `RowTapped`, `RowDoubleTapped`) |
| 4 | src/KumikoUI.Core/Rendering/ICellRenderer.cs (`Render` on Text/Boolean/Image/ProgressBar renderers) |
| 3 | src/KumikoUI.Core/Models/DataGridSource.cs (`SortColumnsChangingEventArgs`/`SortColumnsChangedEventArgs` ctors, `DataChanged` event) |
| 3 | src/KumikoUI.Core/Components/DrawnComponent.cs (`ComponentValueChangedEventArgs`) |
| 2 | src/KumikoUI.Core/Editing/EditorDescriptor.cs (`CreateEditor` overrides) |
| 2 | src/KumikoUI.Core/Components/DrawnTextBox.cs (`OnGotFocus`, `OnLostFocus`) |
| 2 | src/KumikoUI.Core/Components/DrawnDatePicker.cs (ctor, `OnLostFocus`) |
| 1 | src/KumikoUI.Core/DataGridRenderer.cs (ctor) |
| 1 | src/KumikoUI.Core/Components/DrawnComboBox.cs (`OnLostFocus`) |

Recommendation: fix, then enforce with `<WarningsAsErrors>CS1591</WarningsAsErrors>` (or `TreatWarningsAsErrors` with the two nullability warnings fixed first).

---

## §4 Missing documentation for 1.0 — ranked

1. **Per-package NuGet README** (Core / SkiaSharp / Maui / Uno) — blocks a presentable nuget.org page (DOC-01).
2. **Uno getting-started** — package, `UseKumikoUI`, XAML namespaces, `KumikoFonts`, per-head caveats (WASM keyboard proxy, Windows-only build), no `MauiActionButtonDefinition` equivalent (DOC-03/04).
3. **Supported platforms & minimum OS matrix** for both hosts incl. WebAssembly/Linux desktop, build-host requirements, SkiaSharp version pin, verification status per head (DOC-14; `docs/uno/07-tests.md:82-113` says only Desktop is hand-verified).
4. **CHANGELOG.md + versioning/support policy** — tags are 0.1.x, csprojs say 1.0.0; no breaking-change policy, no statement that the four packages version in lockstep (`docs/uno/08-ci-cd-packaging.md:84-89` implies it).
5. **Theming reference** — full `DataGridStyle` table (~95 members, `GridState.cs:53-272`), `CellStyle`/`RowStyle`, `CellStyleResolver`/`RowStyleResolver`, `DataGridTheme.Create*`, `PinBottomSummaryRows`; README currently shows 13 members, 7 wrong (DOC-02).
6. **Troubleshooting / FAQ** seeded from closed issues: #5 (Japanese text garbled on Android → `SkiaFontRegistrar`), #2 (bind a button in a Template column to `ICommand` → action buttons), #6 (per-column edit triggers); plus "no scrollbars", "Windows TFM only builds on Windows", "star sizing is `SizeMode`", "`EditTextSelectionMode.CursorAtEnd`".
7. **CONTRIBUTING.md** (+ fix PR #9's link `EightBot/…/.github/CONTRIBUTING.md`), **SECURITY.md** (disclosure path), **PULL_REQUEST_TEMPLATE.md**, YAML issue forms (DOC-15/16).
8. **Accessibility statement** — only `SemanticProperties.SetDescription(this, "Data grid")` / `GridDescription` exist (`Maui/DataGridView.cs:87-88,472-487`); cells/headers are not exposed to screen readers; state this explicitly and list keyboard support as the mitigation.
9. **Performance guidance** — row/column limits actually exercised, when `TryIncrementalUpdate` applies (single-item Add/Remove, no transforms), "assign a new collection, don't add 100K items one-by-one" (`LargeDataPage.xaml.cs:42-47`), `ScrollSettings`, auto-size cost, image columns.
10. **API reference site** (DocFX from the already-generated XML docs) — would also expose the 96 undocumented members.
11. **Custom column / renderer / editor cookbook** — `ICellRenderer`, `ICellEditorProvider`, `EditorDescriptor`, `DrawnComponent` lifecycle (currently only in `.github/prompts`, which end users never see).
12. **Samples README** for `samples/` (both apps, how to run each head: `docs/uno/06-sample-app.md:103-141` has the commands) and **Uno/WASM/Linux screenshots**.

---

## §5 Stale / inconsistent statements (consolidated)

| Location | Statement | Reality |
|---|---|---|
| U:7-8 | "No `KumikoUI.Uno` code exists yet … Implement only after this plan set is reviewed." | `src/KumikoUI.Uno/` has 8 files; `tests/KumikoUI.Uno.Tests`; CI/publish jobs exist (`ci.yml:111-164`, `publish.yml:173-234`). |
| U:73-74,78 | TFMs "representative … confirm after scaffolding", `net9.0-windows10.0.xxxxx.0` | `KumikoUI.Uno.csproj:14-15` is final. |
| CI:5,24; AI:9,15-32; A:3,7-37; RD:8-12 | ".NET MAUI" DataGrid, "three-layer", "Core → SkiaSharp → Maui" | Four projects, two hosts. |
| AI:36-44, A:22-25 | paths list only MAUI sample and Core tests | `samples/SampleApp.Uno`, `tests/KumikoUI.Uno.Tests` exist. |
| AI:59 | `KumikoUI.Maui` → `DataGridHostingExtensions` | Type is `KumikoUIHostingExtensions` in file `DataGridHostingExtensions.cs:8`. |
| AI:66 | "All public API members have `///`" | 96 CS1591 in Core. |
| A:12 | `Data/ (future)` | No folder; data lives in `Models/DataGridSource.cs`. |
| A:20-21 | `Hosting/`, `Platforms/` | Neither exists. |
| A:50,150; R:840 | 14 `HitRegion` values with listed names | 15 values, different names. |
| A:51,114; R:765,841; RD:21,134 | ~60 paints | 29. |
| A:91-100 | Drawn component table | Missing `DrawnActionButtons`. |
| RD:5-43, R:837,852 | 18 numbered steps | No numbering in code; different pass order. |
| RD:103,117-123,192,200 | `Draw(context, cellRect, value, paint)`, `TextColumn…`, compiled setter, popup `Draw(context, rect)` | `Render(8 args)`, enum values, reflection setter, `OnDraw(ctx)`. |
| RI:104-121 | render-loop method names | Mostly non-existent (DOC-11). |
| CF:107,115-124 | `Source.GroupDescriptions.Add`, `<dg:DataGridView.GroupSummaryRows>` | No such members. |
| R:32; RI:116 | canvas scrollbars | None in the grid. |
| R:73,163,182-185 | `ProgressBar` column type | Template + `ProgressBarCellRenderer`. |
| R:858 | "four pages" | seven (MAUI) + seven (Uno). |
| R:896-904, R:20 | platforms | WebAssembly/Linux desktop/Uno heads missing. |
| BR:26-35 | Browser/Smartphone fields | Not applicable to a .NET grid library. |
| PR #9 body | "I have read the CONTRIBUTING doc" → `github.com/EightBot/KumikoUI/blob/main/.github/CONTRIBUTING.md` | Org is `TheEightBot`; file does not exist. |
| csproj `<Version>1.0.0</Version>` vs tags `v0.1.1…v0.1.10` | version source of truth | publish.yml overrides via tag; README/CHANGELOG never state the scheme. |

---

## §6 API naming consistency visible from docs

- **File ≠ type**: `DataGridTheme.cs` → `DataGridThemeMode` + static `DataGridTheme`; `DataGridStyle` (the type users configure most) is in `GridState.cs:53` beside `ScrollState` and `DragHandlePosition`; `StyleClasses.cs` → `CellStyle`, `RowStyle`; `Color.cs` → `GridColor`, `GridColorTypeConverter`; `Primitives.cs` → `GridFont`, `GridPaint`, `PaintStyle`, `GridTextAlignment`, `GridVerticalAlignment`, `GridRect`, `GridSize`, `GridPoint`, `GridFontMetrics`; `HitTesting.cs` → `HitRegion`, `HitTestResult`, `GridHitTester`; `InputEvents.cs` → `InputAction`, `InputModifiers`, `PointerButton`, `GridPointerEventArgs`, `GridKeyEventArgs`, `GridKey`; `SummaryDescription.cs` → no `SummaryDescription` type (README assumes one); `DataGridHostingExtensions.cs` → `KumikoUIHostingExtensions`. Docs and prompts repeatedly point at files to "add to", so this mismatch costs contributors.
- **Prefix families**: `DataGrid*` (View, Column, ColumnType, Source, Style, Theme, ThemeMode, Renderer), `Grid*` (Color, Font, Paint, Rect, Size, Point, Key, PointerEventArgs, KeyEventArgs, LayoutEngine, HitTester, InputController, TextAlignment, VerticalAlignment, FontMetrics), `Drawn*` (components), `Kumiko*` (`KumikoFonts`, `KumikoUIHostingExtensions`), and unprefixed (`PaintStyle`, `SelectionMode`, `SelectionUnit`, `SortDirection`, `SummaryType`, `SummaryPosition`, `CheckState`, `ValidationState`, `ValidationSeverity`, `DragHandlePosition`, `PopupPlacement`, `EditTrigger`, `EditTextSelectionMode`). `SelectionMode` collides with `Microsoft.Maui.Controls.SelectionMode`, which is presumably why the view property is `GridSelectionMode` — but `SelectionUnit`/`SortDirection` keep bare names.
- **`RowTappedEventArgs2`** (`GridInputController.cs:1477`) is public API on both hosts (`RowTapped`, `RowDoubleTapped`). A "2" suffix must not ship in 1.0.
- **Hosting**: `UseSkiaKumikoUI()` (MAUI) vs `UseKumikoUI()` (Uno, a no-op `IHostBuilder` pass-through). Pick one name (or document the asymmetry and why).
- **Freeze API**: `IsFrozen`, `IsFrozenRight`, `FreezeMode` (three ways to say one thing; `DataGridColumn.cs:135-154`).
- **Enum spelling vs docs**: `EditTextSelectionMode.CursorAtEnd` (README says `End`); `ColumnFreezeMode`/`ColumnSizeMode` enums vs `FreezeMode`/`SizeMode` properties (fine, but docs should show the enum names once).
- **Product name**: ".NET KumikoUI" (R:3), "the Kumiko" (RD:3), "KumikoUI control" (Core/SkiaSharp/Maui csproj descriptions read "fully custom-drawn KumikoUI control" — the word "DataGrid" is missing from the MAUI package description), "KumikoUI DataGrid control" (Uno csproj). Settle on "KumikoUI DataGrid".
- **`DataGridView`** exists in both `KumikoUI.Maui` and `KumikoUI.Uno` — fine, but every doc snippet must say which host it targets.

---

## (e) Questions for the maintainer

1. Is 1.0 meant to ship KumikoUI.Uno alongside the other three, or will Uno be a 1.x addition? The answer decides whether README becomes host-neutral now or Uno gets a separate "preview" page. `docs/uno/07-tests.md:82-113` records that only the Desktop head has been exercised by hand — is that acceptable for a 1.0 label on the Uno package?
2. Are "60 fps" and "tested with 100K+ rows" (R:71-72) intended as measured claims? If yes, which device/head/row count, and can the measurement be checked in (BenchmarkDotNet or an fps overlay in `LargeDataPage`)? If no, may they be softened?
3. Do you want one rich GitHub README plus four short NuGet READMEs (recommended), or a single plain-Markdown README with absolute image URLs? Either way, should `Directory.Build.props:40` stop injecting the root README into every package?
4. Which naming changes are acceptable before 1.0: `RowTappedEventArgs2` → `RowTappedEventArgs`; `UseSkiaKumikoUI` vs `UseKumikoUI`; moving `DataGridStyle` to its own file; a single prefix policy? After 1.0 these are breaking.
5. Is header drag-to-group-panel (R:80,363 "drag-and-drop group panel") implemented somewhere I missed, or is the panel click/remove-chips only? Likewise, are canvas scrollbars (R:32) planned for 1.0 or should the claim go?
6. Should `CS1591` become an error (`WarningsAsErrors`) once the 96 members are documented, so coverage can't regress?
7. What is the version/release policy? Tags are `v0.1.x`, csprojs say `1.0.0`, publish is tag-driven; do all four packages move in lockstep, and will there be a CHANGELOG from 1.0.0 on?
8. Should the README document the accessibility position explicitly (canvas-drawn → no per-cell UIA/AX tree; `GridDescription` + keyboard nav only)?
9. PR #9's description links to `.github/CONTRIBUTING.md` under org `EightBot` — is there a PR template or CONTRIBUTING draft elsewhere that should be added to the repo?
10. The `.github/instructions` and `prompts` are the declared AI-assisted workflow; do you want them cross-checked in CI against public declarations (cheap script), given they already reference ~15 non-existent members?

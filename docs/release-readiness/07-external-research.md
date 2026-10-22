# KumikoUI 1.0 — External Research (primary sources)

Research date: **2026-10-22**. Every factual claim below is followed by the URL it was taken from. Dates shown as "last updated" are what nuget.org displays. Anything I could not verify from a primary page is marked **[UNVERIFIED]** and collected again in the "Could not verify" section.

Legend used in tables: ✓ = documented on the cited official page; ◐ = partial / documented with caveats; ✗ = not found on the fetched official pages (absence of evidence, not proof of absence unless stated); ? = not checked.

---

## 0. Baseline: what KumikoUI claims and what the source actually contains

README "Features at a Glance" (lines 67–90 of `/Users/mstonis/Developer/KumikoUI/README.md`) claims: 100% SkiaSharp rendering; virtual scrolling (100K+ rows); column types Text, Numeric, Boolean, Date, ComboBox, Picker, ProgressBar, Image, Template; action-button columns; Fixed/Star/Auto column sizing; frozen columns (left/right) and frozen rows; multi-column sorting; Excel-style filter popup; drag-and-drop group panel with nested groups; table- and group-level summaries; editing with configurable triggers; Single/Multiple/Extended selection with Row/Cell unit; keyboard navigation; row drag & drop; custom fonts; Light/Dark/HighContrast themes; momentum scrolling; live `INotifyPropertyChanged`/`ObservableCollection` updates; column resizing.

Source facts that change the gap analysis (local repo, `src/KumikoUI.Core`):
- **Column header drag-reorder already exists but is undocumented**: `DataGridColumn.AllowReorder` (default `true`, `Models/DataGridColumn.cs:122`), header-drag detection in `Input/GridInputController.cs:577`, `ColumnReordered` event (`:70`), and `DataGridSource.ReorderColumn(int,int)` (`Models/DataGridSource.cs:501`). The README feature table does not mention it (grep for "reorder" only hits row drag & drop).
- **Validation plumbing exists but is undocumented**: `Editing/EditSession.cs:498–514` checks `IDataErrorInfo` and `INotifyDataErrorInfo`; `Components/DrawnTextBox.cs` has `InputValidator`, `ValidationState`, `ValidationMessage` and error/warning border colours. No README section describes validation UI.
- **Column visibility exists as an API** (`DataGridColumn.IsVisible` is honoured by `DataGridRenderer.cs:1696` and hit-testing), but there is no column-chooser UI.
- **Conditional formatting exists**: `DataGridStyle.CellStyleResolver` (README lines 632–645).
- **Row height is uniform** (`style.RowHeight` used in the virtual-scroll formula, `DataGridRenderer.cs:243–257`); there is no auto/variable row height.
- **Built-in UI strings are hard-coded English** ("Search...", "Select All", "OK", "Cancel", "Clear" in `Components/DrawnFilterPopup.cs:329–556`), so there is no localization of built-in strings.
- No accessibility code, export, clipboard, paging, load-more, pull-to-refresh, swipe, context menu, tooltip, row-details, tree, stacked-header, cell-merge, hyperlink or text-wrap APIs were found by keyword scan (grep over `src/KumikoUI.Core` for those terms returned nothing).

---

## 1. Feature parity matrix

### 1.1 Status of each comparison grid (important context)

| Product | Status found on primary source |
|---|---|
| Syncfusion .NET MAUI SfDataGrid | Active; overview lists 8 feature groups; Globalization table rates localization, RTL and keyboard navigation "fully supported", screen reader "partial". https://help.syncfusion.com/maui/datagrid/overview |
| Telerik UI for .NET MAUI DataGrid | Active; docs moved to telerik.com (301 from docs.telerik.com). Supports Android, iOS, macOS, Windows; keyboard navigation "available on WinUI and Mac Catalyst". https://www.telerik.com/maui-ui/documentation/controls/datagrid/overview |
| DevExpress .NET MAUI DataGridView | Active, docs at V26.1 (NuGet 26.1.5). https://docs.devexpress.com/MAUI/DevExpress.Maui.DataGrid.DataGridView |
| Avalonia `Avalonia.Controls.DataGrid` | **Deprecated.** Docs banner: "DataGrid is deprecated! For read-only tabular data, we recommend using TableView. For advanced editing, we recommend using TreeDataGrid." https://docs.avaloniaui.net/docs/reference/controls/datagrid/ — repo README: "`DataGrid` is deprecated and only receives bug fixes." https://github.com/AvaloniaUI/Avalonia.Controls.DataGrid — TableView is read-only, in core `Avalonia.Controls` from Avalonia 12.1, virtualizes rows but "does **not** virtualize columns", "does not provide in-place editing". https://docs.avaloniaui.net/controls/data-display/structured-data/tableview — TreeDataGrid OSS repo "has been archived and is no longer actively maintained… moving to Avalonia Accelerate as a commercially supported component." https://github.com/AvaloniaUI/Avalonia.Controls.TreeDataGrid |
| Windows Community Toolkit DataGrid | **Deprecated / archived.** "not available in the current version of the Windows Community Toolkit… The DataGrid control has been deprecated in favor of alternatives. For new development, we recommend using the `DataTable` or `WinUI.TableView` component." https://learn.microsoft.com/en-us/dotnet/communitytoolkit/archive/windows/datagrid — last NuGet release `CommunityToolkit.WinUI.UI.Controls.DataGrid` 7.1.2, 2021-11-18. https://www.nuget.org/packages/CommunityToolkit.WinUI.UI.Controls.DataGrid |
| Uno Platform | No first-party DataGrid. Uno ships a port of the WCT **7.x** DataGrid (`Uno.CommunityToolkit.WinUI.UI.Controls.DataGrid`, 7.1.200 for non-Windows heads; Windows uses Microsoft's 7.1.2). https://platform.uno/docs/articles/uno-community-toolkit-v7.html — the community **WinUI.TableView** (MIT) "is a lightweight and fast data grid control made for WinUI apps and now supports the Uno Platform." https://github.com/w-ahmad/WinUI.TableView |
| Syncfusion WinUI SfDataGrid | Active; 14 feature bullets on overview. https://help.syncfusion.com/winui/datagrid/overview |

### 1.2 Matrix (K = KumikoUI; SF-M = Syncfusion MAUI; TK = Telerik MAUI; DX = DevExpress MAUI; AV = Avalonia DataGrid; WCT = Windows Community Toolkit DataGrid 7.1; UNO = best available on Uno (WCT port / WinUI.TableView); SF-W = Syncfusion WinUI)

| Feature | K | SF-M | TK | DX | AV | WCT | UNO | SF-W | Evidence (competitors) |
|---|---|---|---|---|---|---|---|---|---|
| Virtualization | ✓ | ✓ | ✓ | ✓ | ? | ? | ◐ | ✓ | SF-M "Performance and virtualization" (overview); TK "High Performance & UI Virtualization" (https://www.telerik.com/maui-ui/datagrid); DX "infinite scrolling" (class page); SF-W "Data virtualization" (https://www.syncfusion.com/winui-controls/datagrid); TableView virtualizes rows only (tableview page) |
| Column types (text/number/bool/date/combo/image/template) | ✓ | ✓ | ✓ | ✓ | ◐ | ◐ | ◐ | ✓ | SF-M "text, numbers, and dates to checkboxes, images, dropdowns, pickers, and custom templates" (overview); TK "text, boolean, numeric, combo box, date, time, and template" + SKIAColumn (overview, product); DX Text, MultilineText, Number, Date, Time, CheckBox, ComboBox, Picker, AutoComplete, Image, Template (class page); AV Text/CheckBox/Template only (datagrid page); WCT text/checkbox/combobox/template (WCT page); SF-W "combo box, checkbox, date, and text box" (product) |
| Multi-column sorting | ✓ | ✓ | ✓ | ✓ | ◐ | ◐ | ◐ | ✓ | DX `SortMode` "multiple columns or only one" (members page); SF-W "sort one or more columns" (overview); TK sort descriptor collection (overview); AV/WCT header-click sort documented, multi not stated |
| Filtering UI | ✓ | ◐ | ✓ | ✓ | ✗ | ◐ | ✓ | ✓ | TK "built-in UI" (overview); DX `ShowAutoFilterRow`, `ShowFilterIcon`, `FilteringUITemplate` (members); SF-W "Excel-inspired filtering UI" (product); WinUI.TableView "Excel-like Column Filter" (repo); WCT filtering via LINQ (WCT page); SF-M overview says "narrowing" only — UI not verified |
| Grouping (drag-to-group UI) | ✓ | ◐ | ✓ | ◐ | ✗ | ◐ | ◐ | ✓ | TK "Grouping UI" (product); SF-W "group drop area" (product); SF-M grouping listed, drag UI not verified; DX grouping via API (members); WinUI.TableView grouping "Windows only" (repo) |
| Summaries / aggregates | ✓ | ✓ | ✓ | ✓ | ✗ | ✗ | ✗ | ✓ | SF-M "totals"; TK "Aggregates", "Column and Group Footers"; DX "Summaries"; SF-W "Summaries" |
| Editing | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | all cited overview pages; AV via `IsReadOnly` false (datagrid page); TableView: no editing — WinUI.TableView: editing by double-tap (repo) |
| Validation (error UI) | ◐ (undocumented) | ✓ | ✓ | ✓ | ? | ✓ | ✓ | ✓ | SF-M `ValidationMode` InEdit/InView with error icons (https://help.syncfusion.com/maui/datagrid/data-validation); TK validation command with `INotifyDataErrorInfo` (https://www.telerik.com/maui-ui/documentation/controls/datagrid/commands/validation); DX "Data Validation" topic (https://docs.devexpress.com/MAUI/403255/data-grid); WCT "Editing and input validation" guide (WCT page); SF-W "Built-in data error indication and validation" (product) |
| Selection modes (single/multi/extended; row/cell) | ✓ | ✓ | ✓ | ◐ | ? | ✓ | ✓ | ✓ | TK "Single or multiple… cells or rows" (overview); WCT `Single`/`Extended` (keyboard page); DX `SelectionMode` (members); SF-W rows or cells (overview); WinUI.TableView cell selection (repo) |
| Keyboard navigation | ✓ | ◐ | ◐ | ✗ | ? | ✓ | ✓ | ✓ | SF-M fully supported per Globalization table but product page scopes it to Windows; TK "WinUI and Mac Catalyst" (overview); WCT full key table (keyboard page); DX no keyboard property found (members) |
| Row drag & drop | ✓ | ✓ | ✗ | ✓ | ✗ | ✗ | ✗ | ✓ | SF-M `AllowDraggingRow` (https://help.syncfusion.com/maui/datagrid/row-drag-and-drop); DX `AllowDragDropRows` (https://docs.devexpress.com/MAUI/404493/data-grid/drag-and-drop); SF-W "Row drag and drop" (product) |
| Frozen columns / rows | ✓ | ✓ | ✓ | ◐ | ? | ? | ? | ✓ | SF-M "frozen panes"; TK "Frozen/Pinned Columns Support" (product); DX fixed columns (`FixedColumnSeparatorThickness`, `FixedStyle`) (members); SF-W `FrozenColumnsCount`/`FrozenFooterColumnsCount` (https://help.syncfusion.com/winui/datagrid/columns) |
| Column resizing | ✓ | ✓ | ✓ | ✗ | ✓ | ✓ | ✓ | ✓ | SF-M "Resizing" (product); TK "Column Resizing" (product); AV `CanUserResizeColumns`; WCT `CanUserResizeColumns` (keyboard page); TableView `CanUserResizeColumns`; SF-W `AllowResizingColumns`; DX: no resizing property found in members list **[UNVERIFIED]** |
| **Column reordering (drag headers)** | ◐ (implemented, undocumented) | ✓ | ✓ | ✗ | ✓ | ✓ | ✓ | ✓ | SF-M `AllowDraggingColumn` (https://help.syncfusion.com/maui/datagrid/column-drag-and-drop); TK `CanUserReorderColumns` default true (https://www.telerik.com/maui-ui/documentation/controls/datagrid/columns/reordering); AV `CanUserReorderColumns` (default false); WCT `CanUserReorderColumns` (keyboard page); WinUI.TableView "drag and rearrange columns at runtime"; SF-W `AllowDraggingColumns` (columns page); DX not found in members/drag-drop pages |
| Column chooser / visibility UI | ◐ (`IsVisible` API only) | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ◐ | SF-W `GridColumn.Hidden` + hidden-column resize indicator (columns page); no chooser UI found for any product; a 2022 Syncfusion forum reply said MAUI lacked a column chooser **[UNVERIFIED now]** |
| Row details / master-detail | ✗ | ✓ | ✓ | ✗ | ? | ✓ | ✓ | ✓ | SF-M "master-detail layouts" (overview); TK "Row Details" (product); WCT "drop-down details section" (WCT page); SF-W "Master-details view" (product); DX has "Multi-Row Layout" instead (data-grid topic) |
| Hierarchical / tree grid | ✗ | ✗ | ◐ (separate TreeDataGrid product) | ✗ | ◐ (TreeDataGrid, now commercial) | ✗ | ✗ | ✗ | Telerik treedatagrid docs exist (search result https://www.telerik.com/maui-ui/documentation/controls/treedatagrid/columns/reordering); Avalonia TreeDataGrid archived/commercial (repo) |
| Paging | ✗ | ✓ | ✓ | ✗ | ✗ | ✗ | ✗ | ✗ | SF-M "Paging" (product); TK "Uses the separate DataPager control" (overview) |
| Incremental / load-more | ✗ | ✓ | ✓ | ✓ | ✗ | ✗ | ✗ | ✗ | SF-M "Loading more items"; TK "Load on Demand"; DX `IsLoadMoreEnabled`, `LoadMoreCommand` (members) |
| Export (CSV / Excel / PDF) | ✗ | ✓ (Excel, PDF) | ✗ | ✓ (PDF, XLSX, XLS, DOCX, RTF, CSV, text, HTML, MHT, image) | ✗ | ✗ | ◐ (TableView CSV) | ◐ (Excel) | SF-M "Exports to Excel and PDF" (overview); DX export extension methods (class page); SF-W "Excel exporting" (product); WinUI.TableView "export data to CSV" (repo) |
| Clipboard copy / paste | ✗ | ✓ | ✗ | ✗ | ✗ | ? | ✓ | ✓ | SF-M "uses the clipboard" (overview); SF-W "Clipboard operations (cut, copy, and paste)" (product); WinUI.TableView copy rows/cells with or without headers (repo); WCT `ClipboardCopyMode` **[UNVERIFIED]** |
| Printing | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | not found on any fetched page |
| Cell merging | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✓ | SF-W "Merge cells" (product) |
| Conditional formatting | ✓ (`CellStyleResolver`) | ✓ | ✓ | ◐ | ? | ✓ | ? | ✓ | SF-M "rule-based formatting", "Conditional styling"; TK "style selectors"; DX appearance types / `CustomCellAppearanceEventArgs`; WCT style/template properties; SF-W "Conditional formatting" |
| Row / cell templates | ✓ (Template column) | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | all cited pages (template columns; TK row details template; AV `CellTemplate`/`CellEditingTemplate`) |
| Header templates | ? | ? | ? | ? | ✓ (`HeaderTemplate`) | ? | ? | ? | AV datagrid page lists `HeaderTemplate`; others not checked |
| Unbound / computed columns | ✗ | ✓ | ✗ | ✗ | ✗ | ✗ | ✗ | ✓ | SF-M "unbound rows and columns" (overview); SF-W "unbound columns" (overview) |
| DataTable source | ✗ | ✓ | ✓ | ? | ✗ | ✗ | ✗ | ? | SF-M "collections, tables" (overview); TK "DataTable Support" (product) |
| Context menu | ✗ | ✓ | ✗ | ✗ | ✗ | ✗ | ✗ | ✓ | SF-M context menu page (https://help.syncfusion.com/maui/datagrid/context-menu); SF-W "Context menu" (product) |
| Tooltips | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | not found on any fetched page |
| Multi-select checkbox column | ✗ | ? | ? | ? | ? | ? | ? | ? | not verified for any product |
| Swipe actions (mobile) | ✗ | ✓ | ✗ | ✓ | ✗ | ✗ | ✗ | ✗ | SF-M "Swiping"; DX swipe items (members) |
| Pull-to-refresh | ✗ | ✓ | ✗ | ✓ | ✗ | ✗ | ✗ | ✗ | SF-M "Pull to refresh"; DX `IsPullToRefreshEnabled` |
| Search / find (grid-wide) | ◐ (per-column search in filter popup) | ? | ✓ | ✗ | ✗ | ✗ | ✗ | ✗ | TK "Search as You Type" (product/overview) |
| Autoscroll during drag | ? | ✓ (column drag `CanAutoScroll`) | ? | ? | ? | ? | ? | ? | SF-M column-drag page |
| RTL | ✗ | ✓ | ✗ | ✗ | ? | ? | ? | ✓ | SF-M Globalization table; SF-W "Right to left (RTL)" (product) |
| Localization of built-in strings | ✗ (hard-coded) | ✓ | ✓ | ✓ | ? | ? | ✓ | ✓ | SF-M localization page (https://help.syncfusion.com/maui/datagrid/localization); TK "Localization" (overview); DX `GridLocalizer`/`GridStringId` (class page); WinUI.TableView "multiple languages" (repo); SF-W "Localization" |
| Accessibility (screen reader) | ✗ | ◐ ("partial") | ✗ | ✗ | ? | ✓ (Narrator) | ✓ (WCT port) | ✗ | SF-M Globalization table; WCT "built-in support for Narrator and Touch Narrator" (WCT page) |
| Theming | ✓ | ✓ | ✓ | ◐ | ✓ | ✓ | ✓ | ✓ | SF-M "theme support"; TK "built-in themes"; DX appearance; AV Fluent theme include; SF-W light/dark |
| Animations / momentum | ✓ | ? | ? | ? | ? | ? | ? | ? | not checked for competitors |
| Column auto-size to content | ✓ | ✓ | ? | ? | ✓ | ? | ? | ✓ | SF-M "Column sizing"; AV auto-size when no width set; SF-W `ColumnWidthMode.Auto` |
| Stacked headers | ✗ | ✓ | ✗ | ◐ (multi-row layout) | ✗ | ✗ | ✗ | ✓ | SF-M "Stacked headers"; SF-W "Stacked Headers"; DX "Multi-Row Layout" |
| Row/column spanning | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ◐ (merge) | SF-W merge cells |
| Auto / variable row height | ✗ (uniform) | ✓ | ✓ | ◐ | ? | ? | ? | ✓ | SF-M "Row height"; TK "sized automatically to content by default" (overview); SF-W "auto fit the row height"; DX `GroupRowHeight` only |
| Text wrapping | ✗ | ? | ? | ◐ (MultilineTextColumn) | ? | ? | ? | ? | DX column list |
| Image columns | ✓ | ✓ | ✗ | ✓ | ✗ | ✗ | ✗ | ? | SF-M overview; DX `ImageColumn` |
| Hyperlink columns | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | not found on fetched pages **[UNVERIFIED for Syncfusion]** |
| Date/time columns | ✓ | ✓ | ✓ | ✓ | ✗ | ✗ | ✗ | ✓ | as above |
| Enum / combobox columns | ✓ | ✓ | ✓ | ✓ | ✗ | ✓ | ✓ | ✓ | as above |
| Numeric formatting | ✓ (Numeric column) | ✓ | ✓ | ✓ | ✗ | ✗ | ✗ | ✓ | as above (format-string API not verified for K) |

### 1.3 Ranked: features most commonly present across competitors that KumikoUI lacks (or does not document)

Count = number of the 7 comparison products where the feature is documented on the fetched official page (◐ counted as ½).

1. **Column reordering by dragging headers — 6/7** (all but DevExpress, where it was not found). KumikoUI has the code (`AllowReorder`, `ColumnReordered`, `ReorderColumn`) but the README does not document it. Action: document, test, and expose on both hosts (API + event). Sources: Syncfusion MAUI column-drag page, Telerik reordering page, Avalonia datagrid page, WCT keyboard page, WinUI.TableView README, Syncfusion WinUI columns page (URLs above).
2. **Validation UI (error indication tied to `INotifyDataErrorInfo`/`IDataErrorInfo`) — 6/7.** KumikoUI's `EditSession` already consults both interfaces; what is missing is a documented, visible error-state contract (error icon/tooltip/border per cell, commit blocking). Sources: Syncfusion MAUI data-validation page, Telerik validation page, DevExpress data-grid topic, WCT page, Syncfusion WinUI product page.
3. **Row details / master-detail — 5/7** (Syncfusion MAUI, Telerik, WCT, Uno-WCT, Syncfusion WinUI).
4. **Localization of built-in strings — 5/7** (Syncfusion MAUI, Telerik, DevExpress, WinUI.TableView, Syncfusion WinUI). KumikoUI hard-codes "Search...", "Select All", "OK", "Cancel", "Clear".
5. **Conditional formatting — 5/7**: KumikoUI already has `CellStyleResolver`; only row-level and header-level resolvers/documentation are missing (treat as a documentation/API-completeness item, not a gap).
6. **Export — 4/7 (Excel/PDF/CSV)**: Syncfusion MAUI (Excel, PDF), DevExpress (many), Syncfusion WinUI (Excel), WinUI.TableView (CSV). A CSV exporter is cheap and dependency-free; Excel/PDF require third-party libraries.
7. **Load-more / incremental loading — 3/7, but 3/3 of the MAUI-market vendors** (Syncfusion MAUI, Telerik, DevExpress). This is table-stakes in the mobile-DataGrid segment KumikoUI competes in.
8. **Clipboard copy (rows/cells, with/without headers) — 3/7** (Syncfusion MAUI, Syncfusion WinUI, WinUI.TableView); desktop users expect Ctrl+C.
9. **Auto / variable row height — 3/7** (Syncfusion MAUI, Telerik default, Syncfusion WinUI). KumikoUI's uniform `style.RowHeight` is baked into the virtual-scroll math, so this is architecturally expensive; schedule post-1.0 unless text wrapping is a 1.0 goal.
10. **Screen-reader accessibility — 2–3/7 among competitors** (WCT/Uno-WCT ✓, Syncfusion MAUI partial), but it is a compliance requirement rather than a parity item; see §4.
11. **Mobile idioms: pull-to-refresh and swipe actions — 2/7 each (Syncfusion MAUI, DevExpress)**; paging — 2/7 (Syncfusion MAUI, Telerik); DataTable binding — 2/7 (Syncfusion MAUI, Telerik); RTL — 2/7 (both Syncfusion); context menu — 2/7 (both Syncfusion); stacked headers — 2/7 (both Syncfusion); unbound columns — 2/7 (both Syncfusion).
12. **Grid-wide search-as-you-type — 1/7** (Telerik); **cell merging — 1/7** (Syncfusion WinUI); **tree/hierarchical — 0/7 in-grid** (separate products at Telerik and Avalonia); **printing, tooltips, column-chooser UI, hyperlink columns — 0/7 verified**. These are not parity blockers.

---

## 2. Ecosystem / support lifecycle

### 2.1 .NET
- Support table (fetched 2026-10-22): **.NET 8 LTS** released 2023-11-14, latest patch 8.0.32, end of support **2026-11-10**; **.NET 9 STS** released 2024-11-12, latest patch 9.0.21, end of support **2026-11-10**; **.NET 10 LTS** released 2025-11-11, latest patch 10.0.13, end of support **2028-11-14**. .NET 11 appears only as go-live RC1 (2026-09-08) and RC2 (2026-10-13; support ends 2026-11-10). https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core
- Policy text: "LTS releases get free support and patches for three years. STS releases get free support and patches for two years." (same page)
- **Implication:** KumikoUI.Core/KumikoUI.SkiaSharp (`net9.0` only) and the KumikoUI.Uno heads (`net9.0-*`) sit on a runtime that leaves support **19 days from today**.

### 2.2 .NET MAUI
- ".NET MAUI aligns with the .NET release cadence." Supported: **.NET MAUI 10** (released 2025-11-11, end of support **2027-05-11**). Out of support: **.NET MAUI 9** (end of support **2026-05-12**). "A major version of .NET MAUI receives support for a minimum of 6 months after a successor… ships." Servicing "ship[s] on the same monthly update cycle as the rest of .NET… the second Tuesday of the month". https://dotnet.microsoft.com/en-us/platform/support/policy/maui
- Latest release on GitHub: **10.0.120 (21 Oct 2026, "Latest")**; 11.0.100-rc.2 (15 Oct 2026) is prerelease. https://github.com/dotnet/maui/releases
- KumikoUI.Maui already targets `net10.0-*` (`src/KumikoUI.Maui/KumikoUI.Maui.csproj:4`), which is correct.

### 2.3 SkiaSharp
- Latest stable on nuget.org: **4.153.1 (last updated 2026-09-30)**; newest prerelease 4.156.0-preview.1 (2026-10-15). https://www.nuget.org/packages/SkiaSharp
- **3.119.2 is dated 2026-02-07** and the page shows "There is a newer version of this package available"; no deprecation notice. https://www.nuget.org/packages/SkiaSharp/3.119.2
- GitHub releases: 4.153.1 marked "Latest" (30 Sep); RAW/DNG image decoding is disabled across 4.150.5–4.155.0 ("decode them before handing raster data to SkiaSharp"). https://github.com/mono/SkiaSharp/releases
- **4.148.0 (released 2026-06-22) is the first stable v4** and its "Breaking Changes" include: deprecated v3 text/font members on `SKPaint` are now compile errors (move typeface/size/hinting/encoding to `SKFont` + `SKCanvas.DrawText(…, SKFont, …)`); `new SKFont()` uses `SKTypeface.Empty` (measures zero until a typeface is set); `SKPaint.Typeface` never returns null (check `IsEmpty`); `SKTypeface.FromFamilyName` falls back to `SKTypeface.Default` for missing families; `SKStream.Move(long)` obsolete; `SKNativeObject.IgnorePublicDispose` setter removed; Ganesh Vulkan requires 1.1+; WASM native assets require .NET 8+. https://mono.github.io/SkiaSharp/docs/releases/4.148.0.html
- 4.150.0 (2026-07-07, m150) and 4.152.0 (2026-09-05, m152): "Breaking Changes: None in this release." https://mono.github.io/SkiaSharp/docs/releases/4.150.0.html , https://mono.github.io/SkiaSharp/docs/releases/4.152.0.html
- Release cadence: stable channel follows Skia milestones in Chrome Stable/Extended Stable; preview channel follows Chrome Beta. Uno apps "ship with SkiaSharp 3.x by default"; opting into 4.x is via the `SkiaSharpVersion` MSBuild property and was described as experimental (post dated 2026-06-29). https://platform.uno/blog/announcing-skiasharp-4-0/
- `SKPath` → `SKPathBuilder` obsoletions in 4.152 were reported by third-party migration PRs found in search, **not** by the official release notes **[UNVERIFIED]**.

### 2.4 SkiaSharp.Views.Maui.Controls vs .Core
- `SkiaSharp.Views.Maui.Controls` 4.153.1 "adds SkiaSharp rendering controls and image sources to .NET MAUI applications": `SKCanvasView` (CPU, on-demand), `SKGLView` (GPU, continuous), image sources, XAML/data binding/touch, "Native handlers for Android, iOS, Mac Catalyst, and Windows"; depends on `SkiaSharp.Views.Maui.Core` (>= same version); targets net9.0 and net10.0 platform TFMs (MAUI >= 9.0.120 / 10.0.20). https://www.nuget.org/packages/SkiaSharp.Views.Maui.Controls
- `SkiaSharp.Views.Maui.Core` 4.153.1: "most .NET MAUI apps should install Controls, which includes this package as shared infrastructure." https://www.nuget.org/packages/SkiaSharp.Views.Maui.Core
- The mono/SkiaSharp README does not discuss the two packages; it lists Uno Platform support and the `SkiaSharp.Views.Uno.WinUI` badge only. https://github.com/mono/SkiaSharp
- Recommendation derived from the above: keep `SkiaSharp.Views.Maui.Controls` in KumikoUI.Maui (it is the documented app-facing package and already what the csproj references at `KumikoUI.Maui.csproj:28`).

### 2.5 Uno Platform
- Latest stable **Uno.Sdk 6.7.30 (2026-09-18)**; 6.8.0-dev and 7.0.0-dev prereleases exist (2026-10-21). https://www.nuget.org/packages/Uno.Sdk — pinned **6.5.31 is dated 2026-02-10** with a "newer version" banner. https://www.nuget.org/packages/Uno.Sdk/6.5.31
- GitHub releases list 6.6.166 as "Latest" with 6.6.184 (06 Aug) newer; the 6.7 line is announced on the blog (Uno.Sdk 6.7.30, 2026-09-24) with damage-region repaint "on Win32, X11, macOS, Android, iOS, and WebAssembly", a VSync-paced Win32 render thread, GPU `RenderTargetBitmap`, and WebGL-2 `GLCanvasElement` on WASM. https://github.com/unoplatform/uno/releases , https://platform.uno/blog/uno-platform-6-7/
- .NET support: **Uno 6.4+: default .NET 10, min .NET 9, max .NET 10**; Uno 6.3 removed .NET 8; ".NET 9 (STS) is supported for 24 months (until November 10, 2026)" and "Mobile workloads remain at 18 months, until May 12, 2026"; .NET 10 supported until November 2028; nothing about .NET 11. https://platform.uno/docs/articles/net-version-support.html
- Uno 6.4 (2025-11-11): "Uno Platform supports .NET 10 from the day it launches… .NET 10 is now a stable target… the recommended runtime for greenfield cross-platform Uno Platform apps", "While Uno Platform continues to support .NET 9 for production apps". https://platform.uno/blog/uno-platform-6-4/
- `SkiaSharp.Views.Uno.WinUI` 4.153.1 depends on Uno.WinUI >= 5.2.175 and SkiaSharp 4.153.1; targets net9.0/net10.0 + Android/iOS/MacCatalyst/macOS/Windows. https://www.nuget.org/packages/SkiaSharp.Views.Uno.WinUI
- Uno.Sdk implicit packages: versions are overridden via `*Version` properties set in `Directory.Build.props` or the csproj; `SkiaSharpVersion` is one of them; the page does not state the default SkiaSharp major. https://platform.uno/docs/articles/features/using-the-uno-sdk.html
- **SKCanvasElement vs SKXamlCanvas**: `SKCanvasElement` is "an abstract FrameworkElement for 2D drawing with Skia" that draws on the app's existing Skia canvas; it is only available where Skia rendering is used (`Uno.WinUI.Graphics2DSK`, always referenced on `netX.0-desktop`, and on other targets with the `SkiaRenderer` feature), check `IsSupportedOnCurrentPlatform()`; hardware acceleration "comes out of the box if the Uno application is already using OpenGL to render", whereas `SKXamlCanvas` "doesn't support hardware acceleration on Skia targets yet" and "requires additional buffer copying"; drawing is cached until `Invalidate()`. https://platform.uno/docs/articles/controls/SKCanvasElement.html — KumikoUI.Uno currently uses `SKXamlCanvas` (`src/KumikoUI.Uno/KumikoUI.Uno.csproj:35,50`).

---

## 3. NuGet packaging best practices (with current repo status)

| Topic | Guidance (cited) | KumikoUI today (`Directory.Build.props`, csproj, workflows) |
|---|---|---|
| README on nuget.org | Only Markdown (CommonMark via Markdig) and images "from a limited set of domains"; supported features list headers, images, lists, links, tables, task lists, emojis, code spans — raw HTML is **not** listed; "Images with relative local paths and images hosted from unsupported domains will not be rendered"; img.shields.io and raw.githubusercontent.com are allowed. Fix requires pushing a new package version. https://learn.microsoft.com/en-us/nuget/nuget-org/package-readme-on-nuget-org | `PackageReadmeFile=README.md` packs the repo README, which uses `<p align="center"><img src="images/logo.svg">`, `images/main.png`, `images/grouping.png`, `images/dark_mode.png`, `images/large_data.gif` (relative paths inside HTML). These will not render on nuget.org. |
| Package validation | `EnablePackageValidation=true` runs after `Pack`; three validators (baseline version, compatible runtime, compatible framework). https://learn.microsoft.com/en-us/dotnet/fundamentals/apicompat/package-validation/overview — `PackageValidationBaselineVersion` compares against the last stable package; breaking changes fail pack with `CP0002` etc.; intentional breaks are recorded via `dotnet pack /p:GenerateCompatibilitySuppressionFile=true` → `CompatibilitySuppressions.xml` checked into source. https://learn.microsoft.com/en-us/dotnet/fundamentals/apicompat/package-validation/baseline-version-validator | Not enabled. Becomes meaningful only after the first stable 1.0.0 exists as a baseline. |
| Public API tracking | `Microsoft.CodeAnalysis.PublicApiAnalyzers`: RS0016 "Add public types and members to the declared API", RS0017 "Remove deleted types and members…", RS0026/RS0027 (optional-parameter overload rules), RS0036/RS0037/RS0041 (nullability), RS0048 "Missing shipped or unshipped public API file"; files `PublicAPI.Shipped.txt`/`PublicAPI.Unshipped.txt`; add `#nullable enable` at the top; per-TFM files via `PublicAPI/$(TargetFramework)/`. https://github.com/dotnet/roslyn-analyzers/blob/main/src/PublicApiAnalyzers/Microsoft.CodeAnalysis.PublicApiAnalyzers.md , https://github.com/dotnet/roslyn-analyzers/blob/main/src/PublicApiAnalyzers/PublicApiAnalyzers.Help.md | Not used. |
| Trimming | `<IsTrimmable>true</IsTrimmable>` marks the assembly trimmable and enables trim warnings; `EnableTrimAnalyzer` warns without marking; `VerifyReferenceTrimCompatibility` (IL2125); build a self-contained test app with `PublishTrimmed` + `TrimmerRootAssembly` to see all warnings; "Introducing trim analysis warnings is a breaking change when the library is used with `PublishTrimmed`." https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/prepare-libraries-for-trimming | Not set. Relevant because iOS/Mac Catalyst/WASM heads trim by default. |
| Native AOT | `<IsAotCompatible>true</IsAotCompatible>` defaults `IsTrimmable`, `EnableTrimAnalyzer`, `EnableSingleFileAnalyzer`, `EnableAotAnalyzer` to true; requires `net8.0`+ (conditionally enable with `IsTargetFrameworkCompatible`); `VerifyReferenceAotCompatibility` (IL3058); "The `IsAotCompatible` assembly metadata was introduced in .NET 10"; iOS/MacCatalyst Native AOT fully supported from .NET 9+, Android experimental. https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/ | Not set. |
| Source Link / symbols / deterministic | Source Link "included in the .NET SDK and enabled by default" from .NET 8 with GitHub built in, so no package reference is needed; `PublishRepositoryUrl=true`; `EmbedUntrackedSources` default on SDK 8+. https://github.com/dotnet/sourcelink/blob/main/README.md — Library guidance: "CONSIDER using Source Link", "CONSIDER publishing symbol files", "CONSIDER enabling deterministic builds". https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/sourcelink — `ContinuousIntegrationBuild` normalizes stored file paths for official builds. https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props — "CONSIDER publishing symbols as a symbol package (`*.snupkg`)"; nuget.org symbol server supports portable PDBs only. https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/nuget | Present: `Deterministic`, `DebugType=portable`, `EmbedUntrackedSources`, `PublishRepositoryUrl`, `ContinuousIntegrationBuild` (on GITHUB_ACTIONS), `--include-symbols -p:SymbolPackageFormat=snupkg` in publish.yml. The explicit `Microsoft.SourceLink.GitHub` 8.0.0 reference is redundant on SDK 8+ but harmless. |
| Versioning | "CONSIDER using SemVer 2.0.0"; "DO include a prerelease suffix when releasing a nonstable package"; "CONSIDER only including a major version in the AssemblyVersion"; "CONSIDER keeping the major version number of the AssemblyVersion and the NuGet package version in sync"; "AVOID setting the assembly informational version yourself" (Source Link sets it). https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/versioning — NuGet metadata table (PackageId, Description, Authors, PackageTags, PackageIcon 64×64, PackageProjectUrl, PackageLicenseExpression); "CONSIDER choosing a NuGet package name with a prefix that meets NuGet's prefix reservation criteria". https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/nuget | All four csproj files hard-code `<Version>1.0.0</Version>` while nothing is published as stable yet; publish.yml overrides via `-p:Version`. Prefix reservation for `KumikoUI.*` not checked **[UNVERIFIED]**. |
| Central package management | `Directory.Packages.props` with `ManagePackageVersionsCentrally=true` and `<PackageVersion>` items; per-TFM conditions; `VersionOverride`; `CentralPackageTransitivePinningEnabled` (promotes pinned transitives into the nuspec, "should be carefully evaluated when authoring a library"); `GlobalPackageReference`; NU1507 with multiple sources. https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management | Not used; `3.119.2` is repeated in 7 places (Core, Maui, Uno, two samples, Uno tests, README badge). |
| Publishing credentials | NuGet Trusted Publishing: GitHub Actions OIDC → `NuGet/login@v1` → 1-hour temporary API key; policy = repo owner, repo, workflow file name, optional environment; private-repo policies are "temporarily active for 7 days" until first publish. https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing | publish.yml not inspected past line 80 for the push step; whether it uses a long-lived `NUGET_API_KEY` secret is **[UNVERIFIED]**. |

---

## 4. Accessibility for a canvas-drawn grid

What each platform requires to expose content that is drawn, not composed of native views:

- **iOS / Mac Catalyst (UIKit)** — "Such separate elements within a container view are not automatically accessible (because they are not subclasses of `UIView`) and do not provide any accessibility information." The container implements `UIAccessibilityContainer` (`accessibilityElementCount`, `accessibilityElementAtIndex:`, `indexOfAccessibilityElement:`), creates one `UIAccessibilityElement` per logical item (`initWithAccessibilityContainer:`), returns `NO` from `isAccessibilityElement` for the container itself, sets `accessibilityLabel`/`accessibilityTraits` (traits OR'd together), and posts `UIAccessibilityLayoutChangedNotification` when layout changes. https://developer.apple.com/library/archive/documentation/UserExperience/Conceptual/iPhoneAccessibility/Making_Application_Accessible/Making_Application_Accessible.html — The modern reference pages for `UIAccessibilityElement` and `UIAccessibilityContainer` returned only their titles (JavaScript-rendered) and could not be read: https://developer.apple.com/documentation/uikit/uiaccessibilityelement , https://developer.apple.com/documentation/uikit/uiaccessibilitycontainer **[could not fetch body]**.
- **Android** — `ExploreByTouchHelper` (androidx.customview) is "a utility for adding accessibility support to custom views that represent a collection of View-like logical items"; it extends `AccessibilityNodeProviderCompat`; "Clients should override abstract methods on this class and attach it to the host view using setAccessibilityDelegate"; the host forwards `dispatchHoverEvent` (and key events); subclasses implement `getVirtualViewAt(x, y)`, `getVisibleVirtualViews`, `onPopulateNodeForVirtualView`, `onPerformActionForVirtualView`; `invalidateVirtualView` notifies property changes. Source: reference page https://developer.android.com/reference/androidx/customview/widget/ExploreByTouchHelper — direct fetches returned only the site navigation (page is 338 K chars of nav), so the quotes above come from the search-index excerpt of that page **[partially verified]**. The former "custom views" guide now redirects to the Compose guide, which only notes Views content lives on "Make custom views more accessible (Views)". https://developer.android.com/guide/topics/ui/accessibility/custom-views
- **Windows (WinUI 3 / Windows App SDK)** — Override `OnCreateAutomationPeer` to return a peer deriving from `FrameworkElementAutomationPeer`; declare patterns in `GetPatternCore` (return `this` for supported `PatternInterface`s); "If your control exposes collections… override `GetChildrenCore` to accurately represent parent-child relationships in the automation tree" (the default "traverses the visual tree", custom controls "can override this to expose a more meaningful automation representation"); report `AutomationControlType` (avoid `Custom`); raise `RaiseAutomationEvent` / `RaisePropertyChangedEvent` on state changes. https://learn.microsoft.com/en-us/windows/apps/design/accessibility/custom-automation-peers — Grid pattern: children "must implement `IGridItemProvider` and be organized in a two-dimensional logical coordinate system"; required `IGridProvider` members `RowCount`, `ColumnCount`, `GetItem`; "If a cell is empty, a Microsoft UI Automation element must still be returned"; hidden rows/columns "should not be counted" until loaded. https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-implementinggrid — Table pattern adds `RowOrColumnMajor`, `GetColumnHeaders`, `GetRowHeaders` and requires "the required, concurrent implementation of `IGridProvider`". https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-implementingtable
- **Web (WASM)** — ARIA grid: container `role="grid"`, rows `role="row"`, cells `columnheader`/`rowheader`/`gridcell`; keyboard: arrows move one cell, Home/End first/last cell in row, Page Up/Down "an author-determined number of rows", Ctrl+Home/End; "Only one of the focusable elements contained by the grid is included in the page tab sequence"; for virtualized grids set `aria-rowcount`/`aria-colcount` on the grid and `aria-rowindex`/`aria-colindex` on cells; `aria-sort` on the sorted header; `aria-selected` on selected elements. https://www.w3.org/WAI/ARIA/apg/patterns/grid/
- **Uno Platform** — "Uno implements the same AutomationPeer model as WinUI"; supported patterns table includes Invoke, Toggle, RangeValue, Value, ExpandCollapse, Selection, SelectionItem, Scroll, ScrollItem, **Grid, GridItem, Table**; on WASM "the ARIA role is derived from the AutomationControlType reported by the peer" (`AriaMapper`); WASM "prunes structural elements… that have no accessible information". https://platform.uno/docs/articles/features/accessibility/automation-peers.html — Status table: Win32/Skia Narrator **Supported**; macOS/Skia VoiceOver **Supported**; WASM/Skia ARIA **Supported**; Linux **Planned**; **Android/Skia TalkBack and iOS/Skia VoiceOver "Work in progress"**; native Android/iOS/WASM supported. "On Skia-rendered targets, Uno maintains a semantic accessibility tree alongside the visual tree." "On WASM, the accessibility layer activates when the user first presses the `Tab` key." https://platform.uno/docs/articles/features/accessibility/index.html
- **.NET MAUI** — `SemanticProperties.Description/Hint/HeadingLevel` and `AutomationProperties.IsInAccessibleTree/ExcludedWithChildren` are attached properties on a `VisualElement`; "Semantic properties don't try to force equivalent behavior on each platform"; "On iOS, if you set the `Description` property on any control that has children the screen reader will be unable to reach the children"; `SemanticScreenReader.Default.Announce(...)`; `SetSemanticFocus()`. The page documents no way to expose multiple accessibility elements from one view. https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/accessibility — Handlers expose the native view via `PlatformView` ("can be accessed to set native view properties, invoke native view methods, and subscribe to native view events"), which is the hook for per-platform accessibility providers. https://learn.microsoft.com/en-us/dotnet/maui/user-interface/handlers/

**Realistic effort and minimum viable approach.** A single `SKCanvasView`/`SKXamlCanvas` is one opaque accessibility node on every platform, so screen readers see nothing inside the grid. The minimum viable design is a platform-neutral **virtual accessibility tree built from the existing layout engine** (visible rows × visible columns, headers, group rows, summary rows, each with label/value/bounds/state and actions select/edit/sort/expand), then per-host adapters: iOS `UIAccessibilityContainer` + `UIAccessibilityElement` per visible cell (Apple archive guide); Android `ExploreByTouchHelper` with one virtual-view id per visible cell (reference page); WinUI/Uno a custom `AutomationPeer` returning child peers from `GetChildrenCore` and implementing `IGridProvider`/`ITableProvider`/`ISelectionProvider` (WinUI docs, Uno patterns table); WASM via Uno's semantic DOM/ARIA (Uno accessibility pages). Effort drivers: four native adapters, re-publishing the tree on scroll/sort/filter (layout-changed notifications), keyboard focus parity (already present), and the fact that Uno's Skia accessibility is still "Work in progress" on Android/iOS, so the Uno mobile heads cannot be fully accessible until Uno finishes that work regardless of what KumikoUI does.

---

## 5. Open-source 1.0 release hygiene (with repo status)

| Item | Guidance (cited) | KumikoUI today |
|---|---|---|
| Community profile | GitHub's checklist "checks to see if a project includes recommended community health files" (README, CODE_OF_CONDUCT, LICENSE, CONTRIBUTING) "in a supported location"; security policy; issue templates "must be located in the `.github/ISSUE_TEMPLATE` folder" with `name:`/`about:` (Markdown) or `name:`/`description:` (YAML forms). https://docs.github.com/en/communities/setting-up-your-project-for-healthy-contributions/about-community-profiles-for-public-repositories — Supported files: CODE_OF_CONDUCT.md, CONTRIBUTING.md, FUNDING.yml, issue/PR templates, SECURITY.md, SUPPORT.md; precedence `.github` → root → `docs`. https://docs.github.com/en/communities/setting-up-your-project-for-healthy-contributions/creating-a-default-community-health-file | Present: README.md, LICENSE (MIT), CODE_OF_CONDUCT.md, `.github/ISSUE_TEMPLATE/bug_report.md` + `feature_request.md`. **Missing: CONTRIBUTING.md, SECURITY.md, SUPPORT.md, PULL_REQUEST_TEMPLATE.md, CODEOWNERS.** |
| Security policy | "give instructions for how to report a security vulnerability… by adding a security policy"; include "information about supported versions of your project and how to report a vulnerability". https://docs.github.com/en/code-security/getting-started/adding-a-security-policy-to-your-repository | Missing (SECURITY.md absent). |
| Changelog | Keep a Changelog 1.1.0: "Changelogs are for humans, not machines", "There should be an entry for every single version", "The same types of changes should be grouped", "Versions and sections should be linkable", "The latest version comes first", "The release date of each version is displayed", "Mention whether you follow Semantic Versioning"; sections Added/Changed/Deprecated/Removed/Fixed/Security; keep an `Unreleased` section; ISO 8601 dates; `[YANKED]` tag. https://keepachangelog.com/en/1.1.0/ | Missing (CHANGELOG.md absent). |
| Commit convention | Conventional Commits 1.0.0: `<type>[optional scope]: <description>`; "The type `fix` MUST be used when a commit represents a bug fix" (PATCH); "The type `feat` MUST be used when a commit adds a new feature" (MINOR); `BREAKING CHANGE:` footer or `!` before the colon (MAJOR); "A scope MUST consist of a noun describing a section of the codebase surrounded by parenthesis"; initial-development FAQ: proceed "as if you've already released the product". https://www.conventionalcommits.org/en/v1.0.0/ | Already used (`feat(core):`, `fix(uno):`, `fix(sample):` in recent history). Document it in CONTRIBUTING.md and consider generating the changelog from it. |
| Dependabot | "You enable Dependabot version updates by committing a `dependabot.yml` configuration file… in the `.github` directory"; file must start with `version: 2`; each `updates` entry needs `package-ecosystem`, `directory`/`directories`, `schedule.interval`. https://docs.github.com/en/code-security/dependabot/dependabot-version-updates/configuring-dependabot-version-updates — ecosystem values: NuGet = `nuget` ("<=6.12.0"), GitHub Actions = `github-actions` (`directory: "/"`); `groups` and `open-pull-requests-limit` (default 5) available. https://docs.github.com/en/code-security/dependabot/dependabot-version-updates/configuration-options-for-the-dependabot.yml-file — Dependabot alerts and version updates are "Available for all GitHub plans". https://docs.github.com/en/code-security/getting-started/github-security-features | Missing (`.github/dependabot.yml` absent). |
| CodeQL | C# is a supported language; default setup "automatically chooses the languages to analyze, query suite to run, and events that trigger scans"; advanced setup adds a workflow using `github/codeql-action`. https://docs.github.com/en/code-security/code-scanning/introduction-to-code-scanning/about-code-scanning-with-codeql — For C# the default build mode is `none` ("CodeQL creates a database without requiring a build"), with the caveat "Generated code not analyzed" and inaccurate dependency guesses; `autobuild`/`manual` are alternatives. https://docs.github.com/en/code-security/code-scanning/creating-an-advanced-setup-for-code-scanning/codeql-code-scanning-for-compiled-languages — Code scanning is "Available for public repositories by default"; private repos need GitHub Code Security. https://docs.github.com/en/code-security/getting-started/github-security-features | Not configured (only `ci.yml` and `publish.yml` in `.github/workflows`). |
| OpenSSF Scorecard | Checks include Branch-Protection (High), Code-Review (High), Dangerous-Workflow (Critical), Dependency-Update-Tool (High), Maintained (High), Pinned-Dependencies (Medium), Packaging (Medium), SAST (Medium), Security-Policy (Medium), Signed-Releases (High), Token-Permissions (High), Vulnerabilities (High), License (Low), CI-Tests (Low); run via `ossf/scorecard-action` with `publish_results: true` for a README badge. https://github.com/ossf/scorecard | Not configured. Quick wins from the table: SECURITY.md, dependabot.yml, CodeQL (SAST), least-privilege `permissions:` in workflows, pinning actions by SHA. |

---

## Could not verify (explicit list)

- Apple modern reference pages for `UIAccessibilityElement` / `UIAccessibilityContainer` (body not fetchable; archive guide used instead).
- Android `ExploreByTouchHelper` reference body (fetch returned navigation only; quotes come from the search index of that page).
- DevExpress MAUI column resizing/reordering (not present in the first 100 K chars of the members page; absence not proven).
- Telerik MAUI export, RTL, accessibility, context menu (not found on overview/product pages).
- Syncfusion WinUI printing, column chooser, PDF/CSV export, paging, hyperlink column (not on the fetched overview/product/columns pages; these may exist on other doc pages).
- WCT DataGrid `ClipboardCopyMode` and `FrozenColumnCount` (not on fetched pages).
- Whether Uno.Sdk 6.6/6.7 changed the default SkiaSharp major from 3.x to 4.x (6.7 post silent; June 2026 post says 3.x default).
- `SKPath`→`SKPathBuilder` obsoletions in SkiaSharp 4.152 (community PR claim; official notes say no breaking changes).
- KumikoUI `NUGET_API_KEY` usage in publish.yml (only first 80 lines read) and `KumikoUI.*` prefix reservation.
- Multi-select checkbox column, tooltips, text wrapping, header templates, animations for most competitors (not checked).

---

## Sources

Competitors
- https://help.syncfusion.com/maui/datagrid/overview
- https://www.syncfusion.com/maui-controls/maui-datagrid
- https://help.syncfusion.com/maui/datagrid/column-drag-and-drop
- https://help.syncfusion.com/maui/datagrid/row-drag-and-drop
- https://help.syncfusion.com/maui/datagrid/data-validation
- https://help.syncfusion.com/maui/datagrid/context-menu
- https://help.syncfusion.com/maui/datagrid/localization
- https://www.telerik.com/maui-ui/documentation/controls/datagrid/overview (301 from https://docs.telerik.com/devtools/maui/controls/datagrid/overview)
- https://www.telerik.com/maui-ui/datagrid
- https://www.telerik.com/maui-ui/documentation/controls/datagrid/columns/reordering
- https://www.telerik.com/maui-ui/documentation/controls/datagrid/commands/validation
- https://docs.devexpress.com/MAUI/DevExpress.Maui.DataGrid.DataGridView
- https://docs.devexpress.com/MAUI/403255/data-grid
- https://docs.devexpress.com/MAUI/DevExpress.Maui.DataGrid.DataGridView._members
- https://docs.devexpress.com/MAUI/404493/data-grid/drag-and-drop
- https://docs.avaloniaui.net/docs/reference/controls/datagrid/
- https://github.com/AvaloniaUI/Avalonia.Controls.DataGrid
- https://docs.avaloniaui.net/controls/data-display/structured-data/tableview
- https://github.com/AvaloniaUI/Avalonia.Controls.TreeDataGrid
- https://learn.microsoft.com/en-us/windows/communitytoolkit/controls/datagrid
- https://learn.microsoft.com/en-us/windows/communitytoolkit/controls/datagrid_guidance/keyboard_navigation_selection
- https://learn.microsoft.com/en-us/dotnet/communitytoolkit/archive/windows/datagrid
- https://www.nuget.org/packages/CommunityToolkit.WinUI.UI.Controls.DataGrid
- https://github.com/w-ahmad/WinUI.TableView
- https://platform.uno/docs/articles/uno-community-toolkit-v7.html
- https://help.syncfusion.com/winui/datagrid/overview
- https://www.syncfusion.com/winui-controls/datagrid
- https://help.syncfusion.com/winui/datagrid/columns

Ecosystem
- https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core
- https://dotnet.microsoft.com/en-us/platform/support/policy/maui
- https://github.com/dotnet/maui/releases
- https://www.nuget.org/packages/SkiaSharp
- https://www.nuget.org/packages/SkiaSharp/3.119.2
- https://github.com/mono/SkiaSharp/releases
- https://mono.github.io/SkiaSharp/docs/releases/4.148.0.html
- https://mono.github.io/SkiaSharp/docs/releases/4.150.0.html
- https://mono.github.io/SkiaSharp/docs/releases/4.152.0.html
- https://platform.uno/blog/announcing-skiasharp-4-0/
- https://github.com/mono/SkiaSharp
- https://www.nuget.org/packages/SkiaSharp.Views.Maui.Controls
- https://www.nuget.org/packages/SkiaSharp.Views.Maui.Core
- https://www.nuget.org/packages/SkiaSharp.Views.Uno.WinUI
- https://www.nuget.org/packages/Uno.Sdk
- https://www.nuget.org/packages/Uno.Sdk/6.5.31
- https://github.com/unoplatform/uno/releases
- https://platform.uno/blog/uno-platform-6-4/
- https://platform.uno/blog/uno-platform-6-7/
- https://platform.uno/docs/articles/net-version-support.html
- https://platform.uno/docs/articles/features/using-the-uno-sdk.html
- https://platform.uno/docs/articles/controls/SKCanvasElement.html

Packaging
- https://learn.microsoft.com/en-us/nuget/nuget-org/package-readme-on-nuget-org
- https://learn.microsoft.com/en-us/dotnet/fundamentals/apicompat/package-validation/overview
- https://learn.microsoft.com/en-us/dotnet/fundamentals/apicompat/package-validation/baseline-version-validator
- https://github.com/dotnet/roslyn-analyzers/blob/main/src/PublicApiAnalyzers/PublicApiAnalyzers.Help.md
- https://github.com/dotnet/roslyn-analyzers/blob/main/src/PublicApiAnalyzers/Microsoft.CodeAnalysis.PublicApiAnalyzers.md
- https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/prepare-libraries-for-trimming
- https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/
- https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/sourcelink
- https://github.com/dotnet/sourcelink/blob/main/README.md
- https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props
- https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/versioning
- https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/nuget
- https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management
- https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing

Accessibility
- https://developer.apple.com/library/archive/documentation/UserExperience/Conceptual/iPhoneAccessibility/Making_Application_Accessible/Making_Application_Accessible.html
- https://developer.apple.com/documentation/uikit/uiaccessibilityelement (body not fetchable)
- https://developer.apple.com/documentation/uikit/uiaccessibilitycontainer (body not fetchable)
- https://developer.android.com/reference/androidx/customview/widget/ExploreByTouchHelper (nav only; search excerpt)
- https://developer.android.com/guide/topics/ui/accessibility/custom-views
- https://learn.microsoft.com/en-us/windows/apps/design/accessibility/custom-automation-peers
- https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-implementinggrid
- https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-implementingtable
- https://www.w3.org/WAI/ARIA/apg/patterns/grid/
- https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/accessibility
- https://learn.microsoft.com/en-us/dotnet/maui/user-interface/handlers/
- https://platform.uno/docs/articles/features/accessibility/index.html
- https://platform.uno/docs/articles/features/accessibility/automation-peers.html

Hygiene
- https://docs.github.com/en/communities/setting-up-your-project-for-healthy-contributions/about-community-profiles-for-public-repositories
- https://docs.github.com/en/communities/setting-up-your-project-for-healthy-contributions/creating-a-default-community-health-file
- https://docs.github.com/en/code-security/getting-started/adding-a-security-policy-to-your-repository
- https://docs.github.com/en/code-security/getting-started/github-security-features
- https://keepachangelog.com/en/1.1.0/
- https://www.conventionalcommits.org/en/v1.0.0/
- https://docs.github.com/en/code-security/dependabot/dependabot-version-updates/configuring-dependabot-version-updates
- https://docs.github.com/en/code-security/dependabot/dependabot-version-updates/configuration-options-for-the-dependabot.yml-file
- https://docs.github.com/en/code-security/code-scanning/introduction-to-code-scanning/about-code-scanning-with-codeql
- https://docs.github.com/en/code-security/code-scanning/creating-an-advanced-setup-for-code-scanning/codeql-code-scanning-for-compiled-languages
- https://github.com/ossf/scorecard

Local repository files read: `README.md`, `Directory.Build.props`, `src/*/*.csproj`, `samples/*/*.csproj`, `tests/*/*.csproj`, `.github/workflows/ci.yml`, `.github/workflows/publish.yml` (first 80 lines), `src/KumikoUI.Core/{Models,Input,Editing,Components}/*.cs` (keyword scans).

---

## Implications for KumikoUI 1.0 (ranked)

1. **Move off .NET 9 before shipping 1.0.** .NET 9 support ends 2026-11-10 (19 days away); .NET MAUI 9 already ended 2026-05-12; Uno says .NET 9 mobile workloads ended 2026-05-12. Retarget `KumikoUI.Core`/`KumikoUI.SkiaSharp` to `net10.0` (keep `net9.0` only if you accept an unsupported TFM) and the Uno heads to `net10.0-*` on Uno.Sdk 6.4+ (default .NET 10, max .NET 10). https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core , https://dotnet.microsoft.com/en-us/platform/support/policy/maui , https://platform.uno/docs/articles/net-version-support.html
2. **Decide the SkiaSharp 3→4 story now, because 1.0 freezes the public surface.** 4.148.0 (2026-06-22) is stable with compile-breaking `SKPaint` text changes and `SKFont` defaults; 4.153.1 is current. Either ship 1.0 on 4.x (audit text/typeface code first) or ship on 3.119.2 and declare 2.0 for the 4.x move. Uno still defaults to 3.x via `SkiaSharpVersion`, so a 4.x KumikoUI.Uno forces consumers to override. https://mono.github.io/SkiaSharp/docs/releases/4.148.0.html , https://platform.uno/blog/announcing-skiasharp-4-0/
3. **Document (and test) column header drag-reorder** — it is the single most common competitor feature (6/7) and it already exists in `GridInputController`/`DataGridColumn.AllowReorder` but is absent from the README. Add it to the feature table, sample app, and both hosts' public API.
4. **Surface validation as a first-class, documented feature** (6/7 competitors): `EditSession` already honours `IDataErrorInfo`/`INotifyDataErrorInfo`; add a visible error state per cell, commit-blocking semantics, and docs. https://help.syncfusion.com/maui/datagrid/data-validation
5. **Fix the NuGet README before the first stable push.** nuget.org will not render relative `images/*.png` paths and does not list raw HTML among supported features; use absolute `raw.githubusercontent.com` URLs or a dedicated package README. Each fix needs a new package version. https://learn.microsoft.com/en-us/nuget/nuget-org/package-readme-on-nuget-org
6. **Add the mobile-market table stakes: load-more/incremental loading first** (all three MAUI vendors), then pull-to-refresh and swipe actions (Syncfusion, DevExpress), and row details/master-detail (5/7).
7. **Externalize built-in strings** ("Search...", "Select All", "OK", "Cancel", "Clear") into a localizable resource/provider; 5/7 competitors document localization and RTL follows the same seam. https://help.syncfusion.com/maui/datagrid/localization
8. **Plan accessibility as a virtual accessibility tree + four adapters**, and state the current gap honestly in the README. Expect WinUI (`AutomationPeer` + `IGridProvider`/`ITableProvider`), iOS (`UIAccessibilityContainer`), Android (`ExploreByTouchHelper`), WASM (Uno semantic DOM/ARIA) work; note that Uno's Skia accessibility on Android/iOS is still "Work in progress". https://learn.microsoft.com/en-us/windows/apps/design/accessibility/custom-automation-peers , https://platform.uno/docs/articles/features/accessibility/index.html
9. **Lock the API with tooling at 1.0.0:** add `Microsoft.CodeAnalysis.PublicApiAnalyzers` (`PublicAPI.Shipped.txt`) now, then `EnablePackageValidation` + `PackageValidationBaselineVersion=1.0.0` on the first post-1.0 build; use `CompatibilitySuppressions.xml` for intentional breaks. https://github.com/dotnet/roslyn-analyzers/blob/main/src/PublicApiAnalyzers/Microsoft.CodeAnalysis.PublicApiAnalyzers.md , https://learn.microsoft.com/en-us/dotnet/fundamentals/apicompat/package-validation/baseline-version-validator
10. **Mark the libraries trimmable/AOT-compatible** (`IsTrimmable`, `IsAotCompatible`, which auto-enables the trim/AOT/single-file analyzers) and add a `PublishTrimmed` test app; iOS/Mac Catalyst/WASM consumers trim by default and "introducing trim analysis warnings is a breaking change". https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/prepare-libraries-for-trimming , https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/
11. **Adopt Central Package Management** to replace the seven hand-pinned `3.119.2` occurrences and keep MAUI/Uno/SkiaSharp versions in one `Directory.Packages.props`. https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management
12. **Ship the missing community files**: CONTRIBUTING.md (document the Conventional Commits convention already in use), SECURITY.md, SUPPORT.md, PULL_REQUEST_TEMPLATE.md, CHANGELOG.md (Keep a Changelog with an `Unreleased` section). https://docs.github.com/en/communities/setting-up-your-project-for-healthy-contributions/creating-a-default-community-health-file , https://keepachangelog.com/en/1.1.0/
13. **Turn on free supply-chain checks**: `.github/dependabot.yml` for `nuget` + `github-actions`, CodeQL default setup (C# `build-mode: none`), least-privilege workflow `permissions`, and run OpenSSF Scorecard for a baseline. https://docs.github.com/en/code-security/dependabot/dependabot-version-updates/configuration-options-for-the-dependabot.yml-file , https://docs.github.com/en/code-security/code-scanning/creating-an-advanced-setup-for-code-scanning/codeql-code-scanning-for-compiled-languages , https://github.com/ossf/scorecard
14. **Publish with NuGet Trusted Publishing (OIDC) instead of a long-lived API key** and keep the `snupkg` symbol packages the workflow already produces; the explicit `Microsoft.SourceLink.GitHub` reference can be dropped on SDK 8+. https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing , https://github.com/dotnet/sourcelink/blob/main/README.md
15. **Evaluate `SKCanvasElement` for the Uno Skia heads post-1.0** — it avoids the extra buffer copy and gets hardware acceleration "out of the box" where the app already renders with OpenGL, but it only exists on Skia-rendered targets (`IsSupportedOnCurrentPlatform()`), so keep `SKXamlCanvas` as the fallback. https://platform.uno/docs/articles/controls/SKCanvasElement.html

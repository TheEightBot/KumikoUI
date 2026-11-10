# Changelog

All notable changes to KumikoUI are documented in this file. All four packages (KumikoUI.Core, KumikoUI.SkiaSharp, KumikoUI.Maui, KumikoUI.Uno) share one version.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html) as described in [docs/VERSIONING.md](docs/VERSIONING.md). Each release's section is also used as its GitHub release notes.

## [Unreleased]

### Added
- `IInteractiveCellRenderer` (`OnCellTap`, `OnCellActivate`, `RedrawRequested`): a cell renderer whose cell is acted on without an editor. `ActionButtonsCellRenderer` implements it, is `IDisposable`, and listens to its commands' `CanExecuteChanged` through weak listeners. `KumikoRenderer` is `IDisposable` and raises `RedrawRequested` for such renderers (the MAUI and Uno grids redraw on it) (#35).
- `KumikoStyle.ActionButtonDisabledBackgroundColor` and `ActionButtonDisabledTextColor`, with values for the Light, Dark and HighContrast themes: the look of an action button whose command cannot execute (#35).
- `KumikoSelectionModel.ClearSelectionOnEmptyAreaTap` (default `true`), also `ClearSelectionOnEmptyAreaTap` on the MAUI and Uno `KumikoDataGrid`: a tap on the empty part of the rows area (below the last row, right of the last column) commits an open edit and clears the selection and the current cell (#45).
- `KumikoInputController.WouldHandleTab(selection, source, backwards)` tells a host whether Tab would be handled before it processes the key, for hosts that must claim Tab up front (#45).
- Space toggles the current Boolean cell when its column is editable (not read-only, edit triggers not `None`); on other cells it still toggles the row's selection (#45).
- Custom editors can see the character that opened the edit (#34). New `CellEditorContext` (`Value`, `Bounds`, `InitialCharacter`, `Culture`), `KumikoColumn.EditorFactory` (takes precedence over `CustomEditorFactory`), `EditorDescriptor.CreateEditor(CellEditorContext)` (virtual; forwards to the existing method) and `ICellEditorProvider.CreateEditor(CellEditorContext)` (default interface method; forwards to the existing method). Existing factories, descriptors and providers keep working but receive no character; `TextEditorDescriptor` and the template-column text fallback show it.
- `IValueEditor` (`Value`, `HasValue`, `IsDirty`), implemented by every built-in editor; a custom editor implements it to have its value committed (#33). `DrawnScrollPicker` takes display/value pairs (`SetItems(displayTexts, values)`, `ItemValues`, `SelectedItemValue`, `SelectValue`, `ClearSelection`), and `DrawnDatePicker.TimeOfDay` keeps an edited cell's time of day.
- Validation surface (#32): `EditSession.ValidationFailed` (`CellValidationEventArgs`) reports every error, warning or informational message with its severity and whether the value was committed, and the editor shows the message under the cell with an icon and a red (error) or orange (warning) outline. `KumikoValueConverter.TryConvert` is public. See "Validation" in the README.
- Text-input contract and a rewritten `DrawnTextBox` (#39):
  - `ITextInputSession` (`KumikoUI.Core.Input`) is what a host's native text-input adapter drives: committed text, input-method composition (`SetComposition`, `CommitComposition`, `CancelComposition`), grapheme-wise deletion and selection, with `KumikoTextRange` for ranges and `TextInputStateChanged` for changes made in Core. `EditSession.ActiveTextInput` exposes it for the open editor. The contract is described in [docs/TEXT-INPUT.md](docs/TEXT-INPUT.md); the host adapters follow in #40–#43.
  - `DrawnTextBox` keeps the caret and selection on grapheme-cluster boundaries, so Backspace, Delete and the arrow keys treat an emoji sequence, a flag or a letter with combining marks as one character, and a surrogate pair is never split. It underlines composition text, and applies `NumericOnly`, `InputMask`, `InputValidator` and `MaxLength` (cut at a grapheme boundary) to typed, pasted and composed text alike. Pasted text is single-line.
  - Undo and redo (Ctrl/Cmd+Z, Ctrl/Cmd+Shift+Z, Ctrl/Cmd+Y, `Undo()`, `Redo()`): typing is undone a word at a time, a run of deletes as one step.
  - Double click selects a word, triple click selects all, and dragging selects text and scrolls past the edges; Ctrl/Cmd+Left/Right and Ctrl/Cmd+Backspace/Delete work by word; Left/Right collapse a selection to its edge.
  - `EditSession.ClipboardCopyRequested` and `ClipboardPasteRequested` carry the editor's copy, cut and paste to the host.
  - `KumikoKeyEventArgs.Text` carries text input as a string (a surrogate pair or dead-key sequence arrives whole), `KumikoKeyEventArgs.HasCommand` is Control or Command, and `KumikoKey.Y` is mapped on Uno for redo.
  - Caret hit-testing is a binary search over cached text widths instead of measuring every prefix on every click.
- Safe, culture-aware comparison (#15): `KumikoComparer` orders any two cell values without throwing (nulls first; numbers by value across numeric types; `DateTime`, `DateTimeOffset` and `DateOnly` chronologically; same-type `IComparable`; otherwise by text, then type name). `KumikoSource.Comparer`, `KumikoSource.Culture` (default: current culture) and `KumikoSource.StringCompareOptions` (default: `IgnoreCase`) set one rule for sorting, grouping, the filter popup's value list and search, text and parsed filter conditions, Min/Max summaries and cell formatting.
- View change notifications (#20):
  - `KumikoSource.ViewChanged` (`KumikoViewChangedEventArgs`) is raised exactly once per change, with its kind: `Redraw`, `Rebuild`, `Insert` or `Remove`, and `ViewIndex`/`Count` for incremental changes. `MapRow(oldRow)` follows a row from before the change to after it (data rows by item, group rows by group), and `GetViewRow(item)` finds an item's row.
  - `KumikoSource.BeginUpdate`/`EndUpdate` batch changes (they nest) into one rebuild and one notification.
  - `KumikoSource.SetSort(column, direction, order)` sets single or multi-column sort; with `ToggleSort`, `ToggleMultiSort` and `ClearSort` it is the only writer of sort state.
  - `ScrollState.ScrollChanged` (`KumikoScrollChangedEventArgs`) is raised once per actual scroll offset change.
  - `KumikoSource.GroupExpandedChanged` (`GroupExpandedChangedEventArgs.IsExpanded`) is raised when a group header is expanded or collapsed.
  - The selection model and the edit session follow `ViewChanged` by themselves (#30, #31).
- Native AOT and trimming support (#17). Item properties are read and written, in order, through:
  - `KumikoColumn.ValueAccessor` / `ValueSetter` delegates (and `EditorDisplaySelector` / `EditorValueSelector` for ComboBox items, `DrawnComboBox.DisplaySelector` / `ValueSelector`);
  - accessors generated for types marked `[KumikoGridItem]` (`KumikoUI.Core.Data`), by a source generator now included in the KumikoUI.Core package;
  - providers registered with `KumikoAccessorRegistry.Register` / `AddProvider` (`IKumikoAccessorProvider`, `KumikoPropertyAccessor`);
  - reflection, as before.

  Generated and delegate reads are 3–5× faster than the previous compiled expression trees, with 80% less allocation (`bench/KumikoUI.Benchmarks`). All four packages are marked `IsAotCompatible` and build with no trimming or AOT analyzer warnings; such warnings now fail the build.
- **KumikoUI.Uno**: a new package hosting the grid on the Uno Platform (Windows App SDK, desktop on Windows/macOS/Linux, WebAssembly, Android, iOS) with the same engine, rendering and features as KumikoUI.Maui, plus `KumikoFonts` for registering custom fonts from app assets, and the `SampleApp.Uno` sample.
- `KumikoStyle.PinBottomSummaryRows` (default `true`): `Position=Bottom` table summary rows are pinned to the bottom of the viewport as a fixed footer instead of scrolling after the last row. **Behavior change** for existing users of bottom summaries; set it to `false` to restore the previous layout.

### Changed
- **Breaking:** a tap on an action-button cell runs the button without opening an editor (#35). A column whose renderer is an `ActionButtonsCellRenderer` (and that has no `EditorFactory`, `CustomEditorFactory` or `EditorDescriptor`) never opens an editor from a tap, double-tap, long press, F2 or typing, so `CellBeginEdit`, `CellEndEdit`, `CellValueChanged` and the view change a commit caused no longer fire from a button tap, and `EditTriggers` and `IsReadOnly` no longer matter for it. Enter on the current action cell runs its first button when enabled; Space still toggles the row's selection. To keep the old editor behavior, set `CustomEditorFactory` to create a `DrawnActionButtons`.
- **Breaking:** keyboard and tap contract (#45):
  - Tab and Shift+Tab are no longer a keyboard trap. With no current cell, or past the last (before the first) tab stop, `HandleKey` leaves Tab unhandled and the hosts move focus to the next (previous) control; an open edit is committed first. Tab only opens the editor of the cell it lands on when that column's edit triggers are not `None`.
  - Enter outside an edit opens the editor of the current cell, like F2 (the `F2Key` trigger); when it can't, Enter is left unhandled so the page's default button works. Escape outside an edit is left unhandled when there is nothing to clear.
  - Boolean cells: a single tap toggles only on the check box (anywhere in the cell with the `SingleTap` trigger); the second tap of a double-tap toggles with the `DoubleTap` trigger unless the first tap already did, so a double-tap toggles once. `EditTriggers=None` and read-only columns never toggle. Before, every tap anywhere in the cell toggled, even with `EditTriggers=None`, and a double-tap toggled twice.
  - A tap on the empty area below the rows now clears the selection; set `ClearSelectionOnEmptyAreaTap` to `false` to keep it.
- **Breaking:** `DrawnDatePicker.SelectedDate` is `DateTime?` (null: nothing selected). `DrawnScrollPicker.SelectedIndex` can be -1 (nothing selected), and its `ValueChanged` reports item texts instead of indices. `CellEditorFactory.GetEditorValue` reads `IValueEditor.Value`, so a picker returns the selected item's value. An edit whose editor wasn't changed ends without writing or validating (#33).
- **Breaking:** `CellValueChanged` is now raised after `CellEndEdit` (it was before), for an edit and for a boolean toggle, so a handler sees the session no longer editing and can start the next edit. A refused boolean toggle raises `CellEndEdit` with `Committed` false. Handlers that relied on the old order must change (#34).
- **Breaking:** `KumikoSource.SetCellValue` returns `bool` (whether the value was written) and raises a change only on success. `CellEditorFactory.GetEditorValue` returns the editor's own value (a text box's text) and no longer converts it: `EditSession.CommitEdit` converts. `CellEditorFactory.CreateEditor` takes an optional culture. A warning from validation now keeps the editor open on the first commit (#32).
- **Breaking:** `KumikoColumn.AllowSorting` now defaults to `false` for Template and Image columns and for columns with neither a `PropertyName` nor a `ValueAccessor`; set it to `true` to sort them. `KumikoSource.FormatCellValue` is now an instance method, so it formats in the source's `Culture` (#15).
- **Breaking:** item-based selection (#30). `KumikoSelectionModel` is created for a source (`new KumikoSelectionModel(source)`; the hosts do this) and tracks the selected **items** and the current cell's item and column, so the same items stay selected through sorts, filters, regrouping and collection changes, and items that are no longer visible leave the selection.
  - `SelectedItems` and `SelectedRows` are read-only sets.
  - New: `Select(item)`, `Deselect(item)`, `SelectRange(from, to)`, `SetCurrentCell(row, column)`, `IsSelected(item)` and `CurrentItem`.
  - `SelectionChanged` reports `AddedItems` and `RemovedItems`, and is raised only when the selected items change, not when they merely move.
  - `SelectAll()` and Shift ranges select only data rows (group header and summary rows are never selected); `SelectAll(int)` and `HandleRowSelection(row, shift, ctrl, totalRows)` lose their row-count parameter, and `ClearSelection()` is now `Clear()`.
  - `SelectionUnit`, `KumikoSelectionModel.Unit`, `SelectedCells` and `IsCellSelected` are removed: cell selection was never implemented and is planned for 1.x (#108).
- **Breaking:** `KumikoSource.DataChanged` (an argument-less `Action`) is replaced by `ViewChanged`. `GroupCollapsed`/`GroupCollapsedEventArgs`, which fired on expand as well, are replaced by `GroupExpandedChanged`. `KumikoColumn.SortDirection`, `SortOrder` and `ActiveFilter` are read-only outside KumikoUI.Core: use `KumikoSource.SetSort`, `ClearSort`, `SetColumnFilter` and `ClearAllFilters`. `SetColumns` now rebuilds the view, `ReorderRow` raises one notification instead of two, and a `SetCellValue` whose item raises `PropertyChanged` raises one notification instead of two. `IsMultiSortActive` is cached.
- The reflection fallback for item properties is off for `PublishAot=true` and `TrimMode=full` publishes (KumikoUI.Core build targets set the `KumikoUI.Core.IsReflectionPropertyAccessSupported` switch). A property with no generated accessor, delegate or provider then reads as null and is traced once. Override with the `KumikoReflectionPropertyAccess` MSBuild property. Partial trimming, the .NET MAUI default, is unaffected.
- A nested property path with a `null` value part-way (`Address.City` when `Address` is null) now reads as an empty cell instead of throwing, and an edit to it is ignored.
- **Breaking:** naming pass: every grid type now uses the `Kumiko` prefix (drawn components keep `Drawn`), and no public name collides with types of MAUI, CommunityToolkit.Maui, WinUI/Uno, the Windows Community Toolkit DataGrid, WPF, Windows Forms or Avalonia (#16). Namespaces are unchanged (`KumikoUI.*`); member names are unchanged (for example `KumikoColumn.SortDirection`, `KumikoDataGrid.GridSelectionMode` and `KumikoDataGrid.GridStyle` keep their names, only their types change). Update XAML and code as follows:
  - Host control: `DataGridView` → `KumikoDataGrid` in both `KumikoUI.Maui` and `KumikoUI.Uno`, including XAML (`<dg:DataGridView …>` → `<dg:KumikoDataGrid …>`, `<dg:DataGridView.Columns>` → `<dg:KumikoDataGrid.Columns>`, and the same for `TableSummaryRows` and `GroupSummaryRows`).
  - Models: `DataGridSource` → `KumikoSource`, `DataGridColumn` → `KumikoColumn` (including `<core:DataGridColumn …>` in XAML), `DataGridColumnType` → `KumikoColumnType`, `DataGridStyle` → `KumikoStyle`, `DataGridTheme` → `KumikoTheme`, `DataGridThemeMode` → `KumikoThemeMode`, `CellStyle` → `KumikoCellStyle`, `RowStyle` → `KumikoRowStyle`, `DataGridRenderer` → `KumikoRenderer`
  - Selection and sorting: `SelectionModel` → `KumikoSelectionModel`, `SelectionMode` → `KumikoSelectionMode`, `SelectionChangedEventArgs` → `KumikoSelectionChangedEventArgs`, `SortDirection` → `KumikoSortDirection`
  - Primitives: `GridColor` → `KumikoColor` (and `GridColorTypeConverter` → `KumikoColorTypeConverter`), `GridFont` → `KumikoFont`, `GridFontMetrics` → `KumikoFontMetrics`, `GridPaint` → `KumikoPaint`, `GridRect` → `KumikoRect`, `GridSize` → `KumikoSize`, `GridPoint` → `KumikoPoint`, `GridTextAlignment` → `KumikoTextAlignment`, `GridVerticalAlignment` → `KumikoVerticalAlignment`
  - Input: `GridKey` → `KumikoKey`, `GridKeyEventArgs` → `KumikoKeyEventArgs`, `GridPointerEventArgs` → `KumikoPointerEventArgs`, `GridInputController` → `KumikoInputController`, `GridHitTester` → `KumikoHitTester`, `HitTestResult` → `KumikoHitTestResult`, `GridLayoutEngine` → `KumikoLayoutEngine`
  - Event arguments: `TextChangedEventArgs` → `KumikoTextChangedEventArgs`, `TimeChangedEventArgs` → `KumikoTimeChangedEventArgs`, `ColumnReorderedEventArgs` → `KumikoColumnReorderedEventArgs`, `FocusChangedEventArgs` → `KumikoFocusChangedEventArgs`, `RowTappedEventArgs2` → `RowTappedEventArgs` (the type of `RowTapped` and `RowDoubleTapped` on both hosts and on `KumikoInputController`)
  - Drawn components: `ComboBoxItem` → `DrawnComboBoxItem`, `FocusManager` → `DrawnFocusManager`, `CheckState` → `DrawnCheckState`

  Apps that aliased Core's types to avoid clashes with `Microsoft.UI.Xaml.Controls` or `Microsoft.Maui.Controls` (for example `using SelectionMode = KumikoUI.Core.Models.SelectionMode;`) can drop the aliases.
- KumikoUI.Uno no longer ships the `KumikoUI.Uno.WindowExtensions` app-icon helper that Uno.Resizetizer's source generator added to the library.
- CI fails when a public KumikoUI type shares its simple name with another public KumikoUI type or with a type of those frameworks. The check (`eng/TypeNameCheck`) reads the names from pinned packages, so it needs no workloads. See "Public type names" in CONTRIBUTING.md.
- Every public type now lives in its own source file named after it (for example `KumikoStyle` moved from `GridState.cs` to `KumikoStyle.cs`; the MAUI hosting extensions file is now `KumikoUIHostingExtensions.cs`). No API change.
- The product is named "KumikoUI DataGrid" in all package descriptions, assembly metadata and the README.
- **Breaking:** minimum OS versions raised and unified across KumikoUI.Maui and KumikoUI.Uno: iOS 17.0 (was 15.0 on MAUI, 14.2 on Uno), Mac Catalyst 17.0 (was 15.0, which current Xcode toolchains reject), Android API 24 (was 21), Windows 10.0.19041 (was 10.0.17763 on MAUI). Defined once in `Directory.Build.props` and re-baselined at each .NET major.
- **Breaking:** SkiaSharp 4.153.1 (was 3.119.2) for KumikoUI.SkiaSharp, KumikoUI.Maui and KumikoUI.Uno, which changes the native SkiaSharp library in every consuming app. **Uno apps must set `<SkiaSharpVersion>4.153.1</SkiaSharpVersion>`** in their csproj: Uno.Sdk's implicit SkiaSharp packages default to 3.x, and the app must resolve the same major as KumikoUI. KumikoUI.Uno also references the SkiaSharp packages Uno's runtime pulls at 3.x (`SkiaSharp.Views`, `SkiaSharp.NativeAssets.Linux`, `SkiaSharp.Skottie`), so the whole graph resolves one major.
- **Breaking:** KumikoUI.Core, KumikoUI.SkiaSharp and every KumikoUI.Uno head now target .NET 10 (`net10.0`, `net10.0-*`); `net9.0` is dropped because .NET 9 support ends 2026-11-10. KumikoUI.Maui already targeted .NET 10. Apps must target .NET 10.
- Supply-chain hardening: Dependabot (NuGet and GitHub Actions, grouped weekly), CodeQL analysis, OpenSSF Scorecard, read-only workflow tokens by default, and every GitHub Action pinned to a commit SHA at its current major (checkout v7, setup-dotnet v6, upload-artifact v7, download-artifact v8, action-gh-release v3).
- Releases are gated: publishing requires approval in the `nuget-publish` environment and passing Core and Uno test suites; `main` and `v*` tags are protected by rulesets kept in `.github/settings/`. CI now runs on every pull request to `main`, including documentation-only changes, and no longer runs for `develop`.
- Package versions come from git tags (MinVer); project files no longer contain `<Version>1.0.0</Version>`, so a local `dotnet pack` now produces a prerelease such as `0.2.0-alpha.0.8` instead of a misleading `1.0.0`. Copyright metadata uses a fixed year, and symbol packages are produced by default.
- KumikoUI.Uno package description no longer claims a Mac Catalyst head (macOS is served by the desktop head).
- KumikoUI.Maui now declares an explicit minimum of `Microsoft.Maui.Controls` 10.0.20 instead of whatever MAUI version the build machine had installed.
- KumikoUI.Uno is built with Uno.Sdk 6.7.30 (was 6.5.31).

### Fixed
- Action buttons (#35):
  - `ICommand.CanExecute` is checked before `Execute`, for the button's `CommandParameter` or the row item. A button that cannot execute is drawn disabled and does nothing when tapped, in the cell and in the `DrawnActionButtons` editor; `CanExecuteChanged` redraws the grid.
  - A command that removes its own row, even the last one, no longer throws from the tap handler: the tap reads nothing about the row after the command runs.
  - A button tap no longer commits an edit through `EditSession` (it raised `CellBeginEdit`/`CellEndEdit` and rebuilt the view).
- Keyboard navigation (#45):
  - Scrolling the current cell into view keeps a scrollable row between the grouping panel, header, top summaries and frozen rows and the pinned bottom summary footer, with every combination of them, and keeps a column between the frozen columns net of the row drag handle (it ignored the handle's width).
  - Tab's search for the next tab stop walks rows, not rows × columns cells, so a read-only grid with many rows no longer freezes on Tab.
  - Enter, like F2 and typing, scrolls the cell into view before the editor opens.
  - KumikoUI.Uno: the hidden keyboard proxy no longer marks every key handled. An unhandled Tab moves focus out of the grid (past both the grid and its proxy), and an unhandled Enter, Escape or F2 reaches the page; a key the proxy left unhandled is not sent to Core a second time by the grid.
  - KumikoUI.Maui (iOS, Mac Catalyst): the native key responder declines Tab when Core would leave it unhandled, so UIKit's focus navigation takes it. On Windows the hidden `Entry` already leaves Tab to the system.
- Editing no longer loses input or leaves two editors (#34):
  - `EditSession.BeginEdit` while an edit is open commits it first and returns false, keeping the open editor, when the commit is refused; the old editor is ended and removed from focus before the new one is created, so the focus manager holds exactly one editor.
  - A refused commit now blocks the gesture that would end the edit: a tap or long press on another cell, Tab, Enter, and Up, Down, Home, End, PageUp or PageDown that the editor leaves alone. The selection stays, the editor stays focused and shows its message. Before, the selection moved while the editor stayed open, or the edit was silently dropped.
  - `ToggleBooleanCell` runs the validation pipeline of `CommitEdit` (`CellValidator`, the write, the item's own validation with rollback, `ValidationFailed`) and raises `CellValueChanged` only after a successful write; it commits an open edit first.
  - `DrawnFocusManager.Unregister` of the focused component now blurs it (`OnLostFocus`, `FocusChanged`).
- Editors never commit a value the user didn't choose (#33). Before, opening an editor and leaving it (a tap elsewhere, Tab, Enter) overwrote the cell and raised `CellValueChanged`:
  - a combo box whose value wasn't in its list wrote null;
  - a scroll picker wrote its first item;
  - a date picker on an empty cell wrote today, and any picked date lost its time of day;
  - a custom editor wrote null.

  Now `CommitEdit` writes only when the editor has a value the user changed, and only when it differs from the original. Combo boxes and pickers start with nothing selected when the cell's value isn't in their list; they match enum values to names. The date picker opens empty on an empty cell, keeps the original time, and reads `DateOnly`. Enter confirms in the date and scroll pickers. A scroll picker no longer coasts past the item its release confirmed, and Up/Down at the ends no longer report a change.
- Typed value conversion and validation (#32):
  - **Conversion to the property type.** Edited text converts to the property's type in `KumikoSource.Culture`: every numeric type, `decimal` at full precision (it went through `double`), enums by name, `Guid`, `DateOnly`, `TimeOnly`, `DateTimeOffset`, `TimeSpan`, `bool`, `char`, `string` and nullable forms. The date picker writes `DateOnly` properties.
  - **No more silent failures.** Out-of-range numbers, fractions in integer properties (they were rounded), unparsable text, blank text for a non-nullable property, and setters that throw are now errors with a message. They used to be dropped silently while the commit reported success.
  - **Item validation sees the new value.** `IDataErrorInfo`/`INotifyDataErrorInfo` are checked after the new value is written, and the original value is restored on an error. Before, they saw the old value.
  - **Both validators run.** A passing `CellValidator` no longer skips the item's own validation.
  - **Warnings are shown.** A first commit shows the warning; committing the same value again accepts it.
  - **Editing text.** Editors show values in the grid's culture, without the display format, so they parse back.
  - **No-value editors.** Editors that hold no value (action buttons), and columns with nothing to write to, end the edit without writing null into the item.
- Filter popup (#49):
  - **Scrolling:** the value list scrolls. It can be dragged, scrolled with the mouse wheel, moved with a draggable scrollbar (a press on its track jumps), and stepped through with the keyboard: Up/Down, PageUp/PageDown, Space to toggle, Enter to apply, Escape to cancel. Previously only the first values were reachable.
  - **Press and release:** an item toggles on release, and only if the pointer didn't move past a small threshold, so dragging no longer flips checkboxes. Buttons act on release over the same button.
  - **Large lists:** only the visible rows are drawn, and the search is applied once per frame however fast you type, narrowing the current matches as the search text grows. A column with 10,000 distinct values opens in about 30 ms.
  - **Search box:** it is a `DrawnTextBox`, so it follows the theme (it was always white), edits by grapheme cluster, supports undo, and drives the text-input contract (`DrawnFilterPopup.SearchTextInput`, `KumikoInputController.ActiveTextInput`).
  - **Following the column:** the popup follows its column when the grid scrolls, and draws one shadow instead of two.
- Popups (`PopupManager`):
  - A press outside a popup closes it and does nothing else: it no longer also selects a cell, starts an edit or sorts. `PopupClosed` reports every close, so `KumikoInputController.FilterPopupClosed` is now raised for an outside tap too.
  - A press inside a popup captures the pointer until release.
  - The mouse wheel reaches the popup under it (`DrawnComponent.OnScroll`).
  - Z-order only increases.
  - `Show` accepts an anchor function, which popups re-read before each frame (`UpdateAnchors`).
- Conditional styles no longer leak (#22). A `RowStyleResolver` text color or font was written into the column's shared `CellStyle` (permanently recoloring the column for every row), and into a `CellStyleResolver` result (corrupting cached instances). The effective cell style is now composed into a separate instance; the column's style and resolver results are never modified. The `cellStyle` passed to `ICellRenderer.Render` may be reused for the next cell, so renderers must not keep or modify it.
- Incremental updates and item change notifications (#21). Adding or removing one item of an `ObservableCollection` now honors `SetFilter` predicates and column filters: an added item the filter rejects no longer becomes a row, and removing a filtered-out item no longer forces a rebuild (the view only shifts its indices; such a change raises a `Redraw`). `PropertyChanged` with a `null` or empty name, with a parent of a nested path (`Address` for a column, group or summary on `Address.City`; the item must raise it, as nested objects are not observed), or with any name while a `SetFilter` predicate is set, now rebuilds the view; two columns sharing a property both count, only an active filter counts, and names match ignoring case, as column bindings do. `DefaultGroupExpanded` rebuilds when grouping is active and the value changes, and `SetFilter(null)`, `ClearAllFilters()` and `SetColumnFilter(column, null)` raise nothing when there was no filter.
- Sorting, grouping and filtering never throw on mixed or non-comparable values (#15). Tapping the header of a Template or action column (whose value is the whole row item) no longer crashes; a column holding both `int` and `double` sorts by value instead of throwing; Min/Max summaries over mixed numeric types work. Sorting is stable, so rows with equal keys keep their source order. Groups are ordered by their key (1, 2, 10; dates chronologically) instead of their header text, and header text uses the column's `Format`. Group buckets, the filter value list and value filters follow the same case rule as sorting, so "Oslo" and "oslo" are one group and one filter value by default.
- Heterogeneous item lists (subclasses mixed in one list, for example a `List<Animal>` of `Dog` and `Cat`) sort, filter, group and summarize correctly: every column property is resolved per item type (fixed by the per-type accessors of #17, now covered by tests). A `null` item in the source collection is skipped, as if a filter rejected it, instead of crashing the first render (#18).
- Editing tracks the edited item and column instead of a visible row index (#31). An edit that outlives a sort, filter, grouping change, collection change or column reorder now commits to the item it was opened on, wherever its row is, instead of writing whatever item sits at the old row (or throwing). An edit whose item is filtered out, collapsed or removed is canceled. Sorting, filtering, group toggles and drag-reorders from the grid commit an open edit first, and are ignored while its value is invalid. The editor overlay moves with its cell when the grid scrolls and hides while the cell is out of view (`UpdateEditorBounds` was never called). `SetCellValue`, `ToggleBooleanCell` and starting an edit ignore rows that are out of range or not data rows. `EditSession` exposes `EditItem` and `EditColumn`, and follows the source's `ViewChanged` by itself while an edit is open.
- ComboBox editors now apply the column's `EditorDisplayMemberPath` and `EditorValueMemberPath`; they were set after the items were built, so items showed `ToString()` and the current value was not selected.
- An unknown font family now falls back to the default family in the requested weight and slant instead of producing a null typeface. Styled typefaces are resolved through `SKFontManager` with an explicit family, which SkiaSharp 4 needs to keep bold and italic.
- Taps on the pinned summary footer no longer select or edit the data row behind it; unpinned bottom summary rows are also reported as `HitRegion.TableSummaryRow`.
- Keyboard navigation keeps the current row fully visible between the header/frozen rows and the pinned footer, and accounts for the grouping panel and top summary rows (previously the scroll offset was counted twice).
- Row drag-handle grips no longer paint over the pinned summary footer.

## [0.1.10] - 2026-05-05

### Added
- `SkiaFontRegistrar`: register custom typefaces (for example CJK or icon fonts) and use them by family name in `GridFont`. Fixes Japanese text and icon fonts rendering garbled on Android (#5, #8).

### Fixed
- Sample: corrected the selection text color property name in `IconFontCellRenderer`.

## [0.1.9] - 2026-05-04

### Added
- Per-column `EditTriggers` on `DataGridColumn`, overriding the grid-level triggers (#6, #7).

## [0.1.8] - 2026-05-03

### Added
- Action button columns: pill buttons in a cell with `ICommand` or callback support, activating on the first tap (#3).
- MIT license and Contributor Covenant Code of Conduct.

## [0.1.7] - 2026-03-06

### Changed
- README: NuGet installation steps in Getting Started.

## [0.1.6] - 2026-03-06

### Changed
- NuGet package icon switched to PNG.

## [0.1.4] - 2026-03-05

### Added
- NuGet package icon.

## [0.1.3] - 2026-03-05

### Added
- First release on NuGet.org of KumikoUI.Core, KumikoUI.SkiaSharp and KumikoUI.Maui: a fully canvas-drawn DataGrid for .NET MAUI with virtual scrolling, text/numeric/boolean/date/combo/picker/image/template columns, sorting, Excel-style filtering, grouping with summaries, editing, selection, keyboard navigation, frozen columns and rows, row drag and drop, column resizing, theming and momentum scrolling.

Versions 0.1.1, 0.1.2 and 0.1.5 were tagged but never published to NuGet.org.

[Unreleased]: https://github.com/TheEightBot/KumikoUI/compare/v0.1.10...HEAD
[0.1.10]: https://github.com/TheEightBot/KumikoUI/compare/v0.1.9...v0.1.10
[0.1.9]: https://github.com/TheEightBot/KumikoUI/compare/v0.1.8...v0.1.9
[0.1.8]: https://github.com/TheEightBot/KumikoUI/compare/v0.1.7...v0.1.8
[0.1.7]: https://github.com/TheEightBot/KumikoUI/compare/v0.1.6...v0.1.7
[0.1.6]: https://github.com/TheEightBot/KumikoUI/compare/v0.1.4...v0.1.6
[0.1.4]: https://github.com/TheEightBot/KumikoUI/compare/v0.1.3...v0.1.4
[0.1.3]: https://github.com/TheEightBot/KumikoUI/releases/tag/v0.1.3

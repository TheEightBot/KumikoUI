# KumikoUI 1.0 API review (#112)

The last pass over the public surface before it is frozen at 1.0. Every public and protected member of the four
shipped packages was read line by line from the recorded API files, which are the authoritative list:

| Package | File(s) reviewed | Lines before | Lines after |
|---|---|---|---|
| KumikoUI.Core | `src/KumikoUI.Core/PublicAPI.Unshipped.txt` | 2378 | 2373 |
| KumikoUI.SkiaSharp | `src/KumikoUI.SkiaSharp/PublicAPI.Unshipped.txt` | 86 | 86 |
| KumikoUI.Maui | `src/KumikoUI.Maui/PublicAPI.Unshipped.txt` (+ `PublicAPI/net10.0-android/`) | 216 + 2 | 217 |
| KumikoUI.Uno | `src/KumikoUI.Uno/PublicAPI.Unshipped.txt` (+ `PublicAPI/<tfm>/` per head) | 216 + 5 per head (7 on Android) | 213 + 5 per head |

The yardsticks are the naming policy in [CONTRIBUTING.md, "Public type names"](../../CONTRIBUTING.md#public-type-names)
(one `Kumiko` prefix, `Drawn` for drawn components, no new `Grid*`/`DataGrid*` types, `eng/TypeNameCheck` clean) and the
.NET Framework Design Guidelines (naming, casing, abbreviations, events, overloads, optional parameters, collections,
sealing). Findings from the release-readiness evaluation that this issue carries are PKG-04 (no API gate for 1.0) and
DOC-13 (naming to settle before 1.0); most of DOC-13 was already done by #37 and #54 (the `Grid*`/`DataGrid*` names,
`RowTappedEventArgs2`, `PaintStyle`, file/type mismatches), and this review checks what is left.

Each finding is marked **fix now** (done in this change; a **Breaking:** entry is in the CHANGELOG) or **keep** (with
the reason). Items that are the maintainer's call are collected in [Decisions for the maintainer](#decisions-for-the-maintainer).
Existing identifiers that use a spelling other than American English for an established name (for example
`InputAction.Cancelled`, `DrawnTextBox.Cancelled`, `DrawnFilterPopup.FilterCancelled`) are kept as they are.

## Summary

**Fixed now** (all breaking unless noted; see the CHANGELOG's Unreleased section):

1. RS0026/RS0027 suppression removed, and both rules are build errors: `KumikoHitTester.HitTest` is one method with two
   optional parameters; `IDrawingContext.DrawTextInRect` keeps a six-parameter primitive without defaults and puts every
   default on the wrapping overload; `KumikoDataGrid.ScrollToColumn` (MAUI and Uno) has four explicit overloads.
2. `latest-recommended` code analysis for the four libraries, with every finding fixed or suppressed in source with a
   justification (details in [AnalysisLevel](#analysislevel-latest-recommended)).
3. `KumikoKeyEventArgs` and `KumikoPointerEventArgs` derive from `EventArgs` (CA1711).
4. Host parity: `KumikoRenderMode` has the same members and values on MAUI and Uno; `MauiActionButtonDefinition` uses
   MAUI `Color` like Uno's definition uses `Windows.UI.Color`; `UnoActionButtonDefinition` is sealed like the MAUI one.
5. Not public any more: `UseSkiaKumikoUI()` (removed), Uno's `InputMapping`, `UnoActionButtonDefinition.Changed`,
   `ActionButtonLayout`, `PaintCache.BackgroundPaint`, the setter of `KumikoSelectionModel.IsEditing`, and the generated
   Android `Resource` classes of both hosts.
6. Shape and naming: the selection navigation methods (unused `totalColumns` parameters, `NavigatePageUp` argument
   order, `ctrl`), `ComputedSummaryRow.Values`/`RawValues` read-only, `KumikoColor.ToUInt32`, `KumikoMomentumPreset.IOS`,
   `KumikoStyle` sealed, and abbreviated parameter names on event-argument constructors and a few methods.
7. `InternalsVisibleTo("SampleApp.Maui")` replaced by a read-only `KumikoInputController.ActivePopup` (additive).
8. `eng/check-uno-package.sh` passes: the HarfBuzzSharp native-asset dependencies are intended and allow-listed.

**Kept with a reason** (selected; every type is listed below): the unprefixed descriptive type names the policy allows,
the `ctx` parameter name, `Ms`-suffixed timing members, `EventHandler<KumikoCursor>`/`EventHandler<KumikoSortDirection>`,
the public host-contract members of `KumikoInputController`/`KumikoRenderer`/`KumikoLayoutEngine`, Uno's generated
`GlobalStaticResources`, `KumikoSelectionMode.Single`, and the `#AARRGGBB` order of `KumikoColorTypeConverter`.

**Open for the maintainer:** the 8-digit hex order, the legacy `(value, bounds)` editor-factory path, exact-version
dependencies between the packages, and the remaining doc follow-ups. See [Decisions for the maintainer](#decisions-for-the-maintainer).

## Cross-cutting findings

| # | Area | Finding | Verdict | Reason |
|---|---|---|---|---|
| X1 | Type prefixes | Four families remain: `Kumiko*` (grid types), `Drawn*` (drawn components), `Skia*` (the SkiaSharp backend: `SkiaDrawingContext`, `SkiaResourceCache`, `SkiaFontRegistrar`), host-named (`MauiActionButtonDefinition`, `UnoActionButtonDefinition`), plus unprefixed descriptive names (`FilterDescription`, `EditSession`, `HitRegion`, `InputAction`, `InputModifiers`, `PointerButton`, `ScrollState`, `ScrollSettings`, `InertialScroller`, `PaintCache`, the `*EventArgs` of grid events...). | keep | The policy prefixes a type when its simple name could clash with another UI framework's (`KumikoKey`, `KumikoKeyEventArgs`, `KumikoSelectionMode`, `KumikoTextChangedEventArgs`) and allows descriptive unprefixed names when `eng/TypeNameCheck` passes, which it does. `Skia*` and `Maui*`/`Uno*` name the backend or host the type belongs to. Renaming the unprefixed set (about 60 types) would churn every doc and sample for no clash avoided. |
| X2 | `Grid*`/`DataGrid*` | No public type starts with `Grid` or `DataGrid`. Member names `GridStyle`, `GridSelectionMode`, `GridDescription`, `GridHint` on the hosts remain. | keep | Members, not types; `GridSelectionMode`/`GridStyle` avoid the `SelectionMode`/`Style` members the base `ContentView`/`UserControl` already has. |
| X3 | Abbreviations in parameters | `row`/`col`, `index`, `old`/`new`, `oldH`/`oldM`, `a`/`b`, `ctrl`/`ctrlHeld`, `hAlign`/`vAlign` | fix now | FDG: no abbreviations; the event-args constructors now name their parameters after the properties they fill (`rowIndex`, `columnIndex`). Only callers with named arguments are affected. |
| X4 | `ctx` | 22 public members take `IDrawingContext ctx` (`ICellRenderer.Render`, `DrawnComponent.OnDraw`, `IKumikoIconProvider.DrawIcon`, `KumikoRenderer.Render`, ...). | keep | One consistent, widely understood abbreviation across the whole rendering surface; overriders choose their own parameter names, so the only effect of a rename would be on named-argument calls, and ApiCompat does not treat a parameter rename as breaking. It can be revisited after 1.0 without a major version. |
| X5 | Units in names | `TimestampMs`, `FrameTimeMs`, `frameIntervalMs`, `FlingHoldCutoffMs`, `RepeatDelayMs`, `RepeatIntervalMs`, `MinRepeatIntervalMs` are numbers in milliseconds; `KumikoFrameStats.UpdateInterval` is a `TimeSpan`. | keep | Per-frame and input-timestamp values are used in arithmetic in the render loop and come from platform timestamps in milliseconds; the unit in the name is unambiguous. `UpdateInterval` is an application setting, where `TimeSpan` reads best. |
| X6 | Events | Every public event is `EventHandler` or `EventHandler<T>` (since #37). Two use a non-`EventArgs` `T`: `KumikoInputController.CursorChanged` (`EventHandler<KumikoCursor>`) and `DrawnFilterPopup.SortRequested` (`EventHandler<KumikoSortDirection>`). | keep | The generic constraint on `T` was removed in .NET 4.5; wrapping one enum value in an args class adds an allocation per event for no information. Decided in #37. |
| X7 | EventArgs base type | `KumikoKeyEventArgs` and `KumikoPointerEventArgs` did not derive from `EventArgs` (CA1711). | fix now | They are the arguments of the input pipeline and of `DrawnComponent.On*`; deriving from `EventArgs` costs nothing and makes the `*EventArgs` suffix truthful. |
| X8 | Nullability | No oblivious (`~`) entry in any API file; every reference type is annotated. | keep | Nothing to change. |
| X9 | Sealing | Since #37 the leaf types are sealed. Left open: `KumikoStyle` (no virtual members, never subclassed), `UnoActionButtonDefinition` (its MAUI twin is sealed). | fix now | Sealed both. The remaining open classes are documented extension points: `DrawnComponent`, `DrawnActionButtons`, `EditorDescriptor`, the cell renderers, `KumikoIconProvider`, `KumikoFontIconProvider`, `KumikoImageLoader`, and the two `KumikoDataGrid` controls. |
| X10 | Optional parameters | RS0026/RS0027 were suppressed repo-wide. | fix now | See [RS0026/RS0027](#rs0026rs0027-overload-sets). |
| X11 | Mutable collections in the API | `ComputedSummaryRow.Values`/`RawValues` exposed mutable `Dictionary<,>` of computed results. Configuration collections stay mutable (`TableSummaryRow.Columns`, `GroupSummaryRow.Columns`, `CaptionSummaryDescription.Columns`, `FilterDescription.SelectedValues`, the `Kumiko*LayoutState` lists, the hosts' `ObservableCollection` properties). | fix now (results) / keep (configuration) | Results are read-only snapshots; configuration objects are filled from XAML and code, and the layout-state types are JSON DTOs. |
| X12 | Hosts depend on Core internals | KumikoUI.Maui and KumikoUI.Uno use Core internals through `InternalsVisibleTo` (`KumikoInputController.PopupManager`, the internal `KumikoRenderer.Render` overload that draws popups, the drawn focus manager, `KumikoSelectionModel.IsEditing`'s setter). Their NuGet dependency on KumikoUI.Core is `>= version`. | decision | Mixing KumikoUI.Maui 1.0.0 with a later KumikoUI.Core could fail at run time with `MissingMethodException`. See [decision D3](#decisions-for-the-maintainer). |

## KumikoUI.Core

### `KumikoUI.Core` (root namespace)

| Type | Findings | Verdict |
|---|---|---|
| `KumikoRenderer` | `Render(ctx, dataSource, scroll, selection, style, dragColumnIndex = -1, dragColumnScreenX = 0, dragRowIndex = -1, dragRowScreenY = 0, editSession = null)`: the drag arguments repeat state the renderer could read from `InputController`. `GetCellRenderer`/`SetCellRenderer`, `AutoFitColumn`, `FrameStats`, `ShowFrameStats`, `ImageResolver`, `RedrawRequested` are fine. | keep: one overload, RS0026/RS0027 clean; it is the host contract a custom host calls once per frame. Folding the drag state into the renderer is a possible post-1.0 addition (a new overload without the drag arguments), not a reason to break hosts now. |

### `KumikoUI.Core.Components`

| Type | Findings | Verdict |
|---|---|---|
| `DrawnComponent` | Public virtual `On*` input handlers (`OnPointerDown`, `OnKeyDown`, ...) rather than protected: the input pipeline calls them from outside. `RaiseValueChanged`/`RaiseEditCompleted` are protected. | keep: the documented base class for custom editors; the `On*` methods are the pipeline's entry points, not event raisers. |
| `DrawnTextBox` | `Cancelled` event (British spelling), `SetMeasureContext(ctx)`, `InputValidator`, `OutputFormatter`, composition members (`SetComposition`, `CommitComposition`, `CancelComposition`). | keep: existing identifier; the rest is the `ITextInputSession` contract. |
| `DrawnComboBox`, `DrawnComboBoxItem` | `AddItem(displayText, value = null)` single overload with an optional parameter; `DisplayMemberPath`/`DisplaySelector`, `ValueMemberPath`/`ValueSelector` pairs (XAML path or code delegate). | keep. |
| `DrawnDatePicker` | `SelectedDate` (nullable date), `SelectedDateTime` (date plus time), `TimeOfDay`, `SelectedHour`, `SelectedMinute` overlap. | keep: each serves one editor mode (date, date-time, range); documented. |
| `DrawnNumericUpDown` | `RepeatDelayMs`, `RepeatIntervalMs`, `MinRepeatIntervalMs` (see X5). | keep. |
| `DrawnScrollPicker` | `SetItems(IEnumerable<string>)` and `SetItems(IReadOnlyList<string>, IReadOnlyList<object?>)`; `SelectedValue` (text) vs `SelectedItemValue` (value). | keep: no optional parameters; names match `Items`/`ItemValues`. |
| `DrawnFilterPopup` | `FilterCancelled` (spelling), read-only `VisibleValues`, `CheckedValues`, `ValueCount`, `ListScrollOffset`, `SearchTextInput`. | keep: existing identifier; the read-only members are what UI tests and the DevFlow extension read. |
| `DrawnActionButtons` | Open class (extension point), `Buttons` is `IList<IActionButtonDefinition>`. | keep. |
| `ComboBoxSelectionChangedEventArgs` | Constructor parameters `old`, `@new`. | fix now: `oldItem`, `newItem`. |
| `KumikoTimeChangedEventArgs` | Constructor parameters `oldH`, `oldM`, `newH`, `newM`. | fix now: `oldHour`, `oldMinute`, `newHour`, `newMinute`. |
| `ClipboardRequestEventArgs`, `ClipboardTextEventArgs`, `ComponentValueChangedEventArgs`, `DateRangeSelectedEventArgs`, `DateSelectedEventArgs`, `KumikoTextChangedEventArgs`, `PickerSelectionChangedEventArgs` | Sealed, derive from `EventArgs`, read-only except the documented `Handled`/`IsPending`/`Text` request members. | keep. |
| `ValidationState` | `None = 0`, `Valid`, `Warning`, `Error`. | keep. |

### `KumikoUI.Core.Data`

| Type | Findings | Verdict |
|---|---|---|
| `KumikoGridItemAttribute` | Contains "Grid" in a type name, but as `Kumiko` + `GridItem` + `Attribute`; the source generator's trigger. | keep: prefixed; renaming would break every annotated model class. |
| `IKumikoAccessorProvider`, `KumikoAccessorRegistry`, `KumikoPropertyAccessor` | Generator contract; `ReflectionSwitchName` const and `IsReflectionPropertyAccessSupported` switch. | keep. |
| `KumikoValueConverter` | Two `TryConvert` overloads (with and without `KumikoStrings`), no optional parameters. | keep. |

### `KumikoUI.Core.Editing`

| Type | Findings | Verdict |
|---|---|---|
| `EditSession` | `IsCellBeingEdited(row, col)` and `ToggleBooleanCell(row, col, ...)` abbreviations. `UpdateAnimation(frameIntervalMs = 16)`, `DrawEditor(ctx)`, `HandleKeyEvent`/`HandlePointerEvent`, `CellValidator` (`Func<int, int, object?, CellValidationResult>`). | fix now: `rowIndex`, `columnIndex`. The rest keep (host contract). |
| `CellBeginEditEventArgs`, `CellEndEditEventArgs`, `CellValueChangedEventArgs` | Constructor parameters `row`, `col` while the properties are `RowIndex`, `ColumnIndex`. | fix now: `rowIndex`, `columnIndex`. |
| `CellValidationEventArgs`, `CellValidationResult` | `CellValidationResult` is a mutable class with `Valid`, `Error(msg)`, `Warning(msg)` factories. | keep: the result is created by user validators; immutability would break object-initializer use for no safety gain. |
| `CellEditorContext` | Positional record (`Value`, `Bounds`, `InitialCharacter = null`, `Culture = null`). | keep. |
| `EditorDescriptor`, `TextEditorDescriptor`, `NumericUpDownEditorDescriptor` | `abstract CreateEditor(object? currentValue, KumikoRect cellBounds)` plus `virtual CreateEditor(CellEditorContext)` that forwards to it. | keep for now; see [decision D2](#decisions-for-the-maintainer). |
| `CellEditorFactory` | Public statics (`CreateEditor`, `GetEditorValue`, `Apply*ToEditor`) used only inside Core today. | keep: a custom `EditorFactory` can create the default editor for a column and adjust it. |
| `IValueEditor` | `HasValue`, `IsDirty`, `Value`. | keep. |
| `EditTrigger` | `[Flags]` with `Default = DoubleTap \| F2Key \| Typing`, `F2Key` contains a digit. | keep: names the key. |
| `EditTextSelectionMode`, `ValidationSeverity` | `ValidationSeverity.Error = 0` is the default value. | keep: an error is the safe default for an unset severity. |

### `KumikoUI.Core.Input`

| Type | Findings | Verdict |
|---|---|---|
| `KumikoHitTester` | Two `HitTest` overloads, the shorter with an optional `dataSource` (RS0027). | fix now: one method, `HitTest(viewportX, viewportY, scroll, style, columns, totalRows, dataSource = null, deviceType = Mouse)`. |
| `KumikoInputController` | Large host contract: `HandlePointer`/`HandleKey`, `Update*` per-frame methods, `Drag*` read-only state the renderer and hosts read, `WouldHandleTab`, `CopySelection`, `EditSession` setter, events. `PopupManager` internal. | keep (host contract; see X12). **Added** `ActivePopup` (read-only), see [InternalsVisibleTo](#internalsvisibleto-for-sampleappmaui). |
| `KumikoKeyEventArgs`, `KumikoPointerEventArgs` | Not derived from `EventArgs` (CA1711). `HasCommand` means Ctrl or Meta (the platform's shortcut modifier). `TimestampMs`. | fix now: derive from `EventArgs`. `HasCommand` and `TimestampMs` keep (documented; X5). |
| `KumikoMomentumPreset` | `Ios` member. | fix now: `IOS`, as `OperatingSystem.IsIOS()` and the `ios` platform name. |
| `InertialScroller`, `ScrollSettings` | `StartFling()` and `StartFling(long releaseTimestampMs)`, `TrackVelocity(dx, dy, timestampMs)`, `Update(frameIntervalMs = 16)`, `FlingHoldCutoffMs`. | keep (no optional parameters on two overloads; X5). |
| `RowTappedEventArgs` | Constructor `(row, col, item)`. | fix now: `(rowIndex, columnIndex, item)`. |
| `AutoFitColumnEventArgs`, `ColumnResizedEventArgs` | Constructor parameter `index`. | fix now: `columnIndex`. |
| `RowReorderedEventArgs` | Two constructors (with and without `KumikoReorderResult`); also the args of `RowReorderRejected`. | keep. |
| `KumikoCellTappedEventArgs`, `KumikoColumnReorderedEventArgs`, `KumikoContextRequestedEventArgs`, `KumikoHeaderTappedEventArgs`, `KumikoHapticEventArgs` | Prefixed (clash with WinForms/WinUI names) while `RowTappedEventArgs` is not. | keep: the prefix rule is clash-driven (X1). |
| `KumikoHitTestResult` | `readonly record struct` with `init` members and `Empty`. | keep. |
| `KumikoTextRange` | `FromBounds(int a, int b)`. | fix now: `FromBounds(int start, int end)` (either order). |
| `ITextInputSession`, `IKumikoClipboard` | `IKumikoClipboard.GetTextAsync` / `TrySetText`. | keep. |
| `KumikoTsvFormatter` | Public static `Format`, `EscapeField`, `RowSeparator`. | keep: lets an app export a selection as the grid copies it. |
| `HitRegion`, `InputAction`, `InputModifiers`, `PointerButton`, `KumikoKey`, `KumikoCursor`, `KumikoContextTrigger`, `KumikoHapticKind`, `KumikoPointerDeviceType`, `KumikoScrollUnit` | `InputAction.Cancelled` (spelling), `KumikoKey` has only the letters the grid handles (A, C, V, X, Y, Z), `InputModifiers` is `[Flags]` with `None = 0`. | keep: adding enum members later is not breaking. |

### `KumikoUI.Core.Layout`

| Type | Findings | Verdict |
|---|---|---|
| `KumikoLayoutEngine` | Eight public instance members do not use instance state (CA1822: `GetContentWidth`, `GetFrozenColumnWidth`, `GetRightFrozenColumnWidth`, `GetContentHeight`, `GetFrozenRowHeight`, `GetVisibleRowRangeWithOffset`, `PopulateVisibleFrozenColumns`, `PopulateVisibleScrollableColumns`). Three `ComputeColumnWidths` and two `CalculateAutoFitWidth` overloads without optional parameters. Tuple returns `(KumikoColumn Column, float X, int OriginalIndex)`. | keep: making them static would be a breaking change and would rule out instance caching later; CA1822 is scoped to private and internal members (`.editorconfig`). |
| `KumikoScroller` | Static helpers with one optional `position` each. | keep. |
| `KumikoKeyboardAvoidance` | Static helpers used by both hosts; `DefaultMargin` const. | keep. |

### `KumikoUI.Core.Models`

| Type | Findings | Verdict |
|---|---|---|
| `KumikoSource` | The central model; `BeginUpdate()` returns `KumikoUpdateScope`; `GetUniqueColumnValues` returns a new `List<string>` per call; `SetSort(column, direction, order = 0)`. | keep. |
| `KumikoColumn` | `IsFrozen`/`IsFrozenRight`/`FreezeMode` overlap; flat `Editor*` and `Progress*` properties; `CustomEditorFactory` and `EditorFactory`; read-only runtime state (`SortDirection`, `SortOrder`, `ContentFitWidth`, `HeaderFitWidth`, `ActiveFilter`). | keep: the booleans are XAML conveniences over `FreezeMode`; flat properties are XAML-friendly. `CustomEditorFactory`: [decision D2](#decisions-for-the-maintainer). |
| `KumikoSelectionModel` | `NavigateLeft(totalColumns, shift)`/`NavigateRight(totalColumns, shift)`/`NavigateEnd(..., totalColumns, ...)` with an unused parameter documented as "kept for compatibility"; `NavigatePageUp(pageSize, shift, totalRows)` vs `NavigatePageDown(pageSize, totalRows, shift)`; `NavigateHome(ctrlHeld, ...)`; `HandleRowSelection(row, shift, ctrl)`; `IsEditing { get; set; }` is runtime state the input controller owns. | fix now: `NavigateLeft(shift)`, `NavigateRight(shift)`, `NavigateHome(totalRows, shift, control)`, `NavigateEnd(totalRows, shift, control)`, `NavigatePageUp(pageSize, totalRows, shift)`, `HandleRowSelection(row, shift, control)`; `IsEditing { get; internal set; }`. Every old call is a compile error, never a silent change of meaning. |
| `KumikoSelectionMode` | `Single` trips CA1720. | keep: the standard name (WinUI `ListViewSelectionMode.Single`, MAUI `SelectionMode.Single`); suppressed in source with a justification. |
| `KumikoStyle` | Not sealed, no virtual members, never subclassed. `AlternateRowBackground` (bool) beside `AlternateRowColor`; `KumikoStyleOptions.AlternateRowBackground` and the hosts' `AlternateRowBackground` are colors. | fix now: sealed. Names: keep; the host property follows the XAML "Background is a color" idiom and the style's switch/color pair is documented; renaming is cosmetic against README, samples and tests. |
| `KumikoStyleOptions` | The nullable bridge from host properties to `KumikoStyle`. | keep. |
| `ComputedSummaryRow` | `Values`/`RawValues` mutable `Dictionary<,>`. | fix now: `IReadOnlyDictionary<,>` (still case-insensitive). |
| `ComputedSummaryValue`, `GroupHeaderInfo` | `init`-only results (`GroupHeaderInfo.IsExpanded` is settable by the source). | keep. |
| `CellPosition` | `Row`/`Column` rather than `RowIndex`/`ColumnIndex`; `Invalid`, `IsValid`, equality operators. | keep: a coordinate pair, named like `KumikoPoint.X`/`Y`. |
| `KumikoAppearance` | Host theme state machine (`ThemeAssigned`, `StyleAssigned`, `SystemThemeChanged`, `FollowAssigned` return the theme to apply). Method names read like events. | keep: used by both hosts and by any custom host that follows the system theme; making it internal would add to X12. |
| `KumikoAnnouncements`, `KumikoCommands`, `KumikoStrings` | Host helpers (screen-reader text, `TryExecute`) and localizable strings with `init` members. | keep. |
| `KumikoSelectionBinder` | Two-way `SelectedItem(s)` binding helper used by both hosts. | keep. |
| `KumikoScrollChangedEventArgs`, `KumikoScrolledEventArgs` | Two scroll args (state change vs host `Scrolled` event with extents). Constructor orders differ: `(oldX, oldY, x, y)` vs `(x, y, oldX, oldY, ...)`. | keep: reordering parameters of the same type would silently swap values for existing callers; the names are documented. |
| `KumikoLayoutState`, `KumikoColumnLayoutState`, `KumikoFilterLayoutState`, `KumikoFilterConditionLayoutState`, `KumikoGroupLayoutState` | Mutable JSON DTOs with `Version`/`CurrentVersion`. | keep. |
| `FilterDescription`, `FilterCondition`, `GroupDescription`, `SummaryColumnDescription`, `TableSummaryRow`, `GroupSummaryRow`, `CaptionSummaryDescription` | `FilterDescription.Condition1`/`Condition2` (numbered names, mirroring the two-condition filter UI); `TableSummaryRow(name, position = Bottom)`. | keep: the filter UI has exactly two conditions. |
| `FilterChangedEventArgs`, `FilterChangingEventArgs`, `GroupExpandedChangedEventArgs`, `GroupExpandingEventArgs`, `SortColumnsChangedEventArgs`, `SortColumnsChangingEventArgs`, `KumikoSelectionChangedEventArgs`, `KumikoViewChangedEventArgs` | Sealed, `EventArgs`-derived, `Cancel` on the `*ing` ones. | keep. |
| `ScrollState` | Mutable `OffsetX/Y`, viewport and content sizes, `BottomInset`. | keep: host-owned state. |
| `KumikoTheme`, `KumikoThemeMode`, `KumikoUpdateScope`, `KumikoComparer` | — | keep. |
| Enums `ColumnFreezeMode`, `ColumnSizeMode`, `DateFilterOperator`, `NumericFilterOperator`, `TextFilterOperator`, `DragHandlePosition`, `FilterCombination`, `GroupSummaryLevels`, `KumikoColumnType`, `KumikoFilterValueKind`, `KumikoReorderResult`, `KumikoScrollPosition`, `KumikoSortDirection`, `KumikoViewChangeKind`, `SummaryPosition`, `SummaryType` | `*FilterOperator.Equals` hides `object.Equals` as an enum member name (allowed); `KumikoReorderResult.NoOp`. | keep. |

### `KumikoUI.Core.Rendering`

| Type | Findings | Verdict |
|---|---|---|
| `IDrawingContext` | Two `DrawTextInRect` overloads, both with optional parameters (RS0026/RS0027); `hAlign`/`vAlign`; `DrawImage(object image, KumikoRect destRect)`. | fix now: the six-parameter primitive has no defaults, the wrapping overload has all of them; `horizontalAlignment`/`verticalAlignment`. `destRect`: keep (reads as "destination rectangle", one occurrence). |
| `ICellRenderer`, `IInteractiveCellRenderer`, `ICellEditorProvider`, `IKumikoIconProvider`, `IKumikoImageResolver`, `IActionButtonDefinition` | `ICellEditorProvider` has `CreateEditor(value, bounds)` plus a default-implemented `CreateEditor(CellEditorContext)`. | keep; [decision D2](#decisions-for-the-maintainer). |
| `ActionButtonLayout` | Public static layout helper that allocates a list per call, used only inside Core. | fix now: internal. |
| `PaintCache` | `BackgroundPaint(color)` is a public static that allocates a new paint on a type whose purpose is caching; used only by tests. | fix now: internal. The cached paints stay public (`CellRenderContext.Paints`). |
| `KumikoColor` | `ToUint32()`; a small set of named colors (`CornflowerBlue`, `DodgerBlue`, `WhiteSmoke`, ...). | fix now: `ToUInt32()` (FDG casing of `UInt32`). Named colors keep. |
| `KumikoColorTypeConverter` | 8-digit hex is `#AARRGGBB`; issue #37 asked for `#RRGGBBAA`. | keep (behavior unchanged); [decision D1](#decisions-for-the-maintainer). |
| `KumikoFont`, `KumikoFontMetrics`, `KumikoPoint`, `KumikoRect`, `KumikoSize`, `KumikoTextLines`, `KumikoFrameStats` | `KumikoRect.Contains(float px, float py)`; `KumikoFont(family, size, bold = false, italic = false)`. | keep. |
| `KumikoPaint` | Mutable, sealed. | keep: reused across draw calls without allocating. |
| `KumikoTextLayout`, `KumikoTextMeasurer` | Public static text layout used by the default `DrawTextInRect` and by custom contexts. | keep. |
| `CellRenderContext` | Two constructors (with and without `IKumikoImageResolver`), no optional parameters. | keep. |
| `ActionButtonDefinition`, `ActionButtonsCellRenderer`, `BooleanCellRenderer`, `ImageCellRenderer`, `ProgressBarCellRenderer`, `TextCellRenderer` | Open renderers (extension points). | keep. |
| `KumikoIconProvider`, `KumikoFontIconProvider`, `KumikoIcon`, `KumikoImageStatus`, `KumikoTextAlignment`, `KumikoTextWrap`, `KumikoVerticalAlignment` | — | keep. |

## KumikoUI.SkiaSharp

| Type | Findings | Verdict |
|---|---|---|
| `SkiaDrawingContext` | Mirrors `IDrawingContext.DrawTextInRect`. | fix now (with the interface). |
| `SkiaResourceCache` | Three constructors (none, three limits, four limits), `Default*` consts, counters. | keep. |
| `SkiaFontRegistrar` | Static registry (`RegisterTypeface*`, `TryGetTypeface`, `SetFallbackFamilies`, `Version`). | keep: process-wide fonts are global in SkiaSharp too. |
| `KumikoImageCache`, `KumikoImageLoader`, `IKumikoImageLoader` | `KumikoImageLoader` open with virtual `OpenAsync`/`TryGetCacheKey`; `OpenUriAsync` static. | keep. |
| Prefixes | `Skia*` vs `Kumiko*` in one package. | keep (X1): `Skia*` types are the backend, `Kumiko*` types the image features any backend could offer. |

The package passes `latest-recommended` without changes.

## KumikoUI.Maui

| Type | Findings | Verdict |
|---|---|---|
| `KumikoDataGrid` | `ScrollToColumn` overload pair with optional `position` (RS0026); CA1001 (owns disposable fields). | fix now: four explicit overloads. CA1001: keep, suppressed with a justification (a control is not disposed by its owner; resources are released when the handler disconnects). |
| `KumikoRenderMode` | `Gpu` while Uno says `Hardware`. | fix now: `Hardware` (value 2, unchanged). |
| `KumikoUIHostingExtensions` | `UseSkiaKumikoUI()` obsolete, documented as "will be removed before 1.0". | fix now: removed. |
| `MauiActionButtonDefinition` | Colors are `KumikoColor` (text form `R,G,B`), unlike every other MAUI color property and unlike the Uno definition. | fix now: `Microsoft.Maui.Graphics.Color`; the `IActionButtonDefinition` colors are cached `KumikoColor`s. |
| `KumikoFonts` | `RegisterFromAppPackageAsync((Family, FileName))` vs Uno's `(Family, AppxUri)`. | keep: the platforms address package files differently. |
| `KumikoUI.Maui.Resource` (Android head) | Generated Android resource designer class, public. | fix now: `AndroidGenerateResourceDesigner=false` (the library has no Android resources). |

## KumikoUI.Uno

| Type | Findings | Verdict |
|---|---|---|
| `KumikoDataGrid` | `ScrollToColumn` (as MAUI); CA1001; CA1010 (Uno's `UserControl` enumerates its children). | fix now (overloads); CA1001/CA1010 keep, suppressed with justifications. |
| `KumikoRenderMode` | `Hardware = 1`, `Software = 2` while MAUI has `Software = 1`. | fix now: `Software = 1`, `Hardware = 2`. |
| `UnoActionButtonDefinition` | Not sealed; public `Changed` event used only by the grid; CA1010. | fix now: sealed, `Changed` internal. CA1010 suppressed (framework-imposed). |
| `KumikoUI.Uno.Input.InputMapping` | Public static mapping (`ToGridKey`, `ToInputModifiers(mods)`, `GetLiveModifiers`); MAUI's equivalent is internal. | fix now: internal (the tests keep access through `InternalsVisibleTo`). |
| `KumikoFonts` | See MAUI. | keep. |
| `KumikoUI.Uno.Resource` (Android head) | Generated, public. | fix now: `AndroidGenerateResourceDesigner=false`. |
| `KumikoUI.Uno.GlobalStaticResources` (every head) | Generated by the Uno XAML generator, `public sealed`. | keep, see [GlobalStaticResources](#globalstaticresources-uno). |

### Host parity (KumikoDataGrid)

The two `KumikoDataGrid` controls were compared member by member (properties, bindable/dependency properties, events,
methods). After this change they have the same members with the same names, parameter lists and Core types; the
remaining differences are platform types or framework overrides (184 recorded entries each, counting accessors, fields,
events and methods):

| Member | MAUI | Uno | Verdict |
|---|---|---|---|
| `AlternateRowBackground` | `Microsoft.Maui.Graphics.Color?` | `Windows.UI.Color?` | keep: native color type on each host. |
| `*Property` fields | `BindableProperty` | `DependencyProperty` | keep. |
| `RenderMode`, `ActiveRenderMode` | `KumikoUI.Maui.KumikoRenderMode` | `KumikoUI.Uno.KumikoRenderMode` | **fixed**: same member names and values. |
| `ScrollToColumn` | two overloads with a default | same | **fixed** on both: four overloads. |
| Overrides | `OnHandlerChanged`, `OnHandlerChanging`, `OnPropertyChanged` | `OnCreateAutomationPeer` | keep: framework hooks. |
| Hosting | `UseKumikoUI()` | none needed | keep. |
| Action button definitions | `MauiActionButtonDefinition` (`BindableObject`, no `BindingContext` from the grid) | `UnoActionButtonDefinition` (`FrameworkElement`, gets the grid's `DataContext`) | colors and sealing **fixed**; the `BindingContext` hand-off is a behavior gap, listed as follow-up F3. |

## Specific items

### GlobalStaticResources (Uno)

The Uno XAML source generator (`Uno.UI.SourceGenerators`, Uno.WinUI 6.7) emits `public sealed partial class
GlobalStaticResources` in the root namespace of every Uno project, hard-coded as `public` in the generator (no MSBuild
property or attribute controls its accessibility). It cannot be internal: an app head's generated start-up code calls
`global::<Library>.GlobalStaticResources.Initialize()`, `RegisterDefaultStyles()` and
`RegisterResourceDictionariesBySource()` for each referenced Uno library, so the type is called across assemblies. The
only way to drop it would be to switch off Uno's source generators for the library (`ShouldRunGenerator=false`), which
also drops the dependency-object and bindable-metadata generation the control relies on. **Keep**: it stays recorded in
the per-head `PublicAPI/<tfm>/PublicAPI.Unshipped.txt` files, is marked `[EditorBrowsable(Never)]` by the generator,
and `eng/update-public-api.sh` writes it there. `eng/TypeNameCheck` does not flag it (no other framework has the name).

The Android `Resource` designer classes of both hosts were a similar generated surface; unlike `GlobalStaticResources`
they can be switched off, and are.

### RS0026/RS0027 overload sets

The repo-wide `NoWarn` covered four sets. All four are reshaped and the rules are now errors (`Directory.Build.targets`):

| Set | Before | After |
|---|---|---|
| `KumikoHitTester.HitTest` | `(…, dataSource = null)` and `(…, dataSource, deviceType)` | `(…, dataSource = null, deviceType = Mouse)` |
| `IDrawingContext.DrawTextInRect` | `(…, hAlign = Left, vAlign = Center, clip = true)` and `(…, hAlign, vAlign, clip, wrap)` | `(…, horizontalAlignment, verticalAlignment, clip)` (no defaults; the primitive an implementation provides) and `(…, horizontalAlignment = Left, verticalAlignment = Center, clip = true, wrap = None)` (default interface member) |
| `SkiaDrawingContext.DrawTextInRect` | as the interface | as the interface |
| `KumikoDataGrid.ScrollToColumn` (MAUI, Uno) | `(int columnIndex, position = Nearest)` and `(KumikoColumn column, position = Nearest)` | `(int)`, `(int, position)`, `(KumikoColumn)`, `(KumikoColumn, position)` |

Calls with three to five arguments to `DrawTextInRect` bind to the wrapping overload, which forwards to the primitive
when `wrap` is `None`; existing calls compile unchanged. A custom `IDrawingContext` still implements the six-parameter
method (now without defaults on the interface).

### AnalysisLevel latest-recommended

Adopted for KumikoUI.Core, .SkiaSharp, .Maui and .Uno (their project files); tests, samples and tools stay on `latest`
so test-name and sample conventions are not forced. Every buildable head builds without analyzer warnings.

| Rule | Where | Action |
|---|---|---|
| CA1711 | `KumikoKeyEventArgs`, `KumikoPointerEventArgs` | Derive from `EventArgs`. |
| CA1720 | `KumikoSelectionMode.Single` | Suppressed on the member with a justification (standard selection-mode name). |
| CA1305 | `string.Format` in `SummaryText.BuildCaption` | Formats with the source's `Culture` (it already had it); a caption template is now applied with the grid's culture instead of the thread's. |
| CA1822 (12) | 8 public `KumikoLayoutEngine` members; private/internal helpers in `KumikoHitTester` (1) and `KumikoInputController` (5, two of them surfaced once their callers were static), MAUI `GetPointerButton` | Public: keep instance members; `dotnet_code_quality.CA1822.api_surface = private, internal` in `.editorconfig`. Private/internal: made static. MAUI `GetPointerButton`: suppressed (instance on Android only). |
| CA1805 (2) | `KumikoStyle.AllowRowDragDrop`, `ShowRowDragHandle` | Removed the redundant `= false`. |
| CA1845 | `DrawnNumericUpDown` minus-sign parsing | `string.Concat(span, span)`. |
| CA1859 | `CellEditorFactory.CreateNumericEditor` (private) | Returns `DrawnTextBox`. |
| CA1001 | MAUI and Uno `KumikoDataGrid` | Suppressed with a justification (controls are not disposed by their owners). |
| CA1010 | Uno `KumikoDataGrid`, `UnoActionButtonDefinition` | Suppressed: `IEnumerable` comes from Uno's `FrameworkElement`. |
| CA2215 | MAUI `KumikoGpuViewSurface.Dispose` (Android) | Calls `base.Dispose()`. |

Also fixed while there: the ambiguous `cref` to `SkiaFontRegistrar.RegisterTypefaceFromStream` (CS0419) in both hosts'
`KumikoFonts`. Not changed: CA1422 on `UIImpactFeedbackGenerator` (iOS 17.5 obsoletion, MAUI and Uno haptics), which is
reported at every analysis level and belongs to the haptics owner.

### InternalsVisibleTo for SampleApp.Maui

Core granted `InternalsVisibleTo("SampleApp.Maui")` so the DevFlow extension could read `PopupManager` and find the open
popup by hit-testing a lattice of points. A shipped assembly that trusts a sample's assembly name lets any assembly with
that name use Core's internals, and the sample's code reached past the public API. Replaced with a small read-only
member, **`KumikoInputController.ActivePopup`** (`DrawnComponent?`, the topmost open popup), which also serves apps and
UI tests that want to know whether a filter popup is open. The DevFlow extension uses it; the `InternalsVisibleTo` is
gone. The other `InternalsVisibleTo` entries (test projects and the two hosts) stay; the host ones are X12.

### KumikoColorTypeConverter 8-digit hex

`KumikoColorTypeConverter` reads `#AARRGGBB` (alpha first), as XAML, WinUI and MAUI's `Color.FromArgb(string)` do;
issue #37 asked for `#RRGGBBAA` (CSS Color 4 order). The behavior is unchanged here. Options:

| Option | Effect | Risk |
|---|---|---|
| A. Keep `#AARRGGBB` (recommended) | Matches every XAML color property next to a `KumikoColor` property in the same file (`Background="#80FF0000"` on a MAUI or Uno element means the same as on a Kumiko property). | Web developers expect CSS order; documented in the converter's remarks and the README. |
| B. Switch to `#RRGGBBAA` | Matches CSS. | Silent: the same string gives a different color, with no compile error; inconsistent with the host's own color properties in the same XAML. |
| C. Accept CSS only in a functional form | Keep `#AARRGGBB`, add `rgba(r, g, b, a)` (and `rgb(...)`) for CSS-minded authors. | Additive, unambiguous; a small parser addition after 1.0 is non-breaking. |

Recommendation: A now, C if users ask. The `#ARGB` short form follows the same order.

### Uno package check and the HarfBuzz dependencies

`eng/check-uno-package.sh` failed on a locally packed KumikoUI.Uno because the desktop and WebAssembly dependency
groups list `HarfBuzzSharp.NativeAssets.Linux` and `HarfBuzzSharp.NativeAssets.WebAssembly` (added in wave 1, #251/#258,
for emoji shaping). They belong in the package: `SkiaSharp.HarfBuzz` (through KumikoUI.SkiaSharp) brings the managed
HarfBuzzSharp 14.x, while Uno's runtime references the native library at 8.3.x; without the direct dependency an app on
Linux desktop or WebAssembly can resolve the older native library and fail at load, the same reason
`SkiaSharp.NativeAssets.Linux` is a dependency. The allow-list now includes `HarfBuzzSharp.NativeAssets.(Linux|WebAssembly)`
with that reason, and the script passes on the packed package.

## Steps at the 1.0 tag

Do these on the release commit, in this order, after every change that should be in 1.0 is merged:

1. Make sure the `Unshipped` files are current: `eng/update-public-api.sh` (on macOS for all buildable heads), review
   `git diff -- '*PublicAPI*.txt'`, and build the Windows heads in CI so their `PublicAPI/net10.0-windows*/` files are
   confirmed.
2. Move the surface into `Shipped`, per file pair (the project's own pair and every `PublicAPI/<tfm>/` pair of MAUI and
   Uno): append the `Unshipped` lines to `PublicAPI.Shipped.txt` below its `#nullable enable` line, sort the lines
   (`LC_ALL=C sort`), and leave `PublicAPI.Unshipped.txt` empty (zero bytes, or `#nullable enable` only). For example:
   ```bash
   for dir in src/KumikoUI.{Core,SkiaSharp,Maui,Uno} src/KumikoUI.{Maui,Uno}/PublicAPI/*; do
     [ -f "$dir/PublicAPI.Unshipped.txt" ] || continue
     { echo '#nullable enable'; { grep -v '^#nullable' "$dir/PublicAPI.Shipped.txt"; cat "$dir/PublicAPI.Unshipped.txt"; } | LC_ALL=C sort -u; } > "$dir/shipped.tmp"
     mv "$dir/shipped.tmp" "$dir/PublicAPI.Shipped.txt"; : > "$dir/PublicAPI.Unshipped.txt"
   done
   ```
   Build every head; RS0016/RS0017 must stay quiet. The acceptance criterion of #112 is "no `Unshipped` entries at tag
   time".
3. Do **not** set `PackageValidationBaselineVersion` on the 1.0 commit: there is no earlier stable package to compare
   with. Tag `v1.0.0` and publish.
4. On the first commit after the tag, uncomment `<PackageValidationBaselineVersion>1.0.0</PackageValidationBaselineVersion>`
   in `Directory.Build.props` (PKG-04). From then on `dotnet pack` runs ApiCompat against the published 1.0.0 packages
   (restored from nuget.org) and fails on a breaking change; raise the baseline after each stable release. Package
   validation needs the baseline packages to be restorable in CI.
5. Update CONTRIBUTING "Public API and formatting" to say the `Shipped` files are live: a removed or changed `Shipped`
   line is a breaking change that needs a major version (or an explicit, documented exception) and a CHANGELOG
   **Breaking:** entry.

## Decisions for the maintainer

| # | Decision | Options | Recommendation |
|---|---|---|---|
| D1 | `KumikoColorTypeConverter` 8-digit hex order | A. keep `#AARRGGBB`; B. switch to `#RRGGBBAA`; C. keep A and add `rgba()`/`rgb()` | A, later C ([details](#kumikocolortypeconverter-8-digit-hex)). |
| D2 | Two editor-factory shapes | Keep both (`KumikoColumn.CustomEditorFactory`, `EditorDescriptor.CreateEditor(object?, KumikoRect)` abstract, `ICellEditorProvider.CreateEditor(object?, KumikoRect)`), or make the `CellEditorContext` form the only one before 1.0 (remove `CustomEditorFactory`, make `EditorDescriptor.CreateEditor(CellEditorContext)` the abstract member, give `ICellEditorProvider` one method). | Consolidate before 1.0 if a breaking release note for custom-editor authors is acceptable: today a new descriptor must implement the legacy overload to get the full one. Touches about 15 test sites and three README examples. |
| D3 | Hosts use Core internals while depending on `KumikoUI.Core >= x` (X12) | a. Pin exact versions between the packages (`[1.0.0]`), for example with a target after `_GetProjectReferenceVersions` that wraps `ProjectVersion` in brackets; b. make the members the hosts need public and drop the host `InternalsVisibleTo`; c. leave it and document "keep all KumikoUI packages on the same version" | a: all four packages already share one version (MinVer); an exact range turns a run-time failure into a restore error. |
| D4 | Doc follow-ups owned by other work | `docs/RENDERING.md` (MAUI surface table still says `Gpu`) and the `.github/instructions` API fragments were outside this change's scope. | Update with the `#96` documentation pass. |

## Follow-ups (not done here)

| # | Item |
|---|---|
| F1 | `docs/RENDERING.md` "Surfaces and Frame Loop (MAUI)" table: rename the `Gpu` column to `Hardware`. |
| F2 | CA1422 (`UIImpactFeedbackGenerator` obsolete on iOS 17.5+) in both hosts' haptics. |
| F3 | MAUI action-button definitions do not receive the grid's `BindingContext` (Uno's do), and the MAUI grid does not redraw when a definition's property changes after it is shown; align with Uno. |
| F4 | `KumikoRenderer.Render`'s drag arguments could come from `InputController` (a post-1.0 additive overload). |

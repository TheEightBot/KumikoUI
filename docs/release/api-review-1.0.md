# KumikoUI 1.0 API review: sign-off pass (#112)

This is the second and last review of the public surface before `PublicAPI.Shipped.txt` is frozen at `1.0.0`. The
first pass ([docs/release-readiness/api-review-1.0.md](../release-readiness/api-review-1.0.md), commits 0929b92 and
e269086) fixed the naming, overload and sealing problems it found and recorded a verdict for every type. This pass
re-reads the surface as it shipped in `1.0.0-preview.1` (main at a20aec3), checks it again against the naming policy and
the .NET Framework Design Guidelines (FDG), and does not repeat the first pass's verdicts unless it disagrees with one or
has new evidence. Nothing in the public API is changed here: the maintainer decides on the findings and the open
decisions first. The only code change in this pass is a doc comment fix (see [Doc comment fixes in this pass](#doc-comment-fixes-in-this-pass)).

## Method and scope

**Inventory.** The `PublicAPI.*.txt` files are the list of what is public. Every line was read:

| Package | Files | Lines |
|---|---|---|
| KumikoUI.Core | `src/KumikoUI.Core/PublicAPI.Unshipped.txt` | 2373 |
| KumikoUI.SkiaSharp | `src/KumikoUI.SkiaSharp/PublicAPI.Unshipped.txt` | 86 |
| KumikoUI.Maui | `src/KumikoUI.Maui/PublicAPI.Unshipped.txt` (no per-framework files) | 217 |
| KumikoUI.Uno | `src/KumikoUI.Uno/PublicAPI.Unshipped.txt` and `PublicAPI/<tfm>/PublicAPI.Unshipped.txt` for `net10.0`, `-android`, `-ios`, `-browserwasm` and `-desktop` | 213 + 5 × 5 |

Every `PublicAPI.Shipped.txt` holds only `#nullable enable`. There is no `PublicAPI/net10.0-windows*` folder in either
host: the Windows heads build in CI (`ci.yml` jobs `build-maui` and `build-uno` on `windows-latest`) with RS0016 and
RS0017 as errors (`Directory.Build.targets:48`), so their surface equals the common file as long as those jobs are green.

**Context.** The source of every type was read where a line in the API file raised a question (who calls it, what a
parameter means, whether a class has virtual members). Usage was checked with searches over `src/`, `tests/`,
`samples/` and `bench/`.

**Yardsticks.**

- The naming policy in [CONTRIBUTING.md, "Public type names"](../../CONTRIBUTING.md#public-type-names): one `Kumiko`
  prefix for grid types, `Drawn` for drawn components and what belongs to them, descriptive unprefixed names allowed
  when `eng/TypeNameCheck` passes, no new `Grid*`/`DataGrid*` types. `eng/TypeNameCheck` was run on this commit over
  KumikoUI.Core and KumikoUI.SkiaSharp: 174 public types, no collision (CI runs it over the host assemblies too).
- The decisions in [docs/ROADMAP.md §2](../ROADMAP.md#2-decisions-record) and the compatibility rules in
  [docs/VERSIONING.md](../VERSIONING.md) (documented defaults and behavior are public API).
- FDG: names and casing, abbreviations, Boolean names, events and event arguments, collections, async, nullable
  annotations, sealing, optional parameters, and members that should not be public.

**Checked and found clean** (no finding): casing and abbreviations in type and member names (the first pass's renames
hold); no oblivious (`~`) entry, so every reference type is annotated; every public `*EventArgs` is sealed and derives
from `EventArgs`; every public event is `EventHandler` or `EventHandler<T>`; no `async void` and no `[Obsolete]` member;
RS0026/RS0027 are errors, so no overload set has optional parameters on more than one overload; all drawn components
except the extensible `DrawnActionButtons` are sealed; the two `KumikoDataGrid` controls have the same members with the
same names, parameters and Core types (138 member and accessor lines and 47 property identifiers in each file, equal
line for line once the host's color and property-identifier types are mapped; the files differ only in the
action-button definition, `KumikoFonts`'s package-path parameter, the framework overrides and MAUI's `UseKumikoUI()`).

**Severity.**

- **must-fix**: wrong, or blocks the freeze, and cannot be changed after `1.0.0` without a major version.
- **should**: worth doing before the freeze (it is breaking later), but shipping as is can be defended.
- **nit**: cosmetic, or additive (it can be done in a 1.x minor without a break).

"Breaking" means a binary or source break for code compiled against `1.0.0-preview.1`. Every breaking change gets a
**Breaking:** entry in the CHANGELOG; [VERSIONING.md](../VERSIONING.md) allows them between previews.

## Summary

| Severity | Count | IDs |
|---|---|---|
| must-fix | 1 | X1 |
| should | 11 | X2, C1–C9, C16 |
| nit | 13 | C10–C15, C17–C20, S1, H1, H2 |

Must-fix: **X1**, `eng/update-public-api.sh` breaks the build the first time it is run after the freeze.

The four open decisions are in [Open decisions before 1.0.0](#open-decisions-before-100). Recommendations: keep
`#AARRGGBB` (D1); consolidate the editor factories on `CellEditorContext` now (D2); keep `Auto` = Hardware on Android
behind a physical-device check and stop promising the per-platform mapping (D3); keep the HarfBuzz dependency once its
size is measured and Uno is checked with HarfBuzzSharp 14 (D4).

## Cross-cutting findings

| ID | Member | Problem | Proposed change | Breaking | Severity |
|---|---|---|---|---|---|
| X1 | `eng/update-public-api.sh:99`, `:103-105` | The script is written for the pre-freeze state. After `Unshipped` is moved into `Shipped`, its next run (1) appends the five generated `GlobalStaticResources` lines (`generated_for`, line 37) to each Uno head's `PublicAPI/<tfm>/PublicAPI.Unshipped.txt` although they are now in that head's `Shipped` file: RS0025 (a symbol listed twice), an error (`Directory.Build.targets:48`), on every Uno head; and (2) deletes a head's `PublicAPI.Shipped.txt` whenever that head's `Unshipped` comes out empty (line 104). After the freeze an empty `Unshipped` is the normal case, so once (1) is fixed the shipped lines of the Uno heads would be deleted and RS0016 fires. | Before or with the freeze commit: filter the `generated_for` lines against the head's `Shipped` file (`comm -23` on sorted copies), and remove a head's folder only when both of its files are empty. Test it by running the script right after the move in step 3 of [Steps at the 1.0 tag](#steps-at-the-10-tag): `git diff` must stay empty. | No (tooling) | must-fix |
| X2 | `docs/ROADMAP.md:21` ("Naming" row of §2) | The recorded decision says "`DataGrid*` for model/host types, `Grid*` for primitives and engine, `Drawn*` for editors; no other `Kumiko*` types". The code and the policy in `CONTRIBUTING.md:193` do the opposite (one `Kumiko` prefix, no new `Grid*`/`DataGrid*` types), which #16 and the first pass settled. A reader who checks the roadmap gets the wrong rule. | Add an "Update" sentence to the row, like the "Package scope" row has, pointing at CONTRIBUTING's "Public type names". | No (docs) | should |

## KumikoUI.Core

| ID | Member | Problem | Proposed change | Breaking | Severity |
|---|---|---|---|---|---|
| C1 | `TextCellRenderer` (`Rendering/TextCellRenderer.cs:7`), `BooleanCellRenderer` (`BooleanCellRenderer.cs:6`), `ImageCellRenderer` (`ImageCellRenderer.cs:15`), `ProgressBarCellRenderer` (`ProgressBarCellRenderer.cs:9`), `ActionButtonsCellRenderer` (`ActionButtonsCellRenderer.cs:35`) | The first pass kept these open as "extension points", but none has a virtual or protected member: `Render` is a plain public method on each (`TextCellRenderer.cs:10`, `BooleanCellRenderer.cs:35`, `ImageCellRenderer.cs:25`, `ProgressBarCellRenderer.cs:65`, `ActionButtonsCellRenderer.cs:72`), so a subclass can only add properties or re-implement `ICellRenderer` by hand. Nothing in the repository derives from them; the documented way to customize is to implement `ICellRenderer`. Adding `virtual` to an existing member after 1.0 is a breaking change (callers compiled with `call` do not dispatch), and so is sealing, so the current half-open state is frozen either way. | Seal the five (the README and `.github/prompts/add-cell-renderer.prompt.md` already point custom renderers at `ICellRenderer`). If subclassing is wanted, make `Render` (and the `IInteractiveCellRenderer` members of `ActionButtonsCellRenderer`) `virtual` now instead. | Yes | should |
| C2 | `ImageCellRenderer.ImageResolver` (`ImageCellRenderer.cs:22`, `Func<object?, object?>?`) | The same name as `KumikoRenderer.ImageResolver` (`KumikoRenderer.cs:137`) and `CellRenderContext.ImageResolver` (`CellRenderContext.cs:135`), which are `IKumikoImageResolver`s that load images, while this one maps a cell value to an image *source* that then goes through that resolver. Two different things with one name in the same pipeline. | Rename to a selector name like the other `Func` properties (`DisplaySelector`, `KeySelector`, `ValueSelector`), for example `SourceSelector`. | Yes | should |
| C3 | `DrawnScrollPicker.SelectedValue` (`DrawnScrollPicker.cs:131`, `string?`), `SelectedItemValue` (`:135`), `SelectValue(object?)` (`:145`) | On `DrawnComboBox`, `SelectedValue` is the item's value (`DrawnComboBox.cs:129`, `object?`), `SelectedText` the display text (`:132`) and the setter is `SetSelectedValue(object?)` (`:255`). On `DrawnScrollPicker`, `SelectedValue` is the display text and the value is `SelectedItemValue`. A custom editor factory that switches between the two components reads different things from the same name. The first pass kept the picker's names because they match `Items`/`ItemValues`; the clash with the sibling component was not weighed. | Picker: `SelectedValue` returns the value (`object?`), a new `SelectedText` returns the text, `SelectedItemValue` goes; use one method name on both components for selecting by value. | Yes (the `SelectedValue` type changes) | should |
| C4 | `EditSession.NeedsRedraw` (`EditSession.cs:152`), `KumikoInputController.NeedsRedraw` (`KumikoInputController.cs:192`) | The same signal is `RedrawRequested` on `KumikoRenderer` (`KumikoRenderer.cs:36`), `DrawnComponent` (`DrawnComponent.cs:158`) and `IInteractiveCellRenderer`/`ActionButtonsCellRenderer` (`ActionButtonsCellRenderer.cs:65`). A custom host subscribes to four sources and meets two names. | Rename both to `RedrawRequested`. | Yes (custom hosts only; the built-in hosts are updated in the same change) | should |
| C5 | `KumikoRenderer.AllowFiltering` (`KumikoRenderer.cs:129`), `KumikoInputController.AllowFiltering` (`KumikoInputController.cs:181`, which forwards to `KumikoHitTester.AllowFiltering`, `KumikoHitTester.cs:23`) | One setting lives in two objects; the hosts set both (`src/KumikoUI.Maui/KumikoDataGrid.cs:512-513`). A custom host that sets only one draws filter icons that do nothing, or hides icons that still open the popup. `AllowSorting` exists only on the controller, so the two grid-level switches are shaped differently. | Keep `AllowFiltering` on `KumikoInputController` only and let the renderer read it through `KumikoRenderer.InputController` (or move both switches to `KumikoSource`); remove `KumikoRenderer.AllowFiltering`. See C6 for the hit tester. | Yes (custom hosts only) | should |
| C6 | `KumikoLayoutEngine` (`Layout/KumikoLayoutEngine.cs:10`, 24 API lines), `KumikoHitTester` (`Input/KumikoHitTester.cs:10`) | Engine internals that a host never touches: `KumikoRenderer` and `KumikoInputController` each own a private instance, neither host uses them, and their only outside users are the tests (which have `InternalsVisibleTo`) and `bench/KumikoUI.Benchmarks/DataBenchmarks.cs:65`. Freezing them freezes the layout algorithm's shape, including `GetVisibleFrozenColumns`/`GetVisibleRightFrozenColumns`/`GetVisibleScrollableColumns` returning `List<(KumikoColumn Column, float X, int OriginalIndex)>` (`KumikoLayoutEngine.cs:378`, `:411`, `:447`) and the `Populate*` methods taking that list: a concrete mutable list of tuples, which FDG advises against in public APIs. The README's "Custom hosts" section needs neither type. | Make both `internal` and add `InternalsVisibleTo` for `KumikoUI.Benchmarks`. If they stay public, replace the tuple with a named `readonly struct` and drop either the `GetVisible*` or the `Populate*` family. | Yes | should |
| C7 | `EditSession.UpdateEditorBounds` (`EditSession.cs:560`), `DrawnTextBox.SetMeasureContext` (`DrawnTextBox.cs:268`) | No production caller. `UpdateEditorBounds` is called only by `EditSessionTests`; `SetMeasureContext` only by tests, and its summary ("Call before drawing") is misleading because `OnDraw` sets the same context (`DrawnTextBox.cs:354`). | Make both `internal` (the tests keep access), or remove `UpdateEditorBounds`. | Yes | should |
| C8 | `TableSummaryRow.Columns` (`TableSummaryRow.cs:23`), `GroupSummaryRow.Columns` (`GroupSummaryRow.cs:19`), `CaptionSummaryDescription.Columns` (`CaptionSummaryDescription.cs:12`): `List<SummaryColumnDescription>` with a setter; `FilterDescription.SelectedValues` (`FilterDescription.cs:18`): `HashSet<string>?` with a setter | Concrete collection types on configuration objects (CA1002) with setters (CA2227). The first pass kept these mutable on purpose, which is right; the problem is the concrete type: it rules out change notification or validation later, and a `HashSet` hides which comparer the filter uses. | `IList<SummaryColumnDescription>` without a setter, initialized (XAML collection syntax still works); `ISet<string>?` for `SelectedValues`, with the comparer documented. Keep the `Kumiko*LayoutState` lists: they are JSON data-transfer objects. | Yes | should |
| C9 | `KumikoRect` (`Rendering/KumikoRect.cs:6`), `KumikoPoint` (`KumikoPoint.cs:6`), `KumikoSize` (`KumikoSize.cs:6`), `KumikoFontMetrics` (`KumikoFontMetrics.cs:7`) | Value types without `IEquatable<T>`, `Equals`/`GetHashCode` overrides or `==`/`!=` (CA1815), while `KumikoColor` and `CellPosition` have them and `KumikoFont`, `KumikoTextRange` and `KumikoHitTestResult` are record structs. `rect == other` does not compile, and `Equals`/`GetHashCode` fall back to the runtime's default for structs (boxing, possibly reflection, and float semantics the type never chose). | Add `IEquatable<T>`, the overrides and the operators. Additive, but doing it after 1.0 can change what `Equals` returns for some values, so do it now. | No | should |
| C10 | Boolean names: `KumikoStyle.EnableAnimations` (`KumikoStyle.cs:311`) beside `KumikoInputController.AnimationsEnabled` (`KumikoInputController.cs:42`); `ScrollSettings.EnableInertia` (`ScrollSettings.cs:33`); the hosts' `EnableHaptics` (`src/KumikoUI.Maui/KumikoDataGrid.Haptics.cs:25`, Uno `:26`); `DrawnTextBox.NumericOnly` (`DrawnTextBox.cs:174`) and `TextEditorDescriptor.NumericOnly` (`TextEditorDescriptor.cs:12`); `KumikoColumn.EditorIsFilterable` (`KumikoColumn.cs:563`) and `ProgressShowText` (`:273`) | FDG asks for affirmative phrases; `Enable…` reads as a command. The animation pair is the confusing case: two switches with different jobs (style preference, host capability) and mirrored names. The others are understandable as they are. | Optional: `IsInertiaEnabled`, `IsHapticFeedbackEnabled` (MAUI's `Is…Enabled` idiom), `AreAnimationsEnabled`/keep the controller's name; or leave all as they are. | Yes | nit |
| C11 | Parameter names that differ from the property they fill: `DateRangeSelectedEventArgs(start, end)` (`DateRangeSelectedEventArgs.cs:16`, properties `StartDate`/`EndDate`), `PickerSelectionChangedEventArgs(index)` (`PickerSelectionChangedEventArgs.cs:13`, `SelectedIndex`), `RowTappedEventArgs(…, item)` (`RowTappedEventArgs.cs:21`, `DataItem`; `KumikoCellTappedEventArgs` says `dataItem`), `KumikoFont(…, bold, italic)` (`KumikoFont.cs:38`, `IsBold`/`IsItalic`); also `CellPosition`'s operators take `a`, `b` (`CellPosition.cs:36`) where `KumikoColor`'s take `left`, `right`, and `KumikoRect.Contains(px, py)` (`KumikoRect.cs:52`) | The first pass renamed most such parameters (X3 there); these were missed. | Rename to match the properties (`startDate`, `endDate`, `selectedIndex`, `dataItem`, `isBold`, `isItalic`, `left`, `right`, `x`, `y`). | Source only, for named-argument callers | nit |
| C12 | `AutoFitColumnEventArgs` (`AutoFitColumnEventArgs.cs:9`) for `KumikoInputController.AutoFitColumnRequested` (`KumikoInputController.cs:284`); `RowReorderedEventArgs` for `RowReorderRejected` (`:230`) | Args named after a different event than the one that raises them. Both are documented. | Keep, or rename the first to `AutoFitColumnRequestedEventArgs`. | Yes, if renamed | nit |
| C13 | Value tuples in signatures: `KumikoSource.GetGroupSummaryRowInfo` returns `(string GroupPath, int SummaryIndex)?` (`KumikoSource.cs:1274`); the hosts' `KumikoFonts.RegisterFromAppPackageAsync(IEnumerable<(string Family, string FileName)>)` (`src/KumikoUI.Maui/KumikoFonts.cs:62`, Uno `AppxUri`, `src/KumikoUI.Uno/KumikoFonts.cs:87`) | FDG advises named types over `ValueTuple` in public APIs; tuple element names are not part of the binary signature, so a rename is never caught by ApiCompat. Low traffic. | Keep. | — | nit |
| C14 | `KumikoSource.GetUniqueColumnValues` (`KumikoSource.cs:1645`) returns `List<string>` | A new mutable list per call; every other collection the source returns is `IReadOnlyList<T>`. | Return `IReadOnlyList<string>`. | Yes | nit |
| C15 | `IKumikoClipboard.GetTextAsync()` (`IKumikoClipboard.cs:25`), `KumikoSource.WhenViewUpdatedAsync()` (`KumikoSource.Background.cs:98`) | Async without a `CancellationToken`. | Add overloads (a default interface member for the clipboard) when needed. | No (additive) | nit |
| C16 | `CellEditorFactory.ApplyThemeToEditor` (`CellEditorFactory.cs:66-78`), `ApplyFontToEditor` (`:86`) | They switch over the built-in drawn components, so a custom `DrawnComponent` editor returned by an editor factory never receives the grid's style or `CellFont`. `DrawnComponent` has no hook for it. | Add `protected virtual` theme and font hooks to `DrawnComponent` that the factory calls for every editor; the built-in components override them. | No (additive; can be a 1.x minor) | should |
| C17 | Missing `<exception>` docs | 29 public members throw `ArgumentNullException` (or another exception) without saying so, for example `KumikoSelectionModel(KumikoSource)` and `Select`/`Deselect`/`SetSelectedItems` (`KumikoSelectionModel.cs:25`, `:119`, `:139`, `:159`), `KumikoSource.GetItem` (`KumikoSource.cs:1221`, also `InvalidOperationException`), `SetSort` (`:1708`), `IndexOfItem` (`:1370`), `ExpandToItem` (`:1389`), `KumikoLayoutState.Capture`/`ApplyTo` (`KumikoLayoutState.cs:48`, `:101`), `KumikoAccessorRegistry.Register`/`AddProvider` (`KumikoAccessorRegistry.cs:58`, `:73`), `DrawnScrollPicker.SetItems` (`DrawnScrollPicker.cs:178`), and the hosts' `ApplyLayoutState` and `ScrollToColumn(KumikoColumn, …)`. `KumikoScroller.RefreshContentExtents` (`KumikoScroller.cs:281`) has no argument checks at all, unlike its siblings. | Add the `<exception>` lines (13 were added in this pass, see below). | No | nit |
| C18 | `EditSession.CellValidator` (`EditSession.cs:99`) and the hosts' `CellValidator`: `Func<int, int, object?, CellValidationResult>` | Two positional `int`s (row, column) are easy to swap in a lambda; a named delegate or a context record would be self-describing, like `CellEditorContext` is for editors. | Keep, or switch to a `Func<CellValidationContext, CellValidationResult>`-style shape now. | Yes, if changed | nit |
| C19 | `EditSession.UpdateAnimation` (`EditSession.cs:709`) beside `KumikoInputController.UpdateAnimations` (`KumikoInputController.cs:69`) | Singular and plural names for the same per-frame step. | Keep, or align. | Yes, if renamed | nit |
| C20 | `KumikoSource.ReorderColumn` (`KumikoSource.cs:1066`) returns `void` while `ReorderRow` (`:1143`) returns `KumikoReorderResult`; `RemoveGroupDescription(string propertyName)` (`:1513`) takes a name while `AddGroupDescription`/`InsertGroupDescription` take a `GroupDescription` | Asymmetric pairs. | Keep (additive overloads can follow). | — | nit |

## KumikoUI.SkiaSharp

| ID | Member | Problem | Proposed change | Breaking | Severity |
|---|---|---|---|---|---|
| S1 | `SkiaFontRegistrar.TryGetTypeface` (`SkiaFontRegistrar.cs:290`, `:315`) | The `out SKTypeface? typeface` parameter has no `[NotNullWhen(true)]`, so callers get a nullable warning after a successful call; `IKumikoImageLoader.TryGetCacheKey` has the attribute. | Add the attribute. | No (annotation) | nit |

The rest of the package (86 lines) has no finding. `RegisterTypefaceFamiliesFromStream(Stream, IEnumerable<string?>)`
takes nullable names on purpose (null and empty names are ignored, as documented). The package's dependency on
`SkiaSharp.HarfBuzz` is decision D4.

## KumikoUI.Maui and KumikoUI.Uno

The two hosts are reviewed together: their `KumikoDataGrid` surfaces are identical apart from platform types (see
[Method and scope](#method-and-scope)).

| ID | Member | Problem | Proposed change | Breaking | Severity |
|---|---|---|---|---|---|
| H1 | `KumikoDataGrid.SortChanged`/`SortChanging` (`src/KumikoUI.Maui/KumikoDataGrid.Options.cs:377`, `src/KumikoUI.Uno/KumikoDataGrid.Options.cs:399`) | They forward `KumikoSource.SortColumnsChanged`/`SortColumnsChanging` (`KumikoSource.cs:129`) with `SortColumnsChangedEventArgs`, so the host and Core use different names for one event, while `FilterChanged`/`FilterChanging` match. | Keep (the host names are what the README documents); or rename Core's to `SortChanged`/`SortChanging`. | Yes, if renamed | nit |
| H2 | `KumikoDataGrid.ScrollToRow(int row, …)` (MAUI `KumikoDataGrid.Scrolling.cs:116`, Uno `:119`) | The parameter is `row` while `ScrollToColumn` takes `columnIndex`. | Rename to `rowIndex`. | Source only, for named-argument callers | nit |

Checked and kept, with the reason:

- `ContextMenuRequested` (and its command) on both hosts while Core's event is `KumikoInputController.ContextRequested`:
  deliberate, so as not to hide WinUI's `UIElement.ContextRequested` on the Uno control (documented at
  `src/KumikoUI.Uno/KumikoDataGrid.cs:293-301`); MAUI uses the same name for parity.
- `Columns`, `GroupDescriptions`, `TableSummaryRows`, `GroupSummaryRows` as settable `ObservableCollection<T>`: bindable
  properties that XAML and view models replace, as with `ItemsSource`.
- `ScrollToColumn` as four overloads and `ScrollToRow`/`ScrollToItem` with optional parameters: both shapes pass
  RS0026/RS0027 (the methods have different names).
- `GlobalStaticResources` in every Uno head's file: generated by Uno's XAML generator and called across assemblies by app
  heads; see the first pass.

## Doc comment fixes in this pass

Committed on this branch, no signature changed:

- `<exception cref="ArgumentNullException">` on the seven `KumikoScroller` methods that check their arguments
  (`GetRowY`, `GetColumnX`, `GetRowOffsetY`, `GetColumnOffsetX`, `ScrollToRow`, `ScrollToColumn`, `ScrollToItem`) and on
  the six `KumikoAnnouncements` methods (`GridHint`, `CurrentCell`, `SortChanged`, `EditStarted`, `EditEnded`,
  `ValidationFailed`).
- "cancelled" to "canceled" in the summaries of `InputAction.Cancelled` and `CellEndEditEventArgs.Committed` (the
  identifiers are unchanged).

## Open decisions before 1.0.0

These four are listed in [1.0-checklist-status.md](1.0-checklist-status.md#open-maintainer-decisions) and in the
[preview release notes](1.0.0-preview.1-release-notes.md). Each changes the API, a default or a package dependency, and
each is cheapest to take before the freeze.

### D1. Order of 8-digit hex colors in `KumikoColorTypeConverter`

**Evidence.**

- The converter reads `#AARRGGBB` and `#ARGB`, alpha first, and says so in its remarks
  (`src/KumikoUI.Core/Rendering/KumikoColorTypeConverter.cs:16-18`): "alpha first as in XAML and MAUI". WinUI/Uno XAML
  color strings and MAUI's `Color.FromArgb(string)` use the same order, so `#80FF0000` means the same on a Kumiko
  property as on the host's own `Background` in the same XAML file.
- The same converter's comma form is `R,G,B,A`, alpha last (`KumikoColorTypeConverter.cs:14`, `:207`), the CSS
  `rgba()` order. Each form follows its own convention.
- `KumikoColor.ToUInt32()` packs ARGB (`KumikoColor.cs:106`).
- `1.0.0-preview.1` shipped with `#AARRGGBB`, and its release notes (`1.0.0-preview.1-release-notes.md:103`), the README
  (`README.md:1217`) and the CHANGELOG (`CHANGELOG.md:87`) say so. Issue #37 asked for `#RRGGBBAA` (CSS Color 4).

**Options.**

| Option | Effect | Risk |
|---|---|---|
| A. Keep `#AARRGGBB` | Same as every XAML color property next to it. | Web developers expect CSS order; already documented. |
| B. Switch to `#RRGGBBAA` | Matches CSS hex. | Silent: the same string gives a different color, no compile error, and preview users' XAML changes color; inconsistent with the host's own color properties in the same file. |
| C. A, plus `rgb()`/`rgba()` functional forms | CSS authors get an unambiguous form. | Additive; can come in any 1.x minor. |

**Recommendation: A.** Add C only if users ask; it does not need to be decided now.

**Decided: keep `#AARRGGBB` and add `rgb()`/`rgba()`** (#304). Implemented in `KumikoColorTypeConverter`.

### D2. Two editor-factory shapes

**Evidence.**

- Two shapes exist side by side on three extension points: `KumikoColumn.CustomEditorFactory`
  (`Func<object?, KumikoRect, DrawnComponent?>`, `KumikoColumn.cs:569`) and `KumikoColumn.EditorFactory`
  (`Func<CellEditorContext, DrawnComponent?>`, `:576`); `EditorDescriptor.CreateEditor(object?, KumikoRect)` is the
  `abstract` member and `CreateEditor(CellEditorContext)` a `virtual` that forwards to it (`EditorDescriptor.cs:16`,
  `:24`); `ICellEditorProvider` has the same pair with a default interface member (`ICellEditorProvider.cs:31`, `:39`),
  and `ActionButtonsCellRenderer` implements the legacy one.
- The precedence is `EditorFactory`, then `CustomEditorFactory`, then `EditorDescriptor`, then a renderer that is an
  `ICellEditorProvider` (`CellEditorFactory.cs:310-323`).
- The legacy shape cannot see `CellEditorContext.InitialCharacter` or `Culture`. Its docs justify it "so an existing
  descriptor keeps working", but no stable release exists, so there is no existing code to protect beyond preview users.
- As it stands, a new `EditorDescriptor` must implement the legacy abstract overload even when it only wants the context
  one: the inheritance contract is upside down, and after the freeze it stays that way until 2.0.
- Cost of consolidating: `CustomEditorFactory` appears in 5 test files (11 references), 3 places in the README and 3 in
  `.github/instructions`; legacy `CreateEditor(object?, KumikoRect)` overrides are in 3 test files (9) and the two
  built-in descriptors.

**Options.**

| Option | What changes | Risk |
|---|---|---|
| A. Keep both | Nothing. | Two ways to do one thing for the life of 1.x; descriptors keep the inverted abstract member. |
| B. Consolidate now | Remove `KumikoColumn.CustomEditorFactory`; make `EditorDescriptor.CreateEditor(CellEditorContext)` the only (abstract) member; give `ICellEditorProvider` the single context method; update `ActionButtonsCellRenderer`, the built-in descriptors, tests, README and instructions. | A **Breaking:** note for preview users who wrote custom editors (a mechanical change: `(value, bounds)` becomes `context.Value`, `context.Bounds`). |
| C. Half step | Mark `CustomEditorFactory` and the legacy overloads `[Obsolete]` at 1.0, remove them in 2.0; flip `EditorDescriptor` so the context overload is the abstract one. | Still breaking for descriptors now, and leaves the obsolete members in 1.x. |

**Recommendation: B**, in this preview cycle. It is the last cheap moment, the change is mechanical, and it removes an
extension point the docs would otherwise have to explain for every 1.x release.

### D3. `RenderMode = Auto` stays Hardware on Android

**Evidence.**

- MAUI's `KumikoRenderMode.Auto` resolves to Hardware (`SKGLView`) on Android and Mac Catalyst and to Software on iOS and
  Windows (`src/KumikoUI.Maui/KumikoRenderMode.cs:9-13`, `README.md:1406`).
- The emulator measurements in [docs/maui-verification.md](../maui-verification.md#frame-times-and-the-263-recommendation)
  (lines 91-112): Hardware 0.50 ms per frame and 0 B allocated during a fling, Software 22-26 ms and 2-6 KB per frame;
  Hardware drew correctly on every page; switching to `Software` at run time works.
- Not measured: any physical Android device. The realistic risk is a GL driver bug on a low-end Mali or Adreno phone,
  which an emulator on a desktop GPU does not show. The release notes list it as untested.
- [VERSIONING.md](../VERSIONING.md) counts documented defaults as public API, so the per-platform mapping that the XML
  doc and README spell out is a compatibility promise from 1.0.

**Options.**

| Option | Effect |
|---|---|
| A. Keep Hardware on Android | Fast path by default; `RenderMode = Software` is the escape hatch for a misbehaving driver. |
| B. Make Android `Auto` Software | Safe on every driver, two to ten times slower on the measured emulator, and a per-frame bitmap allocation. |
| C. A, and stop promising the mapping | Document `Auto` as "the surface KumikoUI recommends for the platform; the choice can change in a minor release" and keep the current mapping as information, not contract. |

**Recommendation: A with C.** Keep Hardware, make one physical low-end Android device a release gate for `1.0.0` (draw
every sample page, fling, edit, re-measure with the frame stats overlay), and reword the `Auto` summary and the README
row so a later switch for a bad driver family is not a breaking change. Not an API shape question; nothing in the
`PublicAPI` files changes.

### D4. The HarfBuzz dependency

**Evidence.**

- `KumikoUI.SkiaSharp` already depends on `SkiaSharp.HarfBuzz` (`src/KumikoUI.SkiaSharp/KumikoUI.SkiaSharp.csproj:14`,
  version `$(SkiaSharpVersion)` = 4.153.1, `Directory.Packages.props:17`, `:63`), since #259, to draw emoji sequences
  as one glyph (`CHANGELOG.md:126`); when the native library cannot be loaded the sequences fall back to glyph-by-glyph
  drawing (`SkiaResourceCache.cs:549-561`). So every app, MAUI included, already ships HarfBuzz.
- `KumikoUI.Uno` adds `HarfBuzzSharp.NativeAssets.Linux` and `.WebAssembly` 14.2.1.301 (`Directory.Packages.props:70-71`,
  `src/KumikoUI.Uno/KumikoUI.Uno.csproj:96-102`, allowed by `eng/check-uno-package.sh:67-71`). That lifts Uno's own
  HarfBuzzSharp (8.3.x in Uno 6.7's Skia runtime) to 14.x, which is the lift that the complex-script spec
  ([docs/specs/111-harfbuzz-shaping.md](../specs/111-harfbuzz-shaping.md) §5.2) recommended avoiding.
- The spec planned complex-script shaping (#111) as an optional `KumikoUI.SkiaSharp.HarfBuzz` package behind an internal
  seam (§6.1), so that only apps that need it pay. The emoji work made the dependency unconditional before that package
  exists, which removes most of the reason for a separate package.
- Size: spec §5.1 lists the native libraries (about 0.8-1.4 MB compressed per Android ABI, iOS and Windows target; Uno's
  Skia heads already carry HarfBuzz) and says the real delta should be measured in the sample apps. The "about 172 KB more
  WebAssembly" in the release notes (`1.0.0-preview.1-release-notes.md:157`) has no measurement recorded in the
  repository that I could find.
- Neither [docs/uno/verification.md](../uno/verification.md) nor [docs/maui-verification.md](../maui-verification.md)
  mentions emoji or HarfBuzz, so Uno's own text rendering with HarfBuzzSharp 14 is not recorded as checked.

**Options.**

| Option | Effect | Risk |
|---|---|---|
| A. Keep the dependency in `KumikoUI.SkiaSharp` for 1.0; build #111 shaping on it later, without a new package | Emoji correct everywhere now; complex scripts become a 1.x feature with no new dependency. | Every app pays the native size; Uno apps run on HarfBuzzSharp 14. |
| B. Move all shaping (emoji included) into an optional package before 1.0 | Apps that do not add it lose emoji-sequence shaping (the fallback draws glyph by glyph) and the size. | More work now; a new package to version; removing a dependency after 1.0 can break apps that relied on it transitively, so this is now or 2.0. |
| C. Keep the emoji dependency, ship #111 as the separate package from the spec | Two places where shaping lives. | The optional package saves nothing, since the dependency is already there. |

**Recommendation: A**, on two conditions before the tag: measure the real size delta (MAUI Android and iOS release
builds, Uno WebAssembly) and record it in the release notes in place of the unsourced figure, and add an Uno check to the
device matrix that Uno's own `TextBlock` text still renders with HarfBuzzSharp 14 on desktop and WebAssembly. If the MAUI
delta is judged too large, B is the only option that is cheap, and it has to happen before 1.0.

## Steps at the 1.0 tag

This replaces the first pass's section of the same name. Do these on the release commit, after every accepted finding,
D1 and D2 are merged.

1. **Fix the script first** (X1), so the freeze leaves the documented workflow working.
2. **Make the `Unshipped` files current.** Run `bash eng/update-public-api.sh` on macOS (all buildable heads) and review
   `git diff -- '*PublicAPI*.txt'`. Make sure the CI jobs that build the Windows heads (`build-maui`, `build-uno`) are
   green on that commit: they are the only check of `net10.0-windows10.0.19041.0` (MAUI) and `net10.0-windows10.0.26100`
   (Uno), which have no per-framework files today.
3. **Move `Unshipped` into `Shipped`**, per file pair: the project's own pair in all four libraries and every
   `PublicAPI/<tfm>/` pair of Uno (MAUI has none). Run it with `bash` (zsh stops on a glob with no match):

   ```bash
   for dir in src/KumikoUI.{Core,SkiaSharp,Maui,Uno} src/KumikoUI.Uno/PublicAPI/*; do
     [ -f "$dir/PublicAPI.Unshipped.txt" ] || continue
     { echo '#nullable enable'; { grep -v '^#nullable' "$dir/PublicAPI.Shipped.txt"; cat "$dir/PublicAPI.Unshipped.txt"; } | LC_ALL=C sort -u; } > "$dir/shipped.tmp"
     mv "$dir/shipped.tmp" "$dir/PublicAPI.Shipped.txt"; : > "$dir/PublicAPI.Unshipped.txt"
   done
   ```

   Then check: every `PublicAPI.Unshipped.txt` is empty (`find src -name PublicAPI.Unshipped.txt -size +0` prints
   nothing); every head builds with RS0016, RS0017 and RS0025 quiet; running `bash eng/update-public-api.sh` again leaves
   `git diff` empty (this is the test for X1). This is the acceptance criterion of #112: no `Unshipped` entries at tag
   time.
4. **Tag `v1.0.0` without a package validation baseline.** No earlier stable package exists to compare with.
5. **On the first commit after the tag**, uncomment `<PackageValidationBaselineVersion>1.0.0</PackageValidationBaselineVersion>`
   in `Directory.Build.props:60` and reword its comment (lines 55-58 still say "for the first stable release"). From then
   on `dotnet pack` compares against the published `1.0.0` packages, which CI must be able to restore. Packs of MAUI and
   Uno happen on Windows in CI and in the publish workflow (`ci.yml:280`, `:317`; `publish.yml:232`, `:289`); a local
   pack on macOS or Linux lacks the Windows target framework, which package validation is likely to report against a
   baseline that has it. Check that once and, if so, either document "pack the hosts on Windows" or condition the
   baseline for those two projects.
6. **Make the `Shipped` files live in CONTRIBUTING** (`CONTRIBUTING.md:174`): a removed or changed `Shipped` line is a
   breaking change that needs a major version (or a documented exception) and a **Breaking:** CHANGELOG entry (the script
   never removes a `Shipped` line; that stays a deliberate edit by hand). Point its link at this section.
7. **Close #112.**

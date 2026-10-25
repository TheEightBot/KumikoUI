# Changelog

All notable changes to KumikoUI are documented in this file. All four packages (KumikoUI.Core, KumikoUI.SkiaSharp, KumikoUI.Maui, KumikoUI.Uno) share one version.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html) as described in [docs/VERSIONING.md](docs/VERSIONING.md). Each release's section is also used as its GitHub release notes.

## [Unreleased]

### Added
- **KumikoUI.Uno**: a new package hosting the grid on the Uno Platform (Windows App SDK, desktop on Windows/macOS/Linux, WebAssembly, Android, iOS) with the same engine, rendering and features as KumikoUI.Maui, plus `KumikoFonts` for registering custom fonts from app assets, and the `SampleApp.Uno` sample.
- `DataGridStyle.PinBottomSummaryRows` (default `true`): `Position=Bottom` table summary rows are pinned to the bottom of the viewport as a fixed footer instead of scrolling after the last row. **Behaviour change** for existing users of bottom summaries; set it to `false` to restore the previous layout.

### Changed
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
- An unknown font family now falls back to the default family in the requested weight and slant instead of producing a null typeface. Styled typefaces are resolved through `SKFontManager` with an explicit family, which SkiaSharp 4 needs to keep bold and italic.
- Taps on the pinned summary footer no longer select or edit the data row behind it; unpinned bottom summary rows are also reported as `HitRegion.TableSummaryRow`.
- Keyboard navigation keeps the current row fully visible between the header/frozen rows and the pinned footer, and accounts for the grouping panel and top summary rows (previously the scroll offset was counted twice).
- Row drag-handle grips no longer paint over the pinned summary footer.

## [0.1.10] - 2026-05-05

### Added
- `SkiaFontRegistrar`: register custom typefaces (for example CJK or icon fonts) and use them by family name in `GridFont`. Fixes Japanese text and icon fonts rendering garbled on Android (#5, #8).

### Fixed
- Sample: corrected the selection text colour property name in `IconFontCellRenderer`.

## [0.1.9] - 2026-05-04

### Added
- Per-column `EditTriggers` on `DataGridColumn`, overriding the grid-level triggers (#6, #7).

## [0.1.8] - 2026-05-03

### Added
- Action button columns: pill buttons in a cell with `ICommand` or callback support, activating on the first tap (#3).
- MIT licence and Contributor Covenant Code of Conduct.

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

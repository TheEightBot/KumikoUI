# Changelog

All notable changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed
- **Breaking:** minimum OS versions raised and unified across KumikoUI.Maui and KumikoUI.Uno: iOS 17.0 (was 15.0 on MAUI, 14.2 on Uno), Mac Catalyst 17.0 (was 15.0, which current Xcode toolchains reject), Android API 24 (was 21), Windows 10.0.19041 (was 10.0.17763 on MAUI). Defined once in `Directory.Build.props` and re-baselined at each .NET major.
- KumikoUI.Uno package description no longer claims a Mac Catalyst head (macOS is served by the desktop head).
- KumikoUI.Maui now declares an explicit minimum of `Microsoft.Maui.Controls` 10.0.20 instead of whatever MAUI version the build machine had installed.
- KumikoUI.Uno is built with Uno.Sdk 6.7.30 (was 6.5.31).
- **Breaking:** SkiaSharp 4.153.1 (was 3.119.2) for KumikoUI.SkiaSharp, KumikoUI.Maui and KumikoUI.Uno, which changes the native SkiaSharp library in every consuming app. **Uno apps must set `<SkiaSharpVersion>4.153.1</SkiaSharpVersion>`** in their csproj: Uno.Sdk's implicit SkiaSharp packages default to 3.x, and the app must resolve the same major as KumikoUI. KumikoUI.Uno also references the SkiaSharp packages Uno's runtime pulls at 3.x (`SkiaSharp.Views`, `SkiaSharp.NativeAssets.Linux`, `SkiaSharp.Skottie`), so the whole graph resolves one major.
- **Breaking:** KumikoUI.Core, KumikoUI.SkiaSharp and every KumikoUI.Uno head now target .NET 10 (`net10.0`, `net10.0-*`); `net9.0` is dropped because .NET 9 support ends 2026-11-10. KumikoUI.Maui already targeted .NET 10. Apps must target .NET 10.

### Added
- `DataGridStyle.PinBottomSummaryRows` (default `true`): `Position=Bottom` table summary rows are pinned to the bottom of the viewport as a fixed footer instead of scrolling after the last row. **Behaviour change** for existing users of bottom summaries; set it to `false` to restore the previous layout.

### Fixed
- An unknown font family now falls back to the default family in the requested weight and slant instead of producing a null typeface. Styled typefaces are resolved through `SKFontManager` with an explicit family, which SkiaSharp 4 needs to keep bold and italic.
- Taps on the pinned summary footer no longer select or edit the data row behind it; unpinned bottom summary rows are also reported as `HitRegion.TableSummaryRow`.
- Keyboard navigation keeps the current row fully visible between the header/frozen rows and the pinned footer, and accounts for the grouping panel and top summary rows (previously the scroll offset was counted twice).
- Row drag-handle grips no longer paint over the pinned summary footer.

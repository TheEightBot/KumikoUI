# KumikoUI sample apps

Two apps show the same grid on both hosts:

| Sample | Host | Heads |
|---|---|---|
| `SampleApp.Maui` | .NET MAUI (`KumikoUI.Maui`) | iOS, Mac Catalyst, Android, Windows |
| `SampleApp.Uno` | Uno Platform (`KumikoUI.Uno`) | Desktop (Skia), WebAssembly, iOS, Android, Windows App SDK |

`samples/Shared` holds what both use: the fonts, the avatar images, `GroupingTheme.cs` (the Grouping page's style)
and `ShowcaseData.cs` (the rows, the cell and row styles, the validator and the event descriptions of the Styling
and Events pages). Every page exists in both apps with the same name and content.

## Prerequisites

.NET 10 SDK. MAUI needs the `maui` workload, and `dotnet workload install` for the heads you target (`android`,
`ios`, `maccatalyst`, `wasm-tools`). iOS and Mac Catalyst need macOS with Xcode. Windows heads build on Windows
only.

## Running the MAUI sample

From the repository root:

| Head | Command |
|---|---|
| Mac Catalyst | `dotnet build samples/SampleApp.Maui -f net10.0-maccatalyst -t:Run` |
| iOS Simulator | `dotnet build samples/SampleApp.Maui -f net10.0-ios -t:Run -p:_DeviceName=:v2:udid=<simulator UDID>` (`xcrun simctl list devices` lists the UDIDs) |
| Android (emulator or device) | `dotnet build samples/SampleApp.Maui -f net10.0-android -t:Run` |
| Windows | `dotnet build samples/SampleApp.Maui -f net10.0-windows10.0.19041.0 -t:Run` |

## Running the Uno sample

| Head | Command |
|---|---|
| Desktop | `dotnet run --project samples/SampleApp.Uno -f net10.0-desktop` |
| WebAssembly | `dotnet run --project samples/SampleApp.Uno -f net10.0-browserwasm` (serves a local URL; needs the `wasm-tools` workload) |
| iOS Simulator | `dotnet build samples/SampleApp.Uno -f net10.0-ios -t:Run -p:_DeviceName=:v2:udid=<simulator UDID>` |
| Android (emulator or device) | `dotnet build samples/SampleApp.Uno -f net10.0-android -t:Run` |
| Windows | `dotnet run --project samples/SampleApp.Uno -f net10.0-windows10.0.26100` |

A trimmed WebAssembly build, as shipped: `dotnet publish samples/SampleApp.Uno -f net10.0-browserwasm -c Release`.
`Platforms/WebAssembly/LinkerConfig.xml` explains why KumikoUI needs no linker entry: row types carry
`[KumikoGridItem]`, so the grid reads properties through generated accessors rather than reflection.

On Android the activities use `WindowSoftInputMode = AdjustResize`, so the soft keyboard shrinks the window and the
grid scrolls the edited cell into view (see [docs/TEXT-INPUT.md](../docs/TEXT-INPUT.md#android)).

## Pages and features

| Page (MAUI route / Uno item) | Shows | README section |
|---|---|---|
| **All Components** (`MainPage` / All Components) | Every column type, frozen columns and rows, edit triggers and text selection, selection modes, table summaries, adding, removing and updating rows (live data), row drag and drop, themes, built-in or Material icons, context menu, saving and restoring the layout | Columns, Editing, Selection, Row Drag and Drop, Summaries, Theming, Icons, Live Data |
| **Grouping** (`GroupingPage`) | Nested groups, the grouping panel, filter popups, sorting, group summaries, sticky headers | Grouping, Sorting and Filtering, Summaries |
| **Large Data** (`LargeDataPage`) | 100,000 rows, load timings, the frame-time overlay, scrolling | Performance, Scrolling |
| **Theming** (`ThemingPage`) | Light, Dark, HighContrast, custom colors and `FollowSystemTheme` | Theming and Styling |
| **MVVM Actions** (`MvvmActionsPage`) | Action button columns bound to commands, per-column `EditTriggers`, `SelectedItem`, `SelectedItems` and `SelectionChangedCommand` | Action Button Columns, Selection in MVVM |
| **Custom Fonts** (`CustomFontsPage`) | Noto Sans JP headers, Material Icons columns, registered fonts | Custom Fonts |
| **Images** (`ImagesPage`) | `Image` columns from app assets, `ImageSource` or `BitmapImage` and web URLs over 10,000 rows, the bounded `ImageCache` | Images |
| **Styling** (`StylingPage`) | An image column, switching `KumikoStyle.IconProvider` between the built-in set and `KumikoFontIconProvider`, `CellStyleResolver`, `RowStyleResolver`, `AlternateRowBackground` | Images, Icons, Per-cell and per-row styles |
| **Events** (`EventsPage`) | A log of `CellTapped`, `HeaderTapped`, `ContextMenuRequested`, `RowDoubleTapped`, `CellValueChanged`, `ValidationFailed` and `SelectionChanged`; the four selection modes; a `CellValidator`; `KumikoLayoutState` save and restore; `Culture` and `Strings`; `ScrollSettings`; `ClipboardCopyIncludesHeaders` | Host Properties, Commands and Events, Selection, Validation, Localization, Scrolling, Saving and restoring the layout |

## Checking the MAUI sample with DevFlow

`eng/devflow/verify.sh` drives the Mac Catalyst build page by page and takes a screenshot per step. See
[eng/devflow/README.md](../eng/devflow/README.md). It navigates by Shell route name, so keep the routes above
when you rename a page's title.

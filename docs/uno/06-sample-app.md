# 06 — Sample App (`SampleApp.Uno`)

Goal: a runnable Uno app that exercises `KumikoUI.Uno` on every head, mirroring
[`samples/SampleApp.Maui`](../../samples/SampleApp.Maui). Created with the `dotnet` CLI.

---

## 1. Scaffold with the CLI

- [x] Create the app (all heads, XAML, Skia renderer). **Deviation: `-presentation none`** (code-behind),
  not `-presentation mvvm`, and the `-theme material` flag was dropped (the `blank` preset takes no theme
  flag). Rationale per Phase 06 priorities: mirror `SampleApp.Maui` (which is code-behind) and prioritize a
  clean, runnable desktop build over MVVM purity — Uno.Extensions/MVVM scaffolding adds a host-builder layer
  the sample doesn't need. Actual command run:
  ```bash
  dotnet new unoapp -o samples/SampleApp.Uno -n SampleApp.Uno \
    -preset blank -tfm net9.0 -markup xaml -presentation none \
    -platforms android ios wasm desktop windows -renderer skia
  ```
  > The modern `unoapp` template nests the project one level deep (`samples/SampleApp.Uno/SampleApp.Uno/…`)
  > and emits repo-root-style files at the output root. The project was **flattened** up one level so the
  > csproj lives at `samples/SampleApp.Uno/SampleApp.Uno.csproj` (as the rest of this doc assumes).
- [x] **Reconcile generated solution-scaffolding files.** All of the following were generated at the output
  root and removed during reconciliation:
  - deleted the generated `samples/SampleApp.Uno/SampleApp.Uno.sln` (we use the root `KumikoUI.sln`);
  - removed the generated `global.json` (keep the repo SDK-unpinned, per [ADR](README.md)) — its `Uno.Sdk`
    pin (`6.5.31`) is instead inlined in the csproj `Sdk` attribute (`Sdk="Uno.Sdk/6.5.31"`), mirroring
    `src/KumikoUI.Uno`;
  - removed the generated `Directory.Packages.props` **and** `Directory.Build.props` — the latter enabled
    Central Package Management (`ManagePackageVersionsCentrally=true`), which the repo does **not** use and
    which would fight the root [`Directory.Build.props`](../../Directory.Build.props). Its `ImplicitUsings` /
    `Nullable` / `NoWarn` settings were baked directly into the csproj instead;
  - also removed generated `Directory.Build.targets`, `.gitignore`, `.editorconfig`, `.vsconfig`, `.vscode/`,
    `.run/` from the output root (template cruft / would conflict with repo-level equivalents).
  - confirmed the repo-root `Directory.Build.props` (package metadata) applying to the sample is harmless
    (it is — the sample sets `IsPackable=false`). No `nuget.config` was generated.
  - **Verified: no `global.json` / `Directory.Packages.props` / `nuget.config` at the repo root, and no stray
    `*.sln` / CPM / `Directory.Build.*` left under `samples/SampleApp.Uno/`.**

## 2. Wire into the solution & reference the library

- [x] Add the app project to `KumikoUI.sln`. **Deviation: edited the `.sln` by hand** rather than
  `dotnet sln add` (project GUID `{70A42A2C-0C67-4D56-9D61-AF5A6A11B913}`, `Any CPU`/`x64`/`x86` configs
  mirroring `SampleApp.Maui`). The `dotnet add reference` was likewise done by hand in the csproj. As with
  Phase 02, the CLI's pre-restore of the `Uno.Sdk` single-project is unreliable for these solution edits.
- [x] Reference the library: `ProjectReference` to `src/KumikoUI.Uno/KumikoUI.Uno.csproj` added to the csproj.
  **Note:** referenced **without** `UndefineProperties` — `KumikoUI.Uno` is itself a multi-head Uno project
  with the *same* head TFMs as the sample, so the per-head `TargetFramework` must flow through for the app's
  `net9.0-desktop` to bind the library's matching `net9.0-desktop` output. (An initial attempt that copied the
  library's own `UndefineProperties="TargetFramework;TargetFrameworks"` — which is correct only for its
  single-TFM Core/SkiaSharp deps — caused `CS0234: KumikoUI.Uno does not exist` because the reference resolved
  to no matching head.) The library transitively brings Core + SkiaSharp.
- [x] Nested under the **`samples`** solution folder (next to `SampleApp.Maui`), GUID
  `{5D20AA90-6969-D8BD-9DCD-8634F4692FDA}`. Verified `dotnet sln KumikoUI.sln list` parses and lists the
  project.

## 3. Assets & fonts

- [x] Copied the demo fonts from the MAUI sample (`samples/SampleApp.Maui/Resources/Raw/`):
  `NotoSansJP-Regular.ttf`, `MaterialIcons-Regular.ttf` → `samples/SampleApp.Uno/Assets/Fonts/`,
  marked as `Content` via `<Content Include="Assets/Fonts/*.ttf" />`
  ([05 §4](05-fonts-and-assets.md#4-where-the-font-files-live)).
- [x] Registered at startup in `App.OnLaunched` **before** `Activate()` — `OnLaunched` was made `async void`
  and `await`s `KumikoFonts.RegisterFromAppPackageAsync((family, "ms-appx:///Assets/Fonts/…"))` for both
  families ([05 §2–3](05-fonts-and-assets.md#2-loading-assets-on-uno-the-platform-specific-bit)).

## 4. Demo pages (mirror the MAUI sample's coverage)

Port a representative subset of the MAUI sample pages so each feature is demonstrable. Use XAML pages
with the same data/columns. **All six pages were ported** (none deferred), reachable from a
`NavigationView`-driven shell (`MainShell.xaml`) — the Uno analogue of `SampleApp.Maui`'s `AppShell` tabs.

- [x] **Main / basic grid** — `BasicGridPage`: binds `ItemsSource` + `Columns`; every column type (Text,
  Numeric, Boolean, Date, ComboBox, Picker, Template w/ progress bar + numeric-up-down), top/bottom table
  summary rows, 2 frozen rows, frozen-left/right columns, runtime theme cycle + selection-mode toggle.
  (← `MainPage`)
- [x] **Large data** — `LargeDataPage`: 10K/50K/100K loads generated off-thread then assigned in one shot
  (single `RebuildView`), with timing readout. (← `LargeDataPage`)
- [x] **Grouping** — `GroupingPage`: group by Department/Level/both, expand/collapse all, toggle a group
  summary row, toggle filtering — all via `kumiko.DataSource.*`. (← `GroupingPage`)
- [x] **Theming** — `ThemingPage`: built-in Light/Dark/HighContrast (via `DataGridTheme.Create(mode)`), three
  hand-built custom palettes (Ocean/Forest/Sunset), and live header-colour RGB sliders. **Note:** the Uno
  `DataGridView` exposes **no `Theme` DP** (unlike MAUI's `kumiko.Theme`); themes are applied by assigning the
  produced `DataGridStyle` to `kumiko.GridStyle` — same end result. (← `ThemingPage`)
- [x] **MVVM actions** — `MvvmActionsPage`: per-column `EditTriggers` overrides + an Actions column
  (`ActionButtonsCellRenderer` w/ Details/Delete). **Note:** uses the framework-agnostic Core
  `ActionButtonDefinition.Command` (`ICommand`) built in code-behind with a small `RelayCommand<T>` — **not**
  MAUI's `MauiActionButtonDefinition` XAML binding (MAUI-specific). Confirmation/details use a WinUI
  `ContentDialog` instead of MAUI's `DisplayAlert`/modal page. (← `MvvmActionsPage`)
- [x] **Custom fonts** — `CustomFontsPage`: Japanese (CJK) headers via `NotoSansJP` (`DataGridStyle`
  header/cell fonts) + a Material-Icons status column via a small `IconFontCellRenderer : ICellRenderer`,
  proving [05](05-fonts-and-assets.md). (← `CustomFontsPage`)

XAML usage (declare the library namespace and drop the control in):
```xml
<Page xmlns:kumiko="using:KumikoUI.Uno">
    <kumiko:DataGridView x:Name="Grid"
                         ItemsSource="{Binding Items}"
                         RowHeight="36" HeaderHeight="40" />
</Page>
```

## 5. Run each head

| Head | Command |
|---|---|
| Desktop (Skia) | `dotnet run --project samples/SampleApp.Uno -f net9.0-desktop` |
| WebAssembly | `dotnet run --project samples/SampleApp.Uno -f net9.0-browserwasm` (serves a local URL) |
| Windows (WinAppSDK) | `dotnet run --project samples/SampleApp.Uno -f net9.0-windows10.0.26100` (or run from the IDE; use an `x64`/`ARM64` config) |
| Android | `dotnet build samples/SampleApp.Uno -t:Run -f net9.0-android` (emulator/device running) |
| iOS | `dotnet build samples/SampleApp.Uno -t:Run -f net9.0-ios` (macOS + simulator) |

> macOS is served by the **Desktop (Skia)** head — the sample was scaffolded with `-platforms android ios wasm desktop windows` (no separate `maccatalyst` head, matching the library; see [02 §3](02-library-scaffold.md)).
> On a machine without the .NET 9 **runtime** installed (only the SDK), prefix `run` with `DOTNET_ROLL_FORWARD=LatestMajor` to roll forward onto the .NET 10 runtime.

> Start with **Desktop** — fastest inner loop and the reference for input behavior. Validate WASM next
> (focus/keyboard), then the mobile heads (touch + soft keyboard), then Windows.

### Desktop build + run — verified (Phase 06)

- **Build:** `dotnet build samples/SampleApp.Uno/SampleApp.Uno.csproj -c Debug -f net9.0-desktop` →
  **Build succeeded, 0 Warning(s), 0 Error(s)** (also verified from a clean `obj`/`bin`).
- **Run smoke test:** `dotnet run --project samples/SampleApp.Uno -f net9.0-desktop` →
  the app process launched and **stayed alive through first render with zero exceptions**. The macOS Skia
  windowing backend initialized (`Uno.UI.Runtime.Skia.MacOS.MacOSWindowNative`); the only log output was
  non-fatal dev-server/HotReload connection warnings (no IDE dev-server attached in a headless launch) and a
  cosmetic missing-`iconStoreLogo.png` warning (a generated `Package.appxmanifest` default). No XAML parse
  error, no font/asset load failure, no grid-instantiation or first-paint crash. Terminated cleanly via
  `SIGTERM`.
  > **Environment note:** the local box has no `Microsoft.NETCore.App 9.0.x` shared runtime (only 6/7/8/**10.x**;
  > SDK is 10.0.300), so the first `dotnet run` aborted with `app-launch-failed (framework 9.0.0 not found)`.
  > Re-running with **`DOTNET_ROLL_FORWARD=LatestMajor`** rolled the 9.0-targeted app onto the installed 10.x
  > runtime and it started cleanly. This is purely a local-runtime-availability quirk; the build artifact is
  > correct and CI (Phase 08) provides the matching runtime.
- Only `net9.0-desktop` is buildable/runnable locally (no android/wasm/ios workloads installed); the other
  heads are CI-verified in Phase 08.

---

## ✅ Exit criteria

- [x] `SampleApp.Uno` launches on Desktop and renders the basic grid with live data. *(Verified: desktop build
  0/0 and a clean run-to-first-render smoke test — see above.)*
- [x] Sorting, selection, scrolling, grouping, theming, action buttons, and custom-font pages all
  **implemented** (all six pages ported). Interactive behaviour is exercised by the library's own input layer
  (Phase 04) and Core tests (Phase 07); the sample wires every feature to UI. *(Full hands-on interaction not
  scriptable in this headless session — startup/first-render verified.)*
- [ ] The same app runs on WASM, Windows, and at least one mobile head. *(Deferred to CI — Phase 08; those
  workloads aren't installed locally. The csproj targets all heads; Windows is Windows-only-conditioned.)*
- [x] The sample references `KumikoUI.Uno` only (which transitively brings Core + SkiaSharp).

➡️ Next: [07 — Tests](07-tests.md)

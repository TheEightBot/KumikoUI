# 06 — Sample App (`SampleApp.Uno`)

Goal: a runnable Uno app that exercises `KumikoUI.Uno` on every head, mirroring
[`samples/SampleApp.Maui`](../../samples/SampleApp.Maui). Created with the `dotnet` CLI.

---

## 1. Scaffold with the CLI

- [ ] Create the app (all heads, XAML, MVVM, Skia renderer):
  ```bash
  dotnet new unoapp -o samples/SampleApp.Uno -n SampleApp.Uno \
    -preset blank -tfm net9.0 -markup xaml -presentation mvvm \
    -platforms android ios wasm desktop windows \
    -renderer skia -theme material
  ```
- [ ] **Reconcile generated solution-scaffolding files.** The template emits a few repo-root-style
  files into the output folder (a `.sln`, possibly `global.json`, `Directory.Packages.props`,
  `nuget.config`). Since this lives inside an existing repo:
  - delete the generated `samples/SampleApp.Uno/*.sln` (we use the root `KumikoUI.sln`);
  - remove any generated `global.json` (keep the repo SDK-unpinned, per [ADR](README.md));
  - if a `Directory.Packages.props` (Central Package Management) was generated, either remove it or
    confirm it doesn't fight the root [`Directory.Build.props`](../../Directory.Build.props).
  - confirm the repo-root `Directory.Build.props` (package metadata) applying to the sample is harmless
    (it is — the sample isn't packed).

## 2. Wire into the solution & reference the library

- [ ] Add the app project (the modern `unoapp` is a single cross-targeted project):
  ```bash
  dotnet sln KumikoUI.sln add samples/SampleApp.Uno/SampleApp.Uno.csproj
  dotnet add samples/SampleApp.Uno/SampleApp.Uno.csproj reference src/KumikoUI.Uno/KumikoUI.Uno.csproj
  ```
- [ ] Nest it under the **`samples`** solution folder (next to `SampleApp.Maui`).

## 3. Assets & fonts

- [ ] Copy the demo fonts from the MAUI sample so behavior matches:
  `NotoSansJP-Regular.ttf`, `MaterialIcons-Regular.ttf` → `samples/SampleApp.Uno/Assets/Fonts/`,
  marked as `Content` ([05 §4](05-fonts-and-assets.md#4-where-the-font-files-live)).
- [ ] Register them at startup ([05 §2–3](05-fonts-and-assets.md#2-loading-assets-on-uno-the-platform-specific-bit))
  in `App.OnLaunched` before activating the window.

## 4. Demo pages (mirror the MAUI sample's coverage)

Port a representative subset of the MAUI sample pages so each feature is demonstrable. Use XAML pages
with the same data/columns:

- [ ] **Main / basic grid** — bind `ItemsSource` + `Columns`; sorting + selection. (← `MainPage`)
- [ ] **Large data** — virtualization / inertial scroll with thousands of rows. (← `LargeDataPage`)
- [ ] **Grouping** — group descriptions + summaries. (← `GroupingPage`)
- [ ] **Theming** — switch `DataGridStyle`/theme at runtime. (← `ThemingPage`)
- [ ] **MVVM actions** — action-button column bound to commands. (← `MvvmActionsPage`)
- [ ] **Custom fonts** — CJK + icon cells proving [05](05-fonts-and-assets.md). (← `CustomFontsPage`)

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
| Windows (WinAppSDK) | `dotnet run --project samples/SampleApp.Uno -f net9.0-windows10.0.19041.0` (or run from the IDE; use an `x64`/`ARM64` config) |
| Android | `dotnet build samples/SampleApp.Uno -t:Run -f net9.0-android` (emulator/device running) |
| iOS | `dotnet build samples/SampleApp.Uno -t:Run -f net9.0-ios` (macOS + simulator) |
| Mac Catalyst | `dotnet build samples/SampleApp.Uno -t:Run -f net9.0-maccatalyst` |

> Start with **Desktop** — fastest inner loop and the reference for input behavior. Validate WASM next
> (focus/keyboard), then the mobile heads (touch + soft keyboard), then Windows.

---

## ✅ Exit criteria

- [ ] `SampleApp.Uno` launches on Desktop and renders the basic grid with live data.
- [ ] Sorting, selection, scrolling, grouping, theming, action buttons, and custom-font pages all work.
- [ ] The same app runs on WASM, Windows, and at least one mobile head.
- [ ] The sample references `KumikoUI.Uno` only (which transitively brings Core + SkiaSharp).

➡️ Next: [07 — Tests](07-tests.md)

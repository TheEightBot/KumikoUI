# 02 — Library Scaffold (`KumikoUI.Uno`)

Goal: a cross-targeted `src/KumikoUI.Uno` project, created with the `dotnet` CLI, referencing the
**unchanged** `KumikoUI.Core` and `KumikoUI.SkiaSharp`, packable to NuGet alongside the existing
packages.

All commands are run from the repo root (`/Users/mstonis/Developer/KumikoUI`).

---

## 1. Scaffold with the CLI

> **`global.json` gotcha:** the `unolib` template can emit a `global.json` that pins the SDK
> (`msbuild-sdks.Uno.Sdk`) for the whole repo (it inherits upward). KumikoUI has no `global.json`
> today — keep it that way. Pass `-global-json false`.
>
> **Trade-off (resolved in §1a):** with `-global-json false` there is no SDK-version pin, so MSBuild
> cannot resolve the NuGet-delivered `Uno.Sdk` (`error MSB4236 / NETSDK: no version specified in the
> project or global.json`). We pin the version **inline in the `Sdk` attribute** instead — local to
> the project, nothing at the repo root. See §1a.

- [x] Create the library:
  ```bash
  dotnet new unolib -o src/KumikoUI.Uno -n KumikoUI.Uno -tfm net9.0 -r skia -global-json false
  ```
  - `-tfm net9.0` — align with Core/SkiaSharp ([ADR-4](README.md#2-architecture-decision-record)).
    **Required:** the template defaults to `net10.0`, so this flag must be passed. Use `net10.0` only
    if you also move the rest of `src/` there.
  - `-r skia` — Skia renderer, consistent with the SkiaSharp drawing backend.
    **Note:** the flag is `-r`/`--renderer` (the long-with-single-dash form `-renderer` is rejected by
    the template CLI).
  - The modern `unolib` template (Uno.Templates 6.5.31) produces an **`Uno.Sdk` single-project**
    (`<Project Sdk="Uno.Sdk">`, `UnoSingleProject=true`, `UnoFeatures`), **not** a `Microsoft.NET.Sdk`
    project. This is expected and good — it is the idiomatic Uno layout.
  - The template emits exactly two files: `KumikoUI.Uno.csproj` and a sample `Class1.cs` (removed in §5).
    With `-global-json false` **no `global.json` is written** anywhere.
- [x] Confirm no stray `global.json` was added at the repo root (or anywhere):
  ```bash
  find . -name global.json -not -path '*/obj/*' -not -path '*/bin/*'   # expect no output
  ```

### 1a. Pin the `Uno.Sdk` version inline (no `global.json`)

Because we skipped `global.json`, pin the SDK version in the project's `Sdk` attribute so MSBuild can
resolve it. The version matches the template (`Uno.Templates`/`Uno.Sdk` 6.5.31):

- [x] In `KumikoUI.Uno.csproj`:
  ```xml
  <Project Sdk="Uno.Sdk/6.5.31">
  ```

## 2. Wire into the solution & reference Core + SkiaSharp

> **CLI gotcha (deviation):** both `dotnet sln add` and `dotnet add … reference` first **evaluate**
> the target project, and that evaluation fails for an `Uno.Sdk` project before its first restore:
> `The SDK 'Uno.Sdk' specified could not be found`. (The NuGet-delivered SDK isn't materialized until
> a restore runs, and these commands don't restore.) So both steps were done **by hand** instead — a
> direct `.sln` edit and inline `<ProjectReference>` entries. This is functionally identical to what
> the CLI would have written.

- [x] Add to the solution **by editing `KumikoUI.sln` directly** (the `dotnet sln add` route fails as
  above). Register a `Project(...)`/`EndProject` entry for `src\KumikoUI.Uno\KumikoUI.Uno.csproj`
  (new GUID `{9C72BF6C-75B7-4F92-9B3A-B45AB43EC467}`, project-type GUID
  `{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}` like the other C# projects), add its six
  `ProjectConfigurationPlatforms` rows (all mapped to `Any CPU`, mirroring the sibling projects), and
  nest it (see next item).
- [x] Reference the two reused projects (mirrors `KumikoUI.Maui`) **as inline `<ProjectReference>`**
  in `KumikoUI.Uno.csproj` (the `dotnet add … reference` route fails as above):
  ```xml
  <ItemGroup>
    <ProjectReference Include="../KumikoUI.Core/KumikoUI.Core.csproj" UndefineProperties="TargetFramework;TargetFrameworks" />
    <ProjectReference Include="../KumikoUI.SkiaSharp/KumikoUI.SkiaSharp.csproj" UndefineProperties="TargetFramework;TargetFrameworks" />
  </ItemGroup>
  ```
  `UndefineProperties` stops this multi-head project's per-head `TargetFramework` from flowing into
  the P2P build of the single-TFM (`net9.0`) Core/SkiaSharp projects (see §7 for the matching
  build-command caveat).
- [x] In the solution, nest `KumikoUI.Uno` under the existing **`src`** solution folder by adding a
  `NestedProjects` row: `{9C72BF6C-75B7-4F92-9B3A-B45AB43EC467} = {827E0CD3-B72D-47B6-A68D-7590B98EB39B}`
  (the `src` folder GUID), matching `KumikoUI.Core/SkiaSharp/Maui`.

## 3. Confirm the cross-target set

- [x] Open `src/KumikoUI.Uno/KumikoUI.Uno.csproj`. The template (Uno.Sdk 6.5.31) generated this exact
  head set — note it includes a **bare `net9.0` reference head** and does **not** include
  `net9.0-maccatalyst` (this template version omits it; iOS covers Apple here), and the Windows
  moniker is **`net9.0-windows10.0.26100`**:
  ```
  net9.0 ; net9.0-ios ; net9.0-android ;
  net9.0-windows10.0.26100 ; net9.0-browserwasm ; net9.0-desktop
  ```
  > Deviation from the original list above: the doc anticipated `net9.0-maccatalyst` and a
  > `…19041.0` Windows moniker and no bare `net9.0`. The template's actual output is authoritative and
  > was kept as-is (no maccatalyst added, since its workload isn't installable in this environment and
  > the template intentionally omits it). The bare `net9.0` is Uno's reference/tooling head and is the
  > canonical packable slice.
- [x] Mirror MAUI's Windows-only conditioning so non-Windows machines/CI don't try to build the
  Windows head (compare with [`src/KumikoUI.Maui/KumikoUI.Maui.csproj`](../../src/KumikoUI.Maui/KumikoUI.Maui.csproj) lines 4–5).
  The Windows head is split out onto its own conditioned line:
  ```xml
  <!-- keep the Windows head only on Windows; other heads everywhere -->
  <TargetFrameworks>net9.0;net9.0-ios;net9.0-android;net9.0-browserwasm;net9.0-desktop</TargetFrameworks>
  <TargetFrameworks Condition="$([MSBuild]::IsOSPlatform('windows'))">$(TargetFrameworks);net9.0-windows10.0.26100</TargetFrameworks>
  ```

## 4. SkiaSharp.Views — provided idiomatically by `SkiaRenderer`, version-pinned (key gotcha)

The control type Phase 03 needs is `SKXamlCanvas` (namespace `SkiaSharp.Views.Windows`), which lives
in `SkiaSharp.Views.Uno.WinUI` (all non-Windows heads) and `SkiaSharp.Views.WinUI` (the `-windows`
head). The namespace/type is identical across heads — only the *package* differs.

**Deviation from the original plan (verified against the Uno.Sdk 6.5.31 targets):** the modern
`Uno.Sdk` single-project pulls `SkiaSharp.Views.Uno.WinUI` in **implicitly** from the
`<UnoFeatures>SkiaRenderer</UnoFeatures>` we already have, so a manual non-Windows `PackageReference`
is **not** needed. The relevant rule in
`Uno.Implicit.Packages.ProjectSystem.Uno.targets` is commented *"SkiaSharp.Views are always included
on Skia Renderer targets"* and fires when `UnoFeatures` contains `;skiarenderer;`. Two adjustments are
still required:

- [x] **Pin the version to `3.119.2`** (matching
  [`KumikoUI.SkiaSharp`](../../src/KumikoUI.SkiaSharp/KumikoUI.SkiaSharp.csproj) and
  [`KumikoUI.Maui`](../../src/KumikoUI.Maui/KumikoUI.Maui.csproj), to avoid native-binary conflicts).
  Uno.Sdk 6.5.31 would otherwise apply its own group version **3.119.1** to the implicit SkiaSharp
  packages. Override it with the SDK-recognised property:
  ```xml
  <PropertyGroup>
    <SkiaSharpVersion>3.119.2</SkiaSharpVersion>
  </PropertyGroup>
  ```
  Verified: `project.assets.json` resolves `SkiaSharp.Views.Uno.WinUI/3.119.2` and `SkiaSharp/3.119.2`,
  and the produced `.nupkg` lists `SkiaSharp.Views.Uno.WinUI 3.119.2` in every TFM dependency group.
- [x] **Add an explicit `SkiaSharp.Views.WinUI` only on the Windows head.** The `skiarenderer`
  feature does **not** bring it in — the Windows gate in
  `Uno.Implicit.Packages.ProjectSystem.WinAppSdk.targets` fires only for the `skia` / `lottie` / `svg`
  features. Reference it explicitly so `SKXamlCanvas` resolves on Windows too (Uno dedups implicit vs
  explicit, so this also holds the version at `3.119.2`):
  ```xml
  <ItemGroup Condition="$(TargetFramework.Contains('-windows'))">
    <PackageReference Include="SkiaSharp.Views.WinUI" Version="3.119.2" />
  </ItemGroup>
  ```
- [x] **Verified `SKXamlCanvas` resolves at compile time** (not just that the empty project builds):
  a temporary probe file referencing `typeof(global::SkiaSharp.Views.Windows.SKXamlCanvas)` was added,
  built clean for `net9.0-desktop` (a missing type would be `CS0246`), then deleted so the project
  stays empty for Phase 03.

## 5. Library layout / assets & template cruft

- [x] Enable Uno library layout so fonts and other assets resolve from the packaged library (needed by
  [05 — Fonts & assets](05-fonts-and-assets.md)). **The template already sets this** — kept as-is:
  ```xml
  <PropertyGroup>
    <GenerateLibraryLayout>true</GenerateLibraryLayout>
  </PropertyGroup>
  ```
- [x] Remove the template sample cruft so the project is clean for Phase 03: deleted `Class1.cs`. No
  `Themes/Generic.xaml` is needed (the drawn control has no `ControlTemplate`; Uno.Sdk does not require
  a generic theme for a library that ships no templated control). After this, the only file under
  `src/KumikoUI.Uno/` is `KumikoUI.Uno.csproj` — no `.cs`, ready for Phase 03.

## 6. NuGet metadata (mirror the sibling projects)

- [x] Add package metadata to the `<PropertyGroup>` (Authors/License/URLs/icon/README come for free
  from [`Directory.Build.props`](../../Directory.Build.props) — verified inherited: the produced
  `.nuspec` carries `authors=Eight-Bot, Inc.,Michael Stonis`, `license=MIT`, the GitHub `projectUrl`,
  `README.md`, and `logo.png`):
  ```xml
  <PackageId>KumikoUI.Uno</PackageId>
  <Version>1.0.0</Version>
  <Description>A high-performance, fully custom-drawn KumikoUI DataGrid control for the Uno Platform. Renders via SkiaSharp (SKXamlCanvas) across Windows, Desktop, WebAssembly, Android, iOS, and Mac Catalyst — reusing the same engine as KumikoUI.Maui.</Description>
  <PackageTags>uno;unoplatform;winui;datagrid;grid;table;skiasharp;dotnet;cross-platform</PackageTags>
  ```
- [x] **Opt the project back into packing.** `Uno.Sdk` defaults `IsPackable` to `false` for
  single-projects (it ties it to `GeneratePackageOnBuild`), so `dotnet pack` silently produces no
  package until this is set — unlike the `Microsoft.NET.Sdk` siblings, which are packable by default:
  ```xml
  <IsPackable>true</IsPackable>
  ```
- [x] Keep `ImplicitUsings`, `Nullable`, and `GenerateDocumentationFile` consistent with the other
  `src/` projects (`enable`/`enable`/`true`). The template already sets `ImplicitUsings`/`Nullable` to
  `enable`; `GenerateDocumentationFile=true` was added.

## 7. Build verification

> **Environment note:** only the `ios`/`macos` workloads are installed in the build environment
> (`android`/`wasm-tools`/`maccatalyst` are not, and can't be without sudo). The head that fully
> builds locally is **`net9.0-desktop`**. A bare `dotnet build` (all heads) is therefore **not** run —
> it would fail on the missing workloads.

> **Scope with `-f`, not `-p:TargetFrameworks` (important):** scope a local build to one head with the
> `--framework` switch. `dotnet build … -p:TargetFrameworks=net9.0-desktop` **fails** here:
> `TargetFrameworks` is a *global* property that propagates into the `Microsoft.NET.Sdk` Core/SkiaSharp
> references at restore time, which reject the Uno `-desktop` platform id with
> `error NETSDK1139: The target platform identifier desktop was not recognized`. `-f`/`--framework` is
> applied to the entry project only and does not propagate, so it builds clean.

- [x] Build the one head that is fully buildable in this environment (this was the authoritative
  verification — **succeeds, 0 errors, 0 warnings attributable to `KumikoUI.Uno`**; the ~98 warnings
  seen on a cold build are pre-existing `CS1591`/nullable warnings from the unchanged Core/SkiaSharp):
  ```bash
  dotnet build src/KumikoUI.Uno/KumikoUI.Uno.csproj -c Debug -f net9.0-desktop
  ```
  (Release behaves identically; Debug was used for the recorded run.)
- [x] Pack dry-run (proves it is NuGet-packable, like the CI does for the other projects). The **full
  multi-head pack succeeds locally** — `dotnet pack` only needs the workload *manifests* (present in
  the SDK), not the full runtime workload packs an app build/run needs, so it packs all heads
  including android/wasm here:
  ```bash
  dotnet pack src/KumikoUI.Uno/KumikoUI.Uno.csproj -c Release -p:Version=0.0.0-ci -o /tmp/uno-pack
  ```
  Result: `Successfully created package '/tmp/uno-pack/KumikoUI.Uno.0.0.0-ci.nupkg'` containing
  `lib/{net9.0, net9.0-android35.0, net9.0-browserwasm1.0, net9.0-desktop1.0, net9.0-ios18.0}/KumikoUI.Uno.dll`.
  The Windows head is correctly absent (excluded off-Windows). Each TFM dependency group lists only
  `KumikoUI.Core`, `KumikoUI.SkiaSharp`, and the Skia/Uno runtime packages.
  > Do **not** pass `-p:TargetFrameworks=…` / `-p:TargetFramework=…` to scope the pack: the plural form
  > hits the same NETSDK1139 dependency failure as in the build note; the singular form makes the Uno
  > single-project pack target no-op (produces no `.nupkg`). Pack the full head set as shown.

---

## ✅ Exit criteria

- [x] `KumikoUI.Uno` is in `KumikoUI.sln` (nested under `src`) and references **only** `KumikoUI.Core`
  + `KumikoUI.SkiaSharp` (confirmed in the `.nuspec` dependency groups — no other KumikoUI projects).
- [x] No `global.json` was introduced at the repo root (or anywhere — `find` returns none). The
  `Uno.Sdk` version is pinned inline via `Sdk="Uno.Sdk/6.5.31"` instead.
- [x] The project builds for `net9.0-desktop` (verified, 0 errors). Other current-OS heads
  (`net9.0`, `net9.0-ios`) also pack successfully; `android`/`wasm`/`maccatalyst` app *builds* are
  blocked only by absent workloads, not by project config.
- [x] `dotnet pack` produces `KumikoUI.Uno.0.0.0-ci.nupkg` (full multi-head package).
- [x] `KumikoUI.Core`, `KumikoUI.SkiaSharp`, and `KumikoUI.Maui` are byte-for-byte unchanged (only
  read, never edited; source mtimes remain at their May dates).

➡️ Next: [03 — DataGridView host](03-datagridview-host.md)

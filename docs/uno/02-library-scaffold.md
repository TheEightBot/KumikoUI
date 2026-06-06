# 02 — Library Scaffold (`KumikoUI.Uno`)

Goal: a cross-targeted `src/KumikoUI.Uno` project, created with the `dotnet` CLI, referencing the
**unchanged** `KumikoUI.Core` and `KumikoUI.SkiaSharp`, packable to NuGet alongside the existing
packages.

All commands are run from the repo root (`/Users/mstonis/Developer/KumikoUI`).

---

## 1. Scaffold with the CLI

> **`global.json` gotcha:** the `unolib` template can emit a `global.json` that pins the SDK for the
> whole repo (it inherits upward). KumikoUI has no `global.json` today — keep it that way. Pass
> `-global-json false`, or generate then delete the file.

- [ ] Create the library:
  ```bash
  dotnet new unolib -o src/KumikoUI.Uno -n KumikoUI.Uno -tfm net9.0 -renderer skia -global-json false
  ```
  - `-tfm net9.0` — align with Core/SkiaSharp ([ADR-4](README.md#2-architecture-decision-record)). Use `net10.0` only if you also move the rest of `src/` there.
  - `-renderer skia` — Skia rendering, consistent with the SkiaSharp drawing backend.
- [ ] Confirm no stray `global.json` was added at the repo root:
  ```bash
  git status --porcelain | grep global.json   # expect no output
  ```

## 2. Wire into the solution & reference Core + SkiaSharp

- [ ] Add to the solution:
  ```bash
  dotnet sln KumikoUI.sln add src/KumikoUI.Uno/KumikoUI.Uno.csproj
  ```
- [ ] Reference the two reused projects (mirrors `KumikoUI.Maui`):
  ```bash
  dotnet add src/KumikoUI.Uno/KumikoUI.Uno.csproj reference \
    src/KumikoUI.Core/KumikoUI.Core.csproj \
    src/KumikoUI.SkiaSharp/KumikoUI.SkiaSharp.csproj
  ```
- [ ] In the solution, nest `KumikoUI.Uno` under the existing **`src`** solution folder (the
  `dotnet sln add` may place it at the root; move it under `src` to match `KumikoUI.Core/SkiaSharp/Maui`).

## 3. Confirm the cross-target set

- [ ] Open `src/KumikoUI.Uno/KumikoUI.Uno.csproj`. The template generates the Uno head TFMs. Verify it
  includes (monikers may vary slightly by template version):
  ```
  net9.0-android ; net9.0-ios ; net9.0-maccatalyst ;
  net9.0-browserwasm ; net9.0-desktop ; net9.0-windows10.0.xxxxx.0
  ```
- [ ] Mirror MAUI's Windows-only conditioning so non-Windows machines/CI don't try to build the
  Windows head (compare with [`src/KumikoUI.Maui/KumikoUI.Maui.csproj`](../../src/KumikoUI.Maui/KumikoUI.Maui.csproj) lines 4–5):
  ```xml
  <!-- keep the Windows head only on Windows; other heads everywhere -->
  <TargetFrameworks>net9.0-android;net9.0-ios;net9.0-maccatalyst;net9.0-browserwasm;net9.0-desktop</TargetFrameworks>
  <TargetFrameworks Condition="$([MSBuild]::IsOSPlatform('windows'))">$(TargetFrameworks);net9.0-windows10.0.19041.0</TargetFrameworks>
  ```

## 4. SkiaSharp.Views — the conditional reference (key gotcha)

The Windows (WinAppSDK) head uses a different SkiaSharp views package than every other head, but the
control type (`SKXamlCanvas`, namespace `SkiaSharp.Views.Windows`) is the same in both — so only the
package reference is conditional, not the C#.

- [ ] Add to `KumikoUI.Uno.csproj`:
  ```xml
  <!-- All non-Windows Uno heads -->
  <ItemGroup Condition="!$(TargetFramework.Contains('-windows'))">
    <PackageReference Include="SkiaSharp.Views.Uno.WinUI" Version="3.119.2" />
  </ItemGroup>

  <!-- WinAppSDK / Windows head -->
  <ItemGroup Condition="$(TargetFramework.Contains('-windows'))">
    <PackageReference Include="SkiaSharp.Views.WinUI" Version="3.119.2" />
  </ItemGroup>
  ```
- [ ] Keep the SkiaSharp version pinned to **`3.119.2`** — the same version used by
  [`KumikoUI.SkiaSharp`](../../src/KumikoUI.SkiaSharp/KumikoUI.SkiaSharp.csproj) and
  [`KumikoUI.Maui`](../../src/KumikoUI.Maui/KumikoUI.Maui.csproj) — to avoid native-binary conflicts.

## 5. Library layout / assets

- [ ] Enable Uno library layout so fonts and other assets resolve from the packaged library (needed by
  [05 — Fonts & assets](05-fonts-and-assets.md)):
  ```xml
  <PropertyGroup>
    <GenerateLibraryLayout>true</GenerateLibraryLayout>
  </PropertyGroup>
  ```

## 6. NuGet metadata (mirror the sibling projects)

- [ ] Add package metadata to the `<PropertyGroup>` (Authors/License/URLs/icon/README come for free
  from [`Directory.Build.props`](../../Directory.Build.props)):
  ```xml
  <PackageId>KumikoUI.Uno</PackageId>
  <Version>1.0.0</Version>
  <Description>A high-performance, fully custom-drawn KumikoUI DataGrid control for the Uno Platform. Renders via SkiaSharp (SKXamlCanvas) across Windows, Desktop, WebAssembly, Android, iOS, and Mac Catalyst — reusing the same engine as KumikoUI.Maui.</Description>
  <PackageTags>uno;unoplatform;winui;datagrid;grid;table;skiasharp;dotnet;cross-platform</PackageTags>
  ```
- [ ] Keep `ImplicitUsings`, `Nullable`, and `GenerateDocumentationFile` consistent with the other
  `src/` projects (`enable`/`enable`/`true`).

## 7. Build verification

- [ ] Restore + build the heads available on the current OS:
  ```bash
  dotnet build src/KumikoUI.Uno/KumikoUI.Uno.csproj -c Release           # all heads valid on this OS
  dotnet build src/KumikoUI.Uno/KumikoUI.Uno.csproj -c Release -f net9.0-desktop   # single head
  ```
- [ ] Pack dry-run (proves it is NuGet-packable, like the CI does for the other projects):
  ```bash
  dotnet pack src/KumikoUI.Uno/KumikoUI.Uno.csproj -c Release -p:Version=0.0.0-ci -o /tmp/uno-pack
  ```

---

## ✅ Exit criteria

- [ ] `KumikoUI.Uno` is in `KumikoUI.sln` and references **only** `KumikoUI.Core` + `KumikoUI.SkiaSharp`.
- [ ] No `global.json` was introduced at the repo root.
- [ ] The project builds for `net9.0-desktop` (and other current-OS heads) in Release.
- [ ] `dotnet pack` produces a `KumikoUI.Uno.*.nupkg`.
- [ ] `KumikoUI.Core`, `KumikoUI.SkiaSharp`, and `KumikoUI.Maui` are byte-for-byte unchanged.

➡️ Next: [03 — DataGridView host](03-datagridview-host.md)

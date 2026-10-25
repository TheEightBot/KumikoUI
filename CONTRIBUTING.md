# Contributing to KumikoUI

Thanks for helping make KumikoUI better. This guide covers how to report problems, set up a machine, make a change and get it merged, and how releases work.

By participating you agree to follow the [Code of Conduct](CODE_OF_CONDUCT.md). Security problems go through [SECURITY.md](SECURITY.md), never public issues.

## Ways to contribute

- **Report a bug or request a feature** with the [issue forms](https://github.com/TheEightBot/KumikoUI/issues/new/choose). Bug reports need the package, version, host (MAUI or Uno), platform and a small repro.
- **Ask a question or share an idea** in [Discussions](https://github.com/TheEightBot/KumikoUI/discussions).
- **Fix or build something.** Comment on the issue first so we can agree on the approach; for anything larger than a bug fix, open an issue before writing code. The [roadmap](docs/ROADMAP.md) and the [1.0 checklist](https://github.com/TheEightBot/KumikoUI/issues/114) show what is planned.

## Toolchain

The repository pins its toolchain so every machine and CI build the same way:

| What | Where | Value |
|---|---|---|
| .NET SDK | `global.json` → `sdk.version` (roll forward: latest patch) | 10.0.401 |
| Workload set used by CI (MAUI, iOS, Android, Mac Catalyst, WebAssembly) | `.github/actions/setup-dotnet/action.yml` | 10.0.402.1 |
| Uno.Sdk | `global.json` → `msbuild-sdks` | 6.7.30 |
| NuGet package versions | `Directory.Packages.props` (central package management) | — |
| Package sources | `nuget.config` (nuget.org only) | — |

Package versions live only in `Directory.Packages.props`; project files reference packages without a `Version`. SkiaSharp is a single `SkiaSharpVersion` property there, which Uno.Sdk also uses for its implicit packages.

## Setup

1. Install the .NET SDK band from `global.json` (any 10.0.40x patch).
2. Install the workloads you need, ideally from the same workload set CI uses. The platform-agnostic projects (Core, SkiaSharp, their tests) need none.

   ```bash
   dotnet workload install maui wasm-tools --version 10.0.402.1
   ```

   The workload set is deliberately not pinned in `global.json`: a pin there makes every build, including Core-only builds, require that exact set to be installed.

   On macOS these need `sudo` when the SDK lives in a system location. `uno-check` (https://aka.platform.uno/uno-check) can verify a machine for the Uno heads.
3. Platform requirements for app heads: Xcode for iOS and Mac Catalyst (macOS only), the Android SDK for Android, Windows for the WinAppSDK heads.

## Build and test

Build what you are working on; the platform-agnostic projects need no workloads:

```bash
dotnet build src/KumikoUI.Core
dotnet build src/KumikoUI.SkiaSharp
dotnet test tests/KumikoUI.Core.Tests
```

Check public type names after building (see [Public type names](#public-type-names)):

```bash
dotnet run --project eng/TypeNameCheck -c Release -- \
  src/KumikoUI.Core/bin/Release/net10.0/KumikoUI.Core.dll \
  src/KumikoUI.SkiaSharp/bin/Release/net10.0/KumikoUI.SkiaSharp.dll
```

Hosts and samples (one head at a time with `-f`):

```bash
dotnet build src/KumikoUI.Maui -f net10.0-maccatalyst
dotnet build src/KumikoUI.Uno -f net10.0-desktop
dotnet test tests/KumikoUI.Uno.Tests
dotnet run --project samples/SampleApp.Uno -f net10.0-desktop
```

`dotnet build KumikoUI.sln` builds every head and therefore needs every workload, plus Windows for the WinAppSDK heads.

## Updating the toolchain

- **SDK or workloads:** update `sdk.version` in `global.json` and the workload set in `.github/actions/setup-dotnet/action.yml` together, plus the command above (`dotnet workload --version` prints the installed set). Workload sets are tied to an SDK feature band.
- **Uno.Sdk:** update `msbuild-sdks` in `global.json` and rebuild every head; Uno changes its implicit packages between versions.
- **Packages:** change the version in `Directory.Packages.props` only.
- **Minimum OS versions:** change the `KumikoMin*Version` properties in `Directory.Build.props` and the README "Supported platforms" table together. They are re-baselined at each .NET major release.

## Versions and releases

Package versions come from git tags through [MinVer](https://github.com/adamralph/minver); no project file contains a version.

- A commit tagged `vX.Y.Z` (or `vX.Y.Z-suffix`) builds exactly that version for all four packages.
- Any other commit builds the next version as a prerelease, for example `0.2.0-alpha.0.8`: eight commits after the last tag. `MinVerMinimumMajorMinor` in `Directory.Build.props` sets the floor (currently 0.2).
- Clones need tags and history (`git fetch --tags`, no shallow clone) or MinVer falls back to `0.2.0-alpha.0`.

To release:

1. In [CHANGELOG.md](CHANGELOG.md), rename `## [Unreleased]` to `## [X.Y.Z] - YYYY-MM-DD`, add a fresh empty `## [Unreleased]` above it, and update the compare links at the bottom. Merge that through a pull request. Stable releases fail to publish without their section; prereleases fall back to `Unreleased`.
2. Push a version tag on that commit on `main`:

```bash
git tag v0.2.0
git push origin v0.2.0
```

The section becomes the GitHub release notes, and every package links to the CHANGELOG. Versioning rules (SemVer, lockstep packages, deprecation, support window) are in [docs/VERSIONING.md](docs/VERSIONING.md).

The publish workflow refuses to run unless the ref is a version tag, the tagged commit is on `main`, and MinVer computes the same version as the tag. It then runs the Core and Uno test suites and waits for approval in the `nuget-publish` environment before pushing to NuGet.org. Symbol packages (`.snupkg`) are produced for every package by default. Only admins can create `v*` tags.

## Making a change

### Branches

`main` is the only long-lived branch; releases are tags on `main`. Create a short-lived branch from `main` named after the issue:

```
<type>/<issue>-<short-description>      e.g. fix/25-pinned-summary-footer, chore/12-cpm-pins
```

Fork the repository if you don't have write access.

### Commits

Commit messages follow [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/):

```
<type>(<optional scope>): <summary in the imperative, lower case>

<body: what changed and why>

Closes #<issue>
```

| Type | Use for |
|---|---|
| `feat` | a new capability |
| `fix` | a bug fix |
| `perf` | a performance improvement |
| `refactor` | a code change that neither fixes a bug nor adds a feature |
| `test` | tests only |
| `docs` | documentation only |
| `ci`, `build`, `chore` | pipelines, build system, maintenance |

Scopes name the area: `core`, `skiasharp`, `maui`, `uno`, `samples`. Mark breaking changes with `!` after the type (`feat(core)!: …`) and a `BREAKING CHANGE:` footer; until 1.0 they are allowed but must be called out in the CHANGELOG.

### Code

- Follow the conventions in [`.github/instructions/`](.github/instructions) and the surrounding code: nullable enabled, `float` coordinates, `KumikoColor` for colours, no LINQ or allocations in the render loop.
- Every public member gets an XML doc comment.
- One public type per file, in a file named after the type (`KumikoStyle` lives in `KumikoStyle.cs`). Small enums and event-args types get their own file too.
- Public type names follow the rules in [Public type names](#public-type-names).
- KumikoUI.Core must stay free of UI-framework and SkiaSharp references; host-specific code belongs in KumikoUI.Maui or KumikoUI.Uno.
- Add or update tests with every behaviour change (xUnit; see [`tests/`](tests)). Bug fixes start with a failing test.

### Public type names

KumikoUI types are imported next to other UI frameworks' types, so a public type must never share its simple name with a public type of:

- .NET MAUI (`Microsoft.Maui.*`) and CommunityToolkit.Maui (`CommunityToolkit.Maui.*`)
- WinUI and Uno (`Microsoft.UI.Xaml.*`, including Uno's own types) and the Windows Community Toolkit DataGrid (`CommunityToolkit.WinUI.*`)
- WPF and Windows Forms (`System.Windows.*`)
- Avalonia and its DataGrid (`Avalonia.*`)
- another public KumikoUI type (two hosts may each define the same type, such as `KumikoDataGrid`)

`SelectionMode`, `FocusManager`, `TextChangedEventArgs`, `HitTestResult` and `DataGridColumn` are examples that clash; other grids use the `DataGrid` and `Grid` prefixes themselves. KumikoUI therefore uses one prefix:

| Prefix | For |
|---|---|
| `Kumiko` | every grid type: the host control and entry points (`KumikoDataGrid`, `KumikoFonts`), models (`KumikoSource`, `KumikoColumn`, `KumikoStyle`, `KumikoCellStyle`), enums (`KumikoSelectionMode`, `KumikoSortDirection`), primitives (`KumikoRect`, `KumikoColor`, `KumikoFont`), input (`KumikoKey`, `KumikoPointerEventArgs`) and event arguments (`KumikoTextChangedEventArgs`) |
| `Drawn` | drawn components and the types that belong to them (`DrawnTextBox`, `DrawnComboBox`, `DrawnComboBoxItem`) |

Do not introduce new `DataGrid*` or `Grid*` type names. Other types keep descriptive, unprefixed names (`FilterDescription`, `EditSession`, `TableSummaryRow`) as long as the check below passes.

CI enforces this with `eng/TypeNameCheck`, a small console tool (not part of `KumikoUI.sln` and not packed). It lists the public top-level type names in the built KumikoUI assemblies and compares them with each other and with the names read, via `System.Reflection.Metadata`, from pinned packages: `Microsoft.Maui.Controls.Core`, `.Xaml`, `Microsoft.Maui.Core` and `Microsoft.Maui.Graphics` (at `MauiVersion`); `Microsoft.WindowsAppSDK.WinUI` and `Uno.WinUI`; `Microsoft.WindowsDesktop.App.Ref`; `CommunityToolkit.WinUI.UI.Controls.DataGrid`; `CommunityToolkit.Maui`; `Avalonia` and `Avalonia.Controls.DataGrid`. The versions are in `Directory.Packages.props`; the tool needs no workloads and runs on any OS:

```bash
dotnet run --project eng/TypeNameCheck -c Release -- src/KumikoUI.Core/bin/Release/net10.0/KumikoUI.Core.dll src/KumikoUI.SkiaSharp/bin/Release/net10.0/KumikoUI.SkiaSharp.dll
```

It exits non-zero and lists every collision. Raising one of those versions can surface a new collision, which is then fixed by renaming the KumikoUI type.

### Pull requests

1. Rebase on the latest `main`, build the projects you touched and run the tests.
2. Open a pull request against `main` and fill in the template: what changed, why, how you verified it, and anything not verified (devices, platforms).
3. Add a line to the `Unreleased` section of [CHANGELOG.md](CHANGELOG.md) for user-visible changes.
4. CI must pass (`Build & Test (Core + SkiaSharp)`, `Build (KumikoUI.Maui — all TFMs)`, `Build & Test (KumikoUI.Uno — all heads)`), CodeQL must not report new alerts, and review threads must be resolved.
5. A maintainer merges by squash or rebase; `main` keeps a linear history.

The branch protection and other repository settings are kept in [`.github/settings/`](.github/settings/README.md).


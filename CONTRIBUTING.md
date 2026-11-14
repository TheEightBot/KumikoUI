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
dotnet test tests/KumikoUI.SkiaSharp.Tests
dotnet test tests/KumikoUI.SourceGenerators.Tests
```

`tests/KumikoUI.SkiaSharp.Tests` renders a small grid to a bitmap and compares it with the PNG in its `Goldens/` folder, with a tolerance for each platform's text anti-aliasing (see `GoldenImage.cs`). After an intended rendering change, regenerate the image with `KUMIKO_UPDATE_GOLDENS=1 dotnet test tests/KumikoUI.SkiaSharp.Tests` and commit it; a failing comparison writes the actual and diff images to `GoldenFailures/` in the test output folder.

The libraries are `IsAotCompatible`: a trimming or Native AOT analyzer warning (IL2xxx/IL3xxx) fails the build (`Directory.Build.targets`). Read item properties through `KumikoAccessorRegistry` rather than reflection (see "Native AOT & Trimming" in the README). Property-access benchmarks live in `bench/` (see `bench/README.md`).

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

The publish workflow refuses to run unless the ref is a version tag, the tagged commit is on `main`, and MinVer computes the same version as the tag. It then runs the Core and Uno test suites, packs all four packages, and fails unless each `.nupkg` has a matching `.snupkg` symbol package and carries the CHANGELOG section as its release notes. The publish job then waits for approval in the `nuget-publish` environment before pushing to NuGet.org. Only admins can create `v*` tags.

To rehearse a release, run the **Publish NuGet Packages** workflow manually with **dry run** on (the default), from any branch. It runs the same resolve, test, pack and validate jobs as a tag push but never pushes to NuGet.org or creates a GitHub Release. To retry a failed publish, run it from the `vX.Y.Z` tag with dry run off. Runs are serialized per ref, so a tag push and a manual retry cannot publish at the same time.

### NuGet Trusted Publishing (one-time maintainer setup)

The workflow authenticates to NuGet.org with [Trusted Publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing): the publish job exchanges a GitHub OIDC token for a short-lived API key through `NuGet/login`. No API key is stored as a secret. Before the first release with this workflow:

1. Sign in to nuget.org as the owner of the four packages (or of the organization that owns them), open **Trusted Publishing**, and add a policy with:
   - Repository owner: `TheEightBot`
   - Repository: `KumikoUI`
   - Workflow file: `publish.yml`
   - Environment: `nuget-publish`
   For a package ID that does not exist yet, create the policy as pending so the first push can claim it.
2. In the GitHub repository, add the variable `NUGET_USER` (Settings, Secrets and variables, Actions, Variables) containing the nuget.org profile name (not the email address) that owns the policy.
3. After one successful release, delete the old `EIGHTBOT_NUGET_APIKEY` secret and revoke that key on nuget.org.

Only the publish job has `id-token: write`, and the policy is bound to the `nuget-publish` environment, so the approval and `v*` tag restrictions apply to getting a key.

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

- Follow the conventions in [`.github/instructions/`](.github/instructions) and the surrounding code: nullable enabled, `float` coordinates, `KumikoColor` for colors, no LINQ or allocations in the render loop.
- Every public member gets an XML doc comment (summary, plus `<param>`/`<returns>` where they add information; `<inheritdoc/>` for overrides and interface implementations; document each enum member). The build enforces it: in the four shipped libraries a missing comment (CS1591) or a malformed or unresolvable one (CS1570-CS1734, see `Directory.Build.targets`) is a compile error, not a warning.
- One public type per file, in a file named after the type (`KumikoStyle` lives in `KumikoStyle.cs`). Small enums and event-args types get their own file too.
- Public type names follow the rules in [Public type names](#public-type-names).
- KumikoUI.Core must stay free of UI-framework and SkiaSharp references; host-specific code belongs in KumikoUI.Maui or KumikoUI.Uno.
- Add or update tests with every behavior change (xUnit; see [`tests/`](tests)). Bug fixes start with a failing test.

### Public API and formatting

**Recording an API change.** The public surface of the four shipped libraries (Core, SkiaSharp, Maui, Uno) is recorded in `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt` next to each project and checked by `Microsoft.CodeAnalysis.PublicApiAnalyzers`. Adding, changing or removing a public or protected member without updating those files fails the build (RS0016 for an unrecorded member, RS0017 for a recorded one that no longer exists). RS0026 and RS0027 are errors as well: only one overload of a method may have optional parameters, and it must be the one with the most parameters, so a later optional parameter can be added without a break. Nothing has shipped as a stable release yet, so every `Shipped` file is empty and the whole surface is in the `Unshipped` files; at the first stable release the maintainer moves the `Unshipped` lines into `Shipped` (the exact steps are in [the 1.0 API review](docs/release-readiness/api-review-1.0.md#steps-at-the-10-tag)), and from then on a removal or change of a shipped line is a breaking change that must be called out in the CHANGELOG.

To record a change, either:

- run `eng/update-public-api.sh` (all four libraries) or `eng/update-public-api.sh Core` (named ones), which rewrites the `Unshipped` files from the current source for every target framework the machine can build, then review `git diff -- '*PublicAPI*.txt'`; or
- apply the RS0016 code fix in your IDE ("Add to public API"), or edit `PublicAPI.Unshipped.txt` by hand. Lines are sorted and look like `KumikoUI.Core.Models.KumikoColumn.Header.get -> string!`; the first line of a file is `#nullable enable` in the `Shipped` file.

KumikoUI.Maui and KumikoUI.Uno target several frameworks. Members every framework has live in the project's own `PublicAPI.Unshipped.txt`; members only some frameworks have (for example the `GlobalStaticResources` class the Uno XAML generator adds to every Uno head) live in `PublicAPI/<target framework>/PublicAPI.Unshipped.txt`. The Windows heads cannot be built on macOS or Linux, so CI is the first to check them; if the Windows build reports RS0016 or RS0017, add or remove the line in `PublicAPI/net10.0-windows10.0.19041.0/` (Maui) or `PublicAPI/net10.0-windows10.0.26100/` (Uno), each with its own `PublicAPI.Shipped.txt` containing `#nullable enable`.

Package validation (`EnablePackageValidation`) also runs when the packages are packed. Once the first stable version is out, the maintainer enables `PackageValidationBaselineVersion` (see `Directory.Build.props`) so that a breaking change against the last release fails the pack.

**Formatting.** [`.editorconfig`](.editorconfig) records the whitespace rules the code follows (four-space indentation, Allman braces, LF line endings, final newline). CI runs:

```bash
dotnet format whitespace --folder . --verify-no-changes
```

Run `dotnet format whitespace --folder .` to fix what it reports before you push; it only changes whitespace.

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

### Documentation snippets

Every ` ```csharp ` and ` ```xml ` block in `README.md` and the NuGet READMEs (`docs/nuget/*.md`) is compiled in CI against both hosts, so a documented member that is renamed or removed fails the build. `eng/DocsSnippets` (a console tool, not part of `KumikoUI.sln` and not packed) extracts the blocks into git-ignored `Generated/` folders; `tests/KumikoUI.Docs.Snippets.Maui` and `tests/KumikoUI.Docs.Snippets.Uno` compile them. Nothing is run. Compiler errors point at the README line (`README.md(266,39): error CS0117 ...`).

To check your edit locally, extract and then build the host you can (MAUI needs the MAUI workloads; the Uno head builds on any OS):

```bash
dotnet run --project eng/DocsSnippets -c Release
dotnet build tests/KumikoUI.Docs.Snippets.Uno -f net10.0-desktop -c Release
dotnet build tests/KumikoUI.Docs.Snippets.Maui -f net10.0-maccatalyst -c Release
```

Build in Release: that is the configuration where MAUI's XamlC validates the XAML. The tool prints how many C# and XAML snippets each host got and exits non-zero for a block it cannot classify, so nothing is skipped silently.

**What needs no annotation.** A C# block is compiled for both hosts. It is wrapped in a method if it is statements, and put in its own namespace if it declares types (the tool tells them apart). The `using` directives and objects the README says snippets assume (`kumiko`, `employees`, `vm`, `Employee`, ...) come from the README's own `using` block and from `tests/KumikoUI.Docs.Snippets.Shared/Fixtures.cs`; add a member there only when a snippet needs one the README states as an assumption. An XML block that starts with a `ContentPage` or `Page` is a page for the host its `xmlns` names. One that starts with a `kumiko:`, `core:` or `render:` element is wrapped in a data grid and a page of each host, with the three prefixes declared as the README describes.

**Annotations.** When a block needs more, put an HTML comment on the line before the fence (indented like the fence inside a list). It is invisible in the rendered Markdown:

| Annotation | Meaning |
|---|---|
| `<!-- snippet: maui -->` or `uno` | Compile for that host only (the block uses a host-specific type or namespace). `both` is the default. |
| `<!-- snippet: usings -->` | The README's standard `using` block: it is prepended to every other C# snippet of that file. A `KumikoUI.Maui` or `KumikoUI.Uno` line in it is replaced with the host's. |
| `<!-- snippet: shared -->` | A block of type declarations that other snippets of the file use too (for example a renderer a later snippet instantiates). |
| `<!-- snippet: members=Base.Type -->` | Class members (for example an override), compiled inside a class that derives from `Base.Type`. Combine with a host: `<!-- snippet: uno members=Microsoft.UI.Xaml.Application -->`. |
| `<!-- snippet: statements -->` or `file` | Overrides the detected kind. |
| `<!-- snippet: skip reason -->` | Not compiled. Use it only for text that is not C# or XAML (a project-file fragment, for example) and say why; everything after `skip` is the reason, and the tool lists every skipped block. |

An annotation that is not directly followed by a code block, or has an unknown word, is an error. Shell and plain blocks are ignored.

**Writing snippets that compile.** Keep a snippet short and complete enough to compile as written; if it only works with something the text does not show, either show it or state the assumption in the README and add it to `Fixtures.cs`. If a snippet fails on one host only, annotate it `maui` or `uno` rather than hiding the failure. To add other Markdown files, pass `--file` (repeatable, relative to the repository root) to the tool; the default list is `README.md` and `docs/nuget/*.md`, and a file that does not exist is skipped. The NuGet READMEs carry no annotations (nuget.org cannot render HTML comments), so the tool infers what it needs: statements followed by type declarations are split into a method and a namespace, a block that names `MauiApp`, `ContentPage` or `Microsoft.UI.Xaml` is compiled for that host, a block in `README.KumikoUI.Maui.md` or `README.KumikoUI.Uno.md` for that host (Core and SkiaSharp for both), and a `partial` page class is paired with the XAML page whose `x:Class` names it. The `.github/instructions/*.md` files hold API fragments rather than compilable examples, so they are not in the default list.

CI (`Build (Documentation snippets — MAUI + Uno)`) runs this on every pull request that changes code or one of those Markdown files.

### Pull requests

1. Rebase on the latest `main`, build the projects you touched and run the tests.
2. Open a pull request against `main` and fill in the template: what changed, why, how you verified it, and anything not verified (devices, platforms).
3. Add a line to the `Unreleased` section of [CHANGELOG.md](CHANGELOG.md) for user-visible changes.
4. CI must pass (`Build & Test (Core + SkiaSharp)`, `Build (KumikoUI.Maui — all TFMs)`, `Build & Test (KumikoUI.Uno — all heads)`), CodeQL must not report new alerts, and review threads must be resolved.
5. A maintainer merges by squash or rebase; `main` keeps a linear history.

The branch protection and other repository settings are kept in [`.github/settings/`](.github/settings/README.md).


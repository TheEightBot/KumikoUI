# Spec: API reference site (DocFX) and cookbook for custom columns, renderers and editors

Issue: [#110](https://github.com/TheEightBot/KumikoUI/issues/110) (plan key X9, milestone "1.x - Fast follow-ons", blocked by #54, which is merged).
Finding: DOC-07 section 4, items 10 and 11 in [release-readiness/06-docs.md](../release-readiness/06-docs.md).
Status: draft for review. Written against `main` at c77f44b plus three unmerged branches it builds on (see section 10).

## 1. Problem

KumikoUI generates XML docs for all four packages (and, since #188, a missing or malformed comment fails the build), but nobody can browse them: there is no API reference site. The recipes for extending the grid (a custom cell renderer, editor, column) live only in `.github/prompts/*.prompt.md`, which are instructions for AI-assisted contributors and are invisible to people who install the packages. The README is being reduced to a landing page (PR #203), so the depth has to live somewhere else.

## 2. Goals and non-goals

Goals

1. A browsable, searchable API reference for `KumikoUI.Core`, `KumikoUI.SkiaSharp`, `KumikoUI.Maui` and `KumikoUI.Uno`, generated from the XML docs and published on GitHub Pages.
2. Conceptual guides (architecture, rendering, text input, versioning) reachable from the same site without being moved or duplicated.
3. A cookbook of task-oriented recipes for the extension points, where every code sample is compiled in CI against both hosts, so a renamed member fails a pull request instead of silently rotting.
4. One version of the site per release line, so a user on 1.2 reads 1.2 docs, plus a `next` site from `main`.
5. A docs build that runs on every relevant pull request (broken cross references and links fail it) and a deploy that needs only the minimum token permissions.
6. Every cookbook recipe linked from the API pages it uses (`<see cref>` and xref), and from the NuGet READMEs (PR #193) and README (PR #203).

Non-goals

- A new extensibility API. This spec documents the extension points that exist; where a recipe needs a workaround (see recipe 4) that is recorded as an open question, not designed here.
- Theming reference table, FAQ, accessibility statement, performance guidance, Uno getting-started, platform matrix (DOC-07 items 2, 3, 5, 6, 8, 9). The site gives them a home, but they are separate documentation issues; this spec only reserves their place in the table of contents.
- Rewriting ARCHITECTURE.md or RENDERING.md (DOC-09, DOC-10). The site publishes what is in the repo; fixing it is separate work, and a link check will not detect factual drift.
- Localizing the documentation site itself, a custom domain, analytics, a blog, an API playground, or a live WebAssembly demo.
- Publishing `docs/uno/` (a historical port plan), `docs/release-readiness/`, `docs/research/` or `docs/ROADMAP.md`. They stay in the repo, outside the site.

## 3. Tool choice

### 3.1 Options

| Option | Fit | Verdict |
|---|---|---|
| DocFX (`dotnet tool`, 2.x) | One tool for API metadata (Roslyn), Markdown, table of contents, xref cross-references, built-in client-side search, a modern template with dark mode. Reads project files or assemblies plus XML docs. The common choice for .NET OSS. Cons: configuration is verbose; its Roslyn must understand the language level the code uses; versioning is "one build per version", not built in. | **Recommended** |
| xmldocmd or DefaultDocumentation, plus a static site generator (MkDocs Material, Docusaurus, Astro Starlight) | Produces Markdown per type from an assembly; the site tool then adds search and navigation. Pros: attractive themes, simple versioning (mike for MkDocs). Cons: two toolchains (.NET plus Node or Python) in CI and in contributors' heads, no xref between conceptual pages and API pages (links are hand-written and rot), and generated Markdown is large and noisy in review. | Rejected |
| Sandcastle Help File Builder | Windows only, heavy, dated output. | Rejected |
| Statiq Docs, Wyam | Smaller ecosystem and maintenance risk. | Rejected |
| Publishing only the XML docs (IntelliSense) | Status quo. | Rejected |

DocFX wins on the property that matters most here: API pages and prose pages live in one reference graph, so `<xref:KumikoUI.Core.Rendering.ICellRenderer>` in a recipe and `<see cref>` in the XML docs both resolve, and DocFX reports the ones that do not.

### 3.2 Language level

`KumikoUI.Core` uses C# 14 features (for example the `field` keyword in `KumikoStyle`). DocFX parses source with its bundled Roslyn, so the pinned DocFX version must be new enough to parse it. Reading compiled assemblies plus XML docs sidesteps this (Roslyn only reads metadata), which is why assembly mode stays a documented fallback (3.3). The first implementation issue is a spike that proves one mode end to end before anything else is built; the DocFX version is then pinned in a `dotnet-tools.json` manifest (a local tool, so `dotnet tool restore` gives contributors and CI the same version).

### 3.3 Generating metadata for multi-TFM and platform-specific projects

| Package | Target frameworks | Metadata source |
|---|---|---|
| KumikoUI.Core | `net10.0` | The project, or `bin/Release/net10.0/KumikoUI.Core.dll` plus its XML. |
| KumikoUI.SkiaSharp | `net10.0` | Same. |
| KumikoUI.Uno | `net10.0`, `net10.0-ios`, `-android`, `-browserwasm`, `-desktop` (and `net10.0-windows10.0.26100` on Windows) | The plain `net10.0` target, which has no platform-specific source and should build without a mobile workload (verify in the spike). |
| KumikoUI.Maui | `net10.0-android`, `-ios`, `-maccatalyst` (and `net10.0-windows10.0.19041.0` on Windows) | One platform target: `net10.0-android` on an Ubuntu runner (needs the MAUI Android workload) or `net10.0-maccatalyst` on the macOS runner the snippet job already uses. |

Rule: one reference target framework per package, chosen as the one that builds on the cheapest runner and exposes the cross-platform public surface. The API reference is not per platform. Platform-specific members are rare; where one exists it carries a remark in its XML docs (`<remarks>Android only.</remarks>`), which is more honest than four near-identical API trees. The Windows targets are never the reference target (they build only on Windows).

Two ways to feed DocFX, in order of preference (the spike decides):

1. **Project mode.** `metadata[].src` points at the four `.csproj` files with `properties` setting `TargetFramework` to the reference target and `Configuration` to `Release`. DocFX builds through MSBuild, so references (SkiaSharp, MAUI, Uno) resolve and inherited members from base types such as `ContentView` link correctly. Needs the workloads in the docs job.
2. **Assembly mode.** CI builds the packages first (it already does), and `metadata[].src` points at the `bin/Release/<tfm>/*.dll` of each package, with `references` globs for the dependencies. No Roslyn parsing, so no language-level risk, but base types from the host frameworks show as plain text unless the references are supplied.

To verify in the spike: that the public API of each multi-target package is identical across its targets (a diff of the metadata from two targets). If a package exposes extra members on one platform, the reference target is the one with the superset and each such member gets the remark above.

Because the four packages version in lockstep (docs/VERSIONING.md), the site is one site with four package groups, not four sites.

### 3.4 What the API reference excludes

`filter.yml` hides `KumikoUI.SourceGenerators` (an analyzer users never reference), members marked `[EditorBrowsable(EditorBrowsableState.Never)]`, and generated files under `obj/`. `[KumikoGridItem]` is public API and stays. The `buildTransitive` targets are explained in the AOT recipe, not listed as API.

## 4. Site structure

The source of truth stays where it is. DocFX reads `docs/` in place (`build.content[].src` pointing at the repository root with explicit file lists), so GitHub-rendered relative links keep working and nothing moves. New site-only files live under `docs/site/`.

```
docs/site/
  docfx.json
  filter.yml
  index.md                     landing page (short; links the three entry points)
  toc.yml                      top navigation: Guides | Cookbook | API reference
  guides/toc.yml               includes docs/ARCHITECTURE.md, RENDERING.md, TEXT-INPUT.md, VERSIONING.md by file
  cookbook/toc.yml
  cookbook/index.md            what each recipe covers, prerequisites, how samples are verified
  cookbook/*.md                one page per recipe (section 5)
  api/                         generated, git-ignored
  images/                      logo and screenshots used by the site
  templates/                   DocFX template overrides (logo, footer with the version), minimal
```

Top navigation

| Section | Content | Source |
|---|---|---|
| Home | Install, a short example, links | `docs/site/index.md` (not a copy of the README, to avoid two sources of truth; it links to nuget.org and the repository) |
| Guides | Architecture, Rendering pipeline, Text input, Versioning and support. Reserved slots, filled by their own issues: Theming reference, Platform matrix, Uno getting started, FAQ, Performance, Accessibility | `docs/*.md` in place |
| Cookbook | Recipes in section 5 | `docs/site/cookbook/` |
| API reference | Namespaces grouped by package: Core (Models, Rendering, Components, Editing, Data, Input, Layout), SkiaSharp, Maui, Uno | generated |
| Changelog | Rendered `CHANGELOG.md` | included in place |
| Contributing | `CONTRIBUTING.md` | included in place |

Guide files written for GitHub contain links that point outside `docs/` (for example to `../CONTRIBUTING.md` or `../src/...`). Those are either included in the site (CONTRIBUTING, CHANGELOG) or rewritten to absolute GitHub URLs by a small pre-build step in the docs build, not by editing the source files. The docs build fails on any link it cannot resolve (section 9).

## 5. Cookbook

Each recipe has the same shape: the goal in one sentence, when to use it versus the built-in way, a complete minimal sample that compiles, an explanation of the members involved with xref links, behavior notes and pitfalls, and "see also". `cookbook/index.md` states the host assumptions once ("the samples assume the `using` directives below and a grid named `kumiko`"), the same convention the README uses, so the compiled-snippet fixtures (section 6) are shared.

Names below are the public API as of `main` (checked against `src/KumikoUI.Core`).

| # | Recipe | Extension points covered | Notes on content |
|---|---|---|---|
| 1 | Custom cell renderer (for example a star rating) | `ICellRenderer.Render(IDrawingContext, in CellRenderContext)`; `KumikoColumn.CustomCellRenderer` on a `KumikoColumnType.Template` column; `CellRenderContext` (`Bounds`, `Value`, `DisplayText`, `Style`, `CellStyle`, `IsSelected`, `Paints`, `TextPaint`); `IDrawingContext` primitives (`FillRect`, `DrawText`, `MeasureText`, `ClipRect`, `Save`/`Restore`) | The per-frame allocation rule from the interface remarks: reuse paints from `CellRenderContext.Paints` or create them once; do not keep references to the context's reference members. Shows a unit test of the renderer against a recording `IDrawingContext` fake. |
| 2 | Interactive cell (buttons without an editor) | `IInteractiveCellRenderer` (`OnCellTap`, `OnCellActivate`, `HasControlAt`), `ActionButtonsCellRenderer` as the reference implementation | Explains why the grid never opens an editor for such a column and that edit events do not fire. |
| 3 | Custom editor | `KumikoColumn.EditorFactory` (`Func<CellEditorContext, DrawnComponent?>`, sees `InitialCharacter`), `CustomEditorFactory` (older shape), `EditorDescriptor` (XAML-declarable), `ICellEditorProvider` (a renderer that is also the editor factory); `DrawnComponent` overrides (`OnDraw`, `OnPointerDown`/`Up`/`Move`, `OnKeyDown`, `HitTest`, focus hooks); precedence among the mechanisms; commit and validation (`CellValidationEventArgs`, `CellValidationResult`) | A decision table: which mechanism to choose. |
| 4 | Custom column type | A look and edit behavior for a data type the built-ins do not cover, composed from a `Template` column: `CustomCellRenderer` + editor + `CustomComparer` (sort) + `ValueAccessor` (read) | `KumikoColumnType` is a closed enum, so "custom column type" means this composition. Contributors adding a built-in type follow `.github/prompts/add-column-type.prompt.md`; the recipe links to CONTRIBUTING for that. See open question 3. |
| 5 | Conditional styling with resolvers | `KumikoStyle.CellStyleResolver` (`Func<object, KumikoColumn, KumikoCellStyle?>`), `RowStyleResolver` (`Func<object, KumikoRowStyle?>`), `KumikoCellStyle`, `KumikoRowStyle`, `KumikoStyleOptions` | Return null for "no override". Resolvers run per visible cell per frame: no allocation and no I/O; cache style instances. Includes dark-mode-aware colors. |
| 6 | Localization | `KumikoStrings` (an immutable record: `with` expressions, assigned through `KumikoSource.Strings`), `KumikoSource.Culture` for number and date editing and formatting | A Spanish example, switching language at runtime, the filter popup and validation messages. States which strings are not yet localizable. |
| 7 | Theming | `KumikoTheme.Create(KumikoThemeMode)`, `CreateLight`, `CreateDark`, `CreateHighContrast`; copying and tweaking a `KumikoStyle`; `KumikoFonts` and font registration on both hosts | A brand color theme that follows system light and dark. Links to the full theming reference when it exists. |
| 8 | MVVM selection | `KumikoSelectionBinder` (`SelectedItem`, `SelectedItems`, `SelectedIndex`, `CurrentCell`, `SelectionChanged`), the host's selection properties, `ICommand` handling | MAUI and Uno variants side by side. |
| 9 | Live data | `ObservableCollection<T>` and `INotifyPropertyChanged` rows, the incremental update path (single add or remove), replacing the collection for bulk loads, `KumikoSource.DataChanged`, updating from a background thread (marshal to the UI thread) | Contrasts "add 100K items one at a time" with "assign a new collection". Quotes timings only if the benchmarks project backs them. |
| 10 | Native AOT and trimming | `[KumikoGridItem]` and the source generator in `KumikoUI.SourceGenerators`, `KumikoAccessorRegistry` and `IKumikoAccessorProvider`, the `KumikoUI.Core.IsReflectionPropertyAccessSupported` feature switch, `ValueAccessor` | What breaks without the attribute under AOT and how to verify (publish with AOT and run the sample). Cross-references `buildTransitive/KumikoUI.Core.targets`. |

Order: recipes 1, 3 and 5 first (the ones the issue names), then 2, 4, 6, 7, 8, 9, 10. Recipes 1 to 4 share one running example (a rating column: render stars, edit by clicking stars, sort and filter by number) so a reader can follow the whole path.

Each recipe's footer names the Markdown file the compiled snippets come from ("verified by CI"), so a reader knows the sample is not hand-typed.

## 6. Keeping samples compiling

### 6.1 What exists

PR #213 (branch `test/87-compiled-snippets`, unmerged) adds `eng/DocsSnippets`: a console tool that extracts every fenced C# and XAML block from named Markdown files into generated source for `tests/KumikoUI.Docs.Snippets.Maui` (built for `net10.0-maccatalyst` in Release, so XamlC validates the XAML) and `tests/KumikoUI.Docs.Snippets.Uno` (`net10.0-desktop`). It supports annotations in HTML comments (`<!-- snippet: maui | uno | shared | usings | members=Base | skip reason -->`), shared fixtures in `tests/KumikoUI.Docs.Snippets.Shared/Fixtures.cs`, a `--file <path-or-glob>` option, and a CI job (`docs-snippets`) with a `snippets` path filter so Markdown-only pull requests still run it. By default it reads `README.md` and `docs/nuget/*.md`.

This spec depends on #213 being merged and adds no second mechanism.

### 6.2 Options for the cookbook

| Option | How | Pros | Cons |
|---|---|---|---|
| A. Fenced blocks, compiled by DocsSnippets | Recipes are plain Markdown with fenced `csharp` and `xml` blocks; DocsSnippets gets `docs/site/cookbook/*.md` added to its sources | One mechanism for README, NuGet READMEs and cookbook; the Markdown reads the same on GitHub and in DocFX; no DocFX-specific syntax; prose and code are edited in one file | No IDE support while editing; long samples are awkward; the tool wraps statements and namespaces, so a sample is proven to compile but not to behave |
| B. Real source files, included | Samples live in `samples/Cookbook/*.cs` (a normal project that can carry tests); recipes pull regions with DocFX `[!code-csharp[](path#region)]` | Full IDE support; samples can be unit-tested; refactoring renames them | DocFX-only syntax (GitHub shows a link, not code), so recipes are unreadable in pull requests; a second build project and convention beside DocsSnippets; per-host variants need two files |
| C. Hybrid | A for all recipes, plus a tested class in `tests/KumikoUI.Core.Tests` for the one sample that carries logic (the star rating) | Cheap behavioral coverage where it matters | Two copies of that example; drift in code is caught by compile, drift in behavior is not |

Recommendation: A for all recipes, with C's behavior test added for the renderer recipe only if the sample grows logic worth testing. The tool exists, it already handles per-host variants and XAML validation, recipes stay readable on GitHub where contributors review them, and the failure the issue cares about (an API rename silently invalidating docs) is exactly what compilation catches. Option B's main benefit (IDE support) is real but is bought with unreadable pull requests.

### 6.3 Changes needed in DocsSnippets

1. Add `docs/site/cookbook/*.md` to the default source list and support a recursive pattern (`docs/site/**/*.md`): the current glob expands one directory level only.
2. Snippet ids derive from the source path and fence line; confirm two files with the same name in different folders cannot collide.
3. A `usings` block per cookbook file is the existing convention; `cookbook/index.md` documents the standard one so every recipe starts with the same preamble.
4. Fixtures (`Fixtures.cs`) grow only for objects recipes assume (for example `ratings`); recipe types are declared in the recipe with `<!-- snippet: shared -->`.
5. The CI `snippets` filter adds `docs/site/**`, so a cookbook-only change runs the snippet job.
6. A recipe that uses Core types only still compiles in both projects, which is acceptable: it proves the sample builds against both packages.
7. `<!-- snippet: skip reason -->` is for non-C# fragments only (project files, shell). A recipe cannot skip a C# sample to make CI pass.

### 6.4 The docs build also validates

DocFX verifies `xref` and `cref` targets. Together the two checks protect a recipe against a removed API (snippet compile), a renamed type in prose (xref warning) and a moved page (link error).

## 7. Versioning

Release identity comes from MinVer tags (`v` prefix, `MinVerMinimumMajorMinor` 0.2); the four packages share one version.

Site layout on GitHub Pages:

```
/                redirect to the latest stable minor line, e.g. /1.2/   (before 1.0: to /next/)
/next/           built from main on every merge
/1.2/            built from the latest v1.2.x tag; one folder per retained minor line
/versions.json   the list the template's version switcher reads
```

Rules

- A stable tag `vX.Y.Z` builds the site and overwrites `/X.Y/`; a later patch replaces the earlier patch of the same minor line. A prerelease tag publishes nothing beyond `/next/` (which main already updates).
- Retained lines: the current major's supported lines and the previous major while it receives security fixes (docs/VERSIONING.md support window). Older lines stay reachable only through Git tags.
- The footer and title show the version MinVer computed, passed to DocFX through global metadata; `next` shows the commit.
- API and recipes are versioned together: the recipe that shipped with 1.2 compiled against 1.2.

How versions are assembled (GitHub Pages deploys one artifact at a time, so every deploy must contain the whole tree):

1. When a stable release tag is pushed, the docs workflow builds the site for that tag and attaches it as an asset (`site-vX.Y.Z.zip`) to the GitHub Release that `publish.yml` creates. Release assets are immutable per version and cost nothing to store.
2. The deploy job downloads the assets of the retained lines (`gh release download`; public repository, read-only), unpacks each under `/X.Y/`, builds `next` from `main`, writes `versions.json` and the root redirect, and uploads one Pages artifact.
3. There is no `gh-pages` branch, so no `contents: write` is needed to publish.

Why not rebuild old versions from tags each time: the DocFX configuration changes over time, so an old tag cannot be built by today's configuration reliably (and old tags have none). Why not a `gh-pages` branch: it needs a write token and accumulates binary history.

Pre-1.0 simplification: until 1.0, publish only `/next/` and `/latest/` (the latest tag, prerelease or not), both labeled preview. Per-minor retention starts at the first stable minor, which keeps the first deliverable small (stages 1 to 4).

## 8. Hosting, workflows and permissions

Hosting: GitHub Pages with "Source: GitHub Actions". A maintainer switches the repository setting once (and records it in `eng/apply-github-settings.sh` if that script manages Pages; check at implementation). URL: `https://theeightbot.github.io/KumikoUI/`. A custom domain is a non-goal.

New workflow `docs.yml`, following the conventions of `ci.yml` and `publish.yml` (actions pinned by commit SHA, `permissions: contents: read` at the workflow level, concurrency groups, `timeout-minutes`):

| Job | Trigger | Permissions | Does |
|---|---|---|---|
| `build` | `pull_request` (paths: `docs/**`, `src/**`, `CHANGELOG.md`, `CONTRIBUTING.md`, the workflow), `push` to main, push of stable `v*` tags | `contents: read` | Restore tools, set up .NET with the workloads (reuse `.github/actions/setup-dotnet`), build the reference TFMs in Release, `docfx metadata` and `docfx build --warningsAsErrors`, link check (section 9), upload the site as a workflow artifact. |
| `archive` | stable tag `vX.Y.Z` | `contents: write` (this job only) | Waits for the GitHub Release created by `publish.yml` (a release created with `GITHUB_TOKEN` does not trigger the `release` event in other workflows, so it polls with `gh release view`, with a bounded timeout) and uploads `site-vX.Y.Z.zip`. |
| `deploy` | push to main and stable tags, never pull requests | `pages: write`, `id-token: write` (this job only), plus `contents: read` | Assembles all versions (section 7), `actions/upload-pages-artifact`, `actions/deploy-pages`; `environment: github-pages`. |

Notes

- Pull requests from forks get the `build` job with a read-only token and no secrets; they never reach `deploy`.
- `ci.yml` treats documentation as "no code changes" so its required checks stay green. The docs `build` job is not a required check initially; making it required (with the same skipped-counts-as-passed treatment) is part of stage 4 once it is stable.
- Cost: the MAUI reference target needs a workload install, about the same as `docs-snippets`. Cache NuGet and workloads as that job does. If Android on Ubuntu proves flaky, reuse the macOS runner at the cost of runner minutes (open question 2).
- `workflow_dispatch` is allowed for `deploy` only, restricted to `main` (re-deploy after a failed run), mirroring the tag rule in `publish.yml`.

## 9. Link checking, quality gates and search

Quality gates

1. `docfx build --warningsAsErrors` fails on unresolved `xref` and `cref`, invalid file links, duplicate uids and missing toc entries.
2. External links: `lychee` (a pinned release) over the built site with a cache, retries and an allow-list for flaky hosts. On pull requests it fails only for links the pull request added (compared with main), so an unrelated third-party outage does not block merges; a weekly scheduled run opens or updates one issue for rot.
3. Internal anchors: lychee with fragment checking.
4. The compiled-snippet job (section 6) for samples.
5. No spell checker; American English is a review rule (CONTRIBUTING), not a tool.

Search: DocFX's modern template builds a client-side search index at build time covering conceptual and API pages. It needs no service and no extra JavaScript dependency, and is sufficient for this site's size. Algolia DocSearch is a later option if the index becomes slow; it is not planned.

Other site properties: dark mode and mobile layout come from the template; images live in `docs/site/images` and are referenced relatively inside the site (the nuget.org relative-image problem in DOC-01 concerns NuGet READMEs, which use absolute URLs and are not part of the site).

## 10. Related work and ordering

| Item | State | Relationship |
|---|---|---|
| #54 XML docs on every public member, warnings as errors | merged (#188) | Prerequisite met; the API reference is complete by construction. |
| PR #213 compiled snippets (`eng/DocsSnippets`) | unmerged | Hard dependency for stage 3. |
| PR #203 README rewrite | unmerged | The README links to the site and its "learn more" sections point to cookbook pages; wording agreed with that PR. |
| PR #193 per-package NuGet READMEs (`docs/nuget/`) | unmerged | NuGet READMEs link to the matching API namespace and cookbook recipes (absolute URLs); they are also compiled by DocsSnippets. |
| DOC-09, DOC-10 (stale ARCHITECTURE and RENDERING) | open | Should land before or with stage 2, otherwise the site publishes known-wrong pages (open question 6). |
| Theming reference, platform matrix, FAQ | open | Use the reserved toc slots; not blocked by this spec. |

## 11. Effort estimate

One developer who knows the repository, in working days. Excludes review latency and the work the related issues carry.

| Stage | Estimate |
|---|---|
| 1. Spike and skeleton (DocFX mode, TFMs, language level, API diff across TFMs) | 1.5 to 2 |
| 2. Guides, API reference and PR build | 2 |
| 3. Cookbook infrastructure (DocsSnippets glob and CI filter, index page, fixtures) | 1 |
| 3b. Ten recipes at 0.5 to 1 day each (the first three, with the running example, take longer) | 6 to 8 |
| 4. Pages deploy, `next`, link checking, required check | 1.5 to 2 |
| 5. Versioned releases (archive job, version switcher, retention) | 2 |
| 6. Links from README, NuGet READMEs and package metadata; replace duplicated recipe text in `.github/prompts` | 0.5 |
| Total | about 15 to 18 days |

Stages 1 to 4 with the first three recipes (a published site with API reference, guides and the recipes the issue names) are the useful minimum: about 8 to 10 days.

## 12. Staged implementation issues

Titles use the repository's conventional-commit style. Acceptance criteria are checkable by reviewing the pull request.

### Stage 1 - Spike and skeleton

**docs: spike the DocFX metadata pipeline for the multi-target packages (part of #110)**
- Findings in the pull request description state the chosen mode (project or assembly), the DocFX version, the reference target per package, and whether the public API differs between targets of the multi-target packages (with the diff).
- `docs/site/docfx.json` generates metadata for all four packages locally on macOS and on `ubuntu-latest` with the minimum workloads.
- A C# 14 source file (`KumikoStyle`, the `field` keyword) is processed without errors.
- `dotnet-tools.json` pins DocFX; `dotnet tool restore` then `docfx docs/site/docfx.json` works from a clean checkout.
- The MAUI reference target (Android on Ubuntu or Mac Catalyst on macOS) is decided and recorded.

### Stage 2 - Site content and PR build

**docs: DocFX site skeleton with guides and API reference (part of #110)**
- `docs/site/` holds `docfx.json`, `filter.yml`, `index.md`, `toc.yml`; the guides include ARCHITECTURE, RENDERING, TEXT-INPUT, VERSIONING, CHANGELOG and CONTRIBUTING in place (no copies).
- The API reference lists every public type of the four packages, grouped by package; `KumikoUI.SourceGenerators` and internal types are absent (checked explicitly).
- Links in included files that point outside the site resolve (rewritten to GitHub URLs at build time or included); the source documents are not hand-edited for this.
- `docfx build --warningsAsErrors` passes.

**ci: build the documentation site on pull requests (part of #110)**
- `docs.yml` `build` job runs on pull requests that touch `docs/**` or `src/**`, with `contents: read`, pinned action SHAs and a timeout.
- A deliberately broken xref on a test branch fails the job (shown in the pull request description).
- The built site is uploaded as an artifact reviewers can download.
- The job needs no secrets and does not touch the NuGet publish path.

### Stage 3 - Cookbook

**docs: compile cookbook snippets with DocsSnippets (part of #110)** (depends on #213)
- `eng/DocsSnippets` reads `docs/site/**/*.md` (a recursive pattern is supported) in addition to its current defaults.
- The `snippets` path filter in `ci.yml` includes `docs/site/**`; a cookbook-only change runs the `docs-snippets` job.
- `cookbook/index.md` documents the standard `usings` block, fixtures and annotation rules, pointing at CONTRIBUTING for detail rather than duplicating it.
- Renaming a member used by a cookbook sample fails CI (demonstrated once in the pull request).

**docs: cookbook recipes for custom renderer, editor and conditional styling (part of #110)**
- Pages for recipes 1, 3 and 5 with the shared running example; each follows the recipe shape in section 5; every sample compiles on MAUI and Uno via the snippet job.
- Each page links (xref) to the API pages of every type it names and has a "see also".
- The renderer recipe states the per-frame allocation rule; the editor recipe has the decision table for the editor mechanisms.
- Someone other than the author followed the recipes from scratch against a sample app (noted in the pull request).

**docs: cookbook recipes for interactive cells, custom column type, localization and theming (part of #110)**
- Pages for recipes 2, 4, 6 and 7, same criteria. Recipe 4 states plainly that `KumikoColumnType` is a closed enum and shows the Template-column composition.

**docs: cookbook recipes for MVVM selection, live data and Native AOT (part of #110)**
- Pages for recipes 8, 9 and 10, same criteria. The AOT recipe is verified by publishing a sample with AOT once (result in the pull request); the live-data recipe quotes only numbers backed by the benchmarks project.

### Stage 4 - Publish

**ci: publish the documentation site to GitHub Pages (part of #110)**
- The `deploy` job publishes `/next/` (and `/latest/` pre-1.0) on every push to main using `actions/deploy-pages`, with `pages: write` and `id-token: write` granted to that job only, `environment: github-pages`, and no write permission on repository contents.
- The site is reachable at the GitHub Pages URL.
- Pull requests, including from forks, never deploy.
- `workflow_dispatch` re-deploy works only from main.

**ci: check documentation links (part of #110)**
- lychee runs on the built site; on pull requests it fails only for links the change introduced; a weekly scheduled run files or updates one issue on rot; the allow-list is a file in the repo with a reason per entry.
- Anchors are checked.
- After two green weeks, the docs build is proposed as a required check using the skipped-counts-as-passed pattern of the other jobs (a maintainer applies the setting).

### Stage 5 - Versioned releases

**ci: publish one documentation version per release line (part of #110)**
- A stable `vX.Y.Z` tag attaches `site-vX.Y.Z.zip` to its GitHub Release (job-scoped `contents: write`), waiting for the release with a bounded timeout and a clear error.
- The deploy job assembles `/next/`, one folder per retained minor line, `versions.json` and the root redirect; the template shows a version switcher; the footer shows the version.
- Retention follows docs/VERSIONING.md; the rule is a small script with a test.
- A prerelease tag publishes nothing beyond `/next/`.
- A dry run on a test tag (or by `workflow_dispatch`) is documented in the pull request.
- The deploy fails, rather than deploying a partial tree, when a retained version's asset is missing.

### Stage 6 - Cross-links

**docs: link the site from the README, package READMEs and package metadata (part of #110)**
- `PackageProjectUrl` (or a dedicated documentation link) points at the site; each NuGet README links to its API namespace and the relevant recipes with absolute URLs.
- `.github/prompts/*` keep working, but the recipe text they duplicate is replaced by a link to the cookbook, so there is one set of instructions.

## 13. Risks

| Risk | Mitigation |
|---|---|
| DocFX's Roslyn cannot parse the language level, or MAUI and Uno references break project mode | Stage 1 spike; assembly mode fallback; DocFX pinned. |
| Workload install makes the docs job slow or flaky | Reference target per package chosen for cost (Uno `net10.0`; Core and SkiaSharp need none); caching as in `docs-snippets`; the job is path-filtered. |
| Samples compile but do not behave | Snippets prove API shape only. Behavior is covered for the running example by a unit test (recipe 1) and by a from-scratch walkthrough before a recipe merges. |
| The site makes stale ARCHITECTURE and RENDERING pages (DOC-09, DOC-10) more visible | Order those fixes before or with stage 2, or publish them with a banner (open question 6). |
| The Pages artifact must contain all versions; a failed asset download would drop old versions | The deploy job fails when any retained version asset is missing; it never deploys a partial tree. |
| Link checker failing on third-party outages blocks merges | Pull requests fail only for links they introduce; the scheduled full run reports instead of blocking. |
| Two sources of truth for onboarding text (README, landing page, NuGet READMEs) | The landing page links, it does not copy. |

## 14. Open questions

1. **Project mode or assembly mode** for DocFX metadata (section 3.3), and the DocFX version: decided by the stage 1 spike. If neither reads the code at the current language level, the fallback is a pinned DocFX prerelease.
2. **MAUI reference target on Ubuntu (Android workload) or macOS (Mac Catalyst)?** Ubuntu is cheaper and queues faster; macOS reuses the exact setup the snippet job proves works. Needs a maintainer preference on runner cost.
3. **Should "custom column type" get a real extension seam before 1.0?** `KumikoColumnType` is a closed enum, so third parties compose Template columns. If the 1.0 API review (DOC-13) wants a pluggable type registration, recipe 4 changes or waits.
4. **Version retention**: the current major plus the previous major during its support window (proposed), or the latest minor only? Affects stage 5 effort; Pages size is small either way.
5. **Is a pre-1.0 site wanted?** Proposal: publish `/next/` and `/latest/` before 1.0, labeled preview, and start per-minor retention at 1.0. Alternative: hold publication until the 1.0 tag.
6. **Do ARCHITECTURE.md and RENDERING.md ship on the site before DOC-09 and DOC-10 fix them?** Proposal: wait for the fixes, or publish with a visible "contributor notes, may lag the code" banner.
7. **Must the docs build be a required status check?** Proposal: yes after two green weeks (stage 4), using the skipped-counts-as-passed pattern for non-doc changes.
8. **Cookbook tests**: is compile-only enough for the renderer and editor recipes, or should the running example have a tested twin in `tests/KumikoUI.Core.Tests` from the start (option C in 6.2)? The estimate assumes compile-only plus one test for recipe 1.
9. **Host naming in recipes**: samples use the host's grid control name and host-specific `using` lines through the DocsSnippets `usings` convention; if the README rewrite (PR #203) changes the convention, the cookbook index follows.
10. **Ownership of `docs/site/`**: a CODEOWNERS entry, so recipe changes get a reviewer who knows the extension points.

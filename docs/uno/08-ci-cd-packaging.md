# 08 — CI/CD & NuGet Packaging

Goal: extend the existing pipelines so `KumikoUI.Uno` is built on PRs and published to NuGet.org with
the other packages — without disturbing the Core/SkiaSharp/Maui flows.

Existing pipelines: [`.github/workflows/ci.yml`](../../.github/workflows/ci.yml) (build + test) and
[`.github/workflows/publish.yml`](../../.github/workflows/publish.yml) (tag-driven pack + push).

---

## 1. Packaging fact that simplifies everything

`KumikoUI.Uno` is a **class library**, not an app. Library cross-compilation for the `-ios` /
`-maccatalyst` heads works on Windows/Linux with the workloads installed — only **app** build/run/codesign
needs macOS. The only head that strictly requires its host OS to *compile* is the WinAppSDK
`-windows10.0.x` head (Windows).

➡️ Therefore the whole library (all six heads) can be **built and packed on a single `windows-latest`
runner**, exactly like the existing `build-maui` / `pack-maui` jobs.

> The macOS/Windows constraints in [01 §5](01-prerequisites.md#5-per-os-build-constraints) apply to the
> **sample app** (`SampleApp.Uno`) and to *running* heads — not to packing the library.

## 2. CI — add a `build-uno` job

Mirror the `build-maui` job in [`ci.yml`](../../.github/workflows/ci.yml).

- [x] New job, `needs: build-core`, `runs-on: windows-latest`. *(job `build-uno`)*
- [x] Set up .NET `9.x` + `10.x` (same as `build-maui`).
- [x] **Restore Uno workloads** from the project (lighter than full `uno-check` in CI):
  ```yaml
  - name: Restore Uno workloads
    if: steps.workload-cache.outputs.cache-hit != 'true'
    run: dotnet workload restore src/KumikoUI.Uno/KumikoUI.Uno.csproj
  ```
  (Alternative: `dotnet tool install -g uno.check && uno-check --ci --fix --non-interactive`.)
  Gated on the cache miss to match the `build-maui` workload-install pattern.
- [x] Cache workloads with a key over `KumikoUI.Uno.csproj` (copied the `actions/cache@v4` block from
  `build-maui`, new key prefix `uno-workloads-win-`).
- [x] Restore + build + pack dry-run + Uno tests:
  ```yaml
  - run: dotnet restore src/KumikoUI.Uno/KumikoUI.Uno.csproj
  - run: dotnet build  src/KumikoUI.Uno/KumikoUI.Uno.csproj --no-restore -c Release
  - run: dotnet pack   src/KumikoUI.Uno/KumikoUI.Uno.csproj --no-restore -c Release -p:Version=0.0.0-ci -o C:\ci-pack
  - run: dotnet test   tests/KumikoUI.Uno.Tests/KumikoUI.Uno.Tests.csproj -c Release --logger "trx" --results-directory TestResults
  ```
  The `dotnet test` step intentionally lets the test project restore itself (no `--no-restore`): the
  test csproj is a separate Uno single-project head with its own xUnit/test-SDK packages that the
  `dotnet restore` of the *library* csproj above does not pull in.
- [ ] (Optional, not added) a `macos-latest` job that builds the **sample app's** Apple heads
  (`dotnet build samples/SampleApp.Uno -f net9.0-ios` / `-f net9.0-maccatalyst`) to catch app-level
  breakage the library pack can't. Skipped — Phase 08 scope is the library pipeline; can be added later.

## 3. Publish — add a `pack-uno` job

Mirror `pack-maui` in [`publish.yml`](../../.github/workflows/publish.yml).

- [x] New job `pack-uno`, `needs: resolve-version`, `runs-on: windows-latest`; sets up .NET 9 + 10;
  restores Uno workloads (with cache, gated on cache-miss) as in §2.
- [x] Pack with the resolved version and symbols (match the other pack jobs). Implemented with the
  PowerShell line-continuation (`` ` ``) form to match the sibling `pack-maui` job on the same
  Windows runner:
  ```yaml
  - name: Pack — KumikoUI.Uno
    run: |
      dotnet pack src/KumikoUI.Uno/KumikoUI.Uno.csproj `
        --no-restore -c Release `
        -p:Version=${{ env.VERSION }} `
        --include-symbols -p:SymbolPackageFormat=snupkg `
        -o ./artifacts
  - uses: actions/upload-artifact@v4
    with: { name: nuget-uno, path: artifacts/*.nupkg, if-no-files-found: error }
  - uses: actions/upload-artifact@v4
    with: { name: snupkg-uno, path: artifacts/*.snupkg, if-no-files-found: warn }
  ```
- [x] **Wire into publish:** added `pack-uno` to the `publish` job's `needs:` list
  (`needs: [ pack-core, pack-maui, pack-uno, test ]`). The publish step already downloads `nuget-*` /
  `snupkg-*` with `merge-multiple`, so the Uno package is picked up **automatically** — no change to the
  push step. Verified the `nuget-uno` / `snupkg-uno` artifact names match those `nuget-*` / `snupkg-*`
  download globs.

## 4. Versioning & metadata

- [x] `KumikoUI.Uno` follows the same tag-driven versioning (`v1.2.3` → `-p:Version=1.2.3`) — the
  `pack-uno` job consumes `needs.resolve-version.outputs.version` exactly like `pack-maui`/`pack-core` —
  and inherits Authors/License/SourceLink from [`Directory.Build.props`](../../Directory.Build.props).
- [x] Keep its `<Version>` in lockstep with the other packages, or bump together on release. *(The
  release `-p:Version` flag overrides the csproj `<Version>` on tagged builds.)*
- [x] `PackageId`/`Description`/`PackageTags` are set in the csproj
  ([02 §6](02-library-scaffold.md#6-nuget-metadata-mirror-the-sibling-projects)) — confirmed present
  (`PackageId=KumikoUI.Uno`).

## 5. Notes & caveats

- [x] **Docs-only changes skip CI:** this documentation PR is ignored by the `paths-ignore` (`**.md`,
  `docs/**`) filters in [`ci.yml`](../../.github/workflows/ci.yml) — intended; no build runs for docs.
- [ ] **MSIX / library assets:** if the library ever ships assets ([05 §5](05-fonts-and-assets.md#5-shipping-assets-inside-the-library-only-if-needed)),
  document the unpackaged-mode requirement for the WinAppSDK head. *(Not applicable yet — library ships no assets today.)*
- [x] **SkiaSharp native bits:** `SkiaSharp*` is pinned to `3.119.2` across all packages (via
  `<SkiaSharpVersion>` in the Uno csproj + explicit `SkiaSharp.Views.WinUI` 3.119.2) so a consuming app
  that references both `KumikoUI.Maui` and `KumikoUI.Uno` doesn't hit native version conflicts.
- [x] **Transitive AndroidX `content/` in the nupkg (observed in [Phase 05](05-fonts-and-assets.md)):**
  the multi-head pack pulls transitive AndroidX/AppCompat `content/` + `contentFiles/` resources (the
  `abc_*` drawables/layouts) into the package from the `net9.0-android` head — ~1,200 entries in the
  local `0.0.0-ci` dry-run. This is **acceptable** (cosmetic bloat from the Android head's resource
  pipeline; the `net9.0-android` `lib/` assembly is still correct) and is left as-is. If trimming is
  ever wanted it can be scoped with a `<None Remove>` / pack-exclude later.
- [x] **`Uno0001` not-implemented analyzer warnings:** the non-desktop heads emit `Uno0001` warnings for
  APIs not implemented on that head (e.g. `UIElement.IsHoldingEnabled`, `UIElement.CharacterReceived` on
  wasm/ios/android). These are **non-fatal** — `dotnet build`/`dotnet pack` exit 0; the input host
  already guards those paths per head (see [Phase 04](04-input-keyboard-focus.md)). The macOS dry-run
  also emits a benign Android Java-SDK validation warning, likewise non-fatal.

---

## ✅ Exit criteria

- [x] `ci.yml` builds and pack-dry-runs `KumikoUI.Uno` (and runs its tests) on PRs to `main`/`develop`
  via the new `build-uno` job. *(Workflow authored + YAML-validated locally; the actual hosted-runner
  run is pending the branch push — cannot execute GitHub Actions from this environment.)*
- [x] A version tag produces a `KumikoUI.Uno.<version>.nupkg` (+ `.snupkg`) and pushes it to NuGet.org
  alongside Core/SkiaSharp/Maui via the new `pack-uno` job, whose `nuget-uno`/`snupkg-uno` artifacts are
  consumed by the existing `publish` job's `nuget-*`/`snupkg-*` download globs. *(Wiring verified
  statically; live push pending a real tag.)* Local multi-head `dotnet pack` dry-run **succeeded** and
  produced `KumikoUI.Uno.0.0.0-ci.nupkg` with `lib/` for every locally-available head
  (`net9.0`, `-desktop`, `-browserwasm`, `-ios`, `-android`; the `-windows` head is added on the CI
  Windows runner).
- [x] The MAUI and Core/SkiaSharp jobs are **unchanged** (additive edits only — existing jobs in both
  workflows are byte-for-byte intact).

---

🎉 **End of the Uno port plan set.** Back to the [index](README.md).

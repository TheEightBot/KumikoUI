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

- [ ] New job, `needs: build-core`, `runs-on: windows-latest`.
- [ ] Set up .NET `9.x` + `10.x` (same as `build-maui`).
- [ ] **Restore Uno workloads** from the project (lighter than full `uno-check` in CI):
  ```yaml
  - name: Restore Uno workloads
    run: dotnet workload restore src/KumikoUI.Uno/KumikoUI.Uno.csproj
  ```
  (Alternative: `dotnet tool install -g uno.check && uno-check --ci --fix --non-interactive`.)
- [ ] Cache workloads with a key over `KumikoUI.Uno.csproj` (copy the `actions/cache@v4` block from
  `build-maui`, new key prefix `uno-workloads-win-`).
- [ ] Build + pack dry-run:
  ```yaml
  - run: dotnet build src/KumikoUI.Uno/KumikoUI.Uno.csproj --no-restore -c Release
  - run: dotnet pack  src/KumikoUI.Uno/KumikoUI.Uno.csproj --no-restore -c Release -p:Version=0.0.0-ci -o C:\ci-pack
  ```
- [ ] (Optional) add a `macos-latest` job that builds the **sample app's** Apple heads
  (`dotnet build samples/SampleApp.Uno -f net9.0-ios` / `-f net9.0-maccatalyst`) to catch app-level
  breakage the library pack can't.

## 3. Publish — add a `pack-uno` job

Mirror `pack-maui` in [`publish.yml`](../../.github/workflows/publish.yml).

- [ ] New job `pack-uno`, `needs: resolve-version`, `runs-on: windows-latest`; set up .NET 9 + 10;
  restore Uno workloads (with cache) as in §2.
- [ ] Pack with the resolved version and symbols (match the other pack jobs):
  ```yaml
  - name: Pack — KumikoUI.Uno
    run: >
      dotnet pack src/KumikoUI.Uno/KumikoUI.Uno.csproj
      --no-restore -c Release
      -p:Version=${{ env.VERSION }}
      --include-symbols -p:SymbolPackageFormat=snupkg
      -o ./artifacts
  - uses: actions/upload-artifact@v4
    with: { name: nuget-uno, path: artifacts/*.nupkg, if-no-files-found: error }
  - uses: actions/upload-artifact@v4
    with: { name: snupkg-uno, path: artifacts/*.snupkg, if-no-files-found: warn }
  ```
- [ ] **Wire into publish:** add `pack-uno` to the `publish` job's `needs:` list
  (`needs: [ pack-core, pack-maui, pack-uno, test ]`). The publish step already downloads `nuget-*` /
  `snupkg-*` with `merge-multiple`, so the Uno package is picked up **automatically** — no change to the
  push step.

## 4. Versioning & metadata

- [ ] `KumikoUI.Uno` follows the same tag-driven versioning (`v1.2.3` → `-p:Version=1.2.3`) and inherits
  Authors/License/SourceLink from [`Directory.Build.props`](../../Directory.Build.props).
- [ ] Keep its `<Version>` in lockstep with the other packages, or bump together on release.
- [ ] Confirm `PackageId`/`Description`/`PackageTags` are set ([02 §6](02-library-scaffold.md#6-nuget-metadata-mirror-the-sibling-projects)).

## 5. Notes & caveats

- [ ] **Docs-only changes skip CI:** this documentation PR is ignored by the `paths-ignore` (`**.md`,
  `docs/**`) filters in [`ci.yml`](../../.github/workflows/ci.yml) — intended; no build runs for docs.
- [ ] **MSIX / library assets:** if the library ever ships assets ([05 §5](05-fonts-and-assets.md#5-shipping-assets-inside-the-library-only-if-needed)),
  document the unpackaged-mode requirement for the WinAppSDK head.
- [ ] **SkiaSharp native bits:** keep `SkiaSharp*` at `3.119.2` across all packages so a consuming app
  that references both `KumikoUI.Maui` and `KumikoUI.Uno` (unlikely, but possible) doesn't hit native
  version conflicts.

---

## ✅ Exit criteria

- [ ] `ci.yml` builds and pack-dry-runs `KumikoUI.Uno` on PRs to `main`/`develop`.
- [ ] A version tag produces a `KumikoUI.Uno.<version>.nupkg` (+ `.snupkg`) and pushes it to NuGet.org
  alongside Core/SkiaSharp/Maui.
- [ ] The MAUI and Core/SkiaSharp jobs are unchanged and still green.

---

🎉 **End of the Uno port plan set.** Back to the [index](README.md).

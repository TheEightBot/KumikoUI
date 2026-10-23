# 07 — Tests

Goal: prove the port didn't regress the engine, and add the lightest meaningful coverage for the new
Uno host.

---

## 1. Core tests — unchanged, must stay green

Because `KumikoUI.Core` and `KumikoUI.SkiaSharp` are **byte-for-byte unchanged**, the existing
[`tests/KumikoUI.Core.Tests`](../../tests/KumikoUI.Core.Tests) (xUnit) still fully covers layout, input
dispatch, models, selection, filtering, scrolling, etc.

- [x] Run them and confirm green — this is the regression gate for "Core wasn't touched":
  ```bash
  dotnet test tests/KumikoUI.Core.Tests/KumikoUI.Core.Tests.csproj -c Release
  ```
  > **Result (Phase 07):** **GREEN — Failed: 0, Passed: 353, Skipped: 0, Total: 353.**
  > Run unchanged against the existing suite, proving `KumikoUI.Core` is byte-for-byte intact and
  > the engine still passes. Executed as `-c Debug` with `DOTNET_ROLL_FORWARD=LatestMajor` (this box
  > has SDK 10.x but no `Microsoft.NETCore.App 9.0.x` runtime, so the net9.0 test host must roll
  > forward to the 10.x runtime). `Debug` vs `Release` does not affect this pure-logic suite.
- [x] Do **not** add platform code to Core to make it "more testable" — that would violate the
  separation of concerns. Host-specific logic belongs in `KumikoUI.Uno`. *(Core untouched; no new
  Core types or test edits — only the new `tests/KumikoUI.Uno.Tests` project was added.)*

## 2. Make the host's pure logic testable

The only non-trivial logic in the host is the input mapping. Factor it into **pure static methods** so
it can be unit-tested without a live UI:

- [x] Put `VirtualKey → GridKey` and `VirtualKeyModifiers → InputModifiers` in a small static class
  (e.g. `KumikoUI.Uno.Input.InputMapping`) with no control state. *(Done in Phase 04:
  `src/KumikoUI.Uno/Input/InputMapping.cs` — `ToGridKey`, `ToInputModifiers` are pure enum→enum
  with zero `DataGridView`/element dependency.)*
- [x] These reference WinUI types, so the tests live in an **Uno runtime test project** (below), not in
  `KumikoUI.Core.Tests` (which must stay WinUI-free). Assert the full key table from
  [04 §3](04-input-keyboard-focus.md#3-keyboard--focus).
  > **Result (Phase 07):** `tests/KumikoUI.Uno.Tests/InputMappingTests.cs` asserts the **entire**
  > documented table: arrows · Home/End · PageUp/PageDown · Tab · Enter · Escape · Space · Delete ·
  > **Back→Backspace** · F2 · A/C/V/X/Z; modifiers Shift/Control/**Menu→Alt**/**Windows→Meta** plus
  > combinations. A whole-enum sweep also asserts every *other* `VirtualKey` collapses to
  > `GridKey.None`, so an accidental added/dropped mapping fails the suite.
  > `GetLiveModifiers()` is **excluded** — it reads thread keyboard state via `InputKeyboardSource`
  > and needs a live Uno runtime, which is out of scope for a pure unit test.

## 3. Uno runtime / UI tests (optional but recommended)

For host behavior that only exists at runtime (focus, pointer routing, redraw-on-property-change),
add an Uno test project:

- [x] **Added `tests/KumikoUI.Uno.Tests`** — an **Uno single-project test library** (same
  `Uno.Sdk/6.5.31` pin + `SkiaRenderer` feature as `src/KumikoUI.Uno`, but scoped to a **single**
  `net9.0-desktop` head) with the xUnit runner + `Microsoft.NET.Test.Sdk`, referencing
  `KumikoUI.Uno`. This shape is what makes the WinUI/Uno enums (`Windows.System.VirtualKey` /
  `VirtualKeyModifiers`) resolve at compile time — a plain `Microsoft.NET.Sdk` xUnit project cannot
  see those types. The `unoapp-uitest` (Playwright/Selenium) template was **not** used: it drives a
  *running* app, which is heavier than (and orthogonal to) the pure enum-mapping coverage this phase
  needs, and would not run headlessly here.
  - **Tests RAN headlessly and PASSED:** `dotnet test tests/KumikoUI.Uno.Tests -f net9.0-desktop`
    (with `DOTNET_ROLL_FORWARD=LatestMajor`) → **Failed: 0, Passed: 41, Skipped: 0, Total: 41.**
    The methods under test are pure enum switches/flag-folds, so they execute without any Uno
    runtime/`UIElement` initialization — only the enum *types* need to resolve, which the desktop
    head provides.
- [x] Added to `KumikoUI.sln` (via `dotnet sln add … --solution-folder tests`; the sln still parses
  and the project is nested under the `tests` folder alongside `KumikoUI.Core.Tests`).
- [ ] **Deferred (runtime UI assertions):** grid-renders-on-load, `ItemsSource`/`Columns` repaint,
  keyboard-navigation-moves-selection, and typed-character-edits-cell are **live-UI** behaviors that
  need a pumped Uno dispatcher + a rendered `SKXamlCanvas`. Those are **not** covered by this pure
  unit suite; they remain the job of the per-head **smoke matrix** (§4) and CI device/desktop runs.
  The grid's *logic* for these paths is already covered by `KumikoUI.Core.Tests` (selection,
  input dispatch, layout, redraw triggers); only the host wiring is left to smoke.
- [x] Keep these tests on the **Desktop (Skia)** head for CI speed; treat other heads as manual smoke.
  *(The project targets `net9.0-desktop` only.)*

## 4. Manual smoke matrix

Track per-head before declaring a head "supported".

**Legend:** ☑ verified · ◐ partially verified (see note) · ☐ pending (not yet run) · n/a not applicable.

**Status (as of Phase 07):** only **Desktop (Skia)** is buildable/runnable on the dev box (no
android/wasm/ios workloads, and the Windows head is Windows-only). The Desktop column reflects the
**Phase 06 headless run-to-first-render** smoke (app launched, the basic grid rendered with live data,
zero exceptions) — that confirms *grid-renders-on-load* only. Interactive behaviors (inertial scroll,
mouse/touch selection, keyboard nav, editing, resize/reorder, sort/filter, HiDPI crispness) were **not**
hands-on exercised in the headless session and are **pending** an interactive desktop run / CI. All other
heads are **pending CI + device testing** (Phase 08).

| Feature | Desktop | WASM | Windows | Android | iOS | Mac Catalyst |
|---|:--:|:--:|:--:|:--:|:--:|:--:|
| Grid renders, scrolls (wheel + inertial) | ◐ ¹ | ☐ | ☐ | ☐ | ☐ | ☐ |
| Selection (mouse/touch) | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| Keyboard navigation | ☐ | ☐ | ☐ | n/a | n/a | ☐ |
| Cell editing (typed / soft keyboard) | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| Column resize / reorder, row drag | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| Sorting / filtering / grouping | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| CJK + icon fonts render | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| Crisp at 150% / 200% scale | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |

> ¹ **Desktop, "Grid renders":** the *render-on-load* half is verified (Phase 06 run-to-first-render,
> zero exceptions). The *scrolls (wheel + inertial)* half is **pending** an interactive run — not
> scriptable in the headless session. Marked ◐ rather than ☑ for that reason.

---

## ✅ Exit criteria

- [x] `KumikoUI.Core.Tests` passes unchanged (no Core regressions). *(353/353 green; Core untouched.)*
- [x] Input-mapping unit tests pass for the full key/modifier table. *(`tests/KumikoUI.Uno.Tests`,
  41/41 green on `net9.0-desktop` — full `ToGridKey`/`ToInputModifiers` table + whole-enum sweep.)*
- [ ] The smoke matrix is green for the heads being released. *(Pending: only Desktop render-on-load
  is verified so far; interactive paths + all non-desktop heads await CI / device testing in Phase 08.)*

➡️ Next: [08 — CI/CD & packaging](08-ci-cd-packaging.md)

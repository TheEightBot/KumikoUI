# 07 — Tests

Goal: prove the port didn't regress the engine, and add the lightest meaningful coverage for the new
Uno host.

---

## 1. Core tests — unchanged, must stay green

Because `KumikoUI.Core` and `KumikoUI.SkiaSharp` are **byte-for-byte unchanged**, the existing
[`tests/KumikoUI.Core.Tests`](../../tests/KumikoUI.Core.Tests) (xUnit) still fully covers layout, input
dispatch, models, selection, filtering, scrolling, etc.

- [ ] Run them and confirm green — this is the regression gate for "Core wasn't touched":
  ```bash
  dotnet test tests/KumikoUI.Core.Tests/KumikoUI.Core.Tests.csproj -c Release
  ```
- [ ] Do **not** add platform code to Core to make it "more testable" — that would violate the
  separation of concerns. Host-specific logic belongs in `KumikoUI.Uno`.

## 2. Make the host's pure logic testable

The only non-trivial logic in the host is the input mapping. Factor it into **pure static methods** so
it can be unit-tested without a live UI:

- [ ] Put `VirtualKey → GridKey` and `VirtualKeyModifiers → InputModifiers` in a small static class
  (e.g. `KumikoUI.Uno.Input.InputMapping`) with no control state.
- [ ] These reference WinUI types, so the tests live in an **Uno runtime test project** (below), not in
  `KumikoUI.Core.Tests` (which must stay WinUI-free). Assert the full key table from
  [04 §3](04-input-keyboard-focus.md#3-keyboard--focus).

## 3. Uno runtime / UI tests (optional but recommended)

For host behavior that only exists at runtime (focus, pointer routing, redraw-on-property-change),
add an Uno test project:

- [ ] Either re-scaffold the sample with test projects:
  ```bash
  # adds wired unit + UI test projects alongside the app
  dotnet new unoapp -o samples/SampleApp.Uno … -tests ui unit
  ```
  or add a standalone UI-test project:
  ```bash
  dotnet new unoapp-uitest -o tests/KumikoUI.Uno.UITests
  ```
- [ ] Add it to `KumikoUI.sln`; cover: grid renders on load; changing `ItemsSource`/`Columns` repaints;
  keyboard navigation moves selection; a typed character edits the focused cell.
- [ ] Keep these tests on the **Desktop (Skia)** head for CI speed; treat other heads as manual smoke.

## 4. Manual smoke matrix

Track per-head before declaring a head "supported":

| Feature | Desktop | WASM | Windows | Android | iOS | Mac Catalyst |
|---|:--:|:--:|:--:|:--:|:--:|:--:|
| Grid renders, scrolls (wheel + inertial) | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| Selection (mouse/touch) | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| Keyboard navigation | ☐ | ☐ | ☐ | n/a | n/a | ☐ |
| Cell editing (typed / soft keyboard) | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| Column resize / reorder, row drag | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| Sorting / filtering / grouping | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| CJK + icon fonts render | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| Crisp at 150% / 200% scale | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |

---

## ✅ Exit criteria

- [ ] `KumikoUI.Core.Tests` passes unchanged (no Core regressions).
- [ ] Input-mapping unit tests pass for the full key/modifier table.
- [ ] The smoke matrix is green for the heads being released.

➡️ Next: [08 — CI/CD & packaging](08-ci-cd-packaging.md)

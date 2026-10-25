# KumikoUI Roadmap to 1.0

**Status:** planned 2026-10-22 from the [release-readiness evaluation](release-readiness/README.md) and two rounds of maintainer decisions. Work items are in the [issue plan](release-readiness/issue-plan.md) (105 issues) and, once approved, on GitHub under the milestones below.

## 1. Where we are

KumikoUI 0.1.10 is a feature-rich, well-layered canvas-drawn DataGrid whose engine assumes happy-path data, whose edit pipeline can write to the wrong item, whose public API still contains placeholders, whose tests cover models but not the control, and whose packages target a runtime that leaves support on 2026-11-10. It is not ready for a 1.0 tag. The plan below takes it there in three steps: a stabilisation release that freezes the API, a preview line for device verification, then 1.0.0.

## 2. Decisions (record)

Settled with the maintainer on 2026-10-22. Each is the contract the issues implement.

| # | Decision |
|---|---|
| Release path | No 1.0.0 from the current state. `0.2.0` (stabilisation + API freeze) → `1.0.0-preview.N` (device verification) → `1.0.0`. Milestone-driven; no public date until blockers are closed. Capacity: solo maintainer plus AI agents. |
| Package scope | Core, SkiaSharp and MAUI reach 1.0.0 together. KumikoUI.Uno is backed as a first-class target and ships as 1.0.0 only if every head passes the device matrix (H3); otherwise it ships `1.0.0-preview` and joins at 1.0.x. Versions stay in lockstep. |
| Runtime | `net10.0` everywhere; `net9.0` dropped. |
| SkiaSharp | 4.153.x before 1.0 (compile-breaking text API and native ABI change land once, before the freeze). |
| Watermark | Android API 24, iOS 17.0, Mac Catalyst 17.0, Windows 10.0.19041 on both hosts; re-baselined at each .NET major. Uno.Sdk 6.7.x via `global.json`, central package management, pinned workload sets. |
| API freeze | All breaking clean-up happens in 0.2.0: `KumikoDataGrid` control name, collision-free type names, `RowTappedEventArgs`, runtime state only through `KumikoSource`, item-based read-only selection, structured `ViewChanged`, dead members removed, grid-level `IsReadOnly`/`AllowSorting`/`AllowFiltering` implemented, one `UseKumikoUI()` on MAUI and no hosting call on Uno, `ContentView`/`Control` base classes, `IDisposable` source. Namespaces stay rooted at `KumikoUI`. |
| Naming | Product "KumikoUI DataGrid". `DataGrid*` for model/host types, `Grid*` for primitives and engine, `Drawn*` for editors; no other `Kumiko*` types. File names match type names. |
| Governance tooling | PublicApiAnalyzers now; package validation from 1.0.0; CS1591 as error after docs; `.editorconfig` + `dotnet format` in CI. |
| Native AOT | A 1.0 goal: source-generated accessors (recommended), public delegate accessors, annotated reflection fallback; `IsAotCompatible` everywhere with zero IL warnings; AOT publish jobs in CI. PR #1 closed as superseded. |
| Text input | The hidden-proxy design is replaced by a Core composition-aware text-input contract with native adapters (UITextInput, InputConnection, CoreTextEditContext, Uno composition events). 1.0 scope: IME everywhere, undo/redo, selection gestures, clipboard, culture-aware entry, scrollable popups with typeahead. Best-in-class is the bar. |
| Accessibility | 1.0 is "keyboard accessible with an honest statement" plus quick wins (proxies hidden, grid-level peers/semantics, announcements). The per-cell virtual accessibility tree is a 1.x milestone. |
| Localisation | `GridStrings` provider and culture-aware formatting in 1.0; RTL mirroring in 1.x. |
| Features in 1.0 | Column reorder documented, validation UI, clipboard, `ScrollTo*`, MVVM selection, bindable theme with system follow, `ContextRequested`, multi-aggregate summaries, sampled Auto sizing. Deferred to 1.x as spec issues: row details, load-more, export, variable row height, column chooser, paging, DataTable sources, cell merging, stacked headers, cell selection unit. |
| Threading & responsiveness | Mutations are UI-thread-only (captured context, debug assert, clear exception). Core computes sort/filter/group/summaries on a background worker from an immutable snapshot and swaps a `ViewSnapshot` on the UI thread; small changes stay incremental; sources under ~1,000 rows rebuild synchronously. Text measurement cached, images decoded asynchronously. |
| Rendering | `RenderMode = Auto | Software | Gpu`: `SKGLView` (MAUI), `SKCanvasElement` (Uno Skia heads), `SKSwapChainPanel` (WinAppSDK); platform frame callbacks replace 16 ms timers; editor-only redraw for caret blink. Uno in 0.2.0, MAUI in 1.0.0-preview. |
| Identity | Selection and the edit session track items, survive sort/filter/group; a mid-edit view change commits to the item if still present, else cancels. |
| Semantics | Culture-sensitive, case-insensitive comparison by default through a safe comparer that never throws; groups ordered by raw key; stable sort. Editable types: all numerics, decimal, enum, Guid, DateTime, DateOnly, TimeOnly, DateTimeOffset, TimeSpan, bool, string, nullable forms; empty → null for nullable, validation error otherwise; conversion failure keeps the editor open (WPF/WinUI DataGrid conventions). |
| Gestures | Long-press starts full-row drag on touch, drag on mouse, handle column always; outside tap only dismisses popups; Boolean single-tap toggle honouring `EditTriggers`, Space toggles; right-click/long-press raise `ContextRequested` and never sort, edit or drag. Interaction quality must feel premium (animations, hover/pressed states, cursor feedback, momentum tuning, haptics). |
| Frozen rows | "First N rows of the current view" (Excel semantics); item pinning is 1.x. |
| Process | `main` + tags + rulesets (`develop` retired); MinVer; NuGet Trusted Publishing with a required reviewer; Dependabot, CodeQL, least-privilege tokens, SHA-pinned actions; Keep a Changelog; CONTRIBUTING, SECURITY (GitHub private vulnerability reporting, no email), PR template, YAML issue forms, CODEOWNERS; Discussions on; font licence notices; the showcase GIF stays, re-encoded, with an MP4 release asset; no history rewrite. |
| Docs | Host-neutral GitHub README with compiled snippets and only measured claims; a plain-Markdown README per package for nuget.org; `docs/uno` becomes a user guide; internal docs regenerated from code; DocFX site in 1.x. |
| Performance | BenchmarkDotNet project and frame-time overlay; the promise is an allocation-free steady-state render loop and published per-device numbers, not "60 fps". |
| Tracking | One issue per work item with finding IDs, acceptance criteria, dependencies and a suggested model; labels `area:*`, `kind:*`, `priority:*`, `model:*`; milestones below; a pinned release checklist. Planning used Claude Fable; implementation issues are labelled `model:opus` or `model:sonnet` and the label is re-evaluated when each issue is picked up. |

## 3. Milestones

| Milestone | Goal | Exit criteria | Issues |
|---|---|---|---|
| **0.2.0 – Stabilisation** | Every blocker and high closed, the public API frozen, the platform baseline current, the text-input and identity architectures in place, docs truthful, process in place. | All `priority:blocker` and `priority:high` issues closed; `PublicAPI.Unshipped.txt` reviewed; CI green on .NET 10 with the macOS/Windows/Linux matrix; nuget.org README previews approved; CHANGELOG section written. | 81 |
| **1.0.0-preview** | Prove the release on devices and publish numbers. | Uno device matrix green or decided; MAUI GPU mode measured; AOT publish jobs green; accessibility quick wins verified with screen readers; benchmark numbers in the README; samples cover every documented feature. | 9 |
| **1.0.0** | Ship. | Release checklist complete; API sign-off; package validation baseline set; announcement. | 3 |
| **1.x – Fast follow-ons** | The deferred features, each with a reviewed design ready to implement. | Spec issues reviewed and split into implementation issues. | 12 |

## 4. Epics and issues

Keys refer to the [issue plan](release-readiness/issue-plan.md), whose index maps each key to its GitHub issue (#10–#114, created 2026-10-22; pinned tracking issue [#114](https://github.com/TheEightBot/KumikoUI/issues/114)).

| Epic | Issues | Milestone |
|---|---|---|
| A — Platform & toolchain | A1 net10 · A2 SkiaSharp 4 · A3 CPM/global.json/workloads · A4 watermark · A5 MinVer | 0.2.0 |
| B — Data engine correctness | B1 safe comparer · B2 per-item accessors/null rows · B3 summaries · B4 incremental updates & INPC · B5 style mutation · B6 column sizing · B7 PR #9 footer · B8 pane clipping · B9 source lifecycle · B10 group state | 0.2.0 |
| C — Editing pipeline | C1 item-tracked edits · C2 IValueEditor · C3 conversion/validation UI · C4 commit results & ordering · C5 action buttons · C6 editor fonts & popup placement · C7 ProgressBar/tri-state | 0.2.0 |
| D — Text input & editors | D1 Core text-input contract · D2 iOS/Catalyst · D3 Android · D4 Windows · D5 Uno · D6 clipboard · D7 ComboBox/Picker · D8 DatePicker/NumericUpDown · D9 filter popup | 0.2.0 |
| E — Identity & events | E1 ViewChanged · E2 item-based selection · E3 column state | 0.2.0 |
| F — API freeze & naming | F1 naming · F2 dead surface · F3 hosting · F4 base classes · F5 grid-level flags · F6 governance tooling | 0.2.0 |
| G — Host surface & MVVM | G1 selection bindables · G2 ScrollTo · G3 bindable theme · G4 option bindables · G5 lifecycle/leaks · G6 pointer & wheel · G7 Uno long-press · G8 images (preview) · G9 Uno action buttons | 0.2.0 / preview |
| H — Uno host rework | H1 SKCanvasElement surface · H2 package hygiene · H3 device matrix (preview) | 0.2.0 / preview |
| I — Rendering performance | I1 allocation-free loop · I2 text cache & wrap · I3 Skia resource cache · I4 async view pipeline · I5 MAUI GPU (preview) · I6 benchmarks (preview) · I7 inertia edge cases | 0.2.0 / preview |
| J — Fonts | J1 unknown family · J2 glyph fallback & styles · J3 KumikoFonts on MAUI, licences | 0.2.0 |
| K — Native AOT | K1 accessor tiers & annotations · K2 AOT CI (preview) | 0.2.0 / preview |
| L — Accessibility | L1 MAUI quick wins · L2 Uno quick wins (preview) · L3 virtual tree spec (1.x) | preview / 1.x |
| M — Localisation | M1 GridStrings · M2 RTL spec (1.x) | 0.2.0 / 1.x |
| N — Tests | N1 recording context & renderer · N2 source/layout/selection · N3 editing/input/components · N4 SkiaSharp golden · N5 host tests · N6 coverage · N7 compiled doc snippets | 0.2.0 |
| O — CI/CD & release | O1 release gate · O2 CI matrix · O3 supply chain · O4 trusted publishing | 0.2.0 |
| P — Governance & docs | P1 community files · P2 CHANGELOG/versioning · P3 assets · P4 README · P5 NuGet READMEs · P6 internal docs · P7 samples (preview) | 0.2.0 / preview |
| Q — Features & interaction | Q1 column reorder · Q2 row drag · Q3 keyboard completeness · Q4 ContextRequested · Q5 interaction polish (preview) | 0.2.0 / preview |
| R — 1.0.0 | R1 checklist · R2 API sign-off · R3 final docs | 1.0.0 |
| X — 1.x specs | X1 row details · X2 load-more · X3 export · X4 variable row height · X5 column chooser & checkbox column · X6 paging & DataTable · X7 cell selection · X8 pinning/stacked headers/merging · X9 DocFX & cookbook · X10 HarfBuzz | 1.x |

## 5. Sequencing

Dependencies are recorded on every issue. The waves below are the critical path through 0.2.0; anything not listed can run in parallel as capacity allows.

1. **Unblock (first).** B7 then merge PR #9 so all work starts from `main`. A1 → A2 → A3, A4, A5. O1, O3, P1, P2 can start immediately (settings and files). N1 (recording drawing context) early because every later Core change needs it. F1 (naming) before the big refactors to avoid churn.
2. **Foundations.** K1 (accessor abstraction) → B2, I1, I4. E1 (ViewChanged) → E2, E3, B4, C1, C3, G2, G4. F2–F6 (API clean-up, base classes, governance tooling). B1, B5, B9, I3, J1, M1.
3. **Editing and input.** D1 (text-input contract) → C3 → C2 → C4 → C5; C1 after E2. Q3 (keyboard) → D7; D8, D9, C6, C7. G6 (pointer semantics) → G7, Q2, Q4. B3, B6 (after I2), B8, B10, I2, I7, J2.
4. **Hosts.** D2, D3, D4, D5 (adapters) → D6 (clipboard) → L1/L2 (preview). F4 → G1, G3, G4, G5, G9, H1, H2, Q1. N2–N7, O2, O4, P4 → P5, P6.
5. **Preview.** G8, H3, I5, I6, K2, L1, L2, P7, Q5.
6. **1.0.0.** R1, R2, R3.

## 6. Model policy for implementation

Planning (this document and the evaluation) used Claude Fable. Implementation issues carry a `model:` label chosen by these rules, and the person picking up an issue re-evaluates it:

- **`model:opus`** — architectural or cross-cutting changes, concurrency, rendering/input semantics, platform-native adapters, performance work that must be measured, anything where the acceptance criteria leave design judgement to the implementer. 43 issues.
- **`model:sonnet`** — well-specified, contained changes: configuration, CI, docs, tests with explicit expectations, single-module fixes whose acceptance criteria are mechanical. 62 issues.
- Verification runs (device matrices, screen-reader walkthroughs, benchmark baselines) are opus-labelled because they require judgement about what they observe.

## 7. Definition of done for 1.0.0

- Every `priority:blocker` and `priority:high` issue in 0.2.0 and 1.0.0-preview closed; no open `kind:bug` above `priority:low`.
- Public API reviewed and frozen (`PublicAPI.Shipped.txt`), package validation baseline enabled, zero CS1591, zero IL warnings.
- CI green on .NET 10 across Linux/Windows/macOS with samples built for every head; AOT publish jobs green; coverage threshold met.
- Uno device matrix recorded with the GA/preview decision; MAUI interaction and accessibility checklists signed off per platform.
- README, per-package NuGet READMEs, CHANGELOG, platform matrix and benchmark numbers current; nuget.org previews checked.
- Release published through the gated, OIDC-based pipeline from a protected `main`.

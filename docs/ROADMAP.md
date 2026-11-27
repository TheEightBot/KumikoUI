# KumikoUI roadmap

**Status:** planned 2026-11-27, after the `1.0.0` release. The plan that led to 1.0.0 is archived in [release/roadmap-to-1.0.md](release/roadmap-to-1.0.md).

## 1. Where we are

KumikoUI 1.0.0 (2026-11-25) is one control, a canvas-drawn datagrid, on two frameworks: .NET MAUI and the Uno Platform. Its public API is frozen and follows [VERSIONING.md](VERSIONING.md).

From here KumikoUI grows into a control suite: a tree grid, charts, a scheduler, a pivot grid and a Gantt chart, on six frameworks. Every control is drawn by the same Core, SkiaSharp and host layers the datagrid uses. What runs where is recorded in the [support matrix](SUPPORT-MATRIX.md).

Three tracks run from 1.0.0:

| Track | What | Where |
|---|---|---|
| Patch | Fixes and open verification items for the shipped datagrid | [1.0.1 milestone](https://github.com/TheEightBot/KumikoUI/milestone/5) |
| Grid features | Datagrid feature gaps, one issue each, in order of expected value | [1.1 – Grid feature gaps](https://github.com/TheEightBot/KumikoUI/milestone/6) |
| Suite | New controls, new framework hosts, and the website | The eleven milestones below |

## 2. Decisions (record)

Settled with the maintainer on 2026-11-27. The reasoning is in the [research notes](research/control-suite-expansion.md); the detail of each milestone is in the [milestone plan](suite/milestone-plan.md).

| Area | Decision |
|---|---|
| Controls | Tree grid, charts, scheduler, pivot grid and Gantt. Editors are deferred: they need separate work on every platform. |
| Frameworks | Six, for every control including the datagrid: .NET MAUI, Uno Platform, WPF, WinUI, Avalonia and Blazor WebAssembly. Blazor server rendering is not supported. |
| Considered and dropped | A docking control, because it shares almost nothing with the drawn engine and depends on host windowing. Windows Forms, because it is not a target for this suite. |
| Platform coverage | A control may ship on a subset of frameworks. Every gap is recorded in the support matrix with a reason. |
| Rendering | Every control is drawn on the Core, SkiaSharp and host split. Native pieces are used only where drawing cannot do the job. |
| Foundations first | A foundations milestone precedes every new control and host, so a second control is not a copy of the grid. |
| Grid features and foundations | Foundations runs before the bulk of the open grid feature issues, so they land on the split renderer and input code. Grid issues that do not touch the renderer or the input controller proceed in parallel. |
| Tree grid | A separate `KumikoTreeGrid` control over shared engine code, not a mode of the datagrid. |
| First-release depth | Scheduler: a documented RFC 5545 recurrence subset with a conformance suite. Pivot grid: in-memory data. Gantt: display only, with the scheduling engine in its complete tier. |
| Editing | Scheduler and Gantt raise an edit-requested event and offer an optional quick editor built from the existing drawn components. |
| Shared engines | The scale and axis component, the date engine and the aggregation engine are built inside the first control that needs them, with no dependency on that control's types, so later controls take them unchanged. |
| Accessibility | A control-neutral semantic tree, designed once. A control does not leave experimental without it. |
| Versioning | One lockstep version for every package. New controls ship in stable 1.x minors marked experimental until their complete tier and the semantic tree are done. |
| Packaging | Each control, including the datagrid, ends up in its own package. New control families get their own package from the start (`KumikoUI.Charts`, `KumikoUI.Scheduling`, `KumikoUI.Gantt`). The split of the existing packages happens in 1.1 or 2.0, decided by a spike. |
| Compatibility | The frozen 1.0.0 API is not broken: additions only, and the drawing abstraction grows through a companion interface. |
| Repository | One repository and one solution. KumikoUI stays the suite name. |
| Docs | The support matrix is updated in the pull request that changes a status. One website for the suite is its own milestone. No third-party control vendor or control library is named in issues, docs or milestones. |
| Tracking | GitHub milestones without due dates, ordered by dependency, each with a tracking issue. Issues are written in detail when a milestone's work starts, not months ahead. |

## 3. Milestones

| Milestone | Goal | Depends on | Tracking issue |
|---|---|---|---|
| [Suite – Foundations](https://github.com/TheEightBot/KumikoUI/milestone/7) | A control-neutral kernel and a per-framework host kit | 1.0.1 package validation baseline | [#433](https://github.com/TheEightBot/KumikoUI/issues/433) |
| [Suite – Tree grid](https://github.com/TheEightBot/KumikoUI/milestone/8) | `KumikoTreeGrid` on MAUI and Uno | Foundations | [#434](https://github.com/TheEightBot/KumikoUI/issues/434) |
| [Suite – Charts](https://github.com/TheEightBot/KumikoUI/milestone/9) | Chart control with the shared scale and axis component | Foundations | [#435](https://github.com/TheEightBot/KumikoUI/issues/435) |
| [Suite – Scheduler](https://github.com/TheEightBot/KumikoUI/milestone/10) | Scheduler control with the shared date engine | Foundations; scale component from Charts | [#436](https://github.com/TheEightBot/KumikoUI/issues/436) |
| [Suite – Pivot grid](https://github.com/TheEightBot/KumikoUI/milestone/11) | Pivot control with the shared aggregation engine | Foundations; Tree grid | [#437](https://github.com/TheEightBot/KumikoUI/issues/437) |
| [Suite – Gantt](https://github.com/TheEightBot/KumikoUI/milestone/12) | Gantt control | Tree grid; time scale from Charts; date engine from Scheduler | [#438](https://github.com/TheEightBot/KumikoUI/issues/438) |
| [Host – WPF](https://github.com/TheEightBot/KumikoUI/milestone/13) | The datagrid, then every shipped control, on WPF | Foundations | [#439](https://github.com/TheEightBot/KumikoUI/issues/439) |
| [Host – WinUI](https://github.com/TheEightBot/KumikoUI/milestone/14) | The same on native WinUI | Foundations | [#440](https://github.com/TheEightBot/KumikoUI/issues/440) |
| [Host – Avalonia](https://github.com/TheEightBot/KumikoUI/milestone/15) | The same on Avalonia | Foundations; SkiaSharp version spike | [#441](https://github.com/TheEightBot/KumikoUI/issues/441) |
| [Host – Blazor WebAssembly](https://github.com/TheEightBot/KumikoUI/milestone/16) | The same on Blazor WebAssembly | Foundations | [#442](https://github.com/TheEightBot/KumikoUI/issues/442) |
| [Suite – Docs and website](https://github.com/TheEightBot/KumikoUI/milestone/17) | One website: landing page, per-control docs, API reference, support matrix, live demos | Starts with Tree grid | [#443](https://github.com/TheEightBot/KumikoUI/issues/443) |

No milestone has a date. Foundations and Tree grid have their issues open now; the others have a tracking issue that lists the planned work.

## 4. Order

```
Foundations ─┬─► Tree grid ─► Charts ─► Scheduler ─► Pivot grid ─► Gantt
             ├─► WPF ─► WinUI ─► Avalonia ─► Blazor WebAssembly          (parallel track)
             └─► Docs and website (starts with Tree grid, grows with each control and host)
```

- The control track builds each control on .NET MAUI and the Uno Platform first.
- The host track brings up the datagrid on a new framework, then every control that has shipped by then. A control that ships after a host exists includes that host in its own exit criteria.
- WPF goes first on the host track because its drawing surface is the simplest, so it validates the host kit fastest.

## 5. What is done for every milestone

- XML docs on every public member; public API recorded and reviewed.
- A sample page for every documented feature, package README updates and a CHANGELOG entry.
- The support matrix updated in the pull request that changes a status.
- Numbers published from benchmarks, not promised.
- A non-drag alternative for every drag interaction, and a text alternative where assistive technology has no matching role.
- For a new host: the same verification pass the .NET MAUI and Uno hosts went through for 1.0.

## 6. Risks

| Risk | Where it is handled |
|---|---|
| The foundations refactor and the open grid feature issues touch the same renderer and input code | Sequencing decision in section 2; the first foundations issues re-read the current code |
| A framework may bring a SkiaSharp version that conflicts with the pinned one | Foundations spike, before the Avalonia host is designed |
| Moved types may not pass package validation behind type forwards | Foundations spike; decides whether the package split is 1.1 or 2.0 |
| Recurrence and time-zone correctness | Conformance suite from the first scheduler issue |
| Chart performance in WebAssembly | Benchmark issue in the charts milestone |
| Each new host repeats the device verification the 1.0 cycle needed | Verification issue in every host milestone |

## 7. Still deferred

- An editors pack.
- Right-to-left layout ([#80](https://github.com/TheEightBot/KumikoUI/issues/80)).
- The 1.x datagrid specs that no suite milestone needs first; each one that a suite milestone does need (stacked headers, cell-range selection, export, the accessibility tree) is pulled into that milestone.

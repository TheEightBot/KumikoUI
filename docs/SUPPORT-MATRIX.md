# KumikoUI support matrix

Which control runs on which framework. This file is updated in the same pull request that changes a status.

**Last updated:** 2026-11-27, for release `1.0.0`.

## Status values

| Status | Meaning |
|---|---|
| **Shipped** | Released and covered by the compatibility rules in [VERSIONING.md](VERSIONING.md). |
| **Experimental** | Released, but the API may change in a minor release. |
| **In progress** | Being built; not in a release yet. |
| **Planned** | On the [roadmap](ROADMAP.md); work has not started. |
| **N/A** | Does not apply to this framework. |

A cell that is not Shipped links to the milestone that delivers it.

## Controls by framework

| Control | .NET MAUI | Uno Platform | WPF | WinUI | Avalonia | Blazor WebAssembly |
|---|---|---|---|---|---|---|
| Datagrid | Shipped | Shipped | [Planned][wpf] | [Planned][winui] | [Planned][avalonia] | [Planned][blazor] |
| Tree grid | [Planned][treegrid] | [Planned][treegrid] | [Planned][wpf] | [Planned][winui] | [Planned][avalonia] | [Planned][blazor] |
| Charts | [Planned][charts] | [Planned][charts] [1] | Planned | Planned | Planned | Planned [2] |
| Scheduler | [Planned][scheduler] [3] | [Planned][scheduler] [3] | Planned | Planned | Planned [3] | Planned [4] |
| Pivot grid | [Planned][pivot] [5] | [Planned][pivot] [5] | Planned | Planned | Planned | Planned [6] |
| Gantt | [Planned][gantt] [7] | [Planned][gantt] [7] | Planned | Planned | Planned [7] | Planned |

A control reaches WPF, WinUI, Avalonia and Blazor WebAssembly through that framework's host milestone if the control has shipped by then, and through its own milestone otherwise.

Notes:

1. On Uno's Windows App SDK target the software canvas is used.
2. No chart role exists for assistive technology on any framework; a text and table alternative is provided everywhere.
3. Phones get day and agenda-style views, not the full week.
4. Requires time-zone data that the app must not trim.
5. The field list needs a separate phone layout.
6. Large pivots compute on the UI thread unless WebAssembly threading is enabled.
7. Phones show one pane at a time.

## Scope

- **Blazor means WebAssembly.** Server rendering is not supported. A Blazor Hybrid app uses the native host's control.
- **The datagrid's platforms, minimum OS versions and verification results** for .NET MAUI and the Uno Platform are in the README under [Supported platforms](../README.md#supported-platforms). Open verification items are in the [1.0.1 milestone](https://github.com/TheEightBot/KumikoUI/milestone/5).
- **Uno Platform** covers its desktop, WebAssembly, iOS, Android and Windows App SDK targets. **Avalonia** covers Windows, macOS and Linux; its mobile and browser targets will be recorded separately when the host exists.

## Keeping this file current

- Change a cell in the pull request that changes the status, not afterwards.
- A caveat is a numbered note, not a new status value.
- A new control or framework adds a row or a column here and a milestone on the roadmap.

[treegrid]: https://github.com/TheEightBot/KumikoUI/milestone/8
[charts]: https://github.com/TheEightBot/KumikoUI/milestone/9
[scheduler]: https://github.com/TheEightBot/KumikoUI/milestone/10
[pivot]: https://github.com/TheEightBot/KumikoUI/milestone/11
[gantt]: https://github.com/TheEightBot/KumikoUI/milestone/12
[wpf]: https://github.com/TheEightBot/KumikoUI/milestone/13
[winui]: https://github.com/TheEightBot/KumikoUI/milestone/14
[avalonia]: https://github.com/TheEightBot/KumikoUI/milestone/15
[blazor]: https://github.com/TheEightBot/KumikoUI/milestone/16

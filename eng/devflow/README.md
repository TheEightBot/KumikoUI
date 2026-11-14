# MAUI sample verification with .NET MAUI DevFlow

[.NET MAUI DevFlow](https://learn.microsoft.com/dotnet/maui/developer-tools/devflow/) drives a running MAUI app from the
command line: an in-app agent serves the visual tree, screenshots and input over HTTP, and the `maui devflow` CLI (and
its MCP server for AI agents) talks to it. `samples/SampleApp.Maui` references the agent in **Debug builds only**, and
`verify.sh` here uses it to exercise the sample page by page, with a screenshot per step.

DevFlow is an experimental preview; its commands change between releases.

## What is wired up

| Piece | Where |
| --- | --- |
| Agent package `Microsoft.Maui.DevFlow.Agent` (version `MauiDevFlowVersion`) | `Directory.Packages.props`, referenced by `samples/SampleApp.Maui/SampleApp.Maui.csproj` when `KumikoDevFlow` is true: the default in Debug, never in other configurations. `-p:KumikoDevFlow=false` builds a Debug sample without it. |
| Agent registration and the KumikoUI extension | `samples/SampleApp.Maui/DevFlow/KumikoDevFlowExtension.cs`, called from `MauiProgram` under `#if KUMIKO_DEVFLOW` |
| Mac Catalyst `network.server` entitlement for the agent's listener | `samples/SampleApp.Maui/Platforms/MacCatalyst/Entitlements.Debug.plist` (DevFlow builds only) |
| The verification suite | `eng/devflow/verify.sh` and its helpers in `eng/devflow/lib.sh` |

While DevFlow is on, the sample's `Microsoft.Maui.Controls` follows `MauiDevFlowMauiVersion` (the `Microsoft.Maui.Core`
version the agent requires) instead of `MauiVersion`, so Controls and Core come from the same MAUI release. Release
builds keep `MauiVersion` and contain no DevFlow assembly.

### The `com.kumikoui.sample` extension

KumikoUI draws the whole grid on one canvas, so DevFlow's visual tree stops at the `KumikoDataGrid`: there are no cell
elements to query or tap. The sample registers a DevFlow agent extension that reads the grid through its public API
and tells a script where things are drawn. Every tool takes an optional `grid` (index on the current page, or
`AutomationId`); coordinates are DIPs relative to the grid, which is what `ui tap --x --y` expects.

| Tool | Method | Returns or does |
| --- | --- | --- |
| `page` | GET | Current page title, type, Shell route and grid count (works on pages without a grid). |
| `state` | GET | Rows, items, whether a background view rebuild is pending (`isViewUpdatePending`; wait for `false` before reading rows after a sort, filter or grouping change on a large source), columns (display index, width, freeze, sort, filter), sort, filters, groups, collapsed groups, selection, current cell, edit state and editor type, scroll offsets, summaries, theme and colors, whether a popup is open. |
| `cell` | GET | Bounds, center and display text of a cell (`row`, `column` as index, header or property name). |
| `header` | GET | Header bounds plus the filter funnel and resize grip points, each with the region Core's hit tester reports there. |
| `hit-test` | GET | Region, row and column at a point. |
| `rows` | GET | Display texts of one column for a range of rows. |
| `group-row` | GET | The n-th group header row: index, bounds, text, item count, expanded. |
| `popup` | GET | The open drawn popup; for the filter panel also its buttons and list items. |
| `scroll` | POST | `ScrollToRow`, `ScrollToColumn`, or the offsets. |
| `edit` | POST | Types into, sets, commits or cancels the open editor (see the limits below). |

```bash
maui devflow extensions describe com.kumikoui.sample
maui devflow extensions call com.kumikoui.sample state '{}'
maui devflow extensions call com.kumikoui.sample cell '{"row":4,"column":"Salary"}'
maui devflow ui tap <grid element id> --x 464 --y 20      # the Salary header: sorts
```

## Prerequisites

- .NET 10 SDK with the MAUI workloads, Xcode (for Mac Catalyst and the iOS Simulator), `jq` and `curl` (both ship
  with macOS).
- The `maui` CLI at the same version as `MauiDevFlowVersion` in `Directory.Packages.props`:

  ```bash
  dotnet tool install -g Microsoft.Maui.Cli --version 0.1.0-preview.14.26555.2
  ```

  Or install it anywhere (`--tool-path`) and point the suite at it with `MAUI=/path/to/maui`.

## Running the suite

```bash
eng/devflow/verify.sh                       # Mac Catalyst: build, launch, verify, quit
eng/devflow/verify.sh --platform ios        # iOS Simulator (a dedicated "KumikoUI-DevFlow-iPad" simulator)
eng/devflow/verify.sh --platform ios --device "iPhone 17 Pro"
eng/devflow/verify.sh --no-build            # reuse the last Debug build
eng/devflow/verify.sh --agent-port 10224    # attach to a sample that is already running
```

The suite builds the Debug sample, launches it, waits for its agent (matched by process id, so other DevFlow apps on
the machine are left alone), and on Mac Catalyst resizes the window to 1000 × 800 so the grid scrolls sideways. On iOS it
creates and boots an 11-inch iPad Pro simulator named `KumikoUI-DevFlow-iPad` unless `--device` names one, so it never
replaces an app another project installed on a shared simulator. An iPad, because on a phone in portrait the All
Components grid's frozen columns are wider than the screen and hide the scrolling ones
([#268](https://github.com/TheEightBot/KumikoUI/issues/268)), which fails most of the column checks; `--device` runs it
on a phone anyway.

It prints `PASS`/`FAIL`/`SKIP` per check and a summary of the failures, and exits 0 when everything passed, 1 when a
check failed and 2 when the app could not be built or reached.

Screenshots go to `artifacts/devflow/<platform>-<timestamp>/` (git-ignored) with the build and app logs, or to
`--out <dir>`.

### What it covers

| Page | Checks |
| --- | --- |
| Every page | Navigates by Shell route (the Styling and Events pages included); the route, a sized grid and its rows. |
| All Components | Summary rows; header-tap sort ascending and descending (row order checked); tap selection and current cell; double tap opens the text, numeric and date editors, commits (`$123,456`, `2020-01-15`) and the salary total follows; tap toggles a check box; resize by dragging the header grip; reorder by dragging a header; a sideways drag scrolls while the left- and right-frozen columns stay put; an upward drag scrolls while the frozen rows stay put; the filter funnel opens the panel, Deselect All, check one value, OK filters, Clear restores. |
| Grouping | Group by a column; tap a group header to collapse and expand it (row counts checked); Collapse All, Expand All; two-level grouping; Clear Groups; Toggle Filtering. |
| Large Data | Load 100K rows; scroll to the end; tap a row there. |
| Theming | Light, Dark (background brightness checked), High Contrast and the custom styles; a grid that follows the system theme switches with `maui devflow theme set dark/light` (skipped on the iOS Simulator, where DevFlow changes the simulator's appearance and the running app does not follow). |
| MVVM Actions | Tap selection; the Clear command (`SelectedItem = null` from the view model); the Details action button drawn in a cell opens the details page; Close returns; last, Select first (`SelectedItem` set from the view model), which currently hangs the app ([#267](https://github.com/TheEightBot/KumikoUI/issues/267)). |
| Custom Fonts | Both grids load. |

Pages are reached by Shell route and buttons by their text, so the suite keeps working when page layouts change.

## Limits (DevFlow preview 14)

- **Keyboard.** DevFlow sends keys only to `Entry`, `Editor` and `SearchBar`; the grid takes keys through its own
  responder, so a script cannot type into a cell. The suite opens editors with real taps and then fills them through
  the `edit` tool, which makes the same `ITextInputSession` calls a platform text-input adapter makes, or sets the date
  or number on the drawn editor. Keyboard navigation, Tab, Enter-to-commit and clipboard shortcuts are not covered.
- **Taps at a point** need `AgentOptions.EnableSyntheticTouch` on iOS and Mac Catalyst (the sample enables it).
  DevFlow's `ui gesture doubletap` and `ui gesture pan` did not reach the canvas; the suite double-taps with two taps
  from one `maui devflow batch` process and scrolls with `ui gesture drag`.
- **Mutation lease.** Every `maui devflow` process claims the app's mutation lease under a new id and holds it for
  10 s, so a second mutating command from a new process is refused. `lib.sh` frees the lease through the agent's
  `/api/v1/agent/lease` endpoint before each mutating command.
- **Mac Catalyst screenshots** come out at twice the window's height with the lower half black (a DevFlow capture
  issue with Catalyst's 77 % scaling); the app content in the upper half is correct.
- **Android and Windows** agents are still "in progress" in DevFlow. Android needs `adb reverse` for the agent port;
  the suite does not launch Android yet. With an emulator, deploy the Debug sample yourself
  (`dotnet build samples/SampleApp.Maui -f net10.0-android -t:Run`) and run `verify.sh --agent-port <port>` with
  the port `maui devflow list` shows.

## AI agents

`maui devflow mcp` starts an MCP server over stdio with the same capabilities (tree, screenshots, taps, gestures, and
`maui_extension_call` for the `com.kumikoui.sample` tools). Point an MCP client at it while the Debug sample runs.

## Adding it to CI later

The suite is not part of the CI test jobs: it needs a desktop session (Mac Catalyst) or a booted simulator, and a
full Debug build of the sample. To add it, a macOS job would:

1. Install the MAUI workloads and the matching `Microsoft.Maui.Cli` tool.
2. Run `eng/devflow/verify.sh --platform ios` (a simulator boots headless on the hosted macOS runners; Mac Catalyst
   needs a logged-in GUI session, which hosted runners have but self-hosted ones may not).
3. Upload `artifacts/devflow/` as a workflow artifact so the screenshots can be reviewed, and fail the job on exit
   code 1.

Expect it to take several minutes (the Debug build dominates) and to need updating as DevFlow's preview CLI changes.

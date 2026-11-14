# Interaction timings and thresholds

Every timing, distance and target size the grid uses to read a gesture or play feedback, in one place (#101). Distances are in device-independent pixels (points on Apple platforms, dp on Android, DIPs on Windows and Uno); times are in milliseconds. "Core" means `KumikoUI.Core`, which both hosts share; host rows apply to the MAUI and Uno grids as named.

## Gestures

| What | Value | Where | Notes |
|---|---|---|---|
| Double tap / double click | 400 ms, 20 px | MAUI `KumikoDataGrid` (`DoubleTapThresholdMs`, `DoubleTapDistanceThreshold`), Uno `PointerGestures` | The second press within the time and distance of the first reports `ClickCount` 2 on its press and release. A third tap starts over. |
| Long press | 500 ms, 15 px | MAUI `KumikoDataGrid` (`LongPressThresholdMs`, `LongPressMoveTolerance`), Uno `LongPressTracker` | Moving farther cancels it. On Uno, WinUI's `Holding` event is a fallback for touch; whichever fires first wins. A mouse long press is a slow click (the secondary button asks for the menu). |
| Pan threshold | 10 px | Core `KumikoInputController` (`PanThreshold`) | Movement from the press before a drag scrolls the grid. |
| Drag threshold | 8 px | Core `KumikoInputController` (`DragThreshold`) | Movement before a header drag reorders a column (mostly horizontal) or a mouse or pen drag on a cell reorders a row (mostly vertical, with `AllowRowDragDrop`). |
| Drag handle slop | 2 px | Core `KumikoInputController` (`HandleDragSlop`) | A press on a row's drag handle starts the drag after this little movement, in any direction. |
| Press and release on the same target | — | Core | Header sorts, filter icons, grouping chips, group headers and cell taps act on the release, only when the press started on the same target (a group header: the same header, chevron or row). |
| Stale pointer | 1000 ms | Core (`StalePointerMs`), both hosts | While one pointer is down, others are ignored, unless it has been silent this long (its release was lost). |
| Text box multi-click | 500 ms, 4 px | Core `DrawnTextBox` | Double click selects a word, triple click everything. |
| Editor list drag | 6 px | Core `DrawnComboBox`, `DrawnFilterPopup`, `DrawnScrollPicker` | Movement before a press in a list scrolls it instead of picking. Year list in `DrawnDatePicker`: 4 px. |

## Targets

| What | Mouse or pen | Finger | Notes |
|---|---|---|---|
| Column resize grip | 12 px (`KumikoStyle.ColumnResizeGripWidth`) | 32 px (`KumikoStyle.TouchColumnResizeGripWidth`) | Centered on the column's right header edge; each half reaches at most a quarter of the column on that side (at least 3 px). A double click on it auto-fits the column. |
| Filter icon | 10 px icon + 5 px each side | + 11 px each side (32 px) | The margin stops at the resize grip, but the icon itself wins over a finger's wide grip that reaches over it. Drawn left of the sort arrow on a sorted column. |
| Group header chevron | its pill + `CellPadding` after it | + `CellPadding` before and twice after | The whole group header row toggles the group too; the chevron gets the hand cursor and the hover highlight. |
| Grouping panel chip | the chip as drawn, + 2 px each side | as tall as the panel | Measured by the renderer while drawing (`KumikoRenderer.InputController`). Its right end (the remove button and its padding, about 25 px) removes the grouping. |
| Check box | 16 px box + 8 px each side | same | Inside the cell. |
| Row drag handle | `KumikoStyle.RowDragHandleWidth` (32 px) | same | Full row height. |

## Scrolling and momentum

| What | Default | iOS preset | Android preset | Desktop preset | Notes |
|---|---|---|---|---|---|
| Friction (velocity kept per 16 ms) | 0.92 | 0.968 | 0.955 | 0.92 | `ScrollSettings.Friction`; integrated over the real frame time, so 60 Hz and 120 Hz coast alike. |
| Maximum fling speed | 8000 px/s | 8000 | 8000 | 6000 | `ScrollSettings.MaxVelocity`. |
| Stop speed | 0.5 px/s | 10 | 10 | 5 | `ScrollSettings.MinVelocity`. |
| Speed needed to fling | 5 px/s | 50 | 50 | 100 | `MinVelocity × FlingThresholdMultiplier`. |
| Hold before release that cancels the fling | 100 ms | same | same | same | `ScrollSettings.FlingHoldCutoffMs`. |
| Velocity smoothing | 0.3 | same | same | same | `ScrollSettings.VelocitySmoothing`. |
| Wheel notch | 3 rows | same | same | same | `ScrollSettings.WheelScrollMultiplier`; trackpads scroll by their pixels. Shift turns a vertical wheel horizontal. |
| Longest frame step integrated | 100 ms | | | | Core `InertialScroller`; the hosts' frame schedulers pass at most 50 ms. |
| Edge autoscroll band, speed | 40 px, 1200 px/s | same | same | same | `ScrollSettings.AutoScrollEdgeSize`, `AutoScrollMaxSpeed`; during a row drag, linear from the band's inner edge. |

The MAUI grid picks the iOS preset on iPhone and iPad, Android on Android and Desktop on Mac Catalyst and Windows; the Uno grid picks iOS and Android on those heads, Android in a browser whose main pointer is a finger (CSS `pointer: coarse`), and Desktop elsewhere (`ScrollSettings.FromPreset`). Assigning `ScrollSettings` replaces the preset.

## Feedback and transitions

| What | Value | Notes |
|---|---|---|
| Hover highlight | `KumikoStyle.HoverOverlayColor` (black 6%, white 8% on Dark) | Mouse and pen only, on sortable headers, filter icons, grouping chips and their remove buttons, group chevrons, check boxes and enabled action buttons. Redrawn only when the target under the pointer changes. |
| Pressed highlight | `KumikoStyle.PressedOverlayColor` (black 12%, white 16% on Dark) | Any pointer, while it stays on the pressed target; dropped once the press becomes a pan or a drag. |
| Group expand / collapse | 200 ms | The rows after the header slide by the rows added or removed (at most the height below the header). |
| Column header lift | 120 ms | The dragged header's shadow grows. |
| Column header drop | 180 ms | The header settles from where it was released into its slot (back to its old one when the drag is canceled). |
| Selection fade-in | 120 ms | The current row's highlight, when the current row shows another item. |
| Easing | ease-out cubic | All transitions. `KumikoStyle.EnableAnimations` = false, or the platform's reduced-motion setting, shows every change at once. |
| Haptics | drag start, drop, long press | `KumikoDataGrid.EnableHaptics` (off by default); finger and pen only. iOS `UIImpactFeedbackGenerator`, Android `PerformHapticFeedback`. |

## Editors and frame loop

| What | Value | Where |
|---|---|---|
| Caret blink | 530 ms on, 530 ms off | Core `DrawnTextBox`; the hosts redraw only at the transitions (`KumikoCaretBlink`, 8 ms slack). |
| Spin button repeat | 400 ms delay, then every 100 ms, speeding up to every 25 ms | `DrawnNumericUpDown.RepeatDelayMs`, `RepeatIntervalMs`, `MinRepeatIntervalMs`. |
| Spin key release / first repeat | 300 ms, 1500 ms | `DrawnNumericUpDown`: a held arrow key without key-up events. |
| Frame step | 16 ms nominal, 1 to 50 ms | Hosts' `KumikoFrameScheduler`: the measured time between frames, clamped. |

# MAUI host device verification

Verification of `KumikoUI.Maui` and `samples/SampleApp.Maui` on devices, on the integrated 1.0 code (main plus the
wave 1 and wave 2 branches, including the render mode rename `KumikoRenderMode.Gpu` to `Hardware`). The method is the
DevFlow suite (`eng/devflow/verify.sh`, see [eng/devflow/README.md](../eng/devflow/README.md)) plus, on Android,
manual checks that the suite cannot make (soft keyboard, render modes, frame times). The screenshots (one per
suite step, 6.4 MB) are kept out of the repository; a rerun of the suite writes them to `artifacts/devflow/`.

| Target | Device | Result | Screenshots |
| --- | --- | --- | --- |
| Mac Catalyst | the Mac, window 1000 x 800 | 106 passed, 0 failed | `maccatalyst/` |
| iPad simulator | KumikoUI-MAUI-iPad (iPad Pro 11-inch M5, iOS 26.5) | 104 passed, 0 failed, 1 skipped | `ios-ipad/` |
| iPhone simulator | KumikoUI-MAUI-iPhone (iPhone 17 Pro, iOS 26.5) | 106 passed, 0 failed | `ios-iphone/` |
| Android emulator | KumikoUI-MAUI-Android (Pixel 7, Android 15 arm64, 1080 x 2400 at 420 dpi, host GPU) | 106 passed, 0 failed (final run, see Android) | `android-suite/`, `android-manual/` |

The one skip on the iPad is "a grid follows the app's light/dark theme": DevFlow changes the simulator's appearance
and the running app does not follow it (known, see the README). Mac Catalyst screenshots have the lower half black (a
DevFlow capture issue, also in the README).

## What the suite checks (all targets)

Navigation to every page; sorting; selection; text, numeric and date editing with commit; check box toggle; column
resize and reorder; scrolling with frozen panes; the filter popup; grouping; 100K rows with background sort; theming;
custom fonts; MVVM selection and the in-cell Details action button. 106 checks.

## iPhone: the four failures of the earlier phone run

All four were suite or sample assumptions that do not hold in a 402 pt wide portrait grid. None was a Core or host
bug, so no product code changed.

| Failure | Cause | Fix |
| --- | --- | --- |
| Dragging the Department grip widens it by 60 | The grip sits at the grid's right edge (x = 402); the drag ended 60 points beyond the screen. | The suite scrolls sideways until the whole drag is on screen. |
| Dragging Hire Date onto Salary reorders it | Two 120 pt columns never fit beside the 232 pt frozen pane, so Hire Date's center was off screen and `header_point` scrolled Salary under the pane. The first fix attempt pressed the part of Hire Date that is visible, which is Salary's own resize grip. | A drop only picks a slot among the pane's columns (`ColumnDragGeometry`: the pointer is clamped to the pane), so Salary may be partly under the frozen pane. The suite scrolls until Salary's left edge is 40 points under it, presses Hire Date at 28 points from its left edge and releases just inside the pane. On wide grids the original center-to-left-edge drag is unchanged. |
| Right-frozen City stays put | Not a bug: #268 now unpins frozen columns when they exceed `MaxFrozenColumnsWidthRatio` of a grid that scrolls (right-frozen first). City is a scrolling column on a phone. The README note blaming #268 for failing checks is replaced. | The extension reports `frozenPanes.mayUnpin` (the same arithmetic from public data: columns' widths, the style's ratio, the drag handle). A frozen column that moved passes only where `mayUnpin` is true; where it is false, as on the iPad, it must still stay put. |
| The Details action button opens the details page | The Actions column is `SizeMode="Star"` with `Width="200"` and the default `MinWidth` of 40. Core documents that a Star column shrinks to its minimum when the other columns fill the grid, so on a phone the column was 40 points wide and its two buttons were slivers (the buttons drew as thin stripes). | Sample fix: `MinWidth="180"`, so the buttons stay tappable on a phone and the column still grows on a wide screen. |

The filter checks also needed the funnel on screen once the resize had widened Department (the funnel was past the
right edge); `header_point` now scrolls on until the point is 24 DIPs inside the grid.

## Android

### Method

`verify.sh --platform android --device emulator-5554` builds with `-t:Install` (a Fast Deployment APK installed with
`adb install` aborts at startup: it has no assemblies in it), starts the activity, forwards the agent's port 9223 and
runs the suite. DevFlow on Android needed three workarounds, all in `eng/devflow/lib.sh`:

- DevFlow refuses `ui tap` and `ui gesture drag` at a point ("Point on SKGLView is covered by PlatformGraphicsView").
  Real `adb input` taps work, so the suite maps a grid point to a screen pixel through the grid's bounds in
  `uiautomator dump` and sends taps, double taps and swipes through adb. The refusal is a DevFlow hit-test check; the
  grid receives real touches normally.
- DevFlow's screenshot leaves the GPU surface out (a `TextureView`): the grid comes out blank. Screenshots use
  `adb screencap`.
- `maui devflow theme set` calls adb without a serial and fails when two emulators run; the suite passes `--device`.

The suite's resize check allows 10 DIPs on Android (touch slop and pixel rounding of an `input swipe`).

### Render mode on the device

`RenderMode = Auto` resolved to the hardware surface on the emulator (`ActiveRenderMode` is `Gpu` in these runs, now
named `Hardware`): SkiaSharp's `SKGLView` on a `TextureView`, which had never run on a device. Findings:

| Area | Result |
| --- | --- |
| Rendering | Correct on every page: text, frozen panes, selection, summaries, popups, themes (light, dark, high contrast, three custom) and the custom fonts page. No tearing, wrong colors or blank frames were seen in the screenshots or by eye during flings. |
| Scroll and fling | Smooth by eye (`android-manual/01-gpu-fling-framestats.jpg` shows the overlay); momentum works. |
| Selection, editing | Tap and double tap select and open editors; text, numeric, date and scroll picker editors work. |
| Soft keyboard | See below. |
| Filter popup | Opens, the search box takes the keyboard, the popup keeps its OK / Cancel / Clear buttons above the keyboard (`android-suite/17-main-filter-open.jpg`). |
| Haptics | `EnableHaptics = true` through the DevFlow `render` tool; long press and a row drag from the drag handle (hold, move, release) reordered the row, no crash. |
| Mouse hover | Not verified: adb cannot inject hover events and the emulator was driven headless. The Android pointer path (`AttachNativePointer`) was exercised only through touch. |
| Switching `RenderMode` at run time | Auto to Software to Hardware through the DevFlow tool worked and redrew each time. |
| Software mode | Drew the same frames as Hardware (`android-manual/11-software-framestats.jpg`). Nothing looked wrong in Hardware, so Software was used only for the comparison. |

Soft keyboard (#41, #258), on a row near the bottom of the grid:

- A double tap on a Name cell near the bottom opened the editor; the keyboard appeared, the window moved up and the
  edited cell was left visible just above the keyboard (`android-manual/03-edit-bottom-row-keyboard.jpg`). The text is
  selected on entry.
- Composition through Gboard: typing letters showed the suggestion strip, the composed text appeared in the cell with a
  caret and replaced the selection (`04-composition-typing.jpg`); tapping a suggestion committed it
  (`05-suggestion-committed.jpg`); `adb input text` typed on.
- Back with the keyboard up hid the keyboard and left the edit open (`06-after-back.jpg`); the layout returned to
  normal. Enter committed the text and ended the edit (`07-after-enter.jpg`); Escape ended an edit. A further Back
  with no edit open left the app, as Android does.
- Observation, by design per `docs/TEXT-INPUT.md` ("No text surface"): a non-text editor (the Level scroll picker,
  `08-edit-level.jpg`) also raises the soft keyboard, so typed characters reach the picker as key events. It covers the
  lower half of the grid for an editor that mostly needs touch. A product decision, not changed here.

### Frame times and the #263 recommendation

The grid's frame stats overlay (`ShowFrameStats`) and the new DevFlow `render` tool (reads `KumikoFrameStats`) were
used. Caveats first: the emulator runs on a host with a load average of 17 to 40 (other agents building and running
two emulators), so absolute numbers are inflated and noisy, and `FrameTimeMs` measures the grid's own `Render` call on
the UI thread (for the Hardware surface that is recording the frame; the GPU work is on another thread).

| Measure | Hardware (`SKGLView`) | Software (`SKCanvasView`) |
| --- | --- | --- |
| `FrameTimeMs` during a fling, quiet moments | 0.50 ms, 60 fps, 0 B per frame | 22 to 26 ms, 4 to 18 fps, 2 to 6 KB per frame |
| Same, under host load | 5 to 40 ms | 20 to 290 ms |
| `dumpsys gfxinfo` over 16 flings (UI thread frames) | median 93 ms, 85 % janky | median 200 ms, 95 % janky |
| Allocations per frame | 0 (192 B appeared only after switching modes at run time) | grows with the scroll: the bitmap copy, not the render loop |

Both are far from 16 ms on this emulator under load; Hardware is two to ten times better on every measure, and the
software surface's per-frame allocation (a bitmap) and CPU cost are what the hardware surface exists to avoid.

**Recommendation for #263: keep `Auto` = Hardware on Android.** It drew correctly everywhere, it is clearly faster on
every measure, and `RenderMode = Software` is a one-line escape hatch that works at run time (verified). Before 1.0,
confirm it on one physical device (a low-end Mali or Adreno phone is the realistic risk: GL driver bugs do not show on
the emulator's host GPU) and keep the XML doc's advice to switch to `Software` if a driver misdraws. Re-measure on
that device with the `render` tool.

## Follow-ups

Not fixed here (outside `src/KumikoUI.Maui`, the sample and `eng/devflow`, or a product decision):

1. Core renderer: while a row scrolls under a frozen row, its drag handle glyph is drawn over the frozen row's handle
   (visible in `ios-iphone/16-main-scrolled-vertically.jpg` and `android-manual/01-gpu-fling-framestats.jpg`): the handle
   column is not clipped to the scrolling area.
2. Dragging a column header across a phone-width scrolling pane cannot reach a column that is off screen: only row
   drags autoscroll (`ScrollSettings.AutoScrollEdgeSize`). A horizontal autoscroll for column drags would make reordering
   work on phones.
3. Soft keyboard for non-text editors on Android (see above).
4. Mouse hover and a physical-device pass on Android (hover, haptics feel, GPU driver variety).
5. DevFlow preview issues that shape the suite: refused point taps on the Android GPU view, blank GPU screenshots,
   `theme set` without a serial.

# Uno device verification (#67)

Every head of `samples/SampleApp.Uno` that runs on the verification Mac, exercised by hand (through automation that
drives real input) against the checklist in issue #67. Run on 2026-11-13 on `integration/1.0-wave1` plus the fixes
listed under [Fixed during the pass](#fixed-during-the-pass); everything marked ✅ for WebAssembly and Android was
re-checked on the merged code.

The desktop columns were walked again on 2026-11-17 on `main` after `1.0.0-preview.1` (#274): macOS, Windows and Linux
(X11). Notes 17 to 27 describe that pass.

**Legend:** ✅ works · ⚠️ works with a problem (see the note) · ❌ broken (follow-up filed below) · ◻ not exercised
(the reason is in the note) · n/a not applicable to the head.

## Environment

| Head | Where it ran | How it was driven |
|---|---|---|
| WebAssembly | `dotnet publish -f net10.0-browserwasm -c Release -p:PublishTrimmed=true -p:TrimMode=full` (exactly as CI), served with `python3 -m http.server`, opened in Chromium (Claude Browser pane), 1280×800, light and dark color scheme | Chrome DevTools Protocol mouse, wheel and keyboard input |
| Android | `net10.0-android` Release APK on a new emulator AVD `KumikoUI-Uno-Android` (Pixel 7 profile, Android 15 / API 35 Google APIs arm64, 4 GB RAM, 8 GB data partition), Gboard | `adb shell input` taps, swipes and soft-keyboard key taps (so typing goes through Gboard's composing text) |
| iOS | `net10.0-ios` Release build for `iossimulator-arm64` on a new simulator `KumikoUI-Uno-iPhone` (iPhone 17 Pro, iOS 26.5) | Simulator touch injection (taps, swipes) and the simulator keyboard |
| Desktop (macOS) | `net10.0-desktop` Release, launched from an app bundle wrapper, macOS 27.2, dark appearance; again on 2026-11-17 (Release and Debug) | Screenshots and the macOS accessibility tree only (notes 3 and 17) |
| Desktop (Windows) | 2026-11-17: the portable `net10.0-desktop` Release output, run with `dotnet SampleApp.Uno.dll` (Win32 host) in a Windows 11 ARM64 VM (Parallels), 2048×1280 at 200% (192 DPI) | Mouse and keyboard input injected in the signed-in session from PowerShell (`mouse_event`, `keybd_event`), screenshots of the window, the UI Automation tree (note 18) |
| Desktop (Linux) | 2026-11-17: the same output in Docker (`mcr.microsoft.com/dotnet/sdk:10.0`, Ubuntu 24.04 arm64), X11 host on Xvfb 1600×1000 at 96 DPI, no window manager | `xdotool` (XTest) input, ImageMagick screenshots, an AT-SPI dump with `pyatspi` (note 19) |
| Windows App SDK | not run (note 20) | — |

Screenshots of the run were kept outside the repository (they are large and machine-specific); the final report of
this work lists where.

## Matrix

| Check | Desktop (macOS) | WebAssembly (Chromium) | iOS simulator | Android emulator | Desktop (Windows) | Desktop (Linux, X11) | WinAppSDK |
|---|:--:|:--:|:--:|:--:|:--:|:--:|:--:|
| Render on launch (grid, frozen rows/columns, summaries, sample shell) | ✅ ¹⁷ | ✅ ¹ | ⚠️ ² | ⚠️ ² | ✅ ¹⁸ | ✅ ¹⁹ | not run ²⁰ |
| Touch scroll and fling (momentum) | n/a | n/a | ✅ | ✅ | n/a | n/a | not run ²⁰ |
| Mouse wheel / trackpad, vertical and horizontal | ◻ ³ | ✅ | n/a | n/a | ✅ (3 rows a notch, horizontal too) | ◻ ²¹ | not run ²⁰ |
| Selection (click/tap, current cell) | ◻ ³ | ✅ | ✅ | ✅ | ✅ | ✅ | not run ²⁰ |
| Editing: text (begin with double click/tap, F2, typing; commit with Enter) | ◻ ³ | ✅ | ❌ ⁴ (typing over a selection) | ✅ after fix ⁴ | ✅ (double click, typing over the selection, Enter) | ✅ (double click, F2 + End to append, typing to start an edit, Enter) | not run ²⁰ |
| Editing: IME / composition | ◻ ³ | ◻ ⁵ | ◻ ⁵ | ✅ after fix ⁴ (Gboard composing text, suggestions) | ◻ ²² | ◻ ²² | not run ²⁰ |
| Editing: combo box | ◻ ³ | ✅ ⁶ | ◻ | ◻ | ✅ | ✅ | not run ²⁰ |
| Editing: date picker | ◻ ³ | ✅ | ◻ | ◻ | ✅ | ✅ | not run ²⁰ |
| Keyboard navigation (arrows, Tab between cells) | ◻ ³ | ✅ | n/a | n/a | ✅ | ✅ | not run ²⁰ |
| Tab out of the grid (no keyboard trap) | ◻ ³ | ✅ | n/a | n/a | ✅ (both ends) | ✅ (both ends) | not run ²⁰ |
| Filter popup (open, check list, search, OK) | ◻ ³ | ✅ after fix ⁷ | ✅ | ✅ ⁸ | ◻ ²³ (opens) | ✅ (search narrows the list, OK filters, Clear) | not run ²⁰ |
| Column resize (drag the header border) | ◻ ³ | ✅ | ◻ | ✅ | ◻ ²³ | ✅ | not run ²⁰ |
| Column reorder (drag a header) | ◻ ³ | ✅ | ◻ | ✅ ⁹ | ◻ ²³ | ✅ | not run ²⁰ |
| Row drag (handle) | ◻ ³ | ◻ ¹⁰ | ◻ ¹⁰ | ✅ (after the sample fix) | ◻ ²³ | ✅ | not run ²⁰ |
| Grouping (group buttons, chips, remove chip, expand/collapse) | ◻ ³ | ✅ | ◻ | ◻ | ◻ ²³ | ✅ | not run ²⁰ |
| Theme switch: sample button (Light → Dark → High Contrast) | ◻ ³ | ✅ | ◻ | ◻ | ◻ ²³ | ✅ | not run ²⁰ |
| Theme switch: follows the system light/dark change live | ✅ (dark at launch) | ✅ | ◻ | ✅ after fix ¹¹ | ◻ ²⁴ (dark at launch) | ◻ ²⁴ | not run ²⁰ |
| DPI / scale change | ◻ ¹² | ◻ ¹² | n/a | n/a | ◻ ²⁴ (correct at 200% from launch) | ⚠️ ²⁵ | not run ²⁰ |
| Keyboard avoidance (edited bottom row stays above the soft keyboard) | n/a | n/a | ✅ | ✅ | n/a | n/a | n/a |
| Hover feedback (headers, chips, chevrons) | ◻ ³ | ✅ | n/a | n/a | ◻ ²³ | ✅ (headers) | not run ²⁰ |
| Pressed feedback and transitions (group slide, header lift/drop, selection fade) | ◻ ³ | ◻ ¹³ | ◻ ¹³ | ◻ ¹³ | ◻ ¹³ | ◻ ¹³ | not run ²⁰ |
| Large data (100,000 rows: load, scroll) | ◻ | ✅ (load 514 ms, 3.8 ms frames) | ◻ | ◻ | ◻ ²³ | ✅ (load 73 ms, Ctrl+End to row 100,000) | not run ²⁰ |
| CJK and icon fonts (Custom Fonts page) | ◻ | ✅ | ◻ | ◻ | ◻ ²³ | ✅ | not run ²⁰ |
| Screen reader smoke test | ❌ ¹⁴ | ✅ ¹⁵ | ◻ | ✅ ¹⁶ | ✅ ²⁶ (UI Automation) | ❌ ²⁷ | not run ²⁰ |

### Notes

1. **WebAssembly, trimmed build:** the CI publish (`PublishTrimmed=true`, `TrimMode=full`) builds and runs: every
   page loads and every check in the WebAssembly column was made on it. Two console errors are unrelated to the
   grid: the service worker registration fails under the local `python3 -m http.server`, and before the fix in
   [Fixed during the pass](#fixed-during-the-pass) an `ArgumentOutOfRangeException` was logged on most keystrokes.
   Cell text uses a monospaced fallback face in the browser (the grid's default typeface has no system font to
   resolve to there); this is legible but differs from the other heads.
2. **iOS and Android, sample shell:** *fixed* (follow-up F10, #280). The mobile heads draw edge to edge, so the sample
   page's title and toolbar ran under the status bar and the bottom row under the home indicator / gesture bar.
   The sample shell now pads itself by `ApplicationView.VisibleBounds` and re-applies it on `VisibleBoundsChanged`.
   Checked in portrait and landscape on the iOS simulator (iPhone 17 Pro) and the Android emulator (API 35).
3. **Desktop (macOS) interaction:** the app renders, follows the dark appearance and exposes its XAML buttons to
   the accessibility tree, but pointer and keyboard input could not be delivered to it in this run (background
   automation only reaches accessibility actions, and full-screen control was not granted). Every interactive row
   is therefore unverified on the desktop head. The desktop head shares all of its input code with WebAssembly
   except the platform host itself; it needs a hands-on pass (follow-up F1, #274). The cause is the host, not the
   automation tool alone: Uno's macOS window (`UNOWindow sendEvent:` in Uno 6.7) hands every event straight to AppKit
   while the application is not active, so input sent to an app in the background never reaches Uno or the grid.
   The pass needs the app in front, driven by a person or by full-screen automation. The 2026-11-17 pass did not get
   that (its request to control the app was declined), so these rows are still open.
4. **Typing on iOS and Android was broken before this pass:** typing over a selected value inserted in front of
   it ("XBob Jones"), and typing at the end of a value threw inside Uno; on Android that exception crashed the app
   from the input connection, on iOS characters were lost ("Kim Lee" became "iK" + the old text). Both are fixed
   on Android and re-checked there on the merged code. On iOS the merged code types every character in order
   ("Kim Lee" arrives whole). This pass also saw typing over a selected value go in front of it on iOS (F9, #279),
   which later runs on the released code did not reproduce.
5. **IME on WebAssembly and iOS:** no input method that composes could be driven by the automation (no Japanese
   keyboard is installed in the browser or the simulator). The composition path itself is covered by the unit
   tests of `KumikoTextInputAdapter`.
6. **Combo box (WebAssembly):** picking an item works and Tab commits it. Picking an item with the mouse completes
   and commits the edit at once, so the Enter that follows is not a second commit: on a cell that is not being
   edited Enter opens the editor (like F2), and the new editor opens its list, which is what the run saw as the
   list reopening (follow-up F5). With the list closed by Escape, Enter commits and opens nothing. The list is drawn on
   top of the rows below it and every item stays inside its bounds at every scroll position; the Core tests in
   `ComboBoxEditorFlowTests` pin this, and the overdraw seen in the run was not reproduced.
7. **Filter popup search (WebAssembly):** the search narrows the list, and OK used to apply the check marks of all
   values, including the hidden ones, so search plus OK did not filter; "Select All" stayed checked after
   unchecking items. Both were Core behavior, the same on every host, and are fixed (see
   [Fixed during the pass](#fixed-during-the-pass)).
8. **Filter popup on Android:** opening it, the check list and the search work. The first pass saw no soft keyboard
   when tapping the search box; this did not reproduce on the released code (F2, #275): the keyboard shows on open
   and when the box is tapped after the keyboard was dismissed, and typed text filters the list. The host's
   show-keyboard decision is now `SoftKeyboardPolicy` (unit tested), and the explicit show is repeated once the press
   has settled.
9. **Column reorder by touch (Android):** the first pass's two `adb input swipe` drags did nothing visible. Re-run
   with `adb shell input motionevent DOWN/MOVE/UP` (steps of 20 px, 100 ms apart), the header drag starts (the ghost
   follows the finger) and the column moves on release. A hold of 500 ms or more before moving opens the header's
   sort menu instead (#288), a press near a header's right edge resizes the column (#289), and dragging toward the
   frozen columns auto-scrolls to the start (#290). The earlier failure is put down to the automation; a check with a
   real finger on a physical device is still pending (F3, #276).
10. **Row drag on WebAssembly and iOS:** not exercised; the sample had no page with row drag enabled until the sample
    fix in this pass, and the WebAssembly and iOS runs predate it. Android, run after the fix, works.
11. **Theme follow on Android:** the grid follows a system dark-mode switch live. An editor that was open kept the old
    theme's colors until the edit ended; that is fixed in Core (the edit session re-themes its editor when the style
    changes) and covered by unit tests, not re-run on the emulator.
12. **DPI change:** not possible with the tools available (the browser pane cannot change the device pixel ratio,
    and the macOS desktop app could not be moved between displays). UNO-18 tracks the code path.
13. **Pressed feedback and transitions:** the automation delivers press and release together, and screenshots cannot
    catch a 120 to 200 ms transition. The group expand/collapse, header drag and selection changes all ended in the
    right state.
14. **VoiceOver on macOS desktop:** the macOS accessibility tree lists the sample's buttons and navigation items but
    no element for the grid, so VoiceOver cannot reach it (follow-up F4, #277). The 2026-11-17 pass found the grid
    exposed on the first page shown: in the Release build the window's tree has an `AXTable` with the description
    "Employee data grid with all component types" and the help "200 rows, 11 columns", and setting its `AXFocused`
    focuses the grid. The tree breaks in two cases, both in Uno's macOS bridge: after navigating to another page the
    whole window's tree collapses to one stray element (buttons and navigation items included), and in a Debug build
    (what `dotnet run` gives) the sample's Hot Design (`UseStudio()`) collapses it at launch. VoiceOver itself was not
    turned on. Details and the upstream report are under F4.
15. **Screen reader on WebAssembly:** with Uno's "Enable accessibility" semantic DOM on, the grid is a `role="grid"`
    element named by `GridDescription` whose description follows the current cell ("200 rows, 11 columns, current
    cell row 7 column 2"). No screen reader was run against it.
16. **TalkBack on Android:** the accessibility tree TalkBack reads (`uiautomator dump`) has the grid as one focusable
    view named "Employee data grid with all component types"; TalkBack itself was not driven.
17. **macOS, 2026-11-17:** the Release build of `main`, wrapped in an app bundle and launched in the background, drew
    the All Components page in the dark appearance (grid, frozen rows and columns, summaries, sample shell). The
    session had no screen-recording permission, so the window was captured from inside the app with
    `RenderTargetBitmap`, and the accessibility tree was read through the AX API.
18. **Windows, 2026-11-17:** the portable desktop output built on macOS (`dotnet build -f net10.0-desktop -c Release`)
    ran with `dotnet SampleApp.Uno.dll` in a Windows 11 ARM64 VM at 200% scale, through Uno's Win32 host, and started
    in the dark theme. Building in the VM failed at restore with NETSDK1147: restore walks every head, and the VM's
    workloads (installed by Visual Studio) do not cover the band of SDK 10.0.402.
19. **Linux, 2026-11-17:** the same output ran in Docker under Xvfb through Uno's X11 host and started in the light
    theme (the container has no desktop settings). Every ✅ in this column is a screenshot after real X input.
20. **Windows App SDK head:** not run. It cannot be built in the VM for the reason in note 18, and no other Windows
    machine was available.
21. **Wheel on Linux:** not checkable in the container. A wheel notch from `xdotool` arrives as an XInput2 press of
    button 4 to 7. Uno 6.7's X11 host makes wheel deltas from smooth-scroll valuators only (its button-press branch
    matches detail values that buttons 4 to 7 never have) and raises the press as a pointer press with no named
    button, so nothing scrolls. The grid takes that press as a primary press: one notch selects the cell under the
    pointer and two quick notches open its editor (F11). Real mice and touchpads send smooth-scroll valuators, which
    Xvfb's test pointer cannot.
22. **IME on Windows and Linux:** on Windows no composing input method was tried before the pass stopped (note 23).
    On Linux, IBus with the Mozc engine ran on the session bus (`org.freedesktop.portal.IBus` was there) and the app was
    restarted with `UNO_IM_MODULE=ibus`, but typing after `Zenkaku_Hankaku` inserted plain letters; the container
    could not tell whether Uno's IBus client failed to connect or Mozc stayed in direct input. The composition path
    is covered by the unit tests of `KumikoTextInputAdapter`.
23. **Windows pass stopped:** the VM was in use by someone else. While the app was in front, text this pass did not
    type ("3 Repeat Walk") arrived in the grid's date editor, so the pass stopped sending input after the filter popup
    opened and closed the app. The rows marked with this note need a Windows machine nobody else is using.
24. **Theme follow and DPI change on Windows and Linux:** the system appearance and the display scale of the shared VM
    were left alone, and the container has no settings service for Uno to follow. On Windows the grid was sharp and
    sized correctly at 200% from launch, and the toolbar, editors and popups matched it.
25. **DPI on Linux:** with `Xft.dpi: 192` at launch, Uno drew the first frames at twice the size but anchored wrongly:
    only the bottom-left part of the layout showed, for the whole window (Uno's own navigation pane too). After the
    window was resized it drew correctly at 2×, grid included. A live DPI change is not possible under Xvfb. This is
    the host's first layout, not the grid.
26. **UI Automation on Windows:** the grid is one `ControlType.DataGrid` element (AutomationId `kumiko`, class
    `KumikoDataGrid`) named "Employee data grid with all component types", and it is keyboard focusable. After a
    click it holds the keyboard focus and its help text follows the current cell ("200 rows, 11 columns, current
    cell row 3 column 2"), also after arrows, Tab and Ctrl+Home/End; Shift+Tab and Tab past the ends move the UIA
    focus to the "Restore Layout" button and the "All Components" navigation item. Narrator itself was not run.
27. **AT-SPI on Linux:** Uno 6.7's X11 host has no AT-SPI bridge. With at-spi2-core running, an AT-SPI dump lists a
    GTK test window and its button but nothing of the Uno app: no window, no buttons, no grid, so Orca would find
    nothing (F12). Orca was not run (no audio in the container).

## Fixed during the pass

| Fix | Heads | Commit |
|---|---|---|
| Typing at the end of a cell editor or the filter search box threw `ArgumentOutOfRangeException` from `TextBox.Select`: the grid wrote its hidden text box back while that text box was still raising `TextChanging`. Crashed the app on Android, logged on most WebAssembly keystrokes, lost characters on iOS. The grid now syncs the text box right after the change. | Android, iOS, WebAssembly (and desktop, same code path) | `fix(uno): do not write the proxy text box while it raises TextChanging` |
| Typing over a selected value inserted in front of it on Android: focusing the hidden text box moved its caret to the start. The grid puts the editor's selection back after focusing it. (This pass also saw the symptom on iOS, F9, which later runs did not reproduce.) | Android | `fix(uno): restore the editor's selection after focusing the proxy text box` |
| The sample had no page with row drag; the All Components page now shows the drag handle and has the MAUI page's Row Drag button. Its theme button said Light while the grid followed a dark app theme. | all | `fix(samples): Uno main page can drag rows and its theme button starts from the shown theme` |

| The filter popup applied every checked value on OK, including values a search had hidden, and its Select All box always showed a check mark. OK now keeps the checked values that match the search, and the box shows checked, empty or indeterminate. | all (Core) | `fix(core): filter popup OK applies the search and Select All shows a mixed state` |
| An open cell editor kept the old theme after the style changed. `EditSession.Style` now re-themes the active editor. | all (Core) | `fix(core): re-theme the open cell editor when the style changes` |

Both host fixes have unit tests in `tests/KumikoUI.Uno.Tests/KumikoTextInputAdapterTests.cs` (the fake text box now
raises its changes the way Uno's does).

## Follow-ups

Each open item has an issue; repro steps are against `samples/SampleApp.Uno`.

- **F1. Desktop head needs a hands-on pass.** ([#274](https://github.com/TheEightBot/KumikoUI/issues/274)) Launch `dotnet run --project samples/SampleApp.Uno -f net10.0-desktop`
  on macOS, Windows and Linux and walk every row of the matrix. Nothing in this run suggests a problem, but no input
  reached the macOS app. *Partly done on 2026-11-17:* Linux walked every row a container allows, Windows the rows up
  to the filter popup (notes 18 to 26). Still open: every interactive row on macOS (the app has to be in front, note
  3), the rest of Windows (note 23), the Windows App SDK head (note 20), and IME, theme follow and DPI change on a
  real desktop.
- **F2. Android: the filter popup's search box gets no soft keyboard.** *Not reproduced on 1.0.0-preview.1*
  ([#275](https://github.com/TheEightBot/KumikoUI/issues/275)). On the Android emulator and the iOS simulator the
  keyboard appears when the popup opens and again when the search box is tapped after the keyboard was dismissed,
  and typing filters the list. A defensive change repeats the explicit show once the press has settled
  (`SoftKeyboardPolicy`, tests in `SoftKeyboardPolicyTests`). Re-check on a physical device and with other keyboards
  (Samsung, SwiftKey).
- **F3. Touch column reorder on Android.** *Not a bug on the emulator; physical device pending*
  ([#276](https://github.com/TheEightBot/KumikoUI/issues/276)). With `adb shell input motionevent` touch input the
  header drag starts and the column moves. What looked like nothing was a hold of 500 ms or more (opens the sort
  menu, #288), a drag across the frozen/unfrozen boundary, or a press at a header's right edge (resize, #289). Tests:
  `TouchColumnReorderTests`. Confirm with a real finger on a physical device.
- **F4. macOS desktop: the grid is missing from the accessibility tree.** ([#277](https://github.com/TheEightBot/KumikoUI/issues/277)) Launch the desktop head, inspect with
  Accessibility Inspector or VoiceOver: buttons and navigation items are there, the grid is not. The same peer is
  exposed on WebAssembly and Android, so the gap is in how Uno's macOS host maps `KumikoDataGridAutomationPeer`.
  *Cause found (2026-11-17), in Uno, not KumikoUI.* Uno 6.7.135's macOS bridge maps the peer correctly: on the first
  page shown, the Release build has an `AXTable` named by `GridDescription`, with `GridHint`'s text as its help and a
  settable `AXFocused`, and the peer's help-text changes and notifications reach the element and VoiceOver's
  announcements. What loses the grid is the bridge's handling of elements added later:
  - The bridge never registers an element whose `AccessibilityView` is Raw; it attaches that element's children to
    the nearest registered ancestor while it builds the tree. A child added *after* the tree is built under a Raw
    parent is sent to the native side with the Raw parent's handle, which the native side does not know, and
    `uno_accessibility_add_element` then makes that child the root of the window's tree. VoiceOver is left with one
    stray element: no buttons, no navigation items, no grid.
  - Any page shown after the first adds such children: the grid's hidden keyboard proxy (a Raw `TextBox`) and the Raw
    `ContentElement` scroll viewer of the `TextBox` template build their parts on the first layout, and so do a
    `ScrollBar`'s Raw thumb and a `ToggleSwitch`'s Raw thumb in the sample's toolbars. Seen on the Grouping and Large
    Data pages: after navigating, the window's tree was a 1×1 element inside the grid's proxy, or a scroll bar thumb.
  - In a Debug build the sample calls `UseStudio()` (Hot Design), whose elements do the same at launch, so
    `dotnet run` shows an empty tree from the start.
  KumikoUI cannot avoid it: applying the proxy's templates in the grid's constructor was tried and changed nothing
  (the template parts were still added once the grid was in the window), and without Raw the proxy would be exposed as an empty text field while its
  template's Raw scroll viewer still triggers the bug. The fix belongs in Uno's macOS bridge (attach a late child to
  the nearest registered ancestor, and never replace the root with an element whose parent is unknown); the report
  for Uno is ready to file. Until it is fixed, VoiceOver reaches the grid only on the page the app opens with, in a
  Release build. Windows exposes the grid through UI Automation (note 26); Linux exposes nothing (F12).
- **F5. Combo box keys and list drawing.** *Resolved, no code change.* Picking an item commits the edit, and Enter on
  the cell then opens the editor again by design (Enter is an edit trigger like F2). The list overdraw was not
  reproduced in Core; guard tests were added. Re-check on a device if it shows again, with a screenshot of the
  popup edge.
- **F6. Filter popup search and Select All.** *Fixed in Core* (see above).
- **F7. An open editor keeps the old theme.** *Fixed in Core* (see above); re-checked on the iOS simulator
  (iPhone 17 Pro, Debug): with a text editor open, switching the system appearance from light to dark restyled the
  open editor (dark background, light text) without closing it. Not re-run on Android.
- **F8. Android Release cold start is slow.** *Closed: not reproduced*
  ([#278](https://github.com/TheEightBot/KumikoUI/issues/278)). The earlier 15 to 20 seconds did not reproduce on
  `1.0.0-preview.1`. On the `KumikoUI-MAUI-Android` AVD (Android 35 google_apis arm64-v8a, 4 cores, 2 GB RAM, Apple
  Silicon host, other emulators running at the same time) the Uno sample's Release build (default profiled AOT,
  Uno.Sdk 6.7.30) started in a median of 2.16 s (`am start -W` TotalTime, 7 force-stop cold starts, range 1.5 to
  3.5 s), with the first grid frame about 1.85 s after launch (range 1.3 to 3.2 s). The MAUI sample's Release build
  on the same emulator started in a median of 1.13 s (range 0.65 to 2.07 s), first grid frame about 0.96 s after
  process start. The extra second is Uno runtime startup (about 0.6 to 0.9 s to `OnLaunched`) and the NavigationView
  shell (about 0.35 s); KumikoUI's own share (font registration about 15 ms, grid page construction and data about
  70 ms, first paint a few hundred ms) is small. The sample logs startup markers under the logcat tag
  `KumikoStartup` (`StartupTrace`). No physical device was measured.
- **F9. iOS: typing over a selected cell value goes in front of it.** *Closed: not reproduced on the released code*
  ([#279](https://github.com/TheEightBot/KumikoUI/issues/279)). This pass saw it on the iOS simulator: on the All
  Components page, double-tap "Eve Davis" (the editor selects all of it) and type "Kim Lee", and the cell read
  "Kim LeeEve Davis". The suspected cause was the native `UITextField` behind Uno's proxy `TextBox`, whose selection
  could fall out of step with the managed one. Later runs on `1.0.0-preview.1` (Debug and Release, iPhone 17 and the
  `KumikoUI-Uno-iPhone` simulator, typed text and on-screen keys, first and later edits) replaced the value every
  time, and logging showed the native field's selection matched the editor's once the edit opened. The grid's
  re-sync after focusing the proxy (`fix(uno): restore the editor's selection after focusing the proxy text box`)
  reaches the native field through `TextBox.Select`.
- **F10. Sample shell under the system bars on iOS and Android.** *Fixed in the sample*
  ([#280](https://github.com/TheEightBot/KumikoUI/issues/280)). `VisibleBoundsPadding` on the `NavigationView` had no
  effect on iOS, and padding on the `Page` did not apply either. The shell now wraps the `NavigationView` in a `Grid`
  whose padding comes from `ApplicationView.VisibleBounds`, on iOS and Android only. The minimal pane's menu button
  still covers the start of the page title on phones (#291).
- **F11. A mouse press with no named button is taken as a primary press.** ([#286](https://github.com/TheEightBot/KumikoUI/issues/286)) On Uno's X11 host a wheel
  notch that arrives as an XInput2 press of button 4 to 7 reaches the grid as a pointer press whose update kind is
  `Other` and with no button pressed; `PointerMapping.ToPointerButton` maps that to `PointerButton.Primary`, so the
  notch selects the cell under the pointer and two notches open its editor (note 21). A mouse press that names no
  button should be ignored.
- **F12. Linux: the app is not in the AT-SPI tree.** ([#287](https://github.com/TheEightBot/KumikoUI/issues/287)) Uno 6.7's X11 host has no AT-SPI bridge, so
  Orca sees nothing of the app, the grid included (note 27). Nothing in KumikoUI can expose it; track the Uno side
  and document the limit.

## Recommendation: ship KumikoUI.Uno as 1.0.0-preview

Ship the Uno package as `1.0.0-preview` alongside the 1.0.0 Core, SkiaSharp and MAUI packages, and move it to 1.0.0
once the follow-ups above are closed and the matrix is green on every head.

**Outcome:** the packages ship in lockstep with exact sibling pins (#112), so an Uno-only preview version is not
possible without splitting the versioning. The first 1.0 release is therefore `1.0.0-preview.1` for all four packages.
KumikoUI.Uno stays the least verified host until the issues above are closed.

- **Text entry on the two mobile heads was broken until this pass** (an app crash on Android, lost and misplaced
  characters on iOS) and nothing had caught it: the unit tests modeled a text box that behaves better than Uno's.
  Android is fixed and has had one emulator run; iOS replaces a selected value in later simulator runs (F9 is closed).
- **The desktop head has had no interactive run on macOS** (F1). Linux (X11) passed every row a container can check
  and Windows the rows it got to before the pass stopped; one input bug turned up there (F11). Desktop is the head
  most Uno apps ship first.
- **Accessibility is uneven:** Windows exposes the grid through UI Automation, macOS only until the user navigates
  (F4), and Linux not at all (F12); both gaps are in Uno's desktop hosts. The filter search cannot be typed into on
  Android (F2).
- **What is solid:** rendering on every head that ran, the trimmed WebAssembly build CI produces (so the
  `continue-on-error` on that job can go), scrolling and momentum, selection, keyboard navigation and Tab exit,
  keyboard avoidance on iOS and Android, theme following, grouping, filtering, column and row drag where exercised,
  100,000-row data in the browser, and fonts.

A preview label tells users the package works and what to expect, without promising the stability guarantees that
1.0.0 carries in [VERSIONING.md](../VERSIONING.md) for heads that have not been walked end to end.

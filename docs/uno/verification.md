# Uno device verification (#67)

Every head of `samples/SampleApp.Uno` that runs on the verification Mac, exercised by hand (through automation that
drives real input) against the checklist in issue #67. Run on 2026-11-13 on `integration/1.0-wave1` plus the fixes
listed under [Fixed during the pass](#fixed-during-the-pass); everything marked ✅ for WebAssembly and Android was
re-checked on the merged code.

**Legend:** ✅ works · ⚠️ works with a problem (see the note) · ❌ broken (follow-up filed below) · ◻ not exercised
(the reason is in the note) · n/a not applicable to the head.

## Environment

| Head | Where it ran | How it was driven |
|---|---|---|
| WebAssembly | `dotnet publish -f net10.0-browserwasm -c Release -p:PublishTrimmed=true -p:TrimMode=full` (exactly as CI), served with `python3 -m http.server`, opened in Chromium (Claude Browser pane), 1280×800, light and dark color scheme | Chrome DevTools Protocol mouse, wheel and keyboard input |
| Android | `net10.0-android` Release APK on a new emulator AVD `KumikoUI-Uno-Android` (Pixel 7 profile, Android 15 / API 35 Google APIs arm64, 4 GB RAM, 8 GB data partition), Gboard | `adb shell input` taps, swipes and soft-keyboard key taps (so typing goes through Gboard's composing text) |
| iOS | `net10.0-ios` Release build for `iossimulator-arm64` on a new simulator `KumikoUI-Uno-iPhone` (iPhone 17 Pro, iOS 26.5) | Simulator touch injection (taps, swipes) and the simulator keyboard |
| Desktop (macOS) | `net10.0-desktop` Release, launched from an app bundle wrapper, macOS 27.2, dark appearance | Screenshots and the macOS accessibility tree only (see the Desktop note) |
| Desktop (Windows, Linux), Windows App SDK | not run (no machine) | — |

Screenshots of the run were kept outside the repository (they are large and machine-specific); the final report of
this work lists where.

## Matrix

| Check | Desktop (macOS) | WebAssembly (Chromium) | iOS simulator | Android emulator | Desktop Windows / Linux, WinAppSDK |
|---|:--:|:--:|:--:|:--:|:--:|
| Render on launch (grid, frozen rows/columns, summaries, sample shell) | ✅ | ✅ ¹ | ⚠️ ² | ⚠️ ² | not run (no machine) |
| Touch scroll and fling (momentum) | n/a | n/a | ✅ | ✅ | not run (no machine) |
| Mouse wheel / trackpad, vertical and horizontal | ◻ ³ | ✅ | n/a | n/a | not run (no machine) |
| Selection (click/tap, current cell) | ◻ ³ | ✅ | ✅ | ✅ | not run (no machine) |
| Editing: text (begin with double click/tap, F2, typing; commit with Enter) | ◻ ³ | ✅ | ❌ ⁴ (typing over a selection) | ✅ after fix ⁴ | not run (no machine) |
| Editing: IME / composition | ◻ ³ | ◻ ⁵ | ◻ ⁵ | ✅ after fix ⁴ (Gboard composing text, suggestions) | not run (no machine) |
| Editing: combo box | ◻ ³ | ✅ ⁶ | ◻ | ◻ | not run (no machine) |
| Editing: date picker | ◻ ³ | ✅ | ◻ | ◻ | not run (no machine) |
| Keyboard navigation (arrows, Tab between cells) | ◻ ³ | ✅ | n/a | n/a | not run (no machine) |
| Tab out of the grid (no keyboard trap) | ◻ ³ | ✅ | n/a | n/a | not run (no machine) |
| Filter popup (open, check list, search, OK) | ◻ ³ | ✅ after fix ⁷ | ◻ | ⚠️ ⁸ | not run (no machine) |
| Column resize (drag the header border) | ◻ ³ | ✅ | ◻ | ✅ | not run (no machine) |
| Column reorder (drag a header) | ◻ ³ | ✅ | ◻ | ◻ ⁹ | not run (no machine) |
| Row drag (handle) | ◻ ³ | ◻ ¹⁰ | ◻ ¹⁰ | ✅ (after the sample fix) | not run (no machine) |
| Grouping (group buttons, chips, remove chip, expand/collapse) | ◻ ³ | ✅ | ◻ | ◻ | not run (no machine) |
| Theme switch: sample button (Light → Dark → High Contrast) | ◻ ³ | ✅ | ◻ | ◻ | not run (no machine) |
| Theme switch: follows the system light/dark change live | ✅ (dark at launch) | ✅ | ◻ | ✅ after fix ¹¹ | not run (no machine) |
| DPI / scale change | ◻ ¹² | ◻ ¹² | n/a | n/a | not run (no machine) |
| Keyboard avoidance (edited bottom row stays above the soft keyboard) | n/a | n/a | ✅ | ✅ | n/a |
| Hover feedback (headers, chips, chevrons) | ◻ ³ | ✅ | n/a | n/a | not run (no machine) |
| Pressed feedback and transitions (group slide, header lift/drop, selection fade) | ◻ ³ | ◻ ¹³ | ◻ ¹³ | ◻ ¹³ | not run (no machine) |
| Large data (100,000 rows: load, scroll) | ◻ | ✅ (load 514 ms, 3.8 ms frames) | ◻ | ◻ | not run (no machine) |
| CJK and icon fonts (Custom Fonts page) | ◻ | ✅ | ◻ | ◻ | not run (no machine) |
| Screen reader smoke test | ❌ ¹⁴ | ✅ ¹⁵ | ◻ | ✅ ¹⁶ | not run (no machine) |

### Notes

1. **WebAssembly, trimmed build:** the CI publish (`PublishTrimmed=true`, `TrimMode=full`) builds and runs: every
   page loads and every check in the WebAssembly column was made on it. Two console errors are unrelated to the
   grid: the service worker registration fails under the local `python3 -m http.server`, and before the fix in
   [Fixed during the pass](#fixed-during-the-pass) an `ArgumentOutOfRangeException` was logged on most keystrokes.
   Cell text uses a monospaced fallback face in the browser (the grid's default typeface has no system font to
   resolve to there); this is legible but differs from the other heads.
2. **iOS and Android, sample shell:** the window is drawn edge to edge, and the sample page's title and toolbar
   run under the status bar and the bottom row under the home indicator / gesture bar. The grid itself draws
   correctly; the sample shell needs to respect the visible bounds (follow-up F10, #280).
3. **Desktop (macOS) interaction:** the app renders, follows the dark appearance and exposes its XAML buttons to
   the accessibility tree, but pointer and keyboard input could not be delivered to it in this run (background
   automation only reaches accessibility actions, and full-screen control was not granted). Every interactive row
   is therefore unverified on the desktop head. The desktop head shares all of its input code with WebAssembly
   except the platform host itself; it needs a hands-on pass (follow-up F1, #274).
4. **Typing on iOS and Android was broken before this pass:** typing over a selected value inserted in front of
   it ("XBob Jones"), and typing at the end of a value threw inside Uno; on Android that exception crashed the app
   from the input connection, on iOS characters were lost ("Kim Lee" became "iK" + the old text). Both are fixed
   on Android and re-checked there on the merged code. On iOS the merged code types every character in order
   ("Kim Lee" arrives whole), but typing over a selected value still goes in front of it (follow-up F9, #279).
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
8. **Filter popup on Android:** opening it and the check list work, but tapping the search box never shows the soft
   keyboard, so the search cannot be typed into on a phone (follow-up F2, #275).
9. **Column reorder by touch (Android):** two slow header drags with `adb input swipe` started neither a column drag
   nor a pan; mouse reorder works on WebAssembly. Needs a check with a real finger (follow-up F3, #276).
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
    no element for the grid, so VoiceOver cannot reach it (follow-up F4, #277).
15. **Screen reader on WebAssembly:** with Uno's "Enable accessibility" semantic DOM on, the grid is a `role="grid"`
    element named by `GridDescription` whose description follows the current cell ("200 rows, 11 columns, current
    cell row 7 column 2"). No screen reader was run against it.
16. **TalkBack on Android:** the accessibility tree TalkBack reads (`uiautomator dump`) has the grid as one focusable
    view named "Employee data grid with all component types"; TalkBack itself was not driven.

## Fixed during the pass

| Fix | Heads | Commit |
|---|---|---|
| Typing at the end of a cell editor or the filter search box threw `ArgumentOutOfRangeException` from `TextBox.Select`: the grid wrote its hidden text box back while that text box was still raising `TextChanging`. Crashed the app on Android, logged on most WebAssembly keystrokes, lost characters on iOS. The grid now syncs the text box right after the change. | Android, iOS, WebAssembly (and desktop, same code path) | `fix(uno): do not write the proxy text box while it raises TextChanging` |
| Typing over a selected value inserted in front of it on Android: focusing the hidden text box moved its caret to the start. The grid puts the editor's selection back after focusing it. (iOS has the same symptom from a different cause: F9.) | Android | `fix(uno): restore the editor's selection after focusing the proxy text box` |
| The sample had no page with row drag; the All Components page now shows the drag handle and has the MAUI page's Row Drag button. Its theme button said Light while the grid followed a dark app theme. | all | `fix(samples): Uno main page can drag rows and its theme button starts from the shown theme` |

| The filter popup applied every checked value on OK, including values a search had hidden, and its Select All box always showed a check mark. OK now keeps the checked values that match the search, and the box shows checked, empty or indeterminate. | all (Core) | `fix(core): filter popup OK applies the search and Select All shows a mixed state` |
| An open cell editor kept the old theme after the style changed. `EditSession.Style` now re-themes the active editor. | all (Core) | `fix(core): re-theme the open cell editor when the style changes` |

Both host fixes have unit tests in `tests/KumikoUI.Uno.Tests/KumikoTextInputAdapterTests.cs` (the fake text box now
raises its changes the way Uno's does).

## Follow-ups

Each open item has an issue; repro steps are against `samples/SampleApp.Uno`.

- **F1. Desktop head needs a hands-on pass.** ([#274](https://github.com/TheEightBot/KumikoUI/issues/274)) Launch `dotnet run --project samples/SampleApp.Uno -f net10.0-desktop`
  on macOS, Windows and Linux and walk every row of the matrix. Nothing in this run suggests a problem, but no input
  reached the macOS app.
- **F2. Android: the filter popup's search box gets no soft keyboard.** ([#275](https://github.com/TheEightBot/KumikoUI/issues/275)) All Components page, tap the Department
  filter icon, tap the search box: the keyboard never appears (`dumpsys input_method` shows `mInputShown=false`), so
  nothing can be typed. Cell editors do get the keyboard. Core raises `KeyboardFocusRequested` and the host calls
  `InputPane.TryShow()` because the proxy already has focus; that does not show the keyboard on Uno's Android Skia
  head. Check iOS as well.
- **F3. Touch column reorder on Android.** ([#276](https://github.com/TheEightBot/KumikoUI/issues/276)) All Components page, press and slowly drag the Salary header to the left:
  no column drag starts (mouse drag works on WebAssembly). Confirm on a device with a real finger before treating
  it as a bug.
- **F4. macOS desktop: the grid is missing from the accessibility tree.** ([#277](https://github.com/TheEightBot/KumikoUI/issues/277)) Launch the desktop head, inspect with
  Accessibility Inspector or VoiceOver: buttons and navigation items are there, the grid is not. The same peer is
  exposed on WebAssembly and Android, so the gap is in how Uno's macOS host maps `KumikoDataGridAutomationPeer`.
- **F5. Combo box keys and list drawing.** *Resolved, no code change.* Picking an item commits the edit, and Enter on
  the cell then opens the editor again by design (Enter is an edit trigger like F2). The list overdraw was not
  reproduced in Core; guard tests were added. Re-check on a device if it shows again, with a screenshot of the
  popup edge.
- **F6. Filter popup search and Select All.** *Fixed in Core* (see above).
- **F7. An open editor keeps the old theme.** *Fixed in Core* (see above); re-check on the Android emulator.
- **F8. Android Release cold start is slow.** ([#278](https://github.com/TheEightBot/KumikoUI/issues/278)) About 15 to 20 seconds from launch to the first grid on the emulator;
  measure on a device.
- **F9. iOS: typing over a selected cell value goes in front of it.** ([#279](https://github.com/TheEightBot/KumikoUI/issues/279)) iOS simulator, All Components, double-tap
  "Eve Davis" (the editor selects all of it), type "Kim Lee": the cell reads "Kim LeeEve Davis". The managed proxy
  `TextBox` has the right selection, but Uno's iOS host keeps a native `UITextField` behind it whose own selection is
  not updated: `InvisibleTextBoxViewExtension.StartEntry` copies the selection only when it creates that field, and
  `TextBox.Select` ignores a selection that did not change, so re-syncing from the grid does not reach it. Re-selecting
  through a different selection after focus was tried in this pass and made iOS drop characters, so it was not kept.
  Needs a fix in the host that reaches the native field after it becomes first responder, or an Uno fix.
- **F10. Sample shell under the system bars on iOS and Android.** ([#280](https://github.com/TheEightBot/KumikoUI/issues/280)) The page title and toolbar run under the status bar
  and the last row under the home indicator / gesture bar. `VisibleBoundsPadding` on the `NavigationView` had no
  effect on iOS; the shell needs a padded container (or the safe-area insets applied by hand).

## Recommendation: ship KumikoUI.Uno as 1.0.0-preview

Ship the Uno package as `1.0.0-preview` alongside the 1.0.0 Core, SkiaSharp and MAUI packages, and move it to 1.0.0
once the follow-ups above are closed and the matrix is green on every head.

**Outcome:** the packages ship in lockstep with exact sibling pins (#112), so an Uno-only preview version is not
possible without splitting the versioning. The first 1.0 release is therefore `1.0.0-preview.1` for all four packages.
KumikoUI.Uno stays the least verified host until the issues above are closed.

- **Text entry on the two mobile heads was broken until this pass** (an app crash on Android, lost and misplaced
  characters on iOS) and nothing had caught it: the unit tests modeled a text box that behaves better than Uno's.
  Android is fixed and has had one emulator run; iOS still types in front of a selected value (F9).
- **The desktop head has had no interactive run at all** (F1), and Windows and Linux have none either. Desktop is the
  head most Uno apps ship first.
- **Accessibility is uneven** (F4: VoiceOver cannot reach the grid on macOS), and the filter search cannot be typed
  into on Android (F2).
- **What is solid:** rendering on every head that ran, the trimmed WebAssembly build CI produces (so the
  `continue-on-error` on that job can go), scrolling and momentum, selection, keyboard navigation and Tab exit,
  keyboard avoidance on iOS and Android, theme following, grouping, filtering, column and row drag where exercised,
  100,000-row data in the browser, and fonts.

A preview label tells users the package works and what to expect, without promising the stability guarantees that
1.0.0 carries in [VERSIONING.md](../VERSIONING.md) for heads that have not been walked end to end.

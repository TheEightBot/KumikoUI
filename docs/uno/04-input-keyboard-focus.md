# 04 — Input, Keyboard & Focus

Goal: translate Uno/WinUI pointer, keyboard, and focus events into the **existing** Core input API.
Core already owns all behavior; the host only adapts event shapes. No Core changes.

Core entry points (unchanged):
`GridInputController.HandlePointer(GridPointerEventArgs, scroll, selection, style, dataSource)`,
`HandleKey(GridKeyEventArgs, …)`, `UpdateInertialScroll(scroll, 16f)`.

> **Status:** Implemented for `net9.0-desktop`. Wiring lives in
> `src/KumikoUI.Uno/DataGridView.Input.cs` (a partial of `DataGridView`); the pure enum mapping is in
> `src/KumikoUI.Uno/Input/InputMapping.cs`. The three Phase-03 Core→platform stubs
> (`OnKeyboardFocusRequested`, `OnFilterPopupOpened`, `OnFilterPopupClosed`) are implemented in
> `DataGridView.cs`. Build verified: `dotnet build … -f net9.0-desktop` → **0 errors**.
> Behavioral/visual verification is deferred to Phase 06 (sample) / Phase 07 (tests), per scope.

---

## 1. Pointer input

WinUI delivers pointer events on any `UIElement`; subscribe on the `DataGridView` (or its
`SKXamlCanvas`). `GetCurrentPoint(this).Position` is already in **logical DIPs** — the same coordinate
space the renderer was fed in [03 §4](03-datagridview-host.md#4-dpi--scaling), so hit-testing lines up.

> **Deviation (subscription target):** handlers are attached on the `DataGridView` itself (a
> `Grid`), **not** the child `SKXamlCanvas`. The control is the focusable element and the canvas
> `Stretch`-fills it, so coordinates and capture are identical, and a single attach/detach point
> keeps lifecycle clean.

- [x] Subscribe (attach in `OnLoaded` via `AttachInputHandlers`, detach in `OnUnloaded` via
  `DetachInputHandlers`):
  `PointerPressed`, `PointerMoved`, `PointerReleased`, `PointerCanceled`, `PointerWheelChanged`,
  `PointerExited` — **plus `PointerCaptureLost`** (treated as cancel; preserves fling momentum if a
  pan was in flight). Both attach and detach are idempotent (`-=` then `+=`) so Uno re-raising
  `Loaded` on navigation-back never double-subscribes.
- [x] Capture the pointer on press so drags (column resize/reorder, selection, scroll) keep tracking
  outside the element:
  ```csharp
  private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
  {
      CapturePointer(e.Pointer);
      Focus(FocusState.Programmatic);               // so keyboard works (see §3)
      // … press-timing double-tap detection …
      DispatchPointer(e, e.GetCurrentPoint(this), InputAction.Pressed, clickCount);
  }
  ```
- [x] Build `GridPointerEventArgs` (mirrors MAUI's `OnCanvasTouch`, just a different source type):
  ```csharp
  private void DispatchPointer(PointerRoutedEventArgs e, PointerPoint point, InputAction action, int clickCount = 1)
  {
      var props = point.Properties;
      var evt = new GridPointerEventArgs
      {
          X = (float)point.Position.X,
          Y = (float)point.Position.Y,
          Action = action,
          Button = props.IsRightButtonPressed ? PointerButton.Secondary
                 : props.IsMiddleButtonPressed ? PointerButton.Middle
                 : PointerButton.Primary,
          ScrollDeltaY = action == InputAction.Scroll ? props.MouseWheelDelta / WheelNotch : 0f,   // WheelNotch = 120f
          ScrollDeltaX = action == InputAction.Scroll && props.IsHorizontalMouseWheel
                       ? props.MouseWheelDelta / WheelNotch : 0f,
          Modifiers = InputMapping.ToInputModifiers(e.KeyModifiers),
          TimestampMs = (long)(point.Timestamp / 1000),   // WinUI timestamp is microseconds
          ClickCount = clickCount,
      };
      _inputController.HandlePointer(evt, _scroll, _selection, _style, _dataSource);
      e.Handled = evt.Handled;
  }
  ```
  > **Note:** `PointerRoutedEventArgs.KeyModifiers` **is** implemented on the Skia head (real backing
  > field), so pointer-event modifiers are read directly off the args — unlike `KeyRoutedEventArgs`
  > (see §2). `ScrollDeltaX` is populated only when `IsHorizontalMouseWheel` is set.
- [x] **Hover vs. drag (desktop fix):** WinUI/Uno raises `PointerMoved` on every mouse hover, even
  with no button pressed. Core's `GridInputController.HandlePointerMove` has no concept of pointer
  contact — it assumes `Moved` only arrives while a button or finger is down (the MAUI
  `SKCanvasView` contract). Without a guard, plain hover reaches Core and is mis-read as a pan/drag.
  Fix: `OnPointerMoved` checks `PointerPoint.IsInContact` and returns early when `false`. Real drags
  (column resize/reorder, selection, pan-scroll) and touch drags all occur while in contact, so they
  continue to receive `Moved` unaffected. Correspondingly, `DispatchPointer` now reports
  `PointerButton.None` (instead of falling through to `Primary`) when no button is pressed, so any
  stray non-contact event that somehow reaches `DispatchPointer` cannot be mis-read as a left-drag.
- [x] Map the action per event: `PointerPressed→Pressed`, `PointerMoved→Moved`,
  `PointerReleased→Released` (+ `ReleasePointerCapture`), `PointerCanceled`/`PointerCaptureLost→Cancelled`,
  `PointerWheelChanged→Scroll`. The fling timer is (re)started after `Released`/`Cancelled`/
  `CaptureLost` when `_inputController.IsInertialScrolling`.
- [x] **Wheel magnitude:** WinUI `MouseWheelDelta` is in 120-unit notches. We divide by **`WheelNotch =
  120f`** so one notch ⇒ `ScrollDeltaY = 1.0`; Core's `HandleScroll` then multiplies by
  `RowHeight * ScrollSettings.WheelScrollMultiplier`, i.e. **one notch scrolls
  `WheelScrollMultiplier` rows** — matching the MAUI feel (where `SKTouch.WheelDelta` is already a
  small per-notch value fed straight in). The factor lives in one place and is easy to retune.
- [x] **Double-tap / click-count:** WinUI does not surface a click count on pointer args. We use the
  **press-timing heuristic** (`< 400 ms` and `< 20 px` from the previous press → `ClickCount = 2`),
  stamped on `Pressed` and carried to the matching `Released` — byte-for-byte the MAUI approach.
  > **Deviation:** we deliberately do **not** use the WinUI `DoubleTapped` routed event for the click
  > count: it fires *after* the second press/release pair (too late to stamp that release) and would
  > mis-count a subsequent third click. The press-timing path is the single source of truth.
- [x] **Long-press (touch):** handled via WinUI `Holding` (`HoldingState.Started`) → dispatch
  `InputAction.LongPress`. Gated to **touch only** (`PointerDeviceType.Touch`) and `IsHoldingEnabled
  = true`; mouse/pen long-press is unnecessary because right-click already maps to
  `PointerButton.Secondary`. `Holding` carries no timestamp, so a `Stopwatch`-derived microsecond
  value is supplied to keep `TimestampMs` consistent.

## 2. Modifier mapping

- [x] `InputModifiers` is a `[Flags]` enum (`Shift`/`Control`/`Alt`/**`Meta`**). Mapping from WinUI
  `VirtualKeyModifiers` is the **pure** `InputMapping.ToInputModifiers`:
  ```csharp
  public static InputModifiers ToInputModifiers(VirtualKeyModifiers mods)
  {
      var r = InputModifiers.None;
      if ((mods & VirtualKeyModifiers.Shift)   != 0) r |= InputModifiers.Shift;
      if ((mods & VirtualKeyModifiers.Control) != 0) r |= InputModifiers.Control;
      if ((mods & VirtualKeyModifiers.Menu)    != 0) r |= InputModifiers.Alt;   // Menu == Alt
      if ((mods & VirtualKeyModifiers.Windows) != 0) r |= InputModifiers.Meta;  // Windows/Cmd
      return r;
  }
  ```
  > **Exact member names:** WinUI `VirtualKeyModifiers` = `None`/`Control`/`Menu`/`Shift`/`Windows`;
  > Core `InputModifiers` = `None`/`Shift`/`Control`/`Alt`/`Meta`. `Menu→Alt`, `Windows→Meta`.
- [x] **Reading modifiers in `KeyDown`/`CharacterReceived`.**
  > **Major deviation from the spec's first suggestion:** `KeyRoutedEventArgs.KeyboardModifiers` is
  > marked **`[Uno.NotImplemented]`** on the Skia/WASM heads (the Uno source generator strips it from
  > the public API surface there → it does **not** compile: `CS1061`). So we use the spec's *fallback*
  > path — live key state via `InputMapping.GetLiveModifiers()`, which calls
  > `InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey)` for Shift/Control/Menu/Win and
  > tests `CoreVirtualKeyStates.Down`. This is also the **Microsoft-recommended** WinUI 3 way to read
  > modifiers when a non-modifier key is pressed (`CoreWindow.GetKeyState` returns null in WinUI 3).
  > Verified the Uno Skia `GetKeyStateForCurrentThread` is a real delegating implementation (forwards
  > to `Uno.UI.Core.KeyboardStateTracker.GetKeyState`), **not** a `TryRaiseNotImplemented` stub like
  > the instance `GetKeyState`/`GetCurrentKeyState` overloads. `GetLiveModifiers()` is wrapped in a
  > defensive `try/catch (NotImplementedException)` returning `None`, so a keystroke can never throw
  > on a head that lacks the lookup.

## 3. Keyboard & focus

The control must be focusable to receive key events.

- [x] In `AttachInputHandlers` (called from `OnLoaded`): `IsTabStop = true;` and a default
  `TabIndex = 0` (only set if still at the WinUI default `int.MaxValue`, so a XAML override is
  respected). Focus the control on pointer press (above) and on edit-begin, and wire Core's request:
  ```csharp
  private void OnKeyboardFocusRequested() => Focus(FocusState.Programmatic);
  ```
  > **Note:** `IsTabStop` / `TabIndex` / `Focus(FocusState)` are defined on **`UIElement`** in
  > WinUI/Uno (not `Control` as in WPF), so they are available on this `Grid`-derived control.
  > There is **no hidden `Entry`** as in MAUI; the focused, editable Skia surface is the input sink,
  > and WinUI surfaces the soft keyboard / input pane for it automatically.
- [x] **Navigation / command keys** — `KeyDown` → map `VirtualKey` → `GridKey` → `HandleKey`:
  ```csharp
  private void OnKeyDown(object sender, KeyRoutedEventArgs e)
  {
      var key = InputMapping.ToGridKey(e.Key);          // VirtualKey → GridKey
      if (key == GridKey.None) return;                  // printable text → CharacterReceived
      var evt = new GridKeyEventArgs { Key = key, Modifiers = InputMapping.GetLiveModifiers(), IsKeyDown = true };
      _inputController.HandleKey(evt, _scroll, _selection, _style, _dataSource);
      if (evt.Handled) e.Handled = true;
  }
  ```
  `InputMapping.ToGridKey` (pure) covers all `GridKey` command members:
  arrows (`Up`/`Down`/`Left`/`Right`), `Home`/`End`, `PageUp`/`PageDown`, `Tab`, `Enter`,
  `Escape`, `Space`, `Delete`, **`Back → Backspace`**, `F2`, and letters **`A`/`C`/`V`/`X`/`Z`**
  (select-all / clipboard / undo). Everything else → `GridKey.None`.
- [x] **Text entry (cell editing)** — uses `CharacterReceived` instead of MAUI's hidden-`Entry`
  proxy. It yields the resolved character (respecting IME/dead keys):
  ```csharp
  private void OnCharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs e)
  {
      var evt = new GridKeyEventArgs { Key = GridKey.None, Character = e.Character,
                                       Modifiers = InputMapping.GetLiveModifiers(), IsKeyDown = true };
      _inputController.HandleKey(evt, _scroll, _selection, _style, _dataSource);
      if (evt.Handled) e.Handled = true;
  }
  ```
  > **Deviation from the spec snippet:** we do **not** early-return on `!_editSession.IsEditing`.
  > Core's `HandleKey` is the authority on whether a printable char *starts* an edit (the `Typing`
  > edit-trigger path in `GridInputController`), so forwarding every character — with live modifiers
  > so Ctrl/Alt combos are correctly *excluded* from typing — lets type-to-edit work, not just
  > type-while-already-editing. `CharacterReceivedRoutedEventArgs.Character` is a non-nullable `char`.

## 4. Inertial scroll timer

- [x] A `Microsoft.UI.Xaml.DispatcherTimer` (~16 ms) pumps Core's physics and stops when it returns
  `false` (mirrors MAUI's `_scrollTimer`):
  ```csharp
  private DispatcherTimer? _scrollTimer;
  private void StartInertialScrollTimer()
  {
      if (_scrollTimer != null) return;
      _scrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
      _scrollTimer.Tick += OnScrollTimerTick;
      _scrollTimer.Start();
  }
  private void OnScrollTimerTick(object? s, object e)
  {
      if (!_inputController.UpdateInertialScroll(_scroll, 16f)) StopInertialScrollTimer();
      _canvasView.Invalidate();   // keep the surface in lockstep, incl. the final stopping tick
  }
  ```
  > **Note:** the WinUI `DispatcherTimer.Tick` handler signature is `(object? sender, object e)`
  > (not `EventArgs`). `Stop` + null-out the field in `StopInertialScrollTimer` so a re-fling
  > re-creates a fresh timer (guard-on-`null` start, exactly like MAUI).
- [x] The same `DispatcherTimer` pattern drives the **cursor-blink** timer (530 ms). It is started on
  `CellBeginEdit` and on `FilterPopupOpened`, invalidates while `IsEditing || _filterPopupActive`,
  and self-stops otherwise; explicitly stopped on `CellEndEdit`/`FilterPopupClosed` (unless the other
  is still active) and on `OnUnloaded`.
  > **Note:** there is **no separate long-press timer** (unlike MAUI's `_longPressTimer`): WinUI's
  > built-in `Holding` gesture provides the long-press, so no host-side timer is needed. The only
  > input-driven timers are the inertial-scroll and cursor-blink ones — **both stopped in
  > `DetachInputHandlers` (from `OnUnloaded`)**, so no timer roots the page across navigation.

## 5. Per-target nuances

| Head | Pointer | Keyboard | Notes |
|---|---|---|---|
| Desktop (Skia) / Windows | mouse + touch + pen | full | Reference behavior; richest input. **Built & wired for `net9.0-desktop`.** |
| WebAssembly | mouse/touch OK | needs focusable element | Element is focusable (`IsTabStop=true`) so `KeyDown`/`CharacterReceived` fire; `GetLiveModifiers` is wrapped in try/catch in case the WASM keyboard-state lookup differs. Verify browser doesn't steal wheel/scroll. |
| Android / iOS | touch only, no hover | soft keyboard | Editing is driven by `CharacterReceived` from the on-screen keyboard; focusing the editable surface on edit-begin surfaces the input pane. `Holding` provides long-press. |
| Mac Catalyst | trackpad + keyboard | full | Like desktop; validate momentum-scroll feel vs. the inertial timer. |

- [ ] Verify selection, keyboard navigation, editing, wheel + inertial scroll, column resize/reorder,
  and row drag on **each** head's smoke test (see [07](07-tests.md)). *(Deferred: behavioral/visual
  verification is Phase 06/07 per this phase's scope; Phase 04's bar is a clean `net9.0-desktop`
  compile with every input path correctly constructed.)*

---

## ✅ Exit criteria

- [x] Pointer + keyboard + focus + inertial/cursor-blink timers are fully wired on `net9.0-desktop`
  and the project **compiles cleanly** (`dotnet build … -f net9.0-desktop` → 0 errors; the only
  warnings are pre-existing `CS1591` doc warnings from `KumikoUI.Core`/`KumikoUI.SkiaSharp`).
  Every input path constructs `GridPointerEventArgs`/`GridKeyEventArgs` correctly and calls the right
  Core method; `VirtualKey→GridKey` mapping is complete; handlers attach on `Loaded` and fully detach
  on `Unloaded`; all timers stop on `Unloaded`.
- [ ] *(Phase 06/07)* Mouse/touch select, scroll (wheel + inertial), resize, reorder, and drag all
  work behaviorally on `net9.0-desktop`.
- [ ] *(Phase 06/07)* Arrow/Tab/Enter/Escape navigation and typed cell editing work via keyboard +
  `CharacterReceived`.
- [ ] *(Phase 06/07)* Pointer coordinates align with drawn cells at all display scales.
- [x] Handlers are attached on `Loaded` and fully detached on `Unloaded` (no leaks across
  navigation); idempotent re-attach on navigation-back.

➡️ Next: [05 — Fonts & assets](05-fonts-and-assets.md)

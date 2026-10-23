# 04 — Input, Keyboard & Focus

Goal: translate Uno/WinUI pointer, keyboard, and focus events into the **existing** Core input API.
Core already owns all behavior; the host only adapts event shapes. No Core changes.

Core entry points (unchanged):
`GridInputController.HandlePointer(GridPointerEventArgs, scroll, selection, style, dataSource)`,
`HandleKey(GridKeyEventArgs, …)`, `UpdateInertialScroll(scroll, 16f)`.

> **Status:** Implemented for all heads (desktop, iOS, Android, WASM). Wiring lives in
> `src/KumikoUI.Uno/DataGridView.Input.cs` (pointer + keyboard + timer, a partial of `DataGridView`),
> `src/KumikoUI.Uno/DataGridView.Editing.cs` (hidden-TextBox proxy — the keyboard sink on all heads;
> see §3a), and `src/KumikoUI.Uno/Input/InputMapping.cs` (pure enum mapping).
> Build verified: `dotnet build … -f net9.0-desktop` → **0 errors, 0 new warnings**;
> `dotnet build … -f net10.0-desktop` (sample) → **0 errors**.
> WASM cannot be built locally (the `wasm-tools` workload is not installed in this environment);
> however, the fix is platform-agnostic — the proxy is a real `TextBox` on every Uno head, and its
> `TextChanged`/`KeyDown` fire reliably there. Actual WASM typing confirmation requires a browser
> environment.

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
      _lastPointerWasTouch = e.Pointer.PointerDeviceType == PointerDeviceType.Touch;
      // … press-timing double-tap detection …
      DispatchPointer(e, e.GetCurrentPoint(this), InputAction.Pressed, clickCount);
      // Focus the proxy for non-touch input (see §3a).
      if (!_lastPointerWasTouch)
          FocusKeyboardInput();
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
- [x] **Reading modifiers in `KeyDown`.**
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

## 3. Keyboard & focus — proxy-as-keyboard-sink design

### Why `CharacterReceived` and `Grid` focus don't work on WASM/Skia

Two independent issues make a plain `Grid`-based keyboard approach fail on WASM (and Skia targets):

1. **`UIElement.CharacterReceived` is `[Uno.NotImplemented]` on Skia/WASM heads.** Uno's source
   generator marks it as a no-op stub there — it simply never fires. Any type-to-edit design that
   relies on `CharacterReceived` is dead on WASM.

2. **`Grid` (a `Panel`) is not reliably keyboard-focusable on WASM.** WinUI/Uno's WASM focus model
   does not guarantee `KeyDown` delivery to a `Panel`-derived element that has `IsTabStop = true`;
   in practice `KeyDown` also fails to fire on WASM for a `Grid` host.

Together these mean that when not editing: navigation keys are dead, and typed characters never reach
Core's Typing trigger (type-to-edit is broken).

### Solution: proxy-as-keyboard-sink at all times for non-touch input

The existing `_inputProxy` (`TextBox`) is a real focusable text element on **every** head. Its
`TextChanged` and `KeyDown` events fire reliably on WASM, Skia-desktop, Windows, iOS, and Android
because Uno maps a WinUI `TextBox` to the platform's native text field on each head. We promote it
from an edit-only sink to the **permanent keyboard sink for non-touch input**:

- **Non-touch press (mouse/pen):** `OnPointerPressed` calls `FocusKeyboardInput()` *after*
  `DispatchPointer` returns, so the proxy holds focus immediately after any click and stays focused
  between interactions. Navigation arrows, command keys, and typed characters are all delivered there.
- **Touch press:** `FocusKeyboardInput()` is deliberately NOT called on touch presses. Focusing a
  `TextBox` on a touch tap would summon the soft keyboard on every tap, not just on edit-begin.
  The edit lifecycle (`CellBeginEdit`) remains the sole path that focuses the proxy on touch.
- **After edit ends (non-touch):** `OnEditSessionCellEndEdit` calls `FocusKeyboardInput()` instead of
  `Focus(FocusState.Programmatic)` on the grid. This keeps the proxy focused, so arrow-key
  navigation and type-to-edit work immediately without requiring another click.
- **After edit ends (touch):** `Focus(FocusState.Programmatic)` on the grid is kept — blurring the
  proxy dismisses the soft keyboard on mobile, which is the correct behavior.

```csharp
// OnPointerPressed (DataGridView.Input.cs)
CapturePointer(e.Pointer);
_lastPointerWasTouch = e.Pointer.PointerDeviceType == PointerDeviceType.Touch;
// … double-tap detection …
DispatchPointer(e, point, InputAction.Pressed, clickCount);
if (!_lastPointerWasTouch)
    FocusKeyboardInput();   // proxy becomes the keyboard sink for this and all subsequent events

// OnEditSessionCellEndEdit (DataGridView.cs)
ResetProxy();
if (_lastPointerWasTouch)
    Focus(FocusState.Programmatic);   // touch: blur proxy → soft keyboard dismisses
else
    FocusKeyboardInput();             // mouse/pen: proxy stays focused → arrows/typing keep working
```

### Why `CharacterReceived` is removed from the grid subscription

`CharacterReceived` is now removed entirely from `AttachInputHandlers`/`DetachInputHandlers`. Two
reasons:

1. On WASM/Skia it is `[Uno.NotImplemented]` — keeping it is misleading and does nothing.
2. On Windows/desktop it **bubbles**: a character typed while the proxy holds focus would travel up
   the visual tree and fire *both* `OnProxyTextChanged` (correct) *and* the grid's
   `OnCharacterReceived` (duplicate). Removing the grid subscription eliminates the double-fire.

The grid's `KeyDown` handler is **kept** as a harmless fallback. `OnProxyKeyDown` marks all mapped
keys `e.Handled = true`, so they don't bubble to the grid's `OnKeyDown`; only unmapped keys (none,
in practice) would reach it.

### `OnProxyTextChanged` — always-on forwarding (no edit-state guard)

The previous `if (!_editSession.IsEditing) return;` guard has been removed. Text arriving at the
proxy is now forwarded via `HandleEditChar` regardless of edit state. Core's `HandleKey` owns the
"start edit vs. continue edit" decision:

- Not editing: `HandleEditChar(ch)` → Core's Typing trigger → `CellBeginEdit` → proxy already
  focused (the press focus-step already happened) → subsequent chars continue the edit via the
  same `TextChanged` path.
- Already editing: `HandleEditChar(ch)` → Core appends to the editor value. Identical behavior.

The sentinel-diff algorithm, Backspace detection, and `ResetProxy()` call are unchanged.

### 3a. Hidden-TextBox proxy — full design (DataGridView.Editing.cs)

**Proxy construction** (field initializer, added to `Children` in `SetupInputProxy()` called from ctor):
```csharp
private readonly TextBox _inputProxy = new TextBox
{
    Visibility = Visibility.Visible,  // NOT Collapsed — collapsed elements cannot focus
    Opacity = 0,
    Width = 1,
    Height = 1,
    BorderThickness = new Thickness(0),
    Padding = new Thickness(0),
    IsSpellCheckEnabled = false,
    IsTextPredictionEnabled = false,  // Uno0001 no-op on some heads — safe
    AcceptsReturn = false,
    IsTabStop = false,
    Text = ProxySentinel,             // start with sentinel
};
```

**Sentinel / suppression fields:**
```csharp
private const string ProxySentinel = "​";   // zero-width space U+200B
private bool _suppressProxyTextChanged;
private string _lastProxyText = ProxySentinel;
```

**`ResetProxy()`** — called after every input event and on edit-end:
```csharp
private void ResetProxy()
{
    _suppressProxyTextChanged = true;
    _inputProxy.Text = ProxySentinel;
    _lastProxyText = ProxySentinel;
    _suppressProxyTextChanged = false;
}
```

**`FocusKeyboardInput()`** — called on `CellBeginEdit`, `OnKeyboardFocusRequested`, non-touch press,
and after edit-end on non-touch:
```csharp
private void FocusKeyboardInput()
{
    ResetProxy();
    _inputProxy.Focus(FocusState.Programmatic);
}
```
On iOS/Android this summons the on-screen keyboard (focusing a `TextBox` triggers the platform
first-responder). On desktop/WASM it routes subsequent `KeyDown`/`TextChanged` events to the proxy.

**`OnProxyTextChanged`** (soft-keyboard / IME / typed-char path):
1. Guard: `if (_suppressProxyTextChanged) return`.
2. Empty new text → Backspace (`HandleEditKey(GridKey.Backspace)`) + `ResetProxy()`.
3. Otherwise, extract newly inserted characters via sentinel-diff:
   - Standard append (`sentinel + "a"`): take the suffix after `oldText`.
   - IME full-replacement: strip the sentinel if embedded; use remaining text.
4. For each extracted char: `\n`/`\r` → `HandleEditKey(GridKey.Enter)`, `\t` → `HandleEditKey(GridKey.Tab)`,
   otherwise → `HandleEditChar(ch)` which builds `GridKeyEventArgs { Key=None, Character=ch }`.
5. `ResetProxy()`.
6. **No edit-state guard** — Core decides whether to start or continue an edit.

**`OnProxyKeyDown`** (hardware keyboard path — navigation and command keys):
Maps `e.Key` via `InputMapping.ToGridKey` and calls `HandleEditKey`; sets `e.Handled = true` for all
mapped keys. This prevents the TextBox from also acting on Backspace (deleting sentinel), Enter
(adding newline), Tab (moving focus), arrows (moving caret), etc., and prevents the mapped keys
from bubbling to the grid's `OnKeyDown`.

**On `CellBeginEdit`:**
```csharp
StartCursorBlinkTimer();
FocusKeyboardInput();   // proxy gets focus → soft keyboard appears on mobile
```

**On `CellEndEdit`:**
```csharp
ResetProxy();
if (_lastPointerWasTouch)
    Focus(FocusState.Programmatic);   // touch: blur proxy → soft keyboard dismisses
else
    FocusKeyboardInput();             // mouse/pen: proxy stays focused → keyboard sink maintained
```

**Handler subscription:** proxy `TextChanged`/`KeyDown` are attached idempotently in
`AttachInputHandlers` (detach-then-attach) and detached in `TeardownInputProxy` (called from
`DetachInputHandlers`/`OnUnloaded`) so navigation-back never double-subscribes.

## 4. Keyboard paths — walk-through

### Non-touch (mouse / pen / keyboard-only)

| Scenario | Keyboard path |
|---|---|
| **Click a cell** | `OnPointerPressed` → `DispatchPointer` (Core selects cell) → `FocusKeyboardInput()` → proxy focused |
| **Arrow / Tab / Home / End** | `OnProxyKeyDown` → `InputMapping.ToGridKey` → `HandleEditKey` → Core navigates; `e.Handled=true` blocks TextBox caret movement |
| **Type a character (type-to-edit)** | Char appears in proxy → `OnProxyTextChanged` → `HandleEditChar(ch)` → Core Typing trigger → `CellBeginEdit` → `FocusKeyboardInput()` (proxy already focused) → edit continues via same path |
| **F2 (explicit edit start)** | `OnProxyKeyDown` → `HandleEditKey(GridKey.F2)` → Core → `CellBeginEdit` → `FocusKeyboardInput()` |
| **Double-click (edit start)** | `DispatchPointer(ClickCount=2)` → Core → `CellBeginEdit` → `FocusKeyboardInput()` |
| **Typing during edit** | Chars arrive at proxy `TextChanged` → `HandleEditChar(ch)` → Core editor appends |
| **Backspace during edit** | `OnProxyKeyDown` → `HandleEditKey(Backspace)` → `e.Handled=true` (no sentinel deletion) |
| **Enter / Escape / Tab during edit** | `OnProxyKeyDown` → `HandleEditKey` → Core commits/cancels → `CellEndEdit` → `ResetProxy()` + `FocusKeyboardInput()` (proxy stays focused) |
| **Arrow after edit ends** | Proxy still focused → `OnProxyKeyDown` → navigation continues seamlessly |

### Touch

| Scenario | Keyboard path |
|---|---|
| **Tap a cell** | `OnPointerPressed` (`_lastPointerWasTouch=true`) → `DispatchPointer` → Core selects cell; proxy NOT focused (no soft keyboard summon) |
| **Double-tap (edit start)** | `DispatchPointer(ClickCount=2)` → Core → `CellBeginEdit` → `FocusKeyboardInput()` → proxy focused → soft keyboard appears |
| **Long-press** | `OnHolding` → `LongPress` → Core context action |
| **Typing during edit** | Soft keyboard injects into proxy `TextChanged` → `HandleEditChar` → Core editor appends |
| **Backspace on soft keyboard** | Proxy text becomes empty → `OnProxyTextChanged` → `HandleEditKey(Backspace)` |
| **Done / Return on soft keyboard** | `\n` in `OnProxyTextChanged` → `HandleEditKey(GridKey.Enter)` → Core commits → `CellEndEdit` → `ResetProxy()` + `Focus(grid)` → soft keyboard dismisses |

### No double-handling

- `OnProxyKeyDown` sets `e.Handled = true` for every mapped key, so mapped keys don't bubble to
  the grid's `OnKeyDown`.
- `CharacterReceived` is unsubscribed from the grid entirely — typed characters flow only through
  `OnProxyTextChanged`. No character is delivered twice.
- `_suppressProxyTextChanged` and `_lastProxyText` guard `ResetProxy()`'s own `Text` assignment
  from triggering another `TextChanged` round-trip.

## 5. Inertial scroll timer

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

## 6. Per-target nuances

| Head | Pointer | Keyboard / Editing | Notes |
|---|---|---|---|
| Desktop (Skia) / Windows | mouse + touch + pen | proxy `TextChanged` for all typing; proxy `KeyDown` for navigation/command keys; grid `KeyDown` as harmless fallback | Reference behavior. Non-touch press focuses proxy immediately; proxy is the keyboard sink at all times. |
| WebAssembly | mouse/touch OK | **proxy-only** — `CharacterReceived` is `[Uno.NotImplemented]`; grid focus unreliable for `KeyDown` | Proxy `TextChanged`/`KeyDown` fire on WASM. `GetLiveModifiers` wrapped in try/catch. |
| Android | touch only, no hover | **proxy-only** — `CharacterReceived` not reliable on Android IME | TextBox proxy handles all text input via `TextChanged`; sentinel-diff handles IME full-replacement. |
| iOS | touch only, no hover | **proxy-only** — `CharacterReceived` is `[Uno.NotImplemented]` on iOS | TextBox (`UITextField`) gains focus → `becomeFirstResponder` → soft keyboard. `Holding` provides long-press. |
| Mac Catalyst | trackpad + keyboard | full — same as desktop | Like desktop; validate momentum-scroll feel vs. the inertial timer. |

> **Root cause note:** `UIElement.CharacterReceived` is `[Uno.NotImplemented]` on iOS (and WASM) —
> confirmed by Uno build warnings (`Uno0001`) on those heads. Additionally, `DataGridView` derives
> from `Grid` (a `Panel`), which is not reliably keyboard-focusable on WASM. The hidden-TextBox
> proxy (`_inputProxy`) fully replaces `CharacterReceived` and the `Grid`-focus approach for all
> heads. Since the proxy is a real `TextBox`, Uno maps its `TextChanged`/`KeyDown` to the native
> text-event pipeline on every platform — the fix is structurally correct and platform-agnostic.

- [ ] Verify selection, keyboard navigation, editing, wheel + inertial scroll, column resize/reorder,
  and row drag on **each** head's smoke test (see [07](07-tests.md)). *(Behavioral/visual
  verification requires a simulator/device for iOS/Android and a browser for WASM.)*

---

## ✅ Exit criteria

- [x] Pointer + keyboard + focus + inertial/cursor-blink timers are fully wired on `net9.0-desktop`
  and the project **compiles cleanly** (`dotnet build … -f net9.0-desktop` → 0 errors; the only
  warnings are pre-existing `CS1591` doc warnings from `KumikoUI.Core`/`KumikoUI.SkiaSharp`).
  Every input path constructs `GridPointerEventArgs`/`GridKeyEventArgs` correctly and calls the right
  Core method; `VirtualKey→GridKey` mapping is complete; handlers attach on `Loaded` and fully detach
  on `Unloaded`; all timers stop on `Unloaded`.
- [x] `CharacterReceived` removed from grid subscription — no double-fire risk on Windows; no dead
  stub on WASM/iOS. All character input flows through `OnProxyTextChanged` only.
- [x] Proxy is the keyboard sink for non-touch input at all times (focused on every mouse/pen press
  and after every edit-end); `OnProxyTextChanged` forwards characters regardless of edit state so
  type-to-edit works without a separate `CharacterReceived` path.
- [x] Touch behavior is unchanged: proxy focused only on `CellBeginEdit`; soft keyboard dismissed
  on `CellEndEdit` by re-focusing the grid.
- [ ] *(Phase 06/07)* Mouse/touch select, scroll (wheel + inertial), resize, reorder, and drag all
  work behaviorally on `net9.0-desktop`.
- [ ] *(Phase 06/07)* Arrow/Tab/Enter/Escape navigation and typed cell editing work via keyboard.
- [ ] *(Phase 06/07)* Pointer coordinates align with drawn cells at all display scales.
- [x] Handlers are attached on `Loaded` and fully detached on `Unloaded` (no leaks across
  navigation); idempotent re-attach on navigation-back.

➡️ Next: [05 — Fonts & assets](05-fonts-and-assets.md)

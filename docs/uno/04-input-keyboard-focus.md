# 04 — Input, Keyboard & Focus

Goal: translate Uno/WinUI pointer, keyboard, and focus events into the **existing** Core input API.
Core already owns all behavior; the host only adapts event shapes. No Core changes.

Core entry points (unchanged):
`GridInputController.HandlePointer(GridPointerEventArgs, scroll, selection, style, dataSource)`,
`HandleKey(GridKeyEventArgs, …)`, `UpdateInertialScroll(scroll, 16f)`.

> **Status:** Implemented for all heads (desktop, iOS, Android, WASM). Wiring lives in
> `src/KumikoUI.Uno/DataGridView.Input.cs` (pointer + keyboard + timer, a partial of `DataGridView`),
> `src/KumikoUI.Uno/DataGridView.Editing.cs` (hidden-TextBox proxy for soft-keyboard input — see §3a),
> and `src/KumikoUI.Uno/Input/InputMapping.cs` (pure enum mapping).
> Build verified: `dotnet build … -f net9.0-desktop` → **0 errors, 0 new warnings**;
> `dotnet build … -f net9.0-ios` → **0 errors** (pre-existing `Uno0001` warnings for `CharacterReceived`
> / `IsHoldingEnabled` on iOS; these are not caused by the fix and are safe no-ops on that head).
> Soft-keyboard runtime behavior on iOS/Android cannot be verified headlessly (requires a
> simulator/device), but the fix is structurally correct: see §3a for reasoning.

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
  respected). Focus the control on pointer press (when NOT editing — see §3a) and wire Core's request
  to `FocusKeyboardInput()` (see §3a).
  > **Note:** `IsTabStop` / `TabIndex` / `Focus(FocusState)` are defined on **`UIElement`** in
  > WinUI/Uno (not `Control` as in WPF), so they are available on this `Grid`-derived control.
- [x] **Navigation / command keys** — `KeyDown` on the **grid** (still active when not editing):
  maps `VirtualKey` → `GridKey` → `HandleKey`. Navigation keys (arrows, Tab, Home/End, etc.), F2
  to start edit, Escape, clipboard shortcuts — all flow through `OnKeyDown`. During an active edit
  the proxy's `OnProxyKeyDown` handles these instead (see §3a), so `OnKeyDown` fires for the not-editing
  case only (the proxy holds focus while editing, so grid `KeyDown` does not fire then).
- [x] **Text entry (cell editing)** — the grid's `OnCharacterReceived` is still subscribed for the
  **not-editing** case (type-to-start-edit on desktop, where the grid holds focus). However,
  `CharacterReceived` is marked `[Uno.NotImplemented]` on the iOS head, so it is a no-op there —
  this is the root cause confirmed by the iOS build warnings. The hidden-TextBox proxy (§3a) is the
  correct cross-platform path for all soft-keyboard text entry.

### 3a. Hidden-TextBox soft-keyboard proxy (iOS/Android/WASM fix)

**Root cause:** `DataGridView` derives from `Grid`, a non-text element. On iOS and Android, focusing a
non-text element does NOT summon the on-screen keyboard, and `CharacterReceived`/`KeyDown` do not fire
from on-screen keys (confirmed: `CharacterReceived` is `[Uno.NotImplemented]` on the iOS head).
Desktop editing worked only because it has a hardware keyboard. This is the same problem MAUI solved
with a hidden `Entry`; the fix mirrors that pattern using a WinUI `TextBox`.

**Implementation:** `src/KumikoUI.Uno/DataGridView.Editing.cs` (a new partial of `DataGridView`).

**Proxy construction** (field initializer, added to Children in `SetupInputProxy()` called from ctor):
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

**`FocusKeyboardInput()`** — called on `CellBeginEdit` and `OnKeyboardFocusRequested`:
```csharp
private void FocusKeyboardInput()
{
    ResetProxy();
    _inputProxy.Focus(FocusState.Programmatic);
}
```
On iOS/Android this summons the on-screen keyboard (focusing a `TextBox` triggers UIKit's `becomeFirstResponder`).
On desktop it routes subsequent `KeyDown`/`TextChanged` events to the proxy.

**`OnProxyTextChanged`** (soft-keyboard / IME path):
1. Guard: `if (_suppressProxyTextChanged || !_editSession.IsEditing) return`.
2. Empty new text → Backspace (`HandleEditKey(GridKey.Backspace)`) + `ResetProxy()`.
3. Otherwise, extract newly inserted characters via sentinel-diff:
   - Standard append (`sentinel + "a"`): take the suffix after `oldText`.
   - IME full-replacement: strip the sentinel if embedded; use remaining text.
4. For each extracted char: `\n`/`\r` → `HandleEditKey(GridKey.Enter)`, `\t` → `HandleEditKey(GridKey.Tab)`,
   otherwise → `HandleEditChar(ch)` which builds `GridKeyEventArgs { Key=None, Character=ch }`.
5. `ResetProxy()`.

**`OnProxyKeyDown`** (hardware keyboard path):
Maps `e.Key` via `InputMapping.ToGridKey` and calls `HandleEditKey`; sets `e.Handled = true` for all
mapped keys. This prevents the TextBox from also acting on Backspace (deleting sentinel), Enter
(adding newline), Tab (moving focus), arrows (moving caret), etc., and prevents double-handling
with the TextChanged path.

**On `CellBeginEdit`:**
```csharp
StartCursorBlinkTimer();
FocusKeyboardInput();   // proxy gets focus → soft keyboard appears on mobile
```

**On `CellEndEdit`:**
```csharp
ResetProxy();
Focus(FocusState.Programmatic);  // grid regains focus → soft keyboard dismisses on mobile
```
WinUI has no `Unfocus()`; focusing another element is the idiom.

**On `OnPointerPressed`:** now guards `if (!_editSession.IsEditing) Focus(FocusState.Programmatic)`.
This prevents a tap during an active edit from stealing focus from the proxy and prematurely
dismissing the keyboard. The edit lifecycle owns proxy focus.

**Handler subscription:** proxy `TextChanged`/`KeyDown` are attached idempotently in
`AttachInputHandlers` (detach-then-attach) and detached in `TeardownInputProxy` (called from
`DetachInputHandlers`/`OnUnloaded`) so navigation-back never double-subscribes.

**Desktop edit-not-regressed reasoning:**
1. User types a character while the grid has focus (not editing yet): `OnCharacterReceived` fires on
   the grid → `HandleKey` → Core starts an edit → `CellBeginEdit` → `FocusKeyboardInput()` → proxy
   gets focus. The initial character is passed through via `CharacterReceived` before focus shifts,
   so Core's `Typing` trigger sees it. On the next keystroke the proxy holds focus.
2. Proxy `OnProxyTextChanged`: typed character appended after sentinel → `HandleEditChar(ch)` → Core
   editor receives the character exactly as before.
3. Proxy `OnProxyKeyDown`: Enter/Escape/Tab/arrows are handled and marked `e.Handled=true` so the
   TextBox does not consume them.
4. Edit ends → `Focus(FocusState.Programmatic)` on the grid → grid `OnKeyDown`/`OnCharacterReceived`
   resume for navigation.

**iOS/Android runtime caveat:** soft-keyboard behavior (keyboard appearance, dismissal, IME
composition) cannot be verified headlessly and requires a simulator or device. The implementation is
structurally correct: `TextBox` is a standard WinUI control that Uno maps to `UITextField` on iOS
(which calls `becomeFirstResponder` on focus) and `EditText` on Android. The sentinel-diff algorithm
and guard logic mirror MAUI's proven approach exactly.

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

| Head | Pointer | Keyboard / Editing | Notes |
|---|---|---|---|
| Desktop (Skia) / Windows | mouse + touch + pen | full — hardware keyboard via `OnKeyDown`; proxy `TextChanged` also receives typed chars | Reference behavior. Grid `CharacterReceived` starts type-to-edit; proxy takes over once editing begins. |
| WebAssembly | mouse/touch OK | proxy `TextChanged` for typing; `OnKeyDown` for navigation | Browser soft keyboard (on touch devices) summons on proxy focus. `GetLiveModifiers` wrapped in try/catch. |
| Android | touch only, no hover | **proxy-only** — `CharacterReceived` is not reliable on Android IME | TextBox proxy handles all text input via `TextChanged`; sentinel-diff handles IME full-replacement. |
| iOS | touch only, no hover | **proxy-only** — `CharacterReceived` is `[Uno.NotImplemented]` on iOS | TextBox (`UITextField`) gains focus → `becomeFirstResponder` → soft keyboard. `Holding` provides long-press (if implemented on the iOS head). |
| Mac Catalyst | trackpad + keyboard | full — same as desktop | Like desktop; validate momentum-scroll feel vs. the inertial timer. |

> **Note on `CharacterReceived` on iOS:** Uno's iOS head marks `UIElement.CharacterReceived` as
> `[Uno.NotImplemented]` — it is a no-op stub. This is the confirmed root cause of the original bug.
> The hidden-TextBox proxy (§3a) fully replaces `CharacterReceived` for all soft-keyboard input,
> and desktop/WASM hardware-keyboard paths continue to use `OnKeyDown` (for command keys) and the
> proxy's `TextChanged` (for printable chars).

- [ ] Verify selection, keyboard navigation, editing, wheel + inertial scroll, column resize/reorder,
  and row drag on **each** head's smoke test (see [07](07-tests.md)). *(Behavioral/visual
  verification requires a simulator/device for iOS/Android; WASM requires a browser environment.)*

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

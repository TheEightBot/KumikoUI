# 04 — Input, Keyboard & Focus

Goal: translate Uno/WinUI pointer, keyboard, and focus events into the **existing** Core input API.
Core already owns all behavior; the host only adapts event shapes. No Core changes.

Core entry points (unchanged):
`GridInputController.HandlePointer(GridPointerEventArgs, scroll, selection, style, dataSource)`,
`HandleKey(GridKeyEventArgs, …)`, `UpdateInertialScroll(scroll, 16f)`.

---

## 1. Pointer input

WinUI delivers pointer events on any `UIElement`; subscribe on the `DataGridView` (or its
`SKXamlCanvas`). `GetCurrentPoint(this).Position` is already in **logical DIPs** — the same coordinate
space the renderer was fed in [03 §4](03-datagridview-host.md#4-dpi--scaling), so hit-testing lines up.

- [ ] Subscribe (attach in `OnLoaded`, detach in `OnUnloaded`):
  `PointerPressed`, `PointerMoved`, `PointerReleased`, `PointerCanceled`, `PointerWheelChanged`,
  `PointerExited`.
- [ ] Capture the pointer on press so drags (column resize/reorder, selection, scroll) keep tracking
  outside the element:
  ```csharp
  private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
  {
      CapturePointer(e.Pointer);
      Focus(FocusState.Programmatic);               // so keyboard works (see §3)
      DispatchPointer(e, InputAction.Pressed);
  }
  ```
- [ ] Build `GridPointerEventArgs` (mirrors MAUI's `OnCanvasTouch`, just a different source type):
  ```csharp
  private void DispatchPointer(PointerRoutedEventArgs e, InputAction action)
  {
      var p   = e.GetCurrentPoint(this);
      var pp  = p.Properties;
      var evt = new GridPointerEventArgs
      {
          X = (float)p.Position.X,
          Y = (float)p.Position.Y,
          Action = action,
          Button = pp.IsRightButtonPressed ? PointerButton.Secondary
                 : pp.IsMiddleButtonPressed ? PointerButton.Middle
                 : PointerButton.Primary,
          ScrollDeltaY = action == InputAction.Scroll ? pp.MouseWheelDelta : 0,
          Modifiers = MapModifiers(e.KeyModifiers),
          TimestampMs = (long)(p.Timestamp / 1000),   // WinUI timestamp is microseconds
          ClickCount = 1,
      };
      _inputController.HandlePointer(evt, _scroll, _selection, _style, _dataSource);

      if ((action is InputAction.Released or InputAction.Cancelled) && _inputController.IsInertialScrolling)
          StartInertialScrollTimer();
  }
  ```
- [ ] Map the action per event: `PointerPressed→Pressed`, `PointerMoved→Moved`,
  `PointerReleased→Released` (+ `ReleasePointerCapture`), `PointerCanceled`/`PointerCaptureLost→Cancelled`,
  `PointerWheelChanged→Scroll`.
- [ ] **Wheel magnitude:** WinUI `MouseWheelDelta` is in 120-unit notches. Scale to match the pixel
  delta the renderer expects (compare against MAUI's `SKTouch.WheelDelta`); tune so one notch scrolls a
  sensible number of rows.
- [ ] **Double-tap / click-count:** WinUI does not surface a click count on pointer args. Either handle
  the `DoubleTapped` event (raise `InputAction.DoubleTap`) or track press timing manually and set
  `ClickCount = 2`.
- [ ] **Long-press (touch):** Core supports `InputAction.LongPress`. Handle WinUI `Holding`
  (`HoldingState.Started`) or a manual press timer and dispatch `LongPress` — needed for touch row
  drag / context actions on mobile.

## 2. Modifier mapping

- [ ] `InputModifiers` is a `[Flags]` enum (Shift/Control/Alt). Map from WinUI `VirtualKeyModifiers`:
  ```csharp
  private static InputModifiers MapModifiers(VirtualKeyModifiers m)
  {
      var r = InputModifiers.None;
      if (m.HasFlag(VirtualKeyModifiers.Shift))   r |= InputModifiers.Shift;
      if (m.HasFlag(VirtualKeyModifiers.Control)) r |= InputModifiers.Control;
      if (m.HasFlag(VirtualKeyModifiers.Menu))    r |= InputModifiers.Alt;   // Menu == Alt
      return r;
  }
  ```
  > In `KeyDown`/`KeyUp` (which lack `KeyModifiers`), read live state via
  > `InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)` (or track Shift/Ctrl/Alt
  > down/up) to build the same flags.

## 3. Keyboard & focus

The control must be focusable to receive key events.

- [ ] In the ctor/loaded: `IsTabStop = true;` and give the control a `TabIndex`. Focus it on pointer
  press (above) and wire Core's request:
  ```csharp
  private void OnKeyboardFocusRequested() => Focus(FocusState.Programmatic);
  ```
- [ ] **Navigation / command keys** — `KeyDown` → map `VirtualKey` → `GridKey` → `HandleKey`:
  ```csharp
  private void OnKeyDown(object sender, KeyRoutedEventArgs e)
  {
      var key = MapKey(e.Key);                 // VirtualKey → GridKey
      if (key == GridKey.None) return;
      var evt = new GridKeyEventArgs { Key = key, Modifiers = CurrentModifiers(), IsKeyDown = true };
      _inputController.HandleKey(evt, _scroll, _selection, _style, _dataSource);
      if (evt.Handled) e.Handled = true;
  }
  ```
  Mapping covers: arrows, `Home`/`End`, `PageUp`/`PageDown`, `Tab`, `Enter`, `Escape`, `Space`,
  `Delete`, `Back→Backspace`, and letters `A/C/V/X/Z` (select-all / clipboard / undo while editing).
- [ ] **Text entry (cell editing)** — use `CharacterReceived` instead of MAUI's hidden-`Entry` proxy.
  It yields the resolved character (respecting IME/dead keys):
  ```csharp
  private void OnCharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs e)
  {
      if (!_editSession.IsEditing) return;
      var evt = new GridKeyEventArgs { Key = GridKey.None, Character = e.Character, IsKeyDown = true };
      _inputController.HandleKey(evt, _scroll, _selection, _style, _dataSource);
  }
  ```

## 4. Inertial scroll timer

- [ ] Use a `DispatcherTimer` (~16 ms) that pumps Core's physics and stops when it returns `false`
  (mirror MAUI's `_scrollTimer`):
  ```csharp
  private DispatcherTimer? _scrollTimer;
  private void StartInertialScrollTimer()
  {
      _scrollTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
      _scrollTimer.Tick -= OnScrollTick; _scrollTimer.Tick += OnScrollTick;
      _scrollTimer.Start();
  }
  private void OnScrollTick(object? s, object e)
  {
      if (!_inputController.UpdateInertialScroll(_scroll, 16f)) _scrollTimer!.Stop();
      Invalidate();
  }
  ```
- [ ] Apply the same `DispatcherTimer` pattern to the cursor-blink and long-press timers MAUI runs via
  `Dispatcher.CreateTimer()`.

## 5. Per-target nuances

| Head | Pointer | Keyboard | Notes |
|---|---|---|---|
| Desktop (Skia) / Windows | mouse + touch + pen | full | Reference behavior; richest input. |
| WebAssembly | mouse/touch OK | needs focusable element | Element must be focusable (TabStop) for `KeyDown`/`CharacterReceived`; verify browser doesn't steal wheel/scroll. |
| Android / iOS | touch only, no hover | soft keyboard | Drive editing via on-screen keyboard; show the input pane (`InputPane.GetForCurrentView().TryShow()`) when an edit begins; rely on `Holding` for long-press. |
| Mac Catalyst | trackpad + keyboard | full | Like desktop; validate momentum-scroll feel vs. inertial timer. |

- [ ] Verify selection, keyboard navigation, editing, wheel + inertial scroll, column resize/reorder,
  and row drag on **each** head's smoke test (see [07](07-tests.md)).

---

## ✅ Exit criteria

- [ ] Mouse/touch select, scroll (wheel + inertial), resize, reorder, and drag all work on `net9.0-desktop`.
- [ ] Arrow/Tab/Enter/Escape navigation and typed cell editing work via keyboard + `CharacterReceived`.
- [ ] Pointer coordinates align with drawn cells at all display scales.
- [ ] Handlers are attached on `Loaded` and fully detached on `Unloaded` (no leaks across navigation).

➡️ Next: [05 — Fonts & assets](05-fonts-and-assets.md)

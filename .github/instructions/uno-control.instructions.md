---
applyTo: "src/KumikoUI.Uno/**,samples/SampleApp.Uno/**,tests/KumikoUI.Uno.Tests/**,docs/uno/**"
---

# KumikoUI — Uno Control & XAML Usage

The Uno host mirrors the MAUI one ([maui-control.instructions.md](maui-control.instructions.md)): the same Core engine,
the same properties and events, a different framework underneath. Read the user guide in
[docs/uno/README.md](../../docs/uno/README.md) for behavior; this file is the rule set for changing the host. The numbered
notes in `docs/uno/plan/` are history, not instructions.

## Rules of the host

- **Nothing platform-specific in Core or SkiaSharp.** A fix that both hosts need goes into `src/KumikoUI.Core` (or
  `src/KumikoUI.SkiaSharp`) and is tested there. A change in `src/KumikoUI.Uno` must not change what the MAUI host does.
- **Keep the hosts in parity.** A property, command or event added to one gets the same name, type (the XAML default
  aside: Uno's `RowHeight` and `HeaderHeight` are `double`) and default on the other, in
  `KumikoDataGrid.Properties.cs` here and `KumikoDataGrid.cs` in `src/KumikoUI.Maui`.
- **Logic that does not need WinUI is internal and unit-tested** in `tests/KumikoUI.Uno.Tests` without a running app
  (`UnoTestRuntime` lets a test create a `DependencyObject`): `InputMapping`, `PointerMapping`, `PointerGestures`,
  `LongPressTracker`, `KumikoFrameScheduler`, `KumikoTextInputAdapter`. Only the event wiring stays in the control.
- **Compile for every head.** The library targets `net10.0`, `net10.0-desktop`, `net10.0-browserwasm`, `net10.0-ios`,
  `net10.0-android` and (on Windows) `net10.0-windows10.0.26100`. Code that exists only on the Skia heads is under
  `#if HAS_UNO`; the Windows App SDK head has `SKXamlCanvas` only. Build the desktop head locally; read the others carefully.
- **XML docs on every public member** and no Uno-specific public type named like a WinUI or MAUI type (CONTRIBUTING.md,
  "Public type names").

---

## Setup (no startup call)

There is no `UseKumikoUI()` on Uno. The app project turns on the Skia renderer and pins SkiaSharp:

```xml
<SkiaSharpVersion>4.153.1</SkiaSharpVersion>
<UnoFeatures>
  SkiaRenderer;
</UnoFeatures>
```

On the Windows App SDK head it also references `SkiaSharp.Views.WinUI`. Fonts are registered in `App.OnLaunched` before the
window is activated:

```csharp
await KumikoFonts.RegisterFromAppPackageAsync("NotoSansJP", "ms-appx:///Assets/Fonts/NotoSansJP-Regular.ttf");
```

---

## Required XAML namespaces

```xml
xmlns:kumiko="using:KumikoUI.Uno"
xmlns:core="using:KumikoUI.Core.Models"
xmlns:render="using:KumikoUI.Core.Rendering"
xmlns:editing="using:KumikoUI.Core.Editing"
```

---

## `KumikoDataGrid` — properties, commands, events

Same names and defaults as MAUI (`DependencyProperty` here); the list is in `maui-control.instructions.md` and the
README's "Host Properties, Commands & Events". Uno-specific:

- `RenderMode`: `Auto`, `Hardware` (Uno's `SKCanvasElement`), `Software` (`SKXamlCanvas`); `ActiveRenderMode` reports the surface in use.
- `FollowSystemTheme` (default true) takes the grid's theme from `ActualTheme`; assigning `Theme` or `GridStyle` turns it off.
- `UnoActionButtonDefinition` is the action button for XAML and code (`MauiActionButtonDefinition` on MAUI).
- `[ContentProperty]` is `Columns`: columns are XAML content.

```xml
<kumiko:KumikoDataGrid ItemsSource="{Binding Items}"
                       GridSelectionMode="Extended"
                       SelectedItem="{Binding Selected, Mode=TwoWay}"
                       RowHeight="36" HeaderHeight="40"
                       AllowSorting="True" AllowFiltering="True">
    <core:KumikoColumn Header="Name" PropertyName="Name" Width="180" />
    <core:KumikoColumn Header="Hired" PropertyName="HireDate" ColumnType="Date" Format="yyyy-MM-dd" Width="120" />
</kumiko:KumikoDataGrid>
```

---

## `KumikoDataGrid` internals (for host work)

The control is a `UserControl`, a `partial class` over `src/KumikoUI.Uno/KumikoDataGrid.*.cs`, one file per concern
(properties, rendering, input, editing, scrolling, selection, theming, options, layout, lifecycle, accessibility,
clipboard, images, haptics, motion, keyboard avoidance, action buttons, renderers).

- **Surface.** The content is a private `IKumikoSurface` (`src/KumikoUI.Uno/Rendering/`): `KumikoCanvasElementSurface`
  (Uno's `SKCanvasElement`, logical units) on Skia heads, `KumikoXamlCanvasSurface` (`SKXamlCanvas`, device pixels scaled
  to logical units) elsewhere, chosen by `KumikoSurfaceFactory` from `RenderMode`.
- **Frame loop.** Every redraw goes through `RequestRedraw`. `KumikoFrameScheduler` (platform-free, unit-tested)
  coalesces requests and measures frame times; the `CompositionTarget.Rendering` callback is subscribed only while a
  fling, a row drag's autoscroll, an editor animation or a deferred redraw needs it. `KumikoCaretBlink` redraws only at caret
  blink transitions. Each frame draws with `ResourceCache.CreateContext(canvas)` and `KumikoRenderer.RenderFrame`.
- **Lifecycle.** Input handlers attach in `OnLoaded` and detach in `OnUnloaded`, which also stops the frame loop and caret
  timer and disposes the `SkiaResourceCache`, so nothing roots the page across navigation. Handlers must be
  idempotent: Uno raises `Loaded` again on navigation back.
- **Pointer** (`KumikoDataGrid.Input.cs`; `PointerMapping`, `PointerGestures`, `LongPressTracker` in `src/KumikoUI.Uno/Input/`): positions
  are logical DIPs relative to the drawing surface, already the renderer's space. Double click 400 ms and 20 px,
  long press 500 ms and 15 px (the grid's own timer, `Holding` as a fallback). Wheel deltas differ per head (120ths of a
  notch, pixels, lines) and are normalized before they reach Core.
- **Keyboard and text** (`KumikoDataGrid.Editing.cs`; `KumikoTextInputAdapter`, `ProxyTextInput` and
  `InputMapping` in `src/KumikoUI.Uno/Input/`): a hidden, invisible `TextBox` proxy receives the keyboard and the input method on every head. It
  mirrors the editor's text and selection; `TextChanging` and the composition events are replayed on Core's
  `ITextInputSession` by `KumikoTextInputAdapter`; `KeyDown` sends command keys to Core and marks a key handled only when
  Core handled it, so Tab and Escape leave the grid when there is nothing to do (no keyboard trap). Command acts as Control
  on Apple platforms and in the browser. See docs/TEXT-INPUT.md.
- **Keyboard avoidance** (`KumikoDataGrid.KeyboardAvoidance.cs`): follows `InputPane.Showing` and `Hiding`, scrolls the edited
  cell above the keyboard, sets `ScrollState.BottomInset`. The geometry is `KumikoKeyboardAvoidance` in Core, shared with MAUI.
- **Focus.** The grid is a tab stop (`IsTabStop` set once in the constructor; a consumer's value wins) and draws its own
  focus: `UseSystemFocusVisuals` is off.
- **Accessibility.** `OnCreateAutomationPeer` returns the internal `KumikoDataGridAutomationPeer` (DataGrid control type, name
  from `GridDescription`, help text from `GridHint`); the proxy is `AccessibilityView.Raw`.
- **Theme and DPI.** `XamlRoot.Changed` is observed while loaded: a rasterization-scale change redraws. `FollowSystemTheme`
  reads `ActualTheme`.
- **Fonts and images.** `KumikoFonts` registers app-package fonts with `SkiaFontRegistrar`; `UnoImageLoader` resolves `ms-appx`,
  `ms-appdata` and `BitmapImage` image values for the shared `KumikoImageCache`.
- **Background rebuilds.** The control creates its `KumikoSource` with `BackgroundRebuildThreshold` set and repaints on
  `ViewChanged` and `ViewUpdated`.

---

## Tests and the sample

- `tests/KumikoUI.Uno.Tests` (`Uno.Sdk`, `net10.0-desktop`, xUnit): `dotnet test tests/KumikoUI.Uno.Tests -c Release`.
  `TextInputFakes.cs` holds the fake proxy and target the adapter tests use.
- `samples/SampleApp.Uno` has the same pages as `samples/SampleApp.Maui` (shared data in `samples/Shared`). A new host
  feature gets a control or a line on the page that shows it, in both samples.
- Run the desktop head: `dotnet run --project samples/SampleApp.Uno -f net10.0-desktop`.

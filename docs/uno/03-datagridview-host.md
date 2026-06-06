# 03 — The `DataGridView` Host Control

Goal: a single Uno control, `KumikoUI.Uno.DataGridView`, that hosts an `SKXamlCanvas`, drives the
reused `DataGridRenderer`, and exposes a `DependencyProperty` surface matching `KumikoUI.Maui`.

Reference implementation to mirror: [`src/KumikoUI.Maui/DataGridView.cs`](../../src/KumikoUI.Maui/DataGridView.cs).

---

## 1. Choose the base type (and why not a templated control)

KumikoUI draws the **entire** grid onto one Skia surface — there are no child controls, named template
parts, or visual states. So the idiomatic WinUI "templated control + `Themes/Generic.xaml`" machinery
(from the [custom-controls guide](https://platform.uno/docs/articles/guides/creating-custom-controls.html))
is unnecessary here. Mirror MAUI instead: a thin layout host that owns one canvas child.

- [x] Declare the control deriving from a panel so the canvas can be added as a child (MAUI derives
  from `Grid` and calls `Children.Add(_canvasView)` — do the same):
  ```csharp
  using Microsoft.UI.Xaml.Controls;
  using SkiaSharp.Views.Windows;        // SKXamlCanvas + SKPaintSurfaceEventArgs (both packages)

  namespace KumikoUI.Uno;

  public partial class DataGridView : Grid   // Microsoft.UI.Xaml.Controls.Grid
  {
      private readonly SKXamlCanvas _canvasView = new();
      private readonly DataGridRenderer _renderer = new();
      // _dataSource, _scroll, _selection, _style, _editSession, _inputController — same Core types as MAUI
  }
  ```
  > Deviation: deriving from `Microsoft.UI.Xaml.Controls.Grid` pulls
  > `Microsoft.UI.Xaml.Controls.SelectionModel` / `SelectionMode` into scope, colliding with
  > Core's `KumikoUI.Core.Models.SelectionModel` / `SelectionMode`. Resolved with `using`-aliases
  > at the top of `DataGridView.cs` and `DataGridView.Properties.cs`.
- [x] The `unolib` template ships a sample `MyTemplatedControl` + `Themes/Generic.xaml`. Remove the
  sample control. Keep an (empty) `Themes/Generic.xaml` only if you later add stylable resources;
  it is **not** required by the drawn `DataGridView`.
  > N/A in this repo — Phase 02 scaffolded the project without the sample control or a
  > `Themes/Generic.xaml`, so there was nothing to remove. The drawn control needs neither.

## 2. Constructor wiring (parity with MAUI ctor)

- [x] Configure and add the canvas, then subscribe to paint + `GridInputController` events:
  ```csharp
  public DataGridView()
  {
      _canvasView.HorizontalAlignment = HorizontalAlignment.Stretch;
      _canvasView.VerticalAlignment   = VerticalAlignment.Stretch;
      Children.Add(_canvasView);

      // Auto-init bindable collections so XAML can add items directly (mirrors MAUI ctor).
      Columns          = new ObservableCollection<DataGridColumn>();
      TableSummaryRows = new ObservableCollection<TableSummaryRow>();

      _dataSource.DataChanged += OnDataSourceDataChanged;

      _canvasView.PaintSurface += OnPaintSurface;

      _inputController.NeedsRedraw          += OnInputControllerNeedsRedraw;
      _inputController.ColumnReordered      += OnColumnReordered;
      _inputController.RowReordered         += OnRowReordered;
      _inputController.AutoFitColumnRequested += OnAutoFitColumnRequested;
      _inputController.KeyboardFocusRequested += OnKeyboardFocusRequested;
      _inputController.FilterPopupOpened    += OnFilterPopupOpened;
      _inputController.FilterPopupClosed    += OnFilterPopupClosed;
      _inputController.EditSession = _editSession;
      _editSession.Style = _style;
      _editSession.NeedsRedraw += OnEditSessionNeedsRedraw;

      Loaded   += OnLoaded;     // (re)attach pointer/keyboard handlers — see 04
      Unloaded += OnUnloaded;   // detach everything (mirror MAUI lines ~928/990)
  }
  ```
  > Notes vs. the skeleton above (all to keep full MAUI parity):
  > - Also subscribed `_dataSource.DataChanged`, set `_editSession.Style`, and subscribed
  >   `_editSession.NeedsRedraw` — MAUI does all three in its ctor.
  > - The `KeyboardFocusRequested` / `FilterPopupOpened` / `FilterPopupClosed` handlers are
  >   subscribed now (the controller raises them), but their bodies are **Phase 04** stubs (soft
  >   keyboard focus + cursor-blink timer) carrying a `// Phase 04:` comment.
  > - MAUI's ctor also subscribes `_editSession.CellBeginEdit` / `CellEndEdit` to drive the
  >   cursor-blink timer and keyboard focus — those are **Phase 04** (timer/keyboard), so they are
  >   *not* wired here. The public `CellBeginEdit` / `CellEndEdit` events (§6) still forward
  >   straight from `_editSession`, so API parity holds.

## 3. The paint loop (reuse SkiaSharp + Core verbatim)

The event and args are the **same types** MAUI uses, so the body is copied almost verbatim from
[`DataGridView.OnPaintSurface`](../../src/KumikoUI.Maui/DataGridView.cs) (line ~610) — only DPI scaling
differs (see step 4).

- [x] Implement (as shipped — adds `canvas.Save()/Restore()` around the scale and feeds the
  renderer the logical viewport via `_scroll.ViewportWidth/Height`, exactly as MAUI does):
  ```csharp
  private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
  {
      var canvas = e.Surface.Canvas;
      var info   = e.Info;

      var bg = _style.BackgroundColor;            // Core GridColor
      canvas.Clear(new SKColor(bg.R, bg.G, bg.B, bg.A));

      // DPI: e.Info is in physical pixels; ActualWidth/Height are logical DIPs (step 4)
      float scale = ActualWidth > 0 ? (float)(info.Width / ActualWidth) : 1f;
      _scroll.ViewportWidth  = (float)(ActualWidth  > 0 ? ActualWidth  : info.Width  / scale);
      _scroll.ViewportHeight = (float)(ActualHeight > 0 ? ActualHeight : info.Height / scale);

      canvas.Save();
      canvas.Scale(scale);

      using var drawingContext = new SkiaDrawingContext(canvas);   // REUSED from KumikoUI.SkiaSharp
      _renderer.Render(                                            // REUSED from KumikoUI.Core
          drawingContext, _dataSource, _scroll, _selection, _style,
          _inputController.DragColumnIndex, _inputController.DragColumnScreenX,
          _inputController.DragRowIndex,    _inputController.DragRowScreenY,
          _editSession, _inputController.PopupManager);

      canvas.Restore();
  }
  ```
  > The `Render(...)` argument list matches `DataGridRenderer.Render` and the MAUI call site exactly.
- [x] Request a redraw on every Core "invalidate" signal:
  ```csharp
  private void InvalidateSurface() => _canvasView.Invalidate();          // MAUI: _canvasView.InvalidateSurface()
  private void OnInputControllerNeedsRedraw() => _canvasView.Invalidate();
  private void OnEditSessionNeedsRedraw()      => _canvasView.Invalidate();
  private void OnDataSourceDataChanged()       => _canvasView.Invalidate();
  ```

## 4. DPI / scaling

`SKXamlCanvas.PaintSurface` provides `e.Info` in **device pixels**, while `ActualWidth`/`ActualHeight`
are **logical DIPs**. KumikoUI's renderer is resolution-independent and is fed logical units (as in
MAUI). Bridge the two with a single `canvas.Scale(scale)` where `scale = e.Info.Width / ActualWidth`.

- [x] Apply the scale transform before rendering (shown above).
- [ ] Re-invalidate on DPI changes: handle `XamlRoot.Changed` (or `RasterizationScale` changes) and
  call `Invalidate()`.
  > **Deferred to Phase 04.** `SKXamlCanvas` already re-rasterises (and raises `PaintSurface`) on
  > size/scale changes, so the per-frame `scale = e.Info.Width / ActualWidth` keeps output correct
  > across DPI today. A `XamlRoot`/`RasterizationScale` subscription needs the live visual tree, so
  > it belongs with the rest of the runtime wiring attached in `OnLoaded`/`OnUnloaded` (Phase 04).
- [ ] **Verify** text stays crisp and hit-testing aligns with drawn cells at 100%, 150%, and 200% scale.
  > **Deferred to Phase 06.** Requires the sample app (`SampleApp.Uno`) to render on a real surface;
  > Phase 03's bar is a clean compile + fully wired paint loop. Hit-testing alignment is a Phase 04
  > concern (no pointer input is wired yet).

## 5. `DependencyProperty` surface (parity with MAUI `BindableProperty`s)

Map each MAUI `BindableProperty` to a WinUI `DependencyProperty`. Same names, same defaults; the
`PropertyChangedCallback` mutates Core state and calls `Invalidate()`.

- [x] Register these (1:1 with [`DataGridView.cs`](../../src/KumikoUI.Maui/DataGridView.cs)). The
  "Core type / target" column records the exact CLR type each DP registers and what its callback
  mutates:

  | DependencyProperty | DP type | Default | Callback target (Core) |
  |---|---|---|---|
  | `ItemsSource` | `System.Collections.IEnumerable` | `null` | `_dataSource.SetItems(...)`; (un)subscribes `INotifyCollectionChanged` |
  | `Columns` | `ObservableCollection<DataGridColumn>` | `null` | `_dataSource.SetColumns(...)`; subscribes `CollectionChanged` |
  | `TableSummaryRows` | `ObservableCollection<TableSummaryRow>` | `null` | `_dataSource.ClearTableSummaryRows()` + `AddTableSummaryRow(...)` |
  | `GridSelectionMode` | `KumikoUI.Core.Models.SelectionMode` | `Extended` | `_selection.Mode` |
  | `RowHeight` | `double` | `36` | `_style.RowHeight` (cast `double`→`float`) |
  | `HeaderHeight` | `double` | `40` | `_style.HeaderHeight` (cast `double`→`float`) |
  | `FrozenRowCount` | `int` | `0` | `_dataSource.FrozenRowCount` |
  | `EditTriggers` | `KumikoUI.Core.Editing.EditTrigger` | `EditTrigger.Default` | `_editSession.EditTriggers` |
  | `EditTextSelectionMode` | `KumikoUI.Core.Editing.EditTextSelectionMode` | `SelectAll` | `_editSession.TextSelectionMode` |
  | `IsReadOnly` | `bool` | `false` | repaint (honored at edit time — Phase 04 gesture path) |
  | `AllowSorting` | `bool` | `true` | repaint |
  | `AllowFiltering` | `bool` | `true` | repaint |
  | `DismissKeyboardOnEnter` | `bool` | `true` | `_editSession.DismissKeyboardOnEnter` |
  | `GridDescription` | `string` | `"Data grid"` | `AutomationProperties.SetName(this, ...)` |

  > Deviations vs. the table the doc originally assumed:
  > - **`EditTriggers`**: the Core type is the singular flags enum `EditTrigger` (not `EditTriggers`).
  >   The *DP* keeps the plural name `EditTriggers` (matches MAUI's `BindableProperty`); its CLR type
  >   is `EditTrigger`.
  > - **`RowHeight` / `HeaderHeight`**: registered as `double` (the WinUI idiom for sizes) and cast to
  >   `float` for `DataGridStyle`. MAUI registered these as `float`; defaults (36 / 40) match.
  > - **`GridDescription`**: there is no Core "GridDescription" type — it is a plain `string`
  >   accessibility label. MAUI routes it through `SemanticProperties.SetDescription`; the WinUI
  >   equivalent is `AutomationProperties.SetName`.
  > - **`IsReadOnly` / `AllowSorting` / `AllowFiltering`**: MAUI exposes these as plain flags with no
  >   `propertyChanged` (read at gesture time). Here each callback just repaints so any affordance
  >   stays in sync; the read-only/sort/filter gesture enforcement is wired in Phase 04.
  > - `MAUI`'s `[ContentProperty(nameof(Columns))]` was **not** ported — WinUI's XAML content-property
  >   model differs and it isn't needed for Phase 03 (Phase 06 sample binds `Columns` explicitly).

- [x] Registration pattern (one example — repeat per property), implemented in the
  `DataGridView.Properties.cs` partial:
  ```csharp
  public static readonly DependencyProperty ItemsSourceProperty =
      DependencyProperty.Register(
          nameof(ItemsSource), typeof(IEnumerable), typeof(DataGridView),
          new PropertyMetadata(null, OnItemsSourceChanged));

  public IEnumerable? ItemsSource
  {
      get => (IEnumerable?)GetValue(ItemsSourceProperty);
      set => SetValue(ItemsSourceProperty, value);
  }

  private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
  {
      var view = (DataGridView)d;
      // …update _dataSource, (re)subscribe INotifyCollectionChanged…
      view.Invalidate();
  }
  ```
  > WinUI callbacks are `static` and receive the instance via `d` — unlike MAUI's `(bindable, old, new)`
  > signature. The body logic is otherwise identical to the MAUI `propertyChanged` handlers.

## 6. Public event parity

- [x] Re-expose the same public events MAUI offers, forwarding from `InputController` / `_editSession`
  (see [`DataGridView.cs`](../../src/KumikoUI.Maui/DataGridView.cs) lines ~1009–1037):
  `RowTapped`, `RowDoubleTapped` (→ `_inputController`), `CellBeginEdit`, `CellEndEdit`,
  `CellValueChanged` (→ `_editSession`). All five are implemented as add/remove forwarders, so the
  events fire as soon as Phase 04 wires the input/edit gesture paths.
  ```csharp
  public event EventHandler<RowTappedEventArgs2>? RowTapped
  {
      add    => _inputController.RowTapped += value;
      remove => _inputController.RowTapped -= value;
  }
  ```

## 7. Lifecycle

- [x] On `Unloaded`: unsubscribe `PaintSurface` (its delegate roots `this`), plus — in Phase 04 —
  pointer/keyboard handlers and any input-driven timers. The Core redraw-signal subscriptions
  (`_dataSource` / `_inputController` / `_editSession`) target objects this control *owns*, so they
  don't root the page and are left attached across navigation (re-subscribing them in `OnLoaded`
  would risk double-firing). A `// Phase 04:` seam in `OnUnloaded` marks where the input teardown
  lands.
- [x] On `Loaded`: idempotently (re)attach `PaintSurface` (detach-then-attach) and request a repaint
  at the current DPI. A `// Phase 04:` seam marks where pointer/keyboard (re)attach lands. Uno raises
  `Loaded`/`Unloaded` on navigation, so the attach/detach is written to be idempotent.

## 8. Hosting extension

- [x] Add `KumikoUIHostingExtensions` mirroring
  [`DataGridHostingExtensions.cs`](../../src/KumikoUI.Maui/DataGridHostingExtensions.cs). Uno apps
  configure services on the `IHostBuilder` from `IApplicationBuilder`:
  ```csharp
  using Microsoft.Extensions.Hosting;

  public static class KumikoUIHostingExtensions
  {
      // Place to register fonts / services; keeps app startup symmetric with MAUI's UseSkiaKumikoUI()
      public static IHostBuilder UseKumikoUI(this IHostBuilder host) => host;
  }
  ```
  > Unlike MAUI, SkiaSharp needs no `UseSkiaSharp()` initializer on Uno — `SKXamlCanvas` self-hosts.
  > The extension still earns its place as the documented spot for font registration (see [05](05-fonts-and-assets.md)).
  > Deviation: the Uno.Sdk library head does **not** transitively reference the hosting abstractions,
  > so `IHostBuilder` was unresolved. Added a direct `PackageReference` to
  > `Microsoft.Extensions.Hosting.Abstractions` (9.0.4) in `KumikoUI.Uno.csproj` — it carries only the
  > `IHostBuilder` contract (no runtime), so the documented signature stays intact and the extension
  > stays minimal.

---

## ✅ Exit criteria

- [x] `DataGridView` compiles for `net9.0-desktop` with the paint loop + DP surface + lifecycle fully
  wired (`dotnet build … -f net9.0-desktop` → Build succeeded, 0 warnings, 0 errors). Visual
  rendering of a bound `ItemsSource` + `Columns` is verified in Phase 06 (needs the sample app).
- [x] Each `DependencyProperty` `PropertyChangedCallback` mutates the matching Core state and calls a
  single `Invalidate()` (`_canvasView.Invalidate()`). Visible update is confirmed in Phase 06.
- [x] No SkiaSharp/Core source was modified — only `KumikoUI.Uno` files were added
  (`DataGridView.cs`, `DataGridView.Properties.cs`, `KumikoUIHostingExtensions.cs`) plus one
  `PackageReference` in `KumikoUI.Uno.csproj`.
- [ ] Rendering is crisp at 100/150/200% display scale.
  > **Deferred to Phase 06** (requires a live surface from the sample app). The per-frame
  > `canvas.Scale(e.Info.Width / ActualWidth)` is in place so the output is resolution-independent.

> **Phase 04 carry-over (intentionally left unchecked above):** pointer + keyboard event wiring
> (`PointerPressed`/`PointerMoved`/`PointerReleased`/`PointerWheelChanged`, `KeyDown`,
> `CharacterReceived`), the `KeyboardFocusRequested` / `FilterPopupOpened` / `FilterPopupClosed`
> handler bodies, input-driven timers (inertial scroll, cursor blink, long-press), `IsReadOnly` /
> sort / filter gesture enforcement, `XamlRoot`/`RasterizationScale` DPI-change re-invalidation, and
> hit-test alignment verification.

➡️ Next: [04 — Input, keyboard & focus](04-input-keyboard-focus.md)

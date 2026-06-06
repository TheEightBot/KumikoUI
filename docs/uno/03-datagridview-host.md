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

- [ ] Declare the control deriving from a panel so the canvas can be added as a child (MAUI derives
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
- [ ] The `unolib` template ships a sample `MyTemplatedControl` + `Themes/Generic.xaml`. Remove the
  sample control. Keep an (empty) `Themes/Generic.xaml` only if you later add stylable resources;
  it is **not** required by the drawn `DataGridView`.

## 2. Constructor wiring (parity with MAUI ctor)

- [ ] Configure and add the canvas, then subscribe to paint + `GridInputController` events:
  ```csharp
  public DataGridView()
  {
      _canvasView.HorizontalAlignment = HorizontalAlignment.Stretch;
      _canvasView.VerticalAlignment   = VerticalAlignment.Stretch;
      Children.Add(_canvasView);

      _canvasView.PaintSurface += OnPaintSurface;

      _inputController.NeedsRedraw          += OnInputControllerNeedsRedraw;
      _inputController.ColumnReordered      += OnColumnReordered;
      _inputController.RowReordered         += OnRowReordered;
      _inputController.AutoFitColumnRequested += OnAutoFitColumnRequested;
      _inputController.KeyboardFocusRequested += OnKeyboardFocusRequested;
      _inputController.FilterPopupOpened    += OnFilterPopupOpened;
      _inputController.FilterPopupClosed    += OnFilterPopupClosed;
      _inputController.EditSession = _editSession;

      Loaded   += OnLoaded;     // (re)attach pointer/keyboard handlers — see 04
      Unloaded += OnUnloaded;   // detach everything (mirror MAUI lines ~928/990)
  }
  ```

## 3. The paint loop (reuse SkiaSharp + Core verbatim)

The event and args are the **same types** MAUI uses, so the body is copied almost verbatim from
[`DataGridView.OnPaintSurface`](../../src/KumikoUI.Maui/DataGridView.cs) (line ~610) — only DPI scaling
differs (see step 4).

- [ ] Implement:
  ```csharp
  private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
  {
      var canvas = e.Surface.Canvas;

      var bg = _style.BackgroundColor;            // Core GridColor
      canvas.Clear(new SKColor(bg.R, bg.G, bg.B, bg.A));

      // DPI: e.Info is in physical pixels; ActualWidth/Height are logical DIPs (step 4)
      float scale = ActualWidth > 0 ? (float)(e.Info.Width / ActualWidth) : 1f;
      canvas.Scale(scale);
      float canvasWidth  = (float)(ActualWidth  > 0 ? ActualWidth  : e.Info.Width);
      float canvasHeight = (float)(ActualHeight > 0 ? ActualHeight : e.Info.Height);

      using var drawingContext = new SkiaDrawingContext(canvas);   // REUSED from KumikoUI.SkiaSharp
      _renderer.Render(                                            // REUSED from KumikoUI.Core
          drawingContext, _dataSource, _scroll, _selection, _style,
          _inputController.DragColumnIndex, _inputController.DragColumnScreenX,
          _inputController.DragRowIndex,    _inputController.DragRowScreenY,
          _editSession, _inputController.PopupManager);
  }
  ```
- [ ] Request a redraw on every Core "invalidate" signal:
  ```csharp
  private void InvalidateSurface() => _canvasView.Invalidate();          // MAUI: _canvasView.InvalidateSurface()
  private void OnInputControllerNeedsRedraw() => _canvasView.Invalidate();
  private void OnEditSessionNeedsRedraw()      => _canvasView.Invalidate();
  ```

## 4. DPI / scaling

`SKXamlCanvas.PaintSurface` provides `e.Info` in **device pixels**, while `ActualWidth`/`ActualHeight`
are **logical DIPs**. KumikoUI's renderer is resolution-independent and is fed logical units (as in
MAUI). Bridge the two with a single `canvas.Scale(scale)` where `scale = e.Info.Width / ActualWidth`.

- [ ] Apply the scale transform before rendering (shown above).
- [ ] Re-invalidate on DPI changes: handle `XamlRoot.Changed` (or `RasterizationScale` changes) and
  call `Invalidate()`.
- [ ] **Verify** text stays crisp and hit-testing aligns with drawn cells at 100%, 150%, and 200% scale.

## 5. `DependencyProperty` surface (parity with MAUI `BindableProperty`s)

Map each MAUI `BindableProperty` to a WinUI `DependencyProperty`. Same names, same defaults; the
`PropertyChangedCallback` mutates Core state and calls `Invalidate()`.

- [ ] Register these (1:1 with [`DataGridView.cs`](../../src/KumikoUI.Maui/DataGridView.cs)):

  | DependencyProperty | Type | Notes |
  |---|---|---|
  | `ItemsSource` | `IEnumerable` | hook `INotifyCollectionChanged` like MAUI; push to `DataGridSource` |
  | `Columns` | `ObservableCollection<DataGridColumn>` | Core type; reflows layout |
  | `GridSelectionMode` | `SelectionMode` (Core) | |
  | `RowHeight` / `HeaderHeight` | `double` | |
  | `FrozenRowCount` | `int` | |
  | `EditTriggers` | `EditTriggers` (Core) | |
  | `EditTextSelectionMode` | enum | |
  | `IsReadOnly` | `bool` | |
  | `AllowSorting` / `AllowFiltering` | `bool` | |
  | `DismissKeyboardOnEnter` | `bool` | |
  | `GridDescription` | Core type | |
  | `TableSummaryRows` | Core collection | |

- [ ] Registration pattern (one example — repeat per property):
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

- [ ] Re-expose the same public events MAUI offers, forwarding from `InputController` / `_editSession`
  (see [`DataGridView.cs`](../../src/KumikoUI.Maui/DataGridView.cs) lines ~1009–1037):
  `RowTapped`, `RowDoubleTapped`, `CellBeginEdit`, `CellEndEdit`, `CellValueChanged`.
  ```csharp
  public event EventHandler<RowTappedEventArgs2>? RowTapped
  {
      add    => _inputController.RowTapped += value;
      remove => _inputController.RowTapped -= value;
  }
  ```

## 7. Lifecycle

- [ ] On `Unloaded`: unsubscribe `PaintSurface`, all `_inputController` events, pointer/keyboard
  handlers (04), and stop timers — mirroring MAUI's teardown (line ~928) to avoid leaks when the page
  is navigated away.
- [ ] On `Loaded`: (re)attach the same handlers (MAUI line ~990). Uno raises `Loaded`/`Unloaded` on
  navigation, so attach/detach must be idempotent.

## 8. Hosting extension

- [ ] Add `KumikoUIHostingExtensions` mirroring
  [`DataGridHostingExtensions.cs`](../../src/KumikoUI.Maui/DataGridHostingExtensions.cs). Uno apps
  configure services on the `IHostBuilder` from `IApplicationBuilder`:
  ```csharp
  public static class KumikoUIHostingExtensions
  {
      // Place to register fonts / services; keeps app startup symmetric with MAUI's UseSkiaKumikoUI()
      public static IHostBuilder UseKumikoUI(this IHostBuilder host) => host;
  }
  ```
  > Unlike MAUI, SkiaSharp needs no `UseSkiaSharp()` initializer on Uno — `SKXamlCanvas` self-hosts.
  > The extension still earns its place as the documented spot for font registration (see [05](05-fonts-and-assets.md)).

---

## ✅ Exit criteria

- [ ] `DataGridView` renders a static grid (bind `ItemsSource` + `Columns`) on `net9.0-desktop`.
- [ ] Changing any `DependencyProperty` triggers a single `Invalidate()` and visibly updates.
- [ ] No SkiaSharp/Core source was modified — only `KumikoUI.Uno` files were added.
- [ ] Rendering is crisp at 100/150/200% display scale.

➡️ Next: [04 — Input, keyboard & focus](04-input-keyboard-focus.md)

using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using KumikoUI.Core;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Layout;
using KumikoUI.Core.Models;
using KumikoUI.SkiaSharp;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SkiaSharp;
using SkiaSharp.Views.Windows; // SKXamlCanvas + SKPaintSurfaceEventArgs

namespace KumikoUI.Uno;

/// <summary>
/// Uno Platform / WinUI KumikoUI DataGrid control backed by SkiaSharp rendering.
/// Drop this into any Uno page. The entire grid is drawn onto a single
/// <see cref="SKXamlCanvas"/> — there are no templated parts or visual
/// states. The canvas and the keyboard proxy are the control's private content: it has no
/// layout API, and its XAML content is <see cref="Columns"/>. Mirrors
/// <c>KumikoUI.Maui.KumikoDataGrid</c> (a <c>ContentView</c> with private content) so the reused
/// <see cref="KumikoRenderer"/> / <see cref="KumikoInputController"/> drive the
/// same way on both platforms.
/// </summary>
/// <remarks>
/// Layout host + paint loop + DependencyProperty surface + lifecycle live in this file and
/// <c>KumikoDataGrid.Properties.cs</c>. Pointer / keyboard / focus event wiring and the
/// input-driven repaint timers live in <c>KumikoDataGrid.Input.cs</c> (attached from
/// <see cref="OnLoaded"/>, detached from <see cref="OnUnloaded"/>) and the mapping helpers in
/// <c>Input/InputMapping.cs</c>.
/// </remarks>
[Microsoft.UI.Xaml.Markup.ContentProperty(Name = nameof(Columns))]
public partial class KumikoDataGrid : UserControl
{
    private readonly Grid _root = new();
    private readonly SKXamlCanvas _canvasView = new();
    private readonly KumikoRenderer _renderer = new();
    private readonly KumikoSource _dataSource = new();
    private readonly ScrollState _scroll = new();
    private readonly KumikoSelectionModel _selection;
    private readonly KumikoInputController _inputController = new();
    private readonly EditSession _editSession = new();
    private KumikoStyle _style = new();

    // Native Skia objects (paints, fonts, typefaces) reused across frames (#62). Created on first
    // use, disposed in OnUnloaded and created again on the next frame after OnLoaded.
    private SkiaResourceCache? _resourceCache;

    private SkiaResourceCache ResourceCache => _resourceCache ??= new SkiaResourceCache();

    /// <summary>
    /// Creates a new <see cref="KumikoDataGrid"/>, adds the Skia canvas child, and
    /// wires the Core redraw signals. Pointer / keyboard handlers are attached in
    /// <see cref="OnLoaded"/> (see <c>KumikoDataGrid.Input.cs</c>).
    /// </summary>
    public KumikoDataGrid()
    {
        // The selection follows the source's view changes by itself (item-based, #30).
        _selection = new KumikoSelectionModel(_dataSource);
        InitializeSelectionBinding();

        // Layout: the canvas fills the grid (mirrors MAUI's LayoutOptions.Fill).
        _canvasView.HorizontalAlignment = HorizontalAlignment.Stretch;
        _canvasView.VerticalAlignment = VerticalAlignment.Stretch;
        _root.Children.Add(_canvasView);
        Content = _root;
        UseSystemFocusVisuals = false; // the grid draws its own focus (the current cell)

        // Add the hidden TextBox proxy for soft-keyboard input (iOS/Android/WASM).
        // Must be added after the canvas so it sits on top in the z-order, though its
        // Opacity=0 / 1×1px size means it is not visible regardless.
        SetupInputProxy();

        // The content is the grid's own: replacing it would detach the canvas
        RegisterPropertyChangedCallback(ContentProperty, OnContentReplaced);

        // Auto-init the bindable collections so XAML can add items directly,
        // matching the MAUI control's ctor.
        Columns = new ObservableCollection<KumikoColumn>();
        TableSummaryRows = new ObservableCollection<TableSummaryRow>();

        // Grouping collections, option storage and command wiring (KumikoDataGrid.Options.cs)
        InitializeOptions();

        // Default accessible name + hint refresh (KumikoDataGrid.Accessibility.cs)
        InitializeAccessibility();

        // Core "invalidate" signals → request a single canvas repaint.
        _dataSource.ViewChanged += OnDataSourceViewChanged;

        _inputController.NeedsRedraw += OnInputControllerNeedsRedraw;
        _renderer.RedrawRequested += OnRendererRedrawRequested;
        _inputController.ColumnReordered += OnColumnReordered;
        _inputController.RowReordered += OnRowReordered;
        _inputController.AutoFitColumnRequested += OnAutoFitColumnRequested;
        _inputController.KeyboardFocusRequested += OnKeyboardFocusRequested;
        _inputController.FilterPopupOpened += OnFilterPopupOpened;
        _inputController.FilterPopupClosed += OnFilterPopupClosed;
        _inputController.CursorChanged += OnInputControllerCursorChanged;

        // Wire edit session to input controller and style (mirrors MAUI).
        _inputController.EditSession = _editSession;
        _editSession.Style = _style;
        _editSession.NeedsRedraw += OnEditSessionNeedsRedraw;

        // Edit lifecycle drives the cursor-blink repaint timer (mirrors MAUI). These target the
        // owned _editSession, so they're wired once in the ctor and don't leak the page.
        _editSession.CellBeginEdit += OnEditSessionCellBeginEdit;
        _editSession.CellEndEdit += OnEditSessionCellEndEdit;

        // Canvas paint loop.
        _canvasView.PaintSurface += OnPaintSurface;

        // Loaded/Unloaded attach/detach the paint loop and the pointer/keyboard handlers
        // idempotently — Uno raises these on navigation (wiring in KumikoDataGrid.Input.cs).
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    // ── Public API (parity with MAUI) ─────────────────────────────

    /// <summary>Gets the underlying data source.</summary>
    public KumikoSource DataSource => _dataSource;

    /// <summary>Gets the selection model.</summary>
    public KumikoSelectionModel Selection => _selection;

    /// <summary>Gets the input controller for advanced event wiring.</summary>
    public KumikoInputController InputController => _inputController;

    /// <summary>Gets the edit session for inline cell editing.</summary>
    public EditSession EditSession => _editSession;

    /// <summary>Configurable scroll/inertia settings.</summary>
    public ScrollSettings ScrollSettings
    {
        get => _inputController.ScrollSettings;
        set => _inputController.ScrollSettings = value;
    }

    /// <summary>
    /// Gets or sets the grid style / theme. Style-backed properties you set explicitly
    /// (<see cref="ShowHorizontalGridLines"/> and the others) are re-applied to the style you assign.
    /// </summary>
    public KumikoStyle GridStyle
    {
        get => _style;
        set
        {
            _style = value;
            ApplyStyleOptions();
            _editSession.Style = value;
            InvalidateSurface();
        }
    }

    /// <summary>
    /// Applies a built-in theme preset. Sets <see cref="GridStyle"/> to a preconfigured
    /// <see cref="KumikoStyle"/> for the chosen theme; style-backed properties you set explicitly
    /// (<see cref="ShowHorizontalGridLines"/> and the others) are re-applied on top of it.
    /// </summary>
    public KumikoThemeMode Theme
    {
        get => _themeMode;
        set
        {
            if (_themeMode == value) return;
            _themeMode = value;
            GridStyle = KumikoTheme.Create(value);
        }
    }
    private KumikoThemeMode _themeMode = KumikoThemeMode.Light;

    private void OnContentReplaced(DependencyObject sender, DependencyProperty property)
    {
        if (ReferenceEquals(Content, _root)) return;
        Content = _root;
        throw new InvalidOperationException(
            "KumikoDataGrid draws its own content; it cannot be replaced. Add columns through Columns.");
    }

    // ── Rendering ─────────────────────────────────────────────────

    /// <summary>
    /// Paints the entire grid onto the Skia surface. The renderer is fed logical
    /// (DIP) units; a single <c>canvas.Scale(scale)</c> bridges device pixels
    /// (<c>e.Info</c>) to logical units (<see cref="FrameworkElement.ActualWidth"/>).
    /// </summary>
    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var info = e.Info;

        var bg = _style.BackgroundColor;
        canvas.Clear(new SKColor(bg.R, bg.G, bg.B, bg.A));

        // DPI: e.Info is in physical pixels; ActualWidth/Height are logical DIPs.
        // KumikoUI's renderer is resolution-independent and fed logical units.
        float scale = ActualWidth > 0 ? (float)(info.Width / ActualWidth) : 1f;
        float viewportWidth = (float)(ActualWidth > 0 ? ActualWidth : info.Width / scale);
        float viewportHeight = (float)(ActualHeight > 0 ? ActualHeight : info.Height / scale);

        _scroll.ViewportWidth = viewportWidth;
        _scroll.ViewportHeight = viewportHeight;

        canvas.Save();
        canvas.Scale(scale);

        using var drawingContext = new SkiaDrawingContext(canvas, ResourceCache);

        _renderer.Render(drawingContext, _dataSource, _scroll, _selection, _style,
            _inputController.DragColumnIndex, _inputController.DragColumnScreenX,
            _inputController.DragRowIndex, _inputController.DragRowScreenY,
            _editSession, _inputController.PopupManager);

        canvas.Restore();
    }

    /// <summary>Invalidate the canvas surface to trigger a repaint (MAUI: <c>InvalidateSurface()</c>).</summary>
    private void InvalidateSurface() => _canvasView.Invalidate();

    // ── Core redraw signal handlers ───────────────────────────────
    // Named handlers (not lambdas) so they unsubscribe cleanly in OnUnloaded.

    private void OnDataSourceViewChanged(object? sender, KumikoViewChangedEventArgs e)
    {
        _canvasView.Invalidate();
    }

    private void OnInputControllerNeedsRedraw() => _canvasView.Invalidate();

    private void OnEditSessionNeedsRedraw() => _canvasView.Invalidate();

    // An action button's command raised CanExecuteChanged: that can happen on any thread.
    private void OnRendererRedrawRequested()
    {
        if (DispatcherQueue is { } queue && !queue.HasThreadAccess)
            queue.TryEnqueue(() => _canvasView.Invalidate());
        else
            _canvasView.Invalidate();
    }

    // ── Core structural event handlers (forwarded into the data source) ──

    private void OnColumnReordered(object? sender, KumikoColumnReorderedEventArgs e)
        => _dataSource.ReorderColumn(e.OldIndex, e.NewIndex);

    private void OnRowReordered(object? sender, RowReorderedEventArgs e)
        => _dataSource.ReorderRow(e.OldIndex, e.NewIndex);

    private void OnAutoFitColumnRequested(object? sender, AutoFitColumnEventArgs e)
    {
        // Use a throwaway 1x1 Skia surface for text measurement (mirrors MAUI).
        using var bitmap = new SKBitmap(1, 1);
        using var canvas = new SKCanvas(bitmap);
        using var measureCtx = new SkiaDrawingContext(canvas, ResourceCache);

        // Samples the rows on screen plus the first and last rows of the view, never every row (#24); a Star or
        // Auto column becomes Fixed at the fitted width so it keeps it.
        _renderer.AutoFitColumn(e.Column, _dataSource, measureCtx, _style);
        InvalidateSurface();
    }

    // ── Core → platform callbacks (input wiring lives in KumikoDataGrid.Input.cs) ──

    /// <summary>
    /// Core asks for keyboard focus (e.g. editing began, or a filter popup opened on mobile where
    /// the input pane must be shown). Delegates to <see cref="FocusKeyboardInput"/> which focuses the
    /// hidden TextBox proxy — on iOS/Android that summons the soft keyboard; on desktop it routes
    /// subsequent <c>KeyDown</c>/<c>CharacterReceived</c> to the proxy (which forwards to Core).
    /// </summary>
    private void OnKeyboardFocusRequested() => FocusKeyboardInput();

    /// <summary>A filter popup opened: start the cursor-blink repaint timer so its search caret blinks.</summary>
    private void OnFilterPopupOpened()
    {
        _filterPopupActive = true;
        StartCursorBlinkTimer();
    }

    /// <summary>The filter popup closed: stop the cursor-blink timer (unless a cell edit is still active).</summary>
    private void OnFilterPopupClosed()
    {
        _filterPopupActive = false;
        if (!_editSession.IsEditing)
            StopCursorBlinkTimer();
    }

    /// <summary>
    /// Begin-edit: start the cursor-blink timer and focus the hidden TextBox proxy so the
    /// soft keyboard appears on iOS/Android (mirrors MAUI's <c>FocusKeyboardInput</c> call).
    /// </summary>
    private void OnEditSessionCellBeginEdit(object? sender, CellBeginEditEventArgs e)
    {
        if (e.Cancel) return;
        StartCursorBlinkTimer();
        // FocusKeyboardInput resets the proxy sentinel and focuses the TextBox.
        // On iOS/Android this summons the on-screen keyboard; on desktop it routes
        // subsequent key events to the proxy → OnProxyKeyDown / OnProxyTextChanged.
        FocusKeyboardInput();
    }

    /// <summary>
    /// End-edit: reset the proxy, stop the cursor-blink timer (unless a filter popup is still
    /// open), and return focus appropriately based on input device:
    /// touch → focus the grid (blurs the proxy and dismisses the soft keyboard on mobile);
    /// mouse/pen → re-focus the proxy so arrow-key navigation and type-to-edit keep working
    /// immediately without requiring another click.
    /// </summary>
    private void OnEditSessionCellEndEdit(object? sender, CellEndEditEventArgs e)
    {
        _selection.IsEditing = false;
        // Reset proxy text so no stale characters linger if the same proxy is re-focused.
        ResetProxy();
        if (!_filterPopupActive)
            StopCursorBlinkTimer();
        // Device-aware focus: on touch, blur the proxy to dismiss the soft keyboard;
        // on mouse/pen, keep the proxy focused so keyboard navigation continues seamlessly.
        if (_lastPointerWasTouch)
            Focus(FocusState.Programmatic);   // touch: blur proxy → soft keyboard dismisses
        else
            FocusKeyboardInput();             // mouse/pen: proxy stays focused → arrows/typing keep working
    }

    // ── Public events (parity with MAUI) ──────────────────────────

    /// <summary>Fires when a row is tapped.</summary>
    public event EventHandler<RowTappedEventArgs>? RowTapped
    {
        add => _inputController.RowTapped += value;
        remove => _inputController.RowTapped -= value;
    }

    /// <summary>Fires when a row is double-tapped.</summary>
    public event EventHandler<RowTappedEventArgs>? RowDoubleTapped
    {
        add => _inputController.RowDoubleTapped += value;
        remove => _inputController.RowDoubleTapped -= value;
    }

    /// <summary>Fires before a cell enters edit mode. Set <c>Cancel=true</c> to prevent.</summary>
    public event EventHandler<CellBeginEditEventArgs>? CellBeginEdit
    {
        add => _editSession.CellBeginEdit += value;
        remove => _editSession.CellBeginEdit -= value;
    }

    /// <summary>Fires after a cell exits edit mode.</summary>
    public event EventHandler<CellEndEditEventArgs>? CellEndEdit
    {
        add => _editSession.CellEndEdit += value;
        remove => _editSession.CellEndEdit -= value;
    }

    /// <summary>Fires when a cell value changes via editing.</summary>
    public event EventHandler<CellValueChangedEventArgs>? CellValueChanged
    {
        add => _editSession.CellValueChanged += value;
        remove => _editSession.CellValueChanged -= value;
    }

    // ── Lifecycle ─────────────────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Re-attach the paint loop in case Unloaded detached it on a prior
        // navigation. Idempotent: detach first to avoid a double subscription.
        _canvasView.PaintSurface -= OnPaintSurface;
        _canvasView.PaintSurface += OnPaintSurface;
        _renderer.RedrawRequested -= OnRendererRedrawRequested;
        _renderer.RedrawRequested += OnRendererRedrawRequested;

        // Resume following the app's collection if Unloaded detached it (no-op on first load).
        _dataSource.Attach();

        // Re-render with the current DPI now that we are in the visual tree.
        InvalidateSurface();

        // (Re)attach pointer + keyboard handlers and enable focusability. Idempotent —
        // see AttachInputHandlers in KumikoDataGrid.Input.cs.
        AttachInputHandlers();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Detach the paint loop. The Core redraw-signal subscriptions target
        // owned objects (this control owns _dataSource/_inputController/
        // _editSession), so they don't leak the page — but the canvas's
        // PaintSurface delegate roots `this`, so it must be released.
        _canvasView.PaintSurface -= OnPaintSurface;

        // Stop observing the app's collection and its items so they do not keep this grid's source alive;
        // OnLoaded re-attaches (the control can be reloaded, so the source is detached, not disposed).
        _dataSource.Detach();

        // Stop listening to action-button commands (a cell renderer can outlive this grid). The renderer
        // re-attaches to them on the next paint after a reload.
        _renderer.RedrawRequested -= OnRendererRedrawRequested;
        _renderer.Dispose();

        // Release the cached native Skia objects; the first frame after OnLoaded creates a new cache.
        _resourceCache?.Dispose();
        _resourceCache = null;

        // Detach pointer + keyboard handlers and stop input-driven timers
        // (inertial scroll, cursor blink) — see DetachInputHandlers in KumikoDataGrid.Input.cs.
        DetachInputHandlers();
    }
}

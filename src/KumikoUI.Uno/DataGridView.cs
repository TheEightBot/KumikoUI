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

// Disambiguate Core's selection types from the WinUI controls of the same name
// (Microsoft.UI.Xaml.Controls.SelectionModel / SelectionMode), which are in scope
// here because the control derives from Microsoft.UI.Xaml.Controls.Grid.
using SelectionModel = KumikoUI.Core.Models.SelectionModel;

namespace KumikoUI.Uno;

/// <summary>
/// Uno Platform / WinUI KumikoUI control backed by SkiaSharp rendering.
/// Drop this into any Uno page. The entire grid is drawn onto a single
/// <see cref="SKXamlCanvas"/> child — there are no templated parts or visual
/// states. Mirrors <c>KumikoUI.Maui.DataGridView</c> (which derives from
/// <c>Grid</c> and hosts a single canvas) so the reused
/// <see cref="DataGridRenderer"/> / <see cref="GridInputController"/> drive the
/// same way on both platforms.
/// </summary>
/// <remarks>
/// Phase 03 scope: layout host + paint loop + DependencyProperty surface +
/// lifecycle. Pointer / keyboard event wiring lives in Phase 04 (see the
/// <c>// Phase 04:</c> seam in the constructor and <see cref="OnLoaded"/>).
/// </remarks>
public partial class DataGridView : Grid
{
    private readonly SKXamlCanvas _canvasView = new();
    private readonly DataGridRenderer _renderer = new();
    private readonly DataGridSource _dataSource = new();
    private readonly ScrollState _scroll = new();
    private readonly SelectionModel _selection = new();
    private readonly GridInputController _inputController = new();
    private readonly EditSession _editSession = new();
    private DataGridStyle _style = new();

    /// <summary>
    /// Creates a new <see cref="DataGridView"/>, adds the Skia canvas child, and
    /// wires the Core redraw signals. Pointer / keyboard handlers are attached in
    /// <see cref="OnLoaded"/> (Phase 04).
    /// </summary>
    public DataGridView()
    {
        // Layout: the canvas fills the grid (mirrors MAUI's LayoutOptions.Fill).
        _canvasView.HorizontalAlignment = HorizontalAlignment.Stretch;
        _canvasView.VerticalAlignment = VerticalAlignment.Stretch;
        Children.Add(_canvasView);

        // Auto-init the bindable collections so XAML can add items directly,
        // matching the MAUI control's ctor.
        Columns = new ObservableCollection<DataGridColumn>();
        TableSummaryRows = new ObservableCollection<TableSummaryRow>();

        // Core "invalidate" signals → request a single canvas repaint.
        _dataSource.DataChanged += OnDataSourceDataChanged;

        _inputController.NeedsRedraw += OnInputControllerNeedsRedraw;
        _inputController.ColumnReordered += OnColumnReordered;
        _inputController.RowReordered += OnRowReordered;
        _inputController.AutoFitColumnRequested += OnAutoFitColumnRequested;
        _inputController.KeyboardFocusRequested += OnKeyboardFocusRequested;
        _inputController.FilterPopupOpened += OnFilterPopupOpened;
        _inputController.FilterPopupClosed += OnFilterPopupClosed;

        // Wire edit session to input controller and style (mirrors MAUI).
        _inputController.EditSession = _editSession;
        _editSession.Style = _style;
        _editSession.NeedsRedraw += OnEditSessionNeedsRedraw;

        // Canvas paint loop.
        _canvasView.PaintSurface += OnPaintSurface;

        // Loaded/Unloaded attach/detach handlers idempotently — Uno raises these
        // on navigation. Pointer/keyboard wiring is added in Phase 04.
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    // ── Public API (parity with MAUI) ─────────────────────────────

    /// <summary>Gets the underlying data source.</summary>
    public DataGridSource DataSource => _dataSource;

    /// <summary>Gets the selection model.</summary>
    public SelectionModel Selection => _selection;

    /// <summary>Gets the input controller for advanced event wiring.</summary>
    public GridInputController InputController => _inputController;

    /// <summary>Gets the edit session for inline cell editing.</summary>
    public EditSession EditSession => _editSession;

    /// <summary>Configurable scroll/inertia settings.</summary>
    public ScrollSettings ScrollSettings
    {
        get => _inputController.ScrollSettings;
        set => _inputController.ScrollSettings = value;
    }

    /// <summary>Gets or sets the grid style / theme.</summary>
    public DataGridStyle GridStyle
    {
        get => _style;
        set
        {
            _style = value;
            _editSession.Style = value;
            InvalidateSurface();
        }
    }

    /// <summary>Set the data items imperatively (alternative to <see cref="ItemsSource"/>).</summary>
    public void SetItemsSource(IEnumerable items) => _dataSource.SetItems(items);

    /// <summary>Set the column definitions imperatively (alternative to <see cref="Columns"/>).</summary>
    public void SetColumns(IEnumerable<DataGridColumn> columns)
    {
        _dataSource.SetColumns(columns);
        InvalidateSurface();
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

        using var drawingContext = new SkiaDrawingContext(canvas);

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

    private void OnDataSourceDataChanged() => _canvasView.Invalidate();

    private void OnInputControllerNeedsRedraw() => _canvasView.Invalidate();

    private void OnEditSessionNeedsRedraw() => _canvasView.Invalidate();

    // ── Core structural event handlers (forwarded into the data source) ──

    private void OnColumnReordered(object? sender, ColumnReorderedEventArgs e)
        => _dataSource.ReorderColumn(e.OldIndex, e.NewIndex);

    private void OnRowReordered(object? sender, RowReorderedEventArgs e)
        => _dataSource.ReorderRow(e.OldIndex, e.NewIndex);

    private void OnAutoFitColumnRequested(object? sender, AutoFitColumnEventArgs e)
    {
        // Use a throwaway 1x1 Skia surface for text measurement (mirrors MAUI).
        using var bitmap = new SKBitmap(1, 1);
        using var canvas = new SKCanvas(bitmap);
        var measureCtx = new SkiaDrawingContext(canvas);
        var layoutEngine = new GridLayoutEngine();

        float optimalWidth = layoutEngine.CalculateAutoFitWidth(
            e.Column, _dataSource, measureCtx, _style);

        e.Column.Width = optimalWidth;
        InvalidateSurface();
    }

    // Phase 04: these are surfaced by the input controller but only become
    // meaningful once pointer/keyboard events are wired. They are subscribed
    // here so the controller can already drive the platform layer (e.g. show
    // the soft keyboard); the no-op bodies are fleshed out in Phase 04.
    private void OnKeyboardFocusRequested()
    {
        // Phase 04: focus the keyboard input sink so the soft keyboard appears.
    }

    private void OnFilterPopupOpened()
    {
        // Phase 04: start the cursor-blink repaint timer while the popup is open.
    }

    private void OnFilterPopupClosed()
    {
        // Phase 04: stop the cursor-blink repaint timer.
    }

    // ── Public events (parity with MAUI) ──────────────────────────

    /// <summary>Fires when a row is tapped.</summary>
    public event EventHandler<RowTappedEventArgs2>? RowTapped
    {
        add => _inputController.RowTapped += value;
        remove => _inputController.RowTapped -= value;
    }

    /// <summary>Fires when a row is double-tapped.</summary>
    public event EventHandler<RowTappedEventArgs2>? RowDoubleTapped
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

        // Re-render with the current DPI now that we are in the visual tree.
        InvalidateSurface();

        // Phase 04: (re)attach pointer + keyboard handlers here
        //   (PointerPressed/PointerMoved/PointerReleased/PointerWheelChanged,
        //    KeyDown, CharacterReceived) and start any input-driven timers.
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Detach the paint loop. The Core redraw-signal subscriptions target
        // owned objects (this control owns _dataSource/_inputController/
        // _editSession), so they don't leak the page — but the canvas's
        // PaintSurface delegate roots `this`, so it must be released.
        _canvasView.PaintSurface -= OnPaintSurface;

        // Phase 04: detach pointer + keyboard handlers and stop input-driven
        //   timers (inertial scroll, cursor blink, long-press) here.
    }
}

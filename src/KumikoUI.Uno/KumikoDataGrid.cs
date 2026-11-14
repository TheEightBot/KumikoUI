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

namespace KumikoUI.Uno;

/// <summary>
/// Uno Platform / WinUI KumikoUI DataGrid control backed by SkiaSharp rendering.
/// Drop this into any Uno page. The entire grid is drawn onto a single Skia surface (see
/// <see cref="RenderMode"/>) — there are no templated parts or visual
/// states. The surface and the keyboard proxy are the control's private content: it has no
/// layout API, and its XAML content is <see cref="Columns"/>. Mirrors
/// <c>KumikoUI.Maui.KumikoDataGrid</c> (a <c>ContentView</c> with private content) so the reused
/// <see cref="KumikoRenderer"/> / <see cref="KumikoInputController"/> drive the
/// same way on both platforms.
/// </summary>
/// <remarks>
/// Layout host + DependencyProperty surface + lifecycle live in this file and
/// <c>KumikoDataGrid.Properties.cs</c>; the drawing surface, frame loop, caret blink and DPI handling in
/// <c>KumikoDataGrid.Rendering.cs</c>. Pointer / keyboard / focus event wiring lives in
/// <c>KumikoDataGrid.Input.cs</c> (attached from
/// <see cref="OnLoaded"/>, detached from <see cref="OnUnloaded"/>) and the mapping helpers in
/// <c>Input/InputMapping.cs</c>.
/// </remarks>
[Microsoft.UI.Xaml.Markup.ContentProperty(Name = nameof(Columns))]
public partial class KumikoDataGrid : UserControl
{
    private readonly Grid _root = new();
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
    /// Creates a new <see cref="KumikoDataGrid"/>, adds the Skia surface child, and
    /// wires the Core redraw signals. Pointer / keyboard handlers are attached in
    /// <see cref="OnLoaded"/> (see <c>KumikoDataGrid.Input.cs</c>).
    /// </summary>
    public KumikoDataGrid()
    {
        // The selection follows the source's view changes by itself (item-based, #30).
        _selection = new KumikoSelectionModel(_dataSource);
        InitializeSelectionBinding();

        // Layout: the drawing surface fills the grid (mirrors MAUI's LayoutOptions.Fill); see
        // KumikoDataGrid.Rendering.cs for how the surface is chosen.
        InitializeSurface();
        Content = _root;
        UseSystemFocusVisuals = false; // the grid draws its own focus (the current cell)
        // Focusable, so it takes part in tab navigation and receives KeyDown. Set once: a consumer's
        // IsTabStop="False" or TabIndex is respected.
        IsTabStop = true;

        // The hidden TextBox proxy and the text-input adapter (KumikoDataGrid.Editing.cs).
        _textInput = SetupInputProxy();
        _syncTextInput = SyncTextInputQueued;

        // The content is the grid's own: replacing it would detach the canvas
        RegisterPropertyChangedCallback(ContentProperty, OnContentReplaced);

        // Auto-init the bindable collections so XAML can add items directly,
        // matching the MAUI control's ctor.
        Columns = new ObservableCollection<KumikoColumn>();
        DataContextChanged += OnGridDataContextChanged;
        TableSummaryRows = new ObservableCollection<TableSummaryRow>();

        // Grouping collections, option storage and command wiring (KumikoDataGrid.Options.cs)
        InitializeOptions();
        InitializeScrolling(); // KumikoDataGrid.Scrolling.cs
        InitializeImages(); // KumikoDataGrid.Images.cs

        // Default accessible name + hint refresh (KumikoDataGrid.Accessibility.cs)
        InitializeAccessibility();

        // Core "invalidate" signals → request a single canvas repaint.
        _dataSource.ViewChanged += OnDataSourceViewChanged;
        // The source is the grid's own, so this never needs removing (KumikoDataGrid.Layout.cs)
        _dataSource.ColumnOrderChanged += OnDataSourceColumnOrderChanged;

        _inputController.NeedsRedraw += OnInputControllerNeedsRedraw;
        _renderer.RedrawRequested += OnRendererRedrawRequested;
        _inputController.ColumnReordered += OnColumnReordered;
        _inputController.AutoFitColumnRequested += OnAutoFitColumnRequested;
        _inputController.KeyboardFocusRequested += OnKeyboardFocusRequested;
        _inputController.FilterPopupOpened += OnFilterPopupOpened;
        _inputController.FilterPopupClosed += OnFilterPopupClosed;
        _inputController.CursorChanged += OnInputControllerCursorChanged;

        // IsEnabled=false stops all input in Core (KumikoDataGrid.Input.cs). Own event: wired once, no leak.
        IsEnabledChanged += OnIsEnabledChanged;

        // Wire edit session to input controller and style (mirrors MAUI).
        _inputController.EditSession = _editSession;
        InitializeClipboard(); // KumikoDataGrid.Clipboard.cs
        _editSession.Style = _style;
        ConnectTheming();
        _editSession.NeedsRedraw += OnEditSessionNeedsRedraw;

        // Edit lifecycle drives the cursor-blink repaint timer (mirrors MAUI). These target the
        // owned _editSession, so they're wired once in the ctor and don't leak the page.
        _editSession.CellBeginEdit += OnEditSessionCellBeginEdit;
        _editSession.CellEndEdit += OnEditSessionCellEndEdit;

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

    private void OnContentReplaced(DependencyObject sender, DependencyProperty property)
    {
        if (ReferenceEquals(Content, _root)) return;
        Content = _root;
        throw new InvalidOperationException(
            "KumikoDataGrid draws its own content; it cannot be replaced. Add columns through Columns.");
    }

    // ── Core structural event handlers (forwarded into the data source) ──

    private void OnColumnReordered(object? sender, KumikoColumnReorderedEventArgs e)
    {
        _dataSource.ReorderColumn(e.OldIndex, e.NewIndex); // Columns follows (KumikoDataGrid.Layout.cs)
        ColumnReordered?.Invoke(this, e);
    }

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
    /// the input pane must be shown). Delegates to <see cref="FocusKeyboardInput"/> which mirrors the
    /// active text input into the hidden TextBox proxy and focuses it — on iOS/Android that summons the
    /// soft keyboard; on every head it routes subsequent keys and text to the proxy (which forwards to Core).
    /// </summary>
    private void OnKeyboardFocusRequested(object? sender, EventArgs e) => FocusKeyboardInput();

    /// <summary>A filter popup opened: start the cursor-blink repaint timer so its search caret blinks.</summary>
    private void OnFilterPopupOpened(object? sender, EventArgs e)
    {
        _filterPopupActive = true;
        StartCursorBlinkTimer();
        _textInput.Sync(); // the popup's search box is now the active text input
    }

    /// <summary>The filter popup closed: stop the cursor-blink timer (unless a cell edit is still active).</summary>
    private void OnFilterPopupClosed(object? sender, EventArgs e)
    {
        _filterPopupActive = false;
        if (!_editSession.IsEditing)
            StopCursorBlinkTimer();
        _textInput.Sync();
    }

    /// <summary>
    /// Begin-edit: start the cursor-blink timer and focus the hidden TextBox proxy so the
    /// soft keyboard appears on iOS/Android (mirrors MAUI's <c>FocusKeyboardInput</c> call).
    /// </summary>
    private void OnEditSessionCellBeginEdit(object? sender, CellBeginEditEventArgs e)
    {
        if (e.Cancel) return;
        StartCursorBlinkTimer();
        // FocusKeyboardInput mirrors the new editor into the proxy and focuses the TextBox.
        // On iOS/Android this summons the on-screen keyboard; on every head it routes
        // subsequent keys and text to the proxy → OnProxyKeyDown / OnProxyTextChanging.
        FocusKeyboardInput();
    }

    /// <summary>
    /// End-edit: empty the proxy, stop the cursor-blink timer (unless a filter popup is still
    /// open), and keep the keyboard on the proxy so arrow-key navigation and type-to-edit keep
    /// working without another click — also for a hardware keyboard on a touch device. After a
    /// touch edit the soft keyboard is hidden explicitly instead of by moving focus away.
    /// </summary>
    private void OnEditSessionCellEndEdit(object? sender, CellEndEditEventArgs e)
    {
        _selection.IsEditing = false;
        if (!_filterPopupActive)
            StopCursorBlinkTimer();
        if (_lastPointerWasTouch)
        {
            _textInput.Sync();
            HideSoftKeyboard();
        }
        else
        {
            FocusKeyboardInput();
        }
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

    /// <summary>
    /// Fires when a data cell is tapped with the primary button or a finger, with the column, the position and the
    /// click count (see <see cref="KumikoInputController.CellTapped"/>).
    /// </summary>
    public event EventHandler<KumikoCellTappedEventArgs>? CellTapped
    {
        add => _inputController.CellTapped += value;
        remove => _inputController.CellTapped -= value;
    }

    /// <summary>
    /// Fires when a column header is tapped with the primary button or a finger, after the tap sorted the column
    /// (see <see cref="KumikoInputController.HeaderTapped"/>).
    /// </summary>
    public event EventHandler<KumikoHeaderTappedEventArgs>? HeaderTapped
    {
        add => _inputController.HeaderTapped += value;
        remove => _inputController.HeaderTapped -= value;
    }

    /// <summary>
    /// Fires when the user asks for a context menu: a right click (on its release) or a touch or pen long press that
    /// neither starts a row drag nor edits the cell. A right click never sorts, scrolls, drags or edits; on a data
    /// row the row is selected first unless it already is (see <see cref="KumikoContextRequestedEventArgs"/>).
    /// <see cref="KumikoContextRequestedEventArgs.X"/> and <see cref="KumikoContextRequestedEventArgs.Y"/> are relative
    /// to the grid's drawing surface, which fills this control, so a menu opens at the pointer with
    /// <c>flyout.ShowAt(grid, new FlyoutShowOptions { Position = new Point(e.X, e.Y) })</c>. It re-surfaces
    /// <see cref="KumikoInputController.ContextRequested"/> (named so as not to hide WinUI's
    /// <c>UIElement.ContextRequested</c>, which knows nothing of rows and columns).
    /// </summary>
    public event EventHandler<KumikoContextRequestedEventArgs>? ContextMenuRequested
    {
        add => _inputController.ContextRequested += value;
        remove => _inputController.ContextRequested -= value;
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
        // Re-attach the surface's renderer, DPI tracking and any pending frame work (redraws at the
        // current DPI) in case Unloaded detached them on a prior navigation. Idempotent.
        AttachSurface();
        _renderer.RedrawRequested -= OnRendererRedrawRequested;
        _renderer.RedrawRequested += OnRendererRedrawRequested;
        ConnectImages();

        _isUnloaded = false;

        // Resume following the app's collection if Unloaded detached it (no-op on first load), then the
        // collections the grid observes, then the app's SelectedItems list.
        _dataSource.Attach();
        ResubscribeObservedCollections();
        _selectionBinder.Attach();

        // (Re)attach pointer + keyboard handlers and enable focusability. Idempotent —
        // see AttachInputHandlers in KumikoDataGrid.Input.cs.
        AttachInputHandlers();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Stop the frame loop (a static CompositionTarget.Rendering subscription that would root `this`),
        // the caret timer and DPI tracking, and release the surface's renderer. The Core redraw-signal
        // subscriptions target owned objects (this control owns _dataSource/_inputController/
        // _editSession), so they don't leak the page.
        DetachSurface();

        // Stop observing the app's collection and its items so they do not keep this grid's source alive;
        // OnLoaded re-attaches (the control can be reloaded, so the source is detached, not disposed).
        _dataSource.Detach();

        // Unsubscribe the bound items, the Columns/summary/grouping collections and the app's SelectedItems
        // list, which can outlive the page; OnLoaded restores them.
        _isUnloaded = true;
        UnsubscribeObservedCollections();
        _selectionBinder.Detach();

        // Stop listening to action-button commands (a cell renderer can outlive this grid). The renderer
        // re-attaches to them on the next paint after a reload.
        _renderer.RedrawRequested -= OnRendererRedrawRequested;
        _renderer.Dispose();

        // Release the cached native Skia objects; the first frame after OnLoaded creates a new cache.
        _resourceCache?.Dispose();
        _resourceCache = null;

        // Cancel image loads and dispose the cached images; the first frame after OnLoaded loads them again.
        DisconnectImages();

        // Detach pointer + keyboard handlers — see DetachInputHandlers in KumikoDataGrid.Input.cs.
        DetachInputHandlers();
    }
}

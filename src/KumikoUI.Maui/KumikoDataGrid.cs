using KumikoUI.Core;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Layout;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.SkiaSharp;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
#if MACCATALYST || IOS
using UIKit;
using Foundation;
using CoreGraphics;
using ObjCRuntime;
#endif

namespace KumikoUI.Maui;

/// <summary>
/// .NET MAUI KumikoUI DataGrid control backed by SkiaSharp rendering.
/// Drop this into any MAUI page. All rendering is done on the SKCanvas.
/// Keyboard input reaches it through each platform's own text input: a <c>UITextInput</c> responder on iOS and
/// Mac Catalyst, an input view with an <c>InputConnection</c> on Android, and the grid's platform view (WinUI
/// key events and the Windows text services) on Windows.
/// </summary>
/// <remarks>
/// The control is a <see cref="ContentView"/> whose content (the canvas) is private: it has no layout API, and
/// its XAML content is <see cref="Columns"/>.
/// </remarks>
[ContentProperty(nameof(Columns))]
public partial class KumikoDataGrid : ContentView
{
    private readonly Grid _root = new();
    // The view of the drawing surface (an SKCanvasView or an SKGLView, see RenderMode); replaced when the mode changes
    private View _canvasView = null!;
    private readonly KumikoRenderer _renderer = new();
    private readonly KumikoSource _dataSource = new();
    private readonly ScrollState _scroll = new();
    private readonly KumikoSelectionModel _selection;
    private readonly KumikoInputController _inputController = new();
    private readonly EditSession _editSession = new();
    private KumikoStyle _style = new();

    // A filter popup is open (its search box's caret blinks; KumikoDataGrid.Rendering.cs)
    private bool _filterPopupActive;

    // Double-tap detection
    private long _lastTapTimeMs;
    private float _lastTapX, _lastTapY;
    private int _pendingClickCount = 1; // Carries click count from Pressed to Released
    private const long DoubleTapThresholdMs = 400;
    private const float DoubleTapDistanceThreshold = 20f;

    // Long press detection
    private IDispatcherTimer? _longPressTimer;
    private float _longPressX, _longPressY;
    private bool _longPressFired;
    private const long LongPressThresholdMs = 500;
    private const float LongPressMoveTolerance = 15f;

    // The pointer (SKTouchEventArgs.Id) whose press is in progress, and when it was last heard from. Core ignores
    // other pointers by id; the host keeps them out of its click-count and long-press bookkeeping too. A press
    // after the pressed pointer has been silent for a second takes over, in case its release was lost (as Core).
    private long? _pressedPointerId;
    private long _pressedPointerLastMs;
    private const long StalePointerMs = 1000;

    // The device of the press in progress, for the long press synthesized from it
    private KumikoPointerDeviceType _pressedDeviceType;

    // Track whether Cleanup has been called so Reconnect knows to re-wire
    private bool _wasCleanedUp;

    // Native Skia objects (paints, fonts, typefaces) reused across frames (#62). Created on first
    // use, disposed in Cleanup and created again on the next frame after Reconnect.
    private SkiaResourceCache? _resourceCache;

    private SkiaResourceCache ResourceCache => _resourceCache ??= new SkiaResourceCache();

#if MACCATALYST || IOS
    private KeyInputResponder? _nativeKeyInput;
    private UIPanGestureRecognizer? _nativeScrollGesture;
    private UITapGestureRecognizer? _nativeSecondaryClickGesture;
    private bool _useNativeKeyInput;
#endif

    /// <summary>Creates the grid with its drawing surface and empty <see cref="Columns"/>.</summary>
    public KumikoDataGrid()
    {
        // The selection follows the source's view changes by itself (item-based, #30).
        _selection = new KumikoSelectionModel(_dataSource);
        InitializeSelectionBinding();

        // Layout: the drawing surface fills the grid (KumikoDataGrid.Rendering.cs)
        InitializeSurface();
        InitializeAccessibility();
        Content = _root;
        _contentSealed = true;

        // Auto-init the Columns collection so XAML can add items directly
        Columns = new ObservableCollection<KumikoColumn>();

        // Auto-init the TableSummaryRows collection so XAML can add items directly
        TableSummaryRows = new ObservableCollection<TableSummaryRow>();

        // Grouping collections, option storage and command wiring (KumikoDataGrid.Options.cs)
        InitializeOptions();

        // Scroll offsets and the Scrolled event (KumikoDataGrid.Scrolling.cs)
        InitializeScrolling();

        // Image cells load through the grid's image cache (KumikoDataGrid.Images.cs)
        InitializeImages();

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

        // Wire edit session to input controller
        _inputController.EditSession = _editSession;
        InitializeClipboard(); // KumikoDataGrid.Clipboard.cs
        _editSession.Style = _style;
        ConnectTheming();
        _editSession.NeedsRedraw += OnEditSessionNeedsRedraw;

        // Wire edit session lifecycle for cursor blink timer
        _editSession.CellBeginEdit += OnEditSessionCellBeginEdit;
        _editSession.CellEndEdit += OnEditSessionCellEndEdit;


#if MACCATALYST || IOS
        // Set up native keyboard input and scroll gesture after the view is loaded
        this.HandlerChanged += OnHandlerChanged_SetupNativeKeyboard;
        this.HandlerChanged += OnHandlerChanged_SetupNativeScroll;
#endif

#if ANDROID
        // The native input view joins the grid's platform view once the handler is connected (#41)
        this.HandlerChanged += OnHandlerChanged_SetupAndroidTextInput;
#endif
    }

    private readonly bool _contentSealed;

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">When <see cref="ContentView.Content"/> is replaced: the
    /// grid draws everything itself, so it has no content slot. Add columns through <see cref="Columns"/>.</exception>
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == IsEnabledProperty.PropertyName)
            SyncIsEnabled();
        if (_contentSealed && propertyName == ContentProperty.PropertyName && !ReferenceEquals(Content, _root))
        {
            Content = _root;
            throw new InvalidOperationException(
                "KumikoDataGrid draws its own content; it cannot be replaced. Add columns through Columns.");
        }
    }

    // ── MAUI Lifecycle ────────────────────────────────────────────

    /// <inheritdoc/>
    protected override void OnHandlerChanging(HandlerChangingEventArgs args)
    {
        base.OnHandlerChanging(args);

        // Disconnecting from the old handler — tear down everything
        if (args.OldHandler != null)
        {
            Cleanup();
        }
    }

    /// <inheritdoc/>
    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        EnsureHostingConfigured();

        // Connected to a new handler — re-subscribe if previously cleaned up.
        // On first attach, the constructor already did the wiring and _isConnected
        // is still false, so we skip. On subsequent attach (e.g. navigating back),
        // we need to re-wire.
        if (Handler != null && _wasCleanedUp)
        {
            _wasCleanedUp = false;
            Reconnect();
        }
        if (Handler != null) AttachRendering();
#if WINDOWS
        if (Handler != null)
        {
            AttachNativeWheel();
            AttachNativeKeyboard();
        }
#endif
    }

    // ── Public API ────────────────────────────────────────────────

    /// <summary>Gets the underlying data source.</summary>
    public KumikoSource DataSource => _dataSource;

    /// <summary>Gets the selection model.</summary>
    public KumikoSelectionModel Selection => _selection;

    /// <summary>Gets the input controller for advanced event wiring.</summary>
    public KumikoInputController InputController => _inputController;

    /// <summary>Gets the edit session for inline cell editing.</summary>
    public EditSession EditSession => _editSession;

    // ── Bindable Properties ──────────────────────────────────────

    // ── ItemsSource ──

    /// <summary>Identifies the <see cref="ItemsSource"/> bindable property.</summary>
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
        nameof(ItemsSource), typeof(IEnumerable), typeof(KumikoDataGrid),
        propertyChanged: (b, o, n) => ((KumikoDataGrid)b).OnItemsSourceChanged((IEnumerable?)o, (IEnumerable?)n));

    /// <summary>
    /// The items the grid shows, one row each. An <see cref="INotifyCollectionChanged"/> collection is
    /// followed as it changes.
    /// </summary>
    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    private INotifyCollectionChanged? _boundCollectionChangedSource;

    private void OnItemsSourceChanged(IEnumerable? oldValue, IEnumerable? newValue)
    {
        // Unsubscribe from old collection's CollectionChanged at the MAUI layer
        if (_boundCollectionChangedSource != null)
        {
            _boundCollectionChangedSource.CollectionChanged -= OnBoundItemsCollectionChanged;
            _boundCollectionChangedSource = null;
        }

        if (newValue != null)
        {
            _dataSource.SetItems(newValue);

            if (!IsObservingCollections)
            {
                // Set while disconnected: SetItems subscribed, so release it; Reconnect re-attaches
                _dataSource.Detach();
            }
            // Subscribe at MAUI layer for thread-safe invalidation
            else if (newValue is INotifyCollectionChanged incc)
            {
                _boundCollectionChangedSource = incc;
                _boundCollectionChangedSource.CollectionChanged += OnBoundItemsCollectionChanged;
            }
        }
        else
        {
            _dataSource.SetItems(Array.Empty<object>());
        }
    }

    private void OnBoundItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // KumikoSource already handles the rebuild; we just ensure canvas invalidation
        // happens on the UI thread. A change raised on a background thread never gets here:
        // KumikoSource subscribed first and throws (#69), then resyncs on the UI thread and
        // raises ViewChanged. The dispatch is a safety net.
        if (Dispatcher.IsDispatchRequired)
            Dispatcher.Dispatch(InvalidateSurface);
        else
            InvalidateSurface();
    }

    // ── FrozenRowCount ──

    /// <summary>Identifies the <see cref="FrozenRowCount"/> bindable property.</summary>
    public static readonly BindableProperty FrozenRowCountProperty = BindableProperty.Create(
        nameof(FrozenRowCount), typeof(int), typeof(KumikoDataGrid), 0,
        propertyChanged: (b, _, n) =>
        {
            var view = (KumikoDataGrid)b;
            view._dataSource.FrozenRowCount = (int)n;
        });

    /// <summary>Number of top data rows to freeze when scrolling vertically.</summary>
    public int FrozenRowCount
    {
        get => (int)GetValue(FrozenRowCountProperty);
        set => SetValue(FrozenRowCountProperty, value);
    }

    // ── GridSelectionMode ──

    /// <summary>Identifies the <see cref="GridSelectionMode"/> bindable property.</summary>
    public static readonly BindableProperty GridSelectionModeProperty = BindableProperty.Create(
        nameof(GridSelectionMode), typeof(KumikoUI.Core.Models.KumikoSelectionMode), typeof(KumikoDataGrid),
        KumikoUI.Core.Models.KumikoSelectionMode.Extended,
        propertyChanged: (b, _, n) =>
        {
            var view = (KumikoDataGrid)b;
            view._selection.Mode = (KumikoUI.Core.Models.KumikoSelectionMode)n;
            view.InvalidateSurface();
        });

    /// <summary>Selection mode: None, Single, Multiple, or Extended.</summary>
    public KumikoUI.Core.Models.KumikoSelectionMode GridSelectionMode
    {
        get => (KumikoUI.Core.Models.KumikoSelectionMode)GetValue(GridSelectionModeProperty);
        set => SetValue(GridSelectionModeProperty, value);
    }

    // ── ClearSelectionOnEmptyAreaTap ──

    /// <summary>Identifies the <see cref="ClearSelectionOnEmptyAreaTap"/> bindable property.</summary>
    public static readonly BindableProperty ClearSelectionOnEmptyAreaTapProperty = BindableProperty.Create(
        nameof(ClearSelectionOnEmptyAreaTap), typeof(bool), typeof(KumikoDataGrid), true,
        propertyChanged: (b, _, n) => ((KumikoDataGrid)b)._selection.ClearSelectionOnEmptyAreaTap = (bool)n);

    /// <summary>
    /// When true (default), a tap on the empty part of the rows area — below the last row or right of the
    /// last column — clears the selection and the current cell.
    /// </summary>
    public bool ClearSelectionOnEmptyAreaTap
    {
        get => (bool)GetValue(ClearSelectionOnEmptyAreaTapProperty);
        set => SetValue(ClearSelectionOnEmptyAreaTapProperty, value);
    }

    // ── IsReadOnly ──

    /// <summary>Identifies the <see cref="IsReadOnly"/> bindable property.</summary>
    public static readonly BindableProperty IsReadOnlyProperty = BindableProperty.Create(
        nameof(IsReadOnly), typeof(bool), typeof(KumikoDataGrid), false,
        propertyChanged: (b, _, n) =>
        {
            var grid = (KumikoDataGrid)b;
            grid._editSession.IsReadOnly = (bool)n;
            grid.InvalidateSurface();
        });

    /// <summary>
    /// When true, no cell can be edited, Boolean cells do not toggle and Tab skips every cell; combines with
    /// the per-column <c>IsReadOnly</c> (either one makes a cell read-only).
    /// </summary>
    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    // ── ShowFrameStats ──

    /// <summary>Identifies the <see cref="ShowFrameStats"/> bindable property.</summary>
    public static readonly BindableProperty ShowFrameStatsProperty = BindableProperty.Create(
        nameof(ShowFrameStats), typeof(bool), typeof(KumikoDataGrid), false,
        propertyChanged: (b, _, n) =>
        {
            var grid = (KumikoDataGrid)b;
            grid._renderer.ShowFrameStats = (bool)n;
            grid.InvalidateSurface();
        });

    /// <summary>
    /// Draws a frame-time overlay (render time, frames per second, bytes allocated per frame) in the grid's
    /// top-right corner. A debugging aid, off by default; when off it costs nothing per frame.
    /// </summary>
    public bool ShowFrameStats
    {
        get => (bool)GetValue(ShowFrameStatsProperty);
        set => SetValue(ShowFrameStatsProperty, value);
    }

    // ── DismissKeyboardOnEnter ──

    /// <summary>Identifies the <see cref="DismissKeyboardOnEnter"/> bindable property.</summary>
    public static readonly BindableProperty DismissKeyboardOnEnterProperty = BindableProperty.Create(
        nameof(DismissKeyboardOnEnter), typeof(bool), typeof(KumikoDataGrid), true,
        propertyChanged: (b, _, n) => ((KumikoDataGrid)b)._editSession.DismissKeyboardOnEnter = (bool)n);

    /// <summary>
    /// When true (default), pressing Enter while editing commits the edit,
    /// dismisses the keyboard, and ends editing. When false, Enter commits
    /// the current cell and automatically begins editing the cell below.
    /// </summary>
    public bool DismissKeyboardOnEnter
    {
        get => (bool)GetValue(DismissKeyboardOnEnterProperty);
        set => SetValue(DismissKeyboardOnEnterProperty, value);
    }

    // ── EditTriggers ──

    /// <summary>Identifies the <see cref="EditTriggers"/> bindable property.</summary>
    public static readonly BindableProperty EditTriggersProperty = BindableProperty.Create(
        nameof(EditTriggers), typeof(EditTrigger), typeof(KumikoDataGrid), EditTrigger.Default,
        propertyChanged: (b, _, n) => ((KumikoDataGrid)b)._editSession.EditTriggers = (EditTrigger)n);

    /// <summary>
    /// Configures which user actions trigger cell editing.
    /// Default: DoubleTap | F2Key | Typing.
    /// Use flags to combine: e.g. SingleTap | F2Key for quick editing.
    /// </summary>
    public EditTrigger EditTriggers
    {
        get => (EditTrigger)GetValue(EditTriggersProperty);
        set => SetValue(EditTriggersProperty, value);
    }

    // ── EditTextSelectionMode ──

    /// <summary>Identifies the <see cref="EditTextSelectionMode"/> bindable property.</summary>
    public static readonly BindableProperty EditTextSelectionModeProperty = BindableProperty.Create(
        nameof(EditTextSelectionMode), typeof(EditTextSelectionMode), typeof(KumikoDataGrid), EditTextSelectionMode.SelectAll,
        propertyChanged: (b, _, n) => ((KumikoDataGrid)b)._editSession.TextSelectionMode = (EditTextSelectionMode)n);

    /// <summary>
    /// Controls how text is selected when a cell enters edit mode.
    /// SelectAll (default): all text is selected.
    /// CursorAtEnd: cursor is placed at the end with no selection.
    /// </summary>
    public EditTextSelectionMode EditTextSelectionMode
    {
        get => (EditTextSelectionMode)GetValue(EditTextSelectionModeProperty);
        set => SetValue(EditTextSelectionModeProperty, value);
    }

    // ── AllowSorting ──

    /// <summary>Identifies the <see cref="AllowSorting"/> bindable property.</summary>
    public static readonly BindableProperty AllowSortingProperty = BindableProperty.Create(
        nameof(AllowSorting), typeof(bool), typeof(KumikoDataGrid), true,
        propertyChanged: (b, _, n) =>
        {
            var grid = (KumikoDataGrid)b;
            grid._inputController.AllowSorting = (bool)n;
            grid.InvalidateSurface();
        });

    /// <summary>When false, column header taps do not sort (a column's own <c>AllowSorting</c> also applies).</summary>
    public bool AllowSorting
    {
        get => (bool)GetValue(AllowSortingProperty);
        set => SetValue(AllowSortingProperty, value);
    }

    // ── AllowFiltering ──

    /// <summary>Identifies the <see cref="AllowFiltering"/> bindable property.</summary>
    public static readonly BindableProperty AllowFilteringProperty = BindableProperty.Create(
        nameof(AllowFiltering), typeof(bool), typeof(KumikoDataGrid), true,
        propertyChanged: (b, _, n) =>
        {
            var grid = (KumikoDataGrid)b;
            grid._inputController.AllowFiltering = (bool)n;
            grid._renderer.AllowFiltering = (bool)n;
            grid.InvalidateSurface();
        });

    /// <summary>
    /// When false, filter icons are not shown in column headers and cannot be tapped (a column's own
    /// <c>AllowFiltering</c> also applies).
    /// </summary>
    public bool AllowFiltering
    {
        get => (bool)GetValue(AllowFilteringProperty);
        set => SetValue(AllowFilteringProperty, value);
    }

    // ── HeaderHeight ──

    /// <summary>Identifies the <see cref="HeaderHeight"/> bindable property.</summary>
    public static readonly BindableProperty HeaderHeightProperty = BindableProperty.Create(
        nameof(HeaderHeight), typeof(double), typeof(KumikoDataGrid), 40d,
        coerceValue: (b, v) =>
        {
            // Runs on every assignment (even one equal to the default), so the value is always an explicit
            // override that survives a later style or theme change.
            var view = (KumikoDataGrid)b;
            view._styleOptions.HeaderHeight = (float)(double)v;
            view.ApplyStyleOptions();
            view.InvalidateSurface();
            return v;
        });

    /// <summary>Height of the column header row in pixels.</summary>
    public double HeaderHeight
    {
        get => (double)GetValue(HeaderHeightProperty);
        set => SetValue(HeaderHeightProperty, value);
    }

    // ── RowHeight ──

    /// <summary>Identifies the <see cref="RowHeight"/> bindable property.</summary>
    public static readonly BindableProperty RowHeightProperty = BindableProperty.Create(
        nameof(RowHeight), typeof(double), typeof(KumikoDataGrid), 36d,
        coerceValue: (b, v) =>
        {
            // Runs on every assignment (even one equal to the default), so the value is always an explicit
            // override that survives a later style or theme change.
            var view = (KumikoDataGrid)b;
            view._styleOptions.RowHeight = (float)(double)v;
            view.ApplyStyleOptions();
            view.InvalidateSurface();
            return v;
        });

    /// <summary>Height of each data row in pixels.</summary>
    public double RowHeight
    {
        get => (double)GetValue(RowHeightProperty);
        set => SetValue(RowHeightProperty, value);
    }

    // ── GridDescription (Accessibility) ──

    /// <summary>Identifies the <see cref="GridDescription"/> bindable property.</summary>
    public static readonly BindableProperty GridDescriptionProperty = BindableProperty.Create(
        nameof(GridDescription), typeof(string), typeof(KumikoDataGrid), null,
        propertyChanged: (b, _, _) => ((KumikoDataGrid)b).UpdateAccessibilityHint());

    /// <summary>
    /// Accessible description for screen readers. Null (the default) uses the localized
    /// <see cref="KumikoStrings.AccessibleGridName"/> ("Data grid").
    /// </summary>
    public string? GridDescription
    {
        get => (string?)GetValue(GridDescriptionProperty);
        set => SetValue(GridDescriptionProperty, value);
    }

    // ── Columns Bindable Property ──

    /// <summary>Identifies the <see cref="Columns"/> bindable property.</summary>
    public static readonly BindableProperty ColumnsProperty = BindableProperty.Create(
        nameof(Columns),
        typeof(ObservableCollection<KumikoColumn>),
        typeof(KumikoDataGrid),
        defaultValue: null,
        coerceValue: (_, v) => v ?? new ObservableCollection<KumikoColumn>(),
        propertyChanged: (b, o, n) => ((KumikoDataGrid)b).OnColumnsChanged(
            o as ObservableCollection<KumikoColumn>,
            n as ObservableCollection<KumikoColumn>));

    /// <summary>
    /// Bindable collection of column definitions. Supports ObservableCollection
    /// for dynamic add/remove of columns at runtime. Never null: assigning null sets an empty collection.
    /// </summary>
    public ObservableCollection<KumikoColumn> Columns
    {
        get => (ObservableCollection<KumikoColumn>)GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    private void OnColumnsChanged(
        ObservableCollection<KumikoColumn>? oldCollection,
        ObservableCollection<KumikoColumn>? newCollection)
    {
        if (oldCollection != null)
            oldCollection.CollectionChanged -= OnColumnsCollectionChanged;

        if (newCollection != null)
        {
            if (IsObservingCollections)
                newCollection.CollectionChanged += OnColumnsCollectionChanged;
            _dataSource.SetColumns(newCollection);
        }
        else
        {
            _dataSource.SetColumns(Array.Empty<KumikoColumn>());
        }

        InvalidateSurface();
    }

    private void OnColumnsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_syncingFromSource) return; // the grid is copying the source's column order in (KumikoDataGrid.Layout.cs)
        _dataSource.SetColumns(Columns);
        InvalidateSurface();
    }

    // ── TableSummaryRows Bindable Property ──

    /// <summary>Identifies the <see cref="TableSummaryRows"/> bindable property.</summary>
    public static readonly BindableProperty TableSummaryRowsProperty = BindableProperty.Create(
        nameof(TableSummaryRows),
        typeof(ObservableCollection<TableSummaryRow>),
        typeof(KumikoDataGrid),
        defaultValue: null,
        coerceValue: (_, v) => v ?? new ObservableCollection<TableSummaryRow>(),
        propertyChanged: (b, o, n) => ((KumikoDataGrid)b).OnTableSummaryRowsChanged(
            o as ObservableCollection<TableSummaryRow>,
            n as ObservableCollection<TableSummaryRow>));

    /// <summary>
    /// Bindable collection of table summary row definitions.
    /// Define summary rows in XAML with column aggregates. Never null: assigning null sets an empty collection.
    /// </summary>
    public ObservableCollection<TableSummaryRow> TableSummaryRows
    {
        get => (ObservableCollection<TableSummaryRow>)GetValue(TableSummaryRowsProperty);
        set => SetValue(TableSummaryRowsProperty, value);
    }

    private void OnTableSummaryRowsChanged(
        ObservableCollection<TableSummaryRow>? oldCollection,
        ObservableCollection<TableSummaryRow>? newCollection)
    {
        if (oldCollection != null)
            oldCollection.CollectionChanged -= OnTableSummaryRowsCollectionChanged;

        if (newCollection != null)
        {
            if (IsObservingCollections)
                newCollection.CollectionChanged += OnTableSummaryRowsCollectionChanged;
            SyncTableSummaryRows(newCollection);
        }
        else
        {
            _dataSource.ClearTableSummaryRows();
        }
    }

    private void OnTableSummaryRowsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SyncTableSummaryRows(TableSummaryRows);
    }

    private void SyncTableSummaryRows(IEnumerable<TableSummaryRow> rows)
    {
        _dataSource.ClearTableSummaryRows();
        foreach (var row in rows)
            _dataSource.AddTableSummaryRow(row);
    }

    // ── Touch / Input ────────────────────────────────────────────

    private void OnCanvasTouch(object? sender, SKTouchEventArgs e)
    {
        // A move with no contact is a mouse hover (Windows); entering in contact (a drag coming back) is
        // nothing to act on.
        if (e.ActionType == SKTouchAction.Entered && e.InContact)
            return;
#if WINDOWS
        // The wheel is read from WinUI directly (KumikoDataGrid.Pointer.cs), which knows its axis.
        if (e.ActionType == SKTouchAction.WheelChanged && _nativeWheelView != null)
            return;
#endif
        // A disabled grid takes no input (and leaves the event to the page).
        if (!_inputController.IsEnabled)
            return;

        // Before the first layout and paint the touch has no grid position (MAUI-26).
        if (!TryGetTouchScale(out float scale))
            return;

        var action = MapAction(e.ActionType, e.InContact);
        var deviceType = MapDeviceType(e.DeviceType);
        var button = action is InputAction.Hover or InputAction.Exited ? PointerButton.None : GetPointerButton(e);
        float x = e.Location.X / scale;
        float y = e.Location.Y / scale;

        // A second finger while one is down: Core ignores it by id (and marks it handled). It must not restart the
        // click count or the long-press timer, or take the keyboard focus.
        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        bool fromPressedPointer = _pressedPointerId == null || _pressedPointerId == e.Id ||
            (e.ActionType == SKTouchAction.Pressed && nowMs - _pressedPointerLastMs > StalePointerMs);
        if (!fromPressedPointer && action is InputAction.Pressed or InputAction.Moved or InputAction.Released or InputAction.Cancelled)
        {
            var ignored = new KumikoPointerEventArgs
            {
                X = x, Y = y, Action = action, PointerId = e.Id, DeviceType = deviceType, TimestampMs = nowMs,
            };
            _inputController.HandlePointer(ignored, _scroll, _selection, _style, _dataSource);
            e.Handled = ignored.Handled;
            return;
        }
        if (e.ActionType == SKTouchAction.Pressed)
        {
            _pressedPointerId = e.Id;
            _pressedDeviceType = deviceType;
        }
        else if (e.ActionType is SKTouchAction.Released or SKTouchAction.Cancelled)
            _pressedPointerId = null;
        if (action != InputAction.Hover) _pressedPointerLastMs = nowMs;

        // On desktop platforms, focus keyboard input on press so we can capture
        // key events for grid navigation (arrow keys, Tab, etc.).
        // On mobile (iOS/Android), we do NOT focus here because it would
        // show the software keyboard during scrolling and other non-edit interactions.
        // Mobile keyboard focus is handled in OnEditSessionCellBeginEdit instead.
        if (e.ActionType == SKTouchAction.Pressed)
        {
#if WINDOWS || MACCATALYST
            FocusKeyboardInput();
#elif ANDROID
            FocusAndroidTextInputForKeys(x, y); // focus for a hardware keyboard; no soft keyboard
#endif
        }
#if ANDROID
        else if (e.ActionType == SKTouchAction.Released)
            ReshowAndroidSoftInput(); // a tap during an edit brings back a dismissed soft keyboard
#endif

        // Detect double-tap — store on Pressed so Released also gets ClickCount=2
        int clickCount = 1;
        if (e.ActionType == SKTouchAction.Pressed)
        {
            float dx = x - _lastTapX;
            float dy = y - _lastTapY;
            float dist = MathF.Sqrt(dx * dx + dy * dy);

            if (nowMs - _lastTapTimeMs < DoubleTapThresholdMs &&
                dist < DoubleTapDistanceThreshold)
            {
                clickCount = 2;
                _lastTapTimeMs = 0; // Reset so triple-tap doesn't count
            }
            else
            {
                _lastTapTimeMs = nowMs;
            }
            _lastTapX = x;
            _lastTapY = y;
            _pendingClickCount = clickCount;

            // Start long press detection (a right or middle button held down is not a long press: its release
            // must still reach Core, which raises the context menu on it)
            _longPressFired = false;
            _longPressX = x;
            _longPressY = y;
            if (button == PointerButton.Primary)
                StartLongPressTimer(x, y);
            else
                CancelLongPressTimer();
        }
        else if (e.ActionType == SKTouchAction.Released)
        {
            // Carry the click count from the preceding Pressed event
            clickCount = _pendingClickCount;
            _pendingClickCount = 1;
            CancelLongPressTimer();
        }
        else if (action == InputAction.Moved)
        {
            // Cancel long press if finger moved too far
            float lpDx = x - _longPressX;
            float lpDy = y - _longPressY;
            if (MathF.Sqrt(lpDx * lpDx + lpDy * lpDy) > LongPressMoveTolerance)
                CancelLongPressTimer();
        }
        else if (e.ActionType == SKTouchAction.Cancelled)
        {
            CancelLongPressTimer();
        }

        // If long press already fired, the release must not also tap: Core is told the press was canceled, which
        // ends it without acting on it. A drag in progress (a long press starts a row drag on touch) is dropped by
        // the release as usual.
        bool releaseAfterLongPress = _longPressFired && e.ActionType == SKTouchAction.Released;
        if (releaseAfterLongPress) _longPressFired = false;
        bool dragging = _inputController.DragRowIndex >= 0 || _inputController.DragColumnIndex >= 0;
        if (releaseAfterLongPress && !dragging)
        {
            var cancel = new KumikoPointerEventArgs
            {
                X = x, Y = y, Action = InputAction.Cancelled, PointerId = e.Id, DeviceType = deviceType,
                TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };
            _inputController.HandlePointer(cancel, _scroll, _selection, _style, _dataSource);
            e.Handled = true;
            return;
        }

        var gridEvent = new KumikoPointerEventArgs
        {
            X = x,
            Y = y,
            Action = action,
            Button = button,
            Modifiers = GetPointerModifiers(),
            PointerId = e.Id,
            DeviceType = deviceType,
            // Only Windows reports the wheel here (when the native hook is missing): 120ths of a notch
            ScrollDeltaY = e.ActionType == SKTouchAction.WheelChanged ? e.WheelDelta / 120f : 0,
            TimestampMs = nowMs,
            ClickCount = clickCount
        };

        _inputController.HandlePointer(gridEvent, _scroll, _selection, _style, _dataSource);
        if (action == InputAction.Moved)
            ReassertPlatformCursor(); // keeps the resize cursor through a drag on Catalyst

        // Start the frame loop for a fling (the grid coasting, or a scroll picker editor coasting and snapping
        // to an item) on release, or for edge autoscroll (a row drag held at an edge)
        if (((e.ActionType == SKTouchAction.Released || e.ActionType == SKTouchAction.Cancelled)
             && (_inputController.IsInertialScrolling || _editSession.IsAnimating)) || _inputController.IsAutoScrolling)
            StartFrameAnimation();
        else if (_editSession.IsAnimating)
            StartFrameAnimation(); // a spin button pressed and held repeats from the frame loop

        e.Handled = gridEvent.Handled;
    }

    /// <summary>The Core device type of a SkiaSharp touch event: a finger, a pen, or else a mouse.</summary>
    private static KumikoPointerDeviceType MapDeviceType(SKTouchDeviceType deviceType) => deviceType switch
    {
        SKTouchDeviceType.Touch => KumikoPointerDeviceType.Touch,
        SKTouchDeviceType.Pen => KumikoPointerDeviceType.Pen,
        _ => KumikoPointerDeviceType.Mouse,
    };

    private static InputAction MapAction(SKTouchAction action, bool inContact) => action switch
    {
        SKTouchAction.Pressed => InputAction.Pressed,
        SKTouchAction.Released => InputAction.Released,
        // Hover (no button down) only updates the cursor and hover feedback; it never pans
        SKTouchAction.Moved => inContact ? InputAction.Moved : InputAction.Hover,
        SKTouchAction.Entered => InputAction.Hover,
        // Leaving resets the cursor; it does not cancel a drag (the pointer may come back)
        SKTouchAction.Exited => InputAction.Exited,
        SKTouchAction.Cancelled => InputAction.Cancelled,
        SKTouchAction.WheelChanged => InputAction.Scroll,
        _ => InputAction.Cancelled
    };

    // ── Long press detection ─────────────────────────────────────

    private void StartLongPressTimer(float x, float y)
    {
        CancelLongPressTimer();
        _longPressTimer = Dispatcher.CreateTimer();
        _longPressTimer.Interval = TimeSpan.FromMilliseconds(LongPressThresholdMs);
        _longPressTimer.IsRepeating = false;
        _longPressTimer.Tick += OnLongPressTimerTick;
        _longPressTimer.Start();
    }

    private void CancelLongPressTimer()
    {
        if (_longPressTimer != null)
        {
            _longPressTimer.Stop();
            _longPressTimer.Tick -= OnLongPressTimerTick;
            _longPressTimer = null;
        }
    }

    private void OnLongPressTimerTick(object? sender, EventArgs e)
    {
        CancelLongPressTimer();

        // Synthesize a LongPress event for the input controller, from the press in progress
        if (_pressedPointerId is not { } pointerId) return;
        _longPressFired = true;

        var gridEvent = new KumikoPointerEventArgs
        {
            X = _longPressX,
            Y = _longPressY,
            Action = InputAction.LongPress,
            Button = PointerButton.Primary,
            PointerId = pointerId,
            DeviceType = _pressedDeviceType,
            TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ClickCount = 1
        };

        _inputController.HandlePointer(gridEvent, _scroll, _selection, _style, _dataSource);
    }

    // ── Column reorder event ──

    private void OnColumnReordered(object? sender, KumikoColumnReorderedEventArgs e)
    {
        _dataSource.ReorderColumn(e.OldIndex, e.NewIndex); // Columns follows (KumikoDataGrid.Layout.cs)
        ColumnReordered?.Invoke(this, e);
    }

    // ── Auto-fit column event ──

    private void OnAutoFitColumnRequested(object? sender, AutoFitColumnEventArgs e)
    {
        // Use a temporary SKBitmap/Canvas for text measurement
        using var bitmap = new SKBitmap(1, 1);
        using var canvas = new SKCanvas(bitmap);
        using var measureCtx = new SkiaDrawingContext(canvas, ResourceCache);

        // Samples the rows on screen plus the first and last rows of the view, never every row (#24); a Star or
        // Auto column becomes Fixed at the fitted width so it keeps it.
        _renderer.AutoFitColumn(e.Column, _dataSource, measureCtx, _style);
        InvalidateSurface();
    }

    // ── Named event handlers (avoid anonymous lambdas for clean unsubscribe) ──

    private void OnDataSourceViewChanged(object? sender, KumikoViewChangedEventArgs e)
    {
        RequestRedraw();
        if (e.IsStructural)
            UpdateAccessibilityHint();
    }

    private void OnInputControllerNeedsRedraw()
    {
        RequestRedraw();
        SyncNativeTextInput(); // an editor or the filter search box may have opened or closed
    }

    private void OnEditSessionNeedsRedraw()
    {
        RequestRedraw();
        SyncNativeTextInput();
#if WINDOWS
        // An editor opened or closed: attach the Windows text services to it (KumikoDataGrid.TextInput.Windows.cs)
        SyncTextServices();
#endif
    }

    // An action button's command raised CanExecuteChanged: that can happen on any thread.
    private void OnRendererRedrawRequested()
    {
        if (Dispatcher.IsDispatchRequired)
            Dispatcher.Dispatch(RequestRedraw);
        else
            RequestRedraw();
    }

    private void OnEditSessionCellBeginEdit(object? sender, CellBeginEditEventArgs e)
    {
        if (!e.Cancel)
        {
            StartCursorBlinkTimer();
            // Focus keyboard input when editing starts — this is the only time
            // the software keyboard should appear on mobile platforms.
            FocusKeyboardInput();
        }
    }

    private void OnKeyboardFocusRequested() => FocusKeyboardInput();

    private void OnFilterPopupOpened()
    {
        _filterPopupActive = true;
        StartCursorBlinkTimer();
    }

    private void OnFilterPopupClosed()
    {
        _filterPopupActive = false;
        StopCursorBlinkTimer();
#if ANDROID
        ScheduleAndroidImeUpdate(); // the search box is gone: hide the soft keyboard unless an edit is open
#endif
    }

    private void OnEditSessionCellEndEdit(object? sender, CellEndEditEventArgs e)
    {
        StopCursorBlinkTimer();
        _selection.IsEditing = false;

        // On mobile platforms, dismiss the software keyboard when editing ends
        UnfocusKeyboardInput();
    }

    // ── Lifecycle cleanup ────────────────────────────────────────

    /// <summary>
    /// Unsubscribe all event handlers, stop timers, and remove native subviews.
    /// Called when the view is disconnected from its handler (page navigation, etc.)
    /// to prevent memory leaks from delegate roots.
    /// </summary>
    private void Cleanup()
    {
        _wasCleanedUp = true;
        DisconnectTheming();

        // Stop the frame loop and timers first (they capture 'this' via their callbacks), and release the surface
        DetachRendering();
        CancelLongPressTimer();

        // Stop observing the app's collection and its items (the source lives on; Reconnect re-attaches it)
        _dataSource.Detach();

        // Unsubscribe from internal owned objects
        _dataSource.ViewChanged -= OnDataSourceViewChanged;
        _inputController.NeedsRedraw -= OnInputControllerNeedsRedraw;
        _renderer.RedrawRequested -= OnRendererRedrawRequested;
        _renderer.Dispose();
        _inputController.ColumnReordered -= OnColumnReordered;
        _inputController.AutoFitColumnRequested -= OnAutoFitColumnRequested;
        _inputController.KeyboardFocusRequested -= OnKeyboardFocusRequested;
        _inputController.FilterPopupOpened -= OnFilterPopupOpened;
        _inputController.FilterPopupClosed -= OnFilterPopupClosed;
        _inputController.CursorChanged -= OnInputControllerCursorChanged;
        _editSession.NeedsRedraw -= OnEditSessionNeedsRedraw;
        _editSession.CellBeginEdit -= OnEditSessionCellBeginEdit;
        _editSession.CellEndEdit -= OnEditSessionCellEndEdit;
        UnsubscribeAccessibility();

#if WINDOWS
        DetachNativeWheel();
        DetachNativeKeyboard();
#endif
        CancelLongPressTimer();
        _pressedPointerId = null;

        // Release the cached native Skia objects; the next frame after Reconnect creates a new cache.
        _resourceCache?.Dispose();
        _resourceCache = null;

        // Cancel image loads and dispose the cached images; the next frame after Reconnect loads them again.
        DisconnectImages();

        // Unsubscribe the bound items and the Columns/summary/grouping collections (Reconnect restores them)
        UnsubscribeObservedCollections();
        _selectionBinder.Detach(); // the app's SelectedItems list, when it supplied one

#if MACCATALYST || IOS
        StopObservingKeyboardFrame();

        // Remove native keyboard input
        if (_nativeKeyInput != null)
        {
            _nativeKeyInput.Detach();
            _nativeKeyInput.RemoveFromSuperview();
            _nativeKeyInput = null;
            _useNativeKeyInput = false;
        }

        // Remove native scroll gesture recognizer
        if (_nativeScrollGesture != null)
        {
            if (Handler?.PlatformView is UIView platformView)
                platformView.RemoveGestureRecognizer(_nativeScrollGesture);
            _nativeScrollGesture = null;
        }
        if (_nativeSecondaryClickGesture != null)
        {
            if (Handler?.PlatformView is UIView platformView)
                platformView.RemoveGestureRecognizer(_nativeSecondaryClickGesture);
            _nativeSecondaryClickGesture = null;
        }
#if MACCATALYST
        TeardownNativeHover(Handler?.PlatformView as UIView);
#endif

        // Unsubscribe handler changed (prevent duplicate native setup on re-attach)
        this.HandlerChanged -= OnHandlerChanged_SetupNativeKeyboard;
        this.HandlerChanged -= OnHandlerChanged_SetupNativeScroll;
#endif

#if ANDROID
        this.HandlerChanged -= OnHandlerChanged_SetupAndroidTextInput;
        DetachAndroidTextInput();
#endif
    }

    /// <summary>
    /// Re-subscribe all event handlers after the view is re-connected to its handler.
    /// Mirrors the constructor subscriptions so the view works after navigation back.
    /// </summary>
    private void Reconnect()
    {
        ConnectTheming();
        _dataSource.ViewChanged += OnDataSourceViewChanged;
        // After ViewChanged is re-subscribed, so the rebuild Attach requests reaches this view
        _dataSource.Attach();
        ResubscribeObservedCollections();
        _selectionBinder.Attach();
        _inputController.NeedsRedraw += OnInputControllerNeedsRedraw;
        _renderer.RedrawRequested += OnRendererRedrawRequested;
        _inputController.ColumnReordered += OnColumnReordered;
        _inputController.AutoFitColumnRequested += OnAutoFitColumnRequested;
        _inputController.KeyboardFocusRequested += OnKeyboardFocusRequested;
        _inputController.FilterPopupOpened += OnFilterPopupOpened;
        _inputController.FilterPopupClosed += OnFilterPopupClosed;
        _inputController.CursorChanged += OnInputControllerCursorChanged;
        _editSession.NeedsRedraw += OnEditSessionNeedsRedraw;
        _editSession.CellBeginEdit += OnEditSessionCellBeginEdit;
        _editSession.CellEndEdit += OnEditSessionCellEndEdit;
        SubscribeAccessibility();
        ConnectImages();

        ConnectSurface();

#if MACCATALYST || IOS
        this.HandlerChanged += OnHandlerChanged_SetupNativeKeyboard;
        this.HandlerChanged += OnHandlerChanged_SetupNativeScroll;
#endif

#if ANDROID
        this.HandlerChanged += OnHandlerChanged_SetupAndroidTextInput;
        AttachAndroidTextInput(); // the handler is already connected: HandlerChanged has fired
#endif
    }

    // ── Events ───────────────────────────────────────────────────

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
    /// Fires when the user asks for a context menu: a right click (on its release; on Mac Catalyst and iPad a
    /// secondary click) or a touch or pen long press that neither starts a row drag nor edits the cell. A right
    /// click never sorts, scrolls, drags or edits; on a data row the row is selected first unless it already is (see
    /// <see cref="KumikoContextRequestedEventArgs"/>). <see cref="KumikoContextRequestedEventArgs.X"/> and
    /// <see cref="KumikoContextRequestedEventArgs.Y"/> are in device-independent units relative to this control. It
    /// re-surfaces <see cref="KumikoInputController.ContextRequested"/>, under the same name as on the Uno control.
    /// </summary>
    public event EventHandler<KumikoContextRequestedEventArgs>? ContextMenuRequested
    {
        add => _inputController.ContextRequested += value;
        remove => _inputController.ContextRequested -= value;
    }

    /// <summary>Fires before a cell enters edit mode. Set Cancel=true to prevent.</summary>
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

#if !WINDOWS // Windows: KumikoDataGrid.TextInput.Windows.cs
    /// <summary>Focus the appropriate keyboard input mechanism for the current platform.</summary>
    private void FocusKeyboardInput()
    {
#if ANDROID
        ScheduleAndroidImeUpdate(); // the input view takes focus and shows the soft keyboard once the editor exists
#elif MACCATALYST || IOS
        if (_useNativeKeyInput && _nativeKeyInput != null)
        {
            _nativeKeyInput.BecomeFirstResponder();
            SyncNativeTextInput();
        }
#endif
    }

    /// <summary>Dismiss the software keyboard by removing focus from the keyboard input mechanism.</summary>
    private void UnfocusKeyboardInput()
    {
#if ANDROID
        ScheduleAndroidImeUpdate(); // hides the soft keyboard unless another edit began meanwhile
#elif MACCATALYST || IOS
        if (_useNativeKeyInput && _nativeKeyInput != null)
        {
            SyncNativeTextInput();
            _nativeKeyInput.ResignFirstResponder();
        }
#endif
    }
#endif

    private void SendKeyEvent(KumikoKey key, char? character = null, InputModifiers modifiers = InputModifiers.None) =>
        SendKeyEvent(new KumikoKeyEventArgs
        {
            Key = key,
            Character = character,
            Modifiers = modifiers
        });

    private void SendKeyEvent(KumikoKeyEventArgs keyEvent)
    {
        _inputController.HandleKey(keyEvent, _scroll, _selection, _style, _dataSource);
        if (_editSession.IsAnimating)
            StartFrameAnimation(); // a held arrow key repeats a spin button from the frame loop
        RestartCursorBlinkTimer(); // a key restarts the caret's blink
        SyncNativeTextInput(); // the key may have opened or closed an editor
    }

#if MACCATALYST || IOS
    // ── Native Keyboard Input (Apple platforms) ──────────────────

    private void OnHandlerChanged_SetupNativeKeyboard(object? sender, EventArgs e)
    {
        // Guard against duplicate setup (HandlerChanged can fire multiple times)
        if (_nativeKeyInput != null) return;

        if (Handler?.PlatformView is UIView platformView)
        {
            _nativeKeyInput = CreateNativeTextInput(); // KumikoDataGrid.TextInput.cs
            platformView.AddSubview(_nativeKeyInput);
            _useNativeKeyInput = true;
            ObserveKeyboardFrame(); // keeps the edited cell above the on-screen keyboard (#258)
        }
    }

    private void OnHandlerChanged_SetupNativeScroll(object? sender, EventArgs e)
    {
#if MACCATALYST
        // Mouse hover (cursor feedback): SKCanvasView only reports touches on Catalyst
        if (Handler?.PlatformView is UIView hoverView)
            SetupNativeHover(hoverView);
#endif

        // Guard against duplicate setup
        if (_nativeScrollGesture != null) return;

        if (Handler?.PlatformView is UIView platformView)
        {
            // Add a pan gesture recognizer that captures indirect input
            // (trackpad scroll, mouse wheel) on Mac Catalyst
            _nativeScrollGesture = new UIPanGestureRecognizer(HandleNativeScrollGesture);
            _nativeScrollGesture.AllowedScrollTypesMask = UIScrollTypeMask.Continuous | UIScrollTypeMask.Discrete;
            _nativeScrollGesture.MaximumNumberOfTouches = 0; // Only indirect (trackpad/mouse wheel)
            platformView.AddGestureRecognizer(_nativeScrollGesture);

            // A secondary (right) click reaches no touch handler, SkiaSharp's included: it needs its own recognizer
            _nativeSecondaryClickGesture = new UITapGestureRecognizer(HandleNativeSecondaryClick)
            {
                ButtonMaskRequired = UIEventButtonMask.Secondary,
            };
            platformView.AddGestureRecognizer(_nativeSecondaryClickGesture);
        }
    }
#endif
}

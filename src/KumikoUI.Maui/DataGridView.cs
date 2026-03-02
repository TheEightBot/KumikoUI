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
#if ANDROID
using AndroidInputTypes = Android.Text.InputTypes;
using AndroidImeAction = Android.Views.InputMethods.ImeAction;
#endif

namespace KumikoUI.Maui;

/// <summary>
/// .NET MAUI KumikoUI control backed by SkiaSharp rendering.
/// Drop this into any MAUI page. All rendering is done on the SKCanvas.
/// Uses a hidden Entry overlay for keyboard input capture.
/// </summary>
[ContentProperty(nameof(Columns))]
public class DataGridView : Grid
{
    private readonly SKCanvasView _canvasView = new();
    private readonly Entry _keyboardProxy;
    private readonly DataGridRenderer _renderer = new();
    private readonly DataGridSource _dataSource = new();
    private readonly ScrollState _scroll = new();
    private readonly SelectionModel _selection = new();
    private readonly GridInputController _inputController = new();
    private readonly EditSession _editSession = new();
    private DataGridStyle _style = new();

    // Inertial scroll timer
    private IDispatcherTimer? _scrollTimer;

    // Cursor blink timer — drives regular repaints while editing
    private IDispatcherTimer? _cursorBlinkTimer;
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

    // Track if we are suppressing TextChanged because we set the text programmatically
    private bool _suppressTextChanged;

    // Track whether Cleanup has been called so Reconnect knows to re-wire
    private bool _wasCleanedUp;

    // Sentinel character used to detect backspace on Entry.
    // Zero-width space avoids collision with user-typed spaces and reduces
    // interference from Android IME composing/replacement behavior.
    private const string KeyboardSentinel = "\u200B";

#if MACCATALYST || IOS
    private KeyInputResponder? _nativeKeyInput;
    private UIPanGestureRecognizer? _nativeScrollGesture;
    private bool _useNativeKeyInput;
#endif

    public DataGridView()
    {
        // Accessibility: set semantic description for screen readers
        SemanticProperties.SetDescription(this, "Data grid");
        AutomationProperties.SetIsInAccessibleTree(this, true);

        // Set up the hidden keyboard proxy entry (fallback for Android/Windows)
        _keyboardProxy = new Entry
        {
            Opacity = 0,
            HeightRequest = 0,
            WidthRequest = 0,
            InputTransparent = false,
            IsVisible = true,
            Text = KeyboardSentinel,
            ReturnType = ReturnType.Done,   // Ensures Completed fires on Android
            IsTextPredictionEnabled = false, // Reduce IME composing interference
            IsSpellCheckEnabled = false,
        };

        // Layout: canvas fills the grid, keyboard proxy sits hidden at top-left
        _canvasView.HorizontalOptions = LayoutOptions.Fill;
        _canvasView.VerticalOptions = LayoutOptions.Fill;
        _canvasView.EnableTouchEvents = true;
        _canvasView.IgnorePixelScaling = false;

        Children.Add(_canvasView);
        Children.Add(_keyboardProxy);

        // Auto-init the Columns collection so XAML can add items directly
        Columns = new ObservableCollection<DataGridColumn>();

        // Auto-init the TableSummaryRows collection so XAML can add items directly
        TableSummaryRows = new ObservableCollection<TableSummaryRow>();

        _dataSource.DataChanged += OnDataSourceDataChanged;

        _inputController.NeedsRedraw += OnInputControllerNeedsRedraw;
        _inputController.ColumnReordered += OnColumnReordered;
        _inputController.RowReordered += OnRowReordered;
        _inputController.AutoFitColumnRequested += OnAutoFitColumnRequested;
        _inputController.KeyboardFocusRequested += OnKeyboardFocusRequested;
        _inputController.FilterPopupOpened += OnFilterPopupOpened;
        _inputController.FilterPopupClosed += OnFilterPopupClosed;

        // Wire edit session to input controller
        _inputController.EditSession = _editSession;
        _editSession.Style = _style;
        _editSession.NeedsRedraw += OnEditSessionNeedsRedraw;

        // Wire edit session lifecycle for cursor blink timer
        _editSession.CellBeginEdit += OnEditSessionCellBeginEdit;
        _editSession.CellEndEdit += OnEditSessionCellEndEdit;

        // Wire canvas events
        _canvasView.PaintSurface += OnPaintSurface;
        _canvasView.Touch += OnCanvasTouch;

        // Wire keyboard proxy events (used on Android/Windows, or as fallback)
        _keyboardProxy.TextChanged += OnKeyboardProxyTextChanged;
        _keyboardProxy.Completed += OnKeyboardProxyCompleted;

#if MACCATALYST || IOS
        // Set up native keyboard input and scroll gesture after the view is loaded
        this.HandlerChanged += OnHandlerChanged_SetupNativeKeyboard;
        this.HandlerChanged += OnHandlerChanged_SetupNativeScroll;
#endif

#if ANDROID
        // Configure Android-specific keyboard proxy once its handler is connected
        _keyboardProxy.HandlerChanged += OnKeyboardProxyHandlerChanged_Android;
#endif
    }

    /// <summary>Canvas size for coordinate mapping.</summary>
    private SKSize CanvasSize => _canvasView.CanvasSize;

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

        // Connected to a new handler — re-subscribe if previously cleaned up.
        // On first attach, the constructor already did the wiring and _isConnected
        // is still false, so we skip. On subsequent attach (e.g. navigating back),
        // we need to re-wire.
        if (Handler != null && _wasCleanedUp)
        {
            _wasCleanedUp = false;
            Reconnect();
        }
    }

    // ── Public API ────────────────────────────────────────────────

    /// <summary>Configurable scroll/inertia settings.</summary>
    public ScrollSettings ScrollSettings
    {
        get => _inputController.ScrollSettings;
        set => _inputController.ScrollSettings = value;
    }

    /// <summary>Gets the underlying data source.</summary>
    public DataGridSource DataSource => _dataSource;

    /// <summary>Gets the selection model.</summary>
    public SelectionModel Selection => _selection;

    /// <summary>Gets the input controller for advanced event wiring.</summary>
    public GridInputController InputController => _inputController;

    /// <summary>Gets the edit session for inline cell editing.</summary>
    public EditSession EditSession => _editSession;

    /// <summary>Gets or sets the grid style / theme.</summary>
    public DataGridStyle GridStyle
    {
        get => _style;
        set { _style = value; _editSession.Style = value; InvalidateSurface(); }
    }

    /// <summary>
    /// Applies a built-in theme preset. Sets <see cref="GridStyle"/>
    /// to a preconfigured <see cref="DataGridStyle"/> for the chosen theme.
    /// </summary>
    public DataGridThemeMode Theme
    {
        get => _themeMode;
        set
        {
            if (_themeMode == value) return;
            _themeMode = value;
            _style = DataGridTheme.Create(value);
            _editSession.Style = _style;
            InvalidateSurface();
        }
    }
    private DataGridThemeMode _themeMode = DataGridThemeMode.Light;

    /// <summary>Set the data items.</summary>
    public void SetItemsSource(System.Collections.IEnumerable items)
    {
        _dataSource.SetItems(items);
    }

    /// <summary>Set the column definitions.</summary>
    public void SetColumns(IEnumerable<DataGridColumn> columns)
    {
        _dataSource.SetColumns(columns);
        InvalidateSurface();
    }

    // ── Bindable Properties ──────────────────────────────────────

    // ── ItemsSource ──

    /// <summary>Identifies the <see cref="ItemsSource"/> bindable property.</summary>
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
        nameof(ItemsSource), typeof(IEnumerable), typeof(DataGridView),
        propertyChanged: (b, o, n) => ((DataGridView)b).OnItemsSourceChanged((IEnumerable?)o, (IEnumerable?)n));

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

            // Subscribe at MAUI layer for thread-safe invalidation
            if (newValue is INotifyCollectionChanged incc)
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
        // DataGridSource already handles the rebuild; we just ensure canvas invalidation
        // happens on the UI thread
        if (Dispatcher.IsDispatchRequired)
            Dispatcher.Dispatch(InvalidateSurface);
        else
            InvalidateSurface();
    }

    // ── FrozenRowCount ──

    /// <summary>Identifies the <see cref="FrozenRowCount"/> bindable property.</summary>
    public static readonly BindableProperty FrozenRowCountProperty = BindableProperty.Create(
        nameof(FrozenRowCount), typeof(int), typeof(DataGridView), 0,
        propertyChanged: (b, _, n) =>
        {
            var view = (DataGridView)b;
            view._dataSource.FrozenRowCount = (int)n;
        });

    /// <summary>Number of top data rows to freeze when scrolling vertically.</summary>
    public int FrozenRowCount
    {
        get => (int)GetValue(FrozenRowCountProperty);
        set => SetValue(FrozenRowCountProperty, value);
    }

    // ── SelectionMode ──

    /// <summary>Identifies the <see cref="GridSelectionMode"/> bindable property.</summary>
    public static readonly BindableProperty GridSelectionModeProperty = BindableProperty.Create(
        nameof(GridSelectionMode), typeof(KumikoUI.Core.Models.SelectionMode), typeof(DataGridView),
        KumikoUI.Core.Models.SelectionMode.Extended,
        propertyChanged: (b, _, n) =>
        {
            var view = (DataGridView)b;
            view._selection.Mode = (KumikoUI.Core.Models.SelectionMode)n;
            view.InvalidateSurface();
        });

    /// <summary>Selection mode: None, Single, Multiple, or Extended.</summary>
    public KumikoUI.Core.Models.SelectionMode GridSelectionMode
    {
        get => (KumikoUI.Core.Models.SelectionMode)GetValue(GridSelectionModeProperty);
        set => SetValue(GridSelectionModeProperty, value);
    }

    // ── IsReadOnly ──

    /// <summary>Identifies the <see cref="IsReadOnly"/> bindable property.</summary>
    public static readonly BindableProperty IsReadOnlyProperty = BindableProperty.Create(
        nameof(IsReadOnly), typeof(bool), typeof(DataGridView), false);

    /// <summary>When true, cells cannot be edited.</summary>
    public new bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    // ── DismissKeyboardOnEnter ──

    /// <summary>Identifies the <see cref="DismissKeyboardOnEnter"/> bindable property.</summary>
    public static readonly BindableProperty DismissKeyboardOnEnterProperty = BindableProperty.Create(
        nameof(DismissKeyboardOnEnter), typeof(bool), typeof(DataGridView), true,
        propertyChanged: (b, _, n) => ((DataGridView)b)._editSession.DismissKeyboardOnEnter = (bool)n);

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
        nameof(EditTriggers), typeof(EditTrigger), typeof(DataGridView), EditTrigger.Default,
        propertyChanged: (b, _, n) => ((DataGridView)b)._editSession.EditTriggers = (EditTrigger)n);

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
        nameof(EditTextSelectionMode), typeof(EditTextSelectionMode), typeof(DataGridView), EditTextSelectionMode.SelectAll,
        propertyChanged: (b, _, n) => ((DataGridView)b)._editSession.TextSelectionMode = (EditTextSelectionMode)n);

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
        nameof(AllowSorting), typeof(bool), typeof(DataGridView), true);

    /// <summary>When false, column header taps do not sort.</summary>
    public bool AllowSorting
    {
        get => (bool)GetValue(AllowSortingProperty);
        set => SetValue(AllowSortingProperty, value);
    }

    // ── AllowFiltering ──

    /// <summary>Identifies the <see cref="AllowFiltering"/> bindable property.</summary>
    public static readonly BindableProperty AllowFilteringProperty = BindableProperty.Create(
        nameof(AllowFiltering), typeof(bool), typeof(DataGridView), true);

    /// <summary>When false, filter icons are not shown in column headers.</summary>
    public bool AllowFiltering
    {
        get => (bool)GetValue(AllowFilteringProperty);
        set => SetValue(AllowFilteringProperty, value);
    }

    // ── HeaderHeight ──

    /// <summary>Identifies the <see cref="HeaderHeight"/> bindable property.</summary>
    public static readonly BindableProperty HeaderHeightProperty = BindableProperty.Create(
        nameof(HeaderHeight), typeof(float), typeof(DataGridView), 40f,
        propertyChanged: (b, _, n) =>
        {
            var view = (DataGridView)b;
            view._style.HeaderHeight = (float)n;
            view.InvalidateSurface();
        });

    /// <summary>Height of the column header row in pixels.</summary>
    public float HeaderHeight
    {
        get => (float)GetValue(HeaderHeightProperty);
        set => SetValue(HeaderHeightProperty, value);
    }

    // ── RowHeight ──

    /// <summary>Identifies the <see cref="RowHeight"/> bindable property.</summary>
    public static readonly BindableProperty RowHeightProperty = BindableProperty.Create(
        nameof(RowHeight), typeof(float), typeof(DataGridView), 36f,
        propertyChanged: (b, _, n) =>
        {
            var view = (DataGridView)b;
            view._style.RowHeight = (float)n;
            view.InvalidateSurface();
        });

    /// <summary>Height of each data row in pixels.</summary>
    public float RowHeight
    {
        get => (float)GetValue(RowHeightProperty);
        set => SetValue(RowHeightProperty, value);
    }

    // ── GridDescription (Accessibility) ──

    /// <summary>Identifies the <see cref="GridDescription"/> bindable property.</summary>
    public static readonly BindableProperty GridDescriptionProperty = BindableProperty.Create(
        nameof(GridDescription), typeof(string), typeof(DataGridView), "Data grid",
        propertyChanged: (b, _, n) =>
        {
            var view = (DataGridView)b;
            SemanticProperties.SetDescription(view, (string)n);
        });

    /// <summary>Accessible description for screen readers.</summary>
    public string GridDescription
    {
        get => (string)GetValue(GridDescriptionProperty);
        set => SetValue(GridDescriptionProperty, value);
    }

    // ── Columns Bindable Property ──

    /// <summary>Identifies the <see cref="Columns"/> bindable property.</summary>
    public static readonly BindableProperty ColumnsProperty = BindableProperty.Create(
        nameof(Columns),
        typeof(ObservableCollection<DataGridColumn>),
        typeof(DataGridView),
        defaultValue: null,
        propertyChanged: (b, o, n) => ((DataGridView)b).OnColumnsChanged(
            o as ObservableCollection<DataGridColumn>,
            n as ObservableCollection<DataGridColumn>));

    /// <summary>
    /// Bindable collection of column definitions. Supports ObservableCollection
    /// for dynamic add/remove of columns at runtime.
    /// </summary>
    public ObservableCollection<DataGridColumn>? Columns
    {
        get => (ObservableCollection<DataGridColumn>?)GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    private void OnColumnsChanged(
        ObservableCollection<DataGridColumn>? oldCollection,
        ObservableCollection<DataGridColumn>? newCollection)
    {
        if (oldCollection != null)
            oldCollection.CollectionChanged -= OnColumnsCollectionChanged;

        if (newCollection != null)
        {
            newCollection.CollectionChanged += OnColumnsCollectionChanged;
            _dataSource.SetColumns(newCollection);
        }
        else
        {
            _dataSource.SetColumns(Array.Empty<DataGridColumn>());
        }

        InvalidateSurface();
    }

    private void OnColumnsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (Columns != null)
            _dataSource.SetColumns(Columns);
        InvalidateSurface();
    }

    // ── TableSummaryRows Bindable Property ──

    /// <summary>Identifies the <see cref="TableSummaryRows"/> bindable property.</summary>
    public static readonly BindableProperty TableSummaryRowsProperty = BindableProperty.Create(
        nameof(TableSummaryRows),
        typeof(ObservableCollection<TableSummaryRow>),
        typeof(DataGridView),
        defaultValue: null,
        propertyChanged: (b, o, n) => ((DataGridView)b).OnTableSummaryRowsChanged(
            o as ObservableCollection<TableSummaryRow>,
            n as ObservableCollection<TableSummaryRow>));

    /// <summary>
    /// Bindable collection of table summary row definitions.
    /// Define summary rows in XAML with column aggregates.
    /// </summary>
    public ObservableCollection<TableSummaryRow>? TableSummaryRows
    {
        get => (ObservableCollection<TableSummaryRow>?)GetValue(TableSummaryRowsProperty);
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
        if (TableSummaryRows != null)
            SyncTableSummaryRows(TableSummaryRows);
    }

    private void SyncTableSummaryRows(IEnumerable<TableSummaryRow> rows)
    {
        _dataSource.ClearTableSummaryRows();
        foreach (var row in rows)
            _dataSource.AddTableSummaryRow(row);
    }

    /// <summary>Invalidate the canvas surface to trigger a repaint.</summary>
    private void InvalidateSurface() => _canvasView.InvalidateSurface();

    /// <summary>Update the accessibility hint with current grid dimensions.</summary>
    private void UpdateAccessibilityHint()
    {
        int rows = _dataSource.RowCount;
        int cols = _dataSource.Columns.Count;
        string hint = $"{rows} rows, {cols} columns";

        if (_selection.CurrentCell.IsValid)
            hint += $", current cell row {_selection.CurrentCell.Row + 1} column {_selection.CurrentCell.Column + 1}";

        SemanticProperties.SetHint(this, hint);
    }

    // ── Rendering ────────────────────────────────────────────────

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var info = e.Info;

        var bg = _style.BackgroundColor;
        canvas.Clear(new SKColor(bg.R, bg.G, bg.B, bg.A));

        float canvasWidth = _canvasView.Width > 0 ? (float)_canvasView.Width : 1;
        float scale = (float)(info.Width / canvasWidth);
        _scroll.ViewportWidth = info.Width / scale;
        _scroll.ViewportHeight = info.Height / scale;

        canvas.Save();
        canvas.Scale(scale);

        using var drawingContext = new SkiaDrawingContext(canvas);

        _renderer.Render(drawingContext, _dataSource, _scroll, _selection, _style,
            _inputController.DragColumnIndex, _inputController.DragColumnScreenX,
            _inputController.DragRowIndex, _inputController.DragRowScreenY,
            _editSession, _inputController.PopupManager);

        canvas.Restore();
    }

    // ── Touch / Input ────────────────────────────────────────────

    private void OnCanvasTouch(object? sender, SKTouchEventArgs e)
    {
        // On desktop platforms, focus keyboard input on press so we can capture
        // key events for grid navigation (arrow keys, Tab, etc.).
        // On mobile (iOS/Android), we do NOT focus here because it would
        // show the software keyboard during scrolling and other non-edit interactions.
        // Mobile keyboard focus is handled in OnEditSessionCellBeginEdit instead.
        if (e.ActionType == SKTouchAction.Pressed)
        {
#if WINDOWS || MACCATALYST
            FocusKeyboardInput();
#endif
        }

        float canvasWidth = _canvasView.Width > 0 ? (float)_canvasView.Width : 1;
        float scale = (float)(CanvasSize.Width / canvasWidth);
        float x = e.Location.X / scale;
        float y = e.Location.Y / scale;

        // Detect double-tap — store on Pressed so Released also gets ClickCount=2
        int clickCount = 1;
        if (e.ActionType == SKTouchAction.Pressed)
        {
            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
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

            // Start long press detection
            _longPressFired = false;
            _longPressX = x;
            _longPressY = y;
            StartLongPressTimer(x, y);
        }
        else if (e.ActionType == SKTouchAction.Released)
        {
            // Carry the click count from the preceding Pressed event
            clickCount = _pendingClickCount;
            _pendingClickCount = 1;
            CancelLongPressTimer();
        }
        else if (e.ActionType == SKTouchAction.Moved)
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

        // If long press already fired, suppress the Released event to avoid double-triggering
        if (_longPressFired && e.ActionType == SKTouchAction.Released)
        {
            _longPressFired = false;
            e.Handled = true;
            return;
        }

        var gridEvent = new GridPointerEventArgs
        {
            X = x,
            Y = y,
            Action = MapAction(e.ActionType),
            Button = e.MouseButton switch
            {
                SKMouseButton.Right => PointerButton.Secondary,
                SKMouseButton.Middle => PointerButton.Middle,
                _ => PointerButton.Primary
            },
            ScrollDeltaY = e.ActionType == SKTouchAction.WheelChanged ? e.WheelDelta : 0,
            TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ClickCount = clickCount
        };

        _inputController.HandlePointer(gridEvent, _scroll, _selection, _style, _dataSource);

        // Start/stop inertial scroll timer
        if ((e.ActionType == SKTouchAction.Released || e.ActionType == SKTouchAction.Cancelled)
            && _inputController.IsInertialScrolling)
            StartInertialScrollTimer();

        e.Handled = gridEvent.Handled;
    }

    private static InputAction MapAction(SKTouchAction action) => action switch
    {
        SKTouchAction.Pressed => InputAction.Pressed,
        SKTouchAction.Released => InputAction.Released,
        SKTouchAction.Moved => InputAction.Moved,
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
        _longPressFired = true;

        // Synthesize a LongPress event for the input controller
        float canvasWidth = _canvasView.Width > 0 ? (float)_canvasView.Width : 1;
        float scale = (float)(CanvasSize.Width / canvasWidth);

        var gridEvent = new GridPointerEventArgs
        {
            X = _longPressX,
            Y = _longPressY,
            Action = InputAction.LongPress,
            Button = PointerButton.Primary,
            TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ClickCount = 1
        };

        _inputController.HandlePointer(gridEvent, _scroll, _selection, _style, _dataSource);
    }

    private void StartInertialScrollTimer()
    {
        if (_scrollTimer != null) return;

        _scrollTimer = Dispatcher.CreateTimer();
        _scrollTimer.Interval = TimeSpan.FromMilliseconds(16); // ~60fps
        _scrollTimer.Tick += OnScrollTimerTick;
        _scrollTimer.Start();
    }

    private void OnScrollTimerTick(object? sender, EventArgs e)
    {
        if (!_inputController.UpdateInertialScroll(_scroll, 16f))
        {
            StopInertialScrollTimer();
        }
    }

    private void StopInertialScrollTimer()
    {
        if (_scrollTimer != null)
        {
            _scrollTimer.Stop();
            _scrollTimer.Tick -= OnScrollTimerTick;
            _scrollTimer = null;
        }
    }

    // ── Column reorder event ──

    private void OnColumnReordered(object? sender, ColumnReorderedEventArgs e)
    {
        _dataSource.ReorderColumn(e.OldIndex, e.NewIndex);
    }

    // ── Row reorder event ──

    private void OnRowReordered(object? sender, RowReorderedEventArgs e)
    {
        _dataSource.ReorderRow(e.OldIndex, e.NewIndex);
    }

    // ── Auto-fit column event ──

    private void OnAutoFitColumnRequested(object? sender, AutoFitColumnEventArgs e)
    {
        // Use a temporary SKBitmap/Canvas for text measurement
        using var bitmap = new SKBitmap(1, 1);
        using var canvas = new SKCanvas(bitmap);
        var measureCtx = new SkiaDrawingContext(canvas);
        var layoutEngine = new GridLayoutEngine();

        float optimalWidth = layoutEngine.CalculateAutoFitWidth(
            e.Column, _dataSource, measureCtx, _style);

        e.Column.Width = optimalWidth;
        InvalidateSurface();
    }

    // ── Named event handlers (avoid anonymous lambdas for clean unsubscribe) ──

    private void OnDataSourceDataChanged()
    {
        _canvasView.InvalidateSurface();
        UpdateAccessibilityHint();
    }

    private void OnInputControllerNeedsRedraw() => _canvasView.InvalidateSurface();

    private void OnEditSessionNeedsRedraw() => _canvasView.InvalidateSurface();

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

        // Stop timers first (they capture 'this' via Tick delegates)
        StopInertialScrollTimer();
        StopCursorBlinkTimer();

        // Unsubscribe from internal owned objects
        _dataSource.DataChanged -= OnDataSourceDataChanged;
        _inputController.NeedsRedraw -= OnInputControllerNeedsRedraw;
        _inputController.ColumnReordered -= OnColumnReordered;
        _inputController.RowReordered -= OnRowReordered;
        _inputController.AutoFitColumnRequested -= OnAutoFitColumnRequested;
        _inputController.KeyboardFocusRequested -= OnKeyboardFocusRequested;
        _inputController.FilterPopupOpened -= OnFilterPopupOpened;
        _inputController.FilterPopupClosed -= OnFilterPopupClosed;
        _editSession.NeedsRedraw -= OnEditSessionNeedsRedraw;
        _editSession.CellBeginEdit -= OnEditSessionCellBeginEdit;
        _editSession.CellEndEdit -= OnEditSessionCellEndEdit;

        // Unsubscribe canvas events
        _canvasView.PaintSurface -= OnPaintSurface;
        _canvasView.Touch -= OnCanvasTouch;

        // Unsubscribe keyboard proxy events
        _keyboardProxy.TextChanged -= OnKeyboardProxyTextChanged;
        _keyboardProxy.Completed -= OnKeyboardProxyCompleted;

        // Unsubscribe bound collection
        if (_boundCollectionChangedSource != null)
        {
            _boundCollectionChangedSource.CollectionChanged -= OnBoundItemsCollectionChanged;
            _boundCollectionChangedSource = null;
        }

#if MACCATALYST || IOS
        // Remove native keyboard input
        if (_nativeKeyInput != null)
        {
            _nativeKeyInput.TextInserted -= OnNativeTextInserted;
            _nativeKeyInput.BackspaceDeleted -= OnNativeBackspace;
            _nativeKeyInput.KeyPressed -= OnNativeKeyPress;
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

        // Unsubscribe handler changed (prevent duplicate native setup on re-attach)
        this.HandlerChanged -= OnHandlerChanged_SetupNativeKeyboard;
        this.HandlerChanged -= OnHandlerChanged_SetupNativeScroll;
#endif

#if ANDROID
        _keyboardProxy.HandlerChanged -= OnKeyboardProxyHandlerChanged_Android;
#endif
    }

    /// <summary>
    /// Re-subscribe all event handlers after the view is re-connected to its handler.
    /// Mirrors the constructor subscriptions so the view works after navigation back.
    /// </summary>
    private void Reconnect()
    {
        _dataSource.DataChanged += OnDataSourceDataChanged;
        _inputController.NeedsRedraw += OnInputControllerNeedsRedraw;
        _inputController.ColumnReordered += OnColumnReordered;
        _inputController.RowReordered += OnRowReordered;
        _inputController.AutoFitColumnRequested += OnAutoFitColumnRequested;
        _inputController.KeyboardFocusRequested += OnKeyboardFocusRequested;
        _inputController.FilterPopupOpened += OnFilterPopupOpened;
        _inputController.FilterPopupClosed += OnFilterPopupClosed;
        _editSession.NeedsRedraw += OnEditSessionNeedsRedraw;
        _editSession.CellBeginEdit += OnEditSessionCellBeginEdit;
        _editSession.CellEndEdit += OnEditSessionCellEndEdit;

        _canvasView.PaintSurface += OnPaintSurface;
        _canvasView.Touch += OnCanvasTouch;

        _keyboardProxy.TextChanged += OnKeyboardProxyTextChanged;
        _keyboardProxy.Completed += OnKeyboardProxyCompleted;

#if MACCATALYST || IOS
        this.HandlerChanged += OnHandlerChanged_SetupNativeKeyboard;
        this.HandlerChanged += OnHandlerChanged_SetupNativeScroll;
#endif

#if ANDROID
        _keyboardProxy.HandlerChanged += OnKeyboardProxyHandlerChanged_Android;
#endif
    }

    // ── Events ───────────────────────────────────────────────────

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

    // ── Keyboard Proxy Handlers (Entry-based, Android/Windows fallback) ──

    private void OnKeyboardProxyTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_suppressTextChanged) return;

#if MACCATALYST || IOS
        // On Apple platforms, prefer native keyboard input
        if (_useNativeKeyInput) return;
#endif

        string oldText = e.OldTextValue ?? string.Empty;
        string newText = e.NewTextValue ?? string.Empty;

        // No meaningful change
        if (newText == oldText) return;

        // Programmatic reset back to sentinel — ignore
        if (newText == KeyboardSentinel) return;

        // Detect backspace: text became empty (user deleted the sentinel)
        if (string.IsNullOrEmpty(newText))
        {
            SendKeyEvent(GridKey.Backspace);
            ResetKeyboardProxy();
            return;
        }

        // Extract new characters using content-based diffing.
        // This handles both standard append behavior (e.g., sentinel + "a" on desktop)
        // and Android IME replacement behavior (sentinel replaced entirely with "a").
        string newInput;

        if (newText.Length > oldText.Length && newText.StartsWith(oldText))
        {
            // Standard append: characters added after existing text
            newInput = newText.Substring(oldText.Length);
        }
        else
        {
            // IME replacement or other non-standard input:
            // Remove the sentinel character if still present, treat rest as input.
            int sentinelIdx = newText.IndexOf(KeyboardSentinel, StringComparison.Ordinal);
            if (sentinelIdx >= 0)
                newInput = newText.Remove(sentinelIdx, KeyboardSentinel.Length);
            else
                newInput = newText; // Sentinel fully replaced — all chars are new input
        }

        // Forward each new character
        foreach (char ch in newInput)
        {
            SendKeyEvent(GridKey.Character, ch);
        }

        ResetKeyboardProxy();
    }

    private void OnKeyboardProxyCompleted(object? sender, EventArgs e)
    {
#if MACCATALYST || IOS
        if (_useNativeKeyInput) return;
#endif
        SendKeyEvent(GridKey.Enter);
        // Only re-focus if editing continues (e.g. DismissKeyboardOnEnter=false
        // auto-edits the next cell). When editing has ended, the keyboard should
        // stay dismissed — UnfocusKeyboardInput() was already called by CellEndEdit.
        if (_editSession.IsEditing)
            FocusKeyboardInput();
    }

    private void ResetKeyboardProxy()
    {
        _suppressTextChanged = true;
        _keyboardProxy.Text = KeyboardSentinel;
        _suppressTextChanged = false;
    }

    /// <summary>Focus the appropriate keyboard input mechanism for the current platform.</summary>
    private void FocusKeyboardInput()
    {
#if MACCATALYST || IOS
        if (_useNativeKeyInput && _nativeKeyInput != null)
        {
            _nativeKeyInput.BecomeFirstResponder();
            return;
        }
#endif
        if (!_keyboardProxy.IsFocused)
        {
            ResetKeyboardProxy();
            _keyboardProxy.Focus();
        }
    }

    /// <summary>Dismiss the software keyboard by removing focus from the keyboard input mechanism.</summary>
    private void UnfocusKeyboardInput()
    {
#if MACCATALYST || IOS
        if (_useNativeKeyInput && _nativeKeyInput != null)
        {
            _nativeKeyInput.ResignFirstResponder();
            return;
        }
#endif
        if (_keyboardProxy.IsFocused)
        {
            _keyboardProxy.Unfocus();
        }
    }

    // ── Cursor Blink Timer ───────────────────────────────────────

    private void StartCursorBlinkTimer()
    {
        if (_cursorBlinkTimer != null) return;

        _cursorBlinkTimer = Dispatcher.CreateTimer();
        _cursorBlinkTimer.Interval = TimeSpan.FromMilliseconds(530);
        _cursorBlinkTimer.Tick += OnCursorBlinkTimerTick;
        _cursorBlinkTimer.Start();
    }

    private void OnCursorBlinkTimerTick(object? sender, EventArgs e)
    {
        if (_editSession.IsEditing || _filterPopupActive)
            _canvasView.InvalidateSurface();
        else
            StopCursorBlinkTimer();
    }

    private void StopCursorBlinkTimer()
    {
        if (_cursorBlinkTimer != null)
        {
            _cursorBlinkTimer.Stop();
            _cursorBlinkTimer.Tick -= OnCursorBlinkTimerTick;
            _cursorBlinkTimer = null;
        }
    }

    private void SendKeyEvent(GridKey key, char? character = null, InputModifiers modifiers = InputModifiers.None)
    {
        var keyEvent = new GridKeyEventArgs
        {
            Key = key,
            Character = character,
            Modifiers = modifiers
        };

        _inputController.HandleKey(keyEvent, _scroll, _selection, _style, _dataSource);
    }

#if ANDROID
    // ── Android Keyboard Configuration ───────────────────────────

    private void OnKeyboardProxyHandlerChanged_Android(object? sender, EventArgs e)
    {
        if (_keyboardProxy.Handler?.PlatformView is Android.Widget.EditText editText)
        {
            // Disable IME predictions and composing behavior to ensure reliable
            // character-by-character input through the hidden Entry proxy.
            editText.InputType = AndroidInputTypes.ClassText | AndroidInputTypes.TextFlagNoSuggestions;
            editText.ImeOptions = AndroidImeAction.Done;
        }
    }
#endif

#if MACCATALYST || IOS
    // ── Native Keyboard Input (Apple platforms) ──────────────────

    private void OnHandlerChanged_SetupNativeKeyboard(object? sender, EventArgs e)
    {
        // Guard against duplicate setup (HandlerChanged can fire multiple times)
        if (_nativeKeyInput != null) return;

        if (Handler?.PlatformView is UIView platformView)
        {
            _nativeKeyInput = new KeyInputResponder(CGRect.Empty)
            {
                Frame = new CGRect(0, 0, 0, 0),
                Alpha = 0f
            };

            _nativeKeyInput.TextInserted += OnNativeTextInserted;
            _nativeKeyInput.BackspaceDeleted += OnNativeBackspace;
            _nativeKeyInput.KeyPressed += OnNativeKeyPress;
            _nativeKeyInput.TabKeyReceived += OnNativeTabKey;
            _nativeKeyInput.NavigationKeyReceived += OnNativeNavigationKey;

            platformView.AddSubview(_nativeKeyInput);
            _useNativeKeyInput = true;
        }
    }

    private void OnHandlerChanged_SetupNativeScroll(object? sender, EventArgs e)
    {
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
        }
    }

    private void HandleNativeScrollGesture(UIPanGestureRecognizer recognizer)
    {
        var translation = recognizer.TranslationInView(recognizer.View);

        // Apply scroll — translation is in points, negate for natural scrolling direction
        float dx = -(float)translation.X;
        float dy = -(float)translation.Y;

        _scroll.ScrollBy(dx, dy);
        _canvasView.InvalidateSurface();

        // Reset translation so we get deltas, not cumulative values
        recognizer.SetTranslation(CGPoint.Empty, recognizer.View);
    }

    private void OnNativeTextInserted(string text)
    {
        foreach (char ch in text)
        {
            if (ch == '\n' || ch == '\r')
            {
                // iOS software keyboard sends Return as InsertText("\n")
                // rather than through PressesBegan, so map it to Enter.
                SendKeyEvent(GridKey.Enter);
            }
            else if (ch == '\t')
            {
                // Tab character from software keyboard — map to GridKey.Tab
                SendKeyEvent(GridKey.Tab);
            }
            else
            {
                SendKeyEvent(GridKey.Character, ch);
            }
        }
    }

    private void OnNativeTabKey(bool hasShift)
    {
        var modifiers = hasShift ? InputModifiers.Shift : InputModifiers.None;
        SendKeyEvent(GridKey.Tab, null, modifiers);
    }

    private void OnNativeNavigationKey(GridKey key, InputModifiers modifiers)
    {
        SendKeyEvent(key, null, modifiers);
    }

    private void OnNativeBackspace()
    {
        SendKeyEvent(GridKey.Backspace);
    }

    private void OnNativeKeyPress(UIPress press)
    {
        if (press.Key == null) return;

        var modifiers = InputModifiers.None;
        var flags = press.Key.ModifierFlags;
        if (flags.HasFlag(UIKeyModifierFlags.Shift)) modifiers |= InputModifiers.Shift;
        if (flags.HasFlag(UIKeyModifierFlags.Command)) modifiers |= InputModifiers.Control;
        if (flags.HasFlag(UIKeyModifierFlags.Alternate)) modifiers |= InputModifiers.Alt;

        var keyCode = press.Key.KeyCode;
        GridKey? gridKey = keyCode switch
        {
            UIKeyboardHidUsage.KeyboardLeftArrow => GridKey.Left,
            UIKeyboardHidUsage.KeyboardRightArrow => GridKey.Right,
            UIKeyboardHidUsage.KeyboardUpArrow => GridKey.Up,
            UIKeyboardHidUsage.KeyboardDownArrow => GridKey.Down,
            UIKeyboardHidUsage.KeyboardHome => GridKey.Home,
            UIKeyboardHidUsage.KeyboardEnd => GridKey.End,
            UIKeyboardHidUsage.KeyboardPageUp => GridKey.PageUp,
            UIKeyboardHidUsage.KeyboardPageDown => GridKey.PageDown,
            UIKeyboardHidUsage.KeyboardEscape => GridKey.Escape,
            UIKeyboardHidUsage.KeyboardDeleteForward => GridKey.Delete,
            UIKeyboardHidUsage.KeyboardDeleteOrBackspace => GridKey.Backspace,
            UIKeyboardHidUsage.KeyboardReturnOrEnter => GridKey.Enter,
            UIKeyboardHidUsage.KeyboardTab => GridKey.Tab,
            UIKeyboardHidUsage.KeyboardSpacebar => GridKey.Space,
            UIKeyboardHidUsage.KeyboardF2 => GridKey.F2,
            _ => null
        };

        if (gridKey.HasValue)
        {
            SendKeyEvent(gridKey.Value, null, modifiers);
        }
        // Character input is handled via InsertText, not PressesBegan
    }
#endif
}

#if MACCATALYST || IOS
/// <summary>
/// Native UIView that acts as a keyboard input sink on Apple platforms.
/// Implements IUIKeyInput to receive character input and backspace,
/// and overrides PressesBegan for arrow keys, Escape, Delete, etc.
/// </summary>
internal class KeyInputResponder : UIView, IUIKeyInput
{
    public event Action<string>? TextInserted;
    public event Action? BackspaceDeleted;
    public event Action<UIPress>? KeyPressed;
    /// <summary>Fires when Tab is pressed via UIKeyCommand (bypasses iOS focus system).</summary>
    public event Action<bool>? TabKeyReceived;
    /// <summary>Fires when a navigation key is pressed via UIKeyCommand (bypasses iOS focus system).</summary>
    public event Action<GridKey, InputModifiers>? NavigationKeyReceived;

    // UIKeyCommand entries for Tab and navigation keys — takes priority over the
    // iOS focus system, which would otherwise steal these for focus navigation.
    private static readonly Selector _navKeySelector = new Selector("handleNavKey:");
    private static readonly UIKeyCommand[] _keyCommands = BuildKeyCommands();

    private static UIKeyCommand[] BuildKeyCommands()
    {
        var cmds = new List<UIKeyCommand>();

        // Tab / Shift+Tab
        cmds.Add(UIKeyCommand.Create((NSString)"\t", 0, new Selector("handleTabKey:")));
        cmds.Add(UIKeyCommand.Create((NSString)"\t", UIKeyModifierFlags.Shift, new Selector("handleShiftTabKey:")));

        // Arrow keys, Escape — each with plain + Shift variants
        var navInputs = new[]
        {
            UIKeyCommand.LeftArrow,
            UIKeyCommand.RightArrow,
            UIKeyCommand.UpArrow,
            UIKeyCommand.DownArrow,
            UIKeyCommand.Escape,
            UIKeyCommand.PageUp,
            UIKeyCommand.PageDown,
            UIKeyCommand.Home,
            UIKeyCommand.End,
        };

        foreach (var input in navInputs)
        {
            cmds.Add(UIKeyCommand.Create((NSString)input, 0, _navKeySelector));
            cmds.Add(UIKeyCommand.Create((NSString)input, UIKeyModifierFlags.Shift, _navKeySelector));
        }

        return cmds.ToArray();
    }

    public KeyInputResponder(CGRect frame) : base(frame) { }

    public override bool CanBecomeFirstResponder => true;

    public override UIKeyCommand[] KeyCommands => _keyCommands;

    [Export("handleTabKey:")]
    private void HandleTabKey(UIKeyCommand cmd) => TabKeyReceived?.Invoke(false);

    [Export("handleShiftTabKey:")]
    private void HandleShiftTabKey(UIKeyCommand cmd) => TabKeyReceived?.Invoke(true);

    [Export("handleNavKey:")]
    private void HandleNavKey(UIKeyCommand cmd)
    {
        var input = cmd.Input;
        if (input == null) return;

        var modifiers = InputModifiers.None;
        if (cmd.ModifierFlags.HasFlag(UIKeyModifierFlags.Shift)) modifiers |= InputModifiers.Shift;
        if (cmd.ModifierFlags.HasFlag(UIKeyModifierFlags.Command)) modifiers |= InputModifiers.Control;
        if (cmd.ModifierFlags.HasFlag(UIKeyModifierFlags.Alternate)) modifiers |= InputModifiers.Alt;

        GridKey? gridKey = input switch
        {
            var s when s == UIKeyCommand.LeftArrow => GridKey.Left,
            var s when s == UIKeyCommand.RightArrow => GridKey.Right,
            var s when s == UIKeyCommand.UpArrow => GridKey.Up,
            var s when s == UIKeyCommand.DownArrow => GridKey.Down,
            var s when s == UIKeyCommand.Escape => GridKey.Escape,
            var s when s == UIKeyCommand.PageUp => GridKey.PageUp,
            var s when s == UIKeyCommand.PageDown => GridKey.PageDown,
            var s when s == UIKeyCommand.Home => GridKey.Home,
            var s when s == UIKeyCommand.End => GridKey.End,
            _ => null
        };

        if (gridKey.HasValue)
            NavigationKeyReceived?.Invoke(gridKey.Value, modifiers);
    }

    public bool HasText => true;

    public void InsertText(string text)
    {
        TextInserted?.Invoke(text);
    }

    public void DeleteBackward()
    {
        BackspaceDeleted?.Invoke();
    }

    public override void PressesBegan(NSSet<UIPress> presses, UIPressesEvent evt)
    {
        bool handled = false;
        foreach (UIPress press in presses)
        {
            if (press.Key != null)
            {
                var keyCode = press.Key.KeyCode;
                // Only handle non-character keys here (characters come via InsertText)
                bool isNavigationKey = keyCode switch
                {
                    UIKeyboardHidUsage.KeyboardLeftArrow => true,
                    UIKeyboardHidUsage.KeyboardRightArrow => true,
                    UIKeyboardHidUsage.KeyboardUpArrow => true,
                    UIKeyboardHidUsage.KeyboardDownArrow => true,
                    UIKeyboardHidUsage.KeyboardHome => true,
                    UIKeyboardHidUsage.KeyboardEnd => true,
                    UIKeyboardHidUsage.KeyboardPageUp => true,
                    UIKeyboardHidUsage.KeyboardPageDown => true,
                    UIKeyboardHidUsage.KeyboardEscape => true,
                    UIKeyboardHidUsage.KeyboardDeleteForward => true,
                    UIKeyboardHidUsage.KeyboardReturnOrEnter => true,
                    UIKeyboardHidUsage.KeyboardTab => true,
                    UIKeyboardHidUsage.KeyboardSpacebar => true,
                    UIKeyboardHidUsage.KeyboardF2 => true,
                    _ => false
                };

                if (isNavigationKey)
                {
                    KeyPressed?.Invoke(press);
                    handled = true;
                }
            }
        }

        if (!handled)
            base.PressesBegan(presses, evt);
    }
}
#endif

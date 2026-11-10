using KumikoUI.Core.Input;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>
/// Base class for all custom-drawn UI components (TextBox, CheckBox, ComboBox, etc.).
/// These are entirely rendered on the canvas — no native UI controls.
/// </summary>
public abstract class DrawnComponent
{
    private KumikoRect _bounds;
    private bool _isFocused;
    private bool _isEnabled = true;
    private bool _isVisible = true;
    private int _zOrder;

    /// <summary>Gets or sets the bounding rectangle of this component.</summary>
    public KumikoRect Bounds
    {
        get => _bounds;
        set
        {
            if (!_bounds.Equals(value))
            {
                _bounds = value;
                OnBoundsChanged();
            }
        }
    }

    /// <summary>Gets or sets whether this component has keyboard focus.</summary>
    public bool IsFocused
    {
        get => _isFocused;
        internal set
        {
            if (_isFocused == value) return;
            _isFocused = value;
            if (value) OnGotFocus();
            else OnLostFocus();
            InvalidateVisual();
        }
    }

    /// <summary>Gets or sets whether this component accepts input.</summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value) return;
            _isEnabled = value;
            InvalidateVisual();
        }
    }

    /// <summary>Gets or sets whether this component is visible.</summary>
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible == value) return;
            _isVisible = value;
            InvalidateVisual();
        }
    }

    /// <summary>Z-order for overlapping components. Higher = on top.</summary>
    public int ZOrder
    {
        get => _zOrder;
        set => _zOrder = value;
    }

    /// <summary>Optional tag for identification.</summary>
    public object? Tag { get; set; }

    // ── Rendering ────────────────────────────────────────────────

    /// <summary>Draw this component. Override in derived classes.</summary>
    public abstract void OnDraw(IDrawingContext ctx);

    /// <summary>
    /// When <see langword="true"/>, the grid immediately forwards the activating tap
    /// to the editor as a synthetic press + release after <c>BeginEdit</c> is called.
    /// This allows editors like <c>DrawnActionButtons</c> to respond on the very
    /// first tap rather than requiring a second tap to interact.
    /// Default: <see langword="false"/>.
    /// </summary>
    public virtual bool ActivatesImmediately => false;

    /// <summary>Request a redraw of the hosting surface.</summary>
    public void InvalidateVisual() => RedrawRequested?.Invoke();

    /// <summary>Raised when the component needs to be redrawn.</summary>
    public event Action? RedrawRequested;

    // ── Input ────────────────────────────────────────────────────

    /// <summary>Returns true if the point is within this component's bounds.</summary>
    public virtual bool HitTest(float x, float y) =>
        IsVisible && IsEnabled && _bounds.Contains(x, y);

    /// <summary>Handle pointer down. Return true if handled.</summary>
    public virtual bool OnPointerDown(KumikoPointerEventArgs e) => false;

    /// <summary>Handle pointer up. Return true if handled.</summary>
    public virtual bool OnPointerUp(KumikoPointerEventArgs e) => false;

    /// <summary>Handle pointer move. Return true if handled.</summary>
    public virtual bool OnPointerMove(KumikoPointerEventArgs e) => false;

    /// <summary>
    /// Handle mouse-wheel or trackpad scrolling (<see cref="KumikoPointerEventArgs.ScrollDeltaX"/>/<see cref="KumikoPointerEventArgs.ScrollDeltaY"/>,
    /// in wheel notches, positive up). Return true if handled.
    /// </summary>
    public virtual bool OnScroll(KumikoPointerEventArgs e) => false;

    /// <summary>Handle key down. Return true if handled.</summary>
    public virtual bool OnKeyDown(KumikoKeyEventArgs e) => false;

    /// <summary>Handle key up. Return true if handled.</summary>
    public virtual bool OnKeyUp(KumikoKeyEventArgs e) => false;

    // ── Focus ────────────────────────────────────────────────────

    /// <summary>Called when this component gains focus.</summary>
    protected virtual void OnGotFocus() { }

    /// <summary>Called when this component loses focus.</summary>
    protected virtual void OnLostFocus() { }

    /// <summary>Called when bounds change.</summary>
    protected virtual void OnBoundsChanged() { }

    // ── Value binding ────────────────────────────────────────────

    /// <summary>Raised when the component's value changes.</summary>
    public event EventHandler<ComponentValueChangedEventArgs>? ValueChanged;

    /// <summary>
    /// Raised when the editor signals that its value is finalized and interaction
    /// is complete (e.g. date picked, combo item selected). The hosting edit
    /// session should commit the value when this fires.
    /// </summary>
    public event Action? EditCompleted;

    /// <summary>Invoke the ValueChanged event.</summary>
    protected void RaiseValueChanged(object? oldValue, object? newValue)
    {
        if (_trackingChanges) HasChangedSinceTracking = true;
        ValueChanged?.Invoke(this, new ComponentValueChangedEventArgs(oldValue, newValue));
    }

    // ── Change tracking (for IValueEditor.IsDirty) ───────────────

    private bool _trackingChanges;

    /// <summary>Whether <see cref="StartTrackingChanges"/> was called.</summary>
    internal bool IsTrackingChanges => _trackingChanges;

    /// <summary>Whether the value changed (<see cref="ValueChanged"/>) since <see cref="StartTrackingChanges"/>.</summary>
    internal bool HasChangedSinceTracking { get; private set; }

    /// <summary>
    /// Start counting value changes: called once an editor holds its cell's value, so that only changes
    /// made after that (by the user) make it dirty.
    /// </summary>
    internal void StartTrackingChanges()
    {
        _trackingChanges = true;
        HasChangedSinceTracking = false;
    }

    /// <summary>
    /// Signal that the editor's value is final and the edit session should commit.
    /// Call this from editors that have a definitive "selection made" gesture
    /// (e.g. day clicked in calendar, item picked in combo dropdown).
    /// </summary>
    protected void RaiseEditCompleted() => EditCompleted?.Invoke();
}

namespace KumikoUI.Core.Models;

/// <summary>
/// Tracks scroll position and visible row/column ranges.
/// All values are in logical (unscaled) pixels.
/// </summary>
public sealed class ScrollState
{
    private float _offsetX;
    private float _offsetY;

    /// <summary>
    /// Raised when <see cref="OffsetX"/> or <see cref="OffsetY"/> changes, once per change: setting a property
    /// to its current value, or a <see cref="ClampOffset"/> that moves nothing, raises nothing.
    /// </summary>
    public event EventHandler<KumikoScrollChangedEventArgs>? ScrollChanged;

    /// <summary>Horizontal scroll offset in pixels.</summary>
    public float OffsetX
    {
        get => _offsetX;
        set => SetOffsets(value, _offsetY);
    }

    /// <summary>Vertical scroll offset in pixels.</summary>
    public float OffsetY
    {
        get => _offsetY;
        set => SetOffsets(_offsetX, value);
    }

    /// <summary>Total content width (sum of column widths).</summary>
    public float ContentWidth { get; set; }

    /// <summary>Total content height (row count × row height + header).</summary>
    public float ContentHeight { get; set; }

    /// <summary>Viewport width.</summary>
    public float ViewportWidth { get; set; }

    /// <summary>Viewport height.</summary>
    public float ViewportHeight { get; set; }

    /// <summary>Maximum horizontal scroll offset (content width minus viewport width, minimum 0).</summary>
    public float MaxOffsetX => Math.Max(0, ContentWidth - ViewportWidth);
    /// <summary>Maximum vertical scroll offset (content height minus viewport height, minimum 0).</summary>
    public float MaxOffsetY => Math.Max(0, ContentHeight - ViewportHeight);

    /// <summary>Clamps the current scroll offset to valid bounds.</summary>
    public void ClampOffset() =>
        SetOffsets(Math.Clamp(_offsetX, 0, MaxOffsetX), Math.Clamp(_offsetY, 0, MaxOffsetY));

    /// <summary>Scrolls by the specified deltas and clamps the result.</summary>
    public void ScrollBy(float dx, float dy) =>
        SetOffsets(Math.Clamp(_offsetX + dx, 0, MaxOffsetX), Math.Clamp(_offsetY + dy, 0, MaxOffsetY));

    /// <summary>
    /// Scrolls to the given offsets, clamped to the valid range, raising <see cref="ScrollChanged"/> once when either
    /// moved (setting <see cref="OffsetX"/> and then <see cref="OffsetY"/> would raise it twice).
    /// </summary>
    /// <param name="offsetX">The horizontal offset.</param>
    /// <param name="offsetY">The vertical offset.</param>
    public void ScrollTo(float offsetX, float offsetY) =>
        SetOffsets(Math.Clamp(offsetX, 0, MaxOffsetX), Math.Clamp(offsetY, 0, MaxOffsetY));

    private void SetOffsets(float x, float y)
    {
        float oldX = _offsetX, oldY = _offsetY;
        if (oldX.Equals(x) && oldY.Equals(y)) return;
        _offsetX = x;
        _offsetY = y;
        ScrollChanged?.Invoke(this, new KumikoScrollChangedEventArgs(oldX, oldY, x, y));
    }
}

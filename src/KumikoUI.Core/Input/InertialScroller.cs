namespace KumikoUI.Core.Input;

/// <summary>
/// Tracks inertial scrolling (fling) with deceleration.
/// </summary>
public class InertialScroller
{
    private float _velocityX;
    private float _velocityY;
    private bool _isActive;
    private long _lastTimestampMs;

    // Smoothed velocity samples for less jerky tracking
    private float _smoothVelocityX;
    private float _smoothVelocityY;

    /// <summary>Configurable scroll settings. Shared with KumikoDataGrid.</summary>
    public ScrollSettings Settings { get; set; } = new();

    /// <summary>Is inertial scroll currently animating?</summary>
    public bool IsActive => _isActive;

    /// <summary>
    /// Record a velocity sample from touch/pan move events.
    /// Call this on every pointer-move during a pan.
    /// Uses exponential moving average for smoother velocity tracking.
    /// </summary>
    public void TrackVelocity(float dx, float dy, long timestampMs)
    {
        if (_lastTimestampMs > 0)
        {
            float dt = (timestampMs - _lastTimestampMs) / 1000f;
            if (dt > 0 && dt < 0.2f)  // Ignore stale or zero-gap samples
            {
                float rawVx = Math.Clamp(dx / dt, -Settings.MaxVelocity, Settings.MaxVelocity);
                float rawVy = Math.Clamp(dy / dt, -Settings.MaxVelocity, Settings.MaxVelocity);

                float s = Settings.VelocitySmoothing;
                _smoothVelocityX = _smoothVelocityX * s + rawVx * (1f - s);
                _smoothVelocityY = _smoothVelocityY * s + rawVy * (1f - s);

                _velocityX = _smoothVelocityX;
                _velocityY = _smoothVelocityY;
            }
        }
        else
        {
            // First sample — no smoothing
            _smoothVelocityX = 0;
            _smoothVelocityY = 0;
        }
        _lastTimestampMs = timestampMs;
    }

    /// <summary>
    /// Start the inertial animation with the tracked velocity.
    /// Call this when the pointer is released after a pan.
    /// </summary>
    public void StartFling()
    {
        if (!Settings.EnableInertia)
        {
            _isActive = false;
            return;
        }

        float threshold = Settings.MinVelocity * Settings.FlingThresholdMultiplier;
        _isActive = Math.Abs(_velocityX) > threshold ||
                    Math.Abs(_velocityY) > threshold;
    }

    /// <summary>
    /// Reset tracking and cancel any active fling.
    /// </summary>
    public void Reset()
    {
        _velocityX = 0;
        _velocityY = 0;
        _smoothVelocityX = 0;
        _smoothVelocityY = 0;
        _isActive = false;
        _lastTimestampMs = 0;
    }

    /// <summary>
    /// Call each frame to get the scroll delta.
    /// Returns (dx, dy) to apply to scroll offset.
    /// </summary>
    public (float dx, float dy) Update(float frameIntervalMs = 16f)
    {
        if (!_isActive) return (0, 0);

        float factor = frameIntervalMs / 16f; // Normalize to 60fps baseline
        float dx = _velocityX * (frameIntervalMs / 1000f);
        float dy = _velocityY * (frameIntervalMs / 1000f);

        _velocityX *= MathF.Pow(Settings.Friction, factor);
        _velocityY *= MathF.Pow(Settings.Friction, factor);

        if (Math.Abs(_velocityX) < Settings.MinVelocity &&
            Math.Abs(_velocityY) < Settings.MinVelocity)
        {
            _isActive = false;
            return (0, 0);
        }

        return (dx, dy);
    }
}

namespace KumikoUI.Core.Input;

/// <summary>
/// Tracks inertial scrolling (fling) with deceleration.
/// </summary>
public sealed class InertialScroller
{
    private float _velocityX;
    private float _velocityY;
    private bool _isActive;
    private bool _hasSample;
    private long _lastTimestampMs;

    // Smoothed velocity samples for less jerky tracking
    private float _smoothVelocityX;
    private float _smoothVelocityY;

    /// <summary>Longest frame step, in milliseconds, that <see cref="Update"/> will integrate in one call.</summary>
    private const float MaxFrameIntervalMs = 100f;

    /// <summary>Configurable scroll settings. Shared with KumikoDataGrid.</summary>
    public ScrollSettings Settings { get; set; } = new();

    /// <summary>Is inertial scroll currently animating?</summary>
    public bool IsActive => _isActive;

    /// <summary>
    /// Number of velocity samples ignored since the last <see cref="Reset"/> because their timestamp was missing
    /// (zero or negative), duplicated, or earlier than the previous sample. A host that never sets the pointer
    /// timestamp sees this grow with every move and gets no fling; use it to diagnose that.
    /// </summary>
    public int IgnoredSampleCount { get; private set; }

    /// <summary>
    /// Record a velocity sample from touch/pan move events.
    /// Call this on every pointer-move during a pan.
    /// Uses exponential moving average for smoother velocity tracking.
    /// Samples whose timestamp is missing, duplicated or moves backward are ignored and counted in
    /// <see cref="IgnoredSampleCount"/>; a gap longer than <see cref="ScrollSettings.FlingHoldCutoffMs"/> since the
    /// previous sample drops the accumulated velocity, because the finger paused.
    /// </summary>
    public void TrackVelocity(float dx, float dy, long timestampMs)
    {
        if (timestampMs <= 0 || !float.IsFinite(dx) || !float.IsFinite(dy))
        {
            IgnoredSampleCount++;
            return;
        }

        if (!_hasSample)
        {
            // First sample — no smoothing, nothing to measure a delta against.
            _smoothVelocityX = 0;
            _smoothVelocityY = 0;
            _velocityX = 0;
            _velocityY = 0;
            _hasSample = true;
            _lastTimestampMs = timestampMs;
            return;
        }

        long gapMs = timestampMs - _lastTimestampMs;
        if (gapMs <= 0)
        {
            // Duplicate or backward timestamp: no usable interval. Keep the newest time seen.
            IgnoredSampleCount++;
            return;
        }

        if (gapMs >= Settings.FlingHoldCutoffMs)
        {
            // The finger paused: earlier motion says nothing about the current speed.
            _smoothVelocityX = 0;
            _smoothVelocityY = 0;
            _velocityX = 0;
            _velocityY = 0;
            _lastTimestampMs = timestampMs;
            return;
        }

        float dt = gapMs / 1000f;
        float max = Settings.MaxVelocity;
        float rawVx = Math.Clamp(dx / dt, -max, max);
        float rawVy = Math.Clamp(dy / dt, -max, max);

        float s = Settings.VelocitySmoothing;
        _smoothVelocityX = _smoothVelocityX * s + rawVx * (1f - s);
        _smoothVelocityY = _smoothVelocityY * s + rawVy * (1f - s);

        _velocityX = _smoothVelocityX;
        _velocityY = _smoothVelocityY;
        _lastTimestampMs = timestampMs;
    }

    /// <summary>
    /// Start the inertial animation with the tracked velocity, without checking how long the pointer rested before
    /// release. Prefer <see cref="StartFling(long)"/> when the release timestamp is known.
    /// </summary>
    public void StartFling() => StartFling(0);

    /// <summary>
    /// Start the inertial animation with the tracked velocity.
    /// Call this when the pointer is released after a pan.
    /// If the pointer rested for <see cref="ScrollSettings.FlingHoldCutoffMs"/> or longer between the last move
    /// sample and the release, the velocity is dropped and no fling starts (hold-then-release).
    /// </summary>
    /// <param name="releaseTimestampMs">Timestamp of the release event, on the same clock as
    /// <see cref="TrackVelocity"/>. Zero or negative skips the hold check.</param>
    public void StartFling(long releaseTimestampMs)
    {
        if (!Settings.EnableInertia)
        {
            _isActive = false;
            return;
        }

        if (releaseTimestampMs > 0 && _hasSample
            && releaseTimestampMs - _lastTimestampMs >= Settings.FlingHoldCutoffMs)
        {
            _velocityX = 0;
            _velocityY = 0;
            _smoothVelocityX = 0;
            _smoothVelocityY = 0;
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
        _hasSample = false;
        _lastTimestampMs = 0;
        IgnoredSampleCount = 0;
    }

    /// <summary>
    /// Report how far the host actually scrolled for the delta returned by <see cref="Update"/>. An axis that was
    /// asked to move but did not (the content edge clamped it) loses its velocity, and the fling ends once no axis
    /// has any left, so a fling into an edge does not keep the frame loop alive.
    /// </summary>
    /// <param name="requestedDx">Horizontal delta returned by <see cref="Update"/>.</param>
    /// <param name="requestedDy">Vertical delta returned by <see cref="Update"/>.</param>
    /// <param name="appliedDx">Horizontal distance the offset really changed by.</param>
    /// <param name="appliedDy">Vertical distance the offset really changed by.</param>
    public void ReportApplied(float requestedDx, float requestedDy, float appliedDx, float appliedDy)
    {
        if (!_isActive) return;

        if (!IsZero(requestedDx) && IsZero(appliedDx)) { _velocityX = 0; _smoothVelocityX = 0; }
        if (!IsZero(requestedDy) && IsZero(appliedDy)) { _velocityY = 0; _smoothVelocityY = 0; }

        if (IsZero(_velocityX) && IsZero(_velocityY))
            _isActive = false;
    }

    /// <summary>Exact zero test written as a magnitude comparison so no floating point equality is used.</summary>
    internal static bool IsZero(float value) => MathF.Abs(value) < float.Epsilon;

    /// <summary>
    /// Call each frame to get the scroll delta.
    /// Returns (dx, dy) to apply to scroll offset.
    /// </summary>
    /// <param name="frameIntervalMs">Real time since the previous step, in milliseconds. Travel and friction both
    /// scale with it, so a fling covers the same distance whatever the frame rate (60 Hz, 120 Hz, or uneven
    /// frames). Non-finite or non-positive values produce no motion; values above 100 ms are clamped.</param>
    /// <remarks>
    /// The velocity decays continuously by <see cref="ScrollSettings.Friction"/> per 16 ms. A step travels the
    /// distance covered while it decays over the interval, <c>v · 16 ms · (1 − f^(t/16)) / (1 − f)</c>: a 16 ms step
    /// travels <c>v · 16 ms</c> as before, and the steps of any split of the same time add up to the same distance.
    /// </remarks>
    public (float dx, float dy) Update(float frameIntervalMs = 16f)
    {
        if (!_isActive) return (0, 0);
        if (float.IsNaN(frameIntervalMs) || frameIntervalMs <= 0f || !float.IsFinite(frameIntervalMs)) return (0, 0);
        if (frameIntervalMs > MaxFrameIntervalMs) frameIntervalMs = MaxFrameIntervalMs;

        float friction = Settings.Friction;
        float factor = frameIntervalMs / 16f; // Normalize to 60fps baseline
        float decay = MathF.Pow(friction, factor);

        // Time-weighted travel of the decaying velocity over this step (see remarks). Without friction (1) the
        // velocity is constant and the step travels its own length.
        float travelMs = MathF.Abs(1f - friction) < 1e-6f
            ? frameIntervalMs
            : 16f * (1f - decay) / (1f - friction);
        if (!float.IsFinite(travelMs)) travelMs = frameIntervalMs;

        float dx = _velocityX * (travelMs / 1000f);
        float dy = _velocityY * (travelMs / 1000f);

        _velocityX *= decay;
        _velocityY *= decay;

        if (Math.Abs(_velocityX) < Settings.MinVelocity &&
            Math.Abs(_velocityY) < Settings.MinVelocity)
        {
            _isActive = false;
            return (0, 0);
        }

        return (dx, dy);
    }
}

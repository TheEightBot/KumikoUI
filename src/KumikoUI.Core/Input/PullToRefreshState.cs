namespace KumikoUI.Core.Input;

/// <summary>
/// Where the pull-to-refresh indicator is and what it shows (#330): how far it is pulled down, whether a finger holds
/// it, whether the app is refreshing, the transition it plays after a release or a change of
/// <see cref="KumikoInputController.IsRefreshing"/>, and the spinner's clock. The controller drives it; the renderer
/// reads it (<see cref="KumikoRenderer.InputController"/>); the host's frame loop advances it through
/// <see cref="KumikoInputController.UpdateAnimations"/>. Nothing here allocates once constructed.
/// </summary>
internal sealed class PullToRefreshState
{
    /// <summary>The default pull, in pixels of indicator travel, that refreshes on release.</summary>
    public const float DefaultThreshold = 64f;

    /// <summary>How far the indicator moves per pixel the finger moves: the pull resists.</summary>
    public const float Resistance = 0.5f;

    /// <summary>The farthest the indicator follows the finger, as a multiple of the threshold.</summary>
    public const float MaxPullFactor = 2f;

    /// <summary>A pull released short of the threshold returns out of sight in this time, in milliseconds.</summary>
    public const float CancelMs = 200f;

    /// <summary>A pull released past the threshold moves to the hold position in this time, in milliseconds.</summary>
    public const float HoldMs = 150f;

    /// <summary>
    /// The indicator slides into the hold position in this time when <see cref="KumikoInputController.IsRefreshing"/> is
    /// set without a gesture, in milliseconds.
    /// </summary>
    public const float ShowMs = 200f;

    /// <summary>The indicator slides out of sight in this time once the refresh ended, in milliseconds.</summary>
    public const float HideMs = 250f;

    /// <summary>One turn of the spinner while refreshing, in milliseconds.</summary>
    public const float SpinPeriodMs = 1000f;

    private float _from, _to, _elapsed, _duration = float.NaN;

    /// <summary>The pull that refreshes on release, in pixels of indicator travel; always positive.</summary>
    public float Threshold { get; set; } = DefaultThreshold;

    /// <summary>The farthest the indicator follows the finger, in pixels.</summary>
    public float MaxDistance => Threshold * MaxPullFactor;

    /// <summary>How far the indicator is pulled down from under the header, in pixels; 0 when it is out of sight.</summary>
    public float Distance { get; private set; }

    /// <summary>Whether a finger (or a pen) holds the indicator.</summary>
    public bool IsTracking { get; private set; }

    /// <summary>Whether the app is refreshing: the indicator holds at the threshold with a spinner.</summary>
    public bool IsRefreshing { get; set; }

    /// <summary>Whether a transition (settle, hold, show or hide) is running.</summary>
    public bool IsTransitioning => !float.IsNaN(_duration);

    /// <summary>Whether the indicator is on screen.</summary>
    public bool IsVisible => Distance > 0.01f;

    /// <summary>How far the pull is toward the threshold, from 0 to 1.</summary>
    public float Progress => Math.Clamp(Distance / Threshold, 0f, 1f);

    /// <summary>The spinner's clock, in milliseconds within one turn (<see cref="SpinPeriodMs"/>).</summary>
    public float SpinMs { get; private set; }

    /// <summary>
    /// Whether the frame loop has something to advance: a transition, or the spinner turning while it shows and
    /// <paramref name="spin"/> (animations are allowed) is true.
    /// </summary>
    public bool IsActive(bool spin) => IsTransitioning || (spin && IsRefreshing && IsVisible);

    /// <summary>A finger pulls by <paramref name="fingerTravel"/> pixels: the indicator follows, with resistance, up to <see cref="MaxDistance"/>.</summary>
    public void Track(float fingerTravel)
    {
        _duration = float.NaN;
        IsTracking = true;
        Distance = Math.Clamp(fingerTravel * Resistance, 0f, MaxDistance);
    }

    /// <summary>The finger travel that puts the indicator where it is now, for a pull that takes over a running transition.</summary>
    public float FingerTravel => Distance / Resistance;

    /// <summary>The finger let go: <see cref="IsTracking"/> turns off and the indicator stays where it is.</summary>
    public void Release() => IsTracking = false;

    /// <summary>
    /// Moves the indicator to <paramref name="target"/> over <paramref name="durationMs"/>, or at once when
    /// <paramref name="animate"/> is false.
    /// </summary>
    public void MoveTo(float target, float durationMs, bool animate)
    {
        IsTracking = false;
        if (!animate || durationMs <= 0 || !InertialScroller.Differs(Distance, target))
        {
            Distance = target;
            _duration = float.NaN;
            return;
        }
        _from = Distance;
        _to = target;
        _elapsed = 0;
        _duration = durationMs;
    }

    /// <summary>Ends the running transition where it was going.</summary>
    public void Finish()
    {
        if (!IsTransitioning) return;
        Distance = _to;
        _duration = float.NaN;
    }

    /// <summary>Hides the indicator at once and forgets the gesture (the grid was disabled).</summary>
    public void Reset()
    {
        _duration = float.NaN;
        IsTracking = false;
        Distance = 0;
    }

    /// <summary>Restarts the spinner's clock (a refresh started).</summary>
    public void RestartSpinner() => SpinMs = 0;

    /// <summary>
    /// Advances the transition and, with <paramref name="spin"/>, the spinner by <paramref name="frameIntervalMs"/>.
    /// </summary>
    /// <returns>Whether anything drawn changed.</returns>
    public bool Step(float frameIntervalMs, bool spin)
    {
        float ms = float.IsFinite(frameIntervalMs) && frameIntervalMs > 0 ? frameIntervalMs : 0f;
        bool changed = false;
        if (IsTransitioning)
        {
            _elapsed += ms;
            float t = Math.Clamp(_elapsed / _duration, 0f, 1f);
            float inverse = 1f - t;
            Distance = _from + (_to - _from) * (1f - inverse * inverse * inverse); // ease-out cubic
            if (t >= 1f) _duration = float.NaN;
            changed = true;
        }
        if (spin && IsRefreshing && IsVisible && ms > 0)
        {
            SpinMs = (SpinMs + ms) % SpinPeriodMs;
            changed = true;
        }
        return changed;
    }
}

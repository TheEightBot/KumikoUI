namespace KumikoUI.Core.Input;

/// <summary>
/// Configurable scroll/inertia settings for the grid.
/// </summary>
public class ScrollSettings
{
    /// <summary>Deceleration factor per frame (0..1). Lower = more friction, stops faster. Default 0.92.</summary>
    public float Friction { get; set; } = 0.92f;

    /// <summary>Minimum velocity (px/s) below which scrolling stops. Default 0.5.</summary>
    public float MinVelocity { get; set; } = 0.5f;

    /// <summary>Maximum velocity (px/s) cap to prevent insane flinging. Default 8000.</summary>
    public float MaxVelocity { get; set; } = 8000f;

    /// <summary>Minimum fling velocity threshold — must exceed MinVelocity × this to start a fling. Default 10.</summary>
    public float FlingThresholdMultiplier { get; set; } = 10f;

    /// <summary>Smoothing factor for velocity tracking (0..1). Higher = more smoothing, less responsive. Default 0.3.</summary>
    public float VelocitySmoothing { get; set; } = 0.3f;

    /// <summary>Mouse wheel / trackpad scroll multiplier in row-heights. Default 3.</summary>
    public float WheelScrollMultiplier { get; set; } = 3f;

    /// <summary>Whether inertial scrolling (fling) is enabled. Default true.</summary>
    public bool EnableInertia { get; set; } = true;
}

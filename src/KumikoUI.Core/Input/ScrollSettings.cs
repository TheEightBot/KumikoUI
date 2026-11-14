namespace KumikoUI.Core.Input;

/// <summary>
/// Configurable scroll/inertia settings for the grid.
/// </summary>
public sealed class ScrollSettings
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

    /// <summary>
    /// Pause, in milliseconds, between the last move sample and the next sample or the release after which the
    /// finger counts as stopped: its velocity is dropped and releasing does not fling. Default 100.
    /// </summary>
    public long FlingHoldCutoffMs { get; set; } = 100;

    /// <summary>Whether inertial scrolling (fling) is enabled. Default true.</summary>
    public bool EnableInertia { get; set; } = true;

    /// <summary>
    /// Height in pixels of the bands at the top and bottom of the scrolling rows in which a row drag scrolls the grid
    /// (edge autoscroll). The bands shrink to a third of the scrolling area when it is short. Default 40.
    /// </summary>
    public float AutoScrollEdgeSize { get; set; } = 40f;

    /// <summary>
    /// Edge autoscroll speed in pixels per second with the pointer at the outer edge of an autoscroll band or beyond
    /// it. The speed grows linearly with the depth into the band, from 0 at its inner edge. Default 1200.
    /// </summary>
    public float AutoScrollMaxSpeed { get; set; } = 1200f;

    /// <summary>
    /// New settings tuned for a platform's feel (#101): the friction, speed cap and stop and fling thresholds of
    /// <paramref name="preset"/>; every other setting keeps its default. Each call returns a new instance to change
    /// further.
    /// </summary>
    /// <param name="preset">The platform to feel like.</param>
    /// <returns>The settings.</returns>
    public static ScrollSettings FromPreset(KumikoMomentumPreset preset) => preset switch
    {
        KumikoMomentumPreset.Ios => new ScrollSettings
        {
            Friction = 0.968f, MaxVelocity = 8000f, MinVelocity = 10f, FlingThresholdMultiplier = 5f,
        },
        KumikoMomentumPreset.Android => new ScrollSettings
        {
            Friction = 0.955f, MaxVelocity = 8000f, MinVelocity = 10f, FlingThresholdMultiplier = 5f,
        },
        KumikoMomentumPreset.Desktop => new ScrollSettings
        {
            Friction = 0.92f, MaxVelocity = 6000f, MinVelocity = 5f, FlingThresholdMultiplier = 20f,
            WheelScrollMultiplier = 3f,
        },
        _ => new ScrollSettings(),
    };
}

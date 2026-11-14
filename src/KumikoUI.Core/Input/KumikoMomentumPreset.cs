namespace KumikoUI.Core.Input;

/// <summary>
/// A tuning of the inertial scroller for a platform (<see cref="ScrollSettings.FromPreset"/>): how far a fling coasts,
/// how fast it may start and how slow it may get before it stops. The MAUI and Uno grids pick the preset of the
/// platform they run on unless a <c>ScrollSettings</c> is assigned.
/// </summary>
public enum KumikoMomentumPreset
{
    /// <summary>The <see cref="ScrollSettings"/> defaults: a medium coast that stops at 0.5 px/s.</summary>
    Default,

    /// <summary>
    /// iOS: a long, gently decaying coast like <c>UIScrollView</c>'s normal deceleration (friction 0.968 per 16 ms),
    /// up to 8000 px/s, ending below 10 px/s.
    /// </summary>
    IOS,

    /// <summary>
    /// Android: a shorter coast that settles sooner, like <c>OverScroller</c> (friction 0.955 per 16 ms), up to
    /// 8000 px/s, ending below 10 px/s.
    /// </summary>
    Android,

    /// <summary>
    /// Desktop (Windows, macOS, Linux, the browser): touch screens and pens coast briefly (friction 0.92 per 16 ms) up to
    /// 6000 px/s and need a deliberate flick (100 px/s) to coast at all; the wheel scrolls three rows a notch, and
    /// trackpads bring their own momentum.
    /// </summary>
    Desktop,
}

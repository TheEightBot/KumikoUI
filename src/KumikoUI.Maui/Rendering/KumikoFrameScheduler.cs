namespace KumikoUI.Maui.Rendering;

/// <summary>What the host must do after <see cref="KumikoFrameScheduler.RequestRedraw"/>.</summary>
internal enum KumikoRedrawAction
{
    /// <summary>Nothing: the request joined one already pending, or the grid has no handler.</summary>
    None,

    /// <summary>Call the surface's <c>Invalidate</c> now; the surface itself coalesces it into the next frame.</summary>
    InvalidateNow,

    /// <summary>Subscribe the frame callback; the redraw happens on the next frame.</summary>
    StartFrameLoop,
}

/// <summary>
/// The state machine behind the MAUI host's frame loop (#71, the same as the Uno host's, #65): decides when the
/// platform's per-frame callback (<see cref="KumikoFrameClock"/>) must run, coalesces redraw requests into one paint
/// per frame, and measures the time between frames for frame-synchronous animation (inertial scrolling). Holds no
/// platform types, so it is unit-tested directly.
/// </summary>
/// <remarks>
/// <para>
/// The loop runs only while there is work: an animation in progress, or — on a surface whose invalidation paints
/// synchronously (<see cref="DeferInvalidation"/>) — a redraw waiting for the next frame. It stops on the first idle
/// frame. A running frame callback (<c>CADisplayLink</c>, <c>Choreographer</c>, <c>CompositionTarget.Rendering</c>)
/// keeps the display pipeline producing frames, so an idle grid must not keep it running.
/// </para>
/// <para>
/// A host drives it as: <see cref="Attach"/>/<see cref="Detach"/> with its handler; <see cref="RequestRedraw"/>,
/// <see cref="StartAnimation"/> and <see cref="StopAnimation"/> from events; and per frame
/// <see cref="BeginFrame"/>, then the animation step, then <see cref="TakePendingRedraw"/> and
/// <see cref="EndFrame"/>. Every member is allocation-free.
/// </para>
/// </remarks>
internal sealed class KumikoFrameScheduler
{
    /// <summary>The frame time assumed for the first frame of an animation, when there is no previous frame (60 Hz).</summary>
    public const float NominalFrameMs = 16f;

    /// <summary>The shortest step an animation advances by in one frame.</summary>
    public const float MinFrameMs = 1f;

    /// <summary>
    /// The longest step an animation advances by in one frame, so a stall (a long layout, a debugger break) does not
    /// make a fling jump.
    /// </summary>
    public const float MaxFrameMs = 50f;

    private bool _attached;
    private bool _running;
    private bool _redrawPending;
    private bool _animating;
    private TimeSpan _lastFrameTime;
    private bool _hasLastFrameTime;

    /// <summary>
    /// True when the surface's invalidation paints synchronously, so redraw requests wait for the frame loop and
    /// coalesce; false when the surface already coalesces them (they are passed straight through).
    /// </summary>
    public bool DeferInvalidation { get; set; }

    /// <summary>Whether the grid has a handler; the loop never runs while it has none.</summary>
    public bool IsAttached => _attached;

    /// <summary>Whether the host should be subscribed to the frame callback.</summary>
    public bool IsRunning => _running;

    /// <summary>Whether a deferred redraw waits for the next frame.</summary>
    public bool IsRedrawPending => _redrawPending;

    /// <summary>Whether an animation (inertial scrolling) needs a step every frame.</summary>
    public bool IsAnimating => _animating;

    /// <summary>
    /// The grid was connected to its handler. Returns true when the host must start the frame loop for work that was
    /// requested while it was detached.
    /// </summary>
    public bool Attach()
    {
        _attached = true;
        return TryStart();
    }

    /// <summary>
    /// The grid was disconnected from its handler: animations end and the loop stops (a pending redraw is kept for the next
    /// <see cref="Attach"/>). Returns true when the host must unsubscribe the frame callback.
    /// </summary>
    public bool Detach()
    {
        bool wasRunning = _running;
        _attached = false;
        _running = false;
        _animating = false;
        _hasLastFrameTime = false;
        return wasRunning;
    }

    /// <summary>Asks for the whole grid to be redrawn. Any number of requests before a frame produce one paint.</summary>
    public KumikoRedrawAction RequestRedraw()
    {
        if (!DeferInvalidation)
            return KumikoRedrawAction.InvalidateNow;

        _redrawPending = true;
        return TryStart() ? KumikoRedrawAction.StartFrameLoop : KumikoRedrawAction.None;
    }

    /// <summary>
    /// An animation started (a fling). Returns true when the host must start the frame loop. Restarting a running
    /// animation keeps its frame clock.
    /// </summary>
    public bool StartAnimation()
    {
        if (!_animating)
            _hasLastFrameTime = false;
        _animating = true;
        return TryStart();
    }

    /// <summary>The animation finished or was interrupted; the loop stops on its next idle frame.</summary>
    public void StopAnimation() => _animating = false;

    /// <summary>
    /// Starts a frame at <paramref name="renderingTime"/> (a monotonic frame clock) and returns how far an animation
    /// advances in it, in milliseconds: the time since the previous frame, clamped to
    /// [<see cref="MinFrameMs"/>, <see cref="MaxFrameMs"/>], or <see cref="NominalFrameMs"/> on an animation's first frame.
    /// </summary>
    public float BeginFrame(TimeSpan renderingTime)
    {
        float delta = NominalFrameMs;
        if (_hasLastFrameTime)
            delta = Math.Clamp((float)(renderingTime - _lastFrameTime).TotalMilliseconds, MinFrameMs, MaxFrameMs);
        _lastFrameTime = renderingTime;
        _hasLastFrameTime = true;
        return delta;
    }

    /// <summary>Returns true, once, when a deferred redraw is waiting: the host paints the surface now.</summary>
    public bool TakePendingRedraw()
    {
        if (!_redrawPending) return false;
        _redrawPending = false;
        return true;
    }

    /// <summary>
    /// Ends the frame. Returns true when there is no more work, so the loop stops and the host must unsubscribe the
    /// frame callback.
    /// </summary>
    public bool EndFrame()
    {
        if (_running && HasWork) return false;
        _running = false;
        _hasLastFrameTime = false;
        return true;
    }

    private bool HasWork => _animating || (_redrawPending && DeferInvalidation);

    private bool TryStart()
    {
        if (_running || !_attached || !HasWork) return false;
        _running = true;
        return true;
    }
}

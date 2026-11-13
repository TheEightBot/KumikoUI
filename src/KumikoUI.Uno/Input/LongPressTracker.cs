namespace KumikoUI.Uno.Input;

/// <summary>A one-shot timer the <see cref="LongPressTracker"/> arms on press, so tests can drive time by hand.</summary>
internal interface ILongPressTimer
{
    /// <summary>Starts (or restarts) the timer; <paramref name="tick"/> runs once after <paramref name="interval"/>.</summary>
    /// <param name="interval">How long to wait.</param>
    /// <param name="tick">Called once, on the UI thread, when the interval elapses.</param>
    void Start(TimeSpan interval, Action tick);

    /// <summary>Stops the timer; a pending tick does not run.</summary>
    void Stop();
}

/// <summary>
/// The long-press state machine of the Uno host, the same rule as the MAUI host: a press held for
/// <see cref="ThresholdMs"/> without moving more than <see cref="MoveTolerance"/> fires once, and the release that ends
/// that press is swallowed so it neither taps nor toggles a second time. WinUI's <c>Holding</c> event is kept as a
/// fallback through <see cref="NotifyHolding"/>; whichever arrives first fires, the other does not.
/// </summary>
internal sealed class LongPressTracker
{
    /// <summary>How long a press is held before it is a long press.</summary>
    internal const long ThresholdMs = 500;

    /// <summary>How far, in pixels, a press may move and still be a long press.</summary>
    internal const float MoveTolerance = 15f;

    private readonly ILongPressTimer _timer;
    private readonly Action<uint, float, float> _fired;
    private bool _armed;
    private bool _firedForPress;
    private uint _pointerId;
    private float _x, _y;

    /// <summary>Creates a tracker.</summary>
    /// <param name="timer">The timer armed on press.</param>
    /// <param name="fired">Called with the pointer id and press position when a long press fires.</param>
    internal LongPressTracker(ILongPressTimer timer, Action<uint, float, float> fired)
    {
        _timer = timer;
        _fired = fired;
    }

    /// <summary>True from the moment the long press fires until the press ends.</summary>
    internal bool HasFired => _firedForPress;

    /// <summary>Arms the timer for a new press; a long press from an earlier press is forgotten.</summary>
    internal void Press(uint pointerId, float x, float y)
    {
        _firedForPress = false;
        _pointerId = pointerId;
        _x = x;
        _y = y;
        _armed = true;
        _timer.Start(TimeSpan.FromMilliseconds(ThresholdMs), OnTick);
    }

    /// <summary>Cancels the pending long press when the pointer moves beyond <see cref="MoveTolerance"/> of the press.</summary>
    internal void Move(uint pointerId, float x, float y)
    {
        if (!_armed || pointerId != _pointerId) return;
        float dx = x - _x, dy = y - _y;
        if (MathF.Sqrt(dx * dx + dy * dy) > MoveTolerance) Cancel();
    }

    /// <summary>Cancels the pending long press. A long press that already fired is still remembered for its release.</summary>
    internal void Cancel()
    {
        _armed = false;
        _timer.Stop();
    }

    /// <summary>Forgets everything about the press in progress (a canceled or lost press has no release to swallow).</summary>
    internal void Reset()
    {
        Cancel();
        _firedForPress = false;
    }

    /// <summary>
    /// Ends the press. True when a long press fired for it, so its release must not be delivered as a release
    /// (it would tap, or toggle a Boolean cell, a second time).
    /// </summary>
    internal bool ConsumeRelease()
    {
        Cancel();
        bool fired = _firedForPress;
        _firedForPress = false;
        return fired;
    }

    /// <summary>
    /// The WinUI <c>Holding</c> fallback: fires the long press unless the timer already did, or the press moved too far
    /// or ended. True when it fired.
    /// </summary>
    internal bool NotifyHolding(uint pointerId, float x, float y)
    {
        if (_firedForPress || !_armed || pointerId != _pointerId) return false;
        Fire(x, y);
        return true;
    }

    private void OnTick()
    {
        if (!_armed || _firedForPress) return;
        Fire(_x, _y);
    }

    private void Fire(float x, float y)
    {
        Cancel();
        _firedForPress = true;
        _fired(_pointerId, x, y);
    }
}

/// <summary>An <see cref="ILongPressTimer"/> backed by a WinUI <see cref="Microsoft.UI.Xaml.DispatcherTimer"/>.</summary>
internal sealed class DispatcherLongPressTimer : ILongPressTimer
{
    private readonly Microsoft.UI.Xaml.DispatcherTimer _timer = new();
    private Action? _tick;

    /// <summary>Creates the timer; it is idle until <see cref="Start"/>.</summary>
    internal DispatcherLongPressTimer() => _timer.Tick += OnTick;

    /// <inheritdoc />
    public void Start(TimeSpan interval, Action tick)
    {
        _timer.Stop();
        _tick = tick;
        _timer.Interval = interval;
        _timer.Start();
    }

    /// <inheritdoc />
    public void Stop()
    {
        _timer.Stop();
        _tick = null;
    }

    private void OnTick(object? sender, object e)
    {
        var tick = _tick;
        _timer.Stop();
        _tick = null;
        tick?.Invoke();
    }
}

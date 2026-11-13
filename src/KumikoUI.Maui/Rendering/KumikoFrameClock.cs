#if !(IOS || MACCATALYST || ANDROID)
using System.Diagnostics;
#endif
#if IOS || MACCATALYST
using CoreAnimation;
using Foundation;
#endif

namespace KumikoUI.Maui.Rendering;

/// <summary>
/// The platform's per-frame callback, started and stopped by the grid's frame loop (#71): <c>CADisplayLink</c> on
/// iOS and Mac Catalyst, <c>Choreographer</c> on Android, <c>CompositionTarget.Rendering</c> on Windows, and a 16 ms
/// dispatcher timer elsewhere. Each callback reports the frame's time on a monotonic clock, so the time between two
/// frames is the display's real frame interval (8.3 ms at 120 Hz, 16.7 ms at 60 Hz).
/// </summary>
/// <remarks>
/// Every callback arrives on the UI thread. While it runs, the callback is referenced by the platform (the run loop,
/// the choreographer, the compositor), so the owner must <see cref="Stop"/> it when the grid is disconnected; the
/// grid does that from its handler teardown. Starting and stopping allocate nothing after the first start.
/// </remarks>
internal sealed class KumikoFrameClock : IDisposable
{
#if !(IOS || MACCATALYST || ANDROID)
    private static readonly long s_clockStart = Stopwatch.GetTimestamp();
#endif

    private readonly Action<TimeSpan> _onFrame;
    private bool _running;

#if IOS || MACCATALYST
    private CADisplayLink? _displayLink;
#elif ANDROID
    private ChoreographerCallback? _choreographerCallback;
#elif WINDOWS
    private EventHandler<object>? _renderingHandler;
#else
    private readonly IDispatcher? _dispatcher;
    private IDispatcherTimer? _timer;
#endif

    /// <summary>Creates a stopped clock.</summary>
    /// <param name="dispatcher">The UI thread's dispatcher, for the timer used where the platform has no frame callback.</param>
    /// <param name="onFrame">Called once per frame with the frame's time, on the UI thread.</param>
    public KumikoFrameClock(IDispatcher? dispatcher, Action<TimeSpan> onFrame)
    {
        _onFrame = onFrame;
#if !(IOS || MACCATALYST || ANDROID || WINDOWS)
        _dispatcher = dispatcher;
#endif
    }

    /// <summary>Whether frames are being delivered.</summary>
    public bool IsRunning => _running;

    /// <summary>Starts delivering frames. Idempotent.</summary>
    public void Start()
    {
        if (_running) return;
        _running = true;
#if IOS || MACCATALYST
        if (_displayLink is null)
        {
            _displayLink = CADisplayLink.Create(OnDisplayLink);
            // Up to the display's refresh rate (120 Hz on ProMotion screens), not the 60 Hz default.
            _displayLink.PreferredFrameRateRange = CAFrameRateRange.Create(60, 120, 120);
            _displayLink.AddToRunLoop(NSRunLoop.Main, NSRunLoopMode.Common);
        }
        _displayLink.Paused = false;
#elif ANDROID
        _choreographerCallback ??= new ChoreographerCallback(this);
        Android.Views.Choreographer.Instance?.PostFrameCallback(_choreographerCallback);
#elif WINDOWS
        _renderingHandler ??= OnRendering;
        Microsoft.UI.Xaml.Media.CompositionTarget.Rendering += _renderingHandler;
#else
        if (_timer is null)
        {
            if (_dispatcher is null)
            {
                _running = false;
                return;
            }
            _timer = _dispatcher.CreateTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(16);
            _timer.Tick += OnTimerTick;
        }
        _timer.Start();
#endif
    }

    /// <summary>Stops delivering frames. Idempotent; a callback already queued for this frame is ignored.</summary>
    public void Stop()
    {
        if (!_running) return;
        _running = false;
#if IOS || MACCATALYST
        if (_displayLink is not null) _displayLink.Paused = true;
#elif ANDROID
        if (_choreographerCallback is not null)
            Android.Views.Choreographer.Instance?.RemoveFrameCallback(_choreographerCallback);
#elif WINDOWS
        Microsoft.UI.Xaml.Media.CompositionTarget.Rendering -= _renderingHandler;
#else
        _timer?.Stop();
#endif
    }

    /// <summary>Stops the clock and releases the platform callback, so nothing platform-side references the grid.</summary>
    public void Dispose()
    {
        Stop();
#if IOS || MACCATALYST
        // Removes the link from the run loop, which held it (and through it this clock).
        _displayLink?.Invalidate();
        _displayLink?.Dispose();
        _displayLink = null;
#elif ANDROID
        _choreographerCallback?.Dispose();
        _choreographerCallback = null;
#elif WINDOWS
        _renderingHandler = null;
#else
        if (_timer is not null) _timer.Tick -= OnTimerTick;
        _timer = null;
#endif
    }

    private void Deliver(TimeSpan frameTime)
    {
        if (_running) _onFrame(frameTime);
    }

#if IOS || MACCATALYST
    // Timestamp: when the last frame was displayed, in seconds on the host's monotonic clock.
    private void OnDisplayLink() => Deliver(TimeSpan.FromSeconds(_displayLink!.Timestamp));
#elif ANDROID
    private void OnChoreographerFrame(long frameTimeNanos)
    {
        if (!_running) return;
        // A frame callback runs once; post the next one before handing over, so the step can stop it.
        Android.Views.Choreographer.Instance?.PostFrameCallback(_choreographerCallback!);
        Deliver(TimeSpan.FromTicks(frameTimeNanos / 100));
    }

    /// <summary>The Java-side frame callback; it only forwards to the clock.</summary>
    private sealed class ChoreographerCallback(KumikoFrameClock owner)
        : Java.Lang.Object, Android.Views.Choreographer.IFrameCallback
    {
        public void DoFrame(long frameTimeNanos) => owner.OnChoreographerFrame(frameTimeNanos);
    }
#elif WINDOWS
    private void OnRendering(object? sender, object e) =>
        Deliver(e is Microsoft.UI.Xaml.Media.RenderingEventArgs args
            ? args.RenderingTime
            : Stopwatch.GetElapsedTime(s_clockStart));
#else
    private void OnTimerTick(object? sender, EventArgs e) => Deliver(Stopwatch.GetElapsedTime(s_clockStart));
#endif
}

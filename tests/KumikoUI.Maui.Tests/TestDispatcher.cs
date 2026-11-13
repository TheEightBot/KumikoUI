using System.Runtime.CompilerServices;
using Microsoft.Maui.Dispatching;

namespace KumikoUI.Maui.Tests;

/// <summary>
/// Installs a synchronous <see cref="IDispatcher"/> so MAUI views can be used without a running application:
/// <c>BindableObject.Dispatcher</c> throws when no dispatcher is available, and the grid reads it on every
/// invalidation. Work dispatched through it runs inline; nothing is ever marshaled to another thread. Its timers
/// (<see cref="ManualTimer"/>) only tick when a test fires them.
/// </summary>
internal static class TestDispatcher
{
    [ModuleInitializer]
    internal static void Install() => DispatcherProvider.SetCurrent(new InlineDispatcherProvider());

    private sealed class InlineDispatcherProvider : IDispatcherProvider
    {
        private readonly InlineDispatcher _dispatcher = new();

        public IDispatcher? GetForCurrentThread() => _dispatcher;
    }

    private sealed class InlineDispatcher : IDispatcher
    {
        public bool IsDispatchRequired => false;

        public bool Dispatch(Action action)
        {
            action();
            return true;
        }

        public bool DispatchDelayed(TimeSpan delay, Action action)
        {
            action();
            return true;
        }

        public IDispatcherTimer CreateTimer() => new ManualTimer();
    }

    /// <summary>
    /// A timer that never fires by itself: a test raises <see cref="Fire"/> to run its tick, so timer-driven behavior
    /// (the caret blink) is deterministic.
    /// </summary>
    internal sealed class ManualTimer : IDispatcherTimer
    {
        public TimeSpan Interval { get; set; }

        public bool IsRepeating { get; set; } = true;

        public bool IsRunning { get; private set; }

        public event EventHandler? Tick;

        public void Start() => IsRunning = true;

        public void Stop() => IsRunning = false;

        /// <summary>Runs one tick as the platform would: a one-shot timer stops first.</summary>
        public void Fire()
        {
            if (!IsRepeating) IsRunning = false;
            Tick?.Invoke(this, EventArgs.Empty);
        }
    }
}

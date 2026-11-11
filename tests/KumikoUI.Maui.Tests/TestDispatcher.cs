using System.Runtime.CompilerServices;
using Microsoft.Maui.Dispatching;

namespace KumikoUI.Maui.Tests;

/// <summary>
/// Installs a synchronous <see cref="IDispatcher"/> so MAUI views can be used without a running application:
/// <c>BindableObject.Dispatcher</c> throws when no dispatcher is available, and the grid reads it on every
/// invalidation. Work dispatched through it runs inline; nothing is ever marshaled to another thread.
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

        public IDispatcherTimer CreateTimer() => throw new NotSupportedException("Timers are not needed by the host tests.");
    }
}

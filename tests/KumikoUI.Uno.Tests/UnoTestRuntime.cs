using System.Reflection;
using System.Runtime.CompilerServices;

namespace KumikoUI.Uno.Tests;

/// <summary>
/// Lets tests create Uno <c>DependencyObject</c>s without a running Uno app. Creating one asks for the current
/// <c>DispatcherQueue</c>, which asks the <c>NativeDispatcher</c> for thread access; this makes it report
/// thread access on every thread.
/// </summary>
internal static class UnoTestRuntime
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        var type = Type.GetType("Uno.UI.Dispatching.NativeDispatcher, Uno.UI.Dispatching");
        if (type is null) return;

        const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        try
        {
            RuntimeHelpers.RunClassConstructor(type.TypeHandle);

            type.GetField("HasThreadAccessOverride", Static)?.SetValue(null, new Func<bool>(() => true));
        }
        catch (Exception ex) when (ex is MissingMethodException or TargetInvocationException or FieldAccessException)
        {
            // A different Uno version: DependencyObject tests will fail loudly instead.
        }
    }
}

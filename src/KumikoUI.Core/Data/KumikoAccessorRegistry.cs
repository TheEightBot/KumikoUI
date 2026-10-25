using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace KumikoUI.Core.Data;

/// <summary>
/// Resolves how the grid reads and writes item properties named by a path such as
/// <c>"Name"</c> or <c>"Address.City"</c>.
/// </summary>
/// <remarks>
/// Each path segment is resolved, in order, from:
/// <list type="number">
/// <item>providers registered for the item's type or one of its base types (<see cref="Register"/>),
/// including the ones the <c>[KumikoGridItem]</c> source generator registers;</item>
/// <item>providers added for every type (<see cref="AddProvider"/>);</item>
/// <item>reflection, when <see cref="IsReflectionPropertyAccessSupported"/> is true.</item>
/// </list>
/// A column's <see cref="Models.KumikoColumn.ValueAccessor"/> and <see cref="Models.KumikoColumn.ValueSetter"/>
/// take precedence over all of these.
/// </remarks>
public static class KumikoAccessorRegistry
{
    /// <summary>
    /// The <see cref="AppContext"/> switch (and <c>RuntimeHostConfigurationOption</c>) that turns the
    /// reflection fallback off. The KumikoUI.Core package sets it to false for Native AOT and
    /// <c>TrimMode=full</c> publishes; override it with the <c>KumikoReflectionPropertyAccess</c>
    /// MSBuild property.
    /// </summary>
    public const string ReflectionSwitchName = "KumikoUI.Core.IsReflectionPropertyAccessSupported";

    private static readonly object s_gate = new();
    private static readonly Dictionary<Type, IKumikoAccessorProvider> s_typeProviders = new();
    private static readonly List<IKumikoAccessorProvider> s_globalProviders = new();
    private static readonly ConcurrentDictionary<(Type Type, string Name), KumikoPropertyAccessor?> s_segments = new();
    private static readonly ConcurrentDictionary<(Type Type, string Path), KumikoValuePath> s_paths = new();
    private static readonly ConcurrentDictionary<(Type Type, string Name), bool> s_reportedMissing = new();
    private static int s_version;

    /// <summary>
    /// Whether properties without a registered accessor are read and written through reflection.
    /// True unless the <see cref="ReflectionSwitchName"/> switch is false. When it is false, such a
    /// property reads as null, cannot be written, and a warning is traced once per type and property.
    /// </summary>
    [FeatureSwitchDefinition(ReflectionSwitchName)]
    [FeatureGuard(typeof(RequiresUnreferencedCodeAttribute))]
    [UnconditionalSuppressMessage("Trimming", "IL4000",
        Justification = "The KumikoUI.Core build targets set this switch to false (RuntimeHostConfigurationOption, " +
                        "Trim=true) for Native AOT and TrimMode=full, so the trimmer removes the reflection path; " +
                        "partial trimming keeps app types' metadata, which the reflection path reads.")]
    public static bool IsReflectionPropertyAccessSupported =>
        AppContext.TryGetSwitch(ReflectionSwitchName, out bool enabled) ? enabled : true;

    /// <summary>
    /// Registers the accessor provider for <paramref name="itemType"/> and the types derived from it.
    /// The last registration for a type wins.
    /// </summary>
    public static void Register(Type itemType, IKumikoAccessorProvider provider)
    {
        ArgumentNullException.ThrowIfNull(itemType);
        ArgumentNullException.ThrowIfNull(provider);
        lock (s_gate)
        {
            s_typeProviders[itemType] = provider;
            ClearCaches();
        }
    }

    /// <summary>
    /// Adds a provider consulted for every item type after the type-specific ones, for example one
    /// that reads dictionary entries.
    /// </summary>
    public static void AddProvider(IKumikoAccessorProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        lock (s_gate)
        {
            s_globalProviders.Add(provider);
            ClearCaches();
        }
    }

    /// <summary>Changes whenever registrations change, invalidating paths cached outside the registry.</summary>
    internal static int Version => Volatile.Read(ref s_version);

    /// <summary>Returns the resolved path for <paramref name="itemType"/>, cached.</summary>
    internal static KumikoValuePath GetPath(Type itemType, string path) =>
        s_paths.GetOrAdd((itemType, path), static key => new KumikoValuePath(key.Type, key.Path));

    /// <summary>Returns the accessor for one path segment of <paramref name="type"/>, or null; cached.</summary>
    internal static KumikoPropertyAccessor? GetSegmentAccessor(Type type, string name) =>
        s_segments.GetOrAdd((type, name), static key => ResolveSegment(key.Type, key.Name));

    /// <summary>Forgets every resolved accessor (after a registration, or in tests).</summary>
    internal static void ClearCaches()
    {
        s_segments.Clear();
        s_paths.Clear();
        s_reportedMissing.Clear();
        Interlocked.Increment(ref s_version);
    }

    private static KumikoPropertyAccessor? ResolveSegment(Type type, string name)
    {
        IKumikoAccessorProvider[] globals;
        List<IKumikoAccessorProvider>? typed = null;
        lock (s_gate)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                if (s_typeProviders.TryGetValue(t, out var provider))
                    (typed ??= new()).Add(provider);
            }
            globals = s_globalProviders.ToArray();
        }

        if (typed != null)
        {
            foreach (var provider in typed)
            {
                if (provider.GetAccessor(type, name) is { } accessor)
                    return accessor;
            }
        }

        foreach (var provider in globals)
        {
            if (provider.GetAccessor(type, name) is { } accessor)
                return accessor;
        }

        if (IsReflectionPropertyAccessSupported)
            return ReflectionAccessorFactory.Create(type, name);

        ReportMissing(type, name);
        return null;
    }

    private static void ReportMissing(Type type, string name)
    {
        if (!s_reportedMissing.TryAdd((type, name), true)) return;
        Trace.TraceWarning(
            $"KumikoUI: no accessor for property '{name}' of {type.FullName}, and reflection property access is " +
            $"disabled ({ReflectionSwitchName}=false, the default for Native AOT and full trimming). Mark the type " +
            "with [KumikoGridItem], set KumikoColumn.ValueAccessor/ValueSetter, or register an IKumikoAccessorProvider.");
    }
}

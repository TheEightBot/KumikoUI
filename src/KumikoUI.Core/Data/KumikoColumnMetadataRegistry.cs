using System.Collections.Concurrent;
using System.Diagnostics;

namespace KumikoUI.Core.Data;

/// <summary>
/// Resolves the properties of an item type that the grid can generate columns for
/// (<see cref="Models.KumikoAutoGenerateColumnsMode"/>, <see cref="Models.KumikoColumnFactory"/>).
/// </summary>
/// <remarks>
/// The metadata comes, in order, from:
/// <list type="number">
/// <item>the provider registered for the item's type or the nearest of its base types (<see cref="Register"/>),
/// including the ones the <c>[KumikoGridItem]</c> source generator registers;</item>
/// <item>reflection over the public instance properties and their DataAnnotations attributes, when
/// <see cref="KumikoAccessorRegistry.IsReflectionPropertyAccessSupported"/> is true.</item>
/// </list>
/// With reflection off and no provider, a type has no columns to generate, and a warning is traced once per type.
/// </remarks>
public static class KumikoColumnMetadataRegistry
{
    private static readonly object s_gate = new();
    private static readonly Dictionary<Type, IKumikoColumnMetadataProvider> s_providers = new();
    private static readonly ConcurrentDictionary<Type, bool> s_reportedMissing = new();

    /// <summary>
    /// Registers the metadata provider for <paramref name="itemType"/> and the types derived from it. The last
    /// registration for a type wins.
    /// </summary>
    public static void Register(Type itemType, IKumikoColumnMetadataProvider provider)
    {
        ArgumentNullException.ThrowIfNull(itemType);
        ArgumentNullException.ThrowIfNull(provider);
        lock (s_gate)
            s_providers[itemType] = provider;
    }

    /// <summary>
    /// Returns the properties of <paramref name="itemType"/> in declaration order (base type properties first),
    /// including the ones marked as not generated, or an empty list when nothing describes the type.
    /// </summary>
    public static IReadOnlyList<KumikoColumnMetadata> GetColumns(Type itemType)
    {
        ArgumentNullException.ThrowIfNull(itemType);

        for (var t = itemType; t != null; t = t.BaseType)
        {
            IKumikoColumnMetadataProvider? provider;
            lock (s_gate)
                s_providers.TryGetValue(t, out provider);
            if (provider?.GetColumns(itemType) is { } columns)
                return columns;
        }

        if (KumikoAccessorRegistry.IsReflectionPropertyAccessSupported)
            return ReflectionColumnMetadataFactory.Create(itemType);

        if (s_reportedMissing.TryAdd(itemType, true))
        {
            Trace.TraceWarning(
                $"KumikoUI: cannot generate columns for {itemType.FullName}: it has no column metadata and reflection " +
                $"property access is disabled ({KumikoAccessorRegistry.ReflectionSwitchName}=false, the default for " +
                "Native AOT and full trimming). Mark the type with [KumikoGridItem] or register an " +
                "IKumikoColumnMetadataProvider.");
        }
        return Array.Empty<KumikoColumnMetadata>();
    }
}

namespace KumikoUI.Core.Data;

/// <summary>
/// Supplies property accessors for item types, so the grid reads and writes item properties without
/// reflection (Native AOT and trimming safe). Register providers with <see cref="KumikoAccessorRegistry"/>;
/// the <c>[KumikoGridItem]</c> source generator registers one for every attributed type.
/// </summary>
/// <remarks>
/// A provider can also serve items without CLR properties, for example dictionaries or data rows:
/// return an accessor whose getter reads the entry named <c>propertyName</c>.
/// </remarks>
public interface IKumikoAccessorProvider
{
    /// <summary>
    /// Returns the accessor for one property (a single path segment, never dotted) of
    /// <paramref name="itemType"/>, or null when this provider does not know it. Property names
    /// are matched case-insensitively.
    /// </summary>
    KumikoPropertyAccessor? GetAccessor(Type itemType, string propertyName);
}

namespace KumikoUI.Core.Data;

/// <summary>
/// Describes the properties of item types that the grid can generate columns for, without reflection (Native AOT and
/// trimming safe). Register providers with <see cref="KumikoColumnMetadataRegistry"/>; the <c>[KumikoGridItem]</c>
/// source generator registers one for every attributed type.
/// </summary>
public interface IKumikoColumnMetadataProvider
{
    /// <summary>
    /// Returns the properties of <paramref name="itemType"/> in declaration order, or null when this provider does not
    /// know the type. Include properties marked as not generated (<see cref="KumikoColumnMetadata.AutoGenerate"/> false);
    /// the grid skips them.
    /// </summary>
    IReadOnlyList<KumikoColumnMetadata>? GetColumns(Type itemType);
}

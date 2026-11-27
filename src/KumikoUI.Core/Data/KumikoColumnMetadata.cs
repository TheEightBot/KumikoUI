namespace KumikoUI.Core.Data;

/// <summary>
/// Describes one property of an item type that the grid can turn into a column when it generates columns
/// (<see cref="Models.KumikoAutoGenerateColumnsMode"/>, <see cref="Models.KumikoColumnFactory"/>). The
/// <c>[KumikoGridItem]</c> source generator emits it from the property and its DataAnnotations attributes; the
/// reflection fallback reads the same attributes at run time.
/// </summary>
public sealed class KumikoColumnMetadata
{
    /// <summary>Creates metadata for the property <paramref name="propertyName"/> of type <paramref name="propertyType"/>.</summary>
    /// <param name="propertyName">The property name, used as the column's <see cref="Models.KumikoColumn.PropertyName"/>.</param>
    /// <param name="propertyType">The declared type of the property, which picks the column type.</param>
    public KumikoColumnMetadata(string propertyName, Type propertyType)
    {
        ArgumentException.ThrowIfNullOrEmpty(propertyName);
        ArgumentNullException.ThrowIfNull(propertyType);
        PropertyName = propertyName;
        PropertyType = propertyType;
    }

    /// <summary>The property name.</summary>
    public string PropertyName { get; }

    /// <summary>The declared type of the property (nullable value types keep their <see cref="Nullable{T}"/> wrapper).</summary>
    public Type PropertyType { get; }

    /// <summary>The display name (<c>[Display(Name = ...)]</c>), or null.</summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// The short name meant for column headers (<c>[Display(ShortName = ...)]</c>), or null. A generated column's
    /// header is the short name, else <see cref="DisplayName"/>, else <see cref="PropertyName"/>.
    /// </summary>
    public string? ShortName { get; init; }

    /// <summary>The description (<c>[Display(Description = ...)]</c>), or null.</summary>
    public string? Description { get; init; }

    /// <summary>
    /// The position requested by <c>[Display(Order = ...)]</c>, or null. Generated columns with an order come first,
    /// in ascending order, then the others in declaration order.
    /// </summary>
    public int? Order { get; init; }

    /// <summary>
    /// The format string from <c>[DisplayFormat(DataFormatString = ...)]</c>, or null. Both a composite format such as
    /// <c>"{0:C2}"</c> and a plain format such as <c>"C2"</c> are accepted; a generated column uses the part after the
    /// colon as its <see cref="Models.KumikoColumn.Format"/>.
    /// </summary>
    public string? Format { get; init; }

    /// <summary>
    /// True when the property cannot be edited in the grid: it has no accessible setter, or it is marked
    /// <c>[Editable(false)]</c> or <c>[ReadOnly(true)]</c>.
    /// </summary>
    public bool IsReadOnly { get; init; }

    /// <summary>
    /// False when the property must not get a generated column (<c>[Display(AutoGenerateField = false)]</c> or
    /// <c>[Browsable(false)]</c>). Default true.
    /// </summary>
    public bool AutoGenerate { get; init; } = true;

    /// <summary>
    /// For an enum property (or a nullable enum), its values, which a generated column offers in its drop-down editor;
    /// otherwise null.
    /// </summary>
    public IReadOnlyList<object>? EnumValues { get; init; }
}

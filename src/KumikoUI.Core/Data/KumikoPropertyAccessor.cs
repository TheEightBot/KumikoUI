namespace KumikoUI.Core.Data;

/// <summary>
/// Reads and writes one property of an item without reflection. Created by the
/// <c>[KumikoGridItem]</c> source generator, by an <see cref="IKumikoAccessorProvider"/>, or by hand.
/// </summary>
public sealed class KumikoPropertyAccessor
{
    private readonly Func<object, object?>? _getter;
    private readonly Action<object, object?>? _setter;

    /// <summary>Creates an accessor.</summary>
    /// <param name="name">The property name.</param>
    /// <param name="propertyType">The declared property type; values are converted to it before they are set.</param>
    /// <param name="getter">Reads the property from an item, or null when the property is write-only.</param>
    /// <param name="setter">Writes the property on an item, or null when the property is read-only.</param>
    public KumikoPropertyAccessor(string name, Type propertyType,
        Func<object, object?>? getter, Action<object, object?>? setter)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(propertyType);
        Name = name;
        PropertyType = propertyType;
        _getter = getter;
        _setter = setter;
    }

    /// <summary>The property name.</summary>
    public string Name { get; }

    /// <summary>The declared property type.</summary>
    public Type PropertyType { get; }

    /// <summary>Whether the property can be read.</summary>
    public bool CanRead => _getter != null;

    /// <summary>Whether the property can be written.</summary>
    public bool CanWrite => _setter != null;

    /// <summary>Reads the property, or returns null when it is write-only.</summary>
    public object? GetValue(object item) => _getter?.Invoke(item);

    /// <summary>Writes the property. Returns false when it is read-only.</summary>
    public bool SetValue(object item, object? value)
    {
        if (_setter == null) return false;
        _setter(item, value);
        return true;
    }
}

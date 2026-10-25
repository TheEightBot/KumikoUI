namespace KumikoUI.Core.Data;

/// <summary>
/// A property path (<c>"Name"</c>, <c>"Address.City"</c>) resolved for one root item type. Segments whose
/// declaring type is known up front are resolved once; the rest are resolved per call from the runtime
/// type of the intermediate value. A null intermediate value reads as null and cannot be written.
/// </summary>
internal sealed class KumikoValuePath
{
    private readonly string[] _names;
    private readonly KumikoPropertyAccessor?[] _accessors;

    public KumikoValuePath(Type rootType, string path)
    {
        RootType = rootType;
        Path = path;
        Version = KumikoAccessorRegistry.Version;
        _names = string.IsNullOrEmpty(path) ? Array.Empty<string>() : path.Split('.');
        _accessors = new KumikoPropertyAccessor?[_names.Length];

        Type? declaringType = rootType;
        for (int i = 0; i < _names.Length && declaringType != null; i++)
        {
            var accessor = KumikoAccessorRegistry.GetSegmentAccessor(declaringType, _names[i]);
            _accessors[i] = accessor;
            // A value typed as object or an interface may be anything; resolve the next segment per call.
            declaringType = accessor?.PropertyType is { } next && next != typeof(object) && !next.IsInterface
                ? next
                : null;
        }
    }

    /// <summary>The item type the path was resolved for.</summary>
    public Type RootType { get; }

    /// <summary>The path this instance resolves.</summary>
    public string Path { get; }

    /// <summary>The <see cref="KumikoAccessorRegistry.Version"/> the path was resolved under.</summary>
    public int Version { get; }

    /// <summary>Whether this resolution still applies to <paramref name="itemType"/> and <paramref name="path"/>.</summary>
    public bool IsCurrentFor(Type itemType, string path) =>
        RootType == itemType && Version == KumikoAccessorRegistry.Version && string.Equals(Path, path, StringComparison.Ordinal);

    /// <summary>
    /// The declared type of the last segment when it is known up front, otherwise null. An empty path
    /// (the item itself) has no value type.
    /// </summary>
    public Type? ValueType => _names.Length > 0 ? _accessors[^1]?.PropertyType : null;

    /// <summary>Reads the value at the path; the item itself for an empty path.</summary>
    public object? GetValue(object item)
    {
        object? current = item;
        for (int i = 0; i < _names.Length; i++)
        {
            if (current is null) return null;
            var accessor = Accessor(i, current);
            if (accessor is null) return null;
            current = accessor.GetValue(current);
        }
        return current;
    }

    /// <summary>
    /// Writes <paramref name="value"/> at the path, converting it to the property type when needed.
    /// Returns false when the path cannot be written (unknown or read-only property, null intermediate
    /// value, empty path, a value that does not convert, or a setter that throws).
    /// </summary>
    public bool TrySetValue(object item, object? value)
    {
        if (_names.Length == 0) return false;

        object? parent = item;
        for (int i = 0; i < _names.Length - 1; i++)
        {
            if (parent is null) return false;
            var accessor = Accessor(i, parent);
            if (accessor is null) return false;
            parent = accessor.GetValue(parent);
        }
        if (parent is null) return false;

        var last = Accessor(_names.Length - 1, parent);
        if (last is null || !last.CanWrite) return false;

        if (value != null && !last.PropertyType.IsInstanceOfType(value))
        {
            var target = Nullable.GetUnderlyingType(last.PropertyType) ?? last.PropertyType;
            try
            {
                value = Convert.ChangeType(value, target);
            }
            catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
            {
                return false;
            }
        }

        try
        {
            return last.SetValue(parent, value);
        }
        catch (Exception ex) when (ex is not (OutOfMemoryException or InsufficientExecutionStackException))
        {
            // A throwing setter (validation in the item, a wrong-typed value) leaves the item unchanged,
            // as the reflection path always did. #32 surfaces such failures as validation errors.
            return false;
        }
    }

    private KumikoPropertyAccessor? Accessor(int index, object instance) =>
        _accessors[index] ?? KumikoAccessorRegistry.GetSegmentAccessor(instance.GetType(), _names[index]);
}

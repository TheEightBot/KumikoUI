namespace KumikoUI.Core.Models;

/// <summary>
/// The identity of a group in the hierarchy: the chain of (property, raw key) segments from the outermost group
/// to this one. Expand/collapse state is keyed by it, so two groups whose keys differ stay distinct when their
/// display text is the same, and state survives changes to the descriptions that leave the path valid. Built
/// when the view is rebuilt and reused afterwards, so reading it while rendering allocates nothing.
/// </summary>
internal sealed class GroupKeyPath : IEquatable<GroupKeyPath>
{
    private readonly int _hash;

    /// <summary>The enclosing group's path, or null for an outermost group.</summary>
    public GroupKeyPath? Parent { get; }

    /// <summary>The property the group at this level is based on.</summary>
    public string PropertyName { get; }

    /// <summary>The raw group key at this level; null is a key of its own.</summary>
    public object? Key { get; }

    /// <summary>The number of segments: 1 for an outermost group.</summary>
    public int Depth { get; }

    public GroupKeyPath(GroupKeyPath? parent, string propertyName, object? key)
    {
        Parent = parent;
        PropertyName = propertyName;
        Key = key;
        Depth = (parent?.Depth ?? 0) + 1;
        _hash = HashCode.Combine(parent?._hash ?? 0, StringComparer.Ordinal.GetHashCode(propertyName), key);
    }

    /// <inheritdoc />
    public bool Equals(GroupKeyPath? other)
    {
        var a = this;
        while (true)
        {
            if (ReferenceEquals(a, other)) return true;
            if (a is null || other is null || a._hash != other._hash || a.Depth != other.Depth) return false;
            if (!string.Equals(a.PropertyName, other.PropertyName, StringComparison.Ordinal)
                || !Equals(a.Key, other.Key)) return false;
            a = a.Parent!;
            other = other.Parent;
        }
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is GroupKeyPath p && Equals(p);

    /// <inheritdoc />
    public override int GetHashCode() => _hash;

    /// <summary>
    /// Whether every segment matches the group descriptions at the same level, so the path still names a group
    /// that can exist.
    /// </summary>
    public bool IsValidFor(IReadOnlyList<GroupDescription> descriptions)
    {
        if (Depth > descriptions.Count) return false;
        int level = Depth - 1;
        for (var p = this; p != null; p = p.Parent, level--)
            if (!string.Equals(p.PropertyName, descriptions[level].PropertyName, StringComparison.Ordinal))
                return false;
        return true;
    }
}

/// <summary>Compares group keys: strings by the grid's text rules, everything else by Equals/GetHashCode.</summary>
internal sealed class GroupKeyComparer : IEqualityComparer<object>
{
    private readonly StringComparer _text;

    public GroupKeyComparer(StringComparer text) => _text = text;

    public new bool Equals(object? x, object? y) =>
        x is string a && y is string b ? _text.Equals(a, b) : object.Equals(x, y);

    public int GetHashCode(object obj) => obj is string s ? _text.GetHashCode(s) : obj.GetHashCode();
}

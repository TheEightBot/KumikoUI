using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace KumikoUI.SourceGenerators;

/// <summary>An immutable array compared by value, so incremental generator outputs cache correctly.</summary>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    private readonly ImmutableArray<T> _items;

    public EquatableArray(ImmutableArray<T> items) => _items = items;

    public int Length => _items.IsDefault ? 0 : _items.Length;

    public bool Equals(EquatableArray<T> other)
    {
        if (Length != other.Length) return false;
        for (int i = 0; i < Length; i++)
        {
            if (!_items[i].Equals(other._items[i])) return false;
        }
        return true;
    }

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        int hash = 17;
        for (int i = 0; i < Length; i++)
            hash = unchecked(hash * 31 + _items[i].GetHashCode());
        return hash;
    }

    public IEnumerator<T> GetEnumerator() =>
        ((IEnumerable<T>)(_items.IsDefault ? ImmutableArray<T>.Empty : _items)).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

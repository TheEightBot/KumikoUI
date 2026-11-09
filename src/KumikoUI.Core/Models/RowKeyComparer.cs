using System.Runtime.CompilerServices;

namespace KumikoUI.Core.Models;

/// <summary>Row keys: items compare by reference, <see cref="GroupRowKey"/>s by value.</summary>
internal sealed class RowKeyComparer : IEqualityComparer<object>
{
    public static readonly RowKeyComparer Instance = new();

    public new bool Equals(object? x, object? y) =>
        x is GroupRowKey key ? key.Equals(y) : ReferenceEquals(x, y);

    public int GetHashCode(object obj) =>
        obj is GroupRowKey key ? key.GetHashCode() : RuntimeHelpers.GetHashCode(obj);
}

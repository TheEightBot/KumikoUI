namespace KumikoUI.SkiaSharp;

/// <summary>
/// A size-capped map that evicts its least recently used entry when full. Not thread-safe:
/// <see cref="SkiaResourceCache"/> guards it with its own lock.
/// </summary>
/// <remarks>
/// A hit moves the entry's node to the front of the recency list and an eviction reuses the evicted
/// node for the new entry, so neither allocates once the map has reached its capacity.
/// </remarks>
internal sealed class LruMap<TKey, TValue> where TKey : notnull
{
    private readonly Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>> _map;

    // First = most recently used, Last = least recently used.
    private readonly LinkedList<KeyValuePair<TKey, TValue>> _order = new();

    public LruMap(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        Capacity = capacity;
        _map = new Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>>();
    }

    public int Capacity { get; }

    public int Count => _map.Count;

    /// <summary>Looks up <paramref name="key"/> and marks it most recently used.</summary>
    public bool TryGet(TKey key, out TValue value)
    {
        if (_map.TryGetValue(key, out var node))
        {
            if (!ReferenceEquals(node, _order.First))
            {
                _order.Remove(node);
                _order.AddFirst(node);
            }
            value = node.Value.Value;
            return true;
        }
        value = default!;
        return false;
    }

    /// <summary>
    /// Adds a key that is not in the map. When the map is full, the least recently used entry is
    /// removed and returned through <paramref name="evicted"/> so the caller can release it.
    /// </summary>
    public bool Add(TKey key, TValue value, out KeyValuePair<TKey, TValue> evicted)
    {
        if (_map.Count >= Capacity)
        {
            var last = _order.Last!;
            evicted = last.Value;
            _map.Remove(evicted.Key);
            _order.RemoveLast();
            last.Value = new KeyValuePair<TKey, TValue>(key, value);
            _order.AddFirst(last);
            _map.Add(key, last);
            return true;
        }

        _map.Add(key, _order.AddFirst(new KeyValuePair<TKey, TValue>(key, value)));
        evicted = default;
        return false;
    }

    public bool Remove(TKey key, out TValue value)
    {
        if (_map.Remove(key, out var node))
        {
            _order.Remove(node);
            value = node.Value.Value;
            return true;
        }
        value = default!;
        return false;
    }

    /// <summary>Adds the keys of every entry that matches <paramref name="predicate"/> to <paramref name="keys"/>.</summary>
    public void CollectKeys(Func<TKey, TValue, bool> predicate, List<TKey> keys)
    {
        foreach (var entry in _order)
        {
            if (predicate(entry.Key, entry.Value))
                keys.Add(entry.Key);
        }
    }

    /// <summary>Removes every entry.</summary>
    public void Clear()
    {
        _order.Clear();
        _map.Clear();
    }

    /// <summary>Removes every entry, adding the values to <paramref name="removed"/>.</summary>
    public void Clear(List<TValue> removed)
    {
        foreach (var entry in _order)
            removed.Add(entry.Value);
        _order.Clear();
        _map.Clear();
    }
}

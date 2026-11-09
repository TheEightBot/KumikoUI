namespace KumikoUI.Core.Models;

/// <summary>
/// Describes one change to the rows a <see cref="KumikoSource"/> shows, and maps visible rows from before
/// the change to after it.
/// </summary>
public sealed class KumikoViewChangedEventArgs : EventArgs
{
    private readonly object?[]? _oldRowKeys;
    private readonly object?[] _newRowKeys;
    private Dictionary<object, int>? _newRowByKey;

    private KumikoViewChangedEventArgs(KumikoViewChangeKind kind, int viewIndex, int count,
        int oldRowCount, object?[]? oldRowKeys, object?[] newRowKeys)
    {
        Kind = kind;
        ViewIndex = viewIndex;
        Count = count;
        OldRowCount = oldRowCount;
        NewRowCount = newRowKeys.Length;
        _oldRowKeys = oldRowKeys;
        _newRowKeys = newRowKeys;
    }

    internal static KumikoViewChangedEventArgs Redraw(object?[] rowKeys) =>
        new(KumikoViewChangeKind.Redraw, -1, 0, rowKeys.Length, null, rowKeys);

    internal static KumikoViewChangedEventArgs Rebuild(object?[] oldRowKeys, object?[] newRowKeys) =>
        new(KumikoViewChangeKind.Rebuild, -1, 0, oldRowKeys.Length, oldRowKeys, newRowKeys);

    internal static KumikoViewChangedEventArgs Insert(int viewIndex, int count, object?[] newRowKeys) =>
        new(KumikoViewChangeKind.Insert, viewIndex, count, newRowKeys.Length - count, null, newRowKeys);

    internal static KumikoViewChangedEventArgs Remove(int viewIndex, int count, object?[] newRowKeys) =>
        new(KumikoViewChangeKind.Remove, viewIndex, count, newRowKeys.Length + count, null, newRowKeys);

    /// <summary>What changed.</summary>
    public KumikoViewChangeKind Kind { get; }

    /// <summary>The first visible row inserted or removed, for <see cref="KumikoViewChangeKind.Insert"/> and
    /// <see cref="KumikoViewChangeKind.Remove"/>; otherwise -1.</summary>
    public int ViewIndex { get; }

    /// <summary>The number of visible rows inserted or removed; otherwise 0.</summary>
    public int Count { get; }

    /// <summary>The number of visible rows before the change.</summary>
    public int OldRowCount { get; }

    /// <summary>The number of visible rows after the change.</summary>
    public int NewRowCount { get; }

    /// <summary>Whether visible rows may have moved, appeared or disappeared (anything but a redraw).</summary>
    public bool IsStructural => Kind != KumikoViewChangeKind.Redraw;

    /// <summary>
    /// Maps a visible row index from before the change to the index of the same row after it, or -1 when the
    /// row is no longer visible (filtered out, collapsed, removed) or <paramref name="oldViewRow"/> was out of
    /// range. Data rows are matched by item; group header and group summary rows by group.
    /// </summary>
    public int MapRow(int oldViewRow)
    {
        if (oldViewRow < 0 || oldViewRow >= OldRowCount) return -1;

        switch (Kind)
        {
            case KumikoViewChangeKind.Redraw:
                return oldViewRow;
            case KumikoViewChangeKind.Insert:
                return oldViewRow < ViewIndex ? oldViewRow : oldViewRow + Count;
            case KumikoViewChangeKind.Remove:
                if (oldViewRow < ViewIndex) return oldViewRow;
                return oldViewRow < ViewIndex + Count ? -1 : oldViewRow - Count;
            default:
                var key = _oldRowKeys![oldViewRow];
                return key != null && NewRowByKey().TryGetValue(key, out int row) ? row : -1;
        }
    }

    /// <summary>The visible row that shows <paramref name="item"/> after the change, or -1 when it is not visible.</summary>
    public int GetViewRow(object item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return NewRowByKey().TryGetValue(item, out int row) ? row : -1;
    }

    private Dictionary<object, int> NewRowByKey()
    {
        if (_newRowByKey != null) return _newRowByKey;

        var map = new Dictionary<object, int>(_newRowKeys.Length, RowKeyComparer.Instance);
        for (int i = 0; i < _newRowKeys.Length; i++)
        {
            if (_newRowKeys[i] is { } key)
                map.TryAdd(key, i);
        }
        return _newRowByKey = map;
    }
}

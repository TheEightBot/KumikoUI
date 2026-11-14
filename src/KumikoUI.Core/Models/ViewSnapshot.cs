using System.Collections;

namespace KumikoUI.Core.Models;

/// <summary>
/// One built view of a <see cref="KumikoSource"/>: the visible rows after filtering, sorting and grouping, the group
/// structure, and the computed summaries (#254). The source publishes one at a time and answers every read from it,
/// so a rebuild that runs on a worker never shows a half-built view: the source swaps the finished snapshot in on
/// its own thread. Built by <see cref="ViewBuilder"/>; after it is published only the source's thread touches it.
/// </summary>
internal sealed class ViewSnapshot
{
    /// <summary>
    /// The items that <see cref="ViewIndices"/> and the data rows of <see cref="FlatView"/> index: the bound
    /// collection itself, or (<see cref="ItemsAreCopy"/>) a copy taken when the build started, which keeps the view
    /// readable after the collection changes and until the next view is published.
    /// </summary>
    public IList Items = Array.Empty<object>();

    /// <summary>Whether <see cref="Items"/> is a copy (an <see cref="ItemBuffer"/>) rather than the bound collection.</summary>
    public bool ItemsAreCopy;

    /// <summary>
    /// The source's collection version the copied <see cref="Items"/> match: equal to the source's current version
    /// while the copy and the collection hold the same items in the same order.
    /// </summary>
    public int CollectionVersion;

    /// <summary>The filtered and sorted source indices.</summary>
    public List<int> ViewIndices = new();

    /// <summary>Group headers, data rows and group summary rows in view order; null when not grouping.</summary>
    public List<FlatViewRow>? FlatView;

    /// <summary>The list <see cref="FlatView"/> points to while grouping (reused by synchronous rebuilds).</summary>
    public List<FlatViewRow> FlatViewBuffer = new();

    /// <summary>The number of group levels the view was built with (0 when not grouping).</summary>
    public int GroupLevels;

    // The group structure of the flat view (sticky group headers, #174). For every group header in view order: its
    // row, its level, the index (into these lists) of its enclosing group's header (-1 at level 0) and the row just
    // after the group (the next header at the same or an outer level, or the row count).
    public readonly List<int> GroupHeaderRows = new();
    public readonly List<int> GroupHeaderLevels = new();
    public readonly List<int> GroupHeaderParents = new();
    public readonly List<int> GroupHeaderEnds = new();

    /// <summary>Every group header, in view order (parallel to <see cref="GroupHeaderRows"/>).</summary>
    public readonly List<GroupHeaderInfo> Groups = new();

    /// <summary>Groups by <see cref="GroupHeaderInfo.GroupPath"/>, built on the first lookup by path.</summary>
    public Dictionary<string, GroupHeaderInfo>? GroupsByPath;

    // ── Table summaries ──────────────────────────────────────────
    public List<ComputedSummaryRow> TopSummaries = new();
    public List<ComputedSummaryRow> BottomSummaries = new();

    /// <summary>
    /// One aggregate per summarized property over the visible rows (#69). Sum, Average and Count aggregates also keep
    /// each row's value, so a PropertyChanged of the property updates them in O(1).
    /// </summary>
    public Dictionary<string, PropertyAggregate> TableAggregates = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The computed row each table summary definition produced: which list and where, for in-place updates.</summary>
    public readonly List<TableSummarySlot> TableSummarySlots = new();

    /// <summary>The number of items when the table aggregates kept their row values.</summary>
    public int TableValuesCount;

    // ── Group and caption summaries (in place, #254) ─────────────
    /// <summary>The group summary and caption settings the view was built with; null when it has neither.</summary>
    public GroupSummarySettings? GroupSummaries;

    /// <summary>
    /// The innermost group each source index falls in (null: no row), kept when a group or caption summary can follow
    /// a changed value in place.
    /// </summary>
    public GroupHeaderInfo?[]? InnermostGroup;

    // ── Lookups ──────────────────────────────────────────────────
    /// <summary>
    /// Each item's index in <see cref="Items"/> (by reference; -1 for an item that appears more than once). Built when
    /// a summary keeps row values; null until the first lookup otherwise.
    /// </summary>
    public Dictionary<object, int>? SourceIndexByItem;

    /// <summary>The visible row of each row key; built on first use, or by a background build.</summary>
    public Dictionary<object, int>? RowKeyIndex;

    /// <summary>The key of every visible row, built by a background build for the change notification.</summary>
    public object?[]? RowKeys;

    /// <summary>A copy buffer a background build allocated for the source's next copy of the items.</summary>
    public ItemBuffer? SpareItems;

    /// <summary>Reset the state a synchronous rebuild that reuses this snapshot's buffers recomputes.</summary>
    public void ResetForReuse()
    {
        ViewIndices.Clear();
        FlatViewBuffer.Clear();
        FlatView = null;
        GroupLevels = 0;
        GroupHeaderRows.Clear();
        GroupHeaderLevels.Clear();
        GroupHeaderParents.Clear();
        GroupHeaderEnds.Clear();
        Groups.Clear();
        GroupsByPath = null;
        TableSummarySlots.Clear();
        GroupSummaries = null;
        InnermostGroup = null;
        SourceIndexByItem = null;
        RowKeyIndex = null;
        RowKeys = null;
    }
}

/// <summary>The computed row a table summary definition produced: which list and where, for in-place updates.</summary>
internal readonly record struct TableSummarySlot(string Name, string? Title, IReadOnlyList<SummaryColumnDescription> Columns,
    List<ComputedSummaryRow> List, int Index);

/// <summary>
/// The group summary rows and caption summary a grouped view was built with, and per summarized property the row
/// values the in-place updates replace (#254).
/// </summary>
internal sealed class GroupSummarySettings
{
    public GroupSummarySettings(SummaryRowSpec[] rows, CaptionSpec? caption, string[] properties)
    {
        Rows = rows;
        OuterRows = Array.FindAll(rows, r => r.Levels != GroupSummaryLevels.Innermost);
        Caption = caption;
        Properties = properties;
        Readers = new ItemReader[properties.Length];
        Values = new object?[]?[properties.Length];
        InPlace = new bool[properties.Length];
    }

    public SummaryRowSpec[] Rows { get; }

    /// <summary>The rows that apply at an outer (not innermost) group level.</summary>
    public SummaryRowSpec[] OuterRows { get; }

    public CaptionSpec? Caption { get; }

    /// <summary>Every property a group summary row or the caption aggregates (the index is the property id).</summary>
    public string[] Properties { get; }

    /// <summary>Reads each property from an item.</summary>
    public ItemReader[] Readers { get; }

    /// <summary>Each property's value by source index, for the rows of the view.</summary>
    public object?[]?[] Values { get; }

    /// <summary>Whether every aggregate of the property is a Sum, Average or Count (so a change applies in place).</summary>
    public bool[] InPlace { get; }

    /// <summary>The id of <paramref name="property"/> in <see cref="Properties"/> (ignoring case), or -1.</summary>
    public int IndexOf(string property)
    {
        for (int i = 0; i < Properties.Length; i++)
            if (string.Equals(Properties[i], property, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }
}

/// <summary>
/// A private, growable copy of the bound collection's items: what a view built from a snapshot reads, so the view
/// stays readable when the collection changes. Single-item adds and removes are mirrored into it while the view
/// follows them incrementally. Implements just the <see cref="IList"/> members the source uses.
/// </summary>
internal sealed class ItemBuffer : IList
{
    private object?[] _items;
    private int _count;

    private ItemBuffer(object?[] items, int count)
    {
        _items = items;
        _count = count;
    }

    /// <summary>An empty buffer that holds <paramref name="count"/> items, and some more, without growing.</summary>
    public static ItemBuffer WithCapacity(int count) => new(new object?[CapacityFor(count)], 0);

    /// <summary>The array length for <paramref name="count"/> items: room for some growth.</summary>
    private static int CapacityFor(int count) => count == 0 ? 0 : count + count / 16 + 16;

    /// <summary>The number of items the buffer holds without growing.</summary>
    public int Capacity => _items.Length;

    /// <summary>
    /// Copy <paramref name="source"/>, with one bulk copy when the collection supports it, into
    /// <paramref name="reuse"/> when it is large enough (and not much larger), otherwise into a new buffer.
    /// </summary>
    public static ItemBuffer CopyOf(IList source, ItemBuffer? reuse = null)
    {
        int count = source.Count;
        object?[] items;
        if (reuse != null && reuse._items.Length >= count && reuse._items.Length <= 2 * count + 1024)
        {
            items = reuse._items;
            if (reuse._count > count)
                Array.Clear(items, count, reuse._count - count); // drop references to items no longer there
            reuse._count = 0;
        }
        else
        {
            items = count == 0 ? Array.Empty<object?>() : new object?[CapacityFor(count)];
            reuse = null;
        }

        if (count > 0)
        {
            try
            {
                source.CopyTo(items, 0);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidCastException or NotSupportedException
                                           or ArrayTypeMismatchException or InvalidOperationException)
            {
                for (int i = 0; i < count; i++) items[i] = source[i];
            }
        }
        if (reuse == null) return new ItemBuffer(items, count);
        reuse._count = count;
        return reuse;
    }

    public object? this[int index]
    {
        get => (uint)index < (uint)_count ? _items[index] : throw new ArgumentOutOfRangeException(nameof(index));
        set => throw new NotSupportedException();
    }

    public int Count => _count;

    public bool IsReadOnly => true;

    public bool IsFixedSize => true;

    public bool IsSynchronized => false;

    public object SyncRoot => this;

    /// <summary>Insert <paramref name="item"/> at <paramref name="index"/>, as the bound collection did.</summary>
    public void InsertItem(int index, object? item)
    {
        if ((uint)index > (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));
        if (_count == _items.Length)
            Array.Resize(ref _items, Math.Max(4, _items.Length * 2));
        if (index < _count)
            Array.Copy(_items, index, _items, index + 1, _count - index);
        _items[index] = item;
        _count++;
    }

    /// <summary>Remove the item at <paramref name="index"/>, as the bound collection did.</summary>
    public void RemoveItemAt(int index)
    {
        if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));
        _count--;
        if (index < _count)
            Array.Copy(_items, index + 1, _items, index, _count - index);
        _items[_count] = null;
    }

    public int Add(object? value) => throw new NotSupportedException();

    public void Clear() => throw new NotSupportedException();

    public bool Contains(object? value) => IndexOf(value) >= 0;

    public int IndexOf(object? value)
    {
        for (int i = 0; i < _count; i++)
            if (ReferenceEquals(_items[i], value)) return i;
        return -1;
    }

    public void Insert(int index, object? value) => throw new NotSupportedException();

    public void Remove(object? value) => throw new NotSupportedException();

    public void RemoveAt(int index) => throw new NotSupportedException();

    public void CopyTo(Array array, int index) => Array.Copy(_items, 0, array, index, _count);

    public IEnumerator GetEnumerator()
    {
        for (int i = 0; i < _count; i++)
            yield return _items[i];
    }
}

/// <summary>Row-key lookups shared by the source and <see cref="KumikoViewChangedEventArgs"/>.</summary>
internal static class RowKeys
{
    /// <summary>The visible row of each key (first occurrence), as <see cref="KumikoViewChangedEventArgs"/> maps rows.</summary>
    public static Dictionary<object, int> Index(object?[] keys)
    {
        var map = new Dictionary<object, int>(keys.Length, RowKeyComparer.Instance);
        for (int i = 0; i < keys.Length; i++)
        {
            if (keys[i] is { } key)
                map.TryAdd(key, i);
        }
        return map;
    }
}

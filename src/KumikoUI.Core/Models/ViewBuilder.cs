using System.Collections;
using System.Runtime.InteropServices;
using KumikoUI.Core.Data;

namespace KumikoUI.Core.Models;

/// <summary>
/// Reads one column's (or summarized property's) value from items: the column's
/// <see cref="KumikoColumn.ValueAccessor"/> when it has one, otherwise its property path, resolved per item type.
/// Captured on the source's thread so a background build never reads the (mutable) column; not thread-safe itself,
/// so each build has its own.
/// </summary>
internal sealed class ItemReader
{
    private readonly Func<object, object?>? _accessor;
    private readonly string _path;
    private Type? _type;
    private KumikoValuePath? _resolved;

    public ItemReader(KumikoColumn column) : this(column.ValueAccessor, column.PropertyName) { }

    public ItemReader(Func<object, object?>? accessor, string? path)
    {
        _accessor = accessor;
        _path = path ?? string.Empty;
    }

    /// <summary>The value of the column for <paramref name="item"/>.</summary>
    public object? Read(object item)
    {
        if (_accessor != null) return _accessor(item);
        var type = item.GetType();
        var path = _resolved;
        if (path == null || !ReferenceEquals(type, _type))
        {
            _resolved = path = KumikoAccessorRegistry.GetPath(type, _path);
            _type = type;
        }
        return path.GetValue(item);
    }
}

/// <summary>A sort column, captured for one build: its reader, comparer and direction (1 or -1).</summary>
internal readonly record struct SortSpec(ItemReader Reader, IComparer Comparer, int Direction);

/// <summary>A column filter, captured for one build: its value list as a set under the build's text rules.</summary>
internal sealed record FilterSpec(KumikoColumn Column, ItemReader Reader, FilterDescription Filter, ISet<string>? Values, string? Format);

/// <summary>One group level, captured for one build.</summary>
internal sealed record GroupSpec(
    string PropertyName,
    string HeaderText,
    ItemReader? Reader,
    string? ColumnFormat,
    IComparer KeyComparer,
    Func<object?, object?>? KeySelector,
    Func<object?, string>? DisplayTextSelector,
    KumikoSortDirection SortDirection);

/// <summary>A table or group summary row definition, captured for one build.</summary>
internal sealed record SummaryRowSpec(
    string Name,
    string? Title,
    SummaryPosition Position,
    GroupSummaryLevels Levels,
    IReadOnlyList<SummaryColumnDescription> Columns);

/// <summary>The caption summary definition, captured for one build.</summary>
internal sealed record CaptionSpec(IReadOnlyList<SummaryColumnDescription> Columns, string? FormatTemplate);

/// <summary>
/// Everything a view build reads, captured on the source's thread: the items (the collection itself, or a copy for a
/// background build) and copies of the sort, filter, grouping and summary settings, with a comparer whose culture is
/// fixed. A background build reads only this and the items' values, never the source or its columns.
/// </summary>
internal sealed class ViewBuildSettings
{
    public required IList Items { get; init; }
    public bool ItemsAreCopy { get; init; }
    public int CollectionVersion { get; init; }
    public required KumikoComparer Comparer { get; init; }
    public required KumikoStrings Strings { get; init; }
    public Func<object, bool>? Predicate { get; init; }
    public required FilterSpec[] Filters { get; init; }
    public required SortSpec[] Sorts { get; init; }
    public required GroupSpec[] Groups { get; init; }
    public required IReadOnlyDictionary<GroupKeyPath, bool> ExpandedState { get; init; }
    public bool DefaultExpanded { get; init; }
    public required SummaryRowSpec[] TableSummaries { get; init; }
    public required SummaryRowSpec[] GroupSummaries { get; init; }
    public CaptionSpec? Caption { get; init; }

    /// <summary>A reader for each summarized property (ignoring case).</summary>
    public required Dictionary<string, ItemReader> SummaryReaders { get; init; }

    /// <summary>
    /// Build the row keys, the row-key index and the item index too, so publishing the view on the source's thread
    /// costs no pass over the rows (background builds).
    /// </summary>
    public bool BuildLookups { get; init; }

    /// <summary>Allocate a spare copy buffer of the items' size for the source's next copy (background builds).</summary>
    public bool AllocateSpareItems { get; init; }
}

/// <summary>
/// Builds a <see cref="ViewSnapshot"/> from <see cref="ViewBuildSettings"/>: filter, sort (each sort value read once
/// per row), group (one shared index buffer partitioned in place, no list per group), and compute the table, group
/// and caption summaries. Pure: it reads only the settings and the items, so it runs on the source's thread for a
/// synchronous rebuild and on a worker for a background one (#254). Checks <see cref="CancellationToken"/> as it goes.
/// </summary>
internal sealed class ViewBuilder
{
    private const int CheckMask = 0x3FF; // check for cancellation every 1024 rows

    private readonly ViewBuildSettings _s;
    private readonly ViewSnapshot _view;
    private readonly CancellationToken _ct;
    private readonly IList _items;

    // Grouping buffers: the view indices in group order, a scatter buffer and each position's group at the level
    // being partitioned; one set of per-level buffers, reused by every group at that level.
    private int[] _segment = Array.Empty<int>();
    private int[] _scratch = Array.Empty<int>();
    private int[] _groupOf = Array.Empty<int>();
    private LevelBuffers[] _levels = Array.Empty<LevelBuffers>();
    private GroupSummarySettings? _groupSummaries;
    private GroupHeaderInfo?[]? _innermost;

    private ViewBuilder(ViewBuildSettings settings, ViewSnapshot view, CancellationToken ct)
    {
        _s = settings;
        _view = view;
        _ct = ct;
        _items = settings.Items;
    }

    /// <summary>
    /// Build a view. With <paramref name="reuse"/>, its lists are cleared and refilled (a synchronous rebuild of the
    /// published view); otherwise a new snapshot is returned (a background build).
    /// </summary>
    public static ViewSnapshot Build(ViewBuildSettings settings, ViewSnapshot? reuse, CancellationToken ct)
    {
        var view = reuse ?? new ViewSnapshot();
        if (reuse != null) reuse.ResetForReuse();
        view.Items = settings.Items;
        view.ItemsAreCopy = settings.ItemsAreCopy;
        view.CollectionVersion = settings.CollectionVersion;

        var builder = new ViewBuilder(settings, view, ct);
        builder.Filter();
        builder.Sort();
        if (settings.Groups.Length > 0)
            builder.Group();
        ComputeTableSummaries(view, settings, ct);
        if (settings.BuildLookups)
            builder.BuildLookups();
        if (settings.AllocateSpareItems)
            view.SpareItems = ItemBuffer.WithCapacity(settings.Items.Count);
        return view;
    }

    private void Check(int i)
    {
        if ((i & CheckMask) == 0) _ct.ThrowIfCancellationRequested();
    }

    // ── Filter ───────────────────────────────────────────────────

    private void Filter()
    {
        var indices = _view.ViewIndices;
        var filters = _s.Filters;
        var predicate = _s.Predicate;
        for (int i = 0; i < _items.Count; i++)
        {
            Check(i);
            if (_items[i] is not { } item)
                continue; // null items never become rows
            if (predicate == null && filters.Length == 0 || Passes(item, predicate, filters, _s.Comparer))
                indices.Add(i);
        }
    }

    /// <summary>Whether an item passes the filter predicate and the column filters.</summary>
    public static bool Passes(object item, Func<object, bool>? predicate, FilterSpec[] filters, KumikoComparer comparer)
    {
        if (predicate != null && !predicate(item))
            return false;

        foreach (var spec in filters)
        {
            var cellValue = spec.Reader.Read(item);
            // Only a value list compares display text; conditions read the value, so don't format for them.
            string displayText = spec.Values != null ? comparer.Format(cellValue, spec.Format) : string.Empty;
            if (!spec.Filter.Evaluate(cellValue, displayText, spec.Values, comparer))
                return false;
        }

        return true;
    }

    // ── Sort ─────────────────────────────────────────────────────

    private void Sort()
    {
        var sorts = _s.Sorts;
        var indices = _view.ViewIndices;
        int n = indices.Count;
        if (sorts.Length == 0 || n < 2) return;

        // Read each sort value once per row (by position in the filtered list), then sort positions by them: the
        // comparisons read no item, and a row's value cannot change between two comparisons.
        var span = CollectionsMarshal.AsSpan(indices);
        var keys = new object?[sorts.Length][];
        for (int k = 0; k < sorts.Length; k++)
        {
            var column = keys[k] = new object?[n];
            var reader = sorts[k].Reader;
            for (int p = 0; p < n; p++)
            {
                Check(p);
                column[p] = _items[span[p]] is { } item ? reader.Read(item) : null;
            }
        }

        var order = new int[n];
        for (int p = 0; p < n; p++) order[p] = p;

        int compared = 0;
        var ct = _ct;
        Array.Sort(order, (a, b) =>
        {
            if ((++compared & 0xFFFF) == 0) ct.ThrowIfCancellationRequested();
            for (int k = 0; k < sorts.Length; k++)
            {
                var va = keys[k][a];
                var vb = keys[k][b];
                var spec = sorts[k];

                int result;
                if (va == null && vb == null) result = 0;
                else if (va == null) result = -1 * spec.Direction;
                else if (vb == null) result = 1 * spec.Direction;
                else result = spec.Comparer.Compare(va, vb) * spec.Direction;

                if (result != 0) return result;
            }
            return a.CompareTo(b); // stable: equal values keep their source order
        });

        for (int p = 0; p < n; p++) order[p] = span[order[p]];
        order.CopyTo(span);
    }

    // ── Group ────────────────────────────────────────────────────

    /// <summary>A group being collected at one level.</summary>
    private struct Bucket
    {
        public object? Key;
        public string DisplayText;
        public int Count;
        public int Start;
        public int Next;
    }

    /// <summary>The buffers of one group level, reused by every group partitioned at that level.</summary>
    private sealed class LevelBuffers(StringComparer text)
    {
        public readonly List<Bucket> Buckets = new();
        public readonly Dictionary<object, int> Map = new(new GroupKeyComparer(text));
        public readonly List<int> Order = new();
        public readonly Dictionary<string, int> Texts = new(StringComparer.Ordinal);
    }

    private void Group()
    {
        var view = _view;
        var flat = view.FlatViewBuffer;
        flat.Clear();
        view.FlatView = flat;
        view.GroupLevels = _s.Groups.Length;

        int n = view.ViewIndices.Count;
        _segment = view.ViewIndices.ToArray();
        _scratch = new int[n];
        _groupOf = new int[n];
        var text = _s.Comparer.TextComparer;
        _levels = new LevelBuffers[_s.Groups.Length];
        for (int i = 0; i < _levels.Length; i++) _levels[i] = new LevelBuffers(text);

        PrepareGroupSummaries();
        BuildLevel(0, n, 0, null, -1);
    }

    /// <summary>
    /// Read every property a group summary or the caption aggregates once per row (so nested levels aggregate from the
    /// values instead of reading the items again), and note which properties can follow a changed value in place.
    /// </summary>
    private void PrepareGroupSummaries()
    {
        var caption = _s.Caption is { Columns.Count: > 0 } c ? c : null;
        if (_s.GroupSummaries.Length == 0 && caption == null) return;

        var properties = new List<string>();
        void Collect(IReadOnlyList<SummaryColumnDescription> columns)
        {
            foreach (var column in columns)
            {
                bool known = false;
                foreach (var p in properties)
                    known |= string.Equals(p, column.PropertyName, StringComparison.OrdinalIgnoreCase);
                if (!known) properties.Add(column.PropertyName);
            }
        }
        foreach (var row in _s.GroupSummaries) Collect(row.Columns);
        if (caption != null) Collect(caption.Columns);

        var settings = new GroupSummarySettings(_s.GroupSummaries, caption, properties.ToArray());
        bool anyInPlace = false;
        var indices = _view.ViewIndices;
        for (int k = 0; k < settings.Properties.Length; k++)
        {
            string property = settings.Properties[k];
            var reader = settings.Readers[k] = _s.SummaryReaders[property];
            bool inPlace = true;
            foreach (var row in _s.GroupSummaries) inPlace &= InPlaceFor(row.Columns, property);
            if (caption != null) inPlace &= InPlaceFor(caption.Columns, property);
            settings.InPlace[k] = inPlace;
            anyInPlace |= inPlace;

            var values = settings.Values[k] = new object?[_items.Count];
            for (int i = 0; i < indices.Count; i++)
            {
                Check(i);
                int source = indices[i];
                if (_items[source] is { } item) values[source] = reader.Read(item);
            }
        }

        _groupSummaries = _view.GroupSummaries = settings;
        if (anyInPlace)
            _innermost = _view.InnermostGroup = new GroupHeaderInfo?[_items.Count];
    }

    /// <summary>Whether every description of <paramref name="property"/> is a Sum, Average or Count.</summary>
    private static bool InPlaceFor(IReadOnlyList<SummaryColumnDescription> columns, string property)
    {
        foreach (var column in columns)
        {
            if (string.Equals(column.PropertyName, property, StringComparison.OrdinalIgnoreCase) &&
                column.SummaryType is not (SummaryType.Sum or SummaryType.Average or SummaryType.Count))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Emit the groups of <c>_segment[start..end)</c> at <paramref name="level"/>: partition the segment by group key
    /// (stable, so the sort order holds within each group), order the groups, then emit each group's header, its
    /// rows (recursively) and its summary rows.
    /// </summary>
    private void BuildLevel(int start, int end, int level, GroupHeaderInfo? parent, int parentHeader)
    {
        var view = _view;
        var flat = view.FlatViewBuffer;
        if (level >= _s.Groups.Length)
        {
            for (int i = start; i < end; i++)
            {
                flat.Add(FlatViewRow.Data(_segment[i]));
                if (_innermost != null) _innermost[_segment[i]] = parent;
            }
            return;
        }

        var spec = _s.Groups[level];
        var buffers = _levels[level];
        var buckets = buffers.Buckets;
        buckets.Clear();
        buffers.Map.Clear();
        buffers.Texts.Clear();
        int nullBucket = -1;

        // Group the positions by key: groups are the distinct keys (strings by the text rules, other keys by
        // Equals/GetHashCode), whatever their display text; a null key is a group of its own.
        for (int i = start; i < end; i++)
        {
            Check(i);
            var item = _items[_segment[i]]!; // the view holds no null items
            object? raw = spec.Reader?.Read(item);
            object? key = spec.KeySelector != null ? spec.KeySelector(raw) : raw;

            int bucket;
            if (key is null)
            {
                if (nullBucket < 0)
                {
                    nullBucket = buckets.Count;
                    buckets.Add(new Bucket { Key = null, DisplayText = DisplayText(spec, null) });
                }
                bucket = nullBucket;
            }
            else if (!buffers.Map.TryGetValue(key, out bucket))
            {
                bucket = buckets.Count;
                buffers.Map[key] = bucket;
                buckets.Add(new Bucket { Key = key, DisplayText = DisplayText(spec, key) });
            }
            _groupOf[i] = bucket;
            CollectionsMarshal.AsSpan(buckets)[bucket].Count++;
        }

        // Order groups by their key (numbers numerically, dates chronologically), through the column's comparer
        // when it has one; equal keys fall back to display text, then first appearance.
        var order = buffers.Order;
        order.Clear();
        for (int b = 0; b < buckets.Count; b++) order.Add(b);
        if (spec.SortDirection != KumikoSortDirection.None && order.Count > 1)
        {
            var comparer = _s.Comparer;
            int direction = spec.SortDirection == KumikoSortDirection.Descending ? -1 : 1;
            order.Sort((a, b) =>
            {
                var x = buckets[a];
                var y = buckets[b];
                int result = spec.KeyComparer.Compare(x.Key, y.Key);
                if (result == 0) result = comparer.CompareText(x.DisplayText, y.DisplayText);
                return result != 0 ? result * direction : a.CompareTo(b);
            });
        }

        // Partition the segment into the groups, in group order (a stable counting sort).
        var all = CollectionsMarshal.AsSpan(buckets);
        int position = start;
        foreach (int b in order)
        {
            all[b].Start = all[b].Next = position;
            position += all[b].Count;
        }
        for (int i = start; i < end; i++)
            _scratch[all[_groupOf[i]].Next++] = _segment[i];
        Array.Copy(_scratch, start, _segment, start, end - start);

        bool innermost = level == _s.Groups.Length - 1;
        foreach (int b in order)
        {
            var bucket = buckets[b];

            // Distinct keys can share display text; the group path stays unique per group with a numeric suffix.
            int ordinal = 0;
            if (buffers.Texts.TryGetValue(bucket.DisplayText, out int seen))
                buffers.Texts[bucket.DisplayText] = ordinal = seen + 1;
            else
                buffers.Texts[bucket.DisplayText] = 1;

            var keyPath = new GroupKeyPath(parent?.KeyPath, spec.PropertyName, bucket.Key);
            bool isExpanded = _s.ExpandedState.TryGetValue(keyPath, out bool state) ? state : _s.DefaultExpanded;

            var info = new GroupHeaderInfo
            {
                Level = level,
                Key = bucket.Key,
                DisplayText = bucket.DisplayText,
                PropertyName = spec.PropertyName,
                HeaderText = spec.HeaderText,
                ItemCount = bucket.Count,
                IsExpanded = isExpanded,
                KeyPath = keyPath,
                Parent = parent,
                PathOrdinal = ordinal,
            };

            int header = view.Groups.Count;
            view.Groups.Add(info);
            view.GroupHeaderRows.Add(flat.Count);
            view.GroupHeaderLevels.Add(level);
            view.GroupHeaderParents.Add(parentHeader);
            view.GroupHeaderEnds.Add(flat.Count);
            flat.Add(FlatViewRow.Header(info));

            int groupStart = bucket.Start, groupEnd = bucket.Start + bucket.Count;
            ComputeGroupSummaries(info, groupStart, groupEnd, isExpanded, innermost);

            if (isExpanded)
            {
                BuildLevel(groupStart, groupEnd, level + 1, info, header);

                // Group summary rows follow the group's data: at every level for rows set to All, at the innermost
                // level for rows set to Innermost.
                if (info.Summaries is { } rows)
                {
                    for (int si = 0; si < rows.Count; si++)
                        flat.Add(FlatViewRow.Summary(info, si));
                }
            }
            else if (_innermost != null)
            {
                for (int i = groupStart; i < groupEnd; i++)
                    _innermost[_segment[i]] = info;
            }

            view.GroupHeaderEnds[header] = flat.Count;
        }
    }

    /// <summary>The display text of a group key.</summary>
    private string DisplayText(GroupSpec spec, object? key) =>
        spec.DisplayTextSelector != null
            ? spec.DisplayTextSelector(key)
            : key is null
                ? _s.Strings.BlankGroupText
                : _s.Comparer.Format(key, spec.KeySelector == null ? spec.ColumnFormat : null);

    /// <summary>
    /// The caption summary of a group (every group) and its group summary rows (expanded groups, for the rows that
    /// apply at its level), from one aggregate per property over the group's rows. The aggregates stay on the group so
    /// a changed value can update them in place.
    /// </summary>
    private void ComputeGroupSummaries(GroupHeaderInfo info, int start, int end, bool isExpanded, bool isInnermost)
    {
        var settings = _groupSummaries;
        if (settings == null) return;

        bool wantRows = false;
        if (isExpanded)
        {
            foreach (var row in settings.Rows)
                wantRows |= row.Levels != GroupSummaryLevels.Innermost || isInnermost;
        }
        var caption = settings.Caption;
        if (!wantRows && caption == null) return;

        var aggregates = new PropertyAggregate?[settings.Properties.Length];
        if (caption != null) Require(settings, aggregates, caption.Columns);
        if (wantRows)
        {
            foreach (var row in settings.Rows)
                if (row.Levels != GroupSummaryLevels.Innermost || isInnermost) Require(settings, aggregates, row.Columns);
        }

        for (int k = 0; k < aggregates.Length; k++)
        {
            var aggregate = aggregates[k];
            if (aggregate == null) continue;
            aggregate.Reader = settings.Readers[k].Read;
            aggregate.Begin();
            var values = settings.Values[k]!;
            for (int i = start; i < end; i++)
            {
                Check(i);
                aggregate.Add(values[_segment[i]], _s.Comparer);
            }
        }
        info.SummaryAggregates = aggregates;
        info.SummaryOwner = settings;

        if (caption != null)
            info.CaptionText = BuildCaption(settings, aggregates, _s.Comparer, _s.Strings);
        if (wantRows)
        {
            var rows = new List<ComputedSummaryRow>(settings.Rows.Length);
            foreach (var row in settings.Rows)
            {
                if (row.Levels == GroupSummaryLevels.Innermost && !isInnermost) continue;
                rows.Add(BuildGroupRow(settings, aggregates, row, _s.Comparer, _s.Strings));
            }
            info.Summaries = rows;
            info.SummaryRowSpecs = isInnermost ? settings.Rows : settings.OuterRows;
        }
    }

    private static void Require(GroupSummarySettings settings, PropertyAggregate?[] aggregates,
        IReadOnlyList<SummaryColumnDescription> columns)
    {
        foreach (var column in columns)
        {
            int k = settings.IndexOf(column.PropertyName);
            if (k < 0) continue;
            (aggregates[k] ??= new PropertyAggregate(settings.Properties[k])).Require(column.SummaryType);
        }
    }

    /// <summary>The caption text of a group from its aggregates.</summary>
    public static string BuildCaption(GroupSummarySettings settings, PropertyAggregate?[] aggregates,
        KumikoComparer comparer, KumikoStrings strings) =>
        SummaryText.BuildCaption(settings.Caption!.Columns, settings.Caption.FormatTemplate,
            p => settings.IndexOf(p) is var k and >= 0 ? aggregates[k] : null, comparer.Culture, strings);

    /// <summary>One group summary row of a group from its aggregates.</summary>
    public static ComputedSummaryRow BuildGroupRow(GroupSummarySettings settings, PropertyAggregate?[] aggregates,
        SummaryRowSpec row, KumikoComparer comparer, KumikoStrings strings) =>
        SummaryText.BuildRow(row.Name, row.Title, row.Columns,
            p => settings.IndexOf(p) is var k and >= 0 ? aggregates[k] : null, comparer.Culture, strings);

    // ── Table summaries ──────────────────────────────────────────

    /// <summary>
    /// Compute the table-level summaries (top/bottom) of <paramref name="view"/> from all its visible data rows. Its
    /// aggregates are kept between rebuilds of the same snapshot, so their row-value buffers are reused.
    /// </summary>
    public static void ComputeTableSummaries(ViewSnapshot view, ViewBuildSettings settings, CancellationToken ct)
    {
        view.TopSummaries.Clear();
        view.BottomSummaries.Clear();
        view.TableSummarySlots.Clear();

        // One aggregate per property for every table summary row: they all cover the visible rows.
        var aggregates = view.TableAggregates;
        foreach (var aggregate in aggregates.Values)
            aggregate.ResetRequirements();
        foreach (var row in settings.TableSummaries)
            PropertyAggregate.Require(aggregates, row.Columns);
        if (aggregates.Values.Any(a => !a.InUse)) // a summary was removed: rare, so LINQ is fine
        {
            foreach (var unused in aggregates.Values.Where(a => !a.InUse).ToList())
                aggregates.Remove(unused.PropertyName);
        }

        Accumulate(aggregates, view.ViewIndices, settings, ct);
        view.TableValuesCount = settings.Items.Count;

        foreach (var row in settings.TableSummaries)
        {
            var list = row.Position == SummaryPosition.Top ? view.TopSummaries : view.BottomSummaries;
            view.TableSummarySlots.Add(new TableSummarySlot(row.Name, row.Title, row.Columns, list, list.Count));
            list.Add(SummaryText.BuildRow(row.Name, row.Title, row.Columns,
                p => aggregates.TryGetValue(p, out var a) ? a : null, settings.Comparer.Culture, settings.Strings));
        }
    }

    /// <summary>
    /// One pass per aggregate over the items at <paramref name="sourceIndices"/>. The property need not have a
    /// column; without one it is read from the items directly. An aggregate that can follow a changed value keeps
    /// each row's value by source index (in a buffer reused across rebuilds).
    /// </summary>
    private static void Accumulate(Dictionary<string, PropertyAggregate> aggregates, List<int> sourceIndices,
        ViewBuildSettings settings, CancellationToken ct)
    {
        var items = settings.Items;
        int itemCount = items.Count;
        foreach (var aggregate in aggregates.Values)
        {
            var reader = settings.SummaryReaders[aggregate.PropertyName];
            aggregate.Reader = reader.Read;
            aggregate.Begin();

            object?[]? values = null;
            if (aggregate.CanUpdateInPlace && aggregate.DependsOnValues)
            {
                values = aggregate.Values is { } buffer && buffer.Length >= itemCount && buffer.Length <= 2 * itemCount + 16
                    ? buffer
                    : new object?[itemCount];
                Array.Fill(values, PropertyAggregate.NoRow);
            }
            aggregate.Values = values;

            for (int i = 0; i < sourceIndices.Count; i++)
            {
                if ((i & CheckMask) == 0) ct.ThrowIfCancellationRequested();
                int sourceIndex = sourceIndices[i];
                if (items[sourceIndex] is not { } item) continue;
                object? value = reader.Read(item);
                aggregate.Add(value, settings.Comparer);
                if (values != null) values[sourceIndex] = value;
            }
        }
    }

    // ── Lookups ──────────────────────────────────────────────────

    /// <summary>
    /// Build the row keys, the row-key index and (when a summary keeps row values) the item index, so publishing the
    /// view and mapping the selection through it cost no pass over the rows on the source's thread.
    /// </summary>
    private void BuildLookups()
    {
        var view = _view;
        int count = view.FlatView?.Count ?? view.ViewIndices.Count;
        var keys = new object?[count];
        for (int i = 0; i < count; i++)
        {
            Check(i);
            keys[i] = RowKeyOf(view, i);
        }
        view.RowKeys = keys;
        _ct.ThrowIfCancellationRequested();
        view.RowKeyIndex = RowKeys.Index(keys);

        bool keepsValues = view.InnermostGroup != null;
        foreach (var aggregate in view.TableAggregates.Values)
            keepsValues |= aggregate.Values != null;
        if (keepsValues)
        {
            _ct.ThrowIfCancellationRequested();
            view.SourceIndexByItem = SourceIndexMap(_items);
        }
    }

    /// <summary>Each item's index in <paramref name="items"/> (by reference; -1 for an item that appears more than once).</summary>
    public static Dictionary<object, int> SourceIndexMap(IList items)
    {
        var map = new Dictionary<object, int>(items.Count, ReferenceEqualityComparer.Instance);
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] is { } candidate && !map.TryAdd(candidate, i))
                map[candidate] = -1;
        }
        return map;
    }

    /// <summary>
    /// The key of one visible row of <paramref name="view"/>: the item for a data row, a <see cref="GroupRowKey"/> for
    /// a group header or group summary row; null when the row is out of range.
    /// </summary>
    public static object? RowKeyOf(ViewSnapshot view, int visibleRow)
    {
        var flat = view.FlatView;
        if (flat == null)
            return visibleRow >= 0 && visibleRow < view.ViewIndices.Count ? view.Items[view.ViewIndices[visibleRow]] : null;
        if (visibleRow < 0 || visibleRow >= flat.Count) return null;
        var row = flat[visibleRow];
        return row.Kind switch
        {
            FlatRowKind.Data => view.Items[row.SourceIndex],
            FlatRowKind.GroupHeader => new GroupRowKey(row.GroupInfo!.KeyPath!, -1),
            _ => new GroupRowKey(row.GroupInfo!.KeyPath!, row.GroupSummaryIndex),
        };
    }
}

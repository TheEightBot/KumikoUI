using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Layout;

/// <summary>
/// A programmatic scroll the next frame finishes with measured heights (§7.8): to <paramref name="Row"/> at
/// <paramref name="Position"/>, applied when the offset is still <paramref name="OffsetY"/> (otherwise the user scrolled).
/// </summary>
internal readonly record struct PendingRowScroll(int Row, KumikoScrollPosition Position, float OffsetY);

/// <summary>
/// Where every row of a source's flat view is, vertically (#320): the one row-geometry service the renderer, the hit
/// tester, the input controller, sticky group headers, row drag and <see cref="KumikoScroller"/> ask. Owned by the
/// <see cref="KumikoSource"/> (<see cref="KumikoSource.RowLayout"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Coordinate spaces.</b> <em>Content</em> space (<see cref="GetRowTop"/>, <see cref="GetRowAt"/>,
/// <see cref="GetTotalHeight"/>) starts at the top of row 0, frozen rows included. <em>Scroll</em> space
/// (<see cref="GetScrollTop"/>, <see cref="GetScrollRowAt"/>) is the space of <see cref="ScrollState.OffsetY"/>: 0 is the
/// top of the first row after the frozen rows. <em>Data-area</em> helpers (<see cref="GetRowY"/>,
/// <see cref="GetRowAtY"/>) take the screen y of the top of the rows (below the header and top summaries) and the
/// scroll offset, and place frozen rows at fixed positions and the others scrolled.
/// </para>
/// <para>
/// <b>Uniform path.</b> With <see cref="KumikoRowHeightMode.Fixed"/> and no <see cref="KumikoStyle.RowHeightResolver"/>
/// every row is <see cref="KumikoStyle.RowHeight"/> tall and every query is the arithmetic the grid always used, with no
/// per-row state. Otherwise the layout keeps per-row heights in two Fenwick trees (the sum of the known heights and the
/// count of the rows not measured yet, which use an estimate), so row to y and y to row are O(log n) and changing the
/// estimate is O(1). Sums are doubles; heights and the estimate are floats, so every sum is exact and independent of
/// the order it is computed in.
/// </para>
/// <para>
/// Heights are measured only by the renderer while it draws a frame (<c>KumikoRenderer</c>'s row measurer), once per
/// item, content version and column-width signature, and cached per item so they survive sorting, filtering and
/// grouping. Queries outside a frame never measure: they see the heights of the last frame, adjusted for view changes.
/// Group header and group summary rows are always <see cref="KumikoStyle.RowHeight"/> tall. Used on one thread, like
/// the source.
/// </para>
/// </remarks>
internal sealed class KumikoRowLayout
{
    private struct ItemHeight
    {
        public float Height;
        public int Stamp; // the measurement stamp the height was measured at; 0 when invalidated
    }

    // Marks a non-data row (group header or summary) in _stamps: always RowHeight tall, never measured.
    private const int NonDataRow = -1;

    /// <summary>A layout for code that has no source: always uniform, no frozen rows, no rows.</summary>
    internal static readonly KumikoRowLayout Detached = new(null);

    private readonly KumikoSource? _source;
    private readonly Dictionary<object, ItemHeight> _items = new(ReferenceEqualityComparer.Instance);

    // Per view row (non-uniform only): the known height (0: not measured, the estimate applies), the stamp it was
    // measured at, and the dirty generation at which its item's cache entry was last checked.
    private float[] _heights = Array.Empty<float>();
    private int[] _stamps = Array.Empty<int>();
    private int[] _checked = Array.Empty<int>();

    // Fenwick trees (1-based) over the rows: sum of known heights, count of unknown rows.
    private double[] _sumTree = Array.Empty<double>();
    private int[] _unknownTree = Array.Empty<int>();

    private int _count;
    private int _highBit;
    private bool _active;
    private bool _mapDirty = true;
    private bool _treeDirty = true;

    private int _stamp = 1;
    private int _dirtyGeneration;
    private double _measuredSum;
    private int _measuredCount;
    private float _estimate = 1f;

    // Moves on whenever a row top can have changed; the sequential top cache is valid only for its version.
    private int _version;
    private int _cacheRow = -1;
    private double _cacheTop;
    private int _cacheVersion = -1;

    // The style the snapshot was taken from, and the fields that affect heights.
    private KumikoStyle? _style;
    private int _styleVersion;
    private float _snapRowHeight = float.NaN;
    private KumikoRowHeightMode _snapMode;
    private float? _snapMin, _snapMax;
    private float _snapVerticalPadding, _snapCellPadding;
    private KumikoFont _snapFont;
    private KumikoRowHeightResolver? _snapResolver;

    // Column width signature: what of each column can change a measured height.
    private ColumnSignature[] _columnSignature = Array.Empty<ColumnSignature>();
    private int _columnSignatureCount = -1;

    private record struct ColumnSignature(bool Visible, float Width, KumikoTextWrap Wrap, KumikoFont? Font,
        float? Padding, ICellRenderer? Renderer);

    internal KumikoRowLayout(KumikoSource? source) => _source = source;

    /// <summary>
    /// Whether every row is <see cref="KumikoStyle.RowHeight"/> tall for <paramref name="style"/>:
    /// <see cref="KumikoRowHeightMode.Fixed"/> with no <see cref="KumikoStyle.RowHeightResolver"/> (or no source).
    /// </summary>
    public bool IsUniform(KumikoStyle style) =>
        _source == null || (style.RowHeightMode == KumikoRowHeightMode.Fixed && style.RowHeightResolver == null);

    /// <summary>The rows that do not scroll (<see cref="KumikoSource.EffectiveFrozenRowCount"/>).</summary>
    public int FrozenRowCount => _source?.EffectiveFrozenRowCount ?? 0;

    /// <summary>Whether per-row heights are being kept (the last query was for a non-uniform style).</summary>
    internal bool IsActive => _active;

    /// <summary>Whether the last non-uniform style had a <see cref="KumikoStyle.RowHeightResolver"/>.</summary>
    internal bool HasResolver => _snapResolver != null;

    /// <summary>
    /// A programmatic scroll to a row whose height was not measured yet (§7.8): the next frame measures around the
    /// row, scrolls to it again with real heights and clears it. A user scroll clears it too.
    /// </summary>
    internal PendingRowScroll? PendingScrollTarget { get; set; }

    // ── Content space ────────────────────────────────────────────

    /// <summary>The top of <paramref name="row"/> from the top of row 0 (frozen rows included).</summary>
    public double GetRowTop(int row, KumikoStyle style)
    {
        if (IsUniform(style)) return (double)row * style.RowHeight;
        EnsureIndex(style);
        return TopOf(row);
    }

    /// <summary>The height of <paramref name="row"/> as laid out now (the estimate for an unmeasured row).</summary>
    public float GetRowHeight(int row, KumikoStyle style)
    {
        if (IsUniform(style)) return style.RowHeight;
        EnsureIndex(style);
        return HeightOf(row);
    }

    /// <summary>
    /// The row containing content y <paramref name="y"/>: -1 above row 0, the row count or more below the last row. A y
    /// on the boundary between two rows belongs to the lower one.
    /// </summary>
    public int GetRowAt(double y, KumikoStyle style)
    {
        if (IsUniform(style))
        {
            float rowHeight = style.RowHeight;
            if (y < 0 || rowHeight <= 0) return y < 0 ? -1 : 0;
            return (int)Math.Min(int.MaxValue, Math.Floor(y / rowHeight));
        }
        EnsureIndex(style);
        return RowAt(y);
    }

    /// <summary>The height of all rows.</summary>
    public double GetTotalHeight(KumikoStyle style)
    {
        if (IsUniform(style)) return (double)(_source?.RowCount ?? 0) * style.RowHeight;
        EnsureIndex(style);
        return Prefix(_count);
    }

    /// <summary>The height of the frozen rows: the top of the first scrolling row.</summary>
    public float GetFrozenHeight(KumikoStyle style)
    {
        int frozen = FrozenRowCount;
        if (IsUniform(style)) return frozen * style.RowHeight;
        return (float)GetRowTop(frozen, style);
    }

    // ── Scroll space (ScrollState.OffsetY) ───────────────────────

    /// <summary>
    /// Where scrolling row <paramref name="row"/> starts in <see cref="ScrollState.OffsetY"/> space: the offset that puts
    /// it right below the frozen rows.
    /// </summary>
    public float GetScrollTop(int row, KumikoStyle style)
    {
        if (IsUniform(style)) return (row - FrozenRowCount) * style.RowHeight;
        EnsureIndex(style);
        int frozen = FrozenRowCount;
        return (float)(TopOf(row) - TopOf(frozen));
    }

    /// <summary>
    /// The scrolling row at <paramref name="scrollY"/> in <see cref="ScrollState.OffsetY"/> space (below the frozen
    /// rows): below <see cref="FrozenRowCount"/> above them, the row count or more below the last row.
    /// </summary>
    public int GetScrollRowAt(float scrollY, KumikoStyle style)
    {
        int frozen = FrozenRowCount;
        if (IsUniform(style))
        {
            float rowHeight = style.RowHeight;
            if (rowHeight <= 0) return frozen;
            return frozen + (int)MathF.Floor(scrollY / rowHeight);
        }
        EnsureIndex(style);
        return RowAt(TopOf(frozen) + scrollY);
    }

    // ── Data area (screen) ───────────────────────────────────────

    /// <summary>
    /// The screen y of the top of <paramref name="row"/> when the rows start at <paramref name="dataTop"/> (below the
    /// header and the top summaries): a frozen row at its fixed place, a scrolling row moved by
    /// <paramref name="offsetY"/>.
    /// </summary>
    public float GetRowY(int row, float dataTop, float offsetY, KumikoStyle style)
    {
        int frozen = FrozenRowCount;
        if (IsUniform(style))
        {
            float rowHeight = style.RowHeight;
            return row < frozen
                ? dataTop + row * rowHeight
                : dataTop + frozen * rowHeight + (row - frozen) * rowHeight - offsetY;
        }
        EnsureIndex(style);
        return row < frozen
            ? dataTop + (float)TopOf(row)
            : dataTop + (float)(TopOf(row) - offsetY);
    }

    /// <summary>
    /// The row at screen <paramref name="y"/> when the rows start at <paramref name="dataTop"/>: a frozen row over the
    /// frozen band (clamped to it), otherwise the scrolling row under y at <paramref name="offsetY"/>, which is the row
    /// count or more below the last row.
    /// </summary>
    public int GetRowAtY(float y, float dataTop, float offsetY, KumikoStyle style)
    {
        int frozen = FrozenRowCount;
        if (IsUniform(style))
        {
            float rowHeight = style.RowHeight;
            float frozenHeight = frozen * rowHeight;
            if (rowHeight <= 0) return 0;
            if (frozen > 0 && y < dataTop + frozenHeight)
                return Math.Clamp((int)MathF.Floor((y - dataTop) / rowHeight), 0, frozen - 1);
            return frozen + (int)MathF.Floor((y - (dataTop + frozenHeight) + offsetY) / rowHeight);
        }
        EnsureIndex(style);
        double frozenTop = TopOf(frozen);
        if (frozen > 0 && y < dataTop + frozenTop)
            return Math.Clamp(RowAt(y - dataTop), 0, frozen - 1);
        return Math.Max(frozen, RowAt((double)y - dataTop + offsetY));
    }

    /// <summary>
    /// The first and last scrolling rows that show in a viewport <paramref name="scrollingHeight"/> tall below the
    /// frozen rows at <paramref name="offsetY"/>, clamped to the rows; (0, -1) when none scrolls.
    /// </summary>
    public (int First, int Last) GetVisibleScrollingRows(float offsetY, float scrollingHeight, KumikoStyle style)
    {
        int rowCount = _source?.RowCount ?? 0;
        int frozen = FrozenRowCount;
        if (rowCount - frozen <= 0) return (0, -1);
        int first = Math.Max(frozen, GetScrollRowAt(offsetY, style));
        int last = Math.Min(rowCount - 1, GetScrollRowAt(offsetY + Math.Max(0, scrollingHeight), style));
        return (first, last);
    }

    // ── View changes (from the source) ───────────────────────────

    /// <summary>The view was rebuilt (sort, filter, grouping, expand/collapse, items): re-map heights before the next query.</summary>
    internal void OnViewRebuilt()
    {
        _mapDirty = true;
        _version++;
    }

    /// <summary><paramref name="count"/> rows were inserted at <paramref name="index"/> without a rebuild.</summary>
    internal void OnRowsInserted(int index, int count)
    {
        _version++;
        if (!_active || _mapDirty) return;
        if (index < 0 || index > _count || count <= 0)
        {
            _mapDirty = true;
            return;
        }
        EnsureCapacity(_count + count);
        int tail = _count - index;
        Array.Copy(_heights, index, _heights, index + count, tail);
        Array.Copy(_stamps, index, _stamps, index + count, tail);
        Array.Copy(_checked, index, _checked, index + count, tail);
        for (int i = index; i < index + count; i++)
        {
            _heights[i] = 0;
            _stamps[i] = 0;
            _checked[i] = _dirtyGeneration;
        }
        _count += count;
        _treeDirty = true;
    }

    /// <summary><paramref name="count"/> rows were removed at <paramref name="index"/> without a rebuild.</summary>
    internal void OnRowsRemoved(int index, int count)
    {
        _version++;
        if (!_active || _mapDirty) return;
        if (index < 0 || count <= 0 || index + count > _count)
        {
            _mapDirty = true;
            return;
        }
        int tail = _count - index - count;
        Array.Copy(_heights, index + count, _heights, index, tail);
        Array.Copy(_stamps, index + count, _stamps, index, tail);
        Array.Copy(_checked, index + count, _checked, index, tail);
        _count -= count;
        _treeDirty = true;
    }

    /// <summary>Re-measure the rows showing <paramref name="item"/> the next time they are drawn.</summary>
    internal void InvalidateItem(object item)
    {
        ref var entry = ref CollectionsMarshal.GetValueRefOrNullRef(_items, item);
        if (!Unsafe.IsNullRef(ref entry)) entry.Stamp = 0;
        _dirtyGeneration++;
    }

    /// <summary>Re-measure every row the next time it is drawn; measured heights stay in use until then.</summary>
    internal void InvalidateAll() => _stamp = NextStamp(_stamp);

    /// <summary>Forget the measured height of an item that left the collection.</summary>
    internal void ForgetItem(object item)
    {
        if (_items.Remove(item, out var entry) && entry.Height > 0)
        {
            _measuredSum -= entry.Height;
            _measuredCount--;
        }
    }

    /// <summary>Forget every measured height (the items were replaced).</summary>
    internal void Clear()
    {
        _items.Clear();
        _measuredSum = 0;
        _measuredCount = 0;
        _stamp = NextStamp(_stamp);
        _mapDirty = true;
        _version++;
    }

    // ── Frame protocol (renderer) ────────────────────────────────

    /// <summary>
    /// Starts a frame for <paramref name="style"/> and <paramref name="columns"/> (after their widths are resolved).
    /// Returns false for a uniform style, after releasing any per-row state; otherwise brings the index up to date and
    /// makes every height stale when something a measurement depends on changed (column widths, wrap modes, fonts).
    /// </summary>
    internal bool BeginFrame(KumikoStyle style, IReadOnlyList<KumikoColumn> columns)
    {
        if (IsUniform(style))
        {
            if (_active) Release();
            return false;
        }
        EnsureIndex(style);
        if (UpdateColumnSignature(columns)) InvalidateAll();
        return true;
    }

    /// <summary>The number of rows indexed (the source's row count after <see cref="BeginFrame"/>).</summary>
    internal int Count => _count;

    /// <summary>
    /// Whether <paramref name="row"/>'s height is measured and current. Non-data rows always are. Call after
    /// <see cref="BeginFrame"/>.
    /// </summary>
    internal bool IsCurrent(int row)
    {
        if ((uint)row >= (uint)_count) return true;
        int stamp = _stamps[row];
        if (stamp == NonDataRow) return true;
        if (stamp != _stamp) return false;
        if (_checked[row] != _dirtyGeneration)
        {
            _checked[row] = _dirtyGeneration;
            ref var entry = ref CollectionsMarshal.GetValueRefOrNullRef(_items, _source!.GetItem(row));
            if (Unsafe.IsNullRef(ref entry) || entry.Stamp != _stamp)
            {
                _stamps[row] = 0;
                return false;
            }
        }
        return true;
    }

    /// <summary>Whether <paramref name="row"/> is a data row whose height was measured at least once.</summary>
    internal bool IsMeasured(int row) => (uint)row < (uint)_count && _stamps[row] != NonDataRow && _heights[row] > 0;

    /// <summary>
    /// Records the height measured for data row <paramref name="row"/> showing <paramref name="item"/>, as current.
    /// Returns whether the row's laid-out height (or the estimate other rows use) changed.
    /// </summary>
    internal bool SetMeasuredHeight(int row, object item, float height)
    {
        if ((uint)row >= (uint)_count || _stamps[row] == NonDataRow) return false;
        if (!(height >= 1f)) height = 1f;
        if (float.IsPositiveInfinity(height)) height = float.MaxValue / 4;

        ref var entry = ref CollectionsMarshal.GetValueRefOrAddDefault(_items, item, out bool existed);
        if (existed && entry.Height > 0)
        {
            _measuredSum -= entry.Height;
            _measuredCount--;
        }
        entry.Height = height;
        entry.Stamp = _stamp;
        _measuredSum += height;
        _measuredCount++;

        _stamps[row] = _stamp;
        _checked[row] = _dirtyGeneration;

        bool changed = false;
        float old = _heights[row];
        if (old != height)
        {
            _heights[row] = height;
            if (!_treeDirty)
            {
                double deltaSum = old > 0 ? height - (double)old : height;
                int deltaUnknown = old > 0 ? 0 : -1;
                for (int i = row + 1; i <= _count; i += i & -i)
                {
                    _sumTree[i] += deltaSum;
                    _unknownTree[i] += deltaUnknown;
                }
            }
            changed = true;
        }
        if (UpdateEstimate()) changed = true;
        if (changed) _version++;
        return changed;
    }

    // ── Internals ────────────────────────────────────────────────

    private float HeightOf(int row)
    {
        if ((uint)row >= (uint)_count) return _estimate;
        float h = _heights[row];
        return h > 0 ? h : _estimate;
    }

    private double TopOf(int row)
    {
        if (row <= 0) return 0;
        if (row >= _count) return Prefix(_count) + (double)(row - _count) * _estimate;

        // Draw loops walk rows in order: the next row's top is the cached one plus a height, with no tree query.
        if (_cacheVersion == _version)
        {
            if (row == _cacheRow) return _cacheTop;
            if (row == _cacheRow + 1)
            {
                _cacheTop += HeightOf(_cacheRow);
                _cacheRow = row;
                return _cacheTop;
            }
        }
        double top = Prefix(row);
        _cacheRow = row;
        _cacheTop = top;
        _cacheVersion = _version;
        return top;
    }

    /// <summary>The sum of the heights of rows [0, <paramref name="row"/>).</summary>
    private double Prefix(int row)
    {
        if (row > _count) row = _count;
        double sum = 0;
        long unknown = 0;
        for (int i = row; i > 0; i -= i & -i)
        {
            sum += _sumTree[i];
            unknown += _unknownTree[i];
        }
        return sum + unknown * (double)_estimate;
    }

    private int RowAt(double y)
    {
        if (y < 0) return -1;
        int pos = 0;
        double remaining = y;
        double estimate = _estimate;
        for (int step = _highBit; step > 0; step >>= 1)
        {
            int next = pos + step;
            if (next > _count) continue;
            double span = _sumTree[next] + _unknownTree[next] * estimate;
            if (span <= remaining)
            {
                pos = next;
                remaining -= span;
            }
        }
        return pos;
    }

    private void EnsureIndex(KumikoStyle style)
    {
        int rowCount = _source!.RowCount; // publishes a pending view first (which marks the map dirty)
        SyncStyle(style);
        if (!_active || _mapDirty || rowCount != _count)
            Remap(rowCount, style);
        else if (_treeDirty)
            BuildTrees();
    }

    private void SyncStyle(KumikoStyle style)
    {
        if (ReferenceEquals(style, _style) && style.Version == _styleVersion) return;
        _style = style;
        _styleVersion = style.Version;

        bool rowHeightChanged = style.RowHeight != _snapRowHeight;
        if (!rowHeightChanged && style.RowHeightMode == _snapMode && style.MinRowHeight == _snapMin &&
            style.MaxRowHeight == _snapMax && style.CellVerticalPadding == _snapVerticalPadding &&
            style.CellPadding == _snapCellPadding && style.CellFont == _snapFont &&
            ReferenceEquals(style.RowHeightResolver, _snapResolver))
            return;

        _snapRowHeight = style.RowHeight;
        _snapMode = style.RowHeightMode;
        _snapMin = style.MinRowHeight;
        _snapMax = style.MaxRowHeight;
        _snapVerticalPadding = style.CellVerticalPadding;
        _snapCellPadding = style.CellPadding;
        _snapFont = style.CellFont;
        _snapResolver = style.RowHeightResolver;

        _stamp = NextStamp(_stamp);
        if (rowHeightChanged) _mapDirty = true; // group rows take the new height
        UpdateEstimate();
        _version++;
    }

    /// <summary>Recomputes the estimate for unmeasured rows; returns whether it changed.</summary>
    private bool UpdateEstimate()
    {
        float rowHeight = Math.Max(1f, _snapRowHeight);
        float estimate;
        if (_snapMode == KumikoRowHeightMode.Auto)
        {
            estimate = _measuredCount > 0
                ? (float)(_measuredSum / _measuredCount)
                : Math.Max(rowHeight, _snapMin ?? rowHeight);
        }
        else
        {
            estimate = rowHeight;
        }
        if (!(estimate >= 1f)) estimate = 1f;
        if (estimate == _estimate) return false;
        _estimate = estimate;
        return true;
    }

    private void Remap(int rowCount, KumikoStyle style)
    {
        EnsureCapacity(rowCount);
        var source = _source!;
        float groupRowHeight = Math.Max(1f, style.RowHeight);
        for (int row = 0; row < rowCount; row++)
        {
            _checked[row] = _dirtyGeneration;
            if (source.IsNonDataRow(row))
            {
                _heights[row] = groupRowHeight;
                _stamps[row] = NonDataRow;
                continue;
            }
            if (_items.TryGetValue(source.GetItem(row), out var entry))
            {
                _heights[row] = entry.Height;
                _stamps[row] = entry.Stamp;
            }
            else
            {
                _heights[row] = 0;
                _stamps[row] = 0;
            }
        }
        _count = rowCount;
        _active = true;
        _mapDirty = false;
        BuildTrees();
    }

    private void BuildTrees()
    {
        int n = _count;
        Array.Clear(_sumTree, 0, n + 1);
        Array.Clear(_unknownTree, 0, n + 1);
        for (int i = 1; i <= n; i++)
        {
            float h = _heights[i - 1];
            if (h > 0) _sumTree[i] += h;
            else _unknownTree[i] += 1;
            int parent = i + (i & -i);
            if (parent <= n)
            {
                _sumTree[parent] += _sumTree[i];
                _unknownTree[parent] += _unknownTree[i];
            }
        }
        _highBit = n == 0 ? 0 : 1 << (31 - System.Numerics.BitOperations.LeadingZeroCount((uint)n));
        _treeDirty = false;
        _version++;
    }

    private void EnsureCapacity(int rowCount)
    {
        if (_heights.Length >= rowCount && _sumTree.Length >= rowCount + 1) return;
        int capacity = Math.Max(16, _heights.Length);
        while (capacity < rowCount) capacity *= 2;
        Array.Resize(ref _heights, capacity);
        Array.Resize(ref _stamps, capacity);
        Array.Resize(ref _checked, capacity);
        _sumTree = new double[capacity + 1];
        _unknownTree = new int[capacity + 1];
        _treeDirty = true;
    }

    /// <summary>Drops the per-row arrays and measured heights: the style is uniform again.</summary>
    private void Release()
    {
        _heights = Array.Empty<float>();
        _stamps = Array.Empty<int>();
        _checked = Array.Empty<int>();
        _sumTree = Array.Empty<double>();
        _unknownTree = Array.Empty<int>();
        _items.Clear();
        _measuredSum = 0;
        _measuredCount = 0;
        _count = 0;
        _active = false;
        _mapDirty = true;
        _treeDirty = true;
        _style = null;
        _snapRowHeight = float.NaN;
        _snapResolver = null;
        _columnSignatureCount = -1;
        PendingScrollTarget = null;
        _version++;
    }

    private bool UpdateColumnSignature(IReadOnlyList<KumikoColumn> columns)
    {
        int count = columns.Count;
        bool changed = count != _columnSignatureCount;
        if (_columnSignature.Length < count)
        {
            Array.Resize(ref _columnSignature, Math.Max(count, _columnSignature.Length * 2));
            changed = true;
        }
        for (int i = 0; i < count; i++)
        {
            var col = columns[i];
            var signature = new ColumnSignature(col.IsVisible, col.Width, col.TextWrap, col.CellStyle?.Font,
                col.CellStyle?.Padding, col.CustomCellRenderer);
            if (changed || _columnSignature[i] != signature)
            {
                _columnSignature[i] = signature;
                changed = true;
            }
        }
        _columnSignatureCount = count;
        return changed;
    }

    private static int NextStamp(int stamp)
    {
        stamp++;
        return stamp <= 0 ? 1 : stamp;
    }
}

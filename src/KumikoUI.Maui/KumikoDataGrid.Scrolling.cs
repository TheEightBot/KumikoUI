using KumikoUI.Core.Layout;
using KumikoUI.Core.Models;

namespace KumikoUI.Maui;

/// <summary>
/// The programmatic scrolling surface of <see cref="KumikoDataGrid"/> (#56): <see cref="ScrollToRow"/>,
/// <see cref="ScrollToItem"/> and <see cref="ScrollToColumn(int, KumikoScrollPosition)"/>, the two-way
/// <see cref="VerticalOffset"/> and <see cref="HorizontalOffset"/>, and the <see cref="Scrolled"/> event. The
/// geometry lives in Core's <see cref="KumikoScroller"/>; the properties only mirror the Core
/// <see cref="ScrollState"/>, and a flag keeps the two directions from feeding back into each other.
/// </summary>
public partial class KumikoDataGrid
{
    private bool _pushingScrollState;

    private void InitializeScrolling() => _scroll.ScrollChanged += OnScrollStateChanged;

    // ── Offsets ──

    /// <summary>Identifies the <see cref="VerticalOffset"/> bindable property.</summary>
    public static readonly BindableProperty VerticalOffsetProperty = BindableProperty.Create(
        nameof(VerticalOffset), typeof(double), typeof(KumikoDataGrid), 0d, BindingMode.TwoWay,
        propertyChanged: (b, _, n) => ((KumikoDataGrid)b).OnOffsetPropertyChanged(horizontal: false, (double)n));

    /// <summary>Identifies the <see cref="HorizontalOffset"/> bindable property.</summary>
    public static readonly BindableProperty HorizontalOffsetProperty = BindableProperty.Create(
        nameof(HorizontalOffset), typeof(double), typeof(KumikoDataGrid), 0d, BindingMode.TwoWay,
        propertyChanged: (b, _, n) => ((KumikoDataGrid)b).OnOffsetPropertyChanged(horizontal: true, (double)n));

    /// <summary>
    /// The vertical scroll offset in device-independent units, measured over the scrolling rows: 0 puts the first
    /// scrolling row right below the header, summaries and frozen rows. Two-way: it follows every scroll, and
    /// setting it (or its binding source) scrolls the grid, clamped to the scrollable range, after which the
    /// property reads the clamped value. Setting it before the grid's first layout is honored once the content has a
    /// size.
    /// </summary>
    public double VerticalOffset
    {
        get => _scroll.OffsetY;
        set => SetValue(VerticalOffsetProperty, value);
    }

    /// <summary>
    /// The horizontal scroll offset in device-independent units, measured over the scrolling columns, between the
    /// frozen ones. Two-way, clamped, like <see cref="VerticalOffset"/>.
    /// </summary>
    public double HorizontalOffset
    {
        get => _scroll.OffsetX;
        set => SetValue(HorizontalOffsetProperty, value);
    }

    private void OnOffsetPropertyChanged(bool horizontal, double value)
    {
        if (_pushingScrollState) return;

        // A NaN or infinite value from a binding is not a position.
        float v = double.IsFinite(value) ? (float)value : 0f;
        KumikoScroller.RefreshContentExtents(_scroll, _dataSource, _style);
        if (horizontal) _scroll.ScrollTo(v, _scroll.OffsetY);
        else _scroll.ScrollTo(_scroll.OffsetX, v);
        InvalidateSurface();

        // Write the clamped value back so the property (and its binding source) never claims an unreachable offset.
        PushScrollState();
    }

    private void OnScrollStateChanged(object? sender, KumikoScrollChangedEventArgs e)
    {
        PushScrollState();
        if (Scrolled is { } handler)
        {
            handler(this, new KumikoScrolledEventArgs(e.OffsetX, e.OffsetY, e.OldOffsetX, e.OldOffsetY,
                _scroll.ContentWidth, _scroll.ContentHeight, _scroll.ViewportWidth, _scroll.ViewportHeight));
        }
    }

    private void PushScrollState()
    {
        if (_pushingScrollState) return;
        _pushingScrollState = true;
        try
        {
            // Only the property whose value differs is written, so a scroll that moves one axis boxes one double.
            if (!((double)GetValue(VerticalOffsetProperty)).Equals(_scroll.OffsetY))
                SetValue(VerticalOffsetProperty, (double)_scroll.OffsetY);
            if (!((double)GetValue(HorizontalOffsetProperty)).Equals(_scroll.OffsetX))
                SetValue(HorizontalOffsetProperty, (double)_scroll.OffsetX);
        }
        finally { _pushingScrollState = false; }
    }

    // ── Event ──

    /// <summary>
    /// Raised after the grid scrolled, by the user, inertia, the keyboard or code, once per change of
    /// <see cref="VerticalOffset"/> or <see cref="HorizontalOffset"/> (including the clamp when the content or
    /// viewport shrinks). The event data carries the new and old offsets and the content and viewport sizes, which
    /// is enough to load more rows when the user nears the end. Raised on the UI thread; nothing is allocated for
    /// it while nobody listens.
    /// </summary>
    public event EventHandler<KumikoScrolledEventArgs>? Scrolled;

    // ── ScrollTo ──

    /// <summary>
    /// Scrolls so the row at <paramref name="row"/> (a row of the current view, group header and summary rows
    /// included) lands at <paramref name="position"/>. A frozen row needs no scroll. Targets are clamped to the
    /// scrollable range and respect the sticky group headers, header, summaries and frozen rows. Call it on the UI
    /// thread; scrolling before the first layout is honored once the grid has a size.
    /// </summary>
    /// <param name="row">The row index in the current view.</param>
    /// <param name="position">Where the row lands; <see cref="KumikoScrollPosition.Nearest"/> by default.</param>
    /// <returns>False, scrolling nothing, when <paramref name="row"/> is out of range.</returns>
    public bool ScrollToRow(int row, KumikoScrollPosition position = KumikoScrollPosition.Nearest) =>
        FinishScroll(KumikoScroller.ScrollToRow(_scroll, _dataSource, _style, row, position));

    /// <summary>
    /// Scrolls to <paramref name="item"/>, finding its row in the current sorted, filtered and grouped view, so it
    /// works after a sort, filter or grouping change. An item inside a collapsed group expands the group (and any
    /// collapsed group around it) first, as a tap on its header would, raising the group expand events; pass
    /// <paramref name="expandGroups"/> false to leave collapsed groups alone, which makes such an item not found.
    /// </summary>
    /// <param name="item">The item to bring into view.</param>
    /// <param name="position">Where its row lands; <see cref="KumikoScrollPosition.Nearest"/> by default.</param>
    /// <param name="expandGroups">Whether to expand collapsed groups that hold the item.</param>
    /// <returns>False, scrolling nothing, when the item has no row: it is not in the data, a filter hides it, or an
    /// expansion was canceled.</returns>
    public bool ScrollToItem(object item, KumikoScrollPosition position = KumikoScrollPosition.Nearest, bool expandGroups = true) =>
        FinishScroll(KumikoScroller.ScrollToItem(_scroll, _dataSource, _style, item, position, expandGroups));

    /// <summary>
    /// Scrolls so the column at <paramref name="columnIndex"/> (an index into the column list, hidden columns
    /// included) lands at <paramref name="position"/> between the frozen columns. A frozen column needs no scroll,
    /// unless the grid is too narrow to pin it (<see cref="KumikoStyle.MaxFrozenColumnsWidthRatio"/>): then it scrolls
    /// like the others.
    /// </summary>
    /// <param name="columnIndex">The index in the column list.</param>
    /// <param name="position">Where the column lands; <see cref="KumikoScrollPosition.Nearest"/> by default.</param>
    /// <returns>False, scrolling nothing, when the index is out of range or the column is hidden.</returns>
    public bool ScrollToColumn(int columnIndex, KumikoScrollPosition position = KumikoScrollPosition.Nearest) =>
        FinishScroll(KumikoScroller.ScrollToColumn(_scroll, _dataSource, _style, columnIndex, position));

    /// <summary>Scrolls so <paramref name="column"/> lands at <paramref name="position"/>; see <see cref="ScrollToColumn(int, KumikoScrollPosition)"/>.</summary>
    /// <param name="column">A column of this grid.</param>
    /// <param name="position">Where the column lands; <see cref="KumikoScrollPosition.Nearest"/> by default.</param>
    /// <returns>False, scrolling nothing, when the column is not in this grid or is hidden.</returns>
    public bool ScrollToColumn(KumikoColumn column, KumikoScrollPosition position = KumikoScrollPosition.Nearest)
    {
        ArgumentNullException.ThrowIfNull(column);
        var columns = _dataSource.Columns;
        for (int i = 0; i < columns.Count; i++)
            if (ReferenceEquals(columns[i], column)) return ScrollToColumn(i, position);
        return false;
    }

    private bool FinishScroll(bool found)
    {
        if (found) InvalidateSurface();
        return found;
    }
}

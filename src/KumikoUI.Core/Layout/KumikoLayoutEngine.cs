namespace KumikoUI.Core.Layout;

using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

/// <summary>
/// Computes column widths (star sizing), total content dimensions,
/// and determines visible row/column ranges for virtualized rendering.
/// </summary>
public class KumikoLayoutEngine
{
    // Header room for the glyphs a header can show beside its text: the sort arrow (8) and its gap (4), and the
    // filter icon (10) and its gap (4). The renderer reserves the same space while it draws them.
    internal const float SortGlyphSpace = 12f;
    internal const float FilterGlyphSpace = 14f;

    // Slack in a header-fit width, so rounding and anti-aliasing never ellipsize a header that just fits.
    internal const float HeaderFitMargin = 4f;

    // The paint headers are measured with; only its font is set, just before each measurement.
    private readonly KumikoPaint _headerMeasurePaint = new() { IsAntiAlias = true };

    // Star columns held at their minimum or maximum during distribution; reused every pass.
    private bool[] _starSettled = Array.Empty<bool>();

    /// <summary>
    /// Measures the headers that need it (see <see cref="UpdateHeaderFitWidths"/>), then recalculates column widths
    /// for the available viewport width. The renderer calls this every frame; with unchanged headers it measures
    /// nothing and allocates nothing.
    /// </summary>
    public void ComputeColumnWidths(IReadOnlyList<KumikoColumn> columns, float viewportWidth,
        IDrawingContext measuringContext, KumikoStyle style)
    {
        UpdateHeaderFitWidths(columns, measuringContext, style);
        ComputeColumnWidths(columns, viewportWidth);
    }

    /// <summary>
    /// Recalculate column widths based on the available viewport width, using each column's last measured
    /// <see cref="KumikoColumn.HeaderFitWidth"/>.
    /// </summary>
    /// <remarks>
    /// <para>A Fixed or Auto column with an explicit <see cref="KumikoColumn.Width"/>
    /// (<see cref="KumikoColumn.HasExplicitWidth"/>) keeps it, and a Star column with one is held only to its
    /// <see cref="KumikoColumn.MinWidth"/> and <see cref="KumikoColumn.MaxWidth"/>; a header that does not fit is
    /// truncated with an ellipsis.</para>
    /// <para>A column without one is never narrower than its header-fit width, unless its
    /// <see cref="KumikoColumn.MaxWidth"/> is smaller: then the maximum wins and the header may be truncated. Fixed
    /// and Auto columns without one are <see cref="KumikoColumn.DefaultWidth"/> wide, or their header-fit width
    /// when that is wider, within their minimum and maximum.</para>
    /// <para>Star columns share what Fixed and Auto columns leave of the viewport by weight. A column whose share
    /// is below its minimum (or above its maximum) is held there and the rest is shared again among the others.
    /// When the viewport is too narrow for every column's minimum, columns keep their minimums and the content is
    /// wider than the viewport, so the grid scrolls horizontally instead of truncating headers.</para>
    /// </remarks>
    public void ComputeColumnWidths(IReadOnlyList<KumikoColumn> columns, float viewportWidth)
    {
        float fixedTotal = 0f;
        bool hasStar = false;

        // Indexed loops: foreach over an IReadOnlyList boxes its enumerator, and this runs every frame.
        for (int i = 0; i < columns.Count; i++)
        {
            var col = columns[i];
            if (!col.IsVisible) continue;

            if (col.SizeMode == ColumnSizeMode.Star)
            {
                hasStar = true;
                continue;
            }

            if (!col.HasExplicitWidth)
                col.SetLayoutWidth(Clamp(KumikoColumn.DefaultWidth, EffectiveMinWidth(col), col.MaxWidth));
            fixedTotal += col.Width;
        }

        if (hasStar)
            DistributeStarSpace(columns, Math.Max(0, viewportWidth - fixedTotal));
    }

    /// <summary>
    /// The narrowest <paramref name="column"/> may be laid out: its <see cref="KumikoColumn.MinWidth"/> and, without
    /// an explicit width, its <see cref="KumikoColumn.HeaderFitWidth"/> capped at its <see cref="KumikoColumn.MaxWidth"/>.
    /// </summary>
    public static float EffectiveMinWidth(KumikoColumn column) =>
        column.HasExplicitWidth
            ? column.MinWidth
            : Math.Max(column.MinWidth, Math.Min(column.HeaderFitWidth, column.MaxWidth));

    // Math.Clamp, except that a minimum above the maximum wins instead of throwing.
    private static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(value, max));

    /// <summary>
    /// Shares <paramref name="space"/> among the visible Star columns by weight, holding a column at its minimum or
    /// maximum when its share falls outside them and sharing what is left among the others again.
    /// </summary>
    private void DistributeStarSpace(IReadOnlyList<KumikoColumn> columns, float space)
    {
        int count = columns.Count;
        if (_starSettled.Length < count)
            _starSettled = new bool[Math.Max(count, _starSettled.Length * 2)];
        Array.Clear(_starSettled, 0, count);

        // No weight to share by (every Star column weighs 0): leave their widths as they are
        float totalWeight = 0f;
        for (int i = 0; i < count; i++)
            if (columns[i].IsVisible && columns[i].SizeMode == ColumnSizeMode.Star)
                totalWeight += Math.Max(0f, columns[i].StarWeight);
        if (totalWeight <= 0f) return;

        float remaining = space;

        // Each pass either holds at least one column at a bound or sizes all the others and stops.
        while (true)
        {
            float weights = 0f;
            for (int i = 0; i < count; i++)
                if (IsOpenStar(columns[i], i))
                    weights += Math.Max(0f, columns[i].StarWeight);

            // Total of (bounded share - share): positive when shares fall below minimums, negative above maximums.
            float violation = 0f;
            for (int i = 0; i < count; i++)
            {
                var col = columns[i];
                if (!IsOpenStar(col, i)) continue;
                float share = StarShare(col, remaining, weights);
                violation += Clamp(share, EffectiveMinWidth(col), col.MaxWidth) - share;
            }

            if (Math.Abs(violation) < 0.01f) // nothing (beyond sub-pixel rounding) to hold at a bound
            {
                for (int i = 0; i < count; i++)
                {
                    var col = columns[i];
                    if (IsOpenStar(col, i))
                        col.SetLayoutWidth(Clamp(StarShare(col, remaining, weights), EffectiveMinWidth(col), col.MaxWidth));
                }
                return;
            }

            // Hold the columns that violate in the direction of the total at their bound, then share the rest again.
            for (int i = 0; i < count; i++)
            {
                var col = columns[i];
                if (!IsOpenStar(col, i)) continue;
                float share = StarShare(col, remaining, weights);
                float bounded = Clamp(share, EffectiveMinWidth(col), col.MaxWidth);
                if (violation > 0f ? bounded > share : bounded < share)
                {
                    col.SetLayoutWidth(bounded);
                    _starSettled[i] = true;
                    remaining -= bounded;
                }
            }
        }
    }

    private bool IsOpenStar(KumikoColumn col, int index) =>
        col.IsVisible && col.SizeMode == ColumnSizeMode.Star && !_starSettled[index];

    private static float StarShare(KumikoColumn col, float space, float weights) =>
        weights > 0f ? space * (Math.Max(0f, col.StarWeight) / weights) : 0f;

    /// <summary>
    /// Updates <see cref="KumikoColumn.HeaderFitWidth"/> of every visible column. A header's text is measured with
    /// <paramref name="measuringContext"/> only when the text, the header font instance or one of the font's
    /// properties changed since it was last measured; otherwise the cached width is reused.
    /// </summary>
    public void UpdateHeaderFitWidths(IReadOnlyList<KumikoColumn> columns, IDrawingContext measuringContext,
        KumikoStyle style)
    {
        for (int i = 0; i < columns.Count; i++)
        {
            var col = columns[i];
            if (col.IsVisible)
                col.HeaderFitWidth = MeasureHeaderFitWidth(col, measuringContext, style);
        }
    }

    /// <summary>
    /// The narrowest width that shows <paramref name="column"/>'s whole header: its text measured in the header
    /// font (the <see cref="KumikoCellStyle.Font"/> of its <see cref="KumikoColumn.HeaderCellStyle"/>, else
    /// <see cref="KumikoStyle.HeaderFont"/>), the header padding on both sides, room for the sort arrow when the
    /// column can be sorted (whether or not it is sorted now, so sorting it later does not truncate the header) and
    /// for the filter icon when it can be filtered, and a small margin. The text measurement is cached on the column.
    /// </summary>
    public float MeasureHeaderFitWidth(KumikoColumn column, IDrawingContext measuringContext, KumikoStyle style)
    {
        var headerStyle = column.HeaderCellStyle;
        var font = headerStyle?.Font ?? style.HeaderFont;
        float padding = headerStyle?.Padding ?? style.HeaderPadding;
        string header = column.Header ?? string.Empty;

        var measured = column.HeaderMeasure;
        if (!measured.IsCurrentFor(header, font))
        {
            float textWidth = 0f;
            if (header.Length > 0)
            {
                _headerMeasurePaint.Font = font;
                textWidth = measuringContext.MeasureText(header, _headerMeasurePaint).Width;
            }
            measured.Store(header, font, textWidth);
        }

        float glyphs = (column.AllowSorting ? SortGlyphSpace : 0f) + (column.AllowFiltering ? FilterGlyphSpace : 0f);
        return MathF.Ceiling(measured.TextWidth) + padding * 2 + glyphs + HeaderFitMargin;
    }

    /// <summary>
    /// Calculate total content width from visible columns.
    /// </summary>
    public float GetContentWidth(IReadOnlyList<KumikoColumn> columns)
    {
        float w = 0;
        for (int i = 0; i < columns.Count; i++)
            if (columns[i].IsVisible) w += columns[i].Width;
        return w;
    }

    /// <summary>
    /// Calculate the total width of frozen (pinned left) columns.
    /// </summary>
    public float GetFrozenColumnWidth(IReadOnlyList<KumikoColumn> columns)
    {
        float w = 0;
        for (int i = 0; i < columns.Count; i++)
            if (columns[i].IsVisible && columns[i].IsFrozen) w += columns[i].Width;
        return w;
    }

    /// <summary>
    /// Calculate the total width of right-frozen (pinned right) columns.
    /// </summary>
    public float GetRightFrozenColumnWidth(IReadOnlyList<KumikoColumn> columns)
    {
        float w = 0;
        for (int i = 0; i < columns.Count; i++)
            if (columns[i].IsVisible && columns[i].IsFrozenRight) w += columns[i].Width;
        return w;
    }

    /// <summary>
    /// Get only the frozen columns with their screen X positions.
    /// Frozen columns are always drawn starting at X=0,
    /// in the order they appear in the columns list.
    /// </summary>
    public List<(KumikoColumn Column, float X, int OriginalIndex)> GetVisibleFrozenColumns(
        IReadOnlyList<KumikoColumn> columns)
    {
        var result = new List<(KumikoColumn, float, int)>();
        PopulateVisibleFrozenColumns(columns, result);
        return result;
    }

    /// <summary>
    /// Populate an existing list with frozen columns (avoids per-frame allocation).
    /// </summary>
    public void PopulateVisibleFrozenColumns(
        IReadOnlyList<KumikoColumn> columns,
        List<(KumikoColumn Column, float X, int OriginalIndex)> result)
    {
        result.Clear();
        float x = 0;
        for (int i = 0; i < columns.Count; i++)
        {
            var col = columns[i];
            if (!col.IsVisible) continue;
            if (!col.IsFrozen) continue;

            result.Add((col, x, i));
            x += col.Width;
        }
    }

    /// <summary>
    /// Get only the right-frozen columns with their screen X positions.
    /// Right-frozen columns are drawn at the right edge of the viewport.
    /// X positions are relative to the right-frozen pane (0 = left edge of right-frozen area).
    /// </summary>
    public List<(KumikoColumn Column, float X, int OriginalIndex)> GetVisibleRightFrozenColumns(
        IReadOnlyList<KumikoColumn> columns, float viewportWidth)
    {
        var result = new List<(KumikoColumn, float, int)>();
        PopulateVisibleRightFrozenColumns(columns, viewportWidth, result);
        return result;
    }

    /// <summary>
    /// Populate an existing list with right-frozen columns (avoids per-frame allocation).
    /// </summary>
    public void PopulateVisibleRightFrozenColumns(
        IReadOnlyList<KumikoColumn> columns, float viewportWidth,
        List<(KumikoColumn Column, float X, int OriginalIndex)> result)
    {
        result.Clear();
        float rightFrozenWidth = GetRightFrozenColumnWidth(columns);
        float rightFrozenStart = viewportWidth - rightFrozenWidth;

        float x = rightFrozenStart;
        for (int i = 0; i < columns.Count; i++)
        {
            var col = columns[i];
            if (!col.IsVisible) continue;
            if (!col.IsFrozenRight) continue;

            result.Add((col, x, i));
            x += col.Width;
        }
    }

    /// <summary>
    /// Get only the scrollable (non-frozen) columns that are currently visible
    /// within the viewport, taking the frozen area widths into account.
    /// Returns content X positions (not screen positions).
    /// </summary>
    public List<(KumikoColumn Column, float X, int OriginalIndex)> GetVisibleScrollableColumns(
        IReadOnlyList<KumikoColumn> columns, ScrollState scroll, float frozenWidth, float rightFrozenWidth = 0)
    {
        var result = new List<(KumikoColumn, float, int)>();
        PopulateVisibleScrollableColumns(columns, scroll, frozenWidth, rightFrozenWidth, result);
        return result;
    }

    /// <summary>
    /// Populate an existing list with visible scrollable columns (avoids per-frame allocation).
    /// </summary>
    public void PopulateVisibleScrollableColumns(
        IReadOnlyList<KumikoColumn> columns, ScrollState scroll, float frozenWidth,
        float rightFrozenWidth,
        List<(KumikoColumn Column, float X, int OriginalIndex)> result)
    {
        result.Clear();

        // scrollable columns start after the frozen in content space
        float x = frozenWidth;
        float adjustedOffsetX = scroll.OffsetX + frozenWidth;
        float viewportRight = scroll.OffsetX + scroll.ViewportWidth - rightFrozenWidth;

        for (int i = 0; i < columns.Count; i++)
        {
            var col = columns[i];
            if (!col.IsVisible || col.IsFrozen || col.IsFrozenRight) continue;

            float colRight = x + col.Width;

            if (colRight > adjustedOffsetX && x < viewportRight)
                result.Add((col, x, i));

            x += col.Width;
        }
    }

    /// <summary>
    /// Calculate total content height from row count and style.
    /// </summary>
    public float GetContentHeight(int rowCount, KumikoStyle style)
    {
        return style.HeaderHeight + rowCount * style.RowHeight;
    }

    /// <summary>
    /// Calculate total height of frozen rows.
    /// </summary>
    public float GetFrozenRowHeight(int frozenRowCount, KumikoStyle style)
    {
        return frozenRowCount * style.RowHeight;
    }

    /// <summary>
    /// Determine the visible row range given the scroll state.
    /// Returns (firstVisibleRow, lastVisibleRow) inclusive.
    /// </summary>
    public (int First, int Last) GetVisibleRowRange(
        ScrollState scroll, KumikoStyle style, int totalRows)
    {
        return GetVisibleRowRangeWithOffset(scroll, style, totalRows, 0, 0);
    }

    /// <summary>
    /// Determine the visible row range given the scroll state and an additional offset
    /// (such as top summary row height) between the header and the data area.
    /// frozenRowCount rows are excluded from the scrollable range (they are drawn separately).
    /// </summary>
    public (int First, int Last) GetVisibleRowRangeWithOffset(
        ScrollState scroll, KumikoStyle style, int totalRows, float topOffset, int frozenRowCount = 0)
    {
        if (totalRows == 0) return (0, -1);

        int effectiveFrozen = Math.Min(frozenRowCount, totalRows);
        int scrollableRowCount = totalRows - effectiveFrozen;
        if (scrollableRowCount <= 0) return (0, -1); // all rows are frozen, none to scroll

        float frozenRowHeight = effectiveFrozen * style.RowHeight;
        float headerBottom = style.HeaderHeight + topOffset + frozenRowHeight;
        float scrollY = scroll.OffsetY;
        float viewportBottom = scrollY + scroll.ViewportHeight;

        // First row whose bottom edge is below the data area's top: row r spans
        // [headerBottom + r*RowHeight - scrollY, +RowHeight), so r = floor(scrollY / RowHeight).
        int first = Math.Max(0, (int)(scrollY / style.RowHeight));
        int last = Math.Min(scrollableRowCount - 1,
            (int)((viewportBottom - headerBottom) / style.RowHeight));

        // Map back to absolute row indices (offset by frozen row count)
        return (first + effectiveFrozen, last + effectiveFrozen);
    }

    /// <summary>
    /// Determine visible columns given horizontal scroll offset and viewport width.
    /// Returns list of (column, xOffset) pairs.
    /// </summary>
    public List<(KumikoColumn Column, float X)> GetVisibleColumns(
        IReadOnlyList<KumikoColumn> columns, ScrollState scroll)
    {
        var result = new List<(KumikoColumn, float)>();
        float x = 0;

        foreach (var col in columns)
        {
            if (!col.IsVisible)
                continue;

            float colRight = x + col.Width;

            // Column is at least partially visible
            if (colRight > scroll.OffsetX && x < scroll.OffsetX + scroll.ViewportWidth)
                result.Add((col, x));

            x += col.Width;
        }

        return result;
    }

    /// <summary>
    /// Get the column at a given X position (in content coordinates, not viewport).
    /// </summary>
    public (KumikoColumn? Column, int Index) HitTestColumn(
        IReadOnlyList<KumikoColumn> columns, float contentX)
    {
        float x = 0;
        for (int i = 0; i < columns.Count; i++)
        {
            var col = columns[i];
            if (!col.IsVisible) continue;

            if (contentX >= x && contentX < x + col.Width)
                return (col, i);

            x += col.Width;
        }
        return (null, -1);
    }

    /// <summary>
    /// Get the row index under a given Y position (in content coordinates).
    /// Returns -1 for the header area, -2 for out of bounds.
    /// </summary>
    public int HitTestRow(float contentY, KumikoStyle style, int totalRows)
    {
        if (contentY < style.HeaderHeight) return -1;
        int row = (int)((contentY - style.HeaderHeight) / style.RowHeight);
        return row < totalRows ? row : -2;
    }

    /// <summary>
    /// Get the X position of a column's left edge.
    /// </summary>
    public float GetColumnX(IReadOnlyList<KumikoColumn> columns, int columnIndex)
    {
        float x = 0;
        for (int i = 0; i < columnIndex && i < columns.Count; i++)
        {
            if (columns[i].IsVisible)
                x += columns[i].Width;
        }
        return x;
    }

    /// <summary>
    /// Calculate the optimal auto-fit width for a column based on its content: the wider of its header-fit width
    /// (see <see cref="MeasureHeaderFitWidth"/>, so auto-fit never truncates the header) and its widest cell
    /// display text plus padding, clamped to MinWidth/MaxWidth.
    /// </summary>
    public float CalculateAutoFitWidth(
        KumikoColumn column,
        KumikoSource dataSource,
        IDrawingContext ctx,
        KumikoStyle style)
    {
        float headerWidth = MeasureHeaderFitWidth(column, ctx, style);
        float maxWidth = 0;

        // Measure all visible row cell text
        var cellPaint = new KumikoPaint
        {
            Font = style.CellFont,
            IsAntiAlias = true
        };

        int rowCount = dataSource.RowCount;
        for (int row = 0; row < rowCount; row++)
        {
            var displayText = dataSource.GetCellDisplayText(row, column);
            if (string.IsNullOrEmpty(displayText)) continue;

            var textSize = ctx.MeasureText(displayText, cellPaint);
            if (textSize.Width > maxWidth)
                maxWidth = textSize.Width;
        }

        // Cell text: padding on both sides plus a small buffer
        float optimalWidth = Math.Max(headerWidth, maxWidth + style.CellPadding * 2 + 16);

        return Clamp(optimalWidth, column.MinWidth, column.MaxWidth);
    }
}

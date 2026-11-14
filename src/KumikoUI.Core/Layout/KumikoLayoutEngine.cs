namespace KumikoUI.Core.Layout;

using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

/// <summary>
/// Computes column widths (star sizing), total content dimensions,
/// and determines visible row/column ranges for virtualized rendering.
/// </summary>
public sealed class KumikoLayoutEngine
{
    // Header room for the glyphs a header can show beside its text: the sort arrow (8) and its gap (4), and the
    // filter icon (10) and its gap (4). The renderer reserves the same space while it draws them.
    internal const float SortGlyphSpace = 12f;
    internal const float FilterGlyphSpace = 14f;

    // Slack in a header-fit width, so rounding and anti-aliasing never ellipsize a header that just fits.
    internal const float HeaderFitMargin = 4f;

    // The paint headers are measured with; only its font is set, just before each measurement.
    private readonly KumikoPaint _headerMeasurePaint = new() { IsAntiAlias = true };

    // Room auto-fit leaves beside the widest cell text, on top of the cell padding on both sides.
    internal const float AutoFitTextBuffer = 16f;

    // The paint cells are measured with for auto-fit; only its font is set, just before each measurement.
    private readonly KumikoPaint _cellMeasurePaint = new() { IsAntiAlias = true };

    // Star columns held at their minimum or maximum during distribution; reused every pass.
    private bool[] _starSettled = Array.Empty<bool>();

    /// <summary>
    /// How many rows at each end of the view auto-fit and <see cref="ColumnSizeMode.Auto"/> columns measure, on top
    /// of the rows on screen: 100 by default, so measuring costs the same on 100 rows as on 100 000. Zero measures
    /// only the rows on screen (and the header). A cell outside the sample that is wider than every sampled one is
    /// truncated with an ellipsis.
    /// </summary>
    public int AutoFitSampleRows { get; set; } = 100;

    /// <summary>
    /// Measures the headers that need it (see <see cref="UpdateHeaderFitWidths"/>), then recalculates column widths
    /// for the available viewport width. With unchanged headers it measures nothing and allocates nothing.
    /// <see cref="ColumnSizeMode.Auto"/> columns are not measured; the renderer uses the overload that takes the data
    /// source.
    /// </summary>
    public void ComputeColumnWidths(IReadOnlyList<KumikoColumn> columns, float viewportWidth,
        IDrawingContext measuringContext, KumikoStyle style)
    {
        UpdateHeaderFitWidths(columns, measuringContext, style);
        ComputeColumnWidths(columns, viewportWidth);
    }

    /// <summary>
    /// Measures the headers that need it (see <see cref="UpdateHeaderFitWidths"/>) and the
    /// <see cref="ColumnSizeMode.Auto"/> columns whose data changed (see <see cref="UpdateContentFitWidths"/>), then
    /// recalculates column widths for the available viewport width. The renderer calls this every frame; with
    /// unchanged headers and data it measures nothing and allocates nothing.
    /// </summary>
    /// <param name="columns">The columns to lay out.</param>
    /// <param name="viewportWidth">The width available to the columns.</param>
    /// <param name="measuringContext">Measures header and cell text.</param>
    /// <param name="style">The grid style (fonts and padding).</param>
    /// <param name="dataSource">The rows Auto columns are fitted to.</param>
    /// <param name="firstVisibleRow">The first row on screen, sampled with the rows at each end of the view.</param>
    /// <param name="lastVisibleRow">The last row on screen (inclusive); below <paramref name="firstVisibleRow"/> for none.</param>
    public void ComputeColumnWidths(IReadOnlyList<KumikoColumn> columns, float viewportWidth,
        IDrawingContext measuringContext, KumikoStyle style, KumikoSource dataSource, int firstVisibleRow,
        int lastVisibleRow)
    {
        UpdateHeaderFitWidths(columns, measuringContext, style);
        UpdateContentFitWidths(columns, dataSource, measuringContext, style, firstVisibleRow, lastVisibleRow);
        ComputeColumnWidths(columns, viewportWidth);
    }

    /// <summary>
    /// Recalculate column widths based on the available viewport width, using each column's last measured
    /// <see cref="KumikoColumn.HeaderFitWidth"/>.
    /// </summary>
    /// <remarks>
    /// <para>A Fixed column, or an Auto column not measured yet, with an explicit <see cref="KumikoColumn.Width"/>
    /// (<see cref="KumikoColumn.HasExplicitWidth"/>) keeps it, and a Star column with one is held only to its
    /// <see cref="KumikoColumn.MinWidth"/> and <see cref="KumikoColumn.MaxWidth"/>; a header that does not fit is
    /// truncated with an ellipsis.</para>
    /// <para>A column without one is never narrower than its header-fit width, unless its
    /// <see cref="KumikoColumn.MaxWidth"/> is smaller: then the maximum wins and the header may be truncated. Fixed
    /// and Auto columns without one are <see cref="KumikoColumn.DefaultWidth"/> wide, or their header-fit width
    /// when that is wider, within their minimum and maximum.</para>
    /// <para>An Auto column whose content has been measured (<see cref="KumikoColumn.ContentFitWidth"/> above zero,
    /// see <see cref="UpdateContentFitWidths"/>) is its content-fit width within its minimum and maximum, whether or
    /// not its width is explicit.</para>
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

            if (col.SizeMode == ColumnSizeMode.Auto && col.ContentFitWidth > 0f)
                col.SetLayoutWidth(Clamp(col.ContentFitWidth, col.MinWidth, col.MaxWidth));
            else if (!col.HasExplicitWidth)
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
    /// Updates <see cref="KumikoColumn.ContentFitWidth"/> of every visible <see cref="ColumnSizeMode.Auto"/> column
    /// (and sets it to zero for the other size modes). A column's cells are sampled with
    /// <paramref name="measuringContext"/> (see <see cref="CalculateAutoFitWidth(KumikoColumn, KumikoSource, IDrawingContext, KumikoStyle, int, int)"/>)
    /// only when it has not been measured yet, the source's rows or cell values changed since (any change that
    /// raises <see cref="KumikoSource.ViewChanged"/>), the column is laid out for another source, or its cell font
    /// changed; otherwise the cached width is reused. Scrolling alone measures nothing. Call
    /// <see cref="UpdateHeaderFitWidths"/> first: the content-fit width is never narrower than the header-fit width.
    /// </summary>
    /// <param name="columns">The columns to update.</param>
    /// <param name="dataSource">The rows to sample.</param>
    /// <param name="measuringContext">Measures the cell text.</param>
    /// <param name="style">The grid style (cell font and padding).</param>
    /// <param name="firstVisibleRow">The first row on screen.</param>
    /// <param name="lastVisibleRow">The last row on screen (inclusive); below <paramref name="firstVisibleRow"/> for none.</param>
    public void UpdateContentFitWidths(IReadOnlyList<KumikoColumn> columns, KumikoSource dataSource,
        IDrawingContext measuringContext, KumikoStyle style, int firstVisibleRow, int lastVisibleRow)
    {
        for (int i = 0; i < columns.Count; i++)
        {
            var col = columns[i];
            if (col.SizeMode != ColumnSizeMode.Auto)
            {
                col.ContentFitWidth = 0f;
                continue;
            }
            if (!col.IsVisible) continue;

            var font = CellFont(col, style);
            var measured = col.ContentMeasure;
            if (!measured.IsCurrentFor(dataSource, font))
                measured.Store(dataSource, font,
                    MeasureSampledCellText(col, dataSource, measuringContext, font, firstVisibleRow, lastVisibleRow));

            col.ContentFitWidth = ContentFit(col, col.HeaderFitWidth, measured.TextWidth, style);
        }
    }

    private static KumikoFont CellFont(KumikoColumn column, KumikoStyle style) => column.CellStyle?.Font ?? style.CellFont;

    // The wider of the header fit and the widest cell text with the cell padding on both sides and the buffer.
    private static float ContentFit(KumikoColumn column, float headerFit, float textWidth, KumikoStyle style) =>
        Math.Max(headerFit, textWidth + (column.CellStyle?.Padding ?? style.CellPadding) * 2 + AutoFitTextBuffer);

    /// <summary>
    /// The width of the widest display text among the sampled rows of <paramref name="column"/>: the first and last
    /// <see cref="AutoFitSampleRows"/> rows of the view and the rows from <paramref name="firstRow"/> to
    /// <paramref name="lastRow"/>, each measured once. Group header rows and empty cells are skipped.
    /// </summary>
    private float MeasureSampledCellText(KumikoColumn column, KumikoSource dataSource, IDrawingContext ctx,
        KumikoFont font, int firstRow, int lastRow)
    {
        int count = dataSource.RowCount;
        if (count == 0) return 0f;

        _cellMeasurePaint.Font = font;
        int sample = Math.Max(0, AutoFitSampleRows);
        int headEnd = Math.Min(sample, count) - 1;
        int tailStart = Math.Max(0, count - sample);
        int visibleStart = Math.Max(0, firstRow);
        int visibleEnd = Math.Min(lastRow, count - 1);

        // Ranges in order of their start, so rows already measured are skipped and none is measured twice.
        float widest = 0f;
        int next = 0;
        widest = MeasureRows(column, dataSource, ctx, 0, headEnd, ref next, widest);
        if (visibleStart <= tailStart)
        {
            widest = MeasureRows(column, dataSource, ctx, visibleStart, visibleEnd, ref next, widest);
            widest = MeasureRows(column, dataSource, ctx, tailStart, count - 1, ref next, widest);
        }
        else
        {
            widest = MeasureRows(column, dataSource, ctx, tailStart, count - 1, ref next, widest);
            widest = MeasureRows(column, dataSource, ctx, visibleStart, visibleEnd, ref next, widest);
        }
        return widest;
    }

    private float MeasureRows(KumikoColumn column, KumikoSource dataSource, IDrawingContext ctx,
        int start, int end, ref int next, float widest)
    {
        for (int row = Math.Max(start, next); row <= end; row++)
        {
            var displayText = dataSource.GetCellDisplayText(row, column);
            if (string.IsNullOrEmpty(displayText)) continue;

            float width = ctx.MeasureText(displayText, _cellMeasurePaint).Width;
            if (width > widest) widest = width;
        }
        if (end + 1 > next) next = end + 1;
        return widest;
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
    /// Calculate the total width of frozen (pinned left) columns. A left-frozen column the last frame could not pin
    /// because the viewport is too narrow (<see cref="KumikoStyle.MaxFrozenColumnsWidthRatio"/>) is a
    /// scrolling column and is not counted.
    /// </summary>
    public float GetFrozenColumnWidth(IReadOnlyList<KumikoColumn> columns) => FrozenPanes.GetWidths(columns).Left;

    /// <summary>
    /// Calculate the total width of right-frozen (pinned right) columns. A right-frozen column the last frame
    /// could not pin because the viewport is too narrow (<see cref="KumikoStyle.MaxFrozenColumnsWidthRatio"/>)
    /// is a scrolling column and is not counted.
    /// </summary>
    public float GetRightFrozenColumnWidth(IReadOnlyList<KumikoColumn> columns) => FrozenPanes.GetWidths(columns).Right;

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
            if (col.Pane != ColumnFreezeMode.Left) continue;

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
            if (col.Pane != ColumnFreezeMode.Right) continue;

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

        // In on-screen order: unpinned left-frozen columns, then the scrolling ones, then unpinned right-frozen ones.
        for (int pass = 0; pass < 3; pass++)
        {
            for (int i = 0; i < columns.Count; i++)
            {
                var col = columns[i];
                if (!col.IsVisible || FrozenPanes.ScrollPass(col) != pass) continue;

                float colRight = x + col.Width;

                if (colRight > adjustedOffsetX && x < viewportRight)
                    result.Add((col, x, i));

                x += col.Width;
            }
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
    /// Calculate the optimal auto-fit width for a column based on its content: the wider of its header-fit width
    /// (see <see cref="MeasureHeaderFitWidth"/>, so auto-fit never truncates the header) and its widest sampled cell
    /// display text plus padding, clamped to MinWidth/MaxWidth. Samples the first and last
    /// <see cref="AutoFitSampleRows"/> rows of the view, never every row; the overload that takes the visible rows
    /// also measures the rows on screen.
    /// </summary>
    /// <param name="column">The column to fit.</param>
    /// <param name="dataSource">The rows to sample.</param>
    /// <param name="ctx">Measures header and cell text.</param>
    /// <param name="style">The grid style (fonts and padding).</param>
    /// <returns>The width, in pixels, within the column's minimum and maximum.</returns>
    public float CalculateAutoFitWidth(
        KumikoColumn column,
        KumikoSource dataSource,
        IDrawingContext ctx,
        KumikoStyle style) =>
        CalculateAutoFitWidth(column, dataSource, ctx, style, 0, -1);

    /// <summary>
    /// Calculate the optimal auto-fit width for a column based on its content: the wider of its header-fit width
    /// (see <see cref="MeasureHeaderFitWidth"/>, so auto-fit never truncates the header) and the widest display text
    /// among the sampled cells plus padding, clamped to MinWidth/MaxWidth. The sample is the rows from
    /// <paramref name="firstVisibleRow"/> to <paramref name="lastVisibleRow"/> plus the first and last
    /// <see cref="AutoFitSampleRows"/> rows of the view, so it costs the same on any number of rows.
    /// </summary>
    /// <param name="column">The column to fit.</param>
    /// <param name="dataSource">The rows to sample.</param>
    /// <param name="ctx">Measures header and cell text.</param>
    /// <param name="style">The grid style (fonts and padding).</param>
    /// <param name="firstVisibleRow">The first row on screen.</param>
    /// <param name="lastVisibleRow">The last row on screen (inclusive); below <paramref name="firstVisibleRow"/> for none.</param>
    /// <returns>The width, in pixels, within the column's minimum and maximum.</returns>
    public float CalculateAutoFitWidth(
        KumikoColumn column,
        KumikoSource dataSource,
        IDrawingContext ctx,
        KumikoStyle style,
        int firstVisibleRow,
        int lastVisibleRow)
    {
        float headerWidth = MeasureHeaderFitWidth(column, ctx, style);
        float textWidth = MeasureSampledCellText(column, dataSource, ctx, CellFont(column, style),
            firstVisibleRow, lastVisibleRow);
        return Clamp(ContentFit(column, headerWidth, textWidth, style), column.MinWidth, column.MaxWidth);
    }
}

namespace KumikoUI.Core.Layout;

using System.Diagnostics.CodeAnalysis;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

/// <summary>
/// Computes column widths (star sizing), total content dimensions,
/// and determines visible row/column ranges for virtualized rendering.
/// </summary>
public class GridLayoutEngine
{
    /// <summary>
    /// Recalculate column widths based on the available viewport width.
    /// Handles Fixed, Star, and Auto sizing modes.
    /// </summary>
    public void ComputeColumnWidths(IReadOnlyList<DataGridColumn> columns, float viewportWidth)
    {
        float fixedTotal = 0f;
        float starTotal = 0f;

        foreach (var col in columns)
        {
            if (!col.IsVisible) continue;

            if (col.SizeMode == ColumnSizeMode.Fixed || col.SizeMode == ColumnSizeMode.Auto)
                fixedTotal += col.Width;
            else if (col.SizeMode == ColumnSizeMode.Star)
                starTotal += col.StarWeight;
        }

        float starSpace = Math.Max(0, viewportWidth - fixedTotal);

        foreach (var col in columns)
        {
            if (!col.IsVisible) continue;

            if (col.SizeMode == ColumnSizeMode.Star && starTotal > 0)
            {
                float desired = starSpace * (col.StarWeight / starTotal);
                col.Width = Math.Clamp(desired, col.MinWidth, col.MaxWidth);
            }
        }
    }

    /// <summary>
    /// Calculate total content width from visible columns.
    /// </summary>
    public float GetContentWidth(IReadOnlyList<DataGridColumn> columns)
    {
        float w = 0;
        foreach (var col in columns)
            if (col.IsVisible) w += col.Width;
        return w;
    }

    /// <summary>
    /// Calculate the total width of frozen (pinned left) columns.
    /// </summary>
    public float GetFrozenColumnWidth(IReadOnlyList<DataGridColumn> columns)
    {
        float w = 0;
        foreach (var col in columns)
            if (col.IsVisible && col.IsFrozen) w += col.Width;
        return w;
    }

    /// <summary>
    /// Calculate the total width of right-frozen (pinned right) columns.
    /// </summary>
    public float GetRightFrozenColumnWidth(IReadOnlyList<DataGridColumn> columns)
    {
        float w = 0;
        foreach (var col in columns)
            if (col.IsVisible && col.IsFrozenRight) w += col.Width;
        return w;
    }

    /// <summary>
    /// Get only the frozen columns with their screen X positions.
    /// Frozen columns are always drawn starting at X=0,
    /// in the order they appear in the columns list.
    /// </summary>
    public List<(DataGridColumn Column, float X, int OriginalIndex)> GetVisibleFrozenColumns(
        IReadOnlyList<DataGridColumn> columns)
    {
        var result = new List<(DataGridColumn, float, int)>();
        PopulateVisibleFrozenColumns(columns, result);
        return result;
    }

    /// <summary>
    /// Populate an existing list with frozen columns (avoids per-frame allocation).
    /// </summary>
    public void PopulateVisibleFrozenColumns(
        IReadOnlyList<DataGridColumn> columns,
        List<(DataGridColumn Column, float X, int OriginalIndex)> result)
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
    public List<(DataGridColumn Column, float X, int OriginalIndex)> GetVisibleRightFrozenColumns(
        IReadOnlyList<DataGridColumn> columns, float viewportWidth)
    {
        var result = new List<(DataGridColumn, float, int)>();
        PopulateVisibleRightFrozenColumns(columns, viewportWidth, result);
        return result;
    }

    /// <summary>
    /// Populate an existing list with right-frozen columns (avoids per-frame allocation).
    /// </summary>
    public void PopulateVisibleRightFrozenColumns(
        IReadOnlyList<DataGridColumn> columns, float viewportWidth,
        List<(DataGridColumn Column, float X, int OriginalIndex)> result)
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
    public List<(DataGridColumn Column, float X, int OriginalIndex)> GetVisibleScrollableColumns(
        IReadOnlyList<DataGridColumn> columns, ScrollState scroll, float frozenWidth, float rightFrozenWidth = 0)
    {
        var result = new List<(DataGridColumn, float, int)>();
        PopulateVisibleScrollableColumns(columns, scroll, frozenWidth, rightFrozenWidth, result);
        return result;
    }

    /// <summary>
    /// Populate an existing list with visible scrollable columns (avoids per-frame allocation).
    /// </summary>
    public void PopulateVisibleScrollableColumns(
        IReadOnlyList<DataGridColumn> columns, ScrollState scroll, float frozenWidth,
        float rightFrozenWidth,
        List<(DataGridColumn Column, float X, int OriginalIndex)> result)
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
    public float GetContentHeight(int rowCount, DataGridStyle style)
    {
        return style.HeaderHeight + rowCount * style.RowHeight;
    }

    /// <summary>
    /// Calculate total height of frozen rows.
    /// </summary>
    public float GetFrozenRowHeight(int frozenRowCount, DataGridStyle style)
    {
        return frozenRowCount * style.RowHeight;
    }

    /// <summary>
    /// Determine the visible row range given the scroll state.
    /// Returns (firstVisibleRow, lastVisibleRow) inclusive.
    /// </summary>
    public (int First, int Last) GetVisibleRowRange(
        ScrollState scroll, DataGridStyle style, int totalRows)
    {
        return GetVisibleRowRangeWithOffset(scroll, style, totalRows, 0, 0);
    }

    /// <summary>
    /// Determine the visible row range given the scroll state and an additional offset
    /// (such as top summary row height) between the header and the data area.
    /// frozenRowCount rows are excluded from the scrollable range (they are drawn separately).
    /// </summary>
    public (int First, int Last) GetVisibleRowRangeWithOffset(
        ScrollState scroll, DataGridStyle style, int totalRows, float topOffset, int frozenRowCount = 0)
    {
        if (totalRows == 0) return (0, -1);

        int effectiveFrozen = Math.Min(frozenRowCount, totalRows);
        int scrollableRowCount = totalRows - effectiveFrozen;
        if (scrollableRowCount <= 0) return (0, -1); // all rows are frozen, none to scroll

        float frozenRowHeight = effectiveFrozen * style.RowHeight;
        float headerBottom = style.HeaderHeight + topOffset + frozenRowHeight;
        float scrollY = scroll.OffsetY;
        float viewportBottom = scrollY + scroll.ViewportHeight;

        int first = Math.Max(0, (int)((scrollY - headerBottom) / style.RowHeight));
        int last = Math.Min(scrollableRowCount - 1,
            (int)((viewportBottom - headerBottom) / style.RowHeight));

        // Map back to absolute row indices (offset by frozen row count)
        return (first + effectiveFrozen, last + effectiveFrozen);
    }

    /// <summary>
    /// Determine visible columns given horizontal scroll offset and viewport width.
    /// Returns list of (column, xOffset) pairs.
    /// </summary>
    public List<(DataGridColumn Column, float X)> GetVisibleColumns(
        IReadOnlyList<DataGridColumn> columns, ScrollState scroll)
    {
        var result = new List<(DataGridColumn, float)>();
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
    public (DataGridColumn? Column, int Index) HitTestColumn(
        IReadOnlyList<DataGridColumn> columns, float contentX)
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
    public int HitTestRow(float contentY, DataGridStyle style, int totalRows)
    {
        if (contentY < style.HeaderHeight) return -1;
        int row = (int)((contentY - style.HeaderHeight) / style.RowHeight);
        return row < totalRows ? row : -2;
    }

    /// <summary>
    /// Get the X position of a column's left edge.
    /// </summary>
    public float GetColumnX(IReadOnlyList<DataGridColumn> columns, int columnIndex)
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
    /// Calculate the optimal auto-fit width for a column based on its content.
    /// Measures the header text and all visible cell display text, then returns
    /// the maximum width plus padding, clamped to MinWidth/MaxWidth.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "GridLayoutEngine is part of the library and operates on columns configured by the consuming app. Reflection use is intentional.")]
    public float CalculateAutoFitWidth(
        DataGridColumn column,
        DataGridSource dataSource,
        IDrawingContext ctx,
        DataGridStyle style)
    {
        float maxWidth = 0;

        // Measure header text
        var headerPaint = new GridPaint
        {
            Font = style.HeaderFont,
            IsAntiAlias = true
        };
        var headerSize = ctx.MeasureText(column.Header, headerPaint);
        maxWidth = headerSize.Width;

        // Measure all visible row cell text
        var cellPaint = new GridPaint
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

        // Add padding on both sides plus a small buffer for sort indicator
        float optimalWidth = maxWidth + style.CellPadding * 2 + 16;

        return Math.Clamp(optimalWidth, column.MinWidth, column.MaxWidth);
    }
}

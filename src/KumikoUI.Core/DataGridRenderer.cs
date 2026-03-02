using KumikoUI.Core.Components;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Layout;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core;

/// <summary>
/// Renders the entire grid (header, rows, grid lines, selection, frozen columns)
/// using the platform-independent IDrawingContext.
/// </summary>
public class DataGridRenderer
{
    private readonly GridLayoutEngine _layout = new();
    private readonly PaintCache _paintCache = new();
    private readonly Dictionary<DataGridColumnType, ICellRenderer> _cellRenderers = new();

    // Reusable per-frame column lists — avoids allocating new Lists every Render() call
    private readonly List<(DataGridColumn Column, float X, int OriginalIndex)> _frozenCols = new();
    private readonly List<(DataGridColumn Column, float X, int OriginalIndex)> _rightFrozenCols = new();
    private readonly List<(DataGridColumn Column, float X, int OriginalIndex)> _scrollableCols = new();
    private readonly List<(DataGridColumn Column, float X, int OriginalIndex)> _allCols = new();

    public DataGridRenderer()
    {
        var textRenderer = new TextCellRenderer();
        _cellRenderers[DataGridColumnType.Text] = textRenderer;
        _cellRenderers[DataGridColumnType.Numeric] = textRenderer;
        _cellRenderers[DataGridColumnType.Date] = textRenderer;
        _cellRenderers[DataGridColumnType.Template] = textRenderer;
        _cellRenderers[DataGridColumnType.ComboBox] = textRenderer;
        _cellRenderers[DataGridColumnType.Picker] = textRenderer;
        _cellRenderers[DataGridColumnType.Boolean] = new BooleanCellRenderer();
        _cellRenderers[DataGridColumnType.Image] = new ImageCellRenderer();
    }

    /// <summary>Register a custom cell renderer for a column type.</summary>
    public void SetCellRenderer(DataGridColumnType columnType, ICellRenderer renderer)
    {
        _cellRenderers[columnType] = renderer;
    }

    /// <summary>Get the cell renderer for a column type.</summary>
    public ICellRenderer GetCellRenderer(DataGridColumnType columnType)
    {
        return _cellRenderers.TryGetValue(columnType, out var renderer)
            ? renderer
            : _cellRenderers[DataGridColumnType.Text];
    }

    /// <summary>Render the full grid given the current state.</summary>
    public void Render(
        IDrawingContext ctx,
        DataGridSource dataSource,
        ScrollState scroll,
        SelectionModel selection,
        DataGridStyle style,
        int dragColumnIndex = -1,
        float dragColumnScreenX = 0,
        int dragRowIndex = -1,
        float dragRowScreenY = 0,
        EditSession? editSession = null,
        PopupManager? popupManager = null)
    {
        var columns = dataSource.Columns;

        // Compute grouping panel offset (shifts everything below it)
        float groupPanelOffset = dataSource.IsGroupingActive ? style.GroupPanelHeight : 0;

        // Compute top summary height
        float topSummaryHeight = dataSource.TopSummaryCount * style.SummaryRowHeight;
        float bottomSummaryHeight = dataSource.BottomSummaryCount * style.SummaryRowHeight;

        // Compute drag handle offsets
        float handleOffset = 0, rightHandleWidth = 0;
        if (style.ShowRowDragHandle)
        {
            if (style.RowDragHandlePosition == DragHandlePosition.Left)
                handleOffset = style.RowDragHandleWidth;
            else
                rightHandleWidth = style.RowDragHandleWidth;
        }
        float effectiveViewportWidth = scroll.ViewportWidth - handleOffset - rightHandleWidth;

        _layout.ComputeColumnWidths(columns, effectiveViewportWidth);
        scroll.ContentWidth = _layout.GetContentWidth(columns) + handleOffset + rightHandleWidth;
        scroll.ContentHeight = _layout.GetContentHeight(dataSource.RowCount, style)
            + groupPanelOffset + topSummaryHeight + bottomSummaryHeight;
        scroll.ClampOffset();

        _paintCache.Update(style);

        ctx.FillRect(new GridRect(0, 0, scroll.ViewportWidth, scroll.ViewportHeight),
            _paintCache.Background);

        // Draw grouping panel above the header
        if (dataSource.IsGroupingActive)
        {
            DrawGroupingPanel(ctx, dataSource, scroll, style, groupPanelOffset);
        }

        // Offset all rendering below the grouping panel
        ctx.Save();
        if (groupPanelOffset > 0)
            ctx.Translate(0, groupPanelOffset);

        float adjustedViewportHeight = scroll.ViewportHeight - groupPanelOffset;

        // Compute frozen column panes — reuse lists to avoid per-frame allocation
        float frozenWidth = _layout.GetFrozenColumnWidth(columns);
        float rightFrozenWidth = _layout.GetRightFrozenColumnWidth(columns);
        _layout.PopulateVisibleFrozenColumns(columns, _frozenCols);
        _layout.PopulateVisibleRightFrozenColumns(columns, scroll.ViewportWidth - rightHandleWidth, _rightFrozenCols);
        _layout.PopulateVisibleScrollableColumns(columns, scroll, frozenWidth, rightFrozenWidth, _scrollableCols);
        var frozenCols = _frozenCols;
        var rightFrozenCols = _rightFrozenCols;
        var scrollableCols = _scrollableCols;

        // Offset column X positions for drag handle on the left
        if (handleOffset > 0)
        {
            for (int i = 0; i < frozenCols.Count; i++)
            {
                var (col, x, idx) = frozenCols[i];
                frozenCols[i] = (col, x + handleOffset, idx);
            }
            for (int i = 0; i < scrollableCols.Count; i++)
            {
                var (col, x, idx) = scrollableCols[i];
                scrollableCols[i] = (col, x + handleOffset, idx);
            }
        }

        // Compute frozen rows
        int frozenRowCount = dataSource.EffectiveFrozenRowCount;
        float frozenRowHeight = _layout.GetFrozenRowHeight(frozenRowCount, style);

        // Data rows start after header + top summaries + frozen rows
        float dataAreaTop = style.HeaderHeight + topSummaryHeight;
        var (firstRow, lastRow) = _layout.GetVisibleRowRangeWithOffset(
            scroll, style, dataSource.RowCount, topSummaryHeight, frozenRowCount);
        bool isMultiSort = dataSource.IsMultiSortActive;

        // ── Pass 1: Scrollable columns area ──────────────────────
        ctx.Save();
        float scrollClipLeft = frozenWidth + handleOffset;
        float scrollClipWidth = effectiveViewportWidth - frozenWidth - rightFrozenWidth;
        if (frozenWidth > 0 || rightFrozenWidth > 0 || handleOffset > 0 || rightHandleWidth > 0)
        {
            ctx.ClipRect(new GridRect(scrollClipLeft, 0, scrollClipWidth, adjustedViewportHeight));
        }

        DrawRows(ctx, dataSource, scrollableCols, firstRow, lastRow, scroll, selection, style, false, frozenWidth, editSession, topSummaryHeight, frozenRowCount);
        DrawCurrentCellBorder(ctx, scrollableCols, scroll, selection, style, false, frozenWidth, dataSource, topSummaryHeight, frozenRowCount);
        DrawGridLines(ctx, scrollableCols, firstRow, lastRow, scroll, style, dataSource.RowCount, false, frozenWidth, dataSource, topSummaryHeight, frozenRowCount);
        DrawHeader(ctx, scrollableCols, scroll, style, false, frozenWidth, handleOffset, rightHandleWidth);
        DrawSortIndicators(ctx, scrollableCols, scroll, style, false, frozenWidth, isMultiSort);
        DrawFilterIcons(ctx, scrollableCols, scroll, style, false, frozenWidth);

        // Draw frozen rows in the scrollable column area
        if (frozenRowCount > 0)
        {
            DrawFrozenRows(ctx, dataSource, scrollableCols, frozenRowCount, scroll, selection, style, false, frozenWidth, editSession, topSummaryHeight);
        }

        ctx.Restore();

        // ── Pass 2: Left-frozen columns area ─────────────────────
        if (frozenWidth > 0)
        {
            ctx.Save();
            ctx.ClipRect(new GridRect(handleOffset, 0, frozenWidth, adjustedViewportHeight));

            ctx.FillRect(new GridRect(handleOffset, 0, frozenWidth, adjustedViewportHeight),
                _paintCache.FrozenColumnBackground);
            DrawRows(ctx, dataSource, frozenCols, firstRow, lastRow, scroll, selection, style, true, frozenWidth, editSession, topSummaryHeight, frozenRowCount);
            DrawCurrentCellBorder(ctx, frozenCols, scroll, selection, style, true, frozenWidth, dataSource, topSummaryHeight, frozenRowCount);
            DrawGridLines(ctx, frozenCols, firstRow, lastRow, scroll, style, dataSource.RowCount, true, frozenWidth, dataSource, topSummaryHeight, frozenRowCount);
            DrawHeader(ctx, frozenCols, scroll, style, true, frozenWidth, handleOffset, rightHandleWidth);
            DrawSortIndicators(ctx, frozenCols, scroll, style, true, frozenWidth, isMultiSort);
            DrawFilterIcons(ctx, frozenCols, scroll, style, true, frozenWidth);

            // Draw frozen rows in the left-frozen column area
            if (frozenRowCount > 0)
            {
                DrawFrozenRows(ctx, dataSource, frozenCols, frozenRowCount, scroll, selection, style, true, frozenWidth, editSession, topSummaryHeight);
            }

            ctx.Restore();

            DrawFrozenColumnDivider(ctx, handleOffset + frozenWidth, scroll, style);
        }

        // ── Pass 3: Right-frozen columns area ────────────────────
        if (rightFrozenWidth > 0)
        {
            float rightFrozenLeft = scroll.ViewportWidth - rightHandleWidth - rightFrozenWidth;

            ctx.Save();
            ctx.ClipRect(new GridRect(rightFrozenLeft, 0, rightFrozenWidth, adjustedViewportHeight));

            DrawRightFrozenBackground(ctx, rightFrozenLeft, rightFrozenWidth, scroll, style);
            DrawRows(ctx, dataSource, rightFrozenCols, firstRow, lastRow, scroll, selection, style, ColumnFreezeMode.Right, frozenWidth, editSession, topSummaryHeight, frozenRowCount);
            DrawCurrentCellBorder(ctx, rightFrozenCols, scroll, selection, style, ColumnFreezeMode.Right, frozenWidth, dataSource, topSummaryHeight, frozenRowCount);
            DrawGridLines(ctx, rightFrozenCols, firstRow, lastRow, scroll, style, dataSource.RowCount, ColumnFreezeMode.Right, frozenWidth, dataSource, topSummaryHeight, frozenRowCount, rightFrozenLeft, rightFrozenWidth);
            DrawHeader(ctx, rightFrozenCols, scroll, style, ColumnFreezeMode.Right, frozenWidth, rightFrozenLeft, rightFrozenWidth, handleOffset, rightHandleWidth);
            DrawSortIndicators(ctx, rightFrozenCols, scroll, style, ColumnFreezeMode.Right, frozenWidth, isMultiSort);
            DrawFilterIcons(ctx, rightFrozenCols, scroll, style, ColumnFreezeMode.Right, frozenWidth);

            // Draw frozen rows in the right-frozen column area
            if (frozenRowCount > 0)
            {
                DrawFrozenRows(ctx, dataSource, rightFrozenCols, frozenRowCount, scroll, selection, style, ColumnFreezeMode.Right, frozenWidth, editSession, topSummaryHeight);
            }

            ctx.Restore();

            DrawRightFrozenColumnDivider(ctx, rightFrozenLeft, scroll, style);
        }

        // ── Draw frozen row divider (horizontal line below frozen rows) ──
        if (frozenRowCount > 0)
        {
            float dividerY = dataAreaTop + frozenRowHeight;
            DrawFrozenRowDivider(ctx, dividerY, scroll, style);
        }

        // ── Draw table summary rows outside clip regions (they span full width) ──
        // Reuse allCols list to avoid per-frame allocation
        _allCols.Clear();
        _allCols.AddRange(frozenCols);
        _allCols.AddRange(scrollableCols);
        _allCols.AddRange(rightFrozenCols);
        var allCols = _allCols;

        if (dataSource.TopSummaryCount > 0)
            DrawTableSummaryRows(ctx, dataSource, allCols, scroll, style, frozenWidth,
                dataSource.ComputedTopSummaries, style.HeaderHeight, handleOffset);

        if (dataSource.BottomSummaryCount > 0)
        {
            float bottomSummaryY = dataAreaTop + dataSource.RowCount * style.RowHeight - scroll.OffsetY;
            DrawTableSummaryRows(ctx, dataSource, allCols, scroll, style, frozenWidth,
                dataSource.ComputedBottomSummaries, bottomSummaryY, handleOffset);
        }

        // ── Draw full-width rows (group headers + group summaries) outside column clip regions ──
        // Clipped to the data area (below header + top summaries) so they don't scroll over the header
        {
            ctx.Save();
            float dataClipTop = style.HeaderHeight + topSummaryHeight;
            ctx.ClipRect(new GridRect(0, dataClipTop, scroll.ViewportWidth, adjustedViewportHeight - dataClipTop));

            for (int row = firstRow; row <= lastRow; row++)
            {
                float rowY = style.HeaderHeight + topSummaryHeight + frozenRowHeight + (row - frozenRowCount) * style.RowHeight - scroll.OffsetY;

                if (dataSource.IsGroupHeaderRow(row))
                {
                    DrawGroupHeaderRow(ctx, dataSource, row, rowY, allCols, scroll, style, frozenWidth, handleOffset);
                }
                else if (dataSource.IsGroupSummaryRow(row))
                {
                    DrawGroupSummaryRow(ctx, dataSource, row, rowY, allCols, scroll, style, frozenWidth, handleOffset);
                }
            }

            ctx.Restore();
        }

        // ── Draw drag handle column ──────────────────────────────
        if (style.ShowRowDragHandle)
        {
            DrawDragHandleColumn(ctx, dataSource, firstRow, lastRow, scroll, style,
                handleOffset, rightHandleWidth, topSummaryHeight, frozenRowCount, frozenRowHeight,
                adjustedViewportHeight);
        }

        if (dragColumnIndex >= 0)
            DrawColumnDragOverlay(ctx, columns, dragColumnIndex, dragColumnScreenX, scroll, style);

        if (dragRowIndex >= 0)
            DrawRowDragOverlay(ctx, dataSource, columns, dragRowIndex, dragRowScreenY, scroll, style,
                frozenWidth, rightFrozenWidth, topSummaryHeight, frozenRowCount,
                handleOffset, rightHandleWidth);

        // Restore from grouping panel offset
        ctx.Restore();

        // Draw active cell editor on top of everything (in absolute coordinates)
        editSession?.DrawEditor(ctx);

        // Draw any popups (filter, etc.) on top of everything
        popupManager?.DrawPopups(ctx);
    }

    private static float ColumnScreenX(float colContentX, ScrollState scroll, bool isFrozen)
    {
        return isFrozen ? colContentX : colContentX - scroll.OffsetX;
    }

    /// <summary>
    /// Returns screen X for a column, handling left-frozen, right-frozen, and scrollable.
    /// For right-frozen columns, the X is already an absolute screen position from GetVisibleRightFrozenColumns.
    /// </summary>
    private static float ColumnScreenX(float colContentX, ScrollState scroll, ColumnFreezeMode freezeMode)
    {
        return freezeMode switch
        {
            ColumnFreezeMode.Left => colContentX,
            ColumnFreezeMode.Right => colContentX, // already absolute screen X
            _ => colContentX - scroll.OffsetX
        };
    }

    private void DrawFrozenBackground(
        IDrawingContext ctx, float frozenWidth, ScrollState scroll, DataGridStyle style)
    {
        ctx.FillRect(new GridRect(0, 0, frozenWidth, scroll.ViewportHeight),
            _paintCache.FrozenColumnBackground);
    }

    private void DrawRightFrozenBackground(
        IDrawingContext ctx, float rightFrozenLeft, float rightFrozenWidth, ScrollState scroll, DataGridStyle style)
    {
        ctx.FillRect(new GridRect(rightFrozenLeft, 0, rightFrozenWidth, scroll.ViewportHeight),
            _paintCache.RightFrozenColumnBackground);
    }

    private void DrawFrozenColumnDivider(
        IDrawingContext ctx, float frozenWidth, ScrollState scroll, DataGridStyle style)
    {
        ctx.DrawLine(
            frozenWidth, 0,
            frozenWidth, scroll.ViewportHeight,
            _paintCache.FrozenColumnDivider);
    }

    private void DrawRightFrozenColumnDivider(
        IDrawingContext ctx, float rightFrozenLeft, ScrollState scroll, DataGridStyle style)
    {
        ctx.DrawLine(
            rightFrozenLeft, 0,
            rightFrozenLeft, scroll.ViewportHeight,
            _paintCache.RightFrozenColumnDivider);
    }

    private void DrawFrozenRowDivider(
        IDrawingContext ctx, float dividerY, ScrollState scroll, DataGridStyle style)
    {
        ctx.DrawLine(
            0, dividerY,
            scroll.ViewportWidth, dividerY,
            _paintCache.FrozenRowDivider);
    }

    private void DrawHeader(
        IDrawingContext ctx,
        List<(DataGridColumn Column, float X, int OriginalIndex)> visibleCols,
        ScrollState scroll,
        DataGridStyle style,
        bool isFrozen,
        float frozenWidth,
        float handleOffset = 0,
        float rightHandleWidth = 0)
    {
        DrawHeader(ctx, visibleCols, scroll, style,
            isFrozen ? ColumnFreezeMode.Left : ColumnFreezeMode.None,
            frozenWidth, 0, 0, handleOffset, rightHandleWidth);
    }

    private void DrawHeader(
        IDrawingContext ctx,
        List<(DataGridColumn Column, float X, int OriginalIndex)> visibleCols,
        ScrollState scroll,
        DataGridStyle style,
        ColumnFreezeMode freezeMode,
        float frozenWidth,
        float rightFrozenLeft = 0,
        float rightFrozenWidth = 0,
        float handleOffset = 0,
        float rightHandleWidth = 0)
    {
        float areaLeft, areaWidth;
        switch (freezeMode)
        {
            case ColumnFreezeMode.Left:
                areaLeft = handleOffset;
                areaWidth = frozenWidth;
                break;
            case ColumnFreezeMode.Right:
                areaLeft = rightFrozenLeft;
                areaWidth = rightFrozenWidth;
                break;
            default:
                areaLeft = frozenWidth + handleOffset;
                areaWidth = scroll.ViewportWidth - frozenWidth - handleOffset;
                break;
        }

        ctx.FillRect(new GridRect(areaLeft, 0, areaWidth, style.HeaderHeight),
            _paintCache.HeaderBackground);

        ctx.DrawLine(areaLeft, style.HeaderHeight, areaLeft + areaWidth, style.HeaderHeight,
            _paintCache.HeaderBorder);

        var headerPaint = _paintCache.HeaderText;

        foreach (var (col, colX, _) in visibleCols)
        {
            float screenX = ColumnScreenX(colX, scroll, col.FreezeMode);

            // Per-column header style overrides
            var hStyle = col.HeaderCellStyle;
            var colPaint = hStyle != null && (hStyle.TextColor.HasValue || hStyle.Font != null)
                ? new GridPaint
                {
                    Color = hStyle.TextColor ?? style.HeaderTextColor,
                    Font = hStyle.Font ?? style.HeaderFont,
                    IsAntiAlias = true
                }
                : headerPaint;

            // Per-column header background
            if (hStyle?.BackgroundColor != null)
            {
                ctx.FillRect(new GridRect(screenX, 0, col.Width, style.HeaderHeight),
                    new GridPaint { Color = hStyle.BackgroundColor.Value });
            }

            float padding = hStyle?.Padding ?? style.HeaderPadding;

            // Reserve right-side space for sort indicator and filter icon
            // to prevent header text from overlapping them
            float rightReserved = 0;
            if (col.AllowSorting) rightReserved += 12f;   // sort arrow (8) + gap (4)
            if (col.AllowFiltering) rightReserved += 14f;  // filter icon (10) + gap (4)

            var cellRect = new GridRect(
                screenX + padding,
                0,
                col.Width - padding * 2 - rightReserved,
                style.HeaderHeight);

            ctx.DrawTextInRect(col.Header, cellRect, colPaint,
                GridTextAlignment.Left, GridVerticalAlignment.Center);
        }
    }

    private void DrawRows(
        IDrawingContext ctx,
        DataGridSource dataSource,
        List<(DataGridColumn Column, float X, int OriginalIndex)> visibleCols,
        int firstRow, int lastRow,
        ScrollState scroll,
        SelectionModel selection,
        DataGridStyle style,
        bool isFrozen,
        float frozenWidth,
        EditSession? editSession = null,
        float topSummaryHeight = 0,
        int frozenRowCount = 0)
    {
        DrawRows(ctx, dataSource, visibleCols, firstRow, lastRow, scroll, selection, style,
            isFrozen ? ColumnFreezeMode.Left : ColumnFreezeMode.None,
            frozenWidth, editSession, topSummaryHeight, frozenRowCount);
    }

    private void DrawRows(
        IDrawingContext ctx,
        DataGridSource dataSource,
        List<(DataGridColumn Column, float X, int OriginalIndex)> visibleCols,
        int firstRow, int lastRow,
        ScrollState scroll,
        SelectionModel selection,
        DataGridStyle style,
        ColumnFreezeMode freezeMode,
        float frozenWidth,
        EditSession? editSession = null,
        float topSummaryHeight = 0,
        int frozenRowCount = 0)
    {
        if (firstRow > lastRow) return;

        bool hasCurrentRow = selection.CurrentCell.IsValid;
        int currentRow = selection.CurrentCell.Row;
        float frozenRowHeight = frozenRowCount * style.RowHeight;

        for (int row = firstRow; row <= lastRow; row++)
        {
            // Account for frozen rows: scrollable rows start after frozen rows in screen space
            float rowY = style.HeaderHeight + topSummaryHeight + frozenRowHeight
                + (row - frozenRowCount) * style.RowHeight - scroll.OffsetY;

            // Skip group headers and group summaries — they are drawn in a separate full-width pass
            if (dataSource.IsGroupHeaderRow(row))
                continue;
            if (dataSource.IsGroupSummaryRow(row))
                continue;

            // Resolve conditional row style
            object? dataItem = null;
            RowStyle? dynamicRowStyle = null;
            if (style.RowStyleResolver != null || style.CellStyleResolver != null)
            {
                dataItem = dataSource.GetItem(row);
                dynamicRowStyle = style.RowStyleResolver?.Invoke(dataItem);
            }

            bool isFrozenLeft = freezeMode == ColumnFreezeMode.Left;
            bool isSelected = selection.IsRowSelected(row);
            bool isFocused = hasCurrentRow && row == currentRow;

            // Resolve row background — prefer cached PaintCache instances for common cases
            GridPaint rowBgPaint;
            if (isSelected)
            {
                rowBgPaint = _paintCache.SelectionBackground;
            }
            else if (isFocused)
            {
                rowBgPaint = _paintCache.FocusedRowBackground;
            }
            else if (dynamicRowStyle?.BackgroundColor != null)
            {
                rowBgPaint = new GridPaint { Color = dynamicRowStyle.BackgroundColor.Value };
            }
            else if (style.AlternateRowBackground && row % 2 == 1)
            {
                rowBgPaint = _paintCache.AlternateRowBackground;
            }
            else if (isFrozenLeft || freezeMode == ColumnFreezeMode.Right)
            {
                rowBgPaint = freezeMode == ColumnFreezeMode.Right
                    ? _paintCache.RightFrozenColumnBackground
                    : _paintCache.FrozenColumnBackground;
            }
            else
            {
                rowBgPaint = _paintCache.Background;
            }

            // Row background fill area depends on freeze mode — but when inside a clip, fill entire clip
            ctx.FillRect(
                new GridRect(0, rowY, scroll.ViewportWidth, style.RowHeight),
                rowBgPaint);

            foreach (var (col, colX, origIdx) in visibleCols)
            {
                float screenX = ColumnScreenX(colX, scroll, col.FreezeMode);

                // Merge static per-column style with dynamic resolver style
                var colCellStyle = col.CellStyle;
                if (style.CellStyleResolver != null)
                {
                    dataItem ??= dataSource.GetItem(row);
                    var dynamicCellStyle = style.CellStyleResolver(dataItem, col);
                    colCellStyle = CellStyle.Merge(dynamicCellStyle, colCellStyle);
                }

                // Apply row-level text/font overrides into the effective cell style
                if (dynamicRowStyle != null &&
                    (dynamicRowStyle.TextColor != null || dynamicRowStyle.Font != null))
                {
                    colCellStyle ??= new CellStyle();
                    if (colCellStyle == col.CellStyle)
                        colCellStyle = CellStyle.Merge(null, colCellStyle) ?? new CellStyle();
                    colCellStyle.TextColor ??= dynamicRowStyle.TextColor;
                    colCellStyle.Font ??= dynamicRowStyle.Font;
                }

                float cellPadding = colCellStyle?.Padding ?? style.CellPadding;

                // Per-column cell background
                if (colCellStyle?.BackgroundColor != null)
                {
                    ctx.FillRect(new GridRect(screenX, rowY, col.Width, style.RowHeight),
                        new GridPaint { Color = colCellStyle.BackgroundColor.Value });
                }

                var cellRect = new GridRect(
                    screenX + cellPadding,
                    rowY,
                    col.Width - cellPadding * 2,
                    style.RowHeight);

                // Skip rendering content for the cell being edited (editor draws on top)
                if (editSession != null && editSession.IsCellBeingEdited(row, origIdx))
                    continue;

                var renderer = col.CustomCellRenderer ?? GetCellRenderer(col.ColumnType);
                var value = dataSource.GetCellValue(row, col);
                var displayText = DataGridSource.FormatCellValue(value, col);

                renderer.Render(ctx, cellRect, value, displayText, col, style, isSelected, colCellStyle);
            }
        }
    }

    /// <summary>
    /// Draw frozen rows (top N sticky rows) at fixed Y positions below header + top summaries.
    /// These rows don't scroll vertically.
    /// </summary>
    private void DrawFrozenRows(
        IDrawingContext ctx,
        DataGridSource dataSource,
        List<(DataGridColumn Column, float X, int OriginalIndex)> visibleCols,
        int frozenRowCount,
        ScrollState scroll,
        SelectionModel selection,
        DataGridStyle style,
        bool isFrozenCol,
        float frozenWidth,
        EditSession? editSession,
        float topSummaryHeight)
    {
        DrawFrozenRows(ctx, dataSource, visibleCols, frozenRowCount, scroll, selection, style,
            isFrozenCol ? ColumnFreezeMode.Left : ColumnFreezeMode.None, frozenWidth, editSession, topSummaryHeight);
    }

    private void DrawFrozenRows(
        IDrawingContext ctx,
        DataGridSource dataSource,
        List<(DataGridColumn Column, float X, int OriginalIndex)> visibleCols,
        int frozenRowCount,
        ScrollState scroll,
        SelectionModel selection,
        DataGridStyle style,
        ColumnFreezeMode freezeMode,
        float frozenWidth,
        EditSession? editSession,
        float topSummaryHeight)
    {
        if (frozenRowCount <= 0) return;

        bool hasCurrentRow = selection.CurrentCell.IsValid;
        int currentRow = selection.CurrentCell.Row;

        for (int row = 0; row < frozenRowCount && row < dataSource.RowCount; row++)
        {
            // Frozen rows are at fixed Y positions, unaffected by scroll.OffsetY
            float rowY = style.HeaderHeight + topSummaryHeight + row * style.RowHeight;

            // Skip group headers/summaries in frozen rows (shouldn't normally happen)
            if (dataSource.IsGroupHeaderRow(row) || dataSource.IsGroupSummaryRow(row))
                continue;

            // Resolve conditional row style
            object? dataItem = null;
            RowStyle? dynamicRowStyle = null;
            if (style.RowStyleResolver != null || style.CellStyleResolver != null)
            {
                dataItem = dataSource.GetItem(row);
                dynamicRowStyle = style.RowStyleResolver?.Invoke(dataItem);
            }

            bool isFrozenLeft = freezeMode == ColumnFreezeMode.Left;
            bool isSelected = selection.IsRowSelected(row);
            bool isFocused = hasCurrentRow && row == currentRow;

            // Resolve frozen row background — prefer cached PaintCache instances
            GridPaint rowBgPaint;
            if (isSelected)
            {
                rowBgPaint = _paintCache.SelectionBackground;
            }
            else if (isFocused)
            {
                rowBgPaint = _paintCache.FocusedRowBackground;
            }
            else if (dynamicRowStyle?.BackgroundColor != null)
            {
                rowBgPaint = new GridPaint { Color = dynamicRowStyle.BackgroundColor.Value };
            }
            else if (style.AlternateRowBackground && row % 2 == 1)
            {
                rowBgPaint = _paintCache.AlternateRowBackground;
            }
            else
            {
                // Frozen rows use FrozenRowBackgroundColor by default
                rowBgPaint = _paintCache.FrozenRowBackground;
            }

            ctx.FillRect(
                new GridRect(0, rowY, scroll.ViewportWidth, style.RowHeight),
                rowBgPaint);

            foreach (var (col, colX, origIdx) in visibleCols)
            {
                float screenX = ColumnScreenX(colX, scroll, col.FreezeMode);

                // Merge static per-column style with dynamic resolver style
                var colCellStyle = col.CellStyle;
                if (style.CellStyleResolver != null)
                {
                    dataItem ??= dataSource.GetItem(row);
                    var dynamicCellStyle = style.CellStyleResolver(dataItem, col);
                    colCellStyle = CellStyle.Merge(dynamicCellStyle, colCellStyle);
                }

                // Apply row-level text/font overrides into the effective cell style
                if (dynamicRowStyle != null &&
                    (dynamicRowStyle.TextColor != null || dynamicRowStyle.Font != null))
                {
                    colCellStyle ??= new CellStyle();
                    if (colCellStyle == col.CellStyle)
                        colCellStyle = CellStyle.Merge(null, colCellStyle) ?? new CellStyle();
                    colCellStyle.TextColor ??= dynamicRowStyle.TextColor;
                    colCellStyle.Font ??= dynamicRowStyle.Font;
                }

                float cellPadding = colCellStyle?.Padding ?? style.CellPadding;

                // Per-column cell background
                if (colCellStyle?.BackgroundColor != null)
                {
                    ctx.FillRect(new GridRect(screenX, rowY, col.Width, style.RowHeight),
                        new GridPaint { Color = colCellStyle.BackgroundColor.Value });
                }

                var cellRect = new GridRect(
                    screenX + cellPadding,
                    rowY,
                    col.Width - cellPadding * 2,
                    style.RowHeight);

                if (editSession != null && editSession.IsCellBeingEdited(row, origIdx))
                    continue;

                var renderer = col.CustomCellRenderer ?? GetCellRenderer(col.ColumnType);
                var value = dataSource.GetCellValue(row, col);
                var displayText = DataGridSource.FormatCellValue(value, col);

                renderer.Render(ctx, cellRect, value, displayText, col, style, isSelected, colCellStyle);
            }
        }
    }

    /// <summary>
    /// Draw a group header row spanning the full width with chevron, group text, and item count.
    /// </summary>
    private static void DrawGroupHeaderRow(
        IDrawingContext ctx,
        DataGridSource dataSource,
        int row,
        float rowY,
        List<(DataGridColumn Column, float X, int OriginalIndex)> allCols,
        ScrollState scroll,
        DataGridStyle style,
        float frozenWidth,
        float handleOffset = 0)
    {
        var groupInfo = dataSource.GetGroupHeaderInfo(row);
        if (groupInfo == null) return;

        // Group header background spans full viewport width
        float areaLeft = 0;
        float areaWidth = scroll.ViewportWidth;

        ctx.FillRect(
            new GridRect(areaLeft, rowY, areaWidth, style.RowHeight),
            new GridPaint { Color = style.GroupHeaderBackgroundColor });

        // Bottom border line
        ctx.DrawLine(areaLeft, rowY + style.RowHeight, areaLeft + areaWidth, rowY + style.RowHeight,
            new GridPaint
            {
                Color = style.GridLineColor,
                StrokeWidth = style.GridLineWidth,
                Style = PaintStyle.Stroke,
                IsAntiAlias = false
            });

        // Indent based on group level
        float indent = handleOffset + style.CellPadding + groupInfo.Level * style.GroupIndentWidth;

        // Draw expand/collapse indicator with background pill
        float chevronSize = style.GroupChevronSize;
        float chevronX = indent;
        float chevronCenterY = rowY + style.RowHeight / 2;

        // Draw rounded-rect background behind the indicator
        float pillW = chevronSize + 8f;
        float pillH = chevronSize + 6f;
        var pillRect = new GridRect(
            chevronX - 4f, chevronCenterY - pillH / 2, pillW, pillH);
        ctx.FillRoundRect(pillRect, 4f,
            new GridPaint { Color = style.GroupChevronBackgroundColor, IsAntiAlias = true });

        // Draw filled triangle using thick lines converging to a solid shape
        var chevronPaint = new GridPaint
        {
            Color = style.GroupChevronColor,
            Style = PaintStyle.Stroke,
            StrokeWidth = 2.5f,
            IsAntiAlias = true
        };

        if (groupInfo.IsExpanded)
        {
            // Down-pointing triangle (▼) — three edges
            float triW = chevronSize * 0.7f;
            float triH = chevronSize * 0.45f;
            float cx = chevronX + chevronSize / 2;
            float topY = chevronCenterY - triH / 2;
            float botY = chevronCenterY + triH / 2;
            // left-top to bottom-center
            ctx.DrawLine(cx - triW / 2, topY, cx, botY, chevronPaint);
            // bottom-center to right-top
            ctx.DrawLine(cx, botY, cx + triW / 2, topY, chevronPaint);
            // top edge
            ctx.DrawLine(cx - triW / 2, topY, cx + triW / 2, topY, chevronPaint);
        }
        else
        {
            // Right-pointing triangle (▶) — three edges
            float triW = chevronSize * 0.45f;
            float triH = chevronSize * 0.7f;
            float leftX = chevronX + chevronSize * 0.3f;
            float rightX = leftX + triW;
            // top-left to right-center
            ctx.DrawLine(leftX, chevronCenterY - triH / 2, rightX, chevronCenterY, chevronPaint);
            // right-center to bottom-left
            ctx.DrawLine(rightX, chevronCenterY, leftX, chevronCenterY + triH / 2, chevronPaint);
            // left edge
            ctx.DrawLine(leftX, chevronCenterY - triH / 2, leftX, chevronCenterY + triH / 2, chevronPaint);
        }

        // Draw group text: "Header: DisplayText (count items)"
        float textX = indent + chevronSize + style.CellPadding;
        string groupText = $"{groupInfo.HeaderText}: {groupInfo.DisplayText}";
        string countText = $" ({groupInfo.ItemCount})";

        // Include caption summary if available
        string? captionText = dataSource.GetCaptionSummaryText(groupInfo.GroupPath);
        if (captionText != null)
            countText += $"  —  {captionText}";

        var textPaint = new GridPaint
        {
            Color = style.GroupHeaderTextColor,
            Font = style.GroupHeaderFont,
            IsAntiAlias = true
        };

        var textRect = new GridRect(textX, rowY, areaWidth - textX - style.CellPadding, style.RowHeight);
        ctx.DrawTextInRect(groupText + countText, textRect, textPaint,
            GridTextAlignment.Left, GridVerticalAlignment.Center);
    }

    private void DrawGridLines(
        IDrawingContext ctx,
        List<(DataGridColumn Column, float X, int OriginalIndex)> visibleCols,
        int firstRow, int lastRow,
        ScrollState scroll,
        DataGridStyle style,
        int totalRows,
        bool isFrozen,
        float frozenWidth,
        DataGridSource? dataSource = null,
        float topSummaryHeight = 0,
        int frozenRowCount = 0)
    {
        DrawGridLines(ctx, visibleCols, firstRow, lastRow, scroll, style, totalRows,
            isFrozen ? ColumnFreezeMode.Left : ColumnFreezeMode.None,
            frozenWidth, dataSource, topSummaryHeight, frozenRowCount, 0, 0);
    }

    private void DrawGridLines(
        IDrawingContext ctx,
        List<(DataGridColumn Column, float X, int OriginalIndex)> visibleCols,
        int firstRow, int lastRow,
        ScrollState scroll,
        DataGridStyle style,
        int totalRows,
        ColumnFreezeMode freezeMode,
        float frozenWidth,
        DataGridSource? dataSource = null,
        float topSummaryHeight = 0,
        int frozenRowCount = 0,
        float rightFrozenLeft = 0,
        float rightFrozenWidth = 0)
    {
        var linePaint = _paintCache.GridLine;

        float frozenRowHeight = frozenRowCount * style.RowHeight;

        if (style.ShowHorizontalGridLines)
        {
            for (int row = firstRow; row <= lastRow + 1; row++)
            {
                float y = style.HeaderHeight + topSummaryHeight + frozenRowHeight
                    + (row - frozenRowCount) * style.RowHeight - scroll.OffsetY;
                // Let the clip region handle the horizontal extent
                ctx.DrawLine(0, y, scroll.ViewportWidth, y, linePaint);
            }
        }

        if (style.ShowVerticalGridLines)
        {
            float maxY = Math.Min(
                style.HeaderHeight + topSummaryHeight + totalRows * style.RowHeight - scroll.OffsetY,
                scroll.ViewportHeight);

            foreach (var (col, colX, _) in visibleCols)
            {
                float screenX = ColumnScreenX(colX, scroll, col.FreezeMode) + col.Width;
                ctx.DrawLine(screenX, 0, screenX, maxY, linePaint);
            }
        }
    }

    private void DrawCurrentCellBorder(
        IDrawingContext ctx,
        List<(DataGridColumn Column, float X, int OriginalIndex)> visibleCols,
        ScrollState scroll,
        SelectionModel selection,
        DataGridStyle style,
        bool isFrozen,
        float frozenWidth,
        DataGridSource? dataSource = null,
        float topSummaryHeight = 0,
        int frozenRowCount = 0)
    {
        DrawCurrentCellBorder(ctx, visibleCols, scroll, selection, style,
            isFrozen ? ColumnFreezeMode.Left : ColumnFreezeMode.None,
            frozenWidth, dataSource, topSummaryHeight, frozenRowCount);
    }

    private void DrawCurrentCellBorder(
        IDrawingContext ctx,
        List<(DataGridColumn Column, float X, int OriginalIndex)> visibleCols,
        ScrollState scroll,
        SelectionModel selection,
        DataGridStyle style,
        ColumnFreezeMode freezeMode,
        float frozenWidth,
        DataGridSource? dataSource = null,
        float topSummaryHeight = 0,
        int frozenRowCount = 0)
    {
        if (!selection.CurrentCell.IsValid) return;
        if (selection.Unit != SelectionUnit.Cell && selection.Unit != SelectionUnit.Row) return;

        int row = selection.CurrentCell.Row;
        int targetOrigIdx = selection.CurrentCell.Column;

        // Don't draw cell border on group header or group summary rows
        if (dataSource != null && dataSource.IsNonDataRow(row)) return;

        float frozenRowHeight = frozenRowCount * style.RowHeight;

        foreach (var (col, colX, origIdx) in visibleCols)
        {
            if (origIdx == targetOrigIdx)
            {
                float screenX = ColumnScreenX(colX, scroll, col.FreezeMode);
                float rowY;
                if (row < frozenRowCount)
                {
                    // Frozen row: fixed position
                    rowY = style.HeaderHeight + topSummaryHeight + row * style.RowHeight;
                }
                else
                {
                    // Scrollable row
                    rowY = style.HeaderHeight + topSummaryHeight + frozenRowHeight
                        + (row - frozenRowCount) * style.RowHeight - scroll.OffsetY;
                }
                var cellRect = new GridRect(screenX, rowY, col.Width, style.RowHeight);

                ctx.DrawRect(cellRect, _paintCache.CurrentCellBorder);
                return;
            }
        }
    }

    private void DrawSortIndicators(
        IDrawingContext ctx,
        List<(DataGridColumn Column, float X, int OriginalIndex)> visibleCols,
        ScrollState scroll,
        DataGridStyle style,
        bool isFrozen,
        float frozenWidth,
        bool isMultiSort = false)
    {
        DrawSortIndicators(ctx, visibleCols, scroll, style,
            isFrozen ? ColumnFreezeMode.Left : ColumnFreezeMode.None,
            frozenWidth, isMultiSort);
    }

    private void DrawSortIndicators(
        IDrawingContext ctx,
        List<(DataGridColumn Column, float X, int OriginalIndex)> visibleCols,
        ScrollState scroll,
        DataGridStyle style,
        ColumnFreezeMode freezeMode,
        float frozenWidth,
        bool isMultiSort = false)
    {
        foreach (var (col, colX, _) in visibleCols)
        {
            if (col.SortDirection == SortDirection.None) continue;

            float screenX = ColumnScreenX(colX, scroll, col.FreezeMode);
            float arrowX = screenX + col.Width - style.CellPadding - 8;
            float arrowCenterY = style.HeaderHeight / 2;

            var paint = _paintCache.SortIndicator;

            if (col.SortDirection == SortDirection.Ascending)
            {
                ctx.DrawLine(arrowX, arrowCenterY + 3, arrowX + 4, arrowCenterY - 3, paint);
                ctx.DrawLine(arrowX + 4, arrowCenterY - 3, arrowX + 8, arrowCenterY + 3, paint);
                ctx.DrawLine(arrowX + 8, arrowCenterY + 3, arrowX, arrowCenterY + 3, paint);
            }
            else
            {
                ctx.DrawLine(arrowX, arrowCenterY - 3, arrowX + 4, arrowCenterY + 3, paint);
                ctx.DrawLine(arrowX + 4, arrowCenterY + 3, arrowX + 8, arrowCenterY - 3, paint);
                ctx.DrawLine(arrowX + 8, arrowCenterY - 3, arrowX, arrowCenterY - 3, paint);
            }

            // Draw sort order number badge for multi-sort
            if (isMultiSort && col.SortOrder > 0)
            {
                var numberText = col.SortOrder.ToString();
                var numberPaint = new GridPaint
                {
                    Color = style.SortIndicatorColor,
                    Font = new GridFont(style.HeaderFont.Family, style.HeaderFont.Size * 0.65f),
                    IsAntiAlias = true
                };

                float numberX = arrowX - 2;
                float numberY = arrowCenterY + (style.HeaderFont.Size * 0.65f) / 2 - 1;
                ctx.DrawText(numberText, numberX, numberY, numberPaint);
            }
        }
    }

    private void DrawFilterIcons(
        IDrawingContext ctx,
        List<(DataGridColumn Column, float X, int OriginalIndex)> visibleCols,
        ScrollState scroll,
        DataGridStyle style,
        bool isFrozen,
        float frozenWidth)
    {
        DrawFilterIcons(ctx, visibleCols, scroll, style,
            isFrozen ? ColumnFreezeMode.Left : ColumnFreezeMode.None, frozenWidth);
    }

    private void DrawFilterIcons(
        IDrawingContext ctx,
        List<(DataGridColumn Column, float X, int OriginalIndex)> visibleCols,
        ScrollState scroll,
        DataGridStyle style,
        ColumnFreezeMode freezeMode,
        float frozenWidth)
    {
        foreach (var (col, colX, _) in visibleCols)
        {
            if (!col.AllowFiltering) continue;

            float screenX = ColumnScreenX(colX, scroll, col.FreezeMode);
            bool hasActiveFilter = col.ActiveFilter != null && col.ActiveFilter.IsActive;

            // Position filter icon: right of header text, left of sort indicator if present
            float iconSize = 10f;
            float sortSpace = col.AllowSorting ? 12f : 0f;  // sort arrow (8) + gap (4)
            float iconX = screenX + col.Width - style.CellPadding - sortSpace - iconSize;
            float iconCenterY = style.HeaderHeight / 2;

            var paint = hasActiveFilter ? _paintCache.FilterActiveIcon : _paintCache.FilterIcon;

            // Draw funnel icon: wider at top, narrow at bottom, with a stem
            float top = iconCenterY - iconSize / 2;
            float bottom = iconCenterY + iconSize / 2;
            float mid = top + iconSize * 0.45f;

            // Top wide part of funnel
            ctx.DrawLine(iconX, top, iconX + iconSize, top, paint);
            // Left slope
            ctx.DrawLine(iconX, top, iconX + iconSize * 0.35f, mid, paint);
            // Right slope
            ctx.DrawLine(iconX + iconSize, top, iconX + iconSize * 0.65f, mid, paint);
            // Stem
            ctx.DrawLine(iconX + iconSize * 0.35f, mid, iconX + iconSize * 0.35f, bottom, paint);
            ctx.DrawLine(iconX + iconSize * 0.65f, mid, iconX + iconSize * 0.65f, bottom, paint);
            // Bottom
            ctx.DrawLine(iconX + iconSize * 0.35f, bottom, iconX + iconSize * 0.65f, bottom, paint);

            // Fill the funnel if filter is active
            if (hasActiveFilter)
            {
                var fillPaint = new GridPaint
                {
                    Color = style.FilterActiveIconColor,
                    Style = PaintStyle.Fill,
                    IsAntiAlias = true
                };
                // Simple fill indication: draw a filled circle dot
                float dotR = 2.5f;
                float dotX = iconX + iconSize / 2;
                float dotY = mid + (bottom - mid) / 2;
                ctx.FillRect(new GridRect(dotX - dotR, dotY - dotR, dotR * 2, dotR * 2), fillPaint);
            }
        }
    }

    /// <summary>
    /// Draw the grouping panel above the header, showing active group chips with remove buttons.
    /// </summary>
    private static void DrawGroupingPanel(
        IDrawingContext ctx,
        DataGridSource dataSource,
        ScrollState scroll,
        DataGridStyle style,
        float panelHeight)
    {
        // Background
        ctx.FillRect(new GridRect(0, 0, scroll.ViewportWidth, panelHeight),
            new GridPaint { Color = style.GroupPanelBackgroundColor });

        // Bottom border
        ctx.DrawLine(0, panelHeight, scroll.ViewportWidth, panelHeight,
            new GridPaint
            {
                Color = style.GridLineColor,
                StrokeWidth = style.GridLineWidth,
                Style = PaintStyle.Stroke,
                IsAntiAlias = false
            });

        var groups = dataSource.GroupDescriptions;
        if (groups.Count == 0) return;

        // Draw "Grouped by:" label
        float labelX = style.CellPadding + 2;
        var labelPaint = new GridPaint
        {
            Color = style.GroupPanelLabelColor,
            Font = style.GroupPanelLabelFont,
            IsAntiAlias = true
        };
        var labelSize = ctx.MeasureText("Grouped by:", labelPaint);
        var labelRect = new GridRect(labelX, 0, labelSize.Width, panelHeight);
        ctx.DrawTextInRect("Grouped by:", labelRect, labelPaint,
            GridTextAlignment.Left, GridVerticalAlignment.Center);

        float chipX = labelX + labelSize.Width + 10;
        float chipHeight = 26;
        float chipY = (panelHeight - chipHeight) / 2;
        float chipPadding = 10;
        float chipGap = 4;
        float accentWidth = 4f;

        var chipTextPaint = new GridPaint
        {
            Color = style.GroupPanelChipTextColor,
            Font = style.GroupPanelFont,
            IsAntiAlias = true
        };

        foreach (var group in groups)
        {
            // Find the column header text
            string displayName = group.Header ?? group.PropertyName;
            var col = dataSource.Columns.FirstOrDefault(c =>
                string.Equals(c.PropertyName, group.PropertyName, StringComparison.OrdinalIgnoreCase));
            if (col != null && group.Header == null)
                displayName = col.Header;

            // Measure chip text
            string chipText = displayName;
            var textSize = ctx.MeasureText(chipText, chipTextPaint);
            float sortArrowWidth = 16;  // space for sort direction arrow
            float removeButtonSize = 16;
            float chipWidth = accentWidth + chipPadding + textSize.Width + 4 + sortArrowWidth + chipPadding + removeButtonSize + chipPadding / 2;

            // Draw chip background with rounded corners
            var chipRect = new GridRect(chipX, chipY, chipWidth, chipHeight);
            ctx.FillRoundRect(chipRect, 6, new GridPaint { Color = style.GroupPanelChipBackgroundColor });

            // Draw chip border
            ctx.DrawRoundRect(chipRect, 6, new GridPaint
            {
                Color = style.GroupPanelChipBorderColor,
                Style = PaintStyle.Stroke,
                StrokeWidth = 1f,
                IsAntiAlias = true
            });

            // Draw left accent bar (rounded)
            var accentRect = new GridRect(chipX, chipY, accentWidth + 6, chipHeight);
            ctx.Save();
            ctx.ClipRect(new GridRect(chipX, chipY, accentWidth + 3, chipHeight));
            ctx.FillRoundRect(accentRect, 6, new GridPaint { Color = style.GroupPanelChipAccentColor });
            ctx.Restore();

            // Draw chip text
            float textStartX = chipX + accentWidth + chipPadding;
            var textRect = new GridRect(textStartX, chipY, textSize.Width, chipHeight);
            ctx.DrawTextInRect(chipText, textRect, chipTextPaint,
                GridTextAlignment.Left, GridVerticalAlignment.Center);

            // Draw sort direction arrow
            float arrowX = textStartX + textSize.Width + 4;
            float arrowCY = chipY + chipHeight / 2;
            var arrowPaint = new GridPaint
            {
                Color = style.GroupPanelChipAccentColor,
                Style = PaintStyle.Stroke,
                StrokeWidth = 2f,
                IsAntiAlias = true
            };
            if (group.GroupSortDirection == SortDirection.Ascending)
            {
                // Up arrow
                float ax = arrowX + 6;
                ctx.DrawLine(ax, arrowCY + 4, ax, arrowCY - 4, arrowPaint);
                ctx.DrawLine(ax - 3, arrowCY - 1, ax, arrowCY - 4, arrowPaint);
                ctx.DrawLine(ax + 3, arrowCY - 1, ax, arrowCY - 4, arrowPaint);
            }
            else
            {
                // Down arrow
                float ax = arrowX + 6;
                ctx.DrawLine(ax, arrowCY - 4, ax, arrowCY + 4, arrowPaint);
                ctx.DrawLine(ax - 3, arrowCY + 1, ax, arrowCY + 4, arrowPaint);
                ctx.DrawLine(ax + 3, arrowCY + 1, ax, arrowCY + 4, arrowPaint);
            }

            // Draw "×" remove button (circle + x)
            float removeX = chipX + chipWidth - removeButtonSize - chipPadding / 2 + removeButtonSize / 2;
            float removeCenterY = chipY + chipHeight / 2;
            float removeHalf = 3.5f;

            // Small circle background for remove
            ctx.FillRoundRect(
                new GridRect(removeX - 7, removeCenterY - 7, 14, 14), 7,
                new GridPaint { Color = style.GroupPanelChipRemoveColor.WithAlpha(40) });

            var removePaint = new GridPaint
            {
                Color = style.GroupPanelChipRemoveColor,
                Style = PaintStyle.Stroke,
                StrokeWidth = 1.5f,
                IsAntiAlias = true
            };
            ctx.DrawLine(removeX - removeHalf, removeCenterY - removeHalf,
                         removeX + removeHalf, removeCenterY + removeHalf, removePaint);
            ctx.DrawLine(removeX - removeHalf, removeCenterY + removeHalf,
                         removeX + removeHalf, removeCenterY - removeHalf, removePaint);

            // Advance to next chip
            chipX += chipWidth + chipGap;

            // Draw hierarchy arrow between group chips (except after last)
            if (group != groups[^1])
            {
                float sepX = chipX + 2;
                float sepCY = chipY + chipHeight / 2;
                var sepPaint = new GridPaint
                {
                    Color = style.GroupPanelLabelColor,
                    Style = PaintStyle.Stroke,
                    StrokeWidth = 1.5f,
                    IsAntiAlias = true
                };
                // Right-pointing chevron >
                ctx.DrawLine(sepX, sepCY - 4, sepX + 5, sepCY, sepPaint);
                ctx.DrawLine(sepX + 5, sepCY, sepX, sepCY + 4, sepPaint);
                chipX += 14 + chipGap;
            }
        }
    }

    /// <summary>
    /// Draw table-level summary rows (top or bottom positioned).
    /// </summary>
    private static void DrawTableSummaryRows(
        IDrawingContext ctx,
        DataGridSource dataSource,
        List<(DataGridColumn Column, float X, int OriginalIndex)> allCols,
        ScrollState scroll,
        DataGridStyle style,
        float frozenWidth,
        IReadOnlyList<ComputedSummaryRow> summaryRows,
        float startY,
        float handleOffset = 0)
    {
        for (int i = 0; i < summaryRows.Count; i++)
        {
            var summary = summaryRows[i];
            float rowY = startY + i * style.SummaryRowHeight;

            // Background spans full viewport
            ctx.FillRect(new GridRect(0, rowY, scroll.ViewportWidth, style.SummaryRowHeight),
                new GridPaint { Color = style.SummaryRowBackgroundColor });

            // Top border line
            ctx.DrawLine(0, rowY, scroll.ViewportWidth, rowY,
                new GridPaint
                {
                    Color = style.SummaryRowBorderColor,
                    StrokeWidth = 1f,
                    Style = PaintStyle.Stroke,
                    IsAntiAlias = false
                });

            // Draw title at a fixed left position (always visible, not clipped)
            if (summary.Title != null)
            {
                var titlePaint = new GridPaint
                {
                    Color = style.SummaryRowLabelColor,
                    Font = style.SummaryRowFont,
                    IsAntiAlias = true
                };
                var titleRect = new GridRect(
                    handleOffset + style.CellPadding * 2,
                    rowY,
                    frozenWidth > 0 ? frozenWidth - style.CellPadding * 3 : 200f,
                    style.SummaryRowHeight);
                ctx.DrawTextInRect(summary.Title, titleRect, titlePaint,
                    GridTextAlignment.Left, GridVerticalAlignment.Center);
            }

            // Draw summary values in their respective columns
            // Collect values that don't fit in narrow columns to draw after the title
            var overflowValues = new List<string>();

            foreach (var (col, colX, origIdx) in allCols)
            {
                if (summary.Values.TryGetValue(col.PropertyName, out var displayText))
                {
                    float screenX = ColumnScreenX(colX, scroll, col.FreezeMode);
                    float usableWidth = col.Width - style.CellPadding * 2;

                    // If the column is too narrow for the label+value, overflow it
                    if (usableWidth < 70)
                    {
                        overflowValues.Add(displayText);
                        continue;
                    }

                    var cellRect = new GridRect(
                        screenX + style.CellPadding,
                        rowY,
                        usableWidth,
                        style.SummaryRowHeight);
                    var valuePaint = new GridPaint
                    {
                        Color = style.SummaryRowTextColor,
                        Font = style.SummaryRowFont,
                        IsAntiAlias = true
                    };
                    ctx.DrawTextInRect(displayText, cellRect, valuePaint,
                        col.TextAlignment, GridVerticalAlignment.Center);
                }
            }

            // Draw overflow values after the title
            if (overflowValues.Count > 0 && summary.Title != null)
            {
                string overflowText = "  |  " + string.Join("  |  ", overflowValues);
                var overflowPaint = new GridPaint
                {
                    Color = style.SummaryRowTextColor,
                    Font = style.SummaryRowFont,
                    IsAntiAlias = true
                };
                var titleMeasurePaint = new GridPaint
                {
                    Color = style.SummaryRowLabelColor,
                    Font = style.SummaryRowFont,
                    IsAntiAlias = true
                };
                var titleTextSize = ctx.MeasureText(summary.Title, titleMeasurePaint);
                float overflowX = handleOffset + style.CellPadding * 2 + titleTextSize.Width;
                float overflowWidth = (frozenWidth > 0 ? frozenWidth : scroll.ViewportWidth / 3) - overflowX - style.CellPadding;
                if (overflowWidth > 50)
                {
                    var overflowRect = new GridRect(overflowX, rowY, overflowWidth, style.SummaryRowHeight);
                    ctx.DrawTextInRect(overflowText, overflowRect, overflowPaint,
                        GridTextAlignment.Left, GridVerticalAlignment.Center);
                }
            }

            // Bottom border
            ctx.DrawLine(0, rowY + style.SummaryRowHeight,
                scroll.ViewportWidth, rowY + style.SummaryRowHeight,
                new GridPaint
                {
                    Color = style.SummaryRowBorderColor,
                    StrokeWidth = 1f,
                    Style = PaintStyle.Stroke,
                    IsAntiAlias = false
                });
        }
    }

    /// <summary>
    /// Draw a group summary row (displayed after each group's data rows).
    /// </summary>
    private static void DrawGroupSummaryRow(
        IDrawingContext ctx,
        DataGridSource dataSource,
        int row,
        float rowY,
        List<(DataGridColumn Column, float X, int OriginalIndex)> allCols,
        ScrollState scroll,
        DataGridStyle style,
        float frozenWidth,
        float handleOffset = 0)
    {
        var summaryInfo = dataSource.GetGroupSummaryRowInfo(row);
        if (summaryInfo == null) return;

        var (groupPath, summaryIndex) = summaryInfo.Value;
        var groupSummaries = dataSource.GetGroupSummaries(groupPath);
        if (groupSummaries == null || summaryIndex >= groupSummaries.Count) return;

        var summary = groupSummaries[summaryIndex];

        // Background spans full viewport
        ctx.FillRect(new GridRect(0, rowY, scroll.ViewportWidth, style.RowHeight),
            new GridPaint { Color = style.GroupSummaryRowBackgroundColor });

        // Bottom border
        ctx.DrawLine(0, rowY + style.RowHeight, scroll.ViewportWidth, rowY + style.RowHeight,
            new GridPaint
            {
                Color = style.SummaryRowBorderColor,
                StrokeWidth = 1f,
                Style = PaintStyle.Stroke,
                IsAntiAlias = false
            });

        // Draw title at a fixed left position (always visible at left edge)
        if (summary.Title != null)
        {
            var titlePaint = new GridPaint
            {
                Color = style.GroupSummaryRowTextColor,
                Font = style.GroupSummaryRowFont,
                IsAntiAlias = true
            };
            // Title goes in the frozen area, or a fixed 200px width if no frozen cols
            float titleWidth = frozenWidth > 0 ? frozenWidth - style.CellPadding * 3 : 200f;
            var titleRect = new GridRect(
                handleOffset + style.CellPadding * 2,
                rowY,
                titleWidth,
                style.RowHeight);
            ctx.DrawTextInRect(summary.Title, titleRect, titlePaint,
                GridTextAlignment.Left, GridVerticalAlignment.Center);
        }

        // Draw summary values in their respective columns
        // Collect values that don't fit in narrow columns to draw in a flow layout
        var overflowValues = new List<string>();

        foreach (var (col, colX, origIdx) in allCols)
        {
            if (summary.Values.TryGetValue(col.PropertyName, out var displayText))
            {
                // Use correct screen X depending on whether column is frozen
                float screenX = ColumnScreenX(colX, scroll, col.FreezeMode);
                float usableWidth = col.Width - style.CellPadding * 2;

                // If the column is too narrow for the label+value, add to overflow
                if (usableWidth < 70)
                {
                    overflowValues.Add(displayText);
                    continue;
                }

                var cellRect = new GridRect(
                    screenX + style.CellPadding,
                    rowY,
                    usableWidth,
                    style.RowHeight);
                var valuePaint = new GridPaint
                {
                    Color = style.GroupSummaryRowTextColor,
                    Font = style.GroupSummaryRowFont,
                    IsAntiAlias = true
                };
                ctx.DrawTextInRect(displayText, cellRect, valuePaint,
                    col.TextAlignment, GridVerticalAlignment.Center);
            }
        }

        // Draw overflow values after the title
        if (overflowValues.Count > 0 && summary.Title != null)
        {
            string overflowText = "  |  " + string.Join("  |  ", overflowValues);
            float titleStartX = handleOffset + style.CellPadding * 2;
            float titleWidth = frozenWidth > 0 ? frozenWidth - style.CellPadding * 3 : 200f;

            var overflowPaint = new GridPaint
            {
                Color = style.GroupSummaryRowTextColor,
                Font = style.GroupSummaryRowFont,
                IsAntiAlias = true
            };

            // Measure title to place overflow after it
            var titleMeasurePaint = new GridPaint
            {
                Color = style.GroupSummaryRowTextColor,
                Font = style.GroupSummaryRowFont,
                IsAntiAlias = true
            };
            var titleTextSize = ctx.MeasureText(summary.Title, titleMeasurePaint);
            float overflowX = titleStartX + titleTextSize.Width;
            float overflowWidth = (frozenWidth > 0 ? frozenWidth : scroll.ViewportWidth / 3) - overflowX - style.CellPadding;
            if (overflowWidth > 50)
            {
                var overflowRect = new GridRect(overflowX, rowY, overflowWidth, style.RowHeight);
                ctx.DrawTextInRect(overflowText, overflowRect, overflowPaint,
                    GridTextAlignment.Left, GridVerticalAlignment.Center);
            }
        }
    }

    private static void DrawColumnDragOverlay(
        IDrawingContext ctx,
        IReadOnlyList<DataGridColumn> columns,
        int dragColumnIndex,
        float dragScreenX,
        ScrollState scroll,
        DataGridStyle style)
    {
        if (dragColumnIndex < 0 || dragColumnIndex >= columns.Count) return;
        var col = columns[dragColumnIndex];

        var ghostRect = new GridRect(dragScreenX, 0, col.Width, style.HeaderHeight);
        ctx.FillRect(ghostRect, new GridPaint { Color = style.HeaderBackgroundColor.WithAlpha(180) });
        ctx.DrawRect(ghostRect, new GridPaint
        {
            Color = style.CurrentCellBorderColor,
            Style = PaintStyle.Stroke,
            StrokeWidth = 2f
        });

        var textRect = new GridRect(
            dragScreenX + style.CellPadding, 0,
            col.Width - style.CellPadding * 2, style.HeaderHeight);
        ctx.DrawTextInRect(col.Header, textRect, new GridPaint
        {
            Color = style.HeaderTextColor,
            Font = style.HeaderFont,
            IsAntiAlias = true
        }, GridTextAlignment.Left, GridVerticalAlignment.Center);
    }

    /// <summary>
    /// Draws the drag handle column — background strip, header, and grip icons for each visible row.
    /// </summary>
    private void DrawDragHandleColumn(
        IDrawingContext ctx,
        DataGridSource dataSource,
        int firstRow, int lastRow,
        ScrollState scroll,
        DataGridStyle style,
        float handleOffset,
        float rightHandleWidth,
        float topSummaryHeight,
        int frozenRowCount,
        float frozenRowHeight,
        float adjustedViewportHeight)
    {
        float handleWidth = style.RowDragHandleWidth;
        bool isLeft = style.RowDragHandlePosition == DragHandlePosition.Left;
        float handleX = isLeft ? 0 : scroll.ViewportWidth - rightHandleWidth;

        var handleBgPaint = _paintCache.DragHandleBackground;
        var handleHeaderBgPaint = PaintCache.BackgroundPaint(style.RowDragHandleHeaderBackgroundColor);
        var gripPaint = _paintCache.DragHandleIcon;

        // Draw handle header background
        ctx.FillRect(new GridRect(handleX, 0, handleWidth, style.HeaderHeight), handleHeaderBgPaint);

        // Draw handle background for the full data area
        float dataAreaTop = style.HeaderHeight + topSummaryHeight;
        ctx.FillRect(new GridRect(handleX, dataAreaTop, handleWidth, adjustedViewportHeight - dataAreaTop), handleBgPaint);

        // Draw grip icons for frozen rows
        for (int row = 0; row < frozenRowCount && row < dataSource.RowCount; row++)
        {
            if (dataSource.IsNonDataRow(row)) continue;
            float rowY = dataAreaTop + row * style.RowHeight;
            DrawGripIcon(ctx, handleX, rowY, handleWidth, style.RowHeight, gripPaint);
        }

        // Draw grip icons for scrollable visible rows (clipped below frozen rows)
        float scrollableAreaTop = dataAreaTop + frozenRowHeight;
        for (int row = firstRow; row <= lastRow; row++)
        {
            if (dataSource.IsNonDataRow(row)) continue;
            float rowY = dataAreaTop + frozenRowHeight + (row - frozenRowCount) * style.RowHeight - scroll.OffsetY;
            if (rowY + style.RowHeight <= scrollableAreaTop || rowY > adjustedViewportHeight) continue;
            DrawGripIcon(ctx, handleX, rowY, handleWidth, style.RowHeight, gripPaint);
        }

        // Draw a subtle vertical divider between handle and columns
        float dividerX = isLeft ? handleWidth : handleX;
        ctx.DrawLine(dividerX, 0, dividerX, adjustedViewportHeight, new GridPaint
        {
            Color = style.GridLineColor,
            StrokeWidth = 1f,
            Style = PaintStyle.Stroke
        });
    }

    /// <summary>
    /// Draws a grip/drag icon (2-column × 3-row dot pattern) centered in the given cell area.
    /// </summary>
    private static void DrawGripIcon(
        IDrawingContext ctx, float cellX, float cellY, float cellWidth, float cellHeight, GridPaint paint)
    {
        float centerX = cellX + cellWidth / 2;
        float centerY = cellY + cellHeight / 2;
        float dotRadius = 1.5f;
        float colSpacing = 5f;  // horizontal distance between dot centers
        float rowSpacing = 5f;  // vertical distance between dot centers

        // Draw 2-column × 3-row dot grid (modern grab handle)
        for (int row = -1; row <= 1; row++)
        {
            for (int col = 0; col <= 1; col++)
            {
                float dx = centerX + (col - 0.5f) * colSpacing;
                float dy = centerY + row * rowSpacing;
                ctx.FillRoundRect(
                    new GridRect(dx - dotRadius, dy - dotRadius, dotRadius * 2, dotRadius * 2),
                    dotRadius, paint);
            }
        }
    }

    /// <summary>
    /// Draws a ghost row at the drag position and an insertion indicator line at the drop target.
    /// </summary>
    private void DrawRowDragOverlay(
        IDrawingContext ctx,
        DataGridSource dataSource,
        IReadOnlyList<DataGridColumn> columns,
        int dragRowIndex,
        float dragScreenY,
        ScrollState scroll,
        DataGridStyle style,
        float frozenWidth,
        float rightFrozenWidth,
        float topSummaryHeight,
        int frozenRowCount,
        float handleOffset = 0,
        float rightHandleWidth = 0)
    {
        int totalRows = dataSource.RowCount;
        if (dragRowIndex < 0 || dragRowIndex >= totalRows) return;

        float rowWidth = scroll.ViewportWidth;
        float rowHeight = style.RowHeight;

        // ── Ghost row ──
        var ghostRect = new GridRect(handleOffset, dragScreenY, rowWidth - handleOffset - rightHandleWidth, rowHeight);
        ctx.FillRect(ghostRect, new GridPaint { Color = style.RowDragOverlayColor });
        ctx.DrawRect(ghostRect, new GridPaint
        {
            Color = style.RowDragBorderColor,
            Style = PaintStyle.Stroke,
            StrokeWidth = 2f
        });

        // Draw cell text in the ghost row
        float cellX = handleOffset - scroll.OffsetX;
        for (int c = 0; c < columns.Count; c++)
        {
            if (!columns[c].IsVisible) continue;
            float colWidth = columns[c].Width;

            // Skip rendering cells outside the visible area
            if (cellX + colWidth > handleOffset && cellX < rowWidth - rightHandleWidth)
            {
                string cellText = "";
                try
                {
                    if (!dataSource.IsNonDataRow(dragRowIndex))
                    {
                        cellText = dataSource.GetCellDisplayText(dragRowIndex, columns[c]);
                    }
                }
                catch { /* guard against group rows */ }

                if (!string.IsNullOrEmpty(cellText))
                {
                    var textRect = new GridRect(
                        cellX + style.CellPadding, dragScreenY,
                        colWidth - style.CellPadding * 2, rowHeight);
                    ctx.DrawTextInRect(cellText, textRect, new GridPaint
                    {
                        Color = style.CellTextColor.WithAlpha(200),
                        Font = style.CellFont,
                        IsAntiAlias = true
                    }, GridTextAlignment.Left, GridVerticalAlignment.Center);
                }
            }

            cellX += colWidth;
        }

        // ── Drop indicator line ──
        // Determine where the row would be inserted based on the center of the ghost
        float ghostCenterY = dragScreenY + rowHeight / 2;
        float dataAreaTop = style.HeaderHeight + topSummaryHeight;
        float frozenRowHeight = frozenRowCount * style.RowHeight;

        int dropIndex;
        if (frozenRowCount > 0 && ghostCenterY >= dataAreaTop && ghostCenterY < dataAreaTop + frozenRowHeight)
        {
            dropIndex = (int)((ghostCenterY - dataAreaTop) / style.RowHeight);
            dropIndex = Math.Clamp(dropIndex, 0, frozenRowCount - 1);
        }
        else
        {
            float scrollableDataTop = dataAreaTop + frozenRowHeight;
            dropIndex = (int)((ghostCenterY - scrollableDataTop + scroll.OffsetY) / style.RowHeight) + frozenRowCount;
        }
        dropIndex = Math.Clamp(dropIndex, 0, totalRows);

        // Compute the Y position of the drop indicator
        float indicatorY;
        if (dropIndex < frozenRowCount)
        {
            indicatorY = dataAreaTop + dropIndex * style.RowHeight;
        }
        else
        {
            indicatorY = dataAreaTop + frozenRowHeight + (dropIndex - frozenRowCount) * style.RowHeight - scroll.OffsetY;
        }

        // Draw the horizontal drop indicator line
        float indicatorLeft = handleOffset;
        float indicatorRight = rowWidth - rightHandleWidth;
        ctx.DrawLine(indicatorLeft, indicatorY, indicatorRight, indicatorY, new GridPaint
        {
            Color = style.RowDropIndicatorColor,
            StrokeWidth = style.RowDropIndicatorWidth,
            Style = PaintStyle.Stroke
        });

        // Draw small triangles at the ends of the indicator line for visibility
        float triSize = 6f;
        // Left triangle
        ctx.DrawLine(indicatorLeft, indicatorY - triSize, indicatorLeft, indicatorY + triSize, new GridPaint
        {
            Color = style.RowDropIndicatorColor,
            StrokeWidth = style.RowDropIndicatorWidth,
            Style = PaintStyle.Stroke
        });
        // Right triangle
        ctx.DrawLine(indicatorRight, indicatorY - triSize, indicatorRight, indicatorY + triSize, new GridPaint
        {
            Color = style.RowDropIndicatorColor,
            StrokeWidth = style.RowDropIndicatorWidth,
            Style = PaintStyle.Stroke
        });
    }
}

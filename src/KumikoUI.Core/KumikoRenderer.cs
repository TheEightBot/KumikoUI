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
public class KumikoRenderer
{
    private readonly KumikoLayoutEngine _layout = new();
    private readonly PaintCache _paintCache = new();

    // The effective style of the cell being drawn when resolvers override the column's style; reused per cell
    private readonly KumikoCellStyle _effectiveCellStyle = new();
    private readonly Dictionary<KumikoColumnType, ICellRenderer> _cellRenderers = new();

    // Reusable per-frame column lists — avoids allocating new Lists every Render() call
    private readonly List<(KumikoColumn Column, float X, int OriginalIndex)> _frozenCols = new();
    private readonly List<(KumikoColumn Column, float X, int OriginalIndex)> _rightFrozenCols = new();
    private readonly List<(KumikoColumn Column, float X, int OriginalIndex)> _scrollableCols = new();
    private readonly List<(KumikoColumn Column, float X, int OriginalIndex)> _allCols = new();

    // Paints whose color varies per row, cell or column (resolver and per-column style overrides); set just
    // before each draw, so overrides allocate nothing. Drawing contexts read a paint during the call only.
    private readonly KumikoPaint _rowBackground = new();
    private readonly KumikoPaint _cellBackground = new();
    private readonly KumikoPaint _headerBackground = new();
    private readonly KumikoPaint _headerText = new() { IsAntiAlias = true };

    // Summary values that do not fit their column, reused for every summary row
    private readonly List<string> _overflowValues = new();

    // Group headers pinned at the top of the data area this frame (#174), outermost first; reused every frame
    private readonly List<PinnedGroupHeader> _pinnedGroupHeaders = new();

    // "1".."16": multi-sort order badges without formatting a number per frame
    private static readonly string[] s_sortOrderLabels = CreateSortOrderLabels(16);

    private static string[] CreateSortOrderLabels(int count)
    {
        var labels = new string[count + 1];
        for (int i = 0; i <= count; i++)
            labels[i] = i.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return labels;
    }

    private static string SortOrderLabel(int order) =>
        order >= 0 && order < s_sortOrderLabels.Length ? s_sortOrderLabels[order] : order.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private KumikoFrameStats? _frameStats;

    /// <summary>
    /// Draws a frame-time readout (render time, frames per second, bytes allocated per frame) in the top-right
    /// corner of the grid (#70). Off by default, and then costs one boolean check per frame. The readout is
    /// averaged over short windows, so it adds no per-frame allocation.
    /// </summary>
    public bool ShowFrameStats { get; set; }

    /// <summary>The overlay's counters, readable for logging; they update only while <see cref="ShowFrameStats"/> is true.</summary>
    public KumikoFrameStats FrameStats => _frameStats ??= new KumikoFrameStats();

    /// <summary>Create a renderer with the built-in cell renderers registered for every column type.</summary>
    public KumikoRenderer()
    {
        var textRenderer = new TextCellRenderer();
        _cellRenderers[KumikoColumnType.Text] = textRenderer;
        _cellRenderers[KumikoColumnType.Numeric] = textRenderer;
        _cellRenderers[KumikoColumnType.Date] = textRenderer;
        _cellRenderers[KumikoColumnType.Template] = textRenderer;
        _cellRenderers[KumikoColumnType.ComboBox] = textRenderer;
        _cellRenderers[KumikoColumnType.Picker] = textRenderer;
        _cellRenderers[KumikoColumnType.Boolean] = new BooleanCellRenderer();
        _cellRenderers[KumikoColumnType.Image] = new ImageCellRenderer();
    }

    /// <summary>Register a custom cell renderer for a column type.</summary>
    public void SetCellRenderer(KumikoColumnType columnType, ICellRenderer renderer)
    {
        _cellRenderers[columnType] = renderer;
    }

    /// <summary>Get the cell renderer for a column type.</summary>
    public ICellRenderer GetCellRenderer(KumikoColumnType columnType)
    {
        return _cellRenderers.TryGetValue(columnType, out var renderer)
            ? renderer
            : _cellRenderers[KumikoColumnType.Text];
    }

    /// <summary>Render the full grid given the current state.</summary>
    public void Render(
        IDrawingContext ctx,
        KumikoSource dataSource,
        ScrollState scroll,
        KumikoSelectionModel selection,
        KumikoStyle style,
        int dragColumnIndex = -1,
        float dragColumnScreenX = 0,
        int dragRowIndex = -1,
        float dragRowScreenY = 0,
        EditSession? editSession = null,
        PopupManager? popupManager = null)
    {
        var stats = ShowFrameStats ? FrameStats : null;
        stats?.BeginFrame();

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

        _layout.ComputeColumnWidths(columns, effectiveViewportWidth, ctx, style);
        scroll.ContentWidth = _layout.GetContentWidth(columns) + handleOffset + rightHandleWidth;
        scroll.ContentHeight = _layout.GetContentHeight(dataSource.RowCount, style)
            + groupPanelOffset + topSummaryHeight + bottomSummaryHeight;
        scroll.ClampOffset();

        _paintCache.Update(style);

        ctx.FillRect(new KumikoRect(0, 0, scroll.ViewportWidth, scroll.ViewportHeight),
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
        dataSource.PrepareCellTextCache(frozenRowCount, Math.Max(0, lastRow - firstRow + 1));

        // Vertical extent rows may paint in: below header + top summaries, above a pinned footer.
        float dataClipBottom = adjustedViewportHeight - (style.PinBottomSummaryRows ? bottomSummaryHeight : 0);
        float dataClipHeight = Math.Max(0, dataClipBottom - dataAreaTop);
        var dataClip = new KumikoRect(0, dataAreaTop, scroll.ViewportWidth, dataClipHeight);

        // Sticky group headers (#174): pinned below the frozen rows, over the scrolling rows.
        float scrollingRowsTop = dataAreaTop + frozenRowHeight;
        float pinnedBandBottom = PinnedGroupHeaders.Compute(dataSource, style, scrollingRowsTop, scroll.OffsetY,
            frozenRowCount, _pinnedGroupHeaders);

        // ── Pass 1: Scrollable columns area ──────────────────────
        ctx.Save();
        float scrollClipLeft = frozenWidth + handleOffset;
        float scrollClipWidth = effectiveViewportWidth - frozenWidth - rightFrozenWidth;
        // Always clipped to the pane (also without frozen columns): rows partly above the data area must
        // not reach the grouping panel, which is drawn before the translation.
        ctx.ClipRect(new KumikoRect(scrollClipLeft, 0, scrollClipWidth, adjustedViewportHeight));

        // Scrolling rows are further clipped to the data area (below the header and top summaries, above a
        // pinned footer); the header is drawn after them with only the pane clip.
        ctx.Save();
        ctx.ClipRect(dataClip);
        DrawRows(ctx, dataSource, scrollableCols, firstRow, lastRow, scroll, selection, style, false, frozenWidth, editSession, topSummaryHeight, frozenRowCount);
        DrawCurrentCellBorder(ctx, scrollableCols, scroll, selection, style, false, frozenWidth, dataSource, topSummaryHeight, frozenRowCount);
        DrawGridLines(ctx, scrollableCols, firstRow, lastRow, scroll, style, dataSource.RowCount, false, frozenWidth, dataSource, topSummaryHeight, frozenRowCount);
        ctx.Restore();
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
            ctx.ClipRect(new KumikoRect(handleOffset, 0, frozenWidth, adjustedViewportHeight));

            ctx.FillRect(new KumikoRect(handleOffset, 0, frozenWidth, adjustedViewportHeight),
                _paintCache.FrozenColumnBackground);
            ctx.Save();
            ctx.ClipRect(dataClip);
            DrawRows(ctx, dataSource, frozenCols, firstRow, lastRow, scroll, selection, style, true, frozenWidth, editSession, topSummaryHeight, frozenRowCount);
            DrawCurrentCellBorder(ctx, frozenCols, scroll, selection, style, true, frozenWidth, dataSource, topSummaryHeight, frozenRowCount);
            DrawGridLines(ctx, frozenCols, firstRow, lastRow, scroll, style, dataSource.RowCount, true, frozenWidth, dataSource, topSummaryHeight, frozenRowCount);
            ctx.Restore();
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
            ctx.ClipRect(new KumikoRect(rightFrozenLeft, 0, rightFrozenWidth, adjustedViewportHeight));

            DrawRightFrozenBackground(ctx, rightFrozenLeft, rightFrozenWidth, scroll, style);
            ctx.Save();
            ctx.ClipRect(dataClip);
            DrawRows(ctx, dataSource, rightFrozenCols, firstRow, lastRow, scroll, selection, style, ColumnFreezeMode.Right, frozenWidth, editSession, topSummaryHeight, frozenRowCount);
            DrawCurrentCellBorder(ctx, rightFrozenCols, scroll, selection, style, ColumnFreezeMode.Right, frozenWidth, dataSource, topSummaryHeight, frozenRowCount);
            DrawGridLines(ctx, rightFrozenCols, firstRow, lastRow, scroll, style, dataSource.RowCount, ColumnFreezeMode.Right, frozenWidth, dataSource, topSummaryHeight, frozenRowCount, rightFrozenLeft, rightFrozenWidth);
            ctx.Restore();
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

        if (dataSource.BottomSummaryCount > 0 && !style.PinBottomSummaryRows)
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
            ctx.ClipRect(new KumikoRect(0, dataClipTop, scroll.ViewportWidth, adjustedViewportHeight - dataClipTop));

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

        // ── Sticky group headers (#174): on top of every pane's rows, inside the data area below the frozen rows.
        // Innermost first, so a pushed-up inner header slides under the outer ones.
        if (_pinnedGroupHeaders.Count > 0)
        {
            ctx.Save();
            ctx.ClipRect(new KumikoRect(0, scrollingRowsTop, scroll.ViewportWidth, Math.Max(0, dataClipBottom - scrollingRowsTop)));
            for (int i = _pinnedGroupHeaders.Count - 1; i >= 0; i--)
            {
                var pinned = _pinnedGroupHeaders[i];
                DrawGroupHeaderRow(ctx, dataSource, pinned.Row, pinned.Y, allCols, scroll, style, frozenWidth, handleOffset);
            }
            ctx.Restore();
        }

        // ── Draw drag handle column ──────────────────────────────
        if (style.ShowRowDragHandle)
        {
            DrawDragHandleColumn(ctx, dataSource, firstRow, lastRow, scroll, style,
                handleOffset, rightHandleWidth, topSummaryHeight, frozenRowCount, frozenRowHeight,
                adjustedViewportHeight, _pinnedGroupHeaders.Count > 0 ? pinnedBandBottom : float.NegativeInfinity);
        }

        // Pinned bottom summary (footer): drawn on top of all scrolling data — and after the drag-handle
        // column so row grip icons never paint over it — at the viewport bottom. Its opaque, full-width
        // SummaryRowBackgroundColor covers the rows behind it; the scroll range already reserves
        // bottomSummaryHeight so the last row scrolls to exactly the footer's top edge. Hit testing and
        // keyboard ensure-visible exclude this band (see KumikoHitTester / EnsureCurrentCellVisible).
        if (dataSource.BottomSummaryCount > 0 && style.PinBottomSummaryRows)
        {
            float pinnedBottomSummaryY = adjustedViewportHeight - bottomSummaryHeight;
            DrawTableSummaryRows(ctx, dataSource, allCols, scroll, style, frozenWidth,
                dataSource.ComputedBottomSummaries, pinnedBottomSummaryY, handleOffset);
        }

        if (dragColumnIndex >= 0)
            DrawColumnDragOverlay(ctx, columns, dragColumnIndex, dragColumnScreenX, scroll, style);

        if (dragRowIndex >= 0)
            DrawRowDragOverlay(ctx, dataSource, columns, dragRowIndex, dragRowScreenY, scroll, style,
                frozenWidth, rightFrozenWidth, topSummaryHeight, frozenRowCount,
                handleOffset, rightHandleWidth);

        // Restore from grouping panel offset
        ctx.Restore();

        // Keep the editor on its cell (it moves when the grid scrolls, sorts or regroups), hidden while
        // the cell is scrolled out of view, then draw it on top of everything (absolute coordinates).
        if (editSession is { IsEditing: true })
        {
            float pinnedFooterHeight = style.PinBottomSummaryRows ? bottomSummaryHeight : 0;
            editSession.PlaceEditor(GetEditorCellBounds(editSession.EditCell, scroll, style,
                groupPanelOffset, dataAreaTop, frozenRowCount, frozenRowHeight, pinnedBandBottom,
                scrollClipLeft, scrollClipLeft + scrollClipWidth, adjustedViewportHeight - pinnedFooterHeight));
        }
        editSession?.DrawEditor(ctx);

        // Draw any popups (filter, etc.) on top of everything
        popupManager?.DrawPopups(ctx);

        if (stats != null)
        {
            stats.EndFrame();
            stats.Draw(ctx, scroll.ViewportWidth);
        }
    }

    /// <summary>
    /// Returns screen X for a column, handling left-frozen, right-frozen, and scrollable.
    /// For right-frozen columns, the X is already an absolute screen position from GetVisibleRightFrozenColumns.
    /// </summary>
    /// <summary>
    /// The edited cell's screen rectangle this frame (absolute coordinates), or null when the cell is
    /// scrolled out of its pane or band, or its column is not laid out.
    /// </summary>
    private KumikoRect? GetEditorCellBounds(CellPosition cell, ScrollState scroll, KumikoStyle style,
        float groupPanelOffset, float dataAreaTop, int frozenRowCount, float frozenRowHeight, float scrollingRowsTop,
        float scrollPaneLeft, float scrollPaneRight, float dataAreaBottom)
    {
        if (!cell.IsValid) return null;

        // The column's layout this frame; absent when it is scrolled out horizontally or hidden.
        var match = Find(_frozenCols, cell.Column) ?? Find(_scrollableCols, cell.Column) ?? Find(_rightFrozenCols, cell.Column);
        if (match is not { } found) return null;

        float x = ColumnScreenX(found.X, scroll, found.Column.FreezeMode);
        float width = found.Column.Width;
        if (found.Column.FreezeMode == ColumnFreezeMode.None && (x + width <= scrollPaneLeft || x >= scrollPaneRight))
            return null;

        float y;
        if (cell.Row < frozenRowCount)
        {
            y = dataAreaTop + cell.Row * style.RowHeight;
        }
        else
        {
            y = dataAreaTop + frozenRowHeight + (cell.Row - frozenRowCount) * style.RowHeight - scroll.OffsetY;
            // Hidden once the row is entirely above the scrolling rows' top: the frozen rows, or the sticky group
            // headers pinned over it (#174).
            if (y + style.RowHeight <= scrollingRowsTop || y >= dataAreaBottom)
                return null;
        }

        return new KumikoRect(x, y + groupPanelOffset, width, style.RowHeight);
    }

    private static (KumikoColumn Column, float X, int OriginalIndex)? Find(
        List<(KumikoColumn Column, float X, int OriginalIndex)> columns, int originalIndex)
    {
        foreach (var entry in columns)
        {
            if (entry.OriginalIndex == originalIndex) return entry;
        }
        return null;
    }


    /// <summary>
    /// The style of one cell: the cell resolver's values over the column's <see cref="KumikoColumn.CellStyle"/>,
    /// with the row resolver's text color and font under both. With no resolver override this is the column's
    /// own style; otherwise the values are composed into one instance reused for every cell, so the column's
    /// style and the resolvers' results (which may be shared or cached) are never modified. It is valid only
    /// while the cell is drawn.
    /// </summary>
    private KumikoCellStyle? ResolveCellStyle(KumikoStyle style, KumikoColumn col, object? dataItem, KumikoRowStyle? rowStyle)
    {
        var columnStyle = col.CellStyle;
        var cellStyle = style.CellStyleResolver != null && dataItem != null ? style.CellStyleResolver(dataItem, col) : null;
        bool rowOverrides = rowStyle != null && (rowStyle.TextColor != null || rowStyle.Font != null);
        if (cellStyle == null && !rowOverrides)
            return columnStyle;

        var effective = _effectiveCellStyle;
        effective.BackgroundColor = cellStyle?.BackgroundColor ?? columnStyle?.BackgroundColor;
        effective.TextColor = cellStyle?.TextColor ?? columnStyle?.TextColor ?? rowStyle?.TextColor;
        effective.Font = cellStyle?.Font ?? columnStyle?.Font ?? rowStyle?.Font;
        effective.Padding = cellStyle?.Padding ?? columnStyle?.Padding;
        effective.BorderColor = cellStyle?.BorderColor ?? columnStyle?.BorderColor;
        effective.BorderWidth = cellStyle?.BorderWidth ?? columnStyle?.BorderWidth;
        effective.TextAlignment = cellStyle?.TextAlignment ?? columnStyle?.TextAlignment;
        return effective;
    }

    private static float ColumnScreenX(float colContentX, ScrollState scroll, ColumnFreezeMode freezeMode)
    {
        return freezeMode switch
        {
            ColumnFreezeMode.Left => colContentX,
            ColumnFreezeMode.Right => colContentX, // already absolute screen X
            _ => colContentX - scroll.OffsetX
        };
    }

    private void DrawRightFrozenBackground(
        IDrawingContext ctx, float rightFrozenLeft, float rightFrozenWidth, ScrollState scroll, KumikoStyle style)
    {
        ctx.FillRect(new KumikoRect(rightFrozenLeft, 0, rightFrozenWidth, scroll.ViewportHeight),
            _paintCache.RightFrozenColumnBackground);
    }

    private void DrawFrozenColumnDivider(
        IDrawingContext ctx, float frozenWidth, ScrollState scroll, KumikoStyle style)
    {
        ctx.DrawLine(
            frozenWidth, 0,
            frozenWidth, scroll.ViewportHeight,
            _paintCache.FrozenColumnDivider);
    }

    private void DrawRightFrozenColumnDivider(
        IDrawingContext ctx, float rightFrozenLeft, ScrollState scroll, KumikoStyle style)
    {
        ctx.DrawLine(
            rightFrozenLeft, 0,
            rightFrozenLeft, scroll.ViewportHeight,
            _paintCache.RightFrozenColumnDivider);
    }

    private void DrawFrozenRowDivider(
        IDrawingContext ctx, float dividerY, ScrollState scroll, KumikoStyle style)
    {
        ctx.DrawLine(
            0, dividerY,
            scroll.ViewportWidth, dividerY,
            _paintCache.FrozenRowDivider);
    }

    private void DrawHeader(
        IDrawingContext ctx,
        List<(KumikoColumn Column, float X, int OriginalIndex)> visibleCols,
        ScrollState scroll,
        KumikoStyle style,
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
        List<(KumikoColumn Column, float X, int OriginalIndex)> visibleCols,
        ScrollState scroll,
        KumikoStyle style,
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

        ctx.FillRect(new KumikoRect(areaLeft, 0, areaWidth, style.HeaderHeight),
            _paintCache.HeaderBackground);

        ctx.DrawLine(areaLeft, style.HeaderHeight, areaLeft + areaWidth, style.HeaderHeight,
            _paintCache.HeaderBorder);

        var headerPaint = _paintCache.HeaderText;

        foreach (var (col, colX, _) in visibleCols)
        {
            float screenX = ColumnScreenX(colX, scroll, col.FreezeMode);

            // Per-column header style overrides
            var hStyle = col.HeaderCellStyle;
            var colPaint = headerPaint;
            if (hStyle != null && (hStyle.TextColor.HasValue || hStyle.Font != null))
            {
                colPaint = _headerText;
                colPaint.Color = hStyle.TextColor ?? style.HeaderTextColor;
                colPaint.Font = hStyle.Font ?? style.HeaderFont;
            }

            // Per-column header background
            if (hStyle?.BackgroundColor != null)
            {
                _headerBackground.Color = hStyle.BackgroundColor.Value;
                ctx.FillRect(new KumikoRect(screenX, 0, col.Width, style.HeaderHeight), _headerBackground);
            }

            float padding = hStyle?.Padding ?? style.HeaderPadding;

            // Reserve right-side space for the glyphs this column actually shows (sort arrow, filter icon),
            // so the header text never runs under them and is not cut short for glyphs that are not drawn.
            float rightReserved = SortGlyphSpace(col) + FilterGlyphSpace(col);

            var cellRect = new KumikoRect(
                screenX + padding,
                0,
                col.Width - padding * 2 - rightReserved,
                style.HeaderHeight);

            ctx.DrawTextInRect(col.Header, cellRect, colPaint,
                KumikoTextAlignment.Left, KumikoVerticalAlignment.Center);
        }
    }

    // Header width the sort arrow takes when it is drawn (the column is sorted): arrow (8) + gap (4).
    private static float SortGlyphSpace(KumikoColumn col) => col.SortDirection != KumikoSortDirection.None ? KumikoLayoutEngine.SortGlyphSpace : 0f;

    // Header width the filter icon takes when it is drawn (the column allows filtering): icon (10) + gap (4).
    private static float FilterGlyphSpace(KumikoColumn col) => col.AllowFiltering ? KumikoLayoutEngine.FilterGlyphSpace : 0f;

    private void DrawRows(
        IDrawingContext ctx,
        KumikoSource dataSource,
        List<(KumikoColumn Column, float X, int OriginalIndex)> visibleCols,
        int firstRow, int lastRow,
        ScrollState scroll,
        KumikoSelectionModel selection,
        KumikoStyle style,
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
        KumikoSource dataSource,
        List<(KumikoColumn Column, float X, int OriginalIndex)> visibleCols,
        int firstRow, int lastRow,
        ScrollState scroll,
        KumikoSelectionModel selection,
        KumikoStyle style,
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
            object dataItem = dataSource.GetItem(row);
            KumikoRowStyle? dynamicRowStyle = style.RowStyleResolver?.Invoke(dataItem);

            bool isFrozenLeft = freezeMode == ColumnFreezeMode.Left;
            bool isSelected = selection.IsRowSelected(row);
            bool isFocused = hasCurrentRow && row == currentRow;

            // Resolve row background — prefer cached PaintCache instances for common cases
            KumikoPaint rowBgPaint;
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
                rowBgPaint = _rowBackground;
                rowBgPaint.Color = dynamicRowStyle.BackgroundColor.Value;
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
                new KumikoRect(0, rowY, scroll.ViewportWidth, style.RowHeight),
                rowBgPaint);

            DrawRowCells(ctx, dataSource, visibleCols, row, rowY, dataItem, dynamicRowStyle, scroll, style,
                isSelected, editSession);
        }
    }

    /// <summary>
    /// Draw the cells of one data row: each cell's style background, then its content through the column's
    /// <see cref="ICellRenderer"/>, with the value and display text from the source's display text cache.
    /// </summary>
    private void DrawRowCells(
        IDrawingContext ctx,
        KumikoSource dataSource,
        List<(KumikoColumn Column, float X, int OriginalIndex)> visibleCols,
        int row,
        float rowY,
        object dataItem,
        KumikoRowStyle? dynamicRowStyle,
        ScrollState scroll,
        KumikoStyle style,
        bool isSelected,
        EditSession? editSession)
    {
        foreach (var (col, colX, origIdx) in visibleCols)
        {
            float screenX = ColumnScreenX(colX, scroll, col.FreezeMode);

            // The column's style with the resolvers' overrides, never written into either
            var colCellStyle = ResolveCellStyle(style, col, dataItem, dynamicRowStyle);

            float cellPadding = colCellStyle?.Padding ?? style.CellPadding;

            // Per-column cell background
            if (colCellStyle?.BackgroundColor != null)
            {
                _cellBackground.Color = colCellStyle.BackgroundColor.Value;
                ctx.FillRect(new KumikoRect(screenX, rowY, col.Width, style.RowHeight), _cellBackground);
            }

            var cellRect = new KumikoRect(
                screenX + cellPadding,
                rowY,
                col.Width - cellPadding * 2,
                style.RowHeight);

            // Skip rendering content for the cell being edited (editor draws on top)
            if (editSession != null && editSession.IsCellBeingEdited(row, origIdx))
                continue;

            var renderer = col.CustomCellRenderer ?? GetCellRenderer(col.ColumnType);
            string displayText = dataSource.GetCachedCellText(row, dataItem, col, out object? value);

            var cell = new CellRenderContext(cellRect, row, value, displayText, col, style, colCellStyle, isSelected,
                _paintCache, _paintCache.CellTextPaint(colCellStyle, style),
                colCellStyle?.TextAlignment ?? col.TextAlignment);
            renderer.Render(ctx, in cell);
        }
    }

    /// <summary>
    /// Draw frozen rows (top N sticky rows) at fixed Y positions below header + top summaries.
    /// These rows don't scroll vertically.
    /// </summary>
    private void DrawFrozenRows(
        IDrawingContext ctx,
        KumikoSource dataSource,
        List<(KumikoColumn Column, float X, int OriginalIndex)> visibleCols,
        int frozenRowCount,
        ScrollState scroll,
        KumikoSelectionModel selection,
        KumikoStyle style,
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
        KumikoSource dataSource,
        List<(KumikoColumn Column, float X, int OriginalIndex)> visibleCols,
        int frozenRowCount,
        ScrollState scroll,
        KumikoSelectionModel selection,
        KumikoStyle style,
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
            object dataItem = dataSource.GetItem(row);
            KumikoRowStyle? dynamicRowStyle = style.RowStyleResolver?.Invoke(dataItem);

            bool isSelected = selection.IsRowSelected(row);
            bool isFocused = hasCurrentRow && row == currentRow;

            // Resolve frozen row background — prefer cached PaintCache instances
            KumikoPaint rowBgPaint;
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
                rowBgPaint = _rowBackground;
                rowBgPaint.Color = dynamicRowStyle.BackgroundColor.Value;
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
                new KumikoRect(0, rowY, scroll.ViewportWidth, style.RowHeight),
                rowBgPaint);

            DrawRowCells(ctx, dataSource, visibleCols, row, rowY, dataItem, dynamicRowStyle, scroll, style,
                isSelected, editSession);
        }
    }

    /// <summary>
    /// Draw a group header row spanning the full width with chevron, group text, and item count.
    /// </summary>
    private void DrawGroupHeaderRow(
        IDrawingContext ctx,
        KumikoSource dataSource,
        int row,
        float rowY,
        List<(KumikoColumn Column, float X, int OriginalIndex)> allCols,
        ScrollState scroll,
        KumikoStyle style,
        float frozenWidth,
        float handleOffset = 0)
    {
        var groupInfo = dataSource.GetGroupHeaderInfo(row);
        if (groupInfo == null) return;

        // Group header background spans full viewport width
        float areaLeft = 0;
        float areaWidth = scroll.ViewportWidth;

        ctx.FillRect(
            new KumikoRect(areaLeft, rowY, areaWidth, style.RowHeight),
            _paintCache.GroupHeaderBackground);

        // Bottom border line
        ctx.DrawLine(areaLeft, rowY + style.RowHeight, areaLeft + areaWidth, rowY + style.RowHeight,
            _paintCache.GridLine);

        // Indent based on group level
        float indent = handleOffset + style.CellPadding + groupInfo.Level * style.GroupIndentWidth;

        // Draw expand/collapse indicator with background pill
        float chevronSize = style.GroupChevronSize;
        float chevronX = indent;
        float chevronCenterY = rowY + style.RowHeight / 2;

        // Draw rounded-rect background behind the indicator
        float pillW = chevronSize + 8f;
        float pillH = chevronSize + 6f;
        var pillRect = new KumikoRect(
            chevronX - 4f, chevronCenterY - pillH / 2, pillW, pillH);
        ctx.FillRoundRect(pillRect, 4f, _paintCache.GroupChevronBackground);

        // Draw filled triangle using thick lines converging to a solid shape
        var chevronPaint = _paintCache.GroupChevron;

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

        // Include caption summary if available
        string? captionText = dataSource.GetCaptionSummaryText(groupInfo.GroupPath);
        string label = groupInfo.GetLabel(captionText);

        var textRect = new KumikoRect(textX, rowY, areaWidth - textX - style.CellPadding, style.RowHeight);
        ctx.DrawTextInRect(label, textRect, _paintCache.GroupHeaderText,
            KumikoTextAlignment.Left, KumikoVerticalAlignment.Center);
    }

    private void DrawGridLines(
        IDrawingContext ctx,
        List<(KumikoColumn Column, float X, int OriginalIndex)> visibleCols,
        int firstRow, int lastRow,
        ScrollState scroll,
        KumikoStyle style,
        int totalRows,
        bool isFrozen,
        float frozenWidth,
        KumikoSource? dataSource = null,
        float topSummaryHeight = 0,
        int frozenRowCount = 0)
    {
        DrawGridLines(ctx, visibleCols, firstRow, lastRow, scroll, style, totalRows,
            isFrozen ? ColumnFreezeMode.Left : ColumnFreezeMode.None,
            frozenWidth, dataSource, topSummaryHeight, frozenRowCount, 0, 0);
    }

    private void DrawGridLines(
        IDrawingContext ctx,
        List<(KumikoColumn Column, float X, int OriginalIndex)> visibleCols,
        int firstRow, int lastRow,
        ScrollState scroll,
        KumikoStyle style,
        int totalRows,
        ColumnFreezeMode freezeMode,
        float frozenWidth,
        KumikoSource? dataSource = null,
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
        List<(KumikoColumn Column, float X, int OriginalIndex)> visibleCols,
        ScrollState scroll,
        KumikoSelectionModel selection,
        KumikoStyle style,
        bool isFrozen,
        float frozenWidth,
        KumikoSource? dataSource = null,
        float topSummaryHeight = 0,
        int frozenRowCount = 0)
    {
        DrawCurrentCellBorder(ctx, visibleCols, scroll, selection, style,
            isFrozen ? ColumnFreezeMode.Left : ColumnFreezeMode.None,
            frozenWidth, dataSource, topSummaryHeight, frozenRowCount);
    }

    private void DrawCurrentCellBorder(
        IDrawingContext ctx,
        List<(KumikoColumn Column, float X, int OriginalIndex)> visibleCols,
        ScrollState scroll,
        KumikoSelectionModel selection,
        KumikoStyle style,
        ColumnFreezeMode freezeMode,
        float frozenWidth,
        KumikoSource? dataSource = null,
        float topSummaryHeight = 0,
        int frozenRowCount = 0)
    {
        if (!selection.CurrentCell.IsValid) return;

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
                var cellRect = new KumikoRect(screenX, rowY, col.Width, style.RowHeight);

                ctx.DrawRect(cellRect, _paintCache.CurrentCellBorder);
                return;
            }
        }
    }

    private void DrawSortIndicators(
        IDrawingContext ctx,
        List<(KumikoColumn Column, float X, int OriginalIndex)> visibleCols,
        ScrollState scroll,
        KumikoStyle style,
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
        List<(KumikoColumn Column, float X, int OriginalIndex)> visibleCols,
        ScrollState scroll,
        KumikoStyle style,
        ColumnFreezeMode freezeMode,
        float frozenWidth,
        bool isMultiSort = false)
    {
        foreach (var (col, colX, _) in visibleCols)
        {
            if (col.SortDirection == KumikoSortDirection.None) continue;

            float screenX = ColumnScreenX(colX, scroll, col.FreezeMode);
            float arrowX = screenX + col.Width - style.CellPadding - 8;
            float arrowCenterY = style.HeaderHeight / 2;

            var paint = _paintCache.SortIndicator;

            if (col.SortDirection == KumikoSortDirection.Ascending)
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
                float numberX = arrowX - 2;
                float numberY = arrowCenterY + (style.HeaderFont.Size * 0.65f) / 2 - 1;
                ctx.DrawText(SortOrderLabel(col.SortOrder), numberX, numberY, _paintCache.SortOrderBadge);
            }
        }
    }

    private void DrawFilterIcons(
        IDrawingContext ctx,
        List<(KumikoColumn Column, float X, int OriginalIndex)> visibleCols,
        ScrollState scroll,
        KumikoStyle style,
        bool isFrozen,
        float frozenWidth)
    {
        DrawFilterIcons(ctx, visibleCols, scroll, style,
            isFrozen ? ColumnFreezeMode.Left : ColumnFreezeMode.None, frozenWidth);
    }

    private void DrawFilterIcons(
        IDrawingContext ctx,
        List<(KumikoColumn Column, float X, int OriginalIndex)> visibleCols,
        ScrollState scroll,
        KumikoStyle style,
        ColumnFreezeMode freezeMode,
        float frozenWidth)
    {
        foreach (var (col, colX, _) in visibleCols)
        {
            if (!col.AllowFiltering) continue;

            float screenX = ColumnScreenX(colX, scroll, col.FreezeMode);
            bool hasActiveFilter = col.ActiveFilter != null && col.ActiveFilter.IsActive;

            // Position filter icon: right of header text, left of the sort arrow when one is drawn
            float iconSize = 10f;
            float sortSpace = SortGlyphSpace(col);
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
                var fillPaint = _paintCache.FilterActiveIcon;
                // Simple fill indication: draw a filled circle dot
                float dotR = 2.5f;
                float dotX = iconX + iconSize / 2;
                float dotY = mid + (bottom - mid) / 2;
                ctx.FillRect(new KumikoRect(dotX - dotR, dotY - dotR, dotR * 2, dotR * 2), fillPaint);
            }
        }
    }

    /// <summary>
    /// Draw the grouping panel above the header, showing active group chips with remove buttons.
    /// </summary>
    private void DrawGroupingPanel(
        IDrawingContext ctx,
        KumikoSource dataSource,
        ScrollState scroll,
        KumikoStyle style,
        float panelHeight)
    {
        // Background
        ctx.FillRect(new KumikoRect(0, 0, scroll.ViewportWidth, panelHeight), _paintCache.GroupPanelBackground);

        // Bottom border
        ctx.DrawLine(0, panelHeight, scroll.ViewportWidth, panelHeight, _paintCache.GridLine);

        var groups = dataSource.GroupDescriptions;
        if (groups.Count == 0) return;

        // Draw "Grouped by:" label
        float labelX = style.CellPadding + 2;
        var labelPaint = _paintCache.GroupPanelLabel;
        var labelSize = ctx.MeasureText("Grouped by:", labelPaint);
        var labelRect = new KumikoRect(labelX, 0, labelSize.Width, panelHeight);
        ctx.DrawTextInRect("Grouped by:", labelRect, labelPaint,
            KumikoTextAlignment.Left, KumikoVerticalAlignment.Center);

        float chipX = labelX + labelSize.Width + 10;
        float chipHeight = 26;
        float chipY = (panelHeight - chipHeight) / 2;
        float chipPadding = 10;
        float chipGap = 4;
        float accentWidth = 4f;

        var chipTextPaint = _paintCache.GroupChipText;
        var columns = dataSource.Columns;

        for (int g = 0; g < groups.Count; g++)
        {
            var group = groups[g];

            // The chip shows the group's header, else the header of the column it groups by, else the property
            string chipText = group.Header ?? group.PropertyName;
            if (group.Header == null)
            {
                for (int c = 0; c < columns.Count; c++)
                {
                    if (string.Equals(columns[c].PropertyName, group.PropertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        chipText = columns[c].Header;
                        break;
                    }
                }
            }

            // Measure chip text
            var textSize = ctx.MeasureText(chipText, chipTextPaint);
            float sortArrowWidth = 16;  // space for sort direction arrow
            float removeButtonSize = 16;
            float chipWidth = accentWidth + chipPadding + textSize.Width + 4 + sortArrowWidth + chipPadding + removeButtonSize + chipPadding / 2;

            // Draw chip background with rounded corners
            var chipRect = new KumikoRect(chipX, chipY, chipWidth, chipHeight);
            ctx.FillRoundRect(chipRect, 6, _paintCache.GroupChipBackground);

            // Draw chip border
            ctx.DrawRoundRect(chipRect, 6, _paintCache.GroupChipBorder);

            // Draw left accent bar (rounded)
            var accentRect = new KumikoRect(chipX, chipY, accentWidth + 6, chipHeight);
            ctx.Save();
            ctx.ClipRect(new KumikoRect(chipX, chipY, accentWidth + 3, chipHeight));
            ctx.FillRoundRect(accentRect, 6, _paintCache.GroupChipAccent);
            ctx.Restore();

            // Draw chip text
            float textStartX = chipX + accentWidth + chipPadding;
            var textRect = new KumikoRect(textStartX, chipY, textSize.Width, chipHeight);
            ctx.DrawTextInRect(chipText, textRect, chipTextPaint,
                KumikoTextAlignment.Left, KumikoVerticalAlignment.Center);

            // Draw sort direction arrow
            float arrowX = textStartX + textSize.Width + 4;
            float arrowCY = chipY + chipHeight / 2;
            var arrowPaint = _paintCache.GroupChipArrow;
            if (group.GroupSortDirection == KumikoSortDirection.Ascending)
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
                new KumikoRect(removeX - 7, removeCenterY - 7, 14, 14), 7,
                _paintCache.GroupChipRemoveBackground);

            var removePaint = _paintCache.GroupChipRemove;
            ctx.DrawLine(removeX - removeHalf, removeCenterY - removeHalf,
                         removeX + removeHalf, removeCenterY + removeHalf, removePaint);
            ctx.DrawLine(removeX - removeHalf, removeCenterY + removeHalf,
                         removeX + removeHalf, removeCenterY - removeHalf, removePaint);

            // Advance to next chip
            chipX += chipWidth + chipGap;

            // Draw hierarchy arrow between group chips (except after last)
            if (g < groups.Count - 1)
            {
                float sepX = chipX + 2;
                float sepCY = chipY + chipHeight / 2;
                var sepPaint = _paintCache.GroupChipSeparator;
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
    private void DrawTableSummaryRows(
        IDrawingContext ctx,
        KumikoSource dataSource,
        List<(KumikoColumn Column, float X, int OriginalIndex)> allCols,
        ScrollState scroll,
        KumikoStyle style,
        float frozenWidth,
        IReadOnlyList<ComputedSummaryRow> summaryRows,
        float startY,
        float handleOffset = 0)
    {
        var borderPaint = _paintCache.SummaryBorder;
        var titlePaint = _paintCache.SummaryLabel;
        var valuePaint = _paintCache.SummaryText;

        for (int i = 0; i < summaryRows.Count; i++)
        {
            var summary = summaryRows[i];
            float rowY = startY + i * style.SummaryRowHeight;

            // Background spans full viewport
            ctx.FillRect(new KumikoRect(0, rowY, scroll.ViewportWidth, style.SummaryRowHeight),
                _paintCache.SummaryBackground);

            // Top border line
            ctx.DrawLine(0, rowY, scroll.ViewportWidth, rowY, borderPaint);

            // Draw title at a fixed left position (always visible, not clipped)
            if (summary.Title != null)
            {
                var titleRect = new KumikoRect(
                    handleOffset + style.CellPadding * 2,
                    rowY,
                    frozenWidth > 0 ? frozenWidth - style.CellPadding * 3 : 200f,
                    style.SummaryRowHeight);
                ctx.DrawTextInRect(summary.Title, titleRect, titlePaint,
                    KumikoTextAlignment.Left, KumikoVerticalAlignment.Center);
            }

            // Draw summary values in their respective columns; values that don't fit narrow columns are drawn
            // after the title
            DrawSummaryValues(ctx, allCols, scroll, style, summary, rowY, style.SummaryRowHeight, valuePaint);
            DrawSummaryOverflow(ctx, scroll, style, frozenWidth, summary, rowY, style.SummaryRowHeight,
                handleOffset + style.CellPadding * 2, titlePaint, valuePaint);

            // Bottom border
            ctx.DrawLine(0, rowY + style.SummaryRowHeight,
                scroll.ViewportWidth, rowY + style.SummaryRowHeight, borderPaint);
        }
    }

    /// <summary>
    /// Draw a summary row's values in their columns; values whose column is too narrow (under 70 px of text
    /// width) are collected in <see cref="_overflowValues"/> instead.
    /// </summary>
    private void DrawSummaryValues(
        IDrawingContext ctx,
        List<(KumikoColumn Column, float X, int OriginalIndex)> allCols,
        ScrollState scroll,
        KumikoStyle style,
        ComputedSummaryRow summary,
        float rowY,
        float rowHeight,
        KumikoPaint valuePaint)
    {
        _overflowValues.Clear();
        foreach (var (col, colX, _) in allCols)
        {
            if (!summary.Values.TryGetValue(col.PropertyName, out var displayText)) continue;

            // Use correct screen X depending on whether column is frozen
            float screenX = ColumnScreenX(colX, scroll, col.FreezeMode);
            float usableWidth = col.Width - style.CellPadding * 2;

            // If the column is too narrow for the label+value, overflow it
            if (usableWidth < 70)
            {
                _overflowValues.Add(displayText);
                continue;
            }

            var cellRect = new KumikoRect(screenX + style.CellPadding, rowY, usableWidth, rowHeight);
            ctx.DrawTextInRect(displayText, cellRect, valuePaint,
                col.TextAlignment, KumikoVerticalAlignment.Center);
        }
    }

    /// <summary>
    /// Draw the values <see cref="DrawSummaryValues"/> could not fit, joined with " | ", after the row's title
    /// (only when the row has a title and the space left is wider than 50 px).
    /// </summary>
    private void DrawSummaryOverflow(
        IDrawingContext ctx,
        ScrollState scroll,
        KumikoStyle style,
        float frozenWidth,
        ComputedSummaryRow summary,
        float rowY,
        float rowHeight,
        float titleStartX,
        KumikoPaint titlePaint,
        KumikoPaint valuePaint)
    {
        if (_overflowValues.Count == 0 || summary.Title == null) return;

        var titleTextSize = ctx.MeasureText(summary.Title, titlePaint);
        float overflowX = titleStartX + titleTextSize.Width;
        float overflowWidth = (frozenWidth > 0 ? frozenWidth : scroll.ViewportWidth / 3) - overflowX - style.CellPadding;
        if (overflowWidth > 50)
        {
            var overflowRect = new KumikoRect(overflowX, rowY, overflowWidth, rowHeight);
            ctx.DrawTextInRect(summary.GetOverflowText(_overflowValues), overflowRect, valuePaint,
                KumikoTextAlignment.Left, KumikoVerticalAlignment.Center);
        }
    }

    /// <summary>
    /// Draw a group summary row (displayed after each group's data rows).
    /// </summary>
    private void DrawGroupSummaryRow(
        IDrawingContext ctx,
        KumikoSource dataSource,
        int row,
        float rowY,
        List<(KumikoColumn Column, float X, int OriginalIndex)> allCols,
        ScrollState scroll,
        KumikoStyle style,
        float frozenWidth,
        float handleOffset = 0)
    {
        var summaryInfo = dataSource.GetGroupSummaryRowInfo(row);
        if (summaryInfo == null) return;

        var (groupPath, summaryIndex) = summaryInfo.Value;
        var groupSummaries = dataSource.GetGroupSummaries(groupPath);
        if (groupSummaries == null || summaryIndex >= groupSummaries.Count) return;

        var summary = groupSummaries[summaryIndex];
        var textPaint = _paintCache.GroupSummaryText;

        // Background spans full viewport
        ctx.FillRect(new KumikoRect(0, rowY, scroll.ViewportWidth, style.RowHeight),
            _paintCache.GroupSummaryBackground);

        // Bottom border
        ctx.DrawLine(0, rowY + style.RowHeight, scroll.ViewportWidth, rowY + style.RowHeight,
            _paintCache.SummaryBorder);

        // Draw title at a fixed left position (always visible at left edge)
        float titleStartX = handleOffset + style.CellPadding * 2;
        if (summary.Title != null)
        {
            // Title goes in the frozen area, or a fixed 200px width if no frozen cols
            float titleWidth = frozenWidth > 0 ? frozenWidth - style.CellPadding * 3 : 200f;
            var titleRect = new KumikoRect(titleStartX, rowY, titleWidth, style.RowHeight);
            ctx.DrawTextInRect(summary.Title, titleRect, textPaint,
                KumikoTextAlignment.Left, KumikoVerticalAlignment.Center);
        }

        // Draw summary values in their respective columns; values that don't fit narrow columns are drawn
        // after the title
        DrawSummaryValues(ctx, allCols, scroll, style, summary, rowY, style.RowHeight, textPaint);
        DrawSummaryOverflow(ctx, scroll, style, frozenWidth, summary, rowY, style.RowHeight,
            titleStartX, textPaint, textPaint);
    }

    private void DrawColumnDragOverlay(
        IDrawingContext ctx,
        IReadOnlyList<KumikoColumn> columns,
        int dragColumnIndex,
        float dragScreenX,
        ScrollState scroll,
        KumikoStyle style)
    {
        if (dragColumnIndex < 0 || dragColumnIndex >= columns.Count) return;
        var col = columns[dragColumnIndex];

        var ghostRect = new KumikoRect(dragScreenX, 0, col.Width, style.HeaderHeight);
        ctx.FillRect(ghostRect, _paintCache.ColumnDragGhost);
        ctx.DrawRect(ghostRect, _paintCache.ColumnDragBorder);

        var textRect = new KumikoRect(
            dragScreenX + style.CellPadding, 0,
            col.Width - style.CellPadding * 2, style.HeaderHeight);
        ctx.DrawTextInRect(col.Header, textRect, _paintCache.HeaderText,
            KumikoTextAlignment.Left, KumikoVerticalAlignment.Center);
    }

    /// <summary>
    /// Draws the drag handle column — background strip, header, and grip icons for each visible row.
    /// </summary>
    private void DrawDragHandleColumn(
        IDrawingContext ctx,
        KumikoSource dataSource,
        int firstRow, int lastRow,
        ScrollState scroll,
        KumikoStyle style,
        float handleOffset,
        float rightHandleWidth,
        float topSummaryHeight,
        int frozenRowCount,
        float frozenRowHeight,
        float adjustedViewportHeight,
        float pinnedBandBottom)
    {
        // pinnedBandBottom: bottom of the sticky group headers, or negative infinity when none is pinned
        float handleWidth = style.RowDragHandleWidth;
        bool isLeft = style.RowDragHandlePosition == DragHandlePosition.Left;
        float handleX = isLeft ? 0 : scroll.ViewportWidth - rightHandleWidth;

        var handleBgPaint = _paintCache.DragHandleBackground;
        var handleHeaderBgPaint = _paintCache.DragHandleHeaderBackground;
        var gripPaint = _paintCache.DragHandleIcon;

        // Draw handle header background
        ctx.FillRect(new KumikoRect(handleX, 0, handleWidth, style.HeaderHeight), handleHeaderBgPaint);

        // Draw handle background for the full data area
        float dataAreaTop = style.HeaderHeight + topSummaryHeight;
        ctx.FillRect(new KumikoRect(handleX, dataAreaTop, handleWidth, adjustedViewportHeight - dataAreaTop), handleBgPaint);

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
            // No grip for a row whose middle is under the sticky group headers (#174)
            if (rowY + style.RowHeight / 2 < pinnedBandBottom) continue;
            DrawGripIcon(ctx, handleX, rowY, handleWidth, style.RowHeight, gripPaint);
        }

        // Draw a subtle vertical divider between handle and columns
        float dividerX = isLeft ? handleWidth : handleX;
        ctx.DrawLine(dividerX, 0, dividerX, adjustedViewportHeight, _paintCache.DragHandleDivider);
    }

    /// <summary>
    /// Draws a grip/drag icon (2-column × 3-row dot pattern) centered in the given cell area.
    /// </summary>
    private static void DrawGripIcon(
        IDrawingContext ctx, float cellX, float cellY, float cellWidth, float cellHeight, KumikoPaint paint)
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
                    new KumikoRect(dx - dotRadius, dy - dotRadius, dotRadius * 2, dotRadius * 2),
                    dotRadius, paint);
            }
        }
    }

    /// <summary>
    /// Draws a ghost row at the drag position and an insertion indicator line at the drop target.
    /// </summary>
    private void DrawRowDragOverlay(
        IDrawingContext ctx,
        KumikoSource dataSource,
        IReadOnlyList<KumikoColumn> columns,
        int dragRowIndex,
        float dragScreenY,
        ScrollState scroll,
        KumikoStyle style,
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
        var ghostRect = new KumikoRect(handleOffset, dragScreenY, rowWidth - handleOffset - rightHandleWidth, rowHeight);
        ctx.FillRect(ghostRect, _paintCache.RowDragGhost);
        ctx.DrawRect(ghostRect, _paintCache.RowDragBorder);

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
                    var textRect = new KumikoRect(
                        cellX + style.CellPadding, dragScreenY,
                        colWidth - style.CellPadding * 2, rowHeight);
                    ctx.DrawTextInRect(cellText, textRect, _paintCache.RowDragText,
                        KumikoTextAlignment.Left, KumikoVerticalAlignment.Center);
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
        ctx.DrawLine(indicatorLeft, indicatorY, indicatorRight, indicatorY, _paintCache.RowDropIndicator);

        // Draw small triangles at the ends of the indicator line for visibility
        float triSize = 6f;
        // Left triangle
        ctx.DrawLine(indicatorLeft, indicatorY - triSize, indicatorLeft, indicatorY + triSize, _paintCache.RowDropIndicator);
        // Right triangle
        ctx.DrawLine(indicatorRight, indicatorY - triSize, indicatorRight, indicatorY + triSize, _paintCache.RowDropIndicator);
    }
}

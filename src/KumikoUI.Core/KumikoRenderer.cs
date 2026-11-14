using KumikoUI.Core.Components;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Layout;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core;

/// <summary>
/// Renders the entire grid (header, rows, grid lines, selection, frozen columns)
/// using the platform-independent IDrawingContext.
/// </summary>
public sealed class KumikoRenderer : IDisposable
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

    private readonly HashSet<IInteractiveCellRenderer> _interactiveRenderers = new();

    /// <summary>
    /// Raised when an interactive cell renderer (<see cref="IInteractiveCellRenderer"/>) changed its look
    /// outside a draw, for example an action button's command raised <c>CanExecuteChanged</c>. The host
    /// redraws. It can be raised from any thread.
    /// </summary>
    public event EventHandler? RedrawRequested;

    // Paints whose color varies per row, cell or column (resolver and per-column style overrides); set just
    // before each draw, so overrides allocate nothing. Drawing contexts read a paint during the call only.
    private readonly KumikoPaint _rowBackground = new();
    private readonly KumikoPaint _cellBackground = new();
    private readonly KumikoPaint _headerBackground = new();
    private readonly KumikoPaint _headerText = new() { IsAntiAlias = true };

    // Summary values that do not fit their column, reused for every summary row
    private readonly List<string> _overflowValues = new();

    // Horizontal extents (left, width) of the scrollable, left-frozen and right-frozen panes this frame; summary
    // values are clipped to their pane so a scrolled column's total never slides over a frozen one
    private (float Left, float Width) _scrollPane, _leftPane, _rightPane;

    // Group headers pinned at the top of the data area this frame (#174), outermost first; reused every frame
    private readonly List<PinnedGroupHeader> _pinnedGroupHeaders = new();

    // The dragged row's cell texts (by column index), read once per drag so the ghost allocates nothing per frame
    private string?[] _ghostTexts = Array.Empty<string?>();
    private object? _ghostItem;
    private int _ghostColumnCount;

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

    // Hover and pressed feedback this frame, from the connected controller (#101): the hovered target, the pressed
    // target while the pointer is still on it, and the cell whose control the pointer is over (row, column index)
    private FeedbackTarget _hoverTarget = FeedbackTarget.None;
    private FeedbackTarget _pressedTarget = FeedbackTarget.None;
    private int _pointerCellRow = -1, _pointerCellColumn = -1;
    private bool _pointerCellPressed;
    private KumikoPoint _pointerInRows;

    // Transitions this frame, from the connected controller (#101). A group sliding open or shut: the rows after
    // _slideAnchor are drawn _slideShift lower (negative: higher), clipped below _slideTop (the header's bottom).
    private int _slideAnchor = -1;
    private float _slideShift, _slideTop;
    // The current row whose highlight fades in, and how far (0..1); the item it showed last frame
    private int _fadeRow = -1;
    private float _fadeProgress = 1f;
    private object? _lastCurrentItem;
    private bool _sawFirstFrame;
    private readonly KumikoPaint _fadePaint = new();
    private readonly KumikoPaint _ghostShadow = new() { IsAntiAlias = true };

    // Hit regions measured while drawing (grouping panel chips), read by the connected controller's hit tester
    private readonly RenderedHitRegions _hitRegions = new();
    private KumikoInputController? _inputController;

    /// <summary>
    /// The input controller this renderer draws for. Connecting it gives the controller's hit tester the regions this
    /// renderer measures while drawing (the grouping panel's chips, whose width depends on their text), so a press
    /// lands exactly on what was drawn. Both hosts connect their grid's controller; a renderer that is not connected
    /// draws the same frame and the controller estimates those regions. One renderer serves one controller.
    /// </summary>
    public KumikoInputController? InputController
    {
        get => _inputController;
        set
        {
            if (ReferenceEquals(_inputController, value)) return;
            if (_inputController != null) _inputController.HitTester.RenderedRegions = null;
            _inputController = value;
            if (value != null) value.HitTester.RenderedRegions = _hitRegions;
        }
    }

    /// <summary>
    /// Draws a frame-time readout (render time, frames per second, bytes allocated per frame) in the top-right
    /// corner of the grid (#70). Off by default, and then costs one boolean check per frame. The readout is
    /// averaged over short windows, so it adds no per-frame allocation.
    /// </summary>
    public bool ShowFrameStats { get; set; }

    /// <summary>
    /// Grid-level switch for the header filter icons: when false none is drawn and no header width is reserved
    /// for it, whatever a column's own <see cref="KumikoColumn.AllowFiltering"/>. Default true.
    /// </summary>
    public bool AllowFiltering { get; set; } = true;

    /// <summary>
    /// Turns cell values into drawable images for <see cref="ImageCellRenderer"/> and other renderers that draw
    /// images (passed to them as <see cref="CellRenderContext.ImageResolver"/>). Both hosts set it to their grid's
    /// image cache; null (the default for a renderer you create) passes cell values to
    /// <see cref="IDrawingContext.DrawImage"/> unchanged.
    /// </summary>
    public IKumikoImageResolver? ImageResolver { get; set; }

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
        _cellRenderers[KumikoColumnType.ProgressBar] = new ProgressBarCellRenderer { UsesColumnOptions = true };
    }

    /// <summary>Stops listening to the interactive cell renderers it has drawn.</summary>
    public void Dispose()
    {
        foreach (var renderer in _interactiveRenderers) renderer.RedrawRequested -= OnInteractiveRedrawRequested;
        _interactiveRenderers.Clear();
        GC.SuppressFinalize(this);
    }

    private void OnInteractiveRedrawRequested(object? sender, EventArgs e) => RedrawRequested?.Invoke(this, EventArgs.Empty);

    private void TrackInteractive(ICellRenderer renderer)
    {
        if (renderer is IInteractiveCellRenderer interactive && _interactiveRenderers.Add(interactive))
            interactive.RedrawRequested += OnInteractiveRedrawRequested;
    }

    /// <summary>
    /// Register a custom cell renderer for every column of a type that has no
    /// <see cref="KumikoColumn.CustomCellRenderer"/> of its own. Replaces the built-in renderer for that type.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="renderer"/> is null.</exception>
    public void SetCellRenderer(KumikoColumnType columnType, ICellRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        _cellRenderers[columnType] = renderer;
    }

    /// <summary>Get the cell renderer for a column type.</summary>
    public ICellRenderer GetCellRenderer(KumikoColumnType columnType)
    {
        return _cellRenderers.TryGetValue(columnType, out var renderer)
            ? renderer
            : _cellRenderers[KumikoColumnType.Text];
    }

    // The scrolling data rows on screen in the last frame, sampled by AutoFitColumn (#24); none before the first.
    private int _lastFirstRow;
    private int _lastLastRow = -1;

    /// <summary>
    /// Fits <paramref name="column"/> to its header and its content, as a double-click on its resize grip asks
    /// (<see cref="Input.KumikoInputController.AutoFitColumnRequested"/>), and returns the new width. The cells
    /// measured are the rows on screen in the last frame plus the first and last
    /// <see cref="KumikoLayoutEngine.AutoFitSampleRows"/> rows of the view, so it costs the same on any number of
    /// rows (see <see cref="KumikoLayoutEngine.CalculateAutoFitWidth(KumikoColumn, KumikoSource, IDrawingContext, KumikoStyle, int, int)"/>).
    /// The width becomes explicit, and a <see cref="ColumnSizeMode.Star"/> or <see cref="ColumnSizeMode.Auto"/>
    /// column switches to <see cref="ColumnSizeMode.Fixed"/> so it keeps the width (see
    /// <see cref="KumikoColumn.SizeMode"/>). The host redraws afterwards.
    /// </summary>
    /// <param name="column">The column to fit.</param>
    /// <param name="dataSource">The rows to sample.</param>
    /// <param name="measuringContext">Measures header and cell text; the caller owns and disposes it.</param>
    /// <param name="style">The grid style (fonts and padding).</param>
    /// <returns>The column's new width, in pixels.</returns>
    public float AutoFitColumn(KumikoColumn column, KumikoSource dataSource, IDrawingContext measuringContext,
        KumikoStyle style)
    {
        float width = _layout.CalculateAutoFitWidth(column, dataSource, measuringContext, style,
            _lastFirstRow, _lastLastRow);
        column.ApplyUserWidth(width);
        return width;
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
        EditSession? editSession = null)
        => RenderFrame(ctx, dataSource, scroll, selection, style, dragColumnIndex, dragColumnScreenX, dragRowIndex,
            dragRowScreenY, editSession, popupManager: null);

    /// <summary>Render the full grid, then the popups the grid's input controller has open.</summary>
    internal void RenderFrame(
        IDrawingContext ctx,
        KumikoSource dataSource,
        ScrollState scroll,
        KumikoSelectionModel selection,
        KumikoStyle style,
        int dragColumnIndex,
        float dragColumnScreenX,
        int dragRowIndex,
        float dragRowScreenY,
        EditSession? editSession,
        PopupManager? popupManager)
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

        // Auto columns sample the rows on screen (#24); the row range does not depend on column widths. A culture
        // change is applied first, so they measure text formatted as this frame draws it.
        dataSource.SyncCellTextCulture();
        int frozenRowCount = dataSource.EffectiveFrozenRowCount;
        var (firstRow, lastRow) = _layout.GetVisibleRowRangeWithOffset(
            scroll, style, dataSource.RowCount, topSummaryHeight, frozenRowCount);

        _layout.ComputeColumnWidths(columns, effectiveViewportWidth, ctx, style, dataSource, firstRow, lastRow);
        // Frozen panes that do not fit the viewport give columns back to the scrolling pane (#268).
        FrozenPanes.Resolve(columns, style, scroll.ViewportWidth);
        scroll.ContentWidth = _layout.GetContentWidth(columns) + handleOffset + rightHandleWidth;
        scroll.ContentHeight = _layout.GetContentHeight(dataSource.RowCount, style)
            + groupPanelOffset + topSummaryHeight + bottomSummaryHeight;
        scroll.ClampOffset();

        _paintCache.Update(style);
        CaptureFeedback(scroll, style, groupPanelOffset);

        ctx.FillRect(new KumikoRect(0, 0, scroll.ViewportWidth, scroll.ViewportHeight),
            _paintCache.Background);

        // Draw grouping panel above the header
        if (dataSource.IsGroupingActive)
        {
            DrawGroupingPanel(ctx, dataSource, scroll, style, groupPanelOffset);
        }
        else
        {
            _hitRegions.BeginGroupPanel(0);
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
        float frozenRowHeight = _layout.GetFrozenRowHeight(frozenRowCount, style);

        // Data rows start after header + top summaries + frozen rows. The vertical offset may have been clamped
        // since the range was first computed (the row count or viewport changed), so compute it again.
        float dataAreaTop = style.HeaderHeight + topSummaryHeight;
        (firstRow, lastRow) = _layout.GetVisibleRowRangeWithOffset(
            scroll, style, dataSource.RowCount, topSummaryHeight, frozenRowCount);
        _lastFirstRow = firstRow;
        _lastLastRow = lastRow;
        bool isMultiSort = dataSource.IsMultiSortActive;

        // Vertical extent rows may paint in: below header + top summaries, above a pinned footer.
        float dataClipBottom = adjustedViewportHeight - (style.PinBottomSummaryRows ? bottomSummaryHeight : 0);
        float dataClipHeight = Math.Max(0, dataClipBottom - dataAreaTop);
        var dataClip = new KumikoRect(0, dataAreaTop, scroll.ViewportWidth, dataClipHeight);
        float scrollingRowsTop = dataAreaTop + frozenRowHeight;

        // Transitions (#101): a group sliding open draws rows from below the viewport too, while they slide up into it
        CaptureTransitions(dataSource, selection, scroll, style, frozenRowCount, scrollingRowsTop, dataClipBottom);
        if (_slideShift < 0)
            lastRow = Math.Min(dataSource.RowCount - 1, lastRow + (int)MathF.Ceiling(-_slideShift / style.RowHeight) + 1);
        dataSource.PrepareCellTextCache(frozenRowCount, Math.Max(0, lastRow - firstRow + 1));

        // Sticky group headers (#174): pinned below the frozen rows, over the scrolling rows.
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
        DrawPaneRows(ctx, dataSource, scrollableCols, firstRow, lastRow, scroll, selection, style, ColumnFreezeMode.None,
            frozenWidth, editSession, topSummaryHeight, frozenRowCount, dataClip);
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
            DrawPaneRows(ctx, dataSource, frozenCols, firstRow, lastRow, scroll, selection, style, ColumnFreezeMode.Left,
                frozenWidth, editSession, topSummaryHeight, frozenRowCount, dataClip);
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
            DrawPaneRows(ctx, dataSource, rightFrozenCols, firstRow, lastRow, scroll, selection, style, ColumnFreezeMode.Right,
                frozenWidth, editSession, topSummaryHeight, frozenRowCount, dataClip, rightFrozenLeft, rightFrozenWidth);
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

        _scrollPane = (scrollClipLeft, scrollClipWidth);
        _leftPane = (handleOffset, frozenWidth);
        _rightPane = (scroll.ViewportWidth - rightHandleWidth - rightFrozenWidth, rightFrozenWidth);

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
            float bottomSummaryY = dataAreaTop + dataSource.RowCount * style.RowHeight - scroll.OffsetY
                + RowShift(dataSource.RowCount);
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
                // The rows after a sliding group's header are drawn below it only (#101)
                if (_slideAnchor >= 0 && row == _slideAnchor + 1)
                {
                    ctx.Save();
                    ctx.ClipRect(new KumikoRect(0, _slideTop, scroll.ViewportWidth, Math.Max(0, adjustedViewportHeight - _slideTop)));
                }

                float rowY = style.HeaderHeight + topSummaryHeight + frozenRowHeight + (row - frozenRowCount) * style.RowHeight - scroll.OffsetY
                    + RowShift(row);

                if (dataSource.IsGroupHeaderRow(row))
                {
                    DrawGroupHeaderRow(ctx, dataSource, row, rowY, allCols, scroll, style, frozenWidth, handleOffset);
                }
                else if (dataSource.IsGroupSummaryRow(row))
                {
                    DrawGroupSummaryRow(ctx, dataSource, row, rowY, allCols, scroll, style, frozenWidth, handleOffset);
                }
            }
            if (_slideAnchor >= 0 && _slideAnchor + 1 >= firstRow && _slideAnchor + 1 <= lastRow) ctx.Restore();

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

        var animations = _inputController?.Animations;
        if (dragColumnIndex >= 0)
        {
            DrawColumnDragOverlay(ctx, columns, dragColumnIndex, dragColumnScreenX, scroll, style,
                animations?.LiftProgress ?? 1f);
        }
        else if (animations is { IsDropping: true, DropColumn: { } dropped } && IndexOf(columns, dropped) is var droppedIndex
                 && droppedIndex >= 0)
        {
            // A dropped header settles into its slot while its shadow fades (#101)
            float p = animations.DropProgress;
            DrawColumnDragOverlay(ctx, columns, droppedIndex,
                animations.DropFromX + (animations.DropToX - animations.DropFromX) * p, scroll, style, 1f - p);
        }

        if (dragRowIndex >= 0)
            DrawRowDragOverlay(ctx, dataSource, columns, dragRowIndex, dragRowScreenY, scroll, style,
                groupPanelOffset, handleOffset, rightHandleWidth);
        else
            _ghostItem = null; // the next drag reads its row's texts afresh

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

        float x = ColumnScreenX(found.X, scroll, found.Column.Pane);
        float width = found.Column.Width;
        if (found.Column.Pane == ColumnFreezeMode.None && (x + width <= scrollPaneLeft || x >= scrollPaneRight))
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
        bool rowOverrides = rowStyle != null && (rowStyle.TextColor != null || rowStyle.Font.HasValue);
        if (cellStyle == null && !rowOverrides)
            return columnStyle;

        var effective = _effectiveCellStyle;
        effective.BackgroundColor = cellStyle?.BackgroundColor ?? columnStyle?.BackgroundColor;
        effective.TextColor = cellStyle?.TextColor ?? columnStyle?.TextColor ?? rowStyle?.TextColor;
        effective.Font = cellStyle?.Font ?? columnStyle?.Font ?? rowStyle?.Font;
        effective.Padding = cellStyle?.Padding ?? columnStyle?.Padding;
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

    /// <summary>
    /// Reads the connected controller's transitions for this frame (#101): the slide of the rows after a group header
    /// that was just toggled, and the fade-in of the current row's highlight, which starts here when the current row
    /// shows another item than last frame (a tap, a key, a binding). Allocation-free.
    /// </summary>
    private void CaptureTransitions(KumikoSource dataSource, KumikoSelectionModel selection, ScrollState scroll,
        KumikoStyle style, int frozenRowCount, float scrollingRowsTop, float dataClipBottom)
    {
        _slideAnchor = -1;
        _slideShift = 0;
        _fadeRow = -1;
        _fadeProgress = 1f;

        // The current row's item, to notice the current row moving to another item
        var current = selection.CurrentCell;
        object? currentItem = current.IsValid && current.Row < dataSource.RowCount && !dataSource.IsNonDataRow(current.Row)
            ? dataSource.GetItem(current.Row)
            : null;
        bool canAnimate = _inputController != null && _inputController.CanAnimate(style);
        if (!ReferenceEquals(currentItem, _lastCurrentItem))
        {
            if (_sawFirstFrame && currentItem != null && canAnimate) _inputController!.Animations.StartSelection(currentItem);
            _lastCurrentItem = currentItem;
        }
        _sawFirstFrame = true;

        if (!canAnimate || _inputController!.Animations is not { IsActive: true } animations) return;

        if (animations.IsSelectionFading && ReferenceEquals(animations.SelectionItem, currentItem))
        {
            _fadeRow = current.Row;
            _fadeProgress = animations.SelectionProgress;
        }

        int anchor = animations.GroupAnchorRow;
        if (animations.IsGroupSliding && anchor >= frozenRowCount && anchor < dataSource.RowCount)
        {
            float headerBottom = scrollingRowsTop + (anchor - frozenRowCount + 1) * style.RowHeight - scroll.OffsetY;
            float top = Math.Max(headerBottom, scrollingRowsTop);
            float room = dataClipBottom - top;
            if (room > 0)
            {
                // Expanding: the new rows come out from under the header, pushing the rest down. Collapsing: the rows
                // after the group close up under it. At most the height left below the header slides.
                float distance = Math.Min(Math.Abs(animations.GroupRowDelta) * style.RowHeight, room);
                float remaining = (1f - animations.GroupProgress) * distance;
                _slideAnchor = anchor;
                _slideTop = top;
                _slideShift = animations.GroupRowDelta > 0 ? -remaining : remaining;
            }
        }
    }

    /// <summary>How far a sliding group moves <paramref name="row"/> this frame (0 for every row up to its header).</summary>
    private float RowShift(int row) => _slideAnchor >= 0 && row > _slideAnchor ? _slideShift : 0f;

    /// <summary>
    /// Lays the row's unselected background over its highlight, fading out as the highlight fades in, so the row
    /// starts as it was and ends exactly as a selected row is drawn.
    /// </summary>
    private void FadeInHighlight(IDrawingContext ctx, KumikoRect rowRect, KumikoPaint unselectedPaint)
    {
        var color = unselectedPaint.Color;
        byte alpha = (byte)(color.A * (1f - _fadeProgress));
        if (alpha == 0) return;
        _fadePaint.Color = color.WithAlpha(alpha);
        ctx.FillRect(rowRect, _fadePaint);
    }

    /// <summary>
    /// The data rows of one pane: backgrounds and cells, the current cell's border and the grid lines, clipped to the
    /// data area. While a group slides open or shut (#101) they are drawn in two bands: the rows up to its header as
    /// usual, and the rows after it moved by the slide and clipped below the header.
    /// </summary>
    private void DrawPaneRows(
        IDrawingContext ctx, KumikoSource dataSource, List<(KumikoColumn Column, float X, int OriginalIndex)> cols,
        int firstRow, int lastRow, ScrollState scroll, KumikoSelectionModel selection, KumikoStyle style,
        ColumnFreezeMode mode, float frozenWidth, EditSession? editSession, float topSummaryHeight, int frozenRowCount,
        KumikoRect dataClip, float rightFrozenLeft = 0, float rightFrozenWidth = 0)
    {
        if (_slideAnchor < 0 || _slideAnchor < firstRow - 1 || _slideAnchor >= lastRow)
        {
            ctx.Save();
            ctx.ClipRect(dataClip);
            DrawRowBand(ctx, dataSource, cols, firstRow, lastRow, scroll, selection, style, mode, frozenWidth, editSession,
                topSummaryHeight, frozenRowCount, rightFrozenLeft, rightFrozenWidth);
            ctx.Restore();
            return;
        }

        // The rows up to the header end at its bottom edge (and its grid line), so their vertical grid lines stop there.
        // The rows after it start at their slid position: while a group closes, the gap between them is left empty.
        float bandTop = _slideTop + Math.Max(0f, _slideShift);
        ctx.Save();
        ctx.ClipRect(dataClip);
        ctx.ClipRect(new KumikoRect(0, dataClip.Y, scroll.ViewportWidth, Math.Max(0, _slideTop + 1 - dataClip.Y)));
        DrawRowBand(ctx, dataSource, cols, firstRow, _slideAnchor, scroll, selection, style, mode, frozenWidth, editSession,
            topSummaryHeight, frozenRowCount, rightFrozenLeft, rightFrozenWidth);
        ctx.Restore();
        ctx.Save();
        ctx.ClipRect(dataClip);
        ctx.ClipRect(new KumikoRect(0, bandTop, scroll.ViewportWidth, Math.Max(0, dataClip.Bottom - bandTop)));
        DrawRowBand(ctx, dataSource, cols, _slideAnchor + 1, lastRow, scroll, selection, style, mode, frozenWidth, editSession,
            topSummaryHeight, frozenRowCount, rightFrozenLeft, rightFrozenWidth);
        ctx.Restore();
    }

    /// <summary>Rows <paramref name="firstRow"/>..<paramref name="lastRow"/> of a pane, with the current cell's border when it is among them.</summary>
    private void DrawRowBand(
        IDrawingContext ctx, KumikoSource dataSource, List<(KumikoColumn Column, float X, int OriginalIndex)> cols,
        int firstRow, int lastRow, ScrollState scroll, KumikoSelectionModel selection, KumikoStyle style,
        ColumnFreezeMode mode, float frozenWidth, EditSession? editSession, float topSummaryHeight, int frozenRowCount,
        float rightFrozenLeft, float rightFrozenWidth)
    {
        DrawRows(ctx, dataSource, cols, firstRow, lastRow, scroll, selection, style, mode, frozenWidth, editSession,
            topSummaryHeight, frozenRowCount);
        int currentRow = selection.CurrentCell.Row;
        bool currentInBand = currentRow < frozenRowCount || (currentRow >= firstRow && currentRow <= lastRow) ||
                             (_slideAnchor < 0 && currentRow > lastRow) || (_slideAnchor < 0 && currentRow < firstRow);
        if (currentInBand)
            DrawCurrentCellBorder(ctx, cols, scroll, selection, style, mode, frozenWidth, dataSource, topSummaryHeight, frozenRowCount);
        DrawGridLines(ctx, cols, firstRow, lastRow, scroll, style, dataSource.RowCount, mode, frozenWidth, dataSource,
            topSummaryHeight, frozenRowCount, rightFrozenLeft, rightFrozenWidth);
    }

    /// <summary>
    /// Reads the connected controller's hover and pressed feedback for this frame. Targets on rows and columns are
    /// dropped once the grid has scrolled since they were set (they would light up whatever moved under a still
    /// pointer); a transparent overlay color turns its feedback off.
    /// </summary>
    private void CaptureFeedback(ScrollState scroll, KumikoStyle style, float groupPanelOffset)
    {
        _hoverTarget = _pressedTarget = FeedbackTarget.None;
        _pointerCellRow = _pointerCellColumn = -1;
        _pointerCellPressed = false;
        if (_inputController?.Feedback is not { } feedback) return;

        bool current = feedback.IsCurrent(scroll);
        if (style.HoverOverlayColor.A > 0) _hoverTarget = Usable(feedback.Hover, current);
        if (style.PressedOverlayColor.A > 0) _pressedTarget = Usable(feedback.ActivePressed, current);

        // A cell's control under the pointer: the cell renderer draws its feedback from the pointer position, in the
        // coordinates the rows are drawn in (below the grouping panel)
        var cellTarget = _pressedTarget.Region == HitRegion.Cell ? _pressedTarget : _hoverTarget;
        if (cellTarget.Region == HitRegion.Cell && float.IsFinite(feedback.PointerX) && float.IsFinite(feedback.PointerY))
        {
            _pointerCellRow = cellTarget.Row;
            _pointerCellColumn = cellTarget.Column;
            _pointerCellPressed = _pressedTarget.Region == HitRegion.Cell;
            _pointerInRows = new KumikoPoint(feedback.PointerX, feedback.PointerY - groupPanelOffset);
        }

        static FeedbackTarget Usable(FeedbackTarget target, bool current) =>
            current || target.Region is HitRegion.GroupPanelChip or HitRegion.GroupPanelChipRemove ? target : FeedbackTarget.None;
    }

    /// <summary>The overlay for the target at <paramref name="region"/> (row, column, group): pressed, hovered, or none.</summary>
    private KumikoPaint? OverlayFor(HitRegion region, int row, int column, int group)
    {
        if (_pressedTarget.Region == region && _pressedTarget.Row == row && _pressedTarget.Column == column &&
            _pressedTarget.Group == group)
            return _paintCache.PressedOverlay;
        if (_hoverTarget.Region == region && _hoverTarget.Row == row && _hoverTarget.Column == column &&
            _hoverTarget.Group == group)
            return _paintCache.HoverOverlay;
        return null;
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

        foreach (var (col, colX, columnIndex) in visibleCols)
        {
            float screenX = ColumnScreenX(colX, scroll, col.Pane);

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

            // Hover and pressed feedback on a sortable header
            if (OverlayFor(HitRegion.HeaderCell, -1, columnIndex, -1) is { } headerOverlay)
                ctx.FillRect(new KumikoRect(screenX, 0, col.Width, style.HeaderHeight), headerOverlay);

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
    private float FilterGlyphSpace(KumikoColumn col) => AllowFiltering && col.AllowFiltering ? KumikoLayoutEngine.FilterGlyphSpace : 0f;

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
                + (row - frozenRowCount) * style.RowHeight - scroll.OffsetY + RowShift(row);

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
            KumikoPaint unselectedPaint;
            if (dynamicRowStyle?.BackgroundColor != null)
            {
                unselectedPaint = _rowBackground;
                unselectedPaint.Color = dynamicRowStyle.BackgroundColor.Value;
            }
            else if (style.AlternateRowBackground && row % 2 == 1)
            {
                unselectedPaint = _paintCache.AlternateRowBackground;
            }
            else if (isFrozenLeft || freezeMode == ColumnFreezeMode.Right)
            {
                unselectedPaint = freezeMode == ColumnFreezeMode.Right
                    ? _paintCache.RightFrozenColumnBackground
                    : _paintCache.FrozenColumnBackground;
            }
            else
            {
                unselectedPaint = _paintCache.Background;
            }
            KumikoPaint rowBgPaint = isSelected ? _paintCache.SelectionBackground
                : isFocused ? _paintCache.FocusedRowBackground
                : unselectedPaint;

            // Row background fill area depends on freeze mode — but when inside a clip, fill entire clip
            var rowRect = new KumikoRect(0, rowY, scroll.ViewportWidth, style.RowHeight);
            ctx.FillRect(rowRect, rowBgPaint);
            if (row == _fadeRow && (isSelected || isFocused))
                FadeInHighlight(ctx, rowRect, unselectedPaint);

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
            float screenX = ColumnScreenX(colX, scroll, col.Pane);

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
            TrackInteractive(renderer);
            string displayText = dataSource.GetCachedCellText(row, dataItem, col, out object? value);

            var cell = new CellRenderContext(cellRect, row, value, displayText, col, style, colCellStyle, isSelected,
                _paintCache, _paintCache.CellTextPaint(colCellStyle, style, isSelected),
                colCellStyle?.TextAlignment ?? col.TextAlignment, ImageResolver);
            if (row == _pointerCellRow && origIdx == _pointerCellColumn)
                cell = cell with { PointerPosition = _pointerInRows, IsPointerPressed = _pointerCellPressed };
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
            KumikoPaint unselectedPaint;
            if (dynamicRowStyle?.BackgroundColor != null)
            {
                unselectedPaint = _rowBackground;
                unselectedPaint.Color = dynamicRowStyle.BackgroundColor.Value;
            }
            else if (style.AlternateRowBackground && row % 2 == 1)
            {
                unselectedPaint = _paintCache.AlternateRowBackground;
            }
            else
            {
                // Frozen rows use FrozenRowBackgroundColor by default
                unselectedPaint = _paintCache.FrozenRowBackground;
            }
            KumikoPaint rowBgPaint = isSelected ? _paintCache.SelectionBackground
                : isFocused ? _paintCache.FocusedRowBackground
                : unselectedPaint;

            var rowRect = new KumikoRect(0, rowY, scroll.ViewportWidth, style.RowHeight);
            ctx.FillRect(rowRect, rowBgPaint);
            if (row == _fadeRow && (isSelected || isFocused))
                FadeInHighlight(ctx, rowRect, unselectedPaint);

            DrawRowCells(ctx, dataSource, visibleCols, row, rowY, dataItem, dynamicRowStyle, scroll, style,
                isSelected, editSession);

            // The row fill covers the grid lines drawn before it, so a frozen row gets its own
            if (style.ShowHorizontalGridLines)
            {
                float lineY = rowY + style.RowHeight;
                ctx.DrawLine(0, lineY, scroll.ViewportWidth, lineY, _paintCache.GridLine);
            }
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
        float indent = InteractionGeometry.GroupChevronX(groupInfo.Level, style, handleOffset);

        // Draw expand/collapse indicator with background pill (the hit tester uses the same geometry)
        float chevronSize = style.GroupChevronSize;
        float chevronX = indent;
        float chevronCenterY = rowY + style.RowHeight / 2;

        var pillRect = InteractionGeometry.GroupChevronPill(rowY, groupInfo.Level, style, handleOffset);
        ctx.FillRoundRect(pillRect, 4f, _paintCache.GroupChevronBackground);
        if (OverlayFor(HitRegion.GroupHeaderRow, row, -1, -1) is { } chevronOverlay)
            ctx.FillRoundRect(pillRect, 4f, chevronOverlay);

        // The disclosure glyph, in the chevron's square: expanded or collapsed
        style.IconProvider.DrawIcon(ctx,
            groupInfo.IsExpanded ? KumikoIcon.GroupExpanded : KumikoIcon.GroupCollapsed,
            new KumikoRect(chevronX, chevronCenterY - chevronSize / 2, chevronSize, chevronSize),
            _paintCache.GroupChevron);

        // Draw group text in up to three runs: "Header: DisplayText", " (count)" and the caption summary, each
        // with its own paint (group header text, count, caption summary).
        float textX = indent + chevronSize + style.CellPadding;
        float textRight = areaWidth - style.CellPadding;

        string? captionLabel = groupInfo.GetCaptionLabel(KumikoSource.GetCaptionSummaryText(groupInfo));

        textX = DrawGroupHeaderRun(ctx, groupInfo.GetTitle(), _paintCache.GroupHeaderText, textX, textRight, rowY, style);
        textX = DrawGroupHeaderRun(ctx, groupInfo.GetCountText(), _paintCache.GroupHeaderCount, textX, textRight, rowY, style);
        if (captionLabel != null)
            DrawGroupHeaderRun(ctx, captionLabel, _paintCache.CaptionSummaryText, textX, textRight, rowY, style);
    }

    /// <summary>
    /// Draw one run of a group header's text starting at <paramref name="x"/> and return where the next run
    /// starts; returns <paramref name="right"/> when the run did not fit, so nothing is drawn after it.
    /// </summary>
    private static float DrawGroupHeaderRun(
        IDrawingContext ctx, string text, KumikoPaint paint, float x, float right, float rowY, KumikoStyle style)
    {
        if (x >= right) return right;

        float width = ctx.MeasureText(text, paint).Width;
        var rect = new KumikoRect(x, rowY, right - x, style.RowHeight);
        ctx.DrawTextInRect(text, rect, paint, KumikoTextAlignment.Left, KumikoVerticalAlignment.Center);
        return x + width;
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
                    + (row - frozenRowCount) * style.RowHeight - scroll.OffsetY + RowShift(row);
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
                float screenX = ColumnScreenX(colX, scroll, col.Pane) + col.Width;
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
                float screenX = ColumnScreenX(colX, scroll, col.Pane);
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
                        + (row - frozenRowCount) * style.RowHeight - scroll.OffsetY + RowShift(row);
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

            float screenX = ColumnScreenX(colX, scroll, col.Pane);
            float arrowX = screenX + col.Width - style.CellPadding - 8;
            float arrowCenterY = style.HeaderHeight / 2;

            // The arrow's 12 px square, centered on the 8 px wide arrow slot
            style.IconProvider.DrawIcon(ctx,
                col.SortDirection == KumikoSortDirection.Ascending ? KumikoIcon.SortAscending : KumikoIcon.SortDescending,
                new KumikoRect(arrowX - 2, arrowCenterY - 6, 12, 12),
                _paintCache.SortIndicator);

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
        foreach (var (col, colX, columnIndex) in visibleCols)
        {
            if (!AllowFiltering || !col.AllowFiltering) continue;

            float screenX = ColumnScreenX(colX, scroll, col.Pane);
            bool hasActiveFilter = col.ActiveFilter != null && col.ActiveFilter.IsActive;

            // Right of header text, left of the sort arrow when one is drawn; the hit tester uses the same geometry
            var iconRect = InteractionGeometry.FilterIconRect(screenX, col, style);
            if (OverlayFor(HitRegion.HeaderFilterIcon, -1, columnIndex, -1) is { } iconOverlay)
                ctx.FillRoundRect(iconRect.Inflate(4, 4), 4, iconOverlay);
            style.IconProvider.DrawIcon(ctx,
                hasActiveFilter ? KumikoIcon.FilterActive : KumikoIcon.Filter,
                iconRect,
                hasActiveFilter ? _paintCache.FilterActiveIcon : _paintCache.FilterIcon);
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
        _hitRegions.BeginGroupPanel(panelHeight);

        // Background
        ctx.FillRect(new KumikoRect(0, 0, scroll.ViewportWidth, panelHeight), _paintCache.GroupPanelBackground);

        // Bottom border
        ctx.DrawLine(0, panelHeight, scroll.ViewportWidth, panelHeight, _paintCache.GridLine);

        var groups = dataSource.GroupDescriptions;
        if (groups.Count == 0) return;

        // Draw the group panel label
        float labelX = style.CellPadding + 2;
        var labelPaint = _paintCache.GroupPanelLabel;
        var labelSize = ctx.MeasureText(dataSource.Strings.GroupPanelLabel, labelPaint);
        var labelRect = new KumikoRect(labelX, 0, labelSize.Width, panelHeight);
        ctx.DrawTextInRect(dataSource.Strings.GroupPanelLabel, labelRect, labelPaint,
            KumikoTextAlignment.Left, KumikoVerticalAlignment.Center);

        // The chip layout is shared with the hit tester's estimate (InteractionGeometry); the chips drawn are
        // published to it (_hitRegions), so a press hits exactly what is drawn.
        float chipX = InteractionGeometry.FirstChipX(labelSize.Width, style);
        float chipHeight = InteractionGeometry.ChipHeight;
        float chipY = InteractionGeometry.ChipY(panelHeight);
        float chipPadding = InteractionGeometry.ChipPadding;
        float accentWidth = InteractionGeometry.ChipAccentWidth;
        const float chipRadius = InteractionGeometry.ChipCornerRadius;

        var chipTextPaint = _paintCache.GroupChipText;
        var columns = dataSource.Columns;

        for (int g = 0; g < groups.Count; g++)
        {
            var group = groups[g];

            // The chip shows the group's header, else the header of the column it groups by, else the property
            string chipText = KumikoHitTester.ChipText(group, columns);

            // Measure chip text
            var textSize = ctx.MeasureText(chipText, chipTextPaint);
            float chipWidth = InteractionGeometry.ChipWidth(textSize.Width);

            // Draw chip background with rounded corners
            var chipRect = new KumikoRect(chipX, chipY, chipWidth, chipHeight);
            _hitRegions.AddChip(group, chipRect);
            ctx.FillRoundRect(chipRect, chipRadius, _paintCache.GroupChipBackground);

            // Draw chip border
            ctx.DrawRoundRect(chipRect, chipRadius, _paintCache.GroupChipBorder);

            // Draw left accent bar (rounded)
            var accentRect = new KumikoRect(chipX, chipY, accentWidth + 6, chipHeight);
            ctx.Save();
            ctx.ClipRect(new KumikoRect(chipX, chipY, accentWidth + 3, chipHeight));
            ctx.FillRoundRect(accentRect, chipRadius, _paintCache.GroupChipAccent);
            ctx.Restore();

            // Hover and pressed feedback on the chip (a tap reverses the group's sort)
            if (OverlayFor(HitRegion.GroupPanelChip, -1, -1, g) is { } chipOverlay)
                ctx.FillRoundRect(chipRect, chipRadius, chipOverlay);

            // Draw chip text
            float textStartX = chipX + accentWidth + chipPadding;
            var textRect = new KumikoRect(textStartX, chipY, textSize.Width, chipHeight);
            ctx.DrawTextInRect(chipText, textRect, chipTextPaint,
                KumikoTextAlignment.Left, KumikoVerticalAlignment.Center);

            // Draw sort direction arrow
            float arrowX = textStartX + textSize.Width + 4;
            float arrowCY = chipY + chipHeight / 2;
            float ax = arrowX + 6;
            style.IconProvider.DrawIcon(ctx,
                group.GroupSortDirection == KumikoSortDirection.Ascending ? KumikoIcon.SortAscending : KumikoIcon.SortDescending,
                new KumikoRect(ax - 6, arrowCY - 6, 12, 12),
                _paintCache.GroupChipArrow);

            // Remove button: a circle with the remove glyph
            var removeCenter = InteractionGeometry.ChipRemoveCenter(chipRect);
            float removeX = removeCenter.X;
            float removeCenterY = removeCenter.Y;

            // Small circle background for remove
            ctx.FillRoundRect(
                new KumikoRect(removeX - 7, removeCenterY - 7, 14, 14), 7,
                _paintCache.GroupChipRemoveBackground);
            if (OverlayFor(HitRegion.GroupPanelChipRemove, -1, -1, g) is { } removeOverlay)
                ctx.FillRoundRect(new KumikoRect(removeX - 9, removeCenterY - 9, 18, 18), 9, removeOverlay);

            style.IconProvider.DrawIcon(ctx, KumikoIcon.Remove,
                new KumikoRect(removeX - 5, removeCenterY - 5, 10, 10),
                _paintCache.GroupChipRemove);

            // Draw hierarchy arrow between group chips (except after last), then advance to the next chip
            if (g < groups.Count - 1)
            {
                float sepX = chipX + chipWidth + InteractionGeometry.ChipGap + 2;
                float sepCY = chipY + chipHeight / 2;
                style.IconProvider.DrawIcon(ctx, KumikoIcon.GroupSeparator,
                    new KumikoRect(sepX - 2.5f, sepCY - 5, 10, 10),
                    _paintCache.GroupChipSeparator);
            }
            chipX = InteractionGeometry.NextChipX(chipX, chipWidth);
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
            DrawSummaryValues(ctx, allCols, scroll, style, summary, rowY, style.SummaryRowHeight, valuePaint,
                SummaryTitleEnd(ctx, summary, titlePaint, handleOffset + style.CellPadding * 2, frozenWidth, style));
            DrawSummaryOverflow(ctx, scroll, style, frozenWidth, summary, rowY, style.SummaryRowHeight,
                handleOffset + style.CellPadding * 2, titlePaint, valuePaint);

            // Bottom border
            ctx.DrawLine(0, rowY + style.SummaryRowHeight,
                scroll.ViewportWidth, rowY + style.SummaryRowHeight, borderPaint);
        }
    }

    /// <summary>
    /// The X where a summary row's title stops (its left edge when it has none), so values start after it.
    /// </summary>
    private static float SummaryTitleEnd(
        IDrawingContext ctx, ComputedSummaryRow summary, KumikoPaint titlePaint,
        float titleStartX, float frozenWidth, KumikoStyle style)
    {
        if (summary.Title == null) return float.NegativeInfinity;
        float titleBoxWidth = frozenWidth > 0 ? frozenWidth - style.CellPadding * 3 : 200f;
        float textWidth = Math.Min(ctx.MeasureText(summary.Title, titlePaint).Width, Math.Max(0, titleBoxWidth));
        return titleStartX + textWidth + style.CellPadding;
    }

    /// <summary>
    /// Draw a summary row's values in their columns, each inside its own column and pane: the text is laid out
    /// (and ellipsised) in the part of the column that is visible in its pane and to the right of the row's
    /// title. Values whose column is too narrow (under 70 px of text width) are collected in
    /// <see cref="_overflowValues"/> instead.
    /// </summary>
    private void DrawSummaryValues(
        IDrawingContext ctx,
        List<(KumikoColumn Column, float X, int OriginalIndex)> allCols,
        ScrollState scroll,
        KumikoStyle style,
        ComputedSummaryRow summary,
        float rowY,
        float rowHeight,
        KumikoPaint valuePaint,
        float titleEnd)
    {
        _overflowValues.Clear();
        foreach (var (col, colX, _) in allCols)
        {
            if (!summary.Values.TryGetValue(col.PropertyName, out var displayText)) continue;

            // Use correct screen X depending on whether column is frozen
            float screenX = ColumnScreenX(colX, scroll, col.Pane);
            float usableWidth = col.Width - style.CellPadding * 2;

            // If the column is too narrow for the label+value, overflow it
            if (usableWidth < 70)
            {
                _overflowValues.Add(displayText);
                continue;
            }

            var (paneLeft, paneWidth) = col.FreezeMode switch
            {
                ColumnFreezeMode.Left => _leftPane,
                ColumnFreezeMode.Right => _rightPane,
                _ => _scrollPane,
            };

            float left = screenX + style.CellPadding;
            float right = left + usableWidth;
            // The title owns the space it occupies: a value starts after it
            left = Math.Max(left, titleEnd);
            left = Math.Max(left, paneLeft);
            right = Math.Min(right, paneLeft + paneWidth);
            if (right - left < MinSummaryValueWidth) continue;

            var cellRect = new KumikoRect(left, rowY, right - left, rowHeight);
            ctx.Save();
            ctx.ClipRect(new KumikoRect(paneLeft, rowY, paneWidth, rowHeight));
            ctx.DrawTextInRect(displayText, cellRect, valuePaint,
                col.TextAlignment, KumikoVerticalAlignment.Center);
            ctx.Restore();
        }
    }

    // Narrowest visible part of a column that still shows a summary value
    private const float MinSummaryValueWidth = 16f;

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
        // Read from the group directly, so drawing composes no group path (#254).
        var summary = dataSource.GetGroupSummaryRow(row);
        if (summary == null) return;
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
        DrawSummaryValues(ctx, allCols, scroll, style, summary, rowY, style.RowHeight, textPaint,
            SummaryTitleEnd(ctx, summary, textPaint, titleStartX, frozenWidth, style));
        DrawSummaryOverflow(ctx, scroll, style, frozenWidth, summary, rowY, style.RowHeight,
            titleStartX, textPaint, textPaint);
    }

    /// <summary>The index of <paramref name="column"/> in <paramref name="columns"/>, or -1.</summary>
    private static int IndexOf(IReadOnlyList<KumikoColumn> columns, KumikoColumn column)
    {
        for (int i = 0; i < columns.Count; i++)
        {
            if (ReferenceEquals(columns[i], column)) return i;
        }
        return -1;
    }

    /// <summary>
    /// Draws a dragged column header's ghost at <paramref name="dragScreenX"/>, lifted off the header row by
    /// <paramref name="lift"/> (0 flat, 1 fully lifted): a soft shadow below and to the right of it.
    /// </summary>
    private void DrawColumnDragOverlay(
        IDrawingContext ctx,
        IReadOnlyList<KumikoColumn> columns,
        int dragColumnIndex,
        float dragScreenX,
        ScrollState scroll,
        KumikoStyle style,
        float lift)
    {
        if (dragColumnIndex < 0 || dragColumnIndex >= columns.Count) return;
        var col = columns[dragColumnIndex];

        var ghostRect = new KumikoRect(dragScreenX, 0, col.Width, style.HeaderHeight);
        if (lift > 0.01f)
        {
            _ghostShadow.Color = new KumikoColor(0, 0, 0, (byte)(48 * Math.Min(1f, lift)));
            ctx.FillRoundRect(ghostRect.Offset(3 * lift, 3 * lift), 3, _ghostShadow);
        }
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
            DrawGripIcon(ctx, style.IconProvider, handleX, rowY, handleWidth, style.RowHeight, gripPaint);
        }

        // Draw grip icons for scrollable visible rows (clipped below frozen rows)
        float scrollableAreaTop = dataAreaTop + frozenRowHeight;
        for (int row = firstRow; row <= lastRow; row++)
        {
            if (dataSource.IsNonDataRow(row)) continue;
            float rowY = dataAreaTop + frozenRowHeight + (row - frozenRowCount) * style.RowHeight - scroll.OffsetY
                + RowShift(row);
            if (rowY + style.RowHeight <= scrollableAreaTop || rowY > adjustedViewportHeight) continue;
            // A row sliding out from under a group header shows its grip only below the header (#101)
            if (row > _slideAnchor && _slideAnchor >= 0 && rowY + style.RowHeight / 2 < _slideTop) continue;
            // No grip for a row whose middle is under the sticky group headers (#174)
            if (rowY + style.RowHeight / 2 < pinnedBandBottom) continue;
            DrawGripIcon(ctx, style.IconProvider, handleX, rowY, handleWidth, style.RowHeight, gripPaint);
        }

        // Draw a subtle vertical divider between handle and columns
        float dividerX = isLeft ? handleWidth : handleX;
        ctx.DrawLine(dividerX, 0, dividerX, adjustedViewportHeight, _paintCache.DragHandleDivider);
    }

    /// <summary>
    /// Draws the drag handle glyph in a 16 px square centered in the given cell area.
    /// </summary>
    private static void DrawGripIcon(IDrawingContext ctx, IKumikoIconProvider icons,
        float cellX, float cellY, float cellWidth, float cellHeight, KumikoPaint paint)
    {
        const float size = 16f;
        float centerX = cellX + cellWidth / 2;
        float centerY = cellY + cellHeight / 2;
        icons.DrawIcon(ctx, KumikoIcon.DragHandle, new KumikoRect(centerX - size / 2, centerY - size / 2, size, size), paint);
    }

    /// <summary>
    /// Draws a ghost of the dragged row at the drag position and a line where the drop lands. Called inside the
    /// grouping panel offset, so the viewport coordinates of <paramref name="dragScreenY"/> and of
    /// <see cref="RowDragGeometry"/> are shifted up by <paramref name="groupPanelOffset"/>. The ghost's texts are read
    /// once per drag (<see cref="FillGhostTexts"/>), so a steady drag frame allocates nothing.
    /// </summary>
    private void DrawRowDragOverlay(
        IDrawingContext ctx,
        KumikoSource dataSource,
        IReadOnlyList<KumikoColumn> columns,
        int dragRowIndex,
        float dragScreenY,
        ScrollState scroll,
        KumikoStyle style,
        float groupPanelOffset,
        float handleOffset = 0,
        float rightHandleWidth = 0)
    {
        if (dragRowIndex < 0 || dragRowIndex >= dataSource.RowCount || dataSource.IsNonDataRow(dragRowIndex)) return;

        float rowWidth = scroll.ViewportWidth;
        float rowHeight = style.RowHeight;
        float ghostTop = RowDragGeometry.ClampGhostTop(dragScreenY, scroll, style, dataSource);
        float ghostY = ghostTop - groupPanelOffset;

        // ── Ghost row ──
        var ghostRect = new KumikoRect(handleOffset, ghostY, rowWidth - handleOffset - rightHandleWidth, rowHeight);
        ctx.FillRect(ghostRect, _paintCache.RowDragGhost);
        ctx.DrawRect(ghostRect, _paintCache.RowDragBorder);

        FillGhostTexts(dataSource, columns, dragRowIndex);
        float cellX = handleOffset - scroll.OffsetX;
        for (int c = 0; c < columns.Count; c++)
        {
            if (!columns[c].IsVisible) continue;
            float colWidth = columns[c].Width;

            // Skip cells outside the visible area
            string? cellText = _ghostTexts[c];
            if (cellX + colWidth > handleOffset && cellX < rowWidth - rightHandleWidth && !string.IsNullOrEmpty(cellText))
            {
                var textRect = new KumikoRect(
                    cellX + style.CellPadding, ghostY,
                    colWidth - style.CellPadding * 2, rowHeight);
                ctx.DrawTextInRect(cellText, textRect, _paintCache.RowDragText,
                    KumikoTextAlignment.Left, KumikoVerticalAlignment.Center);
            }

            cellX += colWidth;
        }

        // ── Drop indicator line ──
        // Where the drop lands: the row under the ghost's middle, the same computation the input controller drops on.
        // No line when the drop would not move the row (onto itself, a group row or another group).
        int target = RowDragGeometry.DropTarget(ghostTop, scroll, style, dataSource);
        if (!RowDragGeometry.IsDroppable(dragRowIndex, target, dataSource)) return;

        float indicatorY = RowDragGeometry.IndicatorY(dragRowIndex, target, scroll, style, dataSource) - groupPanelOffset;
        float indicatorLeft = handleOffset;
        float indicatorRight = rowWidth - rightHandleWidth;
        ctx.DrawLine(indicatorLeft, indicatorY, indicatorRight, indicatorY, _paintCache.RowDropIndicator);

        // Short ticks at both ends of the indicator line for visibility
        float triSize = 6f;
        ctx.DrawLine(indicatorLeft, indicatorY - triSize, indicatorLeft, indicatorY + triSize, _paintCache.RowDropIndicator);
        ctx.DrawLine(indicatorRight, indicatorY - triSize, indicatorRight, indicatorY + triSize, _paintCache.RowDropIndicator);
    }

    /// <summary>
    /// Reads the display texts of the dragged row's cells into <see cref="_ghostTexts"/> once per drag (again only
    /// when the row shows another item, or the columns change), so drawing the ghost every frame allocates nothing.
    /// </summary>
    private void FillGhostTexts(KumikoSource dataSource, IReadOnlyList<KumikoColumn> columns, int row)
    {
        object item = dataSource.GetItem(row);
        if (ReferenceEquals(item, _ghostItem) && _ghostColumnCount == columns.Count) return;

        if (_ghostTexts.Length < columns.Count) _ghostTexts = new string?[columns.Count];
        for (int c = 0; c < columns.Count; c++)
            _ghostTexts[c] = columns[c].IsVisible ? dataSource.GetCellDisplayText(row, columns[c]) : null;
        _ghostItem = item;
        _ghostColumnCount = columns.Count;
    }
}

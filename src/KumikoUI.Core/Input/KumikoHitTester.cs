using KumikoUI.Core.Layout;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Input;

/// <summary>
/// Performs hit testing against the grid layout.
/// Determines what the user clicked/touched.
/// </summary>
public sealed class KumikoHitTester
{
    /// <summary>
    /// The regions the renderer measured and drew in its last frame (the grouping panel's chips), set when a renderer
    /// is connected to the controller that owns this tester (<see cref="KumikoRenderer.InputController"/>). Without
    /// them the hit tester estimates the chips.
    /// </summary>
    internal RenderedHitRegions? RenderedRegions { get; set; }

    /// <summary>
    /// Grid-level switch for the header filter icon: when false no column reports
    /// <see cref="HitRegion.HeaderFilterIcon"/>, whatever its own <see cref="KumikoColumn.AllowFiltering"/>.
    /// </summary>
    public bool AllowFiltering { get; set; } = true;

    // Sticky group headers pinned for the scroll offset being hit-tested (#174); reused by every hit test
    private readonly List<PinnedGroupHeader> _pinnedGroupHeaders = new();

    /// <summary>
    /// Resolve a viewport coordinate to a grid region, row, and column.
    /// Properly handles left-frozen, right-frozen, and frozen rows.
    /// </summary>
    public KumikoHitTestResult HitTest(
        float viewportX, float viewportY,
        ScrollState scroll, KumikoStyle style,
        IReadOnlyList<KumikoColumn> columns, int totalRows,
        KumikoSource? dataSource = null)
        => HitTestCore(viewportX, viewportY, scroll, style, columns, totalRows, dataSource).ToResult();

    /// <summary>
    /// Resolve a viewport coordinate to a grid region, row, and column for a pointer of type
    /// <paramref name="deviceType"/>: a finger gets wider targets (the column resize grip is
    /// <see cref="KumikoStyle.TouchColumnResizeGripWidth"/> wide, the filter icon, group chevrons and group chips take a
    /// larger margin).
    /// </summary>
    /// <param name="viewportX">X in the grid's viewport, from its left edge.</param>
    /// <param name="viewportY">Y in the grid's viewport, from its top edge (grouping panel included).</param>
    /// <param name="scroll">The scroll state.</param>
    /// <param name="style">The grid style.</param>
    /// <param name="columns">The columns, in display order.</param>
    /// <param name="totalRows">The number of rows in the view.</param>
    /// <param name="dataSource">The source, for the grouping panel, summary rows and group rows; null for none.</param>
    /// <param name="deviceType">The kind of pointer the point comes from.</param>
    /// <returns>What the point is on.</returns>
    public KumikoHitTestResult HitTest(
        float viewportX, float viewportY,
        ScrollState scroll, KumikoStyle style,
        IReadOnlyList<KumikoColumn> columns, int totalRows,
        KumikoSource? dataSource, KumikoPointerDeviceType deviceType)
        => HitTestCore(viewportX, viewportY, scroll, style, columns, totalRows, dataSource, deviceType).ToResult();

    /// <summary>
    /// Whether a column's header edge is a resize grip: it allows resizing and its width can change
    /// (<see cref="KumikoColumn.MaxWidth"/> above <see cref="KumikoColumn.MinWidth"/>). Elsewhere the edge
    /// is part of the header cell.
    /// </summary>
    internal static bool IsResizable(KumikoColumn column) =>
        column.AllowResize && column.MaxWidth > column.MinWidth;

    /// <summary>
    /// <see cref="HitTest(float, float, ScrollState, KumikoStyle, IReadOnlyList{KumikoColumn}, int, KumikoSource?, KumikoPointerDeviceType)"/>
    /// without allocating: the result is a value. Used on every hover move.
    /// </summary>
    internal KumikoHitInfo HitTestCore(
        float viewportX, float viewportY,
        ScrollState scroll, KumikoStyle style,
        IReadOnlyList<KumikoColumn> columns, int totalRows,
        KumikoSource? dataSource = null,
        KumikoPointerDeviceType deviceType = KumikoPointerDeviceType.Mouse)
    {
        // Pinned bottom summary footer first: it is drawn last, at the bottom of the viewport, over the
        // scrolling rows — and over the header and grouping panel when the viewport is short — so a
        // point inside it must never reach anything underneath.
        int bottomSummaryCount = dataSource?.BottomSummaryCount ?? 0;
        float pinnedFooterHeight = style.GetPinnedBottomSummaryHeight(bottomSummaryCount);
        if (pinnedFooterHeight > 0 && viewportY >= scroll.ViewportHeight - pinnedFooterHeight)
        {
            return new KumikoHitInfo { Region = HitRegion.TableSummaryRow, ContentX = viewportX, ContentY = viewportY };
        }

        // Check grouping panel area
        float groupPanelOffset = 0;
        if (dataSource != null && dataSource.IsGroupingActive)
        {
            groupPanelOffset = style.GroupPanelHeight;
            if (viewportY < groupPanelOffset)
            {
                return HitTestGroupPanel(viewportX, viewportY, style, dataSource, deviceType);
            }
        }

        // Adjust Y for grouping panel offset
        float adjustedY = viewportY - groupPanelOffset;

        // Compute frozen column widths, with the panes the renderer draws for this viewport (#268)
        FrozenPanes.Resolve(columns, style, scroll.ViewportWidth);
        var (frozenWidth, rightFrozenWidth) = FrozenPanes.GetWidths(columns);

        // Compute drag handle offsets
        float handleOffset = 0, rightHandleWidth = 0;
        if (style.ShowRowDragHandle)
        {
            if (style.RowDragHandlePosition == DragHandlePosition.Left)
                handleOffset = style.RowDragHandleWidth;
            else
                rightHandleWidth = style.RowDragHandleWidth;
        }

        float rightFrozenLeft = scroll.ViewportWidth - rightHandleWidth - rightFrozenWidth;

        // columnAreaViewportX is viewportX shifted into the column content area
        // (after the left drag handle, if any)
        float columnAreaViewportX = viewportX - handleOffset;

        // Determine which column pane the X coordinate falls in and compute the
        // effective content X for column hit testing
        float effectiveContentX;
        if (frozenWidth > 0 && columnAreaViewportX >= 0 && columnAreaViewportX < frozenWidth)
        {
            // Left-frozen pane: columnAreaViewportX IS the content X (frozen cols start at 0)
            effectiveContentX = columnAreaViewportX;
        }
        else if (rightFrozenWidth > 0 && viewportX >= rightFrozenLeft)
        {
            // Right-frozen pane: map viewport X to the right-frozen column positions
            effectiveContentX = viewportX;
        }
        else
        {
            // Scrollable pane (or drag handle area): content coordinate
            effectiveContentX = columnAreaViewportX + scroll.OffsetX;
        }

        float contentY = adjustedY + scroll.OffsetY;

        // Check if in header area (header doesn't scroll vertically)
        bool inHeader = adjustedY < style.HeaderHeight;

        if (inHeader)
        {
            return HitTestHeader(columnAreaViewportX, effectiveContentX, contentY, columns, scroll, style,
                frozenWidth, rightFrozenWidth, rightFrozenLeft, handleOffset, deviceType);
        }

        // Account for top summary height
        float topSummaryHeight = dataSource != null ? dataSource.TopSummaryCount * style.SummaryRowHeight : 0;

        // Check if in top summary area
        if (adjustedY >= style.HeaderHeight && adjustedY < style.HeaderHeight + topSummaryHeight)
        {
            return new KumikoHitInfo { Region = HitRegion.TableSummaryRow, ContentX = effectiveContentX, ContentY = contentY };
        }

        // Compute frozen rows
        int frozenRowCount = dataSource?.EffectiveFrozenRowCount ?? 0;
        float frozenRowHeight = frozenRowCount * style.RowHeight;

        // Data area — find row (offset by top summary height and frozen rows)
        float dataAreaTop = style.HeaderHeight + topSummaryHeight;

        // Sticky group headers (#174) are drawn over the scrolling rows, so a point on one hits that group's header,
        // never the row beneath. Outermost first: it is drawn on top of a pushed-up inner header.
        float scrollingRowsTop = dataAreaTop + frozenRowHeight;
        if (dataSource != null && adjustedY >= scrollingRowsTop)
        {
            PinnedGroupHeaders.Compute(dataSource, style, scrollingRowsTop, scroll.OffsetY, frozenRowCount, _pinnedGroupHeaders);
            foreach (var pinned in _pinnedGroupHeaders)
            {
                if (adjustedY >= pinned.Y && adjustedY < pinned.Y + style.RowHeight
                    && dataSource.GetGroupHeaderInfo(pinned.Row) is { } pinnedInfo)
                {
                    return GroupHeaderHit(pinned.Row, pinnedInfo, viewportX, effectiveContentX, contentY, style,
                        handleOffset, deviceType);
                }
            }
        }

        // Unpinned bottom summary rows scroll with the content, directly after the last row.
        if (pinnedFooterHeight == 0 && bottomSummaryCount > 0)
        {
            float bottomSummaryTop = dataAreaTop + totalRows * style.RowHeight - scroll.OffsetY;
            if (adjustedY >= bottomSummaryTop && adjustedY < bottomSummaryTop + bottomSummaryCount * style.SummaryRowHeight)
            {
                return new KumikoHitInfo { Region = HitRegion.TableSummaryRow, ContentX = effectiveContentX, ContentY = contentY };
            }
        }
        int row;

        if (frozenRowCount > 0 && adjustedY >= dataAreaTop && adjustedY < dataAreaTop + frozenRowHeight)
        {
            // Click is in the frozen row area (fixed, doesn't scroll)
            row = (int)((adjustedY - dataAreaTop) / style.RowHeight);
            row = Math.Clamp(row, 0, frozenRowCount - 1);
        }
        else
        {
            // Click is in the scrollable row area
            float scrollableDataTop = dataAreaTop + frozenRowHeight;
            row = (int)((adjustedY - scrollableDataTop + scroll.OffsetY) / style.RowHeight) + frozenRowCount;
        }

        if (row < 0 || row >= totalRows)
        {
            return new KumikoHitInfo { Region = HitRegion.EmptyArea, ContentX = effectiveContentX, ContentY = contentY };
        }

        // Check if this row is a group summary
        if (dataSource != null && dataSource.IsGroupSummaryRow(row))
        {
            return new KumikoHitInfo
            {
                Region = HitRegion.GroupSummaryRow,
                RowIndex = row,
                ContentX = effectiveContentX,
                ContentY = contentY
            };
        }

        // Check if this row is a group header
        if (dataSource != null && dataSource.IsGroupHeaderRow(row))
        {
            var groupInfo = dataSource.GetGroupHeaderInfo(row);
            if (groupInfo != null)
                return GroupHeaderHit(row, groupInfo, viewportX, effectiveContentX, contentY, style, handleOffset, deviceType);
        }

        // Check if click is on the row drag handle
        if (style.ShowRowDragHandle && dataSource != null && !dataSource.IsNonDataRow(row))
        {
            float handleWidth = style.RowDragHandleWidth;
            bool isOnHandle;
            if (style.RowDragHandlePosition == DragHandlePosition.Left)
            {
                isOnHandle = viewportX < handleWidth;
            }
            else
            {
                isOnHandle = viewportX >= scroll.ViewportWidth - handleWidth;
            }

            if (isOnHandle)
            {
                return new KumikoHitInfo
                {
                    Region = HitRegion.RowDragHandle,
                    RowIndex = row,
                    ContentX = effectiveContentX,
                    ContentY = contentY
                };
            }
        }

        // Find column — use the correct coordinate depending on pane
        return HitTestColumn(columnAreaViewportX, effectiveContentX, contentY, row, columns, scroll,
            frozenWidth, rightFrozenWidth, rightFrozenLeft, handleOffset);
    }

    /// <summary>A hit on a group header row: its chevron, or the rest of the row.</summary>
    private static KumikoHitInfo GroupHeaderHit(
        int row, GroupHeaderInfo groupInfo, float viewportX, float effectiveContentX, float contentY, KumikoStyle style,
        float handleOffset, KumikoPointerDeviceType deviceType)
    {
        // The chevron as the renderer draws it: after the left drag handle, indented by the group's level. Viewport X
        // (not content X), since group headers span the full viewport.
        bool onChevron = InteractionGeometry.IsOnGroupChevron(viewportX, groupInfo.Level, style, handleOffset, deviceType);
        return new KumikoHitInfo
        {
            Region = onChevron ? HitRegion.GroupChevron : HitRegion.GroupHeaderRow,
            RowIndex = row,
            ContentX = effectiveContentX,
            ContentY = contentY
        };
    }

    /// <summary>
    /// Hit test a column header, properly handling frozen, scrollable, and right-frozen panes.
    /// viewportX here is columnAreaViewportX (already offset by drag handle width).
    /// </summary>
    private KumikoHitInfo HitTestHeader(
        float viewportX, float effectiveContentX, float contentY,
        IReadOnlyList<KumikoColumn> columns, ScrollState scroll, KumikoStyle style,
        float frozenWidth, float rightFrozenWidth, float rightFrozenLeft, float handleOffset,
        KumikoPointerDeviceType deviceType)
    {
        // Try left-frozen columns first (column-area coords, starting at 0)
        if (frozenWidth > 0 && viewportX >= 0 && viewportX < frozenWidth)
        {
            float x = 0;
            for (int i = 0; i < columns.Count; i++)
            {
                var col = columns[i];
                if (!col.IsVisible || col.Pane != ColumnFreezeMode.Left) continue;
                float colRight = x + col.Width;
                var result = CheckHeaderColumn(columns, i, x, colRight, viewportX, effectiveContentX, contentY, style, deviceType);
                if (result.HasValue) return result.Value;
                x += col.Width;
            }
        }

        // Try right-frozen columns (column-area coords)
        float columnAreaRightFrozenLeft = rightFrozenLeft - handleOffset;
        if (rightFrozenWidth > 0 && viewportX >= columnAreaRightFrozenLeft)
        {
            float x = columnAreaRightFrozenLeft;
            for (int i = 0; i < columns.Count; i++)
            {
                var col = columns[i];
                if (!col.IsVisible || col.Pane != ColumnFreezeMode.Right) continue;
                float colRight = x + col.Width;
                var result = CheckHeaderColumn(columns, i, x, colRight, viewportX, effectiveContentX, contentY, style, deviceType);
                if (result.HasValue) return result.Value;
                x += col.Width;
            }
        }

        // Scrollable columns (content coords)
        {
            float x = frozenWidth; // scrollable columns start after frozen in content space
            float scrollContentX = viewportX + scroll.OffsetX;
            // In on-screen order, three passes in one loop: unpinned left-frozen columns, then the scrolling ones, then
            // unpinned right-frozen ones (FrozenPanes.ScrollPass).
            for (int k = 0, n = columns.Count; k < 3 * n; k++)
            {
                int i = k % n;
                var col = columns[i];
                if (!col.IsVisible || FrozenPanes.ScrollPass(col) != k / n) continue;
                float colRight = x + col.Width;

                float grip = IsResizable(col) ? InteractionGeometry.ResizeGripHalfWidth(col, style, deviceType) : 0f;
                if (grip > 0 &&
                    scrollContentX >= colRight - grip &&
                    scrollContentX <= colRight + InteractionGeometry.ResizeGripReach(NextScrollColumnWidth(columns, k), style, deviceType))
                {
                    return new KumikoHitInfo
                    {
                        Region = HitRegion.HeaderResizeGrip,
                        ColumnIndex = i, Column = col,
                        ContentX = scrollContentX, ContentY = contentY
                    };
                }

                if (scrollContentX >= x && scrollContentX < colRight)
                {
                    if (AllowFiltering && col.AllowFiltering)
                    {
                        if (InteractionGeometry.IsOnFilterIcon(scrollContentX, x, col, style, deviceType,
                            InteractionGeometry.ResizeGripHalfWidth(col, style, deviceType)))
                        {
                            return new KumikoHitInfo
                            {
                                Region = HitRegion.HeaderFilterIcon,
                                ColumnIndex = i, Column = col,
                                ContentX = scrollContentX, ContentY = contentY
                            };
                        }
                    }

                    return new KumikoHitInfo
                    {
                        Region = HitRegion.HeaderCell,
                        ColumnIndex = i, Column = col,
                        ContentX = scrollContentX, ContentY = contentY
                    };
                }

                x += col.Width;
            }
        }

        return new KumikoHitInfo { Region = HitRegion.EmptyArea, ContentX = effectiveContentX, ContentY = contentY };
    }

    /// <summary>
    /// The width of the column drawn right after <c>columns[index]</c> in its frozen pane, or infinity for the last
    /// one. Only read near a column edge.
    /// </summary>
    private static float NextPaneColumnWidth(IReadOnlyList<KumikoColumn> columns, int index)
    {
        var pane = columns[index].Pane;
        for (int j = index + 1; j < columns.Count; j++)
        {
            var c = columns[j];
            if (c.IsVisible && c.Pane == pane) return c.Width;
        }
        return float.PositiveInfinity;
    }

    /// <summary>
    /// The width of the column drawn right after step <paramref name="k"/> of the scrolling pane's three-pass walk
    /// (<see cref="FrozenPanes.ScrollPass"/>), or infinity for the last one.
    /// </summary>
    private static float NextScrollColumnWidth(IReadOnlyList<KumikoColumn> columns, int k)
    {
        int n = columns.Count;
        for (int kk = k + 1; kk < 3 * n; kk++)
        {
            var c = columns[kk % n];
            if (c.IsVisible && FrozenPanes.ScrollPass(c) == kk / n) return c.Width;
        }
        return float.PositiveInfinity;
    }

    /// <summary>Check a single column header for resize grip, filter icon, or header cell hit.</summary>
    private KumikoHitInfo? CheckHeaderColumn(
        IReadOnlyList<KumikoColumn> columns, int index, float colLeft, float colRight,
        float testX, float effectiveContentX, float contentY, KumikoStyle style, KumikoPointerDeviceType deviceType)
    {
        var col = columns[index];
        float grip = IsResizable(col) ? InteractionGeometry.ResizeGripHalfWidth(col, style, deviceType) : 0f;
        if (grip > 0 &&
            testX >= colRight - grip &&
            testX <= colRight + InteractionGeometry.ResizeGripReach(NextPaneColumnWidth(columns, index), style, deviceType))
        {
            return new KumikoHitInfo
            {
                Region = HitRegion.HeaderResizeGrip,
                ColumnIndex = index, Column = col,
                ContentX = effectiveContentX, ContentY = contentY
            };
        }

        if (testX >= colLeft && testX < colRight)
        {
            if (AllowFiltering && col.AllowFiltering)
            {
                if (InteractionGeometry.IsOnFilterIcon(testX, colLeft, col, style, deviceType,
                    InteractionGeometry.ResizeGripHalfWidth(col, style, deviceType)))
                {
                    return new KumikoHitInfo
                    {
                        Region = HitRegion.HeaderFilterIcon,
                        ColumnIndex = index, Column = col,
                        ContentX = effectiveContentX, ContentY = contentY
                    };
                }
            }

            return new KumikoHitInfo
            {
                Region = HitRegion.HeaderCell,
                ColumnIndex = index, Column = col,
                ContentX = effectiveContentX, ContentY = contentY
            };
        }

        return null;
    }

    /// <summary>
    /// Hit test a data cell column, handling frozen, scrollable, and right-frozen columns.
    /// viewportX here is columnAreaViewportX (already offset by drag handle width).
    /// </summary>
    private KumikoHitInfo HitTestColumn(
        float viewportX, float effectiveContentX, float contentY, int row,
        IReadOnlyList<KumikoColumn> columns, ScrollState scroll,
        float frozenWidth, float rightFrozenWidth, float rightFrozenLeft, float handleOffset)
    {
        // Check left-frozen columns (column-area coords, starting at 0)
        if (frozenWidth > 0 && viewportX >= 0 && viewportX < frozenWidth)
        {
            float x = 0;
            for (int i = 0; i < columns.Count; i++)
            {
                var col = columns[i];
                if (!col.IsVisible || col.Pane != ColumnFreezeMode.Left) continue;
                if (viewportX >= x && viewportX < x + col.Width)
                {
                    return new KumikoHitInfo
                    {
                        Region = HitRegion.Cell, RowIndex = row,
                        ColumnIndex = i, Column = col,
                        ContentX = effectiveContentX, ContentY = contentY
                    };
                }
                x += col.Width;
            }
        }

        // Check right-frozen columns (column-area coords)
        float columnAreaRightFrozenLeft = rightFrozenLeft - handleOffset;
        if (rightFrozenWidth > 0 && viewportX >= columnAreaRightFrozenLeft)
        {
            float x = columnAreaRightFrozenLeft;
            for (int i = 0; i < columns.Count; i++)
            {
                var col = columns[i];
                if (!col.IsVisible || col.Pane != ColumnFreezeMode.Right) continue;
                if (viewportX >= x && viewportX < x + col.Width)
                {
                    return new KumikoHitInfo
                    {
                        Region = HitRegion.Cell, RowIndex = row,
                        ColumnIndex = i, Column = col,
                        ContentX = effectiveContentX, ContentY = contentY
                    };
                }
                x += col.Width;
            }
        }

        // Scrollable columns
        float scrollContentX = viewportX + scroll.OffsetX;
        float cx = frozenWidth;
        // In on-screen order, three passes in one loop: unpinned left-frozen columns, then the scrolling ones, then
        // unpinned right-frozen ones (FrozenPanes.ScrollPass).
        for (int k = 0, n = columns.Count; k < 3 * n; k++)
        {
            int i = k % n;
            var col = columns[i];
            if (!col.IsVisible || FrozenPanes.ScrollPass(col) != k / n) continue;
            if (scrollContentX >= cx && scrollContentX < cx + col.Width)
            {
                return new KumikoHitInfo
                {
                    Region = HitRegion.Cell, RowIndex = row,
                    ColumnIndex = i, Column = col,
                    ContentX = scrollContentX, ContentY = contentY
                };
            }
            cx += col.Width;
        }

        return new KumikoHitInfo
        {
            Region = HitRegion.EmptyArea, RowIndex = row,
            ContentX = effectiveContentX, ContentY = contentY
        };
    }
    /// <summary>
    /// Hit test within the grouping panel area (chips and remove buttons): against the chips the renderer drew in its
    /// last frame (<see cref="RenderedRegions"/>), or, before a frame or after the groups changed, an estimate of them.
    /// </summary>
    private KumikoHitInfo HitTestGroupPanel(
        float viewportX, float viewportY, KumikoStyle style, KumikoSource dataSource, KumikoPointerDeviceType deviceType)
    {
        var groups = dataSource.GroupDescriptions;
        if (groups.Count == 0)
            return new KumikoHitInfo { Region = HitRegion.EmptyArea };

        float panelHeight = style.GroupPanelHeight;
        if (RenderedRegions is { } rendered && rendered.Matches(groups, panelHeight))
        {
            var chips = rendered.Chips;
            for (int i = 0; i < chips.Count; i++)
            {
                var hit = HitTestChip(chips[i].Bounds, i, viewportX, viewportY, panelHeight, deviceType);
                if (hit.HasValue) return hit.Value;
            }
            return new KumikoHitInfo { Region = HitRegion.EmptyArea };
        }

        // No frame drawn for these groups yet: the renderer's layout with text widths estimated at 0.6 of the font
        // size per character (there is no drawing context to measure with here).
        float labelApproxWidth = dataSource.Strings.GroupPanelLabel.Length * style.GroupPanelLabelFont.Size * 0.6f;
        float chipX = InteractionGeometry.FirstChipX(labelApproxWidth, style);
        float chipY = InteractionGeometry.ChipY(panelHeight);

        for (int i = 0; i < groups.Count; i++)
        {
            string displayName = ChipText(groups[i], dataSource.Columns);
            float chipWidth = InteractionGeometry.ChipWidth(displayName.Length * style.GroupPanelFont.Size * 0.6f);
            var chip = new Rendering.KumikoRect(chipX, chipY, chipWidth, InteractionGeometry.ChipHeight);
            var hit = HitTestChip(chip, i, viewportX, viewportY, panelHeight, deviceType);
            if (hit.HasValue) return hit.Value;
            chipX = InteractionGeometry.NextChipX(chipX, chipWidth);
        }

        return new KumikoHitInfo { Region = HitRegion.EmptyArea };
    }

    /// <summary>The chip at <paramref name="chip"/> under the point: its remove button, the rest of it, or null.</summary>
    private static KumikoHitInfo? HitTestChip(
        Rendering.KumikoRect chip, int index, float x, float y, float panelHeight, KumikoPointerDeviceType deviceType)
    {
        if (!InteractionGeometry.ChipTarget(chip, panelHeight, deviceType).Contains(x, y)) return null;
        var remove = InteractionGeometry.ChipRemoveRect(chip);
        return new KumikoHitInfo
        {
            Region = x >= remove.X ? HitRegion.GroupPanelChipRemove : HitRegion.GroupPanelChip,
            GroupDescriptionIndex = index,
        };
    }

    /// <summary>
    /// The text a grouping panel chip shows: the group's header, else the header of the column it groups by, else the
    /// property name. A loop, not FirstOrDefault with a capturing lambda: the hit tester runs on every hover move.
    /// </summary>
    internal static string ChipText(GroupDescription group, IReadOnlyList<KumikoColumn> columns)
    {
        if (group.Header != null) return group.Header;
        for (int c = 0; c < columns.Count; c++)
        {
            if (string.Equals(columns[c].PropertyName, group.PropertyName, StringComparison.OrdinalIgnoreCase))
                return columns[c].Header;
        }
        return group.PropertyName;
    }
}

/// <summary>
/// The allocation-free form of <see cref="KumikoHitTestResult"/> that <see cref="KumikoHitTester"/> computes.
/// </summary>
internal readonly struct KumikoHitInfo
{
    public KumikoHitInfo() { }

    public HitRegion Region { get; init; }
    public int RowIndex { get; init; } = -1;
    public int ColumnIndex { get; init; } = -1;
    public KumikoColumn? Column { get; init; }
    public float ContentX { get; init; }
    public float ContentY { get; init; }
    public int GroupDescriptionIndex { get; init; } = -1;

    public KumikoHitTestResult ToResult() => new()
    {
        Region = Region,
        RowIndex = RowIndex,
        ColumnIndex = ColumnIndex,
        Column = Column,
        ContentX = ContentX,
        ContentY = ContentY,
        GroupDescriptionIndex = GroupDescriptionIndex,
    };
}

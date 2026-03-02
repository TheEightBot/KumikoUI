using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Input;

/// <summary>
/// Identifies what region of the grid a point falls in.
/// </summary>
public enum HitRegion
{
    None,
    Cell,
    HeaderCell,
    HeaderResizeGrip,
    HeaderFilterIcon,
    RowHeader,
    RowDragHandle,
    FrozenColumn,
    EmptyArea,
    GroupHeaderRow,
    GroupChevron,
    GroupPanelChip,
    GroupPanelChipRemove,
    GroupSummaryRow,
    TableSummaryRow
}

/// <summary>
/// Result of a hit test — identifies the grid region, row/column, and exact position.
/// </summary>
public class HitTestResult
{
    public HitRegion Region { get; init; }
    public int RowIndex { get; init; } = -1;
    public int ColumnIndex { get; init; } = -1;
    public DataGridColumn? Column { get; init; }
    public float ContentX { get; init; }
    public float ContentY { get; init; }

    /// <summary>Index into GroupDescriptions for GroupPanelChip/GroupPanelChipRemove hits.</summary>
    public int GroupDescriptionIndex { get; init; } = -1;

    public static HitTestResult Empty => new() { Region = HitRegion.None };
}

/// <summary>
/// Performs hit testing against the grid layout.
/// Determines what the user clicked/touched.
/// </summary>
public class GridHitTester
{
    private const float ResizeGripWidth = 6f;
    private const float FilterIconSize = 12f;
    private const float FilterIconRightMargin = 20f; // Right of sort indicator space

    /// <summary>
    /// Resolve a viewport coordinate to a grid region, row, and column.
    /// Properly handles left-frozen, right-frozen, and frozen rows.
    /// </summary>
    public HitTestResult HitTest(
        float viewportX, float viewportY,
        ScrollState scroll, DataGridStyle style,
        IReadOnlyList<DataGridColumn> columns, int totalRows,
        DataGridSource? dataSource = null)
    {
        // Check grouping panel area first
        float groupPanelOffset = 0;
        if (dataSource != null && dataSource.IsGroupingActive)
        {
            groupPanelOffset = style.GroupPanelHeight;
            if (viewportY < groupPanelOffset)
            {
                return HitTestGroupPanel(viewportX, viewportY, scroll, style, dataSource);
            }
        }

        // Adjust Y for grouping panel offset
        float adjustedY = viewportY - groupPanelOffset;

        // Compute frozen column widths
        float frozenWidth = 0;
        float rightFrozenWidth = 0;
        foreach (var col in columns)
        {
            if (!col.IsVisible) continue;
            if (col.IsFrozen) frozenWidth += col.Width;
            if (col.IsFrozenRight) rightFrozenWidth += col.Width;
        }

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
                frozenWidth, rightFrozenWidth, rightFrozenLeft, handleOffset);
        }

        // Account for top summary height
        float topSummaryHeight = dataSource != null ? dataSource.TopSummaryCount * style.SummaryRowHeight : 0;

        // Check if in top summary area
        if (adjustedY >= style.HeaderHeight && adjustedY < style.HeaderHeight + topSummaryHeight)
        {
            return new HitTestResult { Region = HitRegion.TableSummaryRow, ContentX = effectiveContentX, ContentY = contentY };
        }

        // Compute frozen rows
        int frozenRowCount = dataSource?.EffectiveFrozenRowCount ?? 0;
        float frozenRowHeight = frozenRowCount * style.RowHeight;

        // Data area — find row (offset by top summary height and frozen rows)
        float dataAreaTop = style.HeaderHeight + topSummaryHeight;
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
            return new HitTestResult { Region = HitRegion.EmptyArea, ContentX = effectiveContentX, ContentY = contentY };
        }

        // Check if this row is a group summary
        if (dataSource != null && dataSource.IsGroupSummaryRow(row))
        {
            return new HitTestResult
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
            {
                // Check if click is on the chevron area
                float indent = style.CellPadding + groupInfo.Level * style.GroupIndentWidth;
                float chevronLeft = indent;
                float chevronRight = indent + style.GroupChevronSize;

                // Use viewport X (not content X) for the chevron check
                // since group headers span the full viewport
                if (viewportX >= chevronLeft && viewportX <= chevronRight + style.CellPadding)
                {
                    return new HitTestResult
                    {
                        Region = HitRegion.GroupChevron,
                        RowIndex = row,
                        ContentX = effectiveContentX,
                        ContentY = contentY
                    };
                }

                return new HitTestResult
                {
                    Region = HitRegion.GroupHeaderRow,
                    RowIndex = row,
                    ContentX = effectiveContentX,
                    ContentY = contentY
                };
            }
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
                return new HitTestResult
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

    /// <summary>
    /// Hit test a column header, properly handling frozen, scrollable, and right-frozen panes.
    /// viewportX here is columnAreaViewportX (already offset by drag handle width).
    /// </summary>
    private HitTestResult HitTestHeader(
        float viewportX, float effectiveContentX, float contentY,
        IReadOnlyList<DataGridColumn> columns, ScrollState scroll, DataGridStyle style,
        float frozenWidth, float rightFrozenWidth, float rightFrozenLeft, float handleOffset)
    {
        // Try left-frozen columns first (column-area coords, starting at 0)
        if (frozenWidth > 0 && viewportX >= 0 && viewportX < frozenWidth)
        {
            float x = 0;
            for (int i = 0; i < columns.Count; i++)
            {
                var col = columns[i];
                if (!col.IsVisible || !col.IsFrozen) continue;
                float colRight = x + col.Width;
                var result = CheckHeaderColumn(col, i, x, colRight, viewportX, effectiveContentX, contentY, style);
                if (result != null) return result;
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
                if (!col.IsVisible || !col.IsFrozenRight) continue;
                float colRight = x + col.Width;
                var result = CheckHeaderColumn(col, i, x, colRight, viewportX, effectiveContentX, contentY, style);
                if (result != null) return result;
                x += col.Width;
            }
        }

        // Scrollable columns (content coords)
        {
            float x = frozenWidth; // scrollable columns start after frozen in content space
            float scrollContentX = viewportX + scroll.OffsetX;
            for (int i = 0; i < columns.Count; i++)
            {
                var col = columns[i];
                if (!col.IsVisible || col.IsFrozen || col.IsFrozenRight) continue;
                float colRight = x + col.Width;

                if (col.AllowResize &&
                    scrollContentX >= colRight - ResizeGripWidth &&
                    scrollContentX <= colRight + ResizeGripWidth)
                {
                    return new HitTestResult
                    {
                        Region = HitRegion.HeaderResizeGrip,
                        ColumnIndex = i, Column = col,
                        ContentX = scrollContentX, ContentY = contentY
                    };
                }

                if (scrollContentX >= x && scrollContentX < colRight)
                {
                    if (col.AllowFiltering)
                    {
                        float filterIconX = colRight - FilterIconRightMargin - FilterIconSize;
                        if (scrollContentX >= filterIconX && scrollContentX <= colRight - ResizeGripWidth)
                        {
                            return new HitTestResult
                            {
                                Region = HitRegion.HeaderFilterIcon,
                                ColumnIndex = i, Column = col,
                                ContentX = scrollContentX, ContentY = contentY
                            };
                        }
                    }

                    return new HitTestResult
                    {
                        Region = HitRegion.HeaderCell,
                        ColumnIndex = i, Column = col,
                        ContentX = scrollContentX, ContentY = contentY
                    };
                }

                x += col.Width;
            }
        }

        return new HitTestResult { Region = HitRegion.EmptyArea, ContentX = effectiveContentX, ContentY = contentY };
    }

    /// <summary>Check a single column header for resize grip, filter icon, or header cell hit.</summary>
    private HitTestResult? CheckHeaderColumn(
        DataGridColumn col, int index, float colLeft, float colRight,
        float testX, float effectiveContentX, float contentY, DataGridStyle style)
    {
        if (col.AllowResize &&
            testX >= colRight - ResizeGripWidth &&
            testX <= colRight + ResizeGripWidth)
        {
            return new HitTestResult
            {
                Region = HitRegion.HeaderResizeGrip,
                ColumnIndex = index, Column = col,
                ContentX = effectiveContentX, ContentY = contentY
            };
        }

        if (testX >= colLeft && testX < colRight)
        {
            if (col.AllowFiltering)
            {
                float filterIconX = colRight - FilterIconRightMargin - FilterIconSize;
                if (testX >= filterIconX && testX <= colRight - ResizeGripWidth)
                {
                    return new HitTestResult
                    {
                        Region = HitRegion.HeaderFilterIcon,
                        ColumnIndex = index, Column = col,
                        ContentX = effectiveContentX, ContentY = contentY
                    };
                }
            }

            return new HitTestResult
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
    private HitTestResult HitTestColumn(
        float viewportX, float effectiveContentX, float contentY, int row,
        IReadOnlyList<DataGridColumn> columns, ScrollState scroll,
        float frozenWidth, float rightFrozenWidth, float rightFrozenLeft, float handleOffset)
    {
        // Check left-frozen columns (column-area coords, starting at 0)
        if (frozenWidth > 0 && viewportX >= 0 && viewportX < frozenWidth)
        {
            float x = 0;
            for (int i = 0; i < columns.Count; i++)
            {
                var col = columns[i];
                if (!col.IsVisible || !col.IsFrozen) continue;
                if (viewportX >= x && viewportX < x + col.Width)
                {
                    return new HitTestResult
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
                if (!col.IsVisible || !col.IsFrozenRight) continue;
                if (viewportX >= x && viewportX < x + col.Width)
                {
                    return new HitTestResult
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
        for (int i = 0; i < columns.Count; i++)
        {
            var col = columns[i];
            if (!col.IsVisible || col.IsFrozen || col.IsFrozenRight) continue;
            if (scrollContentX >= cx && scrollContentX < cx + col.Width)
            {
                return new HitTestResult
                {
                    Region = HitRegion.Cell, RowIndex = row,
                    ColumnIndex = i, Column = col,
                    ContentX = scrollContentX, ContentY = contentY
                };
            }
            cx += col.Width;
        }

        return new HitTestResult
        {
            Region = HitRegion.EmptyArea, RowIndex = row,
            ContentX = effectiveContentX, ContentY = contentY
        };
    }
    /// <summary>
    /// Hit test within the grouping panel area (chips and remove buttons).
    /// </summary>
    private HitTestResult HitTestGroupPanel(
        float viewportX, float viewportY,
        ScrollState scroll, DataGridStyle style,
        DataGridSource dataSource)
    {
        var groups = dataSource.GroupDescriptions;
        if (groups.Count == 0)
            return new HitTestResult { Region = HitRegion.EmptyArea };

        // Replicate the layout from DrawGroupingPanel
        // "Grouped by:" label takes space first
        float labelApproxWidth = 11 * style.GroupPanelLabelFont.Size * 0.6f; // "Grouped by:" ~ 11 chars
        float chipX = style.CellPadding + 2 + labelApproxWidth + 10;
        float chipHeight = 26;
        float chipY = (style.GroupPanelHeight - chipHeight) / 2;
        float chipPadding = 10;
        float chipGap = 4;
        float accentWidth = 4f;

        for (int i = 0; i < groups.Count; i++)
        {
            var group = groups[i];
            string displayName = group.Header ?? group.PropertyName;
            var col = dataSource.Columns.FirstOrDefault(c =>
                string.Equals(c.PropertyName, group.PropertyName, StringComparison.OrdinalIgnoreCase));
            if (col != null && group.Header == null)
                displayName = col.Header;

            // Approximate chip width (match renderer layout)
            float approxTextWidth = displayName.Length * style.GroupPanelFont.Size * 0.6f;
            float sortArrowWidth = 16;
            float removeButtonSize = 16;
            float chipWidth = accentWidth + chipPadding + approxTextWidth + 4 + sortArrowWidth + chipPadding + removeButtonSize + chipPadding / 2;

            if (viewportX >= chipX && viewportX <= chipX + chipWidth &&
                viewportY >= chipY && viewportY <= chipY + chipHeight)
            {
                // Check if on the remove button (rightmost portion of chip)
                float removeAreaX = chipX + chipWidth - removeButtonSize - chipPadding / 2 - 4;
                if (viewportX >= removeAreaX)
                {
                    return new HitTestResult
                    {
                        Region = HitRegion.GroupPanelChipRemove,
                        GroupDescriptionIndex = i
                    };
                }

                return new HitTestResult
                {
                    Region = HitRegion.GroupPanelChip,
                    GroupDescriptionIndex = i
                };
            }

            chipX += chipWidth + chipGap;

            // Skip separator space
            if (i < groups.Count - 1)
                chipX += 14 + chipGap;
        }

        return new HitTestResult { Region = HitRegion.EmptyArea };
    }
}

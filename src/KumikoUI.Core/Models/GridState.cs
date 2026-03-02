using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Models;

/// <summary>
/// Tracks scroll position and visible row/column ranges.
/// All values are in logical (unscaled) pixels.
/// </summary>
public class ScrollState
{
    /// <summary>Horizontal scroll offset in pixels.</summary>
    public float OffsetX { get; set; }

    /// <summary>Vertical scroll offset in pixels.</summary>
    public float OffsetY { get; set; }

    /// <summary>Total content width (sum of column widths).</summary>
    public float ContentWidth { get; set; }

    /// <summary>Total content height (row count × row height + header).</summary>
    public float ContentHeight { get; set; }

    /// <summary>Viewport width.</summary>
    public float ViewportWidth { get; set; }

    /// <summary>Viewport height.</summary>
    public float ViewportHeight { get; set; }

    /// <summary>Maximum horizontal scroll offset (content width minus viewport width, minimum 0).</summary>
    public float MaxOffsetX => Math.Max(0, ContentWidth - ViewportWidth);
    /// <summary>Maximum vertical scroll offset (content height minus viewport height, minimum 0).</summary>
    public float MaxOffsetY => Math.Max(0, ContentHeight - ViewportHeight);

    /// <summary>Clamps the current scroll offset to valid bounds.</summary>
    public void ClampOffset()
    {
        OffsetX = Math.Clamp(OffsetX, 0, MaxOffsetX);
        OffsetY = Math.Clamp(OffsetY, 0, MaxOffsetY);
    }

    /// <summary>Scrolls by the specified deltas and clamps the result.</summary>
    public void ScrollBy(float dx, float dy)
    {
        OffsetX += dx;
        OffsetY += dy;
        ClampOffset();
    }
}

/// <summary>
/// Theme / style settings for the grid renderer.
/// </summary>
public class DataGridStyle
{
    // ── Sizing ───────────────────────────────────────────────────
    /// <summary>Height of the header row in pixels. Default: 40.</summary>
    public float HeaderHeight { get; set; } = 40f;
    /// <summary>Height of each data row in pixels. Default: 36.</summary>
    public float RowHeight { get; set; } = 36f;
    /// <summary>Horizontal padding inside data cells in pixels. Default: 8.</summary>
    public float CellPadding { get; set; } = 8f;

    // ── Colors ───────────────────────────────────────────────────
    /// <summary>Background color of the grid canvas.</summary>
    public GridColor BackgroundColor { get; set; } = GridColor.White;
    /// <summary>Background color of the header row.</summary>
    public GridColor HeaderBackgroundColor { get; set; } = new(230, 230, 230);
    /// <summary>Text color for header cells.</summary>
    public GridColor HeaderTextColor { get; set; } = GridColor.Black;
    /// <summary>Default text color for data cells.</summary>
    public GridColor CellTextColor { get; set; } = new(30, 30, 30);
    /// <summary>Color of horizontal and vertical grid lines.</summary>
    public GridColor GridLineColor { get; set; } = new(220, 220, 220);
    /// <summary>Fill color of selected row/cell highlights.</summary>
    public GridColor SelectionColor { get; set; } = new(0, 120, 215, 60);
    /// <summary>Text color for selected cells (used in high-contrast themes).</summary>
    public GridColor SelectionTextColor { get; set; } = new(255, 255, 255);
    /// <summary>Subtle highlight color for the focused/current row.</summary>
    public GridColor FocusedRowColor { get; set; } = new(0, 120, 215, 30);
    /// <summary>Background color of alternate (even) rows.</summary>
    public GridColor AlternateRowColor { get; set; } = new(245, 245, 245);
    /// <summary>Color of the sort direction indicator arrow in the header.</summary>
    public GridColor SortIndicatorColor { get; set; } = new(100, 100, 100);
    /// <summary>Border color of the current (focused) cell.</summary>
    public GridColor CurrentCellBorderColor { get; set; } = new(0, 120, 215);
    /// <summary>Border width of the current cell indicator in pixels.</summary>
    public float CurrentCellBorderWidth { get; set; } = 2f;

    /// <summary>General accent color used for interactive elements (checkmarks, etc.).</summary>
    public GridColor AccentColor { get; set; } = new(0, 120, 215);

    // ── Fonts ────────────────────────────────────────────────────
    /// <summary>Font used for header cell text.</summary>
    public GridFont HeaderFont { get; set; } = new("Default", 14, bold: true);
    /// <summary>Font used for data cell text.</summary>
    public GridFont CellFont { get; set; } = new("Default", 13);

    // ── Header ───────────────────────────────────────────────────
    /// <summary>Border color drawn below the header row.</summary>
    public GridColor HeaderBorderColor { get; set; } = new(200, 200, 200);
    /// <summary>Padding inside header cells (overrides CellPadding for headers).</summary>
    public float HeaderPadding { get; set; } = 8f;

    // ── Grid lines ───────────────────────────────────────────────
    /// <summary>Width of grid lines in pixels. Default: 1.</summary>
    public float GridLineWidth { get; set; } = 1f;
    /// <summary>Whether to draw horizontal grid lines between rows.</summary>
    public bool ShowHorizontalGridLines { get; set; } = true;
    /// <summary>Whether to draw vertical grid lines between columns.</summary>
    public bool ShowVerticalGridLines { get; set; } = true;

    // ── Alternate rows ───────────────────────────────────────────
    /// <summary>Whether to use alternating row background colors.</summary>
    public bool AlternateRowBackground { get; set; } = true;

    // ── Frozen columns ───────────────────────────────────────────
    /// <summary>Divider line color between frozen and scrollable columns.</summary>
    public GridColor FrozenColumnDividerColor { get; set; } = new(180, 180, 180);
    /// <summary>Divider line width between frozen and scrollable columns.</summary>
    public float FrozenColumnDividerWidth { get; set; } = 2f;
    /// <summary>Background color for left-frozen column cells.</summary>
    public GridColor FrozenColumnBackgroundColor { get; set; } = new(250, 250, 250);

    // ── Right-frozen columns ─────────────────────────────────────
    /// <summary>Divider line color between scrollable and right-frozen columns.</summary>
    public GridColor RightFrozenColumnDividerColor { get; set; } = new(180, 180, 180);
    /// <summary>Divider line width between scrollable and right-frozen columns.</summary>
    public float RightFrozenColumnDividerWidth { get; set; } = 2f;
    /// <summary>Background color for right-frozen column cells.</summary>
    public GridColor RightFrozenColumnBackgroundColor { get; set; } = new(250, 250, 250);

    // ── Frozen rows ──────────────────────────────────────────────
    /// <summary>Divider line color below frozen rows.</summary>
    public GridColor FrozenRowDividerColor { get; set; } = new(180, 180, 180);
    /// <summary>Divider line width below frozen rows.</summary>
    public float FrozenRowDividerWidth { get; set; } = 2f;
    /// <summary>Background color for frozen row cells.</summary>
    public GridColor FrozenRowBackgroundColor { get; set; } = new(250, 250, 250);

    // ── Filter ───────────────────────────────────────────────
    /// <summary>Color of the filter icon in column headers.</summary>
    public GridColor FilterIconColor { get; set; } = new(140, 140, 140);
    /// <summary>Color of the filter icon when a filter is active on that column.</summary>
    public GridColor FilterActiveIconColor { get; set; } = new(0, 120, 215);

    // ── Grouping ─────────────────────────────────────────────
    /// <summary>Background color of group header rows.</summary>
    public GridColor GroupHeaderBackgroundColor { get; set; } = new(240, 240, 240);
    /// <summary>Text color for group header labels.</summary>
    public GridColor GroupHeaderTextColor { get; set; } = new(40, 40, 40);
    /// <summary>Color of the item count text displayed in group headers.</summary>
    public GridColor GroupHeaderCountColor { get; set; } = new(120, 120, 120);
    /// <summary>Color of the expand/collapse chevron icon in group headers.</summary>
    public GridColor GroupChevronColor { get; set; } = new(80, 80, 80);
    /// <summary>Background color behind the group expand/collapse chevron icon.</summary>
    public GridColor GroupChevronBackgroundColor { get; set; } = new(220, 225, 235);
    /// <summary>Font for the main text in group header rows.</summary>
    public GridFont GroupHeaderFont { get; set; } = new("Default", 13, bold: true);
    /// <summary>Font for the item count portion of group headers.</summary>
    public GridFont GroupCountFont { get; set; } = new("Default", 12);
    /// <summary>Width of each grouping indentation level in pixels.</summary>
    public float GroupIndentWidth { get; set; } = 24f;
    /// <summary>Size of the expand/collapse chevron icon in pixels.</summary>
    public float GroupChevronSize { get; set; } = 14f;
    /// <summary>Height of the group-by panel at the top of the grid.</summary>
    public float GroupPanelHeight { get; set; } = 40f;
    /// <summary>Background color of the group-by panel.</summary>
    public GridColor GroupPanelBackgroundColor { get; set; } = new(248, 249, 252);
    /// <summary>Text color used in the group-by panel.</summary>
    public GridColor GroupPanelTextColor { get; set; } = new(100, 105, 115);
    /// <summary>Color of the label text in the group-by panel (e.g., "Drag columns here").</summary>
    public GridColor GroupPanelLabelColor { get; set; } = new(130, 135, 145);
    /// <summary>Font for label text in the group-by panel.</summary>
    public GridFont GroupPanelLabelFont { get; set; } = new("Default", 11);
    /// <summary>Background color of group-by chips in the panel.</summary>
    public GridColor GroupPanelChipBackgroundColor { get; set; } = new(230, 235, 245);
    /// <summary>Border color of group-by chips in the panel.</summary>
    public GridColor GroupPanelChipBorderColor { get; set; } = new(180, 190, 210);
    /// <summary>Accent/indicator color on group-by chips.</summary>
    public GridColor GroupPanelChipAccentColor { get; set; } = new(60, 120, 215);
    /// <summary>Text color of group-by chips.</summary>
    public GridColor GroupPanelChipTextColor { get; set; } = new(40, 45, 55);
    /// <summary>Color of the remove (×) button on group-by chips.</summary>
    public GridColor GroupPanelChipRemoveColor { get; set; } = new(140, 145, 155);
    /// <summary>Font for group-by chip labels in the panel.</summary>
    public GridFont GroupPanelFont { get; set; } = new("Default", 12, bold: true);

    // ── Summaries ────────────────────────────────────────────
    /// <summary>Background color of the table summary row.</summary>
    public GridColor SummaryRowBackgroundColor { get; set; } = new(235, 240, 248);
    /// <summary>Text color for table summary values.</summary>
    public GridColor SummaryRowTextColor { get; set; } = new(30, 30, 30);
    /// <summary>Font for table summary values.</summary>
    public GridFont SummaryRowFont { get; set; } = new("Default", 13, bold: true);
    /// <summary>Text color for table summary labels (e.g., "Sum:", "Avg:").</summary>
    public GridColor SummaryRowLabelColor { get; set; } = new(100, 100, 100);
    /// <summary>Font for table summary labels.</summary>
    public GridFont SummaryRowLabelFont { get; set; } = new("Default", 12);
    /// <summary>Height of table summary rows in pixels.</summary>
    public float SummaryRowHeight { get; set; } = 36f;
    /// <summary>Border color of the table summary row.</summary>
    public GridColor SummaryRowBorderColor { get; set; } = new(200, 200, 200);

    // Group summary rows (after each group)
    /// <summary>Background color of group summary rows displayed after each group.</summary>
    public GridColor GroupSummaryRowBackgroundColor { get; set; } = new(242, 245, 250);
    /// <summary>Text color for group summary values.</summary>
    public GridColor GroupSummaryRowTextColor { get; set; } = new(50, 50, 50);
    /// <summary>Font for group summary row values.</summary>
    public GridFont GroupSummaryRowFont { get; set; } = new("Default", 12, bold: true);

    // Caption summary (inline in group header)
    /// <summary>Text color for caption summaries displayed inline within group headers.</summary>
    public GridColor CaptionSummaryTextColor { get; set; } = new(100, 100, 100);
    /// <summary>Font for caption summary text in group headers.</summary>
    public GridFont CaptionSummaryFont { get; set; } = new("Default", 11);

    // ── Row drag & drop ──────────────────────────────────────────
    /// <summary>Enable row drag &amp; drop reordering. Default is false.</summary>
    public bool AllowRowDragDrop { get; set; } = false;
    /// <summary>Semi-transparent overlay color applied to the row being dragged.</summary>
    public GridColor RowDragOverlayColor { get; set; } = new(255, 255, 255, 200);
    /// <summary>Border color around the row being dragged.</summary>
    public GridColor RowDragBorderColor { get; set; } = new(0, 120, 215);
    /// <summary>Color of the horizontal line indicating the drop target position.</summary>
    public GridColor RowDropIndicatorColor { get; set; } = new(0, 120, 215);
    /// <summary>Width of the drop indicator line in pixels.</summary>
    public float RowDropIndicatorWidth { get; set; } = 2f;

    // ── Row drag handle ──────────────────────────────────────────
    /// <summary>
    /// Show a drag handle icon on each row. When enabled, row dragging only starts
    /// when the user presses and drags the handle. This works independently of
    /// AllowRowDragDrop (which enables full-row drag from any cell).
    /// </summary>
    public bool ShowRowDragHandle { get; set; } = false;

    /// <summary>Which side of the row the drag handle appears on.</summary>
    public DragHandlePosition RowDragHandlePosition { get; set; } = DragHandlePosition.Left;

    /// <summary>Width of the drag handle column in pixels.</summary>
    public float RowDragHandleWidth { get; set; } = 32f;

    /// <summary>Color of the drag handle icon.</summary>
    public GridColor RowDragHandleColor { get; set; } = new(160, 160, 160);

    /// <summary>Background color of the drag handle column.</summary>
    public GridColor RowDragHandleBackgroundColor { get; set; } = new(248, 248, 248);

    /// <summary>Background color of the drag handle header area.</summary>
    public GridColor RowDragHandleHeaderBackgroundColor { get; set; } = new(235, 235, 235);

    // ── Conditional style resolvers ──────────────────────────────
    /// <summary>
    /// Callback invoked for each visible cell to dynamically resolve a CellStyle
    /// based on the row data item and column. Return null if no override is needed.
    /// Dynamic styles take precedence over per-column CellStyle.
    /// </summary>
    public Func<object, DataGridColumn, CellStyle?>? CellStyleResolver { get; set; }

    /// <summary>
    /// Callback invoked for each visible row to dynamically resolve a RowStyle
    /// based on the row data item. Return null if no override is needed.
    /// </summary>
    public Func<object, RowStyle?>? RowStyleResolver { get; set; }
}

/// <summary>
/// Specifies which side the drag handle appears on.
/// </summary>
public enum DragHandlePosition
{
    /// <summary>Drag handle appears on the left side of the row.</summary>
    Left,
    /// <summary>Drag handle appears on the right side of the row.</summary>
    Right
}

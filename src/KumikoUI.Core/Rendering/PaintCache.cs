using KumikoUI.Core.Models;

namespace KumikoUI.Core.Rendering;

/// <summary>
/// Frame-scoped cache of commonly used <see cref="GridPaint"/> instances.
/// Pre-computes paint objects from the current <see cref="DataGridStyle"/> at the start
/// of each frame, so renderers can reuse them instead of allocating new paints per draw call.
///
/// <para>Typical savings: eliminates ~60+ <c>new GridPaint</c> allocations per frame in the
/// renderer's fixed overhead (header, grid lines, backgrounds, dividers, etc.).</para>
/// </summary>
public class PaintCache
{
    // ── Backgrounds ──────────────────────────────────────────────
    /// <summary>Paint for the default grid background.</summary>
    public GridPaint Background { get; private set; } = new();

    /// <summary>Paint for the column header background.</summary>
    public GridPaint HeaderBackground { get; private set; } = new();

    /// <summary>Paint for left-frozen column backgrounds.</summary>
    public GridPaint FrozenColumnBackground { get; private set; } = new();

    /// <summary>Paint for right-frozen column backgrounds.</summary>
    public GridPaint RightFrozenColumnBackground { get; private set; } = new();

    /// <summary>Paint for selected row/cell backgrounds.</summary>
    public GridPaint SelectionBackground { get; private set; } = new();

    /// <summary>Paint for the focused (current) row background.</summary>
    public GridPaint FocusedRowBackground { get; private set; } = new();

    /// <summary>Paint for alternate row backgrounds.</summary>
    public GridPaint AlternateRowBackground { get; private set; } = new();

    /// <summary>Paint for frozen row backgrounds.</summary>
    public GridPaint FrozenRowBackground { get; private set; } = new();

    // ── Text ─────────────────────────────────────────────────────
    /// <summary>Paint for header text.</summary>
    public GridPaint HeaderText { get; private set; } = new();

    /// <summary>Paint for cell text.</summary>
    public GridPaint CellText { get; private set; } = new();

    // ── Grid Lines ───────────────────────────────────────────────
    /// <summary>Paint for cell grid lines.</summary>
    public GridPaint GridLine { get; private set; } = new();

    /// <summary>Paint for the header bottom border.</summary>
    public GridPaint HeaderBorder { get; private set; } = new();

    // ── Dividers ─────────────────────────────────────────────────
    /// <summary>Paint for the left-frozen column divider line.</summary>
    public GridPaint FrozenColumnDivider { get; private set; } = new();

    /// <summary>Paint for the right-frozen column divider line.</summary>
    public GridPaint RightFrozenColumnDivider { get; private set; } = new();

    /// <summary>Paint for the frozen row divider line.</summary>
    public GridPaint FrozenRowDivider { get; private set; } = new();

    // ── Selection / Focus ────────────────────────────────────────
    /// <summary>Paint for the current-cell border highlight.</summary>
    public GridPaint CurrentCellBorder { get; private set; } = new();

    /// <summary>Paint for accent stroke effects (e.g., resize handles).</summary>
    public GridPaint AccentStroke { get; private set; } = new();

    // ── Sort / Filter icons ──────────────────────────────────────
    /// <summary>Paint for the sort direction indicator arrow.</summary>
    public GridPaint SortIndicator { get; private set; } = new();

    /// <summary>Paint for inactive filter icons in column headers.</summary>
    public GridPaint FilterIcon { get; private set; } = new();

    /// <summary>Paint for active (applied) filter icons in column headers.</summary>
    public GridPaint FilterActiveIcon { get; private set; } = new();

    // ── Drag handles ─────────────────────────────────────────────
    /// <summary>Paint for the row drag-handle background.</summary>
    public GridPaint DragHandleBackground { get; private set; } = new();

    /// <summary>Paint for the row drag-handle icon.</summary>
    public GridPaint DragHandleIcon { get; private set; } = new();

    // ── Group panel ──────────────────────────────────────────────
    /// <summary>Paint for the group panel background area.</summary>
    public GridPaint GroupPanelBackground { get; private set; } = new();

    /// <summary>Paint for group panel instruction text.</summary>
    public GridPaint GroupPanelText { get; private set; } = new();

    /// <summary>Paint for group chip backgrounds.</summary>
    public GridPaint GroupChipBackground { get; private set; } = new();

    /// <summary>Paint for group chip border outlines.</summary>
    public GridPaint GroupChipBorder { get; private set; } = new();

    // ── Summary rows ─────────────────────────────────────────────
    /// <summary>Paint for summary row backgrounds.</summary>
    public GridPaint SummaryBackground { get; private set; } = new();

    /// <summary>Paint for summary row text.</summary>
    public GridPaint SummaryText { get; private set; } = new();

    /// <summary>
    /// Rebuild all cached paint objects from the current style.
    /// Call once at the start of each render frame.
    /// </summary>
    public void Update(DataGridStyle style)
    {
        // Backgrounds
        Background = new GridPaint { Color = style.BackgroundColor };
        HeaderBackground = new GridPaint { Color = style.HeaderBackgroundColor };
        FrozenColumnBackground = new GridPaint { Color = style.FrozenColumnBackgroundColor };
        RightFrozenColumnBackground = new GridPaint { Color = style.RightFrozenColumnBackgroundColor };
        SelectionBackground = new GridPaint { Color = style.SelectionColor };
        FocusedRowBackground = new GridPaint { Color = style.FocusedRowColor };
        AlternateRowBackground = new GridPaint { Color = style.AlternateRowColor };
        FrozenRowBackground = new GridPaint { Color = style.FrozenRowBackgroundColor };

        // Text
        HeaderText = new GridPaint
        {
            Color = style.HeaderTextColor,
            Font = style.HeaderFont,
            IsAntiAlias = true
        };
        CellText = new GridPaint
        {
            Color = style.CellTextColor,
            Font = style.CellFont,
            IsAntiAlias = true
        };

        // Grid lines
        GridLine = new GridPaint
        {
            Color = style.GridLineColor,
            StrokeWidth = style.GridLineWidth,
            Style = PaintStyle.Stroke,
            IsAntiAlias = false
        };
        HeaderBorder = new GridPaint
        {
            Color = style.HeaderBorderColor,
            StrokeWidth = style.GridLineWidth,
            Style = PaintStyle.Stroke
        };

        // Dividers
        FrozenColumnDivider = new GridPaint
        {
            Color = style.FrozenColumnDividerColor,
            StrokeWidth = style.FrozenColumnDividerWidth,
            Style = PaintStyle.Stroke,
            IsAntiAlias = false
        };
        RightFrozenColumnDivider = new GridPaint
        {
            Color = style.RightFrozenColumnDividerColor,
            StrokeWidth = style.RightFrozenColumnDividerWidth,
            Style = PaintStyle.Stroke,
            IsAntiAlias = false
        };
        FrozenRowDivider = new GridPaint
        {
            Color = style.FrozenRowDividerColor,
            StrokeWidth = style.FrozenRowDividerWidth,
            Style = PaintStyle.Stroke,
            IsAntiAlias = false
        };

        // Selection / Focus
        CurrentCellBorder = new GridPaint
        {
            Color = style.CurrentCellBorderColor,
            StrokeWidth = style.CurrentCellBorderWidth,
            Style = PaintStyle.Stroke,
            IsAntiAlias = true
        };
        AccentStroke = new GridPaint
        {
            Color = style.AccentColor,
            Style = PaintStyle.Stroke,
            StrokeWidth = 2f,
            IsAntiAlias = true
        };

        // Sort / Filter icons
        SortIndicator = new GridPaint
        {
            Color = style.SortIndicatorColor,
            Style = PaintStyle.Fill,
            IsAntiAlias = true
        };
        FilterIcon = new GridPaint
        {
            Color = style.FilterIconColor,
            Style = PaintStyle.Fill,
            IsAntiAlias = true
        };
        FilterActiveIcon = new GridPaint
        {
            Color = style.FilterActiveIconColor,
            Style = PaintStyle.Fill,
            IsAntiAlias = true
        };

        // Drag handles
        DragHandleBackground = new GridPaint { Color = style.RowDragHandleBackgroundColor };
        DragHandleIcon = new GridPaint
        {
            Color = style.RowDragHandleColor,
            StrokeWidth = 1.5f,
            Style = PaintStyle.Stroke,
            IsAntiAlias = true
        };

        // Group panel
        GroupPanelBackground = new GridPaint { Color = style.GroupPanelBackgroundColor };
        GroupPanelText = new GridPaint
        {
            Color = style.GroupPanelTextColor,
            Font = style.GroupPanelFont,
            IsAntiAlias = true
        };
        GroupChipBackground = new GridPaint { Color = style.GroupPanelChipBackgroundColor };
        GroupChipBorder = new GridPaint
        {
            Color = style.GroupPanelChipBorderColor,
            Style = PaintStyle.Stroke,
            StrokeWidth = 1f,
            IsAntiAlias = true
        };

        // Summary rows
        SummaryBackground = new GridPaint { Color = style.SummaryRowBackgroundColor };
        SummaryText = new GridPaint
        {
            Color = style.SummaryRowTextColor,
            Font = style.SummaryRowFont,
            IsAntiAlias = true
        };
    }

    /// <summary>
    /// Create a paint for a specific background color (for row backgrounds that vary per row).
    /// </summary>
    public static GridPaint BackgroundPaint(GridColor color) => new() { Color = color };

    /// <summary>
    /// Create a paint for cell text with optional style overrides.
    /// </summary>
    public GridPaint CellTextPaint(CellStyle? cellStyle, DataGridStyle style)
    {
        if (cellStyle == null || (cellStyle.TextColor == null && cellStyle.Font == null))
            return CellText;

        return new GridPaint
        {
            Color = cellStyle.TextColor ?? style.CellTextColor,
            Font = cellStyle.Font ?? style.CellFont,
            IsAntiAlias = true
        };
    }
}

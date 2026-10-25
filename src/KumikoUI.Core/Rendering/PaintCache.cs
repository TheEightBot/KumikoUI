using KumikoUI.Core.Models;

namespace KumikoUI.Core.Rendering;

/// <summary>
/// Frame-scoped cache of commonly used <see cref="KumikoPaint"/> instances.
/// Pre-computes paint objects from the current <see cref="KumikoStyle"/> at the start
/// of each frame, so renderers can reuse them instead of allocating new paints per draw call.
///
/// <para>Typical savings: eliminates ~60+ <c>new KumikoPaint</c> allocations per frame in the
/// renderer's fixed overhead (header, grid lines, backgrounds, dividers, etc.).</para>
/// </summary>
public class PaintCache
{
    // ── Backgrounds ──────────────────────────────────────────────
    /// <summary>Paint for the default grid background.</summary>
    public KumikoPaint Background { get; private set; } = new();

    /// <summary>Paint for the column header background.</summary>
    public KumikoPaint HeaderBackground { get; private set; } = new();

    /// <summary>Paint for left-frozen column backgrounds.</summary>
    public KumikoPaint FrozenColumnBackground { get; private set; } = new();

    /// <summary>Paint for right-frozen column backgrounds.</summary>
    public KumikoPaint RightFrozenColumnBackground { get; private set; } = new();

    /// <summary>Paint for selected row/cell backgrounds.</summary>
    public KumikoPaint SelectionBackground { get; private set; } = new();

    /// <summary>Paint for the focused (current) row background.</summary>
    public KumikoPaint FocusedRowBackground { get; private set; } = new();

    /// <summary>Paint for alternate row backgrounds.</summary>
    public KumikoPaint AlternateRowBackground { get; private set; } = new();

    /// <summary>Paint for frozen row backgrounds.</summary>
    public KumikoPaint FrozenRowBackground { get; private set; } = new();

    // ── Text ─────────────────────────────────────────────────────
    /// <summary>Paint for header text.</summary>
    public KumikoPaint HeaderText { get; private set; } = new();

    /// <summary>Paint for cell text.</summary>
    public KumikoPaint CellText { get; private set; } = new();

    // ── Grid Lines ───────────────────────────────────────────────
    /// <summary>Paint for cell grid lines.</summary>
    public KumikoPaint GridLine { get; private set; } = new();

    /// <summary>Paint for the header bottom border.</summary>
    public KumikoPaint HeaderBorder { get; private set; } = new();

    // ── Dividers ─────────────────────────────────────────────────
    /// <summary>Paint for the left-frozen column divider line.</summary>
    public KumikoPaint FrozenColumnDivider { get; private set; } = new();

    /// <summary>Paint for the right-frozen column divider line.</summary>
    public KumikoPaint RightFrozenColumnDivider { get; private set; } = new();

    /// <summary>Paint for the frozen row divider line.</summary>
    public KumikoPaint FrozenRowDivider { get; private set; } = new();

    // ── Selection / Focus ────────────────────────────────────────
    /// <summary>Paint for the current-cell border highlight.</summary>
    public KumikoPaint CurrentCellBorder { get; private set; } = new();

    /// <summary>Paint for accent stroke effects (e.g., resize handles).</summary>
    public KumikoPaint AccentStroke { get; private set; } = new();

    // ── Sort / Filter icons ──────────────────────────────────────
    /// <summary>Paint for the sort direction indicator arrow.</summary>
    public KumikoPaint SortIndicator { get; private set; } = new();

    /// <summary>Paint for inactive filter icons in column headers.</summary>
    public KumikoPaint FilterIcon { get; private set; } = new();

    /// <summary>Paint for active (applied) filter icons in column headers.</summary>
    public KumikoPaint FilterActiveIcon { get; private set; } = new();

    // ── Drag handles ─────────────────────────────────────────────
    /// <summary>Paint for the row drag-handle background.</summary>
    public KumikoPaint DragHandleBackground { get; private set; } = new();

    /// <summary>Paint for the row drag-handle icon.</summary>
    public KumikoPaint DragHandleIcon { get; private set; } = new();

    // ── Group panel ──────────────────────────────────────────────
    /// <summary>Paint for the group panel background area.</summary>
    public KumikoPaint GroupPanelBackground { get; private set; } = new();

    /// <summary>Paint for group panel instruction text.</summary>
    public KumikoPaint GroupPanelText { get; private set; } = new();

    /// <summary>Paint for group chip backgrounds.</summary>
    public KumikoPaint GroupChipBackground { get; private set; } = new();

    /// <summary>Paint for group chip border outlines.</summary>
    public KumikoPaint GroupChipBorder { get; private set; } = new();

    // ── Summary rows ─────────────────────────────────────────────
    /// <summary>Paint for summary row backgrounds.</summary>
    public KumikoPaint SummaryBackground { get; private set; } = new();

    /// <summary>Paint for summary row text.</summary>
    public KumikoPaint SummaryText { get; private set; } = new();

    /// <summary>
    /// Rebuild all cached paint objects from the current style.
    /// Call once at the start of each render frame.
    /// </summary>
    public void Update(KumikoStyle style)
    {
        // Backgrounds
        Background = new KumikoPaint { Color = style.BackgroundColor };
        HeaderBackground = new KumikoPaint { Color = style.HeaderBackgroundColor };
        FrozenColumnBackground = new KumikoPaint { Color = style.FrozenColumnBackgroundColor };
        RightFrozenColumnBackground = new KumikoPaint { Color = style.RightFrozenColumnBackgroundColor };
        SelectionBackground = new KumikoPaint { Color = style.SelectionColor };
        FocusedRowBackground = new KumikoPaint { Color = style.FocusedRowColor };
        AlternateRowBackground = new KumikoPaint { Color = style.AlternateRowColor };
        FrozenRowBackground = new KumikoPaint { Color = style.FrozenRowBackgroundColor };

        // Text
        HeaderText = new KumikoPaint
        {
            Color = style.HeaderTextColor,
            Font = style.HeaderFont,
            IsAntiAlias = true
        };
        CellText = new KumikoPaint
        {
            Color = style.CellTextColor,
            Font = style.CellFont,
            IsAntiAlias = true
        };

        // Grid lines
        GridLine = new KumikoPaint
        {
            Color = style.GridLineColor,
            StrokeWidth = style.GridLineWidth,
            Style = PaintStyle.Stroke,
            IsAntiAlias = false
        };
        HeaderBorder = new KumikoPaint
        {
            Color = style.HeaderBorderColor,
            StrokeWidth = style.GridLineWidth,
            Style = PaintStyle.Stroke
        };

        // Dividers
        FrozenColumnDivider = new KumikoPaint
        {
            Color = style.FrozenColumnDividerColor,
            StrokeWidth = style.FrozenColumnDividerWidth,
            Style = PaintStyle.Stroke,
            IsAntiAlias = false
        };
        RightFrozenColumnDivider = new KumikoPaint
        {
            Color = style.RightFrozenColumnDividerColor,
            StrokeWidth = style.RightFrozenColumnDividerWidth,
            Style = PaintStyle.Stroke,
            IsAntiAlias = false
        };
        FrozenRowDivider = new KumikoPaint
        {
            Color = style.FrozenRowDividerColor,
            StrokeWidth = style.FrozenRowDividerWidth,
            Style = PaintStyle.Stroke,
            IsAntiAlias = false
        };

        // Selection / Focus
        CurrentCellBorder = new KumikoPaint
        {
            Color = style.CurrentCellBorderColor,
            StrokeWidth = style.CurrentCellBorderWidth,
            Style = PaintStyle.Stroke,
            IsAntiAlias = true
        };
        AccentStroke = new KumikoPaint
        {
            Color = style.AccentColor,
            Style = PaintStyle.Stroke,
            StrokeWidth = 2f,
            IsAntiAlias = true
        };

        // Sort / Filter icons
        SortIndicator = new KumikoPaint
        {
            Color = style.SortIndicatorColor,
            Style = PaintStyle.Fill,
            IsAntiAlias = true
        };
        FilterIcon = new KumikoPaint
        {
            Color = style.FilterIconColor,
            Style = PaintStyle.Fill,
            IsAntiAlias = true
        };
        FilterActiveIcon = new KumikoPaint
        {
            Color = style.FilterActiveIconColor,
            Style = PaintStyle.Fill,
            IsAntiAlias = true
        };

        // Drag handles
        DragHandleBackground = new KumikoPaint { Color = style.RowDragHandleBackgroundColor };
        DragHandleIcon = new KumikoPaint
        {
            Color = style.RowDragHandleColor,
            StrokeWidth = 1.5f,
            Style = PaintStyle.Stroke,
            IsAntiAlias = true
        };

        // Group panel
        GroupPanelBackground = new KumikoPaint { Color = style.GroupPanelBackgroundColor };
        GroupPanelText = new KumikoPaint
        {
            Color = style.GroupPanelTextColor,
            Font = style.GroupPanelFont,
            IsAntiAlias = true
        };
        GroupChipBackground = new KumikoPaint { Color = style.GroupPanelChipBackgroundColor };
        GroupChipBorder = new KumikoPaint
        {
            Color = style.GroupPanelChipBorderColor,
            Style = PaintStyle.Stroke,
            StrokeWidth = 1f,
            IsAntiAlias = true
        };

        // Summary rows
        SummaryBackground = new KumikoPaint { Color = style.SummaryRowBackgroundColor };
        SummaryText = new KumikoPaint
        {
            Color = style.SummaryRowTextColor,
            Font = style.SummaryRowFont,
            IsAntiAlias = true
        };
    }

    /// <summary>
    /// Create a paint for a specific background color (for row backgrounds that vary per row).
    /// </summary>
    public static KumikoPaint BackgroundPaint(KumikoColor color) => new() { Color = color };

    /// <summary>
    /// Create a paint for cell text with optional style overrides.
    /// </summary>
    public KumikoPaint CellTextPaint(KumikoCellStyle? cellStyle, KumikoStyle style)
    {
        if (cellStyle == null || (cellStyle.TextColor == null && cellStyle.Font == null))
            return CellText;

        return new KumikoPaint
        {
            Color = cellStyle.TextColor ?? style.CellTextColor,
            Font = cellStyle.Font ?? style.CellFont,
            IsAntiAlias = true
        };
    }
}

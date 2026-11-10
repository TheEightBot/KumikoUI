using KumikoUI.Core.Models;

namespace KumikoUI.Core.Rendering;

/// <summary>
/// Cache of the <see cref="KumikoPaint"/> instances the renderer draws with, built from a
/// <see cref="KumikoStyle"/>. <see cref="Update"/> rebuilds the paints only when it is given a different style
/// instance or the style's <see cref="KumikoStyle.Version"/> changed, so steady-state frames allocate no paints.
/// Cell renderers receive the renderer's cache through <see cref="CellRenderContext.Paints"/>.
///
/// <para>The paints are shared: draw with them, but do not modify them. A rebuild creates new instances, so a
/// paint obtained earlier keeps the values it had.</para>
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

    // ── Cell content ─────────────────────────────────────────────
    /// <summary>Paint for the border of a Boolean cell's checkbox.</summary>
    public KumikoPaint CheckBoxBorder { get; private set; } = new();

    /// <summary>Paint for the checkmark of a checked Boolean cell.</summary>
    public KumikoPaint CheckMark { get; private set; } = new();

    // ── Grid chrome (drawn by KumikoRenderer only) ───────────────
    internal KumikoPaint SortOrderBadge { get; private set; } = new();
    internal KumikoPaint GroupPanelLabel { get; private set; } = new();
    internal KumikoPaint GroupChipText { get; private set; } = new();
    internal KumikoPaint GroupChipAccent { get; private set; } = new();
    internal KumikoPaint GroupChipArrow { get; private set; } = new();
    internal KumikoPaint GroupChipRemoveBackground { get; private set; } = new();
    internal KumikoPaint GroupChipRemove { get; private set; } = new();
    internal KumikoPaint GroupChipSeparator { get; private set; } = new();
    internal KumikoPaint GroupHeaderBackground { get; private set; } = new();
    internal KumikoPaint GroupChevronBackground { get; private set; } = new();
    internal KumikoPaint GroupChevron { get; private set; } = new();
    internal KumikoPaint GroupHeaderText { get; private set; } = new();
    internal KumikoPaint SummaryBorder { get; private set; } = new();
    internal KumikoPaint SummaryLabel { get; private set; } = new();
    internal KumikoPaint GroupSummaryBackground { get; private set; } = new();
    internal KumikoPaint GroupSummaryText { get; private set; } = new();
    internal KumikoPaint DragHandleHeaderBackground { get; private set; } = new();
    internal KumikoPaint DragHandleDivider { get; private set; } = new();
    internal KumikoPaint ColumnDragGhost { get; private set; } = new();
    internal KumikoPaint ColumnDragBorder { get; private set; } = new();
    internal KumikoPaint RowDragGhost { get; private set; } = new();
    internal KumikoPaint RowDragBorder { get; private set; } = new();
    internal KumikoPaint RowDragText { get; private set; } = new();
    internal KumikoPaint RowDropIndicator { get; private set; } = new();

    // The style the paints were built from, and its version then; a frame with the same pair rebuilds nothing.
    private KumikoStyle? _builtStyle;
    private int _builtVersion;

    // The header font the sort-order badge font was derived from, with its family and size then: a font is
    // mutable, and changing one does not change the style's version.
    private KumikoFont? _badgeSourceFont;
    private string? _badgeSourceFamily;
    private float _badgeSourceSize;

    // Reused by CellTextPaint for cells whose style overrides the text color or font.
    private readonly KumikoPaint _cellTextOverride = new() { IsAntiAlias = true };

    /// <summary>
    /// Bring the paints up to date with <paramref name="style"/>. Call at the start of each frame: the paints are
    /// rebuilt only when <paramref name="style"/> is a different instance than last time or its
    /// <see cref="KumikoStyle.Version"/> changed; otherwise nothing is allocated.
    /// </summary>
    public void Update(KumikoStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        if (!ReferenceEquals(style, _builtStyle) || style.Version != _builtVersion)
        {
            Rebuild(style);
            _builtStyle = style;
            _builtVersion = style.Version;
        }

        UpdateSortOrderBadgeFont(style.HeaderFont);
    }

    /// <summary>Create every cached paint from <paramref name="style"/>.</summary>
    private void Rebuild(KumikoStyle style)
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
        SummaryLabel = new KumikoPaint
        {
            Color = style.SummaryRowLabelColor,
            Font = style.SummaryRowFont,
            IsAntiAlias = true
        };
        SummaryBorder = Stroke(style.SummaryRowBorderColor, 1f, antiAlias: false);
        GroupSummaryBackground = new KumikoPaint { Color = style.GroupSummaryRowBackgroundColor };
        GroupSummaryText = new KumikoPaint
        {
            Color = style.GroupSummaryRowTextColor,
            Font = style.GroupSummaryRowFont,
            IsAntiAlias = true
        };

        // Cell content
        CheckBoxBorder = Stroke(style.GridLineColor, 1.5f);
        CheckMark = Stroke(style.AccentColor, 2f);

        // Multi-sort order badge; its font is derived from the header font in UpdateSortOrderBadgeFont
        SortOrderBadge = new KumikoPaint { Color = style.SortIndicatorColor, IsAntiAlias = true };
        _badgeSourceFont = null;

        // Group panel chips
        GroupPanelLabel = new KumikoPaint
        {
            Color = style.GroupPanelLabelColor,
            Font = style.GroupPanelLabelFont,
            IsAntiAlias = true
        };
        GroupChipText = new KumikoPaint
        {
            Color = style.GroupPanelChipTextColor,
            Font = style.GroupPanelFont,
            IsAntiAlias = true
        };
        GroupChipAccent = new KumikoPaint { Color = style.GroupPanelChipAccentColor };
        GroupChipArrow = Stroke(style.GroupPanelChipAccentColor, 2f);
        GroupChipRemoveBackground = new KumikoPaint { Color = style.GroupPanelChipRemoveColor.WithAlpha(40) };
        GroupChipRemove = Stroke(style.GroupPanelChipRemoveColor, 1.5f);
        GroupChipSeparator = Stroke(style.GroupPanelLabelColor, 1.5f);

        // Group header rows
        GroupHeaderBackground = new KumikoPaint { Color = style.GroupHeaderBackgroundColor };
        GroupChevronBackground = new KumikoPaint { Color = style.GroupChevronBackgroundColor, IsAntiAlias = true };
        GroupChevron = Stroke(style.GroupChevronColor, 2.5f);
        GroupHeaderText = new KumikoPaint
        {
            Color = style.GroupHeaderTextColor,
            Font = style.GroupHeaderFont,
            IsAntiAlias = true
        };

        // Drag handle column and drag overlays
        DragHandleHeaderBackground = new KumikoPaint { Color = style.RowDragHandleHeaderBackgroundColor };
        DragHandleDivider = new KumikoPaint
        {
            Color = style.GridLineColor,
            StrokeWidth = 1f,
            Style = PaintStyle.Stroke
        };
        ColumnDragGhost = new KumikoPaint { Color = style.HeaderBackgroundColor.WithAlpha(180) };
        ColumnDragBorder = new KumikoPaint
        {
            Color = style.CurrentCellBorderColor,
            Style = PaintStyle.Stroke,
            StrokeWidth = 2f
        };
        RowDragGhost = new KumikoPaint { Color = style.RowDragOverlayColor };
        RowDragBorder = new KumikoPaint
        {
            Color = style.RowDragBorderColor,
            Style = PaintStyle.Stroke,
            StrokeWidth = 2f
        };
        RowDragText = new KumikoPaint
        {
            Color = style.CellTextColor.WithAlpha(200),
            Font = style.CellFont,
            IsAntiAlias = true
        };
        RowDropIndicator = new KumikoPaint
        {
            Color = style.RowDropIndicatorColor,
            StrokeWidth = style.RowDropIndicatorWidth,
            Style = PaintStyle.Stroke
        };
    }

    private static KumikoPaint Stroke(KumikoColor color, float width, bool antiAlias = true) => new()
    {
        Color = color,
        Style = PaintStyle.Stroke,
        StrokeWidth = width,
        IsAntiAlias = antiAlias
    };

    /// <summary>
    /// Derive the multi-sort order badge font (65% of the header font) when the header font, its family or its
    /// size changed since it was last derived.
    /// </summary>
    private void UpdateSortOrderBadgeFont(KumikoFont headerFont)
    {
        if (ReferenceEquals(headerFont, _badgeSourceFont)
            && ReferenceEquals(headerFont.Family, _badgeSourceFamily)
            && headerFont.Size.Equals(_badgeSourceSize)) // exact: cache change detection, not a numeric tolerance
            return;

        _badgeSourceFont = headerFont;
        _badgeSourceFamily = headerFont.Family;
        _badgeSourceSize = headerFont.Size;
        SortOrderBadge.Font = new KumikoFont(headerFont.Family, headerFont.Size * 0.65f);
    }

    /// <summary>
    /// Create a paint for a specific background color (for row backgrounds that vary per row).
    /// </summary>
    public static KumikoPaint BackgroundPaint(KumikoColor color) => new() { Color = color };

    /// <summary>
    /// The paint for cell text under a cell's style overrides: the shared <see cref="CellText"/> when
    /// <paramref name="cellStyle"/> overrides neither the text color nor the font, otherwise one paint reused for
    /// every overriding call, set from <paramref name="cellStyle"/> over <paramref name="style"/>. Draw with the
    /// result before the next call; do not keep or modify it.
    /// </summary>
    public KumikoPaint CellTextPaint(KumikoCellStyle? cellStyle, KumikoStyle style)
    {
        if (cellStyle == null || (cellStyle.TextColor == null && cellStyle.Font == null))
            return CellText;

        var paint = _cellTextOverride;
        paint.Color = cellStyle.TextColor ?? style.CellTextColor;
        paint.Font = cellStyle.Font ?? style.CellFont;
        return paint;
    }
}

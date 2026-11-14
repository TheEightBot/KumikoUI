using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Models;

/// <summary>
/// Theme / style settings for the grid renderer.
/// </summary>
public class KumikoStyle
{
    /// <summary>
    /// A counter incremented whenever a property of this style is set to a different value. The renderer
    /// rebuilds its cached paints only when the style instance or its version changes, so a frame drawn with an
    /// unchanged style allocates no paints. Changing a property of a <see cref="KumikoFont"/> the style refers
    /// to does not change the version (and needs none: cached paints refer to the same font instance).
    /// </summary>
    public int Version { get; private set; }

    private void Set<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        Version++;
    }

    // ── Sizing ───────────────────────────────────────────────────
    /// <summary>Height of the header row in pixels. Default: 40.</summary>
    public float HeaderHeight { get; set => Set(ref field, value); } = 40f;
    /// <summary>Height of each data row in pixels. Default: 36.</summary>
    public float RowHeight { get; set => Set(ref field, value); } = 36f;
    /// <summary>Horizontal padding inside data cells in pixels. Default: 8.</summary>
    public float CellPadding { get; set => Set(ref field, value); } = 8f;

    // ── Colors ───────────────────────────────────────────────────
    /// <summary>Background color of the grid canvas.</summary>
    public KumikoColor BackgroundColor { get; set => Set(ref field, value); } = KumikoColor.White;
    /// <summary>Background color of the header row.</summary>
    public KumikoColor HeaderBackgroundColor { get; set => Set(ref field, value); } = new(230, 230, 230);
    /// <summary>Text color for header cells.</summary>
    public KumikoColor HeaderTextColor { get; set => Set(ref field, value); } = KumikoColor.Black;
    /// <summary>Default text color for data cells.</summary>
    public KumikoColor CellTextColor { get; set => Set(ref field, value); } = new(30, 30, 30);
    /// <summary>Color of horizontal and vertical grid lines.</summary>
    public KumikoColor GridLineColor { get; set => Set(ref field, value); } = new(220, 220, 220);
    /// <summary>
    /// Color of the outline of a Boolean cell's check box. <c>null</c> (the default) draws it in
    /// <see cref="GridLineColor"/>; set it when the grid lines are too faint to outline an unchecked box.
    /// </summary>
    public KumikoColor? CheckBoxBorderColor { get; set => Set(ref field, value); }
    /// <summary>Fill color of selected row/cell highlights.</summary>
    public KumikoColor SelectionColor { get; set => Set(ref field, value); } = new(0, 120, 215, 60);
    /// <summary>
    /// Text color of cells in selected rows, drawn over <see cref="SelectionColor"/>. A text color set by
    /// <see cref="CellStyleResolver"/>, the column's <see cref="KumikoColumn.CellStyle"/> or
    /// <see cref="RowStyleResolver"/> takes precedence, so a cell that sets its own color keeps it when
    /// selected. Custom cell renderers read it through <see cref="Rendering.CellRenderContext.TextPaint"/>.
    /// Default: dark text (30, 30, 30), readable on the light preset's translucent selection; the dark and
    /// high-contrast presets use white.
    /// </summary>
    public KumikoColor SelectionTextColor { get; set => Set(ref field, value); } = new(30, 30, 30);
    /// <summary>Subtle highlight color for the focused/current row.</summary>
    public KumikoColor FocusedRowColor { get; set => Set(ref field, value); } = new(0, 120, 215, 30);
    /// <summary>Background color of alternate (even) rows.</summary>
    public KumikoColor AlternateRowColor { get; set => Set(ref field, value); } = new(245, 245, 245);
    /// <summary>Color of the sort direction indicator arrow in the header.</summary>
    public KumikoColor SortIndicatorColor { get; set => Set(ref field, value); } = new(100, 100, 100);
    /// <summary>Border color of the current (focused) cell.</summary>
    public KumikoColor CurrentCellBorderColor { get; set => Set(ref field, value); } = new(0, 120, 215);
    /// <summary>Border width of the current cell indicator in pixels.</summary>
    public float CurrentCellBorderWidth { get; set => Set(ref field, value); } = 2f;

    /// <summary>General accent color used for interactive elements (checkmarks, etc.).</summary>
    public KumikoColor AccentColor { get; set => Set(ref field, value); } = new(0, 120, 215);

    // ── Fonts ────────────────────────────────────────────────────
    /// <summary>Font used for header cell text.</summary>
    public KumikoFont HeaderFont { get; set => Set(ref field, value); } = new("Default", 14, bold: true);
    /// <summary>Font used for data cell text.</summary>
    public KumikoFont CellFont { get; set => Set(ref field, value); } = new("Default", 13);

    // ── Header ───────────────────────────────────────────────────
    /// <summary>Border color drawn below the header row.</summary>
    public KumikoColor HeaderBorderColor { get; set => Set(ref field, value); } = new(200, 200, 200);
    /// <summary>Padding inside header cells (overrides CellPadding for headers).</summary>
    public float HeaderPadding { get; set => Set(ref field, value); } = 8f;

    // ── Grid lines ───────────────────────────────────────────────
    /// <summary>Width of grid lines in pixels. Default: 1.</summary>
    public float GridLineWidth { get; set => Set(ref field, value); } = 1f;
    /// <summary>Whether to draw horizontal grid lines between rows.</summary>
    public bool ShowHorizontalGridLines { get; set => Set(ref field, value); } = true;
    /// <summary>Whether to draw vertical grid lines between columns.</summary>
    public bool ShowVerticalGridLines { get; set => Set(ref field, value); } = true;

    // ── Alternate rows ───────────────────────────────────────────
    /// <summary>Whether to use alternating row background colors.</summary>
    public bool AlternateRowBackground { get; set => Set(ref field, value); } = true;

    // ── Frozen columns ───────────────────────────────────────────
    /// <summary>Divider line color between frozen and scrollable columns.</summary>
    public KumikoColor FrozenColumnDividerColor { get; set => Set(ref field, value); } = new(180, 180, 180);
    /// <summary>Divider line width between frozen and scrollable columns.</summary>
    public float FrozenColumnDividerWidth { get; set => Set(ref field, value); } = 2f;
    /// <summary>Background color for left-frozen column cells.</summary>
    public KumikoColor FrozenColumnBackgroundColor { get; set => Set(ref field, value); } = new(250, 250, 250);
    /// <summary>
    /// The largest share of the column area (the viewport less the row drag handle) the left-frozen and
    /// right-frozen columns may take together, from 0 to 1. Default: two thirds, so at least a third of the width
    /// is left to the scrolling columns. When the columns are wider than the viewport and the frozen ones take
    /// more than this, columns are unpinned until they fit: the right-frozen columns first, from the one nearest
    /// the scrolling columns outward, then the left-frozen ones from the rightmost. An unpinned column scrolls
    /// with the others at the same place in the on-screen order (at the start of the scrolling columns when it
    /// was left-frozen, at their end when it was right-frozen), and is pinned again as soon as the viewport is
    /// wide enough. Columns keep their <see cref="KumikoColumn.FreezeMode"/>. When every column fits in the
    /// viewport nothing is unpinned, whatever the ratio. 1 unpins only what would otherwise overlap; 0 unpins
    /// every frozen column whenever the grid scrolls horizontally. Values outside 0 to 1 are clamped.
    /// </summary>
    public float MaxFrozenColumnsWidthRatio { get; set => Set(ref field, value); } = 2f / 3f;

    // ── Right-frozen columns ─────────────────────────────────────
    /// <summary>Divider line color between scrollable and right-frozen columns.</summary>
    public KumikoColor RightFrozenColumnDividerColor { get; set => Set(ref field, value); } = new(180, 180, 180);
    /// <summary>Divider line width between scrollable and right-frozen columns.</summary>
    public float RightFrozenColumnDividerWidth { get; set => Set(ref field, value); } = 2f;
    /// <summary>Background color for right-frozen column cells.</summary>
    public KumikoColor RightFrozenColumnBackgroundColor { get; set => Set(ref field, value); } = new(250, 250, 250);

    // ── Frozen rows ──────────────────────────────────────────────
    /// <summary>Divider line color below frozen rows.</summary>
    public KumikoColor FrozenRowDividerColor { get; set => Set(ref field, value); } = new(180, 180, 180);
    /// <summary>Divider line width below frozen rows.</summary>
    public float FrozenRowDividerWidth { get; set => Set(ref field, value); } = 2f;
    /// <summary>Background color for frozen row cells.</summary>
    public KumikoColor FrozenRowBackgroundColor { get; set => Set(ref field, value); } = new(250, 250, 250);

    // ── Filter ───────────────────────────────────────────────
    /// <summary>Color of the filter icon in column headers.</summary>
    public KumikoColor FilterIconColor { get; set => Set(ref field, value); } = new(140, 140, 140);
    /// <summary>Color of the filter icon when a filter is active on that column.</summary>
    public KumikoColor FilterActiveIconColor { get; set => Set(ref field, value); } = new(0, 120, 215);

    // ── Grouping ─────────────────────────────────────────────
    /// <summary>Background color of group header rows.</summary>
    public KumikoColor GroupHeaderBackgroundColor { get; set => Set(ref field, value); } = new(240, 240, 240);
    /// <summary>Text color for group header labels.</summary>
    public KumikoColor GroupHeaderTextColor { get; set => Set(ref field, value); } = new(40, 40, 40);
    /// <summary>Color of the item count text displayed in group headers.</summary>
    public KumikoColor GroupHeaderCountColor { get; set => Set(ref field, value); } = new(120, 120, 120);
    /// <summary>Color of the expand/collapse chevron icon in group headers.</summary>
    public KumikoColor GroupChevronColor { get; set => Set(ref field, value); } = new(80, 80, 80);
    /// <summary>Background color behind the group expand/collapse chevron icon.</summary>
    public KumikoColor GroupChevronBackgroundColor { get; set => Set(ref field, value); } = new(220, 225, 235);
    /// <summary>Font for the main text in group header rows.</summary>
    public KumikoFont GroupHeaderFont { get; set => Set(ref field, value); } = new("Default", 13, bold: true);
    /// <summary>Font for the item count portion of group headers.</summary>
    public KumikoFont GroupCountFont { get; set => Set(ref field, value); } = new("Default", 12);
    /// <summary>Width of each grouping indentation level in pixels.</summary>
    public float GroupIndentWidth { get; set => Set(ref field, value); } = 24f;
    /// <summary>Size of the expand/collapse chevron icon in pixels.</summary>
    public float GroupChevronSize { get; set => Set(ref field, value); } = 14f;
    /// <summary>Height of the group-by panel at the top of the grid.</summary>
    public float GroupPanelHeight { get; set => Set(ref field, value); } = 40f;
    /// <summary>
    /// When true (default), the header of the group the top rows belong to stays pinned at the top of the data area
    /// while its rows scroll beneath it; with nested grouping, each level's header stacks below the one outside it.
    /// The next group's header pushes a pinned header up as it arrives. A pinned header works like the real one:
    /// tapping it (or its chevron) expands or collapses that group, and keyboard navigation keeps the current row
    /// below the pinned headers. Collapsing a pinned group scrolls so its header stays where it was pinned.
    /// When false, group headers scroll away with their rows.
    /// </summary>
    public bool StickyGroupHeaders { get; set => Set(ref field, value); } = true;
    /// <summary>Background color of the group-by panel.</summary>
    public KumikoColor GroupPanelBackgroundColor { get; set => Set(ref field, value); } = new(248, 249, 252);
    /// <summary>Text color used in the group-by panel.</summary>
    public KumikoColor GroupPanelTextColor { get; set => Set(ref field, value); } = new(100, 105, 115);
    /// <summary>Color of the "Grouped by:" label in the group-by panel, and of the separators between its chips.</summary>
    public KumikoColor GroupPanelLabelColor { get; set => Set(ref field, value); } = new(130, 135, 145);
    /// <summary>Font for label text in the group-by panel.</summary>
    public KumikoFont GroupPanelLabelFont { get; set => Set(ref field, value); } = new("Default", 11);
    /// <summary>Background color of group-by chips in the panel.</summary>
    public KumikoColor GroupPanelChipBackgroundColor { get; set => Set(ref field, value); } = new(230, 235, 245);
    /// <summary>Border color of group-by chips in the panel.</summary>
    public KumikoColor GroupPanelChipBorderColor { get; set => Set(ref field, value); } = new(180, 190, 210);
    /// <summary>Accent/indicator color on group-by chips.</summary>
    public KumikoColor GroupPanelChipAccentColor { get; set => Set(ref field, value); } = new(60, 120, 215);
    /// <summary>Text color of group-by chips.</summary>
    public KumikoColor GroupPanelChipTextColor { get; set => Set(ref field, value); } = new(40, 45, 55);
    /// <summary>Color of the remove (×) button on group-by chips.</summary>
    public KumikoColor GroupPanelChipRemoveColor { get; set => Set(ref field, value); } = new(140, 145, 155);
    /// <summary>Background color of an action button whose command cannot execute (<see cref="System.Windows.Input.ICommand.CanExecute"/> is false).</summary>
    public KumikoColor ActionButtonDisabledBackgroundColor { get; set => Set(ref field, value); } = new(222, 226, 230);
    /// <summary>Label color of an action button whose command cannot execute.</summary>
    public KumikoColor ActionButtonDisabledTextColor { get; set => Set(ref field, value); } = new(134, 142, 150);
    /// <summary>Font for group-by chip labels in the panel.</summary>
    public KumikoFont GroupPanelFont { get; set => Set(ref field, value); } = new("Default", 12, bold: true);

    // ── Summaries ────────────────────────────────────────────
    /// <summary>Background color of the table summary row.</summary>
    public KumikoColor SummaryRowBackgroundColor { get; set => Set(ref field, value); } = new(235, 240, 248);
    /// <summary>Text color for table summary values.</summary>
    public KumikoColor SummaryRowTextColor { get; set => Set(ref field, value); } = new(30, 30, 30);
    /// <summary>Font for table summary values.</summary>
    public KumikoFont SummaryRowFont { get; set => Set(ref field, value); } = new("Default", 13, bold: true);
    /// <summary>Text color for table summary labels (e.g., "Sum:", "Avg:").</summary>
    public KumikoColor SummaryRowLabelColor { get; set => Set(ref field, value); } = new(100, 100, 100);
    /// <summary>Font for table summary labels.</summary>
    public KumikoFont SummaryRowLabelFont { get; set => Set(ref field, value); } = new("Default", 12);
    /// <summary>Height of table summary rows in pixels.</summary>
    public float SummaryRowHeight { get; set => Set(ref field, value); } = 36f;
    /// <summary>Border color of the table summary row.</summary>
    public KumikoColor SummaryRowBorderColor { get; set => Set(ref field, value); } = new(200, 200, 200);

    /// <summary>
    /// When true (default), Position=Bottom table summary rows are pinned to the bottom edge of the
    /// viewport (a fixed footer that stays visible while the data scrolls). Taps on the footer do not
    /// reach the rows behind it, and keyboard navigation keeps the current row above it.
    /// When false, they are drawn at the end of the scrolling content (a totals row after the last data row).
    /// </summary>
    public bool PinBottomSummaryRows { get; set => Set(ref field, value); } = true;

    /// <summary>
    /// Height of the pinned bottom summary footer for the given number of bottom summary rows,
    /// or 0 when <see cref="PinBottomSummaryRows"/> is false.
    /// </summary>
    internal float GetPinnedBottomSummaryHeight(int bottomSummaryCount)
        => PinBottomSummaryRows && bottomSummaryCount > 0 ? bottomSummaryCount * SummaryRowHeight : 0f;

    // Group summary rows (after each group)
    /// <summary>Background color of group summary rows displayed after each group.</summary>
    public KumikoColor GroupSummaryRowBackgroundColor { get; set => Set(ref field, value); } = new(242, 245, 250);
    /// <summary>Text color for group summary values.</summary>
    public KumikoColor GroupSummaryRowTextColor { get; set => Set(ref field, value); } = new(50, 50, 50);
    /// <summary>Font for group summary row values.</summary>
    public KumikoFont GroupSummaryRowFont { get; set => Set(ref field, value); } = new("Default", 12, bold: true);

    // Caption summary (inline in group header)
    /// <summary>Text color for caption summaries displayed inline within group headers.</summary>
    public KumikoColor CaptionSummaryTextColor { get; set => Set(ref field, value); } = new(100, 100, 100);
    /// <summary>Font for caption summary text in group headers.</summary>
    public KumikoFont CaptionSummaryFont { get; set => Set(ref field, value); } = new("Default", 11);

    // ── Row drag & drop ──────────────────────────────────────────
    /// <summary>Enable row drag &amp; drop reordering. Default is false.</summary>
    public bool AllowRowDragDrop { get; set => Set(ref field, value); } = false;
    /// <summary>Semi-transparent overlay color applied to the row being dragged.</summary>
    public KumikoColor RowDragOverlayColor { get; set => Set(ref field, value); } = new(255, 255, 255, 200);
    /// <summary>Border color around the row being dragged.</summary>
    public KumikoColor RowDragBorderColor { get; set => Set(ref field, value); } = new(0, 120, 215);
    /// <summary>Color of the horizontal line indicating the drop target position.</summary>
    public KumikoColor RowDropIndicatorColor { get; set => Set(ref field, value); } = new(0, 120, 215);
    /// <summary>Width of the drop indicator line in pixels.</summary>
    public float RowDropIndicatorWidth { get; set => Set(ref field, value); } = 2f;

    // ── Row drag handle ──────────────────────────────────────────
    /// <summary>
    /// Show a drag handle icon on each row. When enabled, row dragging only starts
    /// when the user presses and drags the handle. This works independently of
    /// AllowRowDragDrop (which enables full-row drag from any cell).
    /// </summary>
    public bool ShowRowDragHandle { get; set => Set(ref field, value); } = false;

    /// <summary>Which side of the row the drag handle appears on.</summary>
    public DragHandlePosition RowDragHandlePosition { get; set => Set(ref field, value); } = DragHandlePosition.Left;

    /// <summary>Width of the drag handle column in pixels.</summary>
    public float RowDragHandleWidth { get; set => Set(ref field, value); } = 32f;

    /// <summary>Color of the drag handle icon.</summary>
    public KumikoColor RowDragHandleColor { get; set => Set(ref field, value); } = new(160, 160, 160);

    /// <summary>Background color of the drag handle column.</summary>
    public KumikoColor RowDragHandleBackgroundColor { get; set => Set(ref field, value); } = new(248, 248, 248);

    /// <summary>Background color of the drag handle header area.</summary>
    public KumikoColor RowDragHandleHeaderBackgroundColor { get; set => Set(ref field, value); } = new(235, 235, 235);

    // ── Interaction ──────────────────────────────────────────────
    /// <summary>
    /// Width in pixels of a column's resize grip for a mouse or a pen: the band centered on the column's right header
    /// edge where a press starts a resize and the resize cursor shows. Each half reaches at most a quarter of the
    /// column on that side of the edge (at least 3 px), so the middle of a narrow header still sorts. Default: 12.
    /// </summary>
    public float ColumnResizeGripWidth { get; set => Set(ref field, value); } = 12f;

    /// <summary>
    /// Width in pixels of a column's resize grip for a finger (see <see cref="ColumnResizeGripWidth"/>), wider so a
    /// touch can find the edge. Default: 32.
    /// </summary>
    public float TouchColumnResizeGripWidth { get; set => Set(ref field, value); } = 32f;

    /// <summary>
    /// Color laid over what the mouse or pen hovers when a click acts on it: a sortable column header, a filter
    /// icon, a grouping panel chip or its remove button, a group header's chevron, a Boolean cell's check box and an
    /// enabled action button. Translucent, so it darkens (or, in the Dark theme, lightens) whatever is under it.
    /// A transparent color turns hover feedback off. Default: black at 6% opacity.
    /// </summary>
    public KumikoColor HoverOverlayColor { get; set => Set(ref field, value); } = new(0, 0, 0, 16);

    /// <summary>
    /// Color laid over the same targets as <see cref="HoverOverlayColor"/> while a press on them is held, with any
    /// pointer (a finger too), as long as the pointer stays on the target. A transparent color turns pressed feedback
    /// off. Default: black at 12% opacity.
    /// </summary>
    public KumikoColor PressedOverlayColor { get; set => Set(ref field, value); } = new(0, 0, 0, 31);

    /// <summary>
    /// Whether the grid plays its short transitions: a group's rows sliding open or shut, a dragged column header
    /// lifting and settling into its slot, and the current row's highlight fading in. False shows every change at once.
    /// The built-in hosts also skip them when the platform asks for reduced motion
    /// (<see cref="Input.KumikoInputController.AnimationsEnabled"/>). Default: true.
    /// </summary>
    public bool EnableAnimations { get; set => Set(ref field, value); } = true;

    // ── Icons ────────────────────────────────────────────────────
    /// <summary>
    /// Draws every built-in glyph of the grid (group chevrons, sort arrows, filter buttons, grouping chip buttons,
    /// drag grips, check marks) and of the editors and filter popup it opens. Glyph colors still come from the
    /// style's color properties. Default: <see cref="KumikoIconProvider.Default"/>, the built-in vector set; use a
    /// <see cref="KumikoFontIconProvider"/> for an icon font, or your own <see cref="IKumikoIconProvider"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when set to <c>null</c>.</exception>
    public IKumikoIconProvider IconProvider
    {
        get;
        set => Set(ref field, value ?? throw new ArgumentNullException(nameof(value)));
    } = KumikoIconProvider.Default;

    // ── Conditional style resolvers ──────────────────────────────
    /// <summary>
    /// Callback invoked for each visible cell to dynamically resolve a KumikoCellStyle
    /// based on the row data item and column. Return null if no override is needed.
    /// Dynamic styles take precedence over per-column KumikoCellStyle.
    /// </summary>
    public Func<object, KumikoColumn, KumikoCellStyle?>? CellStyleResolver { get; set => Set(ref field, value); }

    /// <summary>
    /// Callback invoked for each visible row to dynamically resolve a KumikoRowStyle
    /// based on the row data item. Return null if no override is needed.
    /// </summary>
    public Func<object, KumikoRowStyle?>? RowStyleResolver { get; set => Set(ref field, value); }
}

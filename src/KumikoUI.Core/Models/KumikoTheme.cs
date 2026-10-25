using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Models;

/// <summary>
/// Factory for creating preconfigured <see cref="KumikoStyle"/> instances
/// representing complete visual themes.
/// </summary>
public static class KumikoTheme
{
    /// <summary>
    /// Creates a <see cref="KumikoStyle"/> for the specified built-in theme.
    /// </summary>
    public static KumikoStyle Create(KumikoThemeMode theme) => theme switch
    {
        KumikoThemeMode.Light => CreateLight(),
        KumikoThemeMode.Dark => CreateDark(),
        KumikoThemeMode.HighContrast => CreateHighContrast(),
        _ => CreateLight()
    };

    /// <summary>
    /// Creates the default Light theme. This matches the original KumikoStyle defaults.
    /// </summary>
    public static KumikoStyle CreateLight() => new();

    /// <summary>
    /// Creates a Dark theme suitable for dark-mode UIs.
    /// </summary>
    public static KumikoStyle CreateDark()
    {
        return new KumikoStyle
        {
            // ── Colors ───────────────────────────────────────────────
            BackgroundColor = new KumikoColor(30, 30, 30),
            HeaderBackgroundColor = new KumikoColor(45, 45, 48),
            HeaderTextColor = new KumikoColor(220, 220, 220),
            CellTextColor = new KumikoColor(204, 204, 204),
            GridLineColor = new KumikoColor(60, 60, 60),
            SelectionColor = new KumikoColor(38, 79, 120, 100),
            SelectionTextColor = new KumikoColor(255, 255, 255),
            FocusedRowColor = new KumikoColor(38, 79, 120, 50),
            AlternateRowColor = new KumikoColor(37, 37, 38),
            SortIndicatorColor = new KumikoColor(170, 170, 170),
            CurrentCellBorderColor = new KumikoColor(0, 122, 204),
            CurrentCellBorderWidth = 2f,
            AccentColor = new KumikoColor(0, 122, 204),

            // ── Header ───────────────────────────────────────────────
            HeaderBorderColor = new KumikoColor(60, 60, 60),
            HeaderPadding = 8f,

            // ── Frozen columns ───────────────────────────────────────
            FrozenColumnDividerColor = new KumikoColor(80, 80, 80),
            FrozenColumnBackgroundColor = new KumikoColor(38, 38, 40),
            RightFrozenColumnDividerColor = new KumikoColor(80, 80, 80),
            RightFrozenColumnBackgroundColor = new KumikoColor(38, 38, 40),

            // ── Frozen rows ──────────────────────────────────────────
            FrozenRowDividerColor = new KumikoColor(80, 80, 80),
            FrozenRowBackgroundColor = new KumikoColor(38, 38, 40),

            // ── Filter ───────────────────────────────────────────────
            FilterIconColor = new KumikoColor(140, 140, 140),
            FilterActiveIconColor = new KumikoColor(55, 148, 255),

            // ── Grouping ─────────────────────────────────────────────
            GroupHeaderBackgroundColor = new KumikoColor(40, 40, 42),
            GroupHeaderTextColor = new KumikoColor(200, 200, 200),
            GroupHeaderCountColor = new KumikoColor(130, 130, 130),
            GroupChevronColor = new KumikoColor(170, 170, 170),
            GroupChevronBackgroundColor = new KumikoColor(55, 60, 70),
            GroupPanelBackgroundColor = new KumikoColor(35, 35, 38),
            GroupPanelTextColor = new KumikoColor(160, 165, 175),
            GroupPanelLabelColor = new KumikoColor(120, 125, 135),
            GroupPanelChipBackgroundColor = new KumikoColor(50, 55, 65),
            GroupPanelChipBorderColor = new KumikoColor(70, 75, 90),
            GroupPanelChipAccentColor = new KumikoColor(55, 148, 255),
            GroupPanelChipTextColor = new KumikoColor(200, 205, 215),
            GroupPanelChipRemoveColor = new KumikoColor(140, 145, 155),

            // ── Summaries ────────────────────────────────────────────
            SummaryRowBackgroundColor = new KumikoColor(38, 42, 50),
            SummaryRowTextColor = new KumikoColor(204, 204, 204),
            SummaryRowLabelColor = new KumikoColor(140, 140, 140),
            SummaryRowBorderColor = new KumikoColor(60, 60, 60),
            GroupSummaryRowBackgroundColor = new KumikoColor(42, 45, 52),
            GroupSummaryRowTextColor = new KumikoColor(190, 190, 190),
            CaptionSummaryTextColor = new KumikoColor(140, 140, 140),

            // ── Row drag & drop ──────────────────────────────────────
            RowDragOverlayColor = new KumikoColor(40, 40, 40, 200),
            RowDragBorderColor = new KumikoColor(0, 122, 204),
            RowDropIndicatorColor = new KumikoColor(0, 122, 204),
            RowDragHandleColor = new KumikoColor(130, 130, 130),
            RowDragHandleBackgroundColor = new KumikoColor(35, 35, 38),
            RowDragHandleHeaderBackgroundColor = new KumikoColor(42, 42, 45),
        };
    }

    /// <summary>
    /// Creates a High Contrast theme for accessibility.
    /// Uses pure black/white with bright accent colors and thick borders.
    /// </summary>
    public static KumikoStyle CreateHighContrast()
    {
        return new KumikoStyle
        {
            // ── Colors ───────────────────────────────────────────────
            BackgroundColor = KumikoColor.Black,
            HeaderBackgroundColor = new KumikoColor(0, 0, 128),
            HeaderTextColor = KumikoColor.White,
            CellTextColor = KumikoColor.White,
            GridLineColor = new KumikoColor(0, 255, 0),
            SelectionColor = new KumikoColor(0, 128, 255, 140),
            SelectionTextColor = new KumikoColor(255, 255, 255),
            FocusedRowColor = new KumikoColor(0, 128, 255, 80),
            AlternateRowColor = new KumikoColor(20, 20, 20),
            SortIndicatorColor = new KumikoColor(255, 255, 0),
            CurrentCellBorderColor = new KumikoColor(255, 255, 0),
            CurrentCellBorderWidth = 3f,
            AccentColor = new KumikoColor(0, 255, 255),

            // ── Header ───────────────────────────────────────────────
            HeaderBorderColor = new KumikoColor(0, 255, 0),
            HeaderPadding = 10f,

            // ── Grid lines ───────────────────────────────────────────
            GridLineWidth = 1f,

            // ── Frozen columns ───────────────────────────────────────
            FrozenColumnDividerColor = new KumikoColor(255, 255, 0),
            FrozenColumnDividerWidth = 2f,
            FrozenColumnBackgroundColor = new KumikoColor(10, 10, 10),
            RightFrozenColumnDividerColor = new KumikoColor(255, 255, 0),
            RightFrozenColumnDividerWidth = 2f,
            RightFrozenColumnBackgroundColor = new KumikoColor(10, 10, 10),

            // ── Frozen rows ──────────────────────────────────────────
            FrozenRowDividerColor = new KumikoColor(255, 255, 0),
            FrozenRowDividerWidth = 2f,
            FrozenRowBackgroundColor = new KumikoColor(10, 10, 10),

            // ── Filter ───────────────────────────────────────────────
            FilterIconColor = new KumikoColor(0, 255, 255),
            FilterActiveIconColor = new KumikoColor(255, 255, 0),

            // ── Grouping ─────────────────────────────────────────────
            GroupHeaderBackgroundColor = new KumikoColor(0, 0, 80),
            GroupHeaderTextColor = KumikoColor.White,
            GroupHeaderCountColor = new KumikoColor(0, 255, 255),
            GroupChevronColor = new KumikoColor(255, 255, 0),
            GroupChevronBackgroundColor = new KumikoColor(0, 0, 60),
            GroupPanelBackgroundColor = new KumikoColor(0, 0, 40),
            GroupPanelTextColor = KumikoColor.White,
            GroupPanelLabelColor = new KumikoColor(0, 255, 255),
            GroupPanelChipBackgroundColor = new KumikoColor(0, 0, 80),
            GroupPanelChipBorderColor = new KumikoColor(0, 255, 255),
            GroupPanelChipAccentColor = new KumikoColor(255, 255, 0),
            GroupPanelChipTextColor = KumikoColor.White,
            GroupPanelChipRemoveColor = new KumikoColor(255, 0, 0),

            // ── Summaries ────────────────────────────────────────────
            SummaryRowBackgroundColor = new KumikoColor(0, 0, 80),
            SummaryRowTextColor = KumikoColor.White,
            SummaryRowLabelColor = new KumikoColor(0, 255, 255),
            SummaryRowBorderColor = new KumikoColor(0, 255, 0),
            GroupSummaryRowBackgroundColor = new KumikoColor(0, 0, 60),
            GroupSummaryRowTextColor = KumikoColor.White,
            CaptionSummaryTextColor = new KumikoColor(0, 255, 255),

            // ── Row drag & drop ──────────────────────────────────────
            RowDragOverlayColor = new KumikoColor(0, 0, 0, 200),
            RowDragBorderColor = new KumikoColor(255, 255, 0),
            RowDropIndicatorColor = new KumikoColor(255, 255, 0),
            RowDragHandleColor = new KumikoColor(0, 255, 255),
            RowDragHandleBackgroundColor = KumikoColor.Black,
            RowDragHandleHeaderBackgroundColor = new KumikoColor(0, 0, 80),
        };
    }
}

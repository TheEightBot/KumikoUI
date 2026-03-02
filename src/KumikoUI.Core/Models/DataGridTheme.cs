using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Models;

/// <summary>
/// Identifies built-in themes.
/// </summary>
public enum DataGridThemeMode
{
    /// <summary>Light theme with white background and dark text.</summary>
    Light,
    /// <summary>Dark theme with dark background and light text.</summary>
    Dark,
    /// <summary>High-contrast theme for accessibility.</summary>
    HighContrast
}

/// <summary>
/// Factory for creating preconfigured <see cref="DataGridStyle"/> instances
/// representing complete visual themes.
/// </summary>
public static class DataGridTheme
{
    /// <summary>
    /// Creates a <see cref="DataGridStyle"/> for the specified built-in theme.
    /// </summary>
    public static DataGridStyle Create(DataGridThemeMode theme) => theme switch
    {
        DataGridThemeMode.Light => CreateLight(),
        DataGridThemeMode.Dark => CreateDark(),
        DataGridThemeMode.HighContrast => CreateHighContrast(),
        _ => CreateLight()
    };

    /// <summary>
    /// Creates the default Light theme. This matches the original DataGridStyle defaults.
    /// </summary>
    public static DataGridStyle CreateLight() => new();

    /// <summary>
    /// Creates a Dark theme suitable for dark-mode UIs.
    /// </summary>
    public static DataGridStyle CreateDark()
    {
        return new DataGridStyle
        {
            // ── Colors ───────────────────────────────────────────────
            BackgroundColor = new GridColor(30, 30, 30),
            HeaderBackgroundColor = new GridColor(45, 45, 48),
            HeaderTextColor = new GridColor(220, 220, 220),
            CellTextColor = new GridColor(204, 204, 204),
            GridLineColor = new GridColor(60, 60, 60),
            SelectionColor = new GridColor(38, 79, 120, 100),
            SelectionTextColor = new GridColor(255, 255, 255),
            FocusedRowColor = new GridColor(38, 79, 120, 50),
            AlternateRowColor = new GridColor(37, 37, 38),
            SortIndicatorColor = new GridColor(170, 170, 170),
            CurrentCellBorderColor = new GridColor(0, 122, 204),
            CurrentCellBorderWidth = 2f,
            AccentColor = new GridColor(0, 122, 204),

            // ── Header ───────────────────────────────────────────────
            HeaderBorderColor = new GridColor(60, 60, 60),
            HeaderPadding = 8f,

            // ── Frozen columns ───────────────────────────────────────
            FrozenColumnDividerColor = new GridColor(80, 80, 80),
            FrozenColumnBackgroundColor = new GridColor(38, 38, 40),
            RightFrozenColumnDividerColor = new GridColor(80, 80, 80),
            RightFrozenColumnBackgroundColor = new GridColor(38, 38, 40),

            // ── Frozen rows ──────────────────────────────────────────
            FrozenRowDividerColor = new GridColor(80, 80, 80),
            FrozenRowBackgroundColor = new GridColor(38, 38, 40),

            // ── Filter ───────────────────────────────────────────────
            FilterIconColor = new GridColor(140, 140, 140),
            FilterActiveIconColor = new GridColor(55, 148, 255),

            // ── Grouping ─────────────────────────────────────────────
            GroupHeaderBackgroundColor = new GridColor(40, 40, 42),
            GroupHeaderTextColor = new GridColor(200, 200, 200),
            GroupHeaderCountColor = new GridColor(130, 130, 130),
            GroupChevronColor = new GridColor(170, 170, 170),
            GroupChevronBackgroundColor = new GridColor(55, 60, 70),
            GroupPanelBackgroundColor = new GridColor(35, 35, 38),
            GroupPanelTextColor = new GridColor(160, 165, 175),
            GroupPanelLabelColor = new GridColor(120, 125, 135),
            GroupPanelChipBackgroundColor = new GridColor(50, 55, 65),
            GroupPanelChipBorderColor = new GridColor(70, 75, 90),
            GroupPanelChipAccentColor = new GridColor(55, 148, 255),
            GroupPanelChipTextColor = new GridColor(200, 205, 215),
            GroupPanelChipRemoveColor = new GridColor(140, 145, 155),

            // ── Summaries ────────────────────────────────────────────
            SummaryRowBackgroundColor = new GridColor(38, 42, 50),
            SummaryRowTextColor = new GridColor(204, 204, 204),
            SummaryRowLabelColor = new GridColor(140, 140, 140),
            SummaryRowBorderColor = new GridColor(60, 60, 60),
            GroupSummaryRowBackgroundColor = new GridColor(42, 45, 52),
            GroupSummaryRowTextColor = new GridColor(190, 190, 190),
            CaptionSummaryTextColor = new GridColor(140, 140, 140),

            // ── Row drag & drop ──────────────────────────────────────
            RowDragOverlayColor = new GridColor(40, 40, 40, 200),
            RowDragBorderColor = new GridColor(0, 122, 204),
            RowDropIndicatorColor = new GridColor(0, 122, 204),
            RowDragHandleColor = new GridColor(130, 130, 130),
            RowDragHandleBackgroundColor = new GridColor(35, 35, 38),
            RowDragHandleHeaderBackgroundColor = new GridColor(42, 42, 45),
        };
    }

    /// <summary>
    /// Creates a High Contrast theme for accessibility.
    /// Uses pure black/white with bright accent colors and thick borders.
    /// </summary>
    public static DataGridStyle CreateHighContrast()
    {
        return new DataGridStyle
        {
            // ── Colors ───────────────────────────────────────────────
            BackgroundColor = GridColor.Black,
            HeaderBackgroundColor = new GridColor(0, 0, 128),
            HeaderTextColor = GridColor.White,
            CellTextColor = GridColor.White,
            GridLineColor = new GridColor(0, 255, 0),
            SelectionColor = new GridColor(0, 128, 255, 140),
            SelectionTextColor = new GridColor(255, 255, 255),
            FocusedRowColor = new GridColor(0, 128, 255, 80),
            AlternateRowColor = new GridColor(20, 20, 20),
            SortIndicatorColor = new GridColor(255, 255, 0),
            CurrentCellBorderColor = new GridColor(255, 255, 0),
            CurrentCellBorderWidth = 3f,
            AccentColor = new GridColor(0, 255, 255),

            // ── Header ───────────────────────────────────────────────
            HeaderBorderColor = new GridColor(0, 255, 0),
            HeaderPadding = 10f,

            // ── Grid lines ───────────────────────────────────────────
            GridLineWidth = 1f,

            // ── Frozen columns ───────────────────────────────────────
            FrozenColumnDividerColor = new GridColor(255, 255, 0),
            FrozenColumnDividerWidth = 2f,
            FrozenColumnBackgroundColor = new GridColor(10, 10, 10),
            RightFrozenColumnDividerColor = new GridColor(255, 255, 0),
            RightFrozenColumnDividerWidth = 2f,
            RightFrozenColumnBackgroundColor = new GridColor(10, 10, 10),

            // ── Frozen rows ──────────────────────────────────────────
            FrozenRowDividerColor = new GridColor(255, 255, 0),
            FrozenRowDividerWidth = 2f,
            FrozenRowBackgroundColor = new GridColor(10, 10, 10),

            // ── Filter ───────────────────────────────────────────────
            FilterIconColor = new GridColor(0, 255, 255),
            FilterActiveIconColor = new GridColor(255, 255, 0),

            // ── Grouping ─────────────────────────────────────────────
            GroupHeaderBackgroundColor = new GridColor(0, 0, 80),
            GroupHeaderTextColor = GridColor.White,
            GroupHeaderCountColor = new GridColor(0, 255, 255),
            GroupChevronColor = new GridColor(255, 255, 0),
            GroupChevronBackgroundColor = new GridColor(0, 0, 60),
            GroupPanelBackgroundColor = new GridColor(0, 0, 40),
            GroupPanelTextColor = GridColor.White,
            GroupPanelLabelColor = new GridColor(0, 255, 255),
            GroupPanelChipBackgroundColor = new GridColor(0, 0, 80),
            GroupPanelChipBorderColor = new GridColor(0, 255, 255),
            GroupPanelChipAccentColor = new GridColor(255, 255, 0),
            GroupPanelChipTextColor = GridColor.White,
            GroupPanelChipRemoveColor = new GridColor(255, 0, 0),

            // ── Summaries ────────────────────────────────────────────
            SummaryRowBackgroundColor = new GridColor(0, 0, 80),
            SummaryRowTextColor = GridColor.White,
            SummaryRowLabelColor = new GridColor(0, 255, 255),
            SummaryRowBorderColor = new GridColor(0, 255, 0),
            GroupSummaryRowBackgroundColor = new GridColor(0, 0, 60),
            GroupSummaryRowTextColor = GridColor.White,
            CaptionSummaryTextColor = new GridColor(0, 255, 255),

            // ── Row drag & drop ──────────────────────────────────────
            RowDragOverlayColor = new GridColor(0, 0, 0, 200),
            RowDragBorderColor = new GridColor(255, 255, 0),
            RowDropIndicatorColor = new GridColor(255, 255, 0),
            RowDragHandleColor = new GridColor(0, 255, 255),
            RowDragHandleBackgroundColor = GridColor.Black,
            RowDragHandleHeaderBackgroundColor = new GridColor(0, 0, 80),
        };
    }
}

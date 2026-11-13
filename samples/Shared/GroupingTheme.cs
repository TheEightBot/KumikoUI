using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace SampleApp.Shared;

/// <summary>
/// The light and dark styles of the Grouping sample page, shared by the MAUI and Uno samples so both hosts
/// draw the same grid. Built for reading: neutral gray surfaces with no tint, near-black (or near-white) text at
/// 14 pt, group headers set apart by a solid band and bold text rather than by color, and one blue accent kept
/// for what is active (selection, current cell, sort, active filter, group chips). Body text is at least 12:1
/// against its background and secondary text at least 7:1 (WCAG AAA).
/// </summary>
/// <remarks>
/// Each style starts from the built-in preset of the same mode, so anything not listed here (frozen panes,
/// drag and drop, popups) keeps a value that matches the mode. The dark variant uses the same neutral grays as
/// the sample pages around the grid, so the grid does not sit on the page as a tinted block.
/// </remarks>
public static class GroupingTheme
{
    /// <summary>Creates the style for the given mode.</summary>
    /// <param name="dark">True for the dark variant, false for the light one.</param>
    /// <returns>A new style instance; assign it to the grid's <c>GridStyle</c>.</returns>
    public static KumikoStyle Create(bool dark) => dark ? CreateDark() : CreateLight();

    /// <summary>Creates the light variant.</summary>
    /// <returns>A new style instance.</returns>
    public static KumikoStyle CreateLight()
    {
        var accent = Rgb(0x25, 0x63, 0xEB);      // blue 600
        var ink = Rgb(0x11, 0x18, 0x27);         // gray 900: body text
        var inkSecondary = Rgb(0x37, 0x41, 0x51); // gray 700: counts, labels, summaries
        var line = Rgb(0xE5, 0xE7, 0xEB);        // gray 200
        var lineStrong = Rgb(0xD1, 0xD5, 0xDB);  // gray 300
        var surfaceAlt = Rgb(0xF3, 0xF4, 0xF6);  // gray 100

        var style = KumikoTheme.CreateLight();
        ApplyFonts(style);

        style.BackgroundColor = KumikoColor.White;
        style.CellTextColor = ink;
        style.GridLineColor = line;
        style.CheckBoxBorderColor = Rgb(0x6B, 0x72, 0x80); // gray 500: an unchecked box stays visible
        style.AlternateRowBackground = true;
        style.AlternateRowColor = Rgb(0xF9, 0xFA, 0xFB); // gray 50
        style.AccentColor = accent;

        style.HeaderBackgroundColor = surfaceAlt;
        style.HeaderTextColor = ink;
        style.HeaderBorderColor = lineStrong;
        style.SortIndicatorColor = accent;
        style.FilterIconColor = Rgb(0x6B, 0x72, 0x80); // gray 500
        style.FilterActiveIconColor = accent;

        style.SelectionColor = Rgb(0xDB, 0xEA, 0xFE);   // blue 100, opaque so text keeps its contrast
        style.SelectionTextColor = ink;
        style.FocusedRowColor = Rgb(0xEF, 0xF6, 0xFF);  // blue 50
        style.CurrentCellBorderColor = accent;
        style.CurrentCellBorderWidth = 2f;

        style.GroupHeaderBackgroundColor = Rgb(0xE5, 0xE7, 0xEB); // gray 200: a solid band
        style.GroupHeaderTextColor = ink;
        style.GroupHeaderCountColor = inkSecondary;
        style.GroupChevronColor = ink;
        style.GroupChevronBackgroundColor = Rgb(0xD1, 0xD5, 0xDB);
        style.CaptionSummaryTextColor = inkSecondary;

        style.GroupPanelBackgroundColor = surfaceAlt;
        style.GroupPanelLabelColor = inkSecondary;
        style.GroupPanelTextColor = inkSecondary;
        style.GroupPanelChipBackgroundColor = KumikoColor.White;
        style.GroupPanelChipBorderColor = lineStrong;
        style.GroupPanelChipAccentColor = accent;
        style.GroupPanelChipTextColor = ink;
        style.GroupPanelChipRemoveColor = Rgb(0x4B, 0x55, 0x63); // gray 600

        style.GroupSummaryRowBackgroundColor = Rgb(0xF9, 0xFA, 0xFB);
        style.GroupSummaryRowTextColor = inkSecondary;

        style.SummaryRowBackgroundColor = surfaceAlt;
        style.SummaryRowTextColor = ink;
        style.SummaryRowLabelColor = inkSecondary;
        style.SummaryRowBorderColor = lineStrong;

        style.RowDragBorderColor = accent;
        style.RowDropIndicatorColor = accent;
        return style;
    }

    /// <summary>Creates the dark variant.</summary>
    /// <returns>A new style instance.</returns>
    public static KumikoStyle CreateDark()
    {
        var accent = Rgb(0x60, 0xA5, 0xFA);      // blue 400
        var ink = Rgb(0xF3, 0xF4, 0xF6);         // gray 100: body text
        var inkSecondary = Rgb(0xC4, 0xC9, 0xD1); // counts, labels, summaries
        var surface = Rgb(0x1C, 0x1C, 0x1E);
        var surfaceRaised = Rgb(0x2A, 0x2A, 0x2D);
        var line = Rgb(0x33, 0x33, 0x37);
        var lineStrong = Rgb(0x45, 0x45, 0x4A);

        var style = KumikoTheme.CreateDark();
        ApplyFonts(style);

        style.BackgroundColor = surface;
        style.CellTextColor = ink;
        style.GridLineColor = line;
        style.CheckBoxBorderColor = Rgb(0x9C, 0xA3, 0xAF); // gray 400: an unchecked box stays visible
        style.AlternateRowBackground = true;
        style.AlternateRowColor = Rgb(0x22, 0x22, 0x25);
        style.AccentColor = accent;

        style.HeaderBackgroundColor = surfaceRaised;
        style.HeaderTextColor = ink;
        style.HeaderBorderColor = lineStrong;
        style.SortIndicatorColor = accent;
        style.FilterIconColor = Rgb(0x9C, 0xA3, 0xAF); // gray 400
        style.FilterActiveIconColor = accent;

        style.SelectionColor = Rgb(0x1E, 0x3A, 0x5F);   // a deep blue, opaque so text keeps its contrast
        style.SelectionTextColor = KumikoColor.White;
        style.FocusedRowColor = Rgb(0x26, 0x2B, 0x33);
        style.CurrentCellBorderColor = accent;
        style.CurrentCellBorderWidth = 2f;

        style.GroupHeaderBackgroundColor = Rgb(0x34, 0x34, 0x38); // a solid band
        style.GroupHeaderTextColor = KumikoColor.White;
        style.GroupHeaderCountColor = inkSecondary;
        style.GroupChevronColor = ink;
        style.GroupChevronBackgroundColor = Rgb(0x4A, 0x4A, 0x50);
        style.CaptionSummaryTextColor = inkSecondary;

        style.GroupPanelBackgroundColor = surfaceRaised;
        style.GroupPanelLabelColor = inkSecondary;
        style.GroupPanelTextColor = inkSecondary;
        style.GroupPanelChipBackgroundColor = Rgb(0x3A, 0x3A, 0x3F);
        style.GroupPanelChipBorderColor = Rgb(0x55, 0x55, 0x5B);
        style.GroupPanelChipAccentColor = accent;
        style.GroupPanelChipTextColor = ink;
        style.GroupPanelChipRemoveColor = inkSecondary;

        style.GroupSummaryRowBackgroundColor = Rgb(0x24, 0x24, 0x27);
        style.GroupSummaryRowTextColor = inkSecondary;

        style.SummaryRowBackgroundColor = surfaceRaised;
        style.SummaryRowTextColor = ink;
        style.SummaryRowLabelColor = inkSecondary;
        style.SummaryRowBorderColor = lineStrong;

        style.RowDragBorderColor = accent;
        style.RowDropIndicatorColor = accent;
        return style;
    }

    // One step larger than the defaults everywhere, so nothing on the page is drawn below 12 pt.
    private static void ApplyFonts(KumikoStyle style)
    {
        style.CellFont = new KumikoFont("Default", 14);
        style.HeaderFont = new KumikoFont("Default", 14, bold: true);
        style.GroupHeaderFont = new KumikoFont("Default", 14, bold: true);
        style.GroupCountFont = new KumikoFont("Default", 13);
        style.CaptionSummaryFont = new KumikoFont("Default", 13);
        style.GroupSummaryRowFont = new KumikoFont("Default", 13, bold: true);
        style.SummaryRowFont = new KumikoFont("Default", 13, bold: true);
        style.SummaryRowLabelFont = new KumikoFont("Default", 13);
        style.GroupPanelLabelFont = new KumikoFont("Default", 12);
        style.GroupPanelFont = new KumikoFont("Default", 13, bold: true);
    }

    private static KumikoColor Rgb(byte r, byte g, byte b) => new(r, g, b);
}

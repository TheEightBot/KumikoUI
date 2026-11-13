using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace SampleApp.Shared;

/// <summary>
/// The light and dark styles of the Grouping sample page, shared by the MAUI and Uno samples so both hosts
/// draw the same grid. A cool slate palette with one indigo accent: group headers sit on a faint indigo tint,
/// the accent marks only what is active (selection, current cell, sort and active filter, group chips,
/// chevrons and caption summaries), and grid lines stay soft. Every text color meets WCAG AA (4.5:1) on the
/// background it is drawn on.
/// </summary>
/// <remarks>
/// Each style starts from the built-in preset of the same mode, so anything not listed here (frozen panes,
/// drag and drop, popups) keeps a value that matches the mode.
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
        var accent = Rgb(0x4F, 0x46, 0xE5);       // indigo 600
        var accentText = Rgb(0x43, 0x38, 0xCA);   // indigo 700, for accent-colored text
        var ink = Rgb(0x1E, 0x29, 0x3B);          // slate 800
        var inkMuted = Rgb(0x47, 0x55, 0x69);     // slate 600
        var line = Rgb(0xDA, 0xE1, 0xEA);         // between slate 200 and 300: soft, but still outlines checkboxes
        var surfaceAlt = Rgb(0xF8, 0xFA, 0xFC);   // slate 50

        var style = KumikoTheme.CreateLight();

        style.BackgroundColor = KumikoColor.White;
        style.CellTextColor = ink;
        style.GridLineColor = line;
        style.AlternateRowBackground = true;
        style.AlternateRowColor = Rgb(0xFA, 0xFB, 0xFD);
        style.AccentColor = accent;

        style.HeaderBackgroundColor = surfaceAlt;
        style.HeaderTextColor = Rgb(0x33, 0x41, 0x55); // slate 700
        style.HeaderBorderColor = Rgb(0xCB, 0xD5, 0xE1); // slate 300
        style.HeaderFont = new KumikoFont("Default", 13, bold: true);
        style.SortIndicatorColor = accent;
        style.FilterIconColor = Rgb(0x94, 0xA3, 0xB8); // slate 400: quiet until active
        style.FilterActiveIconColor = accent;

        style.SelectionColor = accent.WithAlpha(30);
        style.SelectionTextColor = Rgb(0x1E, 0x1B, 0x4B); // indigo 950
        style.FocusedRowColor = accent.WithAlpha(14);
        style.CurrentCellBorderColor = accent;
        style.CurrentCellBorderWidth = 1.5f;

        style.GroupHeaderBackgroundColor = Rgb(0xEE, 0xF1, 0xFB);
        style.GroupHeaderTextColor = Rgb(0x0F, 0x17, 0x2A); // slate 900
        style.GroupHeaderCountColor = inkMuted;
        style.GroupChevronColor = accentText;
        style.GroupChevronBackgroundColor = Rgb(0xDC, 0xE1, 0xF7);
        style.CaptionSummaryTextColor = accentText;
        style.CaptionSummaryFont = new KumikoFont("Default", 12);

        style.GroupPanelBackgroundColor = surfaceAlt;
        style.GroupPanelLabelColor = inkMuted;
        style.GroupPanelTextColor = inkMuted;
        style.GroupPanelChipBackgroundColor = KumikoColor.White;
        style.GroupPanelChipBorderColor = Rgb(0xCB, 0xD5, 0xE1);
        style.GroupPanelChipAccentColor = accent;
        style.GroupPanelChipTextColor = ink;
        style.GroupPanelChipRemoveColor = Rgb(0x94, 0xA3, 0xB8);

        style.GroupSummaryRowBackgroundColor = surfaceAlt;
        style.GroupSummaryRowTextColor = Rgb(0x33, 0x41, 0x55);

        style.SummaryRowBackgroundColor = Rgb(0xF1, 0xF5, 0xF9); // slate 100
        style.SummaryRowTextColor = Rgb(0x0F, 0x17, 0x2A);
        style.SummaryRowLabelColor = inkMuted;
        style.SummaryRowBorderColor = Rgb(0xCB, 0xD5, 0xE1);

        style.RowDragBorderColor = accent;
        style.RowDropIndicatorColor = accent;
        return style;
    }

    /// <summary>Creates the dark variant.</summary>
    /// <returns>A new style instance.</returns>
    public static KumikoStyle CreateDark()
    {
        var accent = Rgb(0x81, 0x8C, 0xF8);       // indigo 400
        var accentText = Rgb(0xA5, 0xB4, 0xFC);   // indigo 300, for accent-colored text
        var ink = Rgb(0xE2, 0xE8, 0xF0);          // slate 200
        var inkMuted = Rgb(0x94, 0xA3, 0xB8);     // slate 400
        var surface = Rgb(0x14, 0x17, 0x1F);
        var surfaceRaised = Rgb(0x1A, 0x1E, 0x28);
        var line = Rgb(0x33, 0x3B, 0x50);

        var style = KumikoTheme.CreateDark();

        style.BackgroundColor = surface;
        style.CellTextColor = ink;
        style.GridLineColor = line;
        style.AlternateRowBackground = true;
        style.AlternateRowColor = Rgb(0x17, 0x1B, 0x24);
        style.AccentColor = accent;

        style.HeaderBackgroundColor = surfaceRaised;
        style.HeaderTextColor = Rgb(0xCB, 0xD5, 0xE1); // slate 300
        style.HeaderBorderColor = Rgb(0x33, 0x3B, 0x4F);
        style.HeaderFont = new KumikoFont("Default", 13, bold: true);
        style.SortIndicatorColor = accent;
        style.FilterIconColor = Rgb(0x64, 0x74, 0x8B); // slate 500: quiet until active
        style.FilterActiveIconColor = accent;

        style.SelectionColor = accent.WithAlpha(46);
        style.SelectionTextColor = Rgb(0xF8, 0xFA, 0xFC);
        style.FocusedRowColor = accent.WithAlpha(20);
        style.CurrentCellBorderColor = accent;
        style.CurrentCellBorderWidth = 1.5f;

        style.GroupHeaderBackgroundColor = Rgb(0x1D, 0x22, 0x36);
        style.GroupHeaderTextColor = Rgb(0xF1, 0xF5, 0xF9); // slate 100
        style.GroupHeaderCountColor = inkMuted;
        style.GroupChevronColor = accentText;
        style.GroupChevronBackgroundColor = Rgb(0x2B, 0x32, 0x54);
        style.CaptionSummaryTextColor = accentText;
        style.CaptionSummaryFont = new KumikoFont("Default", 12);

        style.GroupPanelBackgroundColor = surfaceRaised;
        style.GroupPanelLabelColor = inkMuted;
        style.GroupPanelTextColor = inkMuted;
        style.GroupPanelChipBackgroundColor = Rgb(0x22, 0x27, 0x36);
        style.GroupPanelChipBorderColor = Rgb(0x3A, 0x42, 0x5A);
        style.GroupPanelChipAccentColor = accent;
        style.GroupPanelChipTextColor = ink;
        style.GroupPanelChipRemoveColor = inkMuted;

        style.GroupSummaryRowBackgroundColor = Rgb(0x18, 0x1C, 0x27);
        style.GroupSummaryRowTextColor = Rgb(0xCB, 0xD5, 0xE1);

        style.SummaryRowBackgroundColor = surfaceRaised;
        style.SummaryRowTextColor = Rgb(0xF1, 0xF5, 0xF9);
        style.SummaryRowLabelColor = inkMuted;
        style.SummaryRowBorderColor = Rgb(0x33, 0x3B, 0x4F);

        style.RowDragBorderColor = accent;
        style.RowDropIndicatorColor = accent;
        return style;
    }

    private static KumikoColor Rgb(byte r, byte g, byte b) => new(r, g, b);
}

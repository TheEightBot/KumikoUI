using KumikoUI.Core.Models;

namespace KumikoUI.Export.Excel;

/// <summary>
/// How <see cref="KumikoXlsxExporter"/> lays out the workbook. What is exported (rows, columns, groups, values) is set by
/// <see cref="KumikoUI.Core.Export.KumikoExportOptions"/>; this record only covers what is specific to Excel.
/// </summary>
/// <remarks>The defaults write one sheet named "Sheet1" with a frozen, filterable header row and the grid's column widths.</remarks>
public sealed record KumikoXlsxOptions
{
    /// <summary>
    /// The name of the sheet. Default "Sheet1". It is made valid for Excel when the workbook is written: the characters
    /// <c>[ ] : * ? / \</c> and control characters are removed, a leading or trailing apostrophe is dropped, the name is cut
    /// to 31 characters, the reserved name "History" gets a trailing underscore, and an empty result becomes "Sheet1".
    /// </summary>
    public string SheetName { get; init; } = "Sheet1";

    /// <summary>
    /// Whether the header row, and the columns frozen at the left of the grid, stay in view when the sheet scrolls.
    /// Default <see langword="true"/>. Without a header row (<c>IncludeHeaders = false</c>) only the columns are frozen.
    /// </summary>
    public bool FreezeHeader { get; init; } = true;

    /// <summary>Whether the header row gets Excel's filter and sort buttons. Default <see langword="true"/>; needs a header row.</summary>
    public bool AutoFilter { get; init; } = true;

    /// <summary>How the columns are sized. Default <see cref="KumikoXlsxColumnWidths.FromGrid"/>.</summary>
    public KumikoXlsxColumnWidths ColumnWidths { get; init; } = KumikoXlsxColumnWidths.FromGrid;

    /// <summary>
    /// Whether the columns' <c>KumikoColumn.TextAlignment</c> is applied to their cells and header. Default
    /// <see langword="true"/>. Center and right alignment are written; left alignment keeps Excel's general alignment,
    /// which puts text on the left and numbers on the right.
    /// </summary>
    public bool ApplyColumnAlignment { get; init; } = true;

    /// <summary>
    /// Whether the header, group header and summary rows use the grid's colors (background and text) from
    /// <see cref="Style"/>. Default <see langword="false"/>: those rows are bold, with no fill.
    /// </summary>
    public bool UseGridColors { get; init; }

    /// <summary>
    /// The style whose colors <see cref="UseGridColors"/> applies: <c>HeaderBackgroundColor</c> and <c>HeaderTextColor</c>,
    /// <c>GroupHeaderBackgroundColor</c> and <c>GroupHeaderTextColor</c>, <c>GroupSummaryRowBackgroundColor</c> and
    /// <c>GroupSummaryRowTextColor</c>, <c>SummaryRowBackgroundColor</c> and <c>SummaryRowTextColor</c>. Pass the grid's
    /// <c>GridStyle</c>; <see langword="null"/> uses a default <see cref="KumikoStyle"/>. It is read when the export starts.
    /// </summary>
    public KumikoStyle? Style { get; init; }

    /// <summary>The workbook's title document property, or <see langword="null"/> (default) for none.</summary>
    public string? Title { get; init; }

    /// <summary>The workbook's author (creator) document property, or <see langword="null"/> (default) for none.</summary>
    public string? Author { get; init; }
}

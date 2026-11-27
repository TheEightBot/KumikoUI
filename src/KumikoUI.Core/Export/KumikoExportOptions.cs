using System.Globalization;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Export;

/// <summary>
/// What an export writes: the rows, the columns, the headers, grouping and summaries, and how values and numbers
/// are written. The defaults export what the user sees (the filtered rows, the visible columns in pane order, with
/// headers). How a file is written (delimiter, sheet name, page size) belongs to each format's own options.
/// </summary>
public sealed record KumikoExportOptions
{
    /// <summary>Which rows to export. Default: <see cref="KumikoExportRows.Filtered"/>.</summary>
    public KumikoExportRows Rows { get; init; } = KumikoExportRows.Filtered;

    /// <summary>The rows to export when <see cref="Rows"/> is <see cref="KumikoExportRows.Selected"/>; required then.</summary>
    public KumikoExportSelection? Selection { get; init; }

    /// <summary>The columns to export, in this order, including ones that are hidden or have no text; <c>null</c> (the default) uses the visible columns in pane order.</summary>
    public IReadOnlyList<KumikoColumn>? Columns { get; init; }

    /// <summary>Whether to include columns that are not visible when <see cref="Columns"/> is <c>null</c>. Default: <c>false</c>.</summary>
    public bool IncludeHiddenColumns { get; init; }

    /// <summary>Keeps only the columns it returns <c>true</c> for when <see cref="Columns"/> is <c>null</c>; <c>null</c> keeps them all.</summary>
    public Func<KumikoColumn, bool>? ColumnFilter { get; init; }

    /// <summary>Whether to start with a row of column headers. Default: <c>true</c>.</summary>
    public bool IncludeHeaders { get; init; } = true;

    /// <summary>The header text of a column; <c>null</c> (the default) uses <see cref="KumikoColumn.Header"/>.</summary>
    public Func<KumikoColumn, string>? HeaderSelector { get; init; }

    /// <summary>How grouped rows are laid out. Default: <see cref="KumikoExportGroupLayout.Rows"/>.</summary>
    public KumikoExportGroupLayout GroupLayout { get; init; } = KumikoExportGroupLayout.Rows;

    /// <summary>
    /// Whether to write the table summary rows and (with <see cref="KumikoExportGroupLayout.Rows"/>) the group summary
    /// rows. <c>null</c> (the default) writes them when <see cref="GroupLayout"/> is <see cref="KumikoExportGroupLayout.Rows"/>.
    /// Summaries describe the grid's view, so they are not written for <see cref="KumikoExportRows.All"/> or
    /// <see cref="KumikoExportRows.Selected"/>.
    /// </summary>
    public bool? IncludeSummaries { get; init; }

    /// <summary>How cell values are written. Default: <see cref="KumikoExportValueMode.Auto"/>.</summary>
    public KumikoExportValueMode ValueMode { get; init; } = KumikoExportValueMode.Auto;

    /// <summary>
    /// The culture of display text. <c>null</c> (the default) uses <see cref="KumikoSource.Culture"/>, or the current
    /// culture when that is <c>null</c> too; it is read once when the export starts. Raw text is always invariant.
    /// </summary>
    public CultureInfo? Culture { get; init; }

    /// <summary>
    /// Read the items' values on the calling thread, in chunks of about 2,000 rows with a yield between them, instead
    /// of on a thread-pool thread (the default). Set it when the items' getters are not thread-safe.
    /// </summary>
    public bool ReadOnCallerThread { get; init; }
}

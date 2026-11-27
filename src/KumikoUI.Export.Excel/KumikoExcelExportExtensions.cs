using KumikoUI.Core.Export;
using KumikoUI.Core.Models;

namespace KumikoUI.Export.Excel;

/// <summary>Excel export of a <see cref="KumikoSource"/>.</summary>
public static class KumikoExcelExportExtensions
{
    /// <summary>
    /// Writes the source as an Excel workbook (<c>.xlsx</c>) to <paramref name="destination"/>; shorthand for
    /// <c>new KumikoXlsxExporter(xlsx).ExportAsync(...)</c>. Call it on the thread that owns the source (the UI thread).
    /// </summary>
    /// <param name="source">The grid's data source.</param>
    /// <param name="destination">A writable stream; it need not be seekable and is neither flushed by the caller nor closed.</param>
    /// <param name="options">What to export; <see langword="null"/> exports what the grid shows, with typed values.</param>
    /// <param name="xlsx">How the workbook is laid out; <see langword="null"/> uses the defaults of <see cref="KumikoXlsxOptions"/>.</param>
    /// <param name="progress">Receives the number of rows written; may be <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the export; the stream then holds an incomplete workbook.</param>
    /// <returns>A task that completes when the workbook has been written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="destination"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The options are invalid, or the export exceeds Excel's limits; nothing has been written.</exception>
    public static Task ExportToExcelAsync(this KumikoSource source, Stream destination, KumikoExportOptions? options = null,
        KumikoXlsxOptions? xlsx = null, IProgress<KumikoExportProgress>? progress = null,
        CancellationToken cancellationToken = default) =>
        new KumikoXlsxExporter(xlsx).ExportAsync(source, destination, options, progress, cancellationToken);
}

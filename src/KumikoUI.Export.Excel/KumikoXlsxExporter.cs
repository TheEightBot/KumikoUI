using System.IO.Compression;
using KumikoUI.Core.Export;
using KumikoUI.Core.Models;
using KumikoUI.Export.Excel.Internal;

namespace KumikoUI.Export.Excel;

/// <summary>
/// Exports a <see cref="KumikoSource"/> as an Excel workbook (<c>.xlsx</c>) with one sheet: typed cells (numbers, dates,
/// times, booleans and text) in the number formats the columns' <c>Format</c> maps to, a bold header row, column widths,
/// a frozen header, an autofilter, and groups as collapsible row outlines. The workbook is written row by row through
/// <see cref="ZipArchive"/>, so memory does not grow with the row count and the destination need not be seekable.
/// </summary>
/// <remarks>
/// <para>
/// With <see cref="KumikoExportValueMode.Auto"/> (the default) cells hold the values themselves: a number is a number
/// Excel can sum and a date is a date it can sort. A column whose <c>Format</c> Excel cannot express exactly is written
/// as the text the grid shows, so the sheet never shows a different value than the grid. Text is written as inline
/// strings, which Excel never evaluates, so a value starting with <c>=</c> is not run as a formula.
/// <see cref="KumikoExportValueMode.DisplayText"/> writes every cell as the grid's text.
/// </para>
/// <para>
/// Excel's limits are checked before any byte is written: 1,048,576 rows (header and summary rows included) and 16,384
/// columns; longer text than the 32,767 characters a cell holds is cut. The writer uses only BCL types (no reflection),
/// so it is trimming and Native AOT safe.
/// </para>
/// </remarks>
public sealed class KumikoXlsxExporter : IKumikoExporter
{
    private readonly KumikoXlsxOptions _xlsx;

    /// <summary>Creates an Excel exporter.</summary>
    /// <param name="xlsx">How the workbook is laid out; <see langword="null"/> uses the defaults of <see cref="KumikoXlsxOptions"/>.</param>
    public KumikoXlsxExporter(KumikoXlsxOptions? xlsx = null)
    {
        _xlsx = xlsx ?? new KumikoXlsxOptions();
    }

    /// <summary>"Excel".</summary>
    public string FormatName => "Excel";

    /// <summary>".xlsx".</summary>
    public string FileExtension => ".xlsx";

    /// <summary>"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet".</summary>
    public string MediaType => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>
    /// Writes the source as an Excel workbook to <paramref name="destination"/>. Call it on the thread that owns the
    /// source (the UI thread): the rows are copied there before the first <see langword="await"/>, then the values are
    /// read and written on a thread-pool thread (or on the calling thread in chunks with
    /// <see cref="KumikoExportOptions.ReadOnCallerThread"/>).
    /// </summary>
    /// <param name="source">The grid's data source.</param>
    /// <param name="destination">A writable stream, written from its current position; it need not be seekable and is not closed.</param>
    /// <param name="options">What to export; <see langword="null"/> exports what the grid shows, with typed values.</param>
    /// <param name="progress">Receives the number of rows written; may be <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the export; the stream then holds an incomplete workbook.</param>
    /// <returns>A task that completes when the workbook has been written and the destination flushed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="destination"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The destination is not writable, the options are invalid, or the export has more rows or columns than an Excel
    /// sheet holds. Nothing has been written.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public async Task ExportAsync(KumikoSource source, Stream destination, KumikoExportOptions? options = null,
        IProgress<KumikoExportProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanWrite) throw new ArgumentException("The destination stream is not writable.", nameof(destination));
        Validate(_xlsx);
        cancellationToken.ThrowIfCancellationRequested();

        // The snapshot reads the source: this runs on the caller's thread, before the first await.
        var snapshot = KumikoExportSnapshot.Create(source, options, KumikoExportValueMode.RawValue);
        var resolved = snapshot.Options;
        int outlineLevels = resolved.GroupLayout == KumikoExportGroupLayout.Rows && resolved.Rows != KumikoExportRows.All
            ? source.GroupDescriptions.Count
            : 0;
        var layout = XlsxLayout.Create(snapshot, _xlsx, outlineLevels); // throws over Excel's limits: nothing written yet
        bool coreProperties = _xlsx.Title != null || _xlsx.Author != null;
        // Reading on the caller's thread means every await before and around the row walk must come back to it.
        bool onCaller = snapshot.ReadOnCallerThread;

        var archive = await ZipArchive.CreateAsync(destination, ZipArchiveMode.Create, leaveOpen: true, entryNameEncoding: null,
            cancellationToken).ConfigureAwait(onCaller);
        await using (archive.ConfigureAwait(onCaller))
        {
            var part = new XmlText();
            await WritePartAsync(archive, "[Content_Types].xml", part, x => XlsxPackage.ContentTypes(x, coreProperties), cancellationToken).ConfigureAwait(onCaller);
            await WritePartAsync(archive, "_rels/.rels", part, x => XlsxPackage.PackageRelationships(x, coreProperties), cancellationToken).ConfigureAwait(onCaller);
            if (coreProperties)
                await WritePartAsync(archive, "docProps/core.xml", part, x => XlsxPackage.CoreProperties(x, _xlsx.Title, _xlsx.Author), cancellationToken).ConfigureAwait(onCaller);
            await WritePartAsync(archive, "xl/workbook.xml", part, x => XlsxPackage.Workbook(x, layout), cancellationToken).ConfigureAwait(onCaller);
            await WritePartAsync(archive, "xl/_rels/workbook.xml.rels", part, XlsxPackage.WorkbookRelationships, cancellationToken).ConfigureAwait(onCaller);
            await WritePartAsync(archive, "xl/styles.xml", part, layout.Styles.Write, cancellationToken).ConfigureAwait(onCaller);

            // The sheet is streamed: rows are encoded and compressed as they are read.
            var entry = archive.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.Fastest);
            var stream = await entry.OpenAsync(cancellationToken).ConfigureAwait(onCaller);
            await using (stream.ConfigureAwait(onCaller))
            {
                var sink = new XmlStreamSink(stream, canBlock: !snapshot.ReadOnCallerThread);
                var writer = new XlsxSheetWriter(layout, snapshot);
                writer.WriteStart(sink.Buffer);
                await snapshot.ForEachRowAsync(
                    row =>
                    {
                        writer.WriteRow(sink.Buffer, row);
                        sink.AfterRow();
                    },
                    sink.FlushAsync, progress, cancellationToken).ConfigureAwait(onCaller);
                writer.WriteEnd(sink.Buffer);
                await sink.CompleteAsync(cancellationToken).ConfigureAwait(onCaller);
            }
        }

        await destination.FlushAsync(cancellationToken).ConfigureAwait(onCaller);
    }

    private static async Task WritePartAsync(ZipArchive archive, string name, XmlText buffer, Action<XmlText> write,
        CancellationToken cancellationToken)
    {
        buffer.Clear();
        write(buffer);
        byte[] bytes = XmlStreamSink.ToBytes(buffer);
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        var stream = await entry.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        }
    }

    private static void Validate(KumikoXlsxOptions xlsx)
    {
        if (xlsx.SheetName == null) throw new ArgumentException("The sheet name cannot be null.", nameof(xlsx));
        if (!Enum.IsDefined(xlsx.ColumnWidths)) throw new ArgumentException("Unknown column width mode.", nameof(xlsx));
    }
}

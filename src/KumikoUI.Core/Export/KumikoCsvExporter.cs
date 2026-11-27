using System.Globalization;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Export;

/// <summary>
/// Exports a grid as RFC 4180 CSV, streamed row by row through a buffer of about 64 KB: nothing proportional to the
/// output or to a cell table is held in memory. By default it writes the display text the grid shows, UTF-8 with a
/// byte order mark, comma-delimited with <c>\r\n</c> line endings, and protects text that a spreadsheet would run as a
/// formula with a leading apostrophe (<see cref="KumikoFormulaInjection.Prefix"/>); numbers, dates and headers are
/// never altered. Fields are escaped by the same code as the clipboard's tab-separated text.
/// </summary>
public sealed class KumikoCsvExporter : IKumikoExporter
{
    private readonly KumikoCsvOptions _csv;

    /// <summary>Creates an exporter.</summary>
    /// <param name="csv">How to write the file; <c>null</c> uses the defaults.</param>
    public KumikoCsvExporter(KumikoCsvOptions? csv = null)
    {
        _csv = csv ?? new KumikoCsvOptions();
    }

    /// <inheritdoc />
    public string FormatName => "CSV";

    /// <inheritdoc />
    public string FileExtension => ".csv";

    /// <inheritdoc />
    public string MediaType => "text/csv";

    /// <inheritdoc />
    /// <exception cref="ArgumentException">The options are invalid (for example <see cref="KumikoExportRows.Selected"/> without a selection, or a delimiter that is a quote or a line break); nothing is written.</exception>
    public async Task ExportAsync(KumikoSource source, Stream destination, KumikoExportOptions? options = null,
        IProgress<KumikoExportProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanWrite) throw new ArgumentException("The destination stream is not writable.", nameof(destination));
        ValidateCsvOptions(_csv);
        cancellationToken.ThrowIfCancellationRequested();

        // The snapshot reads the source: this runs on the caller's thread, before the first await.
        var snapshot = KumikoExportSnapshot.Create(source, options, KumikoExportValueMode.DisplayText);
        var formatter = new CsvRowFormatter(_csv, Delimiter(_csv, snapshot.Culture));
        var sink = new CsvStreamSink(destination, _csv, canBlock: !snapshot.ReadOnCallerThread, cancellationToken);

        await snapshot.ForEachRowAsync(
            row =>
            {
                formatter.WriteRow(row, sink.Buffer);
                sink.AfterRow();
            },
            sink.FlushAsync, progress, cancellationToken).ConfigureAwait(false);

        formatter.Finish(sink.Buffer);
        await sink.CompleteAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Exports to a string, on the calling thread. The string has no byte order mark.</summary>
    internal string ExportToString(KumikoSource source, KumikoExportOptions? options)
    {
        ArgumentNullException.ThrowIfNull(source);
        ValidateCsvOptions(_csv);
        var snapshot = KumikoExportSnapshot.Create(source, options, KumikoExportValueMode.DisplayText);
        var formatter = new CsvRowFormatter(_csv, Delimiter(_csv, snapshot.Culture));
        var buffer = new CharBuffer();
        foreach (var row in snapshot.Rows) formatter.WriteRow(row, buffer);
        formatter.Finish(buffer);
        return buffer.ToString();
    }

    private static void ValidateCsvOptions(KumikoCsvOptions csv)
    {
        if (csv.Delimiter is '"' or '\r' or '\n')
            throw new ArgumentException("The delimiter cannot be a double quote, a carriage return or a line feed.", nameof(csv));
        if (string.IsNullOrEmpty(csv.LineEnding))
            throw new ArgumentException("The line ending cannot be empty.", nameof(csv));
        if (csv.Encoding == null) throw new ArgumentException("The encoding cannot be null.", nameof(csv));
        if (csv.NullText == null) throw new ArgumentException("The null text cannot be null.", nameof(csv));
    }

    private static char Delimiter(KumikoCsvOptions csv, CultureInfo culture)
    {
        if (!csv.UseCultureListSeparator) return csv.Delimiter;
        string separator = culture.TextInfo.ListSeparator;
        return separator.Length == 1 && separator[0] is not ('"' or '\r' or '\n') ? separator[0] : csv.Delimiter;
    }
}

using KumikoUI.Core.Models;

namespace KumikoUI.Core.Export;

/// <summary>
/// Writes the rows of a <see cref="KumikoSource"/> to a stream in one file format. Implementations build a
/// <see cref="KumikoExportSnapshot"/> and write its row stream, so every format agrees on scope, columns, order,
/// grouping and culture. <see cref="KumikoCsvExporter"/> is the format Core ships.
/// </summary>
public interface IKumikoExporter
{
    /// <summary>A short, human-readable name of the format, such as <c>CSV</c>.</summary>
    string FormatName { get; }

    /// <summary>The file extension of the format, with its dot, such as <c>.csv</c>.</summary>
    string FileExtension { get; }

    /// <summary>The media (MIME) type of the format, such as <c>text/csv</c>.</summary>
    string MediaType { get; }

    /// <summary>
    /// Exports <paramref name="source"/> to <paramref name="destination"/>. Call it on the source's thread (the UI
    /// thread of a grid): the snapshot is taken before the first await, and the awaited work then continues off that
    /// thread unless <see cref="KumikoExportOptions.ReadOnCallerThread"/> is set. The stream is never closed or
    /// disposed; on cancellation it is left with partial content.
    /// </summary>
    /// <param name="source">The data source to export.</param>
    /// <param name="destination">A writable stream; it need not be seekable.</param>
    /// <param name="options">What to export; <c>null</c> exports what the grid shows.</param>
    /// <param name="progress">Receives the rows written so far, throttled, and once at the end.</param>
    /// <param name="cancellationToken">Cancels the export with an <see cref="OperationCanceledException"/>.</param>
    /// <returns>A task that completes when the last byte has been flushed to <paramref name="destination"/>.</returns>
    Task ExportAsync(
        KumikoSource source,
        Stream destination,
        KumikoExportOptions? options = null,
        IProgress<KumikoExportProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

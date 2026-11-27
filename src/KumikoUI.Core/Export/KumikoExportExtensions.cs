using KumikoUI.Core.Models;

namespace KumikoUI.Core.Export;

/// <summary>Export shortcuts for a <see cref="KumikoSource"/>. Call them on the source's thread (a grid's UI thread).</summary>
public static class KumikoExportExtensions
{
    /// <summary>Exports <paramref name="source"/> as CSV (see <see cref="KumikoCsvExporter"/>).</summary>
    /// <param name="source">The data source.</param>
    /// <param name="destination">A writable stream; it is not closed.</param>
    /// <param name="options">What to export; <c>null</c> exports what the grid shows.</param>
    /// <param name="csv">How to write the file; <c>null</c> uses the defaults.</param>
    /// <param name="progress">Receives the progress.</param>
    /// <param name="cancellationToken">Cancels the export.</param>
    /// <returns>A task that completes when the last byte has been flushed.</returns>
    public static Task ExportToCsvAsync(this KumikoSource source, Stream destination, KumikoExportOptions? options = null,
        KumikoCsvOptions? csv = null, IProgress<KumikoExportProgress>? progress = null,
        CancellationToken cancellationToken = default) =>
        new KumikoCsvExporter(csv).ExportAsync(source, destination, options, progress, cancellationToken);

    /// <summary>Exports <paramref name="source"/> as CSV text, synchronously on the calling thread (no byte order mark).</summary>
    /// <param name="source">The data source.</param>
    /// <param name="options">What to export; <c>null</c> exports what the grid shows.</param>
    /// <param name="csv">How to write the file; <c>null</c> uses the defaults.</param>
    /// <returns>The CSV text.</returns>
    public static string ExportToCsvString(this KumikoSource source, KumikoExportOptions? options = null, KumikoCsvOptions? csv = null) =>
        new KumikoCsvExporter(csv).ExportToString(source, options);
}

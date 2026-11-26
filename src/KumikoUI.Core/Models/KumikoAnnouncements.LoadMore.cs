using System.Globalization;
using KumikoUI.Core.Data;

namespace KumikoUI.Core.Models;

/// <summary>The announcements of incremental "load more" loading (#318).</summary>
public static partial class KumikoAnnouncements
{
    /// <summary>
    /// The announcement for a batch that started loading. The hosts announce it at once when the user asked for the
    /// batch (a tap, a key, a retry) and after a second for an automatic batch that is still loading, so fast loads
    /// stay quiet.
    /// </summary>
    /// <param name="strings">The text table.</param>
    /// <exception cref="ArgumentNullException"><paramref name="strings"/> is <c>null</c>.</exception>
    public static string LoadMoreStarted(KumikoStrings strings)
    {
        ArgumentNullException.ThrowIfNull(strings);
        return strings.AnnounceLoadMoreStarted;
    }

    /// <summary>
    /// The announcement for a batch that finished: the error of a failed batch, "All rows loaded." once the source
    /// has no more items, the number of rows added otherwise, or null when a batch added no row and more remain.
    /// </summary>
    /// <param name="strings">The text table.</param>
    /// <param name="e">The completed batch.</param>
    /// <param name="culture">Formats the row count; null uses the current culture.</param>
    /// <exception cref="ArgumentNullException"><paramref name="strings"/> or <paramref name="e"/> is <c>null</c>.</exception>
    public static string? LoadMoreCompleted(KumikoStrings strings, KumikoLoadMoreCompletedEventArgs e, CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(strings);
        ArgumentNullException.ThrowIfNull(e);
        return e.State switch
        {
            KumikoLoadMoreState.Failed => string.IsNullOrEmpty(e.ErrorText)
                ? strings.LoadMoreFailed
                : string.Format(culture ?? CultureInfo.CurrentCulture, strings.AnnounceLoadMoreFailed, e.ErrorText),
            KumikoLoadMoreState.Completed => strings.AnnounceAllRowsLoaded,
            _ when e.RowsAdded > 0 => string.Format(culture ?? CultureInfo.CurrentCulture, strings.AnnounceLoadMoreCompleted, e.RowsAdded),
            _ => null,
        };
    }
}

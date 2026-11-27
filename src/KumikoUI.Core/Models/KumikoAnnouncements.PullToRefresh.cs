namespace KumikoUI.Core.Models;

/// <summary>The announcements of pull-to-refresh (#330).</summary>
public static partial class KumikoAnnouncements
{
    /// <summary>
    /// The announcement for a change of the grid's refreshing state: <see cref="KumikoStrings.AnnounceRefreshing"/> when
    /// a refresh started, <see cref="KumikoStrings.AnnounceRefreshed"/> when it ended. Screen reader users cannot pull,
    /// so this tells them about a refresh that someone else, or the app, started.
    /// </summary>
    /// <param name="strings">The text table.</param>
    /// <param name="isRefreshing">The new state.</param>
    /// <exception cref="ArgumentNullException"><paramref name="strings"/> is <c>null</c>.</exception>
    public static string RefreshStateChanged(KumikoStrings strings, bool isRefreshing)
    {
        ArgumentNullException.ThrowIfNull(strings);
        return isRefreshing ? strings.AnnounceRefreshing : strings.AnnounceRefreshed;
    }
}

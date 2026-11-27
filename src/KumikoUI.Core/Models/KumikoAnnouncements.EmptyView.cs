namespace KumikoUI.Core.Models;

/// <summary>The screen reader text of the empty view (#326).</summary>
public static partial class KumikoAnnouncements
{
    /// <summary>
    /// The text of the empty view for <paramref name="kind"/>: <paramref name="overrideText"/> when it is not null, else
    /// the localized string. It replaces the grid hint while the view has no rows, and is announced once after a filter or
    /// search change empties the view.
    /// </summary>
    /// <param name="strings">The text table.</param>
    /// <param name="kind">Why the view has no rows.</param>
    /// <param name="overrideText">The host's empty view text, which replaces both strings; null uses the table.</param>
    /// <exception cref="ArgumentNullException"><paramref name="strings"/> is <c>null</c>.</exception>
    public static string EmptyView(KumikoStrings strings, KumikoEmptyViewKind kind, string? overrideText = null)
    {
        ArgumentNullException.ThrowIfNull(strings);
        return overrideText ?? (kind == KumikoEmptyViewKind.NoMatches ? strings.EmptyViewNoMatches : strings.EmptyViewNoItems);
    }
}

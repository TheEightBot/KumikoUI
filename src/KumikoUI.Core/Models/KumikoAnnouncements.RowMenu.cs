using System.Globalization;

namespace KumikoUI.Core.Models;

/// <summary>The announcements of the row menu (#331).</summary>
public static partial class KumikoAnnouncements
{
    /// <summary>
    /// The announcement after rows were copied from the row menu: "Row copied" for one, "{0} rows copied" for more.
    /// </summary>
    /// <param name="strings">The text table.</param>
    /// <param name="rowCount">The number of rows copied.</param>
    /// <param name="culture">Formats the row count; null uses the current culture.</param>
    /// <exception cref="ArgumentNullException"><paramref name="strings"/> is <c>null</c>.</exception>
    public static string RowsCopied(KumikoStrings strings, int rowCount, CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(strings);
        return rowCount == 1
            ? strings.AnnounceRowCopied
            : string.Format(culture ?? CultureInfo.CurrentCulture, strings.AnnounceRowsCopied, rowCount);
    }
}

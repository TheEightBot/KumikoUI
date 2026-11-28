using System.Globalization;

namespace KumikoUI.Core.Models;

/// <summary>The announcements of deleting rows (#328).</summary>
public static partial class KumikoAnnouncements
{
    /// <summary>
    /// The announcement after rows were deleted: "Row deleted" for one, "{0} rows deleted" for more.
    /// </summary>
    /// <param name="strings">The text table.</param>
    /// <param name="rowCount">The number of rows deleted.</param>
    /// <param name="culture">Formats the row count; null uses the current culture.</param>
    /// <exception cref="ArgumentNullException"><paramref name="strings"/> is <c>null</c>.</exception>
    public static string RowsDeleted(KumikoStrings strings, int rowCount, CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(strings);
        return rowCount == 1
            ? strings.AnnounceRowDeleted
            : string.Format(culture ?? CultureInfo.CurrentCulture, strings.AnnounceRowsDeleted, rowCount);
    }
}

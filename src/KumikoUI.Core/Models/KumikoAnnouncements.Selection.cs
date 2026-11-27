using System.Globalization;

namespace KumikoUI.Core.Models;

/// <summary>The announcements of the selection column (#321).</summary>
public static partial class KumikoAnnouncements
{
    /// <summary>
    /// The announcement for the check-all box of a selection column after it was toggled: "All {0} rows selected"
    /// when it selected every row, "Selection cleared" when it cleared them.
    /// </summary>
    /// <param name="strings">The text table.</param>
    /// <param name="allSelected"><c>true</c> when the toggle selected every row, <c>false</c> when it cleared the selection.</param>
    /// <param name="rowCount">The number of rows selected; used when <paramref name="allSelected"/>.</param>
    /// <param name="culture">Formats the row count; null uses the current culture.</param>
    /// <exception cref="ArgumentNullException"><paramref name="strings"/> is <c>null</c>.</exception>
    public static string CheckAllToggled(KumikoStrings strings, bool allSelected, int rowCount, CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(strings);
        return allSelected
            ? string.Format(culture ?? CultureInfo.CurrentCulture, strings.AllRowsSelected, rowCount)
            : strings.SelectionCleared;
    }

    /// <summary>
    /// The accessible name and state of a row's box in a selection column: "Select row, checked" or
    /// "Select row, not checked".
    /// </summary>
    /// <param name="strings">The text table.</param>
    /// <param name="isSelected">Whether the row is selected.</param>
    /// <exception cref="ArgumentNullException"><paramref name="strings"/> is <c>null</c>.</exception>
    public static string SelectionBox(KumikoStrings strings, bool isSelected)
    {
        ArgumentNullException.ThrowIfNull(strings);
        return Format("{0}, {1}", strings.SelectRow, isSelected ? strings.CheckStateChecked : strings.CheckStateUnchecked);
    }

    /// <summary>
    /// The accessible name and state of the check-all box in a selection column's header: "Select all rows, checked",
    /// "Select all rows, not checked" or "Select all rows, partially checked".
    /// </summary>
    /// <param name="strings">The text table.</param>
    /// <param name="state"><see cref="KumikoSelectionModel.CheckAllState"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="strings"/> is <c>null</c>.</exception>
    public static string CheckAllBox(KumikoStrings strings, bool? state)
    {
        ArgumentNullException.ThrowIfNull(strings);
        string checkState = !state.HasValue ? strings.CheckStateMixed
            : state.Value ? strings.CheckStateChecked
            : strings.CheckStateUnchecked;
        return Format("{0}, {1}", strings.SelectAllRows, checkState);
    }
}

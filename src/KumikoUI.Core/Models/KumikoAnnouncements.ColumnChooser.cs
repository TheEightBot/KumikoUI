using System.Globalization;

namespace KumikoUI.Core.Models;

/// <summary>The announcements of the column chooser (#335).</summary>
public static partial class KumikoAnnouncements
{
    /// <summary>
    /// The name the column chooser lists a column by: its <see cref="KumikoColumn.Header"/>, else its
    /// <see cref="KumikoColumn.PropertyName"/>, else "Column {n}" (<see cref="KumikoStrings.AnnounceUnnamedColumn"/>).
    /// </summary>
    /// <param name="strings">The text table.</param>
    /// <param name="column">The column.</param>
    /// <param name="columnIndex">The column's zero-based index in <see cref="KumikoSource.Columns"/>, for an unnamed column.</param>
    /// <exception cref="ArgumentNullException"><paramref name="strings"/> or <paramref name="column"/> is <c>null</c>.</exception>
    public static string ColumnLabel(KumikoStrings strings, KumikoColumn column, int columnIndex)
    {
        ArgumentNullException.ThrowIfNull(strings);
        ArgumentNullException.ThrowIfNull(column);
        if (!string.IsNullOrWhiteSpace(column.Header)) return column.Header;
        if (!string.IsNullOrWhiteSpace(column.PropertyName)) return column.PropertyName;
        return Format(strings.AnnounceUnnamedColumn, columnIndex + 1);
    }

    /// <summary>The announcement for a column the chooser showed or hid: "Salary shown", "Salary hidden".</summary>
    /// <param name="strings">The text table.</param>
    /// <param name="columnLabel">The column's name (<see cref="ColumnLabel"/>).</param>
    /// <param name="visible">Whether the column is now visible.</param>
    /// <exception cref="ArgumentNullException"><paramref name="strings"/> is <c>null</c>.</exception>
    public static string ColumnVisibilityChanged(KumikoStrings strings, string columnLabel, bool visible)
    {
        ArgumentNullException.ThrowIfNull(strings);
        return Format(visible ? strings.AnnounceColumnShown : strings.AnnounceColumnHidden, columnLabel ?? string.Empty);
    }

    /// <summary>The announcement for a column the chooser moved: "Salary moved to position 3 of 12".</summary>
    /// <param name="strings">The text table.</param>
    /// <param name="columnLabel">The column's name (<see cref="ColumnLabel"/>).</param>
    /// <param name="position">The column's new one-based position in the chooser's list.</param>
    /// <param name="count">The number of columns the chooser lists.</param>
    /// <exception cref="ArgumentNullException"><paramref name="strings"/> is <c>null</c>.</exception>
    public static string ColumnMoved(KumikoStrings strings, string columnLabel, int position, int count)
    {
        ArgumentNullException.ThrowIfNull(strings);
        return Format(strings.AnnounceColumnMoved, columnLabel ?? string.Empty, position, count);
    }

    /// <summary>
    /// The accessible name, state and position of a column chooser entry: "Salary, checked, 3 of 12", with
    /// "This column cannot be hidden" added when its box is disabled.
    /// </summary>
    /// <param name="strings">The text table.</param>
    /// <param name="columnLabel">The column's name (<see cref="ColumnLabel"/>).</param>
    /// <param name="visible">Whether the column is visible (its box is checked).</param>
    /// <param name="canToggle">Whether the user may change the box.</param>
    /// <param name="position">The entry's one-based position in the list.</param>
    /// <param name="count">The number of entries in the list.</param>
    /// <exception cref="ArgumentNullException"><paramref name="strings"/> is <c>null</c>.</exception>
    public static string ColumnChooserEntry(KumikoStrings strings, string columnLabel, bool visible, bool canToggle,
        int position, int count)
    {
        ArgumentNullException.ThrowIfNull(strings);
        string text = Format("{0}, {1}, {2}", columnLabel ?? string.Empty,
            visible ? strings.CheckStateChecked : strings.CheckStateUnchecked,
            string.Format(CultureInfo.CurrentCulture, strings.ColumnChooserPosition, position, count));
        return canToggle ? text : Format("{0}, {1}", text, strings.ColumnChooserCannotHide);
    }
}

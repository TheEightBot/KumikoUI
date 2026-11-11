using System.Globalization;

namespace KumikoUI.Core.Models;

/// <summary>
/// Builds the texts a host hands to its platform's screen reader: the grid's accessible hint and the
/// announcements for a moved current cell, a sort change and the start and end of an edit. The wording comes
/// from <see cref="KumikoStrings"/>, so it is localized with the rest of the grid's text. The class has no
/// platform dependency; MAUI and Uno hosts share it. Call it only when the state changes, never per frame.
/// </summary>
public static class KumikoAnnouncements
{
    /// <summary>The accessible hint describing the grid's size and, when there is one, its current cell.</summary>
    /// <param name="strings">The text table.</param>
    /// <param name="rowCount">The number of visible rows.</param>
    /// <param name="columnCount">The number of columns.</param>
    /// <param name="currentCell">The current cell; an invalid position leaves it out.</param>
    public static string GridHint(KumikoStrings strings, int rowCount, int columnCount, CellPosition currentCell)
    {
        ArgumentNullException.ThrowIfNull(strings);
        return currentCell.IsValid
            ? Format(strings.AccessibleGridHintWithCell, rowCount, columnCount, currentCell.Row + 1, currentCell.Column + 1)
            : Format(strings.AccessibleGridHint, rowCount, columnCount);
    }

    /// <summary>The announcement for a current cell that moved.</summary>
    /// <param name="strings">The text table.</param>
    /// <param name="column">The cell's column.</param>
    /// <param name="columnIndex">The zero-based column index.</param>
    /// <param name="rowIndex">The zero-based visible row.</param>
    /// <param name="cellText">The cell's display text; null or empty is announced as empty.</param>
    public static string CurrentCell(KumikoStrings strings, KumikoColumn column, int columnIndex, int rowIndex, string? cellText)
    {
        ArgumentNullException.ThrowIfNull(strings);
        return Format(strings.AnnounceCurrentCell, ColumnName(strings, column, columnIndex),
            Text(strings, cellText), rowIndex + 1, columnIndex + 1);
    }

    /// <summary>The announcement for a column whose sort changed, or whose sort was removed.</summary>
    /// <param name="strings">The text table.</param>
    /// <param name="column">The column; its <see cref="KumikoColumn.SortDirection"/> says what happened.</param>
    /// <param name="columnIndex">The zero-based column index, used when the header is empty.</param>
    public static string SortChanged(KumikoStrings strings, KumikoColumn column, int columnIndex)
    {
        ArgumentNullException.ThrowIfNull(strings);
        string name = ColumnName(strings, column, columnIndex);
        return column.SortDirection switch
        {
            KumikoSortDirection.Ascending => Format(strings.AnnounceSortedAscending, name),
            KumikoSortDirection.Descending => Format(strings.AnnounceSortedDescending, name),
            _ => Format(strings.AnnounceSortRemoved, name)
        };
    }

    /// <summary>The announcement for an edit that started.</summary>
    /// <param name="strings">The text table.</param>
    /// <param name="column">The edited column.</param>
    /// <param name="columnIndex">The zero-based column index.</param>
    /// <param name="cellText">The cell's text before the edit.</param>
    public static string EditStarted(KumikoStrings strings, KumikoColumn column, int columnIndex, string? cellText)
    {
        ArgumentNullException.ThrowIfNull(strings);
        return Format(strings.AnnounceEditStarted, ColumnName(strings, column, columnIndex), Text(strings, cellText));
    }

    /// <summary>The announcement for an edit that ended.</summary>
    /// <param name="strings">The text table.</param>
    /// <param name="column">The edited column.</param>
    /// <param name="columnIndex">The zero-based column index.</param>
    /// <param name="committed">Whether the value was written to the item.</param>
    /// <param name="newText">The new text; only used when <paramref name="committed"/>.</param>
    public static string EditEnded(KumikoStrings strings, KumikoColumn column, int columnIndex, bool committed, string? newText)
    {
        ArgumentNullException.ThrowIfNull(strings);
        string name = ColumnName(strings, column, columnIndex);
        return committed
            ? Format(strings.AnnounceEditCommitted, name, Text(strings, newText))
            : Format(strings.AnnounceEditCanceled, name);
    }

    /// <summary>The announcement for an edit that failed validation.</summary>
    /// <param name="strings">The text table.</param>
    /// <param name="column">The edited column.</param>
    /// <param name="columnIndex">The zero-based column index.</param>
    /// <param name="message">The validation message.</param>
    public static string ValidationFailed(KumikoStrings strings, KumikoColumn column, int columnIndex, string? message)
    {
        ArgumentNullException.ThrowIfNull(strings);
        return Format(strings.AnnounceValidationFailed, ColumnName(strings, column, columnIndex), message ?? string.Empty);
    }

    private static string ColumnName(KumikoStrings strings, KumikoColumn column, int columnIndex)
        => string.IsNullOrWhiteSpace(column.Header)
            ? Format(strings.AnnounceUnnamedColumn, columnIndex + 1)
            : column.Header;

    private static string Text(KumikoStrings strings, string? text)
        => string.IsNullOrEmpty(text) ? strings.AnnounceEmptyValue : text;

    private static string Format(string format, params object[] args)
        => string.Format(CultureInfo.CurrentCulture, format, args);
}

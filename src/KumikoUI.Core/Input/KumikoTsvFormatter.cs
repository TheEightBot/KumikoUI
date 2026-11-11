using System.Text;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Input;

/// <summary>
/// Formats grid rows as tab-separated text that spreadsheets paste as a table.
/// </summary>
/// <remarks>
/// The format is Excel's: fields are separated by a tab and every row, the last included, ends with
/// <c>\r\n</c>. A field that contains a tab, a carriage return, a line feed or a double quote is wrapped in
/// double quotes with each embedded quote doubled, so a multi-line or tabbed value stays in its cell.
/// Cells are written as their display text (the column's format applied), hidden columns are skipped and
/// the rest keep their display order.
/// </remarks>
public static class KumikoTsvFormatter
{
    /// <summary>The text that ends each row.</summary>
    public const string RowSeparator = "\r\n";

    /// <summary>Escapes one field for tab-separated output (see the type remarks for the rules).</summary>
    /// <param name="field">The field text; <c>null</c> is an empty field.</param>
    /// <returns>The field, quoted when it needs to be.</returns>
    public static string EscapeField(string? field)
    {
        if (string.IsNullOrEmpty(field)) return string.Empty;
        if (field.AsSpan().IndexOfAny("\t\r\n\"".AsSpan()) < 0) return field;
        return "\"" + field.Replace("\"", "\"\"") + "\"";
    }

    /// <summary>Formats the given visible rows of <paramref name="source"/>.</summary>
    /// <param name="source">The data source the rows belong to.</param>
    /// <param name="rows">Visible row indices, written in ascending order; group rows and rows out of range are skipped.</param>
    /// <param name="includeHeaders">Whether to start with a row of the column headers.</param>
    /// <returns>The tab-separated text, empty when there is nothing to write.</returns>
    public static string Format(KumikoSource source, IEnumerable<int> rows, bool includeHeaders)
    {
        // Columns in the order the grid shows them: left-frozen pane, scrolling columns, right-frozen pane.
        var visible = source.Columns.Where(c => c.IsVisible).ToList();
        var columns = visible.Where(c => c.IsFrozen)
            .Concat(visible.Where(c => !c.IsFrozen && !c.IsFrozenRight))
            .Concat(visible.Where(c => c.IsFrozenRight))
            .ToList();
        if (columns.Count == 0) return string.Empty;

        var ordered = new List<int>(rows);
        ordered.Sort();

        var sb = new StringBuilder();
        if (includeHeaders)
        {
            for (int c = 0; c < columns.Count; c++)
            {
                if (c > 0) sb.Append('\t');
                sb.Append(EscapeField(columns[c].Header));
            }
            sb.Append(RowSeparator);
        }

        int last = -1;
        int written = 0;
        foreach (int row in ordered.Where(r => r >= 0 && r < source.RowCount && !source.IsNonDataRow(r)))
        {
            if (row == last) continue;
            last = row;
            written++;
            for (int c = 0; c < columns.Count; c++)
            {
                if (c > 0) sb.Append('\t');
                sb.Append(EscapeField(source.GetCellDisplayText(row, columns[c])));
            }
            sb.Append(RowSeparator);
        }

        // Headers alone are not a copy
        return written > 0 ? sb.ToString() : string.Empty;
    }
}

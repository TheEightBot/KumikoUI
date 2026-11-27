namespace KumikoUI.Core.Export;

/// <summary>
/// Field escaping shared by the delimited-text formats: the clipboard's tab-separated text
/// (<see cref="Input.KumikoTsvFormatter"/>) and the CSV exporter, so copy and export cannot drift apart.
/// </summary>
internal static class KumikoDelimitedText
{
    /// <summary>
    /// Whether <paramref name="field"/> must be quoted: it contains the delimiter, a double quote, a carriage return
    /// or a line feed (and, with <paramref name="quoteEdgeSpaces"/>, starts or ends with a space).
    /// </summary>
    public static bool NeedsQuoting(ReadOnlySpan<char> field, char delimiter, bool quoteEdgeSpaces = false)
    {
        if (field.IsEmpty) return false;
        foreach (char c in field)
        {
            if (c == delimiter || c == '"' || c == '\r' || c == '\n') return true;
        }
        return quoteEdgeSpaces && (field[0] == ' ' || field[^1] == ' ');
    }

    /// <summary>
    /// Escapes one field: unchanged unless it needs quoting, then wrapped in double quotes with each embedded quote
    /// doubled. <c>null</c> is an empty field.
    /// </summary>
    public static string EscapeField(string? field, char delimiter)
    {
        if (string.IsNullOrEmpty(field)) return string.Empty;
        if (!NeedsQuoting(field, delimiter)) return field;
        return "\"" + field.Replace("\"", "\"\"") + "\"";
    }
}

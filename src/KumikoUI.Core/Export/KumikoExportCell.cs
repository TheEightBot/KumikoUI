namespace KumikoUI.Core.Export;

/// <summary>One cell of an exported row: the value and the text a format writes for it.</summary>
public readonly struct KumikoExportCell
{
    internal KumikoExportCell(object? value, string text)
    {
        Value = value;
        _text = text;
    }

    private readonly string? _text;

    /// <summary>
    /// The cell's value as the column reads it (a number, date, boolean, string and so on), or <c>null</c>. For a
    /// header, group header or title cell it is that text. Typed writers use it; text writers use <see cref="Text"/>.
    /// </summary>
    public object? Value { get; }

    /// <summary>
    /// The cell's text: the display text in the export culture for <see cref="KumikoExportValueMode.DisplayText"/>, or
    /// the invariant, round-trippable text of <see cref="Value"/> for <see cref="KumikoExportValueMode.RawValue"/>.
    /// Never <c>null</c>; empty for a missing value.
    /// </summary>
    public string Text => _text ?? string.Empty;
}

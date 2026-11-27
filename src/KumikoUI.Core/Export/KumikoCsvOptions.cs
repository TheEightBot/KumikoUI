using System.Text;

namespace KumikoUI.Core.Export;

/// <summary>How <see cref="KumikoCsvExporter"/> writes the file (what it writes is <see cref="KumikoExportOptions"/>).</summary>
public sealed record KumikoCsvOptions
{
    /// <summary>The field delimiter. Any single character except a double quote, a carriage return or a line feed. Default: a comma.</summary>
    public char Delimiter { get; init; } = ',';

    /// <summary>
    /// Use the export culture's list separator (a semicolon in de-DE, fr-FR and tr-TR: what Excel in that locale
    /// expects when it opens a <c>.csv</c>) instead of <see cref="Delimiter"/>. A separator that is not a usable
    /// single character falls back to <see cref="Delimiter"/>. Default: <c>false</c>.
    /// </summary>
    public bool UseCultureListSeparator { get; init; }

    /// <summary>The text that ends a row. Default: <c>\r\n</c> (RFC 4180).</summary>
    public string LineEnding { get; init; } = "\r\n";

    /// <summary>Whether the last row ends with <see cref="LineEnding"/> too. Default: <c>true</c>.</summary>
    public bool EndWithLineEnding { get; init; } = true;

    /// <summary>The text encoding. Default: UTF-8.</summary>
    public Encoding Encoding { get; init; } = Encoding.UTF8;

    /// <summary>
    /// Whether the file starts with the encoding's byte order mark (for UTF-8, <c>EF BB BF</c>), which Excel needs to
    /// read non-ASCII text when it opens a <c>.csv</c> directly. Turn it off for strict machine consumers. Default: <c>true</c>.
    /// </summary>
    public bool EmitByteOrderMark { get; init; } = true;

    /// <summary>Whether to quote every field, an empty one as <c>""</c>. Default: <c>false</c>.</summary>
    public bool QuoteAllFields { get; init; }

    /// <summary>How text a spreadsheet would run as a formula is written. Default: <see cref="KumikoFormulaInjection.Prefix"/>.</summary>
    public KumikoFormulaInjection FormulaInjection { get; init; } = KumikoFormulaInjection.Prefix;

    /// <summary>The text written for a <c>null</c> value in a data row. Default: empty.</summary>
    public string NullText { get; init; } = string.Empty;
}

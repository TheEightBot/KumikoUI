using System.Globalization;
using KumikoUI.Core.Export;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Export.Excel.Internal;

/// <summary>The look of a kind of row: its font (bold, color) and fill.</summary>
internal enum XlsxRowStyle { Data = 0, Header = 1, GroupHeader = 2, GroupSummary = 3, TableSummary = 4 }

/// <summary>What a cell holds, which decides its number format.</summary>
internal enum XlsxValueKind { Text = 0, Number = 1, DateTime = 2, Date = 3, Time = 4, Duration = 5, General = 6 }

/// <summary>
/// The style sheet (<c>xl/styles.xml</c>). It has to be written before the sheet, so every cell format a column can need
/// is built from the column list up front: one per kind of row and kind of value, deduplicated.
/// </summary>
internal sealed class XlsxStyles
{
    public const int RowStyles = 5;
    public const int ValueKinds = 7;

    private readonly Dictionary<string, int> _numFmtIds = new(StringComparer.Ordinal);
    private readonly List<(int Id, string Code)> _numFmts = new();
    private readonly List<string> _fonts = new();
    private readonly List<string> _fills = new();
    private readonly Dictionary<(int NumFmt, int Font, int Fill, int Align), int> _xfIds = new();
    private readonly List<(int NumFmt, int Font, int Fill, int Align)> _xfs = new();
    private readonly (int Font, int Fill)[] _rowLooks = new (int, int)[RowStyles];

    public XlsxStyles(KumikoXlsxOptions xlsx)
    {
        _fonts.Add(Font(bold: false, color: null)); // 0: the default font
        _fills.Add("<fill><patternFill patternType=\"none\"/></fill>");
        _fills.Add("<fill><patternFill patternType=\"gray125\"/></fill>"); // fills 0 and 1 are reserved by Excel
        Xf(0, 0, 0, 0); // xf 0: the default cell format

        _rowLooks[(int)XlsxRowStyle.Data] = (0, 0);
        if (xlsx.UseGridColors)
        {
            var style = xlsx.Style ?? new KumikoUI.Core.Models.KumikoStyle();
            _rowLooks[(int)XlsxRowStyle.Header] = Look(style.HeaderTextColor, style.HeaderBackgroundColor);
            _rowLooks[(int)XlsxRowStyle.GroupHeader] = Look(style.GroupHeaderTextColor, style.GroupHeaderBackgroundColor);
            _rowLooks[(int)XlsxRowStyle.GroupSummary] = Look(style.GroupSummaryRowTextColor, style.GroupSummaryRowBackgroundColor);
            _rowLooks[(int)XlsxRowStyle.TableSummary] = Look(style.SummaryRowTextColor, style.SummaryRowBackgroundColor);
        }
        else
        {
            _fonts.Add(Font(bold: true, color: null)); // 1: bold
            for (int r = 1; r < RowStyles; r++) _rowLooks[r] = (1, 0);
        }
    }

    /// <summary>Whether header, group and summary rows have a fill, so their empty cells are written to paint the band.</summary>
    public bool HasRowFills { get; private set; }

    /// <summary>
    /// The cell formats of a column, indexed <c>[rowStyle * ValueKinds + valueKind]</c>; -1 where the column's
    /// <c>Format</c> cannot be expressed for that kind of value (the cell is then written as display text).
    /// </summary>
    public int[] ForColumn(KumikoExportColumn column, CultureInfo culture, bool align, out bool integerOnly)
    {
        string? number = XlsxNumberFormats.ForNumber(column.Format, culture, out integerOnly);
        string? dateTime = XlsxNumberFormats.ForDate(column.Format, "G", culture);
        string? date = XlsxNumberFormats.ForDate(column.Format, "d", culture);
        string? time = XlsxNumberFormats.ForDate(column.Format, "t", culture);
        string? duration = XlsxNumberFormats.ForDuration(column.Format);
        int alignment = align ? column.TextAlignment switch
        {
            KumikoTextAlignment.Center => 1,
            KumikoTextAlignment.Right => 2,
            _ => 0,
        } : 0;

        var xfs = new int[RowStyles * ValueKinds];
        for (int r = 0; r < RowStyles; r++)
        {
            var (font, fill) = _rowLooks[r];
            // Group headers and summary titles are text that runs across the row: never aligned.
            int textAlign = r is (int)XlsxRowStyle.Data or (int)XlsxRowStyle.Header ? alignment : 0;
            int Code(string? code) => code == null ? -1 : Xf(NumFmt(code), font, fill, alignment);
            int at = r * ValueKinds;
            xfs[at + (int)XlsxValueKind.Text] = Xf(0, font, fill, textAlign);
            xfs[at + (int)XlsxValueKind.Number] = Code(number);
            xfs[at + (int)XlsxValueKind.DateTime] = Code(dateTime);
            xfs[at + (int)XlsxValueKind.Date] = Code(date);
            xfs[at + (int)XlsxValueKind.Time] = Code(time);
            xfs[at + (int)XlsxValueKind.Duration] = Code(duration);
            xfs[at + (int)XlsxValueKind.General] = Xf(0, font, fill, alignment);
        }

        return xfs;
    }

    public void Write(XmlText x)
    {
        x.Raw("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n");
        x.Raw("<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
        if (_numFmts.Count > 0)
        {
            x.Raw("<numFmts count=\"").Int(_numFmts.Count).Raw("\">");
            foreach (var (id, code) in _numFmts)
                x.Raw("<numFmt numFmtId=\"").Int(id).Raw("\" formatCode=\"").Attribute(code).Raw("\"/>");
            x.Raw("</numFmts>");
        }

        x.Raw("<fonts count=\"").Int(_fonts.Count).Raw("\">");
        foreach (var font in _fonts) x.Raw(font);
        x.Raw("</fonts>");
        x.Raw("<fills count=\"").Int(_fills.Count).Raw("\">");
        foreach (var fill in _fills) x.Raw(fill);
        x.Raw("</fills>");
        x.Raw("<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>");
        x.Raw("<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>");
        x.Raw("<cellXfs count=\"").Int(_xfs.Count).Raw("\">");
        foreach (var (numFmt, font, fill, align) in _xfs)
        {
            x.Raw("<xf numFmtId=\"").Int(numFmt).Raw("\" fontId=\"").Int(font).Raw("\" fillId=\"").Int(fill)
                .Raw("\" borderId=\"0\" xfId=\"0\"");
            if (numFmt != 0) x.Raw(" applyNumberFormat=\"1\"");
            if (font != 0) x.Raw(" applyFont=\"1\"");
            if (fill != 0) x.Raw(" applyFill=\"1\"");
            if (align == 0)
            {
                x.Raw("/>");
                continue;
            }

            x.Raw(" applyAlignment=\"1\"><alignment horizontal=\"").Raw(align == 1 ? "center" : "right").Raw("\"/></xf>");
        }

        x.Raw("</cellXfs>");
        x.Raw("<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>");
        x.Raw("<dxfs count=\"0\"/><tableStyles count=\"0\"/>");
        x.Raw("</styleSheet>");
    }

    private int NumFmt(string code)
    {
        if (code == XlsxNumberFormats.General) return 0;
        if (_numFmtIds.TryGetValue(code, out int id)) return id;
        id = 164 + _numFmts.Count; // ids below 164 are Excel's built-in formats
        _numFmtIds[code] = id;
        _numFmts.Add((id, code));
        return id;
    }

    private int Xf(int numFmt, int font, int fill, int align)
    {
        var key = (numFmt, font, fill, align);
        if (_xfIds.TryGetValue(key, out int index)) return index;
        index = _xfs.Count;
        _xfIds[key] = index;
        _xfs.Add(key);
        return index;
    }

    private (int Font, int Fill) Look(KumikoColor text, KumikoColor background)
    {
        _fonts.Add(Font(bold: true, color: text.A == 0 ? null : Rgb(text)));
        int font = _fonts.Count - 1;
        if (background.A == 0) return (font, 0);
        _fills.Add("<fill><patternFill patternType=\"solid\"><fgColor rgb=\"" + Rgb(background) + "\"/><bgColor indexed=\"64\"/></patternFill></fill>");
        HasRowFills = true;
        return (font, _fills.Count - 1);
    }

    private static string Rgb(KumikoColor color) =>
        string.Create(CultureInfo.InvariantCulture, $"FF{color.R:X2}{color.G:X2}{color.B:X2}");

    private static string Font(bool bold, string? color) =>
        "<font>" + (bold ? "<b/>" : "") + "<sz val=\"11\"/>" + (color == null ? "" : "<color rgb=\"" + color + "\"/>")
        + "<name val=\"Calibri\"/><family val=\"2\"/></font>";
}

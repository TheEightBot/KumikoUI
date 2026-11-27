using KumikoUI.Core.Export;
using KumikoUI.Core.Models;

namespace KumikoUI.Export.Excel.Internal;

/// <summary>
/// Writes <c>xl/worksheets/sheet1.xml</c> row by row into an <see cref="XmlText"/> buffer: the sheet properties, the
/// frozen pane and the column widths first, then one <c>row</c> per export row, then the autofilter.
/// </summary>
internal sealed class XlsxSheetWriter
{
    private readonly XlsxLayout _layout;
    private readonly KumikoExportSnapshot _snapshot;
    private readonly bool _raw;
    private int _rowNumber;

    public XlsxSheetWriter(XlsxLayout layout, KumikoExportSnapshot snapshot)
    {
        _layout = layout;
        _snapshot = snapshot;
        _raw = snapshot.ValueMode == KumikoExportValueMode.RawValue;
    }

    public void WriteStart(XmlText x)
    {
        var layout = _layout;
        x.Raw("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n");
        x.Raw("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" ")
            .Raw("xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">");

        // The outline buttons sit on the group header row, which comes before the group's rows.
        if (layout.OutlineLevels > 0) x.Raw("<sheetPr><outlinePr summaryBelow=\"0\"/></sheetPr>");
        x.Raw("<dimension ref=\"").Raw(layout.UsedRange).Raw("\"/>");

        x.Raw("<sheetViews><sheetView tabSelected=\"1\" workbookViewId=\"0\">");
        if (layout.FrozenRows > 0 || layout.FrozenColumns > 0)
        {
            string topLeft = layout.ColumnRef(layout.FrozenColumns) + (layout.FrozenRows + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            string pane = layout.FrozenRows > 0 && layout.FrozenColumns > 0 ? "bottomRight"
                : layout.FrozenRows > 0 ? "bottomLeft" : "topRight";
            x.Raw("<pane");
            if (layout.FrozenColumns > 0) x.Raw(" xSplit=\"").Int(layout.FrozenColumns).Raw("\"");
            if (layout.FrozenRows > 0) x.Raw(" ySplit=\"").Int(layout.FrozenRows).Raw("\"");
            x.Raw(" topLeftCell=\"").Raw(topLeft).Raw("\" activePane=\"").Raw(pane).Raw("\" state=\"frozen\"/>");
            x.Raw("<selection pane=\"").Raw(pane).Raw("\" activeCell=\"").Raw(topLeft).Raw("\" sqref=\"").Raw(topLeft).Raw("\"/>");
        }

        x.Raw("</sheetView></sheetViews>");
        x.Raw("<sheetFormatPr defaultRowHeight=\"15\"");
        if (layout.OutlineLevels > 0) x.Raw(" outlineLevelRow=\"").Int(layout.OutlineLevels).Raw("\"");
        x.Raw("/>");

        if (layout.Widths != null)
        {
            x.Raw("<cols>");
            for (int c = 0; c < layout.Widths.Length; c++)
            {
                double width = layout.Widths[c];
                if (width <= 0) continue;
                x.Raw("<col min=\"").Int(c + 1).Raw("\" max=\"").Int(c + 1).Raw("\" width=\"").Number(width).Raw("\" customWidth=\"1\"/>");
            }

            x.Raw("</cols>");
        }

        x.Raw("<sheetData>");
    }

    public void WriteRow(XmlText x, KumikoExportRow row)
    {
        int number = ++_rowNumber;
        int level = Math.Min(row.Level, XlsxLayout.MaxOutlineLevel);
        x.Raw("<row r=\"").Int(number).Raw("\"");
        if (level > 0) x.Raw(" outlineLevel=\"").Int(level).Raw("\"");
        x.Raw(">");

        var rowStyle = row.Kind switch
        {
            KumikoExportRowKind.Header => XlsxRowStyle.Header,
            KumikoExportRowKind.GroupHeader => XlsxRowStyle.GroupHeader,
            KumikoExportRowKind.GroupSummary => XlsxRowStyle.GroupSummary,
            KumikoExportRowKind.TableSummary => XlsxRowStyle.TableSummary,
            _ => XlsxRowStyle.Data,
        };
        int styleBase = (int)rowStyle * XlsxStyles.ValueKinds;
        bool paintEmpty = rowStyle != XlsxRowStyle.Data && _layout.PaintRowBands;

        var cells = row.Cells;
        var columns = _snapshot.Columns;
        for (int c = 0; c < cells.Length; c++)
        {
            var cell = cells[c];
            var xfs = _layout.ColumnXfs[c];
            object? value = cell.Value;
            if (value == null || (value is string s && s.Length == 0))
            {
                if (paintEmpty) Start(x, c, number, xfs[styleBase]).Raw("/>");
                continue;
            }

            if (!_raw || row.Kind is KumikoExportRowKind.Header or KumikoExportRowKind.GroupHeader)
            {
                WriteText(x, c, number, xfs[styleBase], cell.Text);
                continue;
            }

            WriteTyped(x, c, number, value, xfs, styleBase, rowStyle, columns[c]);
        }

        x.Raw("</row>");
    }

    public void WriteEnd(XmlText x)
    {
        x.Raw("</sheetData>");
        if (_layout.AutoFilterRange != null) x.Raw("<autoFilter ref=\"").Raw(_layout.AutoFilterRange).Raw("\"/>");
        x.Raw("<pageMargins left=\"0.7\" right=\"0.7\" top=\"0.75\" bottom=\"0.75\" header=\"0.3\" footer=\"0.3\"/>");
        x.Raw("</worksheet>");
    }

    private void WriteTyped(XmlText x, int c, int number, object value, int[] xfs, int styleBase, XlsxRowStyle rowStyle,
        KumikoExportColumn column)
    {
        bool summary = rowStyle is XlsxRowStyle.GroupSummary or XlsxRowStyle.TableSummary;
        switch (value)
        {
            case string text:
                WriteText(x, c, number, xfs[styleBase], text);
                return;
            case bool b:
                Start(x, c, number, xfs[styleBase + (int)XlsxValueKind.General]).Raw(" t=\"b\"><v>").Raw(b ? "1" : "0").Raw("</v></c>");
                return;
            case DateTime dateTime:
                WriteSerial(x, c, number, XlsxDates.FromDateTime(dateTime), xfs[styleBase + (int)XlsxValueKind.DateTime], value, column);
                return;
            case DateTimeOffset offset:
                // The clock time in the value's own offset: what the grid shows for it.
                WriteSerial(x, c, number, XlsxDates.FromDateTime(offset.DateTime), xfs[styleBase + (int)XlsxValueKind.DateTime], value, column);
                return;
            case DateOnly date:
                WriteSerial(x, c, number, XlsxDates.FromDateTime(date.ToDateTime(TimeOnly.MinValue)), xfs[styleBase + (int)XlsxValueKind.Date], value, column);
                return;
            case TimeOnly time:
                WriteSerial(x, c, number, XlsxDates.TimeFraction(time.Ticks), xfs[styleBase + (int)XlsxValueKind.Time], value, column);
                return;
            case TimeSpan span:
                WriteSerial(x, c, number, span.Ticks >= 0 ? (double)span.Ticks / TimeSpan.TicksPerDay : null,
                    xfs[styleBase + (int)XlsxValueKind.Duration], value, column);
                return;
        }

        // A summary value is the aggregate the grid shows with its own format (a count of a currency column is not
        // currency), so it keeps Excel's General format.
        bool integer;
        switch (value)
        {
            case int or long or short or sbyte or byte or ushort or uint or ulong:
                integer = true;
                break;
            case decimal:
            case double d when double.IsFinite(d):
            case float f when float.IsFinite(f):
            case Half h when Half.IsFinite(h):
                integer = false;
                break;
            default:
                // Text, enums, infinities, NaN and other types: the text the grid shows.
                WriteText(x, c, number, xfs[styleBase], DisplayText(value, column));
                return;
        }

        int xf = xfs[styleBase + (int)(summary ? XlsxValueKind.General : XlsxValueKind.Number)];
        if (xf < 0 || (!summary && !integer && _layout.IntegerOnly[c]))
        {
            WriteText(x, c, number, xfs[styleBase], DisplayText(value, column));
            return;
        }

        Start(x, c, number, xf).Raw("><v>");
        switch (value)
        {
            case int v: x.Int(v); break;
            case long v: x.Int(v); break;
            case short v: x.Int(v); break;
            case sbyte v: x.Int(v); break;
            case byte v: x.Int(v); break;
            case ushort v: x.Int(v); break;
            case uint v: x.Int(v); break;
            case ulong v: x.Number((decimal)v); break;
            case decimal v: x.Number(v); break;
            case double v: x.Number(v); break;
            case float v: x.Number(v); break;
            case Half v: x.Number((float)v); break;
        }

        x.Raw("</v></c>");
    }

    private void WriteSerial(XmlText x, int c, int number, double? serial, int xf, object value, KumikoExportColumn column)
    {
        if (serial is not { } days || xf < 0)
        {
            // Before 1900, a negative duration or a format Excel cannot show: the text the grid shows.
            WriteText(x, c, number, _layout.ColumnXfs[c][0], DisplayText(value, column));
            return;
        }

        Start(x, c, number, xf).Raw("><v>").Number(days).Raw("</v></c>");
    }

    private string DisplayText(object value, KumikoExportColumn column) =>
        column.IsGroupKey ? value.ToString() ?? string.Empty : _snapshot.FormatDisplayText(value, column);

    private void WriteText(XmlText x, int c, int number, int xf, string text)
    {
        if (text.Length == 0) return;

        Start(x, c, number, xf).Raw(" t=\"inlineStr\"><is><t");
        if (XmlText.NeedsPreserve(text)) x.Raw(" xml:space=\"preserve\"");
        x.Raw(">").CellText(text).Raw("</t></is></c>");
    }

    private XmlText Start(XmlText x, int c, int number, int xf)
    {
        x.Raw("<c r=\"").Raw(_layout.ColumnRef(c)).Int(number).Raw("\"");
        if (xf > 0) x.Raw(" s=\"").Int(xf).Raw("\"");
        return x;
    }
}

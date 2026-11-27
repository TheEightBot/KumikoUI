using System.Globalization;
using System.Text;
using KumikoUI.Core.Export;

namespace KumikoUI.Export.Excel.Internal;

/// <summary>
/// Everything about the workbook that is decided before the first byte is written: Excel's limits, the sheet name,
/// the cell formats of every column, the widths, the frozen pane, the autofilter and the outline depth.
/// </summary>
internal sealed class XlsxLayout
{
    /// <summary>The most rows an Excel sheet holds.</summary>
    public const int MaxRows = 1_048_576;

    /// <summary>The most columns an Excel sheet holds (column XFD).</summary>
    public const int MaxColumns = 16_384;

    /// <summary>The deepest row outline Excel supports.</summary>
    public const int MaxOutlineLevel = 7;

    private readonly string[] _columnRefs;

    private XlsxLayout(KumikoExportSnapshot snapshot, KumikoXlsxOptions xlsx, int rowCount, int outlineLevels)
    {
        var columns = snapshot.Columns;
        int columnCount = columns.Count;
        RowCount = rowCount;
        SheetName = SanitizeSheetName(xlsx.SheetName);
        OutlineLevels = Math.Clamp(outlineLevels, 0, MaxOutlineLevel);

        _columnRefs = new string[columnCount + 1];
        for (int c = 0; c <= columnCount; c++) _columnRefs[c] = ColumnName(c);

        string last = _columnRefs[columnCount - 1] + Math.Max(rowCount, 1).ToString(CultureInfo.InvariantCulture);
        UsedRange = rowCount == 0 && columnCount == 1 ? "A1" : "A1:" + last;

        bool headers = snapshot.IncludeHeaders;
        FrozenRows = xlsx.FreezeHeader && headers ? 1 : 0;
        // Freezing every column would leave nothing to scroll (and no cell to the right of the split).
        FrozenColumns = xlsx.FreezeHeader ? Math.Min(snapshot.FrozenColumnCount, columnCount - 1) : 0;
        if (xlsx.AutoFilter && headers)
        {
            AutoFilterRange = "A1:" + last;
            AutoFilterName = "'" + SheetName.Replace("'", "''", StringComparison.Ordinal) + "'!$A$1:$"
                + _columnRefs[columnCount - 1] + "$" + Math.Max(rowCount, 1).ToString(CultureInfo.InvariantCulture);
        }

        Styles = new XlsxStyles(xlsx);
        ColumnXfs = new int[columnCount][];
        IntegerOnly = new bool[columnCount];
        if (xlsx.ColumnWidths == KumikoXlsxColumnWidths.FromGrid) Widths = new double[columnCount];
        for (int c = 0; c < columnCount; c++)
        {
            ColumnXfs[c] = Styles.ForColumn(columns[c], snapshot.Culture, xlsx.ApplyColumnAlignment, out bool integerOnly);
            IntegerOnly[c] = integerOnly;
            if (Widths != null) Widths[c] = ExcelWidth(columns[c].Width);
        }

        PaintRowBands = Styles.HasRowFills;
    }

    public int RowCount { get; }

    public string SheetName { get; }

    public int OutlineLevels { get; }

    public string UsedRange { get; }

    public int FrozenRows { get; }

    public int FrozenColumns { get; }

    public string? AutoFilterRange { get; }

    /// <summary>The range of the autofilter as a workbook reference, for the hidden <c>_xlnm._FilterDatabase</c> name.</summary>
    public string? AutoFilterName { get; }

    public XlsxStyles Styles { get; }

    public int[][] ColumnXfs { get; }

    public bool[] IntegerOnly { get; }

    public double[]? Widths { get; }

    public bool PaintRowBands { get; }

    public string ColumnRef(int column) => _columnRefs[column];

    /// <summary>
    /// Checks Excel's limits and plans the workbook. Throws <see cref="ArgumentException"/> when the export has more
    /// rows or columns than a sheet holds, before anything is written.
    /// </summary>
    public static XlsxLayout Create(KumikoExportSnapshot snapshot, KumikoXlsxOptions xlsx, int outlineLevels)
    {
        int columns = snapshot.Columns.Count;
        if (columns > MaxColumns)
        {
            throw new ArgumentException(string.Create(CultureInfo.InvariantCulture,
                $"The export has {columns} columns; an Excel sheet holds at most {MaxColumns}."));
        }

        long rows = (long)snapshot.TotalRows + (snapshot.IncludeHeaders ? 1 : 0);
        if (rows > MaxRows)
        {
            throw new ArgumentException(string.Create(CultureInfo.InvariantCulture,
                $"The export has {rows} rows (header and summaries included); an Excel sheet holds at most {MaxRows}. Export fewer rows, for example a filtered or selected set."));
        }

        return new XlsxLayout(snapshot, xlsx, (int)rows, outlineLevels);
    }

    /// <summary>The column letters of a zero-based column index: 0 is A, 25 is Z, 26 is AA, 16383 is XFD.</summary>
    public static string ColumnName(int column)
    {
        Span<char> chars = stackalloc char[4];
        int at = chars.Length;
        int n = column + 1;
        while (n > 0)
        {
            int rem = (n - 1) % 26;
            chars[--at] = (char)('A' + rem);
            n = (n - 1) / 26;
        }

        return new string(chars[at..]);
    }

    /// <summary>
    /// Device-independent pixels as Excel's column width in characters of the default font (Calibri 11, a maximum digit
    /// width of 7 pixels): what Excel itself stores for a column of that many pixels. Zero (no width written) for a
    /// width that is not a positive number.
    /// </summary>
    public static double ExcelWidth(float pixels)
    {
        if (!float.IsFinite(pixels) || pixels <= 0) return 0;
        return Math.Clamp(Math.Truncate(pixels / 7.0 * 256) / 256, 1, 255);
    }

    /// <summary>
    /// A sheet name Excel accepts: without <c>[ ] : * ? / \</c> and characters XML cannot hold, without a leading or
    /// trailing apostrophe, at most 31 characters (never cutting a surrogate pair), not the reserved "History", and
    /// "Sheet1" when nothing is left.
    /// </summary>
    public static string SanitizeSheetName(string? name)
    {
        var sb = new StringBuilder(name?.Length ?? 0);
        if (name != null)
        {
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (char.IsHighSurrogate(c) && i + 1 < name.Length && char.IsLowSurrogate(name[i + 1]))
                {
                    sb.Append(c).Append(name[++i]);
                    continue;
                }

                if (c is '[' or ']' or ':' or '*' or '?' or '/' or '\\' || char.IsControl(c) || char.IsSurrogate(c)
                    || c is '￾' or '￿')
                {
                    continue;
                }

                sb.Append(c);
            }
        }

        string result = sb.ToString().Trim('\'');
        if (result.Length > 31)
        {
            int cut = char.IsHighSurrogate(result[30]) ? 30 : 31;
            result = result[..cut].TrimEnd('\'');
        }

        if (string.Equals(result, "History", StringComparison.OrdinalIgnoreCase)) result += "_";
        return result.Trim().Length == 0 ? "Sheet1" : result;
    }
}

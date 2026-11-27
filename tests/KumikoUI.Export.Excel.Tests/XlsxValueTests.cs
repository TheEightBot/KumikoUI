using System.Globalization;
using KumikoUI.Core.Export;
using KumikoUI.Export.Excel.Internal;
using static KumikoUI.Export.Excel.Tests.Xlsx;

namespace KumikoUI.Export.Excel.Tests;

public class XlsxValueTests
{
    private enum Shade { Light, Dark }

    [Theory]
    [InlineData(1900, 1, 1, 1.0)]
    [InlineData(1900, 2, 28, 59.0)]
    [InlineData(1900, 3, 1, 61.0)] // Excel's fictitious 1900-02-29 is serial 60
    [InlineData(1904, 1, 1, 1462.0)]
    [InlineData(2020, 1, 15, 43845.0)]
    [InlineData(9999, 12, 31, 2958465.0)]
    public void Serials_FollowExcels1900DateSystem(int year, int month, int day, double serial) =>
        Assert.Equal(serial, XlsxDates.FromDateTime(new DateTime(year, month, day)));

    [Fact]
    public void Serials_CarryTheTimeAsAFractionAndStopBefore1900()
    {
        Assert.Equal(43845.75, XlsxDates.FromDateTime(new DateTime(2020, 1, 15, 18, 0, 0)));
        Assert.Equal(1.5, XlsxDates.FromDateTime(new DateTime(1900, 1, 1, 12, 0, 0)));
        Assert.Null(XlsxDates.FromDateTime(new DateTime(1899, 12, 31)));
        Assert.Null(XlsxDates.FromDateTime(DateTime.MinValue));
        Assert.True(XlsxDates.FromDateTime(DateTime.MaxValue) < 2958466.0);
    }

    [Fact]
    public async Task EveryKindOfValue_IsTypedAndValidates()
    {
        var source = Values(new (string, object?, string?)[]
        {
            ("int", 42, null),
            ("long", 9_007_199_254_740_993L, null),
            ("double", 0.1 + 0.2, null),
            ("float", 0.1f, null),
            ("decimal", 1234.5678m, "N2"),
            ("ulong", ulong.MaxValue, null),
            ("byte", (byte)7, null),
            ("bool", false, null),
            ("date", new DateTime(2020, 1, 15, 13, 45, 0), "yyyy-MM-dd HH:mm"),
            ("offset", new DateTimeOffset(2020, 1, 15, 23, 30, 0, TimeSpan.FromHours(-5)), "yyyy-MM-dd HH:mm"),
            ("dateonly", new DateOnly(2021, 3, 9), null),
            ("timeonly", new TimeOnly(12, 0), null),
            ("span", TimeSpan.FromHours(30), null),
            ("enum", Shade.Dark, null),
            ("null", null, null),
            ("guid", new Guid("6f9619ff-8b86-d011-b42d-00cf4fc964ff"), null),
        });

        var bytes = await ExportAsync(source);
        AssertValid(bytes);
        var cells = Cells(bytes);

        Assert.Equal("42", cells["A2"].Value);
        Assert.Equal("9007199254740993", cells["B2"].Value);
        Assert.Equal(0.1 + 0.2, double.Parse(cells["C2"].Value!, CultureInfo.InvariantCulture));
        Assert.Equal("0.1", cells["D2"].Value);
        Assert.Equal("1234.5678", cells["E2"].Value);
        Assert.Equal("#,##0.00", NumberFormat(bytes, cells["E2"].Style));
        Assert.Equal("18446744073709551615", cells["F2"].Value);
        Assert.Equal("7", cells["G2"].Value);
        Assert.Equal(("b", "0"), (cells["H2"].Type, cells["H2"].Value));

        Assert.Equal(new DateTime(2020, 1, 15, 13, 45, 0), DateTime.FromOADate(double.Parse(cells["I2"].Value!, CultureInfo.InvariantCulture)));
        Assert.Equal("yyyy\"-\"mm\"-\"dd\" \"hh\":\"mm", NumberFormat(bytes, cells["I2"].Style));
        // A DateTimeOffset keeps its clock time, as the grid shows it.
        Assert.Equal(new DateTime(2020, 1, 15, 23, 30, 0), DateTime.FromOADate(double.Parse(cells["J2"].Value!, CultureInfo.InvariantCulture)));
        Assert.Equal("44264", cells["K2"].Value);
        Assert.Equal("MM\"/\"dd\"/\"yyyy", NumberFormat(bytes, cells["K2"].Style).Replace("mm", "MM", StringComparison.Ordinal));
        Assert.Equal("0.5", cells["L2"].Value);
        Assert.Equal("hh\":\"mm", NumberFormat(bytes, cells["L2"].Style));
        Assert.Equal("1.25", cells["M2"].Value);
        Assert.Equal("[h]:mm:ss", NumberFormat(bytes, cells["M2"].Style));

        Assert.Equal(("inlineStr", "Dark"), (cells["N2"].Type, cells["N2"].Text));
        Assert.False(cells.ContainsKey("O2")); // null writes no cell
        Assert.Equal("6f9619ff-8b86-d011-b42d-00cf4fc964ff", cells["P2"].Text);
    }

    [Fact]
    public async Task ValuesExcelCannotHold_AreWrittenAsTheGridText()
    {
        var source = Values(new (string, object?, string?)[]
        {
            ("old", new DateTime(1899, 12, 31), "yyyy-MM-dd"),
            ("negative span", TimeSpan.FromMinutes(-90), null),
            ("nan", double.NaN, null),
            ("infinity", double.PositiveInfinity, null),
            ("hex", 255, "X4"),
            ("per mille", 0.5, "0.0‰"),
            ("era", new DateTime(2020, 1, 1), "yyyy gg"),
        });

        var bytes = await ExportAsync(source);
        AssertValid(bytes);
        var cells = Cells(bytes);
        Assert.Equal(("inlineStr", "1899-12-31"), (cells["A2"].Type, cells["A2"].Text));
        Assert.Equal("-01:30:00", cells["B2"].Text);
        Assert.Equal("NaN", cells["C2"].Text);
        Assert.Equal("Infinity", cells["D2"].Text);
        Assert.Equal("00FF", cells["E2"].Text);
        Assert.Equal("500.0‰", cells["F2"].Text);
        Assert.Equal("2020 A.D.", cells["G2"].Text);
    }

    [Fact]
    public async Task Text_IsNeverAFormulaAndKeepsItsSpaces()
    {
        var source = Values(new (string, object?, string?)[]
        {
            ("formula", "=HYPERLINK(\"http://x\")", null),
            ("spaces", "  padded  ", null),
            ("markup", "<b>&amp;</b>", null),
            ("lines", "one\r\ntwo\nthree", null),
            ("escape", "_x0041_ stays", null),
            ("emoji", "smile 😀", null),
        });

        var bytes = await ExportAsync(source);
        AssertValid(bytes);
        var sheet = Sheet(bytes);
        Assert.Empty(sheet.Descendants(Ns + "f"));
        var cells = Cells(bytes);
        Assert.Equal(("inlineStr", "=HYPERLINK(\"http://x\")"), (cells["A2"].Type, cells["A2"].Text));
        Assert.Equal("  padded  ", cells["B2"].Text);
        var t = sheet.Descendants(Ns + "c").Single(c => (string?)c.Attribute("r") == "B2").Descendants(Ns + "t").Single();
        Assert.Equal("preserve", (string?)t.Attribute(System.Xml.Linq.XNamespace.Xml + "space"));
        Assert.Equal("<b>&amp;</b>", cells["C2"].Text);
        Assert.Equal("one\r\ntwo\nthree", cells["D2"].Text);
        Assert.Equal("_x005F_x0041_ stays", cells["E2"].Text); // Excel decodes it back to "_x0041_ stays"
        Assert.Equal("smile 😀", cells["F2"].Text);
    }

    [Fact]
    public async Task IllegalXmlCharacters_AreStripped()
    {
        var source = Values(new (string, object?, string?)[]
        {
            ("control", "a\u0001b\u0008c\u001Fd", null),
            ("surrogate", "x\uD800y\uDC00z", null),
            ("nonchar", "p￾q￿r", null),
        });
        var bytes = await ExportAsync(source, xlsx: new KumikoXlsxOptions { Title = "t\u0002itle" });
        AssertValid(bytes);
        var cells = Cells(bytes);
        Assert.Equal("abcd", cells["A2"].Text);
        Assert.Equal("xyz", cells["B2"].Text);
        Assert.Equal("pqr", cells["C2"].Text);
    }

    [Fact]
    public async Task LongText_IsCutToExcelsCellLimit()
    {
        string longText = new string('a', 32_766) + "😀" + new string('b', 100);
        var bytes = await ExportAsync(Values(new (string, object?, string?)[] { ("long", longText, null), ("exact", new string('c', 32_767), null) }));
        AssertValid(bytes);
        var cells = Cells(bytes);
        Assert.Equal(new string('a', 32_766), cells["A2"].Text); // the emoji would pass the limit: not split
        Assert.Equal(32_767, cells["B2"].Text!.Length);
    }

    [Fact]
    public async Task CultureFormats_MapToExcelCodes()
    {
        var de = CultureInfo.GetCultureInfo("de-DE");
        var source = Values(new (string, object?, string?)[]
        {
            ("currency", -1234.5m, "C"),
            ("percent", 0.256, "P1"),
            ("date", new DateTime(2020, 1, 15), "D"),
        }, de);
        var bytes = await ExportAsync(source);
        AssertValid(bytes);
        var cells = Cells(bytes);
        Assert.Equal("-1234.5", cells["A2"].Value);
        Assert.Equal("#,##0.00\" €\";\"-\"#,##0.00\" €\"", NumberFormat(bytes, cells["A2"].Style));
        Assert.Equal("#,##0.0\" \"%;\"-\"#,##0.0\" \"%", NumberFormat(bytes, cells["B2"].Style));
        Assert.Equal("[$-407]dddd\", \"d\". \"mmmm\" \"yyyy", NumberFormat(bytes, cells["C2"].Style));
    }

    [Fact]
    public async Task ManyNumberFormats_ValidateTogether()
    {
        var formats = new[]
        {
            "N0", "N1", "N2", "N3", "N4", "C", "C0", "C2", "P", "P0", "P2", "D", "D5", "E", "e3", "F", "F3", "G", "R",
            "0.00", "#,##0", "#,##0.00;(#,##0.00)", "0%", "0.0%", "000000", "#,##0,,", "'$'#,##0", "\\#0", "0.00E+00",
        };
        var dateFormats = new[] { "yyyy-MM-dd", "HH:mm", "HH:mm:ss.fff", "d", "D", "f", "F", "g", "G", "t", "T", "M", "Y", "s", "dd MMM yyyy", "h:mm tt" };
        var cells = formats.Select(f => (f, f.StartsWith('D') ? (object?)123 : -1234.5678, (string?)f))
            .Concat(dateFormats.Select(f => ("date " + f, (object?)new DateTime(2024, 2, 29, 14, 5, 9, 250), (string?)f)))
            .ToList();
        var bytes = await ExportAsync(Values(cells, CultureInfo.GetCultureInfo("en-US")));
        AssertValid(bytes);

        // Every one of them maps to a number format (none falls back to text).
        Assert.All(Cells(bytes).Values.Where(c => c.Ref.EndsWith("2", StringComparison.Ordinal) && c.Ref.Length <= 3), c => Assert.Null(c.Type));
    }
}

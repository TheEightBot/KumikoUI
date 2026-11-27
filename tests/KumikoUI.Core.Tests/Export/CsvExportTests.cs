using System.Globalization;
using System.Text;
using KumikoUI.Core.Export;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using static KumikoUI.Core.Tests.Export.ExportTests;

namespace KumikoUI.Core.Tests.Export;

public class CsvExportTests
{
    private static readonly string[] AllRows =
    {
        "Name,Dept,Salary,Hired,Active",
        "Ada,Eng,\"1,200.50\",2020-01-15,True",
        "Bob,Ops,900.00,2019-06-01,False",
        "Cy,Eng,\"1,500.00\",2021-03-09,True",
        "Di,Ops,700.25,2018-12-31,True",
        "Ed,Eng,800.00,2022-08-20,False",
    };

    [Fact]
    public void ExportToCsvString_WritesHeadersAndDisplayText()
    {
        Assert.Equal(Text(AllRows), Source().ExportToCsvString());
    }

    [Fact]
    public async Task ExportToCsvAsync_StartsWithBomAndWritesTheSameText()
    {
        using var stream = new MemoryStream();
        await Source().ExportToCsvAsync(stream);
        var bytes = stream.ToArray();
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        Assert.Equal(Text(AllRows), Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));
    }

    [Fact]
    public async Task ByteOrderMarkCanBeTurnedOff()
    {
        using var stream = new MemoryStream();
        await Source().ExportToCsvAsync(stream, csv: new KumikoCsvOptions { EmitByteOrderMark = false });
        Assert.Equal((byte)'N', stream.ToArray()[0]);
    }

    [Fact]
    public async Task TheDestinationIsNotDisposedAndNeedNotBeSeekable()
    {
        var stream = new CountingStream();
        await Source().ExportToCsvAsync(stream);
        Assert.False(stream.Disposed);
        Assert.True(stream.BytesWritten > 50);
    }

    [Fact]
    public async Task TheDestinationIsWrittenAfterTheCurrentPosition_NotRewound()
    {
        using var stream = new MemoryStream();
        stream.Write(new byte[] { 1, 2, 3 });
        await Source().ExportToCsvAsync(stream, csv: new KumikoCsvOptions { EmitByteOrderMark = false });
        Assert.Equal(new byte[] { 1, 2, 3, (byte)'N' }, stream.ToArray()[..4]);
    }

    [Fact]
    public void Quoting_FollowsRfc4180()
    {
        var items = new[]
        {
            new Person { Name = "a,b", Dept = "say \"hi\"" },
            new Person { Name = "line1\r\nline2", Dept = "lf\nonly" },
            new Person { Name = " lead", Dept = "trail " },
            new Person { Name = "", Dept = "café \U0001F600" },
        };
        var source = Source(items);
        string csv = source.ExportToCsvString(new KumikoExportOptions { Columns = new[] { Col(source, "Name"), Col(source, "Dept") } });
        Assert.Equal(Text(new[]
        {
            "Name,Dept",
            "\"a,b\",\"say \"\"hi\"\"\"",
            "\"line1\r\nline2\",\"lf\nonly\"",
            "\" lead\",\"trail \"",
            ",café \U0001F600",
        }), csv);
    }

    [Fact]
    public void QuoteAllFields_QuotesEmptyFieldsToo()
    {
        var source = Source(new[] { new Person { Name = "x" } });
        string csv = source.ExportToCsvString(new KumikoExportOptions { Columns = new[] { Col(source, "Name"), Col(source, "Dept") } },
            new KumikoCsvOptions { QuoteAllFields = true });
        Assert.Equal("\"Name\",\"Dept\"\r\n\"x\",\"\"\r\n", csv);
    }

    [Fact]
    public void LineEnding_EndWithLineEnding_AndNullText()
    {
        var source = Source(new[] { new Person { Name = "x" } });
        source.SetColumns(new[] { new KumikoColumn { Header = "N", ValueAccessor = i => ((Person)i).Name }, new KumikoColumn { Header = "Z", ValueAccessor = _ => null } });
        string csv = source.ExportToCsvString(csv: new KumikoCsvOptions { LineEnding = "\n", EndWithLineEnding = false, NullText = "NULL" });
        Assert.Equal("N,Z\nx,NULL", csv);
    }

    [Fact]
    public void RandomData_RoundTripsThroughAnRfc4180Reader()
    {
        var random = new Random(12345);
        const string alphabet = "ab ,\"\r\n\t;=+-@é\U0001F600xyz'";
        var items = Enumerable.Range(0, 400).Select(_ => new Person
        {
            Name = new string(Enumerable.Range(0, random.Next(0, 12)).Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray()),
            Dept = new string(Enumerable.Range(0, random.Next(0, 6)).Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray()),
        }).ToList();
        // Surrogate halves would not survive: draw whole emoji only.
        items.ForEach(p => { p.Name = p.Name.Replace("\U0001F600", "e"); p.Dept = p.Dept.Replace("\U0001F600", "e"); });
        var source = Source(items);
        var columns = new[] { Col(source, "Name"), Col(source, "Dept") };

        string csv = source.ExportToCsvString(new KumikoExportOptions { Columns = columns }, new KumikoCsvOptions { FormulaInjection = KumikoFormulaInjection.None });
        var parsed = Rfc4180.Parse(csv);

        Assert.Equal(items.Count + 1, parsed.Count);
        for (int i = 0; i < items.Count; i++)
        {
            Assert.Equal(items[i].Name, parsed[i + 1][0]);
            Assert.Equal(items[i].Dept, parsed[i + 1][1]);
        }
    }

    [Fact]
    public void EscapeParityWithTsv_ForGeneratedInputs()
    {
        var random = new Random(7);
        const string alphabet = "ab \t\r\n\",;'";
        for (int i = 0; i < 2000; i++)
        {
            string s = new string(Enumerable.Range(0, random.Next(0, 8)).Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray());
            Assert.Equal(KumikoDelimitedText.EscapeField(s, '\t'), KumikoTsvFormatter.EscapeField(s));
        }
        Assert.Equal(string.Empty, KumikoTsvFormatter.EscapeField(null));
        Assert.Equal("\"a\tb\"", KumikoTsvFormatter.EscapeField("a\tb"));
        Assert.Equal("a,b", KumikoTsvFormatter.EscapeField("a,b"));
    }

    [Theory]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("+49 171", "'+49 171")]
    [InlineData("-5", "'-5")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("\tx", "'\tx")]
    [InlineData("safe=1", "safe=1")]
    public void FormulaInjection_PrefixesStringsThatStartLikeFormulas(string name, string expected)
    {
        var source = Source(new[] { new Person { Name = name } });
        string csv = source.ExportToCsvString(new KumikoExportOptions { Columns = new[] { Col(source, "Name") } });
        Assert.Equal("Name\r\n" + expected + "\r\n", csv);

        string plain = source.ExportToCsvString(new KumikoExportOptions { Columns = new[] { Col(source, "Name") } },
            new KumikoCsvOptions { FormulaInjection = KumikoFormulaInjection.None });
        Assert.Equal("Name\r\n" + name + "\r\n", plain);
    }

    [Fact]
    public void FormulaInjection_NeverAltersNumbersDatesBooleansOrHeaders()
    {
        var source = new KumikoSource { Culture = CultureInfo.InvariantCulture };
        source.SetColumns(new[]
        {
            new KumikoColumn { Header = "-Header", ValueAccessor = i => ((Person)i).Salary, Format = "N0" },
            new KumikoColumn { Header = "=Neg", ValueAccessor = _ => -5 },
            new KumikoColumn { Header = "Plus", ValueAccessor = _ => 5.5m, Format = "+0.0;-0.0" },
        });
        source.SetItems(new[] { new Person { Salary = -1234 } });
        Assert.Equal("-Header,=Neg,Plus\r\n\"-1,234\",-5,+5.5\r\n", source.ExportToCsvString());
    }

    [Fact]
    public void FormulaInjection_AlsoProtectsGroupKeyTextAndSummaryTitles()
    {
        var items = new[] { new Person { Name = "x", Dept = "=cmd", Salary = 1 } };
        var source = Source(items);
        source.AddGroupDescription(new GroupDescription("Dept"));
        source.AddGroupSummaryRow(new GroupSummaryRow("g") { Title = "@Total", Columns = { new SummaryColumnDescription("Dept", SummaryType.Count) } });
        var columns = new[] { Col(source, "Name"), Col(source, "Salary") };

        var keyed = Rfc4180.Parse(source.ExportToCsvString(new KumikoExportOptions { Columns = columns, GroupLayout = KumikoExportGroupLayout.Columns }));
        Assert.Equal("'=cmd", keyed[1][0]);

        var rows = Rfc4180.Parse(source.ExportToCsvString(new KumikoExportOptions { Columns = columns }));
        Assert.Equal("'@Total", rows[^1][0]);
    }

    [Fact]
    public void UseCultureListSeparator_UsesTheCulturesSeparator()
    {
        var source = Source();
        string csv = source.ExportToCsvString(new KumikoExportOptions { Culture = new CultureInfo("de-DE"), Columns = new[] { Col(source, "Name"), Col(source, "Salary") } },
            new KumikoCsvOptions { UseCultureListSeparator = true });
        Assert.StartsWith("Name;Salary\r\nAda;1.200,50\r\n", csv);
    }

    [Theory]
    [InlineData("de-DE", "1.200,50", "15.01.2020")]
    [InlineData("fr-FR", "1 200,50", "15/01/2020")]
    [InlineData("tr-TR", "1.200,50", "15.01.2020")]
    [InlineData("en-US", "1,200.50", "1/15/2020")]
    public void Culture_DrivesDisplayText(string culture, string salary, string date)
    {
        var source = Source();
        Col(source, "Hired").Format = "d";
        var options = new KumikoExportOptions { Culture = new CultureInfo(culture), Columns = new[] { Col(source, "Salary"), Col(source, "Hired") } };
        string csv = source.ExportToCsvString(options, new KumikoCsvOptions { Delimiter = '|' });
        var row = Rfc4180.Parse(csv, '|')[1];
        Assert.Equal(salary.Normalize(), row[0].Replace(' ', ' '));
        Assert.Equal(date, row[1]);
    }

    [Fact]
    public void Culture_DefaultsToTheSourceCulture_NotTheThreadCulture()
    {
        var source = Source();
        source.Culture = new CultureInfo("de-DE");
        var options = new KumikoExportOptions { Columns = new[] { Col(source, "Salary") } };
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            Assert.Equal("1.200,50", Rfc4180.Parse(source.ExportToCsvString(options))[1][0]);
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [Fact]
    public void RawValueMode_WritesInvariantRoundTrippableText()
    {
        var source = Source();
        source.Culture = new CultureInfo("de-DE");
        var options = new KumikoExportOptions { ValueMode = KumikoExportValueMode.RawValue };
        var rows = Rfc4180.Parse(source.ExportToCsvString(options));
        Assert.Equal(new[] { "Ada", "Eng", "1200.5", "2020-01-15T00:00:00", "true" }, rows[1]);
    }
}

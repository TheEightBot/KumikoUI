using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using KumikoUI.Core.Export;
using KumikoUI.Core.Models;

namespace KumikoUI.Export.Excel.Tests;

/// <summary>An item the Excel tests export.</summary>
internal sealed class Person
{
    public string Name { get; set; } = "";
    public string Dept { get; set; } = "";
    public decimal Salary { get; set; }
    public DateTime Hired { get; set; }
    public bool Active { get; set; }
}

/// <summary>A write-only stream that is not seekable (a network or response stream); it keeps what it was given.</summary>
internal sealed class ForwardOnlyStream : Stream
{
    private readonly MemoryStream _inner = new();
    public bool Keep { get; init; } = true;
    public long BytesWritten { get; private set; }
    public bool Disposed { get; private set; }
    public byte[] ToArray() => _inner.ToArray();
    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count)
    {
        BytesWritten += count;
        if (Keep) _inner.Write(buffer, offset, count);
    }

    protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
}

/// <summary>One cell of a sheet as written: its reference, type, style index and value or inline text.</summary>
internal sealed record XCell(string Ref, string? Type, int Style, string? Value, string? Text);

internal static class Xlsx
{
    public static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    public static List<Person> People() => new()
    {
        new Person { Name = "Ada", Dept = "Eng", Salary = 1200.5m, Hired = new DateTime(2020, 1, 15), Active = true },
        new Person { Name = "Bob", Dept = "Ops", Salary = 900m, Hired = new DateTime(2019, 6, 1), Active = false },
        new Person { Name = "Cy", Dept = "Eng", Salary = 1500m, Hired = new DateTime(2021, 3, 9), Active = true },
        new Person { Name = "Di", Dept = "Ops", Salary = 700.25m, Hired = new DateTime(2018, 12, 31), Active = true },
        new Person { Name = "Ed", Dept = "Eng", Salary = 800m, Hired = new DateTime(2022, 8, 20), Active = false },
    };

    public static KumikoSource Source(IEnumerable<Person>? items = null)
    {
        var source = new KumikoSource { Culture = CultureInfo.InvariantCulture };
        source.SetColumns(new[]
        {
            new KumikoColumn { PropertyName = nameof(Person.Name), Header = "Name", Width = 140 },
            new KumikoColumn { PropertyName = nameof(Person.Dept), Header = "Dept" },
            new KumikoColumn
            {
                PropertyName = nameof(Person.Salary), Header = "Salary", ColumnType = KumikoColumnType.Numeric, Format = "N2",
                TextAlignment = KumikoUI.Core.Rendering.KumikoTextAlignment.Right,
            },
            new KumikoColumn { PropertyName = nameof(Person.Hired), Header = "Hired", ColumnType = KumikoColumnType.Date, Format = "yyyy-MM-dd" },
            new KumikoColumn { PropertyName = nameof(Person.Active), Header = "Active", ColumnType = KumikoColumnType.Boolean },
        });
        source.SetItems((items ?? People()).ToList());
        return source;
    }

    /// <summary>A source with one column per value, each read through a delegate.</summary>
    public static KumikoSource Values(IReadOnlyList<(string Header, object? Value, string? Format)> cells, CultureInfo? culture = null)
    {
        var source = new KumikoSource { Culture = culture ?? CultureInfo.InvariantCulture };
        source.SetColumns(cells.Select(c => new KumikoColumn { Header = c.Header, Format = c.Format, ValueAccessor = _ => c.Value }).ToList());
        source.SetItems(new List<object> { new() });
        return source;
    }

    public static async Task<byte[]> ExportAsync(KumikoSource source, KumikoExportOptions? options = null, KumikoXlsxOptions? xlsx = null)
    {
        using var stream = new MemoryStream();
        await source.ExportToExcelAsync(stream, options, xlsx);
        return stream.ToArray();
    }

    /// <summary>Validates the workbook with the Open XML SDK and fails with the list of errors.</summary>
    public static void AssertValid(byte[] workbook)
    {
        using var stream = new MemoryStream(workbook);
        using var document = SpreadsheetDocument.Open(stream, false);
        var errors = new OpenXmlValidator(FileFormatVersions.Microsoft365).Validate(document)
            .Select(e => $"{e.Part?.Uri} {e.Path?.XPath}: {e.Description}").Take(20).ToList();
        Assert.True(errors.Count == 0, "Open XML validation errors:\n" + string.Join("\n", errors));
    }

    public static XDocument Part(byte[] workbook, string name)
    {
        using var zip = new ZipArchive(new MemoryStream(workbook), ZipArchiveMode.Read);
        var entry = zip.GetEntry(name) ?? throw new Xunit.Sdk.XunitException($"No part {name}");
        using var stream = entry.Open();
        return XDocument.Load(stream);
    }

    public static string[] PartNames(byte[] workbook)
    {
        using var zip = new ZipArchive(new MemoryStream(workbook), ZipArchiveMode.Read);
        return zip.Entries.Select(e => e.FullName).ToArray();
    }

    public static XDocument Sheet(byte[] workbook) => Part(workbook, "xl/worksheets/sheet1.xml");

    public static Dictionary<string, XCell> Cells(byte[] workbook) =>
        Sheet(workbook).Descendants(Ns + "c").Select(c => new XCell(
            (string)c.Attribute("r")!,
            (string?)c.Attribute("t"),
            (int?)c.Attribute("s") ?? 0,
            (string?)c.Element(Ns + "v"),
            c.Element(Ns + "is")?.Element(Ns + "t")?.Value)).ToDictionary(c => c.Ref);

    /// <summary>The number format code of a cell format index ("General" for 0).</summary>
    public static string NumberFormat(byte[] workbook, int style)
    {
        var styles = Part(workbook, "xl/styles.xml");
        var xf = styles.Descendants(Ns + "cellXfs").Single().Elements(Ns + "xf").ElementAt(style);
        int id = (int)xf.Attribute("numFmtId")!;
        if (id == 0) return "General";
        return (string)styles.Descendants(Ns + "numFmt").Single(n => (int)n.Attribute("numFmtId")! == id).Attribute("formatCode")!;
    }

    public static XElement CellFormat(byte[] workbook, int style) =>
        Part(workbook, "xl/styles.xml").Descendants(Ns + "cellXfs").Single().Elements(Ns + "xf").ElementAt(style);

    public static XElement Font(byte[] workbook, int style)
    {
        var styles = Part(workbook, "xl/styles.xml");
        int fontId = (int)CellFormat(workbook, style).Attribute("fontId")!;
        return styles.Descendants(Ns + "fonts").Single().Elements(Ns + "font").ElementAt(fontId);
    }
}

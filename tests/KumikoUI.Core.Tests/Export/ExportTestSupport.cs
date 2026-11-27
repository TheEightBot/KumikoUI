using System.Text;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests.Export;

/// <summary>An item the export tests read.</summary>
internal sealed class Person
{
    public string Name { get; set; } = "";
    public string Dept { get; set; } = "";
    public decimal Salary { get; set; }
    public DateTime Hired { get; set; }
    public bool Active { get; set; }
}

/// <summary>A write-only stream that is not seekable and counts what it was given (a network or response stream).</summary>
internal sealed class CountingStream : Stream
{
    public long BytesWritten { get; private set; }
    public bool Disposed { get; private set; }
    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => BytesWritten += count;
    protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
}

/// <summary>A minimal RFC 4180 reader the tests use to read the exporter's output back.</summary>
internal static class Rfc4180
{
    public static List<List<string>> Parse(string text, char delimiter = ',')
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        bool quoted = false, any = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else field.Append(c);
            }
            else if (c == '"') { quoted = true; any = true; }
            else if (c == delimiter) { row.Add(field.ToString()); field.Clear(); any = true; }
            else if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
            {
                row.Add(field.ToString()); field.Clear(); rows.Add(row); row = new List<string>(); any = false; i++;
            }
            else { field.Append(c); any = true; }
        }
        if (any || field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row); }
        return rows;
    }
}

internal static class ExportTests
{
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
        var source = new KumikoSource { Culture = System.Globalization.CultureInfo.InvariantCulture };
        source.SetColumns(new[]
        {
            new KumikoColumn { PropertyName = nameof(Person.Name), Header = "Name" },
            new KumikoColumn { PropertyName = nameof(Person.Dept), Header = "Dept" },
            new KumikoColumn { PropertyName = nameof(Person.Salary), Header = "Salary", ColumnType = KumikoColumnType.Numeric, Format = "N2" },
            new KumikoColumn { PropertyName = nameof(Person.Hired), Header = "Hired", ColumnType = KumikoColumnType.Date, Format = "yyyy-MM-dd" },
            new KumikoColumn { PropertyName = nameof(Person.Active), Header = "Active", ColumnType = KumikoColumnType.Boolean },
        });
        source.SetItems((items ?? People()).ToList());
        return source;
    }

    public static KumikoColumn Col(KumikoSource s, string property) => s.Columns.Single(c => c.PropertyName == property);

    public static string Text(string[] lines) => string.Join("\r\n", lines) + "\r\n";
}

using System.Globalization;
using KumikoUI.Core.Export;
using KumikoUI.Core.Models;
using KumikoUI.Export.Excel.Internal;
using static KumikoUI.Export.Excel.Tests.Xlsx;

namespace KumikoUI.Export.Excel.Tests;

public class XlsxLimitsAndStreamingTests
{
    [Theory]
    [InlineData(0, "A")]
    [InlineData(25, "Z")]
    [InlineData(26, "AA")]
    [InlineData(701, "ZZ")]
    [InlineData(702, "AAA")]
    [InlineData(16_383, "XFD")]
    public void ColumnNames_FollowExcelsLetters(int index, string name) => Assert.Equal(name, XlsxLayout.ColumnName(index));

    [Theory]
    [InlineData("Sheet1", "Sheet1")]
    [InlineData("a/b\\c?d*e:f[g]h", "abcdefgh")]
    [InlineData("'quoted'", "quoted")]
    [InlineData("", "Sheet1")]
    [InlineData("[]:*?", "Sheet1")]
    [InlineData("   ", "Sheet1")]
    [InlineData("history", "history_")]
    [InlineData("tab\there", "tabhere")]
    [InlineData("1234567890123456789012345678901234567890", "1234567890123456789012345678901")]
    public void SheetNames_AreMadeValid(string name, string expected) => Assert.Equal(expected, XlsxLayout.SanitizeSheetName(name));

    [Fact]
    public void SheetNames_AreNotCutInsideASurrogatePair()
    {
        string name = new string('a', 30) + "😀";
        Assert.Equal(new string('a', 30), XlsxLayout.SanitizeSheetName(name));
    }

    [Fact]
    public async Task TooManyColumns_ThrowBeforeAnyByteIsWritten()
    {
        var source = new KumikoSource();
        source.SetColumns(Enumerable.Range(0, XlsxLayout.MaxColumns + 1).Select(i => new KumikoColumn { Header = "C" + i, ValueAccessor = _ => i }).ToList());
        source.SetItems(new List<object> { new() });

        var stream = new ForwardOnlyStream();
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => source.ExportToExcelAsync(stream));
        Assert.Contains("16384", ex.Message, StringComparison.Ordinal);
        Assert.Equal(0, stream.BytesWritten);
    }

    [Fact]
    public async Task ExactlyTheColumnLimit_IsWritten()
    {
        var source = new KumikoSource();
        source.SetColumns(Enumerable.Range(0, XlsxLayout.MaxColumns).Select(i => new KumikoColumn { Header = "C" + i, ValueAccessor = _ => i }).ToList());
        source.SetItems(new List<object> { new() });

        var bytes = await ExportAsync(source, xlsx: new KumikoXlsxOptions { ColumnWidths = KumikoXlsxColumnWidths.Fixed });
        Assert.Equal("A1:XFD2", (string)Sheet(bytes).Descendants(Ns + "dimension").Single().Attribute("ref")!);
        Assert.Equal("16383", Cells(bytes)["XFD2"].Value);
    }

    [Fact]
    public async Task TooManyRows_ThrowBeforeAnyByteIsWritten()
    {
        // 1,048,576 data rows plus the header row is one row more than a sheet holds.
        var items = Enumerable.Repeat(new object(), XlsxLayout.MaxRows).ToList();
        KumikoSource Make()
        {
            var made = new KumikoSource();
            made.SetColumns(new[] { new KumikoColumn { Header = "X", ValueAccessor = _ => 1 } });
            made.SetItems(items);
            return made;
        }

        var source = Make();
        var stream = new ForwardOnlyStream();
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => source.ExportToExcelAsync(stream));
        Assert.Contains("1048577", ex.Message, StringComparison.Ordinal);
        Assert.Equal(0, stream.BytesWritten);

        // Without the header row it fits (a new source: the old one belongs to the thread before the await).
        var count = new ForwardOnlyStream { Keep = false };
        await Make().ExportToExcelAsync(count, new KumikoExportOptions { IncludeHeaders = false });
        Assert.True(count.BytesWritten > 0);
    }

    [Fact]
    public async Task NonSeekableDestination_GetsAValidWorkbookAndIsNotClosed()
    {
        var stream = new ForwardOnlyStream();
        await Source().ExportToExcelAsync(stream);
        Assert.False(stream.Disposed);
        var bytes = stream.ToArray();
        AssertValid(bytes);
        Assert.Equal("Ada", Cells(bytes)["A2"].Text);
    }

    [Fact]
    public async Task CanceledToken_ThrowsBeforeAnythingIsWritten()
    {
        var stream = new ForwardOnlyStream();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Source().ExportToExcelAsync(stream, cancellationToken: cts.Token));
        Assert.Equal(0, stream.BytesWritten);
    }

    [Fact]
    public async Task Progress_EndsAtTheRowCount()
    {
        var reports = new List<KumikoExportProgress>();
        using var stream = new MemoryStream();
        await Source().ExportToExcelAsync(stream, progress: new SyncProgress(reports.Add));
        Assert.Equal(new KumikoExportProgress(5, 5), reports[^1]);
    }

    [Fact]
    public async Task CallerThreadMode_ReadsOnTheCallerAndWritesTheSameSheet()
    {
        var items = Enumerable.Range(0, 5000).Select(i => new Person { Name = "P" + i, Dept = "D" + i % 7, Salary = i, Hired = new DateTime(2020, 1, 1).AddDays(i % 400), Active = i % 2 == 0 }).ToList();
        var pool = await ExportAsync(Source(items));

        var context = new PumpContext();
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            int caller = Environment.CurrentManagedThreadId;
            var threads = new HashSet<int>();
            var source = Source(items);
            source.Columns[0].ValueAccessor = i => { lock (threads) threads.Add(Environment.CurrentManagedThreadId); return ((Person)i).Name; };
            using var stream = new MemoryStream();
            await source.ExportToExcelAsync(stream, new KumikoExportOptions { ReadOnCallerThread = true });

            Assert.Equal(new[] { caller }, threads.ToArray());
            Assert.True(context.Posts >= 2, $"expected yields between chunks, saw {context.Posts}");
            AssertValid(stream.ToArray());
            Assert.Equal(Sheet(pool).ToString(), Sheet(stream.ToArray()).ToString());
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
    }

    [Fact]
    public async Task HundredThousandRowsByTenColumns_ValidateAndStreamWithBoundedMemory()
    {
        const int rows = 100_000;
        var items = Enumerable.Range(0, rows).Select(i => new Person { Name = "Person " + i, Dept = "Dept " + i % 50, Salary = i * 1.25m, Hired = new DateTime(2020, 1, 1).AddDays(i % 1000), Active = i % 2 == 0 }).ToList();
        // A source belongs to the thread that creates it, and the test continues on another one after an await.
        KumikoSource Make()
        {
            var source = new KumikoSource { Culture = CultureInfo.InvariantCulture };
            var columns = new List<KumikoColumn>();
            for (int c = 0; c < 10; c++)
            {
                int k = c % 5;
                columns.Add(new KumikoColumn
                {
                    Header = "C" + c,
                    Format = k switch { 2 => "N2", 3 => "yyyy-MM-dd", _ => null },
                    ValueAccessor = k switch
                    {
                        0 => i => ((Person)i).Name,
                        1 => i => ((Person)i).Dept,
                        2 => i => ((Person)i).Salary,
                        3 => i => ((Person)i).Hired,
                        _ => i => ((Person)i).Active,
                    },
                });
            }

            source.SetColumns(columns);
            source.SetItems(items);
            return source;
        }

        // Streaming: memory measured while the rows are written to a stream that keeps nothing.
        long baseline = GC.GetTotalMemory(true); // collects first
        long peak = 0;
        var counting = new ForwardOnlyStream { Keep = false };
        await Make().ExportToExcelAsync(counting, progress: new SyncProgress(_ => peak = Math.Max(peak, GC.GetTotalMemory(true))));
        Assert.True(counting.BytesWritten > 1_000_000, $"output was {counting.BytesWritten} bytes");
        Assert.True(peak - baseline < 60_000_000, $"peak {peak - baseline} above the baseline");
        Assert.True(GC.GetTotalMemory(true) - baseline < 8_000_000, "memory retained after the export");

        // The same workbook, kept, passes the validator and has every row.
        var bytes = await ExportAsync(Make());
        AssertValid(bytes);
        Assert.Equal($"A1:J{rows + 1}", (string)Sheet(bytes).Descendants(Ns + "dimension").Single().Attribute("ref")!);
    }

    private sealed class PumpContext : SynchronizationContext
    {
        public int Posts;
        public override void Post(SendOrPostCallback d, object? state) { Interlocked.Increment(ref Posts); d(state); }
    }

    private sealed class SyncProgress(Action<KumikoExportProgress> action) : IProgress<KumikoExportProgress>
    {
        public void Report(KumikoExportProgress value) => action(value);
    }
}

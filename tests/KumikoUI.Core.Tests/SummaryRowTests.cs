using System.Globalization;
using KumikoUI.Core.Models;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests;

/// <summary>
/// Summary rows (#19): several aggregates per property, group summaries at every level, exact decimal
/// arithmetic, cheap handling of non-numeric values, safe Min/Max, case-insensitive and column-less lookup.
/// </summary>
public class SummaryRowTests
{
    private sealed class Line
    {
        public string Region { get; set; } = "";
        public string Team { get; set; } = "";
        public decimal Amount { get; set; }
        public object? Mixed { get; set; }
        public string Label { get; set; } = "";
    }

    private static KumikoSource Build(IEnumerable<Line> lines, bool amountColumn = true)
    {
        var source = new KumikoSource { Culture = CultureInfo.InvariantCulture };
        var columns = new List<KumikoColumn>
        {
            new() { Header = "Region", PropertyName = "Region" },
            new() { Header = "Team", PropertyName = "Team" },
            new() { Header = "Label", PropertyName = "Label" },
            new() { Header = "Mixed", PropertyName = "Mixed" },
        };
        if (amountColumn)
            columns.Add(new KumikoColumn { Header = "Amount", PropertyName = "Amount", ColumnType = KumikoColumnType.Numeric });
        source.SetColumns(columns);
        source.SetItems(lines.ToList());
        return source;
    }

    private static List<Line> Sample() => new()
    {
        new Line { Region = "N", Team = "a", Amount = 10m },
        new Line { Region = "N", Team = "a", Amount = 20m },
        new Line { Region = "N", Team = "b", Amount = 30m },
        new Line { Region = "S", Team = "c", Amount = 40m },
    };

    [Fact]
    public void ReadmeExample_SumAndAverageOfOneColumnBothSurvive()
    {
        var source = Build(Sample());
        source.AddTableSummaryRow(new TableSummaryRow("Totals")
        {
            Columns =
            {
                new SummaryColumnDescription("Amount", SummaryType.Sum, "C0"),
                new SummaryColumnDescription("Label", SummaryType.Count) { Label = "Rows: " },
                new SummaryColumnDescription("Amount", SummaryType.Average, "C0") { Label = "Avg: " },
            },
        });

        var row = Assert.Single(source.ComputedBottomSummaries);

        Assert.Equal(3, row.Aggregates.Count);
        Assert.Equal(100m, row.GetRawValue("Amount", SummaryType.Sum));
        Assert.Equal(25m, row.GetRawValue("Amount", SummaryType.Average));
        Assert.Contains("Sum: ", row.Values["Amount"]);
        Assert.Contains("Avg: ", row.Values["Amount"]);
        Assert.Equal("Rows: 4", row.Values["Label"]);
    }

    [Fact]
    public void ReadmeExample_RendersBothAggregatesInTheCell()
    {
        var h = RenderHarness.Create(rows: 5, configure: x =>
        {
            x.Source.Culture = CultureInfo.InvariantCulture;
            x.Source.AddTableSummaryRow(new TableSummaryRow("Totals")
            {
                Columns =
                {
                    new SummaryColumnDescription("Salary", SummaryType.Sum),
                    new SummaryColumnDescription("Salary", SummaryType.Average) { Label = "Avg: " },
                },
            });
        });

        var ctx = h.Render();

        var cell = Assert.Single(ctx.Ops.Where(o => o.IsText && o.Text!.StartsWith("Sum: ")));
        Assert.Contains("Avg: 1020", cell.Text);
    }

    [Fact]
    public void SeveralAggregatesOfOneProperty_AreInDeclarationOrder()
    {
        var source = Build(Sample());
        source.AddTableSummaryRow(new TableSummaryRow("T")
        {
            Columns =
            {
                new SummaryColumnDescription("Amount", SummaryType.Min),
                new SummaryColumnDescription("Amount", SummaryType.Max),
            },
        });

        var row = Assert.Single(source.ComputedBottomSummaries);

        Assert.Equal(new[] { SummaryType.Min, SummaryType.Max }, row.Aggregates.Select(a => a.SummaryType));
        Assert.Equal("Min: 10  |  Max: 40", row.Values["Amount"]);
    }

    [Fact]
    public void GroupSummaries_Innermost_IsTheDefault()
    {
        var source = Build(Sample());
        source.AddGroupDescription(new GroupDescription("Region"));
        source.AddGroupDescription(new GroupDescription("Team"));
        source.AddGroupSummaryRow(new GroupSummaryRow("G") { Columns = { new SummaryColumnDescription("Amount", SummaryType.Sum) } });

        Assert.Equal(GroupSummaryLevels.Innermost, source.GroupSummaryRows[0].Levels);
        Assert.NotNull(source.GetGroupSummaries("Region=N|Team=a"));
        Assert.Null(source.GetGroupSummaries("Region=N"));
    }

    [Fact]
    public void GroupSummaries_All_AppearAtBothLevels()
    {
        var source = Build(Sample());
        source.AddGroupDescription(new GroupDescription("Region"));
        source.AddGroupDescription(new GroupDescription("Team"));
        source.AddGroupSummaryRow(new GroupSummaryRow("G")
        {
            Levels = GroupSummaryLevels.All,
            Columns = { new SummaryColumnDescription("Amount", SummaryType.Sum) },
        });

        var outer = Assert.Single(source.GetGroupSummaries("Region=N")!);
        var inner = Assert.Single(source.GetGroupSummaries("Region=N|Team=a")!);

        Assert.Equal(60m, outer.RawValues["Amount"]);
        Assert.Equal(30m, inner.RawValues["Amount"]);
        Assert.Equal(40m, Assert.Single(source.GetGroupSummaries("Region=S")!).RawValues["Amount"]);

        // The outer summary follows its subgroups in the flat view, and every summary row resolves to its group
        var summaryRows = Enumerable.Range(0, source.RowCount).Where(source.IsGroupSummaryRow).ToList();
        Assert.Equal(5, summaryRows.Count); // N/a, N/b, N, S/c, S
        Assert.Equal("Region=N", source.GetGroupSummaryRowInfo(summaryRows[2])!.Value.GroupPath);
    }

    [Fact]
    public void GroupSummaries_InnermostAndAllRowsCanBeMixed()
    {
        var source = Build(Sample());
        source.AddGroupDescription(new GroupDescription("Region"));
        source.AddGroupDescription(new GroupDescription("Team"));
        source.AddGroupSummaryRow(new GroupSummaryRow("Leaf") { Columns = { new SummaryColumnDescription("Amount", SummaryType.Sum) } });
        source.AddGroupSummaryRow(new GroupSummaryRow("Every") { Levels = GroupSummaryLevels.All, Columns = { new SummaryColumnDescription("Amount", SummaryType.Count) } });

        Assert.Equal(new[] { "Every" }, source.GetGroupSummaries("Region=N")!.Select(r => r.Name));
        Assert.Equal(new[] { "Leaf", "Every" }, source.GetGroupSummaries("Region=N|Team=a")!.Select(r => r.Name));
    }

    [Fact]
    public void DecimalTotals_AreExact()
    {
        var lines = Enumerable.Repeat(0.1m, 3).Select(a => new Line { Amount = a }).ToList();
        var source = Build(lines);
        source.AddTableSummaryRow(new TableSummaryRow("T")
        {
            Columns =
            {
                new SummaryColumnDescription("Amount", SummaryType.Sum),
                new SummaryColumnDescription("Amount", SummaryType.Average),
            },
        });

        var row = Assert.Single(source.ComputedBottomSummaries);

        Assert.Equal(0.3m, row.GetRawValue("Amount", SummaryType.Sum));
        Assert.Equal(0.1m, row.GetRawValue("Amount", SummaryType.Average));
        Assert.Equal("Sum: 0.3  |  Avg: 0.1", row.Values["Amount"]);
    }

    [Fact]
    public void IntegerAndFloatingPointValues_StillSumAsDouble()
    {
        var source = Build(new[]
        {
            new Line { Mixed = 1 }, new Line { Mixed = 2.5 }, new Line { Mixed = 3f },
        });
        source.AddTableSummaryRow(new TableSummaryRow("T") { Columns = { new SummaryColumnDescription("Mixed", SummaryType.Sum) } });

        Assert.Equal(6.5d, Assert.Single(source.ComputedBottomSummaries).RawValues["Mixed"]);
    }

    [Fact]
    public void SumOfAStringColumn_SkipsEveryValueWithoutThrowing()
    {
        var lines = Enumerable.Range(0, 100_000).Select(i => new Line { Label = "x" + i }).ToList();
        // The event is process-wide, so count only the exceptions raised on this test's thread
        int thrown = 0, testThread = Environment.CurrentManagedThreadId;
        void OnThrown(object? sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
        {
            if (Environment.CurrentManagedThreadId == testThread) thrown++;
        }

        var source = Build(lines);
        AppDomain.CurrentDomain.FirstChanceException += OnThrown;
        try
        {
            source.AddTableSummaryRow(new TableSummaryRow("T")
            {
                Columns =
                {
                    new SummaryColumnDescription("Label", SummaryType.Sum),
                    new SummaryColumnDescription("Label", SummaryType.Average),
                },
            });
        }
        finally
        {
            AppDomain.CurrentDomain.FirstChanceException -= OnThrown;
        }

        var row = Assert.Single(source.ComputedBottomSummaries);
        Assert.Equal(0d, row.GetRawValue("Label", SummaryType.Sum));
        Assert.Null(row.GetRawValue("Label", SummaryType.Average));
        Assert.Equal(0, thrown);
    }

    [Fact]
    public void MinAndMax_OfMixedTypes_DoNotThrow()
    {
        var source = Build(new[]
        {
            new Line { Mixed = 5 }, new Line { Mixed = "apple" }, new Line { Mixed = 2.5m },
            new Line { Mixed = null }, new Line { Mixed = new DateTime(2020, 1, 1) },
        });
        source.AddTableSummaryRow(new TableSummaryRow("T")
        {
            Columns =
            {
                new SummaryColumnDescription("Mixed", SummaryType.Min),
                new SummaryColumnDescription("Mixed", SummaryType.Max),
            },
        });

        var row = Assert.Single(source.ComputedBottomSummaries);

        Assert.NotNull(row.GetRawValue("Mixed", SummaryType.Min));
        Assert.NotNull(row.GetRawValue("Mixed", SummaryType.Max));
    }

    [Fact]
    public void MinAndMax_OfNumbersAcrossTypes_CompareByValue()
    {
        var source = Build(new[] { new Line { Mixed = 3 }, new Line { Mixed = 2.5 }, new Line { Mixed = 10L } });
        source.AddTableSummaryRow(new TableSummaryRow("T")
        {
            Columns =
            {
                new SummaryColumnDescription("Mixed", SummaryType.Min),
                new SummaryColumnDescription("Mixed", SummaryType.Max),
            },
        });

        var row = Assert.Single(source.ComputedBottomSummaries);

        Assert.Equal(2.5, row.GetRawValue("Mixed", SummaryType.Min));
        Assert.Equal(10L, row.GetRawValue("Mixed", SummaryType.Max));
    }

    [Fact]
    public void PropertyLookup_IsCaseInsensitive()
    {
        var source = Build(Sample());
        source.AddTableSummaryRow(new TableSummaryRow("T") { Columns = { new SummaryColumnDescription("amount", SummaryType.Sum) } });

        var row = Assert.Single(source.ComputedBottomSummaries);

        Assert.Equal(100m, row.RawValues["Amount"]);
        Assert.Equal("Sum: 100", row.Values["AMOUNT"]);
        Assert.Equal(100m, row.GetRawValue("AMOUNT", SummaryType.Sum));
    }

    [Fact]
    public void PropertyWithoutAColumn_CanBeAggregated()
    {
        var source = Build(Sample(), amountColumn: false);
        source.AddTableSummaryRow(new TableSummaryRow("T") { Columns = { new SummaryColumnDescription("Amount", SummaryType.Sum) } });

        Assert.Equal(100m, Assert.Single(source.ComputedBottomSummaries).RawValues["Amount"]);
    }

    [Fact]
    public void HiddenColumn_CanBeAggregated()
    {
        var source = Build(Sample());
        source.Columns.Single(c => c.PropertyName == "Amount").IsVisible = false;
        source.AddTableSummaryRow(new TableSummaryRow("T") { Columns = { new SummaryColumnDescription("Amount", SummaryType.Sum) } });

        Assert.Equal(100m, Assert.Single(source.ComputedBottomSummaries).RawValues["Amount"]);
    }
}

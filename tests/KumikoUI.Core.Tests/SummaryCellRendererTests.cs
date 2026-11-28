using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests;

/// <summary>The custom summary cell renderer (#334): resolution, clipping, the overflow rule and every row kind.</summary>
public class SummaryCellRendererTests
{
    private sealed class Probe : ISummaryCellRenderer
    {
        public List<(KumikoRect Bounds, KumikoRect CellBounds, KumikoSummaryRowKind Kind, string Property, string Text,
            object? Raw, string? Group, int Aggregates, string Row)> Calls
        { get; } = new();

        public void Render(IDrawingContext ctx, in SummaryCellRenderContext c) =>
            Calls.Add((c.Bounds, c.CellBounds, c.Kind, c.Column.PropertyName, c.DisplayText, c.RawValue,
                c.Group?.DisplayText, c.Summary.Aggregates.Count, c.Summary.Name));
    }

    private static TableSummaryRow Row(string name, SummaryPosition position, params SummaryColumnDescription[] columns)
    {
        var row = new TableSummaryRow(name, position) { Title = name };
        foreach (var c in columns) row.Columns.Add(c);
        return row;
    }

    [Fact]
    public void ResolutionOrder_DescriptionThenGridWideThenText()
    {
        var own = new Probe();
        var gridWide = new Probe();
        var h = RenderHarness.Create(rows: 10, width: 700, configure: x =>
        {
            x.Source.AddTableSummaryRow(Row("T", SummaryPosition.Bottom,
                new SummaryColumnDescription("Age", SummaryType.Sum) { CustomCellRenderer = own },
                new SummaryColumnDescription("Salary", SummaryType.Sum)));
            x.Renderer.SummaryCellRenderer = gridWide;
        });

        h.Render();
        h.Render();

        Assert.All(own.Calls, c => Assert.Equal("Age", c.Property));
        Assert.NotEmpty(own.Calls);
        Assert.All(gridWide.Calls, c => Assert.Equal("Salary", c.Property));
        Assert.NotEmpty(gridWide.Calls);

        h.Renderer.SummaryCellRenderer = null;
        own.Calls.Clear();
        gridWide.Calls.Clear();
        var ctx = h.Render();
        Assert.Empty(gridWide.Calls);
        Assert.Contains(ctx.Texts, o => o.Text!.StartsWith("Sum: ", StringComparison.Ordinal) && o.Color == h.Style.SummaryRowTextColor);
    }

    [Fact]
    public void SeveralAggregatesOfOneProperty_FirstWithRendererDrawsTheCell_AndSeesAll()
    {
        var probe = new Probe();
        var h = RenderHarness.Create(rows: 10, width: 700, configure: x =>
            x.Source.AddTableSummaryRow(Row("T", SummaryPosition.Bottom,
                new SummaryColumnDescription("Age", SummaryType.Min),
                new SummaryColumnDescription("Age", SummaryType.Max) { CustomCellRenderer = probe },
                new SummaryColumnDescription("Age", SummaryType.Sum) { CustomCellRenderer = new Probe() })));

        h.Render();

        var call = Assert.Single(probe.Calls.Take(1));
        Assert.Equal(3, call.Aggregates);
        Assert.Equal("Min: 20  |  Max: 24  |  Sum: 220", call.Text);
        Assert.Equal(20, call.Raw);
        Assert.Equal(KumikoSummaryRowKind.Table, call.Kind);
        Assert.Null(call.Group);
    }

    [Fact]
    public void CustomRenderer_IsCalledForTopBottomAndGroupRows()
    {
        var probe = new Probe();
        var h = RenderHarness.Create(rows: 10, width: 700, configure: x =>
        {
            x.Source.AddTableSummaryRow(Row("Top", SummaryPosition.Top,
                new SummaryColumnDescription("Age", SummaryType.Sum) { CustomCellRenderer = probe }));
            x.Source.AddTableSummaryRow(Row("Bottom", SummaryPosition.Bottom,
                new SummaryColumnDescription("Age", SummaryType.Max) { CustomCellRenderer = probe }));
            x.Source.AddGroupDescription(new GroupDescription("City"));
            x.Source.AddGroupSummaryRow(new GroupSummaryRow("Grp")
            {
                Title = "Grp",
                Columns = { new SummaryColumnDescription("Age", SummaryType.Sum) { CustomCellRenderer = probe } },
            });
        });

        h.Render();

        Assert.Contains(probe.Calls, c => c.Row == "Top" && c.Kind == KumikoSummaryRowKind.Table);
        Assert.Contains(probe.Calls, c => c.Row == "Bottom" && c.Kind == KumikoSummaryRowKind.Table);
        var groupCalls = probe.Calls.Where(c => c.Kind == KumikoSummaryRowKind.Group).ToList();
        Assert.NotEmpty(groupCalls);
        Assert.All(groupCalls, c => Assert.Contains(c.Group, new[] { "Oslo", "Rome" }));
    }

    [Fact]
    public void PinnedBottomSummary_CallsTheRenderer()
    {
        var probe = new Probe();
        var h = RenderHarness.Create(rows: 100, width: 700, configure: x =>
        {
            x.Style.PinBottomSummaryRows = true;
            x.Source.AddTableSummaryRow(Row("Bottom", SummaryPosition.Bottom,
                new SummaryColumnDescription("Age", SummaryType.Max) { CustomCellRenderer = probe }));
        });

        h.Render();

        Assert.NotEmpty(probe.Calls);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(35f)]
    [InlineData(120f)]
    public void Bounds_AreClippedToTheVisiblePart_AndNeverUnderTheTitle(float offsetX)
    {
        var probe = new Probe();
        var h = RenderHarness.Create(rows: 20, width: 400, height: 400, configure: x =>
        {
            x.Column("Name").IsFrozen = true;
            x.Column("Name").Width = 60;
            x.Column("Age").Width = 100;
            x.Column("City").Width = 150;
            x.Column("Salary").Width = 90;
            x.Source.AddTableSummaryRow(Row("Totals", SummaryPosition.Bottom,
                new SummaryColumnDescription("Name", SummaryType.Count) { CustomCellRenderer = probe },
                new SummaryColumnDescription("Age", SummaryType.Sum) { CustomCellRenderer = probe },
                new SummaryColumnDescription("Salary", SummaryType.Sum) { CustomCellRenderer = probe }));
            x.Scroll.OffsetX = offsetX;
        });
        h.Render();
        probe.Calls.Clear();

        h.Render();

        float frozen = h.Column("Name").Width;
        Assert.NotEmpty(probe.Calls);
        foreach (var c in probe.Calls)
        {
            Assert.True(c.Bounds.Left >= c.CellBounds.Left - 0.01f && c.Bounds.Right <= c.CellBounds.Right + 0.01f, c.Property);
            Assert.True(c.Bounds.Width > 0);
            if (c.Property == "Name")
                Assert.True(c.Bounds.Right <= frozen + 0.01f);
            else
                Assert.True(c.Bounds.Left >= frozen - 0.01f);
        }
    }

    [Fact]
    public void CustomCell_IsNeverMovedToTheOverflowText_ButDefaultCellIs()
    {
        var probe = new Probe();
        var h = RenderHarness.Create(rows: 10, width: 700, configure: x =>
        {
            x.Column("Age").Width = 60; // 44 px of text width: under the 70 px rule
            x.Column("Salary").Width = 60;
            x.Source.AddTableSummaryRow(Row("Totals", SummaryPosition.Bottom,
                new SummaryColumnDescription("Age", SummaryType.Sum) { CustomCellRenderer = probe },
                new SummaryColumnDescription("Salary", SummaryType.Sum)));
        });

        var ctx = h.Render();

        Assert.Contains(probe.Calls, c => c.Property == "Age" && c.Bounds.Width > 0);
        var overflow = ctx.Texts.Single(o => o.Text!.StartsWith("  |  ", StringComparison.Ordinal));
        Assert.Contains("Sum: ", overflow.Text);
        Assert.DoesNotContain("220", overflow.Text);
        Assert.DoesNotContain(ctx.Texts, o => o.Text == "Sum: 220");
    }

    [Fact]
    public void DefaultRenderer_DrawsTheSameTextAsBefore()
    {
        var h = RenderHarness.Create(rows: 10, width: 700, configure: x =>
            x.Source.AddTableSummaryRow(Row("Totals", SummaryPosition.Bottom,
                new SummaryColumnDescription("Age", SummaryType.Sum))));
        var plain = h.Render();

        h.Renderer.SummaryCellRenderer = new TextSummaryCellRenderer();
        var explicitText = h.Render();

        var a = plain.Texts.Single(o => o.Text == "Sum: 220");
        var b = explicitText.Texts.Single(o => o.Text == "Sum: 220");
        Assert.Equal(a.Rect, b.Rect);
        Assert.Equal(a.Color, b.Color);
    }

    [Fact]
    public void GetAccessibleText_DefaultsToNull()
    {
        ISummaryCellRenderer renderer = new TextSummaryCellRenderer();
        var h = RenderHarness.Create();
        var ctx = new SummaryCellRenderContext(default, default, KumikoSummaryRowKind.Table,
            new ComputedSummaryRow(), null, h.Column("Age"), "x", null, h.Style, new PaintCache(), new KumikoPaint(),
            KumikoTextAlignment.Left);

        Assert.Null(renderer.GetAccessibleText(in ctx));
    }
}

using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests;

/// <summary>
/// Summary values stay inside their own column and pane, never overlap the row's title, and keep their first
/// glyph when ellipsised (#27); frozen rows get horizontal grid lines like scrolling rows.
/// </summary>
public class SummaryClipTests
{
    private const float ViewportWidth = 400f;

    private static RenderHarness Create(float offsetX, bool groupSummary = false) =>
        RenderHarness.Create(rows: 20, width: ViewportWidth, height: 400, configure: h =>
        {
            h.Column("Name").IsFrozen = true;
            h.Column("Name").Width = 60;
            h.Column("Age").Width = 100;
            h.Column("City").Width = 150;
            h.Column("Salary").Width = 90;
            var row = new TableSummaryRow("Totals", SummaryPosition.Bottom) 
            {
                Title = "Totals",
                Columns =
                {
                    new SummaryColumnDescription("Name", SummaryType.Count) { Label = "Rows: " },
                    new SummaryColumnDescription("Age", SummaryType.Sum) { Label = "Sum: " },
                    new SummaryColumnDescription("Salary", SummaryType.Sum, "C0") { Label = "Sum: " },
                },
            };
            h.Source.AddTableSummaryRow(row);
            h.Scroll.OffsetX = offsetX;
        });

    private static RecordingDrawingContext RenderFrame(RenderHarness h)
    {
        h.Render(); // establishes the content size so the offset is not clamped away
        return h.Render();
    }

    /// <summary>Viewport rectangle (x, width) of a column in the current frame.</summary>
    private static (float Left, float Right) ColumnSpan(RenderHarness h, string property)
    {
        float x = 0;
        foreach (var c in h.Source.Columns)
        {
            if (c.PropertyName == property)
                return c.IsFrozen ? (x, x + c.Width) : (x - h.Scroll.OffsetX, x - h.Scroll.OffsetX + c.Width);
            x += c.Width;
        }
        throw new InvalidOperationException(property);
    }

    private static List<DrawOp> SummaryValueTexts(RenderHarness h, RecordingDrawingContext ctx) =>
        ctx.Texts.Where(o => o.Color == h.Style.SummaryRowTextColor).ToList();

    [Theory]
    [InlineData(0f)]
    [InlineData(35f)]
    [InlineData(120f)]
    public void EverySummaryText_IsInsideItsColumnAndItsPaneClip(float offsetX)
    {
        var h = Create(offsetX);
        var ctx = RenderFrame(h);
        float frozenWidth = h.Column("Name").Width;

        var texts = SummaryValueTexts(h, ctx);
        Assert.NotEmpty(texts);
        foreach (var op in texts)
        {
            Assert.NotNull(op.Clip);
            var clip = op.Clip!.Value;
            Assert.True(op.Rect.Left >= clip.Left - 0.01f && op.Rect.Right <= clip.Right + 0.01f, $"outside its clip: {op}");

            string property = op.Text!.StartsWith("Rows", StringComparison.Ordinal) ? "Name"
                : op.Text.Contains('$') ? "Salary" : "Age";
            var (left, right) = ColumnSpan(h, property);
            Assert.True(op.Rect.Left >= left - 0.01f && op.Rect.Right <= right + 0.01f, $"outside column {property}: {op}");

            if (property == "Name")
                Assert.True(clip.Right <= frozenWidth + 0.01f, $"frozen value clipped to the scroll pane: {op}");
            else
                Assert.True(clip.Left >= frozenWidth - 0.01f, $"scrolled value reaches the frozen pane: {op}");
        }
    }

    [Fact]
    public void ScrolledColumn_NeverDrawsOverFrozenPane()
    {
        var h = Create(offsetX: 120f); // Age (x 60..160) is scrolled mostly under the frozen Name column
        var ctx = RenderFrame(h);

        foreach (var op in SummaryValueTexts(h, ctx).Where(o => o.Text!.StartsWith("Sum: ", StringComparison.Ordinal)))
            Assert.True(op.Visible is null || op.Visible.Value.Left >= h.Column("Name").Width - 0.01f, op.ToString());
    }

    [Fact]
    public void WideValue_IsEllipsisedFromTheColumnWidth_AndKeepsItsFirstGlyph()
    {
        var h = Create(offsetX: 0f);
        var ctx = RenderFrame(h);

        var salary = SummaryValueTexts(h, ctx).Single(o => o.Text!.Contains('$'));
        string shown = RecordingDrawingContext.ShownText(salary);
        Assert.StartsWith("S", shown);
        Assert.EndsWith("…", shown);
        Assert.True(shown.Length * RecordingDrawingContext.CharWidth <= salary.Rect.Width + 0.01f);
        var (left, right) = ColumnSpan(h, "Salary");
        Assert.True(salary.Rect.Width <= right - left - 2 * h.Style.CellPadding + 0.01f);
    }

    [Fact]
    public void FirstColumnValue_DoesNotOverlapTheRowTitle()
    {
        // A wide first column: the value must start after the title rather than under it.
        var h = RenderHarness.Create(rows: 20, width: ViewportWidth, height: 400, configure: x =>
        {
            x.Column("Name").Width = 200;
            x.Source.AddTableSummaryRow(new TableSummaryRow("Totals", SummaryPosition.Bottom) 
            {
                Title = "Totals",
                Columns = { new SummaryColumnDescription("Name", SummaryType.Count) { Label = "Rows: " } },
            });
        });
        var ctx = RenderFrame(h);

        var title = ctx.Texts.Single(o => o.Text == "Totals");
        float titleRight = title.Rect.Left + "Totals".Length * RecordingDrawingContext.CharWidth;
        var value = SummaryValueTexts(h, ctx).Single();
        Assert.True(value.Rect.Left >= titleRight - 0.01f, $"{value} overlaps the title {title}");
    }

    [Fact]
    public void FrozenRows_GetHorizontalGridLines()
    {
        var h = RenderHarness.Create(rows: 20, width: ViewportWidth, height: 400, configure: x =>
        {
            x.Source.FrozenRowCount = 2;
        });
        var ctx = RenderFrame(h);

        foreach (int row in new[] { 0, 1 })
        {
            float y = h.Style.HeaderHeight + (row + 1) * h.Style.RowHeight;
            var fill = ctx.Ops.Where(o => o.Kind == DrawOpKind.FillRect && RenderHarness.Near(o.Rect.Y, y - h.Style.RowHeight)
                && o.Rect.Width >= ViewportWidth).Min(o => o.Index);
            Assert.Contains(ctx.Lines, l => l.Color == h.Style.GridLineColor && l.Index > fill
                && RenderHarness.Near(l.Y1, y) && RenderHarness.Near(l.Y2, y) && l.X2 - l.X1 >= ViewportWidth - 0.01f);
        }
    }
}

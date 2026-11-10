using System.Text.RegularExpressions;
using KumikoUI.Core.Layout;
using KumikoUI.Core.Models;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests;

/// <summary>
/// Scrolled rows are clipped vertically to the data area in every pane, with or without frozen
/// columns, so they never paint over the grouping panel (#117).
/// </summary>
public class PaneClipTests
{
    private static readonly Regex RowText = new(@"^P\d\d$");

    private static RenderHarness Grouped(bool frozenColumn) =>
        RenderHarness.Create(rows: 60, width: 600, height: 400, configure: h =>
        {
            h.Source.AddGroupDescription(new GroupDescription("City"));
            if (frozenColumn) h.Column("Name").IsFrozen = true;
        });

    private static RecordingDrawingContext RenderAt(RenderHarness h, float offsetY)
    {
        h.Render(); // establishes content size so the offset is not clamped away
        h.Scroll.OffsetY = offsetY;
        return h.Render();
    }

    private static bool IsRowPaint(DrawOp op, KumikoStyle style) => IsRowFill(op, style) || IsRowText(op);

    private static bool IsRowFill(DrawOp op, KumikoStyle style) =>
        op.Kind == DrawOpKind.FillRect && (op.Color == style.AlternateRowColor || op.Color == style.GroupHeaderBackgroundColor);

    private static bool IsRowText(DrawOp op) => op.IsText && RowText.IsMatch(op.Text ?? "");

    [Theory]
    [InlineData(false, 0f)]
    [InlineData(false, 7.5f)]
    [InlineData(false, 18f)]
    [InlineData(false, 35.9f)]
    [InlineData(false, 100.25f)]
    [InlineData(true, 0f)]
    [InlineData(true, 7.5f)]
    [InlineData(true, 18f)]
    [InlineData(true, 100.25f)]
    public void Grouped_NoRowPaintReachesAboveTheDataArea(bool frozenColumn, float offsetY)
    {
        var h = Grouped(frozenColumn);
        var ctx = RenderAt(h, offsetY);

        // Viewport Y of the data area's top: grouping panel + header (no top summaries).
        float dataTop = h.Style.GroupPanelHeight + h.Style.HeaderHeight;

        var rowPaint = ctx.Ops.Where(o => IsRowPaint(o, h.Style)).ToList();
        Assert.NotEmpty(rowPaint);
        // Visible is the op's rectangle intersected with the active clip; null when clipped away.
        foreach (var op in rowPaint.Where(o => o.Visible.HasValue))
            Assert.True(op.Visible!.Value.Top >= dataTop - 0.01f, $"Row paint above the data area: {op}");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Grouped_RowPassesAreClippedToDataArea(bool frozenColumn)
    {
        var h = Grouped(frozenColumn);
        var ctx = RenderAt(h, 18f);
        float dataTop = h.Style.GroupPanelHeight + h.Style.HeaderHeight;

        var rowFills = ctx.Ops.Where(o => o.Kind == DrawOpKind.FillRect && o.Color == h.Style.AlternateRowColor).ToList();
        Assert.NotEmpty(rowFills);
        Assert.All(rowFills, op =>
        {
            Assert.NotNull(op.Clip);
            Assert.True(op.Clip!.Value.Top >= dataTop - 0.01f, op.ToString());
        });
    }

    [Fact]
    public void FrozenColumn_StillClipsHorizontally()
    {
        var h = Grouped(frozenColumn: true);
        var ctx = RenderAt(h, 18f);

        float frozenWidth = h.Column("Name").Width;
        var scrollableText = ctx.Ops.Where(o => o.IsText && o.Text == "Oslo" && o.Clip is not null).ToList();
        Assert.NotEmpty(scrollableText);
        Assert.All(scrollableText, op => Assert.True(op.Clip!.Value.Left >= frozenWidth - 0.01f, op.ToString()));

        var frozenText = ctx.Ops.Where(o => o.IsText && o.Text != null && RowText.IsMatch(o.Text)).ToList();
        Assert.NotEmpty(frozenText);
        Assert.All(frozenText, op => Assert.True(op.Clip!.Value.Right <= frozenWidth + 0.01f, op.ToString()));
    }

    [Fact]
    public void VisibleRowRange_StartsAtFirstRowIntersectingTheDataArea()
    {
        var h = Grouped(frozenColumn: false);
        h.Scroll.OffsetY = 100.25f;
        var (first, _) = new KumikoLayoutEngine()
            .GetVisibleRowRangeWithOffset(h.Scroll, h.Style, h.Source.RowCount, 0);

        // Row r spans [r*RowHeight - 100.25, +RowHeight) below the header: row 1 ends above it, row 2 still shows 7.75 px.
        Assert.Equal(2, first);
    }
}

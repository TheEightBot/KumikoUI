using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;
using static KumikoUI.Core.Tests.TestHelpers.RenderHarness;

namespace KumikoUI.Core.Tests.Rendering;

/// <summary>
/// <see cref="ProgressBarCellRenderer"/>: the track and fill geometry for a numeric cell value, the
/// color choices (gradient, fixed, selector), the text overlay, value coercion, and the renderer in a
/// grid column.
/// </summary>
public class ProgressBarCellRendererTests
{
    // A 100 x 30 cell at (10, 20): the track is inset by 4 px, so it spans x 14-106 (92 wide).
    private static readonly KumikoRect Cell = new(10, 20, 100, 30);
    private static readonly KumikoStyle Style = new();

    private static RecordingDrawingContext Render(ProgressBarCellRenderer renderer, object? value)
    {
        var ctx = new RecordingDrawingContext();
        renderer.Render(ctx, Cell, value, value?.ToString() ?? "", new KumikoColumn(), Style, isSelected: false);
        return ctx;
    }

    private static DrawOp Fill(RecordingDrawingContext ctx) => ctx.Fills.Last();

    [Fact]
    public void Render_DrawsACenteredTrack_ThenAFillClippedToTheValue()
    {
        var ctx = Render(new ProgressBarCellRenderer { TrackColor = new KumikoColor(1, 1, 1), FillColor = new KumikoColor(2, 2, 2) }, 50);

        var fills = ctx.Fills.ToList();
        Assert.Equal(2, fills.Count);
        Assert.Equal(new KumikoRect(14, 30, 92, 10), fills[0].Rect);
        Assert.Equal(new KumikoColor(1, 1, 1), fills[0].Color);
        // The fill is a full-length rounded rect, clipped to half the track
        Assert.Equal(fills[0].Rect, fills[1].Rect);
        Assert.Equal(new KumikoColor(2, 2, 2), fills[1].Color);
        Assert.Equal((KumikoRect?)new KumikoRect(14, 30, 46, 10), fills[1].Clip);
        Assert.Equal(0, ctx.SaveDepth);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(25, 23)]
    [InlineData(100, 92)]
    [InlineData(250, 92)]
    [InlineData(-30, 0)]
    public void Render_FillWidthIsProportionalAndClamped(double value, float expectedWidth)
    {
        var ctx = Render(new ProgressBarCellRenderer { FillColor = KumikoColor.White }, value);

        if (expectedWidth < 0.01f)
            Assert.Single(ctx.Fills);
        else
            Assert.Equal(expectedWidth, Fill(ctx).Clip!.Value.Width, 3);
    }

    [Fact]
    public void Render_UsesTheConfiguredRange()
    {
        var ctx = Render(new ProgressBarCellRenderer { Minimum = 100, Maximum = 300, FillColor = KumikoColor.White }, 150);

        Assert.Equal(23, Fill(ctx).Clip!.Value.Width, 3);
    }

    [Fact]
    public void Render_AnEmptyRange_ShowsNoFill()
    {
        var ctx = Render(new ProgressBarCellRenderer { Minimum = 5, Maximum = 5 }, 5);

        Assert.Single(ctx.Fills);
    }

    [Theory]
    [InlineData(10, 220, 53, 69)]
    [InlineData(49, 220, 53, 69)]
    [InlineData(50, 255, 193, 7)]
    [InlineData(79, 255, 193, 7)]
    [InlineData(80, 40, 167, 70)]
    [InlineData(100, 40, 167, 70)]
    public void Render_WithoutAFixedColor_UsesARedAmberGreenGradient(double value, byte r, byte g, byte b)
    {
        var ctx = Render(new ProgressBarCellRenderer(), value);

        Assert.Equal(new KumikoColor(r, g, b), Fill(ctx).Color);
    }

    [Fact]
    public void Render_ASelectorOverridesTheFixedColor_AndReceivesTheValueAndRange()
    {
        var seen = new List<(double, double, double)>();
        var renderer = new ProgressBarCellRenderer
        {
            Minimum = 10,
            Maximum = 90,
            FillColor = new KumikoColor(1, 1, 1),
            ColorSelector = (v, min, max) =>
            {
                seen.Add((v, min, max));
                return new KumikoColor(9, 8, 7);
            },
        };

        var ctx = Render(renderer, 50);

        Assert.Equal(new KumikoColor(9, 8, 7), Fill(ctx).Color);
        Assert.Equal((50d, 10d, 90d), Assert.Single(seen));
    }

    [Fact]
    public void Render_ASelector_IsNotCalledWhenThereIsNothingToFill()
    {
        int calls = 0;
        var renderer = new ProgressBarCellRenderer { ColorSelector = (_, _, _) => { calls++; return KumikoColor.White; } };

        Render(renderer, 0);

        Assert.Equal(0, calls);
    }

    [Fact]
    public void Render_ShowsTheValueFormattedOverTheTrack()
    {
        var ctx = Render(new ProgressBarCellRenderer { TextFormat = "N1" }, 42.55);

        var text = ctx.SingleText(42.55.ToString("N1"));
        Assert.Equal(new KumikoRect(14, 30, 92, 10), text.Rect);
        Assert.Equal(KumikoTextAlignment.Center, text.HAlign);
    }

    [Fact]
    public void Render_TextIsLightOnAMostlyFilledBar_AndThemedOnAMostlyEmptyOne()
    {
        var full = Render(new ProgressBarCellRenderer(), 60).Texts.Single();
        var empty = Render(new ProgressBarCellRenderer(), 20).Texts.Single();

        Assert.Equal(KumikoColor.White, full.Color);
        Assert.Equal(Style.CellTextColor, empty.Color);
    }

    [Fact]
    public void Render_TextFontIsOnePointSmallerThanTheCellFont_ButNotBelowNine()
    {
        var style = new KumikoStyle { CellFont = new KumikoFont("Mono", 14) };
        var ctx = new RecordingDrawingContext();
        new ProgressBarCellRenderer().Render(ctx, Cell, 10, "10", new KumikoColumn(), style, false);
        Assert.Equal(13, ctx.Texts.Single().Font!.Size);
        Assert.Equal("Mono", ctx.Texts.Single().Font!.Family);

        style.CellFont = new KumikoFont("Mono", 8);
        ctx = new RecordingDrawingContext();
        new ProgressBarCellRenderer().Render(ctx, Cell, 10, "10", new KumikoColumn(), style, false);
        Assert.Equal(9, ctx.Texts.Single().Font!.Size);
    }

    [Fact]
    public void Render_ShowTextOff_DrawsNoText()
    {
        Assert.Empty(Render(new ProgressBarCellRenderer { ShowText = false }, 50).Texts);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not a number")]
    [InlineData(typeof(object))]
    public void Render_ValuesThatAreNotNumbers_ShowAnEmptyBar(object? value)
    {
        var ctx = Render(new ProgressBarCellRenderer(), value is Type ? new object() : value);

        Assert.Single(ctx.Fills);
        Assert.Equal("0", ctx.Texts.Single().Text);
    }

    [Theory]
    [InlineData(40)]
    [InlineData(40L)]
    [InlineData(40f)]
    [InlineData("40")]
    public void Render_ConvertsOtherNumericTypes(object value)
    {
        var ctx = Render(new ProgressBarCellRenderer { FillColor = KumikoColor.White }, value);

        Assert.Equal(36.8f, Fill(ctx).Clip!.Value.Width, 2);
    }

    [Fact]
    public void Render_TrackHeightZero_FillsTheCellHeightMinusPadding()
    {
        var ctx = Render(new ProgressBarCellRenderer { TrackHeight = 0 }, 50);

        Assert.Equal(new KumikoRect(14, 24, 92, 22), ctx.Fills.First().Rect);
    }

    [Fact]
    public void InAGridColumn_EveryVisibleRowGetsABarScaledToItsOwnValue()
    {
        var h = Create(rows: 5, configure: x =>
            x.Column("Age").CustomCellRenderer = new ProgressBarCellRenderer
            {
                Minimum = 20, Maximum = 24, FillColor = new KumikoColor(7, 7, 7),
            });

        var ctx = h.Render();

        var fills = ctx.Fills.Where(o => o.Color.Equals(new KumikoColor(7, 7, 7))).OrderBy(o => o.Rect.Y).ToList();
        // Ages cycle 20..24, so row 0 is empty and rows 1-4 are 25, 50, 75 and 100 percent
        Assert.Equal(4, fills.Count);
        // The cell is the column inset by the style's cell padding, and the track is inset by 4 more px
        float trackWidth = 100 - 2 * h.Style.CellPadding - 8;
        for (int i = 0; i < 4; i++)
        {
            Assert.Equal(trackWidth * (i + 1) / 4, fills[i].Clip!.Value.Width, 2);
            Assert.True(Near(fills[i].Rect.Y, h.RowTop(i + 1) + (h.Style.RowHeight - 10) / 2f));
            Assert.True(fills[i].Rect.X >= 150); // inside the Age column, after the 150 px Name column
        }
    }
}

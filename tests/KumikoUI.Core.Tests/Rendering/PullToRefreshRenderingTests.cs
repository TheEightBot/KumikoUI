using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.Input;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.Rendering;

/// <summary>
/// The pull-to-refresh indicator (#330): a disc that comes out from under the header (and the grouping panel) by the
/// pull's distance, over the rows, with an arrow while pulled and a spinner while refreshing.
/// </summary>
public class PullToRefreshRenderingTests
{
    private static readonly KumikoColor DiscColor = new(1, 2, 3);
    private static readonly KumikoColor GlyphColor = new(4, 5, 6);

    private static InputGrid Grid(Action<InputGrid>? configure = null) =>
        new(configure: g =>
        {
            g.Input.IsPullToRefreshEnabled = true;
            g.Input.AnimationsEnabled = true;
            g.Style.RefreshIndicatorBackgroundColor = DiscColor;
            g.Style.RefreshIndicatorColor = GlyphColor;
            configure?.Invoke(g);
        });

    /// <summary>Presses on a row and drags straight down so the indicator is pulled by <paramref name="distance"/>.</summary>
    private static void PullBy(InputGrid g, float distance, float startY = 100)
    {
        Touch(g, InputAction.Pressed, 150, startY);
        Touch(g, InputAction.Moved, 150, startY + 11); // the pan threshold
        Touch(g, InputAction.Moved, 150, startY + 11 + distance / PullToRefreshState.Resistance);
    }

    private static void Touch(InputGrid g, InputAction action, float x, float y) =>
        g.Pointer(action, x, y, deviceType: KumikoPointerDeviceType.Touch);

    private static void Settle(KumikoInputController input)
    {
        for (int i = 0; i < 100 && input.PullToRefresh.IsTransitioning; i++)
            input.UpdateAnimations(16);
    }

    /// <summary>The indicator's glyph: its line segments in the glyph color (any alpha when <paramref name="faded"/>).</summary>
    private static List<DrawOp> Glyph(RecordingDrawingContext ctx, bool faded = false) =>
        ctx.Lines.Where(o => faded
            ? o.Color.R == GlyphColor.R && o.Color.G == GlyphColor.G && o.Color.B == GlyphColor.B
            : o.Color.Equals(GlyphColor)).ToList();

    private static DrawOp Disc(RecordingDrawingContext ctx) =>
        Assert.Single(ctx.Ops, o => o.Kind == DrawOpKind.FillRoundRect && o.Color.Equals(DiscColor));

    [Fact]
    public void NoPull_DrawsNothing()
    {
        var g = Grid();

        var ctx = g.RenderRecorded();

        Assert.DoesNotContain(ctx.Ops, o => o.Color.Equals(DiscColor));
        Assert.Equal(KumikoRect.Empty, g.Renderer.PullIndicatorBounds);
    }

    [Fact]
    public void APull_DrawsTheDiscBelowTheHeader_ByThePullDistance_ClippedToBelowTheHeader()
    {
        var g = Grid();
        PullBy(g, 50);

        var ctx = g.RenderRecorded();

        var disc = Disc(ctx);
        float d = KumikoRenderer.PullIndicatorDiameter;
        float headerBottom = g.Style.HeaderHeight;
        Assert.Equal(headerBottom + 50 - d, disc.Rect.Top, 0.01f); // its bottom is the pull's distance below the header
        Assert.Equal(InputGrid.ViewportWidth / 2, disc.Rect.Left + d / 2, 0.01f); // centered
        Assert.Equal(headerBottom, disc.Clip!.Value.Top, 0.01f); // comes out from under the header
        Assert.Equal(disc.Rect, g.Renderer.PullIndicatorBounds);
    }

    [Fact]
    public void WithGrouping_TheDiscComesOutFromUnderTheGroupingPanelAndTheHeader()
    {
        var g = Grid(x => x.Source.AddGroupDescription(new GroupDescription("City")));
        g.Render();
        PullBy(g, 60, startY: 140);

        var ctx = g.RenderRecorded();

        var disc = Disc(ctx);
        float top = g.Style.GroupPanelHeight + g.Style.HeaderHeight;
        Assert.Equal(top, disc.Clip!.Value.Top, 0.01f);
        Assert.Equal(top + 60 - KumikoRenderer.PullIndicatorDiameter, disc.Rect.Top, 0.01f);
    }

    [Fact]
    public void TheIndicator_IsDrawnOverTheRows_AndTheRowsDoNotMove()
    {
        var g = Grid();
        var before = g.RenderRecorded().SingleText("N00").Rect;
        PullBy(g, 80);

        var ctx = g.RenderRecorded();

        var row = ctx.SingleText("N00");
        Assert.Equal(before, row.Rect);
        Assert.True(Disc(ctx).Index > row.Index);
    }

    [Fact]
    public void WhilePulled_AnArrowIsDrawn_FainterBelowTheThreshold()
    {
        var g = Grid();
        PullBy(g, 32); // half the threshold

        var ctx = g.RenderRecorded();

        var arrow = Glyph(ctx, faded: true);
        Assert.Equal(KumikoRenderer.PullArcSegments + 2, arrow.Count); // the arc and its head
        Assert.All(arrow, o => Assert.True(o.Color.A < 255));
    }

    [Fact]
    public void WhileRefreshing_ASpinnerIsDrawn_WithoutAnArrowHead_AndTurns()
    {
        var g = Grid();
        g.Input.IsRefreshing = true;
        Settle(g.Input);

        var first = g.RenderRecorded();
        var spinner = Glyph(first);
        Assert.Equal(KumikoRenderer.PullArcSegments, spinner.Count); // no arrow head
        Assert.Equal(g.Style.HeaderHeight + g.Input.PullToRefreshThreshold - KumikoRenderer.PullIndicatorDiameter,
            Disc(first).Rect.Top, 0.01f); // held at the threshold

        g.Input.UpdateAnimations(250); // a quarter turn
        var turned = Glyph(g.RenderRecorded());
        Assert.NotEqual(spinner[0].X1, turned[0].X1, 1);
    }

    [Fact]
    public void WithAnimationsOff_TheSpinnerStandsStill()
    {
        var g = Grid(x => x.Style.EnableAnimations = false);
        g.Input.IsRefreshing = true;

        var first = Glyph(g.RenderRecorded());
        g.Input.UpdateAnimations(250);
        var second = Glyph(g.RenderRecorded());

        Assert.Equal(first[0].X1, second[0].X1, 0.01f);
        Assert.Equal(first[0].Y1, second[0].Y1, 0.01f);
    }

    [Fact]
    public void TheGlyph_DefaultsToTheAccentColor()
    {
        var g = Grid(x => x.Style.RefreshIndicatorColor = null);
        g.Input.IsRefreshing = true;
        Settle(g.Input);

        var ctx = g.RenderRecorded();

        Assert.Equal(KumikoRenderer.PullArcSegments, ctx.Lines.Count(o => o.Color.Equals(g.Style.AccentColor)));
    }

    [Theory]
    [InlineData(KumikoThemeMode.Light)]
    [InlineData(KumikoThemeMode.Dark)]
    [InlineData(KumikoThemeMode.HighContrast)]
    public void EveryTheme_GivesTheDiscADistinctFillFromTheGlyph(KumikoThemeMode mode)
    {
        var style = KumikoTheme.Create(mode);

        var glyph = style.RefreshIndicatorColor ?? style.AccentColor;

        Assert.NotEqual(glyph, style.RefreshIndicatorBackgroundColor);
    }
}

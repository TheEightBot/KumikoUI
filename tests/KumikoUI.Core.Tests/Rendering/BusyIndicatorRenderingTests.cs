using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.Rendering;

/// <summary>
/// The busy indicator as drawn (#327): a bar or a scrim with a spinner over the data area, placed below the header,
/// the grouping panel and the top summaries and above a pinned footer, still with animations off.
/// </summary>
public class BusyIndicatorRenderingTests
{
    private static bool SameRgb(KumikoColor a, KumikoColor b) => a.R == b.R && a.G == b.G && a.B == b.B;

    private static List<DrawOp> Bar(RecordingDrawingContext ctx, KumikoStyle style) =>
        ctx.Ops.Where(o => o.Kind == DrawOpKind.FillRect && SameRgb(o.Color, style.BusyIndicatorColor)).ToList();

    private static List<DrawOp> Scrim(RecordingDrawingContext ctx, KumikoStyle style) =>
        ctx.Ops.Where(o => o.Kind == DrawOpKind.FillRect && SameRgb(o.Color, style.BusyOverlayColor)).ToList();

    private static List<DrawOp> Spokes(RecordingDrawingContext ctx, KumikoStyle style) =>
        ctx.Ops.Where(o => o.Kind == DrawOpKind.Line && SameRgb(o.Color, style.BusyIndicatorColor)).ToList();

    private static RenderHarness Busy(Action<RenderHarness>? configure = null, KumikoBusyIndicatorKind kind = KumikoBusyIndicatorKind.Bar) =>
        RenderHarness.Create(20, 600, 400, h =>
        {
            h.Style.BusyIndicatorKind = kind;
            h.Renderer.IsBusy = true;
            configure?.Invoke(h);
        });

    private static TableSummaryRow Summary(SummaryPosition position) => new("Totals", position)
    {
        Columns = { new SummaryColumnDescription { PropertyName = "Salary", SummaryType = SummaryType.Sum } },
    };

    // ── Not busy ──

    [Theory]
    [InlineData(KumikoBusyIndicatorKind.Bar)]
    [InlineData(KumikoBusyIndicatorKind.Overlay)]
    public void NotBusy_DrawsNothing_AndDoesNotAnimate(KumikoBusyIndicatorKind kind)
    {
        var h = Busy(x => x.Renderer.IsBusy = false, kind);

        var ctx = h.Render();

        Assert.Empty(Bar(ctx, h.Style));
        Assert.Empty(Scrim(ctx, h.Style));
        Assert.False(h.Renderer.IsBusyIndicatorAnimating);
    }

    // ── Bar ──

    [Fact]
    public void Bar_IsAThinBandAtTheTopOfTheDataArea_BelowTheHeader()
    {
        var h = Busy();

        var ctx = h.Render();

        var bar = Bar(ctx, h.Style);
        Assert.NotEmpty(bar);
        Assert.All(bar, o =>
        {
            Assert.Equal(h.Style.HeaderHeight, o.Rect.Y, 2);
            Assert.Equal(3f, o.Rect.Height, 2);
        });
        var track = bar.First();
        Assert.Equal(0f, track.Rect.X, 2);
        Assert.Equal(600f, track.Rect.Width, 2);
        Assert.Empty(Scrim(ctx, h.Style)); // the bar kind draws no scrim
    }

    [Fact]
    public void Bar_IsDrawnAfterTheRows_AndTheHeader()
    {
        var h = Busy();

        var ctx = h.Render();

        int firstBar = Bar(ctx, h.Style).Min(o => o.Index);
        Assert.True(ctx.Ops.Where(o => o.IsText).All(o => o.Index < firstBar), "every text is under the bar");
    }

    [Fact]
    public void Bar_SitsBelowTheGroupingPanel()
    {
        var h = Busy(x => x.Source.AddGroupDescription(new GroupDescription("City")));

        var ctx = h.Render();

        Assert.All(Bar(ctx, h.Style), o =>
            Assert.Equal(h.Style.GroupPanelHeight + h.Style.HeaderHeight, o.Rect.Y, 2));
    }

    [Fact]
    public void Bar_SitsBelowTheTopSummaries()
    {
        var h = Busy(x => x.Source.AddTableSummaryRow(Summary(SummaryPosition.Top)));

        var ctx = h.Render();

        Assert.All(Bar(ctx, h.Style), o =>
            Assert.Equal(h.Style.HeaderHeight + h.Style.SummaryRowHeight, o.Rect.Y, 2));
    }

    [Fact]
    public void Bar_WithAPinnedFooter_IsDrawnAfterIt()
    {
        var h = Busy(x =>
        {
            x.Style.PinBottomSummaryRows = true;
            x.Source.AddTableSummaryRow(Summary(SummaryPosition.Bottom));
        });

        var ctx = h.Render();

        int footer = ctx.Ops.Where(o => o.Kind == DrawOpKind.FillRect && SameRgb(o.Color, h.Style.SummaryRowBackgroundColor))
            .Max(o => o.Index);
        Assert.True(Bar(ctx, h.Style).Min(o => o.Index) > footer);
    }

    [Fact]
    public void Bar_Animated_SweepsWithTheClock_AndAsksForFrames()
    {
        var h = Busy();
        h.Renderer.BusyClock = () => 100;
        var first = Bar(h.Render(), h.Style).Last().Rect;
        h.Renderer.BusyClock = () => 700;
        var later = Bar(h.Render(), h.Style).Last().Rect;

        Assert.True(h.Renderer.IsBusyIndicatorAnimating);
        Assert.True(Math.Abs(first.X - later.X) > 1f, "the sweep moved");
        Assert.True(first.Width <= 600f * 0.35f + 0.01f);
    }

    [Fact]
    public void Bar_WithAnimationsOff_IsStatic_AndDoesNotAskForFrames()
    {
        var h = Busy(x => x.Style.EnableAnimations = false);
        h.Renderer.BusyClock = () => 100;
        var first = Bar(h.Render(), h.Style);
        h.Renderer.BusyClock = () => 700;
        var later = Bar(h.Render(), h.Style);

        Assert.False(h.Renderer.IsBusyIndicatorAnimating);
        Assert.NotEmpty(first);
        Assert.Equal(first.Select(o => o.Rect), later.Select(o => o.Rect));
    }

    [Fact]
    public void ClearingBusy_StopsTheAnimationRequest()
    {
        var h = Busy();
        h.Render();
        Assert.True(h.Renderer.IsBusyIndicatorAnimating);

        h.Renderer.IsBusy = false;
        h.Render();

        Assert.False(h.Renderer.IsBusyIndicatorAnimating);
    }

    // ── Overlay ──

    [Fact]
    public void Overlay_CoversTheDataArea_AndLeavesTheHeaderVisible()
    {
        var h = Busy(kind: KumikoBusyIndicatorKind.Overlay);

        var ctx = h.Render();

        var scrim = Assert.Single(Scrim(ctx, h.Style));
        Assert.Equal(0f, scrim.Rect.X, 2);
        Assert.Equal(h.Style.HeaderHeight, scrim.Rect.Y, 2);
        Assert.Equal(600f, scrim.Rect.Width, 2);
        Assert.Equal(400f - h.Style.HeaderHeight, scrim.Rect.Height, 2);
        Assert.Empty(Bar(ctx, h.Style)); // no bar band: only the spinner's spokes use the indicator color, as lines
    }

    [Fact]
    public void Overlay_SpinnerIsCenteredInTheScrim()
    {
        var h = Busy(kind: KumikoBusyIndicatorKind.Overlay);

        var ctx = h.Render();

        var scrim = Assert.Single(Scrim(ctx, h.Style)).Rect;
        var spokes = Spokes(ctx, h.Style);
        Assert.Equal(12, spokes.Count);
        float cx = spokes.Average(o => (o.X1 + o.X2) / 2);
        float cy = spokes.Average(o => (o.Y1 + o.Y2) / 2);
        Assert.Equal(scrim.X + scrim.Width / 2, cx, 1);
        Assert.Equal(scrim.Y + scrim.Height / 2, cy, 1);
    }

    [Fact]
    public void Overlay_SitsBelowTheGroupingPanelAndTopSummaries_AndAboveAPinnedFooter()
    {
        var h = Busy(x =>
        {
            x.Style.PinBottomSummaryRows = true;
            x.Source.AddGroupDescription(new GroupDescription("City"));
            x.Source.AddTableSummaryRow(Summary(SummaryPosition.Top));
            x.Source.AddTableSummaryRow(Summary(SummaryPosition.Bottom));
        }, KumikoBusyIndicatorKind.Overlay);

        var ctx = h.Render();

        var scrim = Assert.Single(Scrim(ctx, h.Style));
        float top = h.Style.GroupPanelHeight + h.Style.HeaderHeight + h.Style.SummaryRowHeight;
        Assert.Equal(top, scrim.Rect.Y, 2);
        Assert.Equal(400f - h.Style.SummaryRowHeight - top, scrim.Rect.Height, 2);
        int footer = ctx.Ops.Where(o => o.Kind == DrawOpKind.FillRect && SameRgb(o.Color, h.Style.SummaryRowBackgroundColor))
            .Max(o => o.Index);
        Assert.True(scrim.Index > footer, "the scrim is drawn after the footer");
    }

    [Fact]
    public void Overlay_Animated_TurnsTheSpinner_AndWithAnimationsOffIsStill()
    {
        var h = Busy(kind: KumikoBusyIndicatorKind.Overlay);
        h.Renderer.BusyClock = () => 0;
        var a = Spokes(h.Render(), h.Style).Select(o => o.Color.A).ToList();
        h.Renderer.BusyClock = () => 450;
        var b = Spokes(h.Render(), h.Style).Select(o => o.Color.A).ToList();
        Assert.True(h.Renderer.IsBusyIndicatorAnimating);
        Assert.NotEqual(a, b);

        h.Style.EnableAnimations = false;
        h.Renderer.BusyClock = () => 0;
        var s1 = Spokes(h.Render(), h.Style).Select(o => o.Color.A).ToList();
        h.Renderer.BusyClock = () => 450;
        var s2 = Spokes(h.Render(), h.Style).Select(o => o.Color.A).ToList();
        Assert.False(h.Renderer.IsBusyIndicatorAnimating);
        Assert.Equal(s1, s2);
    }

    [Fact]
    public void ADisconnectedAnimationSwitch_OnTheController_AlsoMakesItStatic()
    {
        var h = Busy();
        var input = new KumikoUI.Core.Input.KumikoInputController { AnimationsEnabled = false };
        h.Renderer.InputController = input;

        h.Render();

        Assert.False(h.Renderer.IsBusyIndicatorAnimating);
    }

    // ── Style and themes ──

    [Fact]
    public void TheStyleDefaults_AreABar_InTheAccentColor()
    {
        var style = new KumikoStyle();

        Assert.Equal(KumikoBusyIndicatorKind.Bar, style.BusyIndicatorKind);
        Assert.Equal(style.AccentColor, style.BusyIndicatorColor);
    }

    [Fact]
    public void TheThemes_PickIndicatorAndScrimColorsThatFitTheirBackground()
    {
        var light = KumikoTheme.CreateLight();
        var dark = KumikoTheme.CreateDark();
        var contrast = KumikoTheme.CreateHighContrast();

        Assert.True(light.BusyOverlayColor.R > 200, "a light scrim over a light grid");
        Assert.True(dark.BusyOverlayColor.R < 60, "a dark scrim over a dark grid");
        Assert.True(contrast.BusyOverlayColor.R < 10 && contrast.BusyOverlayColor.A >= 180, "an almost opaque black scrim");
        Assert.NotEqual(light.BusyIndicatorColor, dark.BusyIndicatorColor);
        Assert.Equal(new KumikoColor(255, 255, 0), contrast.BusyIndicatorColor);
    }

    [Fact]
    public void TheAnnouncementText_IsLocalizable()
    {
        Assert.Equal("Loading", new KumikoStrings().AnnounceBusy);
        Assert.Equal("Cargando", (new KumikoStrings() with { AnnounceBusy = "Cargando" }).AnnounceBusy);
    }
}

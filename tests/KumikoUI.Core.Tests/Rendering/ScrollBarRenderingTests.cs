using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.Input;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.Rendering;

/// <summary>
/// The grid's scroll bars as drawn (#316): where the thumb is and how long, across the bands around the scrolling area
/// (grouping panel, header, top summaries, frozen rows, a pinned footer, frozen columns, the drag handle column), and
/// when each visibility mode draws a bar.
/// </summary>
public class ScrollBarRenderingTests
{
    // The idle thumb is ScrollBarThickness (4) thick, 2 px in from the outer edge; the pointer band is 10 px.
    private const float Thin = 4f, Inset = 2f, Band = 10f;

    private static List<DrawOp> Thumbs(RecordingDrawingContext ctx, KumikoStyle style) =>
        ctx.Ops.Where(o => o.Kind == DrawOpKind.FillRoundRect && SameRgb(o.Color, style.ScrollBarThumbColor)).ToList();

    private static List<DrawOp> Tracks(RecordingDrawingContext ctx, KumikoStyle style) =>
        ctx.Ops.Where(o => o.Kind == DrawOpKind.FillRect && SameRgb(o.Color, style.ScrollBarTrackColor)).ToList();

    private static bool SameRgb(KumikoColor a, KumikoColor b) => a.R == b.R && a.G == b.G && a.B == b.B;

    private static DrawOp VerticalThumb(RecordingDrawingContext ctx, KumikoStyle style) =>
        Assert.Single(Thumbs(ctx, style), o => o.Rect.Height > o.Rect.Width);

    private static DrawOp HorizontalThumb(RecordingDrawingContext ctx, KumikoStyle style) =>
        Assert.Single(Thumbs(ctx, style), o => o.Rect.Width > o.Rect.Height);

    private static void AssertRect(KumikoRect expected, KumikoRect actual)
    {
        Assert.Equal(expected.X, actual.X, 2);
        Assert.Equal(expected.Y, actual.Y, 2);
        Assert.Equal(expected.Width, actual.Width, 2);
        Assert.Equal(expected.Height, actual.Height, 2);
    }

    private static RenderHarness Always(int rows = 20, float width = 600, float height = 400,
        Action<RenderHarness>? configure = null) =>
        RenderHarness.Create(rows, width, height, h =>
        {
            h.Style.VerticalScrollBarVisibility = KumikoScrollBarVisibility.Always;
            h.Style.HorizontalScrollBarVisibility = KumikoScrollBarVisibility.Always;
            configure?.Invoke(h);
        });

    /// <summary>An <see cref="InputGrid"/> (300 x 256, connected controller) whose bar colors no other paint shares.</summary>
    private static InputGrid Grid(Action<InputGrid>? configure = null) =>
        new(configure: x =>
        {
            x.Style.ScrollBarThumbColor = new KumikoColor(1, 2, 3, 110);
            x.Style.ScrollBarTrackColor = new KumikoColor(4, 5, 6, 20);
            configure?.Invoke(x);
        });

    private static TableSummaryRow Summary(SummaryPosition position) => new("Totals", position)
    {
        Columns = { new SummaryColumnDescription { PropertyName = "Salary", SummaryType = SummaryType.Sum } },
    };

    // ── Geometry ──

    [Fact]
    public void Vertical_ThumbIsTheVisibleShareOfTheRows_AtTheRightEdgeBelowTheHeader()
    {
        // 20 rows of 36 below a 40 px header: 760 px of content in 400, so 360 px of rows show 360 of 720 scrolling.
        var h = Always();

        var ctx = h.Render();

        AssertRect(new KumikoRect(600 - Inset - Thin, 40, Thin, 180), VerticalThumb(ctx, h.Style).Rect);
        Assert.DoesNotContain(Thumbs(ctx, h.Style), o => o.Rect.Width > o.Rect.Height); // 550 px of columns fit in 600
    }

    [Fact]
    public void Vertical_ThumbMovesWithTheOffset_AndEndsAtTheBottomOfTheRows()
    {
        var h = Always();

        h.Scroll.OffsetY = 180; // half way
        AssertRect(new KumikoRect(594, 40 + 90, Thin, 180), VerticalThumb(h.Render(), h.Style).Rect);

        h.Scroll.OffsetY = 360; // the end
        Assert.Equal(400, VerticalThumb(h.Render(), h.Style).Rect.Bottom, 2);
    }

    [Fact]
    public void BothBars_WithFrozenRowsAndColumns_TopSummary_PinnedFooter_AndALeftDragHandle()
    {
        // 500 x 400: drag handle 32 on the left, Name (150) frozen left, Salary (150) frozen right. Rows band: below the
        // header (40), a top summary (36) and two frozen rows (72), above the pinned footer (36) = 148..364, 216 px.
        // Content height 40 + 20 * 36 + 2 * 36 = 832, so the rows scroll 432 px. Scrolling columns: 182..350 (168 px)
        // of 582 - 32 - 300 = 250 px, so 82 px of horizontal scrolling.
        var h = Always(width: 500, configure: x =>
        {
            x.Style.ShowRowDragHandle = true;
            x.Column("Name").FreezeMode = ColumnFreezeMode.Left;
            x.Column("Salary").FreezeMode = ColumnFreezeMode.Right;
            x.Source.FrozenRowCount = 2;
            x.Source.AddTableSummaryRow(Summary(SummaryPosition.Top));
            x.Source.AddTableSummaryRow(Summary(SummaryPosition.Bottom));
        });
        h.Scroll.OffsetY = 216; // half of 432
        h.Scroll.OffsetX = 41;  // half of 82

        var ctx = h.Render();

        // Vertical: the track stops above the horizontal bar's band (148..354, 206 px); thumb 206 * 216 / 648.
        float vThumb = 206f * 216 / 648;
        AssertRect(new KumikoRect(500 - Inset - Thin, 148 + (206 - vThumb) / 2, Thin, vThumb), VerticalThumb(ctx, h.Style).Rect);

        // Horizontal: across the scrolling columns only, at the bottom of the rows above the footer; thumb 168 * 168 / 250.
        float hThumb = 168f * 168 / 250;
        AssertRect(new KumikoRect(182 + (168 - hThumb) / 2, 364 - Inset - Thin, hThumb, Thin), HorizontalThumb(ctx, h.Style).Rect);
    }

    [Fact]
    public void Vertical_StartsBelowTheGroupingPanel()
    {
        var h = Always(configure: x => x.Source.AddGroupDescription(new GroupDescription("City")));

        var thumb = VerticalThumb(h.Render(), h.Style);

        Assert.Equal(h.Style.GroupPanelHeight + h.Style.HeaderHeight, thumb.Rect.Y, 2);
    }

    [Fact]
    public void Horizontal_WithARightDragHandle_StopsAtTheHandle()
    {
        // 300 px: columns get 268, the handle 268..300; the vertical bar's band (290..300) lies over the handle.
        var h = Always(width: 300, configure: x =>
        {
            x.Style.ShowRowDragHandle = true;
            x.Style.RowDragHandlePosition = DragHandlePosition.Right;
        });

        var thumb = HorizontalThumb(h.Render(), h.Style);

        Assert.Equal(0, thumb.Rect.X, 2);
        float track = 268, thumbLength = track * 268 / 550;
        Assert.Equal(thumbLength, thumb.Rect.Width, 2);
    }

    [Fact]
    public void Thumb_IsNeverShorterThanTheMinimum()
    {
        var h = Always(rows: 2000);

        var thumb = VerticalThumb(h.Render(), h.Style);

        Assert.Equal(h.Style.ScrollBarMinThumbLength, thumb.Rect.Height, 2);
    }

    [Fact]
    public void NoBar_WhenTheContentFits()
    {
        var h = Always(rows: 5); // 40 + 5 * 36 in 400; 550 px of columns in 600

        Assert.Empty(Thumbs(h.Render(), h.Style));
    }

    [Fact]
    public void Thumb_FollowsTheContentExtentTheScrollStateReports()
    {
        // The bars read the extent only from ScrollState: a keyboard inset (more to scroll into) shortens the thumb.
        var h = Always();
        float before = VerticalThumb(h.Render(), h.Style).Rect.Height;

        h.Scroll.BottomInset = 360;

        Assert.Equal(360f * 360 / 1080, VerticalThumb(h.Render(), h.Style).Rect.Height, 2);
        Assert.True(VerticalThumb(h.Render(), h.Style).Rect.Height < before);
    }

    [Fact]
    public void Bars_AreDrawnAfterTheRowsHandleAndFooter_BeforeTheEditor()
    {
        var h = Always(width: 500, configure: x =>
        {
            x.Style.ShowRowDragHandle = true;
            x.Style.RowDragHandlePosition = DragHandlePosition.Right;
            x.Source.AddTableSummaryRow(Summary(SummaryPosition.Bottom));
        });

        var ctx = h.Render();

        int firstBar = Thumbs(ctx, h.Style).Min(o => o.Index);
        Assert.All(ctx.Ops.Where(o => o.Kind != DrawOpKind.FillRoundRect && !SameRgb(o.Color, h.Style.ScrollBarThumbColor)),
            o => Assert.True(o.Index < firstBar, $"drawn over the bar: {o}"));
        Assert.All(Thumbs(ctx, h.Style), o => Assert.Null(o.Clip)); // viewport coordinates, unclipped
    }

    // ── Visibility ──

    [Fact]
    public void Never_DrawsNoBar()
    {
        var h = Always(width: 300);
        h.Style.VerticalScrollBarVisibility = KumikoScrollBarVisibility.Never;
        h.Style.HorizontalScrollBarVisibility = KumikoScrollBarVisibility.Never;

        Assert.Empty(Thumbs(h.Render(), h.Style));
    }

    [Fact]
    public void Auto_WithoutAConnectedController_DrawsNoBar()
    {
        var h = RenderHarness.Create(width: 300);
        h.Render();
        h.Scroll.OffsetY = 100;

        Assert.Empty(Thumbs(h.Render(), h.Style));
    }

    [Fact]
    public void Auto_ShowsTheBarThatScrolled_HoldsIt_ThenFadesItOut()
    {
        var g = Grid(x => x.Input.AnimationsEnabled = true);
        Assert.Empty(Thumbs(g.RenderRecorded(), g.Style)); // nothing scrolled yet

        g.Scroll.OffsetY = 100;
        var shown = Thumbs(g.RenderRecorded(), g.Style);

        var thumb = Assert.Single(shown); // the vertical one only: nothing scrolled sideways
        Assert.True(thumb.Rect.Height > thumb.Rect.Width);
        Assert.Equal(g.Style.ScrollBarThumbColor.A, thumb.Color.A);
        Assert.True(g.Input.IsAnimating);

        int redraws = g.Redraws;
        g.Input.UpdateAnimations(InteractionAnimations.ScrollBarHoldMs - 1);
        Assert.Equal(redraws, g.Redraws); // the hold draws nothing new
        Assert.Equal(g.Style.ScrollBarThumbColor.A, Assert.Single(Thumbs(g.RenderRecorded(), g.Style)).Color.A);

        g.Input.UpdateAnimations(1 + InteractionAnimations.ScrollBarFadeMs / 2);
        Assert.True(g.Redraws > redraws);
        Assert.Equal((byte)MathF.Round(g.Style.ScrollBarThumbColor.A * 0.5f), Assert.Single(Thumbs(g.RenderRecorded(), g.Style)).Color.A);

        Assert.False(g.Input.UpdateAnimations(InteractionAnimations.ScrollBarFadeMs));
        Assert.Empty(Thumbs(g.RenderRecorded(), g.Style));
        Assert.False(g.Input.IsAnimating);
    }

    [Fact]
    public void Auto_KeepsShowingWhileTheGridKeepsScrolling()
    {
        var g = Grid(x => x.Input.AnimationsEnabled = true);
        for (int i = 1; i <= 5; i++)
        {
            g.Scroll.OffsetY = i * 50f;
            g.RenderRecorded();
            g.Input.UpdateAnimations(InteractionAnimations.ScrollBarHoldMs / 2);
        }

        Assert.Equal(g.Style.ScrollBarThumbColor.A, Assert.Single(Thumbs(g.RenderRecorded(), g.Style)).Color.A);
    }

    [Theory]
    [InlineData(false, true)]  // the host does not animate (reduced motion)
    [InlineData(true, false)]  // the style turns animations off
    public void Auto_WithoutAnimations_ShowsForTheHoldThenHidesWithoutFading(bool hostAnimates, bool styleAnimates)
    {
        var g = Grid(x =>
        {
            x.Input.AnimationsEnabled = hostAnimates;
            x.Style.EnableAnimations = styleAnimates;
        });
        g.Scroll.OffsetX = 100;
        var thumb = Assert.Single(Thumbs(g.RenderRecorded(), g.Style));
        Assert.True(thumb.Rect.Width > thumb.Rect.Height); // the horizontal bar

        g.Input.UpdateAnimations(InteractionAnimations.ScrollBarHoldMs - 1);
        Assert.Equal(g.Style.ScrollBarThumbColor.A, Assert.Single(Thumbs(g.RenderRecorded(), g.Style)).Color.A);

        g.Input.UpdateAnimations(2);
        Assert.Empty(Thumbs(g.RenderRecorded(), g.Style));
    }

    [Fact]
    public void Always_IsDrawnWithoutScrolling_AndWithoutAController()
    {
        var h = Always(width: 300);

        var ctx = h.Render();

        Assert.Equal(2, Thumbs(ctx, h.Style).Count);
        Assert.Empty(Tracks(ctx, h.Style)); // the track only shows expanded
    }

    // ── Expanded ──

    [Fact]
    public void HoveredBar_ExpandsOverItsTrack_AndShowsInAuto()
    {
        var g = Grid();

        g.Hover(295, 150);
        var ctx = g.RenderRecorded();

        // InputGrid: 300 x 256, rows band 40..256, the horizontal bar's band 246..256 below the vertical track
        var track = Assert.Single(Tracks(ctx, g.Style));
        AssertRect(new KumikoRect(300 - Band, 40, Band, 206), track.Rect);
        var thumb = Assert.Single(Thumbs(ctx, g.Style));
        Assert.Equal(300 - Inset - (Band - 2 * Inset), thumb.Rect.X, 2);
        Assert.Equal(Band - 2 * Inset, thumb.Rect.Width, 2);
        Assert.True(track.Index < thumb.Index);

        g.Hover(150, 150); // away: an Auto bar holds, then hides
        ctx = g.RenderRecorded();
        Assert.Empty(Tracks(ctx, g.Style));
        Assert.Equal(Thin, Assert.Single(Thumbs(ctx, g.Style)).Rect.Width, 2);
        g.Input.UpdateAnimations(InteractionAnimations.ScrollBarHoldMs + InteractionAnimations.ScrollBarFadeMs);
        Assert.Empty(Thumbs(g.RenderRecorded(), g.Style));
    }

    [Fact]
    public void ScrollBarColors_FollowTheTheme()
    {
        var dark = KumikoTheme.CreateDark();
        var contrast = KumikoTheme.CreateHighContrast();

        Assert.Equal(new KumikoColor(255, 255, 255, 110), dark.ScrollBarThumbColor);
        Assert.Equal(new KumikoColor(255, 255, 255, 20), dark.ScrollBarTrackColor);
        Assert.Equal(new KumikoColor(0, 255, 255), contrast.ScrollBarThumbColor);
        Assert.Equal(new KumikoColor(0, 0, 0, 110), new KumikoStyle().ScrollBarThumbColor);
    }
}

using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Tests.Rendering;

/// <summary>
/// The thumb math shared by the grid's scroll bars, the combo box drop-down and the filter popup list (#316), the clock
/// that shows and fades an Auto bar, and the host option that sets a bar's visibility.
/// </summary>
public class ScrollThumbTests
{
    [Theory]
    [InlineData(200, 100, 400, 20, 50)]   // a quarter of the content shows: a quarter of the track
    [InlineData(200, 100, 10_000, 20, 20)] // never shorter than the minimum
    [InlineData(10, 100, 10_000, 20, 10)]  // nor longer than the track
    [InlineData(200, 400, 400, 20, 200)]   // everything shows: all thumb
    [InlineData(200, 100, 0, 20, 200)]     // no content
    [InlineData(0, 100, 400, 20, 0)]       // no track
    public void Length_IsTheVisibleShareOfTheTrack(float track, float viewport, float content, float min, float expected) =>
        Assert.Equal(expected, ScrollThumb.Length(track, viewport, content, min), 3);

    [Theory]
    [InlineData(0, 10)]
    [InlineData(150, 85)]  // half way: 10 + 75
    [InlineData(300, 160)] // the end: 10 + 200 - 50
    [InlineData(900, 160)] // past the end: clamped
    public void Start_MovesTheThumbAcrossItsTravel(float offset, float expected) =>
        Assert.Equal(expected, ScrollThumb.Start(10, 200, 50, offset, 300), 3);

    [Fact]
    public void OffsetAt_IsTheInverseOfStart()
    {
        float start = ScrollThumb.Start(10, 200, 50, 120, 300);

        Assert.Equal(120, ScrollThumb.OffsetAt(start, 10, 200, 50, 300), 3);
        Assert.True(float.IsNaN(ScrollThumb.OffsetAt(start, 10, 50, 50, 300))); // a thumb that fills the track
    }

    [Fact]
    public void Fill_RoundsTheEndsFully()
    {
        var ctx = new TestHelpers.RecordingDrawingContext();
        var paint = new KumikoPaint();

        ScrollThumb.Fill(ctx, new KumikoRect(0, 0, 4, 40), paint);

        var op = Assert.Single(ctx.Ops);
        Assert.Equal(TestHelpers.DrawOpKind.FillRoundRect, op.Kind);
    }

    // ── The Auto bar's clock ──

    [Fact]
    public void Clock_HoldsThenFades_EachBarOnItsOwn()
    {
        var clock = new InteractionAnimations();
        clock.ShowScrollBar(vertical: true);

        Assert.True(clock.IsActive);
        Assert.Equal(0f, clock.ScrollBarOpacity(vertical: false, fade: true));
        clock.Update(InteractionAnimations.ScrollBarHoldMs / 2);
        Assert.False(clock.ChangedVisibly);
        clock.ShowScrollBar(vertical: false);
        clock.Update(InteractionAnimations.ScrollBarHoldMs / 2 + InteractionAnimations.ScrollBarFadeMs / 2);

        Assert.True(clock.ChangedVisibly);
        Assert.Equal(0.5f, clock.ScrollBarOpacity(vertical: true, fade: true), 3);
        Assert.Equal(0f, clock.ScrollBarOpacity(vertical: true, fade: false));
        Assert.Equal(1f, clock.ScrollBarOpacity(vertical: false, fade: true));

        clock.StopAll();
        Assert.False(clock.IsActive);
        Assert.Equal(0f, clock.ScrollBarOpacity(vertical: false, fade: true));
    }

    // ── Host option ──

    [Fact]
    public void StyleOptions_ScrollBarVisibility_IsAnOverrideThatSurvivesATheme()
    {
        var options = new KumikoStyleOptions { VerticalScrollBarVisibility = KumikoScrollBarVisibility.Never };
        Assert.True(options.HasOverrides);

        var style = KumikoTheme.CreateDark();
        options.Apply(style);

        Assert.Equal(KumikoScrollBarVisibility.Never, style.VerticalScrollBarVisibility);
        Assert.Equal(KumikoScrollBarVisibility.Auto, style.HorizontalScrollBarVisibility); // not set: the theme's
    }
}

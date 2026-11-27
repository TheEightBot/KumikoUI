using KumikoUI.Core.Input;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests.Input;

/// <summary>
/// Pull-to-refresh (#330): a finger or pen pan that starts at the top and moves down pulls the indicator instead of
/// scrolling; a release past the threshold refreshes, one short of it lets go. Mice, flings and pans that start lower
/// never pull, and a second pull is ignored while refreshing.
/// </summary>
public class PullToRefreshTests
{
    private const float X = 150f;
    private const float StartY = 60f;

    // The pan starts once the finger moved past the pan threshold (10 px), and that first stretch never scrolls or pulls.
    private const float PanStart = 11f;

    private long _clock = 1;

    private static InputGrid Grid(bool enabled = true, int rows = 30) =>
        new(rows: rows, configure: g =>
        {
            g.Input.IsPullToRefreshEnabled = enabled;
            g.Input.AnimationsEnabled = true;
        });

    private KumikoPointerEventArgs Touch(InputGrid g, InputAction action, float x, float y,
        KumikoPointerDeviceType device = KumikoPointerDeviceType.Touch)
    {
        _clock += 16;
        return g.Pointer(action, x, y, timestampMs: _clock, deviceType: device);
    }

    /// <summary>Presses at <see cref="StartY"/> and drags straight down by <paramref name="fingerTravel"/> past the pan threshold, in 10 px steps.</summary>
    private void Drag(InputGrid g, float fingerTravel, KumikoPointerDeviceType device = KumikoPointerDeviceType.Touch,
        float startY = StartY)
    {
        Touch(g, InputAction.Pressed, X, startY, device);
        Touch(g, InputAction.Moved, X, startY + PanStart, device);
        MoveBy(g, startY + PanStart, fingerTravel, device);
    }

    private void MoveBy(InputGrid g, float fromY, float travel, KumikoPointerDeviceType device = KumikoPointerDeviceType.Touch)
    {
        int steps = (int)MathF.Ceiling(MathF.Abs(travel) / 10f);
        for (int i = 1; i <= steps; i++)
            Touch(g, InputAction.Moved, X, fromY + travel * i / steps, device);
    }

    private static void Settle(KumikoInputController input)
    {
        for (int i = 0; i < 100 && input.PullToRefresh.IsTransitioning; i++)
            input.UpdateAnimations(16);
    }

    private static List<string> Track(KumikoInputController input)
    {
        var events = new List<string>();
        input.RefreshRequested += (_, _) => events.Add("RefreshRequested");
        input.IsRefreshingChanged += (s, _) => events.Add($"IsRefreshingChanged:{((KumikoInputController)s!).IsRefreshing}");
        return events;
    }

    // ── Pulling from the top ──

    [Fact]
    public void Pull_PastTheThreshold_RefreshesOnRelease_AndHoldsTheIndicator()
    {
        var g = Grid();
        var events = Track(g.Input);

        Drag(g, fingerTravel: 140); // 70 px of indicator travel, past the 64 px threshold
        Assert.True(g.Input.PullToRefresh.IsTracking);
        Assert.Equal(70f, g.Input.PullToRefresh.Distance, 0.01f);
        Assert.Equal(0f, g.Scroll.OffsetY, 0.01f); // the rows do not move
        Assert.Empty(events);

        var up = Touch(g, InputAction.Released, X, StartY + PanStart + 140);

        Assert.True(up.Handled);
        Assert.True(g.Input.IsRefreshing);
        Assert.Equal(new[] { "IsRefreshingChanged:True", "RefreshRequested" }, events);
        Assert.False(g.Input.IsInertialScrolling); // a pull does not fling
        Settle(g.Input);
        Assert.Equal(g.Input.PullToRefreshThreshold, g.Input.PullToRefresh.Distance, 0.01f);
    }

    [Fact]
    public void Pull_BelowTheThreshold_GoesBackWithoutRefreshing()
    {
        var g = Grid();
        var events = Track(g.Input);

        Drag(g, fingerTravel: 100); // 50 px
        Assert.Equal(50f, g.Input.PullToRefresh.Distance, 0.01f);
        Touch(g, InputAction.Released, X, StartY + PanStart + 100);

        Assert.False(g.Input.IsRefreshing);
        Assert.Empty(events);
        Assert.True(g.Input.IsAnimating); // it slides back
        Settle(g.Input);
        Assert.False(g.Input.PullToRefresh.IsVisible);
        Assert.False(g.Input.IsAnimating);
    }

    [Fact]
    public void Pull_FollowsTheFingerWithResistance_UpToTwiceTheThreshold()
    {
        var g = Grid();

        Drag(g, fingerTravel: 600);

        Assert.Equal(2 * g.Input.PullToRefreshThreshold, g.Input.PullToRefresh.Distance, 0.01f);
    }

    [Fact]
    public void Pull_ReversedPastTheStart_ScrollsTheRestNormally()
    {
        var g = Grid();
        var events = Track(g.Input);
        float y = StartY + PanStart;

        Drag(g, fingerTravel: 80); // 40 px of pull
        MoveBy(g, y + 80, -60); // back up: 10 px of pull left
        Assert.Equal(10f, g.Input.PullToRefresh.Distance, 0.01f);
        Assert.Equal(0f, g.Scroll.OffsetY, 0.01f);

        MoveBy(g, y + 20, -70); // 20 px more takes the pull back to 0, the other 50 scroll
        Assert.False(g.Input.PullToRefresh.IsVisible);
        Assert.Equal(50f, g.Scroll.OffsetY, 0.01f);

        Touch(g, InputAction.Released, X, y - 50);
        Assert.False(g.Input.IsRefreshing);
        Assert.Empty(events);
        Assert.False(g.Input.PullToRefresh.IsTracking);
    }

    [Fact]
    public void Pull_FromAPanThatStartedBelowTheTop_NeverStarts()
    {
        var g = Grid();
        g.Scroll.OffsetY = 60;
        var events = Track(g.Input);

        Drag(g, fingerTravel: 200); // scrolls to the top, then keeps going

        Assert.Equal(0f, g.Scroll.OffsetY, 0.01f);
        Assert.False(g.Input.PullToRefresh.IsVisible);
        Touch(g, InputAction.Released, X, StartY + PanStart + 200);
        Assert.False(g.Input.IsRefreshing);
        Assert.Empty(events);
    }

    [Fact]
    public void AFlingThatReachesTheTop_NeverPulls()
    {
        var g = Grid();
        g.Scroll.OffsetY = 300;
        var events = Track(g.Input);

        // A quick flick down: 30 px every 16 ms
        Touch(g, InputAction.Pressed, X, 40);
        for (int i = 1; i <= 6; i++) Touch(g, InputAction.Moved, X, 40 + i * 30f);
        Touch(g, InputAction.Released, X, 220);
        Assert.True(g.Input.IsInertialScrolling);

        for (int i = 0; i < 500 && g.Input.UpdateInertialScroll(g.Scroll); i++) { }

        Assert.Equal(0f, g.Scroll.OffsetY, 0.01f);
        Assert.False(g.Input.PullToRefresh.IsVisible);
        Assert.False(g.Input.IsRefreshing);
        Assert.Empty(events);
    }

    [Fact]
    public void AMouseDrag_NeverPulls_ButAPenDoes()
    {
        var g = Grid();

        Drag(g, fingerTravel: 200, device: KumikoPointerDeviceType.Mouse);
        Touch(g, InputAction.Released, X, StartY + PanStart + 200, KumikoPointerDeviceType.Mouse);
        Assert.False(g.Input.PullToRefresh.IsVisible);
        Assert.False(g.Input.IsRefreshing);

        Drag(g, fingerTravel: 200, device: KumikoPointerDeviceType.Pen);
        Touch(g, InputAction.Released, X, StartY + PanStart + 200, KumikoPointerDeviceType.Pen);
        Assert.True(g.Input.IsRefreshing);
    }

    [Fact]
    public void TheWheel_NeverPulls()
    {
        var g = Grid();

        var e = g.Pointer(InputAction.Scroll, X, StartY, scrollDy: 5, deviceType: KumikoPointerDeviceType.Touch);

        Assert.False(e.Handled); // at the top already: the page scrolls on
        Assert.False(g.Input.PullToRefresh.IsVisible);
    }

    [Fact]
    public void AMostlySidewaysPan_NeverPulls()
    {
        var g = Grid();

        Touch(g, InputAction.Pressed, 200, StartY);
        Touch(g, InputAction.Moved, 180, StartY + 6); // starts the pan mostly sideways
        Touch(g, InputAction.Moved, 170, StartY + 150);

        Assert.False(g.Input.PullToRefresh.IsVisible);
    }

    [Fact]
    public void Pull_WhenDisabled_DoesNothing()
    {
        var g = Grid(enabled: false);

        Drag(g, fingerTravel: 200);
        Touch(g, InputAction.Released, X, StartY + PanStart + 200);

        Assert.False(g.Input.PullToRefresh.IsVisible);
        Assert.False(g.Input.IsRefreshing);
    }

    [Fact]
    public void Pull_OnAGridThatCannotScroll_Works()
    {
        var g = Grid(rows: 2);

        Drag(g, fingerTravel: 140);
        Touch(g, InputAction.Released, X, StartY + PanStart + 140);

        Assert.True(g.Input.IsRefreshing);
    }

    [Fact]
    public void ACanceledPull_PastTheThreshold_GoesBackWithoutRefreshing()
    {
        var g = Grid();

        Drag(g, fingerTravel: 200);
        Touch(g, InputAction.Cancelled, X, StartY + PanStart + 200);

        Assert.False(g.Input.IsRefreshing);
        Settle(g.Input);
        Assert.False(g.Input.PullToRefresh.IsVisible);
    }

    // ── While refreshing ──

    [Fact]
    public void ASecondPull_WhileRefreshing_IsIgnored_AndScrollsInstead()
    {
        var g = Grid();
        Drag(g, fingerTravel: 140);
        Touch(g, InputAction.Released, X, StartY + PanStart + 140);
        Settle(g.Input);
        var events = Track(g.Input);

        Drag(g, fingerTravel: 200);
        Assert.Equal(g.Input.PullToRefreshThreshold, g.Input.PullToRefresh.Distance, 0.01f); // held where it was
        Assert.False(g.Input.PullToRefresh.IsTracking);
        Touch(g, InputAction.Released, X, StartY + PanStart + 200);

        Assert.Empty(events);
        Assert.True(g.Input.IsRefreshing);
    }

    [Fact]
    public void AfterTheRefreshEnds_TheIndicatorSlidesAway_AndAPullWorksAgain()
    {
        var g = Grid();
        Drag(g, fingerTravel: 140);
        Touch(g, InputAction.Released, X, StartY + PanStart + 140);
        Settle(g.Input);

        g.Input.IsRefreshing = false;
        Assert.True(g.Input.PullToRefresh.IsTransitioning);
        Settle(g.Input);
        Assert.False(g.Input.PullToRefresh.IsVisible);

        var events = Track(g.Input);
        Drag(g, fingerTravel: 140);
        Touch(g, InputAction.Released, X, StartY + PanStart + 140);
        Assert.Equal(new[] { "IsRefreshingChanged:True", "RefreshRequested" }, events);
    }

    [Fact]
    public void IsRefreshing_SetInCode_ShowsAndHidesTheIndicator_WithoutRequestingARefresh()
    {
        var g = Grid(enabled: false); // works without the gesture too
        var events = Track(g.Input);

        g.Input.IsRefreshing = true;
        Settle(g.Input);
        Assert.Equal(g.Input.PullToRefreshThreshold, g.Input.PullToRefresh.Distance, 0.01f);

        g.Input.IsRefreshing = false;
        Settle(g.Input);
        Assert.False(g.Input.PullToRefresh.IsVisible);
        Assert.Equal(new[] { "IsRefreshingChanged:True", "IsRefreshingChanged:False" }, events);
    }

    [Fact]
    public void IsRefreshing_SetToTheSameValue_RaisesNothing()
    {
        var g = Grid();
        var events = Track(g.Input);

        g.Input.IsRefreshing = false;

        Assert.Empty(events);
    }

    [Fact]
    public void IsRefreshing_SetDuringAPull_EndsThePull()
    {
        var g = Grid();
        Drag(g, fingerTravel: 60);

        g.Input.IsRefreshing = true;
        MoveBy(g, StartY + PanStart + 60, 100);
        var events = Track(g.Input);
        Touch(g, InputAction.Released, X, StartY + PanStart + 160);

        Assert.False(g.Input.PullToRefresh.IsTracking);
        Assert.Empty(events); // no second refresh
        Settle(g.Input);
        Assert.Equal(g.Input.PullToRefreshThreshold, g.Input.PullToRefresh.Distance, 0.01f);
    }

    // ── Feedback and frames ──

    [Fact]
    public void CrossingTheThreshold_RequestsOneHaptic()
    {
        var g = Grid();
        var kinds = new List<KumikoHapticKind>();
        g.Input.HapticFeedbackRequested += (_, e) => kinds.Add(e.Kind);

        Drag(g, fingerTravel: 200);
        MoveBy(g, StartY + PanStart + 200, -100); // back below the threshold
        MoveBy(g, StartY + PanStart + 100, 100); // and past it again

        Assert.Equal(new[] { KumikoHapticKind.RefreshThreshold, KumikoHapticKind.RefreshThreshold }, kinds);
    }

    [Fact]
    public void WhileRefreshing_TheSpinnerAnimates_AndStopsWithReducedMotion()
    {
        var g = Grid();
        g.Input.IsRefreshing = true;
        Settle(g.Input);

        Assert.True(g.Input.IsAnimating); // the spinner turns
        float before = g.Input.PullToRefresh.SpinMs;
        Assert.True(g.Input.UpdateAnimations(100));
        Assert.Equal(before + 100, g.Input.PullToRefresh.SpinMs, 0.01f);

        g.Style.EnableAnimations = false;
        g.Render(); // the renderer passes the style on
        Assert.False(g.Input.IsAnimating);
        Assert.False(g.Input.UpdateAnimations(16));
    }

    [Fact]
    public void WithReducedMotion_TheIndicatorMovesAtOnce()
    {
        var g = Grid();
        g.Input.AnimationsEnabled = false;

        g.Input.IsRefreshing = true;
        Assert.False(g.Input.IsAnimating);
        Assert.Equal(g.Input.PullToRefreshThreshold, g.Input.PullToRefresh.Distance, 0.01f);

        g.Input.IsRefreshing = false;
        Assert.False(g.Input.PullToRefresh.IsVisible);
        Assert.False(g.Input.IsAnimating);
    }

    [Fact]
    public void AHeldPull_RequestsNoFrames()
    {
        var g = Grid();

        Drag(g, fingerTravel: 100);

        Assert.False(g.Input.IsAnimating); // the finger moves it; each move redraws
    }

    [Fact]
    public void DisablingTheGrid_DuringAPull_LetsGoAtOnce()
    {
        var g = Grid();
        Drag(g, fingerTravel: 200);

        g.Input.IsEnabled = false;

        Assert.False(g.Input.PullToRefresh.IsVisible);
        Assert.False(g.Input.IsRefreshing);
        Assert.False(g.Input.IsAnimating);
    }

    [Fact]
    public void TurningThePullOff_DuringAPull_LetsGo()
    {
        var g = Grid();
        Drag(g, fingerTravel: 200);

        g.Input.IsPullToRefreshEnabled = false;
        Touch(g, InputAction.Released, X, StartY + PanStart + 200);

        Assert.False(g.Input.IsRefreshing);
        Settle(g.Input);
        Assert.False(g.Input.PullToRefresh.IsVisible);
    }

    [Fact]
    public void TheAnnouncement_FollowsTheState()
    {
        Assert.Equal("Refreshing", KumikoAnnouncements.RefreshStateChanged(KumikoStrings.Default, isRefreshing: true));
        Assert.Equal("Updated", KumikoAnnouncements.RefreshStateChanged(KumikoStrings.Default, isRefreshing: false));
        Assert.Equal("Neu", KumikoAnnouncements.RefreshStateChanged(
            KumikoStrings.Default with { AnnounceRefreshing = "Neu" }, isRefreshing: true));
        Assert.Throws<ArgumentNullException>(() => KumikoAnnouncements.RefreshStateChanged(null!, true));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-5f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void PullToRefreshThreshold_NotAPositiveNumber_RestoresTheDefault(float value)
    {
        var input = new KumikoInputController { PullToRefreshThreshold = 100 };

        input.PullToRefreshThreshold = value;

        Assert.Equal(64f, input.PullToRefreshThreshold, 0.01f);
    }

    [Fact]
    public void PullToRefreshThreshold_SetsHowFarAPullMustGo()
    {
        var g = Grid();
        g.Input.PullToRefreshThreshold = 100;

        Drag(g, fingerTravel: 180); // 90 px
        Touch(g, InputAction.Released, X, StartY + PanStart + 180);
        Assert.False(g.Input.IsRefreshing);

        Drag(g, fingerTravel: 210); // 105 px
        Touch(g, InputAction.Released, X, StartY + PanStart + 210);
        Assert.True(g.Input.IsRefreshing);
    }
}

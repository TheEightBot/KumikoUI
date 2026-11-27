using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Tests.Input;

/// <summary>
/// Swipe row actions (#329) and pull-to-refresh (#330) on one grid at the top of its rows: a sideways pan swipes the
/// row and never pulls, a downward pan pulls and never swipes, and a diagonal
/// pan between the two does neither (it scrolls).
/// </summary>
public class SwipeAndPullArbitrationTests
{
    private const float X = 150f;
    private const float Y = 60f;

    private long _clock = 1;

    private static InputGrid Grid() =>
        new(rows: 30, configure: g =>
        {
            g.Input.IsPullToRefreshEnabled = true;
            g.Input.AllowSwiping = true;
            g.Input.AnimationsEnabled = true;
            g.Input.TrailingSwipeItems.Add(new ActionButtonDefinition { Label = "Archive", Action = () => { } });
        });

    private void Touch(InputGrid g, InputAction action, float x, float y)
    {
        _clock += 16;
        g.Pointer(action, x, y, timestampMs: _clock, deviceType: KumikoPointerDeviceType.Touch);
    }

    private void Pan(InputGrid g, float dx, float dy)
    {
        Touch(g, InputAction.Pressed, X, Y);
        for (int i = 1; i <= 10; i++)
            Touch(g, InputAction.Moved, X + dx * i / 10, Y + dy * i / 10);
    }

    [Fact]
    public void ASidewaysPan_Swipes_AndDoesNotPull()
    {
        var g = Grid();

        Pan(g, dx: -80, dy: 4);

        Assert.True(g.Input.Swipe.IsActive);
        Assert.False(g.Input.PullToRefresh.IsTracking);
        Assert.Equal(0f, g.Input.PullToRefresh.Distance, 0.01f);
    }

    [Fact]
    public void ADownwardPan_Pulls_AndDoesNotSwipe()
    {
        var g = Grid();

        Pan(g, dx: 4, dy: 120);

        Assert.True(g.Input.PullToRefresh.IsTracking);
        Assert.False(g.Input.Swipe.IsActive);
    }

    [Fact]
    public void AMostlySidewaysDiagonalPan_NeitherSwipesNorPulls()
    {
        var g = Grid();

        Pan(g, dx: -60, dy: 40); // not twice as wide as tall (no swipe), not mostly downward (no pull)

        Assert.False(g.Input.Swipe.IsActive);
        Assert.False(g.Input.PullToRefresh.IsTracking);
    }
}

using KumikoUI.Core.Rendering;
using KumikoUI.Uno.Input;
using Windows.UI;

namespace KumikoUI.Uno.Tests;

/// <summary>
/// Tests for the pure pointer and color translation the Uno host uses: timestamp units, the
/// double-tap detector and the WinUI to Core color conversion.
/// </summary>
public class PointerGesturesTests
{
    // ── ToMillis ─────────────────────────────────────────────────

    [Theory]
    [InlineData(0UL, 0L)]
    [InlineData(999UL, 0L)]
    [InlineData(1_000UL, 1L)]
    [InlineData(1_999UL, 1L)]
    [InlineData(2_500_000UL, 2_500L)]
    public void ToMillis_ConvertsMicrosecondsToWholeMilliseconds(ulong micros, long expected)
    {
        Assert.Equal(expected, PointerGestures.ToMillis(micros));
    }

    // ── TapTracker ───────────────────────────────────────────────

    [Fact]
    public void RegisterPress_FirstPress_IsSingleClick()
    {
        var tracker = new PointerGestures.TapTracker();

        Assert.Equal(1, tracker.RegisterPress(1_000, 50, 50));
    }

    [Fact]
    public void RegisterPress_SecondPressWithinTimeAndDistance_IsDoubleClick()
    {
        var tracker = new PointerGestures.TapTracker();
        tracker.RegisterPress(1_000, 50, 50);

        Assert.Equal(2, tracker.RegisterPress(1_200, 55, 52));
    }

    [Fact]
    public void RegisterPress_ThirdPressRightAfterDouble_IsSingleClick()
    {
        var tracker = new PointerGestures.TapTracker();
        tracker.RegisterPress(1_000, 50, 50);
        tracker.RegisterPress(1_100, 50, 50);

        Assert.Equal(1, tracker.RegisterPress(1_200, 50, 50));
    }

    [Fact]
    public void RegisterPress_SecondPressAtTheTimeThreshold_IsSingleClick()
    {
        var tracker = new PointerGestures.TapTracker();
        tracker.RegisterPress(1_000, 50, 50);

        Assert.Equal(1, tracker.RegisterPress(1_000 + PointerGestures.DoubleTapThresholdMs, 50, 50));
    }

    [Fact]
    public void RegisterPress_SecondPressJustInsideTheTimeThreshold_IsDoubleClick()
    {
        var tracker = new PointerGestures.TapTracker();
        tracker.RegisterPress(1_000, 50, 50);

        Assert.Equal(2, tracker.RegisterPress(1_000 + PointerGestures.DoubleTapThresholdMs - 1, 50, 50));
    }

    [Fact]
    public void RegisterPress_SecondPressTooFarAway_IsSingleClick()
    {
        var tracker = new PointerGestures.TapTracker();
        tracker.RegisterPress(1_000, 50, 50);

        Assert.Equal(1, tracker.RegisterPress(1_100, 50 + PointerGestures.DoubleTapDistanceThreshold, 50));
    }

    [Fact]
    public void RegisterPress_SlowSecondPress_BecomesTheNewFirstTap()
    {
        var tracker = new PointerGestures.TapTracker();
        tracker.RegisterPress(1_000, 50, 50);
        tracker.RegisterPress(5_000, 50, 50); // too slow: starts a new sequence

        Assert.Equal(2, tracker.RegisterPress(5_100, 50, 50));
    }

    // ── ToKumikoColor ────────────────────────────────────────────

    [Fact]
    public void ToKumikoColor_Null_ReturnsNull()
    {
        Assert.Null(PointerGestures.ToKumikoColor(null));
    }

    [Fact]
    public void ToKumikoColor_CopiesEveryChannel()
    {
        var result = PointerGestures.ToKumikoColor(Color.FromArgb(10, 20, 30, 40));

        Assert.Equal(new KumikoColor(20, 30, 40, 10), result);
    }
}

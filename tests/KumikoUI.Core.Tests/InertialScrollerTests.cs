using KumikoUI.Core.Input;

namespace KumikoUI.Core.Tests;

public class InertialScrollerTests
{
    // ── ScrollSettings defaults ───────────────────────────────────

    [Fact]
    public void ScrollSettings_DefaultValues_AreReasonable()
    {
        var settings = new ScrollSettings();
        Assert.Equal(0.92f, settings.Friction);
        Assert.Equal(0.5f, settings.MinVelocity);
        Assert.Equal(8000f, settings.MaxVelocity);
        Assert.True(settings.EnableInertia);
        Assert.True(settings.WheelScrollMultiplier > 0);
    }

    // ── Initial state ─────────────────────────────────────────────

    [Fact]
    public void IsActive_Initially_IsFalse()
    {
        var scroller = new InertialScroller();
        Assert.False(scroller.IsActive);
    }

    // ── TrackVelocity + StartFling ─────────────────────────────────

    [Fact]
    public void StartFling_AfterSufficientVelocity_BecomesActive()
    {
        var scroller = new InertialScroller();
        // Simulate a fast flick: 200px over 16ms = 12500 px/s
        scroller.TrackVelocity(200f, 0f, 1000L);
        scroller.TrackVelocity(200f, 0f, 1016L);
        scroller.StartFling();
        Assert.True(scroller.IsActive);
    }

    [Fact]
    public void StartFling_SlowVelocity_DoesNotActivate()
    {
        var scroller = new InertialScroller();
        // Very slow movement
        scroller.TrackVelocity(0.01f, 0f, 1000L);
        scroller.TrackVelocity(0.01f, 0f, 1016L);
        scroller.StartFling();
        Assert.False(scroller.IsActive);
    }

    [Fact]
    public void StartFling_InertiaDisabled_DoesNotActivate()
    {
        var scroller = new InertialScroller();
        scroller.Settings.EnableInertia = false;
        scroller.TrackVelocity(200f, 0f, 1000L);
        scroller.TrackVelocity(200f, 0f, 1016L);
        scroller.StartFling();
        Assert.False(scroller.IsActive);
    }

    // ── Update ────────────────────────────────────────────────────

    [Fact]
    public void Update_WhileActive_ReturnsDelta()
    {
        var scroller = new InertialScroller();
        scroller.TrackVelocity(300f, 0f, 1000L);
        scroller.TrackVelocity(300f, 0f, 1016L);
        scroller.StartFling();
        var (dx, dy) = scroller.Update(16f);
        Assert.True(dx != 0 || dy != 0);
    }

    [Fact]
    public void Update_WhileInactive_ReturnsZero()
    {
        var scroller = new InertialScroller();
        var (dx, dy) = scroller.Update(16f);
        Assert.Equal(0f, dx);
        Assert.Equal(0f, dy);
    }

    [Fact]
    public void Update_EventuallyDeceleratesToStop()
    {
        var scroller = new InertialScroller();
        scroller.Settings.Friction = 0.5f; // Heavy friction to stop fast
        scroller.TrackVelocity(100f, 0f, 1000L);
        scroller.TrackVelocity(100f, 0f, 1016L);
        scroller.StartFling();

        // Run many frames until it stops
        for (int i = 0; i < 200 && scroller.IsActive; i++)
            scroller.Update(16f);

        Assert.False(scroller.IsActive);
    }

    // ── Reset ─────────────────────────────────────────────────────

    [Fact]
    public void Reset_CancelsActiveFling()
    {
        var scroller = new InertialScroller();
        scroller.TrackVelocity(300f, 0f, 1000L);
        scroller.TrackVelocity(300f, 0f, 1016L);
        scroller.StartFling();
        Assert.True(scroller.IsActive);
        scroller.Reset();
        Assert.False(scroller.IsActive);
    }

    [Fact]
    public void Reset_AfterReset_UpdateReturnsZero()
    {
        var scroller = new InertialScroller();
        scroller.TrackVelocity(300f, 0f, 1000L);
        scroller.TrackVelocity(300f, 0f, 1016L);
        scroller.StartFling();
        scroller.Reset();
        var (dx, dy) = scroller.Update(16f);
        Assert.Equal(0f, dx);
        Assert.Equal(0f, dy);
    }

    [Fact]
    public void Reset_AllowsNewFlingAfterReset()
    {
        var scroller = new InertialScroller();
        scroller.TrackVelocity(300f, 0f, 1000L);
        scroller.TrackVelocity(300f, 0f, 1016L);
        scroller.StartFling();
        scroller.Reset();

        // New fling
        scroller.TrackVelocity(400f, 0f, 2000L);
        scroller.TrackVelocity(400f, 0f, 2016L);
        scroller.StartFling();
        Assert.True(scroller.IsActive);
    }

    // ── Velocity velocity cap ─────────────────────────────────────

    [Fact]
    public void TrackVelocity_CapsAtMaxVelocity()
    {
        var scroller = new InertialScroller();
        scroller.Settings.MaxVelocity = 100f;
        // Ridiculous speed: 100000px in 1ms
        scroller.TrackVelocity(100_000f, 0f, 1000L);
        scroller.TrackVelocity(100_000f, 0f, 1001L);
        scroller.StartFling();
        if (scroller.IsActive)
        {
            var (dx, _) = scroller.Update(16f);
            // At 100 px/s for 16ms = 1.6px max
            Assert.True(Math.Abs(dx) <= 3f);
        }
    }
}


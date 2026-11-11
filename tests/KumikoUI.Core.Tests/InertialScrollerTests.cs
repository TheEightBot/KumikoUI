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

    // ── Velocity cap ──────────────────────────────────────────────

    [Fact]
    public void TrackVelocity_CapsAtMaxVelocity()
    {
        var scroller = new InertialScroller();
        scroller.Settings.MaxVelocity = 100f;
        scroller.Settings.VelocitySmoothing = 0f;
        scroller.Settings.MinVelocity = 0.01f;
        // Ridiculous speed: 100000px in 1ms
        scroller.TrackVelocity(100_000f, 0f, 1000L);
        scroller.TrackVelocity(100_000f, 0f, 1001L);
        scroller.StartFling(1001L);
        Assert.True(scroller.IsActive);
        var (dx, _) = scroller.Update(16f);
        // Capped at 100 px/s for 16 ms = 1.6 px.
        Assert.Equal(1.6f, dx, 3);
    }

    // ── Hold then release ─────────────────────────────────────────

    private static InertialScroller FastFlick(long lastSampleMs = 1032L)
    {
        var scroller = new InertialScroller();
        scroller.TrackVelocity(0f, 20f, lastSampleMs - 32);
        scroller.TrackVelocity(0f, 20f, lastSampleMs - 16);
        scroller.TrackVelocity(0f, 20f, lastSampleMs);
        return scroller;
    }

    [Fact]
    public void StartFling_ReleaseRightAfterLastMove_Flings()
    {
        var scroller = FastFlick(1032L);
        scroller.StartFling(1040L);
        Assert.True(scroller.IsActive);
    }

    [Fact]
    public void StartFling_HeldStillBeforeRelease_DoesNotFling()
    {
        var scroller = FastFlick(1032L);
        scroller.StartFling(1032L + 1000L);
        Assert.False(scroller.IsActive);
        Assert.Equal((0f, 0f), scroller.Update(16f));
    }

    [Fact]
    public void StartFling_HeldJustUnderCutoff_StillFlings()
    {
        var scroller = FastFlick(1032L);
        scroller.StartFling(1032L + 99L);
        Assert.True(scroller.IsActive);
    }

    [Fact]
    public void StartFling_CutoffIsConfigurable()
    {
        var scroller = FastFlick(1032L);
        scroller.Settings.FlingHoldCutoffMs = 30;
        scroller.StartFling(1032L + 50L);
        Assert.False(scroller.IsActive);
    }

    [Fact]
    public void StartFling_WithoutReleaseTimestamp_SkipsHoldCheck()
    {
        var scroller = FastFlick(1032L);
        scroller.StartFling();
        Assert.True(scroller.IsActive);
    }

    [Fact]
    public void TrackVelocity_PauseMidDrag_DropsEarlierVelocity()
    {
        var scroller = FastFlick(1032L);
        // Finger rests 500 ms, then creeps a hair over 16 ms and lifts.
        scroller.TrackVelocity(0f, 0.01f, 1532L);
        scroller.TrackVelocity(0f, 0.01f, 1548L);
        scroller.StartFling(1550L);
        Assert.False(scroller.IsActive);
    }

    // ── Timestamp guard ───────────────────────────────────────────

    [Fact]
    public void TrackVelocity_AllTimestampsZero_IgnoredAndCounted()
    {
        var scroller = new InertialScroller();
        for (int i = 0; i < 5; i++)
            scroller.TrackVelocity(0f, 20f, 0L);
        scroller.StartFling(0L);
        Assert.False(scroller.IsActive);
        Assert.Equal(5, scroller.IgnoredSampleCount);
    }

    [Fact]
    public void TrackVelocity_FirstSampleAtSmallTimestamp_StillTracks()
    {
        var scroller = new InertialScroller();
        scroller.TrackVelocity(0f, 20f, 1L);
        scroller.TrackVelocity(0f, 20f, 17L);
        scroller.StartFling(17L);
        Assert.True(scroller.IsActive);
    }

    [Fact]
    public void TrackVelocity_DuplicateTimestamp_ProducesNoInfiniteOrNaNVelocity()
    {
        var scroller = new InertialScroller();
        scroller.TrackVelocity(0f, 20f, 1000L);
        scroller.TrackVelocity(0f, 20f, 1016L);
        scroller.TrackVelocity(0f, 20f, 1016L);
        scroller.StartFling(1016L);
        Assert.True(scroller.IsActive);
        var (dx, dy) = scroller.Update(16f);
        Assert.True(float.IsFinite(dx));
        Assert.True(float.IsFinite(dy));
        Assert.InRange(dy, 0f, 8000f * 0.016f + 0.001f);
        Assert.Equal(1, scroller.IgnoredSampleCount);
    }

    [Fact]
    public void TrackVelocity_BackwardTimestamp_IsIgnoredAndLaterSamplesMeasureFromNewest()
    {
        var scroller = new InertialScroller();
        scroller.Settings.VelocitySmoothing = 0f;
        scroller.TrackVelocity(0f, 10f, 1000L);
        scroller.TrackVelocity(0f, 10f, 1010L);
        scroller.TrackVelocity(0f, 10f, 900L);  // goes backward
        scroller.TrackVelocity(0f, 10f, 1020L); // 10 ms after the newest
        scroller.StartFling(1020L);
        Assert.True(scroller.IsActive);
        var (_, dy) = scroller.Update(16f);
        Assert.Equal(1000f * 0.016f, dy, 2); // 10 px / 10 ms = 1000 px/s
        Assert.Equal(1, scroller.IgnoredSampleCount);
    }

    [Fact]
    public void TrackVelocity_NaNAndInfiniteDeltas_AreIgnored()
    {
        var scroller = new InertialScroller();
        scroller.TrackVelocity(0f, 20f, 1000L);
        scroller.TrackVelocity(float.NaN, float.PositiveInfinity, 1016L);
        scroller.StartFling(1016L);
        Assert.False(scroller.IsActive);
        Assert.Equal((0f, 0f), scroller.Update(16f));
        Assert.Equal(1, scroller.IgnoredSampleCount);
    }

    [Fact]
    public void Reset_ClearsIgnoredSampleCount()
    {
        var scroller = new InertialScroller();
        scroller.TrackVelocity(0f, 20f, 0L);
        scroller.Reset();
        Assert.Equal(0, scroller.IgnoredSampleCount);
    }

    // ── Frame-delta driven ────────────────────────────────────────

    private static InertialScroller Flinging()
    {
        var scroller = new InertialScroller();
        scroller.Settings.VelocitySmoothing = 0f;
        scroller.TrackVelocity(0f, 10f, 1000L);
        scroller.TrackVelocity(0f, 10f, 1010L); // 1000 px/s
        scroller.StartFling(1010L);
        return scroller;
    }

    [Fact]
    public void Update_UsesTheFrameIntervalForDistance()
    {
        var (_, d16) = Flinging().Update(16f);
        var (_, d32) = Flinging().Update(32f);
        Assert.Equal(16f, d16, 2);
        Assert.Equal(32f, d32, 2);
    }

    [Fact]
    public void Update_TwoShortFramesTravelAboutAsFarAsOneLongFrame()
    {
        var split = Flinging();
        float splitTotal = split.Update(8f).dy + split.Update(8f).dy;
        float wholeTotal = Flinging().Update(16f).dy;
        Assert.Equal(wholeTotal, splitTotal, 0);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-5f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void Update_BadFrameInterval_MovesNothingAndStaysActive(float interval)
    {
        var scroller = Flinging();
        Assert.Equal((0f, 0f), scroller.Update(interval));
        Assert.True(scroller.IsActive);
    }

    [Fact]
    public void Update_HugeFrameInterval_IsClamped()
    {
        var (_, dy) = Flinging().Update(60_000f);
        Assert.Equal(100f, dy, 2); // 1000 px/s for the 100 ms cap
    }

    // ── Boundary stop ─────────────────────────────────────────────

    [Fact]
    public void ReportApplied_BlockedAxis_EndsTheFling()
    {
        var scroller = Flinging();
        var (dx, dy) = scroller.Update(16f);
        scroller.ReportApplied(dx, dy, 0f, 0f);
        Assert.False(scroller.IsActive);
    }

    [Fact]
    public void ReportApplied_OneAxisBlocked_KeepsTheOtherFlinging()
    {
        var scroller = new InertialScroller();
        scroller.Settings.VelocitySmoothing = 0f;
        scroller.TrackVelocity(10f, 10f, 1000L);
        scroller.TrackVelocity(10f, 10f, 1010L);
        scroller.StartFling(1010L);
        var (dx, dy) = scroller.Update(16f);
        scroller.ReportApplied(dx, dy, 0f, dy);
        Assert.True(scroller.IsActive);
        var (nx, ny) = scroller.Update(16f);
        Assert.Equal(0f, nx);
        Assert.True(ny > 0f);
    }

    [Fact]
    public void ReportApplied_Moved_KeepsFlinging()
    {
        var scroller = Flinging();
        var (dx, dy) = scroller.Update(16f);
        scroller.ReportApplied(dx, dy, dx, dy);
        Assert.True(scroller.IsActive);
    }

    // ── Controller wiring ─────────────────────────────────────────

    private static void FlickUp(Input.InputGrid g, long releaseMs)
    {
        g.Pointer(InputAction.Pressed, 100, 220, timestampMs: 100);
        for (int i = 1; i <= 4; i++)
            g.Pointer(InputAction.Moved, 100, 220f - 40f * i, timestampMs: 100 + i * 16);
        g.Pointer(InputAction.Released, 100, 60, timestampMs: releaseMs);
    }

    [Fact]
    public void UpdateInertialScroll_FlingIntoTheEnd_StopsWithoutOvershootOrLoop()
    {
        var g = new Input.InputGrid(rows: 40);
        FlickUp(g, 180);
        Assert.True(g.Input.IsInertialScrolling);

        g.Scroll.OffsetY = g.Scroll.MaxOffsetY - 5f;
        int frames = 0;
        while (g.Input.UpdateInertialScroll(g.Scroll, 16f) && frames++ < 200) { }

        Assert.False(g.Input.IsInertialScrolling);
        Assert.Equal(g.Scroll.MaxOffsetY, g.Scroll.OffsetY, 3);
        Assert.True(frames < 5, $"Fling kept running {frames} frames after hitting the end.");
    }

    [Fact]
    public void UpdateInertialScroll_FlingAtTopEdgeScrollingUp_StopsImmediately()
    {
        var g = new Input.InputGrid(rows: 40);
        g.Pointer(InputAction.Pressed, 100, 60, timestampMs: 100);
        for (int i = 1; i <= 4; i++)
            g.Pointer(InputAction.Moved, 100, 60f + 40f * i, timestampMs: 100 + i * 16);
        g.Pointer(InputAction.Released, 100, 220, timestampMs: 180);
        Assert.True(g.Input.IsInertialScrolling);
        Assert.Equal(0f, g.Scroll.OffsetY);

        bool running = g.Input.UpdateInertialScroll(g.Scroll, 16f);

        Assert.False(running);
        Assert.Equal(0f, g.Scroll.OffsetY);
    }

    [Fact]
    public void Pointer_DragPauseRelease_DoesNotFling()
    {
        var g = new Input.InputGrid(rows: 40);
        FlickUp(g, 1200);
        Assert.False(g.Input.IsInertialScrolling);
    }

    [Fact]
    public void UpdateInertialScroll_ZeroFrameInterval_DoesNotStopTheFling()
    {
        var g = new Input.InputGrid(rows: 40);
        FlickUp(g, 180);

        Assert.True(g.Input.UpdateInertialScroll(g.Scroll, 0f));
        Assert.True(g.Input.IsInertialScrolling);
    }
}


using KumikoUI.Uno.Rendering;

namespace KumikoUI.Uno.Tests;

/// <summary>
/// Unit tests for <see cref="KumikoFrameScheduler"/>, the state machine behind the Uno host's
/// <c>CompositionTarget.Rendering</c> frame loop (#65): when the loop starts and stops, how redraw requests
/// coalesce, and how far an animation advances per frame.
/// </summary>
public class KumikoFrameSchedulerTests
{
    private static KumikoFrameScheduler Attached(bool deferInvalidation)
    {
        var sut = new KumikoFrameScheduler { DeferInvalidation = deferInvalidation };
        Assert.False(sut.Attach()); // nothing to do yet
        return sut;
    }

    private static TimeSpan Ms(double ms) => TimeSpan.FromMilliseconds(ms);

    // ── Redraw requests ───────────────────────────────────────────

    [Fact]
    public void RequestRedraw_SurfaceCoalescesItself_InvalidatesImmediatelyWithoutStartingTheLoop()
    {
        var sut = Attached(deferInvalidation: false);

        Assert.Equal(KumikoRedrawAction.InvalidateNow, sut.RequestRedraw());
        Assert.Equal(KumikoRedrawAction.InvalidateNow, sut.RequestRedraw());
        Assert.False(sut.IsRunning);
        Assert.False(sut.IsRedrawPending);
    }

    [Fact]
    public void RequestRedraw_DeferredSurface_FirstRequestStartsTheLoopAndLaterOnesCoalesce()
    {
        var sut = Attached(deferInvalidation: true);

        Assert.Equal(KumikoRedrawAction.StartFrameLoop, sut.RequestRedraw());
        Assert.Equal(KumikoRedrawAction.None, sut.RequestRedraw());
        Assert.Equal(KumikoRedrawAction.None, sut.RequestRedraw());
        Assert.True(sut.IsRunning);
        Assert.True(sut.IsRedrawPending);
    }

    [Fact]
    public void Frame_DeferredRedraws_PaintOnceThenStopTheLoop()
    {
        var sut = Attached(deferInvalidation: true);
        sut.RequestRedraw();
        sut.RequestRedraw();

        sut.BeginFrame(Ms(100));
        Assert.True(sut.TakePendingRedraw());
        Assert.False(sut.TakePendingRedraw());
        Assert.True(sut.EndFrame()); // idle: unsubscribe
        Assert.False(sut.IsRunning);
    }

    [Fact]
    public void Frame_RedrawRequestedDuringFrame_KeepsTheLoopForTheNextFrame()
    {
        var sut = Attached(deferInvalidation: true);
        sut.RequestRedraw();

        sut.BeginFrame(Ms(100));
        Assert.True(sut.TakePendingRedraw());
        Assert.Equal(KumikoRedrawAction.None, sut.RequestRedraw()); // loop already running
        Assert.False(sut.EndFrame());

        sut.BeginFrame(Ms(116));
        Assert.True(sut.TakePendingRedraw());
        Assert.True(sut.EndFrame());
    }

    [Fact]
    public void RequestRedraw_AfterTheLoopStopped_StartsItAgain()
    {
        var sut = Attached(deferInvalidation: true);
        sut.RequestRedraw();
        sut.BeginFrame(Ms(0));
        sut.TakePendingRedraw();
        Assert.True(sut.EndFrame());

        Assert.Equal(KumikoRedrawAction.StartFrameLoop, sut.RequestRedraw());
    }

    // ── Animation ─────────────────────────────────────────────────

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StartAnimation_StartsTheLoopOnceAndKeepsItRunningUntilStopped(bool deferInvalidation)
    {
        var sut = Attached(deferInvalidation);

        Assert.True(sut.StartAnimation());
        Assert.False(sut.StartAnimation()); // already running
        sut.BeginFrame(Ms(0));
        Assert.False(sut.EndFrame());
        sut.BeginFrame(Ms(16));
        Assert.False(sut.EndFrame());

        sut.StopAnimation();
        sut.BeginFrame(Ms(32));
        Assert.True(sut.EndFrame());
        Assert.False(sut.IsRunning);
    }

    [Fact]
    public void StartAnimation_WhileARedrawLoopRuns_DoesNotStartASecondSubscription()
    {
        var sut = Attached(deferInvalidation: true);
        Assert.Equal(KumikoRedrawAction.StartFrameLoop, sut.RequestRedraw());

        Assert.False(sut.StartAnimation());
        Assert.True(sut.IsAnimating);
    }

    [Fact]
    public void BeginFrame_FirstFrameOfAnimation_UsesTheNominalFrameTime()
    {
        var sut = Attached(deferInvalidation: false);
        sut.StartAnimation();

        Assert.Equal(KumikoFrameScheduler.NominalFrameMs, sut.BeginFrame(Ms(5000)));
    }

    [Theory]
    [InlineData(8.0, 8f)]       // 120 Hz
    [InlineData(16.7, 16.7f)]   // 60 Hz
    [InlineData(33.3, 33.3f)]   // 30 Hz (WASM under load)
    [InlineData(0.0, KumikoFrameScheduler.MinFrameMs)]   // same timestamp twice
    [InlineData(-5.0, KumikoFrameScheduler.MinFrameMs)]  // clock went backwards
    [InlineData(400.0, KumikoFrameScheduler.MaxFrameMs)] // a stall must not make the fling jump
    public void BeginFrame_LaterFrames_AdvanceByTheClampedTimeSinceThePreviousFrame(double gapMs, float expected)
    {
        var sut = Attached(deferInvalidation: false);
        sut.StartAnimation();
        sut.BeginFrame(Ms(1000));

        Assert.Equal(expected, sut.BeginFrame(Ms(1000 + gapMs)), precision: 3);
    }

    [Fact]
    public void BeginFrame_NewAnimationAfterTheLoopStopped_StartsFromTheNominalFrameTimeAgain()
    {
        var sut = Attached(deferInvalidation: false);
        sut.StartAnimation();
        sut.BeginFrame(Ms(0));
        sut.StopAnimation();
        Assert.True(sut.EndFrame());

        sut.StartAnimation();
        Assert.Equal(KumikoFrameScheduler.NominalFrameMs, sut.BeginFrame(Ms(10_000)));
    }

    // ── Visual tree ───────────────────────────────────────────────

    [Fact]
    public void RequestsWhileDetached_DoNotStartTheLoop_AndRunOnAttach()
    {
        var sut = new KumikoFrameScheduler { DeferInvalidation = true };

        Assert.Equal(KumikoRedrawAction.None, sut.RequestRedraw());
        Assert.False(sut.StartAnimation());
        Assert.False(sut.IsRunning);

        Assert.True(sut.Attach());
        Assert.True(sut.IsRunning);
    }

    [Fact]
    public void Detach_StopsTheLoopAndEndsAnimations_ButKeepsAPendingRedraw()
    {
        var sut = Attached(deferInvalidation: true);
        sut.StartAnimation();
        sut.RequestRedraw();

        Assert.True(sut.Detach()); // was running: unsubscribe
        Assert.False(sut.IsRunning);
        Assert.False(sut.IsAnimating);
        Assert.True(sut.IsRedrawPending);
        Assert.False(sut.Detach()); // idempotent

        Assert.True(sut.Attach()); // the pending redraw resumes the loop
    }

    [Fact]
    public void EndFrame_WhenNotRunning_ReportsStopped()
    {
        var sut = Attached(deferInvalidation: true);

        Assert.True(sut.EndFrame());
    }

    [Fact]
    public void SwitchingToASelfCoalescingSurface_StopsTheLoopOnTheNextFrame()
    {
        var sut = Attached(deferInvalidation: true);
        sut.RequestRedraw();

        sut.DeferInvalidation = false;
        sut.BeginFrame(Ms(0));
        Assert.True(sut.EndFrame());
    }
}

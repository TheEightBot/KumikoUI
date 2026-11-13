using KumikoUI.Maui.Rendering;

namespace KumikoUI.Maui.Tests;

/// <summary>
/// Unit tests for <see cref="KumikoCaretBlink"/>: the caret redraw lands just after each blink transition, so the host
/// redraws twice per blink cycle instead of every frame (#71, as on Uno, #65).
/// </summary>
public class KumikoCaretBlinkTests
{
    private const int Period = KumikoCaretBlink.PeriodMs;
    private const int Slack = KumikoCaretBlink.SlackMs;

    [Theory]
    [InlineData(0L, Period + Slack)]                  // blink just started: first transition at one period
    [InlineData(100L, Period - 100 + Slack)]
    [InlineData(Period - 1L, 1 + Slack)]              // just before a transition
    [InlineData(Period + Slack, Period)]              // a tick that landed on schedule: exactly one period later
    [InlineData(10L * Period + 3, Period - 3 + Slack)] // no drift after many cycles
    [InlineData(-50L, Period + Slack)]                // clock skew is treated as the start
    public void DelayToNextTransition_LandsJustAfterTheNextTransition(long elapsedMs, int expected)
    {
        Assert.Equal(expected, KumikoCaretBlink.DelayToNextTransition(elapsedMs));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(17L)]
    [InlineData(529L)]
    [InlineData(530L)]
    [InlineData(123_456L)]
    public void DelayToNextTransition_TickAlwaysFallsInTheNextPhase(long elapsedMs)
    {
        long tickAt = elapsedMs + KumikoCaretBlink.DelayToNextTransition(elapsedMs);

        Assert.Equal(elapsedMs / Period + 1, tickAt / Period);
        Assert.Equal(Slack, tickAt % Period);
    }
}

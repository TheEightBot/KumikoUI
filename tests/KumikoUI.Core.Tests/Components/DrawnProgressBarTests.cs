using KumikoUI.Core.Components;
using KumikoUI.Core.Input;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;
using static KumikoUI.Core.Tests.Components.ComponentInput;

namespace KumikoUI.Core.Tests.Components;

/// <summary>
/// The drawn progress bar in its two modes: a read-only indicator that ignores input, and an
/// interactive slider driven by press, drag, release and the keyboard; plus what it draws.
/// </summary>
public class DrawnProgressBarTests
{
    // Bounds x 100-300, so a pointer at x = 100 + 2 * v is at v percent of the 0-100 range.
    private static DrawnProgressBar Bar(bool interactive = true, Action<DrawnProgressBar>? configure = null)
    {
        var bar = new DrawnProgressBar { Bounds = new KumikoRect(100, 10, 200, 20), IsInteractive = interactive };
        configure?.Invoke(bar);
        return bar;
    }

    private static float X(double percent) => 100 + (float)percent * 2;

    // ── Slider input ──────────────────────────────────────────────

    [Fact]
    public void Press_JumpsTheValueToThePointer()
    {
        var bar = Bar();

        Assert.True(bar.Down(X(25), 20));

        Assert.Equal(25, bar.Value, 3);
    }

    [Fact]
    public void Drag_FollowsThePointer_AndRaisesValueChangedWithOldAndNew()
    {
        var bar = Bar();
        var values = new List<ComponentValueChangedEventArgs>();
        bar.ValueChanged += (_, e) => values.Add(e);

        bar.Down(X(10), 20);
        Assert.True(bar.Move(X(40), 20));
        Assert.True(bar.Move(X(75), 20));
        bar.Up(X(75), 20);

        Assert.Equal(75, bar.Value, 3);
        Assert.Equal(3, values.Count);
        Assert.Equal(0d, values[0].OldValue);
        Assert.Equal(10, (double)values[0].NewValue!, 3);
        Assert.Equal(10, (double)values[1].OldValue!, 3);
        Assert.Equal(40, (double)values[1].NewValue!, 3);
    }

    [Fact]
    public void Drag_BeyondEitherEnd_ClampsToTheRange()
    {
        var bar = Bar();

        bar.Down(X(50), 20);
        bar.Move(900, 20);
        Assert.Equal(100, bar.Value);
        bar.Move(-900, 20);
        Assert.Equal(0, bar.Value);
    }

    [Fact]
    public void MoveWithoutAPress_AndAfterTheRelease_DoesNothing()
    {
        var bar = Bar();
        Assert.False(bar.Move(X(30), 20));
        Assert.Equal(0, bar.Value);

        bar.Down(X(10), 20);
        bar.Up(X(10), 20);
        Assert.False(bar.Move(X(80), 20));
        Assert.Equal(10, bar.Value, 3);
    }

    [Fact]
    public void Slider_UsesTheConfiguredRange()
    {
        var bar = Bar(configure: b => { b.Minimum = 50; b.Maximum = 250; });

        bar.Down(X(50), 20); // halfway

        Assert.Equal(150, bar.Value, 3);
    }

    [Fact]
    public void ReadOnlyBar_IgnoresThePointerAndTheKeyboard()
    {
        var bar = Bar(interactive: false);
        bar.Value = 40;

        Assert.False(bar.Down(X(90), 20));
        Assert.False(bar.Move(X(90), 20));
        Assert.False(bar.Key(KumikoKey.Right));

        Assert.Equal(40, bar.Value);
    }

    [Fact]
    public void DisabledSlider_IgnoresThePress()
    {
        var bar = Bar();
        bar.IsEnabled = false;

        Assert.False(bar.Down(X(90), 20));
        Assert.Equal(0, bar.Value);
    }

    [Fact]
    public void Release_IsAlwaysConsumed()
    {
        Assert.True(Bar(interactive: false).Up(X(5), 20));
    }

    // ── Keyboard ──────────────────────────────────────────────────

    [Fact]
    public void ArrowKeys_StepByATwentiethOfTheRange_AndClamp()
    {
        var bar = Bar();

        Assert.True(bar.Key(KumikoKey.Right));
        Assert.Equal(5, bar.Value, 6);
        Assert.True(bar.Key(KumikoKey.Up));
        Assert.Equal(10, bar.Value, 6);
        Assert.True(bar.Key(KumikoKey.Left));
        Assert.True(bar.Key(KumikoKey.Down));
        Assert.Equal(0, bar.Value, 6);

        bar.Key(KumikoKey.Left);
        Assert.Equal(0, bar.Value);

        bar.Value = 98;
        bar.Key(KumikoKey.Right);
        Assert.Equal(100, bar.Value);
    }

    [Fact]
    public void OtherKeys_AreNotHandled()
    {
        Assert.False(Bar().Key(KumikoKey.Enter));
        Assert.False(Bar().Key(KumikoKey.Home));
    }

    [Fact]
    public void Value_IsClamped_AndTheSameValueRaisesNothing()
    {
        var bar = Bar();
        int raised = 0;
        bar.ValueChanged += (_, _) => raised++;

        bar.Value = 250;
        bar.Value = 100;
        bar.Value = -5;

        Assert.Equal(0, bar.Value);
        Assert.Equal(2, raised);
    }

    // ── Drawing ───────────────────────────────────────────────────

    [Fact]
    public void Draw_ShowsACenteredTrackAndAFillInProportionToTheValue()
    {
        var bar = Bar(interactive: false);
        bar.Value = 25;

        var fills = Draw(bar).Fills.ToList();

        Assert.Equal(2, fills.Count);
        Assert.Equal(new KumikoRect(100, 17, 200, 6), fills[0].Rect);
        Assert.Equal(bar.TrackColor, fills[0].Color);
        Assert.Equal(new KumikoRect(100, 17, 50, 6), fills[1].Rect);
        Assert.Equal(bar.FillColor, fills[1].Color);
    }

    [Fact]
    public void Draw_AtZero_ShowsOnlyTheTrack()
    {
        var bar = Bar(interactive: false);

        Assert.Single(Draw(bar).Fills);
    }

    [Fact]
    public void Draw_WithAnEmptyRange_ShowsNoFill()
    {
        var bar = Bar(interactive: false, b => { b.Minimum = 10; b.Maximum = 10; });

        Assert.Single(Draw(bar).Fills);
    }

    [Fact]
    public void Draw_Thumb_AppearsForSlidersAndWhenRequested_AtTheEndOfTheFill()
    {
        var plain = Bar(interactive: false);
        plain.Value = 50;
        Assert.Equal(2, Draw(plain).Fills.Count());

        plain.ShowThumb = true;
        var withThumb = Draw(plain).Fills.ToList();
        Assert.Equal(3, withThumb.Count);
        var thumb = withThumb[2];
        Assert.Equal(plain.ThumbColor, thumb.Color);
        Assert.Equal(16, thumb.Rect.Width);
        Assert.Equal(200, thumb.Rect.X + thumb.Rect.Width / 2, 3);
        Assert.Equal(20, thumb.Rect.Y + thumb.Rect.Height / 2, 3);

        var slider = Bar();
        slider.Value = 50;
        Assert.Equal(3, Draw(slider).Fills.Count());
    }

    [Fact]
    public void Draw_Thumb_FollowsADrag_AndGetsAFocusRingWhenFocused()
    {
        var bar = Bar();
        bar.Down(X(80), 20);
        Assert.Equal(260, Draw(bar).Fills.Last().Rect.X + 8, 3);
        Assert.DoesNotContain(Draw(bar).Ops, o => o.Kind == DrawOpKind.DrawRoundRect);

        Focus(bar);
        var ring = Assert.Single(Draw(bar).Ops, o => o.Kind == DrawOpKind.DrawRoundRect);
        Assert.Equal(2, ring.StrokeWidth);
    }
}

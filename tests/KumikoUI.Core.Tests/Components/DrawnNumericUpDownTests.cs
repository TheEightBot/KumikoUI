using KumikoUI.Core.Components;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;
using static KumikoUI.Core.Tests.Components.ComponentInput;

namespace KumikoUI.Core.Tests.Components;

/// <summary>
/// The drawn numeric up/down: [-] and [+] button presses, clamping, keyboard stepping, hold-to-repeat,
/// formatting and what each button draws while pressed.
/// </summary>
public class DrawnNumericUpDownTests
{
    // Bounds (0,0)-(200,40): the buttons are square (40 wide), so [-] is x 0-40 and [+] is x 160-200.
    private const float MinusX = 20, PlusX = 180, ValueX = 100, Y = 20;

    private static DrawnNumericUpDown Spin(double value = 5, Action<DrawnNumericUpDown>? configure = null)
    {
        var n = new DrawnNumericUpDown { Bounds = new KumikoRect(0, 0, 200, 40) };
        configure?.Invoke(n);
        n.Value = value;
        return n;
    }

    // ── Buttons ───────────────────────────────────────────────────

    [Fact]
    public void PressingPlusAndMinus_StepsTheValue_AndReportsIt()
    {
        var n = Spin(5, x => x.Step = 2);
        var values = new List<ComponentValueChangedEventArgs>();
        n.ValueChanged += (_, e) => values.Add(e);

        Assert.True(n.Down(PlusX, Y));
        n.Up(PlusX, Y);
        Assert.True(n.Down(MinusX, Y));
        n.Up(MinusX, Y);
        n.Down(MinusX, Y);
        n.Up(MinusX, Y);

        Assert.Equal(3, n.Value);
        Assert.Equal(new object[] { 7d, 5d, 3d }, values.Select(v => v.NewValue!).ToArray());
        Assert.Equal(5d, values[0].OldValue);
    }

    [Fact]
    public void PressingTheValueArea_ClaimsThePressButChangesNothing()
    {
        var n = Spin();
        int changes = 0;
        n.ValueChanged += (_, _) => changes++;

        Assert.True(n.Down(ValueX, Y));
        Assert.True(n.Up(ValueX, Y));

        Assert.Equal(5, n.Value);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void ReadOnlyAndDisabled_IgnoreThePointer()
    {
        var ro = Spin(configure: x => x.IsReadOnly = true);
        Assert.False(ro.Down(PlusX, Y));
        Assert.Equal(5, ro.Value);

        var off = Spin();
        off.IsEnabled = false;
        Assert.False(off.Down(PlusX, Y));
        Assert.Equal(5, off.Value);
    }

    [Fact]
    public void Value_IsClampedToMinimumAndMaximum()
    {
        var n = Spin(8, x => { x.Minimum = 0; x.Maximum = 10; });
        var raised = new List<double>();
        n.ValueChanged += (_, e) => raised.Add((double)e.NewValue!);

        n.Tap(PlusX, Y);
        n.Tap(PlusX, Y);
        n.Tap(PlusX, Y); // already at 10: no change, no event
        Assert.Equal(10, n.Value);
        Assert.Equal(new[] { 9d, 10d }, raised);

        n.Value = -50;
        Assert.Equal(0, n.Value);
        n.Tap(MinusX, Y);
        Assert.Equal(0, n.Value);
    }

    // ── Keyboard ──────────────────────────────────────────────────

    [Fact]
    public void ArrowKeys_StepByOne_PageKeysByTenSteps()
    {
        var n = Spin(50, x => x.Step = 2);

        Assert.True(n.Key(KumikoKey.Up));
        Assert.Equal(52, n.Value);
        Assert.True(n.Key(KumikoKey.Down));
        Assert.Equal(50, n.Value);
        Assert.True(n.Key(KumikoKey.PageUp));
        Assert.Equal(70, n.Value);
        Assert.True(n.Key(KumikoKey.PageDown));
        Assert.Equal(50, n.Value);
    }

    [Fact]
    public void HomeAndEnd_JumpToTheBounds_OnlyWhenTheyAreSet()
    {
        var unbounded = Spin(5);
        Assert.True(unbounded.Key(KumikoKey.Home));
        Assert.True(unbounded.Key(KumikoKey.End));
        Assert.Equal(5, unbounded.Value);

        var bounded = Spin(5, x => { x.Minimum = 0; x.Maximum = 10; });
        bounded.Key(KumikoKey.Home);
        var afterHome = bounded.Value;
        bounded.Key(KumikoKey.End);
        var afterEnd = bounded.Value;
        Assert.Equal(new[] { 0d, 10d }, new[] { afterHome, afterEnd }.OrderBy(v => v));
    }

    [Fact]
    public void ReadOnly_IgnoresKeys_AndUnrelatedKeysAreNotHandled()
    {
        var ro = Spin(configure: x => x.IsReadOnly = true);
        Assert.False(ro.Key(KumikoKey.Up));
        Assert.Equal(5, ro.Value);

        var n = Spin();
        Assert.False(n.Key(KumikoKey.Enter));
        Assert.False(n.Key(KumikoKey.Character, '7'));
    }

    // ── Hold to repeat ────────────────────────────────────────────

    [Fact]
    public void UpdateAutoRepeat_IsIdleWhenNoButtonIsHeld()
    {
        var n = Spin();

        Assert.False(n.UpdateAutoRepeat());
    }

    [Fact]
    public void UpdateAutoRepeat_WaitsOutTheInitialDelay()
    {
        var n = Spin(5, x => x.RepeatDelayMs = 60_000);
        n.Down(PlusX, Y);

        Assert.True(n.UpdateAutoRepeat());

        Assert.Equal(6, n.Value); // only the press itself stepped
    }

    [Fact]
    public void UpdateAutoRepeat_KeepsSteppingWhileHeld_AndStopsAfterTheRelease()
    {
        var n = Spin(0, x => { x.RepeatDelayMs = 0; x.RepeatIntervalMs = 1; });
        n.Down(PlusX, Y);
        Assert.Equal(1, n.Value);

        Thread.Sleep(40);
        Assert.True(n.UpdateAutoRepeat());
        Assert.True(n.Value > 1);

        n.Up(PlusX, Y);
        var held = n.Value;
        Thread.Sleep(10);
        Assert.False(n.UpdateAutoRepeat());
        Assert.Equal(held, n.Value);
    }

    [Fact]
    public void UpdateAutoRepeat_RepeatsTheDecrementButtonToo()
    {
        var n = Spin(100, x => { x.RepeatDelayMs = 0; x.RepeatIntervalMs = 1; });
        n.Down(MinusX, Y);

        Thread.Sleep(40);
        n.UpdateAutoRepeat();

        Assert.True(n.Value < 99);
    }

    // ── Drawing ───────────────────────────────────────────────────

    [Theory]
    [InlineData(-1, "G", "3.5")]
    [InlineData(2, "G", "3.50")]
    [InlineData(0, "G", "4")]
    [InlineData(-1, "N1", "3.5")]
    public void Draw_ShowsTheFormattedValueBetweenTheButtons(int decimals, string format, string expected)
    {
        var n = Spin(3.5, x => { x.DecimalPlaces = decimals; x.Format = format; });

        var ctx = Draw(n);

        var text = ctx.SingleText(expected);
        Assert.Equal(new KumikoRect(40, 0, 120, 40), text.Rect);
        Assert.Equal(KumikoTextAlignment.Center, text.HAlign);
    }

    [Fact]
    public void Draw_ShowsAMinusAndAPlusSymbol()
    {
        var n = Spin();

        var lines = Draw(n).Lines.ToList();

        Assert.Equal(3, lines.Count);
        Assert.All(lines, l => Assert.Equal(n.ButtonSymbolColor, l.Color));
        Assert.Equal(20, lines[0].Rect.X + lines[0].Rect.Width / 2, 2); // minus centered in the left button
        Assert.Equal(180, lines[1].Rect.X + lines[1].Rect.Width / 2, 2); // plus centered in the right button
        Assert.Equal(0, lines[0].Rect.Height);
        Assert.Equal(0, lines[2].Rect.Width); // the plus's vertical stroke
    }

    [Fact]
    public void Draw_ColorsTheHeldButtonWithThePressedColor()
    {
        var n = Spin();
        var idle = Draw(n).Fills.Where(o => o.Kind == DrawOpKind.FillRect).Select(o => o.Color).ToList();
        Assert.All(idle, c => Assert.Equal(n.ButtonBackgroundColor, c));

        n.Down(MinusX, Y);
        var held = Draw(n).Fills.Where(o => o.Kind == DrawOpKind.FillRect).ToList();
        Assert.Equal(n.ButtonPressedColor, held.Single(o => Math.Abs(o.Rect.X) < 0.01f).Color);
        Assert.Equal(n.ButtonBackgroundColor, held.Single(o => Math.Abs(o.Rect.X - 160) < 0.01f).Color);

        n.Up(MinusX, Y);
        var released = Draw(n).Fills.Where(o => o.Kind == DrawOpKind.FillRect).Select(o => o.Color);
        Assert.All(released, c => Assert.Equal(n.ButtonBackgroundColor, c));
    }

    [Fact]
    public void Draw_UsesAnExplicitButtonWidth()
    {
        var n = Spin(configure: x => x.ButtonWidth = 50);

        var ctx = Draw(n);

        Assert.Equal(new KumikoRect(50, 0, 100, 40), ctx.Texts.Single().Rect);
        n.Down(100, Y); // the value area
        Assert.Equal(5, n.Value);
        n.Down(45, Y);  // inside the wider [-] button, though not the default 40 px one
        Assert.Equal(4, n.Value);
        n.Down(170, Y);
        Assert.Equal(5, n.Value);
    }

    [Fact]
    public void ApplyTheme_TakesTheColorsFromTheGridStyle()
    {
        var style = new KumikoStyle();
        var n = new DrawnNumericUpDown();

        n.ApplyTheme(style);

        Assert.Equal(style.BackgroundColor, n.BackgroundColor);
        Assert.Equal(style.CellTextColor, n.TextColor);
        Assert.Equal(style.CellTextColor, n.ButtonSymbolColor);
        Assert.Equal(style.AccentColor, n.FocusedBorderColor);
    }
}

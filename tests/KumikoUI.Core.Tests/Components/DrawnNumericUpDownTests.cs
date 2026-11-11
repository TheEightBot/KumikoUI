using System.Globalization;
using KumikoUI.Core.Components;
using KumikoUI.Core.Editing;
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
        var n = new DrawnNumericUpDown { Bounds = new KumikoRect(0, 0, 200, 40), Culture = CultureInfo.InvariantCulture };
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
        Assert.Equal(0, bounded.Value); // Home is the minimum
        bounded.Key(KumikoKey.End);
        Assert.Equal(10, bounded.Value); // End is the maximum
    }

    [Fact]
    public void ReadOnly_IgnoresKeys_AndUnrelatedKeysAreNotHandled()
    {
        var ro = Spin(configure: x => x.IsReadOnly = true);
        Assert.False(ro.Key(KumikoKey.Up));
        Assert.Equal(5, ro.Value);

        var n = Spin();
        Assert.False(n.Key(KumikoKey.Enter));
        Assert.False(n.Key(KumikoKey.Tab));
        Assert.False(ro.Key(KumikoKey.Character, '7')); // read-only: no typed entry
    }

    // ── Hold to repeat (driven by simulated frames) ───────────────

    private const float Frame = 16f;

    /// <summary>Runs <paramref name="milliseconds"/> of frames, as a host's frame timer would while the editor animates.</summary>
    private static void Run(DrawnComponent c, float milliseconds)
    {
        for (float t = 0; t < milliseconds; t += Frame)
            c.UpdateAnimation(Frame);
    }

    [Fact]
    public void Holding_Plus_StepsRepeatedlyFromTheFrameLoop()
    {
        var n = Spin(0);
        n.Down(PlusX, Y);
        Assert.Equal(1, n.Value); // the press itself
        Assert.True(n.IsAnimating);

        Run(n, 1000);

        Assert.True(n.Value >= 6, $"expected repeated steps after a second, got {n.Value}");
        Assert.True(n.IsAnimating);
    }

    [Fact]
    public void Holding_WaitsOutTheInitialDelay_BeforeRepeating()
    {
        var n = Spin(0);
        n.Down(PlusX, Y);

        Run(n, 300);

        Assert.Equal(1, n.Value);
    }

    [Fact]
    public void Holding_Minus_RepeatsTheDecrement()
    {
        var n = Spin(100);
        n.Down(MinusX, Y);

        Run(n, 1000);

        Assert.True(n.Value <= 94);
    }

    [Fact]
    public void Releasing_StopsTheRepeat_AndTheAnimation()
    {
        var n = Spin(0);
        n.Down(PlusX, Y);
        Run(n, 800);
        n.Up(PlusX, Y);
        var held = n.Value;

        Assert.False(n.IsAnimating);
        Assert.False(n.UpdateAnimation(Frame));
        Run(n, 500);

        Assert.Equal(held, n.Value);
    }

    [Fact]
    public void ACanceledPress_StopsTheRepeat()
    {
        var n = Spin(0);
        n.Down(PlusX, Y);

        n.OnPointerCancel(Ptr(InputAction.Cancelled, PlusX, Y));

        Assert.False(n.IsAnimating);
    }

    [Fact]
    public void TheRepeatAccelerates_ShorterIntervalsThenBiggerSteps()
    {
        var n = Spin(0, x => x.Maximum = 1_000_000);
        n.Down(PlusX, Y);

        Run(n, 1000);
        double afterFirstSecond = n.Value;
        Run(n, 1000);
        double secondSecond = n.Value - afterFirstSecond;
        Run(n, 3000);

        Assert.True(secondSecond > afterFirstSecond, $"{secondSecond} steps in the second second vs {afterFirstSecond} in the first");
        Assert.True(n.Value > 200, $"a long hold takes bigger steps, got {n.Value}");
    }

    [Fact]
    public void AStalledFrame_DoesNotQueueABurstOfSteps()
    {
        var n = Spin(0);
        n.Down(PlusX, Y);

        n.UpdateAnimation(10_000); // the app froze for ten seconds

        Assert.True(n.Value <= 1 + 16, $"got {n.Value}");
    }

    [Fact]
    public void RepeatDelayAndInterval_AreConfigurable()
    {
        var n = Spin(0, x => { x.RepeatDelayMs = 0; x.RepeatIntervalMs = 50; x.MinRepeatIntervalMs = 50; });
        n.Down(PlusX, Y);

        Run(n, 512);

        Assert.InRange(n.Value, 8, 13);
    }

    [Fact]
    public void HoldingAnArrowKey_RepeatsOnceTheOperatingSystemRepeatsTheKey()
    {
        var n = Spin(0);

        n.Key(KumikoKey.Up);
        Assert.Equal(1, n.Value);
        Run(n, 600);
        Assert.Equal(1, n.Value); // no key repeat seen yet: nothing to repeat

        n.Key(KumikoKey.Up); // the first repeat from the operating system
        for (int i = 0; i < 60; i++)
        {
            n.UpdateAnimation(Frame);
            if (i % 3 == 0) Assert.True(n.Key(KumikoKey.Up)); // the system keeps repeating at ~50 ms
        }

        Assert.True(n.Value >= 8, $"got {n.Value}");
    }

    [Fact]
    public void ReleasingAnArrowKey_StopsTheRepeat()
    {
        var n = Spin(0);
        n.Key(KumikoKey.Up);
        n.Key(KumikoKey.Up);
        Run(n, 400);
        var held = n.Value;
        Assert.True(held > 1);

        n.OnKeyUp(new KumikoKeyEventArgs { Key = KumikoKey.Up, IsKeyDown = false });
        Run(n, 400);

        Assert.False(n.IsAnimating);
        Assert.Equal(held, n.Value);
    }

    [Fact]
    public void AnArrowKeyWhoseRepeatStops_EndsItsHold_WithoutAKeyUpEvent()
    {
        var n = Spin(0);
        n.Key(KumikoKey.Up);
        n.Key(KumikoKey.Up);
        Run(n, 200);

        Run(n, 600); // no more repeats from the system: the key was released
        var settled = n.Value;
        Run(n, 400);

        Assert.False(n.IsAnimating);
        Assert.Equal(settled, n.Value);
    }

    [Fact]
    public void AnArrowKeyThatNeverRepeats_ReleasesItself()
    {
        var n = Spin(0);
        n.Key(KumikoKey.Up);

        Run(n, 3000);

        Assert.False(n.IsAnimating);
        Assert.Equal(1, n.Value);
    }

    [Fact]
    public void LosingFocus_StopsTheRepeat()
    {
        var n = Spin(0);
        var focus = Focus(n);
        n.Down(PlusX, Y);

        focus.SetFocus(null);

        Assert.False(n.IsAnimating);
    }

    // ── Sane limits ───────────────────────────────────────────────

    [Fact]
    public void Minimum_NeverExceedsMaximum_TheLastLimitSetWins()
    {
        var n = Spin(5);
        n.Maximum = 20;

        n.Minimum = 30;
        Assert.Equal(30, n.Maximum);
        Assert.Equal(30, n.Value);

        n.Maximum = 3;
        Assert.Equal(3, n.Minimum);
        Assert.Equal(3, n.Value);

        n.Maximum = 8;
        n.Minimum = 1;
        Assert.True(n.Minimum <= n.Maximum);
    }

    [Fact]
    public void NewLimits_ClampTheCurrentValue_AndReportIt()
    {
        var n = Spin(50);
        var raised = new List<double>();
        n.ValueChanged += (_, e) => raised.Add((double)e.NewValue!);

        n.Maximum = 20;

        Assert.Equal(20, n.Value);
        Assert.Equal(new[] { 20d }, raised);
    }

    [Fact]
    public void Limits_Value_Step_AndDecimalPlaces_IgnoreNonsense()
    {
        var n = Spin(5, x => { x.Minimum = 0; x.Maximum = 10; });

        n.Value = double.NaN;
        n.Minimum = double.NaN;
        n.Maximum = double.NaN;
        n.Step = 0;
        n.Step = double.PositiveInfinity;
        n.DecimalPlaces = 99;

        Assert.Equal(5, n.Value);
        Assert.Equal(0, n.Minimum);
        Assert.Equal(10, n.Maximum);
        Assert.Equal(1, n.Step);
        Assert.Equal(15, n.DecimalPlaces);

        n.Step = -2;
        Assert.Equal(2, n.Step);
        n.DecimalPlaces = -7;
        Assert.Equal(-1, n.DecimalPlaces);
    }

    [Fact]
    public void SteppingNearTheEndsOfTheDoubleRange_CannotOverflow()
    {
        var n = Spin(0);
        n.Step = double.MaxValue;
        n.Value = double.MaxValue;

        n.Increment();
        Assert.Equal(double.MaxValue, n.Value);
        n.Key(KumikoKey.PageUp);
        Assert.Equal(double.MaxValue, n.Value);

        n.Value = double.MinValue;
        n.Decrement();
        n.Key(KumikoKey.PageDown);
        Assert.Equal(double.MinValue, n.Value);
        Assert.True(double.IsFinite(n.Value));
    }

    [Fact]
    public void AnAcceleratedHold_StopsAtTheMaximum()
    {
        var n = Spin(0, x => x.Maximum = 500);
        n.Down(PlusX, Y);

        Run(n, 10_000);

        Assert.Equal(500, n.Value);
    }

    // ── Rounding ──────────────────────────────────────────────────

    [Fact]
    public void SteppingByAFraction_NeverShowsFloatingPointNoise()
    {
        var n = Spin(0, x => x.Step = 0.1);

        n.Tap(PlusX, Y);
        n.Tap(PlusX, Y);
        n.Tap(PlusX, Y);

        Assert.Equal(0.3, n.Value); // not 0.30000000000000004
        Assert.Single(Draw(n).TextsEqual("0.3"));
    }

    [Fact]
    public void HoldingPlus_ByTenths_StaysOnCleanValues()
    {
        var n = Spin(0, x => { x.Step = 0.1; x.Maximum = 1000; });
        n.Down(PlusX, Y);
        for (int i = 0; i < 60; i++)
        {
            n.UpdateAnimation(Frame);
            string shown = n.Value.ToString("R", CultureInfo.InvariantCulture);
            Assert.True(shown.Length <= 8, $"noisy value {shown}");
        }
    }

    [Theory]
    [InlineData(2, 1.23456, 1.23)]
    [InlineData(2, 1.235, 1.24)]
    [InlineData(0, 2.5, 3)]
    [InlineData(1, -0.05, -0.1)]
    public void DecimalPlaces_RoundTheValue(int places, double set, double expected)
    {
        var n = Spin(0, x => x.DecimalPlaces = places);

        n.Value = set;

        Assert.Equal(expected, n.Value);
    }

    [Fact]
    public void StepsOfAFraction_AreRoundedToTheDecimalPlaces()
    {
        var n = Spin(0, x => { x.DecimalPlaces = 1; x.Step = 0.25; });

        n.Increment();

        Assert.Equal(0.3, n.Value);
        Assert.Single(Draw(n).TextsEqual("0.3"));
    }

    // ── Culture ───────────────────────────────────────────────────

    [Theory]
    [InlineData("de-DE", "1234,5")]
    [InlineData("ja-JP", "1234.5")]
    [InlineData("fr-FR", "1234,5")]
    [InlineData("en-US", "1234.5")]
    public void Draw_ShowsTheNumberInTheCulture(string culture, string expected)
    {
        var n = Spin(1234.5, x => x.Culture = CultureInfo.GetCultureInfo(culture));

        Assert.Single(Draw(n).TextsEqual(expected));
    }

    [Fact]
    public void DecimalPlaces_AreShownWithTheCulturesSeparator()
    {
        var n = Spin(3.5, x => { x.Culture = CultureInfo.GetCultureInfo("de-DE"); x.DecimalPlaces = 2; });

        Assert.Single(Draw(n).TextsEqual("3,50"));
    }

    // ── Typed entry ───────────────────────────────────────────────

    private static void Type(DrawnNumericUpDown n, string text)
    {
        foreach (char c in text)
            n.Key(KumikoKey.Character, c);
    }

    private static object? EditorValue(DrawnNumericUpDown n) => ((IValueEditor)n).Value;

    [Theory]
    [InlineData("en-US", "1,234.5", 1234.5)]
    [InlineData("en-US", "1234.5", 1234.5)]
    [InlineData("en-US", "-7.25", -7.25)]
    [InlineData("de-DE", "1.234,5", 1234.5)]
    [InlineData("de-DE", "1234,5", 1234.5)]
    [InlineData("de-DE", "-7,25", -7.25)]
    [InlineData("fr-FR", "12,5", 12.5)]
    [InlineData("ja-JP", "1,234.5", 1234.5)]
    [InlineData("ja-JP", "99", 99)]
    public void TypedText_IsParsedInTheCulture_OnEnter(string culture, string typed, double expected)
    {
        var n = Spin(1, x => x.Culture = CultureInfo.GetCultureInfo(culture));

        Type(n, typed);
        Assert.Equal(expected, EditorValue(n)); // what a commit would write
        Assert.False(n.Key(KumikoKey.Enter));   // left to the host, which commits

        Assert.Equal(expected, n.Value);
    }

    [Fact]
    public void ATapOnTheNumber_StartsTypedEntry_WithTheValueAsText()
    {
        var n = Spin(12.5);

        n.Tap(ValueX, Y);
        var ctx = Draw(n);

        Assert.Single(ctx.TextsEqual("12.5"));
        Assert.True(((IValueEditor)n).HasValue);
        Assert.False(((IValueEditor)n).IsDirty); // nothing typed yet
    }

    [Fact]
    public void TypingReplacesTheNumber_AndMakesTheEditorDirty()
    {
        var n = Spin(12.5);
        n.StartTrackingChanges();

        Type(n, "7");

        Assert.True(((IValueEditor)n).IsDirty);
        Assert.Equal(12.5, n.Value); // not applied until Enter, focus loss or a step
        Assert.Equal(7d, EditorValue(n));
    }

    [Fact]
    public void TypedText_IsClampedToTheLimits_AndRounded()
    {
        var n = Spin(5, x => { x.Minimum = 0; x.Maximum = 10; x.DecimalPlaces = 1; });

        Type(n, "99");
        n.Key(KumikoKey.Enter);
        Assert.Equal(10, n.Value);

        Type(n, "-5");
        n.Key(KumikoKey.Enter);
        Assert.Equal(0, n.Value);

        Type(n, "3.456");
        n.Key(KumikoKey.Enter);
        Assert.Equal(3.5, n.Value);
    }

    [Theory]
    [InlineData("en-US", "-")]
    [InlineData("en-US", "1.2.3")]
    [InlineData("en-US", "1,5")]     // not a thousands group
    [InlineData("de-DE", "1.5")]     // would be 15 as a lenient parse
    [InlineData("de-DE", "1,2,3")]
    public void TypedText_ThatIsNotANumber_IsNotApplied_AndIsDrawnAsAnError(string culture, string typed)
    {
        var n = Spin(5, x => x.Culture = CultureInfo.GetCultureInfo(culture));
        n.StartTrackingChanges();

        Type(n, typed);

        var pending = ((IValueEditor)n).Value; // the edit session refuses it: nothing converts from it
        Assert.IsNotType<double>(pending);
        Assert.IsNotType<string>(pending);
        Assert.Equal(typed, pending!.ToString());
        Assert.True(((IValueEditor)n).IsDirty);
        n.Key(KumikoKey.Enter);
        Assert.Equal(5, n.Value);
        var ctx = Draw(n);
        Assert.Contains(ctx.Ops, o => o.Kind == DrawOpKind.DrawRoundRect && o.Color.Equals(n.ErrorBorderColor));
    }

    [Fact]
    public void Escape_DropsTypedText_Then_LeavesToTheHost()
    {
        var n = Spin(5);
        Type(n, "42");

        Assert.True(n.Key(KumikoKey.Escape));
        Assert.Equal(5, n.Value);
        Assert.Equal(5d, EditorValue(n));
        Assert.False(n.Key(KumikoKey.Escape)); // nothing pending: the host cancels the edit
    }

    [Fact]
    public void LosingFocus_AppliesTypedNumbers_AndDropsTheRest()
    {
        var n = Spin(5);
        var focus = Focus(n);
        Type(n, "42");
        focus.SetFocus(null);
        Assert.Equal(42, n.Value);

        focus.SetFocus(n);
        Type(n, "4-2");
        focus.SetFocus(null);
        Assert.Equal(42, n.Value);
    }

    [Fact]
    public void SteppingWhileTyped_AppliesTheTextFirst()
    {
        var n = Spin(5);
        Type(n, "20");

        n.Key(KumikoKey.Up);
        Assert.Equal(21, n.Value);

        Type(n, "30");
        n.Tap(MinusX, Y);
        Assert.Equal(29, n.Value);
    }

    [Fact]
    public void TypedEntry_EditsTheTextWithTheCaretKeys()
    {
        var n = Spin(0);
        Type(n, "125");

        n.Key(KumikoKey.Home);   // while typing, Home moves the caret
        Type(n, "9");
        n.Key(KumikoKey.End);
        n.Key(KumikoKey.Backspace);

        Assert.Equal(912d, EditorValue(n));
    }

    [Fact]
    public void LettersAndOtherCharacters_AreRejectedByTheTextBox()
    {
        var n = Spin(0);

        Type(n, "1a2b3");

        Assert.Equal(123d, EditorValue(n));
    }

    [Fact]
    public void HomeAndEnd_WhenNotTyping_AreTheMinimumAndTheMaximum()
    {
        var n = Spin(5, x => { x.Minimum = -3; x.Maximum = 9; });

        n.Key(KumikoKey.End);
        Assert.Equal(9, n.Value);
        n.Key(KumikoKey.Home);
        Assert.Equal(-3, n.Value);
    }

    // ── Narrow cells ──────────────────────────────────────────────

    [Fact]
    public void ANarrowCell_KeepsRoomForTheNumber()
    {
        var n = new DrawnNumericUpDown { Bounds = new KumikoRect(0, 0, 80, 28), Culture = CultureInfo.InvariantCulture, Value = 12 };

        var ctx = Draw(n);

        var text = ctx.SingleText("12");
        Assert.True(text.Rect.Width >= 32, $"the number has {text.Rect.Width} px");
        // The buttons still take presses at their (narrower) edges
        n.Tap(70, 14);
        Assert.Equal(13, n.Value);
        n.Tap(10, 14);
        Assert.Equal(12, n.Value);
    }

    [Fact]
    public void AnExplicitButtonWidth_IsLimitedToAThirdOfTheControl()
    {
        var n = new DrawnNumericUpDown { Bounds = new KumikoRect(0, 0, 60, 28), ButtonWidth = 50, Culture = CultureInfo.InvariantCulture };

        var ctx = Draw(n);

        Assert.Equal(20, ctx.Texts.Single().Rect.Width, 2);
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

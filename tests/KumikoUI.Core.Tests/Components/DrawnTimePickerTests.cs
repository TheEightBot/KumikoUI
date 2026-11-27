using System.Globalization;
using KumikoUI.Core.Components;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using static KumikoUI.Core.Tests.Components.ComponentInput;

namespace KumikoUI.Core.Tests.Components;

/// <summary>
/// The drawn time picker: keyboard and pointer stepping, typed entry, 12-hour and 24-hour time, the minute
/// step, the seconds a value keeps, and the value it commits.
/// </summary>
public class DrawnTimePickerTests
{
    // Field (0,0)-(200,30): the up and down buttons take the last 28 px, so the segments share 172 px.
    // 24-hour: hour 0-86, minute 86-172. 12-hour: hour 0-62, minute 62-117, AM/PM 117-172.
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    private static DrawnTimePicker Picker(TimeSpan? time = null, bool use24 = false, Action<DrawnTimePicker>? configure = null)
    {
        var p = new DrawnTimePicker { Bounds = new KumikoRect(0, 0, 200, 30), Culture = En, Use24HourFormat = use24 };
        configure?.Invoke(p);
        if (time is { } t) p.TimeOfDay = t;
        return p;
    }

    private static void Type(DrawnTimePicker p, string text)
    {
        foreach (char c in text) p.Key(KumikoKey.Character, c);
    }

    private static object? Value(DrawnTimePicker p) => ((IValueEditor)p).Value;

    private static List<string> Texts(DrawnTimePicker p) => Draw(p).Texts.Select(o => o.Text!).ToList();

    // ── Format ────────────────────────────────────────────────────

    [Fact]
    public void FollowsTheCulture_UntilUse24HourFormatIsSet()
    {
        var us = new DrawnTimePicker { Culture = En };
        var de = new DrawnTimePicker { Culture = De };

        Assert.False(us.Use24HourFormat);
        Assert.True(de.Use24HourFormat);

        us.Use24HourFormat = true;
        Assert.True(us.Use24HourFormat);
    }

    [Fact]
    public void Draws12HourTime_WithTheCulturesDesignator()
    {
        var p = Picker(new TimeSpan(14, 5, 0));

        Assert.Equal(new[] { "02", ":", "05", "PM" }.OrderBy(s => s), Texts(p).OrderBy(s => s));
    }

    [Fact]
    public void Draws24HourTime_WithoutADesignator()
    {
        var p = Picker(new TimeSpan(14, 5, 0), use24: true);

        Assert.Equal(new[] { "14", ":", "05" }.OrderBy(s => s), Texts(p).OrderBy(s => s));
    }

    [Theory]
    [InlineData(0, "12", "AM")]
    [InlineData(12, "12", "PM")]
    [InlineData(23, "11", "PM")]
    [InlineData(1, "01", "AM")]
    public void TwelveHourDisplay_MapsMidnightAndNoon(int hour, string text, string period)
    {
        var p = Picker(new TimeSpan(hour, 0, 0));

        var texts = Texts(p);
        Assert.Contains(text, texts);
        Assert.Contains(period, texts);
    }

    [Fact]
    public void AnEmptyPicker_ShowsDashes_AndHoldsNoValue()
    {
        var p = Picker();

        Assert.False(p.HasTime);
        Assert.Null(Value(p));
        Assert.False(((IValueEditor)p).HasValue);
        Assert.Contains("--", Texts(p));
    }

    // ── Keyboard stepping ─────────────────────────────────────────

    [Fact]
    public void UpAndDown_StepTheHour_AndWrap()
    {
        var p = Picker(new TimeSpan(23, 30, 0), use24: true);

        Assert.True(p.Key(KumikoKey.Up));
        Assert.Equal(new TimeSpan(0, 30, 0), p.TimeOfDay);

        p.Key(KumikoKey.Down);
        p.Key(KumikoKey.Down);
        Assert.Equal(new TimeSpan(22, 30, 0), p.TimeOfDay);
    }

    [Fact]
    public void RightMovesToTheMinutes_UpAndDownStepThem_AndWrapWithoutCarrying()
    {
        var p = Picker(new TimeSpan(9, 59, 0), use24: true);

        p.Key(KumikoKey.Right);
        p.Key(KumikoKey.Up);
        Assert.Equal(new TimeSpan(9, 0, 0), p.TimeOfDay);

        p.Key(KumikoKey.Down);
        Assert.Equal(new TimeSpan(9, 59, 0), p.TimeOfDay);

        p.Key(KumikoKey.Left);
        p.Key(KumikoKey.Up);
        Assert.Equal(new TimeSpan(10, 59, 0), p.TimeOfDay);
    }

    [Fact]
    public void In12HourTime_RightReachesAmPm_WhichUpAndDownFlip()
    {
        var p = Picker(new TimeSpan(9, 30, 0));

        p.Key(KumikoKey.Right);
        p.Key(KumikoKey.Right);
        p.Key(KumikoKey.Right); // already on the last segment
        p.Key(KumikoKey.Up);
        Assert.Equal(new TimeSpan(21, 30, 0), p.TimeOfDay);

        p.Key(KumikoKey.Down);
        Assert.Equal(new TimeSpan(9, 30, 0), p.TimeOfDay);
    }

    [Fact]
    public void In24HourTime_RightStopsAtTheMinutes()
    {
        var p = Picker(new TimeSpan(9, 30, 0), use24: true);

        p.Key(KumikoKey.Right);
        p.Key(KumikoKey.Right);
        p.Key(KumikoKey.Up);

        Assert.Equal(new TimeSpan(9, 31, 0), p.TimeOfDay);
    }

    [Theory]
    [InlineData(15, 7, 1, 15)]
    [InlineData(15, 15, 1, 30)]
    [InlineData(15, 52, 1, 0)]
    [InlineData(15, 7, -1, 0)]
    [InlineData(15, 30, -1, 15)]
    [InlineData(15, 0, -1, 45)]
    [InlineData(5, 58, 1, 0)]
    public void MinuteStep_StepsToTheNextMultiple(int step, int minute, int direction, int expected)
    {
        var p = Picker(new TimeSpan(9, minute, 0), use24: true, p => p.MinuteStep = step);
        p.Key(KumikoKey.Right);

        p.Key(direction > 0 ? KumikoKey.Up : KumikoKey.Down);

        Assert.Equal(expected, p.Minute);
    }

    [Fact]
    public void MinuteStep_IsLimitedToOneToThirty()
    {
        var p = new DrawnTimePicker { MinuteStep = 0 };
        Assert.Equal(1, p.MinuteStep);
        p.MinuteStep = 99;
        Assert.Equal(30, p.MinuteStep);
    }

    [Fact]
    public void SteppingAnEmptyPicker_GivesItATime()
    {
        var p = Picker(use24: true);

        p.Key(KumikoKey.Up);

        Assert.True(p.HasTime);
        Assert.Equal(new TimeSpan(1, 0, 0), p.TimeOfDay);
    }

    [Fact]
    public void KeysItDoesNotUse_AreLeftToTheGrid()
    {
        var p = Picker(new TimeSpan(9, 0, 0));

        Assert.False(p.Key(KumikoKey.Enter));
        Assert.False(p.Key(KumikoKey.Escape));
        Assert.False(p.Key(KumikoKey.Tab));
    }

    // ── Typed entry ───────────────────────────────────────────────

    [Fact]
    public void TypingFourDigits_SetsHourAndMinute_In24HourTime()
    {
        var p = Picker(use24: true);

        Type(p, "1405");

        Assert.Equal(new TimeSpan(14, 5, 0), p.TimeOfDay);
    }

    [Fact]
    public void ADigitNoSecondDigitCanFollow_CompletesTheSegment()
    {
        var p = Picker(use24: true);

        Type(p, "930");

        Assert.Equal(new TimeSpan(9, 30, 0), p.TimeOfDay);
    }

    [Fact]
    public void ColonMovesToTheMinutes()
    {
        var p = Picker(use24: true);

        Type(p, "1:7");

        Assert.Equal(new TimeSpan(1, 7, 0), p.TimeOfDay);
    }

    [Fact]
    public void In12HourTime_TypedHoursKeepAmPm_AndLettersSetIt()
    {
        var p = Picker(new TimeSpan(15, 0, 0)); // 3 PM

        Type(p, "0840");
        Assert.Equal(new TimeSpan(20, 40, 0), p.TimeOfDay); // 8:40 PM

        Type(p, "a");
        Assert.Equal(new TimeSpan(8, 40, 0), p.TimeOfDay);

        Type(p, "P");
        Assert.Equal(new TimeSpan(20, 40, 0), p.TimeOfDay);
    }

    [Fact]
    public void In12HourTime_TwelveIsNoonOrMidnight()
    {
        var am = Picker(new TimeSpan(1, 0, 0));
        Type(am, "12");
        Assert.Equal(new TimeSpan(0, 0, 0), am.TimeOfDay);

        var pm = Picker(new TimeSpan(13, 0, 0));
        Type(pm, "12");
        Assert.Equal(new TimeSpan(12, 0, 0), pm.TimeOfDay);
    }

    [Fact]
    public void ATypedHourOutOfRange_StartsOver()
    {
        var p = Picker(use24: true);

        Type(p, "27"); // 2 then 7: 27 is not an hour, so the 7 is a new hour

        Assert.Equal(7, p.Hour);
    }

    [Fact]
    public void TypingAMinuteDigitAbove5_CompletesIt()
    {
        var p = Picker(new TimeSpan(9, 0, 0), use24: true);
        p.Key(KumikoKey.Right);

        Type(p, "7");
        Type(p, "3"); // a new minute: 7 was already complete

        Assert.Equal(3, p.Minute);
    }

    [Fact]
    public void TypedCharacters_ArriveAsTextToo()
    {
        var p = Picker(use24: true);

        p.OnKeyDown(new KumikoKeyEventArgs { Key = KumikoKey.Character, Text = "1830" });

        Assert.Equal(new TimeSpan(18, 30, 0), p.TimeOfDay);
    }

    [Fact]
    public void AnotherCulturesTimeSeparator_MovesToTheMinutes()
    {
        var p = new DrawnTimePicker { Bounds = new KumikoRect(0, 0, 200, 30), Culture = CultureInfo.GetCultureInfo("fi-FI") };

        Type(p, "9.45");

        Assert.Equal(new TimeSpan(9, 45, 0), p.TimeOfDay);
    }

    // ── Pointer ───────────────────────────────────────────────────

    [Fact]
    public void TheUpAndDownButtons_StepTheActiveSegment()
    {
        var p = Picker(new TimeSpan(9, 30, 0), use24: true);

        p.Tap(190, 7);
        Assert.Equal(new TimeSpan(10, 30, 0), p.TimeOfDay);

        p.Tap(190, 23);
        p.Tap(190, 23);
        Assert.Equal(new TimeSpan(8, 30, 0), p.TimeOfDay);
    }

    [Fact]
    public void TappingASegment_ActivatesIt()
    {
        var p = Picker(new TimeSpan(9, 30, 0), use24: true);

        p.Tap(130, 15); // the minutes
        p.Tap(190, 7);

        Assert.Equal(new TimeSpan(9, 31, 0), p.TimeOfDay);

        p.Tap(30, 15); // back to the hours
        p.Tap(190, 7);
        Assert.Equal(new TimeSpan(10, 31, 0), p.TimeOfDay);
    }

    [Fact]
    public void TappingAmPm_FlipsIt()
    {
        var p = Picker(new TimeSpan(9, 30, 0));

        p.Tap(150, 15);
        Assert.Equal(new TimeSpan(21, 30, 0), p.TimeOfDay);

        p.Tap(150, 15);
        Assert.Equal(new TimeSpan(9, 30, 0), p.TimeOfDay);
    }

    [Fact]
    public void AmPmIsNotAHitTarget_In24HourTime()
    {
        var p = Picker(new TimeSpan(9, 30, 0), use24: true);

        p.Tap(150, 15); // the minutes zone in 24-hour time

        Assert.Equal(new TimeSpan(9, 30, 0), p.TimeOfDay);
    }

    // ── Seconds and the value ─────────────────────────────────────

    [Fact]
    public void SecondsAndFractions_SurviveUntilTheTimeIsChanged()
    {
        var p = Picker(new TimeSpan(0, 9, 30, 45, 250), use24: true);

        Assert.Equal(new TimeSpan(0, 9, 30, 45, 250), (TimeSpan)Value(p)!);

        p.Key(KumikoKey.Up);

        Assert.Equal(new TimeSpan(10, 30, 0), (TimeSpan)Value(p)!);
    }

    [Fact]
    public void ChangingTheTime_RaisesTimeChangedAndValueChanged_AndMakesItDirty()
    {
        var p = Picker(new TimeSpan(9, 30, 0), use24: true);
        p.StartTrackingChanges();
        var times = new List<KumikoTimeChangedEventArgs>();
        var values = new List<ComponentValueChangedEventArgs>();
        p.TimeChanged += (_, e) => times.Add(e);
        p.ValueChanged += (_, e) => values.Add(e);

        Assert.False(((IValueEditor)p).IsDirty);
        p.Key(KumikoKey.Up);

        Assert.True(((IValueEditor)p).IsDirty);
        var time = Assert.Single(times);
        Assert.Equal((9, 30, 10, 30), (time.OldHour, time.OldMinute, time.NewHour, time.NewMinute));
        Assert.Single(values);
    }

    [Fact]
    public void WithABaseDate_TheValueIsThatDateAtTheTime()
    {
        var p = Picker(new TimeSpan(9, 30, 15), use24: true);
        p.BaseDate = new DateTime(2024, 3, 5, 18, 0, 0);

        Assert.Equal(new DateTime(2024, 3, 5, 9, 30, 15), Value(p));
    }

    [Fact]
    public void Clear_RemovesTheTime()
    {
        var p = Picker(new TimeSpan(9, 30, 0));

        p.Clear();

        Assert.False(p.HasTime);
        Assert.Null(Value(p));
    }

    [Fact]
    public void HourAndMinuteSetters_AreLimitedToTheirRange()
    {
        var p = new DrawnTimePicker { Hour = 40, Minute = -3 };

        Assert.Equal((23, 0), (p.Hour, p.Minute));
        Assert.True(p.HasTime);
    }

    [Fact]
    public void TimeOfDay_IsLimitedToADay()
    {
        var p = new DrawnTimePicker { TimeOfDay = TimeSpan.FromDays(3) };

        Assert.Equal(new TimeSpan(23, 59, 59) + TimeSpan.FromTicks(TimeSpan.TicksPerSecond - 1), p.TimeOfDay);
    }

    [Fact]
    public void ApplyTheme_TakesTheStylesColorsAndFont()
    {
        var style = new KumikoStyle { CellTextColor = new KumikoColor(1, 2, 3), BackgroundColor = new KumikoColor(250, 251, 252) };
        var p = new DrawnTimePicker();

        p.ApplyTheme(style);

        Assert.Equal(style.CellTextColor, p.TextColor);
        Assert.Equal(style.BackgroundColor, p.BackgroundColor);
        Assert.Equal(style.CellFont, p.Font);
    }

    [Fact]
    public void Focus_DrawsWithoutError_AndAnInvisibleEditorDrawsNothing()
    {
        var p = Picker(new TimeSpan(9, 30, 0));
        Focus(p);

        Assert.NotEmpty(Draw(p).Texts);

        p.IsVisible = false;
        Assert.Empty(Draw(p).Texts);
    }
}

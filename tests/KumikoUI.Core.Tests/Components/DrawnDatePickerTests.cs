using KumikoUI.Core.Components;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;
using static KumikoUI.Core.Tests.Components.ComponentInput;

namespace KumikoUI.Core.Tests.Components;

/// <summary>
/// The drawn date picker: opening the calendar, picking a day on release, month navigation and the
/// month and year dropdowns, range mode, min and max dates, the time row, keyboard stepping and what
/// the calendar draws (<see cref="DateSelectedEventArgs"/>, <see cref="DateRangeSelectedEventArgs"/>,
/// <see cref="KumikoTimeChangedEventArgs"/>).
/// </summary>
public class DrawnDatePickerTests
{
    // Field (0,0)-(200,30). The calendar is 232 wide, starts at y = 32, and its day grid at y = 104
    // with 32 px cells starting at x = 4. March 2024 starts on a Friday (column 5).
    private static readonly DateTime Mar15 = new(2024, 3, 15);

    private static DrawnDatePicker Picker(DateTime? date = null, bool open = false, Action<DrawnDatePicker>? configure = null)
    {
        // Invariant names ("Su", "January", "AM"), whatever culture the machine running the tests uses
        var p = new DrawnDatePicker { Bounds = new KumikoRect(0, 0, 200, 30), Culture = System.Globalization.CultureInfo.InvariantCulture };
        configure?.Invoke(p);
        p.SelectedDate = date ?? Mar15;
        if (open) p.OpenCalendar();
        return p;
    }

    /// <summary>Center of a day's cell, in the month the calendar currently shows.</summary>
    private static (float X, float Y) Cell(DateTime date)
    {
        int index = (int)new DateTime(date.Year, date.Month, 1).DayOfWeek + date.Day - 1;
        int column = index % 7;
        int week = index / 7; // whole weeks: the integer division is intended
        return (4 + column * 32f + 16, 104 + week * 32f + 16);
    }

    private static void TapDay(DrawnDatePicker p, DateTime date)
    {
        var (x, y) = Cell(date);
        p.Tap(x, y);
    }

    private const float NavY = 50;

    // ── Opening and picking ───────────────────────────────────────

    [Fact]
    public void PressOnTheField_OpensTheCalendar()
    {
        var p = Picker();

        Assert.True(p.Down(100, 15));

        Assert.True(p.IsCalendarOpen);
    }

    [Fact]
    public void PressAndReleaseOnADay_SelectsItClosesAndCompletesTheEdit()
    {
        var p = Picker(open: true);
        var dates = new List<DateSelectedEventArgs>();
        var values = new List<ComponentValueChangedEventArgs>();
        int completed = 0;
        p.DateSelected += (_, e) => dates.Add(e);
        p.ValueChanged += (_, e) => values.Add(e);
        p.EditCompleted += () => completed++;

        TapDay(p, new DateTime(2024, 3, 20));

        Assert.Equal(new DateTime(2024, 3, 20), p.SelectedDate);
        Assert.False(p.IsCalendarOpen);
        var d = Assert.Single(dates);
        Assert.Equal(Mar15, d.OldDate);
        Assert.Equal(new DateTime(2024, 3, 20), d.NewDate);
        Assert.Equal(Mar15, values[0].OldValue);
        Assert.Equal(new DateTime(2024, 3, 20), values[0].NewValue);
        Assert.Equal(1, completed);
    }

    [Fact]
    public void Press_DoesNotSelect_UntilTheRelease()
    {
        var p = Picker(open: true);
        var (x, y) = Cell(new DateTime(2024, 3, 20));

        p.Down(x, y);

        Assert.Equal(Mar15, p.SelectedDate);
        Assert.True(p.IsCalendarOpen);
    }

    [Fact]
    public void ReleasingOnAnotherDay_SelectsNothing()
    {
        var p = Picker(open: true);
        var (px, py) = Cell(new DateTime(2024, 3, 20));
        var (rx, ry) = Cell(new DateTime(2024, 3, 21));

        p.Down(px, py);
        Assert.True(p.Up(rx, ry));

        Assert.Equal(Mar15, p.SelectedDate);
        Assert.True(p.IsCalendarOpen);
    }

    [Fact]
    public void DaysOfTheNeighboringMonth_AreSelectable()
    {
        var p = Picker(open: true);

        // March 2024's grid starts on Sunday Feb 25
        TapDay(p, new DateTime(2024, 3, 1)); // sanity: a day inside the month
        Assert.Equal(new DateTime(2024, 3, 1), p.SelectedDate);

        p.OpenCalendar();
        p.Tap(4 + 16, 104 + 16); // first cell of the grid
        Assert.Equal(new DateTime(2024, 2, 25), p.SelectedDate);
    }

    [Fact]
    public void PressOutsideTheCalendar_ClosesIt()
    {
        var p = Picker(open: true);

        Assert.True(p.Down(500, 500));

        Assert.False(p.IsCalendarOpen);
        Assert.Equal(Mar15, p.SelectedDate);
    }

    [Fact]
    public void MinAndMaxDates_BlockPickingOutsideTheRange()
    {
        var p = Picker(open: true, configure: x =>
        {
            x.MinDate = new DateTime(2024, 3, 10);
            x.MaxDate = new DateTime(2024, 3, 20);
        });

        TapDay(p, new DateTime(2024, 3, 5));
        TapDay(p, new DateTime(2024, 3, 25));
        Assert.Equal(Mar15, p.SelectedDate);
        Assert.True(p.IsCalendarOpen);

        TapDay(p, new DateTime(2024, 3, 12));
        Assert.Equal(new DateTime(2024, 3, 12), p.SelectedDate);
    }

    [Fact]
    public void SettingTheSameDate_RaisesNothing_AndKeepsTheTimeOfDayOut()
    {
        var p = Picker();
        int raised = 0;
        p.DateSelected += (_, _) => raised++;
        p.ValueChanged += (_, _) => raised++;

        p.SelectedDate = Mar15.AddHours(9);

        Assert.Equal(0, raised);
        Assert.Equal(Mar15, p.SelectedDate);
    }

    [Fact]
    public void Disabled_IgnoresThePress()
    {
        var p = Picker();
        p.IsEnabled = false;

        Assert.False(p.Down(100, 15));
        Assert.False(p.IsCalendarOpen);
    }

    [Fact]
    public void HitTest_CoversTheCalendarOnlyWhileItIsOpen()
    {
        var p = Picker();
        Assert.False(p.HitTest(100, 200));

        p.OpenCalendar();
        Assert.True(p.HitTest(100, 200));
        Assert.True(p.GetCalendarBounds().Contains(100, 200));
        Assert.Equal(new KumikoRect(0, 32, 232, 268), p.GetCalendarBounds());
    }

    [Fact]
    public void LosingFocus_ClosesTheCalendar()
    {
        var p = Picker(open: true);
        var focus = Focus(p);

        focus.SetFocus(null);

        Assert.False(p.IsCalendarOpen);
    }

    // ── Navigation ────────────────────────────────────────────────

    private static string MonthShown(DrawnDatePicker p)
    {
        var ctx = Draw(p);
        return string.Join(" ", ctx.Texts.Select(t => t.Text!).Where(t =>
            t.Length > 3 && DateTime.TryParseExact(t, "MMMM", null, System.Globalization.DateTimeStyles.None, out _)));
    }

    [Fact]
    public void Arrows_StepTheDisplayedMonth_WithoutChangingTheSelection()
    {
        var p = Picker(open: true);

        p.Down(10, NavY);   // left arrow
        Assert.Equal("February", MonthShown(p));
        p.Down(10, NavY);
        Assert.Equal("January", MonthShown(p));

        p.Down(220, NavY);  // right arrow
        p.Down(220, NavY);
        p.Down(220, NavY);
        Assert.Equal("April", MonthShown(p));

        Assert.Equal(Mar15, p.SelectedDate);
    }

    [Fact]
    public void PreviousAndNextMonth_RollOverTheYear()
    {
        var p = Picker(new DateTime(2024, 1, 10), open: true);

        p.PreviousMonth();
        var ctx = Draw(p);
        Assert.Single(ctx.TextsEqual("December"));
        Assert.Single(ctx.TextsEqual("2023"));

        p.NextMonth();
        p.NextMonth();
        ctx = Draw(p);
        Assert.Single(ctx.TextsEqual("February"));
        Assert.Single(ctx.TextsEqual("2024"));
    }

    [Fact]
    public void MonthDropdown_PicksAMonth()
    {
        var p = Picker(open: true);

        p.Down(80, NavY); // month label
        var open = Draw(p);
        foreach (var name in new[] { "January", "June", "December" })
            Assert.Contains(open.Texts, t => t.Text == name);

        // The dropdown is 110 wide at x = 41, y = 68, with 26 px items; June is the sixth
        Assert.True(p.Down(60, 68 + 5 * 26 + 13));

        var ctx = Draw(p);
        Assert.Single(ctx.TextsEqual("June"));
        Assert.Single(ctx.TextsEqual("2024"));
        Assert.Empty(ctx.TextsEqual("January")); // dropdown closed again
    }

    [Fact]
    public void MonthDropdown_PressOutsideItOnlyClosesTheDropdown()
    {
        var p = Picker(open: true);
        p.Down(80, NavY);

        Assert.True(p.Down(10, 200));

        Assert.True(p.IsCalendarOpen);
        Assert.Empty(Draw(p).TextsEqual("January"));
    }

    [Fact]
    public void YearDropdown_PicksAYear()
    {
        var p = Picker(open: true);
        p.Down(150, NavY); // year label

        // The list is centered on the displayed year (2024): its row sits 100 px below the dropdown top.
        // 2022 is two rows (52 px) higher.
        Assert.True(p.Down(120, 68 + 100 - 52 + 13));

        var ctx = Draw(p);
        Assert.Single(ctx.TextsEqual("2022"));
        Assert.Single(ctx.TextsEqual("March"));
    }

    [Fact]
    public void MonthAndYearDropdowns_AreMutuallyExclusive()
    {
        var p = Picker(open: true);
        p.Down(80, NavY);
        Assert.Contains(Draw(p).Texts, t => t.Text == "January");

        p.Down(150, 0 + NavY); // swallowed by the month dropdown: closes it
        Assert.DoesNotContain(Draw(p).Texts, t => t.Text == "January");

        p.Down(150, NavY);
        var years = Draw(p);
        Assert.DoesNotContain(years.Texts, t => t.Text == "January");
        Assert.Contains(years.Texts, t => t.Text == "2024" && t.Rect.X > 100);
    }

    // ── Keyboard ──────────────────────────────────────────────────

    [Theory]
    [InlineData(KumikoKey.Space)]
    [InlineData(KumikoKey.Enter)]
    [InlineData(KumikoKey.Down)]
    public void Keys_OpenAClosedCalendar(KumikoKey key)
    {
        var p = Picker();

        Assert.True(p.Key(key));

        Assert.True(p.IsCalendarOpen);
    }

    [Fact]
    public void Keys_OnAClosedCalendar_IgnoreEverythingElse()
    {
        var p = Picker();

        Assert.False(p.Key(KumikoKey.Left));
        Assert.False(p.Key(KumikoKey.Escape));
        Assert.Equal(Mar15, p.SelectedDate);
    }

    [Fact]
    public void Arrows_InTheOpenCalendar_MoveTheSelectionByDayAndWeek()
    {
        var p = Picker(open: true);
        var moves = new List<(DateTime, DateTime)>();
        p.DateSelected += (_, e) => moves.Add((e.OldDate, e.NewDate));

        p.Key(KumikoKey.Right);
        p.Key(KumikoKey.Down);
        p.Key(KumikoKey.Left);
        p.Key(KumikoKey.Up);

        Assert.Equal(Mar15, p.SelectedDate);
        Assert.Equal(new[]
        {
            (Mar15, Mar15.AddDays(1)),
            (Mar15.AddDays(1), Mar15.AddDays(8)),
            (Mar15.AddDays(8), Mar15.AddDays(7)),
            (Mar15.AddDays(7), Mar15),
        }, moves);
    }

    [Fact]
    public void Arrows_ThatCrossAMonth_FollowTheSelectionInTheCalendar()
    {
        var p = Picker(new DateTime(2024, 3, 31), open: true);

        p.Key(KumikoKey.Right);

        Assert.Equal(new DateTime(2024, 4, 1), p.SelectedDate);
        Assert.Equal("April", MonthShown(p));
    }

    [Theory]
    [InlineData(KumikoKey.Escape)]
    [InlineData(KumikoKey.Enter)]
    public void EscapeAndEnter_CloseTheOpenCalendar(KumikoKey key)
    {
        var p = Picker(open: true);

        Assert.True(p.Key(key));

        Assert.False(p.IsCalendarOpen);
        Assert.Equal(Mar15, p.SelectedDate);
    }

    [Fact]
    public void OtherKeys_InTheOpenCalendar_AreNotHandled()
    {
        var p = Picker(open: true);

        Assert.False(p.Key(KumikoKey.Tab));
        Assert.False(p.Key(KumikoKey.Character, 'x'));
    }

    // ── Range mode ────────────────────────────────────────────────

    [Fact]
    public void RangeMode_FirstDayStartsTheRange_SecondEndsIt_ThirdStartsANewOne()
    {
        var p = Picker(open: true, configure: x => x.IsRangeMode = true);
        var ranges = new List<DateRangeSelectedEventArgs>();
        var dates = new List<DateSelectedEventArgs>();
        int completed = 0;
        p.RangeSelected += (_, e) => ranges.Add(e);
        p.DateSelected += (_, e) => dates.Add(e);
        p.EditCompleted += () => completed++;

        TapDay(p, new DateTime(2024, 3, 20));
        var r = Assert.Single(ranges);
        Assert.Equal(Mar15, r.StartDate);
        Assert.Equal(new DateTime(2024, 3, 20), r.EndDate);
        Assert.Equal(new DateTime(2024, 3, 20), p.RangeEndDate);
        Assert.True(p.IsCalendarOpen);

        TapDay(p, new DateTime(2024, 3, 25));
        Assert.Equal(new DateTime(2024, 3, 25), p.SelectedDate);
        Assert.Null(p.RangeEndDate);
        Assert.Equal((Mar15, new DateTime(2024, 3, 25)), (dates.Single().OldDate, dates.Single().NewDate));

        Assert.True(p.IsCalendarOpen);
        Assert.Equal(0, completed);
    }

    [Fact]
    public void RangeMode_TappingTheStartDayAgain_RestartsTheRange()
    {
        var p = Picker(open: true, configure: x => x.IsRangeMode = true);
        int ranges = 0;
        p.RangeSelected += (_, _) => ranges++;

        TapDay(p, Mar15);

        Assert.Equal(0, ranges);
        Assert.Null(p.RangeEndDate);
    }

    [Fact]
    public void RangeMode_SettingTheEndDate_RaisesRangeSelectedOnlyOnChange()
    {
        var p = Picker(configure: x => x.IsRangeMode = true);
        var ranges = new List<DateRangeSelectedEventArgs>();
        p.RangeSelected += (_, e) => ranges.Add(e);

        p.RangeEndDate = new DateTime(2024, 3, 18);
        p.RangeEndDate = new DateTime(2024, 3, 18);
        p.RangeEndDate = null;

        Assert.Equal(2, ranges.Count);
        Assert.Equal(new DateTime(2024, 3, 18), ranges[0].EndDate);
        Assert.Null(ranges[1].EndDate);
    }

    [Fact]
    public void RangeMode_ShowsTheRangeInTheFieldAndShadesTheDaysBetween()
    {
        var p = Picker(configure: x => x.IsRangeMode = true);
        p.RangeEndDate = new DateTime(2024, 3, 18);

        var field = Draw(p);
        Assert.Single(field.TextsEqual("2024-03-15 — 2024-03-18"));

        p.OpenCalendar();
        var ctx = Draw(p);
        var shaded = ctx.Fills.Where(o => o.Kind == DrawOpKind.FillRect && o.Color.A == 25).ToList();
        Assert.Equal(2, shaded.Count); // the 16th and 17th; the ends are filled with the selected color
        var selected = ctx.Fills.Where(o => o.Kind == DrawOpKind.FillRoundRect && o.Color.Equals(p.SelectedDayColor)).ToList();
        Assert.Equal(2, selected.Count);
    }

    // ── Time row ──────────────────────────────────────────────────

    private static DrawnDatePicker TimePicker(int hour = 9, int minute = 30, bool h24 = false)
    {
        var p = Picker(open: true, configure: x => { x.ShowTimePicker = true; x.Use24HourFormat = h24; });
        p.SelectedHour = hour;
        p.SelectedMinute = minute;
        return p;
    }

    // Time row: centered at x = 116; its arrows are above y = 316 (up) and below it (down)
    private const float HourX = 60, MinuteX = 128, AmPmX = 150, UpY = 305, DownY = 325;

    [Fact]
    public void TimeRow_ExtendsTheCalendarAndShowsTheTime()
    {
        var p = TimePicker();

        Assert.Equal(308, p.GetCalendarBounds().Height);
        Assert.Single(Draw(p).TextsEqual("09:30 AM"));
        Assert.Single(Draw(p).TextsEqual("2024-03-15 09:30 AM"));
    }

    [Fact]
    public void TimeRow_HourAndMinuteArrows_StepAndWrap()
    {
        var p = TimePicker(hour: 23, minute: 59, h24: true);
        var changes = new List<KumikoTimeChangedEventArgs>();
        p.TimeChanged += (_, e) => changes.Add(e);

        p.Down(HourX, UpY);
        Assert.Equal(0, p.SelectedHour);
        p.Down(HourX, DownY);
        Assert.Equal(23, p.SelectedHour);

        p.Down(MinuteX, UpY);
        Assert.Equal(0, p.SelectedMinute);
        p.Down(MinuteX, DownY);
        Assert.Equal(59, p.SelectedMinute);

        Assert.Equal(4, changes.Count);
        Assert.Equal((23, 59, 0, 59), (changes[0].OldHour, changes[0].OldMinute, changes[0].NewHour, changes[0].NewMinute));
        Assert.Equal((0, 59, 23, 59), (changes[1].OldHour, changes[1].OldMinute, changes[1].NewHour, changes[1].NewMinute));
        Assert.Equal((23, 59, 23, 0), (changes[2].OldHour, changes[2].OldMinute, changes[2].NewHour, changes[2].NewMinute));
        Assert.Equal((23, 0, 23, 59), (changes[3].OldHour, changes[3].OldMinute, changes[3].NewHour, changes[3].NewMinute));
        Assert.Equal(Mar15.AddHours(23).AddMinutes(59), p.SelectedDateTime);
        Assert.True(p.IsCalendarOpen);
    }

    [Fact]
    public void TimeRow_AmPmArea_TogglesTwelveHours_Only_In12HourFormat()
    {
        var p = TimePicker(hour: 9, minute: 30);
        KumikoTimeChangedEventArgs? args = null;
        p.TimeChanged += (_, e) => args = e;

        p.Down(AmPmX, UpY);
        Assert.Equal(21, p.SelectedHour);
        Assert.Equal(30, p.SelectedMinute);
        Assert.Equal(9, args!.OldHour);
        Assert.Equal(21, args.NewHour);
        Assert.Single(Draw(p).TextsEqual("09:30 PM"));

        var p24 = TimePicker(hour: 9, minute: 30, h24: true);
        p24.Down(AmPmX, UpY);
        Assert.Equal(9, p24.SelectedHour);
        Assert.Equal(31, p24.SelectedMinute);
    }

    [Fact]
    public void TimeRow_ShowsNoonAndMidnightAsTwelve()
    {
        Assert.Single(Draw(TimePicker(hour: 0, minute: 5)).TextsEqual("12:05 AM"));
        Assert.Single(Draw(TimePicker(hour: 12, minute: 5)).TextsEqual("12:05 PM"));
        Assert.Single(Draw(TimePicker(hour: 15, minute: 5, h24: true)).TextsEqual("15:05"));
    }

    // ── Drawing ───────────────────────────────────────────────────

    [Fact]
    public void Draw_Closed_ShowsOnlyTheFormattedDate()
    {
        var p = Picker(configure: x => x.DateFormat = "dd/MM/yyyy");

        var ctx = Draw(p);

        Assert.Single(ctx.TextsEqual("15/03/2024"));
        Assert.DoesNotContain(ctx.Texts, t => t.Text == "Mo");
    }

    [Fact]
    public void Draw_Open_ShowsWeekdayHeadersAndTheSelectedDayHighlighted()
    {
        var p = Picker(open: true);

        var ctx = Draw(p);

        foreach (var day in new[] { "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" })
            Assert.Single(ctx.TextsEqual(day));
        var (x, y) = Cell(Mar15);
        var highlight = ctx.Fills.Single(o => o.Kind == DrawOpKind.FillRoundRect && o.Color.Equals(p.SelectedDayColor));
        Assert.True(highlight.Rect.Contains(x, y));
        Assert.Equal(32, highlight.Rect.Width);
        // The selected day's number uses the selected-day text color
        var number = ctx.Texts.Single(t => t.Text == "15" && highlight.Rect.Contains(t.Rect.X + 1, t.Rect.Y + 1));
        Assert.Equal(p.SelectedDayTextColor, number.Color);
    }

    [Fact]
    public void Draw_Open_DimsDaysOutsideMinAndMax()
    {
        var p = Picker(open: true, configure: x => x.MinDate = new DateTime(2024, 3, 10));

        var ctx = Draw(p);

        var disabledColor = new KumikoColor(200, 200, 200);
        var dimmed = ctx.Texts.Where(t => t.Color.Equals(disabledColor)).Select(t => t.Text).ToList();
        Assert.Contains("9", dimmed);
        Assert.DoesNotContain("12", dimmed);
    }

    [Fact]
    public void ApplyTheme_TakesTheColorsFromTheGridStyle()
    {
        var style = new KumikoStyle();
        var p = new DrawnDatePicker();

        p.ApplyTheme(style);

        Assert.Equal(style.BackgroundColor, p.BackgroundColor);
        Assert.Equal(style.CellTextColor, p.TextColor);
        Assert.Equal(style.AccentColor, p.SelectedDayColor);
        Assert.Equal(style.AccentColor, p.FocusedBorderColor);
    }
}

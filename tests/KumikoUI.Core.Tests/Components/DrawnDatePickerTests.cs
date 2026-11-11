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
        // English names ("Su", "January", "AM") and an ISO date, whatever culture the machine running the tests uses
        var p = new DrawnDatePicker
        {
            Bounds = new KumikoRect(0, 0, 200, 30),
            Culture = System.Globalization.CultureInfo.GetCultureInfo("en-US"),
            DateFormat = "yyyy-MM-dd",
        };
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
        // 2022 is two rows (52 px) higher. A year is picked when the press is released without a drag.
        Assert.True(p.Down(120, 68 + 100 - 52 + 13));
        Assert.Contains(Draw(p).Texts, t => t.Text == "2023"); // the list is still open: the pick waits for the release
        Assert.True(p.Up(120, 68 + 100 - 52 + 13));

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
        Assert.False(p.Key(KumikoKey.F2));
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

        // The header names come from the culture's own data, which differs between ICU and Windows NLS ("Su" vs "S")
        var format = p.Culture!.DateTimeFormat;
        foreach (var day in Enum.GetValues<DayOfWeek>())
            Assert.NotEmpty(ctx.TextsEqual(format.GetShortestDayName(day)));
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

    // ── Culture ───────────────────────────────────────────────────

    private static CultureInfo Culture(string name) => CultureInfo.GetCultureInfo(name);

    private static DrawnDatePicker CulturePicker(string culture, DateTime? date = null, bool open = true, Action<DrawnDatePicker>? configure = null)
    {
        var p = new DrawnDatePicker { Bounds = new KumikoRect(0, 0, 200, 30), Culture = Culture(culture) };
        configure?.Invoke(p);
        p.SelectedDate = date ?? Mar15;
        if (open) p.OpenCalendar();
        return p;
    }

    /// <summary>The center of a day's cell, in a calendar whose weeks start on <paramref name="firstDay"/>.</summary>
    private static (float X, float Y) CellFrom(DateTime date, DayOfWeek firstDay)
    {
        int lead = ((int)new DateTime(date.Year, date.Month, 1).DayOfWeek - (int)firstDay + 7) % 7;
        int index = lead + date.Day - 1;
        int column = index % 7;
        int row = index / 7;
        return (4 + column * 32f + 16, 104 + row * 32f + 16);
    }

    /// <summary>The weekday headers, left to right.</summary>
    private static string[] Headers(DrawnDatePicker p) =>
        Draw(p).Texts.Where(t => t.Rect.Y > 68 && t.Rect.Y < 104 && t.Rect.Width > 0).OrderBy(t => t.Rect.X).Select(t => t.Text!).ToArray();

    private static string ShortestName(string culture, DayOfWeek day) => Culture(culture).DateTimeFormat.GetShortestDayName(day);

    [Fact]
    public void German_StartsTheWeekOnMonday_WithGermanNames()
    {
        var p = CulturePicker("de-DE");

        var expected = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
            DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday }.Select(d => ShortestName("de-DE", d)).ToArray();
        Assert.Equal(expected, Headers(p));
        Assert.Single(Draw(p).TextsEqual(Culture("de-DE").DateTimeFormat.GetMonthName(3))); // März
        Assert.DoesNotContain(Draw(p).Texts, t => t.Text == "March");
    }

    [Fact]
    public void German_PlacesDaysUnderMondayFirstColumns()
    {
        var p = CulturePicker("de-DE");

        // Monday March 4 is the first column of the second row; Sunday March 17 the last of the third
        var monday = new DateTime(2024, 3, 4);
        var (mx, my) = CellFrom(monday, DayOfWeek.Monday);
        Assert.Equal(4 + 16, mx);
        p.Tap(mx, my);
        Assert.Equal(monday, p.SelectedDate);

        p.OpenCalendar();
        var sunday = new DateTime(2024, 3, 17);
        var (sx, sy) = CellFrom(sunday, DayOfWeek.Monday);
        Assert.Equal(4 + 6 * 32 + 16, sx);
        p.Tap(sx, sy);
        Assert.Equal(sunday, p.SelectedDate);
    }

    [Fact]
    public void English_StillStartsTheWeekOnSunday()
    {
        var p = CulturePicker("en-US");

        Assert.Equal(ShortestName("en-US", DayOfWeek.Sunday), Headers(p)[0]);
        Assert.Equal(ShortestName("en-US", DayOfWeek.Saturday), Headers(p)[6]);
    }

    [Fact]
    public void FirstDayOfWeek_OverridesTheCulture()
    {
        var p = CulturePicker("en-US", configure: x => x.FirstDayOfWeek = DayOfWeek.Wednesday);

        Assert.Equal(ShortestName("en-US", DayOfWeek.Wednesday), Headers(p)[0]);
        var (x, y) = CellFrom(Mar15, DayOfWeek.Wednesday); // a Friday: the third column
        Assert.Equal(4 + 2 * 32 + 16, x);
        p.Tap(x, y);
        Assert.Equal(new DateTime(2024, 3, 15), p.SelectedDate);

        p.OpenCalendar();
        p.FirstDayOfWeek = null;
        Assert.Equal(ShortestName("en-US", DayOfWeek.Sunday), Headers(p)[0]);
    }

    [Fact]
    public void Weekends_AreColoredOnSaturdayAndSunday_WhateverColumnTheyAreIn()
    {
        var p = CulturePicker("de-DE");

        var ctx = Draw(p);

        // March 2024: the 16th is a Saturday, the 17th a Sunday, the 18th a Monday
        Assert.Equal(p.WeekendColor, ctx.Texts.First(t => t.Text == "16" && t.Rect.Y > 104).Color);
        Assert.Equal(p.WeekendColor, ctx.Texts.First(t => t.Text == "17" && t.Rect.Y > 104).Color);
        Assert.Equal(p.TextColor, ctx.Texts.First(t => t.Text == "18" && t.Rect.Y > 104).Color);
    }

    [Theory]
    [InlineData("en-US", "3/15/2024")]
    [InlineData("de-DE", "15.03.2024")]
    [InlineData("ja-JP", "2024/03/15")]
    [InlineData("fr-FR", "15/03/2024")]
    public void TheField_ShowsTheDateInTheCulturesOrder(string culture, string expected)
    {
        var p = CulturePicker(culture, open: false);

        Assert.Single(Draw(p).TextsEqual(expected));
    }

    [Fact]
    public void DateFormat_OverridesTheCulturesPattern()
    {
        var p = CulturePicker("de-DE", open: false, configure: x => x.DateFormat = "yyyy-MM-dd");

        Assert.Single(Draw(p).TextsEqual("2024-03-15"));
    }

    [Fact]
    public void Japanese_PutsTheYearBeforeTheMonth_AndNamesThemInJapanese()
    {
        var p = CulturePicker("ja-JP");
        var ja = Culture("ja-JP").DateTimeFormat;

        var ctx = Draw(p);

        var year = ctx.SingleText("2024");
        var month = ctx.SingleText(ja.GetMonthName(3));
        Assert.True(year.Rect.X < month.Rect.X, "the year is the first label");
        Assert.Equal(ShortestName("ja-JP", DayOfWeek.Sunday), Headers(p)[0]);
    }

    [Fact]
    public void Japanese_YearLabelOnTheLeftOpensTheYearList_AndMonthLabelOnTheRightTheMonthList()
    {
        var p = CulturePicker("ja-JP");
        var ja = Culture("ja-JP").DateTimeFormat;

        p.Down(80, NavY);
        Assert.Contains(Draw(p).Texts, t => t.Text == "2023"); // the year list (the header shows 2024)
        p.Down(500, 500);                                      // closes the dropdown

        p.Down(150, NavY);
        Assert.Contains(Draw(p).Texts, t => t.Text == ja.GetMonthName(12)); // the month list
    }

    [Fact]
    public void ChangingTheCulture_RedrawsTheDateText()
    {
        var p = CulturePicker("en-US", open: false);
        Assert.Single(Draw(p).TextsEqual("3/15/2024"));

        p.Culture = Culture("de-DE");

        Assert.Single(Draw(p).TextsEqual("15.03.2024"));
    }

    // ── Limits ────────────────────────────────────────────────────

    [Fact]
    public void DaysOutsideTheRange_AreDrawnDisabled_AndCannotBePicked()
    {
        var p = CulturePicker("en-US", configure: x => { x.MinDate = new DateTime(2024, 3, 10); x.MaxDate = new DateTime(2024, 3, 20); });

        var ctx = Draw(p);

        var dimmed = ctx.Texts.Where(t => t.Rect.Y > 104 && t.Color.Equals(p.DisabledDayColor)).Select(t => t.Text).ToList();
        Assert.Contains("9", dimmed);
        Assert.Contains("21", dimmed);
        Assert.DoesNotContain("10", dimmed);
        Assert.DoesNotContain("20", dimmed);

        p.Tap(CellFrom(new DateTime(2024, 3, 9), DayOfWeek.Sunday).X, CellFrom(new DateTime(2024, 3, 9), DayOfWeek.Sunday).Y);
        p.Tap(CellFrom(new DateTime(2024, 3, 21), DayOfWeek.Sunday).X, CellFrom(new DateTime(2024, 3, 21), DayOfWeek.Sunday).Y);
        Assert.Equal(Mar15, p.SelectedDate);
        Assert.True(p.IsCalendarOpen);
    }

    [Fact]
    public void TheRange_IsComparedByDay_NotByTime()
    {
        var p = CulturePicker("en-US", configure: x => { x.MinDate = new DateTime(2024, 3, 10, 18, 0, 0); x.MaxDate = new DateTime(2024, 3, 20, 6, 0, 0); });

        Assert.Equal(new DateTime(2024, 3, 10), p.MinDate);
        var (x1, y1) = CellFrom(new DateTime(2024, 3, 10), DayOfWeek.Sunday);
        var (x2, y2) = CellFrom(new DateTime(2024, 3, 20), DayOfWeek.Sunday);
        p.Tap(x1, y1);
        Assert.Equal(new DateTime(2024, 3, 10), p.SelectedDate);
        p.OpenCalendar();
        p.Tap(x2, y2);
        Assert.Equal(new DateTime(2024, 3, 20), p.SelectedDate);
    }

    [Fact]
    public void MinDate_NeverExceedsMaxDate_TheLastLimitSetWins()
    {
        var p = new DrawnDatePicker();
        p.MaxDate = new DateTime(2024, 3, 20);

        p.MinDate = new DateTime(2024, 6, 1);
        Assert.Equal(new DateTime(2024, 6, 1), p.MaxDate);

        p.MaxDate = new DateTime(2024, 1, 1);
        Assert.Equal(new DateTime(2024, 1, 1), p.MinDate);

        p.MinDate = null;
        p.MaxDate = null;
        Assert.Null(p.MinDate);
        Assert.Null(p.MaxDate);
    }

    [Fact]
    public void ArrowsAndHomeAndEnd_StopAtTheLimits()
    {
        var p = CulturePicker("en-US", date: new DateTime(2024, 3, 19), configure: x => { x.MinDate = new DateTime(2024, 3, 12); x.MaxDate = new DateTime(2024, 3, 20); });

        p.Key(KumikoKey.Right);
        Assert.Equal(new DateTime(2024, 3, 20), p.SelectedDate);
        p.Key(KumikoKey.Right);
        p.Key(KumikoKey.Down);
        Assert.Equal(new DateTime(2024, 3, 20), p.SelectedDate); // no step into disabled days

        p.Key(KumikoKey.PageDown);
        Assert.Equal(new DateTime(2024, 3, 20), p.SelectedDate);
        p.Key(KumikoKey.PageUp);
        Assert.Equal(new DateTime(2024, 3, 12), p.SelectedDate); // a month back, clamped to the minimum
        p.Key(KumikoKey.Home);
        Assert.Equal(new DateTime(2024, 3, 12), p.SelectedDate);  // the week starts before the minimum
        p.Key(KumikoKey.Up);
        Assert.Equal(new DateTime(2024, 3, 12), p.SelectedDate);
    }

    [Fact]
    public void TheCalendar_CannotBrowseBeyondTheMonthsOfTheRange()
    {
        var p = CulturePicker("en-US", configure: x => { x.MinDate = new DateTime(2024, 2, 10); x.MaxDate = new DateTime(2024, 4, 20); });
        var en = Culture("en-US").DateTimeFormat;

        p.PreviousMonth();
        p.PreviousMonth();
        p.PreviousMonth();
        Assert.Single(Draw(p).TextsEqual(en.GetMonthName(2)));

        p.NextMonth();
        p.NextMonth();
        p.NextMonth();
        p.NextMonth();
        Assert.Single(Draw(p).TextsEqual(en.GetMonthName(4)));
    }

    [Fact]
    public void TheCalendar_StopsAtTheEndsOfTheDateRange()
    {
        var first = CulturePicker("en-US", date: new DateTime(1, 1, 5));
        first.PreviousMonth();
        first.Key(KumikoKey.PageUp);
        first.Key(KumikoKey.Left);
        Assert.Single(Draw(first).TextsEqual("January")); // the leading days of the grid don't overflow

        var last = CulturePicker("en-US", date: new DateTime(9999, 12, 20));
        last.NextMonth();
        last.Key(KumikoKey.PageDown);
        last.Key(KumikoKey.Down);
        last.Key(KumikoKey.End);
        Assert.Equal(9999, last.SelectedDate!.Value.Year);
        Assert.Single(Draw(last).TextsEqual("December"));
    }

    // ── Keyboard ──────────────────────────────────────────────────

    [Fact]
    public void PageUpAndPageDown_MoveTheSelectionByAMonth_AndShiftByAYear()
    {
        var p = CulturePicker("en-US");

        p.Key(KumikoKey.PageDown);
        Assert.Equal(new DateTime(2024, 4, 15), p.SelectedDate);
        p.Key(KumikoKey.PageUp);
        p.Key(KumikoKey.PageUp);
        Assert.Equal(new DateTime(2024, 2, 15), p.SelectedDate);

        p.Key(KumikoKey.PageDown, modifiers: InputModifiers.Shift);
        Assert.Equal(new DateTime(2025, 2, 15), p.SelectedDate);
        p.Key(KumikoKey.PageUp, modifiers: InputModifiers.Shift);
        p.Key(KumikoKey.PageUp, modifiers: InputModifiers.Shift);
        Assert.Equal(new DateTime(2023, 2, 15), p.SelectedDate);
    }

    [Fact]
    public void PageDown_FromTheEndOfAMonth_LandsOnTheLastDayOfTheNext()
    {
        var p = CulturePicker("en-US", date: new DateTime(2024, 1, 31));

        p.Key(KumikoKey.PageDown);

        Assert.Equal(new DateTime(2024, 2, 29), p.SelectedDate);
        Assert.Single(Draw(p).TextsEqual("February"));
    }

    [Theory]
    [InlineData("en-US", 10, 16)]  // Sunday March 10 .. Saturday March 16
    [InlineData("de-DE", 11, 17)]  // Monday March 11 .. Sunday March 17
    public void HomeAndEnd_GoToTheFirstAndLastDayOfTheWeek(string culture, int first, int last)
    {
        var p = CulturePicker(culture);

        p.Key(KumikoKey.Home);
        Assert.Equal(new DateTime(2024, 3, first), p.SelectedDate);

        p.SelectedDate = Mar15;
        p.Key(KumikoKey.End);
        Assert.Equal(new DateTime(2024, 3, last), p.SelectedDate);
    }

    [Fact]
    public void Keys_RaiseTheSameEventsAsAClick()
    {
        var p = CulturePicker("en-US");
        var moves = new List<DateTime>();
        p.DateSelected += (_, e) => moves.Add(e.NewDate);

        p.Key(KumikoKey.Home);
        p.Key(KumikoKey.PageDown);

        Assert.Equal(new[] { new DateTime(2024, 3, 10), new DateTime(2024, 4, 10) }, moves);
    }

    [Fact]
    public void TheMouseWheel_MovesTheCalendarByAMonth()
    {
        var p = CulturePicker("en-US");
        var en = Culture("en-US").DateTimeFormat;

        Assert.True(p.OnScroll(new KumikoPointerEventArgs { X = 100, Y = 150, ScrollDeltaY = -1, Action = InputAction.Scroll }));
        Assert.Single(Draw(p).TextsEqual(en.GetMonthName(4)));
        Assert.True(p.OnScroll(new KumikoPointerEventArgs { X = 100, Y = 150, ScrollDeltaY = 2, Action = InputAction.Scroll }));
        Assert.Single(Draw(p).TextsEqual(en.GetMonthName(3)));
        Assert.Equal(Mar15, p.SelectedDate);
    }

    // ── Year list ─────────────────────────────────────────────────

    private static IEnumerable<int> YearsListed(DrawnDatePicker p) =>
        Draw(p).Texts.Where(t => t.Rect.X > 100 && t.Rect.Y > 62 && t.Text!.Length == 4 && int.TryParse(t.Text, out _)).Select(t => int.Parse(t.Text!));

    [Fact]
    public void TheYearList_ReachesYearsFarFromToday()
    {
        var p = CulturePicker("en-US", date: new DateTime(1950, 6, 15));
        p.Down(150, NavY); // the year label

        var years = YearsListed(p).ToList();
        Assert.Contains(1950, years);

        // Centered on the displayed year: tap it, two rows up is 1948
        p.Up(150, NavY);
        p.Down(120, 68 + 100 - 52 + 13);
        p.Up(120, 68 + 100 - 52 + 13);

        Assert.Single(Draw(p).TextsEqual("1948"));
        Assert.Equal(new DateTime(1950, 6, 15), p.SelectedDate); // browsing doesn't select
    }

    [Fact]
    public void TheYearList_ScrollsWithTheWheel_FromTheFirstYearToTheLast()
    {
        var p = CulturePicker("en-US");
        p.Down(150, NavY);

        for (int i = 0; i < 200; i++)
            p.OnScroll(new KumikoPointerEventArgs { X = 120, Y = 150, ScrollDeltaY = 3, Action = InputAction.Scroll });
        Assert.Contains(1900, YearsListed(p));
        Assert.DoesNotContain(1899, YearsListed(p));

        for (int i = 0; i < 400; i++)
            p.OnScroll(new KumikoPointerEventArgs { X = 120, Y = 150, ScrollDeltaY = -3, Action = InputAction.Scroll });
        Assert.Contains(2100, YearsListed(p));
        Assert.DoesNotContain(2101, YearsListed(p));
    }

    [Fact]
    public void TheYearList_ScrollsByDragging_AndOnlyAPlainPressPicksAYear()
    {
        var p = CulturePicker("en-US");
        p.Down(150, NavY);
        var before = YearsListed(p).Max();

        p.Down(120, 150);
        p.Move(120, 100); // 50 px up: later years come into view
        p.Up(120, 100);

        Assert.True(YearsListed(p).Max() > before);
        Assert.Contains(Draw(p).Texts, t => t.Text == "2024" && t.Rect.X > 100); // dropdown still open, nothing picked
    }

    [Fact]
    public void TheYearList_FollowsTheRange()
    {
        var p = CulturePicker("en-US", configure: x => { x.MinDate = new DateTime(2020, 1, 1); x.MaxDate = new DateTime(2030, 12, 31); });
        p.Down(150, NavY);

        var years = YearsListed(p).ToList();

        Assert.Contains(2020, years);
        Assert.DoesNotContain(2019, years);
        for (int i = 0; i < 50; i++)
            p.OnScroll(new KumikoPointerEventArgs { X = 120, Y = 150, ScrollDeltaY = -3, Action = InputAction.Scroll });
        Assert.Contains(2030, YearsListed(p));
        Assert.DoesNotContain(2031, YearsListed(p));
    }

    [Fact]
    public void ACanceledPress_PicksNothing()
    {
        var p = CulturePicker("en-US");
        var (x, y) = CellFrom(new DateTime(2024, 3, 20), DayOfWeek.Sunday);

        p.Down(x, y);
        p.OnPointerCancel(Ptr(InputAction.Cancelled, x, y));
        p.Up(x, y);

        Assert.Equal(Mar15, p.SelectedDate);
    }

    // ── Typed entry ───────────────────────────────────────────────

    private static void Type(DrawnDatePicker p, string text)
    {
        foreach (char c in text)
            p.Key(KumikoKey.Character, c);
    }

    private static object? EditorValue(DrawnDatePicker p) => ((IValueEditor)p).Value;

    [Theory]
    [InlineData("de-DE", "20.03.2024")]
    [InlineData("en-US", "3/20/2024")]
    [InlineData("ja-JP", "2024/03/20")]
    [InlineData("fr-FR", "20/03/2024")]
    [InlineData("en-US", "2024-03-20")]
    public void ATypedDate_IsParsedInTheCulture_AndConfirmedWithEnter(string culture, string typed)
    {
        var p = CulturePicker(culture);
        int completed = 0;
        p.EditCompleted += () => completed++;
        p.StartTrackingChanges();

        Type(p, typed);
        Assert.Equal(new DateTime(2024, 3, 20), EditorValue(p)); // what a commit would write
        Assert.True(((IValueEditor)p).IsDirty);
        Assert.True(p.Key(KumikoKey.Enter));

        Assert.Equal(new DateTime(2024, 3, 20), p.SelectedDate);
        Assert.False(p.IsCalendarOpen);
        Assert.Equal(1, completed);
    }

    [Fact]
    public void TypingReplacesTheDate_ItDoesNotAppendToIt()
    {
        var p = CulturePicker("de-DE");

        p.Key(KumikoKey.Character, '2');

        Assert.Equal("2", Assert.IsType<string>(EditorValue(p)));
    }

    [Fact]
    public void TheTypedText_IsShownInTheFieldWithACaret_AndTheCalendarFollowsIt()
    {
        var p = CulturePicker("de-DE");
        var de = Culture("de-DE").DateTimeFormat;

        Type(p, "05.07.2025");
        var ctx = Draw(p);

        Assert.Single(ctx.TextsEqual("05.07.2025"));
        Assert.Single(ctx.TextsEqual(de.GetMonthName(7))); // the month of the date being typed
        Assert.Equal(Mar15, p.SelectedDate);               // nothing selected until it is confirmed
    }

    [Theory]
    [InlineData("de-DE", "31.02.2024")]
    [InlineData("de-DE", "abc")]
    [InlineData("en-US", "13/45/2024")]
    [InlineData("de-DE", "")]
    public void ATypedText_ThatIsNotADate_IsNotSelected_AndIsDrawnAsAnError(string culture, string typed)
    {
        var p = CulturePicker(culture);
        int completed = 0;
        p.EditCompleted += () => completed++;
        p.StartTrackingChanges();

        p.Key(KumikoKey.Character, 'x'); // starts typing and selects everything
        p.Key(KumikoKey.Backspace);
        Type(p, typed);

        Assert.Equal(typed, EditorValue(p) as string); // the edit session refuses it
        Assert.True(p.Key(KumikoKey.Enter));
        Assert.Equal(1, completed);                    // reported complete: the session shows its validation message
        Assert.Equal(Mar15, p.SelectedDate);
        Assert.True(p.IsCalendarOpen);
        Assert.Contains(Draw(p).Ops, o => o.Kind == DrawOpKind.DrawRect && o.Color.Equals(p.ErrorBorderColor) && Math.Abs(o.Rect.Y) < 0.001f);
    }

    [Fact]
    public void ATypedDate_OutsideTheRange_IsNotAccepted()
    {
        var p = CulturePicker("de-DE", configure: x => x.MaxDate = new DateTime(2024, 12, 31));

        Type(p, "01.01.2025");

        var value = EditorValue(p);
        Assert.IsNotType<DateTime>(value);
        Assert.IsNotType<string>(value);
        Assert.True(p.Key(KumikoKey.Enter));
        Assert.Equal(Mar15, p.SelectedDate);
        Assert.Contains(Draw(p).Ops, o => o.Kind == DrawOpKind.DrawRect && o.Color.Equals(p.ErrorBorderColor) && Math.Abs(o.Rect.Y) < 0.001f);
    }

    [Fact]
    public void Escape_DropsTheTypedText_ThenClosesTheCalendar()
    {
        var p = CulturePicker("de-DE");
        Type(p, "01.01.2025");

        Assert.True(p.Key(KumikoKey.Escape));
        Assert.True(p.IsCalendarOpen);
        Assert.Equal(Mar15, p.SelectedDate);
        Assert.Equal(Mar15, EditorValue(p));
        Assert.Single(Draw(p).TextsEqual("15.03.2024"));

        Assert.True(p.Key(KumikoKey.Escape));
        Assert.False(p.IsCalendarOpen);
    }

    [Fact]
    public void WhileTyping_LeftRightHomeEnd_EditTheText_NotTheDate()
    {
        var p = CulturePicker("de-DE");
        Type(p, "5.3.24");

        p.Key(KumikoKey.Home);
        p.Key(KumikoKey.Character, '2');
        p.Key(KumikoKey.End);
        p.Key(KumikoKey.Left);
        p.Key(KumikoKey.Left);
        Type(p, "20");

        Assert.Equal(Mar15, p.SelectedDate); // the arrows edited the text, they did not move the date
        Assert.Single(Draw(p).TextsEqual("25.3.2024"));
        Assert.Equal(new DateTime(2024, 3, 25), EditorValue(p));
    }

    [Fact]
    public void WhileTyping_UpAndDown_ApplyTheDateAndThenMoveTheCalendar()
    {
        var p = CulturePicker("de-DE");
        Type(p, "01.04.2024");

        p.Key(KumikoKey.Down);

        Assert.Equal(new DateTime(2024, 4, 8), p.SelectedDate);
        Assert.Single(Draw(p).TextsEqual("08.04.2024"));
    }

    [Fact]
    public void LosingFocus_AppliesATypedDate_AndDropsTheRest()
    {
        var p = CulturePicker("de-DE");
        var focus = Focus(p);
        Type(p, "20.03.2024");
        focus.SetFocus(null);
        Assert.Equal(new DateTime(2024, 3, 20), p.SelectedDate);

        focus.SetFocus(p);
        Type(p, "nonsense");
        focus.SetFocus(null);
        Assert.Equal(new DateTime(2024, 3, 20), p.SelectedDate);
        Assert.False(p.IsCalendarOpen);
    }

    [Fact]
    public void ATypedTime_IsKeptWhenTheTimePickerIsShown()
    {
        var p = CulturePicker("en-US", configure: x => x.ShowTimePicker = true);

        Type(p, "3/20/2024 2:45 PM");

        Assert.Equal(new DateTime(2024, 3, 20, 14, 45, 0), EditorValue(p));
        p.Key(KumikoKey.Enter);
        Assert.Equal(14, p.SelectedHour);
        Assert.Equal(45, p.SelectedMinute);
        Assert.Equal(new DateTime(2024, 3, 20), p.SelectedDate);
    }

    [Fact]
    public void ATypedDate_KeepsTheEditedCellsTimeOfDay()
    {
        var p = CulturePicker("de-DE", configure: x => x.TimeOfDay = new TimeSpan(9, 30, 0));

        Type(p, "20.03.2024");

        Assert.Equal(new DateTime(2024, 3, 20, 9, 30, 0), EditorValue(p));
    }

    [Fact]
    public void ARangePicker_HasNoTypedEntry()
    {
        var p = CulturePicker("de-DE", configure: x => x.IsRangeMode = true);

        Assert.False(p.Key(KumikoKey.Character, '2'));
    }

    [Fact]
    public void WithTheCalendarClosed_TypingStillEntersADate_AndSpaceStillOpensIt()
    {
        var p = CulturePicker("de-DE", open: false);

        Assert.True(p.Key(KumikoKey.Character, '2'));
        Assert.False(p.IsCalendarOpen);
        Type(p, "0.03.2024");
        Assert.True(p.Key(KumikoKey.Enter)); // applies the typed date, as it does with the calendar open
        Assert.Equal(new DateTime(2024, 3, 20), p.SelectedDate);

        var q = CulturePicker("de-DE", open: false);
        Assert.True(q.Key(KumikoKey.Space));
        Assert.True(q.IsCalendarOpen);
    }

    [Fact]
    public void ADateColumn_WithNoValue_StartsEmpty_AndATypedDateSelectsIt()
    {
        var p = new DrawnDatePicker { Bounds = new KumikoRect(0, 0, 200, 30), Culture = Culture("de-DE") };
        p.SelectedDate = null;
        p.OpenCalendar();
        p.StartTrackingChanges();

        Type(p, "20.03.2024");

        var editor = (IValueEditor)p;
        Assert.True(editor.HasValue);
        Assert.True(editor.IsDirty);
        Assert.Equal(new DateTime(2024, 3, 20), editor.Value);
    }

    // ── Editors made from a column ────────────────────────────────

    [Fact]
    public void TheFactory_GivesTheDateEditorTheColumnsFormatAndLimits()
    {
        var column = new KumikoColumn
        {
            PropertyName = "D",
            ColumnType = KumikoColumnType.Date,
            Format = "dd MMM yyyy",
            EditorMinDate = new DateTime(2024, 1, 1),
            EditorMaxDate = new DateTime(2024, 12, 31),
        };

        var picker = Assert.IsType<DrawnDatePicker>(CellEditorFactory.CreateEditor(
            column, new DateTime(2024, 3, 15), new KumikoRect(0, 0, 200, 30), culture: Culture("de-DE")));

        Assert.Equal("dd MMM yyyy", picker.DateFormat);
        Assert.Equal(new DateTime(2024, 1, 1), picker.MinDate);
        Assert.Equal(new DateTime(2024, 12, 31), picker.MaxDate);
        Assert.Equal(Culture("de-DE"), picker.Culture);
        Assert.Contains(Draw(picker).Texts, t => t.Text == Mar15.ToString("dd MMM yyyy", Culture("de-DE")));
    }

    [Fact]
    public void TheFactory_StartsTypingADate_FromTheCharacterThatOpenedTheEdit()
    {
        var column = new KumikoColumn { PropertyName = "D", ColumnType = KumikoColumnType.Date };

        var picker = Assert.IsType<DrawnDatePicker>(CellEditorFactory.CreateEditor(
            column, new DateTime(2024, 3, 15), new KumikoRect(0, 0, 200, 30), '7', culture: Culture("de-DE")));

        Assert.Equal("7", ((IValueEditor)picker).Value as string);
        Assert.True(((IValueEditor)picker).IsDirty);
    }
}

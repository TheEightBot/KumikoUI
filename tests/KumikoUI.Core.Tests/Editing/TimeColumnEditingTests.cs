using System.Globalization;
using KumikoUI.Core.Components;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Tests.Editing;

/// <summary>
/// Editing <see cref="KumikoColumnType.Time"/> and <see cref="KumikoColumnType.DateTime"/> columns: the editor
/// each opens, 12-hour and 24-hour time, the typing trigger, what is committed for a <see cref="TimeOnly"/>,
/// <see cref="TimeSpan"/>, <see cref="DateTime"/> and <see cref="DateTimeOffset"/> property, and the seconds an
/// edit that leaves the time alone keeps.
/// </summary>
public class TimeColumnEditingTests
{
    internal sealed class Shift
    {
        public TimeOnly Start { get; set; } = new(9, 30, 45);
        public TimeOnly? Break { get; set; }
        public TimeSpan Length { get; set; } = new(2, 15, 30);
        public TimeSpan Overtime { get; set; } = TimeSpan.FromHours(30);
        public DateTime Logged { get; set; } = new(2024, 3, 5, 14, 30, 45, 250);
        public DateTime? Closed { get; set; }
        public DateTimeOffset Sent { get; set; } = new(2024, 3, 5, 8, 15, 20, TimeSpan.FromHours(2));
    }

    private static readonly KumikoRect Bounds = new(0, 30, 150, 30);

    private sealed class Fixture
    {
        public Shift Row { get; } = new();
        public KumikoSource Source { get; }
        public EditSession Session { get; } = new();
        public KumikoColumn Column { get; }
        public List<CellValueChangedEventArgs> Changes { get; } = new();

        public Fixture(string property, KumikoColumnType type, CultureInfo? culture = null, Action<KumikoColumn>? configure = null)
        {
            Column = new KumikoColumn { Header = property, PropertyName = property, ColumnType = type };
            configure?.Invoke(Column);
            Source = new KumikoSource { Culture = culture ?? CultureInfo.GetCultureInfo("en-US") };
            Source.SetColumns(new[] { Column });
            Source.SetItems(new[] { Row });
            Session.CellValueChanged += (_, e) => Changes.Add(e);
        }

        public T Open<T>(char? initial = null) where T : DrawnComponent
        {
            Assert.True(Session.BeginEdit(0, 0, Column, Source, Bounds, initial));
            return Assert.IsType<T>(Session.ActiveEditor);
        }

        public bool Commit() => Session.CommitEdit(Source);
    }

    private static void Press(DrawnComponent editor, KumikoKey key, char? character = null) =>
        editor.OnKeyDown(new KumikoKeyEventArgs { Key = key, Character = character });

    // ── Editor type and start state ───────────────────────────────

    [Fact]
    public void TimeColumn_OpensATimePicker_OnTheCellsTime()
    {
        var f = new Fixture(nameof(Shift.Start), KumikoColumnType.Time);

        var picker = f.Open<DrawnTimePicker>();

        Assert.Equal(new TimeSpan(0, 9, 30, 45), picker.TimeOfDay);
        Assert.Equal(Bounds, picker.Bounds);
        Assert.Equal(KumikoColumnType.Time, f.Column.ColumnType);
    }

    [Fact]
    public void TimeColumn_OnAnEmptyCell_OpensWithNoTime()
    {
        var f = new Fixture(nameof(Shift.Break), KumikoColumnType.Time);

        var picker = f.Open<DrawnTimePicker>();

        Assert.False(picker.HasTime);
    }

    [Fact]
    public void TimeColumn_UsesTheCulturesHourFormat_UnlessTheColumnSaysOtherwise()
    {
        var us = new Fixture(nameof(Shift.Start), KumikoColumnType.Time).Open<DrawnTimePicker>();
        var de = new Fixture(nameof(Shift.Start), KumikoColumnType.Time, CultureInfo.GetCultureInfo("de-DE")).Open<DrawnTimePicker>();
        var forced24 = new Fixture(nameof(Shift.Start), KumikoColumnType.Time, configure: c => c.Use24HourTime = true).Open<DrawnTimePicker>();
        var forced12 = new Fixture(nameof(Shift.Start), KumikoColumnType.Time, CultureInfo.GetCultureInfo("de-DE"), c => c.Use24HourTime = false).Open<DrawnTimePicker>();

        Assert.False(us.Use24HourFormat);
        Assert.True(de.Use24HourFormat);
        Assert.True(forced24.Use24HourFormat);
        Assert.False(forced12.Use24HourFormat);
    }

    [Fact]
    public void TimeColumn_TakesTheMinuteStep()
    {
        var f = new Fixture(nameof(Shift.Start), KumikoColumnType.Time, configure: c => c.TimeMinuteStep = 15);

        Assert.Equal(15, f.Open<DrawnTimePicker>().MinuteStep);
    }

    [Fact]
    public void TimeColumn_CannotEditADurationOfADayOrMore()
    {
        var f = new Fixture(nameof(Shift.Overtime), KumikoColumnType.Time);

        Assert.False(f.Session.BeginEdit(0, 0, f.Column, f.Source, Bounds));
        Assert.False(f.Session.IsEditing);
    }

    [Fact]
    public void DateTimeColumn_OpensTheCalendarWithATimeRow_OnTheCellsValue()
    {
        var f = new Fixture(nameof(Shift.Logged), KumikoColumnType.DateTime, configure: c => c.Use24HourTime = true);

        var picker = f.Open<DrawnDatePicker>();

        Assert.True(picker.ShowTimePicker);
        Assert.True(picker.Use24HourFormat);
        Assert.Equal(new DateTime(2024, 3, 5), picker.SelectedDate);
        Assert.Equal((14, 30), (picker.SelectedHour, picker.SelectedMinute));
        Assert.True(picker.IsCalendarOpen);
    }

    [Fact]
    public void DateTimeColumn_UsesTheCulturesHourFormat()
    {
        var us = new Fixture(nameof(Shift.Logged), KumikoColumnType.DateTime).Open<DrawnDatePicker>();
        var de = new Fixture(nameof(Shift.Logged), KumikoColumnType.DateTime, CultureInfo.GetCultureInfo("de-DE")).Open<DrawnDatePicker>();

        Assert.False(us.Use24HourFormat);
        Assert.True(de.Use24HourFormat);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("d", "d")]
    [InlineData("yyyy-MM-dd", "yyyy-MM-dd")]
    [InlineData("MMMM d, yyyy", "MMMM d, yyyy")]
    [InlineData("g", null)]
    [InlineData("yyyy-MM-dd HH:mm", null)]
    [InlineData("d MMM yyyy h:mm tt", null)]
    [InlineData("yyyy-MM-dd 'at' hh", null)]
    [InlineData("yyyy-MM-dd 'time'", "yyyy-MM-dd 'time'")] // a quoted letter is not a time field
    public void DateTimeColumn_ShowsOnlyTheDatePartOfItsFormat_InTheDateField(string? format, string? expected)
    {
        var f = new Fixture(nameof(Shift.Logged), KumikoColumnType.DateTime, configure: c => c.Format = format);

        var picker = f.Open<DrawnDatePicker>();

        Assert.Equal(expected, picker.DateFormat);
    }

    // ── Typing trigger ────────────────────────────────────────────

    [Fact]
    public void TypingADigit_OpensTheTimeEditorWithThatDigitTyped()
    {
        var f = new Fixture(nameof(Shift.Break), KumikoColumnType.Time, configure: c => c.Use24HourTime = true);

        var picker = f.Open<DrawnTimePicker>('7');

        Assert.Equal(7, picker.Hour);
        Assert.True(((IValueEditor)picker).IsDirty);
    }

    [Fact]
    public void TypingADigit_ThenTheRest_CommitsThatTime()
    {
        var f = new Fixture(nameof(Shift.Start), KumikoColumnType.Time, configure: c => c.Use24HourTime = true);

        var picker = f.Open<DrawnTimePicker>('1');
        foreach (char c in "830") Press(picker, KumikoKey.Character, c);
        f.Commit();

        Assert.Equal(new TimeOnly(18, 30), f.Row.Start);
    }

    // ── Commit: TimeOnly and TimeSpan ─────────────────────────────

    [Fact]
    public void TimeOnlyProperty_GetsTheEditedTime()
    {
        var f = new Fixture(nameof(Shift.Start), KumikoColumnType.Time);
        var picker = f.Open<DrawnTimePicker>();

        Press(picker, KumikoKey.Up);
        Press(picker, KumikoKey.Right);
        Press(picker, KumikoKey.Down);
        f.Commit();

        Assert.Equal(new TimeOnly(10, 29), f.Row.Start);
    }

    [Fact]
    public void NullableTimeOnlyProperty_GetsATimeOnAnEmptyCell()
    {
        var f = new Fixture(nameof(Shift.Break), KumikoColumnType.Time, configure: c => c.Use24HourTime = true);
        var picker = f.Open<DrawnTimePicker>();

        foreach (char c in "1245") Press(picker, KumikoKey.Character, c);
        f.Commit();

        Assert.Equal(new TimeOnly(12, 45), f.Row.Break);
    }

    [Fact]
    public void TimeSpanProperty_GetsATimeSpan()
    {
        var f = new Fixture(nameof(Shift.Length), KumikoColumnType.Time);
        var picker = f.Open<DrawnTimePicker>();

        Press(picker, KumikoKey.Up);
        f.Commit();

        Assert.Equal(new TimeSpan(3, 15, 0), f.Row.Length);
    }

    [Fact]
    public void AnEditThatDoesNotTouchTheTime_LeavesTheValueAlone_WithItsSeconds()
    {
        var f = new Fixture(nameof(Shift.Start), KumikoColumnType.Time);
        f.Open<DrawnTimePicker>();

        f.Commit();

        Assert.Equal(new TimeOnly(9, 30, 45), f.Row.Start);
        Assert.Empty(f.Changes);
    }

    [Fact]
    public void ASteppedAndRestoredTime_NoLongerHasItsSeconds()
    {
        var f = new Fixture(nameof(Shift.Start), KumikoColumnType.Time);
        var picker = f.Open<DrawnTimePicker>();

        Press(picker, KumikoKey.Up);
        Press(picker, KumikoKey.Down);
        f.Commit();

        Assert.Equal(new TimeOnly(9, 30), f.Row.Start);
    }

    // ── Commit: DateTime ──────────────────────────────────────────

    [Fact]
    public void DateTimeProperty_UntouchedKeepsEverySecondAndFraction()
    {
        var f = new Fixture(nameof(Shift.Logged), KumikoColumnType.DateTime);
        f.Open<DrawnDatePicker>();

        f.Commit();

        Assert.Equal(new DateTime(2024, 3, 5, 14, 30, 45, 250), f.Row.Logged);
        Assert.Empty(f.Changes);
    }

    [Fact]
    public void DateTimeProperty_ADayPicked_KeepsTheSecondsOfTheTime()
    {
        var f = new Fixture(nameof(Shift.Logged), KumikoColumnType.DateTime);
        var picker = f.Open<DrawnDatePicker>();

        picker.SelectedDate = new DateTime(2024, 4, 9);
        f.Commit();

        Assert.Equal(new DateTime(2024, 4, 9, 14, 30, 45, 250), f.Row.Logged);
    }

    [Fact]
    public void DateTimeProperty_ATimeChange_CommitsTheNewHourAndMinute()
    {
        var f = new Fixture(nameof(Shift.Logged), KumikoColumnType.DateTime);
        var picker = f.Open<DrawnDatePicker>();

        picker.SelectedHour = 8;
        picker.SelectedMinute = 5;
        picker.SelectedDate = new DateTime(2024, 3, 6);
        f.Commit();

        Assert.Equal(new DateTime(2024, 3, 6, 8, 5, 0), f.Row.Logged);
    }

    [Fact]
    public void NullableDateTimeProperty_GetsADateAndTimeOnAnEmptyCell()
    {
        var f = new Fixture(nameof(Shift.Closed), KumikoColumnType.DateTime);
        var picker = f.Open<DrawnDatePicker>();

        picker.SelectedDate = new DateTime(2025, 1, 2);
        picker.SelectedHour = 17;
        picker.SelectedMinute = 45;
        f.Commit();

        Assert.Equal(new DateTime(2025, 1, 2, 17, 45, 0), f.Row.Closed);
    }

    [Fact]
    public void DateTimeOffsetProperty_KeepsItsTime_WhenOnlyTheDateChanges()
    {
        var f = new Fixture(nameof(Shift.Sent), KumikoColumnType.DateTime);
        var picker = f.Open<DrawnDatePicker>();

        picker.SelectedDate = new DateTime(2024, 3, 9);
        f.Commit();

        Assert.Equal(new DateTime(2024, 3, 9, 8, 15, 20), f.Row.Sent.DateTime);
    }

    [Fact]
    public void DateTimeValueInATimeColumn_ChangesOnlyTheTimeOfDay()
    {
        var f = new Fixture(nameof(Shift.Logged), KumikoColumnType.Time, configure: c => c.Use24HourTime = true);
        var picker = f.Open<DrawnTimePicker>();

        Press(picker, KumikoKey.Up);
        f.Commit();

        Assert.Equal(new DateTime(2024, 3, 5, 15, 30, 0), f.Row.Logged);
    }

    // ── Fallbacks ─────────────────────────────────────────────────

    [Fact]
    public void WithoutAProperty_TheColumnTypeStillPicksTheEditor()
    {
        var timeColumn = new KumikoColumn { Header = "T", ColumnType = KumikoColumnType.Time };

        Assert.IsType<DrawnTimePicker>(CellEditorFactory.CreateEditor(timeColumn, null, Bounds));
        Assert.IsType<DrawnDatePicker>(CellEditorFactory.CreateEditor(
            new KumikoColumn { Header = "D", ColumnType = KumikoColumnType.DateTime }, null, Bounds));
    }

    [Fact]
    public void ThemeAndFont_ReachTheTimePicker()
    {
        var picker = Assert.IsType<DrawnTimePicker>(CellEditorFactory.CreateEditor(
            new KumikoColumn { Header = "T", ColumnType = KumikoColumnType.Time }, null, Bounds));
        var style = new KumikoStyle { CellTextColor = new KumikoColor(9, 8, 7) };

        CellEditorFactory.ApplyThemeToEditor(picker, style);
        CellEditorFactory.ApplyFontToEditor(picker, new KumikoFont("Mono", 20));

        Assert.Equal(new KumikoColor(9, 8, 7), picker.TextColor);
        Assert.Equal(new KumikoFont("Mono", 20), picker.Font);
    }
}

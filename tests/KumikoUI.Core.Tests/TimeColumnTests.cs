using System.Globalization;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests;

/// <summary>
/// <see cref="KumikoColumnType.Time"/> and <see cref="KumikoColumnType.DateTime"/> columns: display formats per
/// culture, sorting, the comparer across <see cref="TimeOnly"/> and <see cref="TimeSpan"/>, filter conditions and
/// the saved layout of a time condition.
/// </summary>
public class TimeColumnTests
{
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    private sealed class Row
    {
        public string Name { get; set; } = "";
        public TimeOnly Start { get; set; }
        public TimeSpan Span { get; set; }
        public DateTime When { get; set; }
        public DateTimeOffset Sent { get; set; }
    }

    private static KumikoColumn Col(string property, KumikoColumnType type, string? format = null) =>
        new() { Header = property, PropertyName = property, ColumnType = type, Format = format };

    // ── Display ───────────────────────────────────────────────────

    // The expected text comes from the culture's own pattern: newer ICU data separates the time and the AM/PM
    // designator with a narrow no-break space instead of a space.
    private static string Expect(CultureInfo culture, string format) => new DateTime(2024, 3, 5, 14, 5, 0).ToString(format, culture);

    [Theory]
    [InlineData("en-US", "t")]
    [InlineData("de-DE", "t")]
    public void TimeColumn_ShowsTheCulturesShortTime_ByDefault(string name, string format)
    {
        var culture = CultureInfo.GetCultureInfo(name);
        string expected = Expect(culture, format);
        var source = new KumikoSource { Culture = culture };
        var column = Col("Start", KumikoColumnType.Time);

        Assert.StartsWith("14:05", Expect(De, "t"));
        Assert.StartsWith("2:05", Expect(En, "t"));

        Assert.Equal(expected, source.FormatCellValue(new TimeOnly(14, 5, 30), column));
        Assert.Equal(expected, source.FormatCellValue(new TimeSpan(14, 5, 30), column));
        Assert.Equal(expected, source.FormatCellValue(new DateTime(2024, 3, 5, 14, 5, 30), column));
    }

    [Theory]
    [InlineData("en-US", "3/5/2024 2:05")]
    [InlineData("de-DE", "05.03.2024 14:05")]
    public void DateTimeColumn_ShowsTheCulturesShortDateAndTime_ByDefault(string name, string start)
    {
        var culture = CultureInfo.GetCultureInfo(name);
        string expected = Expect(culture, "g");
        Assert.StartsWith(start, expected);
        var source = new KumikoSource { Culture = culture };
        var column = Col("When", KumikoColumnType.DateTime);
        var value = new DateTime(2024, 3, 5, 14, 5, 30);

        Assert.Equal(expected, source.FormatCellValue(value, column));
        Assert.Equal(expected, source.FormatCellValue(new DateTimeOffset(value, TimeSpan.FromHours(1)), column));
    }

    [Fact]
    public void AFormat_OverridesTheDefault()
    {
        var source = new KumikoSource { Culture = En };

        Assert.Equal("14:05:30", source.FormatCellValue(new TimeOnly(14, 5, 30), Col("Start", KumikoColumnType.Time, "HH:mm:ss")));
        Assert.Equal("2024-03-05 14:05", source.FormatCellValue(new DateTime(2024, 3, 5, 14, 5, 30), Col("When", KumikoColumnType.DateTime, "yyyy-MM-dd HH:mm")));
    }

    [Theory]
    [InlineData("t")]
    [InlineData("HH:mm")]
    [InlineData("h:mm tt")]
    [InlineData("hh\\:mm")]   // a TimeSpan's own custom format
    [InlineData("c")]        // and its standard one
    public void ATimeSpanTakesTimeFormatsToo(string format)
    {
        var source = new KumikoSource { Culture = En };
        string expected = format switch
        {
            "hh\\:mm" => "14:05",
            "c" => "14:05:00",
            _ => new TimeOnly(14, 5).ToString(format, En),
        };

        Assert.Equal(expected, source.FormatCellValue(new TimeSpan(14, 5, 0), Col("Span", KumikoColumnType.Time, format)));
    }

    [Fact]
    public void ADurationOfADay_FallsBackToTheDurationFormat()
    {
        var source = new KumikoSource { Culture = En };
        var column = Col("Span", KumikoColumnType.Time);

        Assert.Equal("1.02:30:00", source.FormatCellValue(new TimeSpan(1, 2, 30, 0), column));
        Assert.Equal("1.02:30:00", source.FormatCellValue(new TimeSpan(1, 2, 30, 0), Col("Span", KumikoColumnType.Text, "HH:mm")));
    }

    [Fact]
    public void ATextColumnWithoutAFormat_KeepsItsPlainText()
    {
        var source = new KumikoSource { Culture = En };

        Assert.Equal("14:05:00", source.FormatCellValue(new TimeSpan(14, 5, 0), Col("Span", KumikoColumnType.Text)));
    }

    // ── Sorting ───────────────────────────────────────────────────

    [Theory]
    [InlineData(KumikoColumnType.Time, KumikoSortDirection.Ascending)]
    [InlineData(KumikoColumnType.DateTime, KumikoSortDirection.Descending)]
    [InlineData(KumikoColumnType.Date, KumikoSortDirection.Descending)]
    public void FirstSortDirection_FollowsTheColumnType(KumikoColumnType type, KumikoSortDirection expected)
    {
        Assert.Equal(expected, Col("X", type).FirstSortDirection);
    }

    [Fact]
    public void SortLabels_AreTheDateOnesForDateTime_AndTheTimeOnesForTime()
    {
        var strings = new KumikoStrings();

        Assert.Equal(strings.SortDateAscending, strings.SortLabel(KumikoColumnType.DateTime, KumikoSortDirection.Ascending));
        Assert.Equal(strings.SortDateDescending, strings.SortLabel(KumikoColumnType.DateTime, KumikoSortDirection.Descending));
        Assert.Equal("Sort Earliest to Latest", strings.SortLabel(KumikoColumnType.Time, KumikoSortDirection.Ascending));
        Assert.Equal("Sort Latest to Earliest", strings.SortLabel(KumikoColumnType.Time, KumikoSortDirection.Descending));
    }

    [Fact]
    public void SortLabels_CanBeLocalized()
    {
        var strings = new KumikoStrings { SortTimeAscending = "Früheste zuerst", SortTimeDescending = "Späteste zuerst" };

        Assert.Equal("Früheste zuerst", strings.SortLabel(KumikoColumnType.Time, KumikoSortDirection.Ascending));
        Assert.Equal("Späteste zuerst", strings.SortLabel(KumikoColumnType.Time, KumikoSortDirection.Descending));
    }

    [Fact]
    public void ATimeColumn_SortsByTimeOfDay_AcrossTimeOnlyAndTimeSpan()
    {
        var comparer = new KumikoComparer(En);
        var values = new object?[] { new TimeSpan(12, 0, 0), new TimeOnly(8, 30), new TimeSpan(23, 59, 0), null, new TimeOnly(0, 15) };

        var sorted = values.OrderBy(v => v, comparer).ToArray();

        Assert.Null(sorted[0]);
        Assert.Equal(new TimeOnly(0, 15), sorted[1]);
        Assert.Equal(new TimeOnly(8, 30), sorted[2]);
        Assert.Equal(new TimeSpan(12, 0, 0), sorted[3]);
        Assert.Equal(new TimeSpan(23, 59, 0), sorted[4]);
    }

    [Fact]
    public void ATimeOnlyAndATimeSpanOfTheSameTime_CompareEqual()
    {
        var comparer = new KumikoComparer(En);

        Assert.Equal(0, comparer.Compare(new TimeOnly(9, 30), new TimeSpan(9, 30, 0)));
        Assert.True(comparer.Compare(new TimeOnly(9, 30), new TimeSpan(9, 31, 0)) < 0);
        Assert.True(comparer.Compare(new TimeSpan(1, 0, 0, 0), new TimeOnly(23, 59)) > 0);
    }

    [Fact]
    public void TheSource_SortsATimeColumn_EarliestFirst()
    {
        var column = Col("Start", KumikoColumnType.Time);
        var source = new KumikoSource { Culture = En };
        source.SetColumns(new[] { column });
        source.SetItems(new[]
        {
            new Row { Name = "c", Start = new TimeOnly(17, 0) },
            new Row { Name = "a", Start = new TimeOnly(6, 45) },
            new Row { Name = "b", Start = new TimeOnly(12, 0) },
        });

        source.SetSort(column, column.FirstSortDirection);

        Assert.Equal(new[] { "a", "b", "c" }, Enumerable.Range(0, source.RowCount).Select(r => ((Row)source.GetItem(r)).Name));
    }

    // ── Filtering ─────────────────────────────────────────────────

    private static FilterCondition Cond(DateFilterOperator op, object value) => new() { DateOperator = op, Value = value };

    [Theory]
    [InlineData(DateFilterOperator.Equals, 9, 30, true)]
    [InlineData(DateFilterOperator.Equals, 9, 31, false)]
    [InlineData(DateFilterOperator.NotEquals, 9, 31, true)]
    [InlineData(DateFilterOperator.NotEquals, 9, 30, false)]
    [InlineData(DateFilterOperator.Before, 9, 31, true)]
    [InlineData(DateFilterOperator.Before, 9, 30, false)]
    [InlineData(DateFilterOperator.After, 9, 29, true)]
    [InlineData(DateFilterOperator.After, 9, 30, false)]
    [InlineData(DateFilterOperator.BeforeOrEqual, 9, 30, true)]
    [InlineData(DateFilterOperator.BeforeOrEqual, 9, 29, false)]
    [InlineData(DateFilterOperator.AfterOrEqual, 9, 30, true)]
    [InlineData(DateFilterOperator.AfterOrEqual, 9, 31, false)]
    public void TimeFilter_EvaluatesEachOperator_ForTimeOnlyAndTimeSpanCells(DateFilterOperator op, int hour, int minute, bool expected)
    {
        var condition = Cond(op, new TimeOnly(hour, minute));

        // Seconds are ignored: 9:30:45 is 9:30
        Assert.Equal(expected, condition.Evaluate(new TimeOnly(9, 30, 45), KumikoColumnType.Time));
        Assert.Equal(expected, condition.Evaluate(new TimeSpan(9, 30, 45), KumikoColumnType.Time));
    }

    [Fact]
    public void TimeFilter_ComparesTheTimeOfADateTimeCell_IgnoringTheDate()
    {
        var condition = Cond(DateFilterOperator.AfterOrEqual, new TimeOnly(9, 0));

        Assert.True(condition.Evaluate(new DateTime(1999, 1, 1, 9, 0, 0), KumikoColumnType.Time));
        Assert.True(condition.Evaluate(new DateTimeOffset(2030, 6, 1, 23, 0, 0, TimeSpan.FromHours(5)), KumikoColumnType.Time));
        Assert.False(condition.Evaluate(new DateTime(2030, 6, 1, 8, 59, 0), KumikoColumnType.Time));
    }

    [Fact]
    public void TimeFilter_AcceptsATimeSpanOrADateTimeAsTheValue()
    {
        Assert.True(Cond(DateFilterOperator.Equals, new TimeSpan(7, 15, 0)).Evaluate(new TimeOnly(7, 15), KumikoColumnType.Time));
        Assert.True(Cond(DateFilterOperator.Equals, new DateTime(2024, 1, 1, 7, 15, 0)).Evaluate(new TimeOnly(7, 15), KumikoColumnType.Time));
    }

    [Fact]
    public void TimeFilter_ReadsTextInTheCulture()
    {
        var comparer = new KumikoComparer(De);
        var condition = Cond(DateFilterOperator.Equals, "14:05");

        Assert.True(condition.Evaluate("14:05", KumikoColumnType.Time, comparer));
        Assert.True(condition.Evaluate(new TimeOnly(14, 5), KumikoColumnType.Time, comparer));
    }

    [Fact]
    public void TimeFilter_RejectsWhatIsNotATime()
    {
        var condition = Cond(DateFilterOperator.NotEquals, new TimeOnly(9, 0));

        Assert.False(condition.Evaluate(null, KumikoColumnType.Time));
        Assert.False(condition.Evaluate("soon", KumikoColumnType.Time));
        Assert.False(condition.Evaluate(new DateOnly(2024, 1, 1), KumikoColumnType.Time));
        Assert.False(Cond(DateFilterOperator.Equals, "later").Evaluate(new TimeOnly(9, 0), KumikoColumnType.Time));
    }

    [Theory]
    [InlineData(DateFilterOperator.Equals, 14, 30, true)]
    [InlineData(DateFilterOperator.Equals, 14, 31, false)]
    [InlineData(DateFilterOperator.Before, 14, 31, true)]
    [InlineData(DateFilterOperator.After, 14, 29, true)]
    [InlineData(DateFilterOperator.After, 14, 30, false)]
    [InlineData(DateFilterOperator.BeforeOrEqual, 14, 30, true)]
    [InlineData(DateFilterOperator.AfterOrEqual, 14, 31, false)]
    [InlineData(DateFilterOperator.NotEquals, 14, 30, false)]
    public void DateTimeFilter_ComparesTheFullValueToTheMinute(DateFilterOperator op, int hour, int minute, bool expected)
    {
        var condition = Cond(op, new DateTime(2024, 3, 5, hour, minute, 10));

        Assert.Equal(expected, condition.Evaluate(new DateTime(2024, 3, 5, 14, 30, 55), KumikoColumnType.DateTime));
    }

    [Fact]
    public void DateTimeFilter_ADifferentDayWithTheSameTime_IsDifferent_UnlikeADateColumn()
    {
        var condition = Cond(DateFilterOperator.Equals, new DateTime(2024, 3, 5, 14, 30, 0));
        var otherDay = new DateTime(2024, 3, 6, 14, 30, 0);

        Assert.False(condition.Evaluate(otherDay, KumikoColumnType.DateTime));
        Assert.False(Cond(DateFilterOperator.Equals, new DateTime(2024, 3, 6, 9, 0, 0)).Evaluate(new DateTime(2024, 3, 5), KumikoColumnType.Date));
        Assert.True(Cond(DateFilterOperator.Equals, new DateTime(2024, 3, 5, 9, 0, 0)).Evaluate(new DateTime(2024, 3, 5, 18, 0, 0), KumikoColumnType.Date));
    }

    [Fact]
    public void DateTimeFilter_AcceptsDateTimeOffsetAndDateOnlyCells()
    {
        Assert.True(Cond(DateFilterOperator.Equals, new DateTime(2024, 3, 5, 8, 15, 0))
            .Evaluate(new DateTimeOffset(2024, 3, 5, 8, 15, 20, TimeSpan.FromHours(2)), KumikoColumnType.DateTime));
        Assert.True(Cond(DateFilterOperator.Equals, new DateTime(2024, 3, 5))
            .Evaluate(new DateOnly(2024, 3, 5), KumikoColumnType.DateTime));
    }

    [Fact]
    public void TheSource_FiltersATimeColumn()
    {
        var column = Col("Start", KumikoColumnType.Time);
        var source = new KumikoSource { Culture = En };
        source.SetColumns(new[] { column });
        source.SetItems(new[]
        {
            new Row { Name = "early", Start = new TimeOnly(6, 0) },
            new Row { Name = "noon", Start = new TimeOnly(12, 0) },
            new Row { Name = "late", Start = new TimeOnly(22, 0) },
        });

        source.SetColumnFilter(column, new FilterDescription(column)
        {
            Condition1 = Cond(DateFilterOperator.AfterOrEqual, new TimeOnly(12, 0)),
        });

        Assert.Equal(new[] { "noon", "late" }, Enumerable.Range(0, source.RowCount).Select(r => ((Row)source.GetItem(r)).Name));
    }

    // ── Layout ────────────────────────────────────────────────────

    private static KumikoSource LayoutSource(object? value, DateFilterOperator op)
    {
        var column = Col("Start", KumikoColumnType.Time);
        var source = new KumikoSource { Culture = En };
        source.SetColumns(new[] { column });
        source.SetItems(new[] { new Row { Name = "a", Start = new TimeOnly(9, 0) } });
        source.SetColumnFilter(column, new FilterDescription(column) { Condition1 = Cond(op, value!) });
        return source;
    }

    [Fact]
    public void ATimeCondition_RoundTripsThroughTheSavedLayout()
    {
        var original = LayoutSource(new TimeOnly(9, 30, 15, 500), DateFilterOperator.AfterOrEqual);

        var state = KumikoLayoutState.Capture(original);
        var saved = state.Columns!.Single().Filter!.Condition1!;
        Assert.Equal(KumikoFilterValueKind.Time, saved.ValueKind);
        Assert.Equal(DateFilterOperator.AfterOrEqual, saved.DateOperator);

        var restored = LayoutSource(new TimeOnly(1, 0), DateFilterOperator.Equals);
        restored.SetColumnFilter(restored.Columns[0], null);
        KumikoLayoutState.FromJson(state.ToJson()).ApplyTo(restored);

        var condition = restored.Columns[0].ActiveFilter!.Condition1!;
        Assert.Equal(new TimeOnly(9, 30, 15, 500), Assert.IsType<TimeOnly>(condition.Value));
        Assert.Equal(DateFilterOperator.AfterOrEqual, condition.DateOperator);
    }

    [Fact]
    public void ATimeSpanCondition_RestoresAsATimeOnly_WithinADay_AndAsATimeSpanBeyondIt()
    {
        var inDay = KumikoLayoutState.Capture(LayoutSource(new TimeSpan(7, 45, 0), DateFilterOperator.Before));
        var beyond = KumikoLayoutState.Capture(LayoutSource(new TimeSpan(1, 2, 0, 0), DateFilterOperator.Before));

        var a = LayoutSource(new TimeOnly(1, 0), DateFilterOperator.Equals);
        var b = LayoutSource(new TimeOnly(1, 0), DateFilterOperator.Equals);
        KumikoLayoutState.FromJson(inDay.ToJson()).ApplyTo(a);
        KumikoLayoutState.FromJson(beyond.ToJson()).ApplyTo(b);

        Assert.Equal(new TimeOnly(7, 45), a.Columns[0].ActiveFilter!.Condition1!.Value);
        Assert.Equal(new TimeSpan(1, 2, 0, 0), b.Columns[0].ActiveFilter!.Condition1!.Value);
    }

    // ── Column properties ─────────────────────────────────────────

    [Fact]
    public void TimeMinuteStep_IsLimitedToOneToThirty()
    {
        var column = new KumikoColumn { TimeMinuteStep = 0 };
        Assert.Equal(1, column.TimeMinuteStep);
        column.TimeMinuteStep = 45;
        Assert.Equal(30, column.TimeMinuteStep);
        column.TimeMinuteStep = 15;
        Assert.Equal(15, column.TimeMinuteStep);
    }

    [Theory]
    [InlineData(null, "en-US", false)]
    [InlineData(null, "de-DE", true)]
    [InlineData(true, "en-US", true)]
    [InlineData(false, "de-DE", false)]
    public void Use24HourTime_FollowsTheCulture_UnlessSet(bool? setting, string culture, bool expected)
    {
        var column = new KumikoColumn { Use24HourTime = setting };

        Assert.Equal(expected, column.UsesTwentyFourHourTime(CultureInfo.GetCultureInfo(culture)));
    }
}

using System.Globalization;
using System.Reflection;
using KumikoUI.Core.Components;
using KumikoUI.Core.Data;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests;

/// <summary>
/// Localization (#47): <see cref="KumikoStrings"/> replaces every piece of text the grid draws or reports
/// on its own, and the date picker and summaries follow the grid's culture.
/// </summary>
public class LocalizationTests
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>A table in which every string differs from <see cref="KumikoStrings.Default"/>.</summary>
    private static readonly KumikoStrings GermanStrings = new()
    {
        GroupPanelLabel = "Gruppiert nach:",
        BlankGroupText = "(Leer)",
        SummarySum = "Summe: ",
        SummaryAverage = "Mittel: ",
        SummaryCount = "Anzahl: ",
        SummaryMin = "Kleinster: ",
        SummaryMax = "Größter: ",
        FilterBlanks = "(Leere)",
        FilterSearchPlaceholder = "Suchen…",
        FilterSelectAll = "Alle auswählen",
        FilterDeselectAll = "Auswahl aufheben",
        FilterOk = "Übernehmen",
        FilterCancel = "Abbrechen",
        FilterClear = "Zurücksetzen",
        SortTextAscending = "A bis Z sortieren",
        SortTextDescending = "Z bis A sortieren",
        SortNumericAscending = "Aufsteigend sortieren",
        SortNumericDescending = "Absteigend sortieren",
        SortDateAscending = "Älteste zuerst",
        SortDateDescending = "Neueste zuerst",
        SortBooleanCheckedFirst = "Angehakte zuerst",
        SortBooleanUncheckedFirst = "Nicht angehakte zuerst",
        ValueRequired = "Ein Wert ist erforderlich.",
        EnterNumber = "Geben Sie eine Zahl ein.",
        EnterWholeNumber = "Geben Sie eine ganze Zahl ein.",
        NumberOutOfRange = "Die Zahl liegt außerhalb des Bereichs.",
        EnterBoolean = "Geben Sie wahr oder falsch ein.",
        EnterCharacter = "Geben Sie ein einzelnes Zeichen ein.",
        EnterDate = "Geben Sie ein gültiges Datum ein.",
        EnterTime = "Geben Sie eine gültige Uhrzeit ein.",
        EnterDuration = "Geben Sie eine gültige Dauer ein.",
        EnterIdentifier = "Geben Sie eine gültige Kennung ein.",
        EnterOneOf = "Geben Sie einen dieser Werte ein: {0}.",
        EnterValidValue = "Geben Sie einen gültigen Wert ein.",
        ValueCouldNotBeSaved = "Der Wert konnte nicht gespeichert werden.",
        AccessibleGridName = "Datentabelle",
        AccessibleGridHint = "{0} Zeilen, {1} Spalten",
        AccessibleGridHintWithCell = "{0} Zeilen, {1} Spalten, aktuelle Zelle Zeile {2} Spalte {3}",
        AnnounceCurrentCell = "{0}: {1}, Zeile {2}, Spalte {3}",
        AnnounceEmptyValue = "leer",
        AnnounceUnnamedColumn = "Spalte {0}",
        AnnounceSortedAscending = "Sortiert nach {0}, aufsteigend",
        AnnounceSortedDescending = "Sortiert nach {0}, absteigend",
        AnnounceSortRemoved = "Sortierung von {0} entfernt",
        AnnounceEditStarted = "{0} wird bearbeitet: {1}",
        AnnounceEditCommitted = "{0} geändert in {1}",
        AnnounceEditCanceled = "Bearbeitung von {0} beendet",
        AnnounceValidationFailed = "{0} ist ungültig: {1}",
    };

    private static IEnumerable<PropertyInfo> StringProperties =>
        typeof(KumikoStrings).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(string));

    private static IEnumerable<string> EnglishDefaults =>
        StringProperties.Select(p => (string)p.GetValue(KumikoStrings.Default)!);

    private static void AssertNoEnglish(IEnumerable<string> drawn)
    {
        foreach (var text in drawn)
            foreach (var english in EnglishDefaults.Select(e => e.Trim()).Where(e => e.Length > 0 && !e.Contains("{0}")))
                Assert.False(text.Contains(english, StringComparison.Ordinal), $"\"{text}\" still contains \"{english}\"");
    }

    private sealed class Event
    {
        public string? Title { get; set; }
        public int Count { get; set; }
        public DateTime When { get; set; }
    }

    private static (KumikoSource Source, KumikoColumn Title, KumikoColumn Count, KumikoColumn When) EventSource()
    {
        var title = new KumikoColumn { Header = "Titel", PropertyName = "Title", Width = 150 };
        var count = new KumikoColumn { Header = "Menge", PropertyName = "Count", Width = 100, ColumnType = KumikoColumnType.Numeric };
        var when = new KumikoColumn { Header = "Datum", PropertyName = "When", Width = 150, ColumnType = KumikoColumnType.Date };
        var source = new KumikoSource { Culture = German };
        source.SetColumns(new[] { title, count, when });
        source.SetItems(new[]
        {
            new Event { Title = "Alpha", Count = 1, When = new DateTime(2024, 3, 1) },
            new Event { Title = "", Count = 2, When = new DateTime(2024, 3, 2) },
            new Event { Title = null, Count = 3, When = new DateTime(2024, 3, 3) },
        });
        return (source, title, count, when);
    }

    // ── The table ─────────────────────────────────────────────────

    [Fact]
    public void TheGermanTable_ChangesEveryProperty()
    {
        var props = StringProperties.ToList();
        Assert.NotEmpty(props);
        foreach (var p in props)
        {
            Assert.NotEqual(p.GetValue(KumikoStrings.Default), p.GetValue(GermanStrings));
            Assert.False(string.IsNullOrEmpty((string?)p.GetValue(KumikoStrings.Default)), p.Name);
        }
    }

    [Fact]
    public void Strings_DefaultsToTheEnglishTable_AndNullRestoresIt()
    {
        var source = new KumikoSource();
        Assert.Same(KumikoStrings.Default, source.Strings);

        source.Strings = GermanStrings;
        Assert.Same(GermanStrings, source.Strings);

        source.Strings = null!;
        Assert.Same(KumikoStrings.Default, source.Strings);
    }

    // ── Acceptance: nothing English is left ───────────────────────

    [Fact]
    public void WithAGermanTable_NoEnglishIsDrawn()
    {
        var drawn = new List<string>();

        // The grid: group panel, a group whose key is null, group headers and summaries
        var h = RenderHarness.Create(rows: 6, configure: x =>
        {
            x.People[0].City = null!;
            x.Source.Strings = GermanStrings;
            x.Source.Culture = German;
            x.Source.AddGroupDescription(new GroupDescription("City"));
            x.Source.AddTableSummaryRow(new TableSummaryRow("Totals", SummaryPosition.Bottom)
            {
                Columns =
                {
                    new SummaryColumnDescription("Salary", SummaryType.Sum) { Format = "N1" },
                    new SummaryColumnDescription("Name", SummaryType.Count),
                    new SummaryColumnDescription("Age", SummaryType.Average),
                },
            });
            x.Source.AddTableSummaryRow(new TableSummaryRow("Extremes", SummaryPosition.Top)
            {
                Columns =
                {
                    new SummaryColumnDescription("Salary", SummaryType.Min),
                    new SummaryColumnDescription("Age", SummaryType.Max),
                },
            });
        });
        var grid = h.Render().Texts.Select(t => t.Text!).ToList();
        Assert.Contains("Gruppiert nach:", grid);
        Assert.Contains(grid, t => t.Contains("(Leer)"));
        Assert.Contains(grid, t => t.StartsWith("Summe: "));
        Assert.Contains(grid, t => t.StartsWith("Kleinster: "));
        drawn.AddRange(grid);

        // The filter popup of a text, a numeric and a date column
        var (source, title, count, when) = EventSource();
        source.Strings = GermanStrings;
        foreach (var popup in new[] { title, count, when }.Select(column => new DrawnFilterPopup(column, source)))
        {
            new PopupManager().Show(popup, new KumikoRect(0, 0, 150, 0));
            var ctx = new RecordingDrawingContext();
            popup.OnDraw(ctx);
            drawn.AddRange(ctx.Texts.Select(t => t.Text!));
        }

        // The date editor, with its time row
        var picker = (DrawnDatePicker)CellEditorFactory.CreateEditor(when, new DateTime(2024, 3, 15, 9, 30, 0),
            new KumikoRect(0, 0, 200, 30), culture: German)!;
        picker.ShowTimePicker = true;
        picker.OpenCalendar();
        var pickerCtx = new RecordingDrawingContext();
        picker.OnDraw(pickerCtx);
        drawn.AddRange(pickerCtx.Texts.Select(t => t.Text!));

        // A conversion failure and an enum's list of names
        var session = new EditSession();
        Assert.True(session.BeginEdit(0, 1, count, source, new KumikoRect(0, 30, 120, 30)));
        ((DrawnTextBox)session.ActiveEditor!).Text = "viele";
        Assert.False(session.CommitEdit(source));
        drawn.Add(session.CurrentValidation!.ErrorMessage!);
        Assert.False(KumikoValueConverter.TryConvert("x", typeof(DayOfWeek), German, source.Strings, out _, out var enumError));
        drawn.Add(enumError!);

        Assert.Equal(GermanStrings.EnterNumber, drawn.Single(t => t == GermanStrings.EnterNumber));
        AssertNoEnglish(drawn);
    }

    // ── Filter popup sort labels ──────────────────────────────────

    private static List<string> PopupTexts(KumikoColumn column, KumikoSource source)
    {
        var popup = new DrawnFilterPopup(column, source);
        new PopupManager().Show(popup, new KumikoRect(0, 0, 150, 0));
        var ctx = new RecordingDrawingContext();
        popup.OnDraw(ctx);
        return ctx.Texts.Select(t => t.Text!).ToList();
    }

    [Fact]
    public void SortLabels_FollowTheColumnType()
    {
        var (source, title, count, when) = EventSource();

        var text = PopupTexts(title, source);
        Assert.Contains("Sort A to Z", text);
        Assert.Contains("Sort Z to A", text);

        var numeric = PopupTexts(count, source);
        Assert.Contains("Sort Smallest to Largest", numeric);
        Assert.Contains("Sort Largest to Smallest", numeric);

        var date = PopupTexts(when, source);
        Assert.Contains("Sort Oldest to Newest", date);
        Assert.Contains("Sort Newest to Oldest", date);
    }

    [Fact]
    public void FilterPopup_UsesTheSourcesStringsForItsOtherText()
    {
        var (source, title, _, _) = EventSource();
        source.Strings = KumikoStrings.Default with { FilterOk = "Apply", FilterBlanks = "(Empty)", FilterSearchPlaceholder = "Find" };

        var text = PopupTexts(title, source);

        Assert.Contains("Apply", text);
        Assert.Contains("(Empty)", text);
        Assert.Contains("Find", text);
        Assert.DoesNotContain("OK", text);
    }

    // ── Groups and summaries ──────────────────────────────────────

    [Fact]
    public void ChangingStrings_RelabelsTheBlankGroup()
    {
        var (source, _, _, _) = EventSource();
        source.AddGroupDescription(new GroupDescription("Title"));
        Assert.Contains(Headers(source), t => t == "(Blank)");

        source.Strings = KumikoStrings.Default with { BlankGroupText = "(Vide)" };

        Assert.Contains(Headers(source), t => t == "(Vide)");
        Assert.DoesNotContain(Headers(source), t => t == "(Blank)");
    }

    private static List<string> Headers(KumikoSource source) =>
        Enumerable.Range(0, source.RowCount).Select(source.GetGroupHeaderInfo)
            .Where(g => g != null).Select(g => g!.DisplayText).ToList();

    [Fact]
    public void SummaryValues_AreFormattedInTheSourcesCulture()
    {
        var source = new KumikoSource { Culture = German };
        source.SetColumns(new[] { new KumikoColumn { Header = "Value", PropertyName = "Value", ColumnType = KumikoColumnType.Numeric } });
        source.SetItems(new[] { new Amount(1000m), new Amount(234.5m) });
        source.AddTableSummaryRow(new TableSummaryRow("T", SummaryPosition.Bottom)
        {
            Columns = { new SummaryColumnDescription("Value", SummaryType.Sum) { Format = "N1" } },
        });

        var row = Assert.Single(source.ComputedBottomSummaries);

        Assert.Equal("Sum: 1.234,5", row.Values["Value"]);

        var plain = new KumikoSource { Culture = German };
        plain.SetColumns(new[] { new KumikoColumn { Header = "Value", PropertyName = "Value" } });
        plain.SetItems(new[] { new Amount(1000m), new Amount(234.5m) });
        plain.AddTableSummaryRow(new TableSummaryRow("T", SummaryPosition.Bottom)
        {
            Columns = { new SummaryColumnDescription("Value", SummaryType.Sum) { Format = "Total {0}" } },
        });
        Assert.Equal("Sum: Total 1234,5", Assert.Single(plain.ComputedBottomSummaries).Values["Value"]);

        var composite = new KumikoSource { Culture = German };
        composite.SetColumns(new[] { new KumikoColumn { Header = "Value", PropertyName = "Value" } });
        composite.SetItems(new[] { new Amount(1000m), new Amount(234.5m) });
        composite.AddTableSummaryRow(new TableSummaryRow("T", SummaryPosition.Bottom)
        {
            Columns = { new SummaryColumnDescription("Value", SummaryType.Sum) { Format = "Total {0:N1}" } },
        });
        Assert.Equal("Sum: Total 1.234,5", Assert.Single(composite.ComputedBottomSummaries).Values["Value"]);
    }

    private sealed class Amount
    {
        public Amount(decimal value) => Value = value;
        public decimal Value { get; }
    }

    [Fact]
    public void SummaryLabels_FollowTheStrings_AndAnExplicitLabelWins()
    {
        var h = RenderHarness.Create(rows: 4, configure: x =>
        {
            x.Source.Strings = GermanStrings;
            x.Source.AddTableSummaryRow(new TableSummaryRow("T", SummaryPosition.Bottom)
            {
                Columns =
                {
                    new SummaryColumnDescription("Salary", SummaryType.Sum),
                    new SummaryColumnDescription("Age", SummaryType.Min) { Label = "Jüngster: " },
                    new SummaryColumnDescription("Name", SummaryType.Count),
                },
            });
        });

        var row = Assert.Single(h.Source.ComputedBottomSummaries);

        Assert.StartsWith("Summe: ", row.Values["Salary"]);
        Assert.StartsWith("Jüngster: ", row.Values["Age"]);
        Assert.StartsWith("Anzahl: ", row.Values["Name"]);
    }

    // ── The group panel ───────────────────────────────────────────

    [Fact]
    public void GroupPanel_DrawsTheConfiguredLabel_AndChipsStartAfterIt()
    {
        var h = RenderHarness.Create();
        h.Source.AddGroupDescription(new GroupDescription("City"));
        h.Source.Strings = KumikoStrings.Default with { GroupPanelLabel = "Gruppiert nach den folgenden Spalten:" };

        var ctx = h.Render();

        var label = ctx.SingleText("Gruppiert nach den folgenden Spalten:");
        Assert.Equal("Gruppiert nach den folgenden Spalten:".Length * RecordingDrawingContext.CharWidth, label.Rect.Width);
        var chip = ctx.Texts.Single(t => t.Text == "City" && t.Color == h.Style.GroupPanelChipTextColor);
        Assert.True(chip.Rect.X > label.Rect.Right);
    }

    // ── Conversion messages ───────────────────────────────────────

    [Fact]
    public void TheConverterOverload_ReportsTheSuppliedStrings()
    {
        Assert.False(KumikoValueConverter.TryConvert("x", typeof(int), German, GermanStrings, out _, out var error));
        Assert.Equal(GermanStrings.EnterNumber, error);

        Assert.False(KumikoValueConverter.TryConvert("", typeof(int), German, GermanStrings, out _, out error));
        Assert.Equal(GermanStrings.ValueRequired, error);

        Assert.False(KumikoValueConverter.TryConvert("Nope", typeof(DayOfWeek), German, GermanStrings, out _, out error));
        Assert.StartsWith("Geben Sie einen dieser Werte ein: Sunday", error);

        Assert.False(KumikoValueConverter.TryConvert("x", typeof(int), German, out _, out error));
        Assert.Equal(KumikoStrings.Default.EnterNumber, error);
    }

    [Fact]
    public void AnEditsFailure_ReportsTheSourcesStrings()
    {
        var (source, _, count, _) = EventSource();
        source.Strings = GermanStrings;
        var session = new EditSession();
        Assert.True(session.BeginEdit(0, 1, count, source, new KumikoRect(0, 30, 120, 30)));
        ((DrawnTextBox)session.ActiveEditor!).Text = "1,5";

        Assert.False(session.CommitEdit(source));

        Assert.Equal(GermanStrings.EnterWholeNumber, session.CurrentValidation!.ErrorMessage);
    }

    // ── Date picker ───────────────────────────────────────────────

    private static List<string> Draw(DrawnDatePicker picker)
    {
        var ctx = new RecordingDrawingContext();
        picker.OnDraw(ctx);
        return ctx.Texts.Select(t => t.Text!).ToList();
    }

    private static DrawnDatePicker Picker(CultureInfo culture)
    {
        var picker = new DrawnDatePicker
        {
            Bounds = new KumikoRect(0, 0, 200, 30),
            Culture = culture,
            DateFormat = "yyyy-MM-dd",
            SelectedDate = new DateTime(2024, 3, 15),
            ShowTimePicker = true,
            SelectedHour = 15,
            SelectedMinute = 5,
        };
        picker.OpenCalendar();
        return picker;
    }

    [Fact]
    public void DatePicker_DrawsTheCulturesDayAndMonthNames()
    {
        var texts = Draw(Picker(German));

        foreach (var day in German.DateTimeFormat.ShortestDayNames)
            Assert.Contains(day, texts);
        Assert.Contains(German.DateTimeFormat.GetMonthName(3), texts);
        Assert.DoesNotContain("Su", texts);
        Assert.DoesNotContain("March", texts);
    }

    [Fact]
    public void DatePickerMonthDropdown_ListsTheCulturesMonths()
    {
        var picker = Picker(German);
        picker.OnPointerDown(new KumikoPointerEventArgs { X = 80, Y = 50, Action = InputAction.Pressed }); // the month label
        var texts = Draw(picker);

        for (int m = 1; m <= 12; m++)
            Assert.Contains(German.DateTimeFormat.GetMonthName(m), texts);
        Assert.DoesNotContain("January", texts);
    }

    [Fact]
    public void DatePicker_UsesTheCulturesAmPmDesignators_AndOmitsEmptyOnes()
    {
        var us = CultureInfo.GetCultureInfo("en-US");
        var texts = Draw(Picker(us));
        Assert.Contains("03:05 PM", texts);
        Assert.Contains("2024-03-15 03:05 PM", texts);

        var custom = (CultureInfo)us.Clone();
        custom.DateTimeFormat.PMDesignator = "nachm.";
        Assert.Contains("03:05 nachm.", Draw(Picker(custom)));

        var bare = (CultureInfo)us.Clone();
        bare.DateTimeFormat.PMDesignator = "";
        var bareTexts = Draw(Picker(bare));
        Assert.Contains("03:05", bareTexts);
        Assert.Contains("2024-03-15 03:05", bareTexts);
    }

    [Fact]
    public void TheDateEditor_TakesTheGridsCulture()
    {
        var (_, _, _, when) = EventSource();

        var editor = (DrawnDatePicker)CellEditorFactory.CreateEditor(when, new DateTime(2024, 3, 15),
            new KumikoRect(0, 0, 200, 30), culture: German)!;

        Assert.Same(German, editor.Culture);
    }
}

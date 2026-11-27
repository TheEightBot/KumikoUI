using System.Globalization;
using KumikoUI.Core.Components;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.Components;

/// <summary>
/// The filter popup's condition filters (#325): the Values | Conditions switch, the operators of each column type,
/// typed values parsed in the source's culture, the second condition with And/Or, Is empty, invalid input, Clear, the
/// tab markers, and a layout round trip of conditions set through the popup.
/// </summary>
public class DrawnFilterPopupConditionsTests
{
    private sealed class Row
    {
        public string? Name { get; set; }
        public double Amount { get; set; }
        public DateTime When { get; set; }
        public bool Done { get; set; }
    }

    // A popup shown at (0, 0): the switch is the 26 px row at y 43 (Values x 6–108, Conditions x 112–214); on the
    // Conditions page the first operator is at y 73, its value at y 105, And/Or at y 139, the second operator at
    // y 169 and its value at y 201; the OK/Cancel/Clear row is at y 257.
    private const float SwitchY = 56, ValuesTabX = 50, ConditionsTabX = 160;
    private const float Operator1Y = 87, Value1Y = 119, CombinationY = 151, Operator2Y = 183, Value2Y = 215;
    private const float AndX = 50, OrX = 160, ButtonsY = 270, OkX = 16, ClearX = 157;

    private static readonly DateTime Day = new(2024, 3, 15);

    private sealed class Setup
    {
        public required KumikoSource Source { get; init; }
        public required KumikoColumn Name { get; init; }
        public required KumikoColumn Amount { get; init; }
        public required KumikoColumn When { get; init; }
        public required KumikoColumn Done { get; init; }
    }

    private static Setup Create(CultureInfo? culture = null)
    {
        var source = new KumikoSource { Culture = culture ?? CultureInfo.InvariantCulture };
        var name = new KumikoColumn { Header = "Name", PropertyName = "Name", Width = 150 };
        var amount = new KumikoColumn { Header = "Amount", PropertyName = "Amount", Width = 150, ColumnType = KumikoColumnType.Numeric };
        var when = new KumikoColumn { Header = "When", PropertyName = "When", Width = 150, ColumnType = KumikoColumnType.Date };
        var done = new KumikoColumn { Header = "Done", PropertyName = "Done", Width = 80, ColumnType = KumikoColumnType.Boolean };
        source.SetColumns(new[] { name, amount, when, done });
        source.SetItems(new List<Row>
        {
            new() { Name = "Alpha", Amount = 5, When = Day.AddDays(-2) },
            new() { Name = "Beta", Amount = 1_234.5, When = Day },
            new() { Name = "", Amount = 20, When = Day.AddDays(3), Done = true },
            new() { Name = "Gamma", Amount = 2_000, When = Day.AddDays(10) },
        });
        return new Setup { Source = source, Name = name, Amount = amount, When = when, Done = done };
    }

    private static (DrawnFilterPopup Popup, PopupManager Manager) Open(KumikoSource source, KumikoColumn column)
    {
        var popup = new DrawnFilterPopup(column, source);
        var manager = new PopupManager();
        manager.Show(popup, new KumikoRect(0, 0, 150, 0));
        var focus = new DrawnFocusManager();
        focus.Register(popup);
        focus.SetFocus(popup);
        popup.OnDraw(new RecordingDrawingContext()); // places the editors, as the first frame does
        return (popup, manager);
    }

    private static void Tap(PopupManager manager, float x, float y)
    {
        manager.HandlePointer(new KumikoPointerEventArgs { X = x, Y = y, Action = InputAction.Pressed });
        manager.HandlePointer(new KumikoPointerEventArgs { X = x, Y = y, Action = InputAction.Released });
    }

    private static void Key(DrawnFilterPopup popup, KumikoKey key, char? character = null, InputModifiers modifiers = InputModifiers.None) =>
        popup.OnKeyDown(new KumikoKeyEventArgs { Key = key, Character = character, Modifiers = modifiers });

    private static void Type(DrawnFilterPopup popup, string text)
    {
        foreach (char c in text) Key(popup, KumikoKey.Character, c);
    }

    private static List<string> OperatorLabels(DrawnFilterPopup popup) =>
        popup.OperatorBox(0).Items.Select(i => i.DisplayText).ToList();

    private static List<string?> VisibleNames(KumikoSource source, KumikoColumn name) =>
        Enumerable.Range(0, source.RowCount).Select(r => source.GetCellDisplayText(r, name)).ToList()!;

    // ── The mode switch ───────────────────────────────────────────

    [Fact]
    public void Mode_OpensOnValues_AndTheTabsSwitchThePage()
    {
        var s = Create();
        var (popup, manager) = Open(s.Source, s.Amount);
        float valuesHeight = popup.Bounds.Height;
        Assert.Equal(KumikoFilterPopupMode.Values, popup.Mode);

        Tap(manager, ConditionsTabX, SwitchY);
        Assert.Equal(KumikoFilterPopupMode.Conditions, popup.Mode);
        Assert.Equal(DrawnFilterPopup.FocusStop.Operator1, popup.CurrentStop);
        Assert.NotEqual(valuesHeight, popup.Bounds.Height);
        var texts = Draw(popup);
        Assert.Contains("Equals", texts);
        Assert.DoesNotContain("Select All", texts);

        Tap(manager, ValuesTabX, SwitchY);
        Assert.Equal(KumikoFilterPopupMode.Values, popup.Mode);
        Assert.Equal(valuesHeight, popup.Bounds.Height);
        Assert.Contains("Select All", Draw(popup));
    }

    [Fact]
    public void Mode_OpensOnConditions_WhenTheFilterHasOne()
    {
        var s = Create();
        s.Source.SetColumnFilter(s.Amount, new FilterDescription(s.Amount)
        {
            Condition1 = new FilterCondition { NumericOperator = NumericFilterOperator.GreaterThan, Value = 10.0 },
        });

        var (popup, _) = Open(s.Source, s.Amount);

        Assert.Equal(KumikoFilterPopupMode.Conditions, popup.Mode);
        Assert.Equal("Greater than", popup.OperatorBox(0).SelectedText);
        Assert.Equal("10", popup.ValueTextBox(0).Text);
    }

    [Fact]
    public void BooleanColumns_AndColumnsThatTurnConditionsOff_ShowOnlyValues()
    {
        var s = Create();
        s.Name.AllowConditionFilter = false;

        foreach (var column in new[] { s.Done, s.Name })
        {
            var (popup, _) = Open(s.Source, column);
            popup.Mode = KumikoFilterPopupMode.Conditions;

            Assert.Equal(KumikoFilterPopupMode.Values, popup.Mode);
            Assert.DoesNotContain("Conditions", Draw(popup));
        }
    }

    [Theory]
    [InlineData(KumikoColumnType.Text, "Text")]
    [InlineData(KumikoColumnType.ComboBox, "Text")]
    [InlineData(KumikoColumnType.Picker, "Text")]
    [InlineData(KumikoColumnType.Numeric, "Number")]
    [InlineData(KumikoColumnType.ProgressBar, "Number")]
    [InlineData(KumikoColumnType.Date, "Date")]
    [InlineData(KumikoColumnType.Boolean, "None")]
    [InlineData(KumikoColumnType.Image, "None")]
    [InlineData(KumikoColumnType.Template, "None")]
    public void EachColumnType_GetsItsConditionEditor(KumikoColumnType type, string kind) =>
        Assert.Equal(kind, FilterConditionTypes.KindOf(type).ToString());

    [Fact]
    public void OperatorLists_FollowTheColumnType()
    {
        var s = Create();

        Assert.Equal(
            new[] { "Contains", "Does not contain", "Equals", "Does not equal", "Begins with", "Ends with", "Is empty", "Is not empty" },
            OperatorLabels(Open(s.Source, s.Name).Popup));
        Assert.Equal(
            new[] { "Equals", "Does not equal", "Greater than", "Greater than or equal to", "Less than", "Less than or equal to" },
            OperatorLabels(Open(s.Source, s.Amount).Popup));
        Assert.Equal(
            new[] { "On", "Not on", "Before", "After", "On or before", "On or after" },
            OperatorLabels(Open(s.Source, s.When).Popup));
    }

    [Fact]
    public void OperatorLabels_ComeFromTheSourcesStrings()
    {
        var s = Create();
        s.Source.Strings = KumikoStrings.Default with { FilterNumberGreaterThan = "Mehr als", FilterConditionsTab = "Regeln" };

        var (popup, _) = Open(s.Source, s.Amount);

        Assert.Contains("Mehr als", OperatorLabels(popup));
        Assert.Contains("Regeln", Draw(popup));
    }

    // ── Typed values ──────────────────────────────────────────────

    [Theory]
    [InlineData("en-US", "1,234.5")]
    [InlineData("de-DE", "1.234,5")]
    [InlineData("fr-FR", "1234,5")]
    public void Numbers_AreParsedInTheSourcesCulture(string culture, string typed)
    {
        var s = Create(CultureInfo.GetCultureInfo(culture));
        var (popup, manager) = Open(s.Source, s.Amount);
        Tap(manager, ConditionsTabX, SwitchY);

        Key(popup, KumikoKey.Tab); // to the value
        Type(popup, typed);
        Key(popup, KumikoKey.Enter);

        var condition = s.Amount.ActiveFilter!.Condition1!;
        Assert.Equal(NumericFilterOperator.Equals, condition.NumericOperator);
        Assert.Equal(1_234.5, condition.Value);
        Assert.Equal(new[] { "Beta" }, VisibleNames(s.Source, s.Name));
    }

    [Fact]
    public void ANumber_ShowsInTheSourcesCulture_WhenThePopupOpens()
    {
        var s = Create(CultureInfo.GetCultureInfo("de-DE"));
        s.Source.SetColumnFilter(s.Amount, new FilterDescription(s.Amount) { Condition1 = new FilterCondition { Value = 1_234.5 } });

        var (popup, _) = Open(s.Source, s.Amount);

        Assert.Equal("1234,5", popup.ValueTextBox(0).Text);
    }

    [Fact]
    public void TextConditions_KeepTheTypedText()
    {
        var s = Create();
        var (popup, manager) = Open(s.Source, s.Name);
        Tap(manager, ConditionsTabX, SwitchY);
        Tap(manager, 100, Value1Y);
        Type(popup, "ta");
        Key(popup, KumikoKey.Enter);

        var condition = s.Name.ActiveFilter!.Condition1!;
        Assert.Equal(TextFilterOperator.Contains, condition.TextOperator);
        Assert.Equal("ta", condition.Value);
        Assert.Equal(new[] { "Beta" }, VisibleNames(s.Source, s.Name));
    }

    [Fact]
    public void Dates_ComeFromTheDatePicker_TypedInTheSourcesCulture()
    {
        var s = Create(CultureInfo.GetCultureInfo("de-DE"));
        var (popup, manager) = Open(s.Source, s.When);
        Tap(manager, ConditionsTabX, SwitchY);
        popup.OperatorBox(0).SelectedIndex = 3; // After

        Key(popup, KumikoKey.Tab);
        Assert.Equal(DrawnFilterPopup.FocusStop.Value1, popup.CurrentStop);
        Type(popup, "15.03.2024");
        Key(popup, KumikoKey.Enter);

        var condition = s.When.ActiveFilter!.Condition1!;
        Assert.Equal(DateFilterOperator.After, condition.DateOperator);
        Assert.Equal(Day, condition.Value);
        Assert.Equal(2, s.Source.RowCount);
    }

    // ── Two conditions ────────────────────────────────────────────

    [Theory]
    [InlineData(FilterCombination.And, new[] { "Beta", "" })]
    [InlineData(FilterCombination.Or, new[] { "Alpha", "Beta", "", "Gamma" })]
    public void TheSecondCondition_CombinesWithAndOrOr(FilterCombination combination, string[] expected)
    {
        var s = Create();
        var (popup, manager) = Open(s.Source, s.Amount);
        Tap(manager, ConditionsTabX, SwitchY);
        popup.OperatorBox(0).SelectedIndex = 2; // Greater than
        popup.ValueTextBox(0).Text = "10";
        popup.OperatorBox(1).SelectedIndex = 4; // Less than
        popup.ValueTextBox(1).Text = "1500";
        if (combination == FilterCombination.Or) Tap(manager, OrX, CombinationY);
        else Tap(manager, AndX, CombinationY);

        Tap(manager, OkX, ButtonsY);

        var filter = s.Amount.ActiveFilter!;
        Assert.Equal(NumericFilterOperator.GreaterThan, filter.Condition1!.NumericOperator);
        Assert.Equal(NumericFilterOperator.LessThan, filter.Condition2!.NumericOperator);
        Assert.Equal(1_500.0, filter.Condition2.Value);
        Assert.Equal(combination, filter.Combination);
        Assert.Null(filter.SelectedValues);
        Assert.Equal(expected, VisibleNames(s.Source, s.Name));
    }

    [Fact]
    public void ABlankFirstRow_LeavesTheSecondAsTheOnlyCondition()
    {
        var s = Create();
        var (popup, manager) = Open(s.Source, s.Amount);
        Tap(manager, ConditionsTabX, SwitchY);
        popup.ValueTextBox(1).Text = "20";

        Key(popup, KumikoKey.Enter);

        var filter = s.Amount.ActiveFilter!;
        Assert.Equal(20.0, filter.Condition1!.Value);
        Assert.Null(filter.Condition2);
    }

    [Fact]
    public void BlankConditions_RemoveTheFilter()
    {
        var s = Create();
        s.Source.SetColumnFilter(s.Amount, new FilterDescription(s.Amount) { Condition1 = new FilterCondition { Value = 5.0 } });
        var (popup, _) = Open(s.Source, s.Amount);
        popup.ValueTextBox(0).Text = "";

        Key(popup, KumikoKey.Enter);

        Assert.Null(s.Amount.ActiveFilter);
        Assert.Equal(4, s.Source.RowCount);
    }

    // ── Is empty / Is not empty ───────────────────────────────────

    [Theory]
    [InlineData(6, new[] { "" })]                         // Is empty
    [InlineData(7, new[] { "Alpha", "Beta", "Gamma" })]   // Is not empty
    public void EmptyAndNotEmpty_HideTheValue_AndNeedNone(int operatorIndex, string[] expected)
    {
        var s = Create();
        var (popup, manager) = Open(s.Source, s.Name);
        Tap(manager, ConditionsTabX, SwitchY);
        popup.ValueTextBox(0).Text = "ignored";

        popup.OperatorBox(0).SelectedIndex = operatorIndex;

        Assert.DoesNotContain("ignored", Draw(popup));
        Key(popup, KumikoKey.Tab); // the value is skipped: And/Or is next
        Assert.Equal(DrawnFilterPopup.FocusStop.Combination, popup.CurrentStop);

        Key(popup, KumikoKey.Enter);
        var condition = s.Name.ActiveFilter!.Condition1!;
        Assert.Null(condition.Value);
        Assert.Equal(expected, VisibleNames(s.Source, s.Name));
    }

    // ── Invalid input ─────────────────────────────────────────────

    [Fact]
    public void ATextThatIsNotANumber_KeepsThePopupOpen_WithTheMessage()
    {
        var s = Create();
        var (popup, manager) = Open(s.Source, s.Amount);
        int applied = 0;
        popup.FilterApplied += (_, _) => applied++;
        Tap(manager, ConditionsTabX, SwitchY);
        popup.ValueTextBox(1).Text = "lots";

        Key(popup, KumikoKey.Enter);

        Assert.Equal(0, applied);
        Assert.Null(s.Amount.ActiveFilter);
        Assert.Equal("Enter a number.", popup.ConditionError);
        Assert.Equal(ValidationState.Error, popup.ValueTextBox(1).Validation);
        Assert.Equal(DrawnFilterPopup.FocusStop.Value2, popup.CurrentStop); // the keyboard is on the value to fix
        Assert.Contains("Enter a number.", Draw(popup));

        Key(popup, KumikoKey.Backspace); // editing the value clears the message
        Assert.Null(popup.ConditionError);
        popup.ValueTextBox(1).Text = "7";
        Key(popup, KumikoKey.Enter);
        Assert.Equal(1, applied);
    }

    [Fact]
    public void AnInvalidValueOnTheConditionsPage_ShowsItWhenOkIsPressedOnValues()
    {
        var s = Create();
        var (popup, manager) = Open(s.Source, s.Amount);
        Tap(manager, ConditionsTabX, SwitchY);
        popup.ValueTextBox(0).Text = "x";
        Tap(manager, ValuesTabX, SwitchY);

        Key(popup, KumikoKey.Enter);

        Assert.Equal(KumikoFilterPopupMode.Conditions, popup.Mode);
        Assert.Equal("Enter a number.", popup.ConditionError);
    }

    [Fact]
    public void ATypedTextThatIsNotADate_ShowsEnterDate()
    {
        var s = Create();
        s.Source.Strings = KumikoStrings.Default with { EnterDate = "Bad date" };
        var (popup, manager) = Open(s.Source, s.When);
        Tap(manager, ConditionsTabX, SwitchY);
        Key(popup, KumikoKey.Tab);
        Type(popup, "45/45/2024");

        Key(popup, KumikoKey.Enter);

        Assert.Null(s.When.ActiveFilter);
        Assert.Equal("Bad date", popup.ConditionError);
    }

    // ── OK keeps the page the user did not change; Clear removes both ──

    [Fact]
    public void OkOnConditions_KeepsTheCheckedValues()
    {
        var s = Create();
        s.Source.SetColumnFilter(s.Amount, new FilterDescription(s.Amount)
        {
            SelectedValues = new HashSet<string> { "5", "20", "2000" },
        });
        var (popup, manager) = Open(s.Source, s.Amount);
        Assert.Equal(KumikoFilterPopupMode.Values, popup.Mode);
        Tap(manager, ConditionsTabX, SwitchY);
        popup.OperatorBox(0).SelectedIndex = 2; // Greater than
        popup.ValueTextBox(0).Text = "10";

        Tap(manager, OkX, ButtonsY);

        var filter = s.Amount.ActiveFilter!;
        Assert.Equal(new[] { "5", "20", "2000" }, filter.SelectedValues!.OrderBy(v => v.Length));
        Assert.Equal(10.0, filter.Condition1!.Value);
        Assert.Equal(2, s.Source.RowCount); // the values and the condition are combined with And
    }

    [Fact]
    public void OkOnValues_KeepsConditionsItCannotShow()
    {
        var s = Create();
        // A text operator on a numeric column, set from code: the Conditions page cannot show it, and OK on Values keeps it
        var fromCode = new FilterCondition { TextOperator = TextFilterOperator.StartsWith, NumericOperator = NumericFilterOperator.LessThan, Value = "3000" };
        s.Source.SetColumnFilter(s.Amount, new FilterDescription(s.Amount) { Condition1 = fromCode });
        var (popup, manager) = Open(s.Source, s.Amount);
        Tap(manager, ValuesTabX, SwitchY);

        Key(popup, KumikoKey.Enter);

        var kept = s.Amount.ActiveFilter!.Condition1!;
        Assert.NotSame(fromCode, kept);
        Assert.Equal(TextFilterOperator.StartsWith, kept.TextOperator);
        Assert.Equal("3000", kept.Value);
    }

    [Fact]
    public void Clear_RemovesTheValuesAndTheConditions()
    {
        var s = Create();
        s.Source.SetColumnFilter(s.Amount, new FilterDescription(s.Amount)
        {
            SelectedValues = new HashSet<string> { "5" },
            Condition1 = new FilterCondition { Value = 5.0 },
        });
        var (_, manager) = Open(s.Source, s.Amount);

        Tap(manager, ClearX, ButtonsY);

        Assert.Null(s.Amount.ActiveFilter);
        Assert.Equal(4, s.Source.RowCount);
    }

    [Fact]
    public void TheInactiveTab_ShowsAMarker_WhenItHoldsCriteria()
    {
        var s = Create();
        var (popup, manager) = Open(s.Source, s.Amount);
        Assert.Equal(0, Markers(popup));

        Tap(manager, ConditionsTabX, SwitchY);
        popup.ValueTextBox(0).Text = "3";
        Assert.True(popup.ConditionsHaveCriteria);
        Assert.Equal(0, Markers(popup)); // the active tab has none

        Tap(manager, ValuesTabX, SwitchY);
        Assert.Equal(1, Markers(popup)); // on Conditions

        popup.Mode = KumikoFilterPopupMode.Conditions;
        Assert.False(popup.ValuesHaveCriteria);
        Assert.Equal(0, Markers(popup));
    }

    private static int Markers(DrawnFilterPopup popup)
    {
        var ctx = new RecordingDrawingContext();
        popup.OnDraw(ctx);
        return ctx.Ops.Count(o => o.Rect.Width == 5 && o.Rect.Height == 5 && o.Rect.Y == 47);
    }

    // ── Layout round trip ─────────────────────────────────────────

    [Fact]
    public void ConditionsSetThroughThePopup_SurviveALayoutRoundTrip()
    {
        var s = Create();
        var (popup, manager) = Open(s.Source, s.When);
        Tap(manager, ConditionsTabX, SwitchY);
        popup.OperatorBox(0).SelectedIndex = 4; // On or before
        popup.ValueDatePicker(0).SelectedDate = Day;
        Tap(manager, OrX, CombinationY);
        popup.OperatorBox(1).SelectedIndex = 3; // After
        popup.ValueDatePicker(1).SelectedDate = Day.AddDays(5);
        Tap(manager, OkX, ButtonsY);
        Assert.Equal(new[] { "Alpha", "Beta", "Gamma" }, VisibleNames(s.Source, s.Name));

        string json = KumikoLayoutState.Capture(s.Source).ToJson();
        var restored = Create();
        KumikoLayoutState.FromJson(json).ApplyTo(restored.Source);

        var filter = restored.When.ActiveFilter!;
        Assert.Equal(DateFilterOperator.BeforeOrEqual, filter.Condition1!.DateOperator);
        Assert.Equal(Day, filter.Condition1.Value);
        Assert.Equal(DateFilterOperator.After, filter.Condition2!.DateOperator);
        Assert.Equal(Day.AddDays(5), filter.Condition2.Value);
        Assert.Equal(FilterCombination.Or, filter.Combination);
        Assert.Equal(new[] { "Alpha", "Beta", "Gamma" }, VisibleNames(restored.Source, restored.Name));

        // And the restored filter opens on the Conditions page with the same rows
        var (reopened, _) = Open(restored.Source, restored.When);
        Assert.Equal(KumikoFilterPopupMode.Conditions, reopened.Mode);
        Assert.Equal("On or before", reopened.OperatorBox(0).SelectedText);
        Assert.Equal(Day.AddDays(5), reopened.ValueDatePicker(1).SelectedDate);
        Assert.Equal(FilterCombination.Or, reopened.Combination);
    }

    // ── Drawing ───────────────────────────────────────────────────

    [Fact]
    public void TheConditionsPage_DrawsWithoutAllocating()
    {
        var s = Create();
        var (popup, _) = Open(s.Source, s.Amount);
        popup.Mode = KumikoFilterPopupMode.Conditions;
        popup.ValueTextBox(0).Text = "12";
        var ctx = new NoOpDrawingContext();
        popup.OnDraw(ctx);
        popup.OnDraw(ctx);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10; i++) popup.OnDraw(ctx);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void TheTheme_ReachesTheConditionEditors()
    {
        var s = Create();
        var (popup, _) = Open(s.Source, s.When);
        var dark = KumikoTheme.Create(KumikoThemeMode.Dark);
        popup.ApplyTheme(dark);
        popup.Mode = KumikoFilterPopupMode.Conditions;

        var ctx = new RecordingDrawingContext();
        popup.OnDraw(ctx);

        Assert.DoesNotContain(ctx.Fills, f => f.Color == KumikoColor.White);
        Assert.Equal(dark.BackgroundColor, popup.ValueDatePicker(0).BackgroundColor);
    }

    private static List<string> Draw(DrawnFilterPopup popup)
    {
        var ctx = new RecordingDrawingContext();
        popup.OnDraw(ctx);
        return ctx.Texts.Select(t => t.Text!).ToList();
    }
}

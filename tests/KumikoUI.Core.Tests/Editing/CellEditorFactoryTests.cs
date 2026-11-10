using KumikoUI.Core.Components;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Tests.Editing;

/// <summary>
/// The editor factory: which drawn editor each column type gets, how the editor starts (initial value,
/// initial character, selection mode), what <c>GetEditorValue</c> reads back, and how themes are applied.
/// </summary>
public class CellEditorFactoryTests
{
    private static readonly KumikoRect Bounds = new(10, 20, 120, 30);

    private static KumikoColumn Column(KumikoColumnType type, Action<KumikoColumn>? configure = null)
    {
        var column = new KumikoColumn { Header = "H", PropertyName = "P", ColumnType = type };
        configure?.Invoke(column);
        return column;
    }

    private static T Create<T>(KumikoColumn column, object? value = null, char? initial = null,
        EditTextSelectionMode mode = EditTextSelectionMode.SelectAll) where T : DrawnComponent =>
        Assert.IsType<T>(CellEditorFactory.CreateEditor(column, value, Bounds, initial, mode));

    // ── Editor type per column type ───────────────────────────────

    [Theory]
    [InlineData(KumikoColumnType.Text, typeof(DrawnTextBox))]
    [InlineData(KumikoColumnType.Numeric, typeof(DrawnTextBox))]
    [InlineData(KumikoColumnType.Date, typeof(DrawnDatePicker))]
    [InlineData(KumikoColumnType.ComboBox, typeof(DrawnComboBox))]
    [InlineData(KumikoColumnType.Picker, typeof(DrawnScrollPicker))]
    [InlineData(KumikoColumnType.Template, typeof(DrawnTextBox))] // text fallback
    public void CreateEditor_ReturnsTheEditorForTheColumnType(KumikoColumnType type, Type expected)
    {
        var editor = CellEditorFactory.CreateEditor(Column(type), null, Bounds);

        Assert.NotNull(editor);
        Assert.IsType(expected, editor);
        Assert.True(editor!.IsVisible);
    }

    [Theory]
    [InlineData(KumikoColumnType.Boolean)] // toggled in place
    [InlineData(KumikoColumnType.Image)]   // not editable
    [InlineData((KumikoColumnType)99)]     // unknown
    public void CreateEditor_ReturnsNullWhereThereIsNoOverlayEditor(KumikoColumnType type)
    {
        Assert.Null(CellEditorFactory.CreateEditor(Column(type), true, Bounds));
    }

    [Fact]
    public void CreateEditor_SetsTheCellBounds_ExceptForThePicker()
    {
        Assert.Equal(Bounds, Create<DrawnTextBox>(Column(KumikoColumnType.Text)).Bounds);
        Assert.Equal(Bounds, Create<DrawnTextBox>(Column(KumikoColumnType.Numeric)).Bounds);
        Assert.Equal(Bounds, Create<DrawnDatePicker>(Column(KumikoColumnType.Date)).Bounds);
        Assert.Equal(Bounds, Create<DrawnComboBox>(Column(KumikoColumnType.ComboBox)).Bounds);
    }

    // ── Text ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("hello", "hello")]
    [InlineData(null, "")]
    [InlineData(42, "42")]
    public void TextEditor_StartsWithTheCurrentValue(object? value, string expected)
    {
        var box = Create<DrawnTextBox>(Column(KumikoColumnType.Text), value);

        Assert.Equal(expected, box.Text);
        Assert.False(box.NumericOnly);
        Assert.Equal(4f, box.Padding);
    }

    [Fact]
    public void TextEditor_InitialCharacterReplacesTheValue_AndPutsTheCursorAtTheEnd()
    {
        var box = Create<DrawnTextBox>(Column(KumikoColumnType.Text), "old", 'x', EditTextSelectionMode.SelectAll);

        Assert.Equal("x", box.Text);
        Assert.Equal(EditTextSelectionMode.CursorAtEnd, box.InitialSelectionMode);
    }

    [Theory]
    [InlineData(EditTextSelectionMode.SelectAll)]
    [InlineData(EditTextSelectionMode.CursorAtEnd)]
    public void TextEditor_HonorsTheSelectionModeWhenNothingWasTyped(EditTextSelectionMode mode)
    {
        var box = Create<DrawnTextBox>(Column(KumikoColumnType.Text), "abc", null, mode);

        Assert.Equal(mode, box.InitialSelectionMode);
    }

    // ── Numeric ───────────────────────────────────────────────────

    [Fact]
    public void NumericEditor_IsANumericOnlyTextBoxHoldingTheValue()
    {
        var box = Create<DrawnTextBox>(Column(KumikoColumnType.Numeric), 12.5);

        Assert.True(box.NumericOnly);
        Assert.Equal("12.5", box.Text);
    }

    [Theory]
    [InlineData('7', "7")]
    [InlineData('-', "-")]
    [InlineData('.', ".")]
    public void NumericEditor_AcceptsDigitSignAndPointAsTheInitialCharacter(char initial, string expected)
    {
        var box = Create<DrawnTextBox>(Column(KumikoColumnType.Numeric), 99, initial);

        Assert.Equal(expected, box.Text);
        Assert.Equal(EditTextSelectionMode.CursorAtEnd, box.InitialSelectionMode);
    }

    [Theory]
    [InlineData('x', 99, "99")]
    [InlineData('#', 99, "99")]
    [InlineData(' ', 99, "99")]
    [InlineData('x', null, "")]
    public void NumericEditor_IgnoresANonNumericInitialCharacter_KeepingTheCurrentValue(
        char initial, object? value, string expected)
    {
        var box = Create<DrawnTextBox>(Column(KumikoColumnType.Numeric), value, initial);

        Assert.Equal(expected, box.Text);
    }

    // ── Date ──────────────────────────────────────────────────────

    [Fact]
    public void DateEditor_ShowsADateTimeAndOpensTheCalendar()
    {
        var picker = Create<DrawnDatePicker>(Column(KumikoColumnType.Date), new DateTime(2024, 3, 5, 14, 30, 0));

        Assert.Equal(new DateTime(2024, 3, 5), picker.SelectedDate);
        Assert.True(picker.IsCalendarOpen);
    }

    [Fact]
    public void DateEditor_ShowsADateTimeOffset()
    {
        var value = new DateTimeOffset(2023, 11, 9, 8, 0, 0, TimeSpan.FromHours(2));

        var picker = Create<DrawnDatePicker>(Column(KumikoColumnType.Date), value);

        Assert.Equal(new DateTime(2023, 11, 9), picker.SelectedDate);
    }

    [Fact]
    public void DateEditor_ParsesAStringValue()
    {
        var picker = Create<DrawnDatePicker>(Column(KumikoColumnType.Date), "2022-07-04");

        Assert.Equal(new DateTime(2022, 7, 4), picker.SelectedDate);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not a date")]
    public void DateEditor_SelectsNothing_ForAMissingOrUnparsableValue(object? value)
    {
        var picker = Create<DrawnDatePicker>(Column(KumikoColumnType.Date), value);

        Assert.Null(picker.SelectedDate);
        Assert.False(((IValueEditor)picker).HasValue);
        Assert.True(picker.IsCalendarOpen);
    }

    // ── ComboBox ──────────────────────────────────────────────────

    [Fact]
    public void ComboBoxEditor_ListsTheEditorItems_SelectsTheValue_AndOpensTheDropdown()
    {
        var column = Column(KumikoColumnType.ComboBox, c => c.EditorItemsString = "Low, Mid, High");

        var combo = Create<DrawnComboBox>(column, "Mid");

        Assert.Equal(new[] { "Low", "Mid", "High" }, combo.Items.Select(i => i.DisplayText));
        Assert.Equal("Mid", combo.SelectedValue);
        Assert.Equal(1, combo.SelectedIndex);
        Assert.True(combo.IsDropdownOpen);
    }

    [Fact]
    public void ComboBoxEditor_WithoutItemsOrValue_IsEmpty()
    {
        var combo = Create<DrawnComboBox>(Column(KumikoColumnType.ComboBox));

        Assert.Empty(combo.Items);
        Assert.Equal(-1, combo.SelectedIndex);
        Assert.Null(combo.SelectedValue);
    }

    [Fact]
    public void ComboBoxEditor_UnknownValueSelectsNothing()
    {
        var column = Column(KumikoColumnType.ComboBox, c => c.EditorItemsString = "A,B");

        var combo = Create<DrawnComboBox>(column, "Z");

        Assert.Equal(-1, combo.SelectedIndex);
    }

    private sealed record Choice(int Id, string Label);

    [Fact]
    public void ComboBoxEditor_UsesDisplayAndValueMemberPaths()
    {
        var column = Column(KumikoColumnType.ComboBox, c =>
        {
            c.EditorItems = new[] { new Choice(1, "One"), new Choice(2, "Two") };
            c.EditorDisplayMemberPath = nameof(Choice.Label);
            c.EditorValueMemberPath = nameof(Choice.Id);
        });

        var combo = Create<DrawnComboBox>(column, 2);

        Assert.Equal(new[] { "One", "Two" }, combo.Items.Select(i => i.DisplayText));
        Assert.Equal(2, combo.SelectedValue);
        Assert.Equal("Two", combo.SelectedText);
    }

    [Fact]
    public void ComboBoxEditor_UsesDisplayAndValueSelectors()
    {
        var column = Column(KumikoColumnType.ComboBox, c =>
        {
            c.EditorItems = new[] { new Choice(1, "One"), new Choice(2, "Two") };
            c.EditorDisplaySelector = o => ((Choice)o).Label.ToUpperInvariant();
            c.EditorValueSelector = o => ((Choice)o).Id * 10;
        });

        var combo = Create<DrawnComboBox>(column, 10);

        Assert.Equal(new[] { "ONE", "TWO" }, combo.Items.Select(i => i.DisplayText));
        Assert.Equal(10, combo.SelectedValue);
        Assert.Equal("ONE", combo.SelectedText);
    }

    // ── Picker ────────────────────────────────────────────────────

    [Fact]
    public void PickerEditor_ListsTheItemsAsStrings_AndSelectsTheValue()
    {
        var column = Column(KumikoColumnType.Picker, c => c.EditorItems = new object?[] { "S", 5, null });

        var picker = Create<DrawnScrollPicker>(column, 5);

        Assert.Equal(new[] { "S", "5", "" }, picker.Items);
        Assert.Equal(1, picker.SelectedIndex);
        Assert.Equal("5", picker.SelectedValue);
    }

    [Fact]
    public void PickerEditor_IsExpandedToTheHeightOfItsWheel_AtTheCellOrigin()
    {
        var picker = Create<DrawnScrollPicker>(Column(KumikoColumnType.Picker, c => c.EditorItemsString = "a,b,c"));

        Assert.Equal(Bounds.X, picker.Bounds.X);
        Assert.Equal(Bounds.Y, picker.Bounds.Y);
        Assert.Equal(Bounds.Width, picker.Bounds.Width);
        Assert.Equal(picker.VisibleItemCount * picker.ItemHeight, picker.Bounds.Height);
    }

    [Fact]
    public void PickerEditor_UnknownValue_SelectsNothing()
    {
        var column = Column(KumikoColumnType.Picker, c => c.EditorItemsString = "a,b");

        var picker = Create<DrawnScrollPicker>(column, "zzz");

        Assert.Equal(-1, picker.SelectedIndex);
        Assert.False(((IValueEditor)picker).HasValue);
    }

    // ── Template ──────────────────────────────────────────────────

    private sealed class MarkerComponent : DrawnComponent
    {
        public override void OnDraw(IDrawingContext ctx) { }
    }

    private sealed class ProviderRenderer : ICellRenderer, ICellEditorProvider
    {
        public DrawnComponent? Result { get; set; } = new MarkerComponent();

        public void Render(IDrawingContext ctx, KumikoRect cellRect, object? value, string displayText,
            KumikoColumn column, KumikoStyle style, bool isSelected, KumikoCellStyle? cellStyle = null)
        { }

        public DrawnComponent? CreateEditor(object? value, KumikoRect bounds) => Result;
    }

    private sealed class DescriptorStub : EditorDescriptor
    {
        public DrawnComponent? Result { get; set; } = new MarkerComponent();
        public override DrawnComponent? CreateEditor(object? currentValue, KumikoRect cellBounds) => Result;
    }

    [Fact]
    public void TemplateEditor_CustomFactoryWinsOverDescriptorAndProvider()
    {
        var fromFactory = new MarkerComponent();
        object? seenValue = null;
        KumikoRect seenBounds = default;
        var column = Column(KumikoColumnType.Template, c =>
        {
            c.CustomEditorFactory = (v, b) => { seenValue = v; seenBounds = b; return fromFactory; };
            c.EditorDescriptor = new DescriptorStub();
            c.CustomCellRenderer = new ProviderRenderer();
        });

        var editor = CellEditorFactory.CreateEditor(column, "v", Bounds);

        Assert.Same(fromFactory, editor);
        Assert.Equal("v", seenValue);
        Assert.Equal(Bounds, seenBounds);
    }

    [Fact]
    public void TemplateEditor_DescriptorWinsOverProvider()
    {
        var fromDescriptor = new MarkerComponent();
        var column = Column(KumikoColumnType.Template, c =>
        {
            c.EditorDescriptor = new DescriptorStub { Result = fromDescriptor };
            c.CustomCellRenderer = new ProviderRenderer();
        });

        Assert.Same(fromDescriptor, CellEditorFactory.CreateEditor(column, null, Bounds));
    }

    [Fact]
    public void TemplateEditor_UsesTheRendererWhenItIsAnEditorProvider()
    {
        var fromProvider = new MarkerComponent();
        var column = Column(KumikoColumnType.Template, c => c.CustomCellRenderer = new ProviderRenderer { Result = fromProvider });

        Assert.Same(fromProvider, CellEditorFactory.CreateEditor(column, null, Bounds));
    }

    [Fact]
    public void TemplateEditor_ProviderReturningNullMeansNoEditor()
    {
        var column = Column(KumikoColumnType.Template, c => c.CustomCellRenderer = new ProviderRenderer { Result = null });

        Assert.Null(CellEditorFactory.CreateEditor(column, null, Bounds));
    }

    [Fact]
    public void TemplateEditor_FallsBackToATextBox()
    {
        var box = Create<DrawnTextBox>(Column(KumikoColumnType.Template), "abc");

        Assert.Equal("abc", box.Text);
    }

    // ── GetEditorValue ────────────────────────────────────────────

    private static object? ValueOfText(KumikoColumnType type, string text)
    {
        var box = new DrawnTextBox { Text = text };
        return CellEditorFactory.GetEditorValue(box, Column(type));
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("")]
    public void GetEditorValue_TextColumn_ReturnsTheText(string text)
    {
        Assert.Equal(text, ValueOfText(KumikoColumnType.Text, text));
    }

    [Theory]
    [InlineData(KumikoColumnType.Numeric, "42")]
    [InlineData(KumikoColumnType.Numeric, "")]
    [InlineData(KumikoColumnType.Numeric, "abc")]
    [InlineData(KumikoColumnType.Date, "2024-01-31")]
    public void GetEditorValue_TextBox_ReturnsTheTextAsTyped_TheCommitConvertsIt(KumikoColumnType type, string text)
    {
        // Conversion to the property type happens in EditSession.CommitEdit (KumikoValueConverter, #32)
        Assert.Equal(text, ValueOfText(type, text));
    }

    [Fact]
    public void GetEditorValue_ReadsEachEditorKind()
    {
        var numeric = new DrawnNumericUpDown { Value = 3.5 };
        Assert.Equal(3.5, CellEditorFactory.GetEditorValue(numeric, Column(KumikoColumnType.Template)));

        var picker = new DrawnDatePicker { SelectedDate = new DateTime(2020, 2, 29) };
        Assert.Equal(new DateTime(2020, 2, 29), CellEditorFactory.GetEditorValue(picker, Column(KumikoColumnType.Date)));

        var combo = new DrawnComboBox();
        combo.AddItem("One", 1);
        combo.AddItem("Two", 2);
        combo.SelectedIndex = 1;
        Assert.Equal(2, CellEditorFactory.GetEditorValue(combo, Column(KumikoColumnType.ComboBox)));

        var wheel = new DrawnScrollPicker();
        wheel.SetItems(new[] { "x", "y" });
        wheel.SelectedIndex = 1;
        Assert.Equal("y", CellEditorFactory.GetEditorValue(wheel, Column(KumikoColumnType.Picker)));
    }

    [Fact]
    public void GetEditorValue_ComboBoxWithNothingSelected_IsNull()
    {
        Assert.Null(CellEditorFactory.GetEditorValue(new DrawnComboBox(), Column(KumikoColumnType.ComboBox)));
    }

    [Fact]
    public void GetEditorValue_UnknownEditorKind_IsNull()
    {
        Assert.Null(CellEditorFactory.GetEditorValue(new MarkerComponent(), Column(KumikoColumnType.Template)));
    }

    // ── ApplyThemeToEditor ────────────────────────────────────────

    private static KumikoStyle ThemedStyle() => new()
    {
        BackgroundColor = new KumikoColor(10, 20, 30),
        CellTextColor = new KumikoColor(200, 210, 220),
        GridLineColor = new KumikoColor(70, 80, 90),
        AccentColor = new KumikoColor(250, 100, 50),
    };

    [Fact]
    public void ApplyThemeToEditor_TextBox()
    {
        var style = ThemedStyle();
        var box = new DrawnTextBox();

        CellEditorFactory.ApplyThemeToEditor(box, style);

        Assert.Equal(style.CellTextColor, box.TextColor);
        Assert.Equal(style.BackgroundColor, box.BackgroundColor);
        Assert.Equal(style.GridLineColor, box.BorderColor);
        Assert.Equal(style.AccentColor, box.FocusedBorderColor);
        Assert.Equal(style.AccentColor.WithAlpha(80), box.SelectionHighlightColor);
    }

    [Fact]
    public void ApplyThemeToEditor_ComboBox()
    {
        var style = ThemedStyle();
        var combo = new DrawnComboBox();

        CellEditorFactory.ApplyThemeToEditor(combo, style);

        Assert.Equal(style.CellTextColor, combo.TextColor);
        Assert.Equal(style.BackgroundColor, combo.BackgroundColor);
        Assert.Equal(style.BackgroundColor, combo.DropdownBackgroundColor);
        Assert.Equal(style.AccentColor, combo.FocusedBorderColor);
    }

    [Fact]
    public void ApplyThemeToEditor_NumericUpDown()
    {
        var style = ThemedStyle();
        var numeric = new DrawnNumericUpDown();

        CellEditorFactory.ApplyThemeToEditor(numeric, style);

        Assert.Equal(style.CellTextColor, numeric.TextColor);
        Assert.Equal(style.CellTextColor, numeric.ButtonSymbolColor);
        Assert.Equal(style.BackgroundColor, numeric.BackgroundColor);
        Assert.Equal(style.GridLineColor, numeric.BorderColor);
    }

    [Fact]
    public void ApplyThemeToEditor_DatePicker()
    {
        var style = ThemedStyle();
        var picker = new DrawnDatePicker();

        CellEditorFactory.ApplyThemeToEditor(picker, style);

        Assert.Equal(style.CellTextColor, picker.TextColor);
        Assert.Equal(style.BackgroundColor, picker.CalendarBackgroundColor);
        Assert.Equal(style.AccentColor, picker.SelectedDayColor);
    }

    [Fact]
    public void ApplyThemeToEditor_ScrollPicker()
    {
        var style = ThemedStyle();
        var wheel = new DrawnScrollPicker();

        CellEditorFactory.ApplyThemeToEditor(wheel, style);

        Assert.Equal(style.CellTextColor, wheel.SelectedTextColor);
        Assert.Equal(style.BackgroundColor, wheel.BackgroundColor);
        Assert.Equal(style.GridLineColor, wheel.SeparatorColor);
    }

    private sealed class ThemeSpyButtons : DrawnActionButtons
    {
        public KumikoStyle? Applied { get; private set; }
        public override void ApplyTheme(KumikoStyle style) => Applied = style;
    }

    [Fact]
    public void ApplyThemeToEditor_ActionButtons_CallsTheirApplyTheme()
    {
        var style = ThemedStyle();
        var buttons = new ThemeSpyButtons();

        CellEditorFactory.ApplyThemeToEditor(buttons, style);

        Assert.Same(style, buttons.Applied);
    }

    [Fact]
    public void ApplyThemeToEditor_UnknownEditorIsLeftAlone()
    {
        var editor = new MarkerComponent();

        var error = Record.Exception(() => CellEditorFactory.ApplyThemeToEditor(editor, ThemedStyle()));

        Assert.Null(error);
        Assert.True(editor.IsVisible);
    }
}

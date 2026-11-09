using KumikoUI.Core.Components;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Tests.Editing;

/// <summary>
/// The value an editor hands back, converted to the edited property's type through a full edit
/// (begin, type, commit): numeric widths, nullable forms, dates, times, booleans, enums, strings and
/// blank text (#32). The source parses in the invariant culture here; culture-specific parsing is covered
/// by <c>KumikoValueConverterTests</c>.
/// </summary>
public class EditorValueConversionTests
{
    internal enum Mode { Off, On, Auto }

    internal sealed class TypedRow
    {
        public int IntValue { get; set; } = 1;
        public long LongValue { get; set; } = 1;
        public decimal DecimalValue { get; set; } = 1m;
        public double DoubleValue { get; set; } = 1;
        public float FloatValue { get; set; } = 1;
        public int? NullableInt { get; set; } = 1;
        public decimal? NullableDecimal { get; set; } = 1m;
        public DateTime When { get; set; } = new(2000, 1, 1);
        public DateTime? NullableWhen { get; set; } = new DateTime(2000, 1, 1);
        public DateOnly Day { get; set; } = new(2000, 1, 1);
        public TimeOnly Time { get; set; } = new(1, 0);
        public TimeSpan Span { get; set; } = TimeSpan.FromHours(1);
        public bool Flag { get; set; }
        public Mode Mode { get; set; } = Mode.Off;
        public string? Name { get; set; } = "start";
    }

    private static readonly KumikoRect Bounds = new(0, 0, 100, 30);

    /// <summary>Edits <paramref name="property"/> of a fresh row through the session; returns the commit result.</summary>
    private static (bool Committed, TypedRow Row) EditAndCommit(
        string property, KumikoColumnType type, Action<DrawnComponent> setEditorValue,
        Action<KumikoColumn>? configure = null)
    {
        var row = new TypedRow();
        var column = new KumikoColumn { Header = property, PropertyName = property, ColumnType = type };
        configure?.Invoke(column);
        var source = new KumikoSource { Culture = System.Globalization.CultureInfo.InvariantCulture };
        source.SetColumns(new[] { column });
        source.SetItems(new[] { row });
        var session = new EditSession();

        Assert.True(session.BeginEdit(0, 0, column, source, Bounds));
        setEditorValue(session.ActiveEditor!);
        bool committed = session.CommitEdit(source);
        return (committed, row);
    }

    private static Action<DrawnComponent> Text(string text) => e => ((DrawnTextBox)e).Text = text;

    // ── Numeric text into numeric properties ──────────────────────

    [Fact]
    public void Int_FromWholeNumberText()
    {
        var (committed, row) = EditAndCommit(nameof(TypedRow.IntValue), KumikoColumnType.Numeric, Text("42"));

        Assert.True(committed);
        Assert.Equal(42, row.IntValue);
    }

    [Fact]
    public void Int_FromNegativeText()
    {
        var (_, row) = EditAndCommit(nameof(TypedRow.IntValue), KumikoColumnType.Numeric, Text("-17"));

        Assert.Equal(-17, row.IntValue);
    }

    [Fact]
    public void Long_FromTextBeyondIntRange()
    {
        var (_, row) = EditAndCommit(nameof(TypedRow.LongValue), KumikoColumnType.Numeric, Text("9999999999"));

        Assert.Equal(9_999_999_999L, row.LongValue);
    }

    [Fact]
    public void Long_FromSmallText()
    {
        var (_, row) = EditAndCommit(nameof(TypedRow.LongValue), KumikoColumnType.Numeric, Text("12"));

        Assert.Equal(12L, row.LongValue);
    }

    [Fact]
    public void Decimal_FromFractionalText()
    {
        var (_, row) = EditAndCommit(nameof(TypedRow.DecimalValue), KumikoColumnType.Numeric, Text("19.99"));

        Assert.Equal(19.99m, row.DecimalValue);
    }

    [Fact]
    public void Decimal_FromWholeText()
    {
        var (_, row) = EditAndCommit(nameof(TypedRow.DecimalValue), KumikoColumnType.Numeric, Text("20"));

        Assert.Equal(20m, row.DecimalValue);
    }

    [Fact]
    public void Decimal_KeepsAllDigitsOfPreciseText()
    {
        var (_, row) = EditAndCommit(nameof(TypedRow.DecimalValue), KumikoColumnType.Numeric, Text("1234567.890123456789"));

        Assert.Equal(1234567.890123456789m, row.DecimalValue);
    }

    [Fact]
    public void Double_FromFractionalText()
    {
        var (_, row) = EditAndCommit(nameof(TypedRow.DoubleValue), KumikoColumnType.Numeric, Text("2.5"));

        Assert.Equal(2.5, row.DoubleValue);
    }

    [Fact]
    public void Float_FromFractionalText()
    {
        var (_, row) = EditAndCommit(nameof(TypedRow.FloatValue), KumikoColumnType.Numeric, Text("0.25"));

        Assert.Equal(0.25f, row.FloatValue);
    }

    [Fact]
    public void NullableInt_FromText()
    {
        var (_, row) = EditAndCommit(nameof(TypedRow.NullableInt), KumikoColumnType.Numeric, Text("8"));

        Assert.Equal(8, row.NullableInt);
    }

    [Fact]
    public void NullableInt_BlankTextClearsTheValue()
    {
        var (committed, row) = EditAndCommit(nameof(TypedRow.NullableInt), KumikoColumnType.Numeric, Text(""));

        Assert.True(committed);
        Assert.Null(row.NullableInt);
    }

    [Fact]
    public void NullableDecimal_FromTextAndBlank()
    {
        var (_, row) = EditAndCommit(nameof(TypedRow.NullableDecimal), KumikoColumnType.Numeric, Text("3.5"));
        Assert.Equal(3.5m, row.NullableDecimal);

        var (_, cleared) = EditAndCommit(nameof(TypedRow.NullableDecimal), KumikoColumnType.Numeric, Text("  "));
        Assert.Null(cleared.NullableDecimal);
    }

    [Fact]
    public void Int_ValueTooLargeForTheProperty_IsReportedAsInvalid_AndLeavesTheItemUnchanged()
    {
        var (committed, row) = EditAndCommit(nameof(TypedRow.IntValue), KumikoColumnType.Numeric, Text("9999999999"));

        Assert.False(committed);
        Assert.Equal(1, row.IntValue);
    }

    [Fact]
    public void Int_FractionalTextIsRejected()
    {
        var (committed, row) = EditAndCommit(nameof(TypedRow.IntValue), KumikoColumnType.Numeric, Text("2.6"));

        Assert.False(committed);
        Assert.Equal(1, row.IntValue);
    }

    [Fact]
    public void Int_BlankTextIsRequired_ForTheNonNullableProperty()
    {
        var (committed, row) = EditAndCommit(nameof(TypedRow.IntValue), KumikoColumnType.Numeric, Text(""));

        Assert.False(committed);
        Assert.Equal(1, row.IntValue);
    }

    [Fact]
    public void Int_TextThatIsNotANumber_IsRejected_AndLeavesTheItemUnchanged()
    {
        var (committed, row) = EditAndCommit(nameof(TypedRow.IntValue), KumikoColumnType.Numeric, Text("abc"));

        Assert.False(committed);
        Assert.Equal(1, row.IntValue);
    }

    // ── Numeric up/down editor ────────────────────────────────────

    [Fact]
    public void NumericUpDown_DoubleIsConvertedToTheProperty()
    {
        void Configure(KumikoColumn c) => c.EditorDescriptor = new NumericUpDownEditorDescriptor { Minimum = 0, Maximum = 10 };
        Action<DrawnComponent> set = e => ((DrawnNumericUpDown)e).Value = 7;

        var (_, intRow) = EditAndCommit(nameof(TypedRow.IntValue), KumikoColumnType.Template, set, Configure);
        var (_, decimalRow) = EditAndCommit(nameof(TypedRow.DecimalValue), KumikoColumnType.Template, set, Configure);
        var (_, floatRow) = EditAndCommit(nameof(TypedRow.FloatValue), KumikoColumnType.Template, set, Configure);

        Assert.Equal(7, intRow.IntValue);
        Assert.Equal(7m, decimalRow.DecimalValue);
        Assert.Equal(7f, floatRow.FloatValue);
    }

    // ── Dates, times, spans ───────────────────────────────────────

    [Fact]
    public void DateTime_FromTheDatePicker()
    {
        var (_, row) = EditAndCommit(nameof(TypedRow.When), KumikoColumnType.Date,
            e => ((DrawnDatePicker)e).SelectedDate = new DateTime(2025, 6, 7));

        Assert.Equal(new DateTime(2025, 6, 7), row.When);
    }

    [Fact]
    public void NullableDateTime_FromTheDatePicker()
    {
        var (_, row) = EditAndCommit(nameof(TypedRow.NullableWhen), KumikoColumnType.Date,
            e => ((DrawnDatePicker)e).SelectedDate = new DateTime(2025, 6, 7));

        Assert.Equal(new DateTime(2025, 6, 7), row.NullableWhen);
    }

    [Fact]
    public void DateTime_FromTextTypedIntoATextColumn()
    {
        var (_, row) = EditAndCommit(nameof(TypedRow.When), KumikoColumnType.Text, Text("2031-02-03"));

        Assert.Equal(new DateTime(2031, 2, 3), row.When);
    }

    [Fact]
    public void DateOnly_FromTheDatePicker()
    {
        var (_, row) = EditAndCommit(nameof(TypedRow.Day), KumikoColumnType.Date,
            e => ((DrawnDatePicker)e).SelectedDate = new DateTime(2025, 6, 7));

        Assert.Equal(new DateOnly(2025, 6, 7), row.Day);
    }

    [Fact]
    public void TimeOnly_FromText()
    {
        var (_, row) = EditAndCommit(nameof(TypedRow.Time), KumikoColumnType.Text, Text("13:45"));

        Assert.Equal(new TimeOnly(13, 45), row.Time);
    }

    [Fact]
    public void TimeSpan_FromText()
    {
        var (_, row) = EditAndCommit(nameof(TypedRow.Span), KumikoColumnType.Text, Text("02:30:00"));

        Assert.Equal(TimeSpan.FromMinutes(150), row.Span);
    }

    // ── Booleans ──────────────────────────────────────────────────

    [Fact]
    public void Boolean_ColumnsHaveNoOverlayEditor_SoNothingIsBegun()
    {
        var row = new TypedRow();
        var column = new KumikoColumn { PropertyName = nameof(TypedRow.Flag), ColumnType = KumikoColumnType.Boolean };
        var source = new KumikoSource();
        source.SetColumns(new[] { column });
        source.SetItems(new[] { row });
        var session = new EditSession();

        Assert.False(session.BeginEdit(0, 0, column, source, Bounds));
        Assert.False(session.IsEditing);
    }

    [Fact]
    public void Boolean_ToggleWritesTheOppositeValue()
    {
        var row = new TypedRow();
        var column = new KumikoColumn { PropertyName = nameof(TypedRow.Flag), ColumnType = KumikoColumnType.Boolean };
        var source = new KumikoSource();
        source.SetColumns(new[] { column });
        source.SetItems(new[] { row });
        var session = new EditSession();

        session.ToggleBooleanCell(0, 0, column, source);
        Assert.True(row.Flag);
        session.ToggleBooleanCell(0, 0, column, source);
        Assert.False(row.Flag);
    }

    // ── Enums ─────────────────────────────────────────────────────

    [Fact]
    public void Enum_FromAComboBoxOfEnumValues()
    {
        var (_, row) = EditAndCommit(nameof(TypedRow.Mode), KumikoColumnType.ComboBox,
            e => ((DrawnComboBox)e).SelectedIndex = 2,
            c => c.EditorItems = Enum.GetValues<Mode>());

        Assert.Equal(Mode.Auto, row.Mode);
    }

    [Fact]
    public void Enum_FromAComboBoxOfNames()
    {
        var (_, row) = EditAndCommit(nameof(TypedRow.Mode), KumikoColumnType.ComboBox,
            e => ((DrawnComboBox)e).SelectedIndex = 1,
            c => c.EditorItemsString = "Off,On,Auto");

        Assert.Equal(Mode.On, row.Mode);
    }

    [Fact]
    public void Enum_FromText()
    {
        var (_, row) = EditAndCommit(nameof(TypedRow.Mode), KumikoColumnType.Text, Text("Auto"));

        Assert.Equal(Mode.Auto, row.Mode);
    }

    // ── Strings ───────────────────────────────────────────────────

    [Fact]
    public void String_FromText()
    {
        var (_, row) = EditAndCommit(nameof(TypedRow.Name), KumikoColumnType.Text, Text("Ada"));

        Assert.Equal("Ada", row.Name);
    }

    [Fact]
    public void String_EmptyTextWritesAnEmptyString()
    {
        var (committed, row) = EditAndCommit(nameof(TypedRow.Name), KumikoColumnType.Text, Text(""));

        Assert.True(committed);
        Assert.Equal("", row.Name);
    }

    [Fact]
    public void String_FromAPickerItem()
    {
        var (_, row) = EditAndCommit(nameof(TypedRow.Name), KumikoColumnType.Picker,
            e => ((DrawnScrollPicker)e).SelectedIndex = 1,
            c => c.EditorItemsString = "low,mid,high");

        Assert.Equal("mid", row.Name);
    }

    [Fact]
    public void String_FromANumericColumnConvertsTheNumberToText()
    {
        var (_, row) = EditAndCommit(nameof(TypedRow.Name), KumikoColumnType.Numeric, Text("15"));

        Assert.Equal("15", row.Name);
    }
}

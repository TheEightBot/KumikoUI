using System.Globalization;
using KumikoUI.Core.Components;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Tests.Editing;

/// <summary>
/// Editors never commit a value the user didn't choose (#33). Each editor × (the cell's value is in its
/// list, absent from it, null) keeps the original when committed untouched (an outside tap, Tab, Enter)
/// and raises no <see cref="EditSession.CellValueChanged"/>; a value the user chose is written.
/// </summary>
public class UnchangedEditTests
{
    internal enum Level { Junior, Mid, Senior }

    internal sealed class Employee
    {
        public string Name { get; set; } = "Alice";
        public string? Team { get; set; } = "Ops";
        public Level Level { get; set; } = Level.Mid;
        public int Size { get; set; } = 2;
        public DateTime Hired { get; set; } = new(2024, 3, 5, 14, 30, 0);
        public DateTime? Left { get; set; }
        public DateOnly Birthday { get; set; } = new(1990, 6, 1);
    }

    private sealed class Fixture
    {
        public Employee Employee { get; } = new();
        public KumikoSource Source { get; } = new() { Culture = CultureInfo.InvariantCulture };
        public EditSession Session { get; } = new();
        public List<CellValueChangedEventArgs> Changes { get; } = new();
        public KumikoColumn Column { get; }

        public Fixture(KumikoColumn column, Action<Employee>? setup = null)
        {
            Column = column;
            setup?.Invoke(Employee);
            Source.SetColumns(new[] { column });
            Source.SetItems(new[] { Employee });
            Session.CellValueChanged += (_, e) => Changes.Add(e);
        }

        public DrawnComponent Open()
        {
            Assert.True(Session.BeginEdit(0, 0, Column, Source, new KumikoRect(0, 30, 150, 30)));
            return Session.ActiveEditor!;
        }

        public T Open<T>() where T : DrawnComponent => Assert.IsType<T>(Open());

        /// <summary>Commit as an outside tap, Tab or Enter would.</summary>
        public void Commit() => Assert.True(Session.CommitEdit(Source));
    }

    private static KumikoColumn Combo(string property, params object[] items) =>
        new() { Header = property, PropertyName = property, ColumnType = KumikoColumnType.ComboBox, EditorItems = items };

    private static KumikoColumn Picker(string property, params object[] items) =>
        new() { Header = property, PropertyName = property, ColumnType = KumikoColumnType.Picker, EditorItems = items };

    // ── Combo box ─────────────────────────────────────────────────

    [Theory]
    [InlineData("Ops")]      // in the list
    [InlineData("Research")] // absent from the list
    [InlineData(null)]       // null
    public void ComboBox_Untouched_KeepsTheValue(string? team)
    {
        var f = new Fixture(Combo(nameof(Employee.Team), "Ops", "Sales"), e => e.Team = team);
        var combo = f.Open<DrawnComboBox>();
        Assert.Equal(team == "Ops", ((IValueEditor)combo).HasValue); // only a listed value is selected

        f.Commit();

        Assert.Equal(team, f.Employee.Team);
        Assert.Empty(f.Changes);
    }

    [Theory]
    [InlineData("Research")]
    [InlineData(null)]
    public void ComboBox_AChosenItem_IsWritten(string? team)
    {
        var f = new Fixture(Combo(nameof(Employee.Team), "Ops", "Sales"), e => e.Team = team);
        var combo = f.Open<DrawnComboBox>();

        combo.SelectedIndex = 1;
        f.Commit();

        Assert.Equal("Sales", f.Employee.Team);
        Assert.Equal("Sales", Assert.Single(f.Changes).NewValue);
    }

    [Fact]
    public void ComboBox_ReselectingTheSameItem_RaisesNoChange()
    {
        var f = new Fixture(Combo(nameof(Employee.Team), "Ops", "Sales"));
        var combo = f.Open<DrawnComboBox>();

        combo.SelectedIndex = 1;
        combo.SelectedIndex = 0; // back to "Ops"
        f.Commit();

        Assert.Equal("Ops", f.Employee.Team);
        Assert.Empty(f.Changes);
    }

    [Fact]
    public void ComboBox_OfNames_SelectsAndWritesAnEnumProperty()
    {
        var f = new Fixture(Combo(nameof(Employee.Level), "Junior", "Mid", "Senior"));
        var combo = f.Open<DrawnComboBox>();
        Assert.Equal(1, combo.SelectedIndex); // Level.Mid matched by name

        combo.SelectedIndex = 2;
        f.Commit();

        Assert.Equal(Level.Senior, f.Employee.Level);
    }

    // ── Scroll picker ─────────────────────────────────────────────

    [Theory]
    [InlineData("Ops")]
    [InlineData("Research")]
    [InlineData(null)]
    public void Picker_Untouched_KeepsTheValue(string? team)
    {
        var f = new Fixture(Picker(nameof(Employee.Team), "Ops", "Sales"), e => e.Team = team);
        var picker = f.Open<DrawnScrollPicker>();
        Assert.Equal(team == "Ops" ? 0 : -1, picker.SelectedIndex); // no default to the first item

        f.Commit();

        Assert.Equal(team, f.Employee.Team);
        Assert.Empty(f.Changes);
    }

    [Fact]
    public void Picker_AChosenItem_IsWritten_AsTheItemsValue()
    {
        var f = new Fixture(Picker(nameof(Employee.Level), Level.Junior, Level.Mid, Level.Senior));
        var picker = f.Open<DrawnScrollPicker>();
        Assert.Equal("Mid", picker.SelectedValue);

        picker.OnKeyDown(new KumikoKeyEventArgs { Key = KumikoKey.Down });
        picker.OnKeyDown(new KumikoKeyEventArgs { Key = KumikoKey.Enter }); // confirms: the edit completes

        Assert.False(f.Session.IsEditing);
        Assert.Equal(Level.Senior, f.Employee.Level);
        Assert.Single(f.Changes);
    }

    [Fact]
    public void Picker_WithNothingSelected_ChoosingTheFirstItemWritesIt()
    {
        var f = new Fixture(Picker(nameof(Employee.Team), "Ops", "Sales"), e => e.Team = null);
        var picker = f.Open<DrawnScrollPicker>();

        picker.OnKeyDown(new KumikoKeyEventArgs { Key = KumikoKey.Down }); // nothing → the first item
        f.Commit();

        Assert.Equal("Ops", f.Employee.Team);
    }

    // ── Date picker ───────────────────────────────────────────────

    [Fact]
    public void DatePicker_Untouched_KeepsTheDateAndTime()
    {
        var f = new Fixture(new KumikoColumn { PropertyName = nameof(Employee.Hired), ColumnType = KumikoColumnType.Date });
        f.Open<DrawnDatePicker>();

        f.Commit();

        Assert.Equal(new DateTime(2024, 3, 5, 14, 30, 0), f.Employee.Hired);
        Assert.Empty(f.Changes);
    }

    [Fact]
    public void DatePicker_APickedDate_KeepsTheOriginalTimeOfDay()
    {
        var f = new Fixture(new KumikoColumn { PropertyName = nameof(Employee.Hired), ColumnType = KumikoColumnType.Date });
        var picker = f.Open<DrawnDatePicker>();

        picker.SelectedDate = new DateTime(2024, 3, 9);
        f.Commit();

        Assert.Equal(new DateTime(2024, 3, 9, 14, 30, 0), f.Employee.Hired);
    }

    [Fact]
    public void DatePicker_OnAnEmptyCell_SelectsNothing_AndKeepsItEmpty()
    {
        var f = new Fixture(new KumikoColumn { PropertyName = nameof(Employee.Left), ColumnType = KumikoColumnType.Date });
        var picker = f.Open<DrawnDatePicker>();
        Assert.Null(picker.SelectedDate); // not today

        f.Commit();

        Assert.Null(f.Employee.Left);
        Assert.Empty(f.Changes);
    }

    [Fact]
    public void DatePicker_EnterInTheCalendar_ConfirmsTheDate()
    {
        var f = new Fixture(new KumikoColumn { PropertyName = nameof(Employee.Left), ColumnType = KumikoColumnType.Date });
        var picker = f.Open<DrawnDatePicker>();

        picker.OnKeyDown(new KumikoKeyEventArgs { Key = KumikoKey.Right }); // selects the starting date
        picker.OnKeyDown(new KumikoKeyEventArgs { Key = KumikoKey.Right }); // and moves a day
        picker.OnKeyDown(new KumikoKeyEventArgs { Key = KumikoKey.Enter });

        Assert.False(f.Session.IsEditing);
        Assert.Equal(DateTime.Today.AddDays(1), f.Employee.Left);
    }

    [Fact]
    public void DatePicker_WritesADateOnlyProperty()
    {
        var f = new Fixture(new KumikoColumn { PropertyName = nameof(Employee.Birthday), ColumnType = KumikoColumnType.Date });
        var picker = f.Open<DrawnDatePicker>();
        Assert.Equal(new DateTime(1990, 6, 1), picker.SelectedDate);

        picker.SelectedDate = new DateTime(1991, 7, 2);
        f.Commit();

        Assert.Equal(new DateOnly(1991, 7, 2), f.Employee.Birthday);
    }

    // ── Text and numeric editors ──────────────────────────────────

    [Fact]
    public void TextBox_Untouched_OrChangedBack_RaisesNoChange()
    {
        var f = new Fixture(new KumikoColumn { PropertyName = nameof(Employee.Name) });
        f.Open();
        f.Commit();

        var box = f.Open<DrawnTextBox>();
        box.Text = "Alicia";
        box.Text = "Alice";
        f.Commit();

        Assert.Equal("Alice", f.Employee.Name);
        Assert.Empty(f.Changes);
    }

    [Fact]
    public void TextBox_OpenedByTyping_WritesTheTypedText()
    {
        var f = new Fixture(new KumikoColumn { PropertyName = nameof(Employee.Size), ColumnType = KumikoColumnType.Numeric });
        Assert.True(f.Session.BeginEdit(0, 0, f.Column, f.Source, new KumikoRect(0, 30, 150, 30), initialCharacter: '7'));

        f.Commit();

        Assert.Equal(7, f.Employee.Size);
    }

    [Fact]
    public void NumericUpDown_Untouched_KeepsTheValue_ThenAStepIsWritten()
    {
        var f = new Fixture(new KumikoColumn
        {
            PropertyName = nameof(Employee.Size),
            ColumnType = KumikoColumnType.Template,
            EditorDescriptor = new NumericUpDownEditorDescriptor { Minimum = 0, Maximum = 10 },
        });

        f.Open<DrawnNumericUpDown>();
        f.Commit();
        Assert.Empty(f.Changes);

        var numeric = f.Open<DrawnNumericUpDown>();
        numeric.Value = 3;
        f.Commit();
        Assert.Equal(3, f.Employee.Size);
    }

    // ── Custom editors ────────────────────────────────────────────

    private sealed class Stars : DrawnComponent, IValueEditor
    {
        private int _rating;
        public bool Changed { get; private set; }
        public int Rating { get => _rating; set { _rating = value; Changed = true; } }
        public override void OnDraw(IDrawingContext ctx) { }
        object? IValueEditor.Value => Rating;
        bool IValueEditor.HasValue => true;
        bool IValueEditor.IsDirty => Changed;
    }

    private sealed class Decoration : DrawnComponent
    {
        public override void OnDraw(IDrawingContext ctx) { }
    }

    [Fact]
    public void ACustomValueEditor_IsWrittenOnlyWhenDirty()
    {
        Stars? stars = null;
        var f = new Fixture(new KumikoColumn
        {
            PropertyName = nameof(Employee.Size),
            ColumnType = KumikoColumnType.Template,
            CustomEditorFactory = (_, bounds) => stars = new Stars { Bounds = bounds },
        });

        f.Open();
        f.Commit();
        Assert.Equal(2, f.Employee.Size);

        f.Open();
        stars!.Rating = 5;
        f.Commit();
        Assert.Equal(5, f.Employee.Size);
    }

    [Fact]
    public void ACustomEditorWithoutAValue_NeverWrites()
    {
        var f = new Fixture(new KumikoColumn
        {
            PropertyName = nameof(Employee.Name),
            ColumnType = KumikoColumnType.Template,
            CustomEditorFactory = (_, bounds) => new Decoration { Bounds = bounds },
        });

        f.Open();
        f.Commit();

        Assert.Equal("Alice", f.Employee.Name);
        Assert.Empty(f.Changes);
    }
}

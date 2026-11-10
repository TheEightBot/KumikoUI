using KumikoUI.Core.Components;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Tests.Editing;

/// <summary>
/// Editor descriptors and editor providers on Template columns: what the text and numeric up/down
/// descriptors build, and how a column's descriptor or renderer-provided editor reaches an edit session.
/// </summary>
public class EditorDescriptorTests
{
    private static readonly KumikoRect Bounds = new(5, 6, 140, 30);

    // ── TextEditorDescriptor ──────────────────────────────────────

    [Fact]
    public void TextEditorDescriptor_BuildsATextBoxHoldingTheValue()
    {
        var box = Assert.IsType<DrawnTextBox>(new TextEditorDescriptor().CreateEditor("hello", Bounds));

        Assert.Equal("hello", box.Text);
        Assert.Equal(Bounds, box.Bounds);
        Assert.True(box.IsVisible);
        Assert.Equal(4f, box.Padding);
        Assert.False(box.NumericOnly);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData(12.5, "12.5")]
    public void TextEditorDescriptor_FormatsTheValueAsText(object? value, string expected)
    {
        var box = Assert.IsType<DrawnTextBox>(new TextEditorDescriptor().CreateEditor(value, Bounds));

        Assert.Equal(expected, box.Text);
    }

    [Fact]
    public void TextEditorDescriptor_NumericOnlyIsPassedOn()
    {
        var box = Assert.IsType<DrawnTextBox>(new TextEditorDescriptor { NumericOnly = true }.CreateEditor(1, Bounds));

        Assert.True(box.NumericOnly);
    }

    // ── NumericUpDownEditorDescriptor ─────────────────────────────

    [Fact]
    public void NumericUpDownDescriptor_Defaults()
    {
        var descriptor = new NumericUpDownEditorDescriptor();

        Assert.Equal(double.MinValue, descriptor.Minimum);
        Assert.Equal(double.MaxValue, descriptor.Maximum);
        Assert.Equal(1, descriptor.Step);
        Assert.Equal(-1, descriptor.DecimalPlaces);
        Assert.Equal("G", descriptor.Format);
        Assert.False(descriptor.IsReadOnly);
    }

    [Fact]
    public void NumericUpDownDescriptor_PassesEverySettingOn()
    {
        var descriptor = new NumericUpDownEditorDescriptor
        {
            Minimum = 1, Maximum = 5, Step = 0.5, DecimalPlaces = 1, Format = "N1", IsReadOnly = true
        };

        var editor = Assert.IsType<DrawnNumericUpDown>(descriptor.CreateEditor(3, Bounds));

        Assert.Equal(Bounds, editor.Bounds);
        Assert.Equal(1, editor.Minimum);
        Assert.Equal(5, editor.Maximum);
        Assert.Equal(0.5, editor.Step);
        Assert.Equal(1, editor.DecimalPlaces);
        Assert.Equal("N1", editor.Format);
        Assert.True(editor.IsReadOnly);
        Assert.Equal(3, editor.Value);
    }

    [Theory]
    [InlineData(4, 4.0)]
    [InlineData(2.5, 2.5)]
    [InlineData("3", 3.0)]
    [InlineData(null, 0.0)]       // no value: the default
    [InlineData("many", 0.0)]     // not a number: the default
    [InlineData(true, 1.0)]
    public void NumericUpDownDescriptor_StartsFromTheConvertedValue(object? value, double expected)
    {
        var editor = Assert.IsType<DrawnNumericUpDown>(new NumericUpDownEditorDescriptor().CreateEditor(value, Bounds));

        Assert.Equal(expected, editor.Value);
    }

    [Fact]
    public void NumericUpDownDescriptor_ValueOutsideTheRangeIsClamped()
    {
        var descriptor = new NumericUpDownEditorDescriptor { Minimum = 1, Maximum = 5 };

        Assert.Equal(5, Assert.IsType<DrawnNumericUpDown>(descriptor.CreateEditor(99, Bounds)).Value);
        Assert.Equal(1, Assert.IsType<DrawnNumericUpDown>(descriptor.CreateEditor(-3, Bounds)).Value);
    }

    [Fact]
    public void NumericUpDownDescriptor_ValueThatIsNotConvertibleFallsBackToTheDefault()
    {
        var editor = Assert.IsType<DrawnNumericUpDown>(
            new NumericUpDownEditorDescriptor().CreateEditor(new object(), Bounds));

        Assert.Equal(0, editor.Value);
    }

    [Fact]
    public void NumericUpDownEditor_StepsWithinTheRange()
    {
        var editor = Assert.IsType<DrawnNumericUpDown>(
            new NumericUpDownEditorDescriptor { Minimum = 0, Maximum = 2, Step = 1 }.CreateEditor(1, Bounds));

        editor.Increment();
        editor.Increment();
        Assert.Equal(2, editor.Value);
        editor.Decrement();
        Assert.Equal(1, editor.Value);
    }

    // ── Descriptors used through a column ─────────────────────────

    private sealed class Rating
    {
        public int Stars { get; set; } = 3;
        public string Note { get; set; } = "n";
    }

    private static (KumikoSource Source, KumikoColumn Column, Rating Row, EditSession Session) Grid(
        string property, Action<KumikoColumn> configure)
    {
        var row = new Rating();
        var column = new KumikoColumn { Header = property, PropertyName = property, ColumnType = KumikoColumnType.Template };
        configure(column);
        var source = new KumikoSource();
        source.SetColumns(new[] { column });
        source.SetItems(new[] { row });
        return (source, column, row, new EditSession());
    }

    [Fact]
    public void ColumnDescriptor_NumericUpDown_EditsAndCommitsThroughTheSession()
    {
        var (source, column, row, session) = Grid(nameof(Rating.Stars), c =>
            c.EditorDescriptor = new NumericUpDownEditorDescriptor { Minimum = 1, Maximum = 5 });

        Assert.True(session.BeginEdit(0, 0, column, source, Bounds));
        var editor = Assert.IsType<DrawnNumericUpDown>(session.ActiveEditor);
        Assert.Equal(3, editor.Value);
        editor.Value = 99; // clamped to the maximum
        Assert.True(session.CommitEdit(source));

        Assert.Equal(5, row.Stars);
    }

    [Fact]
    public void ColumnDescriptor_Text_EditsAndCommitsThroughTheSession()
    {
        var (source, column, row, session) = Grid(nameof(Rating.Note), c =>
            c.EditorDescriptor = new TextEditorDescriptor());

        session.BeginEdit(0, 0, column, source, Bounds);
        ((DrawnTextBox)session.ActiveEditor!).Text = "updated";
        session.CommitEdit(source);

        Assert.Equal("updated", row.Note);
    }

    [Fact]
    public void ColumnDescriptor_ReturningNull_MeansTheCellCannotBeEdited()
    {
        var (source, column, _, session) = Grid(nameof(Rating.Note), c =>
            c.EditorDescriptor = new NullDescriptor());

        Assert.False(session.BeginEdit(0, 0, column, source, Bounds));
        Assert.False(session.IsEditing);
    }

    private sealed class NullDescriptor : EditorDescriptor
    {
        public override DrawnComponent? CreateEditor(object? currentValue, KumikoRect cellBounds) => null;
    }

    [Fact]
    public void ColumnDescriptor_ReceivesTheCellValueAndBounds()
    {
        var recorder = new RecordingDescriptor();
        var (source, column, _, session) = Grid(nameof(Rating.Stars), c => c.EditorDescriptor = recorder);

        session.BeginEdit(0, 0, column, source, Bounds);

        Assert.Equal(3, recorder.Value);
        Assert.Equal(Bounds, recorder.Bounds);
    }

    private sealed class RecordingDescriptor : EditorDescriptor
    {
        public object? Value { get; private set; }
        public KumikoRect Bounds { get; private set; }

        public override DrawnComponent? CreateEditor(object? currentValue, KumikoRect cellBounds)
        {
            Value = currentValue;
            Bounds = cellBounds;
            return new DrawnTextBox { Bounds = cellBounds, IsVisible = true };
        }
    }

    // ── ICellEditorProvider ───────────────────────────────────────

    private sealed class ProviderRenderer : ICellRenderer, ICellEditorProvider
    {
        public int Created { get; private set; }

        public void Render(IDrawingContext ctx, in CellRenderContext cell) { }

        public DrawnComponent? CreateEditor(object? value, KumikoRect bounds)
        {
            Created++;
            return new DrawnNumericUpDown { Bounds = bounds, Value = Convert.ToDouble(value), IsVisible = true };
        }
    }

    [Fact]
    public void RendererProvidedEditor_IsUsedByTheSession()
    {
        var renderer = new ProviderRenderer();
        var (source, column, row, session) = Grid(nameof(Rating.Stars), c => c.CustomCellRenderer = renderer);

        Assert.True(session.BeginEdit(0, 0, column, source, Bounds));
        var editor = Assert.IsType<DrawnNumericUpDown>(session.ActiveEditor);
        editor.Value = 4;
        session.CommitEdit(source);

        Assert.Equal(1, renderer.Created);
        Assert.Equal(4, row.Stars);
    }

    [Fact]
    public void ColumnDescriptor_BeatsTheRendererProvidedEditor()
    {
        var renderer = new ProviderRenderer();
        var (source, column, _, session) = Grid(nameof(Rating.Note), c =>
        {
            c.CustomCellRenderer = renderer;
            c.EditorDescriptor = new TextEditorDescriptor();
        });

        session.BeginEdit(0, 0, column, source, Bounds);

        Assert.IsType<DrawnTextBox>(session.ActiveEditor);
        Assert.Equal(0, renderer.Created);
    }

    [Fact]
    public void TemplateColumnWithoutAnyEditorSource_FallsBackToText()
    {
        var (source, column, row, session) = Grid(nameof(Rating.Note), _ => { });

        session.BeginEdit(0, 0, column, source, Bounds);
        ((DrawnTextBox)session.ActiveEditor!).Text = "fallback";
        session.CommitEdit(source);

        Assert.Equal("fallback", row.Note);
    }
}

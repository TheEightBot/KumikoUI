using System.ComponentModel;
using KumikoUI.Core.Components;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.Input;

namespace KumikoUI.Core.Tests.Editing;

/// <summary>
/// Ordering of the editing pipeline (#34): a refused commit blocks whatever would end the edit, one editor
/// exists at a time, events fire in the order CellEndEdit then CellValueChanged, custom editor factories see
/// the typed character, and toggling a boolean cell runs the validation pipeline.
/// </summary>
public class EditOrderingTests
{
    private static readonly KumikoRect Bounds = new(0, 40, 120, 36);

    private static InputGrid InvalidEdit(int row = 2, int column = 0, string message = "no")
    {
        var g = new InputGrid(configure: x => x.Edit.CellValidator = (_, _, _) => CellValidationResult.Error(message));
        g.Select(row, column);
        g.Type('Z');
        Assert.True(g.Edit.IsEditing);
        // A refused commit shows the message; the editor stays
        Assert.False(g.Edit.CommitEdit(g.Source));
        return g;
    }

    private static void AssertEditorKept(InputGrid g, DrawnComponent editor, CellPosition cell)
    {
        Assert.True(g.Edit.IsEditing);
        Assert.True(g.Selection.IsEditing);
        Assert.Same(editor, g.Edit.ActiveEditor);
        Assert.Same(editor, g.Edit.FocusManager.FocusedComponent);
        Assert.True(editor.IsFocused);
        Assert.Equal(cell, g.Selection.CurrentCell);
        Assert.NotNull(g.Edit.CurrentValidation);
    }

    // ── BeginEdit while editing ───────────────────────────────────

    [Fact]
    public void BeginEdit_WhileEditing_WithARefusedCommit_ReturnsFalseAndKeepsTheEditor()
    {
        var g = InvalidEdit();
        var editor = g.Edit.ActiveEditor!;
        var begun = 0;
        g.Edit.CellBeginEdit += (_, _) => begun++;

        bool started = g.Edit.BeginEdit(3, 0, g.Name, g.Source, Bounds);

        Assert.False(started);
        Assert.Equal(0, begun);
        Assert.Same(editor, g.Edit.ActiveEditor);
        Assert.Equal(2, g.Edit.EditCell.Row);
        Assert.Equal("N02", g.Items[2].Name);
        Assert.Equal(1, g.Edit.FocusManager.ComponentCount);
    }

    [Fact]
    public void BeginEdit_WhileEditing_CommitsTheOpenEditFirst()
    {
        var g = new InputGrid();
        Assert.True(g.Edit.BeginEdit(2, 0, g.Name, g.Source, Bounds));
        ((DrawnTextBox)g.Edit.ActiveEditor!).Text = "Zed";

        Assert.True(g.Edit.BeginEdit(3, 0, g.Name, g.Source, Bounds));

        Assert.Equal("Zed", g.Items[2].Name);
        Assert.Equal(3, g.Edit.EditCell.Row);
    }

    [Fact]
    public void BeginEdit_WhileEditing_EndsAndUnregistersTheOldEditorBeforeCreatingTheNext()
    {
        var g = new InputGrid();
        Assert.True(g.Edit.BeginEdit(2, 0, g.Name, g.Source, Bounds));
        var first = g.Edit.ActiveEditor!;
        var seenAtBegin = new List<int>();
        g.Edit.CellBeginEdit += (_, _) => seenAtBegin.Add(g.Edit.FocusManager.ComponentCount);

        Assert.True(g.Edit.BeginEdit(3, 0, g.Name, g.Source, Bounds));
        var second = g.Edit.ActiveEditor!;

        Assert.NotSame(first, second);
        Assert.False(first.IsFocused);
        Assert.True(second.IsFocused);
        Assert.Equal(1, g.Edit.FocusManager.ComponentCount);
        Assert.Same(second, g.Edit.FocusManager.FocusedComponent);
        Assert.Equal(new[] { 0 }, seenAtBegin); // the old editor was gone when the new edit was announced
    }

    [Fact]
    public void FocusManager_HoldsExactlyOneEditorWhileEditing_AndNoneAfter()
    {
        var g = new InputGrid();
        Assert.Equal(0, g.Edit.FocusManager.ComponentCount);

        for (int row = 0; row < 4; row++)
        {
            Assert.True(g.Edit.BeginEdit(row, 0, g.Name, g.Source, Bounds));
            Assert.Equal(1, g.Edit.FocusManager.ComponentCount);
            Assert.Same(g.Edit.ActiveEditor, g.Edit.FocusManager.FocusedComponent);
        }

        g.Edit.CancelEdit();
        Assert.Equal(0, g.Edit.FocusManager.ComponentCount);
        Assert.Null(g.Edit.FocusManager.FocusedComponent);

        Assert.True(g.Edit.BeginEdit(0, 0, g.Name, g.Source, Bounds));
        Assert.True(g.Edit.CommitEdit(g.Source));
        Assert.Equal(0, g.Edit.FocusManager.ComponentCount);
    }

    [Fact]
    public void BeginEdit_FromACellEndEditHandler_LeavesOneEditor()
    {
        var g = new InputGrid();
        g.Edit.CellEndEdit += (_, e) =>
        {
            if (e.RowIndex == 2) g.Edit.BeginEdit(5, 0, g.Name, g.Source, Bounds);
        };
        Assert.True(g.Edit.BeginEdit(2, 0, g.Name, g.Source, Bounds));

        // Committing row 2 makes its handler open row 5; that edit ends, and row 3 opens
        Assert.True(g.Edit.BeginEdit(3, 0, g.Name, g.Source, Bounds));

        Assert.Equal(3, g.Edit.EditCell.Row);
        Assert.Equal(1, g.Edit.FocusManager.ComponentCount);
    }

    // ── Navigation blocked by a refused commit ────────────────────

    [Fact]
    public void PointerTapOnAnotherCell_WithARefusedCommit_KeepsTheEditorAndTheSelection()
    {
        var g = InvalidEdit();
        var editor = g.Edit.ActiveEditor!;
        var redraws = g.Redraws;

        g.Tap(60, g.RowY(5)); // a different row

        AssertEditorKept(g, editor, new CellPosition(2, 0));
        Assert.True(g.Redraws > redraws);
    }

    [Fact]
    public void PointerTapOnAnotherCell_WithAValidEdit_CommitsAndSelects()
    {
        var g = new InputGrid();
        g.Select(2, 0);
        g.Type('Z');

        g.Tap(60, g.RowY(5));

        Assert.False(g.Edit.IsEditing);
        Assert.Equal("Z", g.Items[2].Name);
        Assert.Equal(5, g.Selection.CurrentCell.Row);
    }

    [Fact]
    public void LongPressOnAnotherCell_WithARefusedCommit_KeepsTheEditorAndTheSelection()
    {
        var g = InvalidEdit();
        var editor = g.Edit.ActiveEditor!;

        g.Pointer(InputAction.LongPress, 60, g.RowY(5));

        AssertEditorKept(g, editor, new CellPosition(2, 0));
    }

    [Fact]
    public void Tab_WithARefusedCommit_KeepsTheEditorAndTheSelection()
    {
        var g = InvalidEdit();
        var editor = g.Edit.ActiveEditor!;

        var e = g.Key(KumikoKey.Tab);

        Assert.True(e.Handled);
        AssertEditorKept(g, editor, new CellPosition(2, 0));
    }

    [Fact]
    public void Enter_WithARefusedCommit_KeepsTheEditorAndTheSelection()
    {
        var g = InvalidEdit();
        var editor = g.Edit.ActiveEditor!;

        var e = g.Key(KumikoKey.Enter);

        Assert.True(e.Handled);
        AssertEditorKept(g, editor, new CellPosition(2, 0));
    }

    [Theory]
    [InlineData(KumikoKey.Up)]
    [InlineData(KumikoKey.Down)]
    [InlineData(KumikoKey.PageDown)]
    [InlineData(KumikoKey.PageUp)]
    public void NavigationKeyTheEditorLeavesAlone_WithARefusedCommit_KeepsTheEditorAndTheSelection(KumikoKey key)
    {
        var g = InvalidEdit();
        var editor = g.Edit.ActiveEditor!;

        var e = g.Key(key);

        Assert.True(e.Handled);
        AssertEditorKept(g, editor, new CellPosition(2, 0));
    }

    [Fact]
    public void ArrowDown_WithAValidEdit_CommitsAndMovesTheSelection()
    {
        var g = new InputGrid();
        g.Select(2, 0);
        g.Type('Z');

        g.Key(KumikoKey.Down);

        Assert.False(g.Edit.IsEditing);
        Assert.False(g.Selection.IsEditing);
        Assert.Equal("Z", g.Items[2].Name);
        Assert.Equal(3, g.Selection.CurrentCell.Row);
    }

    // ── Event order ───────────────────────────────────────────────

    [Fact]
    public void CommitEdit_RaisesCellEndEditThenCellValueChanged_WithTheSessionNoLongerEditing()
    {
        var g = new InputGrid();
        var order = new List<string>();
        g.Edit.CellEndEdit += (_, _) => order.Add($"end editing={g.Edit.IsEditing}");
        g.Edit.CellValueChanged += (_, _) => order.Add($"changed editing={g.Edit.IsEditing}");
        g.Select(2, 0);
        g.Type('Z');

        Assert.True(g.Edit.CommitEdit(g.Source));

        Assert.Equal(new[] { "end editing=False", "changed editing=False" }, order);
    }

    [Fact]
    public void CommitEdit_ThatIsRefused_RaisesNeitherEvent()
    {
        var g = InvalidEdit();
        var raised = 0;
        g.Edit.CellEndEdit += (_, _) => raised++;
        g.Edit.CellValueChanged += (_, _) => raised++;

        Assert.False(g.Edit.CommitEdit(g.Source));

        Assert.Equal(0, raised);
    }

    // ── Typing character reaches custom editors ───────────────────

    private sealed class Row
    {
        public string Name { get; set; } = "Alice";
    }

    private sealed class ContextDescriptor : EditorDescriptor
    {
        public CellEditorContext? Seen { get; private set; }
        public bool LegacyCalled { get; private set; }

        public override DrawnComponent? CreateEditor(object? currentValue, KumikoRect cellBounds)
        {
            LegacyCalled = true;
            return new DrawnTextBox { Bounds = cellBounds };
        }

        public override DrawnComponent? CreateEditor(CellEditorContext context)
        {
            Seen = context;
            return new DrawnTextBox { Bounds = context.Bounds };
        }
    }

    private sealed class LegacyDescriptor : EditorDescriptor
    {
        public bool LegacyCalled { get; private set; }

        public override DrawnComponent? CreateEditor(object? currentValue, KumikoRect cellBounds)
        {
            LegacyCalled = true;
            return new DrawnTextBox { Bounds = cellBounds };
        }
    }

    private sealed class ContextProvider : ICellRenderer, ICellEditorProvider
    {
        public CellEditorContext? Seen { get; private set; }

        public void Render(IDrawingContext ctx, KumikoRect cellRect, object? value, string displayText, KumikoColumn column,
            KumikoStyle style, bool isSelected, KumikoCellStyle? cellStyle = null) { }

        public DrawnComponent? CreateEditor(object? value, KumikoRect bounds) => new DrawnTextBox { Bounds = bounds };

        public DrawnComponent? CreateEditor(CellEditorContext context)
        {
            Seen = context;
            return new DrawnTextBox { Bounds = context.Bounds };
        }
    }

    private sealed class LegacyProvider : ICellRenderer, ICellEditorProvider
    {
        public bool Called { get; private set; }

        public void Render(IDrawingContext ctx, KumikoRect cellRect, object? value, string displayText, KumikoColumn column,
            KumikoStyle style, bool isSelected, KumikoCellStyle? cellStyle = null) { }

        public DrawnComponent? CreateEditor(object? value, KumikoRect bounds)
        {
            Called = true;
            return new DrawnTextBox { Bounds = bounds };
        }
    }

    private static (KumikoSource Source, EditSession Session, KumikoColumn Column) TemplateGrid(Action<KumikoColumn> configure)
    {
        var column = new KumikoColumn { Header = "T", PropertyName = nameof(Row.Name), ColumnType = KumikoColumnType.Template };
        configure(column);
        var source = new KumikoSource();
        source.SetColumns(new[] { column });
        source.SetItems(new[] { new Row() });
        return (source, new EditSession(), column);
    }

    [Fact]
    public void EditorFactory_ReceivesTheTypedCharacterValueBoundsAndCulture()
    {
        CellEditorContext? seen = null;
        var (source, session, column) = TemplateGrid(c => c.EditorFactory = ctx =>
        {
            seen = ctx;
            return new DrawnTextBox { Bounds = ctx.Bounds };
        });

        Assert.True(session.BeginEdit(0, 0, column, source, Bounds, 'q'));

        Assert.NotNull(seen);
        Assert.Equal('q', seen!.InitialCharacter);
        Assert.Equal("Alice", seen.Value);
        Assert.Equal(Bounds, seen.Bounds);
        Assert.Same(source.Comparer.Culture, seen.Culture);
    }

    [Fact]
    public void EditorFactory_WithoutATypedCharacter_SeesNull()
    {
        CellEditorContext? seen = null;
        var (source, session, column) = TemplateGrid(c => c.EditorFactory = ctx =>
        {
            seen = ctx;
            return new DrawnTextBox();
        });

        Assert.True(session.BeginEdit(0, 0, column, source, Bounds));

        Assert.Null(seen!.InitialCharacter);
    }

    [Fact]
    public void EditorFactory_TakesPrecedenceOverTheLegacyFactory()
    {
        bool legacy = false;
        var (source, session, column) = TemplateGrid(c =>
        {
            c.CustomEditorFactory = (_, b) => { legacy = true; return new DrawnTextBox { Bounds = b }; };
            c.EditorFactory = ctx => new DrawnTextBox { Bounds = ctx.Bounds };
        });

        Assert.True(session.BeginEdit(0, 0, column, source, Bounds, 'q'));

        Assert.False(legacy);
    }

    [Fact]
    public void CustomEditorFactory_StillWorksAsBefore()
    {
        object? seenValue = null;
        var (source, session, column) = TemplateGrid(c => c.CustomEditorFactory = (value, b) =>
        {
            seenValue = value;
            return new DrawnTextBox { Bounds = b };
        });

        Assert.True(session.BeginEdit(0, 0, column, source, Bounds, 'q'));

        Assert.Equal("Alice", seenValue);
    }

    [Fact]
    public void EditorDescriptor_ReceivesTheTypedCharacter()
    {
        var descriptor = new ContextDescriptor();
        var (source, session, column) = TemplateGrid(c => c.EditorDescriptor = descriptor);

        Assert.True(session.BeginEdit(0, 0, column, source, Bounds, 'q'));

        Assert.Equal('q', descriptor.Seen!.InitialCharacter);
        Assert.False(descriptor.LegacyCalled);
    }

    [Fact]
    public void EditorDescriptor_OverridingOnlyTheLegacyMethod_StillWorks()
    {
        var descriptor = new LegacyDescriptor();
        var (source, session, column) = TemplateGrid(c => c.EditorDescriptor = descriptor);

        Assert.True(session.BeginEdit(0, 0, column, source, Bounds, 'q'));

        Assert.True(descriptor.LegacyCalled);
    }

    [Fact]
    public void TextEditorDescriptor_ShowsTheTypedCharacterInPlaceOfTheValue()
    {
        var (source, session, column) = TemplateGrid(c => c.EditorDescriptor = new TextEditorDescriptor());

        Assert.True(session.BeginEdit(0, 0, column, source, Bounds, 'q'));

        var box = Assert.IsType<DrawnTextBox>(session.ActiveEditor);
        Assert.Equal("q", box.Text);
        Assert.True(((IValueEditor)box).IsDirty);
    }

    [Fact]
    public void CellEditorProvider_ReceivesTheTypedCharacter()
    {
        var provider = new ContextProvider();
        var (source, session, column) = TemplateGrid(c => c.CustomCellRenderer = provider);

        Assert.True(session.BeginEdit(0, 0, column, source, Bounds, 'q'));

        Assert.Equal('q', provider.Seen!.InitialCharacter);
    }

    [Fact]
    public void CellEditorProvider_ImplementingOnlyTheLegacyMethod_StillWorks()
    {
        var provider = new LegacyProvider();
        var (source, session, column) = TemplateGrid(c => c.CustomCellRenderer = provider);

        Assert.True(session.BeginEdit(0, 0, column, source, Bounds, 'q'));

        Assert.True(provider.Called);
    }

    [Fact]
    public void TemplateColumnWithoutACustomEditor_FallsBackToATextEditorShowingTheTypedCharacter()
    {
        var (source, session, column) = TemplateGrid(_ => { });

        Assert.True(session.BeginEdit(0, 0, column, source, Bounds, 'q'));

        Assert.Equal("q", Assert.IsType<DrawnTextBox>(session.ActiveEditor).Text);
    }

    // ── Boolean toggle validation ─────────────────────────────────

    private sealed class StrictRow : IDataErrorInfo
    {
        public bool Active { get; set; }
        public string Name { get; set; } = "Alice";
        public string Error => "";
        public string this[string columnName] => columnName == nameof(Active) && Active ? "Cannot be active." : "";
    }

    private sealed class ToggleFixture
    {
        public StrictRow Row { get; } = new();
        public KumikoSource Source { get; } = new();
        public EditSession Session { get; } = new();
        public KumikoColumn Active { get; } = new() { Header = "A", PropertyName = nameof(StrictRow.Active), ColumnType = KumikoColumnType.Boolean };
        public KumikoColumn Name { get; } = new() { Header = "N", PropertyName = nameof(StrictRow.Name) };
        public List<string> Events { get; } = new();
        public List<CellValidationEventArgs> Reported { get; } = new();
        public List<CellEndEditEventArgs> Ended { get; } = new();
        public List<CellValueChangedEventArgs> Changed { get; } = new();

        public ToggleFixture()
        {
            Source.SetColumns(new[] { Name, Active });
            Source.SetItems(new[] { Row });
            Session.CellEndEdit += (_, e) => { Events.Add("end"); Ended.Add(e); };
            Session.CellValueChanged += (_, e) => { Events.Add("changed"); Changed.Add(e); };
            Session.ValidationFailed += (_, e) => { Events.Add("validation"); Reported.Add(e); };
        }
    }

    [Fact]
    public void ToggleBooleanCell_RaisesCellEndEditThenCellValueChanged()
    {
        var f = new ToggleFixture();
        f.Row.Active = true; // toggling to false passes the item's validation

        f.Session.ToggleBooleanCell(0, 1, f.Active, f.Source);

        Assert.False(f.Row.Active);
        Assert.Equal(new[] { "end", "changed" }, f.Events);
        Assert.True(Assert.Single(f.Ended).Committed);
        Assert.Equal(true, Assert.Single(f.Changed).OldValue);
        Assert.Equal(false, f.Changed[0].NewValue);
    }

    [Fact]
    public void ToggleBooleanCell_RefusedByTheValidator_LeavesTheValueAndRaisesNoValueChanged()
    {
        var f = new ToggleFixture();
        f.Session.CellValidator = (_, _, value) => value is true ? CellValidationResult.Error("Not now.") : CellValidationResult.Valid;

        f.Session.ToggleBooleanCell(0, 1, f.Active, f.Source);

        Assert.False(f.Row.Active);
        var reported = Assert.Single(f.Reported);
        Assert.False(reported.Committed);
        Assert.Equal("Not now.", reported.Result.ErrorMessage);
        Assert.Empty(f.Changed);
        Assert.False(Assert.Single(f.Ended).Committed);
    }

    [Fact]
    public void ToggleBooleanCell_RefusedByTheItem_RollsBackAndReports()
    {
        var f = new ToggleFixture();

        f.Session.ToggleBooleanCell(0, 1, f.Active, f.Source);

        Assert.False(f.Row.Active);
        Assert.Equal("Cannot be active.", Assert.Single(f.Reported).Result.ErrorMessage);
        Assert.Empty(f.Changed);
    }

    [Fact]
    public void ToggleBooleanCell_WhileAnEditIsRefused_DoesNotToggle()
    {
        var f = new ToggleFixture();
        f.Row.Active = true;
        f.Session.CellValidator = (_, col, _) => col == 0 ? CellValidationResult.Error("Name no.") : CellValidationResult.Valid;
        Assert.True(f.Session.BeginEdit(0, 0, f.Name, f.Source, Bounds, 'x'));

        f.Session.ToggleBooleanCell(0, 1, f.Active, f.Source);

        Assert.True(f.Row.Active);
        Assert.True(f.Session.IsEditing);
        Assert.Empty(f.Changed);
    }

    [Fact]
    public void TapOnAnotherCell_WithARefusedEdit_KeepsTheEditorAndDoesNotMoveTheSelection()
    {
        var g = InvalidEdit();
        var editor = g.Edit.ActiveEditor!;

        g.Tap(60, g.RowY(5));

        AssertEditorKept(g, editor, new CellPosition(2, 0));
    }
}

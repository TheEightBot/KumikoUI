using System.ComponentModel;
using KumikoUI.Core.Components;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.Editing;

/// <summary>
/// The edit session lifecycle on its own, without the input controller: which cells may begin an edit,
/// the cancelable begin event, validation on commit, cancel, change notifications, auto-commit from
/// editors, input forwarding and theming of new editors.
/// </summary>
public class EditSessionTests
{
    private static readonly KumikoRect Bounds = new(0, 0, 150, 36);

    private sealed class Row
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
        public string Locked { get; set; } = "fixed";
        public string Group { get; set; } = "G";
        public bool Active { get; set; }
    }

    private sealed class Fixture
    {
        public List<Row> Rows { get; } = new()
        {
            new Row { Name = "Alice", Age = 30 },
            new Row { Name = "Bob", Age = 25 },
        };

        public KumikoSource Source { get; } = new();
        public EditSession Session { get; } = new();
        public KumikoColumn Name { get; } = new() { Header = "Name", PropertyName = "Name", Width = 150 };
        public KumikoColumn Age { get; } = new() { Header = "Age", PropertyName = "Age", Width = 100, ColumnType = KumikoColumnType.Numeric };
        public KumikoColumn Locked { get; } = new() { Header = "Locked", PropertyName = "Locked", Width = 100, IsReadOnly = true };
        public KumikoColumn Active { get; } = new() { Header = "Active", PropertyName = "Active", Width = 60, ColumnType = KumikoColumnType.Boolean };
        public List<CellBeginEditEventArgs> Begun { get; } = new();
        public List<CellEndEditEventArgs> Ended { get; } = new();
        public List<CellValueChangedEventArgs> Changed { get; } = new();
        public int Redraws { get; private set; }

        public Fixture()
        {
            Source.SetColumns(new[] { Name, Age, Locked, Active });
            Source.SetItems(Rows);
            Session.CellBeginEdit += (_, e) => Begun.Add(e);
            Session.CellEndEdit += (_, e) => Ended.Add(e);
            Session.CellValueChanged += (_, e) => Changed.Add(e);
            Session.NeedsRedraw += (_, _) => Redraws++;
        }

        public bool Begin(int row, int column, char? initial = null) =>
            Session.BeginEdit(row, column, Source.Columns[column], Source, Bounds, initial);

        public DrawnTextBox Box => Assert.IsType<DrawnTextBox>(Session.ActiveEditor);
    }

    // ── BeginEdit ─────────────────────────────────────────────────

    [Fact]
    public void BeginEdit_OpensAnEditorOnTheCell()
    {
        var f = new Fixture();

        Assert.True(f.Begin(1, 0));

        Assert.True(f.Session.IsEditing);
        Assert.Equal(new CellPosition(1, 0), f.Session.EditCell);
        Assert.Same(f.Rows[1], f.Session.EditItem);
        Assert.Same(f.Name, f.Session.EditColumn);
        Assert.Equal("Bob", f.Box.Text);
        Assert.Same(f.Session.ActiveEditor, f.Session.ActiveTextInput);
        Assert.True(f.Session.IsCellBeingEdited(1, 0));
        Assert.False(f.Session.IsCellBeingEdited(0, 0));
        Assert.True(f.Box.IsFocused);
        Assert.True(f.Redraws > 0);
    }

    [Fact]
    public void BeginEdit_WithAnInitialCharacter_StartsTheTextWithIt()
    {
        var f = new Fixture();

        f.Begin(0, 0, 'Z');

        Assert.Equal("Z", f.Box.Text);
    }

    [Fact]
    public void BeginEdit_ReadOnlyColumn_IsRefusedWithoutAnEvent()
    {
        var f = new Fixture();

        Assert.False(f.Begin(0, 2));

        Assert.False(f.Session.IsEditing);
        Assert.Empty(f.Begun);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(100)]
    public void BeginEdit_RowOutOfRange_IsRefusedWithoutAnEvent(int row)
    {
        var f = new Fixture();

        Assert.False(f.Begin(row, 0));

        Assert.False(f.Session.IsEditing);
        Assert.Empty(f.Begun);
    }

    [Fact]
    public void BeginEdit_GroupHeaderRow_IsRefused_AndDataRowsStillWork()
    {
        var f = new Fixture();
        f.Source.AddGroupDescription(new GroupDescription("Group"));
        Assert.True(f.Source.IsGroupHeaderRow(0));

        Assert.False(f.Begin(0, 0));
        Assert.Empty(f.Begun);
        Assert.True(f.Begin(1, 0));
        Assert.Same(f.Rows[0], f.Session.EditItem);
    }

    [Fact]
    public void BeginEdit_GroupSummaryRow_IsRefused()
    {
        var f = new Fixture();
        f.Source.AddGroupDescription(new GroupDescription("Group"));
        f.Source.AddGroupSummaryRow(new GroupSummaryRow("Totals")
        {
            Columns = { new SummaryColumnDescription("Age", SummaryType.Sum) }
        });
        int summaryRow = Enumerable.Range(0, f.Source.RowCount).First(f.Source.IsGroupSummaryRow);

        Assert.False(f.Begin(summaryRow, 0));
    }

    [Theory]
    [InlineData(KumikoColumnType.Boolean)]
    [InlineData(KumikoColumnType.Image)]
    public void BeginEdit_ColumnTypeWithoutAnEditor_ReturnsFalseAndLeavesNoState(KumikoColumnType type)
    {
        var f = new Fixture();
        f.Name.ColumnType = type;

        Assert.False(f.Begin(0, 0));

        Assert.False(f.Session.IsEditing);
        Assert.Equal(CellPosition.Invalid, f.Session.EditCell);
        Assert.Null(f.Session.EditItem);
        Assert.Null(f.Session.EditColumn);
        Assert.Null(f.Session.ActiveEditor);
        Assert.Null(f.Session.ActiveTextInput);
    }

    [Fact]
    public void CellBeginEdit_ReportsTheCellAndItsValue()
    {
        var f = new Fixture();

        f.Begin(1, 1);

        var args = Assert.Single(f.Begun);
        Assert.Equal(1, args.RowIndex);
        Assert.Equal(1, args.ColumnIndex);
        Assert.Same(f.Age, args.Column);
        Assert.Equal(25, args.CellValue);
        Assert.False(args.Cancel);
    }

    [Fact]
    public void CellBeginEdit_Cancel_PreventsTheEdit()
    {
        var f = new Fixture();
        f.Session.CellBeginEdit += (_, e) => e.Cancel = true;

        Assert.False(f.Begin(0, 0));

        Assert.False(f.Session.IsEditing);
        Assert.Null(f.Session.ActiveEditor);
        Assert.Empty(f.Ended);
    }

    [Fact]
    public void CellBeginEdit_CancelOnOneColumnOnly_LetsOthersEdit()
    {
        var f = new Fixture();
        f.Session.CellBeginEdit += (_, e) => e.Cancel = e.Column.PropertyName == "Age";

        Assert.False(f.Begin(0, 1));
        Assert.True(f.Begin(0, 0));
    }

    [Fact]
    public void BeginEdit_WhileEditing_CommitsThePreviousEditFirst()
    {
        var f = new Fixture();
        f.Begin(0, 0);
        f.Box.Text = "Alicia";

        Assert.True(f.Begin(1, 0));

        Assert.Equal("Alicia", f.Rows[0].Name);
        Assert.Same(f.Rows[1], f.Session.EditItem);
        Assert.Contains(f.Ended, e => e.RowIndex == 0 && e.Committed);
    }

    [Fact]
    public void BeginEdit_WhileEditingAnInvalidValue_KeepsTheFirstEditorWhenRefusedElsewhere()
    {
        var f = new Fixture();
        f.Session.CellValidator = (_, _, _) => CellValidationResult.Error("no");
        f.Begin(0, 0);
        f.Box.Text = "Changed";

        // The previous edit cannot commit, but a read-only target is refused outright.
        Assert.False(f.Begin(0, 2));

        Assert.True(f.Session.IsEditing);
        Assert.Same(f.Rows[0], f.Session.EditItem);
    }

    // ── CommitEdit ────────────────────────────────────────────────

    [Fact]
    public void CommitEdit_WithoutAnEdit_ReturnsFalse()
    {
        var f = new Fixture();

        Assert.False(f.Session.CommitEdit(f.Source));
        Assert.Empty(f.Ended);
    }

    [Fact]
    public void CommitEdit_WritesTheValue_AndRaisesTheEvents()
    {
        var f = new Fixture();
        f.Begin(0, 1);
        f.Box.Text = "31";

        Assert.True(f.Session.CommitEdit(f.Source));

        Assert.Equal(31, f.Rows[0].Age);
        Assert.False(f.Session.IsEditing);
        Assert.Null(f.Session.ActiveEditor);

        var changed = Assert.Single(f.Changed);
        Assert.Equal(0, changed.RowIndex);
        Assert.Equal(1, changed.ColumnIndex);
        Assert.Same(f.Age, changed.Column);
        Assert.Equal(30, changed.OldValue);
        Assert.Equal(31, changed.NewValue);

        var ended = Assert.Single(f.Ended);
        Assert.True(ended.Committed);
        Assert.Equal(30, ended.OldValue);
        Assert.Equal(31, ended.NewValue);
        Assert.Equal(0, ended.RowIndex);
        Assert.Equal(1, ended.ColumnIndex);
    }

    [Fact]
    public void CommitEdit_EndEditIsRaisedBeforeValueChanged()
    {
        var f = new Fixture();
        var order = new List<string>();
        f.Session.CellValueChanged += (_, _) => order.Add("changed");
        f.Session.CellEndEdit += (_, _) => order.Add("end");
        f.Begin(0, 0);
        f.Box.Text = "Zed";

        f.Session.CommitEdit(f.Source);

        Assert.Equal(new[] { "end", "changed" }, order);
    }

    [Fact]
    public void CommitEdit_UnchangedValue_RaisesEndEditButNotValueChanged()
    {
        var f = new Fixture();
        f.Begin(0, 0);

        Assert.True(f.Session.CommitEdit(f.Source));

        Assert.Empty(f.Changed);
        var ended = Assert.Single(f.Ended);
        Assert.True(ended.Committed);
        Assert.Equal("Alice", ended.NewValue);
        Assert.Equal("Alice", f.Rows[0].Name);
    }

    [Fact]
    public void CommitEdit_ReportsTheSourceViewValueChange()
    {
        var f = new Fixture();
        var views = new List<KumikoViewChangedEventArgs>();
        f.Source.ViewChanged += (_, e) => views.Add(e);
        f.Begin(1, 0);
        f.Box.Text = "Bobby";

        f.Session.CommitEdit(f.Source);

        Assert.NotEmpty(views);
        Assert.Equal("Bobby", f.Source.GetCellValue(1, f.Name));
    }

    // ── Validation ────────────────────────────────────────────────

    [Fact]
    public void CommitEdit_InvalidValue_KeepsTheEditorOpenAndTheItemUnchanged()
    {
        var f = new Fixture();
        f.Session.CellValidator = (_, _, v) => (v is int n && n < 0) ? CellValidationResult.Error("negative") : CellValidationResult.Valid;
        f.Begin(0, 1);
        f.Box.Text = "-5";

        Assert.False(f.Session.CommitEdit(f.Source));

        Assert.True(f.Session.IsEditing);
        Assert.Equal(30, f.Rows[0].Age);
        Assert.NotNull(f.Session.CurrentValidation);
        Assert.False(f.Session.CurrentValidation!.IsValid);
        Assert.Equal("negative", f.Session.CurrentValidation.ErrorMessage);
        Assert.Equal(ValidationSeverity.Error, f.Session.CurrentValidation.Severity);
        Assert.Empty(f.Changed);
        Assert.Empty(f.Ended);
    }

    [Fact]
    public void CommitEdit_ValidatorReceivesTheRowColumnAndNewValue()
    {
        var f = new Fixture();
        (int Row, int Column, object? Value)? seen = null;
        f.Session.CellValidator = (r, c, v) => { seen = (r, c, v); return CellValidationResult.Valid; };
        f.Begin(1, 1);
        f.Box.Text = "44";

        f.Session.CommitEdit(f.Source);

        Assert.Equal((1, 1, (object?)44), seen);
    }

    [Fact]
    public void CommitEdit_AfterFixingTheValue_Commits()
    {
        var f = new Fixture();
        f.Session.CellValidator = (_, _, v) => v is int n && n < 0 ? CellValidationResult.Error("negative") : CellValidationResult.Valid;
        f.Begin(0, 1);
        f.Box.Text = "-5";
        f.Session.CommitEdit(f.Source);

        f.Box.Text = "5";

        Assert.True(f.Session.CommitEdit(f.Source));
        Assert.Equal(5, f.Rows[0].Age);
        Assert.Null(f.Session.CurrentValidation);
    }

    [Fact]
    public void CommitEdit_WarningResult_IsShownOnce_ThenCommittingTheSameValueAcceptsIt()
    {
        var f = new Fixture();
        f.Session.CellValidator = (_, _, _) => CellValidationResult.Warning("unusual");
        var reported = new List<CellValidationEventArgs>();
        f.Session.ValidationFailed += (_, e) => reported.Add(e);
        f.Begin(0, 0);
        f.Box.Text = "Odd";

        Assert.False(f.Session.CommitEdit(f.Source)); // shown, not yet written
        Assert.Equal("unusual", f.Session.CurrentValidation!.ErrorMessage);
        Assert.NotEqual("Odd", f.Rows[0].Name);

        Assert.True(f.Session.CommitEdit(f.Source)); // the same value again: accepted
        Assert.Equal("Odd", f.Rows[0].Name);
        Assert.Null(f.Session.CurrentValidation);
        Assert.Equal(new[] { false, true }, reported.Select(e => e.Committed));
        Assert.All(reported, e => Assert.Equal(ValidationSeverity.Warning, e.Severity));
    }

    [Fact]
    public void CommitEdit_InvalidResultWithWarningSeverity_StillBlocksTheCommit()
    {
        var f = new Fixture();
        f.Session.CellValidator = (_, _, _) => new CellValidationResult
        {
            IsValid = false, ErrorMessage = "careful", Severity = ValidationSeverity.Warning
        };
        f.Begin(0, 0);
        f.Box.Text = "Odd";

        Assert.False(f.Session.CommitEdit(f.Source));

        Assert.Equal(ValidationSeverity.Warning, f.Session.CurrentValidation!.Severity);
        Assert.Equal("Alice", f.Rows[0].Name);
    }

    [Theory]
    [InlineData(ValidationSeverity.Error, 220, 50, 50)]
    [InlineData(ValidationSeverity.Warning, 255, 165, 0)]
    public void DrawEditor_OutlinesAnInvalidEditorInTheSeverityColor(ValidationSeverity severity, byte r, byte g, byte b)
    {
        var f = new Fixture();
        f.Session.CellValidator = (_, _, _) => new CellValidationResult { IsValid = false, Severity = severity };
        f.Begin(0, 0);
        f.Box.Text = "Changed";
        f.Session.CommitEdit(f.Source);
        var ctx = new RecordingDrawingContext();

        f.Session.DrawEditor(ctx);

        var outline = ctx.Ops.Last(o => o.Kind == DrawOpKind.DrawRect && Math.Abs(o.StrokeWidth - 2f) < 0.01f);
        Assert.Equal(new KumikoColor(r, g, b), outline.Color);
    }

    [Fact]
    public void DrawEditor_ValidEditor_HasNoSeverityOutline()
    {
        var f = new Fixture();
        f.Begin(0, 0);
        var ctx = new RecordingDrawingContext();

        f.Session.DrawEditor(ctx);

        Assert.DoesNotContain(ctx.Ops, o => o.Kind == DrawOpKind.DrawRect &&
            (o.Color == new KumikoColor(220, 50, 50) || o.Color == new KumikoColor(255, 165, 0)));
        Assert.NotEmpty(ctx.Fills);
    }

    [Fact]
    public void DrawEditor_WithoutAnEdit_OrWhenHidden_DrawsNothing()
    {
        var f = new Fixture();
        var idle = new RecordingDrawingContext();
        f.Session.DrawEditor(idle);
        Assert.Empty(idle.Ops);

        f.Begin(0, 0);
        f.Box.IsVisible = false;
        var hidden = new RecordingDrawingContext();
        f.Session.DrawEditor(hidden);
        Assert.Empty(hidden.Ops);
    }

    [Fact]
    public void DrawEditor_UsesTheStyleBackgroundBehindTheEditor()
    {
        var f = new Fixture();
        f.Session.Style = new KumikoStyle { BackgroundColor = new KumikoColor(1, 2, 3) };
        f.Begin(0, 0);
        var ctx = new RecordingDrawingContext();

        f.Session.DrawEditor(ctx);

        Assert.Contains(ctx.Fills, o => o.Color == new KumikoColor(1, 2, 3) && o.Rect.Equals(f.Box.Bounds));
    }

    private sealed class ErrorInfoRow : IDataErrorInfo
    {
        public string Name { get; set; } = "ok";
        public string Error => "";
        public string this[string columnName] => columnName == nameof(Name) && Name.Length > 3 ? "too long" : "";
    }

    [Fact]
    public void CommitEdit_ReportsIDataErrorInfoMessages()
    {
        var row = new ErrorInfoRow();
        var source = new KumikoSource();
        var column = new KumikoColumn { PropertyName = "Name" };
        source.SetColumns(new[] { column });
        source.SetItems(new[] { row });
        var session = new EditSession();
        session.BeginEdit(0, 0, column, source, Bounds);
        ((DrawnTextBox)session.ActiveEditor!).Text = "toolong";

        // The item validates itself with the new value in place; the error rolls the write back
        Assert.False(session.CommitEdit(source));
        Assert.Equal("too long", session.CurrentValidation!.ErrorMessage);
        Assert.True(session.IsEditing);
        Assert.Equal("ok", row.Name);

        ((DrawnTextBox)session.ActiveEditor!).Text = "yes";
        Assert.Null(session.CurrentValidation); // a changed value clears the message
        Assert.True(session.CommitEdit(source));
        Assert.Equal("yes", row.Name);
    }

    private sealed class NotifyErrorRow : INotifyDataErrorInfo
    {
        public string Name { get; set; } = "x";
        public bool HasErrors { get; set; } = true;
        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged { add { } remove { } }
        public System.Collections.IEnumerable GetErrors(string? propertyName) =>
            propertyName == nameof(Name) ? new object?[] { null, "bad name" } : Array.Empty<object>();
    }

    [Fact]
    public void CommitEdit_ReportsINotifyDataErrorInfoMessages_SkippingNullErrors()
    {
        var row = new NotifyErrorRow();
        var source = new KumikoSource();
        var column = new KumikoColumn { PropertyName = "Name" };
        source.SetColumns(new[] { column });
        source.SetItems(new[] { row });
        var session = new EditSession();
        session.BeginEdit(0, 0, column, source, Bounds);
        ((DrawnTextBox)session.ActiveEditor!).Text = "y";

        Assert.False(session.CommitEdit(source));

        Assert.Equal("bad name", session.CurrentValidation!.ErrorMessage);
    }

    [Fact]
    public void CommitEdit_ItemWithoutErrors_IsValid()
    {
        var row = new NotifyErrorRow { HasErrors = false };
        var source = new KumikoSource();
        var column = new KumikoColumn { PropertyName = "Name" };
        source.SetColumns(new[] { column });
        source.SetItems(new[] { row });
        var session = new EditSession();
        session.BeginEdit(0, 0, column, source, Bounds);

        Assert.True(session.CommitEdit(source));
    }

    [Fact]
    public void CellValidator_Passing_DoesNotSkipTheItemsOwnErrors()
    {
        var row = new NotifyErrorRow();
        var source = new KumikoSource();
        var column = new KumikoColumn { PropertyName = "Name" };
        source.SetColumns(new[] { column });
        source.SetItems(new[] { row });
        var session = new EditSession { CellValidator = (_, _, _) => CellValidationResult.Valid };
        session.BeginEdit(0, 0, column, source, Bounds);
        ((DrawnTextBox)session.ActiveEditor!).Text = "y";

        Assert.False(session.CommitEdit(source));
        Assert.Equal("bad name", session.CurrentValidation!.ErrorMessage);
    }

    // ── CancelEdit ────────────────────────────────────────────────

    [Fact]
    public void CancelEdit_RestoresTheOriginalValue_AndRaisesEndEditNotCommitted()
    {
        var f = new Fixture();
        f.Begin(0, 0);
        f.Box.Text = "Changed";

        f.Session.CancelEdit();

        Assert.False(f.Session.IsEditing);
        Assert.Equal("Alice", f.Rows[0].Name);
        Assert.Empty(f.Changed);
        var ended = Assert.Single(f.Ended);
        Assert.False(ended.Committed);
        Assert.Equal("Alice", ended.OldValue);
        Assert.Equal("Alice", ended.NewValue);
        Assert.Equal(0, ended.RowIndex);
        Assert.Equal(0, ended.ColumnIndex);
        Assert.Same(f.Name, ended.Column);
    }

    [Fact]
    public void CancelEdit_WithoutAnEdit_DoesNothing()
    {
        var f = new Fixture();

        f.Session.CancelEdit();

        Assert.Empty(f.Ended);
    }

    [Fact]
    public void CancelEdit_AfterAFailedCommit_DiscardsTheValidation()
    {
        var f = new Fixture();
        f.Session.CellValidator = (_, _, _) => CellValidationResult.Error("no");
        f.Begin(0, 0);
        f.Box.Text = "bad";
        f.Session.CommitEdit(f.Source);

        f.Session.CancelEdit();

        Assert.Null(f.Session.CurrentValidation);
        Assert.Equal("Alice", f.Rows[0].Name);
        Assert.False(Assert.Single(f.Ended).Committed);
    }

    [Fact]
    public void AfterCancel_ANewEditStartsFromTheOriginalValue()
    {
        var f = new Fixture();
        f.Begin(0, 0);
        f.Box.Text = "Changed";
        f.Session.CancelEdit();

        f.Begin(0, 0);

        Assert.Equal("Alice", f.Box.Text);
    }

    // ── Editor completion, input forwarding ───────────────────────

    // A drawn editor that can finish an edit on demand, as a combo box does when an item is picked.
    private sealed class CompletingCombo : DrawnComponent, IValueEditor
    {
        public object? Value { get; private set; }
        public bool HasValue => Value != null;
        public bool IsDirty => Value != null;
        public void Pick(string item) => Value = item;
        public override void OnDraw(IDrawingContext ctx) { }
        public void Complete() => RaiseEditCompleted();
    }

    [Fact]
    public void EditCompleted_FromTheEditor_CommitsTheEdit()
    {
        var f = new Fixture();
        var combo = new CompletingCombo { IsVisible = true };
        f.Name.ColumnType = KumikoColumnType.Template;
        f.Name.CustomEditorFactory = (_, _) => combo;
        f.Begin(0, 0);
        combo.Pick("Yan");

        combo.Complete();

        Assert.False(f.Session.IsEditing);
        Assert.Equal("Yan", f.Rows[0].Name);
        Assert.True(Assert.Single(f.Ended).Committed);
    }

    [Fact]
    public void EditCompleted_AfterTheEditEnded_IsIgnored()
    {
        var f = new Fixture();
        var combo = new CompletingCombo { IsVisible = true };
        f.Name.ColumnType = KumikoColumnType.Template;
        f.Name.CustomEditorFactory = (_, _) => combo;
        f.Begin(0, 0);
        f.Session.CancelEdit();

        combo.Complete();

        Assert.Single(f.Ended);
    }

    private static (float X, float Y) PickerItemPoint(DrawnScrollPicker picker, int itemsBelowCenter) =>
        (picker.Bounds.X + picker.Bounds.Width / 2,
         picker.Bounds.Y + picker.Bounds.Height / 2 + itemsBelowCenter * picker.ItemHeight);

    private static void PickerTap(Fixture f, (float X, float Y) point)
    {
        f.Session.HandlePointerEvent(new KumikoPointerEventArgs { X = point.X, Y = point.Y, Action = InputAction.Pressed });
        f.Session.HandlePointerEvent(new KumikoPointerEventArgs { X = point.X, Y = point.Y, Action = InputAction.Released });
    }

    [Fact]
    public void ScrollPickerEditor_ATapOnTheCenteredItem_CompletesTheEdit()
    {
        var f = new Fixture();
        f.Name.ColumnType = KumikoColumnType.Picker;
        f.Name.EditorItemsString = "Alice,Carol,Dave";
        f.Begin(0, 0);
        var picker = Assert.IsType<DrawnScrollPicker>(f.Session.ActiveEditor);
        picker.SelectedIndex = 2;

        PickerTap(f, PickerItemPoint(picker, 0));

        Assert.False(f.Session.IsEditing);
        Assert.Equal("Dave", f.Rows[0].Name);
    }

    [Fact]
    public void ScrollPickerEditor_ATapOnAnotherItem_CommitsItOnlyOnceTheFrameLoopSettlesTheWheel()
    {
        var f = new Fixture();
        f.Name.ColumnType = KumikoColumnType.Picker;
        f.Name.EditorItemsString = "Alice,Carol,Dave";
        f.Begin(0, 0); // "Alice": the first item is centered
        var picker = Assert.IsType<DrawnScrollPicker>(f.Session.ActiveEditor);

        PickerTap(f, PickerItemPoint(picker, 1));

        Assert.True(f.Session.IsEditing);
        Assert.True(f.Session.IsAnimating);
        Assert.Equal("Alice", f.Rows[0].Name);

        int frames = 0;
        while (f.Session.UpdateAnimation(16f) && frames < 1000) frames++;

        Assert.False(f.Session.IsEditing);
        Assert.False(f.Session.IsAnimating);
        Assert.False(f.Session.UpdateAnimation(16f));
        Assert.Equal("Carol", f.Rows[0].Name);
    }

    [Fact]
    public void ScrollPickerEditor_UsesTheColumnsDisplayAndValueSelectors()
    {
        var f = new Fixture();
        f.Name.ColumnType = KumikoColumnType.Picker;
        f.Name.EditorItems = new[] { 1, 2, 3 };
        f.Name.EditorDisplaySelector = o => $"#{o}";
        f.Name.EditorValueSelector = o => $"Value {o}";
        f.Rows[0].Name = "Value 2";
        f.Begin(0, 0);
        var picker = Assert.IsType<DrawnScrollPicker>(f.Session.ActiveEditor);

        Assert.Equal(new[] { "#1", "#2", "#3" }, picker.Items);
        Assert.Equal(1, picker.SelectedIndex);

        picker.SelectedIndex = 2;
        Assert.True(f.Session.CommitEdit(f.Source));
        Assert.Equal("Value 3", f.Rows[0].Name);
    }

    [Fact]
    public void UpdateAnimation_WithoutAnAnimatingEditor_DoesNothing()
    {
        var f = new Fixture();
        Assert.False(f.Session.IsAnimating);
        Assert.False(f.Session.UpdateAnimation(16f));

        f.Begin(0, 0);
        Assert.False(f.Session.IsAnimating);
        Assert.False(f.Session.UpdateAnimation(16f));
    }

    [Fact]
    public void ComboBoxEditor_IsFilterableFromTheColumn()
    {
        var f = new Fixture();
        f.Name.ColumnType = KumikoColumnType.ComboBox;
        f.Name.EditorItemsString = "Alice,Bob";
        f.Begin(0, 0);
        Assert.False(Assert.IsType<DrawnComboBox>(f.Session.ActiveEditor).IsFilterable);
        f.Session.CancelEdit();

        f.Name.EditorIsFilterable = true;
        f.Begin(0, 0);

        Assert.True(Assert.IsType<DrawnComboBox>(f.Session.ActiveEditor).IsFilterable);
    }

    [Fact]
    public void ComboBoxEditor_OpenedByTyping_JumpsToTheItemStartingWithTheCharacter()
    {
        var f = new Fixture();
        f.Name.ColumnType = KumikoColumnType.ComboBox;
        f.Name.EditorItemsString = "Alice,Bob,Carol";
        f.Begin(0, 0, 'c');

        var combo = Assert.IsType<DrawnComboBox>(f.Session.ActiveEditor);
        Assert.Equal("Carol", combo.SelectedText);
        Assert.True(combo.IsDropdownOpen);
        Assert.True(f.Session.CommitEdit(f.Source));
        Assert.Equal("Carol", f.Rows[0].Name);
    }

    [Fact]
    public void HandlePointerEvent_APressTheEditorTook_CapturesTheMovesAndTheRelease()
    {
        var f = new Fixture();
        f.Name.ColumnType = KumikoColumnType.ComboBox;
        f.Name.EditorItemsString = string.Join(",", Enumerable.Range(0, 50).Select(i => $"Item {i:D2}"));
        f.Begin(0, 0);
        var combo = Assert.IsType<DrawnComboBox>(f.Session.ActiveEditor);
        var drop = combo.GetDropdownBounds();
        float x = drop.X + 20, y = drop.Bottom - 10;

        Assert.True(f.Session.HandlePointerEvent(new KumikoPointerEventArgs { X = x, Y = y, Action = InputAction.Pressed }));
        // Far outside the editor, yet the drag still belongs to it
        Assert.True(f.Session.HandlePointerEvent(new KumikoPointerEventArgs { X = x, Y = y - 5000, Action = InputAction.Moved }));
        Assert.True(f.Session.HandlePointerEvent(new KumikoPointerEventArgs { X = x, Y = y - 5000, Action = InputAction.Released }));

        Assert.Equal(50 * combo.ItemHeight - drop.Height, combo.DropdownScrollOffset);
        Assert.True(f.Session.IsEditing);

        // The capture ended with the release: a move outside is no longer the editor's
        Assert.False(f.Session.HandlePointerEvent(new KumikoPointerEventArgs { X = x, Y = y - 5000, Action = InputAction.Moved }));
    }

    [Fact]
    public void HandleKeyEvent_WithoutAnEdit_IsNotHandled()
    {
        var f = new Fixture();

        Assert.False(f.Session.HandleKeyEvent(new KumikoKeyEventArgs { Key = KumikoKey.Character, Character = 'a' }));
    }

    [Fact]
    public void HandleKeyEvent_ForwardsTypingToTheEditor()
    {
        var f = new Fixture();
        f.Begin(0, 0, 'A');

        bool handled = f.Session.HandleKeyEvent(new KumikoKeyEventArgs { Key = KumikoKey.Character, Character = 'b' });

        Assert.True(handled);
        Assert.Equal("Ab", f.Box.Text);
    }

    [Fact]
    public void HandlePointerEvent_WithoutAnEdit_IsNotHandled()
    {
        var f = new Fixture();

        Assert.False(f.Session.HandlePointerEvent(
            new KumikoPointerEventArgs { X = 5, Y = 5, Action = InputAction.Pressed }));
    }

    [Fact]
    public void HandlePointerEvent_OutsideTheEditor_IsNotHandled()
    {
        var f = new Fixture();
        f.Begin(0, 0);

        Assert.False(f.Session.HandlePointerEvent(
            new KumikoPointerEventArgs { X = 900, Y = 900, Action = InputAction.Pressed }));
    }

    [Fact]
    public void HandlePointerEvent_InsideTheEditor_GoesToTheEditor()
    {
        var f = new Fixture();
        f.Begin(0, 0);
        f.Box.SetMeasureContext(new RecordingDrawingContext());

        bool handled = f.Session.HandlePointerEvent(
            new KumikoPointerEventArgs { X = 20, Y = 10, Action = InputAction.Pressed });

        Assert.True(handled);
        Assert.True(f.Box.IsFocused);
    }

    // ── Bounds and placement ──────────────────────────────────────

    [Fact]
    public void UpdateEditorBounds_MovesTheEditor_AndIsHarmlessWithoutOne()
    {
        var f = new Fixture();
        f.Session.UpdateEditorBounds(new KumikoRect(1, 2, 3, 4)); // no editor: no-op

        f.Begin(0, 0);
        f.Session.UpdateEditorBounds(new KumikoRect(5, 6, 70, 20));

        Assert.Equal(new KumikoRect(5, 6, 70, 20), f.Box.Bounds);
    }

    // ── Theming ───────────────────────────────────────────────────

    [Fact]
    public void Style_ThemesNewEditors()
    {
        var f = new Fixture();
        f.Session.Style = new KumikoStyle
        {
            CellTextColor = new KumikoColor(11, 22, 33),
            BackgroundColor = new KumikoColor(44, 55, 66),
        };

        f.Begin(0, 0);

        Assert.Equal(new KumikoColor(11, 22, 33), f.Box.TextColor);
        Assert.Equal(new KumikoColor(44, 55, 66), f.Box.BackgroundColor);
    }

    [Fact]
    public void WithoutAStyle_EditorsKeepTheirDefaultColors()
    {
        var f = new Fixture();

        f.Begin(0, 0);

        Assert.Equal(new DrawnTextBox().TextColor, f.Box.TextColor);
    }

    // ── Boolean toggle ────────────────────────────────────────────

    [Fact]
    public void ToggleBooleanCell_FlipsTheValue_AndRaisesTheEvents()
    {
        var f = new Fixture();

        f.Session.ToggleBooleanCell(0, 3, f.Active, f.Source);

        Assert.True(f.Rows[0].Active);
        var begin = Assert.Single(f.Begun);
        Assert.Equal(false, begin.CellValue);
        var changed = Assert.Single(f.Changed);
        Assert.Equal(false, changed.OldValue);
        Assert.Equal(true, changed.NewValue);
        var ended = Assert.Single(f.Ended);
        Assert.True(ended.Committed);
        Assert.Equal(3, ended.ColumnIndex);
        Assert.True(f.Redraws > 0);
    }

    [Fact]
    public void ToggleBooleanCell_CanceledBeginEdit_LeavesTheValue()
    {
        var f = new Fixture();
        f.Session.CellBeginEdit += (_, e) => e.Cancel = true;

        f.Session.ToggleBooleanCell(0, 3, f.Active, f.Source);

        Assert.False(f.Rows[0].Active);
        Assert.Empty(f.Changed);
        Assert.Empty(f.Ended);
    }

    [Fact]
    public void ToggleBooleanCell_ReadOnlyColumnAndBadRows_AreIgnored()
    {
        var f = new Fixture();
        f.Active.IsReadOnly = true;
        f.Session.ToggleBooleanCell(0, 3, f.Active, f.Source);
        Assert.False(f.Rows[0].Active);

        f.Active.IsReadOnly = false;
        f.Session.ToggleBooleanCell(-1, 3, f.Active, f.Source);
        f.Session.ToggleBooleanCell(2, 3, f.Active, f.Source);
        Assert.Empty(f.Begun);
    }

    [Fact]
    public void ToggleBooleanCell_GroupHeaderRow_IsIgnored()
    {
        var f = new Fixture();
        f.Source.AddGroupDescription(new GroupDescription("Group"));

        f.Session.ToggleBooleanCell(0, 3, f.Active, f.Source);

        Assert.Empty(f.Begun);
    }

    // ── Immediately-activating editors ────────────────────────────

    [Fact]
    public void TryForwardInitialTap_FiresAnActivatesImmediatelyEditorOnTheFirstTap()
    {
        var f = new Fixture();
        int fired = 0;
        f.Name.ColumnType = KumikoColumnType.Template;
        f.Name.PropertyName = "";
        f.Name.CustomEditorFactory = (value, bounds) => new DrawnActionButtons
        {
            Bounds = bounds,
            IsVisible = true,
            RowItem = value,
            Buttons = new List<IActionButtonDefinition>
            {
                new ActionButtonDefinition { Label = "Go", Action = () => fired++ }
            }
        };
        Assert.True(f.Begin(0, 0));
        Assert.True(f.Session.ActiveEditor!.ActivatesImmediately);

        f.Session.TryForwardInitialTap(new KumikoPointerEventArgs { X = 75, Y = 18, Action = InputAction.Released });

        Assert.Equal(1, fired);
        Assert.False(f.Session.IsEditing); // the editor completed, which commits and closes
    }

    [Fact]
    public void TryForwardInitialTap_IgnoresOrdinaryEditorsAndMissingEdits()
    {
        var f = new Fixture();
        var tap = new KumikoPointerEventArgs { X = 20, Y = 10, Action = InputAction.Released };

        f.Session.TryForwardInitialTap(tap); // not editing

        f.Begin(0, 0);
        f.Session.TryForwardInitialTap(tap); // a text box does not activate immediately
        Assert.True(f.Session.IsEditing);
    }

    [Fact]
    public void TryForwardInitialTap_OutsideTheEditor_DoesNothing()
    {
        var f = new Fixture();
        int fired = 0;
        f.Name.ColumnType = KumikoColumnType.Template;
        f.Name.PropertyName = "";
        f.Name.CustomEditorFactory = (value, bounds) => new DrawnActionButtons
        {
            Bounds = bounds,
            IsVisible = true,
            Buttons = new List<IActionButtonDefinition>
            {
                new ActionButtonDefinition { Label = "Go", Action = () => fired++ }
            }
        };
        f.Begin(0, 0);

        f.Session.TryForwardInitialTap(new KumikoPointerEventArgs { X = 900, Y = 900, Action = InputAction.Released });

        Assert.Equal(0, fired);
        Assert.True(f.Session.IsEditing);
    }

    // ── Argument checks ───────────────────────────────────────────

    [Fact]
    public void ApplyViewChange_NullArguments_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new EditSession().ApplyViewChange(null!));
    }

    [Fact]
    public void EditSessionDefaults()
    {
        var session = new EditSession();

        Assert.Equal(EditTrigger.Default, session.EditTriggers);
        Assert.Equal(EditTextSelectionMode.SelectAll, session.TextSelectionMode);
        Assert.True(session.DismissKeyboardOnEnter);
        Assert.Null(session.CellValidator);
        Assert.Null(session.CurrentValidation);
        Assert.Null(session.Style);
        Assert.False(session.IsEditing);
        Assert.Equal(CellPosition.Invalid, session.EditCell);
    }
}

using System.Collections.ObjectModel;
using KumikoUI.Core.Components;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.Editing;

/// <summary>
/// How the session settings take effect through the input controller: edit triggers (grid and column),
/// the text selection mode of new editors, Enter, Tab and Escape while editing, and the editor following
/// its cell when the grid is rendered.
/// </summary>
public class EditSessionInputTests
{
    private sealed class Row
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
        public string City { get; set; } = "";
    }

    private sealed class Grid
    {
        public ObservableCollection<Row> Rows { get; } = new()
        {
            new Row { Name = "Alice", Age = 30, City = "Oslo" },
            new Row { Name = "Bob", Age = 25, City = "Rome" },
            new Row { Name = "Carol", Age = 41, City = "Lima" },
        };

        public KumikoSource Source { get; } = new();
        public KumikoStyle Style { get; } = new();
        public ScrollState Scroll { get; } = new() { ViewportWidth = 400, ViewportHeight = 200 };
        public KumikoSelectionModel Selection { get; }
        public EditSession Edit { get; } = new();
        public KumikoInputController Input { get; } = new();
        public KumikoRenderer Renderer { get; } = new();
        public KumikoColumn Name { get; } = new() { Header = "Name", PropertyName = "Name", Width = 150 };
        public KumikoColumn Age { get; } = new() { Header = "Age", PropertyName = "Age", Width = 100, ColumnType = KumikoColumnType.Numeric };
        public KumikoColumn City { get; } = new() { Header = "City", PropertyName = "City", Width = 150 };

        public Grid()
        {
            Selection = new KumikoSelectionModel(Source);
            Source.SetColumns(new[] { Name, Age, City });
            Source.SetItems(Rows);
            Input.EditSession = Edit;
            Render();
        }

        public RecordingDrawingContext Render()
        {
            var ctx = new RecordingDrawingContext();
            Renderer.Render(ctx, Source, Scroll, Selection, Style, editSession: Edit);
            return ctx;
        }

        public void Key(KumikoKey key, char? character = null, InputModifiers modifiers = InputModifiers.None) =>
            Input.HandleKey(new KumikoKeyEventArgs { Key = key, Character = character, Modifiers = modifiers },
                Scroll, Selection, Style, Source);

        public void Tap(float x, float y, int clicks = 1)
        {
            Input.HandlePointer(new KumikoPointerEventArgs { X = x, Y = y, Action = InputAction.Pressed, ClickCount = clicks },
                Scroll, Selection, Style, Source);
            Input.HandlePointer(new KumikoPointerEventArgs { X = x, Y = y, Action = InputAction.Released, ClickCount = clicks },
                Scroll, Selection, Style, Source);
        }

        /// <summary>Taps the center of a cell in the first three columns (header 40 px, rows 36 px).</summary>
        public void TapCell(int row, int column, int clicks = 1)
        {
            float x = column switch { 0 => 75, 1 => 200, _ => 325 };
            Tap(x, Style.HeaderHeight + row * Style.RowHeight + Style.RowHeight / 2, clicks);
        }

        public void Select(int row, int column) => Selection.CurrentCell = new CellPosition(row, column);

        public DrawnTextBox Box => Assert.IsType<DrawnTextBox>(Edit.ActiveEditor);
    }

    // ── EditTriggers ──────────────────────────────────────────────

    [Fact]
    public void DefaultTriggers_DoubleTapOpensTheEditor_SingleTapDoesNot()
    {
        var g = new Grid();

        g.TapCell(0, 0);
        Assert.False(g.Edit.IsEditing);

        g.TapCell(0, 0, clicks: 2);
        Assert.True(g.Edit.IsEditing);
        Assert.Equal(new CellPosition(0, 0), g.Edit.EditCell);
    }

    [Fact]
    public void SingleTapTrigger_OpensTheEditorOnOneTap()
    {
        var g = new Grid();
        g.Edit.EditTriggers = EditTrigger.SingleTap;

        g.TapCell(1, 0);

        Assert.True(g.Edit.IsEditing);
        Assert.Equal(new CellPosition(1, 0), g.Edit.EditCell);
    }

    [Fact]
    public void NoneTrigger_NothingOpensTheEditor()
    {
        var g = new Grid();
        g.Edit.EditTriggers = EditTrigger.None;

        g.TapCell(0, 0, clicks: 2);
        g.Select(0, 0);
        g.Key(KumikoKey.F2);
        g.Key(KumikoKey.Character, 'x');

        Assert.False(g.Edit.IsEditing);
    }

    [Fact]
    public void F2Trigger_OpensTheEditorWithTheCurrentValue()
    {
        var g = new Grid();
        g.Edit.EditTriggers = EditTrigger.F2Key;
        g.Select(1, 0);

        g.Key(KumikoKey.F2);

        Assert.True(g.Edit.IsEditing);
        Assert.Equal("Bob", g.Box.Text);
    }

    [Fact]
    public void F2Key_WithoutTheF2Trigger_DoesNothing()
    {
        var g = new Grid();
        g.Edit.EditTriggers = EditTrigger.DoubleTap | EditTrigger.Typing;
        g.Select(0, 0);

        g.Key(KumikoKey.F2);

        Assert.False(g.Edit.IsEditing);
    }

    [Fact]
    public void TypingTrigger_StartsTheEditorWithTheTypedCharacter()
    {
        var g = new Grid();
        g.Select(0, 0);

        g.Key(KumikoKey.Character, 'Q');

        Assert.True(g.Edit.IsEditing);
        Assert.Equal("Q", g.Box.Text);
    }

    [Fact]
    public void TypingTrigger_NonDigitInANumericColumn_KeepsTheCurrentValue()
    {
        var g = new Grid();
        g.Select(0, 1);

        g.Key(KumikoKey.Character, 'q');

        Assert.True(g.Edit.IsEditing);
        Assert.Equal("30", g.Box.Text);
        Assert.True(g.Box.NumericOnly);
    }

    [Fact]
    public void TypingTrigger_DigitInANumericColumn_ReplacesTheValue()
    {
        var g = new Grid();
        g.Select(0, 1);

        g.Key(KumikoKey.Character, '7');

        Assert.Equal("7", g.Box.Text);
    }

    [Fact]
    public void TypingTrigger_WithoutIt_TypingDoesNotEdit()
    {
        var g = new Grid();
        g.Edit.EditTriggers = EditTrigger.DoubleTap;
        g.Select(0, 0);

        g.Key(KumikoKey.Character, 'x');

        Assert.False(g.Edit.IsEditing);
    }

    [Fact]
    public void TypingTrigger_ControlCharacterAndControlModifier_DoNotEdit()
    {
        var g = new Grid();
        g.Select(0, 0);

        g.Key(KumikoKey.Character, '\u0001');
        g.Key(KumikoKey.Character, 'a', InputModifiers.Control);

        Assert.False(g.Edit.IsEditing);
    }

    [Fact]
    public void ColumnTriggers_OverrideTheGridTriggers()
    {
        var g = new Grid();
        g.Edit.EditTriggers = EditTrigger.Default;
        g.City.EditTriggers = EditTrigger.None;
        g.Name.EditTriggers = EditTrigger.SingleTap;

        g.TapCell(0, 2, clicks: 2); // City: no triggers at all
        Assert.False(g.Edit.IsEditing);

        g.TapCell(0, 0); // Name: single tap now edits
        Assert.True(g.Edit.IsEditing);
        Assert.Same(g.Name, g.Edit.EditColumn);
    }

    [Fact]
    public void ColumnTriggers_BlockTypingEvenWhenTheGridAllowsIt()
    {
        var g = new Grid();
        g.City.EditTriggers = EditTrigger.DoubleTap;
        g.Select(0, 2);

        g.Key(KumikoKey.Character, 'x');

        Assert.False(g.Edit.IsEditing);
    }

    [Fact]
    public void ReadOnlyColumn_NeverOpensAnEditor()
    {
        var g = new Grid();
        g.City.IsReadOnly = true;

        g.TapCell(0, 2, clicks: 2);
        g.Select(0, 2);
        g.Key(KumikoKey.F2);
        g.Key(KumikoKey.Character, 'x');

        Assert.False(g.Edit.IsEditing);
    }

    // ── TextSelectionMode ─────────────────────────────────────────

    [Fact]
    public void SelectAllMode_OpensTheEditorWithTheWholeTextSelected()
    {
        var g = new Grid();
        g.Edit.TextSelectionMode = EditTextSelectionMode.SelectAll;
        g.Select(0, 0);

        g.Key(KumikoKey.F2);

        Assert.Equal("Alice", g.Box.SelectedText);
    }

    [Fact]
    public void CursorAtEndMode_OpensTheEditorWithTheCursorAfterTheText()
    {
        var g = new Grid();
        g.Edit.TextSelectionMode = EditTextSelectionMode.CursorAtEnd;
        g.Select(0, 0);

        g.Key(KumikoKey.F2);

        Assert.False(g.Box.HasSelection);
        Assert.Equal("Alice".Length, g.Box.CursorPosition);
    }

    [Fact]
    public void TypingToStart_AlwaysPutsTheCursorAtTheEnd_WhateverTheMode()
    {
        var g = new Grid();
        g.Edit.TextSelectionMode = EditTextSelectionMode.SelectAll;
        g.Select(0, 0);

        g.Key(KumikoKey.Character, 'Z');

        Assert.False(g.Box.HasSelection);
        Assert.Equal(1, g.Box.CursorPosition);
    }

    // ── Enter, Tab, Escape ────────────────────────────────────────

    [Fact]
    public void Enter_CommitsAndMovesDown_WithoutOpeningTheNextEditor_WhenKeyboardDismissIsOn()
    {
        var g = new Grid();
        Assert.True(g.Edit.DismissKeyboardOnEnter);
        g.Select(0, 0);
        g.Key(KumikoKey.Character, 'Z');

        g.Key(KumikoKey.Enter);

        Assert.Equal("Z", g.Rows[0].Name);
        Assert.False(g.Edit.IsEditing);
        Assert.Equal(new CellPosition(1, 0), g.Selection.CurrentCell);
    }

    [Fact]
    public void Enter_CommitsAndEditsTheCellBelow_WhenKeyboardDismissIsOff()
    {
        var g = new Grid();
        g.Edit.DismissKeyboardOnEnter = false;
        g.Select(0, 0);
        g.Key(KumikoKey.Character, 'Z');

        g.Key(KumikoKey.Enter);

        Assert.Equal("Z", g.Rows[0].Name);
        Assert.True(g.Edit.IsEditing);
        Assert.Equal(new CellPosition(1, 0), g.Edit.EditCell);
        Assert.Equal("Bob", g.Box.Text);
    }

    [Fact]
    public void Enter_WithAnInvalidValue_KeepsEditingInPlace()
    {
        var g = new Grid();
        g.Edit.CellValidator = (_, _, v) => v is string { Length: 0 } ? CellValidationResult.Error("required") : CellValidationResult.Valid;
        g.Select(0, 0);
        g.Key(KumikoKey.F2);
        g.Box.Text = "";

        g.Key(KumikoKey.Enter);

        Assert.True(g.Edit.IsEditing);
        Assert.Equal(new CellPosition(0, 0), g.Selection.CurrentCell);
        Assert.Equal("Alice", g.Rows[0].Name);
        Assert.Equal("required", g.Edit.CurrentValidation!.ErrorMessage);
    }

    [Fact]
    public void Escape_CancelsTheEditAndKeepsTheValue()
    {
        var g = new Grid();
        var ended = new List<CellEndEditEventArgs>();
        g.Edit.CellEndEdit += (_, e) => ended.Add(e);
        g.Select(0, 0);
        g.Key(KumikoKey.Character, 'Z');

        g.Key(KumikoKey.Escape);

        Assert.False(g.Edit.IsEditing);
        Assert.Equal("Alice", g.Rows[0].Name);
        Assert.False(Assert.Single(ended).Committed);
    }

    [Fact]
    public void Tab_CommitsAndOpensTheNextEditableCell()
    {
        var g = new Grid();
        g.Select(0, 0);
        g.Key(KumikoKey.Character, 'Z');

        g.Key(KumikoKey.Tab);

        Assert.Equal("Z", g.Rows[0].Name);
        Assert.True(g.Edit.IsEditing);
        Assert.Equal(new CellPosition(0, 1), g.Edit.EditCell);
    }

    [Fact]
    public void ShiftTab_OpensThePreviousEditableCell()
    {
        var g = new Grid();
        g.Select(0, 1);
        g.Key(KumikoKey.F2);

        g.Key(KumikoKey.Tab, modifiers: InputModifiers.Shift);

        Assert.Equal(new CellPosition(0, 0), g.Edit.EditCell);
    }

    [Fact]
    public void Typing_WhileEditing_GoesToTheEditor()
    {
        var g = new Grid();
        g.Select(0, 0);
        g.Key(KumikoKey.Character, 'A');

        g.Key(KumikoKey.Character, 'B');

        Assert.Equal("AB", g.Box.Text);
    }

    // ── Rendering ─────────────────────────────────────────────────

    [Fact]
    public void Render_PlacesTheEditorOverItsCell_ThenFollowsAScroll()
    {
        var g = new Grid();
        g.Select(1, 0);
        g.Key(KumikoKey.F2);
        g.Render();

        float expectedY = g.Style.HeaderHeight + g.Style.RowHeight;
        Assert.Equal(expectedY, g.Box.Bounds.Y, 1);
        Assert.True(g.Box.IsVisible);
    }

    [Fact]
    public void Render_DrawsTheEditorOnTop_WithTheStyleApplied()
    {
        var g = new Grid();
        g.Style.CellTextColor = new KumikoColor(9, 8, 7);
        g.Edit.Style = g.Style;
        g.Select(0, 0);
        g.Key(KumikoKey.F2);

        var ctx = g.Render();

        Assert.Equal(new KumikoColor(9, 8, 7), g.Box.TextColor);
        Assert.NotEmpty(ctx.Texts);
    }
}

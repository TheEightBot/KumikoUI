using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests.Input;

/// <summary>
/// Key routing while a cell is being edited: Enter and Tab commit and move, Escape cancels, a failed
/// validation keeps the editor open, and other keys go to the editor.
/// </summary>
public class EditingKeyRoutingTests
{
    private static InputGrid Editing(int row, int column, char first = 'Z', Action<InputGrid>? configure = null)
    {
        var g = new InputGrid(configure: configure);
        g.Select(row, column);
        g.Type(first);
        Assert.True(g.Edit.IsEditing, "typing did not open an editor");
        return g;
    }

    [Fact]
    public void Enter_CommitsAndMovesDown()
    {
        var g = Editing(2, 0);

        var e = g.Key(KumikoKey.Enter);

        Assert.True(e.Handled);
        Assert.False(g.Edit.IsEditing);
        Assert.False(g.Selection.IsEditing);
        Assert.Equal("Z", g.Items[2].Name);
        Assert.Equal(new CellPosition(3, 0), g.Selection.CurrentCell);
    }

    [Fact]
    public void Enter_OnTheLastRow_CommitsAndStays()
    {
        var g = Editing(29, 0);

        g.Key(KumikoKey.Enter);

        Assert.Equal("Z", g.Items[29].Name);
        Assert.Equal(new CellPosition(29, 0), g.Selection.CurrentCell);
    }

    [Fact]
    public void Enter_WithDismissKeyboardOff_ContinuesEditingTheCellBelow()
    {
        var g = Editing(2, 0, configure: x => x.Edit.DismissKeyboardOnEnter = false);

        g.Key(KumikoKey.Enter);

        Assert.True(g.Edit.IsEditing);
        Assert.Equal(new CellPosition(3, 0), g.Edit.EditCell);
        Assert.Equal("Z", g.Items[2].Name);
    }

    [Fact]
    public void Enter_ScrollsTheNextRowIntoView()
    {
        var g = Editing(5, 0);

        g.Key(KumikoKey.Enter); // row 6 sits below the 6 visible rows

        Assert.Equal(36, g.Scroll.OffsetY);
    }

    [Fact]
    public void Enter_WithAnInvalidValue_KeepsTheEditorOpen()
    {
        var g = Editing(2, 0, configure: x => x.Edit.CellValidator = (_, _, _) => CellValidationResult.Error("no"));

        var e = g.Key(KumikoKey.Enter);

        Assert.True(e.Handled);
        Assert.True(g.Edit.IsEditing);
        Assert.Equal(new CellPosition(2, 0), g.Selection.CurrentCell);
        Assert.Equal("N02", g.Items[2].Name);
    }

    [Fact]
    public void Escape_CancelsTheEdit()
    {
        var g = Editing(2, 0);

        var e = g.Key(KumikoKey.Escape);

        Assert.True(e.Handled);
        Assert.False(g.Edit.IsEditing);
        Assert.False(g.Selection.IsEditing);
        Assert.Equal("N02", g.Items[2].Name);
        Assert.Equal(new CellPosition(2, 0), g.Selection.CurrentCell); // unlike Escape outside an edit
    }

    [Fact]
    public void Tab_CommitsAndEditsTheNextCell()
    {
        var g = Editing(2, 0);

        var e = g.Key(KumikoKey.Tab);

        Assert.True(e.Handled);
        Assert.Equal("Z", g.Items[2].Name);
        Assert.True(g.Edit.IsEditing);
        Assert.Equal(new CellPosition(2, 1), g.Edit.EditCell);
        Assert.Equal(new CellPosition(2, 1), g.Selection.CurrentCell);
    }

    [Fact]
    public void Tab_SkipsReadOnlyColumns()
    {
        var g = Editing(2, 0, configure: x => x.Age.IsReadOnly = true);

        g.Key(KumikoKey.Tab);

        Assert.Equal(new CellPosition(2, 2), g.Edit.EditCell);
    }

    [Fact]
    public void ShiftTab_CommitsAndEditsThePreviousCell()
    {
        var g = Editing(2, 2);

        g.Key(KumikoKey.Tab, InputModifiers.Shift);

        Assert.Equal("Z", g.Items[2].City);
        Assert.Equal(new CellPosition(2, 1), g.Edit.EditCell);
    }

    [Fact]
    public void ShiftTab_SkipsReadOnlyColumnsAndWrapsToThePreviousRow()
    {
        var g = Editing(2, 1, configure: x => x.Name.IsReadOnly = true);

        g.Key(KumikoKey.Tab, InputModifiers.Shift);

        Assert.Equal(new CellPosition(1, 4), g.Edit.EditCell);
    }

    [Fact]
    public void Tab_ScrollsTheNextCellIntoView()
    {
        var g = Editing(0, 3);

        g.Key(KumikoKey.Tab); // Score spans 400..500

        Assert.Equal(new CellPosition(0, 4), g.Edit.EditCell);
        Assert.Equal(200, g.Scroll.OffsetX);
    }

    [Fact]
    public void Tab_WithAnInvalidValue_StaysPut()
    {
        var g = Editing(2, 0, configure: x => x.Edit.CellValidator = (_, _, _) => CellValidationResult.Error("no"));

        var e = g.Key(KumikoKey.Tab);

        Assert.True(e.Handled);
        Assert.Equal(new CellPosition(2, 0), g.Edit.EditCell);
        Assert.Equal(new CellPosition(2, 0), g.Selection.CurrentCell);
    }

    [Fact]
    public void OtherKeys_GoToTheEditor()
    {
        var g = Editing(2, 0, 'Z');

        Assert.True(g.Type('Q').Handled);
        Assert.True(g.Key(KumikoKey.Backspace).Handled);
        g.Type('W');
        g.Key(KumikoKey.Enter);

        Assert.Equal("ZW", g.Items[2].Name);
    }

    [Fact]
    public void ArrowKeys_MoveTheCaretInsteadOfTheCell()
    {
        var g = Editing(2, 0, 'Z');
        g.Type('Q');

        var e = g.Key(KumikoKey.Left);
        g.Type('X');
        g.Key(KumikoKey.Enter);

        Assert.True(e.Handled);
        Assert.Equal("ZXQ", g.Items[2].Name);
    }

    [Fact]
    public void ClickingAnotherCell_CommitsAndSelectsIt()
    {
        var g = Editing(1, 0);

        g.Tap(150, g.RowY(4));

        Assert.False(g.Edit.IsEditing);
        Assert.Equal("Z", g.Items[1].Name);
        Assert.Equal(new CellPosition(4, 1), g.Selection.CurrentCell);
    }

    [Fact]
    public void SortingFromTheHeader_CommitsFirst_AndSkipsTheSortWhenInvalid()
    {
        var g = Editing(1, 0, configure: x => x.Edit.CellValidator = (_, _, _) => CellValidationResult.Error("no"));

        g.Tap(60, InputGrid.HeaderY);

        Assert.True(g.Edit.IsEditing);
        Assert.Equal(KumikoSortDirection.None, g.Name.SortDirection);
    }

    [Fact]
    public void SortingFromTheHeader_CommitsAValidEditFirst()
    {
        var g = Editing(1, 0);

        g.Tap(60, InputGrid.HeaderY);

        Assert.False(g.Edit.IsEditing);
        Assert.Equal("Z", g.Items[1].Name);
        Assert.Equal(KumikoSortDirection.Ascending, g.Name.SortDirection);
    }
}

using KumikoUI.Core.Components;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests.Input;

/// <summary>
/// Key routing while a cell is being edited: the editor sees every key first (an open dropdown picks with
/// Enter and closes with Escape), then Enter and Tab commit and move, Escape cancels, a failed validation
/// keeps the editor open, and keys the editor rejects never reach the grid's shortcuts. Also the pointer
/// and wheel reaching an open ComboBox dropdown, and a picker confirming once its wheel settles.
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

    // ── ComboBox, date and picker editors (#46) ────────────────────

    /// <summary>A grid whose City column (x 200..300) edits with a combo box of <paramref name="items"/>.</summary>
    private static InputGrid ComboGrid(string items, Action<InputGrid>? configure = null) =>
        new(viewportHeight: 400, configure: x =>
        {
            x.City.ColumnType = KumikoColumnType.ComboBox;
            x.City.EditorItemsString = items;
            configure?.Invoke(x);
        });

    private static DrawnComboBox Combo(InputGrid g) => Assert.IsType<DrawnComboBox>(g.Edit.ActiveEditor);

    [Fact]
    public void ComboBox_F2_DownThreeTimes_Enter_CommitsTheThirdItem()
    {
        var g = ComboGrid("Alpha,Bravo,Charlie,Delta,Echo"); // "Oslo" is none of them: nothing selected
        g.Select(2, 2);

        g.Key(KumikoKey.F2);
        Assert.True(Combo(g).IsDropdownOpen);
        g.Key(KumikoKey.Down);
        g.Key(KumikoKey.Down);
        g.Key(KumikoKey.Down);
        var e = g.Key(KumikoKey.Enter);

        Assert.True(e.Handled);
        Assert.Equal("Charlie", g.Items[2].City);
        Assert.False(g.Edit.IsEditing);
        Assert.False(g.Selection.IsEditing);
        Assert.Equal(new CellPosition(2, 2), g.Selection.CurrentCell); // picking an item stays on the cell
    }

    [Fact]
    public void ComboBox_DownThenTab_CommitsTheHighlightedItem()
    {
        var g = ComboGrid("Alpha,Bravo,Charlie");
        g.Select(2, 2);
        g.Key(KumikoKey.F2);

        g.Key(KumikoKey.Down);
        g.Key(KumikoKey.Down);
        g.Key(KumikoKey.Tab);

        Assert.Equal("Bravo", g.Items[2].City);
        Assert.Equal(new CellPosition(2, 3), g.Edit.EditCell);
    }

    [Fact]
    public void ComboBox_TypingALetterInTheOpenDropdown_JumpsToTheItem_AndDoesNotRestartTheEdit()
    {
        var g = ComboGrid("Alpha,Bravo,Charlie,Beta");
        int begun = 0;
        g.Edit.CellBeginEdit += (_, _) => begun++;
        g.Select(2, 2);
        g.Key(KumikoKey.F2);
        var editor = Combo(g);

        var e = g.Type('B');

        Assert.True(e.Handled);
        Assert.Same(editor, g.Edit.ActiveEditor);
        Assert.Equal(1, begun);
        Assert.Equal("Bravo", editor.SelectedText);
        Assert.True(editor.IsDropdownOpen);
        Assert.Equal("Oslo", g.Items[2].City); // nothing committed yet

        g.Type('b');
        Assert.Equal("Beta", editor.SelectedText);
        g.Key(KumikoKey.Enter);
        Assert.Equal("Beta", g.Items[2].City);
    }

    [Fact]
    public void ComboBox_TypingOnTheCell_OpensTheEditorOnTheMatchingItem()
    {
        var g = ComboGrid("Alpha,Bravo,Charlie");
        g.Select(2, 2);

        g.Type('c');

        Assert.Equal("Charlie", Combo(g).SelectedText);
        g.Key(KumikoKey.Enter);
        Assert.Equal("Charlie", g.Items[2].City);
    }

    [Fact]
    public void ComboBox_Escape_ClosesTheDropdownFirst_ThenCancelsTheEdit()
    {
        var g = ComboGrid("Alpha,Bravo,Charlie");
        g.Select(2, 2);
        g.Key(KumikoKey.F2);
        g.Key(KumikoKey.Down);

        Assert.True(g.Key(KumikoKey.Escape).Handled);
        Assert.True(g.Edit.IsEditing);
        Assert.False(Combo(g).IsDropdownOpen);

        Assert.True(g.Key(KumikoKey.Escape).Handled);
        Assert.False(g.Edit.IsEditing);
        Assert.Equal("Oslo", g.Items[2].City);
    }

    [Fact]
    public void ComboBox_EnterWithTheDropdownClosed_CommitsAndMovesDown()
    {
        var g = ComboGrid("Alpha,Bravo,Charlie");
        g.Select(2, 2);
        g.Key(KumikoKey.F2);
        g.Key(KumikoKey.Down);
        g.Key(KumikoKey.Down);
        g.Key(KumikoKey.Escape); // closes the dropdown and restores the opening selection
        g.Key(KumikoKey.Down);   // reopens it

        g.Key(KumikoKey.Escape);
        g.Key(KumikoKey.Enter);

        Assert.False(g.Edit.IsEditing);
        Assert.Equal("Oslo", g.Items[2].City);
        Assert.Equal(new CellPosition(3, 2), g.Selection.CurrentCell);
    }

    [Theory]
    [InlineData(KumikoKey.F2)]
    [InlineData(KumikoKey.Space)]
    [InlineData(KumikoKey.Delete)]
    public void KeysTheEditorRejects_AreConsumed_AndNeverReachTheGridsShortcuts(KumikoKey key)
    {
        var g = ComboGrid("Alpha,Bravo");
        int begun = 0;
        g.Edit.CellBeginEdit += (_, _) => begun++;
        g.Select(2, 2);
        g.Key(KumikoKey.F2);
        var editor = Combo(g);

        var e = g.Key(key);

        Assert.True(e.Handled);
        Assert.Same(editor, g.Edit.ActiveEditor);
        Assert.Equal(1, begun);
        Assert.Empty(g.Selection.SelectedItems);
    }

    [Fact]
    public void ShortcutKeysTheEditorRejects_DoNotSelectEveryRow_ButStayUnhandledForThePage()
    {
        var g = ComboGrid("Alpha,Bravo");
        g.Select(2, 2);
        g.Key(KumikoKey.F2);

        var e = g.Key(KumikoKey.A, InputModifiers.Control);

        Assert.False(e.Handled);
        Assert.True(g.Edit.IsEditing);
        Assert.Empty(g.Selection.SelectedItems);
    }

    [Fact]
    public void DatePicker_Space_DoesNotToggleTheRowSelection_OrRestartTheEdit()
    {
        var g = new InputGrid(configure: x => x.City.ColumnType = KumikoColumnType.Date);
        int begun = 0;
        g.Edit.CellBeginEdit += (_, _) => begun++;
        g.Select(2, 2);
        g.Key(KumikoKey.F2);
        var editor = Assert.IsType<DrawnDatePicker>(g.Edit.ActiveEditor);

        Assert.True(g.Key(KumikoKey.Space).Handled);
        Assert.True(g.Type(' ').Handled); // hosts that capture the keyboard in a text box send it as text

        Assert.Empty(g.Selection.SelectedItems);
        Assert.Same(editor, g.Edit.ActiveEditor);
        Assert.Equal(1, begun);
    }

    [Fact]
    public void ComboBox_FiftyItemDropdown_ReachesTheLastItemByDrag()
    {
        var g = ComboGrid(string.Join(",", Enumerable.Range(0, 50).Select(i => $"Item {i:D2}")));
        g.Select(0, 2);
        g.Key(KumikoKey.F2);
        var combo = Combo(g);
        var drop = combo.GetDropdownBounds();
        float x = drop.X + 30;

        // A drag that starts on the list and runs far past its top edge scrolls the list, not the grid
        g.Press(x, drop.Y + 150);
        for (int i = 1; i <= 30; i++) g.Move(x, drop.Y + 150 - i * 50f);
        g.Release(x, drop.Y + 150 - 30 * 50f);

        Assert.Equal(50 * combo.ItemHeight - drop.Height, combo.DropdownScrollOffset);
        Assert.Equal(0, g.Scroll.OffsetY);
        Assert.True(g.Edit.IsEditing);
        Assert.Equal(-1, combo.SelectedIndex);

        g.Tap(x, drop.Bottom - combo.ItemHeight / 2);

        Assert.Equal("Item 49", g.Items[0].City);
        Assert.False(g.Edit.IsEditing);
    }

    [Fact]
    public void ComboBox_FiftyItemDropdown_ReachesTheLastItemByWheel()
    {
        var g = ComboGrid(string.Join(",", Enumerable.Range(0, 50).Select(i => $"Item {i:D2}")));
        g.Select(0, 2);
        g.Key(KumikoKey.F2);
        var combo = Combo(g);
        var drop = combo.GetDropdownBounds();
        float x = drop.X + 30, y = drop.Y + 40;

        for (int i = 0; i < 20; i++)
            Assert.True(g.Pointer(InputAction.Scroll, x, y, scrollDy: -1).Handled);

        Assert.Equal(50 * combo.ItemHeight - drop.Height, combo.DropdownScrollOffset);
        Assert.Equal(0, g.Scroll.OffsetY); // the grid did not scroll under the dropdown

        g.Tap(x, drop.Bottom - combo.ItemHeight / 2);
        Assert.Equal("Item 49", g.Items[0].City);
    }

    [Fact]
    public void Wheel_OverTheGridWhileEditing_StillScrollsTheGrid()
    {
        var g = ComboGrid("Alpha,Bravo");
        g.Select(0, 2);
        g.Key(KumikoKey.F2);

        g.Pointer(InputAction.Scroll, 60, g.RowY(4), scrollDy: -1);

        Assert.True(g.Scroll.OffsetY > 0);
    }

    [Fact]
    public void Picker_ConfirmsOnlyOnceTheFrameLoopSettlesTheWheel()
    {
        var g = new InputGrid(configure: x =>
        {
            x.City.ColumnType = KumikoColumnType.Picker;
            x.City.EditorItemsString = "Oslo,Rome,Paris,Lima";
        });
        g.Select(0, 2); // "Oslo": the first item is centered
        g.Key(KumikoKey.F2);
        var picker = Assert.IsType<DrawnScrollPicker>(g.Edit.ActiveEditor);
        float x = picker.Bounds.X + picker.Bounds.Width / 2;
        float center = picker.Bounds.Y + picker.Bounds.Height / 2;

        g.Tap(x, center + 2 * picker.ItemHeight); // "Paris", two below the center

        Assert.True(g.Edit.IsEditing);
        Assert.True(g.Edit.IsAnimating);
        Assert.Equal("Oslo", g.Items[0].City);

        int frames = 0;
        while (g.Edit.UpdateAnimation(16f) && frames < 1000) frames++;

        Assert.False(g.Edit.IsEditing);
        Assert.Equal("Paris", g.Items[0].City);
    }
}

using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests;

public class SelectionModelTests
{
    // ── Defaults ──────────────────────────────────────────────────

    [Fact]
    public void Default_Mode_IsExtended()
    {
        var model = new SelectionModel();
        Assert.Equal(SelectionMode.Extended, model.Mode);
    }

    [Fact]
    public void Default_Unit_IsRow()
    {
        var model = new SelectionModel();
        Assert.Equal(SelectionUnit.Row, model.Unit);
    }

    [Fact]
    public void Default_CurrentCell_IsInvalid()
    {
        var model = new SelectionModel();
        Assert.False(model.CurrentCell.IsValid);
    }

    // ── SelectionMode.None ────────────────────────────────────────

    [Fact]
    public void HandleRowSelection_NoneMode_DoesNotSelectAnyRow()
    {
        var model = new SelectionModel { Mode = SelectionMode.None };
        model.HandleRowSelection(0, false, false, 10);
        Assert.Empty(model.SelectedRows);
    }

    // ── SelectionMode.Single ──────────────────────────────────────

    [Fact]
    public void HandleRowSelection_Single_SelectsOneRow()
    {
        var model = new SelectionModel { Mode = SelectionMode.Single };
        model.HandleRowSelection(3, false, false, 10);
        Assert.Single(model.SelectedRows);
        Assert.Contains(3, model.SelectedRows);
    }

    [Fact]
    public void HandleRowSelection_Single_SecondClick_ReplacesPreviousSelection()
    {
        var model = new SelectionModel { Mode = SelectionMode.Single };
        model.HandleRowSelection(3, false, false, 10);
        model.HandleRowSelection(7, false, false, 10);
        Assert.Single(model.SelectedRows);
        Assert.Contains(7, model.SelectedRows);
    }

    // ── SelectionMode.Multiple ────────────────────────────────────

    [Fact]
    public void HandleRowSelection_Multiple_TogglesRow()
    {
        var model = new SelectionModel { Mode = SelectionMode.Multiple };
        model.HandleRowSelection(3, false, false, 10);
        model.HandleRowSelection(5, false, false, 10);
        Assert.Equal(2, model.SelectedRows.Count);

        // Second click on 3 removes it
        model.HandleRowSelection(3, false, false, 10);
        Assert.DoesNotContain(3, model.SelectedRows);
    }

    // ── SelectionMode.Extended ────────────────────────────────────

    [Fact]
    public void HandleRowSelection_Extended_SingleClick_SelectsOneRow()
    {
        var model = new SelectionModel { Mode = SelectionMode.Extended };
        model.HandleRowSelection(4, false, false, 10);
        Assert.Single(model.SelectedRows);
        Assert.Contains(4, model.SelectedRows);
    }

    [Fact]
    public void HandleRowSelection_Extended_Ctrl_TogglesRows()
    {
        var model = new SelectionModel { Mode = SelectionMode.Extended };
        model.HandleRowSelection(2, false, false, 10);
        model.HandleRowSelection(5, false, ctrl: true, 10);
        Assert.Equal(2, model.SelectedRows.Count);
        Assert.Contains(2, model.SelectedRows);
        Assert.Contains(5, model.SelectedRows);

        // Ctrl click on 5 again removes it
        model.HandleRowSelection(5, false, ctrl: true, 10);
        Assert.DoesNotContain(5, model.SelectedRows);
    }

    [Fact]
    public void HandleRowSelection_Extended_Shift_SelectsRange()
    {
        var model = new SelectionModel { Mode = SelectionMode.Extended };
        model.HandleRowSelection(2, false, false, 10); // anchor = 2
        model.HandleRowSelection(5, shift: true, false, 10);
        // Should select 2, 3, 4, 5
        for (int i = 2; i <= 5; i++)
            Assert.Contains(i, model.SelectedRows);
        Assert.Equal(4, model.SelectedRows.Count);
    }

    [Fact]
    public void HandleRowSelection_Extended_ShiftUp_SelectsRange()
    {
        var model = new SelectionModel { Mode = SelectionMode.Extended };
        model.HandleRowSelection(5, false, false, 10); // anchor = 5
        model.HandleRowSelection(2, shift: true, false, 10);
        // Should select 2, 3, 4, 5
        for (int i = 2; i <= 5; i++)
            Assert.Contains(i, model.SelectedRows);
    }

    // ── CurrentCell ───────────────────────────────────────────────

    [Fact]
    public void HandleRowSelection_UpdatesCurrentCell()
    {
        var model = new SelectionModel();
        model.HandleRowSelection(4, false, false, 10);
        Assert.Equal(4, model.CurrentCell.Row);
    }

    // ── SelectAll ─────────────────────────────────────────────────

    [Fact]
    public void SelectAll_Multiple_SelectsAllRows()
    {
        var model = new SelectionModel { Mode = SelectionMode.Multiple };
        model.SelectAll(5);
        Assert.Equal(5, model.SelectedRows.Count);
        for (int i = 0; i < 5; i++)
            Assert.Contains(i, model.SelectedRows);
    }

    [Fact]
    public void SelectAll_Extended_SelectsAllRows()
    {
        var model = new SelectionModel { Mode = SelectionMode.Extended };
        model.SelectAll(4);
        Assert.Equal(4, model.SelectedRows.Count);
    }

    [Fact]
    public void SelectAll_None_DoesNotSelectRows()
    {
        var model = new SelectionModel { Mode = SelectionMode.None };
        model.SelectAll(10);
        Assert.Empty(model.SelectedRows);
    }

    [Fact]
    public void SelectAll_Single_DoesNotSelectRows()
    {
        var model = new SelectionModel { Mode = SelectionMode.Single };
        model.SelectAll(10);
        Assert.Empty(model.SelectedRows);
    }

    // ── ClearSelection ────────────────────────────────────────────

    [Fact]
    public void ClearSelection_ClearsEverything()
    {
        var model = new SelectionModel { Mode = SelectionMode.Multiple };
        model.HandleRowSelection(1, false, false, 10);
        model.HandleRowSelection(2, false, false, 10);
        model.IsEditing = true;

        model.ClearSelection();

        Assert.Empty(model.SelectedRows);
        Assert.Empty(model.SelectedCells);
        Assert.False(model.CurrentCell.IsValid);
        Assert.False(model.IsEditing);
    }

    [Fact]
    public void ClearSelection_EmptySelection_DoesNotRaiseEvent()
    {
        var model = new SelectionModel();
        int eventCount = 0;
        model.SelectionChanged += (_, _) => eventCount++;
        model.ClearSelection();
        Assert.Equal(0, eventCount);
    }

    // ── IsRowSelected / IsCellSelected ────────────────────────────

    [Fact]
    public void IsRowSelected_AfterSelection_ReturnsTrue()
    {
        var model = new SelectionModel { Mode = SelectionMode.Single };
        model.HandleRowSelection(3, false, false, 10);
        Assert.True(model.IsRowSelected(3));
        Assert.False(model.IsRowSelected(4));
    }

    [Fact]
    public void IsCellSelected_SelectedCell_ReturnsTrue()
    {
        var model = new SelectionModel();
        model.SelectedCells.Add(new CellPosition(2, 3));
        Assert.True(model.IsCellSelected(2, 3));
        Assert.False(model.IsCellSelected(2, 4));
    }

    // ── Navigation ────────────────────────────────────────────────

    [Fact]
    public void NavigateDown_MovesCursorDown()
    {
        var model = new SelectionModel { Mode = SelectionMode.Single };
        model.HandleRowSelection(2, false, false, 10);
        model.NavigateDown(10, false);
        Assert.Equal(3, model.CurrentCell.Row);
    }

    [Fact]
    public void NavigateDown_AtLastRow_StaysAtLastRow()
    {
        var model = new SelectionModel { Mode = SelectionMode.Single };
        model.HandleRowSelection(9, false, false, 10);
        model.NavigateDown(10, false);
        Assert.Equal(9, model.CurrentCell.Row);
    }

    [Fact]
    public void NavigateUp_MovesCursorUp()
    {
        var model = new SelectionModel { Mode = SelectionMode.Single };
        model.HandleRowSelection(5, false, false, 10);
        model.NavigateUp(10, false);
        Assert.Equal(4, model.CurrentCell.Row);
    }

    [Fact]
    public void NavigateUp_AtFirstRow_StaysAtFirstRow()
    {
        var model = new SelectionModel { Mode = SelectionMode.Single };
        model.HandleRowSelection(0, false, false, 10);
        model.NavigateUp(10, false);
        Assert.Equal(0, model.CurrentCell.Row);
    }

    [Fact]
    public void NavigateLeft_MovesCursorLeft()
    {
        var model = new SelectionModel { Mode = SelectionMode.Single };
        model.HandleRowSelection(2, false, false, 10);
        model.NavigateLeft(5, false);
        // Initial column was 0, stays at 0
        Assert.Equal(0, model.CurrentCell.Column);
    }

    [Fact]
    public void NavigateRight_MovesCursorRight()
    {
        var model = new SelectionModel { Mode = SelectionMode.Single };
        model.HandleRowSelection(2, false, false, 10);
        model.NavigateRight(5, false);
        Assert.Equal(1, model.CurrentCell.Column);
    }

    [Fact]
    public void NavigateRight_AtLastColumn_Clamps()
    {
        var model = new SelectionModel { Mode = SelectionMode.Single };
        model.HandleRowSelection(0, false, false, 10);
        model.NavigateRight(1, false);
        // With 1 column (index 0 only), should stay at 0
        Assert.Equal(0, model.CurrentCell.Column);
    }

    [Fact]
    public void NavigateHome_MovesColumnToZero()
    {
        var model = new SelectionModel { Mode = SelectionMode.Single };
        model.HandleRowSelection(3, false, false, 10);
        // move to column 3 first
        model.NavigateRight(10, false);
        model.NavigateRight(10, false);
        model.NavigateRight(10, false);
        model.NavigateHome(false, 10, false);
        Assert.Equal(0, model.CurrentCell.Column);
        Assert.Equal(3, model.CurrentCell.Row);
    }

    [Fact]
    public void NavigateHome_WithCtrl_MovesToFirstRow()
    {
        var model = new SelectionModel { Mode = SelectionMode.Single };
        model.HandleRowSelection(7, false, false, 10);
        model.NavigateHome(true, 10, false);
        Assert.Equal(0, model.CurrentCell.Row);
        Assert.Equal(0, model.CurrentCell.Column);
    }

    [Fact]
    public void NavigateEnd_WithCtrl_MovesToLastRow()
    {
        var model = new SelectionModel { Mode = SelectionMode.Single };
        model.HandleRowSelection(3, false, false, 10);
        model.NavigateEnd(true, 10, 5, false);
        Assert.Equal(9, model.CurrentCell.Row);
        Assert.Equal(4, model.CurrentCell.Column);
    }

    [Fact]
    public void NavigatePageDown_MovesDownByPageSize()
    {
        var model = new SelectionModel { Mode = SelectionMode.Single };
        model.HandleRowSelection(0, false, false, 20);
        model.NavigatePageDown(5, 20, false);
        Assert.Equal(5, model.CurrentCell.Row);
    }

    [Fact]
    public void NavigatePageDown_ClampsToLastRow()
    {
        var model = new SelectionModel { Mode = SelectionMode.Single };
        model.HandleRowSelection(18, false, false, 20);
        model.NavigatePageDown(10, 20, false);
        Assert.Equal(19, model.CurrentCell.Row);
    }

    [Fact]
    public void NavigatePageUp_MovesUpByPageSize()
    {
        var model = new SelectionModel { Mode = SelectionMode.Single };
        model.HandleRowSelection(10, false, false, 20);
        model.NavigatePageUp(5, false, 20);
        Assert.Equal(5, model.CurrentCell.Row);
    }

    // ── SelectionChanged event ─────────────────────────────────────

    [Fact]
    public void SelectionChanged_RaisedWhenSelectionChanges()
    {
        var model = new SelectionModel { Mode = SelectionMode.Single };
        int eventCount = 0;
        model.SelectionChanged += (_, _) => eventCount++;
        model.HandleRowSelection(3, false, false, 10);
        Assert.Equal(1, eventCount);
    }

    [Fact]
    public void SelectionChanged_EventArgs_ContainsCorrectData()
    {
        var model = new SelectionModel { Mode = SelectionMode.Single };
        SelectionChangedEventArgs? args = null;
        model.SelectionChanged += (_, e) => args = e;
        model.HandleRowSelection(3, false, false, 10);
        Assert.NotNull(args);
        Assert.Contains(3, args!.SelectedRows);
        Assert.Equal(3, args.CurrentCell.Row);
    }

    [Fact]
    public void SelectAll_RaisesSelectionChangedEvent()
    {
        var model = new SelectionModel { Mode = SelectionMode.Multiple };
        int eventCount = 0;
        model.SelectionChanged += (_, _) => eventCount++;
        model.SelectAll(5);
        Assert.Equal(1, eventCount);
    }
}


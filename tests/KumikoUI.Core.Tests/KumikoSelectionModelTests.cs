using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests;

public class KumikoSelectionModelTests
{
    /// <summary>A selection over a source of <paramref name="rows"/> items (row i shows item i).</summary>
    private static KumikoSelectionModel NewModel(KumikoSelectionMode mode = KumikoSelectionMode.Extended, int rows = 20)
    {
        var source = new KumikoSource();
        source.SetColumns(new[]
        {
            new KumikoColumn { PropertyName = "Name" },
            new KumikoColumn { PropertyName = "Age" },
            new KumikoColumn { PropertyName = "Department" },
        });
        source.SetItems(Enumerable.Range(0, rows).Select(i => new Person { Name = $"P{i}", Age = i }).ToList());
        return new KumikoSelectionModel(source) { Mode = mode };
    }

    // ── Defaults ──────────────────────────────────────────────────

    [Fact]
    public void Default_Mode_IsExtended()
    {
        var model = NewModel();
        Assert.Equal(KumikoSelectionMode.Extended, model.Mode);
    }

    [Fact]
    public void Default_CurrentCell_IsInvalid()
    {
        var model = NewModel();
        Assert.False(model.CurrentCell.IsValid);
    }

    // ── KumikoSelectionMode.None ────────────────────────────────────────

    [Fact]
    public void HandleRowSelection_NoneMode_DoesNotSelectAnyRow()
    {
        var model = NewModel(KumikoSelectionMode.None);
        model.HandleRowSelection(0, false, false);
        Assert.Empty(model.SelectedRows);
    }

    // ── KumikoSelectionMode.Single ──────────────────────────────────────

    [Fact]
    public void HandleRowSelection_Single_SelectsOneRow()
    {
        var model = NewModel(KumikoSelectionMode.Single);
        model.HandleRowSelection(3, false, false);
        Assert.Single(model.SelectedRows);
        Assert.Contains(3, model.SelectedRows);
    }

    [Fact]
    public void HandleRowSelection_Single_SecondClick_ReplacesPreviousSelection()
    {
        var model = NewModel(KumikoSelectionMode.Single);
        model.HandleRowSelection(3, false, false);
        model.HandleRowSelection(7, false, false);
        Assert.Single(model.SelectedRows);
        Assert.Contains(7, model.SelectedRows);
    }

    // ── KumikoSelectionMode.Multiple ────────────────────────────────────

    [Fact]
    public void HandleRowSelection_Multiple_TogglesRow()
    {
        var model = NewModel(KumikoSelectionMode.Multiple);
        model.HandleRowSelection(3, false, false);
        model.HandleRowSelection(5, false, false);
        Assert.Equal(2, model.SelectedRows.Count);

        // Second click on 3 removes it
        model.HandleRowSelection(3, false, false);
        Assert.DoesNotContain(3, model.SelectedRows);
    }

    // ── KumikoSelectionMode.Extended ────────────────────────────────────

    [Fact]
    public void HandleRowSelection_Extended_SingleClick_SelectsOneRow()
    {
        var model = NewModel(KumikoSelectionMode.Extended);
        model.HandleRowSelection(4, false, false);
        Assert.Single(model.SelectedRows);
        Assert.Contains(4, model.SelectedRows);
    }

    [Fact]
    public void HandleRowSelection_Extended_Ctrl_TogglesRows()
    {
        var model = NewModel(KumikoSelectionMode.Extended);
        model.HandleRowSelection(2, false, false);
        model.HandleRowSelection(5, false, ctrl: true);
        Assert.Equal(2, model.SelectedRows.Count);
        Assert.Contains(2, model.SelectedRows);
        Assert.Contains(5, model.SelectedRows);

        // Ctrl click on 5 again removes it
        model.HandleRowSelection(5, false, ctrl: true);
        Assert.DoesNotContain(5, model.SelectedRows);
    }

    [Fact]
    public void HandleRowSelection_Extended_Shift_SelectsRange()
    {
        var model = NewModel(KumikoSelectionMode.Extended);
        model.HandleRowSelection(2, false, false); // anchor = 2
        model.HandleRowSelection(5, shift: true, false);
        // Should select 2, 3, 4, 5
        for (int i = 2; i <= 5; i++)
            Assert.Contains(i, model.SelectedRows);
        Assert.Equal(4, model.SelectedRows.Count);
    }

    [Fact]
    public void HandleRowSelection_Extended_ShiftUp_SelectsRange()
    {
        var model = NewModel(KumikoSelectionMode.Extended);
        model.HandleRowSelection(5, false, false); // anchor = 5
        model.HandleRowSelection(2, shift: true, false);
        // Should select 2, 3, 4, 5
        for (int i = 2; i <= 5; i++)
            Assert.Contains(i, model.SelectedRows);
    }

    // ── CurrentCell ───────────────────────────────────────────────

    [Fact]
    public void HandleRowSelection_UpdatesCurrentCell()
    {
        var model = NewModel();
        model.HandleRowSelection(4, false, false);
        Assert.Equal(4, model.CurrentCell.Row);
    }

    // ── SelectAll ─────────────────────────────────────────────────

    [Fact]
    public void SelectAll_Multiple_SelectsAllRows()
    {
        var model = NewModel(KumikoSelectionMode.Multiple, rows: 5);
        model.SelectAll();
        Assert.Equal(5, model.SelectedRows.Count);
        for (int i = 0; i < 5; i++)
            Assert.Contains(i, model.SelectedRows);
    }

    [Fact]
    public void SelectAll_Extended_SelectsAllRows()
    {
        var model = NewModel(KumikoSelectionMode.Extended, rows: 4);
        model.SelectAll();
        Assert.Equal(4, model.SelectedRows.Count);
    }

    [Fact]
    public void SelectAll_None_DoesNotSelectRows()
    {
        var model = NewModel(KumikoSelectionMode.None);
        model.SelectAll();
        Assert.Empty(model.SelectedRows);
    }

    [Fact]
    public void SelectAll_Single_DoesNotSelectRows()
    {
        var model = NewModel(KumikoSelectionMode.Single);
        model.SelectAll();
        Assert.Empty(model.SelectedRows);
    }

    // ── ClearSelection ────────────────────────────────────────────

    [Fact]
    public void ClearSelection_ClearsEverything()
    {
        var model = NewModel(KumikoSelectionMode.Multiple);
        model.HandleRowSelection(1, false, false);
        model.HandleRowSelection(2, false, false);
        model.IsEditing = true;

        model.Clear();

        Assert.Empty(model.SelectedRows);
        Assert.Empty(model.SelectedItems);
        Assert.False(model.CurrentCell.IsValid);
        Assert.False(model.IsEditing);
    }

    [Fact]
    public void ClearSelection_EmptySelection_DoesNotRaiseEvent()
    {
        var model = NewModel();
        int eventCount = 0;
        model.SelectionChanged += (_, _) => eventCount++;
        model.Clear();
        Assert.Equal(0, eventCount);
    }

    // ── IsRowSelected ────────────────────────────────────────

    [Fact]
    public void IsRowSelected_AfterSelection_ReturnsTrue()
    {
        var model = NewModel(KumikoSelectionMode.Single);
        model.HandleRowSelection(3, false, false);
        Assert.True(model.IsRowSelected(3));
        Assert.False(model.IsRowSelected(4));
    }

    // ── Navigation ────────────────────────────────────────────────

    [Fact]
    public void NavigateDown_MovesCursorDown()
    {
        var model = NewModel(KumikoSelectionMode.Single);
        model.HandleRowSelection(2, false, false);
        model.NavigateDown(10, false);
        Assert.Equal(3, model.CurrentCell.Row);
    }

    [Fact]
    public void NavigateDown_AtLastRow_StaysAtLastRow()
    {
        var model = NewModel(KumikoSelectionMode.Single);
        model.HandleRowSelection(9, false, false);
        model.NavigateDown(10, false);
        Assert.Equal(9, model.CurrentCell.Row);
    }

    [Fact]
    public void NavigateUp_MovesCursorUp()
    {
        var model = NewModel(KumikoSelectionMode.Single);
        model.HandleRowSelection(5, false, false);
        model.NavigateUp(10, false);
        Assert.Equal(4, model.CurrentCell.Row);
    }

    [Fact]
    public void NavigateUp_AtFirstRow_StaysAtFirstRow()
    {
        var model = NewModel(KumikoSelectionMode.Single);
        model.HandleRowSelection(0, false, false);
        model.NavigateUp(10, false);
        Assert.Equal(0, model.CurrentCell.Row);
    }

    [Fact]
    public void NavigateLeft_MovesCursorLeft()
    {
        var model = NewModel(KumikoSelectionMode.Single);
        model.HandleRowSelection(2, false, false);
        model.NavigateLeft(5, false);
        // Initial column was 0, stays at 0
        Assert.Equal(0, model.CurrentCell.Column);
    }

    [Fact]
    public void NavigateRight_MovesCursorRight()
    {
        var model = NewModel(KumikoSelectionMode.Single);
        model.HandleRowSelection(2, false, false);
        model.NavigateRight(5, false);
        Assert.Equal(1, model.CurrentCell.Column);
    }

    [Fact]
    public void NavigateRight_AtLastColumn_Clamps()
    {
        var model = NewModel(KumikoSelectionMode.Single);
        model.HandleRowSelection(0, false, false);
        for (int i = 0; i < 5; i++) model.NavigateRight(1, false); // the source's three columns decide, not the argument
        Assert.Equal(2, model.CurrentCell.Column);
    }

    [Fact]
    public void NavigateHome_MovesColumnToZero()
    {
        var model = NewModel(KumikoSelectionMode.Single);
        model.HandleRowSelection(3, false, false);
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
        var model = NewModel(KumikoSelectionMode.Single);
        model.HandleRowSelection(7, false, false);
        model.NavigateHome(true, 10, false);
        Assert.Equal(0, model.CurrentCell.Row);
        Assert.Equal(0, model.CurrentCell.Column);
    }

    [Fact]
    public void NavigateEnd_WithCtrl_MovesToLastRow()
    {
        var model = NewModel(KumikoSelectionMode.Single);
        model.HandleRowSelection(3, false, false);
        model.NavigateEnd(true, 10, 5, false);
        Assert.Equal(9, model.CurrentCell.Row);
        Assert.Equal(2, model.CurrentCell.Column); // the last of the source's three visible columns
    }

    [Fact]
    public void NavigatePageDown_MovesDownByPageSize()
    {
        var model = NewModel(KumikoSelectionMode.Single);
        model.HandleRowSelection(0, false, false);
        model.NavigatePageDown(5, 20, false);
        Assert.Equal(5, model.CurrentCell.Row);
    }

    [Fact]
    public void NavigatePageDown_ClampsToLastRow()
    {
        var model = NewModel(KumikoSelectionMode.Single);
        model.HandleRowSelection(18, false, false);
        model.NavigatePageDown(10, 20, false);
        Assert.Equal(19, model.CurrentCell.Row);
    }

    [Fact]
    public void NavigatePageUp_MovesUpByPageSize()
    {
        var model = NewModel(KumikoSelectionMode.Single);
        model.HandleRowSelection(10, false, false);
        model.NavigatePageUp(5, false, 20);
        Assert.Equal(5, model.CurrentCell.Row);
    }

    // ── SelectionChanged event ─────────────────────────────────────

    [Fact]
    public void SelectionChanged_RaisedWhenSelectionChanges()
    {
        var model = NewModel(KumikoSelectionMode.Single);
        int eventCount = 0;
        model.SelectionChanged += (_, _) => eventCount++;
        model.HandleRowSelection(3, false, false);
        Assert.Equal(1, eventCount);
    }

    [Fact]
    public void SelectionChanged_EventArgs_ContainsCorrectData()
    {
        var model = NewModel(KumikoSelectionMode.Single);
        KumikoSelectionChangedEventArgs? args = null;
        model.SelectionChanged += (_, e) => args = e;
        model.HandleRowSelection(3, false, false);
        Assert.NotNull(args);
        Assert.Contains(3, args!.SelectedRows);
        Assert.Equal(3, args.CurrentCell.Row);
    }

    [Fact]
    public void SelectAll_RaisesSelectionChangedEvent()
    {
        var model = NewModel(KumikoSelectionMode.Multiple);
        int eventCount = 0;
        model.SelectionChanged += (_, _) => eventCount++;
        model.SelectAll();
        Assert.Equal(1, eventCount);
    }
}


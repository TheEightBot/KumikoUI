using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests.Input;

/// <summary>
/// Which taps toggle a Boolean cell, and taps on the empty part of the rows area.
/// </summary>
/// <remarks>
/// The Boolean rule: a single tap toggles when it lands on the check box (or anywhere in the cell with the
/// <see cref="EditTrigger.SingleTap"/> trigger); the second tap of a double-tap toggles with the
/// <see cref="EditTrigger.DoubleTap"/> trigger unless the first tap already did, so a double-tap toggles once;
/// <see cref="EditTrigger.None"/> and read-only columns never toggle.
/// </remarks>
public class BooleanAndEmptyAreaTapTests
{
    private const float OnCheckBox = 50;   // the 100 px column's check box is centered
    private const float OffCheckBox = 10;

    // ── Single tap ──

    [Fact]
    public void Tap_OnTheCheckBox_Toggles()
    {
        var g = BooleanColumnGrid.Create();

        g.Tap(OnCheckBox, g.RowY(1));

        Assert.True(g.Items[1].On);
        Assert.Equal(new CellPosition(1, 0), g.Selection.CurrentCell);
    }

    [Fact]
    public void Tap_ElsewhereInTheCell_OnlySelects()
    {
        var g = BooleanColumnGrid.Create();

        g.Tap(OffCheckBox, g.RowY(1));

        Assert.False(g.Items[1].On);
        Assert.True(g.Selection.IsRowSelected(1));
        Assert.Equal(new CellPosition(1, 0), g.Selection.CurrentCell);
    }

    [Fact]
    public void Tap_NearTheCheckBox_StillToggles()
    {
        var g = BooleanColumnGrid.Create();

        g.Tap(OnCheckBox + 8 + 6, g.RowY(0)); // inside the margin around the 16 px box

        Assert.True(g.Items[0].On);
    }

    [Fact]
    public void Tap_AnywhereInTheCell_TogglesWithTheSingleTapTrigger()
    {
        var g = BooleanColumnGrid.Create(EditTrigger.SingleTap);

        g.Tap(OffCheckBox, g.RowY(1));

        Assert.True(g.Items[1].On);
    }

    [Fact]
    public void SeparateTaps_OnTheCheckBox_ToggleEachTime()
    {
        var g = BooleanColumnGrid.Create();

        g.Tap(OnCheckBox, g.RowY(0));
        g.Tap(OnCheckBox, g.RowY(0));

        Assert.False(g.Items[0].On);
    }

    // ── Double tap ──

    [Fact]
    public void DoubleTap_OnTheCheckBox_TogglesOnce()
    {
        var g = BooleanColumnGrid.Create();
        int changes = 0;
        g.Edit.CellValueChanged += (_, _) => changes++;

        g.DoubleTap(OnCheckBox, g.RowY(0));

        Assert.True(g.Items[0].On);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void DoubleTap_ElsewhereInTheCell_TogglesOnceWithTheDoubleTapTrigger()
    {
        var g = BooleanColumnGrid.Create();
        int changes = 0;
        g.Edit.CellValueChanged += (_, _) => changes++;

        g.DoubleTap(OffCheckBox, g.RowY(0));

        Assert.True(g.Items[0].On);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void DoubleTap_WithSingleTapAndDoubleTapTriggers_TogglesOnce()
    {
        var g = BooleanColumnGrid.Create(EditTrigger.SingleTap | EditTrigger.DoubleTap);

        g.DoubleTap(OffCheckBox, g.RowY(2));

        Assert.True(g.Items[2].On);
    }

    [Fact]
    public void DoubleTap_ElsewhereInTheCell_WithoutTheDoubleTapTrigger_DoesNotToggle()
    {
        var g = BooleanColumnGrid.Create(EditTrigger.F2Key);

        g.DoubleTap(OffCheckBox, g.RowY(0));

        Assert.False(g.Items[0].On);
    }

    [Fact]
    public void DoubleTap_AfterAToggleOfAnotherCell_TogglesThisOne()
    {
        var g = BooleanColumnGrid.Create();
        g.Tap(OnCheckBox, g.RowY(0));              // toggles row 0

        g.Tap(OffCheckBox, g.RowY(1), clicks: 2);  // reported as a double-tap, but on another cell

        Assert.True(g.Items[0].On);
        Assert.True(g.Items[1].On);
    }

    // ── Never toggles ──

    [Fact]
    public void EditTriggersNone_NeverToggles()
    {
        var g = BooleanColumnGrid.Create(EditTrigger.None);

        g.Tap(OnCheckBox, g.RowY(0));
        g.DoubleTap(OnCheckBox, g.RowY(1));
        g.Input.HandlePointer(new KumikoPointerEventArgs { X = OnCheckBox, Y = g.RowY(2), Action = InputAction.LongPress },
            g.Scroll, g.Selection, g.Style, g.Source);

        Assert.All(g.Items, i => Assert.False(i.On));
    }

    [Fact]
    public void ColumnEditTriggersNone_OverridesTheGrid()
    {
        var g = BooleanColumnGrid.Create(EditTrigger.SingleTap, configure: c => c.EditTriggers = EditTrigger.None);

        g.Tap(OnCheckBox, g.RowY(0));

        Assert.False(g.Items[0].On);
    }

    [Fact]
    public void ReadOnlyColumn_NeverToggles()
    {
        var g = BooleanColumnGrid.Create(EditTrigger.SingleTap, configure: c => c.IsReadOnly = true);

        g.Tap(OnCheckBox, g.RowY(0));
        g.DoubleTap(OnCheckBox, g.RowY(1));

        Assert.All(g.Items, i => Assert.False(i.On));
    }

    // ── Empty area ──

    [Fact]
    public void Tap_BelowTheLastRow_ClearsTheSelection()
    {
        var g = new InputGrid(rows: 2, viewportHeight: 400);
        g.Tap(10, g.RowY(1));
        Assert.True(g.Selection.IsRowSelected(1));

        g.Tap(10, g.RowY(5));

        Assert.Empty(g.Selection.SelectedRows);
        Assert.False(g.Selection.CurrentCell.IsValid);
    }

    [Fact]
    public void Tap_RightOfTheLastColumn_ClearsTheSelection()
    {
        var g = new InputGrid(rows: 4, viewportWidth: 600);
        g.Tap(10, g.RowY(1));

        g.Tap(550, g.RowY(1));

        Assert.Empty(g.Selection.SelectedRows);
        Assert.False(g.Selection.CurrentCell.IsValid);
    }

    [Fact]
    public void Tap_OnTheEmptyArea_KeepsTheSelection_WhenTurnedOff()
    {
        var g = new InputGrid(rows: 2, viewportHeight: 400, configure: x => x.Selection.ClearSelectionOnEmptyAreaTap = false);
        g.Tap(10, g.RowY(1));

        g.Tap(10, g.RowY(5));

        Assert.True(g.Selection.IsRowSelected(1));
        Assert.Equal(new CellPosition(1, 0), g.Selection.CurrentCell);
    }

    [Fact]
    public void Tap_RightOfTheLastHeader_KeepsTheSelection()
    {
        var g = new InputGrid(rows: 4, viewportWidth: 600);
        g.Tap(10, g.RowY(1));

        g.Tap(550, InputGrid.HeaderY);

        Assert.True(g.Selection.IsRowSelected(1));
    }

    [Fact]
    public void Tap_OnTheEmptyArea_CommitsAnOpenEditFirst()
    {
        var g = new InputGrid(rows: 2, viewportHeight: 400);
        g.Select(1, 0);
        g.Type('Q');

        g.Tap(10, g.RowY(5));

        Assert.Equal("Q", g.Items[1].Name);
        Assert.False(g.Edit.IsEditing);
        Assert.False(g.Selection.IsEditing);
        Assert.False(g.Selection.CurrentCell.IsValid);
    }

    [Fact]
    public void Tap_OnTheEmptyArea_WithARefusedCommit_KeepsTheEditorAndSelection()
    {
        var g = new InputGrid(rows: 2, viewportHeight: 400, configure: x =>
            x.Edit.CellValidator = (_, _, _) => CellValidationResult.Error("No."));
        g.Tap(10, g.RowY(1));
        g.Type('Q');

        g.Tap(10, g.RowY(5));

        Assert.True(g.Edit.IsEditing);
        Assert.True(g.Selection.IsRowSelected(1));
        Assert.Equal(new CellPosition(1, 0), g.Selection.CurrentCell);
    }

    [Fact]
    public void ClearSelectionOnEmptyAreaTap_IsOnByDefault()
    {
        Assert.True(new KumikoSelectionModel(new KumikoSource()).ClearSelectionOnEmptyAreaTap);
    }
}

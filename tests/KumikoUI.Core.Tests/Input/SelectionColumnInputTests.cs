using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests.Input;

/// <summary>
/// The selection column (#321) through the input controller: taps on a row's box and on the check-all header box,
/// Space and Shift+Space, Tab and the arrows, and what a box press never does (edit, pan into a row drag).
/// </summary>
/// <remarks>
/// Geometry: the 44 px selection column is pinned left (x 0-44), so Name starts at x 44. Header y 20, row r at
/// <see cref="InputGrid.RowY"/>.
/// </remarks>
public class SelectionColumnInputTests
{
    private const float Box = 22f; // the middle of the selection cell
    private const float NameCell = 80f;

    private static InputGrid Grid(KumikoSelectionMode mode = KumikoSelectionMode.Extended, int rows = 30,
        Action<InputGrid>? configure = null) =>
        new(rows, configure: g =>
        {
            g.Source.SetColumns(new KumikoColumn[] { new KumikoSelectionColumn(), g.Name, g.Age, g.City, g.Dept, g.Score });
            g.Selection.Mode = mode;
            configure?.Invoke(g);
        });

    // ── A tap on a row's box ──

    [Fact]
    public void TapOnTheBox_ChecksTheRow_AndAnotherTapUnchecksIt()
    {
        var g = Grid();

        g.Tap(Box, g.RowY(2));
        Assert.True(g.Selection.IsRowSelected(2));

        g.Tap(Box, g.RowY(2));
        Assert.False(g.Selection.IsRowSelected(2));
    }

    [Theory]
    [InlineData(2f)]
    [InlineData(22f)]
    [InlineData(42f)]
    public void TheWholeCell_IsTheTarget(float x)
    {
        var g = Grid();

        g.Tap(x, g.RowY(1));

        Assert.True(g.Selection.IsRowSelected(1));
    }

    [Fact]
    public void BoxTaps_AddRows_WithoutCtrl_InExtendedMode()
    {
        var g = Grid(KumikoSelectionMode.Extended);
        g.Tap(NameCell, g.RowY(0)); // a plain row tap selects one row

        g.Tap(Box, g.RowY(3));
        g.Tap(Box, g.RowY(5));

        Assert.Equal(new[] { 0, 3, 5 }, g.Selection.SelectedRows.Order());
    }

    [Fact]
    public void ShiftTap_OnTheBox_AppliesTheNewStateToTheRange()
    {
        var g = Grid();
        g.Tap(Box, g.RowY(1));

        g.Tap(Box, g.RowY(4), InputModifiers.Shift);

        Assert.Equal(new[] { 1, 2, 3, 4 }, g.Selection.SelectedRows.Order());
    }

    [Fact]
    public void CtrlTap_OnTheBox_BehavesLikeATap()
    {
        var g = Grid();

        g.Tap(Box, g.RowY(2), InputModifiers.Control);
        g.Tap(Box, g.RowY(4), InputModifiers.Control);

        Assert.Equal(new[] { 2, 4 }, g.Selection.SelectedRows.Order());
    }

    [Fact]
    public void SingleMode_ACheckReplacesTheSelection()
    {
        var g = Grid(KumikoSelectionMode.Single);

        g.Tap(Box, g.RowY(1));
        g.Tap(Box, g.RowY(3));
        Assert.Equal(new[] { 3 }, g.Selection.SelectedRows);

        g.Tap(Box, g.RowY(3));
        Assert.Empty(g.Selection.SelectedRows);
    }

    [Fact]
    public void NoneMode_TheBoxDoesNothing()
    {
        var g = Grid(KumikoSelectionMode.None);

        g.Tap(Box, g.RowY(2));

        Assert.Empty(g.Selection.SelectedItems);
    }

    [Fact]
    public void ATapOnTheBox_MovesTheCurrentCellToTheRow_InTheDataColumn()
    {
        var g = Grid();
        g.Select(0, 3);

        g.Tap(Box, g.RowY(4));

        Assert.Equal(new CellPosition(4, 3), g.Selection.CurrentCell);
    }

    [Fact]
    public void ATapOnTheBox_WithNoCurrentCell_UsesTheFirstDataColumn()
    {
        var g = Grid();

        g.Tap(Box, g.RowY(4));

        Assert.Equal(new CellPosition(4, 1), g.Selection.CurrentCell);
    }

    [Fact]
    public void ATapOnTheBox_RaisesNoRowOrCellTap_AndStartsNoEdit()
    {
        var g = Grid();
        int tapped = 0;
        g.Input.RowTapped += (_, _) => tapped++;
        g.Input.CellTapped += (_, _) => tapped++;

        g.Tap(Box, g.RowY(2));
        g.Tap(Box, g.RowY(2), clicks: 2);

        Assert.Equal(0, tapped);
        Assert.False(g.Edit.IsEditing);
    }

    [Fact]
    public void ATapOnTheBox_CommitsAnOpenEdit_BeforeToggling()
    {
        var g = Grid();
        g.Select(1, 1);
        g.Type('Q');
        Assert.True(g.Edit.IsEditing);

        g.Tap(Box, g.RowY(3));

        Assert.False(g.Edit.IsEditing);
        Assert.Equal("Q", g.Items[1].Name);
        Assert.True(g.Selection.IsRowSelected(3));
    }

    [Fact]
    public void ATapOnTheBox_WithARefusedCommit_KeepsTheEditor_AndDoesNotToggle()
    {
        var g = Grid(configure: x => x.Edit.CellValidator = (_, _, _) => CellValidationResult.Error("No."));
        g.Select(1, 1);
        g.Type('Q');

        g.Tap(Box, g.RowY(3));

        Assert.True(g.Edit.IsEditing);
        Assert.False(g.Selection.IsRowSelected(3));
    }

    [Fact]
    public void DraggingFromTheBox_PansOrScrolls_NeverReordersARow_NorToggles()
    {
        var g = Grid(configure: x => x.Style.AllowRowDragDrop = true);
        var reordered = new List<RowReorderedEventArgs>();
        g.Input.RowReordered += (_, e) => reordered.Add(e);

        g.Press(Box, g.RowY(3));
        g.Move(Box, g.RowY(3) + 20);
        g.Move(Box, g.RowY(3) + 60);
        g.Release(Box, g.RowY(3) + 60);

        Assert.Equal(-1, g.Input.DragRowIndex);
        Assert.Empty(reordered);
        Assert.Empty(g.Selection.SelectedItems);
    }

    [Fact]
    public void ALongPress_OnTheBox_StartsNoRowDrag_AndNoEdit()
    {
        var g = Grid(configure: x => x.Style.AllowRowDragDrop = true);

        g.Pointer(InputAction.Pressed, Box, g.RowY(3), deviceType: KumikoPointerDeviceType.Touch);
        g.Pointer(InputAction.LongPress, Box, g.RowY(3), deviceType: KumikoPointerDeviceType.Touch);

        Assert.Equal(-1, g.Input.DragRowIndex);
        Assert.False(g.Edit.IsEditing);
    }

    [Fact]
    public void AReleaseOnTheBox_AfterAPressElsewhere_DoesNotToggle()
    {
        var g = Grid();

        g.Press(NameCell, g.RowY(2));
        g.Release(Box, g.RowY(2));

        Assert.Empty(g.Selection.SelectedItems);
    }

    [Fact]
    public void EditTriggers_NeverOpenAnEditorOnTheBox()
    {
        var g = Grid(configure: x => x.Edit.EditTriggers = EditTrigger.SingleTap | EditTrigger.DoubleTap | EditTrigger.LongPress);

        g.Tap(Box, g.RowY(1));
        g.Tap(Box, g.RowY(1), clicks: 2);

        Assert.False(g.Edit.IsEditing);
    }

    // ── SelectOnRowClick ──

    [Fact]
    public void SelectOnRowClickOff_ARowTapOnlyMovesTheCurrentCell_TheBoxStillSelects()
    {
        var g = Grid(configure: x => x.Selection.SelectOnRowClick = false);

        g.Tap(NameCell, g.RowY(2));
        Assert.Empty(g.Selection.SelectedItems);
        Assert.Equal(new CellPosition(2, 1), g.Selection.CurrentCell);

        g.Tap(Box, g.RowY(4));
        Assert.Equal(new[] { 4 }, g.Selection.SelectedRows);
    }

    // ── The check-all header box ──

    [Fact]
    public void HeaderTap_SelectsAll_ThenClears_AndKeepsTheCurrentCell()
    {
        var g = Grid();
        g.Select(2, 3);

        g.Tap(Box, InputGrid.HeaderY);
        Assert.Equal(30, g.Selection.SelectedItems.Count);
        Assert.True(g.Selection.CheckAllState);

        g.Tap(Box, InputGrid.HeaderY);
        Assert.Empty(g.Selection.SelectedItems);
        Assert.Equal(new CellPosition(2, 3), g.Selection.CurrentCell);
    }

    [Fact]
    public void HeaderTap_WhenSomeAreSelected_SelectsAll()
    {
        var g = Grid();
        g.Tap(Box, g.RowY(1));
        Assert.Null(g.Selection.CheckAllState);

        g.Tap(Box, InputGrid.HeaderY);

        Assert.True(g.Selection.CheckAllState);
    }

    [Fact]
    public void HeaderTap_DoesNotSort_NorOpenAFilterOrMenu()
    {
        var g = Grid();
        int sorts = 0;
        g.Source.SortColumnsChanged += (_, _) => sorts++;

        g.Tap(Box, InputGrid.HeaderY);

        Assert.Equal(0, sorts);
        Assert.False(g.Input.PopupManager.HasActivePopups);
        Assert.All(g.Source.Columns, c => Assert.Equal(KumikoSortDirection.None, c.SortDirection));
    }

    [Fact]
    public void HeaderTap_RaisesCheckAllToggled_WithTheNewState()
    {
        var g = Grid();
        var states = new List<bool>();
        g.Input.CheckAllToggled += (_, all) => states.Add(all);

        g.Tap(Box, InputGrid.HeaderY);
        g.Tap(Box, InputGrid.HeaderY);

        Assert.Equal(new[] { true, false }, states);
    }

    [Theory]
    [InlineData(KumikoSelectionMode.Single)]
    [InlineData(KumikoSelectionMode.None)]
    public void HeaderTap_HasNoCheckAllInSingleAndNone(KumikoSelectionMode mode)
    {
        var g = Grid(mode);
        int toggled = 0;
        g.Input.CheckAllToggled += (_, _) => toggled++;

        g.Tap(Box, InputGrid.HeaderY);

        Assert.Empty(g.Selection.SelectedItems);
        Assert.Equal(0, toggled);
    }

    [Fact]
    public void HeaderTap_SelectsOnlyTheFilteredView()
    {
        var g = Grid();
        g.Source.SetFilter(o => ((InputGrid.Item)o).City == "Oslo");

        g.Tap(Box, InputGrid.HeaderY);

        Assert.Equal(15, g.Selection.SelectedItems.Count);
        Assert.True(g.Selection.CheckAllState);
    }

    [Fact]
    public void ThePressOnTheHeader_DoesNotStartAColumnDrag()
    {
        var g = Grid();

        g.Press(Box, InputGrid.HeaderY);
        g.Move(Box + 60, InputGrid.HeaderY);
        g.Release(Box + 60, InputGrid.HeaderY);

        Assert.Equal(-1, g.Input.DragColumnIndex);
        Assert.Equal(KumikoColumnType.Selection, g.Source.Columns[0].ColumnType);
    }

    // ── Keyboard ──

    [Fact]
    public void Space_OnTheSelectionColumn_TogglesTheRow()
    {
        var g = Grid();
        g.Select(2, 0);

        var first = g.Key(KumikoKey.Space);
        Assert.True(first.Handled);
        Assert.Equal(new[] { 2 }, g.Selection.SelectedRows);

        g.Key(KumikoKey.Space);
        Assert.Empty(g.Selection.SelectedRows);
    }

    [Fact]
    public void ShiftSpace_OnTheSelectionColumn_RangeToggles()
    {
        var g = Grid();
        g.Select(1, 0);
        g.Key(KumikoKey.Space);
        g.Select(4, 0);

        g.Key(KumikoKey.Space, InputModifiers.Shift);

        Assert.Equal(new[] { 1, 2, 3, 4 }, g.Selection.SelectedRows.Order());
    }

    [Fact]
    public void Space_OnTheSelectionColumn_AddsToTheSelection_InExtendedMode()
    {
        var g = Grid();
        g.Tap(NameCell, g.RowY(0));
        g.Select(3, 0);

        g.Key(KumikoKey.Space);

        Assert.Equal(new[] { 0, 3 }, g.Selection.SelectedRows.Order());
    }

    [Fact]
    public void Space_OnAnotherColumn_StillTogglesTheRowAsBefore()
    {
        var g = Grid();
        g.Select(2, 3);

        g.Key(KumikoKey.Space);

        Assert.Equal(new[] { 2 }, g.Selection.SelectedRows);
    }

    [Fact]
    public void LeftArrow_CanLandOnTheSelectionColumn()
    {
        var g = Grid();
        g.Select(2, 1);

        g.Key(KumikoKey.Left);

        Assert.Equal(new CellPosition(2, 0), g.Selection.CurrentCell);
    }

    [Fact]
    public void Tab_SkipsTheSelectionColumn()
    {
        var g = Grid();
        g.Select(0, 5); // the last column

        g.Key(KumikoKey.Tab);

        Assert.Equal(new CellPosition(1, 1), g.Selection.CurrentCell);
    }

    [Fact]
    public void ShiftTab_FromTheFirstDataColumn_DoesNotLandOnTheSelectionColumn()
    {
        var g = Grid();
        g.Select(2, 1);

        g.Key(KumikoKey.Tab, InputModifiers.Shift);

        Assert.Equal(new CellPosition(1, 5), g.Selection.CurrentCell);
    }

    [Fact]
    public void TypingOnTheSelectionColumn_StartsNoEdit()
    {
        var g = Grid();
        g.Select(2, 0);

        g.Type('Z');

        Assert.False(g.Edit.IsEditing);
    }

    // ── Exclusions ──

    [Fact]
    public void Copy_LeavesTheSelectionColumnOut()
    {
        string text = KumikoTsvFormatter.Format(Grid().Source, new[] { 0, 1 }, includeHeaders: true);

        Assert.StartsWith("Name\tAge\tCity\tDept\tScore\r\n", text);
        Assert.Equal(3, text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.StartsWith("N00\t", text.Split("\r\n")[1]);
    }

    [Fact]
    public void TheMenuButton_IsNotDrawnOrHit_InTheSelectionHeader()
    {
        var g = Grid(configure: x =>
        {
            x.Input.ShowColumnMenuButton = true;
            x.Renderer.ShowColumnMenuButton = true;
        });
        g.Render();

        var hit = g.Hit(40, InputGrid.HeaderY);

        Assert.Equal(HitRegion.HeaderCell, hit.Region);
        Assert.Equal(KumikoColumnType.Selection, hit.Column!.ColumnType);
    }

    [Fact]
    public void TheSelectionColumn_IsNotSearched()
    {
        var g = Grid();

        Assert.False(g.Source.Columns[0].IsSearchable);
        Assert.True(g.Source.Columns[1].IsSearchable);
    }
}

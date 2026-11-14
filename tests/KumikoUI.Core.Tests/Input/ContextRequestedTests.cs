using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests.Input;

/// <summary>
/// Pointer buttons and context menus (#100): only the primary button sorts, selects, scrolls, drags and edits; a
/// secondary-button click and a touch or pen long press raise <see cref="KumikoInputController.ContextRequested"/>;
/// primary taps raise <see cref="KumikoInputController.CellTapped"/> and <see cref="KumikoInputController.HeaderTapped"/>.
/// </summary>
public class ContextRequestedTests
{
    private static List<KumikoContextRequestedEventArgs> TrackContext(InputGrid g)
    {
        var requests = new List<KumikoContextRequestedEventArgs>();
        g.Input.ContextRequested += (_, e) => requests.Add(e);
        return requests;
    }

    private static (KumikoPointerEventArgs Press, KumikoPointerEventArgs Release) RightClick(
        InputGrid g, float x, float y, PointerButton releaseButton = PointerButton.Secondary) =>
        (g.Pointer(InputAction.Pressed, x, y, button: PointerButton.Secondary),
         g.Pointer(InputAction.Released, x, y, button: releaseButton));

    // ── Secondary button: never sorts, edits or drags ──

    [Fact]
    public void RightClick_OnAHeader_DoesNotSort_AndRaisesContextRequestedForTheColumn()
    {
        var g = new InputGrid();
        var requests = TrackContext(g);
        int headerTaps = 0;
        g.Input.HeaderTapped += (_, _) => headerTaps++;

        var (press, release) = RightClick(g, 130, InputGrid.HeaderY);

        Assert.Equal(KumikoSortDirection.None, g.Age.SortDirection);
        Assert.Equal(0, headerTaps);
        Assert.True(press.Handled && release.Handled);
        var e = Assert.Single(requests);
        Assert.Equal(KumikoContextTrigger.SecondaryButton, e.Trigger);
        Assert.Equal(HitRegion.HeaderCell, e.Region);
        Assert.True(e.IsHeader);
        Assert.Same(g.Age, e.Column);
        Assert.Equal(1, e.ColumnIndex);
        Assert.Equal(-1, e.RowIndex);
        Assert.Null(e.DataItem);
        Assert.Equal((130f, InputGrid.HeaderY), (e.X, e.Y));
    }

    [Fact]
    public void RightClick_ReleaseReportingNoButton_StillFollowsThePress()
    {
        // Some hosts name no button (or the primary one) on a release: the press decides.
        var g = new InputGrid();
        var requests = TrackContext(g);

        RightClick(g, 60, InputGrid.HeaderY, releaseButton: PointerButton.Primary);

        Assert.Equal(KumikoSortDirection.None, g.Name.SortDirection);
        Assert.Single(requests);
    }

    [Fact]
    public void RightClick_OnACell_RaisesContextRequestedWithTheRowItemAndColumn_AndSelectsTheRow()
    {
        var g = new InputGrid();
        var requests = TrackContext(g);
        int selectionAtEvent = -1;
        g.Input.ContextRequested += (_, _) => selectionAtEvent = g.Selection.SelectedRows.Single();

        RightClick(g, 150, g.RowY(3));

        var e = Assert.Single(requests);
        Assert.Equal(KumikoContextTrigger.SecondaryButton, e.Trigger);
        Assert.Equal(HitRegion.Cell, e.Region);
        Assert.Equal(3, e.RowIndex);
        Assert.Equal(1, e.ColumnIndex);
        Assert.Same(g.Age, e.Column);
        Assert.Same(g.Items[3], e.DataItem);
        Assert.False(e.IsHeader);
        Assert.Equal(3, selectionAtEvent); // the selection is updated before the event
        Assert.Equal(new CellPosition(3, 1), g.Selection.CurrentCell);
    }

    [Fact]
    public void RightClick_OnAnUnselectedRow_ReplacesTheSelection()
    {
        var g = new InputGrid();
        g.Tap(60, g.RowY(1));
        g.Tap(60, g.RowY(2), InputModifiers.Control);

        RightClick(g, 60, g.RowY(4));

        Assert.Equal(new[] { 4 }, g.Selection.SelectedRows);
    }

    [Fact]
    public void RightClick_OnASelectedRow_KeepsTheSelection_AndMovesTheCurrentCell()
    {
        var g = new InputGrid();
        g.Tap(60, g.RowY(1));
        g.Tap(60, g.RowY(2), InputModifiers.Control);

        RightClick(g, 230, g.RowY(1));

        Assert.Equal(new[] { 1, 2 }, g.Selection.SelectedRows.OrderBy(r => r));
        Assert.Equal(new CellPosition(1, 2), g.Selection.CurrentCell);
    }

    [Fact]
    public void RightClick_OnACell_DoesNotBeginAnEdit_EvenWithTheSingleTapTrigger()
    {
        var g = new InputGrid(configure: x => x.Edit.EditTriggers = EditTrigger.SingleTap | EditTrigger.DoubleTap);
        int beginEdits = 0;
        g.Edit.CellBeginEdit += (_, _) => beginEdits++;

        RightClick(g, 150, g.RowY(2));
        g.Pointer(InputAction.Pressed, 150, g.RowY(2), clicks: 2, button: PointerButton.Secondary);
        g.Pointer(InputAction.Released, 150, g.RowY(2), clicks: 2, button: PointerButton.Secondary);

        Assert.False(g.Edit.IsEditing);
        Assert.Equal(0, beginEdits);
    }

    [Fact]
    public void RightClick_DoesNotRaiseTapEvents()
    {
        var g = new InputGrid();
        int taps = 0;
        g.Input.RowTapped += (_, _) => taps++;
        g.Input.CellTapped += (_, _) => taps++;
        g.Input.HeaderTapped += (_, _) => taps++;

        RightClick(g, 150, g.RowY(2));
        RightClick(g, 150, InputGrid.HeaderY);

        Assert.Equal(0, taps);
    }

    [Fact]
    public void RightDrag_OnACell_NeitherPansNorDragsTheRow()
    {
        var g = new InputGrid(configure: x => x.Style.AllowRowDragDrop = true);
        var requests = TrackContext(g);

        g.Pointer(InputAction.Pressed, 150, g.RowY(4), button: PointerButton.Secondary);
        var move = g.Pointer(InputAction.Moved, 150, g.RowY(1), button: PointerButton.Secondary);
        Assert.Equal(-1, g.Input.DragRowIndex);
        g.Pointer(InputAction.Released, 150, g.RowY(1), button: PointerButton.Secondary);

        Assert.True(move.Handled);
        Assert.Equal(0f, g.Scroll.OffsetY);
        Assert.Equal(-1, g.Input.DragRowIndex);
        Assert.Equal(1, Assert.Single(requests).RowIndex); // the menu is for where the button went up
    }

    [Fact]
    public void RightDrag_OnAHeader_DoesNotStartAColumnDrag()
    {
        var g = new InputGrid();

        g.Pointer(InputAction.Pressed, 60, InputGrid.HeaderY, button: PointerButton.Secondary);
        g.Pointer(InputAction.Moved, 160, InputGrid.HeaderY, button: PointerButton.Secondary);

        Assert.Equal(-1, g.Input.DragColumnIndex);
    }

    [Fact]
    public void RightPress_OnAResizeGrip_DoesNotResize()
    {
        var g = new InputGrid();
        Assert.Equal(HitRegion.HeaderResizeGrip, g.Hit(119, InputGrid.HeaderY).Region);
        var requests = TrackContext(g);

        g.Pointer(InputAction.Pressed, 119, InputGrid.HeaderY, button: PointerButton.Secondary);
        g.Pointer(InputAction.Moved, 180, InputGrid.HeaderY, button: PointerButton.Secondary);
        g.Pointer(InputAction.Released, 180, InputGrid.HeaderY, button: PointerButton.Secondary);

        Assert.Equal(120f, g.Name.Width);
        Assert.Single(requests);
    }

    [Fact]
    public void RightClick_OnAGroupChevron_DoesNotCollapseTheGroup()
    {
        var g = new InputGrid(rows: 6, viewportHeight: 500, configure: x => x.Source.AddGroupDescription(new GroupDescription("City")));
        int rows = g.Source.RowCount;
        var requests = TrackContext(g);

        RightClick(g, 15, 80 + 18);

        Assert.Equal(rows, g.Source.RowCount);
        var e = Assert.Single(requests);
        Assert.Equal(HitRegion.GroupChevron, e.Region);
        Assert.Equal(0, e.RowIndex);
        Assert.Null(e.DataItem);
    }

    [Fact]
    public void RightClick_OnTheEmptyArea_KeepsTheSelection_AndReportsNoRow()
    {
        var g = new InputGrid(rows: 2, viewportHeight: 400);
        g.Tap(60, g.RowY(1));
        var requests = TrackContext(g);

        RightClick(g, 60, g.RowY(6));

        Assert.Equal(new[] { 1 }, g.Selection.SelectedRows);
        var e = Assert.Single(requests);
        Assert.Equal(HitRegion.EmptyArea, e.Region);
        Assert.Equal(-1, e.RowIndex);
        Assert.Null(e.DataItem);
    }

    [Fact]
    public void RightClick_OnABooleanCell_DoesNotToggleIt()
    {
        var items = new[] { new Flag { On = false } };
        var source = new KumikoSource();
        var column = new KumikoColumn { Header = "On", PropertyName = "On", Width = 100, ColumnType = KumikoColumnType.Boolean };
        source.SetColumns(new[] { column });
        source.SetItems(items);
        var selection = new KumikoSelectionModel(source);
        var input = new KumikoInputController { EditSession = new EditSession { EditTriggers = EditTrigger.SingleTap } };
        var scroll = new ScrollState { ViewportWidth = 300, ViewportHeight = 200 };
        var style = new KumikoStyle();
        float y = style.HeaderHeight + style.RowHeight / 2;

        foreach (var action in new[] { InputAction.Pressed, InputAction.Released })
            input.HandlePointer(new KumikoPointerEventArgs { X = 50, Y = y, Action = action, Button = PointerButton.Secondary },
                scroll, selection, style, source);

        Assert.False(items[0].On);
    }

    private sealed class Flag
    {
        public bool On { get; set; }
    }

    [Fact]
    public void MiddleClick_DoesNothing_AndIsLeftToThePage()
    {
        var g = new InputGrid();
        var requests = TrackContext(g);

        var press = g.Pointer(InputAction.Pressed, 60, InputGrid.HeaderY, button: PointerButton.Middle);
        var release = g.Pointer(InputAction.Released, 60, InputGrid.HeaderY, button: PointerButton.Middle);
        g.Pointer(InputAction.Pressed, 60, g.RowY(2), button: PointerButton.Middle);
        g.Pointer(InputAction.Released, 60, g.RowY(2), button: PointerButton.Middle);

        Assert.False(press.Handled || release.Handled);
        Assert.Equal(KumikoSortDirection.None, g.Name.SortDirection);
        Assert.Empty(g.Selection.SelectedRows);
        Assert.Empty(requests);
    }

    [Fact]
    public void RightClick_AfterwardsAPrimaryTap_WorksAsUsual()
    {
        var g = new InputGrid();
        RightClick(g, 60, InputGrid.HeaderY);

        g.Tap(60, InputGrid.HeaderY);

        Assert.Equal(KumikoSortDirection.Ascending, g.Name.SortDirection);
    }

    // ── Secondary button with an editor or a popup open ──

    [Fact]
    public void RightClick_OnAnotherCell_CommitsTheOpenEdit_ThenRaisesContextRequested()
    {
        var g = new InputGrid();
        g.Select(1, 0);
        g.Key(KumikoKey.F2);
        Assert.True(g.Edit.IsEditing);
        var requests = TrackContext(g);

        RightClick(g, 150, g.RowY(4));

        Assert.False(g.Edit.IsEditing);
        Assert.Equal(4, Assert.Single(requests).RowIndex);
    }

    [Fact]
    public void RightClick_InsideTheOpenEditor_KeepsTheEdit_AndRaisesNothing()
    {
        var g = new InputGrid();
        g.Select(1, 0);
        g.Key(KumikoKey.F2);
        var requests = TrackContext(g);

        RightClick(g, 60, g.RowY(1));

        Assert.True(g.Edit.IsEditing);
        Assert.Empty(requests);
    }

    [Fact]
    public void RightClick_OnAnotherCell_WithARefusedCommit_KeepsTheEditor_AndRaisesNothing()
    {
        var g = new InputGrid(configure: x => x.Edit.CellValidator = (_, _, _) => CellValidationResult.Error("no"));
        g.Select(1, 0);
        g.Type('x');
        Assert.True(g.Edit.IsEditing);
        var requests = TrackContext(g);

        RightClick(g, 150, g.RowY(4));

        Assert.True(g.Edit.IsEditing);
        Assert.Empty(requests);
        Assert.Equal(1, g.Selection.CurrentCell.Row);
    }

    [Fact]
    public void RightClick_OutsideAnOpenPopup_ClosesIt_AndRaisesNothing()
    {
        var g = new InputGrid(viewportWidth: 900);
        g.Tap(120 + 80 - 8 - 5, InputGrid.HeaderY); // the Age filter icon
        Assert.True(g.Input.PopupManager.HasActivePopups);
        var requests = TrackContext(g);

        RightClick(g, 700, g.RowY(5));

        Assert.False(g.Input.PopupManager.HasActivePopups);
        Assert.Empty(requests);
        Assert.Empty(g.Selection.SelectedRows);
    }

    [Fact]
    public void RightClick_InsideAnOpenPopup_DoesNothing()
    {
        var g = new InputGrid(viewportWidth: 900);
        g.Tap(120 + 80 - 8 - 5, InputGrid.HeaderY);
        var popup = g.Input.PopupManager.HitTest(122, g.Style.HeaderHeight + 2)!;
        var requests = TrackContext(g);

        // Anywhere in the popup: its sort buttons, its list, its OK button
        for (float y = popup.Bounds.Y + 4; y < popup.Bounds.Bottom; y += 12)
            RightClick(g, popup.Bounds.X + popup.Bounds.Width / 2, y);

        Assert.True(g.Input.PopupManager.HasActivePopups);
        Assert.Equal(KumikoSortDirection.None, g.Age.SortDirection);
        Assert.Empty(requests);
    }

    // ── Long press ──

    [Theory]
    [InlineData(KumikoPointerDeviceType.Touch)]
    [InlineData(KumikoPointerDeviceType.Pen)]
    public void TouchOrPenLongPress_OnACell_RaisesContextRequested_AndSelectsTheRow(KumikoPointerDeviceType device)
    {
        var g = new InputGrid();
        var requests = TrackContext(g);

        g.Pointer(InputAction.Pressed, 150, g.RowY(2), deviceType: device);
        var longPress = g.Pointer(InputAction.LongPress, 150, g.RowY(2), deviceType: device);

        Assert.True(longPress.Handled);
        var e = Assert.Single(requests);
        Assert.Equal(KumikoContextTrigger.LongPress, e.Trigger);
        Assert.Equal(device, e.DeviceType);
        Assert.Equal(2, e.RowIndex);
        Assert.Same(g.Age, e.Column);
        Assert.Same(g.Items[2], e.DataItem);
        Assert.Equal(new[] { 2 }, g.Selection.SelectedRows);
        Assert.False(g.Edit.IsEditing);
    }

    [Fact]
    public void TouchLongPress_ThenRelease_DoesNotTap()
    {
        var g = new InputGrid(configure: x => x.Edit.EditTriggers = EditTrigger.SingleTap);
        int taps = 0;
        g.Input.RowTapped += (_, _) => taps++;

        g.Pointer(InputAction.Pressed, 150, g.RowY(2), deviceType: KumikoPointerDeviceType.Touch);
        g.Pointer(InputAction.LongPress, 150, g.RowY(2), deviceType: KumikoPointerDeviceType.Touch);
        var release = g.Pointer(InputAction.Released, 150, g.RowY(2), deviceType: KumikoPointerDeviceType.Touch);

        Assert.True(release.Handled);
        Assert.Equal(0, taps);
        Assert.False(g.Edit.IsEditing);
    }

    [Fact]
    public void TouchLongPress_OnAHeader_RaisesContextRequested_AndTheReleaseDoesNotSort()
    {
        var g = new InputGrid();
        var requests = TrackContext(g);

        g.Pointer(InputAction.Pressed, 60, InputGrid.HeaderY, deviceType: KumikoPointerDeviceType.Touch);
        g.Pointer(InputAction.LongPress, 60, InputGrid.HeaderY, deviceType: KumikoPointerDeviceType.Touch);
        g.Pointer(InputAction.Moved, 64, InputGrid.HeaderY + 20, deviceType: KumikoPointerDeviceType.Touch);
        g.Pointer(InputAction.Released, 64, InputGrid.HeaderY + 20, deviceType: KumikoPointerDeviceType.Touch);

        Assert.Equal(KumikoSortDirection.None, g.Name.SortDirection);
        Assert.Equal(0f, g.Scroll.OffsetY);
        var e = Assert.Single(requests);
        Assert.Equal(HitRegion.HeaderCell, e.Region);
        Assert.Same(g.Name, e.Column);
    }

    [Fact]
    public void TouchLongPress_WithTheLongPressEditTrigger_EditsInsteadOfRaisingContextRequested()
    {
        var g = new InputGrid(configure: x => x.Edit.EditTriggers = EditTrigger.LongPress);
        var requests = TrackContext(g);

        g.Pointer(InputAction.Pressed, 150, g.RowY(2), deviceType: KumikoPointerDeviceType.Touch);
        g.Pointer(InputAction.LongPress, 150, g.RowY(2), deviceType: KumikoPointerDeviceType.Touch);

        Assert.True(g.Edit.IsEditing);
        Assert.Empty(requests);
    }

    [Fact]
    public void TouchLongPress_WithRowDragDrop_DragsInsteadOfRaisingContextRequested()
    {
        var g = new InputGrid(configure: x => x.Style.AllowRowDragDrop = true);
        var requests = TrackContext(g);

        g.Pointer(InputAction.Pressed, 150, g.RowY(2), deviceType: KumikoPointerDeviceType.Touch);
        g.Pointer(InputAction.LongPress, 150, g.RowY(2), deviceType: KumikoPointerDeviceType.Touch);

        Assert.Equal(2, g.Input.DragRowIndex);
        Assert.Empty(requests);
    }

    [Fact]
    public void TouchLongPress_InsideTheOpenEditor_KeepsTheEdit_AndRaisesNothing()
    {
        var g = new InputGrid();
        g.Select(1, 0);
        g.Key(KumikoKey.F2);
        var requests = TrackContext(g);

        var longPress = g.Pointer(InputAction.LongPress, 60, g.RowY(1), deviceType: KumikoPointerDeviceType.Touch);

        Assert.True(longPress.Handled);
        Assert.True(g.Edit.IsEditing);
        Assert.Empty(requests);
    }

    [Fact]
    public void MouseLongPress_IsASlowClick_NotAContextRequest()
    {
        var g = new InputGrid();
        var requests = TrackContext(g);

        g.Pointer(InputAction.Pressed, 150, g.RowY(2));
        g.Pointer(InputAction.LongPress, 150, g.RowY(2));

        Assert.Empty(requests);
        Assert.Equal(new CellPosition(2, 1), g.Selection.CurrentCell);
    }

    [Fact]
    public void LongPress_DuringASecondaryPress_DoesNothing()
    {
        var g = new InputGrid(configure: x => x.Edit.EditTriggers = EditTrigger.LongPress);
        var requests = TrackContext(g);

        g.Pointer(InputAction.Pressed, 150, g.RowY(2), button: PointerButton.Secondary);
        g.Pointer(InputAction.LongPress, 150, g.RowY(2), button: PointerButton.Primary);

        Assert.False(g.Edit.IsEditing);
        Assert.Empty(requests);
    }

    // ── CellTapped / HeaderTapped ──

    [Fact]
    public void PrimaryTap_OnACell_RaisesCellTapped_WithTheColumnItemAndPosition()
    {
        var g = new InputGrid();
        var tapped = new List<KumikoCellTappedEventArgs>();
        g.Input.CellTapped += (_, e) => tapped.Add(e);

        g.Tap(150, g.RowY(3));

        var e = Assert.Single(tapped);
        Assert.Equal((3, 1), (e.RowIndex, e.ColumnIndex));
        Assert.Same(g.Age, e.Column);
        Assert.Same(g.Items[3], e.DataItem);
        Assert.Equal((150f, g.RowY(3)), (e.X, e.Y));
        Assert.Equal(1, e.ClickCount);
    }

    [Fact]
    public void DoubleTap_RaisesCellTappedForEachTap_WithTheClickCount()
    {
        var g = new InputGrid();
        var counts = new List<int>();
        g.Input.CellTapped += (_, e) => counts.Add(e.ClickCount);

        g.Tap(150, g.RowY(3));
        g.Tap(150, g.RowY(3), clicks: 2);

        Assert.Equal(new[] { 1, 2 }, counts);
    }

    [Fact]
    public void PrimaryTap_OnAHeader_SortsAndRaisesHeaderTapped()
    {
        var g = new InputGrid();
        var tapped = new List<KumikoHeaderTappedEventArgs>();
        var directionAtEvent = KumikoSortDirection.None;
        g.Input.HeaderTapped += (_, e) => { tapped.Add(e); directionAtEvent = e.Column.SortDirection; };

        g.Tap(130, InputGrid.HeaderY, InputModifiers.Shift);

        var e = Assert.Single(tapped);
        Assert.Same(g.Age, e.Column);
        Assert.Equal(1, e.ColumnIndex);
        Assert.Equal(InputModifiers.Shift, e.Modifiers);
        Assert.Equal((130f, InputGrid.HeaderY), (e.X, e.Y));
        Assert.Equal(KumikoSortDirection.Ascending, directionAtEvent); // raised after the sort
    }

    [Fact]
    public void PrimaryTap_OnAHeader_RaisesHeaderTapped_EvenWhenSortingIsOff()
    {
        var g = new InputGrid();
        g.Input.AllowSorting = false;
        int tapped = 0;
        g.Input.HeaderTapped += (_, _) => tapped++;

        g.Tap(60, InputGrid.HeaderY);

        Assert.Equal(1, tapped);
        Assert.Equal(KumikoSortDirection.None, g.Name.SortDirection);
    }

    [Fact]
    public void HeaderTapped_IsNotRaised_ForACellTapOrAColumnDrag()
    {
        var g = new InputGrid();
        int tapped = 0;
        g.Input.HeaderTapped += (_, _) => tapped++;
        int cellTaps = 0;
        g.Input.CellTapped += (_, _) => cellTaps++;

        g.Tap(60, g.RowY(1));
        g.Press(60, InputGrid.HeaderY);
        g.Move(160, InputGrid.HeaderY);
        g.Release(160, InputGrid.HeaderY);

        Assert.Equal(0, tapped);
        Assert.Equal(1, cellTaps);
    }
}

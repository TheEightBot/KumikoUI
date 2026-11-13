using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests.Input;

/// <summary>
/// Pointer gestures through <see cref="KumikoInputController.HandlePointer"/>: tap and multi-select,
/// double tap, long press, header sorting, grouping controls, filter popup, wheel and pan scrolling.
/// </summary>
public class PointerGestureTests
{
    // ── Tap and selection ──

    [Fact]
    public void Tap_SelectsTheRowAndMovesTheCurrentCell()
    {
        var g = new InputGrid();

        g.Tap(150, g.RowY(2));

        Assert.Equal(new CellPosition(2, 1), g.Selection.CurrentCell);
        Assert.Equal(new[] { 2 }, g.Selection.SelectedRows);
    }

    [Fact]
    public void Tap_HandlesBothPressAndRelease()
    {
        var g = new InputGrid();

        var press = g.Pointer(InputAction.Pressed, 150, g.RowY(2));
        var release = g.Pointer(InputAction.Released, 150, g.RowY(2));

        Assert.True(press.Handled);
        Assert.True(release.Handled);
    }

    [Fact]
    public void Tap_RaisesRowTapped_WithTheRowColumnAndItem()
    {
        var g = new InputGrid();
        var tapped = new List<RowTappedEventArgs>();
        g.Input.RowTapped += (_, e) => tapped.Add(e);

        g.Tap(150, g.RowY(3));

        var e = Assert.Single(tapped);
        Assert.Equal(3, e.RowIndex);
        Assert.Equal(1, e.ColumnIndex);
        Assert.Same(g.Items[3], e.DataItem);
    }

    [Fact]
    public void Tap_OnHeader_OrEmptyArea_DoesNotRaiseRowTapped()
    {
        var g = new InputGrid(rows: 2, viewportHeight: 400);
        int tapped = 0;
        g.Input.RowTapped += (_, _) => tapped++;

        g.Tap(60, InputGrid.HeaderY);
        g.Tap(60, g.RowY(6));

        Assert.Equal(0, tapped);
        Assert.Empty(g.Selection.SelectedRows);
    }

    [Fact]
    public void CtrlTap_TogglesRowsInTheSelection()
    {
        var g = new InputGrid();
        g.Tap(10, g.RowY(1));

        g.Tap(10, g.RowY(3), InputModifiers.Control);
        Assert.Equal(new[] { 1, 3 }, g.Selection.SelectedRows.OrderBy(r => r));

        g.Tap(10, g.RowY(1), InputModifiers.Control);
        Assert.Equal(new[] { 3 }, g.Selection.SelectedRows);
    }

    [Fact]
    public void ShiftTap_SelectsTheRangeFromTheAnchor()
    {
        var g = new InputGrid();
        g.Tap(10, g.RowY(1));

        g.Tap(10, g.RowY(4), InputModifiers.Shift);

        Assert.Equal(new[] { 1, 2, 3, 4 }, g.Selection.SelectedRows.OrderBy(r => r));
    }

    [Fact]
    public void TapWithoutModifiers_ReplacesTheSelection()
    {
        var g = new InputGrid();
        g.Tap(10, g.RowY(1));
        g.Tap(10, g.RowY(3), InputModifiers.Control);

        g.Tap(10, g.RowY(5));

        Assert.Equal(new[] { 5 }, g.Selection.SelectedRows);
    }

    [Fact]
    public void Tap_OnTheScrolledGrid_UsesTheScrollOffsets()
    {
        var g = new InputGrid();
        g.Scroll.OffsetY = 72;
        g.Scroll.OffsetX = 100;

        g.Tap(50, 40 + 5);

        Assert.Equal(new CellPosition(2, 1), g.Selection.CurrentCell);
    }

    // ── Double tap and edit triggers ──

    [Fact]
    public void DoubleTap_RaisesRowDoubleTapped_AndBeginsAnEdit()
    {
        var g = new InputGrid();
        var doubleTapped = new List<RowTappedEventArgs>();
        g.Input.RowDoubleTapped += (_, e) => doubleTapped.Add(e);

        g.Tap(60, g.RowY(2), clicks: 2);

        var e = Assert.Single(doubleTapped);
        Assert.Equal(2, e.RowIndex);
        Assert.Equal(0, e.ColumnIndex);
        Assert.Same(g.Items[2], e.DataItem);
        Assert.True(g.Edit.IsEditing);
        Assert.Equal(new CellPosition(2, 0), g.Edit.EditCell);
    }

    [Fact]
    public void SingleTap_DoesNotRaiseRowDoubleTapped_OrEdit()
    {
        var g = new InputGrid();
        int doubleTapped = 0;
        g.Input.RowDoubleTapped += (_, _) => doubleTapped++;

        g.Tap(60, g.RowY(2));

        Assert.Equal(0, doubleTapped);
        Assert.False(g.Edit.IsEditing);
    }

    [Fact]
    public void DoubleTap_WithoutTheTrigger_StillRaisesTheEvent()
    {
        var g = new InputGrid(configure: x => x.Edit.EditTriggers = EditTrigger.F2Key);
        int doubleTapped = 0;
        g.Input.RowDoubleTapped += (_, _) => doubleTapped++;

        g.Tap(60, g.RowY(2), clicks: 2);

        Assert.Equal(1, doubleTapped);
        Assert.False(g.Edit.IsEditing);
    }

    [Fact]
    public void DoubleTap_OnAReadOnlyCell_DoesNotEdit()
    {
        var g = new InputGrid(configure: x => x.Name.IsReadOnly = true);

        g.Tap(60, g.RowY(2), clicks: 2);

        Assert.False(g.Edit.IsEditing);
    }

    [Fact]
    public void DoubleTapAction_IsIgnoredByTheController()
    {
        var g = new InputGrid();

        var e = g.Pointer(InputAction.DoubleTap, 60, g.RowY(2), clicks: 2);

        Assert.False(e.Handled);
        Assert.False(g.Edit.IsEditing);
        Assert.False(g.Selection.CurrentCell.IsValid);
    }

    [Fact]
    public void SingleTapTrigger_BeginsAnEditOnTap()
    {
        var g = new InputGrid(configure: x => x.Edit.EditTriggers = EditTrigger.SingleTap);

        g.Tap(60, g.RowY(2));

        Assert.True(g.Edit.IsEditing);
        Assert.Equal(new CellPosition(2, 0), g.Edit.EditCell);
    }

    [Fact]
    public void SingleTapTrigger_ColumnOverride_Wins()
    {
        var g = new InputGrid(configure: x =>
        {
            x.Edit.EditTriggers = EditTrigger.SingleTap;
            x.Name.EditTriggers = EditTrigger.None;
        });

        g.Tap(60, g.RowY(2));

        Assert.False(g.Edit.IsEditing);
    }

    private sealed class Flag
    {
        public bool On { get; set; }
    }

    private static (KumikoInputController input, KumikoSource source, KumikoSelectionModel selection, ScrollState scroll, KumikoStyle style, Flag[] items)
        BooleanGrid(EditTrigger triggers = EditTrigger.Default)
    {
        var items = new[] { new Flag(), new Flag() };
        var column = new KumikoColumn { Header = "On", PropertyName = "On", Width = 100, ColumnType = KumikoColumnType.Boolean };
        var source = new KumikoSource();
        source.SetColumns(new[] { column });
        source.SetItems(items);
        var selection = new KumikoSelectionModel(source);
        var style = new KumikoStyle();
        var scroll = new ScrollState { ViewportWidth = 300, ViewportHeight = 200 };
        var edit = new EditSession { Style = style, EditTriggers = triggers };
        var input = new KumikoInputController { EditSession = edit };
        return (input, source, selection, scroll, style, items);
    }

    [Fact]
    public void SingleTap_OnABooleanCell_TogglesIt()
    {
        var (input, source, selection, scroll, style, items) = BooleanGrid();
        float y = style.HeaderHeight + style.RowHeight / 2;

        foreach (var action in new[] { InputAction.Pressed, InputAction.Released })
            input.HandlePointer(new KumikoPointerEventArgs { X = 50, Y = y, Action = action }, scroll, selection, style, source);

        Assert.True(items[0].On);
        Assert.False(items[1].On);
    }

    [Fact]
    public void LongPress_OnABooleanCell_WithTheTrigger_TogglesIt()
    {
        var (input, source, selection, scroll, style, items) = BooleanGrid(EditTrigger.LongPress);
        float y = style.HeaderHeight + style.RowHeight * 1.5f;

        input.HandlePointer(new KumikoPointerEventArgs { X = 50, Y = y, Action = InputAction.LongPress }, scroll, selection, style, source);

        Assert.True(items[1].On);
        Assert.Equal(new CellPosition(1, 0), selection.CurrentCell);
    }

    // ── Long press ──

    [Fact]
    public void LongPress_SelectsTheCell_AndEditsWithTheTrigger()
    {
        var g = new InputGrid(configure: x => x.Edit.EditTriggers = EditTrigger.LongPress);

        var e = g.Pointer(InputAction.LongPress, 150, g.RowY(4));

        Assert.True(e.Handled);
        Assert.Equal(new CellPosition(4, 1), g.Selection.CurrentCell);
        Assert.True(g.Edit.IsEditing);
    }

    [Fact]
    public void LongPress_WithoutTheTrigger_OnlyMovesTheCurrentCell()
    {
        var g = new InputGrid();

        var e = g.Pointer(InputAction.LongPress, 150, g.RowY(4));

        Assert.True(e.Handled);
        Assert.Equal(new CellPosition(4, 1), g.Selection.CurrentCell);
        Assert.False(g.Edit.IsEditing);
    }

    [Fact]
    public void LongPress_OutsideCells_IsIgnored()
    {
        var g = new InputGrid();

        var e = g.Pointer(InputAction.LongPress, 60, InputGrid.HeaderY);

        Assert.False(e.Handled);
        Assert.False(g.Selection.CurrentCell.IsValid);
    }

    // ── Header: sort ──

    [Fact]
    public void HeaderTap_SortsAndCycles()
    {
        var g = new InputGrid();

        g.Tap(60, InputGrid.HeaderY);
        Assert.Equal(KumikoSortDirection.Ascending, g.Name.SortDirection);
        g.Tap(60, InputGrid.HeaderY);
        Assert.Equal(KumikoSortDirection.Descending, g.Name.SortDirection);
        g.Tap(60, InputGrid.HeaderY);
        Assert.Equal(KumikoSortDirection.None, g.Name.SortDirection);
    }

    [Fact]
    public void HeaderTap_ReplacesTheSortOfOtherColumns()
    {
        var g = new InputGrid();
        g.Tap(60, InputGrid.HeaderY);

        g.Tap(130, InputGrid.HeaderY);

        Assert.Equal(KumikoSortDirection.None, g.Name.SortDirection);
        Assert.Equal(KumikoSortDirection.Ascending, g.Age.SortDirection);
        Assert.False(g.Source.IsMultiSortActive);
    }

    [Theory]
    [InlineData(InputModifiers.Shift)]
    [InlineData(InputModifiers.Control)]
    public void ModifiedHeaderTap_AddsToTheSortChain(InputModifiers modifier)
    {
        var g = new InputGrid();
        g.Tap(60, InputGrid.HeaderY);

        g.Tap(130, InputGrid.HeaderY, modifier);

        Assert.True(g.Source.IsMultiSortActive);
        Assert.Equal(new[] { g.Name, g.Age }, g.Source.ActiveSortColumns);
    }

    [Fact]
    public void HeaderTap_OnAColumnThatForbidsSorting_DoesNothing()
    {
        var g = new InputGrid(configure: x => x.Name.AllowSorting = false);

        var e = g.Pointer(InputAction.Released, 60, InputGrid.HeaderY);

        Assert.True(e.Handled);
        Assert.Equal(KumikoSortDirection.None, g.Name.SortDirection);
    }

    [Fact]
    public void HeaderTap_ReordersRowsByTheSort()
    {
        var g = new InputGrid();

        g.Tap(60, InputGrid.HeaderY);
        g.Tap(60, InputGrid.HeaderY); // descending

        Assert.Equal("N29", g.NameAt(0));
    }

    [Fact]
    public void SecondaryButtonHeaderTap_DoesNotSort()
    {
        var g = new InputGrid();

        g.Pointer(InputAction.Pressed, 60, InputGrid.HeaderY, button: PointerButton.Secondary);
        g.Pointer(InputAction.Released, 60, InputGrid.HeaderY, button: PointerButton.Secondary);

        Assert.Equal(KumikoSortDirection.None, g.Name.SortDirection);
    }

    [Fact]
    public void SecondaryButtonCellTap_OnASelectedRow_KeepsTheMultiSelection()
    {
        var g = new InputGrid();
        g.Tap(60, g.RowY(1));
        g.Tap(60, g.RowY(2), InputModifiers.Control);

        g.Pointer(InputAction.Pressed, 60, g.RowY(2), button: PointerButton.Secondary);
        g.Pointer(InputAction.Released, 60, g.RowY(2), button: PointerButton.Secondary);

        Assert.Equal(new[] { 1, 2 }, g.Selection.SelectedRows.OrderBy(r => r));
    }

    [Fact]
    public void PointerEvents_DefaultToThePrimaryButton()
    {
        var e = new KumikoPointerEventArgs();

        Assert.Equal(PointerButton.Primary, e.Button);
        Assert.Equal(1, e.ClickCount);
        Assert.NotEqual(PointerButton.Secondary, e.Button);
        Assert.NotEqual(PointerButton.Middle, e.Button);
        Assert.NotEqual(PointerButton.None, e.Button);
    }

    [Fact]
    public void PrimaryButtonTap_IsTheDefaultGesture()
    {
        var g = new InputGrid();

        var press = g.Pointer(InputAction.Pressed, 60, g.RowY(2), button: PointerButton.Primary);
        var release = g.Pointer(InputAction.Released, 60, g.RowY(2), button: PointerButton.Primary);

        Assert.True(press.Handled && release.Handled);
        Assert.Equal(new[] { 2 }, g.Selection.SelectedRows);
    }

    // ── Group panel, chevrons ──

    private static InputGrid Grouped() =>
        new(rows: 6, viewportHeight: 500, configure: x => x.Source.AddGroupDescription(new GroupDescription("City")));

    private static float? ChipX(InputGrid g, HitRegion region)
    {
        for (float x = 0; x < 300; x += 2)
            if (g.Hit(x, 20).Region == region) return x;
        return null;
    }

    [Fact]
    public void TapOnGroupChevron_TogglesTheGroup()
    {
        var g = Grouped();
        int expanded = g.Source.RowCount;

        g.Tap(15, 80 + 18);

        Assert.True(g.Source.RowCount < expanded);
        g.Tap(15, 80 + 18);
        Assert.Equal(expanded, g.Source.RowCount);
    }

    [Fact]
    public void TapOnGroupHeaderRow_TogglesTheGroup()
    {
        var g = Grouped();
        int expanded = g.Source.RowCount;

        g.Tap(200, 80 + 18);

        Assert.True(g.Source.RowCount < expanded);
    }

    [Fact]
    public void TapOnGroupChip_TogglesTheGroupSortDirection()
    {
        var g = Grouped();
        float x = ChipX(g, HitRegion.GroupPanelChip)!.Value;
        var description = g.Source.GroupDescriptions[0];
        Assert.Equal(KumikoSortDirection.Ascending, description.GroupSortDirection);

        g.Tap(x, 20);

        Assert.Equal(KumikoSortDirection.Descending, description.GroupSortDirection);
        Assert.Equal("Rome", g.Source.GetGroupHeaderInfo(0)!.Key);
    }

    [Fact]
    public void TapOnGroupChipRemove_RemovesTheGrouping()
    {
        var g = Grouped();
        float x = ChipX(g, HitRegion.GroupPanelChipRemove)!.Value;

        g.Tap(x, 20);

        Assert.False(g.Source.IsGroupingActive);
        Assert.Equal(6, g.Source.RowCount);
    }

    [Fact]
    public void TapOnGroupSummaryRow_IsHandledAndSelectsNothing()
    {
        var g = new InputGrid(rows: 4, viewportHeight: 400, configure: x =>
        {
            x.Source.AddGroupDescription(new GroupDescription("City"));
            x.Source.AddGroupSummaryRow(new GroupSummaryRow("Sub")
            {
                Columns = { new SummaryColumnDescription { PropertyName = "Score", SummaryType = SummaryType.Sum } },
            });
        });

        var press = g.Pointer(InputAction.Pressed, 150, 80 + 3 * 36 + 10);
        var release = g.Pointer(InputAction.Released, 150, 80 + 3 * 36 + 10);

        Assert.True(press.Handled && release.Handled);
        Assert.Empty(g.Selection.SelectedRows);
    }

    [Fact]
    public void TapOnTableSummaryRow_IsHandledAndSelectsNothing()
    {
        var g = new InputGrid(configure: x => x.Source.AddTableSummaryRow(new TableSummaryRow("T", SummaryPosition.Bottom)
        {
            Columns = { new SummaryColumnDescription { PropertyName = "Score", SummaryType = SummaryType.Sum } },
        }));

        var release = g.Pointer(InputAction.Released, 50, 256 - 10);

        Assert.True(release.Handled);
        Assert.Empty(g.Selection.SelectedRows);
    }

    // ── Filter popup ──

    [Fact]
    public void TapOnFilterIcon_OpensThePopup_AndEscapeClosesIt()
    {
        var g = new InputGrid();
        int opened = 0, closed = 0, focus = 0;
        g.Input.FilterPopupOpened += () => opened++;
        g.Input.FilterPopupClosed += () => closed++;
        g.Input.KeyboardFocusRequested += () => focus++;

        g.Tap(180, InputGrid.HeaderY); // Age filter icon

        Assert.Equal(1, opened);
        Assert.True(focus >= 1);
        Assert.True(g.Input.PopupManager.HasActivePopups);
        Assert.NotNull(g.Input.ActiveTextInput);

        var e = g.Key(KumikoKey.Escape);

        Assert.True(e.Handled);
        Assert.Equal(1, closed);
        Assert.False(g.Input.PopupManager.HasActivePopups);
    }

    [Fact]
    public void PressOutsideTheFilterPopup_ClosesItAndIsSwallowed()
    {
        var g = new InputGrid();
        int closed = 0;
        g.Input.FilterPopupClosed += () => closed++;
        g.Tap(180, InputGrid.HeaderY);

        g.Tap(10, 240); // below the popup, over a grid row

        Assert.Equal(1, closed);
        Assert.Empty(g.Selection.SelectedRows);
    }

    [Fact]
    public void FilterIconTap_DoesNotSort()
    {
        var g = new InputGrid();

        g.Tap(180, InputGrid.HeaderY);

        Assert.Equal(KumikoSortDirection.None, g.Age.SortDirection);
    }

    // ── Wheel ──

    [Fact]
    public void Wheel_ScrollsByRowHeightTimesTheMultiplier()
    {
        var g = new InputGrid();

        var e = g.Pointer(InputAction.Scroll, 100, 100, scrollDy: -1);

        Assert.True(e.Handled);
        Assert.Equal(108, g.Scroll.OffsetY); // 36 * 3
        Assert.Equal(0, g.Scroll.OffsetX);
    }

    [Fact]
    public void Wheel_Horizontal_ScrollsTheColumns()
    {
        var g = new InputGrid();

        g.Pointer(InputAction.Scroll, 100, 100, scrollDx: -1);

        Assert.Equal(108, g.Scroll.OffsetX);
    }

    [Fact]
    public void Wheel_UsesTheConfiguredMultiplier()
    {
        var g = new InputGrid();
        g.Input.ScrollSettings = new ScrollSettings { WheelScrollMultiplier = 1 };

        g.Pointer(InputAction.Scroll, 100, 100, scrollDy: -2);

        Assert.Equal(72, g.Scroll.OffsetY);
    }

    [Fact]
    public void Wheel_IsClampedToTheContent()
    {
        var g = new InputGrid();

        g.Pointer(InputAction.Scroll, 100, 100, scrollDy: 5);
        Assert.Equal(0, g.Scroll.OffsetY);
        g.Pointer(InputAction.Scroll, 100, 100, scrollDy: -500);
        Assert.Equal(g.Scroll.MaxOffsetY, g.Scroll.OffsetY);
    }

    [Fact]
    public void ScrollSettings_AssigningNull_FallsBackToDefaults()
    {
        var g = new InputGrid();

        g.Input.ScrollSettings = null!;

        Assert.NotNull(g.Input.ScrollSettings);
        Assert.Equal(3f, g.Input.ScrollSettings.WheelScrollMultiplier);
    }

    // ── Pan and inertia ──

    private static void Pan(InputGrid g, float x, float fromY, float toY, long start = 100, int steps = 4)
    {
        g.Pointer(InputAction.Pressed, x, fromY, timestampMs: start);
        float step = (toY - fromY) / steps;
        for (int i = 1; i <= steps; i++)
            g.Pointer(InputAction.Moved, x, fromY + step * i, timestampMs: start + i * 16);
    }

    [Fact]
    public void Pan_ScrollsTheContentWithTheFinger()
    {
        var g = new InputGrid();
        g.Pointer(InputAction.Pressed, 100, 200, timestampMs: 100);

        g.Pointer(InputAction.Moved, 100, 180, timestampMs: 116); // starts the pan: no jump
        Assert.Equal(0, g.Scroll.OffsetY);
        g.Pointer(InputAction.Moved, 100, 150, timestampMs: 132);
        Assert.Equal(30, g.Scroll.OffsetY);
        g.Pointer(InputAction.Moved, 100, 140, timestampMs: 148);
        Assert.Equal(40, g.Scroll.OffsetY);
    }

    [Fact]
    public void Pan_Horizontally_ScrollsTheColumns()
    {
        var g = new InputGrid();
        g.Pointer(InputAction.Pressed, 250, 150, timestampMs: 100);

        g.Pointer(InputAction.Moved, 220, 150, timestampMs: 116);
        g.Pointer(InputAction.Moved, 180, 150, timestampMs: 132);

        Assert.Equal(40, g.Scroll.OffsetX);
    }

    [Fact]
    public void Pan_BelowTheThreshold_IsATapNotAScroll()
    {
        var g = new InputGrid();
        g.Pointer(InputAction.Pressed, 100, 150, timestampMs: 100);

        g.Pointer(InputAction.Moved, 100, 145, timestampMs: 116);
        g.Pointer(InputAction.Released, 100, 145, timestampMs: 132);

        Assert.Equal(0, g.Scroll.OffsetY);
        Assert.False(g.Input.IsInertialScrolling);
    }

    [Fact]
    public void Pan_ReleaseWhileMoving_StartsInertialScrollThatDecaysToAStop()
    {
        var g = new InputGrid();
        Pan(g, 100, 220, 100);
        float atRelease = g.Scroll.OffsetY;
        Assert.True(atRelease > 0);

        var release = g.Pointer(InputAction.Released, 100, 100, timestampMs: 200);

        Assert.True(release.Handled);
        Assert.True(g.Input.IsInertialScrolling);
        int ticks = 0;
        while (g.Input.UpdateInertialScroll(g.Scroll) && ticks++ < 5000) { }
        Assert.False(g.Input.IsInertialScrolling);
        Assert.True(g.Scroll.OffsetY > atRelease);
        Assert.True(ticks < 5000);
    }

    [Fact]
    public void Pan_ReleaseDoesNotTap()
    {
        var g = new InputGrid();
        Pan(g, 100, 220, 100);

        g.Pointer(InputAction.Released, 100, 100, timestampMs: 200);

        Assert.Empty(g.Selection.SelectedRows);
        Assert.False(g.Selection.CurrentCell.IsValid);
    }

    [Fact]
    public void Inertia_Disabled_StopsWhereTheFingerLifts()
    {
        var g = new InputGrid();
        g.Input.ScrollSettings = new ScrollSettings { EnableInertia = false };
        Pan(g, 100, 220, 100);

        g.Pointer(InputAction.Released, 100, 100, timestampMs: 200);

        Assert.False(g.Input.IsInertialScrolling);
    }

    [Fact]
    public void Inertia_StopsOnTheNextPress()
    {
        var g = new InputGrid();
        Pan(g, 100, 220, 100);
        g.Pointer(InputAction.Released, 100, 100, timestampMs: 200);
        Assert.True(g.Input.IsInertialScrolling);

        g.Pointer(InputAction.Pressed, 100, 100, timestampMs: 300);

        Assert.False(g.Input.IsInertialScrolling);
        Assert.False(g.Input.UpdateInertialScroll(g.Scroll));
    }

    [Fact]
    public void Inertia_StopsOnWheel()
    {
        var g = new InputGrid();
        Pan(g, 100, 220, 100);
        g.Pointer(InputAction.Released, 100, 100, timestampMs: 200);

        g.Pointer(InputAction.Scroll, 100, 100, scrollDy: 0);

        Assert.False(g.Input.IsInertialScrolling);
    }

    [Fact]
    public void Canceled_WhilePanning_KeepsTheMomentum()
    {
        var g = new InputGrid();
        Pan(g, 100, 220, 100);

        var e = g.Pointer(InputAction.Cancelled, 100, 100, timestampMs: 200);

        Assert.True(e.Handled);
        Assert.True(g.Input.IsInertialScrolling);
    }

    [Fact]
    public void Canceled_WithNothingInProgress_IsIgnored()
    {
        var g = new InputGrid();

        var e = g.Pointer(InputAction.Cancelled, 100, 100);

        Assert.False(e.Handled);
    }

    [Fact]
    public void UpdateInertialScroll_WhenIdle_ReturnsFalseAndDoesNotScroll()
    {
        var g = new InputGrid();

        Assert.False(g.Input.UpdateInertialScroll(g.Scroll));
        Assert.Equal(0, g.Scroll.OffsetY);
    }

    [Fact]
    public void HoverMoveWithoutPress_DoesNotScroll()
    {
        var g = new InputGrid();

        g.Pointer(InputAction.Moved, 200, 200, timestampMs: 100);
        g.Pointer(InputAction.Moved, 200, 100, timestampMs: 116);

        Assert.Equal(0, g.Scroll.OffsetY);
        Assert.Equal(0, g.Scroll.OffsetX);
    }

    // ── Cursor feedback ──

    [Fact]
    public void Hover_OverAResizeGrip_RequestsTheResizeCursor()
    {
        var g = new InputGrid();

        g.Move(116, InputGrid.HeaderY); // a move with no press is a hover: on the grip at x 120
        g.Move(60, InputGrid.HeaderY);  // back on the header cell

        Assert.Equal(new[] { KumikoCursor.ResizeHorizontal, KumikoCursor.Default }, g.Cursors.TakeLast(2));
    }
}

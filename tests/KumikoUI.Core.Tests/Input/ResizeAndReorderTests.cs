using KumikoUI.Core.Input;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests.Input;

/// <summary>
/// Drag gestures that raise events: column resize and auto-fit, column header reorder, and row
/// drag-and-drop through the drag handle or the whole row. The controller reports the gesture; the host
/// applies the new order.
/// </summary>
public class ResizeAndReorderTests
{
    // Age spans 120..200, so its resize grip is at x 196 (inside the cell) to 206.
    private const float AgeGripX = 196f;

    // ── Column resize ──

    [Fact]
    public void ResizeDrag_ChangesTheWidth_AndRaisesColumnResizedOnRelease()
    {
        var g = new InputGrid();
        var resized = new List<ColumnResizedEventArgs>();
        g.Input.ColumnResized += (_, e) => resized.Add(e);

        var press = g.Pointer(InputAction.Pressed, AgeGripX, InputGrid.HeaderY);
        var move = g.Pointer(InputAction.Moved, AgeGripX + 30, InputGrid.HeaderY);

        Assert.True(press.Handled && move.Handled);
        Assert.Equal(110, g.Age.Width);
        Assert.Empty(resized); // raised once, when the drag ends

        g.Release(AgeGripX + 30, InputGrid.HeaderY);

        var e = Assert.Single(resized);
        Assert.Equal(1, e.ColumnIndex);
        Assert.Same(g.Age, e.Column);
        Assert.Equal(110, e.Column.Width);
    }

    [Fact]
    public void ResizeDrag_FollowsThePointerFromTheStartWidth()
    {
        var g = new InputGrid();
        g.Press(AgeGripX, InputGrid.HeaderY);

        g.Move(AgeGripX + 50, InputGrid.HeaderY);
        Assert.Equal(130, g.Age.Width);
        g.Move(AgeGripX + 10, InputGrid.HeaderY);
        Assert.Equal(90, g.Age.Width);
    }

    [Fact]
    public void ResizeDrag_IsClampedToMinAndMaxWidth()
    {
        var g = new InputGrid(configure: x => x.Age.MaxWidth = 150);
        g.Press(AgeGripX, InputGrid.HeaderY);

        g.Move(AgeGripX - 500, InputGrid.HeaderY);
        Assert.Equal(g.Age.MinWidth, g.Age.Width);
        g.Move(AgeGripX + 500, InputGrid.HeaderY);
        Assert.Equal(150, g.Age.Width);
    }

    [Fact]
    public void ResizeDrag_UsesTheScrolledGripPosition()
    {
        var g = new InputGrid();
        g.Scroll.OffsetX = 100;
        var resized = new List<ColumnResizedEventArgs>();
        g.Input.ColumnResized += (_, e) => resized.Add(e);

        g.Press(AgeGripX - 100, InputGrid.HeaderY);
        g.Move(AgeGripX - 100 + 20, InputGrid.HeaderY);
        g.Release(AgeGripX - 100 + 20, InputGrid.HeaderY);

        Assert.Equal(100, g.Age.Width);
        Assert.Equal(1, Assert.Single(resized).ColumnIndex);
    }

    [Fact]
    public void ResizeDrag_ShowsTheResizeCursorUntilRelease()
    {
        var g = new InputGrid();

        g.Press(AgeGripX, InputGrid.HeaderY);
        Assert.Equal(KumikoCursor.ResizeHorizontal, g.Input.Cursor);
        g.Move(AgeGripX + 60, InputGrid.HeaderY + 100); // far off the grip, below the header
        Assert.Equal(KumikoCursor.ResizeHorizontal, g.Input.Cursor);
        g.Release(AgeGripX + 60, InputGrid.HeaderY + 100);
        Assert.Equal(KumikoCursor.Default, g.Input.Cursor);
        Assert.Equal(new[] { KumikoCursor.ResizeHorizontal, KumikoCursor.Default }, g.Cursors);
    }

    [Fact]
    public void ResizeDrag_DoesNotSort()
    {
        var g = new InputGrid();

        g.Press(AgeGripX, InputGrid.HeaderY);
        g.Move(AgeGripX + 20, InputGrid.HeaderY);
        g.Release(AgeGripX + 20, InputGrid.HeaderY);

        Assert.Equal(KumikoSortDirection.None, g.Age.SortDirection);
    }

    [Fact]
    public void ResizeDrag_Canceled_RaisesNothing()
    {
        var g = new InputGrid();
        int resized = 0;
        g.Input.ColumnResized += (_, _) => resized++;
        g.Press(AgeGripX, InputGrid.HeaderY);
        g.Move(AgeGripX + 20, InputGrid.HeaderY);

        var e = g.Pointer(InputAction.Cancelled, AgeGripX + 20, InputGrid.HeaderY);

        Assert.True(e.Handled);
        Assert.Equal(0, resized);
        Assert.Equal(KumikoCursor.Default, g.Cursors[^1]);
    }

    [Fact]
    public void ResizeGrip_OnAColumnThatForbidsResize_IsAHeaderTap()
    {
        var g = new InputGrid(configure: x => x.Age.AllowResize = false);

        g.Tap(AgeGripX + 8, InputGrid.HeaderY); // lands on City's header instead

        Assert.Equal(KumikoSortDirection.Ascending, g.City.SortDirection);
        Assert.Equal(80, g.Age.Width);
    }

    [Fact]
    public void DoubleClickOnTheGrip_RequestsAutoFit()
    {
        var g = new InputGrid();
        var requested = new List<AutoFitColumnEventArgs>();
        g.Input.AutoFitColumnRequested += (_, e) => requested.Add(e);

        var press = g.Pointer(InputAction.Pressed, AgeGripX, InputGrid.HeaderY, clicks: 2);

        var e = Assert.Single(requested);
        Assert.True(press.Handled);
        Assert.Equal(1, e.ColumnIndex);
        Assert.Same(g.Age, e.Column);
        Assert.Equal(80, g.Age.Width); // the host does the fitting
    }

    [Fact]
    public void DoubleClickOnTheGrip_DoesNotStartAResize()
    {
        var g = new InputGrid();
        int resized = 0;
        g.Input.ColumnResized += (_, _) => resized++;

        g.Pointer(InputAction.Pressed, AgeGripX, InputGrid.HeaderY, clicks: 2);
        g.Move(AgeGripX + 4, InputGrid.HeaderY);
        g.Release(AgeGripX + 4, InputGrid.HeaderY);

        Assert.Equal(0, resized);
        Assert.Equal(80, g.Age.Width);
    }

    [Fact]
    public void SingleClickOnTheGrip_DoesNotRequestAutoFit()
    {
        var g = new InputGrid();
        int requested = 0;
        g.Input.AutoFitColumnRequested += (_, _) => requested++;

        g.Tap(AgeGripX, InputGrid.HeaderY);

        Assert.Equal(0, requested);
    }

    // ── Column reorder ──

    [Fact]
    public void HeaderDrag_RaisesColumnReordered_WithTheDropSlot()
    {
        var g = new InputGrid();
        var reordered = new List<KumikoColumnReorderedEventArgs>();
        g.Input.ColumnReordered += (_, e) => reordered.Add(e);

        g.Press(60, InputGrid.HeaderY);
        g.Move(70, InputGrid.HeaderY);
        Assert.Equal(0, g.Input.DragColumnIndex);
        g.Move(190, InputGrid.HeaderY);
        Assert.Equal(130, g.Input.DragColumnScreenX); // grabbed 60 px into the column
        Assert.Empty(reordered);
        g.Release(190, InputGrid.HeaderY); // past the middle of Age (160), before the middle of City (250)

        var e = Assert.Single(reordered);
        Assert.Equal(0, e.OldIndex);
        Assert.Equal(1, e.NewIndex); // between Age and City: Columns[1] after the move
        Assert.Same(g.Name, e.Column);
        Assert.Equal(-1, g.Input.DragColumnIndex);
        Assert.Same(g.Name, g.Source.Columns[0]); // the host applies the move
    }

    [Fact]
    public void HeaderDrag_DragOffsetKeepsTheGrabPoint()
    {
        var g = new InputGrid();
        g.Press(60, InputGrid.HeaderY);

        g.Move(80, InputGrid.HeaderY);

        Assert.Equal(20, g.Input.DragColumnScreenX); // grabbed 60 px into Name (x 0), so its left edge is at 80 - 60
    }

    [Fact]
    public void HeaderDrag_ToTheEnd_DropsOnTheLastColumn()
    {
        var g = new InputGrid(viewportWidth: 700);
        var reordered = new List<KumikoColumnReorderedEventArgs>();
        g.Input.ColumnReordered += (_, e) => reordered.Add(e);

        g.Press(60, InputGrid.HeaderY);
        g.Move(120, InputGrid.HeaderY);
        g.Release(690, InputGrid.HeaderY);

        Assert.Equal(4, Assert.Single(reordered).NewIndex);
    }

    [Fact]
    public void HeaderDrag_BackToTheSameSlot_RaisesNothing()
    {
        var g = new InputGrid();
        int reordered = 0;
        g.Input.ColumnReordered += (_, _) => reordered++;

        g.Press(60, InputGrid.HeaderY);
        g.Move(75, InputGrid.HeaderY);
        g.Release(40, InputGrid.HeaderY);

        Assert.Equal(0, reordered);
        Assert.Equal(-1, g.Input.DragColumnIndex);
    }

    [Fact]
    public void HeaderDrag_DoesNotSort()
    {
        var g = new InputGrid();

        g.Press(60, InputGrid.HeaderY);
        g.Move(75, InputGrid.HeaderY);
        g.Release(190, InputGrid.HeaderY);

        Assert.Equal(KumikoSortDirection.None, g.Name.SortDirection);
    }

    [Fact]
    public void HeaderDrag_IncludesTheHorizontalScroll()
    {
        var g = new InputGrid();
        g.Scroll.OffsetX = 100;
        var reordered = new List<KumikoColumnReorderedEventArgs>();
        g.Input.ColumnReordered += (_, e) => reordered.Add(e);

        g.Press(60, InputGrid.HeaderY); // Age at content 160
        g.Move(80, InputGrid.HeaderY);
        g.Release(190, InputGrid.HeaderY); // content 290: past the middle of City (250), before Dept's (350)

        var e = Assert.Single(reordered);
        Assert.Equal(1, e.OldIndex);
        Assert.Equal(2, e.NewIndex); // Name, City, Age, Dept, Score
    }

    [Fact]
    public void HeaderDrag_OnAColumnThatForbidsReorder_Pans()
    {
        var g = new InputGrid(configure: x => x.Name.AllowReorder = false);
        int reordered = 0;
        g.Input.ColumnReordered += (_, _) => reordered++;

        g.Pointer(InputAction.Pressed, 100, InputGrid.HeaderY, timestampMs: 100);
        g.Pointer(InputAction.Moved, 60, InputGrid.HeaderY, timestampMs: 116);
        g.Pointer(InputAction.Moved, 20, InputGrid.HeaderY, timestampMs: 132);

        Assert.Equal(-1, g.Input.DragColumnIndex);
        Assert.Equal(0, reordered);
    }

    [Fact]
    public void HeaderDrag_Vertical_DoesNotStartAReorder()
    {
        var g = new InputGrid();

        g.Press(60, InputGrid.HeaderY);
        g.Move(62, InputGrid.HeaderY + 15);

        Assert.Equal(-1, g.Input.DragColumnIndex);
    }

    [Fact]
    public void HeaderDrag_Canceled_SnapsBackWithoutAnEvent()
    {
        var g = new InputGrid();
        int reordered = 0;
        g.Input.ColumnReordered += (_, _) => reordered++;
        g.Press(60, InputGrid.HeaderY);
        g.Move(75, InputGrid.HeaderY);
        Assert.Equal(0, g.Input.DragColumnIndex);

        var e = g.Pointer(InputAction.Cancelled, 190, InputGrid.HeaderY);

        Assert.True(e.Handled);
        Assert.Equal(-1, g.Input.DragColumnIndex);
        Assert.Equal(0, reordered);
    }

    [Fact]
    public void HeaderDrag_OfAFrozenColumn_ReordersWithinThePane()
    {
        var g = new InputGrid(configure: x =>
        {
            x.Name.IsFrozen = true;
            x.Age.IsFrozen = true;
        });
        var reordered = new List<KumikoColumnReorderedEventArgs>();
        g.Input.ColumnReordered += (_, e) => reordered.Add(e);

        g.Press(150, InputGrid.HeaderY); // Age in the frozen pane (120..200)
        g.Move(130, InputGrid.HeaderY);
        g.Release(20, InputGrid.HeaderY); // over Name, no scroll

        var e = Assert.Single(reordered);
        Assert.Equal(1, e.OldIndex);
        Assert.Equal(0, e.NewIndex);
    }

    [Fact]
    public void HeaderDrag_OfAFrozenColumn_WithHorizontalScroll_DropsOverTheFrozenSlot()
    {
        var g = new InputGrid(configure: x =>
        {
            x.Name.IsFrozen = true;
            x.Age.IsFrozen = true;
        });
        g.Scroll.OffsetX = 100;
        var reordered = new List<KumikoColumnReorderedEventArgs>();
        g.Input.ColumnReordered += (_, e) => reordered.Add(e);

        g.Press(150, InputGrid.HeaderY);
        g.Move(130, InputGrid.HeaderY);
        g.Release(20, InputGrid.HeaderY); // over Name

        var e = Assert.Single(reordered);
        Assert.Equal(1, e.OldIndex);
        Assert.Equal(0, e.NewIndex);
    }

    /// <summary>Records every reorder and applies it the way the hosts do.</summary>
    private static List<KumikoColumnReorderedEventArgs> ApplyReorders(InputGrid g)
    {
        var reordered = new List<KumikoColumnReorderedEventArgs>();
        g.Input.ColumnReordered += (_, e) =>
        {
            reordered.Add(e);
            g.Source.ReorderColumn(e.OldIndex, e.NewIndex);
        };
        return reordered;
    }

    private static void Drag(InputGrid g, float fromX, float toX)
    {
        g.Press(fromX, InputGrid.HeaderY);
        g.Move(fromX + (toX > fromX ? 15 : -15), InputGrid.HeaderY);
        g.Release(toX, InputGrid.HeaderY);
    }

    [Fact]
    public void HeaderDrag_OfAFrozenColumn_OverTheScrollingPane_StaysAtTheEndOfItsPane()
    {
        var g = new InputGrid(configure: x =>
        {
            x.Name.IsFrozen = true;
            x.Age.IsFrozen = true;
        });
        var reordered = ApplyReorders(g);

        Drag(g, 60, 280); // Name over Dept: the pointer is kept at the frozen pane's right edge (200)

        var e = Assert.Single(reordered);
        Assert.Equal(0, e.OldIndex);
        Assert.Equal(1, e.NewIndex);
        Assert.Equal(new[] { g.Age, g.Name, g.City, g.Dept, g.Score }, g.Source.DisplayColumns);
    }

    [Fact]
    public void HeaderDrag_OfAScrollingColumn_WithLeftFrozenPaneAndScroll_NeverTargetsAColumnUnderThePane()
    {
        // Frozen Name + Age (0..200); with 100 px of scroll City is drawn at 100..200, under the frozen pane, Dept at
        // 200..300 and Score at 300..400.
        var g = new InputGrid(viewportWidth: 400, configure: x =>
        {
            x.Name.IsFrozen = true;
            x.Age.IsFrozen = true;
        });
        g.Scroll.OffsetX = 100;
        var reordered = ApplyReorders(g);

        g.Press(330, InputGrid.HeaderY); // Score, grabbed 30 px into it
        g.Move(310, InputGrid.HeaderY);
        Assert.Equal(4, g.Input.DragColumnIndex);
        Assert.Equal(280, g.Input.DragColumnScreenX);
        g.Release(20, InputGrid.HeaderY); // over the frozen pane: kept at the scrolling pane's left edge (200)

        var e = Assert.Single(reordered);
        Assert.Equal(4, e.OldIndex);
        Assert.Equal(3, e.NewIndex); // before Dept, the first column on screen, not before the hidden City
        Assert.Equal(new[] { g.Name, g.Age, g.City, g.Score, g.Dept }, g.Source.DisplayColumns);
    }

    [Fact]
    public void HeaderDrag_OfARightFrozenColumn_ReordersWithinThePane_WhateverTheScroll()
    {
        // Right-frozen Dept + Score at 100..300 (Dept 100..200, Score 200..300); scrolling Name, Age, City at 0..100.
        var g = new InputGrid(configure: x =>
        {
            x.Dept.IsFrozenRight = true;
            x.Score.IsFrozenRight = true;
        });
        g.Scroll.OffsetX = 50;
        var reordered = ApplyReorders(g);

        g.Press(230, InputGrid.HeaderY); // Score, grabbed 30 px into it
        g.Move(215, InputGrid.HeaderY);
        Assert.Equal(185, g.Input.DragColumnScreenX);
        g.Release(120, InputGrid.HeaderY); // before the middle of Dept (150)

        var e = Assert.Single(reordered);
        Assert.Equal(4, e.OldIndex);
        Assert.Equal(3, e.NewIndex);
        Assert.Equal(new[] { g.Name, g.Age, g.City, g.Score, g.Dept }, g.Source.DisplayColumns);
    }

    [Fact]
    public void HeaderDrag_OfARightFrozenColumn_OverTheScrollingPane_StaysAtTheStartOfItsPane()
    {
        var g = new InputGrid(configure: x =>
        {
            x.Dept.IsFrozenRight = true;
            x.Score.IsFrozenRight = true;
        });
        var reordered = ApplyReorders(g);

        Drag(g, 230, 10); // Score over Name: kept at the right-frozen pane's left edge (100)

        Assert.Equal(3, Assert.Single(reordered).NewIndex);
        Assert.Equal(new[] { g.Name, g.Age, g.City, g.Score, g.Dept }, g.Source.DisplayColumns);
    }

    [Fact]
    public void HeaderDrag_OfAScrollingColumn_OverARightFrozenPane_NeverTargetsAColumnUnderThePane()
    {
        // Right-frozen Dept 200..300 and Score 300..400; scrolling Name 0..120, Age 120..200 and City 200..300, which
        // is drawn under the right-frozen pane.
        var g = new InputGrid(viewportWidth: 400, configure: x =>
        {
            x.Dept.IsFrozenRight = true;
            x.Score.IsFrozenRight = true;
        });
        var reordered = ApplyReorders(g);

        Drag(g, 40, 350); // Name over Score: kept at the scrolling pane's right edge (200)

        var e = Assert.Single(reordered);
        Assert.Equal(0, e.OldIndex);
        Assert.Equal(1, e.NewIndex); // after Age, not after the hidden City
        Assert.Equal(new[] { g.Age, g.Name, g.City, g.Dept, g.Score }, g.Source.DisplayColumns);
    }

    [Fact]
    public void HeaderDrag_WithALeftDragHandle_UsesTheShiftedColumns_AndClampsDropsOnTheHandle()
    {
        // The 32 px handle shifts the columns: Name 32..152, Age 152..232, City 232..332.
        var g = new InputGrid(configure: x => x.Style.ShowRowDragHandle = true);
        var reordered = ApplyReorders(g);

        g.Press(92, InputGrid.HeaderY); // Name, grabbed 60 px into it
        g.Move(110, InputGrid.HeaderY);
        Assert.Equal(50, g.Input.DragColumnScreenX);
        g.Release(222, InputGrid.HeaderY); // past Age's middle (192), before City's (282)
        Assert.Equal(1, reordered[^1].NewIndex);
        Assert.Equal(new[] { g.Age, g.Name, g.City, g.Dept, g.Score }, g.Source.Columns);

        Drag(g, 260, 5); // City onto the handle: kept at the columns' left edge, before Age
        Assert.Equal(2, reordered[^1].OldIndex);
        Assert.Equal(0, reordered[^1].NewIndex);
        Assert.Equal(new[] { g.City, g.Age, g.Name, g.Dept, g.Score }, g.Source.Columns);
    }

    [Fact]
    public void HeaderDrag_WithARightDragHandle_KeepsRightFrozenDropsOffTheHandle()
    {
        // 32 px handle on the right; right-frozen Dept 68..168 and Score 168..268.
        var g = new InputGrid(configure: x =>
        {
            x.Style.ShowRowDragHandle = true;
            x.Style.RowDragHandlePosition = DragHandlePosition.Right;
            x.Dept.IsFrozenRight = true;
            x.Score.IsFrozenRight = true;
        });
        var reordered = ApplyReorders(g);

        Drag(g, 100, 295); // Dept onto the handle: kept at the pane's right edge (268), after Score

        var e = Assert.Single(reordered);
        Assert.Equal(3, e.OldIndex);
        Assert.Equal(4, e.NewIndex);
        Assert.Equal(new[] { g.Name, g.Age, g.City, g.Score, g.Dept }, g.Source.DisplayColumns);
    }

    [Fact]
    public void HeaderDrag_SkipsHiddenColumns_AndTheyKeepTheirPlace()
    {
        // Age hidden: Name 0..120, City 120..220, Dept 220..320.
        var g = new InputGrid(configure: x => x.Age.IsVisible = false);
        var reordered = ApplyReorders(g);

        Drag(g, 60, 230); // past City's middle (170), before Dept's (270)

        var e = Assert.Single(reordered);
        Assert.Equal(0, e.OldIndex);
        Assert.Equal(2, e.NewIndex);
        Assert.Equal(new[] { g.Age, g.City, g.Name, g.Dept, g.Score }, g.Source.Columns);
        Assert.Equal(new[] { g.City, g.Name, g.Dept, g.Score }, g.Source.DisplayColumns);
    }

    [Fact]
    public void HeaderDrag_OverItsOwnColumn_RaisesNothing()
    {
        var g = new InputGrid();
        var reordered = ApplyReorders(g);

        Drag(g, 150, 125); // Age, still over Age

        Assert.Empty(reordered);
    }

    // ── Row drag ──

    private static InputGrid WithHandle(int rows = 30, Action<InputGrid>? more = null) =>
        new(rows, configure: x =>
        {
            x.Style.ShowRowDragHandle = true;
            more?.Invoke(x);
        });

    [Fact]
    public void HandleDrag_RaisesRowReordered_WithTheDropRow()
    {
        var g = WithHandle();
        var reordered = new List<RowReorderedEventArgs>();
        g.Input.RowReordered += (_, e) => reordered.Add(e);
        var dragged = g.Items[1];

        g.Press(10, g.RowY(1));
        g.Move(10, g.RowY(1) + 16);
        Assert.Equal(1, g.Input.DragRowIndex);
        g.Move(10, g.RowY(4));
        Assert.Empty(reordered);
        g.Release(10, g.RowY(4));

        var e = Assert.Single(reordered);
        Assert.Equal(1, e.OldIndex);
        Assert.Equal(4, e.NewIndex);
        Assert.Same(dragged, e.DataItem);
        Assert.Equal(KumikoReorderResult.Moved, e.Result);
        Assert.Same(dragged, g.Items[4]); // the controller moved the item in the list before raising the event
        Assert.Equal(-1, g.Input.DragRowIndex);
    }

    [Fact]
    public void HandleDrag_GhostKeepsTheGrabOffset()
    {
        var g = WithHandle();
        g.Press(10, g.RowY(1)); // 18 px below the row top (76)

        g.Move(10, g.RowY(1) + 16);
        Assert.Equal(g.RowY(1) + 16 - 18, g.Input.DragRowScreenY);
        g.Move(10, 200);
        Assert.Equal(182, g.Input.DragRowScreenY);
    }

    [Fact]
    public void HandleDrag_GhostAccountsForTheVerticalScroll()
    {
        var g = WithHandle();
        g.Scroll.OffsetY = 72;
        g.Press(10, 40 + 18); // row 2 on screen, 18 px below its top

        g.Move(10, 40 + 18 + 16);

        Assert.Equal(2, g.Input.DragRowIndex);
        Assert.Equal(40 + 16, g.Input.DragRowScreenY);
    }

    [Fact]
    public void HandleDrag_DropOnItself_RaisesNothing()
    {
        var g = WithHandle();
        int reordered = 0;
        g.Input.RowReordered += (_, _) => reordered++;

        g.Press(10, g.RowY(2));
        g.Move(10, g.RowY(2) + 12);
        g.Release(10, g.RowY(2) + 5);

        Assert.Equal(0, reordered);
        Assert.Equal(-1, g.Input.DragRowIndex);
    }

    [Fact]
    public void HandleDrag_DropIsClampedToTheFirstAndLastVisibleRow()
    {
        // 10 rows, 6 on screen: a drop above or below the grid lands on the first or last row on screen (edge
        // autoscroll brings the others into view first).
        var g = WithHandle(rows: 10);
        var reordered = new List<RowReorderedEventArgs>();
        g.Input.RowReordered += (_, e) => reordered.Add(e);

        g.Press(10, g.RowY(3));
        g.Move(10, g.RowY(3) + 12);
        g.Release(10, -50);
        g.Press(10, g.RowY(3));
        g.Move(10, g.RowY(3) + 12);
        g.Release(10, 5000);

        Assert.Equal(new[] { 0, 5 }, reordered.Select(e => e.NewIndex));
    }

    [Fact]
    public void HandleDrag_DropIncludesTheVerticalScroll()
    {
        var g = WithHandle();
        g.Scroll.OffsetY = 72;
        var reordered = new List<RowReorderedEventArgs>();
        g.Input.RowReordered += (_, e) => reordered.Add(e);

        g.Press(10, 40 + 18);
        g.Move(10, 40 + 18 + 16);
        g.Release(10, 202); // (202 - 40 + 72) / 36 = row 6

        var e = Assert.Single(reordered);
        Assert.Equal(2, e.OldIndex);
        Assert.Equal(6, e.NewIndex);
    }

    [Fact]
    public void HandleDrag_DropOverFrozenRows_UsesTheFixedBand()
    {
        var g = WithHandle(more: x => x.Source.FrozenRowCount = 2);
        g.Scroll.OffsetY = 360;
        var reordered = new List<RowReorderedEventArgs>();
        g.Input.RowReordered += (_, e) => reordered.Add(e);

        g.Press(10, 40 + 72 + 18); // first scrollable row: index 12
        g.Move(10, 40 + 72 + 18 + 16);
        Assert.Equal(12, g.Input.DragRowIndex);
        g.Release(10, 40 + 36 + 5); // second frozen row

        var e = Assert.Single(reordered);
        Assert.Equal(12, e.OldIndex);
        Assert.Equal(1, e.NewIndex);
    }

    [Fact]
    public void HandleDrag_StartsAsSoonAsItMoves_InAnyDirection()
    {
        // The drag handle exists to drag: no threshold, no direction test (#99).
        var g = WithHandle();

        g.Press(10, g.RowY(1));
        g.Move(14, g.RowY(1) + 1);

        Assert.Equal(1, g.Input.DragRowIndex);
        Assert.Equal(0, g.Scroll.OffsetX);
        Assert.Equal(0, g.Scroll.OffsetY);
    }

    [Fact]
    public void HandleDrag_Canceled_SnapsBackWithoutAnEvent()
    {
        var g = WithHandle();
        int reordered = 0;
        g.Input.RowReordered += (_, _) => reordered++;
        g.Press(10, g.RowY(1));
        g.Move(10, g.RowY(1) + 16);

        var e = g.Pointer(InputAction.Cancelled, 10, g.RowY(4));

        Assert.True(e.Handled);
        Assert.Equal(-1, g.Input.DragRowIndex);
        Assert.Equal(0, reordered);
    }

    [Fact]
    public void RowDragDrop_FromACell_WorksWhenEnabled()
    {
        var g = new InputGrid(configure: x => x.Style.AllowRowDragDrop = true);
        var reordered = new List<RowReorderedEventArgs>();
        g.Input.RowReordered += (_, e) => reordered.Add(e);

        g.Press(150, g.RowY(1));
        g.Move(150, g.RowY(1) + 16);
        g.Release(150, g.RowY(3));

        var e = Assert.Single(reordered);
        Assert.Equal(1, e.OldIndex);
        Assert.Equal(3, e.NewIndex);
    }

    [Fact]
    public void CellDrag_WhenRowDragIsDisabled_Pans()
    {
        var g = new InputGrid();
        int reordered = 0;
        g.Input.RowReordered += (_, _) => reordered++;

        g.Pointer(InputAction.Pressed, 150, 200, timestampMs: 100);
        g.Pointer(InputAction.Moved, 150, 180, timestampMs: 116);
        g.Pointer(InputAction.Moved, 150, 150, timestampMs: 132);
        g.Pointer(InputAction.Released, 150, 150, timestampMs: 148);

        Assert.Equal(-1, g.Input.DragRowIndex);
        Assert.Equal(0, reordered);
        Assert.True(g.Scroll.OffsetY > 0);
    }

    [Fact]
    public void RowDrag_RightHandle_Works()
    {
        var g = new InputGrid(configure: x =>
        {
            x.Style.ShowRowDragHandle = true;
            x.Style.RowDragHandlePosition = DragHandlePosition.Right;
        });
        var reordered = new List<RowReorderedEventArgs>();
        g.Input.RowReordered += (_, e) => reordered.Add(e);

        g.Press(290, g.RowY(2));
        g.Move(290, g.RowY(2) + 16);
        g.Release(290, g.RowY(0));

        var e = Assert.Single(reordered);
        Assert.Equal(2, e.OldIndex);
        Assert.Equal(0, e.NewIndex);
    }

    [Fact]
    public void RowDrag_OntoAGroupHeader_IsIgnored()
    {
        var g = new InputGrid(rows: 4, viewportHeight: 500, configure: x =>
        {
            x.Style.AllowRowDragDrop = true;
            x.Source.AddGroupDescription(new GroupDescription("City"));
        });
        var reordered = new List<RowReorderedEventArgs>();
        var rejected = new List<RowReorderedEventArgs>();
        g.Input.RowReordered += (_, e) => reordered.Add(e);
        g.Input.RowReorderRejected += (_, e) => rejected.Add(e);
        // Rows: 0 Oslo header, 1, 2 items, 3 Rome header, 4, 5 items; data starts below the 40 px panel and header.

        g.Press(150, 80 + 36 + 18);
        g.Move(150, 80 + 36 + 34);
        g.Release(150, 80 + 3 * 36 + 10); // onto the Rome header

        Assert.Empty(reordered);
        Assert.Equal(KumikoReorderResult.OutOfRange, Assert.Single(rejected).Result);

        g.Press(150, 80 + 36 + 18);
        g.Move(150, 80 + 36 + 34);
        g.Release(150, 80 + 2 * 36 + 10); // onto the other Oslo item

        var e = Assert.Single(reordered);
        Assert.Equal(1, e.OldIndex);
        Assert.Equal(2, e.NewIndex);
    }

    [Fact]
    public void RowDrag_UnderGrouping_GhostKeepsTheGrabOffset()
    {
        var g = new InputGrid(rows: 4, viewportHeight: 500, configure: x =>
        {
            x.Style.AllowRowDragDrop = true;
            x.Source.AddGroupDescription(new GroupDescription("City"));
        });

        g.Press(150, 80 + 36 + 18); // row 1: top at 116, grabbed 18 px below it
        g.Move(150, 80 + 36 + 34);

        Assert.Equal(1, g.Input.DragRowIndex);
        Assert.Equal(80 + 36 + 34 - 18, g.Input.DragRowScreenY);
    }
}

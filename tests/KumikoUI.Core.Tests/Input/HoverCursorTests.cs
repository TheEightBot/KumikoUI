using System.Windows.Input;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Tests.Input;

/// <summary>
/// The cursor the controller asks the host for (#181): a resize cursor over a resize grip and for the whole of a
/// resize drag, a hand over what a click acts on directly, the default elsewhere; <c>CursorChanged</c> only on
/// a change; hover never scrolls or selects; leaving the grid resets it.
/// </summary>
public class HoverCursorTests
{
    // Age spans 120..200: its filter icon covers 168..194, its resize grip 194..206.
    private const float AgeGripX = 196f;
    private const float AgeFilterIconX = 180f;
    private const float AgeHeaderX = 140f;

    // ── Regions ──

    [Fact]
    public void Hover_ReportsTheCursorOfEachHeaderAndCellRegion()
    {
        var g = new InputGrid();

        Assert.Equal(KumikoCursor.Default, Hover(g, AgeHeaderX, InputGrid.HeaderY));
        Assert.Equal(KumikoCursor.ResizeHorizontal, Hover(g, AgeGripX, InputGrid.HeaderY));
        Assert.Equal(KumikoCursor.Hand, Hover(g, AgeFilterIconX, InputGrid.HeaderY));
        Assert.Equal(KumikoCursor.Default, Hover(g, 50, g.RowY(1)));
    }

    [Fact]
    public void Hover_BelowTheLastRow_IsDefault()
    {
        var g = new InputGrid(rows: 2);
        Hover(g, AgeGripX, InputGrid.HeaderY);

        Assert.Equal(KumikoCursor.Default, Hover(g, 50, g.RowY(4)));
    }

    [Fact]
    public void Hover_OnTheGripOfAColumnThatForbidsResize_IsDefault()
    {
        var g = new InputGrid(configure: x => x.Age.AllowResize = false);

        Assert.Equal(KumikoCursor.Default, Hover(g, AgeGripX, InputGrid.HeaderY));
        Assert.Empty(g.Cursors);
    }

    [Fact]
    public void Hover_OnTheGripOfAFixedWidthColumn_IsDefault_AndAPressThereDoesNotResize()
    {
        var g = new InputGrid(configure: x => { x.Age.MinWidth = 80; x.Age.MaxWidth = 80; });

        Assert.Equal(KumikoCursor.Default, Hover(g, AgeGripX, InputGrid.HeaderY));
        Assert.Equal(HitRegion.HeaderCell, g.Hit(AgeGripX, InputGrid.HeaderY).Region);
    }

    [Fact]
    public void Hover_OnTheFilterIconOfAColumnWithoutFiltering_IsDefault()
    {
        var g = new InputGrid(configure: x => x.Age.AllowFiltering = false);

        Assert.Equal(KumikoCursor.Default, Hover(g, AgeFilterIconX, InputGrid.HeaderY));
    }

    [Fact]
    public void Hover_OnTheRowDragHandle_IsAHand()
    {
        var g = new InputGrid(configure: x => x.Style.ShowRowDragHandle = true);

        Assert.Equal(KumikoCursor.Hand, Hover(g, 10, g.RowY(1)));
        Assert.Equal(KumikoCursor.Default, Hover(g, 10, InputGrid.HeaderY)); // the header above it
        Assert.Equal(KumikoCursor.Default, Hover(g, 60, g.RowY(1)));
    }

    [Fact]
    public void Hover_OnAGroupChevronOrGroupingChip_IsAHand_AndOnTheRestOfTheGroupRow_IsDefault()
    {
        var g = new InputGrid(rows: 4, viewportHeight: 400,
            configure: x => x.Source.AddGroupDescription(new GroupDescription("City")));
        float firstRowY = 40 + 40 + 18; // grouping panel + header, middle of the first (group header) row

        Assert.Equal(KumikoCursor.Hand, Hover(g, 15, firstRowY));
        Assert.Equal(KumikoCursor.Default, Hover(g, 150, firstRowY));

        float chipX = FirstX(g, 20, HitRegion.GroupPanelChip);
        float removeX = FirstX(g, 20, HitRegion.GroupPanelChipRemove);
        Assert.Equal(KumikoCursor.Hand, Hover(g, chipX, 20));
        Assert.Equal(KumikoCursor.Hand, Hover(g, removeX, 20));
        Assert.Equal(KumikoCursor.Default, Hover(g, 2, 20)); // the panel's label
    }

    [Fact]
    public void Hover_OnAnEditableCheckBox_IsAHand()
    {
        var g = BooleanColumnGrid.Create();

        Assert.Equal(KumikoCursor.Hand, Hover(g, 50, g.RowY(1)));
        Assert.Equal(KumikoCursor.Default, Hover(g, 10, g.RowY(1)));
    }

    [Fact]
    public void Hover_OnTheCheckBoxOfAReadOnlyOrNonEditableColumn_IsDefault()
    {
        var readOnly = BooleanColumnGrid.Create(configure: c => c.IsReadOnly = true);
        var noTriggers = BooleanColumnGrid.Create(EditTrigger.None);

        Assert.Equal(KumikoCursor.Default, Hover(readOnly, 50, readOnly.RowY(1)));
        Assert.Equal(KumikoCursor.Default, Hover(noTriggers, 50, noTriggers.RowY(1)));
    }

    [Fact]
    public void Hover_OnAnEnabledActionButton_IsAHand_AndOnADisabledOneOrTheGap_IsDefault()
    {
        // The Name column holds the buttons: cell x 0-120, two buttons at 12-57 and 63-108.
        var disabled = new FixedCommand(canExecute: false);
        var g = new InputGrid(configure: x =>
        {
            x.Name.PropertyName = "";
            x.Name.ColumnType = KumikoColumnType.Template;
            x.Name.CustomCellRenderer = new ActionButtonsCellRenderer
            {
                Buttons =
                {
                    new ActionButtonDefinition { Label = "Open" },
                    new ActionButtonDefinition { Label = "Delete", Command = disabled },
                },
            };
        });

        Assert.Equal(KumikoCursor.Hand, Hover(g, 30, g.RowY(1)));
        Assert.Equal(KumikoCursor.Default, Hover(g, 60, g.RowY(1)));
        Assert.Equal(KumikoCursor.Default, Hover(g, 90, g.RowY(1)));

        disabled.Enabled = true;
        Assert.Equal(KumikoCursor.Hand, Hover(g, 90, g.RowY(1)));
    }

    [Fact]
    public void Hover_OverAnOpenPopup_IsDefault_AndOutsideItFollowsTheGrid()
    {
        var g = new InputGrid();
        g.Tap(AgeFilterIconX, InputGrid.HeaderY); // opens the filter popup below the header
        Assert.True(g.Input.PopupManager.HasActivePopups);
        var (px, py) = PointInPopup(g);

        Assert.Equal(KumikoCursor.ResizeHorizontal, Hover(g, AgeGripX, InputGrid.HeaderY));
        Assert.Equal(KumikoCursor.Default, Hover(g, px, py));
    }

    // ── Change notifications ──

    [Fact]
    public void CursorChanged_IsRaisedOnlyWhenTheCursorChanges()
    {
        var g = new InputGrid();

        Hover(g, AgeGripX, InputGrid.HeaderY);
        Hover(g, AgeGripX + 2, InputGrid.HeaderY);      // still the grip
        Hover(g, AgeHeaderX, InputGrid.HeaderY);
        Hover(g, AgeHeaderX + 5, InputGrid.HeaderY);    // still the header
        Hover(g, 50, g.RowY(2));                        // a cell: also default
        Hover(g, AgeFilterIconX, InputGrid.HeaderY);

        Assert.Equal(new[] { KumikoCursor.ResizeHorizontal, KumikoCursor.Default, KumikoCursor.Hand }, g.Cursors);
    }

    [Fact]
    public void Hover_NeverScrollsSelectsOrIsHandled()
    {
        var g = new InputGrid();

        var first = g.Hover(200, 200);
        var second = g.Hover(200, 60);
        g.Hover(20, 220);

        Assert.False(first.Handled || second.Handled);
        Assert.Equal(0, g.Scroll.OffsetX);
        Assert.Equal(0, g.Scroll.OffsetY);
        Assert.False(g.Selection.CurrentCell.IsValid);
        Assert.Empty(g.Selection.SelectedItems);
    }

    [Fact]
    public void AMoveWithNoPressInProgress_IsAHover()
    {
        var g = new InputGrid();
        g.Tap(50, g.RowY(1)); // a press and release: no press in progress afterward

        g.Move(AgeGripX, InputGrid.HeaderY);
        g.Move(AgeGripX, 200);

        Assert.Equal(KumikoCursor.Default, g.Input.Cursor);
        Assert.Equal(new[] { KumikoCursor.ResizeHorizontal, KumikoCursor.Default }, g.Cursors);
        Assert.Equal(0, g.Scroll.OffsetY);
    }

    // ── Resize drag ──

    [Fact]
    public void ResizeDrag_KeepsTheResizeCursorOffTheGrip_AndHoverOrExitDoNotChangeIt()
    {
        var g = new InputGrid();
        Hover(g, AgeGripX, InputGrid.HeaderY);
        g.Press(AgeGripX, InputGrid.HeaderY);

        g.Move(AgeGripX - 60, 200);                       // over a data cell
        g.Hover(AgeGripX - 60, 200);                      // a host that reports a stray hover
        g.Pointer(InputAction.Exited, -10, 200);          // and the pointer leaves the grid

        Assert.Equal(KumikoCursor.ResizeHorizontal, g.Input.Cursor);
        Assert.Equal(new[] { KumikoCursor.ResizeHorizontal }, g.Cursors);
        Assert.Equal(g.Age.MinWidth, g.Age.Width);          // the drag went on
    }

    [Fact]
    public void ResizeDrag_ReleasedOffTheGrip_ReturnsToTheCursorUnderThePointer()
    {
        var g = new InputGrid();
        g.Press(AgeGripX, InputGrid.HeaderY);
        g.Move(AgeGripX - 10, g.RowY(2));

        g.Release(AgeGripX - 10, g.RowY(2));

        Assert.Equal(KumikoCursor.Default, g.Input.Cursor);
        Assert.Equal(new[] { KumikoCursor.ResizeHorizontal, KumikoCursor.Default }, g.Cursors);
    }

    [Fact]
    public void ResizeDrag_ReleasedOnTheMovedGrip_KeepsTheResizeCursor()
    {
        var g = new InputGrid();
        g.Press(AgeGripX, InputGrid.HeaderY);
        g.Move(AgeGripX + 20, InputGrid.HeaderY); // Age is now 100 wide: its grip followed the pointer

        g.Release(AgeGripX + 20, InputGrid.HeaderY);

        Assert.Equal(KumikoCursor.ResizeHorizontal, g.Input.Cursor);
        Assert.Equal(new[] { KumikoCursor.ResizeHorizontal }, g.Cursors);
    }

    // ── Leaving the grid ──

    [Fact]
    public void Exited_ResetsTheCursor()
    {
        var g = new InputGrid();
        Hover(g, AgeGripX, InputGrid.HeaderY);

        var exited = g.Pointer(InputAction.Exited, AgeGripX, -1);

        Assert.Equal(KumikoCursor.Default, g.Input.Cursor);
        Assert.Equal(new[] { KumikoCursor.ResizeHorizontal, KumikoCursor.Default }, g.Cursors);
        Assert.False(exited.Handled);
    }

    [Fact]
    public void Exited_DoesNotCancelARowDrag()
    {
        var g = new InputGrid(configure: x => x.Style.ShowRowDragHandle = true);
        g.Press(10, g.RowY(1));
        g.Move(10, g.RowY(3));
        Assert.Equal(1, g.Input.DragRowIndex);

        g.Pointer(InputAction.Exited, 10, 400);

        Assert.Equal(1, g.Input.DragRowIndex);
    }

    // ── Allocation ──

    [Fact]
    public void Hover_DoesNotAllocate()
    {
        var g = new InputGrid(configure: x => x.Style.ShowRowDragHandle = true);
        var events = new[]
        {
            new KumikoPointerEventArgs { X = AgeGripX, Y = InputGrid.HeaderY, Action = InputAction.Hover },
            new KumikoPointerEventArgs { X = AgeHeaderX, Y = InputGrid.HeaderY, Action = InputAction.Hover },
            new KumikoPointerEventArgs { X = AgeFilterIconX, Y = InputGrid.HeaderY, Action = InputAction.Hover },
            new KumikoPointerEventArgs { X = 10, Y = g.RowY(2), Action = InputAction.Hover },
            new KumikoPointerEventArgs { X = 150, Y = g.RowY(2), Action = InputAction.Hover },
        };
        g.Cursors.Capacity = 10_000; // the test's own CursorChanged log must not grow during the measurement
        foreach (var e in events) g.Input.HandlePointer(e, g.Scroll, g.Selection, g.Style, g.Source); // warm up

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 200; i++)
            foreach (var e in events)
                g.Input.HandlePointer(e, g.Scroll, g.Selection, g.Style, g.Source);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    // ── Helpers ──

    private static KumikoCursor Hover(InputGrid g, float x, float y)
    {
        g.Hover(x, y);
        return g.Input.Cursor;
    }

    private static KumikoCursor Hover(BooleanColumnGrid g, float x, float y)
    {
        g.Input.HandlePointer(new KumikoPointerEventArgs { X = x, Y = y, Action = InputAction.Hover },
            g.Scroll, g.Selection, g.Style, g.Source);
        return g.Input.Cursor;
    }

    private static float FirstX(InputGrid g, float y, HitRegion region)
    {
        for (float x = 0; x < InputGrid.ViewportWidth; x += 1)
            if (g.Hit(x, y).Region == region) return x;
        throw new InvalidOperationException($"No {region} at y {y}");
    }

    private static (float X, float Y) PointInPopup(InputGrid g)
    {
        for (float y = 0; y < g.Scroll.ViewportHeight; y += 4)
            for (float x = 0; x < InputGrid.ViewportWidth; x += 4)
                if (g.Input.PopupManager.HitTest(x, y) != null && y > 60) return (x, y);
        throw new InvalidOperationException("The popup is not on screen");
    }

    private sealed class FixedCommand(bool canExecute) : ICommand
    {
        public bool Enabled { get; set; } = canExecute;
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => Enabled;
        public void Execute(object? parameter) { }
    }
}

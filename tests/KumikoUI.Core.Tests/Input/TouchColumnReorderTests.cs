using KumikoUI.Core.Input;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests.Input;

/// <summary>
/// Column reorder with a finger (#276): the press-and-drag path with touch pointers, a slow drag in small steps, and
/// what a long press on a header does instead (a context request, not a drag).
/// </summary>
public class TouchColumnReorderTests
{
    private const KumikoPointerDeviceType Touch = KumikoPointerDeviceType.Touch;

    [Fact]
    public void SlowTouchDrag_InSmallSteps_StartsAColumnDrag_AndReordersOnRelease()
    {
        var g = new InputGrid();
        var reordered = new List<KumikoColumnReorderedEventArgs>();
        g.Input.ColumnReordered += (_, e) => reordered.Add(e);

        long t = 0;
        g.Pointer(InputAction.Pressed, 60, InputGrid.HeaderY, timestampMs: t, deviceType: Touch);
        // A finger crawls: 4 px per sample, 80 ms apart. The drag starts once the finger has moved past the slop.
        float x = 60;
        for (int i = 0; i < 33; i++)
        {
            x += 4;
            t += 80;
            g.Pointer(InputAction.Moved, x, InputGrid.HeaderY, timestampMs: t, deviceType: Touch);
        }
        Assert.Equal(0, g.Input.DragColumnIndex);
        g.Pointer(InputAction.Released, x, InputGrid.HeaderY, timestampMs: t + 80, deviceType: Touch);

        var e = Assert.Single(reordered);
        Assert.Equal(0, e.OldIndex);
        Assert.Same(g.Name, e.Column);
        Assert.Equal(-1, g.Input.DragColumnIndex);
    }

    [Fact]
    public void TouchDrag_WithJitterBeforeTheMove_StillStartsAColumnDrag()
    {
        var g = new InputGrid();
        g.Pointer(InputAction.Pressed, 60, InputGrid.HeaderY, deviceType: Touch);
        g.Pointer(InputAction.Moved, 61, InputGrid.HeaderY + 1, deviceType: Touch);
        g.Pointer(InputAction.Moved, 59, InputGrid.HeaderY, deviceType: Touch);

        Assert.Equal(-1, g.Input.DragColumnIndex); // within the slop: still a press

        g.Pointer(InputAction.Moved, 80, InputGrid.HeaderY, deviceType: Touch);

        Assert.Equal(0, g.Input.DragColumnIndex);
    }

    [Fact]
    public void TouchLongPress_OnAHeader_ThenDrag_ReordersTheColumn_AndRaisesNoContextRequest()
    {
        // The host fires LongPress after 500 ms without movement; a finger that holds the header and then drags
        // lifts the column like a row long press lifts a row, and the menu does not open (#288).
        var g = new InputGrid();
        int requests = 0;
        var reordered = new List<KumikoColumnReorderedEventArgs>();
        g.Input.ContextRequested += (_, _) => requests++;
        g.Input.ColumnReordered += (_, e) => reordered.Add(e);

        g.Pointer(InputAction.Pressed, 60, InputGrid.HeaderY, deviceType: Touch);
        g.Pointer(InputAction.LongPress, 60, InputGrid.HeaderY, deviceType: Touch);
        Assert.Equal(-1, g.Input.DragColumnIndex); // held, not yet dragging
        g.Pointer(InputAction.Moved, 66, InputGrid.HeaderY, deviceType: Touch);
        Assert.Equal(-1, g.Input.DragColumnIndex); // within the slop
        g.Pointer(InputAction.Moved, 100, InputGrid.HeaderY, deviceType: Touch);
        Assert.Equal(0, g.Input.DragColumnIndex);
        g.Pointer(InputAction.Moved, 200, InputGrid.HeaderY, deviceType: Touch);
        g.Pointer(InputAction.Released, 200, InputGrid.HeaderY, deviceType: Touch);

        Assert.Equal(0, requests);
        var e = Assert.Single(reordered);
        Assert.Equal(0, e.OldIndex);
        Assert.Same(g.Name, e.Column);
        Assert.Equal(-1, g.Input.DragColumnIndex);
        Assert.Equal(KumikoSortDirection.None, g.Name.SortDirection);
    }

    [Fact]
    public void TouchLongPress_OnAHeader_ThenRelease_RaisesTheContextRequestOnRelease()
    {
        var g = new InputGrid();
        var requests = new List<KumikoContextRequestedEventArgs>();
        g.Input.ContextRequested += (_, e) => requests.Add(e);

        g.Pointer(InputAction.Pressed, 60, InputGrid.HeaderY, deviceType: Touch);
        g.Pointer(InputAction.LongPress, 60, InputGrid.HeaderY, deviceType: Touch);
        Assert.Empty(requests); // not while the finger is down: it may still drag
        g.Pointer(InputAction.Released, 61, InputGrid.HeaderY, deviceType: Touch);

        var e = Assert.Single(requests);
        Assert.Equal(KumikoContextTrigger.LongPress, e.Trigger);
        Assert.True(e.IsHeader);
        Assert.Same(g.Name, e.Column);
        Assert.Equal(-1, g.Input.DragColumnIndex);
        Assert.Equal(KumikoSortDirection.None, g.Name.SortDirection);
    }

    [Fact]
    public void TouchLongPress_OnAHeader_ThenCancel_RaisesNothing()
    {
        var g = new InputGrid();
        int requests = 0;
        g.Input.ContextRequested += (_, _) => requests++;

        g.Pointer(InputAction.Pressed, 60, InputGrid.HeaderY, deviceType: Touch);
        g.Pointer(InputAction.LongPress, 60, InputGrid.HeaderY, deviceType: Touch);
        g.Pointer(InputAction.Cancelled, 60, InputGrid.HeaderY, deviceType: Touch);
        // The next press is a plain tap again
        g.Pointer(InputAction.Pressed, 60, InputGrid.HeaderY, deviceType: Touch);
        g.Pointer(InputAction.Released, 60, InputGrid.HeaderY, deviceType: Touch);

        Assert.Equal(0, requests);
        Assert.NotEqual(KumikoSortDirection.None, g.Name.SortDirection);
    }

    [Fact]
    public void TouchLongPress_OnAHeaderThatCannotReorder_RaisesTheContextRequestAtOnce()
    {
        var g = new InputGrid();
        g.Name.AllowReorder = false;
        int requests = 0;
        g.Input.ContextRequested += (_, _) => requests++;

        g.Pointer(InputAction.Pressed, 60, InputGrid.HeaderY, deviceType: Touch);
        g.Pointer(InputAction.LongPress, 60, InputGrid.HeaderY, deviceType: Touch);

        Assert.Equal(1, requests);
        Assert.Equal(-1, g.Input.DragColumnIndex);
    }

    [Fact]
    public void QuickTouchPressAndDrag_BeforeTheLongPress_StillReordersAsBefore()
    {
        var g = new InputGrid();
        int requests = 0;
        g.Input.ContextRequested += (_, _) => requests++;
        g.Pointer(InputAction.Pressed, 60, InputGrid.HeaderY, deviceType: Touch);
        g.Pointer(InputAction.Moved, 90, InputGrid.HeaderY, deviceType: Touch);

        Assert.Equal(0, g.Input.DragColumnIndex);
        g.Pointer(InputAction.Released, 90, InputGrid.HeaderY, deviceType: Touch);
        Assert.Equal(0, requests);
    }
}

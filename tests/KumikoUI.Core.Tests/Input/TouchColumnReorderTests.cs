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
    public void TouchLongPress_OnAHeader_IsAContextRequest_NotADragStart()
    {
        // The host fires LongPress after 500 ms without movement; a finger that holds the header that long gets the
        // header menu, and the drag that follows the hold does not start a reorder.
        var g = new InputGrid();
        int requests = 0;
        g.Input.ContextRequested += (_, _) => requests++;
        g.Pointer(InputAction.Pressed, 60, InputGrid.HeaderY, deviceType: Touch);
        g.Pointer(InputAction.LongPress, 60, InputGrid.HeaderY, deviceType: Touch);

        Assert.Equal(1, requests);
        Assert.Equal(-1, g.Input.DragColumnIndex);
    }
}

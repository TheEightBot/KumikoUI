using KumikoUI.Core.Input;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests.Input;

/// <summary>
/// Where the filter popup opens (#228): under its column's header, with the row drag handle taking width at the
/// start or the end of the viewport. A wide viewport keeps the popup from being clamped to the edge.
/// </summary>
public class FilterPopupAnchorTests
{
    private const float Wide = 900f;
    private const float HandleWidth = 30f;

    // Age spans content x 120..200, so with no handle its header starts at 120.
    private static InputGrid CreateGrid(bool showHandle, DragHandlePosition position, Action<InputGrid>? configure = null) =>
        new(viewportWidth: Wide, configure: g =>
        {
            g.Style.ShowRowDragHandle = showHandle;
            g.Style.RowDragHandlePosition = position;
            g.Style.RowDragHandleWidth = HandleWidth;
            configure?.Invoke(g);
        });

    private static float HeaderX(InputGrid g, KumikoColumn column) =>
        KumikoInputController.HeaderScreenX(column, g.Scroll, g.Source, g.Style);

    private static float OpenPopupX(InputGrid g, KumikoColumn column)
    {
        float x = HeaderX(g, column);
        // Click the column's filter icon (12 px wide, 20 px inside its right edge) and read where the popup landed
        float iconX = x + column.Width - 20 - 6;
        g.Tap(iconX, InputGrid.HeaderY);
        Assert.True(g.Input.PopupManager.HasActivePopups);
        var popup = g.Input.PopupManager.HitTest(x + 2, g.Style.HeaderHeight + 2);
        Assert.NotNull(popup);
        return popup!.Bounds.X;
    }

    [Fact]
    public void NoDragHandle_PopupOpensAtTheColumnHeader()
    {
        var g = CreateGrid(false, DragHandlePosition.Left);

        Assert.Equal(120f, OpenPopupX(g, g.Age));
    }

    [Fact]
    public void DragHandleAtTheStart_ShiftsThePopupByTheHandleWidth()
    {
        var g = CreateGrid(true, DragHandlePosition.Left);

        Assert.Equal(120f + HandleWidth, HeaderX(g, g.Age));
        Assert.Equal(120f + HandleWidth, OpenPopupX(g, g.Age));
    }

    [Fact]
    public void DragHandleAtTheEnd_LeavesThePopupAtTheColumnHeader()
    {
        var g = CreateGrid(true, DragHandlePosition.Right);

        Assert.Equal(120f, HeaderX(g, g.Age));
        Assert.Equal(120f, OpenPopupX(g, g.Age));
    }

    [Fact]
    public void DragHandleAtTheStart_ShiftsAFrozenLeftColumn_AndTheScrollableOnesAfterIt()
    {
        var g = CreateGrid(true, DragHandlePosition.Left, x => x.Name.IsFrozen = true);

        Assert.Equal(HandleWidth, HeaderX(g, g.Name));
        Assert.Equal(HandleWidth + 120f, HeaderX(g, g.Age));
    }

    [Fact]
    public void RightFrozenColumn_SitsBeforeAnEndHandle_AndIgnoresAStartHandle()
    {
        var atEnd = CreateGrid(true, DragHandlePosition.Right, x => x.Score.IsFrozenRight = true);
        var atStart = CreateGrid(true, DragHandlePosition.Left, x => x.Score.IsFrozenRight = true);

        Assert.Equal(Wide - HandleWidth - 100f, HeaderX(atEnd, atEnd.Score));
        Assert.Equal(Wide - 100f, HeaderX(atStart, atStart.Score));
    }
}

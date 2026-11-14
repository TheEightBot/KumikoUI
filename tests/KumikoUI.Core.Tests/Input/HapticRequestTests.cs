using KumikoUI.Core.Input;

namespace KumikoUI.Core.Tests.Input;

/// <summary>
/// <see cref="KumikoInputController.HapticFeedbackRequested"/> (#101): once per drag start, drop and long press of a
/// finger or pen, never for a mouse.
/// </summary>
public class HapticRequestTests
{
    private static List<KumikoHapticKind> Track(InputGrid g)
    {
        var kinds = new List<KumikoHapticKind>();
        g.Input.HapticFeedbackRequested += (_, e) => kinds.Add(e.Kind);
        return kinds;
    }

    private static void Touch(InputGrid g, InputAction action, float x, float y, long t = 0) =>
        g.Pointer(action, x, y, timestampMs: t, deviceType: KumikoPointerDeviceType.Touch);

    [Fact]
    public void ATouchRowDrag_RequestsDragStartThenDrop()
    {
        var g = new InputGrid(configure: x => x.Style.AllowRowDragDrop = true);
        var kinds = Track(g);

        Touch(g, InputAction.Pressed, 60, g.RowY(1));
        Touch(g, InputAction.LongPress, 60, g.RowY(1));
        Touch(g, InputAction.Moved, 60, g.RowY(3));
        Touch(g, InputAction.Released, 60, g.RowY(3));

        Assert.Equal(new[] { KumikoHapticKind.DragStart, KumikoHapticKind.Drop }, kinds);
    }

    [Fact]
    public void ATouchColumnDrag_RequestsDragStartThenDrop()
    {
        var g = new InputGrid();
        var kinds = Track(g);

        Touch(g, InputAction.Pressed, 60, InputGrid.HeaderY);
        Touch(g, InputAction.Moved, 100, InputGrid.HeaderY);
        Touch(g, InputAction.Moved, 160, InputGrid.HeaderY);
        Touch(g, InputAction.Released, 160, InputGrid.HeaderY);

        Assert.Equal(new[] { KumikoHapticKind.DragStart, KumikoHapticKind.Drop }, kinds);
    }

    [Fact]
    public void ATouchLongPressThatAsksForAMenu_RequestsLongPress()
    {
        var g = new InputGrid();
        g.Input.ContextRequested += (_, _) => { };
        var kinds = Track(g);

        Touch(g, InputAction.Pressed, 60, g.RowY(1));
        Touch(g, InputAction.LongPress, 60, g.RowY(1));

        Assert.Equal(new[] { KumikoHapticKind.LongPress }, kinds);
    }

    [Fact]
    public void AMouse_NeverRequestsHaptics()
    {
        var g = new InputGrid(configure: x => x.Style.AllowRowDragDrop = true);
        var kinds = Track(g);

        g.Press(60, InputGrid.HeaderY);
        g.Move(160, InputGrid.HeaderY);
        g.Release(160, InputGrid.HeaderY);
        g.Press(60, g.RowY(1));
        g.Move(60, g.RowY(3));
        g.Release(60, g.RowY(3));
        g.Pointer(InputAction.Pressed, 60, g.RowY(1));
        g.Pointer(InputAction.LongPress, 60, g.RowY(1));

        Assert.Empty(kinds);
    }

    [Fact]
    public void ATouchTapOrPan_RequestsNothing()
    {
        var g = new InputGrid();
        var kinds = Track(g);

        Touch(g, InputAction.Pressed, 60, g.RowY(1), 1000);
        Touch(g, InputAction.Released, 60, g.RowY(1), 1050);
        Touch(g, InputAction.Pressed, 60, g.RowY(4), 2000);
        Touch(g, InputAction.Moved, 60, g.RowY(1), 2016);
        Touch(g, InputAction.Released, 60, g.RowY(1), 2032);

        Assert.Empty(kinds);
    }
}

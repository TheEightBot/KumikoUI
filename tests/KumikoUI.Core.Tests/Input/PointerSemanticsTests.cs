using KumikoUI.Core.Input;

namespace KumikoUI.Core.Tests.Input;

/// <summary>
/// Pointer and wheel semantics every host relies on (#60): hover never pans, wheel notches and trackpad pixels,
/// Shift+wheel scrolls horizontally, the wheel is handled only when the grid moved (so the page scrolls on at
/// the end of the content), a second finger is ignored, a disabled grid does nothing, and a position the host
/// could not compute is dropped.
/// </summary>
public class PointerSemanticsTests
{
    // ── Hover ──

    [Fact]
    public void AMoveAfterTheRelease_IsAHover_AndDoesNotPan()
    {
        var g = new InputGrid();
        g.Tap(100, g.RowY(1));

        // A host that reports hover as a plain move (no press in progress), well past the pan threshold
        var e = g.Pointer(InputAction.Moved, 100, g.RowY(5));
        g.Pointer(InputAction.Moved, 20, g.RowY(0));

        Assert.False(e.Handled);
        Assert.Equal(0, g.Scroll.OffsetX);
        Assert.Equal(0, g.Scroll.OffsetY);
        Assert.Equal(1, g.Selection.CurrentCell.Row);
        Assert.Equal(-1, g.Input.DragColumnIndex);
    }

    [Fact]
    public void Hover_OverAHeader_NeverStartsAColumnDrag()
    {
        var g = new InputGrid();
        g.Tap(60, InputGrid.HeaderY);

        g.Hover(160, InputGrid.HeaderY);
        g.Hover(260, InputGrid.HeaderY);

        Assert.Equal(-1, g.Input.DragColumnIndex);
        Assert.Equal(0, g.Scroll.OffsetX);
    }

    // ── Wheel ──

    [Theory]
    [InlineData(-1f, 108f)]  // one notch: 36 px rows × 3
    [InlineData(-0.5f, 54f)] // half a notch (a high-resolution wheel)
    [InlineData(-2f, 216f)]
    public void Wheel_Notches_ScrollRowsTimesTheMultiplier(float notches, float expected)
    {
        var g = new InputGrid();

        g.Pointer(InputAction.Scroll, 100, 100, scrollDy: notches);

        Assert.Equal(expected, g.Scroll.OffsetY);
    }

    [Theory]
    [InlineData(-1f, 1f)]
    [InlineData(-25f, 25f)]
    [InlineData(-37.5f, 37.5f)]
    public void Wheel_Pixels_ScrollExactlyThatFar(float pixels, float expected)
    {
        var g = new InputGrid();

        g.Pointer(InputAction.Scroll, 100, 100, scrollDy: pixels, scrollUnit: KumikoScrollUnit.Pixels);

        Assert.Equal(expected, g.Scroll.OffsetY);
    }

    [Fact]
    public void Wheel_Pixels_IgnoreTheMultiplier()
    {
        var g = new InputGrid();
        g.Input.ScrollSettings = new ScrollSettings { WheelScrollMultiplier = 10 };

        g.Pointer(InputAction.Scroll, 100, 100, scrollDx: -12, scrollUnit: KumikoScrollUnit.Pixels);

        Assert.Equal(12, g.Scroll.OffsetX);
    }

    [Fact]
    public void ShiftWheel_ScrollsHorizontally()
    {
        var g = new InputGrid();

        g.Pointer(InputAction.Scroll, 100, 100, InputModifiers.Shift, scrollDy: -1);

        Assert.Equal(108, g.Scroll.OffsetX);
        Assert.Equal(0, g.Scroll.OffsetY);
    }

    [Fact]
    public void ShiftWheel_WithAHorizontalDelta_UsesItAsIs()
    {
        var g = new InputGrid();

        // The platform already turned Shift+wheel into a horizontal delta (macOS does)
        g.Pointer(InputAction.Scroll, 100, 100, InputModifiers.Shift, scrollDx: -20, scrollUnit: KumikoScrollUnit.Pixels);

        Assert.Equal(20, g.Scroll.OffsetX);
        Assert.Equal(0, g.Scroll.OffsetY);
    }

    [Fact]
    public void Wheel_ThatMovesTheGrid_IsHandled_AndRedraws()
    {
        var g = new InputGrid();
        int redraws = g.Redraws;

        var e = g.Pointer(InputAction.Scroll, 100, 100, scrollDy: -1);

        Assert.True(e.Handled);
        Assert.True(g.Redraws > redraws);
    }

    [Fact]
    public void Wheel_PastTheTop_IsNotHandled_SoThePageScrolls()
    {
        var g = new InputGrid();
        int redraws = g.Redraws;

        var e = g.Pointer(InputAction.Scroll, 100, 100, scrollDy: 1);

        Assert.False(e.Handled);
        Assert.Equal(redraws, g.Redraws);
    }

    [Fact]
    public void Wheel_PastTheBottom_IsNotHandled_AfterTheLastStepThatMoved()
    {
        var g = new InputGrid();

        var reachEnd = g.Pointer(InputAction.Scroll, 100, 100, scrollDy: -500);
        var past = g.Pointer(InputAction.Scroll, 100, 100, scrollDy: -1);

        Assert.True(reachEnd.Handled);
        Assert.Equal(g.Scroll.MaxOffsetY, g.Scroll.OffsetY);
        Assert.False(past.Handled);
    }

    [Fact]
    public void Wheel_PastTheLastColumn_IsNotHandled()
    {
        var g = new InputGrid();
        g.Pointer(InputAction.Scroll, 100, 100, scrollDx: -100);

        var e = g.Pointer(InputAction.Scroll, 100, 100, scrollDx: -1);

        Assert.Equal(g.Scroll.MaxOffsetX, g.Scroll.OffsetX);
        Assert.False(e.Handled);
    }

    [Fact]
    public void Wheel_OnAGridThatFitsItsViewport_IsNotHandled()
    {
        var g = new InputGrid(rows: 2, viewportWidth: 600);

        var e = g.Pointer(InputAction.Scroll, 100, 100, scrollDy: -1);

        Assert.False(e.Handled);
        Assert.Equal(0, g.Scroll.OffsetY);
    }

    // ── Pointer identity ──

    [Fact]
    public void ASecondFinger_DoesNotResetThePan()
    {
        var g = new InputGrid();
        g.Pointer(InputAction.Pressed, 100, 200, timestampMs: 100, pointerId: 1);
        g.Pointer(InputAction.Moved, 100, 180, timestampMs: 116, pointerId: 1);
        g.Pointer(InputAction.Moved, 100, 160, timestampMs: 132, pointerId: 1);
        float panned = g.Scroll.OffsetY;

        var second = g.Pointer(InputAction.Pressed, 250, 100, timestampMs: 140, pointerId: 2);
        g.Pointer(InputAction.Moved, 250, 60, timestampMs: 148, pointerId: 2);
        g.Pointer(InputAction.Moved, 100, 140, timestampMs: 150, pointerId: 1);

        Assert.True(second.Handled);
        Assert.Equal(20, panned); // the first move past the threshold starts the pan
        Assert.Equal(40, g.Scroll.OffsetY); // followed only the first finger
    }

    [Fact]
    public void ASecondFinger_TappingWhileTheFirstIsDown_SelectsNothing()
    {
        var g = new InputGrid();
        g.Pointer(InputAction.Pressed, 100, g.RowY(1), pointerId: 1);

        g.Pointer(InputAction.Pressed, 100, g.RowY(4), pointerId: 2);
        g.Pointer(InputAction.Released, 100, g.RowY(4), pointerId: 2);
        g.Pointer(InputAction.Released, 100, g.RowY(1), pointerId: 1);

        Assert.Equal(1, g.Selection.CurrentCell.Row);
    }

    [Fact]
    public void ASecondFinger_LiftedAfterTheFirst_IsNotATap()
    {
        var g = new InputGrid();
        g.Pointer(InputAction.Pressed, 100, g.RowY(1), pointerId: 1);
        g.Pointer(InputAction.Pressed, 100, g.RowY(4), pointerId: 2);
        g.Pointer(InputAction.Released, 100, g.RowY(1), pointerId: 1);

        var stray = g.Pointer(InputAction.Released, 100, g.RowY(4), pointerId: 2);

        Assert.True(stray.Handled);
        Assert.Equal(1, g.Selection.CurrentCell.Row);
    }

    [Fact]
    public void AFingerAfterTheFirstWasLifted_IsFollowed()
    {
        var g = new InputGrid();
        g.Tap(100, g.RowY(1)); // pointer 0

        g.Pointer(InputAction.Pressed, 100, g.RowY(3), pointerId: 7);
        g.Pointer(InputAction.Released, 100, g.RowY(3), pointerId: 7);

        Assert.Equal(3, g.Selection.CurrentCell.Row);
    }

    [Fact]
    public void APress_AfterTheFollowedPointerWentSilent_TakesOver()
    {
        var g = new InputGrid();
        g.Pointer(InputAction.Pressed, 100, g.RowY(1), timestampMs: 100, pointerId: 1);
        // pointer 1's release was lost

        g.Pointer(InputAction.Pressed, 100, g.RowY(3), timestampMs: 5_000, pointerId: 2);
        g.Pointer(InputAction.Released, 100, g.RowY(3), timestampMs: 5_050, pointerId: 2);

        Assert.Equal(3, g.Selection.CurrentCell.Row);
    }

    [Fact]
    public void ACancel_AfterTheRelease_IsIgnored()
    {
        var g = new InputGrid();
        g.Tap(100, g.RowY(1));

        // WinUI raises PointerCaptureLost after every release
        var cancel = g.Pointer(InputAction.Cancelled, 100, g.RowY(1));

        Assert.False(cancel.Handled);
        Assert.Equal(1, g.Selection.CurrentCell.Row);
    }

    // ── IsEnabled ──

    [Fact]
    public void Disabled_TapsSelectNothing_AndAreNotHandled()
    {
        var g = new InputGrid();
        g.Input.IsEnabled = false;

        g.Press(100, g.RowY(2));
        var release = g.Pointer(InputAction.Released, 100, g.RowY(2));

        Assert.False(release.Handled);
        Assert.False(g.Selection.CurrentCell.IsValid);
        Assert.Empty(g.Selection.SelectedRows);
    }

    [Fact]
    public void Disabled_TheWheelAndPansDoNotScroll()
    {
        var g = new InputGrid();
        g.Input.IsEnabled = false;

        var wheel = g.Pointer(InputAction.Scroll, 100, 100, scrollDy: -1);
        g.Press(100, 200);
        g.Move(100, 100);

        Assert.False(wheel.Handled);
        Assert.Equal(0, g.Scroll.OffsetY);
    }

    [Fact]
    public void Disabled_KeysDoNothing_AndDoNotEdit()
    {
        var g = new InputGrid();
        g.Select(1, 0);
        g.Input.IsEnabled = false;

        var down = g.Key(KumikoKey.Down);
        var f2 = g.Key(KumikoKey.F2);
        g.Type('x');

        Assert.False(down.Handled);
        Assert.False(f2.Handled);
        Assert.Equal(1, g.Selection.CurrentCell.Row);
        Assert.False(g.Edit.IsEditing);
    }

    [Fact]
    public void Disabled_DoubleTapDoesNotEdit_AndHoverShowsNoResizeCursor()
    {
        var g = new InputGrid();
        g.Input.IsEnabled = false;

        g.Tap(100, g.RowY(1), clicks: 1);
        g.Tap(100, g.RowY(1), clicks: 2);
        g.Hover(196, InputGrid.HeaderY); // Age's resize grip

        Assert.False(g.Edit.IsEditing);
        Assert.Equal(KumikoCursor.Default, g.Input.Cursor);
    }

    [Fact]
    public void Disabling_CancelsTheEdit_AndStopsThePan()
    {
        var g = new InputGrid();
        g.Select(1, 0);
        g.Key(KumikoKey.F2);
        Assert.True(g.Edit.IsEditing);
        g.Press(100, 200);
        g.Move(100, 170);

        g.Input.IsEnabled = false;
        g.Input.IsEnabled = true;
        float offset = g.Scroll.OffsetY;
        g.Move(100, 100); // the press ended with the disable: this is a hover now

        Assert.False(g.Edit.IsEditing);
        Assert.Equal(offset, g.Scroll.OffsetY);
        Assert.False(g.Input.IsInertialScrolling);
    }

    [Fact]
    public void Reenabled_RespondsAgain()
    {
        var g = new InputGrid();
        g.Input.IsEnabled = false;
        g.Input.IsEnabled = true;

        g.Tap(100, g.RowY(2));

        Assert.Equal(2, g.Selection.CurrentCell.Row);
    }

    // ── Positions the host could not compute ──

    [Theory]
    [InlineData(float.NaN, 100f)]
    [InlineData(100f, float.NaN)]
    [InlineData(float.PositiveInfinity, 100f)]
    [InlineData(100f, float.NegativeInfinity)]
    public void ANonFinitePosition_IsDropped(float x, float y)
    {
        var g = new InputGrid();

        var press = g.Pointer(InputAction.Pressed, x, y);
        var release = g.Pointer(InputAction.Released, x, y);
        var wheel = g.Pointer(InputAction.Scroll, x, y, scrollDy: -1);

        Assert.False(press.Handled);
        Assert.False(release.Handled);
        Assert.False(wheel.Handled);
        Assert.False(g.Selection.CurrentCell.IsValid);
        Assert.Equal(0, g.Scroll.OffsetY);
    }
}

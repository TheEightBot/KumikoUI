using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Tests.Input;

/// <summary>
/// The grid's scroll bars under a mouse, a pen and a finger (#316): a press on the thumb drags it, a press on the track
/// pages toward the pointer and repeats while held, hovering expands the bar, a press on a bar never pans, drags or
/// selects, and a finger pans through the bars.
/// </summary>
/// <remarks>
/// <see cref="InputGrid"/>: 300 x 256, rows below a 40 px header. 30 rows of 36: 1120 px of content, 864 to scroll.
/// 500 px of columns, 200 to scroll. The vertical bar's band is x 290..300, y 40..246 (it stops above the horizontal
/// band, y 246..256, x 0..290). Its thumb is 206 * 216 / 1080 = 41.2 px and travels 164.8 px; the horizontal thumb is
/// 290 * 300 / 500 = 174 px and travels 116 px.
/// </remarks>
public class ScrollBarInputTests
{
    private const float VBarX = 295f, HBarY = 251f;
    private const float VThumb = 41.2f, VTravel = 164.8f;

    private static InputGrid Grid(Action<InputGrid>? configure = null) => new(configure: configure);

    private static KumikoPointerEventArgs Mouse(InputGrid g, InputAction action, float x, float y, long t = 0) =>
        g.Pointer(action, x, y, timestampMs: t);

    private static KumikoPointerEventArgs Finger(InputGrid g, InputAction action, float x, float y, long t = 0) =>
        g.Pointer(action, x, y, timestampMs: t, deviceType: KumikoPointerDeviceType.Touch);

    // ── Hit testing ──

    [Fact]
    public void HitTest_AMouseOnTheEdgeBands_LandsOnTheBars()
    {
        var g = Grid();

        Assert.Equal(HitRegion.VerticalScrollBarThumb, g.Hit(VBarX, 60).Region);
        Assert.Equal(HitRegion.VerticalScrollBarTrack, g.Hit(VBarX, 200).Region);
        Assert.Equal(HitRegion.HorizontalScrollBarThumb, g.Hit(100, HBarY).Region);
        Assert.Equal(HitRegion.HorizontalScrollBarTrack, g.Hit(250, HBarY).Region);
        Assert.Equal(HitRegion.Cell, g.Hit(285, 60).Region);
        Assert.True(g.Hit(VBarX, 20).Region is HitRegion.HeaderCell or HitRegion.HeaderResizeGrip); // the header stays reachable
    }

    [Fact]
    public void HitTest_AFinger_PassesThroughTheBars()
    {
        var g = Grid();

        Assert.Equal(HitRegion.Cell, g.Hit(VBarX, 60, KumikoPointerDeviceType.Touch).Region);
        Assert.Equal(HitRegion.Cell, g.Hit(100, HBarY, KumikoPointerDeviceType.Touch).Region);
        Assert.Equal(HitRegion.VerticalScrollBarThumb, g.Hit(VBarX, 60, KumikoPointerDeviceType.Pen).Region);
    }

    [Fact]
    public void HitTest_Never_LeavesTheEdgesToTheCells()
    {
        var g = Grid(x =>
        {
            x.Style.VerticalScrollBarVisibility = KumikoScrollBarVisibility.Never;
            x.Style.HorizontalScrollBarVisibility = KumikoScrollBarVisibility.Never;
        });

        Assert.Equal(HitRegion.Cell, g.Hit(VBarX, 60).Region);
        Assert.Equal(HitRegion.Cell, g.Hit(100, HBarY).Region);
    }

    [Fact]
    public void HitTest_ContentThatFits_HasNoBar()
    {
        var g = new InputGrid(rows: 3, viewportWidth: 600);

        Assert.Equal(HitRegion.EmptyArea, g.Hit(595, 60).Region);
    }

    // ── Thumb drag ──

    [Fact]
    public void ThumbDrag_ScrollsInProportion_WhereverThePointerGoesSideways()
    {
        var g = Grid();

        Mouse(g, InputAction.Pressed, VBarX, 50);
        Mouse(g, InputAction.Moved, VBarX, 50 + VTravel / 2);
        Assert.Equal(432, g.Scroll.OffsetY, 1); // half the travel: half of 864

        Mouse(g, InputAction.Moved, 100, 50 + VTravel / 4); // off the bar: still dragging
        Assert.Equal(216, g.Scroll.OffsetY, 1);
        Assert.Equal(0, g.Scroll.OffsetX);

        Mouse(g, InputAction.Moved, 100, 1000); // clamped at the end
        Assert.Equal(864, g.Scroll.OffsetY, 1);
    }

    [Fact]
    public void ThumbDrag_KeepsWhereTheThumbWasGrabbed()
    {
        var g = Grid();
        g.Scroll.OffsetY = 432; // thumb at 40 + 82.4 = 122.4

        Mouse(g, InputAction.Pressed, VBarX, 140); // 17.6 px into the thumb
        Assert.Equal(432, g.Scroll.OffsetY, 1); // a press on the thumb does not jump

        Mouse(g, InputAction.Moved, VBarX, 140 - VTravel / 4);
        Assert.Equal(216, g.Scroll.OffsetY, 1);
    }

    [Fact]
    public void ThumbDrag_Horizontal()
    {
        var g = Grid();

        Mouse(g, InputAction.Pressed, 50, HBarY);
        Mouse(g, InputAction.Moved, 50 + 58, HBarY); // half of 116
        Mouse(g, InputAction.Released, 50 + 58, HBarY);

        Assert.Equal(100, g.Scroll.OffsetX, 1);
        Assert.Equal(0, g.Scroll.OffsetY);
    }

    [Fact]
    public void ThumbDrag_NeitherSelectsNorTapsNorStartsADrag()
    {
        var g = Grid(x => x.Style.AllowRowDragDrop = true);
        int taps = 0;
        g.Input.RowTapped += (_, _) => taps++;
        g.Input.CellTapped += (_, _) => taps++;
        var before = g.Selection.CurrentCell;

        Mouse(g, InputAction.Pressed, VBarX, 50);
        Mouse(g, InputAction.Moved, VBarX, 120);
        Mouse(g, InputAction.Released, VBarX, 120);

        Assert.Equal(before, g.Selection.CurrentCell);
        Assert.Equal(0, taps);
        Assert.Equal(-1, g.Input.DragRowIndex);
        Assert.False(g.Input.IsInertialScrolling); // a drag never flings
    }

    [Fact]
    public void ThumbPress_StopsAFling()
    {
        var g = Grid();
        Finger(g, InputAction.Pressed, 100, 220, 100);
        for (int i = 1; i <= 4; i++) Finger(g, InputAction.Moved, 100, 220 - 30 * i, 100 + 16 * i);
        Finger(g, InputAction.Released, 100, 100, 200);
        Assert.True(g.Input.IsInertialScrolling);

        float thumbMiddle = 40 + g.Scroll.OffsetY / 864 * VTravel + VThumb / 2;
        Mouse(g, InputAction.Pressed, VBarX, thumbMiddle, 300);

        Assert.False(g.Input.IsInertialScrolling);
    }

    // ── Track paging ──

    [Fact]
    public void TrackPress_PagesAtOnce_ThenRepeatsWhileHeld_UntilTheThumbReachesThePointer()
    {
        var g = Grid();

        var press = Mouse(g, InputAction.Pressed, VBarX, 200);

        Assert.True(press.Handled);
        Assert.Equal(216, g.Scroll.OffsetY); // one page: the rows band
        Assert.True(g.Input.IsAutoScrolling); // the host's frame loop repeats it

        Assert.True(g.Input.UpdateAutoScroll(g.Scroll, KumikoInputController.ScrollBarPageDelayMs - 1));
        Assert.Equal(216, g.Scroll.OffsetY); // the pause before repeating

        g.Input.UpdateAutoScroll(g.Scroll, 1);
        Assert.Equal(432, g.Scroll.OffsetY);
        g.Input.UpdateAutoScroll(g.Scroll, KumikoInputController.ScrollBarPageRepeatMs);
        Assert.Equal(648, g.Scroll.OffsetY); // thumb 163.6..204.8: it covers the pointer at 200

        Assert.False(g.Input.UpdateAutoScroll(g.Scroll, KumikoInputController.ScrollBarPageRepeatMs));
        Assert.Equal(648, g.Scroll.OffsetY);
        Assert.False(g.Input.IsAutoScrolling);

        Mouse(g, InputAction.Released, VBarX, 200);
        Assert.Equal(648, g.Scroll.OffsetY);
    }

    [Fact]
    public void TrackPress_AboveTheThumb_PagesBack()
    {
        var g = Grid();
        g.Scroll.OffsetY = 864; // thumb at the bottom

        Mouse(g, InputAction.Pressed, VBarX, 60);

        Assert.Equal(864 - 216, g.Scroll.OffsetY);
    }

    [Fact]
    public void TrackPress_Released_StopsPaging()
    {
        var g = Grid();
        Mouse(g, InputAction.Pressed, VBarX, 230);
        Mouse(g, InputAction.Released, VBarX, 230);

        Assert.False(g.Input.IsAutoScrolling);
        Assert.False(g.Input.UpdateAutoScroll(g.Scroll, 1000));
        Assert.Equal(216, g.Scroll.OffsetY);
    }

    [Fact]
    public void TrackPress_PointerLeavingTheBar_PausesPaging_ReturningResumesIt()
    {
        var g = Grid();
        Mouse(g, InputAction.Pressed, VBarX, 240);

        Mouse(g, InputAction.Moved, 150, 240);
        Assert.False(g.Input.IsAutoScrolling);
        g.Input.UpdateAutoScroll(g.Scroll, 1000);
        Assert.Equal(216, g.Scroll.OffsetY);

        Mouse(g, InputAction.Moved, VBarX, 240);
        Assert.True(g.Input.IsAutoScrolling);
        g.Input.UpdateAutoScroll(g.Scroll, KumikoInputController.ScrollBarPageDelayMs);
        Assert.Equal(432, g.Scroll.OffsetY);
    }

    [Fact]
    public void TrackPress_Horizontal_PagesByTheScrollingColumnsWidth()
    {
        var g = Grid(x => x.Name.IsFrozen = true); // the scrolling columns: 120..290 of the bar, 180 px of pane

        Mouse(g, InputAction.Pressed, 285, HBarY);

        Assert.Equal(180, g.Scroll.OffsetX); // one page: the scrolling pane (120..300)
        Assert.Equal(0, g.Scroll.OffsetY);
    }

    [Fact]
    public void TrackPress_NeverPansOrSelects()
    {
        var g = Grid();
        var before = g.Selection.CurrentCell;

        Mouse(g, InputAction.Pressed, VBarX, 200);
        Mouse(g, InputAction.Moved, VBarX, 120); // would be a pan of 80 px anywhere else
        Mouse(g, InputAction.Released, VBarX, 120);

        Assert.Equal(216, g.Scroll.OffsetY);
        Assert.Equal(0, g.Scroll.OffsetX);
        Assert.Equal(before, g.Selection.CurrentCell);
        Assert.False(g.Input.IsInertialScrolling);
    }

    // ── Hover ──

    [Fact]
    public void Hover_ExpandsTheBarUnderThePointer_WithTheDefaultCursor()
    {
        var g = Grid();
        g.Hover(VBarX, 150);

        Assert.True(g.Input.IsScrollBarExpanded(vertical: true));
        Assert.False(g.Input.IsScrollBarExpanded(vertical: false));
        Assert.Equal(KumikoCursor.Default, g.Input.Cursor);
        Assert.Equal(1f, g.Input.ScrollBarOpacity(vertical: true, g.Style));

        g.Hover(100, HBarY);
        Assert.False(g.Input.IsScrollBarExpanded(vertical: true));
        Assert.True(g.Input.IsScrollBarExpanded(vertical: false));

        g.Pointer(InputAction.Exited, 100, HBarY);
        Assert.False(g.Input.IsScrollBarExpanded(vertical: false));
        Assert.Equal(1f, g.Input.ScrollBarOpacity(vertical: false, g.Style)); // an Auto bar holds before it hides
        Assert.True(g.Input.IsAnimating);
    }

    [Fact]
    public void Hover_ByAFinger_ExpandsNothing()
    {
        var g = Grid();

        g.Pointer(InputAction.Hover, VBarX, 150, button: PointerButton.None, deviceType: KumikoPointerDeviceType.Touch);

        Assert.False(g.Input.IsScrollBarExpanded(vertical: true));
    }

    [Fact]
    public void Press_KeepsTheBarExpanded_UntilReleasedAwayFromIt()
    {
        var g = Grid();
        Mouse(g, InputAction.Pressed, VBarX, 50);
        Mouse(g, InputAction.Moved, 100, 80);
        Assert.True(g.Input.IsScrollBarExpanded(vertical: true));

        Mouse(g, InputAction.Released, 100, 80);

        Assert.False(g.Input.IsScrollBarExpanded(vertical: true));
        Assert.True(g.Input.IsAnimating); // it holds, then fades
    }

    // ── Touch ──

    [Fact]
    public void Finger_PansThroughTheOverlay()
    {
        var g = Grid();

        Finger(g, InputAction.Pressed, VBarX, 200, 100);
        Finger(g, InputAction.Moved, VBarX, 180, 116); // starts the pan
        Finger(g, InputAction.Moved, VBarX, 80, 132);
        Finger(g, InputAction.Released, VBarX, 80, 400);

        Assert.Equal(100, g.Scroll.OffsetY, 1);
        Assert.False(g.Input.IsScrollBarExpanded(vertical: true));
    }

    [Fact]
    public void Finger_TapOnTheEdge_SelectsTheCellUnderTheBar()
    {
        var g = Grid();

        Finger(g, InputAction.Pressed, VBarX, g.RowY(1));
        Finger(g, InputAction.Released, VBarX, g.RowY(1));

        Assert.Equal(1, g.Selection.CurrentCell.Row);
    }

    // ── Lifecycle ──

    [Fact]
    public void Disabling_EndsAPressOnABar()
    {
        var g = Grid();
        Mouse(g, InputAction.Pressed, VBarX, 200);
        Assert.True(g.Input.IsAutoScrolling);

        g.Input.IsEnabled = false;

        Assert.False(g.Input.IsAutoScrolling);
        Assert.False(g.Input.IsScrollBarExpanded(vertical: true));
    }

    [Fact]
    public void ANewPress_AfterALostRelease_IsNotTakenForTheBar()
    {
        var g = Grid();
        Mouse(g, InputAction.Pressed, VBarX, 50);
        // the release never arrives; the next press lands on a cell
        Mouse(g, InputAction.Pressed, 100, g.RowY(2));
        Mouse(g, InputAction.Released, 100, g.RowY(2));

        Assert.Equal(2, g.Selection.CurrentCell.Row);
    }
}

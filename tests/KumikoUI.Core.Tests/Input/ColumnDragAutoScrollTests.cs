using KumikoUI.Core.Input;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests.Input;

/// <summary>
/// Edge autoscroll for a column header drag: the scrolling columns scroll sideways while the pointer is held in a band
/// at either edge of their pane, so a column can be dragged to a position that is off screen.
/// </summary>
/// <remarks>
/// <see cref="InputGrid"/> geometry: columns Name 120, Age 80, City 100, Dept 100, Score 100 (500 px) in a 300 px
/// viewport, so the content scrolls 200 px. Edge autoscroll bands are 40 px: 0-40 and 260-300 of the scrolling pane.
/// </remarks>
public class ColumnDragAutoScrollTests
{
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

    private static int RunAutoScroll(InputGrid g, int maxFrames = 1000)
    {
        int frames = 0;
        while (frames < maxFrames && g.Input.UpdateAutoScroll(g.Scroll, 16f)) frames++;
        return frames;
    }

    /// <summary>Grabs the header at <paramref name="fromX"/> and holds the pointer at <paramref name="toX"/>.</summary>
    private static void Hold(InputGrid g, float fromX, float toX)
    {
        g.Press(fromX, InputGrid.HeaderY);
        g.Move(fromX + (toX > fromX ? 12 : -12), InputGrid.HeaderY);
        g.Move(toX, InputGrid.HeaderY);
    }

    [Fact]
    public void HoldingAtTheRightEdge_Autoscrolls_UntilTheContentEnd()
    {
        var g = new InputGrid();
        Hold(g, 60, 295);

        Assert.True(g.Input.IsAutoScrolling);
        Assert.Equal(0, g.Scroll.OffsetX);
        int frames = RunAutoScroll(g);

        Assert.True(frames > 1);
        Assert.Equal(g.Scroll.MaxOffsetX, g.Scroll.OffsetX);
        Assert.False(g.Input.IsAutoScrolling); // at the content edge it waits for the pointer to move
        Assert.Equal(0, g.Scroll.OffsetY);
    }

    [Fact]
    public void AColumnCanBeDroppedAtAnOffScreenPosition()
    {
        var g = new InputGrid();
        var reordered = ApplyReorders(g);

        Hold(g, 60, 295);
        RunAutoScroll(g);
        g.Release(295, InputGrid.HeaderY); // Score now spans 200..300: past its middle, after the last column

        var e = Assert.Single(reordered);
        Assert.Equal(0, e.OldIndex);
        Assert.Equal(4, e.NewIndex);
        Assert.Same(g.Name, g.Source.Columns[4]);
        Assert.False(g.Input.IsAutoScrolling);
    }

    [Fact]
    public void HoldingAtTheLeftEdge_AutoscrollsBack()
    {
        var g = new InputGrid();
        g.Scroll.OffsetX = 200;
        Hold(g, 250, 5);

        Assert.True(g.Input.IsAutoScrolling);
        RunAutoScroll(g);
        Assert.Equal(0, g.Scroll.OffsetX);
    }

    [Fact]
    public void TheSpeedGrowsWithTheDepthIntoTheBand()
    {
        var shallow = new InputGrid();
        Hold(shallow, 60, 270);
        shallow.Input.UpdateAutoScroll(shallow.Scroll, 100f);

        var deep = new InputGrid();
        Hold(deep, 60, 299);
        deep.Input.UpdateAutoScroll(deep.Scroll, 100f);

        Assert.True(shallow.Scroll.OffsetX > 0);
        Assert.True(deep.Scroll.OffsetX > shallow.Scroll.OffsetX);
    }

    [Fact]
    public void OutsideTheBands_DoesNotAutoscroll()
    {
        var g = new InputGrid();
        Hold(g, 60, 150);

        Assert.False(g.Input.IsAutoScrolling);
        Assert.False(g.Input.UpdateAutoScroll(g.Scroll, 16f));
        Assert.Equal(0, g.Scroll.OffsetX);
    }

    [Fact]
    public void AtTheContentStart_TheLeftBandDoesNotAutoscroll()
    {
        var g = new InputGrid();
        Hold(g, 60, 5);

        Assert.False(g.Input.IsAutoScrolling);
    }

    [Fact]
    public void MovingOutOfTheBand_StopsIt()
    {
        var g = new InputGrid();
        Hold(g, 60, 295);
        Assert.True(g.Input.IsAutoScrolling);

        g.Move(150, InputGrid.HeaderY);

        Assert.False(g.Input.IsAutoScrolling);
        Assert.False(g.Input.UpdateAutoScroll(g.Scroll, 16f));
    }

    [Fact]
    public void ReleasingOrCanceling_StopsIt()
    {
        var g = new InputGrid();
        Hold(g, 60, 295);
        g.Release(295, InputGrid.HeaderY);
        Assert.False(g.Input.IsAutoScrolling);

        var c = new InputGrid();
        Hold(c, 60, 295);
        c.Pointer(InputAction.Cancelled, 295, InputGrid.HeaderY);
        Assert.False(c.Input.IsAutoScrolling);
        Assert.False(c.Input.UpdateAutoScroll(c.Scroll, 16f));
    }

    [Fact]
    public void AFrozenColumn_DoesNotAutoscroll()
    {
        var g = new InputGrid(configure: x => x.Name.IsFrozen = true);
        Hold(g, 60, 295);

        Assert.False(g.Input.IsAutoScrolling);
    }

    [Fact]
    public void TheScrollingBands_SitInsideTheFrozenPane()
    {
        var g = new InputGrid(configure: x => x.Name.IsFrozen = true);
        // Name is frozen (0..120), so the scrolling pane is 120..300 and its left band 120..160
        g.Scroll.OffsetX = 50;
        Hold(g, 200, 125);

        Assert.True(g.Input.IsAutoScrolling);
    }

    [Fact]
    public void WithAFrozenColumn_TheLeftBandStartsAtTheFrozenEdge_NotTheGridEdge()
    {
        // Name is frozen (0..120): the scrolling pane is 120..300 and its left band 120..160 (#290).
        InputGrid Held(float toX)
        {
            var g = new InputGrid(configure: x => x.Name.IsFrozen = true);
            g.Scroll.OffsetX = 50;
            Hold(g, 200, toX);
            return g;
        }

        Assert.True(Held(125).Input.IsAutoScrolling);   // inside the band, just right of the frozen edge
        Assert.True(Held(159).Input.IsAutoScrolling);   // the band's inner edge
        Assert.False(Held(165).Input.IsAutoScrolling);  // past the band: choosing a drop slot scrolls nothing
        Assert.False(Held(200).Input.IsAutoScrolling);
        // A band measured from the grid's left edge (0..40) would have missed 125 and caught nothing here; the
        // pane's own left edge decides.
    }

    [Fact]
    public void WithAFrozenColumn_TheBandSpeedGrowsFromTheFrozenEdge()
    {
        InputGrid Held(float toX)
        {
            var g = new InputGrid(configure: x => x.Name.IsFrozen = true);
            g.Scroll.OffsetX = 150;
            Hold(g, 200, toX);
            g.Input.UpdateAutoScroll(g.Scroll, 100f);
            return g;
        }

        float shallow = 150 - Held(155).Scroll.OffsetX; // distance scrolled back
        float deep = 150 - Held(121).Scroll.OffsetX;

        Assert.True(shallow > 0);
        Assert.True(deep > shallow);
    }

    [Fact]
    public void WithAFrozenColumn_TheLeftBandDoesNotAutoscrollAtTheContentStart()
    {
        var g = new InputGrid(configure: x => x.Name.IsFrozen = true);
        Hold(g, 250, 125);

        Assert.False(g.Input.IsAutoScrolling); // OffsetX is 0: nothing to scroll back to
    }

    [Fact]
    public void WithARightFrozenColumn_TheRightBandEndsAtTheFrozenEdge()
    {
        // Score is frozen right (200..300): the scrolling pane is 0..200 and its right band 160..200.
        InputGrid Held(float toX)
        {
            var g = new InputGrid(configure: x => x.Score.IsFrozenRight = true);
            Hold(g, 60, toX);
            return g;
        }

        Assert.True(Held(165).Input.IsAutoScrolling);
        Assert.True(Held(195).Input.IsAutoScrolling);
        Assert.False(Held(140).Input.IsAutoScrolling);
        Assert.False(Held(0).Input.IsAutoScrolling); // the pane's left band: no scroll back at the content start
    }

    [Fact]
    public void WithoutFrozenColumns_TheBandsKeepTheGridEdges()
    {
        var left = new InputGrid();
        left.Scroll.OffsetX = 100;
        Hold(left, 250, 39);
        Assert.True(left.Input.IsAutoScrolling);

        var past = new InputGrid();
        past.Scroll.OffsetX = 100;
        Hold(past, 250, 45);
        Assert.False(past.Input.IsAutoScrolling);

        var right = new InputGrid();
        Hold(right, 60, 261);
        Assert.True(right.Input.IsAutoScrolling);
    }

    [Fact]
    public void ADragWithAutoscrollTurnedOff_DoesNotScroll()
    {
        var g = new InputGrid(configure: x => x.Input.ScrollSettings.AutoScrollMaxSpeed = 0);
        Hold(g, 60, 295);

        Assert.False(g.Input.IsAutoScrolling);
    }

    [Fact]
    public void AutoscrollSteps_AllocateNothing()
    {
        var g = new InputGrid();
        Hold(g, 60, 295);
        g.Input.UpdateAutoScroll(g.Scroll, 1f); // warm up

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 20; i++) g.Input.UpdateAutoScroll(g.Scroll, 1f);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }
}

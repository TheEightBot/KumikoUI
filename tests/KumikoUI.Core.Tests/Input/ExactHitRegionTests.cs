using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.Input;

/// <summary>
/// Hit regions that match what the renderer drew (#101, INPUT-12, INPUT-15): grouping panel chips measured while
/// drawing and published to the connected controller, resize grips sized by the style and the pointer type, the filter
/// icon and group chevron at their drawn positions.
/// </summary>
public class ExactHitRegionTests
{
    private const float PanelY = 20f; // the middle of the 40 px grouping panel

    private static InputGrid Grouped(string? caption = null, Action<InputGrid>? configure = null) =>
        new(rows: 4, viewportWidth: 700, viewportHeight: 400, configure: x =>
        {
            x.Renderer.InputController = x.Input;
            x.Source.AddGroupDescription(new GroupDescription("City", caption));
            configure?.Invoke(x);
        });

    private static KumikoHitTestResult ControllerHit(InputGrid g, float x, float y,
        KumikoPointerDeviceType device = KumikoPointerDeviceType.Mouse) =>
        g.Input.HitTester.HitTest(x, y, g.Scroll, g.Style, g.Source.Columns, g.Source.RowCount, g.Source, device);

    // ── Grouping panel chips ──

    [Fact]
    public void ConnectedRenderer_PublishesTheChipsItDrew()
    {
        var g = Grouped("A much longer custom caption");

        var chips = g.Input.HitTester.RenderedRegions!.Chips;

        Assert.Single(chips);
        Assert.Same(g.Source.GroupDescriptions[0], chips[0].Group);
        // Measured text (8 px a character in the test context), not the 0.6 × font size estimate
        float measured = "A much longer custom caption".Length * RecordingDrawingContext.CharWidth;
        Assert.Equal(InteractionGeometry.ChipWidth(measured), chips[0].Bounds.Width, 3);
    }

    [Fact]
    public void ChipRemoveButton_IsHitWhereItIsDrawn_WhereTheEstimateMissesIt()
    {
        var g = Grouped("A much longer custom caption");
        var chip = g.Input.HitTester.RenderedRegions!.Chips[0].Bounds;
        float removeX = chip.Right - 6;

        Assert.Equal(HitRegion.GroupPanelChipRemove, ControllerHit(g, removeX, PanelY).Region);
        // A tester no renderer publishes to can only estimate the text widths, and puts the button elsewhere
        var estimate = new KumikoHitTester().HitTest(removeX, PanelY, g.Scroll, g.Style, g.Source.Columns,
            g.Source.RowCount, g.Source);
        Assert.NotEqual(HitRegion.GroupPanelChipRemove, estimate.Region);
    }

    [Fact]
    public void TappingTheDrawnRemoveButton_RemovesTheGrouping()
    {
        var g = Grouped("A much longer custom caption");
        var chip = g.Input.HitTester.RenderedRegions!.Chips[0].Bounds;

        g.Tap(chip.Right - 6, PanelY);

        Assert.Empty(g.Source.GroupDescriptions);
    }

    [Fact]
    public void SecondChip_IsHitWhereItIsDrawn()
    {
        var g = Grouped("A much longer custom caption", x => x.Source.AddGroupDescription(new GroupDescription("Dept")));
        var second = g.Input.HitTester.RenderedRegions!.Chips[1].Bounds;

        var hit = ControllerHit(g, second.X + 6, PanelY);

        Assert.Equal(HitRegion.GroupPanelChip, hit.Region);
        Assert.Equal(1, hit.GroupDescriptionIndex);
    }

    [Fact]
    public void ChangedGroups_FallBackToTheEstimate_UntilTheNextFrame()
    {
        var g = Grouped();
        g.Source.AddGroupDescription(new GroupDescription("Dept")); // not drawn yet

        Assert.False(g.Input.HitTester.RenderedRegions!.Matches(g.Source.GroupDescriptions, g.Style.GroupPanelHeight));
        var estimated = ControllerHit(g, 0, PanelY);
        Assert.Equal(HitRegion.EmptyArea, estimated.Region);

        g.Render();
        Assert.True(g.Input.HitTester.RenderedRegions!.Matches(g.Source.GroupDescriptions, g.Style.GroupPanelHeight));
    }

    [Fact]
    public void Chip_ForAFinger_SpansThePanelHeight()
    {
        var g = Grouped();
        var chip = g.Input.HitTester.RenderedRegions!.Chips[0].Bounds;
        float x = chip.X + 10;

        Assert.Equal(HitRegion.EmptyArea, ControllerHit(g, x, 3).Region);
        Assert.Equal(HitRegion.GroupPanelChip, ControllerHit(g, x, 3, KumikoPointerDeviceType.Touch).Region);
    }

    [Fact]
    public void DisconnectingTheRenderer_ClearsThePublishedRegions()
    {
        var g = Grouped();

        g.Renderer.InputController = null;

        Assert.Null(g.Input.HitTester.RenderedRegions);
    }

    // ── Resize grips ──

    // Age spans 120..200 in the header.

    [Theory]
    [InlineData(194f, true)]
    [InlineData(206f, true)]
    [InlineData(193f, false)]
    [InlineData(207f, false)]
    public void ResizeGrip_ForAMouse_IsTheStyleWidthCenteredOnTheEdge(float x, bool onGrip)
    {
        var g = new InputGrid();

        Assert.Equal(onGrip, g.Tester.HitTest(x, InputGrid.HeaderY, g.Scroll, g.Style, g.Source.Columns,
            g.Source.RowCount, g.Source, KumikoPointerDeviceType.Mouse).Region == HitRegion.HeaderResizeGrip);
    }

    [Theory]
    [InlineData(188f, true)]
    [InlineData(212f, true)]
    [InlineData(187f, false)]
    [InlineData(213f, false)]
    public void ResizeGrip_ForAFinger_IsTheTouchWidth(float x, bool onGrip)
    {
        var g = new InputGrid(configure: x => x.Age.AllowFiltering = false); // the drawn filter icon wins over the grip

        Assert.Equal(onGrip, g.Tester.HitTest(x, InputGrid.HeaderY, g.Scroll, g.Style, g.Source.Columns,
            g.Source.RowCount, g.Source, KumikoPointerDeviceType.Touch).Region == HitRegion.HeaderResizeGrip);
    }

    [Fact]
    public void ResizeGrip_FollowsTheStyle()
    {
        var g = new InputGrid(configure: x => { x.Style.ColumnResizeGripWidth = 20; x.Age.AllowFiltering = false; });

        Assert.Equal(HitRegion.HeaderResizeGrip, g.Hit(191, InputGrid.HeaderY).Region);
        Assert.Equal(HitRegion.HeaderResizeGrip, g.Hit(210, InputGrid.HeaderY).Region);
        Assert.NotEqual(HitRegion.HeaderResizeGrip, g.Hit(189, InputGrid.HeaderY).Region);
    }

    [Fact]
    public void ResizeGrip_OnANarrowColumn_LeavesItsMiddleToTheHeader()
    {
        var g = new InputGrid(configure: x => { x.Age.Width = 40; x.Age.AllowFiltering = false; }); // Age: 120..160

        var middle = g.Tester.HitTest(140, InputGrid.HeaderY, g.Scroll, g.Style, g.Source.Columns, g.Source.RowCount,
            g.Source, KumikoPointerDeviceType.Touch);
        var nearEdge = g.Tester.HitTest(153, InputGrid.HeaderY, g.Scroll, g.Style, g.Source.Columns, g.Source.RowCount,
            g.Source, KumikoPointerDeviceType.Touch);

        Assert.Equal(HitRegion.HeaderCell, middle.Region);
        Assert.Equal(HitRegion.HeaderResizeGrip, nearEdge.Region); // 7 px inside the edge, within the 8 px floor
    }

    // Age spans 120..(120 + width); the point is `inside` px left of its right edge. A finger's grip reaches the
    // smaller of 12 px (half the default 24 px touch grip) and a fifth of the column (at least 8 px) into it.
    [Theory]
    [InlineData(40f, 8f, true)]
    [InlineData(40f, 9f, false)]
    [InlineData(60f, 12f, true)]
    [InlineData(60f, 13f, false)] // a quarter would still be a resize here
    [InlineData(100f, 12f, true)]
    [InlineData(100f, 13f, false)]
    [InlineData(100f, 16f, false)] // #289: 16 px inside a wide column is a reorder press, not a resize
    public void ResizeGrip_ForAFinger_ReachesAtMostAFifthIntoAColumn(float width, float inside, bool onGrip)
    {
        var g = new InputGrid(configure: c => { c.Age.Width = width; c.Age.AllowFiltering = false; });

        Assert.Equal(onGrip, g.Tester.HitTest(120 + width - inside, InputGrid.HeaderY, g.Scroll, g.Style,
            g.Source.Columns, g.Source.RowCount, g.Source, KumikoPointerDeviceType.Touch).Region == HitRegion.HeaderResizeGrip);
    }

    [Fact]
    public void ResizeGrip_ForAFinger_InANarrowColumn_ReorderPressNearTheRightIsAHeaderCell()
    {
        var g = new InputGrid(configure: c => { c.Age.Width = 60; c.Age.AllowFiltering = false; }); // Age: 120..180

        var hit = g.Tester.HitTest(167, InputGrid.HeaderY, g.Scroll, g.Style, g.Source.Columns, g.Source.RowCount,
            g.Source, KumikoPointerDeviceType.Touch); // 13 px left of the border: a quarter-wide grip (15 px) made this a resize

        Assert.Equal(HitRegion.HeaderCell, hit.Region);
        Assert.Same(g.Age, hit.Column);
    }

    [Fact]
    public void ResizeGrip_ForAMouse_StillReachesAQuarterOfANarrowColumn()
    {
        var g = new InputGrid(configure: c => { c.Style.ColumnResizeGripWidth = 40; c.Age.Width = 40; c.Age.AllowFiltering = false; }); // Age: 120..160

        Assert.Equal(HitRegion.HeaderResizeGrip, g.Hit(150, InputGrid.HeaderY).Region); // 10 px inside: a quarter of 40
        Assert.Equal(HitRegion.HeaderCell, g.Hit(149, InputGrid.HeaderY).Region);
    }

    [Fact]
    public void PressingWithAFinger_InsideTheTouchGrip_Resizes()
    {
        var g = new InputGrid(configure: x => x.Age.AllowFiltering = false);

        g.Pointer(InputAction.Pressed, 190, InputGrid.HeaderY, deviceType: KumikoPointerDeviceType.Touch);
        g.Pointer(InputAction.Moved, 230, InputGrid.HeaderY, deviceType: KumikoPointerDeviceType.Touch);
        g.Pointer(InputAction.Released, 230, InputGrid.HeaderY, deviceType: KumikoPointerDeviceType.Touch);

        Assert.Equal(120, g.Age.Width);
    }

    // ── Filter icon ──

    [Fact]
    public void FilterIcon_OfASortedColumn_SitsLeftOfTheSortArrow_AndTheArrowSorts()
    {
        var g = new InputGrid();
        g.Source.ToggleSort(g.Age); // the icon moves left by the arrow's 12 px: drawn at 170..180, the arrow at 184..192
        g.Render();

        Assert.Equal(HitRegion.HeaderFilterIcon, g.Hit(175, InputGrid.HeaderY).Region);
        Assert.Equal(HitRegion.HeaderCell, g.Hit(188, InputGrid.HeaderY).Region);
    }

    [Fact]
    public void FilterIcon_ForAFinger_HasAWiderTarget()
    {
        var g = new InputGrid(); // drawn at 182..192

        var mouse = g.Tester.HitTest(173, InputGrid.HeaderY, g.Scroll, g.Style, g.Source.Columns, g.Source.RowCount,
            g.Source, KumikoPointerDeviceType.Mouse);
        var touch = g.Tester.HitTest(173, InputGrid.HeaderY, g.Scroll, g.Style, g.Source.Columns, g.Source.RowCount,
            g.Source, KumikoPointerDeviceType.Touch);

        Assert.Equal(HitRegion.HeaderCell, mouse.Region);
        Assert.Equal(HitRegion.HeaderFilterIcon, touch.Region);
    }

    [Fact]
    public void FilterIcon_AsDrawn_WinsOverAFingersWideResizeGrip()
    {
        var g = new InputGrid(); // Age's icon drawn at 182..192; a finger's grip reaches from 184

        var onIcon = g.Tester.HitTest(190, InputGrid.HeaderY, g.Scroll, g.Style, g.Source.Columns, g.Source.RowCount,
            g.Source, KumikoPointerDeviceType.Touch);
        var pastIcon = g.Tester.HitTest(194, InputGrid.HeaderY, g.Scroll, g.Style, g.Source.Columns, g.Source.RowCount,
            g.Source, KumikoPointerDeviceType.Touch);

        Assert.Equal(HitRegion.HeaderFilterIcon, onIcon.Region);
        Assert.Equal(HitRegion.HeaderResizeGrip, pastIcon.Region);
    }

    // ── Group chevron ──

    [Fact]
    public void GroupChevron_AfterALeftDragHandle_IsWhereItIsDrawn()
    {
        var g = new InputGrid(rows: 4, viewportHeight: 400, configure: x =>
        {
            x.Style.ShowRowDragHandle = true; // 32 px on the left: the chevron is drawn from 40
            x.Source.AddGroupDescription(new GroupDescription("City"));
        });
        float y = 40 + 40 + 18;

        Assert.Equal(HitRegion.GroupChevron, g.Hit(45, y).Region);
        Assert.Equal(HitRegion.GroupHeaderRow, g.Hit(150, y).Region);
    }
}

using KumikoUI.Core.Layout;
using KumikoUI.Core.Models;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests;

/// <summary>
/// Programmatic scrolling (#56): where rows and columns are (group headers, frozen rows, header, panel), the offsets
/// that bring them into view for each <see cref="KumikoScrollPosition"/>, scrolling to an item after a filter or in
/// a collapsed group, and <see cref="ScrollState.ScrollChanged"/> carrying the offsets.
/// </summary>
/// <remarks>
/// Geometry (<see cref="RenderHarness"/>, 600x400 viewport): header 40, rows 36, grouping panel 40, summary rows 36.
/// </remarks>
public class KumikoScrollerTests
{
    private const float Header = 40f;
    private const float Row = 36f;
    private const float Panel = 40f;

    private static RenderHarness Grid(int rows = 100, Action<RenderHarness>? configure = null)
    {
        var h = RenderHarness.Create(rows: rows, configure: configure);
        h.Render(); // sizes the content and viewport like the first paint
        return h;
    }

    private static float Y(RenderHarness h, int row) => KumikoScroller.GetRowY(h.Source, h.Style, h.Scroll, row);

    private static bool FullyVisible(RenderHarness h, int row, float top, float bottom)
    {
        float y = Y(h, row);
        return y >= top - 0.01f && y + Row <= bottom + 0.01f;
    }

    // ── Row Y ──

    [Fact]
    public void GetRowY_PlainGrid_IsHeaderPlusRowsMinusOffset()
    {
        var h = Grid();
        h.Scroll.OffsetY = 100;

        Assert.Equal(Header - 100, Y(h, 0), 2);
        Assert.Equal(Header + 5 * Row - 100, Y(h, 5), 2);
    }

    [Fact]
    public void GetRowY_WithGroupHeaders_CountsThePanelAndEveryGroupRow()
    {
        // 100 people by City: row 0 Oslo header, rows 1-50 Oslo, row 51 Rome header, rows 52-101 Rome.
        var h = Grid(configure: g => g.Source.AddGroupDescription(new GroupDescription("City")));

        Assert.Equal(Panel + Header, Y(h, 0), 2);                   // the first group header
        Assert.Equal(Panel + Header + Row, Y(h, 1), 2);             // first Oslo row
        Assert.Equal(Panel + Header + 51 * Row, Y(h, 51), 2);       // the Rome header: after 50 Oslo rows
        Assert.True(h.Source.IsGroupHeaderRow(51));
    }

    [Fact]
    public void GetRowY_WithTopSummaryAndFrozenRows_FrozenRowsStayPutAndScrollingRowsMove()
    {
        var h = Grid(configure: g =>
        {
            g.Source.FrozenRowCount = 2;
            g.Source.AddTableSummaryRow(new TableSummaryRow { Name = "total", Position = SummaryPosition.Top });
        });
        float rowsTop = Header + Row; // header + one top summary row
        h.Scroll.OffsetY = 100;

        Assert.Equal(rowsTop, Y(h, 0), 2);
        Assert.Equal(rowsTop + Row, Y(h, 1), 2);
        // Row 5 is the fourth scrolling row (rows 0-1 are frozen): 2 frozen rows, then (5 - 2) rows, minus the offset.
        Assert.Equal(rowsTop + 2 * Row + 3 * Row - 100, Y(h, 5), 2);
    }

    // ── Rows ──

    [Fact]
    public void ScrollToRow_Nearest_ScrollsTheLeastAndLeavesAVisibleRowAlone()
    {
        var h = Grid();
        float viewportBottom = h.Scroll.ViewportHeight;

        Assert.True(KumikoScroller.ScrollToRow(h.Scroll, h.Source, h.Style, 5)); // already visible
        Assert.Equal(0, h.Scroll.OffsetY);

        KumikoScroller.ScrollToRow(h.Scroll, h.Source, h.Style, 50);
        Assert.True(FullyVisible(h, 50, Header, viewportBottom));
        Assert.Equal(viewportBottom, Y(h, 50) + Row, 2); // scrolled down: the row ends at the bottom edge

        KumikoScroller.ScrollToRow(h.Scroll, h.Source, h.Style, 10);
        Assert.Equal(Header, Y(h, 10), 2); // scrolled back up: the row starts at the top edge
    }

    [Fact]
    public void ScrollToRow_StartCenterEnd_PlaceTheRow()
    {
        var h = Grid();
        float visible = h.Scroll.ViewportHeight - Header;

        KumikoScroller.ScrollToRow(h.Scroll, h.Source, h.Style, 40, KumikoScrollPosition.Start);
        Assert.Equal(Header, Y(h, 40), 2);

        KumikoScroller.ScrollToRow(h.Scroll, h.Source, h.Style, 40, KumikoScrollPosition.End);
        Assert.Equal(h.Scroll.ViewportHeight, Y(h, 40) + Row, 2);

        KumikoScroller.ScrollToRow(h.Scroll, h.Source, h.Style, 40, KumikoScrollPosition.Center);
        Assert.Equal(Header + (visible - Row) / 2, Y(h, 40), 2);
    }

    [Fact]
    public void ScrollToRow_IsClampedToTheScrollableRange()
    {
        var h = Grid(rows: 100);

        KumikoScroller.ScrollToRow(h.Scroll, h.Source, h.Style, 99, KumikoScrollPosition.Start); // cannot put the last row at the top
        Assert.Equal(h.Scroll.MaxOffsetY, h.Scroll.OffsetY);

        KumikoScroller.ScrollToRow(h.Scroll, h.Source, h.Style, 0, KumikoScrollPosition.End);
        Assert.Equal(0, h.Scroll.OffsetY);
    }

    [Fact]
    public void ScrollToRow_OutOfRange_ReturnsFalseAndScrollsNothing()
    {
        var h = Grid(rows: 10);
        h.Scroll.OffsetY = 12;

        Assert.False(KumikoScroller.ScrollToRow(h.Scroll, h.Source, h.Style, -1));
        Assert.False(KumikoScroller.ScrollToRow(h.Scroll, h.Source, h.Style, 10));
        Assert.Equal(12, h.Scroll.OffsetY);
    }

    [Fact]
    public void ScrollToRow_FrozenRow_DoesNotScroll()
    {
        var h = Grid(configure: g => g.Source.FrozenRowCount = 3);
        h.Scroll.OffsetY = 200;

        KumikoScroller.ScrollToRow(h.Scroll, h.Source, h.Style, 1, KumikoScrollPosition.Start);

        Assert.Equal(200, h.Scroll.OffsetY);
    }

    [Fact]
    public void ScrollToRow_WithFrozenRows_KeepsTheRowBelowThem()
    {
        var h = Grid(configure: g => g.Source.FrozenRowCount = 2);

        KumikoScroller.ScrollToRow(h.Scroll, h.Source, h.Style, 30, KumikoScrollPosition.Start);

        Assert.Equal(Header + 2 * Row, Y(h, 30), 2); // right under the two frozen rows
        h.Render();
        Assert.Equal(Header + 2 * Row, Y(h, 30), 2); // and the paint's own clamp agrees
    }

    [Fact]
    public void ScrollToRow_Start_PlacesTheRowBelowTheStickyGroupHeaderPinnedOverIt()
    {
        var h = Grid(configure: g => g.Source.AddGroupDescription(new GroupDescription("City")));
        float rowsTop = Panel + Header;

        KumikoScroller.ScrollToRow(h.Scroll, h.Source, h.Style, 20, KumikoScrollPosition.Start); // an Oslo row

        Assert.Equal(rowsTop + Row, Y(h, 20), 2); // one pinned group header above it
    }

    [Fact]
    public void ScrollToRow_Start_WithStickyHeadersOff_PlacesTheRowAtTheTop()
    {
        var h = Grid(configure: g =>
        {
            g.Source.AddGroupDescription(new GroupDescription("City"));
            g.Style.StickyGroupHeaders = false;
        });

        KumikoScroller.ScrollToRow(h.Scroll, h.Source, h.Style, 20, KumikoScrollPosition.Start);

        Assert.Equal(Panel + Header, Y(h, 20), 2);
    }

    [Fact]
    public void ScrollToRow_AboveThePinnedFooter_KeepsTheRowOutOfIt()
    {
        var h = Grid(configure: g =>
        {
            g.Style.PinBottomSummaryRows = true;
            g.Source.AddTableSummaryRow(new TableSummaryRow { Name = "total", Position = SummaryPosition.Bottom });
        });

        KumikoScroller.ScrollToRow(h.Scroll, h.Source, h.Style, 60, KumikoScrollPosition.End);

        Assert.Equal(h.Scroll.ViewportHeight - Row, Y(h, 60) + Row, 2); // ends one summary row above the bottom edge
    }

    // ── Items ──

    [Fact]
    public void ScrollToItem_AfterAFilter_LandsTheItemVisible()
    {
        var h = Grid(rows: 100);
        var target = h.People[97]; // Age 22
        h.Source.SetFilter(o => ((RenderHarness.Person)o).Age == 22);
        Assert.Equal(20, h.Source.RowCount);

        Assert.True(KumikoScroller.ScrollToItem(h.Scroll, h.Source, h.Style, target));

        var ctx = h.Render();
        int row = h.Source.IndexOfItem(target);
        Assert.Equal(19, row); // the last of the 20 filtered rows, not row 97
        Assert.True(FullyVisible(h, row, Header, h.Scroll.ViewportHeight));
        Assert.Contains(ctx.Texts, o => o.Text == "P97" && o.Visible is not null);
    }

    [Fact]
    public void ScrollToItem_AfterASort_FollowsTheItemsNewRow()
    {
        var h = Grid(rows: 100);
        h.Source.SetSort(h.Column("Name"), KumikoSortDirection.Descending);
        var target = h.People[0]; // "P00" is now the last row

        Assert.True(KumikoScroller.ScrollToItem(h.Scroll, h.Source, h.Style, target, KumikoScrollPosition.Center));

        Assert.Equal(99, h.Source.IndexOfItem(target));
        Assert.True(FullyVisible(h, 99, Header, h.Scroll.ViewportHeight));
    }

    [Fact]
    public void ScrollToItem_FilteredOutOrUnknown_ReturnsFalseAndScrollsNothing()
    {
        var h = Grid(rows: 100);
        h.Source.SetFilter(o => ((RenderHarness.Person)o).Age == 22);
        h.Scroll.OffsetY = 30;

        Assert.False(KumikoScroller.ScrollToItem(h.Scroll, h.Source, h.Style, h.People[0])); // Age 20: hidden
        Assert.False(KumikoScroller.ScrollToItem(h.Scroll, h.Source, h.Style, new RenderHarness.Person()));
        Assert.Equal(30, h.Scroll.OffsetY);
    }

    [Fact]
    public void ScrollToItem_InACollapsedGroup_ExpandsItByDefault()
    {
        var h = Grid(rows: 100, configure: g =>
        {
            g.Source.AddGroupDescription(new GroupDescription("City"));
            g.Source.CollapseAllGroups();
        });
        Assert.Equal(2, h.Source.RowCount); // two collapsed headers
        var target = h.People[98]; // Oslo, 50th of 50

        Assert.True(KumikoScroller.ScrollToItem(h.Scroll, h.Source, h.Style, target));

        int row = h.Source.IndexOfItem(target);
        Assert.Equal(50, row); // header + 50 Oslo rows, the Rome header collapsed after them
        Assert.True(h.Source.GetGroupHeaderInfo(0)!.IsExpanded);
        Assert.False(h.Source.GetGroupHeaderInfo(51)!.IsExpanded);
        Assert.True(FullyVisible(h, row, Panel + Header, h.Scroll.ViewportHeight));
    }

    [Fact]
    public void ScrollToItem_InACollapsedGroup_WithExpandGroupsOff_IsNotFoundAndLeavesTheGroupCollapsed()
    {
        var h = Grid(rows: 100, configure: g =>
        {
            g.Source.AddGroupDescription(new GroupDescription("City"));
            g.Source.CollapseAllGroups();
        });

        Assert.False(KumikoScroller.ScrollToItem(h.Scroll, h.Source, h.Style, h.People[98],
            KumikoScrollPosition.Nearest, expandGroups: false));

        Assert.Equal(2, h.Source.RowCount);
    }

    [Fact]
    public void ScrollToItem_InNestedCollapsedGroups_ExpandsOutermostFirst()
    {
        var h = Grid(rows: 100, configure: g =>
        {
            g.Source.AddGroupDescription(new GroupDescription("City"));
            g.Source.AddGroupDescription(new GroupDescription("Age"));
            g.Source.CollapseAllGroups();
        });
        var target = h.People[3]; // Rome, Age 23

        Assert.True(KumikoScroller.ScrollToItem(h.Scroll, h.Source, h.Style, target));

        int row = h.Source.IndexOfItem(target);
        Assert.True(row > 0);
        Assert.True(FullyVisible(h, row, Panel + Header, h.Scroll.ViewportHeight));
    }

    [Fact]
    public void ScrollToItem_ACanceledExpansion_IsNotFound()
    {
        var h = Grid(rows: 100, configure: g =>
        {
            g.Source.AddGroupDescription(new GroupDescription("City"));
            g.Source.CollapseAllGroups();
            g.Source.GroupExpanding += (_, e) => e.Cancel = true;
        });

        Assert.False(KumikoScroller.ScrollToItem(h.Scroll, h.Source, h.Style, h.People[0]));
        Assert.Equal(2, h.Source.RowCount);
    }

    // ── Columns ──

    private static RenderHarness NarrowWithFrozenName() =>
        Grid(configure: g =>
        {
            g.Scroll.ViewportWidth = 300;
            g.Column("Name").FreezeMode = ColumnFreezeMode.Left;
        });

    [Fact]
    public void ScrollToColumn_ScrollingColumn_UsesTheSpaceBesideTheFrozenColumn()
    {
        // Name 150 frozen; scrolling area is 150 wide: Age 100 (content x 150), City 150 (x 250), Salary 150 (x 400).
        var h = NarrowWithFrozenName();
        var cols = h.Source.Columns;

        KumikoScroller.ScrollToColumn(h.Scroll, h.Source, h.Style, 2, KumikoScrollPosition.End); // City
        Assert.Equal(100, h.Scroll.OffsetX);
        Assert.Equal(150, KumikoScroller.GetColumnX(cols, h.Style, h.Scroll, 2), 2); // right behind the frozen column

        KumikoScroller.ScrollToColumn(h.Scroll, h.Source, h.Style, 1, KumikoScrollPosition.Nearest); // Age, now off the left
        Assert.Equal(0, h.Scroll.OffsetX);

        KumikoScroller.ScrollToColumn(h.Scroll, h.Source, h.Style, 3, KumikoScrollPosition.Start); // Salary: clamped
        Assert.Equal(h.Scroll.MaxOffsetX, h.Scroll.OffsetX);
    }

    [Fact]
    public void ScrollToColumn_CenterAndStart_PlaceTheColumn()
    {
        var h = NarrowWithFrozenName();
        var cols = h.Source.Columns;

        KumikoScroller.ScrollToColumn(h.Scroll, h.Source, h.Style, 2, KumikoScrollPosition.Start); // City at content x 250
        Assert.Equal(100, h.Scroll.OffsetX); // clamped: 550 content - 300 viewport = 250 max, but 250 - 150 = 100
        Assert.Equal(150, KumikoScroller.GetColumnX(cols, h.Style, h.Scroll, 2), 2);

        h.Scroll.OffsetX = 0;
        KumikoScroller.ScrollToColumn(h.Scroll, h.Source, h.Style, 1, KumikoScrollPosition.Center); // Age 100 in a 150 area
        Assert.Equal(0, h.Scroll.OffsetX); // would be -25: clamped
    }

    [Fact]
    public void ScrollToColumn_FrozenHiddenOrOutOfRange_DoesNotScroll()
    {
        var h = NarrowWithFrozenName();
        h.Column("Age").IsVisible = false;
        h.Scroll.OffsetX = 40;

        Assert.True(KumikoScroller.ScrollToColumn(h.Scroll, h.Source, h.Style, 0)); // frozen: always visible
        Assert.False(KumikoScroller.ScrollToColumn(h.Scroll, h.Source, h.Style, 1));  // hidden
        Assert.False(KumikoScroller.ScrollToColumn(h.Scroll, h.Source, h.Style, 9));
        Assert.Equal(40, h.Scroll.OffsetX);
    }

    // ── Scroll events ──

    [Fact]
    public void ScrollToRow_RaisesScrollChangedOnceWithBothOffsets()
    {
        var h = Grid();
        var events = new List<KumikoScrollChangedEventArgs>();
        h.Scroll.ScrollChanged += (_, e) => events.Add(e);
        h.Scroll.OffsetX = 0;

        KumikoScroller.ScrollToRow(h.Scroll, h.Source, h.Style, 50, KumikoScrollPosition.Start);

        var e = Assert.Single(events);
        Assert.Equal(0, e.OldOffsetY);
        Assert.Equal(50 * Row, e.OffsetY, 2);
        Assert.Equal(h.Scroll.OffsetX, e.OffsetX);
    }

    [Fact]
    public void ScrollTo_ClampsAndRaisesOnceOnlyWhenSomethingMoved()
    {
        var scroll = new ScrollState { ViewportWidth = 100, ViewportHeight = 100, ContentWidth = 300, ContentHeight = 500 };
        var events = new List<KumikoScrollChangedEventArgs>();
        scroll.ScrollChanged += (_, e) => events.Add(e);

        scroll.ScrollTo(50, 1000);
        scroll.ScrollTo(50, 400); // already there after clamping
        scroll.ScrollTo(-5, -5);

        Assert.Equal(2, events.Count);
        Assert.Equal((50f, 400f), (events[0].OffsetX, events[0].OffsetY));
        Assert.Equal((0f, 0f), (events[1].OffsetX, events[1].OffsetY));
    }

    [Fact]
    public void ScrolledEventArgs_ReportOffsetsDeltasAndExtents()
    {
        var args = new KumikoScrolledEventArgs(10, 200, 4, 150, 800, 3000, 300, 400);

        Assert.Equal(6, args.DeltaX);
        Assert.Equal(50, args.DeltaY);
        Assert.Equal(500, args.MaxOffsetX);
        Assert.Equal(2600, args.MaxOffsetY);
    }

    // ── Before layout ──

    [Fact]
    public void ScrollToRow_BeforeTheFirstPaint_StillLands()
    {
        var h = RenderHarness.Create(rows: 100); // never rendered: content size not computed yet

        KumikoScroller.ScrollToRow(h.Scroll, h.Source, h.Style, 60, KumikoScrollPosition.Start);

        Assert.Equal(60 * Row, h.Scroll.OffsetY, 2);
    }
}

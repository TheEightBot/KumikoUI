using KumikoUI.Core.Input;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests.Input;

/// <summary>
/// The hit-test matrix of <see cref="KumikoHitTester"/>: which region, row and column a viewport point
/// resolves to across the freeze panes, header, bands, grouping and the row drag handle.
/// </summary>
public class HitTesterTests
{
    private static InputGrid Wide(Action<InputGrid>? configure = null, int rows = 30) =>
        new(rows, viewportWidth: 700, configure: configure);

    // ── Cells and panes ──

    [Fact]
    public void Cell_InScrollablePane_ResolvesRowAndColumn()
    {
        var g = new InputGrid();

        var hit = g.Hit(150, g.RowY(2));

        Assert.Equal(HitRegion.Cell, hit.Region);
        Assert.Equal(2, hit.RowIndex);
        Assert.Equal(1, hit.ColumnIndex);
        Assert.Same(g.Age, hit.Column);
        Assert.Equal(150, hit.ContentX);
        Assert.Equal(g.RowY(2), hit.ContentY);
    }

    [Fact]
    public void Cell_WithHorizontalScroll_MapsViewportToContent()
    {
        var g = new InputGrid();
        g.Scroll.OffsetX = 100;

        var hit = g.Hit(50, g.RowY(0)); // content x 150

        Assert.Equal(HitRegion.Cell, hit.Region);
        Assert.Equal(1, hit.ColumnIndex);
        Assert.Equal(150, hit.ContentX);
    }

    [Fact]
    public void Cell_WithVerticalScroll_MapsViewportToContent()
    {
        var g = new InputGrid();
        g.Scroll.OffsetY = 72; // two rows

        var hit = g.Hit(10, 40 + 5);

        Assert.Equal(2, hit.RowIndex);
        Assert.Equal(45 + 72, hit.ContentY);
    }

    [Fact]
    public void Cell_InLeftFrozenPane_IgnoresHorizontalScroll()
    {
        var g = new InputGrid(configure: x => x.Name.IsFrozen = true);
        g.Scroll.OffsetX = 100;

        var hit = g.Hit(50, g.RowY(1));

        Assert.Equal(HitRegion.Cell, hit.Region);
        Assert.Same(g.Name, hit.Column);
        Assert.Equal(0, hit.ColumnIndex);
    }

    [Fact]
    public void Cell_RightOfLeftFrozenPane_IsTheScrollablePaneAfterTheFrozenWidth()
    {
        var g = new InputGrid(configure: x => x.Name.IsFrozen = true);
        g.Scroll.OffsetX = 100;

        var hit = g.Hit(150, g.RowY(1)); // content x 250: scrollable columns start at the frozen width (120)

        Assert.Same(g.City, hit.Column);
        Assert.Equal(2, hit.ColumnIndex);
    }

    [Fact]
    public void Cell_InRightFrozenPane_IgnoresHorizontalScroll()
    {
        var g = new InputGrid(configure: x => x.Score.IsFrozenRight = true);
        g.Scroll.OffsetX = 50;

        var hit = g.Hit(250, g.RowY(1)); // pane spans 200..300

        Assert.Equal(HitRegion.Cell, hit.Region);
        Assert.Same(g.Score, hit.Column);
        Assert.Equal(4, hit.ColumnIndex);
    }

    [Fact]
    public void Cell_LeftOfRightFrozenPane_IsScrollable()
    {
        var g = new InputGrid(configure: x => x.Score.IsFrozenRight = true);

        var hit = g.Hit(150, g.RowY(0));

        Assert.Same(g.Age, hit.Column);
    }

    [Fact]
    public void Cell_WithBothFrozenPanes_ResolvesEachPane()
    {
        var g = new InputGrid(configure: x =>
        {
            x.Name.IsFrozen = true;
            x.Score.IsFrozenRight = true;
            x.Style.MaxFrozenColumnsWidthRatio = 1f; // both panes pinned: 220 of the 300 px exceeds the default share
        });
        g.Scroll.OffsetX = 50;

        Assert.Same(g.Name, g.Hit(10, g.RowY(0)).Column);
        Assert.Same(g.Score, g.Hit(290, g.RowY(0)).Column);
        Assert.Same(g.City, g.Hit(150, g.RowY(0)).Column); // content x 200: scrollable columns start at 120
    }

    [Fact]
    public void Cell_PastTheLastColumn_IsEmptyAreaWithTheRow()
    {
        var g = Wide();

        var hit = g.Hit(650, g.RowY(3));

        Assert.Equal(HitRegion.EmptyArea, hit.Region);
        Assert.Equal(3, hit.RowIndex);
        Assert.Null(hit.Column);
    }

    [Fact]
    public void Cell_HiddenColumnsAreSkipped()
    {
        var g = new InputGrid(configure: x => x.Age.IsVisible = false);

        var hit = g.Hit(130, g.RowY(0)); // where Age was; City now starts at 120

        Assert.Same(g.City, hit.Column);
    }

    [Fact]
    public void BelowTheLastRow_IsEmptyArea()
    {
        var g = new InputGrid(rows: 2, viewportHeight: 400);

        var hit = g.Hit(50, g.RowY(5));

        Assert.Equal(HitRegion.EmptyArea, hit.Region);
        Assert.Equal(-1, hit.RowIndex);
    }

    [Fact]
    public void NegativeOrOutsidePoints_ResolveToEmptyArea_NeverNone()
    {
        var g = new InputGrid(rows: 2, viewportHeight: 400);

        Assert.Equal(HitRegion.EmptyArea, g.Hit(-20, -20).Region);
        Assert.Equal(HitRegion.EmptyArea, g.Hit(5000, g.RowY(0)).Region);
        Assert.Equal(HitRegion.EmptyArea, g.Hit(5000, 20).Region);
        Assert.Equal(HitRegion.None, KumikoHitTestResult.Empty.Region);
        Assert.Equal(-1, KumikoHitTestResult.Empty.RowIndex);
        Assert.Equal(-1, KumikoHitTestResult.Empty.ColumnIndex);
        Assert.Equal(-1, KumikoHitTestResult.Empty.GroupDescriptionIndex);
    }

    [Fact]
    public void HitTestResult_IsAValue_EqualByContent()
    {
        var a = new KumikoHitTestResult { Region = HitRegion.Cell, RowIndex = 2, ColumnIndex = 1, ContentX = 4, ContentY = 9 };
        var b = new KumikoHitTestResult { Region = HitRegion.Cell, RowIndex = 2, ColumnIndex = 1, ContentX = 4, ContentY = 9 };

        Assert.Equal(a, b);
        Assert.NotEqual(a, b with { RowIndex = 3 });
        Assert.Equal(KumikoHitTestResult.Empty, new KumikoHitTestResult());
        Assert.Equal(HitRegion.None, new KumikoHitTestResult().Region);
        Assert.Equal(-1, new KumikoHitTestResult().RowIndex);
    }

    [Fact]
    public void EmptyGrid_HasNothingToHit()
    {
        var g = new InputGrid(rows: 0);

        Assert.Equal(HitRegion.EmptyArea, g.Hit(50, g.RowY(0)).Region);
        Assert.Equal(HitRegion.HeaderCell, g.Hit(50, 20).Region);
    }

    // ── Header ──

    [Fact]
    public void Header_ResolvesTheColumn()
    {
        var g = new InputGrid();

        var hit = g.Hit(130, InputGrid.HeaderY);

        Assert.Equal(HitRegion.HeaderCell, hit.Region);
        Assert.Same(g.Age, hit.Column);
        Assert.Equal(1, hit.ColumnIndex);
    }

    [Fact]
    public void Header_FilterIcon_SitsLeftOfTheResizeGrip()
    {
        var g = new InputGrid();

        // Age spans 120..200: the icon covers 168..194, the grip 194..206.
        Assert.Equal(HitRegion.HeaderFilterIcon, g.Hit(180, InputGrid.HeaderY).Region);
        Assert.Equal(HitRegion.HeaderFilterIcon, g.Hit(168, InputGrid.HeaderY).Region);
        Assert.Equal(HitRegion.HeaderCell, g.Hit(167, InputGrid.HeaderY).Region);
        Assert.Equal(HitRegion.HeaderResizeGrip, g.Hit(196, InputGrid.HeaderY).Region);
    }

    [Fact]
    public void Header_WithFilteringOff_HasNoFilterIcon()
    {
        var g = new InputGrid(configure: x => x.Age.AllowFiltering = false);

        Assert.Equal(HitRegion.HeaderCell, g.Hit(180, InputGrid.HeaderY).Region);
    }

    [Fact]
    public void Header_ResizeGrip_StraddlesTheColumnEdge()
    {
        var g = new InputGrid();

        var left = g.Hit(196, InputGrid.HeaderY);
        var right = g.Hit(204, InputGrid.HeaderY);

        Assert.Equal(HitRegion.HeaderResizeGrip, left.Region);
        Assert.Equal(HitRegion.HeaderResizeGrip, right.Region);
        Assert.Same(g.Age, left.Column);
        Assert.Same(g.Age, right.Column); // the grip belongs to the column on its left
        Assert.Equal(1, right.ColumnIndex);
    }

    [Fact]
    public void Header_WithResizeOff_HasNoGrip()
    {
        var g = new InputGrid(configure: x => x.Age.AllowResize = false);

        var hit = g.Hit(204, InputGrid.HeaderY);

        Assert.Equal(HitRegion.HeaderCell, hit.Region);
        Assert.Same(g.City, hit.Column);
    }

    [Fact]
    public void Header_FollowsHorizontalScroll()
    {
        var g = new InputGrid();
        g.Scroll.OffsetX = 100;

        var hit = g.Hit(50, InputGrid.HeaderY); // content x 150

        Assert.Same(g.Age, hit.Column);
    }

    [Fact]
    public void Header_InFrozenPanes_IgnoresHorizontalScroll()
    {
        var g = new InputGrid(configure: x =>
        {
            x.Name.IsFrozen = true;
            x.Score.IsFrozenRight = true;
            x.Style.MaxFrozenColumnsWidthRatio = 1f; // both panes pinned: 220 of the 300 px exceeds the default share
        });
        g.Scroll.OffsetX = 80;

        Assert.Same(g.Name, g.Hit(10, InputGrid.HeaderY).Column);
        Assert.Equal(HitRegion.HeaderResizeGrip, g.Hit(122, InputGrid.HeaderY).Region); // frozen column's right edge
        var right = g.Hit(230, InputGrid.HeaderY);
        Assert.Equal(HitRegion.HeaderCell, right.Region);
        Assert.Same(g.Score, right.Column);
    }

    [Fact]
    public void Header_PastTheLastColumn_IsEmptyArea()
    {
        var g = Wide();

        Assert.Equal(HitRegion.EmptyArea, g.Hit(650, InputGrid.HeaderY).Region);
    }

    [Fact]
    public void Header_OverTheDragHandle_IsEmptyArea()
    {
        var g = new InputGrid(configure: x => x.Style.ShowRowDragHandle = true);

        Assert.Equal(HitRegion.EmptyArea, g.Hit(10, InputGrid.HeaderY).Region);
        var hit = g.Hit(40, InputGrid.HeaderY); // column area starts after the 32 px handle
        Assert.Equal(HitRegion.HeaderCell, hit.Region);
        Assert.Same(g.Name, hit.Column);
    }

    // ── Frozen rows ──

    [Fact]
    public void FrozenRows_AreFixedWhileTheRestScroll()
    {
        var g = new InputGrid(configure: x => x.Source.FrozenRowCount = 2);
        g.Scroll.OffsetY = 360;

        Assert.Equal(0, g.Hit(10, 45).RowIndex);
        Assert.Equal(1, g.Hit(10, 40 + 36 + 5).RowIndex);
        Assert.Equal(12, g.Hit(10, 40 + 72 + 10).RowIndex); // (10 + 360) / 36 + 2
    }

    // ── Summaries ──

    [Fact]
    public void TopSummary_SitsBetweenHeaderAndRows()
    {
        var g = new InputGrid(configure: x => x.Source.AddTableSummaryRow(Summary(x, SummaryPosition.Top)));

        var summary = g.Hit(50, 40 + 18);
        var firstRow = g.Hit(50, 40 + 36 + 5);

        Assert.Equal(HitRegion.TableSummaryRow, summary.Region);
        Assert.Equal(HitRegion.Cell, firstRow.Region);
        Assert.Equal(0, firstRow.RowIndex);
    }

    [Fact]
    public void BottomSummary_Pinned_CoversTheViewportBottom()
    {
        var g = new InputGrid(configure: x => x.Source.AddTableSummaryRow(Summary(x, SummaryPosition.Bottom)));
        g.Scroll.OffsetY = 100;

        Assert.Equal(HitRegion.TableSummaryRow, g.Hit(50, 256 - 10).Region);
        Assert.Equal(HitRegion.Cell, g.Hit(50, 256 - 40).Region);
    }

    [Fact]
    public void BottomSummary_Unpinned_FollowsTheLastRow()
    {
        var g = new InputGrid(rows: 3, viewportHeight: 400, configure: x =>
        {
            x.Style.PinBottomSummaryRows = false;
            x.Source.AddTableSummaryRow(Summary(x, SummaryPosition.Bottom));
        });

        // Rows end at y = 40 + 3 * 36 = 148; the summary row follows.
        Assert.Equal(HitRegion.TableSummaryRow, g.Hit(50, 148 + 10).Region);
        Assert.Equal(HitRegion.Cell, g.Hit(50, 148 - 10).Region);
        Assert.Equal(HitRegion.EmptyArea, g.Hit(50, 148 + 36 + 10).Region);
    }

    private static TableSummaryRow Summary(InputGrid g, SummaryPosition position) => new("Totals", position)
    {
        Columns = { new SummaryColumnDescription { PropertyName = "Score", SummaryType = SummaryType.Sum } },
    };

    // ── Grouping ──

    private static void GroupByCity(InputGrid g, bool withSummary = false)
    {
        g.Source.AddGroupDescription(new GroupDescription("City"));
        if (withSummary)
        {
            g.Source.AddGroupSummaryRow(new GroupSummaryRow("Sub")
            {
                Columns = { new SummaryColumnDescription { PropertyName = "Score", SummaryType = SummaryType.Sum } },
            });
        }
    }

    [Fact]
    public void GroupHeader_ChevronAndRowAreDistinct()
    {
        var g = new InputGrid(rows: 4, viewportHeight: 400, configure: x => GroupByCity(x));
        float y = 40 + 40 + 18; // header + grouping panel, first row

        var chevron = g.Hit(15, y);
        var row = g.Hit(150, y);

        Assert.Equal(HitRegion.GroupChevron, chevron.Region);
        Assert.Equal(0, chevron.RowIndex);
        Assert.Equal(HitRegion.GroupHeaderRow, row.Region);
        Assert.Equal(0, row.RowIndex);
    }

    [Fact]
    public void GroupSummaryRow_IsReported()
    {
        var g = new InputGrid(rows: 4, viewportHeight: 400, configure: x => GroupByCity(x, withSummary: true));
        // Rows: 0 Oslo header, 1, 2 items, 3 summary, 4 Rome header...
        var hit = g.Hit(150, 80 + 3 * 36 + 10);

        Assert.True(g.Source.IsGroupSummaryRow(3));
        Assert.Equal(HitRegion.GroupSummaryRow, hit.Region);
        Assert.Equal(3, hit.RowIndex);
    }

    [Fact]
    public void GroupedDataRow_IsACell()
    {
        var g = new InputGrid(rows: 4, viewportHeight: 400, configure: x => GroupByCity(x));

        var hit = g.Hit(50, 80 + 36 + 10);

        Assert.Equal(HitRegion.Cell, hit.Region);
        Assert.Equal(1, hit.RowIndex);
    }

    [Fact]
    public void GroupPanel_ChipsAndRemoveButtonAreFoundAlongThePanel()
    {
        var g = new InputGrid(rows: 4, viewportHeight: 400, configure: x => GroupByCity(x));

        var regions = new List<HitRegion>();
        for (float x = 0; x < 300; x += 2)
        {
            var hit = g.Hit(x, 20);
            if (regions.Count == 0 || regions[^1] != hit.Region) regions.Add(hit.Region);
            if (hit.Region is HitRegion.GroupPanelChip or HitRegion.GroupPanelChipRemove)
                Assert.Equal(0, hit.GroupDescriptionIndex);
        }

        Assert.Equal(new[] { HitRegion.EmptyArea, HitRegion.GroupPanelChip, HitRegion.GroupPanelChipRemove, HitRegion.EmptyArea },
            regions);
    }

    [Fact]
    public void GroupPanel_IndexesEachChip()
    {
        var g = new InputGrid(rows: 4, viewportHeight: 400, configure: x =>
        {
            x.Source.AddGroupDescription(new GroupDescription("City"));
            x.Source.AddGroupDescription(new GroupDescription("Dept"));
        });

        var indexes = new SortedSet<int>();
        for (float x = 0; x < 400; x += 2)
        {
            var hit = g.Hit(x, 20);
            if (hit.Region == HitRegion.GroupPanelChip) indexes.Add(hit.GroupDescriptionIndex);
        }

        Assert.Equal(new[] { 0, 1 }, indexes);
    }

    [Fact]
    public void GroupPanel_UsesTheColumnHeaderForTheChipWidth()
    {
        var g = new InputGrid(rows: 4, viewportHeight: 400, configure: x =>
        {
            x.Source.AddGroupDescription(new GroupDescription("City", "A much longer custom caption"));
        });
        var plain = new InputGrid(rows: 4, viewportHeight: 400, configure: x => GroupByCity(x));

        static float ChipEnd(InputGrid grid)
        {
            float end = 0;
            for (float x = 0; x < 600; x += 1)
            {
                var hit = new KumikoHitTester().HitTest(x, 20, new ScrollState { ViewportWidth = 600, ViewportHeight = 400 },
                    grid.Style, grid.Source.Columns, grid.Source.RowCount, grid.Source);
                if (hit.Region is HitRegion.GroupPanelChip or HitRegion.GroupPanelChipRemove) end = x;
            }
            return end;
        }

        Assert.True(ChipEnd(g) > ChipEnd(plain));
    }

    [Fact]
    public void GroupPanel_ShiftsTheHeaderDown()
    {
        var g = new InputGrid(rows: 4, viewportHeight: 400, configure: x => GroupByCity(x));

        Assert.Equal(HitRegion.HeaderCell, g.Hit(50, 40 + 20).Region);
        Assert.Equal(HitRegion.HeaderCell, g.Hit(50, 40 + 39).Region);
    }

    [Fact]
    public void PinnedFooter_WinsOverTheGroupPanelAndHeaderInAShortViewport()
    {
        var g = new InputGrid(rows: 4, viewportHeight: 100, configure: x =>
        {
            GroupByCity(x);
            x.Source.AddTableSummaryRow(Summary(x, SummaryPosition.Bottom));
        });

        // The footer occupies y >= 64, which overlaps the header (40..80).
        Assert.Equal(HitRegion.TableSummaryRow, g.Hit(50, 70).Region);
        Assert.Equal(HitRegion.GroupPanelChip, FirstChipRegion(g));
    }

    private static HitRegion FirstChipRegion(InputGrid g)
    {
        for (float x = 0; x < 300; x += 2)
        {
            var region = g.Hit(x, 20).Region;
            if (region != HitRegion.EmptyArea) return region;
        }
        return HitRegion.None;
    }

    // ── Row drag handle ──

    [Fact]
    public void DragHandle_Left_IsTheFirstStripOfEachDataRow()
    {
        var g = new InputGrid(configure: x => x.Style.ShowRowDragHandle = true);

        var handle = g.Hit(10, g.RowY(2));
        var cell = g.Hit(40, g.RowY(2));

        Assert.Equal(HitRegion.RowDragHandle, handle.Region);
        Assert.Equal(2, handle.RowIndex);
        Assert.Equal(HitRegion.Cell, cell.Region);
        Assert.Same(g.Name, cell.Column); // columns start after the handle
    }

    [Fact]
    public void DragHandle_Right_IsTheLastStripOfEachDataRow()
    {
        var g = new InputGrid(configure: x =>
        {
            x.Style.ShowRowDragHandle = true;
            x.Style.RowDragHandlePosition = DragHandlePosition.Right;
        });

        Assert.Equal(HitRegion.RowDragHandle, g.Hit(290, g.RowY(1)).Region);
        Assert.Equal(HitRegion.Cell, g.Hit(100, g.RowY(1)).Region);
    }

    [Fact]
    public void DragHandle_IsNotOfferedOnGroupRows()
    {
        var g = new InputGrid(rows: 4, viewportHeight: 400, configure: x =>
        {
            x.Style.ShowRowDragHandle = true;
            GroupByCity(x);
        });

        Assert.Equal(HitRegion.GroupHeaderRow, g.Hit(150, 80 + 18).Region);
        Assert.Equal(HitRegion.RowDragHandle, g.Hit(10, 80 + 36 + 10).Region);
    }

    [Fact]
    public void DragHandle_Hidden_IsNotHit()
    {
        var g = new InputGrid();

        Assert.Equal(HitRegion.Cell, g.Hit(10, g.RowY(2)).Region);
    }
}

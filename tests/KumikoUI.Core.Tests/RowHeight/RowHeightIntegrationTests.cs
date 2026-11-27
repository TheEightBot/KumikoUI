using KumikoUI.Core.Data;
using KumikoUI.Core.Input;
using KumikoUI.Core.Layout;
using KumikoUI.Core.Models;
using KumikoUI.Core.Tests.IncrementalLoading;
using KumikoUI.Core.Tests.TestHelpers;
using static KumikoUI.Core.Tests.RowHeight.RowHeightGrid;

namespace KumikoUI.Core.Tests.RowHeight;

/// <summary>
/// Variable row height (#320) together with the load-more row (#318), the scroll bars (#316) and grid search (#317):
/// each places its geometry from the row layout, so rows of different heights move the load-more band, size the scroll
/// thumb and bring a search match into view exactly.
/// </summary>
public class RowHeightIntegrationTests
{
    // ── Load-more row (#318) ──

    private static LoadMoreGrid LoadMoreWithTallSecondRow(KumikoRowHeightMode mode)
    {
        var grid = new LoadMoreGrid(rows: 3, configure: g => g.Loader.Mode = KumikoLoadMoreMode.Manual);
        grid.Style.RowHeightMode = mode;
        grid.Style.RowHeightResolver = (in KumikoRowHeightContext c) => c.Row == 1 ? 80 : null;
        grid.Frame();
        Assert.Equal(LoadMoreRowKind.LoadMore, grid.Loader.RowKind);
        return grid;
    }

    [Theory]
    [InlineData(KumikoRowHeightMode.Fixed)]
    [InlineData(KumikoRowHeightMode.Auto)]
    public void TheLoadMoreRow_FollowsTheLastRowsRealBottom(KumikoRowHeightMode mode)
    {
        var grid = LoadMoreWithTallSecondRow(mode);
        float rowsHeight = 36 + 80 + 36;

        grid.Render();

        Assert.Equal(40 + rowsHeight + 36, grid.Scroll.ContentHeight);
        Assert.Equal(40 + rowsHeight, LoadMoreRowLayout.GetTop(grid.Source, grid.Style, 40, 0));
        Assert.Equal(HitRegion.LoadMoreRow, grid.Hit(100, 40 + rowsHeight + 5).Region);
        Assert.Equal(2, grid.Hit(100, 40 + rowsHeight - 5).RowIndex);
        Assert.Equal(1, grid.Hit(100, 40 + 36 + 79).RowIndex);
    }

    [Fact]
    public void TheScrollersContentHeight_MatchesTheRenderers_WithTheLoadMoreRowAndTallRows()
    {
        var grid = LoadMoreWithTallSecondRow(KumikoRowHeightMode.Auto);
        grid.Render();
        float rendered = grid.Scroll.ContentHeight;

        grid.Scroll.ContentHeight = 0;
        KumikoScroller.RefreshContentExtents(grid.Scroll, grid.Source, grid.Style);

        Assert.Equal(rendered, grid.Scroll.ContentHeight);
    }

    // ── Scroll bars (#316) ──

    [Fact]
    public void TheScrollThumb_IsSizedAndPlacedFromTheRealRowHeights()
    {
        // 20 rows of 72 px: 1,440 px of rows under a 40 px header in a 400 px viewport, so 1,080 px to scroll once
        // every row has been drawn (before that, rows not drawn yet count at the 36 px estimate).
        var h = RenderHarness.Create(rows: 20, configure: g =>
        {
            g.Style.VerticalScrollBarVisibility = KumikoScrollBarVisibility.Always;
            g.Style.RowHeightResolver = (in KumikoRowHeightContext _) => 72;
        });
        h.Render();
        var bar = ScrollBarGeometry.Compute(h.Scroll, h.Style, h.Source.Columns, h.Source).Vertical;
        Assert.Equal(h.Scroll.MaxOffsetY, bar.MaxOffset);
        Assert.True(bar.MaxOffset > 20 * 36 + 40 - 400, "the rows drawn count at their real height");

        // Scroll down a page at a time: every row is measured, and the range settles on the real extent
        for (int i = 0; i < 10; i++)
        {
            h.Scroll.OffsetY += 300;
            h.Render();
        }
        bar = ScrollBarGeometry.Compute(h.Scroll, h.Style, h.Source.Columns, h.Source).Vertical;

        Assert.Equal(1080f, bar.MaxOffset, 1);
        Assert.Equal(1080f, h.Scroll.OffsetY, 1);
        Assert.Equal(360f, bar.Page, 1);
        Assert.Equal(bar.TrackLength * 360f / 1440f, bar.ThumbLength, 1);
        Assert.Equal(bar.TrackStart + bar.TrackLength, bar.ThumbStart + bar.ThumbLength, 1);
    }

    [Fact]
    public void TheScrollBar_StartsBelowTallFrozenRows()
    {
        var h = RenderHarness.Create(rows: 30, configure: g =>
        {
            g.Style.VerticalScrollBarVisibility = KumikoScrollBarVisibility.Always;
            g.Source.FrozenRowCount = 2;
            g.Style.RowHeightResolver = (in KumikoRowHeightContext c) => c.Row < 2 ? 60 : null;
        });
        h.Render();

        var bar = ScrollBarGeometry.Compute(h.Scroll, h.Style, h.Source.Columns, h.Source).Vertical;

        Assert.Equal(40 + 120f, bar.TrackStart, 1);
        Assert.Equal(400 - 40 - 120f, bar.Page, 1);
    }

    // ── Search (#317) ──

    [Fact]
    public void FindNext_BringsATallMatchingRowFullyIntoView_AfterOneFrame()
    {
        var g = new RowHeightGrid(i => i == 200 ? 6 : 1 + i % 2, rows: 300);
        g.Items[200].Name = "Needle";
        g.Input.SearchDebounceMs = 0;
        g.Render();
        g.Source.SearchText = "Needle";

        Assert.True(g.Input.FindNext(g.Scroll, g.Selection, g.Style, g.Source));
        g.Render();

        Assert.Equal(200, g.Selection.CurrentCell.Row);
        Assert.Equal(AutoHeight(6), g.H(200));
        Assert.True(g.Y(200) >= Header - 0.5f, $"row top {g.Y(200)}");
        Assert.Equal(Height, g.Y(200) + g.H(200), 1);
    }
}

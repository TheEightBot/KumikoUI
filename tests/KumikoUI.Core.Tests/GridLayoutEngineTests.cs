using KumikoUI.Core.Layout;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Tests;

public class GridLayoutEngineTests
{
    private static List<DataGridColumn> MakeColumns(params (float width, ColumnSizeMode mode, float star)[] specs)
    {
        var cols = new List<DataGridColumn>();
        foreach (var (width, mode, star) in specs)
        {
            cols.Add(new DataGridColumn
            {
                Width = width,
                SizeMode = mode,
                StarWeight = star,
                MinWidth = 20f,
                MaxWidth = float.MaxValue,
                IsVisible = true
            });
        }
        return cols;
    }

    // ── ComputeColumnWidths ───────────────────────────────────────

    [Fact]
    public void ComputeColumnWidths_FixedColumns_NotChanged()
    {
        var engine = new GridLayoutEngine();
        var cols = MakeColumns((100f, ColumnSizeMode.Fixed, 1f), (200f, ColumnSizeMode.Fixed, 1f));
        engine.ComputeColumnWidths(cols, 600f);
        Assert.Equal(100f, cols[0].Width);
        Assert.Equal(200f, cols[1].Width);
    }

    [Fact]
    public void ComputeColumnWidths_StarColumns_FillRemainingSpace()
    {
        var engine = new GridLayoutEngine();
        // 1 fixed (100) + 1 star with equal weight → star gets remaining 500
        var cols = MakeColumns((100f, ColumnSizeMode.Fixed, 1f), (0f, ColumnSizeMode.Star, 1f));
        engine.ComputeColumnWidths(cols, 600f);
        Assert.Equal(500f, cols[1].Width, 0.01f);
    }

    [Fact]
    public void ComputeColumnWidths_TwoEqualStarColumns_SplitRemainingEvenly()
    {
        var engine = new GridLayoutEngine();
        var cols = MakeColumns((0f, ColumnSizeMode.Star, 1f), (0f, ColumnSizeMode.Star, 1f));
        engine.ComputeColumnWidths(cols, 400f);
        Assert.Equal(200f, cols[0].Width, 0.01f);
        Assert.Equal(200f, cols[1].Width, 0.01f);
    }

    [Fact]
    public void ComputeColumnWidths_WeightedStarColumns_ProportionalWidths()
    {
        var engine = new GridLayoutEngine();
        // Weight 1 and 2 → 1/3 and 2/3 of 300
        var cols = MakeColumns((0f, ColumnSizeMode.Star, 1f), (0f, ColumnSizeMode.Star, 2f));
        engine.ComputeColumnWidths(cols, 300f);
        Assert.Equal(100f, cols[0].Width, 0.01f);
        Assert.Equal(200f, cols[1].Width, 0.01f);
    }

    [Fact]
    public void ComputeColumnWidths_StarColumn_RespectsMinWidth()
    {
        var engine = new GridLayoutEngine();
        var cols = MakeColumns((500f, ColumnSizeMode.Fixed, 1f), (0f, ColumnSizeMode.Star, 1f));
        cols[1].MinWidth = 50f;
        // Viewport 200, fixed 500 → star space = max(0, 200-500) = 0 → clamped to MinWidth
        engine.ComputeColumnWidths(cols, 200f);
        Assert.True(cols[1].Width >= 50f);
    }

    [Fact]
    public void ComputeColumnWidths_StarColumn_RespectsMaxWidth()
    {
        var engine = new GridLayoutEngine();
        var col = MakeColumns((0f, ColumnSizeMode.Star, 1f));
        col[0].MaxWidth = 100f;
        engine.ComputeColumnWidths(col, 1000f);
        Assert.True(col[0].Width <= 100f);
    }

    [Fact]
    public void ComputeColumnWidths_HiddenColumns_NotIncluded()
    {
        var engine = new GridLayoutEngine();
        var cols = MakeColumns((0f, ColumnSizeMode.Star, 1f), (0f, ColumnSizeMode.Star, 1f));
        cols[1].IsVisible = false;
        engine.ComputeColumnWidths(cols, 400f);
        // All 400 goes to the visible star column
        Assert.Equal(400f, cols[0].Width, 0.01f);
    }

    // ── GetContentWidth ───────────────────────────────────────────

    [Fact]
    public void GetContentWidth_SumsVisibleColumnWidths()
    {
        var engine = new GridLayoutEngine();
        var cols = MakeColumns((100f, ColumnSizeMode.Fixed, 1f), (200f, ColumnSizeMode.Fixed, 1f));
        Assert.Equal(300f, engine.GetContentWidth(cols));
    }

    [Fact]
    public void GetContentWidth_ExcludesHiddenColumns()
    {
        var engine = new GridLayoutEngine();
        var cols = MakeColumns((100f, ColumnSizeMode.Fixed, 1f), (200f, ColumnSizeMode.Fixed, 1f));
        cols[1].IsVisible = false;
        Assert.Equal(100f, engine.GetContentWidth(cols));
    }

    // ── GetFrozenColumnWidth ──────────────────────────────────────

    [Fact]
    public void GetFrozenColumnWidth_SumsFrozenColumns()
    {
        var engine = new GridLayoutEngine();
        var cols = MakeColumns((100f, ColumnSizeMode.Fixed, 1f), (150f, ColumnSizeMode.Fixed, 1f));
        cols[0].IsFrozen = true;
        Assert.Equal(100f, engine.GetFrozenColumnWidth(cols));
    }

    [Fact]
    public void GetFrozenColumnWidth_NoFrozenColumns_ReturnsZero()
    {
        var engine = new GridLayoutEngine();
        var cols = MakeColumns((100f, ColumnSizeMode.Fixed, 1f));
        Assert.Equal(0f, engine.GetFrozenColumnWidth(cols));
    }

    // ── GetRightFrozenColumnWidth ─────────────────────────────────

    [Fact]
    public void GetRightFrozenColumnWidth_SumsRightFrozenColumns()
    {
        var engine = new GridLayoutEngine();
        var cols = MakeColumns((100f, ColumnSizeMode.Fixed, 1f), (80f, ColumnSizeMode.Fixed, 1f));
        cols[1].IsFrozenRight = true;
        Assert.Equal(80f, engine.GetRightFrozenColumnWidth(cols));
    }

    // ── GetVisibleFrozenColumns ───────────────────────────────────

    [Fact]
    public void GetVisibleFrozenColumns_ReturnsOnlyFrozenInOrder()
    {
        var engine = new GridLayoutEngine();
        var cols = MakeColumns(
            (100f, ColumnSizeMode.Fixed, 1f),
            (80f, ColumnSizeMode.Fixed, 1f),
            (120f, ColumnSizeMode.Fixed, 1f));
        cols[0].IsFrozen = true;
        cols[2].IsFrozen = true;
        var frozen = engine.GetVisibleFrozenColumns(cols);
        Assert.Equal(2, frozen.Count);
        Assert.Equal(0f, frozen[0].X);
        Assert.Equal(100f, frozen[1].X);
    }

    // ── GetContentHeight ──────────────────────────────────────────

    [Fact]
    public void GetContentHeight_IsHeaderPlusRowsTimesRowHeight()
    {
        var engine = new GridLayoutEngine();
        var style = new DataGridStyle { HeaderHeight = 40f, RowHeight = 36f };
        float height = engine.GetContentHeight(10, style);
        Assert.Equal(40f + 10 * 36f, height);
    }

    [Fact]
    public void GetFrozenRowHeight_IsRowsTimesRowHeight()
    {
        var engine = new GridLayoutEngine();
        var style = new DataGridStyle { RowHeight = 36f };
        Assert.Equal(72f, engine.GetFrozenRowHeight(2, style));
    }

    // ── GetVisibleRowRange ────────────────────────────────────────

    [Fact]
    public void GetVisibleRowRange_FirstPage_StartsFromZero()
    {
        var engine = new GridLayoutEngine();
        var scroll = new ScrollState { OffsetY = 0, ViewportHeight = 400 };
        var style = new DataGridStyle { HeaderHeight = 40f, RowHeight = 36f };
        var (first, last) = engine.GetVisibleRowRange(scroll, style, 100);
        Assert.Equal(0, first);
        Assert.True(last > 0);
    }

    [Fact]
    public void GetVisibleRowRange_ZeroRows_ReturnsInvalidRange()
    {
        var engine = new GridLayoutEngine();
        var scroll = new ScrollState { OffsetY = 0, ViewportHeight = 400 };
        var style = new DataGridStyle { HeaderHeight = 40f, RowHeight = 36f };
        var (first, last) = engine.GetVisibleRowRange(scroll, style, 0);
        Assert.True(first > last);
    }

    [Fact]
    public void GetVisibleRowRange_ScrolledDown_SkipsOffScreenRows()
    {
        var engine = new GridLayoutEngine();
        var style = new DataGridStyle { HeaderHeight = 40f, RowHeight = 36f };
        // Scroll past header + 5 full rows = 40 + 5*36 = 220px
        var scroll = new ScrollState { OffsetY = 220f, ViewportHeight = 200 };
        var (first, last) = engine.GetVisibleRowRange(scroll, style, 100);
        Assert.True(first >= 5); // Should skip the first 5 rows
    }

    // ── HitTestColumn ─────────────────────────────────────────────

    [Fact]
    public void HitTestColumn_PointInFirstColumn_ReturnsFirstColumn()
    {
        var engine = new GridLayoutEngine();
        var cols = MakeColumns((100f, ColumnSizeMode.Fixed, 1f), (200f, ColumnSizeMode.Fixed, 1f));
        var (col, idx) = engine.HitTestColumn(cols, 50f);
        Assert.NotNull(col);
        Assert.Equal(0, idx);
    }

    [Fact]
    public void HitTestColumn_PointInSecondColumn_ReturnsSecondColumn()
    {
        var engine = new GridLayoutEngine();
        var cols = MakeColumns((100f, ColumnSizeMode.Fixed, 1f), (200f, ColumnSizeMode.Fixed, 1f));
        var (col, idx) = engine.HitTestColumn(cols, 150f);
        Assert.NotNull(col);
        Assert.Equal(1, idx);
    }

    [Fact]
    public void HitTestColumn_PointBeyondAllColumns_ReturnsNull()
    {
        var engine = new GridLayoutEngine();
        var cols = MakeColumns((100f, ColumnSizeMode.Fixed, 1f));
        var (col, idx) = engine.HitTestColumn(cols, 500f);
        Assert.Null(col);
        Assert.Equal(-1, idx);
    }

    // ── HitTestRow ────────────────────────────────────────────────

    [Fact]
    public void HitTestRow_InHeader_ReturnsNegativeOne()
    {
        var engine = new GridLayoutEngine();
        var style = new DataGridStyle { HeaderHeight = 40f, RowHeight = 36f };
        Assert.Equal(-1, engine.HitTestRow(20f, style, 10));
    }

    [Fact]
    public void HitTestRow_InFirstDataRow_ReturnsZero()
    {
        var engine = new GridLayoutEngine();
        var style = new DataGridStyle { HeaderHeight = 40f, RowHeight = 36f };
        // 40 (header) + 10 (into first row) = 50
        Assert.Equal(0, engine.HitTestRow(50f, style, 10));
    }

    [Fact]
    public void HitTestRow_BeyondLastRow_ReturnsNegativeTwo()
    {
        var engine = new GridLayoutEngine();
        var style = new DataGridStyle { HeaderHeight = 40f, RowHeight = 36f };
        // beyond 5 rows (header + 5*36 = 220)
        Assert.Equal(-2, engine.HitTestRow(500f, style, 5));
    }

    // ── GetColumnX ────────────────────────────────────────────────

    [Fact]
    public void GetColumnX_FirstColumn_IsZero()
    {
        var engine = new GridLayoutEngine();
        var cols = MakeColumns((100f, ColumnSizeMode.Fixed, 1f), (150f, ColumnSizeMode.Fixed, 1f));
        Assert.Equal(0f, engine.GetColumnX(cols, 0));
    }

    [Fact]
    public void GetColumnX_SecondColumn_IsFirstColumnWidth()
    {
        var engine = new GridLayoutEngine();
        var cols = MakeColumns((100f, ColumnSizeMode.Fixed, 1f), (150f, ColumnSizeMode.Fixed, 1f));
        Assert.Equal(100f, engine.GetColumnX(cols, 1));
    }

    [Fact]
    public void GetColumnX_ThirdColumn_IsSumOfFirstTwo()
    {
        var engine = new GridLayoutEngine();
        var cols = MakeColumns(
            (100f, ColumnSizeMode.Fixed, 1f),
            (150f, ColumnSizeMode.Fixed, 1f),
            (80f, ColumnSizeMode.Fixed, 1f));
        Assert.Equal(250f, engine.GetColumnX(cols, 2));
    }

    // ── GetVisibleScrollableColumns ───────────────────────────────

    [Fact]
    public void GetVisibleScrollableColumns_NoScroll_ReturnsAllScrollableColumns()
    {
        var engine = new GridLayoutEngine();
        var cols = MakeColumns(
            (100f, ColumnSizeMode.Fixed, 1f),
            (100f, ColumnSizeMode.Fixed, 1f),
            (100f, ColumnSizeMode.Fixed, 1f));
        var scroll = new ScrollState { OffsetX = 0, ViewportWidth = 500 };
        var visible = engine.GetVisibleScrollableColumns(cols, scroll, 0f);
        Assert.Equal(3, visible.Count);
    }

    [Fact]
    public void GetVisibleScrollableColumns_Scrolled_OmitsOffscreenColumns()
    {
        var engine = new GridLayoutEngine();
        // 5 columns each 100px wide
        var cols = Enumerable.Repeat(0, 5)
            .Select(_ => new DataGridColumn { Width = 100f, IsVisible = true, SizeMode = ColumnSizeMode.Fixed, MinWidth = 20f })
            .ToList();
        // scroll past first 3
        var scroll = new ScrollState { OffsetX = 300, ViewportWidth = 120 };
        var visible = engine.GetVisibleScrollableColumns(cols, scroll, 0f);
        // Columns 3 and 4 (partially or fully visible)
        Assert.All(visible, item => Assert.True(item.X >= 250f));
    }
}



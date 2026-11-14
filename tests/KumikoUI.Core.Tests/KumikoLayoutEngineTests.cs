using KumikoUI.Core.Layout;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Tests;

public class KumikoLayoutEngineTests
{
    private static List<KumikoColumn> MakeColumns(params (float width, ColumnSizeMode mode, float star)[] specs)
    {
        var cols = new List<KumikoColumn>();
        foreach (var (width, mode, star) in specs)
        {
            cols.Add(new KumikoColumn
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
        var engine = new KumikoLayoutEngine();
        var cols = MakeColumns((100f, ColumnSizeMode.Fixed, 1f), (200f, ColumnSizeMode.Fixed, 1f));
        engine.ComputeColumnWidths(cols, 600f);
        Assert.Equal(100f, cols[0].Width);
        Assert.Equal(200f, cols[1].Width);
    }

    [Fact]
    public void ComputeColumnWidths_StarColumns_FillRemainingSpace()
    {
        var engine = new KumikoLayoutEngine();
        // 1 fixed (100) + 1 star with equal weight → star gets remaining 500
        var cols = MakeColumns((100f, ColumnSizeMode.Fixed, 1f), (0f, ColumnSizeMode.Star, 1f));
        engine.ComputeColumnWidths(cols, 600f);
        Assert.Equal(500f, cols[1].Width, 0.01f);
    }

    [Fact]
    public void ComputeColumnWidths_TwoEqualStarColumns_SplitRemainingEvenly()
    {
        var engine = new KumikoLayoutEngine();
        var cols = MakeColumns((0f, ColumnSizeMode.Star, 1f), (0f, ColumnSizeMode.Star, 1f));
        engine.ComputeColumnWidths(cols, 400f);
        Assert.Equal(200f, cols[0].Width, 0.01f);
        Assert.Equal(200f, cols[1].Width, 0.01f);
    }

    [Fact]
    public void ComputeColumnWidths_WeightedStarColumns_ProportionalWidths()
    {
        var engine = new KumikoLayoutEngine();
        // Weight 1 and 2 → 1/3 and 2/3 of 300
        var cols = MakeColumns((0f, ColumnSizeMode.Star, 1f), (0f, ColumnSizeMode.Star, 2f));
        engine.ComputeColumnWidths(cols, 300f);
        Assert.Equal(100f, cols[0].Width, 0.01f);
        Assert.Equal(200f, cols[1].Width, 0.01f);
    }

    [Fact]
    public void ComputeColumnWidths_StarColumn_RespectsMinWidth()
    {
        var engine = new KumikoLayoutEngine();
        var cols = MakeColumns((500f, ColumnSizeMode.Fixed, 1f), (0f, ColumnSizeMode.Star, 1f));
        cols[1].MinWidth = 50f;
        // Viewport 200, fixed 500 → star space = max(0, 200-500) = 0 → clamped to MinWidth
        engine.ComputeColumnWidths(cols, 200f);
        Assert.True(cols[1].Width >= 50f);
    }

    [Fact]
    public void ComputeColumnWidths_StarColumn_RespectsMaxWidth()
    {
        var engine = new KumikoLayoutEngine();
        var col = MakeColumns((0f, ColumnSizeMode.Star, 1f));
        col[0].MaxWidth = 100f;
        engine.ComputeColumnWidths(col, 1000f);
        Assert.True(col[0].Width <= 100f);
    }

    [Fact]
    public void ComputeColumnWidths_HiddenColumns_NotIncluded()
    {
        var engine = new KumikoLayoutEngine();
        var cols = MakeColumns((0f, ColumnSizeMode.Star, 1f), (0f, ColumnSizeMode.Star, 1f));
        cols[1].IsVisible = false;
        engine.ComputeColumnWidths(cols, 400f);
        // All 400 goes to the visible star column
        Assert.Equal(400f, cols[0].Width, 0.01f);
    }

    [Fact]
    public void ComputeColumnWidths_StarColumnHeldAtItsMaxWidth_LeavesTheRestToTheOthers()
    {
        var engine = new KumikoLayoutEngine();
        var cols = MakeColumns((0f, ColumnSizeMode.Star, 1f), (0f, ColumnSizeMode.Star, 1f));
        cols[0].MaxWidth = 100f;

        engine.ComputeColumnWidths(cols, 400f);

        Assert.Equal(100f, cols[0].Width, 0.01f);
        Assert.Equal(300f, cols[1].Width, 0.01f);
    }

    [Fact]
    public void ComputeColumnWidths_StarColumnHeldAtItsMinWidth_TakesFromTheOthers()
    {
        var engine = new KumikoLayoutEngine();
        var cols = MakeColumns((0f, ColumnSizeMode.Star, 1f), (0f, ColumnSizeMode.Star, 1f), (0f, ColumnSizeMode.Star, 1f));
        cols[0].MinWidth = 200f;

        engine.ComputeColumnWidths(cols, 300f);

        Assert.Equal(200f, cols[0].Width, 0.01f);
        Assert.Equal(50f, cols[1].Width, 0.01f);
        Assert.Equal(50f, cols[2].Width, 0.01f);
    }

    [Fact]
    public void ComputeColumnWidths_MinWidthAboveMaxWidth_UsesTheMinimumWithoutThrowing()
    {
        var engine = new KumikoLayoutEngine();
        var cols = MakeColumns((0f, ColumnSizeMode.Star, 1f));
        cols[0].MinWidth = 150f;
        cols[0].MaxWidth = 100f;

        engine.ComputeColumnWidths(cols, 400f);

        Assert.Equal(150f, cols[0].Width, 0.01f);
    }

    [Fact]
    public void ComputeColumnWidths_DoesNotMakeLaidOutWidthsExplicit()
    {
        var engine = new KumikoLayoutEngine();
        var star = new KumikoColumn { SizeMode = ColumnSizeMode.Star };
        var fixedDefault = new KumikoColumn();

        engine.ComputeColumnWidths(new[] { star, fixedDefault }, 500f);

        Assert.Equal(380f, star.Width, 0.01f);
        Assert.False(star.HasExplicitWidth);
        Assert.False(fixedDefault.HasExplicitWidth);
    }

    [Fact]
    public void ComputeColumnWidths_WithAMeasuringContext_FitsHeadersOfColumnsWithoutAWidth()
    {
        var engine = new KumikoLayoutEngine();
        var style = new KumikoStyle();
        var fitted = new KumikoColumn { Header = "A rather long column header", PropertyName = "X" };
        var explicitWidth = new KumikoColumn { Header = "A rather long column header", PropertyName = "X", Width = 90 };

        engine.ComputeColumnWidths(new[] { fitted, explicitWidth }, 500f, new TestHelpers.NoOpDrawingContext(), style);

        // 27 characters at 7 px, 8 px padding on each side, sort arrow 12, filter icon 14, margin 4.
        float fit = MathF.Ceiling(27 * 7f) + 16 + 12 + 14 + 4;
        Assert.Equal(fit, fitted.HeaderFitWidth);
        Assert.Equal(fit, fitted.Width);
        Assert.Equal(fit, explicitWidth.HeaderFitWidth);
        Assert.Equal(90f, explicitWidth.Width);
        Assert.Equal(fit, KumikoLayoutEngine.EffectiveMinWidth(fitted));
        Assert.Equal(explicitWidth.MinWidth, KumikoLayoutEngine.EffectiveMinWidth(explicitWidth));
    }

    [Fact]
    public void CalculateAutoFitWidth_IsNeverNarrowerThanTheHeaderFit()
    {
        var engine = new KumikoLayoutEngine();
        var source = new KumikoSource();
        var column = new KumikoColumn { Header = "A rather long column header", PropertyName = "Length" };
        source.SetColumns(new[] { column });
        source.SetItems(new[] { "a", "b" });

        float width = engine.CalculateAutoFitWidth(column, source, new TestHelpers.NoOpDrawingContext(), new KumikoStyle());

        Assert.Equal(MathF.Ceiling(27 * 7f) + 16 + 12 + 14 + 4, width);
    }

    // ── GetContentWidth ───────────────────────────────────────────

    [Fact]
    public void GetContentWidth_SumsVisibleColumnWidths()
    {
        var engine = new KumikoLayoutEngine();
        var cols = MakeColumns((100f, ColumnSizeMode.Fixed, 1f), (200f, ColumnSizeMode.Fixed, 1f));
        Assert.Equal(300f, engine.GetContentWidth(cols));
    }

    [Fact]
    public void GetContentWidth_ExcludesHiddenColumns()
    {
        var engine = new KumikoLayoutEngine();
        var cols = MakeColumns((100f, ColumnSizeMode.Fixed, 1f), (200f, ColumnSizeMode.Fixed, 1f));
        cols[1].IsVisible = false;
        Assert.Equal(100f, engine.GetContentWidth(cols));
    }

    // ── GetFrozenColumnWidth ──────────────────────────────────────

    [Fact]
    public void GetFrozenColumnWidth_SumsFrozenColumns()
    {
        var engine = new KumikoLayoutEngine();
        var cols = MakeColumns((100f, ColumnSizeMode.Fixed, 1f), (150f, ColumnSizeMode.Fixed, 1f));
        cols[0].IsFrozen = true;
        Assert.Equal(100f, engine.GetFrozenColumnWidth(cols));
    }

    [Fact]
    public void GetFrozenColumnWidth_NoFrozenColumns_ReturnsZero()
    {
        var engine = new KumikoLayoutEngine();
        var cols = MakeColumns((100f, ColumnSizeMode.Fixed, 1f));
        Assert.Equal(0f, engine.GetFrozenColumnWidth(cols));
    }

    // ── GetRightFrozenColumnWidth ─────────────────────────────────

    [Fact]
    public void GetRightFrozenColumnWidth_SumsRightFrozenColumns()
    {
        var engine = new KumikoLayoutEngine();
        var cols = MakeColumns((100f, ColumnSizeMode.Fixed, 1f), (80f, ColumnSizeMode.Fixed, 1f));
        cols[1].IsFrozenRight = true;
        Assert.Equal(80f, engine.GetRightFrozenColumnWidth(cols));
    }

    // ── GetVisibleFrozenColumns ───────────────────────────────────

    [Fact]
    public void GetVisibleFrozenColumns_ReturnsOnlyFrozenInOrder()
    {
        var engine = new KumikoLayoutEngine();
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
        var engine = new KumikoLayoutEngine();
        var style = new KumikoStyle { HeaderHeight = 40f, RowHeight = 36f };
        float height = engine.GetContentHeight(10, style);
        Assert.Equal(40f + 10 * 36f, height);
    }

    [Fact]
    public void GetFrozenRowHeight_IsRowsTimesRowHeight()
    {
        var engine = new KumikoLayoutEngine();
        var style = new KumikoStyle { RowHeight = 36f };
        Assert.Equal(72f, engine.GetFrozenRowHeight(2, style));
    }

    // ── GetVisibleRowRange ────────────────────────────────────────

    [Fact]
    public void GetVisibleRowRange_FirstPage_StartsFromZero()
    {
        var engine = new KumikoLayoutEngine();
        var scroll = new ScrollState { OffsetY = 0, ViewportHeight = 400 };
        var style = new KumikoStyle { HeaderHeight = 40f, RowHeight = 36f };
        var (first, last) = engine.GetVisibleRowRangeWithOffset(scroll, style, 100, 0);
        Assert.Equal(0, first);
        Assert.True(last > 0);
    }

    [Fact]
    public void GetVisibleRowRange_ZeroRows_ReturnsInvalidRange()
    {
        var engine = new KumikoLayoutEngine();
        var scroll = new ScrollState { OffsetY = 0, ViewportHeight = 400 };
        var style = new KumikoStyle { HeaderHeight = 40f, RowHeight = 36f };
        var (first, last) = engine.GetVisibleRowRangeWithOffset(scroll, style, 0, 0);
        Assert.True(first > last);
    }

    [Fact]
    public void GetVisibleRowRange_ScrolledDown_SkipsOffScreenRows()
    {
        var engine = new KumikoLayoutEngine();
        var style = new KumikoStyle { HeaderHeight = 40f, RowHeight = 36f };
        // Scroll past header + 5 full rows = 40 + 5*36 = 220px
        var scroll = new ScrollState { OffsetY = 220f, ViewportHeight = 200 };
        var (first, last) = engine.GetVisibleRowRangeWithOffset(scroll, style, 100, 0);
        Assert.True(first >= 5); // Should skip the first 5 rows
    }

    // ── GetVisibleScrollableColumns ───────────────────────────────

    [Fact]
    public void GetVisibleScrollableColumns_NoScroll_ReturnsAllScrollableColumns()
    {
        var engine = new KumikoLayoutEngine();
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
        var engine = new KumikoLayoutEngine();
        // 5 columns each 100px wide
        var cols = Enumerable.Repeat(0, 5)
            .Select(_ => new KumikoColumn { Width = 100f, IsVisible = true, SizeMode = ColumnSizeMode.Fixed, MinWidth = 20f })
            .ToList();
        // scroll past first 3
        var scroll = new ScrollState { OffsetX = 300, ViewportWidth = 120 };
        var visible = engine.GetVisibleScrollableColumns(cols, scroll, 0f);
        // Columns 3 and 4 (partially or fully visible)
        Assert.All(visible, item => Assert.True(item.X >= 250f));
    }
}



using KumikoUI.Core.Layout;
using KumikoUI.Core.Models;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests;

/// <summary>
/// Layout edge cases the basic tests leave out: the visible row range under a top summary and frozen rows,
/// right-frozen and scrollable column placement, star sizing at its limits, hit testing around hidden
/// columns and boundaries, and auto-fit measuring.
/// </summary>
public class KumikoLayoutEngineEdgeTests
{
    private static readonly KumikoLayoutEngine Engine = new();
    private static readonly KumikoStyle Style = new() { HeaderHeight = 40f, RowHeight = 36f, CellPadding = 8f };

    private static KumikoColumn Fixed(float width, string header = "") => new()
    {
        Header = header,
        PropertyName = header,
        Width = width,
        SizeMode = ColumnSizeMode.Fixed,
        MinWidth = 20f,
    };

    private static KumikoColumn Star(float weight = 1f, float min = 20f, float max = float.MaxValue) => new()
    {
        Width = 0f,
        SizeMode = ColumnSizeMode.Star,
        StarWeight = weight,
        MinWidth = min,
        MaxWidth = max,
    };

    private static ScrollState Scroll(float y = 0, float height = 400, float x = 0, float width = 400) =>
        new() { ViewportHeight = height, ViewportWidth = width, OffsetY = y, OffsetX = x };

    // ── GetVisibleRowRangeWithOffset ──────────────────────────────

    [Theory]
    [InlineData(0f, 0f, 0, 0, 10)]     // header 40: the row starting at 400px is the last one touched
    [InlineData(0f, 30f, 0, 0, 9)]     // a 30px top summary pushes the data area down
    [InlineData(0f, 30f, 2, 2, 9)]     // two frozen rows are drawn separately: the range starts after them
    [InlineData(360f, 0f, 0, 10, 20)]  // scrolled 10 rows: row 10 is the first one below the header (#117)
    [InlineData(-50f, 0f, 0, 0, 8)]    // overscroll above the top never yields a negative first row
    public void VisibleRowRange_AccountsForTheTopOffsetAndFrozenRows(float scrollY, float topOffset, int frozen, int first, int last)
    {
        var range = Engine.GetVisibleRowRangeWithOffset(Scroll(scrollY), Style, 100, topOffset, frozen);

        Assert.Equal((first, last), range);
    }

    [Fact]
    public void VisibleRowRange_ClampsTheLastRowToTheRowCount()
    {
        Assert.Equal((0, 4), Engine.GetVisibleRowRange(Scroll(), Style, 5));
    }

    [Fact]
    public void VisibleRowRange_WithFrozenRows_ClampsAfterTheFrozenOffset()
    {
        Assert.Equal((1, 4), Engine.GetVisibleRowRangeWithOffset(Scroll(), Style, 5, 0, 1));
    }

    [Theory]
    [InlineData(3, 3)]
    [InlineData(3, 10)]
    public void VisibleRowRange_WhenEveryRowIsFrozen_IsEmpty(int rows, int frozen)
    {
        Assert.Equal((0, -1), Engine.GetVisibleRowRangeWithOffset(Scroll(), Style, rows, 0, frozen));
    }

    [Fact]
    public void VisibleRowRange_WithAZeroHeightViewport_StillReturnsAValidRange()
    {
        var (first, last) = Engine.GetVisibleRowRange(Scroll(height: 0), Style, 10);

        Assert.InRange(first, 0, 9);
        Assert.InRange(last, first - 1, 9);
    }

    [Fact]
    public void VisibleRowRange_ScrolledPastTheEnd_ClampsToTheLastRow()
    {
        var (first, last) = Engine.GetVisibleRowRange(Scroll(y: 100000), Style, 20);

        Assert.Equal(19, last);
        Assert.True(first >= last);
    }

    // ── Frozen columns ────────────────────────────────────────────

    [Fact]
    public void RightFrozenColumns_SitAtTheRightEdge_InColumnOrder()
    {
        var cols = new List<KumikoColumn> { Fixed(100), Fixed(80), Fixed(60) };
        cols[1].IsFrozenRight = true;
        cols[2].IsFrozenRight = true;

        var result = Engine.GetVisibleRightFrozenColumns(cols, 500f);

        Assert.Equal(new[] { 1, 2 }, result.Select(r => r.OriginalIndex));
        Assert.Equal(new[] { 360f, 440f }, result.Select(r => r.X));
    }

    [Fact]
    public void RightFrozenColumns_SkipHiddenColumns_WhenPlacing()
    {
        var cols = new List<KumikoColumn> { Fixed(100), Fixed(80), Fixed(60) };
        cols[1].IsFrozenRight = true;
        cols[1].IsVisible = false;
        cols[2].IsFrozenRight = true;

        var result = Engine.GetVisibleRightFrozenColumns(cols, 500f);

        Assert.Equal((cols[2], 440f, 2), result.Single());
    }

    [Fact]
    public void PopulateVisibleRightFrozenColumns_ClearsTheListItIsGiven()
    {
        var cols = new List<KumikoColumn> { Fixed(100) };
        cols[0].IsFrozenRight = true;
        var list = new List<(KumikoColumn Column, float X, int OriginalIndex)> { (cols[0], 1f, 9), (cols[0], 2f, 9) };

        Engine.PopulateVisibleRightFrozenColumns(cols, 300f, list);

        Assert.Equal(new[] { 200f }, list.Select(r => r.X));
    }

    [Fact]
    public void PopulateVisibleFrozenColumns_StartsAtZero_AndSkipsHiddenOnes()
    {
        var cols = new List<KumikoColumn> { Fixed(100), Fixed(50), Fixed(70) };
        cols[0].IsFrozen = true;
        cols[1].IsFrozen = true;
        cols[1].IsVisible = false;
        cols[2].IsFrozen = true;
        var list = new List<(KumikoColumn Column, float X, int OriginalIndex)>();

        Engine.PopulateVisibleFrozenColumns(cols, list);

        Assert.Equal(new[] { 0f, 100f }, list.Select(r => r.X));
        Assert.Equal(new[] { 0, 2 }, list.Select(r => r.OriginalIndex));
    }

    [Fact]
    public void FrozenWidths_IgnoreHiddenColumns()
    {
        var cols = new List<KumikoColumn> { Fixed(100), Fixed(50), Fixed(70) };
        cols[0].IsFrozen = true;
        cols[1].IsFrozen = true;
        cols[1].IsVisible = false;
        cols[2].IsFrozenRight = true;
        cols[2].IsVisible = false;

        Assert.Equal(100f, Engine.GetFrozenColumnWidth(cols));
        Assert.Equal(0f, Engine.GetRightFrozenColumnWidth(cols));
    }

    // ── Scrollable columns ────────────────────────────────────────

    private static List<KumikoColumn> FrozenLayout()
    {
        var cols = new List<KumikoColumn> { Fixed(100), Fixed(100), Fixed(100), Fixed(100), Fixed(80) };
        cols[0].IsFrozen = true;
        cols[4].IsFrozenRight = true;
        return cols;
    }

    [Fact]
    public void ScrollableColumns_FitBetweenTheFrozenPanes_WhenNotScrolled()
    {
        var cols = FrozenLayout();

        var result = Engine.GetVisibleScrollableColumns(cols, Scroll(), 100f, 80f);

        Assert.Equal(new[] { 1, 2, 3 }, result.Select(r => r.OriginalIndex));
        Assert.Equal(new[] { 100f, 200f, 300f }, result.Select(r => r.X)); // content coordinates
    }

    [Fact]
    public void ScrollableColumns_DropTheOnesScrolledOutOfView()
    {
        var cols = FrozenLayout();

        var result = Engine.GetVisibleScrollableColumns(cols, Scroll(x: 150, width: 400), 100f, 80f);

        Assert.Equal(new[] { 2, 3 }, result.Select(r => r.OriginalIndex));
    }

    [Fact]
    public void ScrollableColumns_ExcludeFrozenAndHiddenColumns()
    {
        var cols = FrozenLayout();
        cols[2].IsVisible = false;

        var result = Engine.GetVisibleScrollableColumns(cols, Scroll(width: 1000), 100f, 80f);

        Assert.Equal(new[] { 1, 3 }, result.Select(r => r.OriginalIndex));
        Assert.Equal(new[] { 100f, 200f }, result.Select(r => r.X));
    }

    [Fact]
    public void GetVisibleColumns_IncludesPartiallyVisibleColumns_AndSkipsHiddenOnes()
    {
        var cols = new List<KumikoColumn> { Fixed(100), Fixed(100), Fixed(100), Fixed(100) };
        cols[1].IsVisible = false;

        var result = Engine.GetVisibleColumns(cols, Scroll(x: 50, width: 120));

        // Visible columns sit at x = 0, 100, 200; the viewport covers 50..170.
        Assert.Equal(new[] { 0f, 100f }, result.Select(r => r.X));
        Assert.Equal(new[] { cols[0], cols[2] }, result.Select(r => r.Column));
    }

    // ── ComputeColumnWidths ───────────────────────────────────────

    [Fact]
    public void ComputeColumnWidths_WithAZeroViewport_GivesEveryStarItsMinimum()
    {
        var cols = new List<KumikoColumn> { Star(1, min: 30), Star(3, min: 50) };

        Engine.ComputeColumnWidths(cols, 0f);

        Assert.Equal(new[] { 30f, 50f }, cols.Select(c => c.Width));
    }

    [Fact]
    public void ComputeColumnWidths_WhenFixedColumnsFillTheViewport_StarsTakeTheirMinimum()
    {
        var cols = new List<KumikoColumn> { Fixed(300), Fixed(300), Star(1, min: 40) };

        Engine.ComputeColumnWidths(cols, 500f);

        Assert.Equal(40f, cols[2].Width);
        Assert.Equal(300f, cols[0].Width);
    }

    [Fact]
    public void ComputeColumnWidths_UnmeasuredAutoColumnsKeepTheirWidth_AndCountAsFixed()
    {
        var auto = Fixed(50);
        auto.SizeMode = ColumnSizeMode.Auto;
        var cols = new List<KumikoColumn> { Fixed(100), auto, Star() };

        Engine.ComputeColumnWidths(cols, 600f);

        Assert.Equal(50f, auto.Width);
        Assert.Equal(450f, cols[2].Width, 0.01f);
    }

    [Fact]
    public void ComputeColumnWidths_HiddenStarColumns_KeepTheirWidth_AndTakeNoShare()
    {
        var hidden = Star();
        hidden.Width = 77f;
        hidden.IsVisible = false;
        var cols = new List<KumikoColumn> { hidden, Star() };

        Engine.ComputeColumnWidths(cols, 300f);

        Assert.Equal(77f, hidden.Width);
        Assert.Equal(300f, cols[1].Width, 0.01f);
    }

    [Fact]
    public void ComputeColumnWidths_WithZeroWeights_LeavesStarWidthsAlone()
    {
        var a = Star(0);
        a.Width = 55f;
        var cols = new List<KumikoColumn> { a };

        Engine.ComputeColumnWidths(cols, 300f);

        Assert.Equal(55f, a.Width);
    }

    [Fact]
    public void ComputeColumnWidths_WithNoColumns_DoesNotThrow()
    {
        Engine.ComputeColumnWidths(new List<KumikoColumn>(), 300f);
        Assert.Equal(0f, Engine.GetContentWidth(new List<KumikoColumn>()));
    }

    [Fact]
    public void ComputeColumnWidths_IsRepeatable_AndFollowsTheViewport()
    {
        var cols = new List<KumikoColumn> { Fixed(100), Star(1), Star(1) };

        Engine.ComputeColumnWidths(cols, 500f);
        Engine.ComputeColumnWidths(cols, 500f);
        Assert.Equal(new[] { 100f, 200f, 200f }, cols.Select(c => c.Width));

        Engine.ComputeColumnWidths(cols, 300f);
        Assert.Equal(new[] { 100f, 100f, 100f }, cols.Select(c => c.Width));
    }

    [Fact]
    public void ComputeColumnWidths_NeverLeavesAStarOutsideItsMinAndMax()
    {
        var cols = new List<KumikoColumn> { Star(1, min: 150, max: 160), Star(1, min: 10, max: 60), Star(2) };

        Engine.ComputeColumnWidths(cols, 400f);

        Assert.InRange(cols[0].Width, 150f, 160f);
        Assert.InRange(cols[1].Width, 10f, 60f);
    }

    [Fact]
    public void ComputeColumnWidths_WithMinimumsAboveTheViewport_LetsTheContentOverflow()
    {
        var cols = new List<KumikoColumn> { Star(1, min: 80), Star(1, min: 80) };

        Engine.ComputeColumnWidths(cols, 100f);

        Assert.Equal(160f, Engine.GetContentWidth(cols)); // wider than the 100px viewport: it scrolls
    }

    [Fact]
    public void ComputeColumnWidths_RedistributesSpaceAMaxWidthClampFreesUp()
    {
        var cols = new List<KumikoColumn> { Star(1, max: 100), Star(1) };

        Engine.ComputeColumnWidths(cols, 500f);

        Assert.Equal(100f, cols[0].Width, 0.01f);
        Assert.Equal(400f, cols[1].Width, 0.01f);
    }

    [Fact]
    public void ComputeColumnWidths_RedistributesTheSpaceAMinWidthClampTakes()
    {
        var cols = new List<KumikoColumn> { Star(1, min: 300), Star(1) };

        Engine.ComputeColumnWidths(cols, 400f);

        Assert.Equal(400f, Engine.GetContentWidth(cols), 0.01f);
    }

    // ── Hit testing ───────────────────────────────────────────────

    [Fact]
    public void HitTestColumn_AtAColumnBoundary_BelongsToTheColumnThatStartsThere()
    {
        var cols = new List<KumikoColumn> { Fixed(100), Fixed(100) };

        Assert.Equal(0, Engine.HitTestColumn(cols, 99.9f).Index);
        Assert.Equal(1, Engine.HitTestColumn(cols, 100f).Index);
        Assert.Equal(-1, Engine.HitTestColumn(cols, 200f).Index);
    }

    [Fact]
    public void HitTestColumn_SkipsHiddenColumns_ButReportsTheOriginalIndex()
    {
        var cols = new List<KumikoColumn> { Fixed(100), Fixed(100), Fixed(100) };
        cols[0].IsVisible = false;

        var (column, index) = Engine.HitTestColumn(cols, 10f);

        Assert.Same(cols[1], column);
        Assert.Equal(1, index);
    }

    [Fact]
    public void HitTestColumn_LeftOfTheContent_AndWithNoColumns_HitsNothing()
    {
        Assert.Null(Engine.HitTestColumn(new List<KumikoColumn> { Fixed(100) }, -1f).Column);
        Assert.Null(Engine.HitTestColumn(new List<KumikoColumn>(), 5f).Column);
    }

    [Fact]
    public void HitTestRow_AtTheHeaderEdge_IsTheFirstRow_AndAtTheLastRowsEnd_IsOutOfBounds()
    {
        Assert.Equal(-1, Engine.HitTestRow(39.9f, Style, 3));
        Assert.Equal(0, Engine.HitTestRow(40f, Style, 3));
        Assert.Equal(2, Engine.HitTestRow(40f + 36f * 3 - 0.1f, Style, 3));
        Assert.Equal(-2, Engine.HitTestRow(40f + 36f * 3, Style, 3));
    }

    [Fact]
    public void HitTestRow_WithNoRows_IsEitherTheHeaderOrOutOfBounds()
    {
        Assert.Equal(-1, Engine.HitTestRow(10f, Style, 0));
        Assert.Equal(-2, Engine.HitTestRow(50f, Style, 0));
    }

    [Fact]
    public void GetColumnX_SumsOnlyTheVisibleColumnsBefore_AndClampsAnIndexPastTheEnd()
    {
        var cols = new List<KumikoColumn> { Fixed(100), Fixed(50), Fixed(70) };
        cols[0].IsVisible = false;

        Assert.Equal(0f, Engine.GetColumnX(cols, 1));
        Assert.Equal(50f, Engine.GetColumnX(cols, 2));
        Assert.Equal(120f, Engine.GetColumnX(cols, 9));
    }

    // ── Content sizes ─────────────────────────────────────────────

    [Fact]
    public void ContentHeight_OfNoRows_IsJustTheHeader()
    {
        Assert.Equal(40f, Engine.GetContentHeight(0, Style));
        Assert.Equal(0f, Engine.GetFrozenRowHeight(0, Style));
    }

    // ── Auto-fit ──────────────────────────────────────────────────

    private sealed class Person2
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
    }

    private static (KumikoSource Source, KumikoColumn Column) NameColumn(params string[] names)
    {
        var column = new KumikoColumn { PropertyName = "Name", Header = "Name", MinWidth = 40f, MaxWidth = 500f };
        var source = new KumikoSource();
        source.SetColumns(new[] { column });
        source.SetItems(names.Select(n => new Person2 { Name = n }).ToList());
        return (source, column);
    }

    [Fact]
    public void AutoFit_IsTheWidestTextPlusPaddingOnBothSidesAndTheSortGlyphAllowance()
    {
        var (source, column) = NameColumn("Al", "Alexander", "Bob");

        float width = Engine.CalculateAutoFitWidth(column, source, new RecordingDrawingContext(), Style);

        // "Alexander" is 9 characters at 8px, plus 2 * 8 padding plus 16.
        Assert.Equal(9 * RecordingDrawingContext.CharWidth + 8 * 2 + 16, width);
    }

    [Fact]
    public void AutoFit_UsesTheHeader_WhenItIsWiderThanEveryCell()
    {
        var (source, column) = NameColumn("a", "b");
        column.Header = "A long column header";

        var ctx = new RecordingDrawingContext();
        float width = Engine.CalculateAutoFitWidth(column, source, ctx, Style);

        // Never narrower than the header with its sort and filter space (#182)
        Assert.Equal(Engine.MeasureHeaderFitWidth(column, ctx, Style), width);
    }

    [Fact]
    public void AutoFit_OfAnEmptySource_MeasuresTheHeaderOnly()
    {
        var (source, column) = NameColumn();

        var ctx = new RecordingDrawingContext();
        float width = Engine.CalculateAutoFitWidth(column, source, ctx, Style);

        Assert.Equal(Engine.MeasureHeaderFitWidth(column, ctx, Style), width);
    }

    [Fact]
    public void AutoFit_ClampsToTheColumnsMinAndMax()
    {
        var (source, column) = NameColumn("Alexander");

        column.MaxWidth = 60f;
        Assert.Equal(60f, Engine.CalculateAutoFitWidth(column, source, new RecordingDrawingContext(), Style));

        column.MinWidth = 200f;
        column.MaxWidth = 500f;
        Assert.Equal(200f, Engine.CalculateAutoFitWidth(column, source, new RecordingDrawingContext(), Style));
    }

    [Fact]
    public void AutoFit_MeasuresTheFormattedText_NotTheRawValue()
    {
        var age = new KumikoColumn { PropertyName = "Age", Header = "A", Format = "000000", MinWidth = 10f };
        var source = new KumikoSource();
        source.SetColumns(new[] { age });
        source.SetItems(new List<Person2> { new() { Age = 7 } });

        float width = Engine.CalculateAutoFitWidth(age, source, new RecordingDrawingContext(), Style);

        Assert.Equal(6 * RecordingDrawingContext.CharWidth + 32, width); // "000007"
    }

    [Fact]
    public void AutoFit_IgnoresGroupHeaderRows()
    {
        var (source, column) = NameColumn("a", "b");
        source.AddGroupDescription(new GroupDescription("Name") { DisplayTextSelector = _ => "a very long group header text" });

        var ctx = new RecordingDrawingContext();
        float width = Engine.CalculateAutoFitWidth(column, source, ctx, Style);

        Assert.Equal(Engine.MeasureHeaderFitWidth(column, ctx, Style), width);
    }

    [Fact]
    public void AutoFit_SkipsEmptyAndNullCells()
    {
        var (source, column) = NameColumn("", "xy");

        var ctx = new RecordingDrawingContext();
        float width = Engine.CalculateAutoFitWidth(column, source, ctx, Style);

        Assert.Equal(Engine.MeasureHeaderFitWidth(column, ctx, Style), width); // the header (with its glyph space) is wider than "xy"
    }
}

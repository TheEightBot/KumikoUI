using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;
using static KumikoUI.Core.Tests.TestHelpers.RenderHarness;

namespace KumikoUI.Core.Tests.Rendering;

/// <summary>
/// <see cref="DataGridRenderer"/>'s three column passes (scrollable, left-frozen, right-frozen),
/// frozen rows and the row drag-handle column: what each pass draws, where, and what its clip lets
/// reach the screen.
/// </summary>
public class DataGridRendererPaneTests
{
    private static readonly string[] ScrollableCellTexts = { "20", "21", "22", "23", "24", "Oslo", "Rome" };

    // ── Left-frozen columns ───────────────────────────────────────

    /// <summary>Name frozen left (0–150); 300 px viewport scrolled 50 px: Age at 100–200, City at 200–350.</summary>
    private static RenderHarness LeftFrozen()
    {
        var h = Create(width: 300);
        h.Column("Name").FreezeMode = ColumnFreezeMode.Left;
        h.Scroll.OffsetX = 50;
        return h;
    }

    [Fact]
    public void LeftFrozen_CellsStayPut_InsideTheFrozenClip()
    {
        var h = LeftFrozen();

        var ctx = h.Render();

        var name = ctx.SingleText("P00");
        Assert.Equal(8, name.Rect.X);
        Assert.Equal(new GridRect(0, 0, 150, 400), name.Clip);
        Assert.Equal(8, ctx.SingleText("Name").Rect.X);
    }

    [Fact]
    public void LeftFrozen_ScrollableCellsScroll_AndAreClippedAtThePaneEdge()
    {
        var h = LeftFrozen();

        var ctx = h.Render();

        var age = ctx.SingleText("20", row: 0, h);
        Assert.Equal(150 - 50 + 8, age.Rect.X);
        Assert.Equal(new GridRect(150, 0, 150, 400), age.Clip);
        Assert.Equal(150, age.Visible!.Value.Left);
    }

    [Fact]
    public void LeftFrozen_NoScrollableCellReachesTheFrozenPane()
    {
        var h = LeftFrozen();

        var ctx = h.Render();

        var cells = ctx.Texts.Where(o => ScrollableCellTexts.Contains(o.Text)).ToList();
        Assert.NotEmpty(cells);
        Assert.All(cells, o => Assert.Equal(0, RectMath.HorizontalOverlap(o.Visible ?? GridRect.Empty, 0, 150)));
    }

    [Fact]
    public void LeftFrozen_PaneHasItsBackgroundAndADivider()
    {
        var h = LeftFrozen();

        var ctx = h.Render();

        Assert.Contains(ctx.Fills, f => f.Color == h.Style.FrozenColumnBackgroundColor && f.Rect.Equals(new GridRect(0, 0, 150, 400)));
        var divider = Assert.Single(ctx.Lines, l => l.Color == h.Style.FrozenColumnDividerColor);
        Assert.Equal((150f, 0f, 150f, 400f), (divider.X1, divider.Y1, divider.X2, divider.Y2));
        Assert.Null(divider.Clip);
    }

    [Fact]
    public void LeftFrozen_CurrentCellBorder_IsDrawnByThePaneThatOwnsTheColumn()
    {
        var h = LeftFrozen();
        h.Selection.CurrentCell = new CellPosition(1, 0);

        var border = Assert.Single(h.Render().Ops, o => o.Kind == DrawOpKind.DrawRect && o.Color == h.Style.CurrentCellBorderColor);

        Assert.Equal(new GridRect(0, h.RowTop(1), 150, 36), border.Rect);
        Assert.Equal(new GridRect(0, 0, 150, 400), border.Clip);
    }

    [Fact]
    public void LeftFrozen_WithLeftDragHandle_BothPanesStartAfterTheHandle()
    {
        var h = Create();
        h.Column("Name").FreezeMode = ColumnFreezeMode.Left;
        h.Style.ShowRowDragHandle = true;

        var ctx = h.Render();

        var name = ctx.SingleText("P00");
        Assert.Equal(32 + 8, name.Rect.X);
        Assert.Equal(new GridRect(32, 0, 150, 400), name.Clip);
        var age = ctx.SingleText("20", row: 0, h);
        Assert.Equal(32 + 150 + 8, age.Rect.X);
        Assert.Equal(new GridRect(182, 0, 418, 400), age.Clip);
        Assert.Contains(ctx.Lines, l => l.Color == h.Style.FrozenColumnDividerColor && Near(l.X1, 182));
    }

    // ── Right-frozen columns ──────────────────────────────────────

    /// <summary>Salary frozen right (350–500) in a 500 px viewport scrolled 50 px.</summary>
    private static RenderHarness RightFrozen()
    {
        var h = Create(width: 500);
        h.Column("Salary").FreezeMode = ColumnFreezeMode.Right;
        h.Scroll.OffsetX = 50;
        return h;
    }

    [Fact]
    public void RightFrozen_CellsAreAnchoredToTheRightEdge_InsideTheirClip()
    {
        var h = RightFrozen();

        var ctx = h.Render();

        var salary = ctx.SingleText("1000");
        Assert.Equal(358, salary.Rect.X);
        Assert.Equal(new GridRect(350, 0, 150, 400), salary.Clip);
        Assert.Equal(358, ctx.SingleText("Salary").Rect.X);
    }

    [Fact]
    public void RightFrozen_ScrollableCellsAreClippedBeforeThePane()
    {
        var h = RightFrozen();

        var ctx = h.Render();

        var cells = ctx.Texts.Where(o => ScrollableCellTexts.Contains(o.Text)).ToList();
        Assert.NotEmpty(cells);
        Assert.All(cells, o => Assert.Equal(new GridRect(0, 0, 350, 400), o.Clip));
        Assert.Equal(250 - 50 + 8, ctx.SingleText("Oslo", row: 0, h).Rect.X);
    }

    [Fact]
    public void RightFrozen_PaneHasItsBackgroundAndADivider()
    {
        var h = RightFrozen();

        var ctx = h.Render();

        Assert.Contains(ctx.Fills, f => f.Color == h.Style.RightFrozenColumnBackgroundColor && f.Rect.Equals(new GridRect(350, 0, 150, 400)));
        var divider = Assert.Single(ctx.Lines, l => l.Color == h.Style.RightFrozenColumnDividerColor);
        Assert.Equal((350f, 0f, 350f, 400f), (divider.X1, divider.Y1, divider.X2, divider.Y2));
    }

    [Fact]
    public void RightFrozen_WithFrozenRows_KeepsFrozenRowsInThePane()
    {
        var h = RightFrozen();
        h.Source.FrozenRowCount = 2;
        h.Scroll.OffsetY = 180;

        var ctx = h.Render();

        var salary = ctx.SingleText("1000");
        Assert.Equal(new GridRect(358, 40, 134, 36), salary.Rect);
        Assert.Equal(new GridRect(350, 0, 150, 400), salary.Clip);
    }

    // ── Frozen rows ───────────────────────────────────────────────

    /// <summary>Two frozen rows (40–112); the scrollable rows below are scrolled 180 px.</summary>
    private static RenderHarness FrozenRows()
    {
        var h = Create();
        h.Source.FrozenRowCount = 2;
        h.Scroll.OffsetY = 180;
        return h;
    }

    [Fact]
    public void FrozenRows_StayBelowTheHeader_WhileTheRestScrolls()
    {
        var h = FrozenRows();

        var ctx = h.Render();

        Assert.Equal(40, ctx.SingleText("P00").Rect.Y);
        Assert.Equal(76, ctx.SingleText("P01").Rect.Y);
        // Scrollable rows start below the frozen band: row 7 = 112 + 5 × 36 − 180.
        Assert.Equal(112, ctx.SingleText("P07").Rect.Y);
    }

    [Fact]
    public void FrozenRows_UseTheFrozenRowBackground_AndADividerBelowThem()
    {
        var h = FrozenRows();

        var ctx = h.Render();

        Assert.Contains(RowBackgrounds(ctx, 40, 600), f => f.Color == h.Style.FrozenRowBackgroundColor);
        Assert.Contains(RowBackgrounds(ctx, 76, 600), f => f.Color == h.Style.AlternateRowColor);
        var divider = Assert.Single(ctx.Lines, l => l.Color == h.Style.FrozenRowDividerColor);
        Assert.Equal((0f, 112f, 600f, 112f), (divider.X1, divider.Y1, divider.X2, divider.Y2));
    }

    [Fact]
    public void FrozenRows_AreDrawnAfterTheScrollableRows_SoScrolledRowsPassUnderThem()
    {
        var h = FrozenRows();

        var ctx = h.Render();

        // Row 4 has scrolled up to y = 4, under the header and the frozen band.
        var scrolledUnder = ctx.SingleText("P04");
        Assert.Equal(4, scrolledUnder.Rect.Y);
        int frozenBackground = RowBackgrounds(ctx, 40, 600).Single(f => f.Color == h.Style.FrozenRowBackgroundColor).Index;
        Assert.True(frozenBackground > scrolledUnder.Index, "scrolled rows are painted over the frozen rows");
    }

    [Fact]
    public void FrozenRows_CurrentCellBorder_IgnoresTheVerticalScroll()
    {
        var h = FrozenRows();
        h.Selection.CurrentCell = new CellPosition(1, 0);

        var border = Assert.Single(h.Render().Ops, o => o.Kind == DrawOpKind.DrawRect && o.Color == h.Style.CurrentCellBorderColor);

        Assert.Equal(new GridRect(0, 76, 150, 36), border.Rect);
    }

    [Fact]
    public void FrozenRows_WithFrozenColumns_AreDrawnInEveryPane()
    {
        var h = FrozenRows();
        h.Column("Name").FreezeMode = ColumnFreezeMode.Left;

        var ctx = h.Render();

        Assert.Equal(new GridRect(0, 0, 150, 400), ctx.SingleText("P00").Clip);
        var salary = ctx.SingleText("1000");
        Assert.Equal(40, salary.Rect.Y);
        Assert.Equal(new GridRect(150, 0, 450, 400), salary.Clip);
    }

    [Fact]
    public void FrozenRows_HonourSelectionFocusAndStyles()
    {
        var h = Create();
        h.Source.FrozenRowCount = 3;
        var highlight = new GridColor(200, 10, 10);
        var rowText = new GridColor(10, 200, 10);
        var warn = new GridColor(250, 120, 0);
        var tint = new GridColor(9, 99, 199);
        h.Selection.HandleRowSelection(0, shift: false, ctrl: false, totalRows: 20);
        h.Selection.CurrentCell = new CellPosition(1, 0);
        h.Style.RowStyleResolver = item => ((RenderHarness.Person)item!).Name == "P02"
            ? new DataGridRowStyle { BackgroundColor = highlight, TextColor = rowText }
            : null;
        h.Style.CellStyleResolver = (item, column) =>
            column.PropertyName == "Age" && ((RenderHarness.Person)item!).Name == "P00" ? new DataGridCellStyle { TextColor = warn } : null;
        h.Column("City").CellStyle = new DataGridCellStyle { BackgroundColor = tint };
        h.Scroll.OffsetY = 100;

        var ctx = h.Render();

        Assert.Contains(RowBackgrounds(ctx, 40, 600), f => f.Color == h.Style.SelectionColor);
        Assert.Contains(RowBackgrounds(ctx, 76, 600), f => f.Color == h.Style.FocusedRowColor);
        Assert.Contains(RowBackgrounds(ctx, 112, 600), f => f.Color == highlight);
        Assert.Contains(ctx.Fills, f => f.Color == tint && f.Rect.Equals(new GridRect(250, 112, 150, 36)));
        Assert.Equal(rowText, ctx.SingleText("P02").Color);
        Assert.Equal(warn, Assert.Single(ctx.TextsEqual("20"), t => Near(t.Rect.Y, 40)).Color);
    }

    [Fact]
    public void FrozenRows_CellBeingEdited_IsSkipped()
    {
        var h = Create();
        h.Source.FrozenRowCount = 2;
        var edit = new KumikoUI.Core.Editing.EditSession { Style = h.Style };
        Assert.True(edit.BeginEdit(1, 0, h.Column("Name"), h.Source, new GridRect(0, 76, 150, 36)));

        var ctx = h.Render(editSession: edit);

        Assert.DoesNotContain(ctx.TextsEqual("P01"), o => o.Rect.Equals(new GridRect(8, 76, 134, 36)));
        Assert.Single(ctx.TextsEqual("P00"));
    }

    // ── Row drag-handle column ────────────────────────────────────

    [Fact]
    public void DragHandleLeft_ShiftsColumnsRight_AndClipsTheScrollablePaneAfterIt()
    {
        var h = Create();
        h.Style.ShowRowDragHandle = true;

        var ctx = h.Render();

        var name = ctx.SingleText("P00");
        Assert.Equal(32 + 8, name.Rect.X);
        Assert.Equal(new GridRect(32, 0, 568, 400), name.Clip);
        Assert.Equal(40, ctx.SingleText("Name").Rect.X);
        Assert.Equal(550 + 32, h.Scroll.ContentWidth);
    }

    [Fact]
    public void DragHandleLeft_DrawsHeaderBodyDividerAndSixGripDotsPerRow()
    {
        var h = Create();
        h.Style.ShowRowDragHandle = true;

        var ctx = h.Render();

        Assert.Contains(ctx.Fills, f => f.Color == h.Style.RowDragHandleHeaderBackgroundColor && f.Rect.Equals(new GridRect(0, 0, 32, 40)));
        Assert.Contains(ctx.Fills, f => f.Color == h.Style.RowDragHandleBackgroundColor && f.Rect.Equals(new GridRect(0, 40, 32, 360)));
        Assert.Contains(ctx.Lines, l => l.Color == h.Style.GridLineColor && Near(l.X1, 32) && Near(l.X2, 32) && Near(l.Y1, 0) && Near(l.Y2, 400));

        var row0Grips = ctx.Fills.Where(f => f.Color == h.Style.RowDragHandleColor && f.Rect.Top >= 40 && f.Rect.Bottom <= 76).ToList();
        Assert.Equal(6, row0Grips.Count);
        Assert.All(row0Grips, g => Assert.True(g.Rect.Left >= 0 && g.Rect.Right <= 32));
        // 2 × 3 dots centred in the 32 × 36 cell: centres at x 13.5 / 18.5, y 53 / 58 / 63.
        Assert.Equal(new[] { 12f, 17f }, row0Grips.Select(g => g.Rect.X).Distinct().OrderBy(x => x));
        Assert.Equal(new[] { 51.5f, 56.5f, 61.5f }, row0Grips.Select(g => g.Rect.Y).Distinct().OrderBy(y => y));
    }

    [Fact]
    public void DragHandleRight_SitsAtTheRightEdge_AndColumnsStartAtZero()
    {
        var h = Create();
        h.Style.ShowRowDragHandle = true;
        h.Style.RowDragHandlePosition = DragHandlePosition.Right;

        var ctx = h.Render();

        Assert.Equal(8, ctx.SingleText("P00").Rect.X);
        Assert.Equal(new GridRect(0, 0, 568, 400), ctx.SingleText("P00").Clip);
        Assert.Contains(ctx.Fills, f => f.Color == h.Style.RowDragHandleHeaderBackgroundColor && f.Rect.Equals(new GridRect(568, 0, 32, 40)));
        Assert.All(ctx.Fills.Where(f => f.Color == h.Style.RowDragHandleColor), g => Assert.True(g.Rect.Left >= 568));
    }

    [Fact]
    public void DragHandle_FrozenRowsGetGripsAtTheirFixedPositions()
    {
        var h = Create();
        h.Style.ShowRowDragHandle = true;
        h.Source.FrozenRowCount = 2;
        h.Scroll.OffsetY = 180;

        var grips = h.Render().Fills.Where(f => f.Color == h.Style.RowDragHandleColor).ToList();

        Assert.Equal(6, grips.Count(g => g.Rect.Top >= 40 && g.Rect.Bottom <= 76));
        Assert.Equal(6, grips.Count(g => g.Rect.Top >= 76 && g.Rect.Bottom <= 112));
        // Scrollable rows hidden under the frozen band get no grips.
        Assert.DoesNotContain(grips, g => g.Rect.Bottom <= 40);
    }

    [Fact]
    public void DragHandle_GripsSkipGroupRows()
    {
        var h = Create(rows: 4);
        h.Style.ShowRowDragHandle = true;
        h.Source.AddGroupDescription(new GroupDescription("City"));

        var ctx = h.Render();

        // Rows: Oslo header, P00, P02, Rome header, P01, P03 → 4 data rows × 6 dots.
        Assert.Equal(24, ctx.Fills.Count(f => f.Color == h.Style.RowDragHandleColor));
    }
}

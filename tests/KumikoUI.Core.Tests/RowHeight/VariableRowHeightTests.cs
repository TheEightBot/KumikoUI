using KumikoUI.Core.Input;
using KumikoUI.Core.Layout;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;
using static KumikoUI.Core.Tests.RowHeight.RowHeightGrid;

namespace KumikoUI.Core.Tests.RowHeight;

/// <summary>
/// Variable and auto row height (#320, spec #102 §7, §11): drawing, the resolver, Min/MaxRowHeight, frozen rows,
/// summaries, hit testing, keyboard paging and ensure-visible, scroll anchoring, pending scroll targets, row drag and
/// invalidation. Geometry: <see cref="RowHeightGrid"/>.
/// </summary>
public class VariableRowHeightTests
{
    private static bool Near(float a, float b, float tolerance = 0.01f) => Math.Abs(a - b) < tolerance;

    private static DrawOp RowBackground(RecordingDrawingContext ctx, float y) =>
        ctx.Ops.First(o => o.Kind == DrawOpKind.FillRect && Near(o.Rect.Y, y) && Near(o.Rect.X, 0) && o.Rect.Width >= Width);

    // ── Drawing ──

    [Fact]
    public void Auto_SizesEachRowToItsWrappedNote_AndDrawsEveryLine()
    {
        var g = new RowHeightGrid(i => i == 1 ? 3 : 1);

        var ctx = g.Render();

        Assert.Equal(36f, g.H(0));
        Assert.Equal(AutoHeight(3), g.H(1));      // 14 + 32 + 12 = 58
        Assert.Equal(Header + 36 + 58, g.Y(2));
        Assert.Equal(58f, RowBackground(ctx, Header + 36).Rect.Height);
        Assert.Equal(36f, RowBackground(ctx, Header + 36 + 58).Rect.Height);
        Assert.Equal(2, ctx.TextsEqual(new string('x', 16)).Count());   // two full lines and a last one of 4
        Assert.Equal(Header + 36 + 58 + 36, ctx.Lines.Where(l => Near(l.Y1, l.Y2) && l.Y1 > Header)
            .Select(l => l.Y1).Distinct().OrderBy(y => y).ElementAt(2), 2);   // the grid line below row 2
    }

    [Fact]
    public void Fixed_IsUnchanged_EvenWithWrappingColumns()
    {
        var g = new RowHeightGrid(i => 4);
        g.Style.RowHeightMode = KumikoRowHeightMode.Fixed;

        g.Render();

        Assert.True(g.Rows.IsUniform(g.Style));
        Assert.Equal(Header + 5 * 36f, g.Y(5));
        Assert.Equal(0, g.Counter.Calls);   // a Fixed grid without a resolver measures nothing
    }

    [Fact]
    public void MaxRowHeight_CapsTheRow_AndTheLastLineEndsWithAnEllipsis()
    {
        var g = new RowHeightGrid(i => i == 0 ? 6 : 1);
        g.Style.MaxRowHeight = 50;

        var ctx = g.Render();

        Assert.Equal(50f, g.H(0));
        Assert.Contains(ctx.Texts, t => t.Text!.EndsWith('…'));
    }

    [Fact]
    public void MinRowHeight_IsTheFloor_ForRowsThatDoNotWrap()
    {
        var g = new RowHeightGrid(i => i == 0 ? 3 : 1);
        g.Style.MinRowHeight = 30;

        g.Render();

        Assert.Equal(30f, g.H(1));
        Assert.Equal(58f, g.H(0));
    }

    [Fact]
    public void AResolver_SetsRowHeightsInFixedMode_AndOnlyReadsTheRowsItDraws()
    {
        var g = new RowHeightGrid(_ => 1, rows: 1000);
        g.Style.RowHeightMode = KumikoRowHeightMode.Fixed;
        int calls = 0;
        g.Style.RowHeightResolver = (in KumikoRowHeightContext c) =>
        {
            calls++;
            Assert.Equal(KumikoRowHeightMode.Fixed, c.Mode);
            Assert.Equal(36f, c.DefaultHeight);
            return ((Note)c.Item).Pinned ? 56 : null;
        };

        g.Render();

        Assert.Equal(56f, g.H(0));
        Assert.Equal(36f, g.H(1));
        Assert.Equal(Header + 56 + 4 * 36f, g.Y(5));
        Assert.InRange(calls, 10, 20);         // the visible rows and the overscan, not 1000
        Assert.Equal(0, g.Counter.Calls);      // the resolver did not ask for the content height

        g.Render();
        Assert.InRange(calls, 10, 20);         // a steady frame asks nothing
    }

    [Fact]
    public void AResolver_CanAdjustTheMeasuredHeight_AndInvalidResultsMeanTheModeHeight()
    {
        var g = new RowHeightGrid(i => i < 2 ? 3 : 1);
        g.Style.RowHeightResolver = (in KumikoRowHeightContext c) => c.Row switch
        {
            0 => c.MeasureContentHeight() + 10,
            1 => float.NaN,
            2 => 0.5f,
            _ => null,
        };

        g.Render();

        Assert.Equal(68f, g.H(0));
        Assert.Equal(58f, g.H(1));
        Assert.Equal(36f, g.H(2));
    }

    [Fact]
    public void FrozenRows_TakeTheirMeasuredHeights_AndTheScrollingRowsStartBelowThem()
    {
        var g = new RowHeightGrid(i => i < 2 ? 3 : 1);
        g.Source.FrozenRowCount = 2;
        g.Scroll.OffsetY = 100;

        var ctx = g.Render();

        Assert.Equal(116f, g.Rows.GetFrozenHeight(g.Style));
        Assert.Equal(Header + 58, g.Y(1));
        Assert.Equal(Header + 116 - 100 + 36 * 3, g.Y(5), 2);
        Assert.NotNull(RowBackground(ctx, Header + 58));
        Assert.Equal(1, g.HitTest(400, Header + 58 + 57).RowIndex);
    }

    [Fact]
    public void AnUnpinnedBottomSummary_FollowsTheLastRow()
    {
        var g = new RowHeightGrid(i => i % 2 == 0 ? 3 : 1, rows: 4);
        g.Style.PinBottomSummaryRows = false;
        g.Source.AddTableSummaryRow(new TableSummaryRow
        {
            Name = "Totals", Position = SummaryPosition.Bottom,
            Columns = { new SummaryColumnDescription(nameof(Note.Age), SummaryType.Sum) },
        });

        g.Render();
        float bottom = Header + 2 * 58 + 2 * 36;

        Assert.Equal(HitRegion.TableSummaryRow, g.HitTest(400, bottom + 5).Region);
        Assert.Equal(3, g.HitTest(400, bottom - 5).RowIndex);
    }

    // ── Hit testing ──

    [Fact]
    public void HitTesting_RoundTripsEveryVisibleRow_WithMixedHeights()
    {
        var g = new RowHeightGrid(i => 1 + i % 4, rows: 60);
        g.Scroll.OffsetY = 210;
        g.Render();

        int first = g.Rows.GetScrollRowAt(g.Scroll.OffsetY, g.Style);
        for (int row = first + 1; row < g.Source.RowCount && g.Y(row) + g.H(row) < Height; row++)
        {
            Assert.Equal(row, g.HitTest(400, g.Y(row) + 0.5f).RowIndex);
            Assert.Equal(row, g.HitTest(400, g.Y(row) + g.H(row) - 0.5f).RowIndex);
        }
    }

    [Fact]
    public void TheEditorAndCellBounds_CoverTheWholeTallRow()
    {
        var g = new RowHeightGrid(i => i == 1 ? 4 : 1);
        g.Render();

        var bounds = KumikoInputController.GetCellScreenBounds(1, 1, g.Scroll, g.Style, g.Source.Columns, dataSource: g.Source);

        Assert.Equal(Header + 36, bounds.Y);
        Assert.Equal(AutoHeight(4), bounds.Height);
    }

    // ── Keyboard ──

    [Fact]
    public void PageDownAndPageUp_MoveByWholePagesOfRowsOfAnyHeight()
    {
        // Rows alternate 100 and 20 px: a 360 px page holds six of them.
        var g = new RowHeightGrid(_ => 1);
        g.Style.RowHeightResolver = (in KumikoRowHeightContext c) => c.Row % 2 == 0 ? 100 : 20;
        g.Render();
        g.Selection.CurrentCell = new CellPosition(0, 0);

        g.Key(KumikoKey.PageDown);
        Assert.Equal(6, g.Selection.CurrentCell.Row);

        g.Render();
        g.Key(KumikoKey.PageUp);
        Assert.Equal(0, g.Selection.CurrentCell.Row);
    }

    [Fact]
    public void ARowTallerThanTheViewport_ShowsItsTop_AndStaysPutWhileItsTopIsOnScreen()
    {
        var g = new RowHeightGrid(_ => 1);
        g.Style.RowHeightResolver = (in KumikoRowHeightContext c) => c.Row == 5 ? 1000 : null;
        g.Render();

        // Row 5 starts 180 px down, on screen: Nearest leaves it alone; Start puts its top at the top
        Assert.Equal(0, KumikoScroller.GetRowOffsetY(g.Source, g.Style, g.Scroll, 5));
        Assert.Equal(180, KumikoScroller.GetRowOffsetY(g.Source, g.Style, g.Scroll, 5, KumikoScrollPosition.Start));

        // Scrolled past its top: Nearest brings the top back
        g.Scroll.OffsetY = 400;
        Assert.Equal(180, KumikoScroller.GetRowOffsetY(g.Source, g.Style, g.Scroll, 5));
    }

    [Fact]
    public void CtrlEnd_ShowsTheLastRowFully_AfterOneFrame()
    {
        var g = new RowHeightGrid(i => 1 + i % 3, rows: 300);
        g.Render();
        g.Selection.CurrentCell = new CellPosition(0, 0);

        g.Key(KumikoKey.End, InputModifiers.Control);
        g.Render();

        int last = g.Source.RowCount - 1;
        Assert.Equal(last, g.Selection.CurrentCell.Row);
        Assert.Equal(Height, g.Y(last) + g.H(last), 1);
    }

    // ── Scrolling ──

    [Fact]
    public void MeasuringRowsAboveTheViewport_KeepsTheVisibleRowsStill()
    {
        // Every note is three lines (58 px), but the rows are estimated at 36 px before they are drawn.
        var g = new RowHeightGrid(_ => 3, rows: 300);
        g.Scroll.OffsetY = 50 * 36;
        Assert.Equal(Header, g.Y(50));

        var ctx = g.Render();

        Assert.Equal(Header, g.Y(50), 1);        // the anchor row has not moved
        Assert.Equal(50 * 58f, g.Scroll.OffsetY, 1);
        Assert.Equal(58f, RowBackground(ctx, Header).Rect.Height);
    }

    [Fact]
    public void AtTheTop_MeasuringMovesNothing()
    {
        var g = new RowHeightGrid(_ => 3, rows: 100);

        g.Render();

        Assert.Equal(0, g.Scroll.OffsetY);
        Assert.Equal(Header, g.Y(0));
    }

    [Fact]
    public void ScrollToRow_LandsExactly_AfterOneFrame()
    {
        var g = new RowHeightGrid(i => i % 2 == 0 ? 4 : 1, rows: 400);
        g.Render();
        int scrolled = 0;
        g.Scroll.ScrollChanged += (_, _) => scrolled++;

        Assert.True(KumikoScroller.ScrollToRow(g.Scroll, g.Source, g.Style, 250, KumikoScrollPosition.Start));
        g.Render();

        Assert.Equal(Header, g.Y(250), 1);
        Assert.InRange(scrolled, 1, 2);
    }

    [Fact]
    public void AUserScrollBeforeTheNextFrame_CancelsThePendingTarget()
    {
        var g = new RowHeightGrid(i => i % 2 == 0 ? 4 : 1, rows: 400);
        g.Render();

        KumikoScroller.ScrollToRow(g.Scroll, g.Source, g.Style, 250, KumikoScrollPosition.Start);
        g.Scroll.OffsetY += 30;
        int firstVisible = g.Rows.GetScrollRowAt(g.Scroll.OffsetY, g.Style);
        float before = g.Y(firstVisible);
        g.Render();

        Assert.Equal(before, g.Y(firstVisible), 1);   // anchored, not sent back to row 250
    }

    // ── Row drag ──

    [Fact]
    public void ADrop_SplitsAtTheRealBoundaryBetweenRowsOfDifferentHeights()
    {
        var g = new RowHeightGrid(i => i == 1 ? 6 : 1);   // row 1 is 106 px tall
        g.Render();
        float row2Top = Header + 36 + AutoHeight(6);

        // The ghost of row 3 (36 px): its middle decides the target
        Assert.Equal(1, RowDragGeometry.DropTarget(row2Top - 1 - 18, g.Scroll, g.Style, g.Source, 3));
        Assert.Equal(2, RowDragGeometry.DropTarget(row2Top - 18, g.Scroll, g.Style, g.Source, 3));
        Assert.Equal(AutoHeight(6), RowDragGeometry.GhostHeight(1, g.Style, g.Source));
        Assert.Equal(row2Top, RowDragGeometry.IndicatorY(0, 1, g.Scroll, g.Style, g.Source));
    }

    // ── Sticky group headers ──

    [Fact]
    public void StickyGroupHeaders_PinAboveTallRows_AndAreHitAsGroupHeaders()
    {
        var g = new RowHeightGrid(i => 1 + i % 3, rows: 60);
        g.Source.AddGroupDescription(new GroupDescription(nameof(Note.Pinned)));
        g.Scroll.OffsetY = 300;
        g.Render();
        float rowsTop = g.Style.GroupPanelHeight + Header;

        var hit = g.HitTest(400, rowsTop + 5);

        Assert.Equal(HitRegion.GroupHeaderRow, hit.Region);
        Assert.True(g.Source.IsGroupHeaderRow(hit.RowIndex));
        Assert.Equal(36f, g.H(hit.RowIndex));
    }

    // ── Invalidation ──

    [Fact]
    public void AnItemChange_RemeasuresOnlyThatRow_AndAnUnrelatedPropertyNothing()
    {
        var g = new RowHeightGrid(_ => 1);
        g.Render();
        g.Counter.Calls = 0;

        g.Items[2].Text = Lines(3);
        g.Render();
        Assert.Equal(1, g.Counter.Calls);
        Assert.Equal(58f, g.H(2));

        g.Items[2].Age++;
        g.Render();
        Assert.Equal(1, g.Counter.Calls);

        g.Source.SetCellValue(3, g.NotesColumn, Lines(2));
        g.Render();
        Assert.Equal(2, g.Counter.Calls);
        Assert.Equal(AutoHeight(2), g.H(3));
    }

    [Fact]
    public void WithAResolver_AnyPropertyChangeRemeasuresTheRow()
    {
        var g = new RowHeightGrid(_ => 1);
        int calls = 0;
        g.Style.RowHeightResolver = (in KumikoRowHeightContext c) => { calls++; return null; };
        g.Render();
        calls = 0;

        g.Items[2].Age++;
        g.Render();

        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData("refresh")]
    [InlineData("width")]
    [InlineData("padding")]
    [InlineData("rowHeight")]
    [InlineData("invalidateAll")]
    [InlineData("culture")]
    public void ChangesThatAffectEveryRow_RemeasureTheVisibleRows(string change)
    {
        var g = new RowHeightGrid(_ => 2);
        g.Render();
        g.Counter.Calls = 0;

        switch (change)
        {
            case "refresh": g.Source.Refresh(); break;
            case "width": g.NotesColumn.Width = 120; break;
            case "padding": g.Style.CellPadding = 4; break;
            case "rowHeight": g.Style.RowHeight = 30; break;
            case "invalidateAll": g.Source.InvalidateRowHeights(); break;
            case "culture": g.Source.Culture = new System.Globalization.CultureInfo("de-DE"); break;
        }
        g.Render();

        Assert.InRange(g.Counter.Calls, 8, 20);
    }

    [Fact]
    public void AColorChange_RemeasuresNothing_AndInvalidateRowHeightOnlyItsRow()
    {
        var g = new RowHeightGrid(_ => 2);
        g.Render();
        g.Counter.Calls = 0;

        g.Style.SelectionColor = new KumikoColor(1, 2, 3);
        g.Style.GridLineColor = new KumikoColor(3, 2, 1);
        g.Render();
        Assert.Equal(0, g.Counter.Calls);

        g.Source.InvalidateRowHeight(g.Items[4]);
        g.Render();
        Assert.Equal(1, g.Counter.Calls);
    }

    [Fact]
    public void Sorting_KeepsTheMeasuredHeights()
    {
        var g = new RowHeightGrid(i => 1 + i % 3, rows: 8);   // every row on screen
        g.Render();
        g.Counter.Calls = 0;
        var tall = g.Items[2];

        g.Source.ToggleSort(g.NameColumn);
        g.Source.ToggleSort(g.NameColumn);   // descending
        g.Render();

        Assert.Equal(0, g.Counter.Calls);
        Assert.Equal(AutoHeight(3), g.H(g.Source.IndexOfItem(tall)));
    }

    [Fact]
    public void TheLayoutEngineOverloads_UseTheRowLayout()
    {
        var g = new RowHeightGrid(i => i == 0 ? 3 : 1, rows: 3);
        g.Render();
        var engine = new KumikoLayoutEngine();

        Assert.Equal(Header + 58 + 72, engine.GetContentHeight(g.Source, g.Style));
        Assert.Equal(0, engine.GetFrozenRowHeight(g.Source, g.Style));
        Assert.Equal((0, 2), engine.GetVisibleRowRangeWithOffset(g.Scroll, g.Style, g.Source, 0));
    }
}

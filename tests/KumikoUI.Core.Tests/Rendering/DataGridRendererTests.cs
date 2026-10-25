using KumikoUI.Core.Editing;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;
using static KumikoUI.Core.Tests.TestHelpers.RenderHarness;

namespace KumikoUI.Core.Tests.Rendering;

/// <summary>
/// Golden-command tests for <see cref="DataGridRenderer"/>: frame structure, header, data rows,
/// cell renderers, grid lines, the current-cell border, sort indicators and filter icons.
/// Geometry is described on <see cref="RenderHarness"/>.
/// </summary>
public class DataGridRendererTests
{
    // ── Frame ─────────────────────────────────────────────────────

    [Fact]
    public void Render_FirstOp_ClearsTheViewportWithTheBackground()
    {
        var h = Create();

        var first = h.Render().Ops[0];

        Assert.Equal(DrawOpKind.FillRect, first.Kind);
        Assert.Equal(new GridRect(0, 0, 600, 400), first.Rect);
        Assert.Equal(h.Style.BackgroundColor, first.Color);
        Assert.Null(first.Clip);
    }

    [Fact]
    public void Render_ComputesContentSize()
    {
        var h = Create();
        h.Source.AddTableSummaryRow(new TableSummaryRow("Totals", SummaryPosition.Top)
        {
            Columns = { new SummaryColumnDescription("Age", SummaryType.Sum) },
        });

        h.Render();

        Assert.Equal(550, h.Scroll.ContentWidth);
        Assert.Equal(40 + 36 + 20 * 36, h.Scroll.ContentHeight);
    }

    public static TheoryData<string> Configurations => new()
    {
        "plain", "frozen-left", "frozen-right", "frozen-rows", "grouped", "summaries",
        "drag-handle-left", "drag-handle-right", "column-drag", "row-drag",
    };

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Render_SaveAndRestore_AreBalanced(string configuration)
    {
        var h = Create(width: 500);
        int dragColumn = -1, dragRow = -1;
        switch (configuration)
        {
            case "frozen-left": h.Column("Name").FreezeMode = ColumnFreezeMode.Left; break;
            case "frozen-right": h.Column("Salary").FreezeMode = ColumnFreezeMode.Right; break;
            case "frozen-rows": h.Source.FrozenRowCount = 2; break;
            case "grouped": h.Source.AddGroupDescription(new GroupDescription("City")); break;
            case "summaries":
                h.Source.AddTableSummaryRow(new TableSummaryRow("Top", SummaryPosition.Top) { Columns = { new SummaryColumnDescription("Age", SummaryType.Sum) } });
                h.Source.AddTableSummaryRow(new TableSummaryRow("Bottom") { Columns = { new SummaryColumnDescription("Age", SummaryType.Sum) } });
                break;
            case "drag-handle-left": h.Style.ShowRowDragHandle = true; break;
            case "drag-handle-right":
                h.Style.ShowRowDragHandle = true;
                h.Style.RowDragHandlePosition = DragHandlePosition.Right;
                break;
            case "column-drag": dragColumn = 1; break;
            case "row-drag": dragRow = 2; break;
        }

        var ctx = h.Render(dragColumnIndex: dragColumn, dragColumnScreenX: 100, dragRowIndex: dragRow, dragRowScreenY: 150);

        Assert.Equal(0, ctx.SaveDepth);
        Assert.Equal(0, ctx.UnbalancedRestores);
        Assert.True(ctx.Ops.Count > 0);
    }

    // ── Header ────────────────────────────────────────────────────

    [Fact]
    public void Header_FillsTheBandAndDrawsTheBottomBorder()
    {
        var h = Create();

        var ctx = h.Render();

        Assert.Contains(ctx.Fills, f => f.Color == h.Style.HeaderBackgroundColor && f.Rect.Equals(new GridRect(0, 0, 600, 40)));
        Assert.Contains(ctx.Lines, l => l.Color == h.Style.HeaderBorderColor && Near(l.Y1, 40) && Near(l.Y2, 40) && Near(l.X1, 0) && Near(l.X2, 600));
    }

    [Fact]
    public void Header_DrawsEachTitleInItsColumn_ReservingSortAndFilterSpace()
    {
        var h = Create();

        var ctx = h.Render();

        // Sorting (12 px) and filtering (14 px) are allowed by default: 26 px reserved on the right.
        var expected = new[] { ("Name", 0f, 150f), ("Age", 150f, 100f), ("City", 250f, 150f), ("Salary", 400f, 150f) };
        foreach (var (title, x, width) in expected)
        {
            var op = ctx.SingleText(title);
            Assert.Equal(new GridRect(x + 8, 0, width - 16 - 26, 40), op.Rect);
            Assert.Equal(h.Style.HeaderTextColor, op.Color);
            Assert.Same(h.Style.HeaderFont, op.Font);
        }
    }

    [Theory]
    [InlineData(false, false, 0f)]
    [InlineData(true, false, 12f)]
    [InlineData(false, true, 14f)]
    [InlineData(true, true, 26f)]
    public void Header_TitleWidth_ReservesRoomOnlyForEnabledIcons(bool sorting, bool filtering, float reserved)
    {
        var h = Create();
        var age = h.Column("Age");
        age.AllowSorting = sorting;
        age.AllowFiltering = filtering;

        var op = h.Render().SingleText("Age");

        Assert.Equal(100 - 16 - reserved, op.Rect.Width);
    }

    [Fact]
    public void Header_PerColumnHeaderStyle_OverridesBackgroundTextAndPadding()
    {
        var h = Create();
        var background = new GridColor(10, 20, 30);
        var text = new GridColor(40, 50, 60);
        h.Column("City").HeaderCellStyle = new CellStyle { BackgroundColor = background, TextColor = text, Padding = 2 };

        var ctx = h.Render();

        Assert.Contains(ctx.Fills, f => f.Color == background && f.Rect.Equals(new GridRect(250, 0, 150, 40)));
        var title = ctx.SingleText("City");
        Assert.Equal(text, title.Color);
        Assert.Equal(252, title.Rect.X);
        Assert.Equal(h.Style.HeaderTextColor, ctx.SingleText("Age").Color);
    }

    [Fact]
    public void Header_IsDrawnAfterTheRows_SoRowsScrolledUpwardPassUnderIt()
    {
        var h = Create();
        h.Scroll.OffsetY = 50;

        var ctx = h.Render();

        int headerFill = ctx.Ops.FindIndex(o => o.Color == h.Style.HeaderBackgroundColor);
        int lastRowText = ctx.Ops.FindLastIndex(o => o.IsText && o.Text!.StartsWith('P'));
        Assert.True(headerFill > lastRowText, "rows are painted over the header");
    }

    // ── Rows ──────────────────────────────────────────────────────

    [Fact]
    public void Rows_OnlyVisibleRowsAreDrawn()
    {
        var h = Create();

        var ctx = h.Render();

        // 400 px viewport, 40 px header, 36 px rows: rows 0–9 are on screen.
        for (int row = 0; row <= 9; row++)
            Assert.Single(ctx.TextsEqual($"P{row:D2}"));
        for (int row = 12; row < 20; row++)
            Assert.Empty(ctx.TextsEqual($"P{row:D2}"));
    }

    [Fact]
    public void Rows_CellTextSitsInItsColumnInsideTheCellPadding()
    {
        var h = Create();

        var ctx = h.Render();

        var name = ctx.SingleText("P03");
        Assert.Equal(new GridRect(8, h.RowTop(3), 150 - 16, 36), name.Rect);
        Assert.Equal(h.Style.CellTextColor, name.Color);
        Assert.Same(h.Style.CellFont, name.Font);

        var salary = ctx.SingleText("1030");
        Assert.Equal(new GridRect(408, h.RowTop(3), 150 - 16, 36), salary.Rect);
    }

    [Fact]
    public void Rows_TextUsesTheColumnAlignment()
    {
        var h = Create();
        h.Column("Salary").TextAlignment = GridTextAlignment.Right;

        var ctx = h.Render();

        Assert.Equal(GridTextAlignment.Right, ctx.SingleText("1000").HAlign);
        Assert.Equal(GridTextAlignment.Left, ctx.SingleText("P00").HAlign);
    }

    [Fact]
    public void Rows_ScrolledVertically_MoveUpByTheOffset()
    {
        var h = Create();
        h.Scroll.OffsetY = 72;

        var ctx = h.Render();

        Assert.Equal(40, ctx.SingleText("P02").Rect.Y);
    }

    [Fact]
    public void Rows_ScrolledHorizontally_MoveLeftByTheOffset()
    {
        var h = Create(width: 300);
        h.Scroll.OffsetX = 100;

        var ctx = h.Render();

        Assert.Equal(8 - 100, ctx.SingleText("P00").Rect.X);
        Assert.Equal(250 - 100 + 8, ctx.SingleText("Oslo", row: 0, h).Rect.X);
    }

    [Theory]
    [InlineData(true, 1, "AlternateRowColor")]
    [InlineData(true, 2, "BackgroundColor")]
    [InlineData(false, 1, "BackgroundColor")]
    public void Rows_Background_AlternatesWhenEnabled(bool alternate, int row, string expectedColor)
    {
        var h = Create();
        h.Style.AlternateRowBackground = alternate;

        var ctx = h.Render();

        var expected = (GridColor)typeof(DataGridStyle).GetProperty(expectedColor)!.GetValue(h.Style)!;
        Assert.Contains(RowBackgrounds(ctx, h.RowTop(row), 600), f => f.Color == expected);
    }

    [Fact]
    public void Rows_SelectedRowBeatsFocusedRow_AndFocusedRowIsTinted()
    {
        var h = Create();
        h.Selection.HandleRowSelection(2, shift: false, ctrl: false, totalRows: 20);
        h.Selection.CurrentCell = new CellPosition(4, 0);

        var ctx = h.Render();

        Assert.Contains(RowBackgrounds(ctx, h.RowTop(2), 600), f => f.Color == h.Style.SelectionColor);
        Assert.Contains(RowBackgrounds(ctx, h.RowTop(4), 600), f => f.Color == h.Style.FocusedRowColor);

        h.Selection.CurrentCell = new CellPosition(2, 0);
        Assert.Contains(RowBackgrounds(h.Render(), h.RowTop(2), 600), f => f.Color == h.Style.SelectionColor);
    }

    [Fact]
    public void Rows_RowStyleResolver_SetsTheRowBackground()
    {
        var h = Create();
        var highlight = new GridColor(200, 10, 10);
        h.Style.RowStyleResolver = item => ((RenderHarness.Person)item!).Age == 22 ? new DataGridRowStyle { BackgroundColor = highlight } : null;

        var ctx = h.Render();

        Assert.Contains(RowBackgrounds(ctx, h.RowTop(2), 600), f => f.Color == highlight);
        Assert.DoesNotContain(RowBackgrounds(ctx, h.RowTop(3), 600), f => f.Color == highlight);
    }

    [Fact]
    public void Rows_RowStyleResolver_TextColorAppliesToThatRowOnly()
    {
        var h = Create();
        var red = new GridColor(255, 0, 0);
        h.Style.RowStyleResolver = item => ((RenderHarness.Person)item!).Name == "P00" ? new DataGridRowStyle { TextColor = red } : null;

        var ctx = h.Render();

        Assert.Equal(red, ctx.SingleText("P00").Color);
        Assert.Equal(red, ctx.SingleText("1000").Color);
        Assert.Equal(h.Style.CellTextColor, ctx.SingleText("P01").Color);
    }

    [Fact]
    public void Rows_ColumnCellStyle_FillsTheCellBackground()
    {
        var h = Create();
        var tint = new GridColor(9, 99, 199);
        h.Column("City").CellStyle = new CellStyle { BackgroundColor = tint };

        var ctx = h.Render();

        Assert.Contains(ctx.Fills, f => f.Color == tint && f.Rect.Equals(new GridRect(250, h.RowTop(1), 150, 36)));
    }

    [Fact]
    public void Rows_CellStyleResolver_OverridesTextColorForMatchingCells()
    {
        var h = Create();
        var warn = new GridColor(250, 120, 0);
        h.Style.CellStyleResolver = (item, column) =>
            column.PropertyName == "Age" && ((RenderHarness.Person)item!).Age == 21 ? new CellStyle { TextColor = warn } : null;

        var ctx = h.Render();

        Assert.NotEmpty(ctx.TextsEqual("21"));
        Assert.All(ctx.TextsEqual("21"), op => Assert.Equal(warn, op.Color));
        Assert.NotEmpty(ctx.TextsEqual("20"));
        Assert.All(ctx.TextsEqual("20"), op => Assert.Equal(h.Style.CellTextColor, op.Color));
    }

    [Fact]
    public void Rows_CellBeingEdited_IsSkipped_AndTheEditorIsDrawnLast()
    {
        var h = Create();
        var edit = new EditSession { Style = h.Style };
        var cell = new GridRect(0, h.RowTop(1), 150, 36);
        Assert.True(edit.BeginEdit(1, 0, h.Column("Name"), h.Source, cell));

        var ctx = h.Render(editSession: edit);

        // The renderer's own cell text for P01 sits at the padded cell rect; the editor draws elsewhere.
        Assert.DoesNotContain(ctx.TextsEqual("P01"), o => o.Rect.Equals(new GridRect(8, h.RowTop(1), 134, 36)));
        int editorBackground = ctx.Ops.FindLastIndex(o => o.Kind == DrawOpKind.FillRect && o.Rect.Equals(edit.ActiveEditor!.Bounds));
        int lastGridOp = ctx.Ops.FindLastIndex(o => o.Color == h.Style.HeaderBackgroundColor);
        Assert.True(editorBackground > lastGridOp, "editor is drawn under the grid");
        Assert.Null(ctx.Ops[editorBackground].Clip);
    }

    [Fact]
    public void Rows_CustomCellRenderer_ReceivesThePaddedCellRectValueAndText()
    {
        var h = Create(rows: 3);
        var probe = new CapturingCellRenderer();
        h.Column("Salary").CustomCellRenderer = probe;

        h.Render();

        Assert.Equal(3, probe.Calls.Count);
        var (rect, value, text) = probe.Calls[1];
        Assert.Equal(new GridRect(408, h.RowTop(1), 134, 36), rect);
        Assert.Equal(1010m, value);
        Assert.Equal("1010", text);
    }

    [Fact]
    public void SetCellRenderer_ReplacesTheRendererForEveryColumnOfThatType()
    {
        var h = Create(rows: 2);
        var probe = new CapturingCellRenderer();
        h.Renderer.SetCellRenderer(DataGridColumnType.Numeric, probe);

        var ctx = h.Render();

        Assert.Same(probe, h.Renderer.GetCellRenderer(DataGridColumnType.Numeric));
        Assert.Equal(4, probe.Calls.Count); // Age and Salary, two rows each
        Assert.Empty(ctx.TextsEqual("1000"));
        Assert.Single(ctx.TextsEqual("P00"));
    }

    [Fact]
    public void GetCellRenderer_UnregisteredType_FallsBackToTheTextRenderer()
    {
        var renderer = new DataGridRenderer();

        Assert.IsType<TextCellRenderer>(renderer.GetCellRenderer((DataGridColumnType)999));
        Assert.IsType<BooleanCellRenderer>(renderer.GetCellRenderer(DataGridColumnType.Boolean));
        Assert.IsType<ImageCellRenderer>(renderer.GetCellRenderer(DataGridColumnType.Image));
    }

    [Fact]
    public void Rows_BooleanColumn_DrawsACheckboxAndACheckmarkOnlyWhenTrue()
    {
        var h = Create(rows: 2);
        h.Source.SetColumns(new[] { new DataGridColumn { Header = "Active", PropertyName = "Active", Width = 100, ColumnType = DataGridColumnType.Boolean } });

        var ctx = h.Render();

        var boxes = ctx.Ops.Where(o => o.Kind == DrawOpKind.DrawRect && o.Color == h.Style.GridLineColor).ToList();
        Assert.Equal(2, boxes.Count);
        // 16 px box centred in the padded cell (8..92 x row).
        Assert.Equal(new GridRect(8 + (84 - 16) / 2f, h.RowTop(0) + 10, 16, 16), boxes[0].Rect);
        var checkmarks = ctx.Lines.Where(l => l.Color == h.Style.AccentColor).ToList();
        Assert.Equal(2, checkmarks.Count); // row 0 is Active, row 1 is not
        Assert.All(checkmarks, l => Assert.True(l.Rect.Y >= h.RowTop(0) && l.Rect.Bottom <= h.RowTop(1)));
    }

    // ── Grid lines ────────────────────────────────────────────────

    [Fact]
    public void GridLines_HorizontalAtEveryRowBoundary_VerticalAtEveryColumnEdge()
    {
        var h = Create(rows: 3);

        var lines = h.Render().Lines.Where(l => l.Color == h.Style.GridLineColor).ToList();

        var horizontalYs = lines.Where(l => Near(l.Y1, l.Y2)).Select(l => l.Y1).Distinct().OrderBy(y => y);
        Assert.Equal(new[] { 40f, 76f, 112f, 148f }, horizontalYs);
        var vertical = lines.Where(l => Near(l.X1, l.X2)).ToList();
        Assert.Equal(new[] { 150f, 250f, 400f, 550f }, vertical.Select(l => l.X1).OrderBy(x => x));
        // Vertical lines stop at the last row instead of running to the viewport bottom.
        Assert.All(vertical, l => Assert.Equal(148f, l.Y2));
    }

    [Fact]
    public void GridLines_CanBeTurnedOff()
    {
        var h = Create(rows: 3);
        h.Style.ShowHorizontalGridLines = false;
        h.Style.ShowVerticalGridLines = false;

        Assert.DoesNotContain(h.Render().Lines, l => l.Color == h.Style.GridLineColor);
    }

    // ── Current cell ──────────────────────────────────────────────

    [Fact]
    public void CurrentCell_IsOutlined()
    {
        var h = Create();
        h.Selection.CurrentCell = new CellPosition(2, 1);

        var border = Assert.Single(h.Render().Ops, o => o.Kind == DrawOpKind.DrawRect && o.Color == h.Style.CurrentCellBorderColor);

        Assert.Equal(new GridRect(150, h.RowTop(2), 100, 36), border.Rect);
        Assert.Equal(h.Style.CurrentCellBorderWidth, border.StrokeWidth);
    }

    [Fact]
    public void CurrentCell_NoBorderWithoutACurrentCell()
    {
        var h = Create();

        Assert.DoesNotContain(h.Render().Ops, o => o.Color == h.Style.CurrentCellBorderColor);
    }

    [Fact]
    public void CurrentCell_NoBorderOnAGroupHeaderRow()
    {
        var h = Create();
        h.Source.AddGroupDescription(new GroupDescription("City"));
        Assert.True(h.Source.IsGroupHeaderRow(0));
        h.Selection.CurrentCell = new CellPosition(0, 0);

        Assert.DoesNotContain(h.Render().Ops, o => o.Kind == DrawOpKind.DrawRect && o.Color == h.Style.CurrentCellBorderColor);
    }

    // ── Sort indicators ───────────────────────────────────────────

    [Theory]
    [InlineData(1, 17f)] // ascending: apex at the top
    [InlineData(2, 23f)] // descending: apex at the bottom
    public void SortIndicator_IsATriangleAtTheHeaderRight_PointingInTheSortDirection(int toggles, float apexY)
    {
        var h = Create();
        for (int i = 0; i < toggles; i++) h.Source.ToggleSort(h.Column("Name"));

        var arrow = h.Render().Lines.Where(l => l.Color == h.Style.SortIndicatorColor).ToList();

        Assert.Equal(3, arrow.Count);
        // Name is 150 px wide: arrow spans 134–142 (width − padding − 8), centred on the 40 px header.
        Assert.Equal(134, arrow.Min(l => l.Rect.Left));
        Assert.Equal(142, arrow.Max(l => l.Rect.Right));
        var apex = arrow.SelectMany(l => new[] { (l.X1, l.Y1), (l.X2, l.Y2) }).Where(p => Near(p.Item1, 138f)).ToList();
        Assert.NotEmpty(apex);
        Assert.All(apex, p => Assert.Equal(apexY, p.Item2));
    }

    [Fact]
    public void SortIndicator_NotDrawnForUnsortedColumns()
    {
        var h = Create();

        Assert.DoesNotContain(h.Render().Lines, l => l.Color == h.Style.SortIndicatorColor);
    }

    [Fact]
    public void SortIndicator_MultiSort_DrawsOrderBadges()
    {
        var h = Create();
        h.Column("City").SortDirection = DataGridSortDirection.Ascending;
        h.Column("City").SortOrder = 1;
        h.Column("Age").SortDirection = DataGridSortDirection.Descending;
        h.Column("Age").SortOrder = 2;

        var ctx = h.Render();

        var badges = ctx.Ops.Where(o => o.Kind == DrawOpKind.Text && o.Color == h.Style.SortIndicatorColor).ToList();
        Assert.Equal(new[] { "1", "2" }, badges.Select(b => b.Text).OrderBy(t => t));
        Assert.Equal(6, ctx.Lines.Count(l => l.Color == h.Style.SortIndicatorColor));
    }

    // ── Filter icons ──────────────────────────────────────────────

    [Fact]
    public void FilterIcon_DrawnOnlyForFilterableColumns()
    {
        var h = Create();
        h.Column("Age").AllowFiltering = false;

        var funnel = h.Render().Lines.Where(l => l.Color == h.Style.FilterIconColor).ToList();

        // Six strokes per funnel, three filterable columns; none inside Age (150–250).
        Assert.Equal(18, funnel.Count);
        Assert.DoesNotContain(funnel, l => l.Rect.Left >= 150 && l.Rect.Right <= 250);
        // Name's funnel sits left of the sort-arrow slot: 150 − 8 − 12 − 10 = 120.
        Assert.Equal(120, funnel.Where(l => l.Rect.Right <= 150).Min(l => l.Rect.Left));
    }

    [Fact]
    public void FilterIcon_ActiveFilter_UsesTheActiveColourAndAFilledDot()
    {
        var h = Create();
        var city = h.Column("City");
        h.Source.SetColumnFilter(city, new FilterDescription(city) { SelectedValues = new HashSet<string> { "Oslo" } });

        var ctx = h.Render();

        Assert.Equal(6, ctx.Lines.Count(l => l.Color == h.Style.FilterActiveIconColor));
        var dot = Assert.Single(ctx.Fills, f => f.Color == h.Style.FilterActiveIconColor);
        Assert.Equal(5, dot.Rect.Width);
        Assert.True(dot.Rect.Left >= 250 && dot.Rect.Right <= 400);
    }

    private sealed class CapturingCellRenderer : ICellRenderer
    {
        public List<(GridRect Rect, object? Value, string Text)> Calls { get; } = new();

        public void Render(IDrawingContext ctx, GridRect cellRect, object? value, string displayText,
            DataGridColumn column, DataGridStyle style, bool isSelected, CellStyle? cellStyle = null) =>
            Calls.Add((cellRect, value, displayText));
    }
}

internal static class RecordingAssertions
{
    /// <summary>The single text op with this text whose rectangle lies in <paramref name="row"/>.</summary>
    public static DrawOp SingleText(this RecordingDrawingContext ctx, string text, int row, RenderHarness h) =>
        Assert.Single(ctx.TextsEqual(text), o => RenderHarness.Near(o.Rect.Y, h.RowTop(row)));
}

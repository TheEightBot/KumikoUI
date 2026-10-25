using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests;

/// <summary>
/// Behaviour of <see cref="KumikoStyle.PinBottomSummaryRows"/>: the bottom table summary is a fixed
/// footer at the viewport bottom that hit testing, keyboard navigation and z-order all respect.
/// </summary>
public class PinnedBottomSummaryTests
{
    private const float ViewportWidth = 400f;
    private const float DefaultViewportHeight = 400f;

    private sealed class Person
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
    }

    private sealed class Grid
    {
        public KumikoSource Source { get; } = new();
        public KumikoStyle Style { get; } = new();
        public ScrollState Scroll { get; init; } = new() { ViewportWidth = ViewportWidth, ViewportHeight = DefaultViewportHeight };
        public KumikoSelectionModel Selection { get; } = new();
        public KumikoInputController Input { get; } = new();
        public KumikoRenderer Renderer { get; } = new();

        public float ViewportHeight => Scroll.ViewportHeight;

        /// <summary>Viewport Y of the pinned footer's top edge (anchored to the viewport bottom).</summary>
        public float FooterTop => ViewportHeight - Source.BottomSummaryCount * Style.SummaryRowHeight;

        public RecordingDrawingContext Render()
        {
            var ctx = new RecordingDrawingContext();
            Renderer.Render(ctx, Source, Scroll, Selection, Style);
            return ctx;
        }

        public void Key(KumikoKey key) =>
            Input.HandleKey(new KumikoKeyEventArgs { Key = key }, Scroll, Selection, Style, Source);

        public KumikoHitTestResult HitTest(float x, float y) =>
            new KumikoHitTester().HitTest(x, y, Scroll, Style, Source.Columns, Source.RowCount, Source);
    }

    private static Grid CreateGrid(int rowCount = 100, bool pinned = true, int frozenRows = 0, bool grouped = false,
        float viewportHeight = DefaultViewportHeight)
    {
        var grid = new Grid { Scroll = new ScrollState { ViewportWidth = ViewportWidth, ViewportHeight = viewportHeight } };
        grid.Style.PinBottomSummaryRows = pinned;
        grid.Source.SetColumns(new[]
        {
            new KumikoColumn { Header = "Name", PropertyName = "Name", Width = 200 },
            new KumikoColumn { Header = "Age", PropertyName = "Age", Width = 100, ColumnType = KumikoColumnType.Numeric },
        });
        grid.Source.SetItems(Enumerable.Range(0, rowCount)
            .Select(i => new Person { Name = $"P{i:D3}", Age = i % 7 })
            .ToList());
        grid.Source.AddTableSummaryRow(new TableSummaryRow
        {
            Name = "Totals",
            Position = SummaryPosition.Bottom,
            Columns = { new SummaryColumnDescription { PropertyName = "Age", SummaryType = SummaryType.Sum } },
        });
        if (frozenRows > 0) grid.Source.FrozenRowCount = frozenRows;
        if (grouped) grid.Source.AddGroupDescription(new GroupDescription("Age"));
        grid.Render(); // establishes content size and column widths, like the first frame on a host
        return grid;
    }

    // ── Hit testing ───────────────────────────────────────────────

    [Fact]
    public void HitTest_TapOnPinnedFooter_ReturnsTableSummaryRow()
    {
        var grid = CreateGrid();

        var hit = grid.HitTest(50, grid.FooterTop + 10);

        Assert.Equal(HitRegion.TableSummaryRow, hit.Region);
        Assert.Equal(-1, hit.RowIndex);
    }

    [Fact]
    public void HitTest_TapOnPinnedFooter_WhileScrolled_ReturnsTableSummaryRow()
    {
        var grid = CreateGrid();
        grid.Scroll.OffsetY = 1000;
        grid.Scroll.ClampOffset();

        Assert.Equal(HitRegion.TableSummaryRow, grid.HitTest(50, grid.ViewportHeight - 1).Region);
    }

    [Fact]
    public void HitTest_JustAboveFooter_ReturnsDataCell()
    {
        var grid = CreateGrid();

        var hit = grid.HitTest(50, grid.FooterTop - 1);

        Assert.Equal(HitRegion.Cell, hit.Region);
        Assert.True(hit.RowIndex >= 0);
    }

    [Fact]
    public void HitTest_FooterBandWithGroupingPanel_ReturnsTableSummaryRow()
    {
        var grid = CreateGrid(grouped: true);

        // The footer is anchored to the viewport bottom regardless of the grouping panel above the header.
        Assert.Equal(HitRegion.TableSummaryRow, grid.HitTest(50, grid.ViewportHeight - 5).Region);
    }

    [Fact]
    public void HitTest_ShortViewport_FooterOverlappingHeader_ReturnsTableSummaryRow()
    {
        // 60 px viewport: header 0–40, footer 24–60 drawn over the header's lower part.
        var grid = CreateGrid(viewportHeight: 60);
        Assert.True(grid.FooterTop < grid.Style.HeaderHeight);

        Assert.Equal(HitRegion.TableSummaryRow, grid.HitTest(50, grid.FooterTop + 6).Region);
        Assert.Equal(HitRegion.HeaderCell, grid.HitTest(50, grid.FooterTop - 4).Region);
    }

    [Fact]
    public void Tap_ShortViewport_FooterOverHeader_DoesNotSort()
    {
        var grid = CreateGrid(viewportHeight: 60);
        float y = grid.FooterTop + 6;

        grid.Input.HandlePointer(new KumikoPointerEventArgs { X = 50, Y = y, Action = InputAction.Pressed },
            grid.Scroll, grid.Selection, grid.Style, grid.Source);
        grid.Input.HandlePointer(new KumikoPointerEventArgs { X = 50, Y = y, Action = InputAction.Released },
            grid.Scroll, grid.Selection, grid.Style, grid.Source);

        Assert.Empty(grid.Source.ActiveSortColumns);
        Assert.Equal("P000", grid.Source.GetCellDisplayText(0, grid.Source.Columns[0]));
    }

    [Fact]
    public void HitTest_ShortViewport_FooterOverlappingGroupingPanel_ReturnsTableSummaryRow()
    {
        // Grouping panel occupies the top band; a viewport shorter than panel + footer makes them overlap.
        var grid = CreateGrid(grouped: true, viewportHeight: 50);
        Assert.True(grid.FooterTop < grid.Style.GroupPanelHeight);

        Assert.Equal(HitRegion.TableSummaryRow, grid.HitTest(50, grid.FooterTop + 2).Region);
    }

    [Fact]
    public void HitTest_Unpinned_BottomOfViewportIsADataRow()
    {
        var grid = CreateGrid(pinned: false);

        var hit = grid.HitTest(50, grid.ViewportHeight - 5);

        Assert.Equal(HitRegion.Cell, hit.Region);
    }

    [Fact]
    public void HitTest_Unpinned_SummaryAfterLastRow_ReturnsTableSummaryRow()
    {
        var grid = CreateGrid(rowCount: 3, pinned: false);
        float summaryTop = grid.Style.HeaderHeight + 3 * grid.Style.RowHeight;

        Assert.Equal(HitRegion.TableSummaryRow, grid.HitTest(50, summaryTop + 5).Region);
    }

    [Fact]
    public void Tap_OnPinnedFooter_SelectsNothing()
    {
        var grid = CreateGrid();
        float y = grid.FooterTop + 10;

        grid.Input.HandlePointer(new KumikoPointerEventArgs { X = 50, Y = y, Action = InputAction.Pressed },
            grid.Scroll, grid.Selection, grid.Style, grid.Source);
        grid.Input.HandlePointer(new KumikoPointerEventArgs { X = 50, Y = y, Action = InputAction.Released },
            grid.Scroll, grid.Selection, grid.Style, grid.Source);

        Assert.Empty(grid.Selection.SelectedRows);
        Assert.False(grid.Selection.CurrentCell.IsValid);
    }

    // ── Keyboard ensure-visible ───────────────────────────────────

    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 2)]
    [InlineData(true, 0)]
    public void ArrowDown_KeepsCurrentRowFullyAboveFooter(bool grouped, int frozenRows)
    {
        var grid = CreateGrid(frozenRows: frozenRows, grouped: grouped);
        grid.Selection.CurrentCell = new CellPosition(0, 0);
        float groupPanel = grouped ? grid.Style.GroupPanelHeight : 0;
        float scrollableTop = groupPanel + grid.Style.HeaderHeight + frozenRows * grid.Style.RowHeight;

        for (int i = 0; i < 40; i++)
        {
            grid.Key(KumikoKey.Down);
            grid.Render();

            int row = grid.Selection.CurrentCell.Row;
            if (row < frozenRows) continue;
            float rowTop = scrollableTop + (row - frozenRows) * grid.Style.RowHeight - grid.Scroll.OffsetY;
            float rowBottom = rowTop + grid.Style.RowHeight;

            Assert.True(rowTop >= scrollableTop - 0.01f, $"row {row} top {rowTop} hidden under the header/frozen band");
            Assert.True(rowBottom <= grid.FooterTop + 0.01f, $"row {row} bottom {rowBottom} hidden behind footer at {grid.FooterTop}");
        }
    }

    [Fact]
    public void CtrlEnd_LastRowEndsExactlyAtFooterTop()
    {
        var grid = CreateGrid();
        grid.Selection.CurrentCell = new CellPosition(0, 0);

        grid.Input.HandleKey(new KumikoKeyEventArgs { Key = KumikoKey.End, Modifiers = InputModifiers.Control },
            grid.Scroll, grid.Selection, grid.Style, grid.Source);
        grid.Render();

        Assert.Equal(99, grid.Selection.CurrentCell.Row);
        float lastRowBottom = grid.Style.HeaderHeight + 100 * grid.Style.RowHeight - grid.Scroll.OffsetY;
        Assert.Equal(grid.FooterTop, lastRowBottom, 2);
        Assert.Equal(grid.Scroll.MaxOffsetY, grid.Scroll.OffsetY, 2);
    }

    [Fact]
    public void ArrowUp_AfterScrollingToEnd_ScrollsBackIntoView()
    {
        var grid = CreateGrid();
        grid.Selection.CurrentCell = new CellPosition(99, 0);
        grid.Scroll.OffsetY = grid.Scroll.MaxOffsetY;

        grid.Selection.CurrentCell = new CellPosition(5, 0);
        grid.Key(KumikoKey.Up);

        Assert.Equal(4, grid.Selection.CurrentCell.Row);
        Assert.Equal(4 * grid.Style.RowHeight, grid.Scroll.OffsetY, 2);
    }

    [Fact]
    public void PageDown_WithSeveralPinnedSummaries_NeverSkipsUnseenRows()
    {
        // 400 px viewport, 40 px header, five 36 px pinned summaries (180 px) → 180 px of rows = 5 rows.
        var grid = CreateGrid();
        for (int i = 2; i <= 5; i++)
        {
            grid.Source.AddTableSummaryRow(new TableSummaryRow
            {
                Name = $"Totals{i}",
                Position = SummaryPosition.Bottom,
                Columns = { new SummaryColumnDescription { PropertyName = "Age", SummaryType = SummaryType.Count } },
            });
        }
        grid.Render();
        grid.Selection.CurrentCell = new CellPosition(0, 0);
        int rowsPerPage = (int)((grid.FooterTop - grid.Style.HeaderHeight) / grid.Style.RowHeight);
        Assert.Equal(5, rowsPerPage);

        for (int press = 1; press <= 6; press++)
        {
            int before = grid.Selection.CurrentCell.Row;
            grid.Key(KumikoKey.PageDown);
            grid.Render();

            int row = grid.Selection.CurrentCell.Row;
            Assert.True(row - before <= rowsPerPage, $"PageDown jumped {row - before} rows; only {rowsPerPage} fit");
            float rowBottom = grid.Style.HeaderHeight + (row + 1) * grid.Style.RowHeight - grid.Scroll.OffsetY;
            Assert.True(rowBottom <= grid.FooterTop + 0.01f, $"row {row} hidden behind the footer");
        }
    }

    // ── Editing next to the footer ────────────────────────────────

    [Fact]
    public void DoubleTap_OnRowStraddlingFooter_ScrollsItAboveFooterBeforeEditing()
    {
        var grid = CreateGrid();
        grid.Input.EditSession = new KumikoUI.Core.Editing.EditSession { Style = grid.Style };

        // Default geometry: rows start at 40 px, 36 px tall; the footer starts at 364 px. Scrolled by half
        // a row, row 9 spans 346–382 and straddles the footer edge; tap its visible part (358 px).
        grid.Scroll.OffsetY = 18;
        float y = grid.FooterTop - 6;
        foreach (var clicks in new[] { 1, 2 })
        {
            grid.Input.HandlePointer(new KumikoPointerEventArgs { X = 50, Y = y, Action = InputAction.Pressed, ClickCount = clicks },
                grid.Scroll, grid.Selection, grid.Style, grid.Source);
            grid.Input.HandlePointer(new KumikoPointerEventArgs { X = 50, Y = y, Action = InputAction.Released, ClickCount = clicks },
                grid.Scroll, grid.Selection, grid.Style, grid.Source);
        }

        var editor = grid.Input.EditSession.ActiveEditor;
        Assert.True(grid.Input.EditSession.IsEditing);
        Assert.NotNull(editor);
        Assert.Equal(9, grid.Selection.CurrentCell.Row);
        Assert.True(editor!.Bounds.Bottom <= grid.FooterTop + 0.01f,
            $"editor bottom {editor.Bounds.Bottom} overlaps the footer at {grid.FooterTop}");
    }

    // ── Z-order ───────────────────────────────────────────────────

    [Fact]
    public void Render_PinnedFooter_IsDrawnAfterDragHandleGrips()
    {
        var grid = CreateGrid();
        grid.Style.ShowRowDragHandle = true;
        grid.Style.RowDragHandlePosition = DragHandlePosition.Left;

        var fills = grid.Render().Fills.ToList();

        int footerIndex = fills.FindLastIndex(f =>
            f.Color == grid.Style.SummaryRowBackgroundColor &&
            Math.Abs(f.Rect.Y - grid.FooterTop) < 0.01f &&
            f.Rect.Width >= ViewportWidth);
        int lastGripIndex = fills.FindLastIndex(f =>
            f.Kind == DrawOpKind.FillRoundRect && f.Color == grid.Style.RowDragHandleColor);

        Assert.True(footerIndex >= 0, "pinned footer background was not drawn");
        Assert.True(lastGripIndex >= 0, "drag handle grips were not drawn");
        Assert.True(footerIndex > lastGripIndex, "drag handle grips are painted over the pinned footer");
    }

    [Fact]
    public void Render_Unpinned_SummaryIsDrawnAfterLastRow()
    {
        var grid = CreateGrid(rowCount: 3, pinned: false);
        float expectedY = grid.Style.HeaderHeight + 3 * grid.Style.RowHeight;

        var fills = grid.Render().Fills.ToList();

        Assert.Contains(fills, f => f.Color == grid.Style.SummaryRowBackgroundColor && Math.Abs(f.Rect.Y - expectedY) < 0.01f);
        Assert.DoesNotContain(fills, f => f.Color == grid.Style.SummaryRowBackgroundColor && Math.Abs(f.Rect.Y - grid.FooterTop) < 0.01f);
    }
}

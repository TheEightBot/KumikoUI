using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;
using static KumikoUI.Core.Tests.TestHelpers.RenderHarness;

namespace KumikoUI.Core.Tests.Rendering;

/// <summary>
/// The selection column's drawing (#321): a box per row that follows the selection, a tri-state check-all box in the
/// header, no text, and the dimmed boxes of the None mode.
/// </summary>
/// <remarks>
/// Geometry: the 44 px selection column is pinned left (x 0-44); header 40 px, rows 36 px. The check box is 16 px,
/// centered in its cell.
/// </remarks>
public class SelectionColumnRenderingTests
{
    private static (RenderHarness H, RecordingIconProvider Icons) Grid(KumikoSelectionMode mode = KumikoSelectionMode.Extended)
    {
        var icons = new RecordingIconProvider();
        var h = Create(rows: 10, height: 700, configure: x =>
        {
            var columns = x.Source.Columns.ToList();
            columns.Insert(0, new KumikoSelectionColumn());
            x.Source.SetColumns(columns);
            x.Selection.Mode = mode;
        });
        h.Style.IconProvider = icons;
        return (h, icons);
    }

    private static bool InSelectionColumn(KumikoRect r) => r.X >= 0 && r.Right <= 44.01f;

    private static IEnumerable<IconCall> HeaderIcons(RecordingIconProvider icons, float headerHeight) =>
        icons.Calls.Where(c => InSelectionColumn(c.Bounds) && c.Bounds.Bottom <= headerHeight);

    private static IEnumerable<IconCall> RowIcons(RecordingIconProvider icons, float headerHeight) =>
        icons.Calls.Where(c => InSelectionColumn(c.Bounds) && c.Bounds.Y >= headerHeight);

    // ── The header box ──

    [Fact]
    public void Header_NothingSelected_DrawsAnEmptyBox_Centered()
    {
        var (h, icons) = Grid();

        var ctx = h.Render();

        Assert.Empty(HeaderIcons(icons, h.Style.HeaderHeight));
        var box = Assert.Single(ctx.Ops, o => o.Kind == DrawOpKind.DrawRect && InSelectionColumn(o.Rect) && o.Rect.Bottom <= h.Style.HeaderHeight);
        Assert.Equal(16f, box.Rect.Width);
        Assert.Equal(22f - 8f, box.Rect.X, 2);
        Assert.Equal((h.Style.HeaderHeight - 16f) / 2, box.Rect.Y, 2);
    }

    [Fact]
    public void Header_SomeSelected_DrawsTheIndeterminateMark()
    {
        var (h, icons) = Grid();
        h.Selection.HandleCheckBoxToggle(2, false);

        h.Render();

        var icon = Assert.Single(HeaderIcons(icons, h.Style.HeaderHeight));
        Assert.Equal(KumikoIcon.Indeterminate, icon.Icon);
        Assert.Equal(h.Style.AccentColor, icon.Color);
    }

    [Fact]
    public void Header_AllSelected_DrawsTheCheckMark()
    {
        var (h, icons) = Grid();
        h.Selection.SelectAll();

        h.Render();

        var icon = Assert.Single(HeaderIcons(icons, h.Style.HeaderHeight));
        Assert.Equal(KumikoIcon.Check, icon.Icon);
    }

    [Theory]
    [InlineData(KumikoSelectionMode.Single)]
    [InlineData(KumikoSelectionMode.None)]
    public void Header_InSingleAndNoneMode_DrawsNoBox(KumikoSelectionMode mode)
    {
        var (h, icons) = Grid(mode);

        var ctx = h.Render();

        Assert.Empty(HeaderIcons(icons, h.Style.HeaderHeight));
        Assert.DoesNotContain(ctx.Ops, o => o.Kind == DrawOpKind.DrawRect && InSelectionColumn(o.Rect) && o.Rect.Bottom <= h.Style.HeaderHeight);
    }

    [Fact]
    public void Header_DrawsNoTextSortGlyphFilterIconOrMenuButton()
    {
        var (h, icons) = Grid();
        h.Renderer.ShowColumnMenuButton = true;
        h.Selection.SelectAll();

        var ctx = h.Render();

        Assert.DoesNotContain(ctx.Texts, o => InSelectionColumn(o.Rect));
        Assert.DoesNotContain(icons.Calls, c => InSelectionColumn(c.Bounds) &&
            c.Icon is KumikoIcon.Filter or KumikoIcon.FilterActive or KumikoIcon.SortAscending or KumikoIcon.SortDescending
                or KumikoIcon.ColumnMenu);
        // The data columns keep theirs: one menu button per data column
        Assert.Equal(4, icons.Calls.Count(c => c.Icon == KumikoIcon.ColumnMenu));
    }

    // ── The row boxes ──

    [Fact]
    public void RowBoxes_FollowTheSelection()
    {
        var (h, icons) = Grid();
        h.Selection.HandleCheckBoxToggle(1, false);
        h.Selection.HandleCheckBoxToggle(3, false);

        var ctx = h.Render();

        var checks = RowIcons(icons, h.Style.HeaderHeight).ToList();
        Assert.Equal(2, checks.Count);
        Assert.All(checks, c => Assert.Equal(KumikoIcon.Check, c.Icon));
        Assert.Equal(new[] { 1, 3 }, checks.Select(c => (int)((c.Bounds.Y - h.Style.HeaderHeight) / h.Style.RowHeight)).Order());
        // One border per row, drawn on the cell
        var boxes = ctx.Ops.Where(o => o.Kind == DrawOpKind.DrawRect && InSelectionColumn(o.Rect) && o.Rect.Width == 16f && o.Rect.Y >= h.Style.HeaderHeight).ToList();
        Assert.Equal(10, boxes.Count);
        Assert.All(boxes, b => Assert.Equal(h.Style.CheckBoxBorderColor ?? h.Style.GridLineColor, b.Color));
    }

    [Fact]
    public void RowCells_DrawNoText()
    {
        var (h, _) = Grid();

        var ctx = h.Render();

        Assert.DoesNotContain(ctx.Texts, o => InSelectionColumn(o.Rect) && o.Rect.Y >= h.Style.HeaderHeight);
        Assert.Contains(ctx.Texts, o => o.Text == "P00"); // the data columns still draw theirs
    }

    [Fact]
    public void NoneMode_DrawsDimmedBoxes()
    {
        var (h, _) = Grid(KumikoSelectionMode.None);

        var ctx = h.Render();

        var boxes = ctx.Ops.Where(o => o.Kind == DrawOpKind.DrawRect && InSelectionColumn(o.Rect) && o.Rect.Width == 16f && o.Rect.Y >= h.Style.HeaderHeight).ToList();
        Assert.Equal(10, boxes.Count);
        Assert.All(boxes, b => Assert.True(b.Color.A < 255, "a box that cannot be used is dimmed"));
    }

    [Fact]
    public void ABoxOnAGroupedGrid_IsOnlyDrawnForDataRows()
    {
        var (h, icons) = Grid();
        h.Source.AddGroupDescription(new GroupDescription("City"));
        h.Selection.SelectAll();

        h.Render();

        Assert.Equal(10, RowIcons(icons, h.Style.HeaderHeight).Count(c => c.Icon == KumikoIcon.Check));
    }

    [Fact]
    public void ACustomRenderer_ForTheSelectionType_ReplacesTheBuiltInOne()
    {
        var (h, _) = Grid();
        var probe = new CountingRenderer();
        h.Renderer.SetCellRenderer(KumikoColumnType.Selection, probe);

        h.Render();

        Assert.Equal(10, probe.Calls);
    }

    private sealed class CountingRenderer : ICellRenderer
    {
        public int Calls { get; private set; }
        public void Render(IDrawingContext ctx, in CellRenderContext cell) => Calls++;
    }
}

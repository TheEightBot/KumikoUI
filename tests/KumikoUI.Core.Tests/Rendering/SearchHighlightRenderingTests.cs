using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.Rendering;

/// <summary>
/// The grid draws a rounded fill behind the part of a cell's text that matches the grid-wide search (#317): placed by
/// the text's alignment, measured through the drawing context, in the search highlight color (the current match in
/// its own color), in every pane, and never for a column the search leaves out.
/// </summary>
/// <remarks>
/// <see cref="RecordingDrawingContext"/> measures 8 px per character with text 14 px high; cells are padded 8 px and
/// rows are 36 px tall below a 40 px header, so a single line's highlight runs from 1 px above the centered text to
/// 1 px below it.
/// </remarks>
public class SearchHighlightRenderingTests
{
    private const float CharWidth = RecordingDrawingContext.CharWidth;

    private static IEnumerable<DrawOp> Highlights(RecordingDrawingContext ctx, KumikoStyle style) =>
        ctx.Ops.Where(o => o.Kind == DrawOpKind.FillRoundRect &&
                           (o.Color == style.SearchHighlightColor || o.Color == style.SearchCurrentHighlightColor));

    /// <summary>The text call that drew <paramref name="text"/> in the row whose top is <paramref name="rowTop"/>.</summary>
    private static DrawOp TextAt(RecordingDrawingContext ctx, string text, float rowTop) =>
        ctx.Ops.Single(o => o.Kind == DrawOpKind.TextInRect && o.Text == text && RenderHarness.Near(o.Rect.Y, rowTop));

    [Fact]
    public void ALeftAlignedMatch_IsHighlightedBehindTheMatchingCharacters()
    {
        var grid = RenderHarness.Create();
        grid.Source.SearchText = "sl"; // "Oslo" in the City column of every even row

        var ctx = grid.Render();

        float rowTop = grid.RowTop(0);
        var text = TextAt(ctx, "Oslo", rowTop);
        var highlight = Highlights(ctx, grid.Style).First(o => RenderHarness.Near(o.Rect.Y, rowTop + 10));
        Assert.Equal(text.Rect.X + CharWidth, highlight.Rect.X, 2);
        Assert.Equal(2 * CharWidth, highlight.Rect.Width, 2);
        Assert.Equal(rowTop + (36 - 14) / 2f - 1, highlight.Rect.Y, 2);
        Assert.Equal(16, highlight.Rect.Height, 2);
        Assert.Equal(grid.Style.SearchHighlightColor, highlight.Color);
        Assert.True(highlight.Index < text.Index, "the highlight is drawn under the text");
        Assert.True(Highlights(ctx, grid.Style).Count() >= 5, "every Oslo row on screen (the even rows)");
    }

    [Fact]
    public void ARightAlignedMatch_FollowsTheText()
    {
        var grid = RenderHarness.Create(rows: 2);
        grid.Column("City").TextAlignment = KumikoTextAlignment.Right;
        grid.Source.SearchText = "o"; // "Oslo": the first "O"

        var ctx = grid.Render();

        float rowTop = grid.RowTop(0);
        var text = TextAt(ctx, "Oslo", rowTop);
        var highlight = Highlights(ctx, grid.Style).Single(o => RenderHarness.Near(o.Rect.Y, rowTop + 10));
        Assert.Equal(text.Rect.Right - 4 * CharWidth, highlight.Rect.X, 2);
        Assert.Equal(CharWidth, highlight.Rect.Width, 2);
    }

    [Fact]
    public void Highlights_AreDrawnInFrozenAndScrollingPanes()
    {
        var grid = RenderHarness.Create(rows: 4, width: 400);
        grid.Column("Name").IsFrozen = true;
        grid.Scroll.OffsetX = 100;
        grid.Source.SearchText = "P01";

        var frozen = grid.Render();
        float rowTop = grid.RowTop(1);
        var nameText = TextAt(frozen, "P01", rowTop);
        var nameHighlight = Highlights(frozen, grid.Style).Single();
        Assert.Equal(8, nameText.Rect.X, 2); // the frozen pane does not scroll
        Assert.Equal(nameText.Rect.X, nameHighlight.Rect.X, 2);
        Assert.Equal(3 * CharWidth, nameHighlight.Rect.Width, 2);

        grid.Source.SearchText = "Rome";
        var scrolled = grid.Render();
        var cityText = TextAt(scrolled, "Rome", rowTop);
        var cityHighlight = Highlights(scrolled, grid.Style).First(o => RenderHarness.Near(o.Rect.Y, rowTop + 10));
        Assert.Equal(cityText.Rect.X, cityHighlight.Rect.X, 2);
        Assert.True(cityHighlight.Rect.X < 250, "the scrolling pane moved with the offset");
    }

    [Fact]
    public void TheCurrentMatch_UsesTheCurrentHighlightColor()
    {
        var grid = RenderHarness.Create(rows: 4);
        grid.Source.SearchText = "Rome";
        grid.Selection.CurrentCell = new CellPosition(1, 2); // row 1, City

        var ctx = grid.Render();

        var highlights = Highlights(ctx, grid.Style).ToList();
        Assert.Equal(2, highlights.Count);
        var current = highlights.Single(o => RenderHarness.Near(o.Rect.Y, grid.RowTop(1) + 10));
        Assert.Equal(grid.Style.SearchCurrentHighlightColor, current.Color);
        Assert.Equal(grid.Style.SearchHighlightColor, highlights.Single(o => o != current).Color);
    }

    [Fact]
    public void AMatchCutOffByTheEllipsis_HighlightsTheEllipsis()
    {
        var grid = RenderHarness.Create(rows: 1, configure: h => h.People[0].Name = "Abcdefghijklmnopqrstuvwxyz");
        grid.Source.SearchText = "xyz";

        var ctx = grid.Render();

        var highlight = Highlights(ctx, grid.Style).Single();
        float cellRight = 150 - 8;
        Assert.Equal(cellRight - CharWidth, highlight.Rect.X, 2); // the "…" at the end of the cut line
        Assert.Equal(cellRight, highlight.Rect.Right, 2);
    }

    [Fact]
    public void ColumnsTheSearchLeavesOut_AndTheNoSearchCase_DrawNoHighlight()
    {
        var grid = RenderHarness.Create(rows: 4);
        Assert.Empty(Highlights(grid.Render(), grid.Style));

        grid.Column("City").AllowSearching = false;
        grid.Source.SearchText = "Oslo";
        Assert.Empty(Highlights(grid.Render(), grid.Style));
    }

    [Fact]
    public void ACustomRenderer_ReceivesTheMatchInItsContext()
    {
        var grid = RenderHarness.Create(rows: 2);
        var probe = new ProbeRenderer();
        grid.Column("City").CustomCellRenderer = probe;
        grid.Source.SearchText = "ome";

        grid.Render();

        Assert.Contains(probe.Matches, m => m == ("Rome", 1, 3));
        Assert.Contains(probe.Matches, m => m == ("Oslo", 0, 0));
    }

    private sealed class ProbeRenderer : ICellRenderer
    {
        public List<(string Text, int Start, int Length)> Matches { get; } = new();

        public void Render(IDrawingContext ctx, in CellRenderContext cell) =>
            Matches.Add((cell.DisplayText, cell.SearchMatchStart, cell.SearchMatchLength));
    }
}

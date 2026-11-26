using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests;

/// <summary>
/// A column's <see cref="KumikoColumn.HeaderCellStyle"/> <see cref="KumikoCellStyle.TextAlignment"/> aligns the header
/// text within the space left after the sort and filter glyphs; the default stays left, and the column drag ghost
/// aligns the same way.
/// </summary>
public class HeaderTextAlignmentTests
{
    private sealed class Row { public int Amount { get; set; } public string Name { get; set; } = ""; }

    private static (RecordingDrawingContext Ctx, KumikoColumn Col, KumikoStyle Style) Render(
        KumikoTextAlignment? alignment, bool sorted = false, int dragIndex = -1, bool frozen = false)
    {
        var source = new KumikoSource();
        var col = new KumikoColumn { Header = "Amount", PropertyName = "Amount", Width = 200, IsFrozen = frozen };
        if (alignment.HasValue) col.HeaderCellStyle = new KumikoCellStyle { TextAlignment = alignment };
        if (sorted) col.SortDirection = KumikoSortDirection.Ascending;
        source.SetColumns(new[] { col, new KumikoColumn { Header = "Name", PropertyName = "Name", Width = 100 } });
        source.SetItems(new List<Row> { new() { Amount = 1, Name = "a" } });
        var style = new KumikoStyle();
        var scroll = new ScrollState { ViewportWidth = 600, ViewportHeight = 300 };
        var ctx = new RecordingDrawingContext();
        new KumikoRenderer().Render(ctx, source, scroll, new KumikoSelectionModel(source), style,
            dragColumnIndex: dragIndex, dragColumnScreenX: 50);
        return (ctx, col, style);
    }

    [Theory]
    [InlineData(null, KumikoTextAlignment.Left)]
    [InlineData(KumikoTextAlignment.Left, KumikoTextAlignment.Left)]
    [InlineData(KumikoTextAlignment.Center, KumikoTextAlignment.Center)]
    [InlineData(KumikoTextAlignment.Right, KumikoTextAlignment.Right)]
    public void Header_text_uses_the_header_style_alignment(KumikoTextAlignment? set, KumikoTextAlignment expected)
    {
        var (ctx, _, _) = Render(set);
        Assert.Equal(expected, ctx.SingleText("Amount").HAlign);
    }

    [Fact]
    public void Aligned_header_text_rect_stays_clear_of_the_sort_glyph()
    {
        var (plain, _, _) = Render(KumikoTextAlignment.Right, sorted: false);
        var (sorted, _, _) = Render(KumikoTextAlignment.Right, sorted: true);
        var a = plain.SingleText("Amount");
        var b = sorted.SingleText("Amount");
        Assert.Equal(KumikoTextAlignment.Right, b.HAlign);
        Assert.Equal(a.Rect.X, b.Rect.X);
        Assert.True(b.Rect.Width < a.Rect.Width);
    }

    [Fact]
    public void Column_drag_ghost_aligns_the_header_text_like_the_header()
    {
        var (ctx, _, _) = Render(KumikoTextAlignment.Right, dragIndex: 0);
        var texts = ctx.TextsEqual("Amount").ToList();
        Assert.Equal(2, texts.Count);
        Assert.All(texts, t => Assert.Equal(KumikoTextAlignment.Right, t.HAlign));
    }

    [Fact]
    public void Frozen_header_uses_the_same_alignment()
    {
        var (ctx, _, _) = Render(KumikoTextAlignment.Center, frozen: true);
        Assert.Equal(KumikoTextAlignment.Center, ctx.SingleText("Amount").HAlign);
    }
}

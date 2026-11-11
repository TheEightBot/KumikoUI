using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.Rendering;

/// <summary>
/// Text cells draw one line by default and wrapped lines when the column asks for them (#23). The recording
/// context does not override the wrapping members, so these also cover the default <see cref="IDrawingContext"/>
/// implementation: one <see cref="DrawOpKind.TextInRect"/> per line, inside a clip to the cell.
/// </summary>
public class TextCellRendererTests
{
    private static readonly KumikoRect Cell = new(10, 20, 80, 60);   // 10 characters wide, 4 lines tall (16 px per line)

    private static RecordingDrawingContext Render(string text, KumikoTextWrap wrap, KumikoRect? bounds = null,
        KumikoTextAlignment alignment = KumikoTextAlignment.Left)
    {
        var ctx = new RecordingDrawingContext();
        var column = new KumikoColumn { PropertyName = "Notes", TextWrap = wrap, TextAlignment = alignment };
        new TextCellRenderer().Render(ctx, bounds ?? Cell, text, text, column, new KumikoStyle(), isSelected: false);
        return ctx;
    }

    [Fact]
    public void Render_WithoutWrapping_DrawsOneLineInTheCell()
    {
        var ctx = Render("The quick brown fox", KumikoTextWrap.None);

        var op = Assert.Single(ctx.Texts);
        Assert.Equal(Cell, op.Rect);
        Assert.Equal("The quick" + KumikoTextLayout.Ellipsis, RecordingDrawingContext.ShownText(op));
    }

    [Fact]
    public void Render_WordWrap_DrawsOneLineBelowTheOther_CenteredAsABlock()
    {
        var ctx = Render("The quick brown fox", KumikoTextWrap.Word);

        var lines = ctx.Texts.ToList();
        Assert.Equal(new[] { "The quick", "brown fox" }, lines.Select(o => o.Text));
        // Two lines: 14 + 16 = 30 px tall, centered in 60 px.
        Assert.Equal(new KumikoRect(10, 35, 80, 14), lines[0].Rect);
        Assert.Equal(new KumikoRect(10, 51, 80, 14), lines[1].Rect);
        Assert.All(lines, o => Assert.Equal(Cell, o.Clip));
        Assert.All(lines, o => Assert.False(o.ClipsToRect));
        Assert.Equal(0, ctx.SaveDepth);
    }

    [Fact]
    public void Render_LineBreaksInTheValue_StartNewLines()
    {
        var ctx = Render("Line one\nLine two", KumikoTextWrap.Word);

        Assert.Equal(new[] { "Line one", "Line two" }, ctx.Texts.Select(o => o.Text));
    }

    [Fact]
    public void Render_MoreLinesThanTheCellHolds_EndsTheLastWithAnEllipsis()
    {
        var ctx = Render("one two three four five six seven", KumikoTextWrap.Word, new KumikoRect(0, 0, 80, 36));

        // 36 px holds two lines; the second is cut where the width runs out.
        Assert.Equal(new[] { "one two", "three fou" + KumikoTextLayout.Ellipsis }, ctx.Texts.Select(o => o.Text));
    }

    [Fact]
    public void Render_CharacterWrap_KeepsTheAlignmentOfEachLine()
    {
        var ctx = Render("abcdefghijkl", KumikoTextWrap.Character, alignment: KumikoTextAlignment.Right);

        Assert.Equal(new[] { "abcdefghij", "kl" }, ctx.Texts.Select(o => o.Text));
        Assert.All(ctx.Texts, o => Assert.Equal(KumikoTextAlignment.Right, o.HAlign));
    }

    [Fact]
    public void DefaultMeasureText_ReportsTheWrappedSize()
    {
        IDrawingContext ctx = new RecordingDrawingContext();
        var paint = new KumikoPaint();

        var wrapped = ctx.MeasureText("The quick brown fox", paint, 80, KumikoTextWrap.Word);
        var oneLine = ctx.MeasureText("The quick brown fox", paint, float.PositiveInfinity, KumikoTextWrap.None);
        var cut = ctx.MeasureText("The quick brown fox", paint, 80, KumikoTextWrap.None);

        Assert.Equal(new KumikoSize(72, 30), wrapped, SizeComparer.Instance);
        Assert.Equal(new KumikoSize(152, 14), oneLine, SizeComparer.Instance);
        Assert.Equal(new KumikoSize(80, 14), cut, SizeComparer.Instance);   // "The quick" + ellipsis
        Assert.Equal(KumikoSize.Empty, ctx.MeasureText("", paint, 80, KumikoTextWrap.Word), SizeComparer.Instance);
    }

    private sealed class SizeComparer : IEqualityComparer<KumikoSize>
    {
        public static readonly SizeComparer Instance = new();
        public bool Equals(KumikoSize a, KumikoSize b) => a.Width == b.Width && a.Height == b.Height;
        public int GetHashCode(KumikoSize s) => HashCode.Combine(s.Width, s.Height);
    }
}

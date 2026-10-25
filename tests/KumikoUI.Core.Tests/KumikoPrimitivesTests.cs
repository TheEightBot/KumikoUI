using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Tests;

public class KumikoPrimitivesTests
{
    // ── KumikoFont ──────────────────────────────────────────────────

    [Fact]
    public void GridFont_DefaultConstructor_HasDefaults()
    {
        var font = new KumikoFont();
        Assert.Equal("Default", font.Family);
        Assert.Equal(14f, font.Size);
        Assert.False(font.IsBold);
        Assert.False(font.IsItalic);
    }

    [Fact]
    public void GridFont_ParameterizedConstructor_SetsProperties()
    {
        var font = new KumikoFont("Arial", 18f, bold: true, italic: true);
        Assert.Equal("Arial", font.Family);
        Assert.Equal(18f, font.Size);
        Assert.True(font.IsBold);
        Assert.True(font.IsItalic);
    }

    [Fact]
    public void GridFont_BoldOnly_ItalicIsFalse()
    {
        var font = new KumikoFont("Arial", 12f, bold: true);
        Assert.True(font.IsBold);
        Assert.False(font.IsItalic);
    }

    // ── KumikoSize ──────────────────────────────────────────────────

    [Fact]
    public void GridSize_Constructor_SetsProperties()
    {
        var size = new KumikoSize(200f, 100f);
        Assert.Equal(200f, size.Width);
        Assert.Equal(100f, size.Height);
    }

    [Fact]
    public void GridSize_Empty_IsZero()
    {
        var empty = KumikoSize.Empty;
        Assert.Equal(0f, empty.Width);
        Assert.Equal(0f, empty.Height);
    }

    // ── KumikoPoint ─────────────────────────────────────────────────

    [Fact]
    public void GridPoint_Constructor_SetsProperties()
    {
        var pt = new KumikoPoint(3.14f, 2.71f);
        Assert.Equal(3.14f, pt.X);
        Assert.Equal(2.71f, pt.Y);
    }

    // ── KumikoFontMetrics ───────────────────────────────────────────

    [Fact]
    public void GridFontMetrics_Constructor_SetsProperties()
    {
        var metrics = new KumikoFontMetrics(-10f, 3f, 2f);
        Assert.Equal(-10f, metrics.Ascent);
        Assert.Equal(3f, metrics.Descent);
        Assert.Equal(2f, metrics.Leading);
    }

    [Fact]
    public void GridFontMetrics_LineHeight_IsDescentMinusAscentPlusLeading()
    {
        // Descent - Ascent + Leading = 3 - (-10) + 2 = 15
        var metrics = new KumikoFontMetrics(-10f, 3f, 2f);
        Assert.Equal(15f, metrics.LineHeight);
    }

    [Fact]
    public void GridFontMetrics_TextHeight_IsDescentMinusAscent()
    {
        // Descent - Ascent = 3 - (-10) = 13
        var metrics = new KumikoFontMetrics(-10f, 3f, 2f);
        Assert.Equal(13f, metrics.TextHeight);
    }

    [Fact]
    public void GridFontMetrics_ZeroLeading_LineHeightEqualsTextHeight()
    {
        var metrics = new KumikoFontMetrics(-12f, 4f);
        Assert.Equal(metrics.TextHeight, metrics.LineHeight);
    }

    // ── KumikoPaint ─────────────────────────────────────────────────

    [Fact]
    public void GridPaint_DefaultConstructor_HasSensibleDefaults()
    {
        var paint = new KumikoPaint();
        Assert.Equal(KumikoColor.Black, paint.Color);
        Assert.Equal(1f, paint.StrokeWidth);
        Assert.Equal(PaintStyle.Fill, paint.Style);
        Assert.True(paint.IsAntiAlias);
        Assert.Null(paint.Font);
    }

    [Fact]
    public void GridPaint_CanSetProperties()
    {
        var font = new KumikoFont("Helvetica", 12f);
        var paint = new KumikoPaint
        {
            Color = KumikoColor.Red,
            StrokeWidth = 2f,
            Style = PaintStyle.Stroke,
            IsAntiAlias = false,
            Font = font
        };
        Assert.Equal(KumikoColor.Red, paint.Color);
        Assert.Equal(2f, paint.StrokeWidth);
        Assert.Equal(PaintStyle.Stroke, paint.Style);
        Assert.False(paint.IsAntiAlias);
        Assert.Same(font, paint.Font);
    }
}


using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Tests;

public class GridPrimitivesTests
{
    // ── GridFont ──────────────────────────────────────────────────

    [Fact]
    public void GridFont_DefaultConstructor_HasDefaults()
    {
        var font = new GridFont();
        Assert.Equal("Default", font.Family);
        Assert.Equal(14f, font.Size);
        Assert.False(font.IsBold);
        Assert.False(font.IsItalic);
    }

    [Fact]
    public void GridFont_ParameterizedConstructor_SetsProperties()
    {
        var font = new GridFont("Arial", 18f, bold: true, italic: true);
        Assert.Equal("Arial", font.Family);
        Assert.Equal(18f, font.Size);
        Assert.True(font.IsBold);
        Assert.True(font.IsItalic);
    }

    [Fact]
    public void GridFont_BoldOnly_ItalicIsFalse()
    {
        var font = new GridFont("Arial", 12f, bold: true);
        Assert.True(font.IsBold);
        Assert.False(font.IsItalic);
    }

    // ── GridSize ──────────────────────────────────────────────────

    [Fact]
    public void GridSize_Constructor_SetsProperties()
    {
        var size = new GridSize(200f, 100f);
        Assert.Equal(200f, size.Width);
        Assert.Equal(100f, size.Height);
    }

    [Fact]
    public void GridSize_Empty_IsZero()
    {
        var empty = GridSize.Empty;
        Assert.Equal(0f, empty.Width);
        Assert.Equal(0f, empty.Height);
    }

    // ── GridPoint ─────────────────────────────────────────────────

    [Fact]
    public void GridPoint_Constructor_SetsProperties()
    {
        var pt = new GridPoint(3.14f, 2.71f);
        Assert.Equal(3.14f, pt.X);
        Assert.Equal(2.71f, pt.Y);
    }

    // ── GridFontMetrics ───────────────────────────────────────────

    [Fact]
    public void GridFontMetrics_Constructor_SetsProperties()
    {
        var metrics = new GridFontMetrics(-10f, 3f, 2f);
        Assert.Equal(-10f, metrics.Ascent);
        Assert.Equal(3f, metrics.Descent);
        Assert.Equal(2f, metrics.Leading);
    }

    [Fact]
    public void GridFontMetrics_LineHeight_IsDescentMinusAscentPlusLeading()
    {
        // Descent - Ascent + Leading = 3 - (-10) + 2 = 15
        var metrics = new GridFontMetrics(-10f, 3f, 2f);
        Assert.Equal(15f, metrics.LineHeight);
    }

    [Fact]
    public void GridFontMetrics_TextHeight_IsDescentMinusAscent()
    {
        // Descent - Ascent = 3 - (-10) = 13
        var metrics = new GridFontMetrics(-10f, 3f, 2f);
        Assert.Equal(13f, metrics.TextHeight);
    }

    [Fact]
    public void GridFontMetrics_ZeroLeading_LineHeightEqualsTextHeight()
    {
        var metrics = new GridFontMetrics(-12f, 4f);
        Assert.Equal(metrics.TextHeight, metrics.LineHeight);
    }

    // ── GridPaint ─────────────────────────────────────────────────

    [Fact]
    public void GridPaint_DefaultConstructor_HasSensibleDefaults()
    {
        var paint = new GridPaint();
        Assert.Equal(GridColor.Black, paint.Color);
        Assert.Equal(1f, paint.StrokeWidth);
        Assert.Equal(PaintStyle.Fill, paint.Style);
        Assert.True(paint.IsAntiAlias);
        Assert.Null(paint.Font);
    }

    [Fact]
    public void GridPaint_CanSetProperties()
    {
        var font = new GridFont("Helvetica", 12f);
        var paint = new GridPaint
        {
            Color = GridColor.Red,
            StrokeWidth = 2f,
            Style = PaintStyle.Stroke,
            IsAntiAlias = false,
            Font = font
        };
        Assert.Equal(GridColor.Red, paint.Color);
        Assert.Equal(2f, paint.StrokeWidth);
        Assert.Equal(PaintStyle.Stroke, paint.Style);
        Assert.False(paint.IsAntiAlias);
        Assert.Same(font, paint.Font);
    }
}


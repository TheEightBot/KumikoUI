namespace KumikoUI.Core.Rendering;

/// <summary>
/// Platform-independent font definition.
/// </summary>
public class GridFont
{
    /// <summary>Font family name.</summary>
    public string Family { get; set; } = "Default";

    /// <summary>Font size in pixels.</summary>
    public float Size { get; set; } = 14f;

    /// <summary>Whether the font is bold.</summary>
    public bool IsBold { get; set; }

    /// <summary>Whether the font is italic.</summary>
    public bool IsItalic { get; set; }

    public GridFont() { }

    public GridFont(string family, float size, bool bold = false, bool italic = false)
    {
        Family = family;
        Size = size;
        IsBold = bold;
        IsItalic = italic;
    }
}

/// <summary>
/// Platform-independent paint/style definition for drawing operations.
/// </summary>
public class GridPaint
{
    /// <summary>Fill or stroke color.</summary>
    public GridColor Color { get; set; } = GridColor.Black;

    /// <summary>Stroke width in pixels (used when Style is Stroke or FillAndStroke).</summary>
    public float StrokeWidth { get; set; } = 1f;

    /// <summary>Paint style: fill, stroke, or both.</summary>
    public PaintStyle Style { get; set; } = PaintStyle.Fill;

    /// <summary>Whether anti-aliasing is enabled.</summary>
    public bool IsAntiAlias { get; set; } = true;

    /// <summary>Optional font for text rendering.</summary>
    public GridFont? Font { get; set; }
}

/// <summary>
/// Specifies how a shape or path is painted.
/// </summary>
public enum PaintStyle
{
    /// <summary>Fill the interior.</summary>
    Fill,
    /// <summary>Stroke the outline.</summary>
    Stroke,
    /// <summary>Fill the interior and stroke the outline.</summary>
    FillAndStroke
}

/// <summary>
/// Text alignment options.
/// </summary>
public enum GridTextAlignment
{
    /// <summary>Left-aligned text.</summary>
    Left,
    /// <summary>Center-aligned text.</summary>
    Center,
    /// <summary>Right-aligned text.</summary>
    Right
}

/// <summary>
/// Vertical text alignment options.
/// </summary>
public enum GridVerticalAlignment
{
    /// <summary>Top-aligned.</summary>
    Top,
    /// <summary>Vertically centered.</summary>
    Center,
    /// <summary>Bottom-aligned.</summary>
    Bottom
}

/// <summary>
/// Simple rectangle structure for layout and rendering.
/// </summary>
public readonly struct GridRect
{
    /// <summary>X-coordinate of the left edge.</summary>
    public float X { get; }

    /// <summary>Y-coordinate of the top edge.</summary>
    public float Y { get; }

    /// <summary>Width of the rectangle.</summary>
    public float Width { get; }

    /// <summary>Height of the rectangle.</summary>
    public float Height { get; }

    /// <summary>X-coordinate of the left edge (alias for X).</summary>
    public float Left => X;

    /// <summary>Y-coordinate of the top edge (alias for Y).</summary>
    public float Top => Y;

    /// <summary>X-coordinate of the right edge (X + Width).</summary>
    public float Right => X + Width;

    /// <summary>Y-coordinate of the bottom edge (Y + Height).</summary>
    public float Bottom => Y + Height;

    public GridRect(float x, float y, float width, float height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    /// <summary>Tests whether a point is inside this rectangle.</summary>
    public bool Contains(float px, float py)
        => px >= Left && px <= Right && py >= Top && py <= Bottom;

    /// <summary>Returns a new rectangle expanded by the specified amounts.</summary>
    public GridRect Inflate(float dx, float dy)
        => new(X - dx, Y - dy, Width + 2 * dx, Height + 2 * dy);

    /// <summary>Returns a new rectangle shifted by the specified amounts.</summary>
    public GridRect Offset(float dx, float dy)
        => new(X + dx, Y + dy, Width, Height);

    /// <summary>A zero-size rectangle at the origin.</summary>
    public static GridRect Empty => new(0, 0, 0, 0);
}

/// <summary>
/// Simple size structure.
/// </summary>
public readonly struct GridSize
{
    /// <summary>Width component.</summary>
    public float Width { get; }

    /// <summary>Height component.</summary>
    public float Height { get; }

    public GridSize(float width, float height)
    {
        Width = width;
        Height = height;
    }

    /// <summary>A zero-size instance.</summary>
    public static GridSize Empty => new(0, 0);
}

/// <summary>
/// Simple point structure.
/// </summary>
public readonly struct GridPoint
{
    /// <summary>X-coordinate.</summary>
    public float X { get; }

    /// <summary>Y-coordinate.</summary>
    public float Y { get; }

    public GridPoint(float x, float y)
    {
        X = x;
        Y = y;
    }
}

/// <summary>
/// Font metrics for precise text positioning (ascent, descent, leading).
/// Ascent is negative (above baseline), descent is positive (below baseline).
/// </summary>
public readonly struct GridFontMetrics
{
    /// <summary>Distance from baseline to top of text (negative value).</summary>
    public float Ascent { get; }

    /// <summary>Distance from baseline to bottom of text (positive value).</summary>
    public float Descent { get; }

    /// <summary>Inter-line spacing.</summary>
    public float Leading { get; }

    /// <summary>Total line height (Descent - Ascent + Leading).</summary>
    public float LineHeight => Descent - Ascent + Leading;

    /// <summary>Text height without leading (Descent - Ascent).</summary>
    public float TextHeight => Descent - Ascent;

    public GridFontMetrics(float ascent, float descent, float leading = 0)
    {
        Ascent = ascent;
        Descent = descent;
        Leading = leading;
    }
}

namespace KumikoUI.Core.Rendering;

/// <summary>
/// Simple rectangle structure for layout and rendering.
/// </summary>
public readonly struct KumikoRect
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

    public KumikoRect(float x, float y, float width, float height)
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
    public KumikoRect Inflate(float dx, float dy)
        => new(X - dx, Y - dy, Width + 2 * dx, Height + 2 * dy);

    /// <summary>Returns a new rectangle shifted by the specified amounts.</summary>
    public KumikoRect Offset(float dx, float dy)
        => new(X + dx, Y + dy, Width, Height);

    /// <summary>A zero-size rectangle at the origin.</summary>
    public static KumikoRect Empty => new(0, 0, 0, 0);

    /// <inheritdoc />
    public override string ToString() => FormattableString.Invariant($"KumikoRect({X}, {Y}, {Width}, {Height})");
}

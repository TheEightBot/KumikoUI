namespace KumikoUI.Core.Rendering;

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

namespace KumikoUI.Core.Rendering;

/// <summary>
/// Simple point structure.
/// </summary>
public readonly struct KumikoPoint
{
    /// <summary>X-coordinate.</summary>
    public float X { get; }

    /// <summary>Y-coordinate.</summary>
    public float Y { get; }

    /// <summary>Creates a point.</summary>
    /// <param name="x">X-coordinate.</param>
    /// <param name="y">Y-coordinate.</param>
    public KumikoPoint(float x, float y)
    {
        X = x;
        Y = y;
    }
}

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

    public KumikoPoint(float x, float y)
    {
        X = x;
        Y = y;
    }
}

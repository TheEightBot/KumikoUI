namespace KumikoUI.Core.Rendering;

/// <summary>
/// Simple size structure.
/// </summary>
public readonly struct KumikoSize
{
    /// <summary>Width component.</summary>
    public float Width { get; }

    /// <summary>Height component.</summary>
    public float Height { get; }

    public KumikoSize(float width, float height)
    {
        Width = width;
        Height = height;
    }

    /// <summary>A zero-size instance.</summary>
    public static KumikoSize Empty => new(0, 0);
}

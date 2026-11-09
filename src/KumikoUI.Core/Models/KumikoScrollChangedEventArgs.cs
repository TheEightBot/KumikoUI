namespace KumikoUI.Core.Models;

/// <summary>Event args for <see cref="ScrollState.ScrollChanged"/>.</summary>
public sealed class KumikoScrollChangedEventArgs : EventArgs
{
    /// <summary>Creates the args.</summary>
    public KumikoScrollChangedEventArgs(float oldOffsetX, float oldOffsetY, float offsetX, float offsetY)
    {
        OldOffsetX = oldOffsetX;
        OldOffsetY = oldOffsetY;
        OffsetX = offsetX;
        OffsetY = offsetY;
    }

    /// <summary>The horizontal offset before the change.</summary>
    public float OldOffsetX { get; }

    /// <summary>The vertical offset before the change.</summary>
    public float OldOffsetY { get; }

    /// <summary>The horizontal offset after the change.</summary>
    public float OffsetX { get; }

    /// <summary>The vertical offset after the change.</summary>
    public float OffsetY { get; }
}

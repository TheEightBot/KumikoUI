namespace KumikoUI.Core.Models;

/// <summary>
/// Event data for the hosts' <c>Scrolled</c> event: the scroll offsets after the change, the offsets before it, and
/// the content and viewport extents, enough to implement load-more ("is the user near the end?") without reading
/// the control. All values are in logical pixels.
/// </summary>
public sealed class KumikoScrolledEventArgs : EventArgs
{
    /// <summary>Creates the event data.</summary>
    /// <param name="offsetX">The horizontal offset after the change.</param>
    /// <param name="offsetY">The vertical offset after the change.</param>
    /// <param name="oldOffsetX">The horizontal offset before the change.</param>
    /// <param name="oldOffsetY">The vertical offset before the change.</param>
    /// <param name="contentWidth">The total content width.</param>
    /// <param name="contentHeight">The total content height.</param>
    /// <param name="viewportWidth">The viewport width.</param>
    /// <param name="viewportHeight">The viewport height.</param>
    public KumikoScrolledEventArgs(float offsetX, float offsetY, float oldOffsetX, float oldOffsetY,
        float contentWidth, float contentHeight, float viewportWidth, float viewportHeight)
    {
        OffsetX = offsetX;
        OffsetY = offsetY;
        OldOffsetX = oldOffsetX;
        OldOffsetY = oldOffsetY;
        ContentWidth = contentWidth;
        ContentHeight = contentHeight;
        ViewportWidth = viewportWidth;
        ViewportHeight = viewportHeight;
    }

    /// <summary>The horizontal offset after the change.</summary>
    public float OffsetX { get; }

    /// <summary>The vertical offset after the change.</summary>
    public float OffsetY { get; }

    /// <summary>The horizontal offset before the change.</summary>
    public float OldOffsetX { get; }

    /// <summary>The vertical offset before the change.</summary>
    public float OldOffsetY { get; }

    /// <summary>How far the grid moved horizontally (<see cref="OffsetX"/> minus <see cref="OldOffsetX"/>).</summary>
    public float DeltaX => OffsetX - OldOffsetX;

    /// <summary>How far the grid moved vertically (<see cref="OffsetY"/> minus <see cref="OldOffsetY"/>).</summary>
    public float DeltaY => OffsetY - OldOffsetY;

    /// <summary>The total content width.</summary>
    public float ContentWidth { get; }

    /// <summary>The total content height.</summary>
    public float ContentHeight { get; }

    /// <summary>The viewport width.</summary>
    public float ViewportWidth { get; }

    /// <summary>The viewport height.</summary>
    public float ViewportHeight { get; }

    /// <summary>The largest horizontal offset (content width minus viewport width, minimum 0).</summary>
    public float MaxOffsetX => Math.Max(0, ContentWidth - ViewportWidth);

    /// <summary>The largest vertical offset (content height minus viewport height, minimum 0).</summary>
    public float MaxOffsetY => Math.Max(0, ContentHeight - ViewportHeight);
}

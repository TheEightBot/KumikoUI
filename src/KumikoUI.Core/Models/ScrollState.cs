namespace KumikoUI.Core.Models;

/// <summary>
/// Tracks scroll position and visible row/column ranges.
/// All values are in logical (unscaled) pixels.
/// </summary>
public class ScrollState
{
    /// <summary>Horizontal scroll offset in pixels.</summary>
    public float OffsetX { get; set; }

    /// <summary>Vertical scroll offset in pixels.</summary>
    public float OffsetY { get; set; }

    /// <summary>Total content width (sum of column widths).</summary>
    public float ContentWidth { get; set; }

    /// <summary>Total content height (row count × row height + header).</summary>
    public float ContentHeight { get; set; }

    /// <summary>Viewport width.</summary>
    public float ViewportWidth { get; set; }

    /// <summary>Viewport height.</summary>
    public float ViewportHeight { get; set; }

    /// <summary>Maximum horizontal scroll offset (content width minus viewport width, minimum 0).</summary>
    public float MaxOffsetX => Math.Max(0, ContentWidth - ViewportWidth);
    /// <summary>Maximum vertical scroll offset (content height minus viewport height, minimum 0).</summary>
    public float MaxOffsetY => Math.Max(0, ContentHeight - ViewportHeight);

    /// <summary>Clamps the current scroll offset to valid bounds.</summary>
    public void ClampOffset()
    {
        OffsetX = Math.Clamp(OffsetX, 0, MaxOffsetX);
        OffsetY = Math.Clamp(OffsetY, 0, MaxOffsetY);
    }

    /// <summary>Scrolls by the specified deltas and clamps the result.</summary>
    public void ScrollBy(float dx, float dy)
    {
        OffsetX += dx;
        OffsetY += dy;
        ClampOffset();
    }
}

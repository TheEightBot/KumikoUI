namespace KumikoUI.Core.Rendering;

/// <summary>
/// The math of a scroll bar thumb along its track, shared by the grid's scroll bars (#316), the combo box's drop-down
/// list and the filter popup's value list, so a thumb is sized, placed, dragged and drawn the same way everywhere.
/// Coordinates are along the track's axis. Allocation-free.
/// </summary>
internal static class ScrollThumb
{
    /// <summary>
    /// The thumb's length: the track times the share of the content the viewport shows, at least
    /// <paramref name="minLength"/> and never longer than the track. A track with no content to scroll is all thumb.
    /// </summary>
    /// <param name="track">The track's length.</param>
    /// <param name="viewport">How much of the content is visible at once.</param>
    /// <param name="content">The content's whole length.</param>
    /// <param name="minLength">The shortest the thumb gets.</param>
    public static float Length(float track, float viewport, float content, float minLength)
    {
        if (float.IsNaN(track) || track <= 0) return 0;
        if (float.IsNaN(content) || content <= 0 || float.IsNaN(viewport) || viewport >= content) return track;
        float min = Math.Min(float.IsFinite(minLength) ? Math.Max(0, minLength) : 0, track);
        return Math.Clamp(track * (viewport / content), min, track);
    }

    /// <summary>Where the thumb starts for the scroll offset: at the track's start at 0, at its end at the maximum.</summary>
    /// <param name="trackStart">The track's start.</param>
    /// <param name="track">The track's length.</param>
    /// <param name="thumbLength">The thumb's length (<see cref="Length"/>).</param>
    /// <param name="offset">The scroll offset.</param>
    /// <param name="maxOffset">The largest scroll offset.</param>
    public static float Start(float trackStart, float track, float thumbLength, float offset, float maxOffset)
    {
        float fraction = maxOffset > 0 ? Math.Clamp(offset / maxOffset, 0f, 1f) : 0f;
        return trackStart + fraction * Math.Max(0, track - thumbLength);
    }

    /// <summary>
    /// The scroll offset that puts the thumb's start at <paramref name="thumbStart"/> (the inverse of
    /// <see cref="Start"/>), not clamped; NaN when the thumb fills the track and cannot move.
    /// </summary>
    /// <param name="thumbStart">Where the thumb should start.</param>
    /// <param name="trackStart">The track's start.</param>
    /// <param name="track">The track's length.</param>
    /// <param name="thumbLength">The thumb's length.</param>
    /// <param name="maxOffset">The largest scroll offset.</param>
    public static float OffsetAt(float thumbStart, float trackStart, float track, float thumbLength, float maxOffset)
    {
        float travel = track - thumbLength;
        if (float.IsNaN(travel) || travel <= 0) return float.NaN;
        return (thumbStart - trackStart) / travel * maxOffset;
    }

    /// <summary>Draws a thumb: a rectangle with fully rounded ends.</summary>
    /// <param name="ctx">The drawing context.</param>
    /// <param name="thumb">The thumb's rectangle.</param>
    /// <param name="paint">Its color.</param>
    public static void Fill(IDrawingContext ctx, KumikoRect thumb, KumikoPaint paint) =>
        ctx.FillRoundRect(thumb, Math.Min(thumb.Width, thumb.Height) / 2f, paint);
}

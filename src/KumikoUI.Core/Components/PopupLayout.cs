using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>
/// Places a popup (a dropdown list, a calendar, a filter panel) next to its anchor and keeps it inside the
/// viewport: below the anchor when it fits, above it when only that fits, clamped sideways, and, for a list,
/// shrunk to the room left.
/// </summary>
internal static class PopupLayout
{
    /// <summary>Whether <paramref name="viewport"/> is a real area; an empty one leaves popups unconstrained.</summary>
    internal static bool HasArea(KumikoRect viewport) => viewport.Width > 0 && viewport.Height > 0;

    /// <summary>
    /// The popup's rectangle: below <paramref name="anchor"/> (or above it when <paramref name="preferAbove"/>),
    /// on the other side when the preferred side is too short and the other is not, and inside
    /// <paramref name="viewport"/>.
    /// </summary>
    /// <param name="anchor">The cell or header the popup belongs to.</param>
    /// <param name="width">The popup's width.</param>
    /// <param name="height">The popup's height.</param>
    /// <param name="viewport">The visible area; empty for no limit.</param>
    /// <param name="gap">Space between anchor and popup.</param>
    /// <param name="shrink">When neither side has room: shrink the height to the larger side instead of
    /// sliding the popup over the anchor.</param>
    /// <param name="preferAbove">Open above the anchor when there is room.</param>
    /// <param name="mayCoverAnchor">When a popup that cannot shrink fits on neither side: slide it into the
    /// viewport over the anchor (true), or leave it below the anchor, cut off by the viewport (false).</param>
    internal static KumikoRect Place(
        KumikoRect anchor, float width, float height, KumikoRect viewport,
        float gap = 0, bool shrink = false, bool preferAbove = false, bool mayCoverAnchor = true)
    {
        float belowY = anchor.Bottom + gap;
        float aboveY = anchor.Y - gap - height;

        if (!HasArea(viewport))
            return new KumikoRect(anchor.X, preferAbove ? aboveY : belowY, width, height);

        width = Math.Min(width, viewport.Width);
        float x = Math.Clamp(anchor.X, viewport.X, Math.Max(viewport.X, viewport.Right - width));

        float below = Math.Max(0, viewport.Bottom - belowY);
        float above = Math.Max(0, anchor.Y - gap - viewport.Y);
        bool fitsBelow = height <= below, fitsAbove = height <= above;

        if (preferAbove ? fitsAbove : !fitsBelow && fitsAbove)
            return new KumikoRect(x, aboveY, width, height);
        if (fitsBelow || fitsAbove)
            return new KumikoRect(x, fitsBelow ? belowY : aboveY, width, height);

        if (shrink)
        {
            return below >= above
                ? new KumikoRect(x, belowY, width, below)
                : new KumikoRect(x, anchor.Y - gap - above, width, above);
        }

        if (!mayCoverAnchor)
            return new KumikoRect(x, belowY, width, height);

        // A fixed-size popup that fits on neither side: the roomier side, slid into the viewport
        float y = Math.Clamp(below >= above ? belowY : aboveY, viewport.Y, Math.Max(viewport.Y, viewport.Bottom - height));
        return new KumikoRect(x, y, width, height);
    }

    /// <summary>The room a list has: the taller of the space below and above <paramref name="anchor"/>.</summary>
    internal static float RoomFor(KumikoRect anchor, KumikoRect viewport)
    {
        if (!HasArea(viewport)) return float.PositiveInfinity;
        return Math.Max(Math.Max(0, viewport.Bottom - anchor.Bottom), Math.Max(0, anchor.Y - viewport.Y));
    }

    /// <summary>
    /// <paramref name="bounds"/> moved (not resized) to lie inside <paramref name="viewport"/>, as far as it fits.
    /// </summary>
    internal static KumikoRect Clamp(KumikoRect bounds, KumikoRect viewport)
    {
        if (!HasArea(viewport)) return bounds;
        float x = Math.Clamp(bounds.X, viewport.X, Math.Max(viewport.X, viewport.Right - bounds.Width));
        float y = Math.Clamp(bounds.Y, viewport.Y, Math.Max(viewport.Y, viewport.Bottom - bounds.Height));
        return new KumikoRect(x, y, bounds.Width, bounds.Height);
    }
}

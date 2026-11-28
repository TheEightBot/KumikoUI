using KumikoUI.Core.Models;

namespace KumikoUI.Core.Rendering;

/// <summary>
/// Everything an <see cref="IGroupHeaderRenderer"/> needs to draw one group header: where to draw, which group it is,
/// the texts the grid composed for it, and the grid's style and shared paints. The grid creates one per header on the
/// stack and passes it by reference, so drawing a header allocates nothing.
/// </summary>
/// <remarks>
/// The reference-type members may be replaced or shared once the call returns (the grid builds new
/// <see cref="GroupHeaderInfo"/> objects when it rebuilds the view): read them during
/// <see cref="IGroupHeaderRenderer.Render"/>, and do not keep or modify them.
/// </remarks>
public readonly struct GroupHeaderRenderContext
{
    /// <summary>Create the context for one group header.</summary>
    /// <param name="bounds">The rectangle to draw within: right of the chevron up to the row's right padding.</param>
    /// <param name="rowBounds">The whole header row.</param>
    /// <param name="row">The visible row index of the header.</param>
    /// <param name="isPinned">Whether the header is drawn as a sticky header over the rows.</param>
    /// <param name="group">The group.</param>
    /// <param name="description">The description the group is built from.</param>
    /// <param name="title">The title text, "Header: DisplayText".</param>
    /// <param name="countText">The item count text, " (n)".</param>
    /// <param name="captionText">The caption summary text, or null when the group has none.</param>
    /// <param name="style">The grid style.</param>
    /// <param name="paints">The grid's shared paints for <paramref name="style"/>.</param>
    public GroupHeaderRenderContext(
        KumikoRect bounds,
        KumikoRect rowBounds,
        int row,
        bool isPinned,
        GroupHeaderInfo group,
        GroupDescription description,
        string title,
        string countText,
        string? captionText,
        KumikoStyle style,
        PaintCache paints)
    {
        Bounds = bounds;
        RowBounds = rowBounds;
        Row = row;
        IsPinned = isPinned;
        Group = group;
        Description = description;
        Title = title;
        CountText = countText;
        CaptionText = captionText;
        Style = style;
        Paints = paints;
    }

    /// <summary>
    /// The rectangle to draw the header content within: right of the chevron up to the row's right padding, the full
    /// row height. Anything drawn outside it is clipped.
    /// </summary>
    public KumikoRect Bounds { get; }

    /// <summary>The whole header row, including the chevron.</summary>
    public KumikoRect RowBounds { get; }

    /// <summary>The visible row index of the header.</summary>
    public int Row { get; }

    /// <summary>Whether the header is drawn as a sticky header pinned over the rows (<see cref="KumikoStyle.StickyGroupHeaders"/>).</summary>
    public bool IsPinned { get; }

    /// <summary>The group: <see cref="GroupHeaderInfo.Level"/>, key, display text, item count and expanded state.</summary>
    public GroupHeaderInfo Group { get; }

    /// <summary>The description the group is built from, the one for <see cref="GroupHeaderInfo.Level"/>.</summary>
    public GroupDescription Description { get; }

    /// <summary>The title text, "Header: DisplayText". The same instance from frame to frame while the group is unchanged.</summary>
    public string Title { get; }

    /// <summary>The item count text, " (n)". The same instance from frame to frame while the group is unchanged.</summary>
    public string CountText { get; }

    /// <summary>
    /// The caption summary text of the group (the caption's columns formatted and joined), or null when the grid has
    /// no caption summary. <see cref="GroupHeaderInfo.GetCaptionValue"/> returns the raw aggregates.
    /// </summary>
    public string? CaptionText { get; }

    /// <summary>The grid style (colors, fonts, sizes).</summary>
    public KumikoStyle Style { get; }

    /// <summary>The grid's shared paints, built from <see cref="Style"/>. Draw with them; do not modify them.</summary>
    public PaintCache Paints { get; }
}

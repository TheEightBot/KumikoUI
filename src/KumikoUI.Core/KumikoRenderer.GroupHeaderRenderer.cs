using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core;

public sealed partial class KumikoRenderer
{
    private static readonly TextGroupHeaderRenderer DefaultGroupHeaderRenderer = new();

    /// <summary>The description used when a header's level has none (a view built before the descriptions changed).</summary>
    private static readonly GroupDescription NoGroupDescription = new();

    /// <summary>
    /// Draws the content of every group header that has no <see cref="GroupDescription.CustomHeaderRenderer"/> for its
    /// level: the part right of the chevron. Null (the default) draws the title, the count and the caption summary as
    /// text with <see cref="TextGroupHeaderRenderer"/>. It must not allocate per frame.
    /// </summary>
    public IGroupHeaderRenderer? GroupHeaderRenderer { get; set; }

    /// <summary>The description of a group's level, or an empty one when the level has none.</summary>
    private static GroupDescription DescriptionFor(KumikoSource dataSource, int level)
    {
        var descriptions = dataSource.GroupDescriptions;
        return (uint)level < (uint)descriptions.Count ? descriptions[level] : NoGroupDescription;
    }

    /// <summary>
    /// Draw a group header's content (right of the chevron) with the renderer that applies: the level's
    /// <see cref="GroupDescription.CustomHeaderRenderer"/>, else <see cref="GroupHeaderRenderer"/>, else the text one.
    /// Whatever it draws is clipped to <paramref name="bounds"/>.
    /// </summary>
    private void DrawGroupHeaderContent(
        IDrawingContext ctx, KumikoSource dataSource, GroupHeaderInfo group, int row, bool isPinned,
        KumikoRect rowBounds, KumikoRect bounds, KumikoStyle style)
    {
        var description = DescriptionFor(dataSource, group.Level);
        var renderer = description.CustomHeaderRenderer ?? GroupHeaderRenderer ?? DefaultGroupHeaderRenderer;
        var context = new GroupHeaderRenderContext(
            bounds, rowBounds, row, isPinned, group, description,
            group.GetTitle(), group.GetCountText(), KumikoSource.GetCaptionSummaryText(group), style, _paintCache);

        ctx.Save();
        ctx.ClipRect(bounds);
        renderer.Render(ctx, in context);
        ctx.Restore();
    }
}

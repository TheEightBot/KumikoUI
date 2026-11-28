using KumikoUI.Core.Rendering;

namespace SampleApp.Shared;

/// <summary>
/// A custom group header for the Grouping sample page, shared by the MAUI and Uno samples: the group's title, the
/// item count in a rounded badge in the style's accent color instead of "(n)", and the caption summary, when there is
/// one, right-aligned. The grid keeps the chevron and the row background.
/// </summary>
public sealed class CountBadgeGroupHeaderRenderer : IGroupHeaderRenderer
{
    private const float BadgeGap = 8f;
    private const float BadgePadding = 8f;
    private const float BadgeHeight = 20f;

    private readonly KumikoPaint _badge = new() { IsAntiAlias = true };
    private readonly KumikoPaint _badgeText = new() { Color = KumikoColor.White, Font = new KumikoFont("Default", 12f, bold: true) };
    private readonly Dictionary<int, string> _countTexts = new();

    /// <inheritdoc />
    public void Render(IDrawingContext ctx, in GroupHeaderRenderContext context)
    {
        var bounds = context.Bounds;
        var paints = context.Paints;
        _badge.Color = context.Style.AccentColor;

        var titleSize = ctx.MeasureText(context.Title, paints.GroupHeaderText);
        ctx.DrawTextInRect(context.Title, bounds, paints.GroupHeaderText, KumikoTextAlignment.Left, KumikoVerticalAlignment.Center);

        if (!_countTexts.TryGetValue(context.Group.ItemCount, out var count))
            _countTexts[context.Group.ItemCount] = count = context.Group.ItemCount.ToString(System.Globalization.CultureInfo.CurrentCulture);

        float badgeWidth = ctx.MeasureText(count, _badgeText).Width + 2 * BadgePadding;
        var badge = new KumikoRect(bounds.X + titleSize.Width + BadgeGap, bounds.Y + (bounds.Height - BadgeHeight) / 2, badgeWidth, BadgeHeight);
        ctx.FillRoundRect(badge, BadgeHeight / 2, _badge);
        ctx.DrawTextInRect(count, badge, _badgeText, KumikoTextAlignment.Center, KumikoVerticalAlignment.Center);

        if (context.CaptionText is { } caption)
        {
            var captionBounds = new KumikoRect(badge.Right + BadgeGap, bounds.Y, Math.Max(0f, bounds.Right - badge.Right - BadgeGap), bounds.Height);
            ctx.DrawTextInRect(caption, captionBounds, paints.CaptionSummaryText, KumikoTextAlignment.Right, KumikoVerticalAlignment.Center);
        }
    }

    /// <inheritdoc />
    public string? GetAccessibleText(in GroupHeaderRenderContext context) =>
        $"{context.Title}, {context.Group.ItemCount} items";
}

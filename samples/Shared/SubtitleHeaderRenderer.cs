using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace SampleApp.Shared;

/// <summary>
/// A header renderer for the Styling page, shared by the MAUI and Uno samples: a colored badge with a symbol before
/// the header text and a smaller subtitle line below it ("Salary" over "USD per year"). The grid keeps the sort and
/// filter glyphs and the resize grip; the renderer only draws the content, and measures it for auto-fit.
/// </summary>
public sealed class SubtitleHeaderRenderer : IHeaderCellRenderer
{
    private const float BadgeSize = 16f;
    private const float Gap = 6f;
    private const float TitleHeight = 20f;
    private const float SubtitleHeight = 14f;

    // Created once and reused: only the subtitle's color follows the header's text color
    private readonly KumikoPaint _badge = new() { Color = new KumikoColor(13, 110, 253), IsAntiAlias = true };
    private readonly KumikoPaint _badgeText = new() { Color = KumikoColor.White, IsAntiAlias = true, Font = new KumikoFont("Default", 11, bold: true) };
    private readonly KumikoPaint _subtitlePaint = new() { IsAntiAlias = true, Font = new KumikoFont("Default", 10) };

    /// <summary>The symbol drawn in the badge before the header text.</summary>
    public string Symbol { get; set; } = "$";

    /// <summary>The smaller line drawn below the header text.</summary>
    public string Subtitle { get; set; } = "";

    /// <inheritdoc />
    public void Render(IDrawingContext ctx, in HeaderCellRenderContext context)
    {
        var b = context.Bounds;
        float top = b.Y + (b.Height - TitleHeight - SubtitleHeight) / 2f;

        var badge = new KumikoRect(b.X, top + (TitleHeight - BadgeSize) / 2f, BadgeSize, BadgeSize);
        ctx.FillRoundRect(badge, BadgeSize / 2f, _badge);
        ctx.DrawTextInRect(Symbol, badge, _badgeText, KumikoTextAlignment.Center, KumikoVerticalAlignment.Center);

        float textX = b.X + BadgeSize + Gap;
        float textWidth = Math.Max(0f, b.Width - BadgeSize - Gap);
        ctx.DrawTextInRect(context.Column.Header, new KumikoRect(textX, top, textWidth, TitleHeight), context.TextPaint,
            context.TextAlignment, KumikoVerticalAlignment.Center);

        _subtitlePaint.Color = context.TextPaint.Color.WithAlpha(160);
        ctx.DrawTextInRect(Subtitle, new KumikoRect(textX, top + TitleHeight, textWidth, SubtitleHeight), _subtitlePaint,
            context.TextAlignment, KumikoVerticalAlignment.Center);
    }

    /// <inheritdoc />
    public float? MeasureWidth(IDrawingContext ctx, in HeaderCellRenderContext context)
    {
        float title = ctx.MeasureText(context.Column.Header, context.TextPaint).Width;
        float subtitle = Subtitle.Length == 0 ? 0f : ctx.MeasureText(Subtitle, _subtitlePaint).Width;
        return BadgeSize + Gap + Math.Max(title, subtitle);
    }

    /// <inheritdoc />
    public string? GetAccessibleText(in HeaderCellRenderContext context) =>
        Subtitle.Length == 0 ? null : $"{context.Column.Header}, {Subtitle}";
}

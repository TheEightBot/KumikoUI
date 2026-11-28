namespace KumikoUI.Core.Rendering;

/// <summary>
/// The built-in group header renderer: up to three text runs side by side, "Header: DisplayText" in
/// <see cref="PaintCache.GroupHeaderText"/>, " (count)" in <see cref="PaintCache.GroupHeaderCount"/> and the caption
/// summary in <see cref="PaintCache.CaptionSummaryText"/>. A custom renderer can delegate to it and draw around it.
/// </summary>
public sealed class TextGroupHeaderRenderer : IGroupHeaderRenderer
{
    /// <inheritdoc />
    public void Render(IDrawingContext ctx, in GroupHeaderRenderContext context)
    {
        var bounds = context.Bounds;
        var paints = context.Paints;
        float right = bounds.Right;

        float x = DrawRun(ctx, context.Title, paints.GroupHeaderText, bounds.X, right, bounds);
        x = DrawRun(ctx, context.CountText, paints.GroupHeaderCount, x, right, bounds);
        if (context.Group.GetCaptionLabel(context.CaptionText) is { } caption)
            DrawRun(ctx, caption, paints.CaptionSummaryText, x, right, bounds);
    }

    /// <inheritdoc />
    public string? GetAccessibleText(in GroupHeaderRenderContext context) => context.Title + context.CountText;

    /// <summary>
    /// Draw one run starting at <paramref name="x"/> and return where the next run starts; returns
    /// <paramref name="right"/> when the run did not fit, so nothing is drawn after it.
    /// </summary>
    private static float DrawRun(IDrawingContext ctx, string text, KumikoPaint paint, float x, float right, KumikoRect bounds)
    {
        if (x >= right) return right;

        float width = ctx.MeasureText(text, paint).Width;
        var rect = new KumikoRect(x, bounds.Y, right - x, bounds.Height);
        ctx.DrawTextInRect(text, rect, paint, KumikoTextAlignment.Left, KumikoVerticalAlignment.Center);
        return x + width;
    }
}

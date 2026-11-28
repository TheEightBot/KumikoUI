namespace KumikoUI.Core.Rendering;

/// <summary>
/// The built-in header renderer: draws <see cref="Models.KumikoColumn.Header"/> as one line in
/// <see cref="HeaderCellRenderContext.TextPaint"/>, cut with an ellipsis, aligned by
/// <see cref="HeaderCellRenderContext.TextAlignment"/> and centered vertically. A custom renderer can derive from it
/// to draw something around the text.
/// </summary>
public class TextHeaderCellRenderer : IHeaderCellRenderer
{
    /// <inheritdoc />
    public virtual void Render(IDrawingContext ctx, in HeaderCellRenderContext context)
    {
        ctx.DrawTextInRect(context.Column.Header, context.Bounds, context.TextPaint,
            context.TextAlignment, KumikoVerticalAlignment.Center);
    }
}

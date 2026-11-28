namespace KumikoUI.Core.Rendering;

/// <summary>
/// The built-in summary cell renderer: draws <see cref="SummaryCellRenderContext.DisplayText"/> with the row's text
/// paint, aligned like the column and centered vertically. Derive from it to add to the text.
/// </summary>
public class TextSummaryCellRenderer : ISummaryCellRenderer
{
    /// <summary>The shared instance the grid uses when no custom renderer applies.</summary>
    internal static readonly TextSummaryCellRenderer Default = new();

    /// <inheritdoc />
    public virtual void Render(IDrawingContext ctx, in SummaryCellRenderContext context) =>
        ctx.DrawTextInRect(context.DisplayText, context.Bounds, context.TextPaint,
            context.TextAlignment, KumikoVerticalAlignment.Center);
}

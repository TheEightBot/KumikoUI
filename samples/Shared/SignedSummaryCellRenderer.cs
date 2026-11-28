using KumikoUI.Core.Rendering;

namespace SampleApp.Shared;

/// <summary>
/// A summary cell renderer (#334) that draws a negative total in red and a total of zero or more with the row's own
/// text paint. Shared by the MAUI and Uno samples.
/// </summary>
public sealed class SignedSummaryCellRenderer : ISummaryCellRenderer
{
    // Created once and reused for every frame; the renderer must not allocate per cell.
    private readonly KumikoPaint _negative = new()
    {
        Color = new KumikoColor(0xDC, 0x26, 0x26),
        IsAntiAlias = true,
    };

    /// <inheritdoc />
    public void Render(IDrawingContext ctx, in SummaryCellRenderContext context)
    {
        bool isNegative = context.RawValue switch
        {
            decimal d => d < 0,
            double d => d < 0,
            long l => l < 0,
            int i => i < 0,
            _ => false,
        };

        if (isNegative)
        {
            _negative.Font = context.TextPaint.Font;
            ctx.DrawTextInRect(context.DisplayText, context.Bounds, _negative, context.TextAlignment,
                KumikoVerticalAlignment.Center);
        }
        else
        {
            ctx.DrawTextInRect(context.DisplayText, context.Bounds, context.TextPaint, context.TextAlignment,
                KumikoVerticalAlignment.Center);
        }
    }
}

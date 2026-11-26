namespace KumikoUI.Core.Rendering;

/// <summary>
/// Renders text content (Text, Numeric, Date column types): one line cut with an ellipsis, or wrapped lines when
/// the column's <see cref="Models.KumikoColumn.TextWrap"/> asks for them. A grid-wide search match in the cell
/// (<see cref="CellRenderContext.SearchMatchLength"/>) is highlighted behind the text.
/// </summary>
public class TextCellRenderer : ICellRenderer
{
    private const string Ellipsis = "…";
    private const float HighlightCornerRadius = 3f;
    private const float HighlightVerticalInset = 1f;

    /// <inheritdoc />
    public void Render(IDrawingContext ctx, in CellRenderContext cell)
    {
        if (cell.SearchMatchLength > 0)
            DrawSearchHighlight(ctx, in cell);

        var wrap = cell.Column.TextWrap;
        if (wrap == KumikoTextWrap.None)
        {
            ctx.DrawTextInRect(cell.DisplayText, cell.Bounds, cell.TextPaint,
                cell.TextAlignment, KumikoVerticalAlignment.Center);
        }
        else
        {
            ctx.DrawTextInRect(cell.DisplayText, cell.Bounds, cell.TextPaint,
                cell.TextAlignment, KumikoVerticalAlignment.Center, clip: true, wrap);
        }
    }

    /// <summary>
    /// Fills a rounded rectangle behind the search match of a cell drawn the way this renderer draws text: one line
    /// in <see cref="CellRenderContext.TextPaint"/>, centered vertically and aligned by
    /// <see cref="CellRenderContext.TextAlignment"/> within <see cref="CellRenderContext.Bounds"/>. The fill is
    /// <see cref="PaintCache.SearchCurrentHighlight"/> for the current match and <see cref="PaintCache.SearchHighlight"/>
    /// otherwise. A match in the part of a long line cut off by the ellipsis highlights the ellipsis. A wrapped
    /// cell is highlighted only while its text fits on one line. Draws nothing when the cell has no match
    /// (<see cref="CellRenderContext.SearchMatchLength"/> is 0). Allocates nothing with a drawing context that caches
    /// text widths (see <see cref="IDrawingContext.MeasureText(string, int, int, KumikoPaint)"/>).
    /// </summary>
    /// <param name="ctx">The drawing context.</param>
    /// <param name="cell">The cell.</param>
    public static void DrawSearchHighlight(IDrawingContext ctx, in CellRenderContext cell)
    {
        if (GetSearchHighlightBounds(ctx, in cell) is not { } rect) return;
        var paint = cell.IsCurrentSearchMatch ? cell.Paints.SearchCurrentHighlight : cell.Paints.SearchHighlight;
        ctx.FillRoundRect(rect, HighlightCornerRadius, paint);
    }

    /// <summary>The rectangle <see cref="DrawSearchHighlight"/> fills, or null when it draws nothing.</summary>
    internal static KumikoRect? GetSearchHighlightBounds(IDrawingContext ctx, in CellRenderContext cell)
    {
        string text = cell.DisplayText;
        int start = cell.SearchMatchStart;
        int length = cell.SearchMatchLength;
        if (length <= 0 || start < 0 || start + length > text.Length) return null;

        var bounds = cell.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return null;
        var paint = cell.TextPaint;
        float width = ctx.MeasureText(text, paint).Width;
        bool cut = width > bounds.Width;
        if (cut && cell.Column.TextWrap != KumikoTextWrap.None)
            return null; // laid out on several lines: v1 highlights single-line text only

        float lineX = cut ? bounds.X : cell.TextAlignment switch
        {
            KumikoTextAlignment.Center => bounds.X + (bounds.Width - width) / 2,
            KumikoTextAlignment.Right => bounds.Right - width,
            _ => bounds.X,
        };
        float left = lineX + (start > 0 ? ctx.MeasureText(text, 0, start, paint).Width : 0f);
        float right = left + ctx.MeasureText(text, start, length, paint).Width;

        if (cut)
        {
            // The line ends with an ellipsis where the text is cut: a match behind it highlights the ellipsis.
            float ellipsisLeft = bounds.Right - ctx.MeasureText(Ellipsis, paint).Width;
            if (left >= ellipsisLeft) left = ellipsisLeft;
            right = Math.Min(right, bounds.Right);
        }
        left = Math.Max(left, bounds.X);
        right = Math.Min(right, bounds.Right);
        if (right <= left) return null;

        var metrics = ctx.GetFontMetrics(paint);
        float textHeight = metrics.TextHeight;
        float top = bounds.Y + (bounds.Height - textHeight) / 2 - HighlightVerticalInset;
        float height = Math.Min(textHeight + HighlightVerticalInset * 2, bounds.Height);
        top = Math.Max(top, bounds.Y);
        return new KumikoRect(left, top, right - left, height);
    }
}

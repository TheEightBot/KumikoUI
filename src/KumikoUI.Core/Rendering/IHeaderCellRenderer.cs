namespace KumikoUI.Core.Rendering;

/// <summary>
/// Draws the content of a column header cell: the text, plus whatever an app adds to it (an icon, a subtitle, a
/// "required" marker). Set it per column with <see cref="Models.KumikoColumn.CustomHeaderRenderer"/> or for every
/// column with <see cref="KumikoRenderer.HeaderCellRenderer"/>; without either, <see cref="TextHeaderCellRenderer"/>
/// draws the header text.
/// </summary>
/// <remarks>
/// <para>The grid keeps everything outside <see cref="HeaderCellRenderContext.Bounds"/>: the background, the
/// per-column background, the hover and pressed feedback, the header border, the sort arrow, the filter icon and the
/// column menu button with their hit regions, the resize grips and the reorder ghost. A custom renderer only draws
/// the content, so sorting, filtering, resizing and reordering behave as they do with the default text.</para>
/// <para><see cref="Render"/> runs for every visible header on every frame. To keep frames free of allocations,
/// draw with <see cref="HeaderCellRenderContext.Paints"/> or <see cref="HeaderCellRenderContext.TextPaint"/>, or with
/// paints the renderer creates once and reuses; do not create a <see cref="KumikoPaint"/>, <see cref="KumikoFont"/>
/// or string per call.</para>
/// </remarks>
public interface IHeaderCellRenderer
{
    /// <summary>
    /// Draws the header content inside <see cref="HeaderCellRenderContext.Bounds"/>, which the grid has already
    /// clipped to.
    /// </summary>
    /// <param name="ctx">Drawing context.</param>
    /// <param name="context">The header to draw: its bounds, column, sort and filter state, styles and the grid's
    /// shared paints. Its reference-type members may be reused for the next header: read them during the call, do
    /// not keep or modify them.</param>
    void Render(IDrawingContext ctx, in HeaderCellRenderContext context);

    /// <summary>
    /// The width the content needs, for header auto-fit: the grid adds the header padding, the room for the sort,
    /// filter and menu glyphs and a small margin. Called only when the header's fit width is recomputed (the header
    /// text, the header font or the renderer changed), not on every frame. <see cref="HeaderCellRenderContext.Bounds"/>
    /// and <see cref="HeaderCellRenderContext.CellBounds"/> have zero width in the context it gets.
    /// </summary>
    /// <param name="ctx">Measures text.</param>
    /// <param name="context">The header to measure.</param>
    /// <returns>The content width in pixels, or null to use the width of <see cref="Models.KumikoColumn.Header"/>
    /// in the header font.</returns>
    float? MeasureWidth(IDrawingContext ctx, in HeaderCellRenderContext context) => null;

    /// <summary>The plain text of the header for the accessibility tree.</summary>
    /// <param name="context">The header.</param>
    /// <returns>The text a screen reader reads, or null to use <see cref="Models.KumikoColumn.Header"/>.</returns>
    string? GetAccessibleText(in HeaderCellRenderContext context) => null;
}

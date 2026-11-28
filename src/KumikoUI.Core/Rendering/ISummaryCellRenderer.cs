namespace KumikoUI.Core.Rendering;

/// <summary>
/// Draws one summary value cell in place of the built-in text (#334): set it per summary with
/// <see cref="Models.SummaryColumnDescription.CustomCellRenderer"/>, or for the whole grid with
/// <see cref="KumikoRenderer.SummaryCellRenderer"/>.
/// </summary>
/// <remarks>
/// <see cref="Render"/> runs for every visible summary cell on every frame. To keep frames free of allocations, draw
/// with the paints in <see cref="SummaryCellRenderContext.Paints"/> or with paints the renderer creates once and
/// reuses; do not create a <see cref="KumikoPaint"/>, <see cref="KumikoFont"/> or string per call. A background
/// rebuild replaces the <see cref="SummaryCellRenderContext.Summary"/> instance, so do not keep the context's
/// references between calls.
/// </remarks>
public interface ISummaryCellRenderer
{
    /// <summary>
    /// Draws one summary value cell inside <see cref="SummaryCellRenderContext.Bounds"/>, which is already clipped to
    /// the visible part of the column.
    /// </summary>
    /// <param name="ctx">Drawing context.</param>
    /// <param name="context">The cell to draw. Its reference-type members may be reused for the next cell: read
    /// them during the call, do not keep or modify them.</param>
    void Render(IDrawingContext ctx, in SummaryCellRenderContext context);

    /// <summary>
    /// The plain text a screen reader reads for the cell. Null (the default) uses
    /// <see cref="SummaryCellRenderContext.DisplayText"/>.
    /// </summary>
    /// <param name="context">The cell being described.</param>
    string? GetAccessibleText(in SummaryCellRenderContext context) => null;
}

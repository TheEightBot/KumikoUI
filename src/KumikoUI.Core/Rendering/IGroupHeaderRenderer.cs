namespace KumikoUI.Core.Rendering;

/// <summary>
/// Draws the content of a group header row: the part to the right of the expand/collapse chevron. Set it per group
/// level with <see cref="Models.GroupDescription.CustomHeaderRenderer"/> or for every header with
/// <see cref="KumikoRenderer.GroupHeaderRenderer"/>; without either, <see cref="TextGroupHeaderRenderer"/> draws the
/// title, the item count and the caption summary as text.
/// </summary>
/// <remarks>
/// The grid keeps the row background, the border, the chevron and its hit region, so tapping, Enter, Space and the
/// Left and Right arrows expand and collapse a group whatever a renderer draws. The renderer gets no input.
/// <see cref="Render"/> runs for every visible and sticky group header on every frame: draw with the paints in
/// <see cref="GroupHeaderRenderContext.Paints"/> or with paints the renderer creates once and reuses, and do not create
/// a <see cref="KumikoPaint"/>, <see cref="KumikoFont"/> or string per call.
/// </remarks>
public interface IGroupHeaderRenderer
{
    /// <summary>Draw the header content within <see cref="GroupHeaderRenderContext.Bounds"/>; drawing outside it is clipped.</summary>
    /// <param name="ctx">Drawing context.</param>
    /// <param name="context">The header to draw. Read it during the call only: the group objects are replaced when the
    /// view is rebuilt, and the paints are shared.</param>
    void Render(IDrawingContext ctx, in GroupHeaderRenderContext context);

    /// <summary>
    /// The plain text a screen reader hears for the header, or null for the grid's own description. Never called from
    /// the render loop.
    /// </summary>
    /// <param name="context">The header.</param>
    string? GetAccessibleText(in GroupHeaderRenderContext context) => null;
}

namespace KumikoUI.Core.Rendering;

/// <summary>
/// A cell renderer that can report how tall its content wants to be at a given width, so the row grows to fit it in
/// <see cref="Models.KumikoRowHeightMode.Auto"/> mode (<see cref="Models.KumikoStyle.RowHeightMode"/>). The built-in
/// <see cref="TextCellRenderer"/> implements it for wrapping columns.
/// </summary>
public interface IMeasurableCellRenderer
{
    /// <summary>
    /// The height of the cell's content (without <see cref="Models.KumikoStyle.CellVerticalPadding"/>) when it is
    /// laid out <c>cell.Bounds.Width</c> wide, or <c>null</c> when the content does not constrain the row height. It
    /// is called only to measure a row in <see cref="Models.KumikoRowHeightMode.Auto"/> mode (once per item and
    /// content version, never per frame). <c>cell.Bounds.Height</c> is <see cref="Models.KumikoStyle.RowHeight"/>.
    /// </summary>
    /// <param name="ctx">Measures text; do not draw with it.</param>
    /// <param name="cell">The cell, as it will be drawn.</param>
    /// <returns>The content height in pixels, or <c>null</c>.</returns>
    float? MeasureHeight(IDrawingContext ctx, in CellRenderContext cell);
}

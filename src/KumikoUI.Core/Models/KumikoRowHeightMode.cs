namespace KumikoUI.Core.Models;

/// <summary>How the height of data rows is determined (<see cref="KumikoStyle.RowHeightMode"/>).</summary>
public enum KumikoRowHeightMode
{
    /// <summary>Every data row is <see cref="KumikoStyle.RowHeight"/> tall (the default, and the 1.0 behavior).</summary>
    Fixed,

    /// <summary>
    /// A data row is as tall as its tallest measurable cell plus <see cref="KumikoStyle.CellVerticalPadding"/> above
    /// and below, within <see cref="KumikoStyle.MinRowHeight"/> and <see cref="KumikoStyle.MaxRowHeight"/>. Text cells
    /// are measurable when their column wraps (<see cref="KumikoColumn.TextWrap"/>); a custom cell renderer takes part
    /// by implementing <see cref="Rendering.IMeasurableCellRenderer"/>. Rows are measured when they are first drawn;
    /// until then they are laid out at an estimate (the mean of the rows measured so far), so the scroll extent
    /// converges as rows are drawn.
    /// </summary>
    Auto,
}

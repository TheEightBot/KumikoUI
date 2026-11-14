namespace KumikoUI.Core.Rendering;

/// <summary>
/// A cell renderer whose cell is interactive without being edited: a tap (or Enter) acts on the cell
/// directly, with no editor, no <c>EditSession</c> and so none of <c>CellBeginEdit</c>, <c>CellEndEdit</c>,
/// <c>CellValueChanged</c> or the view change a commit causes. <see cref="ActionButtonsCellRenderer"/> is one.
/// </summary>
/// <remarks>
/// A column whose renderer implements this interface never opens an editor, whatever its edit triggers say,
/// and is not blocked by <c>IsReadOnly</c>. A column with its own <c>EditorFactory</c>,
/// <c>CustomEditorFactory</c> or <c>EditorDescriptor</c> is not treated as interactive: the editor wins.
/// The handlers may change the data (remove the row they belong to): the grid reads nothing about the row
/// after they return.
/// </remarks>
public interface IInteractiveCellRenderer
{
    /// <summary>A single tap landed on the cell.</summary>
    /// <param name="cellRect">The cell rectangle as it was drawn (the same one <see cref="ICellRenderer.Render"/> got).</param>
    /// <param name="x">Tap X, in the same coordinates as <paramref name="cellRect"/>.</param>
    /// <param name="y">Tap Y.</param>
    /// <param name="value">The cell value, read before the tap (the row item for a <c>PropertyName=""</c> column).</param>
    /// <returns>True when the tap did something (hit an enabled control).</returns>
    bool OnCellTap(KumikoRect cellRect, float x, float y, object? value);

    /// <summary>
    /// Enter was pressed on the current cell: invoke its default control, if it has one and it is enabled.
    /// </summary>
    /// <returns>True when something was invoked.</returns>
    bool OnCellActivate(object? value);

    /// <summary>
    /// Whether a tap at (<paramref name="x"/>, <paramref name="y"/>) would act on something (for an
    /// <see cref="ActionButtonsCellRenderer"/>, an enabled button). The grid shows a hand cursor there.
    /// Called on every hover move over the cell, so it should not allocate. The default is <c>false</c>.
    /// </summary>
    /// <param name="cellRect">The cell rectangle, as for <see cref="OnCellTap"/>.</param>
    /// <param name="x">Pointer X, in the same coordinates as <paramref name="cellRect"/>.</param>
    /// <param name="y">Pointer Y.</param>
    /// <param name="value">The cell value, as for <see cref="OnCellTap"/>.</param>
    bool HasControlAt(KumikoRect cellRect, float x, float y, object? value) => false;

    /// <summary>
    /// Which of the cell's controls a tap at (<paramref name="x"/>, <paramref name="y"/>) would act on, or -1 for
    /// none: the grid draws hover and pressed feedback while this stays the same and redraws when it changes (for an
    /// <see cref="ActionButtonsCellRenderer"/>, the index of the enabled button). The renderer draws the feedback
    /// itself, from <see cref="CellRenderContext.PointerPosition"/>. Called on every hover move over the cell, so it
    /// should not allocate. The default is 0 where <see cref="HasControlAt"/> is true, else -1.
    /// </summary>
    /// <param name="cellRect">The cell rectangle, as for <see cref="OnCellTap"/>.</param>
    /// <param name="x">Pointer X, in the same coordinates as <paramref name="cellRect"/>.</param>
    /// <param name="y">Pointer Y.</param>
    /// <param name="value">The cell value, as for <see cref="OnCellTap"/>.</param>
    /// <returns>The control's index, or -1.</returns>
    int GetControlIndexAt(KumikoRect cellRect, float x, float y, object? value) =>
        HasControlAt(cellRect, x, y, value) ? 0 : -1;

    /// <summary>
    /// Raised when the cell's appearance changed without the grid knowing (for example a command's
    /// <c>CanExecuteChanged</c>). The grid redraws.
    /// </summary>
    event EventHandler? RedrawRequested;
}

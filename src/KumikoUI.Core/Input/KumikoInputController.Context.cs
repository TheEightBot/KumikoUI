using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Input;

/// <summary>
/// Pointer buttons and context menus (#100): only the primary button selects, sorts, scrolls, drags and edits; the
/// secondary button and a touch or pen long press raise <see cref="ContextRequested"/>.
/// </summary>
public sealed partial class KumikoInputController
{
    // The button of the press in progress (KumikoPointerEventArgs.Button of its Pressed event).
    private PointerButton _pressButton = PointerButton.Primary;

    // A non-primary press that does nothing more: it closed the popups, or landed in a popup or the open editor.
    private bool _nonPrimaryPressIgnored;

    // The press in progress raised ContextRequested on a long press: its moves and release do nothing.
    private bool _pressConsumedByContext;

    /// <summary>
    /// Raised when the user asks for a context menu: a click with the secondary (right) mouse button, on its
    /// release, or a long press with a finger or a pen that neither starts a row drag nor edits the cell
    /// (<see cref="KumikoContextTrigger"/>). It is raised for every part of the grid (cells, headers, group rows,
    /// the empty area), with what is under the point; on a data row the selection is updated first (see
    /// <see cref="KumikoContextRequestedEventArgs"/>). Not raised for a press inside a popup or the open editor, or
    /// one that closes an open popup. A secondary-button press never sorts, scrolls, drags or edits.
    /// </summary>
    public event EventHandler<KumikoContextRequestedEventArgs>? ContextRequested;

    /// <summary>
    /// Raised when a data cell is tapped with the primary button or a finger (after the selection is updated), like
    /// <see cref="RowTapped"/>, with the column, the position and the click count. A double tap raises it for each
    /// tap.
    /// </summary>
    public event EventHandler<KumikoCellTappedEventArgs>? CellTapped;

    /// <summary>
    /// Raised when a column header is tapped with the primary button or a finger, after the tap sorted the column
    /// (when sorting is allowed). Not raised for a tap on the filter icon or the resize grip, nor at the end of a
    /// column drag or resize.
    /// </summary>
    public event EventHandler<KumikoHeaderTappedEventArgs>? HeaderTapped;

    /// <summary>The primary button, and events that name none (a finger, an older host): everything but right and middle.</summary>
    private static bool IsPrimaryButton(PointerButton button) =>
        button is not (PointerButton.Secondary or PointerButton.Middle);

    /// <summary>
    /// A press with the secondary or middle button, and its moves, long press and release. It never selects,
    /// sorts, scrolls, drags or edits: a press outside the popups closes them (as any press does), and the release
    /// of a secondary press raises <see cref="ContextRequested"/>, unless the press closed popups or landed in a
    /// popup or the open editor. Secondary-button events are handled; middle-button events are left to the page
    /// unless they closed a popup.
    /// </summary>
    private void HandleNonPrimaryPointer(
        KumikoPointerEventArgs e, InputAction action,
        ScrollState scroll, KumikoSelectionModel selection, KumikoStyle style, KumikoSource dataSource)
    {
        bool secondary = _pressButton == PointerButton.Secondary;
        switch (action)
        {
            case InputAction.Pressed:
                _nonPrimaryPressIgnored = false;
                _inertialScroller.Reset(); // a press stops a fling, whatever the button
                if (_popupManager.HasActivePopups)
                {
                    // Only the primary button acts on a popup; a press outside them closes them and does nothing else
                    if (_popupManager.HitTest(e.X, e.Y) == null)
                    {
                        _popupManager.CloseAll();
                        e.Handled = true;
                    }
                    _nonPrimaryPressIgnored = true;
                }
                else if (_editSession is { IsEditing: true, ActiveEditor: { } editor } && editor.HitTest(e.X, e.Y))
                {
                    _nonPrimaryPressIgnored = true; // the open editor keeps the click: no menu over it
                }
                break;

            case InputAction.Released:
                if (secondary && !_nonPrimaryPressIgnored)
                {
                    var hit = _hitTester.HitTest(e.X, e.Y, scroll, style, dataSource.Columns, dataSource.RowCount, dataSource);
                    RequestContext(KumikoContextTrigger.SecondaryButton, e, hit, selection, dataSource);
                }
                _nonPrimaryPressIgnored = false;
                break;
        }

        if (secondary) e.Handled = true;
    }

    /// <summary>
    /// Raises <see cref="ContextRequested"/> for <paramref name="hit"/>. On a data row (a cell or its drag handle) an
    /// open edit on another cell is committed first, a row that is not selected becomes the selection, and the
    /// current cell moves to the cell under the point. Returns false when nothing was hit; true when the event was
    /// raised, or when a refused commit kept the editor open (no event).
    /// </summary>
    private bool RequestContext(
        KumikoContextTrigger trigger, KumikoPointerEventArgs e, KumikoHitTestResult hit,
        KumikoSelectionModel selection, KumikoSource dataSource)
    {
        if (hit.Region == HitRegion.None) return false;

        // The empty area right of the last column reports its row; a menu there is not about that row.
        int row = hit.Region == HitRegion.EmptyArea ? -1 : hit.RowIndex;
        object? item = null;
        if (hit.Region is HitRegion.Cell or HitRegion.RowDragHandle && IsDataRow(dataSource, row))
        {
            bool onEditedCell = _editSession is { IsEditing: true } && _editSession.IsCellBeingEdited(row, hit.ColumnIndex);
            if (!onEditedCell)
            {
                if (!CommitEditBeforeViewChange(dataSource, selection)) return true;
                if (!selection.IsRowSelected(row)) selection.HandleRowSelection(row, shift: false, ctrl: false);
                if (hit.ColumnIndex >= 0) selection.CurrentCell = new CellPosition(row, hit.ColumnIndex);
                Redraw();
            }
            item = dataSource.GetItem(row);
        }

        ContextRequested?.Invoke(this, new KumikoContextRequestedEventArgs(
            trigger, hit.Region, row, hit.ColumnIndex, hit.Column, item, e.X, e.Y, e.DeviceType));
        return true;
    }
}

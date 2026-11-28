using KumikoUI.Core.Layout;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Input;

// Deleting the selected rows (#328): the Delete key, DeleteSelectedRows, and the cancelable RowsDeleting event. The
// removal itself is KumikoSource.RemoveItems; the row menu and swipe actions call DeleteItems with their own rows.
public sealed partial class KumikoInputController
{
    /// <summary>
    /// Whether the Delete key (on Apple keyboards also Command+Backspace, the key they label Delete) removes the
    /// selected rows from the items source while no cell editor is open. It does nothing in a read-only grid, and
    /// with no selected row the key is left to the page. Default <c>false</c>. <see cref="DeleteSelectedRows"/> does
    /// not depend on it. Plain Backspace never deletes rows.
    /// </summary>
    public bool AllowDeleting { get; set; }

    /// <summary>
    /// Raised before rows are removed, by the Delete key or <see cref="DeleteSelectedRows"/>, after an open edit was
    /// committed. Set <see cref="KumikoRowsDeletingEventArgs.Cancel"/> to keep the rows; to confirm with a dialog,
    /// cancel and call <see cref="KumikoSource.RemoveItems"/> with <see cref="KumikoRowsDeletingEventArgs.Items"/>
    /// once the user agrees.
    /// </summary>
    public event EventHandler<KumikoRowsDeletingEventArgs>? RowsDeleting;

    /// <summary>
    /// Raised once after the grid asked <see cref="KumikoSource.RemoveItems"/> to remove rows (a handler of
    /// <see cref="RowsDeleting"/> did not cancel), with its result. The current cell and the selection have already
    /// moved to the row that took the first deleted row's place.
    /// </summary>
    public event EventHandler<KumikoRowsDeletedEventArgs>? RowsDeleted;

    /// <summary>
    /// Removes the selected rows' items from the items source, as the Delete key does but without the
    /// <see cref="AllowDeleting"/> and read-only checks: an open edit is committed first (a refused commit stops the
    /// delete), then <see cref="RowsDeleting"/> is raised and, unless a handler canceled it, the items are removed with
    /// <see cref="KumikoSource.RemoveItems"/> (one view rebuild) and <see cref="RowsDeleted"/> is raised. Afterwards the
    /// current cell, in the same column, and the selection move to the row that took the first deleted row's place
    /// (or the new last row), so deleting again works, and the deletion is announced to a screen reader.
    /// </summary>
    /// <param name="scroll">The grid's scroll state; the new current cell is scrolled into view.</param>
    /// <param name="selection">The selection whose rows are deleted.</param>
    /// <param name="style">The grid's style (row geometry for scrolling).</param>
    /// <param name="dataSource">The data source the selection belongs to.</param>
    /// <returns><c>true</c> when items were removed; <c>false</c> when no row is selected, the edit could not be
    /// committed, a handler canceled the delete or the items source could not be changed.</returns>
    public bool DeleteSelectedRows(
        ScrollState scroll, KumikoSelectionModel selection, KumikoStyle style, KumikoSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(scroll);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(dataSource);
        return DeleteItems(GetSelectedItemsInViewOrder(selection, dataSource), KumikoRowsDeleteTrigger.Command,
            scroll, selection, style, dataSource);
    }

    /// <summary>
    /// The delete flow for <paramref name="items"/> (any rows, not only the selected ones), shared by the Delete key,
    /// <see cref="DeleteSelectedRows"/>, the row menu and swipe actions: commit an open edit, ask
    /// <see cref="RowsDeleting"/>, remove, move the current cell and the selection, raise <see cref="RowsDeleted"/>
    /// and announce. <paramref name="items"/> should be in view order.
    /// </summary>
    internal bool DeleteItems(
        IReadOnlyList<object> items, KumikoRowsDeleteTrigger trigger,
        ScrollState scroll, KumikoSelectionModel selection, KumikoStyle style, KumikoSource dataSource)
    {
        var snapshot = items.Where(item => item != null).ToArray();
        if (snapshot.Length == 0) return false;
        if (!CommitEditBeforeViewChange(dataSource, selection)) return false;

        var deleting = new KumikoRowsDeletingEventArgs(snapshot, trigger);
        RowsDeleting?.Invoke(this, deleting);
        if (deleting.Cancel) return false;

        // The row that takes the first deleted row's place, found while the rows are still there
        int column = CurrentColumnForBoxClick(selection, dataSource, 0);
        var successor = FindSuccessorItem(dataSource, snapshot);

        var result = dataSource.RemoveItems(snapshot);
        if (result == KumikoRemoveResult.Removed)
        {
            if (successor != null)
            {
                selection.MoveToItem(successor, column);
                EnsureCurrentCellVisible(scroll, selection, style, dataSource.Columns, dataSource);
            }
            Announce(KumikoAnnouncements.RowsDeleted(dataSource.Strings, snapshot.Length, dataSource.Culture));
        }

        RowsDeleted?.Invoke(this, new KumikoRowsDeletedEventArgs(snapshot, result));
        Redraw();
        return result == KumikoRemoveResult.Removed;
    }

    /// <summary>
    /// Delete, or Command+Backspace, with <see cref="AllowDeleting"/> on, a writable grid and selected rows, deletes
    /// them. Anything else is left to the page. Returns whether the key was taken.
    /// </summary>
    private bool TryHandleDeleteKey(
        KumikoKeyEventArgs e, ScrollState scroll, KumikoSelectionModel selection, KumikoStyle style, KumikoSource dataSource)
    {
        bool isDeleteKey = e.Key == KumikoKey.Delete
            ? e.Modifiers == InputModifiers.None
            : e.Key == KumikoKey.Backspace && (e.Modifiers & InputModifiers.Meta) != 0 &&
              (e.Modifiers & (InputModifiers.Shift | InputModifiers.Alt)) == 0;
        if (!isDeleteKey || !AllowDeleting || GridReadOnly || selection.SelectedRows.Count == 0) return false;

        DeleteItems(GetSelectedItemsInViewOrder(selection, dataSource), KumikoRowsDeleteTrigger.Key,
            scroll, selection, style, dataSource);
        return true;
    }

    /// <summary>The selected data rows' items, top to bottom.</summary>
    private static List<object> GetSelectedItemsInViewOrder(KumikoSelectionModel selection, KumikoSource dataSource)
    {
        var rows = selection.SelectedRows.Where(row => row >= 0 && row < dataSource.RowCount && !dataSource.IsNonDataRow(row)).ToList();
        rows.Sort();
        return rows.Select(row => dataSource.GetRowKey(row)).Where(key => key != null).Select(key => key!).ToList();
    }

    /// <summary>
    /// The item of the first data row at or after the first deleted row that is not itself deleted, else the last such
    /// row before it; <c>null</c> when none of the items is in the view or no row would be left.
    /// </summary>
    private static object? FindSuccessorItem(KumikoSource dataSource, IReadOnlyList<object> deleted)
    {
        var doomed = new HashSet<object>(deleted, ReferenceEqualityComparer.Instance);
        int rowCount = dataSource.RowCount;
        int first = int.MaxValue;
        foreach (int row in deleted.Select(dataSource.IndexOfRowKey))
        {
            if (row >= 0 && row < first) first = row;
        }
        if (first == int.MaxValue) return null;

        for (int row = first; row < rowCount; row++)
        {
            if (SurvivingItemAt(dataSource, row, doomed) is { } after) return after;
        }
        for (int row = first - 1; row >= 0; row--)
        {
            if (SurvivingItemAt(dataSource, row, doomed) is { } before) return before;
        }
        return null;
    }

    private static object? SurvivingItemAt(KumikoSource dataSource, int row, HashSet<object> doomed)
    {
        if (dataSource.IsNonDataRow(row)) return null;
        var item = dataSource.GetRowKey(row);
        return item != null && !doomed.Contains(item) ? item : null;
    }
}

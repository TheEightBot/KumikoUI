using System.Collections.ObjectModel;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests.Input;

/// <summary>
/// Deleting the selected rows (#328): the Delete key and Command+Backspace behind <c>AllowDeleting</c>, the cancelable
/// <c>RowsDeleting</c> event, <c>DeleteSelectedRows</c>, where the current cell and the selection land, and the
/// announcement.
/// </summary>
public class RowDeletingTests
{
    private static InputGrid Grid(int rows = 12, Action<InputGrid>? configure = null) =>
        new(rows, configure: g =>
        {
            g.Input.AllowDeleting = true;
            configure?.Invoke(g);
        });

    private static void SelectRows(InputGrid g, params int[] rows)
    {
        g.Selection.SetSelectedItems(rows.Select(r => (object)g.Items[r]));
    }

    private static string[] Names(InputGrid g) => g.Items.Select(i => i.Name).ToArray();

    private static string CurrentName(InputGrid g) => CurrentName(g, g.Items);

    private static string CurrentName(InputGrid g, IEnumerable<InputGrid.Item> items) =>
        items.Single(i => ReferenceEquals(i, g.Selection.CurrentItem)).Name;

    // ── The Delete key ──

    [Fact]
    public void Delete_RemovesTheSelectedRows_AndHandlesTheKey()
    {
        var g = Grid();
        SelectRows(g, 2, 3, 7);
        g.Select(3, 1);

        var e = g.Key(KumikoKey.Delete);

        Assert.True(e.Handled);
        Assert.Equal(9, g.Items.Count);
        Assert.DoesNotContain("N02", Names(g));
        Assert.DoesNotContain("N03", Names(g));
        Assert.DoesNotContain("N07", Names(g));
        Assert.Equal(9, g.Source.RowCount);
    }

    [Fact]
    public void AfterTheDelete_TheRowThatTookTheFirstDeletedRowsPlace_IsCurrentAndTheOnlySelection_InTheSameColumn()
    {
        var g = Grid();
        SelectRows(g, 2, 3, 7);
        g.Select(7, 2);

        g.Key(KumikoKey.Delete);

        Assert.Equal("N04", CurrentName(g)); // rows 2 and 3 went; N04 now sits at row 2
        Assert.Equal(new CellPosition(2, 2), g.Selection.CurrentCell);
        Assert.Equal(new[] { 2 }, g.Selection.SelectedRows.ToArray());
        Assert.Same(g.Items.Single(i => i.Name == "N04"), g.Selection.SelectedItems.Single());
    }

    [Fact]
    public void DeletingTheLastRows_MovesToTheNewLastRow()
    {
        var g = Grid(rows: 5);
        SelectRows(g, 3, 4);
        g.Select(4, 0);

        g.Key(KumikoKey.Delete);

        Assert.Equal(new CellPosition(2, 0), g.Selection.CurrentCell);
        Assert.Equal("N02", CurrentName(g));
        Assert.True(g.Selection.IsRowSelected(2));
    }

    [Fact]
    public void RepeatedDelete_KeepsWorking()
    {
        var g = Grid(rows: 5);
        g.Select(1, 0);
        g.Selection.Select(g.Items[1]);

        g.Key(KumikoKey.Delete);
        g.Key(KumikoKey.Delete);
        g.Key(KumikoKey.Delete);

        Assert.Equal(new[] { "N00", "N04" }, Names(g));
        Assert.Equal("N04", CurrentName(g));
    }

    [Fact]
    public void DeletingEveryRow_LeavesNoCurrentCell()
    {
        var g = Grid(rows: 3);
        g.Selection.SetCurrentCell(0, 0);
        g.Selection.SelectAll();

        g.Key(KumikoKey.Delete);

        Assert.Empty(g.Items);
        Assert.False(g.Selection.CurrentCell.IsValid);
        Assert.Empty(g.Selection.SelectedRows);
    }

    [Fact]
    public void TheNewCurrentRow_IsScrolledIntoView()
    {
        var g = Grid(rows: 30);
        g.Scroll.ScrollTo(0, 36f * 20);
        SelectRows(g, 25, 26);
        g.Select(25, 0);

        g.Key(KumikoKey.Delete);

        float rowTop = 36f * g.Selection.CurrentCell.Row;
        Assert.True(rowTop >= g.Scroll.OffsetY - 0.5f && rowTop < g.Scroll.OffsetY + g.Scroll.ViewportHeight);
    }

    [Fact]
    public void WithoutASelection_TheKeyIsLeftToThePage()
    {
        var g = Grid();
        g.Select(2, 0); // a current cell, no selected row

        var e = g.Key(KumikoKey.Delete);

        Assert.False(e.Handled);
        Assert.Equal(12, g.Items.Count);
    }

    [Fact]
    public void WithAllowDeletingOff_TheKeyDoesNothing()
    {
        var g = Grid(configure: x => x.Input.AllowDeleting = false);
        SelectRows(g, 2);

        var e = g.Key(KumikoKey.Delete);

        Assert.False(e.Handled);
        Assert.Equal(12, g.Items.Count);
    }

    [Fact]
    public void AReadOnlyGrid_KeepsItsRows()
    {
        var g = Grid(configure: x => x.Edit.IsReadOnly = true);
        SelectRows(g, 2);

        var e = g.Key(KumikoKey.Delete);

        Assert.False(e.Handled);
        Assert.Equal(12, g.Items.Count);
    }

    [Fact]
    public void GroupHeaders_AreNeverDeleted()
    {
        var g = Grid(configure: x => x.Source.AddGroupDescription(new GroupDescription("City")));
        g.Selection.SetCurrentCell(0, 0); // a group header
        g.Selection.SelectAll();
        int rowsBefore = g.Source.RowCount;
        Assert.True(g.Source.IsNonDataRow(0));

        var deleted = new List<IReadOnlyList<object>>();
        g.Input.RowsDeleting += (_, e) => deleted.Add(e.Items);
        g.Key(KumikoKey.Delete);

        Assert.Equal(12, deleted.Single().Count); // only data items
        Assert.Empty(g.Items);
        Assert.True(rowsBefore > 12);
        Assert.Equal(0, g.Source.DataRowCount);
    }

    // ── Other keys ──

    [Fact]
    public void CommandBackspace_Deletes_PlainBackspaceAndShiftDeleteDoNot()
    {
        var g = Grid();
        SelectRows(g, 4);

        Assert.False(g.Key(KumikoKey.Backspace).Handled);
        Assert.False(g.Key(KumikoKey.Delete, InputModifiers.Shift).Handled);
        Assert.False(g.Key(KumikoKey.Backspace, InputModifiers.Meta | InputModifiers.Alt).Handled);
        Assert.Equal(12, g.Items.Count);

        Assert.True(g.Key(KumikoKey.Backspace, InputModifiers.Meta).Handled);
        Assert.Equal(11, g.Items.Count);
        Assert.DoesNotContain("N04", Names(g));
    }

    [Fact]
    public void CommandBackspace_WithTheControlFlagToo_Deletes()
    {
        var g = Grid(); // Uno on a Mac reports Command as Meta and Control
        SelectRows(g, 4);

        Assert.True(g.Key(KumikoKey.Backspace, InputModifiers.Meta | InputModifiers.Control).Handled);
        Assert.Equal(11, g.Items.Count);
    }

    [Fact]
    public void AKeyRelease_DoesNotDelete()
    {
        var g = Grid();
        SelectRows(g, 4);

        g.Input.HandleKey(new KumikoKeyEventArgs { Key = KumikoKey.Delete, IsKeyDown = false }, g.Scroll, g.Selection, g.Style, g.Source);

        Assert.Equal(12, g.Items.Count);
    }

    // ── Events ──

    [Fact]
    public void RowsDeleting_ListsTheItemsInViewOrder_WithTheKeyTrigger()
    {
        var g = Grid();
        g.Source.SetSort(g.Score, KumikoSortDirection.Descending);
        SelectRows(g, 1, 9, 5);
        KumikoRowsDeletingEventArgs? seen = null;
        g.Input.RowsDeleting += (_, e) => seen = e;

        g.Key(KumikoKey.Delete);

        Assert.NotNull(seen);
        Assert.Equal(KumikoRowsDeleteTrigger.Key, seen!.Trigger);
        Assert.Equal(new[] { "N09", "N05", "N01" }, seen.Items.Cast<InputGrid.Item>().Select(i => i.Name)); // score descending
    }

    [Fact]
    public void ACanceledRowsDeleting_ChangesNothing_AndRaisesNoRowsDeleted()
    {
        var g = Grid();
        SelectRows(g, 2, 3);
        g.Select(3, 1);
        bool deleted = false;
        g.Input.RowsDeleting += (_, e) => e.Cancel = true;
        g.Input.RowsDeleted += (_, _) => deleted = true;

        var e = g.Key(KumikoKey.Delete);

        Assert.True(e.Handled); // the key was consumed even though the delete was refused
        Assert.False(deleted);
        Assert.Equal(12, g.Items.Count);
        Assert.Equal(new[] { 2, 3 }, g.Selection.SelectedRows.OrderBy(r => r));
        Assert.Equal(new CellPosition(3, 1), g.Selection.CurrentCell);
    }

    [Fact]
    public void RowsDeleted_ReportsTheItemsAndTheResult_AfterTheSelectionMoved()
    {
        var g = Grid();
        SelectRows(g, 2, 3);
        KumikoRowsDeletedEventArgs? seen = null;
        CellPosition currentWhenRaised = CellPosition.Invalid;
        g.Input.RowsDeleted += (_, e) => { seen = e; currentWhenRaised = g.Selection.CurrentCell; };

        g.Key(KumikoKey.Delete);

        Assert.Equal(KumikoRemoveResult.Removed, seen!.Result);
        Assert.Equal(2, seen.Items.Count);
        Assert.Equal(2, currentWhenRaised.Row);
    }

    [Fact]
    public void ADeferredConfirmation_CancelsThenRemovesTheItemsItself()
    {
        var g = Grid();
        SelectRows(g, 2, 3);
        IReadOnlyList<object>? asked = null;
        g.Input.RowsDeleting += (_, e) => { e.Cancel = true; asked = e.Items; };
        g.Key(KumikoKey.Delete);
        Assert.Equal(12, g.Items.Count);

        // the user said yes in a dialog
        Assert.Equal(KumikoRemoveResult.Removed, g.Source.RemoveItems(asked!));

        Assert.Equal(10, g.Items.Count);
    }

    [Fact]
    public void ASourceThatCannotBeChanged_ReportsNotSupported_AndKeepsTheSelection()
    {
        var items = Enumerable.Range(0, 5).Select(i => new InputGrid.Item { Name = $"N{i}" }).ToArray();
        var g = Grid(rows: 1, configure: x => x.Source.SetItems(items));
        g.Selection.SetSelectedItems(new object[] { items[1], items[2] });
        g.Select(1, 0);
        KumikoRowsDeletedEventArgs? seen = null;
        g.Input.RowsDeleted += (_, e) => seen = e;

        var e = g.Key(KumikoKey.Delete);

        Assert.True(e.Handled);
        Assert.Equal(KumikoRemoveResult.NotSupportedSource, seen!.Result);
        Assert.Equal(5, g.Source.RowCount);
        Assert.Equal(new[] { 1, 2 }, g.Selection.SelectedRows.OrderBy(r => r));
        Assert.Equal(new CellPosition(1, 0), g.Selection.CurrentCell);
    }

    // ── Editing ──

    [Fact]
    public void WhileAnEditorIsOpen_DeleteBelongsToTheEditor()
    {
        var g = Grid();
        SelectRows(g, 2);
        g.Select(2, 0);
        g.Type('Z');
        Assert.True(g.Edit.IsEditing);

        g.Key(KumikoKey.Delete);

        Assert.Equal(12, g.Items.Count);
        Assert.True(g.Edit.IsEditing);
    }

    [Fact]
    public void DeleteSelectedRows_CommitsAnOpenEditFirst()
    {
        var g = Grid();
        SelectRows(g, 5);
        g.Select(2, 0);
        g.Type('Z'); // edits row 2's name
        Assert.True(g.Edit.IsEditing);

        bool deleted = g.Input.DeleteSelectedRows(g.Scroll, g.Selection, g.Style, g.Source);

        Assert.True(deleted);
        Assert.False(g.Edit.IsEditing);
        Assert.Equal("Z", g.Items[2].Name); // committed
        Assert.DoesNotContain("N05", Names(g));
    }

    [Fact]
    public void ARefusedCommit_StopsTheDelete()
    {
        var g = Grid(configure: x => x.Edit.CellValidator = (_, _, _) => CellValidationResult.Error("no"));
        SelectRows(g, 5);
        g.Select(2, 0);
        g.Type('Z');
        bool raised = false;
        g.Input.RowsDeleting += (_, _) => raised = true;

        bool deleted = g.Input.DeleteSelectedRows(g.Scroll, g.Selection, g.Style, g.Source);

        Assert.False(deleted);
        Assert.False(raised);
        Assert.True(g.Edit.IsEditing);
        Assert.Equal(12, g.Items.Count);
    }

    // ── DeleteSelectedRows ──

    [Fact]
    public void DeleteSelectedRows_UsesTheCommandTrigger_AndWorksWithAllowDeletingOffOrAReadOnlyGrid()
    {
        var g = Grid(configure: x => { x.Input.AllowDeleting = false; x.Edit.IsReadOnly = true; });
        SelectRows(g, 1, 2);
        KumikoRowsDeleteTrigger? trigger = null;
        g.Input.RowsDeleting += (_, e) => trigger = e.Trigger;

        Assert.True(g.Input.DeleteSelectedRows(g.Scroll, g.Selection, g.Style, g.Source));

        Assert.Equal(KumikoRowsDeleteTrigger.Command, trigger);
        Assert.Equal(10, g.Items.Count);
    }

    [Fact]
    public void DeleteSelectedRows_WithNothingSelected_ReturnsFalse_AndRaisesNothing()
    {
        var g = Grid();
        bool raised = false;
        g.Input.RowsDeleting += (_, _) => raised = true;

        Assert.False(g.Input.DeleteSelectedRows(g.Scroll, g.Selection, g.Style, g.Source));

        Assert.False(raised);
    }

    [Fact]
    public void ARowMenuOrSwipe_CanDeleteRowsThatAreNotSelected_ThroughTheSamePath()
    {
        var g = Grid();
        SelectRows(g, 9);
        var victim = g.Items[3];
        KumikoRowsDeletingEventArgs? seen = null;
        g.Input.RowsDeleting += (_, e) => seen = e;

        Assert.True(g.Input.DeleteItems(new object[] { victim }, KumikoRowsDeleteTrigger.Command, g.Scroll, g.Selection, g.Style, g.Source));

        Assert.Same(victim, seen!.Items.Single());
        Assert.DoesNotContain(victim, g.Items);
        Assert.Equal("N04", CurrentName(g)); // the row that took its place
    }

    [Fact]
    public void DeleteRows_RemovesTheGivenItems_AfterRowsDeleting_WhichCanCancel()
    {
        var g = Grid();
        SelectRows(g, 9);
        var victim = g.Items[3];
        bool cancel = true;
        var deleted = new List<object>();
        g.Input.RowsDeleting += (_, e) => e.Cancel = cancel;
        g.Input.RowsDeleted += (_, e) => deleted.AddRange(e.Items);

        Assert.False(g.Input.DeleteRows(new object[] { victim }, g.Scroll, g.Selection, g.Style, g.Source));
        Assert.Contains(victim, g.Items);

        cancel = false;
        Assert.True(g.Input.DeleteRows(new object[] { victim }, g.Scroll, g.Selection, g.Style, g.Source));
        Assert.DoesNotContain(victim, g.Items);
        Assert.Same(victim, Assert.Single(deleted));
    }

    // ── Collections and views ──

    [Fact]
    public void AnObservableCollection_RaisesOneViewChange_ForManyRows()
    {
        ObservableCollection<InputGrid.Item>? list = null;
        var g = Grid(configure: x =>
        {
            list = new ObservableCollection<InputGrid.Item>(x.Items);
            x.Source.SetItems(list);
        });
        SelectRows(g, 0, 1, 2, 3, 4);
        int structural = 0;
        g.Source.ViewChanged += (_, e) => { if (e.IsStructural) structural++; };

        g.Key(KumikoKey.Delete);

        Assert.Equal(7, list!.Count);
        Assert.Equal(1, structural);
        Assert.Equal("N05", CurrentName(g, list));
    }

    [Fact]
    public void AGroupedView_DeletesBySelectedItem_AndMovesPastAGroupHeader()
    {
        var g = Grid(rows: 6, configure: x => x.Source.AddGroupDescription(new GroupDescription("City")));
        // Oslo: N00 N02 N04, Rome: N01 N03 N05. Select the whole Oslo group's rows.
        g.Selection.SetSelectedItems(g.Items.Where(i => i.City == "Oslo").Cast<object>());
        g.Select(1, 0);

        g.Key(KumikoKey.Delete);

        Assert.Equal(new[] { "N01", "N03", "N05" }, Names(g));
        Assert.Equal("N01", CurrentName(g)); // the first data row after the emptied group
        Assert.False(g.Source.IsNonDataRow(g.Selection.CurrentCell.Row));
    }

    // ── Announcement ──

    [Fact]
    public void TheDelete_IsAnnounced()
    {
        var g = Grid();
        var said = new List<string>();
        g.Input.AnnouncementRequested += said.Add;
        SelectRows(g, 2, 3, 4);

        g.Key(KumikoKey.Delete);
        SelectRows(g, 0);
        g.Key(KumikoKey.Delete);

        Assert.Equal(new[] { "3 rows deleted", "Row deleted" }, said);
    }

    [Fact]
    public void ARefusedDelete_IsNotAnnounced()
    {
        var g = Grid();
        var said = new List<string>();
        g.Input.AnnouncementRequested += said.Add;
        g.Input.RowsDeleting += (_, e) => e.Cancel = true;
        SelectRows(g, 2);

        g.Key(KumikoKey.Delete);

        Assert.Empty(said);
    }
}

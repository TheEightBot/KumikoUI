using KumikoUI.Core.Components;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Tests.Input;

/// <summary>
/// The built-in row menu (#331): a right click, a long press and the context-menu key open it only when it is enabled
/// and no <see cref="KumikoInputController.ContextRequested"/> handler took the request; each default item acts on the
/// selection; <see cref="KumikoInputController.RowMenuOpening"/> edits and cancels.
/// </summary>
public class RowMenuTests
{
    private sealed class FakeClipboard : IKumikoClipboard
    {
        public string? Text { get; private set; }

        public bool TrySetText(string text)
        {
            Text = text;
            return true;
        }

        public Task<string?> GetTextAsync() => Task.FromResult(Text);
    }

    private static InputGrid Grid(bool enabled = true, Action<InputGrid>? configure = null, int rows = 12) =>
        new(rows, viewportHeight: 600, viewportWidth: 600, configure: g =>
        {
            g.Input.IsRowMenuEnabled = enabled;
            g.Input.Clipboard = new FakeClipboard();
            configure?.Invoke(g);
        });

    private const float CellX = 250f; // the City column (x 200 to 300)
    private static float Y(InputGrid g, int row) => g.RowY(row);

    private static void RightClick(InputGrid g, float x, float y)
    {
        g.Pointer(InputAction.Pressed, x, y, button: PointerButton.Secondary);
        g.Pointer(InputAction.Released, x, y, button: PointerButton.Secondary);
    }

    private static void TouchLongPress(InputGrid g, float x, float y)
    {
        g.Pointer(InputAction.Pressed, x, y, deviceType: KumikoPointerDeviceType.Touch);
        g.Pointer(InputAction.LongPress, x, y, deviceType: KumikoPointerDeviceType.Touch);
        g.Pointer(InputAction.Released, x, y, deviceType: KumikoPointerDeviceType.Touch);
    }

    private static DrawnMenu OpenMenu(InputGrid g, int row = 2)
    {
        RightClick(g, CellX, Y(g, row));
        return Assert.IsType<DrawnMenu>(g.Input.ActivePopup);
    }

    private static string[] Keys(DrawnMenu menu) => menu.Items.Select(i => i.IsSeparator ? "-" : i.Key).ToArray();

    private static DrawnMenuItem ItemOf(DrawnMenu menu, string key) => menu.Items.Single(i => i.Key == key);

    /// <summary>Clicks the item with <paramref name="key"/> where the menu draws it.</summary>
    private static void Choose(InputGrid g, DrawnMenu menu, string key)
    {
        int index = menu.Items.IndexOf(ItemOf(menu, key));
        var bounds = menu.ItemBounds(index);
        g.Tap(bounds.X + 30, bounds.Y + bounds.Height / 2);
    }

    private static void SelectRows(InputGrid g, params int[] rows) =>
        g.Selection.SetSelectedItems(rows.Select(r => (object)g.Items[r]));

    // ── Opening ──────────────────────────────────────────────────

    [Fact]
    public void RightClick_OnACell_OpensNoMenu_WhileTheRowMenuIsDisabled()
    {
        var g = Grid(enabled: false);

        RightClick(g, CellX, Y(g, 2));

        Assert.Null(g.Input.ActivePopup);
    }

    [Fact]
    public void RightClick_OnACell_RaisesContextRequestedFirst_ThenOpensTheMenuAtThePointer_OnTheSelectedRow()
    {
        var g = Grid();
        bool menuOpenDuringEvent = true;
        g.Input.ContextRequested += (_, _) => menuOpenDuringEvent = g.Input.ActivePopup != null;

        var menu = OpenMenu(g, row: 3);

        Assert.False(menuOpenDuringEvent);
        Assert.Equal(CellX, menu.Bounds.X);
        Assert.Equal(Y(g, 3), menu.Bounds.Y);
        Assert.Equal(new[] { 3 }, g.Selection.SelectedRows);
        Assert.Equal(DrawnMenu.DefaultItemHeight, menu.ItemHeight);
    }

    [Fact]
    public void ContextRequested_MarkedHandled_KeepsTheRowMenuClosed()
    {
        var g = Grid();
        g.Input.ContextRequested += (_, e) => e.Handled = true;

        RightClick(g, CellX, Y(g, 2));

        Assert.Null(g.Input.ActivePopup);
    }

    [Fact]
    public void RightClick_OnAHeader_OrTheEmptyArea_OpensNoRowMenu()
    {
        var g = Grid(rows: 2);

        RightClick(g, CellX, InputGrid.HeaderY);
        Assert.Null(g.Input.ActivePopup);

        RightClick(g, CellX, Y(g, 8));
        Assert.Null(g.Input.ActivePopup);
    }

    [Theory]
    [InlineData(KumikoPointerDeviceType.Touch)]
    [InlineData(KumikoPointerDeviceType.Pen)]
    public void LongPress_OnACell_OpensTheMenu_WithTouchSizedItems(KumikoPointerDeviceType device)
    {
        var g = Grid();

        g.Pointer(InputAction.Pressed, CellX, Y(g, 2), deviceType: device);
        g.Pointer(InputAction.LongPress, CellX, Y(g, 2), deviceType: device);
        g.Pointer(InputAction.Released, CellX, Y(g, 2), deviceType: device);

        var menu = Assert.IsType<DrawnMenu>(g.Input.ActivePopup);
        Assert.Equal(device == KumikoPointerDeviceType.Touch ? DrawnMenu.TouchItemHeight : DrawnMenu.DefaultItemHeight, menu.ItemHeight);
        Assert.Equal(new[] { 2 }, g.Selection.SelectedRows);
    }

    [Fact]
    public void LongPress_WhileTheRowMenuIsDisabled_OpensNoMenu()
    {
        var g = Grid(enabled: false);

        TouchLongPress(g, CellX, Y(g, 2));

        Assert.Null(g.Input.ActivePopup);
    }

    [Fact]
    public void MenuKey_OpensTheMenuOfTheCurrentRow_WithTheFirstItemHighlighted()
    {
        var g = Grid();
        g.Select(4, 1);

        var e = g.Key(KumikoKey.ContextMenu);

        Assert.True(e.Handled);
        var menu = Assert.IsType<DrawnMenu>(g.Input.ActivePopup);
        Assert.Equal(0, menu.HighlightedIndex);
        Assert.True(menu.Bounds.Y >= Y(g, 4)); // below the current row's cell
    }

    [Fact]
    public void MenuKey_WhileTheRowMenuIsDisabled_OpensNoRowMenu_AndWithNoCurrentRowFallsBackToTheColumnMenu()
    {
        var g = Grid(enabled: false);
        g.Select(4, 1);
        Assert.False(g.Key(KumikoKey.ContextMenu).Handled);

        var enabled = Grid();
        enabled.Input.IsColumnMenuEnabled = true;
        Assert.True(enabled.Key(KumikoKey.ContextMenu).Handled); // no current row: the column menu
        Assert.Contains(KumikoColumnMenuItems.SortAscending, ((DrawnMenu)enabled.Input.ActivePopup!).Items.Select(i => i.Key));
    }

    [Fact]
    public void MenuKey_WithBothMenusOn_OpensTheRowMenu_WhileARowIsCurrent()
    {
        var g = Grid(configure: x => x.Input.IsColumnMenuEnabled = true);
        g.Select(4, 1);

        g.Key(KumikoKey.ContextMenu);

        var menu = Assert.IsType<DrawnMenu>(g.Input.ActivePopup);
        Assert.Contains(KumikoRowMenuItems.Copy, menu.Items.Select(i => i.Key));
    }

    // ── Touch with row drag ──────────────────────────────────────

    [Fact]
    public void TouchLongPress_OnADraggableRow_LiftedWithoutMoving_OpensTheMenu_InsteadOfDroppingTheRow()
    {
        var g = Grid(configure: x => x.Style.AllowRowDragDrop = true);
        string first = g.Items[0].Name;

        TouchLongPress(g, CellX, Y(g, 2));

        Assert.IsType<DrawnMenu>(g.Input.ActivePopup);
        Assert.Equal(first, g.Items[0].Name);
        Assert.Equal(new[] { 2 }, g.Selection.SelectedRows);
    }

    [Fact]
    public void TouchLongPress_OnADraggableRow_ThenMoving_StillDragsTheRow_AndOpensNoMenu()
    {
        var g = Grid(configure: x => x.Style.AllowRowDragDrop = true);

        g.Pointer(InputAction.Pressed, CellX, Y(g, 2), deviceType: KumikoPointerDeviceType.Touch);
        g.Pointer(InputAction.LongPress, CellX, Y(g, 2), deviceType: KumikoPointerDeviceType.Touch);
        g.Pointer(InputAction.Moved, CellX, Y(g, 5), deviceType: KumikoPointerDeviceType.Touch);
        g.Pointer(InputAction.Released, CellX, Y(g, 5), deviceType: KumikoPointerDeviceType.Touch);

        Assert.Null(g.Input.ActivePopup);
    }

    [Fact]
    public void TouchLongPress_OnADraggableRow_WithTheRowMenuOff_KeepsTheRowDragBehavior()
    {
        var g = Grid(enabled: false, configure: x => x.Style.AllowRowDragDrop = true);

        TouchLongPress(g, CellX, Y(g, 2));

        Assert.Null(g.Input.ActivePopup);
    }

    // ── The default items ────────────────────────────────────────

    [Fact]
    public void ADataRowMenu_HoldsCopyAndCopyWithHeaders_AndNoEditOrDeleteByDefaultForADeleteOffGrid()
    {
        var g = Grid(configure: x => x.Edit.IsReadOnly = true);

        var menu = OpenMenu(g);

        Assert.Equal(new[] { KumikoRowMenuItems.Copy, KumikoRowMenuItems.CopyWithHeaders }, Keys(menu));
        Assert.Equal("Copy", ItemOf(menu, KumikoRowMenuItems.Copy).Text);
        Assert.Equal("Copy with headers", ItemOf(menu, KumikoRowMenuItems.CopyWithHeaders).Text);
    }

    [Fact]
    public void Copy_PutsTheSelectedRowsOnTheClipboard_AsCtrlCDoes()
    {
        var g = Grid();
        var menu = OpenMenu(g, row: 2);
        var clipboard = (FakeClipboard)g.Input.Clipboard!;

        Choose(g, menu, KumikoRowMenuItems.Copy);
        string fromMenu = clipboard.Text!;
        g.Key(KumikoKey.C, InputModifiers.Control);

        Assert.Equal(clipboard.Text, fromMenu);
        Assert.StartsWith("N02", fromMenu);
        Assert.Null(g.Input.ActivePopup);
    }

    [Fact]
    public void CopyWithHeaders_AddsTheHeaderRow_WhateverCopyIncludesHeadersSays_AndCopyDoesNot()
    {
        var g = Grid(configure: x => x.Input.CopyIncludesHeaders = true);
        var clipboard = (FakeClipboard)g.Input.Clipboard!;
        var menu = OpenMenu(g, row: 2);

        Choose(g, menu, KumikoRowMenuItems.CopyWithHeaders);
        string withHeaders = clipboard.Text!;
        g.Input.CopyIncludesHeaders = false;
        g.Key(KumikoKey.C, InputModifiers.Control);
        string asKeyCopies = clipboard.Text!;

        Assert.StartsWith("Name\tAge\tCity\tDept\tScore\r\nN02", withHeaders);
        Assert.Equal(KumikoTsvFormatter.Format(g.Source, new[] { 2 }, includeHeaders: true), withHeaders);

        var again = OpenMenu(g, row: 2);
        Choose(g, again, KumikoRowMenuItems.Copy);
        Assert.Equal(asKeyCopies, clipboard.Text);
        Assert.StartsWith("N02", clipboard.Text);
    }

    [Fact]
    public void Copy_CopiesEveryRowOfAMultiRowSelection_WhenTheClickedRowIsSelected()
    {
        var g = Grid();
        SelectRows(g, 1, 2, 3);
        var menu = OpenMenu(g, row: 2);
        var clipboard = (FakeClipboard)g.Input.Clipboard!;

        Choose(g, menu, KumikoRowMenuItems.Copy);

        Assert.Equal(3, clipboard.Text!.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public void Copy_IsAnnounced_WithTheNumberOfRows()
    {
        var g = Grid();
        var said = new List<string>();
        g.Input.AnnouncementRequested += said.Add;
        SelectRows(g, 1, 2);
        var menu = OpenMenu(g, row: 2);
        said.Clear();

        Choose(g, menu, KumikoRowMenuItems.Copy);

        Assert.Equal(new[] { "2 rows copied" }, said);
    }

    [Fact]
    public void TheMenu_IsAnnouncedWhenItOpens()
    {
        var g = Grid();
        var said = new List<string>();
        g.Input.AnnouncementRequested += said.Add;

        var menu = OpenMenu(g);

        Assert.Equal($"Row menu, {menu.SelectableCount} items", Assert.Single(said));
    }

    [Fact]
    public void CopyItems_AreDisabled_WithoutAClipboard_AndEnabledWithACopyHandler()
    {
        var g = Grid(configure: x => x.Input.Clipboard = null);
        var menu = OpenMenu(g);
        Assert.False(ItemOf(menu, KumikoRowMenuItems.Copy).IsEnabled);
        Assert.False(ItemOf(menu, KumikoRowMenuItems.CopyWithHeaders).IsEnabled);

        var handled = Grid(configure: x =>
        {
            x.Input.Clipboard = null;
            x.Input.ClipboardCopyRequested += (_, e) => e.Handled = true;
        });
        Assert.True(ItemOf(OpenMenu(handled), KumikoRowMenuItems.Copy).IsEnabled);
    }

    [Fact]
    public void Edit_IsShownForAnEditableCell_AndOpensItsEditor()
    {
        var g = Grid();
        var menu = OpenMenu(g, row: 2);

        Choose(g, menu, KumikoRowMenuItems.Edit);

        Assert.True(g.Edit.IsEditing);
        Assert.Equal(new CellPosition(2, 2), g.Selection.CurrentCell);
    }

    [Fact]
    public void Edit_IsHidden_ForAReadOnlyColumn_AReadOnlyGrid_AndColumnsThatCannotBeEdited()
    {
        var readOnlyColumn = Grid(configure: x => x.City.IsReadOnly = true);
        Assert.DoesNotContain(KumikoRowMenuItems.Edit, OpenMenu(readOnlyColumn).Items.Select(i => i.Key));

        var readOnlyGrid = Grid(configure: x => x.Edit.IsReadOnly = true);
        Assert.DoesNotContain(KumikoRowMenuItems.Edit, OpenMenu(readOnlyGrid).Items.Select(i => i.Key));

        var noEditor = Grid(configure: x => x.City.EditTriggers = EditTrigger.None);
        Assert.DoesNotContain(KumikoRowMenuItems.Edit, OpenMenu(noEditor).Items.Select(i => i.Key));

        var progress = Grid(configure: x => x.City.ColumnType = KumikoColumnType.ProgressBar);
        Assert.DoesNotContain(KumikoRowMenuItems.Edit, OpenMenu(progress).Items.Select(i => i.Key));

        var noSession = new InputGrid(withEditSession: false, viewportHeight: 600, viewportWidth: 600, configure: x =>
        {
            x.Input.IsRowMenuEnabled = true;
            x.Input.Clipboard = new FakeClipboard();
        });
        Assert.DoesNotContain(KumikoRowMenuItems.Edit, OpenMenu(noSession).Items.Select(i => i.Key));
    }

    [Fact]
    public void Delete_IsHidden_WithoutAllowDeleting_AndInAReadOnlyGrid()
    {
        var off = Grid();
        Assert.DoesNotContain(KumikoRowMenuItems.Delete, OpenMenu(off).Items.Select(i => i.Key));

        var readOnly = Grid(configure: x =>
        {
            x.Input.AllowDeleting = true;
            x.Edit.IsReadOnly = true;
        });
        Assert.DoesNotContain(KumikoRowMenuItems.Delete, OpenMenu(readOnly).Items.Select(i => i.Key));
    }

    [Fact]
    public void Delete_RemovesTheSelectedRow_ThroughRowsDeleting()
    {
        var g = Grid(configure: x => x.Input.AllowDeleting = true);
        var triggers = new List<int>();
        g.Input.RowsDeleting += (_, e) => triggers.Add(e.Items.Count);
        var menu = OpenMenu(g, row: 2);
        Assert.Equal("Delete", ItemOf(menu, KumikoRowMenuItems.Delete).Text);

        Choose(g, menu, KumikoRowMenuItems.Delete);

        Assert.Equal(new[] { 1 }, triggers);
        Assert.Equal(11, g.Items.Count);
        Assert.DoesNotContain(g.Items, i => i.Name == "N02");
    }

    [Fact]
    public void Delete_ShowsTheNumberOfSelectedRows_AndDeletesThemAll()
    {
        var g = Grid(configure: x => x.Input.AllowDeleting = true);
        SelectRows(g, 1, 2, 3);
        var menu = OpenMenu(g, row: 2);
        Assert.Equal("Delete 3 rows", ItemOf(menu, KumikoRowMenuItems.Delete).Text);

        Choose(g, menu, KumikoRowMenuItems.Delete);

        Assert.Equal(9, g.Items.Count);
    }

    [Fact]
    public void Delete_CanceledInRowsDeleting_KeepsTheRows()
    {
        var g = Grid(configure: x =>
        {
            x.Input.AllowDeleting = true;
            x.Input.RowsDeleting += (_, e) => e.Cancel = true;
        });
        var menu = OpenMenu(g, row: 2);

        Choose(g, menu, KumikoRowMenuItems.Delete);

        Assert.Equal(12, g.Items.Count);
    }

    // ── Group header rows ────────────────────────────────────────

    private static InputGrid Grouped(Action<InputGrid>? configure = null) =>
        new(rows: 6, viewportHeight: 500, viewportWidth: 600, configure: g =>
        {
            g.Input.IsRowMenuEnabled = true;
            g.Input.Clipboard = new FakeClipboard();
            g.Source.AddGroupDescription(new GroupDescription("City"));
            configure?.Invoke(g);
        });

    private const float GroupRowY = 98f; // group panel (40) + header (40) + half a row

    [Fact]
    public void RightClick_OnAGroupHeaderRow_OffersExpandAllAndCollapseAll()
    {
        var g = Grouped();
        RightClick(g, CellX, GroupRowY);

        var menu = Assert.IsType<DrawnMenu>(g.Input.ActivePopup);
        Assert.Equal(new[] { KumikoRowMenuItems.ExpandAllGroups, KumikoRowMenuItems.CollapseAllGroups }, Keys(menu));

        int expanded = g.Source.RowCount;
        Choose(g, menu, KumikoRowMenuItems.CollapseAllGroups);
        Assert.True(g.Source.RowCount < expanded);

        RightClick(g, CellX, GroupRowY);
        Choose(g, Assert.IsType<DrawnMenu>(g.Input.ActivePopup), KumikoRowMenuItems.ExpandAllGroups);
        Assert.Equal(expanded, g.Source.RowCount);
    }

    [Fact]
    public void RightClick_OnAGroupChevron_OpensTheGroupMenuToo_AndRaisesTheOpeningWithNoItem()
    {
        var g = Grouped();
        KumikoRowMenuOpeningEventArgs? opening = null;
        g.Input.RowMenuOpening += (_, e) => opening = e;

        RightClick(g, 15, GroupRowY);

        Assert.IsType<DrawnMenu>(g.Input.ActivePopup);
        Assert.NotNull(opening);
        Assert.Null(opening!.DataItem);
        Assert.Equal(HitRegion.GroupHeaderRow, opening.Region);
        Assert.Equal(0, opening.RowIndex);
    }

    [Fact]
    public void ADataRowInAGroupedGrid_StillGetsTheDataRowMenu()
    {
        var g = Grouped();
        RightClick(g, CellX, GroupRowY + g.Style.RowHeight);

        var menu = Assert.IsType<DrawnMenu>(g.Input.ActivePopup);
        Assert.Contains(KumikoRowMenuItems.Copy, menu.Items.Select(i => i.Key));
    }

    // ── RowMenuItems, swipe items and RowMenuOpening ─────────────

    [Fact]
    public void RowMenuItems_FollowASeparator_AndTheirCommandGetsTheRowsItem()
    {
        var g = Grid();
        object? received = null;
        var command = new DelegateCommand(p => received = p);
        g.Input.RowMenuItems.Add(new DrawnMenuItem("Raise", "Raise") { Command = command });
        g.Input.RowMenuItems.Add(new DrawnMenuItem("Fixed", "Fixed") { Command = command, CommandParameter = "x" });

        var menu = OpenMenu(g, row: 4);

        Assert.Equal(new[] { KumikoRowMenuItems.Copy, KumikoRowMenuItems.CopyWithHeaders, "-", KumikoRowMenuItems.Edit, "-", "Raise", "Fixed" }, Keys(menu));
        Choose(g, menu, "Raise");
        Assert.Same(g.Items[4], received);

        var again = OpenMenu(g, row: 4);
        Choose(g, again, "Fixed");
        Assert.Equal("x", received);
    }

    [Fact]
    public void RowMenuItems_AlsoShowOnAGroupHeaderRow_WithANullParameter()
    {
        var g = Grouped();
        object? received = new object();
        g.Input.RowMenuItems.Add(new DrawnMenuItem("Mine", "Mine") { Command = new DelegateCommand(p => received = p) });
        RightClick(g, CellX, GroupRowY);

        var menu = Assert.IsType<DrawnMenu>(g.Input.ActivePopup);
        Choose(g, menu, "Mine");

        Assert.Null(received);
    }

    [Fact]
    public void SwipeItems_AreOfferedInTheMenu_AndRunOnTheRowsItem()
    {
        var g = Grid(configure: x => x.Edit.IsReadOnly = true);
        object? flagged = null;
        g.Input.LeadingSwipeItems.Add(new ActionButtonDefinition { Label = "Flag", Command = new DelegateCommand(p => flagged = p) });
        g.Input.TrailingSwipeItems.Add(new ActionButtonDefinition { Label = "Archive", Action = () => { } });
        g.Input.TrailingSwipeItems.Add(new ActionButtonDefinition { Label = "Stuck", Command = new DelegateCommand(_ => { }, canExecute: false) });

        var menu = OpenMenu(g, row: 3);

        Assert.Equal(new[]
        {
            KumikoRowMenuItems.Copy, KumikoRowMenuItems.CopyWithHeaders, "-",
            "Swipe:Leading:0", "-", "Swipe:Trailing:0", "Swipe:Trailing:1",
        }, Keys(menu));
        Assert.Equal("Flag", ItemOf(menu, "Swipe:Leading:0").Text);
        Assert.False(ItemOf(menu, "Swipe:Trailing:1").IsEnabled);
        Choose(g, menu, "Swipe:Leading:0");
        Assert.Same(g.Items[3], flagged);
    }

    [Fact]
    public void RowMenuOpening_CarriesTheRow_AndItsItemsCanBeEditedAndTheMenuCanceled()
    {
        var g = Grid();
        KumikoRowMenuOpeningEventArgs? seen = null;
        g.Input.RowMenuOpening += (_, e) =>
        {
            seen = e;
            e.Items.Remove(e.Items.Single(i => i.Key == KumikoRowMenuItems.CopyWithHeaders));
            e.Items.Add(new DrawnMenuItem("Added", "Added"));
        };

        var menu = OpenMenu(g, row: 5);

        Assert.NotNull(seen);
        Assert.Same(g.Items[5], seen!.DataItem);
        Assert.Equal(5, seen.RowIndex);
        Assert.Same(g.City, seen.Column);
        Assert.Equal(HitRegion.Cell, seen.Region);
        Assert.DoesNotContain(KumikoRowMenuItems.CopyWithHeaders, menu.Items.Select(i => i.Key));
        Assert.Equal("Added", menu.Items[^1].Key);

        g.Tap(2, 2); // closes it
        g.Input.RowMenuOpening += (_, e) => e.Cancel = true;
        RightClick(g, CellX, Y(g, 6));
        Assert.Null(g.Input.ActivePopup);
    }

    [Fact]
    public void RowMenuOpening_CanceledFromTheKeyboard_StillTakesTheKey()
    {
        var g = Grid(configure: x => x.Input.IsColumnMenuEnabled = true);
        g.Input.RowMenuOpening += (_, e) => e.Cancel = true;
        g.Select(3, 0);

        var e = g.Key(KumikoKey.ContextMenu);

        Assert.True(e.Handled);
        Assert.Null(g.Input.ActivePopup);
    }

    [Fact]
    public void RightClick_OnTheCellBeingEdited_OpensNoRowMenu()
    {
        var g = Grid();
        g.Select(2, 2);
        g.Key(KumikoKey.F2);
        Assert.True(g.Edit.IsEditing);

        RightClick(g, CellX, Y(g, 2));

        Assert.False(g.Input.ActivePopup is DrawnMenu);
    }

    private sealed class DelegateCommand : System.Windows.Input.ICommand
    {
        private readonly Action<object?> _execute;
        private readonly bool _canExecute;

        public DelegateCommand(Action<object?> execute, bool canExecute = true)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => _canExecute;

        public void Execute(object? parameter) => _execute(parameter);
    }
}

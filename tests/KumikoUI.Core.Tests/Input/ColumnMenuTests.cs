using KumikoUI.Core.Components;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.Input;

/// <summary>
/// The built-in column header menu (#319): a right click, a long press, the header's menu button and the context-menu
/// key open it only when it is enabled and no <see cref="KumikoInputController.ContextRequested"/> handler took the
/// request; each default item does its job; <see cref="KumikoInputController.ColumnMenuOpening"/> edits and cancels.
/// </summary>
public class ColumnMenuTests
{
    // Wide and tall enough for every column and the whole menu, so nothing is clamped or scrolled.
    private static InputGrid Grid(bool enabled = true, Action<InputGrid>? configure = null) =>
        new(viewportHeight: 600, viewportWidth: 600, configure: g =>
        {
            g.Input.IsColumnMenuEnabled = enabled;
            configure?.Invoke(g);
        });

    private static void RightClick(InputGrid g, float x, float y)
    {
        g.Pointer(InputAction.Pressed, x, y, button: PointerButton.Secondary);
        g.Pointer(InputAction.Released, x, y, button: PointerButton.Secondary);
    }

    /// <summary>The Age header (x 120 to 200), away from its glyphs.</summary>
    private const float AgeX = 140f;

    private static DrawnMenu OpenAgeMenu(InputGrid g)
    {
        RightClick(g, AgeX, InputGrid.HeaderY);
        return Assert.IsType<DrawnMenu>(g.Input.ActivePopup);
    }

    private static string[] Keys(DrawnMenu menu) => menu.Items.Select(i => i.IsSeparator ? "-" : i.Key).ToArray();

    private static DrawnMenuItem ItemOf(DrawnMenu menu, string key) => menu.Items.Single(i => i.Key == key);

    /// <summary>Clicks the item with <paramref name="key"/> where the menu draws it.</summary>
    private static void Choose(InputGrid g, DrawnMenu menu, string key)
    {
        int index = menu.Items.IndexOf(ItemOf(menu, key));
        var row = menu.ItemBounds(index);
        g.Tap(row.X + 30, row.Y + row.Height / 2);
    }

    // ── Opening ──────────────────────────────────────────────────

    [Fact]
    public void RightClick_OnAHeader_OpensNoMenu_WhileTheMenuIsDisabled()
    {
        var g = Grid(enabled: false);

        RightClick(g, AgeX, InputGrid.HeaderY);

        Assert.Null(g.Input.ActivePopup);
    }

    [Fact]
    public void RightClick_OnAHeader_RaisesContextRequestedFirst_ThenOpensTheMenuAtThePointer()
    {
        var g = Grid();
        bool menuOpenDuringEvent = true;
        g.Input.ContextRequested += (_, _) => menuOpenDuringEvent = g.Input.ActivePopup != null;

        var menu = OpenAgeMenu(g);

        Assert.False(menuOpenDuringEvent);
        Assert.Equal(AgeX, menu.Bounds.X);
        Assert.Equal(InputGrid.HeaderY, menu.Bounds.Y);
        Assert.Equal(DrawnMenu.DefaultItemHeight, menu.ItemHeight);
        Assert.Equal(-1, menu.HighlightedIndex);
        Assert.Equal(KumikoSortDirection.None, g.Age.SortDirection);
    }

    [Fact]
    public void RightClick_MarkedHandledByAHandler_OpensNoMenu()
    {
        var g = Grid();
        g.Input.ContextRequested += (_, e) => e.Handled = true;

        RightClick(g, AgeX, InputGrid.HeaderY);

        Assert.Null(g.Input.ActivePopup);
    }

    [Fact]
    public void RightClick_OnACell_OpensNoColumnMenu()
    {
        var g = Grid();

        RightClick(g, AgeX, g.RowY(2));

        Assert.Null(g.Input.ActivePopup);
    }

    [Fact]
    public void TouchLongPress_OnAHeader_OpensTheMenuWithTouchSizedItems_AndTheReleaseChoosesNothing()
    {
        var g = Grid(configure: x => x.Age.AllowReorder = false);
        var touch = KumikoPointerDeviceType.Touch;

        g.Pointer(InputAction.Pressed, AgeX, InputGrid.HeaderY, deviceType: touch);
        g.Pointer(InputAction.LongPress, AgeX, InputGrid.HeaderY, deviceType: touch);
        var menu = Assert.IsType<DrawnMenu>(g.Input.ActivePopup);
        g.Pointer(InputAction.Released, AgeX, InputGrid.HeaderY, deviceType: touch);

        Assert.Same(menu, g.Input.ActivePopup);
        Assert.Equal(DrawnMenu.TouchItemHeight, menu.ItemHeight);
        Assert.Equal(KumikoSortDirection.None, g.Age.SortDirection);
    }

    [Fact]
    public void TouchLongPress_OnAReorderableHeader_OpensTheMenuWhenLiftedWithoutMoving()
    {
        var g = Grid();
        var touch = KumikoPointerDeviceType.Touch;

        g.Pointer(InputAction.Pressed, AgeX, InputGrid.HeaderY, deviceType: touch);
        g.Pointer(InputAction.LongPress, AgeX, InputGrid.HeaderY, deviceType: touch);
        Assert.Null(g.Input.ActivePopup);
        g.Pointer(InputAction.Released, AgeX, InputGrid.HeaderY, deviceType: touch);

        Assert.IsType<DrawnMenu>(g.Input.ActivePopup);
    }

    [Fact]
    public void ContextMenuKey_OpensTheCurrentColumnsMenu_BelowItsHeader_WithTheFirstItemHighlighted()
    {
        var g = Grid();
        g.Select(2, 1);

        var key = g.Key(KumikoKey.ContextMenu);

        Assert.True(key.Handled);
        var menu = Assert.IsType<DrawnMenu>(g.Input.ActivePopup);
        Assert.Equal(0, menu.HighlightedIndex);
        Assert.Equal(g.Style.HeaderHeight, menu.Bounds.Y);
        Assert.Equal(120, menu.Bounds.X);
    }

    [Fact]
    public void ContextMenuKey_WhileTheMenuIsDisabled_IsLeftUnhandled()
    {
        var g = Grid(enabled: false);
        g.Select(2, 1);

        var key = g.Key(KumikoKey.ContextMenu);

        Assert.False(key.Handled);
        Assert.Null(g.Input.ActivePopup);
    }

    [Fact]
    public void ContextMenuKey_AgainClosesTheMenu()
    {
        var g = Grid();
        g.Select(2, 1);
        g.Key(KumikoKey.ContextMenu);

        g.Key(KumikoKey.ContextMenu);

        Assert.Null(g.Input.ActivePopup);
    }

    private static KumikoRect AgeMenuButton(InputGrid g) =>
        InteractionGeometry.MenuButtonRect(120, g.Age, g.Style, filterShown: true);

    [Fact]
    public void MenuButton_ATapOpensTheMenuBelowTheHeader_AndDoesNotSort()
    {
        var g = Grid(configure: x => x.Input.ShowColumnMenuButton = true);
        var button = AgeMenuButton(g);
        Assert.Equal(HitRegion.HeaderMenuButton, g.Input.HitTester.HitTest(button.X + 6, button.Y + 6,
            g.Scroll, g.Style, g.Source.Columns, g.Source.RowCount, g.Source).Region);

        g.Tap(button.X + 6, button.Y + 6);

        var menu = Assert.IsType<DrawnMenu>(g.Input.ActivePopup);
        Assert.Equal(g.Style.HeaderHeight, menu.Bounds.Y);
        Assert.Equal(KumikoSortDirection.None, g.Age.SortDirection);
    }

    [Fact]
    public void MenuButton_DoesNothing_WhileTheMenuIsDisabled()
    {
        var g = Grid(enabled: false, configure: x => x.Input.ShowColumnMenuButton = true);
        var button = AgeMenuButton(g);

        g.Tap(button.X + 6, button.Y + 6);

        Assert.Null(g.Input.ActivePopup);
        Assert.Equal(KumikoSortDirection.None, g.Age.SortDirection);
    }

    [Fact]
    public void MenuButton_IsNotHitTestable_WhenNotShown()
    {
        var g = Grid();
        var button = AgeMenuButton(g);

        var hit = g.Input.HitTester.HitTest(button.X + 6, button.Y + 6, g.Scroll, g.Style, g.Source.Columns,
            g.Source.RowCount, g.Source);

        Assert.Equal(HitRegion.HeaderCell, hit.Region);
    }

    [Fact]
    public void ShowColumnMenu_OpensBelowTheHeader_EvenWhenTheMenuIsDisabled()
    {
        var g = Grid(enabled: false);

        Assert.True(g.Input.ShowColumnMenu(g.City, g.Scroll, g.Selection, g.Style, g.Source));

        var menu = Assert.IsType<DrawnMenu>(g.Input.ActivePopup);
        Assert.Equal(200, menu.Bounds.X);
        Assert.Equal(0, menu.HighlightedIndex);
    }

    [Fact]
    public void ShowColumnMenu_ForAHiddenColumn_OpensNothing()
    {
        var g = Grid();
        g.City.IsVisible = false;

        Assert.False(g.Input.ShowColumnMenu(g.City, g.Scroll, g.Selection, g.Style, g.Source));
    }

    // ── Default items ────────────────────────────────────────────

    [Fact]
    public void DefaultItems_InOrder_WithSeparatorsBetweenTheGroups_AndTypeAwareSortLabels()
    {
        var g = Grid();

        var menu = OpenAgeMenu(g);

        Assert.Equal(new[]
        {
            KumikoColumnMenuItems.SortAscending, KumikoColumnMenuItems.SortDescending, KumikoColumnMenuItems.ClearSort,
            "-", KumikoColumnMenuItems.Filter, "-", KumikoColumnMenuItems.GroupBy, "-",
            KumikoColumnMenuItems.Hide, KumikoColumnMenuItems.AutoFit, KumikoColumnMenuItems.Columns,
        }, Keys(menu));
        Assert.Equal("Sort Smallest to Largest", ItemOf(menu, KumikoColumnMenuItems.SortAscending).Text);
        Assert.Equal("Sort Largest to Smallest", ItemOf(menu, KumikoColumnMenuItems.SortDescending).Text);
        Assert.False(ItemOf(menu, KumikoColumnMenuItems.ClearSort).IsEnabled); // nothing to clear
    }

    [Fact]
    public void DefaultItems_RespectTheGridAndColumnFlags()
    {
        var g = Grid(configure: x =>
        {
            x.Input.AllowSorting = false;
            x.Age.AllowFiltering = false;
            x.Age.AllowResize = false;
            x.Age.PropertyName = "";
        });

        var menu = OpenAgeMenu(g);

        Assert.Equal(new[] { KumikoColumnMenuItems.Hide, KumikoColumnMenuItems.Columns }, Keys(menu));
    }

    [Fact]
    public void DefaultItems_TheCurrentSortDirectionIsChecked()
    {
        var g = Grid();
        g.Source.SetSort(g.Age, KumikoSortDirection.Descending);

        var menu = OpenAgeMenu(g);

        Assert.Null(ItemOf(menu, KumikoColumnMenuItems.SortAscending).IsChecked);
        Assert.True(ItemOf(menu, KumikoColumnMenuItems.SortDescending).IsChecked);
        Assert.True(ItemOf(menu, KumikoColumnMenuItems.ClearSort).IsEnabled);
    }

    [Fact]
    public void DefaultItems_ALocalizedTable_ChangesTheTexts()
    {
        var g = Grid();
        g.Source.Strings = KumikoStrings.Default with { ColumnMenuHide = "Ausblenden" };

        var menu = OpenAgeMenu(g);

        Assert.Equal("Ausblenden", ItemOf(menu, KumikoColumnMenuItems.Hide).Text);
    }

    [Theory]
    [InlineData(KumikoColumnMenuItems.SortAscending, KumikoSortDirection.Ascending)]
    [InlineData(KumikoColumnMenuItems.SortDescending, KumikoSortDirection.Descending)]
    public void SortItems_SortByTheColumnAlone_AndCloseTheMenu(string key, KumikoSortDirection expected)
    {
        var g = Grid();
        g.Source.SetSort(g.Name, KumikoSortDirection.Ascending);
        var menu = OpenAgeMenu(g);

        Choose(g, menu, key);

        Assert.Equal(expected, g.Age.SortDirection);
        Assert.Equal(KumikoSortDirection.None, g.Name.SortDirection);
        Assert.Null(g.Input.ActivePopup);
    }

    [Fact]
    public void ClearSort_RemovesOnlyThisColumnFromTheSort()
    {
        var g = Grid();
        g.Source.SetSort(g.Name, KumikoSortDirection.Ascending, 1);
        g.Source.SetSort(g.Age, KumikoSortDirection.Descending, 2);
        var menu = OpenAgeMenu(g);

        Choose(g, menu, KumikoColumnMenuItems.ClearSort);

        Assert.Equal(KumikoSortDirection.None, g.Age.SortDirection);
        Assert.Equal(KumikoSortDirection.Ascending, g.Name.SortDirection);
    }

    [Fact]
    public void Filter_OpensTheColumnsFilterPopup()
    {
        var g = Grid();
        var menu = OpenAgeMenu(g);

        Choose(g, menu, KumikoColumnMenuItems.Filter);

        Assert.IsType<DrawnFilterPopup>(g.Input.ActivePopup);
    }

    [Fact]
    public void GroupBy_GroupsByTheColumn_AndUngroupStopsIt()
    {
        var g = Grid();
        var menu = OpenAgeMenu(g);

        Choose(g, menu, KumikoColumnMenuItems.GroupBy);
        Assert.Equal("Age", Assert.Single(g.Source.GroupDescriptions).PropertyName);

        menu = Assert.IsType<DrawnMenu>(OpenAgeMenuGrouped(g));
        Assert.Contains(KumikoColumnMenuItems.Ungroup, Keys(menu));
        Assert.DoesNotContain(KumikoColumnMenuItems.GroupBy, Keys(menu));
        Choose(g, menu, KumikoColumnMenuItems.Ungroup);
        Assert.Empty(g.Source.GroupDescriptions);
    }

    // With grouping on, the grouping panel pushes the header down.
    private static DrawnComponent? OpenAgeMenuGrouped(InputGrid g)
    {
        g.Render();
        RightClick(g, AgeX, g.Style.GroupPanelHeight + InputGrid.HeaderY);
        return g.Input.ActivePopup;
    }

    [Fact]
    public void Hide_HidesTheColumn_AndIsDisabledForTheLastVisibleColumn()
    {
        var g = Grid();
        var menu = OpenAgeMenu(g);

        Choose(g, menu, KumikoColumnMenuItems.Hide);
        Assert.False(g.Age.IsVisible);

        foreach (var c in new[] { g.City, g.Dept, g.Score }) c.IsVisible = false;
        g.Render();
        Assert.True(g.Input.ShowColumnMenu(g.Name, g.Scroll, g.Selection, g.Style, g.Source));
        var last = Assert.IsType<DrawnMenu>(g.Input.ActivePopup);
        Assert.False(ItemOf(last, KumikoColumnMenuItems.Hide).IsEnabled);
    }

    [Fact]
    public void AutoFit_RaisesAutoFitColumnRequested_ForTheColumn()
    {
        var g = Grid();
        var requests = new List<AutoFitColumnEventArgs>();
        g.Input.AutoFitColumnRequested += (_, e) => requests.Add(e);
        var menu = OpenAgeMenu(g);

        Choose(g, menu, KumikoColumnMenuItems.AutoFit);

        var e = Assert.Single(requests);
        Assert.Same(g.Age, e.Column);
        Assert.Equal(1, e.ColumnIndex);
    }

    [Fact]
    public void Keyboard_EnterChoosesTheHighlightedItem()
    {
        var g = Grid();
        g.Select(2, 1);
        g.Key(KumikoKey.ContextMenu);

        g.Key(KumikoKey.Enter); // the first item: sort ascending

        Assert.Equal(KumikoSortDirection.Ascending, g.Age.SortDirection);
        Assert.Null(g.Input.ActivePopup);
        Assert.Equal(1, g.Selection.CurrentCell.Column); // Enter did not reach the grid
    }

    [Fact]
    public void Keyboard_EscapeClosesTheMenu_AndTheGridKeepsItsSelection()
    {
        var g = Grid();
        g.Select(2, 1);
        g.Key(KumikoKey.ContextMenu);

        var key = g.Key(KumikoKey.Escape);

        Assert.True(key.Handled);
        Assert.Null(g.Input.ActivePopup);
        Assert.Equal(new CellPosition(2, 1), g.Selection.CurrentCell);
    }

    [Fact]
    public void Keyboard_ArrowsMoveInTheMenu_NotTheGrid()
    {
        var g = Grid();
        g.Select(2, 1);
        g.Key(KumikoKey.ContextMenu);

        g.Key(KumikoKey.Down);

        Assert.Equal(new CellPosition(2, 1), g.Selection.CurrentCell);
        Assert.Equal(1, Assert.IsType<DrawnMenu>(g.Input.ActivePopup).HighlightedIndex);
    }

    // ── Closing ──────────────────────────────────────────────────

    [Fact]
    public void APressOutside_ClosesTheMenuWithoutActing_AndRaisesClosed()
    {
        var g = Grid();
        var menu = OpenAgeMenu(g);
        int closed = 0, invoked = 0;
        menu.Closed += (_, _) => closed++;
        menu.ItemInvoked += (_, _) => invoked++;

        g.Tap(10, InputGrid.HeaderY); // the Name header, outside the menu

        Assert.Null(g.Input.ActivePopup);
        Assert.Equal(1, closed);
        Assert.Equal(0, invoked);
        Assert.Equal(KumikoSortDirection.None, g.Name.SortDirection); // the press only closed the menu
    }

    [Fact]
    public void ChoosingAnItem_RaisesItemInvokedAndClosed()
    {
        var g = Grid();
        var menu = OpenAgeMenu(g);
        var events = new List<string>();
        menu.ItemInvoked += (_, i) => events.Add("invoked:" + i.Key);
        menu.Closed += (_, _) => events.Add("closed");

        Choose(g, menu, KumikoColumnMenuItems.SortAscending);

        Assert.Equal(new[] { "closed", "invoked:SortAscending" }, events);
    }

    [Fact]
    public void DisablingTheGrid_ClosesTheMenu()
    {
        var g = Grid();
        OpenAgeMenu(g);

        g.Input.IsEnabled = false;

        Assert.Null(g.Input.ActivePopup);
    }

    // ── ColumnMenuOpening ────────────────────────────────────────

    [Fact]
    public void ColumnMenuOpening_CanRemoveReorderAndAddItems()
    {
        var g = Grid();
        int custom = 0;
        g.Input.ColumnMenuOpening += (_, e) =>
        {
            Assert.Same(g.Age, e.Column);
            Assert.Equal(1, e.ColumnIndex);
            var hide = e.Items.Single(i => i.Key == KumikoColumnMenuItems.Hide);
            e.Items.Remove(hide);
            e.Items.Insert(0, hide);
            for (int i = e.Items.Count - 1; i >= 0; i--)
                if (e.Items[i].Key == KumikoColumnMenuItems.Filter) e.Items.RemoveAt(i);
            e.Items.Add(DrawnMenuItem.Separator());
            e.Items.Add(new DrawnMenuItem("Custom", "Custom", () => custom++));
        };

        var menu = OpenAgeMenu(g);

        Assert.Equal(KumikoColumnMenuItems.Hide, menu.Items[0].Key);
        Assert.DoesNotContain(KumikoColumnMenuItems.Filter, Keys(menu));
        Choose(g, menu, "Custom");
        Assert.Equal(1, custom);
    }

    [Fact]
    public void ColumnMenuOpening_Cancel_KeepsTheMenuClosed()
    {
        var g = Grid();
        g.Input.ColumnMenuOpening += (_, e) => e.Cancel = true;

        RightClick(g, AgeX, InputGrid.HeaderY);

        Assert.Null(g.Input.ActivePopup);
    }

    [Fact]
    public void ColumnMenuOpening_RemovingEveryItem_KeepsTheMenuClosed()
    {
        var g = Grid();
        g.Input.ColumnMenuOpening += (_, e) => e.Items.Clear();

        RightClick(g, AgeX, InputGrid.HeaderY);

        Assert.Null(g.Input.ActivePopup);
    }

    // ── Screen reader ────────────────────────────────────────────

    [Fact]
    public void Announcements_TheMenuOpening_AndEachItemTheKeyboardReaches()
    {
        var g = Grid();
        var said = new List<string>();
        g.Input.AnnouncementRequested += said.Add;
        g.Source.SetSort(g.Age, KumikoSortDirection.Ascending);
        g.Select(2, 1);

        g.Key(KumikoKey.ContextMenu);
        g.Key(KumikoKey.Down);
        g.Key(KumikoKey.Down);

        Assert.Equal(new[]
        {
            "Age column menu, 8 items",          // eleven entries less three separators
            "Sort Smallest to Largest, checked",
            "Sort Largest to Smallest",
            "Clear Sort",
        }, said);
    }

    [Fact]
    public void Announcements_ADisabledItem_IsReadAsUnavailable()
    {
        var item = new DrawnMenuItem("k", "Hide Column") { IsEnabled = false };

        Assert.Equal("Hide Column, unavailable", KumikoAnnouncements.MenuItem(KumikoStrings.Default, item));
    }

    // ── Drawing ──────────────────────────────────────────────────

    [Fact]
    public void TheMenuButton_IsDrawnLeftOfTheFilterIcon_AndTheHeaderTextKeepsRoomForIt()
    {
        var g = Grid(configure: x =>
        {
            x.Input.ShowColumnMenuButton = true;
            x.Renderer.ShowColumnMenuButton = true;
        });
        var icons = new RecordingIconProvider();
        g.Style.IconProvider = icons;

        var ctx = g.RenderRecorded();

        var buttons = icons.Calls.Where(c => c.Icon == KumikoIcon.ColumnMenu).ToList();
        Assert.Equal(5, buttons.Count); // one per column
        var ageButton = buttons.Single(b => b.Bounds.X > 120 && b.Bounds.X < 200);
        var ageFilter = icons.Calls.Single(c => c.Icon == KumikoIcon.Filter && c.Bounds.X > 120 && c.Bounds.X < 200);
        Assert.True(ageButton.Bounds.Right < ageFilter.Bounds.X);
        var ageText = ctx.SingleText("Age");
        Assert.True(ageText.Rect.Right <= ageButton.Bounds.X, $"{ageText.Rect} vs {ageButton.Bounds}");
    }
}

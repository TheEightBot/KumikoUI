using KumikoUI.Core.Components;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Tests.Input;

/// <summary>
/// The column chooser's entry points (#335, spec #106): <see cref="KumikoInputController.ShowColumnChooser"/>, the column
/// menu's Columns... item, an unhandled header context request and the shortcut; one popup at a time; a press outside
/// and Escape close it; an open edit is committed before a column changes.
/// </summary>
public class ColumnChooserInputTests
{
    private static readonly KumikoShortcut CtrlAltC = new(KumikoKey.C, InputModifiers.Control | InputModifiers.Alt);

    private static InputGrid Grid(Action<InputGrid>? configure = null) =>
        new(viewportHeight: 600, viewportWidth: 600, configure: configure);

    private static DrawnColumnChooser Open(InputGrid g, KumikoRect? anchor = null)
    {
        g.Input.ShowColumnChooser(g.Scroll, g.Selection, g.Style, g.Source, anchor);
        g.RenderRecorded(); // places the popup
        return Assert.IsType<DrawnColumnChooser>(g.Input.ActivePopup);
    }

    private static void RightClick(InputGrid g, float x, float y)
    {
        g.Pointer(InputAction.Pressed, x, y, button: PointerButton.Secondary);
        g.Pointer(InputAction.Released, x, y, button: PointerButton.Secondary);
    }

    private static void TapFilterIcon(InputGrid g, KumikoColumn column)
    {
        float x = KumikoInputController.HeaderScreenX(column, g.Scroll, g.Source, g.Style);
        g.Tap(x + column.Width - g.Style.CellPadding - 5, InputGrid.HeaderY);
    }

    // ── Opening and closing ──────────────────────────────────────

    [Fact]
    public void ShowColumnChooser_OpensAtTheTopRightBelowTheHeader_WithTheKeyboard()
    {
        var g = Grid();
        int opened = 0, focus = 0;
        g.Input.ColumnChooserOpened += (_, _) => opened++;
        g.Input.KeyboardFocusRequested += (_, _) => focus++;

        var chooser = Open(g);

        Assert.True(g.Input.IsColumnChooserOpen);
        Assert.Equal(1, opened);
        Assert.True(focus > 0);
        Assert.True(chooser.IsFocused);
        Assert.Same(chooser.SearchTextInput, g.Input.ActiveTextInput);
        Assert.True(chooser.Bounds.Right <= 600 - 4);
        Assert.True(chooser.Bounds.Y >= g.Style.HeaderHeight);
        Assert.True(g.Input.WouldHandleTab(g.Selection, g.Source, backwards: false));
    }

    [Fact]
    public void ShowColumnChooser_AtAnAnchor_OpensBelowIt()
    {
        var g = Grid();

        var chooser = Open(g, new KumikoRect(20, 100, 80, 30));

        Assert.Equal(20f, chooser.Bounds.X);
        Assert.Equal(130f, chooser.Bounds.Y);
    }

    [Fact]
    public void ShowColumnChooser_NeverTallerThanTheViewport()
    {
        var g = new InputGrid(viewportHeight: 220, viewportWidth: 600, configure: x =>
            x.Source.SetColumns(Enumerable.Range(0, 40).Select(i => new KumikoColumn { Header = $"C{i}", PropertyName = "Name" })));

        var chooser = Open(g);

        Assert.True(chooser.Bounds.Y >= g.Style.HeaderHeight);
        Assert.True(chooser.Bounds.Bottom <= 220.01f);
    }

    [Fact]
    public void ShowColumnChooser_WhenOpen_KeepsTheSameChooser()
    {
        var g = Grid();
        var chooser = Open(g);

        var again = Open(g);

        Assert.Same(chooser, again);
    }

    [Fact]
    public void ShowColumnChooser_DoesNothing_WhileTheGridIsDisabled()
    {
        var g = Grid();
        g.Input.IsEnabled = false;

        g.Input.ShowColumnChooser(g.Scroll, g.Selection, g.Style, g.Source);

        Assert.False(g.Input.IsColumnChooserOpen);
    }

    [Fact]
    public void APressOutside_ClosesIt_AndDoesNothingElse()
    {
        var g = Grid();
        Open(g);
        int closed = 0;
        g.Input.ColumnChooserClosed += (_, _) => closed++;
        g.Select(0, 0);

        g.Tap(20, g.RowY(3));

        Assert.False(g.Input.IsColumnChooserOpen);
        Assert.Equal(1, closed);
        Assert.Equal(new CellPosition(0, 0), g.Selection.CurrentCell);
    }

    [Fact]
    public void Escape_ClosesIt_AndTheGridKeepsItsCurrentCell()
    {
        var g = Grid();
        g.Select(2, 1);
        Open(g);

        g.Key(KumikoKey.Escape);

        Assert.False(g.Input.IsColumnChooserOpen);
        Assert.Equal(new CellPosition(2, 1), g.Selection.CurrentCell);
        Assert.Null(g.Input.ActiveTextInput);
    }

    [Fact]
    public void CloseColumnChooser_ClosesIt_AndDisablingTheGridDoesToo()
    {
        var g = Grid();
        int closed = 0;
        g.Input.ColumnChooserClosed += (_, _) => closed++;

        Open(g);
        g.Input.CloseColumnChooser();
        Assert.False(g.Input.IsColumnChooserOpen);

        Open(g);
        g.Input.IsEnabled = false;
        Assert.False(g.Input.IsColumnChooserOpen);
        Assert.Equal(2, closed);
        g.Input.CloseColumnChooser(); // nothing open: nothing happens
    }

    [Fact]
    public void Opening_AnnouncesTheChooserAndItsSize()
    {
        var g = Grid();
        var said = new List<string>();
        g.Input.AnnouncementRequested += said.Add;

        Open(g);

        Assert.Equal("Columns, 5 items", said[0]);
    }

    // ── One popup at a time ──────────────────────────────────────

    [Fact]
    public void TheChooser_ClosesTheFilterPopup()
    {
        var g = Grid();
        int filterClosed = 0;
        g.Input.FilterPopupClosed += (_, _) => filterClosed++;
        TapFilterIcon(g, g.Age);
        Assert.IsType<DrawnFilterPopup>(g.Input.ActivePopup);

        Open(g);

        Assert.Equal(1, filterClosed);
        Assert.IsType<DrawnColumnChooser>(g.Input.ActivePopup);
    }

    [Fact]
    public void TheFilterIcon_WhileTheChooserIsOpen_ClosesTheChooser_AndTheNextTapOpensTheFilter()
    {
        var g = Grid();
        Open(g);

        TapFilterIcon(g, g.Age);
        Assert.False(g.Input.IsColumnChooserOpen);
        Assert.Null(g.Input.ActivePopup);

        TapFilterIcon(g, g.Age);
        Assert.IsType<DrawnFilterPopup>(g.Input.ActivePopup);
        Assert.False(g.Input.IsColumnChooserOpen);
    }

    [Fact]
    public void TheChooser_ClosesTheSearchPanelAndTheColumnMenu()
    {
        var g = Grid(x => x.Input.IsColumnMenuEnabled = true);
        g.Input.ShowSearchPanel(g.Scroll, g.Selection, g.Style, g.Source);
        Assert.True(g.Input.IsSearchPanelOpen);

        Open(g);
        Assert.False(g.Input.IsSearchPanelOpen);

        g.Input.ShowColumnMenu(g.Age, g.Scroll, g.Selection, g.Style, g.Source);
        Assert.False(g.Input.IsColumnChooserOpen);
        Open(g);
        Assert.Null(g.Input.ColumnMenu);
    }

    // ── The column menu ──────────────────────────────────────────

    [Fact]
    public void TheColumnMenusColumnsItem_OpensTheChooserBelowTheHeader()
    {
        var g = Grid(x => x.Input.IsColumnMenuEnabled = true);
        Assert.True(g.Input.ShowColumnMenu(g.Age, g.Scroll, g.Selection, g.Style, g.Source));
        var menu = Assert.IsType<DrawnMenu>(g.Input.ActivePopup);
        var item = menu.Items.Single(i => i.Key == KumikoColumnMenuItems.Columns);
        Assert.Equal("Columns...", item.Text);
        var row = menu.ItemBounds(menu.Items.IndexOf(item));

        g.Tap(row.X + 30, row.Y + row.Height / 2);
        g.RenderRecorded();

        var chooser = Assert.IsType<DrawnColumnChooser>(g.Input.ActivePopup);
        Assert.Null(g.Input.ColumnMenu);
        Assert.Equal(120f, chooser.Bounds.X);
        Assert.Equal(g.Style.HeaderHeight, chooser.Bounds.Y);
    }

    // ── The header context request ───────────────────────────────

    [Fact]
    public void HeaderContext_OpensTheChooser_OnlyWhenEnabled()
    {
        var g = Grid();

        RightClick(g, 140, InputGrid.HeaderY);
        Assert.False(g.Input.IsColumnChooserOpen);

        g.Input.ColumnChooserOnHeaderContext = true;
        RightClick(g, 140, InputGrid.HeaderY);
        g.RenderRecorded();
        var chooser = Assert.IsType<DrawnColumnChooser>(g.Input.ActivePopup);
        Assert.Equal(120f, chooser.Bounds.X); // at the Age header
    }

    [Fact]
    public void HeaderContext_DoesNotOpenTheChooser_OnACell_OrWhenAHandlerTookIt()
    {
        var g = Grid(x => x.Input.ColumnChooserOnHeaderContext = true);

        RightClick(g, 140, g.RowY(1));
        Assert.False(g.Input.IsColumnChooserOpen);

        g.Input.ContextRequested += (_, e) => e.Handled = true;
        RightClick(g, 140, InputGrid.HeaderY);
        Assert.False(g.Input.IsColumnChooserOpen);
    }

    [Fact]
    public void HeaderContext_TheColumnMenuWins_WhenBothAreOn()
    {
        var g = Grid(x =>
        {
            x.Input.ColumnChooserOnHeaderContext = true;
            x.Input.IsColumnMenuEnabled = true;
        });

        RightClick(g, 140, InputGrid.HeaderY);

        Assert.IsType<DrawnMenu>(g.Input.ActivePopup);
        Assert.False(g.Input.IsColumnChooserOpen);
    }

    [Fact]
    public void HeaderContext_ALongPressOnAHeader_OpensTheChooser()
    {
        var g = Grid(x => x.Input.ColumnChooserOnHeaderContext = true);

        g.Pointer(InputAction.Pressed, 140, InputGrid.HeaderY, deviceType: KumikoPointerDeviceType.Touch);
        g.Pointer(InputAction.LongPress, 140, InputGrid.HeaderY, deviceType: KumikoPointerDeviceType.Touch);
        g.Pointer(InputAction.Released, 140, InputGrid.HeaderY, deviceType: KumikoPointerDeviceType.Touch);

        Assert.True(g.Input.IsColumnChooserOpen);
    }

    // ── The shortcut ─────────────────────────────────────────────

    [Fact]
    public void Shortcut_IsOffByDefault()
    {
        var g = Grid();
        Assert.Null(g.Input.ColumnChooserShortcut);

        var e = g.Key(KumikoKey.C, InputModifiers.Control | InputModifiers.Alt);

        Assert.False(g.Input.IsColumnChooserOpen);
        Assert.False(e.Handled);
    }

    [Theory]
    [InlineData(InputModifiers.Control | InputModifiers.Alt)]
    [InlineData(InputModifiers.Meta | InputModifiers.Alt)] // Command+Option on an Apple keyboard
    public void Shortcut_OpensTheChooser(InputModifiers modifiers)
    {
        var g = Grid(x => x.Input.ColumnChooserShortcut = CtrlAltC);

        var e = g.Key(KumikoKey.C, modifiers);

        Assert.True(e.Handled);
        Assert.True(g.Input.IsColumnChooserOpen);
    }

    [Theory]
    [InlineData(InputModifiers.Control)]
    [InlineData(InputModifiers.Alt)]
    [InlineData(InputModifiers.Control | InputModifiers.Alt | InputModifiers.Shift)]
    public void Shortcut_NeedsExactlyItsModifiers(InputModifiers modifiers)
    {
        var g = Grid(x => x.Input.ColumnChooserShortcut = CtrlAltC);

        g.Key(KumikoKey.C, modifiers);

        Assert.False(g.Input.IsColumnChooserOpen);
    }

    [Fact]
    public void Shortcut_IsLeftToAnOpenEditor()
    {
        var g = Grid(x => x.Input.ColumnChooserShortcut = CtrlAltC);
        g.Select(0, 0);
        g.Key(KumikoKey.F2);
        Assert.True(g.Edit.IsEditing);

        g.Key(KumikoKey.C, InputModifiers.Control | InputModifiers.Alt);

        Assert.False(g.Input.IsColumnChooserOpen);
    }

    [Fact]
    public void KumikoShortcut_Matches_ComparesKeyAndModifiers()
    {
        var shortcut = new KumikoShortcut(KumikoKey.F, InputModifiers.Shift);

        Assert.True(shortcut.Matches(new KumikoKeyEventArgs { Key = KumikoKey.F, Modifiers = InputModifiers.Shift }));
        Assert.False(shortcut.Matches(new KumikoKeyEventArgs { Key = KumikoKey.F }));
        Assert.False(shortcut.Matches(new KumikoKeyEventArgs { Key = KumikoKey.C, Modifiers = InputModifiers.Shift }));
        Assert.False(new KumikoShortcut(KumikoKey.None, InputModifiers.None).Matches(new KumikoKeyEventArgs()));
        Assert.Equal(shortcut, new KumikoShortcut(KumikoKey.F, InputModifiers.Shift));
        Assert.Throws<ArgumentNullException>(() => shortcut.Matches(null!));
    }

    // ── Editing ──────────────────────────────────────────────────

    [Fact]
    public void AToggleInTheChooser_CommitsAnOpenEditFirst()
    {
        var g = Grid();
        g.Select(0, 2);
        g.Key(KumikoKey.F2);
        g.Type('X');
        Assert.True(g.Edit.IsEditing);
        var chooser = Open(g);
        var row = chooser.EntryBounds(2); // City, the edited column

        g.Tap(row.X + 120, row.Y + row.Height / 2);

        Assert.False(g.Edit.IsEditing);
        Assert.False(g.City.IsVisible);
        Assert.Equal("X", g.Items[0].City);
        Assert.Equal(new CellPosition(0, 3), g.Selection.CurrentCell); // moved off the hidden column
    }

    [Fact]
    public void HidingTheEditedColumnInCode_CommitsTheEdit_WhenTheHostReportsIt()
    {
        var g = Grid();
        g.Source.ColumnsChanged += (_, _) => g.Input.OnColumnsChanged(g.Selection, g.Source); // as the hosts do
        g.Select(0, 2);
        g.Key(KumikoKey.F2);
        g.Type('Y');

        g.City.IsVisible = false;

        Assert.False(g.Edit.IsEditing);
        Assert.Equal("Y", g.Items[0].City);
    }

    [Fact]
    public void HidingAnotherColumnInCode_LeavesTheEditOpen()
    {
        var g = Grid();
        g.Source.ColumnsChanged += (_, _) => g.Input.OnColumnsChanged(g.Selection, g.Source);
        g.Select(0, 2);
        g.Key(KumikoKey.F2);

        g.Dept.IsVisible = false;

        Assert.True(g.Edit.IsEditing);
    }

    // ── Keys while it is open ────────────────────────────────────

    [Fact]
    public void Keys_GoToTheChooser_NotToTheGrid()
    {
        var g = Grid();
        g.Select(2, 0);
        var chooser = Open(g);

        g.Key(KumikoKey.Down);
        g.Key(KumikoKey.Down);
        g.Key(KumikoKey.Space);

        Assert.Equal(new CellPosition(2, 0), g.Selection.CurrentCell);
        Assert.Same(g.Age, chooser.FocusedColumn);
        Assert.False(g.Age.IsVisible);
    }
}

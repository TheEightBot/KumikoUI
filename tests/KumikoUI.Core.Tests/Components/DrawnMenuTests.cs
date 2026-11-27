using System.Windows.Input;
using KumikoUI.Core.Components;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.Components;

/// <summary>
/// The drawn popup menu (#319): layout with collapsed separators, measured width, placement through the popup manager
/// (flip and clamp), hover and pressed feedback, keyboard navigation and typeahead, disabled and checked items,
/// choosing an item, a press outside, and scrolling a menu taller than its room.
/// </summary>
public class DrawnMenuTests
{
    // Geometry: 4 px padding at the top and the bottom, items 32 px tall, separators 9 px.
    private const float Pad = 4f;
    private const float Row = DrawnMenu.DefaultItemHeight;

    private static (DrawnMenu Menu, List<string> Invoked) Make(params DrawnMenuItem[] items)
    {
        var invoked = new List<string>();
        var menu = new DrawnMenu();
        foreach (var item in items)
        {
            if (!item.IsSeparator && item.Action == null)
            {
                string key = item.Key;
                item.Action = () => invoked.Add(key);
            }
            menu.Items.Add(item);
        }
        return (menu, invoked);
    }

    private static DrawnMenuItem Item(string text, bool enabled = true, bool? check = null, KumikoIcon? icon = null) =>
        new(text, text) { IsEnabled = enabled, IsChecked = check, Icon = icon };

    /// <summary>Alpha, Bravo (disabled), separator, Charlie (checked), Delta, Echo.</summary>
    private static (DrawnMenu Menu, List<string> Invoked) Standard() => Make(
        Item("Alpha"), Item("Bravo", enabled: false), DrawnMenuItem.Separator(),
        Item("Charlie", check: true), Item("Delta", icon: KumikoIcon.Filter), Item("Echo"));

    private static float MiddleOf(DrawnMenu menu, int index)
    {
        var r = menu.ItemBounds(index);
        return r.Y + r.Height / 2;
    }

    private static KumikoPointerEventArgs Ptr(InputAction action, float x, float y,
        KumikoPointerDeviceType device = KumikoPointerDeviceType.Mouse) =>
        new() { X = x, Y = y, Action = action, DeviceType = device };

    // ── Layout ───────────────────────────────────────────────────

    [Fact]
    public void Layout_StacksItemsAndSeparators_AndDropsLeadingTrailingAndDoubledSeparators()
    {
        var (menu, _) = Make(DrawnMenuItem.Separator(), Item("A"), Item("B"), DrawnMenuItem.Separator(),
            DrawnMenuItem.Separator(), Item("C"), DrawnMenuItem.Separator());

        Assert.Equal(new KumikoRect(0, Pad, menu.Bounds.Width, 0), menu.ItemBounds(0));
        Assert.Equal(new KumikoRect(0, Pad, menu.Bounds.Width, Row), menu.ItemBounds(1));
        Assert.Equal(Pad + Row, menu.ItemBounds(2).Y);
        Assert.Equal(9f, menu.ItemBounds(3).Height);
        Assert.Equal(0f, menu.ItemBounds(4).Height);
        Assert.Equal(Pad + 2 * Row + 9, menu.ItemBounds(5).Y);
        Assert.Equal(0f, menu.ItemBounds(6).Height);
        Assert.Equal(Pad + 3 * Row + 9 + Pad, menu.ContentHeight);
        Assert.Equal(menu.ContentHeight, menu.Bounds.Height);
    }

    [Fact]
    public void Layout_TouchItemHeight_Is44()
    {
        var (menu, _) = Make(Item("A"), Item("B"));
        menu.ItemHeight = DrawnMenu.TouchItemHeight;

        Assert.Equal(44f, menu.ItemBounds(1).Height);
        Assert.Equal(Pad + 88 + Pad, menu.Bounds.Height);
    }

    [Fact]
    public void Layout_FollowsChangesToTheItems()
    {
        var (menu, _) = Make(Item("A"));
        float before = menu.Bounds.Height;

        menu.Items.Add(Item("B"));
        Assert.Equal(before + Row, menu.Bounds.Height);

        menu.Items.RemoveAt(0);
        Assert.Equal(before, menu.Bounds.Height);
    }

    [Theory]
    [InlineData("Short", false, 160f)]                                       // the minimum width
    [InlineData("A text that is thirty chars..", false, 24 + 29 * 8 + 12f)]  // padding, text, trailing room
    [InlineData("A text that is thirty chars..", true, 24 + 24 + 29 * 8 + 12f)] // plus the icon slot
    [InlineData("A much longer text that is fifty characters long..", false, 360f)] // the maximum width
    public void Measure_SizesTheWidthToTheWidestText(string text, bool withIcon, float expected)
    {
        var (menu, _) = Make(Item("x"), Item(text, icon: withIcon ? KumikoIcon.Filter : null));

        menu.Measure(new RecordingDrawingContext()); // 8 px per character

        Assert.Equal(expected, menu.Bounds.Width);
    }

    // ── Placement (through the popup manager) ────────────────────

    private static DrawnMenu ShowAt(float x, float y, KumikoRect viewport, int items = 4)
    {
        var (menu, _) = Make(Enumerable.Range(0, items).Select(i => Item($"Item {i}")).ToArray());
        var popups = new PopupManager { Viewport = viewport };
        var anchor = new KumikoRect(x, y, 0, 0);
        popups.Show(menu, () =>
        {
            menu.MaxHeight = PopupLayout.RoomFor(anchor, viewport) - 4;
            return anchor;
        });
        popups.DrawPopups(new RecordingDrawingContext());
        return menu;
    }

    [Fact]
    public void Placement_OpensBelowAndRightOfThePoint_WhenThereIsRoom()
    {
        var menu = ShowAt(50, 60, new KumikoRect(0, 0, 400, 400));

        Assert.Equal(50, menu.Bounds.X);
        Assert.Equal(60, menu.Bounds.Y);
    }

    [Fact]
    public void Placement_FlipsAboveThePoint_NearTheBottom_AndClampsSideways_NearTheRight()
    {
        var menu = ShowAt(390, 380, new KumikoRect(0, 0, 400, 400));

        Assert.Equal(400 - menu.Bounds.Width, menu.Bounds.X);
        Assert.Equal(380, menu.Bounds.Bottom, 3);
    }

    [Fact]
    public void Placement_ATallMenu_ShrinksToTheRoomOnTheRoomierSide_AndScrolls()
    {
        var menu = ShowAt(50, 100, new KumikoRect(0, 0, 400, 250), items: 12);

        Assert.Equal(150 - 4, menu.Bounds.Height); // 150 px below the point, less the margin
        Assert.Equal(100, menu.Bounds.Y);
        Assert.True(menu.ContentHeight > menu.Bounds.Height);
    }

    // ── Hover and press ──────────────────────────────────────────

    [Fact]
    public void Hover_HighlightsEnabledItems_NotDisabledItemsOrSeparators_AndNothingOutside()
    {
        var (menu, _) = Standard();

        menu.OnPointerMove(Ptr(InputAction.Hover, 20, MiddleOf(menu, 0)));
        Assert.Equal(0, menu.HighlightedIndex);

        menu.OnPointerMove(Ptr(InputAction.Hover, 20, MiddleOf(menu, 1)));
        Assert.Equal(-1, menu.HighlightedIndex);

        menu.OnPointerMove(Ptr(InputAction.Hover, 20, MiddleOf(menu, 2)));
        Assert.Equal(-1, menu.HighlightedIndex);

        menu.OnPointerMove(Ptr(InputAction.Hover, 20, MiddleOf(menu, 3)));
        Assert.Equal(3, menu.HighlightedIndex);

        menu.OnPointerMove(Ptr(InputAction.Hover, 500, 500));
        Assert.Equal(-1, menu.HighlightedIndex);
    }

    [Fact]
    public void Draw_TheHighlightedAndThePressedItem_GetTheirBackgrounds()
    {
        var (menu, _) = Standard();
        menu.OnPointerMove(Ptr(InputAction.Hover, 20, MiddleOf(menu, 0)));

        var hovered = ComponentInput.Draw(menu);
        Assert.Contains(hovered.Fills, f => f.Color.Equals(menu.HighlightColor));

        menu.OnPointerDown(Ptr(InputAction.Pressed, 20, MiddleOf(menu, 0)));
        var pressed = ComponentInput.Draw(menu);
        Assert.Contains(pressed.Fills, f => f.Color.Equals(menu.PressedColor));
    }

    [Fact]
    public void Draw_ACheckedItemShowsACheckMark_AnIconItemItsIcon_AndADisabledItemIsDimmed()
    {
        var (menu, _) = Standard();
        var icons = new RecordingIconProvider();
        menu.IconProvider = icons;

        var ctx = ComponentInput.Draw(menu);

        Assert.Equal(new[] { KumikoIcon.Check, KumikoIcon.Filter }, icons.Icons);
        Assert.Equal(menu.CheckColor, icons.Calls[0].Color);
        Assert.Equal(menu.DisabledTextColor, ctx.SingleText("Bravo").Color);
        Assert.Equal(menu.TextColor, ctx.SingleText("Alpha").Color);
        Assert.Single(ctx.Lines); // the one separator
    }

    // ── Choosing ─────────────────────────────────────────────────

    [Fact]
    public void Click_OnAnEnabledItem_RequestsClose_RaisesItemInvoked_ThenRunsTheActionAndTheCommand()
    {
        var order = new List<string>();
        var command = new RecordingCommand(order);
        var item = new DrawnMenuItem("k", "Go", () => order.Add("action")) { Command = command, CommandParameter = 42 };
        var menu = new DrawnMenu();
        menu.Items.Add(item);
        menu.CloseRequested += _ => order.Add("close");
        menu.ItemInvoked += (_, i) => order.Add("invoked:" + i.Key);

        menu.Tap(20, MiddleOf(menu, 0));

        Assert.Equal(new[] { "close", "invoked:k", "action", "command:42" }, order);
    }

    [Fact]
    public void Click_OnADisabledItem_OrASeparator_DoesNothing()
    {
        var (menu, invoked) = Standard();
        int closes = 0;
        menu.CloseRequested += _ => closes++;

        menu.Tap(20, MiddleOf(menu, 1));
        menu.Tap(20, MiddleOf(menu, 2));

        Assert.Empty(invoked);
        Assert.Equal(0, closes);
    }

    [Fact]
    public void Mouse_ChoosesTheItemItIsReleasedOn_AFingerOnlyTheItemItPressed()
    {
        var (menu, invoked) = Standard();
        menu.OnPointerDown(Ptr(InputAction.Pressed, 20, MiddleOf(menu, 0)));
        menu.OnPointerMove(Ptr(InputAction.Moved, 20, MiddleOf(menu, 3)));
        menu.OnPointerUp(Ptr(InputAction.Released, 20, MiddleOf(menu, 3)));
        Assert.Equal(new[] { "Charlie" }, invoked);

        invoked.Clear();
        var touch = KumikoPointerDeviceType.Touch;
        menu.OnPointerDown(Ptr(InputAction.Pressed, 20, MiddleOf(menu, 0), touch));
        menu.OnPointerUp(Ptr(InputAction.Released, 20, MiddleOf(menu, 3), touch));
        Assert.Empty(invoked);
    }

    [Fact]
    public void ACommandThatCannotExecute_IsNotExecuted()
    {
        var order = new List<string>();
        var command = new RecordingCommand(order) { CanRun = false };
        var menu = new DrawnMenu();
        menu.Items.Add(new DrawnMenuItem("k", "Go") { Command = command });

        menu.Tap(20, MiddleOf(menu, 0));

        Assert.Empty(order);
    }

    // ── Keyboard ─────────────────────────────────────────────────

    [Fact]
    public void Keys_UpAndDown_SkipDisabledItemsAndSeparators_AndWrap()
    {
        var (menu, _) = Standard();

        menu.Key(KumikoKey.Down);
        Assert.Equal(0, menu.HighlightedIndex);
        menu.Key(KumikoKey.Down);
        Assert.Equal(3, menu.HighlightedIndex); // past Bravo (disabled) and the separator
        menu.Key(KumikoKey.Down);
        menu.Key(KumikoKey.Down);
        Assert.Equal(5, menu.HighlightedIndex);
        menu.Key(KumikoKey.Down);
        Assert.Equal(0, menu.HighlightedIndex); // wraps to the top
        menu.Key(KumikoKey.Up);
        Assert.Equal(5, menu.HighlightedIndex); // and back to the bottom
    }

    [Fact]
    public void Keys_UpWithNothingHighlighted_GoesToTheLastItem_HomeAndEndToTheEnds()
    {
        var (menu, _) = Standard();

        menu.Key(KumikoKey.Up);
        Assert.Equal(5, menu.HighlightedIndex);
        menu.Key(KumikoKey.Home);
        Assert.Equal(0, menu.HighlightedIndex);
        menu.Key(KumikoKey.End);
        Assert.Equal(5, menu.HighlightedIndex);
    }

    [Theory]
    [InlineData(KumikoKey.Enter)]
    [InlineData(KumikoKey.Space)]
    public void Keys_EnterOrSpace_ChooseTheHighlightedItem(KumikoKey key)
    {
        var (menu, invoked) = Standard();
        menu.Key(KumikoKey.Down);
        menu.Key(KumikoKey.Down);

        menu.Key(key);

        Assert.Equal(new[] { "Charlie" }, invoked);
    }

    [Fact]
    public void Keys_EnterWithNothingHighlighted_ChoosesNothing()
    {
        var (menu, invoked) = Standard();

        Assert.True(menu.Key(KumikoKey.Enter));

        Assert.Empty(invoked);
    }

    [Theory]
    [InlineData(KumikoKey.Escape)]
    [InlineData(KumikoKey.Tab)]
    [InlineData(KumikoKey.ContextMenu)]
    public void Keys_EscapeTabAndTheMenuKey_RequestClose_WithoutChoosing(KumikoKey key)
    {
        var (menu, invoked) = Standard();
        int closes = 0;
        menu.CloseRequested += _ => closes++;
        menu.Key(KumikoKey.Down);

        menu.Key(key);

        Assert.Equal(1, closes);
        Assert.Empty(invoked);
    }

    [Fact]
    public void Keys_EveryKeyIsHandled_SoNoneReachesTheGrid()
    {
        var (menu, _) = Standard();

        Assert.True(menu.Key(KumikoKey.Left));
        Assert.True(menu.Key(KumikoKey.A, modifiers: InputModifiers.Control));
        Assert.True(menu.Key(KumikoKey.F2));
    }

    [Fact]
    public void Typeahead_ALetterGoesToTheNextEnabledItemStartingWithIt_AndCycles()
    {
        var (menu, _) = Make(Item("Sort A to Z"), Item("Sort Z to A"), Item("Stop", enabled: false), Item("Filter..."));

        menu.Key(KumikoKey.Character, 's');
        Assert.Equal(0, menu.HighlightedIndex);
        menu.Key(KumikoKey.Character, 'S');
        Assert.Equal(1, menu.HighlightedIndex);
        menu.Key(KumikoKey.Character, 's');
        Assert.Equal(0, menu.HighlightedIndex); // the disabled "Stop" is skipped
        menu.Key(KumikoKey.Character, 'f');
        Assert.Equal(3, menu.HighlightedIndex);
        menu.Key(KumikoKey.Character, 'q');
        Assert.Equal(3, menu.HighlightedIndex); // no item starts with q
    }

    [Fact]
    public void Keyboard_MovingTheHighlight_ReportsTheItem_ForAnnouncements()
    {
        var (menu, _) = Standard();
        var reported = new List<string>();
        menu.KeyboardHighlightChanged += item => reported.Add(item.Text);

        menu.Key(KumikoKey.Down);
        menu.Key(KumikoKey.Down);
        menu.OnPointerMove(Ptr(InputAction.Hover, 20, MiddleOf(menu, 4))); // a hover is not announced

        Assert.Equal(new[] { "Alpha", "Charlie" }, reported);
    }

    [Fact]
    public void HighlightFirst_HighlightsTheFirstEnabledItem()
    {
        var (menu, _) = Make(Item("Off", enabled: false), DrawnMenuItem.Separator(), Item("On"));

        Assert.True(menu.HighlightFirst());

        Assert.Equal(2, menu.HighlightedIndex);
    }

    // ── Scrolling ────────────────────────────────────────────────

    private static DrawnMenu Tall(out List<string> invoked)
    {
        var made = Make(Enumerable.Range(0, 12).Select(i => Item($"Item {i:D2}")).ToArray());
        invoked = made.Invoked;
        made.Menu.MaxHeight = 100;
        return made.Menu;
    }

    [Fact]
    public void Scroll_TheWheelScrollsATallMenu_WithinItsContent()
    {
        var menu = Tall(out _);
        float max = menu.ContentHeight - menu.Bounds.Height;

        menu.OnScroll(new KumikoPointerEventArgs { Action = InputAction.Scroll, X = 20, Y = 50, ScrollDeltaY = -1 });
        Assert.Equal(3 * Row, menu.ScrollOffset);

        menu.OnScroll(new KumikoPointerEventArgs { Action = InputAction.Scroll, X = 20, Y = 50, ScrollDeltaY = -100 });
        Assert.Equal(max, menu.ScrollOffset);

        menu.OnScroll(new KumikoPointerEventArgs { Action = InputAction.Scroll, X = 20, Y = 50, ScrollDeltaY = 100 });
        Assert.Equal(0, menu.ScrollOffset);
    }

    [Fact]
    public void Scroll_AFingerDragScrollsATallMenu_AndChoosesNothing()
    {
        var menu = Tall(out var invoked);
        var touch = KumikoPointerDeviceType.Touch;

        menu.OnPointerDown(Ptr(InputAction.Pressed, 20, 80, touch));
        menu.OnPointerMove(Ptr(InputAction.Moved, 20, 60, touch));
        menu.OnPointerMove(Ptr(InputAction.Moved, 20, 30, touch));
        menu.OnPointerUp(Ptr(InputAction.Released, 20, 30, touch));

        Assert.Equal(50, menu.ScrollOffset);
        Assert.Empty(invoked);
    }

    [Fact]
    public void Scroll_TheKeyboardKeepsTheHighlightedItemInView()
    {
        var menu = Tall(out _);

        menu.Key(KumikoKey.End);

        var last = menu.ItemBounds(11);
        Assert.True(last.Bottom <= menu.Bounds.Bottom + 0.01f, $"{last} in {menu.Bounds}");
        Assert.True(menu.ScrollOffset > 0);

        menu.Key(KumikoKey.Home);
        Assert.Equal(0, menu.ScrollOffset);
    }

    [Fact]
    public void Scroll_ClicksHitTheItemUnderThePointer_AfterScrolling()
    {
        var menu = Tall(out var invoked);
        menu.OnScroll(new KumikoPointerEventArgs { Action = InputAction.Scroll, X = 20, Y = 50, ScrollDeltaY = -1 });

        menu.Tap(20, Pad + Row / 2); // the top row is now the fourth item

        Assert.Equal(new[] { "Item 03" }, invoked);
    }

    [Fact]
    public void Draw_ATallMenuDrawsAScrollbarThumb_AndOnlyTheItemsInView()
    {
        var menu = Tall(out _);

        var ctx = ComponentInput.Draw(menu);

        Assert.Contains(ctx.Fills, f => f.Color.Equals(menu.ScrollbarColor));
        Assert.DoesNotContain(ctx.Texts, t => t.Text == "Item 11");
    }

    // ── Theme ────────────────────────────────────────────────────

    [Fact]
    public void ApplyTheme_TakesColorsFontAndIconsFromTheStyle()
    {
        var style = KumikoTheme.CreateDark();
        var menu = new DrawnMenu();

        menu.ApplyTheme(style);

        Assert.Equal(style.BackgroundColor, menu.BackgroundColor);
        Assert.Equal(style.CellTextColor, menu.TextColor);
        Assert.Equal(style.AccentColor, menu.CheckColor);
        Assert.Equal(style.CellFont, menu.Font);
        Assert.Same(style.IconProvider, menu.IconProvider);
    }

    private sealed class RecordingCommand(List<string> order) : ICommand
    {
        public bool CanRun { get; init; } = true;

        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => CanRun;

        public void Execute(object? parameter) => order.Add("command:" + parameter);
    }
}

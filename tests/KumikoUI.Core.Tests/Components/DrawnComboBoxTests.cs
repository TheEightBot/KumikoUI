using KumikoUI.Core.Components;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;
using static KumikoUI.Core.Tests.Components.ComponentInput;

namespace KumikoUI.Core.Tests.Components;

/// <summary>
/// The drawn combo box: opening and closing the dropdown, pick-on-release selection, keyboard
/// navigation, type-to-filter, dropdown scrolling, item sources and what the open dropdown draws
/// (<see cref="DrawnComboBoxItem"/>, <see cref="ComboBoxSelectionChangedEventArgs"/>).
/// </summary>
public class DrawnComboBoxTests
{
    // Box (0,0)-(200,30); the dropdown starts at y = 30 and each item is 28 px tall.
    private const float ItemH = 28;

    private static float ItemY(int i) => 30 + i * ItemH + ItemH / 2;

    private sealed record Person(string Name, int Id);

    private static DrawnComboBox Combo(params string[] items)
    {
        var combo = new DrawnComboBox { Bounds = new KumikoRect(0, 0, 200, 30) };
        combo.AddItems(items.Length == 0 ? new[] { "A", "B", "C", "D" } : items);
        return combo;
    }

    // ── Open and close ────────────────────────────────────────────

    [Fact]
    public void PressOnTheClosedBox_OpensTheDropdown()
    {
        var combo = Combo();
        int opened = 0;
        combo.DropdownOpened += () => opened++;

        Assert.True(combo.Down(100, 15));

        Assert.True(combo.IsDropdownOpen);
        Assert.Equal(1, opened);
    }

    [Fact]
    public void PressOutsideTheOpenDropdown_Closes_AndConsumesThePress()
    {
        var combo = Combo();
        combo.OpenDropdown();
        int closed = 0;
        combo.DropdownClosed += () => closed++;

        Assert.True(combo.Down(500, 500));

        Assert.False(combo.IsDropdownOpen);
        Assert.Equal(1, closed);
        Assert.Equal(-1, combo.SelectedIndex);
    }

    [Fact]
    public void Disabled_IgnoresThePress()
    {
        var combo = Combo();
        combo.IsEnabled = false;

        Assert.False(combo.Down(100, 15));
        Assert.False(combo.IsDropdownOpen);
    }

    [Fact]
    public void LosingFocus_ClosesTheDropdown()
    {
        var combo = Combo();
        var focus = Focus(combo);
        combo.OpenDropdown();

        focus.SetFocus(null);

        Assert.False(combo.IsDropdownOpen);
    }

    [Fact]
    public void HitTest_CoversTheDropdownOnlyWhileItIsOpen()
    {
        var combo = Combo();
        Assert.False(combo.HitTest(100, 60));

        combo.OpenDropdown();
        Assert.True(combo.HitTest(100, 60));
        Assert.False(combo.HitTest(100, 30 + 4 * ItemH + 1));
        Assert.Equal(new KumikoRect(0, 30, 200, 4 * ItemH), combo.GetDropdownBounds());
    }

    // ── Pointer selection ─────────────────────────────────────────

    [Fact]
    public void PressAndReleaseOnAnItem_SelectsItClosesAndCompletesTheEdit()
    {
        var combo = Combo();
        var selections = new List<ComboBoxSelectionChangedEventArgs>();
        var values = new List<ComponentValueChangedEventArgs>();
        int completed = 0, closed = 0;
        combo.SelectionChanged += (_, e) => selections.Add(e);
        combo.ValueChanged += (_, e) => values.Add(e);
        combo.EditCompleted += () => completed++;
        combo.DropdownClosed += () => closed++;
        combo.Down(100, 15);

        combo.Down(100, ItemY(2));
        Assert.Equal(-1, combo.SelectedIndex);
        combo.Up(100, ItemY(2));

        Assert.Equal(2, combo.SelectedIndex);
        Assert.Equal("C", combo.SelectedText);
        Assert.Equal("C", combo.SelectedValue);
        Assert.False(combo.IsDropdownOpen);
        var s = Assert.Single(selections);
        Assert.Null(s.OldItem);
        Assert.Equal("C", s.NewItem!.DisplayText);
        Assert.Equal((null, "C"), (values[0].OldValue, values[0].NewValue));
        Assert.Equal(1, completed);
        Assert.Equal(1, closed);
    }

    [Fact]
    public void SelectingASecondItem_ReportsTheFirstAsOld()
    {
        var combo = Combo();
        combo.SetSelectedValue("A");
        ComboBoxSelectionChangedEventArgs? args = null;
        combo.SelectionChanged += (_, e) => args = e;
        combo.OpenDropdown();

        combo.Tap(100, ItemY(3));

        Assert.Equal("A", args!.OldItem!.DisplayText);
        Assert.Equal("D", args.NewItem!.DisplayText);
    }

    [Fact]
    public void ReleasingOverADifferentItem_SelectsNothingAndStaysOpen()
    {
        var combo = Combo();
        combo.OpenDropdown();
        int completed = 0;
        combo.EditCompleted += () => completed++;

        combo.Down(100, ItemY(1));
        bool handled = combo.Up(100, ItemY(2));

        Assert.True(handled);
        Assert.Equal(-1, combo.SelectedIndex);
        Assert.True(combo.IsDropdownOpen);
        Assert.Equal(0, completed);
    }

    [Fact]
    public void ReleaseWithoutAnItemPress_IsStillConsumed()
    {
        var combo = Combo();

        Assert.True(combo.Up(100, 15));
    }

    [Fact]
    public void MovingOverTheDropdown_HighlightsTheItemUnderThePointer()
    {
        var combo = Combo();
        combo.OpenDropdown();

        Assert.True(combo.Move(100, ItemY(1)));
        var ctx = Draw(combo);

        var highlight = ctx.Fills.Single(o => o.Color.Equals(combo.HighlightColor));
        Assert.Equal(new KumikoRect(0, 30 + ItemH, 200, ItemH), highlight.Rect);

        Assert.False(combo.Move(500, 500));
    }

    [Fact]
    public void Move_WhileClosed_IsNotHandled()
    {
        Assert.False(Combo().Move(100, 60));
    }

    // ── Keyboard ──────────────────────────────────────────────────

    [Theory]
    [InlineData(KumikoKey.Down)]
    [InlineData(KumikoKey.Up)]
    [InlineData(KumikoKey.Space)]
    public void NavigationKeys_OpenAClosedDropdown(KumikoKey key)
    {
        var combo = Combo();

        Assert.True(combo.Key(key));

        Assert.True(combo.IsDropdownOpen);
    }

    [Fact]
    public void DownDownEnter_PicksTheSecondItem()
    {
        var combo = Combo();
        combo.Key(KumikoKey.Down); // open
        combo.Key(KumikoKey.Down); // highlight 0
        combo.Key(KumikoKey.Down); // highlight 1

        combo.Key(KumikoKey.Enter);

        Assert.Equal(1, combo.SelectedIndex);
        Assert.False(combo.IsDropdownOpen);
    }

    [Fact]
    public void Highlight_ClampsAtBothEnds()
    {
        var combo = Combo();
        combo.OpenDropdown();

        for (int i = 0; i < 10; i++) combo.Key(KumikoKey.Down);
        combo.Key(KumikoKey.Enter);
        Assert.Equal(3, combo.SelectedIndex);

        combo.OpenDropdown();
        for (int i = 0; i < 10; i++) combo.Key(KumikoKey.Up);
        combo.Key(KumikoKey.Enter);
        Assert.Equal(0, combo.SelectedIndex);
    }

    [Fact]
    public void Escape_ClosesWithoutSelecting()
    {
        var combo = Combo();
        combo.OpenDropdown();
        combo.Key(KumikoKey.Down);

        Assert.True(combo.Key(KumikoKey.Escape));

        Assert.False(combo.IsDropdownOpen);
        Assert.Equal(-1, combo.SelectedIndex);
    }

    [Fact]
    public void Enter_WithNoHighlight_ChangesNothing()
    {
        var combo = Combo();
        combo.OpenDropdown();

        Assert.True(combo.Key(KumikoKey.Enter));

        Assert.Equal(-1, combo.SelectedIndex);
        Assert.True(combo.IsDropdownOpen);
    }

    [Fact]
    public void OtherKeys_AreNotHandled()
    {
        var combo = Combo();
        combo.OpenDropdown();

        Assert.False(combo.Key(KumikoKey.Tab));
        Assert.False(combo.Key(KumikoKey.Character, 'x'));
    }

    [Fact(Skip = "Known defect: choosing an item with Enter does not raise EditCompleted, while choosing it with the pointer does")]
    public void Enter_OnAHighlightedItem_CompletesTheEdit()
    {
        var combo = Combo();
        int completed = 0;
        combo.EditCompleted += () => completed++;
        combo.OpenDropdown();
        combo.Key(KumikoKey.Down);

        combo.Key(KumikoKey.Enter);

        Assert.Equal(1, completed);
    }

    // ── Filtering ─────────────────────────────────────────────────

    [Fact]
    public void Filterable_TypingNarrowsTheListAndEnterPicksTheFirstMatch()
    {
        var combo = Combo("Apple", "Banana", "Blueberry", "Cherry");
        combo.IsFilterable = true;
        combo.OpenDropdown();

        combo.Key(KumikoKey.Character, 'b');
        Assert.Equal(new KumikoRect(0, 30, 200, 2 * ItemH), combo.GetDropdownBounds());
        combo.Key(KumikoKey.Character, 'l');
        Assert.Equal(new KumikoRect(0, 30, 200, ItemH), combo.GetDropdownBounds());

        combo.Key(KumikoKey.Enter);

        Assert.Equal("Blueberry", combo.SelectedText);
        Assert.Equal(2, combo.SelectedIndex);
    }

    [Fact]
    public void Filterable_BackspaceWidensTheListAgain()
    {
        var combo = Combo("Apple", "Banana", "Blueberry", "Cherry");
        combo.IsFilterable = true;
        combo.OpenDropdown();
        combo.Key(KumikoKey.Character, 'b');
        combo.Key(KumikoKey.Character, 'l');

        Assert.True(combo.Key(KumikoKey.Backspace));

        Assert.Equal(2 * ItemH, combo.GetDropdownBounds().Height);

        Assert.True(combo.Key(KumikoKey.Backspace));
        Assert.Equal(4 * ItemH, combo.GetDropdownBounds().Height);
        Assert.False(combo.Key(KumikoKey.Backspace));
    }

    [Fact]
    public void Filterable_PointerPicksTheFilteredItemNotTheSameRowOfTheFullList()
    {
        var combo = Combo("Apple", "Banana", "Blueberry", "Cherry");
        combo.IsFilterable = true;
        combo.OpenDropdown();
        combo.Key(KumikoKey.Character, 'c');

        combo.Tap(100, ItemY(0));

        Assert.Equal("Cherry", combo.SelectedText);
        Assert.Equal(3, combo.SelectedIndex);
    }

    [Fact]
    public void Filterable_ShowsTheFilterTextInTheBoxWhileOpenAndFocused()
    {
        var combo = Combo("Apple", "Banana");
        combo.IsFilterable = true;
        Focus(combo);
        combo.SetSelectedValue("Apple");
        combo.OpenDropdown();
        combo.Key(KumikoKey.Character, 'b');

        var ctx = Draw(combo);

        Assert.Single(ctx.TextsEqual("b"));
    }

    [Fact]
    public void NotFilterable_IgnoresTypedCharacters()
    {
        var combo = Combo();
        combo.OpenDropdown();

        Assert.False(combo.Key(KumikoKey.Character, 'a'));
        Assert.Equal(4 * ItemH, combo.GetDropdownBounds().Height);
    }

    // ── Scrolling ─────────────────────────────────────────────────

    [Fact]
    public void LongList_ScrollsToKeepTheHighlightVisible_AndPointerPicksTheScrolledItem()
    {
        var combo = Combo(Enumerable.Range(0, 10).Select(i => $"Item {i}").ToArray());
        combo.OpenDropdown();
        Assert.Equal(200, combo.GetDropdownBounds().Height);

        for (int i = 0; i < 10; i++) combo.Key(KumikoKey.Down);

        // Item 9 spans [252, 280) of the list; the 200 px viewport ends at 280
        Assert.Equal(80, combo.DropdownScrollOffset);

        combo.Tap(100, 30 + 252 - 80 + 14);
        Assert.Equal("Item 9", combo.SelectedText);
    }

    [Fact]
    public void Opening_ResetsTheScrollOffset()
    {
        var combo = Combo(Enumerable.Range(0, 10).Select(i => $"Item {i}").ToArray());
        combo.OpenDropdown();
        for (int i = 0; i < 10; i++) combo.Key(KumikoKey.Down);
        combo.CloseDropdown();

        combo.OpenDropdown();

        Assert.Equal(0, combo.DropdownScrollOffset);
    }

    // ── Items and selection ───────────────────────────────────────

    [Fact]
    public void SetItemsFromSource_UsesSelectorsThenMemberPathsThenToString()
    {
        var people = new[] { new Person("Ann", 1), new Person("Bo", 2) };
        var combo = new DrawnComboBox();

        combo.DisplaySelector = o => ((Person)o).Name.ToUpperInvariant();
        combo.ValueSelector = o => ((Person)o).Id;
        combo.SetItemsFromSource(people);
        Assert.Equal(new[] { "ANN", "BO" }, combo.Items.Select(i => i.DisplayText));
        Assert.Equal(new object?[] { 1, 2 }, combo.Items.Select(i => i.Value));

        combo = new DrawnComboBox { DisplayMemberPath = "Name", ValueMemberPath = "Id" };
        combo.SetItemsFromSource(people);
        Assert.Equal(new[] { "Ann", "Bo" }, combo.Items.Select(i => i.DisplayText));
        Assert.Equal(new object?[] { 1, 2 }, combo.Items.Select(i => i.Value));

        combo = new DrawnComboBox();
        combo.SetItemsFromSource(new object?[] { 5, null, "x" }!);
        Assert.Equal(new[] { "5", "x" }, combo.Items.Select(i => i.DisplayText));
        Assert.Equal(new object?[] { 5, "x" }, combo.Items.Select(i => i.Value));
    }

    [Fact]
    public void ItemValue_DefaultsToTheDisplayText()
    {
        var combo = new DrawnComboBox();
        combo.AddItem("Red");
        combo.AddItem("Green", 7);

        Assert.Equal("Red", combo.Items[0].Value);
        Assert.Equal(7, combo.Items[1].Value);
        Assert.Equal("Green", new DrawnComboBoxItem("Green", 7).DisplayText);
    }

    [Fact]
    public void SetSelectedValue_SelectsTheMatchingItem_AndRaisesValueChanged()
    {
        var combo = Combo();
        var values = new List<ComponentValueChangedEventArgs>();
        combo.ValueChanged += (_, e) => values.Add(e);

        combo.SetSelectedValue("C");
        combo.SetSelectedValue("missing");

        Assert.Equal(-1, combo.SelectedIndex);
        Assert.Null(combo.SelectedItem);
        Assert.Equal(string.Empty, combo.SelectedText);
        Assert.Equal(2, values.Count);
        Assert.Equal((null, "C"), (values[0].OldValue, values[0].NewValue));
        Assert.Equal(("C", null), (values[1].OldValue, values[1].NewValue));
    }

    [Fact]
    public void SelectedIndex_IsClampedToTheItems()
    {
        var combo = Combo();

        combo.SelectedIndex = 99;
        Assert.Equal(3, combo.SelectedIndex);

        combo.SelectedIndex = -5;
        Assert.Equal(-1, combo.SelectedIndex);
    }

    [Fact]
    public void ClearItems_AndSetItems_ResetTheSelection()
    {
        var combo = Combo();
        combo.SelectedIndex = 1;

        combo.SetItems(new[] { new DrawnComboBoxItem("X"), new DrawnComboBoxItem("Y") });
        Assert.Equal(-1, combo.SelectedIndex);
        Assert.Equal(2, combo.Items.Count);

        combo.SelectedIndex = 0;
        combo.ClearItems();
        Assert.Equal(-1, combo.SelectedIndex);
        Assert.Empty(combo.Items);
    }

    [Fact]
    public void ApplyTheme_TakesTheColorsFromTheGridStyle()
    {
        var style = new KumikoStyle();
        var combo = new DrawnComboBox();

        combo.ApplyTheme(style);

        Assert.Equal(style.BackgroundColor, combo.BackgroundColor);
        Assert.Equal(style.CellTextColor, combo.TextColor);
        Assert.Equal(style.AccentColor, combo.FocusedBorderColor);
        Assert.Equal(style.GridLineColor, combo.BorderColor);
    }

    // ── Drawing ───────────────────────────────────────────────────

    [Fact]
    public void Draw_Closed_ShowsTheSelectionOrThePlaceholder()
    {
        var combo = Combo();
        combo.Placeholder = "Pick one";
        Assert.Single(Draw(combo).TextsEqual("Pick one"));

        combo.SetSelectedValue("B");
        var ctx = Draw(combo);
        Assert.Single(ctx.TextsEqual("B"));
        Assert.Empty(ctx.TextsEqual("Pick one"));
        Assert.Empty(ctx.TextsEqual("A"));
    }

    [Fact]
    public void Draw_Open_ListsTheItemsBelowTheBoxAndFlipsTheArrow()
    {
        var combo = Combo();
        var closed = Draw(combo);
        combo.OpenDropdown();
        var open = Draw(combo);

        foreach (var text in new[] { "A", "B", "C", "D" })
            Assert.Equal(ItemY(text[0] - 'A'), open.SingleText(text).Rect.Bottom, 2);

        // One filled triangle each; its apex is the vertex alone on its side of the base.
        var closedArrow = Assert.Single(closed.Polygons).Points!;
        var openArrow = Assert.Single(open.Polygons).Points!;
        Assert.Empty(closed.Lines);
        Assert.Empty(open.Lines);
        Assert.Equal(closedArrow[0].Y, closedArrow[1].Y);   // pointing down: base on top, apex below
        Assert.True(closedArrow[2].Y > closedArrow[0].Y);
        Assert.Equal(openArrow[0].Y, openArrow[2].Y);       // pointing up: base at the bottom, apex above
        Assert.True(openArrow[1].Y < openArrow[0].Y);
    }

    [Fact]
    public void Draw_Open_ClipsTheItemsToTheDropdownBounds()
    {
        var combo = Combo(Enumerable.Range(0, 10).Select(i => $"Item {i}").ToArray());
        combo.OpenDropdown();

        var ctx = Draw(combo);

        var drop = combo.GetDropdownBounds();
        var items = ctx.Texts.Where(t => t.Text!.StartsWith("Item")).ToList();
        Assert.NotEmpty(items);
        Assert.All(items, t => Assert.Equal((KumikoRect?)drop, t.Clip));
        Assert.Equal(0, ctx.SaveDepth);
    }
}

using KumikoUI.Core.Components;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using static KumikoUI.Core.Tests.Components.ComponentInput;

namespace KumikoUI.Core.Tests.Components;

/// <summary>
/// The drawn column chooser (#335, spec #106), driven with synthetic pointer and key events: toggling by tap and Space,
/// the guards, search, drag and Alt+arrow reorder within a pane and the refusal across panes, Show all, Reset, the
/// Escape order, keyboard-only use, announcements and theming.
/// </summary>
public class DrawnColumnChooserTests
{
    private sealed class Row
    {
        public string Name { get; set; } = "";
    }

    private sealed class Fixture
    {
        public KumikoSource Source { get; } = new();
        public KumikoColumn Name { get; } = new() { Header = "Name", PropertyName = "Name" };
        public KumikoColumn Age { get; } = new() { Header = "Age", PropertyName = "Age" };
        public KumikoColumn City { get; } = new() { Header = "City", PropertyName = "City" };
        public KumikoColumn Salary { get; } = new() { Header = "Salary", PropertyName = "Salary" };
        public DrawnColumnChooser Chooser { get; }
        public List<string> Said { get; } = new();
        public int Closes { get; private set; }

        public Fixture(Action<Fixture>? configure = null)
        {
            Source.SetColumns(new[] { Name, Age, City, Salary });
            Source.SetItems(new[] { new Row { Name = "a" } });
            configure?.Invoke(this);
            Chooser = new DrawnColumnChooser(Source);
            Chooser.AnnouncementRequested += Said.Add;
            Chooser.CloseRequested += (_, _) => Closes++;
        }

        public IReadOnlyList<KumikoColumn> Entries => Chooser.VisibleEntries;

        public KumikoRect Entry(int index) => Chooser.EntryBounds(index);

        /// <summary>A tap in the middle of an entry's label.</summary>
        public void TapEntry(int index)
        {
            var row = Entry(index);
            Chooser.Tap(row.X + 120, row.Y + row.Height / 2);
        }

        /// <summary>Drags entry <paramref name="from"/> by its grip to the point <paramref name="toY"/>.</summary>
        public void Drag(int from, float toY)
        {
            var row = Entry(from);
            float x = Chooser.GripX;
            float y = row.Y + row.Height / 2;
            Chooser.Down(x, y);
            Chooser.Move(x, y + (toY > y ? 4 : -4));
            Chooser.Move(x, toY);
            Chooser.Up(x, toY);
        }

        public bool Key(KumikoKey key, InputModifiers modifiers = InputModifiers.None, char? character = null) =>
            Chooser.Key(key, character, modifiers);
    }

    // ── The list ─────────────────────────────────────────────────

    [Fact]
    public void Lists_TheColumnsByPane_WithHiddenColumnsInPlace_AndLeavesOutUnlistedAndSelectionColumns()
    {
        var selection = new KumikoSelectionColumn();
        var f = new Fixture(x =>
        {
            x.Salary.FreezeMode = ColumnFreezeMode.Left;
            x.Age.IsVisible = false;
            x.City.ShowInChooser = false;
        });
        f.Source.SetColumns(new[] { selection, f.Name, f.Age, f.City, f.Salary });

        Assert.Equal(new[] { f.Salary, f.Name, f.Age }, f.Entries);
        Assert.Equal(3, f.Chooser.EntryCount);
    }

    [Fact]
    public void FollowsTheSource_WhenColumnsChangeWhileItIsOpen()
    {
        var f = new Fixture();

        f.Source.ReorderColumn(3, 0);
        f.City.ShowInChooser = false;

        Assert.Equal(new[] { f.Salary, f.Name, f.Age }, f.Entries);
    }

    [Fact]
    public void Size_IsTheDefaultWidth_AndShrinksToFewColumns_ButNotBelowOneRow()
    {
        var f = new Fixture();
        Assert.Equal(DrawnColumnChooser.DefaultWidth, f.Chooser.Bounds.Width);
        float four = f.Chooser.Bounds.Height;

        f.Chooser.MaxHeight = 200; // the list scrolls
        Assert.True(f.Chooser.Bounds.Height <= 200.01f);
        Assert.True(f.Chooser.Bounds.Height < four);

        f.Chooser.MaxHeight = 2000;
        f.Source.SetColumns(Array.Empty<KumikoColumn>());
        f.Chooser.OnDraw(new TestHelpers.NoOpDrawingContext());
        Assert.True(f.Chooser.Bounds.Height < four);
        Assert.True(f.Chooser.Bounds.Height > f.Chooser.ItemHeight);
    }

    [Fact]
    public void ItemHeight_FollowsTheGridsRowHeight_WithAMinimum()
    {
        var f = new Fixture();
        f.Chooser.ApplyTheme(new KumikoStyle { RowHeight = 44 });
        Assert.Equal(44f, f.Chooser.ItemHeight);

        f.Chooser.ApplyTheme(new KumikoStyle { RowHeight = 12 });
        Assert.Equal(DrawnColumnChooser.MinItemHeight, f.Chooser.ItemHeight);
    }

    // ── Toggling ─────────────────────────────────────────────────

    [Fact]
    public void TapOnARow_HidesItsColumn_AndAnotherTapShowsIt()
    {
        var f = new Fixture();

        f.TapEntry(1);
        Assert.False(f.Age.IsVisible);
        Assert.Equal("Age hidden", f.Said[^1]);

        f.TapEntry(1);
        Assert.True(f.Age.IsVisible);
        Assert.Equal("Age shown", f.Said[^1]);
    }

    [Fact]
    public void TapOnTheGrip_WithoutMoving_TogglesToo()
    {
        var f = new Fixture();
        var row = f.Entry(2);

        f.Chooser.Tap(f.Chooser.GripX, row.Y + row.Height / 2);

        Assert.False(f.City.IsVisible);
    }

    [Fact]
    public void APressThatMoves_ScrollsInsteadOfToggling()
    {
        var f = new Fixture(x =>
        {
            for (int i = 0; i < 20; i++) x.Source.SetColumns(x.Source.Columns.Append(new KumikoColumn { Header = $"Extra {i}" }));
        });
        var row = f.Entry(3);
        float y = row.Y + row.Height / 2;

        f.Chooser.Down(150, y);
        f.Chooser.Move(150, y - 40);
        f.Chooser.Up(150, y - 40);

        Assert.True(f.Chooser.ListScrollOffset > 0);
        Assert.All(f.Source.Columns, c => Assert.True(c.IsVisible));
    }

    [Fact]
    public void TheLastVisibleColumn_CannotBeHidden_AndSaysWhy()
    {
        var f = new Fixture(x => x.Age.IsVisible = x.City.IsVisible = x.Salary.IsVisible = false);

        f.TapEntry(0);

        Assert.True(f.Name.IsVisible);
        Assert.Equal("This column cannot be hidden", f.Said[^1]);
    }

    [Fact]
    public void AColumnThatDoesNotAllowHiding_CannotBeHidden_ButCanBeShown()
    {
        var f = new Fixture(x =>
        {
            x.Age.AllowHide = false;
            x.City.AllowHide = false;
            x.City.IsVisible = false;
        });

        f.TapEntry(1);
        f.TapEntry(2);

        Assert.True(f.Age.IsVisible);
        Assert.True(f.City.IsVisible);
    }

    [Fact]
    public void BeforeChange_ThatRefuses_KeepsEverything()
    {
        var f = new Fixture();
        int asked = 0;
        f.Chooser.BeforeChange = () => { asked++; return false; };

        f.TapEntry(0);
        f.Drag(0, f.Entry(3).Y);

        Assert.Equal(2, asked);
        Assert.True(f.Name.IsVisible);
        Assert.Same(f.Name, f.Source.Columns[0]);
    }

    [Fact]
    public void Space_TogglesTheFocusedEntry_FromTheKeyboard()
    {
        var f = new Fixture();
        Focus(f.Chooser);

        f.Key(KumikoKey.Down); // from the search box into the list
        f.Key(KumikoKey.Down);
        f.Key(KumikoKey.Space);
        Assert.False(f.Age.IsVisible);

        f.Key(KumikoKey.Character, character: ' ');
        Assert.True(f.Age.IsVisible);

        f.Key(KumikoKey.Enter);
        Assert.False(f.Age.IsVisible);
        Assert.Same(f.Age, f.Chooser.FocusedColumn);
    }

    // ── Search ───────────────────────────────────────────────────

    [Fact]
    public void Search_NarrowsTheList_IgnoringCase()
    {
        var f = new Fixture();

        f.Chooser.SearchText = "a";
        Assert.Equal(new[] { f.Name, f.Age, f.Salary }, f.Entries);

        f.Chooser.SearchText = "SAL";
        Assert.Equal(new[] { f.Salary }, f.Entries);

        f.Chooser.SearchText = "nothing";
        Assert.Empty(f.Entries);
    }

    [Fact]
    public void Search_TheRowsStillToggle_TheMatchingColumn()
    {
        var f = new Fixture();
        f.Chooser.SearchText = "city";

        f.TapEntry(0);

        Assert.False(f.City.IsVisible);
    }

    [Fact]
    public void Search_TypedCharacters_GoToTheSearchBox_FromTheList()
    {
        var f = new Fixture();
        Focus(f.Chooser);
        f.Key(KumikoKey.Down);
        Assert.Equal(DrawnColumnChooser.Stop.List, f.Chooser.CurrentStop);

        f.Key(KumikoKey.Character, character: 'c');

        Assert.Equal(DrawnColumnChooser.Stop.Search, f.Chooser.CurrentStop);
        Assert.Equal("c", f.Chooser.SearchText);
        Assert.Equal(new[] { f.City }, f.Entries);
    }

    [Fact]
    public void Search_DisablesReordering_ByDragAndByKeyboard()
    {
        var f = new Fixture();
        f.Chooser.SearchText = "a"; // Name, Age, Salary

        f.Drag(0, f.Entry(2).Bottom - 2);
        Assert.Equal(new[] { f.Name, f.Age, f.City, f.Salary }, f.Source.Columns);

        Focus(f.Chooser);
        f.Key(KumikoKey.Down);
        f.Key(KumikoKey.Down, InputModifiers.Alt);
        Assert.Equal(new[] { f.Name, f.Age, f.City, f.Salary }, f.Source.Columns);
    }

    // ── Reordering ───────────────────────────────────────────────

    [Fact]
    public void DragByTheGrip_MovesTheColumn_AndAnnouncesIt()
    {
        var f = new Fixture();

        f.Drag(0, f.Entry(3).Y); // the gap between City and Salary

        Assert.Equal(new[] { f.Age, f.City, f.Name, f.Salary }, f.Source.Columns);
        Assert.Equal("Name moved to position 3 of 4", f.Said[^1]);
    }

    [Fact]
    public void DragByTheGrip_Upward_InsertsAboveTheRowUnderThePointer()
    {
        var f = new Fixture();

        f.Drag(3, f.Entry(1).Y + 4); // the upper half of Age

        Assert.Equal(new[] { f.Name, f.Salary, f.Age, f.City }, f.Source.Columns);
    }

    [Fact]
    public void DragByTheGrip_ShowsTheDropSlot_WhileItMoves()
    {
        var f = new Fixture();
        var row = f.Entry(0);
        float y = row.Y + row.Height / 2;

        f.Chooser.Down(f.Chooser.GripX, y);
        f.Chooser.Move(f.Chooser.GripX, y + 4);
        Assert.True(f.Chooser.IsDragging);
        Assert.Equal(-1, f.Chooser.DropSlot); // still over itself
        f.Chooser.Move(f.Chooser.GripX, f.Entry(2).Y + 30);
        Assert.Equal(3, f.Chooser.DropSlot);

        f.Chooser.OnPointerCancel(Ptr(InputAction.Cancelled, 0, 0));
        Assert.False(f.Chooser.IsDragging);
        Assert.Same(f.Name, f.Source.Columns[0]); // a canceled drag moves nothing
    }

    [Fact]
    public void Drag_HiddenColumnsMoveToo_AndKeepTheirState()
    {
        var f = new Fixture(x => x.Name.IsVisible = false);

        f.Drag(0, f.Entry(2).Y);

        Assert.Equal(new[] { f.Age, f.Name, f.City, f.Salary }, f.Source.Columns);
        Assert.False(f.Name.IsVisible);
    }

    [Fact]
    public void Drag_AcrossPanes_IsRefused()
    {
        var f = new Fixture(x => x.Salary.FreezeMode = ColumnFreezeMode.Left); // list: Salary | Name Age City

        f.Drag(0, f.Entry(2).Y); // Salary into the scrolling pane
        Assert.Equal(new[] { f.Name, f.Age, f.City, f.Salary }, f.Source.Columns);
        Assert.Equal(ColumnFreezeMode.Left, f.Salary.FreezeMode);

        f.Drag(2, f.Entry(0).Y + 2); // Age into the left pane
        Assert.Equal(new[] { f.Name, f.Age, f.City, f.Salary }, f.Source.Columns);
    }

    [Fact]
    public void Drag_WithinTheScrollingPane_AroundAFrozenColumn()
    {
        var f = new Fixture(x => x.Name.FreezeMode = ColumnFreezeMode.Left); // list: Name | Age City Salary

        f.Drag(3, f.Entry(1).Y + 2); // Salary to the top of the scrolling pane

        Assert.Equal(new[] { f.Name, f.Salary, f.Age, f.City }, f.Entries);
        Assert.Equal(ColumnFreezeMode.None, f.Salary.FreezeMode);
        Assert.Equal(new[] { f.Name, f.Salary, f.Age, f.City }, f.Source.DisplayColumns);
    }

    [Fact]
    public void AColumnThatCannotMove_HasNoDrag_ButOthersDropAroundIt()
    {
        var f = new Fixture(x => x.Age.AllowReorder = false);

        f.Drag(1, f.Entry(3).Bottom - 2);
        Assert.Equal(new[] { f.Name, f.Age, f.City, f.Salary }, f.Source.Columns);

        f.Drag(0, f.Entry(2).Y); // Name past Age
        Assert.Equal(new[] { f.Age, f.Name, f.City, f.Salary }, f.Source.Columns);

        Focus(f.Chooser);
        f.Key(KumikoKey.Down);
        f.Key(KumikoKey.Down, InputModifiers.Alt); // Age cannot move
        Assert.Equal(new[] { f.Age, f.Name, f.City, f.Salary }, f.Source.Columns);
    }

    [Fact]
    public void AltArrows_MoveTheFocusedColumn_OneSlot_AndTheFocusFollows()
    {
        var f = new Fixture();
        Focus(f.Chooser);
        f.Key(KumikoKey.Down); // Name

        f.Key(KumikoKey.Down, InputModifiers.Alt);
        Assert.Equal(new[] { f.Age, f.Name, f.City, f.Salary }, f.Source.Columns);
        Assert.Same(f.Name, f.Chooser.FocusedColumn);
        Assert.Equal("Name moved to position 2 of 4", f.Said[^1]);

        f.Key(KumikoKey.Down, InputModifiers.Alt);
        f.Key(KumikoKey.Up, InputModifiers.Alt);
        Assert.Equal(new[] { f.Age, f.Name, f.City, f.Salary }, f.Source.Columns);
    }

    [Fact]
    public void AltArrows_StopAtThePaneEdges()
    {
        var f = new Fixture(x => x.Salary.FreezeMode = ColumnFreezeMode.Left); // list: Salary | Name Age City
        Focus(f.Chooser);
        f.Key(KumikoKey.Down); // Salary

        f.Key(KumikoKey.Up, InputModifiers.Alt);
        f.Key(KumikoKey.Down, InputModifiers.Alt);
        Assert.Equal(new[] { f.Name, f.Age, f.City, f.Salary }, f.Source.Columns);

        f.Key(KumikoKey.Down); // Name, the first of the scrolling pane
        f.Key(KumikoKey.Up, InputModifiers.Alt);
        Assert.Equal(new[] { f.Name, f.Age, f.City, f.Salary }, f.Source.Columns);
        f.Key(KumikoKey.End); // City, the last
        f.Key(KumikoKey.Down, InputModifiers.Alt);
        Assert.Equal(new[] { f.Name, f.Age, f.City, f.Salary }, f.Source.Columns);
    }

    // ── Show all and Reset ───────────────────────────────────────

    [Fact]
    public void ShowAll_ShowsEveryListedColumn_InOneChange()
    {
        var hiddenUnlisted = new KumikoColumn { Header = "Secret", PropertyName = "Secret", IsVisible = false, ShowInChooser = false };
        var f = new Fixture(x =>
        {
            x.Age.IsVisible = false;
            x.City.IsVisible = false;
            x.Source.SetColumns(x.Source.Columns.Append(hiddenUnlisted));
        });
        var events = new List<KumikoColumnsChangedEventArgs>();
        f.Source.ColumnsChanged += (_, e) => events.Add(e);
        var button = f.Chooser.ShowAllBounds;

        f.Chooser.Tap(button.X + 10, button.Y + button.Height / 2);

        Assert.True(f.Age.IsVisible);
        Assert.True(f.City.IsVisible);
        Assert.False(hiddenUnlisted.IsVisible);
        Assert.Single(events);
        Assert.Equal("All columns shown", f.Said[^1]);
    }

    [Fact]
    public void Reset_RestoresTheDefaultLayout()
    {
        var f = new Fixture();
        f.TapEntry(1);
        f.Drag(0, f.Entry(3).Y);
        var button = f.Chooser.ResetBounds;

        f.Chooser.Tap(button.X + 10, button.Y + button.Height / 2);

        Assert.Equal(new[] { f.Name, f.Age, f.City, f.Salary }, f.Source.Columns);
        Assert.True(f.Age.IsVisible);
        Assert.Equal("Column layout reset", f.Said[^1]);
    }

    [Fact]
    public void Reset_IsDisabled_WhileTheLayoutIsTheDefault()
    {
        var f = new Fixture();
        var events = new List<KumikoColumnsChangedEventArgs>();
        f.Source.ColumnsChanged += (_, e) => events.Add(e);
        var button = f.Chooser.ResetBounds;

        f.Chooser.Tap(button.X + 10, button.Y + button.Height / 2);

        Assert.Empty(events);
        Assert.Empty(f.Said);
    }

    // ── Escape, close and the keyboard ───────────────────────────

    [Fact]
    public void Escape_ClearsTheSearchFirst_ThenAsksToClose()
    {
        var f = new Fixture();
        Focus(f.Chooser);
        f.Chooser.SearchText = "ag";

        f.Key(KumikoKey.Escape);
        Assert.Equal("", f.Chooser.SearchText);
        Assert.Equal(4, f.Entries.Count);
        Assert.Equal(0, f.Closes);

        f.Key(KumikoKey.Escape);
        Assert.Equal(1, f.Closes);
    }

    [Fact]
    public void TheCloseButton_AsksToClose()
    {
        var f = new Fixture();
        var close = f.Chooser.CloseBounds;

        f.Chooser.Tap(close.X + close.Width / 2, close.Y + close.Height / 2);

        Assert.Equal(1, f.Closes);
    }

    [Fact]
    public void Tab_CyclesSearchListButtonsAndClose_SkippingDisabledButtons()
    {
        var f = new Fixture();
        Focus(f.Chooser);
        Assert.Equal(DrawnColumnChooser.Stop.Search, f.Chooser.CurrentStop);

        f.Key(KumikoKey.Tab);
        Assert.Equal(DrawnColumnChooser.Stop.List, f.Chooser.CurrentStop);
        f.Key(KumikoKey.Tab); // Show all and Reset have nothing to do
        Assert.Equal(DrawnColumnChooser.Stop.Close, f.Chooser.CurrentStop);
        f.Key(KumikoKey.Tab);
        Assert.Equal(DrawnColumnChooser.Stop.Search, f.Chooser.CurrentStop);

        f.Age.IsVisible = false;
        f.Key(KumikoKey.Tab, InputModifiers.Shift);
        Assert.Equal(DrawnColumnChooser.Stop.Close, f.Chooser.CurrentStop);
        f.Key(KumikoKey.Tab, InputModifiers.Shift);
        Assert.Equal(DrawnColumnChooser.Stop.Reset, f.Chooser.CurrentStop);
        f.Key(KumikoKey.Tab, InputModifiers.Shift);
        Assert.Equal(DrawnColumnChooser.Stop.ShowAll, f.Chooser.CurrentStop);
    }

    [Fact]
    public void KeyboardOnly_HideTwoColumns_MoveOne_ShowAll_Reset_AndClose()
    {
        var f = new Fixture();
        Focus(f.Chooser);

        f.Key(KumikoKey.Down);              // Name
        f.Key(KumikoKey.End);               // Salary
        f.Key(KumikoKey.Space);             // hide Salary
        f.Key(KumikoKey.Home);              // Name
        f.Key(KumikoKey.Down);              // Age
        f.Key(KumikoKey.Space);             // hide Age
        f.Key(KumikoKey.Up, InputModifiers.Alt); // Age to the top
        Assert.Equal(new[] { f.Age, f.Name, f.City, f.Salary }, f.Source.Columns);
        Assert.False(f.Age.IsVisible);
        Assert.False(f.Salary.IsVisible);

        f.Key(KumikoKey.Tab);               // Show all
        Assert.Equal(DrawnColumnChooser.Stop.ShowAll, f.Chooser.CurrentStop);
        f.Key(KumikoKey.Enter);
        Assert.True(f.Age.IsVisible && f.Salary.IsVisible);

        f.Key(KumikoKey.Tab);               // Reset (the order still differs)
        Assert.Equal(DrawnColumnChooser.Stop.Reset, f.Chooser.CurrentStop);
        f.Key(KumikoKey.Space);
        Assert.Equal(new[] { f.Name, f.Age, f.City, f.Salary }, f.Source.Columns);

        f.Key(KumikoKey.Tab);               // Close (Reset has nothing left to do)
        Assert.Equal(DrawnColumnChooser.Stop.Close, f.Chooser.CurrentStop);
        f.Key(KumikoKey.Enter);
        Assert.Equal(1, f.Closes);
    }

    [Fact]
    public void UpFromTheFirstEntry_ReturnsToTheSearchBox_AndPagingStaysInTheList()
    {
        var f = new Fixture();
        Focus(f.Chooser);

        f.Key(KumikoKey.Down);
        f.Key(KumikoKey.PageDown);
        Assert.Same(f.Salary, f.Chooser.FocusedColumn);
        f.Key(KumikoKey.PageUp);
        Assert.Same(f.Name, f.Chooser.FocusedColumn);

        f.Key(KumikoKey.Up);
        Assert.Equal(DrawnColumnChooser.Stop.Search, f.Chooser.CurrentStop);
    }

    [Fact]
    public void EveryKey_IsTheChoosers()
    {
        var f = new Fixture();
        Focus(f.Chooser);

        Assert.True(f.Key(KumikoKey.Left));
        Assert.True(f.Key(KumikoKey.Down));
        Assert.True(f.Key(KumikoKey.Right));
        Assert.True(f.Key(KumikoKey.F2));
    }

    [Fact]
    public void Announcements_TheEntryTheKeyboardReaches_WithItsStateAndPosition()
    {
        var f = new Fixture(x =>
        {
            x.Age.IsVisible = false;
            x.City.IsVisible = x.Salary.IsVisible = false; // Name is the last visible column
        });
        Focus(f.Chooser);

        f.Key(KumikoKey.Down);
        f.Key(KumikoKey.Down);

        Assert.Equal(new[]
        {
            "Name, checked, 1 of 4, This column cannot be hidden",
            "Age, not checked, 2 of 4",
        }, f.Said);
    }

    // ── Theme and drawing ────────────────────────────────────────

    public static TheoryData<KumikoThemeMode> Themes => new()
    {
        KumikoThemeMode.Light, KumikoThemeMode.Dark, KumikoThemeMode.HighContrast,
    };

    [Theory]
    [MemberData(nameof(Themes))]
    public void ApplyTheme_TakesTheColorsFromTheStyle_AndDrawsWithThem(KumikoThemeMode mode)
    {
        var style = KumikoTheme.Create(mode);
        var f = new Fixture();

        f.Chooser.ApplyTheme(style);
        var ctx = Draw(f.Chooser);

        Assert.Equal(style.BackgroundColor, f.Chooser.BackgroundColor);
        Assert.Equal(style.CellTextColor, f.Chooser.TextColor);
        Assert.Equal(style.AccentColor, f.Chooser.AccentColor);
        Assert.Equal(style.HeaderBorderColor, f.Chooser.BorderColor);
        Assert.Equal(style.CellFont, f.Chooser.Font);
        Assert.Contains(ctx.Fills, o => o.Rect.Equals(f.Chooser.Bounds) && o.Color == style.BackgroundColor);
        Assert.Equal(style.CellTextColor, ctx.SingleText("Name").Color);
        Assert.Single(ctx.TextsEqual("Columns"));
    }

    [Fact]
    public void Draws_OnlyTheRowsInView()
    {
        var f = new Fixture(x =>
        {
            x.Source.SetColumns(Enumerable.Range(0, 200).Select(i => new KumikoColumn { Header = $"Col {i:D3}" }));
        });

        var ctx = Draw(f.Chooser);

        int drawn = ctx.Texts.Count(o => o.Text?.StartsWith("Col ", StringComparison.Ordinal) == true);
        Assert.InRange(drawn, 1, 12);
    }

    [Fact]
    public void Draws_TheNoMatchesText_WhenTheSearchFindsNothing()
    {
        var f = new Fixture();
        f.Chooser.SearchText = "zzz";

        var ctx = Draw(f.Chooser);

        Assert.Single(ctx.TextsEqual("No matching columns"));
    }
}

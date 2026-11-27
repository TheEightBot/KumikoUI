using KumikoUI.Core.Components;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.Input;

/// <summary>
/// The filter popup's condition filters through the grid's input controller (#325): the operator drop-down and the
/// calendar open inside the popup and close alone (an outside press, Escape), Tab and Shift+Tab move through the page,
/// Enter applies, and typed text, the clipboard and the screen reader follow the value box.
/// </summary>
public class FilterPopupConditionsInputTests
{
    private sealed class Row
    {
        public string Name { get; set; } = "";
        public double Amount { get; set; }
        public DateTime When { get; set; }
    }

    private sealed class FakeClipboard : IKumikoClipboard
    {
        public string? Text { get; set; }
        public bool TrySetText(string text) { Text = text; return true; }
        public Task<string?> GetTextAsync() => Task.FromResult(Text);
    }

    private sealed class Grid
    {
        public KumikoSource Source { get; } = new() { Culture = System.Globalization.CultureInfo.InvariantCulture };
        public KumikoColumn Name { get; } = new() { Header = "Name", PropertyName = "Name", Width = 150 };
        public KumikoColumn Amount { get; } = new() { Header = "Amount", PropertyName = "Amount", Width = 150, ColumnType = KumikoColumnType.Numeric };
        public KumikoColumn When { get; } = new() { Header = "When", PropertyName = "When", Width = 150, ColumnType = KumikoColumnType.Date };
        public KumikoSelectionModel Selection { get; }
        public KumikoInputController Input { get; } = new();
        public ScrollState Scroll { get; } = new() { ViewportWidth = 600, ViewportHeight = 400 };
        public KumikoStyle Style { get; } = new();
        public int Closed { get; private set; }
        public List<string> Said { get; } = new();

        public Grid()
        {
            Source.SetColumns(new[] { Name, Amount, When });
            Source.SetItems(Enumerable.Range(0, 30)
                .Select(i => new Row { Name = $"N{i:D2}", Amount = i * 10, When = DateTime.Today.AddDays(i - 15) }).ToList());
            Selection = new KumikoSelectionModel(Source);
            Input.FilterPopupClosed += (_, _) => Closed++;
            Input.AnnouncementRequested += Said.Add;
            new KumikoRenderer().Render(new RecordingDrawingContext(), Source, Scroll, Selection, Style);
        }

        public void Tap(float x, float y)
        {
            Input.HandlePointer(new KumikoPointerEventArgs { X = x, Y = y, Action = InputAction.Pressed }, Scroll, Selection, Style, Source);
            Input.HandlePointer(new KumikoPointerEventArgs { X = x, Y = y, Action = InputAction.Released }, Scroll, Selection, Style, Source);
        }

        public void Key(KumikoKey key, InputModifiers modifiers = InputModifiers.None, char? character = null) =>
            Input.HandleKey(new KumikoKeyEventArgs { Key = key, Modifiers = modifiers, Character = character }, Scroll, Selection, Style, Source);

        /// <summary>Opens the column's filter popup with its filter icon and draws a frame of it.</summary>
        public DrawnFilterPopup OpenFilter(KumikoColumn column)
        {
            int index = column == Name ? 0 : column == Amount ? 1 : 2;
            float x = index * 150 + 137; // the middle of the filter icon
            Tap(x, Style.HeaderHeight / 2);
            var popup = Assert.IsType<DrawnFilterPopup>(Input.ActivePopup);
            Frame();
            return popup;
        }

        /// <summary>Draws the popups, which places them and their editors.</summary>
        public RecordingDrawingContext Frame()
        {
            var ctx = new RecordingDrawingContext();
            Input.PopupManager.DrawPopups(ctx);
            return ctx;
        }

        /// <summary>A tap at an offset from the popup's top-left corner.</summary>
        public void TapIn(DrawnFilterPopup popup, float dx, float dy)
        {
            Tap(popup.Bounds.X + dx, popup.Bounds.Y + dy);
            Frame();
        }
    }

    // Offsets in a popup (see DrawnFilterPopupConditionsTests): the Conditions tab, the first operator and value
    private const float TabY = 56, ConditionsTabX = 160, ValuesTabX = 50, Operator1Y = 87, Value1Y = 119;

    private static DrawnFilterPopup OpenConditions(Grid g, KumikoColumn column)
    {
        var popup = g.OpenFilter(column);
        g.TapIn(popup, ConditionsTabX, TabY);
        Assert.Equal(KumikoFilterPopupMode.Conditions, popup.Mode);
        return popup;
    }

    // ── Nested drop-down and calendar ─────────────────────────────

    [Fact]
    public void TheOperatorDropDown_OpensInsideThePopup_AndAnOutsidePressClosesOnlyIt()
    {
        var g = new Grid();
        var popup = OpenConditions(g, g.Amount);

        g.TapIn(popup, 100, Operator1Y);
        Assert.True(popup.OperatorBox(0).IsDropdownOpen);
        Assert.Same(popup, g.Input.ActivePopup);

        g.Tap(590, 390); // outside the popup and the drop-down
        Assert.False(popup.OperatorBox(0).IsDropdownOpen);
        Assert.Same(popup, g.Input.ActivePopup);
        Assert.Equal(0, g.Closed);

        g.Tap(590, 390); // the next one closes the popup, and selects nothing
        Assert.Null(g.Input.ActivePopup);
        Assert.Equal(1, g.Closed);
        Assert.False(g.Selection.CurrentCell.IsValid);
    }

    [Fact]
    public void TheOperatorDropDown_DrawsOverThePopup_AndPicksOnRelease()
    {
        var g = new Grid();
        var popup = OpenConditions(g, g.Amount);
        g.TapIn(popup, 100, Operator1Y);

        var ctx = g.Frame();
        int ok = ctx.Ops.FindIndex(o => o.Text == "OK");
        int lessThan = ctx.Ops.FindIndex(o => o.Text == "Less than");
        Assert.True(lessThan > ok, "the drop-down is drawn after the popup's buttons");

        var item = ctx.Texts.First(t => t.Text == "Greater than");
        g.Tap(popup.Bounds.X + 60, item.Rect.Bottom - 2); // the baseline is the middle of the item

        Assert.False(popup.OperatorBox(0).IsDropdownOpen);
        Assert.Equal("Greater than", popup.OperatorBox(0).SelectedText);
        Assert.Same(popup, g.Input.ActivePopup);
    }

    [Fact]
    public void Escape_ClosesTheDropDownFirst_ThenThePopup()
    {
        var g = new Grid();
        var popup = OpenConditions(g, g.Amount);

        g.Key(KumikoKey.Down); // opens the first operator's drop-down
        Assert.True(popup.OperatorBox(0).IsDropdownOpen);

        g.Key(KumikoKey.Escape);
        Assert.False(popup.OperatorBox(0).IsDropdownOpen);
        Assert.Same(popup, g.Input.ActivePopup);

        g.Key(KumikoKey.Escape);
        Assert.Null(g.Input.ActivePopup);
        Assert.Null(g.Amount.ActiveFilter);
    }

    [Fact]
    public void TheCalendar_OpensInsideThePopup_ClosesAlone_AndItsDateIsTheValue()
    {
        var g = new Grid();
        var popup = OpenConditions(g, g.When);

        g.TapIn(popup, 100, Value1Y);
        var picker = popup.ValueDatePicker(0);
        Assert.True(picker.IsCalendarOpen);
        var calendar = picker.GetCalendarBounds();
        Assert.Same(popup, g.Input.PopupManager.HitTest(calendar.X + calendar.Width / 2, calendar.Bottom - 2));

        g.Tap(595, 395);
        Assert.False(picker.IsCalendarOpen);
        Assert.Same(popup, g.Input.ActivePopup);

        g.TapIn(popup, 100, Value1Y);
        g.Key(KumikoKey.Down);  // the calendar selects today
        g.Key(KumikoKey.Enter); // and closes, leaving the popup open
        Assert.False(picker.IsCalendarOpen);
        Assert.Same(popup, g.Input.ActivePopup);

        g.Key(KumikoKey.Enter); // applies
        Assert.Null(g.Input.ActivePopup);
        Assert.Equal(DateTime.Today, g.When.ActiveFilter!.Condition1!.Value);
        Assert.Equal(1, g.Source.RowCount);
    }

    // ── Keyboard ──────────────────────────────────────────────────

    [Fact]
    public void Tab_MovesThroughTheConditionsPage_AndShiftTabBack()
    {
        var g = new Grid();
        var popup = OpenConditions(g, g.Amount);
        Assert.True(g.Input.WouldHandleTab(g.Selection, g.Source, backwards: false));

        var forward = new List<DrawnFilterPopup.FocusStop> { popup.CurrentStop };
        for (int i = 0; i < 9; i++)
        {
            g.Key(KumikoKey.Tab);
            forward.Add(popup.CurrentStop);
        }

        Assert.Equal(new[]
        {
            DrawnFilterPopup.FocusStop.Operator1, DrawnFilterPopup.FocusStop.Value1, DrawnFilterPopup.FocusStop.Combination,
            DrawnFilterPopup.FocusStop.Operator2, DrawnFilterPopup.FocusStop.Value2, DrawnFilterPopup.FocusStop.Ok,
            DrawnFilterPopup.FocusStop.Cancel, DrawnFilterPopup.FocusStop.Clear, DrawnFilterPopup.FocusStop.ModeSwitch,
            DrawnFilterPopup.FocusStop.Operator1,
        }, forward);

        g.Key(KumikoKey.Tab, InputModifiers.Shift);
        Assert.Equal(DrawnFilterPopup.FocusStop.ModeSwitch, popup.CurrentStop);
        g.Key(KumikoKey.Tab, InputModifiers.Shift);
        Assert.Equal(DrawnFilterPopup.FocusStop.Clear, popup.CurrentStop);
    }

    [Fact]
    public void Tab_OnTheValuesPage_ReachesTheSwitchTheSearchAndTheButtons()
    {
        var g = new Grid();
        var popup = g.OpenFilter(g.Name);
        Assert.Equal(DrawnFilterPopup.FocusStop.Search, popup.CurrentStop);

        g.Key(KumikoKey.Tab);
        Assert.Equal(DrawnFilterPopup.FocusStop.Ok, popup.CurrentStop);
        g.Key(KumikoKey.Tab);
        g.Key(KumikoKey.Tab);
        Assert.Equal(DrawnFilterPopup.FocusStop.Clear, popup.CurrentStop);
        g.Key(KumikoKey.Tab);
        Assert.Equal(DrawnFilterPopup.FocusStop.ModeSwitch, popup.CurrentStop);

        g.Key(KumikoKey.Right); // the switch: Right shows Conditions
        Assert.Equal(KumikoFilterPopupMode.Conditions, popup.Mode);
        Assert.Contains("Conditions", g.Said);
        g.Key(KumikoKey.Left);
        Assert.Equal(KumikoFilterPopupMode.Values, popup.Mode);

        g.Key(KumikoKey.Character, character: 'n'); // typing goes to the search box
        Assert.Equal(DrawnFilterPopup.FocusStop.Search, popup.CurrentStop);
        Assert.Equal("n", popup.SearchText);
    }

    [Fact]
    public void Enter_Applies_ButOnCancelItCancels()
    {
        var g = new Grid();
        var popup = OpenConditions(g, g.Amount);
        g.Key(KumikoKey.Tab);
        g.Key(KumikoKey.Character, character: '5');
        for (int i = 0; i < 5; i++) g.Key(KumikoKey.Tab); // to Cancel
        Assert.Equal(DrawnFilterPopup.FocusStop.Cancel, popup.CurrentStop);

        g.Key(KumikoKey.Enter);
        Assert.Null(g.Input.ActivePopup);
        Assert.Null(g.Amount.ActiveFilter);

        popup = OpenConditions(g, g.Amount);
        g.Key(KumikoKey.Tab);
        g.Key(KumikoKey.Character, character: '5');
        g.Key(KumikoKey.Character, character: '0');
        g.Key(KumikoKey.Enter);
        Assert.Null(g.Input.ActivePopup);
        Assert.Equal(50.0, g.Amount.ActiveFilter!.Condition1!.Value);
        Assert.Equal(1, g.Source.RowCount);
    }

    // ── Text input, clipboard, screen reader ──────────────────────

    [Fact]
    public void TextInput_GoesToTheFocusedValueBox()
    {
        var g = new Grid();
        var popup = g.OpenFilter(g.Amount);
        Assert.Same(popup.SearchTextInput, g.Input.ActiveTextInput);

        g.TapIn(popup, ConditionsTabX, TabY);
        Assert.Null(g.Input.ActiveTextInput); // the keyboard is on the operator drop-down

        int focusRequests = 0;
        g.Input.KeyboardFocusRequested += (_, _) => focusRequests++;
        g.TapIn(popup, 100, Value1Y);
        Assert.Same(popup.ValueTextBox(0), g.Input.ActiveTextInput);
        Assert.True(focusRequests > 0); // the host shows the soft keyboard

        var input = g.Input.ActiveTextInput!;
        input.SetComposition("42", 2);
        input.CommitComposition();
        Assert.Equal("42", popup.ValueTextBox(0).Text);

        g.TapIn(popup, ValuesTabX, TabY);
        Assert.Same(popup.SearchTextInput, g.Input.ActiveTextInput);
    }

    [Fact]
    public void TheValueBox_PastesFromTheClipboard()
    {
        var g = new Grid();
        g.Input.Clipboard = new FakeClipboard { Text = "120" };
        var popup = OpenConditions(g, g.Amount);
        g.Key(KumikoKey.Tab);

        g.Key(KumikoKey.V, InputModifiers.Control);

        Assert.Equal("120", popup.ValueTextBox(0).Text);
    }

    [Fact]
    public void TheScreenReader_HearsThePageTheOperatorAndWhereTabGoes()
    {
        var g = new Grid();
        OpenConditions(g, g.Amount);
        Assert.Contains("Conditions", g.Said);

        g.Key(KumikoKey.Down); // opens the drop-down
        g.Key(KumikoKey.Down); // moves to the next operator
        Assert.Equal("Does not equal", g.Said[^1]);

        g.Key(KumikoKey.Tab);
        Assert.Equal("Value", g.Said[^1]);
        g.Key(KumikoKey.Tab);
        Assert.Equal("And", g.Said[^1]);
        g.Key(KumikoKey.Space);
        Assert.Equal("Or", g.Said[^1]);
    }
}

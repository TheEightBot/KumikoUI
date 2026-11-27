using KumikoUI.Core.Components;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.Input;

/// <summary>
/// The drawn search panel (#317): Ctrl/Cmd+F opens it when allowed, its box takes text (keys, input-method
/// composition, paste) and sets the source's search after a pause, Enter and Shift+Enter move between matches,
/// Escape closes it and clears the search, and it is modeless: a press on the grid takes the keyboard back and keeps
/// the panel open.
/// </summary>
public class SearchPanelTests
{
    private const float Width = 600f;

    private static InputGrid NewGrid(bool allow = true) => new(viewportWidth: Width, configure: g =>
    {
        g.Input.AllowSearchPanel = allow;
        g.Input.SearchDebounceMs = 0;
    });

    private static DrawnSearchPanel Panel(InputGrid grid) =>
        grid.Input.SearchPanel ?? throw new InvalidOperationException("The search panel is not open.");

    private static void TypeText(InputGrid grid, string text)
    {
        foreach (char c in text) grid.Type(c);
    }

    [Fact]
    public void CtrlF_IsLeftToThePage_WhenThePanelIsNotAllowed()
    {
        var grid = NewGrid(allow: false);

        var e = grid.Key(KumikoKey.F, InputModifiers.Control);

        Assert.False(e.Handled);
        Assert.False(grid.Input.IsSearchPanelOpen);
    }

    [Fact]
    public void CtrlF_OpensThePanel_WithTheKeyboard()
    {
        var grid = NewGrid();
        int focusRequests = 0;
        grid.Input.KeyboardFocusRequested += (_, _) => focusRequests++;

        var e = grid.Key(KumikoKey.F, InputModifiers.Control);

        Assert.True(e.Handled);
        Assert.True(grid.Input.IsSearchPanelOpen);
        Assert.Same(Panel(grid).TextInput, grid.Input.ActiveTextInput);
        Assert.True(focusRequests > 0);
        Assert.Same(Panel(grid), grid.Input.ActivePopup);
        // At the top right of the data area, below the header
        Assert.Equal(Width - DrawnSearchPanel.PanelWidth - 12, Panel(grid).Bounds.X, 1);
        Assert.True(Panel(grid).Bounds.Y >= grid.Style.HeaderHeight);
    }

    [Fact]
    public void Typing_SetsTheSearch_AndTheCountIsShown()
    {
        var grid = NewGrid();
        grid.Key(KumikoKey.F, InputModifiers.Control);

        TypeText(grid, "rome");

        Assert.Equal("rome", grid.Source.SearchText);
        Assert.Equal(15, grid.Source.SearchMatchCount);
        Assert.Equal("15 matches", Panel(grid).CountText);
    }

    [Fact]
    public void Enter_And_ShiftEnter_MoveBetweenTheMatches()
    {
        var grid = NewGrid();
        grid.Key(KumikoKey.F, InputModifiers.Control);
        TypeText(grid, "rome");

        grid.Key(KumikoKey.Enter);
        Assert.Equal(new CellPosition(1, 2), grid.Selection.CurrentCell);
        Assert.Equal("1 of 15", Panel(grid).CountText);
        grid.Key(KumikoKey.Enter);
        Assert.Equal(new CellPosition(3, 2), grid.Selection.CurrentCell);
        grid.Key(KumikoKey.Enter, InputModifiers.Shift);
        Assert.Equal(new CellPosition(1, 2), grid.Selection.CurrentCell);
        grid.Key(KumikoKey.Enter, InputModifiers.Shift); // wraps to the last match
        Assert.Equal(new CellPosition(29, 2), grid.Selection.CurrentCell);
        Assert.Equal("15 of 15", Panel(grid).CountText);
        Assert.True(Panel(grid).IsFocused, "the panel keeps the keyboard");
    }

    [Fact]
    public void TheButtons_MoveAndClose()
    {
        var grid = NewGrid();
        grid.Key(KumikoKey.F, InputModifiers.Control);
        TypeText(grid, "oslo");
        var b = Panel(grid).Bounds;
        float y = b.Y + b.Height / 2;
        float closeX = b.Right - 6 - 13, nextX = closeX - 26, previousX = nextX - 26;

        grid.Tap(nextX, y);
        Assert.Equal(new CellPosition(0, 2), grid.Selection.CurrentCell);
        grid.Tap(previousX, y);
        Assert.Equal(new CellPosition(28, 2), grid.Selection.CurrentCell);
        grid.Tap(closeX, y);

        Assert.False(grid.Input.IsSearchPanelOpen);
        Assert.Equal(string.Empty, grid.Source.SearchText);
    }

    [Fact]
    public void Escape_ClosesThePanel_AndClearsTheSearch()
    {
        var grid = NewGrid();
        int changes = 0;
        grid.Input.SearchPanelStateChanged += (_, _) => changes++;
        grid.Key(KumikoKey.F, InputModifiers.Control);
        TypeText(grid, "oslo");

        var e = grid.Key(KumikoKey.Escape);

        Assert.True(e.Handled);
        Assert.False(grid.Input.IsSearchPanelOpen);
        Assert.Equal(string.Empty, grid.Source.SearchText);
        Assert.Null(grid.Input.ActiveTextInput);
        Assert.True(changes >= 2);
    }

    [Fact]
    public void APressOnTheGrid_TakesTheKeyboardBack_AndKeepsThePanelOpen()
    {
        var grid = NewGrid();
        grid.Key(KumikoKey.F, InputModifiers.Control);
        TypeText(grid, "oslo");

        grid.Tap(60, grid.RowY(3));

        Assert.True(grid.Input.IsSearchPanelOpen);
        Assert.False(Panel(grid).IsFocused);
        Assert.Equal(3, grid.Selection.CurrentCell.Row); // the press reached the grid
        Assert.Null(grid.Input.ActiveTextInput);
        grid.Key(KumikoKey.Down);
        Assert.Equal(4, grid.Selection.CurrentCell.Row); // keys go to the grid again
        Assert.Equal("oslo", grid.Source.SearchText);

        // Ctrl+F again gives the panel the keyboard, with its text selected for retyping
        grid.Key(KumikoKey.F, InputModifiers.Control);
        Assert.True(Panel(grid).IsFocused);
        grid.Type('x');
        Assert.Equal("x", grid.Source.SearchText);
    }

    [Fact]
    public void APressOutside_ClosesAFilterPopup_ButNotThePanel()
    {
        var grid = NewGrid();
        grid.Key(KumikoKey.F, InputModifiers.Control);
        grid.Input.PopupManager.Show(new ProbeComponent { Bounds = new KumikoRect(10, 100, 50, 50) },
            new KumikoRect(10, 100, 50, 0));

        grid.Tap(200, grid.RowY(5));

        Assert.True(grid.Input.IsSearchPanelOpen);
        Assert.Same(Panel(grid), grid.Input.PopupManager.TopPopup);
        Assert.NotEqual(5, grid.Selection.CurrentCell.Row); // the press only closed the modal popup
    }

    [Fact]
    public void TheBox_TakesInputMethodComposition()
    {
        var grid = NewGrid();
        grid.Key(KumikoKey.F, InputModifiers.Control);
        var input = grid.Input.ActiveTextInput!;

        input.SetComposition("ro", 2);
        Assert.Equal("ro", Panel(grid).Text);
        grid.Key(KumikoKey.Enter); // Enter commits the composition, it does not search yet
        Assert.Null(input.Composition);
        Assert.True(grid.Input.IsSearchPanelOpen);
        Assert.False(grid.Selection.CurrentCell.IsValid);

        input.InsertText("me");
        Assert.Equal("rome", grid.Source.SearchText);
    }

    [Fact]
    public void ThePanel_FollowsASearchSetElsewhere()
    {
        var grid = NewGrid();
        grid.Key(KumikoKey.F, InputModifiers.Control);

        grid.Source.SearchText = "Eng";

        Assert.Equal("Eng", Panel(grid).Text);
        Assert.Equal("10 matches", Panel(grid).CountText);
    }

    [Fact]
    public void ShowSearchPanel_Works_WithoutAllowSearchPanel_AndCloseSearchPanelClears()
    {
        var grid = NewGrid(allow: false);
        grid.Source.SearchText = "Ops";

        grid.Input.ShowSearchPanel(grid.Scroll, grid.Selection, grid.Style, grid.Source);
        Assert.Equal("Ops", Panel(grid).Text);
        grid.Input.CloseSearchPanel();

        Assert.False(grid.Input.IsSearchPanelOpen);
        Assert.Equal(string.Empty, grid.Source.SearchText);
    }

    [Fact]
    public void CtrlF_WhileEditing_CommitsTheEdit_AndOpensThePanel()
    {
        var grid = NewGrid();
        grid.Select(0, 0);
        grid.Key(KumikoKey.F2);
        Assert.True(grid.Edit.IsEditing);
        grid.Type('Z');

        grid.Key(KumikoKey.F, InputModifiers.Control);

        Assert.False(grid.Edit.IsEditing);
        Assert.Equal("Z", grid.NameAt(0));
        Assert.True(grid.Input.IsSearchPanelOpen);
    }

    [Fact]
    public void Typing_WaitsForAPause_BeforeSearching()
    {
        using var ui = new TestUiThread();
        var grid = ui.Invoke(() => new InputGrid(viewportWidth: Width, configure: g =>
        {
            g.Input.AllowSearchPanel = true;
            g.Input.SearchDebounceMs = 100;
        }));

        string immediately = ui.Invoke(() =>
        {
            grid.Key(KumikoKey.F, InputModifiers.Control);
            grid.Type('o');
            return grid.Source.SearchText;
        });
        Assert.Equal(string.Empty, immediately);

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (ui.Invoke(() => grid.Source.SearchText) != "o" && DateTime.UtcNow < deadline)
            Thread.Sleep(20);
        Assert.Equal("o", ui.Invoke(() => grid.Source.SearchText));

        // Enter right after typing searches for what was typed, without waiting.
        var current = ui.Invoke(() =>
        {
            grid.Type('s');
            grid.Key(KumikoKey.Enter);
            return (grid.Source.SearchText, grid.Selection.CurrentCell);
        });
        Assert.Equal("os", current.SearchText);
        Assert.Equal(new CellPosition(0, 2), current.CurrentCell);
    }

    [Fact]
    public void AnOpenPanel_DrawsItsBoxCountAndButtons()
    {
        var grid = NewGrid();
        grid.Key(KumikoKey.F, InputModifiers.Control);
        TypeText(grid, "eng");

        var ctx = new RecordingDrawingContext();
        grid.Renderer.RenderFrame(ctx, grid.Source, grid.Scroll, grid.Selection, grid.Style, -1, 0, -1, 0,
            grid.Edit, grid.Input.PopupManager);

        Assert.Contains(ctx.Texts, o => o.Text == "10 matches");
        Assert.Contains(ctx.Texts, o => o.Text == "eng");
        Assert.Contains(ctx.Fills, o => o.Color == grid.Style.SearchHighlightColor); // the Eng cells under the panel
    }
}

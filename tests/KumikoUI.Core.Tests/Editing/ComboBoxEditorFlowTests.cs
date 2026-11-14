using KumikoUI.Core.Components;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.Editing;

/// <summary>
/// The combo box editor inside a grid (Uno verification F5): picking an item and pressing Enter commits the edit
/// instead of opening the next cell's list, and the open list is drawn on top of the rows below it.
/// </summary>
public class ComboBoxEditorFlowTests
{
    private sealed class Row
    {
        public string Dept { get; set; } = "";
        public string Name { get; set; } = "";
    }

    private sealed class Grid
    {
        public KumikoSource Source { get; } = new();
        public KumikoStyle Style { get; } = new();
        public ScrollState Scroll { get; } = new() { ViewportWidth = 400, ViewportHeight = 400 };
        public KumikoSelectionModel Selection { get; }
        public EditSession Edit { get; } = new();
        public KumikoInputController Input { get; } = new();
        public KumikoRenderer Renderer { get; } = new();
        public KumikoColumn Dept { get; } = new()
        {
            Header = "Dept", PropertyName = "Dept", Width = 150,
            ColumnType = KumikoColumnType.ComboBox, EditorItemsString = "Eng, Sales, Ops, Support",
        };
        public KumikoColumn Name { get; } = new() { Header = "Name", PropertyName = "Name", Width = 150 };

        public Grid()
        {
            Selection = new KumikoSelectionModel(Source);
            Source.SetColumns(new[] { Dept, Name });
            Source.SetItems(Enumerable.Range(0, 5).Select(i => new Row { Dept = "Eng", Name = $"Person {i}" }).ToList());
            Input.EditSession = Edit;
            Render();
        }

        public RecordingDrawingContext Render()
        {
            var ctx = new RecordingDrawingContext();
            Renderer.Render(ctx, Source, Scroll, Selection, Style, editSession: Edit);
            return ctx;
        }

        public void Key(KumikoKey key) =>
            Input.HandleKey(new KumikoKeyEventArgs { Key = key }, Scroll, Selection, Style, Source);

        public void Tap(float x, float y, int clicks = 1)
        {
            Input.HandlePointer(new KumikoPointerEventArgs { X = x, Y = y, Action = InputAction.Pressed, ClickCount = clicks },
                Scroll, Selection, Style, Source);
            Input.HandlePointer(new KumikoPointerEventArgs { X = x, Y = y, Action = InputAction.Released, ClickCount = clicks },
                Scroll, Selection, Style, Source);
        }

        public float RowY(int row) => Style.HeaderHeight + row * Style.RowHeight;

        public DrawnComboBox Combo => Assert.IsType<DrawnComboBox>(Edit.ActiveEditor);
    }

    [Fact]
    public void PickingAnItem_CommitsTheEdit_AtOnce()
    {
        var g = new Grid();
        g.Tap(75, g.RowY(0) + g.Style.RowHeight / 2, clicks: 2);
        g.Render();
        var combo = g.Combo;
        Assert.True(combo.IsDropdownOpen);

        var drop = combo.GetDropdownBounds();
        g.Tap(drop.X + 20, drop.Y + combo.ItemHeight * 1.5f); // "Sales"

        Assert.False(g.Edit.IsEditing);
        Assert.Equal("Sales", ((Row)g.Source.GetItem(0)!).Dept);
        Assert.Equal(new CellPosition(0, 0), g.Selection.CurrentCell);
    }

    [Fact]
    public void WithTheListClosedByEscape_Enter_CommitsTheEdit_AndOpensNoOtherList()
    {
        var g = new Grid();
        g.Tap(75, g.RowY(0) + g.Style.RowHeight / 2, clicks: 2);
        g.Render();
        g.Key(KumikoKey.Down);
        g.Key(KumikoKey.Escape); // closes the list and goes back to the opening value; the editor stays
        Assert.True(g.Edit.IsEditing);
        Assert.False(g.Combo.IsDropdownOpen);

        g.Key(KumikoKey.Enter);

        Assert.False(g.Edit.IsEditing);
    }

    [Fact]
    public void WithTheListOpen_Enter_PicksTheHighlightedItem_AndCommits()
    {
        var g = new Grid();
        g.Tap(75, g.RowY(0) + g.Style.RowHeight / 2, clicks: 2);
        g.Render();

        g.Key(KumikoKey.Down);
        g.Key(KumikoKey.Enter);

        Assert.False(g.Edit.IsEditing);
    }

    [Fact]
    public void TheOpenList_IsDrawnAfterTheRowsBelowIt()
    {
        var g = new Grid();
        g.Tap(75, g.RowY(0) + g.Style.RowHeight / 2, clicks: 2);
        var ctx = g.Render();

        var drop = g.Combo.GetDropdownBounds();
        var lastItem = ctx.Texts.Last(t => t.Text == "Support");
        var rowBelow = ctx.Texts.First(t => t.Text == "Person 3");
        Assert.True(drop.Bottom > g.RowY(3), "the list reaches the rows below the combo box");
        Assert.True(lastItem.Index > rowBelow.Index, "the list is painted on top of the rows");
    }

    [Fact]
    public void ALongList_DrawsEveryItemInsideItsOwnBounds_AtEveryScrollOffset()
    {
        var g = new Grid();
        g.Dept.EditorItemsString = string.Join(", ", Enumerable.Range(0, 12).Select(i => $"Dept{i:D2}"));
        g.Tap(75, g.RowY(0) + g.Style.RowHeight / 2, clicks: 2);
        var combo = g.Combo;

        for (int step = 0; step < 14; step++)
        {
            var ctx = g.Render();
            var drop = combo.GetDropdownBounds();
            foreach (var text in ctx.Texts.Where(t => t.Text!.Length == 6 && t.Text.StartsWith("Dept") && t.Rect.Top >= combo.Bounds.Bottom))
            {
                var visible = text.Visible;
                if (visible is not { } v) continue;
                Assert.True(v.Top >= drop.Top - 0.01f && v.Bottom <= drop.Bottom + 0.01f,
                    $"{text.Text} reaches {v.Top}-{v.Bottom}, the list is {drop.Top}-{drop.Bottom}");
            }
            g.Key(KumikoKey.Down);
        }
    }
}

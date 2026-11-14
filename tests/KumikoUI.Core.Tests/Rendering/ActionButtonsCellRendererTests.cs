using KumikoUI.Core.Components;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;
using static KumikoUI.Core.Tests.Components.ComponentInput;
using static KumikoUI.Core.Tests.TestHelpers.RenderHarness;

namespace KumikoUI.Core.Tests.Rendering;

/// <summary>
/// <see cref="ActionButtonsCellRenderer"/>: the display pass draws the same pills as the editor,
/// ignores actions, hands its definitions to the interactive <see cref="DrawnActionButtons"/> it creates,
/// and works as a grid column's cell renderer.
/// </summary>
public class ActionButtonsCellRendererTests
{
    private static readonly KumikoRect Cell = new(10, 20, 200, 30);

    private static ActionButtonsCellRenderer Renderer(Action? onEdit = null) => new()
    {
        Buttons = new List<IActionButtonDefinition>
        {
            new ActionButtonDefinition { Label = "Edit", BackgroundColor = new KumikoColor(1, 2, 3), Action = onEdit },
            new ActionButtonDefinition { Label = "Delete", BackgroundColor = new KumikoColor(4, 5, 6), TextColor = new KumikoColor(9, 9, 9) },
        },
    };

    private static RecordingDrawingContext Render(ActionButtonsCellRenderer renderer)
    {
        var ctx = new RecordingDrawingContext();
        renderer.Render(ctx, Cell, null, "", new KumikoColumn(), new KumikoStyle(), isSelected: false);
        return ctx;
    }

    [Fact]
    public void Render_DrawsOneLabeledPillPerButton_WithEqualWidths()
    {
        var ctx = Render(Renderer());

        var fills = ctx.Fills.ToList();
        Assert.Equal(new KumikoRect(14, 24, 93, 22), fills[0].Rect);
        Assert.Equal(new KumikoRect(113, 24, 93, 22), fills[1].Rect);
        Assert.Equal(new KumikoColor(1, 2, 3), fills[0].Color);
        Assert.Equal(new KumikoColor(4, 5, 6), fills[1].Color);
        Assert.Equal(fills[0].Rect, ctx.SingleText("Edit").Rect);
        var delete = ctx.SingleText("Delete");
        Assert.Equal(fills[1].Rect, delete.Rect);
        Assert.Equal(new KumikoColor(9, 9, 9), delete.Color);
        Assert.Equal(KumikoColor.White, ctx.SingleText("Edit").Color);
        Assert.True(delete.Font!.Value.IsBold);
    }

    [Fact]
    public void Render_NoButtons_DrawsNothing()
    {
        Assert.Empty(Render(new ActionButtonsCellRenderer()).Ops);
    }

    [Fact]
    public void Render_FollowsPaddingAndSpacing()
    {
        var renderer = Renderer();
        renderer.CellPadding = 10;
        renderer.ButtonSpacing = 20;

        var fills = Render(renderer).Fills.ToList();

        Assert.Equal(new KumikoRect(20, 30, 80, 10), fills[0].Rect);
        Assert.Equal(new KumikoRect(120, 30, 80, 10), fills[1].Rect);
    }

    [Fact]
    public void Render_NeverRunsTheButtonActions()
    {
        int fired = 0;

        Render(Renderer(() => fired++));

        Assert.Equal(0, fired);
    }

    [Fact]
    public void DisplayPass_AndEditor_DrawTheSamePills()
    {
        var renderer = Renderer();
        renderer.CornerRadius = 9;
        var display = Render(renderer);
        var editor = (DrawnActionButtons)renderer.CreateEditor(null, Cell)!;
        var edit = new RecordingDrawingContext();

        editor.OnDraw(edit);

        Assert.Equal(display.Fills.Select(o => (o.Rect, o.Color)), edit.Fills.Select(o => (o.Rect, o.Color)));
        Assert.Equal(display.Texts.Select(o => (o.Text, o.Rect)), edit.Texts.Select(o => (o.Text, o.Rect)));
    }

    [Fact]
    public void CreateEditor_SharesTheDefinitionsAndLayoutSettings_AndPassesTheRowItem()
    {
        int edited = 0;
        var renderer = Renderer(() => edited++);
        renderer.CornerRadius = 9;
        renderer.ButtonSpacing = 2;
        renderer.CellPadding = 1;
        var row = new object();

        var editor = Assert.IsType<DrawnActionButtons>(renderer.CreateEditor(row, Cell));

        Assert.Same(renderer.Buttons, editor.Buttons);
        Assert.Same(row, editor.RowItem);
        Assert.Equal(Cell, editor.Bounds);
        Assert.Equal((9f, 2f, 1f), (editor.CornerRadius, editor.ButtonSpacing, editor.CellPadding));
        Assert.True(editor.ActivatesImmediately);

        // The editor fires the shared definition's action when its first button is tapped
        editor.Down(30, 35);
        editor.Up(30, 35);
        Assert.Equal(1, edited);
    }

    [Fact]
    public void CreateEditor_SeesButtonsAddedLater()
    {
        var renderer = new ActionButtonsCellRenderer();
        var editor = (DrawnActionButtons)renderer.CreateEditor(null, Cell)!;
        int fired = 0;

        renderer.Buttons.Add(new ActionButtonDefinition { Label = "Go", Action = () => fired++ });
        editor.Down(100, 35);
        editor.Up(100, 35);

        Assert.Equal(1, fired);
    }

    [Fact]
    public void InAGridColumn_EveryRowDrawsItsButtonsInsideTheColumn()
    {
        var renderer = Renderer();
        var h = Create(rows: 3, configure: x => x.Column("City").CustomCellRenderer = renderer);

        var ctx = h.Render();

        var edits = ctx.TextsEqual("Edit").OrderBy(o => o.Rect.Y).ToList();
        Assert.Equal(3, edits.Count);
        for (int row = 0; row < 3; row++)
        {
            // City is the third column: x 250-400 after the 150 px Name and 100 px Age columns; the cell is
            // inset by the style's cell padding and the buttons by their own 4 px
            float pad = h.Style.CellPadding + 4;
            Assert.True(Near(edits[row].Rect.X, 250 + pad));
            Assert.True(Near(edits[row].Rect.Y, h.RowTop(row) + 4));
            Assert.True(edits[row].Rect.Right <= 400 - pad + 0.01f);
        }
        Assert.Equal(3, ctx.TextsEqual("Delete").Count());
        Assert.Empty(ctx.TextsEqual("Oslo")); // the renderer replaces the default text
    }
}

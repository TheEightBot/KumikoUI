using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Tests.Input;

/// <summary>
/// One test per row of the README "Keyboard Navigation" table, so the documented keys are the tested keys.
/// Edge cases for each key live in <see cref="KeyboardNavigationTests"/> and <see cref="EditingKeyRoutingTests"/>.
/// </summary>
public class KeyboardTableTests
{
    private static InputGrid Grid(Action<InputGrid>? configure = null) => new(rows: 30, configure: configure);

    [Fact]
    public void ArrowKeys_MoveTheCurrentCellOneCellOrRow()
    {
        var g = Grid();
        g.Select(5, 2);

        Assert.True(g.Key(KumikoKey.Down).Handled);
        Assert.Equal(new CellPosition(6, 2), g.Selection.CurrentCell);
        Assert.True(g.Key(KumikoKey.Right).Handled);
        Assert.Equal(new CellPosition(6, 3), g.Selection.CurrentCell);
        Assert.True(g.Key(KumikoKey.Up).Handled);
        Assert.Equal(new CellPosition(5, 3), g.Selection.CurrentCell);
        Assert.True(g.Key(KumikoKey.Left).Handled);
        Assert.Equal(new CellPosition(5, 2), g.Selection.CurrentCell);
    }

    [Fact]
    public void ShiftArrow_ExtendsTheRowSelection()
    {
        var g = Grid();
        g.Tap(10, g.RowY(1));

        g.Key(KumikoKey.Down, InputModifiers.Shift);

        Assert.Equal(new[] { 1, 2 }, g.Selection.SelectedRows.OrderBy(r => r));
    }

    [Fact]
    public void Tab_MovesToTheNextEditableCell_AndEditsIt()
    {
        var g = Grid(x => x.Age.IsReadOnly = true);
        g.Select(0, 0);

        Assert.True(g.Key(KumikoKey.Tab).Handled);

        Assert.Equal(new CellPosition(0, 2), g.Selection.CurrentCell);
        Assert.True(g.Edit.IsEditing);
    }

    [Fact]
    public void Tab_WrapsToTheNextRow()
    {
        var g = Grid();
        g.Select(0, 4);

        g.Key(KumikoKey.Tab);

        Assert.Equal(new CellPosition(1, 0), g.Selection.CurrentCell);
    }

    [Fact]
    public void Tab_PastTheLastEditableCell_LeavesTheGrid()
    {
        var g = Grid();
        g.Select(29, 4);

        Assert.False(g.Key(KumikoKey.Tab).Handled);
        Assert.Equal(new CellPosition(29, 4), g.Selection.CurrentCell);
    }

    [Fact]
    public void ShiftTab_MovesToThePreviousEditableCell()
    {
        var g = Grid();
        g.Select(1, 0);

        Assert.True(g.Key(KumikoKey.Tab, InputModifiers.Shift).Handled);

        Assert.Equal(new CellPosition(0, 4), g.Selection.CurrentCell);
    }

    [Fact]
    public void ShiftTab_BeforeTheFirstEditableCell_LeavesTheGrid()
    {
        var g = Grid();
        g.Select(0, 0);

        Assert.False(g.Key(KumikoKey.Tab, InputModifiers.Shift).Handled);
        Assert.Equal(new CellPosition(0, 0), g.Selection.CurrentCell);
    }

    [Fact]
    public void Enter_WhileEditing_CommitsAndMovesDown()
    {
        var g = Grid();
        g.Select(2, 0);
        g.Type('Q');

        Assert.True(g.Key(KumikoKey.Enter).Handled);

        Assert.Equal("Q", g.Items[2].Name);
        Assert.False(g.Edit.IsEditing);
        Assert.Equal(new CellPosition(3, 0), g.Selection.CurrentCell);
    }

    [Fact]
    public void Enter_OnTheCurrentCell_OpensTheEditor()
    {
        var g = Grid();
        g.Select(2, 0);

        Assert.True(g.Key(KumikoKey.Enter).Handled);

        Assert.True(g.Edit.IsEditing);
        Assert.Equal(new CellPosition(2, 0), g.Edit.EditCell);
    }

    [Fact]
    public void Escape_WhileEditing_CancelsTheEdit()
    {
        var g = Grid();
        g.Select(2, 0);
        g.Type('Q');

        Assert.True(g.Key(KumikoKey.Escape).Handled);

        Assert.False(g.Edit.IsEditing);
        Assert.Equal("N02", g.Items[2].Name);
    }

    [Fact]
    public void Escape_OutsideAnEdit_ClearsTheSelection()
    {
        var g = Grid();
        g.Tap(10, g.RowY(2));

        Assert.True(g.Key(KumikoKey.Escape).Handled);

        Assert.Empty(g.Selection.SelectedRows);
        Assert.False(g.Selection.CurrentCell.IsValid);
    }

    [Fact]
    public void F2_OpensTheEditor_WithTheF2Trigger()
    {
        var g = Grid();
        g.Select(3, 2);

        Assert.True(g.Key(KumikoKey.F2).Handled);
        Assert.True(g.Edit.IsEditing);

        var off = Grid(x => x.Edit.EditTriggers = EditTrigger.DoubleTap);
        off.Select(3, 2);
        Assert.False(off.Key(KumikoKey.F2).Handled);
        Assert.False(off.Edit.IsEditing);
    }

    [Fact]
    public void Space_TogglesTheRowSelection()
    {
        var g = Grid();
        g.Select(4, 1);

        Assert.True(g.Key(KumikoKey.Space).Handled);

        Assert.True(g.Selection.IsRowSelected(4));
    }

    [Fact]
    public void Space_TogglesTheCurrentBooleanCell()
    {
        var g = BooleanColumnGrid.Create();
        g.Select(1, 0);

        Assert.True(g.Key(KumikoKey.Space).Handled);

        Assert.True(g.Items[1].On);
    }

    [Fact]
    public void HomeAndEnd_GoToTheFirstAndLastCellInTheRow()
    {
        var g = Grid();
        g.Select(6, 2);

        Assert.True(g.Key(KumikoKey.End).Handled);
        Assert.Equal(new CellPosition(6, 4), g.Selection.CurrentCell);
        Assert.True(g.Key(KumikoKey.Home).Handled);
        Assert.Equal(new CellPosition(6, 0), g.Selection.CurrentCell);
    }

    [Fact]
    public void CtrlHomeAndCtrlEnd_GoToTheFirstAndLastCellInTheGrid()
    {
        var g = Grid();
        g.Select(6, 2);

        Assert.True(g.Key(KumikoKey.End, InputModifiers.Control).Handled);
        Assert.Equal(new CellPosition(29, 4), g.Selection.CurrentCell);
        Assert.True(g.Key(KumikoKey.Home, InputModifiers.Control).Handled);
        Assert.Equal(new CellPosition(0, 0), g.Selection.CurrentCell);
    }

    [Fact]
    public void PageUpAndPageDown_MoveOnePageOfVisibleRows()
    {
        var g = Grid(); // 216 px below the header: 6 rows
        g.Select(0, 0);

        Assert.True(g.Key(KumikoKey.PageDown).Handled);
        Assert.Equal(6, g.Selection.CurrentCell.Row);
        Assert.True(g.Key(KumikoKey.PageUp).Handled);
        Assert.Equal(0, g.Selection.CurrentCell.Row);
    }

    [Fact]
    public void CtrlA_SelectsAllRows()
    {
        var g = Grid();
        g.Select(0, 0);

        Assert.True(g.Key(KumikoKey.A, InputModifiers.Control).Handled);

        Assert.Equal(30, g.Selection.SelectedRows.Count);
    }

    [Fact]
    public void Typing_StartsAnEditWithTheTypedCharacter()
    {
        var g = Grid();
        g.Select(1, 0);

        Assert.True(g.Type('k').Handled);
        g.Key(KumikoKey.Enter);

        Assert.Equal("k", g.Items[1].Name);
    }
}

/// <summary>A one-column grid of Boolean cells, wired to an input controller.</summary>
internal sealed class BooleanColumnGrid
{
    public sealed class Flag
    {
        public bool On { get; set; }
    }

    public required KumikoColumn Column { get; init; }
    public required Flag[] Items { get; init; }
    public required KumikoSource Source { get; init; }
    public required KumikoSelectionModel Selection { get; init; }
    public required ScrollState Scroll { get; init; }
    public required KumikoStyle Style { get; init; }
    public required EditSession Edit { get; init; }
    public required KumikoInputController Input { get; init; }

    /// <summary>Column x span 0..100; row <c>r</c> spans y 40 + 36 r .. 76 + 36 r; the check box is centered.</summary>
    public static BooleanColumnGrid Create(EditTrigger triggers = EditTrigger.Default, int rows = 3,
        Action<KumikoColumn>? configure = null)
    {
        var column = new KumikoColumn { Header = "On", PropertyName = "On", Width = 100, ColumnType = KumikoColumnType.Boolean };
        configure?.Invoke(column);
        var items = Enumerable.Range(0, rows).Select(_ => new Flag()).ToArray();
        var source = new KumikoSource();
        source.SetColumns(new[] { column });
        source.SetItems(items);
        var style = new KumikoStyle();
        var edit = new EditSession { Style = style, EditTriggers = triggers };
        return new BooleanColumnGrid
        {
            Column = column,
            Items = items,
            Source = source,
            Selection = new KumikoSelectionModel(source),
            Scroll = new ScrollState { ViewportWidth = 300, ViewportHeight = 200 },
            Style = style,
            Edit = edit,
            Input = new KumikoInputController { EditSession = edit },
        };
    }

    /// <summary>Viewport y of the middle of row <paramref name="row"/>.</summary>
    public float RowY(int row) => Style.HeaderHeight + row * Style.RowHeight + Style.RowHeight / 2;

    public void Select(int row, int column) => Selection.CurrentCell = new CellPosition(row, column);

    public KumikoKeyEventArgs Key(KumikoKey key, InputModifiers modifiers = InputModifiers.None)
    {
        var e = new KumikoKeyEventArgs { Key = key, Modifiers = modifiers };
        Input.HandleKey(e, Scroll, Selection, Style, Source);
        return e;
    }

    public void Tap(float x, float y, int clicks = 1)
    {
        foreach (var action in new[] { InputAction.Pressed, InputAction.Released })
            Input.HandlePointer(new KumikoPointerEventArgs { X = x, Y = y, Action = action, ClickCount = clicks },
                Scroll, Selection, Style, Source);
    }

    /// <summary>A tap followed by a second tap that the host reports as a double-tap.</summary>
    public void DoubleTap(float x, float y)
    {
        Tap(x, y, clicks: 1);
        Tap(x, y, clicks: 2);
    }
}

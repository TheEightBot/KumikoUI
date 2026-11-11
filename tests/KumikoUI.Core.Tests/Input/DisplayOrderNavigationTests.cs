using KumikoUI.Core.Input;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests.Input;

/// <summary>
/// Left, Right, Home, End and Tab follow the on-screen column order (left-frozen, scrolling, right-frozen,
/// hidden skipped), and arrow keys in Multiple mode move without toggling rows.
/// </summary>
public class DisplayOrderNavigationTests
{
    // Columns by index: 0 Name, 1 Age, 2 City, 3 Dept, 4 Score. Interleaved so that the display order is
    // Dept (3, left-frozen), City (2), Score (4), Name (0, right-frozen), with Age (1) hidden.
    private static InputGrid Interleaved(Action<InputGrid>? more = null) => new(rows: 10, configure: g =>
    {
        g.Dept.FreezeMode = ColumnFreezeMode.Left;
        g.Name.FreezeMode = ColumnFreezeMode.Right;
        g.Age.IsVisible = false;
        more?.Invoke(g);
    });

    private static InputGrid MultipleGrid(int rows = 10) =>
        new(rows, configure: x => x.Selection.Mode = KumikoSelectionMode.Multiple);

    private static int[] Columns(InputGrid g, params KumikoKey[] keys)
    {
        var visited = new List<int>();
        foreach (var key in keys)
        {
            g.Key(key);
            visited.Add(g.Selection.CurrentCell.Column);
        }
        return visited.ToArray();
    }

    [Fact]
    public void Right_WalksLeftFrozenScrollingRightFrozen_SkippingHidden()
    {
        var g = Interleaved();
        g.Select(2, 3);

        Assert.Equal(new[] { 2, 4, 0, 0 },
            Columns(g, KumikoKey.Right, KumikoKey.Right, KumikoKey.Right, KumikoKey.Right));
        Assert.Equal(2, g.Selection.CurrentCell.Row);
    }

    [Fact]
    public void Left_WalksBackThroughDisplayOrder_AndStopsAtTheFirstColumn()
    {
        var g = Interleaved();
        g.Select(2, 0);

        Assert.Equal(new[] { 4, 2, 3, 3 },
            Columns(g, KumikoKey.Left, KumikoKey.Left, KumikoKey.Left, KumikoKey.Left));
    }

    [Fact]
    public void HomeAndEnd_GoToTheFirstAndLastDisplayColumn()
    {
        var g = Interleaved();
        g.Select(4, 2);

        g.Key(KumikoKey.End);
        Assert.Equal(new CellPosition(4, 0), g.Selection.CurrentCell);
        g.Key(KumikoKey.Home);
        Assert.Equal(new CellPosition(4, 3), g.Selection.CurrentCell);
        g.Key(KumikoKey.End, InputModifiers.Control);
        Assert.Equal(new CellPosition(9, 0), g.Selection.CurrentCell);
        g.Key(KumikoKey.Home, InputModifiers.Control);
        Assert.Equal(new CellPosition(0, 3), g.Selection.CurrentCell);
    }

    [Fact]
    public void HiddenLastColumn_IsNotReachedByEnd()
    {
        var g = new InputGrid(rows: 5, configure: x => x.Score.IsVisible = false);
        g.Select(0, 0);

        g.Key(KumikoKey.End);

        Assert.Equal(3, g.Selection.CurrentCell.Column);
    }

    [Fact]
    public void Tab_FollowsDisplayOrder_AndWrapsToTheNextRow()
    {
        var g = Interleaved();
        g.Select(0, 3);

        var visited = new List<CellPosition>();
        for (int i = 0; i < 5; i++)
        {
            Assert.True(g.Key(KumikoKey.Tab).Handled);
            visited.Add(g.Selection.CurrentCell);
            g.Key(KumikoKey.Escape); // close the editor Tab opened
        }

        Assert.Equal(new[]
        {
            new CellPosition(0, 2), new CellPosition(0, 4), new CellPosition(0, 0),
            new CellPosition(1, 3), new CellPosition(1, 2),
        }, visited);
    }

    [Fact]
    public void ShiftTab_WalksBackwardsThroughDisplayOrder()
    {
        var g = Interleaved();
        g.Select(1, 3);

        g.Key(KumikoKey.Tab, InputModifiers.Shift);

        Assert.Equal(new CellPosition(0, 0), g.Selection.CurrentCell);
    }

    [Fact]
    public void Tab_StillSkipsColumnsThatOptOutOrAreReadOnly()
    {
        var g = Interleaved(x =>
        {
            x.City.AllowTabStop = false;
            x.Score.IsReadOnly = true;
        });
        g.Select(0, 3);

        g.Key(KumikoKey.Tab);

        Assert.Equal(new CellPosition(0, 0), g.Selection.CurrentCell); // Name: City and Score are not stops
    }

    [Fact]
    public void Tab_FromAHiddenColumn_MovesToTheFirstStopWithAHigherColumnIndex()
    {
        var g = Interleaved();
        g.Select(0, 1); // Age is hidden

        g.Key(KumikoKey.Tab);

        Assert.Equal(new CellPosition(0, 3), g.Selection.CurrentCell); // Dept: first in display order with index > 1
    }

    // ── Multiple mode ──

    [Fact]
    public void Multiple_ArrowKeys_MoveWithoutTogglingRows()
    {
        var g = MultipleGrid();
        g.Tap(10, g.RowY(2)); // toggles row 2 on
        Assert.Equal(new[] { 2 }, g.Selection.SelectedRows);

        g.Key(KumikoKey.Down);
        g.Key(KumikoKey.Down);
        g.Key(KumikoKey.Right);
        g.Key(KumikoKey.Up);

        Assert.Equal(new CellPosition(3, 1), g.Selection.CurrentCell);
        Assert.Equal(new[] { 2 }, g.Selection.SelectedRows);
    }

    [Fact]
    public void Multiple_ShiftArrowsPageKeysAndHomeEnd_AlsoOnlyMove()
    {
        var g = MultipleGrid(rows: 30);
        g.Select(5, 2);

        g.Key(KumikoKey.Down, InputModifiers.Shift);
        g.Key(KumikoKey.PageDown);
        g.Key(KumikoKey.End, InputModifiers.Control);
        g.Key(KumikoKey.Home, InputModifiers.Control);

        Assert.Empty(g.Selection.SelectedRows);
        Assert.Equal(0, g.Selection.CurrentCell.Row);
    }

    [Fact]
    public void Multiple_Space_TogglesTheCurrentRow()
    {
        var g = MultipleGrid();
        g.Select(1, 0);

        g.Key(KumikoKey.Space);
        Assert.Equal(new[] { 1 }, g.Selection.SelectedRows);

        g.Key(KumikoKey.Down);
        g.Key(KumikoKey.Space);
        Assert.Equal(new[] { 1, 2 }, g.Selection.SelectedRows.OrderBy(r => r));

        g.Key(KumikoKey.Space);
        Assert.Equal(new[] { 1 }, g.Selection.SelectedRows);
    }

    [Fact]
    public void Extended_ArrowKeys_StillSelectTheRow()
    {
        var g = new InputGrid(rows: 10);
        g.Select(1, 0);

        g.Key(KumikoKey.Down);

        Assert.Equal(new[] { 2 }, g.Selection.SelectedRows);
    }
}

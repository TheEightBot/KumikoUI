using System.Collections.ObjectModel;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests;

/// <summary>
/// Selection behavior around non-data rows (group headers and group summaries), each selection mode,
/// keyboard navigation with and without Shift, the current cell's bounds, and which changes raise
/// SelectionChanged.
/// </summary>
public class KumikoSelectionBehaviorTests
{
    private sealed class Fixture
    {
        public ObservableCollection<Person> People { get; } = new()
        {
            new Person { Name = "Alice", Age = 30, Department = "Engineering" },
            new Person { Name = "Bob", Age = 25, Department = "Marketing" },
            new Person { Name = "Charlie", Age = 35, Department = "Engineering" },
            new Person { Name = "Diana", Age = 28, Department = "Marketing" },
            new Person { Name = "Eve", Age = 40, Department = "Engineering" },
        };

        public KumikoSource Source { get; } = new();
        public KumikoSelectionModel Selection { get; }
        public List<KumikoSelectionChangedEventArgs> Changes { get; } = new();

        public Fixture(KumikoSelectionMode mode = KumikoSelectionMode.Extended)
        {
            Source.SetColumns(new[]
            {
                new KumikoColumn { PropertyName = "Name" },
                new KumikoColumn { PropertyName = "Age" },
                new KumikoColumn { PropertyName = "Department" },
            });
            Source.SetItems(People);
            Selection = new KumikoSelectionModel(Source) { Mode = mode };
            Selection.SelectionChanged += (_, e) => Changes.Add(e);
        }

        public void Group()
        {
            Source.AddGroupDescription(new GroupDescription("Department"));
            Source.AddGroupSummaryRow(new GroupSummaryRow("S") { Columns = { new SummaryColumnDescription("Age", SummaryType.Sum) } });
            // Rows: 0 Engineering, 1 Alice, 2 Charlie, 3 Eve, 4 summary, 5 Marketing, 6 Bob, 7 Diana, 8 summary
        }

        public string[] Selected => Selection.SelectedItems.Cast<Person>().Select(p => p.Name).OrderBy(n => n).ToArray();
    }

    // ── Group headers and summary rows ────────────────────────────

    [Fact]
    public void GroupRows_TakeTheCurrentCell_ButNeverTheSelection()
    {
        var f = new Fixture();
        f.Group();

        foreach (int row in new[] { 0, 4 })
        {
            f.Selection.HandleRowSelection(row, false, false);
            Assert.Equal(row, f.Selection.CurrentCell.Row);
            Assert.Empty(f.Selection.SelectedItems);
            Assert.Null(f.Selection.CurrentItem);
        }
        Assert.Empty(f.Changes);
    }

    [Fact]
    public void ClickingAGroupRow_InSingleMode_KeepsTheEarlierSelection()
    {
        var f = new Fixture(KumikoSelectionMode.Single);
        f.Group();
        f.Selection.HandleRowSelection(1, false, false); // Alice

        f.Selection.HandleRowSelection(0, false, false); // a group header

        Assert.Equal(new[] { "Alice" }, f.Selected);
    }

    [Fact]
    public void SelectAll_SkipsGroupHeadersAndSummaryRows()
    {
        var f = new Fixture();
        f.Group();

        f.Selection.SelectAll();

        Assert.Equal(5, f.Selection.SelectedItems.Count);
        Assert.Equal(new[] { 1, 2, 3, 6, 7 }, f.Selection.SelectedRows.OrderBy(r => r));
    }

    [Fact]
    public void ShiftRange_AcrossGroupsAndSummaryRows_SelectsOnlyDataRows()
    {
        var f = new Fixture();
        f.Group();
        f.Selection.HandleRowSelection(2, false, false); // Charlie

        f.Selection.HandleRowSelection(6, true, false); // Bob, across a summary row and a header

        Assert.Equal(new[] { "Bob", "Charlie", "Eve" }, f.Selected);
    }

    [Fact]
    public void SelectRange_IncludesNoGroupRows_AndAcceptsEitherOrder()
    {
        var f = new Fixture();
        f.Group();

        f.Selection.SelectRange(7, 3);

        Assert.Equal(new[] { "Bob", "Diana", "Eve" }, f.Selected);
    }

    [Fact]
    public void Select_OfAnItemInACollapsedGroup_IsIgnoredBecauseItHasNoRow()
    {
        var f = new Fixture();
        f.Group();
        f.Source.ToggleGroupExpansion(0); // collapse Engineering

        f.Selection.Select(f.People[0]);

        Assert.Empty(f.Selection.SelectedItems);
        Assert.Empty(f.Changes);
    }

    [Fact]
    public void CollapsingAGroup_DropsItsSelectedItems_AndExpandingDoesNotBringThemBack()
    {
        var f = new Fixture();
        f.Group();
        f.Selection.SelectAll();
        f.Changes.Clear();

        f.Source.ToggleGroupExpansion(0); // collapse Engineering

        Assert.Equal(new[] { "Bob", "Diana" }, f.Selected);
        Assert.Equal(new[] { "Alice", "Charlie", "Eve" }, f.Changes.Single().RemovedItems.Cast<Person>().Select(p => p.Name).OrderBy(n => n));

        f.Source.ToggleGroupExpansion(0);
        Assert.Equal(new[] { "Bob", "Diana" }, f.Selected);
    }

    [Fact]
    public void ArrowDown_ThroughAGroupHeader_MovesTheCurrentCellOntoItWithoutSelecting()
    {
        var f = new Fixture(KumikoSelectionMode.Single);
        f.Group();
        f.Selection.HandleRowSelection(3, false, false); // Eve, the last Engineering row

        f.Selection.NavigateDown(f.Source.RowCount, false); // the summary row
        Assert.Equal(4, f.Selection.CurrentCell.Row);
        f.Selection.NavigateDown(f.Source.RowCount, false); // the Marketing header
        Assert.Equal(5, f.Selection.CurrentCell.Row);
        Assert.Equal(new[] { "Eve" }, f.Selected);

        f.Selection.NavigateDown(f.Source.RowCount, false); // Bob
        Assert.Equal(new[] { "Bob" }, f.Selected);
    }

    // ── Modes ─────────────────────────────────────────────────────

    [Fact]
    public void NoneMode_MovesTheCurrentCell_ButSelectsNothingByAnyRoute()
    {
        var f = new Fixture(KumikoSelectionMode.None);

        f.Selection.HandleRowSelection(1, false, false);
        f.Selection.HandleRowSelection(3, true, false);
        f.Selection.Select(f.People[0]);
        f.Selection.SelectRange(0, 4);
        f.Selection.SelectAll();
        f.Selection.NavigateDown(5, true);

        Assert.Empty(f.Selection.SelectedItems);
        Assert.Empty(f.Changes);
        Assert.Equal(4, f.Selection.CurrentCell.Row);
    }

    [Fact]
    public void SingleMode_IgnoresShiftAndCtrl()
    {
        var f = new Fixture(KumikoSelectionMode.Single);

        f.Selection.HandleRowSelection(1, false, false);
        f.Selection.HandleRowSelection(3, true, false);
        Assert.Equal(new[] { "Diana" }, f.Selected);

        f.Selection.HandleRowSelection(0, false, true);
        Assert.Equal(new[] { "Alice" }, f.Selected);
    }

    [Fact]
    public void SingleMode_ClickingTheSelectedRowAgain_KeepsItSelected_AndRaisesNothing()
    {
        var f = new Fixture(KumikoSelectionMode.Single);
        f.Selection.HandleRowSelection(1, false, false);
        f.Changes.Clear();

        f.Selection.HandleRowSelection(1, false, false);

        Assert.Equal(new[] { "Bob" }, f.Selected);
        Assert.Empty(f.Changes);
    }

    [Fact]
    public void SingleMode_SelectRange_SelectsOnlyTheEndRow()
    {
        var f = new Fixture(KumikoSelectionMode.Single);

        f.Selection.SelectRange(0, 3);

        Assert.Equal(new[] { "Diana" }, f.Selected);
    }

    [Fact]
    public void MultipleMode_ClickToggles_AndShiftIsIgnored()
    {
        var f = new Fixture(KumikoSelectionMode.Multiple);

        f.Selection.HandleRowSelection(0, false, false);
        f.Selection.HandleRowSelection(2, true, false); // shift does not extend in Multiple mode
        Assert.Equal(new[] { "Alice", "Charlie" }, f.Selected);

        f.Selection.HandleRowSelection(0, false, false);
        Assert.Equal(new[] { "Charlie" }, f.Selected);
    }

    [Fact]
    public void ExtendedMode_ShiftWithoutAnAnchor_SelectsJustTheClickedRow()
    {
        var f = new Fixture();

        f.Selection.HandleRowSelection(3, true, false);

        Assert.Equal(new[] { "Diana" }, f.Selected);
    }

    [Fact]
    public void ExtendedMode_CtrlClick_TogglesWithoutDisturbingTheRest_AndShiftRangeReplacesFromTheAnchor()
    {
        var f = new Fixture();
        f.Selection.HandleRowSelection(0, false, false);
        f.Selection.HandleRowSelection(2, false, true);
        f.Selection.HandleRowSelection(4, false, true);
        Assert.Equal(new[] { "Alice", "Charlie", "Eve" }, f.Selected);

        f.Selection.HandleRowSelection(3, true, false); // anchor is Eve (the last ctrl-click)
        Assert.Equal(new[] { "Diana", "Eve" }, f.Selected);
    }

    [Fact]
    public void ExtendedMode_ShiftRange_KeepsTheAnchor_SoItCanShrinkAndGrow()
    {
        var f = new Fixture();
        f.Selection.HandleRowSelection(1, false, false);

        f.Selection.HandleRowSelection(4, true, false);
        Assert.Equal(new[] { "Bob", "Charlie", "Diana", "Eve" }, f.Selected);

        f.Selection.HandleRowSelection(2, true, false);
        Assert.Equal(new[] { "Bob", "Charlie" }, f.Selected);

        f.Selection.HandleRowSelection(0, true, false);
        Assert.Equal(new[] { "Alice", "Bob" }, f.Selected);
    }

    // ── Keyboard navigation ───────────────────────────────────────

    [Fact]
    public void ArrowDown_WithShift_ExtendsTheSelection_AndWithoutShiftMovesIt()
    {
        var f = new Fixture();
        f.Selection.HandleRowSelection(1, false, false);

        f.Selection.NavigateDown(5, true);
        f.Selection.NavigateDown(5, true);
        Assert.Equal(new[] { "Bob", "Charlie", "Diana" }, f.Selected);

        f.Selection.NavigateDown(5, false);
        Assert.Equal(new[] { "Eve" }, f.Selected);
    }

    [Fact]
    public void ArrowUp_WithShift_ShrinksTheRangeBackTowardsTheAnchor()
    {
        var f = new Fixture();
        f.Selection.HandleRowSelection(1, false, false);
        f.Selection.NavigateDown(5, true);
        f.Selection.NavigateDown(5, true);

        f.Selection.NavigateUp(5, true);

        Assert.Equal(new[] { "Bob", "Charlie" }, f.Selected);
    }

    [Fact]
    public void NavigateWithNoCurrentCell_ArrowsStartAtTheFirstCell_OthersDoNothing()
    {
        var f = new Fixture();
        f.Selection.NavigateLeft(3, false);
        f.Selection.NavigateRight(3, false);
        f.Selection.NavigateHome(true, 5, false);
        f.Selection.NavigateEnd(true, 5, 3, false);
        f.Selection.NavigatePageDown(2, 5, false);
        f.Selection.NavigatePageUp(2, false, 5);
        Assert.False(f.Selection.CurrentCell.IsValid);

        f.Selection.NavigateDown(5, false);

        Assert.Equal(new CellPosition(0, 0), f.Selection.CurrentCell);
        Assert.Empty(f.Selection.SelectedItems); // landing on the first cell selects nothing
    }

    [Fact]
    public void NavigateLeftAndRight_MoveOnlyTheColumn_AndNeverChangeTheSelection()
    {
        var f = new Fixture();
        f.Selection.HandleRowSelection(2, false, false);
        f.Changes.Clear();

        f.Selection.NavigateRight(3, true);
        f.Selection.NavigateRight(3, true);
        f.Selection.NavigateRight(3, true);

        Assert.Equal(new CellPosition(2, 2), f.Selection.CurrentCell);
        Assert.Equal(new[] { "Charlie" }, f.Selected);
        Assert.Empty(f.Changes);
    }

    [Fact]
    public void NavigateEnd_WithCtrlAndShift_SelectsFromTheCurrentRowToTheLast()
    {
        var f = new Fixture();
        f.Selection.HandleRowSelection(3, false, false);

        f.Selection.NavigateEnd(true, 5, 3, true);

        Assert.Equal(new[] { "Diana", "Eve" }, f.Selected);
        Assert.Equal(new CellPosition(4, 2), f.Selection.CurrentCell);
    }

    [Fact]
    public void NavigateHome_WithCtrlAndShift_SelectsFromTheCurrentRowToTheFirst()
    {
        var f = new Fixture();
        f.Selection.HandleRowSelection(2, false, false);

        f.Selection.NavigateHome(true, 5, true);

        Assert.Equal(new[] { "Alice", "Bob", "Charlie" }, f.Selected);
    }

    [Fact]
    public void Navigate_OverAnEmptySource_KeepsTheCellInvalid_AndDoesNotThrow()
    {
        var source = new KumikoSource();
        source.SetColumns(new[] { new KumikoColumn { PropertyName = "Name" } });
        source.SetItems(new List<Person>());
        var selection = new KumikoSelectionModel(source);

        selection.NavigateDown(0, false);
        selection.NavigateUp(0, false);
        selection.SelectAll();
        selection.HandleRowSelection(0, false, false);

        Assert.Empty(selection.SelectedItems);
        Assert.False(selection.CurrentCell.IsValid);
    }

    // ── Current cell bounds ───────────────────────────────────────

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(5, 0)]
    public void SetCurrentCell_WithARowOutOfRange_ClearsTheCurrentCell(int row, int column)
    {
        var f = new Fixture();
        f.Selection.SetCurrentCell(1, 1);

        f.Selection.SetCurrentCell(row, column);

        Assert.False(f.Selection.CurrentCell.IsValid);
        Assert.Null(f.Selection.CurrentItem);
    }

    [Fact]
    public void SetCurrentCell_DoesNotChangeTheSelection_OrRaiseAnEvent()
    {
        var f = new Fixture();
        f.Selection.HandleRowSelection(0, false, false);
        f.Changes.Clear();

        f.Selection.SetCurrentCell(3, 2);

        Assert.Equal(new[] { "Alice" }, f.Selected);
        Assert.Equal(f.People[3], f.Selection.CurrentItem);
        Assert.Empty(f.Changes);
    }

    [Fact]
    public void CurrentCell_Setter_MatchesSetCurrentCell()
    {
        var f = new Fixture();

        f.Selection.CurrentCell = new CellPosition(2, 1);

        Assert.Equal(new CellPosition(2, 1), f.Selection.CurrentCell);
        Assert.Same(f.People[2], f.Selection.CurrentItem);
    }

    [Fact]
    public void HandleRowSelection_OutOfRange_ChangesNothing()
    {
        var f = new Fixture();
        f.Selection.HandleRowSelection(1, false, false);
        f.Changes.Clear();

        f.Selection.HandleRowSelection(-1, false, false);
        f.Selection.HandleRowSelection(99, false, false);

        Assert.Equal(new[] { "Bob" }, f.Selected);
        Assert.Equal(1, f.Selection.CurrentCell.Row);
        Assert.Empty(f.Changes);
    }

    [Fact]
    public void ClickingARow_KeepsTheCurrentColumn()
    {
        var f = new Fixture();
        f.Selection.SetCurrentCell(0, 2);

        f.Selection.HandleRowSelection(3, false, false);

        Assert.Equal(new CellPosition(3, 2), f.Selection.CurrentCell);
    }

    [Fact]
    public void ClickingARow_WithNoCurrentCell_StartsAtTheFirstColumn()
    {
        var f = new Fixture();

        f.Selection.HandleRowSelection(3, false, false);

        Assert.Equal(new CellPosition(3, 0), f.Selection.CurrentCell);
    }

    // ── Clear, Select, Deselect ───────────────────────────────────

    [Fact]
    public void Clear_ForgetsTheAnchor_SoShiftStartsAFreshRange()
    {
        var f = new Fixture();
        f.Selection.HandleRowSelection(0, false, false);
        f.Selection.Clear();

        f.Selection.HandleRowSelection(3, true, false);

        Assert.Equal(new[] { "Diana" }, f.Selected);
    }

    [Fact]
    public void Clear_StopsEditing_AndInvalidatesTheCurrentCell()
    {
        var f = new Fixture();
        f.Selection.HandleRowSelection(2, false, false);
        f.Selection.IsEditing = true;

        f.Selection.Clear();

        Assert.False(f.Selection.IsEditing);
        Assert.False(f.Selection.CurrentCell.IsValid);
    }

    [Fact]
    public void Select_OfAnAlreadySelectedItem_RaisesNothing()
    {
        var f = new Fixture();
        f.Selection.Select(f.People[0]);
        f.Changes.Clear();

        f.Selection.Select(f.People[0]);

        Assert.Empty(f.Changes);
    }

    [Fact]
    public void Deselect_OfAnItemThatIsNotSelected_RaisesNothing()
    {
        var f = new Fixture();
        f.Selection.Select(f.People[0]);
        f.Changes.Clear();

        f.Selection.Deselect(f.People[1]);
        f.Selection.Deselect(new Person { Name = "Stranger" });

        Assert.Empty(f.Changes);
        Assert.Equal(new[] { "Alice" }, f.Selected);
    }

    [Fact]
    public void SelectAndDeselect_RejectNull()
    {
        var f = new Fixture();

        Assert.Throws<ArgumentNullException>(() => f.Selection.Select(null!));
        Assert.Throws<ArgumentNullException>(() => f.Selection.Deselect(null!));
        Assert.False(f.Selection.IsSelected(null!));
    }

    [Fact]
    public void Select_OfAnItemFilteredOut_OrNotInTheSource_IsIgnored()
    {
        var f = new Fixture();
        f.Source.SetFilter(p => ((Person)p).Age > 30);

        f.Selection.Select(f.People[0]); // Alice, 30: filtered out
        f.Selection.Select(new Person { Name = "Stranger" });

        Assert.Empty(f.Selection.SelectedItems);
    }

    [Fact]
    public void ChangingTheMode_LeavesTheCurrentSelectionAlone()
    {
        var f = new Fixture();
        f.Selection.SelectAll();

        f.Selection.Mode = KumikoSelectionMode.None;

        Assert.Equal(5, f.Selection.SelectedItems.Count);
    }

    // ── Events ────────────────────────────────────────────────────

    [Fact]
    public void SelectionChanged_ReportsOnlyTheDelta_WithTheRowsAndTheCurrentCellAfterward()
    {
        var f = new Fixture();
        f.Selection.HandleRowSelection(0, false, false);
        f.Changes.Clear();

        f.Selection.HandleRowSelection(2, true, false); // Alice..Charlie

        var change = f.Changes.Single();
        Assert.Equal(new[] { "Bob", "Charlie" }, change.AddedItems.Cast<Person>().Select(p => p.Name));
        Assert.Empty(change.RemovedItems);
        Assert.Equal(new[] { 0, 1, 2 }, change.SelectedRows);
        Assert.Equal(2, change.CurrentCell.Row);
    }

    [Fact]
    public void SelectionChanged_IsNotRaisedWhenOnlyTheCurrentCellMoves()
    {
        var f = new Fixture(KumikoSelectionMode.Multiple);
        f.Selection.HandleRowSelection(0, false, false);
        f.Changes.Clear();

        f.Selection.NavigateRight(3, false);
        f.Selection.SetCurrentCell(0, 0);

        Assert.Empty(f.Changes);
    }

    [Fact]
    public void SelectedRows_AreVisibleRowIndices_InsteadOfSourceIndices()
    {
        var f = new Fixture();
        f.Source.SetSort(f.Source.Columns[1], KumikoSortDirection.Ascending); // Bob 25, Diana 28, Alice 30, Charlie 35, Eve 40

        f.Selection.Select(f.People[0]); // Alice

        Assert.Equal(new[] { 2 }, f.Selection.SelectedRows);
        Assert.True(f.Selection.IsRowSelected(2));
        Assert.False(f.Selection.IsRowSelected(0));
    }

    [Fact]
    public void SelectedItems_AreComparedByReference_NotByEquality()
    {
        var twin1 = new Person { Name = "Twin", Age = 1, Department = "X" };
        var twin2 = new Person { Name = "Twin", Age = 1, Department = "X" };
        var source = new KumikoSource();
        source.SetColumns(new[] { new KumikoColumn { PropertyName = "Name" } });
        source.SetItems(new List<Person> { twin1, twin2 });
        var selection = new KumikoSelectionModel(source);

        selection.Select(twin2);

        Assert.True(selection.IsSelected(twin2));
        Assert.False(selection.IsSelected(twin1));
        Assert.Equal(new[] { 1 }, selection.SelectedRows);
    }
}

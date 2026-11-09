using System.Collections.ObjectModel;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests;

/// <summary>
/// Item-based selection (#30): the selection and current cell follow their items through view changes,
/// non-data rows are never selected, the collections are read-only, and SelectionChanged reports deltas.
/// </summary>
public class ItemSelectionTests
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
        public KumikoColumn Name { get; } = new() { PropertyName = "Name" };
        public KumikoColumn Age { get; } = new() { PropertyName = "Age" };
        public KumikoColumn Department { get; } = new() { PropertyName = "Department" };
        public KumikoSelectionModel Selection { get; }
        public List<KumikoSelectionChangedEventArgs> Changes { get; } = new();

        public Fixture()
        {
            Source.SetColumns(new[] { Name, Age, Department });
            Source.SetItems(People);
            Selection = new KumikoSelectionModel(Source);
            Selection.SelectionChanged += (_, e) => Changes.Add(e);
        }

        public Person P(string name) => People.Single(p => p.Name == name);

        public IEnumerable<string> SelectedNames => Selection.SelectedItems.Cast<Person>().Select(p => p.Name).OrderBy(n => n);

        public IEnumerable<string> NamesAtSelectedRows =>
            Selection.SelectedRows.OrderBy(r => r).Select(r => ((Person)Source.GetItem(r)).Name);
    }

    // ── The same items stay selected ──────────────────────────────

    [Fact]
    public void SelectThenSort_KeepsTheSameItems_AtTheirNewRows()
    {
        var f = new Fixture();
        f.Selection.HandleRowSelection(0, false, false); // Alice
        f.Selection.HandleRowSelection(1, false, true);  // + Bob
        f.Changes.Clear();

        f.Source.SetSort(f.Age, KumikoSortDirection.Descending); // Eve, Charlie, Alice, Diana, Bob

        Assert.Equal(new[] { "Alice", "Bob" }, f.SelectedNames);
        Assert.Equal(new[] { 2, 4 }, f.Selection.SelectedRows.OrderBy(r => r));
        Assert.Empty(f.Changes); // identity kept: no selection change
    }

    [Fact]
    public void SelectThenFilter_DropsHiddenItems_AndReportsThemRemoved()
    {
        var f = new Fixture();
        f.Selection.SelectAll();
        f.Changes.Clear();

        f.Source.SetFilter(o => ((Person)o).Department == "Engineering");

        Assert.Equal(new[] { "Alice", "Charlie", "Eve" }, f.SelectedNames);
        Assert.Equal(new[] { "Alice", "Charlie", "Eve" }, f.NamesAtSelectedRows);
        var change = Assert.Single(f.Changes);
        Assert.Empty(change.AddedItems);
        Assert.Equal(new[] { "Bob", "Diana" }, change.RemovedItems.Cast<Person>().Select(p => p.Name).OrderBy(n => n));
    }

    [Fact]
    public void SelectThenRemoveFromCollection_TheRestStaySelected()
    {
        var f = new Fixture();
        f.Selection.HandleRowSelection(1, false, false); // Bob
        f.Selection.HandleRowSelection(3, false, true);  // + Diana

        f.People.RemoveAt(0); // Alice: rows shift up
        Assert.Equal(new[] { "Bob", "Diana" }, f.NamesAtSelectedRows);

        f.People.Remove(f.P("Bob"));
        Assert.Equal(new[] { "Diana" }, f.SelectedNames);
        Assert.Equal(new[] { "Diana" }, f.NamesAtSelectedRows);
    }

    [Fact]
    public void ReplacingTheItems_ClearsTheSelection()
    {
        var f = new Fixture();
        f.Selection.SelectAll();

        f.Source.SetItems(new List<Person> { new() { Name = "New" } });

        Assert.Empty(f.Selection.SelectedItems);
        Assert.Empty(f.Selection.SelectedRows);
    }

    [Fact]
    public void CurrentCell_FollowsItsItemAndColumn()
    {
        var f = new Fixture();
        f.Selection.CurrentCell = new CellPosition(0, 2); // Alice, Department
        Assert.Same(f.P("Alice"), f.Selection.CurrentItem);

        f.Source.SetSort(f.Age, KumikoSortDirection.Descending);
        Assert.Equal(new CellPosition(2, 2), f.Selection.CurrentCell);

        f.Source.ReorderColumn(2, 0); // Department moves first
        Assert.Equal(new CellPosition(2, 0), f.Selection.CurrentCell);

        f.Source.SetFilter(o => ((Person)o).Name != "Alice");
        Assert.False(f.Selection.CurrentCell.IsValid);
        Assert.Null(f.Selection.CurrentItem);
    }

    [Fact]
    public void ShiftRange_AnchorFollowsItsItem()
    {
        var f = new Fixture();
        f.Selection.HandleRowSelection(0, false, false); // anchor: Alice
        f.Source.SetSort(f.Name, KumikoSortDirection.Descending); // Eve, Diana, Charlie, Bob, Alice

        f.Selection.HandleRowSelection(2, true, false); // shift-click Charlie: Charlie..Alice

        Assert.Equal(new[] { "Alice", "Bob", "Charlie" }, f.SelectedNames);
    }

    // ── Group rows are never selected ─────────────────────────────

    [Fact]
    public void CtrlA_OnAGroupedGrid_SelectsOnlyDataRows()
    {
        var f = new Fixture();
        f.Source.AddGroupDescription(new GroupDescription("Department")); // Engineering + 3, Marketing + 2
        var input = new KumikoInputController();
        var scroll = new ScrollState { ViewportWidth = 400, ViewportHeight = 400 };

        input.HandleKey(new KumikoKeyEventArgs { Key = KumikoKey.A, Modifiers = InputModifiers.Control },
            scroll, f.Selection, new KumikoStyle(), f.Source);

        Assert.Equal(5, f.Selection.SelectedItems.Count);
        Assert.All(f.Selection.SelectedRows, row => Assert.False(f.Source.IsNonDataRow(row)));
        Assert.Equal(new[] { 1, 2, 3, 5, 6 }, f.Selection.SelectedRows.OrderBy(r => r));
    }

    [Fact]
    public void ShiftRangeAcrossGroups_SkipsHeaderRows()
    {
        var f = new Fixture();
        f.Source.AddGroupDescription(new GroupDescription("Department"));

        f.Selection.HandleRowSelection(2, false, false);
        f.Selection.HandleRowSelection(6, true, false);

        Assert.Equal(new[] { 2, 3, 5, 6 }, f.Selection.SelectedRows.OrderBy(r => r));
    }

    [Fact]
    public void ClickingAGroupHeader_MovesTheCurrentCell_ButSelectsNothing()
    {
        var f = new Fixture();
        f.Source.AddGroupDescription(new GroupDescription("Department"));
        f.Selection.HandleRowSelection(1, false, false); // Alice

        f.Selection.HandleRowSelection(0, false, false); // Engineering header

        Assert.Equal(0, f.Selection.CurrentCell.Row);
        Assert.Null(f.Selection.CurrentItem);
        Assert.Equal(new[] { "Alice" }, f.SelectedNames);

        f.Source.ToggleGroupExpansion(4); // collapse Marketing: the header keeps the current cell
        Assert.Equal(0, f.Selection.CurrentCell.Row);
    }

    // ── API ───────────────────────────────────────────────────────

    [Fact]
    public void SelectAndDeselect_ByItem_RaiseDeltas()
    {
        var f = new Fixture();

        f.Selection.Select(f.P("Charlie"));
        f.Selection.Select(f.P("Eve"));
        f.Selection.Select(f.P("Eve")); // already selected: no change
        f.Selection.Deselect(f.P("Charlie"));

        Assert.Equal(new[] { "Eve" }, f.SelectedNames);
        Assert.Equal(new[] { 4 }, f.Selection.SelectedRows);
        Assert.Equal(3, f.Changes.Count);
        Assert.Equal(new object[] { f.P("Charlie") }, f.Changes[0].AddedItems);
        Assert.Equal(new object[] { f.P("Charlie") }, f.Changes[2].RemovedItems);
        Assert.Equal(new[] { 4 }, f.Changes[2].SelectedRows);
    }

    [Fact]
    public void Select_InSingleMode_Replaces_AndIgnoresHiddenItems()
    {
        var f = new Fixture();
        f.Selection.Mode = KumikoSelectionMode.Single;
        f.Source.SetFilter(o => ((Person)o).Name != "Bob");

        f.Selection.Select(f.P("Alice"));
        f.Selection.Select(f.P("Eve"));
        f.Selection.Select(f.P("Bob")); // filtered out

        Assert.Equal(new[] { "Eve" }, f.SelectedNames);
    }

    [Fact]
    public void SelectRange_AddsDataRows_InEitherOrder()
    {
        var f = new Fixture();

        f.Selection.SelectRange(3, 1);
        f.Selection.SelectRange(4, 4);

        Assert.Equal(new[] { 1, 2, 3, 4 }, f.Selection.SelectedRows.OrderBy(r => r));
    }

    [Fact]
    public void Collections_AreReadOnly()
    {
        var f = new Fixture();
        f.Selection.SelectAll();

        Assert.IsNotAssignableFrom<HashSet<int>>(f.Selection.SelectedRows);
        Assert.IsNotAssignableFrom<HashSet<object>>(f.Selection.SelectedItems);
        Assert.Throws<NotSupportedException>(() => ((ISet<int>)f.Selection.SelectedRows).Add(9));
        Assert.Throws<NotSupportedException>(() => ((ICollection<object>)f.Selection.SelectedItems).Clear());
    }

    [Fact]
    public void Clear_RemovesEverything_AndReportsTheRemovedItems()
    {
        var f = new Fixture();
        f.Selection.SelectRange(0, 1);
        f.Selection.CurrentCell = new CellPosition(1, 0);
        f.Changes.Clear();

        f.Selection.Clear();

        Assert.Empty(f.Selection.SelectedItems);
        Assert.False(f.Selection.CurrentCell.IsValid);
        Assert.Equal(2, Assert.Single(f.Changes).RemovedItems.Count);
    }
}

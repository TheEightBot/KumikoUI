using System.Collections.ObjectModel;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests;

/// <summary>The MVVM selection surface (#55): primary item, index, synced list and current cell.</summary>
public class KumikoSelectionBinderTests
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
        public KumikoSelectionModel Selection { get; }
        public KumikoSelectionBinder Binder { get; }
        public int ChangedCount { get; set; }
        public List<KumikoSelectionChangedEventArgs> Events { get; } = new();

        public Fixture()
        {
            Source.SetColumns(new[] { Name, Age });
            Source.SetItems(People);
            Selection = new KumikoSelectionModel(Source);
            Binder = new KumikoSelectionBinder(Selection);
            Binder.Changed += (_, _) => ChangedCount++;
            Binder.SelectionChanged += (_, e) => Events.Add(e);
        }

        public Person P(string name) => People.Single(p => p.Name == name);

        public IEnumerable<string> SelectedNames =>
            Selection.SelectedItems.Cast<Person>().Select(p => p.Name).OrderBy(n => n);
    }

    [Fact]
    public void GridSelection_UpdatesItemIndexAndList()
    {
        var f = new Fixture();
        f.Selection.HandleRowSelection(2, false, false);

        Assert.Same(f.P("Charlie"), f.Binder.SelectedItem);
        Assert.Equal(2, f.Binder.SelectedIndex);
        Assert.Equal(new object[] { f.P("Charlie") }, f.Binder.SelectedItems.Cast<object>());
        Assert.Single(f.Events);
        Assert.True(f.ChangedCount >= 1);
    }

    [Fact]
    public void SelectedIndex_FollowsTheItemThroughSort()
    {
        var f = new Fixture();
        f.Binder.SetSelectedItem(f.P("Alice"));
        f.ChangedCount = 0;

        f.Source.SetSort(f.Age, KumikoSortDirection.Descending); // Eve, Charlie, Alice, ...

        Assert.Same(f.P("Alice"), f.Binder.SelectedItem);
        Assert.Equal(2, f.Binder.SelectedIndex);
        Assert.True(f.ChangedCount >= 1);
    }

    [Fact]
    public void FilteredOutItem_ClearsSelectionAndItemAndList()
    {
        var f = new Fixture();
        var bob = f.P("Bob");
        f.Binder.SetSelectedItem(bob);

        f.People.Remove(bob);

        Assert.Null(f.Binder.SelectedItem);
        Assert.Equal(-1, f.Binder.SelectedIndex);
        Assert.Empty(f.Binder.SelectedItems);
        Assert.Contains(bob, f.Events.Last().RemovedItems);
    }

    [Fact]
    public void SetSelectedItem_SelectsOnlyThatItem_AndMovesTheCurrentCell()
    {
        var f = new Fixture();
        f.Selection.HandleRowSelection(0, false, false);

        f.Binder.SetSelectedItem(f.P("Diana"));

        Assert.Equal(new[] { "Diana" }, f.SelectedNames);
        Assert.Equal(3, f.Binder.CurrentCell.Row);
        Assert.Equal(3, f.Binder.SelectedIndex);
        Assert.Equal(new object[] { f.P("Diana") }, f.Binder.SelectedItems.Cast<object>());
    }

    [Fact]
    public void SetSelectedItem_Null_AndOutOfRangeIndex_Clear()
    {
        var f = new Fixture();
        f.Binder.SetSelectedItem(f.P("Eve"));

        f.Binder.SetSelectedItem(null);
        Assert.Empty(f.Selection.SelectedItems);
        Assert.Null(f.Binder.SelectedItem);

        f.Binder.SetSelectedIndex(1);
        Assert.Same(f.P("Bob"), f.Binder.SelectedItem);
        f.Binder.SetSelectedIndex(99);
        Assert.Null(f.Binder.SelectedItem);
        Assert.Equal(-1, f.Binder.SelectedIndex);
    }

    [Fact]
    public void SetSelectedItem_InNoneMode_LeavesNothingSelected()
    {
        var f = new Fixture();
        f.Selection.Mode = KumikoSelectionMode.None;

        f.Binder.SetSelectedItem(f.P("Eve"));

        Assert.Empty(f.Selection.SelectedItems);
        Assert.Null(f.Binder.SelectedItem);
    }

    [Fact]
    public void ItemNotInTheView_IsIgnored()
    {
        var f = new Fixture();
        f.Binder.SetSelectedItem(new Person { Name = "Stranger" });

        Assert.Empty(f.Selection.SelectedItems);
        Assert.Null(f.Binder.SelectedItem);
    }

    [Fact]
    public void OwnedList_ChangedByTheViewModel_SelectsInTheGrid()
    {
        var f = new Fixture();
        var list = Assert.IsType<ObservableCollection<object>>(f.Binder.SelectedItems);

        list.Add(f.P("Alice"));
        list.Add(f.P("Eve"));
        Assert.Equal(new[] { "Alice", "Eve" }, f.SelectedNames);

        list.Remove(f.P("Alice"));
        Assert.Equal(new[] { "Eve" }, f.SelectedNames);
        Assert.Same(f.P("Eve"), f.Binder.SelectedItem);

        list.Clear();
        Assert.Empty(f.Selection.SelectedItems);
        Assert.Null(f.Binder.SelectedItem);
    }

    [Fact]
    public void AttachedList_IsAppliedThenKeptInSync_WithoutLooping()
    {
        var f = new Fixture();
        var vmList = new ObservableCollection<Person> { f.P("Bob"), f.P("Diana") };
        int notifications = 0;
        vmList.CollectionChanged += (_, _) => notifications++;

        f.Binder.AttachSelectedItems(vmList);
        Assert.Equal(new[] { "Bob", "Diana" }, f.SelectedNames);
        Assert.Equal(0, notifications); // applying the list does not write back to it

        f.Selection.Mode = KumikoSelectionMode.Multiple;
        f.Selection.HandleRowSelection(0, false, false); // toggles Alice on
        Assert.Contains(f.P("Alice"), vmList);
        Assert.Equal(1, notifications);

        f.Selection.HandleRowSelection(1, false, false); // toggles Bob off
        Assert.DoesNotContain(f.P("Bob"), vmList);
        Assert.Equal(2, notifications);

        f.Binder.AttachSelectedItems(null); // back to the owned collection, mirroring the grid
        Assert.Equal(f.Selection.SelectedItems.Count, f.Binder.SelectedItems.Count);
    }

    [Fact]
    public void AttachedList_InSingleMode_KeepsTheLastItem()
    {
        var f = new Fixture();
        f.Selection.Mode = KumikoSelectionMode.Single;

        f.Binder.AttachSelectedItems(new ObservableCollection<Person> { f.P("Alice"), f.P("Charlie") });

        Assert.Equal(new[] { "Charlie" }, f.SelectedNames);
    }

    [Fact]
    public void ListSet_IsOneSelectionChange()
    {
        var f = new Fixture();
        f.Binder.AttachSelectedItems(new List<Person> { f.P("Alice"), f.P("Bob"), f.P("Eve") });

        Assert.Single(f.Events);
        Assert.Equal(3, f.Events[0].AddedItems.Count);
    }

    [Fact]
    public void PrimaryItem_FallsBackToTheTopmostSelectedItem()
    {
        var f = new Fixture();
        f.Selection.Mode = KumikoSelectionMode.Multiple;
        f.Selection.HandleRowSelection(3, false, false);
        f.Selection.HandleRowSelection(1, false, false);
        Assert.Same(f.P("Bob"), f.Binder.SelectedItem);

        f.Selection.HandleRowSelection(1, false, false); // Bob off

        Assert.Same(f.P("Diana"), f.Binder.SelectedItem);
        Assert.Equal(3, f.Binder.SelectedIndex);
    }

    [Fact]
    public void CurrentCell_ChangesRaiseChanged_AndCanBeSet()
    {
        var f = new Fixture();
        f.Binder.CurrentCell = new CellPosition(2, 1);

        Assert.Equal(new CellPosition(2, 1), f.Selection.CurrentCell);
        Assert.Equal(1, f.ChangedCount);

        f.Selection.NavigateDown(5, false);
        Assert.Equal(new CellPosition(3, 1), f.Binder.CurrentCell);
        Assert.True(f.ChangedCount >= 2);
    }

    [Fact]
    public void SelectionModel_CurrentCellChanged_FollowsViewChanges()
    {
        var f = new Fixture();
        f.Selection.SetCurrentCell(0, 0); // Alice
        int raised = 0;
        f.Selection.CurrentCellChanged += (_, _) => raised++;

        f.Source.SetSort(f.Age, KumikoSortDirection.Descending);

        Assert.Equal(1, raised);
        Assert.Equal(2, f.Selection.CurrentCell.Row);
    }

    [Fact]
    public void SetSelectedItems_ReplacesInOneChange_AndReportsTheDelta()
    {
        var f = new Fixture();
        f.Selection.Select(f.P("Alice"));
        f.Selection.Select(f.P("Bob"));
        var changes = new List<KumikoSelectionChangedEventArgs>();
        f.Selection.SelectionChanged += (_, e) => changes.Add(e);

        f.Selection.SetSelectedItems(new object[] { f.P("Bob"), f.P("Eve") });

        var change = Assert.Single(changes);
        Assert.Equal(new object[] { f.P("Eve") }, change.AddedItems);
        Assert.Equal(new object[] { f.P("Alice") }, change.RemovedItems);
    }
}

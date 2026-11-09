using System.Collections.ObjectModel;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Tests;

/// <summary>
/// <see cref="KumikoSource.ViewChanged"/> (#20): one notification of the right kind per mutation, batching,
/// row mapping, the sort API, <see cref="ScrollState.ScrollChanged"/>, and the selection and edit session
/// following their rows.
/// </summary>
public class ViewChangedTests
{
    private static ObservableCollection<Person> People() => new()
    {
        new Person { Name = "Alice", Age = 30, Department = "Engineering" },
        new Person { Name = "Bob", Age = 25, Department = "Marketing" },
        new Person { Name = "Charlie", Age = 35, Department = "Engineering" },
        new Person { Name = "Diana", Age = 28, Department = "Marketing" },
        new Person { Name = "Eve", Age = 40, Department = "Engineering" },
    };

    private sealed class Fixture
    {
        public ObservableCollection<Person> People { get; } = ViewChangedTests.People();
        public KumikoSource Source { get; } = new();
        public KumikoColumn Name { get; } = new() { Header = "Name", PropertyName = "Name" };
        public KumikoColumn Age { get; } = new() { Header = "Age", PropertyName = "Age", ColumnType = KumikoColumnType.Numeric };
        public KumikoColumn Department { get; } = new() { Header = "Department", PropertyName = "Department" };
        public List<KumikoViewChangedEventArgs> Events { get; } = new();

        public Fixture()
        {
            Source.SetColumns(new[] { Name, Age, Department });
            Source.SetItems(People);
            Source.ViewChanged += (_, e) => Events.Add(e);
        }

        public IEnumerable<KumikoViewChangeKind> Kinds => Events.Select(e => e.Kind);

        public string NameAt(int row) => (string)Source.GetCellValue(row, Name)!;
    }

    // ── One notification per mutation ─────────────────────────────

    public static TheoryData<string, KumikoViewChangeKind?> Mutations => new()
    {
        { "SetItems", KumikoViewChangeKind.Rebuild },
        { "SetColumns", KumikoViewChangeKind.Rebuild },
        { "Collection.Add", KumikoViewChangeKind.Insert },
        { "Collection.Remove", KumikoViewChangeKind.Remove },
        { "Collection.Replace", KumikoViewChangeKind.Rebuild },
        { "Collection.Add while sorted", KumikoViewChangeKind.Rebuild },
        { "Item display property", KumikoViewChangeKind.Redraw },
        { "Item sorted property", KumikoViewChangeKind.Rebuild },
        { "ReorderColumn", KumikoViewChangeKind.Redraw },
        { "ReorderRow", KumikoViewChangeKind.Rebuild },
        { "FrozenRowCount", KumikoViewChangeKind.Redraw },
        { "FrozenRowCount unchanged", null },
        { "AddTableSummaryRow", KumikoViewChangeKind.Rebuild },
        { "ClearTableSummaryRows", KumikoViewChangeKind.Rebuild },
        { "AddGroupSummaryRow", KumikoViewChangeKind.Rebuild },
        { "CaptionSummary", KumikoViewChangeKind.Rebuild },
        { "AddGroupDescription", KumikoViewChangeKind.Rebuild },
        { "RemoveGroupDescription", KumikoViewChangeKind.Rebuild },
        { "ToggleGroupExpansion", KumikoViewChangeKind.Rebuild },
        { "CollapseAllGroups", KumikoViewChangeKind.Rebuild },
        { "SetFilter", KumikoViewChangeKind.Rebuild },
        { "SetColumnFilter", KumikoViewChangeKind.Rebuild },
        { "ClearAllFilters", KumikoViewChangeKind.Rebuild },
        { "ToggleSort", KumikoViewChangeKind.Rebuild },
        { "ToggleMultiSort", KumikoViewChangeKind.Rebuild },
        { "SetSort", KumikoViewChangeKind.Rebuild },
        { "SetSort unchanged", null },
        { "ClearSort", KumikoViewChangeKind.Rebuild },
        { "ClearSort with nothing sorted", null },
        { "Refresh", KumikoViewChangeKind.Rebuild },
        { "SetCellValue", KumikoViewChangeKind.Redraw },
        { "SetCellValue on the sorted column", KumikoViewChangeKind.Rebuild },
    };

    [Theory]
    [MemberData(nameof(Mutations))]
    public void EveryMutation_RaisesExactlyOneViewChanged_OfTheRightKind(string mutation, KumikoViewChangeKind? expected)
    {
        var f = new Fixture();
        // Arrange the state some mutations need, then forget the events it raised.
        switch (mutation)
        {
            case "Collection.Add while sorted":
            case "Item sorted property":
            case "SetSort unchanged":
            case "ClearSort":
            case "SetCellValue on the sorted column":
                f.Source.SetSort(f.Age, KumikoSortDirection.Ascending);
                break;
            case "RemoveGroupDescription":
            case "ToggleGroupExpansion":
            case "CollapseAllGroups":
                f.Source.AddGroupDescription(new GroupDescription("Department"));
                break;
            case "ClearTableSummaryRows":
                f.Source.AddTableSummaryRow(new TableSummaryRow("T") { Columns = { new SummaryColumnDescription("Age", SummaryType.Sum) } });
                break;
        }
        f.Events.Clear();

        switch (mutation)
        {
            case "SetItems": f.Source.SetItems(People()); break;
            case "SetColumns": f.Source.SetColumns(new[] { f.Name, f.Age }); break;
            case "Collection.Add": f.People.Add(new Person { Name = "Frank" }); break;
            case "Collection.Remove": f.People.RemoveAt(1); break;
            case "Collection.Replace": f.People[1] = new Person { Name = "Bobby" }; break;
            case "Collection.Add while sorted": f.People.Add(new Person { Name = "Frank", Age = 1 }); break;
            case "Item display property": f.People[0].Name = "Alicia"; break;
            case "Item sorted property": f.People[0].Age = 99; break;
            case "ReorderColumn": f.Source.ReorderColumn(0, 2); break;
            case "ReorderRow": f.Source.ReorderRow(0, 3); break;
            case "FrozenRowCount": f.Source.FrozenRowCount = 2; break;
            case "FrozenRowCount unchanged": f.Source.FrozenRowCount = 0; break;
            case "AddTableSummaryRow": f.Source.AddTableSummaryRow(new TableSummaryRow("T")); break;
            case "ClearTableSummaryRows": f.Source.ClearTableSummaryRows(); break;
            case "AddGroupSummaryRow": f.Source.AddGroupSummaryRow(new GroupSummaryRow("G")); break;
            case "CaptionSummary": f.Source.CaptionSummary = new CaptionSummaryDescription(); break;
            case "AddGroupDescription": f.Source.AddGroupDescription(new GroupDescription("Department")); break;
            case "RemoveGroupDescription": f.Source.RemoveGroupDescription("Department"); break;
            case "ToggleGroupExpansion": f.Source.ToggleGroupExpansion(0); break;
            case "CollapseAllGroups": f.Source.CollapseAllGroups(); break;
            case "SetFilter": f.Source.SetFilter(o => ((Person)o).Age > 26); break;
            case "SetColumnFilter":
                f.Source.SetColumnFilter(f.Department, new FilterDescription(f.Department) { SelectedValues = new() { "Marketing" } });
                break;
            case "ClearAllFilters": f.Source.ClearAllFilters(); break;
            case "ToggleSort": f.Source.ToggleSort(f.Name); break;
            case "ToggleMultiSort": f.Source.ToggleMultiSort(f.Name); break;
            case "SetSort": f.Source.SetSort(f.Name, KumikoSortDirection.Descending); break;
            case "SetSort unchanged": f.Source.SetSort(f.Age, KumikoSortDirection.Ascending); break;
            case "ClearSort": f.Source.ClearSort(); break;
            case "ClearSort with nothing sorted": f.Source.ClearSort(); break;
            case "Refresh": f.Source.Refresh(); break;
            case "SetCellValue": f.Source.SetCellValue(0, f.Name, "Alicia"); break;
            case "SetCellValue on the sorted column": f.Source.SetCellValue(0, f.Age, 99); break;
            default: throw new ArgumentOutOfRangeException(nameof(mutation));
        }

        Assert.Equal(expected is { } kind ? new[] { kind } : Array.Empty<KumikoViewChangeKind>(), f.Kinds);
    }

    [Fact]
    public void Insert_And_Remove_ReportTheVisibleRow()
    {
        var f = new Fixture();

        f.People.Insert(2, new Person { Name = "Zed" });
        f.People.RemoveAt(4);

        Assert.Equal((KumikoViewChangeKind.Insert, 2, 1, 5, 6), (f.Events[0].Kind, f.Events[0].ViewIndex, f.Events[0].Count, f.Events[0].OldRowCount, f.Events[0].NewRowCount));
        Assert.Equal((KumikoViewChangeKind.Remove, 4, 1, 6, 5), (f.Events[1].Kind, f.Events[1].ViewIndex, f.Events[1].Count, f.Events[1].OldRowCount, f.Events[1].NewRowCount));
    }

    // ── Batching ──────────────────────────────────────────────────

    [Fact]
    public void Batch_CollapsesEverythingIntoOneRebuild_AppliedAtTheEnd()
    {
        var f = new Fixture();

        f.Source.BeginUpdate();
        f.Source.SetSort(f.Age, KumikoSortDirection.Descending);
        f.Source.SetFilter(o => ((Person)o).Age >= 30);
        f.Source.BeginUpdate(); // nested
        f.Source.AddGroupDescription(new GroupDescription("Department"));
        f.Source.EndUpdate();
        f.People[1].Name = "Robert";
        Assert.Empty(f.Events);
        Assert.Equal(5, f.Source.RowCount); // the rebuild waits for the outermost EndUpdate
        f.Source.EndUpdate();

        var e = Assert.Single(f.Events);
        Assert.Equal(KumikoViewChangeKind.Rebuild, e.Kind);
        Assert.Equal(5, e.OldRowCount);
        Assert.Equal(f.Source.RowCount, e.NewRowCount);
        Assert.Equal(4, f.Source.RowCount); // Age >= 30 leaves Alice, Charlie, Eve: one Engineering header + 3 rows
    }

    [Fact]
    public void Batch_WithOnlyRedraws_RaisesOneRedraw_AndAnEmptyBatchRaisesNothing()
    {
        var f = new Fixture();

        f.Source.BeginUpdate();
        f.Source.SetCellValue(0, f.Name, "A");
        f.Source.SetCellValue(1, f.Name, "B");
        f.Source.ReorderColumn(0, 1);
        f.Source.EndUpdate();

        f.Source.BeginUpdate();
        f.Source.EndUpdate();

        Assert.Equal(new[] { KumikoViewChangeKind.Redraw }, f.Kinds);
    }

    [Fact]
    public void Batch_CollectionChangesUpdateTheViewAtOnce_ButNotifyOnceAtTheEnd()
    {
        var f = new Fixture();

        f.Source.BeginUpdate();
        f.People.RemoveAt(0);
        f.People.Add(new Person { Name = "Frank" });
        Assert.Equal(5, f.Source.RowCount);
        Assert.Equal("Frank", f.NameAt(4)); // rows stay readable mid-batch
        f.Source.EndUpdate();

        var e = Assert.Single(f.Events);
        Assert.Equal(KumikoViewChangeKind.Rebuild, e.Kind);
        Assert.Equal(-1, e.MapRow(0)); // Alice was removed
        Assert.Equal(0, e.MapRow(1));  // Bob moved up
    }

    [Fact]
    public void EndUpdate_WithoutBeginUpdate_Throws()
    {
        var source = new KumikoSource();
        Assert.Throws<InvalidOperationException>(source.EndUpdate);
    }

    // ── Row mapping ───────────────────────────────────────────────

    [Fact]
    public void MapRow_FollowsItemsThroughASort()
    {
        var f = new Fixture();

        f.Source.SetSort(f.Age, KumikoSortDirection.Descending); // Eve 40, Charlie 35, Alice 30, Diana 28, Bob 25

        var e = Assert.Single(f.Events);
        Assert.Equal(2, e.MapRow(0)); // Alice
        Assert.Equal(4, e.MapRow(1)); // Bob
        Assert.Equal(0, e.MapRow(4)); // Eve
        Assert.Equal(-1, e.MapRow(5));
        Assert.Equal(-1, e.MapRow(-1));
        Assert.Equal(1, e.GetViewRow(f.People[2])); // Charlie
    }

    [Fact]
    public void MapRow_ReturnsMinusOne_ForRowsFilteredOut()
    {
        var f = new Fixture();

        f.Source.SetFilter(o => ((Person)o).Department == "Marketing");

        var e = Assert.Single(f.Events);
        Assert.Equal(-1, e.MapRow(0));
        Assert.Equal(0, e.MapRow(1));
        Assert.Equal(1, e.MapRow(3));
        Assert.Equal(-1, e.GetViewRow(f.People[0]));
    }

    [Fact]
    public void MapRow_ShiftsRowsAroundInsertsAndRemovals()
    {
        var f = new Fixture();

        f.People.Insert(1, new Person { Name = "New" });
        f.People.RemoveAt(3);

        var insert = f.Events[0];
        Assert.Equal(0, insert.MapRow(0));
        Assert.Equal(2, insert.MapRow(1));
        var remove = f.Events[1];
        Assert.Equal(2, remove.MapRow(2));
        Assert.Equal(-1, remove.MapRow(3));
        Assert.Equal(3, remove.MapRow(4));
    }

    [Fact]
    public void MapRow_MatchesGroupHeadersByGroup()
    {
        var f = new Fixture();
        f.Source.AddGroupDescription(new GroupDescription("Department")); // Engineering (0) + 3 rows, Marketing (4) + 2
        f.Events.Clear();

        f.Source.ToggleGroupExpansion(0); // collapse Engineering

        var e = Assert.Single(f.Events);
        Assert.Equal(0, e.MapRow(0));  // Engineering header stays first
        Assert.Equal(-1, e.MapRow(1)); // its rows are hidden
        Assert.Equal(1, e.MapRow(4));  // Marketing header moved up
        Assert.Equal(2, e.MapRow(5));
    }

    [Fact]
    public void Redraw_MapsRowsToThemselves()
    {
        var f = new Fixture();

        f.People[2].Name = "Charles";

        var e = Assert.Single(f.Events);
        Assert.False(e.IsStructural);
        Assert.Equal(3, e.MapRow(3));
        Assert.Equal(2, e.GetViewRow(f.People[2]));
    }

    [Fact]
    public void NoListener_NoSnapshots()
    {
        // Without a ViewChanged handler the source does not capture row keys; this just must not throw.
        var source = new KumikoSource();
        source.SetItems(People());
        source.SetFilter(o => true);
        source.BeginUpdate();
        source.Refresh();
        source.EndUpdate();
        Assert.Equal(5, source.RowCount);
    }

    // ── Sort API ──────────────────────────────────────────────────

    [Fact]
    public void SetSort_WithoutOrder_MakesTheColumnTheOnlySortedOne()
    {
        var f = new Fixture();
        f.Source.SetSort(f.Department, KumikoSortDirection.Ascending, 1);
        f.Source.SetSort(f.Age, KumikoSortDirection.Descending, 2);

        f.Source.SetSort(f.Name, KumikoSortDirection.Ascending);

        Assert.Equal(new[] { f.Name }, f.Source.ActiveSortColumns);
        Assert.Equal((KumikoSortDirection.None, 0), (f.Age.SortDirection, f.Age.SortOrder));
        Assert.False(f.Source.IsMultiSortActive);
        Assert.Equal("Alice", f.NameAt(0));
    }

    [Fact]
    public void SetSort_WithOrder_InsertsIntoTheChain_AndNoneRemovesAndRenumbers()
    {
        var f = new Fixture();
        f.Source.SetSort(f.Department, KumikoSortDirection.Ascending, 1);
        f.Source.SetSort(f.Age, KumikoSortDirection.Descending, 2);

        f.Source.SetSort(f.Name, KumikoSortDirection.Ascending, 1); // becomes primary
        Assert.Equal(new[] { f.Name, f.Department, f.Age }, f.Source.ActiveSortColumns);
        Assert.Equal(new[] { 1, 2, 3 }, f.Source.ActiveSortColumns.Select(c => c.SortOrder));
        Assert.True(f.Source.IsMultiSortActive);

        f.Source.SetSort(f.Department, KumikoSortDirection.None);
        Assert.Equal(new[] { f.Name, f.Age }, f.Source.ActiveSortColumns);
        Assert.Equal(new[] { 1, 2 }, f.Source.ActiveSortColumns.Select(c => c.SortOrder));
    }

    [Fact]
    public void ToggleMultiSort_AppendsCyclesAndRemoves()
    {
        var f = new Fixture();

        f.Source.ToggleMultiSort(f.Department); // asc, order 1
        f.Source.ToggleMultiSort(f.Age);        // asc, order 2
        f.Source.ToggleMultiSort(f.Department); // desc, keeps order 1
        Assert.Equal(new[] { (f.Department, KumikoSortDirection.Descending, 1), (f.Age, KumikoSortDirection.Ascending, 2) },
            f.Source.ActiveSortColumns.Select(c => (c, c.SortDirection, c.SortOrder)));

        f.Source.ToggleMultiSort(f.Department); // none: Age becomes primary
        Assert.Equal(new[] { (f.Age, 1) }, f.Source.ActiveSortColumns.Select(c => (c, c.SortOrder)));
    }

    [Fact]
    public void SetSort_CanceledBySortColumnsChanging_ChangesNothing()
    {
        var f = new Fixture();
        f.Source.SortColumnsChanging += (_, e) => e.Cancel = true;

        f.Source.SetSort(f.Age, KumikoSortDirection.Ascending);

        Assert.Empty(f.Source.ActiveSortColumns);
        Assert.Empty(f.Events);
    }

    [Fact]
    public void SetSort_IgnoresAllowSorting_ButToggleSortHonorsIt()
    {
        var f = new Fixture();
        f.Age.AllowSorting = false;

        f.Source.ToggleSort(f.Age);
        Assert.Empty(f.Source.ActiveSortColumns);

        f.Source.SetSort(f.Age, KumikoSortDirection.Ascending);
        Assert.Equal(new[] { f.Age }, f.Source.ActiveSortColumns);
    }

    [Fact]
    public void SetSort_RejectsAForeignColumn()
    {
        var f = new Fixture();
        Assert.Throws<ArgumentException>(() => f.Source.SetSort(new KumikoColumn { PropertyName = "Age" }, KumikoSortDirection.Ascending));
    }

    // ── ScrollChanged ─────────────────────────────────────────────

    [Fact]
    public void ScrollChanged_RaisedOncePerActualChange()
    {
        var scroll = new ScrollState { ViewportWidth = 100, ViewportHeight = 100, ContentWidth = 500, ContentHeight = 500 };
        var events = new List<KumikoScrollChangedEventArgs>();
        scroll.ScrollChanged += (_, e) => events.Add(e);

        scroll.OffsetY = 50;
        scroll.OffsetY = 50;           // unchanged
        scroll.ScrollBy(10, 1000);     // one event, clamped
        scroll.ClampOffset();          // nothing to clamp
        scroll.OffsetX = 900;          // out of range…
        scroll.ClampOffset();          // …clamped back in one event

        Assert.Equal(4, events.Count);
        Assert.Equal((0f, 0f, 0f, 50f), (events[0].OldOffsetX, events[0].OldOffsetY, events[0].OffsetX, events[0].OffsetY));
        Assert.Equal((0f, 50f, 10f, 400f), (events[1].OldOffsetX, events[1].OldOffsetY, events[1].OffsetX, events[1].OffsetY));
        Assert.Equal((900f, 400f), (events[3].OldOffsetX, events[3].OffsetY));
        Assert.Equal(400f, events[3].OffsetX);
    }

    // ── Selection and editing follow their rows ───────────────────

    [Fact]
    public void Selection_FollowsItemsThroughASort_AndDropsRowsFilteredOut()
    {
        var f = new Fixture();
        var selection = new KumikoSelectionModel(f.Source);
        selection.HandleRowSelection(0, false, false);  // Alice
        selection.HandleRowSelection(4, false, true);   // + Eve (Ctrl)
        selection.CurrentCell = new CellPosition(4, 2);
        int changes = 0;
        selection.SelectionChanged += (_, _) => changes++;

        f.Source.SetSort(f.Age, KumikoSortDirection.Descending); // Eve 0, Charlie 1, Alice 2, Diana 3, Bob 4
        Assert.Equal(new[] { 0, 2 }, selection.SelectedRows.OrderBy(r => r));
        Assert.Equal(new CellPosition(0, 2), selection.CurrentCell);

        f.Source.SetFilter(o => ((Person)o).Name != "Eve");
        Assert.Equal(new[] { 1 }, selection.SelectedRows); // Alice
        Assert.False(selection.CurrentCell.IsValid);
        Assert.Equal(1, changes); // the sort kept the same items selected; only the filter removed one

        f.People[0].Name = "Alicia"; // redraw: nothing moves
        Assert.Equal(1, changes);
    }

    [Fact]
    public void Edit_FollowsItsRow_AndIsCanceledWhenTheRowDisappears()
    {
        var f = new Fixture();
        var edit = new EditSession();
        f.Source.ViewChanged += (_, e) => edit.ApplyViewChange(e);
        var ended = new List<CellEndEditEventArgs>();
        edit.CellEndEdit += (_, e) => ended.Add(e);
        Assert.True(edit.BeginEdit(0, 0, f.Name, f.Source, new KumikoRect(0, 0, 100, 30))); // Alice

        f.Source.SetSort(f.Age, KumikoSortDirection.Descending);
        Assert.Equal(new CellPosition(2, 0), edit.EditCell);
        Assert.True(edit.IsEditing);

        f.Source.SetFilter(o => ((Person)o).Name != "Alice");
        Assert.False(edit.IsEditing);
        var end = Assert.Single(ended);
        Assert.False(end.Committed);
    }

    [Fact]
    public void Commit_ThatMovesTheRow_ReportsTheEditedRow_AndEndsNormally()
    {
        var f = new Fixture();
        f.Source.SetSort(f.Age, KumikoSortDirection.Ascending); // Bob 25, Diana 28, Alice 30, Charlie 35, Eve 40
        var edit = new EditSession();
        f.Source.ViewChanged += (_, e) => edit.ApplyViewChange(e);
        CellEndEditEventArgs? ended = null;
        edit.CellEndEdit += (_, e) => ended = e;
        Assert.True(edit.BeginEdit(0, 1, f.Age, f.Source, new KumikoRect(0, 0, 100, 30), '9')); // Bob's age → 9…

        Assert.True(edit.CommitEdit(f.Source));

        Assert.False(edit.IsEditing);
        Assert.NotNull(ended);
        Assert.True(ended!.Committed);
        Assert.Equal(0, ended.RowIndex);
    }
}

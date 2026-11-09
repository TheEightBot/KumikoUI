using System.Collections.ObjectModel;
using System.ComponentModel;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests;

/// <summary>
/// Incremental collection updates and item property changes (#21): a filter predicate or column filter holds
/// through Add, Remove, Replace, Move and Reset; <c>PropertyChanged</c> with no name or a parent of a nested
/// path rebuilds; and each mutation raises one <see cref="KumikoSource.ViewChanged"/> of the right kind.
/// </summary>
public class IncrementalUpdateTests
{
    private sealed class Row : INotifyPropertyChanged
    {
        private int _age;
        private string _name = "";
        private Address _address = new();

        public int Age { get => _age; set { _age = value; Raise(nameof(Age)); } }
        public string Name { get => _name; set { _name = value; Raise(nameof(Name)); } }
        public Address Address { get => _address; set { _address = value; Raise(nameof(Address)); } }

        public event PropertyChangedEventHandler? PropertyChanged;
        public void Raise(string? name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        public override string ToString() => Name;
    }

    private static Row R(string name, int age, string city = "") =>
        new() { Name = name, Age = age, Address = new Address { City = city } };

    private static ObservableCollection<Row> Items() => new()
    {
        R("A", 10), R("B", 40), R("C", 20), R("D", 50), R("E", 30),
    };

    private static KumikoColumn[] Columns() => new[]
    {
        new KumikoColumn { Header = "Name", PropertyName = "Name" },
        new KumikoColumn { Header = "Age", PropertyName = "Age", ColumnType = KumikoColumnType.Numeric },
        new KumikoColumn { Header = "City", PropertyName = "Address.City" },
    };

    private sealed class Fixture
    {
        public ObservableCollection<Row> Rows { get; }
        public KumikoSource Source { get; } = new();
        public KumikoColumn[] Cols { get; } = Columns();
        public List<KumikoViewChangedEventArgs> Events { get; } = new();

        public Fixture(ObservableCollection<Row>? rows = null)
        {
            Rows = rows ?? Items();
            Source.SetColumns(Cols);
            Source.SetItems(Rows);
            Source.ViewChanged += (_, e) => Events.Add(e);
        }

        public KumikoColumn Name => Cols[0];
        public KumikoColumn Age => Cols[1];
        public KumikoColumn City => Cols[2];

        public IEnumerable<KumikoViewChangeKind> Kinds => Events.Select(e => e.Kind);
        public string[] Visible => Enumerable.Range(0, Source.RowCount).Select(i => ((Row)Source.GetItem(i)).Name).ToArray();

        /// <summary>The rows a full rebuild over the same items, columns' filters and predicate would give.</summary>
        public string[] Expected(Func<object, bool>? predicate, FilterDescription? columnFilter = null)
        {
            var fresh = new KumikoSource();
            var cols = Columns();
            fresh.SetColumns(cols);
            fresh.SetItems(Rows.ToList());
            if (predicate != null) fresh.SetFilter(predicate);
            if (columnFilter != null)
                fresh.SetColumnFilter(cols[1], new FilterDescription(cols[1]) { Condition1 = columnFilter.Condition1 });
            return Enumerable.Range(0, fresh.RowCount).Select(i => ((Row)fresh.GetItem(i)).Name).ToArray();
        }
    }

    private static readonly Func<object, bool> Adults = o => ((Row)o).Age >= 25;

    private static FilterDescription AgeFilter(KumikoColumn age) => new(age)
    {
        Condition1 = new FilterCondition { NumericOperator = NumericFilterOperator.GreaterThanOrEqual, Value = 25.0 },
    };

    public enum Filtering { Predicate, Column }

    private static Fixture Filtered(Filtering kind)
    {
        var f = new Fixture();
        if (kind == Filtering.Predicate)
            f.Source.SetFilter(Adults);
        else
            f.Source.SetColumnFilter(f.Age, AgeFilter(f.Age));
        f.Events.Clear();
        return f;
    }

    private static string[] Rebuilt(Fixture f, Filtering kind) =>
        kind == Filtering.Predicate ? f.Expected(Adults) : f.Expected(null, AgeFilter(f.Age));

    // ── Add ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(Filtering.Predicate, 0, 60, true)]
    [InlineData(Filtering.Predicate, 2, 60, true)]
    [InlineData(Filtering.Predicate, 5, 60, true)]
    [InlineData(Filtering.Predicate, 0, 5, false)]
    [InlineData(Filtering.Predicate, 2, 5, false)]
    [InlineData(Filtering.Predicate, 5, 5, false)]
    [InlineData(Filtering.Column, 0, 60, true)]
    [InlineData(Filtering.Column, 3, 60, true)]
    [InlineData(Filtering.Column, 5, 60, true)]
    [InlineData(Filtering.Column, 0, 5, false)]
    [InlineData(Filtering.Column, 3, 5, false)]
    [InlineData(Filtering.Column, 5, 5, false)]
    public void Add_HonorsTheFilter_AndMatchesAFullRebuild(Filtering kind, int index, int age, bool passes)
    {
        var f = Filtered(kind);
        int before = f.Source.RowCount;

        f.Rows.Insert(index, R("N", age));

        Assert.Equal(passes ? before + 1 : before, f.Source.RowCount);
        Assert.Equal(passes, f.Visible.Contains("N"));
        Assert.Equal(Rebuilt(f, kind), f.Visible);
        // The source indices after the insertion point moved: later changes stay correct.
        f.Rows.Add(R("Z", 99));
        Assert.Equal(Rebuilt(f, kind), f.Visible);
    }

    [Theory]
    [InlineData(Filtering.Predicate)]
    [InlineData(Filtering.Column)]
    public void Add_RaisesInsertForAVisibleRow_AndRedrawForAFilteredOutRow(Filtering kind)
    {
        var f = Filtered(kind);

        f.Rows.Insert(1, R("Pass", 60)); // B (40) is the first visible row, so "Pass" lands at view row 0
        f.Rows.Insert(1, R("Fail", 5));

        Assert.Equal(new[] { KumikoViewChangeKind.Insert, KumikoViewChangeKind.Redraw }, f.Kinds);
        Assert.Equal(0, f.Events[0].ViewIndex);
        Assert.Equal((4, 4), (f.Events[1].OldRowCount, f.Events[1].NewRowCount)); // 3 + Pass
        Assert.Equal(f.Source.RowCount, f.Events[1].NewRowCount);
    }

    [Fact]
    public void Add_ToAFilteredSource_KeepsTheRowKeysConsistent()
    {
        var f = Filtered(Filtering.Predicate);

        f.Rows.Insert(0, R("Fail", 5));
        f.Rows.Add(R("Pass", 60));
        f.Rows.RemoveAt(2); // B (after "Fail" was inserted at the start)

        // MapRow follows items through the changes: the event after Pass was added maps old rows by index.
        var added = f.Events[1];
        Assert.Equal(KumikoViewChangeKind.Insert, added.Kind);
        Assert.Equal(3, added.ViewIndex);
        Assert.Equal(added.ViewIndex, added.GetViewRow(f.Rows[^1]));
        Assert.Equal(KumikoViewChangeKind.Remove, f.Events[2].Kind);
        Assert.Equal(0, f.Events[2].ViewIndex);
        Assert.Equal(new[] { "D", "E", "Pass" }, f.Visible);
    }

    // ── Remove ────────────────────────────────────────────────────

    [Theory]
    [InlineData(Filtering.Predicate)]
    [InlineData(Filtering.Column)]
    public void Remove_OfAFilteredOutItem_ShiftsIndicesWithoutARebuild(Filtering kind)
    {
        var f = Filtered(kind);

        f.Rows.RemoveAt(0); // A (10) is not visible

        Assert.Equal(new[] { KumikoViewChangeKind.Redraw }, f.Kinds);
        Assert.Equal(new[] { "B", "D", "E" }, f.Visible);
        Assert.Equal(Rebuilt(f, kind), f.Visible);
        f.Rows.RemoveAt(2); // D: the shifted indices still point at the right items
        Assert.Equal(Rebuilt(f, kind), f.Visible);
    }

    [Theory]
    [InlineData(Filtering.Predicate)]
    [InlineData(Filtering.Column)]
    public void Remove_OfAVisibleItem_RaisesRemove(Filtering kind)
    {
        var f = Filtered(kind);

        f.Rows.RemoveAt(3); // D

        Assert.Equal(new[] { KumikoViewChangeKind.Remove }, f.Kinds);
        Assert.Equal(1, f.Events[0].ViewIndex);
        Assert.Equal(new[] { "B", "E" }, f.Visible);
        Assert.Equal(Rebuilt(f, kind), f.Visible);
    }

    // ── Replace, Move, Reset ──────────────────────────────────────

    [Theory]
    [InlineData(Filtering.Predicate)]
    [InlineData(Filtering.Column)]
    public void Replace_Move_AndReset_MatchAFullRebuild(Filtering kind)
    {
        var f = Filtered(kind);

        f.Rows[0] = R("A2", 70); // filtered-out item replaced by a visible one
        Assert.Equal(Rebuilt(f, kind), f.Visible);
        f.Rows[1] = R("B2", 1);  // visible replaced by a hidden one
        Assert.Equal(Rebuilt(f, kind), f.Visible);
        f.Rows.Move(0, 4);
        Assert.Equal(Rebuilt(f, kind), f.Visible);
        f.Rows.Move(3, 1);
        Assert.Equal(Rebuilt(f, kind), f.Visible);
        Assert.Equal(new[] { KumikoViewChangeKind.Rebuild, KumikoViewChangeKind.Rebuild, KumikoViewChangeKind.Rebuild, KumikoViewChangeKind.Rebuild }, f.Kinds);

        f.Events.Clear();
        f.Rows.Clear();
        Assert.Equal(0, f.Source.RowCount);
        Assert.Equal(new[] { KumikoViewChangeKind.Rebuild }, f.Kinds);
        f.Rows.Add(R("X", 99));
        f.Rows.Add(R("Y", 1));
        Assert.Equal(new[] { "X" }, f.Visible);
        Assert.Equal(Rebuilt(f, kind), f.Visible);
    }

    [Fact]
    public void Add_WithoutAListener_StillHonorsTheFilter()
    {
        var rows = Items();
        var source = new KumikoSource();
        source.SetColumns(Columns());
        source.SetItems(rows);
        source.SetFilter(Adults);

        rows.Add(R("Fail", 1));
        rows.Add(R("Pass", 90));
        rows.RemoveAt(0);

        Assert.Equal(new[] { "B", "D", "E", "Pass" },
            Enumerable.Range(0, source.RowCount).Select(i => ((Row)source.GetItem(i)).Name).ToArray());
    }

    [Fact]
    public void Add_InsideABatch_WithAFilter_RaisesOneEventAtTheEnd()
    {
        var f = Filtered(Filtering.Predicate);

        f.Source.BeginUpdate();
        f.Rows.Add(R("Fail", 1));
        f.Rows.Add(R("Pass", 90));
        Assert.Empty(f.Events);
        f.Source.EndUpdate();

        Assert.Equal(new[] { KumikoViewChangeKind.Rebuild }, f.Kinds);
        Assert.Equal(new[] { "B", "D", "E", "Pass" }, f.Visible);
    }

    [Fact]
    public void Add_InsideABatch_OfAFilteredOutItemOnly_RaisesARedraw()
    {
        var f = Filtered(Filtering.Predicate);

        f.Source.BeginUpdate();
        f.Rows.Add(R("Fail", 1));
        f.Source.EndUpdate();

        Assert.Equal(new[] { KumikoViewChangeKind.Redraw }, f.Kinds);
    }

    [Fact]
    public void Add_WithSortOrGrouping_StillRebuilds_AndHonorsTheFilter()
    {
        var f = Filtered(Filtering.Predicate);
        f.Source.SetSort(f.Age, KumikoSortDirection.Descending);
        f.Events.Clear();

        f.Rows.Add(R("Fail", 1));
        f.Rows.Add(R("Pass", 45));

        Assert.Equal(new[] { KumikoViewChangeKind.Rebuild, KumikoViewChangeKind.Rebuild }, f.Kinds);
        Assert.Equal(new[] { "D", "Pass", "B", "E" }, f.Visible);
    }

    // ── Item property changes ─────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void PropertyChanged_WithNoName_RebuildsTheView(string? name)
    {
        var f = new Fixture();
        f.Source.SetSort(f.Age, KumikoSortDirection.Ascending);
        f.Events.Clear();

        f.Rows[0].Raise(name);

        Assert.Equal(new[] { KumikoViewChangeKind.Rebuild }, f.Kinds);
    }

    [Fact]
    public void PropertyChanged_WithNoName_RebuildsEvenWhenNothingIsSorted()
    {
        var f = new Fixture();

        f.Rows[0].Raise("");

        // Any property may have changed, and the view cannot tell that none of them matters.
        Assert.Equal(new[] { KumikoViewChangeKind.Rebuild }, f.Kinds);
    }

    [Fact]
    public void PropertyChanged_OfAnUnrelatedProperty_OnlyRedraws()
    {
        var f = new Fixture();
        f.Source.SetSort(f.Age, KumikoSortDirection.Ascending);
        f.Events.Clear();

        f.Rows[0].Name = "Renamed";

        Assert.Equal(new[] { KumikoViewChangeKind.Redraw }, f.Kinds);
    }

    [Fact]
    public void PropertyChanged_MatchesThePropertyName_IgnoringCase()
    {
        var f = new Fixture();
        f.Source.SetSort(f.Age, KumikoSortDirection.Ascending);
        f.Events.Clear();

        // Columns bind to properties ignoring case, so a differently cased notification still counts.
        f.Rows[0].Raise("age");

        Assert.Equal(new[] { KumikoViewChangeKind.Rebuild }, f.Kinds);
    }

    [Fact]
    public void PropertyChanged_OfAPrefixOfAPropertyName_DoesNotRebuild()
    {
        var f = new Fixture();
        f.Source.SetSort(f.City, KumikoSortDirection.Ascending); // path "Address.City"
        f.Events.Clear();

        f.Rows[0].Raise("Add");
        f.Rows[0].Raise("Address.Ci");

        Assert.Equal(new[] { KumikoViewChangeKind.Redraw, KumikoViewChangeKind.Redraw }, f.Kinds);
    }

    [Fact]
    public void NestedPath_ParentChange_RebuildsASortedColumn()
    {
        var rows = new ObservableCollection<Row> { R("A", 1, "Rome"), R("B", 2, "Oslo"), R("C", 3, "Paris") };
        var f = new Fixture(rows);
        f.Source.SetSort(f.City, KumikoSortDirection.Ascending);
        Assert.Equal(new[] { "B", "C", "A" }, f.Visible);
        f.Events.Clear();

        rows[0].Address = new Address { City = "Athens" }; // raises "Address"

        Assert.Equal(new[] { KumikoViewChangeKind.Rebuild }, f.Kinds);
        Assert.Equal(new[] { "A", "B", "C" }, f.Visible);
    }

    [Fact]
    public void NestedPath_ParentChange_RebuildsAFilteredColumn()
    {
        var rows = new ObservableCollection<Row> { R("A", 1, "Rome"), R("B", 2, "Oslo") };
        var f = new Fixture(rows);
        f.Source.SetColumnFilter(f.City, new FilterDescription(f.City) { SelectedValues = new() { "Oslo" } });
        Assert.Equal(new[] { "B" }, f.Visible);
        f.Events.Clear();

        rows[0].Address = new Address { City = "Oslo" };

        Assert.Equal(new[] { KumikoViewChangeKind.Rebuild }, f.Kinds);
        Assert.Equal(new[] { "A", "B" }, f.Visible);
    }

    [Fact]
    public void NestedPath_ParentChange_RebuildsAGroupAndSummary()
    {
        var rows = new ObservableCollection<Row> { R("A", 1, "Rome"), R("B", 2, "Oslo") };
        var f = new Fixture(rows);
        f.Source.AddGroupDescription(new GroupDescription("Address.City"));
        f.Events.Clear();
        rows[0].Address = new Address { City = "Oslo" };
        Assert.Equal(new[] { KumikoViewChangeKind.Rebuild }, f.Kinds);
        Assert.Equal(1, Enumerable.Range(0, f.Source.RowCount).Count(f.Source.IsGroupHeaderRow));

        var g = new Fixture(Items());
        g.Source.AddTableSummaryRow(new TableSummaryRow("T") { Columns = { new SummaryColumnDescription("Address.City", SummaryType.Count) } });
        g.Events.Clear();
        g.Rows[0].Address = new Address { City = "X" };
        Assert.Equal(new[] { KumikoViewChangeKind.Rebuild }, g.Kinds);
    }

    [Fact]
    public void NestedPath_ParentChange_WithNothingDependingOnIt_OnlyRedraws()
    {
        var f = new Fixture();

        f.Rows[0].Address = new Address { City = "Oslo" };

        Assert.Equal(new[] { KumikoViewChangeKind.Redraw }, f.Kinds);
    }

    [Fact]
    public void TwoColumnsSharingAProperty_EitherOneDrivesTheRebuild()
    {
        var f = new Fixture();
        var second = new KumikoColumn { Header = "Age again", PropertyName = "Age" };
        f.Source.SetColumns(f.Cols.Append(second));
        f.Source.SetSort(second, KumikoSortDirection.Descending); // the second of the two
        f.Events.Clear();

        f.Rows[0].Age = 99;

        Assert.Equal(new[] { KumikoViewChangeKind.Rebuild }, f.Kinds);
        Assert.Equal("A", f.Visible[0]);
    }

    [Fact]
    public void ColumnWithAnInactiveFilter_DoesNotRebuild()
    {
        var f = new Fixture();
        f.Source.SetColumnFilter(f.Age, new FilterDescription(f.Age)); // no values, no conditions: not active
        f.Events.Clear();

        f.Rows[0].Age = 99;

        Assert.Equal(new[] { KumikoViewChangeKind.Redraw }, f.Kinds);
    }

    [Fact]
    public void Predicate_AnyPropertyChange_Rebuilds()
    {
        var f = new Fixture();
        f.Source.SetFilter(o => ((Row)o).Age >= 25 && ((Row)o).Name != "Hidden");
        f.Events.Clear();

        f.Rows[1].Name = "Hidden"; // Name drives no column filter, only the predicate

        Assert.Equal(new[] { KumikoViewChangeKind.Rebuild }, f.Kinds);
        Assert.DoesNotContain("Hidden", f.Visible);
        Assert.Equal(new[] { "D", "E" }, f.Visible);

        f.Rows[1].Name = "B"; // and back
        Assert.Equal(new[] { "B", "D", "E" }, f.Visible);
    }

    [Fact]
    public void Predicate_WithNestedItemChange_Rebuilds()
    {
        var rows = new ObservableCollection<Row> { R("A", 1, "Rome"), R("B", 2, "Oslo") };
        var f = new Fixture(rows);
        f.Source.SetFilter(o => ((Row)o).Address.City == "Oslo");
        Assert.Equal(new[] { "B" }, f.Visible);
        f.Events.Clear();

        rows[0].Address = new Address { City = "Oslo" };

        Assert.Equal(new[] { KumikoViewChangeKind.Rebuild }, f.Kinds);
        Assert.Equal(new[] { "A", "B" }, f.Visible);
    }

    // ── Each change event, for each mutation ──────────────────────

    [Fact]
    public void ClearSort_ClearAllFilters_SetFilter_RaiseOneRebuild_AndNothingWhenNothingChanges()
    {
        var f = new Fixture();

        f.Source.ClearSort();
        f.Source.ClearAllFilters();
        f.Source.SetFilter(null);
        f.Source.SetColumnFilter(f.Age, null);
        Assert.Empty(f.Events);

        f.Source.SetSort(f.Age, KumikoSortDirection.Descending);
        f.Source.ClearSort();
        f.Source.SetFilter(Adults);
        f.Source.SetFilter(null);
        f.Source.SetColumnFilter(f.Age, AgeFilter(f.Age));
        f.Source.SetColumnFilter(f.Age, null);
        f.Source.SetFilter(Adults);
        f.Source.ClearAllFilters();
        f.Source.SetColumnFilter(f.Age, AgeFilter(f.Age));
        f.Source.ClearAllFilters();

        Assert.Equal(Enumerable.Repeat(KumikoViewChangeKind.Rebuild, 10), f.Kinds);
        Assert.Equal(5, f.Source.RowCount);
    }

    [Fact]
    public void SetColumnFilter_ThatClearsNothing_RaisesNoFilterEvents()
    {
        var f = new Fixture();
        int events = 0;
        f.Source.FilterChanging += (_, _) => events++;
        f.Source.FilterChanged += (_, _) => events++;

        f.Source.SetColumnFilter(f.Age, null);

        Assert.Equal(0, events);
    }

    [Fact]
    public void DefaultGroupExpanded_RebuildsOnlyWhenGroupingAndChanged()
    {
        var f = new Fixture();

        f.Source.DefaultGroupExpanded = false; // no grouping: nothing to show
        f.Source.DefaultGroupExpanded = false;
        Assert.Empty(f.Events);

        f.Source.DefaultGroupExpanded = true;
        f.Source.AddGroupDescription(new GroupDescription("Name"));
        f.Events.Clear();
        int expandedRows = f.Source.RowCount;

        f.Source.DefaultGroupExpanded = false;
        Assert.Equal(new[] { KumikoViewChangeKind.Rebuild }, f.Kinds);
        Assert.True(f.Source.RowCount < expandedRows);

        f.Source.DefaultGroupExpanded = false; // unchanged
        Assert.Single(f.Events);

        f.Source.DefaultGroupExpanded = true;
        Assert.Equal(2, f.Events.Count);
        Assert.Equal(expandedRows, f.Source.RowCount);
    }

    [Fact]
    public void DefaultGroupExpanded_LeavesGroupsWithTheirOwnStateAlone()
    {
        var f = new Fixture();
        f.Source.AddGroupDescription(new GroupDescription("Name"));
        f.Source.ToggleGroupExpansion(0); // group "A" collapsed on its own
        Assert.False(f.Source.GetGroupHeaderInfo(0)!.IsExpanded);

        f.Source.DefaultGroupExpanded = false;
        f.Source.DefaultGroupExpanded = true;

        Assert.False(f.Source.GetGroupHeaderInfo(0)!.IsExpanded);
        Assert.True(f.Source.GetGroupHeaderInfo(1)!.IsExpanded); // group "B" (row 1: "A" is collapsed) follows the default
    }

    [Fact]
    public void ReorderRow_RaisesOneRebuild_AndNothingWhenTheRowDoesNotMove()
    {
        var f = new Fixture();

        f.Source.ReorderRow(1, 1);
        f.Source.ReorderRow(-1, 2);
        f.Source.ReorderRow(0, 9);
        Assert.Empty(f.Events);

        f.Source.ReorderRow(0, 3);

        Assert.Equal(new[] { KumikoViewChangeKind.Rebuild }, f.Kinds);
        Assert.Equal(new[] { "B", "C", "D", "A", "E" }, f.Visible);
    }

    [Fact]
    public void SetSort_RaisesOneRebuild_AndNothingWhenUnchanged()
    {
        var f = new Fixture();

        f.Source.SetSort(f.Age, KumikoSortDirection.Ascending);
        f.Source.SetSort(f.Age, KumikoSortDirection.Ascending);

        Assert.Equal(new[] { KumikoViewChangeKind.Rebuild }, f.Kinds);
        Assert.Equal(new[] { "A", "C", "E", "B", "D" }, f.Visible);
    }
}

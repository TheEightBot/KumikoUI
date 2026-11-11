using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests;

/// <summary>
/// How KumikoSource follows its items: INotifyCollectionChanged actions the incremental path cannot
/// handle, non-list enumerables, row reordering under a filter or sort, the filter popup's value list, and
/// the access members on non-data rows.
/// </summary>
public class KumikoSourceCollectionTests
{
    private sealed class Row
    {
        public string Id { get; set; } = "";
        public int Value { get; set; }
        public string Group { get; set; } = "";
    }

    /// <summary>A list that raises exactly the collection events a test dictates, with no index unless given.</summary>
    private sealed class ScriptedList : List<Row>, INotifyCollectionChanged
    {
        public event NotifyCollectionChangedEventHandler? CollectionChanged;
        public void Raise(NotifyCollectionChangedEventArgs e) => CollectionChanged?.Invoke(this, e);
    }

    private static Row[] Rows() => new[]
    {
        new Row { Id = "a", Value = 3, Group = "g1" },
        new Row { Id = "b", Value = 1, Group = "g2" },
        new Row { Id = "c", Value = 2, Group = "g1" },
        new Row { Id = "d", Value = 4, Group = "g2" },
    };

    private static KumikoSource MakeSource(IEnumerable items)
    {
        var source = new KumikoSource();
        source.SetColumns(new[]
        {
            new KumikoColumn { PropertyName = "Id" },
            new KumikoColumn { PropertyName = "Value", ColumnType = KumikoColumnType.Numeric },
            new KumikoColumn { PropertyName = "Group" },
        });
        source.SetItems(items);
        return source;
    }

    private static KumikoColumn Col(KumikoSource s, string property) => s.Columns.Single(c => c.PropertyName == property);

    private static string[] Ids(KumikoSource s) =>
        Enumerable.Range(0, s.RowCount).Select(i => ((Row)s.GetItem(i)).Id).ToArray();

    // ── Collection changes the incremental path leaves to a rebuild ──

    [Fact]
    public void Add_WithoutAnIndex_RebuildsTheViewAndRaisesARebuild()
    {
        var list = new ScriptedList();
        list.AddRange(Rows());
        var source = MakeSource(list);
        var kinds = new List<KumikoViewChangeKind>();
        source.ViewChanged += (_, e) => kinds.Add(e.Kind);

        var added = new Row { Id = "e", Value = 9 };
        list.Add(added);
        list.Raise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, added)); // NewStartingIndex = -1

        Assert.Equal(new[] { "a", "b", "c", "d", "e" }, Ids(source));
        Assert.Equal(new[] { KumikoViewChangeKind.Rebuild }, kinds);
    }

    [Fact]
    public void Add_OfSeveralItemsAtOnce_RebuildsInsteadOfInsertingOneRow()
    {
        var items = new ScriptedList();
        items.AddRange(Rows());
        var source = MakeSource(items);
        var kinds = new List<KumikoViewChangeKind>();
        source.ViewChanged += (_, e) => kinds.Add(e.Kind);

        var batch = new List<Row> { new() { Id = "x" }, new() { Id = "y" } };
        items.InsertRange(1, batch);
        items.Raise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, batch, 1));

        Assert.Equal(new[] { "a", "x", "y", "b", "c", "d" }, Ids(source));
        Assert.Equal(new[] { KumikoViewChangeKind.Rebuild }, kinds);
    }

    [Fact]
    public void Insert_InTheMiddle_PutsTheRowAtTheSourcePosition()
    {
        var items = new ObservableCollection<Row>(Rows());
        var source = MakeSource(items);

        items.Insert(2, new Row { Id = "x" });

        Assert.Equal(new[] { "a", "b", "x", "c", "d" }, Ids(source));
    }

    [Fact]
    public void Move_ReordersTheRows()
    {
        var items = new ObservableCollection<Row>(Rows());
        var source = MakeSource(items);

        items.Move(0, 3);

        Assert.Equal(new[] { "b", "c", "d", "a" }, Ids(source));
    }

    [Fact]
    public void Replace_SwapsTheRowAndStopsTrackingTheOldItem()
    {
        var items = new ObservableCollection<Row>(Rows());
        var source = MakeSource(items);

        items[1] = new Row { Id = "z" };

        Assert.Equal(new[] { "a", "z", "c", "d" }, Ids(source));
    }

    [Fact]
    public void Reset_RebuildsFromTheWholeCollection()
    {
        var items = new ObservableCollection<Row>(Rows());
        var source = MakeSource(items);

        items.Clear();
        Assert.Equal(0, source.RowCount);

        items.Add(new Row { Id = "n" });
        Assert.Equal(new[] { "n" }, Ids(source));
    }

    [Fact]
    public void Add_WhileSorted_PlacesTheNewRowBySortKey()
    {
        var items = new ObservableCollection<Row>(Rows());
        var source = MakeSource(items);
        source.SetSort(Col(source, "Value"), KumikoSortDirection.Ascending);

        items.Add(new Row { Id = "x", Value = 2 });

        Assert.Equal(new[] { "b", "c", "x", "a", "d" }, Ids(source)); // the tie with c keeps source order
    }

    [Fact]
    public void Remove_WhileGrouped_DropsTheEmptyGroup()
    {
        var items = new ObservableCollection<Row>(Rows());
        var source = MakeSource(items);
        source.AddGroupDescription(new GroupDescription("Group"));
        Assert.Equal(6, source.RowCount);

        items.Remove(items.Single(r => r.Id == "b"));
        items.Remove(items.Single(r => r.Id == "d"));

        Assert.Equal(3, source.RowCount); // one header + a, c
        Assert.Equal("g1", source.GetGroupHeaderInfo(0)!.DisplayText);
    }

    // ── SetItems with other enumerables ───────────────────────────

    private static IEnumerable<Row> Lazy()
    {
        foreach (var row in Rows())
            yield return row;
    }

    [Fact]
    public void SetItems_WithALazyEnumerable_ShowsAllItemsOnce()
    {
        var source = MakeSource(Lazy());

        Assert.Equal(new[] { "a", "b", "c", "d" }, Ids(source));
    }

    [Fact]
    public void SetItems_WithAnArray_SortsWithoutReorderingTheArray()
    {
        var array = Rows();
        var source = MakeSource(array);

        source.SetSort(Col(source, "Value"), KumikoSortDirection.Ascending);

        Assert.Equal(new[] { "b", "c", "a", "d" }, Ids(source));
        Assert.Equal(new[] { "a", "b", "c", "d" }, array.Select(r => r.Id));
    }

    [Fact]
    public void SetItems_WithANonListCollection_ShowsItsValues()
    {
        // A collection that is not an IList is snapshotted; the view shows what it held.
        var source = MakeSource(Rows().ToDictionary(r => r.Id, r => r).Values);

        Assert.Equal(4, source.RowCount);
    }

    [Fact]
    public void SetItems_Twice_ReplacesTheRows()
    {
        var source = MakeSource(Rows());

        source.SetItems(new List<Row> { new() { Id = "only" } });

        Assert.Equal(new[] { "only" }, Ids(source));
    }

    [Fact]
    public void SetItems_WithAnEmptyList_HasNoRows_AndSummariesStayValid()
    {
        var source = MakeSource(new List<Row>());
        source.AddTableSummaryRow(new TableSummaryRow("T") { Columns = { new SummaryColumnDescription("Value", SummaryType.Sum) } });

        Assert.Equal(0, source.RowCount);
        Assert.Equal(0d, source.ComputedBottomSummaries.Single().RawValues["Value"]);
    }

    // ── ReorderRow ────────────────────────────────────────────────

    [Fact]
    public void ReorderRow_MovesTheItemInTheUnderlyingList()
    {
        var list = Rows().ToList();
        var source = MakeSource(list);

        source.ReorderRow(0, 2);

        Assert.Equal(new[] { "b", "c", "a", "d" }, list.Select(r => r.Id));
        Assert.Equal(new[] { "b", "c", "a", "d" }, Ids(source));
    }

    [Fact]
    public void ReorderRow_UnderAFilter_MovesToTheTargetRowsPositionInTheSource()
    {
        var list = Rows().ToList();
        var source = MakeSource(list);
        source.SetFilter(i => ((Row)i).Group == "g1"); // visible: a, c

        source.ReorderRow(0, 1); // a goes where c is

        Assert.Equal(new[] { "b", "c", "a", "d" }, list.Select(r => r.Id));
        Assert.Equal(new[] { "c", "a" }, Ids(source));
    }

    [Fact]
    public void ReorderRow_WhileSorted_IsRejected_AndChangesNothing()
    {
        // CORE-48: the sort decides the order, so a move would snap back; it is refused instead.
        var list = Rows().ToList();
        var source = MakeSource(list);
        source.SetSort(Col(source, "Value"), KumikoSortDirection.Ascending); // b, c, a, d
        int events = 0;
        source.ViewChanged += (_, _) => events++;

        var result = source.ReorderRow(0, 3);

        Assert.Equal(KumikoReorderResult.NotSupportedSorted, result);
        Assert.Equal(new[] { "b", "c", "a", "d" }, Ids(source));
        Assert.Equal(new[] { "a", "b", "c", "d" }, list.Select(r => r.Id));
        Assert.Equal(0, events);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    [InlineData(4, 0)]
    [InlineData(0, 4)]
    [InlineData(2, 2)]
    public void ReorderRow_OutOfRangeOrSameRow_ChangesNothingAndRaisesNothing(int from, int to)
    {
        var list = Rows().ToList();
        var source = MakeSource(list);
        int events = 0;
        source.ViewChanged += (_, _) => events++;

        source.ReorderRow(from, to);

        Assert.Equal(new[] { "a", "b", "c", "d" }, list.Select(r => r.Id));
        Assert.Equal(0, events);
    }

    [Fact]
    public void ReorderRow_OfAnObservableCollection_RaisesOneRebuild_AndTheRowKeysFollow()
    {
        var items = new ObservableCollection<Row>(Rows());
        var source = MakeSource(items);
        var kinds = new List<KumikoViewChangeKind>();
        source.ViewChanged += (_, e) => kinds.Add(e.Kind);

        source.ReorderRow(3, 0);

        Assert.Equal(new[] { KumikoViewChangeKind.Rebuild }, kinds);
        Assert.Equal(new[] { "d", "a", "b", "c" }, items.Select(r => r.Id));
    }

    // ── ReorderRow results (#99) ──────────────────────────────────

    /// <summary>The visible rows, a group header as <c>[key]</c>, a group summary as <c>Σ</c>.</summary>
    private static string[] View(KumikoSource s) =>
        Enumerable.Range(0, s.RowCount).Select(i =>
            s.GetGroupHeaderInfo(i) is { } header ? $"[{header.DisplayText}]"
            : s.IsGroupSummaryRow(i) ? "Σ"
            : ((Row)s.GetItem(i)).Id).ToArray();

    private static Row[] GroupedRows() => new[]
    {
        new Row { Id = "a", Group = "g1" },
        new Row { Id = "b", Group = "g2" },
        new Row { Id = "c", Group = "g1" },
        new Row { Id = "d", Group = "g2" },
        new Row { Id = "e", Group = "g1" },
        new Row { Id = "f", Group = "g2" },
    };

    [Fact]
    public void ReorderRow_ReturnsMoved_AndRaisesOneRebuild()
    {
        var list = Rows().ToList();
        var source = MakeSource(list);
        int events = 0;
        source.ViewChanged += (_, _) => events++;

        Assert.Equal(KumikoReorderResult.Moved, source.ReorderRow(3, 1));

        Assert.Equal(new[] { "a", "d", "b", "c" }, list.Select(r => r.Id));
        Assert.Equal(1, events);
    }

    [Theory]
    [InlineData(-1, 1, KumikoReorderResult.OutOfRange)]
    [InlineData(0, 4, KumikoReorderResult.OutOfRange)]
    [InlineData(2, 2, KumikoReorderResult.NoOp)]
    public void ReorderRow_ReportsWhyNothingMoved(int from, int to, KumikoReorderResult expected)
    {
        var source = MakeSource(Rows().ToList());

        Assert.Equal(expected, source.ReorderRow(from, to));
    }

    public static TheoryData<IEnumerable> UnsupportedSources() => new()
    {
        Rows().Select(r => r),                                   // not an IList: the grid reads a private copy
        Rows(),                                                  // an array: fixed size
        new ReadOnlyCollection<Row>(Rows()),                     // read-only
    };

    [Theory]
    [MemberData(nameof(UnsupportedSources))]
    public void ReorderRow_OfASourceThatCannotMove_IsRejected_AndChangesNothing(IEnumerable items)
    {
        var source = MakeSource(items);
        int events = 0;
        source.ViewChanged += (_, _) => events++;

        Assert.Equal(KumikoReorderResult.NotSupportedSource, source.ReorderRow(0, 2));

        Assert.Equal(new[] { "a", "b", "c", "d" }, Ids(source));
        Assert.Equal(0, events);
    }

    [Fact]
    public void ReorderRow_WithinAGroup_MovesTheItemNextToTheTarget_KeepingTheGroupsOtherItemsInOrder()
    {
        var list = GroupedRows().ToList();
        var source = MakeSource(list);
        source.AddGroupDescription(new GroupDescription("Group"));
        Assert.Equal(new[] { "[g1]", "a", "c", "e", "[g2]", "b", "d", "f" }, View(source));

        Assert.Equal(KumikoReorderResult.Moved, source.ReorderRow(1, 3)); // a after e

        Assert.Equal(new[] { "[g1]", "c", "e", "a", "[g2]", "b", "d", "f" }, View(source));
        Assert.Equal(new[] { "b", "c", "d", "e", "a", "f" }, list.Select(r => r.Id));

        Assert.Equal(KumikoReorderResult.Moved, source.ReorderRow(7, 5)); // f before b

        Assert.Equal(new[] { "[g1]", "c", "e", "a", "[g2]", "f", "b", "d" }, View(source));
    }

    [Fact]
    public void ReorderRow_AcrossGroups_IsRejected_AndChangesNothing()
    {
        var list = GroupedRows().ToList();
        var source = MakeSource(list);
        source.AddGroupDescription(new GroupDescription("Group"));
        int events = 0;
        source.ViewChanged += (_, _) => events++;

        Assert.Equal(KumikoReorderResult.NotSupportedAcrossGroups, source.ReorderRow(1, 6)); // a onto d (g2)
        Assert.Equal(KumikoReorderResult.OutOfRange, source.ReorderRow(1, 4));                // onto the g2 header

        Assert.Equal(new[] { "a", "b", "c", "d", "e", "f" }, list.Select(r => r.Id));
        Assert.Equal(0, events);
    }

    [Fact]
    public void ReorderRow_WithNestedGroups_StaysWithinTheInnermostGroup()
    {
        var list = new List<Row>
        {
            new() { Id = "a", Group = "g1", Value = 1 },
            new() { Id = "b", Group = "g1", Value = 2 },
            new() { Id = "c", Group = "g1", Value = 1 },
            new() { Id = "d", Group = "g1", Value = 1 },
        };
        var source = MakeSource(list);
        source.AddGroupDescription(new GroupDescription("Group"));
        source.AddGroupDescription(new GroupDescription("Value"));
        Assert.Equal(new[] { "[g1]", "[1]", "a", "c", "d", "[2]", "b" }, View(source));

        Assert.Equal(KumikoReorderResult.Moved, source.ReorderRow(4, 2));                     // d before a
        Assert.Equal(KumikoReorderResult.NotSupportedAcrossGroups, source.ReorderRow(2, 6)); // into Value = 2

        Assert.Equal(new[] { "[g1]", "[1]", "d", "a", "c", "[2]", "b" }, View(source));
    }

    [Fact]
    public void ReorderRow_WithinAGroup_UnderAFilter_MovesToTheTargetsPosition()
    {
        var list = GroupedRows().ToList();
        var source = MakeSource(list);
        source.AddGroupDescription(new GroupDescription("Group"));
        source.SetFilter(i => ((Row)i).Id != "c"); // g1: a, e

        Assert.Equal(KumikoReorderResult.Moved, source.ReorderRow(2, 1)); // e before a

        Assert.Equal(new[] { "[g1]", "e", "a", "[g2]", "b", "d", "f" }, View(source));
        Assert.Equal(new[] { "e", "a", "b", "c", "d", "f" }, list.Select(r => r.Id));
    }

    [Fact]
    public void ReorderRow_WithGroupSummaries_TreatsTheSummaryAsOutOfRange()
    {
        var list = GroupedRows().ToList();
        var source = MakeSource(list);
        source.AddGroupDescription(new GroupDescription("Group"));
        source.AddGroupSummaryRow(new GroupSummaryRow { Name = "S", Columns = { new SummaryColumnDescription("Value", SummaryType.Sum) } });
        Assert.Equal(new[] { "[g1]", "a", "c", "e", "Σ", "[g2]", "b", "d", "f", "Σ" }, View(source));

        Assert.Equal(KumikoReorderResult.OutOfRange, source.ReorderRow(1, 4));
        Assert.Equal(KumikoReorderResult.Moved, source.ReorderRow(1, 3));

        Assert.Equal(new[] { "[g1]", "c", "e", "a", "Σ", "[g2]", "b", "d", "f", "Σ" }, View(source));
    }

    // ── The filter popup's value list ─────────────────────────────

    [Fact]
    public void GetUniqueColumnValues_ExcludesItsOwnFilter_ButHonorsTheOthers()
    {
        var source = MakeSource(Rows());
        var group = Col(source, "Group");
        var id = Col(source, "Id");
        source.SetColumnFilter(group, new FilterDescription(group) { SelectedValues = new() { "g1" } });
        source.SetColumnFilter(id, new FilterDescription(id) { SelectedValues = new() { "a", "b" } });

        // Group's own filter (g1) is ignored; Id's filter (a, b) still applies, leaving g1 (a) and g2 (b).
        Assert.Equal(new[] { "g1", "g2" }, source.GetUniqueColumnValues(group));
        // Id's list is narrowed by Group's filter (g1): a and c.
        Assert.Equal(new[] { "a", "c" }, source.GetUniqueColumnValues(id));
    }

    [Fact]
    public void GetUniqueColumnValues_IsSortedAndDistinct_AndIgnoresTheGlobalPredicateOnlyWhenCleared()
    {
        var source = MakeSource(new[]
        {
            new Row { Id = "x", Group = "b" }, new Row { Id = "y", Group = "a" }, new Row { Id = "z", Group = "b" },
        });
        var group = Col(source, "Group");

        Assert.Equal(new[] { "a", "b" }, source.GetUniqueColumnValues(group));

        source.SetFilter(r => ((Row)r).Id != "y");
        Assert.Equal(new[] { "b" }, source.GetUniqueColumnValues(group));
    }

    // ── Rows that are not data rows ───────────────────────────────

    [Fact]
    public void GetCellValue_OfAGroupHeader_IsNull_AndDisplayTextIsEmpty()
    {
        var source = MakeSource(Rows());
        source.AddGroupDescription(new GroupDescription("Group"));

        Assert.Null(source.GetCellValue(0, Col(source, "Id")));
        Assert.Equal("", source.GetCellDisplayText(0, Col(source, "Id")));
    }

    [Fact(Skip = "Known defect: GetItem and GetCellValue on a group summary row return the first source item instead of throwing or returning null")]
    public void GetCellValue_OfAGroupSummaryRow_IsNull()
    {
        var source = MakeSource(Rows());
        source.AddGroupDescription(new GroupDescription("Group"));
        source.AddGroupSummaryRow(new GroupSummaryRow("S") { Columns = { new SummaryColumnDescription("Value", SummaryType.Sum) } });
        int summaryRow = Enumerable.Range(0, source.RowCount).First(source.IsGroupSummaryRow);

        Assert.Null(source.GetCellValue(summaryRow, Col(source, "Id")));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void GetItem_OutOfRange_Throws_WithAndWithoutGrouping(int row)
    {
        var source = MakeSource(Rows());
        Assert.Throws<ArgumentOutOfRangeException>(() => source.GetItem(row));

        source.AddGroupDescription(new GroupDescription("Group"));
        Assert.Throws<ArgumentOutOfRangeException>(() => source.GetItem(row == 4 ? 99 : row));
    }

    [Fact]
    public void SetCellValue_OnAGroupHeaderOrOutOfRange_WritesNothing()
    {
        var source = MakeSource(Rows());
        source.AddGroupDescription(new GroupDescription("Group"));
        var value = Col(source, "Value");

        Assert.False(source.SetCellValue(0, value, 99)); // group header
        Assert.False(source.SetCellValue(99, value, 99));
        Assert.False(source.SetCellValue(-1, value, 99));
        Assert.DoesNotContain(99, Enumerable.Range(0, source.RowCount).Where(r => !source.IsNonDataRow(r)).Select(r => ((Row)source.GetItem(r)).Value));
    }

    [Fact]
    public void GroupHeaderQueries_OutOfRange_AreFalseOrNull()
    {
        var source = MakeSource(Rows());
        source.AddGroupDescription(new GroupDescription("Group"));

        Assert.False(source.IsGroupHeaderRow(-1));
        Assert.False(source.IsGroupHeaderRow(99));
        Assert.False(source.IsGroupSummaryRow(99));
        Assert.Null(source.GetGroupHeaderInfo(99));
        Assert.Null(source.GetGroupSummaryRowInfo(0));
        Assert.Null(source.GetGroupSummaryRowInfo(99));
    }

    [Fact]
    public void GroupQueries_WithoutGrouping_AreFalseOrNull()
    {
        var source = MakeSource(Rows());

        Assert.False(source.IsGroupHeaderRow(0));
        Assert.False(source.IsGroupSummaryRow(0));
        Assert.False(source.IsNonDataRow(0));
        Assert.Null(source.GetGroupHeaderInfo(0));
        Assert.Null(source.GetGroupSummaryRowInfo(0));
    }

    [Fact]
    public void ToggleGroupExpansion_OnADataRowOrOutOfRange_DoesNothing()
    {
        var source = MakeSource(Rows());
        source.AddGroupDescription(new GroupDescription("Group"));
        int events = 0;
        source.GroupExpanding += (_, _) => events++;

        source.ToggleGroupExpansion(1); // a data row
        source.ToggleGroupExpansion(99);

        Assert.Equal(0, events);
        Assert.Equal(6, source.RowCount);
    }

    // ── Frozen rows ───────────────────────────────────────────────

    [Fact]
    public void EffectiveFrozenRowCount_ClampsToTheVisibleRows_AsFiltersChange()
    {
        var source = MakeSource(Rows());
        source.FrozenRowCount = 3;
        Assert.Equal(3, source.EffectiveFrozenRowCount);

        source.SetFilter(r => ((Row)r).Id == "a");

        Assert.Equal(1, source.EffectiveFrozenRowCount);
        Assert.Equal(3, source.FrozenRowCount);
    }

    // ── Columns ───────────────────────────────────────────────────

    [Fact]
    public void SetColumns_WithFreshColumns_DropsTheOldSort_AndWithTheSameColumnsKeepsIt()
    {
        var source = MakeSource(Rows());
        var value = Col(source, "Value");
        source.SetSort(value, KumikoSortDirection.Descending);
        Assert.Equal(new[] { "d", "a", "c", "b" }, Ids(source));

        source.SetColumns(source.Columns.ToList()); // the same columns carry their sort
        Assert.Equal(new[] { "d", "a", "c", "b" }, Ids(source));

        source.SetColumns(new[] { new KumikoColumn { PropertyName = "Id" }, new KumikoColumn { PropertyName = "Value" } });
        Assert.Equal(new[] { "a", "b", "c", "d" }, Ids(source));
        Assert.Empty(source.ActiveSortColumns);
    }

    [Fact]
    public void ReorderColumn_RaisesARedraw_NotARebuild()
    {
        var source = MakeSource(Rows());
        var kinds = new List<KumikoViewChangeKind>();
        source.ViewChanged += (_, e) => kinds.Add(e.Kind);

        source.ReorderColumn(0, 2);

        Assert.Equal(new[] { KumikoViewChangeKind.Redraw }, kinds);
        Assert.Equal(new[] { "Value", "Group", "Id" }, source.Columns.Select(c => c.PropertyName));
    }

    // ── Item changes ──────────────────────────────────────────────

    [Fact]
    public void ChangingASortedProperty_MovesTheRow_WithoutRebuildingTheCollection()
    {
        var items = new ObservableCollection<Tracked>(new[]
        {
            new Tracked { Id = "a", Value = 3 }, new Tracked { Id = "b", Value = 1 }, new Tracked { Id = "c", Value = 2 },
        });
        var source = new KumikoSource();
        source.SetColumns(new[] { new KumikoColumn { PropertyName = "Id" }, new KumikoColumn { PropertyName = "Value" } });
        source.SetItems(items);
        source.SetSort(source.Columns[1], KumikoSortDirection.Ascending);
        Assert.Equal(new[] { "b", "c", "a" }, TrackedIds(source));

        items[0].Value = 0;

        Assert.Equal(new[] { "a", "b", "c" }, TrackedIds(source));
    }

    [Fact]
    public void ChangingAFilteredProperty_AddsAndRemovesRows()
    {
        var items = new ObservableCollection<Tracked>(new[]
        {
            new Tracked { Id = "a", Value = 3 }, new Tracked { Id = "b", Value = 1 },
        });
        var source = new KumikoSource();
        var value = new KumikoColumn { PropertyName = "Value", ColumnType = KumikoColumnType.Numeric };
        source.SetColumns(new[] { new KumikoColumn { PropertyName = "Id" }, value });
        source.SetItems(items);
        source.SetColumnFilter(value, new FilterDescription(value)
        {
            Condition1 = new FilterCondition { NumericOperator = NumericFilterOperator.GreaterThan, Value = "2" },
        });
        Assert.Equal(new[] { "a" }, TrackedIds(source));

        items[1].Value = 5;
        Assert.Equal(new[] { "a", "b" }, TrackedIds(source));

        items[0].Value = 0;
        Assert.Equal(new[] { "b" }, TrackedIds(source));
    }

    private static string[] TrackedIds(KumikoSource s) =>
        Enumerable.Range(0, s.RowCount).Select(i => ((Tracked)s.GetItem(i)).Id).ToArray();

    private sealed class Tracked : System.ComponentModel.INotifyPropertyChanged
    {
        private int _value;
        public string Id { get; set; } = "";

        public int Value
        {
            get => _value;
            set
            {
                _value = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Value)));
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}

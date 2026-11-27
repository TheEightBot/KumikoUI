using System.Collections.ObjectModel;
using System.ComponentModel;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests;

/// <summary>
/// <see cref="KumikoSource.RemoveItems"/> (#328): removing items from the bound collection by reference, one view
/// rebuild for many items, under a sort, a filter and grouping, and the sources it must leave alone.
/// </summary>
public class KumikoSourceRemoveItemsTests
{
    private sealed class Row : INotifyPropertyChanged
    {
        private int _value;
        public string Id { get; set; } = "";
        public int Value
        {
            get => _value;
            set { _value = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value))); }
        }
        public string Group { get; set; } = "";
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private static Row[] Rows(int count = 6) =>
        Enumerable.Range(0, count).Select(i => new Row { Id = ((char)('a' + i)).ToString(), Value = i, Group = i % 2 == 0 ? "even" : "odd" }).ToArray();

    private static KumikoSource MakeSource(System.Collections.IEnumerable items)
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

    private static string[] Ids(KumikoSource s) =>
        Enumerable.Range(0, s.RowCount).Where(i => !s.IsNonDataRow(i)).Select(i => ((Row)s.GetItem(i)).Id).ToArray();

    private static IEnumerable<Row> Enumerate(IEnumerable<Row> rows)
    {
        foreach (var row in rows) yield return row;
    }

    private static IReadOnlyList<object> Pick(Row[] rows, params int[] indices) => indices.Select(i => (object)rows[i]).ToArray();

    [Fact]
    public void ObservableCollection_RemovesTheItems_WithOneViewChange()
    {
        var rows = Rows();
        var list = new ObservableCollection<Row>(rows);
        var source = MakeSource(list);
        int changes = 0;
        source.ViewChanged += (_, e) => { if (e.IsStructural) changes++; };

        var result = source.RemoveItems(Pick(rows, 1, 3, 4));

        Assert.Equal(KumikoRemoveResult.Removed, result);
        Assert.Equal(new[] { "a", "c", "f" }, list.Select(r => r.Id));
        Assert.Equal(new[] { "a", "c", "f" }, Ids(source));
        Assert.Equal(1, changes);
    }

    [Fact]
    public void List_RemovesTheItems_AndRebuildsTheView()
    {
        var rows = Rows();
        var list = rows.ToList();
        var source = MakeSource(list);
        int changes = 0;
        source.ViewChanged += (_, e) => { if (e.IsStructural) changes++; };

        Assert.Equal(KumikoRemoveResult.Removed, source.RemoveItems(Pick(rows, 0, 5)));

        Assert.Equal(new[] { "b", "c", "d", "e" }, list.Select(r => r.Id));
        Assert.Equal(new[] { "b", "c", "d", "e" }, Ids(source));
        Assert.Equal(1, changes);
    }

    [Fact]
    public void ManyItems_CauseOneRebuild_NotOnePerItem()
    {
        var rows = Rows(2000);
        var list = new ObservableCollection<Row>(rows);
        var source = MakeSource(list);
        int changes = 0;
        source.ViewChanged += (_, _) => changes++;

        source.RemoveItems(rows.Where((_, i) => i % 2 == 0).Cast<object>().ToArray());

        Assert.Equal(1000, source.RowCount);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void Array_IsNotSupported_AndNothingChanges()
    {
        var rows = Rows();
        var source = MakeSource(rows);

        Assert.Equal(KumikoRemoveResult.NotSupportedSource, source.RemoveItems(Pick(rows, 1)));

        Assert.Equal(6, source.RowCount);
    }

    [Fact]
    public void ReadOnlyList_IsNotSupported()
    {
        var rows = Rows();
        var source = MakeSource(new ReadOnlyCollection<Row>(rows));

        Assert.Equal(KumikoRemoveResult.NotSupportedSource, source.RemoveItems(Pick(rows, 1)));
    }

    [Fact]
    public void AnEnumerableThatIsNotAList_IsACopy_AndIsNotSupported()
    {
        var rows = Rows();
        var source = MakeSource(Enumerate(rows));

        Assert.Equal(KumikoRemoveResult.NotSupportedSource, source.RemoveItems(Pick(rows, 1)));

        Assert.Equal(6, source.RowCount);
    }

    [Fact]
    public void NothingToRemove_ForAnEmptyList_OnlyNulls_AndItemsThatAreNotInTheCollection()
    {
        var rows = Rows();
        var list = rows.ToList();
        var source = MakeSource(list);

        Assert.Equal(KumikoRemoveResult.NothingToRemove, source.RemoveItems(Array.Empty<object>()));
        Assert.Equal(KumikoRemoveResult.NothingToRemove, source.RemoveItems(new object[] { null! }));
        Assert.Equal(KumikoRemoveResult.NothingToRemove, source.RemoveItems(new object[] { new Row { Id = "x" } }));
        Assert.Equal(6, list.Count);
    }

    [Fact]
    public void NullEntries_AreIgnored_AndNullItemsOfTheCollectionAreUntouched()
    {
        var rows = Rows(3);
        var list = new List<Row?> { rows[0], null, rows[1], rows[2] };
        var source = MakeSource(list);

        var result = source.RemoveItems(new object?[] { null, rows[0], null }!);

        Assert.Equal(KumikoRemoveResult.Removed, result);
        Assert.Equal(3, list.Count);
        Assert.Contains(null, list);
        Assert.Equal(new[] { "b", "c" }, list.Where(r => r != null).Select(r => r!.Id));
    }

    [Fact]
    public void ItemsAreMatchedByReference_NotByEquality()
    {
        var rows = Rows(2);
        var list = rows.ToList();
        var source = MakeSource(list);
        var lookalike = new Row { Id = rows[0].Id, Value = rows[0].Value, Group = rows[0].Group };

        Assert.Equal(KumikoRemoveResult.NothingToRemove, source.RemoveItems(new object[] { lookalike }));
        Assert.Equal(2, list.Count);
    }

    [Fact]
    public void ASortedView_RemovesTheItems_NotTheRowsAtThoseIndices()
    {
        var rows = Rows();
        var list = new ObservableCollection<Row>(rows);
        var source = MakeSource(list);
        source.SetSort(source.Columns.Single(c => c.PropertyName == "Value"), KumikoSortDirection.Descending);
        Assert.Equal(new[] { "f", "e", "d", "c", "b", "a" }, Ids(source));
        var top = (Row)source.GetItem(0);

        source.RemoveItems(new object[] { top });

        Assert.Equal(new[] { "e", "d", "c", "b", "a" }, Ids(source));
        Assert.DoesNotContain(top, list);
    }

    [Fact]
    public void AFilteredView_CanRemoveRowsTheFilterHides()
    {
        var rows = Rows();
        var list = rows.ToList();
        var source = MakeSource(list);
        source.SetFilter(o => ((Row)o).Group == "even");
        Assert.Equal(new[] { "a", "c", "e" }, Ids(source));

        source.RemoveItems(new object[] { rows[1], rows[2] }); // b is hidden by the filter, c is shown

        Assert.Equal(new[] { "a", "e" }, Ids(source));
        Assert.Equal(new[] { "a", "d", "e", "f" }, list.Select(r => r.Id));
    }

    [Fact]
    public void AGroupedView_DropsAnEmptiedGroup()
    {
        var rows = Rows(4); // even: a c, odd: b d
        var list = new ObservableCollection<Row>(rows);
        var source = MakeSource(list);
        source.AddGroupDescription(new GroupDescription("Group"));
        int headers = Enumerable.Range(0, source.RowCount).Count(source.IsNonDataRow);
        Assert.Equal(2, headers);

        source.RemoveItems(Pick(rows, 1, 3)); // the whole odd group

        Assert.Equal(new[] { "a", "c" }, Ids(source));
        Assert.Equal(1, Enumerable.Range(0, source.RowCount).Count(source.IsNonDataRow));
    }

    [Fact]
    public void RemovedItems_StopRaisingViewChanges()
    {
        var rows = Rows(3);
        var list = new ObservableCollection<Row>(rows);
        var source = MakeSource(list);
        source.RemoveItems(Pick(rows, 0));
        int changes = 0;
        source.ViewChanged += (_, _) => changes++;

        rows[0].Value = 99; // untracked: the grid no longer listens to it

        Assert.Equal(0, changes);
    }

    [Fact]
    public void RemovedItems_OfAPlainList_StopRaisingViewChanges()
    {
        var rows = Rows(3);
        var source = MakeSource(rows.ToList());
        source.RemoveItems(Pick(rows, 0));
        int changes = 0;
        source.ViewChanged += (_, _) => changes++;

        rows[0].Value = 99;

        Assert.Equal(0, changes);
    }

    [Fact]
    public void ANullItemsList_Throws()
    {
        var source = MakeSource(Rows().ToList());

        Assert.Throws<ArgumentNullException>(() => source.RemoveItems(null!));
    }

    [Fact]
    public void RemovingEveryItem_LeavesAnEmptyView()
    {
        var rows = Rows(3);
        var list = new ObservableCollection<Row>(rows);
        var source = MakeSource(list);

        Assert.Equal(KumikoRemoveResult.Removed, source.RemoveItems(rows));

        Assert.Equal(0, source.RowCount);
        Assert.Empty(list);
    }
}

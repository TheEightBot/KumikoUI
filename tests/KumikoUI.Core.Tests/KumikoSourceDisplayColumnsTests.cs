using System.Collections.ObjectModel;
using System.Diagnostics;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests;

/// <summary>
/// <see cref="KumikoSource.DisplayColumns"/> and its index conversions, and the row-key index behind
/// <see cref="KumikoSelectionBinder.SelectedIndex"/>.
/// </summary>
public class KumikoSourceDisplayColumnsTests
{
    private static KumikoSource NewSource(out KumikoColumn[] c)
    {
        c = new[]
        {
            new KumikoColumn { PropertyName = "Name" },
            new KumikoColumn { PropertyName = "Age" },
            new KumikoColumn { PropertyName = "Department" },
            new KumikoColumn { PropertyName = "Id" },
            new KumikoColumn { PropertyName = "Salary" },
        };
        var source = new KumikoSource();
        source.SetColumns(c);
        return source;
    }

    [Fact]
    public void DisplayColumns_NoFreezeNoHidden_FollowsColumnsOrder()
    {
        var source = NewSource(out var c);
        Assert.Equal(c, source.DisplayColumns);
    }

    [Fact]
    public void DisplayColumns_InterleavedFreezeModesAndHidden_AreLeftScrollingRightWithoutHidden()
    {
        var source = NewSource(out var c);
        c[0].FreezeMode = ColumnFreezeMode.Right;
        c[1].IsVisible = false;
        c[3].FreezeMode = ColumnFreezeMode.Left;

        Assert.Equal(new[] { c[3], c[2], c[4], c[0] }, source.DisplayColumns);
        Assert.Equal(new[] { 3, 2, 4, 0 }, Enumerable.Range(0, 4).Select(source.ToColumnIndex));
        Assert.Equal(3, source.ToDisplayIndex(0));
        Assert.Equal(-1, source.ToDisplayIndex(1));
        Assert.Equal(0, source.ToDisplayIndex(3));
        Assert.Equal(2, source.GetDisplayIndex(c[4]));
        Assert.Equal(-1, source.GetDisplayIndex(c[1]));
        Assert.Equal(-1, source.GetDisplayIndex(new KumikoColumn()));
    }

    [Fact]
    public void SetDisplayIndex_MovesTheColumnOnScreen_AndRaisesColumnOrderChanged()
    {
        var source = NewSource(out var c);
        int changes = 0;
        source.ColumnOrderChanged += (_, _) => changes++;

        Assert.Equal(3, source.SetDisplayIndex(c[0], 3)); // right: after the column it displaces
        Assert.Equal(new[] { c[1], c[2], c[3], c[0], c[4] }, source.DisplayColumns);
        Assert.Equal(0, source.SetDisplayIndex(c[4], 0)); // left: before it
        Assert.Equal(new[] { c[4], c[1], c[2], c[3], c[0] }, source.DisplayColumns);
        Assert.Equal(2, changes);

        Assert.Equal(0, source.SetDisplayIndex(c[4], 0)); // already there
        Assert.Equal(2, changes);
    }

    [Fact]
    public void SetDisplayIndex_StaysInThePane_AndHiddenColumnsKeepTheirPlace()
    {
        var source = NewSource(out var c);
        c[0].FreezeMode = ColumnFreezeMode.Left;
        c[4].FreezeMode = ColumnFreezeMode.Right;
        c[2].IsVisible = false;
        // Display: c0 | c1 c3 | c4

        Assert.Equal(1, source.SetDisplayIndex(c[3], 0)); // clamped to the scrolling pane's start
        Assert.Equal(new[] { c[0], c[3], c[1], c[4] }, source.DisplayColumns);
        Assert.Equal(new[] { c[0], c[3], c[1], c[2], c[4] }, source.Columns);

        Assert.Equal(2, source.SetDisplayIndex(c[3], 10)); // clamped to the pane's end
        Assert.Equal(new[] { c[0], c[1], c[3], c[4] }, source.DisplayColumns);

        Assert.Equal(0, source.SetDisplayIndex(c[0], 3)); // the only left-frozen column
        Assert.Equal(-1, source.SetDisplayIndex(c[2], 0)); // hidden
        Assert.Throws<ArgumentException>(() => source.SetDisplayIndex(new KumikoColumn(), 0));
    }

    [Fact]
    public void ReorderColumn_RaisesColumnOrderChanged_OnlyWhenTheOrderChanges()
    {
        var source = NewSource(out _);
        int changes = 0;
        source.ColumnOrderChanged += (_, _) => changes++;

        source.ReorderColumn(1, 1);
        source.ReorderColumn(-1, 2);
        source.SetColumns(source.Columns.Reverse().ToList());
        Assert.Equal(0, changes);

        source.ReorderColumn(0, 2);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void ToColumnIndexAndToDisplayIndex_OutOfRange_ReturnMinusOne()
    {
        var source = NewSource(out _);
        Assert.Equal(-1, source.ToColumnIndex(-1));
        Assert.Equal(-1, source.ToColumnIndex(5));
        Assert.Equal(-1, source.ToDisplayIndex(-1));
        Assert.Equal(-1, source.ToDisplayIndex(5));
    }

    [Fact]
    public void DisplayColumns_FollowLaterChanges_AndBumpTheLayoutVersion()
    {
        var source = NewSource(out var c);
        int version = source.ColumnLayoutVersion;
        Assert.Equal(version, source.ColumnLayoutVersion); // reading changes nothing

        c[2].IsVisible = false;
        Assert.Equal(4, source.DisplayColumns.Count);
        Assert.NotEqual(version, source.ColumnLayoutVersion);

        version = source.ColumnLayoutVersion;
        source.ReorderColumn(0, 4); // Age, Dept (hidden), Id, Salary, Name
        Assert.Equal(new[] { c[1], c[3], c[4], c[0] }, source.DisplayColumns);
        Assert.NotEqual(version, source.ColumnLayoutVersion);
    }

    [Fact]
    public void DisplayColumns_AreStable_BetweenReads()
    {
        var source = NewSource(out _);
        var first = source.DisplayColumns;
        Assert.Same(first, source.DisplayColumns);
    }

    // ── Row-key index ──────────────────────────────────────────────

    private sealed class Row { public int Id { get; set; } }

    private static KumikoSource RowSource(IEnumerable<Row> items)
    {
        var source = new KumikoSource();
        source.SetColumns(new[] { new KumikoColumn { PropertyName = "Id" } });
        source.SetItems(items);
        return source;
    }

    [Fact]
    public void IndexOfRowKey_FindsItemsByReference_AndMissesOthers()
    {
        var items = Enumerable.Range(0, 10).Select(i => new Row { Id = i }).ToList();
        var source = RowSource(items);

        Assert.Equal(7, source.IndexOfRowKey(items[7]));
        Assert.Equal(-1, source.IndexOfRowKey(new Row { Id = 7 })); // equal data, different instance
    }

    [Fact]
    public void IndexOfRowKey_FollowsSortAndFilter()
    {
        var items = Enumerable.Range(0, 5).Select(i => new Row { Id = i }).ToList();
        var source = RowSource(items);
        Assert.Equal(1, source.IndexOfRowKey(items[1]));

        source.SetSort(source.Columns[0], KumikoSortDirection.Descending);
        Assert.Equal(3, source.IndexOfRowKey(items[1]));

        source.SetFilter(o => ((Row)o).Id != 4);
        Assert.Equal(2, source.IndexOfRowKey(items[1]));
        Assert.Equal(-1, source.IndexOfRowKey(items[4]));
    }

    [Fact]
    public void IndexOfRowKey_FollowsIncrementalInsertAndRemove()
    {
        var items = new ObservableCollection<Row>(Enumerable.Range(0, 5).Select(i => new Row { Id = i }));
        var source = RowSource(items);
        Assert.Equal(4, source.IndexOfRowKey(items[4])); // builds the index

        var added = new Row { Id = 99 };
        items.Insert(0, added);
        Assert.Equal(0, source.IndexOfRowKey(added));
        Assert.Equal(5, source.IndexOfRowKey(items[5]));

        var removed = items[2];
        items.RemoveAt(2);
        Assert.Equal(-1, source.IndexOfRowKey(removed));
        Assert.Equal(2, source.IndexOfRowKey(items[2]));

        items.Move(0, 3);
        Assert.Equal(3, source.IndexOfRowKey(added));
    }

    [Fact]
    public void IndexOfRowKey_FindsGroupRowsByValue()
    {
        var items = Enumerable.Range(0, 4).Select(i => new Row { Id = i }).ToList();
        var source = RowSource(items);
        source.AddGroupDescription(new GroupDescription("Id"));

        var key = source.GetRowKey(0)!; // a group header
        Assert.IsType<GroupRowKey>(key);
        Assert.Equal(0, source.IndexOfRowKey(new GroupRowKey(((GroupRowKey)key).GroupPath, -1)));
        Assert.Equal(1, source.IndexOfRowKey(items[0]));
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void IndexOfRowKey_On100KRows_IsConstantTimePerLookup()
    {
        var items = Enumerable.Range(0, 100_000).Select(i => new Row { Id = i }).ToList();
        var source = RowSource(items);

        Assert.Equal(99_999, source.IndexOfRowKey(items[99_999])); // builds the index once

        // 100K lookups, the last rows first: a linear scan would read ~5 * 10^9 row keys.
        var sw = Stopwatch.StartNew();
        for (int i = items.Count - 1; i >= 0; i--)
            Assert.Equal(i, source.IndexOfRowKey(items[i]));
        sw.Stop();

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2), $"100K lookups took {sw.Elapsed}");
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void Selection_On100KRows_StaysFastAcrossSelectionChanges()
    {
        var items = Enumerable.Range(0, 100_000).Select(i => new Row { Id = i }).ToList();
        var source = RowSource(items);
        var model = new KumikoSelectionModel(source) { Mode = KumikoSelectionMode.Single };

        var sw = Stopwatch.StartNew();
        for (int i = 99_999; i >= 90_000; i--)
        {
            model.Select(items[i]);
            Assert.Equal(i, source.IndexOfRowKey(items[i]));
        }
        sw.Stop();

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2), $"10K selections took {sw.Elapsed}");
    }
}

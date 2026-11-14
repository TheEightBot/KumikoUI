using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using KumikoUI.Core.Models;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests;

/// <summary>
/// The view pipeline contract (#69): changes are UI-thread-only, <see cref="KumikoSource.BeginUpdate"/> batches
/// coalesce rebuilds (collection changes included), and table summaries follow an item's PropertyChanged without
/// rebuilding the view.
/// </summary>
public class ViewPipelineTests
{
    public sealed class Item : INotifyPropertyChanged
    {
        private int _quantity;
        private decimal _price;
        private double _ratio;
        private string _city = "";

        public string Name { get; init; } = "";
        public int Quantity { get => _quantity; set => Set(ref _quantity, value); }
        public decimal Price { get => _price; set => Set(ref _price, value); }
        public double Ratio { get => _ratio; set => Set(ref _ratio, value); }
        public string City { get => _city; set => Set(ref _city, value); }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    private static Item NewItem(int i) => new()
    {
        Name = $"Item {i}",
        Quantity = i % 50,
        Price = 1.25m * (i % 17),
        Ratio = i / 3.0,
        City = (i % 3) switch { 0 => "Oslo", 1 => "Rome", _ => "Lima" },
    };

    private static ObservableCollection<Item> NewItems(int count) =>
        new(Enumerable.Range(0, count).Select(NewItem));

    private static KumikoColumn[] NewColumns() => new[]
    {
        new KumikoColumn { Header = "Name", PropertyName = nameof(Item.Name) },
        new KumikoColumn { Header = "Quantity", PropertyName = nameof(Item.Quantity), ColumnType = KumikoColumnType.Numeric },
        new KumikoColumn { Header = "Price", PropertyName = nameof(Item.Price), ColumnType = KumikoColumnType.Numeric },
        new KumikoColumn { Header = "Ratio", PropertyName = nameof(Item.Ratio), ColumnType = KumikoColumnType.Numeric },
        new KumikoColumn { Header = "City", PropertyName = nameof(Item.City) },
    };

    private static KumikoSource NewSource(IEnumerable<Item> items, out KumikoColumn[] columns)
    {
        columns = NewColumns();
        var source = new KumikoSource();
        source.SetColumns(columns);
        source.SetItems(items);
        return source;
    }

    private static TableSummaryRow Totals(params SummaryColumnDescription[] columns)
    {
        var row = new TableSummaryRow("Totals");
        row.Columns.AddRange(columns);
        return row;
    }

    private static object? Raw(KumikoSource source, string property, SummaryType type) =>
        source.ComputedBottomSummaries[0].GetRawValue(property, type);

    // ── Threading contract ────────────────────────────────────────

    [Fact]
    public async Task BackgroundAdd_FromTaskRun_ThrowsTheDocumentedException_AndTheViewResyncsOnTheUiThread()
    {
        using var ui = new TestUiThread();
        var items = NewItems(3);
        var source = ui.Invoke(() => NewSource(items, out _));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Task.Run(() => items.Add(NewItem(3))));

        Assert.Equal(KumikoSource.WrongThreadMessage, ex.Message);
        // The add reached the collection before the source saw it; the source re-reads it on its own thread.
        Assert.Equal(4, ui.Invoke(() => source.RowCount));
        Assert.Equal("Item 3", ui.Invoke(() => ((Item)source.GetItem(3)).Name));
    }

    [Fact]
    public async Task BackgroundPropertyChange_Throws_AndTheSummaryIsRecomputedOnTheUiThread()
    {
        using var ui = new TestUiThread();
        var items = NewItems(3);
        var source = ui.Invoke(() => NewSource(items, out _));
        ui.Invoke(() => source.AddTableSummaryRow(Totals(new SummaryColumnDescription(nameof(Item.Quantity), SummaryType.Sum))));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Task.Run(() => items[0].Quantity = 100));
        Assert.Equal(KumikoSource.WrongThreadMessage, ex.Message);

        Assert.Equal(100d + 1 + 2, ui.Invoke(() => Raw(source, nameof(Item.Quantity), SummaryType.Sum)));
    }

    [Fact]
    public void BackgroundCallsToTheSource_Throw()
    {
        using var ui = new TestUiThread();
        var source = ui.Invoke(() => NewSource(NewItems(3), out _));
        var columns = source.Columns;

        Assert.Throws<InvalidOperationException>(() => source.ToggleSort(columns[1]));
        Assert.Equal(KumikoSortDirection.None, columns[1].SortDirection); // rejected before anything changed
        Assert.Throws<InvalidOperationException>(() => source.BeginUpdate());
        Assert.Throws<InvalidOperationException>(() => source.Refresh());
        Assert.Throws<InvalidOperationException>(() => source.AddGroupDescription(new GroupDescription(nameof(Item.City))));
        Assert.Empty(source.GroupDescriptions);

        ui.Invoke(() => source.ToggleSort(columns[1])); // the source's own thread may
        Assert.Equal(columns[1].FirstSortDirection, columns[1].SortDirection);
    }

    [Fact]
    public void WithoutASynchronizationContext_RefreshResyncsAfterARejectedChange()
    {
        using var ui = new TestUiThread(installContext: false);
        var items = NewItems(3);
        var source = ui.Invoke(() => NewSource(items, out _));

        Exception? caught = null;
        var worker = new Thread(() =>
        {
            try { items.RemoveAt(0); }
            catch (Exception ex) { caught = ex; }
        });
        worker.Start();
        worker.Join();
        Assert.IsType<InvalidOperationException>(caught);

        Assert.Equal(2, ui.Invoke(() => { source.Refresh(); return source.RowCount; }));
        Assert.Equal("Item 1", ui.Invoke(() => ((Item)source.GetItem(0)).Name));
    }

    [Fact]
    public void ChangesAfterSetItemsOnTheSameThread_AreAccepted_AndANewSetItemsMovesTheSource()
    {
        var items = NewItems(3);
        var source = NewSource(items, out _);
        items.Add(NewItem(3));
        items[0].Quantity = 7;
        Assert.Equal(4, source.RowCount);

        // SetItems (or Attach) makes the calling thread the source's thread.
        using var ui = new TestUiThread(installContext: false);
        ui.Invoke(() => source.SetItems(items));
        Assert.Throws<InvalidOperationException>(() => items.Add(NewItem(4)));
    }

    // ── Batching ──────────────────────────────────────────────────

    [Fact]
    public void BeginUpdate_ReturnsAScope_ThatEndsTheBatch()
    {
        var source = NewSource(NewItems(5), out var columns);
        var kinds = new List<KumikoViewChangeKind>();
        source.ViewChanged += (_, e) => kinds.Add(e.Kind);

        using (source.BeginUpdate())
        {
            source.SetSort(columns[1], KumikoSortDirection.Descending);
            source.SetFilter(o => ((Item)o).Quantity > 1);
            Assert.Empty(kinds);
        }

        Assert.Equal(new[] { KumikoViewChangeKind.Rebuild }, kinds);
        Assert.Equal(3, source.RowCount);
        Assert.Throws<InvalidOperationException>(source.EndUpdate); // the scope ended the batch
        default(KumikoUpdateScope).Dispose(); // a default scope does nothing
    }

    [Fact]
    public void CollectionChangesInABatch_UnderASort_RebuildOnceAtTheEnd()
    {
        int reads = 0;
        var items = NewItems(200);
        var source = NewSource(items, out var columns);
        columns[1].ValueAccessor = o => { reads++; return ((Item)o).Quantity; };
        source.SetSort(columns[1], KumikoSortDirection.Ascending);
        var events = new List<KumikoViewChangedEventArgs>();
        source.ViewChanged += (_, e) => events.Add(e);

        reads = 0;
        source.BeginUpdate();
        for (int i = 0; i < 50; i++)
            items.Add(NewItem(1000 + i));
        items.RemoveAt(0);
        Assert.Equal(0, reads); // nothing re-sorted per change
        source.EndUpdate();

        Assert.True(reads > 0);
        var e = Assert.Single(events);
        Assert.Equal(KumikoViewChangeKind.Rebuild, e.Kind);
        Assert.Equal(200, e.OldRowCount);
        Assert.Equal(249, source.RowCount);
        var quantities = Enumerable.Range(0, source.RowCount).Select(r => ((Item)source.GetItem(r)).Quantity).ToList();
        Assert.Equal(quantities.Order().ToList(), quantities);
    }

    [Fact]
    public void ReadingRowsInsideABatch_RebuildsAStaleViewFirst()
    {
        var items = NewItems(10);
        var source = NewSource(items, out var columns);
        source.SetSort(columns[1], KumikoSortDirection.Descending);
        int events = 0;
        source.ViewChanged += (_, _) => events++;

        source.BeginUpdate();
        items.Clear();
        items.Add(NewItem(1));
        items.Add(NewItem(49));
        Assert.Equal(2, source.RowCount);
        Assert.Equal(49, ((Item)source.GetItem(0)).Quantity);
        items.Add(NewItem(20));
        Assert.Equal(20, ((Item)source.GetItem(1)).Quantity);
        Assert.Equal(0, events);
        source.EndUpdate();

        Assert.Equal(1, events);
        Assert.Equal(3, source.RowCount);
    }

    // ── Incremental table summaries ───────────────────────────────

    [Fact]
    public void SumAverageAndCount_FollowAPropertyChange_WithoutARebuild()
    {
        int reads = 0;
        var items = NewItems(100);
        var source = NewSource(items, out var columns);
        columns[1].ValueAccessor = o => { reads++; return ((Item)o).Quantity; };
        source.AddTableSummaryRow(Totals(
            new SummaryColumnDescription(nameof(Item.Quantity), SummaryType.Sum),
            new SummaryColumnDescription(nameof(Item.Quantity), SummaryType.Average),
            new SummaryColumnDescription(nameof(Item.Quantity), SummaryType.Count)));
        var kinds = new List<KumikoViewChangeKind>();
        source.ViewChanged += (_, e) => kinds.Add(e.Kind);
        double sum = items.Sum(i => i.Quantity);

        reads = 0;
        items[10].Quantity += 1000;
        items[20].Quantity = 0;

        Assert.Equal(2, reads); // the changed items only
        Assert.Equal(new[] { KumikoViewChangeKind.Redraw, KumikoViewChangeKind.Redraw }, kinds);
        double expected = sum + 1000 - 20;
        Assert.Equal(expected, Raw(source, nameof(Item.Quantity), SummaryType.Sum));
        Assert.Equal(expected / 100, Raw(source, nameof(Item.Quantity), SummaryType.Average));
        Assert.Equal(100, Raw(source, nameof(Item.Quantity), SummaryType.Count));
        Assert.Contains("3430", source.ComputedBottomSummaries[0].Values[nameof(Item.Quantity)]);
    }

    [Fact]
    public void DecimalSum_StaysExact_AcrossInPlaceUpdates()
    {
        var items = NewItems(50);
        var source = NewSource(items, out _);
        source.AddTableSummaryRow(Totals(new SummaryColumnDescription(nameof(Item.Price), SummaryType.Sum)));

        for (int i = 0; i < 50; i++)
            items[i].Price += 0.01m;

        var sum = Assert.IsType<decimal>(Raw(source, nameof(Item.Price), SummaryType.Sum));
        Assert.Equal(items.Sum(i => i.Price), sum);
    }

    [Fact]
    public void AChangeOfAFilteredOutItem_LeavesTheSummaryAlone()
    {
        var items = NewItems(30);
        var source = NewSource(items, out var columns);
        source.SetColumnFilter(columns[4], new FilterDescription(columns[4]) { SelectedValues = new HashSet<string> { "Oslo" } });
        source.AddTableSummaryRow(Totals(new SummaryColumnDescription(nameof(Item.Quantity), SummaryType.Sum)));
        var before = Raw(source, nameof(Item.Quantity), SummaryType.Sum);

        items.First(i => i.City != "Oslo").Quantity += 500;
        Assert.Equal(before, Raw(source, nameof(Item.Quantity), SummaryType.Sum));

        items.First(i => i.City == "Oslo").Quantity += 500;
        Assert.Equal((double)before! + 500, Raw(source, nameof(Item.Quantity), SummaryType.Sum));
    }

    [Fact]
    public void MinAndMax_AreRecomputedOverTheVisibleRows_WithoutARebuild_OncePerBatch()
    {
        int reads = 0;
        var items = NewItems(40);
        var source = NewSource(items, out var columns);
        source.SetSort(columns[0], KumikoSortDirection.Descending);
        columns[3].ValueAccessor = o => { reads++; return ((Item)o).Ratio; };
        source.AddTableSummaryRow(Totals(
            new SummaryColumnDescription(nameof(Item.Ratio), SummaryType.Max),
            new SummaryColumnDescription(nameof(Item.Ratio), SummaryType.Min)));
        var kinds = new List<KumikoViewChangeKind>();
        source.ViewChanged += (_, e) => kinds.Add(e.Kind);
        var order = Enumerable.Range(0, 40).Select(r => source.GetItem(r)).ToList();

        reads = 0;
        items[5].Ratio = 1000;
        Assert.Equal(40, reads); // one pass over the visible rows
        Assert.Equal(1000d, Raw(source, nameof(Item.Ratio), SummaryType.Max));

        reads = 0;
        using (source.BeginUpdate())
        {
            items[6].Ratio = -1;
            items[7].Ratio = -2;
            items[8].Ratio = 2000;
            Assert.Equal(0, reads);
        }
        Assert.Equal(40, reads);
        Assert.Equal(2000d, Raw(source, nameof(Item.Ratio), SummaryType.Max));
        Assert.Equal(-2d, Raw(source, nameof(Item.Ratio), SummaryType.Min));
        Assert.Equal(new[] { KumikoViewChangeKind.Redraw, KumikoViewChangeKind.Redraw }, kinds);
        Assert.Equal(order, Enumerable.Range(0, 40).Select(r => source.GetItem(r)).ToList()); // nothing re-sorted
    }

    [Fact]
    public void AnItemListedTwice_FallsBackToARecompute()
    {
        var shared = NewItem(1);
        var items = new ObservableCollection<Item> { shared, NewItem(2), shared };
        var source = NewSource(items, out _);
        source.AddTableSummaryRow(Totals(new SummaryColumnDescription(nameof(Item.Quantity), SummaryType.Sum)));

        shared.Quantity = 10;

        Assert.Equal(10d + 2 + 10, Raw(source, nameof(Item.Quantity), SummaryType.Sum));
    }

    [Fact]
    public void InPlaceUpdates_MatchAFullRecompute()
    {
        var random = new Random(69);
        var items = NewItems(500);
        var source = NewSource(items, out var columns);
        source.SetColumnFilter(columns[4], new FilterDescription(columns[4]) { SelectedValues = new HashSet<string> { "Oslo", "Lima" } });
        source.AddTableSummaryRow(Totals(
            new SummaryColumnDescription(nameof(Item.Quantity), SummaryType.Sum),
            new SummaryColumnDescription(nameof(Item.Price), SummaryType.Average),
            new SummaryColumnDescription(nameof(Item.Ratio), SummaryType.Sum)));

        for (int i = 0; i < 300; i++)
        {
            var item = items[random.Next(items.Count)];
            switch (i % 3)
            {
                case 0: item.Quantity = random.Next(-100, 100); break;
                case 1: item.Price = random.Next(0, 10_000) / 100m; break;
                default: item.Ratio = random.NextDouble() * 1000; break;
            }
        }

        var incremental = source.ComputedBottomSummaries[0];
        source.Refresh();
        var fresh = source.ComputedBottomSummaries[0];
        Assert.Equal(fresh.GetRawValue(nameof(Item.Quantity), SummaryType.Sum), incremental.GetRawValue(nameof(Item.Quantity), SummaryType.Sum));
        Assert.Equal(fresh.GetRawValue(nameof(Item.Price), SummaryType.Average), incremental.GetRawValue(nameof(Item.Price), SummaryType.Average));
        Assert.Equal((double)fresh.GetRawValue(nameof(Item.Ratio), SummaryType.Sum)!,
            (double)incremental.GetRawValue(nameof(Item.Ratio), SummaryType.Sum)!, 6);
    }

    [Fact]
    public void AGroupSummaryOfTheProperty_IsUpdatedInPlace_WithoutRebuildingTheGroups()
    {
        var items = NewItems(9);
        var source = NewSource(items, out _);
        source.AddGroupDescription(new GroupDescription(nameof(Item.City)));
        source.AddGroupSummaryRow(new GroupSummaryRow { Name = "G", Columns = { new SummaryColumnDescription(nameof(Item.Quantity), SummaryType.Sum) } });
        source.AddTableSummaryRow(Totals(new SummaryColumnDescription(nameof(Item.Quantity), SummaryType.Sum)));
        var kinds = new List<KumikoViewChangeKind>();
        source.ViewChanged += (_, e) => kinds.Add(e.Kind);

        items[0].Quantity = 100; // Oslo

        // #254: Sum, Average and Count group summaries follow the change in place, like table summaries (#69).
        Assert.Equal(new[] { KumikoViewChangeKind.Redraw }, kinds);
        Assert.Equal(items.Sum(i => (double)i.Quantity), Raw(source, nameof(Item.Quantity), SummaryType.Sum));
        var oslo = source.GetGroupSummaries($"{nameof(Item.City)}=Oslo")!;
        Assert.Equal(items.Where(i => i.City == "Oslo").Sum(i => (double)i.Quantity), oslo[0].GetRawValue(nameof(Item.Quantity), SummaryType.Sum));
    }

    [Fact]
    public void AGroupMaxSummaryOfTheProperty_StillRebuildsTheGroups()
    {
        var items = NewItems(9);
        var source = NewSource(items, out _);
        source.AddGroupDescription(new GroupDescription(nameof(Item.City)));
        source.AddGroupSummaryRow(new GroupSummaryRow { Name = "G", Columns = { new SummaryColumnDescription(nameof(Item.Quantity), SummaryType.Max) } });
        var kinds = new List<KumikoViewChangeKind>();
        source.ViewChanged += (_, e) => kinds.Add(e.Kind);

        items[0].Quantity = 100; // Oslo

        Assert.Equal(new[] { KumikoViewChangeKind.Rebuild }, kinds);
        var oslo = source.GetGroupSummaries($"{nameof(Item.City)}=Oslo")!;
        Assert.Equal(100, oslo[0].GetRawValue(nameof(Item.Quantity), SummaryType.Max));
    }

    [Fact]
    public void AddingAndRemovingItems_KeepsInPlaceUpdatesCorrect()
    {
        var items = NewItems(20);
        var source = NewSource(items, out _);
        source.AddTableSummaryRow(Totals(new SummaryColumnDescription(nameof(Item.Quantity), SummaryType.Sum)));

        items.RemoveAt(3);
        items.Insert(0, NewItem(77));
        items[5].Quantity = 1234;
        items[0].Quantity = 1;

        Assert.Equal(items.Sum(i => (double)i.Quantity), Raw(source, nameof(Item.Quantity), SummaryType.Sum));
    }

    // ── Acceptance: live updates on a large grid ──────────────────

    [Fact]
    [Trait("Category", "Performance")]
    public void HundredRandomPropertyChanges_On100KRows_WithASum_BlockUnder16Ms()
    {
        // Warm up the code paths (JIT) on a small source first.
        var warmItems = NewItems(100);
        var warm = NewSource(warmItems, out _);
        warm.AddTableSummaryRow(Totals(new SummaryColumnDescription(nameof(Item.Quantity), SummaryType.Sum)));
        warm.ViewChanged += (_, _) => { };
        for (int i = 0; i < 10; i++) warmItems[i].Quantity += 1;

        var items = NewItems(100_000);
        var source = NewSource(items, out _);
        source.AddTableSummaryRow(Totals(new SummaryColumnDescription(nameof(Item.Quantity), SummaryType.Sum)));
        source.ViewChanged += (_, _) => { }; // a grid listens
        var random = new Random(16);
        var targets = Enumerable.Range(0, 100).Select(_ => items[random.Next(items.Count)]).ToArray();

        // The best of three rounds: a shared CI runner can stall any single one.
        double fastest = double.MaxValue;
        for (int round = 0; round < 3; round++)
        {
            var watch = Stopwatch.StartNew();
            foreach (var item in targets)
                item.Quantity += 3;
            watch.Stop();
            fastest = Math.Min(fastest, watch.Elapsed.TotalMilliseconds);
        }

        Assert.Equal(items.Sum(i => (double)i.Quantity), Raw(source, nameof(Item.Quantity), SummaryType.Sum));
        Assert.True(fastest < 16, $"100 property changes took {fastest:F2} ms on the calling thread (budget 16 ms)");
    }
}

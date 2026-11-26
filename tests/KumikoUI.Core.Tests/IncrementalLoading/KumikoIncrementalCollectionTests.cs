using System.Collections.Specialized;
using KumikoUI.Core.Data;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests.IncrementalLoading;

/// <summary>
/// <see cref="KumikoIncrementalCollection{T}"/> (spec 104 §8.2) and the batched appends it relies on (§8.3): a batch is
/// one notification and one view change.
/// </summary>
public class KumikoIncrementalCollectionTests
{
    private static KumikoIncrementalCollection<LoadItem> Pages(int total, List<(int Start, int Count)>? fetches = null) =>
        new((start, count, _) =>
        {
            fetches?.Add((start, count));
            var page = new List<LoadItem>();
            for (int i = start; i < Math.Min(total, start + count); i++)
                page.Add(new LoadItem { Name = $"R{i:D3}", Value = i });
            return Task.FromResult<IReadOnlyList<LoadItem>>(page);
        });

    [Fact]
    public void AddRange_RaisesOneAddNotification()
    {
        var items = Pages(0);
        var events = new List<NotifyCollectionChangedEventArgs>();
        items.CollectionChanged += (_, e) => events.Add(e);

        items.AddRange(new[] { new LoadItem(), new LoadItem(), new LoadItem() });

        var e = Assert.Single(events);
        Assert.Equal(NotifyCollectionChangedAction.Add, e.Action);
        Assert.Equal(3, e.NewItems!.Count);
        Assert.Equal(0, e.NewStartingIndex);
        Assert.Equal(3, items.Count);
    }

    [Fact]
    public void AddRange_OfNothing_RaisesNothing()
    {
        var items = Pages(0);
        int raised = 0;
        items.CollectionChanged += (_, _) => raised++;

        items.AddRange(Array.Empty<LoadItem>());

        Assert.Equal(0, raised);
    }

    [Fact]
    public async Task LoadMoreItemsAsync_FetchesAfterTheCurrentItems()
    {
        var fetches = new List<(int, int)>();
        var items = Pages(100, fetches);

        Assert.Equal(20, await items.LoadMoreItemsAsync(20, CancellationToken.None));
        Assert.Equal(20, await items.LoadMoreItemsAsync(20, CancellationToken.None));

        Assert.Equal(new[] { (0, 20), (20, 20) }, fetches);
        Assert.Equal(40, items.Count);
        Assert.True(items.HasMoreItems);
    }

    [Fact]
    public async Task AShortPage_EndsTheData()
    {
        var items = Pages(25);
        var changed = new List<string?>();
        ((System.ComponentModel.INotifyPropertyChanged)items).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        await items.LoadMoreItemsAsync(20, CancellationToken.None);
        await items.LoadMoreItemsAsync(20, CancellationToken.None);

        Assert.Equal(25, items.Count);
        Assert.False(items.HasMoreItems);
        Assert.Contains(nameof(items.HasMoreItems), changed);
    }

    [Fact]
    public async Task ACanceledBatch_AppendsNothing()
    {
        using var cts = new CancellationTokenSource();
        var items = new KumikoIncrementalCollection<LoadItem>(async (_, count, _) =>
        {
            await Task.Yield();
            cts.Cancel();
            return new[] { new LoadItem() };
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => items.LoadMoreItemsAsync(20, cts.Token));
        Assert.Empty(items);
    }

    [Fact]
    public async Task RefreshAsync_ClearsAndRestarts()
    {
        var items = Pages(10);
        await items.LoadMoreItemsAsync(20, CancellationToken.None);
        Assert.False(items.HasMoreItems);
        var actions = new List<NotifyCollectionChangedAction>();
        items.CollectionChanged += (_, e) => actions.Add(e.Action);

        await items.RefreshAsync();

        Assert.Empty(items);
        Assert.True(items.HasMoreItems);
        Assert.Equal(new[] { NotifyCollectionChangedAction.Reset }, actions);
    }

    [Fact]
    public void TheConstructor_RejectsANullFetch() =>
        Assert.Throws<ArgumentNullException>(() => new KumikoIncrementalCollection<LoadItem>(null!));

    // ── In a grid ──

    [Fact]
    public void ABatch_IsOneInsertAtTheEnd()
    {
        var items = Pages(100);
        var source = new KumikoSource();
        source.SetColumns(new[] { new KumikoColumn { PropertyName = "Name" } });
        source.SetItems(items);
        items.AddRange(new[] { new LoadItem(), new LoadItem() });
        var changes = new List<KumikoViewChangedEventArgs>();
        source.ViewChanged += (_, e) => changes.Add(e);

        _ = source.IncrementalLoading.LoadMoreAsync();

        var change = Assert.Single(changes, c => c.IsStructural);
        Assert.Equal(KumikoViewChangeKind.Insert, change.Kind);
        Assert.Equal(2, change.ViewIndex);
        Assert.Equal(20, change.Count);
        Assert.Equal(22, source.RowCount);
    }

    [Fact]
    public void ABatch_UnderASort_IsOneRebuild()
    {
        var items = Pages(100);
        var name = new KumikoColumn { PropertyName = "Name" };
        var source = new KumikoSource();
        source.SetColumns(new[] { name });
        source.SetItems(items);
        source.SetSort(name, KumikoSortDirection.Descending);
        var changes = new List<KumikoViewChangedEventArgs>();
        source.ViewChanged += (_, e) => changes.Add(e);

        _ = source.IncrementalLoading.LoadMoreAsync();

        var change = Assert.Single(changes, c => c.IsStructural);
        Assert.Equal(KumikoViewChangeKind.Rebuild, change.Kind);
        Assert.Equal("R019", ((LoadItem)source.GetItem(0)).Name);
    }

    [Fact]
    public void ABatch_WithFilteredOutItems_InsertsOnlyTheVisibleOnes()
    {
        var items = Pages(100);
        var source = new KumikoSource();
        source.SetColumns(new[] { new KumikoColumn { PropertyName = "Name" } });
        source.SetItems(items);
        source.SetFilter(item => ((LoadItem)item).Value % 2 == 0);
        var changes = new List<KumikoViewChangedEventArgs>();
        source.ViewChanged += (_, e) => changes.Add(e);

        _ = source.IncrementalLoading.LoadMoreAsync();

        var change = Assert.Single(changes, c => c.IsStructural);
        Assert.Equal(KumikoViewChangeKind.Insert, change.Kind);
        Assert.Equal(10, change.Count);
        Assert.Equal(10, source.RowCount);
    }

    [Fact]
    public void ABatch_KeepsTheSelection()
    {
        var items = Pages(100);
        var source = new KumikoSource();
        source.SetColumns(new[] { new KumikoColumn { PropertyName = "Name" } });
        source.SetItems(items);
        items.AddRange(new[] { new LoadItem { Name = "first" } });
        var selection = new KumikoSelectionModel(source);
        selection.Select(items[0]);

        _ = source.IncrementalLoading.LoadMoreAsync();

        Assert.Same(items[0], Assert.Single(selection.SelectedItems));
    }
}

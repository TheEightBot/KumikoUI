using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests;

/// <summary>Dispose/Detach/Attach of KumikoSource and the O(1) item tracking behind them.</summary>
public class KumikoSourceLifecycleTests
{
    private sealed class Row : INotifyPropertyChanged
    {
        private int _value;
        public event PropertyChangedEventHandler? PropertyChanged;
        public int Subscribers => PropertyChanged?.GetInvocationList().Length ?? 0;
        public int Value
        {
            get => _value;
            set { _value = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value))); }
        }
    }

    private sealed class CountingList : List<Row>, INotifyCollectionChanged
    {
        private NotifyCollectionChangedEventHandler? _handlers;
        public int Subscribers => _handlers?.GetInvocationList().Length ?? 0;
        public event NotifyCollectionChangedEventHandler? CollectionChanged
        {
            add => _handlers += value;
            remove => _handlers -= value;
        }
        public void AddAndRaise(Row r)
        {
            Add(r);
            _handlers?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, r, Count - 1));
        }
        public void RaiseRemove(Row r) => _handlers?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, r, Count));
        public void RaiseReset() => _handlers?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    private static CountingList MakeList(int n)
    {
        var l = new CountingList();
        for (int i = 0; i < n; i++) l.Add(new Row { Value = i });
        return l;
    }

    [Fact]
    public void Dispose_UnsubscribesCollectionAndItems_AndLaterChangesAreInert()
    {
        var list = MakeList(3);
        using var src = new KumikoSource();
        int events = 0;
        src.ViewChanged += (_, _) => events++;
        src.SetItems(list);
        Assert.Equal(1, list.Subscribers);
        Assert.All(list, r => Assert.Equal(1, r.Subscribers));

        events = 0;
        src.Dispose();

        Assert.Equal(0, list.Subscribers);
        Assert.All(list, r => Assert.Equal(0, r.Subscribers));
        Assert.Equal(0, src.RowCount);

        list.AddAndRaise(new Row());
        list[0].Value = 99;
        list.RaiseReset();
        Assert.Equal(0, events);
        Assert.Equal(0, src.RowCount);
    }

    [Fact]
    public void Dispose_IsIdempotent_AndSetItemsAfterwardsIsIgnored()
    {
        var list = MakeList(2);
        using var src = new KumikoSource();
        src.Dispose();
        src.Dispose();
        src.SetItems(list);
        Assert.Equal(0, list.Subscribers);
        Assert.Equal(0, src.RowCount);
    }

    [Fact]
    public void DetachThenAttach_ResumesFollowingTheCollection()
    {
        var list = MakeList(3);
        using var src = new KumikoSource();
        src.SetItems(list);

        src.Detach();
        Assert.Equal(0, list.Subscribers);
        Assert.All(list, r => Assert.Equal(0, r.Subscribers));

        list.Add(new Row()); // changed while detached, no event delivered
        src.Attach();

        Assert.Equal(1, list.Subscribers);
        Assert.Equal(4, src.RowCount);
        list.AddAndRaise(new Row());
        Assert.Equal(5, src.RowCount);
        Assert.All(list, r => Assert.Equal(1, r.Subscribers));
    }

    [Fact]
    public void Track_SkipsDuplicates_SoOneUnsubscribeRemovesTheHandler()
    {
        var shared = new Row();
        var list = new CountingList { shared, shared };
        using var src = new KumikoSource();
        src.SetItems(list);
        Assert.Equal(1, shared.Subscribers);
        src.Dispose();
        Assert.Equal(0, shared.Subscribers);
    }

    [Fact]
    public void ResetResubscribesFromTheCurrentSource()
    {
        var list = MakeList(2);
        var removed = list[0];
        using var src = new KumikoSource();
        src.SetItems(list);
        list.RemoveAt(0);
        list.RaiseReset();
        Assert.Equal(0, removed.Subscribers);
        Assert.Equal(1, list[0].Subscribers);
    }

    [Fact]
    public void TrackingAndUntracking100KItems_IsFast()
    {
        const int n = 100_000;
        var list = MakeList(n);
        using var src = new KumikoSource();
        var sw = Stopwatch.StartNew();
        src.SetItems(list); // tracks 100K
        // Untrack one by one through collection events (the old list-based untrack was O(n) each).
        for (int i = 0; i < 20_000; i++)
        {
            var r = list[list.Count - 1];
            list.RemoveAt(list.Count - 1);
            list.RaiseRemove(r);
        }
        src.Dispose();
        sw.Stop();
        // Quadratic behavior would take many seconds; a generous bound keeps this non-flaky.
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10), $"took {sw.Elapsed}");
        Assert.All(list, r => Assert.Equal(0, r.Subscribers));
    }
}

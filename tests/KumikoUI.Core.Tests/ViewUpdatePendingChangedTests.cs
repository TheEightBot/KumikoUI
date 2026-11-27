using KumikoUI.Core.Models;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests;

/// <summary>
/// <see cref="KumikoSource.ViewUpdatePendingChanged"/> (#327): raised once per transition of
/// <see cref="KumikoSource.IsViewUpdatePending"/> for background rebuilds, never for synchronous ones.
/// </summary>
[Collection(nameof(BackgroundRebuildCollection))]
public class ViewUpdatePendingChangedTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    private sealed record Item(int Id, int Quantity, string City);

    private static KumikoSource NewSource(int count, int threshold)
    {
        var source = new KumikoSource { BackgroundRebuildThreshold = threshold };
        source.SetColumns(new[]
        {
            new KumikoColumn { Header = "Id", PropertyName = nameof(Item.Id), ColumnType = KumikoColumnType.Numeric },
            new KumikoColumn { Header = "Quantity", PropertyName = nameof(Item.Quantity), ColumnType = KumikoColumnType.Numeric },
            new KumikoColumn { Header = "City", PropertyName = nameof(Item.City) },
        });
        source.SetItems(Enumerable.Range(0, count).Select(i => new Item(i, (int)(i * 2654435761L % 1000), i % 2 == 0 ? "Oslo" : "Rome")).ToList());
        return source;
    }

    /// <summary>Records the pending flag as each notification arrives.</summary>
    private static List<bool> Record(KumikoSource source)
    {
        var seen = new List<bool>();
        source.ViewUpdatePendingChanged += (_, _) => seen.Add(source.IsViewUpdatePending);
        return seen;
    }

    [Fact]
    public async Task ABackgroundRebuild_RaisesOncePending_AndOnceWhenPublished()
    {
        using var ui = new TestUiThread();
        var source = ui.Invoke(() => NewSource(2000, threshold: 1000));
        await ui.Invoke(source.WhenViewUpdatedAsync).WaitAsync(Timeout);
        var seen = ui.Invoke(() => Record(source));

        // In the same turn: a fast build's result is posted behind the next queued call
        var afterRequest = ui.Invoke(() =>
        {
            source.ToggleSort(source.Columns[1]);
            return seen.ToArray();
        });
        Assert.Equal(new[] { true }, afterRequest);

        await ui.Invoke(source.WhenViewUpdatedAsync).WaitAsync(Timeout);

        Assert.Equal(new[] { true, false }, ui.Invoke(() => seen.ToArray()));
    }

    [Fact]
    public async Task RequestsInOneTurn_RaiseOnePair()
    {
        using var ui = new TestUiThread();
        var source = ui.Invoke(() => NewSource(3000, threshold: 1000));
        await ui.Invoke(source.WhenViewUpdatedAsync).WaitAsync(Timeout);
        var seen = ui.Invoke(() => Record(source));

        ui.Invoke(() =>
        {
            source.ToggleSort(source.Columns[0]);
            source.ToggleSort(source.Columns[1]);
            source.ToggleSort(source.Columns[2]);
        });
        await ui.Invoke(source.WhenViewUpdatedAsync).WaitAsync(Timeout);

        Assert.Equal(new[] { true, false }, ui.Invoke(() => seen.ToArray()));
    }

    [Fact]
    public async Task EachSeparateRebuild_RaisesItsOwnPair()
    {
        using var ui = new TestUiThread();
        var source = ui.Invoke(() => NewSource(2000, threshold: 1000));
        await ui.Invoke(source.WhenViewUpdatedAsync).WaitAsync(Timeout);
        var seen = ui.Invoke(() => Record(source));

        ui.Invoke(() => source.ToggleSort(source.Columns[1]));
        await ui.Invoke(source.WhenViewUpdatedAsync).WaitAsync(Timeout);
        ui.Invoke(() => source.ToggleSort(source.Columns[1]));
        await ui.Invoke(source.WhenViewUpdatedAsync).WaitAsync(Timeout);

        Assert.Equal(new[] { true, false, true, false }, ui.Invoke(() => seen.ToArray()));
    }

    [Fact]
    public void BelowTheThreshold_NothingIsRaised()
    {
        using var ui = new TestUiThread();
        var source = ui.Invoke(() => NewSource(50, threshold: 100));
        var seen = ui.Invoke(() => Record(source));

        ui.Invoke(() =>
        {
            source.ToggleSort(source.Columns[1]);
            source.SetColumnFilter(source.Columns[2], new FilterDescription(source.Columns[2])
            {
                Condition1 = new FilterCondition { TextOperator = TextFilterOperator.Equals, Value = "Rome" },
            });
        });

        Assert.Empty(ui.Invoke(() => seen.ToArray()));
    }

    [Fact]
    public void WithoutASynchronizationContext_NothingIsRaised()
    {
        using var ui = new TestUiThread(installContext: false);
        var source = ui.Invoke(() => NewSource(50, threshold: 0));
        var seen = ui.Invoke(() => Record(source));

        ui.Invoke(() => source.ToggleSort(source.Columns[1]));

        Assert.Empty(ui.Invoke(() => seen.ToArray()));
    }

    [Fact]
    public async Task DisposingASourceWithAPendingRebuild_RaisesOnce()
    {
        using var ui = new TestUiThread();
        var source = ui.Invoke(() => NewSource(2000, threshold: 1000));
        await ui.Invoke(source.WhenViewUpdatedAsync).WaitAsync(Timeout);
        var seen = ui.Invoke(() => Record(source));

        ui.Invoke(() => source.ToggleSort(source.Columns[1]));
        ui.Invoke(source.Dispose); // disposing rebuilds synchronously, which supersedes the pending one

        Assert.Equal(new[] { true, false }, ui.Invoke(() => seen.ToArray()));
        Assert.False(ui.Invoke(() => source.IsViewUpdatePending));
    }
}

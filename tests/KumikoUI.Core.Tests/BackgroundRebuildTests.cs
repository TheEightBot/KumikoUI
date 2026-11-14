using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;
using Xunit.Abstractions;

namespace KumikoUI.Core.Tests;

// Runs alone, so the 1 000 000-row UI-thread timing is not inflated by other tests competing for the CPU.
[CollectionDefinition(nameof(BackgroundRebuildCollection), DisableParallelization = true)]
public sealed class BackgroundRebuildCollection;

/// <summary>
/// Background view rebuilds (#254): above <see cref="KumikoSource.BackgroundRebuildThreshold"/> sorting, filtering and
/// grouping run on a worker; reads answer from the published view until the new one is swapped in on the UI thread;
/// requests in one UI turn are coalesced and a newer one cancels a running build; selection, the current cell and an
/// open edit follow their items; below the threshold everything stays synchronous.
/// </summary>
[Collection(nameof(BackgroundRebuildCollection))]
public class BackgroundRebuildTests(ITestOutputHelper output)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    public sealed class Item : INotifyPropertyChanged
    {
        private int _quantity;
        private string _city = "";

        public int Id { get; init; }
        public int Quantity { get => _quantity; set => Set(ref _quantity, value); }
        public string City { get => _city; set => Set(ref _city, value); }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public override string ToString() => $"#{Id}";
    }

    private static readonly string[] Cities = { "Oslo", "Rome", "Lima", "Kyiv", "Perth" };

    private static Item NewItem(int i) => new()
    {
        Id = i,
        Quantity = (int)((i * 2654435761L) % 1000),
        City = Cities[i % Cities.Length],
    };

    private static ObservableCollection<Item> NewItems(int count) => new(Enumerable.Range(0, count).Select(NewItem));

    private static KumikoColumn[] NewColumns() => new[]
    {
        new KumikoColumn { Header = "Id", PropertyName = nameof(Item.Id), ColumnType = KumikoColumnType.Numeric },
        new KumikoColumn { Header = "Quantity", PropertyName = nameof(Item.Quantity), ColumnType = KumikoColumnType.Numeric },
        new KumikoColumn { Header = "City", PropertyName = nameof(Item.City) },
    };

    private static KumikoSource NewSource(IEnumerable items, int threshold, out KumikoColumn[] columns)
    {
        columns = NewColumns();
        var source = new KumikoSource { BackgroundRebuildThreshold = threshold };
        source.SetColumns(columns);
        source.SetItems(items);
        return source;
    }

    private static List<int> Ids(KumikoSource source)
    {
        var ids = new List<int>(source.RowCount);
        for (int row = 0; row < source.RowCount; row++)
            if (!source.IsNonDataRow(row)) ids.Add(((Item)source.GetItem(row)).Id);
        return ids;
    }

    private static Task Updated(TestUiThread ui, KumikoSource source) =>
        ui.Invoke(source.WhenViewUpdatedAsync).WaitAsync(Timeout);

    /// <summary>A comparer that blocks its first comparison until released, so a test can act while a build runs.</summary>
    private sealed class GateComparer : IComparer
    {
        public readonly ManualResetEventSlim Entered = new();
        public readonly ManualResetEventSlim Release = new();

        public int Compare(object? x, object? y)
        {
            Entered.Set();
            Release.Wait(Timeout);
            return Comparer<object>.Default.Compare(x, y);
        }
    }

    // ── Synchronous by default ────────────────────────────────────

    [Fact]
    public void ANewSource_RebuildsSynchronously()
    {
        var source = new KumikoSource();
        Assert.Equal(int.MaxValue, source.BackgroundRebuildThreshold);
        Assert.Throws<ArgumentOutOfRangeException>(() => source.BackgroundRebuildThreshold = -1);
    }

    [Fact]
    public void BelowTheThreshold_TheRebuildIsSynchronous()
    {
        using var ui = new TestUiThread();
        var source = ui.Invoke(() => NewSource(NewItems(50), threshold: 100, out _));

        var (pending, first) = ui.Invoke(() =>
        {
            source.ToggleSort(source.Columns[1]);
            return (source.IsViewUpdatePending, ((Item)source.GetItem(0)).Quantity);
        });

        Assert.False(pending);
        Assert.Equal(ui.Invoke(() => Ids(source).Select(id => NewItem(id).Quantity).Min()), first);
        Assert.Equal(0, ui.Invoke(() => source.BackgroundBuildsStarted));
        Assert.True(ui.Invoke(source.WhenViewUpdatedAsync).IsCompleted);
    }

    [Fact]
    public void WithoutASynchronizationContext_TheRebuildIsSynchronous()
    {
        using var ui = new TestUiThread(installContext: false);
        var source = ui.Invoke(() => NewSource(NewItems(50), threshold: 0, out _));

        bool pending = ui.Invoke(() =>
        {
            source.ToggleSort(source.Columns[1]);
            return source.IsViewUpdatePending;
        });

        Assert.False(pending);
        var quantities = ui.Invoke(() => Ids(source).Select(id => NewItem(id).Quantity).ToList());
        Assert.Equal(quantities.OrderBy(q => q), quantities);
    }

    // ── Reads answer from the published view until the swap ──────

    [Fact]
    public async Task AboveTheThreshold_ReadsAnswerFromTheOldView_UntilTheNewOneIsSwappedIn()
    {
        using var ui = new TestUiThread();
        var items = NewItems(2000);
        var source = ui.Invoke(() => NewSource(items, threshold: 1000, out _));
        await Updated(ui, source); // the initial view (SetItems) is built in the background too
        var kinds = new List<KumikoViewChangeKind>();
        int updated = 0;
        ui.Invoke(() =>
        {
            source.ViewChanged += (_, e) => kinds.Add(e.Kind);
            source.ViewUpdated += (_, _) => updated++;
        });
        var before = ui.Invoke(() => Ids(source));

        var (pending, during, rowCount) = ui.Invoke(() =>
        {
            source.ToggleSort(source.Columns[1]);
            return (source.IsViewUpdatePending, Ids(source), source.RowCount);
        });

        Assert.True(pending);
        Assert.Equal(before, during); // the old order, whole
        Assert.Equal(2000, rowCount);

        await Updated(ui, source);

        var after = ui.Invoke(() => Ids(source));
        Assert.Equal(items.OrderBy(i => i.Quantity).ThenBy(i => i.Id).Select(i => i.Id), after);
        Assert.False(ui.Invoke(() => source.IsViewUpdatePending));
        Assert.Equal(new[] { KumikoViewChangeKind.Rebuild }, ui.Invoke(() => kinds.ToArray()));
        Assert.Equal(1, ui.Invoke(() => updated));
    }

    [Fact]
    public async Task RequestsInOneTurn_StartOneBuild()
    {
        using var ui = new TestUiThread();
        var items = NewItems(3000);
        var source = ui.Invoke(() => NewSource(items, threshold: 1000, out _));
        await Updated(ui, source);
        int startedBefore = ui.Invoke(() => source.BackgroundBuildsStarted);
        int updated = 0;
        ui.Invoke(() => source.ViewUpdated += (_, _) => updated++);

        ui.Invoke(() =>
        {
            source.ToggleSort(source.Columns[0]);
            source.ToggleSort(source.Columns[1]);
            source.SetColumnFilter(source.Columns[2], new FilterDescription(source.Columns[2])
            {
                Condition1 = new FilterCondition { TextOperator = TextFilterOperator.Equals, Value = "Rome" },
            });
            // PropertyChanged of the sorted property: coalesced with the rest, outside any batch
            for (int i = 0; i < 100; i++)
                items[i * 7].Quantity += 1;
        });
        await Updated(ui, source);

        Assert.Equal(startedBefore + 1, ui.Invoke(() => source.BackgroundBuildsStarted));
        Assert.Equal(1, ui.Invoke(() => updated));
        var expected = items.Where(i => i.City == "Rome").OrderBy(i => i.Quantity).ThenBy(i => i.Id).Select(i => i.Id);
        Assert.Equal(expected, ui.Invoke(() => Ids(source)));
    }

    [Fact]
    public async Task ANewerRequest_SupersedesARunningBuild()
    {
        using var ui = new TestUiThread();
        var items = NewItems(2000);
        var source = ui.Invoke(() => NewSource(items, threshold: 1000, out _));
        await Updated(ui, source);
        var gate = new GateComparer();
        var published = new List<int>();
        int updated = 0;
        ui.Invoke(() =>
        {
            source.Columns[0].CustomComparer = gate;
            source.ViewUpdated += (_, _) =>
            {
                updated++;
                published.Add(((Item)source.GetItem(0)).Id);
            };
            source.ToggleSort(source.Columns[0]); // starts a build that blocks in the comparer
        });
        Assert.True(gate.Entered.Wait(Timeout));

        ui.Invoke(() => source.SetSort(source.Columns[1], KumikoSortDirection.Descending));
        gate.Release.Set(); // the first build finishes, but its view is never published
        await Updated(ui, source);

        Assert.Equal(1, ui.Invoke(() => updated));
        var expected = items.OrderByDescending(i => i.Quantity).ThenBy(i => i.Id).Select(i => i.Id).ToList();
        Assert.Equal(expected, ui.Invoke(() => Ids(source)));
        Assert.Equal(new[] { expected[0] }, ui.Invoke(() => published.ToArray()));
    }

    [Fact]
    public async Task ARunningBuildCanceledTwiceInARow_IsAllowedToFinish_SoTheViewUpdates()
    {
        using var ui = new TestUiThread();
        var items = NewItems(2000);
        var source = ui.Invoke(() => NewSource(items, threshold: 1000, out _));
        await Updated(ui, source);
        var gates = new[] { new GateComparer(), new GateComparer(), new GateComparer() };
        int updated = 0;
        ui.Invoke(() => source.ViewUpdated += (_, _) => updated++);

        // Each request cancels the build before it, until two were canceled in a row: the third build then runs
        // to completion and publishes before the fourth request starts.
        for (int i = 0; i < gates.Length; i++)
        {
            int n = i;
            ui.Invoke(() =>
            {
                source.Columns[0].CustomComparer = gates[n];
                source.SetSort(source.Columns[0], n % 2 == 0 ? KumikoSortDirection.Ascending : KumikoSortDirection.Descending);
            });
            Assert.True(gates[n].Entered.Wait(Timeout));
        }
        ui.Invoke(() =>
        {
            source.Columns[0].CustomComparer = null;
            source.SetSort(source.Columns[1], KumikoSortDirection.Ascending);
        });
        Assert.Equal(1 + 3, ui.Invoke(() => source.BackgroundBuildsStarted)); // the initial view, then the gated three
        foreach (var gate in gates) gate.Release.Set();
        await Updated(ui, source);

        Assert.Equal(2, ui.Invoke(() => updated)); // the build allowed to finish, then the latest request
        Assert.Equal(items.OrderBy(i => i.Quantity).ThenBy(i => i.Id).Select(i => i.Id), ui.Invoke(() => Ids(source)));
    }

    // ── Selection, current cell and edit follow their items ───────

    [Fact]
    public async Task SelectionAndTheCurrentCell_SurviveABackgroundResort()
    {
        using var ui = new TestUiThread();
        var items = NewItems(5000);
        var source = ui.Invoke(() => NewSource(items, threshold: 1000, out _));
        await Updated(ui, source);
        var selection = ui.Invoke(() => new KumikoSelectionModel(source));
        var picked = items[1234];
        var other = items[42];

        int rowBeforeSwap = ui.Invoke(() =>
        {
            selection.Select(picked);
            selection.Select(other);
            selection.SetCurrentCell(source.IndexOfItem(picked), 2);
            source.ToggleSort(source.Columns[1]);
            return selection.CurrentCell.Row;
        });
        // Before the swap nothing moved.
        Assert.Equal(1234, rowBeforeSwap);
        await Updated(ui, source);

        var (current, currentItem, rows, selected) = ui.Invoke(() =>
            (selection.CurrentCell, selection.CurrentItem, selection.SelectedRows.OrderBy(r => r).ToArray(),
             selection.SelectedItems.ToArray()));
        int pickedRow = ui.Invoke(() => source.IndexOfItem(picked));
        int otherRow = ui.Invoke(() => source.IndexOfItem(other));
        Assert.NotEqual(1234, pickedRow);
        Assert.Equal(new CellPosition(pickedRow, 2), current);
        Assert.Same(picked, currentItem);
        Assert.Equal(new[] { pickedRow, otherRow }.OrderBy(r => r), rows);
        Assert.Equal(2, selected.Length);
        Assert.Same(picked, ui.Invoke(() => source.GetItem(pickedRow)));
    }

    [Fact]
    public async Task AnOpenEdit_StaysOnItsItem_ThroughABackgroundResort()
    {
        using var ui = new TestUiThread();
        var items = NewItems(3000);
        var source = ui.Invoke(() => NewSource(items, threshold: 1000, out _));
        await Updated(ui, source);
        var edit = ui.Invoke(() => new EditSession());
        var target = items[10];

        Assert.True(ui.Invoke(() =>
            edit.BeginEdit(source.IndexOfItem(target), 2, source.Columns[2], source, new KumikoRect(0, 0, 100, 30))));
        ui.Invoke(() => source.ToggleSort(source.Columns[1]));
        await Updated(ui, source);

        Assert.True(ui.Invoke(() => edit.IsEditing));
        Assert.Same(target, ui.Invoke(() => edit.EditItem));
        Assert.Equal(ui.Invoke(() => source.IndexOfItem(target)), ui.Invoke(() => edit.EditCell.Row));
    }

    // ── Collection and property changes while a build is pending ──

    [Fact]
    public async Task CollectionChangesWhileABuildIsPending_LeaveTheOldViewReadable_AndTheNextViewIncludesThem()
    {
        using var ui = new TestUiThread();
        var items = NewItems(2000);
        var source = ui.Invoke(() => NewSource(items, threshold: 1000, out _));
        await Updated(ui, source);
        var before = ui.Invoke(() => Ids(source));
        var removed = items[0];

        var during = ui.Invoke(() =>
        {
            source.ToggleSort(source.Columns[1]);
            items.RemoveAt(0);
            items.Add(NewItem(5000));
            items[5] = NewItem(5001);
            return (Ids(source), source.RowCount, ReferenceEquals(source.GetItem(0), removed));
        });

        Assert.Equal(before, during.Item1);
        Assert.Equal(2000, during.RowCount);
        Assert.True(during.Item3); // the removed item still shows until the swap

        await Updated(ui, source);
        Assert.Equal(items.OrderBy(i => i.Quantity).ThenBy(i => items.IndexOf(i)).Select(i => i.Id), ui.Invoke(() => Ids(source)));
        Assert.Equal(-1, ui.Invoke(() => source.IndexOfItem(removed)));
    }

    [Fact]
    public async Task SingleAddsAndRemoves_StillFollowIncrementally_WhenNothingIsPending()
    {
        using var ui = new TestUiThread();
        var items = NewItems(2000);
        var source = ui.Invoke(() => NewSource(items, threshold: 1000, out _));
        await Updated(ui, source);
        var kinds = new List<KumikoViewChangeKind>();
        ui.Invoke(() => source.ViewChanged += (_, e) => kinds.Add(e.Kind));

        var added = NewItem(9000);
        var (pending, count, last) = ui.Invoke(() =>
        {
            items.Insert(3, added);
            items.RemoveAt(10);
            return (source.IsViewUpdatePending, source.RowCount, source.GetItem(3));
        });

        Assert.False(pending);
        Assert.Equal(2000, count);
        Assert.Same(added, last);
        Assert.Equal(new[] { KumikoViewChangeKind.Insert, KumikoViewChangeKind.Remove }, ui.Invoke(() => kinds.ToArray()));
        Assert.Equal(items.Select(i => i.Id), ui.Invoke(() => Ids(source)));
    }

    [Fact]
    public async Task SummaryChangesMadeWhileABuildRuns_AreInTheSummariesOfThePublishedView()
    {
        using var ui = new TestUiThread();
        var items = NewItems(2000);
        var source = ui.Invoke(() =>
        {
            var s = NewSource(items, threshold: 1000, out _);
            s.AddGroupDescription(new GroupDescription(nameof(Item.City)));
            s.AddGroupSummaryRow(new GroupSummaryRow { Name = "G", Columns = { new SummaryColumnDescription(nameof(Item.Quantity), SummaryType.Sum) } });
            s.CaptionSummary = new CaptionSummaryDescription { Columns = { new SummaryColumnDescription(nameof(Item.Quantity), SummaryType.Average) } };
            var totals = new TableSummaryRow("Totals");
            totals.Columns.Add(new SummaryColumnDescription(nameof(Item.Quantity), SummaryType.Sum));
            s.AddTableSummaryRow(totals);
            return s;
        });
        await Updated(ui, source);
        var gate = new GateComparer();
        ui.Invoke(() =>
        {
            source.Columns[0].CustomComparer = gate;
            source.ToggleSort(source.Columns[0]);
        });
        Assert.True(gate.Entered.Wait(Timeout)); // the build has read every value already

        ui.Invoke(() =>
        {
            for (int i = 0; i < 50; i++) items[i * 13].Quantity += 1000; // in place on the old view, replayed on the new
        });
        gate.Release.Set();
        await Updated(ui, source);

        Assert.Equal((double)items.Sum(i => i.Quantity),
            ui.Invoke(() => source.ComputedBottomSummaries[0].GetRawValue(nameof(Item.Quantity), SummaryType.Sum)));
        foreach (var city in Cities)
        {
            var group = ui.Invoke(() => source.GetGroupSummaries($"{nameof(Item.City)}={city}"))!;
            Assert.Equal((double)items.Where(i => i.City == city).Sum(i => i.Quantity),
                group[0].GetRawValue(nameof(Item.Quantity), SummaryType.Sum));
            var caption = ui.Invoke(() => source.GetCaptionSummaryText($"{nameof(Item.City)}={city}"));
            Assert.Equal($"Avg: {items.Where(i => i.City == city).Average(i => i.Quantity).ToString(System.Globalization.CultureInfo.CurrentCulture)}", caption);
        }
    }

    // ── A background view equals the synchronous one ─────────────

    [Fact]
    public async Task AGroupedFilteredSortedView_BuiltInTheBackground_MatchesTheSynchronousOne()
    {
        using var ui = new TestUiThread();
        var items = NewItems(4000);
        void Configure(KumikoSource s)
        {
            s.BeginUpdate();
            s.AddGroupDescription(new GroupDescription(nameof(Item.City)));
            s.AddGroupDescription(new GroupDescription(nameof(Item.Quantity)) { KeySelector = q => (int)q! / 250 });
            s.AddGroupSummaryRow(new GroupSummaryRow { Name = "G", Levels = GroupSummaryLevels.All, Columns = { new SummaryColumnDescription(nameof(Item.Quantity), SummaryType.Max) } });
            s.CaptionSummary = new CaptionSummaryDescription { Columns = { new SummaryColumnDescription(nameof(Item.Quantity), SummaryType.Count) } };
            s.SetSort(s.Columns[1], KumikoSortDirection.Descending);
            s.SetColumnFilter(s.Columns[0], new FilterDescription(s.Columns[0])
            {
                Condition1 = new FilterCondition { NumericOperator = NumericFilterOperator.GreaterThan, Value = 100d },
            });
            s.EndUpdate();
        }

        var background = ui.Invoke(() => NewSource(items, threshold: 1000, out _));
        await Updated(ui, background);
        ui.Invoke(() => Configure(background));
        await Updated(ui, background);
        ui.Invoke(() => background.ToggleGroupExpansion(0)); // collapse the first group: a rebuild of its own
        await Updated(ui, background);
        var synchronous = ui.Invoke(() =>
        {
            var s = NewSource(items, threshold: int.MaxValue, out _);
            Configure(s);
            s.ToggleGroupExpansion(0);
            return s;
        });

        string Describe(KumikoSource s)
        {
            var rows = new List<string>();
            for (int row = 0; row < s.RowCount; row++)
            {
                if (s.GetGroupHeaderInfo(row) is { } header)
                    rows.Add($"H {header.GroupPath} {header.ItemCount} {header.IsExpanded} {s.GetCaptionSummaryText(header.GroupPath)}");
                else if (s.GetGroupSummaryRowInfo(row) is { } info)
                    rows.Add($"S {info.GroupPath} {s.GetGroupSummaries(info.GroupPath)![info.SummaryIndex].Values[nameof(Item.Quantity)]}");
                else
                    rows.Add($"D {((Item)s.GetItem(row)).Id}");
            }
            return string.Join("\n", rows);
        }

        Assert.Equal(ui.Invoke(() => Describe(synchronous)), ui.Invoke(() => Describe(background)));
    }

    [Fact]
    public async Task ExpandToItem_WhileABuildIsPending_RebuildsSynchronously()
    {
        using var ui = new TestUiThread();
        var items = NewItems(2000);
        var source = ui.Invoke(() => NewSource(items, threshold: 1000, out _));
        await Updated(ui, source);
        var target = items[777];

        var (row, pending, item) = ui.Invoke(() =>
        {
            source.DefaultGroupExpanded = false;
            source.AddGroupDescription(new GroupDescription(nameof(Item.City)));
            int r = source.ExpandToItem(target);
            return (r, source.IsViewUpdatePending, r >= 0 ? source.GetItem(r) : null);
        });

        Assert.True(row >= 0);
        Assert.False(pending);
        Assert.Same(target, item);
    }

    [Fact]
    public async Task Dispose_WhileABuildIsPending_CompletesTheAwaiters()
    {
        using var ui = new TestUiThread();
        // In one UI turn, so the build cannot start (let alone finish) before Dispose.
        var (source, waiting, pending) = ui.Invoke(() =>
        {
            var s = NewSource(NewItems(2000), threshold: 1000, out _);
            var task = s.WhenViewUpdatedAsync();
            bool wasPending = !task.IsCompleted;
            s.Dispose();
            return (s, task, wasPending);
        });
        Assert.True(pending);

        await waiting.WaitAsync(Timeout);
        Assert.Equal(0, ui.Invoke(() => source.RowCount));
    }

    [Fact]
    public async Task ABuildThatThrows_FaultsTheAwaiter()
    {
        using var ui = new TestUiThread();
        var source = ui.Invoke(() => NewSource(NewItems(2000), threshold: 1000, out _));
        await Updated(ui, source);

        var waiting = ui.Invoke(() =>
        {
            source.Columns[1].CustomComparer = Comparer<object>.Create((_, _) => throw new FormatException("bad comparer"));
            source.ToggleSort(source.Columns[1]);
            return source.WhenViewUpdatedAsync();
        });

        var ex = await Assert.ThrowsAnyAsync<Exception>(() => waiting.WaitAsync(Timeout));
        Assert.IsType<FormatException>(ex is InvalidOperationException { InnerException: { } inner } ? inner : ex);
        Assert.False(ui.Invoke(() => source.IsViewUpdatePending));
    }

    // ── 1 000 000 rows ────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Performance")]
    public async Task ToggleSort_On1MRows_NeverBlocksTheUiThreadLongerThanAFrame()
    {
        const int rows = 1_000_000;
        var items = new List<Item>(rows);
        for (int i = 0; i < rows; i++) items.Add(new Item { Id = i, Quantity = (int)((i * 2654435761L) % 1_000_000), City = Cities[i % 5] });

        // The time is wall-clock time on the UI thread (GC pauses excluded), so a CI runner that deschedules the thread
        // inflates it: the best of up to three rounds is checked. Without a worker the sort takes hundreds of milliseconds.
        var longest = TimeSpan.MaxValue;
        for (int round = 0; round < 3 && longest >= TimeSpan.FromMilliseconds(16.7); round++)
        {
            using var ui = new TestUiThread();
            var source = ui.Invoke(() => NewSource(items, KumikoSource.DefaultBackgroundRebuildThreshold, out _));
            var selection = ui.Invoke(() => new KumikoSelectionModel(source)); // a grid listens, so the swap maps rows
            await Updated(ui, source);
            var selected = items[123_456];
            ui.Invoke(() => selection.Select(selected));

            ui.ResetTiming();
            ui.Invoke(() => source.ToggleSort(source.Columns[1]));
            await Updated(ui, source);
            var roundLongest = ui.LongestWorkItem;
            output.WriteLine($"Round {round + 1}: longest UI-thread work item while sorting {rows:N0} rows: {roundLongest.TotalMilliseconds:F2} ms");
            if (roundLongest < longest) longest = roundLongest;

            Assert.Equal(0, ((Item)ui.Invoke(() => source.GetItem(0))).Quantity);
            Assert.Same(selected, ui.Invoke(() => source.GetItem(selection.SelectedRows.Single())));
        }

        // The sort ran on a worker; on the UI thread: the call, capturing the items, and swapping the view in.
        Assert.True(longest < TimeSpan.FromMilliseconds(16.7),
            $"a UI-thread work item took {longest.TotalMilliseconds:F1} ms (best of three rounds) while sorting {rows:N0} rows");
    }
}

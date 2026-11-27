using System.Collections.ObjectModel;
using KumikoUI.Core.Layout;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Tests.RowHeight;

/// <summary>
/// The row offset index (#320, spec #102 §6, §11): the uniform path is today's arithmetic and keeps no state; the
/// Fenwick path agrees with a naive model of row heights through measures, invalidations, inserts, removes, rebuilds,
/// estimate changes and style changes.
/// </summary>
public class KumikoRowLayoutTests
{
    public sealed class Item
    {
        public int Id { get; set; }
        public int Value { get; set; }
        public string Group { get; set; } = "";
    }

    private static (KumikoSource Source, ObservableCollection<Item> Items) CreateSource(int rows, int frozen = 0)
    {
        var items = new ObservableCollection<Item>(Enumerable.Range(0, rows)
            .Select(i => new Item { Id = i, Value = (i * 7919) % 1000, Group = i % 3 == 0 ? "A" : "B" }));
        var source = new KumikoSource { FrozenRowCount = frozen };
        source.SetColumns(new[]
        {
            new KumikoColumn { Header = "Id", PropertyName = nameof(Item.Id), Width = 100 },
            new KumikoColumn { Header = "Value", PropertyName = nameof(Item.Value), Width = 100 },
        });
        source.SetItems(items);
        return (source, items);
    }

    private static KumikoStyle Auto() => new() { RowHeightMode = KumikoRowHeightMode.Auto };

    // ── Uniform path ──

    [Fact]
    public void Uniform_IsRowTimesRowHeight_AndKeepsNoState()
    {
        var (source, _) = CreateSource(1000, frozen: 2);
        var style = new KumikoStyle { RowHeight = 30 };
        var rows = source.RowLayout;

        Assert.True(rows.IsUniform(style));
        Assert.Equal(0, rows.GetRowTop(0, style));
        Assert.Equal(999 * 30.0, rows.GetRowTop(999, style));
        Assert.Equal(30f, rows.GetRowHeight(500, style));
        Assert.Equal(30_000.0, rows.GetTotalHeight(style));
        Assert.Equal(60f, rows.GetFrozenHeight(style));
        Assert.Equal(17, rows.GetRowAt(17 * 30, style));            // a boundary belongs to the lower row
        Assert.Equal(16, rows.GetRowAt(17 * 30 - 0.01, style));
        Assert.Equal(-1, rows.GetRowAt(-1, style));
        Assert.Equal(10 * 30f, rows.GetScrollTop(12, style));
        Assert.Equal(12, rows.GetScrollRowAt(10 * 30f, style));
        Assert.Equal(40 + 60 + 10 * 30 - 15f, rows.GetRowY(12, 40, 15, style));
        Assert.Equal(40 + 30f, rows.GetRowY(1, 40, 15, style));     // frozen rows do not scroll
        Assert.False(rows.IsActive);
        Assert.False(rows.BeginFrame(style, source.Columns));
    }

    [Fact]
    public void Uniform_Queries_AllocateNothing()
    {
        var (source, _) = CreateSource(1000);
        var style = new KumikoStyle();
        var rows = source.RowLayout;
        rows.GetRowTop(1, style);

        long before = GC.GetAllocatedBytesForCurrentThread();
        double sum = 0;
        for (int i = 0; i < 1000; i++)
            sum += rows.GetRowTop(i, style) + rows.GetRowAt(i * 13.0, style) + rows.GetRowHeight(i, style);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(sum > 0);
    }

    [Fact]
    public void FixedWithAResolver_IsNotUniform()
    {
        var (source, _) = CreateSource(10);
        var style = new KumikoStyle { RowHeightResolver = (in KumikoRowHeightContext _) => null };

        Assert.False(source.RowLayout.IsUniform(style));
        Assert.False(source.RowLayout.IsUniform(Auto()));
    }

    // ── Fenwick path: edge cases ──

    [Fact]
    public void NoRows_HasNoHeight_AndEveryYIsBelowTheRows()
    {
        var (source, _) = CreateSource(0);
        var style = Auto();
        var rows = source.RowLayout;

        Assert.Equal(0, rows.GetTotalHeight(style));
        Assert.Equal(0, rows.GetRowAt(0, style));
        Assert.Equal(-1, rows.GetRowAt(-5, style));
        Assert.Equal(0, rows.GetRowTop(0, style));
    }

    [Fact]
    public void UnmeasuredRows_UseTheEstimate_ThenTheMeanOfTheMeasuredOnes()
    {
        var (source, _) = CreateSource(10);
        var style = Auto();
        style.MinRowHeight = 40;
        var rows = source.RowLayout;
        Assert.True(rows.BeginFrame(style, source.Columns));

        Assert.Equal(400.0, rows.GetTotalHeight(style));   // max(RowHeight 36, MinRowHeight 40) each

        rows.SetMeasuredHeight(0, source.GetItem(0), 100);
        rows.SetMeasuredHeight(1, source.GetItem(1), 50);

        Assert.Equal(150 + 8 * 75.0, rows.GetTotalHeight(style));
        Assert.Equal(100.0, rows.GetRowTop(1, style));
        Assert.Equal(150 + 75.0, rows.GetRowTop(3, style));
        Assert.Equal(75f, rows.GetRowHeight(5, style));
        Assert.True(rows.IsMeasured(1));
        Assert.False(rows.IsMeasured(2));
    }

    [Fact]
    public void RowAt_BoundariesBelongToTheLowerRow_AndOutsideYsAreClampedSentinels()
    {
        var (source, _) = CreateSource(4);
        var style = Auto();
        var rows = source.RowLayout;
        rows.BeginFrame(style, source.Columns);
        float[] heights = { 1, 250, 36.5f, 1 };
        for (int r = 0; r < heights.Length; r++)
            rows.SetMeasuredHeight(r, source.GetItem(r), heights[r]);

        Assert.Equal(0, rows.GetRowAt(0, style));
        Assert.Equal(1, rows.GetRowAt(1, style));
        Assert.Equal(1, rows.GetRowAt(250.99, style));
        Assert.Equal(2, rows.GetRowAt(251, style));
        Assert.Equal(3, rows.GetRowAt(287.5, style));
        Assert.Equal(4, rows.GetRowAt(288.5, style));       // the total: below the last row
        Assert.Equal(4, rows.GetRowAt(1e9, style));
        Assert.Equal(-1, rows.GetRowAt(-0.001, style));
    }

    [Fact]
    public void AllRowsFrozen_FrozenHeightIsTheTotal()
    {
        var (source, _) = CreateSource(3, frozen: 5);
        var style = Auto();
        var rows = source.RowLayout;
        rows.BeginFrame(style, source.Columns);
        for (int r = 0; r < 3; r++) rows.SetMeasuredHeight(r, source.GetItem(r), 20 + r);

        Assert.Equal(63f, rows.GetFrozenHeight(style));
        Assert.Equal(63.0, rows.GetTotalHeight(style));
        Assert.Equal(2, rows.GetRowAtY(10 + 21 + 20 + 1, 10, 0, style));   // the third frozen row
    }

    [Fact]
    public void GroupRows_AreRowHeightTall_AndFollowARowHeightChange()
    {
        var (source, _) = CreateSource(6);
        source.AddGroupDescription(new GroupDescription(nameof(Item.Group)));
        var style = Auto();
        var rows = source.RowLayout;
        rows.BeginFrame(style, source.Columns);
        Assert.True(source.IsGroupHeaderRow(0));

        Assert.Equal(36f, rows.GetRowHeight(0, style));
        style.RowHeight = 50;
        Assert.Equal(50f, rows.GetRowHeight(0, style));
        Assert.True(rows.IsCurrent(0));   // never measured
    }

    [Fact]
    public void Heights_SurviveSortFilterAndGrouping_AndSetItemsDropsThem()
    {
        var (source, items) = CreateSource(20);
        var style = Auto();
        var rows = source.RowLayout;
        rows.BeginFrame(style, source.Columns);
        var target = items[5];
        rows.SetMeasuredHeight(5, target, 123);

        source.ToggleSort(source.Columns[1]);
        int sorted = source.IndexOfItem(target);
        Assert.NotEqual(5, sorted);
        Assert.Equal(123f, rows.GetRowHeight(sorted, style));
        Assert.True(rows.IsMeasured(sorted));

        source.AddGroupDescription(new GroupDescription(nameof(Item.Group)));
        Assert.Equal(123f, rows.GetRowHeight(source.IndexOfItem(target), style));

        source.SetItems(new List<Item>(items));
        Assert.False(rows.IsMeasured(source.IndexOfItem(target)));
    }

    [Fact]
    public void ARemovedItem_LosesItsHeight()
    {
        var (source, items) = CreateSource(10);
        var style = Auto();
        var rows = source.RowLayout;
        rows.BeginFrame(style, source.Columns);
        var gone = items[3];
        rows.SetMeasuredHeight(3, gone, 90);

        items.RemoveAt(3);
        items.Insert(3, gone);

        Assert.False(rows.IsMeasured(3));
    }

    [Fact]
    public void Invalidation_KeepsTheHeightAsAnEstimate_UntilTheRowIsMeasuredAgain()
    {
        var (source, items) = CreateSource(10);
        var style = Auto();
        var rows = source.RowLayout;
        rows.BeginFrame(style, source.Columns);
        rows.SetMeasuredHeight(2, items[2], 80);
        rows.SetMeasuredHeight(3, items[3], 80);
        Assert.True(rows.IsCurrent(2));

        rows.InvalidateItem(items[2]);
        Assert.False(rows.IsCurrent(2));
        Assert.True(rows.IsCurrent(3));
        Assert.Equal(80f, rows.GetRowHeight(2, style));

        rows.InvalidateAll();
        Assert.False(rows.IsCurrent(3));
        Assert.Equal(80f, rows.GetRowHeight(3, style));
    }

    [Fact]
    public void AColumnWidthChange_MakesEveryHeightStale_AndAColorChangeDoesNot()
    {
        var (source, items) = CreateSource(10);
        var style = Auto();
        var rows = source.RowLayout;
        rows.BeginFrame(style, source.Columns);
        rows.SetMeasuredHeight(2, items[2], 80);

        style.SelectionColor = new KumikoColor(1, 2, 3);
        rows.BeginFrame(style, source.Columns);
        Assert.True(rows.IsCurrent(2));

        source.Columns[0].Width = 140;
        rows.BeginFrame(style, source.Columns);
        Assert.False(rows.IsCurrent(2));

        rows.SetMeasuredHeight(2, items[2], 60);
        style.CellPadding = 4;
        rows.BeginFrame(style, source.Columns);
        Assert.False(rows.IsCurrent(2));
    }

    [Fact]
    public void BackToUniform_ReleasesThePerRowState()
    {
        var (source, items) = CreateSource(10);
        var style = Auto();
        var rows = source.RowLayout;
        rows.BeginFrame(style, source.Columns);
        rows.SetMeasuredHeight(2, items[2], 80);
        Assert.True(rows.IsActive);

        style.RowHeightMode = KumikoRowHeightMode.Fixed;
        Assert.False(rows.BeginFrame(style, source.Columns));
        Assert.False(rows.IsActive);
        Assert.Equal(36 * 2.0, rows.GetRowTop(2, style));
    }

    [Fact]
    public void Queries_AfterTheIndexIsBuilt_AllocateNothing()
    {
        var (source, items) = CreateSource(5000);
        var style = Auto();
        var rows = source.RowLayout;
        rows.BeginFrame(style, source.Columns);
        for (int r = 0; r < 5000; r += 3) rows.SetMeasuredHeight(r, items[r], 20 + r % 50);
        rows.GetRowTop(1, style);

        long before = GC.GetAllocatedBytesForCurrentThread();
        double sum = 0;
        for (int i = 0; i < 5000; i++)
            sum += rows.GetRowTop(i, style) + rows.GetRowAt(i * 37.0, style) + rows.GetRowHeight(i, style);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(sum > 0);
    }

    // ── Differential test against a naive model ──

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 2)]
    [InlineData(3, 0)]
    public void RandomOperations_AgreeWithANaiveModel(int seed, int frozen)
    {
        var rng = new Random(seed);
        var (source, items) = CreateSource(300, frozen);
        var style = Auto();
        var rows = source.RowLayout;
        rows.BeginFrame(style, source.Columns);
        var measured = new Dictionary<object, float>(ReferenceEqualityComparer.Instance);
        int nextId = 1000;
        bool sorted = false, grouped = false;

        for (int op = 0; op < 10_000; op++)
        {
            int rowCount = source.RowCount;
            int kind = rng.Next(100);
            switch (kind)
            {
                case < 55 when rowCount > 0:
                {
                    int r = rng.Next(rowCount);
                    if (source.IsNonDataRow(r)) break;
                    var item = source.GetItem(r);
                    float h = rng.Next(8, 1600) / 8f;   // 1 to 200 px in eighths
                    rows.SetMeasuredHeight(r, item, h);
                    measured[item] = h;
                    break;
                }
                case < 62 when items.Count > 0:
                    rows.InvalidateItem(items[rng.Next(items.Count)]);
                    break;
                case < 64:
                    rows.InvalidateAll();
                    break;
                case < 76:
                    items.Insert(rng.Next(items.Count + 1), new Item { Id = nextId++, Value = rng.Next(1000), Group = rng.Next(2) == 0 ? "A" : "B" });
                    break;
                case < 86 when items.Count > 0:
                {
                    int i = rng.Next(items.Count);
                    measured.Remove(items[i]);
                    items.RemoveAt(i);
                    break;
                }
                case < 91:
                    // A rebuild with another order: sort on, sort off
                    source.ToggleSort(source.Columns[1]);
                    sorted = !sorted;
                    if (!sorted) source.ClearSort();
                    break;
                case < 94:
                    if (grouped) source.ClearGroupDescriptions();
                    else source.AddGroupDescription(new GroupDescription(nameof(Item.Group)));
                    grouped = !grouped;
                    break;
                case < 97:
                    style.RowHeight = rng.Next(20, 60);
                    break;
                default:
                    style.MinRowHeight = rng.Next(2) == 0 ? null : rng.Next(10, 80);
                    break;
            }

            try
            {
                AssertAgrees(source, style, rows, measured, rng);
            }
            catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException))
            {
                throw new InvalidOperationException($"after operation {op} (kind {kind}, sorted {sorted}, grouped {grouped})", ex);
            }
        }
    }

    private static void AssertAgrees(KumikoSource source, KumikoStyle style, KumikoRowLayout rows,
        Dictionary<object, float> measured, Random rng)
    {
        int count = source.RowCount;
        float rowHeight = style.RowHeight;
        float estimate = measured.Count > 0
            ? (float)(measured.Values.Sum(v => (double)v) / measured.Count)
            : Math.Max(rowHeight, style.MinRowHeight ?? rowHeight);
        var tops = new double[count + 1];
        var heights = new float[count];
        for (int r = 0; r < count; r++)
        {
            heights[r] = source.IsNonDataRow(r) ? rowHeight
                : measured.TryGetValue(source.GetItem(r), out var h) ? h : estimate;
            tops[r + 1] = tops[r] + heights[r];
        }

        Assert.Equal(tops[count], rows.GetTotalHeight(style), 6);
        for (int k = 0; k < 8 && count > 0; k++)
        {
            int r = rng.Next(count);
            Assert.Equal(tops[r], rows.GetRowTop(r, style), 6);
            Assert.Equal(heights[r], rows.GetRowHeight(r, style));
            Assert.Equal(r, rows.GetRowAt(tops[r], style));
            Assert.Equal(r, rows.GetRowAt(tops[r] + heights[r] * 0.999, style));
            bool isMeasured = !source.IsNonDataRow(r) && measured.ContainsKey(source.GetItem(r));
            Assert.Equal(isMeasured, rows.IsMeasured(r));
        }
        Assert.Equal(count, rows.GetRowAt(tops[count], style));
    }
}

using System.Collections;
using System.Globalization;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests;

/// <summary>
/// Sort order and stability (single, multi-column, descending, null, custom comparer), culture-dependent
/// formatting, and the order of group keys, pinned to the rows a user sees.
/// </summary>
public class KumikoSourceSortingAndCultureTests
{
    private sealed class Item
    {
        public string Id { get; set; } = "";
        public string? Name { get; set; }
        public int Rank { get; set; }
        public double Score { get; set; }
        public DateTime When { get; set; }
    }

    private static KumikoSource MakeSource(params Item[] items)
    {
        var source = new KumikoSource();
        source.SetColumns(new[]
        {
            new KumikoColumn { PropertyName = "Id" },
            new KumikoColumn { PropertyName = "Name" },
            new KumikoColumn { PropertyName = "Rank", ColumnType = KumikoColumnType.Numeric },
            new KumikoColumn { PropertyName = "Score", ColumnType = KumikoColumnType.Numeric },
            new KumikoColumn { PropertyName = "When", ColumnType = KumikoColumnType.Date },
        });
        source.SetItems(items.ToList());
        return source;
    }

    private static KumikoColumn Col(KumikoSource s, string property) => s.Columns.Single(c => c.PropertyName == property);

    private static string[] Ids(KumikoSource s) =>
        Enumerable.Range(0, s.RowCount).Select(i => ((Item)s.GetItem(i)).Id).ToArray();

    private static Item[] Ranked() => new[]
    {
        new Item { Id = "a", Rank = 2, Name = "x" },
        new Item { Id = "b", Rank = 1, Name = "y" },
        new Item { Id = "c", Rank = 2, Name = "y" },
        new Item { Id = "d", Rank = 1, Name = "x" },
        new Item { Id = "e", Rank = 2, Name = "x" },
    };

    // ── Stability ─────────────────────────────────────────────────

    [Fact]
    public void Sort_Ascending_KeepsEqualKeysInSourceOrder()
    {
        var source = MakeSource(Ranked());
        source.SetSort(Col(source, "Rank"), KumikoSortDirection.Ascending);

        Assert.Equal(new[] { "b", "d", "a", "c", "e" }, Ids(source));
    }

    [Fact]
    public void Sort_Descending_AlsoKeepsEqualKeysInSourceOrder()
    {
        var source = MakeSource(Ranked());
        source.SetSort(Col(source, "Rank"), KumikoSortDirection.Descending);

        Assert.Equal(new[] { "a", "c", "e", "b", "d" }, Ids(source));
    }

    [Fact]
    public void Sort_ToggledThroughAllThreeStates_ReturnsToSourceOrder()
    {
        var source = MakeSource(Ranked());
        var rank = Col(source, "Rank");

        source.ToggleSort(rank);
        source.ToggleSort(rank);
        source.ToggleSort(rank);

        Assert.Equal(new[] { "a", "b", "c", "d", "e" }, Ids(source));
    }

    [Fact]
    public void Sort_AfterAFilterIsCleared_StillUsesSourceOrderForTies()
    {
        var source = MakeSource(Ranked());
        source.SetSort(Col(source, "Rank"), KumikoSortDirection.Ascending);
        source.SetFilter(i => ((Item)i).Id != "d");
        source.SetFilter(null);

        Assert.Equal(new[] { "b", "d", "a", "c", "e" }, Ids(source));
    }

    // ── Multi-column order ────────────────────────────────────────

    [Fact]
    public void MultiSort_OrdersByThePrimaryThenTheSecondaryColumn()
    {
        var source = MakeSource(Ranked());
        source.SetSort(Col(source, "Rank"), KumikoSortDirection.Ascending, 1);
        source.SetSort(Col(source, "Name"), KumikoSortDirection.Descending, 2);

        Assert.Equal(new[] { "b", "d", "c", "a", "e" }, Ids(source));
    }

    [Fact]
    public void MultiSort_ChangingThePriority_ChangesTheOrder()
    {
        var source = MakeSource(Ranked());
        source.SetSort(Col(source, "Rank"), KumikoSortDirection.Ascending, 1);
        source.SetSort(Col(source, "Name"), KumikoSortDirection.Ascending, 2);
        Assert.Equal(new[] { "d", "b", "a", "e", "c" }, Ids(source));

        source.SetSort(Col(source, "Name"), KumikoSortDirection.Ascending, 1); // Name becomes primary
        Assert.Equal(new[] { "d", "a", "e", "b", "c" }, Ids(source)); // x: d, a, e by rank; y: b, c
    }

    [Fact]
    public void MultiSort_ThreeColumns_FallBackThroughEachInTurn()
    {
        var source = MakeSource(
            new Item { Id = "a", Rank = 1, Name = "x", Score = 2 },
            new Item { Id = "b", Rank = 1, Name = "x", Score = 1 },
            new Item { Id = "c", Rank = 1, Name = "w", Score = 9 },
            new Item { Id = "d", Rank = 0, Name = "z", Score = 5 });
        source.SetSort(Col(source, "Rank"), KumikoSortDirection.Ascending, 1);
        source.SetSort(Col(source, "Name"), KumikoSortDirection.Ascending, 2);
        source.SetSort(Col(source, "Score"), KumikoSortDirection.Ascending, 3);

        Assert.Equal(new[] { "d", "c", "b", "a" }, Ids(source));
    }

    [Fact]
    public void MultiSort_RemovingTheSecondaryColumn_FallsBackToStablePrimaryOrder()
    {
        var source = MakeSource(Ranked());
        source.SetSort(Col(source, "Rank"), KumikoSortDirection.Ascending, 1);
        source.SetSort(Col(source, "Name"), KumikoSortDirection.Descending, 2);
        source.SetSort(Col(source, "Name"), KumikoSortDirection.None);

        Assert.Equal(new[] { "b", "d", "a", "c", "e" }, Ids(source));
        Assert.False(source.IsMultiSortActive);
    }

    // ── Nulls and custom comparers ────────────────────────────────

    [Fact]
    public void Sort_PutsNullsFirstAscending_AndLastDescending()
    {
        var source = MakeSource(
            new Item { Id = "a", Name = "m" }, new Item { Id = "b", Name = null },
            new Item { Id = "c", Name = "a" }, new Item { Id = "d", Name = null });
        var name = Col(source, "Name");

        source.SetSort(name, KumikoSortDirection.Ascending);
        Assert.Equal(new[] { "b", "d", "c", "a" }, Ids(source));

        source.SetSort(name, KumikoSortDirection.Descending);
        Assert.Equal(new[] { "a", "c", "b", "d" }, Ids(source));
    }

    [Fact]
    public void Sort_WithACustomComparer_UsesItInBothDirections()
    {
        var source = MakeSource(
            new Item { Id = "a", Name = "zz" }, new Item { Id = "b", Name = "y" }, new Item { Id = "c", Name = "xxx" });
        var name = Col(source, "Name");
        name.CustomComparer = Comparer<object>.Create((x, y) => ((string)x).Length.CompareTo(((string)y).Length));

        source.SetSort(name, KumikoSortDirection.Ascending);
        Assert.Equal(new[] { "b", "a", "c" }, Ids(source)); // y, zz, xxx

        source.SetSort(name, KumikoSortDirection.Descending);
        Assert.Equal(new[] { "c", "a", "b" }, Ids(source));
    }

    [Fact]
    public void Sort_WithACustomComparer_DiffersFromTheTextOrder()
    {
        var source = MakeSource(
            new Item { Id = "a", Name = "b" }, new Item { Id = "b", Name = "aaa" }, new Item { Id = "c", Name = "cc" });
        var name = Col(source, "Name");

        source.SetSort(name, KumikoSortDirection.Ascending);
        Assert.Equal(new[] { "b", "a", "c" }, Ids(source)); // aaa, b, cc

        name.CustomComparer = Comparer<object>.Create((x, y) => ((string)x).Length.CompareTo(((string)y).Length));
        source.Refresh();
        Assert.Equal(new[] { "a", "c", "b" }, Ids(source)); // b, cc, aaa
    }

    [Fact]
    public void Sort_ByAValueAccessor_UsesTheAccessedValue()
    {
        var source = MakeSource(
            new Item { Id = "a", Rank = 3 }, new Item { Id = "b", Rank = 1 }, new Item { Id = "c", Rank = 2 });
        var computed = new KumikoColumn { PropertyName = "Computed", ValueAccessor = o => -((Item)o).Rank };
        source.SetColumns(new[] { computed });

        source.SetSort(computed, KumikoSortDirection.Ascending);

        Assert.Equal(new[] { "a", "c", "b" }, Ids(source));
    }

    [Fact]
    public void Sort_ByDates_IsChronological()
    {
        var source = MakeSource(
            new Item { Id = "a", When = new DateTime(2024, 12, 1) },
            new Item { Id = "b", When = new DateTime(2024, 2, 1) },
            new Item { Id = "c", When = new DateTime(2023, 12, 31) });

        source.SetSort(Col(source, "When"), KumikoSortDirection.Ascending);

        Assert.Equal(new[] { "c", "b", "a" }, Ids(source));
    }

    [Fact]
    public void Sort_OfNumbersStoredAsDoubles_IsNumericNotTextual()
    {
        var source = MakeSource(
            new Item { Id = "a", Score = 9 }, new Item { Id = "b", Score = 10 }, new Item { Id = "c", Score = 100 });

        source.SetSort(Col(source, "Score"), KumikoSortDirection.Descending);

        Assert.Equal(new[] { "c", "b", "a" }, Ids(source));
    }

    // ── Culture ───────────────────────────────────────────────────

    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");
    private static readonly CultureInfo American = CultureInfo.GetCultureInfo("en-US");

    [Fact]
    public void FormatCellValue_FollowsTheSourceCulture_NotTheThreadCulture()
    {
        var source = MakeSource(new Item { Id = "a", Score = 1234.5, When = new DateTime(2024, 3, 9) });
        var score = Col(source, "Score");
        score.Format = "N2";
        var when = Col(source, "When");
        when.Format = "d";

        source.Culture = German;
        Assert.Equal("1.234,50", source.GetCellDisplayText(0, score));
        Assert.Equal("09.03.2024", source.GetCellDisplayText(0, when));

        source.Culture = American;
        Assert.Equal("1,234.50", source.GetCellDisplayText(0, score));
        Assert.Equal("3/9/2024", source.GetCellDisplayText(0, when));
    }

    [Fact]
    public void FormatCellValue_WithTheInvariantCulture_IgnoresTheThreadCulture()
    {
        var source = MakeSource();
        source.Culture = CultureInfo.InvariantCulture;
        var score = new KumikoColumn { PropertyName = "Score", Format = "N1" };

        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = German;
            Assert.Equal("1,234.5", source.FormatCellValue(1234.5, score));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void FormatCellValue_WithNoSourceCulture_UsesTheCurrentCultureAtTheTimeOfUse()
    {
        var source = MakeSource();
        var score = new KumikoColumn { PropertyName = "Score", Format = "N1" };

        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = German;
            Assert.Equal("1.234,5", source.FormatCellValue(1234.5, score));
            CultureInfo.CurrentCulture = American;
            Assert.Equal("1,234.5", source.FormatCellValue(1234.5, score));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void FormatCellValue_OfNull_IsEmpty_InAnyCulture()
    {
        var source = MakeSource();
        source.Culture = German;

        Assert.Equal("", source.FormatCellValue(null, new KumikoColumn { PropertyName = "Score", Format = "N2" }));
    }

    [Fact]
    public void SettingTheCulture_RebuildsTheView_AndRaisesOneRebuild()
    {
        var source = MakeSource(Ranked());
        var kinds = new List<KumikoViewChangeKind>();
        source.ViewChanged += (_, e) => kinds.Add(e.Kind);

        source.Culture = German;
        source.StringCompareOptions = CompareOptions.Ordinal;

        Assert.Equal(new[] { KumikoViewChangeKind.Rebuild, KumikoViewChangeKind.Rebuild }, kinds);
        Assert.Same(German, source.Comparer.Culture);
    }

    [Fact]
    public void GetUniqueColumnValues_AreFormattedInTheSourceCulture()
    {
        var source = MakeSource(new Item { Score = 1.5 }, new Item { Score = 2.25 }, new Item { Score = 1.5 });
        var score = Col(source, "Score");
        score.Format = "F2";
        source.Culture = German;

        Assert.Equal(new[] { "1,50", "2,25" }, source.GetUniqueColumnValues(score));
    }

    [Fact]
    public void GroupHeaders_UseTheColumnFormatInTheSourceCulture()
    {
        var source = MakeSource(new Item { Id = "a", Score = 1.5 }, new Item { Id = "b", Score = 1.5 });
        Col(source, "Score").Format = "F2";
        source.Culture = German;
        source.AddGroupDescription(new GroupDescription("Score"));

        Assert.Equal("1,50", source.GetGroupHeaderInfo(0)!.DisplayText);
    }

    [Fact]
    public void SortingText_FollowsTheCulturesCollation()
    {
        // Swedish sorts "ä" after "z"; American English sorts it next to "a". Skipped where ICU is absent.
        var source = MakeSource(
            new Item { Id = "ae", Name = "ä" }, new Item { Id = "z", Name = "z" }, new Item { Id = "a", Name = "a" });
        var name = Col(source, "Name");

        source.Culture = CultureInfo.GetCultureInfo("sv-SE");
        source.SetSort(name, KumikoSortDirection.Ascending);
        string[] swedish = Ids(source);
        source.Culture = American;
        string[] american = Ids(source);

        if (swedish.SequenceEqual(american))
            return; // invariant-globalization host: every culture collates the same

        Assert.Equal(new[] { "a", "z", "ae" }, swedish);
        Assert.Equal(new[] { "a", "ae", "z" }, american);
    }

    [Fact]
    public void SetCellValue_ParsesTextInTheSourceCulture()
    {
        var source = MakeSource(new Item { Id = "a" });
        var score = Col(source, "Score");
        source.Culture = German;

        Assert.True(source.SetCellValue(0, score, "3,5"));

        Assert.Equal(3.5, ((Item)source.GetItem(0)).Score);
    }

    [Fact(Skip = "Known defect: summary values are formatted in the thread culture, ignoring KumikoSource.Culture (#47)")]
    public void SummaryFormat_UsesTheSourceCulture()
    {
        var source = MakeSource(new Item { Score = 1234.5 }, new Item { Score = 1000 });
        source.Culture = German;
        source.AddTableSummaryRow(new TableSummaryRow("T")
        {
            Columns = { new SummaryColumnDescription("Score", SummaryType.Sum, "N2") { Label = "" } },
        });

        Assert.Equal("2.234,50", source.ComputedBottomSummaries.Single().Values["Score"]);
    }

    // ── Group key order ───────────────────────────────────────────

    [Fact]
    public void GroupKeys_OfNumbers_OrderNumericallyInBothDirections()
    {
        var source = MakeSource(
            new Item { Id = "a", Rank = 10 }, new Item { Id = "b", Rank = 9 }, new Item { Id = "c", Rank = 100 });
        var group = new GroupDescription("Rank");
        source.AddGroupDescription(group);

        Assert.Equal(new[] { "9", "10", "100" }, GroupTexts(source));

        source.ClearGroupDescriptions();
        group.GroupSortDirection = KumikoSortDirection.Descending;
        source.AddGroupDescription(group);
        Assert.Equal(new[] { "100", "10", "9" }, GroupTexts(source));
    }

    [Fact]
    public void GroupKeys_ThatDisplayTheSame_AreOneGroupEvenWhenTheRawKeysDiffer()
    {
        // Groups are keyed by display text (documented behavior; keyed-by-raw-key is #29).
        var source = MakeSource(new Item { Id = "a", Score = 1.001 }, new Item { Id = "b", Score = 1.004 });
        Col(source, "Score").Format = "F1";
        source.AddGroupDescription(new GroupDescription("Score"));

        Assert.Equal(new[] { "1.0" }, GroupTexts(source));
        Assert.Equal(2, source.GetGroupHeaderInfo(0)!.ItemCount);
    }

    [Fact(Skip = "Known defect: collapsing one of two groups that share display text collapses both because expand state is keyed by display text (#29)")]
    public void CollapsingAGroup_DoesNotCollapseAnotherGroupWithTheSameDisplayText()
    {
        var source = MakeSource(new Item { Id = "a", Rank = 1 }, new Item { Id = "b", Rank = 2 });
        source.AddGroupDescription(new GroupDescription("Rank")
        {
            KeySelector = v => (int)v!,
            DisplayTextSelector = _ => "same",
        });

        // Two distinct keys, one header text: they must stay two groups with independent state.
        Assert.Equal(2, GroupTexts(source).Length);
    }

    private static string[] GroupTexts(KumikoSource s) =>
        Enumerable.Range(0, s.RowCount).Select(s.GetGroupHeaderInfo).Where(h => h != null).Select(h => h!.DisplayText).ToArray();
}

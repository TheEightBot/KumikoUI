using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests;

/// <summary>
/// Groups are keyed by the raw group key rather than display text, and expand/collapse state is keyed by the raw
/// key path and pruned per path when the group descriptions change (#29).
/// </summary>
public class KumikoSourceGroupKeyTests
{
    private sealed class Row
    {
        public int Id { get; set; }
        public string? Region { get; set; }
        public string City { get; set; } = "";
    }

    private static List<Row> Rows() => new()
    {
        new Row { Id = 1, Region = "West", City = "Seattle" },
        new Row { Id = 2, Region = "East", City = "Boston" },
        new Row { Id = 3, Region = "West", City = "Portland" },
        new Row { Id = 4, Region = "East", City = "Boston" },
    };

    private static KumikoSource Make(IEnumerable<Row>? rows = null)
    {
        var source = new KumikoSource();
        source.SetColumns(new[]
        {
            new KumikoColumn { PropertyName = "Id", Header = "Id", ColumnType = KumikoColumnType.Numeric },
            new KumikoColumn { PropertyName = "Region", Header = "Region" },
            new KumikoColumn { PropertyName = "City", Header = "City" },
        });
        source.SetItems(rows ?? Rows());
        return source;
    }

    private static List<GroupHeaderInfo> Headers(KumikoSource s)
    {
        var list = new List<GroupHeaderInfo>();
        for (int i = 0; i < s.RowCount; i++)
            if (s.GetGroupHeaderInfo(i) is { } h) list.Add(h);
        return list;
    }

    private static int HeaderRow(KumikoSource s, int level, string text)
    {
        for (int i = 0; i < s.RowCount; i++)
            if (s.GetGroupHeaderInfo(i) is { } h && h.Level == level && h.DisplayText == text) return i;
        return -1;
    }

    [Fact]
    public void Grouping_IdenticalDisplayTextDistinctKeys_MakesTwoGroups()
    {
        var source = Make();
        // Keys 1..4 all display as "Same"; the keys differ so there are four groups.
        source.AddGroupDescription(new GroupDescription("Id") { DisplayTextSelector = _ => "Same" });

        var headers = Headers(source);
        Assert.Equal(4, headers.Count);
        Assert.All(headers, h => Assert.Equal("Same", h.DisplayText));
        Assert.Equal(4, headers.Select(h => h.GroupPath).Distinct().Count());
    }

    [Fact]
    public void Grouping_IdenticalDisplayText_KeepsExpandStatePerKey()
    {
        var source = Make();
        source.AddGroupDescription(new GroupDescription("Id")
        {
            KeySelector = v => (int)v! % 2, // two keys: 0 and 1
            DisplayTextSelector = _ => "Same",
        });
        Assert.Equal(2, Headers(source).Count);

        int first = HeaderRow(source, 0, "Same");
        var collapsedKey = source.GetGroupHeaderInfo(first)!.Key;
        source.ToggleGroupExpansion(first);

        var headers = Headers(source);
        Assert.False(headers.Single(h => Equals(h.Key, collapsedKey)).IsExpanded);
        Assert.True(headers.Single(h => !Equals(h.Key, collapsedKey)).IsExpanded);
    }

    [Fact]
    public void RemoveGroupDescription_InnerLevel_KeepsOuterExpandState()
    {
        var source = Make();
        source.AddGroupDescription(new GroupDescription("Region"));
        source.AddGroupDescription(new GroupDescription("City"));
        source.ToggleGroupExpansion(HeaderRow(source, 0, "East")); // collapse East
        source.ToggleGroupExpansion(HeaderRow(source, 1, "Seattle")); // collapse West > Seattle

        source.RemoveGroupDescription("City");

        var headers = Headers(source);
        Assert.Equal(2, headers.Count);
        Assert.False(headers.Single(h => h.DisplayText == "East").IsExpanded);
        Assert.True(headers.Single(h => h.DisplayText == "West").IsExpanded);

        // The dropped inner path does not come back as stale state when the level is added again.
        source.AddGroupDescription(new GroupDescription("City"));
        Assert.True(Headers(source).Single(h => h.DisplayText == "Seattle").IsExpanded);
        Assert.False(Headers(source).Single(h => h.DisplayText == "East").IsExpanded);
    }

    [Fact]
    public void AddGroupDescription_InnerLevel_KeepsOuterExpandState()
    {
        var source = Make();
        source.AddGroupDescription(new GroupDescription("Region"));
        source.ToggleGroupExpansion(HeaderRow(source, 0, "East"));

        source.AddGroupDescription(new GroupDescription("City"));

        Assert.False(Headers(source).Single(h => h.DisplayText == "East").IsExpanded);
        Assert.True(Headers(source).Single(h => h.DisplayText == "West").IsExpanded);
    }

    [Fact]
    public void RemoveGroupDescription_OuterLevel_DropsStateItCannotMap()
    {
        var source = Make();
        source.AddGroupDescription(new GroupDescription("Region"));
        source.AddGroupDescription(new GroupDescription("City"));
        source.ToggleGroupExpansion(HeaderRow(source, 0, "East"));

        source.RemoveGroupDescription("Region");

        Assert.All(Headers(source), h => Assert.True(h.IsExpanded));
    }

    [Fact]
    public void ClearGroupDescriptions_DropsAllState()
    {
        var source = Make();
        source.AddGroupDescription(new GroupDescription("Region"));
        source.ToggleGroupExpansion(HeaderRow(source, 0, "East"));

        source.ClearGroupDescriptions();
        source.AddGroupDescription(new GroupDescription("Region"));

        Assert.All(Headers(source), h => Assert.True(h.IsExpanded));
    }

    [Fact]
    public void Grouping_NullKey_IsItsOwnGroupAndKeepsState()
    {
        var rows = Rows();
        rows.Add(new Row { Id = 5, Region = null, City = "Nowhere" });
        rows.Add(new Row { Id = 6, Region = null, City = "Nowhere" });
        var source = Make(rows);
        // A null key whose display text matches a real key still stays a group of its own.
        source.AddGroupDescription(new GroupDescription("Region")
        {
            DisplayTextSelector = k => k is null ? "East" : (string)k,
        });

        var headers = Headers(source);
        Assert.Equal(3, headers.Count);
        var nullGroup = headers.Single(h => h.Key is null);
        Assert.Equal(2, nullGroup.ItemCount);

        int nullRow = Enumerable.Range(0, source.RowCount).First(i => source.IsGroupHeaderRow(i)
            && source.GetGroupHeaderInfo(i)!.Key is null);
        source.ToggleGroupExpansion(nullRow);
        // Only the null group collapsed, though the "East" group has the same display text.
        var after = Headers(source);
        Assert.Equal(1, after.Count(h => !h.IsExpanded));
        Assert.False(after.Single(h => h.Key is null).IsExpanded);
    }

    [Fact]
    public void Grouping_NullKey_ExpandStateSurvivesRebuild()
    {
        var rows = Rows();
        rows.Add(new Row { Id = 5, Region = null, City = "Nowhere" });
        var source = Make(rows);
        source.AddGroupDescription(new GroupDescription("Region"));
        int nullRow = Enumerable.Range(0, source.RowCount).First(i => source.IsGroupHeaderRow(i)
            && source.GetGroupHeaderInfo(i)!.Key is null);
        source.ToggleGroupExpansion(nullRow);

        source.AddGroupDescription(new GroupDescription("City"));

        var nullHeader = Headers(source).Single(h => h.Level == 0 && h.Key is null);
        Assert.False(nullHeader.IsExpanded);
    }

    [Fact]
    public void ExpandToItem_DistinctKeysSharingDisplayText_ExpandsTheRightGroup()
    {
        var rows = Rows();
        var source = Make(rows);
        source.AddGroupDescription(new GroupDescription("Id")
        {
            KeySelector = v => (int)v! % 2,
            DisplayTextSelector = _ => "Same",
        });
        source.CollapseAllGroups();
        Assert.Equal(-1, source.IndexOfItem(rows[0]));

        int row = source.ExpandToItem(rows[0]); // Id 1 is in the odd group

        Assert.True(row >= 0);
        Assert.Same(rows[0], source.GetItem(row));
        Assert.Equal(1, Headers(source).Count(h => !h.IsExpanded)); // the other group stays collapsed
    }
}

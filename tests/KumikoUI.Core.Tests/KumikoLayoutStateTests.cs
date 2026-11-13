using System.Text.Json;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests;

/// <summary>
/// <see cref="KumikoLayoutState"/>: capturing a source's layout, the JSON round trip, and applying it to a source
/// with the same, more or fewer columns.
/// </summary>
public class KumikoLayoutStateTests
{
    private sealed class Row
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
        public string City { get; set; } = "";
        public DateTime Joined { get; set; }
        public bool Active { get; set; }
    }

    private static readonly List<Row> Rows = Enumerable.Range(0, 20).Select(i => new Row
    {
        Name = $"N{i:D2}",
        Age = 20 + i % 7,
        City = (i % 3) switch { 0 => "Oslo", 1 => "Rome", _ => "Lima" },
        Joined = new DateTime(2024, 1, 1).AddDays(i * 10),
        Active = i % 2 == 0,
    }).ToList();

    /// <summary>A fresh source with the given columns (by property name) in that order.</summary>
    private static KumikoSource NewSource(params string[] names)
    {
        var source = new KumikoSource();
        source.SetColumns(names.Select(n => new KumikoColumn
        {
            Header = n,
            PropertyName = n,
            ColumnType = n switch
            {
                "Age" => KumikoColumnType.Numeric,
                "Joined" => KumikoColumnType.Date,
                "Active" => KumikoColumnType.Boolean,
                _ => KumikoColumnType.Text,
            },
        }));
        source.SetItems(Rows);
        return source;
    }

    private static KumikoColumn Col(KumikoSource source, string name) =>
        source.Columns.Single(c => c.PropertyName == name);

    private static string[] Order(KumikoSource source) => source.Columns.Select(c => c.PropertyName).ToArray();

    private static string[] Names(KumikoSource source) =>
        Enumerable.Range(0, source.RowCount).Where(r => !source.IsNonDataRow(r))
            .Select(r => ((Row)source.GetItem(r)).Name).ToArray();

    /// <summary>A source with every kind of layout change applied.</summary>
    private static KumikoSource Customized()
    {
        var source = NewSource("Name", "Age", "City", "Joined", "Active");
        source.ReorderColumn(2, 0);                                   // City, Name, Age, Joined, Active
        source.SetDisplayIndex(Col(source, "Active"), 1);             // City, Active, Name, Age, Joined
        Col(source, "Name").Width = 180;
        Col(source, "Joined").SizeMode = ColumnSizeMode.Star;
        Col(source, "Joined").StarWeight = 2;
        Col(source, "Active").IsVisible = false;
        Col(source, "City").FreezeMode = ColumnFreezeMode.Left;
        source.SetSort(Col(source, "City"), KumikoSortDirection.Ascending, 1);
        source.SetSort(Col(source, "Age"), KumikoSortDirection.Descending, 2);
        source.SetColumnFilter(Col(source, "City"), new FilterDescription(Col(source, "City"))
        {
            SelectedValues = new HashSet<string> { "Rome", "Oslo" },
        });
        source.SetColumnFilter(Col(source, "Age"), new FilterDescription(Col(source, "Age"))
        {
            Condition1 = new FilterCondition { NumericOperator = NumericFilterOperator.GreaterThanOrEqual, Value = 22 },
            Condition2 = new FilterCondition { NumericOperator = NumericFilterOperator.LessThan, Value = 25.5m },
            Combination = FilterCombination.And,
        });
        source.SetColumnFilter(Col(source, "Joined"), new FilterDescription(Col(source, "Joined"))
        {
            Condition1 = new FilterCondition { DateOperator = DateFilterOperator.Before, Value = new DateTime(2024, 5, 1) },
        });
        source.SetColumnFilter(Col(source, "Name"), new FilterDescription(Col(source, "Name"))
        {
            Condition1 = new FilterCondition { TextOperator = TextFilterOperator.NotContains, Value = "7" },
        });
        source.AddGroupDescription(new GroupDescription("City", "Town") { GroupSortDirection = KumikoSortDirection.Descending });
        return source;
    }

    [Fact]
    public void Capture_RecordsOrderSizingVisibilityFreezeSortFilterAndGrouping()
    {
        var state = KumikoLayoutState.Capture(Customized());

        Assert.Equal(KumikoLayoutState.CurrentVersion, state.Version);
        Assert.Equal(new[] { "City", "Active", "Name", "Age", "Joined" }, state.Columns!.Select(c => c.Key));
        var name = state.Columns!.Single(c => c.Key == "Name");
        Assert.Equal(180, name.Width);
        Assert.Equal(KumikoSortDirection.None, name.SortDirection);
        var age = state.Columns!.Single(c => c.Key == "Age");
        Assert.Null(age.Width); // never set
        Assert.Equal((KumikoSortDirection.Descending, 2), (age.SortDirection, age.SortOrder));
        Assert.Equal(KumikoFilterValueKind.Number, age.Filter!.Condition1!.ValueKind);
        Assert.Equal("22", age.Filter.Condition1.Value);
        Assert.Equal("25.5", age.Filter.Condition2!.Value);
        var joined = state.Columns!.Single(c => c.Key == "Joined");
        Assert.Equal((ColumnSizeMode.Star, 2f), (joined.SizeMode, joined.StarWeight));
        Assert.Null(joined.Width);
        Assert.False(state.Columns!.Single(c => c.Key == "Active").IsVisible);
        var city = state.Columns!.Single(c => c.Key == "City");
        Assert.Equal(ColumnFreezeMode.Left, city.FreezeMode);
        Assert.Equal(new[] { "Oslo", "Rome" }, city.Filter!.SelectedValues); // sorted
        var group = Assert.Single(state.Groups!);
        Assert.Equal(("City", "Town", KumikoSortDirection.Descending), (group.PropertyName, group.Header, group.SortDirection));
    }

    [Fact]
    public void Layout_RoundTripsThroughJson_OntoAFreshSource()
    {
        var original = Customized();
        string json = KumikoLayoutState.Capture(original).ToJson();

        var restored = NewSource("Name", "Age", "City", "Joined", "Active");
        KumikoLayoutState.FromJson(json).ApplyTo(restored);

        Assert.Equal(Order(original), Order(restored));
        Assert.Equal(original.DisplayColumns.Select(c => c.PropertyName), restored.DisplayColumns.Select(c => c.PropertyName));
        foreach (var expected in original.Columns)
        {
            var actual = Col(restored, expected.PropertyName);
            Assert.Equal(expected.IsVisible, actual.IsVisible);
            Assert.Equal(expected.FreezeMode, actual.FreezeMode);
            Assert.Equal(expected.SizeMode, actual.SizeMode);
            Assert.Equal(expected.StarWeight, actual.StarWeight);
            Assert.Equal(expected.HasExplicitWidth, actual.HasExplicitWidth);
            if (expected.HasExplicitWidth) Assert.Equal(expected.Width, actual.Width);
            Assert.Equal(expected.SortDirection, actual.SortDirection);
            Assert.Equal(expected.SortOrder, actual.SortOrder);
            Assert.Equal(expected.ActiveFilter is null, actual.ActiveFilter is null);
        }
        Assert.True(restored.IsMultiSortActive);
        var group = Assert.Single(restored.GroupDescriptions);
        Assert.Equal(("City", "Town", KumikoSortDirection.Descending), (group.PropertyName, group.Header, group.GroupSortDirection));

        // The restored sort, filters and grouping select and order the same rows.
        Assert.NotEmpty(Names(original));
        Assert.Equal(Names(original), Names(restored));
        // And the JSON of the restored layout is the same.
        Assert.Equal(json, KumikoLayoutState.Capture(restored).ToJson());
    }

    [Fact]
    public void ToJson_IsCamelCase_WithEnumNames_AndWithoutNulls()
    {
        string json = KumikoLayoutState.Capture(Customized()).ToJson();

        using var doc = JsonDocument.Parse(json);
        var columns = doc.RootElement.GetProperty("columns");
        var city = columns.EnumerateArray().First(c => c.GetProperty("key").GetString() == "City");
        Assert.Equal("Left", city.GetProperty("freezeMode").GetString());
        Assert.Equal("Ascending", city.GetProperty("sortDirection").GetString());
        Assert.False(city.TryGetProperty("width", out _)); // null: left out
        Assert.Equal("Before", columns.EnumerateArray().First(c => c.GetProperty("key").GetString() == "Joined")
            .GetProperty("filter").GetProperty("condition1").GetProperty("dateOperator").GetString());
        Assert.Equal(1, doc.RootElement.GetProperty("version").GetInt32());
    }

    [Fact]
    public void ApplyTo_IgnoresUnknownColumns_AndKeepsColumnsTheLayoutDoesNotKnowInPlace()
    {
        // Saved with Age, Gone (a column since removed, sorted first), City, Name.
        var saved = NewSource("Name", "Age", "City");
        saved.ReorderColumn(0, 2); // Age, City, Name
        Col(saved, "Name").Width = 150;
        saved.SetSort(Col(saved, "Age"), KumikoSortDirection.Descending, 1);
        var state = KumikoLayoutState.Capture(saved);
        state.Columns!.Single(c => c.Key == "Age").SortOrder = 2;
        state.Columns!.Insert(1, new KumikoColumnLayoutState
        {
            Key = "Gone", Width = 70, SortDirection = KumikoSortDirection.Ascending, SortOrder = 1,
        });
        string json = state.ToJson();

        // Restored onto Name, Joined (a column added since), Age, City.
        var target = NewSource("Name", "Joined", "Age", "City");
        Col(target, "Joined").Width = 90;
        target.SetSort(Col(target, "Joined"), KumikoSortDirection.Ascending, 1);
        KumikoLayoutState.FromJson(json).ApplyTo(target);

        // Name, Age and City take the saved order in the slots they held (0, 2, 3); Joined stays at 1.
        Assert.Equal(new[] { "Age", "Joined", "City", "Name" }, Order(target));
        Assert.Equal(150, Col(target, "Name").Width);
        Assert.Equal(90, Col(target, "Joined").Width);
        // The sort is replaced: the new column drops out, and with Gone missing Age becomes the primary sort.
        Assert.Equal(KumikoSortDirection.None, Col(target, "Joined").SortDirection);
        Assert.Equal((KumikoSortDirection.Descending, 1), (Col(target, "Age").SortDirection, Col(target, "Age").SortOrder));
        Assert.False(target.IsMultiSortActive);
    }

    [Fact]
    public void ApplyTo_MatchesByKey_BeforePropertyName()
    {
        var saved = NewSource("Name", "Age");
        saved.Columns[0].Key = "first";
        saved.Columns[1].Key = "second";
        saved.Columns[1].IsVisible = false;
        var state = KumikoLayoutState.Capture(saved);
        Assert.Equal(new[] { "first", "second" }, state.Columns!.Select(c => c.Key));

        // Same keys, swapped properties: the key decides.
        var target = NewSource("Age", "Name");
        target.Columns[0].Key = "second";
        target.Columns[1].Key = "first";
        state.ApplyTo(target);

        Assert.Equal(new[] { "Name", "Age" }, Order(target));
        Assert.False(Col(target, "Age").IsVisible);
    }

    [Fact]
    public void Capture_LeavesOutColumnsWithoutAName()
    {
        var source = NewSource("Name");
        source.SetColumns(source.Columns.Append(new KumikoColumn { Header = "Actions" }).ToList());

        Assert.Equal(new[] { "Name" }, KumikoLayoutState.Capture(source).Columns!.Select(c => c.Key));
    }

    [Fact]
    public void ApplyTo_RestoresSavedWidthOnAStarColumn_AsFixed()
    {
        var target = NewSource("Name");
        target.Columns[0].SizeMode = ColumnSizeMode.Star;

        new KumikoLayoutState
        {
            Columns = new() { new KumikoColumnLayoutState { Key = "Name", SizeMode = ColumnSizeMode.Fixed, Width = 210 } },
        }.ApplyTo(target);

        Assert.Equal(ColumnSizeMode.Fixed, target.Columns[0].SizeMode);
        Assert.Equal(210, target.Columns[0].Width);
    }

    [Fact]
    public void ApplyTo_ReusesAnExistingGroupDescription_WithItsSelectors()
    {
        var target = NewSource("Name", "City");
        Func<object?, object?> selector = v => v?.ToString()?[..1];
        var existing = new GroupDescription("City") { KeySelector = selector };
        target.AddGroupDescription(new GroupDescription("Name"));
        target.AddGroupDescription(existing);

        new KumikoLayoutState
        {
            Columns = null,
            Groups = new() { new KumikoGroupLayoutState { PropertyName = "City", SortDirection = KumikoSortDirection.Descending } },
        }.ApplyTo(target);

        var group = Assert.Single(target.GroupDescriptions);
        Assert.Same(existing, group);
        Assert.Same(selector, group.KeySelector);
        Assert.Equal(KumikoSortDirection.Descending, group.GroupSortDirection);
    }

    [Fact]
    public void ApplyTo_NullListsLeaveThatPartAlone()
    {
        var target = NewSource("Name", "Age");
        target.SetSort(target.Columns[1], KumikoSortDirection.Ascending);
        target.AddGroupDescription(new GroupDescription("Age"));

        new KumikoLayoutState { Columns = null, Groups = null }.ApplyTo(target);

        Assert.Equal(KumikoSortDirection.Ascending, target.Columns[1].SortDirection);
        Assert.Single(target.GroupDescriptions);

        new KumikoLayoutState().ApplyTo(target); // empty lists: no sort, no grouping
        Assert.Equal(KumikoSortDirection.None, target.Columns[1].SortDirection);
        Assert.Empty(target.GroupDescriptions);
    }

    [Fact]
    public void ApplyTo_RaisesOneViewChanged()
    {
        string json = KumikoLayoutState.Capture(Customized()).ToJson();
        var target = NewSource("Name", "Age", "City", "Joined", "Active");
        var changes = new List<KumikoViewChangedEventArgs>();
        target.ViewChanged += (_, e) => changes.Add(e);

        KumikoLayoutState.FromJson(json).ApplyTo(target);

        Assert.Equal(KumikoViewChangeKind.Rebuild, Assert.Single(changes).Kind);
    }

    [Fact]
    public void FromJson_ToleratesUnknownAndMissingProperties()
    {
        var state = KumikoLayoutState.FromJson("""
            {
              "version": 7,
              "futureSetting": { "x": 1 },
              "columns": [
                { "key": "Age", "sortDirection": "Descending", "sortOrder": 1, "somethingNew": true },
                { "key": "Name" },
                null,
                { "isVisible": false }
              ],
            }
            """);

        Assert.Equal(7, state.Version);
        Assert.NotNull(state.Groups);
        Assert.Empty(state.Groups);
        var name = state.Columns![1];
        Assert.True(name.IsVisible);
        Assert.Null(name.Width);

        var target = NewSource("Name", "Age");
        state.ApplyTo(target); // the null entry and the one without a key are skipped

        Assert.Equal(new[] { "Age", "Name" }, Order(target));
        Assert.Equal(KumikoSortDirection.Descending, Col(target, "Age").SortDirection);
    }

    [Fact]
    public void FromJson_RejectsInvalidJson()
    {
        Assert.Throws<ArgumentNullException>(() => KumikoLayoutState.FromJson(null!));
        Assert.ThrowsAny<JsonException>(() => KumikoLayoutState.FromJson("null"));
        Assert.ThrowsAny<JsonException>(() => KumikoLayoutState.FromJson("{ \"columns\": 5 }"));
        Assert.ThrowsAny<JsonException>(() => KumikoLayoutState.FromJson("not json"));
    }

    [Theory]
    [InlineData(null, KumikoFilterValueKind.None)]
    [InlineData("Rome", KumikoFilterValueKind.Text)]
    [InlineData(true, KumikoFilterValueKind.Boolean)]
    [InlineData(42L, KumikoFilterValueKind.Number)]
    [InlineData(-0.125, KumikoFilterValueKind.Number)]
    public void FilterConditionValues_RoundTripWithTheirKind(object? value, KumikoFilterValueKind kind)
    {
        var source = NewSource("Name");
        var column = source.Columns[0];
        source.SetColumnFilter(column, new FilterDescription(column)
        {
            Condition1 = new FilterCondition { TextOperator = TextFilterOperator.Equals, Value = value },
        });

        var state = KumikoLayoutState.FromJson(KumikoLayoutState.Capture(source).ToJson());
        Assert.Equal(kind, state.Columns![0].Filter!.Condition1!.ValueKind);

        var target = NewSource("Name");
        state.ApplyTo(target);
        var restored = target.Columns[0].ActiveFilter!.Condition1!.Value;
        if (value is long or double)
            Assert.Equal(Convert.ToDouble(value), restored);
        else
            Assert.Equal(value, restored);
    }

    [Fact]
    public void DateFilterValues_RoundTripAsDateTime()
    {
        var source = NewSource("Joined");
        var column = source.Columns[0];
        source.SetColumnFilter(column, new FilterDescription(column)
        {
            Condition1 = new FilterCondition { Value = new DateOnly(2024, 3, 9) },
            Condition2 = new FilterCondition { Value = new DateTimeOffset(2024, 3, 9, 13, 30, 0, TimeSpan.FromHours(5)) },
        });

        var target = NewSource("Joined");
        KumikoLayoutState.FromJson(KumikoLayoutState.Capture(source).ToJson()).ApplyTo(target);

        var filter = target.Columns[0].ActiveFilter!;
        Assert.Equal(new DateTime(2024, 3, 9), filter.Condition1!.Value);
        Assert.Equal(new DateTime(2024, 3, 9, 13, 30, 0), filter.Condition2!.Value);
    }
}
